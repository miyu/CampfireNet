using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CampfireNet.IO.Transport
{
	public interface IBluetoothAdapter
	{
		Guid AdapterId { get; }
		Task<IReadOnlyList<IBluetoothNeighbor>> DiscoverAsync();
	}
}
