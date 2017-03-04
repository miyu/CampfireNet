
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace CampfireChat
{
	[Activity(Label = "Chat", ParentActivity = typeof(MainActivity))]
	public class ChatActivity : Activity
	{
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.Chat);
			var toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
			SetActionBar(toolbar);
			this.ActionBar.SetDisplayHomeAsUpEnabled(true);

			this.Title = Intent.GetStringExtra("title") ?? "Chat";
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
