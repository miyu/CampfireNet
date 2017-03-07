using CampfireNet.Utilities.Merkle;

namespace CampfireNet.IO.Packets
{
   public class GivePacket {
      public string NodeHash { get; set; }
      public MerkleNode Node { get; set; }
   }
}
