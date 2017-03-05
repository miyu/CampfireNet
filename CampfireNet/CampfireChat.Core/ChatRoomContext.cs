using System;
using System.Collections.Generic;
using System.Linq;
using CampfireNet.Utilities.Collections;

namespace CampfireChat {
   public class ChatRoomContext {
      private readonly object synchronization = new object();
      private readonly ConcurrentSet<ChatRoomViewModel> viewModels = new ConcurrentSet<ChatRoomViewModel>();
      private readonly SortedSet<ChatMessageDto> messages;

      public ChatRoomContext(SortedSet<ChatMessageDto> messages) {
         this.messages = messages;
      }

      public bool IsUnicast { get; set; }
      public string FriendlyName { get; set; }

      public ChatRoomViewModel CreateViewModelAndSubscribe(ChatMessageReceivedCallback messageReceivedCallback) {
         lock(synchronization) {
            var previousMessages = messages.ToList();
            var viewModel = new ChatRoomViewModel(this, previousMessages, messageReceivedCallback);
            viewModels.AddOrThrow(viewModel);
            return viewModel;
         }
      }
   }

   public class ChatRoomViewModel {
      public ChatRoomViewModel(ChatRoomContext context, List<ChatMessageDto> initialMessages, ChatMessageReceivedCallback messageReceivedCallback) {
         Context = context;
         InitialMessages = initialMessages;
         MessageReceivedCallback = messageReceivedCallback;
      }

      public ChatRoomContext Context { get; }
      public IReadOnlyList<ChatMessageDto> InitialMessages { get; }
      public ChatMessageReceivedCallback MessageReceivedCallback { get; }
   }

   public delegate void ChatMessageReceivedCallback(ChatRoomContext sender, ChatMessageReceivedEventArgs e);

   public class ChatMessageReceivedEventArgs : EventArgs {
      public ChatMessageReceivedEventArgs(ChatRoomViewModel viewModel) {
         ViewModel = viewModel;
      }

      public ChatRoomViewModel ViewModel { get; }
   }
}