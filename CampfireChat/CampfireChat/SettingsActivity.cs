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

namespace CampfireChat
{
	[Activity(Label = "Settings", ParentActivity = typeof(MainActivity))]
	public class SettingsActivity : Activity
	{
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.Settings);
			var toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
			SetActionBar(toolbar);
			this.ActionBar.SetDisplayHomeAsUpEnabled(true);
		}

		public override bool OnCreateOptionsMenu(IMenu menu)
		{
			MenuInflater.Inflate(Resource.Menu.settings_menu, menu);
			return base.OnCreateOptionsMenu(menu);
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			return base.OnOptionsItemSelected(item);
		}
	}
}