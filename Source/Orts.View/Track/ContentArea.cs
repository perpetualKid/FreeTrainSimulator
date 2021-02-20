using System;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Orts.Common.Position;
using Orts.View.Track.Widgets;
using Orts.View.Xna;

namespace Orts.View.Track
{
    public class ContentArea : DrawableGameComponent
    {
        private Rectangle bounds;
        private double maxScale;

        public TrackContent TrackContent { get; }

        public double Scale { get; private set; }

        private double offsetX, offsetY;
        internal PointD TopLeftArea { get; private set; }
        internal PointD BottomRightArea { get; private set; }

        private Tile bottomLeft;
        private Tile topRight;

        public Point WindowSize { get; private set; }

        private int screenHeightDelta;  // to account for Menubar/Statusbar height when calculating initial scale and center view

        private readonly SpriteBatch spriteBatch;
        internal SpriteBatch SpriteBatch => spriteBatch;

        private readonly FontManager fontManager;
        private System.Drawing.Font currentFont;

        private double previousScale;
        private PointD previousTopLeft, previousBottomRight;
        private int supressCount;

#pragma warning disable CA2213 // Disposable fields should be disposed
        private InputGameComponent inputComponent;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public ContentArea(Game game, TrackContent trackContent) :
            base(game)
        {
            Enabled = false;
            TrackContent = trackContent ?? throw new ArgumentNullException(nameof(trackContent));
            bounds = trackContent.Bounds;
            spriteBatch = new SpriteBatch(GraphicsDevice);
            fontManager = FontManager.Instance("Segoe UI", System.Drawing.FontStyle.Regular);
            inputComponent = game.Components.OfType<InputGameComponent>().First();
            inputComponent.AddMouseEvent(InputGameComponent.MouseMovedEventType.MouseMoved, MouseMove);
        }

        private GridTile nearest;
        public void MouseMove(Point position, Vector2 delta)
        {
            PointD worldPosition = ScreenToWorldCoordinates(position);
            System.Collections.Generic.IEnumerable<ITileCoordinate<Tile>> result = TrackContent.Tiles.FindNearest(worldPosition, bottomLeft, topRight);
            if (result.First() != nearest)
            {
                nearest = result.First() as GridTile;
            }
        }

        protected override void OnEnabledChanged(object sender, EventArgs args)
        {
            foreach (TextureContentComponent component in Game.Components.OfType<TextureContentComponent>())
            {
                if (Enabled)
                    component.Enable(this);
                else
                    component.Disable();
            }
            base.OnEnabledChanged(sender, args);
        }

        public void ResetSize(in Point windowSize, int screenDelta)
        {
            WindowSize = windowSize;
            screenHeightDelta = screenDelta;
            ScaleToFit();
            CenterView();
            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);

            bottomLeft = new Tile(Tile.TileFromAbs(TopLeftArea.X), Tile.TileFromAbs(BottomRightArea.Y));
            topRight = new Tile(Tile.TileFromAbs(BottomRightArea.X), Tile.TileFromAbs(TopLeftArea.Y));
        }

        public void UpdateSize(in Point windowSize)
        {
            WindowSize = windowSize;
            CenterAround(new PointD((TopLeftArea.X + BottomRightArea.X) / 2, (TopLeftArea.Y + BottomRightArea.Y) / 2));
            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);

            bottomLeft = new Tile(Tile.TileFromAbs(TopLeftArea.X), Tile.TileFromAbs(BottomRightArea.Y));
            topRight = new Tile(Tile.TileFromAbs(BottomRightArea.X), Tile.TileFromAbs(TopLeftArea.Y));
        }

        public void UpdateScaleAt(in Vector2 scaleAt, int steps)
        {
            double scale = Scale * Math.Pow((steps > 0 ? 1 / 0.9 : (steps < 0 ? 0.9 : 1)), Math.Abs(steps));
            if (scale < maxScale || scale > 200)
                return;
            offsetX += scaleAt.X * (scale / Scale - 1.0) / scale;
            offsetY += (WindowSize.Y - scaleAt.Y) * (scale / Scale - 1.0) / scale;
            Scale = scale;

            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);

            bottomLeft = new Tile(Tile.TileFromAbs(TopLeftArea.X), Tile.TileFromAbs(BottomRightArea.Y));
            topRight = new Tile(Tile.TileFromAbs(BottomRightArea.X), Tile.TileFromAbs(TopLeftArea.Y));
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

            bottomLeft = new Tile(Tile.TileFromAbs(TopLeftArea.X), Tile.TileFromAbs(BottomRightArea.Y));
            topRight = new Tile(Tile.TileFromAbs(BottomRightArea.X), Tile.TileFromAbs(TopLeftArea.Y));
        }

        public override void Update(GameTime gameTime)
        {
            if (Scale == previousScale && TopLeftArea == previousTopLeft && BottomRightArea == previousBottomRight && supressCount-- > 0)
            {
                Game.SuppressDraw();
            }
            else
            {
                previousScale = Scale;
                previousTopLeft = TopLeftArea;
                previousBottomRight = BottomRightArea;
                supressCount = 10;
            }
            base.Update(gameTime);
        }

        private void CenterView()
        {
            offsetX = (bounds.Left + bounds.Right) / 2 - WindowSize.X / 2 / Scale;
            offsetY = (bounds.Top + bounds.Bottom) / 2 - (WindowSize.Y) / 2 / Scale;
        }

        private void CenterAround(in PointD centerPoint)
        {
            offsetX = centerPoint.X - WindowSize.X / 2 / Scale;
            offsetY = centerPoint.Y - (WindowSize.Y) / 2 / Scale;
        }

        private void ScaleToFit()
        {
            double xScale = (double)WindowSize.X / bounds.Width;
            double yScale = (double)(WindowSize.Y - screenHeightDelta) / bounds.Height;
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
        internal Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            double x = worldLocation.TileX * WorldLocation.TileSize + worldLocation.Location.X;
            double y = worldLocation.TileZ * WorldLocation.TileSize + worldLocation.Location.Z;
            return new Vector2((float)(Scale * (x - offsetX)),
                               (float)(WindowSize.Y - Scale * (y - offsetY)));
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
                               (float)(WindowSize.Y - Scale * (location.Y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * Scale), minScreenSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InsideScreenArea(PointWidget pointWidget)
        {
            ref readonly PointD location = ref pointWidget.Location;
            return location.X > TopLeftArea.X && location.X < BottomRightArea.X && location.Y < TopLeftArea.Y && location.Y > BottomRightArea.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InsideScreenArea(VectorWidget vectorWidget)
        {
            ref readonly PointD start = ref vectorWidget.Location;
            ref readonly PointD end = ref vectorWidget.Vector;
            bool outside = (start.X < TopLeftArea.X && end.X < TopLeftArea.X) || (start.X > BottomRightArea.X && end.X > BottomRightArea.X) ||
                (start.Y > TopLeftArea.Y && end.Y > TopLeftArea.Y) || (start.Y < BottomRightArea.Y && end.Y < BottomRightArea.Y);
            return !outside;
        }

        private void DrawTracks()
        {
            int fontsize = MathHelper.Clamp((int)(25 * Scale), 1, 25);
            if (fontsize != (currentFont?.Size ?? 0))
                currentFont = fontManager[fontsize];
            TrackItemBase.SetFont(currentFont);

            foreach (TrackSegment segment in TrackContent.TrackSegments.BoundingBox(bottomLeft, topRight))
            {
                if (InsideScreenArea(segment))
                    segment.Draw(this);
            }
            foreach (TrackEndSegment endNode in TrackContent.TrackEndSegments.BoundingBox(bottomLeft, topRight))
            {
                if (InsideScreenArea(endNode))
                    endNode.Draw(this);
            }
            foreach (JunctionSegment junctionNode in TrackContent.JunctionSegments.BoundingBox(bottomLeft, topRight))
            {
                if (InsideScreenArea(junctionNode))
                    junctionNode.Draw(this);
            }
            foreach (TrackItemBase trackItem in TrackContent.TrackItems.BoundingBox(bottomLeft, topRight))
            {
                if (InsideScreenArea(trackItem))
                    trackItem.Draw(this);
            }
            foreach (GridTile tile in TrackContent.Tiles.BoundingBox(bottomLeft, topRight))
            {
                tile.Draw(this);
            }
            nearest?.DrawHighlight(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                spriteBatch?.Dispose();
                inputComponent.RemoveMouseEvent(InputGameComponent.MouseMovedEventType.MouseMoved, MouseMove);
                inputComponent = null;

            }
            base.Dispose(disposing);
        }
    }
}
