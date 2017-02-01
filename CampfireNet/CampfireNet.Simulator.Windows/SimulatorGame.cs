using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.Utilities.AsyncPrimatives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace CampfireNet.Simulator {
   public struct SimulationBluetoothConnectionState {
      public float Quality;
      public float Connectedness;
   }

   public class SimulationBluetoothState {
      public SimulationBluetoothConnectionState[] ConnectionStates { get; set; }
   }
   
   public class DeviceAgent {
      public Guid BluetoothAdapterId;
      public Vector2 Position;
      public Vector2 Velocity;
      public float Value;
      public SimulationBluetoothState BluetoothState;
      public SimulationBluetoothAdapter BluetoothAdapter;
   }

   public interface IBluetoothNeighbor {
      Task HandshakeAsync();
   }

   public interface IBluetoothAdapter {
      Task<IReadOnlyList<IBluetoothNeighbor>> DiscoverAsync();
   }

   public class SimulationBluetoothAdapter : IBluetoothAdapter {
      private readonly AsyncSemaphore requestRateLimitSemaphore = new AsyncSemaphore(0);
      private readonly DeviceAgent[] agents;
      private readonly int agentIndex;
      private readonly SimulationBluetoothState bluetoothState;
      public const int MAX_RATE_LIMIT_TOKENS = 3;
      private float rateLimitTokenGrantingCounter = 0.0f;
      private readonly Dictionary<Guid, SimulationBluetoothNeighbor> neighborsByAdapterId;

      public unsafe SimulationBluetoothAdapter(DeviceAgent[] agents, int agentIndex, Dictionary<Guid, SimulationBluetoothNeighbor> neighborsByAdapterId) {
         this.agents = agents;
         this.agentIndex = agentIndex;
         this.bluetoothState = agents[agentIndex].BluetoothState;
         this.neighborsByAdapterId = neighborsByAdapterId;
      }

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
         await requestRateLimitSemaphore.WaitAsync();
         var neighbors = new List<IBluetoothNeighbor>();
         for (int i = 0; i < agents.Length; i++) {
            if (bluetoothState.ConnectionStates[i].Connectedness == 1.0f) {
               neighbors.Add(neighborsByAdapterId[agents[i].BluetoothAdapterId]);
            }
         }
         return neighbors;
      }

      public void Inject(Guid senderId, byte[] contents) {

      }

      public class SimulationBluetoothNeighbor : IBluetoothNeighbor {
         private readonly SimulationConnectionContext connectionContext;

         public SimulationBluetoothNeighbor(SimulationConnectionContext connectionContext) {
            this.connectionContext = connectionContext;
         }

         public Task HandshakeAsync() {
            return connectionContext.ConnectAsync(CancellationToken.None);
         }
      }

      public class SimulationConnectionContext {
         private readonly AsyncLock synchronization = new AsyncLock();
         private readonly DeviceAgent firstAgent; 
         private readonly DeviceAgent secondAgent;

         // state
         private bool isFirstConnected = false;
         private bool isSecondConnected = false;

         // connect: 
         private bool isConnectingPeerPending;
         private AsyncLatch connectingPeerSignal;

         public SimulationConnectionContext(DeviceAgent firstAgent, DeviceAgent secondAgent) {
            this.firstAgent = firstAgent;
            this.secondAgent = secondAgent;
         }

         public async Task ConnectAsync(CancellationToken cancellationToken) {
            using (var guard = await synchronization.LockAsync(cancellationToken)) {
               if (isConnectingPeerPending) {
                  // simulate handshake delay
                  var connectivity = SimulationBluetoothCalculator.ComputeConnectivity(firstAgent, secondAgent);
                  if (!connectivity.IsSufficientQuality) {
                     await Task.Delay(SimulationBluetoothConstants.HANDSHAKE_TIMEOUT_MILLIS);
                     throw new TimeoutException();
                  }
                  await Task.Delay(SimulationBluetoothCalculator.ComputeHandshakeDelay(connectivity.SignalQuality), cancellationToken);

                  isFirstConnected = true;

                  connectingPeerSignal.Set();
               } else {
                  isConnectingPeerPending = true;
                  connectingPeerSignal = new AsyncLatch();

                  guard.Free();

                  await connectingPeerSignal.WaitAsync(cancellationToken);

                  isSecondConnected = true;
               }
            }
         }

         public async Task SendAsync(DeviceAgent sender, byte[] contents, CancellationToken cancellationToken) {
            using (await synchronization.LockAsync(cancellationToken)) {
               await AssertConnectedElseTimeout(sender);
               GetOther(sender).BluetoothAdapter.Inject(sender.BluetoothAdapterId, contents);
            }
         }

         private DeviceAgent GetOther(DeviceAgent self) => self == firstAgent ? secondAgent : firstAgent;

         private async Task AssertConnectedElseTimeout(DeviceAgent sender) {
            if (sender == firstAgent) {
               if (!isFirstConnected) {
                  throw new InvalidOperationException("not connected");
               } else if (!SimulationBluetoothCalculator.ComputeConnectivity(firstAgent, secondAgent).IsSufficientQuality) {
                  await Task.Delay(SimulationBluetoothConstants.SENDRECV_TIMEOUT_MILLIS);
                  isFirstConnected = false;
                  throw new TimeoutException();
               }
            } else {
               if (!isSecondConnected) {
                  throw new InvalidOperationException("not connected");
               } else if (!SimulationBluetoothCalculator.ComputeConnectivity(firstAgent, secondAgent).IsSufficientQuality) {
                  await Task.Delay(SimulationBluetoothConstants.SENDRECV_TIMEOUT_MILLIS);
                  isSecondConnected = false;
                  throw new TimeoutException();
               }
            }
         }
      }
   }

   public class CampfireNetClient {
      private readonly IBluetoothAdapter bluetoothAdapter;

      public CampfireNetClient(IBluetoothAdapter bluetoothAdapter) {
         this.bluetoothAdapter = bluetoothAdapter;
      }

      public async Task RunAsync() {
         var connectedNeighbors = new HashSet<IBluetoothNeighbor>();
         while (true) {
            var neighbors = await bluetoothAdapter.DiscoverAsync();
            foreach (var neighbor in neighbors) {
               if (!connectedNeighbors.Contains(neighbor)) {
                  await neighbor.HandshakeAsync();
                  connectedNeighbors.Add(neighbor);
                  Console.WriteLine("Handshook!");
               }
            }
         }
      }
   }

   public static class SimulationBluetoothConstants {
      public const int RANGE = 100;
      public const int RANGE_SQUARED = RANGE * RANGE;

      public const int BASE_HANDSHAKE_DELAY_MILLIS = 300;
      public const float MIN_VIABLE_SIGNAL_QUALITY = 0.2f;

      public const int HANDSHAKE_TIMEOUT_MILLIS = 5000;
      public const int SENDRECV_TIMEOUT_MILLIS = 2000;
   }

   public static class SimulationBluetoothCalculator {
      public static SimulationBluetoothConnectivity ComputeConnectivity(DeviceAgent a, DeviceAgent b) {
         var quality = 1.0 - (a.Position - b.Position).LengthSquared() / SimulationBluetoothConstants.RANGE_SQUARED;
         return new SimulationBluetoothConnectivity {
            InRange = quality > 0.0,
            IsSufficientQuality = quality > SimulationBluetoothConstants.MIN_VIABLE_SIGNAL_QUALITY,
            SignalQuality = (float)quality
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
      private const int SCALE = 2;
      private const int NUM_AGENTS = 112 * SCALE * SCALE;
      private const int DISPLAY_WIDTH = 1920;
      private const int DISPLAY_HEIGHT = 1080;
      private const int FIELD_WIDTH = 1280 * SCALE;
      private const int FIELD_HEIGHT = 720 * SCALE;
      private const int AGENT_RADIUS = 10;
      private const float MAX_VALUE = 1.0f;
      private readonly GraphicsDeviceManager graphicsDeviceManager;
      private DeviceAgent[] agents;
      private SpriteBatch spriteBatch;
      private Texture2D whiteTexture;
      private Texture2D whiteCircleTexture;
      private RasterizerState rasterizerState;

      public SimulatorGame() {
         graphicsDeviceManager = new GraphicsDeviceManager(this) {
            PreferredBackBufferWidth = DISPLAY_WIDTH,
            PreferredBackBufferHeight = DISPLAY_HEIGHT,
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

         var random = new Random(2);
         agents = new DeviceAgent[NUM_AGENTS];
         for (int i = 0 ; i < NUM_AGENTS; i++) {
            agents[i] = new DeviceAgent {
               BluetoothAdapterId = Guid.NewGuid(),
               Position = new Vector2(
                  random.Next(AGENT_RADIUS, FIELD_WIDTH - AGENT_RADIUS),
                  random.Next(AGENT_RADIUS, FIELD_HEIGHT - AGENT_RADIUS)
               ),
               Velocity = Vector2.Transform(new Vector2(10, 0), Matrix.CreateRotationZ((float)(random.NextDouble() * Math.PI * 2))),
               BluetoothState = new SimulationBluetoothState {
                  ConnectionStates = Enumerable.Range(0, NUM_AGENTS).Select(x => new SimulationBluetoothConnectionState()).ToArray()
               }
            };
         }

         var agentIndexToNeighborsByAdapterId = Enumerable.Range(0, agents.Length).ToDictionary(
            i => i,
            i => new Dictionary<Guid, SimulationBluetoothAdapter.SimulationBluetoothNeighbor>());
         for (var i = 0; i < agents.Length - 1; i++) {
            for (var j = i + 1; j < agents.Length; j++) {
               var connectionContext = new SimulationBluetoothAdapter.SimulationConnectionContext(agents[i], agents[j]);
               agentIndexToNeighborsByAdapterId[i].Add(agents[j].BluetoothAdapterId, new SimulationBluetoothAdapter.SimulationBluetoothNeighbor(connectionContext));
               agentIndexToNeighborsByAdapterId[j].Add(agents[i].BluetoothAdapterId, new SimulationBluetoothAdapter.SimulationBluetoothNeighbor(connectionContext));
            }
         }

         for (int i = 0; i < agents.Length; i++) {
            var bluetoothAdapter = agents[i].BluetoothAdapter = new SimulationBluetoothAdapter(agents, i, agentIndexToNeighborsByAdapterId[i]);
            agents[i].BluetoothAdapter.Permit(SimulationBluetoothAdapter.MAX_RATE_LIMIT_TOKENS * (float)random.NextDouble());

            var client = new CampfireNetClient(bluetoothAdapter);
            client.RunAsync().ContinueWith(task => {
               if (task.IsFaulted) {
                  Console.WriteLine(task.Exception);
               }
            });
         }

         agents[0].Value = MAX_VALUE;
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
            if (agent.Position.X < AGENT_RADIUS)
               agent.Velocity.X = Math.Abs(agent.Velocity.X);
            if (agent.Position.X > FIELD_WIDTH - AGENT_RADIUS)
               agent.Velocity.X = -Math.Abs(agent.Velocity.X);
            if (agent.Position.Y < AGENT_RADIUS)
               agent.Velocity.Y = Math.Abs(agent.Velocity.Y);
            if (agent.Position.Y > FIELD_HEIGHT - AGENT_RADIUS)
               agent.Velocity.Y = -Math.Abs(agent.Velocity.Y);
            agent.BluetoothAdapter.Permit(dt);
         }

         var dConnectnessInRangeBase = dt * 5.0f;
         var dConnectnessOutOfRangeBase = -dt * 50.0f;
         for (var i = 0; i < agents.Length - 1; i++) {
//         Parallel.For(0, agents.Length - 1, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => {
            var a = agents[i];
            var aConnectionStates = a.BluetoothState.ConnectionStates;
            for (var j = i + 1; j < agents.Length; j++) {
               var b = agents[j];
               var bConnectionStates = b.BluetoothState.ConnectionStates;
               var distanceSquared = (a.Position - b.Position).LengthSquared();
               var quality = 1.0f - distanceSquared / (float)SimulationBluetoothConstants.RANGE_SQUARED; // Math.Max(0.0f, 1.0f - distanceSquared / (float)BLUETOOTH_RANGE_SQUARED);
               var inRange = distanceSquared < SimulationBluetoothConstants.RANGE_SQUARED;
               float connectedness = aConnectionStates[j].Connectedness;
               var dConnectedness = (inRange ? quality * dConnectnessInRangeBase : dConnectnessOutOfRangeBase);
               connectedness = Math.Max(0.0f, Math.Min(1.0f, connectedness + dConnectedness));

               aConnectionStates[j].Quality = bConnectionStates[i].Quality = quality;
               aConnectionStates[j].Connectedness = bConnectionStates[i].Connectedness = connectedness;
               
               if (connectedness == 1.0f) {
                  if (a.Value < MAX_VALUE && b.Value >= MAX_VALUE) {
                     a.Value += quality * dt;
                  } else if (b.Value < MAX_VALUE && a.Value >= MAX_VALUE) {
                     b.Value += quality * dt;
                  }
               }
            }
//         });
         }

         if (Keyboard.GetState().IsKeyDown(Keys.A)) {
            for (var i = 0; i < agents.Length; i++) {
               agents[i].Value = 0;
            }
            agents[(int)(DateTime.Now.ToFileTime() % agents.Length)].Value = 50;
         }
      }

      protected override void Draw(GameTime gameTime) {
         base.Draw(gameTime);

         Console.WriteLine(gameTime.ElapsedGameTime.TotalSeconds);

         GraphicsDevice.Clear(Color.White);
         spriteBatch.Begin(SpriteSortMode.Deferred, null, transformMatrix: Matrix.CreateScale((float)DISPLAY_WIDTH / FIELD_WIDTH));

         for (var i = 0; i < agents.Length - 1; i++) {
            var a = agents[i];
            for (var j = i + 1; j < agents.Length; j++) {
               var b = agents[j];
               if (a.BluetoothState.ConnectionStates[j].Connectedness == 1.0f) {
                  spriteBatch.DrawLine(a.Position, b.Position, Color.Gray);
               }
            }
         }

         for (var i = 0; i < agents.Length; i++) {
            DrawCenteredCircleWorld(agents[i].Position, AGENT_RADIUS, agents[i].Value < MAX_VALUE ? Color.Gray : Color.Red);
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
