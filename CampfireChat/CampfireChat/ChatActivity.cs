
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;

namespace CampfireChat
{
	[Activity(Label = "Chat", ParentActivity = typeof(MainActivity))]
	public class ChatActivity : Activity
	{
		private RecyclerView chatRecyclerView;
		private RecyclerView.Adapter chatAdapter;
		private RecyclerView.LayoutManager chatLayoutManager;

		private MessageEntry[] testMessages;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			testMessages = new MessageEntry[10];
			testMessages[0] = new MessageEntry("Name 1", "This is a test message 1");
			testMessages[1] = new MessageEntry("Name 2", "This is a test message 2");
			testMessages[2] = new MessageEntry("Name 3", "This is a test message 3");
			testMessages[3] = new MessageEntry("Name 2", "This is a test message 4 really long message here one that is sure to overflow. How about some more text here and see if we can get it to three lines - or even more! How far can we go?");
			testMessages[4] = new MessageEntry("Name 3", "This is a test message 5");
			testMessages[5] = new MessageEntry("Name 1", "These are yet more messages designed to be long and take up space.");
			testMessages[6] = new MessageEntry("Name 2", "These are yet more messages designed to be long and take up space.");
			testMessages[7] = new MessageEntry("Name 3", "These are yet more messages designed to be long and take up space.");
			testMessages[8] = new MessageEntry("Name 1", "These are yet more messages designed to be long and take up space.");
			testMessages[9] = new MessageEntry("Name 2", "These are yet more messages designed to be long and take up space.");


			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.Chat);

			var toolbar = FindViewById<Android.Widget.Toolbar>(Resource.Id.Toolbar);
			SetActionBar(toolbar);
			ActionBar.SetDisplayHomeAsUpEnabled(true);

			chatRecyclerView = (RecyclerView)FindViewById(Resource.Id.Messages);
			chatRecyclerView.HasFixedSize = true;

			chatLayoutManager = new LinearLayoutManager(this);
			chatRecyclerView.SetLayoutManager(chatLayoutManager);

			chatAdapter = new ChatAdapter(testMessages);
			((ChatAdapter)chatAdapter).ItemClick += OnItemClick;
			chatRecyclerView.SetAdapter(chatAdapter);

			Title = Intent.GetStringExtra("title") ?? "Chat";
		}

		private void OnItemClick(object sender, byte[] id)
		{
			Toast.MakeText(this, $"got id {Encoding.UTF8.GetString(id)}", ToastLength.Short).Show();

			//Intent intent = new Intent(this, typeof(ChatActivity));
			//intent.PutExtra("id", id);
			//StartActivity(intent);
		}

		public override bool OnCreateOptionsMenu(IMenu menu)
		{
			MenuInflater.Inflate(Resource.Menu.chat_menu, menu);
			return base.OnCreateOptionsMenu(menu);
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			return base.OnOptionsItemSelected(item);
		}
	}
}
