using Android.Bluetooth;
using System;

namespace scratchpad_android
{
   public class Class1 {
      public static void X() {
         var bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
         if (bluetoothAdapter == null) {
            throw new Exception("No bluetooth found");
         }
         if (!bluetoothAdapter.IsEnabled) {
            bluetoothAdapter.Enable();
         }
         BluetoothDevice.disco
         bluetoothAdapter.StartDiscovery()
         bluetoothAdapter.StartDiscovery();
         bluetoothAdapter.NotifyAll();
      }
   }
}
