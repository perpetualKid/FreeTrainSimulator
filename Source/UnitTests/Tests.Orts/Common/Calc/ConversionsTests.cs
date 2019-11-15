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
            for (double i = 0; i < 30; i += .1)
            {
                Assert.AreEqual(i, Frequency.Periodic.FromMinutes(Frequency.Periodic.ToMinutes(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Frequency.Periodic.FromHours(Frequency.Periodic.ToHours(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }
        }

        [TestMethod]
        public void AngularRoundTripTest()
        {
            for (double i = 0; i < 30; i += .1)
            {
                Assert.AreEqual(i, Frequency.Angular.RadToHz(Frequency.Angular.HzToRad(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }
        }

        [TestMethod]
        public void RelatedConversionTest()
        {
            Assert.AreEqual(1.2, Frequency.Periodic.FromMinutes(Time.Second.FromM(1.2)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Frequency.Periodic.ToMinutes(Time.Second.ToM(1.2)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Frequency.Periodic.FromHours(Time.Second.FromH(1.2)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Frequency.Periodic.ToHours(Time.Second.ToH(1.2)), EqualityPrecisionDelta.DoublePrecisionDelta);
        }
    }

    [TestClass]
    public class TimeConversionTests
    {
        [TestMethod]
        public void EqualityTest()
        {
            Assert.AreEqual(210, Time.Second.FromM(3.5), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(9000, Time.Second.FromH(2.5), EqualityPrecisionDelta.DoublePrecisionDelta);
        }

        [TestMethod]
        public void RoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Time.Second.FromM(Time.Second.ToM(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Time.Second.FromH(Time.Second.ToH(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }
        }

        [TestMethod]
        public void CompareTimeTest()
        {
            int time4 = 4 * 3600;
            int time7 = 7 * 3600;
            int time8 = 8 * 3600;
            int time12 = 12 * 3600;
            int time15 = 15 * 3600;
            int time16 = 16 * 3600;
            int time20 = 20 * 3600;

            // Simple cases
            Assert.AreEqual(time7, Time.Compare.Latest(time4, time7));
            Assert.AreEqual(time12, Time.Compare.Latest(time4, time12));
            Assert.AreEqual(time15, Time.Compare.Latest(time4, time15));
            Assert.AreEqual(time4, Time.Compare.Latest(time4, time20));

            Assert.AreEqual(time12, Time.Compare.Latest(time7, time12));
            Assert.AreEqual(time15, Time.Compare.Latest(time7, time15));
            Assert.AreEqual(time7, Time.Compare.Latest(time7, time20));

            Assert.AreEqual(time15, Time.Compare.Latest(time12, time15));
            Assert.AreEqual(time20, Time.Compare.Latest(time12, time20));

            Assert.AreEqual(time20, Time.Compare.Latest(time15, time20));

            Assert.AreEqual(time4, Time.Compare.Earliest(time4, time7));
            Assert.AreEqual(time4, Time.Compare.Earliest(time4, time12));
            Assert.AreEqual(time4, Time.Compare.Earliest(time4, time15));
            Assert.AreEqual(time20, Time.Compare.Earliest(time4, time20));

            Assert.AreEqual(time7, Time.Compare.Earliest(time7, time12));
            Assert.AreEqual(time7, Time.Compare.Earliest(time7, time15));
            Assert.AreEqual(time20, Time.Compare.Earliest(time7, time20));

            Assert.AreEqual(time12, Time.Compare.Earliest(time12, time15));
            Assert.AreEqual(time12, Time.Compare.Earliest(time12, time20));

            Assert.AreEqual(time15, Time.Compare.Earliest(time15, time20));

            // Boundary cases
            Assert.AreEqual(time4, Time.Compare.Earliest(time8, time4));
            Assert.AreEqual(time8, Time.Compare.Earliest(time8, time12));
            Assert.AreEqual(time8, Time.Compare.Earliest(time8, time20));

            Assert.AreEqual(time8, Time.Compare.Latest(time8, time4));
            Assert.AreEqual(time12, Time.Compare.Latest(time8, time12));
            Assert.AreEqual(time20, Time.Compare.Latest(time8, time20));

            Assert.AreEqual(time4, Time.Compare.Earliest(time16, time4));
            Assert.AreEqual(time12, Time.Compare.Earliest(time16, time12));
            Assert.AreEqual(time16, Time.Compare.Earliest(time16, time20));

            Assert.AreEqual(time16, Time.Compare.Latest(time16, time4));
            Assert.AreEqual(time16, Time.Compare.Latest(time16, time12));
            Assert.AreEqual(time20, Time.Compare.Latest(time16, time20));
        }
    }

    [TestClass]
    public class SizeConversionTests
    {
        [TestMethod]
        public void SizeRoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Size.Length.FromMi(Size.Length.ToMi(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Size.Length.FromKM(Size.Length.ToKM(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Size.Length.FromYd(Size.Length.ToYd(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Size.Length.FromFt(Size.Length.ToFt(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Size.Length.FromIn(Size.Length.ToIn(i)), EqualityPrecisionDelta.DoublePrecisionDelta);

                Assert.AreEqual(i, Size.Area.FromFt2(Size.Area.ToFt2(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Size.Area.FromIn2(Size.Area.ToIn2(i)), EqualityPrecisionDelta.DoublePrecisionDelta);

                Assert.AreEqual(i, Size.Volume.FromFt3(Size.Volume.ToFt3(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Size.Volume.FromIn3(Size.Volume.ToIn3(i)), EqualityPrecisionDelta.DoublePrecisionDelta);

                Assert.AreEqual(i, Size.Length.ToM(Size.Length.FromM(i, true), true), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Size.Length.ToM(Size.Length.FromM(i, false), false), EqualityPrecisionDelta.DoublePrecisionDelta);

                Assert.AreEqual(i, Size.LiquidVolume.FromGallonUK(Size.LiquidVolume.ToGallonUK(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Size.LiquidVolume.FromGallonUS(Size.LiquidVolume.ToGallonUS(i)), EqualityPrecisionDelta.DoublePrecisionDelta);

            }
        }

        [TestMethod]
        public void RelatedConversionsTest()
        {
            {
                Assert.AreEqual(1.44, Size.Area.FromFt2(Math.Pow(Size.Length.ToFt(1.2), 2)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(1.44, Size.Area.ToFt2(Math.Pow(Size.Length.FromFt(1.2), 2)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(1.44, Size.Area.FromIn2(Math.Pow(Size.Length.ToIn(1.2), 2)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(1.44, Size.Area.ToIn2(Math.Pow(Size.Length.FromIn(1.2), 2)), EqualityPrecisionDelta.DoublePrecisionDelta);


                Assert.AreEqual(1.728, Size.Volume.FromFt3(Math.Pow(Size.Length.ToFt(1.2), 3)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(1.728, Size.Volume.ToFt3(Math.Pow(Size.Length.FromFt(1.2), 3)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(1.728, Size.Volume.FromIn3(Math.Pow(Size.Length.ToIn(1.2), 3)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(1.728, Size.Volume.ToIn3(Math.Pow(Size.Length.FromIn(1.2), 3)), EqualityPrecisionDelta.DoublePrecisionDelta);

            }
        }
    }

    [TestClass]
    public class MassTests
    {
        [TestMethod]
        public void MassRoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Mass.Kilogram.FromLb(Mass.Kilogram.ToLb(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Mass.Kilogram.FromTonsUS(Mass.Kilogram.ToTonsUS(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Mass.Kilogram.FromTonsUK(Mass.Kilogram.ToTonsUK(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Mass.Kilogram.FromTonnes(Mass.Kilogram.ToTonnes(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }

        }
    }

    [TestClass]
    public class DynamicsTests
    {
        [TestMethod]
        public void ForceRoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Dynamics.Force.FromLbf(Dynamics.Force.ToLbf(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }
        }

        [TestMethod]
        public void PowerRoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Dynamics.Power.FromKW(Dynamics.Power.ToKW(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Dynamics.Power.FromHp(Dynamics.Power.ToHp(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Dynamics.Power.FromBTUpS(Dynamics.Power.ToBTUpS(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }
        }

    }

    [TestClass]
    public class TemperatureTests
    {
        [TestMethod]
        public void ConversionValuesTest()
        {
            Assert.AreEqual(0, Temperature.Celsius.FromF(32), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(-20, Temperature.Celsius.FromF(-4), EqualityPrecisionDelta.DoublePrecisionDelta);
        }

        [TestMethod]
        public void TemperatureRoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Temperature.Celsius.FromF(Temperature.Celsius.ToF(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i * 5, Temperature.Celsius.FromK(Temperature.Celsius.ToK(i * 5)), EqualityPrecisionDelta.DoublePrecisionDelta); // we loose accuracy because of the large 273.15

                Assert.AreEqual(i, Temperature.Kelvin.FromF(Temperature.Kelvin.ToF(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i * 5, Temperature.Kelvin.FromC(Temperature.Kelvin.ToC(i * 5)), EqualityPrecisionDelta.DoublePrecisionDelta); // we loose accuracy because of the large 273.15
            }
        }

        [TestMethod]
        public void RelatedConversionsTest()
        {
            Assert.AreEqual(Temperature.Kelvin.FromC(0), Temperature.Celsius.ToK(0));
            Assert.AreEqual(Temperature.Celsius.FromF(0), Temperature.Kelvin.ToC(Temperature.Kelvin.FromF(0)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(Temperature.Kelvin.ToC(300), Temperature.Celsius.FromK(300), EqualityPrecisionDelta.DoublePrecisionDelta);
        }
    }

    [TestClass]
    public class SpeedTests
    {
        [TestMethod]
        public void SpeedRoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Speed.MeterPerSecond.FromMpH(Speed.MeterPerSecond.ToMpH(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Speed.MeterPerSecond.FromKpH(Speed.MeterPerSecond.ToKpH(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }
        }

        [TestMethod]
        public void RelatedConversionsTest()
        {
            Assert.AreEqual(1.2, Speed.MeterPerSecond.FromMpS(Speed.MeterPerSecond.FromKpH(1.2), true), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Speed.MeterPerSecond.FromMpS(Speed.MeterPerSecond.FromMpH(1.2), false), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Speed.MeterPerSecond.ToMpS(Speed.MeterPerSecond.ToKpH(1.2), true), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Speed.MeterPerSecond.ToMpS(Speed.MeterPerSecond.ToMpH(1.2), false), EqualityPrecisionDelta.DoublePrecisionDelta);


        }
    }

    [TestClass]
    public class RateTests
    {
        [TestMethod]
        public void RateRoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Rate.Flow.Mass.FromLbpH(Rate.Flow.Mass.ToLbpH(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Rate.Pressure.FromPSIpS(Rate.Pressure.ToPSIpS(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }
        }

        //[TestMethod]
        //public void RelatedConversionsTest()
        //{
        //    Assert.AreEqual(1.2, Rate.Pressure.FromPSIpS(Bar.ToPSI(1.2f)), EqualityPrecisionDelta.FloatPrecisionDelta);
        //    Assert.AreEqual(1.2, Bar.FromPSI(Rate.Pressure.ToPSIpS(1.2f)), EqualityPrecisionDelta.FloatPrecisionDelta);
        //    Assert.AreEqual(1.2, Rate.Pressure.ToPSIpS(Bar.FromPSI(1.2f)), EqualityPrecisionDelta.FloatPrecisionDelta);
        //    Assert.AreEqual(1.2, Bar.ToPSI(Rate.Pressure.FromPSIpS(1.2f)), EqualityPrecisionDelta.FloatPrecisionDelta);
        //}

    }

    [TestClass]
    public class EnergyTests
    {
        [TestMethod]
        public void EnergyRoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Energy.Density.Mass.FromBTUpLb(Energy.Density.Mass.ToBTUpLb(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Energy.Density.Volume.FromBTUpFt3(Energy.Density.Volume.ToBTUpFt3(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }
        }
    }

    [TestClass]
    public class PressureTests
    {
        [TestMethod]
        public void PressureRoundTripTest()
        {
            for (double i = 0; i < 30; i += 0.1)
            {
                Assert.AreEqual(i, Pressure.Standard.FromPSI(Pressure.Standard.ToPSI(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Pressure.Standard.FromInHg(Pressure.Standard.ToInHg(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Pressure.Standard.FromBar(Pressure.Standard.ToBar(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Pressure.Standard.FromKgfpCm2(Pressure.Standard.ToKgfpCm2(i)), EqualityPrecisionDelta.DoublePrecisionDelta);

                Assert.AreEqual(i, Pressure.Atmospheric.FromKPa(Pressure.Atmospheric.ToKPa(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Pressure.Atmospheric.FromPSI(Pressure.Atmospheric.ToPSI(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Pressure.Atmospheric.FromInHg(Pressure.Atmospheric.ToInHg(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
                Assert.AreEqual(i, Pressure.Atmospheric.FromKgfpCm2(Pressure.Atmospheric.ToKgfpCm2(i)), EqualityPrecisionDelta.DoublePrecisionDelta);
            }
        }

        [TestMethod]
        public void RelatedConversionsTest()
        {
            Assert.AreEqual(1.2, Pressure.Standard.FromBar(Pressure.Atmospheric.FromKPa(1.2)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.ToBar(Pressure.Atmospheric.ToKPa(1.2)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.FromPSI(Pressure.Atmospheric.ToPSI(Pressure.Standard.ToBar(1.2))), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.ToPSI(Pressure.Standard.FromBar(Pressure.Atmospheric.FromPSI(1.2))), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.FromInHg(Pressure.Atmospheric.ToInHg(Pressure.Standard.ToBar(1.2))), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.ToInHg(Pressure.Standard.FromBar(Pressure.Atmospheric.FromInHg(1.2))), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.FromKgfpCm2(Pressure.Atmospheric.ToKgfpCm2(Pressure.Standard.ToBar(1.2))), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.ToKgfpCm2(Pressure.Standard.FromBar(Pressure.Atmospheric.FromKgfpCm2(1.2))), EqualityPrecisionDelta.DoublePrecisionDelta);
        }

        [TestMethod]
        public void MultiUnitConversions()
        {
            Assert.AreEqual(1.2, Pressure.Standard.FromKPa(1.2, Pressure.Unit.KPa), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.FromBar(Pressure.Standard.FromKPa(1.2, Pressure.Unit.Bar)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.FromInHg(Pressure.Standard.FromKPa(1.2, Pressure.Unit.InHg)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.FromKgfpCm2(Pressure.Standard.FromKPa(1.2, Pressure.Unit.KgfpCm2)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.FromPSI(Pressure.Standard.FromKPa(1.2, Pressure.Unit.PSI)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Pressure.Standard.FromKPa(1.2, Pressure.Unit.None));

            Assert.AreEqual(1.2, Pressure.Standard.ToKPa(1.2, Pressure.Unit.KPa), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.ToBar(Pressure.Standard.ToKPa(1.2, Pressure.Unit.Bar)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.ToInHg(Pressure.Standard.ToKPa(1.2, Pressure.Unit.InHg)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.ToKgfpCm2(Pressure.Standard.ToKPa(1.2, Pressure.Unit.KgfpCm2)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.AreEqual(1.2, Pressure.Standard.ToPSI(Pressure.Standard.ToKPa(1.2, Pressure.Unit.PSI)), EqualityPrecisionDelta.DoublePrecisionDelta);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Pressure.Standard.ToKPa(1.2, Pressure.Unit.None));
        }


    }

}