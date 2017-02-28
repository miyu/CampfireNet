using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Widget;
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

      internal const string PRIVATE_KEY_FILE = "trust_chain_identity.bin";
      internal const string TRUST_CHAIN_FILE_REGEX = @"trust_chain_[0-9A-F]{64}.bin";

      private ArrayAdapter<string> logAdapter;
      private ListView log;
      private Handler uiDispatchHandler;

      private Button generateButton;
      private Button sendButton;
      private Button loadButton;
      private EditText inputText;
      private Button sendTextButton;

      // TODO remove this
      private Button clearButton;

      protected override void OnCreate(Bundle savedInstanceState) {
         base.OnCreate(savedInstanceState);

         InitializeComponents();
      }

      private void InitializeComponents() {
         SetContentView(Resource.Layout.Main);

         generateButton = FindViewById<Button>(Resource.Id.GenerateRoot);
         sendButton = FindViewById<Button>(Resource.Id.Send);
         loadButton = FindViewById<Button>(Resource.Id.Load);
         log = FindViewById<ListView>(Resource.Id.Log);
         inputText = FindViewById<EditText>(Resource.Id.TextInput);
         sendTextButton = FindViewById<Button>(Resource.Id.SendTextButton);

         clearButton = FindViewById<Button>(Resource.Id.ClearButton);

         logAdapter = new ArrayAdapter<string>(this, Resource.Layout.Message);
         log.Adapter = logAdapter;
         log.ItemsCanFocus = false;
         log.Focusable = false;

         uiDispatchHandler = new LambdaHandler(msg => {
            if (msg.What == LOG_MESSAGE) {
               logAdapter.Add((string)msg.Obj);
               log.SmoothScrollToPosition(logAdapter.Count - 1);
            }
         });
      }

      public void Setup() {
         var nativeBluetoothAdapter = BluetoothAdapter.DefaultAdapter;
         if (!nativeBluetoothAdapter.IsEnabled) {
            System.Console.WriteLine("Enabling bluetooth");
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

         var campfireNetBluetoothAdapter = new AndroidBluetoothAdapter(ApplicationContext, nativeBluetoothAdapter,
                                                                       bluetoothDiscoveryFacade, inboundBluetoothSocketTable);

         var identity = EstablishIdentity();
         LoadTrustChains(identity);

         var broadcastMessageSerializer = new BroadcastMessageSerializer();
         var objectStore = new InMemoryCampfireNetObjectStore();
         var clientMerkleTreeFactory = new ClientMerkleTreeFactory(broadcastMessageSerializer, objectStore);
         var client = new CampfireNetClient(identity, campfireNetBluetoothAdapter, broadcastMessageSerializer,
                                            clientMerkleTreeFactory);

         var sync = new object();
         client.BroadcastReceived += e => {
            lock (sync) {
               var s = Encoding.UTF8.GetString(e.Message.DecryptedPayload, 0, e.Message.DecryptedPayload.Length);
               uiDispatchHandler.ObtainMessage(LOG_MESSAGE, "RECV: " + s).SendToTarget();
            }
         };


         generateButton.Click += (s, e) => {
            var path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            path = Path.Combine(path, $"trust_chain_{IdentityManager.GetIdentityString(identity.PublicIdentityHash)}.bin");

            if (!File.Exists(path) && identity.TrustChain == null) {
               identity.GenerateRootChain();

               using (var stream = new FileStream(path, FileMode.Create))
               using (var writer = new BinaryWriter(stream)) {
                  writer.Write(TrustChainUtil.SerializeTrustChain(identity.TrustChain));
               }
            } else {
               Toast.MakeText(ApplicationContext, "Trust chain already exists.", ToastLength.Short).Show();
            }
         };

         sendButton.Click += (s, e) => {
            var filename = $"trust_chain_{IdentityManager.GetIdentityString(identity.PublicIdentityHash)}.bin";
            var path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            path = Path.Combine(path, filename);

            // TODO fix
            if (!File.Exists(path)) {
               Toast.MakeText(ApplicationContext, "Cannot find file in Download folder.", ToastLength.Short).Show();
               return;
            }

            var tmpFolder = Environment.ExternalStorageDirectory.ToString();
            var tmpFile = Path.Combine(tmpFolder, filename);
            File.Copy(path, tmpFile, true);

            var file = new Java.IO.File(tmpFile);
            var uri = Android.Net.Uri.FromFile(file);

            Intent email = new Intent(Intent.ActionSend);
            email.SetType("message/rfc822");
            email.PutExtra(Intent.ExtraSubject, "Trust chain addition");
            email.PutExtra(Intent.ExtraStream, uri);
            email.AddFlags(ActivityFlags.GrantReadUriPermission);

            try {
               StartActivityForResult(Intent.CreateChooser(email, "Send mail with: "), 0);
            } catch (ActivityNotFoundException) {
               Toast.MakeText(ApplicationContext, "There are no email clients installed.", ToastLength.Short).Show();
            }
         };

         loadButton.Click += (s, e) => {
            var path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            path = Path.Combine(path, $"trust_chain_{IdentityManager.GetIdentityString(identity.PublicIdentityHash)}.bin");

            if (!File.Exists(path)) {
               Toast.MakeText(ApplicationContext, "Cannot find own trust chain file in Download folder.", ToastLength.Short).Show();
               return;
            }

            byte[] data = File.ReadAllBytes(path);

            try {
               identity.AddTrustChain(data);
               Toast.MakeText(ApplicationContext, "Successfully loaded trust chain.", ToastLength.Short).Show();
            } catch (BadTrustChainException) {
               Toast.MakeText(ApplicationContext, "Invalid trust chain found.", ToastLength.Short).Show();
            }
         };

         clearButton.Click += (s, e) => {
            var privateFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            var files = Directory.GetFiles(privateFolder);

            foreach (var file in files) {
               File.Delete(file);
            }

            //var path = Path.Combine(privateFolder, PRIVATE_KEY_FILE);
            //identity.SaveKey(path);
         };


         sendTextButton.Click += (s, e) => {
            var text = inputText.Text;
            client.BroadcastAsync(Encoding.UTF8.GetBytes(text)).Forget();
            inputText.Text = "";
         };

         client.RunAsync().Forget();
      }

      public Identity EstablishIdentity() {
         Identity identity = null;

         var privateFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
         var savedIdentityPath = Path.Combine(privateFolder, PRIVATE_KEY_FILE);

         var hasIdentity = File.Exists(savedIdentityPath);

         if (hasIdentity) {
            try {
               byte[] key = File.ReadAllBytes(savedIdentityPath);
               RSAParameters parameters = CryptoUtil.DeserializeKey(key);
               identity = new Identity(new IdentityManager(), parameters, "Name");
            } catch (System.Exception) { }
         }

         if (identity == null) {
            identity = new Identity(new IdentityManager(), "Name");
            identity.SaveKey(savedIdentityPath);
         }

         return identity;
      }

      public void LoadTrustChains(Identity identity) {
         var privateFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
         var trustChainPath = Path.Combine(privateFolder, $"trust_chain_{IdentityManager.GetIdentityString(identity.PublicIdentityHash)}.bin");

         bool hasTrustChain = File.Exists(trustChainPath);

         if (hasTrustChain) {
            identity.AddTrustChain(File.ReadAllBytes(trustChainPath));

            var files = Directory.GetFiles(privateFolder);

            int numValidChains = 0;
            Regex fileRegex = new Regex(TRUST_CHAIN_FILE_REGEX);
            foreach (var file in files) {
               var match = fileRegex.Match(file);
               if (match.Success) {
                  byte[] trustChain = null;

                  try {
                     trustChain = File.ReadAllBytes(file);
                     if (identity.ValidateAndAdd(trustChain)) {
                        numValidChains++;
                     }
                  } catch (CryptographicException) { }

                  if (trustChain == null) {
                     File.Delete(file);
                  }
               }
            }

            Toast.MakeText(ApplicationContext, $"Loaded {numValidChains} saved identities", ToastLength.Short).Show();
         } else {
            Toast.MakeText(ApplicationContext, $"No trust chain found", ToastLength.Short).Show();
         }
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
            System.Console.WriteLine("BT Setup failed!");
         }

         Setup();
      }


      //      protected override void OnDestroy() { base.OnDestroy(); }
      //      protected override void OnRestart() {
      //      protected override void OnPause() => 
      //      protected override void OnResume() {
   }
}
