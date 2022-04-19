using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Graphics.Xna;

namespace Orts.Graphics.MapView.Widgets
{
    public abstract class WidgetBase
    {
        internal float Size;
    }

    /// <summary>
    /// Graphical widget which has an exact, single point location only, such as a signal, junction etc
    /// </summary>
#pragma warning disable CA1708 // Identifiers should differ by more than case
    public abstract class PointWidget : WidgetBase, ITileCoordinate<Tile>
#pragma warning restore CA1708 // Identifiers should differ by more than case
    {
        private protected const double proximityTolerance = 1.0; //allow for a 1m proximity error (rounding, placement) when trying to locate points/locations along a track segment

        private protected PointD location;

        private protected Tile tile;

        public ref readonly Tile Tile => ref tile;

        internal ref readonly PointD Location => ref location;

        internal abstract void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1);

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

        internal static void OverrideColorVariation<T>(ColorVariation colorVariation, Color color) where T : PointWidget
        {
            WidgetCache<T>.colors[colorVariation] = color;
        }
    }

    /// <summary>
    /// Graphical widget which forms a vector (may be a curve as well), having a start point and end point such as a track segment, 
    /// or also 2D dimensional items such such a tile on the grid (having a lower left and upper right coordinate)
    /// </summary>
#pragma warning disable CA1708 // Identifiers should differ by more than case
    public abstract class VectorWidget : PointWidget, ITileCoordinateVector<Tile>
#pragma warning restore CA1708 // Identifiers should differ by more than case
    {

        private protected PointD vectorEnd;

        private protected Tile otherTile;

        public ref readonly Tile OtherTile => ref otherTile;

        internal ref readonly PointD Vector => ref vectorEnd;

        /// <summary>
        /// Squared distance of the given point on the straight line vector or on the arc (curved line)
        /// Points which are not betwween the start and end point, are considered to return NaN.
        /// For this, implementations will mostly allow for a small rounding offset (up to 1m)
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public abstract double DistanceSquared(in PointD point);

    }
}
