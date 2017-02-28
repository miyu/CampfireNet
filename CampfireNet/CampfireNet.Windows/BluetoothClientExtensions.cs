using System;
using System.Threading.Tasks;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace CampfireNet.Simulator {
   public static class BluetoothClientExtensions {
      public static Task<BluetoothDeviceInfo[]> DiscoverDevicesInRangeAsync(this BluetoothClient client) {
         return Task.Factory.FromAsync(client.BeginDiscoverDefault, client.EndDiscoverDevices, null);
      }

      private static IAsyncResult BeginDiscoverDefault(this BluetoothClient client, AsyncCallback callback, object state) {
         return client.BeginDiscoverDevices(32, true, true, true, true, callback, state);
      }

      public static Task ConnectAsync(this BluetoothClient client, BluetoothAddress address, Guid service) {
         return Task.Factory.FromAsync(client.BeginConnect, client.EndConnect, address, service, null);
      }

      public static Task<ServiceRecord[]> GetServiceRecordsAsync(this BluetoothDeviceInfo deviceInfo, Guid serviceGuid) {
         return Task.Factory.FromAsync(deviceInfo.BeginGetServiceRecords, deviceInfo.EndGetServiceRecords, serviceGuid, null);
      }
   }
}