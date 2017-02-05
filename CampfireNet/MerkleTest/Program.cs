using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Merkle;
using static CampfireNet.Utilities.ChannelsExtensions.ChannelsExtensions;

namespace MerkleTest {
   public static class Program {
      public static void Main(string[] args) {
         Run().Wait();
      }

      public static async Task Run() {
         var dataPath = new FileInfo(Assembly.GetEntryAssembly().Location).DirectoryName;

         IItemOperations<int> intOperations = new IntItemOperations();
         ICampfireNetObjectStore storeA = new FileSystemCampfireNetObjectStore(Path.Combine(dataPath, "a"));
         ICampfireNetObjectStore storeB = new InMemoryCampfireNetObjectStore(); // new FileSystemCampfireNetObjectStore(Path.Combine(dataPath, "b"));
         var a = new MerkleTree<int>("ns_a", intOperations, storeA);
         var b = new MerkleTree<int>("ns_b", intOperations, storeB);

         Go(async () => {
            while (true) {
               await a.InsertAsync((int)DateTime.Now.ToFileTimeUtc());
//               await Task.Delay(100);
            }
         }).Forget();

         while (true) {
            var upstreamRootHash = await a.GetRootHashAsync();
            if (upstreamRootHash == null) {
               continue;
            }

            var nodesToImport = new List<Tuple<string, MerkleNode>>();

            var s = new Stack<string>();
            s.Push(upstreamRootHash);
            while (s.Any()) {
               var hash = s.Pop();
               if (hash == CampfireNetHash.ZERO_HASH_BASE64) {
                  continue;
               }

               if (await b.GetNodeAsync(hash) != null) {
                  continue;
               }

               var node = await a.GetNodeAsync(hash);
               nodesToImport.Add(Tuple.Create(hash, node));

               s.Push(node.LeftHash);
               s.Push(node.RightHash);
            }

            await b.ImportAsync(upstreamRootHash, nodesToImport);
            Console.WriteLine("Imported " + nodesToImport.Count + " nodes!");
         }
      }

      public class IntItemOperations : IItemOperations<int> {
         public byte[] Serialize(int item) {
            return BitConverter.GetBytes(item);
         }
      }
   }
}
