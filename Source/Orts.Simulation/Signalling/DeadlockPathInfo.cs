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
using System.IO;

using Orts.Simulation.Physics;
using Orts.Simulation.Track;

namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    ///
    /// DeadlockPath Info Object
    ///
    /// </summary>
    //================================================================================================//

    internal class DeadlockPathInfo
    {
        public string Name { get; internal set; }
        public TrackCircuitPartialPathRoute Path { get; }
        public List<string> Groups { get; }// groups of which this path is a part
        public List<int> AllowedTrains { get; }// list of train for which path is valid (ref. is train/subpath index); -1 indicates public path
        public int LastUsefulSectionIndex { get; internal set; }// Index in Path for last section which can be used before stop position
        public float UsefulLength { get; internal set; } // path useful length
        public int EndSectionIndex { get; internal set; }// index of linked end section

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public DeadlockPathInfo(TrackCircuitPartialPathRoute path, int pathIndex)
        {
            Path = new TrackCircuitPartialPathRoute(path);
            Name = string.Empty;
            Groups = new List<string>();

            UsefulLength = 0.0f;
            EndSectionIndex = -1;
            LastUsefulSectionIndex = -1;
            AllowedTrains = new List<int>();

            Path[0].UsedAlternativePath = pathIndex;
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for restore
        /// </summary>

        public DeadlockPathInfo(BinaryReader inf)
        {
            ArgumentNullException.ThrowIfNull(inf);

            Path = new TrackCircuitPartialPathRoute(inf);
            Name = inf.ReadString();

            Groups = new List<string>();
            int totalGroups = inf.ReadInt32();
            for (int iGroup = 0; iGroup <= totalGroups - 1; iGroup++)
            {
                string thisGroup = inf.ReadString();
                Groups.Add(thisGroup);
            }

            UsefulLength = inf.ReadSingle();
            EndSectionIndex = inf.ReadInt32();
            LastUsefulSectionIndex = inf.ReadInt32();

            AllowedTrains = new List<int>();
            int totalIndex = inf.ReadInt32();
            for (int iIndex = 0; iIndex <= totalIndex - 1; iIndex++)
            {
                int thisIndex = inf.ReadInt32();
                AllowedTrains.Add(thisIndex);
            }
        }

        //================================================================================================//
        /// <summary>
        /// save
        /// </summary>

        public void Save(BinaryWriter outf)
        {
            ArgumentNullException.ThrowIfNull(outf);

            Path.Save(outf);
            outf.Write(Name);

            outf.Write(Groups.Count);
            foreach (string groupName in Groups)
            {
                outf.Write(groupName);
            }

            outf.Write(UsefulLength);
            outf.Write(EndSectionIndex);
            outf.Write(LastUsefulSectionIndex);

            outf.Write(AllowedTrains.Count);
            foreach (int thisIndex in AllowedTrains)
            {
                outf.Write(thisIndex);
            }
        }
    }

}
