using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common.Calc;

namespace Tests.Orts.Common.Calc
{

    [TestClass]
    public class FrequencyConversionTests
    {
        [TestMethod]
        public void AngularEqualityTest()
        {
            Assert.AreEqual(0, Frequency.Angular.HzToRad(0));
            Assert.AreEqual(2 * Math.PI, Frequency.Angular.HzToRad(1));
            Assert.AreEqual(3 * Math.PI, Frequency.Angular.HzToRad(1.5));

            Assert.AreEqual(0, Frequency.Angular.RadToHz(0));
            Assert.AreEqual(1, Frequency.Angular.RadToHz(2 * Math.PI));
            Assert.AreEqual(1.5, Frequency.Angular.RadToHz(3 * Math.PI));

        }

        [TestMethod]
        public void PeriodicRoundTripTest()
        {
            for (float i = 0; i < 30; i += .1f)
            {
                Assert.AreEqual(i, Frequency.Periodic.FromMinutes(Frequency.Periodic.ToMinutes(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(i, Frequency.Periodic.FromHours(Frequency.Periodic.ToHours(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
            }
        }

        [TestMethod]
        public void AngularRoundTripTest()
        {
            for (float i=0; i < 30;  i += .1f)
            {
                Assert.AreEqual(i, Frequency.Angular.RadToHz(Frequency.Angular.HzToRad(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
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

    [TestClass]
    public class SizeConversionTests
    {
        [TestMethod]
        public void SizeRoundTripTest()
        {
            for (float i = 0; i < 30; i += 0.1f)
            {
                Assert.AreEqual(i, Size.Length.FromMi(Size.Length.ToMi(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(i, Size.Length.FromKM(Size.Length.ToKM(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(i, Size.Length.FromYd(Size.Length.ToYd(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(i, Size.Length.FromFt(Size.Length.ToFt(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(i, Size.Length.FromIn(Size.Length.ToIn(i)), EqualityPrecisionDelta.FloatPrecisionDelta);

                Assert.AreEqual(i, Size.Area.FromFt2(Size.Area.ToFt2(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(i, Size.Area.FromIn2(Size.Area.ToIn2(i)), EqualityPrecisionDelta.FloatPrecisionDelta);

                Assert.AreEqual(i, Size.Volume.FromFt3(Size.Volume.ToFt3(i)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(i, Size.Volume.FromIn3(Size.Volume.ToIn3(i)), EqualityPrecisionDelta.FloatPrecisionDelta);

                Assert.AreEqual(i, Size.Length.ToM(Size.Length.FromM(i, true), true), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(i, Size.Length.ToM(Size.Length.FromM(i, false), false), EqualityPrecisionDelta.FloatPrecisionDelta);

            }
        }

        [TestMethod]
        public void RelatedConversionsTest()
        {
            {
                Assert.AreEqual(1.44f, Size.Area.FromFt2((float)Math.Pow(Size.Length.ToFt(1.2f), 2)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(1.44f, Size.Area.ToFt2((float)Math.Pow(Size.Length.FromFt(1.2f), 2)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(1.44f, Size.Area.FromIn2((float)Math.Pow(Size.Length.ToIn(1.2f), 2)), EqualityPrecisionDelta.FloatPrecisionDelta);
                Assert.AreEqual(1.44f, Size.Area.ToIn2((float)Math.Pow(Size.Length.FromIn(1.2f), 2)), EqualityPrecisionDelta.FloatPrecisionDelta);


                //Assert.AreEqual(1.728f, Me3.FromFt3((float)Math.Pow(Size.Length.ToFt(1.2f), 3)), EqualityPrecisionDelta.FloatPrecisionDelta);
                //Assert.AreEqual(1.728f, Me3.ToFt3((float)Math.Pow(Size.Length.FromFt(1.2f), 3)), EqualityPrecisionDelta.FloatPrecisionDelta);
                //Assert.AreEqual(1.728f, Me3.FromIn3((float)Math.Pow(Size.Length.ToIn(1.2f), 3)), EqualityPrecisionDelta.FloatPrecisionDelta);
                //Assert.AreEqual(1.728f, Me3.ToIn3((float)Math.Pow(Size.Length.FromIn(1.2f), 3)), EqualityPrecisionDelta.FloatPrecisionDelta);

            }
        }
    }
}
