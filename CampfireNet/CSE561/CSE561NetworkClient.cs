#define CN_DEBUG

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
using CampfireNet.Utilities.Collections;
using CampfireNet.Utilities.Merkle;
using CSE561;

namespace CampfireNet {
   public class CSE561CampfireNetClient {
      private readonly Identity identity;
      private readonly IBluetoothAdapter bluetoothAdapter;
      private readonly BroadcastMessageSerializer broadcastMessageSerializer;
      private readonly ClientMerkleTreeFactory merkleTreeFactory;
      private readonly CSE561Overnet overnet;
      private readonly MerkleTree<BroadcastMessageDto> localMerkleTree;
      private readonly ConcurrentDictionary<Guid, MerkleTree<BroadcastMessageDto>> localToRemoteMerkleTrees = new ConcurrentDictionary<Guid, MerkleTree<BroadcastMessageDto>>();
      private static readonly ConcurrentDictionary<IdentityHash, Guid> ihLookup = new ConcurrentDictionary<IdentityHash, Guid>();
      private readonly ConcurrentSet<string> proxied = new ConcurrentSet<string>();

      public CSE561CampfireNetClient(Identity identity, IBluetoothAdapter bluetoothAdapter, BroadcastMessageSerializer broadcastMessageSerializer, ClientMerkleTreeFactory merkleTreeFactory, CSE561Overnet overnet) {
         this.identity = identity;
         this.bluetoothAdapter = bluetoothAdapter;
         this.broadcastMessageSerializer = broadcastMessageSerializer;
         this.merkleTreeFactory = merkleTreeFactory;
         this.overnet = overnet;
         this.localMerkleTree = merkleTreeFactory.CreateForLocal();
         ihLookup[IdentityHash.GetFlyweight(identity.PublicIdentityHash)] = bluetoothAdapter.AdapterId;
      }

      public event MessageReceivedEventHandler MessageSent;
      public event MessageReceivedEventHandler MessageReceived;
      public event MessageReceivedEventHandler UndecryptableMessageReceived;
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

      public async Task<BroadcastMessageDto> UnicastAsync(IdentityHash destinationId, byte[] payload) {
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
         RouteUnicast(messageDto);
         return messageDto;
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
         var rateLimit = ChannelFactory.Timer(1000); // 5000, 5000);
         var connectedNeighborContextsByAdapterId = new ConcurrentDictionary<Guid, CSE561NeighborConnectionContext>();
         while (true) {
            overnet.UpdateConnectivities(
               AdapterId,
               localToRemoteMerkleTrees.ToDictionary(
                  kvp => kvp.Key,
                  kvp => 1.0));
//            Debug("Starting discovery round!");
            var discoveryStartTime = DateTime.Now;
            var neighbors = await bluetoothAdapter.DiscoverAsync().ConfigureAwait(false);
            var discoveryDurationSeconds = Math.Max(10, 3 * (DateTime.Now - discoveryStartTime).TotalSeconds);
            try {
               var neighborsToConnectTo = new List<IBluetoothNeighbor>();
               foreach (var neighbor in neighbors) {
                  if (neighbor.IsConnected) {
//                     Debug("Connection Candidate: {0} already connected.", neighbor.AdapterId);
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
                               var localForRemoteMerkleTree = merkleTreeFactory.CreateForNeighbor("LFN_" + neighbor.AdapterId.ToString("N"));
                               localToRemoteMerkleTrees[neighbor.AdapterId] = localForRemoteMerkleTree;

                               var remoteMerkleTree = merkleTreeFactory.CreateForNeighbor("N_" + neighbor.AdapterId.ToString("N"));
                               var connectionContext = new CSE561NeighborConnectionContext(identity, bluetoothAdapter, neighbor, broadcastMessageSerializer, localForRemoteMerkleTree, remoteMerkleTree);
                               connectedNeighborContextsByAdapterId.AddOrThrow(neighbor.AdapterId, connectionContext);
                               connectionContext.BroadcastReceived += HandleBroadcastReceived;
                               connectionContext.UndecryptableMessageReceived += HandleUndecryptableMessageReceived;
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
//            Debug("Ending discovery round!");
            await rateLimit.ReadAsync().ConfigureAwait(false);
         }
      }

      /// <summary>
      /// Dispatches BroadcastReceived from neighbor object to client subscribers
      /// </summary>
      /// <param name="args"></param>
      private void HandleBroadcastReceived(MessageReceivedEventArgs args) {
         MessageReceived?.Invoke(args);
      }

      private void HandleUndecryptableMessageReceived(MessageReceivedEventArgs args) {
         UndecryptableMessageReceived?.Invoke(args);
         RouteUnicast(args.Message.Dto);
      }

      private void RouteUnicast(BroadcastMessageDto mdto) {
         var sig = Helpers.X(mdto.Signature);
         if (!proxied.TryAdd(sig)) return;
         var raid = ihLookup[IdentityHash.GetFlyweight(mdto.DestinationIdHash)];
         var nexts = overnet.ComputeRoutesAndCosts(AdapterId, raid);
         foreach (var x in nexts.Take(3)) {
            localToRemoteMerkleTrees[x.Item1].TryInsertAsync(mdto);
            Console.WriteLine($"L=>R from {AdapterId} to {raid}");
         }
      }
      private void Debug(string s, params object[] args) {
#if CN_DEBUG
         Console.WriteLine(s, args);
#endif
      }
   }

   public static class Helpers  {
      public static string X(IdentityHash h) => X(h.Bytes);
      public static string X(IReadOnlyList<byte> arg) => string.Join("", arg.Map(x => x.ToString("X").PadLeft(2, '0')));
   }
}