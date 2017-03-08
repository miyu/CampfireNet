using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
      private readonly SortedList<ChatMessageDto, ChatMessageDto> messages = new SortedList<ChatMessageDto, ChatMessageDto>(new ChatMessageOrderComparer());
      private readonly Dictionary<IdentityHash, int> currentLogicalClock = new Dictionary<IdentityHash, int>();
      private readonly CampfireChatClient campfireChatClient;
      private readonly ChatMessageSender chatMessageSender;
      private int outboundMessageSequenceNumber;

      public ChatRoomContext(CampfireChatClient campfireChatClient, ChatMessageSender chatMessageSender, IdentityHash chatroomIdentityHash) {
         this.campfireChatClient = campfireChatClient;
         this.chatMessageSender = chatMessageSender;

         ChatroomIdentityHash = chatroomIdentityHash;
      }

      public IdentityHash UserIdentityHash => IdentityHash.GetFlyweight(campfireChatClient.CampfireNetClient.Identity.PublicIdentityHash);
      public IdentityHash ChatroomIdentityHash { get; }
      public bool IsUnicast { get; set; }
      public string FriendlyName { get; set; }

      public Dictionary<IdentityHash, int> CaptureCurrentLogicalClock() {
         lock (synchronization) {
            return new Dictionary<IdentityHash, int>(currentLogicalClock);
         }
      }

      public Dictionary<IdentityHash, int> IncrementThenCaptureCurrentLogicalClock() {
         lock (synchronization) {
            int currentValue;
            currentLogicalClock.TryGetValue(UserIdentityHash, out currentValue);
            currentLogicalClock[UserIdentityHash] = currentValue + 1;
            return new Dictionary<IdentityHash, int>(currentLogicalClock);
         }
      }

      public ChatRoomViewModel CreateViewModelAndSubscribe(ChatMessageReceivedCallback messageReceivedCallback) {
         lock(synchronization) {
            var previousMessages = messages.ToList().Select(kvp => kvp.Key).ToList();
            var viewModel = new ChatRoomViewModel(this, previousMessages, messageReceivedCallback);
            viewModels.AddOrThrow(viewModel);
            return viewModel;
         }
      }

      public void SendMessage(ChatMessageDto message) {
         // This will write to Merkle Tree which will trigger message received.
         Console.WriteLine("Entering send message 3");
         chatMessageSender.Send(ChatroomIdentityHash, message).Forget();
         Console.WriteLine("Exiting send message 3");
      }

      public void SendMessage(ChatMessageContentType contentType, byte[] contentRaw) {
         lock (synchronization) {
            Console.WriteLine("Entering send message 2");
            var message = new ChatMessageDto {
               SequenceNumber = Interlocked.Increment(ref outboundMessageSequenceNumber),
               LogicalClock = IncrementThenCaptureCurrentLogicalClock(),
               FriendlySenderName = campfireChatClient.LocalFriendlyName,
               ContentType = ChatMessageContentType.Text,
               ContentRaw = contentRaw
            };
            Console.WriteLine("Made message");
            SendMessage(message);
            Console.WriteLine("Exiting send message 2");
         }
      }

      public void HandleMessageReceived(ChatMessageDto message) {
         lock(synchronization) {
            messages.Add(message, message);

            foreach (var kvp in message.LogicalClock) {
               int storedCounter;
               if (!currentLogicalClock.TryGetValue(kvp.Key, out storedCounter) || storedCounter < kvp.Value) {
                  currentLogicalClock[kvp.Key] = kvp.Value;
               }
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
         Console.WriteLine("Inside send message");
         viewModel.Context.SendMessage(ChatMessageContentType.Text, Encoding.UTF8.GetBytes(s));
         Console.WriteLine("Finishing send message");
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