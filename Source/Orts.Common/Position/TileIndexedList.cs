using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Orts.Common.Position
{
    /// <summary>
    /// Generic interface for <seealso cref="TileIndexedList{TTileCoordinate, T}"/> to efficiently index and access elements by 2D tile index.
    /// Allows to enumerate elements within a certain "bounding box" area.
    /// Also has basic capabilities to find nearest element from a given position
    /// </summary>
    /// <typeparam name="TTileCoordinate"></typeparam>
    /// <typeparam name="T"></typeparam>
    public interface ITileIndexedList<out TTileCoordinate, T> : IEnumerable<TTileCoordinate> where T : struct, ITile where TTileCoordinate : ITileCoordinate<T>
    {
        /// <summary>
        /// Number of tiles in this list
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Sum of elements from all tiles
        /// </summary>
        int ItemCount { get; }

        IEnumerable<TTileCoordinate> BoundingBox(ITile center, int tileRadius);
        IEnumerable<TTileCoordinate> BoundingBox(ITile bottomLeft, ITile topRight);
#pragma warning disable CA1043 // Use Integral Or String Argument For Indexers
        IEnumerable<TTileCoordinate> this[ITile tile] { get; }
#pragma warning restore CA1043 // Use Integral Or String Argument For Indexers
        IEnumerable<TTileCoordinate> FindNearest(PointD position);
        IEnumerable<TTileCoordinate> FindNearest(PointD position, ITile bottomLeft, ITile topRight);
    }

    /// <summary>
    /// Generic type to efficiently index and access elements by 2D tile index.
    /// Allows to enumerate elements within a certain "bounding box" area.
    /// Also has basic capabilities to find nearest element from a given position.<br/>
    /// TTileCoordinate is the type of elements in this list. The type needs to implement <seealso cref="ITileCoordinate{T}"/><br/>
    /// T is the tile-type, implementing <seealso cref="ITile"/>
    /// </summary>
    /// <typeparam name="TTileCoordinate"></typeparam>
    /// <typeparam name="T"></typeparam>
    public class TileIndexedList<TTileCoordinate, T> : ITileIndexedList<TTileCoordinate, T> where T : struct, ITile where TTileCoordinate : ITileCoordinate<T>
    {
        private readonly SortedList<ITile, List<TTileCoordinate>> tiles;
        private readonly List<ITile> sortedIndexes;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public int Count => sortedIndexes.Count;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public int ItemCount { get; }

        public IList<TTileCoordinate> this[int index] { get => tiles[sortedIndexes[index]]; }

        public TileIndexedList(IEnumerable<TTileCoordinate> data)
        {
            data = data.ToList();
            if (data is IEnumerable<ITileCoordinateVector<T>> vectorData)
            {
                IEnumerable<ITileCoordinateVector<T>> singleTile = vectorData.Where(d => d.Tile.Equals(d.OtherTile));
                IEnumerable<ITileCoordinateVector<T>> multiTile = vectorData.Where(d => !d.Tile.Equals(d.OtherTile));

                tiles = new SortedList<ITile, List<TTileCoordinate>>(
                    singleTile.Select(d => new { Segment = d, Tile = d.Tile as ITile }).
                    Concat(multiTile.Select(d => new { Segment = d, Tile = d.Tile as ITile })).
                    Concat(multiTile.Select(d => new { Segment = d, Tile = d.OtherTile as ITile })).GroupBy(d => d.Tile).
                    ToDictionary(g => g.Key, g => g.Select(f => f.Segment).Cast<TTileCoordinate>().ToList()));
            }
            else
            {
                tiles = new SortedList<ITile, List<TTileCoordinate>>(data.GroupBy(d => d.Tile as ITile).ToDictionary(g => g.Key, g => g.ToList()));
            }
            sortedIndexes = tiles.Keys.ToList();

            if (sortedIndexes.Count > 0 && (Tile.Zero == sortedIndexes[0] || Tile.Zero == sortedIndexes[^1]))
            {
                sortedIndexes.Remove(Tile.Zero);
                tiles.Remove(Tile.Zero);
            }
            ItemCount = data.Count();
        }

        public IEnumerator<TTileCoordinate> GetEnumerator()
        {
            foreach (List<TTileCoordinate> list in tiles.Values)
            {
                foreach (TTileCoordinate item in list)
                    yield return item;
            }
        }

#pragma warning disable CA1043 // Use Integral Or String Argument For Indexers
        public IEnumerable<TTileCoordinate> this[ITile tile]
#pragma warning restore CA1043 // Use Integral Or String Argument For Indexers
        {
            get
            {
                if (!tiles.ContainsKey(tile))
                    yield break;
                foreach (TTileCoordinate item in tiles[tile])
                    yield return item;
            }
        }

        public IEnumerable<TTileCoordinate> BoundingBox(ITile center, int tileRadius = 0)
        { 
            ArgumentNullException.ThrowIfNull(center);

            Tile bottomLeft = new Tile(center.X - tileRadius, center.Z - tileRadius);
            Tile topRight = new Tile(center.X + tileRadius, center.Z + tileRadius);
            return BoundingBox(bottomLeft, topRight);
        }

        public IEnumerable<TTileCoordinate> BoundingBox(ITile bottomLeft, ITile topRight)
        {
            ArgumentNullException.ThrowIfNull(bottomLeft);
            ArgumentNullException.ThrowIfNull(topRight);
            if (bottomLeft.CompareTo(topRight) > 0)
                throw new ArgumentOutOfRangeException(nameof(bottomLeft), $"{nameof(bottomLeft)} can not be larger than {nameof(topRight)}");

            if (sortedIndexes.Count == 0)
                yield break;

            int tileLookupIndex = FindNearestIndexFloor(bottomLeft);
            ITile end = sortedIndexes[FindNearestIndexCeiling(topRight)];

            ITile key = sortedIndexes[tileLookupIndex];

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
                if (tileLookupIndex >= sortedIndexes.Count)
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
            Tile current = new Tile(Tile.TileFromAbs(position.X), Tile.TileFromAbs(position.Y));
            ITile key = sortedIndexes[FindNearestIndexCeiling(current)];
            double minDistance = double.MaxValue;
            if (current != key)
            {
                int tileDistance = Math.Abs(current.X - key.X) + Math.Abs(current.Z - key.Z);
                ITile tileMin = new Tile(current.X - tileDistance, current.Z - tileDistance);
                ITile tileMax = new Tile(current.X + tileDistance, current.Z + tileDistance);
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

        public IEnumerable<TTileCoordinate> FindNearest(PointD position, ITile bottomLeft, ITile topRight)
        {
            Tile current = new Tile(Tile.TileFromAbs(position.X), Tile.TileFromAbs(position.Y));
            ITile key = sortedIndexes[FindNearestIndexCeiling(current)];
            double minDistance = double.MaxValue;
            if (current != key)
            {
                int tileDistance = Math.Abs(current.X - key.X) + Math.Abs(current.Z - key.Z);
                ITile tileMin = new Tile(current.X - tileDistance, current.Z - tileDistance);
                if (tileMin.CompareTo(bottomLeft) < 0)
                    tileMin = bottomLeft;
                ITile tileMax = new Tile(current.X + tileDistance, current.Z + tileDistance);
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

        private int FindNearestIndexFloor(ITile possibleKey)
        {
            int keyIndex = sortedIndexes.BinarySearch(possibleKey);
            if (keyIndex < 0)
            {
                keyIndex = ~keyIndex;
                if (keyIndex == sortedIndexes.Count)
                    keyIndex = sortedIndexes.Count - 1;
            }
            return keyIndex;
        }

        private int FindNearestIndexCeiling(ITile possibleKey)
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
