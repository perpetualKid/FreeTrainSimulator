using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal static class WidgetDrawingOptions<T>
    {
        internal static OutlineRenderOptions OutlineRenderOptions;
        internal static readonly EnumArray<Color, ColorVariation> Colors = new EnumArray<Color, ColorVariation>();
        internal static double ScaleFactor = 1;

        internal static void SetColors(Color color)
        {
            Colors[ColorVariation.None] = color;
            Colors[ColorVariation.Highlight] = color.HighlightColor(0.6);
            Colors[ColorVariation.Complement] = color.ComplementColor();
            Colors[ColorVariation.ComplementHighlight] = Colors[ColorVariation.Complement].HighlightColor(0.6);
        }
    }
}
