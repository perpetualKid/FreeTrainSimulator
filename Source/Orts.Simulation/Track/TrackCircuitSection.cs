﻿// COPYRIGHT 2013 by the Open Rails project.
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

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.MultiPlayer;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Track
{

    //================================================================================================//
    /// <summary>
    ///
    /// class TrackCircuitSection
    /// Class for track circuit and train control
    ///
    /// </summary>
    //================================================================================================//

    public class TrackCircuitSection
    {
        public static TrackCircuitSection Invalid { get; } = new TrackCircuitSection(-1);

        // Properties Index, Length and OffsetLength come from TrackCircuitSectionXref

        public static List<TrackCircuitSection> TrackCircuitList { get; } = new List<TrackCircuitSection>();

        private static SignalEnvironment signals;                                                 // reference to Signals class             //

        public int Index { get; private set; }                                          // Index of TCS                           //
        public float Length { get; private set; }                                       // Length of Section                      //
        public EnumArray<float, Location> OffsetLength { get; private set; } = new EnumArray<float, Location>(); // Offset length in original tracknode    //
        public int OriginalIndex { get; private set; }                                  // original TDB section index             //
        public TrackCircuitType CircuitType { get; private set; }                       // type of section                        //

        public EnumArray2D<TrackPin, TrackDirection, Location> Pins { get; } = new EnumArray2D<TrackPin, TrackDirection, Location>(TrackPin.Empty);                   // next sections                          //
        public EnumArray2D<TrackPin, TrackDirection, Location> ActivePins { get; } = new EnumArray2D<TrackPin, TrackDirection, Location>(TrackPin.Empty);// active next sections                   //

        public int JunctionDefaultRoute { get; private set; } = -1;                     // jn default route, value is out-pin      //
        public int JunctionLastRoute { get; internal set; } = -1;                       // jn last route, value is out-pin         //
        public int JunctionSetManual { get; internal set; } = -1;                       // jn set manual, value is out-pin         //
        public List<int> LinkedSignals { get; internal set; }                           // switchstands linked with this switch    //
        public List<int> SignalsPassingRoutes { get; private set; }                     // list of signals reading passed junction //

        public EnumArray<Signal, TrackDirection> EndSignals { get; private set; } = new EnumArray<Signal, TrackDirection>();   // signals at either end      //

        public double Overlap { get; private set; }                                     // overlap for junction nodes //
        public List<int> PlatformIndices { get; } = new List<int>();                    // platforms along section    //

        internal TrackCircuitItems CircuitItems { get; set; }                    // all items                  //
        public TrackCircuitState CircuitState { get; internal set; }                    // normal states              //

        // old style deadlock definitions
        public Dictionary<int, List<int>> DeadlockTraps { get; private set; }           // deadlock traps             //
        public List<int> DeadlockActives { get; private set; }                          // list of trains with active deadlock traps //
        public List<int> DeadlockAwaited { get; private set; }                          // train is waiting for deadlock to clear //

        // new style deadlock definitions
        public int DeadlockReference { get; internal set; }                             // index of deadlock to related deadlockinfo object for boundary //
        public Dictionary<int, int> DeadlockBoundaries { get; internal set; }           // list of boundaries and path index to boundary for within deadlock //

        internal List<TunnelInfoData> TunnelInfo { get; private set; }                    // full tunnel info data

        // trough data
        internal List<TroughInfoData> TroughInfo { get; private set; }                    // full trough info data

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>
        public TrackCircuitSection(TrackNode node, int nodeIndex, TrackSectionsFile tsectiondat)
        {
            if (null == node)
                throw new ArgumentNullException(nameof(node));
            if (null == tsectiondat)
                throw new ArgumentNullException(nameof(tsectiondat));
            //
            // Copy general info
            //
            Index = nodeIndex;
            OriginalIndex = nodeIndex;

            switch (node)
            {
                case TrackEndNode _:
                    CircuitType = TrackCircuitType.EndOfTrack;
                    break;
                case TrackJunctionNode _:
                    CircuitType = TrackCircuitType.Junction;
                    break;
                default:
                    CircuitType = TrackCircuitType.Normal;
                    break;
            }

            int PinNo = 0;
            for (int pin = 0; pin < Math.Min(node.InPins, EnumExtension.GetLength<Location>()); pin++)
            {
                Pins[TrackDirection.Ahead, (Location)pin] = node.TrackPins[PinNo];
                PinNo++;
            }
            if (PinNo < node.InPins)
                PinNo = node.InPins;
            for (int pin = 0; pin < Math.Min(node.OutPins, EnumExtension.GetLength<Location>()); pin++)
            {
                Pins[TrackDirection.Reverse, (Location)pin] = node.TrackPins[PinNo];
                PinNo++;
            }

            //
            // Preset length and offset
            // If section index not in tsectiondat, length is 0.
            //
            if (node is TrackVectorNode tvn && tvn.TrackVectorSections != null)
            {
                foreach (TrackVectorSection section in tvn.TrackVectorSections)
                {
                    if (tsectiondat.TrackSections.TryGetValue(section.SectionIndex, out TrackSection trackSection))
                    {
                        Length += trackSection.Curved ? MathHelper.ToRadians(Math.Abs(trackSection.Angle)) * trackSection.Radius : trackSection.Length;
                    }
                }
            }

            // for Junction nodes, obtain default route
            // set switch to default route
            // copy overlap (if set)
            if (CircuitType == TrackCircuitType.Junction)
            {
                SignalsPassingRoutes = new List<int>();
                uint trackShapeIndex = (node as TrackJunctionNode).ShapeIndex;
                if (!tsectiondat.TrackShapes.TryGetValue(trackShapeIndex, out TrackShape trackShape))
                {
                    Trace.TraceWarning("Missing TrackShape in tsection.dat : " + trackShapeIndex);
                }
                JunctionDefaultRoute = (int)trackShape.MainRoute;
                Overlap = trackShape.ClearanceDistance;

                JunctionLastRoute = JunctionDefaultRoute;
                signals.SetSwitch(OriginalIndex, JunctionLastRoute, this);
            }

            //
            // Create circuit items
            //
            CircuitItems = new TrackCircuitItems(signals.OrtsSignalTypeCount);
            CircuitState = new TrackCircuitState();

            DeadlockTraps = new Dictionary<int, List<int>>();
            DeadlockActives = new List<int>();
            DeadlockAwaited = new List<int>();

            DeadlockReference = -1;
            DeadlockBoundaries = null;
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for empty entries
        /// </summary>
        internal TrackCircuitSection(SignalEnvironment signals) :
            this(0)
        {
            if (null != TrackCircuitSection.signals)
                throw new InvalidOperationException(nameof(TrackCircuitSection.signals));
            TrackCircuitSection.signals = signals;

            CircuitItems = new TrackCircuitItems(signals.OrtsSignalTypeCount);
        }

        public TrackCircuitSection(int nodeIndex)
        {

            Index = nodeIndex;
            OriginalIndex = -1;
            CircuitType = TrackCircuitType.Empty;

            if (null != signals) //signals is still null for ctor call from default/dummy element
                CircuitItems = new TrackCircuitItems(signals.OrtsSignalTypeCount);
            CircuitState = new TrackCircuitState();

            DeadlockTraps = new Dictionary<int, List<int>>();
            DeadlockActives = new List<int>();
            DeadlockAwaited = new List<int>();

            DeadlockReference = -1;
            DeadlockBoundaries = null;
        }

        //================================================================================================//
        /// <summary>
        /// Restore
        /// </summary>

        public void Restore(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            ActivePins[TrackDirection.Ahead, Location.NearEnd] = new TrackPin(inf.ReadInt32(), (TrackDirection)inf.ReadInt32());
            ActivePins[TrackDirection.Reverse, Location.NearEnd] = new TrackPin(inf.ReadInt32(), (TrackDirection)inf.ReadInt32());
            ActivePins[TrackDirection.Ahead, Location.FarEnd] = new TrackPin(inf.ReadInt32(), (TrackDirection)inf.ReadInt32());
            ActivePins[TrackDirection.Reverse, Location.FarEnd] = new TrackPin(inf.ReadInt32(), (TrackDirection)inf.ReadInt32());

            JunctionSetManual = inf.ReadInt32();
            JunctionLastRoute = inf.ReadInt32();

            CircuitState.Restore(inf);

            // if physical junction, throw switch
            if (CircuitType == TrackCircuitType.Junction)
            {
                signals.SetSwitch(OriginalIndex, JunctionLastRoute, this);
            }

            int deadlockTrapsCount = inf.ReadInt32();
            for (int iDeadlock = 0; iDeadlock < deadlockTrapsCount; iDeadlock++)
            {
                int deadlockKey = inf.ReadInt32();
                int deadlockListCount = inf.ReadInt32();
                List<int> deadlockList = new List<int>();

                for (int iDeadlockInfo = 0; iDeadlockInfo < deadlockListCount; iDeadlockInfo++)
                {
                    int deadlockDetail = inf.ReadInt32();
                    deadlockList.Add(deadlockDetail);
                }
                DeadlockTraps.Add(deadlockKey, deadlockList);
            }

            int deadlockActivesCount = inf.ReadInt32();
            for (int iDeadlockActive = 0; iDeadlockActive < deadlockActivesCount; iDeadlockActive++)
            {
                int deadlockActiveDetails = inf.ReadInt32();
                DeadlockActives.Add(deadlockActiveDetails);
            }

            int deadlockWaitCount = inf.ReadInt32();
            for (int iDeadlockWait = 0; iDeadlockWait < deadlockWaitCount; iDeadlockWait++)
            {
                int deadlockWaitDetails = inf.ReadInt32();
                DeadlockAwaited.Add(deadlockWaitDetails);
            }

            DeadlockReference = inf.ReadInt32();

            DeadlockBoundaries = null;
            int deadlockBoundariesAvailable = inf.ReadInt32();
            if (deadlockBoundariesAvailable > 0)
            {
                DeadlockBoundaries = new Dictionary<int, int>();
                for (int iInfo = 0; iInfo <= deadlockBoundariesAvailable - 1; iInfo++)
                {
                    int boundaryInfo = inf.ReadInt32();
                    int pathInfo = inf.ReadInt32();
                    DeadlockBoundaries.Add(boundaryInfo, pathInfo);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Save
        /// </summary>

        public void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));

            outf.Write(ActivePins[TrackDirection.Ahead, Location.NearEnd].Link);
            outf.Write((int)ActivePins[TrackDirection.Ahead, Location.NearEnd].Direction);
            outf.Write(ActivePins[TrackDirection.Reverse, Location.NearEnd].Link);
            outf.Write((int)ActivePins[TrackDirection.Reverse, Location.NearEnd].Direction);
            outf.Write(ActivePins[TrackDirection.Ahead, Location.FarEnd].Link);
            outf.Write((int)ActivePins[TrackDirection.Ahead, Location.FarEnd].Direction);
            outf.Write(ActivePins[TrackDirection.Reverse, Location.FarEnd].Link);
            outf.Write((int)ActivePins[TrackDirection.Reverse, Location.FarEnd].Direction);

            outf.Write(JunctionSetManual);
            outf.Write(JunctionLastRoute);

            CircuitState.Save(outf);

            outf.Write(DeadlockTraps.Count);
            foreach (KeyValuePair<int, List<int>> thisTrap in DeadlockTraps)
            {
                outf.Write(thisTrap.Key);
                outf.Write(thisTrap.Value.Count);

                foreach (int thisDeadlockRef in thisTrap.Value)
                {
                    outf.Write(thisDeadlockRef);
                }
            }

            outf.Write(DeadlockActives.Count);
            foreach (int thisDeadlockActive in DeadlockActives)
            {
                outf.Write(thisDeadlockActive);
            }

            outf.Write(DeadlockAwaited.Count);
            foreach (int thisDeadlockWait in DeadlockAwaited)
            {
                outf.Write(thisDeadlockWait);
            }

            outf.Write(DeadlockReference);

            if (DeadlockBoundaries == null)
            {
                outf.Write(-1);
            }
            else
            {
                outf.Write(DeadlockBoundaries.Count);
                foreach (KeyValuePair<int, int> thisInfo in DeadlockBoundaries)
                {
                    outf.Write(thisInfo.Key);
                    outf.Write(thisInfo.Value);
                }
            }
        }

        internal void AddTunnelData(TunnelInfoData tunnelData)
        {
            if (null == TunnelInfo)
                TunnelInfo = new List<TunnelInfoData>();
            TunnelInfo.Add(tunnelData);
        }


        internal void AddTroughData(TroughInfoData troughData)
        {
            if (null == TroughInfo)
                TroughInfo = new List<TroughInfoData>();
            TroughInfo.Add(troughData);
        }


        //================================================================================================//
        /// <summary>
        /// Copy basic info only
        /// </summary>

        private TrackCircuitSection CopyFrom(int targetIndex)
        {
            TrackCircuitSection newSection = new TrackCircuitSection(targetIndex)
            {
                OriginalIndex = OriginalIndex,
                CircuitType = TrackCircuitType.Normal// CircuitType;
            };

            newSection.EndSignals[TrackDirection.Ahead] = EndSignals[TrackDirection.Ahead];
            newSection.EndSignals[TrackDirection.Reverse] = EndSignals[TrackDirection.Reverse];

            newSection.Length = Length;

            newSection.OffsetLength = new EnumArray<float, Location>(OffsetLength);

            return newSection;
        }

        //================================================================================================//
        /// <summary>
        /// Check if set for train
        /// </summary>
        public bool IsSet(Train.TrainRouted train, bool validClaim)   // using routed train
        {
            if (null == train)
                return false;

            // return true if train in this section or there is a reservation already for this train
            if (CircuitState.OccupiedByThisTrain(train) || CircuitState.TrainReserved?.Train == train.Train)
            {
                return true;
            }

            // check claim if claim is valid as state
            return (validClaim && CircuitState.TrainClaimed.PeekTrain() == train.Train);
            // section is not yet set for this train
        }

        public bool IsSet(Train train, bool validClaim)    // using unrouted train
        {
            return train != null && (IsSet(train.routedForward, validClaim) || IsSet(train.routedBackward, validClaim));
        }

        //================================================================================================//
        /// <summary>
        /// Check available state for train
        /// </summary>
        public bool IsAvailable(Train.TrainRouted train)    // using routed train
        {
            if (train == null)
                return false;

            // if train in this section, return true; if other train in this section, return false
            // check if train is in section in expected direction - otherwise return false
            if (CircuitState.OccupiedByThisTrain(train))
            {
                return (true);
            }

            if (CircuitState.OccupiedByOtherTrains(train))
            {
                return (false);
            }

            // check reservation
            if (CircuitState.TrainReserved?.Train == train.Train)
            {
                return (true);
            }

            if (!Simulator.Instance.TimetableMode && train.Train.TrainType == TrainType.AiNotStarted &&
                CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train != train.Train)
            {
                ClearSectionsOfTrainBehind(CircuitState.TrainReserved, this);
            }
            else if (train.Train.IsPlayerDriven && train.Train.ControlMode != TrainControlMode.Manual && train.Train.DistanceTravelledM == 0.0 &&
                     train.Train.TCRoute != null && train.Train.ValidRoute[0] != null && train.Train.TCRoute.ActiveSubPath == 0) // We are at initial placement
                                                                                                                                 // Check if section is under train, and therefore can be unreserved from other trains
            {
                int routeIndex = train.Train.ValidRoute[0].GetRouteIndex(Index, 0);
                if (((routeIndex <= train.Train.PresentPosition[Direction.Forward].RouteListIndex && Index >= train.Train.PresentPosition[Direction.Backward].RouteListIndex) ||
                    (routeIndex >= train.Train.PresentPosition[Direction.Forward].RouteListIndex && Index <= train.Train.PresentPosition[Direction.Backward].RouteListIndex)) &&
                    CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train != train.Train)
                {
                    Train.TrainRouted trainRouted = CircuitState.TrainReserved;
                    ClearSectionsOfTrainBehind(trainRouted, this);
                    if (trainRouted.Train.TrainType == TrainType.Ai || trainRouted.Train.TrainType == TrainType.AiPlayerHosting)
                        ((AITrain)trainRouted.Train).ResetActions(true);
                }
            }
            else if (CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train != train.Train)
                return false;

            // check signal reservation

            if (CircuitState.SignalReserved >= 0)
            {
                return (false);
            }

            // check claim
            if (CircuitState.TrainClaimed.Count > 0)
            {
                return (CircuitState.TrainClaimed.PeekTrain() == train.Train);
            }

            // check deadlock trap
            if (DeadlockTraps.ContainsKey(train.Train.Number))
            {
                if (!DeadlockAwaited.Contains(train.Train.Number))
                    DeadlockAwaited.Add(train.Train.Number); // train is waiting for deadlock to clear
                return (false);
            }

            // check deadlock is in use - only if train has valid route
            if (train.Train.ValidRoute[train.TrainRouteDirectionIndex] != null)
            {

                int routeElementIndex = train.Train.ValidRoute[train.TrainRouteDirectionIndex].GetRouteIndex(Index, 0);
                if (routeElementIndex >= 0)
                {
                    TrackCircuitRouteElement thisElement = train.Train.ValidRoute[train.TrainRouteDirectionIndex][routeElementIndex];

                    // check for deadlock awaited at end of passing loop - path based deadlock processing
                    if (!signals.UseLocationPassingPaths)
                    {
                        // if deadlock is allready awaited set available to false to keep one track open
                        if (thisElement.StartAlternativePath != null)
                        {
                            if (thisElement.StartAlternativePath.TrackCircuitSection.CheckDeadlockAwaited(train.Train.Number))
                            {
                                return (false);
                            }
                        }
                    }
                    // check on available paths through deadlock area - location based deadlock processing
                    else
                    {
                        if (DeadlockReference >= 0 && thisElement.FacingPoint)
                        {
                            DeadlockInfo sectionDeadlockInfo = signals.DeadlockInfoList[DeadlockReference];
                            List<int> pathAvail = sectionDeadlockInfo.CheckDeadlockPathAvailability(this, train.Train);
                            if (pathAvail.Count <= 0)
                                return (false);
                        }
                    }
                }
            }

            // section is clear
            return (true);
        }

        public bool IsAvailable(Train train)    // using unrouted train
        {
            return train != null && (IsAvailable(train.routedForward) || IsAvailable(train.routedBackward));
        }

        //================================================================================================//
        /// <summary>
        /// Reserve : set reserve state
        /// </summary>
        public void Reserve(Train.TrainRouted train, TrackCircuitPartialPathRoute route)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            TrackCircuitRouteElement routeElement;

            if (!CircuitState.OccupiedByThisTrain(train.Train))
            {
                // check if not beyond trains route

                bool validPosition = true;

                // try from rear of train
                if (train.Train.PresentPosition[Direction.Backward].RouteListIndex > 0)
                {
                    validPosition = train.Train.ValidRoute[0].GetRouteIndex(Index, train.Train.PresentPosition[Direction.Backward].RouteListIndex) >= 0;
                }
                // if not possible try from front
                else if (train.Train.PresentPosition[Direction.Forward].RouteListIndex > 0)
                {
                    validPosition = train.Train.ValidRoute[0].GetRouteIndex(Index, train.Train.PresentPosition[Direction.Forward].RouteListIndex) >= 0;
                }

                if (validPosition)
                {
                    CircuitState.TrainReserved = train;
                }

                // remove from claim or deadlock claim
                CircuitState.TrainClaimed.Remove(train);

                // get element in routepath to find required alignment
                int currentIndex = -1;

                for (int i = 0; i < route.Count && currentIndex < 0; i++)
                {
                    routeElement = route[i];
                    if (routeElement.TrackCircuitSection.Index == Index)
                    {
                        currentIndex = i;
                    }
                }

                // if junction or crossover, align pins
                // also reset manual set (path will have followed setting)
                if (CircuitType == TrackCircuitType.Junction || CircuitType == TrackCircuitType.Crossover)
                {
                    if (CircuitState.Forced == false)
                    {
                        // set active pins for leading section

                        JunctionSetManual = -1;  // reset manual setting (will have been honoured in route definition if applicable)
                        if (currentIndex > 0)
                        {
                            routeElement = route[currentIndex - 1];
                            AlignSwitchPins(routeElement.TrackCircuitSection.Index);
                        }

                        if (currentIndex <= route.Count - 2)
                        {
                            routeElement = route[currentIndex + 1];
                            // set active pins for trailing section

                            AlignSwitchPins(routeElement.TrackCircuitSection.Index);
                        }

                        // reset signals which routed through this junction

                        foreach (int thisSignalIndex in SignalsPassingRoutes)
                        {
                            Signal thisSignal = signals.Signals[thisSignalIndex];
                            thisSignal.ResetRoute(Index);
                        }
                        SignalsPassingRoutes.Clear();
                    }
                }

                // enable all signals along section in direction of train
                // do not enable those signals who are part of NORMAL signal

                if (currentIndex < 0) return; //Added by JTang
                routeElement = route[currentIndex];
                TrackDirection direction = (TrackDirection)routeElement.Direction;

                for (int fntype = 0; fntype < signals.OrtsSignalTypeCount; fntype++)
                {
                    foreach (TrackCircuitSignalItem item in CircuitItems.TrackCircuitSignals[direction][fntype])
                    {
                        Signal thisSignal = item.Signal;
                        if (!thisSignal.SignalNormal())
                        {
                            thisSignal.EnabledTrain = train;
                        }
                    }
                }

                // also set enabled for speedpost to process speed signals
                foreach (TrackCircuitSignalItem item in CircuitItems.TrackCircuitSpeedPosts[direction])
                {
                    if (!item.Signal.SignalNormal())
                    {
                        item.Signal.EnabledTrain = train;
                    }
                }

                // set deadlock trap if required - do not set deadlock if wait is required at this location
                if (train.Train.DeadlockInfo.ContainsKey(Index))
                {
                    if (!train.Train.CheckWaitCondition(Index))
                    {
                        SetDeadlockTrap(train.Train, train.Train.DeadlockInfo[Index]);
                    }
                }

                // if start of alternative route, set deadlock keys for other end
                // check using path based deadlock processing
                if (!signals.UseLocationPassingPaths)
                {
                    if (routeElement?.StartAlternativePath != null)
                    {
                        TrackCircuitSection endSection = routeElement.StartAlternativePath.TrackCircuitSection;

                        // no deadlock yet active
                        if (train.Train.DeadlockInfo.ContainsKey(endSection.Index))
                        {
                            endSection.SetDeadlockTrap(train.Train, train.Train.DeadlockInfo[endSection.Index]);
                        }
                        else if (endSection.DeadlockTraps.ContainsKey(train.Train.Number) && !endSection.DeadlockAwaited.Contains(train.Train.Number))
                        {
                            endSection.DeadlockAwaited.Add(train.Train.Number);
                        }
                    }
                }
                // search for path using location based deadlock processing
                else
                {
                    if (routeElement != null && routeElement.FacingPoint && DeadlockReference >= 0)
                    {
                        DeadlockInfo sectionDeadlockInfo = signals.DeadlockInfoList[DeadlockReference];
                        if (sectionDeadlockInfo.HasTrainAndSubpathIndex(train.Train.Number, train.Train.TCRoute.ActiveSubPath))
                        {
                            int trainAndSubpathIndex = sectionDeadlockInfo.GetTrainAndSubpathIndex(train.Train.Number, train.Train.TCRoute.ActiveSubPath);
                            int availableRoute = sectionDeadlockInfo.TrainReferences[trainAndSubpathIndex][0];
                            int endSectionIndex = sectionDeadlockInfo.AvailablePathList[availableRoute].EndSectionIndex;
                            TrackCircuitSection endSection = TrackCircuitList[endSectionIndex];

                            // no deadlock yet active - do not set deadlock if train has wait within deadlock section
                            if (train.Train.DeadlockInfo.ContainsKey(endSection.Index))
                            {
                                if (!train.Train.HasActiveWait(Index, endSection.Index))
                                {
                                    endSection.SetDeadlockTrap(train.Train, train.Train.DeadlockInfo[endSection.Index]);
                                }
                            }
                            else if (endSection.DeadlockTraps.ContainsKey(train.Train.Number) && !endSection.DeadlockAwaited.Contains(train.Train.Number))
                            {
                                endSection.DeadlockAwaited.Add(train.Train.Number);
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// insert Claim
        /// </summary>
        public void Claim(Train.TrainRouted train)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            if (!CircuitState.TrainClaimed.ContainsTrain(train))
            {
                CircuitState.TrainClaimed.Enqueue(train);
            }

            // set deadlock trap if required
            if (train.Train.DeadlockInfo.ContainsKey(Index))
            {
                SetDeadlockTrap(train.Train, train.Train.DeadlockInfo[Index]);
            }
        }

        //================================================================================================//
        /// <summary>
        /// insert pre-reserve
        /// </summary>
        public void PreReserve(Train.TrainRouted train)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));
            if (!CircuitState.TrainPreReserved.ContainsTrain(train))
            {
                CircuitState.TrainPreReserved.Enqueue(train);
            }
        }

        //================================================================================================//
        /// <summary>
        /// set track occupied
        /// </summary>
        public void SetOccupied(Train.TrainRouted train)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            SetOccupied(train, (int)train.Train.DistanceTravelledM);
        }

        public void SetOccupied(Train.TrainRouted train, int reqDistanceTravelledM)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            int routeIndex = train.Train.ValidRoute[train.TrainRouteDirectionIndex].GetRouteIndex(Index, train.Train.PresentPosition[train.TrainRouteDirectionIndex == 0 ? Direction.Backward : Direction.Forward].RouteListIndex);
            TrackDirection direction = routeIndex < 0 ? TrackDirection.Ahead : (TrackDirection)train.Train.ValidRoute[train.TrainRouteDirectionIndex][routeIndex].Direction;
            CircuitState.OccupationState.Add(train, (int)direction);
            CircuitState.Forced = false;
            train.Train.OccupiedTrack.Add(this);

            // clear all reservations
            CircuitState.TrainReserved = null;
            CircuitState.SignalReserved = -1;

            CircuitState.TrainClaimed.Remove(train);
            CircuitState.TrainPreReserved.Remove(train);

            float distanceToClear = reqDistanceTravelledM + Length + train.Train.standardOverlapM;

            // add to clear list of train

            if (CircuitType == TrackCircuitType.Junction)
            {
                if (Pins[direction, Location.FarEnd].Link >= 0)  // facing point
                {
                    if (Overlap > 0)
                    {
                        distanceToClear = reqDistanceTravelledM + Length + Convert.ToSingle(Overlap);
                    }
                    else
                    {
                        distanceToClear = reqDistanceTravelledM + Length + train.Train.junctionOverlapM;
                    }
                }
                else
                {
                    distanceToClear = reqDistanceTravelledM + Length + train.Train.standardOverlapM;
                }
            }

            else if (CircuitType == TrackCircuitType.Crossover)
            {
                if (Overlap > 0)
                {
                    distanceToClear = reqDistanceTravelledM + Length + Convert.ToSingle(Overlap);
                }
                else
                {
                    distanceToClear = reqDistanceTravelledM + Length + train.Train.junctionOverlapM;
                }
            }

            TrackCircuitPosition presentFront = train.Train.PresentPosition[train.Direction];
            Direction reverseDirectionIndex = train.TrainRouteDirectionIndex == 0 ? Direction.Backward : Direction.Forward;
            TrackCircuitPosition presentRear = train.Train.PresentPosition[reverseDirectionIndex];

            // correct offset if position direction is not equal to route direction
            float frontOffset = presentFront.Offset;
            if (presentFront.RouteListIndex >= 0 &&
                presentFront.Direction != train.Train.ValidRoute[train.TrainRouteDirectionIndex][presentFront.RouteListIndex].Direction)
                frontOffset = Length - frontOffset;

            float rearOffset = presentRear.Offset;
            if (presentRear.RouteListIndex >= 0 &&
                presentRear.Direction != train.Train.ValidRoute[train.TrainRouteDirectionIndex][presentRear.RouteListIndex].Direction)
                rearOffset = Length - rearOffset;

            if (presentFront.TrackCircuitSectionIndex == Index)
            {
                distanceToClear += train.Train.Length - frontOffset;
            }
            else if (presentRear.TrackCircuitSectionIndex == Index)
            {
                distanceToClear -= rearOffset;
            }
            else
            {
                distanceToClear += train.Train.Length;
            }

            // make sure items are cleared in correct sequence
            float? lastDistance = train.Train.requiredActions.GetLastClearingDistance();
            if (lastDistance.HasValue && lastDistance > distanceToClear)
            {
                distanceToClear = lastDistance.Value;
            }

            train.Train.requiredActions.InsertAction(new Train.ClearSectionItem(distanceToClear, Index));

            // set deadlock trap if required

            if (train.Train.DeadlockInfo.ContainsKey(Index))
            {
                SetDeadlockTrap(train.Train, train.Train.DeadlockInfo[Index]);
            }

            // check for deadlock trap if taking alternative path

            if (train.Train.TCRoute != null && train.Train.TCRoute.ActiveAlternativePath >= 0)
            {
                TrackCircuitPartialPathRoute altRoute = train.Train.TCRoute.TCAlternativePaths[train.Train.TCRoute.ActiveAlternativePath];
                TrackCircuitRouteElement startElement = altRoute[0];
                if (Index == startElement.TrackCircuitSection.Index)
                {
                    TrackCircuitSection endSection = altRoute[altRoute.Count - 1].TrackCircuitSection;

                    // set deadlock trap for next section

                    if (train.Train.DeadlockInfo.ContainsKey(endSection.Index))
                    {
                        endSection.SetDeadlockTrap(train.Train, train.Train.DeadlockInfo[endSection.Index]);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// clear track occupied
        /// </summary>

        /// <summary>
        /// routed train
        /// </summary>
        public void ClearOccupied(Train.TrainRouted train, bool resetEndSignal)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            if (CircuitState.OccupationState.ContainsTrain(train))
            {
                CircuitState.OccupationState.RemoveTrain(train);
                train.Train.OccupiedTrack.Remove(this);
            }

            RemoveTrain(train, false);   // clear occupy first to prevent loop, next clear all hanging references

            ClearDeadlockTrap(train.Train.Number); // clear deadlock traps

            // if signal at either end is still enabled for this train, reset the signal
            foreach (TrackDirection heading in EnumExtension.GetValues<TrackDirection>())
            {
                if (EndSignals[heading]?.EnabledTrain == train && resetEndSignal)
                {
                    EndSignals[heading].ResetSignalEnabled();
                }

                // disable all signals along section if enabled for this train

                for (int fntype = 0; fntype < signals.OrtsSignalTypeCount; fntype++)
                {
                    TrackCircuitSignalList thisSignalList = CircuitItems.TrackCircuitSignals[heading][fntype];
                    foreach (TrackCircuitSignalItem thisItem in thisSignalList)
                    {
                        Signal thisSignal = thisItem.Signal;
                        if (thisSignal.EnabledTrain == train)
                        {
                            thisSignal.ResetSignalEnabled();
                        }
                    }
                }

                // also reset enabled for speedpost to process speed signals
                TrackCircuitSignalList thisSpeedpostList = CircuitItems.TrackCircuitSpeedPosts[heading];
                foreach (TrackCircuitSignalItem thisItem in thisSpeedpostList)
                {
                    Signal thisSpeedpost = thisItem.Signal;
                    if (!thisSpeedpost.SignalNormal())
                    {
                        thisSpeedpost.ResetSignalEnabled();
                    }
                }
            }

            // if section is Junction or Crossover, reset active pins but only if section is not occupied by other train

            if ((CircuitType == TrackCircuitType.Junction || CircuitType == TrackCircuitType.Crossover) && CircuitState.OccupationState.Count == 0)
            {
                DeAlignSwitchPins();

                // reset signals which routed through this junction

                foreach (int thisSignalIndex in SignalsPassingRoutes)
                {
                    Signal thisSignal = signals.Signals[thisSignalIndex];
                    thisSignal.ResetRoute(Index);
                }
                SignalsPassingRoutes.Clear();
            }

            // reset manual junction setting if train is in manual mode

            if (train.Train.ControlMode == TrainControlMode.Manual && CircuitType == TrackCircuitType.Junction && JunctionSetManual >= 0)
            {
                JunctionSetManual = -1;
            }

            // if no longer occupied and pre-reserved not empty, promote first entry of prereserved

            if (CircuitState.OccupationState.Count <= 0 && CircuitState.TrainPreReserved.Count > 0)
            {
                Train.TrainRouted nextTrain = CircuitState.TrainPreReserved.Dequeue();
                TrackCircuitPartialPathRoute RoutePart = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex];

                Reserve(nextTrain, RoutePart);
            }

        }

        /// <summary>
        /// unrouted train
        /// </summary>
        public void ClearOccupied(Train train, bool resetEndSignal)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            ClearOccupied(train.routedForward, resetEndSignal); // forward
            ClearOccupied(train.routedBackward, resetEndSignal);// backward
        }

        /// <summary>
        /// only reset occupied state - use in case of reversal or mode change when train has not actually moved
        /// routed train
        /// </summary>
        public void ResetOccupied(Train.TrainRouted train)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            if (CircuitState.OccupationState.ContainsTrain(train))
            {
                CircuitState.OccupationState.RemoveTrain(train);
                train.Train.OccupiedTrack.Remove(this);
            }

        }

        /// <summary>
        /// unrouted train
        /// </summary>
        public void ResetOccupied(Train train)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            ResetOccupied(train.routedForward); // forward
            ResetOccupied(train.routedBackward);// backward
        }

        //================================================================================================//
        /// <summary>
        /// Remove train from section
        /// </summary>

        /// <summary>
        /// routed train
        /// </summary>
        public void RemoveTrain(Train.TrainRouted train, bool resetEndSignal)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            if (CircuitState.OccupiedByThisTrain(train))
            {
                ClearOccupied(train, resetEndSignal);
            }

            if (CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train == train.Train)
            {
                CircuitState.TrainReserved = null;
                ClearOccupied(train, resetEndSignal);    // call clear occupy to reset signals and switches //
            }

            CircuitState.TrainClaimed.Remove(train);
            CircuitState.TrainPreReserved.Remove(train);
        }


        /// <summary>
        /// unrouted train
        /// </summary>
        public void RemoveTrain(Train train, bool resetEndSignal)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            RemoveTrain(train.routedForward, resetEndSignal);
            RemoveTrain(train.routedBackward, resetEndSignal);
        }

        //================================================================================================//
        /// <summary>
        /// Remove train reservations from section
        /// </summary>
        public void UnreserveTrain(Train.TrainRouted train, bool resetEndSignal)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            if (CircuitState.TrainReserved?.Train == train.Train)
            {
                CircuitState.TrainReserved = null;
                ClearOccupied(train, resetEndSignal);    // call clear occupy to reset signals and switches //
            }

            CircuitState.TrainClaimed.Remove(train);
            CircuitState.TrainPreReserved.Remove(train);
        }

        //================================================================================================//
        /// <summary>
        /// Remove train clain from section
        /// </summary>
        public void UnclaimTrain(Train.TrainRouted train)
        {
            CircuitState.TrainClaimed.Remove(train);
        }

        //================================================================================================//
        /// <summary>
        /// Remove all reservations from section if signal not enabled for train
        /// </summary>
        public void Unreserve()
        {
            CircuitState.SignalReserved = -1;
        }

        //================================================================================================//
        /// <summary>
        /// Remove reservation of train
        /// </summary>
        public void UnreserveTrain()
        {
            CircuitState.TrainReserved = null;
        }

        //================================================================================================//
        /// <summary>
        /// Remove claims from sections for reversed trains
        /// </summary>

        public void ClearReversalClaims(Train.TrainRouted train)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            // check if any trains have claimed this section
            List<Train.TrainRouted> claimedTrains = new List<Train.TrainRouted>(CircuitState.TrainClaimed);

            CircuitState.TrainClaimed.Clear();
            foreach (Train.TrainRouted claimingTrain in claimedTrains)
            {
                claimingTrain.Train.ClaimState = false; // reset train claim state
            }

            // get train route
            TrackCircuitPartialPathRoute usedRoute = train.Train.ValidRoute[train.TrainRouteDirectionIndex];
            int routeIndex = usedRoute.GetRouteIndex(Index, 0);

            // run down route and clear all claims for found trains, until end 
            for (int iRouteIndex = routeIndex + 1; iRouteIndex <= usedRoute.Count - 1 && (claimedTrains.Count > 0); iRouteIndex++)
            {
                TrackCircuitSection nextSection = usedRoute[iRouteIndex].TrackCircuitSection;

                for (int iTrain = claimedTrains.Count - 1; iTrain >= 0; iTrain--)
                {
                    Train.TrainRouted claimingTrain = claimedTrains[iTrain];

                    if (!nextSection.CircuitState.TrainClaimed.Remove(train))
                    {
                        claimedTrains.Remove(claimingTrain);
                    }
                }

                nextSection.Claim(train);
            }
        }

        //================================================================================================//
        /// <summary>
        /// align pins switch or crossover
        /// </summary>
        public void AlignSwitchPins(int linkedSectionIndex)
        {
            TrackDirection alignDirection = (TrackDirection)(-1);  // pin direction for leading section
            Location alignLink = (Location)(-1);       // link index for leading section

            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                foreach (Location location in EnumExtension.GetValues<Location>())
                {
                    if (Pins[direction, location].Link == linkedSectionIndex)
                    {
                        alignDirection = direction;
                        alignLink = location;
                    }
                }
            }

            if ((int)alignDirection >= 0)
            {
                ActivePins[alignDirection, Location.NearEnd] = ActivePins[alignDirection, Location.NearEnd].FromLink(-1);
                ActivePins[alignDirection, Location.FarEnd] = ActivePins[alignDirection, Location.FarEnd].FromLink(-1);

                ActivePins[alignDirection, alignLink] = Pins[alignDirection, alignLink];

                TrackCircuitSection linkedSection = TrackCircuitList[linkedSectionIndex];
                foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
                {
                    foreach (Location location in EnumExtension.GetValues<Location>())
                    {
                        if (linkedSection.Pins[direction, location].Link == Index)
                        {
                            linkedSection.ActivePins[direction, location] = linkedSection.Pins[direction, location].FromLink(Index);
                        }
                    }
                }
            }

            // if junction, align physical switch

            if (CircuitType == TrackCircuitType.Junction)
            {
                int switchPos = -1;
                if (ActivePins[TrackDirection.Reverse, Location.NearEnd].Link != -1)
                    switchPos = 0;
                if (ActivePins[TrackDirection.Reverse, Location.FarEnd].Link != -1)
                    switchPos = 1;

                if (switchPos >= 0)
                {
                    signals.SetSwitch(OriginalIndex, switchPos, this);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// de-align active switch pins
        /// </summary>
        public void DeAlignSwitchPins()
        {
            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                if (Pins[direction, Location.FarEnd].Link > 0)     // active switchable end
                {
                    foreach (Location link in EnumExtension.GetValues<Location>())
                    {
                        int activeLink = Pins[direction, link].Link;
                        TrackDirection activeDirection = Pins[direction, link].Direction.Next();
                        ActivePins[direction, link] = ActivePins[direction, link].FromLink(-1);

                        TrackCircuitSection linkSection = TrackCircuitList[activeLink];
                        linkSection.ActivePins[activeDirection, Location.NearEnd] = linkSection.ActivePins[activeDirection, Location.NearEnd].FromLink(-1);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Get section state for request clear node
        /// Method is put through to train class because of differences between activity and timetable mode
        /// </summary>
        public bool GetSectionStateClearNode(Train.TrainRouted train, int elementDirection, TrackCircuitPartialPathRoute routePart)
        {
            if (train == null)
                throw new ArgumentNullException(nameof(train));

            bool returnValue = train.Train.TrainGetSectionStateClearNode(elementDirection, routePart, this);
            return (returnValue);
        }

        //================================================================================================//
        /// <summary>
        /// Get state of single section
        /// Check for train
        /// </summary>
        public InternalBlockstate GetSectionState(Train.TrainRouted train, int direction, InternalBlockstate passedBlockstate, TrackCircuitPartialPathRoute route, int signalIndex)
        {
            InternalBlockstate localBlockstate = InternalBlockstate.Reservable;  // default value
            bool stateSet = false;

            TrackCircuitState circuitState = CircuitState;

            // track occupied - check speed and direction - only for normal sections
            if (train != null && circuitState.OccupationState.ContainsTrain(train))
            {
                localBlockstate = InternalBlockstate.Reserved;  // occupied by own train counts as reserved
                stateSet = true;
            }
            else if (circuitState.Occupied(direction, true))
            {
                {
                    localBlockstate = InternalBlockstate.OccupiedSameDirection;
                    stateSet = true;
                }
            }
            else
            {
                int reqDirection = direction == 0 ? 1 : 0;
                if (circuitState.Occupied(reqDirection, false))
                {
                    localBlockstate = InternalBlockstate.OccupiedOppositeDirection;
                    stateSet = true;
                }
            }

            // for junctions or cross-overs, check route selection

            if (CircuitType == TrackCircuitType.Junction || CircuitType == TrackCircuitType.Crossover)
            {
                if (circuitState.Occupied())    // there is a train on the switch
                {
                    if (route == null)  // no route from signal - always report switch blocked
                    {
                        localBlockstate = InternalBlockstate.Blocked;
                        stateSet = true;
                    }
                    else
                    {
                        TrackDirection reqPinIndex = (TrackDirection)(-1);
                        foreach (TrackDirection reqDirection in EnumExtension.GetValues<TrackDirection>())
                        {
                            if (Pins[reqDirection, Location.FarEnd].Link > 0)
                            {
                                reqPinIndex = reqDirection;  // switchable end
                                break;
                            }
                        }

                        Location switchEnd = (Location)(-1);
                        foreach (Location location in EnumExtension.GetValues<Location>())
                        {
                            int nextSectionIndex = Pins[reqPinIndex, location].Link;
                            int routeListIndex = route.GetRouteIndex(nextSectionIndex, 0);
                            if (routeListIndex >= 0)
                                switchEnd = location;  // required exit
                        }
                        // allow if switch not active (both links dealligned)
                        if (switchEnd < 0 || (ActivePins[reqPinIndex, switchEnd].Link < 0 && ActivePins[reqPinIndex, switchEnd.Next()].Link >= 0)) // no free exit available or switch misaligned
                        {
                            localBlockstate = InternalBlockstate.Blocked;
                            stateSet = true;
                        }
                    }
                }
            }

            // track reserved - check direction

            if (circuitState.TrainReserved != null && train != null && !stateSet)
            {
                Train.TrainRouted reservedTrain = circuitState.TrainReserved;
                if (reservedTrain.Train == train.Train)
                {
                    localBlockstate = InternalBlockstate.Reserved;
                    stateSet = true;
                }
                else
                {
                    if (MPManager.IsMultiPlayer())
                    {
                        bool reservedTrainStillThere = false;
                        foreach (Signal signal in EndSignals)
                        {
                            if (signal != null && signal.EnabledTrain != null && signal.EnabledTrain.Train == reservedTrain.Train) reservedTrainStillThere = true;
                        }

                        if (reservedTrainStillThere && reservedTrain.Train.ValidRoute[0] != null && reservedTrain.Train.PresentPosition[Direction.Forward] != null &&
                            reservedTrain.Train.GetDistanceToTrain(Index, 0.0f) > 0)
                            localBlockstate = InternalBlockstate.ReservedOther;
                        else
                        {
                            //if (reservedTrain.Train.RearTDBTraveller.DistanceTo(this.
                            circuitState.TrainReserved = train;
                            localBlockstate = InternalBlockstate.Reserved;
                        }
                    }
                    else
                    {
                        localBlockstate = InternalBlockstate.ReservedOther;
                    }
                }
            }

            // signal reserved - reserved for other
            if (circuitState.SignalReserved >= 0 && circuitState.SignalReserved != signalIndex)
            {
                localBlockstate = InternalBlockstate.ReservedOther;
                stateSet = true;
            }

            // track claimed
            if (!stateSet && train != null && circuitState.TrainClaimed.Count > 0 && circuitState.TrainClaimed.PeekTrain() != train.Train)
            {
                localBlockstate = InternalBlockstate.Open;
                stateSet = true;
            }

            // wait condition
            if (train != null)
            {
                bool waitRequired = train.Train.CheckWaitCondition(Index);

                if ((!stateSet || localBlockstate < InternalBlockstate.ForcedWait) && waitRequired)
                {
                    localBlockstate = InternalBlockstate.ForcedWait;
                    train.Train.ClaimState = false; // claim not allowed for forced wait
                }

                // deadlock trap - may not set deadlock if wait is active 
                if (localBlockstate != InternalBlockstate.ForcedWait && DeadlockTraps.ContainsKey(train.Train.Number))
                {
                    if (train.Train.VerifyDeadlock(DeadlockTraps[train.Train.Number]))
                    {
                        localBlockstate = InternalBlockstate.Blocked;
                        if (!DeadlockAwaited.Contains(train.Train.Number))
                            DeadlockAwaited.Add(train.Train.Number);
                    }
                }
            }
            return localBlockstate > passedBlockstate ? localBlockstate : passedBlockstate;
        }


        //================================================================================================//
        /// <summary>
        /// Test only if section reserved to train
        /// </summary>
        public bool CheckReserved(Train.TrainRouted train)
        {
            return CircuitState.TrainReserved?.Train == train?.Train;
        }

        //================================================================================================//
        /// <summary>
        /// Test if train ahead and calculate distance to that train (front or rear depending on direction)
        /// </summary>
        public Dictionary<Train, float> TestTrainAhead(Train train, float offset, TrackDirection direction)
        {
            Train trainFound = null;
            float distanceTrainAheadM = Length + 1.0f; // ensure train is always within section

            List<Train.TrainRouted> trainsInSection = CircuitState.TrainsOccupying();

            // remove own train
            if (train != null)
            {
                for (int i = trainsInSection.Count - 1; i >= 0; i--)
                {
                    if (trainsInSection[i].Train == train)
                        trainsInSection.RemoveAt(i);
                }
            }

            // search for trains in section
            foreach (Train.TrainRouted nextTrain in trainsInSection)
            {
                int nextTrainRouteIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(Index, 0);
                if (nextTrainRouteIndex >= 0)
                {
                    TrackCircuitPosition nextFront = nextTrain.Train.PresentPosition[nextTrain.Direction];
                    Direction reverseDirection = nextTrain.TrainRouteDirectionIndex == 0 ? Direction.Backward : Direction.Forward;
                    TrackCircuitPosition nextRear = nextTrain.Train.PresentPosition[reverseDirection];

                    TrackCircuitRouteElement thisElement = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex][nextTrainRouteIndex];
                    if (thisElement.Direction == direction) // same direction, so if the train is in front we're looking at the rear of the train
                    {
                        if (nextRear.TrackCircuitSectionIndex == Index) // rear of train is in same section
                        {
                            float thisTrainDistanceM = nextRear.Offset;

                            if (thisTrainDistanceM < distanceTrainAheadM && nextRear.Offset >= offset) // train is nearest train and in front
                            {
                                distanceTrainAheadM = thisTrainDistanceM;
                                trainFound = nextTrain.Train;
                            }
                            else if (nextRear.Offset < offset && nextRear.Offset + nextTrain.Train.Length > offset) // our end is in the middle of the train
                            {
                                distanceTrainAheadM = offset; // set distance to 0 (offset is deducted later)
                                trainFound = nextTrain.Train;
                            }
                        }
                        else
                        {
                            // try to use next train indices
                            int nextRouteFrontIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(nextFront.TrackCircuitSectionIndex, 0);
                            int nextRouteRearIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(nextRear.TrackCircuitSectionIndex, 0);
                            int usedTrainRouteIndex = nextTrainRouteIndex;

                            // if not on route, try this trains route
                            if (train != null && (nextRouteFrontIndex < 0 || nextRouteRearIndex < 0))
                            {
                                nextRouteFrontIndex = train.ValidRoute[0].GetRouteIndex(nextFront.TrackCircuitSectionIndex, 0);
                                nextRouteRearIndex = train.ValidRoute[0].GetRouteIndex(nextRear.TrackCircuitSectionIndex, 0);
                                usedTrainRouteIndex = train.ValidRoute[0].GetRouteIndex(Index, 0);
                            }

                            // if not found either, build temp route
                            if (nextRouteFrontIndex < 0 || nextRouteRearIndex < 0)
                            {
                                TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(nextTrain.Train, nextFront.TrackCircuitSectionIndex, nextFront.Offset, nextFront.Direction,
                                    nextTrain.Train.Length, true, true, false);
                                nextRouteFrontIndex = tempRoute.GetRouteIndex(nextFront.TrackCircuitSectionIndex, 0);
                                nextRouteRearIndex = tempRoute.GetRouteIndex(nextRear.TrackCircuitSectionIndex, 0);
                                usedTrainRouteIndex = tempRoute.GetRouteIndex(Index, 0);
                            }

                            if (nextRouteRearIndex < usedTrainRouteIndex)
                            {
                                if (nextRouteFrontIndex > usedTrainRouteIndex) // train spans section, so we're in the middle of it - return 0
                                {
                                    distanceTrainAheadM = offset; // set distance to 0 (offset is deducted later)
                                    trainFound = nextTrain.Train;
                                } // otherwise train is not in front, so don't use it
                            }
                            else  // if index is greater, train has moved on
                            {
                                // check if still ahead of us

                                if (train != null && train.ValidRoute != null)
                                {
                                    int lastSectionIndex = train.ValidRoute[0].GetRouteIndex(nextRear.TrackCircuitSectionIndex, train.PresentPosition[Direction.Forward].RouteListIndex);
                                    if (lastSectionIndex >= train.PresentPosition[Direction.Forward].RouteListIndex)
                                    {
                                        distanceTrainAheadM = Length;  // offset is deducted later
                                        for (int isection = nextTrainRouteIndex + 1; isection <= nextRear.RouteListIndex - 1; isection++)
                                        {
                                            distanceTrainAheadM += nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex][isection].TrackCircuitSection.Length;
                                        }
                                        distanceTrainAheadM += nextTrain.Train.PresentPosition[Direction.Backward].Offset;
                                        trainFound = nextTrain.Train;
                                    }
                                }
                            }
                        }
                    }
                    else // reverse direction, so we're looking at the front - use section length - offset as position
                    {
                        float thisTrainOffset = Length - nextFront.Offset;
                        if (nextFront.TrackCircuitSectionIndex == Index)  // front of train in section
                        {
                            float thisTrainDistanceM = thisTrainOffset;

                            if (thisTrainDistanceM < distanceTrainAheadM && thisTrainOffset >= offset) // train is nearest train and in front
                            {
                                distanceTrainAheadM = thisTrainDistanceM;
                                trainFound = nextTrain.Train;
                            }
                            // extra test : if front is beyond other train but rear is not, train is considered to be still in front (at distance = offset)
                            // this can happen in pre-run mode due to large interval
                            if (train != null && thisTrainDistanceM < distanceTrainAheadM && thisTrainOffset < offset)
                            {
                                if ((!Simulator.Instance.TimetableMode && thisTrainOffset >= (offset - nextTrain.Train.Length)) ||
                                    (Simulator.Instance.TimetableMode && thisTrainOffset >= (offset - train.Length)))
                                {
                                    distanceTrainAheadM = offset;
                                    trainFound = nextTrain.Train;
                                }
                            }
                        }
                        else
                        {
                            int nextRouteFrontIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(nextFront.TrackCircuitSectionIndex, 0);
                            int nextRouteRearIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(nextRear.TrackCircuitSectionIndex, 0);
                            int usedTrainRouteIndex = nextTrainRouteIndex;

                            // if not on route, try this trains route
                            if (train != null && (nextRouteFrontIndex < 0 || nextRouteRearIndex < 0))
                            {
                                nextRouteFrontIndex = train.ValidRoute[0].GetRouteIndex(nextFront.TrackCircuitSectionIndex, 0);
                                nextRouteRearIndex = train.ValidRoute[0].GetRouteIndex(nextRear.TrackCircuitSectionIndex, 0);
                                usedTrainRouteIndex = train.ValidRoute[0].GetRouteIndex(Index, 0);
                            }

                            // if not found either, build temp route
                            if (nextRouteFrontIndex < 0 || nextRouteRearIndex < 0)
                            {
                                TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(nextTrain.Train, nextFront.TrackCircuitSectionIndex, nextFront.Offset, nextFront.Direction,
                                    nextTrain.Train.Length, true, true, false);
                                nextRouteFrontIndex = tempRoute.GetRouteIndex(nextFront.TrackCircuitSectionIndex, 0);
                                nextRouteRearIndex = tempRoute.GetRouteIndex(nextRear.TrackCircuitSectionIndex, 0);
                                usedTrainRouteIndex = tempRoute.GetRouteIndex(Index, 0);
                            }

                            if (nextRouteFrontIndex < usedTrainRouteIndex)
                            {
                                if (nextRouteRearIndex > usedTrainRouteIndex)  // train spans section so we're in the middle of it
                                {
                                    distanceTrainAheadM = offset; // set distance to 0 (offset is deducted later)
                                    trainFound = nextTrain.Train;
                                } // else train is not in front of us
                            }
                            else  // if index is greater, train has moved on - return section length minus offset
                            {
                                // check if still ahead of us
                                if (train != null && train.ValidRoute != null)
                                {
                                    int lastSectionIndex = train.ValidRoute[0].GetRouteIndex(nextRear.TrackCircuitSectionIndex, train.PresentPosition[Direction.Forward].RouteListIndex);
                                    if (lastSectionIndex > train.PresentPosition[Direction.Forward].RouteListIndex)
                                    {
                                        distanceTrainAheadM = Length;  // offset is deducted later
                                        for (int isection = nextTrainRouteIndex + 1; isection <= nextRear.RouteListIndex - 1; isection++)
                                        {
                                            distanceTrainAheadM += nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex][isection].TrackCircuitSection.Length;
                                        }
                                        distanceTrainAheadM += nextTrain.Train.PresentPosition[Direction.Backward].Offset;
                                        trainFound = nextTrain.Train;
                                    }
                                }
                            }
                        }

                    }
                }
                else
                {
                    distanceTrainAheadM = offset; // train is off its route - assume full section occupied, offset is deducted later //
                    trainFound = nextTrain.Train;
                }
            }

            Dictionary<Train, float> result = new Dictionary<Train, float>();
            if (trainFound != null)
                if (distanceTrainAheadM >= offset) // train is indeed ahead
                {
                    result.Add(trainFound, (distanceTrainAheadM - offset));
                }
            return (result);
        }

        //================================================================================================//
        /// <summary>
        /// Get next active link
        /// </summary>

        public TrackPin GetNextActiveLink(TrackDirection direction, int lastIndex)
        {

            // Crossover

            if (CircuitType == TrackCircuitType.Crossover)
            {
                if (Pins[direction.Next(), Location.NearEnd].Link == lastIndex)
                {
                    return ActivePins[direction, Location.NearEnd];
                }
                else if (Pins[direction.Next(), Location.FarEnd].Link == lastIndex)
                {
                    return ActivePins[direction, Location.FarEnd];
                }
                else
                {
                    return TrackPin.Empty;
                }
            }

            // All other sections

            if (ActivePins[direction, Location.NearEnd].Link > 0)
            {
                return ActivePins[direction, Location.NearEnd];
            }

            return ActivePins[direction, Location.FarEnd];
        }

        //================================================================================================//
        /// <summary>
        /// Get distance between objects
        /// </summary>
        public static float GetDistanceBetweenObjects(int startSectionIndex, float startOffset, TrackDirection startDirection, int endSectionIndex, float endOffset)
        {
            int currentSectionIndex = startSectionIndex;
            TrackDirection direction = startDirection;

            TrackCircuitSection section = TrackCircuitList[currentSectionIndex];

            float distanceM = 0.0f;
            int lastIndex = -2;  // set to non-occuring value

            while (currentSectionIndex != endSectionIndex && currentSectionIndex > 0)
            {
                distanceM += section.Length;
                TrackPin nextLink = section.GetNextActiveLink(direction, lastIndex);

                lastIndex = currentSectionIndex;
                currentSectionIndex = nextLink.Link;
                direction = nextLink.Direction;

                if (currentSectionIndex > 0)
                {
                    section = TrackCircuitList[currentSectionIndex];
                    if (currentSectionIndex == startSectionIndex)  // loop found - return distance found sofar
                    {
                        distanceM -= startOffset;
                        return distanceM;
                    }
                }
            }

            // use found distance, correct for begin and end offset

            if (currentSectionIndex == endSectionIndex)
            {
                distanceM += endOffset - startOffset;
                return distanceM;
            }

            return -1.0f;
        }

        //================================================================================================//
        /// <summary>
        /// Check if train can be placed in section
        /// </summary>
        public bool CanPlaceTrain(Train train, float offset, float trainLength)
        {
            if (null == train)
                throw new ArgumentNullException(nameof(train));

            if (!IsAvailable(train))
            {
                if (CircuitState.TrainReserved != null || CircuitState.TrainClaimed.Count > 0)
                {
                    return false;
                }

                if (DeadlockTraps.ContainsKey(train.Number))
                {
                    return false;  // prevent deadlock
                }

                if (CircuitType != TrackCircuitType.Normal) // other than normal and not clear - return false
                {
                    return false;
                }

                if (offset == 0 && trainLength > Length) // train spans section
                {
                    return false;
                }

                // get other trains in section
                Dictionary<Train, float> trainInfo;
                float offsetFromStart = offset;

                // test train ahead of rear end (for non-placed trains, always use direction 0)

                if (train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex == Index)
                {
                    trainInfo = TestTrainAhead(train, offsetFromStart, train.PresentPosition[Direction.Backward].Direction); // rear end in this section, use offset
                }
                else
                {
                    offsetFromStart = 0.0f;
                    trainInfo = TestTrainAhead(train, 0.0f, train.PresentPosition[Direction.Backward].Direction); // test from start
                }

                if (trainInfo.Count > 0)
                {
                    foreach (KeyValuePair<Train, float> trainAhead in trainInfo)
                    {
                        if (trainAhead.Value < trainLength) // train ahead not clear
                        {
                            return (false);
                        }
                        else
                        {
                            TrackCircuitPosition trainPosition = trainAhead.Key.PresentPosition[trainAhead.Key.MUDirection == MidpointDirection.Forward ? Direction.Forward : Direction.Backward];
                            if (trainPosition.TrackCircuitSectionIndex == Index && trainAhead.Key.SpeedMpS > 0 && trainPosition.Direction != train.PresentPosition[Direction.Forward].Direction)
                            {
                                return false;   // train is moving towards us
                            }
                        }
                    }
                }

                // test train behind of front end
                TrackDirection revDirection = train.PresentPosition[Direction.Forward].Direction.Next();
                if (train.PresentPosition[Direction.Forward].TrackCircuitSectionIndex == Index)
                {
                    float offsetFromEnd = Length - (trainLength + offsetFromStart);
                    trainInfo = TestTrainAhead(train, offsetFromEnd, revDirection); // test remaining length
                }
                else
                {
                    trainInfo = TestTrainAhead(train, 0.0f, revDirection); // test full section
                }

                if (trainInfo.Count > 0)
                {
                    foreach (KeyValuePair<Train, float> trainAhead in trainInfo)
                    {
                        if (trainAhead.Value < trainLength) // train behind not clear
                        {
                            return false;
                        }
                    }
                }

            }

            return true;
        }

        //================================================================================================//
        /// <summary>
        /// Set deadlock trap for all trains which deadlock from this section at begin section
        /// </summary>
        public void SetDeadlockTrap(Train train, List<Dictionary<int, int>> deadlocks)
        {
            if (null == train)
                throw new ArgumentNullException(nameof(train));

            foreach (Dictionary<int, int> deadlockInfo in deadlocks ?? Enumerable.Empty<Dictionary<int, int>>())
            {
                foreach (KeyValuePair<int, int> deadlockDetails in deadlockInfo)
                {
                    int otherTrainNumber = deadlockDetails.Key;
                    Train otherTrain = train.GetOtherTrainByNumber(deadlockDetails.Key);

                    int endSectionIndex = deadlockDetails.Value;

                    // check if endsection still in path
                    if (train.ValidRoute[0].GetRouteIndex(endSectionIndex, train.PresentPosition[Direction.Forward].RouteListIndex) >= 0)
                    {
                        TrackCircuitSection endSection = TrackCircuitList[endSectionIndex];

                        // if other section allready set do not set deadlock
                        if (otherTrain != null && endSection.IsSet(otherTrain, true))
                            break;

                        if (DeadlockTraps.ContainsKey(train.Number))
                        {
                            List<int> thisTrap = DeadlockTraps[train.Number];
                            if (thisTrap.Contains(otherTrainNumber))
                                break;  // cannot set deadlock for train which has deadlock on this end
                        }

                        if (endSection.DeadlockTraps.ContainsKey(otherTrainNumber))
                        {
                            if (!endSection.DeadlockTraps[otherTrainNumber].Contains(train.Number))
                            {
                                endSection.DeadlockTraps[otherTrainNumber].Add(train.Number);
                            }
                        }
                        else
                        {
                            List<int> deadlockList = new List<int>
                            {
                                train.Number
                            };
                            endSection.DeadlockTraps.Add(otherTrainNumber, deadlockList);
                        }

                        if (!endSection.DeadlockActives.Contains(train.Number))
                        {
                            endSection.DeadlockActives.Add(train.Number);
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set deadlock trap for individual train at end section
        /// </summary>
        public void SetDeadlockTrap(int trainNumber, int otherTrainNumber)
        {

            if (DeadlockTraps.ContainsKey(otherTrainNumber))
            {
                if (!DeadlockTraps[otherTrainNumber].Contains(trainNumber))
                {
                    DeadlockTraps[otherTrainNumber].Add(trainNumber);
                }
            }
            else
            {
                List<int> deadlockList = new List<int>
                {
                    trainNumber
                };
                DeadlockTraps.Add(otherTrainNumber, deadlockList);
            }

            if (!DeadlockActives.Contains(trainNumber))
            {
                DeadlockActives.Add(trainNumber);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Clear deadlock trap
        /// </summary>
        public void ClearDeadlockTrap(int trainNumber)
        {
            List<int> deadlocksCleared = new List<int>();

            if (DeadlockActives.Contains(trainNumber))
            {
                foreach (KeyValuePair<int, List<int>> thisDeadlock in DeadlockTraps)
                {
                    if (thisDeadlock.Value.Contains(trainNumber))
                    {
                        thisDeadlock.Value.Remove(trainNumber);
                        if (thisDeadlock.Value.Count <= 0)
                        {
                            deadlocksCleared.Add(thisDeadlock.Key);
                        }
                    }
                }
                DeadlockActives.Remove(trainNumber);
            }

            foreach (int deadlockKey in deadlocksCleared)
            {
                DeadlockTraps.Remove(deadlockKey);
            }

            DeadlockAwaited.Remove(trainNumber);
        }

        //================================================================================================//
        /// <summary>
        /// Check if train is waiting for deadlock
        /// </summary>
        public bool CheckDeadlockAwaited(int trainNumber)
        {
            int totalCount = DeadlockAwaited.Count;
            if (DeadlockAwaited.Contains(trainNumber))
                totalCount--;
            return (totalCount > 0);
        }

        //================================================================================================//
        /// <summary>
        /// Clear track sections from train behind
        /// </summary>
        private static void ClearSectionsOfTrainBehind(Train.TrainRouted trainRouted, TrackCircuitSection startSection)
        {
            int startindex = 0;
            startSection.UnreserveTrain(trainRouted, true);
            for (int i = 0; i < trainRouted.Train.ValidRoute[0].Count; i++)
            {
                if (startSection == trainRouted.Train.ValidRoute[0][i].TrackCircuitSection)
                {
                    startindex = i + 1;
                    break;
                }
            }

            for (int i = startindex; i < trainRouted.Train.ValidRoute[0].Count; i++)
            {
                TrackCircuitSection currentSection = trainRouted.Train.ValidRoute[0][i].TrackCircuitSection;
                if (currentSection.CircuitState.TrainReserved == null)
                    break;
                currentSection.UnreserveTrain(trainRouted, true);
            }

            // Reset signal behind new train
            for (int i = startindex - 2; i >= trainRouted.Train.PresentPosition[Direction.Forward].RouteListIndex; i--)
            {
                TrackCircuitSection thisSection = trainRouted.Train.ValidRoute[trainRouted.TrainRouteDirectionIndex][i].TrackCircuitSection;
                Signal thisSignal = thisSection.EndSignals[trainRouted.Train.ValidRoute[trainRouted.TrainRouteDirectionIndex][i].Direction];
                if (thisSignal != null)
                {
                    thisSignal.ResetSignal(false);
                    break;
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Split section
        /// </summary>
        public static void SplitSection(int sourceSectionIndex, int targetSectionIndex, float position)
        {
            TrackCircuitSection sourceSection = TrackCircuitList[sourceSectionIndex];
            TrackCircuitSection targetSection = sourceSection.CopyFrom(targetSectionIndex);
            TrackCircuitSection replacementSection = sourceSection.CopyFrom(sourceSectionIndex);

            replacementSection.CircuitType = targetSection.CircuitType = TrackCircuitType.Normal;

            replacementSection.Length = position;
            targetSection.Length = sourceSection.Length - position;

            // take care of rounding errors

            if (targetSection.Length < 0 || Math.Abs(targetSection.Length) < 0.01f)
            {
                targetSection.Length = 0.01f;
                replacementSection.Length -= 0.01f;  // take length off other part
            }
            if (replacementSection.Length < 0 || Math.Abs(replacementSection.Length) < 0.01f)
            {
                replacementSection.Length = 0.01f;
                targetSection.Length -= 0.01f;  // take length off other part
            }

            // set lengths and offset

            replacementSection.OffsetLength[Location.NearEnd] = sourceSection.OffsetLength[Location.NearEnd] + targetSection.Length;
            replacementSection.OffsetLength[Location.FarEnd] = sourceSection.OffsetLength[Location.FarEnd];

            targetSection.OffsetLength[Location.NearEnd] = sourceSection.OffsetLength[Location.NearEnd];
            targetSection.OffsetLength[Location.FarEnd] = sourceSection.OffsetLength[Location.FarEnd] + replacementSection.Length;

            // set new pins

            replacementSection.Pins[TrackDirection.Ahead, Location.NearEnd] = sourceSection.Pins[TrackDirection.Ahead, Location.NearEnd];
            replacementSection.Pins[TrackDirection.Reverse, Location.NearEnd] = new TrackPin(targetSectionIndex, TrackDirection.Reverse);

            targetSection.Pins[TrackDirection.Ahead, Location.NearEnd] = new TrackPin(sourceSectionIndex, TrackDirection.Ahead);
            targetSection.Pins[TrackDirection.Reverse, Location.NearEnd] = sourceSection.Pins[TrackDirection.Reverse, Location.NearEnd];

            // update pins on adjacent sections

            int refLinkIndex = targetSection.Pins[TrackDirection.Reverse, Location.NearEnd].Link;
            TrackDirection refLinkDirIndex = targetSection.Pins[TrackDirection.Reverse, Location.NearEnd].Direction.Next();
            TrackCircuitSection refLink = TrackCircuitList[refLinkIndex];
            if (refLink.Pins[refLinkDirIndex, Location.NearEnd].Link == sourceSectionIndex)
            {
                refLink.Pins[refLinkDirIndex, Location.NearEnd] = refLink.Pins[refLinkDirIndex, Location.NearEnd].FromLink(targetSectionIndex);
            }
            else if (refLink.Pins[refLinkDirIndex, Location.FarEnd].Link == sourceSectionIndex)
            {
                refLink.Pins[refLinkDirIndex, Location.FarEnd] = refLink.Pins[refLinkDirIndex, Location.FarEnd].FromLink(targetSectionIndex);
            }

            // copy signal information

            for (int i = 0; i < sourceSection.CircuitItems.TrackCircuitSignals[0].Count; i++)
            {
                TrackCircuitSignalList orgSigList = sourceSection.CircuitItems.TrackCircuitSignals[0][i];
                TrackCircuitSignalList replSigList = replacementSection.CircuitItems.TrackCircuitSignals[0][i];
                TrackCircuitSignalList newSigList = targetSection.CircuitItems.TrackCircuitSignals[0][i];

                foreach (TrackCircuitSignalItem thisSignal in orgSigList)
                {
                    float sigLocation = thisSignal.SignalLocation;
                    if (sigLocation <= targetSection.Length)
                    {
                        newSigList.Add(thisSignal);
                    }
                    else
                    {
                        thisSignal.SignalLocation -= targetSection.Length;
                        replSigList.Add(thisSignal);
                    }
                }
            }

            for (int itype = 0; itype < sourceSection.CircuitItems.TrackCircuitSignals[TrackDirection.Reverse].Count; itype++)
            {
                TrackCircuitSignalList orgSigList = sourceSection.CircuitItems.TrackCircuitSignals[TrackDirection.Reverse][itype];
                TrackCircuitSignalList replSigList = replacementSection.CircuitItems.TrackCircuitSignals[TrackDirection.Reverse][itype];
                TrackCircuitSignalList newSigList = targetSection.CircuitItems.TrackCircuitSignals[TrackDirection.Reverse][itype];

                foreach (TrackCircuitSignalItem thisSignal in orgSigList)
                {
                    float sigLocation = thisSignal.SignalLocation;
                    if (sigLocation > replacementSection.Length)
                    {
                        thisSignal.SignalLocation -= replacementSection.Length;
                        newSigList.Add(thisSignal);
                    }
                    else
                    {
                        replSigList.Add(thisSignal);
                    }
                }
            }

            // copy speedpost information

            TrackCircuitSignalList orgSpeedList = sourceSection.CircuitItems.TrackCircuitSpeedPosts[0];
            TrackCircuitSignalList replSpeedList = replacementSection.CircuitItems.TrackCircuitSpeedPosts[0];
            TrackCircuitSignalList newSpeedList = targetSection.CircuitItems.TrackCircuitSpeedPosts[0];

            foreach (TrackCircuitSignalItem thisSpeedpost in orgSpeedList)
            {
                float sigLocation = thisSpeedpost.SignalLocation;
                if (sigLocation < targetSection.Length)
                {
                    newSpeedList.Add(thisSpeedpost);
                }
                else
                {
                    thisSpeedpost.SignalLocation -= targetSection.Length;
                    replSpeedList.Add(thisSpeedpost);
                }
            }

            orgSpeedList = sourceSection.CircuitItems.TrackCircuitSpeedPosts[TrackDirection.Reverse];
            replSpeedList = replacementSection.CircuitItems.TrackCircuitSpeedPosts[TrackDirection.Reverse];
            newSpeedList = targetSection.CircuitItems.TrackCircuitSpeedPosts[TrackDirection.Reverse];

            foreach (TrackCircuitSignalItem thisSpeedpost in orgSpeedList)
            {
                float sigLocation = thisSpeedpost.SignalLocation;
                if (sigLocation > replacementSection.Length)
                {
                    thisSpeedpost.SignalLocation -= replacementSection.Length;
                    newSpeedList.Add(thisSpeedpost);
                }
                else
                {
                    replSpeedList.Add(thisSpeedpost);
                }
            }

            // copy milepost information

            foreach (TrackCircuitMilepost thisMilepost in sourceSection.CircuitItems.TrackCircuitMileposts)
            {
                if (thisMilepost.MilepostLocation[Location.NearEnd] > replacementSection.Length)
                {
                    thisMilepost.MilepostLocation[Location.NearEnd] -= replacementSection.Length;
                    targetSection.CircuitItems.TrackCircuitMileposts.Add(thisMilepost);
                }
                else
                {
                    thisMilepost.MilepostLocation[Location.FarEnd] -= targetSection.Length;
                    replacementSection.CircuitItems.TrackCircuitMileposts.Add(thisMilepost);
                }
            }
            // update list

            TrackCircuitList.RemoveAt(sourceSectionIndex);
            TrackCircuitList.Insert(sourceSectionIndex, replacementSection);
            TrackCircuitList.Add(targetSection);
        }

        //================================================================================================//
        /// <summary>
        /// Add junction sections for Crossover
        /// </summary>

        internal static void AddCrossoverJunction(int leadSectionIndex0, int trailSectionIndex0, int leadSectionIndex1, int trailSectionIndex1, int JnIndex,
                        CrossOverInfo crossOver, TrackSectionsFile tsectiondat)
        {
            if (null == crossOver)
                throw new ArgumentNullException(nameof(crossOver));
            if (null == tsectiondat)
                throw new ArgumentNullException(nameof(tsectiondat));

            TrackCircuitSection leadSection0 = TrackCircuitList[leadSectionIndex0];
            TrackCircuitSection leadSection1 = TrackCircuitList[leadSectionIndex1];
            TrackCircuitSection trailSection0 = TrackCircuitList[trailSectionIndex0];
            TrackCircuitSection trailSection1 = TrackCircuitList[trailSectionIndex1];
            TrackCircuitSection JnSection = new TrackCircuitSection(JnIndex)
            {
                OriginalIndex = leadSection0.OriginalIndex,
                CircuitType = TrackCircuitType.Crossover,
                Length = 0
            };

            leadSection0.Pins[TrackDirection.Reverse, Location.NearEnd] = leadSection0.Pins[TrackDirection.Reverse, Location.NearEnd].FromLink(JnIndex);
            leadSection1.Pins[TrackDirection.Reverse, Location.NearEnd] = leadSection1.Pins[TrackDirection.Reverse, Location.NearEnd].FromLink(JnIndex);
            trailSection0.Pins[TrackDirection.Ahead, Location.NearEnd] = trailSection0.Pins[TrackDirection.Ahead, Location.NearEnd].FromLink(JnIndex);
            trailSection1.Pins[TrackDirection.Ahead, Location.NearEnd] = trailSection1.Pins[TrackDirection.Ahead, Location.NearEnd].FromLink(JnIndex);

            JnSection.Pins[TrackDirection.Ahead, Location.NearEnd] = new TrackPin(leadSectionIndex0, TrackDirection.Ahead);
            JnSection.Pins[TrackDirection.Ahead, Location.FarEnd] = new TrackPin(leadSectionIndex1, TrackDirection.Ahead);
            JnSection.Pins[TrackDirection.Reverse, Location.NearEnd] = new TrackPin(trailSectionIndex0, TrackDirection.Reverse);
            JnSection.Pins[TrackDirection.Reverse, Location.FarEnd] = new TrackPin(trailSectionIndex1, TrackDirection.Reverse);

            if (tsectiondat.TrackShapes.ContainsKey(crossOver.TrackShape))
            {
                JnSection.Overlap = tsectiondat.TrackShapes[crossOver.TrackShape].ClearanceDistance;
            }
            else
            {
                JnSection.Overlap = 0;
            }

            JnSection.SignalsPassingRoutes = new List<int>();

            TrackCircuitList.Add(JnSection);
        }

        //================================================================================================//
        /// <summary>
        /// insert end node to capture database break
        /// </summary>

        public static void InsertEndNode(int node, TrackDirection direction, Location pin, int endNode)
        {

            TrackCircuitSection section = TrackCircuitList[node];
            TrackCircuitSection endSection = new TrackCircuitSection(endNode)
            {
                CircuitType = TrackCircuitType.EndOfTrack
            };
            endSection.Pins[section.Pins[direction, pin].Direction.Next(), Location.NearEnd] = new TrackPin(node, direction.Next());

            section.Pins[direction, pin] = section.Pins[direction, pin].FromLink(endNode);

            TrackCircuitList.Add(endSection);
        }


    }

}
