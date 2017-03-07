using System;
using CampfireNet.Identities;

namespace CampfireNet {
   public class BroadcastMessage {
      public IdentityHash SourceId { get; set; }
      public IdentityHash DestinationId { get; set; }
      public byte[] DecryptedPayload { get; set; }

      public BroadcastMessageDto Dto { get; set; }
   }
}
