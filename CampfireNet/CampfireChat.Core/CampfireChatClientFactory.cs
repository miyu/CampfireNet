using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using CampfireNet;
using CampfireNet.Identities;

namespace CampfireChat {
   public class CampfireChatClientFactory {
      public static CampfireChatClient Create(CampfireNetClient campfireNetClient) {
         var campfireChatSettings = new CampfireChatSettings {
            LocalFriendlyName = "Anonymous"
         };
         var chatRoomTable = new ChatRoomTable();
         var messageSender = new ChatMessageSender(campfireNetClient, chatRoomTable);
         var campfireChatClient = new CampfireChatClient(campfireNetClient, campfireChatSettings, chatRoomTable, messageSender);

         chatRoomTable.SetChatMessageSender(messageSender);
         chatRoomTable.SetCampfireChatClient(campfireChatClient);
         campfireChatClient.Initialize();
         return campfireChatClient;
      }
   }

   public class CampfireChatClient : IDisposable {
      private readonly CampfireChatSettings campfireChatSettings;

      public CampfireChatClient(CampfireNetClient campfireNetClient, CampfireChatSettings campfireChatSettings, ChatRoomTable chatRoomTable, ChatMessageSender messageSender) {
         this.campfireChatSettings = campfireChatSettings;
         CampfireNetClient = campfireNetClient;
         ChatRoomTable = chatRoomTable;
         MessageSender = messageSender;
      }

      public CampfireNetClient CampfireNetClient { get; }
      public ChatRoomTable ChatRoomTable { get; }
      public ChatMessageSender MessageSender { get; }
      public string LocalFriendlyName {
         get { return campfireChatSettings.LocalFriendlyName; }
         set { campfireChatSettings.LocalFriendlyName = value; }
      }

      public void Initialize() {
         CampfireNetClient.MessageSent += HandleClientMessageReceivedOrSent;
         CampfireNetClient.MessageReceived += HandleClientMessageReceivedOrSent;
      }

      private void HandleClientMessageReceivedOrSent(MessageReceivedEventArgs e) {
         Console.WriteLine("Starting handle");
         var dto = CampfireChatSerializer.Deserialize(e);
         switch (dto.GetType().Name) {
            case nameof(ChatMessageDto):
               var message = (ChatMessageDto)dto;
               Console.WriteLine("Calling handle now");
               ChatRoomTable.GetOrCreate(message.BroadcastMessage.DestinationId).HandleMessageReceived(message);
               break;
         }
      }

      public ChatRoomViewModel CreateChatRoomViewModelByNameAndSubscribe(string chatroomName, ChatMessageReceivedCallback messageReceivedCallback) {
         var destinationHash = IdentityHash.GetFlyweight(CryptoUtil.GetHash(Encoding.UTF8.GetBytes(chatroomName)));
         return CreateChatRoomViewModelByIdentityHashAndSubscribe(destinationHash, messageReceivedCallback);
      }

      public ChatRoomViewModel CreateChatRoomViewModelByIdentityHashAndSubscribe(IdentityHash chatroomIdentityHash, ChatMessageReceivedCallback messageReceivedCallback) {
         var chatRoomContext = ChatRoomTable.GetOrCreate(chatroomIdentityHash);
         return chatRoomContext.CreateViewModelAndSubscribe(messageReceivedCallback);
      }

      public ChatRoomContext ConfigurePublicChatRoom(string name) {
         var roomHashBytes = Encoding.UTF8.GetBytes(name);
         return ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(CryptoUtil.GetHash(roomHashBytes)));
      }

      public ChatRoomContext ConfigurePrivateChatRoom(IdentityHash hash, byte[] symmetricKey) {
         CampfireNetClient.IdentityManager.AddMulticastKey(hash, symmetricKey);
         return ChatRoomTable.GetOrCreate(hash);
      }

      public void Dispose() {
         CampfireNetClient.MessageSent -= HandleClientMessageReceivedOrSent;
         CampfireNetClient.MessageReceived -= HandleClientMessageReceivedOrSent;
      }
   }
}
