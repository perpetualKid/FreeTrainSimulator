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

// This module covers all classes and code for signal, speed post, track occupation and track reservation control

namespace Orts.Simulation.Signalling
{
    internal class SpeedInfo
    {
        public float PassengerSpeed { get; internal set; }
        public float FreightSpeed { get; internal set; }
        public bool Flag { get; internal set; }
        public bool Reset { get; internal set; }
        public int LimitedSpeedReduction { get; internal set; } // No Speed Reduction or is Temporary Speed Reduction
                                                                // for signals: if = 1 no speed reduction; for speedposts: if = 0 standard; = 1 start of temp speedreduction post; = 2 end of temp speed reduction post


        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public SpeedInfo(float passenger, float freight, bool asap, bool reset, int limitedSpeedReduction)
        {
            PassengerSpeed = passenger;
            FreightSpeed = freight;
            Flag = asap;
            Reset = reset;
            LimitedSpeedReduction = limitedSpeedReduction;
        }

        public SpeedInfo(SpeedInfo source)
        {
            if (source == null)
            {
                PassengerSpeed = -1;
                FreightSpeed = -1;
            }
            else
            {
                PassengerSpeed = source.PassengerSpeed;
                FreightSpeed = source.FreightSpeed;
                Flag = source.Flag;
                Reset = source.Reset;
                LimitedSpeedReduction = source.LimitedSpeedReduction;
            }
        }
    }

}
