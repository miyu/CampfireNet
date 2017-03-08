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
      private bool stared;
      private string isStar;
      protected override void OnCreate(Bundle savedInstanceState) {
         base.OnCreate(savedInstanceState);
         SetContentView(Resource.Layout.Person);


         var toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
         prefs = Application.Context.GetSharedPreferences("CampfireChat", FileCreationMode.Private);
         var userId = Helper.ByteArrayToString(Intent.GetByteArrayExtra("UserId"));
         var friendlyName = prefs.GetString(userId, null);
         if(friendlyName != null) {
            toolbar.Title = friendlyName;
         } else {
            toolbar.Title = userId;
         }
         isStar = $"star_{userId}";
         stared = prefs.GetBoolean(isStar, false);
         SetActionBar(toolbar);
         ActionBar.SetDisplayHomeAsUpEnabled(true);

         var tagName = FindViewById<LinearLayout>(Resource.Id.TagName);
         tagName.Click += (sender, e) => {
            ShowDialog(userId);
         };
      }

      public override bool OnPrepareOptionsMenu(IMenu menu) {
         menu.Clear();
         if (stared)
            menu.FindItem(Resource.Id.Star).SetIcon(Resource.Drawable.ic_star_white_48dp);
         else {
            menu.FindItem(Resource.Id.Star).SetIcon(Resource.Drawable.ic_star_border_white_48dp);
         }
         return base.OnPrepareOptionsMenu(menu);
      }

      public override bool OnOptionsItemSelected(IMenuItem item) {
         if (item.ItemId == Resource.Id.Star) {
            var toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
            if (stared) {
               item.SetIcon(Resource.Drawable.ic_star_border_white_48dp);
               Helper.UpdateBool(prefs, isStar, false);
               stared = false;
            } else {
               item.SetIcon(Resource.Drawable.ic_star_white_48dp);
               Helper.UpdateBool(prefs, isStar, true);
               stared = true;
            }
         }
         return base.OnOptionsItemSelected(item);
      }

      public override bool OnCreateOptionsMenu(IMenu menu) {
         MenuInflater.Inflate(Resource.Menu.person_menu, menu);
         return base.OnCreateOptionsMenu(menu);
      }

      private void ShowDialog(string id) {
         var transaction = FragmentManager.BeginTransaction();
         var dialog = new TagDialog();
         dialog.Arguments.PutString("UserHash", id);
         dialog.Show(transaction, "TagUser");
      }
   }
}