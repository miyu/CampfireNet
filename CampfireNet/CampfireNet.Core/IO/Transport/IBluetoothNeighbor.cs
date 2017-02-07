using System;
using System.Threading.Tasks;
using CampfireNet.Utilities.ChannelsExtensions;

namespace CampfireNet.IO.Transport
{
	public interface IBluetoothNeighbor
	{
		Guid AdapterId { get; }
		bool IsConnected { get; }
		ReadableChannel<byte[]> InboundChannel { get; }
		Task<bool> TryHandshakeAsync();
		Task SendAsync(byte[] data);
	}
}
