﻿using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Imported.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Models.Imported.Track
{
    [TestClass]
    public class TrackSegmentBaseTests
    {
        private sealed record TrackSegmentTestClass : TrackSegmentBase
        {
            public TrackSegmentTestClass() : base()
            { }

            public TrackSegmentTestClass(PointD start, PointD end) : base(start, end)
            { }
        }

        [TestMethod]
        public void EmptyConstructorTest()
        {
            TrackSegmentTestClass created = new TrackSegmentTestClass();
            Assert.AreEqual(0, created.Length);
            Assert.AreEqual(0, created.Direction);
            Assert.AreEqual(0, created.Angle);
            Assert.AreEqual(0, created.Radius);
            Assert.AreEqual(0, created.Size);
            Assert.AreEqual(Tile.Zero, created.Tile);
        }

        [TestMethod]
        public void EastWestSegmentTest()
        {
            TrackSegmentTestClass created = new TrackSegmentTestClass(new PointD(400, 200), new PointD(100, 200));
            Assert.AreEqual(300, created.Length);
            Assert.AreEqual(MathHelper.ToRadians(-180), created.Direction);
            Assert.AreEqual(Tile.Zero, created.Tile);
        }

        [TestMethod]
        public void WestEastSegmentTest()
        {
            TrackSegmentTestClass created = new TrackSegmentTestClass(new PointD(100, 200), new PointD(400, 200));
            Assert.AreEqual(300, created.Length);
            Assert.AreEqual(MathHelper.ToRadians(0), created.Direction);
            Assert.AreEqual(Tile.Zero, created.Tile);
        }

        [TestMethod]
        public void SouthNorthSegmentTest()
        {
            TrackSegmentTestClass created = new TrackSegmentTestClass(new PointD(200, 100), new PointD(200, 400));
            Assert.AreEqual(300, created.Length);
            Assert.AreEqual(MathHelper.ToRadians(-90), created.Direction);
            Assert.AreEqual(Tile.Zero, created.Tile);
        }

        [TestMethod]
        public void NorthSouthSegmentTest()
        {
            TrackSegmentTestClass created = new TrackSegmentTestClass(new PointD(200, 100), new PointD(200, -200));
            Assert.AreEqual(300, created.Length);
            Assert.AreEqual(MathHelper.ToRadians(90), created.Direction);
            Assert.AreEqual(Tile.Zero, created.Tile);
        }
    }
}
