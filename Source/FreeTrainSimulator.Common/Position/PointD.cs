using System;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Position
{
    public readonly struct PointD : IEquatable<PointD>
    {
        private static readonly PointD none = new PointD(0, 0);

        public static ref readonly PointD None => ref none;

        public readonly double X;
        public readonly double Y;

        public PointD(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static PointD FromWorldLocation(in WorldLocation location)
        {
            return new PointD(location.TileX * WorldLocation.TileSize + location.Location.X, location.TileZ * WorldLocation.TileSize + location.Location.Z);
        }

        public static WorldLocation ToWorldLocation(in PointD location)
        {
            int xTileDistance = (int)Math.Round((int)(location.X / 1024) / 2.0, MidpointRounding.AwayFromZero);
            int zTileDistance = (int)Math.Round((int)(location.Y / 1024) / 2.0, MidpointRounding.AwayFromZero);

            return new WorldLocation(xTileDistance, zTileDistance,
                new Vector3((float)(location.X - xTileDistance * WorldLocation.TileSize), 0, (float)(location.Y - zTileDistance * WorldLocation.TileSize)));
        }

        public static Tile ToTile(in PointD location)
        {
            return new Tile((int)Math.Round((int)(location.X / 1024) / 2.0, MidpointRounding.AwayFromZero), (int)Math.Round((int)(location.Y / 1024) / 2.0, MidpointRounding.AwayFromZero));
        }

        public static PointD TileCenter(in Tile tile)
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
            return !lhs.Equals(rhs);
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

        public static PointD operator /(in PointD source, double scalar)
        {
            return Divide(source, scalar);
        }

        public static PointD Divide(in PointD source, double scalar)
        {
            return new PointD(source.X / scalar, source.Y / scalar);
        }

        public override string ToString()
        {
            return $"{{X:{X} Y:{Y}}}";
        }
    }

}
