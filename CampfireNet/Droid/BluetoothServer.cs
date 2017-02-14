using System.Threading.Tasks;
using Android.Bluetooth;
using CampfireNet.Utilities;

namespace AndroidTest.Droid {
   public class BluetoothServer {
      private readonly BluetoothServerSocket listener;
      private readonly InboundBluetoothSocketTable inboundBluetoothSocketTable;
      private Task listenerTask;

      private BluetoothServer(BluetoothServerSocket listener, InboundBluetoothSocketTable inboundBluetoothSocketTable) {
         this.listener = listener;
         this.inboundBluetoothSocketTable = inboundBluetoothSocketTable;
      }

      public void Start() {
         listenerTask = ListenerTaskStart().Forgettable();
      }

      private async Task ListenerTaskStart() {
         while (true) {
            var socket = await listener.AcceptAsync().ConfigureAwait(false);
            await inboundBluetoothSocketTable.GiveAsync(socket).ConfigureAwait(false);
         }
      }

      public static BluetoothServer Create(BluetoothAdapter adapter, InboundBluetoothSocketTable inboundBluetoothSocketTable) {
         var serverSocket = adapter.ListenUsingInsecureRfcommWithServiceRecord(CampfireNetBluetoothConstants.NAME, CampfireNetBluetoothConstants.APP_UUID);
         return new BluetoothServer(serverSocket, inboundBluetoothSocketTable);
      }
   }
}