using System;

namespace Orts.Common.Calc
{
    // Classes are provided for converting into and out of these internal units.
    // OR will use metric units (m, kg, s, A, 'C) for internal properties and calculations, preferably from SI (m/s, not km/hr).
    // Use these classes rather than in-line literal factors.
    //
    // For example to convert a number from metres to inches, use "DiameterIn = M.ToIn(DiameterM);"
    //
    // Many units begin with a lowercase letter (kg, kW, in, lb) but capitalised here (Kg, KW, In, Lb) for ease of reading.
    //
    //  //2019-07 below comment is questionable but not further investigated one way or another
    // Web research suggests that VC++ will optimize "/ 2.0" replacing it with "* 0.5f" but VC# will not and cost is around 15 cycles.
    // To prevent this, we replace "/ 2.0f" by "(1.0f / 2.0f)", which will be replaced by "*0.5f" already in CIL code (verified in CIL).
    // This enables us to use the same number for both directions of conversion, while not costing any speed.
    //
    // Also because of performance reasons, derived quantities still are hard-coded, instead of calling basic conversions and do multiplication
    //

    /// <summary>
    /// Frequency conversions
    /// </summary>
    public static class Frequency
    {
        /// <summary>
        /// Angular Frequency conversions
        /// </summary>
        public static class Angular
        {
            /// <summary>
            /// Frequency conversion from rad/s to Hz
            /// </summary>
            /// <param name="rad">Frequency in radians per second</param>
            /// <returns>Frequency in Hertz</returns>
            public static double RadToHz(double rad)
            {
                return (rad / (2 * Math.PI));
            }

            /// <summary>
            /// Frequenc conversion from Hz to rad/s
            /// </summary>
            /// <param name="hz">Frequenc in Hertz</param>
            /// <returns>Frequency in radians per second</returns>
            public static double HzToRad(double hz)
            {
                return (2 * Math.PI * hz);
            }
        }

        /// <summary>
        /// Frequency conversions from and to Hz (events/sec)
        /// </summary>
        public static class Periodic
        {
            /// <summary>Convert from per Minute to per Second</summary>
            public static float FromMinutes(float eventsPerMinute) { return eventsPerMinute * (1.0f / 60f); }
            /// <summary>Convert from per Second to per Minute</summary>
            public static float ToMinutes(float eventsPerSecond) { return eventsPerSecond * 60f; }
            /// <summary>Convert from per Hour to per Second</summary>
            public static float FromHours(float eventsPerHour) { return eventsPerHour * (1.0f / 3600f); }
            /// <summary>Convert from per Second to per Hour</summary>
            public static float ToHours(float eventsPerSecond) { return eventsPerSecond * 3600f; }
        }

    }

    /// <summary>
    /// Time conversions
    /// </summary>
    public static class Time
    {
        /// <summary>
        /// Time conversions from and to Seconds
        /// </summary>
        public static class Second
        {
            /// <summary>Convert from minutes to seconds</summary>
            public static float FromM(float minutes) { return minutes * 60f; }
            /// <summary>Convert from seconds to minutes</summary>
            public static float ToM(float seconds) { return seconds * (1.0f / 60f); }
            /// <summary>Convert from hours to seconds</summary>
            public static float FromH(float hours) { return hours * 3600f; }
            /// <summary>Convert from seconds to hours</summary>
            public static float ToH(float seconds) { return seconds * (1.0f / 3600f); }
        }

        /// <summary>
        /// Compare daytimes (given in seconds) taking into account times after midnight
        /// (morning comes after night comes after evening, but morning is before afternoon, which is before evening)
        /// </summary>
        public static class Compare
        {
            private const int eightHundredHours = 8 * 3600;
            private const int sixteenHundredHours = 16 * 3600;

            /// <summary>
            /// Return the latest time of the two input times, keeping in mind that night/morning is after evening/night
            /// </summary>
            public static int Latest(int timeOfDay1, int timeOfDay2)
            {
                if (timeOfDay1 > sixteenHundredHours && timeOfDay2 < eightHundredHours)
                {
                    return (timeOfDay2);
                }
                else if (timeOfDay1 < eightHundredHours && timeOfDay2 > sixteenHundredHours)
                {
                    return (timeOfDay1);
                }
                else if (timeOfDay1 > timeOfDay2)
                {
                    return (timeOfDay1);
                }
                return (timeOfDay2);
            }

            /// <summary>
            /// Return the Earliest time of the two input times, keeping in mind that night/morning is after evening/night
            /// </summary>
            public static int Earliest(int timeOfDay1, int timetimeOfDay2)
            {
                if (timeOfDay1 > sixteenHundredHours && timetimeOfDay2 < eightHundredHours)
                {
                    return (timeOfDay1);
                }
                else if (timeOfDay1 < eightHundredHours && timetimeOfDay2 > sixteenHundredHours)
                {
                    return (timetimeOfDay2);
                }
                else if (timeOfDay1 > timetimeOfDay2)
                {
                    return (timetimeOfDay2);
                }
                return (timeOfDay1);
            }
        }
    }

    /// <summary>
    /// Current conversions
    /// </summary>
    public static class Current
    {
        /// <summary>
        /// Current conversions from and to Amps
        /// </summary>
        public static class Ampere
        {

        }
    }

    /// <summary>
    /// Size (length, area, volume) conversions
    /// </summary>
    public static class Size
    {
        /// <summary>
        /// Length (distance) conversions from and to metres
        /// </summary>
        public static class Length
        {
            /// <summary>Convert (statute or land) miles to metres</summary>
            public static float FromMi(float miles) { return miles * 1609.344f; }
            /// <summary>Convert metres to (statute or land) miles</summary>
            public static float ToMi(float metres) { return metres * (1.0f / 1609.344f); }
            /// <summary>Convert kilometres to metres</summary>
            public static float FromKM(float kilometer) { return kilometer * 1000f; }
            /// <summary>Convert metres to kilometres</summary>
            public static float ToKM(float metres) { return metres * (1.0f / 1000f); }
            /// <summary>Convert yards to metres</summary>
            public static float FromYd(float yards) { return yards * 0.9144f; }
            /// <summary>Convert metres to yards</summary>
            public static float ToYd(float metres) { return metres * (1.0f / 0.9144f); }
            /// <summary>Convert feet to metres</summary>
            public static float FromFt(float feet) { return feet * 0.3048f; }
            /// <summary>Convert metres to feet</summary>
            public static float ToFt(float metres) { return metres * (1.0f / 0.3048f); }
            /// <summary>Convert inches to metres</summary>
            public static float FromIn(float inches) { return inches * 0.0254f; }
            /// <summary>Convert metres to inches</summary>
            public static float ToIn(float metres) { return metres * (1.0f / 0.0254f); }

            /// <summary>
            /// Convert from metres into kilometres or miles, depending on the flag isMetric
            /// </summary>
            /// <param name="distance">distance in metres</param>
            /// <param name="isMetric">if true convert to kilometres, if false convert to miles</param>
            public static float FromM(float distance, bool isMetric)
            {
                return isMetric ? ToKM(distance) : ToMi(distance);
            }
             
            /// <summary>
            /// Convert to metres from kilometres or miles, depending on the flag isMetric
            /// </summary>
            /// <param name="distance">distance to be converted to metres</param>
            /// <param name="isMetric">if true convert from kilometres, if false convert from miles</param>
            public static float ToM(float distance, bool isMetric)
            {
                return isMetric ? FromKM(distance) : FromMi(distance);
            }

        }

        /// <summary>
        /// Area conversions from and to m^2
        /// </summary>
        public static class Area
        {
            /// <summary>Convert from feet squared to metres squared</summary>
            public static float FromFt2(float squareFeet) { return squareFeet * 0.092903f; }
            /// <summary>Convert from metres squared to feet squared</summary>
            public static float ToFt2(float squareMetres) { return squareMetres * (1.0f / 0.092903f); }
            /// <summary>Convert from inches squared to metres squared</summary>
            public static float FromIn2(float squareFeet) { return squareFeet * (1.0f / 1550.0031f); }
            /// <summary>Convert from metres squared to inches squared</summary>
            public static float ToIn2(float squareMetres) { return squareMetres * 1550.0031f; }
        }

        /// <summary>
        /// Volume conversions from and to m^3
        /// </summary>
        public static class Volume
        {
            /// <summary>Convert from cubic feet to cubic metres</summary>
            public static float FromFt3(float qubicFeet) { return qubicFeet * (1.0f / 35.3146665722f); }
            /// <summary>Convert from cubic metres to cubic feet</summary>
            public static float ToFt3(float qubicMetres) { return qubicMetres * 35.3146665722f; }
            /// <summary>Convert from cubic inches to cubic metres</summary>
            public static float FromIn3(float qubicInches) { return qubicInches * (1.0f / 61023.7441f); }
            /// <summary>Convert from cubic metres to cubic inches</summary>
            public static float ToIn3(float qubicMetres) { return qubicMetres * 61023.7441f; }

        }

        /// <summary>
        /// Liquid volume conversions from and to Litres
        /// </summary>
        public static class LiquidVolume
        {
            /// <summary>Convert from UK Gallons to litres</summary>
            public static float FromGallonUK(float gallonUK) { return gallonUK * 4.54609f; }
            /// <summary>Convert from litres to UK Gallons</summary>
            public static float ToGallonUK(float litre) { return litre * (1.0f / 4.54609f); }
            /// <summary>Convert from US Gallons to litres</summary>
            public static float FromGallonUS(float gallonUS) { return gallonUS * 3.78541f; }
            /// <summary>Convert from litres to US Gallons</summary>
            public static float ToGallonUS(float litre) { return litre * (1.0f / 3.78541f); }
        }
    }

    /// <summary>
    /// Mass conversions
    /// </summary>
    public static class Mass
    {
        /// <summary>
        /// Mass conversions from and to Kilograms
        /// </summary>
        public static class Kilogram
        {
            /// <summary>Convert from pounds (lb) to kilograms</summary>
            public static float FromLb(float lb) { return lb * (1.0f / 2.20462f); }
            /// <summary>Convert from kilograms to pounds (lb)</summary>
            public static float ToLb(float kg) { return kg * 2.20462f; }
            /// <summary>Convert from US Tons to kilograms</summary>
            public static float FromTonsUS(float tonsUS) { return tonsUS * 907.1847f; }
            /// <summary>Convert from kilograms to US Tons</summary>
            public static float ToTonsUS(float kg) { return kg * (1.0f / 907.1847f); }
            /// <summary>Convert from UK Tons to kilograms</summary>
            public static float FromTonsUK(float tonsUK) { return tonsUK * 1016.047f; }
            /// <summary>Convert from kilograms to UK Tons</summary>
            public static float ToTonsUK(float kg) { return kg * (1.0f / 1016.047f); }
            /// <summary>Convert from kilogram to metric tonnes</summary>
            public static float ToTonnes(float kg) { return kg * (1.0f / 1000.0f); }
            /// <summary>Convert from metrix tonnes to kilogram</summary>
            public static float FromTonnes(float tonnes) { return tonnes * 1000.0f; }
        }
    }

    /// <summary>
    /// Energy related conversions like Power, Force, Resistance, Stiffness
    /// </summary>
    public static class Dynamics
    {
        /// <summary>
        /// Stiffness conversions from and to Newtons/metre
        /// </summary>
        public static class Stiffness
        {
        }

        /// <summary>
        /// Resistance conversions from and to Newtons/metre/sec
        /// </summary>
        public static class Resistance
        {
        }

        /// <summary>
        /// Power conversions from and to Watts
        /// </summary>
        public static class Power
        {
            /// <summary>Convert from kiloWatts to Watts</summary>
            public static float FromKW(float kiloWatts) { return kiloWatts * 1000f; }
            /// <summary>Convert from Watts to kileWatts</summary>
            public static float ToKW(float watts) { return watts * (1.0f / 1000f); }
            /// <summary>Convert from HorsePower to Watts</summary>
            public static float FromHp(float horsePowers) { return horsePowers * 745.699872f; }
            /// <summary>Convert from Watts to HorsePower</summary>
            public static float ToHp(float watts) { return watts * (1.0f / 745.699872f); }
            /// <summary>Convert from BoilerHorsePower to Watts</summary>
            public static float FromBhp(float horsePowers) { return horsePowers * 9809.5f; }
            /// <summary>Convert from Watts to BoilerHorsePower</summary>
            public static float ToBhp(float watts) { return watts * (1.0f / 9809.5f); }
            /// <summary>Convert from British Thermal Unit (BTU) per second to watts</summary>
            public static float FromBTUpS(float btuPerSecond) { return btuPerSecond * 1055.05585f; }
            /// <summary>Convert from Watts to British Thermal Unit (BTU) per second</summary>
            public static float ToBTUpS(float watts) { return watts * (1.0f / 1055.05585f); }
        }

        /// <summary>
        /// Force conversions from and to Newtons
        /// </summary>
        public static class Force
        {
            /// <summary>Convert from pound-force to Newtons</summary>
            public static float FromLbf(float lbf) { return lbf * (1.0f / 0.224808943871f); }
            /// <summary>Convert from Newtons to Pound-force</summary>
            public static float ToLbf(float newton) { return newton * 0.224808943871f; }
        }

    }

    /// <summary>
    /// Temperature conversions
    /// </summary>
    public static class Temperature
    {
        /// <summary>
        /// Temperature conversions from and to Celsius
        /// </summary>
        public static class Celsius
        {
            /// <summary>Convert from degrees Fahrenheit to degrees Celcius</summary>
            public static float FromF(float fahrenheit) { return (fahrenheit - 32f) * (100f / 180f); }
            /// <summary>Convert from degrees Celcius to degrees Fahrenheit</summary>
            public static float ToF(float celcius) { return celcius * (180f / 100f) + 32f; }
            /// <summary>Convert from Kelvin to degrees Celcius</summary>
            public static float FromK(float kelvin) { return kelvin - 273.15f; }
            /// <summary>Convert from degress Celcius to Kelvin</summary>
            public static float ToK(float celcius) { return celcius + 273.15f; }
        }

        /// <summary>
        /// Temperature conversions from and to Kelvin
        /// </summary>
        public static class Kelvin
        {
            /// <summary>Convert from degrees Fahrenheit to degrees Celcius</summary>
            public static float FromF(float fahrenheit) { return (fahrenheit - 32f) * (100f / 180f) + 273.15f; }
            /// <summary>Convert from degrees Celcius to degrees Fahrenheit</summary>
            public static float ToF(float kelvin) { return (kelvin - 273.15f) * (180f / 100f) + 32f; }
            /// <summary>Convert from Celcisu degress to Kelvin</summary>
            public static float FromC(float celcius) { return celcius + 273.15f; }
            /// <summary>Convert from Kelvin to degress Celcius</summary>
            public static float ToC(float kelvin) { return kelvin - 273.15f; }

        }
    }

    /// <summary>
    /// Speed conversions 
    /// </summary>
    public static class Speed
    {
        /// <summary>
        /// Speed conversions from and to metres/sec
        /// </summary>
        public static class MeterPerSecond
        {
            /// <summary>Convert miles/hour to metres/second</summary>
            public static float FromMpH(float milesPerHour) { return milesPerHour * (1.0f / 2.23693629f); }
            /// <summary>Convert metres/second to miles/hour</summary>
            public static float ToMpH(float metersPerSecond) { return metersPerSecond * 2.23693629f; }
            /// <summary>Convert kilometre/hour to metres/second</summary>
            public static float FromKpH(float kilometersPerHour) { return kilometersPerHour * (1.0f / 3.600f); }
            /// <summary>Convert metres/second to kilometres/hour</summary>
            public static float ToKpH(float metersPerSecond) { return metersPerSecond * 3.600f; }

            /// <summary>
            /// Convert from metres/second to kilometres/hour or miles/hour, depending on value of isMetric
            /// </summary>
            /// <param name="speed">speed in metres/second</param>
            /// <param name="isMetric">true to convert to kilometre/hour, false to convert to miles/hour</param>
            public static float FromMpS(float speed, bool isMetric)
            {
                return isMetric ? ToKpH(speed) : ToMpH(speed);
            }

            /// <summary>
            /// Convert to metres/second from kilometres/hour or miles/hour, depending on value of isMetric
            /// </summary>
            /// <param name="speed">speed to be converted to metres/second</param>
            /// <param name="isMetric">true to convert from kilometre/hour, false to convert from miles/hour</param>
            public static float ToMpS(float speed, bool isMetric)
            {
                return isMetric ? FromKpH(speed) : FromMpH(speed);
            }
        }

    }

    /// <summary>
    /// Rate changes conversions
    /// </summary>
    public static class Rate
    {
        /// <summary>
        /// Flow rate conversions
        /// </summary>
        public static class Flow
        {
            /// <summary>
            /// Mass rate conversions from and to Kg/s
            /// </summary>
            public static class Mass
            {
                /// <summary>Convert from pound/hour to kilograms/second</summary>
                public static float FromLbpH(float poundsPerHour) { return poundsPerHour * (1.0f / 7936.64144f); }
                /// <summary>Convert from kilograms/second to pounds/hour</summary>
                public static float ToLbpH(float kilogramsPerSecond) { return kilogramsPerSecond * 7936.64144f; }
            }
        }

        /// <summary>
        /// Pressure rate conversions from and to bar/s
        /// </summary>
        public static class Pressure
        {
            /// <summary>Convert from Pounds per square Inch per second to bar per second</summary>
            public static float FromPSIpS(float psi) { return psi * (1.0f / 14.5037738f); }
            /// <summary>Convert from</summary>
            public static float ToPSIpS(float bar) { return bar * 14.5037738f; }
        }
    }

    /// <summary>
    /// Energy conversions
    /// </summary>
    public static class Energy
    {
        /// <summary>
        /// Energy density conversions
        /// </summary>
        public static class Density
        {
            /// <summary>
            /// Energy density conversions from and to kJ/Kg
            /// </summary>
            public static class Mass
            {
                /// <summary>Convert from Britisch Thermal Units per Pound to kiloJoule per kilogram</summary>
                public static float FromBTUpLb(float btuPerPound) { return btuPerPound * 2.326f; }
                /// <summary>Convert from kiloJoule per kilogram to Britisch Thermal Units per Pound</summary>
                public static float ToBTUpLb(float kJPerkg) { return kJPerkg * (1.0f / 2.326f); }
            }

            /// <summary>
            /// Energy density conversions from and to kJ/m^3
            /// </summary>
            public static class Volume
            {
                /// <summary>Convert from Britisch Thermal Units per ft^3 to kiloJoule per m^3</summary>
                public static float FromBTUpFt3(float btuPerFt3) { return btuPerFt3 * (1f / 37.3f); }
                /// <summary>Convert from kiloJoule per m^3 to Britisch Thermal Units per ft^3</summary>
                public static float ToBTUpFt3(float kJPerM3) { return kJPerM3 * 37.3f; }
            }

        }
    }

    /// <summary>
    /// Pressure conversion
    /// </summary>
    public static class Pressure
    {
        /// <summary>
        /// Various units of pressure that are used
        /// </summary>
        public enum Unit
        {
            /// <summary>non-defined unit</summary>
            None,
            /// <summary>kiloPascal</summary>
            KPa,
            /// <summary>bar</summary>
            Bar,
            /// <summary>Pounds Per Square Inch</summary>
            PSI,
            /// <summary>Inches Mercury</summary>
            InHg,
            /// <summary>Mass-force per square centimetres</summary>
            KgfpCm2
        }

        /// <summary>
        /// convert vacuum values to psia for vacuum brakes
        /// </summary>
        public static class Vacuum
        {
            readonly static float OneAtmospherePSI = Atmospheric.ToPSI(1);
            /// <summary>vacuum in inhg to pressure in psia</summary>
            public static float ToPressure(float vacuum) { return OneAtmospherePSI - Atmospheric.ToPSI(Atmospheric.FromInHg(vacuum)); }
            /// <summary>convert pressure in psia to vacuum in inhg</summary>
            public static float FromPressure(float pressure) { return Atmospheric.ToInHg(Atmospheric.FromPSI(OneAtmospherePSI - pressure)); }
        }

        /// <summary>
        /// Pressure conversions from and to bar
        /// </summary>
        public static class Atmospheric
        {
            /// <summary>Convert from kiloPascal to Bar</summary>
            public static float FromKPa(float kiloPascal) { return kiloPascal * (1.0f / 100.0f); }
            /// <summary>Convert from bar to kiloPascal</summary>
            public static float ToKPa(float bar) { return bar * 100.0f; }
            /// <summary>Convert from Pounds per Square Inch to Bar</summary>
            public static float FromPSI(float poundsPerSquareInch) { return poundsPerSquareInch * (1.0f / 14.5037738f); }
            /// <summary>Convert from Bar to Pounds per Square Inch</summary>
            public static float ToPSI(float bar) { return bar * 14.5037738f; }
            /// <summary>Convert from Inches Mercury to bar</summary>
            public static float FromInHg(float inchesMercury) { return inchesMercury * 0.03386389f; }
            /// <summary>Convert from bar to Inches Mercury</summary>
            public static float ToInHg(float bar) { return bar * (1.0f / 0.03386389f); }
            /// <summary>Convert from mass-force per square metres to bar</summary>
            public static float FromKgfpCm2(float f) { return f * (1.0f / 1.0197f); }
            /// <summary>Convert from bar to mass-force per square metres</summary>
            public static float ToKgfpCm2(float bar) { return bar * 1.0197f; }
        }

        /// <summary>
        /// Pressure conversions from and to kilopascals
        /// </summary>
        public static class Standard
        {
            /// <summary>Convert from Pounds per Square Inch to kiloPascal</summary>
            public static float FromPSI(float psi) { return psi * 6.89475729f; }
            /// <summary>Convert from kiloPascal to Pounds per Square Inch</summary>
            public static float ToPSI(float kiloPascal) { return kiloPascal * (1.0f / 6.89475729f); }
            /// <summary>Convert from Inches Mercury to kiloPascal</summary>
            public static float FromInHg(float inchesMercury) { return inchesMercury * 3.386389f; }
            /// <summary>Convert from kiloPascal to Inches Mercury</summary>
            public static float ToInHg(float kiloPascal) { return kiloPascal * (1.0f / 3.386389f); }
            /// <summary>Convert from Bar to kiloPascal</summary>
            public static float FromBar(float bar) { return bar * 100.0f; }
            /// <summary>Convert from kiloPascal to Bar</summary>
            public static float ToBar(float kiloPascal) { return kiloPascal * (1.0f / 100.0f); }
            /// <summary>Convert from mass-force per square metres to kiloPascal</summary>
            public static float FromKgfpCm2(float f) { return f * 98.068059f; }
            /// <summary>Convert from kiloPascal to mass-force per square centimetres</summary>
            public static float ToKgfpCm2(float kiloPascal) { return kiloPascal * (1.0f / 98.068059f); }

            /// <summary>
            /// Convert from KPa to any pressure unit
            /// </summary>
            /// <param name="pressure">pressure to convert from</param>
            /// <param name="outputUnit">Unit to convert To</param>
            public static float FromKPa(float pressure, Unit outputUnit)
            {
                switch (outputUnit)
                {
                    case Unit.KPa:
                        return pressure;
                    case Unit.Bar:
                        return ToBar(pressure);
                    case Unit.InHg:
                        return ToInHg(pressure);
                    case Unit.KgfpCm2:
                        return ToKgfpCm2(pressure);
                    case Unit.PSI:
                        return ToPSI(pressure);
                    default:
                        throw new ArgumentOutOfRangeException("Pressure unit not recognized");
                }
            }

            /// <summary>
            /// Convert from any pressure unit to KPa
            /// </summary>
            /// <param name="pressure">pressure to convert from</param>
            /// <param name="inputUnit">Unit to convert from</param>
            public static float ToKPa(float pressure, Unit inputUnit)
            {
                switch (inputUnit)
                {
                    case Unit.KPa:
                        return pressure;
                    case Unit.Bar:
                        return FromBar(pressure);
                    case Unit.InHg:
                        return FromInHg(pressure);
                    case Unit.KgfpCm2:
                        return FromKgfpCm2(pressure);
                    case Unit.PSI:
                        return FromPSI(pressure);
                    default:
                        throw new ArgumentOutOfRangeException("Pressure unit not recognized");
                }
            }
        }
    }
}
