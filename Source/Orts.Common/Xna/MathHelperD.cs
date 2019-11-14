using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.Common.Xna
{
    public class MathHelperD
    {
        /// Restricts a value to be within a specified range.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum value. If <c>value</c> is less than <c>min</c>, <c>min</c> will be returned.</param>
        /// <param name="max">The maximum value. If <c>value</c> is greater than <c>max</c>, <c>max</c> will be returned.</param>
        /// <returns>The clamped value.</returns>
        public static double Clamp(double value, double min, double max)
        {
            // First we check to see if we're greater than the max
            return (value > max) ? max : (value < min) ? min : value; ;
        }

        /// <summary>
        /// Reduces a given angle to a value between π and -π.
        /// </summary>
        /// <param name="angle">The angle to reduce, in radians.</param>
        /// <returns>The new angle, in radians.</returns>
        public static double WrapAngle(double angle)
        {
            const double twoPi = Math.PI * 2.0;
            if ((angle > -Math.PI) && (angle <= Math.PI))
                return angle;
            angle %= twoPi;
            if (angle <= -Math.PI)
                return angle + twoPi;
            if (angle > Math.PI)
                return angle - twoPi;
            return angle;
        }

    }
}
