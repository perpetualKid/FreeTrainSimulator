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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.Imported.State;

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

    internal class DeadlockPathInfo : ISaveStateApi<DeadlockPathInfoSaveState>
    {
        public string Name { get; internal set; }
        public TrackCircuitPartialPathRoute Path { get; private set; }
        public List<string> Groups { get; private set; }// groups of which this path is a part
        public List<int> AllowedTrains { get; private set; }// list of train for which path is valid (ref. is train/subpath index); -1 indicates public path
        public int LastUsefulSectionIndex { get; internal set; }// Index in Path for last section which can be used before stop position
        public float UsefulLength { get; internal set; } // path useful length
        public int EndSectionIndex { get; internal set; }// index of linked end section

        public DeadlockPathInfo() { }

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

        public async ValueTask<DeadlockPathInfoSaveState> Snapshot()
        {
            return new DeadlockPathInfoSaveState() 
            { 
                PathInfo = await Path.Snapshot().ConfigureAwait(false),
                Name = Name,
                Groups = new Collection<string>(Groups),
                UsableLength = UsefulLength,
                EndSectionIndex = EndSectionIndex,
                LastUsableSectionIndex = LastUsefulSectionIndex,
                AllowedTrains = new Collection<int>(AllowedTrains),
            };
        }

        public async ValueTask Restore(DeadlockPathInfoSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));
            Path = new TrackCircuitPartialPathRoute();
            await Path.Restore(saveState.PathInfo).ConfigureAwait(false);
            Name = saveState.Name;
            Groups = saveState.Groups.ToList();
            UsefulLength = saveState.UsableLength;
            EndSectionIndex = saveState.EndSectionIndex;
            LastUsefulSectionIndex = saveState.LastUsableSectionIndex;
            AllowedTrains = saveState.AllowedTrains.ToList();
        }
    }

}
