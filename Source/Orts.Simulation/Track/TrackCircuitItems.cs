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

namespace Orts.Simulation.Track
{
    //================================================================================================//
    /// <summary>
    ///
    /// class TrackCircuitItems
    /// Class for track circuit item storage
    ///
    /// </summary>
    //================================================================================================//

    internal class TrackCircuitItems
    {
        // List of signals (per direction and per type) //
        public EnumArray<List<TrackCircuitSignalList>, TrackDirection> TrackCircuitSignals { get; } = new EnumArray<List<TrackCircuitSignalList>, TrackDirection>();
        // List of speedposts (per direction) //
        public EnumArray<TrackCircuitSignalList, TrackDirection> TrackCircuitSpeedPosts { get; } = new EnumArray<TrackCircuitSignalList, TrackDirection>();
        // List of mileposts //
        internal TrackCircuitMilepostList TrackCircuitMileposts { get; } = new TrackCircuitMilepostList();

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public TrackCircuitItems(int orSignalTypes)
        {
            foreach (TrackDirection heading in EnumExtension.GetValues<TrackDirection>())
            {
                List<TrackCircuitSignalList> trackSignalLists = new List<TrackCircuitSignalList>();
                for (int fntype = 0; fntype < orSignalTypes; fntype++)
                {
                    trackSignalLists.Add(new TrackCircuitSignalList());
                }
                TrackCircuitSignals[heading] = trackSignalLists;

                TrackCircuitSpeedPosts[heading] = new TrackCircuitSignalList(); ;
            }
        }
    }

}
