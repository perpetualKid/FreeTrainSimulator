using System;

using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;

namespace Orts.Common.Position
{
    public readonly struct PointD : IEquatable<PointD>
    {
        private static readonly PointD none = new PointD(0, 0);

        public static ref readonly PointD None => ref none;
#pragma warning disable CA1051 // Do not declare visible instance fields
        public readonly double X;
        public readonly double Y;
#pragma warning restore CA1051 // Do not declare visible instance fields

        public PointD(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static PointD FromWorldLocation(in WorldLocation location)
        {
            return new PointD(location.TileX * WorldLocation.TileSize + location.Location.X, location.TileZ * WorldLocation.TileSize + location.Location.Z);
        }

        public static PointD TileCenter(in ITile tile)
        {
            if (tile == null)
                throw new ArgumentNullException(nameof(tile));

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

        public static implicit operator Point(in PointD point)
        {
            return ToPoint(point);
        }

        public static Point ToPoint(in PointD point)
        {
            return new Point((int)point.X, (int)point.Y);
        }

        public static implicit operator PointD(in Point point)
        {
            return FromPoint(point);
        }

        public static PointD FromPoint(in Point point)
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
            return Add(left, right);
        }

        public static PointD Add(in PointD left, in PointD right)
        {
            return new PointD(left.X + right.X, left.Y + right.Y);
        }

        public static PointD operator -(in PointD left, in PointD right)
        {
            return Subtract(left, right);
        }

        public static PointD Subtract(in PointD left, in PointD right)
        {
            return new PointD(left.X - right.X, left.Y - right.Y);
        }

        public static PointD operator *(in PointD source, double scalar)
        {
            return Multiply(source, scalar);
        }

        public static PointD Multiply(in PointD source, double scalar)
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

        public override string ToString()
        {
            return $"{{X:{X} Y:{Y}}}";
        }
    }

}
