using System;

using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal readonly record struct MapViewportBounds(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;
    }

    internal readonly record struct MapViewportSize(int Width, int Height)
    {
        public static MapViewportSize Empty { get; } = new MapViewportSize(0, 0);
    }

    internal sealed class MapViewportState
    {
        private MapViewportBounds bounds;

        private double scaleMin;
        private double offsetX;
        private double offsetY;
        private int screenHeightDelta;

        public MapViewportSize WindowSize { get; private set; } = MapViewportSize.Empty;

        public double Scale { get; private set; }

        public PointD TopLeftBound { get; private set; }

        public PointD BottomRightBound { get; private set; }

        public Tile BottomLeftTile { get; private set; }

        public Tile TopRightTile { get; private set; }

        public PointD CenterPoint => new PointD(((BottomRightBound.X - TopLeftBound.X) / 2) + TopLeftBound.X, ((TopLeftBound.Y - BottomRightBound.Y) / 2) + BottomRightBound.Y);

        public MapViewportState(in MapViewportBounds bounds)
        {
            this.bounds = bounds;
        }

        public void UpdateBounds(in MapViewportBounds bounds)
        {
            this.bounds = bounds;
        }

        public void UpdateWindowSize(in MapViewportSize windowSize)
        {
            WindowSize = windowSize;

            if (Scale <= 0 || !double.IsFinite(Scale) ||
                !double.IsFinite(TopLeftBound.X) || !double.IsFinite(TopLeftBound.Y) ||
                !double.IsFinite(BottomRightBound.X) || !double.IsFinite(BottomRightBound.Y))
                return;

            CenterAround(CenterPoint);
        }

        public void ResetSize(in MapViewportSize windowSize, int screenDelta)
        {
            WindowSize = windowSize;
            screenHeightDelta = screenDelta;
            ScaleToFit();
            CenterView();
        }

        public void PresetPosition(in PointD centerPoint, double scale)
        {
            Scale = scale;
            CenterAround(centerPoint);
        }

        public void SetTrackingPosition(in PointD location)
        {
            CenterAround(location);
        }

        public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight)
        {
            double xScale = WindowSize.Width / Math.Abs(bottomRight.X - topLeft.X);
            double yScale = (WindowSize.Height - screenHeightDelta) / Math.Abs(topLeft.Y - bottomRight.Y);
            Scale = Math.Min(xScale, yScale) * 0.95;
            SetBounds();
        }

        public void UpdateScaleAt(double scaleAtX, double scaleAtY, int steps, double scaleMax)
        {
            double scale = Scale * Math.Pow(steps > 0 ? 1 / 0.95 : steps < 0 ? 0.95 : 1, Math.Abs(steps));
            if (scale < scaleMin && steps < 0 || scale > scaleMax && steps > 0)
                return;

            offsetX += scaleAtX * ((scale / Scale) - 1.0) / scale;
            offsetY += (WindowSize.Height - scaleAtY) * ((scale / Scale) - 1.0) / scale;
            Scale = scale;
            SetBounds();
        }

        public void UpdateScale(int steps, double scaleMax)
        {
            UpdateScaleAt(WindowSize.Width / 2d, WindowSize.Height / 2d, steps, scaleMax);
        }

        public void UpdateScaleAbsolute(double scale, double scaleMax)
        {
            if (scale < scaleMin)
                scale = scaleMin;
            else if (scale > scaleMax)
                scale = scaleMax;

            Scale = scale;
        }

        public void UpdatePosition(double deltaX, double deltaY)
        {
            offsetX -= deltaX / Scale;
            offsetY += deltaY / Scale;

            SetBounds();

            if (TopLeftBound.X > bounds.Right)
                offsetX = bounds.Right;
            else if (BottomRightBound.X < bounds.Left)
                offsetX = bounds.Left - (BottomRightBound.X - TopLeftBound.X);

            if (BottomRightBound.Y > bounds.Bottom)
                offsetY = bounds.Bottom;
            else if (TopLeftBound.Y < bounds.Top)
                offsetY = bounds.Top - (TopLeftBound.Y - BottomRightBound.Y);

            SetBounds();
        }

        public PointD ScreenToWorldCoordinates(double screenX, double screenY)
        {
            return new PointD(offsetX + (screenX / Scale), offsetY + ((WindowSize.Height - screenY) / Scale));
        }

        public PointD WorldToScreenCoordinates(in PointD location)
        {
            return new PointD(Scale * (location.X - offsetX), WindowSize.Height - (Scale * (location.Y - offsetY)));
        }

        public float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * Scale), minScreenSize);
        }

        public bool InsideScreenArea(in PointD location)
        {
            return location.X > TopLeftBound.X && location.X < BottomRightBound.X && location.Y < TopLeftBound.Y && location.Y > BottomRightBound.Y;
        }

        public bool InsideScreenArea(in PointD start, in PointD end)
        {
            bool outside = start.X < TopLeftBound.X && end.X < TopLeftBound.X || start.X > BottomRightBound.X && end.X > BottomRightBound.X ||
                start.Y > TopLeftBound.Y && end.Y > TopLeftBound.Y || start.Y < BottomRightBound.Y && end.Y < BottomRightBound.Y;

            return !outside;
        }

        private void SetBounds()
        {
            TopLeftBound = ScreenToWorldCoordinates(0, 0);
            BottomRightBound = ScreenToWorldCoordinates(WindowSize.Width, WindowSize.Height);

            BottomLeftTile = Tile.TileFromAbs(TopLeftBound.X, BottomRightBound.Y);
            TopRightTile = Tile.TileFromAbs(BottomRightBound.X, TopLeftBound.Y);
        }

        private void CenterView()
        {
            offsetX = ((bounds.Left + bounds.Right) / 2) - (WindowSize.Width / 2d / Scale);
            offsetY = ((bounds.Top + bounds.Bottom) / 2) - (WindowSize.Height / 2d / Scale);
            SetBounds();
        }

        private void CenterAround(in PointD centerPoint)
        {
            offsetX = centerPoint.X - (WindowSize.Width / 2d / Scale);
            offsetY = centerPoint.Y - (WindowSize.Height / 2d / Scale);
            SetBounds();
        }

        private void ScaleToFit()
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                Scale = 1;
                scaleMin = Scale * 0.75;
                return;
            }

            double xScale = (double)WindowSize.Width / bounds.Width;
            double yScale = (double)(WindowSize.Height - screenHeightDelta) / bounds.Height;
            Scale = Math.Min(xScale, yScale);
            scaleMin = Scale * 0.75;
        }
    }
}
