
using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Widget;
using AndroidTest.Droid;
using CampfireNet;
using CampfireNet.Identities;
using System;
using System.IO;
using System.Security.Cryptography;

namespace CampfireChat {
   [Activity(Label = "Settings", ParentActivity = typeof(MainActivity))]
   public class SettingsActivity : Activity {
      const int PICKFILE_RESULT_CODE = 1;
      private ISharedPreferences prefs;
      protected override void OnCreate(Bundle savedInstanceState) {
         base.OnCreate(savedInstanceState);
         SetContentView(Resource.Layout.Settings);

         var toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
         SetActionBar(toolbar);
         ActionBar.SetDisplayHomeAsUpEnabled(true);

         prefs = Application.GetSharedPreferences("CampfireChat", FileCreationMode.Private);

         var identity = Globals.CampfireNetClient.Identity;
         var generateRoot = FindViewById<LinearLayout>(Resource.Id.BecomeRoot);

         var filename = $"trust_chain_{IdentityManager.GetIdentityString(identity.PublicIdentityHash)}.tc";

         generateRoot.Click += (sender, e) => {
            var dir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            var path = Path.Combine(dir, filename);
            if (!File.Exists(path) && identity.TrustChain == null) {
               identity.GenerateRootChain();
               byte[] trustChain = TrustChainUtil.SerializeTrustChain(identity.TrustChain);
               Helper.UpdateTrustChain(prefs, trustChain);
               Helper.WriteToFile(path, trustChain);
               Toast.MakeText(ApplicationContext, "Root chain generated.", ToastLength.Short).Show();
            } else {
               Toast.MakeText(ApplicationContext, "Trust chain already exists.", ToastLength.Short).Show();
            }
         };

         var loadChain = FindViewById<LinearLayout>(Resource.Id.LoadChain);
         loadChain.Click += (sender, e) => {

            Intent chooseFile = new Intent(Intent.ActionGetContent);
            chooseFile.AddCategory(Intent.CategoryOpenable);
            chooseFile.SetType("file/*");
            chooseFile = Intent.CreateChooser(chooseFile, "Choose a file");
            
            StartActivityForResult(chooseFile, PICKFILE_RESULT_CODE);

            var uri = (Android.Net.Uri)Intent.GetParcelableExtra("ReceivedChain");
            var path = uri.Path;

            if (!path.Contains(filename)) {
               Toast.MakeText(ApplicationContext, "Trust chain file does not match", ToastLength.Short).Show();
               return;
            }

            byte[] trustChain = File.ReadAllBytes(path);

            try {
               identity.AddTrustChain(trustChain);
               var dir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
               var newpath = Path.Combine(dir, filename);
               Helper.UpdateTrustChain(prefs, trustChain);
               Helper.WriteToFile(newpath, trustChain);
               Toast.MakeText(ApplicationContext, "Successfully loaded trust chain.", ToastLength.Short).Show();
            } catch (BadTrustChainException) {
               Toast.MakeText(ApplicationContext, "Invalid trust chain found.", ToastLength.Short).Show();
            }
         };

         var inviteFriend = FindViewById<LinearLayout>(Resource.Id.Invite);
         inviteFriend.Click += (sender, e) => {
            Intent chooseFile = new Intent(Intent.ActionGetContent);

         };
      }

      protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
         base.OnActivityResult(requestCode, resultCode, data);
         if (resultCode == Result.Ok && data != null) {
            Android.Net.Uri uri = data.Data;
            data.PutExtra("ReceivedChain", uri);
         }
      }
   }
}