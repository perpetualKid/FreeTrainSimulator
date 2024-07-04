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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Models.State;
using Orts.Simulation.Track;

namespace Orts.Simulation.Timetables
{
    /// <summary>
    /// Class for transfer details
    /// </summary>
    public class TransferInfo : TimetableTrainInfo, ISaveStateApi<TransferInfoSaveState>
    {
        private TransferType transferType;                     // type of transfer
        private TransferUnits transferUnitsInfo;          // type of unit definition
        private int transferUnitCount;                           // no. of units (if defined as units)
        private List<string> transferConsist;             // consists to transfer (if defined as consist)

        public TransferInfo() { }

        /// <summary>
        /// Constructor for new transfer details
        /// </summary>
        /// <param name="stationPlatformReference"></param>
        /// <param name="command"></param>
        /// <param name="train"></param>
        public TransferInfo(int stationPlatformReference, TTTrainCommands command, TTTrain train)
        {
            bool trainDefined = true;
            bool typeDefined = false;
            bool portionDefined = false;

            // set station platform reference
            StationPlatformReference = stationPlatformReference;

            // set transfer train name
            if (command.CommandValues?.Count > 0)
            {
                TrainName = command.CommandValues[0];
                if (!TrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
                {
                    int seppos = train.Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                    TrainName = string.Concat(TrainName, ":", train.Name[(seppos + 1)..]);
                }
            }
            else if (command.CommandQualifiers != null && command.CommandQualifiers.Count > 0)
            {
                switch (command.CommandQualifiers[0].QualifierName)
                {
                    // static is allowed, will be inserted with key -99
                    case "static":
                        TrainNumber = -99;
                        break;

                    // other qualifiers processed below
                    default:
                        break;
                }
            }
            else
            {
                Trace.TraceInformation("Train {0} : transfer command : missing other train name in transfer command", train.Name);
                trainDefined = false;
            }

            // transfer unit type
            if (command.CommandQualifiers == null || command.CommandQualifiers.Count <= 0)
            {
                Trace.TraceInformation("Train {0} : transfer command : missing transfer type", train.Name);
            }
            else
            {
                foreach (TTTrainCommands.TTTrainComQualifiers Qualifier in command.CommandQualifiers)
                {
                    switch (Qualifier.QualifierName)
                    {
                        // transfer type qualifiers
                        case string tranferType when tranferType.Equals("give", StringComparison.OrdinalIgnoreCase):
                            transferType = TransferType.Give;
                            typeDefined = true;
                            break;

                        case string tranferType when tranferType.Equals("take", StringComparison.OrdinalIgnoreCase):
                            transferType = TransferType.Take;
                            typeDefined = true;
                            break;

                        case string tranferType when tranferType.Equals("keep", StringComparison.OrdinalIgnoreCase):
                            transferType = TransferType.Keep;
                            typeDefined = true;
                            break;

                        case string tranferType when tranferType.Equals("leave", StringComparison.OrdinalIgnoreCase):
                            transferType = TransferType.Leave;
                            typeDefined = true;
                            break;

                        // transfer info qualifiers
                        case string tranferType when tranferType.Equals("onepower", StringComparison.OrdinalIgnoreCase):
                            transferUnitsInfo = TransferUnits.LeadingPower;
                            portionDefined = true;
                            break;

                        case string tranferType when tranferType.Equals("allpower", StringComparison.OrdinalIgnoreCase):
                            transferUnitsInfo = TransferUnits.AllLeadingPower;
                            portionDefined = true;
                            break;

                        case string tranferType when tranferType.Equals("nonpower", StringComparison.OrdinalIgnoreCase):
                            transferUnitsInfo = TransferUnits.NonPower;
                            portionDefined = true;
                            break;

                        case string tranferType when tranferType.Equals("units", StringComparison.OrdinalIgnoreCase):
                            if (int.TryParse(Qualifier.QualifierValues[0], out int nounits))
                            {
                                if (nounits > 0)
                                {
                                    transferUnitsInfo = TransferUnits.UnitsAtFront;
                                    transferUnitCount = nounits;
                                    portionDefined = true;
                                }
                                else
                                {
                                    Trace.TraceInformation("Train {0} : transfer command : invalid definition for units to transfer : {1}", train.Name, Qualifier.QualifierValues[0]);
                                }
                            }
                            else
                            {
                                Trace.TraceInformation("Train {0} : invalid value for units qualifier in transfer command : {1}", train.Name, Qualifier.QualifierValues[0]);
                            }
                            break;

                        case string tranferType when tranferType.Equals("consist", StringComparison.OrdinalIgnoreCase):
                            transferUnitsInfo = TransferUnits.Consists;

                            if (transferConsist == null)
                                transferConsist = new List<string>();
                            foreach (string consistname in Qualifier.QualifierValues)
                            {
                                transferConsist.Add(consistname);
                            }
                            portionDefined = true;
                            break;

                        // static is allready processed, so skip
                        case string tranferType when tranferType.Equals("static", StringComparison.OrdinalIgnoreCase):
                            break;

                        default:
                            Trace.TraceInformation($"Train {train.Name} : transfer command : invalid qualifier : {Qualifier.QualifierName}");
                            break;
                    }
                }
            }
            if (!typeDefined)
            {
                Trace.TraceInformation("Train {0} : transfer command : no valid transfer type defined", train.Name);
            }
            else if (!portionDefined)
            {
                Trace.TraceInformation("Train {0} : transfer command : no valid transfer portion defined", train.Name);
            }
            else if (trainDefined)
            {
                Valid = true;
            }
            else
            {
                Trace.TraceInformation("Train {0} : transfer command : invalid transfer command, command ignored", train.Name);
            }
        }


        public ValueTask<TransferInfoSaveState> Snapshot()
        {
            return ValueTask.FromResult(new TransferInfoSaveState()
            {
                TransferType = transferType,
                TransferUnits = transferUnitsInfo,
                TransferUnitsCount = transferUnitCount,
                TransferConsists = transferConsist == null ? null : new Collection<string>(transferConsist),
                TrainNumber = TrainNumber,
                TrainName = TrainName,
                StationPlatformReference = StationPlatformReference,
                Valid = Valid,
            });
        }

        public ValueTask Restore(TransferInfoSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            transferType = saveState.TransferType;
            transferUnitsInfo = saveState.TransferUnits;
            transferUnitCount = saveState.TransferUnitsCount;

            if (saveState.TransferConsists != null)
            {
                transferConsist = new List<string>(saveState.TransferConsists);
            }

            TrainNumber = saveState.TrainNumber;
            TrainName = saveState.TrainName;

            StationPlatformReference = saveState.StationPlatformReference;
            Valid = saveState.Valid;

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Perform transfer
        /// </summary>
        /// <param name="otherTrain"></param>
        /// <param name="otherTrainFront"></param>
        /// <param name="train"></param>
        /// <param name="trainFront"></param>
        public void PerformTransfer(TTTrain otherTrain, bool otherTrainFront, TTTrain train, bool trainFront)
        {
            TTTrain givingTrain = null;
            TTTrain takingTrain = null;

            bool giveTrainFront = false;
            bool takeTrainFront = false;

            // stop train
            train.SpeedMpS = 0;
            foreach (var car in train.Cars)
            {
                car.SpeedMpS = 0;
            }

            if (train.TrainType != TrainType.Player)
                train.AdjustControlsThrottleOff();
            train.PhysicsUpdate(0);

            // sort out what to detach
            int iunits = 0;
            bool frontpos = true;
            TransferUnits thisInfo;
            bool reverseUnits = false;

            switch (transferType)
            {
                case TransferType.Give:
                    givingTrain = train;
                    giveTrainFront = trainFront;

                    takingTrain = otherTrain;
                    takeTrainFront = otherTrainFront;

                    iunits = givingTrain.GetUnitsToDetach(transferUnitsInfo, transferUnitCount, transferConsist, ref frontpos);
                    break;

                case TransferType.Take:
                    givingTrain = otherTrain;
                    giveTrainFront = otherTrainFront;
                    takingTrain = train;
                    takeTrainFront = trainFront;

                    if (giveTrainFront)
                    {
                        iunits = givingTrain.GetUnitsToDetach(transferUnitsInfo, transferUnitCount, transferConsist, ref frontpos);
                    }
                    else
                    {
                        thisInfo = transferUnitsInfo;
                        switch (thisInfo)
                        {
                            case TransferUnits.LeadingPower:
                                transferUnitsInfo = TransferUnits.TrailingPower;
                                break;

                            case TransferUnits.AllLeadingPower:
                                transferUnitsInfo = TransferUnits.AllTrailingPower;
                                break;

                            case TransferUnits.NonPower:
                                transferUnitsInfo = TransferUnits.AllLeadingPower;
                                reverseUnits = true;
                                break;

                            case TransferUnits.UnitsAtFront:
                                transferUnitsInfo = TransferUnits.UnitsAtEnd;
                                break;

                            // other definitions : no change
                            default:
                                break;
                        }
                        iunits = givingTrain.GetUnitsToDetach(transferUnitsInfo, transferUnitCount, transferConsist, ref frontpos);

                        if (reverseUnits)
                        {
                            iunits = givingTrain.Cars.Count - iunits;
                            frontpos = !frontpos;
                        }
                    }
                    break;

                case TransferType.Leave:
                    givingTrain = otherTrain;
                    giveTrainFront = otherTrainFront;
                    takingTrain = train;
                    takeTrainFront = trainFront;

                    if (giveTrainFront)
                    {
                        thisInfo = transferUnitsInfo;
                        switch (thisInfo)
                        {
                            case TransferUnits.LeadingPower:
                                transferUnitsInfo = TransferUnits.TrailingPower;
                                reverseUnits = true;
                                break;

                            case TransferUnits.AllLeadingPower:
                                transferUnitsInfo = TransferUnits.AllTrailingPower;
                                reverseUnits = true;
                                break;

                            case TransferUnits.NonPower:
                                transferUnitsInfo = TransferUnits.AllLeadingPower;
                                break;

                            case TransferUnits.UnitsAtFront:
                                transferUnitsInfo = TransferUnits.UnitsAtEnd;
                                reverseUnits = true;
                                break;

                            case TransferUnits.Consists:
                                reverseUnits = true;
                                break;
                        }
                        iunits = givingTrain.GetUnitsToDetach(transferUnitsInfo, transferUnitCount, transferConsist, ref frontpos);

                        if (reverseUnits)
                        {
                            iunits = givingTrain.Cars.Count - iunits;
                            frontpos = !frontpos;
                        }
                    }
                    else
                    {
                        thisInfo = transferUnitsInfo;
                        switch (thisInfo)
                        {
                            case TransferUnits.LeadingPower:
                                reverseUnits = true;
                                break;

                            case TransferUnits.AllLeadingPower:
                                reverseUnits = true;
                                break;

                            case TransferUnits.NonPower:
                                transferUnitsInfo = TransferUnits.AllTrailingPower;
                                break;

                            case TransferUnits.UnitsAtFront:
                                reverseUnits = true;
                                break;

                            case TransferUnits.Consists:
                                reverseUnits = true;
                                break;
                        }
                        iunits = givingTrain.GetUnitsToDetach(transferUnitsInfo, transferUnitCount, transferConsist, ref frontpos);

                        if (reverseUnits)
                        {
                            iunits = givingTrain.Cars.Count - iunits;
                            frontpos = !frontpos;
                        }
                    }
                    break;

                case TransferType.Keep:
                    givingTrain = train;
                    giveTrainFront = trainFront;
                    takingTrain = otherTrain;
                    takeTrainFront = otherTrainFront;

                    thisInfo = transferUnitsInfo;
                    switch (thisInfo)
                    {
                        case TransferUnits.LeadingPower:
                            transferUnitsInfo = TransferUnits.TrailingPower;
                            reverseUnits = true;
                            break;

                        case TransferUnits.AllLeadingPower:
                            transferUnitsInfo = TransferUnits.AllTrailingPower;
                            reverseUnits = true;
                            break;

                        case TransferUnits.NonPower:
                            transferUnitsInfo = TransferUnits.AllLeadingPower;
                            break;

                        case TransferUnits.UnitsAtFront:
                            transferUnitsInfo = TransferUnits.UnitsAtEnd;
                            reverseUnits = true;
                            break;

                        case TransferUnits.Consists:
                            reverseUnits = true;
                            break;
                    }

                    iunits = givingTrain.GetUnitsToDetach(transferUnitsInfo, transferUnitCount, transferConsist, ref frontpos);

                    if (reverseUnits)
                    {
                        iunits = givingTrain.Cars.Count - iunits;
                        frontpos = !frontpos;
                    }
                    break;

            }

            if (iunits == 0)
            {
                Trace.TraceInformation("Train {0} : transfer command : no units to transfer from train {0} to train {1}", train.Name, givingTrain.Name, takingTrain.Name);
            }
            else if (iunits == givingTrain.Cars.Count)
            {
                Trace.TraceInformation("Train {0} : transfer command : transfer requires all units of train {0} to transfer to train {1}", train.Name, givingTrain.Name, takingTrain.Name);
                if (train.OrgAINumber == givingTrain.OrgAINumber)
                {
                    train.TTCouple(takingTrain, true, frontpos);
                }
                else
                {
                    givingTrain.TTCouple(train, frontpos, true);
                }
            }
            else
            {
                // create temp train which will hold transfered units
                List<TTTrain> tempList = new List<TTTrain>();
                string tempName = $"T_{train.OrgAINumber:0000}";
                int formedTrainNo = train.CreateStaticTrain(train, ref tempList, tempName, train.PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
                TTTrain tempTrain = tempList[0];

                // add new train (stored in tempList) to AutoGenTrains
                train.AI.AutoGenTrains.Add(tempList[0]);
                Simulator.Instance.AutoGenDictionary.Add(formedTrainNo, tempList[0]);

                // if detached at rear set negative no of units
                if (!frontpos)
                    iunits = -iunits;

                // create detach command
                DetachInfo thisDetach = new DetachInfo(DetachPositionInfo.Start, -1, TransferUnits.UnitsAtFront, iunits, null, formedTrainNo, false);

                // perform detach on giving train
                thisDetach.PerformDetach(givingTrain, false);

                // attach temp train to taking train
                tempTrain.TTCouple(takingTrain, giveTrainFront, takeTrainFront);

                // remove train from need transfer list
                if (StationPlatformReference >= 0)
                {
                    if (otherTrain.NeedStationTransfer.TryGetValue(StationPlatformReference, out List<int> needTransferList))
                    {
                        needTransferList.Remove(train.Number);

                        // remove list if empty
                        if (tempList.Count < 1)
                        {
                            otherTrain.NeedStationTransfer.Remove(StationPlatformReference);
                        }
                    }
                }
                else if (TrainNumber == otherTrain.OrgAINumber)
                {
                    // get last section as reference to transfer position
                    int lastSectionIndex = train.TCRoute.TCRouteSubpaths.Last().Last().TrackCircuitSection.Index;
                    if (otherTrain.NeedTrainTransfer.TryGetValue(lastSectionIndex, out int transferCount))
                    {
                        transferCount--;
                        otherTrain.NeedTrainTransfer.Remove(lastSectionIndex);

                        if (transferCount > 0)
                        {
                            otherTrain.NeedTrainTransfer.Add(lastSectionIndex, transferCount);
                        }
                    }
                }
            }

            // if transfer was part of dispose command, curtail train route so train is positioned at end of route
            if (StationPlatformReference < 0)
            {
                // get furthest index
                int firstSectionIndex = train.OccupiedTrack[0].Index;
                int lastSectionIndex = train.OccupiedTrack.Last().Index;
                int lastRouteIndex = Math.Max(train.ValidRoutes[Direction.Forward].GetRouteIndex(firstSectionIndex, 0), train.ValidRoutes[Direction.Forward].GetRouteIndex(lastSectionIndex, 0));
                TrackCircuitPartialPathRoute newRoute = new TrackCircuitPartialPathRoute(train.TCRoute.TCRouteSubpaths[train.TCRoute.ActiveSubPath], 0, lastRouteIndex);
                train.TCRoute.TCRouteSubpaths[train.TCRoute.ActiveSubPath] = new TrackCircuitPartialPathRoute(newRoute);
                train.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(newRoute);

                train.MovementState = AiMovementState.Stopped;
            }
        }

        /// <summary>
        /// Set transfer cross-reference train information
        /// </summary>
        /// <param name="dettrain"></param>
        /// <param name="trainList"></param>
        /// <param name="playerTrain"></param>
        public void SetTransferXRef(TTTrain dettrain, List<TTTrain> trainList, TTTrain playerTrain, bool stationTransfer, bool trainTransfer)
        {
            bool trainFound = false;
            TTTrain transferTrain = null;

            foreach (TTTrain otherTrain in trainList)
            {
                if (string.Equals(otherTrain.Name, TrainName, StringComparison.OrdinalIgnoreCase))
                {
                    TrainNumber = otherTrain.OrgAINumber;
                    transferTrain = otherTrain;
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
                    transferTrain = playerTrain;
                    trainFound = true;
                }
            }

            // issue warning if train not found
            if (!trainFound)
            {
                Trace.TraceWarning("Train :  {0} : transfer details : train {1} is not found",
                    dettrain.Name, TrainName);
                Valid = false;
                Trace.TraceWarning("Train {0} : transfer command with train {1} : command invalid", dettrain.Name, TrainName);
            }
            else
            // set need to transfer
            {
                if (stationTransfer)
                {
                    if (transferTrain.NeedStationTransfer.TryGetValue(StationPlatformReference, out List<int> value))
                    {
                        value.Add(dettrain.OrgAINumber);
                    }
                    else
                    {
                        value = [dettrain.OrgAINumber];
                        transferTrain.NeedStationTransfer.Add(StationPlatformReference, value);
                    }
                }
                else
                {
                    // get last section if train - assume this to be the transfer section
                    int lastSectionIndex = dettrain.TCRoute.TCRouteSubpaths.Last().Last().TrackCircuitSection.Index;
                    if (transferTrain.NeedTrainTransfer.TryGetValue(lastSectionIndex, out int value))
                    {
                        int transferCount = value;
                        transferCount++;
                        transferTrain.NeedTrainTransfer.Remove(lastSectionIndex);
                        transferTrain.NeedTrainTransfer.Add(lastSectionIndex, transferCount);
                    }
                    else
                    {
                        transferTrain.NeedTrainTransfer.Add(lastSectionIndex, 1);
                    }
                }
            }
        }
    }
}

