
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using AndroidTest.Droid;
using CampfireNet;
using CampfireNet.Identities;
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

         var prefs = Application.GetSharedPreferences("CampfireChat", FileCreationMode.Private);

         var identity = Globals.CampfireNetClient.Identity;
         var generateRoot = FindViewById<LinearLayout>(Resource.Id.BecomeRoot);

         generateRoot.Click += (sender, e) => {
            var path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            path = Path.Combine(path, $"trust_chain_{IdentityManager.GetIdentityString(identity.PublicIdentityHash)}.tc");
            if (!File.Exists(path) && identity.TrustChain == null) {
               identity.GenerateRootChain();
               byte[] trustChain = TrustChainUtil.SerializeTrustChain(identity.TrustChain);
               Helper.UpdateTrustChain(prefs, trustChain);
               using (var stream = new FileStream(path, FileMode.Create))
               using (var writer = new BinaryWriter(stream)) {
                  writer.Write(trustChain);
               }
            } else {
               Toast.MakeText(ApplicationContext, "Trust chain already exists.", ToastLength.Short).Show();
            }
         };

         var loadChain = FindViewById<LinearLayout>(Resource.Id.LoadChain);
         loadChain.Click += (sender, e) => {
            Toast.MakeText(this, "Action selected: load chain", ToastLength.Short).Show();
            Intent chooseFile = new Intent(Intent.ActionGetContent);
            chooseFile.AddCategory(Intent.CategoryOpenable);
            chooseFile.SetType("text/plain");
            chooseFile = Intent.CreateChooser(chooseFile, "Choose a file");
            StartActivityForResult(chooseFile, PICKFILE_RESULT_CODE);
         };

         var inviteFriend = FindViewById<LinearLayout>(Resource.Id.Invite);
         inviteFriend.Click += (sender, e) => {

         };
      }
   }
}