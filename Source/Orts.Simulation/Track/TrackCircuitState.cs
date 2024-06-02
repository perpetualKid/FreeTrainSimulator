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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using Microsoft.CodeAnalysis.Operations;

using Orts.Common;
using Orts.Models.State;
using Orts.Simulation.Physics;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Track
{
    //================================================================================================//
    /// <summary>
    ///
    /// class TrackCircuitState
    /// Class for track circuit state
    ///
    /// </summary>
    //================================================================================================//
    public class TrackCircuitState : ISaveStateApi<TrackCircuitStateSaveState>
    {
        internal TrackOccupationState OccupationState { get; set; }  // trains occupying section      //
        public Train.TrainRouted TrainReserved { get; internal set; }   // train reserving section       //
        public int SignalReserved { get; internal set; }        // signal reserving section      //
        internal TrainQueue TrainPreReserved { get; }             // trains with pre-reservation   //
        internal TrainQueue TrainClaimed { get; }                 // trains with normal claims     //
        public bool Forced { get; internal set; }               // forced by human dispatcher    //

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public TrackCircuitState()
        {
            OccupationState = new TrackOccupationState();
            SignalReserved = -1;
            TrainPreReserved = new TrainQueue();
            TrainClaimed = new TrainQueue();
        }


        //================================================================================================//
        /// <summary>
        /// Restore
        /// IMPORTANT : trains are restored to dummy value, will be restored to full contents later
        /// </summary>
        public void Restore(BinaryReader inf)
        {
            ArgumentNullException.ThrowIfNull(inf);

            int occupied = inf.ReadInt32();
            for (int train = 0; train < occupied; train++)
            {
                int trainNumber = inf.ReadInt32();
                int trainRouteIndex = inf.ReadInt32();
                int trainDirection = inf.ReadInt32();
                Train thisTrain = new Train(trainNumber);
                Train.TrainRouted thisRouted = new Train.TrainRouted(thisTrain, (Direction)trainRouteIndex);
                OccupationState.Add(thisRouted, (Direction)trainDirection);
            }

            int trainReserved = inf.ReadInt32();
            if (trainReserved >= 0)
            {
                int trainRouteIndexR = inf.ReadInt32();
                Train thisTrain = new Train(trainReserved);
                Train.TrainRouted trainRoute = new Train.TrainRouted(thisTrain, (Direction)trainRouteIndexR);
                TrainReserved = trainRoute;
            }

            SignalReserved = inf.ReadInt32();

            int noPreReserve = inf.ReadInt32();
            for (int trainNo = 0; trainNo < noPreReserve; trainNo++)
            {
                int trainNumber = inf.ReadInt32();
                int trainRouteIndex = inf.ReadInt32();
                Train thisTrain = new Train(trainNumber);
                Train.TrainRouted thisRouted = new Train.TrainRouted(thisTrain, (Direction)trainRouteIndex);
                TrainPreReserved.Enqueue(thisRouted);
            }

            int noClaimed = inf.ReadInt32();
            for (int trainNo = 0; trainNo < noClaimed; trainNo++)
            {
                int trainNumber = inf.ReadInt32();
                int trainRouteIndex = inf.ReadInt32();
                Train thisTrain = new Train(trainNumber);
                Train.TrainRouted thisRouted = new Train.TrainRouted(thisTrain, (Direction)trainRouteIndex);
                TrainClaimed.Enqueue(thisRouted);
            }
            Forced = inf.ReadBoolean();

        }


        //================================================================================================//
        /// <summary>
        /// Reset train references after restore
        /// </summary>
        public void RestoreTrains(List<Train> trains, int sectionIndex)
        {
            // Occupy
            Dictionary<(int, Direction), Direction> tempTrains = new Dictionary<(int, Direction), Direction>();

            foreach (KeyValuePair<Train.TrainRouted, Direction> occupation in OccupationState)
            {
                tempTrains.Add((occupation.Key.Train.Number, occupation.Key.Direction), occupation.Value);
            }

            OccupationState.Clear();

            foreach (KeyValuePair<(int, Direction), Direction> item in tempTrains)
            {
                int number = item.Key.Item1;
                Direction routeDirection = item.Key.Item2;
                Train train = SignalEnvironment.FindTrain(number, trains);
                if (train != null)
                {
                    Train.TrainRouted trainRouted = routeDirection == Direction.Forward ? train.RoutedForward : train.RoutedBackward;
                    OccupationState.Add(trainRouted, item.Value);
                }
            }

            // Reserved

            if (TrainReserved != null)
            {
                int number = TrainReserved.Train.Number;
                Train reservedTrain = SignalEnvironment.FindTrain(number, trains);
                if (reservedTrain != null)
                {
                    bool validreserve = true;

                    // check if reserved section is on train's route except when train is in explorer or manual mode
                    if (reservedTrain.ValidRoutes[TrainReserved.Direction].Count > 0 && reservedTrain.ControlMode != TrainControlMode.Explorer && reservedTrain.ControlMode != TrainControlMode.Manual)
                    {
                        _ = reservedTrain.ValidRoutes[TrainReserved.Direction].GetRouteIndex(sectionIndex, reservedTrain.PresentPosition[Direction.Forward].RouteListIndex);
                        validreserve = reservedTrain.ValidRoutes[TrainReserved.Direction].GetRouteIndex(sectionIndex, reservedTrain.PresentPosition[Direction.Forward].RouteListIndex) >= 0;
                    }

                    if (validreserve || reservedTrain.ControlMode == TrainControlMode.Explorer)
                    {
                        TrainReserved = TrainReserved.Direction == Direction.Forward ? reservedTrain.RoutedForward : reservedTrain.RoutedBackward;
                    }
                    else
                    {
                        Trace.TraceInformation("Invalid reservation for train : {0} [{1}], section : {2} not restored", reservedTrain.Name, TrainReserved.Direction, sectionIndex);
                    }
                }
                else
                {
                    TrainReserved = null;
                }
            }

            // PreReserved
            Queue<Train.TrainRouted> queue = new Queue<Train.TrainRouted>(TrainPreReserved);
            TrainPreReserved.Clear();

            foreach (Train.TrainRouted trainRouted in queue)
            {
                Train train = SignalEnvironment.FindTrain(trainRouted.Train.Number, trains);
                if (train != null)
                {
                    TrainPreReserved.Enqueue(trainRouted.Direction == Direction.Forward ? train.RoutedForward : train.RoutedBackward);
                }
            }

            // Claimed
            queue = new Queue<Train.TrainRouted>(TrainClaimed);
            TrainClaimed.Clear();

            foreach (Train.TrainRouted trainRouted in queue)
            {
                Train train = SignalEnvironment.FindTrain(trainRouted.Train.Number, trains);
                if (train != null)
                {
                    TrainClaimed.Enqueue(trainRouted.Direction == Direction.Forward ? train.RoutedForward : train.RoutedBackward);
                }
            }
        }

        public ValueTask<TrackCircuitStateSaveState> Snapshot()
        {
            return ValueTask.FromResult(new TrackCircuitStateSaveState()
            { 
                OccupationStates = new Collection<TrainReservationItemSaveState>(OccupationState.Select(item => new TrainReservationItemSaveState(item.Key.Train.Number, item.Key.Direction)).ToList()),
                TrainReservation = TrainReserved != null ? new TrainReservationItemSaveState(TrainReserved.Train.Number, TrainReserved.Direction) : null,
                SignalReserved = SignalReserved,
                TrainPreReserved = new Collection<TrainReservationItemSaveState>(TrainPreReserved.Select(item => new TrainReservationItemSaveState(item.Train.Number, item.Direction)).ToList()),
                TrainClaimed = new Collection<TrainReservationItemSaveState>(TrainClaimed.Select(item => new TrainReservationItemSaveState(item.Train.Number, item.Direction)).ToList()),
                Forced = Forced,
            });
        }

        public ValueTask Restore(TrackCircuitStateSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            foreach (TrainReservationItemSaveState item in saveState.OccupationStates)
            {
                OccupationState.Add(new Train.TrainRouted(new Train(item.TrainNumber), item.Direction), item.Direction);
            }
            TrainReserved = saveState.TrainReservation != null ? new Train.TrainRouted(new Train(saveState.TrainReservation.Value.TrainNumber), saveState.TrainReservation.Value.Direction) : null;
            SignalReserved = saveState.SignalReserved;
            foreach (TrainReservationItemSaveState item in saveState.TrainPreReserved)
            {
                TrainPreReserved.Enqueue(new Train.TrainRouted(new Train(item.TrainNumber), item.Direction));
            }
            foreach (TrainReservationItemSaveState item in saveState.TrainClaimed)
            {
                TrainClaimed.Enqueue(new Train.TrainRouted(new Train(item.TrainNumber), item.Direction));
            }
            Forced = saveState.Forced;
            return ValueTask.CompletedTask;
        }

        //================================================================================================//
        /// <summary>
        /// Get list of trains occupying track
        /// Check without direction
        /// </summary>
        public List<Train.TrainRouted> TrainsOccupying()
        {
            return OccupationState.Keys.ToList();
        }

        //================================================================================================//
        /// <summary>
        /// Get list of trains occupying track
	    /// Check based on direction
        /// </summary>
        public List<Train.TrainRouted> TrainsOccupying(Direction direction)
        {
            return OccupationState.Where(state => state.Value == direction).Select(state => state.Key).ToList();
        }

        //================================================================================================//
        /// <summary>
        /// check if any trains occupy track
        /// Check without direction
        /// </summary>
        public bool Occupied()
        {
            return (OccupationState.Count > 0);
        }

        //================================================================================================//
        /// <summary>
        /// check if any trains occupy track
        /// Check based on direction
        /// </summary>
        public bool Occupied(Direction direction, bool stationary)
        {
            return OccupationState.Where(state => (state.Value == direction && state.Key.Train.SpeedMpS > 0.5f) || (stationary && state.Key.Train.SpeedMpS <= 0.5)).Any();
        }

        public bool Occupied(TrackDirection direction, bool stationary)
        {
            return Occupied((Direction)direction, stationary);
        }

        //================================================================================================//
        /// <summary>
        /// check if any trains occupy track
        /// Check for other train without direction
        /// </summary>
        public bool OccupiedByOtherTrains(Train.TrainRouted train)
        {
            return OccupationState.Count > 1 || (OccupationState.Count == 1 && !OccupationState.ContainsTrain(train));
        }

        //================================================================================================//
        /// <summary>
        /// check if any trains occupy track
        /// Check for other train based on direction
        /// </summary>
        public bool OccupiedByOtherTrains(Direction direction, bool stationary, Train.TrainRouted train)
        {
            return OccupationState.Where(state => (state.Key != train) && ((state.Value == direction && state.Key.Train.SpeedMpS > 0.5f) || (stationary && state.Key.Train.SpeedMpS <= 0.5))).Any();
        }

        //================================================================================================//
        /// <summary>
        /// check if this train occupies track
        /// routed train
        /// </summary>
        public bool OccupiedByThisTrain(Train.TrainRouted train)
        {
            return OccupationState.ContainsTrain(train);
        }

        //================================================================================================//
        /// <summary>
        /// check if this train occupies track
        /// unrouted train
        /// </summary>
        public bool OccupiedByThisTrain(Train train)
        {
            return OccupationState.ContainsTrain(train);
        }

    }

}
