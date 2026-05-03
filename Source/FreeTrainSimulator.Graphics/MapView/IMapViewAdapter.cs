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
    internal interface IMapViewAdapter : IMapViewStateAdapter, IMapRenderAdapter, IMapInteractionAdapter
    {
        double Scale { get; }

        PointD CenterPoint { get; }

        Point WindowSize { get; }

        PointD TopLeftBound { get; }

        PointD BottomRightBound { get; }

        PointD WorldPosition { get; }

        System.Drawing.Font CurrentFont { get; }

        System.Drawing.Font ConstantSizeFont { get; }

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

        void NotifyFrameRendered(ref bool suppressDrawing);

        void RefreshFonts(ref System.Drawing.Font currentFont);

        void RefreshDrawing(ref bool suppressDrawing);

        void DrawContent(ContentBase content);

        PointD ScreenToWorldCoordinates(in Point screenLocation);

        Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation);

        Vector2 WorldToScreenCoordinates(in PointD location);

        float WorldToScreenSize(double worldSize, int minScreenSize = 1);

        bool InsideScreenArea(PointPrimitive pointPrimitive);

        bool InsideScreenArea(VectorPrimitive vectorPrimitive);

        void DrawLine(float width, Color color, Vector2 point, float length, double angle);

        void DrawLine(float width, Color color, Vector2 point1, Vector2 point2);

        void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2);

        void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color);

        void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions);

        void HandleEnabledChanged(bool enabled, ContentArea contentArea, IMapTextureHelperHost textureHelperHost);

        void RefreshAfterReset(ContentBase content, in Point windowSize, int screenDelta, ref System.Drawing.Font currentFont);

        void RefreshAfterScaleChange(ref System.Drawing.Font currentFont);

        void RefreshAfterPreset(ref System.Drawing.Font currentFont, ref bool suppressDrawing);

        void UpdateColor(ContentBase content, ColorSetting setting, Color color, bool fontOutlining);

        void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler);

        void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler);

        void AttachClientSizeChanged(EventHandler handler);
    }
}
