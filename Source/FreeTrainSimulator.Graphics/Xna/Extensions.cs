
using System;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Xna
{
    public static class PointExtension
    {
        public static readonly Point EmptyPoint = new Point(-1, -1);

        public static Point ToPoint(this in (int X, int Y) source)
        {
            return new Point(source.X, source.Y);
        }

        public static (int X, int Y) FromPoint(this in Point point)
        {
            return (point.X, point.Y);
        }

        public static Point ToPoint(this int[] source)
        {
            if (source?.Length > 1)
                return new Point(source[0], source[1]);
            return Point.Zero;
        }

        public static Point ToPoint(this in System.Drawing.Size size)
        {
            return new Point(size.Width, size.Height);
        }

        public static int[] ToArray(this in Point source)
        {
            return new int[] { source.X, source.Y };
        }
    }

    public static class TextureExtension
    {
        public static void SaveAsPng(this Texture2D texture, string fileName)
        {
            ArgumentNullException.ThrowIfNull(texture);

            GraphicsDevice gpu = texture.GraphicsDevice;
            using (RenderTarget2D target = new RenderTarget2D(gpu, texture.Width, texture.Height))
            {
                gpu.SetRenderTarget(target);
                gpu.Clear(Color.Transparent); // set transparent background

                using (SpriteBatch batch = new SpriteBatch(gpu))
                {
                    batch.Begin();
                    batch.Draw(texture, Vector2.Zero, Color.White);
                    batch.End();
                }

                // save texture
                using (Stream stream = File.OpenWrite(fileName))
                {
                    target.SaveAsPng(stream, texture.Width, texture.Height);
                }

                gpu.SetRenderTarget(null);
            }
        }
    }
}
