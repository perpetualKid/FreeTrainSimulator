using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Orts.Common.Position;

namespace Orts.View.Track.Widgets
{
    internal class TileIndexedList<ITileCoordinate, T> : IEnumerable<ITileCoordinate<T>> where T: struct, ITile
    {
        private readonly SortedList<ITile, List<ITileCoordinate<T>>> tiles;
        private readonly List<ITile> sortedIndexes;

        public TileIndexedList(IEnumerable<ITileCoordinate<T>> data)
        {
            tiles = new SortedList<ITile, List<ITileCoordinate<T>>>(data.GroupBy(d => d.Tile as ITile).ToDictionary(g => g.Key, g => g.ToList()));
            sortedIndexes = tiles.Keys.ToList();

            if (Tile.Zero == sortedIndexes[0] || Tile.Zero == sortedIndexes[sortedIndexes.Count - 1])
            {
                sortedIndexes.Remove(Tile.Zero);
                tiles.Remove(Tile.Zero);
            }
        }

        public IEnumerator<ITileCoordinate<T>> GetEnumerator()
        {
            foreach (List<ITileCoordinate<T>> list in tiles.Values)
            {
                foreach (ITileCoordinate<T> item in list)
                    yield return item;
            }
        }

        public IEnumerable<ITileCoordinate<T>> BoundingBox(ITile bottomLeft, ITile topRight)
        {
            int tileLookupIndex = FindNearestIndex(bottomLeft);
            if (tileLookupIndex > 0)
                tileLookupIndex--;
            ITile key;

            ITile end = sortedIndexes[FindNearestIndex(topRight)];

            do
            {
                key = sortedIndexes[tileLookupIndex];
                while (key.Z > topRight.Z && tileLookupIndex < sortedIndexes.Count-1)
                {
                    tileLookupIndex = FindNearestIndex(new Tile(key.X + 1, bottomLeft.Z));
                    key = sortedIndexes[tileLookupIndex];
                }

                foreach (ITileCoordinate<T> item in tiles[key])
                    yield return item;

                tileLookupIndex++;
            }
            while (key != end && tileLookupIndex < sortedIndexes.Count);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private int FindNearestIndex(ITile possibleKey)
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

    }
}
