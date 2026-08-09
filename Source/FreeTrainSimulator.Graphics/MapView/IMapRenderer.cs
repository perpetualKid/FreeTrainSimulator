using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapRenderer
    {
        double Scale { get; }

        System.Drawing.Font CurrentFont { get; }

        System.Drawing.Font ConstantSizeFont { get; }

        Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation);

        Vector2 WorldToScreenCoordinates(in PointD location);

        float WorldToScreenSize(double worldSize, int minScreenSize = 1);

        void DrawLine(float width, Color color, Vector2 point, float length, double angle);

        void DrawLine(float width, Color color, Vector2 point1, Vector2 point2);

        void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2);

        void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, bool flipHorizontal, bool flipVertical);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color);

        void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions);
    }
}
