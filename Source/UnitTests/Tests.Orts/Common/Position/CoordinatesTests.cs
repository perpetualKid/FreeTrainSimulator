using System;

using FreeTrainSimulator.Common.Xna;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Common.Position;

namespace Tests.Orts.Common.Position
{
    [TestClass]
    public class CoordinatesTests
    {
        [TestMethod]
        public void WorldLocationNormalizeTest()
        {
            WorldLocation location = new WorldLocation(0, 0, 3834, 0, -4118, true);

            Assert.AreEqual(2, location.Tile.X);
            Assert.AreEqual(-262, location.Location.X);
            Assert.AreEqual(-2, location.Tile.Z);
            Assert.AreEqual(-22, location.Location.Z);

            location = new WorldLocation(0, 0, 3834, 0, -1088, true);
            Assert.AreEqual(-1, location.Tile.Z);
            Assert.AreEqual(960, location.Location.Z);
        }

        [TestMethod]
        public void WorldLocationNormalizeToTest()
        {
            WorldLocation location = new WorldLocation(-1, 1, 3834, 0, -4118).NormalizeTo(4, 4);

            Assert.AreEqual(4, location.Tile.X);
            Assert.AreEqual(-6406, location.Location.X);
            Assert.AreEqual(4, location.Tile.Z);
            Assert.AreEqual(-10262, location.Location.Z);
        }

        [TestMethod]
        public void WorldLocationElevationTest()
        {
            WorldLocation location = new WorldLocation(0, 0, 0, 0, 0).SetElevation(123.4f);
            Assert.AreEqual(123.4f, location.Location.Y);
            location = location.ChangeElevation(-10.2f);
            Assert.AreEqual(113.2f, location.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        [TestMethod]
        public void WorldLocationDistanceZeroTest()
        {
            WorldLocation location1 = new WorldLocation();
            WorldLocation location2 = new WorldLocation();

            Assert.IsTrue(WorldLocation.Within(location1, location2, 0));
            Assert.AreEqual(0, WorldLocation.GetDistanceSquared(location1, location2));
            Assert.AreEqual(Microsoft.Xna.Framework.Vector3.Zero, WorldLocation.GetDistance(location1, location2));
            Assert.AreEqual(Microsoft.Xna.Framework.Vector2.Zero, WorldLocation.GetDistance2D(location1, location2));
        }

        [TestMethod]
        public void WorldLocationDistanceTest()
        {
            WorldLocation location1 = new WorldLocation();
            WorldLocation location2 = new WorldLocation(1, -1, Microsoft.Xna.Framework.Vector3.Zero);

            Assert.AreEqual(2048 * 2048 + 2048 * 2048, WorldLocation.GetDistanceSquared(location1, location2));
            Assert.IsTrue(WorldLocation.Within(location1, location2, (float)Math.Sqrt(2048 * 2048 * 2) + 1));

            Assert.AreEqual(new Microsoft.Xna.Framework.Vector3(2048, 0, -2048), WorldLocation.GetDistance(location1, location2));
            Assert.AreEqual(new Microsoft.Xna.Framework.Vector2(2048, -2048), WorldLocation.GetDistance2D(location1, location2));
        }

        [TestMethod]
        public void WorldLocationOperatorTest()
        {
            WorldLocation location1 = new WorldLocation();
            WorldLocation location2 = new WorldLocation(1, 1, Microsoft.Xna.Framework.Vector3.One);

            Assert.IsTrue(location1.Equals(WorldLocation.None));
            Assert.IsTrue(location1 != location2);

            Assert.IsFalse(Equals(WorldLocation.None.Equals(new object())));
        }

        [TestMethod]
        public void WorldPositionCtorTest()
        {
            Assert.AreEqual(WorldPosition.None, new WorldPosition(Tile.Zero, Microsoft.Xna.Framework.Matrix.Identity));
            WorldLocation location = new WorldLocation(3, 4, 5, 6, 7);
            WorldPosition position = new WorldPosition(location);
            Assert.AreEqual(location.Location, position.Location);
            Assert.AreEqual(location, position.WorldLocation);

            Assert.AreEqual("{TileX:3 TileZ:4 X:5 Y:6 Z:7}", position.ToString());
        }

        [TestMethod]
        public void WorldPositionTranslationTest()
        {
            WorldLocation location = new WorldLocation(3, 4, 5, 6, 7);
            WorldPosition position = new WorldPosition(location);
            Assert.AreEqual(position.SetTranslation(Microsoft.Xna.Framework.Vector3.One), position.SetTranslation(1, 1, 1));
        }

        [TestMethod]
        public void WorldPositionNormalizeTest()
        {
            WorldPosition position = new WorldPosition(new WorldLocation(0, 0, 3834, 0, -4118)).Normalize();

            Assert.AreEqual(2, position.Tile.X);
            Assert.AreEqual(-262, position.Location.X);
            Assert.AreEqual(2, position.Tile.Z);
            Assert.AreEqual(-22, position.Location.Z);

            position = new WorldPosition(Tile.Zero, MatrixExtension.SetTranslation(Microsoft.Xna.Framework.Matrix.Identity, 3834, 0, -4118)).Normalize();

            Assert.AreEqual(2, position.Tile.X);
            Assert.AreEqual(-262, position.Location.X);
            Assert.AreEqual(-2, position.Tile.Z);
            Assert.AreEqual(22, position.Location.Z);
            Assert.AreEqual(-22, position.XNAMatrix.M43);
        }

        [TestMethod]
        public void WorldPositionNormalizeToTest()
        {
            WorldPosition position = new WorldPosition(new Tile(-1, 1), MatrixExtension.SetTranslation(Microsoft.Xna.Framework.Matrix.Identity, 3834, 0, -4118)).NormalizeTo(new Tile(4, 4));

            Assert.AreEqual(4, position.Tile.X);
            Assert.AreEqual(-6406, position.Location.X);
            Assert.AreEqual(4, position.Tile.Z);
            Assert.AreEqual(10262, position.Location.Z);
            Assert.AreEqual(-10262, position.XNAMatrix.M43);
        }

    }
}
