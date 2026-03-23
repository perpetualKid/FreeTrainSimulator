using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace FreeTrainSimulator.Common.Position
{
    /// <summary>
    /// Generic interface for <seealso cref="TileIndexedList{TTileCoordinate, T}"/> to efficiently index and access elements by 2D tile index.
    /// Allows to enumerate elements within a certain "bounding box" area.
    /// Also has basic capabilities to find nearest element from a given position
    /// </summary>
    /// <typeparam name="TTileCoordinate"></typeparam>
    /// <typeparam name="T"></typeparam>
    public interface ITileIndexedList<out TTileCoordinate> : IEnumerable<TTileCoordinate> where TTileCoordinate : ITileCoordinate
    {
        /// <summary>
        /// Number of tiles in this list
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Sum of elements from all tiles
        /// </summary>
        int ItemCount { get; }

        IEnumerable<TTileCoordinate> BoundingBox(Tile center, int tileRadius);
        IEnumerable<TTileCoordinate> BoundingBox(Tile bottomLeft, Tile topRight);
#pragma warning disable CA1043 // Use Integral Or String Argument For Indexers
        IEnumerable<TTileCoordinate> this[Tile tile] { get; }
#pragma warning restore CA1043 // Use Integral Or String Argument For Indexers
        IEnumerable<TTileCoordinate> FindNearest(PointD position);
        IEnumerable<TTileCoordinate> FindNearest(PointD position, Tile bottomLeft, Tile topRight);
    }

    /// <summary>
    /// Generic type to efficiently index and access elements by 2D tile index.
    /// Allows to enumerate elements within a certain "bounding box" area.
    /// Also has basic capabilities to find nearest element from a given position.<br/>
    /// TTileCoordinate is the type of elements in this list. The type needs to implement <seealso cref="ITileCoordinate"/><br/>
    /// </summary>
    /// <typeparam name="TTileCoordinate"></typeparam>
    public class TileIndexedList<TTileCoordinate> : ITileIndexedList<TTileCoordinate> where TTileCoordinate : ITileCoordinate
    {
        private readonly SortedList<Tile, ImmutableArray<TTileCoordinate>> tiles;
        private readonly ImmutableArray<Tile> sortedIndexes;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public int Count => sortedIndexes.Length;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public int ItemCount { get; }

        public IList<TTileCoordinate> this[int index] { get => tiles[sortedIndexes[index]]; }

        public TileIndexedList(IEnumerable<TTileCoordinate> data)
        {
            int itemCount = 0;
            if (data is IEnumerable<ITileCoordinateVector> vectorData)
            {
                Dictionary<Tile, List<TTileCoordinate>> tileDict = [];
                foreach (ITileCoordinateVector item in vectorData)
                {
                    itemCount++;
                    TTileCoordinate tileItem = (TTileCoordinate)item;
                    if (!tileDict.TryGetValue(item.Tile, out List<TTileCoordinate> list))
                        tileDict[item.Tile] = list = new List<TTileCoordinate>();
                    list.Add(tileItem);
                    if (!item.Tile.Equals(item.OtherTile))
                    {
                        if (!tileDict.TryGetValue(item.OtherTile, out list))
                            tileDict[item.OtherTile] = list = new List<TTileCoordinate>();
                        list.Add(tileItem);
                    }
                }
                tiles = new SortedList<Tile, ImmutableArray<TTileCoordinate>>(
                    tileDict.ToDictionary(kvp => kvp.Key, kvp => ImmutableArray.CreateRange(kvp.Value)));
            }
            else
            {
                data = data is IList ? data : data.ToList();
                Dictionary<Tile, List<TTileCoordinate>> tileDict = [];
                foreach (TTileCoordinate item in data)
                {
                    itemCount++;
                    if (!tileDict.TryGetValue(item.Tile, out List<TTileCoordinate> list))
                        tileDict[item.Tile] = list = new List<TTileCoordinate>();
                    list.Add(item);
                }
                tiles = new SortedList<Tile, ImmutableArray<TTileCoordinate>>(
                    tileDict.ToDictionary(kvp => kvp.Key, kvp => ImmutableArray.CreateRange(kvp.Value)));
            }

            sortedIndexes = ImmutableArray.CreateRange(tiles.Keys);

            if (sortedIndexes.Length > 0 && (Tile.Zero == sortedIndexes[0] || Tile.Zero == sortedIndexes[^1]))
            {
                sortedIndexes = sortedIndexes.Remove(Tile.Zero);
                tiles.Remove(Tile.Zero);
            }
            ItemCount = itemCount;
        }

        /// <summary>
        /// Allocation-free enumerator used by <see langword="foreach"/> on the concrete type via duck typing.
        /// </summary>
        public struct Enumerator : IEnumerator<TTileCoordinate>
        {
            private readonly IList<ImmutableArray<TTileCoordinate>> tileValues;
            private readonly int tileCount;
            private int tileIndex;
            private int itemIndex;

            internal Enumerator(SortedList<Tile, ImmutableArray<TTileCoordinate>> tiles)
            {
                tileValues = tiles.Values;
                tileCount = tiles.Count;
                tileIndex = 0;
                itemIndex = -1;
            }

            public TTileCoordinate Current => tileValues[tileIndex][itemIndex];

            object IEnumerator.Current => Current!;

            public bool MoveNext()
            {
                itemIndex++;
                while (tileIndex < tileCount)
                {
                    if (itemIndex < tileValues[tileIndex].Length)
                        return true;
                    tileIndex++;
                    itemIndex = 0;
                }
                return false;
            }

            public void Reset()
            {
                tileIndex = 0;
                itemIndex = -1;
            }

            public readonly void Dispose() { }
        }

        /// <summary>
        /// Returns a struct <see cref="Enumerator"/>. Used by <see langword="foreach"/> via duck typing — no heap allocation.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(tiles);

        IEnumerator<TTileCoordinate> IEnumerable<TTileCoordinate>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

#pragma warning disable CA1043 // Use Integral Or String Argument For Indexers
        public IEnumerable<TTileCoordinate> this[Tile tile]
#pragma warning restore CA1043 // Use Integral Or String Argument For Indexers
        {
            get
            {
                if (!tiles.TryGetValue(tile, out ImmutableArray<TTileCoordinate> value))
                {
                    yield break;
                }

                foreach (TTileCoordinate item in value)
                {
                    yield return item;
                }
            }
        }

        public IEnumerable<TTileCoordinate> BoundingBox(Tile center, int tileRadius = 0)
        {
            Tile bottomLeft = new Tile(center.X - tileRadius, center.Z - tileRadius);
            Tile topRight = new Tile(center.X + tileRadius, center.Z + tileRadius);
            return BoundingBox(bottomLeft, topRight);
        }

        public IEnumerable<TTileCoordinate> BoundingBox(Tile bottomLeft, Tile topRight)
        {
            if (bottomLeft.CompareTo(topRight) > 0)
                throw new ArgumentOutOfRangeException(nameof(bottomLeft), $"{nameof(bottomLeft)} can not be larger than {nameof(topRight)}");

            if (sortedIndexes.Length == 0)
                yield break;

            int tileLookupIndex = FindTileIndexCeiling(bottomLeft);
            Tile end = sortedIndexes[FindTileIndexFloor(topRight)];

            Tile key = sortedIndexes[tileLookupIndex];

            while (key.Z < bottomLeft.Z && key.CompareTo(end) < 0)
            {
                tileLookupIndex = FindTileIndexCeiling(new Tile(key.X, bottomLeft.Z));
                key = sortedIndexes[tileLookupIndex];

                if (key.CompareTo(end) > 0)
                    yield break;
            }

            while (key.CompareTo(end) <= 0)
            {
                foreach (TTileCoordinate item in tiles.Values[tileLookupIndex])
                    yield return item;

                tileLookupIndex++;
                if (tileLookupIndex >= sortedIndexes.Length)
                    yield break;
                key = sortedIndexes[tileLookupIndex];

                while (key.Z < bottomLeft.Z && key.CompareTo(end) < 0)
                {
                    tileLookupIndex = FindTileIndexCeiling(new Tile(key.X, bottomLeft.Z));
                    key = sortedIndexes[tileLookupIndex];

                    if (key.CompareTo(end) > 0)
                        yield break;
                }
            }
        }

        public IEnumerable<TTileCoordinate> FindNearest(PointD position)
        {
            Tile current = Tile.TileFromAbs(position.X, position.Y);
            Tile key = sortedIndexes[FindTileIndexFloor(current)];
            double minDistance = double.MaxValue;
            if (current != key)
            {
                int tileDistance = Math.Abs(current.X - key.X) + Math.Abs(current.Z - key.Z);
                Tile tileMin = new Tile(current.X - tileDistance, current.Z - tileDistance);
                Tile tileMax = new Tile(current.X + tileDistance, current.Z + tileDistance);
                int tileMaxIndex = FindTileIndexFloor(tileMax);
                for (int i = FindTileIndexCeiling(tileMin); i <= tileMaxIndex; i++)
                {
                    double currentDistance;
                    if ((currentDistance = position.DistanceSquared(PointD.TileCenter(sortedIndexes[i]))) < minDistance)
                    {
                        minDistance = currentDistance;
                        key = sortedIndexes[i];
                    }
                }
            }
            return tiles[key];
        }

        public IEnumerable<TTileCoordinate> FindNearest(PointD position, Tile bottomLeft, Tile topRight)
        {
            Tile current = Tile.TileFromAbs(position.X, position.Y);
            Tile key = sortedIndexes[FindTileIndexFloor(current)];
            double minDistance = double.MaxValue;
            if (current != key)
            {
                int tileDistance = Math.Abs(current.X - key.X) + Math.Abs(current.Z - key.Z);
                Tile tileMin = new Tile(current.X - tileDistance, current.Z - tileDistance);
                if (tileMin.CompareTo(bottomLeft) < 0)
                    tileMin = bottomLeft;
                Tile tileMax = new Tile(current.X + tileDistance, current.Z + tileDistance);
                if (tileMax.CompareTo(topRight) > 0)
                    tileMax = topRight;
                int tileMaxIndex = FindTileIndexFloor(tileMax);
                for (int i = FindTileIndexCeiling(tileMin); i <= tileMaxIndex; i++)
                {
                    double currentDistance;
                    if ((currentDistance = position.DistanceSquared(PointD.TileCenter(sortedIndexes[i]))) < minDistance)
                    {
                        minDistance = currentDistance;
                        key = sortedIndexes[i];
                    }
                }
            }
            return tiles[key];
        }

        // Returns the index of the first tile >= possibleKey (ceiling)
        private int FindTileIndexCeiling(in Tile possibleKey)
        {
            int keyIndex = sortedIndexes.BinarySearch(possibleKey);
            if (keyIndex < 0)
            {
                keyIndex = ~keyIndex;
                if (keyIndex == sortedIndexes.Length)
                    keyIndex = sortedIndexes.Length - 1;
            }
            return keyIndex;
        }

        // Returns the index of the last tile <= possibleKey (floor)
        private int FindTileIndexFloor(in Tile possibleKey)
        {
            int keyIndex = sortedIndexes.BinarySearch(possibleKey);
            if (keyIndex < 0)
            {
                keyIndex = ~keyIndex;
                if (keyIndex > 0)
                    keyIndex--;
            }
            return keyIndex;
        }
    }
}
