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

    public class ObjectSpeedInfo
    {

        public float SpeedPass;
        public float SpeedFreight;
        public bool Flag { get; set; }
        public bool Reset { get; set; }
        public int SpeedNoSpeedReductionOrIsTempSpeedReduction;

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public ObjectSpeedInfo(float pass, float freight, bool asap, bool reset, int nospeedreductionOristempspeedreduction)
        {
            SpeedPass = pass;
            SpeedFreight = freight;
            Flag = asap;
            Reset = reset;
            SpeedNoSpeedReductionOrIsTempSpeedReduction = nospeedreductionOristempspeedreduction;
        }
    }

}
