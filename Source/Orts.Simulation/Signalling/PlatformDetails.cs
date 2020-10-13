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

// Debug flags :
// #define DEBUG_PRINT
// prints details of the derived signal structure
// #define DEBUG_REPORTS
// print details of train behaviour
// #define DEBUG_DEADLOCK
// print details of deadlock processing

using System;
using System.Collections.Generic;

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

        public List<int> TCSectionIndex = new List<int>();
        public int[] PlatformReference = new int[2];
        public float[,] TCOffset = new float[2, 2];
        public float[] nodeOffset = new float[2];
        public float Length { get; set; }
        public int[] EndSignals = new int[2] { -1, -1 };
        public float[] DistanceToSignals = new float[2];
        public string Name { get; internal set; }
        public uint MinWaitingTime { get; internal set; }
        public int NumPassengersWaiting { get; internal set; }
        public PlatformSides PlatformSide { get; internal set; }
        public int PlatformFrontUiD { get; internal set; } = -1;


        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public PlatformDetails(int platformReference)
        {
            PlatformReference[0] = platformReference;
        }
    }

}
