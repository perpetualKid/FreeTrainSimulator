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
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.Imported.State;

using Orts.Formats.Msts;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;

namespace Orts.Simulation.Timetables
{
    /// <summary>
    /// Class for detach information
    /// </summary>
    public class DetachInfo : TimetableTrainInfo, ISaveStateApi<DetachInfoSaveState>
    {
        private bool? reverseDetachedTrain;
        private bool playerAutoDetach;
        private List<string> detachConsists;
        private int numberOfUnits;

        public DetachPositionInfo DetachPosition { get; private set; }
        public TransferUnits DetachUnits { get; set; }
        public bool DetachFormedStatic { get; private set; }                     // used for station and startup detaches
        public int? DetachTime { get; private set; }

        public DetachInfo() { }

        /// <summary>
        /// Default constructor for auto-generated detach
        /// </summary>
        /// <param name="atStart"></param>
        /// <param name="atEnd"></param>
        /// <param name="atStation"></param>
        /// <param name="sectionIndex"></param>
        /// <param name="leadingPower"></param>
        /// <param name="trailingPower"></param>
        /// <param name="units"></param>
        public DetachInfo(DetachPositionInfo detachPosition, int sectionIndex, TransferUnits detachUnits, int units, int? time, int formedTrain, bool reverseTrain)
        {
            DetachPosition = detachPosition;
            if (detachPosition == DetachPositionInfo.Station || detachPosition == DetachPositionInfo.Section)
            {
                StationPlatformReference = sectionIndex;
            }

            if (detachUnits == TransferUnits.UnitsAtEnd)
                numberOfUnits = -units;

            DetachUnits = detachUnits;

            if (detachUnits == TransferUnits.UnitsAtFront || detachUnits == TransferUnits.UnitsAtEnd)
            {
                    numberOfUnits = Math.Abs(units);
            }

            DetachTime = time;
            TrainNumber = formedTrain;
            DetachFormedStatic = false;
            reverseDetachedTrain = reverseTrain;
            playerAutoDetach = true;
            detachConsists = null;
            Valid = true;
        }

        /// <summary>
        /// Default constructor for detach at station or at start
        /// </summary>
        /// <param name="train"></param>
        /// <param name="commandInfo"></param>
        /// <param name="atActivation"></param>
        /// <param name="atStation"></param>
        /// <param name="detachSectionIndex"></param>
        /// <param name="detachTime"></param>
        public DetachInfo(TTTrain train, TTTrainCommands commandInfo, bool atActivation, bool atStation, bool atForms, int detachSectionIndex, int? detachTime)
        {
            DetachPosition = atActivation ? DetachPositionInfo.Activation : atStation ? DetachPositionInfo.Station : atForms ? DetachPositionInfo.End : DetachPositionInfo.Section;
            StationPlatformReference = detachSectionIndex;
            TrainNumber = -1;
            playerAutoDetach = true;
            detachConsists = null;
            TrainName = String.Empty;

            bool portionDefined = false;
            bool formedTrainDefined = false;


            if (commandInfo?.CommandQualifiers == null || commandInfo.CommandQualifiers.Count < 1)
            {
                Trace.TraceInformation("Train {0} : missing detach command qualifiers", train?.Name);
                Valid = false;
                return;
            }

            foreach (TTTrainCommands.TTTrainComQualifiers Qualifier in commandInfo.CommandQualifiers)
            {
                switch (Qualifier.QualifierName)
                {
                    // detach info qualifiers
                    case string tranferType when tranferType.Equals("power", StringComparison.OrdinalIgnoreCase):
                        DetachUnits = TransferUnits.OnlyPower;
                        portionDefined = true;
                        break;

                    case string tranferType when tranferType.Equals("leadingpower", StringComparison.OrdinalIgnoreCase):
                        DetachUnits = TransferUnits.LeadingPower;
                        portionDefined = true;
                        break;

                    case string tranferType when tranferType.Equals("allleadingpower", StringComparison.OrdinalIgnoreCase):
                        DetachUnits = TransferUnits.AllLeadingPower;
                        portionDefined = true;
                        break;

                    case string tranferType when tranferType.Equals("trailingpower", StringComparison.OrdinalIgnoreCase):
                        DetachUnits = TransferUnits.TrailingPower;
                        portionDefined = true;
                        break;

                    case string tranferType when tranferType.Equals("alltrailingpower", StringComparison.OrdinalIgnoreCase):
                        DetachUnits = TransferUnits.AllTrailingPower;
                        portionDefined = true;
                        break;

                    case string tranferType when tranferType.Equals("nonpower", StringComparison.OrdinalIgnoreCase):
                        DetachUnits = TransferUnits.NonPower;
                        portionDefined = true;
                        break;

                    case string tranferType when tranferType.Equals("units", StringComparison.OrdinalIgnoreCase):

                        if (int.TryParse(Qualifier.QualifierValues[0], out int nounits))
                        {
                            if (nounits > 0)
                            {
                                DetachUnits = TransferUnits.UnitsAtFront;
                            }
                            else
                            {
                                DetachUnits = TransferUnits.UnitsAtEnd;
                            }
                            numberOfUnits = Math.Abs(nounits);
                            portionDefined = true;
                        }
                        else
                        {
                            Trace.TraceInformation("Train {0} : invalid value for units qualifier in detach command : {1}", train.Name, Qualifier.QualifierValues[0]);
                        }
                        break;

                    case string tranferType when tranferType.Equals("consist", StringComparison.OrdinalIgnoreCase):
                        DetachUnits = TransferUnits.Consists;

                        if (detachConsists == null)
                            detachConsists = new List<string>();

                        foreach (string consistname in Qualifier.QualifierValues)
                        {
                            detachConsists.Add(consistname);
                        }
                        portionDefined = true;
                        break;

                    // form qualifier
                    case string tranferType when tranferType.Equals("forms", StringComparison.OrdinalIgnoreCase):
                        if (Qualifier.QualifierValues == null || Qualifier.QualifierValues.Count <= 0)
                        {
                            Trace.TraceInformation("Train {0} : detach command : missing name for formed train", train.Name);
                        }
                        else
                        {
                            TrainName = Qualifier.QualifierValues[0];
                            if (!TrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
                            {
                                int seppos = train.Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                                TrainName += $":{train.Name[(seppos + 1)..]}";
                            }

                            DetachFormedStatic = false;
                            formedTrainDefined = true;
                        }
                        break;

                    // static qualifier
                    case string tranferType when tranferType.Equals("static", StringComparison.OrdinalIgnoreCase):
                        if (Qualifier.QualifierValues != null && Qualifier.QualifierValues.Count > 0)
                        {
                            TrainName = Qualifier.QualifierValues[0];
                            if (!TrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
                            {
                                int seppos = train.Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                                TrainName += $":{train.Name.Substring(seppos + 1)}";
                            }
                        }

                        DetachFormedStatic = true;
                        formedTrainDefined = true;
                        break;

                    // manual or auto detach for player train (note : not yet implemented)
                    case string tranferType when tranferType.Equals("manual", StringComparison.OrdinalIgnoreCase):
                        playerAutoDetach = false;
                        break;

                    case string tranferType when tranferType.Equals("auto", StringComparison.OrdinalIgnoreCase):
                        playerAutoDetach = true;
                        break;

                    // default : invalid qualifier
                    default:
                        Trace.TraceWarning("Train {0} : invalid qualifier for detach command : {1}", train.Name, Qualifier.QualifierName.Trim());
                        break;
                }
            }

            // set detach time to arrival time or activate time
            DetachTime = detachTime ?? 0;

            if (!portionDefined)
            {
                Trace.TraceWarning("Train {0} : detach command : missing portion information", train.Name);
                Valid = false;
            }
            else if (!formedTrainDefined)
            {
                Trace.TraceWarning("Train {0} : detach command : no train defined for detached portion", train.Name);
                Valid = false;
            }
            else
            {
                Valid = true;
            }
        }

        /// <summary>
        /// Perform detach, return state : true if detach may be performed, false if detach is handled through window
        /// </summary>
        /// <param name="train"></param>
        /// <param name="presentTime"></param>
        /// <returns></returns>
        public bool PerformDetach(TTTrain train, bool allowPlayerSelect)
        {
            // Determine no. of units to detach

            int iunits = 0;
            bool frontpos = true;

            // if position of power not defined, set position according to present position of power
            if (DetachUnits == TransferUnits.OnlyPower)
            {
                DetachUnits = TransferUnits.AllLeadingPower;
                if (train.Cars[0].WagonType == WagonType.Engine || train.Cars[0].WagonType == WagonType.Tender)
                {
                    DetachUnits = TransferUnits.AllLeadingPower;
                }
                else
                {
                    DetachUnits = TransferUnits.AllTrailingPower;
                }
            }

            iunits = train.GetUnitsToDetach(this.DetachUnits, this.numberOfUnits, this.detachConsists, ref frontpos);

            // check if anything to detach and anything left on train

            TTTrain newTrain = null;
            if (TrainNumber == 0)
            {
                newTrain = Simulator.Instance.PlayerLocomotive.Train as TTTrain;
            }
            else
            {
                newTrain = Simulator.Instance.GetAutoGenTTTrainByNumber(TrainNumber);
            }

            if (newTrain == null)
            {
                Trace.TraceInformation("Train {0} : detach to train {1} : cannot find new train", train.Name, TrainName);
            }

            train.DetachUnits = iunits;
            train.DetachPosition = frontpos;

            if (iunits == 0)
            {
                Trace.TraceInformation("Train {0} : detach to train {1} : no units to detach", train.Name, TrainName);
            }
            else if (iunits == train.Cars.Count)
            {
                Trace.TraceInformation("Train {0} : detach to train {1} : no units remaining on train", train.Name, TrainName);
            }
            else
            {
                if (newTrain == null)
                {
                    // create dummy train - train will be removed but timetable can continue
                    newTrain = new TTTrain(train);
                    newTrain.AI = train.AI;  // set AT as Simulator.AI does not exist in prerun mode
                    newTrain.ValidRoutes[Direction.Forward] = SignalEnvironment.BuildTempRoute(newTrain, train.PresentPosition[Direction.Forward].TrackCircuitSectionIndex,
                        train.PresentPosition[Direction.Forward].Offset, train.PresentPosition[Direction.Forward].Direction, train.Length, true, true, false);
                    newTrain.PresentPosition[Direction.Forward].UpdateFrom(train.PresentPosition[Direction.Forward]);
                    newTrain.PresentPosition[Direction.Backward].UpdateFrom(train.PresentPosition[Direction.Backward]);

                    reverseDetachedTrain = false;
                    int newLocoIndex = train.TTUncoupleBehind(newTrain, reverseDetachedTrain.Value, train.LeadLocomotiveIndex, false);
                    newTrain.RemoveTrain();
                }
                else
                {
                    // if new train has no route, create from present position
                    if (newTrain.TCRoute == null)
                    {
                        TrackCircuitPartialPathRoute newTrainPath = new TrackCircuitPartialPathRoute(train.ValidRoutes[Direction.Forward], train.PresentPosition[Direction.Backward].RouteListIndex, train.PresentPosition[Direction.Forward].RouteListIndex);
                        newTrain.TCRoute = new TrackCircuitRoutePath(newTrainPath);
                        newTrain.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(newTrain.TCRoute.TCRouteSubpaths[0]);
                    }

                    // handle player train
                    if (train.TrainType == TrainType.Player)
                    {
                        bool detachablePower = CheckDetachedDriveablePower(train);
                        bool keepPower = CheckKeepDriveablePower(train);

                        // if both portions contain detachable power, display window
                        // quit method as detaching is handled through window button activation
                        if (detachablePower && keepPower && allowPlayerSelect)
                        {
                            // reinsert newtrain as autogen train otherwise it cannot be found
                            train.AI.AutoGenTrains.Add(newTrain);
                            Simulator.Instance.AutoGenDictionary.Add(newTrain.Number, newTrain);

                            // show window, set detach action is pending
                            Simulator.Instance.OnRequestTTDetachWindow();
                            train.DetachPending = true;
                            return (false);
                        }

                        // detachable portion has no power, so detach immediately

                        bool playerEngineInRemainingPortion = CheckPlayerPowerPortion(train);

                        // player engine is in remaining portion
                        if (playerEngineInRemainingPortion)
                        {
                            if (!reverseDetachedTrain.HasValue)
                            {
                                reverseDetachedTrain = GetDetachReversalInfo(train, newTrain);
                            }
                            int newLocoIndex = train.TTUncoupleBehind(newTrain, reverseDetachedTrain.Value, train.LeadLocomotiveIndex, false);
                            train.LeadLocomotiveIndex = newLocoIndex;
                            Simulator.Instance.Confirmer?.Information($"{train.DetachUnits} units detached as train : {newTrain.Name}");
                            train.DetachActive[DetachDetailsIndex.DetachActiveList] = -1;

                            // set proper details for new train
                            newTrain.SetFormedOccupied();
                            newTrain.ControlMode = TrainControlMode.Inactive;
                            newTrain.MovementState = AiMovementState.Static;
                            newTrain.SetupStationStopHandling();
                        }
                        // keep portion has no power, so detach immediately and switch to new train
                        else
                        {
                            if (!reverseDetachedTrain.HasValue)
                            {
                                reverseDetachedTrain = GetDetachReversalInfo(train, newTrain);
                            }
                            int newLocoIndex = train.TTUncoupleBehind(newTrain, reverseDetachedTrain.Value, train.LeadLocomotiveIndex, true);
                            Simulator.Instance.Confirmer?.Information($"{train.DetachUnits} units detached as train : {newTrain.Name}");
                            Trace.TraceInformation($"Detach : {train.DetachUnits} units detached as train : {newTrain.Name}");
                            train.DetachActive[DetachDetailsIndex.DetachActiveList] = -1;

                            // set proper details for existing train
                            train.Number = train.OrgAINumber;
                            train.TrainType = TrainType.Ai;
                            train.LeadLocomotiveIndex = -1;
                            Simulator.Instance.Trains.Remove(train);
                            train.AI.TrainsToRemoveFromAI.Add(train);
                            train.AI.TrainsToAdd.Add(train);

                            // set proper details for new train
                            newTrain.AI.TrainsToRemoveFromAI.Add(newTrain);

                            // set proper details for new formed train
                            newTrain.OrgAINumber = newTrain.Number;
                            newTrain.Number = 0;
                            newTrain.LeadLocomotiveIndex = newLocoIndex;
                            newTrain.AI.TrainsToAdd.Add(newTrain);
                            newTrain.AI.TrainListChanged = true;
                            Simulator.Instance.Trains.Add(newTrain);

                            newTrain.SetFormedOccupied();
                            newTrain.TrainType = TrainType.Player;
                            newTrain.ControlMode = TrainControlMode.Inactive;
                            newTrain.MovementState = AiMovementState.Static;

                            // inform viewer about player train switch
                            Simulator.Instance.OnPlayerTrainChanged(train, newTrain);
                            Simulator.Instance.PlayerLocomotive.Train = newTrain;

                            newTrain.SetupStationStopHandling();

                            // clear replay commands
                            Simulator.Instance.Log.CommandList.Clear();

                            // display messages
                            Simulator.Instance.Confirmer?.Information("Player switched to train : " + newTrain.Name);// As Confirmer may not be created until after a restore.
                        }
                    }
                    else
                    {
                        Simulator.Instance.AutoGenDictionary?.Remove(newTrain.Number);

                        if (!reverseDetachedTrain.HasValue)
                        {
                            reverseDetachedTrain = GetDetachReversalInfo(train, newTrain);
                        }

                        bool newIsPlayer = newTrain.TrainType == TrainType.PlayerIntended;
                        newTrain.LeadLocomotiveIndex = train.TTUncoupleBehind(newTrain, reverseDetachedTrain.Value, -1, newIsPlayer);
                        train.DetachActive[DetachDetailsIndex.DetachActiveList] = -1;
                    }
                }


                // if train is player or intended player, determine new loco lead index
                if (train.TrainType == TrainType.Player || train.TrainType == TrainType.PlayerIntended)
                {
                    if (train.LeadLocomotiveIndex >= 0)
                    {
                        train.LeadLocomotive = Simulator.Instance.PlayerLocomotive = train.Cars[train.LeadLocomotiveIndex] as MSTSLocomotive;
                    }
                    else
                    {
                        train.LeadLocomotive = null;
                        Simulator.Instance.PlayerLocomotive = null;

                        for (int iCar = 0; iCar <= train.Cars.Count - 1 && train.LeadLocomotiveIndex < 0; iCar++)
                        {
                            var eachCar = train.Cars[iCar];
                            if (eachCar is MSTSLocomotive locomotive)
                            {
                                train.LeadLocomotive = Simulator.Instance.PlayerLocomotive = locomotive;
                                train.LeadLocomotiveIndex = iCar;
                            }
                        }
                    }
                }

                else if (newTrain.TrainType == TrainType.Player || newTrain.TrainType == TrainType.PlayerIntended)
                {
                    newTrain.TrainType = TrainType.Player;
                    newTrain.ControlMode = TrainControlMode.Inactive;
                    newTrain.MovementState = AiMovementState.Static;
                    if (!newTrain.StartTime.HasValue)
                        newTrain.StartTime = 0;

                    newTrain.AI.TrainsToAdd.Add(newTrain);

                    if (newTrain.LeadLocomotiveIndex >= 0)
                    {
                        newTrain.LeadLocomotive = Simulator.Instance.PlayerLocomotive = newTrain.Cars[newTrain.LeadLocomotiveIndex] as MSTSLocomotive;
                    }
                    else
                    {
                        newTrain.LeadLocomotive = null;
                        Simulator.Instance.PlayerLocomotive = null;

                        for (int iCar = 0; iCar <= newTrain.Cars.Count - 1 && newTrain.LeadLocomotiveIndex < 0; iCar++)
                        {
                            var eachCar = newTrain.Cars[iCar];
                            if (eachCar is MSTSLocomotive locomotive)
                            {
                                newTrain.LeadLocomotive = Simulator.Instance.PlayerLocomotive = locomotive;
                                newTrain.LeadLocomotiveIndex = iCar;
                            }
                        }
                    }
                }
            }

            return (true);
        }

        /// <summary>
        /// Perform detach for player train
        /// Called from player detach selection window
        /// </summary>
        /// <param name="train"></param>
        /// <param name="newTrainNumber"></param>
        public void DetachPlayerTrain(TTTrain train, int newTrainNumber)
        {
            // Determine no. of units to detach

            int iunits = 0;
            bool frontpos = true;

            // if position of power not defined, set position according to present position of power
            if (DetachUnits == TransferUnits.OnlyPower)
            {
                DetachUnits = TransferUnits.AllLeadingPower;
                if (train.Cars[0].WagonType == WagonType.Engine || train.Cars[0].WagonType == WagonType.Tender)
                {
                    DetachUnits = TransferUnits.AllLeadingPower;
                }
                else
                {
                    DetachUnits = TransferUnits.AllTrailingPower;
                }
            }

            iunits = train.GetUnitsToDetach(this.DetachUnits, this.numberOfUnits, this.detachConsists, ref frontpos);

            // check if anything to detach and anything left on train

            train.DetachUnits = iunits;
            train.DetachPosition = frontpos;

            TTTrain newTrain = Simulator.Instance.GetAutoGenTTTrainByNumber(newTrainNumber);
            if (newTrain == null)
            {
                newTrain = train.AI.StartList.GetNotStartedTTTrainByNumber(newTrainNumber, true);
            }
            bool playerEngineInRemainingPortion = CheckPlayerPowerPortion(train);

            reverseDetachedTrain = GetDetachReversalInfo(train, newTrain);

            int newLocoIndex = train.TTUncoupleBehind(newTrain, reverseDetachedTrain.Value, train.LeadLocomotiveIndex, !playerEngineInRemainingPortion);
            Simulator.Instance.Confirmer?.Information($"{train.DetachUnits} units detached as train : {newTrain.Name}");
            Trace.TraceInformation($"{train.DetachUnits} units detached as train : {newTrain.Name}");
            train.DetachActive[DetachDetailsIndex.DetachActiveList] = -1;

            // player engine is in remaining portion
            if (playerEngineInRemainingPortion)
            {
                train.LeadLocomotiveIndex = newLocoIndex;
            }

            // player engine is in detached portion, so switch trains
            else
            {
                // set proper details for existing train
                train.Number = train.OrgAINumber;
                train.TrainType = TrainType.Ai;
                train.MovementState = train.AtStation ? AiMovementState.StationStop : AiMovementState.Stopped;
                train.LeadLocomotiveIndex = -1;
                Simulator.Instance.Trains.Remove(train);
                train.AI.TrainsToRemoveFromAI.Add(train);
                train.AI.TrainsToAdd.Add(train);
                train.MUDirection = MidpointDirection.Forward;

                // set proper details for new formed train
                newTrain.AI.TrainsToRemoveFromAI.Add(newTrain);

                newTrain.OrgAINumber = newTrain.Number;
                newTrain.Number = 0;
                newTrain.LeadLocomotiveIndex = newLocoIndex;
                newTrain.TrainType = TrainType.Player;
                newTrain.ControlMode = TrainControlMode.Inactive;
                newTrain.MovementState = AiMovementState.Static;
                newTrain.AI.TrainsToAdd.Add(newTrain);
                newTrain.AI.TrainListChanged = true;
                Simulator.Instance.Trains.Add(newTrain);

                newTrain.SetFormedOccupied();

                // inform viewer about player train switch
                Simulator.Instance.OnPlayerTrainChanged(train, newTrain);
                Simulator.Instance.PlayerLocomotive.Train = newTrain;

                newTrain.SetupStationStopHandling();

                // clear replay commands
                Simulator.Instance.Log.CommandList.Clear();

                // display messages
                Simulator.Instance.Confirmer?.Information("Player switched to train : " + newTrain.Name);// As Confirmer may not be created until after a restore.
                Trace.TraceInformation("Player switched to train : " + newTrain.Name);
            }

            train.DetachPending = false;   // detach completed
        }

        /// <summary>
        /// Set detach cross-reference train information
        /// </summary>
        /// <param name="dettrain"></param>
        /// <param name="trainList"></param>
        /// <param name="playerTrain"></param>
        public void SetDetachXRef(TTTrain dettrain, List<TTTrain> trainList, TTTrain playerTrain)
        {
            bool trainFound = false;

            foreach (TTTrain otherTrain in trainList)
            {
                if (string.Equals(otherTrain.Name, TrainName, StringComparison.OrdinalIgnoreCase))
                {
                    if (otherTrain.FormedOf >= 0)
                    {
                        Trace.TraceWarning("Train : {0} : detach details : detached train {1} already formed out of another train",
                            dettrain.Name, otherTrain.Name);
                        break;
                    }

                    otherTrain.FormedOf = dettrain.Number;
                    otherTrain.FormedOfType = TimetableFormationCommand.Detached;
                    otherTrain.TrainType = TrainType.AiAutoGenerated;
                    TrainNumber = otherTrain.Number;
                    trainFound = true;

                    break;
                }
            }

            // if not found, try player train
            if (!trainFound)
            {
                if (playerTrain != null && string.Equals(playerTrain.Name, TrainName, StringComparison.OrdinalIgnoreCase))
                {
                    if (playerTrain.FormedOf >= 0)
                    {
                        Trace.TraceWarning("Train : {0} : detach details : detached train {1} already formed out of another train",
                            dettrain.Name, playerTrain.Name);
                    }

                    playerTrain.FormedOf = dettrain.Number;
                    playerTrain.FormedOfType = TimetableFormationCommand.Detached;
                    TrainNumber = playerTrain.Number;
                    trainFound = true;
                }
            }

            // issue warning if train not found
            if (!trainFound)
            {
                Trace.TraceWarning("Train :  {0} : detach details : detached train {1} not found",
                    dettrain.Name, TrainName);
            }
        }

        /// <summary>
        /// Check if detached portion off player train has driveable power
        /// </summary>
        /// <param name="train"></param>
        /// <returns></returns>
        public bool CheckDetachedDriveablePower(TTTrain train)
        {
            bool portionHasDriveablePower = false;

            // detach at front
            if (train.DetachPosition)
            {
                for (int iCar = 0; iCar < train.DetachUnits && !portionHasDriveablePower; iCar++)
                {
                    var thisCar = train.Cars[iCar];
                    if (thisCar is MSTSLocomotive)
                        portionHasDriveablePower = true;
                }
            }
            // detach at rear
            else
            {
                for (int iCar = 0; iCar < train.DetachUnits && !portionHasDriveablePower; iCar++)
                {
                    int actCar = train.Cars.Count - 1 - iCar;
                    var thisCar = train.Cars[actCar];
                    if (thisCar is MSTSLocomotive)
                        portionHasDriveablePower = true;
                }
            }
            return (portionHasDriveablePower);
        }

        //================================================================================================//
        /// <summary>
        /// Check if remaining portion off player train has driveable power
        /// </summary>
        /// <param name="train"></param>
        /// <returns></returns>
        public bool CheckKeepDriveablePower(TTTrain train)
        {
            bool portionHasDriveablePower = false;

            // detach at front - so check rear portion
            if (train.DetachPosition)
            {
                for (int iCar = 0; iCar < (train.Cars.Count - train.DetachUnits) && !portionHasDriveablePower; iCar++)
                {
                    int actCar = train.Cars.Count - 1 - iCar;
                    var thisCar = train.Cars[actCar];
                    if (thisCar is MSTSLocomotive)
                        portionHasDriveablePower = true;
                }
            }
            // detach at rear - so check front portion
            else
            {
                for (int iCar = 0; iCar < (train.Cars.Count - train.DetachUnits) && !portionHasDriveablePower; iCar++)
                {
                    var thisCar = train.Cars[iCar];
                    if (thisCar is MSTSLocomotive)
                        portionHasDriveablePower = true;
                }
            }
            return (portionHasDriveablePower);
        }

        //================================================================================================//
        /// <summary>
        /// Check if player engine is in remaining or detached portion
        /// </summary>
        /// <param name="train"></param>
        /// <returns></returns>
        public bool CheckPlayerPowerPortion(TTTrain train)
        {
            bool PlayerInRemainingPortion = false;

            // detach at front - so check rear portion
            if (train.DetachPosition)
            {
                for (int iCar = 0; iCar < (train.Cars.Count - train.DetachUnits) && !PlayerInRemainingPortion; iCar++)
                {
                    int actCar = train.Cars.Count - 1 - iCar;
                    if (actCar == train.LeadLocomotiveIndex)
                    {
                        PlayerInRemainingPortion = true;
                    }
                }
            }
            // detach at rear - so check front portion
            else
            {
                for (int iCar = 0; iCar < (train.Cars.Count - train.DetachUnits) && !PlayerInRemainingPortion; iCar++)
                {
                    if (iCar == train.LeadLocomotiveIndex)
                    {
                        PlayerInRemainingPortion = true;
                    }
                }
            }
            return (PlayerInRemainingPortion);
        }

        /// <summary>
        /// Determine if detached train is to be reversed
        /// </summary>
        /// <param name="thisTrain"></param>
        /// <param name="detachedTrain"></param>
        /// <returns></returns>
        public bool GetDetachReversalInfo(TTTrain thisTrain, TTTrain detachedTrain)
        {
            bool reversed = false;

            // if detached at front, use front position
            if (thisTrain.DetachPosition)
            {
                int frontSectionIndex = thisTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                TrackDirection thisDirection = thisTrain.PresentPosition[Direction.Forward].Direction;

                TrackCircuitPartialPathRoute otherPath = detachedTrain.TCRoute.TCRouteSubpaths[0];
                int otherTrainIndex = otherPath.GetRouteIndex(frontSectionIndex, 0);

                if (otherTrainIndex >= 0)
                {
                    TrackDirection otherTrainDirection = otherPath[otherTrainIndex].Direction;
                    reversed = (thisDirection != otherTrainDirection);
                }
            }
            else
            {
                int frontSectionIndex = thisTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex;
                TrackDirection thisDirection = thisTrain.PresentPosition[Direction.Backward].Direction;

                TrackCircuitPartialPathRoute otherPath = detachedTrain.TCRoute.TCRouteSubpaths[0];
                int otherTrainIndex = otherPath.GetRouteIndex(frontSectionIndex, 0);

                if (otherTrainIndex >= 0)
                {
                    TrackDirection otherTrainDirection = otherPath[otherTrainIndex].Direction;
                    reversed = (thisDirection != otherTrainDirection);
                }
            }

            return (reversed);
        }

        public ValueTask<DetachInfoSaveState> Snapshot()
        {
            return ValueTask.FromResult(new DetachInfoSaveState()
            {
                DetachPosition = DetachPosition,
                DetachTime = DetachTime,
                DetachSectionIndex = StationPlatformReference,
                DetachUnits = DetachUnits,
                DetachUnitsNumber = numberOfUnits,
                TrainNumber = TrainNumber,
                TrainName = TrainName,
                ReverseDetachedTrain = reverseDetachedTrain,
                PlayerAutoDetach = playerAutoDetach,
                DetachConsists = detachConsists == null || detachConsists.Count == 0 ? null : new Collection<string>(detachConsists),
                DetachFormedStatic = DetachFormedStatic,
                Valid = Valid,
            });
        }

        public ValueTask Restore(DetachInfoSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            DetachPosition = saveState.DetachPosition;
            StationPlatformReference = saveState.DetachSectionIndex;
            DetachUnits = saveState.DetachUnits;
            numberOfUnits = saveState.DetachUnitsNumber;
            DetachTime = saveState.DetachTime;
            TrainNumber = saveState.TrainNumber;
            TrainName = saveState.TrainName;
            reverseDetachedTrain = saveState.ReverseDetachedTrain;
            playerAutoDetach = saveState.PlayerAutoDetach;
            detachConsists = saveState.DetachConsists == null ? null : new List<string>(saveState.DetachConsists);
            DetachFormedStatic = saveState.DetachFormedStatic;
            Valid = saveState.Valid;

            return ValueTask.CompletedTask;
        }
    }
}

