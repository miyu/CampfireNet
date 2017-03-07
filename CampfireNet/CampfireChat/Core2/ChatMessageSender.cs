using System;
using System.Linq;
using System.Threading.Tasks;
using CampfireNet;
using CampfireNet.Identities;
using CampfireNet.Utilities;

namespace CampfireChat {
   public class ChatMessageSender {
      private readonly CampfireNetClient client;
      private readonly ChatRoomTable chatRoomTable;

      public ChatMessageSender(CampfireNetClient client, ChatRoomTable chatRoomTable) {
         this.client = client;
         this.chatRoomTable = chatRoomTable;
      }

      public async Task Send(IdentityHash destination, ChatMessageDto messageDto) {
         ChatRoomContext chatroomContext;
         if (!chatRoomTable.TryLookup(destination, out chatroomContext)) {
            throw new InvalidStateException("Cannot send message to undiscovered chatroom/user.");
         }
         
         var messageDtoBytes = CampfireChatSerializer.GetBytes(messageDto);

         if (chatroomContext.IsUnicast) {
            Console.WriteLine("Unicast messaging");
            await client.UnicastAsync(destination, messageDtoBytes);
         } else if (destination.Bytes.Any(v => v != 0)) {
            Console.WriteLine("Multicast messaging");
            await client.MulticastAsync(destination, messageDtoBytes);
         } else {
            Console.WriteLine("Broadcast messaging");
            await client.BroadcastAsync(messageDtoBytes);
         }
      }
   }
}