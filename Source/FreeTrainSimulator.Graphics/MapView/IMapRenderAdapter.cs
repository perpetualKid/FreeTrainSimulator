using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapRenderAdapter
    {
        void NotifyFrameRendered(ref bool suppressDrawing);

        void RefreshDrawing(ref bool suppressDrawing);

        void DrawContent(ContentBase content);

        void DrawLine(float width, Color color, Vector2 point, float length, double angle);

        void DrawLine(float width, Color color, Vector2 point1, Vector2 point2);

        void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2);

        void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color);

        void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions);

        void UpdateColor(ContentBase content, ColorSetting setting, Color color, bool fontOutlining);
    }
}
