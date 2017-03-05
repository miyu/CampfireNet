using System;
using System.Text;
using Android.Bluetooth;
using Android.Content;
using CampfireNet.Utilities;

namespace AndroidTest.Droid {
   public class AutomaticPairingService : IDisposable {
      private readonly Context applicationContext;
      private readonly LambdaBroadcastReceiver receiver;

      public AutomaticPairingService(Context applicationContext) {
         this.applicationContext = applicationContext;
         var filter = new IntentFilter();
         filter.AddAction(BluetoothDevice.ActionPairingRequest);

         receiver = new LambdaBroadcastReceiver((context, intent) => {
            if (intent.Action != BluetoothDevice.ActionPairingRequest)
               throw new InvalidStateException();

            int pin = intent.GetIntExtra(BluetoothDevice.ExtraPairingKey, 0);
            var pinBytes = Encoding.UTF8.GetBytes("" + pin);

            var device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
            device.SetPin(pinBytes);
            device.SetPairingConfirmation(true);
         });

         applicationContext.RegisterReceiver(receiver, filter);
      }

      public void Dispose() {
         applicationContext.UnregisterReceiver(receiver);
      }
   }
}