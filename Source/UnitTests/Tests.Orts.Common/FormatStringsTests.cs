using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Common;
using Orts.Common.Calc;

namespace Tests.Orts.Common
{
    [TestClass]
    public class FormatStringsTests
    {
        [TestMethod]
        public void FormattedStrings()
        {
            // Note: Only pressure is tested at the moment, mainly because of its complexity.

            Assert.AreEqual(string.Empty, FormatStrings.FormatPressure(1.2f, Pressure.Unit.None, Pressure.Unit.KPa, true));
            Assert.AreEqual(string.Empty, FormatStrings.FormatPressure(1.2f, Pressure.Unit.KPa, Pressure.Unit.None, true));

            Assert.AreEqual("1 kPa", FormatStrings.FormatPressure(1.2f, Pressure.Unit.KPa, Pressure.Unit.KPa, true));
            Assert.AreEqual("1 kPa", FormatStrings.FormatPressure(Pressure.Standard.ToBar(1.2f), Pressure.Unit.Bar, Pressure.Unit.KPa, true));
            Assert.AreEqual("1 kPa", FormatStrings.FormatPressure(Pressure.Standard.ToInHg(1.2f), Pressure.Unit.InHg, Pressure.Unit.KPa, true));
            Assert.AreEqual("1 kPa", FormatStrings.FormatPressure(Pressure.Standard.ToKgfpCm2(1.2f), Pressure.Unit.KgfpCm2, Pressure.Unit.KPa, true));
            Assert.AreEqual("1 kPa", FormatStrings.FormatPressure(Pressure.Standard.ToPSI(1.2f), Pressure.Unit.PSI, Pressure.Unit.KPa, true));

            var barResult = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:F1} bar", 1.2f);
            Assert.AreEqual(barResult, FormatStrings.FormatPressure(Pressure.Atmospheric.ToKPa(1.2f), Pressure.Unit.KPa, Pressure.Unit.Bar, true));
            Assert.AreEqual(barResult, FormatStrings.FormatPressure(1.2f, Pressure.Unit.Bar, Pressure.Unit.Bar, true));
            Assert.AreEqual(barResult, FormatStrings.FormatPressure(Pressure.Atmospheric.ToInHg(1.2f), Pressure.Unit.InHg, Pressure.Unit.Bar, true));
            Assert.AreEqual(barResult, FormatStrings.FormatPressure(Pressure.Atmospheric.ToKgfpCm2(1.2f), Pressure.Unit.KgfpCm2, Pressure.Unit.Bar, true));
            Assert.AreEqual(barResult, FormatStrings.FormatPressure(Pressure.Atmospheric.ToPSI(1.2f), Pressure.Unit.PSI, Pressure.Unit.Bar, true));

            var psiResult = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:F0} psi", 1.2f);
            Assert.AreEqual(psiResult, FormatStrings.FormatPressure(Pressure.Standard.FromPSI(1.2f), Pressure.Unit.KPa, Pressure.Unit.PSI, true));
            Assert.AreEqual(psiResult, FormatStrings.FormatPressure(Pressure.Standard.ToBar(Pressure.Standard.FromPSI(1.2f)), Pressure.Unit.Bar, Pressure.Unit.PSI, true));
            Assert.AreEqual(psiResult, FormatStrings.FormatPressure(Pressure.Standard.ToInHg(Pressure.Standard.FromPSI(1.2f)), Pressure.Unit.InHg, Pressure.Unit.PSI, true));
            Assert.AreEqual(psiResult, FormatStrings.FormatPressure(Pressure.Standard.ToKgfpCm2(Pressure.Standard.FromPSI(1.2f)), Pressure.Unit.KgfpCm2, Pressure.Unit.PSI, true));
            Assert.AreEqual(psiResult, FormatStrings.FormatPressure(Pressure.Standard.ToPSI(Pressure.Standard.FromPSI(1.2f)), Pressure.Unit.PSI, Pressure.Unit.PSI, true));

            var inhgResult = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:F0} inHg", 1.2f);
            Assert.AreEqual(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.FromInHg(1.2f), Pressure.Unit.KPa, Pressure.Unit.InHg, true));
            Assert.AreEqual(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.ToBar(Pressure.Standard.FromInHg(1.2f)), Pressure.Unit.Bar, Pressure.Unit.InHg, true));
            Assert.AreEqual(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.ToInHg(Pressure.Standard.FromInHg(1.2f)), Pressure.Unit.InHg, Pressure.Unit.InHg, true));
            Assert.AreEqual(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.ToKgfpCm2(Pressure.Standard.FromInHg(1.2f)), Pressure.Unit.KgfpCm2, Pressure.Unit.InHg, true));
            Assert.AreEqual(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.ToPSI(Pressure.Standard.FromInHg(1.2f)), Pressure.Unit.PSI, Pressure.Unit.InHg, true));

            var kgfResult = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:F1} " + FormatStrings.kgfpcm2, 1.2f);
            Assert.AreEqual(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.FromKgfpCm2(1.2f), Pressure.Unit.KPa, Pressure.Unit.KgfpCm2, true));
            Assert.AreEqual(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.ToBar(Pressure.Standard.FromKgfpCm2(1.2f)), Pressure.Unit.Bar, Pressure.Unit.KgfpCm2, true));
            Assert.AreEqual(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.ToInHg(Pressure.Standard.FromKgfpCm2(1.2f)), Pressure.Unit.InHg, Pressure.Unit.KgfpCm2, true));
            Assert.AreEqual(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.ToKgfpCm2(Pressure.Standard.FromKgfpCm2(1.2f)), Pressure.Unit.KgfpCm2, Pressure.Unit.KgfpCm2, true));
            Assert.AreEqual(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.ToPSI(Pressure.Standard.FromKgfpCm2(1.2f)), Pressure.Unit.PSI, Pressure.Unit.KgfpCm2, true));
        }

        [TestMethod]
        public void FormattedTimeStrings()
        {
            TimeSpan duration = new TimeSpan(1, 13, 37, 45, 20); //1d 13:37:45.020
            Assert.AreEqual("13:37:45", FormatStrings.FormatTime(duration.TotalSeconds));
            Assert.AreEqual("13:37:45.20", FormatStrings.FormatPreciseTime(duration.TotalSeconds));
            Assert.AreEqual("13:37", FormatStrings.FormatApproximateTime(duration.TotalSeconds));
        }
    }
}
