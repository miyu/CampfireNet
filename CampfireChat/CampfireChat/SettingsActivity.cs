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
        const int PICKFILE_RESULT_CODE = 1;
        protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.Settings);
			var toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
			SetActionBar(toolbar);
			this.ActionBar.SetDisplayHomeAsUpEnabled(true);
            var generateRoot = (LinearLayout)FindViewById<LinearLayout>(Resource.Id.BecomeRoot);
            generateRoot.Click += (sender, e) => {
                Toast.MakeText(this, "Action selected: generate root",
                    ToastLength.Short).Show();
            };
            var loadChain = (LinearLayout)FindViewById<LinearLayout>(Resource.Id.LoadChain);
            loadChain.Click += (sender, e) => {
                Toast.MakeText(this, "Action selected: load chain",
                    ToastLength.Short).Show();
                Intent chooseFile = new Intent(Intent.ActionGetContent);
                chooseFile.AddCategory(Intent.CategoryOpenable);
                chooseFile.SetType("text/plain");
                chooseFile = Intent.CreateChooser(chooseFile, "Choose a file");
                StartActivityForResult(chooseFile, PICKFILE_RESULT_CODE);
            };
            var inviteFriend = (LinearLayout)FindViewById<LinearLayout>(Resource.Id.Invite);
            inviteFriend.Click += (sender, e) =>
            {
                Toast.MakeText(this, "Action selected: invite friend",
                    ToastLength.Short).Show();
            };
        }
	}
}