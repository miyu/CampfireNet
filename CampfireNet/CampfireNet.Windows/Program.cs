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
            var clientMerkleTreeFactory = new ClientMerkleTreeFactory(broadcastMessageSerializer, objectStore);
            var client = new CampfireNetClient(adapter, broadcastMessageSerializer, clientMerkleTreeFactory);
            client.RunAsync().Forget();
            new ManualResetEvent(false).WaitOne();
         }
      }
	}
}
