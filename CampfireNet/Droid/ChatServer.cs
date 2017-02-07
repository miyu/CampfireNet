using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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


		internal AcceptThread acceptThread;
		internal ConcurrentDictionary<ConnectThread, bool> connectThreads;

		protected Handler handler;

		public ChatServer(Handler handler)
		{
			acceptThread = null;
			connectThreads = new ConcurrentDictionary<ConnectThread, bool>();
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

		public void AddConnection(string mac)
		{
			BluetoothDevice device = BluetoothAdapter.DefaultAdapter.GetRemoteDevice(mac);
			ConnectThread thread = new ConnectThread(device, this);
			connectThreads.TryAdd(thread, true);
			thread.Start();
		}

		public void AddConnection(BluetoothSocket socket)
		{
			ConnectThread thread = new ConnectThread(socket, this);
			connectThreads.TryAdd(thread, true);
			thread.Start();
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

		internal class AcceptThread : Java.Lang.Thread
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
				catch (Java.IO.IOException e)
				{
					Console.WriteLine($"Couldn't listen: {e.Message}");
					server.WriteAsServer("Couldn't start server");
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
					catch (Java.IO.IOException e)
					{
						server.WriteAsServer($"Accept failed {e.Message}");

						if (++connectFails >= 3)
						{
							break;
						}
						continue;
					}

					if (socket != null)
					{
						server.AddConnection(socket);
						server.WriteAsServer($"Got connection from {socket.RemoteDevice.Name}");
					}
				}
			}

			public void Cancel()
			{
				try
				{
					serverSocket.Close();
				}
				catch (Java.IO.IOException e)
				{
					server.WriteAsServer("(AcceptThread) Could not close socket: " + e.Message);
				}
			}
		}

		/*internal class ConnectedThread : Thread
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
				catch (Java.IO.IOException e)
				{
					Console.WriteLine("Sockets not created: " + e.Message);
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

			public void Cancel()
			{
				try
				{
					socket.Close();
				}
				catch (Java.IO.IOException e)
				{
					server.WriteAsServer("(ConnectedThread) Could not close socket: " + e.Message);
				}
			}
		}*/

		internal class ConnectThread : Java.Lang.Thread
		{
			private BluetoothSocket socket;
			private BluetoothDevice device;
			private ChatServer server;
			private Stream inStream;
			private Stream outStream;

			private bool needsConnection;

			private ManualResetEvent runLock;
			private object sync;
			private bool finished;

			private void SetupLock()
			{
				runLock = new ManualResetEvent(true);
				sync = new object();
				finished = false;
			}

			public ConnectThread(BluetoothDevice device, ChatServer server)
			{
				this.device = device;
				this.server = server;

				CreateSocket();

				SetupLock();
			}

			private void CreateSocket()
			{
				Console.WriteLine("Running CreateSocket");
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
				needsConnection = true;
			}

			public ConnectThread(BluetoothSocket socket, ChatServer server)
			{
				this.socket = socket;
				this.device = socket.RemoteDevice;
				this.server = server;
				needsConnection = false;
				SetupLock();
			}

			private bool Connect()
			{
				Console.WriteLine("Running Connect");

				if (needsConnection)
				{
					try
					{
						socket.Connect();
						server.WriteAsClient($"Connected to {device.Name}");
					}
					catch (Java.IO.IOException e)
					{
						server.WriteAsClient($"Can't open connection to {device.Name}: {e.Message}");

						try
						{
							socket.Close();
						}
						catch (Java.IO.IOException)
						{
							server.WriteAsClient($"Can't close connection to {device.Name}");
						}

						return false;
					}
				}

				return true;
			}

			private bool GetStreams()
			{
				Console.WriteLine("Running GetStreams");

				Stream tmpIn = null;
				Stream tmpOut = null;

				try
				{
					tmpIn = socket.InputStream;
					tmpOut = socket.OutputStream;
				}
				catch (Java.IO.IOException e)
				{
					Console.WriteLine("Sockets not created: " + e.Message);
					return false;
				}

				inStream = tmpIn;
				outStream = tmpOut;

				return true;
			}

			public override void Run()
			{
				Name = "Connect Thread";

				if (!Connect())
				{
					RemoveFromDict();
					return;
				}

				if (!GetStreams())
				{
					RemoveFromDict();
					return;
				}

				byte[] buffer = new byte[1024];

				while (!finished)
				{
					lock (sync)
					{
						try
						{
							ReadBytes(buffer, 0, 4);
							int numBytes = BitConverter.ToInt32(buffer, 0);

							if (numBytes > 1024)
							{
								server.WriteAsServer($"Got bad byte num {numBytes}");
								break;
							}

							ReadBytes(buffer, 0, numBytes);
							server.WriteAsServer(socket.RemoteDevice.Name + ": " + Encoding.UTF8.GetString(buffer, 0, numBytes));
						}
						catch (Java.IO.IOException)
						{
							server.WriteAsServer($"disconnected from {socket.RemoteDevice.Name}");

							try
							{
								socket.Close();
							}
							catch (Java.IO.IOException)
							{
								server.WriteAsClient($"Can't close connection to {device.Name}");
							}

							break;
						}
					}

					runLock.WaitOne();

				}

				RemoveFromDict();
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

			public void Pause()
			{
				Console.WriteLine("Pausing thread");
				runLock.Reset();

				lock (sync)
				{
					try
					{
						socket.Close();
					}
					catch (Java.IO.IOException e)
					{
						server.WriteAsServer($"(ConnectThread) Could not close socket: {e.Message}");
					}
				}

				Console.WriteLine("Thread paused");
			}

			private void RemoveFromDict()
			{
				bool tmp;
				server.connectThreads.TryRemove(this, out tmp);
				Console.WriteLine("Removing thread");
			}

			public void Restart()
			{
				lock (sync)
				{
					Console.WriteLine("Restarting thread");
					CreateSocket();

					bool connect = Connect();
					bool getStreams = connect && GetStreams();
					finished = !getStreams;

					Console.WriteLine($"Connect: {connect} GetStreams: {getStreams}");
				}

				runLock.Set();
			}
		}
	}
}
