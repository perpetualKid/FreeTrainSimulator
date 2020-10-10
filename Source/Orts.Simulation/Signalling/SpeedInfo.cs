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
    //================================================================================================//
    /// <summary>
    ///
    /// class ObjectSpeedInfo
    ///
    /// </summary>
    //================================================================================================//
    public class SpeedInfo
    {

        public float PassengerSpeed { get; internal set; }
        public float FreightSpeed { get; internal set; }
        public bool Flag { get; internal set; }
        public bool Reset { get; internal set; }
        public int LimitedSpeedReduction { get; internal set; } // No Speed Reduction or is Temporary Speed Reduction

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public SpeedInfo(float pass, float freight, bool asap, bool reset, int nospeedreductionOristempspeedreduction)
        {
            PassengerSpeed = pass;
            FreightSpeed = freight;
            Flag = asap;
            Reset = reset;
            LimitedSpeedReduction = nospeedreductionOristempspeedreduction;
        }
    }

}
