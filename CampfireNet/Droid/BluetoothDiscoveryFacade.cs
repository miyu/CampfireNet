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
using CampfireNet.Utilities.Channels;
using CampfireNet.Utilities.Collections;
using static CampfireNet.Utilities.Channels.ChannelsExtensions;

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

         var discoveryContext = new DiscoveryContext(applicationContext, bluetoothAdapter);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter1);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter2);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter3);
         applicationContext.RegisterReceiver(discoveryContext.Receiver, filter5);

         if (bluetoothAdapter.ScanMode != ScanMode.ConnectableDiscoverable) {
            EnableDiscovery();
         }

         bluetoothAdapter.StartDiscovery();

         var peers = await discoveryContext.FetchResultsAsync().ConfigureAwait(false);
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

         private readonly Channel<Intent> intentChannel = ChannelFactory.Nonblocking<Intent>();

         private readonly ConcurrentDictionary<string, BluetoothDevice> allDiscoveredDevicesByMac = new ConcurrentDictionary<string, BluetoothDevice>();
         private readonly ConcurrentSet<BluetoothDevice> pendingServiceDiscoveryDevices = new ConcurrentSet<BluetoothDevice>();
         private readonly ConcurrentDictionary<string, BluetoothDevice> serviceDiscoveredDevicesByMac = new ConcurrentDictionary<string, BluetoothDevice>();
         private readonly ConcurrentDictionary<string, BluetoothDevice> discoveredCampfireNetDevices = new ConcurrentDictionary<string, BluetoothDevice>();

         private readonly Context applicationContext;
         private readonly BluetoothAdapter adapter;

         private int state = 0;

         public DiscoveryContext(Context applicationContext, BluetoothAdapter adapter) {
            this.applicationContext = applicationContext;
            this.adapter = adapter;

            Receiver = new LambdaBroadcastReceiver(OnReceive);
         }

         public BroadcastReceiver Receiver { get; }

         public async Task<List<BluetoothDevice>> FetchResultsAsync() {
            var running = true;
            while (running) {
               await new Select {
                  Case(ChannelFactory.Timeout(TimeSpan.FromSeconds(20)), async () => {
                     Console.WriteLine("Watchdog timeout at discovery!");
                     adapter.CancelDiscovery();
                     Console.WriteLine("Adapter enabled: " + adapter.IsEnabled);
                     Console.WriteLine("Disabling adapter...");
                     adapter.Disable();
                     await Task.Delay(5000);
                     Console.WriteLine("Enabling adapter...");
                     adapter.Enable();
                     await Task.Delay(10000);
                     running = false;
                  }),
                  Case(intentChannel, intent => {
                     Console.WriteLine($"GOT INTENT: " + intent.Action);

                     var device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);

                     switch (intent.Action) {
                        case BluetoothAdapter.ActionDiscoveryStarted:
                           if(state != 0) {
                              Console.WriteLine("WARN: STATE IS " + state + " NOT 0");
                           }

                           state = 1;
                           Console.WriteLine($"Started Discovery");
                           break;
                        case BluetoothDevice.ActionFound:
                           if (state != 1 && state != 2) {
                              Console.WriteLine("WARN: STATE IS " + state + " NOT 1 or 2");
                           }

                           state = 2;
                           Console.WriteLine($"Found: {device.Address} {device.Name ?? "[no name]"}");

                           if (device.Name == null) {
                              Console.WriteLine("Skip as no name!");
                              return;
                           }

                           allDiscoveredDevicesByMac.TryAdd(device.Address, device);
                           break;
                        case BluetoothAdapter.ActionDiscoveryFinished:
                           if (state != 2) {
                              Console.WriteLine("WARN: STATE IS " + state + " NOT 2");
                              return;
                           }

                           state = 3;
                           Console.WriteLine($"Finished Discovery, Performing Service Discovery for Filtering");
                           adapter.CancelDiscovery();
                           allDiscoveredDevicesByMac.ForEach(kvp => pendingServiceDiscoveryDevices.AddOrThrow(kvp.Value));
                           running = TriggerNextServiceDiscoveryElseCompletion();
                           break;
                        case BluetoothDevice.ActionUuid:
                           if (state != 3 && state != 4) {
                              Console.WriteLine("WARN: STATE IS " + state + " NOT 3 or 4");
                           }

                           state = 4;
                           Console.WriteLine($"Got UUIDs of device {device.Address} {device.Name ?? "[no name]"}");
                           var uuidObjects = intent.GetParcelableArrayExtra(BluetoothDevice.ExtraUuid);
                           if (uuidObjects != null) {
                              var uuids = uuidObjects.Cast<ParcelUuid>().ToArray();
                              uuids.ForEach(Console.WriteLine);
                              // Equality isn't implemented by uuid, so compare tostrings...
                              if (uuids.Any(uuid => uuid.ToString().Equals(CampfireNetBluetoothConstants.APP_UUID.ToString())) ||
                                    uuids.Any(uuid => uuid.ToString().Equals(CampfireNetBluetoothConstants.FIRMWARE_BUG_REVERSE_APP_UUID.ToString()))) {
                                 Console.WriteLine($"Found CampfireNet device {device.Address} {device.Name ?? "[no name]"}");
                                 BluetoothDevice existing;
                                 if (discoveredCampfireNetDevices.TryGetValue(device.Address, out existing)) {
                                    Console.WriteLine("Device already discovered!");
                                 } else {
                                    discoveredCampfireNetDevices.TryAdd(device.Address, device);
                                 }
                              }
                           }
                           if (!allDiscoveredDevicesByMac.ContainsKey(device.Address)) {
                              Console.WriteLine("Unrequested UUID, so tossing");
                              return;
                           }
                           if (serviceDiscoveredDevicesByMac.TryAdd(device.Address, device)) {
                              running = TriggerNextServiceDiscoveryElseCompletion();
                           }
                           break;
                        default:
                           throw new NotImplementedException($"Unhandled intent action: {intent.Action}");
                     }
                  })
               }.ConfigureAwait(false);
            }
            adapter.CancelDiscovery();
            return discoveredCampfireNetDevices.Values.ToList();
         }

         private bool TriggerNextServiceDiscoveryElseCompletion() {
            while (pendingServiceDiscoveryDevices.Any()) {
               var device = pendingServiceDiscoveryDevices.First();
               pendingServiceDiscoveryDevices.RemoveOrThrow(device);

               Console.WriteLine($"Fetching UUIDs of device {device.Address} {device.Name ?? "[no name]"}");
               var result = device.FetchUuidsWithSdp();
               Console.WriteLine("Fetch returned " + result);
               return true;
            }
            return false;
         }

         private void OnReceive(Context context, Intent intent) {
            intentChannel.Write(intent);
         }
      }
   }
}