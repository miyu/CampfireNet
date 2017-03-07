using System.Collections.Generic;
using CampfireNet;

namespace CampfireChat {
   public static class Globals {
      public static CampfireNetClient CampfireNetClient { get; set; }
      public static CampfireChatClient CampfireChatClient { get; set; }
      public static HashSet<byte[]> JoinedRooms { get; set; }
   }
}