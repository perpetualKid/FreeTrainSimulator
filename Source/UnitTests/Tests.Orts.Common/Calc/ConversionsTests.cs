using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common.Calc;

namespace Tests.Orts.Common.Calc
{

    [TestClass]
    public class FrequencyConversionTests
    {
        [TestMethod]
        public void EqualityTest()
        {
            Assert.AreEqual(0, Frequency.HzToRad(0));
            Assert.AreEqual(2 * Math.PI, Frequency.HzToRad(1));
            Assert.AreEqual(3 * Math.PI, Frequency.HzToRad(1.5));

            Assert.AreEqual(0, Frequency.RadToHz(0));
            Assert.AreEqual(1, Frequency.RadToHz(2 * Math.PI));
            Assert.AreEqual(1.5, Frequency.RadToHz(3 * Math.PI));
        }

        [TestMethod]
        public void RoundTripTest()
        {
            for (float i=0; i < 30;  i += .1f)
            {
                Assert.AreEqual(i, Frequency.RadToHz(Frequency.HzToRad(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
            }
        }
    }

    [TestClass]
    public class TimeConversionTests
    {
        [TestMethod]
        public void EqualityTest()
        {
            Assert.AreEqual(210, Time.Second.FromM(3.5f), EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(9000, Time.Second.FromH(2.5f), EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        [TestMethod]
        public void RoundTripTest()
        {
            for (float i = 0; i < 30; i += 0.1f)
            {
                Assert.AreEqual(i, Time.Second.FromM(Time.Second.ToM(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(i, Time.Second.FromH(Time.Second.ToH(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
            }
        }
    }
}
