// Author: Tigran Gasparian
// Source: http://blog.tigrangasparian.com
// License: In short, the author is not responsible for anything. You can do whatever you want with this code.
//          It would be nice if you kept the above credits though :)

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CampfireNet.Simulator
{
   /// <summary>
   /// Contains extension methods of the spritebatch class to draw lines.
   /// </summary>
   static class SpriteBatchEx
   {
      /// <summary>
      /// Draws a single line. 
      /// Require SpriteBatch.Begin() and SpriteBatch.End()
      /// </summary>
      /// <param name="begin">Begin point.</param>
      /// <param name="end">End point.</param>
      /// <param name="color">The color.</param>
      /// <param name="width">The width.</param>
      public static void DrawLine(this SpriteBatch spriteBatch, Vector2 begin, Vector2 end, Color color, int width = 1)
      {
         Rectangle r = new Rectangle((int)begin.X, (int)begin.Y, (int)(end - begin).Length() + width, width);
         Vector2 v = Vector2.Normalize(begin - end);
         float angle = (float)Math.Acos(Vector2.Dot(v, -Vector2.UnitX));
         if (begin.Y > end.Y) angle = MathHelper.TwoPi - angle;
         spriteBatch.Draw(TexGen.White, r, null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
      }

      /// <summary>
      /// Draws a single line. 
      /// Doesn't require SpriteBatch.Begin() or SpriteBatch.End()
      /// </summary>
      /// <param name="begin">Begin point.</param>
      /// <param name="end">End point.</param>
      /// <param name="color">The color.</param>
      /// <param name="width">The width.</param>
      public static void DrawSingleLine(this SpriteBatch spriteBatch, Vector2 begin, Vector2 end, Color color, int width = 1)
      {
         spriteBatch.Begin();
         spriteBatch.DrawLine(begin, end, color, width);
         spriteBatch.End();
      }

      /// <summary>
      /// Draws a poly line.
      /// Doesn't require SpriteBatch.Begin() or SpriteBatch.End()
      /// <param name="points">The points.</param>
      /// <param name="color">The color.</param>
      /// <param name="width">The width.</param>
      /// <param name="closed">Whether the shape should be closed.</param>
      public static void DrawPolyLine(this SpriteBatch spriteBatch, Vector2[] points, Color color, int width = 1, bool closed = false)
      {
         spriteBatch.Begin();
         for (int i = 0; i < points.Length - 1; i++)
            spriteBatch.DrawLine(points[i], points[i + 1], color, width);
         if (closed)
            spriteBatch.DrawLine(points[points.Length - 1], points[0], color, width);
         spriteBatch.End();
      }

      /// <summary>
      /// The graphics device, set this before drawing lines
      /// </summary>
      public static GraphicsDevice GraphicsDevice;

      /// <summary>
      /// Generates a 1 pixel white texture used to draw lines.
      /// </summary>
      static class TexGen
      {
         static Texture2D white = null;
         /// <summary>
         /// Returns a single pixel white texture, if it doesn't exist, it creates one
         /// </summary>
         /// <exception cref="System.Exception">Please set the SpriteBatchEx.GraphicsDevice to your graphicsdevice before drawing lines.</exception>
         public static Texture2D White
         {
            get
            {
               if (white == null)
               {
                  if (SpriteBatchEx.GraphicsDevice == null)
                     throw new Exception("Please set the SpriteBatchEx.GraphicsDevice to your GraphicsDevice before drawing lines.");
                  white = new Texture2D(SpriteBatchEx.GraphicsDevice, 1, 1);
                  Color[] color = new Color[1];
                  color[0] = Color.White;
                  white.SetData<Color>(color);
               }
               return white;
            }
         }
      }
   }
}