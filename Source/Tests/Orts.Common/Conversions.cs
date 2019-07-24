// COPYRIGHT 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using Orts.Common;
using Orts.Common.Calc;
using System;
using System.Collections.Generic;
using Xunit;

namespace Orts.Tests.Orts.Common
{
    public static class Conversions
    {
        [Fact]
        public static void FormattedStrings()
        {
            // Note: Only pressure is tested at the moment, mainly because of its complexity.

            Assert.Equal(String.Empty, FormatStrings.FormatPressure(1.2f, Pressure.Unit.None, Pressure.Unit.KPa, true));
            Assert.Equal(String.Empty, FormatStrings.FormatPressure(1.2f, Pressure.Unit.KPa, Pressure.Unit.None, true));

            Assert.Equal("1 kPa", FormatStrings.FormatPressure(1.2f, Pressure.Unit.KPa, Pressure.Unit.KPa, true));
            Assert.Equal("1 kPa", FormatStrings.FormatPressure(Pressure.Standard.ToBar(1.2f), Pressure.Unit.Bar, Pressure.Unit.KPa, true));
            Assert.Equal("1 kPa", FormatStrings.FormatPressure(Pressure.Standard.ToInHg(1.2f), Pressure.Unit.InHg, Pressure.Unit.KPa, true));
            Assert.Equal("1 kPa", FormatStrings.FormatPressure(Pressure.Standard.ToKgfpCm2(1.2f), Pressure.Unit.KgfpCm2, Pressure.Unit.KPa, true));
            Assert.Equal("1 kPa", FormatStrings.FormatPressure(Pressure.Standard.ToPSI(1.2f), Pressure.Unit.PSI, Pressure.Unit.KPa, true));

            var barResult = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:F1} bar", 1.2f);
            Assert.Equal(barResult, FormatStrings.FormatPressure(Pressure.Atmospheric.ToKPa(1.2f), Pressure.Unit.KPa, Pressure.Unit.Bar, true));
            Assert.Equal(barResult, FormatStrings.FormatPressure(1.2f, Pressure.Unit.Bar, Pressure.Unit.Bar, true));
            Assert.Equal(barResult, FormatStrings.FormatPressure(Pressure.Atmospheric.ToInHg(1.2f), Pressure.Unit.InHg, Pressure.Unit.Bar, true));
            Assert.Equal(barResult, FormatStrings.FormatPressure(Pressure.Atmospheric.ToKgfpCm2(1.2f), Pressure.Unit.KgfpCm2, Pressure.Unit.Bar, true));
            Assert.Equal(barResult, FormatStrings.FormatPressure(Pressure.Atmospheric.ToPSI(1.2f), Pressure.Unit.PSI, Pressure.Unit.Bar, true));

            var psiResult = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:F0} psi", 1.2f);
            Assert.Equal(psiResult, FormatStrings.FormatPressure(Pressure.Standard.FromPSI(1.2f), Pressure.Unit.KPa, Pressure.Unit.PSI, true));
            Assert.Equal(psiResult, FormatStrings.FormatPressure(Pressure.Standard.ToBar(Pressure.Standard.FromPSI(1.2f)), Pressure.Unit.Bar, Pressure.Unit.PSI, true));
            Assert.Equal(psiResult, FormatStrings.FormatPressure(Pressure.Standard.ToInHg(Pressure.Standard.FromPSI(1.2f)), Pressure.Unit.InHg, Pressure.Unit.PSI, true));
            Assert.Equal(psiResult, FormatStrings.FormatPressure(Pressure.Standard.ToKgfpCm2(Pressure.Standard.FromPSI(1.2f)), Pressure.Unit.KgfpCm2, Pressure.Unit.PSI, true));
            Assert.Equal(psiResult, FormatStrings.FormatPressure(Pressure.Standard.ToPSI(Pressure.Standard.FromPSI(1.2f)), Pressure.Unit.PSI, Pressure.Unit.PSI, true));

            var inhgResult = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:F0} inHg", 1.2f);
            Assert.Equal(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.FromInHg(1.2f), Pressure.Unit.KPa, Pressure.Unit.InHg, true));
            Assert.Equal(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.ToBar(Pressure.Standard.FromInHg(1.2f)), Pressure.Unit.Bar, Pressure.Unit.InHg, true));
            Assert.Equal(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.ToInHg(Pressure.Standard.FromInHg(1.2f)), Pressure.Unit.InHg, Pressure.Unit.InHg, true));
            Assert.Equal(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.ToKgfpCm2(Pressure.Standard.FromInHg(1.2f)), Pressure.Unit.KgfpCm2, Pressure.Unit.InHg, true));
            Assert.Equal(inhgResult, FormatStrings.FormatPressure(Pressure.Standard.ToPSI(Pressure.Standard.FromInHg(1.2f)), Pressure.Unit.PSI, Pressure.Unit.InHg, true));

            var kgfResult = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:F1} kgf/cm^2", 1.2f);
            Assert.Equal(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.FromKgfpCm2(1.2f), Pressure.Unit.KPa, Pressure.Unit.KgfpCm2, true));
            Assert.Equal(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.ToBar(Pressure.Standard.FromKgfpCm2(1.2f)), Pressure.Unit.Bar, Pressure.Unit.KgfpCm2, true));
            Assert.Equal(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.ToInHg(Pressure.Standard.FromKgfpCm2(1.2f)), Pressure.Unit.InHg, Pressure.Unit.KgfpCm2, true));
            Assert.Equal(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.ToKgfpCm2(Pressure.Standard.FromKgfpCm2(1.2f)), Pressure.Unit.KgfpCm2, Pressure.Unit.KgfpCm2, true));
            Assert.Equal(kgfResult, FormatStrings.FormatPressure(Pressure.Standard.ToPSI(Pressure.Standard.FromKgfpCm2(1.2f)), Pressure.Unit.PSI, Pressure.Unit.KgfpCm2, true));
        }
    }
}
