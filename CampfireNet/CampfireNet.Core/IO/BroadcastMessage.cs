using System;
using CampfireNet.Identities;

namespace CampfireNet {
   public class BroadcastMessage {
      public byte[] SourceId { get; set; }
      public byte[] DestinationId { get; set; }
      public byte[] DecryptedPayload { get; set; }

      public BroadcastMessageDto Dto { get; set; }
   }
}
