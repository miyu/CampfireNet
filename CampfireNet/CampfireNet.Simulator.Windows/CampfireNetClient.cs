using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.Utilities;
using CampfireNet.Utilities.AsyncPrimatives;
using CampfireNet.Utilities.ChannelsExtensions;
using CampfireNet.Utilities.Collections;
using CampfireNet.Utilities.Merkle;
using static CampfireNet.Utilities.ChannelsExtensions.ChannelsExtensions;

namespace CampfireNet.Simulator {
   public class CampfireNetClient {
      private readonly IBluetoothAdapter bluetoothAdapter;
      private readonly ClientMerkleTreeFactory merkleTreeFactory;
      private readonly MerkleTree<BroadcastMessage> localMerkleTree;

      public CampfireNetClient(IBluetoothAdapter bluetoothAdapter, ClientMerkleTreeFactory merkleTreeFactory) {
         this.bluetoothAdapter = bluetoothAdapter;
         this.merkleTreeFactory = merkleTreeFactory;
         this.localMerkleTree = merkleTreeFactory.CreateForLocal();
      }

      public int Value
      {
         get { return localMerkleTree.GetRootHashAsync().Result != null ? 1 : 0; }
      }

      public async Task BroadcastAsync(BroadcastMessage message) {
         await localMerkleTree.InsertAsync(message);
      }

      public async Task RunAsync() {
         DiscoverAsync().Forget();
      }

      public async Task DiscoverAsync() {
         var rateLimit = ChannelFactory.Timer(5000, 5000);
         var connectedNeighborContextsByAdapterId = new ConcurrentDictionary<Guid, NeighborConnectionContext>();
         while (true) {
            await rateLimit.ReadAsync();

            var neighbors = await bluetoothAdapter.DiscoverAsync();
            await Task.WhenAll(
               neighbors.Where(neighbor => !neighbor.IsConnected)
                        .Where(neighbor => !connectedNeighborContextsByAdapterId.ContainsKey(neighbor.AdapterId))
                        .Select(neighbor => Go(async () => {
                           var connected = await neighbor.TryHandshakeAsync();
                           if (!connected) return;

                           var remoteMerkleTree = merkleTreeFactory.CreateForNeighbor(neighbor.AdapterId.ToString("N"));
                           var connectionContext = new NeighborConnectionContext(bluetoothAdapter, neighbor, localMerkleTree, remoteMerkleTree);
                           connectedNeighborContextsByAdapterId.AddOrThrow(neighbor.AdapterId, connectionContext);
                           connectionContext.Start(() => {
                              connectedNeighborContextsByAdapterId.RemoveOrThrow(neighbor.AdapterId);
                           });
                        }))
            );
         }
      }
   }

   public class BinaryLatchChannel : ReadableChannel<bool> {
      private readonly object synchronization = new object();
      private bool isClosed = false;
      private AsyncLatch latch = new AsyncLatch();

      public BinaryLatchChannel(bool isClosed = false) {
         SetIsClosed(isClosed);
      }

      public bool IsClosed => isClosed;
      public int Count => isClosed ? 1 : 0;

      public bool TryRead(out bool message) {
         return message = isClosed;
      }

      public async Task<bool> ReadAsync(CancellationToken cancellationToken, Func<bool, bool> acceptanceTest) {
         Thread.MemoryBarrier();

         while (true) {
            await latch.WaitAsync(cancellationToken);
            if (acceptanceTest(true)) {
               return true;
            }
         }
      }

      public void SetIsClosed(bool value) {
         lock (synchronization) {
            if (isClosed == value) return;
            isClosed = value;

            if (value) {
               latch.Set();
            } else {
               latch = new AsyncLatch();
            }
         }
      }
   }

   public class DisconnectableChannel<T> : Channel<T> {
      private readonly ReadableChannel<bool> disconnectedChannel;
      private readonly Channel<T> dataChannel;

      public DisconnectableChannel(ReadableChannel<bool> disconnectedChannel, Channel<T> dataChannel) {
         this.disconnectedChannel = disconnectedChannel;
         this.dataChannel = dataChannel;
      }

      public int Count => dataChannel.Count;

      public bool TryRead(out T message) {
         return dataChannel.TryRead(out message);
      }

      public async Task<T> ReadAsync(CancellationToken cancellationToken, Func<T, bool> acceptanceTest) {
         bool disconnected = false;
         T result = default(T);
         await new Select {
            Case(disconnectedChannel, () => {
               disconnected = true;
            }),
            Case(dataChannel, data => {
               result = data;
            }, acceptanceTest)
         }.WaitAsync(cancellationToken);
         if (disconnected) {
            throw new NotConnectedException();
         }
         return result;
      }

      public Task WriteAsync(T message, CancellationToken cancellationToken) {
         return dataChannel.WriteAsync(message, cancellationToken);
      }
   }

   public class ClientMerkleTreeFactory {
      private readonly CampfireNetPacketMerkleOperations packetMerkleOperations;
      private readonly ICampfireNetObjectStore objectStore;

      public ClientMerkleTreeFactory(CampfireNetPacketMerkleOperations packetMerkleOperations, ICampfireNetObjectStore objectStore) {
         this.packetMerkleOperations = packetMerkleOperations;
         this.objectStore = objectStore;
      }

      public MerkleTree<BroadcastMessage> CreateForLocal() {
         return new MerkleTree<BroadcastMessage>("local", packetMerkleOperations, objectStore);
      }

      public MerkleTree<BroadcastMessage> CreateForNeighbor(string id) {
         return new MerkleTree<BroadcastMessage>(id, packetMerkleOperations, objectStore);
      }
   }
}