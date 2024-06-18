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
using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Models.State;
using Orts.Simulation.Track;

namespace Orts.Simulation.Timetables
{
    /// <summary>
    /// Class for waiting instructions
    /// <\summary>
    public class WaitInfo : IComparable<WaitInfo>, ISaveStateApi<WaitInfoSaveState>
    {
        // General info
        public WaitInfoType WaitType;                         // type of wait instruction
        public bool WaitActive;                               // wait state is active

        // preprocessed info - info is removed after processing
        public int startSectionIndex;                         // section from which command is valid (-1 if valid from start)
        public int startSubrouteIndex;                        // subpath index from which command is valid
        public string referencedTrainName;                    // referenced train name (for Wait, Follow or Connect)

        // processed info
        public int activeSectionIndex;                        // index of TrackCircuitSection where wait must be activated
        public int activeSubrouteIndex;                       // subpath in which this wait is valid
        public int activeRouteIndex;                          // index of section in active subpath

        // common for Wait, Follow and Connect
        public int waitTrainNumber;                           // number of train for which to wait
        public int? maxDelayS;                         // max. delay for waiting (in seconds)
        public int? ownDelayS;                         // min. own delay for waiting to be active (in seconds)
        public bool? notStarted;                       // also wait if not yet started
        public bool? atStart;                          // wait at start of wait section, otherwise wait at first not-common section
        public int? waittrigger;                       // time at which wait is triggered
        public int? waitendtrigger;                    // time at which wait is cancelled

        // wait types Wait and Follow :
        public int waitTrainSubpathIndex;                     // subpath index for train - set to -1 if wait is always active
        public int waitTrainRouteIndex;                       // index of section in active subpath

        // wait types Connect :
        public int stationIndex;                              // index in this train station stop list
        public int? holdTimeS;                                // required hold time (in seconds)

        // wait types WaitInfo (no post-processing required) :
        public TrackCircuitPartialPathRoute CheckPath;         // required path to check in case of WaitAny

        public PathCheckDirection PathDirection = PathCheckDirection.Same; // required path direction

        public WaitInfo()
        {
        }

        /// <summary>
        /// Constructor for restore
        /// </summary>
        /// <param name="inf"></param>
        public WaitInfo(BinaryReader inf)
        {
            WaitType = (WaitInfoType)inf.ReadInt32();
            WaitActive = inf.ReadBoolean();

            activeSubrouteIndex = inf.ReadInt32();
            activeSectionIndex = inf.ReadInt32();
            activeRouteIndex = inf.ReadInt32();

            waitTrainNumber = inf.ReadInt32();
            int mdelayValue = inf.ReadInt32();
            if (mdelayValue < 0)
            {
                maxDelayS = null;
            }
            else
            {
                maxDelayS = mdelayValue;
            }

            int odelayValue = inf.ReadInt32();
            if (odelayValue < 0)
            {
                ownDelayS = null;
            }
            else
            {
                ownDelayS = odelayValue;
            }

            int notStartedValue = inf.ReadInt32();
            if (notStartedValue > 0)
            {
                notStarted = inf.ReadBoolean();
            }
            else
            {
                notStarted = null;
            }

            int atStartValue = inf.ReadInt32();
            if (atStartValue > 0)
            {
                atStart = inf.ReadBoolean();
            }
            else
            {
                atStart = null;
            }

            int triggervalue = inf.ReadInt32();
            if (triggervalue > 0)
            {
                waittrigger = triggervalue;
            }
            else
            {
                waittrigger = null;
            }

            int endtriggervalue = inf.ReadInt32();
            if (endtriggervalue > 0)
            {
                waitendtrigger = endtriggervalue;
            }
            else
            {
                waitendtrigger = null;
            }

            waitTrainSubpathIndex = inf.ReadInt32();
            waitTrainRouteIndex = inf.ReadInt32();

            stationIndex = inf.ReadInt32();
            int holdTimevalue = inf.ReadInt32();
            if (holdTimevalue < 0)
            {
                holdTimeS = null;
            }
            else
            {
                holdTimeS = holdTimevalue;
            }

            int validCheckPath = inf.ReadInt32();

            if (validCheckPath < 0)
            {
                CheckPath = null;
                PathDirection = PathCheckDirection.Same;
            }
            else
            {
                CheckPath = new TrackCircuitPartialPathRoute(inf);
                PathDirection = (PathCheckDirection)inf.ReadInt32();
            }
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
                _ => activeSubrouteIndex < other.activeSubrouteIndex || activeSubrouteIndex == other.activeSubrouteIndex && activeRouteIndex < other.activeRouteIndex
                ? -1
                : activeSubrouteIndex == other.activeSubrouteIndex && activeRouteIndex == other.activeRouteIndex ? 0 : 1
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
                TrainNumber = waitTrainNumber,
                ActiveWait = WaitActive,
                ActiveRouteIndex = activeRouteIndex,
                ActiveSubrouteIndex = activeSubrouteIndex,
                ActiveSectionIndex = activeSectionIndex,
                MaxDelay = maxDelayS,
                OwnDelay = ownDelayS,
                NotStarted = notStarted,
                AtStart = atStart,
                WaitTrigger = waittrigger,
                WaitEndTrigger = waitendtrigger,
                WaitingTrainRouteIndex = waitTrainRouteIndex,
                WaitingTrainSubpathIndex = waitTrainSubpathIndex,
                StationIndex = stationIndex,
                HoldTime = holdTimeS,
                CheckPath = CheckPath == null ? null : await CheckPath.Snapshot().ConfigureAwait(false),
                CheckDirection = PathDirection,
            };
        }

        public async ValueTask Restore(WaitInfoSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            WaitType = saveState.WaitInfoType;
            WaitActive = saveState.ActiveWait;

            activeRouteIndex = saveState.ActiveRouteIndex;
            activeSubrouteIndex = saveState.ActiveSubrouteIndex;
            activeSectionIndex = saveState.ActiveSectionIndex;

            waitTrainNumber = saveState.TrainNumber;
            maxDelayS = saveState.MaxDelay;
            ownDelayS = saveState.OwnDelay;
            notStarted = saveState.NotStarted;
            atStart = saveState.AtStart;
            waittrigger = saveState.WaitTrigger;
            waitendtrigger = saveState.WaitEndTrigger;

            waitTrainSubpathIndex = saveState.WaitingTrainSubpathIndex;
            waitTrainRouteIndex = saveState.WaitingTrainRouteIndex;

            stationIndex = saveState.StationIndex;
            holdTimeS = saveState.HoldTime;

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

