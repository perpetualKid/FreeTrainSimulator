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

namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    ///
    /// class TrackCircuitItems
    /// Class for track circuit item storage
    ///
    /// </summary>
    //================================================================================================//

    public class TrackCircuitItems
    {
        public EnumArray<List<TrackCircuitSignalList>, Heading> TrackCircuitSignals { get; } = new EnumArray<List<TrackCircuitSignalList>, Heading>();
        // List of signals (per direction and per type) //
        public EnumArray<TrackCircuitSignalList, Heading> TrackCircuitSpeedPosts { get; } = new EnumArray<TrackCircuitSignalList, Heading>();
        // List of speedposts (per direction) //
        public TrackCircuitMilepostList TrackCircuitMileposts { get; } = new TrackCircuitMilepostList();
        // List of mileposts //

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public TrackCircuitItems(int orSignalTypes)
        {
            foreach (Heading heading in EnumExtension.GetValues<Heading>())
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
