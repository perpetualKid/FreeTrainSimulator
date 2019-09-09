// COPYRIGHT 2009, 2010, 2011, 2013 by the Open Rails project.
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


using System;
using System.Globalization;
using System.Text;
using GNU.Gettext;
using Orts.Common.Calc;

namespace Orts.Common
{
    /// <summary>
    /// Class to convert various quantities (so a value with a unit) into nicely formatted strings for display
    /// </summary>
    public static class FormatStrings
    {
        public static GettextResourceManager Catalog = new GettextResourceManager("Orts.Common");

        public static string m = Catalog.GetString("m");
        public static string km = Catalog.GetString("km");
        public static string mm = Catalog.GetString("mm");
        public static string mi = Catalog.GetString("mi");
        public static string ft = Catalog.GetString("ft");
        public static string yd = Catalog.GetString("yd");
        public static string m2 = Catalog.GetString("m²");
        public static string ft2 = Catalog.GetString("ft²");
        public static string m3 = Catalog.GetString("m³");
        public static string ft3 = Catalog.GetString("ft³");
        public static string kmph = Catalog.GetString("km/h");
        public static string mph = Catalog.GetString("mph");
        public static string kpa = Catalog.GetString("kPa");
        public static string bar = Catalog.GetString("bar");
        public static string psi = Catalog.GetString("psi");
        public static string inhg = Catalog.GetString("inHg");
        public static string kgfpcm2 = Catalog.GetString("kgf/cm²");
        public static string kg = Catalog.GetString("kg");
        public static string t = Catalog.GetString("t");
        public static string tonUK = Catalog.GetString("t-uk");
        public static string tonUS = Catalog.GetString("t-us");
        public static string lb = Catalog.GetString("lb");
        public static string s = Catalog.GetString("s");
        public static string min = Catalog.GetString("min");
        public static string h = Catalog.GetString("h");
        public static string l = Catalog.GetString("L");
        public static string galUK = Catalog.GetString("g-uk");
        public static string galUS = Catalog.GetString("g-us");
        public static string rpm = Catalog.GetString("rpm");
        public static string kW = Catalog.GetString("kW");
        public static string hp = Catalog.GetString("hp"); // mechanical (or brake) horsepower
        public static string bhp = Catalog.GetString("bhp"); // boiler horsepower
        public static string kJ = Catalog.GetString("kJ");
        public static string MJ = Catalog.GetString("MJ");
        public static string btu = Catalog.GetString("BTU");
        public static string c = Catalog.GetString("°C");
        public static string f = Catalog.GetString("°F");
        public static string n = Catalog.GetString("N");
        public static string kN = Catalog.GetString("kN");
        public static string lbf = Catalog.GetString("lbf");
        public static string klbf = Catalog.GetString("klbf");

        /// <summary>
        /// Formatted unlocalized speed string, used in reports and logs.
        /// </summary>
        public static string FormatSpeed(float speed, bool isMetric)
        {
            return string.Format(CultureInfo.CurrentCulture,
                "{0:F1}{1}", Speed.MeterPerSecond.FromMpS(speed, isMetric), isMetric ? kmph : mph);
        }

        /// <summary>
        /// Formatted localized speed string, used to display tracking speed, with 1 decimal precision
        /// </summary>
        public static string FormatSpeedDisplay(float speed, bool isMetric)
        {
            return string.Format(CultureInfo.CurrentCulture,
                "{0:F1} {1}", Speed.MeterPerSecond.FromMpS(speed, isMetric), isMetric ? kmph : mph);
        }

        /// <summary>
        /// Formatted localized speed string, used to display speed limits, with 0 decimal precision
        /// </summary>
        public static string FormatSpeedLimit(float speed, bool isMetric)
        {
            return string.Format(CultureInfo.CurrentCulture,
                "{0:F0} {1}", Speed.MeterPerSecond.FromMpS(speed, isMetric), isMetric ? kmph : mph);
        }

        /// <summary>
        /// Formatted localized speed string, used to display speed limits, with 0 decimal precision and no unit of measure
        /// </summary>
        public static string FormatSpeedLimitNoUoM(float speed, bool isMetric)
        {
            return string.Format(CultureInfo.CurrentCulture,
                "{0:F0}", Speed.MeterPerSecond.FromMpS(speed, isMetric));
        }

        /// <summary>
        /// Formatted unlocalized distance string, used in reports and logs.
        /// </summary>
        public static string FormatDistance(float distance, bool isMetric)
        {
            if (isMetric)
            {
                // <0.1 kilometres, show metres.
                if (Math.Abs(distance) < 100)
                {
                    return string.Format(CultureInfo.CurrentCulture,
                        "{0:N0}m", distance);
                }
                return string.Format(CultureInfo.CurrentCulture,
                    "{0:F1}km", Size.Length.ToKM(distance));
            }
            // <0.1 miles, show yards.
            if (Math.Abs(distance) < Size.Length.FromMi(0.1f))
            {
                return string.Format(CultureInfo.CurrentCulture, "{0:N0}yd", Size.Length.ToYd(distance));
            }
            return string.Format(CultureInfo.CurrentCulture, "{0:F1}mi", Size.Length.ToMi(distance));
        }

        /// <summary>
        /// Formatted localized distance string, as displayed in in-game windows
        /// </summary>
        public static string FormatDistanceDisplay(float distance, bool isMetric)
        {
            if (isMetric)
            {
                // <0.1 kilometres, show metres.
                if (Math.Abs(distance) < 100)
                {
                    return string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", distance, m);
                }
                return string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", Size.Length.ToKM(distance), km);
            }
            // <0.1 miles, show yards.
            if (Math.Abs(distance) < Size.Length.FromMi(0.1f))
            {
                return string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", Size.Length.ToYd(distance), yd);
            }
            return string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", Size.Length.ToMi(distance), mi);
        }

        public static string FormatShortDistanceDisplay(float distanceM, bool isMetric)
        {
            if (isMetric)
                return string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", distanceM, m);
            return string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", Size.Length.ToFt(distanceM), ft);
        }

        public static string FormatVeryShortDistanceDisplay(float distanceM, bool isMetric)
        {
            if (isMetric)
                return string.Format(CultureInfo.CurrentCulture, "{0:N3} {1}", distanceM, m);
            return string.Format(CultureInfo.CurrentCulture, "{0:N3} {1}", Size.Length.ToFt(distanceM), ft);
        }

        /// <summary>
        /// format localized mass string, as displayed in in-game windows.
        /// </summary>
        /// <param name="massKg">mass in kg or in Lb</param>
        /// <param name="isMetric">use kg if true, Lb if false</param>
        public static string FormatMass(float mass, bool isMetric)
        {
            if (isMetric)
            {
                // < 1 tons, show kilograms.
                float tonnes = Mass.Kilogram.ToTonnes(mass);
                if (Math.Abs(tonnes) > 1)
                {
                    return string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", tonnes, t);
                }
                else
                {
                    return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", mass, kg);
                }
            }
            else
            {
                return string.Format(CultureInfo.CurrentCulture,"{0:F0} {1}", Mass.Kilogram.ToLb(mass), lb);
            }
        }

        /// <summary>
        /// format localized mass string, as displayed in in-game windows, to Metric Kg/Tonne or UK or British Tons
        /// </summary>
        public static string FormatLargeMass(float mass, bool isMetric, bool isUK)
        {
            if (isMetric)
                return FormatMass(mass, isMetric);

            var massT = isUK ? Mass.Kilogram.ToTonsUK(mass) : Mass.Kilogram.ToTonsUS(mass);
            if (massT > 1)
                return string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", massT, isUK ? tonUK : tonUS);
            else
                return FormatMass(mass, isMetric);
        }

        /// <summary>
        /// Format localized area
        /// </summary>
        public static string FormatArea(float area, bool isMetric)
        {
            area = isMetric ? area : Size.Area.ToFt2(area);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", area, isMetric ? m2 : ft2);
        }

        /// <summary>
        /// Format localized qubic volume
        /// </summary>
        public static string FormatVolume(float volume, bool isMetric)
        {
            volume = isMetric ? volume : Size.Volume.ToFt3(volume);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", volume, isMetric ? m3 : ft3);
        }

        /// <summary>
        /// Format localized liquid volume
        /// </summary>
        public static string FormatFuelVolume(float volume, bool isMetric, bool isUK)
        {
            volume = isMetric ? volume : isUK ? Size.LiquidVolume.ToGallonUK(volume) : Size.LiquidVolume.ToGallonUS(volume);
            return string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", volume, isMetric ? l : isUK ? galUK : galUS);
        }

        public static string FormatPower(float power, bool isMetric, bool isImperialBHP, bool isImperialBTUpS)
        {
            power = isMetric ? Dynamics.Power.ToKW(power) : 
                isImperialBHP ? Dynamics.Power.ToBhp(power) : 
                isImperialBTUpS ? Dynamics.Power.ToBTUpS(power) : 
                Dynamics.Power.ToHp(power);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", power, isMetric ? kW : isImperialBHP ? bhp : isImperialBTUpS ? string.Format("{0}/{1}", btu, s) : hp);
        }

        public static string FormatForce(float force, bool isMetric)
        {
            bool kilo = false;
            force = isMetric ? force : Dynamics.Force.ToLbf(force);
            if (kilo = Math.Abs(force) > 1e4f)
                force *= 1e-3f;
            string unit = isMetric ? kilo ? kN : n : kilo ? klbf : lbf;
            return string.Format(CultureInfo.CurrentCulture, kilo ? "{0:F1} {1}" : "{0:F0} {1}", force, unit);
        }

        public static string FormatTemperature(float temperature, bool isMetric)
        {
            temperature = isMetric ? temperature : Temperature.Celsius.ToF(temperature);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0}{1}", temperature, isMetric ? c : f);
        }

        public static string FormatEnergyDensityByMass(float energyDensity, bool isMetric)
        {
            energyDensity = isMetric ? energyDensity : Energy.Density.Mass.ToBTUpLb(energyDensity);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}/{2}", energyDensity, isMetric ? kJ : btu, isMetric ? kg : lb);
        }

        public static string FormatEnergyDensityByVolume(float energyDensity, bool isMetric)
        {
            energyDensity = isMetric ? energyDensity : Energy.Density.Volume.ToBTUpFt3(energyDensity);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}/{2}", energyDensity, isMetric ? kJ : btu, String.Format("{0}³", isMetric ? m : ft));
        }

        public static string FormatEnergy(float energy, bool isMetric)
        {
            energy = isMetric ? energy * 1e-6f : Dynamics.Power.ToBTUpS(energy);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", energy, isMetric ? MJ : btu);
        }

        /// <summary>
        /// Formatted localized pressure string
        /// </summary>
        public static string FormatPressure(float pressure, Pressure.Unit inputUnit, Pressure.Unit outputUnit, bool unitDisplayed)
        {
            if (inputUnit == Pressure.Unit.None || outputUnit == Pressure.Unit.None)
                return string.Empty;

            float pressureKPa = Pressure.Standard.ToKPa(pressure, inputUnit);
            float pressureOut = Pressure.Standard.FromKPa(pressureKPa, outputUnit);

            string unit;
            StringBuilder format = new StringBuilder();
            switch (outputUnit)
            {
                case Pressure.Unit.KPa:
                    unit = kpa;
                    format.Append("{0:F0}");
                    break;
                case Pressure.Unit.Bar:
                    unit = bar;
                    format.Append("{0:F1}");
                    break;
                case Pressure.Unit.PSI:
                    unit = psi;
                    format.Append("{0:F0}");
                    break;
                case Pressure.Unit.InHg:
                    unit = inhg;
                    format.Append("{0:F0}");
                    break;
                case Pressure.Unit.KgfpCm2:
                    unit = kgfpcm2;
                    format.Append("{0:F1}");
                    break;
                default:
                    unit = string.Empty;
                    break;
            }

            if (unitDisplayed)
            {
                format.Append(" ");
                format.Append(unit);
            }

            return string.Format(CultureInfo.CurrentCulture, format.ToString(), pressureOut);
        }

        /// <summary>
        /// Converts duration in floating-point seconds to whole hours, minutes and seconds (rounded down).
        /// Returns the time in HH:MM:SS format limited to 24hr (1d) max
        /// </summary>
        public static string FormatTime(double clockTimeSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(clockTimeSeconds);

            return string.Format("{0:D2}:{1:D2}:{2:D2}", duration.Hours, duration.Minutes, duration.Seconds);
        }

        /// <summary>
        /// Converts duration in floating-point seconds to whole hours, minutes and seconds and 2 decimal places of seconds.
        /// Returns the time in HH:MM:SS.SS format limited to 24hr (1d) max
        /// </summary>
        public static string FormatPreciseTime(double clockTimeSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(clockTimeSeconds);

            return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D2}", duration.Hours, duration.Minutes, duration.Seconds, duration.Milliseconds);
        }


        /// <summary>
        /// Converts duration in floating-point seconds to whole hours and minutes (rounded to nearest).
        /// Returns the time in HH:MM format limited to 24hr (1d) max
        /// </summary>
        public static string FormatApproximateTime(double clockTimeSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(clockTimeSeconds);

            return string.Format("{0:D2}:{1:D2}", duration.Hours, duration.Minutes);
        }
    }
}
