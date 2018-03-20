using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.Identities;
using CampfireNet.IO;
using CampfireNet.IO.Transport;
using CampfireNet.Utilities;
using CampfireNet.Utilities.AsyncPrimatives;
using CampfireNet.Utilities.Channels;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using static CampfireNet.Utilities.Channels.ChannelsExtensions;

namespace CampfireNet.Simulator {
   public class SimulationBluetoothConnectionState {
      public float Quality;
      public float Connectedness;
   }
   
   public class DeviceAgent {
      public Guid BluetoothAdapterId;
      public Identity CampfireNetIdentity { get; set; }
      public Vector2 Position;
      public Vector2 Velocity;
      public ConcurrentDictionary<DeviceAgent, SimulationBluetoothConnectionState> ActiveConnectionStates = new ConcurrentDictionary<DeviceAgent, SimulationBluetoothConnectionState>();
      public SimulationBluetoothAdapter BluetoothAdapter;
      public CSE561CampfireNetClient Client;
      public int Value;
   }

   public class SimulationBluetoothAdapter : IBluetoothAdapter {
      public const int MAX_RATE_LIMIT_TOKENS = 3;

      private readonly AsyncSemaphore requestRateLimitSemaphore = new AsyncSemaphore(0);
      private readonly DeviceAgent agent;
      private float rateLimitTokenGrantingCounter = 0.0f;
      private readonly Dictionary<Guid, SimulationBluetoothNeighbor> neighborsByAdapterId;

      public Dictionary<Guid, SimulationBluetoothNeighbor> NeighborsByAdapterId => neighborsByAdapterId;

      public SimulationBluetoothAdapter(DeviceAgent agent, Dictionary<Guid, SimulationBluetoothNeighbor> neighborsByAdapterId) {
         this.agent = agent;
         this.neighborsByAdapterId = neighborsByAdapterId;
      }

      public Guid AdapterId => agent.BluetoothAdapterId;

      public void Permit(float dt) {
         rateLimitTokenGrantingCounter += dt;

         if (rateLimitTokenGrantingCounter > 0.100f) {
            rateLimitTokenGrantingCounter -= 1.0f;

            if (requestRateLimitSemaphore.Count < MAX_RATE_LIMIT_TOKENS) {
               requestRateLimitSemaphore.Release();
            }
         }
      }

      public async Task<IReadOnlyList<IBluetoothNeighbor>> DiscoverAsync() {
         await requestRateLimitSemaphore.WaitAsync().ConfigureAwait(false);
         var neighbors = new List<IBluetoothNeighbor>();
         foreach (var pair in agent.ActiveConnectionStates) {
            if (pair.Value.Connectedness == 1.0f) {
               neighbors.Add(neighborsByAdapterId[pair.Key.BluetoothAdapterId]);
            }
         }
         return neighbors;
      }

      public class SimulationBluetoothNeighbor : IBluetoothNeighbor {
         private readonly DeviceAgent self;
         private readonly SimulationConnectionContext connectionContext;

         public SimulationBluetoothNeighbor(DeviceAgent self, SimulationConnectionContext connectionContext) {
            this.self = self;
            this.connectionContext = connectionContext;
         }

         public Guid AdapterId => connectionContext.GetOther(self).BluetoothAdapterId;

         public async Task<bool> TryHandshakeAsync(double minTimeoutSeconds) {
            try {
               await HandshakeAsync().ConfigureAwait(false);
               return true;
            } catch (TimeoutException) {
               return false;
            }
         }

         public Task HandshakeAsync() {
            return connectionContext.ConnectAsync(self);
         }

         public Task SendAsync(byte[] data) {
            return connectionContext.SendAsync(self, data);
         }

         public bool IsConnected => connectionContext.GetIsConnected(self);
         public ReadableChannel<byte[]> InboundChannel => connectionContext.GetInboundChannel(self);

         public void Disconnect() => connectionContext.Disconnect();
      }

      public class AsyncPriorityQueue<T> {
         private readonly AsyncLock sync = new AsyncLock();
         private readonly PriorityQueue<T> inner;

         public AsyncPriorityQueue(PriorityQueue<T> inner) {
            this.inner = inner;
         }

         public int Count => inner.Count;

         public async Task EnqueueAsync(T item) {
            using (await sync.LockAsync().ConfigureAwait(false)) {
               inner.Enqueue(item);
            }
         }

         public async Task<Tuple<bool, T>> TryDequeueAsync() {
            using (await sync.LockAsync().ConfigureAwait(false)) {
               if (inner.IsEmpty) {
                  return Tuple.Create(false, default(T));
               } else {
                  return Tuple.Create(true, inner.Dequeue());
               }
            }
         }
      }

      public class AsyncAdapterEventPriorityQueueChannel<T> : Channel<T> {
         private readonly object queueSync = new object();
         private readonly PriorityQueue<T> queue;
         private readonly Channel<bool> available;
         private readonly Func<T, DateTime> getItemAvailableTime;

         public AsyncAdapterEventPriorityQueueChannel(PriorityQueue<T> queue, Channel<bool> available, Func<T, DateTime> getItemAvailableTime) {
            this.queue = queue;
            this.available = available;
            this.getItemAvailableTime = getItemAvailableTime;
         }

         public int Count => queue.Count;

         public bool TryRead(out T message) {
            bool throwaway;
            if (available.TryRead(out throwaway)) {
               lock (queueSync) {
                  message = queue.Dequeue();
               }
               return true;
            }
            message = default(T);
            return false;
         }

         public async Task<T> ReadAsync(CancellationToken cancellationToken, Func<T, bool> acceptanceTest) {
            while (true) {
               await available.ReadAsync(cancellationToken, x => true).ConfigureAwait(false);
               lock (queueSync) {
                  if (acceptanceTest(queue.Peek())) {
                     return queue.Dequeue();
                  }
               }
               await available.WriteAsync(true, CancellationToken.None).ConfigureAwait(false);
            }
         }

         public async Task WriteAsync(T message, CancellationToken cancellationToken) {
            lock (queueSync) {
               queue.Enqueue(message);
            }
            Go(async () => {
               var now = DateTime.Now;
               var ready = getItemAvailableTime(message);
               if (now < ready) {
                  await Task.Delay(ready - now).ConfigureAwait(false);
               }
               await available.WriteAsync(true, CancellationToken.None).ConfigureAwait(false);
            }).Forget();
         }
      }

      public class SimulationConnectionContext {
         private readonly AsyncLock synchronization = new AsyncLock();
         private readonly DeviceAgent firstAgent; 
         private readonly DeviceAgent secondAgent;
         private readonly Channel<AdapterEvent> adapterEventQueueChannel = new AsyncAdapterEventPriorityQueueChannel<AdapterEvent>(
            new PriorityQueue<AdapterEvent>((a, b) => a.Time.CompareTo(b.Time)),
            ChannelFactory.Nonblocking<bool>(),
            item => item.Time);

         // connect: 
         private readonly BinaryLatchChannel firstDisconnectChannel = new BinaryLatchChannel(true);
         private readonly BinaryLatchChannel secondDisconnectChannel = new BinaryLatchChannel(true);
         private bool isConnectingPeerPending;
         private AsyncLatch connectingPeerSignal;

         // send:
         private readonly DisconnectableChannel<byte[], NotConnectedException> firstInboundChannel;
         private readonly DisconnectableChannel<byte[], NotConnectedException> secondInboundChannel;

         public SimulationConnectionContext(DeviceAgent firstAgent, DeviceAgent secondAgent) {
            this.firstAgent = firstAgent;
            this.secondAgent = secondAgent;

            this.firstInboundChannel = new DisconnectableChannel<byte[], NotConnectedException>(firstDisconnectChannel, ChannelFactory.Nonblocking<byte[]>());
            this.secondInboundChannel = new DisconnectableChannel<byte[], NotConnectedException>(secondDisconnectChannel, ChannelFactory.Nonblocking<byte[]>());
         }

         public void Start() {
            RunAsync().Forget();
         }

         private async Task RunAsync() {
            var pendingBeginConnect = (BeginConnectEvent)null;

            while (true) {
               var adapterEvent = await adapterEventQueueChannel.ReadAsync(CancellationToken.None, x => true).ConfigureAwait(false);
               switch (adapterEvent.GetType().Name) {
                  case nameof(BeginConnectEvent):
                     var beginConnect = (BeginConnectEvent)adapterEvent;
                     if (pendingBeginConnect == null) {
                        pendingBeginConnect = beginConnect;
                     } else {
                        firstDisconnectChannel.SetIsClosed(false);
                        secondDisconnectChannel.SetIsClosed(false);

                        var pendingBeginConnectCapture = pendingBeginConnect;
                        pendingBeginConnect = null;

                        pendingBeginConnectCapture.ResultBox.SetResult(true);
                        beginConnect.ResultBox.SetResult(true);
                     }
                     break;
                  case nameof(TimeoutConnectEvent):
                     var timeout = (TimeoutConnectEvent)adapterEvent;
                     if (timeout.BeginEvent == pendingBeginConnect) {
                        pendingBeginConnect.ResultBox.SetException(new TimeoutException());
                        pendingBeginConnect = null;
                     }
                     break;
                  case nameof(SendEvent):
                     var send = (SendEvent)adapterEvent;
                     if (!GetIsConnected(send.Initiator)) {
                        send.CompletionBox.SetException(new NotConnectedException());
                        break;
                     }

                     var connectivity = SimulationBluetoothCalculator.ComputeConnectivity(firstAgent, secondAgent);
                     if (!connectivity.InRange) {
                        firstDisconnectChannel.SetIsClosed(true);
                        secondDisconnectChannel.SetIsClosed(true);

                        send.CompletionBox.SetException(new NotConnectedException());
                        break;
                     }

                     var deltaBytesSent = (int)Math.Ceiling(connectivity.SignalQuality * send.Interval.TotalSeconds * SimulationBluetoothConstants.MAX_OUTBOUND_BYTES_PER_SECOND);
                     var bytesSent = send.BytesSent + deltaBytesSent;
                     if (bytesSent >= send.Payload.Length) {
						      await GetOtherInboundChannelInternal(send.Initiator).WriteAsync(send.Payload).ConfigureAwait(false);
                        send.CompletionBox.SetResult(true);
                        break;
                     }

                     var nextEvent = new SendEvent(DateTime.Now + send.Interval, send.Interval, send.Initiator, send.CompletionBox, send.Payload, bytesSent);
                     await adapterEventQueueChannel.WriteAsync(nextEvent, CancellationToken.None).ConfigureAwait(false);
                     break;
               }
            }
         }

         public async Task ConnectAsync(DeviceAgent sender) {
            var now = DateTime.Now;
            var connectEvent = new BeginConnectEvent(now + TimeSpan.FromMilliseconds(SimulationBluetoothConstants.BASE_HANDSHAKE_DELAY_MILLIS), sender);
            await adapterEventQueueChannel.WriteAsync(connectEvent).ConfigureAwait(false);
            var timeoutEvent = new TimeoutConnectEvent(now + TimeSpan.FromMilliseconds(SimulationBluetoothConstants.HANDSHAKE_TIMEOUT_MILLIS), connectEvent);
            await adapterEventQueueChannel.WriteAsync(timeoutEvent).ConfigureAwait(false);
            await connectEvent.ResultBox.GetResultAsync().ConfigureAwait(false);
         }

         public async Task SendAsync(DeviceAgent sender, byte[] contents) {
            var interval = TimeSpan.FromMilliseconds(SimulationBluetoothConstants.SEND_TICK_INTERVAL);
            var completionBox = new AsyncBox<bool>();
            var sendEvent = new SendEvent(DateTime.Now + interval, interval, sender, completionBox, contents, 0);
            await adapterEventQueueChannel.WriteAsync(sendEvent).ConfigureAwait(false);
            await completionBox.GetResultAsync().ConfigureAwait(false);
         }

         public DeviceAgent GetOther(DeviceAgent self) => self == firstAgent ? secondAgent : firstAgent;
         private Channel<byte[]> GetInboundChannelInternal(DeviceAgent self) => self == firstAgent ? firstInboundChannel : secondInboundChannel;
         public ReadableChannel<byte[]> GetInboundChannel(DeviceAgent self) => GetInboundChannelInternal(self);
         private Channel<byte[]> GetOtherInboundChannelInternal(DeviceAgent self) => GetInboundChannelInternal(GetOther(self));
         public ReadableChannel<byte[]> GetOtherInboundChannel(DeviceAgent self) => GetOtherInboundChannelInternal(self);

         public bool GetIsConnected(DeviceAgent self) => self == firstAgent ? !firstDisconnectChannel.IsClosed : !secondDisconnectChannel.IsClosed;

         public void Disconnect() {
//            throw new NotImplementedException();
         }
      }

      public abstract class AdapterEvent {
         protected AdapterEvent(DateTime time) {
            Time = time;
         }

         public DateTime Time { get; }
      }

      public class BeginConnectEvent : AdapterEvent {
         public BeginConnectEvent(DateTime time, DeviceAgent initiator) : base(time) {
            Initiator = initiator;
         }

         public DeviceAgent Initiator { get; }
         public AsyncBox<bool> ResultBox { get; } = new AsyncBox<bool>();
      }

      public class TimeoutConnectEvent : AdapterEvent {
         public TimeoutConnectEvent(DateTime time, BeginConnectEvent beginEvent) : base(time) {
            BeginEvent = beginEvent;
         }

         public BeginConnectEvent BeginEvent { get; }
      }

      public class SendEvent : AdapterEvent {
         public SendEvent(DateTime time, TimeSpan interval, DeviceAgent initiator, AsyncBox<bool> completionBox, byte[] payload, int bytesSent) : base(time) {
            Interval = interval;
            Initiator = initiator;
            CompletionBox = completionBox;
            Payload = payload;
            BytesSent = bytesSent;
         }

         public TimeSpan Interval { get; }
         public DeviceAgent Initiator { get; }
         public AsyncBox<bool> CompletionBox { get; }
         public byte[] Payload { get; }
         public int BytesSent { get; }
      }
   }

   public static class SimulationBluetoothConstants {
      public const int RANGE = 100;
      public const int RANGE_SQUARED = RANGE * RANGE;

      public const int BASE_HANDSHAKE_DELAY_MILLIS = 300;
      public const float MIN_VIABLE_SIGNAL_QUALITY = 0.2f;

      public const int HANDSHAKE_TIMEOUT_MILLIS = 5000;
      public const int SENDRECV_TIMEOUT_MILLIS = 2000;
      public const int SEND_TICK_INTERVAL = 300;

      public const int MAX_OUTBOUND_BYTES_PER_SECOND = 3 * 1024 * 1024;
   }

   public static class SimulationBluetoothCalculator {
      public static SimulationBluetoothConnectivity ComputeConnectivity(DeviceAgent a, DeviceAgent b) {
         var quality = 1.0 - (a.Position - b.Position).LengthSquared() / SimulationBluetoothConstants.RANGE_SQUARED;
         return new SimulationBluetoothConnectivity {
            InRange = quality > 0.0,
            IsSufficientQuality = quality > SimulationBluetoothConstants.MIN_VIABLE_SIGNAL_QUALITY,
            SignalQuality = (float)Math.Max(0, quality)
         };
      }

      public static TimeSpan ComputeHandshakeDelay(float connectivitySignalQuality) {
         return TimeSpan.FromMilliseconds((int)(SimulationBluetoothConstants.BASE_HANDSHAKE_DELAY_MILLIS / connectivitySignalQuality));
      }
   }

   public class SimulationBluetoothConnectivity {
      public bool InRange { get; set; }
      public bool IsSufficientQuality { get; set; }
      public float SignalQuality { get; set; }
   }

   public class SimulatorGame : Game {
      private readonly SimulatorConfiguration configuration;
      private readonly DeviceAgent[] agents;
      private readonly GraphicsDeviceManager graphicsDeviceManager;

      private SpriteBatch spriteBatch;
      private Texture2D whiteTexture;
      private Texture2D whiteCircleTexture;
      private RasterizerState rasterizerState;
      private int epochAgentIndex = 0;
      private int epoch = 0;

      public SimulatorGame(SimulatorConfiguration configuration, DeviceAgent[] agents) {
         this.configuration = configuration;
         this.agents = agents;

         graphicsDeviceManager = new GraphicsDeviceManager(this) {
            PreferredBackBufferWidth = configuration.DisplayWidth,
            PreferredBackBufferHeight = configuration.DisplayHeight,
            PreferMultiSampling = true
         };
      }

      protected override void LoadContent() {
         base.LoadContent();

         spriteBatch = new SpriteBatch(graphicsDeviceManager.GraphicsDevice);
         SpriteBatchEx.GraphicsDevice = GraphicsDevice;

         whiteTexture = CreateSolidTexture(Color.White);
         whiteCircleTexture = CreateSolidCircleTexture(Color.White, 256);

         rasterizerState = GraphicsDevice.RasterizerState = new RasterizerState { MultiSampleAntiAlias = true };

         epoch++;
         epochAgentIndex = agents.Length / 2;
         agents[epochAgentIndex].Client.BroadcastAsync(BitConverter.GetBytes(epoch)).Forget();

         //for (int i = 0; i < agents.Count; i++) {
         //   agents[i].Position = new Vector2(320 + 50 * (i % 14), 80 + 70 * i / 14);
         //   agents[i].Velocity *= 0.05f;
         //}
         //agents[36].Value = MAX_VALUE;
      }

      protected override void Update(GameTime gameTime) {
         base.Update(gameTime);
         var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

         if (Keyboard.GetState().IsKeyDown(Keys.S)) {
            dt *= 10;
         }

         for (var i = 0; i < agents.Length; i++) {
            var agent = agents[i];
            agent.Position += agent.Velocity * dt;
            if (agent.Position.X < configuration.AgentRadius)
               agent.Velocity.X = Math.Abs(agent.Velocity.X);
            if (agent.Position.X > configuration.FieldWidth - configuration.AgentRadius)
               agent.Velocity.X = -Math.Abs(agent.Velocity.X);
            if (agent.Position.Y < configuration.AgentRadius)
               agent.Velocity.Y = Math.Abs(agent.Velocity.Y);
            if (agent.Position.Y > configuration.FieldHeight - configuration.AgentRadius)
               agent.Velocity.Y = -Math.Abs(agent.Velocity.Y);
            agent.BluetoothAdapter.Permit(dt);
         }

         int columns = (int)Math.Ceiling((float)configuration.FieldWidth / SimulationBluetoothConstants.RANGE);
         int rows = (int)Math.Ceiling((float)configuration.FieldHeight / SimulationBluetoothConstants.RANGE);
         var bins = Enumerable.Range(0, columns * rows).Select(i => new List<DeviceAgent>()).ToArray();
         foreach (var agent in agents) {
            var binX = ((int)agent.Position.X) / SimulationBluetoothConstants.RANGE;
            var binY = ((int)agent.Position.Y) / SimulationBluetoothConstants.RANGE;
            bins[binX + binY * columns].Add(agent);
         }

         var processAgentPair = new Action<DeviceAgent, DeviceAgent>((a, b) => {
            var distanceSquared = (a.Position - b.Position).LengthSquared();
            if (distanceSquared < SimulationBluetoothConstants.RANGE_SQUARED) {
               if (!a.ActiveConnectionStates.ContainsKey(b)) {
                  var connectionState = new SimulationBluetoothConnectionState();

                  if (!a.BluetoothAdapter.NeighborsByAdapterId.ContainsKey(b.BluetoothAdapterId)) {
                     var connectionContext = new SimulationBluetoothAdapter.SimulationConnectionContext(a, b);
                     a.BluetoothAdapter.NeighborsByAdapterId[b.BluetoothAdapterId] = new SimulationBluetoothAdapter.SimulationBluetoothNeighbor(a, connectionContext);
                     b.BluetoothAdapter.NeighborsByAdapterId[a.BluetoothAdapterId] = new SimulationBluetoothAdapter.SimulationBluetoothNeighbor(b, connectionContext);
                     connectionContext.Start();
                  }

                  a.ActiveConnectionStates.AddOrThrow(b, connectionState);
                  b.ActiveConnectionStates.AddOrThrow(a, connectionState);
               }
            }
         });

         var processBins = new Action<List<DeviceAgent>, List<DeviceAgent>>((a, b) => {
            if (a == null || b == null) return;
            if (a == b) {
               for (var i = 0; i < a.Count - 1; i++) {
                  for (var j = i + 1; j < a.Count; j++) {
                     processAgentPair(a[i], a[j]);
                  }
               }
            } else {
               for (var i = 0; i < a.Count; i++) {
                  for (var j = 0; j < b.Count; j++) {
                     processAgentPair(a[i], b[j]);
                  }
               }
            }
         });
         
         for (var y = 0; y < rows; y++) {
            for (var x = 0; x < columns; x++) {
               var a = bins[x + y * columns];
               var right = x + 1 < columns ? bins[(x + 1) + y * columns] : null;
               var bottom = (y + 1 < rows) ? bins[x + (y + 1) * columns] : null;
               var bottomRight = (x + 1 < columns && y + 1 < rows) ? bins[(x + 1) + (y + 1) * columns] : null;

               processBins(a, a);
               processBins(a, right);
               processBins(a, bottom);
               processBins(a, bottomRight);
               processBins(right, bottom);
            }
         }

         var dConnectnessInRangeBase = dt * 5.0f;
         var dConnectnessOutOfRangeBase = -dt * 50.0f;
         foreach (var agent in agents) {
            foreach (var pair in agent.ActiveConnectionStates) {
               if (agent.BluetoothAdapterId.CompareTo(pair.Key.BluetoothAdapterId) < 0)
                  continue;

               var other = pair.Key;
               var connectivity = SimulationBluetoothCalculator.ComputeConnectivity(agent, other);
               var deltaConnectedness = (connectivity.InRange ? connectivity.SignalQuality * dConnectnessInRangeBase : dConnectnessOutOfRangeBase);

               var connectionState = pair.Value;
               float nextConnectedness = Math.Max(0.0f, Math.Min(1.0f, connectionState.Connectedness + deltaConnectedness));

               connectionState.Quality = connectivity.SignalQuality;
               connectionState.Connectedness = nextConnectedness;

               if (nextConnectedness == 0.0f) {
                  agent.ActiveConnectionStates.RemoveOrThrow(other);
                  other.ActiveConnectionStates.RemoveOrThrow(agent);
               }
            }
         }

//         for (var i = 0; i < agents.Length - 1; i++) {
//            var a = agents[i];
//            var aConnectionStates = a.BluetoothState.ConnectionStates;
//            for (var j = i + 1; j < agents.Length; j++) {
//               var b = agents[j];
//               var bConnectionStates = b.BluetoothState.ConnectionStates;
//            }
//         }

         if (Keyboard.GetState().IsKeyDown(Keys.A)) {
            epoch++;
            epochAgentIndex = (int)(new Random(epochAgentIndex + 5).Next(0, agents.Length));
            agents[epochAgentIndex].Value = epoch;
            agents[epochAgentIndex].Client.BroadcastAsync(BitConverter.GetBytes(epoch)).Forget();
         }

         if (Keyboard.GetState().IsKeyDown(Keys.Z)) {
            while (epoch < 5) {
               epoch++;
               epochAgentIndex = (int)(new Random(epochAgentIndex + 5).Next(0, agents.Length));
               agents[epochAgentIndex].Client.BroadcastAsync(BitConverter.GetBytes(epoch)).Forget();
            }
         }

         if (Keyboard.GetState().IsKeyDown(Keys.X)) {
            while (epoch < 10) {
               epoch++;
               epochAgentIndex = (int)(new Random(epochAgentIndex + 5).Next(0, agents.Length));
               agents[epochAgentIndex].Client.BroadcastAsync(BitConverter.GetBytes(epoch)).Forget();
            }
         }
      }

      protected override void Draw(GameTime gameTime) {
         base.Draw(gameTime);

         Console.WriteLine((int)(1000 / gameTime.ElapsedGameTime.TotalMilliseconds) + " " + gameTime.ElapsedGameTime.TotalMilliseconds);

         GraphicsDevice.Clear(Color.White);
         spriteBatch.Begin(SpriteSortMode.Deferred, null, transformMatrix: Matrix.CreateScale((float)configuration.DisplayHeight / configuration.FieldHeight));

         for (var i = 0; i < agents.Length; i++) {
            var agent = agents[i];
            foreach (var pair in agent.ActiveConnectionStates) {
               var other = pair.Key;

               if (agent.BluetoothAdapterId.CompareTo(other.BluetoothAdapterId) < 0)
                  continue;

               var neighborBluetoothAdapter = agent.BluetoothAdapter.NeighborsByAdapterId[other.BluetoothAdapterId];
               if (neighborBluetoothAdapter.IsConnected)
                  spriteBatch.DrawLine(agent.Position, other.Position, Color.Gray);
            }
         }

         for (var i = 0; i < agents.Length; i++) {
            var lum = (epoch - agents[i].Value) * 240 / epoch;
            var color = new Color(lum, lum, lum);
            if (epoch == agents[i].Value && agents[i].Value > 0) color = Color.Red;
            if (epoch == agents[i].Value + 1 && agents[i].Value > 0) color = Color.Lime;
            if (epoch == agents[i].Value + 2 && agents[i].Value > 0) color = Color.MediumAquamarine;
            if (epoch == agents[i].Value + 3 && agents[i].Value > 0) color = Color.Magenta;
            if (epoch == agents[i].Value + 4 && agents[i].Value > 0) color = Color.Orange;
            DrawCenteredCircleWorld(agents[i].Position, configuration.AgentRadius, color);
         }
         //spriteBatch.DrawLine(new Vector2(0, 50), new Vector2(100, 50), Color.Red);
         spriteBatch.End();
      }

      private Texture2D CreateSolidTexture(Color color) {
         var texture = new Texture2D(GraphicsDevice, 1, 1);
         texture.SetData(new[] { color });
         return texture;
      }

      private Texture2D CreateSolidCircleTexture(Color color, int radius) {
         var diameter = radius * 2;

         // could optimize by symmetry, but whatever this is cheap
         var imageData = new Color[diameter * diameter];
         for (int x = 0; x < diameter; x++) {
            for (int y = 0; y < diameter; y++) {
               imageData[x * diameter + y] = new Vector2(x - radius, y - radius).Length() <= radius ? color : Color.Transparent;
            }
         }

         var texture = new Texture2D(GraphicsDevice, diameter, diameter);
         texture.SetData(imageData);
         return texture;
      }

      public void DrawCenteredCircleWorld(Vector2 center, float radius, Color color) {
         spriteBatch.Draw(
            whiteCircleTexture,
            new Rectangle((int)(center.X - radius), (int)(center.Y - radius), (int)(2 * radius), (int)(2 * radius)),
            color
         );
      }
   }
}
