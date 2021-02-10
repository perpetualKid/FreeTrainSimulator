using System;

namespace Orts.Common.Position
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

}
