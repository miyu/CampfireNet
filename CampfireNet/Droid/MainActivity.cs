using System;
using System.Text;
using System.Threading;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Widget;
using Android.OS;
using CampfireNet;
using CampfireNet.Identities;
using CampfireNet.IO;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Merkle;

namespace AndroidTest.Droid {
   [Activity(Label = "AndroidTest", MainLauncher = true, Icon = "@mipmap/icon")]
   public class MainActivity : Activity {
      private const int REQUEST_ENABLE_BT = 1;
      internal const int LOG_MESSAGE = 123;

      private ArrayAdapter<string> logAdapter;
      private ListView log;
      private Handler uiDispatchHandler;
      private EditText inputText;
      private Button sendTextButton;

      protected override void OnCreate(Bundle savedInstanceState) {
         base.OnCreate(savedInstanceState);

         InitializeComponents();
      }

      private void InitializeComponents() {
         SetContentView(Resource.Layout.Main);

         var discoverButton = FindViewById<Button>(Resource.Id.Discover);
         var deviceList = FindViewById<ListView>(Resource.Id.DeviceList);
         var beServer = FindViewById<Button>(Resource.Id.Server);
         var beClient = FindViewById<Button>(Resource.Id.Client);
         log = FindViewById<ListView>(Resource.Id.Log);
         inputText = FindViewById<EditText>(Resource.Id.TextInput);
         sendTextButton = FindViewById<Button>(Resource.Id.SendTextButton);

         var deviceListAdapter = new ArrayAdapter<string>(this, Resource.Layout.Message);
         deviceList.Adapter = deviceListAdapter;

         logAdapter = new ArrayAdapter<string>(this, Resource.Layout.Message);
         log.Adapter = logAdapter;
         log.ItemsCanFocus = false;
         log.Focusable = false;

         uiDispatchHandler =new LambdaHandler(msg => {
            if (msg.What == LOG_MESSAGE) {
               logAdapter.Add((string)msg.Obj);
               log.SmoothScrollToPosition(logAdapter.Count - 1);
            }
         });
      }

      public void Setup() {
         var nativeBluetoothAdapter = BluetoothAdapter.DefaultAdapter;
         if (!nativeBluetoothAdapter.IsEnabled) {
            Console.WriteLine("Enabling bluetooth");
            Intent enableBtIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
            StartActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
            return;
         }
         
         var automaticPairingService = new AutomaticPairingService(ApplicationContext);
         var bluetoothFacade = new AndroidBluetoothFacade(ApplicationContext);
         bluetoothFacade.EnableBluetoothFromActivity(this);
         var bluetoothDiscoveryFacade = new BluetoothDiscoveryFacade(ApplicationContext);
         var inboundBluetoothSocketTable = new InboundBluetoothSocketTable();
         var bluetoothServer = BluetoothServer.Create(nativeBluetoothAdapter, inboundBluetoothSocketTable);
         bluetoothServer.Start();
         var campfireNetBluetoothAdapter = new AndroidBluetoothAdapter(ApplicationContext, nativeBluetoothAdapter, bluetoothDiscoveryFacade, inboundBluetoothSocketTable);

         var identity = new Identity(new IdentityManager(), "IdentityName");
         var broadcastMessageSerializer = new BroadcastMessageSerializer();
         var objectStore = new InMemoryCampfireNetObjectStore();
         var clientMerkleTreeFactory = new ClientMerkleTreeFactory(broadcastMessageSerializer, objectStore);
         var client = new CampfireNetClient(identity, campfireNetBluetoothAdapter, broadcastMessageSerializer, clientMerkleTreeFactory);

         var sync = new object();
         client.BroadcastReceived += e => {
            lock (sync) {
               var s = Encoding.UTF8.GetString(e.Message.DecryptedPayload, 0, e.Message.DecryptedPayload.Length);
               uiDispatchHandler.ObtainMessage(LOG_MESSAGE, "RECV: " + s).SendToTarget();
            }
         };

         sendTextButton.Click += (s, e) => {
            var text = inputText.Text;
            client.BroadcastAsync(Encoding.UTF8.GetBytes(text)).Forget();
         };

         client.RunAsync().Forget();
      }

      public void Teardown() {

      }

      protected override void OnStart() {
         base.OnStart();
         Setup();
      }

      protected override void OnStop() {
         base.OnStop();
         Teardown();
      }

      protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
         if (requestCode != REQUEST_ENABLE_BT)
            return;

         if (resultCode != Result.Ok) {
            Console.WriteLine("BT Setup failed!");
         }

         Setup();
      }

      //      protected override void OnDestroy() { base.OnDestroy(); }
      //      protected override void OnRestart() {
      //      protected override void OnPause() => 
      //      protected override void OnResume() {
   }
}
