using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Content;
using CampfireNet.IO;
using CampfireNet.IO.Transport;
using CampfireNet.Utilities;
using CampfireNet.Utilities.AsyncPrimatives;
using CampfireNet.Utilities.Channels;
using static CampfireNet.Utilities.Channels.ChannelsExtensions;

namespace AndroidTest.Droid {
   public class AndroidBluetoothAdapter : IBluetoothAdapter {
      private readonly ConcurrentDictionary<Guid, AndroidBluetoothAdapter.Neighbor> neighborsById = new ConcurrentDictionary<Guid, AndroidBluetoothAdapter.Neighbor>();

      private readonly BluetoothAdapter bluetoothAdapter;
      private readonly BluetoothDiscoveryFacade bluetoothDiscoveryFacade;
      private readonly InboundBluetoothSocketTable inboundBluetoothSocketTable;

      public AndroidBluetoothAdapter(
         Context applicationContext,
         BluetoothAdapter bluetoothAdapter, 
         BluetoothDiscoveryFacade bluetoothDiscoveryFacade,
         InboundBluetoothSocketTable inboundBluetoothSocketTable
      ) {
         this.bluetoothAdapter = bluetoothAdapter;
         this.bluetoothDiscoveryFacade = bluetoothDiscoveryFacade;
         this.inboundBluetoothSocketTable = inboundBluetoothSocketTable;

         // bluetoothAdapter.Address lies. See 
         // http://stackoverflow.com/questions/33377982/get-bluetooth-local-mac-address-in-marshmallow
         var bluetoothAddress = Android.Provider.Settings.Secure.GetString(applicationContext.ContentResolver, "bluetooth_address");
         AdapterId = MacUtilities.ConvertMacToGuid(bluetoothAddress);
      }

      public Guid AdapterId { get; }

      public async Task<IReadOnlyList<IBluetoothNeighbor>> DiscoverAsync() {
         var devices = await bluetoothDiscoveryFacade.DiscoverPeersAsync().ConfigureAwait(false);
         var neighbors = new List<IBluetoothNeighbor>();
         foreach (var device in devices) {
            var neighborId = MacUtilities.ConvertMacToGuid(device.Address);
            Neighbor neighbor;
            if (!neighborsById.TryGetValue(neighborId, out neighbor)) {
               neighbor = new Neighbor(this, inboundBluetoothSocketTable, device);
               neighborsById[neighborId] = neighbor;
            }
            neighbors.Add(neighbor);
         }
         return neighbors;
      }

      public class Neighbor : IBluetoothNeighbor {
         private readonly AsyncLock synchronization = new AsyncLock();
         private readonly BinaryLatchChannel disconnectedChannel = new BinaryLatchChannel(true);
         private readonly DisconnectableChannel<byte[], NotConnectedException> inboundChannel;

         private readonly AndroidBluetoothAdapter androidBluetoothAdapter;
         private readonly InboundBluetoothSocketTable inboundBluetoothSocketTable;
         private readonly BluetoothDevice device;
         private BluetoothSocket socket;

         public Neighbor(AndroidBluetoothAdapter androidBluetoothAdapter, InboundBluetoothSocketTable inboundBluetoothSocketTable, BluetoothDevice device) {
            this.androidBluetoothAdapter = androidBluetoothAdapter;
            this.inboundBluetoothSocketTable = inboundBluetoothSocketTable;
            this.device = device;

            inboundChannel = new DisconnectableChannel<byte[], NotConnectedException>(disconnectedChannel, ChannelFactory.Nonblocking<byte[]>());
         }

         public string Name => device.Name;
         public string MacAddress => device.Address;
         public Guid AdapterId => MacUtilities.ConvertMacToGuid(device.Address);
         public bool IsConnected => !disconnectedChannel.IsClosed;
         public ReadableChannel<byte[]> InboundChannel => inboundChannel;

         private async Task HandshakeAsync() {
            using (await synchronization.LockAsync().ConfigureAwait(false)) {
               var isServer = androidBluetoothAdapter.AdapterId.CompareTo(AdapterId) > 0;

               // Michael's laptop is always the client as windows client doesn't understand being a server.
               if (Name?.Contains("DESKTOP") ?? false) {
                  isServer = true;
               }

               if (isServer) {
                  socket = await inboundBluetoothSocketTable.TakeAsyncOrTimeout(device).ConfigureAwait(false);
               } else {
                  var socketBox = new AsyncBox<BluetoothSocket>();
                  new Thread(() => {
                     try {
                        socketBox.SetResult(device.CreateInsecureRfcommSocketToServiceRecord(CampfireNetBluetoothConstants.APP_UUID));
                     } catch (Exception e) {
                        socketBox.SetException(e);
                     }
                  }).Start();

                  socket = await socketBox.GetResultAsync().ConfigureAwait(false);
                  var connectedChannel = ChannelFactory.Nonblocking<bool>();

                  Go(async () => {
                     await socket.ConnectAsync().ConfigureAwait(false);
                     await connectedChannel.WriteAsync(true);
                  }).Forget();

                  bool isTimeout = false;
                  await new Select {
                     Case(ChannelFactory.Timer(5000), () => {
                        socket.Dispose();
                        isTimeout = true;
                     }),
                     Case(connectedChannel, () => {
                        // whee!
                     })
                  }.ConfigureAwait(false);
                  if (isTimeout) {
                     throw new TimeoutException();
                  }
               }
               disconnectedChannel.SetIsClosed(false);

               ChannelsExtensions.Go(async () => {
                  Console.WriteLine("Entered BT Reader Task");
                  var networkStream = socket.InputStream;
                  try {
                     while (!disconnectedChannel.IsClosed) {
                        Console.WriteLine("Reading BT Frame");
                        var dataLengthBuffer = await ReadBytesAsync(networkStream, 4).ConfigureAwait(false);
                        var dataLength = BitConverter.ToInt32(dataLengthBuffer, 0);
                        var data = await ReadBytesAsync(networkStream, dataLength).ConfigureAwait(false);
                        await inboundChannel.WriteAsync(data).ConfigureAwait(false);
                     }
                  } catch (Exception e) {
                     Console.WriteLine(e);
                     Teardown();
                  }
               }).Forget();
            }
         }

         public async Task<bool> TryHandshakeAsync() {
            try {
               await HandshakeAsync().ConfigureAwait(false);
               return true;
            } catch (TimeoutException) {
               return false;
            } catch (Java.IO.IOException) {
               return false;
            }
         }

         public async Task SendAsync(byte[] data) {
            using (await synchronization.LockAsync().ConfigureAwait(false)) {
               try {
                  var stream = socket.OutputStream;
                  await stream.WriteAsync(BitConverter.GetBytes(data.Length), 0, 4).ConfigureAwait(false);
                  await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
               } catch {
                  Teardown();
                  throw new NotConnectedException();
               }
            }
         }

         private void Teardown() {
            socket?.Dispose();
            socket = null;
            disconnectedChannel.SetIsClosed(true);
         }

         private async Task<byte[]> ReadBytesAsync(Stream stream, int count) {
            var buffer = new byte[count];
            int index = 0;
            while (index < count) {
               var bytesRead = await stream.ReadAsync(buffer, index, count - index).ConfigureAwait(false);
               index += bytesRead;
            }
            return buffer;
         }
      }
   }
}