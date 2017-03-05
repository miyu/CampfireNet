using System;
using System.Threading.Tasks;
using CampfireNet;
using CampfireNet.Utilities;

namespace CampfireChat {
   public class ChatMessageSender {
      private readonly CampfireNetClient client;
      private readonly JoinedChatRoomTable joinedChatRoomTable;

      public ChatMessageSender(CampfireNetClient client, JoinedChatRoomTable joinedChatRoomTable) {
         this.client = client;
         this.joinedChatRoomTable = joinedChatRoomTable;
      }

      public async Task Send(byte[] destination, ChatMessageDto messageDto) {
         ChatRoomContext chatroomContext;
         if (!joinedChatRoomTable.TryLookup(destination, out chatroomContext)) {
            throw new InvalidStateException("Cannot send message to undiscovered chatroom/user.");
         }
         
         var messageDtoBytes = CampfireChatSerializer.GetBytes(messageDto);

         if (chatroomContext.IsUnicast) {
            await client.UnicastAsync(destination, messageDtoBytes);
         } else {
            await client.BroadcastAsync(messageDtoBytes);
         }
      }
   }
}