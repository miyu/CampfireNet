using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
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

   public struct DeviceAgent {
      public Vector2 Position;
      public Vector2 Velocity;
      public float Value;
      public SimulationBluetoothState BluetoothState;
   }

   public class SimulatorGame : Game {
      private const int SCALE = 3;
      private const int NUM_AGENTS = 112 * SCALE * SCALE;
      private const int DISPLAY_WIDTH = 1920;
      private const int DISPLAY_HEIGHT = 1080;
      private const int FIELD_WIDTH = 1280 * SCALE;
      private const int FIELD_HEIGHT = 720 * SCALE;
      private const int AGENT_RADIUS = 10;
      private const int BLUETOOTH_RANGE = 100;
      private const int BLUETOOTH_RANGE_SQUARED = BLUETOOTH_RANGE * BLUETOOTH_RANGE;
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

         for (var i = 0; i < agents.Length; i++) {
            ref DeviceAgent agent = ref agents[i];
            agent.Position += agent.Velocity * dt;
            if (agent.Position.X < AGENT_RADIUS)
               agent.Velocity.X = Math.Abs(agent.Velocity.X);
            if (agent.Position.X > FIELD_WIDTH - AGENT_RADIUS)
               agent.Velocity.X = -Math.Abs(agent.Velocity.X);
            if (agent.Position.Y < AGENT_RADIUS)
               agent.Velocity.Y = Math.Abs(agent.Velocity.Y);
            if (agent.Position.Y > FIELD_HEIGHT - AGENT_RADIUS)
               agent.Velocity.Y = -Math.Abs(agent.Velocity.Y);
         }

         var dConnectnessInRangeBase = dt * 5.0f;
         var dConnectnessOutOfRangeBase = -dt * 50.0f;
         for (var i = 0; i < agents.Length - 1; i++) {
//         Parallel.For(0, agents.Length - 1, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => {
            ref var a = ref agents[i];
            var aConnectionStates = a.BluetoothState.ConnectionStates;
            for (var j = i + 1; j < agents.Length; j++) {
               ref var b = ref agents[j];
               var distanceSquared = (a.Position - b.Position).LengthSquared();
               var quality = 1.0f - distanceSquared / (float)BLUETOOTH_RANGE_SQUARED; // Math.Max(0.0f, 1.0f - distanceSquared / (float)BLUETOOTH_RANGE_SQUARED);
               var inRange = distanceSquared < BLUETOOTH_RANGE_SQUARED;
               float connectedness = aConnectionStates[j].Connectedness;
               var dConnectedness = (inRange ? quality * dConnectnessInRangeBase : dConnectnessOutOfRangeBase);
               connectedness = Math.Max(0.0f, Math.Min(1.0f, connectedness + dConnectedness));

               aConnectionStates[j].Quality = quality;
               aConnectionStates[j].Connectedness = connectedness;
               
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
            ref var a = ref agents[i];
            for (var j = i + 1; j < agents.Length; j++) {
               ref var b = ref agents[j];
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
