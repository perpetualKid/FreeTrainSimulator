using System;

namespace FreeTrainSimulator.Common.Position
{
    public interface ITile : IComparable<ITile>, IEquatable<ITile>
    {
        short X { get; }
        short Z { get; }
    }

    public interface ITileCoordinate<T> where T : struct, ITile
    {
        ref readonly T Tile { get; }
    }

    public interface ITileCoordinateVector<T> : ITileCoordinate<T> where T : struct, ITile
    {
        ref readonly T OtherTile { get; }
    }
}
