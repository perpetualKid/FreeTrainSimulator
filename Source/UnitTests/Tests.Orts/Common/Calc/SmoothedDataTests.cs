using System;
using System.Linq;

using FreeTrainSimulator.Common.Calc;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Common.Calc;

using static FreeTrainSimulator.Common.Calc.Frequency;

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
            Assert.AreEqual(5, data.SmoothPeriod);
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
            Assert.AreEqual(2.723, Math.Round(data.SmoothedValue, 3));
        }

        [TestMethod]
        public void UpdateTwoTest()
        {
            SmoothedData data = new SmoothedData(5);
            data.Update(0, 3);
            data.Update(2, 8);
            Assert.AreEqual(4.651, Math.Round(data.SmoothedValue, 3));
        }

        [TestMethod]
        public void UpdateWithPresetTest()
        {
            SmoothedData data = new SmoothedData(5);
            data.Preset(8);
            data.Update(2, 4);
            Assert.AreEqual(6.679, Math.Round(data.SmoothedValue, 3));
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

        [DataTestMethod]
        // FPS-like tests
        [DataRow(5, 3, 0.353)]
        [DataRow(10, 3, 0.353)]
        [DataRow(30, 3, 0.353)]
        [DataRow(60, 3, 0.353)]
        [DataRow(120, 3, 0.353)]
        // Physics-like tests
        [DataRow(60, 1, 0.000)] // Exhaust particles
        [DataRow(60, 2, 0.066)] // Smoke colour
        [DataRow(60, 45, 8.007)] // Field rate
        [DataRow(60, 150, 9.355)] // Burn rate
        [DataRow(60, 240, 9.592)] // Boiler heat
        public void SmoothedDataTest(int value, double smoothPeriod, double expected)
        {
            double period = 1d / value;
            SmoothedData data = new SmoothedData(smoothPeriod);
            data.Update(0, 10);
#pragma warning disable IDE0059 // Unnecessary assignment of a value
            foreach (int i in Enumerable.Range(0, 10 * value))
                data.Update(period, 0);
#pragma warning restore IDE0059 // Unnecessary assignment of a value
            Assert.AreEqual(expected, Math.Round(data.SmoothedValue, 3));
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
            Assert.AreEqual(3.262, Math.Round(data.SmoothedValue, 3));
        }

    }
}
