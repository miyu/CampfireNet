using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using CampfireNet.Utilities.AsyncPrimatives;
using CampfireNet.Utilities.Collections;

namespace AndroidTest.Droid {
   public class BluetoothDiscoveryFacade {
      private readonly BluetoothAdapter bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
      private readonly Context applicationContext;

      public BluetoothDiscoveryFacade(Context applicationContext) {
         this.applicationContext = applicationContext;
      }

      public async Task<List<BluetoothDevice>> DiscoverPeersAsync() {
         if (bluetoothAdapter.IsDiscovering) {
            Console.WriteLine("Canceled existing discovery");
            bluetoothAdapter.CancelDiscovery();
         }

         var filter1 = new IntentFilter(BluetoothAdapter.ActionDiscoveryStarted);
         var filter2 = new IntentFilter(BluetoothDevice.ActionFound);
         var filter3 = new IntentFilter(BluetoothAdapter.ActionDiscoveryFinished);
         //         filter.AddAction(BluetoothAdapter.ActionDiscoveryStarted);
         //         filter.AddAction(BluetoothDevice.ActionFound);
         //         filter.AddAction(BluetoothAdapter.ActionDiscoveryFinished);

         var resultBox = new AsyncBox<List<BluetoothDevice>>();
         var discoveryContext = new DiscoveryContext(applicationContext, resultBox);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter1);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter2);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter3);

         if (bluetoothAdapter.ScanMode != ScanMode.ConnectableDiscoverable) {
            EnableDiscovery();
         }

         bluetoothAdapter.StartDiscovery();

         var peers = await resultBox.GetResultAsync().ConfigureAwait(false);
         applicationContext.UnregisterReceiver(discoveryContext.Receiver);

         if (bluetoothAdapter.IsDiscovering) {
            Console.WriteLine("Warning: Still IsDiscovering");
         }
         return peers;
      } 

      private void EnableDiscovery() {
         Intent discoverableIntent = new Intent(BluetoothAdapter.ActionRequestDiscoverable);
         discoverableIntent.PutExtra(BluetoothAdapter.ExtraDiscoverableDuration, 300);
         discoverableIntent.SetFlags(ActivityFlags.NewTask);
         applicationContext.StartActivity(discoverableIntent, new Bundle());
      }

      private class DiscoveryContext {
         private readonly object synchronization = new object();
         private readonly ConcurrentSet<BluetoothDevice> discoveredDevices = new ConcurrentSet<BluetoothDevice>();

         private readonly Context applicationContext;
         private readonly AsyncBox<List<BluetoothDevice>> resultBox;

         public DiscoveryContext(Context applicationContext, AsyncBox<List<BluetoothDevice>> resultBox) {
            this.applicationContext = applicationContext;
            this.resultBox = resultBox;

            Receiver = new LambdaBroadcastReceiver(OnReceive);
         }

         public BroadcastReceiver Receiver { get; }

         private void OnReceive(Context context, Intent intent) {
            lock (synchronization) {
               Console.WriteLine($"GOT INTENT: " + intent.Action);

               var device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);

               switch (intent.Action) {
                  case BluetoothAdapter.ActionDiscoveryStarted:
                     Console.WriteLine($"Started Discovery");
                     break;
                  case BluetoothDevice.ActionFound:
                     Console.WriteLine($"Found: {device.Address} {device.Name ?? "[no name]"}");
                     discoveredDevices.TryAdd(device);
                     break;
                  case BluetoothAdapter.ActionDiscoveryFinished:
                     Console.WriteLine($"Finished Discovery");
                     resultBox.SetResult(discoveredDevices.ToList());
                     break;
                  default:
                     throw new NotImplementedException($"Unhandled intent action: {intent.Action}");
               }
            }
         }
      }
   }
}