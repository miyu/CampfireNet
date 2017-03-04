
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace CampfireChat
{
	[Activity(Label = "New Message", ParentActivity = typeof(MainActivity))]
	public class NewChatActivity : Activity
	{
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.Main);
			var toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
			SetActionBar(toolbar);
			this.ActionBar.SetDisplayHomeAsUpEnabled(true);
		}

		public override bool OnCreateOptionsMenu(IMenu menu)
		{
			MenuInflater.Inflate(Resource.Menu.new_chat_menu, menu);
			return base.OnCreateOptionsMenu(menu);
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			return base.OnOptionsItemSelected(item);
		}
	}
}