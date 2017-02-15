using CampfireNet.Utilities.Merkle;

namespace CampfireNet.IO.Packets
{
   public class GivePacket {
      public string NodeHash { get; set; }
      public MerkleNode Node { get; set; }
   }

   public class WhoisPacket {
      public byte[] IdHash { get; set; }
   }

   public class IdentPacket {
      public byte[] Id { get; set; }
      public string FriendlyName { get; set; }
   }
}
