using System;
using System.Globalization;
using System.Linq;
using System.Text;

using FreeTrainSimulator.Common.Calc;

using GetText;

namespace FreeTrainSimulator.Common
{
    /// <summary>
    /// Class to convert various quantities (so a value with a unit) into nicely formatted strings for display
    /// </summary>
    public static class FormatStrings
    {
#pragma warning disable CA1034 // Nested types should not be visible
        public static class Markers
#pragma warning restore CA1034 // Nested types should not be visible
        {
            public const string ArrowUp = "▲"; // \u25B2
            public const string ArrowDown = "▼"; // \u25BC
            public const string ArrowRight = "►"; // \u25BA
            public const string ArrowLeft = "◄"; // \u25C4

            public const string ArrowUpSmall = "˄"; // \u02C4 //"△"; // \u25B3
            public const string ArrowDownSmall = "˅"; // \u02C4 //"▽"; // \u25BD
            public const string ArrowLeftSmall = "˂"; // \u02C2
            public const string ArrowRigthSmall = "˃"; // \u02C3

            public const string Block = "█"; // \u2588
            public const string BlockUpperHalf = "▀"; // \u2580
            public const string BlockLowerHalf = "▄"; // \u2584
            public const string Fence = "│"; // \u2016 //"▐"; // \u2590
            public const string Dash = "―"; // \u2015
            public const string BlockHorizontal = "▬"; // \u25ac
            public const string BlockVertical = "▮"; // \u25ae

            public const string Descent = "↘"; // \u2198
            public const string Ascent = "↗"; // \u2197

            public const string Diamond = "◆"; // \u25C6
        }

        private static readonly Catalog catalog = CatalogManager.Catalog;

#pragma warning disable IDE1006 // Naming Styles
        public static string m { get; } = catalog.GetString("m");
        public static string km { get; } = catalog.GetString("km");
        public static string mm { get; } = catalog.GetString("mm");
        public static string mi { get; } = catalog.GetString("mi");
        public static string ft { get; } = catalog.GetString("ft");
        public static string yd { get; } = catalog.GetString("yd");
        public static string m2 { get; } = catalog.GetString("m²");
        public static string ft2 { get; } = catalog.GetString("ft²");
        public static string m3 { get; } = catalog.GetString("m³");
        public static string ft3 { get; } = catalog.GetString("ft³");
        public static string kmph { get; } = catalog.GetString("km/h");
        public static string mph { get; } = catalog.GetString("mph");
        public static string kpa { get; } = catalog.GetString("kPa");
        public static string bar { get; } = catalog.GetString("bar");
        public static string psi { get; } = catalog.GetString("psi");
        public static string inhg { get; } = catalog.GetString("inHg");
        public static string kgfpcm2 { get; } = catalog.GetString("kgf/cm²");
        public static string kg { get; } = catalog.GetString("kg");
        public static string t { get; } = catalog.GetString("t");
        public static string tonUK { get; } = catalog.GetString("t-uk");
        public static string tonUS { get; } = catalog.GetString("t-us");
        public static string lb { get; } = catalog.GetString("lb");
        public static string s { get; } = catalog.GetString("s");
        public static string min { get; } = catalog.GetString("min");
        public static string h { get; } = catalog.GetString("h");
        public static string l { get; } = catalog.GetString("L");
        public static string galUK { get; } = catalog.GetString("g-uk");
        public static string galUS { get; } = catalog.GetString("g-us");
        public static string rpm { get; } = catalog.GetString("rpm");
        public static string kW { get; } = catalog.GetString("kW");
        public static string hp { get; } = catalog.GetString("hp"); // mechanical (or brake) horsepower
        public static string bhp { get; } = catalog.GetString("bhp"); // boiler horsepower
        public static string kJ { get; } = catalog.GetString("kJ");
        public static string MJ { get; } = catalog.GetString("MJ");
        public static string btu { get; } = catalog.GetString("BTU");
        public static string c { get; } = catalog.GetString("°C");
        public static string f { get; } = catalog.GetString("°F");
        public static string n { get; } = catalog.GetString("N");
        public static string kN { get; } = catalog.GetString("kN");
        public static string lbf { get; } = catalog.GetString("lbf");
        public static string klbf { get; } = catalog.GetString("klbf");
#pragma warning restore IDE1006 // Naming Styles

        /// <summary>
        /// Formatted unlocalized speed string, used in reports and logs.
        /// </summary>
        public static string FormatSpeed(double speed, bool metric)
        {
            return string.Format(CultureInfo.CurrentCulture,
                "{0:F1}{1}", Speed.MeterPerSecond.FromMpS(speed, metric), metric ? kmph : mph);
        }

        /// <summary>
        /// Formatted localized speed string, used to display tracking speed, with 1 decimal precision
        /// </summary>
        public static string FormatSpeedDisplay(double speed, bool metric)
        {
            return string.Format(CultureInfo.CurrentCulture,
                "{0:F1} {1}", Speed.MeterPerSecond.FromMpS(speed, metric), metric ? kmph : mph);
        }

        /// <summary>
        /// Formatted localized speed string, used to display speed limits, with 0 decimal precision
        /// </summary>
        public static string FormatSpeedLimit(double speed, bool metric)
        {
            return string.Format(CultureInfo.CurrentCulture,
                "{0:F0} {1}", Speed.MeterPerSecond.FromMpS(speed, metric), metric ? kmph : mph);
        }

        /// <summary>
        /// Formatted localized speed string, used to display speed limits, with 0 decimal precision and no unit of measure
        /// </summary>
        public static string FormatSpeedLimitNoUoM(double speed, bool metric)
        {
            return string.Format(CultureInfo.CurrentCulture,
                "{0:F0}", Speed.MeterPerSecond.FromMpS(speed, metric));
        }

        /// <summary>
        /// Formatted unlocalized distance string, used in reports and logs.
        /// </summary>
        public static string FormatDistance(double distance, bool metric)
        {
            if (metric)
                // <0.1 kilometres, show metres.
                return Math.Abs(distance) < 100
                    ? string.Format(CultureInfo.CurrentCulture,
                        "{0:N0}m", distance)
                    : string.Format(CultureInfo.CurrentCulture,
                    "{0:F1}km", Size.Length.ToKM(distance));
            // <0.1 miles, show yards.
            return Math.Abs(distance) < Size.Length.FromMi(0.1)
                ? string.Format(CultureInfo.CurrentCulture, "{0:N0}yd", Size.Length.ToYd(distance))
                : string.Format(CultureInfo.CurrentCulture, "{0:F1}mi", Size.Length.ToMi(distance));
        }

        /// <summary>
        /// Formatted localized distance string, as displayed in in-game windows
        /// </summary>
        public static string FormatDistanceDisplay(double distance, bool metric)
        {
            if (metric)
                // <0.1 kilometres, show metres.
                return Math.Abs(distance) < 100
                    ? string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", distance, m)
                    : string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", Size.Length.ToKM(distance), km);
            // <0.1 miles, show yards.
            return Math.Abs(distance) < Size.Length.FromMi(0.1)
                ? string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", Size.Length.ToYd(distance), yd)
                : string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", Size.Length.ToMi(distance), mi);
        }

        public static string FormatShortDistanceDisplay(double distanceM, bool metric)
        {
            return metric
                ? string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", distanceM, m)
                : string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", Size.Length.ToFt(distanceM), ft);
        }

        public static string FormatVeryShortDistanceDisplay(double distanceM, bool metric)
        {
            return metric
                ? string.Format(CultureInfo.CurrentCulture, "{0:N3} {1}", distanceM, m)
                : string.Format(CultureInfo.CurrentCulture, "{0:N3} {1}", Size.Length.ToFt(distanceM), ft);
        }

        /// <summary>
        /// format localized mass string, as displayed in in-game windows.
        /// </summary>
        /// <param name="massKg">mass in kg or in Lb</param>
        /// <param name="metric">use kg if true, Lb if false</param>
        public static string FormatMass(double mass, bool metric)
        {
            if (metric)
            {
                // < 1 tons, show kilograms.
                double tonnes = Mass.Kilogram.ToTonnes(mass);
                return Math.Abs(tonnes) > 1
                    ? string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", tonnes, t)
                    : string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", mass, kg);
            }
            else
                return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", Mass.Kilogram.ToLb(mass), lb);
        }

        /// <summary>
        /// format localized mass string, as displayed in in-game windows, to Metric Kg/Tonne or UK or British Tons
        /// </summary>
        public static string FormatLargeMass(double mass, bool metric, bool isUK)
        {
            if (metric)
                return FormatMass(mass, metric);

            double massT = isUK ? Mass.Kilogram.ToTonsUK(mass) : Mass.Kilogram.ToTonsUS(mass);
            return massT > 1 ? string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", massT, isUK ? tonUK : tonUS) : FormatMass(mass, metric);
        }

        /// <summary>
        /// Format localized area
        /// </summary>
        public static string FormatArea(double area, bool metric)
        {
            area = metric ? area : Size.Area.ToFt2(area);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", area, metric ? m2 : ft2);
        }

        /// <summary>
        /// Format localized qubic volume
        /// </summary>
        public static string FormatVolume(double volume, bool metric)
        {
            volume = metric ? volume : Size.Volume.ToFt3(volume);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", volume, metric ? m3 : ft3);
        }

        /// <summary>
        /// Format localized liquid volume
        /// </summary>
        public static string FormatFuelVolume(double volume, bool metric, bool isUK)
        {
            volume = metric ? volume : isUK ? Size.LiquidVolume.ToGallonUK(volume) : Size.LiquidVolume.ToGallonUS(volume);
            return string.Format(CultureInfo.CurrentCulture, "{0:F1} {1}", volume, metric ? l : isUK ? galUK : galUS);
        }

        public static string FormatPower(double power, bool metric, bool isImperialBHP, bool isImperialBTUpS)
        {
            power = metric ? Dynamics.Power.ToKW(power) :
                isImperialBHP ? Dynamics.Power.ToBhp(power) :
                isImperialBTUpS ? Dynamics.Power.ToBTUpS(power) :
                Dynamics.Power.ToHp(power);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", power, metric ? kW : isImperialBHP ? bhp : isImperialBTUpS ? $"{btu}/{s}" : hp);
        }

        public static string FormatForce(double force, bool metric)
        {
            force = metric ? force : Dynamics.Force.ToLbf(force);
            bool kilo;
            if (kilo = Math.Abs(force) > 1e4f)
                force *= 1e-3f;
            string unit = metric ? kilo ? kN : n : kilo ? klbf : lbf;
            return string.Format(CultureInfo.CurrentCulture, kilo ? "{0:F1} {1}" : "{0:F0} {1}", force, unit);
        }

        public static string FormatTemperature(double temperature, bool metric)
        {
            temperature = metric ? temperature : Temperature.Celsius.ToF(temperature);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0}{1}", temperature, metric ? c : f);
        }

        public static string FormatEnergyDensityByMass(double energyDensity, bool metric)
        {
            energyDensity = metric ? energyDensity : Energy.Density.Mass.ToBTUpLb(energyDensity);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}/{2}", energyDensity, metric ? kJ : btu, metric ? kg : lb);
        }

        public static string FormatEnergyDensityByVolume(double energyDensity, bool metric)
        {
            energyDensity = metric ? energyDensity : Energy.Density.Volume.ToBTUpFt3(energyDensity);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}/{2}", energyDensity, metric ? kJ : btu, $"{(metric ? m : ft)}³");
        }

        public static string FormatEnergy(double energy, bool metric)
        {
            energy = metric ? energy * 1e-6f : Dynamics.Power.ToBTUpS(energy);
            return string.Format(CultureInfo.CurrentCulture, "{0:F0} {1}", energy, metric ? MJ : btu);
        }

        /// <summary>
        /// Formatted localized pressure string
        /// </summary>
        public static string FormatPressure(double pressure, Pressure.Unit inputUnit, Pressure.Unit outputUnit, bool unitDisplayed)
        {
            if (inputUnit == Pressure.Unit.None || outputUnit == Pressure.Unit.None)
                return string.Empty;

            double pressureKPa = Pressure.Standard.ToKPa(pressure, inputUnit);
            double pressureOut = Pressure.Standard.FromKPa(pressureKPa, outputUnit);

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
                format.Append(' ');
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
            return $"{TimeSpan.FromSeconds(clockTimeSeconds):hh\\:mm\\:ss}";
        }

        /// <summary>
        /// Converts duration in floating-point seconds to whole hours, minutes and seconds and 2 decimal places of seconds.
        /// Returns the time in HH:MM:SS.SS format limited to 24hr (1d) max
        /// </summary>
        public static string FormatPreciseTime(double clockTimeSeconds)
        {
            return $"{TimeSpan.FromSeconds(clockTimeSeconds):hh\\:mm\\:ss\\.FF}";
        }


        /// <summary>
        /// Converts duration in floating-point seconds to whole hours and minutes (rounded to nearest).
        /// Returns the time in HH:MM format limited to 24hr (1d) max
        /// </summary>
        public static string FormatApproximateTime(double clockTimeSeconds)
        {
            return $"{TimeSpan.FromSeconds(clockTimeSeconds):hh\\:mm}";
        }

        /// <summary>
        /// Converts a timespan in MM:SS format also if more than one hour
        /// Seconds are rounded to the nearest 10sec range
        /// </summary>
        public static string FormatDelayTime(TimeSpan delay)
        {
            return $"{(int)delay.TotalMinutes}:{delay.Seconds / 10 * 10:00}";
        }

        public static string Max(this string value, int length)
        {
            return value?[..Math.Min(value.Length, length)];
        }

        public static string JoinIfNotEmpty(char separator, params string[] values)
        {
            return string.Join(separator, values.Where(s => !string.IsNullOrEmpty(s)));
        }
    }
}
