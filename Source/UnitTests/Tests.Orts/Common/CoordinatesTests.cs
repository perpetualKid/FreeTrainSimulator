using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common;
using Orts.Common.Xna;

namespace Tests.Orts.Common
{
    [TestClass]
    public class CoordinatesTests
    {
        [TestMethod]
        public void WorldLocationNormalizeTest()
        {
            WorldLocation location = new WorldLocation(0, 0, 3834, 0, -4118, true);

            Assert.AreEqual(2, location.TileX);
            Assert.AreEqual(-262, location.Location.X);
            Assert.AreEqual(-2, location.TileZ);
            Assert.AreEqual(-22, location.Location.Z);

            location = new WorldLocation(0, 0, 3834, 0, -1088, true);
            Assert.AreEqual(-1, location.TileZ);
            Assert.AreEqual(960, location.Location.Z);
        }

        [TestMethod]
        public void WorldLocationNormalizeToTest()
        {
            WorldLocation location = new WorldLocation(-1, 1, 3834, 0, -4118).NormalizeTo(4, 4);

            Assert.AreEqual(4, location.TileX);
            Assert.AreEqual(-6406, location.Location.X);
            Assert.AreEqual(4, location.TileZ);
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
        public void WorldLocationSerializationTest()
        {
            WorldLocation location1 = new WorldLocation(-17, 13, -45.3f, 62.8f, 91.7f);
            WorldLocation location2;

            using (BinaryWriter writer = new BinaryWriter(new MemoryStream()))
            {
                WorldLocation.Save(location1, writer);
                writer.BaseStream.Position = 0;
                using (BinaryReader reader = new BinaryReader(writer.BaseStream))
                {
                    location2 = WorldLocation.Restore(reader);
                }
            }

            Assert.IsTrue(WorldLocation.Equals(location1, location2));
        }

        [TestMethod]
        public void WorldPositionCtorTest()
        {
            Assert.AreEqual(WorldPosition.None, new WorldPosition(0, 0, Microsoft.Xna.Framework.Matrix.Identity));
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

            Assert.AreEqual(2, position.TileX);
            Assert.AreEqual(-262, position.Location.X);
            Assert.AreEqual(2, position.TileZ);
            Assert.AreEqual(-22, position.Location.Z);

            position = new WorldPosition(0, 0, MatrixExtension.SetTranslation(Microsoft.Xna.Framework.Matrix .Identity, 3834, 0, -4118)).Normalize();

            Assert.AreEqual(2, position.TileX);
            Assert.AreEqual(-262, position.Location.X);
            Assert.AreEqual(-2, position.TileZ);
            Assert.AreEqual(22, position.Location.Z);
            Assert.AreEqual(-22, position.XNAMatrix.M43);
        }

        [TestMethod]
        public void WorldPositionNormalizeToTest()
        {
            WorldPosition position = new WorldPosition(-1, 1, MatrixExtension.SetTranslation(Microsoft.Xna.Framework.Matrix.Identity, 3834, 0, -4118)).NormalizeTo(4, 4);

            Assert.AreEqual(4, position.TileX);
            Assert.AreEqual(-6406, position.Location.X);
            Assert.AreEqual(4, position.TileZ);
            Assert.AreEqual(10262, position.Location.Z);
            Assert.AreEqual(-10262, position.XNAMatrix.M43);
        }

    }
}
