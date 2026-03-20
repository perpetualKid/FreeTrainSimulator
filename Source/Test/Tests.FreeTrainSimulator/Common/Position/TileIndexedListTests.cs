using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common.Position;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common.Position
{
    [TestClass]
    public class TileIndexedListTests
    {
        // Concrete ITileCoordinate implementation for point-based tests
        private sealed record TestPoint : PointPrimitive
        {
            public TestPoint(in PointD location) : base(location) { }
        }

        // Concrete ITileCoordinateVector implementation for vector/segment tests
        private sealed record TestVector : VectorPrimitive
        {
            public TestVector(in PointD start, in PointD end) : base(start, end) { }
            public override double DistanceSquared(in PointD point) => double.NaN;
        }

        // All test PointD values and their tile assignments (TileSize = 2048):
        //   PointD(2048,    0) → Tile(1, 0)
        //   PointD(4096,    0) → Tile(2, 0)
        //   PointD(6144,    0) → Tile(3, 0)
        //   PointD(8192,    0) → Tile(4, 0)
        //   PointD(2048, 2048) → Tile(1, 1)
        //   PointD(4096, 2048) → Tile(2, 1)
        //   PointD(1100,    0) → Tile(1, 0)  (within tile bounds)
        //   PointD(2000,    0) → Tile(1, 0)  (within tile bounds)
        //   PointD(0,       0) → Tile(0, 0) = Tile.Zero

        // Empty source produces Count=0 and ItemCount=0
        [TestMethod]
        public void EmptyListHasZeroCountAndItemCount()
        {
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([]);

            Assert.HasCount(0, list);
            Assert.AreEqual(0, list.ItemCount);
        }

        // Empty list enumerates no items
        [TestMethod]
        public void EmptyListEnumeratesNoItems()
        {
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([]);

            Assert.IsEmpty(list.ToList());
        }

        // Count reflects the number of distinct tiles; ItemCount reflects the number of source items
        [TestMethod]
        public void CountAndItemCountReflectTilesAndItems()
        {
            // Two points in tile (1,0), one point in tile (2,0)
            TestPoint[] points =
            [
                new TestPoint(new PointD(2048, 0)),
                new TestPoint(new PointD(2100, 0)),
                new TestPoint(new PointD(4096, 0)),
            ];
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>(points);

            Assert.HasCount(2, list);
            Assert.AreEqual(3, list.ItemCount);
        }

        // Tile.Zero is excluded from the index when it falls at the boundary of the sorted tile list
        [TestMethod]
        public void TileZeroExcludedFromIndexWhenAtBoundary()
        {
            // PointD(0,0) → Tile(0,0) = Tile.Zero; Tile.Zero is first in sorted order here
            TestPoint[] points =
            [
                new TestPoint(new PointD(0, 0)),    // Tile.Zero
                new TestPoint(new PointD(2048, 0)), // Tile(1, 0)
            ];
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>(points);

            Assert.HasCount(1, list);     // only Tile(1,0) is indexed
            Assert.AreEqual(2, list.ItemCount); // both source items are counted
        }

        // GetEnumerator yields all items from all indexed tiles in sorted tile order
        [TestMethod]
        public void EnumeratorYieldsAllIndexedItems()
        {
            TestPoint p10a = new TestPoint(new PointD(2048, 0));
            TestPoint p10b = new TestPoint(new PointD(2100, 0));
            TestPoint p20 = new TestPoint(new PointD(4096, 0));
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10a, p10b, p20]);

            List<TestPoint> all = list.ToList();

            Assert.HasCount(3, all);
            CollectionAssert.Contains(all, p10a);
            CollectionAssert.Contains(all, p10b);
            CollectionAssert.Contains(all, p20);
        }

        // Items in the same tile are yielded consecutively before items in later tiles
        [TestMethod]
        public void EnumeratorYieldsItemsInSortedTileOrder()
        {
            TestPoint p20 = new TestPoint(new PointD(4096, 0)); // Tile(2,0)
            TestPoint p10 = new TestPoint(new PointD(2048, 0)); // Tile(1,0)
            // Inserted in reverse tile order to confirm sorted output
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p20, p10]);

            List<TestPoint> all = list.ToList();

            // Tile(1,0) sorts before Tile(2,0)
            Assert.AreEqual(p10, all[0]);
            Assert.AreEqual(p20, all[1]);
        }

        // this[Tile] returns all items stored under an occupied tile
        [TestMethod]
        public void TileIndexerReturnsItemsForOccupiedTile()
        {
            TestPoint p10 = new TestPoint(new PointD(2048, 0));
            TestPoint p20 = new TestPoint(new PointD(4096, 0));
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p20]);

            List<TestPoint> items = list[new Tile(1, 0)].ToList();

            Assert.HasCount(1, items);
            Assert.AreEqual(p10, items[0]);
        }

        // this[Tile] returns empty for a tile not present in the index
        [TestMethod]
        public void TileIndexerReturnsEmptyForAbsentTile()
        {
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([new TestPoint(new PointD(2048, 0))]);

            List<TestPoint> items = list[new Tile(5, 5)].ToList();

            Assert.HasCount(0, items);
        }

        // this[int] returns the items for the n-th tile in sorted tile order
        [TestMethod]
        public void IntIndexerReturnsTileItemsByPosition()
        {
            TestPoint p10 = new TestPoint(new PointD(2048, 0));
            TestPoint p20 = new TestPoint(new PointD(4096, 0));
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p20]);

            // Sorted: Tile(1,0) at position 0, Tile(2,0) at position 1
            IList<TestPoint> first = list[0];

            Assert.HasCount(1, first);
            Assert.AreEqual(p10, first[0]);
        }

        // A vector whose start and end are in the same tile appears once under that tile
        [TestMethod]
        public void SingleTileVectorAppearsOnceInItsOwnTile()
        {
            // Both endpoints in Tile(1,0)
            TestVector v = new TestVector(new PointD(1100, 0), new PointD(2000, 0));
            TileIndexedList<TestVector> list = new TileIndexedList<TestVector>([v]);

            Assert.HasCount(1, list);
            Assert.AreEqual(1, list.ItemCount);
            Assert.AreEqual(1, list[new Tile(1, 0)].Count());
        }

        // A vector spanning two tiles appears in both tiles; ItemCount counts the source item once
        [TestMethod]
        public void MultiTileVectorAppearsInBothTiles()
        {
            // Start in Tile(1,0), end in Tile(2,0)
            TestVector v = new TestVector(new PointD(2048, 0), new PointD(4096, 0));
            TileIndexedList<TestVector> list = new TileIndexedList<TestVector>([v]);

            Assert.HasCount(2, list);    // two distinct tiles indexed
            Assert.AreEqual(1, list.ItemCount); // one source item
            Assert.AreEqual(1, list[new Tile(1, 0)].Count());
            Assert.AreEqual(1, list[new Tile(2, 0)].Count());
        }

        // A diagonal vector spanning a 3×3 tile bounding box is indexed only in its start and end tiles;
        // intermediate tiles the vector geometrically crosses are not indexed
        [TestMethod]
        public void DiagonalVectorAcrossMultipleTilesIndexedOnlyInStartAndEndTiles()
        {
            // Start at Tile(1,0) (bottom-left corner) and end at Tile(3,2) (top-right corner) —
            // opposite corners of a 3×3 tile square. The midpoint (4096, 2048) lies in Tile(2,1).
            TestVector v = new TestVector(new PointD(2048, 0), new PointD(6144, 4096));
            TileIndexedList<TestVector> list = new TileIndexedList<TestVector>([v]);

            Assert.HasCount(2, list);           // only start and end tiles are indexed
            Assert.AreEqual(1, list.ItemCount); // one source item
            Assert.AreEqual(1, list[new Tile(1, 0)].Count()); // start tile indexed
            Assert.AreEqual(1, list[new Tile(3, 2)].Count()); // end tile indexed
            Assert.AreEqual(0, list[new Tile(2, 1)].Count()); // geometrically crossed tile is not indexed
        }

        // BoundingBox throws ArgumentOutOfRangeException when bottomLeft is greater than topRight
        [TestMethod]
        public void BoundingBoxThrowsWhenBottomLeftExceedsTopRight()
        {
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([new TestPoint(new PointD(2048, 0))]);

            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                list.BoundingBox(new Tile(2, 0), new Tile(1, 0)).ToList());
        }

        // BoundingBox on an empty list returns no items
        [TestMethod]
        public void BoundingBoxOnEmptyListReturnsEmpty()
        {
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([]);

            Assert.AreEqual(0, list.BoundingBox(new Tile(0, 0), new Tile(5, 5)).Count());
        }

        // BoundingBox with tileRadius=0 returns only the items on the exact center tile
        [TestMethod]
        public void BoundingBoxRadiusZeroReturnsSingleTile()
        {
            TestPoint p10 = new TestPoint(new PointD(2048, 0)); // Tile(1,0)
            TestPoint p20 = new TestPoint(new PointD(4096, 0)); // Tile(2,0)
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p20]);

            List<TestPoint> result = list.BoundingBox(new Tile(1, 0), 0).ToList();

            Assert.HasCount(1, result);
            Assert.AreEqual(p10, result[0]);
        }

        // BoundingBox with tileRadius=1 returns items in a 3×3 neighbourhood; excludes the tile just outside
        [TestMethod]
        public void BoundingBoxRadiusOneReturnsThreeByThreeNeighbourhood()
        {
            TestPoint p10 = new TestPoint(new PointD(2048, 0)); // Tile(1,0) — inside radius
            TestPoint p20 = new TestPoint(new PointD(4096, 0)); // Tile(2,0) — center
            TestPoint p30 = new TestPoint(new PointD(6144, 0)); // Tile(3,0) — inside radius
            TestPoint p40 = new TestPoint(new PointD(8192, 0)); // Tile(4,0) — outside radius
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p20, p30, p40]);

            List<TestPoint> result = list.BoundingBox(new Tile(2, 0), 1).ToList();

            Assert.HasCount(3, result);
            CollectionAssert.Contains(result, p10);
            CollectionAssert.Contains(result, p20);
            CollectionAssert.Contains(result, p30);
        }

        // BoundingBox returns all items within the explicit tile rectangle
        [TestMethod]
        public void BoundingBoxReturnsAllItemsWithinTileRectangle()
        {
            TestPoint p10 = new TestPoint(new PointD(2048, 0));    // Tile(1,0)
            TestPoint p20 = new TestPoint(new PointD(4096, 0));    // Tile(2,0)
            TestPoint p11 = new TestPoint(new PointD(2048, 2048)); // Tile(1,1)
            TestPoint p21 = new TestPoint(new PointD(4096, 2048)); // Tile(2,1)
            TestPoint p30 = new TestPoint(new PointD(6144, 0));    // Tile(3,0) — outside
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p20, p11, p21, p30]);

            List<TestPoint> result = list.BoundingBox(new Tile(1, 0), new Tile(2, 1)).ToList();

            Assert.HasCount(4, result);
            CollectionAssert.Contains(result, p10);
            CollectionAssert.Contains(result, p20);
            CollectionAssert.Contains(result, p11);
            CollectionAssert.Contains(result, p21);
        }

        // BoundingBox skips tiles whose Z is below the bottom edge of the bounding box
        [TestMethod]
        public void BoundingBoxSkipsTilesWithZBelowBottomEdge()
        {
            TestPoint p10 = new TestPoint(new PointD(2048, 0));    // Tile(1,0) — Z below bottomLeft.Z
            TestPoint p11 = new TestPoint(new PointD(2048, 2048)); // Tile(1,1) — inside box
            TestPoint p20 = new TestPoint(new PointD(4096, 0));    // Tile(2,0) — Z below bottomLeft.Z
            TestPoint p21 = new TestPoint(new PointD(4096, 2048)); // Tile(2,1) — inside box
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p11, p20, p21]);

            // BoundingBox from Tile(1,1) to Tile(2,1) — Z-column 0 rows are excluded
            List<TestPoint> result = list.BoundingBox(new Tile(1, 1), new Tile(2, 1)).ToList();

            Assert.HasCount(2, result);
            CollectionAssert.Contains(result, p11);
            CollectionAssert.Contains(result, p21);
        }

        // BoundingBox with a range that falls entirely in a gap between occupied tiles returns empty
        [TestMethod]
        public void BoundingBoxBetweenOccupiedTilesReturnsEmpty()
        {
            // Data is in Tile(1,0) and Tile(3,0); the query box Tile(2,0)–Tile(2,5) falls in the gap
            TestPoint p10 = new TestPoint(new PointD(2048, 0)); // Tile(1,0)
            TestPoint p30 = new TestPoint(new PointD(6144, 0)); // Tile(3,0)
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p30]);

            List<TestPoint> result = list.BoundingBox(new Tile(2, 0), new Tile(2, 5)).ToList();

            Assert.HasCount(0, result);
        }

        // FindNearest returns the items of the tile that contains the query position
        [TestMethod]
        public void FindNearestReturnsItemsFromContainingTile()
        {
            TestPoint p10 = new TestPoint(new PointD(2048, 0)); // Tile(1,0)
            TestPoint p30 = new TestPoint(new PointD(6144, 0)); // Tile(3,0)
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p30]);

            // Query position is inside Tile(1,0)
            IEnumerable<TestPoint> result = list.FindNearest(new PointD(2048, 0));

            CollectionAssert.Contains(result.ToList(), p10);
        }

        // FindNearest returns items from the geometrically nearest tile when the query position is not on any occupied tile
        [TestMethod]
        public void FindNearestReturnsNearestTileWhenPositionNotOnOccupiedTile()
        {
            TestPoint p10 = new TestPoint(new PointD(2048, 0)); // Tile(1,0) — center at (2048, 0)
            TestPoint p30 = new TestPoint(new PointD(6144, 0)); // Tile(3,0) — center at (6144, 0)
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p30]);

            // Position at (5000, 0) in Tile(2,0): distance² to Tile(1,0) center = 8714304, to Tile(3,0) center = 1308736
            IEnumerable<TestPoint> result = list.FindNearest(new PointD(5000, 0));

            CollectionAssert.Contains(result.ToList(), p30);
        }

        // FindNearest with bounds restricts the nearest-tile search to the given tile rectangle
        [TestMethod]
        public void FindNearestWithBoundsLimitsSearchArea()
        {
            TestPoint p10 = new TestPoint(new PointD(2048, 0)); // Tile(1,0) — closer overall
            TestPoint p30 = new TestPoint(new PointD(6144, 0)); // Tile(3,0) — inside bounds
            TileIndexedList<TestPoint> list = new TileIndexedList<TestPoint>([p10, p30]);

            // Position in Tile(2,0) (unoccupied). Bounds Tile(3,0)–Tile(5,0) exclude Tile(1,0).
            IEnumerable<TestPoint> result = list.FindNearest(new PointD(4096, 0), new Tile(3, 0), new Tile(5, 0));

            CollectionAssert.Contains(result.ToList(), p30);
        }
    }
}
