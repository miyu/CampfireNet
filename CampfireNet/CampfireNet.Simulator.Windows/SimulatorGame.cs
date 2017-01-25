using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace CampfireNet.Simulator {
   public struct SimulationBluetoothConnectionState {
      public float Quality { get; set; }
      public float Connectedness { get; set; }
   }

   public class SimulationBluetoothState {
      public SimulationBluetoothConnectionState[] ConnectionStates { get; set; }
   }

   public class DeviceAgent {
      public Vector2 Position { get; set; }
      public Vector2 Velocity { get; set; }
      public float Value { get; set; }
      public SimulationBluetoothState BluetoothState { get; set; }
   }

   public class SimulatorGame : Game {
      private const int AGENT_RADIUS = 10;
      private const int BLUETOOTH_RANGE = 100;
      private const int BLUETOOTH_RANGE_SQUARED = BLUETOOTH_RANGE * BLUETOOTH_RANGE;
      private const float MAX_VALUE = 1.0f;
      private readonly Size fieldDimensions = new Size(1280, 720); 
      private readonly GraphicsDeviceManager graphicsDeviceManager;
      private readonly List<DeviceAgent> agents = new List<DeviceAgent>();
      private SpriteBatch spriteBatch;
      private Texture2D whiteTexture;
      private Texture2D whiteCircleTexture;

      public SimulatorGame() {
         graphicsDeviceManager = new GraphicsDeviceManager(this) {
            PreferredBackBufferWidth = fieldDimensions.Width,
            PreferredBackBufferHeight = fieldDimensions.Height
         };
      }

      protected override void LoadContent() {
         base.LoadContent();

         spriteBatch = new SpriteBatch(graphicsDeviceManager.GraphicsDevice);
         SpriteBatchEx.GraphicsDevice = GraphicsDevice;

         whiteTexture = CreateSolidTexture(Color.White);
         whiteCircleTexture = CreateSolidCircleTexture(Color.White, 256);

         var random = new Random(2);
         const int numAgents = 112;
         for (int i = 0 ; i < numAgents; i++) {
            agents.Add(new DeviceAgent {
               Position = new Vector2(
                  random.Next(AGENT_RADIUS, fieldDimensions.Width - AGENT_RADIUS),
                  random.Next(AGENT_RADIUS, fieldDimensions.Height - AGENT_RADIUS)
               ),
               Velocity = Vector2.Transform(new Vector2(100, 0), Matrix.CreateRotationZ((float)(random.NextDouble() * Math.PI * 2))),
               BluetoothState = new SimulationBluetoothState {
                  ConnectionStates = Enumerable.Range(0, numAgents).Select(x => new SimulationBluetoothConnectionState()).ToArray()
               }
            });
         }
         //agents[0].Value = MAX_VALUE;
         for (int i = 0; i < agents.Count; i++) {
            agents[i].Position = new Vector2(320 + 50 * (i % 14), 80 + 70 * i / 14);
            agents[i].Velocity *= 0.02f;
         }
         agents[36].Value = MAX_VALUE;
      }

      protected override void Update(GameTime gameTime) {
         base.Update(gameTime);

         foreach (var agent in agents) {
            agent.Position += agent.Velocity * (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (agent.Position.X < AGENT_RADIUS)
               agent.Velocity = new Vector2(Math.Abs(agent.Velocity.X), agent.Velocity.Y);
            if (agent.Position.X > fieldDimensions.Width - AGENT_RADIUS)
               agent.Velocity = new Vector2(-Math.Abs(agent.Velocity.X), agent.Velocity.Y);
            if (agent.Position.Y < AGENT_RADIUS)
               agent.Velocity = new Vector2(agent.Velocity.X, Math.Abs(agent.Velocity.Y));
            if (agent.Position.Y > fieldDimensions.Height - AGENT_RADIUS)
               agent.Velocity = new Vector2(agent.Velocity.X, -Math.Abs(agent.Velocity.Y));
         }

         for (var i = 0; i < agents.Count - 1; i++) {
            var a = agents[i];
            for (var j = i + 1; j < agents.Count; j++) {
               var b = agents[j];
               var distanceSquared = (a.Position - b.Position).LengthSquared();
               var quality = 1.0f - distanceSquared / BLUETOOTH_RANGE_SQUARED;
               var inRange = distanceSquared < BLUETOOTH_RANGE_SQUARED;
               float connectedness = a.BluetoothState.ConnectionStates[j].Connectedness;
               if (inRange) {
                  a.BluetoothState.ConnectionStates[j].Quality = quality;
                  b.BluetoothState.ConnectionStates[i].Quality = quality;

                  connectedness = Math.Min(1.0f, connectedness + quality * (float)gameTime.ElapsedGameTime.TotalSeconds * 5.0f);
                  a.BluetoothState.ConnectionStates[j].Connectedness = connectedness;
                  b.BluetoothState.ConnectionStates[i].Connectedness = connectedness;
               } else {
                  a.BluetoothState.ConnectionStates[j].Quality = 0;
                  b.BluetoothState.ConnectionStates[i].Quality = 0;

                  connectedness = Math.Max(0.0f, connectedness - (float)gameTime.ElapsedGameTime.TotalSeconds * 50.0f);
                  a.BluetoothState.ConnectionStates[j].Connectedness = connectedness;
                  b.BluetoothState.ConnectionStates[i].Connectedness = connectedness;
               }
               if (Math.Abs(connectedness - 1.0f) < float.Epsilon) {
                  if (a.Value < MAX_VALUE && b.Value >= MAX_VALUE) {
                     a.Value += quality * (float)gameTime.ElapsedGameTime.TotalSeconds;
                  } else if (b.Value < MAX_VALUE && a.Value >= MAX_VALUE) {
                     b.Value += quality * (float)gameTime.ElapsedGameTime.TotalSeconds;
                  }
               }
            }
         }
         if (Keyboard.GetState().IsKeyDown(Keys.A)) {
            foreach (var agent in agents) {
               agent.Value = 0;
            }
            agents[(int)(DateTime.Now.ToFileTime() % agents.Count)].Value = 50;
         }
      }

      protected override void Draw(GameTime gameTime) {
         base.Draw(gameTime);

         GraphicsDevice.Clear(Color.White);
         spriteBatch.Begin();

         for (var i = 0; i < agents.Count - 1; i++) {
            var a = agents[i];
            for (var j = i + 1; j < agents.Count; j++) {
               var b = agents[j];
               if (Math.Abs(a.BluetoothState.ConnectionStates[j].Connectedness - 1.0f) < float.Epsilon) {
                  spriteBatch.DrawLine(a.Position, b.Position, Color.Gray);
               }
            }
         }

         foreach (var agent in agents) {
            DrawCenteredCircleWorld(agent.Position, AGENT_RADIUS, agent.Value < MAX_VALUE ? Color.Gray : Color.Red);
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
