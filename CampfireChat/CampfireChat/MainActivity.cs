using Android.App;
using Android.Widget;
using Android.OS;
using Android.Views;
using Android.Content;
using Android.Support.V7.Widget;

namespace CampfireChat
{
	[Activity(Label = "Chat List", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		private RecyclerView chatlistRecyclerView;
		private RecyclerView.Adapter chatlistAdapter;
		private RecyclerView.LayoutManager chatlistLayoutManager;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			ChatEntry[] testEnries = createTestData();

			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.Main);

			var toolbar = FindViewById<Android.Widget.Toolbar>(Resource.Id.Toolbar);
			SetActionBar(toolbar);

			chatlistRecyclerView = (RecyclerView)FindViewById(Resource.Id.ChatList);
			chatlistRecyclerView.HasFixedSize = true;

			chatlistLayoutManager = new LinearLayoutManager(this);
			chatlistRecyclerView.SetLayoutManager(chatlistLayoutManager);

			chatlistAdapter = new ChatlistAdapter(testEnries);
			chatlistRecyclerView.SetAdapter(chatlistAdapter);
		}

		public override bool OnCreateOptionsMenu(IMenu menu)
		{
			MenuInflater.Inflate(Resource.Menu.main_menu, menu);
			return base.OnCreateOptionsMenu(menu);
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			Intent intent;
			switch (item.ItemId)
			{
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

		public ChatEntry[] createTestData()
		{
			string[] testData = { "Preview of a long message that goes beyond the lines",
				"Preview of a really really long message that really goes beyond the lines and is sure to overflow",
				"text here", "more longish text here", "Love", "Air", "Shoes", "Hair", "Perfume",
				"Obfuscation", "Clock", "Game", "Scroll", "Lion", "Chrome", "Tresure", "Charm" };

			string[][] testNames = new string[5][];
			testNames[0] = new string[] { "Name1Test" };
			testNames[1] = new string[] { "Name2Test1", "Name2Test2" };
			testNames[2] = new string[] { "Name3Test1", "Name3Test2", "Name3Test3" };
			testNames[3] = new string[] { "Name4Test1", "Name4Test2", "Name4Test3", "Name4Test4" };
			testNames[4] = new string[] { "Name5Test1", "Name5Test2", "Name5Test3", "Name5Test4", "Name5Test5" };

			ChatEntry[] entries = new ChatEntry[testData.Length];

			for (var i = 0; i < entries.Length; i++)
			{
				string[] names;
				if (i < testNames.Length)
				{
					names = testNames[i];
				}
				else
				{
					names = new string[] { "default" };
				}

				entries[i] = new ChatEntry(names, testData[i]);
			}

			return entries;
		}
	}

}

