using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.View.Track;

namespace Orts.View.DrawableComponents
{
    public class ScaleRulerComponent : DrawableGameComponent
    {
        private Vector2 position;
        private Vector2 offset;
        private ContentArea content;

        private readonly SpriteBatch spriteBatch;
        private Color color;
        private Texture2D texture;
        private readonly System.Drawing.Font font;

        private static readonly float[] imperialRulerData = new float[] { 0f, 0.9144f, 1.8288f, 4.572f, 9.144f, 18.288f, 45.72f, 91.44f, 182.88f, 356.76f, 731.52f, 1609.344f, 3218.688f, 8046.72f, 16093.44f, 32186.88f, 80467.2f };
        private const int markerLength = 3;

        private readonly System.Drawing.Brush fontBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
        private readonly System.Drawing.Pen rulerPen = new System.Drawing.Pen(System.Drawing.Color.Gray, 1.5f);

        private readonly Dictionary<string, Texture2D> rulerTextures = new Dictionary<string, Texture2D>();

        private enum MetricRuler
        {
            [Description("0m")] m0_0 = 0,
            [Description("1m")] m0_1 = 1,
            [Description("2m")] m0_2 = 2,
            [Description("5m")] m0_5 = 5,
            [Description("10m")] m0_10 = 10,
            [Description("20m")] m0_20 = 20,
            [Description("50m")] m0_50 = 50,
            [Description("100m")] m0_100 = 100,
            [Description("200m")] m0_200 = 200,
            [Description("500m")] m0_500 = 500,
            [Description("1km")] m1_000 = 1000,
            [Description("2km")] m2_000 = 2000,
            [Description("5km")] m5_500 = 5000,
            [Description("10km")] m10_000 = 10000,
            [Description("20km")] m20_000 = 20000,
            [Description("50km")] m50_000 = 50000,
            [Description("100km")] m100_000 = 100000,
        }

        private enum ImperialRuler
        {
            [Description("0yd")] i0_0 = 0,
            [Description("1yd")] i0_1 = 1,
            [Description("2yd")] i0_2 = 2,
            [Description("5yd")] i0_5 = 3,
            [Description("10yd")] i0_10 = 4,
            [Description("20yd")] i0_20 = 5,
            [Description("50yd")] i0_50 = 6,
            [Description("100yd")] i0_100 = 7,
            [Description("200yd")] i0_200 = 8,
            [Description("400yd")] i0_400 = 9,
            [Description("800yd")] i0_800 = 10,
            [Description("1mi")] i1_000 = 11,
            [Description("2mi")] i2_500 = 12,
            [Description("5mi")] i5_000 = 13,
            [Description("10mi")] i10_000 = 14,
            [Description("20mi")] i20_000 = 15,
            [Description("50mi")] i50_000 = 16,
        }

        public ScaleRulerComponent(Game game, SpriteBatch spriteBatch, System.Drawing.Font font, Color color, Vector2 position) :
            base(game)
        {
            Enabled = false;
            Visible = false;

            this.spriteBatch = spriteBatch;
            this.color = color;
            this.position = position;
            this.font = font;
            if (position.X < 0 || position.Y < 0)
            {
                offset = position;
#pragma warning disable CA1062 // Validate arguments of public methods
                game.Window.ClientSizeChanged += Window_ClientSizeChanged;
#pragma warning restore CA1062 // Validate arguments of public methods
                Window_ClientSizeChanged(this, EventArgs.Empty);
            }
        }

        public void Enable(ContentArea content)
        {
            this.content = content;
            Enabled = true;
            Visible = true;
        }

        public void Disable()
        {
            Enabled = false;
            Visible = false;
            content = null;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            position = new Vector2(offset.X > 0 ? offset.X : Game.Window.ClientBounds.Width + offset.X, offset.Y > 0 ? offset.Y : Game.Window.ClientBounds.Height + offset.Y);
        }

        public override void Update(GameTime gameTime)
        {
            Point windowSize = content.WindowSize;

            //max size (length) of the ruler. if less than 50px available, don't draw
            int maxLength = Math.Min(200, windowSize.X - (int)position.X * 2);
            if (maxLength < 50)
                return;

            MetricRuler metricRuler = MetricRuler.m100_000;
            ImperialRuler imperialRuler = ImperialRuler.i50_000;
            int rulerLength;
            if (content.TrackContent.UseMetricUnits)
            {
                while ((int)metricRuler * content.Scale > maxLength && metricRuler != MetricRuler.m0_0)
                {
                    metricRuler = EnumExtension.Previous(metricRuler);
                }
                rulerLength = (int)((int)metricRuler * content.Scale);
                string key = $"{metricRuler.GetDescription()}::{rulerLength}";
                if (!rulerTextures.TryGetValue(key, out texture))
                {
                    texture = DrawRulerTexture(rulerLength, MetricRuler.m0_0.GetDescription(), metricRuler.GetDescription());
                    rulerTextures.Add(key, texture);
                }
            }
            else
            {
                while (imperialRulerData[(int)imperialRuler] * content.Scale > maxLength && imperialRuler != ImperialRuler.i0_0)
                {
                    imperialRuler = EnumExtension.Previous(imperialRuler);
                }
                rulerLength = (int)(imperialRulerData[(int)imperialRuler] * content.Scale);
                string key = $"{imperialRuler.GetDescription()}::{rulerLength}";
                if (!rulerTextures.TryGetValue(key, out texture))
                {
                    texture = DrawRulerTexture(rulerLength, ImperialRuler.i0_0.GetDescription(), imperialRuler.GetDescription());
                    rulerTextures.Add(key, texture);
                }
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            spriteBatch.Begin();
            spriteBatch.Draw(texture, position, null, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);

            base.Draw(gameTime);
            spriteBatch.End();
        }

        private Texture2D DrawRulerTexture(int rulerLength, string startMarker, string endMarker)
        {
            const int overSize = 24;
            const int padding = overSize / 2;
            using (System.Drawing.Bitmap bmpSurface = new System.Drawing.Bitmap(rulerLength + overSize, (int)font.GetHeight() + markerLength * 2))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmpSurface))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    g.DrawLine(rulerPen, padding, markerLength, padding + rulerLength, markerLength);
                    g.DrawLine(rulerPen, padding, 0, padding, markerLength * 2);
                    g.DrawLine(rulerPen, padding + rulerLength, 0, padding + rulerLength, markerLength * 2);
                    g.DrawLine(rulerPen, padding + rulerLength * 0.5f, 0, padding + rulerLength * 0.5f, markerLength * 2);
                    //g.DrawLine(rulerPen, padding + rulerLength * 0.25f, 0, padding + rulerLength * 0.25f, markerLength);
                    //g.DrawLine(rulerPen, padding + rulerLength * 0.75f, 0, padding + rulerLength * 0.75f, markerLength);

                    System.Drawing.SizeF textSize = g.MeasureString(startMarker, font);
                    g.DrawString(startMarker, font, fontBrush, 12 - textSize.Width / 2, markerLength * 2);
                    textSize = g.MeasureString(endMarker, font);
                    g.DrawString(endMarker, font, fontBrush, padding + rulerLength - textSize.Width / 2, markerLength * 2);

                    System.Drawing.Imaging.BitmapData bmd = bmpSurface.LockBits(new System.Drawing.Rectangle(0, 0, bmpSurface.Width, bmpSurface.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    int bufferSize = bmd.Height * bmd.Stride;
                    //create data buffer 
                    byte[] bytes = new byte[bufferSize];
                    // copy bitmap data into buffer
                    Marshal.Copy(bmd.Scan0, bytes, 0, bytes.Length);

                    // copy our buffer to the texture
                    Texture2D texture = new Texture2D(Game.GraphicsDevice, bmpSurface.Width, bmpSurface.Height, false, SurfaceFormat.Bgra32);
                    texture.SetData(bytes);
                    // unlock the bitmap data
                    bmpSurface.UnlockBits(bmd);

                    return texture;
                }
            }
        }

    }
}
