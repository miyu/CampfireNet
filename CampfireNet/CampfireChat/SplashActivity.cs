
using Android.App;
using Android.Content;
using System.Threading.Tasks;
using CampfireNet;
using CampfireNet.Identities;
using CampfireNet.Utilities;

namespace CampfireChat {
   [Activity(Theme = "@style/SplashTheme", MainLauncher = true, NoHistory = true, Icon = "@drawable/icon")]
   public class SplashActivity : Activity {

      protected override void OnResume() {
         base.OnResume();
         Task.Run(() => Startup());
      }

      public void Startup() {
         var nativeBluetoothAdapter = Helper.EnableBluetooth(this);
         var androidBluetoothAdapter = new AndroidBluetoothAdapterFactory().Create(this, ApplicationContext, nativeBluetoothAdapter);

         var prefs = Application.Context.GetSharedPreferences("CampfireChat", FileCreationMode.Private);
         using (var editor = prefs.Edit()) {
            editor.Clear();
            editor.Commit();
         }

         var userName = prefs.GetString("Name", null);
         if (userName == null) {
            Globals.CampfireNetClient = CampfireNetClientBuilder.CreateNew()
                                                                .WithBluetoothAdapter(androidBluetoothAdapter)
                                                                .Build();
            Helper.UpdateIdentity(prefs, Globals.CampfireNetClient.Identity);
         } else {
            var rsa = Helper.InitRSA(prefs);
            var identity = new Identity(new IdentityManager(), rsa, userName);
            var trustChain = prefs.GetString("TC", null);
            if (trustChain != null) {
               identity.AddTrustChain(Helper.HexStringToByteArray(trustChain));
            }
            Globals.CampfireNetClient = CampfireNetClientBuilder.CreateNew()
                                                                .WithBluetoothAdapter(androidBluetoothAdapter)
                                                                .WithIdentity(identity).Build();
         }

         if (Globals.CampfireChatClient == null) {
            Globals.CampfireChatClient = CampfireChatClientFactory.Create(Globals.CampfireNetClient);
            Globals.CampfireNetClient.RunAsync().Forget();
         }

         StartActivity(new Intent(Application.Context, typeof(MainActivity)));
      }

      public override void OnBackPressed() { }
   }
}