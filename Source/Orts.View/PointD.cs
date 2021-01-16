using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;

namespace Orts.View
{
    internal readonly struct PointD: IEquatable<PointD>
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

        public override bool Equals(object obj)
        {
            return obj is PointD point && Equals(point);
        }

        public bool Equals(PointD other)
        {
            return other.X == X && other.Y == Y;
        }

        public static implicit operator Point(PointD point)
        {
            return new Point((int)point.X, (int)point.Y);
        }

        public static implicit operator PointD(Point point)
        {
            return new PointD(point.X, point.Y);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode();
        }

        public static bool operator ==(PointD lhs, PointD rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(PointD lhs, PointD rhs)
        {
            return !(lhs.Equals(rhs));
        }
    }

}
