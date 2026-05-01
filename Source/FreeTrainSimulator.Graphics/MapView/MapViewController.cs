using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class MapViewController : IMapViewController
    {
        private static readonly Vector2 moveLeft = new Vector2(1, 0);
        private static readonly Vector2 moveRight = new Vector2(-1, 0);
        private static readonly Vector2 moveUp = new Vector2(0, 1);
        private static readonly Vector2 moveDown = new Vector2(0, -1);

        private const int zoomAmplifier = 3;
        private const int scaleMax = 200;

        private readonly MapViewportState viewport;
        private PointD worldPosition;
        private double previousScale;
        private PointD previousTopLeft;
        private PointD previousBottomRight;
        private long nextUpdate;

        public MapViewController(in MapViewportBounds bounds)
        {
            viewport = new MapViewportState(bounds);
        }

        public double Scale => viewport.Scale;

        public PointD CenterPoint => viewport.CenterPoint;

        public Point WindowSize => new Point(viewport.WindowSize.Width, viewport.WindowSize.Height);

        public PointD TopLeftBound => viewport.TopLeftBound;

        public PointD BottomRightBound => viewport.BottomRightBound;

        public PointD WorldPosition => worldPosition;

        public Tile BottomLeftTile => viewport.BottomLeftTile;

        public Tile TopRightTile => viewport.TopRightTile;

        public bool UpdateFrameState()
        {
            if (Scale != previousScale || TopLeftBound != previousTopLeft || BottomRightBound != previousBottomRight)
            {
                previousScale = Scale;
                previousTopLeft = TopLeftBound;
                previousBottomRight = BottomRightBound;
                return true;
            }
            return false;
        }

        public void UpdateViewportBounds(in MapViewportBounds bounds)
        {
            viewport.UpdateBounds(bounds);
        }

        public void UpdateViewportWindowSize(in Point windowSize)
        {
            viewport.UpdateWindowSize(new MapViewportSize(windowSize.X, windowSize.Y));
        }

        public void ResetSize(in Point windowSize, int screenDelta, in Point pointerPosition)
        {
            viewport.ResetSize(new MapViewportSize(windowSize.X, windowSize.Y), screenDelta);
            worldPosition = ScreenToWorldCoordinates(pointerPosition);
        }

        public void PresetPosition(in PointD centerPoint, double scale)
        {
            if (centerPoint != PointD.None)
                viewport.PresetPosition(centerPoint, scale);
        }

        public void SetTrackingPosition(in WorldLocation location)
        {
            viewport.SetTrackingPosition(PointD.FromWorldLocation(location));
        }

        public void SetTrackingPosition(in PointD location)
        {
            viewport.SetTrackingPosition(location);
        }

        public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight)
        {
            viewport.UpdateScaleToFit(topLeft, bottomRight);
        }

        public void UpdateScaleAt(in Point scaleAt, int steps)
        {
            viewport.UpdateScaleAt(scaleAt.X, scaleAt.Y, steps, scaleMax);
        }

        public void UpdateScale(int steps)
        {
            viewport.UpdateScale(steps, scaleMax);
        }

        public void UpdateScaleAbsolute(double scale)
        {
            viewport.UpdateScaleAbsolute(scale, scaleMax);
        }

        public void UpdatePosition(in Vector2 delta)
        {
            viewport.UpdatePosition(delta.X, delta.Y);
        }

        public PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return viewport.ScreenToWorldCoordinates(screenLocation.X, screenLocation.Y);
        }

        public Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            double x = (worldLocation.TileX * WorldLocation.TileSize) + worldLocation.Location.X;
            double y = (worldLocation.TileZ * WorldLocation.TileSize) + worldLocation.Location.Z;
            return WorldToScreenCoordinates(new PointD(x, y));
        }

        public Vector2 WorldToScreenCoordinates(in PointD location)
        {
            PointD screenLocation = viewport.WorldToScreenCoordinates(location);
            return new Vector2((float)screenLocation.X, (float)screenLocation.Y);
        }

        public float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return viewport.WorldToScreenSize(worldSize, minScreenSize);
        }

        public bool InsideScreenArea(in PointD location)
        {
            return viewport.InsideScreenArea(location);
        }

        public bool InsideScreenArea(in PointD start, in PointD end)
        {
            return viewport.InsideScreenArea(start, end);
        }

        public void MouseMove(bool enabled, in Point position, ContentBase content)
        {
            if (!enabled)
                return;

            worldPosition = ScreenToWorldCoordinates(position);
            if (Scale > 0.2)
                content.UpdatePointerLocation(worldPosition, BottomLeftTile, TopRightTile);
        }

        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            if (userCommandArgs is PointerMoveCommandArgs mouseMoveCommandArgs)
                UpdatePosition(mouseMoveCommandArgs.Delta);
        }

        public void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (userCommandArgs is ScrollCommandArgs mouseWheelCommandArgs)
                UpdateScaleAt(mouseWheelCommandArgs.Position, Math.Sign(mouseWheelCommandArgs.Delta) * ZoomAmplifier(modifiers));
        }

        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (userCommandArgs is ScrollCommandArgs mouseWheelCommandArgs)
                UpdateScale(Math.Sign(mouseWheelCommandArgs.Delta) * ZoomAmplifier(modifiers));
        }

        public void MoveByKeyLeft(UserCommandArgs commandArgs)
        {
            UpdatePosition(moveLeft * MovementAmplifier(commandArgs));
        }

        public void MoveByKeyRight(UserCommandArgs commandArgs)
        {
            UpdatePosition(moveRight * MovementAmplifier(commandArgs));
        }

        public void MoveByKeyUp(UserCommandArgs commandArgs)
        {
            UpdatePosition(moveUp * MovementAmplifier(commandArgs));
        }

        public void MoveByKeyDown(UserCommandArgs commandArgs)
        {
            UpdatePosition(moveDown * MovementAmplifier(commandArgs));
        }

        public void ZoomIn(UserCommandArgs commandArgs)
        {
            Zoom(ZoomAmplifier(commandArgs));
        }

        public void ZoomOut(UserCommandArgs commandArgs)
        {
            Zoom(-ZoomAmplifier(commandArgs));
        }

        private static int MovementAmplifier(UserCommandArgs commandArgs)
        {
            int amplifier = 5;
            if (commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand)
            {
                if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Control) == KeyModifiers.Control)
                    amplifier = 1;
                else if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                    amplifier = 10;
            }
            return amplifier;
        }

        private static int ZoomAmplifier(KeyModifiers modifiers)
        {
            int amplifier = zoomAmplifier;
            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
                amplifier = 1;
            else if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                amplifier = 5;
            return amplifier;
        }

        private static int ZoomAmplifier(UserCommandArgs commandArgs)
        {
            return commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand ? ZoomAmplifier(modifiableKeyCommand.AdditionalModifiers) : zoomAmplifier;
        }

        private void Zoom(int steps)
        {
            if (Environment.TickCount64 > nextUpdate)
            {
                UpdateScale(steps);
                nextUpdate = Environment.TickCount64 + 30;
            }
        }
    }
}
