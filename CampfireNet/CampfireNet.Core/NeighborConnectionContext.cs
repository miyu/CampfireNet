#define CN_DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using CampfireNet.Identities;
using CampfireNet.IO;
using CampfireNet.IO.Packets;
using CampfireNet.IO.Transport;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Channels;
using CampfireNet.Utilities.Merkle;
using static CampfireNet.Utilities.Channels.ChannelsExtensions;

namespace CampfireNet {
	public static class DebugConsole {
		private static readonly object g_printLock = new object();

		public static void WriteLine(string s, ConsoleColor? foreground, ConsoleColor? background, params object[] args) {
#if CN_DEBUG
			lock (g_printLock) {
				var fc = Console.ForegroundColor;
				var bc = Console.BackgroundColor;
				Console.ForegroundColor = foreground ?? fc;
				Console.BackgroundColor = background ?? bc;
				Console.WriteLine(s, args);
				Console.ForegroundColor = fc;
				Console.BackgroundColor = bc;
			}
#endif
		}
	}

	public class NeighborConnectionContext {
		private readonly WirePacketSerializer serializer = new WirePacketSerializer();

		private readonly BinaryLatchChannel disconnectLatchChannel = new BinaryLatchChannel();
		private readonly DisconnectableChannel<HavePacket, NotConnectedException> haveChannel;
		private readonly DisconnectableChannel<NeedPacket, NotConnectedException> needChannel;
		private readonly DisconnectableChannel<GivePacket, NotConnectedException> giveChannel;
		private readonly DisconnectableChannel<WhoisPacket, NotConnectedException> whoisChannel;
		private readonly DisconnectableChannel<IdentPacket, NotConnectedException> identChannel;
		private readonly DisconnectableChannel<DonePacket, NotConnectedException> doneChannel;

		private readonly Identity identity;
		private readonly IBluetoothAdapter bluetoothAdapter;
		private readonly IBluetoothNeighbor neighbor;

		private readonly BroadcastMessageSerializer broadcastMessageSerializer;

		private readonly MerkleTree<BroadcastMessageDto> localMerkleTree;
		private readonly MerkleTree<BroadcastMessageDto> remoteMerkleTree;

		public NeighborConnectionContext(
			Identity identity,
			IBluetoothAdapter bluetoothAdapter,
			IBluetoothNeighbor neighbor,
			BroadcastMessageSerializer broadcastMessageSerializer,
			MerkleTree<BroadcastMessageDto> localMerkleTree,
			MerkleTree<BroadcastMessageDto> remoteMerkleTree
		) {
			this.haveChannel = new DisconnectableChannel<HavePacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<HavePacket>());
			this.needChannel = new DisconnectableChannel<NeedPacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<NeedPacket>());
			this.giveChannel = new DisconnectableChannel<GivePacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<GivePacket>());
			this.whoisChannel = new DisconnectableChannel<WhoisPacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<WhoisPacket>());
			this.identChannel = new DisconnectableChannel<IdentPacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<IdentPacket>());
			this.doneChannel = new DisconnectableChannel<DonePacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<DonePacket>());

			this.identity = identity;
			this.bluetoothAdapter = bluetoothAdapter;
			this.neighbor = neighbor;
			this.broadcastMessageSerializer = broadcastMessageSerializer;
			this.localMerkleTree = localMerkleTree;
			this.remoteMerkleTree = remoteMerkleTree;
		}

		public Identity Identity => identity;
		public IdentityManager IdentityManager => identity.IdentityManager;

		public event BroadcastReceivedEventHandler BroadcastReceived;

		public void Start(Action shutdownCallback) {
			Task.WhenAll(
				RouterTaskStart(),
				SynchronizationLoopTaskStart()
			).ContinueWith(t => {
				shutdownCallback();
			}).Forget();
		}

		private async Task RouterTaskStart() {
			var inboundChannel = neighbor.InboundChannel;
			try {
				while (true) {
					byte[] packetData = null;
					bool quit = false;
					await new Select {
						Case(ChannelFactory.Timeout(TimeSpan.FromSeconds(30)), () => quit = true),
						Case(inboundChannel, x => packetData = x)
					}.ConfigureAwait(false);

					if (quit) break;

					var packet = serializer.ToObject(packetData);
					switch (packet.GetType().Name) {
						case nameof(HavePacket):
							DebugPrint("Got HAVE {0}", ((HavePacket)packet).MerkleRootHash);
							await ChannelsExtensions.WriteAsync(haveChannel, (HavePacket)packet).ConfigureAwait(false);
							break;
						case nameof(NeedPacket):
							DebugPrint("Got NEED {0}", ((NeedPacket)packet).MerkleRootHash);
							await ChannelsExtensions.WriteAsync(needChannel, (NeedPacket)packet).ConfigureAwait(false);
							break;
						case nameof(GivePacket):
							DebugPrint("Got GIVE {0}", ((GivePacket)packet).NodeHash);
							await ChannelsExtensions.WriteAsync(giveChannel, (GivePacket)packet).ConfigureAwait(false);
							break;
						case nameof(WhoisPacket):
							DebugPrint("Got WHOIS {0}", ((WhoisPacket)packet).IdHash.ToHexString());
							await ChannelsExtensions.WriteAsync(whoisChannel, (WhoisPacket)packet).ConfigureAwait(false);
							break;
						case nameof(IdentPacket):
							DebugPrint("Got IDENT {0}", ((IdentPacket)packet).Id.ToHexString());
							await ChannelsExtensions.WriteAsync(identChannel, (IdentPacket)packet).ConfigureAwait(false);
							break;
						case nameof(DonePacket):
							DebugPrint("Got DONE");
							await ChannelsExtensions.WriteAsync(doneChannel, (DonePacket)packet).ConfigureAwait(false);
							break;
						default:
							throw new InvalidStateException();
					}
				}
			} catch (NotConnectedException) {
				disconnectLatchChannel.SetIsClosed(true);
				try {
					await neighbor.SendAsync(new byte[0]).ConfigureAwait(false);
					throw new InvalidStateException();
				} catch (NotConnectedException) { }
			} finally {
				disconnectLatchChannel.SetIsClosed(true);
				neighbor.Disconnect();
				DebugPrint("Router loop exiting");
			}
		}

		private async Task SynchronizationLoopTaskStart() {
			var isGreater = bluetoothAdapter.AdapterId.CompareTo(neighbor.AdapterId) > 0;
			var rateLimit = ChannelFactory.Timer(5000, 3000);
			try {
				while (true) {
					if (isGreater) {
						await SynchronizeRemoteToLocalAsync().ConfigureAwait(false);
						await SynchronizeLocalToRemoteAsync().ConfigureAwait(false);
					} else {
						await SynchronizeLocalToRemoteAsync().ConfigureAwait(false);
						await SynchronizeRemoteToLocalAsync().ConfigureAwait(false);
					}
					await ChannelsExtensions.ReadAsync(rateLimit).ConfigureAwait(false);
				}
			} catch (NotConnectedException) {
			} finally {
				DebugPrint("Sync loop exiting");
				disconnectLatchChannel.SetIsClosed(true);
				neighbor.Disconnect();
			}
		}

#if __MonoCS__
      private class LinkedList<T> : IEnumerable<T> {
         private Node head;
         private Node tail;
         private int count = 0;
         public int Count => count;
         public void RemoveFirst() {
            head = head.Next;
            if (head == null) tail = null;
            count--;
         }
         public void AddLast(T val) {
            var n = new Node { Value = val };
            if (head == null) {
               head = tail = n;
            } else {
               tail.Next = n;
               tail = n;
            }
            count++;
         }
         public IEnumerator<T> GetEnumerator() {
            var current = head;
            while (current != null) {
               yield return current.Value;
               current = current.Next;
            }
         }

         IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

         private class Node {
            public Node Next { get; set; }
            public T Value { get; set; }
         }
      }
#endif

		private static object g_printLock = new object();

		private void DebugPrint(string s, params object[] args) {
			var colors = new ConsoleColor[] { ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.White };
			var color = colors[Math.Abs(bluetoothAdapter.AdapterId.GetHashCode()) % colors.Length];
			DebugConsole.WriteLine(bluetoothAdapter.AdapterId.ToString("n") + " => " + neighbor.AdapterId.ToString("n") + " " + s, color, ConsoleColor.Black, args);
		}

		private async Task SynchronizeRemoteToLocalAsync() {
			DebugPrint("Enter Remote to Local");
			var have = await ChannelsExtensions.ReadAsync(haveChannel).ConfigureAwait(false);
			DebugPrint("Have is {0}", have.MerkleRootHash);
			var isRemoteRootSyncedLocally = await IsRemoteObjectHeldLocally(have.MerkleRootHash).ConfigureAwait(false);
			DebugPrint("IRRSL {0}", isRemoteRootSyncedLocally);

			if (!isRemoteRootSyncedLocally) {
				var nodesToImport = new List<Tuple<string, MerkleNode>>();
				var neededHashes = new LinkedList<string>();
				neededHashes.AddLast(have.MerkleRootHash);

				while (neededHashes.Count != 0) {
					var hashesReadLocally = new HashSet<string>();
					foreach (var hash in neededHashes) {
						var localNode = await localMerkleTree.GetNodeAsync(hash).ConfigureAwait(false);
						if (localNode != null) {
							nodesToImport.Add(Tuple.Create(hash, localNode));
							hashesReadLocally.Add(hash);
							continue;
						}

						var need = new NeedPacket { MerkleRootHash = hash };
						DebugPrint("SEND NEED {0}", need.MerkleRootHash);
						neighbor.SendAsync(serializer.ToByteArray(need)).Forget();
					}

					foreach (var i in Enumerable.Range(0, neededHashes.Count)) {
						var hash = neededHashes.First();
						neededHashes.RemoveFirst();

						if (hashesReadLocally.Contains(hash)) {
							continue;
						}

						var give = await ChannelsExtensions.ReadAsync(giveChannel).ConfigureAwait(false);
						nodesToImport.Add(Tuple.Create(give.NodeHash, give.Node));

						if (!await IsRemoteObjectHeldLocally(give.Node.LeftHash).ConfigureAwait(false)) {
							neededHashes.AddLast(give.Node.LeftHash);
						}

						if (!await IsRemoteObjectHeldLocally(give.Node.RightHash).ConfigureAwait(false)) {
							neededHashes.AddLast(give.Node.RightHash);
						}
					}
				}

				var broadcastMessagesByNodeHash = nodesToImport.Where(n => n.Item2.TypeTag == MerkleNodeTypeTag.Data)
																			  .ToDictionary(
																				  n => n.Item1,
																				  n => broadcastMessageSerializer.Deserialize(n.Item2.Contents)
																			  );

				var neededSourceIdHashes = broadcastMessagesByNodeHash.Select(kvp => kvp.Value.SourceIdHash)
																						.GroupBy(sourceIdHash => sourceIdHash.ToHexString())
																						.Select(g => new { Bytes = g.First(), Hex = g.Key })
																						.Where(pair => !IdentityManager.IsKnownIdentity(pair.Bytes))
																						.ToList();

				foreach (var neededSourceId in neededSourceIdHashes) {
					var whois = new WhoisPacket { IdHash = neededSourceId.Bytes };
					DebugPrint("SEND WHOIS {0}", neededSourceId.Hex);
					neighbor.SendAsync(serializer.ToByteArray(whois)).Forget();
				}

				foreach (var i in Enumerable.Range(0, neededSourceIdHashes.Count)) {
					var ident = await ChannelsExtensions.ReadAsync(identChannel).ConfigureAwait(false);
					Identity.ValidateAndAdd(ident.TrustChain);
				}

				foreach (var neededSourceId in neededSourceIdHashes) {
					if (!IdentityManager.IsKnownIdentity(neededSourceId.Bytes)) {
						throw new InvalidStateException();
					}
				}

				foreach (var tuple in nodesToImport) {
					var node = tuple.Item2;
					if (node.Descendents == 0 && await localMerkleTree.GetNodeAsync(tuple.Item1).ConfigureAwait(false) == null) {
						var isDataNode = node.TypeTag == MerkleNodeTypeTag.Data;
						BroadcastMessageDto message = isDataNode ? broadcastMessageSerializer.Deserialize(node.Contents) : null;

						var sender = IdentityManager.LookupIdentity(message.SourceIdHash);
						if (message.DestinationIdHash.All(val => val == 0)) {
							if ((sender.HeldPermissions & Permission.Broadcast) == 0) {
								Console.WriteLine("Sender does not have broadcast permissions. Malicious peer!");
								throw new InvalidStateException();
							}
						} else {
							if ((sender.HeldPermissions & Permission.Unicast) == 0) {
								Console.WriteLine("Sender does not have unicast permissions. Malicious peer!");
								throw new InvalidStateException();
							}
						}
					}
				}

				await remoteMerkleTree.ImportAsync(have.MerkleRootHash, nodesToImport).ConfigureAwait(false);
				foreach (var tuple in nodesToImport) {
					var node = tuple.Item2;
					if (node.Descendents == 0 && await localMerkleTree.GetNodeAsync(tuple.Item1).ConfigureAwait(false) == null) {
						var isDataNode = node.TypeTag == MerkleNodeTypeTag.Data;
						BroadcastMessageDto message = isDataNode ? broadcastMessageSerializer.Deserialize(node.Contents) : null;

						var insertionResult = await localMerkleTree.TryInsertAsync(tuple.Item2).ConfigureAwait(false);
						if (insertionResult.Item1 && isDataNode) {
							byte[] decryptedPayload;
							if (identity.TryDecodePayload(message, out decryptedPayload)) {

								byte[] denumberedMessage;
								if (decryptedPayload.Length > 4) {
									denumberedMessage = new byte[decryptedPayload.Length - 4];
									Buffer.BlockCopy(decryptedPayload, 4, denumberedMessage, 0, denumberedMessage.Length - 4);
								} else {
									denumberedMessage = decryptedPayload;
								}
								BroadcastReceived?.Invoke(new BroadcastReceivedEventArgs(neighbor, new BroadcastMessage {
									SourceId = message.SourceIdHash,
									DestinationId = message.DestinationIdHash,
									DecryptedPayload = denumberedMessage,
									Dto = message
								}));
							}
						}
					}
				}
			}

			DebugPrint("SEND DONE");
			await neighbor.SendAsync(serializer.ToByteArray(new DonePacket())).ConfigureAwait(false);
		}

		private async Task SynchronizeLocalToRemoteAsync() {
			DebugPrint("Enter Local to Remote");

			var localRootHash = await localMerkleTree.GetRootHashAsync().ConfigureAwait(false) ?? CampfireNetHash.ZERO_HASH_BASE64;
			var havePacket = new HavePacket { MerkleRootHash = localRootHash };
			DebugPrint("SEND HAVE {0}", havePacket.MerkleRootHash);
			await neighbor.SendAsync(serializer.ToByteArray(havePacket)).ConfigureAwait(false);

			bool done = false;
			while (!done) {
				await new Select {
					ChannelsExtensions.Case(doneChannel, () => {
						done = true;
					}),
					ChannelsExtensions.Case(needChannel, async need => {
//                  Console.WriteLine("RECV NEED " + need.MerkleRootHash);

                  var node = await localMerkleTree.GetNodeAsync(need.MerkleRootHash).ConfigureAwait(false);
						var give = new GivePacket {
							NodeHash = need.MerkleRootHash,
							Node = node
						};
						DebugPrint("SEND GIVE");
						neighbor.SendAsync(serializer.ToByteArray(give)).Forget();
                  //                  Console.WriteLine("EMIT GIVE " + need.MerkleRootHash);
               }),
					ChannelsExtensions.Case(whoisChannel, whois => {
                  // Ideally we send IDs one at a time. However, we are short on time so
                  // we've gone with the simple implementation.
                  var currentIdHash = whois.IdHash;
						var trustChain = new List<TrustChainNode>();
						while (true) {
							var node = IdentityManager.LookupIdentity(currentIdHash);
							trustChain.Add(node);

							if (node.ParentId.SequenceEqual(node.ThisId)) {
								break;
							}

							currentIdHash = node.ParentId;
						}
						trustChain.Reverse();

						DebugPrint("SEND IDENT");
						var ident = new IdentPacket {
							Id = trustChain.Last().ThisId,
							TrustChain = trustChain.ToArray()
						};
						neighbor.SendAsync(serializer.ToByteArray(ident)).Forget();
						Console.WriteLine("EMIT IDENT " + ident.Id.ToHexString());
					})
				}.ConfigureAwait(false);
			}
		}

		private async Task<bool> IsRemoteObjectHeldLocally(string hash) {
			if (hash == CampfireNetHash.ZERO_HASH_BASE64) {
				return true;
			}
			return await remoteMerkleTree.GetNodeAsync(hash).ConfigureAwait(false) != null;
		}
	}
}