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

/* AI
 * 
 * Contains code to initialize and control AI trains.
 * 
 */

using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.State;

using Orts.Simulation.Track;

namespace Orts.Simulation.Timetables
{
    /// <summary>
    /// Class for waiting instructions
    /// <\summary>
    public class WaitInfo : IComparable<WaitInfo>, ISaveStateApi<WaitInfoSaveState>
    {
        // General info
        public WaitInfoType WaitType { get; set; }                         // type of wait instruction
        public bool WaitActive { get; set; }                               // wait state is active

        // preprocessed info - info is removed after processing
        public int StartSectionIndex { get; set; }                         // section from which command is valid (-1 if valid from start)
        public int StartSubrouteIndex { get; set; }                        // subpath index from which command is valid
        public string ReferencedTrainName { get; set; }                    // referenced train name (for Wait, Follow or Connect)

        // processed info
        public int ActiveSectionIndex { get; set; }                        // index of TrackCircuitSection where wait must be activated
        public int ActiveSubrouteIndex { get; set; }                       // subpath in which this wait is valid
        public int ActiveRouteIndex { get; set; }                          // index of section in active subpath

        // common for Wait, Follow and Connect
        public int WaitTrainNumber { get; set; }                           // number of train for which to wait
        public int? MaxDelayS { get; set; }                         // max. delay for waiting (in seconds)
        public int? OwnDelayS { get; set; }                         // min. own delay for waiting to be active (in seconds)
        public bool? NotStarted { get; set; }                       // also wait if not yet started
        public bool? AtStart { get; set; }                          // wait at start of wait section, otherwise wait at first not-common section
        public int? Waittrigger { get; set; }                       // time at which wait is triggered
        public int? Waitendtrigger { get; set; }                    // time at which wait is cancelled

        // wait types Wait and Follow :
        public int WaitTrainSubpathIndex { get; set; }                     // subpath index for train - set to -1 if wait is always active
        public int WaitTrainRouteIndex { get; set; }                       // index of section in active subpath

        // wait types Connect :
        public int StationIndex { get; set; }                              // index in this train station stop list
        public int? HoldTimeS { get; set; }                                // required hold time (in seconds)

        // wait types WaitInfo (no post-processing required) :
        public TrackCircuitPartialPathRoute CheckPath { get; set; }         // required path to check in case of WaitAny

        public PathCheckDirection PathDirection { get; set; } = PathCheckDirection.Same; // required path direction

        public WaitInfo()
        {
        }

        //
        // Compare To (to allow sort)
        //
        public int CompareTo(WaitInfo other)
        {
            // all connects are moved to the end of the queue
            return WaitType == WaitInfoType.Connect
                ? other.WaitType == WaitInfoType.Connect ? 0 : 1
                : other.WaitType switch
            {
                WaitInfoType.Connect => -1,
                _ => ActiveSubrouteIndex < other.ActiveSubrouteIndex || ActiveSubrouteIndex == other.ActiveSubrouteIndex && ActiveRouteIndex < other.ActiveRouteIndex
                ? -1
                : ActiveSubrouteIndex == other.ActiveSubrouteIndex && ActiveRouteIndex == other.ActiveRouteIndex ? 0 : 1
            };
        }

        /// <summary>
        /// Create full copy
        /// </summary>
        /// <returns></returns>
        public WaitInfo CreateCopy()
        {
            return (WaitInfo)MemberwiseClone();
        }

        public async ValueTask<WaitInfoSaveState> Snapshot()
        {
            return new WaitInfoSaveState()
            {
                WaitInfoType = WaitType,
                TrainNumber = WaitTrainNumber,
                ActiveWait = WaitActive,
                ActiveRouteIndex = ActiveRouteIndex,
                ActiveSubrouteIndex = ActiveSubrouteIndex,
                ActiveSectionIndex = ActiveSectionIndex,
                MaxDelay = MaxDelayS,
                OwnDelay = OwnDelayS,
                NotStarted = NotStarted,
                AtStart = AtStart,
                WaitTrigger = Waittrigger,
                WaitEndTrigger = Waitendtrigger,
                WaitingTrainRouteIndex = WaitTrainRouteIndex,
                WaitingTrainSubpathIndex = WaitTrainSubpathIndex,
                StationIndex = StationIndex,
                HoldTime = HoldTimeS,
                CheckPath = CheckPath == null ? null : await CheckPath.Snapshot().ConfigureAwait(false),
                CheckDirection = PathDirection,
            };
        }

        public async ValueTask Restore(WaitInfoSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            WaitType = saveState.WaitInfoType;
            WaitActive = saveState.ActiveWait;

            ActiveRouteIndex = saveState.ActiveRouteIndex;
            ActiveSubrouteIndex = saveState.ActiveSubrouteIndex;
            ActiveSectionIndex = saveState.ActiveSectionIndex;

            WaitTrainNumber = saveState.TrainNumber;
            MaxDelayS = saveState.MaxDelay;
            OwnDelayS = saveState.OwnDelay;
            NotStarted = saveState.NotStarted;
            AtStart = saveState.AtStart;
            Waittrigger = saveState.WaitTrigger;
            Waitendtrigger = saveState.WaitEndTrigger;

            WaitTrainSubpathIndex = saveState.WaitingTrainSubpathIndex;
            WaitTrainRouteIndex = saveState.WaitingTrainRouteIndex;

            StationIndex = saveState.StationIndex;
            HoldTimeS = saveState.HoldTime;

            if (saveState.CheckPath == null)
            {
                CheckPath = null;
                PathDirection = PathCheckDirection.Same;
            }
            else
            {
                CheckPath = new TrackCircuitPartialPathRoute();
                await CheckPath.Restore(saveState.CheckPath).ConfigureAwait(false);
                PathDirection = saveState.CheckDirection;
            }
        }
    }
}

