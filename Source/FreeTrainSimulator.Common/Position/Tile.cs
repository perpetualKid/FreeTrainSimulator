using System;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Position
{
    public readonly struct Tile : ITile, IEquatable<Tile>
    {
        public const int TileSize = 2048;

        public const int TileSizeOver2 = TileSize / 2;

        private static readonly Tile zero;

        public static ref readonly Tile Zero => ref zero;

        public short X { get; }

        public short Z { get; }

        public Tile(ITile source)
        {
            X = source?.X ?? throw new ArgumentNullException(nameof(source));
            Z = source.Z;
        }

        public Tile(short x, short z)
        {
            X = x;
            Z = z;
        }

        public Tile(int x, int z)
        {
            X = Convert.ToInt16(x);
            Z = Convert.ToInt16(z);
        }

        public int CompareTo(ITile other)
        {
            if (other == null)
                return 1;
            int result = X.CompareTo(other.X);
            if (result == 0)
                result = Z.CompareTo(other.Z);
            return result;
        }

        public override bool Equals(object obj)
        {
            return obj is ITile tile && Equals(tile);
        }

        public bool Equals(ITile other)
        {
            return X == other?.X && Z == other?.Z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Z);
        }

        public bool Equals(Tile other)
        {
            return Equals(other as ITile);
        }

        public static bool operator ==(in Tile left, in ITile right) => Equals(left, right);

        public static bool operator !=(in Tile left, in ITile right) => !Equals(left, right);

        public static bool operator <(in Tile left, in ITile right) => left.CompareTo(right) < 0;

        public static bool operator <=(in Tile left, in ITile right) => left.CompareTo(right) <= 0;

        public static bool operator >(in Tile left, in ITile right) => left.CompareTo(right) > 0;

        public static bool operator >=(in Tile left, in ITile right) => left.CompareTo(right) >= 0;

        public static Tile operator +(in Tile left, in ITile right) => new Tile(left.X + right.X, left.Z + right.Z);

        public static Tile operator -(in Tile left, in ITile right) => new Tile(left.X - right.X, left.Z - right.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short TileFromAbs(double value)
        {
            return Convert.ToInt16(Math.Round((int)(value / 1024) / 2.0, MidpointRounding.AwayFromZero));
        }

        public Vector3 TileVector() => new Vector3(X * 2048, 0, Z * 2048);

        public Tile North => new Tile(X, Z + 1);
        public Tile South => new Tile(X, Z - 1);
        public Tile West => new Tile(X - 1, Z);
        public Tile East => new Tile(X + 1, Z);
        public Tile NorthEast => new Tile(X + 1, Z + 1);
        public Tile SouthEast => new Tile(X + 1, Z - 1);
        public Tile NorthWest => new Tile(X - 1, Z + 1);
        public Tile SouthWest => new Tile(X - 1, Z - 1);

        public override string ToString()
        {
            return $"{{X:{X} Z:{Z}}}";
        }
    }
}
