using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.DrawableComponents
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
            if (Texture != null && (size.X != Texture.Width || size.Y != Texture.Height))
            {
                ClearTexture(true);
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
            if (Enabled && Texture == null)
                SetTexture(DrawTrackInset());
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (Texture == null)
                return;
            renderHelper.DrawSpriteBatch(spriteBatch =>
            {
                spriteBatch.Draw(Texture, position, null, color);
                DrawClippingMarker();
            });
            base.Draw(gameTime);
        }

        private RenderTarget2D DrawTrackInset()
        {
            IMapInsetOverlayContext insetContext = content as IMapInsetOverlayContext;
            UpdateWindowSize(insetContext);
            RenderTarget2D renderTarget = renderHelper.CreateRenderTarget(size.X, size.Y);
            renderHelper.RenderToTarget(renderTarget, Color.White, spriteBatch =>
            {
                insetContext.DrawOverlayLine(borderSize, borderColor, new Vector2(borderSize, borderSize), size.X - borderSize - borderSize, 0, spriteBatch);
                insetContext.DrawOverlayLine(borderSize, borderColor, new Vector2(borderSize, size.Y - borderSize), size.X - borderSize - borderSize, 0, spriteBatch);
                insetContext.DrawOverlayLine(borderSize, borderColor, new Vector2(borderSize, borderSize), size.Y - borderSize - borderSize, MathHelper.ToRadians(90), spriteBatch);
                insetContext.DrawOverlayLine(borderSize, borderColor, new Vector2(size.X - borderSize, borderSize), size.Y - borderSize - borderSize, MathHelper.ToRadians(90), spriteBatch);

                if (null != trackSegments)
                {
                    foreach (TrackSegment segment in trackSegments)
                    {
                        if (segment.Curved)
                            insetContext.DrawOverlayArc(WorldToScreenSize(segment.Size), Color.Black, WorldToScreenCoordinates(in segment.Location), WorldToScreenSize(segment.Radius), segment.Direction, segment.Angle, spriteBatch);
                        else
                            insetContext.DrawOverlayLine(WorldToScreenSize(segment.Size), Color.Black, WorldToScreenCoordinates(in segment.Location), WorldToScreenSize(segment.Length), segment.Direction, spriteBatch);
                    }
                }
            });
            return renderTarget;
        }

        private void UpdateWindowSize(IMapInsetOverlayContext insetContext)
        {
            double xScale = (double)size.X / insetContext.ContentBounds.Width;
            double yScale = (double)size.Y / insetContext.ContentBounds.Height;
            scale = Math.Min(xScale, yScale);
            offsetX = ((insetContext.ContentBounds.Left + insetContext.ContentBounds.Right) / 2) - (size.X / 2 / scale);
            offsetY = ((insetContext.ContentBounds.Top + insetContext.ContentBounds.Bottom) / 2) - (size.Y / 2 / scale);
        }

        private void DrawClippingMarker()
        {
            IMapInsetOverlayContext insetContext = content as IMapInsetOverlayContext;
            double width = insetContext.BottomRightBound.X - insetContext.TopLeftBound.X;
            double height = insetContext.TopLeftBound.Y - insetContext.BottomRightBound.Y;
            float screenWidth = WorldToScreenSize(width);
            float screenHeight = WorldToScreenSize(height);
            Vector2 clippingPosition = WorldToScreenCoordinates(insetContext.TopLeftBound) + position;
            insetContext.DrawOverlayLine(1f, Color.Red, clippingPosition, screenWidth, 0, spriteBatch);
            insetContext.DrawOverlayLine(1f, Color.Red, clippingPosition + new Vector2(0, screenHeight), screenWidth, 0, spriteBatch);
            insetContext.DrawOverlayLine(1f, Color.Red, clippingPosition, screenHeight, MathHelper.ToRadians(90), spriteBatch);
            insetContext.DrawOverlayLine(1f, Color.Red, clippingPosition + new Vector2(screenWidth, 0), screenHeight, MathHelper.ToRadians(90), spriteBatch);
            if (screenWidth < 10 || screenHeight < 10)
                insetContext.DrawOverlayTexture(BasicTextureType.Circle, clippingPosition + (new Vector2(screenWidth, screenHeight) / 2), 0, -0.5f, Color.Red, spriteBatch);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return new Vector2((float)(scale * (location.X - offsetX)), (float)(size.Y - (scale * (location.Y - offsetY))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * scale), minScreenSize);
        }
    }
}
