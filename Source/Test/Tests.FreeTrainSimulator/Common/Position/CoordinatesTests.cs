using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common.Position
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
            WorldLocation location = new WorldLocation(new Tile(-1, 1), 3834, 0, -4118).NormalizeTo(new Tile(4, 4));

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
            Assert.AreEqual(Microsoft.Xna.Framework.Vector3.Zero, WorldLocation.GetDistanceVector(location1, location2));
            Assert.AreEqual(Microsoft.Xna.Framework.Vector2.Zero, WorldLocation.GetDistance2D(location1, location2));
        }

        [TestMethod]
        public void WorldLocationDistanceTest()
        {
            WorldLocation location1 = new WorldLocation();
            WorldLocation location2 = new WorldLocation(1, -1, Microsoft.Xna.Framework.Vector3.Zero);

            Assert.AreEqual((2048 * 2048) + (2048 * 2048), WorldLocation.GetDistanceSquared(location1, location2));
            Assert.IsTrue(WorldLocation.Within(location1, location2, (float)Math.Sqrt(2048 * 2048 * 2) + 1));

            Assert.AreEqual(new Microsoft.Xna.Framework.Vector3(2048, 0, -2048), WorldLocation.GetDistanceVector(location1, location2));
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
        public void WorldLocationInterpolateAlongTileTest()
        {
            WorldLocation start = new WorldLocation(-2, -3, 1001, 5, 1);
            WorldLocation end = new WorldLocation(0, -3, -999, 5, 1);

            WorldLocation result = WorldLocation.InterpolateAlong(start, end, 1328f);

            Assert.AreEqual(new Tile(-1, -3), result.Tile);
        }

        [TestMethod]
        public void WorldLocationInterpolateAlongTileAtStartTest()
        {
            WorldLocation start = new WorldLocation(-2, -3, 1001, 5, 1);
            WorldLocation end = new WorldLocation(0, -3, -999, 5, 1);

            WorldLocation result = WorldLocation.InterpolateAlong(start, end, 0f);

            Assert.AreEqual(start, result);
        }

        [TestMethod]
        public void WorldLocationPointAlongDirectionVectorTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 0, 0, 0);

            WorldLocation result = WorldLocation.PointAlongDirection(start, new Microsoft.Xna.Framework.Vector3(2, 0, 0), 10f);

            Assert.AreEqual(new WorldLocation(0, 0, 10, 0, 0), result);
        }

        [TestMethod]
        public void WorldLocationPointAlongDirectionLocationTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 2040, 0, 0);
            WorldLocation end = new WorldLocation(1, 0, 0, 0, 0);

            WorldLocation result = WorldLocation.PointAlongDirection(start, end, 16f);

            Assert.AreEqual(new WorldLocation(1, 0, 8, 0, 0), result);
        }

        [TestMethod]
        public void WorldLocationPointAlongDirectionNegativeDistanceThrowsTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 0, 0, 0);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WorldLocation.PointAlongDirection(start, Microsoft.Xna.Framework.Vector3.UnitX, -1f));
        }

        [TestMethod]
        public void WorldLocationPointAlongDirectionZeroDistanceWithZeroVectorReturnsStartTest()
        {
            WorldLocation start = new WorldLocation(1, -2, 12, 3, -44);

            WorldLocation result = WorldLocation.PointAlongDirection(start, Microsoft.Xna.Framework.Vector3.Zero, 0f);

            Assert.AreEqual(start, result);
        }

        // start=(10,0,0), end=(0,0,10), arcAngle=-π/2, radius=10 → center=(0,0,0)
        // Verified: midpoint=(5,0,5), perp=(1/√2,0,1/√2), offset=(-5,0,-5)
        [TestMethod]
        public void WorldLocationFindArcCenterQuarterCircleCounterClockwiseTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);

            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, -MathF.PI / 2, 10f);

            Assert.AreEqual(0, center.Tile.X);
            Assert.AreEqual(0, center.Tile.Z);
            Assert.AreEqual(0f, center.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // start=(10,0,0), end=(0,0,10), arcAngle=+π/2, radius=10 → center=(10,0,10)
        // Verified: midpoint=(5,0,5), perp=(1/√2,0,1/√2), offset=(5,0,5)
        [TestMethod]
        public void WorldLocationFindArcCenterQuarterCircleClockwiseTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);

            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, MathF.PI / 2, 10f);

            Assert.AreEqual(0, center.Tile.X);
            Assert.AreEqual(0, center.Tile.Z);
            Assert.AreEqual(10f, center.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(10f, center.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // start=(-10,0,0), end=(10,0,0), arcAngle=π, radius=10 → center=(0,0,0) (midpoint, cos(π/2)=0)
        [TestMethod]
        public void WorldLocationFindArcCenterSemicircleTest()
        {
            WorldLocation start = new WorldLocation(0, 0, -10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 10, 0, 0);

            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, MathF.PI, 10f);

            Assert.AreEqual(0, center.Tile.X);
            Assert.AreEqual(0, center.Tile.Z);
            Assert.AreEqual(0f, center.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        [TestMethod]
        public void WorldLocationFindArcCenterEquidistantFromBothEndpointsTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);
            float radius = 10f;

            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, -MathF.PI / 2, radius);

            Assert.AreEqual((double)radius * radius, WorldLocation.GetDistanceSquared(center, start), EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual((double)radius * radius, WorldLocation.GetDistanceSquared(center, end), EqualityPrecisionDelta.FloatPrecisionDelta);
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
