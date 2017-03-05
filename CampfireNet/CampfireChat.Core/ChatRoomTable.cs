using System.Collections.Concurrent;
using CampfireNet.Utilities;

namespace CampfireChat {
   public class ChatRoomTable {
      private readonly ConcurrentDictionary<string, ChatRoomContext> descriptorsByRoomIdHash = new ConcurrentDictionary<string, ChatRoomContext>();

      public bool TryLookup(byte[] destination, out ChatRoomContext context) {
         return descriptorsByRoomIdHash.TryGetValue(destination.ToHexString(), out context);
      }
   }
}