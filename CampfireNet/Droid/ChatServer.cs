using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Android.Bluetooth;
using Android.OS;
using Java.Lang;
using Java.Util;

namespace AndroidTest.Droid
{
	public class ChatServer
	{
		private const string NAME = "Test Chat";
		//private static UUID APP_UUID = UUID.FromString("f4c178b9-f755-4931-88c2-6e32e447813b");
		private static UUID APP_UUID = UUID.FromString("fa87c0d0-afac-11de-8a39-0800200c9a66");


		private AcceptThread acceptThread;
		public List<ConnectedThread> connectThreads;

		protected Handler handler;

		public ChatServer(Handler handler)
		{
			acceptThread = null;
			connectThreads = new List<ConnectedThread>();
			this.handler = handler;
		}

		public void Start()
		{
			if (acceptThread == null)
			{
				acceptThread = new AcceptThread(this);
				acceptThread.Start();
			}
		}

		public void StartNewConnection(string mac)
		{
			BluetoothDevice device = BluetoothAdapter.DefaultAdapter.GetRemoteDevice(mac);
			ConnectThread thread = new ConnectThread(device, this);
			thread.Start();
		}

		public void AddConnection(BluetoothSocket socket)
		{
			lock (this)
			{
				ConnectedThread thread = new ConnectedThread(socket, this);
				connectThreads.Add(thread);
				thread.Start();
			}
		}

		public void WriteAsClient(string msg)
		{
			lock (this)
			{
				handler.ObtainMessage(MainActivity.LOG_MESSAGE, $"Client got: {msg}").SendToTarget();
			}
		}

		public void WriteAsServer(string msg)
		{
			lock (this)
			{
				handler.ObtainMessage(MainActivity.LOG_MESSAGE, $"Server got: {msg}").SendToTarget();
			}
		}

		private class AcceptThread : Thread
		{
			private BluetoothServerSocket serverSocket;
			private ChatServer server;

			public AcceptThread(ChatServer server)
			{
				this.server = server;
				BluetoothServerSocket tmp = null;

				try
				{
					tmp = BluetoothAdapter.DefaultAdapter.ListenUsingInsecureRfcommWithServiceRecord(NAME, APP_UUID);
				}
				catch (Java.IO.IOException)
				{
					Console.WriteLine("Couldn't listen");
				}

				serverSocket = tmp;
			}

			public override void Run()
			{
				Name = "AcceptThread";
				BluetoothSocket socket = null;

				Console.WriteLine("Running Accept thread");
				server.WriteAsServer("Running Accept thread");

				int connectFails = 0;
				while (true)
				{
					try
					{
						socket = serverSocket.Accept();
						connectFails = 0;
					}
					catch (Java.IO.IOException)
					{
						server.WriteAsServer("Accept failed");

						if (connectFails++ > 5)
						{
							break;
						}
						continue;
					}

					if (socket != null)
					{
						lock (this)
						{
							server.AddConnection(socket);
							server.WriteAsServer($"Got connection from {socket.RemoteDevice.Name}");
						}
					}
				}
			}
		}

		public class ConnectedThread : Thread
		{
			private BluetoothSocket socket;
			private ChatServer server;
			private Stream inStream;
			private Stream outStream;

			public ConnectedThread(BluetoothSocket socket, ChatServer server)
			{
				this.socket = socket;
				this.server = server;

				Stream tmpIn = null;
				Stream tmpOut = null;

				try
				{
					tmpIn = socket.InputStream;
					tmpOut = socket.OutputStream;
				}
				catch (Java.IO.IOException)
				{
					Console.WriteLine("Sockets not created");
				}

				inStream = tmpIn;
				outStream = tmpOut;
			}

			public override void Run()
			{
				byte[] buffer = new byte[1024];

				while (true)
				{
					try
					{
						ReadBytes(buffer, 0, 4);
						int numBytes = BitConverter.ToInt32(buffer, 0);

						ReadBytes(buffer, 0, numBytes);
						server.WriteAsServer(socket.RemoteDevice.Name + ": " + Encoding.UTF8.GetString(buffer, 0, numBytes));
					}
					catch (Java.IO.IOException)
					{
						server.WriteAsServer($"disconnected from {socket.RemoteDevice.Name}");
						break;
					}
				}
			}

			private void ReadBytes(byte[] buffer, int offset, int bytesRemaining)
			{
				while (bytesRemaining > 0)
				{
					var bytesRead = inStream.Read(buffer, offset, bytesRemaining);
					offset += bytesRead;
					bytesRemaining -= bytesRead;
				}
			}

			public void Write(byte[] buffer)
			{
				try
				{
					outStream.Write(BitConverter.GetBytes(buffer.Length), 0, 4);
					outStream.Write(buffer, 0, buffer.Length);
					server.WriteAsServer($"Wrote {Encoding.UTF8.GetString(buffer)}");
				}
				catch (Java.IO.IOException)
				{
					server.WriteAsServer($"Could not write to {socket.RemoteDevice.Name}");
				}
			}
		}

		private class ConnectThread : Thread
		{
			private BluetoothSocket socket;
			private BluetoothDevice device;
			private ChatServer server;

			public ConnectThread(BluetoothDevice device, ChatServer server)
			{
				this.device = device;
				this.server = server;

				BluetoothSocket tmp = null;

				server.WriteAsClient("Connecting...");

				try
				{
					tmp = device.CreateInsecureRfcommSocketToServiceRecord(APP_UUID);
				}
				catch (Java.IO.IOException)
				{
					server.WriteAsClient($"Can't create connection to {device.Name}");
				}

				socket = tmp;
			}

			public override void Run()
			{
				Name = "Connect Thread";

				try
				{
					socket.Connect();
				}
				catch (Java.IO.IOException)
				{
					server.WriteAsClient($"Can't open connection to {device.Name}");

					try
					{
						socket.Close();
					}
					catch (Java.IO.IOException)
					{
						server.WriteAsClient($"Can't close connection to {device.Name}");
					}

					return;
				}

				server.AddConnection(socket);
			}

			public void Cancel()
			{
				try
				{
					socket.Close();
				}
				catch (Java.IO.IOException)
				{
					server.WriteAsClient("Cancel failed to close");
				}
			}
		}
	}
}
