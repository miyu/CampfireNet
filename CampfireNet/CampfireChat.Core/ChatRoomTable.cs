using System.Collections.Concurrent;
using System.Collections.Generic;
using CampfireNet;
using CampfireNet.Identities;

namespace CampfireChat {
   public class ChatRoomTable {
      private readonly ConcurrentDictionary<IdentityHash, ChatRoomContext> descriptorsByRoomIdHash = new ConcurrentDictionary<IdentityHash, ChatRoomContext>();
      private ChatMessageSender chatMessageSender;
      private CampfireChatClient campfireChatClient;

      public void SetChatMessageSender(ChatMessageSender chatMessageSender) {
         this.chatMessageSender = chatMessageSender;
      }

      public void SetCampfireChatClient(CampfireChatClient campfireChatClient) {
         this.campfireChatClient = campfireChatClient;
      }

      public bool TryLookup(IdentityHash destination, out ChatRoomContext context) {
         return descriptorsByRoomIdHash.TryGetValue(destination, out context);
      }

      public ChatRoomContext GetOrCreate(IdentityHash destination) {
         return descriptorsByRoomIdHash.GetOrAdd(destination, new ChatRoomContext(campfireChatClient, chatMessageSender, destination));
      }
   }
}