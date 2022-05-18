using System;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Graphics.Xna;

namespace Orts.Graphics.MapView.Widgets
{
    internal static class WidgetColorCache<T>
    {
        internal static readonly EnumArray<Color, ColorVariation> colors = new EnumArray<Color, ColorVariation>();
    }

    /// <summary>
    /// Graphical widget which has an exact, single point location only, such as a signal, junction etc
    /// </summary>
    public abstract class PointWidget : PointPrimitive
    {
        protected PointWidget() : base()
        { }

        protected PointWidget(in WorldLocation location) : base(location)
        { }

        protected PointWidget(in PointD location) : base(location)
        { }

        internal abstract void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1);

        private protected static void SetColors<T>(Color color) where T : PointWidget
        {
            WidgetColorCache<T>.colors[ColorVariation.None] = color;
            WidgetColorCache<T>.colors[ColorVariation.Highlight] = color.HighlightColor(0.6);
            WidgetColorCache<T>.colors[ColorVariation.Complement] = color.ComplementColor();
            WidgetColorCache<T>.colors[ColorVariation.ComplementHighlight] = WidgetColorCache<T>.colors[ColorVariation.Complement].HighlightColor(0.6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal static Color GetColor<T>(ColorVariation colorVariation) where T : PointWidget
        {
            return WidgetColorCache<T>.colors[colorVariation];
        }

        internal static void UpdateColor<T>(Color color) where T : PointWidget
        {
            SetColors<T>(color);
        }

        internal static void OverrideColorVariation<T>(ColorVariation colorVariation, Color color) where T : PointWidget
        {
            WidgetColorCache<T>.colors[colorVariation] = color;
        }
    }

    /// <summary>
    /// Graphical widget which forms a vector (may be a curve as well), having a start point and end point such as a track segment, 
    /// or also 2D dimensional items such such a tile on the grid (having a lower left and upper right coordinate)
    /// </summary>
    public abstract class VectorWidget : VectorPrimitive
    {
        protected VectorWidget() : base()
        { }

        protected VectorWidget(in WorldLocation start, in WorldLocation end) : base(start, end)
        { }

        protected VectorWidget(in PointD start, in PointD end) : base(start, end)
        { }

        protected VectorWidget(VectorWidget source) : base(source?.Location ?? throw new ArgumentNullException(nameof(source)), source.Vector)
        { }

        internal abstract void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1);

        private protected static void SetColors<T>(Color color) where T : VectorWidget
        {
            WidgetColorCache<T>.colors[ColorVariation.None] = color;
            WidgetColorCache<T>.colors[ColorVariation.Highlight] = color.HighlightColor(0.6);
            WidgetColorCache<T>.colors[ColorVariation.Complement] = color.ComplementColor();
            WidgetColorCache<T>.colors[ColorVariation.ComplementHighlight] = WidgetColorCache<T>.colors[ColorVariation.Complement].HighlightColor(0.6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal static Color GetColor<T>(ColorVariation colorVariation) where T : VectorWidget
        {
            return WidgetColorCache<T>.colors[colorVariation];
        }

        internal static void UpdateColor<T>(Color color) where T : VectorWidget
        {
            SetColors<T>(color);
        }

        internal static void OverrideColorVariation<T>(ColorVariation colorVariation, Color color) where T : VectorWidget
        {
            WidgetColorCache<T>.colors[colorVariation] = color;
        }

    }
}
