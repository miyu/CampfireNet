using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.IO;
using CampfireNet.IO.Transport;
using CampfireNet.Simulator;
using CampfireNet.Utilities;
using CampfireNet.Utilities.AsyncPrimatives;
using CampfireNet.Utilities.Channels;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace CampfireNet.Windows {
   public class WindowsBluetoothAdapter : IBluetoothAdapter, IDisposable {
      private static readonly Guid CAMPFIRE_NET_SERVICE_CLASS = Guid.Parse("fa87c0d0-afac-11de-8a39-0800200c9a66");

      private readonly BluetoothClient bluetoothClient = new BluetoothClient();
      private readonly ConcurrentDictionary<Guid, Neighbor> neighborsById = new ConcurrentDictionary<Guid, Neighbor>();
      private readonly BluetoothWin32Authentication bluetoothWin32Authentication;
      private readonly BluetoothListener listener;

      public WindowsBluetoothAdapter() {
         // set win32 bluetooth stack auth callback that to always confirms inbound conns
         bluetoothWin32Authentication = new BluetoothWin32Authentication(Handler);

         listener = new BluetoothListener(CAMPFIRE_NET_SERVICE_CLASS);
         listener.Authenticate = false;
         listener.Encrypt = false;

         new Thread(() => {
            listener.Start();

            while (true) {
               var client = listener.AcceptBluetoothClient();
               Console.WriteLine("Warning: Windows client doesn't support accepting!");
               Console.WriteLine($"Got {client.RemoteMachineName} {client.RemoteEndPoint}");
            }
         }).Start();
      }

      private void Handler(object sender, BluetoothWin32AuthenticationEventArgs e) {
         e.Confirm = true;
      }

      public BluetoothAddress LocalAddress => GetLocalAdapterAddress();
      public Guid AdapterId => BuildDeviceAddressGuid(LocalAddress);

      public async Task<IReadOnlyList<IBluetoothNeighbor>> DiscoverAsync() {
         var devices = await bluetoothClient.DiscoverDevicesInRangeAsync().ConfigureAwait(false);
         var results = new List<IBluetoothNeighbor>();
         foreach (var device in devices) {
            var neighborId = BuildDeviceAddressGuid(device.DeviceAddress);

            Neighbor neighbor;
            if (!neighborsById.TryGetValue(neighborId, out neighbor)) {
               neighbor = new Neighbor(device.DeviceAddress, neighborId, device.DeviceName);
               neighborsById[neighborId] = neighbor;
            }
            Console.WriteLine("Discovered " + (neighbor.Name ?? "[unknown]") + " " + neighbor.AdapterId + " " + neighbor.IsConnected);
            
            foreach (var x in device.InstalledServices) {
               Console.WriteLine(x);
            }
            results.Add(neighbor);
         }
         return results;
      }

      public void Dispose() {
         bluetoothClient.Dispose();
      }

      private static BluetoothAddress GetLocalAdapterAddress() {
         BluetoothRadio myRadio = BluetoothRadio.PrimaryRadio;
         if (myRadio == null) {
            throw new InvalidStateException("No bt hardware / unsupported stack?");
         }
         var localAddress = myRadio.LocalAddress;
         if (localAddress == null) {
            throw new InvalidStateException("BT adapter is off");
         }
         return localAddress;
      }

      private static Guid BuildDeviceAddressGuid(BluetoothAddress address) {
         return new Guid((int)address.Sap, (short)address.Nap, 0, 0, 0, 0, 0, 0, 0, 0, 0);
      }

      public class Neighbor : IBluetoothNeighbor {
         private readonly AsyncLock synchronization = new AsyncLock();
         private readonly BinaryLatchChannel disconnectedChannel = new BinaryLatchChannel(true);
         private readonly DisconnectableChannel<byte[], NotConnectedException> inboundChannel;
         private readonly BluetoothAddress address;
         private BluetoothClient bluetoothClient;

         public Neighbor(BluetoothAddress address, Guid adapterId, string name) {
            this.address = address;
            AdapterId = adapterId;
            Name = name;

            inboundChannel = new DisconnectableChannel<byte[], NotConnectedException>(disconnectedChannel, ChannelFactory.Nonblocking<byte[]>());
         }

         public Guid AdapterId { get; }
         public string Name { get; }
         public bool IsConnected => !disconnectedChannel.IsClosed;
         public ReadableChannel<byte[]> InboundChannel => inboundChannel;

         public async Task<bool> TryHandshakeAsync(double minTimeoutSeconds) {
            try {
               using (await synchronization.LockAsync().ConfigureAwait(false)) {
                  Console.WriteLine("Attempting to connect to ID " + AdapterId + " AKA " + string.Join(" ", AdapterId.ToByteArray()));

                  bluetoothClient = new BluetoothClient();
                  bluetoothClient.Authenticate = false;
                  bluetoothClient.Encrypt = false;

                  await bluetoothClient.ConnectAsync(address, CAMPFIRE_NET_SERVICE_CLASS).ConfigureAwait(false);
                  disconnectedChannel.SetIsClosed(false);

                  Console.WriteLine("Connected. Their Adapter ID is " + AdapterId + " AKA " + string.Join(" ", AdapterId.ToByteArray()));

                  ChannelsExtensions.Go(async () => {
                     Console.WriteLine("Entered BT Reader Task");
                     var networkStream = bluetoothClient.GetStream();
                     try {
                        while (!disconnectedChannel.IsClosed) {
                           Console.WriteLine("Reading BT Frame");
                           var dataLengthBuffer = await ReadBytesAsync(networkStream, 4).ConfigureAwait(false);
                           var dataLength = BitConverter.ToInt32(dataLengthBuffer, 0);
                           Console.WriteLine("Got BT Frame Length: " + dataLength);
                           var data = await ReadBytesAsync(networkStream, dataLength).ConfigureAwait(false);
                           await inboundChannel.WriteAsync(data).ConfigureAwait(false);
                        }
                     } catch (Exception e) {
                        Console.WriteLine(e);
                        Teardown();
                     }
                  }).Forget();
                  return true;
               }
            } catch (Exception e) {
               Console.WriteLine("Failed to connect to ID " + AdapterId + " AKA " + string.Join(" ", AdapterId.ToByteArray()));
               Console.WriteLine(e.GetType().FullName);
               return false;
            }
         }

         public async Task SendAsync(byte[] data) {
            using (await synchronization.LockAsync().ConfigureAwait(false)) {
               Console.WriteLine("Sending to ID " + AdapterId + " AKA " + string.Join(" ", AdapterId.ToByteArray()));

               try {
                  var stream = bluetoothClient.GetStream();
                  await stream.WriteAsync(BitConverter.GetBytes(data.Length), 0, 4).ConfigureAwait(false);
                  await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                  Console.WriteLine("Sent to ID " + AdapterId + " AKA " + string.Join(" ", AdapterId.ToByteArray()));
               } catch {
                  Console.WriteLine("Failed to send to ID " + AdapterId + " AKA " + string.Join(" ", AdapterId.ToByteArray()));
                  Teardown();
                  throw new NotConnectedException();
               }
            }
         }

         private void Teardown() {
            Console.WriteLine("Teardown connection to ID " + AdapterId + " AKA " + string.Join(" ", AdapterId.ToByteArray()));
            bluetoothClient?.Dispose();
            bluetoothClient = null;
            disconnectedChannel.SetIsClosed(true);
         }

         private async Task<byte[]> ReadBytesAsync(NetworkStream stream, int count) {
            var buffer = new byte[count];
            int index = 0;
            while (index < count) {
               var bytesRead = await stream.ReadAsync(buffer, index, count - index).ConfigureAwait(false);
               if (bytesRead <= 0) {
                  Console.WriteLine(nameof(WindowsBluetoothAdapter) + ": Bytes Read was " + bytesRead);
                  throw new NotConnectedException();
               }
               index += bytesRead;
            }
            return buffer;
         }

         public void Disconnect() => Teardown();
      }
   }
}