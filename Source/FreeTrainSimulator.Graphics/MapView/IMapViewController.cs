using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapViewController
    {
        double Scale { get; }

        PointD CenterPoint { get; }

        Point WindowSize { get; }

        PointD TopLeftBound { get; }

        PointD BottomRightBound { get; }

        PointD WorldPosition { get; }

        Tile BottomLeftTile { get; }

        Tile TopRightTile { get; }

        bool UpdateFrameState();

        void UpdateViewportBounds(in MapViewportBounds bounds);

        void UpdateViewportWindowSize(in Point windowSize);

        void ResetSize(in Point windowSize, int screenDelta, in Point pointerPosition);

        void PresetPosition(in PointD centerPoint, double scale);

        void SetTrackingPosition(in WorldLocation location);

        void SetTrackingPosition(in PointD location);

        void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight);

        void UpdateScaleAt(in Point scaleAt, int steps);

        void UpdateScale(int steps);

        void UpdateScaleAbsolute(double scale);

        void UpdatePosition(in Vector2 delta);

        PointD ScreenToWorldCoordinates(in Point screenLocation);

        Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation);

        Vector2 WorldToScreenCoordinates(in PointD location);

        float WorldToScreenSize(double worldSize, int minScreenSize = 1);

        bool InsideScreenArea(in PointD location);

        bool InsideScreenArea(in PointD start, in PointD end);

        void MouseMove(bool enabled, in Point position, ContentBase content);

        void MouseDragging(UserCommandArgs userCommandArgs);

        void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers);

        void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers);

        void MoveByKeyLeft(UserCommandArgs commandArgs);

        void MoveByKeyRight(UserCommandArgs commandArgs);

        void MoveByKeyUp(UserCommandArgs commandArgs);

        void MoveByKeyDown(UserCommandArgs commandArgs);

        void ZoomIn(UserCommandArgs commandArgs);

        void ZoomOut(UserCommandArgs commandArgs);
    }
}
