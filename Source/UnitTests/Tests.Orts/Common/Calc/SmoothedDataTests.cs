using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common.Calc;

namespace Tests.Orts.Common.Calc
{
    [TestClass]
    public class SmoothedDataTests
    {
        [TestMethod]
        public void DefaultInitializeTest()
        {
            SmoothedData data = new SmoothedData(5);
            Assert.AreEqual(double.NaN, data.Value);
            Assert.AreEqual(5, data.SmoothPeriodS);
            Assert.AreEqual(double.NaN, data.SmoothedValue);
        }

        [TestMethod]
        public void UpdateZeroTest()
        {
            SmoothedData data = new SmoothedData(5);
            data.Update(0, 0);
            Assert.AreEqual(0, data.Value);
            Assert.AreEqual(0, data.SmoothedValue);
        }

        [TestMethod]
        public void UpdateOneTest()
        {
            SmoothedData data = new SmoothedData(5);
            data.Update(0, 0);
            data.Update(1, 15);
            Assert.AreEqual(15, data.Value);
            Assert.AreEqual(3, data.SmoothedValue);
        }

        [TestMethod]
        public void UpdateTwoTest()
        {
            SmoothedData data = new SmoothedData(5);
            data.Update(0, 3);
            data.Update(2, 8);
            Assert.AreEqual(5, data.SmoothedValue);
        }

        [TestMethod]
        public void UpdateWithPresetTest()
        {
            SmoothedData data = new SmoothedData(5);
            data.Preset(8);
            data.Update(2, 3);
            Assert.AreEqual(6, data.SmoothedValue);
        }

        [TestMethod]
        public void UpdateWithPresetFullWeightTest()
        {
            SmoothedData data = new SmoothedData(5);
            data.Preset(8);
            data.Update(5, 3);
            Assert.AreEqual(3, data.SmoothedValue);
        }

        [TestMethod]
        public void UpdateWithPresetNullWeightTest()
        {
            SmoothedData data = new SmoothedData(5);
            data.Preset(8);
            data.Update(0, 3);
            Assert.AreEqual(8, data.SmoothedValue);
        }

        [TestMethod]
        public void UpdateWithPresetDoubleWeightTest()
        {
            SmoothedData data = new SmoothedData(5);
            data.Preset(8);
            data.Update(10, 3);
            Assert.AreEqual(3, data.SmoothedValue);
        }

    }

    [TestClass]
    public class SmoothedDataWithPercentilesTests
    {
        [TestMethod]
        public void UpdateWithPresetDoubleWeightTest()
        {
            SmoothedDataWithPercentiles data = new SmoothedDataWithPercentiles();
            data.Preset(8);
            data.Update(5, 3);
            Assert.AreEqual(3, data.SmoothedValue);
        }

        [TestMethod]
        public void UpdateTest()
        {
            SmoothedDataWithPercentiles data = new SmoothedDataWithPercentiles();
            data.Update(2, 3);
            data.Update(2, 6);
            data.Update(2, 2);
            Assert.AreEqual(3, data.SmoothedValue);
        }

    }
}
