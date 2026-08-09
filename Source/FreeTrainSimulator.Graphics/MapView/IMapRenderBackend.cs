using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapRenderBackend
    {
        void BeginFrame();

        void EndFrame();

        void DrawLine(float width, Color color, Vector2 point, float length, double angle);

        void DrawLine(float width, Color color, Vector2 point1, Vector2 point2);

        void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2);

        void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, bool flipHorizontal, bool flipVertical);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color);

        void DrawText(Vector2 point, Color color, string text, System.Drawing.Font font, Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions);
    }
}
