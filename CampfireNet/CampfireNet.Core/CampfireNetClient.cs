#define CN_DEBUG

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using CampfireNet.Identities;
using CampfireNet.IO;
using CampfireNet.IO.Transport;
using CampfireNet.Security;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Channels;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet {
	public class CampfireNetClient {
		private readonly Identity identity;
		private readonly IBluetoothAdapter bluetoothAdapter;
		private readonly BroadcastMessageSerializer broadcastMessageSerializer;
		private readonly ClientMerkleTreeFactory merkleTreeFactory;
		private readonly MerkleTree<BroadcastMessageDto> localMerkleTree;

		public CampfireNetClient(Identity identity, IBluetoothAdapter bluetoothAdapter, BroadcastMessageSerializer broadcastMessageSerializer, ClientMerkleTreeFactory merkleTreeFactory) {
			this.identity = identity;
			this.bluetoothAdapter = bluetoothAdapter;
			this.broadcastMessageSerializer = broadcastMessageSerializer;
			this.merkleTreeFactory = merkleTreeFactory;
			this.localMerkleTree = merkleTreeFactory.CreateForLocal();
		}

		public event MessageReceivedEventHandler BroadcastSent;
		public event MessageReceivedEventHandler BroadcastReceived;
		public event MessageReceivedEventHandler UnicastSent;
		public event MessageReceivedEventHandler UnicastReceived;
		public Guid AdapterId => bluetoothAdapter.AdapterId;

		public async Task BroadcastAsync(byte[] payload) {
			var messageDto = identity.EncodePacket(payload, null);

			var localInsertionResult = await localMerkleTree.TryInsertAsync(messageDto).ConfigureAwait(false);
			if (localInsertionResult.Item1) {
            // "Decrypt the message"
            BroadcastSent?.Invoke(new MessageReceivedEventArgs(
					null,
					new BroadcastMessage {
						SourceId = identity.PublicIdentity,
						DestinationId = Identity.BROADCAST_ID,
						DecryptedPayload = payload,
						Dto = messageDto
					}
				));
			}
		}

      public async Task UnicastAsync(byte[] destinationId, byte[] payload) {
         if (destinationId.Length != CryptoUtil.HASH_SIZE) {
            throw new ArgumentException($"Expected DestinationId to be of size {CryptoUtil.HASH_SIZE}");
         }

         var trustChainNode = identity.IdentityManager.LookupIdentity(destinationId);

         var messageDto = identity.EncodePacket(payload, trustChainNode.ThisId);
         var localInsertionResult = await localMerkleTree.TryInsertAsync(messageDto).ConfigureAwait(false);
         if (localInsertionResult.Item1) {
            // "Decrypt the message"
            UnicastSent?.Invoke(new MessageReceivedEventArgs(
               null,
               new BroadcastMessage {
                  SourceId = identity.PublicIdentity,
                  DestinationId = Identity.BROADCAST_ID,
                  DecryptedPayload = payload,
                  Dto = messageDto
               }
            ));
         }
      }

		public async Task RunAsync() {
			try {
				await DiscoverAsync();
			} catch (Exception e) {
				Console.WriteLine("Warning: RunAsync-DiscoverAsync exited!" + e);
			}
		}

		public async Task DiscoverAsync() {
			var rateLimit = ChannelFactory.Timer(1000); // 5000, 5000);
			var connectedNeighborContextsByAdapterId = new ConcurrentDictionary<Guid, NeighborConnectionContext>();
			while (true) {
				Debug("Starting discovery round!");
				var discoveryStartTime = DateTime.Now;
				var neighbors = await bluetoothAdapter.DiscoverAsync().ConfigureAwait(false);
				var discoveryDurationSeconds = Math.Max(10, 3 * (DateTime.Now - discoveryStartTime).TotalSeconds);
				try {
					await Task.WhenAll(
						neighbors.Where(neighbor => !neighbor.IsConnected)
									.Where(neighbor => !connectedNeighborContextsByAdapterId.ContainsKey(neighbor.AdapterId))
									.Select(neighbor => ChannelsExtensions.Go(async () => {
										Debug("Attempt to connect to: {0}", neighbor.AdapterId);
										var connected = await neighbor.TryHandshakeAsync(discoveryDurationSeconds).ConfigureAwait(false);
										if (!connected) {
											Debug("Failed to connect to: {0}", neighbor.AdapterId);
											return;
										}
										Debug("Successfully connected to: {0}", neighbor.AdapterId);

										//                           Console.WriteLine("Discovered neighbor: " + neighbor.AdapterId);
										var remoteMerkleTree = merkleTreeFactory.CreateForNeighbor(neighbor.AdapterId.ToString("N"));
										var connectionContext = new NeighborConnectionContext(identity, bluetoothAdapter, neighbor, broadcastMessageSerializer, localMerkleTree, remoteMerkleTree);
										connectedNeighborContextsByAdapterId.AddOrThrow(neighbor.AdapterId, connectionContext);
										connectionContext.BroadcastReceived += HandleBroadcastReceived;
										connectionContext.Start(() => {
											Debug("Connection Context Torn Down: {0}", neighbor.AdapterId);

											connectionContext.BroadcastReceived -= HandleBroadcastReceived;
											connectedNeighborContextsByAdapterId.RemoveOrThrow(neighbor.AdapterId);
											neighbor.Disconnect();
										});
									}))
					).ConfigureAwait(false);
				} catch (Exception e) {
					Debug("Discovery threw!");
					Debug(e.ToString());
				}
				Debug("Ending discovery round!");
				await ChannelsExtensions.ReadAsync(rateLimit).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Dispatches BroadcastReceived from neighbor object to client subscribers
		/// </summary>
		/// <param name="args"></param>
		private void HandleBroadcastReceived(MessageReceivedEventArgs args) {
         if (args.Message.DestinationId.SequenceEqual(Identity.BROADCAST_ID)) {
            BroadcastReceived?.Invoke(args);
         } else {
            UnicastReceived?.Invoke(args);
         }
		}

		private void Debug(string s, params object[] args) {
#if CN_DEBUG
			Console.WriteLine(s, args);
#endif
		}
	}
}