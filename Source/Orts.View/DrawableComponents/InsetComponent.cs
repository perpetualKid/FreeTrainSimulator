using System;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.View.Track;
using Orts.View.Track.Shapes;
using Orts.View.Track.Widgets;
using Orts.View.Xna;

namespace Orts.View.DrawableComponents
{
    public class InsetComponent : TextureContentComponent
    {
        private ContentArea content;
        private double scale;
        private double offsetX, offsetY;
        private Point size;

        public InsetComponent(Game game, Color color, Vector2 position) :
            base(game, color, position)
        {
            Enabled = false;
            Visible = false;

            size = new Point(game.GraphicsDevice.DisplayMode.Width / 15, game.GraphicsDevice.DisplayMode.Height / 15);
            Window_ClientSizeChanged(this, EventArgs.Empty);
        }

        internal void Enable(ContentArea content)
        {
            this.content = content;
            DrawOrder = content.DrawOrder + 10;
            Enabled = true;
            Visible = true;
        }

        internal void Disable()
        {
            Enabled = false;
            Visible = false;
            content = null;
            texture = null;
        }

        private protected override void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            size = new Point(Game.Window.ClientBounds.Size.X / 15, Game.Window.ClientBounds.Size.Y / 15);
            Enabled = Visible = size.X > 10 && size.Y > 10 && content != null;
            if (texture != null && (size.X != texture.Width || size.Y != texture.Width))
            {
                Texture2D current = texture;
                texture = null;
                current.Dispose();
            }
            if (positionOffset.X < 0 || positionOffset.Y < 0)
                position = new Vector2(positionOffset.X > 0 ? positionOffset.X : Game.Window.ClientBounds.Width + positionOffset.X - size.X, positionOffset.Y > 0 ? positionOffset.Y : Game.Window.ClientBounds.Height + positionOffset.Y - size.Y);
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
            spriteBatch.Draw(texture, position, null, Color.White);
            DrawClippingMarker();
            spriteBatch.End();
            base.Draw(gameTime);
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

        private void DrawClippingMarker()
        {
            double width = content.BottomRightArea.X - content.TopLeftArea.X;
            double height = content.TopLeftArea.Y - content.BottomRightArea.Y;
            float screenWidth = WorldToScreenSize(width);
            float screenHeight = WorldToScreenSize(height);
            //if (screenHeight > size.Y * 0.95 || screenWidth > size.X * 0.95)
            //    return;
            Vector2 clippingPosition = WorldToScreenCoordinates(content.TopLeftArea) + position;
            BasicShapes.DrawLine(1f, Color.Red, clippingPosition, screenWidth, 0, spriteBatch);
            BasicShapes.DrawLine(1f, Color.Red, clippingPosition + new Vector2(0, screenHeight), screenWidth, 0, spriteBatch);
            BasicShapes.DrawLine(1f, Color.Red, clippingPosition, screenHeight, MathHelper.ToRadians(90), spriteBatch);
            BasicShapes.DrawLine(1f, Color.Red, clippingPosition + new Vector2(screenWidth, 0), screenHeight, MathHelper.ToRadians(90), spriteBatch);
            if (screenWidth < 10 || screenHeight < 10)
                BasicShapes.DrawTexture(BasicTextureType.Circle, clippingPosition + new Vector2(screenWidth, screenHeight) / 2, 0, -0.5f, Color.Red, false, false, false, spriteBatch);

        }
    }
}
