using System;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace CampfireNet.Simulator {
   public class DeviceAgent {
      public Vector2 Position { get; set; }
      public Vector2 Velocity { get; set; }
      public int Value { get; set; }
   }

   public class SimulatorGame : Game {
      private const int AGENT_RADIUS = 10;
      private const int BLUETOOTH_RANGE = 100;
      private const int BLUETOOTH_RANGE_SQUARED = BLUETOOTH_RANGE * BLUETOOTH_RANGE;
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
         for (int i = 0 ; i < 100; i++) {
            agents.Add(new DeviceAgent {
               Position = new Vector2(
                  random.Next(AGENT_RADIUS, fieldDimensions.Width - AGENT_RADIUS),
                  random.Next(AGENT_RADIUS, fieldDimensions.Height - AGENT_RADIUS)
               ),
               Velocity = Vector2.Transform(new Vector2(100, 0), Matrix.CreateRotationZ((float)(random.NextDouble() * Math.PI * 2)))
            });
         }
         //agents[0].Value = 50;
         for (int i = 0; i < agents.Count; i++) {
            agents[i].Position = new Vector2(350 + 50 * (i % 10), 80 + 50 * i / 10);
            //agents[i].Velocity = new Vector2(0, 0);
         }
         agents[38].Value = 50;
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
      }

      protected override void Draw(GameTime gameTime) {
         base.Draw(gameTime);

         GraphicsDevice.Clear(Color.Black);
         spriteBatch.Begin();

         foreach (var a in agents) {
            foreach (var b in agents) {
               if ((a.Position - b.Position).LengthSquared() < BLUETOOTH_RANGE_SQUARED) {
                  spriteBatch.DrawLine(a.Position, b.Position, Color.Gray);
                  if (a.Value < 50 && b.Value == 50) {
                     a.Value++;
                  }
               }
            }
         }
         foreach (var agent in agents) {
            DrawCenteredCircleWorld(agent.Position, AGENT_RADIUS, agent.Value == 0 ? Color.White : Color.Lime);
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
