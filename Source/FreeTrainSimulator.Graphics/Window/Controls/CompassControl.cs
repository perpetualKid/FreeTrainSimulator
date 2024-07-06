using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public class CompassControl : WindowControl
    {
        private const int markerScaling = 2;

        private readonly Point halfPoint;
        private readonly Texture2D compassTexture;

        private Rectangle clippingRectangle;
        private readonly int halfWidth;

        public int Heading { get; set; }

        public CompassControl(FormBase window, int width, int height) : base(window, 0, 0, width, height)
        {
            compassTexture = DrawCompassTexture();
            halfWidth = width / 2;
            halfPoint = new Point(halfWidth, 0);
        }

        internal override void Update(GameTime gameTime, bool shouldUpdate)
        {
            int northLocation = Heading % 360 * markerScaling;
            if (northLocation < Bounds.Width)
                northLocation += 360 * markerScaling;
            clippingRectangle = new Rectangle(northLocation - halfWidth, 0, Bounds.Width, compassTexture.Height);
            base.Update(gameTime, shouldUpdate);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            base.Draw(spriteBatch, offset);

            Window.Owner.BasicShapes.DrawLine(1, Color.Gold, (Bounds.Location + offset + halfPoint).ToVector2(), Bounds.Height, MathHelper.PiOver2, spriteBatch);
            spriteBatch.Draw(compassTexture, (Bounds.Location + offset).ToVector2(), clippingRectangle, Color.White);
        }

        private Texture2D DrawCompassTexture()
        {
            int width = (360 + Bounds.Width) * markerScaling;
            int height = Bounds.Height;
            int markerLength = Window.Owner.TextFontDefault.Height / 2;
            using (System.Drawing.Bitmap bmpSurface = new System.Drawing.Bitmap(width, height))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmpSurface))
                {
                    using (System.Drawing.Pen markerPen = new System.Drawing.Pen(System.Drawing.Color.White, 1f))
                    {
                        using (System.Drawing.Brush fontBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                        {
                            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                            for (int x = 0; x < width; x += 10)
                            {
                                if (x % 30 == 0)
                                {
                                    markerPen.Width = 2;
                                    g.DrawLine(markerPen, (x * markerScaling) - 1, markerLength * 2, (x * markerScaling) - 1, markerLength * 4);
                                    System.Drawing.SizeF textSize = g.MeasureString($"{x % 360}", Window.Owner.TextFontDefault);
                                    g.DrawString($"{x % 360}", Window.Owner.TextFontDefault, fontBrush, (x * markerScaling) - (textSize.Width / 2f), 0);
                                }
                                else
                                {
                                    markerPen.Width = 1;
                                    g.DrawLine(markerPen, x * markerScaling, markerLength * 3, x * markerScaling, (markerLength * 4) - 2);
                                }
                            }

                            System.Drawing.Imaging.BitmapData bmd = bmpSurface.LockBits(new System.Drawing.Rectangle(0, 0, bmpSurface.Width, bmpSurface.Height),
                                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            int bufferSize = bmd.Height * bmd.Stride;
                            //create data buffer 
                            byte[] bytes = new byte[bufferSize];
                            // copy bitmap data into buffer
                            Marshal.Copy(bmd.Scan0, bytes, 0, bytes.Length);

                            // copy our buffer to the texture
                            Texture2D texture = new Texture2D(Window.Owner.GraphicsDevice, bmpSurface.Width, bmpSurface.Height, false, SurfaceFormat.Color);
                            texture.SetData(bytes);
                            // unlock the bitmap data
                            bmpSurface.UnlockBits(bmd);

                            return texture;
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            compassTexture.Dispose();
            base.Dispose(disposing);
        }
    }
}
