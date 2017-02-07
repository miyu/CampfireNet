using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
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

      public WindowsBluetoothAdapter() {
         // set win32 bluetooth stack auth callback that to always confirms inbound conns
         bluetoothWin32Authentication = new BluetoothWin32Authentication(Handler);
      }

      private void Handler(object sender, BluetoothWin32AuthenticationEventArgs e) {
         e.Confirm = true;
      }

      public BluetoothAddress LocalAddress => GetLocalAdapterAddress();
      public Guid AdapterId => BuildDeviceAddressGuid(LocalAddress);

      public async Task<IReadOnlyList<IBluetoothNeighbor>> DiscoverAsync() {
         var devices = await bluetoothClient.DiscoverDevicesInRangeAsync();
         var results = new List<IBluetoothNeighbor>();
         foreach (var device in devices) {
            var neighborId = BuildDeviceAddressGuid(device.DeviceAddress);
            Neighbor neighbor;
            if (!neighborsById.TryGetValue(neighborId, out neighbor)) {
               neighbor = new Neighbor(device.DeviceAddress, neighborId, device.DeviceName);
               neighborsById[neighborId] = neighbor;
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

         public async Task<bool> TryHandshakeAsync() {
            using (await synchronization.LockAsync()) {
               bluetoothClient = new BluetoothClient();
               bluetoothClient.Authenticate = false;
               bluetoothClient.Encrypt = false;

               await bluetoothClient.ConnectAsync(address, CAMPFIRE_NET_SERVICE_CLASS);
               disconnectedChannel.SetIsClosed(false);

               ChannelsExtensions.Go(async () => {
                  Console.WriteLine("Entered BT Reader Task");
                  var networkStream = bluetoothClient.GetStream();
                  try {
                     while (!disconnectedChannel.IsClosed) {
                        Console.WriteLine("Reading BT Frame");
                        var dataLengthBuffer = await ReadBytesAsync(networkStream, 4);
                        var dataLength = BitConverter.ToInt32(dataLengthBuffer, 0);
                        var data = await ReadBytesAsync(networkStream, dataLength);
                        await inboundChannel.WriteAsync(data);
                     }
                  } catch (Exception e) {
                     Console.WriteLine(e);
                     Teardown();
                  }
               }).Forget();
               return true;
            }
         }

         public async Task SendAsync(byte[] data) {
            using (await synchronization.LockAsync()) {
               try {
                  var stream = bluetoothClient.GetStream();
                  await stream.WriteAsync(BitConverter.GetBytes(data.Length), 0, 4);
                  await stream.WriteAsync(data, 0, data.Length);
               } catch {
                  Teardown();
                  throw new NotConnectedException();
               }
            }
         }

         private void Teardown() {
            bluetoothClient?.Dispose();
            bluetoothClient = null;
            disconnectedChannel.SetIsClosed(true);
         }

         private async Task<byte[]> ReadBytesAsync(NetworkStream stream, int count) {
            var buffer = new byte[count];
            int index = 0;
            while (index < count) {
               index += await stream.ReadAsync(buffer, index, count - index);
            }
            return buffer;
         }
      }
   }
}