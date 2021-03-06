using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Android.Bluetooth;
using CampfireNet.Utilities;
using CampfireNet.Utilities.AsyncPrimatives;
using CampfireNet.Utilities.Channels;
using static CampfireNet.Utilities.Channels.ChannelsExtensions;
using CampfireNet.IO;

namespace AndroidTest.Droid
{
	public class InboundBluetoothSocketTable
	{
		private readonly AsyncLock synchronization = new AsyncLock();
		private readonly ConcurrentDictionary<Guid, Channel<BluetoothSocket>> pendingRequests = new ConcurrentDictionary<Guid, Channel<BluetoothSocket>>();

		public async Task GiveAsync(BluetoothSocket socket)
		{
			Channel<BluetoothSocket> channel;
			using (await synchronization.LockAsync().ConfigureAwait(false))
			{
				var deviceId = MacUtilities.ConvertMacToGuid(socket.RemoteDevice.Address);
            Console.WriteLine("INBOUND CONNECTION FROM " + deviceId + " aka " + (socket.RemoteDevice.Name ?? "[unknown]"));
				channel = pendingRequests.GetOrAdd(deviceId, add => ChannelFactory.Nonblocking<BluetoothSocket>());
				await ChannelsExtensions.WriteAsync(channel, socket).ConfigureAwait(false);
			}
		}

		public async Task<BluetoothSocket> TakeAsyncOrTimeout(BluetoothDevice device)
		{
			Channel<BluetoothSocket> channel;
			using (await synchronization.LockAsync().ConfigureAwait(false))
			{
				var deviceId = MacUtilities.ConvertMacToGuid(device.Address);
				channel = pendingRequests.GetOrAdd(deviceId, add => ChannelFactory.Nonblocking<BluetoothSocket>());
			}
			bool isTimeout = false;
			BluetoothSocket result = null;
			await new Select {
			Case(ChannelFactory.Timeout(10000), () => {
			   isTimeout = true;
			}),
			Case(channel, x => {
			   result = x;
			})
		 }.ConfigureAwait(false);
			if (isTimeout)
			{
				throw new TimeoutException();
			}
			BluetoothSocket temporary;
			while (channel.TryRead(out temporary))
			{
				result = temporary;
			}
			return result;
		}
	}
}