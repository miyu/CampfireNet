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
            await ChannelsExtensions.ReadAsync(rateLimit);

            Console.WriteLine("Starting discovery round!");
            var neighbors = await bluetoothAdapter.DiscoverAsync();
            try {
               await Task.WhenAll(
                  neighbors.Where(neighbor => !neighbor.IsConnected)
                           .Where(neighbor => !connectedNeighborContextsByAdapterId.ContainsKey(neighbor.AdapterId))
                           .Select(neighbor => ChannelsExtensions.Go(async () => {
                              Console.WriteLine("Attempt to connect to: " + neighbor.AdapterId);
                              var connected = await neighbor.TryHandshakeAsync();
                              if (!connected) {
                                 Console.WriteLine("Failed to connect to: " + neighbor.AdapterId);
                                 return;
                              }
                              Console.WriteLine("Successfully connected to: " + neighbor.AdapterId);

                              //                           Console.WriteLine("Discovered neighbor: " + neighbor.AdapterId);
                              var remoteMerkleTree = merkleTreeFactory.CreateForNeighbor(neighbor.AdapterId.ToString("N"));
                              var connectionContext = new NeighborConnectionContext(bluetoothAdapter, neighbor, broadcastMessageSerializer, localMerkleTree, remoteMerkleTree);
                              connectedNeighborContextsByAdapterId.AddOrThrow(neighbor.AdapterId, connectionContext);
                              connectionContext.BroadcastReceived += HandleBroadcastReceived;
                              connectionContext.Start(() => {
                                 connectionContext.BroadcastReceived -= HandleBroadcastReceived;
                                 connectedNeighborContextsByAdapterId.RemoveOrThrow(neighbor.AdapterId);
                              });
                           }))
               );
            } catch (Exception e) {
               Console.WriteLine("Discovery threw!");
               Console.WriteLine(e);
            }
            Console.WriteLine("Ending discovery round!");
         }
      }

      /// <summary>
      /// Dispatches BroadcastReceived from neighbor object to client subscribers
      /// </summary>
      /// <param name="args"></param>
      private void HandleBroadcastReceived(BroadcastReceivedEventArgs args) {
         BroadcastReceived?.Invoke(args);
      }
   }
}