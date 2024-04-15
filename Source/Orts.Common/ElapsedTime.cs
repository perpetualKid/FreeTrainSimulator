// COPYRIGHT 2013 by the Open Rails project.
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


namespace Orts.Common
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct ElapsedTime
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        private static readonly ElapsedTime zero;

        public readonly double ClockSeconds;
        public readonly double RealSeconds;

        public static ref readonly ElapsedTime Zero => ref zero;

        public static ElapsedTime operator +(in ElapsedTime a, in ElapsedTime b)
        {
            return new ElapsedTime(a.ClockSeconds + b.ClockSeconds, a.RealSeconds + b.RealSeconds);
        }

        public static ElapsedTime Add(in ElapsedTime a, in ElapsedTime b)
        {
            return a + b;
        }

        public ElapsedTime(double clockSeconds, double realSeconds)
        {
            ClockSeconds = clockSeconds;
            RealSeconds = realSeconds;
        }
    }
}
