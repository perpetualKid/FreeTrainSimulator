using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapHostControl
    {
        bool IsEnabled { get; set; }

        PointD CenterPoint { get; }

        double Scale { get; }

        void ResetSize(in Point windowSize, int screenDelta);

        void UpdatePosition(in Vector2 delta);

        void UpdateScale(int steps);

        void UpdateScaleAt(in Point scaleAt, int steps);

        void UpdateScaleAbsolute(double scale);

        void MouseDragging(UserCommandArgs userCommandArgs);

        void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers);

        void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers);

        void MoveByKeyLeft(UserCommandArgs commandArgs);

        void MoveByKeyRight(UserCommandArgs commandArgs);

        void MoveByKeyUp(UserCommandArgs commandArgs);

        void MoveByKeyDown(UserCommandArgs commandArgs);

        void ZoomIn(UserCommandArgs commandArgs);

        void ZoomOut(UserCommandArgs commandArgs);

        void ResetZoomAndLocation(Point windowSize, int screenDelta);
    }
}
