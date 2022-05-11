using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Common.Position;

namespace Tests.Orts.Common.Position
{
    [TestClass]
    public class PointDTests
    {
        [TestMethod]
        public void TileCenterTest()
        {
            Assert.AreEqual(PointD.None, PointD.TileCenter(new Tile(0, 0)));
            Assert.AreEqual(new PointD(2048, 2048), PointD.TileCenter(new Tile(1, 1)));
            Assert.AreEqual(new PointD(-2048, -2048), PointD.TileCenter(new Tile(-1, -1)));
            Assert.AreEqual(new PointD(-2048, 2048), PointD.TileCenter(new Tile(-1, 1)));
            Assert.AreEqual(new PointD(20480, 20480), PointD.TileCenter(new Tile(10, 10)));
            Assert.AreEqual(new PointD(-20480, -20480), PointD.TileCenter(new Tile(-10, -10)));
        }

        [TestMethod]
        public void ToTileTest()
        {
            Assert.AreEqual(Tile.Zero, PointD.ToTile(PointD.None));
            Assert.AreEqual(Tile.Zero, PointD.ToTile(new PointD(100, 100)));
            Assert.AreEqual(Tile.Zero, PointD.ToTile(new PointD(1000, 1000)));
            Assert.AreEqual(Tile.Zero, PointD.ToTile(new PointD(1023, 1023)));
            Assert.AreEqual(Tile.Zero, PointD.ToTile(new PointD(1023.99, 1023.99)));
            Assert.AreEqual(new Tile(1, 1), PointD.ToTile(new PointD(1024, 1024)));
            Assert.AreEqual(new Tile(1, 0), PointD.ToTile(new PointD(1024, 1023.99)));

            Assert.AreEqual(Tile.Zero, PointD.ToTile(new PointD(-100, -100)));
            Assert.AreEqual(Tile.Zero, PointD.ToTile(new PointD(-1000, -1000)));
            Assert.AreEqual(Tile.Zero, PointD.ToTile(new PointD(-1023, -1023)));
            Assert.AreEqual(Tile.Zero, PointD.ToTile(new PointD(-1023.99, -1023.99)));
            Assert.AreEqual(new Tile(-1, -1), PointD.ToTile(new PointD(-1024, -1024)));
            Assert.AreEqual(new Tile(-1, 0), PointD.ToTile(new PointD(-1024, -1023.99)));
        }

        [TestMethod]
        public void FromWorldLocationTest()
        {
            Assert.AreEqual(PointD.None, PointD.FromWorldLocation(WorldLocation.None));
            Assert.AreEqual(new PointD(2148, 2148), PointD.FromWorldLocation(new WorldLocation(1, 1, 100, 0, 100)));
            Assert.AreEqual(new PointD(1948, 1948), PointD.FromWorldLocation(new WorldLocation(1, 1, -100, 0, -100)));
            Assert.AreEqual(new PointD(-2148, -2148), PointD.FromWorldLocation(new WorldLocation(-1, -1, -100, 0, -100)));
            Assert.AreEqual(new PointD(-1948, -1948), PointD.FromWorldLocation(new WorldLocation(-1, -1, 100, 0, 100)));

            Assert.AreEqual(new PointD(6048, 6048), PointD.FromWorldLocation(new WorldLocation(1, 1, 4000, 0, 4000)));
            Assert.AreEqual(new PointD(-1952, -1952), PointD.FromWorldLocation(new WorldLocation(1, 1, -4000, 0, -4000)));

            Assert.AreEqual(new PointD(6048, 6048), PointD.FromWorldLocation(new WorldLocation(1, 1, 4000, 0, 4000, true)));
            Assert.AreEqual(new PointD(-1952, -1952), PointD.FromWorldLocation(new WorldLocation(1, 1, -4000, 0, -4000, true)));
        }

        [TestMethod]
        public void ToWorldLocationTest()
        {
            Assert.AreEqual(WorldLocation.None, PointD.ToWorldLocation(PointD.None));
            Assert.AreEqual(new WorldLocation(1, 1, 100, 0, 100), PointD.ToWorldLocation(new PointD(2148, 2148)));
            Assert.AreEqual(new WorldLocation(1, 1, -100, 0, -100), PointD.ToWorldLocation(new PointD(1948, 1948)));
            Assert.AreEqual(new WorldLocation(-1, -1, -100, 0, -100), PointD.ToWorldLocation(new PointD(-2148, -2148)));
            Assert.AreEqual(new WorldLocation(-1, -1, 100, 0, 100), PointD.ToWorldLocation(new PointD(-1948, -1948)));

            Assert.AreEqual(new WorldLocation(3, 3, -96, 0, -96), PointD.ToWorldLocation(new PointD(6048, 6048)));
            Assert.AreEqual(new WorldLocation(-1, -1, 96, 0, 96), PointD.ToWorldLocation(new PointD(-1952, -1952)));        
        }

    }
}
