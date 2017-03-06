using System;
using System.Collections.Generic;
using System.Text;
using CampfireNet;

namespace CampfireChat {
   public class CampfireChatClientFactory {
      public CampfireChatClient Create(CampfireNetClient campfireNetClient) {
         var chatRoomTable = new ChatRoomTable();
         var messageSender = new ChatMessageSender(campfireNetClient, chatRoomTable);
         var campfireChatClient = new CampfireChatClient(campfireNetClient, chatRoomTable, messageSender);
         return campfireChatClient;
      }
   }

   public class CampfireChatClient : IDisposable {
      public CampfireChatClient(CampfireNetClient campfireNetClient, ChatRoomTable chatRoomTable, ChatMessageSender messageSender) {
         CampfireNetClient = campfireNetClient;
         ChatRoomTable = chatRoomTable;
         MessageSender = messageSender;
      }

      public CampfireNetClient CampfireNetClient { get; }
      public ChatRoomTable ChatRoomTable { get; }
      public ChatMessageSender MessageSender { get; }

      public void Initialize() {
         CampfireNetClient.MessageReceived += HandleClientMessageReceived;
      }

      private void HandleClientMessageReceived(MessageReceivedEventArgs e) {
         var dto = CampfireChatSerializer.Deserialize(e);
         switch (dto.GetType().Name) {
            case nameof(ChatMessageDto):
               var message = (ChatMessageDto)dto;
               ChatRoomTable.GetOrCreate(message.BroadcastMessage.DestinationId);
               break;
         }
      }

      public void Dispose() {
         CampfireNetClient.MessageReceived -= HandleClientMessageReceived;
      }
   }
}
