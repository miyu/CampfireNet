using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using CampfireNet.Identities;
using CampfireNet.IO;
using CampfireNet.IO.Packets;
using CampfireNet.Security;
using CampfireNet.Simulator;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Merkle;


namespace CampfireNet.Windows {
   public static class Program {
      public static void Main() {
         //         // Generate root pk
         //         var rsa = new RSACryptoServiceProvider(CryptoUtil.ASYM_KEY_SIZE_BITS);
         //		   var bytes = __HackPrivateKeyUtilities.SerializePrivateKey(rsa);
         //         Console.WriteLine($"new byte[] {{ {string.Join(", ", bytes)} }}");

         HackGlobals.DisableChainOfTrustCheck = true;

         var rootRsa = __HackPrivateKeyUtilities.DeserializePrivateKey(__HackPrivateKeyUtilities.__HACK_ROOT_PRIVATE_KEY);
         var rootIdentity = new Identity(rootRsa, new IdentityManager(), "hack_root");
         rootIdentity.GenerateRootChain();

//         Console.WriteLine("Enter key to begin");
         Console.ReadLine();
         Console.Clear();

         using (var adapter = new WindowsBluetoothAdapter()) {
            var broadcastMessageSerializer = new BroadcastMessageSerializer();
            var objectStore = new InMemoryCampfireNetObjectStore();
            //            var objectStore = new FileSystemCampfireNetObjectStore(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "demo_store"));

            var identity = new Identity(new IdentityManager(), "Windows_Client");
            identity.AddTrustChain(rootIdentity.GenerateNewChain(identity.PublicIdentity, Permission.None, Permission.None, identity.Name));
            Console.WriteLine($"I am {string.Join(" > ", identity.TrustChain.Select(n => n.ThisId.ToHexString()))}");

            var clientMerkleTreeFactory = new ClientMerkleTreeFactory(broadcastMessageSerializer, objectStore);
            var client = new CampfireNetClient(identity, adapter, broadcastMessageSerializer, clientMerkleTreeFactory);
            client.RunAsync().Forget();

            client.MessageReceived += e => {
               Console.WriteLine($"{e.Message.SourceId.ToString().Substring(0, 8)} => {e.Message.DestinationId.ToString().Substring(0, 8)}");
               var payload = e.Message.DecryptedPayload;

               Console.WriteLine(payload.ToHexDump());
               Console.WriteLine();

//               var s = Encoding.UTF8.GetString(e.Message.DecryptedPayload, 0, e.Message.DecryptedPayload.Length);
//               DebugConsole.WriteLine(new string(' ', Console.BufferWidth - 1), ConsoleColor.White, ConsoleColor.Red);
//               DebugConsole.WriteLine(("RECV: " + s).PadRight(Console.BufferWidth - 1), ConsoleColor.White, ConsoleColor.Red);
//               DebugConsole.WriteLine(new string(' ', Console.BufferWidth - 1), ConsoleColor.White, ConsoleColor.Red);
            };

            Console.WriteLine("My adapter id is: " + adapter.AdapterId + " AKA " + string.Join(" ", adapter.AdapterId.ToByteArray()));
//            while (true) {
//               var line = Console.ReadLine();
//               client.BroadcastAsync(Encoding.UTF8.GetBytes(line)).Forget();
//            }

            new ManualResetEvent(false).WaitOne();
         }
      }

      public static IEnumerable<Chunk<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize) {
         var chunkCounter = 0;
         var current = new List<T>();
         foreach (var x in source) {
            current.Add(x);
            if (current.Count == chunkSize) {
               yield return new Chunk<T>(chunkCounter, current);
               chunkCounter++;
               current = new List<T>();
            }
         }
         if (current.Count != 0) {
            yield return new Chunk<T>(chunkCounter, current);
         }
      }

      public static string ToHexDump(this byte[] a) {
         return string.Join(
            Environment.NewLine,
            a.Chunk(16)
             .Select(chunk => {
                var offset = chunk.Index * 16;
                var hex = string.Join(" ", chunk.Items.Chunk(4).Select(quad => string.Join("", quad.Items.Map(b => b.ToString("X2"))))).PadRight(4 * 8 + 7);
                var ascii = string.Join("", chunk.Items.Map(b => b < 32 || b >= 126 ? '.' : (char)b));
                return $"{offset:X8}  {hex}  {ascii}";
             }));
      }
   }


   public class Chunk<T> {
      internal Chunk(int index, IReadOnlyList<T> items) {
         Index = index;
         Items = items;
      }

      public int Index { get; }
      public IReadOnlyList<T> Items { get; }
   }
}
