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
using System.Diagnostics;
using System.IO;
using System.Linq;

using Orts.Common;
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
    public class TrackCircuitState
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
                Train.TrainRouted thisRouted = new Train.TrainRouted(thisTrain, trainRouteIndex);
                OccupationState.Add(thisRouted, trainDirection);
            }

            int trainReserved = inf.ReadInt32();
            if (trainReserved >= 0)
            {
                int trainRouteIndexR = inf.ReadInt32();
                Train thisTrain = new Train(trainReserved);
                Train.TrainRouted trainRoute = new Train.TrainRouted(thisTrain, trainRouteIndexR);
                TrainReserved = trainRoute;
            }

            SignalReserved = inf.ReadInt32();

            int noPreReserve = inf.ReadInt32();
            for (int trainNo = 0; trainNo < noPreReserve; trainNo++)
            {
                int trainNumber = inf.ReadInt32();
                int trainRouteIndex = inf.ReadInt32();
                Train thisTrain = new Train(trainNumber);
                Train.TrainRouted thisRouted = new Train.TrainRouted(thisTrain, trainRouteIndex);
                TrainPreReserved.Enqueue(thisRouted);
            }

            int noClaimed = inf.ReadInt32();
            for (int trainNo = 0; trainNo < noClaimed; trainNo++)
            {
                int trainNumber = inf.ReadInt32();
                int trainRouteIndex = inf.ReadInt32();
                Train thisTrain = new Train(trainNumber);
                Train.TrainRouted thisRouted = new Train.TrainRouted(thisTrain, trainRouteIndex);
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
            Dictionary<int[], int> tempTrains = new Dictionary<int[], int>();

            foreach (KeyValuePair<Train.TrainRouted, int> thisOccupy in OccupationState)
            {
                int[] trainKey = new int[2];
                trainKey[0] = thisOccupy.Key.Train.Number;
                trainKey[1] = thisOccupy.Key.TrainRouteDirectionIndex;
                int direction = thisOccupy.Value;
                tempTrains.Add(trainKey, direction);
            }

            OccupationState.Clear();

            foreach (KeyValuePair<int[], int> thisTemp in tempTrains)
            {
                int[] trainKey = thisTemp.Key;
                int number = trainKey[0];
                int routeIndex = trainKey[1];
                int direction = thisTemp.Value;
                Train thisTrain = SignalEnvironment.FindTrain(number, trains);
                if (thisTrain != null)
                {
                    Train.TrainRouted thisTrainRouted = routeIndex == 0 ? thisTrain.RoutedForward : thisTrain.RoutedBackward;
                    OccupationState.Add(thisTrainRouted, direction);
                }
            }

            // Reserved

            if (TrainReserved != null)
            {
                int number = TrainReserved.Train.Number;
                Train reservedTrain = SignalEnvironment.FindTrain(number, trains);
                if (reservedTrain != null)
                {
                    int reservedDirection = TrainReserved.TrainRouteDirectionIndex;
                    bool validreserve = true;

                    // check if reserved section is on train's route except when train is in explorer or manual mode
                    if (reservedTrain.ValidRoute[reservedDirection].Count > 0 && reservedTrain.ControlMode != TrainControlMode.Explorer && reservedTrain.ControlMode != TrainControlMode.Manual)
                    {
                        _ = reservedTrain.ValidRoute[reservedDirection].GetRouteIndex(sectionIndex, reservedTrain.PresentPosition[Direction.Forward].RouteListIndex);
                        validreserve = reservedTrain.ValidRoute[reservedDirection].GetRouteIndex(sectionIndex, reservedTrain.PresentPosition[Direction.Forward].RouteListIndex) >= 0;
                    }

                    if (validreserve || reservedTrain.ControlMode == TrainControlMode.Explorer)
                    {
                        TrainReserved = reservedDirection == 0 ? reservedTrain.RoutedForward : reservedTrain.RoutedBackward;
                    }
                    else
                    {
                        Trace.TraceInformation("Invalid reservation for train : {0} [{1}], section : {2} not restored", reservedTrain.Name, reservedDirection, sectionIndex);
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
                int routeIndex = trainRouted.TrainRouteDirectionIndex;
                if (train != null)
                {
                    TrainPreReserved.Enqueue(routeIndex == 0 ? train.RoutedForward : train.RoutedBackward);
                }
            }

            // Claimed
            queue = new Queue<Train.TrainRouted>(TrainClaimed);
            TrainClaimed.Clear();

            foreach (Train.TrainRouted trainRouted in queue)
            {
                Train train = SignalEnvironment.FindTrain(trainRouted.Train.Number, trains);
                int routeIndex = trainRouted.TrainRouteDirectionIndex;
                if (train != null)
                {
                    TrainClaimed.Enqueue(routeIndex == 0 ? train.RoutedForward : train.RoutedBackward);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Save
        /// </summary>
        public void Save(BinaryWriter outf)
        {
            ArgumentNullException.ThrowIfNull(outf);

            outf.Write(OccupationState.Count);
            foreach (KeyValuePair<Train.TrainRouted, int> thisOccupy in OccupationState)
            {
                Train.TrainRouted thisTrain = thisOccupy.Key;
                outf.Write(thisTrain.Train.Number);
                outf.Write(thisTrain.TrainRouteDirectionIndex);
                outf.Write(thisOccupy.Value);
            }

            if (TrainReserved == null)
            {
                outf.Write(-1);
            }
            else
            {
                outf.Write(TrainReserved.Train.Number);
                outf.Write(TrainReserved.TrainRouteDirectionIndex);
            }

            outf.Write(SignalReserved);

            outf.Write(TrainPreReserved.Count);
            foreach (Train.TrainRouted thisTrain in TrainPreReserved)
            {
                outf.Write(thisTrain.Train.Number);
                outf.Write(thisTrain.TrainRouteDirectionIndex);
            }

            outf.Write(TrainClaimed.Count);
            foreach (Train.TrainRouted thisTrain in TrainClaimed)
            {
                outf.Write(thisTrain.Train.Number);
                outf.Write(thisTrain.TrainRouteDirectionIndex);
            }

            outf.Write(Forced);

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
        public List<Train.TrainRouted> TrainsOccupying(int direction)
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
        public bool Occupied(int direction, bool stationary)
        {
            return OccupationState.Where(state => (state.Value == direction && state.Key.Train.SpeedMpS > 0.5f) || (stationary && state.Key.Train.SpeedMpS <= 0.5)).Any();
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
        public bool OccupiedByOtherTrains(int direction, bool stationary, Train.TrainRouted train)
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
