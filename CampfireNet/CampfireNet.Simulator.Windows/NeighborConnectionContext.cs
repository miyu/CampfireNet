using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CampfireNet.Utilities;
using CampfireNet.Utilities.ChannelsExtensions;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet.Simulator {
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
            disconnectLatchChannel.SetIsClosed(true);
            try {
               await neighbor.SendAsync(new byte[0]);
               throw new InvalidStateException();
            } catch (NotConnectedException) { }
         }
      }

      private async Task SynchronizationLoopTaskStart() {
         var isGreater = bluetoothAdapter.AdapterId.CompareTo(neighbor.AdapterId) > 0;
//         var rateLimit = ChannelFactory.Timer(1000);
         try {
            while (true) {
//               await rateLimit.ReadAsync();
//               await neighbor.SendAsync(serializer.ToByteArray(new DonePacket()));
               if (isGreater) {
                  await SynchronizeRemoteToLocalAsync();
                  await SynchronizeLocalToRemoteAsync();
               } else {
                  await SynchronizeLocalToRemoteAsync();
                  await SynchronizeRemoteToLocalAsync();
               }
            }
         } catch (NotConnectedException) {
            disconnectLatchChannel.SetIsClosed(true);
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
//                  Console.WriteLine("EMIT NEED " + hash);
                  var need = new NeedPacket { MerkleRootHash = hash };
                  await neighbor.SendAsync(serializer.ToByteArray(need));
               }

               foreach (var hash in Enumerable.Range(0, neededHashes.Count)) {
                  neededHashes.RemoveFirst();

                  var give = await giveChannel.ReadAsync();
                  nodesToImport.Add(Tuple.Create(give.NodeHash, give.Node));
//                  Console.WriteLine("RECV GIVE " + give.NodeHash);

                  if (!await IsRemoteObjectHeldLocally(give.Node.LeftHash)) {
                     neededHashes.AddLast(give.Node.LeftHash);
                  }

                  if (!await IsRemoteObjectHeldLocally(give.Node.RightHash)) {
                     neededHashes.AddLast(give.Node.RightHash);
                  }
               }
            }

//            Console.WriteLine("IMPORT");
            await remoteMerkleTree.ImportAsync(have.MerkleRootHash, nodesToImport);

            foreach (var tuple in nodesToImport) {
               var node = tuple.Item2;
               if (node.Descendents == 0 && await localMerkleTree.GetNodeAsync(tuple.Item1) == null) {
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
               ChannelsExtensions.Case(doneChannel, () => {
                  done = true;
               }),
               ChannelsExtensions.Case(needChannel, async need => {
//                  Console.WriteLine("RECV NEED " + need.MerkleRootHash);

                  var node = await localMerkleTree.GetNodeAsync(need.MerkleRootHash);
                  var give = new GivePacket {
                     NodeHash = need.MerkleRootHash,
                     Node = node
                  };
                  await neighbor.SendAsync(serializer.ToByteArray(give));
//                  Console.WriteLine("EMIT GIVE " + need.MerkleRootHash);
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
}