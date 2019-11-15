using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common.Calc;

namespace Tests.Orts.Common.Calc
{
    [TestClass]
    public class MovingAverageTests
    {
        [TestMethod]
        public void EnsureDefaultSizeConstraint()
        {
            MovingAverage instance = new MovingAverage();
            double result = 0;
            for (int i = 0; i < 10; i++)
            {
                result = instance.Update(i);
            }
            Assert.AreEqual((9 + 8 + 7 + 6 + 5 + 4 + 3 + 2 + 1 + 0) / (double)instance.Size, result);
        }

        [TestMethod]
        public void EnsureSizeConstraint()
        {
            int size = 5;
            MovingAverage instance = new MovingAverage(size);
            double result = 0;
            for (int i = 0; i < 10; i++)
            {
                result = instance.Update(i);
            }
            Assert.AreEqual(result, (9 + 8 + 7 + 6 + 5) / (double)size);
        }

        [TestMethod]
        public void BufferSizeTest()
        {
            int size = 5;
            MovingAverage instance = new MovingAverage(size);
            Assert.AreEqual(size, instance.Size);
        }

        [TestMethod]
        public void InitializeDefaultTest()
        {
            int size = 5;
            MovingAverage instance = new MovingAverage(size);
            double result = instance.Update(0f);
            Assert.AreEqual(0f, result);
        }

        [TestMethod]
        public void InitializeByValueTest()
        {
            int size = 5;
            MovingAverage instance = new MovingAverage(size);
            instance.Initialize(size);
            double result = instance.Update(size);
            Assert.AreEqual(size, result);
        }
    }
}
