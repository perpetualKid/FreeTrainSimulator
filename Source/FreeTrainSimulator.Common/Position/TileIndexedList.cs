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
            data = data is IList ? data : data.ToList();
            if (data is IEnumerable<ITileCoordinateVector> vectorData)
            {
                IEnumerable<ITileCoordinateVector> singleTile = vectorData.Where(d => d.Tile.Equals(d.OtherTile)).ToList();
                IEnumerable<ITileCoordinateVector> multiTile = vectorData.Where(d => !d.Tile.Equals(d.OtherTile)).ToList();

                tiles = new SortedList<Tile, ImmutableArray<TTileCoordinate>>(
                    singleTile.Select(d => new { Segment = d, Tile = d.Tile }).
                    Concat(multiTile.Select(d => new { Segment = d, Tile = d.Tile })).
                    Concat(multiTile.Select(d => new { Segment = d, Tile = d.OtherTile })).GroupBy(d => d.Tile).
                    ToDictionary(g => g.Key, g => ImmutableArray.Create(g.Select(f => f.Segment).Cast<TTileCoordinate>().ToArray())));
            }
            else
            {
                tiles = new SortedList<Tile, ImmutableArray<TTileCoordinate>>(data.GroupBy(d => d.Tile).ToDictionary(g => g.Key, g => ImmutableArray.Create(g.ToArray())));
            }

            sortedIndexes = ImmutableArray.Create(tiles.Keys.ToArray());

            if (sortedIndexes.Length > 0 && (Tile.Zero == sortedIndexes[0] || Tile.Zero == sortedIndexes[^1]))
            {
                sortedIndexes = sortedIndexes.Remove(Tile.Zero);
                tiles.Remove(Tile.Zero);
            }
            ItemCount = data.Count();
        }

        public IEnumerator<TTileCoordinate> GetEnumerator()
        {
            foreach (ImmutableArray<TTileCoordinate> tileValues in tiles.Values)
            {
                foreach (TTileCoordinate item in tileValues)
                {
                    yield return item;
                }
            }
        }

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
            ArgumentNullException.ThrowIfNull(center);

            Tile bottomLeft = new Tile(center.X - tileRadius, center.Z - tileRadius);
            Tile topRight = new Tile(center.X + tileRadius, center.Z + tileRadius);
            return BoundingBox(bottomLeft, topRight);
        }

        public IEnumerable<TTileCoordinate> BoundingBox(Tile bottomLeft, Tile topRight)
        {
            ArgumentNullException.ThrowIfNull(bottomLeft);
            ArgumentNullException.ThrowIfNull(topRight);
            if (bottomLeft.CompareTo(topRight) > 0)
                throw new ArgumentOutOfRangeException(nameof(bottomLeft), $"{nameof(bottomLeft)} can not be larger than {nameof(topRight)}");

            if (sortedIndexes.Length == 0)
                yield break;

            int tileLookupIndex = FindNearestIndexFloor(bottomLeft);
            Tile end = sortedIndexes[FindNearestIndexCeiling(topRight)];

            Tile key = sortedIndexes[tileLookupIndex];

            while (key.Z < bottomLeft.Z && key.CompareTo(end) < 0)
            {
                tileLookupIndex = FindNearestIndexFloor(new Tile(key.X, bottomLeft.Z));
                key = sortedIndexes[tileLookupIndex];

                if (key.CompareTo(end) > 0)
                    yield break;
            }

            while (key.CompareTo(end) <= 0)
            {
                foreach (TTileCoordinate item in tiles[key])
                    yield return item;

                tileLookupIndex++;
                if (tileLookupIndex >= sortedIndexes.Length)
                    yield break;
                key = sortedIndexes[tileLookupIndex];

                while (key.Z < bottomLeft.Z && key.CompareTo(end) < 0)
                {
                    tileLookupIndex = FindNearestIndexFloor(new Tile(key.X, bottomLeft.Z));
                    key = sortedIndexes[tileLookupIndex];

                    if (key.CompareTo(end) > 0)
                        yield break;
                }
            }
        }

        public IEnumerable<TTileCoordinate> FindNearest(PointD position)
        {
            Tile current = Tile.TileFromAbs(position.X, position.Y);
            Tile key = sortedIndexes[FindNearestIndexCeiling(current)];
            double minDistance = double.MaxValue;
            if (current != key)
            {
                int tileDistance = Math.Abs(current.X - key.X) + Math.Abs(current.Z - key.Z);
                Tile tileMin = new Tile(current.X - tileDistance, current.Z - tileDistance);
                Tile tileMax = new Tile(current.X + tileDistance, current.Z + tileDistance);
                int tileMaxIndex = FindNearestIndexCeiling(tileMax);
                for (int i = FindNearestIndexFloor(tileMin); i < tileMaxIndex; i++)
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
            Tile key = sortedIndexes[FindNearestIndexCeiling(current)];
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
                int tileMaxIndex = FindNearestIndexCeiling(tileMax);
                for (int i = FindNearestIndexFloor(tileMin); i < tileMaxIndex; i++)
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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private int FindNearestIndexFloor(in Tile possibleKey)
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

        private int FindNearestIndexCeiling(in Tile possibleKey)
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
