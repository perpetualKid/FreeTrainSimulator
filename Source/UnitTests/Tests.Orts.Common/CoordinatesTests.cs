using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common;

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

            Assert.IsTrue(Equals(location1, WorldLocation.None));
            Assert.IsTrue(location1 != location2);

            Assert.IsFalse(Equals(new object(), WorldLocation.None));
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
    }
}
