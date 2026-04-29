using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapTextRenderer
    {
        void DrawString(Vector2 point, Color color, string message, System.Drawing.Font font, Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left, VerticalAlignment verticalAlignment = VerticalAlignment.Bottom,
            OutlineRenderOptions outlineRenderOptions = null);
    }
}
