using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.Graphics.MapView.Shapes;
using Orts.Graphics.MapView.Widgets;
using Orts.Graphics.Xna;

namespace Orts.Graphics.DrawableComponents
{
    public class InsetComponent : TextureContentComponent
    {
        private double scale;
        private double offsetX, offsetY;
        private Point size;
        private const int borderSize = 2;
        private Color borderColor;

        private IEnumerable<TrackSegment> trackSegments;

        public InsetComponent(Game game, Color color, Vector2 position) :
            base(game, color, position)
        {
            Enabled = false;
            Visible = false;

            Window_ClientSizeChanged(this, EventArgs.Empty);
            borderColor = color.HighlightColor(0.6);
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

        internal void SetTrackSegments(IEnumerable<TrackSegment> trackSegments) { this.trackSegments = trackSegments; }

        public override void UpdateColor(Color color)
        {
            base.UpdateColor(color);
            borderColor = color.HighlightColor(0.6);
        }

        public override void Update(GameTime gameTime)
        {
            if (Enabled && texture == null)
                texture = DrawTrackInset();
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (texture == null)
                return;
            spriteBatch.Begin();
            spriteBatch.Draw(texture, position, null, color);
            DrawClippingMarker();
            spriteBatch.End();
            base.Draw(gameTime);
        }

        private Texture2D DrawTrackInset()
        {
            UpdateWindowSize();
            RenderTarget2D renderTarget = new RenderTarget2D(GraphicsDevice, size.X, size.Y);
            GraphicsDevice.SetRenderTarget(renderTarget);
            GraphicsDevice.Clear(Color.White);
            spriteBatch.Begin();
            content.BasicShapes.DrawLine(borderSize, borderColor, new Vector2(borderSize, borderSize), size.X - borderSize - borderSize, 0, spriteBatch);
            content.BasicShapes.DrawLine(borderSize, borderColor, new Vector2(borderSize, size.Y - borderSize), size.X - borderSize - borderSize, 0, spriteBatch);
            content.BasicShapes.DrawLine(borderSize, borderColor, new Vector2(borderSize, borderSize), size.Y - borderSize - borderSize, MathHelper.ToRadians(90), spriteBatch);
            content.BasicShapes.DrawLine(borderSize, borderColor, new Vector2(size.X - borderSize, borderSize), size.Y - borderSize - borderSize, MathHelper.ToRadians(90), spriteBatch);

            if (null != trackSegments)
            {
                foreach (TrackSegment segment in trackSegments)
                {
                    if (segment.Curved)
                        content.BasicShapes.DrawArc(WorldToScreenSize(segment.Size), Color.Black, WorldToScreenCoordinates(in segment.Location), WorldToScreenSize(segment.Radius), segment.Direction, segment.Angle, spriteBatch);
                    else
                        content.BasicShapes.DrawLine(WorldToScreenSize(segment.Size), Color.Black, WorldToScreenCoordinates(in segment.Location), WorldToScreenSize(segment.Length), segment.Direction, spriteBatch);
                }
            }

            spriteBatch.End();
            GraphicsDevice.SetRenderTarget(null);
            return renderTarget;
        }

        private void UpdateWindowSize()
        {
            double xScale = (double)size.X / content.Content.Bounds.Width;
            double yScale = (double)size.Y / content.Content.Bounds.Height;
            scale = Math.Min(xScale, yScale);
            offsetX = (content.Content.Bounds.Left + content.Content.Bounds.Right) / 2 - size.X / 2 / scale;
            offsetY = (content.Content.Bounds.Top + content.Content.Bounds.Bottom) / 2 - size.Y / 2 / scale;
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
            content.BasicShapes.DrawLine(1f, Color.Red, clippingPosition, screenWidth, 0, spriteBatch);
            content.BasicShapes.DrawLine(1f, Color.Red, clippingPosition + new Vector2(0, screenHeight), screenWidth, 0, spriteBatch);
            content.BasicShapes.DrawLine(1f, Color.Red, clippingPosition, screenHeight, MathHelper.ToRadians(90), spriteBatch);
            content.BasicShapes.DrawLine(1f, Color.Red, clippingPosition + new Vector2(screenWidth, 0), screenHeight, MathHelper.ToRadians(90), spriteBatch);
            if (screenWidth < 10 || screenHeight < 10)
                content.BasicShapes.DrawTexture(BasicTextureType.Circle, clippingPosition + new Vector2(screenWidth, screenHeight) / 2, 0, -0.5f, Color.Red, spriteBatch);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return new Vector2((float)(scale * (location.X - offsetX)), (float)(size.Y - scale * (location.Y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * scale), minScreenSize);
        }
    }
}
