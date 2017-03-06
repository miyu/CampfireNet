
using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using System.Collections.Generic;

namespace CampfireChat {
   [Activity(Label = "New Message")]
   [IntentFilter(new string[] { "android.intent.action.SEARCH" })]
   [MetaData(("android.app.searchable"), Resource = "@xml/searchable")]
   public class NewChatActivity : Activity {
      private RecyclerView contactlistRecyclerView;
      private RecyclerView.Adapter contactlistAdapter;
      private RecyclerView.LayoutManager contactlistLayoutManager;

      protected override void OnCreate(Bundle savedInstanceState) {
         List<ContactEntry> testEntries = createTestData();
         base.OnCreate(savedInstanceState);
         SetContentView(Resource.Layout.NewChat);

         var toolbar = FindViewById<Android.Widget.Toolbar>(Resource.Id.Toolbar);
         SetActionBar(toolbar);
         ActionBar.SetDisplayHomeAsUpEnabled(true);
         contactlistRecyclerView = (RecyclerView)FindViewById(Resource.Id.ContactList);
         contactlistRecyclerView.HasFixedSize = true;

         contactlistLayoutManager = new LinearLayoutManager(this);
         contactlistRecyclerView.SetLayoutManager(contactlistLayoutManager);

         contactlistAdapter = new ContactlistAdapter(testEntries);
         contactlistRecyclerView.SetAdapter(contactlistAdapter);
         
      }

      public override bool OnCreateOptionsMenu(IMenu menu) {
         MenuInflater.Inflate(Resource.Menu.new_chat_menu, menu);

         SearchManager searchManager = (SearchManager)GetSystemService(SearchService);
         Android.Widget.SearchView searchView = (Android.Widget.SearchView)menu.FindItem(Resource.Id.SearchFriend).ActionView;
         searchView.SetSearchableInfo(searchManager.GetSearchableInfo(ComponentName));
         
         searchView.QueryTextChange += (sender, e) => {
               string query = e.NewText;
               Console.WriteLine($"Got {query}");
               UpdateResults(query);
         };

         return base.OnCreateOptionsMenu(menu);
      }

      public override bool OnOptionsItemSelected(IMenuItem item) {
         if (item.ItemId == Android.Resource.Id.Home) {
            Finish();
         }

         return base.OnOptionsItemSelected(item);
      }

      private void UpdateResults(string query) {
         List<ContactEntry> entries = (contactlistAdapter as ContactlistAdapter).FullEntries;
         List<ContactEntry> updated = new List<ContactEntry>();

         for (int i = 0; i < entries.Count; i++) {
            string name = entries[i].Name.ToLower();
            string tag = entries[i].Tag.ToLower();
            if (name.Contains(query) || tag.Contains(query)) {
               updated.Add(entries[i]);
            }
         }
         (contactlistAdapter as ContactlistAdapter).CurrentEntries = updated;
         contactlistAdapter.NotifyDataSetChanged();
      }

      public List<ContactEntry> createTestData() {
         string[] testName = {"Love", "Air", "Shoes", "Hair", "Perfume",
            "Obfuscation", "Clock", "Game", "Scroll", "Lion", "Chrome", "Tresure", "Charm" };

         List<ContactEntry> entries = new List<ContactEntry>();

         for (var i = 0; i < testName.Length; i++) {
            entries.Add(new ContactEntry(testName[i], testName[testName.Length - 1 - i]));
         }

         return entries;
      }
   }
}