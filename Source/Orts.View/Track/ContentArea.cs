using System;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.View.DrawableComponents;
using Orts.View.Track.Shapes;
using Orts.View.Track.Widgets;

namespace Orts.View.Track
{
    public class ContentArea : DrawableGameComponent
    {
        private Rectangle bounds;
        private double maxScale;

        public TrackContent TrackContent { get; }

        public double Scale { get; private set; }

        private double offsetX, offsetY;
        private PointD topLeftArea, bottomRightArea;

        public Point WindowSize { get; private set; }

        public Point WindowOffset { get; private set; }

        private readonly SpriteBatch spriteBatch;

        public ContentArea(Game game, TrackContent trackContent) :
            base(game)
        {
            TrackContent = trackContent ?? throw new ArgumentNullException(nameof(trackContent));
            bounds = trackContent.Bounds;
            spriteBatch = new SpriteBatch(GraphicsDevice);
            Game.Components.OfType<ScaleRulerComponent>().FirstOrDefault()?.Enable(this);
            Game.Components.OfType<InsetComponent>().FirstOrDefault()?.Enable(this);
        }

        protected override void OnEnabledChanged(object sender, EventArgs args)
        {
            if (Enabled)
            {
                Game.Components.OfType<ScaleRulerComponent>().FirstOrDefault()?.Enable(this);
                Game.Components.OfType<InsetComponent>().FirstOrDefault()?.Enable(this);
            }
            else
            {
                Game.Components.OfType<ScaleRulerComponent>().FirstOrDefault()?.Disable();
                Game.Components.OfType<InsetComponent>().FirstOrDefault()?.Disable();
            }
            base.OnEnabledChanged(sender, args);
        }

        public void ResetSize(in Point windowSize, in Point offset)
        {
            WindowSize = windowSize;
            WindowOffset = offset;
            ScaleToFit();
            CenterView();
            topLeftArea = ScreenToWorldCoordinates(Point.Zero);
            bottomRightArea = ScreenToWorldCoordinates(WindowSize);
        }

        public void UpdateSize(in Point windowSize)
        {
            WindowSize = windowSize;
            CenterAround(new PointD((topLeftArea.X + bottomRightArea.X) / 2, (topLeftArea.Y + bottomRightArea.Y) / 2));
            topLeftArea = ScreenToWorldCoordinates(Point.Zero);
            bottomRightArea = ScreenToWorldCoordinates(WindowSize);
        }

        public void UpdateScaleAt(in Vector2 scaleAt, int steps)
        {
            double scale = Scale * Math.Pow((steps > 0 ? 1 / 0.9 : (steps < 0 ? 0.9 : 1)), Math.Abs(steps));
            if (scale < maxScale || scale > 200)
                return;
            offsetX += scaleAt.X * (scale / Scale - 1.0) / scale;
            offsetY += (WindowSize.Y - WindowOffset.Y - scaleAt.Y) * (scale / Scale - 1.0) / scale;
            Scale = scale;

            topLeftArea = ScreenToWorldCoordinates(Point.Zero);
            bottomRightArea = ScreenToWorldCoordinates(WindowSize);
        }

        public void UpdatePosition(in Vector2 delta)
        {
            offsetX -= delta.X / Scale;
            offsetY += delta.Y / Scale;

            topLeftArea = ScreenToWorldCoordinates(Point.Zero);
            bottomRightArea = ScreenToWorldCoordinates(WindowSize);
        }

        private void CenterView()
        {
            offsetX = (bounds.Left + bounds.Right) / 2 - WindowSize.X / 2 / Scale;
            offsetY = (bounds.Top + bounds.Bottom) / 2 - (WindowSize.Y - WindowOffset.X) / 2 / Scale;
        }

        private void CenterAround(in PointD centerPoint)
        {
            offsetX = centerPoint.X - WindowSize.X / 2 / Scale;
            offsetY = centerPoint.Y - (WindowSize.Y - WindowOffset.X) / 2 / Scale;
        }

        private void ScaleToFit()
        {
            double xScale = (double)WindowSize.X / bounds.Width;
            double yScale = (double)(WindowSize.Y - WindowOffset.X - WindowOffset.Y) / bounds.Height;
            Scale = Math.Min(xScale, yScale);
            maxScale = Scale * 0.75;
        }

        public override void Draw(GameTime gameTime)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            DrawTracks();
            spriteBatch.End();
            base.Draw(gameTime);
        }

        private Vector2 Translate(in Vector2 world)
        {
            return Translate(world.X, world.Y);
        }

        private Vector2 Translate(float x, float y)
        {
            return new Vector2((float)(x + offsetX * Scale), (float)(y + offsetY * Scale));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            double x = worldLocation.TileX * WorldLocation.TileSize + worldLocation.Location.X;
            double y = worldLocation.TileZ * WorldLocation.TileSize + worldLocation.Location.Z;
            return new Vector2((float)(Scale * (x - offsetX)),
                               (float)(WindowSize.Y - WindowOffset.Y - Scale * (y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return new PointD(offsetX + screenLocation.X / Scale, offsetY + (WindowSize.Y - screenLocation.Y) / Scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return new Vector2((float)(Scale * (location.X - offsetX)),
                               (float)(WindowSize.Y - WindowOffset.Y - Scale * (location.Y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * Scale), minScreenSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InsideScreenArea(in PointD location)
        {
            return location.X > topLeftArea.X && location.X < bottomRightArea.X && location.Y < topLeftArea.Y && location.Y > bottomRightArea.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InsideScreenArea(in PointD start, in PointD end)
        {
            return !(start.X < topLeftArea.X && end.X < topLeftArea.X) || (start.X > topLeftArea.X && end.Y > topLeftArea.X) ||
                (start.Y > topLeftArea.Y && end.Y > topLeftArea.Y) || (start.Y < bottomRightArea.Y && end.Y < bottomRightArea.Y);
        }

        private void DrawTracks()
        {
            foreach (TrackSegment segment in TrackContent.TrackSegments)
            {
                if (InsideScreenArea(in segment.Location, in segment.Vector))
                    if (segment.Curved)
                        BasicShapes.DrawArc(WorldToScreenSize(segment.Width), Color.Black, WorldToScreenCoordinates(in segment.Location), WorldToScreenSize(segment.Length), segment.Direction, segment.Angle, 0, spriteBatch);
                    else
                        BasicShapes.DrawLine(WorldToScreenSize(segment.Width), Color.Black, WorldToScreenCoordinates(in segment.Location), WorldToScreenSize(segment.Length), segment.Direction, spriteBatch);
            }
            foreach (TrackEndSegment endNode in TrackContent.TrackEndNodes)
            {
                if (InsideScreenArea(in endNode.Location))
                    BasicShapes.DrawLine(WorldToScreenSize(endNode.Width), Color.DarkOliveGreen, WorldToScreenCoordinates(in endNode.Location), WorldToScreenSize(TrackEndSegment.Length), endNode.Direction, spriteBatch);
            }
            foreach (JunctionNode junctionNode in TrackContent.JunctionNodes)
            {
                if (InsideScreenArea(in junctionNode.Location))
                    BasicShapes.DrawTexture(BasicTextureType.Disc, WorldToScreenCoordinates(in junctionNode.Location), 0, WorldToScreenSize(junctionNode.Width), Color.DarkRed, false, false, false, spriteBatch);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                spriteBatch?.Dispose();

            }
            base.Dispose(disposing);
        }
    }
}
