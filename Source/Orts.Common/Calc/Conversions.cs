using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.Common.Calc
{
    public static class Frequency
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
}
