using System.Numerics;

using Microsoft.Xna.Framework;

using Orts.Common.Position;

namespace Orts.View.Track.Widgets
{
    internal readonly struct PointD
    {
        internal readonly double X;
        internal readonly double Y;

        internal PointD(double x, double y)
        {
            X = x;
            Y = y;
        }

        internal static PointD FromWorldLocation(in WorldLocation location)
        {
            return new PointD(location.TileX * WorldLocation.TileSize + location.Location.X, location.TileZ * WorldLocation.TileSize + location.Location.Z);
        }

        public static implicit operator Point(PointD point)
        {
            return new Point((int)point.X, (int)point.Y);
        }

        public static implicit operator PointD(Point point)
        {
            return new PointD(point.X, point.Y);
        }

    }

    internal class WidgetBase
    {
        internal float Width;
    }

    internal class PointWidget: WidgetBase
    {
        private protected PointD location;

        internal ref readonly PointD Location => ref location;
    }

    internal class VectorWidget : PointWidget
    {
        private protected PointD vector;

        internal ref readonly PointD Vector => ref vector;
    }
}
