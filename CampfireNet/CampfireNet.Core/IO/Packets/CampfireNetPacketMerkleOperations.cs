using CampfireNet.Utilities.Merkle;

namespace CampfireNet.IO.Packets
{

	public class CampfireNetPacketMerkleOperations : IItemOperations<BroadcastMessage>
	{
		public byte[] Serialize(BroadcastMessage item)
		{
			return item.Data;
		}
	}
}
