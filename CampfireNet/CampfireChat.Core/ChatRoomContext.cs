using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CampfireNet.Identities;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Collections;

namespace CampfireChat {
   public class CampfireChatSettings {
      public string LocalFriendlyName { get; set; } = "Anonymous";
   }

   public class ChatRoomContext {
      private readonly object synchronization = new object();
      private readonly ConcurrentSet<ChatRoomViewModel> viewModels = new ConcurrentSet<ChatRoomViewModel>();
      private readonly SortedSet<ChatMessageDto> messages = new SortedSet<ChatMessageDto>(new ChatMessageOrderComparer());
      private readonly Dictionary<IdentityHash, int> currentLogicalClock = new Dictionary<IdentityHash, int>();
      private readonly CampfireChatSettings campfireChatSettings;
      private readonly ChatMessageSender chatMessageSender;
      private int outboundMessageSequenceNumber;

      public ChatRoomContext(CampfireChatSettings campfireChatSettings, ChatMessageSender chatMessageSender, IdentityHash identityHash) {
         this.campfireChatSettings = campfireChatSettings;
         this.chatMessageSender = chatMessageSender;

         IdentityHash = identityHash;
      }

      public IdentityHash IdentityHash { get; }
      public bool IsUnicast { get; set; }
      public string FriendlyName { get; set; }

      public Dictionary<IdentityHash, int> CaptureCurrentLogicalClock() {
         return new Dictionary<IdentityHash, int>(currentLogicalClock);
      }

      public ChatRoomViewModel CreateViewModelAndSubscribe(ChatMessageReceivedCallback messageReceivedCallback) {
         lock(synchronization) {
            var previousMessages = messages.ToList();
            var viewModel = new ChatRoomViewModel(this, previousMessages, messageReceivedCallback);
            viewModels.AddOrThrow(viewModel);
            return viewModel;
         }
      }

      public void SendMessage(ChatMessageDto message) {
         // This will write to Merkle Tree which will trigger message received.
         chatMessageSender.Send(IdentityHash, message).Forget();
      }

      internal void SendMessage(ChatMessageContentType contentType, byte[] contentRaw) {
         var message = new ChatMessageDto {
            SequenceNumber = Interlocked.Increment(ref outboundMessageSequenceNumber),
            LogicalClock = CaptureCurrentLogicalClock(),
            FriendlySenderName = campfireChatSettings.LocalFriendlyName,
            ContentType = ChatMessageContentType.Text,
            ContentRaw = contentRaw
         };
         chatMessageSender.Send(IdentityHash, message).Forget();
      }

      public void HandleMessageReceived(ChatMessageDto message) {
         lock(synchronization) {
            messages.Add(message);

            foreach (var kvp in message.LogicalClock) {
               int storedCounter;
               if (currentLogicalClock.TryGetValue(kvp.Key, out storedCounter) && storedCounter > kvp.Value) {
                  continue;
               }
               currentLogicalClock[kvp.Key] = kvp.Value;
            }

            foreach (var viewModel in viewModels) {
               viewModel.MessageReceivedCallback(this, new ChatMessageReceivedEventArgs(viewModel, message));
            }
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

   public static class ChatRoomViewModelExtensions {
      public static void SendMessageText(this ChatRoomViewModel viewModel, string s) {
         viewModel.Context.SendMessage(ChatMessageContentType.Text, Encoding.UTF8.GetBytes(s));
      }
   }

   public delegate void ChatMessageReceivedCallback(ChatRoomContext sender, ChatMessageReceivedEventArgs e);

   public class ChatMessageReceivedEventArgs : EventArgs {
      public ChatMessageReceivedEventArgs(ChatRoomViewModel viewModel, ChatMessageDto message) {
         ViewModel = viewModel;
         Message = message;
      }

      public ChatRoomViewModel ViewModel { get; }
      public ChatMessageDto Message { get; }
   }
}