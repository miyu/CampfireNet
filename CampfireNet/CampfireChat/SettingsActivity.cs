
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using CampfireNet.Identities;
using System.IO;
using Android.Views;
using System;

namespace CampfireChat {
   [Activity(Label = "Settings", ParentActivity = typeof(MainActivity))]
   public class SettingsActivity : Activity {
      public const int CHOOSE_TRUST_CHAIN_FILE = 0;
      public const int CHOOSE_IDENTITY_FILE = 1;

      public const string TRUST_CHAIN_FORMAT_STRING = "trust_chain_{0}.tc";
      public const string IDENTITY_FORMAT_STRING = "identity_{0}.id";

      private Toolbar toolbar;
      private LinearLayout becomeRoot;
      private LinearLayout loadTrustChain;
      private LinearLayout inviteFriend;
      private LinearLayout sendId;
      private LinearLayout clearAllKnown;

      private ISharedPreferences prefs;
      private Identity identity;
      private string privatePath;

      protected override void OnCreate(Bundle savedInstanceState) {
         base.OnCreate(savedInstanceState);
         SetContentView(Resource.Layout.Settings);

         toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
         SetActionBar(toolbar);
         ActionBar.SetDisplayHomeAsUpEnabled(true);

         becomeRoot = FindViewById<LinearLayout>(Resource.Id.BecomeRoot);
         loadTrustChain = FindViewById<LinearLayout>(Resource.Id.LoadChain);
         inviteFriend = FindViewById<LinearLayout>(Resource.Id.Invite);
         sendId = FindViewById<LinearLayout>(Resource.Id.SendId);
         clearAllKnown = FindViewById<LinearLayout>(Resource.Id.ClearAll);


         prefs = Application.GetSharedPreferences("CampfireChat", FileCreationMode.Private);

         identity = Globals.CampfireNetClient.Identity;
         privatePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);

         becomeRoot.Click += (sender, e) => {
            var fileName = string.Format(TRUST_CHAIN_FORMAT_STRING,
                                         IdentityManager.GetIdentityString(identity.PublicIdentityHash));
            var fullPath = Path.Combine(privatePath, fileName);

            if (!File.Exists(fullPath) && identity.TrustChain == null) {
               identity.GenerateRootChain();

               byte[] trustChainBytes = TrustChainUtil.SerializeTrustChain(identity.TrustChain);

               using (var stream = new FileStream(fullPath, FileMode.Create))
               using (var writer = new BinaryWriter(stream)) {
                  writer.Write(trustChainBytes);
               }
               Toast.MakeText(ApplicationContext, "Root node successfully created", ToastLength.Short).Show();
            } else {
               Toast.MakeText(ApplicationContext, "Trust chain already exists", ToastLength.Short).Show();
            }
         };

         loadTrustChain.Click += (sender, e) => {
            Intent chooseFile = new Intent(Intent.ActionGetContent);
            chooseFile.AddCategory(Intent.CategoryOpenable);
            chooseFile.SetType("text/plain");
            chooseFile = Intent.CreateChooser(chooseFile, "Choose a trust chain file to load");

            Toast.MakeText(ApplicationContext, "Choose a trust chain file to load (*.tc)", ToastLength.Short).Show();

            StartActivityForResult(chooseFile, CHOOSE_TRUST_CHAIN_FILE);
         };

         inviteFriend.Click += (sender, e) => {
            Intent chooseFile = new Intent(Intent.ActionGetContent);
            chooseFile.AddCategory(Intent.CategoryOpenable);
            chooseFile.SetType("text/plain");
            chooseFile = Intent.CreateChooser(chooseFile, "Choose an identity to load");

            Toast.MakeText(ApplicationContext, "Choose an identity to load (*.id)", ToastLength.Short).Show();

            StartActivityForResult(chooseFile, CHOOSE_IDENTITY_FILE);
         };

         sendId.Click += (sender, e) => {
            var idBytes = identity.PublicIdentity;

            var idFileName = string.Format(IDENTITY_FORMAT_STRING, IdentityManager.GetIdentityString(idBytes));
            var idFullPath = Path.Combine(privatePath, idFileName);
            File.WriteAllBytes(idFullPath, idBytes);

            startEmail("Identification", idFullPath);
         };

         clearAllKnown.Click += (sender, e) => {
            foreach (var file in Directory.GetFiles(privatePath)) {
               if (file.EndsWith(".tc", StringComparison.Ordinal) || file.EndsWith(".id", StringComparison.Ordinal)) {
                  File.Delete(file);
                  Console.WriteLine($"deleting {file}");
               }
            }
         };
      }

      protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
         base.OnActivityResult(requestCode, resultCode, data);

         if (resultCode == Result.Ok && data != null) {
            if (requestCode == CHOOSE_TRUST_CHAIN_FILE) {
               var fullPath = data.Data.Path;

               byte[] trustChainBytes = File.ReadAllBytes(fullPath);

               if (trustChainBytes.Length % TrustChainNode.NODE_BLOCK_SIZE != 0) {
                  Toast.MakeText(this, "Trust chain is invalid length", ToastLength.Short).Show();
               } else if (!identity.ValidateAndAdd(trustChainBytes)) {
                  Toast.MakeText(this, "Could not validate the trust chain", ToastLength.Short).Show();
               } else {
                  Toast.MakeText(this, "Could not validate the trust chain", ToastLength.Short).Show();
               }
            } else if (requestCode == CHOOSE_IDENTITY_FILE) {
               var fullPath = data.Data.Path;
               Console.WriteLine($"got path {fullPath}");

               byte[] newIdentity = File.ReadAllBytes(fullPath);

               if (newIdentity.Length != CryptoUtil.ASYM_KEY_SIZE_BYTES) {
                  Toast.MakeText(this, "Identity is invalid length", ToastLength.Short).Show();
               } else {
                  // TODO add changeable permissions
                  var newChainBytes = identity.GenerateNewChain(newIdentity, identity.PermissionsHeld,
                                                                identity.PermissionsGrantable, "Child");

                  var trustChainFileName = string.Format(TRUST_CHAIN_FORMAT_STRING,
                                               IdentityManager.GetIdentityString(newIdentity));
                  var trustChainFullPath = Path.Combine(privatePath, trustChainFileName);
                  File.WriteAllBytes(trustChainFullPath, newChainBytes);

                  startEmail("Trust chain", trustChainFullPath);
               }
            }
         }
      }

      private void startEmail(string subject, string filePath) {
         var index = filePath.LastIndexOf("/", StringComparison.Ordinal);
         var fileName = filePath.Substring(index + 1, filePath.Length - index - 1);

         var tmpFolder = Android.OS.Environment.ExternalStorageDirectory.ToString();
         var tmpPath = Path.Combine(tmpFolder, fileName);
         File.Copy(filePath, tmpPath, true);

         var file = new Java.IO.File(tmpPath);
         file.SetReadable(true, false);
         var uri = Android.Net.Uri.FromFile(file);

         Intent email = new Intent(Intent.ActionSend);
         email.SetType("message/rfc822");
         email.PutExtra(Intent.ExtraSubject, subject);
         email.PutExtra(Intent.ExtraStream, uri);
         email.AddFlags(ActivityFlags.GrantReadUriPermission);

         try {
            StartActivityForResult(Intent.CreateChooser(email, "Send mail with: "), 0);
         } catch (ActivityNotFoundException) {
            Toast.MakeText(ApplicationContext, "There are no email clients installed.", ToastLength.Short).Show();
         }
      }

      public override bool OnOptionsItemSelected(IMenuItem item) {
         if (item.ItemId == Android.Resource.Id.Home) {
            Finish();
         }

         return base.OnOptionsItemSelected(item);
      }
   }
}