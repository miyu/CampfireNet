using System.Collections.Generic;
using CampfireNet.Identities;

namespace CampfireNet.IO.Packets {
   public class IdentPacket {
      public byte[] Id { get; set; }
      public TrustChainNode[] TrustChain { get; set; }
   }
}