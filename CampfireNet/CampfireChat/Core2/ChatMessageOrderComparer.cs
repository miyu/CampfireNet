using System.Collections.Generic;
using System.Linq;
using CampfireNet.Utilities;

namespace CampfireChat {
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