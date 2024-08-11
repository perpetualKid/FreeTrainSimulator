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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.State;

namespace Orts.Simulation.Timetables
{
    /// <summary>
    /// Class AttachInfo : class for attach details
    /// </summary>
    public class AttachInfo : TimetableTrainInfo, ISaveStateApi<AttachInfoSaveState>
    {
        // StationPlatformReference - set to -1 if attaching to static train in dispose command
        public bool FirstIn { get; private set; }                       // this train arrives first
        public bool SetBack { get; private set; }                       // reverse in order to attach

        public bool ReadyToAttach { get; set; }                         // trains are ready to attach

        public AttachInfo() { }

        /// <summary>
        /// Constructor for attach details at station
        /// </summary>
        /// <param name="stationPlatformReference"></param>
        /// <param name="thisCommand"></param>
        /// <param name="thisTrain"></param>
        public AttachInfo(int stationPlatformReference, TTTrainCommands thisCommand, TTTrain thisTrain)
        {
            FirstIn = false;
            SetBack = false;
            ReadyToAttach = false;

            StationPlatformReference = stationPlatformReference;

            if (thisCommand.CommandValues == null || thisCommand.CommandValues.Count <= 0)
            {
                Trace.TraceInformation("Train {0} : missing train name in attach command", thisTrain.Name);
                Valid = false;
                return;
            }

            TrainName = thisCommand.CommandValues[0];
            if (!TrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
            {
                int seppos = thisTrain.Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                TrainName += $":{thisTrain.Name.Substring(seppos + 1)}";
            }

            if (thisCommand.CommandQualifiers != null)
            {
                foreach (TTTrainCommands.TTTrainComQualifiers thisQualifier in thisCommand.CommandQualifiers)
                {
                    switch (thisQualifier.QualifierName)
                    {
                        case "firstin":
                            if (StationPlatformReference < 0)
                            {
                                Trace.TraceWarning("Train {0} : dispose attach command : FirstIn not allowed for dispose command", thisTrain.Name);
                            }
                            else
                            {
                                FirstIn = true;
                            }
                            break;

                        case "setback":
                            if (StationPlatformReference < 0)
                            {
                                Trace.TraceWarning("Train {0} : dispose attach command : SetBack not allowed for dispose command", thisTrain.Name);
                            }
                            else
                            {
                                SetBack = true;
                                FirstIn = true;
                            }
                            break;

                        default:
                            Trace.TraceWarning("Train {0} : Invalid qualifier for attach command : {1}", thisTrain.Name, thisQualifier.QualifierName);
                            break;
                    }
                }

            }

            // straight forward attach in station without first in and no set back : set no need to store (attach can be set directly)
            if (!FirstIn && !SetBack && StationPlatformReference >= 0)
            {
                ReadyToAttach = true;
            }

            Valid = true;
        }

        /// <summary>
        /// Contructor for attach at dispose
        /// </summary>
        /// <param name="rrtrain"></param>
        public AttachInfo(TTTrain rrtrain)
        {
            TrainNumber = rrtrain.Number;
            TrainName = rrtrain.Name;
            StationPlatformReference = -1;
            FirstIn = false;
            SetBack = false;

            Valid = true;
            ReadyToAttach = true;
        }

        /// <summary>
        ///  Finalize attach details - if valid, work out cross reference informatio
        /// </summary>
        /// <param name="thisTrain"></param>
        /// <param name="trainList"></param>
        /// <param name="playerTrain"></param>
        public void FinalizeAttachDetails(TTTrain thisTrain, List<TTTrain> trainList, TTTrain playerTrain)
        {
            if (Valid)
            {
                // set Xref to train to which to attach
                SetAttachXRef(thisTrain, trainList, playerTrain);
            }
        }

        /// <summary>
        /// Set attach cross-reference train information
        /// </summary>
        /// <param name="dettrain"></param>
        /// <param name="trainList"></param>
        /// <param name="playerTrain"></param>
        public void SetAttachXRef(TTTrain dettrain, List<TTTrain> trainList, TTTrain playerTrain)
        {
            bool trainFound = false;
            TTTrain attachedTrain = null;

            foreach (TTTrain otherTrain in trainList)
            {
                if (string.Equals(otherTrain.Name, TrainName, StringComparison.OrdinalIgnoreCase))
                {
                    TrainNumber = otherTrain.OrgAINumber;
                    attachedTrain = otherTrain;
                    trainFound = true;
                    break;
                }
            }

            // if not found, try player train
            if (!trainFound)
            {
                if (playerTrain != null && string.Equals(playerTrain.Name, TrainName, StringComparison.OrdinalIgnoreCase))
                {
                    TrainNumber = playerTrain.OrgAINumber;
                    attachedTrain = playerTrain;
                    trainFound = true;
                }
            }

            // issue warning if train not found
            if (!trainFound)
            {
                Trace.TraceWarning("Train :  {0} : attach details : train {1} to attach to is not found",
                    dettrain.Name, TrainName);
            }

            // search for station stop if set
            else if (StationPlatformReference >= 0)
            {
                int stationIndex = -1;
                for (int iStationStop = 0; iStationStop < attachedTrain.StationStops.Count && stationIndex < 0; iStationStop++)
                {
                    if (attachedTrain.StationStops[iStationStop].PlatformReference == StationPlatformReference)
                    {
                        stationIndex = iStationStop;
                    }
                }

                if (stationIndex < 0)
                {
                    Trace.TraceWarning("Train {0} : attach details : station stop for train {1} not found", dettrain.Name, attachedTrain.Name);
                    trainFound = false;
                }
            }

            // if train is found, set need attach information
            if (trainFound)
            {
                // set need attach
                if (attachedTrain.NeedAttach.TryGetValue(StationPlatformReference, out List<int> needAttachList))
                {
                    needAttachList.Add(dettrain.OrgAINumber);
                }
                else
                {
                    needAttachList = [dettrain.OrgAINumber];
                    attachedTrain.NeedAttach.Add(StationPlatformReference, needAttachList);
                }
            }
            else
            // if not found, set attach to invalid (nothing to attach to)
            {
                Valid = false;
                Trace.TraceWarning("Train {0} : attach command to attach to train {1} : command invalid", dettrain.Name, TrainName);
            }
        }

        public ValueTask<AttachInfoSaveState> Snapshot()
        {
            return ValueTask.FromResult(new AttachInfoSaveState()
            {
                TrainNumber = TrainNumber,
                TrainName = TrainName,
                FirstToArrive = FirstIn,
                ReadyToAttach = ReadyToAttach,
                Reverse = SetBack,
                Valid = Valid,
                StationPlatformReference = StationPlatformReference,
            });
        }

        public ValueTask Restore(AttachInfoSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            TrainNumber = saveState.TrainNumber;
            TrainName = saveState.TrainName;
            StationPlatformReference = saveState.StationPlatformReference;
            FirstIn = saveState.FirstToArrive;
            SetBack = saveState.Reverse;
            Valid = saveState.Valid;
            ReadyToAttach = saveState.ReadyToAttach;

            return ValueTask.CompletedTask;
        }
    }
}

