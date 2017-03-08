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

namespace CampfireChat {
   [Activity(Label = "PersonActivity")]
   public class PersonActivity : Activity {
      private ISharedPreferences prefs;
      protected override void OnCreate(Bundle savedInstanceState) {
         base.OnCreate(savedInstanceState);
         SetContentView(Resource.Layout.Person);


         var toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
         prefs = Application.Context.GetSharedPreferences("CampfireChat", FileCreationMode.Private);
         var userId = Intent.GetByteArrayExtra("UserId");
         var friendlyName = prefs.GetString(Helper.ByteArrayToString(userId), null);
         
         SetActionBar(toolbar);
         ActionBar.SetDisplayHomeAsUpEnabled(true);

         var tagName = FindViewById<LinearLayout>(Resource.Id.TagName);
         tagName.Click += (sender, e) => {
            ShowDialog();
         };
      }

      public override bool OnOptionsItemSelected(IMenuItem item) {
         if (item.ItemId == Resource.Id.Star) {
            var toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);

            if (false) {
               item.SetIcon(Resource.Drawable.ic_star_white_48dp);
            } else {
               item.SetIcon(Resource.Drawable.ic_star_border_white_48dp);
            }
         }
         return base.OnOptionsItemSelected(item);
      }

      public override bool OnCreateOptionsMenu(IMenu menu) {
         MenuInflater.Inflate(Resource.Menu.person_menu, menu);
         return base.OnCreateOptionsMenu(menu);
      }

      private void ShowDialog() {
         var transaction = FragmentManager.BeginTransaction();
         var dialog = new UsernameDialog();
         dialog.Show(transaction, "TagUser");
      }
   }
}