
using System;
using System.Collections.Generic;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using CampfireNet;
using CampfireNet.Identities;
using CampfireNet.Utilities;
using AndroidTest.Droid;
using Encoding = System.Text.Encoding;

namespace CampfireChat {
   [Activity(Label = "CampfireChat", MainLauncher = true, Icon = "@drawable/icon")]
   public class MainActivity : Activity {
      private const int REQUEST_ENABLE_BT = 1;

      private RecyclerView chatlistRecyclerView;
      private RecyclerView.Adapter chatlistAdapter;
      private RecyclerView.LayoutManager chatlistLayoutManager;

      protected override void OnCreate(Bundle savedInstanceState) {
         base.OnCreate(savedInstanceState);
         SetContentView(Resource.Layout.Main);
         Window.SetTitle("Chats");

         var toolbar = FindViewById<Android.Widget.Toolbar>(Resource.Id.Toolbar);
         SetActionBar(toolbar);
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
         }
         return base.OnOptionsItemSelected(item);
      }

      public void Setup() {
         var nativeBluetoothAdapter = BluetoothAdapter.DefaultAdapter;
         if (!nativeBluetoothAdapter.IsEnabled) {
            System.Console.WriteLine("Enabling bluetooth");
            Intent enableBtIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
            StartActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
            return;
         }

         bool isFirstRun = false;
         if (Globals.CampfireNetClient == null) {
            isFirstRun = true;
            var androidBluetoothAdapter = new AndroidBluetoothAdapterFactory().Create(this, ApplicationContext, nativeBluetoothAdapter);
            Globals.CampfireNetClient = CampfireNetClientBuilder.CreateNew()
                                             .WithDevelopmentNetworkClaims()
                                             .WithBluetoothAdapter(androidBluetoothAdapter)
                                             .Build();
         }
         var identity = Globals.CampfireNetClient.Identity;

         Console.WriteLine("Constructing client");

         if (Globals.CampfireChatClient == null) {
            Globals.CampfireChatClient = CampfireChatClientFactory.Create(Globals.CampfireNetClient);
         }

         Console.WriteLine("Adding data");
         if (Globals.JoinedRooms == null) {
            Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(Identity.BROADCAST_ID)).FriendlyName = "Broadcast 1";
            Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("General")))).FriendlyName = "General";
            Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("Test")))).FriendlyName = "Test";

            Globals.CampfireNetClient.IdentityManager.AddMulticastKey(
               IdentityHash.GetFlyweight(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("General"))),
               CryptoUtil.GetHash(Encoding.UTF8.GetBytes("General_Key")));

            Globals.CampfireNetClient.IdentityManager.AddMulticastKey(
               IdentityHash.GetFlyweight(CryptoUtil.GetHash(Encoding.UTF8.GetBytes("Test"))),
               CryptoUtil.GetHash(Encoding.UTF8.GetBytes("Test_Key")));

            Globals.JoinedRooms = new HashSet<byte[]> {
               Identity.BROADCAST_ID,
               CryptoUtil.GetHash(Encoding.UTF8.GetBytes("General")),
               CryptoUtil.GetHash(Encoding.UTF8.GetBytes("Test")),
            };
         }
         var testEntries = GetKnownRooms();


         chatlistRecyclerView = (RecyclerView)FindViewById(Resource.Id.ChatList);
         chatlistRecyclerView.HasFixedSize = true;

         chatlistLayoutManager = new LinearLayoutManager(this);
         chatlistRecyclerView.SetLayoutManager(chatlistLayoutManager);

         chatlistAdapter = new ChatlistAdapter(testEntries);
         ((ChatlistAdapter)chatlistAdapter).ItemClick += OnItemClick;
         chatlistRecyclerView.SetAdapter(chatlistAdapter);

         if (isFirstRun) {
            Globals.CampfireNetClient.RunAsync().Forget();
         }
      }

      private void OnItemClick(object sender, byte[] chatId) {
         Intent intent = new Intent(this, typeof(ChatActivity));
         intent.PutExtra("chatId", chatId);
         StartActivity(intent);
      }

      protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
         if (requestCode != REQUEST_ENABLE_BT)
            return;

         if (resultCode != Result.Ok) {
            System.Console.WriteLine("BT Setup failed!");
         }

         Setup();
      }

      public List<ChatEntry> CreateTestData() {
         string[] testData = { "Preview of a long message that goes beyond the lines",
            "Preview of a really really long message that really goes beyond the lines and is sure to overflow",
            "text here", "more longish text here", "Love", "Air", "Shoes", "Hair", "Perfume",
            "Obfuscation", "Clock", "Game", "Scroll", "Lion", "Chrome", "Tresure", "Charm" };

         var testNames = new string[5][];
         testNames[0] = new string[] { "Name1Test" };
         testNames[1] = new string[] { "Name2Test1", "Name2Test2" };
         testNames[2] = new string[] { "Name3Test1", "Name3Test2", "Name3Test3" };
         testNames[3] = new string[] { "Name4Test1", "Name4Test2", "Name4Test3", "Name4Test4" };
         testNames[4] = new string[] { "Name5Test1", "Name5Test2", "Name5Test3", "Name5Test4", "Name5Test5" };

         var entries = new List<ChatEntry>();

         //			for (var i = 0; i < testData.Length; i++) {
         //			   var names = i < testNames.Length ? testNames[i] : new string[] { "default" };
         //
         //			   entries.Add(new ChatEntry());
         //			}

         return entries;
      }

      private List<ChatEntry> GetKnownRooms() {
         var entries = new List<ChatEntry>();
         foreach (var roomKey in Globals.JoinedRooms) {
            ChatRoomContext context = Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(roomKey));
            entries.Add(new ChatEntry(roomKey, context));
         }

         return entries;
      }
   }

}

