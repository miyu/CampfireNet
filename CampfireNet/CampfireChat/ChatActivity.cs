
using System;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using AndroidTest.Droid;
using CampfireNet.Identities;

namespace CampfireChat {
   [Activity(Label = "Chat")]
   public class ChatActivity : Activity {
      internal const int UPDATE_VIEW = 0;

      private RecyclerView chatRecyclerView;
      private ChatAdapter chatAdapter;
      private RecyclerView.LayoutManager chatLayoutManager;

      private ChatRoomContext chatRoomContext;
      private ChatRoomViewModel viewModel;

      private Handler uiHandler;

      protected override void OnCreate(Bundle savedInstanceState) {
         //         testMessages = new List<MessageEntry> {
         //            new MessageEntry("Name 1", "This is a test message 1"),
         //            new MessageEntry("Name 2", "This is a test message 2"),
         //            new MessageEntry("Name 3", "This is a test message 3"),
         //            new MessageEntry("Name 2", "This is a test message 4 really long message here one that is sure to overflow. How about some more text here and see if we can get it to three lines - or even more! How far can we go?"),
         //            new MessageEntry("Name 3", "This is a test message 5"),
         //            new MessageEntry("Name 1", "These are yet more messages designed to be long and take up space."),
         //            new MessageEntry("Name 2", "These are yet more messages designed to be long and take up space."),
         //            new MessageEntry("Name 3", "These are yet more messages designed to be long and take up space."),
         //            new MessageEntry("Name 1", "These are yet more messages designed to be long and take up space."),
         //            new MessageEntry("Name 2", "These are yet more messages designed to be long and take up space.")
         //         };

         base.OnCreate(savedInstanceState);
         SetContentView(Resource.Layout.Chat);

         var toolbar = FindViewById<Android.Widget.Toolbar>(Resource.Id.Toolbar);
         SetActionBar(toolbar);
         ActionBar.SetDisplayHomeAsUpEnabled(true);

         chatRecyclerView = (RecyclerView)FindViewById(Resource.Id.Messages);
         chatRecyclerView.HasFixedSize = true;

         chatLayoutManager = new LinearLayoutManager(this);
         chatRecyclerView.SetLayoutManager(chatLayoutManager);

         chatAdapter = new ChatAdapter();
         chatAdapter.ItemClick += OnItemClick;
         chatRecyclerView.SetAdapter(chatAdapter);

         var chatId = Intent.GetByteArrayExtra("chatId");
         chatRoomContext = Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(chatId));
         Title = chatRoomContext.FriendlyName;

         viewModel = chatRoomContext.CreateViewModelAndSubscribe((sender, e) => {
            Console.WriteLine("        ######## hitting add entry time");
            var message = e.Message;
            if (message.ContentType != ChatMessageContentType.Text)
               throw new NotImplementedException();
            //
            chatAdapter.AddEntry(new MessageEntry(message.BroadcastMessage.SourceId.ToString(), message.FriendlySenderName, Encoding.UTF8.GetString(message.ContentRaw)));
            uiHandler.ObtainMessage(UPDATE_VIEW, -1, 0).SendToTarget();
         });
         foreach (var message in viewModel.InitialMessages) {
            chatAdapter.AddEntry(new MessageEntry(message.BroadcastMessage.SourceId.ToString(), message.FriendlySenderName, Encoding.UTF8.GetString(message.ContentRaw)));
         }

         var sendButton = FindViewById<Button>(Resource.Id.SendMessage);
         sendButton.Click += HandleSendButtonClicked;

         uiHandler = new LambdaHandler(msg => {
            if (msg.What == UPDATE_VIEW) {
               var index = msg.Arg1 == -1 ? chatAdapter.Entries.Count - 1 : msg.Arg1;
               Console.WriteLine($"Updating item view at {index}");
               //               chatAdapter.NotifyItemChanged(index);
               chatAdapter.NotifyDataSetChanged();
               chatRecyclerView.GetLayoutManager().ScrollToPosition(chatAdapter.Entries.Count - 1);
            }
         });
      }

      private void OnItemClick(object sender, byte[] id) {
         Toast.MakeText(this, $"got id {Encoding.UTF8.GetString(id)}", ToastLength.Short).Show();
         Intent intent = new Intent(this, typeof(PersonActivity));
         intent.PutExtra("UserId", id);
         StartActivity(intent);
      }

      private void HandleSendButtonClicked(object sender, EventArgs e) {
         var sendTextbox = FindViewById<EditText>(Resource.Id.Input);
         var text = sendTextbox.Text;
         Console.WriteLine("I got: " + text);
         Console.WriteLine("~~~! 9");
         try {
            viewModel.SendMessageText(text);
         } catch (Exception ex) {
            Console.WriteLine("Got error!");
            Console.WriteLine(ex);
         }
         sendTextbox.Text = "";
         Console.WriteLine("Text box cleared");
      }

      public override bool OnCreateOptionsMenu(IMenu menu) {
         MenuInflater.Inflate(Resource.Menu.chat_menu, menu);
         return base.OnCreateOptionsMenu(menu);
      }

      public override bool OnOptionsItemSelected(IMenuItem item) {
         if (item.ItemId == Android.Resource.Id.Home) {
            Finish();
         }

         return base.OnOptionsItemSelected(item);
      }
   }
}
