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
    internal sealed class MapViewAdapterCore : IMapViewAdapter
    {
        private readonly FontManagerInstance fontManager;
        private readonly IMapViewController controller;
        private readonly IMapHostEnvironment hostEnvironment;
        private readonly IMapRenderBackend renderBackend;
        private System.Drawing.Font currentFont;
        private readonly System.Drawing.Font constantSizeFont;

        public MapViewAdapterCore(FontManagerInstance fontManager, IMapViewController controller, IMapHostEnvironment hostEnvironment, IMapRenderBackend renderBackend, System.Drawing.Font constantSizeFont)
        {
            this.fontManager = fontManager ?? throw new ArgumentNullException(nameof(fontManager));
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
            this.renderBackend = renderBackend ?? throw new ArgumentNullException(nameof(renderBackend));
            this.constantSizeFont = constantSizeFont ?? throw new ArgumentNullException(nameof(constantSizeFont));
        }

        public double Scale => controller.Scale;

        public PointD CenterPoint => controller.CenterPoint;

        public Point WindowSize => controller.WindowSize;

        public PointD TopLeftBound => controller.TopLeftBound;

        public PointD BottomRightBound => controller.BottomRightBound;

        public PointD WorldPosition => controller.WorldPosition;

        public System.Drawing.Font CurrentFont => currentFont;

        public System.Drawing.Font ConstantSizeFont => constantSizeFont;

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

        public void NotifyFrameRendered(ref bool suppressDrawing)
        {
            controller.NotifyFrameRendered();
            suppressDrawing = true;
        }

        public void RefreshFonts(ref System.Drawing.Font currentFont)
        {
            if (!controller.ConsumeScaleChanged())
                return;

            int fontSize = controller.DynamicFontSize;
            if (fontSize != (currentFont?.Size ?? 0))
                currentFont = fontManager[fontSize];
            TrackItemWidget.SetFont(currentFont);
            this.currentFont = currentFont;
        }

        public void DrawContent(ContentBase content)
        {
            renderBackend.BeginFrame();
            content.Draw(controller.BottomLeftTile, controller.TopRightTile);
            renderBackend.EndFrame();
        }

        public PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return controller.ScreenToWorldCoordinates(screenLocation);
        }

        public Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            return controller.WorldToScreenCoordinates(worldLocation);
        }

        public Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return controller.WorldToScreenCoordinates(location);
        }

        public float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return controller.WorldToScreenSize(worldSize, minScreenSize);
        }

        public bool InsideScreenArea(PointPrimitive pointPrimitive)
        {
            ArgumentNullException.ThrowIfNull(pointPrimitive);
            return controller.InsideScreenArea(pointPrimitive.Location);
        }

        public bool InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            ArgumentNullException.ThrowIfNull(vectorPrimitive);
            return controller.InsideScreenArea(vectorPrimitive.Location, vectorPrimitive.Vector);
        }

        public void RefreshDrawing(ref bool suppressDrawing)
        {
            if (controller.ConsumeRedrawRequested())
                suppressDrawing = false;
        }

        public void DrawLine(float width, Color color, Vector2 point, float length, double angle)
        {
            renderBackend.DrawLine(width, color, point, length, angle);
        }

        public void DrawLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            renderBackend.DrawLine(width, color, point1, point2);
        }

        public void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            renderBackend.DrawDashedLine(width, color, point1, point2);
        }

        public void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize)
        {
            renderBackend.DrawArc(width, color, point, radius, angle, arcSize);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight)
        {
            renderBackend.DrawTexture(texture, point, angle, size, flipHorizontal, flipVertical, highlight);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color)
        {
            renderBackend.DrawTexture(texture, point, angle, size, color);
        }

        public void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions)
        {
            renderBackend.DrawText(controller.WorldToScreenCoordinates(location), color, text, font, scale, angle, horizontalAlignment, verticalAlignment, outlineRenderOptions);
        }

        public void HandleEnabledChanged(bool enabled, ContentArea contentArea, IMapTextureHelperHost textureHelperHost)
        {
            if (enabled)
                textureHelperHost?.Enable(contentArea);
            else
                textureHelperHost?.Disable();
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
            RefreshDrawing(ref suppressDrawing);
        }

        public void UpdateColor(ContentBase content, ColorSetting setting, Color color, bool fontOutlining)
        {
            switch (setting)
            {
                case ColorSetting.Background:
                    content.InsetHost?.UpdateColor(color);
                    break;
                case ColorSetting.RailTrack:
                    WidgetDrawingOptions<TrackSegment>.SetColors(color);
                    break;
                case ColorSetting.RailTrackEnd:
                    WidgetDrawingOptions<EndNode>.SetColors(color);
                    break;
                case ColorSetting.RailTrackJunction:
                    WidgetDrawingOptions<JunctionNode>.SetColors(color);
                    break;
                case ColorSetting.RailTrackCrossing:
                    WidgetDrawingOptions<CrossOverTrackItem>.SetColors(color);
                    break;
                case ColorSetting.RailLevelCrossing:
                    WidgetDrawingOptions<LevelCrossingTrackItem>.SetColors(color);
                    break;
                case ColorSetting.RoadTrack:
                    WidgetDrawingOptions<RoadSegment>.SetColors(color);
                    break;
                case ColorSetting.RoadTrackEnd:
                    WidgetDrawingOptions<RoadEndSegment>.SetColors(color);
                    break;
                case ColorSetting.PathTrack:
                    WidgetDrawingOptions<PathSegment>.SetColors(color);
                    WidgetDrawingOptions<EditorTrainPathSegment>.SetColors(color);
                    WidgetDrawingOptions<EditorTrainPath>.SetColors(color);
                    break;
                case ColorSetting.StationItem:
                    WidgetDrawingOptions<StationNameItem>.SetColors(color);
                    WidgetDrawingOptions<StationNameItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(3.0f, color, color.ContrastColor()) : null;
                    break;
                case ColorSetting.PlatformItem:
                    WidgetDrawingOptions<PlatformNameItem>.SetColors(color);
                    WidgetDrawingOptions<PlatformNameItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(2.0f, color, color.ContrastColor()) : null;
                    WidgetDrawingOptions<PlatformPath>.SetColors(color);
                    color.A = 160;
                    WidgetDrawingOptions<PlatformSegment>.SetColors(color);
                    break;
                case ColorSetting.SidingItem:
                    WidgetDrawingOptions<SidingNameItem>.SetColors(color);
                    WidgetDrawingOptions<SidingNameItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(2.0f, color, color.ContrastColor()) : null;
                    WidgetDrawingOptions<SidingPath>.SetColors(color);
                    color.A = 160;
                    WidgetDrawingOptions<SidingSegment>.SetColors(color);
                    break;
                case ColorSetting.SpeedPostItem:
                    WidgetDrawingOptions<SpeedPostTrackItem>.SetColors(color);
                    WidgetDrawingOptions<SpeedPostTrackItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(2.0f, color.ContrastColor(), color) : null;
                    break;
                case ColorSetting.MilePostItem:
                    WidgetDrawingOptions<MilePostTrackItem>.SetColors(color);
                    WidgetDrawingOptions<MilePostTrackItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(2.0f, color, color.ContrastColor()) : null;
                    break;
            }
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
