using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics.Xna;

namespace Orts.Graphics.MapView.Widgets
{
    internal static class WidgetColorCache
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Color GetColor<T>(this IDrawable<PointPrimitive> _, ColorVariation colorVariation) where T : PointPrimitive
        {
            return WidgetColorCache<T>.Colors[colorVariation];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Color GetColor<T>(this IDrawable<VectorPrimitive> _, ColorVariation colorVariation) where T : VectorPrimitive
        {
            return WidgetColorCache<T>.Colors[colorVariation];
        }

        internal static void SetColors<T>(Color color) where T : PointPrimitive
        {
            WidgetColorCache<T>.Colors[ColorVariation.None] = color;
            WidgetColorCache<T>.Colors[ColorVariation.Highlight] = color.HighlightColor(0.6);
            WidgetColorCache<T>.Colors[ColorVariation.Complement] = color.ComplementColor();
            WidgetColorCache<T>.Colors[ColorVariation.ComplementHighlight] = WidgetColorCache<T>.Colors[ColorVariation.Complement].HighlightColor(0.6);
        }

        internal static void UpdateColor<T>(Color color) where T : PointPrimitive
        {
            SetColors<T>(color);
        }
    }

    internal static class WidgetColorCache<T>
    {
        internal static readonly EnumArray<Color, ColorVariation> Colors = new EnumArray<Color, ColorVariation>();

        internal static Color GetColor(ColorVariation colorVariation)
        {
            return Colors[colorVariation];
        }
    }
}
