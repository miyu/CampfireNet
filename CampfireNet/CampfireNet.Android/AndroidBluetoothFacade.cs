using System;
using Android.App;
using Android.Bluetooth;
using Android.Content;

namespace AndroidTest.Droid {
   public class AndroidBluetoothFacade {
      private readonly BluetoothAdapter bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
      private readonly Context applicationContext;

      public AndroidBluetoothFacade(Context applicationContext) {
         this.applicationContext = applicationContext;
      }

      public void EnableBluetoothFromActivity(Activity currentActivity) {
         const int REQUEST_ENABLE_BT = 1;

         if (!bluetoothAdapter.IsEnabled) {
            Console.WriteLine("Enabling bluetooth");
            Intent enableBtIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
            currentActivity.StartActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
         }
      }
   }
}