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

using Orts.Common;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Track
{
    //================================================================================================//
    /// <summary>
    ///
    /// class TrackCircuitSignalList
    /// Class for track circuit signal list
    ///
    /// </summary>
    //================================================================================================//
    internal class TrackCircuitSignalList : List<TrackCircuitSignalItem>
    {
    }

    //================================================================================================//
    /// <summary>
    ///
    /// class TrackCircuitSignalItem
    /// Class for track circuit signal item
    ///
    /// </summary>
    //================================================================================================//
    internal class TrackCircuitSignalItem
    {
        public SignalItemFindState SignalState { get; internal set; }  // returned state // 
        public Signal Signal { get; internal set; }            // related SignalObject     //
        public float SignalLocation { get; internal set; }              // relative signal position //


        //================================================================================================//
        /// <summary>
        /// Constructor setting object
        /// </summary>

        public TrackCircuitSignalItem(Signal signal, float location)
        {
            SignalState = SignalItemFindState.Item;
            Signal = signal;
            SignalLocation = location;
        }


        //================================================================================================//
        /// <summary>
        /// Constructor setting state
        /// </summary>

        public TrackCircuitSignalItem(SignalItemFindState state)
        {
            SignalState = state;
        }
    }

}
