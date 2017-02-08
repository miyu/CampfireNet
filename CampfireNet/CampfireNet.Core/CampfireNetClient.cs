//#define CN_DEBUG

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using CampfireNet.IO;
using CampfireNet.IO.Transport;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Channels;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet {
   public class CampfireNetClient {
      private readonly IBluetoothAdapter bluetoothAdapter;
      private readonly BroadcastMessageSerializer broadcastMessageSerializer;
      private readonly ClientMerkleTreeFactory merkleTreeFactory;
      private readonly MerkleTree<BroadcastMessage> localMerkleTree;

      public CampfireNetClient(IBluetoothAdapter bluetoothAdapter, BroadcastMessageSerializer broadcastMessageSerializer, ClientMerkleTreeFactory merkleTreeFactory) {
         this.bluetoothAdapter = bluetoothAdapter;
         this.broadcastMessageSerializer = broadcastMessageSerializer;
         this.merkleTreeFactory = merkleTreeFactory;
         this.localMerkleTree = merkleTreeFactory.CreateForLocal();
      }

      public event BroadcastReceivedEventHandler BroadcastReceived;
      public Guid AdapterId => bluetoothAdapter.AdapterId;

      public async Task BroadcastAsync(BroadcastMessage message) {
         var localInsertionResult = await localMerkleTree.TryInsertAsync(message);
         if (localInsertionResult.Item1) {
            BroadcastReceived?.Invoke(new BroadcastReceivedEventArgs(null, message));
         }
      }

      public async Task RunAsync() {
         DiscoverAsync().Forget();
      }

      public async Task DiscoverAsync() {
         var rateLimit = ChannelFactory.Timer(5000, 5000);
         var connectedNeighborContextsByAdapterId = new ConcurrentDictionary<Guid, NeighborConnectionContext>();
         while (true) {
            Debug("Starting discovery round!");
            var neighbors = await bluetoothAdapter.DiscoverAsync();
            try {
               await Task.WhenAll(
                  neighbors.Where(neighbor => !neighbor.IsConnected)
                           .Where(neighbor => !connectedNeighborContextsByAdapterId.ContainsKey(neighbor.AdapterId))
                           .Select(neighbor => ChannelsExtensions.Go(async () => {
                              Debug("Attempt to connect to: {0}", neighbor.AdapterId);
                              var connected = await neighbor.TryHandshakeAsync();
                              if (!connected) {
                                 Debug("Failed to connect to: {0}", neighbor.AdapterId);
                                 return;
                              }
                              Debug("Successfully connected to: {0}", neighbor.AdapterId);

                              //                           Console.WriteLine("Discovered neighbor: " + neighbor.AdapterId);
                              var remoteMerkleTree = merkleTreeFactory.CreateForNeighbor(neighbor.AdapterId.ToString("N"));
                              var connectionContext = new NeighborConnectionContext(bluetoothAdapter, neighbor, broadcastMessageSerializer, localMerkleTree, remoteMerkleTree);
                              connectedNeighborContextsByAdapterId.AddOrThrow(neighbor.AdapterId, connectionContext);
                              connectionContext.BroadcastReceived += HandleBroadcastReceived;
                              connectionContext.Start(() => {
                                 Debug("Connection Context Torn Down: {0}", neighbor.AdapterId);

                                 connectionContext.BroadcastReceived -= HandleBroadcastReceived;
                                 connectedNeighborContextsByAdapterId.RemoveOrThrow(neighbor.AdapterId);
                              });
                           }))
               );
            } catch (Exception e) {
               Debug("Discovery threw!");
               Debug(e.ToString());
            }
            Debug("Ending discovery round!");
            await ChannelsExtensions.ReadAsync(rateLimit);
         }
      }

      /// <summary>
      /// Dispatches BroadcastReceived from neighbor object to client subscribers
      /// </summary>
      /// <param name="args"></param>
      private void HandleBroadcastReceived(BroadcastReceivedEventArgs args) {
         BroadcastReceived?.Invoke(args);
      }

      private void Debug(string s, params object[] args) {
#if CN_DEBUG
         Console.WriteLine(s, args);
#endif
      }
   }
}