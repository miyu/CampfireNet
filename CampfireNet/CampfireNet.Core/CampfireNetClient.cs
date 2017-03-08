#define CN_DEBUG
//#define FAST

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CampfireNet.Identities;
using CampfireNet.IO;
using CampfireNet.IO.Transport;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Channels;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet {
   public static class HackGlobals {
      public static bool DisableChainOfTrustCheck { get; set; }
   }

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

      public event MessageReceivedEventHandler MessageSent;
      public event MessageReceivedEventHandler MessageReceived;
      public Guid AdapterId => bluetoothAdapter.AdapterId;
      public Identity Identity => identity;
      public IdentityManager IdentityManager => identity.IdentityManager;

      public async Task BroadcastAsync(byte[] payload) {
         var messageDto = identity.EncodePacket(payload, null);

         var localInsertionResult = await localMerkleTree.TryInsertAsync(messageDto).ConfigureAwait(false);
         if (localInsertionResult.Item1) {
            // "Decrypt the message"
            MessageSent?.Invoke(new MessageReceivedEventArgs(
               null,
               new BroadcastMessage {
                  SourceId = IdentityHash.GetFlyweight(identity.PublicIdentityHash),
                  DestinationId = IdentityHash.GetFlyweight(Identity.BROADCAST_ID),
                  DecryptedPayload = payload,
                  Dto = messageDto
               }
            ));
         }
      }

      public async Task UnicastAsync(IdentityHash destinationId, byte[] payload) {
         var trustChainNode = identity.IdentityManager.LookupIdentity(destinationId.Bytes.ToArray());

         var messageDto = identity.EncodePacket(payload, trustChainNode.ThisId);
         var localInsertionResult = await localMerkleTree.TryInsertAsync(messageDto).ConfigureAwait(false);
         if (localInsertionResult.Item1) {
            // "Decrypt the message"
            MessageSent?.Invoke(new MessageReceivedEventArgs(
               null,
               new BroadcastMessage {
                  SourceId = IdentityHash.GetFlyweight(identity.PublicIdentityHash),
                  DestinationId = destinationId,
                  DecryptedPayload = payload,
                  Dto = messageDto
               }
            ));
         }
      }

      public async Task MulticastAsync(IdentityHash destinationId, byte[] payload) {
         byte[] symmetricKey;
         if (!identity.IdentityManager.TryLookupMulticastKey(destinationId, out symmetricKey)) {
            throw new InvalidStateException("Attempted to multicast to destination of unknown key!");
         }

         var messageDto = identity.EncodePacket(payload, symmetricKey);
         var localInsertionResult = await localMerkleTree.TryInsertAsync(messageDto).ConfigureAwait(false);
         if (localInsertionResult.Item1) {
            // "Decrypt the message"
            MessageSent?.Invoke(new MessageReceivedEventArgs(
               null,
               new BroadcastMessage {
                  SourceId = IdentityHash.GetFlyweight(identity.PublicIdentityHash),
                  DestinationId = destinationId,
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
#if FAST
         var rateLimit = ChannelFactory.Timer(1000);
#else
         var rateLimit = ChannelFactory.Timer(5000, 5000);
#endif
         var connectedNeighborContextsByAdapterId = new ConcurrentDictionary<Guid, NeighborConnectionContext>();
         while (true) {
            Debug("Starting discovery round!");
            var discoveryStartTime = DateTime.Now;
            var neighbors = await bluetoothAdapter.DiscoverAsync().ConfigureAwait(false);
            var discoveryDurationSeconds = Math.Max(10, 3 * (DateTime.Now - discoveryStartTime).TotalSeconds);
            try {
               var neighborsToConnectTo = new List<IBluetoothNeighbor>();
               foreach (var neighbor in neighbors) {
                  if (neighbor.IsConnected) {
                     Debug("Connection Candidate: {0} already connected.", neighbor.AdapterId);
                     continue;
                  }

                  if (connectedNeighborContextsByAdapterId.ContainsKey(neighbor.AdapterId)) {
                     Debug("Connection Candidate: {0} already has connected context.", neighbor.AdapterId);
                     continue;
                  }

                  Debug("Connection Candidate: {0} looks like a go.", neighbor.AdapterId);
                  neighborsToConnectTo.Add(neighbor);
               }
               await Task.WhenAll(
                  neighborsToConnectTo.Select(neighbor => ChannelsExtensions.Go(async () => {
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
               )
               .ConfigureAwait(false);
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
         MessageReceived?.Invoke(args);
      }

      private void Debug(string s, params object[] args) {
#if CN_DEBUG
         Console.WriteLine(s, args);
#endif
      }
   }
}