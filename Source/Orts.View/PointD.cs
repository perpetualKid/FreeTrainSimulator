using Microsoft.Xna.Framework;

using Orts.Common.Position;

namespace Orts.View
{
    internal readonly struct PointD
    {
        private static readonly PointD none = new PointD(0, 0);

        public static ref readonly PointD None => ref none;
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

}
