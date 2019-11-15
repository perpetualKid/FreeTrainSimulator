using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common.Calc;

namespace Tests.Orts.Common.Calc
{
    [TestClass]
    public class AlmostEqualTests
    {
        [TestMethod]
        public void AlmostEqualTest()
        {
            float test = 0f;
            Assert.IsTrue(test.AlmostEqual(0.1f, 0.1f));
        }

        [TestMethod]
        public void AlmostEqualOnNegativeNumbersTest()
        {
            float test = -10f;
            Assert.IsTrue(test.AlmostEqual(-11.1f, 1.1f));
        }

        [TestMethod]
        public void NotEqualTest()
        {
            float test = 0f;
            Assert.IsTrue(test.AlmostEqual(0.1f, 0.11f));
        }

        [TestMethod]
        public void AlmostEqualDTest()
        {
            double test = 0;
            Assert.IsTrue(test.AlmostEqual(0.1, 0.1));
        }

        [TestMethod]
        public void AlmostEqualDOnNegativeNumbersTest()
        {
            double test = -10;
            Assert.IsTrue(test.AlmostEqual(-11.1, 1.1));
        }

        [TestMethod]
        public void NotEqualDTest()
        {
            double test = 0;
            Assert.IsTrue(test.AlmostEqual(0.1, 0.11));
        }
    }
}
