using System;

namespace Orts.Common.Position
{
    public readonly struct Tile : ITile, IEquatable<Tile>
    {
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
            X = (short)x;
            Z = (short)z;
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
            return (obj is ITile tile) && Equals(tile);
        }

        public bool Equals(ITile other)
        {
            return X == other?.X && Z == other?.Z;
        }

        public override int GetHashCode()
        {
            return (X, Z).GetHashCode();
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

    }
}
