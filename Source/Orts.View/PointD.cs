using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;

namespace Orts.View
{
    internal readonly struct PointD : IEquatable<PointD>
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

        internal static PointD TileCenter(in ITile tile)
        {
            return new PointD(tile.X * Tile.TileSize, tile.Z * Tile.TileSize);
        }

        public double Distance(in PointD other)
        {
            return Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
        }

        public double DistanceSquared(in PointD other)
        {
            return (X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y);
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

        public double DotProduct(PointD other)
        {
            return X * other.X + Y * other.Y;
        }

        public static PointD operator +(in PointD left, in PointD right)
        {
            return new PointD(left.X + right.X, left.Y + right.Y);
        }

        public static PointD operator -(in PointD left, in PointD right)
        {
            return new PointD(left.X - right.X, left.Y - right.Y);
        }

        public static PointD operator *(in PointD source, double scalar)
        {
            return new PointD(source.X * scalar, source.Y * scalar);
        }

        public double DistanceToLineSegmentSquared(in PointD start, in PointD end)
        {
            // Compute length of line segment (squared) and handle special case of coincident points
            double segmentLengthSquared = start.DistanceSquared(end);
            if (segmentLengthSquared < double.Epsilon)  // start and end are considered same
            {
                return DistanceSquared(start);
            }

            // Use the magic formula to compute the "projection" of this point on the infinite line
            PointD lineSegment = end - start;
            double t = (this - start).DotProduct(lineSegment) / segmentLengthSquared;

            PointD closest;
            // Handle the two cases where the projection is not on the line segment, and the case where 
            //  the projection is on the segment
            if (t <= 0)
                closest = start;
            else if (t >= 1)
                closest = end;
            else
                closest = start + (lineSegment * t);
            return DistanceSquared(closest);
        }

        /// <summary>
        /// 
        /// </summary>
        public static double DistanceToLineSegmentSquared(in PointD start, in PointD end, in PointD source)
        {
            return source.DistanceToLineSegmentSquared(start, end);
        }

    }

}
