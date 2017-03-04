using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using System.Collections;

namespace CampfireChat
{
	[Activity(Label = "New Message", ParentActivity = typeof(MainActivity))]
    [IntentFilter(new string[] { "android.intent.action.SEARCH" })]
    [MetaData(("android.app.searchable"), Resource = "@xml/searchable")]
    public class NewChatActivity : Activity
	{
        private RecyclerView contactlistRecyclerView;
        private RecyclerView.Adapter contactlistAdapter;
        private RecyclerView.LayoutManager contactlistLayoutManager;
        protected override void OnCreate(Bundle savedInstanceState)
		{
            ContactEntry[] testEntries = createTestData();
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.NewChat);
			var toolbar = FindViewById<Android.Widget.Toolbar>(Resource.Id.Toolbar);
			SetActionBar(toolbar);
			this.ActionBar.SetDisplayHomeAsUpEnabled(true);
            contactlistRecyclerView = (RecyclerView)FindViewById(Resource.Id.ContactList);
            contactlistRecyclerView.HasFixedSize = true;

            contactlistLayoutManager = new LinearLayoutManager(this);
            contactlistRecyclerView.SetLayoutManager(contactlistLayoutManager);

            contactlistAdapter = new ContactlistAdapter(testEntries);
            contactlistRecyclerView.SetAdapter(contactlistAdapter);

            var search = (Android.Widget.SearchView)FindViewById<Android.Widget.SearchView>(Resource.Id.SearchFriend);
            HandleIntent(Intent);
        }

		public override bool OnCreateOptionsMenu(IMenu menu)
		{
			MenuInflater.Inflate(Resource.Menu.new_chat_menu, menu);
            SearchManager searchManager = (SearchManager)GetSystemService(Context.SearchService);
            Android.Widget.SearchView searchView = (Android.Widget.SearchView)menu.FindItem(Resource.Id.SearchFriend).ActionView;
            searchView.SetSearchableInfo(searchManager.GetSearchableInfo(ComponentName));
            return base.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
		{
			return base.OnOptionsItemSelected(item);
		}

        protected override void OnNewIntent(Intent intent)
        {
            HandleIntent(intent);
        }

        private void HandleIntent(Intent intent)
        {
            if (Intent.ActionSearch.Equals(intent.Action))
            {
                string query = intent.GetStringExtra(SearchManager.Query);
                UpdateResults(query);
            }
        }

        private void UpdateResults(string query)
        {
            ContactEntry[] entries = createTestData();
            List<ContactEntry> updated = new List<ContactEntry>();
            for (int i = 0; i < entries.Length; i++) {
                string name = entries[i].Name.ToLower();
                string tag = entries[i].Tag.ToLower();
                if (name.Contains(query) || tag.Contains(query))
                {
                    updated.Add(entries[i]);
                }
            }
            contactlistRecyclerView.SwapAdapter(new ContactlistAdapter(updated.ToArray()), false);
        }

        public ContactEntry[] createTestData()
        {
            string[] testName = {"Love", "Air", "Shoes", "Hair", "Perfume",
                "Obfuscation", "Clock", "Game", "Scroll", "Lion", "Chrome", "Tresure", "Charm" };

            ContactEntry[] entries = new ContactEntry[testName.Length];

            for (var i = 0; i < entries.Length; i++)
            {
                entries[i] = new ContactEntry(testName[i], testName[testName.Length-1-i]);
            }

            return entries;
        }
    }
}