using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Models.Track
{
    [TestClass]
    public class VectorSectionNodeTests
    {
        // EndLocation is a 3D WorldLocation computed at import time and stored on the node.
        // It represents the far end of the vector section and drives ITileCoordinateVector.OtherTile,
        // which TileIndexedList<T> uses to register the element in the destination tile bucket as well.

        // Basic construction: EndLocation carries whatever WorldLocation was supplied at construction time.
        [TestMethod]
        public void EndLocationIsStoredAndAccessibleTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 100, 5, 0);

            VectorSectionNode node = new VectorSectionNode(start, Tile.Zero, Vector3.Zero, end);

            Assert.AreEqual(end.Location.X, node.EndLocation.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Y, node.EndLocation.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Z, node.EndLocation.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Tile, node.EndLocation.Tile);
        }

        // When start and end are in the same tile, ITileCoordinateVector.Tile and OtherTile are equal.
        // TileIndexedList only adds the element to one tile bucket in this case.
        [TestMethod]
        public void OtherTileMatchesStartTileWhenEndpointIsInSameTileTest()
        {
            WorldLocation start = new WorldLocation(0, 0, -500, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 500, 0, 0);

            VectorSectionNode node = new VectorSectionNode(start, Tile.Zero, Vector3.Zero, end);
            ITileCoordinateVector tileVector = node;

            Assert.AreEqual(new Tile(0, 0), tileVector.Tile);
            Assert.AreEqual(new Tile(0, 0), tileVector.OtherTile);
        }

        // When the endpoint falls in a different tile, OtherTile differs from Tile.
        // TileIndexedList adds the element to both the start and end tile buckets so
        // queries on either tile find the section.
        [TestMethod]
        public void OtherTileDiffersFromStartTileWhenEndpointCrossesTileBoundaryTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation end = new WorldLocation(1, 0, 0, 0, 0);

            VectorSectionNode node = new VectorSectionNode(start, Tile.Zero, Vector3.Zero, end);
            ITileCoordinateVector tileVector = node;

            Assert.AreEqual(new Tile(0, 0), tileVector.Tile);
            Assert.AreEqual(new Tile(1, 0), tileVector.OtherTile);
            Assert.AreNotEqual(tileVector.Tile, tileVector.OtherTile);
        }

        // ITileCoordinate.Tile reflects the start location's tile, not the WorldTile parameter.
        // WorldTile is a separate world-file origin tile; the coordinate tile comes from Location.
        [TestMethod]
        public void TileReflectsStartLocationTileNotWorldTileTest()
        {
            WorldLocation start = new WorldLocation(2, 3, 0, 0, 0);
            WorldLocation end = new WorldLocation(2, 3, 100, 0, 0);
            Tile differentWorldTile = new Tile(99, 99);

            VectorSectionNode node = new VectorSectionNode(start, differentWorldTile, Vector3.Zero, end);
            ITileCoordinate tileCoord = node;

            Assert.AreEqual(new Tile(2, 3), tileCoord.Tile);
        }
    }
}
