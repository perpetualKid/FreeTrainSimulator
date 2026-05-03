using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class MapInteractionAdapter : IMapInteractionAdapter
    {
        private readonly FontManagerInstance fontManager;
        private readonly IMapViewController controller;
        private readonly IMapHostEnvironment hostEnvironment;
        private readonly IMapViewFontState fontState;

        public MapInteractionAdapter(FontManagerInstance fontManager, IMapViewController controller, IMapHostEnvironment hostEnvironment, IMapViewFontState fontState)
        {
            this.fontManager = fontManager ?? throw new ArgumentNullException(nameof(fontManager));
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
            this.fontState = fontState ?? throw new ArgumentNullException(nameof(fontState));
        }

        public void SyncViewport(in Rectangle bounds)
        {
            controller.SyncViewport(new MapViewportBounds(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), hostEnvironment.ClientSize);
        }

        public void ResetSize(in Point windowSize, int screenDelta)
        {
            controller.ResetSize(windowSize, screenDelta, hostEnvironment.PointerPosition);
        }

        public void PresetPosition(in PointD centerPoint, double scale)
        {
            controller.PresetPosition(centerPoint, scale);
        }

        public void SetTrackingPosition(in WorldLocation location)
        {
            controller.SetTrackingPosition(location);
        }

        public void SetTrackingPosition(in PointD location)
        {
            controller.SetTrackingPosition(location);
        }

        public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight)
        {
            controller.UpdateScaleToFit(topLeft, bottomRight);
        }

        public void UpdateScaleAt(in Point scaleAt, int steps)
        {
            controller.UpdateScaleAt(scaleAt, steps);
        }

        public void UpdateScale(int steps)
        {
            controller.UpdateScale(steps);
        }

        public void UpdateScaleAbsolute(double scale)
        {
            controller.UpdateScaleAbsolute(scale);
        }

        public void UpdatePosition(in Vector2 delta)
        {
            controller.UpdatePosition(delta);
        }

        public void MouseMove(bool enabled, in Point position, ContentBase content)
        {
            controller.MouseMove(enabled, position, content);
        }

        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            controller.MouseDragging(userCommandArgs);
        }

        public void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            controller.MouseWheelAt(userCommandArgs, modifiers);
        }

        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            controller.MouseWheel(userCommandArgs, modifiers);
        }

        public void MoveByKeyLeft(UserCommandArgs commandArgs)
        {
            controller.MoveByKeyLeft(commandArgs);
        }

        public void MoveByKeyRight(UserCommandArgs commandArgs)
        {
            controller.MoveByKeyRight(commandArgs);
        }

        public void MoveByKeyUp(UserCommandArgs commandArgs)
        {
            controller.MoveByKeyUp(commandArgs);
        }

        public void MoveByKeyDown(UserCommandArgs commandArgs)
        {
            controller.MoveByKeyDown(commandArgs);
        }

        public void ZoomIn(UserCommandArgs commandArgs)
        {
            controller.ZoomIn(commandArgs);
        }

        public void ZoomOut(UserCommandArgs commandArgs)
        {
            controller.ZoomOut(commandArgs);
        }

        public void UpdateFrameState(ref bool suppressDrawing)
        {
            controller.UpdateFrameState();
            if (controller.ConsumeRedrawRequested())
                suppressDrawing = false;
        }

        public void HandleEnabledChanged(bool enabled, ContentArea contentArea, IMapTextureHelperHost textureHelperHost)
        {
            if (enabled)
                textureHelperHost?.Enable(contentArea);
            else
                textureHelperHost?.Disable();
        }

        public void RefreshFonts(ref System.Drawing.Font currentFont)
        {
            if (!controller.ConsumeScaleChanged())
                return;

            int fontSize = controller.DynamicFontSize;
            if (fontSize != (currentFont?.Size ?? 0))
                currentFont = fontManager[fontSize];
            TrackItemWidget.SetFont(currentFont);
            fontState.UpdateCurrentFont(currentFont);
        }

        public void RefreshAfterReset(ContentBase content, in Point windowSize, int screenDelta, ref System.Drawing.Font currentFont)
        {
            SyncViewport(content.Bounds);
            ResetSize(windowSize, screenDelta);
            RefreshFonts(ref currentFont);
        }

        public void RefreshAfterScaleChange(ref System.Drawing.Font currentFont)
        {
            RefreshFonts(ref currentFont);
        }

        public void RefreshAfterPreset(ref System.Drawing.Font currentFont, ref bool suppressDrawing)
        {
            RefreshFonts(ref currentFont);
            if (controller.ConsumeRedrawRequested())
                suppressDrawing = false;
        }

        public static void UpdateTrackWidthSettings(bool limitTrackWidth)
        {
            TrackSegment.UpdateTrackWidthRatio(limitTrackWidth);
            JunctionNode.UpdateTrackWidthRatio(limitTrackWidth);
            SpeedPostTrackItem.UpdateTrackWidthRatio(limitTrackWidth);
        }

        public void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler)
        {
            hostEnvironment.RegisterMouseMove(handler);
        }

        public void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler)
        {
            hostEnvironment.UnregisterMouseMove(handler);
        }

        public void AttachClientSizeChanged(EventHandler handler)
        {
            hostEnvironment.ClientSizeChanged += handler;
        }

        public void SyncViewportForContent(ContentBase content)
        {
            SyncViewport(content.Bounds);
        }
    }
}
