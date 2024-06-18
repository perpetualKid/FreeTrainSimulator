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
using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using Orts.Models.State;

using SharpDX.Direct2D1;

namespace Orts.Simulation.Timetables
{
    /// <summary>
    /// Class for pick up information
    /// </summary>
    public class PickUpInfo : TimetableTrainInfo, ISaveStateApi<PickupSaveState>
    {
        private bool pickUpStatic;                         // pickup unnamed static consist

        // StationPlatformReference - set to -1 if attaching to static train in dispose command

        public PickUpInfo() { }
        /// <summary>
        /// Constructor for pick up at station
        /// </summary>
        /// <param name="stationPlatformReference"></param>
        /// <param name="thisCommand"></param>
        /// <param name="thisTrain"></param>
        public PickUpInfo(int stationPlatformReference, TTTrainCommands thisCommand, TTTrain thisTrain)
        {
            Valid = true;
            StationPlatformReference = stationPlatformReference;
            TrainNumber = -1;
            pickUpStatic = false;

            if (thisCommand.CommandValues != null && thisCommand.CommandValues.Count > 0)
            {
                TrainName = thisCommand.CommandValues[0];
                if (!TrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
                {
                    int seppos = thisTrain.Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                    TrainName += $":{thisTrain.Name[(seppos + 1)..]}";
                }
            }
            else if (thisCommand.CommandQualifiers != null && thisCommand.CommandQualifiers.Count > 0)
            {
                switch (thisCommand.CommandQualifiers[0].QualifierName)
                {
                    case "static":
                        pickUpStatic = true;
                        this.StationPlatformReference = stationPlatformReference;
                        break;

                    default:
                        Trace.TraceInformation("Train : {0} : unknown pickup qualifier : {1}", thisTrain.Name, thisCommand.CommandQualifiers[0].QualifierName);
                        break;
                }
            }
            else
            {
                Trace.TraceInformation("Train : {0} : pick-up command must include a train name or static qualifier", thisTrain.Name);
                Valid = false;
            }
        }


        public ValueTask<PickupSaveState> Snapshot()
        {
            return ValueTask.FromResult(new PickupSaveState()
            { 
                PickupStaticConsist = pickUpStatic,
                TrainNumber = TrainNumber,
                TrainName = TrainName,
                StationPlatformReference = StationPlatformReference,
                Valid = Valid,
            });
        }

        public ValueTask Restore(PickupSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            TrainName = saveState.TrainName;
            TrainNumber = saveState.TrainNumber;
            pickUpStatic = saveState.PickupStaticConsist;
            StationPlatformReference = saveState.StationPlatformReference;
            Valid = saveState.Valid;

            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Finalize pickup details : set cross-reference information and check validity
        /// </summary>
        /// <param name="thisTrain"></param>
        /// <param name="trainList"></param>
        /// <param name="playerTrain"></param>
        public void FinalizePickUpDetails(TTTrain thisTrain, List<TTTrain> trainList, TTTrain playerTrain)
        {
            // set Xref to train to which to attach
            SetPickUpXRef(thisTrain, trainList, playerTrain);

            // sort out information per train or per location
            foreach (PickUpInfo thisPickUp in thisTrain.PickUpDetails)
            {
                thisTrain.PickUpStaticOnForms = false;

                if (thisPickUp.Valid)
                {
                    if (thisPickUp.pickUpStatic)
                    {
                        if (thisPickUp.StationPlatformReference >= 0)
                        {
                            if (thisTrain.PickUpStatic.Contains(thisPickUp.StationPlatformReference))
                            {
                                Trace.TraceInformation("Train {0} : multiple PickUp definition for same location : {1}", thisTrain.Name, thisPickUp.StationPlatformReference);
                            }
                            else
                            {
                                thisTrain.PickUpStatic.Add(thisPickUp.StationPlatformReference);
                            }
                        }
                        else
                        {
                            thisTrain.PickUpStaticOnForms = true;
                        }
                    }
                    else
                    {
                        if (thisTrain.PickUpTrains.Contains(thisPickUp.TrainNumber))
                        {
                            Trace.TraceInformation("Train {0} : multiple PickUp definition for same train : {1}", thisTrain.Name, thisPickUp.TrainName);
                        }
                        else
                        {
                            thisTrain.PickUpTrains.Add(thisPickUp.TrainNumber);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set pickup cross-reference train information
        /// </summary>
        /// <param name="dettrain"></param>
        /// <param name="trainList"></param>
        /// <param name="playerTrain"></param>
        public void SetPickUpXRef(TTTrain dettrain, List<TTTrain> trainList, TTTrain playerTrain)
        {
            bool trainFound = false;
            TTTrain pickUpTrain = null;

            if (!pickUpStatic)
            {
                foreach (TTTrain otherTrain in trainList)
                {
                    if (string.Equals(otherTrain.Name, TrainName, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TrainNumber = otherTrain.OrgAINumber;
                        pickUpTrain = otherTrain;
                        trainFound = true;
                        break;
                    }
                }

                // if not found, try player train
                if (!trainFound)
                {
                    if (playerTrain != null && string.Equals(playerTrain.Name, TrainName, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TrainNumber = playerTrain.OrgAINumber;
                        pickUpTrain = playerTrain;
                        trainFound = true;
                    }
                }

                // issue warning if train not found
                if (!trainFound)
                {
                    Trace.TraceWarning("Train :  {0} : pickup details : train {1} to pick up is not found",
                        dettrain.Name, TrainName);
                    Valid = false;
                    Trace.TraceWarning("Train {0} : pickup command to pick up train {1} : command invalid", dettrain.Name, TrainName);
                }
            }
        }
    }
}

