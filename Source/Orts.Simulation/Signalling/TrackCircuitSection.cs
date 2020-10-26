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

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.MultiPlayer;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;

namespace Orts.Simulation.Signalling
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
        // Properties Index, Length and OffsetLength come from TrackCircuitSectionXref

        public static List<TrackCircuitSection> TrackCircuitList { get; } = new List<TrackCircuitSection>();

        private static Signals signals;                                                 // reference to Signals class             //

        public int Index { get; private set; }                                          // Index of TCS                           //
        public float Length { get; private set; }                                       // Length of Section                      //
        public EnumArray<float, Location> OffsetLength { get; private set; } = new EnumArray<float, Location>(); // Offset length in original tracknode    //
        public int OriginalIndex { get; private set; }                                  // original TDB section index             //
        public TrackCircuitType CircuitType { get; private set; }                       // type of section                        //

        public TrackPin[,] Pins = new TrackPin[2, 2];                   // next sections                          //
        public TrackPin[,] ActivePins = new TrackPin[2, 2];             // active next sections                   //
        public bool[] EndIsTrailingJunction = new bool[2];        // next section is trailing jn            //

        public int JunctionDefaultRoute = -1;                     // jn default route, value is out-pin      //
        public int JunctionLastRoute = -1;                        // jn last route, value is out-pin         //
        public int JunctionSetManual = -1;                        // jn set manual, value is out-pin         //
        public List<int> LinkedSignals = null;                    // switchstands linked with this switch    //
        public bool AILock;                                       // jn is locked agains AI trains           //
        public List<int> SignalsPassingRoutes;                    // list of signals reading passed junction //

        public Signal[] EndSignals = new Signal[2];   // signals at either end      //

        public double Overlap;                                    // overlap for junction nodes //
        public List<int> PlatformIndex = new List<int>();         // platforms along section    //

        public TrackCircuitItems CircuitItems;                    // all items                  //
        public TrackCircuitState CircuitState;                    // normal states              //

        // old style deadlock definitions
        public Dictionary<int, List<int>> DeadlockTraps;          // deadlock traps             //
        public List<int> DeadlockActives;                         // list of trains with active deadlock traps //
        public List<int> DeadlockAwaited;                         // train is waiting for deadlock to clear //

        // new style deadlock definitions
        public int DeadlockReference;                             // index of deadlock to related deadlockinfo object for boundary //
        public Dictionary<int, int> DeadlockBoundaries;           // list of boundaries and path index to boundary for within deadlock //

        public List<TunnelInfoData> TunnelInfo { get; private set; }          // full tunnel info data

        public void AddTunnelData(TunnelInfoData tunnelData)
        {
            if (null == TunnelInfo)
                TunnelInfo = new List<TunnelInfoData>();
            TunnelInfo.Add(tunnelData);
        }

        // trough data

        public List<TroughInfoData> TroughInfo { get; private set; }          // full trough info data

        public void AddTroughData(TroughInfoData troughData)
        {
            if (null == TroughInfo)
                TroughInfo = new List<TroughInfoData>();
            TroughInfo.Add(troughData);
        }

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>
        public TrackCircuitSection(TrackNode thisNode, int nodeIndex, TrackSectionsFile tsectiondat)
        {
            //
            // Copy general info
            //
            Index = nodeIndex;
            OriginalIndex = nodeIndex;

            if (thisNode is TrackEndNode)
            {
                CircuitType = TrackCircuitType.EndOfTrack;
            }
            else if (thisNode is TrackJunctionNode)
            {
                CircuitType = TrackCircuitType.Junction;
            }
            else
            {
                CircuitType = TrackCircuitType.Normal;
            }


            //
            // Preset pins, then copy pin info
            //

            for (int direction = 0; direction < 2; direction++)
            {
                for (int pin = 0; pin < 2; pin++)
                {
                    Pins[direction, pin] = new TrackPin(-1, -1);
                    ActivePins[direction, pin] = new TrackPin(-1, -1);
                }
            }

            int PinNo = 0;
            for (int pin = 0; pin < Math.Min(thisNode.InPins, Pins.GetLength(1)); pin++)
            {
                Pins[0, pin] = new TrackPin(thisNode.TrackPins[PinNo].Link, thisNode.TrackPins[PinNo].Direction);
                PinNo++;
            }
            if (PinNo < thisNode.InPins) PinNo = (int)thisNode.InPins;
            for (int pin = 0; pin < Math.Min(thisNode.OutPins, Pins.GetLength(1)); pin++)
            {
                Pins[1, pin] = new TrackPin(thisNode.TrackPins[PinNo].Link, thisNode.TrackPins[PinNo].Direction);
                PinNo++;
            }


            //
            // preset no end signals
            // preset no trailing junction
            //

            for (int direction = 0; direction < 2; direction++)
            {
                EndSignals[direction] = null;
                EndIsTrailingJunction[direction] = false;
            }

            //
            // Preset length and offset
            // If section index not in tsectiondat, set length to 0.
            //

            float totalLength = 0.0f;

            if (thisNode is TrackVectorNode tvn && tvn.TrackVectorSections != null)
            {
                foreach (TrackVectorSection thisSection in tvn.TrackVectorSections)
                {
                    float thisLength = 0.0f;

                    if (tsectiondat.TrackSections.ContainsKey(thisSection.SectionIndex))
                    {
                        TrackSection TS = tsectiondat.TrackSections[thisSection.SectionIndex];

                        if (TS.Curved)
                        {
                            thisLength = MathHelper.ToRadians(Math.Abs(TS.Angle)) * TS.Radius;
                        }
                        else
                        {
                            thisLength = TS.Length;

                        }
                    }

                    totalLength += thisLength;
                }
            }

            Length = totalLength;

            //
            // set signal list for junctions
            //

            if (CircuitType == TrackCircuitType.Junction)
            {
                SignalsPassingRoutes = new List<int>();
            }
            else
            {
                SignalsPassingRoutes = null;
            }

            // for Junction nodes, obtain default route
            // set switch to default route
            // copy overlap (if set)

            if (CircuitType == TrackCircuitType.Junction)
            {
                uint trackShapeIndex = (thisNode as TrackJunctionNode).ShapeIndex;
                try
                {
                    TrackShape trackShape = tsectiondat.TrackShapes[trackShapeIndex];
                    JunctionDefaultRoute = (int)trackShape.MainRoute;

                    Overlap = trackShape.ClearanceDistance;
                }
                catch (Exception)
                {
                    Trace.TraceWarning("Missing TrackShape in tsection.dat : " + trackShapeIndex);
                    JunctionDefaultRoute = 0;
                    Overlap = 0;
                }

                JunctionLastRoute = JunctionDefaultRoute;
                signals.setSwitch(OriginalIndex, JunctionLastRoute, this);
            }

            //
            // Create circuit items
            //

            CircuitItems = new TrackCircuitItems(signals.ORTSSignalTypeCount);
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
        internal TrackCircuitSection(Signals signals) :
            this(0)
        {
            if (null != TrackCircuitSection.signals)
                throw new InvalidOperationException(nameof(TrackCircuitSection.signals));
            TrackCircuitSection.signals = signals;

            CircuitItems = new TrackCircuitItems(signals.ORTSSignalTypeCount);
        }

        public TrackCircuitSection(int INode)
        {

            Index = INode;
            OriginalIndex = -1;
            CircuitType = TrackCircuitType.Empty;

            for (int iDir = 0; iDir < 2; iDir++)
            {
                EndIsTrailingJunction[iDir] = false;
                EndSignals[iDir] = null;
            }

            for (int iDir = 0; iDir < 2; iDir++)
            {
                for (int pin = 0; pin < 2; pin++)
                {
                    Pins[iDir, pin] = new TrackPin(-1, -1);
                    ActivePins[iDir, pin] = new TrackPin(-1, -1);
                }
            }

            if (null != signals) //signals is still null for ctor call from default/dummy element
                CircuitItems = new TrackCircuitItems(signals.ORTSSignalTypeCount);
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

        public void Restore(Simulator simulator, BinaryReader inf)
        {
            ActivePins[0, 0].Link = inf.ReadInt32();
            ActivePins[0, 0].Direction = inf.ReadInt32();
            ActivePins[1, 0].Link = inf.ReadInt32();
            ActivePins[1, 0].Direction = inf.ReadInt32();
            ActivePins[0, 1].Link = inf.ReadInt32();
            ActivePins[0, 1].Direction = inf.ReadInt32();
            ActivePins[1, 1].Link = inf.ReadInt32();
            ActivePins[1, 1].Direction = inf.ReadInt32();

            JunctionSetManual = inf.ReadInt32();
            JunctionLastRoute = inf.ReadInt32();
            AILock = inf.ReadBoolean();

            CircuitState.Restore(simulator, inf);

            // if physical junction, throw switch

            if (CircuitType == TrackCircuitType.Junction)
            {
                signals.setSwitch(OriginalIndex, JunctionLastRoute, this);
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
            outf.Write(ActivePins[0, 0].Link);
            outf.Write(ActivePins[0, 0].Direction);
            outf.Write(ActivePins[1, 0].Link);
            outf.Write(ActivePins[1, 0].Direction);
            outf.Write(ActivePins[0, 1].Link);
            outf.Write(ActivePins[0, 1].Direction);
            outf.Write(ActivePins[1, 1].Link);
            outf.Write(ActivePins[1, 1].Direction);

            outf.Write(JunctionSetManual);
            outf.Write(JunctionLastRoute);
            outf.Write(AILock);

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

        //================================================================================================//
        /// <summary>
        /// Copy basic info only
        /// </summary>

        public TrackCircuitSection CopyBasic(int targetIndex)
        {
            TrackCircuitSection newSection = new TrackCircuitSection(targetIndex);

            newSection.OriginalIndex = OriginalIndex;
            newSection.CircuitType = TrackCircuitType.Normal;// CircuitType;

            newSection.EndSignals[0] = EndSignals[0];
            newSection.EndSignals[1] = EndSignals[1];

            newSection.Length = Length;

            newSection.OffsetLength = new EnumArray<float, Location>(OffsetLength);

            return newSection;
        }

        //================================================================================================//
        /// <summary>
        /// Check if set for train
        /// </summary>

        public bool IsSet(Train.TrainRouted thisTrain, bool claim_is_valid)   // using routed train
        {

            // if train in this section, return true; if other train in this section, return false

            if (CircuitState.OccupiedByThisTrain(thisTrain))
            {
                return (true);
            }

            // check reservation

            if (CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train == thisTrain.Train)
            {
                return (true);
            }

            // check claim if claim is valid as state

            if (CircuitState.TrainClaimed.Count > 0 && claim_is_valid)
            {
                return (CircuitState.TrainClaimed.PeekTrain() == thisTrain.Train);
            }

            // section is not yet set for this train

            return (false);
        }

        public bool IsSet(Train thisTrain, bool claim_is_valid)    // using unrouted train
        {
            if (IsSet(thisTrain.routedForward, claim_is_valid))
            {
                return (true);
            }
            else
            {
                return (IsSet(thisTrain.routedBackward, claim_is_valid));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check available state for train
        /// </summary>

        public bool IsAvailable(Train.TrainRouted thisTrain)    // using routed train
        {

            // if train in this section, return true; if other train in this section, return false
            // check if train is in section in expected direction - otherwise return false

            if (CircuitState.OccupiedByThisTrain(thisTrain))
            {
                return (true);
            }

            if (CircuitState.OccupiedByOtherTrains(thisTrain))
            {
                return (false);
            }

            // check reservation

            if (CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train == thisTrain.Train)
            {
                return (true);
            }

            if (!signals.Simulator.TimetableMode && thisTrain.Train.TrainType == Train.TRAINTYPE.AI_NOTSTARTED)
            {
                if (CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train != thisTrain.Train)
                {
                    ClearSectionsOfTrainBehind(CircuitState.TrainReserved, this);
                }
            }
            else if (thisTrain.Train.IsPlayerDriven && thisTrain.Train.ControlMode != Train.TRAIN_CONTROL.MANUAL && thisTrain.Train.DistanceTravelledM == 0.0 &&
                     thisTrain.Train.TCRoute != null && thisTrain.Train.ValidRoute[0] != null && thisTrain.Train.TCRoute.activeSubpath == 0) // We are at initial placement
            // Check if section is under train, and therefore can be unreserved from other trains
            {
                int thisRouteIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(Index, 0);
                if ((thisRouteIndex <= thisTrain.Train.PresentPosition[0].RouteListIndex && Index >= thisTrain.Train.PresentPosition[1].RouteListIndex) ||
                    (thisRouteIndex >= thisTrain.Train.PresentPosition[0].RouteListIndex && Index <= thisTrain.Train.PresentPosition[1].RouteListIndex))
                {
                    if (CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train != thisTrain.Train)
                    {
                        Train.TrainRouted trainRouted = CircuitState.TrainReserved;
                        ClearSectionsOfTrainBehind(trainRouted, this);
                        if (trainRouted.Train.TrainType == Train.TRAINTYPE.AI || trainRouted.Train.TrainType == Train.TRAINTYPE.AI_PLAYERHOSTING)
                            ((AITrain)trainRouted.Train).ResetActions(true);
                    }
                }
            }
            else if (CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train != thisTrain.Train)
            {
                return (false);
            }

            // check signal reservation

            if (CircuitState.SignalReserved >= 0)
            {
                return (false);
            }

            // check claim

            if (CircuitState.TrainClaimed.Count > 0)
            {
                return (CircuitState.TrainClaimed.PeekTrain() == thisTrain.Train);
            }

            // check deadlock trap

            if (DeadlockTraps.ContainsKey(thisTrain.Train.Number))
            {
                if (!DeadlockAwaited.Contains(thisTrain.Train.Number))
                    DeadlockAwaited.Add(thisTrain.Train.Number); // train is waiting for deadlock to clear
                return (false);
            }
            // check deadlock is in use - only if train has valid route

            if (thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex] != null)
            {

                int routeElementIndex = thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex].GetRouteIndex(Index, 0);
                if (routeElementIndex >= 0)
                {
                    Train.TCRouteElement thisElement = thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex][routeElementIndex];

                    // check for deadlock awaited at end of passing loop - path based deadlock processing
                    if (!signals.UseLocationPassingPaths)
                    {
                        // if deadlock is allready awaited set available to false to keep one track open
                        if (thisElement.StartAlternativePath != null)
                        {
                            TrackCircuitSection endSection = TrackCircuitList[thisElement.StartAlternativePath[1]];
                            if (endSection.CheckDeadlockAwaited(thisTrain.Train.Number))
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
#if DEBUG_DEADLOCK
                            File.AppendAllText(@"C:\Temp\deadlock.txt",
                                "\n **** Check IfAvailable for section " + Index.ToString() + " for train : " + thisTrain.Train.Number.ToString() + "\n");
#endif
                            DeadlockInfo sectionDeadlockInfo = signals.DeadlockInfoList[DeadlockReference];
                            List<int> pathAvail = sectionDeadlockInfo.CheckDeadlockPathAvailability(this, thisTrain.Train);
#if DEBUG_DEADLOCK
                            File.AppendAllText(@"C:\Temp\deadlock.txt", "\nReturned no. of available paths : " + pathAvail.Count.ToString() + "\n");
                            File.AppendAllText(@"C:\Temp\deadlock.txt", "****\n\n");
#endif
                            if (pathAvail.Count <= 0) return (false);
                        }
                    }
                }
            }

            // section is clear

            return (true);
        }

        public bool IsAvailable(Train thisTrain)    // using unrouted train
        {
            if (IsAvailable(thisTrain.routedForward))
            {
                return (true);
            }
            else
            {
                return (IsAvailable(thisTrain.routedBackward));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Reserve : set reserve state
        /// </summary>

        public void Reserve(Train.TrainRouted thisTrain, Train.TCSubpathRoute thisRoute)
        {

#if DEBUG_REPORTS
            File.AppendAllText(@"C:\temp\printproc.txt",
                String.Format("Reserve section {0} for train {1}\n",
                this.Index,
                thisTrain.Train.Number));
#endif
#if DEBUG_DEADLOCK
            File.AppendAllText(@"C:\temp\deadlock.txt",
                String.Format("Reserve section {0} for train {1}\n",
                this.Index,
                thisTrain.Train.Number));
#endif
            if (thisTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Reserve section {0} for train {1}\n",
                    this.Index,
                    thisTrain.Train.Number));
            }

            Train.TCRouteElement thisElement;

            if (!CircuitState.OccupiedByThisTrain(thisTrain.Train))
            {
                // check if not beyond trains route

                bool validPosition = true;
                int routeIndex = 0;

                // try from rear of train
                if (thisTrain.Train.PresentPosition[1].RouteListIndex > 0)
                {
                    routeIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(Index, thisTrain.Train.PresentPosition[1].RouteListIndex);
                    validPosition = routeIndex >= 0;
                }
                // if not possible try from front
                else if (thisTrain.Train.PresentPosition[0].RouteListIndex > 0)
                {
                    routeIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(Index, thisTrain.Train.PresentPosition[0].RouteListIndex);
                    validPosition = routeIndex >= 0;
                }

                if (validPosition)
                {
                    CircuitState.TrainReserved = thisTrain;
                }

                // remove from claim or deadlock claim
                CircuitState.TrainClaimed.Remove(thisTrain);

                // get element in routepath to find required alignment

                int thisIndex = -1;

                for (int iElement = 0; iElement < thisRoute.Count && thisIndex < 0; iElement++)
                {
                    thisElement = thisRoute[iElement];
                    if (thisElement.TCSectionIndex == Index)
                    {
                        thisIndex = iElement;
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

                        int leadSectionIndex = -1;
                        if (thisIndex > 0)
                        {
                            thisElement = thisRoute[thisIndex - 1];
                            leadSectionIndex = thisElement.TCSectionIndex;

                            alignSwitchPins(leadSectionIndex);
                        }

                        // set active pins for trailing section

                        int trailSectionIndex = -1;
                        if (thisIndex <= thisRoute.Count - 2)
                        {
                            thisElement = thisRoute[thisIndex + 1];
                            trailSectionIndex = thisElement.TCSectionIndex;

                            alignSwitchPins(trailSectionIndex);
                        }

                        // reset signals which routed through this junction

                        foreach (int thisSignalIndex in SignalsPassingRoutes)
                        {
                            Signal thisSignal = signals.SignalObjects[thisSignalIndex];
                            thisSignal.ResetRoute(Index);
                        }
                        SignalsPassingRoutes.Clear();
                    }
                }

                // enable all signals along section in direction of train
                // do not enable those signals who are part of NORMAL signal

                if (thisIndex < 0) return; //Added by JTang
                thisElement = thisRoute[thisIndex];
                Heading direction = (Heading)thisElement.Direction;

                for (int fntype = 0; fntype < signals.ORTSSignalTypeCount; fntype++)
                {
                    TrackCircuitSignalList thisSignalList = CircuitItems.TrackCircuitSignals[direction][fntype];
                    foreach (TrackCircuitSignalItem thisItem in thisSignalList)
                    {
                        Signal thisSignal = thisItem.Signal;
                        if (!thisSignal.isSignalNormal())
                        {
                            thisSignal.enabledTrain = thisTrain;
                        }
                    }
                }

                // also set enabled for speedpost to process speed signals
                TrackCircuitSignalList thisSpeedpostList = CircuitItems.TrackCircuitSpeedPosts[direction];
                foreach (TrackCircuitSignalItem thisItem in thisSpeedpostList)
                {
                    Signal thisSpeedpost = thisItem.Signal;
                    if (!thisSpeedpost.isSignalNormal())
                    {
                        thisSpeedpost.enabledTrain = thisTrain;
                    }
                }

                // set deadlock trap if required - do not set deadlock if wait is required at this location

                if (thisTrain.Train.DeadlockInfo.ContainsKey(Index))
                {
                    bool waitRequired = thisTrain.Train.CheckWaitCondition(Index);
                    if (!waitRequired)
                    {
                        SetDeadlockTrap(thisTrain.Train, thisTrain.Train.DeadlockInfo[Index]);
                    }
                }

                // if start of alternative route, set deadlock keys for other end
                // check using path based deadlock processing

                if (!signals.UseLocationPassingPaths)
                {
                    if (thisElement != null && thisElement.StartAlternativePath != null)
                    {
                        TrackCircuitSection endSection = TrackCircuitList[thisElement.StartAlternativePath[1]];

                        // no deadlock yet active
                        if (thisTrain.Train.DeadlockInfo.ContainsKey(endSection.Index))
                        {
                            endSection.SetDeadlockTrap(thisTrain.Train, thisTrain.Train.DeadlockInfo[endSection.Index]);
                        }
                        else if (endSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number) && !endSection.DeadlockAwaited.Contains(thisTrain.Train.Number))
                        {
                            endSection.DeadlockAwaited.Add(thisTrain.Train.Number);
                        }
                    }
                }
                // search for path using location based deadlock processing

                else
                {
                    if (thisElement != null && thisElement.FacingPoint && DeadlockReference >= 0)
                    {
                        DeadlockInfo sectionDeadlockInfo = signals.DeadlockInfoList[DeadlockReference];
                        if (sectionDeadlockInfo.HasTrainAndSubpathIndex(thisTrain.Train.Number, thisTrain.Train.TCRoute.activeSubpath))
                        {
                            int trainAndSubpathIndex = sectionDeadlockInfo.GetTrainAndSubpathIndex(thisTrain.Train.Number, thisTrain.Train.TCRoute.activeSubpath);
                            int availableRoute = sectionDeadlockInfo.TrainReferences[trainAndSubpathIndex][0];
                            int endSectionIndex = sectionDeadlockInfo.AvailablePathList[availableRoute].EndSectionIndex;
                            TrackCircuitSection endSection = TrackCircuitList[endSectionIndex];

                            // no deadlock yet active - do not set deadlock if train has wait within deadlock section
                            if (thisTrain.Train.DeadlockInfo.ContainsKey(endSection.Index))
                            {
                                if (!thisTrain.Train.HasActiveWait(Index, endSection.Index))
                                {
                                    endSection.SetDeadlockTrap(thisTrain.Train, thisTrain.Train.DeadlockInfo[endSection.Index]);
                                }
                            }
                            else if (endSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number) && !endSection.DeadlockAwaited.Contains(thisTrain.Train.Number))
                            {
                                endSection.DeadlockAwaited.Add(thisTrain.Train.Number);
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

        public void Claim(Train.TrainRouted thisTrain)
        {
            if (!CircuitState.TrainClaimed.ContainsTrain(thisTrain))
            {
                CircuitState.TrainClaimed.Enqueue(thisTrain);
            }

            // set deadlock trap if required
            if (thisTrain.Train.DeadlockInfo.ContainsKey(Index))
            {
                SetDeadlockTrap(thisTrain.Train, thisTrain.Train.DeadlockInfo[Index]);
            }
        }

        //================================================================================================//
        /// <summary>
        /// insert pre-reserve
        /// </summary>

        public void PreReserve(Train.TrainRouted thisTrain)
        {
            if (!CircuitState.TrainPreReserved.ContainsTrain(thisTrain))
            {
                CircuitState.TrainPreReserved.Enqueue(thisTrain);
            }
        }

        //================================================================================================//
        /// <summary>
        /// set track occupied
        /// </summary>

        public void SetOccupied(Train.TrainRouted thisTrain)
        {
            SetOccupied(thisTrain, Convert.ToInt32(thisTrain.Train.DistanceTravelledM));
        }


        public void SetOccupied(Train.TrainRouted thisTrain, int reqDistanceTravelledM)
        {
#if DEBUG_REPORTS
			File.AppendAllText(@"C:\temp\printproc.txt",
				String.Format("Occupy section {0} for train {1}\n",
				this.Index,
				thisTrain.Train.Number));
#endif
#if DEBUG_DEADLOCK
            File.AppendAllText(@"C:\temp\deadlock.txt",
                String.Format("Occupy section {0} for train {1}\n",
                this.Index,
                thisTrain.Train.Number));
#endif
            if (thisTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Occupy section {0} for train {1}\n",
                    this.Index,
                    thisTrain.Train.Number));
            }

            int routeIndex = thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex].GetRouteIndex(Index, thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex == 0 ? 1 : 0].RouteListIndex);
            int direction = routeIndex < 0 ? 0 : thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex][routeIndex].Direction;
            CircuitState.OccupationState.Add(thisTrain, direction);
            CircuitState.Forced = false;
            thisTrain.Train.OccupiedTrack.Add(this);

            // clear all reservations
            CircuitState.TrainReserved = null;
            CircuitState.SignalReserved = -1;

            CircuitState.TrainClaimed.Remove(thisTrain);
            CircuitState.TrainPreReserved.Remove(thisTrain);

            float distanceToClear = reqDistanceTravelledM + Length + thisTrain.Train.standardOverlapM;

            // add to clear list of train

            if (CircuitType == TrackCircuitType.Junction)
            {
                if (Pins[direction, 1].Link >= 0)  // facing point
                {
                    if (Overlap > 0)
                    {
                        distanceToClear = reqDistanceTravelledM + Length + Convert.ToSingle(Overlap);
                    }
                    else
                    {
                        distanceToClear = reqDistanceTravelledM + Length + thisTrain.Train.junctionOverlapM;
                    }
                }
                else
                {
                    distanceToClear = reqDistanceTravelledM + Length + thisTrain.Train.standardOverlapM;
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
                    distanceToClear = reqDistanceTravelledM + Length + thisTrain.Train.junctionOverlapM;
                }
            }

            Train.TCPosition presentFront = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex];
            int reverseDirectionIndex = thisTrain.TrainRouteDirectionIndex == 0 ? 1 : 0;
            Train.TCPosition presentRear = thisTrain.Train.PresentPosition[reverseDirectionIndex];

            // correct offset if position direction is not equal to route direction
            float frontOffset = presentFront.TCOffset;
            if (presentFront.RouteListIndex >= 0 &&
                presentFront.TCDirection != thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex][presentFront.RouteListIndex].Direction)
                frontOffset = Length - frontOffset;

            float rearOffset = presentRear.TCOffset;
            if (presentRear.RouteListIndex >= 0 &&
                presentRear.TCDirection != thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex][presentRear.RouteListIndex].Direction)
                rearOffset = Length - rearOffset;

            if (presentFront.TCSectionIndex == Index)
            {
                distanceToClear += thisTrain.Train.Length - frontOffset;
            }
            else if (presentRear.TCSectionIndex == Index)
            {
                distanceToClear -= rearOffset;
            }
            else
            {
                distanceToClear += thisTrain.Train.Length;
            }

            // make sure items are cleared in correct sequence
            float? lastDistance = thisTrain.Train.requiredActions.GetLastClearingDistance();
            if (lastDistance.HasValue && lastDistance > distanceToClear)
            {
                distanceToClear = lastDistance.Value;
            }

            thisTrain.Train.requiredActions.InsertAction(new Train.ClearSectionItem(distanceToClear, Index));

            if (thisTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    "Set clear action : section : " + Index + " : distance to clear : " + distanceToClear + "\n");
            }

            // set deadlock trap if required

            if (thisTrain.Train.DeadlockInfo.ContainsKey(Index))
            {
                SetDeadlockTrap(thisTrain.Train, thisTrain.Train.DeadlockInfo[Index]);
            }

            // check for deadlock trap if taking alternative path

            if (thisTrain.Train.TCRoute != null && thisTrain.Train.TCRoute.activeAltpath >= 0)
            {
                Train.TCSubpathRoute altRoute = thisTrain.Train.TCRoute.TCAlternativePaths[thisTrain.Train.TCRoute.activeAltpath];
                Train.TCRouteElement startElement = altRoute[0];
                if (Index == startElement.TCSectionIndex)
                {
                    TrackCircuitSection endSection = TrackCircuitList[altRoute[altRoute.Count - 1].TCSectionIndex];

                    // set deadlock trap for next section

                    if (thisTrain.Train.DeadlockInfo.ContainsKey(endSection.Index))
                    {
                        endSection.SetDeadlockTrap(thisTrain.Train, thisTrain.Train.DeadlockInfo[endSection.Index]);
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
        public void ClearOccupied(Train.TrainRouted thisTrain, bool resetEndSignal)
        {

#if DEBUG_REPORTS
			File.AppendAllText(@"C:\temp\printproc.txt",
				String.Format("Clear section {0} for train {1}\n",
				this.Index,
				thisTrain.Train.Number));
#endif
            if (thisTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Clear section {0} for train {1}\n",
                    this.Index,
                    thisTrain.Train.Number));
            }

            if (CircuitState.OccupationState.ContainsTrain(thisTrain))
            {
                CircuitState.OccupationState.RemoveTrain(thisTrain);
                thisTrain.Train.OccupiedTrack.Remove(this);
            }

            RemoveTrain(thisTrain, false);   // clear occupy first to prevent loop, next clear all hanging references

            ClearDeadlockTrap(thisTrain.Train.Number); // clear deadlock traps

            // if signal at either end is still enabled for this train, reset the signal

            foreach (Heading heading in EnumExtension.GetValues<Heading>())
            {
                if (EndSignals[(int)heading] is Signal endSignal)
                {
                    if (endSignal.enabledTrain == thisTrain && resetEndSignal)
                    {
                        endSignal.resetSignalEnabled();
                    }
                }

                // disable all signals along section if enabled for this train

                for (int fntype = 0; fntype < signals.ORTSSignalTypeCount; fntype++)
                {
                    TrackCircuitSignalList thisSignalList = CircuitItems.TrackCircuitSignals[heading][fntype];
                    foreach (TrackCircuitSignalItem thisItem in thisSignalList)
                    {
                        Signal thisSignal = thisItem.Signal;
                        if (thisSignal.enabledTrain == thisTrain)
                        {
                            thisSignal.resetSignalEnabled();
                        }
                    }
                }

                // also reset enabled for speedpost to process speed signals
                TrackCircuitSignalList thisSpeedpostList = CircuitItems.TrackCircuitSpeedPosts[heading];
                foreach (TrackCircuitSignalItem thisItem in thisSpeedpostList)
                {
                    Signal thisSpeedpost = thisItem.Signal;
                    if (!thisSpeedpost.isSignalNormal())
                    {
                        thisSpeedpost.resetSignalEnabled();
                    }
                }
            }

            // if section is Junction or Crossover, reset active pins but only if section is not occupied by other train

            if ((CircuitType == TrackCircuitType.Junction || CircuitType == TrackCircuitType.Crossover) && CircuitState.OccupationState.Count == 0)
            {
                deAlignSwitchPins();

                // reset signals which routed through this junction

                foreach (int thisSignalIndex in SignalsPassingRoutes)
                {
                    Signal thisSignal = signals.SignalObjects[thisSignalIndex];
                    thisSignal.ResetRoute(Index);
                }
                SignalsPassingRoutes.Clear();
            }

            // reset manual junction setting if train is in manual mode

            if (thisTrain.Train.ControlMode == Train.TRAIN_CONTROL.MANUAL && CircuitType == TrackCircuitType.Junction && JunctionSetManual >= 0)
            {
                JunctionSetManual = -1;
            }

            // if no longer occupied and pre-reserved not empty, promote first entry of prereserved

            if (CircuitState.OccupationState.Count <= 0 && CircuitState.TrainPreReserved.Count > 0)
            {
                Train.TrainRouted nextTrain = CircuitState.TrainPreReserved.Dequeue();
                Train.TCSubpathRoute RoutePart = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex];

                Reserve(nextTrain, RoutePart);
            }

        }

        /// <summary>
        /// unrouted train
        /// </summary>
        public void ClearOccupied(Train thisTrain, bool resetEndSignal)
        {
            ClearOccupied(thisTrain.routedForward, resetEndSignal); // forward
            ClearOccupied(thisTrain.routedBackward, resetEndSignal);// backward
        }

        /// <summary>
        /// only reset occupied state - use in case of reversal or mode change when train has not actually moved
        /// routed train
        /// </summary>
        public void ResetOccupied(Train.TrainRouted thisTrain)
        {

            if (CircuitState.OccupationState.ContainsTrain(thisTrain))
            {
                CircuitState.OccupationState.RemoveTrain(thisTrain);
                thisTrain.Train.OccupiedTrack.Remove(this);

                if (thisTrain.Train.CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                        "Reset Occupy for section : " + Index + "\n");
                }
            }

        }

        /// <summary>
        /// unrouted train
        /// </summary>
        public void ResetOccupied(Train thisTrain)
        {
            ResetOccupied(thisTrain.routedForward); // forward
            ResetOccupied(thisTrain.routedBackward);// backward
        }

        //================================================================================================//
        /// <summary>
        /// Remove train from section
        /// </summary>

        /// <summary>
        /// routed train
        /// </summary>
        public void RemoveTrain(Train.TrainRouted thisTrain, bool resetEndSignal)
        {
#if DEBUG_REPORTS
			File.AppendAllText(@"C:\temp\printproc.txt",
				String.Format("Remove train from section {0} for train {1}\n",
				this.Index,
				thisTrain.Train.Number));
#endif
            if (thisTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Remove train from section {0} for train {1}\n",
                    this.Index,
                    thisTrain.Train.Number));
            }

            if (CircuitState.OccupiedByThisTrain(thisTrain))
            {
                ClearOccupied(thisTrain, resetEndSignal);
            }

            if (CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train == thisTrain.Train)
            {
                CircuitState.TrainReserved = null;
                ClearOccupied(thisTrain, resetEndSignal);    // call clear occupy to reset signals and switches //
            }

            CircuitState.TrainClaimed.Remove(thisTrain);
            CircuitState.TrainPreReserved.Remove(thisTrain);
        }


        /// <summary>
        /// unrouted train
        /// </summary>
        public void RemoveTrain(Train thisTrain, bool resetEndSignal)
        {
            RemoveTrain(thisTrain.routedForward, resetEndSignal);
            RemoveTrain(thisTrain.routedBackward, resetEndSignal);
        }

        //================================================================================================//
        /// <summary>
        /// Remove train reservations from section
        /// </summary>

        public void UnreserveTrain(Train.TrainRouted thisTrain, bool resetEndSignal)
        {
            if (CircuitState.TrainReserved != null && CircuitState.TrainReserved.Train == thisTrain.Train)
            {
                CircuitState.TrainReserved = null;
                ClearOccupied(thisTrain, resetEndSignal);    // call clear occupy to reset signals and switches //
            }

            CircuitState.TrainClaimed.Remove(thisTrain);
            CircuitState.TrainPreReserved.Remove(thisTrain);
        }

        //================================================================================================//
        /// <summary>
        /// Remove train clain from section
        /// </summary>

        public void UnclaimTrain(Train.TrainRouted thisTrain)
        {
            CircuitState.TrainClaimed.Remove(thisTrain);
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

        public void ClearReversalClaims(Train.TrainRouted thisTrain)
        {
            // check if any trains have claimed this section
            List<Train.TrainRouted> claimedTrains = new List<Train.TrainRouted>(CircuitState.TrainClaimed);

            CircuitState.TrainClaimed.Clear();
            foreach (Train.TrainRouted claimingTrain in claimedTrains)
            {
                claimingTrain.Train.ClaimState = false; // reset train claim state
            }

            // get train route
            Train.TCSubpathRoute usedRoute = thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex];
            int routeIndex = usedRoute.GetRouteIndex(Index, 0);

            // run down route and clear all claims for found trains, until end 
            for (int iRouteIndex = routeIndex + 1; iRouteIndex <= usedRoute.Count - 1 && (claimedTrains.Count > 0); iRouteIndex++)
            {
                TrackCircuitSection nextSection = TrackCircuitList[usedRoute[iRouteIndex].TCSectionIndex];

                for (int iTrain = claimedTrains.Count - 1; iTrain >= 0; iTrain--)
                {
                    Train.TrainRouted claimingTrain = claimedTrains[iTrain];

                    if (!nextSection.CircuitState.TrainClaimed.Remove(thisTrain))
                    {
                        claimedTrains.Remove(claimingTrain);
                    }
                }

                nextSection.Claim(thisTrain);
            }
        }

        //================================================================================================//
        /// <summary>
        /// align pins switch or crossover
        /// </summary>

        public void alignSwitchPins(int linkedSectionIndex)
        {
            int alignDirection = -1;  // pin direction for leading section
            int alignLink = -1;       // link index for leading section

            for (int iDirection = 0; iDirection <= 1; iDirection++)
            {
                for (int iLink = 0; iLink <= 1; iLink++)
                {
                    if (Pins[iDirection, iLink].Link == linkedSectionIndex)
                    {
                        alignDirection = iDirection;
                        alignLink = iLink;
                    }
                }
            }

            if (alignDirection >= 0)
            {
                ActivePins[alignDirection, 0].Link = -1;
                ActivePins[alignDirection, 1].Link = -1;

                ActivePins[alignDirection, alignLink].Link =
                        Pins[alignDirection, alignLink].Link;
                ActivePins[alignDirection, alignLink].Direction =
                        Pins[alignDirection, alignLink].Direction;

                TrackCircuitSection linkedSection = TrackCircuitList[linkedSectionIndex];
                for (int iDirection = 0; iDirection <= 1; iDirection++)
                {
                    for (int iLink = 0; iLink <= 1; iLink++)
                    {
                        if (linkedSection.Pins[iDirection, iLink].Link == Index)
                        {
                            linkedSection.ActivePins[iDirection, iLink].Link = Index;
                            linkedSection.ActivePins[iDirection, iLink].Direction =
                                    linkedSection.Pins[iDirection, iLink].Direction;
                        }
                    }
                }
            }

            // if junction, align physical switch

            if (CircuitType == TrackCircuitType.Junction)
            {
                int switchPos = -1;
                if (ActivePins[1, 0].Link != -1)
                    switchPos = 0;
                if (ActivePins[1, 1].Link != -1)
                    switchPos = 1;

                if (switchPos >= 0)
                {
                    signals.setSwitch(OriginalIndex, switchPos, this);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// de-align active switch pins
        /// </summary>

        public void deAlignSwitchPins()
        {
            for (int iDirection = 0; iDirection <= 1; iDirection++)
            {
                if (Pins[iDirection, 1].Link > 0)     // active switchable end
                {
                    for (int iLink = 0; iLink <= 1; iLink++)
                    {
                        int activeLink = Pins[iDirection, iLink].Link;
                        int activeDirection = Pins[iDirection, iLink].Direction == 0 ? 1 : 0;
                        ActivePins[iDirection, iLink].Link = -1;

                        TrackCircuitSection linkSection = TrackCircuitList[activeLink];
                        linkSection.ActivePins[activeDirection, 0].Link = -1;
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Get section state for request clear node
        /// Method is put through to train class because of differences between activity and timetable mode
        /// </summary>

        public bool GetSectionStateClearNode(Train.TrainRouted thisTrain, int elementDirection, Train.TCSubpathRoute routePart)
        {
            bool returnValue = thisTrain.Train.TrainGetSectionStateClearNode(elementDirection, routePart, this);
            return (returnValue);
        }

        //================================================================================================//
        /// <summary>
        /// Get state of single section
        /// Check for train
        /// </summary>

        public Signal.InternalBlockstate getSectionState(Train.TrainRouted thisTrain, int direction,
                        Signal.InternalBlockstate passedBlockstate, Train.TCSubpathRoute thisRoute, int signalIndex)
        {
            Signal.InternalBlockstate thisBlockstate;
            Signal.InternalBlockstate localBlockstate = Signal.InternalBlockstate.Reservable;  // default value
            bool stateSet = false;

            TrackCircuitState thisState = CircuitState;

            // track occupied - check speed and direction - only for normal sections

            if (thisTrain != null && thisState.OccupationState.ContainsTrain(thisTrain))
            {
                localBlockstate = Signal.InternalBlockstate.Reserved;  // occupied by own train counts as reserved
                stateSet = true;
            }
            else if (thisState.Occupied(direction, true))
            {
                {
                    localBlockstate = Signal.InternalBlockstate.OccupiedSameDirection;
                    stateSet = true;
                }
            }
            else
            {
                int reqDirection = direction == 0 ? 1 : 0;
                if (thisState.Occupied(reqDirection, false))
                {
                    localBlockstate = Signal.InternalBlockstate.OccupiedOppositeDirection;
                    stateSet = true;
                }
            }

            // for junctions or cross-overs, check route selection

            if (CircuitType == TrackCircuitType.Junction || CircuitType == TrackCircuitType.Crossover)
            {
                if (thisState.Occupied())    // there is a train on the switch
                {
                    if (thisRoute == null)  // no route from signal - always report switch blocked
                    {
                        localBlockstate = Signal.InternalBlockstate.Blocked;
                        stateSet = true;
                    }
                    else
                    {
                        int reqPinIndex = -1;
                        for (int iPinIndex = 0; iPinIndex <= 1 && reqPinIndex < 0; iPinIndex++)
                        {
                            if (Pins[iPinIndex, 1].Link > 0)
                                reqPinIndex = iPinIndex;  // switchable end
                        }

                        int switchEnd = -1;
                        for (int iSwitch = 0; iSwitch <= 1; iSwitch++)
                        {
                            int nextSectionIndex = Pins[reqPinIndex, iSwitch].Link;
                            int routeListIndex = thisRoute == null ? -1 : thisRoute.GetRouteIndex(nextSectionIndex, 0);
                            if (routeListIndex >= 0)
                                switchEnd = iSwitch;  // required exit
                        }
                        // allow if switch not active (both links dealligned)
                        int otherEnd = switchEnd == 0 ? 1 : 0;
                        if (switchEnd < 0 || (ActivePins[reqPinIndex, switchEnd].Link < 0 && ActivePins[reqPinIndex, otherEnd].Link >= 0)) // no free exit available or switch misaligned
                        {
                            localBlockstate = Signal.InternalBlockstate.Blocked;
                            stateSet = true;
                        }
                    }
                }
            }

            // track reserved - check direction

            if (thisState.TrainReserved != null && thisTrain != null && !stateSet)
            {
                Train.TrainRouted reservedTrain = thisState.TrainReserved;
                if (reservedTrain.Train == thisTrain.Train)
                {
                    localBlockstate = Signal.InternalBlockstate.Reserved;
                    stateSet = true;
                }
                else
                {
                    if (MPManager.IsMultiPlayer())
                    {
                        var reservedTrainStillThere = false;
                        foreach (var s in this.EndSignals)
                        {
                            if (s != null && s.enabledTrain != null && s.enabledTrain.Train == reservedTrain.Train) reservedTrainStillThere = true;
                        }

                        if (reservedTrainStillThere == true && reservedTrain.Train.ValidRoute[0] != null && reservedTrain.Train.PresentPosition[0] != null &&
                            reservedTrain.Train.GetDistanceToTrain(this.Index, 0.0f) > 0)
                            localBlockstate = Signal.InternalBlockstate.ReservedOther;
                        else
                        {
                            //if (reservedTrain.Train.RearTDBTraveller.DistanceTo(this.
                            thisState.TrainReserved = thisTrain;
                            localBlockstate = Signal.InternalBlockstate.Reserved;
                        }
                    }
                    else
                    {
                        localBlockstate = Signal.InternalBlockstate.ReservedOther;
                    }
                }
            }

            // signal reserved - reserved for other

            if (thisState.SignalReserved >= 0 && thisState.SignalReserved != signalIndex)
            {
                localBlockstate = Signal.InternalBlockstate.ReservedOther;
                stateSet = true;
            }

            // track claimed

            if (!stateSet && thisTrain != null && thisState.TrainClaimed.Count > 0 && thisState.TrainClaimed.PeekTrain() != thisTrain.Train)
            {
                localBlockstate = Signal.InternalBlockstate.Open;
                stateSet = true;
            }

            // wait condition

            if (thisTrain != null)
            {
                bool waitRequired = thisTrain.Train.CheckWaitCondition(Index);

                if ((!stateSet || localBlockstate < Signal.InternalBlockstate.ForcedWait) && waitRequired)
                {
                    localBlockstate = Signal.InternalBlockstate.ForcedWait;
                    thisTrain.Train.ClaimState = false; // claim not allowed for forced wait
                }
            }

            // deadlock trap - may not set deadlock if wait is active 

            if (thisTrain != null && localBlockstate != Signal.InternalBlockstate.ForcedWait && DeadlockTraps.ContainsKey(thisTrain.Train.Number))
            {
                bool acceptDeadlock = thisTrain.Train.VerifyDeadlock(DeadlockTraps[thisTrain.Train.Number]);

                if (acceptDeadlock)
                {
                    localBlockstate = Signal.InternalBlockstate.Blocked;
                    stateSet = true;
                    if (!DeadlockAwaited.Contains(thisTrain.Train.Number))
                        DeadlockAwaited.Add(thisTrain.Train.Number);
                }
            }

            thisBlockstate = localBlockstate > passedBlockstate ? localBlockstate : passedBlockstate;

            return (thisBlockstate);
        }


        //================================================================================================//
        /// <summary>
        /// Test only if section reserved to train
        /// </summary>

        public bool CheckReserved(Train.TrainRouted thisTrain)
        {
            var reserved = false;
            if (CircuitState.TrainReserved != null && thisTrain != null)
            {
                Train.TrainRouted reservedTrain = CircuitState.TrainReserved;
                if (reservedTrain.Train == thisTrain.Train)
                {
                    reserved = true;
                }
            }
            return reserved;
        }

        //================================================================================================//
        /// <summary>
        /// Test if train ahead and calculate distance to that train (front or rear depending on direction)
        /// </summary>

        public Dictionary<Train, float> TestTrainAhead(Train thisTrain, float offset, int direction)
        {
            Train trainFound = null;
            float distanceTrainAheadM = Length + 1.0f; // ensure train is always within section

            List<Train.TrainRouted> trainsInSection = CircuitState.TrainsOccupying();

            // remove own train
            if (thisTrain != null)
            {
                for (int iindex = trainsInSection.Count - 1; iindex >= 0; iindex--)
                {
                    if (trainsInSection[iindex].Train == thisTrain)
                        trainsInSection.RemoveAt(iindex);
                }
            }

            // search for trains in section
            foreach (Train.TrainRouted nextTrain in trainsInSection)
            {
                int nextTrainRouteIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(Index, 0);
                if (nextTrainRouteIndex >= 0)
                {
                    Train.TCPosition nextFront = nextTrain.Train.PresentPosition[nextTrain.TrainRouteDirectionIndex];
                    int reverseDirection = nextTrain.TrainRouteDirectionIndex == 0 ? 1 : 0;
                    Train.TCPosition nextRear = nextTrain.Train.PresentPosition[reverseDirection];

                    Train.TCRouteElement thisElement = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex][nextTrainRouteIndex];
                    if (thisElement.Direction == direction) // same direction, so if the train is in front we're looking at the rear of the train
                    {
                        if (nextRear.TCSectionIndex == Index) // rear of train is in same section
                        {
                            float thisTrainDistanceM = nextRear.TCOffset;

                            if (thisTrainDistanceM < distanceTrainAheadM && nextRear.TCOffset >= offset) // train is nearest train and in front
                            {
                                distanceTrainAheadM = thisTrainDistanceM;
                                trainFound = nextTrain.Train;
                            }
                            else if (nextRear.TCOffset < offset && nextRear.TCOffset + nextTrain.Train.Length > offset) // our end is in the middle of the train
                            {
                                distanceTrainAheadM = offset; // set distance to 0 (offset is deducted later)
                                trainFound = nextTrain.Train;
                            }
                        }
                        else
                        {
                            // try to use next train indices
                            int nextRouteFrontIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(nextFront.TCSectionIndex, 0);
                            int nextRouteRearIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(nextRear.TCSectionIndex, 0);
                            int usedTrainRouteIndex = nextTrainRouteIndex;

                            // if not on route, try this trains route
                            if (thisTrain != null && (nextRouteFrontIndex < 0 || nextRouteRearIndex < 0))
                            {
                                nextRouteFrontIndex = thisTrain.ValidRoute[0].GetRouteIndex(nextFront.TCSectionIndex, 0);
                                nextRouteRearIndex = thisTrain.ValidRoute[0].GetRouteIndex(nextRear.TCSectionIndex, 0);
                                usedTrainRouteIndex = thisTrain.ValidRoute[0].GetRouteIndex(Index, 0);
                            }

                            // if not found either, build temp route
                            if (nextRouteFrontIndex < 0 || nextRouteRearIndex < 0)
                            {
                                Train.TCSubpathRoute tempRoute = signals.BuildTempRoute(nextTrain.Train, nextFront.TCSectionIndex, nextFront.TCOffset, nextFront.TCDirection,
                                    nextTrain.Train.Length, true, true, false);
                                nextRouteFrontIndex = tempRoute.GetRouteIndex(nextFront.TCSectionIndex, 0);
                                nextRouteRearIndex = tempRoute.GetRouteIndex(nextRear.TCSectionIndex, 0);
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

                                if (thisTrain != null && thisTrain.ValidRoute != null)
                                {
                                    int lastSectionIndex = thisTrain.ValidRoute[0].GetRouteIndex(nextRear.TCSectionIndex, thisTrain.PresentPosition[0].RouteListIndex);
                                    if (lastSectionIndex >= thisTrain.PresentPosition[0].RouteListIndex)
                                    {
                                        distanceTrainAheadM = Length;  // offset is deducted later
                                        for (int isection = nextTrainRouteIndex + 1; isection <= nextRear.RouteListIndex - 1; isection++)
                                        {
                                            distanceTrainAheadM += TrackCircuitList[nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex][isection].TCSectionIndex].Length;
                                        }
                                        distanceTrainAheadM += nextTrain.Train.PresentPosition[1].TCOffset;
                                        trainFound = nextTrain.Train;
                                    }
                                }
                            }
                        }
                    }
                    else // reverse direction, so we're looking at the front - use section length - offset as position
                    {
                        float thisTrainOffset = Length - nextFront.TCOffset;
                        if (nextFront.TCSectionIndex == Index)  // front of train in section
                        {
                            float thisTrainDistanceM = thisTrainOffset;

                            if (thisTrainDistanceM < distanceTrainAheadM && thisTrainOffset >= offset) // train is nearest train and in front
                            {
                                distanceTrainAheadM = thisTrainDistanceM;
                                trainFound = nextTrain.Train;
                            }
                            // extra test : if front is beyond other train but rear is not, train is considered to be still in front (at distance = offset)
                            // this can happen in pre-run mode due to large interval
                            if (thisTrain != null && thisTrainDistanceM < distanceTrainAheadM && thisTrainOffset < offset)
                            {
                                if ((!signals.Simulator.TimetableMode && thisTrainOffset >= (offset - nextTrain.Train.Length)) ||
                                    (signals.Simulator.TimetableMode && thisTrainOffset >= (offset - thisTrain.Length)))
                                {
                                    distanceTrainAheadM = offset;
                                    trainFound = nextTrain.Train;
                                }
                            }
                        }
                        else
                        {
                            int nextRouteFrontIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(nextFront.TCSectionIndex, 0);
                            int nextRouteRearIndex = nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex].GetRouteIndex(nextRear.TCSectionIndex, 0);
                            int usedTrainRouteIndex = nextTrainRouteIndex;

                            // if not on route, try this trains route
                            if (thisTrain != null && (nextRouteFrontIndex < 0 || nextRouteRearIndex < 0))
                            {
                                nextRouteFrontIndex = thisTrain.ValidRoute[0].GetRouteIndex(nextFront.TCSectionIndex, 0);
                                nextRouteRearIndex = thisTrain.ValidRoute[0].GetRouteIndex(nextRear.TCSectionIndex, 0);
                                usedTrainRouteIndex = thisTrain.ValidRoute[0].GetRouteIndex(Index, 0);
                            }

                            // if not found either, build temp route
                            if (nextRouteFrontIndex < 0 || nextRouteRearIndex < 0)
                            {
                                Train.TCSubpathRoute tempRoute = signals.BuildTempRoute(nextTrain.Train, nextFront.TCSectionIndex, nextFront.TCOffset, nextFront.TCDirection,
                                    nextTrain.Train.Length, true, true, false);
                                nextRouteFrontIndex = tempRoute.GetRouteIndex(nextFront.TCSectionIndex, 0);
                                nextRouteRearIndex = tempRoute.GetRouteIndex(nextRear.TCSectionIndex, 0);
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
                                if (thisTrain != null && thisTrain.ValidRoute != null)
                                {
                                    int lastSectionIndex = thisTrain.ValidRoute[0].GetRouteIndex(nextRear.TCSectionIndex, thisTrain.PresentPosition[0].RouteListIndex);
                                    if (lastSectionIndex > thisTrain.PresentPosition[0].RouteListIndex)
                                    {
                                        distanceTrainAheadM = Length;  // offset is deducted later
                                        for (int isection = nextTrainRouteIndex + 1; isection <= nextRear.RouteListIndex - 1; isection++)
                                        {
                                            distanceTrainAheadM += TrackCircuitList[nextTrain.Train.ValidRoute[nextTrain.TrainRouteDirectionIndex][isection].TCSectionIndex].Length;
                                        }
                                        distanceTrainAheadM += nextTrain.Train.PresentPosition[1].TCOffset;
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

        public TrackPin GetNextActiveLink(int direction, int lastIndex)
        {

            // Crossover

            if (CircuitType == TrackCircuitType.Crossover)
            {
                int inPinIndex = direction == 0 ? 1 : 0;
                if (Pins[inPinIndex, 0].Link == lastIndex)
                {
                    return (ActivePins[direction, 0]);
                }
                else if (Pins[inPinIndex, 1].Link == lastIndex)
                {
                    return (ActivePins[direction, 1]);
                }
                else
                {
                    TrackPin dummyPin = new TrackPin(-1, -1);
                    return (dummyPin);
                }
            }

            // All other sections

            if (ActivePins[direction, 0].Link > 0)
            {
                return (ActivePins[direction, 0]);
            }

            return (ActivePins[direction, 1]);
        }

        //================================================================================================//
        /// <summary>
        /// Get distance between objects
        /// </summary>

        public float GetDistanceBetweenObjects(int startSectionIndex, float startOffset, int startDirection,
            int endSectionIndex, float endOffset)
        {
            int thisSectionIndex = startSectionIndex;
            int direction = startDirection;

            TrackCircuitSection thisSection = TrackCircuitList[thisSectionIndex];

            float distanceM = 0.0f;
            int lastIndex = -2;  // set to non-occuring value

            while (thisSectionIndex != endSectionIndex && thisSectionIndex > 0)
            {
                distanceM += thisSection.Length;
                TrackPin nextLink = thisSection.GetNextActiveLink(direction, lastIndex);

                lastIndex = thisSectionIndex;
                thisSectionIndex = nextLink.Link;
                direction = nextLink.Direction;

                if (thisSectionIndex > 0)
                {
                    thisSection = TrackCircuitList[thisSectionIndex];
                    if (thisSectionIndex == startSectionIndex)  // loop found - return distance found sofar
                    {
                        distanceM -= startOffset;
                        return (distanceM);
                    }
                }
            }

            // use found distance, correct for begin and end offset

            if (thisSectionIndex == endSectionIndex)
            {
                distanceM += endOffset - startOffset;
                return (distanceM);
            }

            return (-1.0f);
        }

        //================================================================================================//
        /// <summary>
        /// Check if train can be placed in section
        /// </summary>

        public bool CanPlaceTrain(Train thisTrain, float offset, float trainLength)
        {

            if (!IsAvailable(thisTrain))
            {
                if (CircuitState.TrainReserved != null ||
                CircuitState.TrainClaimed.Count > 0)
                {
                    return (false);
                }

                if (DeadlockTraps.ContainsKey(thisTrain.Number))
                {
                    return (false);  // prevent deadlock
                }

                if (CircuitType != TrackCircuitType.Normal) // other than normal and not clear - return false
                {
                    return (false);
                }

                if (offset == 0 && trainLength > Length) // train spans section
                {
                    return (false);
                }

                // get other trains in section

                Dictionary<Train, float> trainInfo = new Dictionary<Train, float>();
                float offsetFromStart = offset;

                // test train ahead of rear end (for non-placed trains, always use direction 0)

                if (thisTrain.PresentPosition[1].TCSectionIndex == Index)
                {
                    trainInfo = TestTrainAhead(thisTrain,
                            offsetFromStart, thisTrain.PresentPosition[1].TCDirection); // rear end in this section, use offset
                }
                else
                {
                    offsetFromStart = 0.0f;
                    trainInfo = TestTrainAhead(thisTrain,
                            0.0f, thisTrain.PresentPosition[1].TCDirection); // test from start
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
                            var trainPosition = trainAhead.Key.PresentPosition[trainAhead.Key.MUDirection == Direction.Forward ? 0 : 1];
                            if (trainPosition.TCSectionIndex == Index && trainAhead.Key.SpeedMpS > 0 && trainPosition.TCDirection != thisTrain.PresentPosition[0].TCDirection)
                            {
                                return (false);   // train is moving towards us
                            }
                        }
                    }
                }

                // test train behind of front end

                int revDirection = thisTrain.PresentPosition[0].TCDirection == 0 ? 1 : 0;
                if (thisTrain.PresentPosition[0].TCSectionIndex == Index)
                {
                    float offsetFromEnd = Length - (trainLength + offsetFromStart);
                    trainInfo = TestTrainAhead(thisTrain, offsetFromEnd, revDirection); // test remaining length
                }
                else
                {
                    trainInfo = TestTrainAhead(thisTrain, 0.0f, revDirection); // test full section
                }

                if (trainInfo.Count > 0)
                {
                    foreach (KeyValuePair<Train, float> trainAhead in trainInfo)
                    {
                        if (trainAhead.Value < trainLength) // train behind not clear
                        {
                            return (false);
                        }
                    }
                }

            }

            return (true);
        }

        //================================================================================================//
        /// <summary>
        /// Set deadlock trap for all trains which deadlock from this section at begin section
        /// </summary>

        public void SetDeadlockTrap(Train thisTrain, List<Dictionary<int, int>> thisDeadlock)
        {
            foreach (Dictionary<int, int> deadlockInfo in thisDeadlock)
            {
                foreach (KeyValuePair<int, int> deadlockDetails in deadlockInfo)
                {
                    int otherTrainNumber = deadlockDetails.Key;
                    Train otherTrain = thisTrain.GetOtherTrainByNumber(deadlockDetails.Key);

                    int endSectionIndex = deadlockDetails.Value;

                    // check if endsection still in path
                    if (thisTrain.ValidRoute[0].GetRouteIndex(endSectionIndex, thisTrain.PresentPosition[0].RouteListIndex) >= 0)
                    {
                        TrackCircuitSection endSection = TrackCircuitList[endSectionIndex];

                        // if other section allready set do not set deadlock
                        if (otherTrain != null && endSection.IsSet(otherTrain, true))
                            break;

                        if (DeadlockTraps.ContainsKey(thisTrain.Number))
                        {
                            List<int> thisTrap = DeadlockTraps[thisTrain.Number];
                            if (thisTrap.Contains(otherTrainNumber))
                                break;  // cannot set deadlock for train which has deadlock on this end
                        }

                        if (endSection.DeadlockTraps.ContainsKey(otherTrainNumber))
                        {
                            if (!endSection.DeadlockTraps[otherTrainNumber].Contains(thisTrain.Number))
                            {
                                endSection.DeadlockTraps[otherTrainNumber].Add(thisTrain.Number);
                            }
                        }
                        else
                        {
                            List<int> deadlockList = new List<int>();
                            deadlockList.Add(thisTrain.Number);
                            endSection.DeadlockTraps.Add(otherTrainNumber, deadlockList);
                        }

                        if (!endSection.DeadlockActives.Contains(thisTrain.Number))
                        {
                            endSection.DeadlockActives.Add(thisTrain.Number);
                        }
                    }
                }
            }
        }
        //================================================================================================//
        /// <summary>
        /// Set deadlock trap for individual train at end section
        /// </summary>

        public void SetDeadlockTrap(int thisTrainNumber, int otherTrainNumber)
        {

#if DEBUG_DEADLOCK
                File.AppendAllText(@"C:\Temp\deadlock.txt",
                    "\n **** Set deadlock " + Index + " for train : " + thisTrainNumber.ToString() + " with train :  " + otherTrainNumber.ToString() + "\n");
#endif

            if (DeadlockTraps.ContainsKey(otherTrainNumber))
            {
                if (!DeadlockTraps[otherTrainNumber].Contains(thisTrainNumber))
                {
                    DeadlockTraps[otherTrainNumber].Add(thisTrainNumber);
                }
            }
            else
            {
                List<int> deadlockList = new List<int>();
                deadlockList.Add(thisTrainNumber);
                DeadlockTraps.Add(otherTrainNumber, deadlockList);
            }

            if (!DeadlockActives.Contains(thisTrainNumber))
            {
                DeadlockActives.Add(thisTrainNumber);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Clear deadlock trap
        /// </summary>

        public void ClearDeadlockTrap(int thisTrainNumber)
        {
            List<int> deadlocksCleared = new List<int>();

            if (DeadlockActives.Contains(thisTrainNumber))
            {

#if DEBUG_DEADLOCK
                File.AppendAllText(@"C:\Temp\deadlock.txt",
                    "\n **** Clearing deadlocks " + Index + " for train : " + thisTrainNumber.ToString() + "\n");
#endif

                foreach (KeyValuePair<int, List<int>> thisDeadlock in DeadlockTraps)
                {
                    if (thisDeadlock.Value.Contains(thisTrainNumber))
                    {
                        thisDeadlock.Value.Remove(thisTrainNumber);
                        if (thisDeadlock.Value.Count <= 0)
                        {
                            deadlocksCleared.Add(thisDeadlock.Key);
                        }
                    }
                }
                DeadlockActives.Remove(thisTrainNumber);
            }

            foreach (int deadlockKey in deadlocksCleared)
            {
                DeadlockTraps.Remove(deadlockKey);
            }

            DeadlockAwaited.Remove(thisTrainNumber);

#if DEBUG_DEADLOCK
            File.AppendAllText(@"C:\Temp\deadlock.txt",
                "\n **** \n");
#endif

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

        public void ClearSectionsOfTrainBehind(Train.TrainRouted trainRouted, TrackCircuitSection startTCSectionIndex)
        {
            int startindex = 0;
            startTCSectionIndex.UnreserveTrain(trainRouted, true);
            for (int iindex = 0; iindex < trainRouted.Train.ValidRoute[0].Count; iindex++)
            {
                if (startTCSectionIndex == TrackCircuitList[trainRouted.Train.ValidRoute[0][iindex].TCSectionIndex])
                {
                    startindex = iindex + 1;
                    break;
                }
            }

            for (int iindex = startindex; iindex < trainRouted.Train.ValidRoute[0].Count; iindex++)
            {
                TrackCircuitSection thisSection = TrackCircuitList[trainRouted.Train.ValidRoute[0][iindex].TCSectionIndex];
                if (thisSection.CircuitState.TrainReserved == null)
                    break;
                thisSection.UnreserveTrain(trainRouted, true);
            }
            // signalRef.BreakDownRouteList(trainRouted.Train.ValidRoute[trainRouted.TrainRouteDirectionIndex], startindex-1, trainRouted);
            // Reset signal behind new train
            for (int iindex = startindex - 2; iindex >= trainRouted.Train.PresentPosition[0].RouteListIndex; iindex--)
            {
                TrackCircuitSection thisSection = TrackCircuitList[trainRouted.Train.ValidRoute[trainRouted.TrainRouteDirectionIndex][iindex].TCSectionIndex];
                Signal thisSignal = thisSection.EndSignals[trainRouted.Train.ValidRoute[trainRouted.TrainRouteDirectionIndex][iindex].Direction];
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
            TrackCircuitSection targetSection = sourceSection.CopyBasic(targetSectionIndex);
            TrackCircuitSection replacementSection = sourceSection.CopyBasic(sourceSectionIndex);

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

            replacementSection.Pins[0, 0].Direction = sourceSection.Pins[0, 0].Direction;
            replacementSection.Pins[0, 0].Link = sourceSection.Pins[0, 0].Link;
            replacementSection.Pins[1, 0].Direction = 1;
            replacementSection.Pins[1, 0].Link = targetSectionIndex;

            targetSection.Pins[0, 0].Direction = 0;
            targetSection.Pins[0, 0].Link = sourceSectionIndex;
            targetSection.Pins[1, 0].Direction = sourceSection.Pins[1, 0].Direction;
            targetSection.Pins[1, 0].Link = sourceSection.Pins[1, 0].Link;

            // update pins on adjacent sections

            int refLinkIndex = targetSection.Pins[1, 0].Link;
            int refLinkDirIndex = targetSection.Pins[1, 0].Direction == 0 ? 1 : 0;
            TrackCircuitSection refLink = TrackCircuitList[refLinkIndex];
            if (refLink.Pins[refLinkDirIndex, 0].Link == sourceSectionIndex)
            {
                refLink.Pins[refLinkDirIndex, 0].Link = targetSectionIndex;
            }
            else if (refLink.Pins[refLinkDirIndex, 1].Link == sourceSectionIndex)
            {
                refLink.Pins[refLinkDirIndex, 1].Link = targetSectionIndex;
            }

            // copy signal information

            for (int itype = 0; itype < sourceSection.CircuitItems.TrackCircuitSignals[0].Count; itype++)
            {
                TrackCircuitSignalList orgSigList = sourceSection.CircuitItems.TrackCircuitSignals[0][itype];
                TrackCircuitSignalList replSigList = replacementSection.CircuitItems.TrackCircuitSignals[0][itype];
                TrackCircuitSignalList newSigList = targetSection.CircuitItems.TrackCircuitSignals[0][itype];

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

            for (int itype = 0; itype < sourceSection.CircuitItems.TrackCircuitSignals[Heading.Reverse].Count; itype++)
            {
                TrackCircuitSignalList orgSigList = sourceSection.CircuitItems.TrackCircuitSignals[Heading.Reverse][itype];
                TrackCircuitSignalList replSigList = replacementSection.CircuitItems.TrackCircuitSignals[Heading.Reverse][itype];
                TrackCircuitSignalList newSigList = targetSection.CircuitItems.TrackCircuitSignals[Heading.Reverse][itype];

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

            orgSpeedList = sourceSection.CircuitItems.TrackCircuitSpeedPosts[Heading.Reverse];
            replSpeedList = replacementSection.CircuitItems.TrackCircuitSpeedPosts[Heading.Reverse];
            newSpeedList = targetSection.CircuitItems.TrackCircuitSpeedPosts[Heading.Reverse];

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

        public static void AddCrossoverJunction(int leadSectionIndex0, int trailSectionIndex0,
                        int leadSectionIndex1, int trailSectionIndex1, int JnIndex,
                        CrossOverInfo CrossOver, TrackSectionsFile tsectiondat)
        {
            TrackCircuitSection leadSection0 = TrackCircuitList[leadSectionIndex0];
            TrackCircuitSection leadSection1 = TrackCircuitList[leadSectionIndex1];
            TrackCircuitSection trailSection0 = TrackCircuitList[trailSectionIndex0];
            TrackCircuitSection trailSection1 = TrackCircuitList[trailSectionIndex1];
            TrackCircuitSection JnSection = new TrackCircuitSection(JnIndex);

            JnSection.OriginalIndex = leadSection0.OriginalIndex;
            JnSection.CircuitType = TrackCircuitType.Crossover;
            JnSection.Length = 0;

            leadSection0.Pins[1, 0].Link = JnIndex;
            leadSection1.Pins[1, 0].Link = JnIndex;
            trailSection0.Pins[0, 0].Link = JnIndex;
            trailSection1.Pins[0, 0].Link = JnIndex;

            JnSection.Pins[0, 0].Direction = 0;
            JnSection.Pins[0, 0].Link = leadSectionIndex0;
            JnSection.Pins[0, 1].Direction = 0;
            JnSection.Pins[0, 1].Link = leadSectionIndex1;
            JnSection.Pins[1, 0].Direction = 1;
            JnSection.Pins[1, 0].Link = trailSectionIndex0;
            JnSection.Pins[1, 1].Direction = 1;
            JnSection.Pins[1, 1].Link = trailSectionIndex1;

            if (tsectiondat.TrackShapes.ContainsKey(CrossOver.TrackShape))
            {
                JnSection.Overlap = tsectiondat.TrackShapes[CrossOver.TrackShape].ClearanceDistance;
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

        public static void insertEndNode(int thisNode, int direction, int pin, int endNode)
        {

            TrackCircuitSection thisSection = TrackCircuitList[thisNode];
            TrackCircuitSection endSection = new TrackCircuitSection(endNode);

            endSection.CircuitType = TrackCircuitType.EndOfTrack;
            int endDirection = direction == 0 ? 1 : 0;
            int iDirection = thisSection.Pins[direction, pin].Direction == 0 ? 1 : 0;
            endSection.Pins[iDirection, 0].Direction = endDirection;
            endSection.Pins[iDirection, 0].Link = thisNode;

            thisSection.Pins[direction, pin].Link = endNode;

            TrackCircuitList.Add(endSection);
        }


    }

}
