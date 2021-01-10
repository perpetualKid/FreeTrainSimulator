using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.View.Track;
using Orts.View.Track.Shapes;
using Orts.View.Track.Widgets;

using SharpDX.WIC;

namespace Orts.View.DrawableComponents
{
    public class InsetComponent : DrawableGameComponent
    {
        private Vector2 position;
        private Vector2 offset;
        private ContentArea content;
        private double scale;
        private double offsetX, offsetY;
        private Point size;

        private readonly SpriteBatch spriteBatch;
        private Color color;
        private Texture2D texture;

        public InsetComponent(Game game, Color color, Vector2 position) :
            base(game)
        {
            Enabled = false;
            Visible = false;

            spriteBatch = new SpriteBatch(game?.GraphicsDevice);
            this.color = color;
            this.position = position;
            size = new Point(game.GraphicsDevice.DisplayMode.Width / 15, game.GraphicsDevice.DisplayMode.Height / 15);
            if (position.X < 0 || position.Y < 0)
            {
                offset = position;
                game.Window.ClientSizeChanged += Window_ClientSizeChanged;
                Window_ClientSizeChanged(this, EventArgs.Empty);
            }
            DrawOrder = 99;
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
            texture = null;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            position = new Vector2(offset.X > 0 ? offset.X : Game.Window.ClientBounds.Width + offset.X - size.X, offset.Y > 0 ? offset.Y : Game.Window.ClientBounds.Height + offset.Y - size.Y);
        }

        public override void Update(GameTime gameTime)
        {
            if (texture == null)
                texture = DrawTrackInset();
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (texture == null)
                return;
            spriteBatch.Begin();
            spriteBatch.Draw(texture, position, null, Color.White, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            spriteBatch.End();
            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                spriteBatch?.Dispose();
                texture?.Dispose();
            }
            base.Dispose(disposing);
        }

        private Texture2D DrawTrackInset()
        {
            UpdateWindowSize();
            RenderTarget2D renderTarget = new RenderTarget2D(GraphicsDevice, size.X, size.Y);
            GraphicsDevice.SetRenderTarget(renderTarget);
            GraphicsDevice.Clear(Color.BurlyWood);
            spriteBatch.Begin();
            BasicShapes.DrawLine(1, color, Vector2.One, size.X - 1, 0, spriteBatch);
            BasicShapes.DrawLine(1, color, new Vector2(1, size.Y), size.X - 1, 0, spriteBatch);
            BasicShapes.DrawLine(1, color, Vector2.One, size.Y, MathHelper.ToRadians(90), spriteBatch);
            BasicShapes.DrawLine(1, color, new Vector2(size.X, 1), size.Y, MathHelper.ToRadians(90), spriteBatch);

            foreach (TrackSegment segment in content.TrackContent.TrackSegments)
            {
                if (segment.Curved)
                    BasicShapes.DrawArc(WorldToScreenSize(segment.Width), Color.Black, WorldToScreenCoordinates(in segment.Location), WorldToScreenSize(segment.Length), segment.Direction, segment.Angle, 0, spriteBatch);
                else
                    BasicShapes.DrawLine(WorldToScreenSize(segment.Width), Color.Black, WorldToScreenCoordinates(in segment.Location), WorldToScreenSize(segment.Length), segment.Direction, spriteBatch);
            }

            spriteBatch.End();
            GraphicsDevice.SetRenderTarget(null);
            return renderTarget;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return new Vector2((float)(scale * (location.X - offsetX)),
                               (float)(size.Y - scale * (location.Y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * scale), minScreenSize);
        }

        private void UpdateWindowSize()
        {
            double xScale = (double)size.X / content.TrackContent.Bounds.Width;
            double yScale = (double)size.Y / content.TrackContent.Bounds.Height;
            scale = Math.Min(xScale, yScale);
            offsetX = (content.TrackContent.Bounds.Left + content.TrackContent.Bounds.Right) / 2 - size.X / 2 / scale;
            offsetY = (content.TrackContent.Bounds.Top + content.TrackContent.Bounds.Bottom) / 2 - size.Y / 2 / scale;

        }

    }
}
