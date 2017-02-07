using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Widget;
using Android.OS;
using System.Text;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace AndroidTest.Droid
{
	[Activity(Label = "AndroidTest", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{
		public const bool debug = true;
		public const int INFO = 0;
		public const int WARNING = 1;
		public const int ERROR = 2;
		public const int CRITICAL = 3;
		public const string version = "1.0.26";


		public const int LOG_MESSAGE = 1;

		public const int REQUEST_ENABLE_BT = 1;

		public const int MAC_STRING_LENGTH = 17;

		private Button discoverButton;
		private ListView deviceList;
		private Button beServer;
		private Button beClient;
		private ListView log;
		private EditText inputText;
		private Button sendTextButton;

		private BluetoothAdapter btAdapter;
		private Receiver receiver;

		private ArrayAdapter<string> deviceListAdapter;
		private ArrayAdapter<string> logAdapter;

		private ChatServer server;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			Debug("OnCreate()");

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			discoverButton = FindViewById<Button>(Resource.Id.Discover);
			deviceList = FindViewById<ListView>(Resource.Id.DeviceList);
			beServer = FindViewById<Button>(Resource.Id.Server);
			beClient = FindViewById<Button>(Resource.Id.Client);
			log = FindViewById<ListView>(Resource.Id.Log);
			inputText = FindViewById<EditText>(Resource.Id.TextInput);
			sendTextButton = FindViewById<Button>(Resource.Id.SendTextButton);


			deviceListAdapter = new ArrayAdapter<string>(this, Resource.Layout.Message);
			deviceList.Adapter = deviceListAdapter;

			logAdapter = new ArrayAdapter<string>(this, Resource.Layout.Message);
			log.Adapter = logAdapter;
			log.ItemsCanFocus = false;
			log.Focusable = false;

			deviceList.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) =>
			{
				string nameAndMac = deviceListAdapter.GetItem(e.Position);
				string mac = nameAndMac.Substring(nameAndMac.Length - MAC_STRING_LENGTH);

				Regex macRegex = new Regex("([0-9a-fA-F]{2}:){5}[0-9a-fA-F]{2}");

				if (macRegex.IsMatch(mac))
				{
					Log($"Starting connection to {mac}");
					server.AddConnection(mac);
				}
				else
				{
					Debug("Regex failed", WARNING);
				}

			};

			Log($"---START LOG--- v{version}");
			Debug($"STARTING APP VERSION {version}");

			discoverButton.Click += (object sender, EventArgs e) =>
			{
				Debug("Discovery button clicked");
				DoDiscovery();
			};

			beServer.Click += (object sender, EventArgs e) =>
			{
				Log("I is a server!");
				Debug("Server button clicked");
			};

			beClient.Click += (object sender, EventArgs e) =>
			{
				Log("I is a client!");
				Debug("Client button clicked");
			};

			sendTextButton.Click += (object sender, EventArgs e) =>
			{
				Debug($"Sending text {inputText.Text} to {server.connectThreads.Count} threads");

				foreach (KeyValuePair<ChatServer.ConnectThread, bool> threadPair in server.connectThreads)
				{
					if (threadPair.Value)
					{
						threadPair.Key.Write(Encoding.UTF8.GetBytes(inputText.Text));
						Debug($"Sending text to {threadPair.Key}");
					}
					else
					{
						Debug($"Skipping message send to dead thread {threadPair.Key}", WARNING);
					}
				}

				Log($"Me ({server.connectThreads.Count}): {inputText.Text}");
				inputText.Text = "";
			};
		}

		protected override void OnRestart()
		{
			base.OnRestart();

			Debug("OnRestart()");

			foreach (KeyValuePair<ChatServer.ConnectThread, bool> threadPair in server.connectThreads)
			{
				threadPair.Key.Restart();
				server.connectThreads[threadPair.Key] = true;
			}
		}

		protected override void OnStart()
		{
			base.OnStart();

			Debug("OnStart()");

			btAdapter = BluetoothAdapter.DefaultAdapter;

			receiver = new Receiver(this);
			var filter = new IntentFilter();
			filter.AddAction(BluetoothDevice.ActionFound);
			filter.AddAction(BluetoothAdapter.ActionDiscoveryStarted);
			filter.AddAction(BluetoothAdapter.ActionDiscoveryFinished);
			filter.AddAction(BluetoothDevice.ActionPairingRequest);
			RegisterReceiver(receiver, filter);

			EnableBluetooth();

			if (btAdapter.IsEnabled)
			{
				startServer();
			}
		}

		protected override void OnResume()
		{
			base.OnResume();

			Debug("OnResume()");

			// something something bluetooth
		}

		private void DoDiscovery()
		{
			Debug("Discovery Starting");

			if (btAdapter.IsDiscovering)
			{
				Debug("Canceled existing discovery");
				btAdapter.CancelDiscovery();
			}

			EnsureDiscoverable();
			btAdapter.StartDiscovery();
		}

		private void EnableBluetooth()
		{
			if (!btAdapter.IsEnabled)
			{
				Debug("Enabling bluetooth", WARNING);
				Intent enableBtIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
				StartActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
			}
		}

		private void EnsureDiscoverable()
		{
			if (btAdapter.ScanMode != ScanMode.ConnectableDiscoverable)
			{
				Debug("Making device discoverable", WARNING);
				Intent discoverableIntent = new Intent(BluetoothAdapter.ActionRequestDiscoverable);
				discoverableIntent.PutExtra(BluetoothAdapter.ExtraDiscoverableDuration, 300);
				StartActivity(discoverableIntent);
			}
		}

		private void startServer()
		{
			if (server == null)
			{
				Debug("Starting server");
				server = new ChatServer(new LogHandler(this));
			}

			server.Start();
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			switch (requestCode)
			{
				case REQUEST_ENABLE_BT:
					if (resultCode != Result.Ok)
					{
						Debug("Bluetooth setup failed!", ERROR);
					}
					else
					{
						startServer();
					}
					break;
			}
		}

		protected void Log(string text)
		{
			logAdapter.Add(text);
			log.SmoothScrollToPosition(logAdapter.Count - 1);
			Debug($"Writing text {text} to log");
		}

		protected override void OnPause()
		{
			base.OnPause();

			Debug("OnPause()");
		}

		protected override void OnStop()
		{
			base.OnStop();

			Debug("OnStop()");

			if (server.acceptThread != null)
			{
				server.acceptThread.Cancel();
				server.acceptThread = null;
			}

			foreach (KeyValuePair<ChatServer.ConnectThread, bool> threadPair in server.connectThreads)
			{
				threadPair.Key.Pause();
				server.connectThreads[threadPair.Key] = false;
			}

			// Unregister broadcast listeners
			UnregisterReceiver(receiver);
			receiver = null;


			// Make sure we're not doing discovery anymore
			if (btAdapter != null)
			{
				btAdapter.CancelDiscovery();
				Debug("Discovery canceled");

				btAdapter = null;
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			Debug("OnDestroy");
		}

		public class Receiver : BroadcastReceiver
		{
			MainActivity chat;

			public Receiver(MainActivity chat)
			{
				this.chat = chat;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				string action = intent.Action;
				BluetoothDevice device;
				switch (action)
				{
					case BluetoothAdapter.ActionDiscoveryStarted:
						chat.Log("Scan started");
						chat.deviceListAdapter.Clear();
						break;
					case BluetoothDevice.ActionFound:
						// Get the BluetoothDevice object from the Intent
						device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);

						string nameAndMac = $"{device.Name}: {device.Address}";
						if (chat.deviceListAdapter.GetPosition(nameAndMac) < 0)
						{
							chat.deviceListAdapter.Add(nameAndMac);
							chat.deviceList.SmoothScrollToPosition(chat.deviceListAdapter.Count - 1);
						}

						break;
					case BluetoothAdapter.ActionDiscoveryFinished:
						chat.Log($"Scan finished");
						break;
					case BluetoothDevice.ActionPairingRequest:
						chat.Debug("GOT PAIRING REQUEST", WARNING);
						device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);

						int pin = intent.GetIntExtra(BluetoothDevice.ExtraPairingKey, 0);

						chat.Debug($"PIN {pin} for {device.Name}");

						byte[] pinBytes;
						pinBytes = Encoding.UTF8.GetBytes("" + pin);
						device.SetPin(pinBytes);
						device.SetPairingConfirmation(true);
						break;
					default:
						chat.Debug($"Got an action: {action}", WARNING);
						break;
				}
			}
		}

		private class LogHandler : Handler
		{
			MainActivity main;

			public LogHandler(MainActivity main)
			{
				this.main = main;
			}

			public override void HandleMessage(Message msg)
			{
				switch (msg.What)
				{
					case LOG_MESSAGE:
						string buffer = (string)msg.Obj;
						main.Log(buffer);
						break;
				}
			}
		}

		public void Debug(string msg, int level = INFO, string info = "")
		{
			if (!debug)
			{
				return;
			}

			string levelString = "";
			switch (level)
			{
				case INFO:
					levelString = "INFO";
					break;
				case WARNING:
					levelString = "WARNING";
					break;
				case ERROR:
					levelString = "ERROR";
					break;
				case CRITICAL:
					levelString = "CRITICAL";
					break;
			}

			string debugPrefix = $"                               [DEBUG {levelString}] ";

			if (info != "")
			{
				debugPrefix += $"({info}) ";
			}

			lock (this)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(debugPrefix + msg);
				Console.ResetColor();
			}

		}
	}
}

