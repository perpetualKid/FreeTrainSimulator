﻿// COPYRIGHT 2009, 2010, 2011, 2015 by the Open Rails project.
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

namespace FreeTrainSimulator.Common.Calc
{
    public static class AlmostEqualExtension
    {
        /// <summary>
        /// Returns true when the floating point value is *close to* the given value,
        /// within a given tolerance.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="other">The value to compare with.</param>
        /// <param name="tolerance">The amount the two values may differ while still being considered equal</param>
        /// <returns></returns>
        public static bool AlmostEqual(this float value, float other, float tolerance)
        {
            return Math.Round(Math.Abs(value - other), 4) <= tolerance;       //added rounding to overcome float imprecision. Using max fractional 4 digits 
        }

        /// <summary>
        /// Returns true when the floating point value is *close to* the given value,
        /// within a given tolerance.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="other">The value to compare with.</param>
        /// <param name="tolerance">The amount the two values may differ while still being considered equal</param>
        /// <returns></returns>
        public static bool AlmostEqual(this double value, double other, double tolerance)
        {
            return Math.Round(Math.Abs(value - other), 4) <= tolerance;       //added rounding to overcome float imprecision. Using max fractional 4 digits 
        }
    }
}
