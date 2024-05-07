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

using System.Collections.Generic;

using FreeTrainSimulator.Common;

using Orts.Common;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Track
{
    //================================================================================================//
    /// <summary>
    ///
    /// class TrackCircuitMilepostList
    /// Class for track circuit mile post lists
    ///
    /// </summary>
    //================================================================================================//
    internal class TrackCircuitMilepostList : List<TrackCircuitMilepost>
    {
    }

    //================================================================================================//
    /// <summary>
    ///
    /// class MilepostObject
    /// Class for track circuit mileposts
    ///
    /// </summary>
    //================================================================================================//
    internal class TrackCircuitMilepost
    {
        public Milepost Milepost { get; }                       // reference to milepost 
        public EnumArray<float, SignalLocation> MilepostLocation { get; } = new EnumArray<float, SignalLocation>();         // milepost location from both ends //

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public TrackCircuitMilepost(Milepost milepost, float nearEnd, float farEnd)
        {
            Milepost = milepost;
            MilepostLocation[SignalLocation.NearEnd] = nearEnd;
            MilepostLocation[SignalLocation.FarEnd] = farEnd; 
        }
    }

}
