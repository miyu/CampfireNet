using System.Collections.Concurrent;
using System.Collections.Generic;
using CampfireNet.Identities;

namespace CampfireChat {
   public class ChatRoomTable {
      private readonly ConcurrentDictionary<IdentityHash, ChatRoomContext> descriptorsByRoomIdHash = new ConcurrentDictionary<IdentityHash, ChatRoomContext>();
      private readonly CampfireChatSettings campfireChatSettings;
      private ChatMessageSender chatMessageSender;

      public ChatRoomTable(CampfireChatSettings campfireChatSettings) {
         this.campfireChatSettings = campfireChatSettings;
      }

      public void SetChatMessageSender(ChatMessageSender chatMessageSender) {
         this.chatMessageSender = chatMessageSender;
      }

      public bool TryLookup(IdentityHash destination, out ChatRoomContext context) {
         return descriptorsByRoomIdHash.TryGetValue(destination, out context);
      }

      public ChatRoomContext GetOrCreate(IdentityHash destination) {
         return descriptorsByRoomIdHash.GetOrAdd(destination, new ChatRoomContext(campfireChatSettings, chatMessageSender, destination));
      }
   }
}