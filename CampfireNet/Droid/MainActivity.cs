using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Widget;
using Android.OS;
using System.Text;
using System;
using Android.Views;
using System.Text.RegularExpressions;

namespace AndroidTest.Droid
{
	[Activity(Label = "AndroidTest", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{
		public const int LOG_MESSAGE = 1;

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
					server.StartNewConnection(mac);
				}
				else
				{
					Log("Regex failed");
				}

			};

			Log("---START LOG---");
			Log("OnCreate");

			discoverButton.Click += (object sender, System.EventArgs e) =>
			{
				DoDiscovery();
			};

			beServer.Click += (object sender, System.EventArgs e) =>
			{
				startServer();
				Log("I is a server!");
			};

			beClient.Click += (object sender, System.EventArgs e) =>
			{
				startClient();
				Log("I is a client!");
			};

			sendTextButton.Click += (object sender, EventArgs e) =>
			{
				Log($"Sending text {inputText.Text}");

				foreach (var thread in server.connectThreads)
				{
					thread.Write(Encoding.UTF8.GetBytes(inputText.Text));
					Log($"Sending text to {thread}");
				}

				inputText.Text = "";
			};

			receiver = new Receiver(this);
			var filter = new IntentFilter();
			filter.AddAction(BluetoothDevice.ActionFound);
			filter.AddAction(BluetoothAdapter.ActionDiscoveryStarted);
			filter.AddAction(BluetoothAdapter.ActionDiscoveryFinished);
			RegisterReceiver(receiver, filter);

			btAdapter = BluetoothAdapter.DefaultAdapter;
		}

		protected override void OnStart()
		{
			base.OnStart();

			Log("OnStart");

			// turn on bluetooth
		}

		protected override void OnResume()
		{
			base.OnResume();

			Log("OnResume");

			// something something bluetooth
		}

		private void DoDiscovery()
		{
			if (btAdapter.IsDiscovering)
			{
				btAdapter.CancelDiscovery();
			}

			btAdapter.StartDiscovery();
		}

		private void startServer()
		{
			if (server == null)
			{
				server = new ChatServer(new LogHandler(this));
				server.Start();
			}
		}

		private void startClient()
		{

		}

		protected void Log(string text)
		{
			logAdapter.Add(text);
			log.SmoothScrollToPosition(logAdapter.Count - 1);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			// Make sure we're not doing discovery anymore
			if (btAdapter != null)
			{
				btAdapter.CancelDiscovery();
			}

			// Unregister broadcast listeners
			UnregisterReceiver(receiver);
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

				// When discovery finds a device
				if (action == BluetoothAdapter.ActionDiscoveryStarted)
				{
					chat.Log("Scan started");
					chat.deviceListAdapter.Clear();
				}
				else if (action == BluetoothDevice.ActionFound)
				{
					// Get the BluetoothDevice object from the Intent
					BluetoothDevice device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
					// If it's already paired, skip it, because it's been listed already
					if (device.BondState != Bond.Bonded)
					{
						chat.deviceListAdapter.Add(device.Name + ": " + device.Address);
						chat.deviceList.SmoothScrollToPosition(chat.deviceListAdapter.Count - 1);
					}
					// When discovery is finished, change the Activity title
				}
				else if (action == BluetoothAdapter.ActionDiscoveryFinished)
				{
					chat.Log("Scan finished");
				}
				else
				{
					chat.Log("Got an action: " + action);
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
	}
}

