using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapInteractionAdapter
    {
        void SyncViewport(in Rectangle bounds);

        void SyncViewportForContent(ContentBase content);

        void ResetSize(in Point windowSize, int screenDelta);

        void PresetPosition(in PointD centerPoint, double scale);

        void SetTrackingPosition(in WorldLocation location);

        void SetTrackingPosition(in PointD location);

        void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight);

        void UpdateScaleAt(in Point scaleAt, int steps);

        void UpdateScale(int steps);

        void UpdateScaleAbsolute(double scale);

        void UpdatePosition(in Vector2 delta);

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

        void UpdateFrameState(ref bool suppressDrawing);

        void HandleEnabledChanged(bool enabled, ContentArea contentArea, IMapTextureHelperHost textureHelperHost);

        void RefreshFonts(ref System.Drawing.Font currentFont);

        void RefreshAfterReset(ContentBase content, in Point windowSize, int screenDelta, ref System.Drawing.Font currentFont);

        void RefreshAfterScaleChange(ref System.Drawing.Font currentFont);

        void RefreshAfterPreset(ref System.Drawing.Font currentFont, ref bool suppressDrawing);

        void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler);

        void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler);

        void AttachClientSizeChanged(EventHandler handler);
    }
}
