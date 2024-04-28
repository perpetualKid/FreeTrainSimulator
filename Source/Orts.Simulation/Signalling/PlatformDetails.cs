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

using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common;

using Orts.Common;

namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    ///
    /// Class Platform Details
    ///
    /// </summary>
    //================================================================================================//

    public class PlatformDetails
    {
        [Flags]
        public enum PlatformSides
        {
            None = 0x0,
            Left = 0x1,
            Right = 0x2,
        }

        public List<int> TCSectionIndex { get; } = new List<int>();
        public EnumArray<int, Location> PlatformReference { get; } = new EnumArray<int, Location>();
        public EnumArray2D<float, Location, TrackDirection> TrackCircuitOffset { get; } = new EnumArray2D<float, Location, TrackDirection>();
        public EnumArray<float, Location> NodeOffset { get; } = new EnumArray<float, Location>();
        public float Length { get; set; }
        public EnumArray<int, TrackDirection> EndSignals { get; } = new EnumArray<int, TrackDirection>(-1);
        public EnumArray<float, TrackDirection> DistanceToSignals { get; } = new EnumArray<float, TrackDirection>();
        public string Name { get; internal set; }
        public int MinWaitingTime { get; internal set; }
        public int NumPassengersWaiting { get; internal set; }
        public PlatformSides PlatformSide { get; internal set; }
        public int PlatformFrontUiD { get; internal set; } = -1;


        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public PlatformDetails(int platformReference)
        {
            PlatformReference[Location.NearEnd] = platformReference;
        }
    }

}
