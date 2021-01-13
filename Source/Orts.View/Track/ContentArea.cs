using System;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.View.DrawableComponents;
using Orts.View.Track.Shapes;
using Orts.View.Track.Widgets;
using Orts.View.Xna;

namespace Orts.View.Track
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct ScreenDelta
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
#pragma warning disable CA1051 // Do not declare visible instance fields
        public readonly int TopOffset;
        public readonly int BottomOffset;
#pragma warning restore CA1051 // Do not declare visible instance fields

        public ScreenDelta(int top, int bottom)
        {
            TopOffset = top;
            BottomOffset = bottom;
        }
    }

    public class ContentArea : DrawableGameComponent
    {
        private Rectangle bounds;
        private double maxScale;

        public TrackContent TrackContent { get; }

        public double Scale { get; private set; }

        private double offsetX, offsetY;
        internal PointD TopLeftArea { get; private set; }
        internal PointD BottomRightArea { get; private set; }

        public Point WindowSize { get; private set; }

        private ScreenDelta screenDelta;

        private readonly SpriteBatch spriteBatch;

        public ContentArea(Game game, TrackContent trackContent) :
            base(game)
        {
            TrackContent = trackContent ?? throw new ArgumentNullException(nameof(trackContent));
            bounds = trackContent.Bounds;
            spriteBatch = new SpriteBatch(GraphicsDevice);
            foreach (TextureContentComponent component in Game.Components.OfType<TextureContentComponent>())
            {
                component.Enable(this);
            }
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

        public void ResetSize(in Point windowSize, in ScreenDelta offset)
        {
            WindowSize = windowSize;
            screenDelta = offset;
            ScaleToFit();
            CenterView();
            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);
        }

        public void UpdateSize(in Point windowSize)
        {
            WindowSize = windowSize;
            CenterAround(new PointD((TopLeftArea.X + BottomRightArea.X) / 2, (TopLeftArea.Y + BottomRightArea.Y) / 2));
            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);
        }

        public void UpdateScaleAt(in Vector2 scaleAt, int steps)
        {
            double scale = Scale * Math.Pow((steps > 0 ? 1 / 0.9 : (steps < 0 ? 0.9 : 1)), Math.Abs(steps));
            if (scale < maxScale || scale > 200)
                return;
            offsetX += scaleAt.X * (scale / Scale - 1.0) / scale;
            offsetY += (WindowSize.Y - screenDelta.BottomOffset - scaleAt.Y) * (scale / Scale - 1.0) / scale;
            Scale = scale;

            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);
        }

        public void UpdatePosition(in Vector2 delta)
        {
            offsetX -= delta.X / Scale;
            offsetY += delta.Y / Scale;

            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);

            if (TopLeftArea.X > bounds.Right)
            {
                offsetX = bounds.Right;
            }
            else if (BottomRightArea.X < bounds.Left)
            {
                offsetX = bounds.Left - (BottomRightArea.X - TopLeftArea.X);
            }

            if (BottomRightArea.Y > bounds.Bottom)
            {
                offsetY = bounds.Bottom;
            }
            else if (TopLeftArea.Y < bounds.Top)
            {
                offsetY = bounds.Top - (TopLeftArea.Y - BottomRightArea.Y);
            }

            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);

        }

        private void CenterView()
        {
            offsetX = (bounds.Left + bounds.Right) / 2 - WindowSize.X / 2 / Scale;
            offsetY = (bounds.Top + bounds.Bottom) / 2 - (WindowSize.Y - screenDelta.TopOffset) / 2 / Scale;
        }

        private void CenterAround(in PointD centerPoint)
        {
            offsetX = centerPoint.X - WindowSize.X / 2 / Scale;
            offsetY = centerPoint.Y - (WindowSize.Y - screenDelta.TopOffset) / 2 / Scale;
        }

        private void ScaleToFit()
        {
            double xScale = (double)WindowSize.X / bounds.Width;
            double yScale = (double)(WindowSize.Y - screenDelta.TopOffset - screenDelta.BottomOffset) / bounds.Height;
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
                               (float)(WindowSize.Y - screenDelta.BottomOffset - Scale * (y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return new PointD(offsetX + screenLocation.X / Scale, offsetY + (WindowSize.Y - screenLocation.Y) / Scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return new Vector2((float)(Scale * (location.X - offsetX)),
                               (float)(WindowSize.Y - screenDelta.BottomOffset - Scale * (location.Y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * Scale), minScreenSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InsideScreenArea(in PointD location)
        {
            return location.X > TopLeftArea.X && location.X < BottomRightArea.X && location.Y < TopLeftArea.Y && location.Y > BottomRightArea.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InsideScreenArea(in PointD start, in PointD end)
        {
            return !(start.X < TopLeftArea.X && end.X < TopLeftArea.X) || (start.X > TopLeftArea.X && end.Y > TopLeftArea.X) ||
                (start.Y > TopLeftArea.Y && end.Y > TopLeftArea.Y) || (start.Y < BottomRightArea.Y && end.Y < BottomRightArea.Y);
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
