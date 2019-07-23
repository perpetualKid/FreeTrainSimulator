using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common.Calc;

namespace Tests.Orts.Common.Calc
{
    [TestClass]
    public class ConversionsTests
    {
        [TestMethod]
        public void TestHzToRadFor0()
        {
            Assert.AreEqual(0, Frequency.HzToRad(0));
        }

        [TestMethod]
        public void TestHzToRadFor1Hz()
        {
            Assert.AreEqual(2*Math.PI, Frequency.HzToRad(1));
        }

        [TestMethod]
        public void TestHzToRadFor1p5Hz()
        {
            Assert.AreEqual(3 * Math.PI, Frequency.HzToRad(1.5));
        }

        [TestMethod]
        public void TestRadToHzFor0()
        {
            Assert.AreEqual(0, Frequency.RadToHz(0));
        }

        [TestMethod]
        public void TestRadToHzFor2Rad()
        {
            Assert.AreEqual(1, Frequency.RadToHz(2*Math.PI));
        }

        [TestMethod]
        public void TestRadToHzFor3()
        {
            Assert.AreEqual(1.5, Frequency.RadToHz(3 * Math.PI));
        }

    }
}
