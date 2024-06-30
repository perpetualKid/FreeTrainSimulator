
using System;

using FreeTrainSimulator.Common.Position;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common.Position
{
    [TestClass]
    public class TileTests
    {
        [TestMethod]
        public void TileFromAbsTest()
        {
            Assert.AreEqual(0, Tile.TileFromAbs(0));
            Assert.AreEqual(0, Tile.TileFromAbs(1023.99));
            Assert.AreEqual(1, Tile.TileFromAbs(1024));
            Assert.AreEqual(1, Tile.TileFromAbs(2048));
            Assert.AreEqual(1, Tile.TileFromAbs(3071.99));
            Assert.AreEqual(2, Tile.TileFromAbs(3072));

            Assert.AreEqual(0, Tile.TileFromAbs(-0));
            Assert.AreEqual(0, Tile.TileFromAbs(-1023.99));
            Assert.AreEqual(-1, Tile.TileFromAbs(-1024));
            Assert.AreEqual(-1, Tile.TileFromAbs(-2048));
            Assert.AreEqual(-1, Tile.TileFromAbs(-3071.99));
            Assert.AreEqual(-2, Tile.TileFromAbs(-3072));

            Assert.ThrowsException<OverflowException>(() => Tile.TileFromAbs(double.MaxValue));
        }

        [TestMethod]
        public void TileEqualTest()
        {
            Tile t1 = new Tile();
            Tile t2 = new Tile();

            Assert.IsTrue(t1.Equals(t2));

            Tile t3 = new Tile(0, 1);
            Assert.IsFalse(t1.Equals(t3));
        }

        [TestMethod]
        public void CompareToTest()
        {
            Tile t1 = new Tile();
            Tile t2 = new Tile();

            Assert.AreEqual(0, t1.CompareTo(t2));

            Tile t3 = new Tile(0, 1);
            Assert.AreEqual(-1, t1.CompareTo(t3));
            Assert.AreEqual(1, t3.CompareTo(t1));
        }

    }
}
