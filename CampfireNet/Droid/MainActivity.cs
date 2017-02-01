using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Widget;
using Android.OS;

namespace AndroidTest.Droid
{
	[Activity(Label = "AndroidTest", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{
		private TextView outputText;
		private ProgressBar progressBar;
		private BluetoothAdapter btAdapter;
		private Receiver receiver;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			Button discoverButton = FindViewById<Button>(Resource.Id.Discover);
			outputText = FindViewById<TextView>(Resource.Id.DeviceList);
			progressBar = FindViewById<ProgressBar>(Resource.Id.Progress);

			progressBar.Indeterminate = false;
			progressBar.Visibility = Android.Views.ViewStates.Invisible;
			Log("---START LOG---");

			discoverButton.Click += (object sender, System.EventArgs e) =>
			{
				DoDiscovery();
			};

			receiver = new Receiver(this);
			var filter = new IntentFilter();
			filter.AddAction(BluetoothDevice.ActionFound);
			filter.AddAction(BluetoothAdapter.ActionDiscoveryStarted);
			filter.AddAction(BluetoothAdapter.ActionDiscoveryFinished);
			RegisterReceiver(receiver, filter);

			btAdapter = BluetoothAdapter.DefaultAdapter;
		}

		private void DoDiscovery()
		{
			if (btAdapter.IsDiscovering)
			{
				btAdapter.CancelDiscovery();
			}

			btAdapter.StartDiscovery();

			progressBar.Visibility = Android.Views.ViewStates.Visible;
		}

		protected void Log(string text)
		{
			outputText.Text += text + "\n";
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
				}
				else if (action == BluetoothDevice.ActionFound)
				{
					// Get the BluetoothDevice object from the Intent
					BluetoothDevice device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
					// If it's already paired, skip it, because it's been listed already
					if (device.BondState != Bond.Bonded)
					{
						chat.Log("Found: " + device.Name + ", " + device.Address);

					}
					// When discovery is finished, change the Activity title
				}
				else if (action == BluetoothAdapter.ActionDiscoveryFinished)
				{
					chat.progressBar.Visibility = Android.Views.ViewStates.Invisible;
					chat.Log("Scan finished");
				}
				else
				{
					chat.Log("Got an action: " + action);
				}
			}
		}
	}
}

