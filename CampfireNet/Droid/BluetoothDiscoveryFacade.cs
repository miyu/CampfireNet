using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using CampfireNet.Utilities;
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
         var filter5 = new IntentFilter(BluetoothDevice.ActionUuid);

         var resultBox = new AsyncBox<List<BluetoothDevice>>();
         var discoveryContext = new DiscoveryContext(applicationContext, bluetoothAdapter, resultBox);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter1);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter2);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter3);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter5);

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
         private readonly ConcurrentDictionary<string, BluetoothDevice> allDiscoveredDevicesByMac = new ConcurrentDictionary<string, BluetoothDevice>();
         private readonly ConcurrentSet<BluetoothDevice> pendingServiceDiscoveryDevices = new ConcurrentSet<BluetoothDevice>();
         private readonly ConcurrentDictionary<string, BluetoothDevice> serviceDiscoveredDevicesByMac = new ConcurrentDictionary<string, BluetoothDevice>();
         private readonly ConcurrentSet<BluetoothDevice> discoveredCampfireNetDevices = new ConcurrentSet<BluetoothDevice>();

         private readonly Context applicationContext;
         private readonly BluetoothAdapter adapter;
         private readonly AsyncBox<List<BluetoothDevice>> resultBox;

         public DiscoveryContext(Context applicationContext, BluetoothAdapter adapter, AsyncBox<List<BluetoothDevice>> resultBox) {
            this.applicationContext = applicationContext;
            this.adapter = adapter;
            this.resultBox = resultBox;

            Receiver = new LambdaBroadcastReceiver(OnReceive);
         }

         public BroadcastReceiver Receiver { get; }

         private void OnReceive(Context context, Intent intent) {
            try {
               lock (synchronization) {
                  Console.WriteLine($"GOT INTENT: " + intent.Action);

                  var device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);

                  switch (intent.Action) {
                     case BluetoothAdapter.ActionDiscoveryStarted:
                        Console.WriteLine($"Started Discovery");
                        break;
                     case BluetoothDevice.ActionFound:
                        Console.WriteLine($"Found: {device.Address} {device.Name ?? "[no name]"}");
                        allDiscoveredDevicesByMac.TryAdd(device.Address, device);
                        break;
                     case BluetoothAdapter.ActionDiscoveryFinished:
                        Console.WriteLine($"Finished Discovery, Performing Service Discovery for Filtering");
                        adapter.CancelDiscovery();
                        allDiscoveredDevicesByMac.ForEach(kvp => pendingServiceDiscoveryDevices.AddOrThrow(kvp.Value));
                        TriggerNextServiceDiscoveryOrCompletion();
                        break;
                     case BluetoothDevice.ActionUuid:
                        Console.WriteLine($"Got UUIDs of device {device.Address} {device.Name ?? "[no name]"}");
                        var uuidObjects = intent.GetParcelableArrayExtra(BluetoothDevice.ExtraUuid);
                        if (uuidObjects != null) {
                           var uuids = uuidObjects.Cast<ParcelUuid>().ToArray();
                           uuids.ForEach(Console.WriteLine);
                           // Equality isn't implemented by uuid, so compare tostrings...
                           if (uuids.Any(uuid => uuid.ToString().Equals(CampfireNetBluetoothConstants.APP_UUID.ToString()))) {
                              Console.WriteLine($"Found CampfireNet device {device.Address} {device.Name ?? "[no name]"}");
                              discoveredCampfireNetDevices.TryAdd(device);
                           }
                        }
                        if (!allDiscoveredDevicesByMac.ContainsKey(device.Address)) {
                           Console.WriteLine("Unrequested UUID, so tossing");
                           return;
                        }
                        if (serviceDiscoveredDevicesByMac.TryAdd(device.Address, device)) {
                           TriggerNextServiceDiscoveryOrCompletion();
                        }
                        break;
                     default:
                        throw new NotImplementedException($"Unhandled intent action: {intent.Action}");
                  }
               }
            } catch (Exception e) {
               Console.WriteLine("FATAL error in discovery " + e);
            }
         }

         private void TriggerNextServiceDiscoveryOrCompletion() {
            while (pendingServiceDiscoveryDevices.Any()) {
               var device = pendingServiceDiscoveryDevices.First();
               pendingServiceDiscoveryDevices.RemoveOrThrow(device);

               Console.WriteLine($"Fetching UUIDs of device {device.Address} {device.Name ?? "[no name]"}");
               var result = device.FetchUuidsWithSdp();
               Console.WriteLine("Fetch returned " + result);
               return;
            }

            Console.WriteLine("Writing discovery result!");
            resultBox.SetResult(discoveredCampfireNetDevices.ToList());
         }
      }
   }
}