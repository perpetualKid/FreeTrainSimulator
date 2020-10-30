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
using System.Text;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
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
    ///  class SignalObject
    ///
    /// </summary>
    //================================================================================================//

    public class Signal
    {
        private static TrackNode[] trackNodes;
        private static TrackItem[] trackItems;

        public static SignalEnvironment SignalEnvironment { get; private set; }   //back reference to the signal environment

        public SignalWorldInfo WorldObject { get; set; }                // Signal World Object information

        private int? nextSwitchIndex;                       // index of first switch in path

        private readonly List<int> junctionsPassed = new List<int>();  // Junctions which are passed checking next signal //

        private int signalNumberNormalHeads;                // no. of normal signal heads in signal
        private int requestedNumClearAhead;                 // Passed on value for SignalNumClearAhead

        private readonly Dictionary<int, int> localStorage = new Dictionary<int, int>();  // list to store local script variables

        private InternalBlockstate internalBlockState = InternalBlockstate.Open;    // internal blockstate

        private int requestingNormalSignal = -1;            // ref of normal signal requesting route clearing (only used for signals without NORMAL heads)
        private readonly int[] defaultNextSignal;           // default next signal
        private readonly Train.TCSubpathRoute fixedRoute = new Train.TCSubpathRoute();     // fixed route from signal
        private bool fullRoute;                             // required route is full route to next signal or end-of-track
        private bool allowPartialRoute;                     // signal is always allowed to clear unto partial route
        private bool propagated;                            // route request propagated to next signal
        private bool isPropagated;                          // route request for this signal was propagated from previous signal

        private bool approachControlCleared;                // set in case signal has cleared on approach control
        private bool approachControlSet;                    // set in case approach control is active
        private bool claimLocked;                           // claim is locked in case of approach control
        private bool forcePropOnApproachControl;            // force propagation if signal is held on close control
        private double timingTriggerValue;                  // used timing trigger if time trigger is required, hold trigger time

        private List<KeyValuePair<int, int>> lockedTrains;

        internal int SignalNumClearAhead_MSTS { get; set; } = -2;    // Overall maximum SignalNumClearAhead over all heads (MSTS calculation)
        internal int SignalNumClearAhead_ORTS { get; set; } = -2;    // Overall maximum SignalNumClearAhead over all heads (ORTS calculation)
        internal int SignalNumClearAheadActive { get; set; } = -2;   // Active SignalNumClearAhead (for ORST calculation only, as set by script)

        public int Index { get; private set; }              // This signal's reference.
        public TrackDirection Direction { get; internal set; }  // Direction facing on track

        public int TrackNode { get; internal set; }         // Track node which contains this signal
        public int TrackItemRefIndex { get; internal set; } // Index to TrItemRef within Track Node 
        public bool ForcePropagation { get; set; }          // Force propagation (used in case of signals at very short distance)
        public bool FixedRoute { get; private set; }        // signal has fixed route
        public Traveller TdbTraveller { get; }              // TDB traveller to determine distance between objects
        public Train.TCSubpathRoute SignalRoute { get; internal set; } = new Train.TCSubpathRoute();  // train route from signal
        public int TrainRouteIndex { get; private set; }    // index of section after signal in train route list
        public Train.TrainRouted EnabledTrain { get; internal set; } // full train structure for which signal is enabled
        public IList<int> Signalfound { get; }              // active next signal - used for signals with NORMAL heads only
        public SignalPermission OverridePermission { get; set; } = SignalPermission.Denied;  // Permission to pass red signal
        public bool Static { get; internal set; }           // set if signal does not required updates (fixed signals)
        public SignalHoldState HoldState { get; set; } = SignalHoldState.None;

        //TODO 20201030 next two properties may be better joined into an enum setting
        public bool IsSignal { get; internal set; } = true; // if signal, false if speedpost //
        public bool IsSpeedSignal { get; internal set; } = true;    // if signal of type SPEED, false if fixed speedpost or actual signal

        public List<SignalHead> SignalHeads { get; } = new List<SignalHead>();
        public int TrackCircuitIndex { get; private set; } = -1;        // Reference to TrackCircuit (index)
        public float TrackCicruitOffset { get; private set; }           // Position within TrackCircuit
        public TrackDirection TrackCircuitDirection { get; private set; }   // Direction within TrackCircuit
        public int TrackCircuitNextIndex { get; private set; } = -1;    // Index of next TrackCircuit (NORMAL signals only)
        public TrackDirection TrackCircuitNextDirection { get; private set; }   // Direction of next TrackCircuit 

        internal static void Initialize(SignalEnvironment signals, TrackNode[] trackNodes, TrackItem[] trackItems)
        {
            SignalEnvironment = signals;               // reference to overlaying Signal class
            Signal.trackNodes = trackNodes;
            Signal.trackItems = trackItems;
        }

        //================================================================================================//
        /// <summary>
        ///  Constructor for empty item
        /// </summary>

        public Signal(int reference, Traveller traveller)
        {
            Index = reference;
            lockedTrains = new List<KeyValuePair<int, int>>();
            Signalfound = new List<int>();
            defaultNextSignal = new int[OrSignalTypes.Instance.FunctionTypes.Count];

            for (int i = 0; i < OrSignalTypes.Instance.FunctionTypes.Count; i++)
            {
                Signalfound.Add(-1);
                defaultNextSignal[i] = -1;
            }

            TdbTraveller = traveller;
        }

        //================================================================================================//
        /// <summary>
        ///  Constructor for Copy 
        /// </summary>
        public Signal(int reference, Signal source)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));
            Index = reference;
            WorldObject = new SignalWorldInfo(source.WorldObject);

            TrackNode = source.TrackNode;
            lockedTrains = new List<KeyValuePair<int, int>>(source.lockedTrains);

            TrackCircuitIndex = source.TrackCircuitIndex;
            TrackCicruitOffset = source.TrackCicruitOffset;
            TrackCircuitDirection = source.TrackCircuitDirection;
            TrackCircuitNextIndex = source.TrackCircuitNextIndex;
            TrackCircuitNextDirection = source.TrackCircuitNextDirection;

            Direction = source.Direction;
            IsSignal = source.IsSignal;
            SignalNumClearAhead_MSTS = source.SignalNumClearAhead_MSTS;
            SignalNumClearAhead_ORTS = source.SignalNumClearAhead_ORTS;
            SignalNumClearAheadActive = source.SignalNumClearAheadActive;
            signalNumberNormalHeads = source.signalNumberNormalHeads;

            internalBlockState = source.internalBlockState;
            OverridePermission = source.OverridePermission;

            TdbTraveller = new Traveller(source.TdbTraveller);

            Signalfound = new List<int>(source.Signalfound);
            defaultNextSignal = new int[source.defaultNextSignal.Length];
            for (int i = 0; i < defaultNextSignal.Length; i++)
            {
                defaultNextSignal[i] = source.defaultNextSignal[i];
            }
        }

        internal void ResetIndex(int reference)
        {
            Index = reference;
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for restore
        /// IMPORTANT : enabled train is restore temporarily as Trains are restored later as Signals
        /// Full restore of train link follows in RestoreTrains
        /// </summary>
        public void Restore(Simulator simulator, BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            int trainNumber = inf.ReadInt32();

            int sigfoundLength = inf.ReadInt32();
            for (int iSig = 0; iSig < sigfoundLength; iSig++)
            {
                Signalfound[iSig] = inf.ReadInt32();
            }

            bool validRoute = inf.ReadBoolean();

            if (validRoute)
            {
                SignalRoute = new Train.TCSubpathRoute(inf);
            }

            TrainRouteIndex = inf.ReadInt32();
            HoldState = (SignalHoldState)inf.ReadInt32();

            int totalJnPassed = inf.ReadInt32();

            for (int iJn = 0; iJn < totalJnPassed; iJn++)
            {
                int thisJunction = inf.ReadInt32();
                junctionsPassed.Add(thisJunction);
                SignalEnvironment.TrackCircuitList[thisJunction].SignalsPassingRoutes.Add(Index);
            }

            fullRoute = inf.ReadBoolean();
            allowPartialRoute = inf.ReadBoolean();
            propagated = inf.ReadBoolean();
            isPropagated = inf.ReadBoolean();
            ForcePropagation = false; // preset (not stored)
            SignalNumClearAheadActive = inf.ReadInt32();
            requestedNumClearAhead = inf.ReadInt32();
            approachControlCleared = inf.ReadBoolean();
            approachControlSet = inf.ReadBoolean();
            claimLocked = inf.ReadBoolean();
            forcePropOnApproachControl = inf.ReadBoolean();
            OverridePermission = (SignalPermission)inf.ReadInt32();

            // set dummy train, route direction index will be set later on restore of train
            EnabledTrain = null;

            if (trainNumber >= 0)
            {
                Train thisTrain = new Train(simulator, trainNumber);
                Train.TrainRouted thisTrainRouted = new Train.TrainRouted(thisTrain, 0);
                EnabledTrain = thisTrainRouted;
            }
            //  Retrieve lock table
            lockedTrains = new List<KeyValuePair<int, int>>();
            int cntLock = inf.ReadInt32();
            for (int cnt = 0; cnt < cntLock; cnt++)
            {
                KeyValuePair<int, int> lockInfo = new KeyValuePair<int, int>(inf.ReadInt32(), inf.ReadInt32());
                lockedTrains.Add(lockInfo);

            }
        }

        //================================================================================================//
        /// <summary>
        /// Restore Train Reference
        /// </summary>
        public void RestoreTrains(List<Train> trains)
        {
            if (EnabledTrain != null)
            {
                int number = EnabledTrain.Train.Number;

                Train foundTrain = SignalEnvironment.FindTrain(number, trains);

                // check if this signal is next signal forward for this train
                if (Index == foundTrain?.NextSignalObject[0]?.Index)
                {
                    EnabledTrain = foundTrain.routedForward;
                    foundTrain.NextSignalObject[0] = this;
                }
                // check if this signal is next signal backward for this train
                else if (Index == foundTrain?.NextSignalObject[1]?.Index)
                {
                    EnabledTrain = foundTrain.routedBackward;
                    foundTrain.NextSignalObject[1] = this;
                }
                else
                {
                    // check if this section is reserved for this train

                    TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[TrackCircuitIndex];
                    if (thisSection.CircuitState.TrainReserved != null && thisSection.CircuitState.TrainReserved.Train.Number == number)
                    {
                        EnabledTrain = thisSection.CircuitState.TrainReserved;
                    }
                    else
                    {
                        EnabledTrain = null; // reset - train not found
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Restore Signal Aspect based on train information
        /// Process non-propagated signals only, others are updated through propagation
        /// </summary>

        public void RestoreAspect()
        {
            if (EnabledTrain != null && !isPropagated)
            {
                if (SignalNormal())
                {
                    checkRouteState(false, SignalRoute, EnabledTrain);
                    propagateRequest();
                    StateUpdate();
                }
                else
                {
                    getBlockState_notRouted();
                    StateUpdate();
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

            if (EnabledTrain == null)
            {
                outf.Write(-1);
            }
            else
            {
                outf.Write(EnabledTrain.Train.Number);
            }

            outf.Write(Signalfound.Count);
            foreach (int thisSig in Signalfound)
            {
                outf.Write(thisSig);
            }

            if (SignalRoute == null)
            {
                outf.Write(false);
            }
            else
            {
                outf.Write(true);
                SignalRoute.Save(outf);
            }

            outf.Write(TrainRouteIndex);
            outf.Write((int)HoldState);

            outf.Write(junctionsPassed.Count);
            if (junctionsPassed.Count > 0)
            {
                foreach (int thisJunction in junctionsPassed)
                {
                    outf.Write(thisJunction);
                }
            }

            outf.Write(fullRoute);
            outf.Write(allowPartialRoute);
            outf.Write(propagated);
            outf.Write(isPropagated);
            outf.Write(SignalNumClearAheadActive);
            outf.Write(requestedNumClearAhead);
            outf.Write(approachControlCleared);
            outf.Write(approachControlSet);
            outf.Write(claimLocked);
            outf.Write(forcePropOnApproachControl);
            outf.Write((int)OverridePermission);
            outf.Write(lockedTrains.Count);
            for (int cnt = 0; cnt < lockedTrains.Count; cnt++)
            {
                outf.Write(lockedTrains[cnt].Key);
                outf.Write(lockedTrains[cnt].Value);
            }

        }

        //================================================================================================//
        /// <summary>
        /// return blockstate
        /// </summary>
        public SignalBlockState BlockState()
        {
            switch (internalBlockState)
            {
                case InternalBlockstate.Reserved:
                case InternalBlockstate.Reservable:
                    return SignalBlockState.Clear;
                case InternalBlockstate.OccupiedSameDirection:
                    return SignalBlockState.Occupied;
                default:
                    return SignalBlockState.Jn_Obstructed;
            }
        }

        public bool Enabled
        {
            get
            {
                if (MPManager.IsMultiPlayer() && MPManager.PreferGreen)
                    return true;
                return EnabledTrain != null;
            }
        }

        public int TrackItemIndex => (trackNodes[TrackNode] as TrackVectorNode).TrackItemIndices[TrackItemRefIndex];


        //================================================================================================//
        /// <summary>
        /// setSignalDefaultNextSignal : set default next signal based on non-Junction tracks ahead
        /// this routine also sets fixed routes for signals which do not lead onto junction or crossover
        /// </summary>
        public void SetSignalDefaultNextSignal()
        {
            int trackCircuitReference = TrackCircuitIndex;
            float position = TrackCicruitOffset;
            TrackDirection direction = TrackCircuitDirection;
            bool setFixedRoute = false;

            // for normal signals : start at next TC
            if (TrackCircuitNextIndex > 0)
            {
                trackCircuitReference = TrackCircuitNextIndex;
                direction = TrackCircuitNextDirection;
                position = 0.0f;
                setFixedRoute = true;
            }

            bool completedFixedRoute = !setFixedRoute;

            // get trackcircuit
            TrackCircuitSection trackCircuitSection = null;
            if (trackCircuitReference > 0)
            {
                trackCircuitSection = SignalEnvironment.TrackCircuitList[trackCircuitReference];
            }

            // set default
            for (int fntype = 0; fntype < defaultNextSignal.Length; fntype++)
            {
                defaultNextSignal[fntype] = -1;
            }

            // loop through valid sections
            while (trackCircuitSection != null && trackCircuitSection.CircuitType == TrackCircuitType.Normal)
            {
                if (!completedFixedRoute)
                {
                    fixedRoute.Add(new Train.TCRouteElement(trackCircuitSection.Index, (int)direction));
                }

                // normal signal
                if (defaultNextSignal[(int)SignalFunction.Normal] < 0)
                {
                    if (trackCircuitSection.EndSignals[direction] != null)
                    {
                        defaultNextSignal[(int)SignalFunction.Normal] = trackCircuitSection.EndSignals[direction].Index;
                        completedFixedRoute = true;
                    }
                }

                // other signals
                for (int i = (int)SignalFunction.Normal.Next(); i < SignalEnvironment.OrtsSignalTypeCount; i++)
                {
                    if (defaultNextSignal[i] < 0)
                    {
                        foreach (TrackCircuitSignalItem signalItem in trackCircuitSection.CircuitItems.TrackCircuitSignals[direction][i])
                        {
                            if (signalItem.Signal.Index != Index && (trackCircuitSection.Index != trackCircuitReference || signalItem.SignalLocation > position))
                            {
                                defaultNextSignal[i] = signalItem.Signal.Index;
                                break;
                            }
                        }
                    }
                }

                TrackDirection currentDirection = direction;
                direction = trackCircuitSection.Pins[direction, Location.NearEnd].Direction;
                trackCircuitSection = SignalEnvironment.TrackCircuitList[trackCircuitSection.Pins[currentDirection, Location.NearEnd].Link];
            }

            // copy default as valid items
            for (int i = 0; i < SignalEnvironment.OrtsSignalTypeCount; i++)
            {
                Signalfound[i] = defaultNextSignal[i];
            }

            // Allow use of fixed route if ended on END_OF_TRACK

            if (trackCircuitSection?.CircuitType == TrackCircuitType.EndOfTrack)
            {
                completedFixedRoute = true;
            }

            // if valid next normal signal found, signal has fixed route

            if (setFixedRoute && completedFixedRoute)
            {
                FixedRoute = true;
                fullRoute = true;
            }
            else
            {
                FixedRoute = false;
                fixedRoute.Clear();
            }
        }

        //================================================================================================//
        /// <summary>
        /// isSignalNormal : Returns true if at least one signal head is type normal.
        /// </summary>
        public bool SignalNormal()
        {
            return SignalHeads.Where(head => head.SignalFunction == SignalFunction.Normal).Any();
        }

        //================================================================================================//
        /// <summary>
        /// isORTSSignalType : Returns true if at least one signal head is of required type
        /// </summary>
        public bool OrtsSignalType(int requestedSignalFunction)
        {
            return SignalHeads.Where(head => head.OrtsSignalFunctionIndex == requestedSignalFunction).Any();
        }

        //================================================================================================//
        /// <summary>
        /// next_sig_mr : returns most restrictive state of next signal of required type
        /// </summary>

        public SignalAspectState next_sig_mr(int fn_type)
        {
            int nextSignal = Signalfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                Signalfound[fn_type] = nextSignal;
            }

            if (nextSignal >= 0)
            {
                return SignalEnvironment.SignalObjects[nextSignal].this_sig_mr(fn_type);
            }
            else
            {
                return SignalAspectState.Stop;
            }
        }

        //================================================================================================//
        /// <summary>
        /// next_sig_lr : returns least restrictive state of next signal of required type
        /// </summary>

        public SignalAspectState next_sig_lr(int fn_type)
        {
            int nextSignal = Signalfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                Signalfound[fn_type] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                return SignalEnvironment.SignalObjects[nextSignal].this_sig_lr(fn_type);
            }
            else
            {
                return SignalAspectState.Stop;
            }
        }

        //================================================================================================//
        /// <summary>
        /// next_nsig_lr : returns least restrictive state of next signal of required type of the nth signal ahead
        /// </summary>

        public SignalAspectState next_nsig_lr(int fn_type, int nsignal)
        {
            int foundsignal = 0;
            SignalAspectState foundAspect = SignalAspectState.Clear_2;
            Signal nextSignalObject = this;

            while (foundsignal < nsignal && foundAspect != SignalAspectState.Stop)
            {
                // use sigfound
                int nextSignal = nextSignalObject.Signalfound[fn_type];

                // sigfound not set, try direct search
                if (nextSignal < 0)
                {
                    nextSignal = SONextSignal(fn_type);
                    nextSignalObject.Signalfound[fn_type] = nextSignal;
                }

                // signal found : get state
                if (nextSignal >= 0)
                {
                    foundsignal++;

                    nextSignalObject = SignalEnvironment.SignalObjects[nextSignal];
                    foundAspect = nextSignalObject.this_sig_lr(fn_type);

                    // reached required signal or state is stop : return
                    if (foundsignal >= nsignal || foundAspect == SignalAspectState.Stop)
                    {
                        return (foundAspect);
                    }
                }

                // signal not found : return stop
                else
                {
                    return SignalAspectState.Stop;
                }
            }
            return (SignalAspectState.Stop); // emergency exit - loop should normally have exited on return
        }

        //================================================================================================//
        /// <summary>
        /// opp_sig_mr
        /// </summary>

        /// normal version
        public SignalAspectState opp_sig_mr(int fn_type)
        {
            int signalFound = SONextSignalOpp(fn_type);
            return (signalFound >= 0 ? SignalEnvironment.SignalObjects[signalFound].this_sig_mr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_mr

        /// debug version
        public SignalAspectState opp_sig_mr(int fn_type, ref Signal foundSignal)
        {
            int signalFound = SONextSignalOpp(fn_type);
            foundSignal = signalFound >= 0 ? SignalEnvironment.SignalObjects[signalFound] : null;
            return (signalFound >= 0 ? SignalEnvironment.SignalObjects[signalFound].this_sig_mr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_mr

        //================================================================================================//
        /// <summary>
        /// opp_sig_lr
        /// </summary>

        /// normal version
        public SignalAspectState opp_sig_lr(int fn_type)
        {
            int signalFound = SONextSignalOpp(fn_type);
            return (signalFound >= 0 ? SignalEnvironment.SignalObjects[signalFound].this_sig_lr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_lr

        /// debug version
        public SignalAspectState opp_sig_lr(int fn_type, ref Signal foundSignal)
        {
            int signalFound = SONextSignalOpp(fn_type);
            foundSignal = signalFound >= 0 ? SignalEnvironment.SignalObjects[signalFound] : null;
            return (signalFound >= 0 ? SignalEnvironment.SignalObjects[signalFound].this_sig_lr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_lr

        //================================================================================================//
        /// <summary>
        /// this_sig_mr : Returns the most restrictive state of this signal's heads of required type
        /// </summary>

        /// <summary>
        /// standard version without state return
        /// </summary>
        public SignalAspectState this_sig_mr(int fn_type)
        {
            bool sigfound = false;
            return (this_sig_mr(fn_type, ref sigfound));
        }

        /// <summary>
        /// standard version without state return using MSTS type parameter
        /// </summary>
        public SignalAspectState this_sig_mr(SignalFunction msfn_type)
        {
            bool sigfound = false;
            return (this_sig_mr((int)msfn_type, ref sigfound));
        }

        /// <summary>
        /// additional version with state return
        /// </summary>
        public SignalAspectState this_sig_mr(int fn_type, ref bool sigfound)
        {
            SignalAspectState sigAsp = SignalAspectState.Unknown;
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.OrtsSignalFunctionIndex == fn_type && sigHead.SignalIndicationState < sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                }
            }
            if (sigAsp == SignalAspectState.Unknown)
            {
                sigfound = false;
                return SignalAspectState.Stop;
            }
            else
            {
                sigfound = true;
                return sigAsp;
            }
        }

        /// <summary>
        /// additional version using valid route heads only
        /// </summary>
        internal SignalAspectState MRSignalOnRoute(int signalType)
        {
            SignalAspectState sigAsp = SignalAspectState.Unknown;

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.OrtsSignalFunctionIndex == signalType && sigHead.VerifyRouteSet() == 1 && sigHead.SignalIndicationState < sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                }
            }

            return sigAsp == SignalAspectState.Unknown ? SignalAspectState.Stop : sigAsp;
        }

        //================================================================================================//
        /// <summary>
        /// this_sig_lr : Returns the least restrictive state of this signal's heads of required type
        /// </summary>

        /// <summary>
        /// standard version without state return
        /// </summary>
        public SignalAspectState this_sig_lr(int fn_type)
        {
            bool sigfound = false;
            return (this_sig_lr(fn_type, ref sigfound));
        }

        /// <summary>
        /// standard version without state return using MSTS type parameter
        /// </summary>
        public SignalAspectState this_sig_lr(SignalFunction msfn_type)
        {
            bool sigfound = false;
            return (this_sig_lr((int)msfn_type, ref sigfound));
        }

        /// <summary>
        /// additional version with state return
        /// </summary>
        public SignalAspectState this_sig_lr(int fn_type, ref bool sigfound)
        {
            SignalAspectState sigAsp = SignalAspectState.Stop;
            bool sigAspSet = false;
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.OrtsSignalFunctionIndex == fn_type && sigHead.SignalIndicationState >= sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                    sigAspSet = true;
                }
            }

            sigfound = sigAspSet;

            if (sigAspSet)
            {
                return sigAsp;
            }
            else if (fn_type == (int)SignalFunction.Normal)
            {
                return SignalAspectState.Clear_2;
            }
            else
            {
                return SignalAspectState.Stop;
            }
        }//this_sig_lr

        //================================================================================================//
        /// <summary>
        /// this_sig_speed : Returns the speed related to the least restrictive aspect (for normal signal)
        /// </summary>

        public SpeedInfo this_sig_speed(SignalFunction fn_type)
        {
            var sigAsp = SignalAspectState.Stop;
            var set_speed = new SpeedInfo(null);

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == fn_type && sigHead.SignalIndicationState >= sigAsp)
                {
                    set_speed = sigHead.SpeedInfoSet[sigHead.SignalIndicationState];
                }
            }
            return set_speed;
        }//this_sig_speed

        //================================================================================================//
        /// <summary>
        /// next_sig_id : returns ident of next signal of required type
        /// </summary>

        public int next_sig_id(int fn_type)
        {
            int nextSignal = Signalfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                Signalfound[fn_type] = nextSignal;
            }

            if (nextSignal >= 0)
            {
                if (fn_type != (int)SignalFunction.Normal)
                {
                    Signal foundSignalObject = SignalEnvironment.SignalObjects[nextSignal];
                    if (SignalNormal())
                    {
                        foundSignalObject.requestingNormalSignal = Index;
                    }
                    else
                    {
                        foundSignalObject.requestingNormalSignal = requestingNormalSignal;
                    }
                }

                return (nextSignal);
            }
            else
            {
                return (-1);
            }
        }

        //================================================================================================//
        /// <summary>
        /// next_nsig_id : returns ident of next signal of required type
        /// </summary>

        public int next_nsig_id(int fn_type, int nsignal)
        {
            int nextSignal = Index;
            int foundsignal = 0;
            Signal nextSignalObject = this;

            while (foundsignal < nsignal && nextSignal >= 0)
            {
                // use sigfound
                nextSignal = nextSignalObject.Signalfound[fn_type];

                // sigfound not set, try direct search
                if (nextSignal < 0)
                {
                    nextSignal = nextSignalObject.SONextSignal(fn_type);
                    nextSignalObject.Signalfound[fn_type] = nextSignal;
                }

                // signal found
                if (nextSignal >= 0)
                {
                    foundsignal++;
                    nextSignalObject = SignalEnvironment.SignalObjects[nextSignal];
                }
            }

            if (nextSignal >= 0 && foundsignal > 0)
            {
                return (nextSignal);
            }
            else
            {
                return (-1);
            }
        }

        //================================================================================================//
        /// <summary>
        /// opp_sig_id : returns ident of next opposite signal of required type
        /// </summary>

        public int opp_sig_id(int fn_type)
        {
            return (SONextSignalOpp(fn_type));
        }

        //================================================================================================//
        /// <summary>
        /// this_sig_noSpeedReduction : Returns the setting if speed must be reduced on RESTRICTED or STOP_AND_PROCEED
        /// returns TRUE if speed reduction must be suppressed
        /// </summary>

        public bool this_sig_noSpeedReduction(SignalFunction fn_type)
        {
            var sigAsp = SignalAspectState.Stop;
            bool setNoReduction = false;

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == fn_type && sigHead.SignalIndicationState >= sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                    if (sigAsp <= SignalAspectState.Restricting && sigHead.SpeedInfoSet?[sigAsp] != null)
                    {
                        setNoReduction = sigHead.SpeedInfoSet[sigAsp].LimitedSpeedReduction == 1;
                    }
                    else
                    {
                        setNoReduction = false;
                    }
                }
            }
            return setNoReduction;
        }//this_sig_noSpeedReduction

        //================================================================================================//
        /// <summary>
        /// isRestrictedSpeedPost : Returns TRUE if it is a restricted (temp) speedpost
        /// </summary>

        public int SpeedPostType()
        {
            var sigAsp = SignalAspectState.Clear_2;
            int speedPostType = 0; // default = standard speedpost

            SignalHead sigHead = SignalHeads.First();

            if (sigHead.SpeedInfoSet?[sigAsp] != null)
            {
                speedPostType = sigHead.SpeedInfoSet[sigAsp].LimitedSpeedReduction;

            }
            return speedPostType;

        }//isRestrictedSpeedPost

        //================================================================================================//
        /// <summary>
        /// this_lim_speed : Returns the lowest allowed speed (for speedpost and speed signal)
        /// </summary>

        public SpeedInfo this_lim_speed(SignalFunction fn_type)
        {
            var set_speed = new SpeedInfo(9E9f, 9E9f, false, false, 0);

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == fn_type)
                {
                    SpeedInfo this_speed = sigHead.SpeedInfoSet[sigHead.SignalIndicationState];
                    if (this_speed != null)
                    {
                        if (this_speed.PassengerSpeed > 0 && this_speed.PassengerSpeed < set_speed.PassengerSpeed)
                        {
                            set_speed.PassengerSpeed = this_speed.PassengerSpeed;
                            set_speed.Flag = false;
                            set_speed.Reset = false;
                            if (!IsSignal) set_speed.LimitedSpeedReduction = this_speed.LimitedSpeedReduction;
                        }

                        if (this_speed.FreightSpeed > 0 && this_speed.FreightSpeed < set_speed.FreightSpeed)
                        {
                            set_speed.FreightSpeed = this_speed.FreightSpeed;
                            set_speed.Flag = false;
                            set_speed.Reset = false;
                            if (!IsSignal) set_speed.LimitedSpeedReduction = this_speed.LimitedSpeedReduction;
                        }
                    }

                }
            }

            if (set_speed.PassengerSpeed > 1E9f)
                set_speed.PassengerSpeed = -1;
            if (set_speed.FreightSpeed > 1E9f)
                set_speed.FreightSpeed = -1;

            return set_speed;
        }//this_lim_speed

        //================================================================================================//
        /// <summary>
        /// store_lvar : store local variable
        /// </summary>

        public void store_lvar(int index, int value)
        {
            if (localStorage.ContainsKey(index))
            {
                localStorage.Remove(index);
            }
            localStorage.Add(index, value);
        }

        //================================================================================================//
        /// <summary>
        /// this_sig_lvar : retrieve variable from this signal
        /// </summary>

        public int this_sig_lvar(int index)
        {
            if (localStorage.ContainsKey(index))
            {
                return (localStorage[index]);
            }
            return (0);
        }

        //================================================================================================//
        /// <summary>
        /// next_sig_lvar : retrieve variable from next signal
        /// </summary>

        public int next_sig_lvar(int fn_type, int index)
        {
            int nextSignal = Signalfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                Signalfound[fn_type] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                Signal nextSignalObject = SignalEnvironment.SignalObjects[nextSignal];
                if (nextSignalObject.localStorage.ContainsKey(index))
                {
                    return (nextSignalObject.localStorage[index]);
                }
            }

            return (0);
        }

        //================================================================================================//
        /// <summary>
        /// next_sig_hasnormalsubtype : check if next signal has normal head with required subtype
        /// </summary>

        public int next_sig_hasnormalsubtype(int reqSubtype)
        {
            int nextSignal = Signalfound[(int)SignalFunction.Normal];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal((int)SignalFunction.Normal);
                Signalfound[(int)SignalFunction.Normal] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                Signal nextSignalObject = SignalEnvironment.SignalObjects[nextSignal];
                return (nextSignalObject.this_sig_hasnormalsubtype(reqSubtype));
            }

            return (0);
        }

        //================================================================================================//
        /// <summary>
        /// this_sig_hasnormalsubtype : check if this signal has normal head with required subtype
        /// </summary>

        public int this_sig_hasnormalsubtype(int reqSubtype)
        {
            foreach (SignalHead thisHead in SignalHeads)
            {
                if (thisHead.SignalFunction == SignalFunction.Normal && thisHead.OrtsNormalSubtypeIndex == reqSubtype)
                {
                    return (1);
                }
            }
            return (0);
        }

        //================================================================================================//
        /// <summary>
        /// switchstand : link signal with next switch and set aspect according to switch state
        /// </summary>

        public int switchstand(int aspect1, int aspect2)
        {
            // if switch index not yet set, find first switch in path
            if (!nextSwitchIndex.HasValue)
            {
                TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[TrackCircuitIndex];
                TrackDirection sectionDirection = TrackCircuitDirection;

                bool switchFound = false;

                while (!switchFound)
                {
                    TrackDirection pinIndex = sectionDirection;

                    if (thisSection.CircuitType == TrackCircuitType.Junction)
                    {
                        if (thisSection.Pins[pinIndex, Location.FarEnd].Link >= 0) // facing point
                        {
                            switchFound = true;
                            nextSwitchIndex = thisSection.Index;
                            if (thisSection.LinkedSignals == null)
                            {
                                thisSection.LinkedSignals = new List<int>
                                {
                                    Index
                                };
                            }
                            else if (!thisSection.LinkedSignals.Contains(Index))
                            {
                                thisSection.LinkedSignals.Add(Index);
                            }
                        }

                    }

                    sectionDirection = thisSection.Pins[pinIndex, Location.NearEnd].Direction;

                    if (thisSection.CircuitType != TrackCircuitType.EndOfTrack && thisSection.Pins[pinIndex, Location.NearEnd].Link >= 0)
                    {
                        thisSection = SignalEnvironment.TrackCircuitList[thisSection.Pins[pinIndex, Location.NearEnd].Link];
                    }
                    else
                    {
                        break;
                    }
                }

                if (!switchFound)
                {
                    nextSwitchIndex = -1;
                }
            }

            if (nextSwitchIndex >= 0)
            {
                TrackCircuitSection switchSection = SignalEnvironment.TrackCircuitList[nextSwitchIndex.Value];
                return (switchSection.JunctionLastRoute == 0 ? aspect1 : aspect2);
            }

            return (aspect1);
        }

        //================================================================================================//
        /// <summary>
        /// route_set : check if required route is set
        /// </summary>

        public bool route_set(int req_mainnode, uint req_jnnode)
        {
            bool routeset = false;
            bool retry = false;

            // if signal is enabled for a train, check if required section is in train route path

            if (EnabledTrain != null && !MPManager.IsMultiPlayer())
            {
                Train.TCSubpathRoute RoutePart = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex];

                TrackNode thisNode = trackNodes[req_mainnode];
                if (RoutePart != null)
                {
                    for (int iSection = 0; iSection <= thisNode.TrackCircuitCrossReferences.Count - 1 && !routeset; iSection++)
                    {
                        int sectionIndex = thisNode.TrackCircuitCrossReferences[iSection].Index;

                        for (int iElement = 0; iElement < RoutePart.Count && !routeset; iElement++)
                        {
                            routeset = (sectionIndex == RoutePart[iElement].TCSectionIndex && SignalEnvironment.TrackCircuitList[sectionIndex].CircuitType == TrackCircuitType.Normal);
                        }
                    }
                }

                // if not found in trainroute, try signalroute

                if (!routeset && SignalRoute != null)
                {
                    for (int iElement = 0; iElement <= SignalRoute.Count - 1 && !routeset; iElement++)
                    {
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[SignalRoute[iElement].TCSectionIndex];
                        routeset = (thisSection.OriginalIndex == req_mainnode && thisSection.CircuitType == TrackCircuitType.Normal);
                    }
                }
                retry = !routeset;
            }


            // not enabled, follow set route but only if not normal signal (normal signal will not clear if not enabled)
            // also, for normal enabled signals - try and follow pins (required node may be beyond present route)

            if (retry || !SignalNormal() || MPManager.IsMultiPlayer())
            {
                TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[TrackCircuitIndex];
                TrackDirection direction = TrackCircuitDirection;
                TrackDirection newDirection;
                int sectionIndex = -1;
                bool passedTrackJn = false;

                List<int> passedSections = new List<int>();
                passedSections.Add(thisSection.Index);

                routeset = (req_mainnode == thisSection.OriginalIndex);
                while (!routeset && thisSection != null)
                {
                    if (thisSection.ActivePins[direction, Location.NearEnd].Link >= 0)
                    {
                        newDirection = thisSection.ActivePins[direction, Location.NearEnd].Direction;
                        sectionIndex = thisSection.ActivePins[direction, Location.NearEnd].Link;
                    }
                    else
                    {
                        newDirection = thisSection.ActivePins[direction, Location.FarEnd].Direction;
                        sectionIndex = thisSection.ActivePins[direction, Location.FarEnd].Link;
                    }

                    // if Junction, if active pins not set use selected route
                    if (sectionIndex < 0 && thisSection.CircuitType == TrackCircuitType.Junction)
                    {
                        // check if this is required junction
                        if (Convert.ToUInt32(thisSection.Index) == req_jnnode)
                        {
                            passedTrackJn = true;
                        }
                        // break if passed required junction
                        else if (passedTrackJn)
                        {
                            break;
                        }

                        if (thisSection.ActivePins[TrackDirection.Reverse, Location.NearEnd].Link == -1 && thisSection.ActivePins[TrackDirection.Reverse, Location.FarEnd].Link == -1)
                        {
                            Location selectedLocation = (Location)(trackNodes[thisSection.OriginalIndex] as TrackJunctionNode).SelectedRoute;
                            newDirection = thisSection.Pins[TrackDirection.Reverse, selectedLocation].Direction;
                            sectionIndex = thisSection.Pins[TrackDirection.Reverse, selectedLocation].Link;
                        }
                    }

                    // if NORMAL, if active pins not set use default pins
                    if (sectionIndex < 0 && thisSection.CircuitType == TrackCircuitType.Normal)
                    {
                        newDirection = thisSection.Pins[direction, Location.NearEnd].Direction;
                        sectionIndex = thisSection.Pins[direction, Location.NearEnd].Link;
                    }

                    // check for loop
                    if (passedSections.Contains(sectionIndex))
                    {
                        thisSection = null;  // route is looped - exit
                    }

                    // next section
                    else if (sectionIndex >= 0)
                    {
                        passedSections.Add(sectionIndex);
                        thisSection = SignalEnvironment.TrackCircuitList[sectionIndex];
                        direction = newDirection;
                        routeset = (req_mainnode == thisSection.OriginalIndex && thisSection.CircuitType == TrackCircuitType.Normal);
                    }

                    // no next section
                    else
                    {
                        thisSection = null;
                    }
                }
            }

            return (routeset);
        }

        //================================================================================================//
        /// <summary>
        /// Find next signal of specified type along set sections - not for NORMAL signals
        /// </summary>

        public int SONextSignal(int fntype)
        {
            int thisTC = TrackCircuitIndex;
            TrackDirection direction = TrackCircuitDirection;
            int signalFound = -1;
            TrackCircuitSection thisSection = null;
            bool sectionSet = false;

            // maximise fntype to length of available type list
            int reqtype = Math.Min(fntype, SignalEnvironment.OrtsSignalTypeCount);

            // if searching for SPEED signal : check if enabled and use train to find next speedpost
            if (reqtype == (int)SignalFunction.Speed)
            {
                if (EnabledTrain != null)
                {
                    signalFound = SONextSignalSpeed(TrackCircuitIndex);
                }
                else
                {
                    return (-1);
                }
            }

            // for normal signals

            else if (reqtype == (int)SignalFunction.Normal)
            {
                if (SignalNormal())        // if this signal is normal : cannot be done using this route (set through sigfound variable)
                    return (-1);
                signalFound = SONextSignalNormal(TrackCircuitIndex);   // other types of signals (sigfound not used)
            }

            // for other signals : move to next TC (signal would have been default if within same section)

            else
            {
                thisSection = SignalEnvironment.TrackCircuitList[thisTC];
                sectionSet = EnabledTrain == null ? false : thisSection.IsSet(EnabledTrain, false);

                if (sectionSet)
                {
                    thisTC = thisSection.ActivePins[direction, Location.NearEnd].Link;
                    direction = thisSection.ActivePins[direction, Location.NearEnd].Direction;
                }
            }

            // loop through valid sections

            while (sectionSet && thisTC > 0 && signalFound < 0)
            {
                thisSection = SignalEnvironment.TrackCircuitList[thisTC];

                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!junctionsPassed.Contains(thisTC))
                        junctionsPassed.Add(thisTC);  // set reference to junction section
                    if (!thisSection.SignalsPassingRoutes.Contains(Index))
                        thisSection.SignalsPassingRoutes.Add(Index);
                }

                // check if required type of signal is along this section

                TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][reqtype];
                if (thisList.Count > 0)
                {
                    signalFound = thisList[0].Signal.Index;
                }

                // get next section if active link is set

                if (signalFound < 0)
                {
                    TrackDirection pinIndex = direction;
                    sectionSet = thisSection.IsSet(EnabledTrain, false);
                    if (sectionSet)
                    {
                        thisTC = thisSection.ActivePins[pinIndex, Location.NearEnd].Link;
                        direction = thisSection.ActivePins[pinIndex, Location.NearEnd].Direction;
                        if (thisTC == -1)
                        {
                            thisTC = thisSection.ActivePins[pinIndex, Location.FarEnd].Link;
                            direction = thisSection.ActivePins[pinIndex, Location.FarEnd].Direction;
                        }
                    }
                }
            }

            // if signal not found following switches use signal route
            if (signalFound < 0 && SignalRoute != null && SignalRoute.Count > 0)
            {
                for (int iSection = 0; iSection <= (SignalRoute.Count - 1) && signalFound < 0; iSection++)
                {
                    thisSection = SignalEnvironment.TrackCircuitList[SignalRoute[iSection].TCSectionIndex];
                    direction = (TrackDirection)SignalRoute[iSection].Direction;
                    TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                    if (thisList.Count > 0)
                    {
                        signalFound = thisList[0].Signal.Index;
                    }
                }
            }

            // if signal not found, use route from requesting normal signal
            if (signalFound < 0 && requestingNormalSignal >= 0)
            {
                Signal refSignal = SignalEnvironment.SignalObjects[requestingNormalSignal];
                if (refSignal.SignalRoute != null && refSignal.SignalRoute.Count > 0)
                {
                    int nextSectionIndex = refSignal.SignalRoute.GetRouteIndex(TrackCircuitIndex, 0);

                    if (nextSectionIndex >= 0)
                    {
                        for (int iSection = nextSectionIndex + 1; iSection <= (refSignal.SignalRoute.Count - 1) && signalFound < 0; iSection++)
                        {
                            thisSection = SignalEnvironment.TrackCircuitList[refSignal.SignalRoute[iSection].TCSectionIndex];
                            direction = (TrackDirection)refSignal.SignalRoute[iSection].Direction;
                            TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                            if (thisList.Count > 0)
                            {
                                signalFound = thisList[0].Signal.Index;
                            }
                        }
                    }
                }
            }

            return (signalFound);
        }

        //================================================================================================//
        /// <summary>
        /// Find next signal of specified type along set sections - for SPEED signals only
        /// </summary>

        private int SONextSignalSpeed(int thisTC)
        {
            int routeListIndex = EnabledTrain.Train.ValidRoute[0].GetRouteIndex(TrackCircuitIndex, EnabledTrain.Train.PresentPosition[0].RouteListIndex);

            // signal not in train's route
            if (routeListIndex < 0)
            {
                return (-1);
            }

            // find next speed object
            TrackCircuitSignalItem foundItem = SignalEnvironment.Find_Next_Object_InRoute(EnabledTrain.Train.ValidRoute[0], routeListIndex, TrackCicruitOffset, -1, SignalFunction.Speed, EnabledTrain);
            if (foundItem.SignalState == SignalItemFindState.Item)
            {
                return (foundItem.Signal.Index);
            }
            else
            {
                return (-1);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Find next signal of specified type along set sections - NORMAL signals ONLY
        /// </summary>

        private int SONextSignalNormal(int thisTC)
        {
            TrackDirection direction = TrackCircuitDirection;
            int signalFound = -1;
            TrackCircuitSection thisSection = null;

            TrackDirection pinIndex;

            if (thisTC < 0)
            {
                thisTC = TrackCircuitIndex;
                thisSection = SignalEnvironment.TrackCircuitList[thisTC];
                pinIndex = direction;
                thisTC = thisSection.ActivePins[pinIndex, Location.NearEnd].Link;
                direction = thisSection.ActivePins[pinIndex, Location.NearEnd].Direction;
            }

            // loop through valid sections

            while (thisTC > 0 && signalFound < 0)
            {
                thisSection = SignalEnvironment.TrackCircuitList[thisTC];

                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!junctionsPassed.Contains(thisTC))
                        junctionsPassed.Add(thisTC);  // set reference to junction section
                    if (!thisSection.SignalsPassingRoutes.Contains(Index))
                        thisSection.SignalsPassingRoutes.Add(Index);
                }

                // check if normal signal is along this section

                if (thisSection.EndSignals[direction] != null)
                {
                    signalFound = thisSection.EndSignals[direction].Index;
                }

                // get next section if active link is set

                if (signalFound < 0)
                {
                    pinIndex = direction;
                    thisTC = thisSection.ActivePins[pinIndex, Location.NearEnd].Link;
                    direction = thisSection.ActivePins[pinIndex, Location.NearEnd].Direction;
                    if (thisTC == -1)
                    {
                        thisTC = thisSection.ActivePins[pinIndex, Location.FarEnd].Link;
                        direction = thisSection.ActivePins[pinIndex, Location.FarEnd].Direction;
                    }

                    // if no active link but signal has route allocated, use train route to find next section

                    if (thisTC == -1 && SignalRoute != null)
                    {
                        int thisIndex = SignalRoute.GetRouteIndex(thisSection.Index, 0);
                        if (thisIndex >= 0 && thisIndex <= SignalRoute.Count - 2)
                        {
                            thisTC = SignalRoute[thisIndex + 1].TCSectionIndex;
                            direction = (TrackDirection)SignalRoute[thisIndex + 1].Direction;
                        }
                    }
                }
            }

            return (signalFound);
        }

        //================================================================================================//
        /// <summary>
        /// Find next signal in opp direction
        /// </summary>

        public int SONextSignalOpp(int fntype)
        {
            int thisTC = TrackCircuitIndex;
            TrackDirection direction = TrackCircuitDirection.Next();    // reverse direction
            int signalFound = -1;

            TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisTC];
            bool sectionSet = EnabledTrain == null ? false : thisSection.IsSet(EnabledTrain, false);

            // loop through valid sections

            while (sectionSet && thisTC > 0 && signalFound < 0)
            {
                thisSection = SignalEnvironment.TrackCircuitList[thisTC];

                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!junctionsPassed.Contains(thisTC))
                        junctionsPassed.Add(thisTC);  // set reference to junction section
                    if (!thisSection.SignalsPassingRoutes.Contains(Index))
                        thisSection.SignalsPassingRoutes.Add(Index);
                }

                // check if required type of signal is along this section

                if (fntype == (int)SignalFunction.Normal)
                {
                    signalFound = thisSection.EndSignals[direction] != null ? thisSection.EndSignals[direction].Index : -1;
                }
                else
                {
                    TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                    if (thisList.Count > 0)
                    {
                        signalFound = thisList[0].Signal.Index;
                    }
                }

                // get next section if active link is set

                if (signalFound < 0)
                {
                    TrackDirection pinIndex = direction;
                    sectionSet = thisSection.IsSet(EnabledTrain, false);
                    if (sectionSet)
                    {
                        thisTC = thisSection.ActivePins[pinIndex, Location.NearEnd].Link;
                        direction = thisSection.ActivePins[pinIndex, Location.NearEnd].Direction;
                        if (thisTC == -1)
                        {
                            thisTC = thisSection.ActivePins[pinIndex, Location.FarEnd].Link;
                            direction = thisSection.ActivePins[pinIndex, Location.FarEnd].Direction;
                        }
                    }
                }
            }

            return (signalFound);
        }

        //================================================================================================//
        /// <summary>
        /// Perform route check and state update
        /// </summary>

        public void Update()
        {
            // perform route update for normal signals if enabled

            if (SignalNormal())
            {
                // if in hold, set to most restrictive for each head

                if (HoldState != SignalHoldState.None)
                {
                    foreach (SignalHead sigHead in SignalHeads)
                    {
                        if (HoldState == SignalHoldState.ManualLock || HoldState == SignalHoldState.StationStop) sigHead.SetMostRestrictiveAspect();
                    }
                    return;
                }

                // if enabled - perform full update and propagate if not yet done

                if (EnabledTrain != null)
                {
                    // if internal state is not reserved (route fully claimed), perform route check

                    if (internalBlockState != InternalBlockstate.Reserved)
                    {
                        checkRouteState(isPropagated, SignalRoute, EnabledTrain);
                    }

                    // propagate request

                    if (!isPropagated)
                    {
                        propagateRequest();
                    }

                    StateUpdate();

                    // propagate request if not yet done

                    if (!propagated && EnabledTrain != null)
                    {
                        propagateRequest();
                    }
                }

                // fixed route - check route and update

                else if (FixedRoute)
                {
                    // if internal state is not reserved (route fully claimed), perform route check

                    if (internalBlockState != InternalBlockstate.Reserved)
                    {
                        checkRouteState(true, fixedRoute, null);
                    }

                    StateUpdate();

                }

                // no route - perform update only

                else
                {
                    StateUpdate();
                }

            }

            // check blockstate for other signals

            else
            {
                getBlockState_notRouted();
                StateUpdate();
            }
        }

        //================================================================================================//
        /// <summary>
        /// fully reset signal as train has passed
        /// </summary>

        public void resetSignalEnabled()
        {
            // reset train information

            EnabledTrain = null;
            SignalRoute.Clear();
            fullRoute = FixedRoute;
            TrainRouteIndex = -1;

            isPropagated = false;
            propagated = false;
            ForcePropagation = false;
            approachControlCleared = false;
            approachControlSet = false;
            claimLocked = false;
            forcePropOnApproachControl = false;

            // reset block state to most restrictive

            internalBlockState = InternalBlockstate.Blocked;

            // reset next signal information to default

            for (int fntype = 0; fntype < SignalEnvironment.OrtsSignalTypeCount; fntype++)
            {
                Signalfound[fntype] = defaultNextSignal[fntype];
            }

            foreach (int thisSectionIndex in junctionsPassed)
            {
                TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisSectionIndex];
                thisSection.SignalsPassingRoutes.Remove(Index);
            }

            // reset permission //

            OverridePermission = SignalPermission.Denied;

            StateUpdate();
        }

        //================================================================================================//
        /// <summary>
        /// Perform the update for each head on this signal to determine state of signal.
        /// </summary>

        public void StateUpdate()
        {
            // reset approach control (must be explicitly reset as test in script may be conditional)
            approachControlSet = false;

            // update all normal heads first

            if (MPManager.IsMultiPlayer())
            {
                if (MPManager.IsClient()) return; //client won't handle signal update

                //if there were hold manually, will not update
                if (HoldState == SignalHoldState.ManualApproach || HoldState == SignalHoldState.ManualLock || HoldState == SignalHoldState.ManualPass) return;
            }

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == SignalFunction.Normal)
                    sigHead.Update();
            }

            // next, update all other heads

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction != SignalFunction.Normal)
                    sigHead.Update();
            }

        } // Update

        //================================================================================================//
        /// <summary>
        /// Returns the distance from the TDBtraveller to this signal. 
        /// </summary>

        public float DistanceTo(Traveller tdbTraveller)
        {
            int trItem = (trackNodes[TrackNode] as TrackVectorNode).TrackItemIndices[TrackItemRefIndex];
            return tdbTraveller.DistanceTo(trackItems[trItem].Location);
        }//DistanceTo

        //================================================================================================//
        /// <summary>
        /// Returns the distance from this object to the next object
        /// </summary>

        public float ObjectDistance(Signal nextObject)
        {
            int nextTrItem = (trackNodes[nextObject.TrackNode] as TrackVectorNode).TrackItemIndices[nextObject.TrackItemRefIndex];
            return this.TdbTraveller.DistanceTo(trackItems[nextTrItem].Location);
        }//ObjectDistance

        //================================================================================================//
        /// <summary>
        /// Check whether signal head is for this signal.
        /// </summary>

        public bool isSignalHead(SignalItem signalItem)
        {
            // Tritem for this signal
            SignalItem thisSignalItem = (SignalItem)trackItems[this.TrackItemIndex];
            // Same Tile
            if (signalItem.Location.TileX == thisSignalItem.Location.TileX && signalItem.Location.TileZ == thisSignalItem.Location.TileZ)
            {
                // Same position
                if ((Math.Abs(signalItem.Location.Location.X - thisSignalItem.Location.Location.X) < 0.01) &&
                    (Math.Abs(signalItem.Location.Location.Y - thisSignalItem.Location.Location.Y) < 0.01) &&
                    (Math.Abs(signalItem.Location.Location.Z - thisSignalItem.Location.Location.Z) < 0.01))
                {
                    return true;
                }
            }
            return false;
        }//isSignalHead

        //================================================================================================//
        /// <summary>
        /// Adds a head to this signal (for signam).
        /// </summary>

        public void AddHead(int trItem, int TDBRef, SignalItem sigItem)
        {
            // create SignalHead
            SignalHead head = new SignalHead(this, trItem, TDBRef, sigItem);

            // set junction link
            if (head.TrackJunctionNode != 0)
            {
                if (head.JunctionPath == 0)
                {
                    head.JunctionMainNode =
                       trackNodes[head.TrackJunctionNode].TrackPins[trackNodes[head.TrackJunctionNode].InPins].Link;
                }
                else
                {
                    head.JunctionMainNode =
                       trackNodes[head.TrackJunctionNode].TrackPins[trackNodes[head.TrackJunctionNode].InPins + 1].Link;
                }
            }
            SignalHeads.Add(head);

        }//AddHead (signal)

        //================================================================================================//
        /// <summary>
        /// Adds a head to this signal (for speedpost).
        /// </summary>

        public void AddHead(int trItem, int TDBRef, SpeedPostItem speedItem)
        {
            // create SignalHead
            SignalHead head = new SignalHead(this, trItem, TDBRef, speedItem);
            SignalHeads.Add(head);

        }//AddHead (speedpost)

        //================================================================================================//
        /// <summary>
        /// Sets the signal type from the sigcfg file for each signal head.
        /// </summary>
        public void SetSignalType(SignalConfigurationFile sigCFG)
        {
            foreach (SignalHead sigHead in SignalHeads)
            {
                sigHead.SetSignalType(trackItems, sigCFG);
            }
        }//SetSignalType

        //================================================================================================//
        /// <summary>
        /// Gets the display aspect for the track monitor.
        /// </summary>
        public TrackMonitorSignalAspect TranslateTMAspect(SignalAspectState signalState)
        {
            switch (signalState)
            {
                case SignalAspectState.Stop:
                    if (OverridePermission == SignalPermission.Granted)
                        return TrackMonitorSignalAspect.Permission;
                    else
                        return TrackMonitorSignalAspect.Stop;
                case SignalAspectState.Stop_And_Proceed:
                    return TrackMonitorSignalAspect.StopAndProceed;
                case SignalAspectState.Restricting:
                    return TrackMonitorSignalAspect.Restricted;
                case SignalAspectState.Approach_1:
                    return TrackMonitorSignalAspect.Approach1;
                case SignalAspectState.Approach_2:
                    return TrackMonitorSignalAspect.Approach2;
                case SignalAspectState.Approach_3:
                    return TrackMonitorSignalAspect.Approach3;
                case SignalAspectState.Clear_1:
                    return TrackMonitorSignalAspect.Clear1;
                case SignalAspectState.Clear_2:
                    return TrackMonitorSignalAspect.Clear2;
                default:
                    return TrackMonitorSignalAspect.None;
            }
        } // GetMonitorAspect

        //================================================================================================//
        /// <summary>
        /// request to clear signal in explorer mode
        /// </summary>
        public Train.TCSubpathRoute requestClearSignalExplorer(Train.TCSubpathRoute thisRoute,
            Train.TrainRouted thisTrain, bool propagated, int signalNumClearAhead)
        {
            // build output route from input route
            Train.TCSubpathRoute newRoute = new Train.TCSubpathRoute(thisRoute);

            // if signal has fixed route, use that else build route
            if (fixedRoute != null && fixedRoute.Count > 0)
            {
                SignalRoute = new Train.TCSubpathRoute(fixedRoute);
            }

            // build route from signal, upto next signal or max distance, take into account manual switch settings
            else
            {
                List<int> nextRoute = SignalEnvironment.ScanRoute(thisTrain.Train, TrackCircuitNextIndex, 0.0f, TrackCircuitNextDirection, true, -1, true, true, true, false,
                true, false, false, false, false, thisTrain.Train.IsFreight);

                SignalRoute = new Train.TCSubpathRoute();

                foreach (int sectionIndex in nextRoute)
                {
                    Train.TCRouteElement thisElement = new Train.TCRouteElement(Math.Abs(sectionIndex), sectionIndex >= 0 ? 0 : 1);
                    SignalRoute.Add(thisElement);
                }
            }

            // set full route if route ends with signal
            TrackCircuitSection lastSection = SignalEnvironment.TrackCircuitList[SignalRoute[SignalRoute.Count - 1].TCSectionIndex];
            TrackDirection lastDirection = (TrackDirection)SignalRoute[SignalRoute.Count - 1].Direction;

            if (lastSection.EndSignals[lastDirection] != null)
            {
                fullRoute = true;
                Signalfound[(int)SignalFunction.Normal] = lastSection.EndSignals[lastDirection].Index;
            }

            // try and clear signal

            EnabledTrain = thisTrain;
            checkRouteState(propagated, SignalRoute, thisTrain);

            // extend route if block is clear or permission is granted, even if signal is not cleared (signal state may depend on next signal)
            bool extendRoute = false;
            if (this_sig_lr(SignalFunction.Normal) > SignalAspectState.Stop) extendRoute = true;
            if (internalBlockState <= InternalBlockstate.Reservable) extendRoute = true;

            // if signal is cleared or permission is granted, extend route with signal route

            if (extendRoute || OverridePermission == SignalPermission.Granted)
            {
                foreach (Train.TCRouteElement thisElement in SignalRoute)
                {
                    newRoute.Add(thisElement);
                }
            }

            // if signal is cleared, propagate request if required
            if (extendRoute && fullRoute)
            {
                isPropagated = propagated;
                int ReqNumClearAhead = 0;

                if (SignalNumClearAhead_MSTS > -2)
                {
                    ReqNumClearAhead = propagated ?
                        signalNumClearAhead - signalNumberNormalHeads : SignalNumClearAhead_MSTS - signalNumberNormalHeads;
                }
                else
                {
                    if (SignalNumClearAheadActive == -1)
                    {
                        ReqNumClearAhead = propagated ? signalNumClearAhead : 1;
                    }
                    else if (SignalNumClearAheadActive == 0)
                    {
                        ReqNumClearAhead = 0;
                    }
                    else
                    {
                        ReqNumClearAhead = isPropagated ? signalNumClearAhead - 1 : SignalNumClearAheadActive - 1;
                    }
                }


                if (ReqNumClearAhead > 0)
                {
                    int nextSignalIndex = Signalfound[(int)SignalFunction.Normal];
                    if (nextSignalIndex >= 0)
                    {
                        Signal nextSignal = SignalEnvironment.SignalObjects[nextSignalIndex];
                        newRoute = nextSignal.requestClearSignalExplorer(newRoute, thisTrain, true, ReqNumClearAhead);
                    }
                }
            }

            return (newRoute);
        }
        //================================================================================================//
        /// <summary>
        /// request to clear signal
        /// </summary>

        public bool requestClearSignal(Train.TCSubpathRoute RoutePart, Train.TrainRouted thisTrain,
                        int clearNextSignals, bool requestIsPropagated, Signal lastSignal)
        {
            // set general variables
            int foundFirstSection = -1;
            int foundLastSection = -1;
            Signal nextSignal = null;

            isPropagated = requestIsPropagated;
            propagated = false;   // always pass on request

            // check if signal not yet enabled - if it is, give warning and quit

            // check if signal not yet enabled - if it is, give warning, reset signal and set both trains to node control, and quit

            if (EnabledTrain != null && EnabledTrain != thisTrain)
            {
                Trace.TraceWarning("Request to clear signal {0} from train {1}, signal already enabled for train {2}",
                                       Index, thisTrain.Train.Name, EnabledTrain.Train.Name);
                Train.TrainRouted otherTrain = EnabledTrain;
                ResetSignal(true);
                int routeListIndex = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].RouteListIndex;
                SignalEnvironment.BreakDownRouteList(thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex], routeListIndex, thisTrain);
                routeListIndex = otherTrain.Train.PresentPosition[otherTrain.TrainRouteDirectionIndex].RouteListIndex;
                SignalEnvironment.BreakDownRouteList(otherTrain.Train.ValidRoute[otherTrain.TrainRouteDirectionIndex], routeListIndex, otherTrain);

                thisTrain.Train.SwitchToNodeControl(thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex);
                if (otherTrain.Train.ControlMode != Train.TRAIN_CONTROL.EXPLORER && !otherTrain.Train.IsPathless) otherTrain.Train.SwitchToNodeControl(otherTrain.Train.PresentPosition[otherTrain.TrainRouteDirectionIndex].TCSectionIndex);
                return false;
            }
            if (thisTrain.Train.TCRoute != null && HasLockForTrain(thisTrain.Train.Number, thisTrain.Train.TCRoute.activeSubpath))
            {
                return false;
            }
            if (EnabledTrain != thisTrain) // new allocation - reset next signals
            {
                for (int fntype = 0; fntype < SignalEnvironment.OrtsSignalTypeCount; fntype++)
                {
                    Signalfound[fntype] = defaultNextSignal[fntype];
                }
            }
            EnabledTrain = thisTrain;

            // find section in route part which follows signal

            SignalRoute.Clear();

            int firstIndex = -1;
            if (lastSignal != null)
            {
                firstIndex = lastSignal.TrainRouteIndex;
            }
            if (firstIndex < 0)
            {
                firstIndex = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].RouteListIndex;
            }

            if (firstIndex >= 0)
            {
                for (int iNode = firstIndex;
                         iNode < RoutePart.Count && foundFirstSection < 0;
                         iNode++)
                {
                    Train.TCRouteElement thisElement = RoutePart[iNode];
                    if (thisElement.TCSectionIndex == TrackCircuitNextIndex)
                    {
                        foundFirstSection = iNode;
                        TrainRouteIndex = iNode;
                    }
                }
            }

            if (foundFirstSection < 0)
            {
                EnabledTrain = null;

                // if signal on holding list, set hold state
                if (thisTrain.Train.HoldingSignals.Contains(Index) && HoldState == SignalHoldState.None)
                {
                    HoldState = SignalHoldState.StationStop;
                }
                return false;
            }

            // copy sections upto next normal signal
            // check for loop

            List<int> sectionsInRoute = new List<int>();

            for (int iNode = foundFirstSection; iNode < RoutePart.Count && foundLastSection < 0; iNode++)
            {
                Train.TCRouteElement thisElement = RoutePart[iNode];
                if (sectionsInRoute.Contains(thisElement.TCSectionIndex))
                {
                    foundLastSection = iNode;  // loop
                }
                else
                {
                    SignalRoute.Add(thisElement);
                    sectionsInRoute.Add(thisElement.TCSectionIndex);

                    TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];

                    // exit if section is pool access section (signal will clear on new route on next try)
                    // reset train details to force new signal clear request
                    // check also creates new full train route
                    // applies to timetable mode only
                    if (thisTrain.Train.CheckPoolAccess(thisSection.Index))
                    {
                        EnabledTrain = null;
                        SignalRoute.Clear();

                        return false;
                    }

                    // check if section has end signal - if so is last section
                    if (thisSection.EndSignals[(TrackDirection)thisElement.Direction] != null)
                    {
                        foundLastSection = iNode;
                        nextSignal = thisSection.EndSignals[(TrackDirection)thisElement.Direction];
                    }
                }
            }

            // check if signal has route, is enabled, request is by enabled train and train is not occupying sections in signal route

            if (EnabledTrain != null && EnabledTrain == thisTrain && SignalRoute != null && SignalRoute.Count > 0)
            {
                foreach (Train.TCRouteElement routeElement in SignalRoute)
                {
                    TrackCircuitSection routeSection = SignalEnvironment.TrackCircuitList[routeElement.TCSectionIndex];
                    if (routeSection.CircuitState.OccupiedByThisTrain(thisTrain))
                    {
                        return false;  // train has passed signal - clear request is invalid
                    }
                }
            }

            // check if end of track reached

            Train.TCRouteElement lastSignalElement = SignalRoute[SignalRoute.Count - 1];
            TrackCircuitSection lastSignalSection = SignalEnvironment.TrackCircuitList[lastSignalElement.TCSectionIndex];

            fullRoute = true;

            // if end of signal route is not a signal or end-of-track it is not a full route

            if (nextSignal == null && lastSignalSection.CircuitType != TrackCircuitType.EndOfTrack)
            {
                fullRoute = false;
            }

            // if next signal is found and relevant, set reference

            if (nextSignal != null)
            {
                Signalfound[(int)SignalFunction.Normal] = nextSignal.Index;
            }
            else
            {
                Signalfound[(int)SignalFunction.Normal] = -1;
            }

            // set number of signals to clear ahead

            if (SignalNumClearAhead_MSTS > -2)
            {
                requestedNumClearAhead = clearNextSignals > 0 ?
                    clearNextSignals - signalNumberNormalHeads : SignalNumClearAhead_MSTS - signalNumberNormalHeads;
            }
            else
            {
                if (SignalNumClearAheadActive == -1)
                {
                    requestedNumClearAhead = clearNextSignals > 0 ? clearNextSignals : 1;
                }
                else if (SignalNumClearAheadActive == 0)
                {
                    requestedNumClearAhead = 0;
                }
                else
                {
                    requestedNumClearAhead = clearNextSignals > 0 ? clearNextSignals - 1 : SignalNumClearAheadActive - 1;
                }
            }

            // perform route check

            checkRouteState(isPropagated, SignalRoute, thisTrain);

            // propagate request

            if (!isPropagated && EnabledTrain != null)
            {
                propagateRequest();
            }
            if (thisTrain != null && thisTrain.Train is AITrain && Math.Abs(thisTrain.Train.SpeedMpS) <= Simulator.MaxStoppedMpS)
            {
                WorldLocation location = this.TdbTraveller.WorldLocation;
                ((AITrain)thisTrain.Train).AuxActionsContain.CheckGenActions(this.GetType(), location, 0f, 0f, this.TdbTraveller.TrackNodeIndex);
            }

            return (this_sig_mr(SignalFunction.Normal) != SignalAspectState.Stop);
        }

        //================================================================================================//
        /// <summary>
        /// check and update Route State
        /// </summary>

        public void checkRouteState(bool isPropagated, Train.TCSubpathRoute thisRoute, Train.TrainRouted thisTrain, bool sound = true)
        {
            // check if signal must be hold
            bool signalHold = (HoldState != SignalHoldState.None);
            if (EnabledTrain != null && EnabledTrain.Train.HoldingSignals.Contains(Index) && HoldState < SignalHoldState.ManualLock)
            {
                HoldState = SignalHoldState.StationStop;
                signalHold = true;
            }
            else if (HoldState == SignalHoldState.StationStop)
            {
                if (EnabledTrain == null || !EnabledTrain.Train.HoldingSignals.Contains(Index))
                {
                    HoldState = SignalHoldState.None;
                    signalHold = false;
                }
            }

            // check if signal has route, is enabled, request is by enabled train and train is not occupying sections in signal route

            if (EnabledTrain != null && EnabledTrain == thisTrain && SignalRoute != null && SignalRoute.Count > 0)
            {
                var forcedRouteElementIndex = -1;
                foreach (Train.TCRouteElement routeElement in SignalRoute)
                {
                    TrackCircuitSection routeSection = SignalEnvironment.TrackCircuitList[routeElement.TCSectionIndex];
                    if (routeSection.CircuitState.OccupiedByThisTrain(thisTrain))
                    {
                        return;  // train has passed signal - clear request is invalid
                    }
                    if (routeSection.CircuitState.Forced)
                    {
                        // route must be recomputed after switch moved by dispatcher
                        forcedRouteElementIndex = SignalRoute.IndexOf(routeElement);
                        break;
                    }
                }
                if (forcedRouteElementIndex >= 0)
                {
                    int forcedTCSectionIndex = SignalRoute[forcedRouteElementIndex].TCSectionIndex;
                    TrackCircuitSection forcedTrackSection = SignalEnvironment.TrackCircuitList[forcedTCSectionIndex];
                    int forcedRouteSectionIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(forcedTCSectionIndex, 0);
                    thisTrain.Train.ReRouteTrain(forcedRouteSectionIndex, forcedTCSectionIndex);
                    if (thisTrain.Train.TrainType == Train.TRAINTYPE.AI || thisTrain.Train.TrainType == Train.TRAINTYPE.AI_PLAYERHOSTING)
                        (thisTrain.Train as AITrain).ResetActions(true);
                    forcedTrackSection.CircuitState.Forced = false;
                }
            }

            // test if propagate state still correct - if next signal for enabled train is this signal, it is not propagated

            if (EnabledTrain != null && EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex] != null &&
                EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex].Index == Index)
            {
                isPropagated = false;
            }

            // test clearance for full route section

            if (!signalHold)
            {
                if (fullRoute)
                {
                    bool newroute = getBlockState(thisRoute, thisTrain, !sound);
                    if (newroute)
                        thisRoute = this.SignalRoute;
                }

                // test clearance for sections in route only if first signal ahead of train or if clearance unto partial route is allowed

                else if (EnabledTrain != null && (!isPropagated || allowPartialRoute) && thisRoute.Count > 0)
                {
                    getPartBlockState(thisRoute);
                }

                // test clearance for sections in route if signal is second signal ahead of train, first signal route is clear but first signal is still showing STOP
                // case for double-hold signals

                else if (EnabledTrain != null && isPropagated)
                {
                    Signal firstSignal = EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex];
                    if (firstSignal != null &&
                        firstSignal.Signalfound[(int)SignalFunction.Normal] == Index &&
                        firstSignal.internalBlockState <= InternalBlockstate.Reservable &&
                        firstSignal.this_sig_lr(SignalFunction.Normal) == SignalAspectState.Stop)
                    {
                        getPartBlockState(thisRoute);
                    }
                }
            }

            // else consider route blocked

            else
            {
                internalBlockState = InternalBlockstate.Blocked;
            }

            // derive signal state

            StateUpdate();
            SignalAspectState signalState = this_sig_lr(SignalFunction.Normal);

            float lengthReserved = 0.0f;

            // check for permission

            if (internalBlockState == InternalBlockstate.OccupiedSameDirection && OverridePermission == SignalPermission.Requested && !isPropagated)
            {
                OverridePermission = SignalPermission.Granted;
                if (sound) SignalEnvironment.Simulator.SoundNotify = TrainEvent.PermissionGranted;
            }
            else
            {
                if (EnabledTrain != null && EnabledTrain.Train.ControlMode == Train.TRAIN_CONTROL.MANUAL &&
                    internalBlockState <= InternalBlockstate.OccupiedSameDirection && OverridePermission == SignalPermission.Requested)
                {
                    SignalEnvironment.Simulator.SoundNotify = TrainEvent.PermissionGranted;
                }
                else if (OverridePermission == SignalPermission.Requested)
                {
                    if (sound) SignalEnvironment.Simulator.SoundNotify = TrainEvent.PermissionDenied;
                }

                if (EnabledTrain != null && EnabledTrain.Train.ControlMode == Train.TRAIN_CONTROL.MANUAL && signalState == SignalAspectState.Stop &&
                internalBlockState <= InternalBlockstate.OccupiedSameDirection && OverridePermission == SignalPermission.Requested)
                {
                    OverridePermission = SignalPermission.Granted;
                }
                else if (OverridePermission == SignalPermission.Requested)
                {
                    OverridePermission = SignalPermission.Denied;
                }
            }

            // reserve full section if allowed, do not set reserved if signal is held on approach control

            if (EnabledTrain != null)
            {
                if (internalBlockState == InternalBlockstate.Reservable && !approachControlSet)
                {
                    internalBlockState = InternalBlockstate.Reserved; // preset all sections are reserved

                    foreach (Train.TCRouteElement thisElement in thisRoute)
                    {
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                        if (thisSection.CircuitState.TrainReserved != null || thisSection.CircuitState.OccupationState.Count > 0)
                        {
                            if (thisSection.CircuitState.TrainReserved != thisTrain)
                            {
                                internalBlockState = InternalBlockstate.Reservable; // not all sections are reserved // 
                                break;
                            }
                        }
                        thisSection.Reserve(EnabledTrain, thisRoute);
                        EnabledTrain.Train.LastReservedSection[EnabledTrain.TrainRouteDirectionIndex] = thisElement.TCSectionIndex;
                        lengthReserved += thisSection.Length;
                    }

                    EnabledTrain.Train.ClaimState = false;
                }

                // reserve partial sections if signal clears on occupied track or permission is granted

                else if ((signalState > SignalAspectState.Stop || OverridePermission == SignalPermission.Granted) &&
                         (internalBlockState != InternalBlockstate.Reserved && internalBlockState < InternalBlockstate.ReservedOther))
                {

                    // reserve upto available section

                    int lastSectionIndex = 0;
                    bool reservable = true;

                    for (int iSection = 0; iSection < thisRoute.Count && reservable; iSection++)
                    {
                        Train.TCRouteElement thisElement = thisRoute[iSection];
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];

                        if (thisSection.IsAvailable(EnabledTrain))
                        {
                            if (thisSection.CircuitState.TrainReserved == null)
                            {
                                thisSection.Reserve(EnabledTrain, thisRoute);
                            }
                            EnabledTrain.Train.LastReservedSection[EnabledTrain.TrainRouteDirectionIndex] = thisElement.TCSectionIndex;
                            lastSectionIndex = iSection;
                            lengthReserved += thisSection.Length;
                        }
                        else
                        {
                            reservable = false;
                        }
                    }

                    // set pre-reserved or reserved for all other sections

                    for (int iSection = lastSectionIndex++; iSection < thisRoute.Count && reservable; iSection++)
                    {
                        Train.TCRouteElement thisElement = thisRoute[iSection];
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];

                        if (thisSection.IsAvailable(EnabledTrain) && thisSection.CircuitState.TrainReserved == null)
                        {
                            thisSection.Reserve(EnabledTrain, thisRoute);
                        }
                        else if (thisSection.CircuitState.OccupiedByOtherTrains(EnabledTrain))
                        {
                            thisSection.PreReserve(EnabledTrain);
                        }
                        else if (thisSection.CircuitState.TrainReserved == null || thisSection.CircuitState.TrainReserved.Train != EnabledTrain.Train)
                        {
                            thisSection.PreReserve(EnabledTrain);
                        }
                        else
                        {
                            reservable = false;
                        }
                    }
                    EnabledTrain.Train.ClaimState = false;
                }

                // if claim allowed - reserve free sections and claim all other if first signal ahead of train

                else if (EnabledTrain.Train.ClaimState && internalBlockState != InternalBlockstate.Reserved &&
                         EnabledTrain.Train.NextSignalObject[0] != null && EnabledTrain.Train.NextSignalObject[0].Index == Index)
                {
                    foreach (Train.TCRouteElement thisElement in thisRoute)
                    {
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                        if (thisSection.DeadlockReference > 0) // do not claim into deadlock area as path may not have been resolved
                        {
                            break;
                        }

                        if (thisSection.CircuitState.TrainReserved == null || (thisSection.CircuitState.TrainReserved.Train != EnabledTrain.Train))
                        {
                            // deadlock has been set since signal request was issued - reject claim, break and reset claimstate
                            if (thisSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number))
                            {
                                thisTrain.Train.ClaimState = false;
                                break;
                            }

                            // claim only if signal claim is not locked (in case of approach control)
                            if (!claimLocked)
                            {
                                thisSection.Claim(EnabledTrain);
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// propagate clearance request
        /// </summary>

        private void propagateRequest()
        {
            // no. of next signals to clear : as passed on -1 if signal has normal clear ahead
            // if passed on < 0, use this signals num to clear

            // sections not available
            bool validPropagationRequest = true;
            if (internalBlockState > InternalBlockstate.Reservable)
            {
                validPropagationRequest = false;
            }

            // sections not reserved and no forced propagation
            if (!ForcePropagation && !forcePropOnApproachControl && internalBlockState > InternalBlockstate.Reserved)
            {
                validPropagationRequest = false;
            }

            // route is not fully available so do not propagate
            if (!validPropagationRequest)
            {
                return;
            }

            Signal nextSignal = null;
            if (Signalfound[(int)SignalFunction.Normal] >= 0)
            {
                nextSignal = SignalEnvironment.SignalObjects[Signalfound[(int)SignalFunction.Normal]];
            }

            Train.TCSubpathRoute RoutePart;
            if (EnabledTrain != null)
            {
                RoutePart = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex];   // if known which route to use
            }
            else
            {
                RoutePart = SignalRoute; // else use signal route
            }

            bool propagateState = true;  // normal propagate state

            // update ReqNumClearAhead if signal is not propagated (only when SignamNumClearAheadActive has other than default value)

            if (!isPropagated)
            {
                // set number of signals to clear ahead

                if (SignalNumClearAhead_MSTS <= -2 && SignalNumClearAheadActive != SignalNumClearAhead_ORTS)
                {
                    if (SignalNumClearAheadActive == 0)
                    {
                        requestedNumClearAhead = 0;
                    }
                    else if (SignalNumClearAheadActive > 0)
                    {
                        requestedNumClearAhead = SignalNumClearAheadActive - 1;
                    }
                    else if (SignalNumClearAheadActive < 0)
                    {
                        requestedNumClearAhead = 1;
                    }
                }
            }

            bool validBlockState = internalBlockState <= InternalBlockstate.Reserved;

            // for approach control, use reservable state instead of reserved state (sections are not reserved on approach control)
            // also on forced propagation, use reservable state instead of reserved state
            if (approachControlSet && forcePropOnApproachControl)
            {
                validBlockState = internalBlockState <= InternalBlockstate.Reservable;
            }

            // if section is clear but signal remains at stop - dual signal situation - do not treat as propagate
            if (validBlockState && this_sig_lr(SignalFunction.Normal) == SignalAspectState.Stop && SignalNormal())
            {
                propagateState = false;
            }

            if ((requestedNumClearAhead > 0 || ForcePropagation) && nextSignal != null && validBlockState && (!approachControlSet || forcePropOnApproachControl))
            {
                nextSignal.requestClearSignal(RoutePart, EnabledTrain, requestedNumClearAhead, propagateState, this);
                propagated = true;
                ForcePropagation = false;
            }

            // check if next signal is cleared by default (state != stop and enabled == false) - if so, set train as enabled train but only if train's route covers signal route

            if (nextSignal != null && nextSignal.this_sig_lr(SignalFunction.Normal) >= SignalAspectState.Approach_1 && nextSignal.FixedRoute && !nextSignal.Enabled && EnabledTrain != null)
            {
                int firstSectionIndex = nextSignal.fixedRoute.First().TCSectionIndex;
                int lastSectionIndex = nextSignal.fixedRoute.Last().TCSectionIndex;
                int firstSectionRouteIndex = RoutePart.GetRouteIndex(firstSectionIndex, 0);
                int lastSectionRouteIndex = RoutePart.GetRouteIndex(lastSectionIndex, 0);

                if (firstSectionRouteIndex >= 0 && lastSectionRouteIndex >= 0)
                {
                    nextSignal.requestClearSignal(nextSignal.fixedRoute, EnabledTrain, 0, true, null);

                    int furtherSignalIndex = nextSignal.Signalfound[(int)SignalFunction.Normal];
                    int furtherSignalsToClear = requestedNumClearAhead - 1;

                    while (furtherSignalIndex >= 0)
                    {
                        Signal furtherSignal = SignalEnvironment.SignalObjects[furtherSignalIndex];
                        if (furtherSignal.this_sig_lr(SignalFunction.Normal) >= SignalAspectState.Approach_1 && !furtherSignal.Enabled && furtherSignal.FixedRoute)
                        {
                            firstSectionIndex = furtherSignal.fixedRoute.First().TCSectionIndex;
                            lastSectionIndex = furtherSignal.fixedRoute.Last().TCSectionIndex;
                            firstSectionRouteIndex = RoutePart.GetRouteIndex(firstSectionIndex, 0);
                            lastSectionRouteIndex = RoutePart.GetRouteIndex(lastSectionIndex, 0);

                            if (firstSectionRouteIndex >= 0 && lastSectionRouteIndex >= 0)
                            {
                                furtherSignal.requestClearSignal(furtherSignal.fixedRoute, EnabledTrain, 0, true, null);

                                furtherSignal.isPropagated = true;
                                furtherSignalsToClear = furtherSignalsToClear > 0 ? furtherSignalsToClear - 1 : 0;
                                furtherSignal.requestedNumClearAhead = furtherSignalsToClear;
                                furtherSignalIndex = furtherSignal.Signalfound[(int)SignalFunction.Normal];
                            }
                            else
                            {
                                furtherSignalIndex = -1;
                            }
                        }
                        else
                        {
                            furtherSignalIndex = -1;
                        }
                    }
                }
            }

        } //propagateRequest

        //================================================================================================//
        /// <summary>
        /// get block state - not routed
        /// Check blockstate for normal signal which is not enabled
        /// Check blockstate for other types of signals
        /// <summary>

        private void getBlockState_notRouted()
        {

            InternalBlockstate localBlockState = InternalBlockstate.Reserved; // preset to lowest option

            // check fixed route for normal signals

            if (SignalNormal() && FixedRoute)
            {
                foreach (Train.TCRouteElement thisElement in fixedRoute)
                {
                    TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                    if (thisSection.CircuitState.Occupied())
                    {
                        localBlockState = InternalBlockstate.OccupiedSameDirection;
                    }
                }
            }

            // otherwise follow sections upto first non-set switch or next signal
            else
            {
                int thisTC = TrackCircuitIndex;
                TrackDirection direction = TrackCircuitDirection;
                int nextTC = -1;

                // for normal signals : start at next TC

                if (TrackCircuitNextIndex > 0)
                {
                    thisTC = TrackCircuitNextIndex;
                    direction = TrackCircuitNextDirection;
                }

                // get trackcircuit

                TrackCircuitSection thisSection = null;
                if (thisTC > 0)
                {
                    thisSection = SignalEnvironment.TrackCircuitList[thisTC];
                }

                // loop through valid sections

                while (thisSection != null)
                {

                    // set blockstate

                    if (thisSection.CircuitState.Occupied())
                    {
                        if (thisSection.Index == TrackCircuitIndex)  // for section where signal is placed, check if train is ahead
                        {
                            Dictionary<Train, float> trainAhead = thisSection.TestTrainAhead(null, TrackCicruitOffset, (int)TrackCircuitDirection);
                            if (trainAhead.Count > 0)
                                localBlockState = InternalBlockstate.OccupiedSameDirection;
                        }
                        else
                        {
                            localBlockState = InternalBlockstate.OccupiedSameDirection;
                        }
                    }

                    // if section has signal at end stop check

                    if (thisSection.EndSignals[direction] != null)
                    {
                        thisSection = null;
                    }

                    // get next section if active link is set

                    else
                    {
                        //                     int pinIndex = direction == 0 ? 1 : 0;
                        TrackDirection pinIndex = direction;
                        nextTC = thisSection.ActivePins[pinIndex, Location.NearEnd].Link;
                        direction = thisSection.ActivePins[pinIndex, Location.NearEnd].Direction;
                        if (nextTC == -1)
                        {
                            nextTC = thisSection.ActivePins[pinIndex, Location.FarEnd].Link;
                            direction = thisSection.ActivePins[pinIndex, Location.FarEnd].Direction;
                        }

                        // set state to blocked if ending at unset or unaligned switch

                        if (nextTC >= 0)
                        {
                            thisSection = SignalEnvironment.TrackCircuitList[nextTC];
                        }
                        else
                        {
                            thisSection = null;
                            localBlockState = InternalBlockstate.Blocked;
                        }
                    }
                }
            }

            internalBlockState = localBlockState;
        }

        //================================================================================================//
        /// <summary>
        /// Get block state
        /// Get internal state of full block for normal enabled signal upto next signal for clear request
        /// returns true if train set to use alternative route
        /// </summary>

        private bool getBlockState(Train.TCSubpathRoute thisRoute, Train.TrainRouted thisTrain, bool AIPermissionRequest)
        {
            if (SignalEnvironment.UseLocationPassingPaths)
            {
                return (getBlockState_locationBased(thisRoute, thisTrain, AIPermissionRequest));
            }
            else
            {
                return (getBlockState_pathBased(thisRoute, thisTrain));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Get block state
        /// Get internal state of full block for normal enabled signal upto next signal for clear request
        /// returns true if train set to use alternative route
        /// based on path-based deadlock processing
        /// </summary>

        private bool getBlockState_pathBased(Train.TCSubpathRoute thisRoute, Train.TrainRouted thisTrain)
        {
            bool returnvalue = false;

            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // loop through all sections in route list

            Train.TCRouteElement lastElement = null;

            foreach (Train.TCRouteElement thisElement in thisRoute)
            {
                lastElement = thisElement;
                TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                int direction = thisElement.Direction;
                blockstate = thisSection.GetSectionState(EnabledTrain, direction, blockstate, thisRoute, Index);
                if (blockstate > InternalBlockstate.Reservable)
                    break;           // break on first non-reservable section //

                // if alternative path from section available but train already waiting for deadlock, set blocked
                if (thisElement.StartAlternativePath != null)
                {
                    TrackCircuitSection endSection = SignalEnvironment.TrackCircuitList[thisElement.StartAlternativePath[1]];
                    if (endSection.CheckDeadlockAwaited(thisTrain.Train.Number))
                    {
                        blockstate = InternalBlockstate.Blocked;
                        lastElement = thisElement;
                        break;
                    }
                }
            }

            // check if alternative route available

            int lastElementIndex = thisRoute.GetRouteIndex(lastElement.TCSectionIndex, 0);

            if (blockstate > InternalBlockstate.Reservable && thisTrain != null)
            {
                int startAlternativeRoute = -1;
                int altRoute = -1;

                Train.TCSubpathRoute trainRoute = thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex];
                Train.TCPosition thisPosition = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex];

                for (int iElement = lastElementIndex; iElement >= 0; iElement--)
                {
                    Train.TCRouteElement prevElement = thisRoute[iElement];
                    if (prevElement.StartAlternativePath != null)
                    {
                        startAlternativeRoute =
                            trainRoute.GetRouteIndex(thisRoute[iElement].TCSectionIndex, thisPosition.RouteListIndex);
                        altRoute = prevElement.StartAlternativePath[0];
                        break;
                    }
                }

                // check if alternative path may be used

                if (startAlternativeRoute > 0)
                {
                    Train.TCRouteElement startElement = trainRoute[startAlternativeRoute];
                    int endSectionIndex = startElement.StartAlternativePath[1];
                    TrackCircuitSection endSection = SignalEnvironment.TrackCircuitList[endSectionIndex];
                    if (endSection.CheckDeadlockAwaited(thisTrain.Train.Number))
                    {
                        startAlternativeRoute = -1; // reset use of alternative route
                    }
                }

                // if available, select part of route upto next signal

                if (startAlternativeRoute > 0)
                {
                    Train.TCSubpathRoute altRoutePart = thisTrain.Train.ExtractAlternativeRoute_pathBased(altRoute);

                    // check availability of alternative route

                    InternalBlockstate newblockstate = InternalBlockstate.Reservable;

                    foreach (Train.TCRouteElement thisElement in altRoutePart)
                    {
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                        int direction = thisElement.Direction;
                        newblockstate = thisSection.GetSectionState(EnabledTrain, direction, newblockstate, thisRoute, Index);
                        if (newblockstate > InternalBlockstate.Reservable)
                            break;           // break on first non-reservable section //
                    }

                    // if available, use alternative route

                    if (newblockstate <= InternalBlockstate.Reservable)
                    {
                        blockstate = newblockstate;
                        thisTrain.Train.SetAlternativeRoute_pathBased(startAlternativeRoute, altRoute, this);
                        returnvalue = true;
                    }
                }
            }

            // check if approaching deadlock part, and if alternative route must be taken - if point where alt route start is not yet reserved
            // alternative route may not be taken if there is a train already waiting for the deadlock
            else if (thisTrain != null)
            {
                int startAlternativeRoute = -1;
                int altRoute = -1;
                TrackCircuitSection startSection = null;
                TrackCircuitSection endSection = null;

                Train.TCSubpathRoute trainRoute = thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex];
                Train.TCPosition thisPosition = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex];

                for (int iElement = lastElementIndex; iElement >= 0; iElement--)
                {
                    Train.TCRouteElement prevElement = thisRoute[iElement];
                    if (prevElement.StartAlternativePath != null)
                    {
                        endSection = SignalEnvironment.TrackCircuitList[prevElement.StartAlternativePath[1]];
                        if (endSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number) && !endSection.CheckDeadlockAwaited(thisTrain.Train.Number))
                        {
                            altRoute = prevElement.StartAlternativePath[0];
                            startAlternativeRoute =
                                trainRoute.GetRouteIndex(prevElement.TCSectionIndex, thisPosition.RouteListIndex);
                            startSection = SignalEnvironment.TrackCircuitList[prevElement.TCSectionIndex];
                        }
                        break;
                    }
                }

                // use alternative route

                if (startAlternativeRoute > 0 &&
                    (startSection.CircuitState.TrainReserved == null || startSection.CircuitState.TrainReserved.Train != thisTrain.Train))
                {
                    Train.TCSubpathRoute altRoutePart = thisTrain.Train.ExtractAlternativeRoute_pathBased(altRoute);

                    // check availability of alternative route

                    InternalBlockstate newblockstate = InternalBlockstate.Reservable;

                    foreach (Train.TCRouteElement thisElement in altRoutePart)
                    {
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                        int direction = thisElement.Direction;
                        newblockstate = thisSection.GetSectionState(EnabledTrain, direction, newblockstate, thisRoute, Index);
                        if (newblockstate > InternalBlockstate.Reservable)
                            break;           // break on first non-reservable section //
                    }

                    // if available, use alternative route

                    if (newblockstate <= InternalBlockstate.Reservable)
                    {
                        blockstate = newblockstate;
                        thisTrain.Train.SetAlternativeRoute_pathBased(startAlternativeRoute, altRoute, this);
                        if (endSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number) && !endSection.DeadlockAwaited.Contains(thisTrain.Train.Number))
                            endSection.DeadlockAwaited.Add(thisTrain.Train.Number);
                        returnvalue = true;

                    }
                }
            }

            internalBlockState = blockstate;
            return (returnvalue);
        }

        //================================================================================================//
        /// <summary>
        /// Get block state
        /// Get internal state of full block for normal enabled signal upto next signal for clear request
        /// returns true if train set to use alternative route
        /// based on location-based deadlock processing
        /// </summary>

        private bool getBlockState_locationBased(Train.TCSubpathRoute thisRoute, Train.TrainRouted thisTrain, bool AIPermissionRequest)
        {
            List<int> SectionsWithAlternativePath = new List<int>();
            List<int> SectionsWithAltPathSet = new List<int>();
            bool altRouteAssigned = false;

            bool returnvalue = false;
            bool deadlockArea = false;

            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // loop through all sections in route list

            Train.TCRouteElement lastElement = null;

            foreach (Train.TCRouteElement thisElement in thisRoute)
            {
                lastElement = thisElement;
                TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                int direction = thisElement.Direction;

                blockstate = thisSection.GetSectionState(EnabledTrain, direction, blockstate, thisRoute, Index);
                if (blockstate > InternalBlockstate.OccupiedSameDirection)
                    break;     // exit on first none-available section

                // check if section is trigger section for waitany instruction
                if (thisTrain != null)
                {
                    if (thisTrain.Train.CheckAnyWaitCondition(thisSection.Index))
                    {
                        blockstate = InternalBlockstate.Blocked;
                    }
                }

                // check if this section is start of passing path area
                // if so, select which path must be used - but only if cleared by train in AUTO mode

                if (thisSection.DeadlockReference > 0 && thisElement.FacingPoint && thisTrain != null)
                {
                    if (thisTrain.Train.ControlMode == Train.TRAIN_CONTROL.AUTO_NODE || thisTrain.Train.ControlMode == Train.TRAIN_CONTROL.AUTO_SIGNAL)
                    {
                        DeadlockInfo sectionDeadlockInfo = SignalEnvironment.DeadlockInfoList[thisSection.DeadlockReference];

                        // if deadlock area and no path yet selected - exit loop; else follow assigned path
                        if (sectionDeadlockInfo.HasTrainAndSubpathIndex(thisTrain.Train.Number, thisTrain.Train.TCRoute.activeSubpath) &&
                            thisElement.UsedAlternativePath < 0)
                        {
                            deadlockArea = true;
                            break; // exits on deadlock area
                        }
                        else
                        {
                            SectionsWithAlternativePath.Add(thisElement.TCSectionIndex);
                            altRouteAssigned = true;
                        }
                    }
                }
                if (thisTrain != null && blockstate == InternalBlockstate.OccupiedSameDirection && (AIPermissionRequest || OverridePermission == SignalPermission.Requested)) break;
            }

            // if deadlock area : check alternative path if not yet selected - but only if opening junction is reservable
            // if free alternative path is found, set path available otherwise set path blocked

            if (deadlockArea && lastElement.UsedAlternativePath < 0)
            {
                if (blockstate <= InternalBlockstate.Reservable)
                {

                    TrackCircuitSection lastSection = SignalEnvironment.TrackCircuitList[lastElement.TCSectionIndex];
                    DeadlockInfo sectionDeadlockInfo = SignalEnvironment.DeadlockInfoList[lastSection.DeadlockReference];
                    List<int> availableRoutes = sectionDeadlockInfo.CheckDeadlockPathAvailability(lastSection, thisTrain.Train);

                    if (availableRoutes.Count >= 1)
                    {
                        int endSectionIndex = -1;
                        int usedRoute = sectionDeadlockInfo.SelectPath(availableRoutes, thisTrain.Train, ref endSectionIndex);
                        lastElement.UsedAlternativePath = usedRoute;
                        SectionsWithAltPathSet.Add(lastElement.TCSectionIndex);
                        altRouteAssigned = true;

                        thisTrain.Train.SetAlternativeRoute_locationBased(lastSection.Index, sectionDeadlockInfo, usedRoute, this);
                        returnvalue = true;
                        blockstate = InternalBlockstate.Reservable;
                    }
                    else
                    {
                        blockstate = InternalBlockstate.Blocked;
                    }
                }
                else
                {
                    blockstate = InternalBlockstate.Blocked;
                }
            }

            internalBlockState = blockstate;

            // reset any alternative route selections if route is not available
            if (altRouteAssigned && blockstate != InternalBlockstate.Reservable && blockstate != InternalBlockstate.Reserved)
            {
                foreach (int SectionNo in SectionsWithAlternativePath)
                {
                    int routeIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(SectionNo, thisTrain.Train.PresentPosition[0].RouteListIndex);
                    Train.TCRouteElement thisElement = thisTrain.Train.ValidRoute[0][routeIndex];
                    thisElement.UsedAlternativePath = -1;
                }
                foreach (int SectionNo in SectionsWithAltPathSet)
                {
                    int routeIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(SectionNo, thisTrain.Train.PresentPosition[0].RouteListIndex);
                    Train.TCRouteElement thisElement = thisTrain.Train.ValidRoute[0][routeIndex];
                    thisElement.UsedAlternativePath = -1;
                }
            }

            return (returnvalue);
        }

        //================================================================================================//
        /// <summary>
        /// Get part block state
        /// Get internal state of part of block for normal enabled signal upto next signal for clear request
        /// if there are no switches before next signal or end of track, treat as full block
        /// </summary>

        private void getPartBlockState(Train.TCSubpathRoute thisRoute)
        {

            // check beyond last section for next signal or end of track 

            int listIndex = (thisRoute.Count > 0) ? (thisRoute.Count - 1) : TrainRouteIndex;

            Train.TCRouteElement lastElement = thisRoute[listIndex];
            int thisSectionIndex = lastElement.TCSectionIndex;
            TrackDirection direction = (TrackDirection)lastElement.Direction;

            Train.TCSubpathRoute additionalElements = new Train.TCSubpathRoute();

            bool end_of_info = false;

            while (!end_of_info)
            {
                TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisSectionIndex];

                TrackCircuitType thisType = thisSection.CircuitType;

                switch (thisType)
                {
                    case (TrackCircuitType.EndOfTrack):
                        end_of_info = true;
                        break;

                    case (TrackCircuitType.Junction):
                    case (TrackCircuitType.Crossover):
                        end_of_info = true;
                        break;

                    default:
                        Train.TCRouteElement newElement = new Train.TCRouteElement(thisSectionIndex, (int)direction);
                        additionalElements.Add(newElement);

                        if (thisSection.EndSignals[direction] != null)
                        {
                            end_of_info = true;
                        }
                        break;
                }

                if (!end_of_info)
                {
                    thisSectionIndex = thisSection.Pins[direction, Location.NearEnd].Link;
                    direction = thisSection.Pins[direction, Location.NearEnd].Direction;
                }
            }

            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // check all elements in original route

            foreach (Train.TCRouteElement thisElement in thisRoute)
            {
                TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                direction = (TrackDirection)thisElement.Direction;
                blockstate = thisSection.GetSectionState(EnabledTrain, (int)direction, blockstate, thisRoute, Index);
                if (blockstate > InternalBlockstate.Reservable)
                    break;           // break on first non-reservable section //
            }

            // check all additional elements upto signal, junction or end-of-track

            if (blockstate <= InternalBlockstate.Reservable)
            {
                foreach (Train.TCRouteElement thisElement in additionalElements)
                {
                    TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                    direction = (TrackDirection)thisElement.Direction;
                    blockstate = thisSection.GetSectionState(EnabledTrain, (int)direction, blockstate, additionalElements, Index);
                    if (blockstate > InternalBlockstate.Reservable)
                        break;           // break on first non-reservable section //
                }
            }

            //          if (blockstate <= INTERNAL_BLOCKSTATE.RESERVABLE && end_at_junction)
            //          {
            //              blockstate = INTERNAL_BLOCKSTATE.OCCUPIED_SAMEDIR;  // set restricted state
            //          }

            internalBlockState = blockstate;

        }

        //================================================================================================//
        /// <summary>
        /// Set signal default route and next signal list as switch in route is reset
        /// Used in manual mode for signals which clear by default
        /// </summary>

        public void SetDefaultRoute()
        {
            SignalRoute = new Train.TCSubpathRoute(fixedRoute);
            for (int iSigtype = 0; iSigtype < defaultNextSignal.Length; iSigtype++)
            {
                Signalfound[iSigtype] = defaultNextSignal[iSigtype];
            }
        }

        //================================================================================================//
        /// <summary>
        /// Reset signal and clear all train sections
        /// </summary>

        public void ResetSignal(bool propagateReset)
        {
            Train.TrainRouted thisTrain = EnabledTrain;

            // search for last signal enabled for this train, start reset from there //

            Signal thisSignal = this;
            List<Signal> passedSignals = new List<Signal>();
            int thisSignalIndex = thisSignal.Index;

            if (propagateReset)
            {
                while (thisSignalIndex >= 0 && SignalEnvironment.SignalObjects[thisSignalIndex].EnabledTrain == thisTrain)
                {
                    thisSignal = SignalEnvironment.SignalObjects[thisSignalIndex];
                    passedSignals.Add(thisSignal);
                    thisSignalIndex = thisSignal.Signalfound[(int)SignalFunction.Normal];
                }
            }
            else
            {
                passedSignals.Add(thisSignal);
            }

            foreach (Signal nextSignal in passedSignals)
            {
                if (nextSignal.SignalRoute != null)
                {
                    List<TrackCircuitSection> sectionsToClear = new List<TrackCircuitSection>();
                    foreach (Train.TCRouteElement thisElement in nextSignal.SignalRoute)
                    {
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                        sectionsToClear.Add(thisSection);  // store in list as signalRoute is lost during remove action
                    }
                    foreach (TrackCircuitSection thisSection in sectionsToClear)
                    {
                        if (thisTrain != null)
                        {
                            thisSection.RemoveTrain(thisTrain, false);
                        }
                        else
                        {
                            thisSection.Unreserve();
                        }
                    }
                }

                nextSignal.resetSignalEnabled();
            }
        }

        //================================================================================================//
        /// <summary>
        /// Reset signal route and next signal list as switch in route is reset
        /// </summary>

        public void ResetRoute(int resetSectionIndex)
        {

            // remove this signal from any other junctions

            foreach (int thisSectionIndex in junctionsPassed)
            {
                if (thisSectionIndex != resetSectionIndex)
                {
                    TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisSectionIndex];
                    thisSection.SignalsPassingRoutes.Remove(Index);
                }
            }

            junctionsPassed.Clear();

            for (int fntype = 0; fntype < SignalEnvironment.OrtsSignalTypeCount; fntype++)
            {
                Signalfound[fntype] = defaultNextSignal[fntype];
            }

            // if signal is enabled, ensure next normal signal is reset

            if (EnabledTrain != null && Signalfound[(int)SignalFunction.Normal] < 0)
            {
                Signalfound[(int)SignalFunction.Normal] = SONextSignalNormal(TrackCircuitNextIndex);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set flag to allow signal to clear to partial route
        /// </summary>
        public void AllowClearPartialRoute(int setting)
        {
            allowPartialRoute = setting == 1;
        }

        //================================================================================================//
        /// <summary>
        /// Test for approach control - position only
        /// </summary>

        public bool ApproachControlPosition(int reqPositionM, bool forced)
        {
            // no train approaching
            if (EnabledTrain == null)
            {
                return false;
            }

            // signal is not first signal for train - check only if not forced
            if (!forced)
            {
                if (EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex] == null ||
                    EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex].Index != Index)
                {
                    approachControlSet = true;  // approach control is selected but train is yet further out, so assume approach control has locked signal
                    return false;
                }
            }

            // if already cleared - return true

            if (approachControlCleared)
            {
                approachControlSet = false;
                claimLocked = false;
                forcePropOnApproachControl = false;
                return (true);
            }

            bool found = false;
            float distance = 0;
            int actDirection = EnabledTrain.TrainRouteDirectionIndex;
            Train.TCSubpathRoute routePath = EnabledTrain.Train.ValidRoute[actDirection];
            int actRouteIndex = routePath == null ? -1 : routePath.GetRouteIndex(EnabledTrain.Train.PresentPosition[actDirection].TCSectionIndex, 0);
            if (actRouteIndex >= 0)
            {
                float offset = 0;
                if (EnabledTrain.TrainRouteDirectionIndex == 0)
                    offset = EnabledTrain.Train.PresentPosition[0].TCOffset;
                else
                    offset = SignalEnvironment.TrackCircuitList[EnabledTrain.Train.PresentPosition[1].TCSectionIndex].Length - EnabledTrain.Train.PresentPosition[1].TCOffset;
                while (!found)
                {
                    Train.TCRouteElement thisElement = routePath[actRouteIndex];
                    TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                    distance += thisSection.Length - offset;
                    if (thisSection.EndSignals[(TrackDirection)thisElement.Direction] == this)
                    {
                        found = true;
                    }
                    else
                    {
                        offset = 0;
                        int setSection = thisSection.ActivePins[(TrackDirection)thisElement.OutPin[0], (Location)thisElement.OutPin[1]].Link;
                        actRouteIndex++;
                        if (actRouteIndex >= routePath.Count || setSection < 0)
                            break;
                    }
                }
            }

            if (!found)
            {
                approachControlSet = true;
                return (false);
            }

            // test distance

            if (Convert.ToInt32(distance) < reqPositionM)
            {
                approachControlSet = false;
                approachControlCleared = true;
                claimLocked = false;
                forcePropOnApproachControl = false;
                return (true);
            }
            else
            {
                approachControlSet = true;
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Test for approach control - position and speed
        /// </summary>

        public bool ApproachControlSpeed(int reqPositionM, int reqSpeedMpS)
        {
            // no train approaching
            if (EnabledTrain == null)
            {
                return (false);
            }

            // signal is not first signal for train
            if (EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex] != null &&
                EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex].Index != Index)
            {
                approachControlSet = true;
                return (false);
            }

            // if already cleared - return true

            if (approachControlCleared)
            {
                approachControlSet = false;
                forcePropOnApproachControl = false;
                return (true);
            }

            // check if distance is valid

            if (!EnabledTrain.Train.DistanceToSignal.HasValue)
            {
                approachControlSet = true;
                return (false);
            }

            // test distance

            if (Convert.ToInt32(EnabledTrain.Train.DistanceToSignal.Value) < reqPositionM)
            {
                bool validSpeed = false;
                if (reqSpeedMpS > 0)
                {
                    if (Math.Abs(EnabledTrain.Train.SpeedMpS) < reqSpeedMpS)
                    {
                        validSpeed = true;
                    }
                }
                else if (reqSpeedMpS == 0)
                {
                    if (Math.Abs(EnabledTrain.Train.SpeedMpS) < 0.1)
                    {
                        validSpeed = true;
                    }
                }

                if (validSpeed)
                {
                    approachControlCleared = true;
                    approachControlSet = false;
                    claimLocked = false;
                    forcePropOnApproachControl = false;
                    return (true);
                }
                else
                {
                    approachControlSet = true;
                    return (false);
                }
            }
            else
            {
                approachControlSet = true;
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Test for approach control in case of APC on next STOP
        /// </summary>

        public bool ApproachControlNextStop(int reqPositionM, int reqSpeedMpS)
        {
            // no train approaching
            if (EnabledTrain == null)
            {
                return (false);
            }

            // signal is not first signal for train
            if (EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex] != null &&
                EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex].Index != Index)
            {
                approachControlSet = true;
                forcePropOnApproachControl = true;
                return (false);
            }

            // if already cleared - return true

            if (approachControlCleared)
            {
                return (true);
            }

            // check if distance is valid

            if (!EnabledTrain.Train.DistanceToSignal.HasValue)
            {
                approachControlSet = true;
                return (false);
            }

            // test distance

            if (Convert.ToInt32(EnabledTrain.Train.DistanceToSignal.Value) < reqPositionM)
            {
                bool validSpeed = false;
                if (reqSpeedMpS > 0)
                {
                    if (Math.Abs(EnabledTrain.Train.SpeedMpS) < reqSpeedMpS)
                    {
                        validSpeed = true;
                    }
                }
                else if (reqSpeedMpS == 0)
                {
                    if (Math.Abs(EnabledTrain.Train.SpeedMpS) < 0.1)
                    {
                        validSpeed = true;
                    }
                }

                if (validSpeed)
                {
                    approachControlCleared = true;
                    approachControlSet = false;
                    claimLocked = false;
                    forcePropOnApproachControl = false;
                    return (true);
                }
                else
                {
                    approachControlSet = true;
                    forcePropOnApproachControl = true;
                    return (false);
                }
            }
            else
            {
                approachControlSet = true;
                forcePropOnApproachControl = true;
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Lock claim (only if approach control is active)
        /// </summary>

        public void LockClaim()
        {
            claimLocked = approachControlSet;
        }

        //================================================================================================//
        /// <summary>
        /// Activate timing trigger
        /// </summary>

        public void ActivateTimingTrigger()
        {
            timingTriggerValue = SignalEnvironment.Simulator.GameTime;
        }

        //================================================================================================//
        /// <summary>
        /// Check timing trigger
        /// </summary>

        public bool CheckTimingTrigger(int reqTiming)
        {
            int foundDelta = (int)(SignalEnvironment.Simulator.GameTime - timingTriggerValue);
            bool triggerExceeded = foundDelta > reqTiming;

            return (triggerExceeded);
        }

        //================================================================================================//
        /// <summary>
        /// Test if train has call-on set
        /// </summary>

        public bool TrainHasCallOn(bool allowOnNonePlatform, bool allowAdvancedSignal)
        {
            // no train approaching
            if (EnabledTrain == null)
            {
                return (false);
            }

            // signal is not first signal for train
            var nextSignal = EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex];

            if (!allowAdvancedSignal &&
               nextSignal != null && nextSignal.Index != Index)
            {
                return (false);
            }

            if (EnabledTrain.Train != null && SignalRoute != null)
            {
                bool callOnValid = EnabledTrain.Train.TestCallOn(this, allowOnNonePlatform, SignalRoute);
                return (callOnValid);
            }
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Test if train requires next signal
        /// </summary>

        public bool RequiresNextSignal(int nextSignalId, int reqPosition)
        {
            // no enabled train
            if (EnabledTrain == null)
            {
                return (false);
            }

            // train has no path
            Train reqTrain = EnabledTrain.Train;
            if (reqTrain.ValidRoute == null || reqTrain.ValidRoute[EnabledTrain.TrainRouteDirectionIndex] == null || reqTrain.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].Count <= 0)
            {
                return (false);
            }

            // next signal is not valid
            if (nextSignalId < 0 || nextSignalId >= SignalEnvironment.SignalObjects.Count || !SignalEnvironment.SignalObjects[nextSignalId].SignalNormal())
            {
                return (false);
            }

            // trains present position is unknown
            if (reqTrain.PresentPosition[EnabledTrain.TrainRouteDirectionIndex].RouteListIndex < 0 ||
                reqTrain.PresentPosition[EnabledTrain.TrainRouteDirectionIndex].RouteListIndex >= reqTrain.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].Count)
            {
                return (false);
            }

            // check if section beyond or ahead of next signal is within trains path ahead of present position of train
            int reqSection = reqPosition == 1 ? SignalEnvironment.SignalObjects[nextSignalId].TrackCircuitNextIndex : SignalEnvironment.SignalObjects[nextSignalId].TrackCircuitIndex;

            int sectionIndex = reqTrain.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].GetRouteIndex(reqSection, reqTrain.PresentPosition[EnabledTrain.TrainRouteDirectionIndex].RouteListIndex);
            if (sectionIndex > 0)
            {
                return (true);
            }
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Get ident of signal ahead with specific details
        /// </summary>

        public int FindReqNormalSignal(int req_value)
        {
            int foundSignal = -1;

            // signal not enabled - no route available
            if (EnabledTrain == null)
            {
            }
            else
            {
                int startIndex = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].GetRouteIndex(TrackCircuitNextIndex, EnabledTrain.Train.PresentPosition[0].RouteListIndex);
                if (startIndex < 0)
                {
                }
                else
                {
                    for (int iRouteIndex = startIndex; iRouteIndex < EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].Count; iRouteIndex++)
                    {
                        Train.TCRouteElement thisElement = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex][iRouteIndex];
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[thisElement.TCSectionIndex];
                        if (thisSection.EndSignals[(TrackDirection)thisElement.Direction] != null)
                        {
                            Signal endSignal = thisSection.EndSignals[(TrackDirection)thisElement.Direction];

                            // found signal, check required value
                            bool found_value = false;

                            foreach (SignalHead thisHead in endSignal.SignalHeads)
                            {
                                if (thisHead.OrtsNormalSubtypeIndex == req_value)
                                {
                                    found_value = true;
                                    break;
                                }
                            }

                            if (found_value)
                            {
                                foundSignal = endSignal.Index;
                                break;
                            }
                        }
                    }
                }
            }

            return (foundSignal);
        }

        //================================================================================================//
        /// <summary>
        /// Check if route for train is cleared upto or beyond next required signal
        /// parameter req_position : 0 = check upto signal, 1 = check beyond signal
        /// </summary>

        public SignalBlockState RouteClearedToSignal(int req_signalid, bool allowCallOn)
        {
            SignalBlockState routeState = SignalBlockState.Jn_Obstructed;
            if (EnabledTrain != null && EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex] != null && req_signalid >= 0 && req_signalid < SignalEnvironment.SignalObjects.Count)
            {
                Signal otherSignal = SignalEnvironment.SignalObjects[req_signalid];

                TrackCircuitSection reqSection = null;
                reqSection = SignalEnvironment.TrackCircuitList[otherSignal.TrackCircuitIndex];

                Train.TCSubpathRoute trainRoute = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex];

                int thisRouteIndex = trainRoute.GetRouteIndex(SignalNormal() ? TrackCircuitNextIndex : TrackCircuitIndex, 0);
                int otherRouteIndex = trainRoute.GetRouteIndex(otherSignal.TrackCircuitIndex, thisRouteIndex);
                if (otherRouteIndex < 0)
                {
                }
                // extract route
                else
                {
                    bool routeCleared = true;
                    Train.TCSubpathRoute reqPath = new Train.TCSubpathRoute(trainRoute, thisRouteIndex, otherRouteIndex);

                    for (int iIndex = 0; iIndex < reqPath.Count && routeCleared; iIndex++)
                    {
                        TrackCircuitSection thisSection = SignalEnvironment.TrackCircuitList[reqPath[iIndex].TCSectionIndex];
                        if (!thisSection.IsSet(EnabledTrain, false))
                        {
                            routeCleared = false;
                        }
                    }

                    if (routeCleared)
                    {
                        routeState = SignalBlockState.Clear;
                    }
                    else if (allowCallOn)
                    {
                        if (EnabledTrain.Train.TestCallOn(this, false, reqPath))
                        {
                            routeCleared = true;
                            routeState = SignalBlockState.Occupied;
                        }
                    }

                    if (!routeCleared)
                    {
                        routeState = SignalBlockState.Jn_Obstructed;
                    }
                }
            }

            return (routeState);
        }

        //================================================================================================//
        /// <summary>
        /// LockForTrain
        /// Add a lock for a train and a specific subpath (default 0).  This allow the control of this signal by a specific action
        /// </summary>

        public bool LockForTrain(int trainNumber, int subpath = 0)
        {
            KeyValuePair<int, int> newLock = new KeyValuePair<int, int>(trainNumber, subpath);
            lockedTrains.Add(newLock);
            return false;
        }

        public bool UnlockForTrain(int trainNumber, int subpath = 0)
        {
            bool info = lockedTrains.Remove(lockedTrains.First(item => item.Key.Equals(trainNumber) && item.Value.Equals(subpath)));
            return info;
        }

        public bool HasLockForTrain(int trainNumber, int subpath = 0)
        {
            bool info = (lockedTrains.Count > 0 && lockedTrains.Exists(item => item.Key.Equals(trainNumber) && item.Value.Equals(subpath)));
            return info;
        }

        public bool CleanAllLock(int trainNumber)
        {
            int info = lockedTrains.RemoveAll(item => item.Key.Equals(trainNumber));
            if (info > 0)
                return true;
            return false;
        }

        //================================================================================================//
        /// <summary>
        /// HasHead
        ///
        /// Returns 1 if signal has optional head set, 0 if not
        /// </summary>

        public int HasHead(int requiredHeadIndex)
        {
            if (WorldObject == null || WorldObject.HeadsSet == null)
            {
                Trace.TraceInformation("Signal {0} (TDB {1}) has no heads", Index, SignalHeads[0].TDBIndex);
                return (0);
            }
            return ((requiredHeadIndex < WorldObject.HeadsSet.Length) ? (WorldObject.HeadsSet[requiredHeadIndex] ? 1 : 0) : 0);
        }

        //================================================================================================//
        /// <summary>
        /// IncreaseSignalNumClearAhead
        ///
        /// Increase SignalNumClearAhead from its default value with the value as passed
        /// <summary>

        public void IncreaseSignalNumClearAhead(int requiredIncreaseValue)
        {
            if (SignalNumClearAhead_ORTS > -2)
            {
                SignalNumClearAheadActive = SignalNumClearAhead_ORTS + requiredIncreaseValue;
            }
        }

        //================================================================================================//
        /// <summary>
        /// DecreaseSignalNumClearAhead
        ///
        /// Decrease SignalNumClearAhead from its default value with the value as passed
        /// </summary>

        public void DecreaseSignalNumClearAhead(int requiredDecreaseValue)
        {
            if (SignalNumClearAhead_ORTS > -2)
            {
                SignalNumClearAheadActive = SignalNumClearAhead_ORTS - requiredDecreaseValue;
            }
        }

        //================================================================================================//
        /// <summary>
        /// SetSignalNumClearAhead
        ///
        /// Set SignalNumClearAhead to the value as passed
        /// <summary>

        public void SetSignalNumClearAhead(int requiredValue)
        {
            if (SignalNumClearAhead_ORTS > -2)
            {
                SignalNumClearAheadActive = requiredValue;
            }
        }

        //================================================================================================//
        /// <summary>
        /// ResetSignalNumClearAhead
        ///
        /// Reset SignalNumClearAhead to the default value
        /// </summary>

        public void ResetSignalNumClearAhead()
        {
            if (SignalNumClearAhead_ORTS > -2)
            {
                SignalNumClearAheadActive = SignalNumClearAhead_ORTS;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set HOLD state for dispatcher control
        ///
        /// Parameter : bool, if set signal must be reset if set (and train position allows)
        ///
        /// Returned : bool[], dimension 2,
        ///            field [0] : if true, hold state is set
        ///            field [1] : if true, signal is reset (always returns false if reset not requested)
        /// </summary>

        public bool[] requestHoldSignalDispatcher(bool requestResetSignal)
        {
            bool[] returnValue = new bool[2] { false, false };
            SignalAspectState thisAspect = this_sig_lr(SignalFunction.Normal);

            // signal not enabled - set lock, reset if cleared (auto signal can clear without enabling)

            if (EnabledTrain == null || EnabledTrain.Train == null)
            {
                HoldState = SignalHoldState.ManualLock;
                if (thisAspect > SignalAspectState.Stop) ResetSignal(true);
                returnValue[0] = true;
            }

            // if enabled, cleared and reset not requested : no action

            else if (!requestResetSignal && thisAspect > SignalAspectState.Stop)
            {
                HoldState = SignalHoldState.ManualLock; //just in case this one later will be set to green by the system
                returnValue[0] = true;
            }

            // if enabled and not cleared : set hold, no reset required

            else if (thisAspect == SignalAspectState.Stop)
            {
                HoldState = SignalHoldState.ManualLock;
                returnValue[0] = true;
            }

            // enabled, cleared , reset required : check train speed
            // if train is moving : no action
            //temporarily removed by JTang, before the full revision is ready
            //          else if (Math.Abs(enabledTrain.Train.SpeedMpS) > 0.1f)
            //          {
            //          }

            // if train is stopped : reset signal, breakdown train route, set holdstate

            else
            {
                int signalRouteIndex = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].GetRouteIndex(TrackCircuitNextIndex, 0);
                if (signalRouteIndex >= 0)
                {
                    SignalEnvironment.BreakDownRouteList(EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex], signalRouteIndex, EnabledTrain);
                    ResetSignal(true);
                    HoldState = SignalHoldState.ManualLock;
                    returnValue[0] = true;
                    returnValue[1] = true;
                }
                else //hopefully this does not happen
                {
                    HoldState = SignalHoldState.ManualLock;
                    returnValue[0] = true;
                }
            }

            return (returnValue);
        }

        //================================================================================================//
        /// <summary>
        /// Reset HOLD state for dispatcher control
        /// </summary>

        public void clearHoldSignalDispatcher()
        {
            HoldState = SignalHoldState.None;
        }

        //================================================================================================//
        /// <summary>
        /// Count number of normal signal heads
        /// </summary>
        public void SetNumberSignalHeads()
        {
            signalNumberNormalHeads = SignalHeads.Where(head => head.SignalFunction == SignalFunction.Normal).Count();
        }

        //================================================================================================//
        /// <summary>
        /// Set trackcircuit cross reference for signal items and speedposts
        /// </summary>
        internal static void SetSignalCrossReference(TrackCircuitSection section)
        {
            // process end signals
            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                Signal signal = section.EndSignals[direction];
                if (signal != null)
                {
                    signal.TrackCircuitIndex = section.Index;
                    signal.TrackCicruitOffset = section.Length;
                    signal.TrackCircuitDirection = direction;

                    signal.TrackCircuitNextIndex = section.Pins[direction, Location.NearEnd].Link;
                    signal.TrackCircuitNextDirection = section.Pins[direction, Location.NearEnd].Direction;
                }
            }

            // process other signals - only set info if not already set
            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                for (int i = 0; i < SignalEnvironment.OrtsSignalTypeCount; i++)
                {
                    foreach (TrackCircuitSignalItem signalItem in section.CircuitItems.TrackCircuitSignals[direction][i])
                    {
                        Signal signal = signalItem.Signal;

                        if (signal.TrackCircuitIndex <= 0)
                        {
                            signal.TrackCircuitIndex = section.Index;
                            signal.TrackCicruitOffset = signalItem.SignalLocation;
                            signal.TrackCircuitDirection = direction;
                        }
                    }
                }
            }

            // process speedposts
            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                TrackCircuitSignalList thisList = section.CircuitItems.TrackCircuitSpeedPosts[direction];
                foreach (TrackCircuitSignalItem thisItem in thisList)
                {
                    Signal thisSignal = thisItem.Signal;

                    if (thisSignal.TrackCircuitIndex <= 0)
                    {
                        thisSignal.TrackCircuitIndex = section.Index;
                        thisSignal.TrackCicruitOffset = thisItem.SignalLocation;
                        thisSignal.TrackCircuitDirection = direction;
                    }
                }
            }

            // process mileposts
            foreach (TrackCircuitMilepost trackCircuitMilepost in section.CircuitItems.TrackCircuitMileposts)
            {
                if (trackCircuitMilepost.Milepost.TrackCircuitReference <= 0)
                {
                    trackCircuitMilepost.Milepost.SetCircuit(section.Index, trackCircuitMilepost.MilepostLocation[Location.NearEnd]);
                }
            }
        }

        internal void ValidateSignal()
        {
            if (SignalNormal())
            {
                if (TrackCircuitNextIndex < 0)
                {
                    Trace.TraceInformation($"Signal {Index}; TC : {TrackCircuitIndex}; NextTC : {TrackCircuitNextIndex}; TN : {TrackNode }; TDB (0) : {SignalHeads[0].TDBIndex}");
                }

                if (TrackCircuitIndex < 0) // signal is not on any track - remove it!
                {
                    Trace.TraceInformation($"Signal removed {Index}; TC : {TrackCircuitIndex}; NextTC : {TrackCircuitNextIndex}; TN : {TrackNode}; TDB (0) : {SignalHeads[0].TDBIndex}");
                    SignalEnvironment.SignalObjects[Index] = null;
                }
            }

        }
    }  // SignalObject

}
