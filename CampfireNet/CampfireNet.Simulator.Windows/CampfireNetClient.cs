using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.Utilities;
using CampfireNet.Utilities.ChannelsExtensions;
using CampfireNet.Utilities.Collections;

namespace CampfireNet.Simulator {
   public class CampfireNetClient {
      private readonly ConcurrentSet<byte[]> haves = new ConcurrentSet<byte[]>();
      private readonly ConcurrentSet<IBluetoothNeighbor> discoveredNeighbors = new ConcurrentSet<IBluetoothNeighbor>();
      private readonly ConcurrentDictionary<IBluetoothNeighbor, Channel<bool>> neighborConnectedChannel = new ConcurrentDictionary<IBluetoothNeighbor, Channel<bool>>();
      private readonly IBluetoothAdapter bluetoothAdapter;
      private Task discoveryTask;

      public CampfireNetClient(IBluetoothAdapter bluetoothAdapter) {
         this.bluetoothAdapter = bluetoothAdapter;
      }

      public int Number { get; set; }

      public async Task RunAsync() {
         discoveryTask = DiscoverAsync().Forgettable();
      }

      public async Task DiscoverAsync() {
         while (true) {
            var neighbors = await bluetoothAdapter.DiscoverAsync();
            await Task.WhenAll(
               neighbors.Where(neighbor => !neighbor.IsConnected)
                        .Select(neighbor => ChannelsExtensions.Go(async () => {
                           var connected = await neighbor.TryHandshakeAsync();
                           if (connected) {
                              var connectedChannel = neighborConnectedChannel.GetOrAdd(neighbor, add => ChannelFactory.Nonblocking<bool>());

                              // If first time connecting, spin up background task to process neighbor
                              if (discoveredNeighbors.TryAdd(neighbor)) {
                                 HandleConnectionAsync(neighbor, connectedChannel).Forget();
                              }

                              await connectedChannel.WriteAsync(true, CancellationToken.None);
                           }
                        }))
            );
         }
      }

      private async Task HandleConnectionAsync(IBluetoothNeighbor neighbor, Channel<bool> connectedChannel) {
         await connectedChannel.ReadAsync(CancellationToken.None, x => true);

         var inboundChannel = neighbor.InboundChannel;
         var syncTimerChannel = ChannelFactory.Timer(500);

         while (true) {
            try {
               await new Select {
                  ChannelsExtensions.Case(inboundChannel, message => {
                     if (!haves.TryAdd(message))
                        return;

                     var n = BitConverter.ToInt32(message, 0);
                     if (n > Number) {
                        Number = n;
                     }

//                     foreach (var peer in discoveredNeighbors) {
//                        var forget = peer.TrySendAsync(message);
//                     }
                  }),
                  ChannelsExtensions.Case(syncTimerChannel, async message => {
                     // periodically send our number to peer.
                     await neighbor.TrySendAsync(BitConverter.GetBytes(Number));
                  })
               }.ConfigureAwait(false);
            } catch (NotConnectedException) {
               await connectedChannel.ReadAsync(CancellationToken.None, x => true);
            }
         }
      }
   }
}