using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.Common.Calc
{
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
}
