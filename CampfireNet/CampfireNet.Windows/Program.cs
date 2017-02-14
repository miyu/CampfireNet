using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using CampfireNet.IO;
using CampfireNet.IO.Packets;
using CampfireNet.Simulator;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet.Windows {
	public static class Program {
		public static void Main() {
         using (var adapter = new WindowsBluetoothAdapter()) {
            var broadcastMessageSerializer = new BroadcastMessageSerializer();
            var objectStore = new InMemoryCampfireNetObjectStore();
//            var objectStore = new FileSystemCampfireNetObjectStore(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "demo_store"));
            var clientMerkleTreeFactory = new ClientMerkleTreeFactory(broadcastMessageSerializer, objectStore);
            var client = new CampfireNetClient(adapter, broadcastMessageSerializer, clientMerkleTreeFactory);
            client.RunAsync().Forget();

            client.BroadcastReceived += e => {
               var s = Encoding.UTF8.GetString(e.Message.Data, 0, e.Message.Data.Length);
               Console.WriteLine("RECV: " + s);
            };

            Console.WriteLine("My adapter id is: " + adapter.AdapterId + " AKA " + string.Join(" ", adapter.AdapterId.ToByteArray()));
            while (true) {
               var line = Console.ReadLine();
               client.BroadcastAsync(new BroadcastMessage {
                  Data = Encoding.UTF8.GetBytes(line)
               }).Forget();
            }

            new ManualResetEvent(false).WaitOne();
         }
      }
	}
}
