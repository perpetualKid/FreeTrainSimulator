using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class MapRenderAdapter : IMapRenderAdapter
    {
        private readonly IMapViewController controller;
        private readonly IMapRenderBackend renderBackend;

        public MapRenderAdapter(IMapViewController controller, IMapRenderBackend renderBackend)
        {
            this.controller = controller;
            this.renderBackend = renderBackend;
        }

        public void NotifyFrameRendered(ref bool suppressDrawing)
        {
            controller.NotifyFrameRendered();
            suppressDrawing = true;
        }

        public void RefreshDrawing(ref bool suppressDrawing)
        {
            if (controller.ConsumeRedrawRequested())
                suppressDrawing = false;
        }

        public void DrawContent(ContentBase content)
        {
            renderBackend.BeginFrame();
            content.Draw(controller.BottomLeftTile, controller.TopRightTile);
            renderBackend.EndFrame();
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
    }
}
