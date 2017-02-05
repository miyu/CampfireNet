using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
         while (true) {
            var neighbors = await bluetoothAdapter.DiscoverAsync();
            await Task.WhenAll(
               neighbors.Where(neighbor => !neighbor.IsConnected)
                        .Select(neighbor => Go(async () => {
                           var connected = await neighbor.TryHandshakeAsync();
                           if (!connected) return;

                           var remoteMerkleTree = merkleTreeFactory.CreateForNeighbor(neighbor.AdapterId.ToString("N"));
                           var connectionContext = new NeighborConnectionContext(bluetoothAdapter, neighbor, localMerkleTree, remoteMerkleTree);
                           connectionContext.Start();
                        }))
            );
         }
      }
   }

   public class BinaryLatchChannel : ReadableChannel<bool> {
      private readonly object synchronization = new object();
      private bool isClosed = false;
      private AsyncLatch latch = new AsyncLatch();

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

      public async Task SetIsClosedAsync(bool value) {
         lock (synchronization) {
            if (isClosed == value) return;

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
         T result = default(T);
         await new Select {
            Case(disconnectedChannel, () => {
               throw new NotConnectedException();
            }),
            Case(dataChannel, data => {
               result = data;
            }, acceptanceTest)
         }.WaitAsync(cancellationToken);
         return result;
      }

      public Task WriteAsync(T message, CancellationToken cancellationToken) {
         return dataChannel.WriteAsync(message, cancellationToken);
      }
   }

   public class NeighborConnectionContext {
      private readonly WirePacketSerializer serializer = new WirePacketSerializer();

      private readonly BinaryLatchChannel disconnectLatchChannel = new BinaryLatchChannel();
      private readonly DisconnectableChannel<HavePacket> haveChannel;
      private readonly DisconnectableChannel<NeedPacket> needChannel;
      private readonly DisconnectableChannel<GivePacket> giveChannel;
      private readonly DisconnectableChannel<DonePacket> doneChannel;

      private readonly IBluetoothAdapter bluetoothAdapter;
      private readonly IBluetoothNeighbor neighbor;

      private readonly MerkleTree<BroadcastMessage> localMerkleTree;
      private readonly MerkleTree<BroadcastMessage> remoteMerkleTree;

      public NeighborConnectionContext(
         IBluetoothAdapter bluetoothAdapter, 
         IBluetoothNeighbor neighbor,
         MerkleTree<BroadcastMessage> localMerkleTree,
         MerkleTree<BroadcastMessage> remoteMerkleTree
      ) {
         this.haveChannel = new DisconnectableChannel<HavePacket>(disconnectLatchChannel, ChannelFactory.Nonblocking<HavePacket>());
         this.needChannel = new DisconnectableChannel<NeedPacket>(disconnectLatchChannel, ChannelFactory.Nonblocking<NeedPacket>());
         this.giveChannel = new DisconnectableChannel<GivePacket>(disconnectLatchChannel, ChannelFactory.Nonblocking<GivePacket>());
         this.doneChannel = new DisconnectableChannel<DonePacket>(disconnectLatchChannel, ChannelFactory.Nonblocking<DonePacket>());

         this.bluetoothAdapter = bluetoothAdapter;
         this.neighbor = neighbor;
         this.localMerkleTree = localMerkleTree;
         this.remoteMerkleTree = remoteMerkleTree;
      }

      public void Start() {
         RouterTaskStart().Forget();
         SynchronizationLoopTaskStart().Forget();
      }

      private async Task RouterTaskStart() {
         var inboundChannel = neighbor.InboundChannel;
         try {
            while (true) {
               var packetData = await inboundChannel.ReadAsync();
               var packet = serializer.ToObject(packetData);
               switch (packet.GetType().Name) {
                  case nameof(HavePacket):
                     await haveChannel.WriteAsync((HavePacket)packet);
                     break;
                  case nameof(NeedPacket):
                     await needChannel.WriteAsync((NeedPacket)packet);
                     break;
                  case nameof(GivePacket):
                     await giveChannel.WriteAsync((GivePacket)packet);
                     break;
                  case nameof(DonePacket):
                     await doneChannel.WriteAsync((DonePacket)packet);
                     break;
                  default:
                     throw new InvalidStateException();
               }
            }
         } catch (NotConnectedException) {
            await disconnectLatchChannel.SetIsClosedAsync(true);
         }
      }

      private async Task SynchronizationLoopTaskStart() {
         var isGreater = bluetoothAdapter.AdapterId.CompareTo(neighbor.AdapterId) > 0;
         var rateLimit = ChannelFactory.Timer(1000);
         try {
            while (true) {
               await rateLimit.ReadAsync();

               if (isGreater) {
                  await SynchronizeRemoteToLocalAsync();
                  await SynchronizeLocalToRemoteAsync();
               } else {
                  await SynchronizeLocalToRemoteAsync();
                  await SynchronizeRemoteToLocalAsync();
               }
            }
         } catch (NotConnectedException) {
            await disconnectLatchChannel.SetIsClosedAsync(true);
         }
      }

      private async Task SynchronizeRemoteToLocalAsync() {
         var have = await haveChannel.ReadAsync();
         
         if (!await IsRemoteObjectHeldLocally(have.MerkleRootHash)) {
            var nodesToImport = new List<Tuple<string, MerkleNode>>();

            var neededHashes = new LinkedList<string>();
            neededHashes.AddLast(have.MerkleRootHash);

            while (neededHashes.Count != 0) {
               foreach (var hash in neededHashes) {
                  Console.WriteLine("EMIT NEED " + hash);
                  var need = new NeedPacket { MerkleRootHash = hash };
                  await neighbor.SendAsync(serializer.ToByteArray(need));
               }

               foreach (var hash in Enumerable.Range(0, neededHashes.Count)) {
                  neededHashes.RemoveFirst();

                  var give = await giveChannel.ReadAsync();
                  nodesToImport.Add(Tuple.Create(give.NodeHash, give.Node));
                  Console.WriteLine("RECV GIVE " + give.NodeHash);

                  if (!await IsRemoteObjectHeldLocally(give.Node.LeftHash)) {
                     neededHashes.AddLast(give.Node.LeftHash);
                  }

                  if (!await IsRemoteObjectHeldLocally(give.Node.RightHash)) {
                     neededHashes.AddLast(give.Node.RightHash);
                  }
               }
            }

            Console.WriteLine("IMPORT");
            await remoteMerkleTree.ImportAsync(have.MerkleRootHash, nodesToImport);

            foreach (var tuple in nodesToImport) {
               var node = tuple.Item2;
               if (node.Descendents == 0) {
                  await localMerkleTree.InsertAsync(tuple.Item2);
               }
            }
         }

         await neighbor.SendAsync(serializer.ToByteArray(new DonePacket()));
      }

      private async Task SynchronizeLocalToRemoteAsync() {
         var localRootHash = await localMerkleTree.GetRootHashAsync() ?? CampfireNetHash.ZERO_HASH_BASE64;
         var havePacket = new HavePacket { MerkleRootHash = localRootHash };
         await neighbor.SendAsync(serializer.ToByteArray(havePacket));

         bool done = false;
         while (!done) {
            await new Select {
               Case(doneChannel, () => {
                  done = true;
               }),
               Case(needChannel, async need => {
                  Console.WriteLine("RECV NEED " + need.MerkleRootHash);

                  var node = await localMerkleTree.GetNodeAsync(need.MerkleRootHash);
                  var give = new GivePacket {
                     NodeHash = need.MerkleRootHash,
                     Node = node
                  };
                  await neighbor.SendAsync(serializer.ToByteArray(give));
                  Console.WriteLine("EMIT GIVE " + need.MerkleRootHash);
               })
            }.ConfigureAwait(false);
         }
      }

      private async Task<bool> IsRemoteObjectHeldLocally(string hash) {
         if (hash == CampfireNetHash.ZERO_HASH_BASE64) {
            return true;
         }
         return await remoteMerkleTree.GetNodeAsync(hash) != null;
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