using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using CampfireNet.Identities;
using CampfireNet.IO;
using CampfireNet.IO.Packets;
using CampfireNet.Simulator;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet.Windows {
	public static class Program {
		public static void Main() {
         Console.WriteLine("Enter key to begin");
		   Console.ReadLine();
		   Console.Clear();

         using (var adapter = new WindowsBluetoothAdapter()) {
            var broadcastMessageSerializer = new BroadcastMessageSerializer();
            var objectStore = new InMemoryCampfireNetObjectStore();
            //            var objectStore = new FileSystemCampfireNetObjectStore(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "demo_store"));
            var identity = new Identity(new IdentityManager(), "Windows_Client");
            identity.GenerateRootChain();
            var clientMerkleTreeFactory = new ClientMerkleTreeFactory(broadcastMessageSerializer, objectStore);
            var client = new CampfireNetClient(identity, adapter, broadcastMessageSerializer, clientMerkleTreeFactory);
            client.RunAsync().Forget();

            client.BroadcastReceived += e => {
               var s = Encoding.UTF8.GetString(e.Message.DecryptedPayload, 0, e.Message.DecryptedPayload.Length);
               DebugConsole.WriteLine(new string(' ', Console.BufferWidth - 1), ConsoleColor.White, ConsoleColor.Red);
               DebugConsole.WriteLine(("RECV: " + s).PadRight(Console.BufferWidth - 1), ConsoleColor.White, ConsoleColor.Red);
               DebugConsole.WriteLine(new string(' ', Console.BufferWidth - 1), ConsoleColor.White, ConsoleColor.Red);
            };

            Console.WriteLine("My adapter id is: " + adapter.AdapterId + " AKA " + string.Join(" ", adapter.AdapterId.ToByteArray()));
            while (true) {
               var line = Console.ReadLine();
               client.BroadcastAsync(Encoding.UTF8.GetBytes(line)).Forget();
            }

            new ManualResetEvent(false).WaitOne();
         }
      }
	}
}
