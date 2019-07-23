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
    }
}
