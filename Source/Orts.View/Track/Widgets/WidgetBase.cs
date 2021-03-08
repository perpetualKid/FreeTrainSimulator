
using System;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.View.Xna;

namespace Orts.View.Track.Widgets
{
    internal abstract class WidgetBase
    {
        internal float Size;
    }

    internal abstract class PointWidget : WidgetBase, ITileCoordinate<Tile>
    {
        private protected PointD location;

        private protected Tile tile;

        public ref readonly Tile Tile => ref tile;

        internal ref readonly PointD Location => ref location;

        internal abstract void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None);

        private static class WidgetCache<T>
        {
            internal static readonly EnumArray<Color, ColorVariation> colors = new EnumArray<Color, ColorVariation>();
        }

        private protected static void SetColors<T>(Color color) where T : PointWidget
        {
            WidgetCache<T>.colors[ColorVariation.None] = color;
            WidgetCache<T>.colors[ColorVariation.Highlight] = color.HighlightColor(0.6);
            WidgetCache<T>.colors[ColorVariation.Complement] = color.ComplementColor();
            WidgetCache<T>.colors[ColorVariation.ComplementHighlight] = WidgetCache<T>.colors[ColorVariation.Complement].HighlightColor(0.6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static Color GetColor<T>(ColorVariation colorVariation) where T : PointWidget
        {
            return WidgetCache<T>.colors[colorVariation];
        }

        internal static void UpdateColor<T>(Color color) where T : PointWidget
        {
            SetColors<T>(color);
        }
    }

    internal abstract class VectorWidget : PointWidget, ITileCoordinateVector<Tile>
    {
        private protected PointD vector;

        private protected Tile otherTile;

        public ref readonly Tile OtherTile => ref otherTile;

        internal ref readonly PointD Vector => ref vector;

    }
}
