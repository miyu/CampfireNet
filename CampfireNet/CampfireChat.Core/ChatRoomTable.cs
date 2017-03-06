using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CampfireNet.Identities;
using CampfireNet.Utilities;

namespace CampfireChat {
   public class ChatRoomTable {
      private readonly ConcurrentDictionary<IdentityHash, ChatRoomContext> descriptorsByRoomIdHash = new ConcurrentDictionary<IdentityHash, ChatRoomContext>();

      public bool TryLookup(IdentityHash destination, out ChatRoomContext context) {
         return descriptorsByRoomIdHash.TryGetValue(destination, out context);
      }

      public ChatRoomContext GetOrCreate(IdentityHash destination) {
         var messages = new SortedSet<ChatMessageDto>(new ChatMessageOrderComparer());
         return descriptorsByRoomIdHash.GetOrAdd(destination, new ChatRoomContext(messages));
      }
   }

   public class ChatMessageOrderComparer : IComparer<ChatMessageDto> {
      public int Compare(ChatMessageDto x, ChatMessageDto y) {
         if ((x.LogicalClock.Count == 0) != (y.LogicalClock.Count == 0)) {
            throw new InvalidStateException("Messages differed in whether logical clock in use");
         }

         if (x.LogicalClock != null) {
            foreach (var commonClockKey in x.LogicalClock.Keys.Intersect(y.LogicalClock.Keys)) {
               var xCounter = x.LogicalClock[commonClockKey];
               var yCounter = y.LogicalClock[commonClockKey];
               if (xCounter != yCounter) {
                  return xCounter.CompareTo(yCounter);
               }
            }
         }

         if (x.BroadcastMessage.SourceId == y.BroadcastMessage.SourceId) {
            if (x.SequenceNumber != y.SequenceNumber) {
               return x.SequenceNumber.CompareTo(y.SequenceNumber);
            }
         }

         return x.LocalTimestamp.CompareTo(y.LocalTimestamp);
      }
   }
}