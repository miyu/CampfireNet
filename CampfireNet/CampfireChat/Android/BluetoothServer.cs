using System;
using System.Threading.Tasks;
using Android.Bluetooth;
using CampfireNet.Utilities;

namespace AndroidTest.Droid {
   public class BluetoothServer {
      private readonly BluetoothAdapter adapter;
      private readonly InboundBluetoothSocketTable inboundBluetoothSocketTable;
      private Task listenerTask;

      private BluetoothServer(BluetoothAdapter adapter, InboundBluetoothSocketTable inboundBluetoothSocketTable) {
         this.adapter = adapter;
         this.inboundBluetoothSocketTable = inboundBluetoothSocketTable;
      }

      public void Start() {
         listenerTask = ListenerTaskStart().Forgettable();
      }

      private async Task ListenerTaskStart() {
         while (true) {
            try {
               using (var listener = adapter.ListenUsingInsecureRfcommWithServiceRecord(CampfireNetBluetoothConstants.NAME, CampfireNetBluetoothConstants.APP_UUID)) {
                  while (true) {
                     var socket = await listener.AcceptAsync().ConfigureAwait(false);
                     await inboundBluetoothSocketTable.GiveAsync(socket).ConfigureAwait(false);
                  }
               }
            } catch (Exception e) {
               Console.WriteLine("Listener caught: " + e);
               await Task.Delay(TimeSpan.FromSeconds(5));
               while (!adapter.IsEnabled) {
                  await Task.Delay(TimeSpan.FromSeconds(5));
               }
            }
         }
      }

      public static BluetoothServer Create(BluetoothAdapter adapter, InboundBluetoothSocketTable inboundBluetoothSocketTable) {
         return new BluetoothServer(adapter, inboundBluetoothSocketTable);
      }
   }
}