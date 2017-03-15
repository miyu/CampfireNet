
using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using AndroidTest.Droid;
using CampfireNet.Identities;
using Encoding = System.Text.Encoding;

namespace CampfireChat {
   [Activity(Label = "CampfireChat", Theme = "@style/CampTheme")]
   public class MainActivity : Activity {

      private RecyclerView chatlistRecyclerView;
      private ChatlistAdapter chatlistAdapter;
      private RecyclerView.LayoutManager chatlistLayoutManager;

      private Handler uiHandler;

      protected override void OnCreate(Bundle savedInstanceState) {
         base.OnCreate(savedInstanceState);
         SetContentView(Resource.Layout.Main);
         Window.SetTitle("Chats");

         var toolbar = FindViewById<Android.Widget.Toolbar>(Resource.Id.Toolbar);
         SetActionBar(toolbar);

         var prefs = Application.Context.GetSharedPreferences("CampfireChat", FileCreationMode.Private);

         if (Globals.CampfireChatClient.LocalFriendlyName == null) {
            ShowUsernameDialog();
            Console.WriteLine($"Updating with name {Globals.CampfireNetClient.Identity.Name}");
         }

         uiHandler = new LambdaHandler(msg => {
            chatlistAdapter.AddEntry(chatlistAdapter.ItemCount, (ChatEntry)msg.Obj);
         });
      }

      protected override void OnStart() {
         base.OnStart();
         Setup();
      }

      public override bool OnCreateOptionsMenu(IMenu menu) {
         MenuInflater.Inflate(Resource.Menu.main_menu, menu);
         return base.OnCreateOptionsMenu(menu);
      }

      public override bool OnOptionsItemSelected(IMenuItem item) {
         Intent intent;
         switch (item.ItemId) {
            case Resource.Id.Settings:
               intent = new Intent(this, typeof(SettingsActivity));
               StartActivity(intent);
               break;
            case Resource.Id.AddChatRoom:
               intent = new Intent(this, typeof(NewChatActivity));
               StartActivity(intent);
               break;
            case Resource.Id.AddChatGroup:
               ShowGroupDialog();
               break;
         }
         return base.OnOptionsItemSelected(item);
      }

      public void Setup() {
         Console.WriteLine("Adding data");
         if (Globals.JoinedRooms == null) {
            Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(Identity.BROADCAST_ID)).FriendlyName = "Broadcast";
            Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(CryptoUtil.GetHash(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("General"))))).FriendlyName = "General";
            Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(CryptoUtil.GetHash(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("Test"))))).FriendlyName = "Test";

            Globals.CampfireNetClient.IdentityManager.AddMulticastKey(
               IdentityHash.GetFlyweight(CryptoUtil.GetHash(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("General")))),
               CryptoUtil.GetHash(Encoding.UTF8.GetBytes("General")));

            Globals.CampfireNetClient.IdentityManager.AddMulticastKey(
               IdentityHash.GetFlyweight(CryptoUtil.GetHash(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("Test")))),
               CryptoUtil.GetHash(Encoding.UTF8.GetBytes("Test")));

            Globals.JoinedRooms = new HashSet<byte[]> {
               Identity.BROADCAST_ID,
               CryptoUtil.GetHash(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("General"))),
               CryptoUtil.GetHash(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("Test"))),
            };
         }
         var testEntries = GetKnownRooms();


         chatlistRecyclerView = (RecyclerView)FindViewById(Resource.Id.ChatList);
         chatlistRecyclerView.HasFixedSize = true;

         chatlistLayoutManager = new LinearLayoutManager(this);
         chatlistRecyclerView.SetLayoutManager(chatlistLayoutManager);

         chatlistAdapter = new ChatlistAdapter(testEntries);
         chatlistAdapter.ItemClick += OnItemClick;
         chatlistRecyclerView.SetAdapter(chatlistAdapter);

      }

      private void OnItemClick(object sender, byte[] chatId) {
         Intent intent = new Intent(this, typeof(ChatActivity));
         intent.PutExtra("chatId", chatId);
         StartActivity(intent);
      }

      protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
         if (requestCode != Helper.REQUEST_ENABLE_BT)
            return;

         if (resultCode != Result.Ok) {
            Console.WriteLine("BT Setup failed!");
         }

         Setup();
      }

      private List<ChatEntry> GetKnownRooms() {
         var entries = new List<ChatEntry>();
         foreach (var roomKey in Globals.JoinedRooms) {
            ChatRoomContext context = Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(roomKey));
            entries.Add(new ChatEntry(roomKey, context));
         }

         return entries;
      }

      public void ShowUsernameDialog() {
         var transaction = FragmentManager.BeginTransaction();
         var dialog = new UsernameDialog();
         dialog.Show(transaction, "InputName");
      }

      public void ShowGroupDialog() {
         var transaction = FragmentManager.BeginTransaction();
         var dialog = new GroupDialog(uiHandler);
         dialog.Show(transaction, "JoinGroup");
      }
   }

}

