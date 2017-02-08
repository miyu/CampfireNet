//#define CN_DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CampfireNet.IO;
using CampfireNet.IO.Packets;
using CampfireNet.IO.Transport;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Channels;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet {
   public class NeighborConnectionContext {
      private readonly WirePacketSerializer serializer = new WirePacketSerializer();

      private readonly BinaryLatchChannel disconnectLatchChannel = new BinaryLatchChannel();
      private readonly DisconnectableChannel<HavePacket, NotConnectedException> haveChannel;
      private readonly DisconnectableChannel<NeedPacket, NotConnectedException> needChannel;
      private readonly DisconnectableChannel<GivePacket, NotConnectedException> giveChannel;
      private readonly DisconnectableChannel<DonePacket, NotConnectedException> doneChannel;

      private readonly IBluetoothAdapter bluetoothAdapter;
      private readonly IBluetoothNeighbor neighbor;

      private readonly BroadcastMessageSerializer broadcastMessageSerializer;

      private readonly MerkleTree<BroadcastMessage> localMerkleTree;
      private readonly MerkleTree<BroadcastMessage> remoteMerkleTree;

      public NeighborConnectionContext(
         IBluetoothAdapter bluetoothAdapter,
         IBluetoothNeighbor neighbor,
         BroadcastMessageSerializer broadcastMessageSerializer,
         MerkleTree<BroadcastMessage> localMerkleTree,
         MerkleTree<BroadcastMessage> remoteMerkleTree
      ) {
         this.haveChannel = new DisconnectableChannel<HavePacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<HavePacket>());
         this.needChannel = new DisconnectableChannel<NeedPacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<NeedPacket>());
         this.giveChannel = new DisconnectableChannel<GivePacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<GivePacket>());
         this.doneChannel = new DisconnectableChannel<DonePacket, NotConnectedException>(disconnectLatchChannel, ChannelFactory.Nonblocking<DonePacket>());

         this.bluetoothAdapter = bluetoothAdapter;
         this.neighbor = neighbor;
         this.broadcastMessageSerializer = broadcastMessageSerializer;
         this.localMerkleTree = localMerkleTree;
         this.remoteMerkleTree = remoteMerkleTree;
      }

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
               var packetData = await inboundChannel.ReadAsync();
               var packet = serializer.ToObject(packetData);
               switch (packet.GetType().Name) {
                  case nameof(HavePacket):
                     DebugPrint("Got HAVE {0}", ((HavePacket)packet).MerkleRootHash);
                     await haveChannel.WriteAsync((HavePacket)packet);
                     break;
                  case nameof(NeedPacket):
                     DebugPrint("Got NEED {0}", ((NeedPacket)packet).MerkleRootHash);
                     await needChannel.WriteAsync((NeedPacket)packet);
                     break;
                  case nameof(GivePacket):
                     DebugPrint("Got GIVE {0}", ((GivePacket)packet).NodeHash);
                     await giveChannel.WriteAsync((GivePacket)packet);
                     break;
                  case nameof(DonePacket):
                     DebugPrint("Got DONE");
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
         } finally {
            DebugPrint("Router loop exiting");
         }
      }

      private async Task SynchronizationLoopTaskStart() {
         var isGreater = bluetoothAdapter.AdapterId.CompareTo(neighbor.AdapterId) > 0;
         var rateLimit = ChannelFactory.Timer(5000, 3000);
         try {
            while (true) {
               if (isGreater) {
                  await SynchronizeRemoteToLocalAsync();
                  await SynchronizeLocalToRemoteAsync();
               } else {
                  await SynchronizeLocalToRemoteAsync();
                  await SynchronizeRemoteToLocalAsync();
               }
               await rateLimit.ReadAsync();
            }
         } catch (NotConnectedException) {
            disconnectLatchChannel.SetIsClosed(true);
         } finally {
            DebugPrint("Sync loop exiting");
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
#if CN_DEBUG
         lock (g_printLock) {
            var colors = new ConsoleColor[] { ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.White };
            var color = colors[Math.Abs(bluetoothAdapter.AdapterId.GetHashCode()) % colors.Length];
            var cc = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(bluetoothAdapter.AdapterId.ToString("n") + " => " + neighbor.AdapterId.ToString("n") + " " + s, args);
            Console.ForegroundColor = cc;
         }
#endif
      }

      private async Task SynchronizeRemoteToLocalAsync() {
         DebugPrint("Enter Remote to Local");
         var have = await haveChannel.ReadAsync();
         DebugPrint("Have is {0}", have.MerkleRootHash);
         var isRemoteRootSyncedLocally = await IsRemoteObjectHeldLocally(have.MerkleRootHash);
         DebugPrint("IRRSL {0}", isRemoteRootSyncedLocally);

         if (!isRemoteRootSyncedLocally) {
            var nodesToImport = new List<Tuple<string, MerkleNode>>();
            var neededHashes = new LinkedList<string>();
            neededHashes.AddLast(have.MerkleRootHash);

            while (neededHashes.Count != 0) {
               var hashesReadLocally = new HashSet<string>();
               foreach (var hash in neededHashes) {
                  var localNode = await localMerkleTree.GetNodeAsync(hash);
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
                  var isDataNode = node.TypeTag == MerkleNodeTypeTag.Data;
                  BroadcastMessage message = isDataNode ? broadcastMessageSerializer.Deserialize(node.Contents) : null;

                  var insertionResult = await localMerkleTree.TryInsertAsync(tuple.Item2);
                  if (insertionResult.Item1 && isDataNode) {
                     BroadcastReceived?.Invoke(new BroadcastReceivedEventArgs(neighbor, message));
                  }
               }
            }
         }

         DebugPrint("SEND DONE");
         await neighbor.SendAsync(serializer.ToByteArray(new DonePacket()));
      }

      private async Task SynchronizeLocalToRemoteAsync() {
         DebugPrint("Enter Local to Remote");

         var localRootHash = await localMerkleTree.GetRootHashAsync() ?? CampfireNetHash.ZERO_HASH_BASE64;
         var havePacket = new HavePacket { MerkleRootHash = localRootHash };
         DebugPrint("SEND HAVE {0}", havePacket.MerkleRootHash);
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
                  DebugPrint("SEND GIVE");
                  neighbor.SendAsync(serializer.ToByteArray(give)).Forget();
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