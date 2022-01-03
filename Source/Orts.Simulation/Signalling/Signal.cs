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
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Simulation.AIs;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.Physics;
using Orts.Simulation.Track;

namespace Orts.Simulation.Signalling
{
    /// <summary>
    ///  class SignalObject
    /// </summary>
    public class Signal : ISignal
    {
        private static TrackNodes trackNodes;
        private static List<TrackItem> trackItems;

        private static SignalEnvironment signalEnvironment; //back reference to the signal environment

        internal SignalWorldInfo WorldObject { get; set; }                // Signal World Object information
        internal SpeedPostWorldObject SpeedPostWorldObject { get; set; } // Speed Post World Object information

        private int? nextSwitchIndex;                       // index of first switch in path

        private readonly List<int> junctionsPassed = new List<int>();  // Junctions which are passed checking next signal //

        private int signalNumberNormalHeads;                // no. of normal signal heads in signal
        private int requestedNumClearAhead;                 // Passed on value for SignalNumClearAhead

        private readonly Dictionary<int, int> localStorage = new Dictionary<int, int>();  // list to store local script variables

        private InternalBlockstate internalBlockState = InternalBlockstate.Open;    // internal blockstate

        private int requestingNormalSignal = -1;            // ref of normal signal requesting route clearing (only used for signals without NORMAL heads)
        private readonly int[] defaultNextSignal;           // default next signal
        private readonly TrackCircuitPartialPathRoute fixedRoute = new TrackCircuitPartialPathRoute();     // fixed route from signal
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

        internal int SignalNumClearAheadMsts { get; set; } = -2;    // Overall maximum SignalNumClearAhead over all heads (MSTS calculation)
        internal int SignalNumClearAheadOrts { get; set; } = -2;    // Overall maximum SignalNumClearAhead over all heads (ORTS calculation)
        internal int SignalNumClearAheadActive { get; set; } = -2;   // Active SignalNumClearAhead (for ORST calculation only, as set by script)

        internal bool Static { get; set; }                  // set if signal does not required updates (fixed signals)

        public int Index { get; private set; }              // This signal's reference.
        public TrackDirection TrackDirection { get; internal set; }  // Direction facing on track

        public int TrackNode { get; internal set; }         // Track node which contains this signal
        public int TrackItemRefIndex { get; internal set; } // Index to TrItemRef within Track Node 
        public bool ForcePropagation { get; set; }          // Force propagation (used in case of signals at very short distance)
        public bool FixedRoute { get; private set; }        // signal has fixed route
        public Traveller TdbTraveller { get; }              // TDB traveller to determine distance between objects
        public TrackCircuitPartialPathRoute SignalRoute { get; internal set; } = new TrackCircuitPartialPathRoute();  // train route from signal
        public int TrainRouteIndex { get; private set; }    // index of section after signal in train route list
        public Train.TrainRouted EnabledTrain { get; internal set; } // full train structure for which signal is enabled
#pragma warning disable CA1002 // Do not expose generic lists
        public List<int> Signalfound { get; }              // active next signal - used for signals with NORMAL heads only     //TODO 20201126 convert to EnumArray on SignalFunction enum
#pragma warning restore CA1002 // Do not expose generic lists
        public SignalPermission OverridePermission { get; set; } = SignalPermission.Denied;  // Permission to pass red signal
        public SignalHoldState HoldState { get; set; } = SignalHoldState.None;
        public bool CallOnManuallyAllowed { get; set; }

        public SignalCategory SignalType
        {
            get
            {
                return SignalHeads.Exists(x => x.SignalFunction != SignalFunction.Speed)
                    ? SignalCategory.Signal
                    : SpeedPostWorldObject != null ? SignalCategory.SpeedPost : SignalCategory.SpeedSignal;
            }
        }

        public List<SignalHead> SignalHeads { get; } = new List<SignalHead>();
        public int TrackCircuitIndex { get; private set; } = -1;        // Reference to TrackCircuit (index)
        public float TrackCircuitOffset { get; private set; }           // Position within TrackCircuit
        public TrackDirection TrackCircuitDirection { get; private set; }   // Direction within TrackCircuit
        public int TrackCircuitNextIndex { get; private set; } = -1;    // Index of next TrackCircuit (NORMAL signals only)
        public TrackDirection TrackCircuitNextDirection { get; private set; } // Direction of next TrackCircuit 

        public bool CallOnEnabled { get; internal set; }      // set if signal script file uses CallOn functionality

        internal static void Initialize(SignalEnvironment signals, TrackNodes trackNodes, List<TrackItem> trackItems)
        {
            signalEnvironment = signals;               // reference to overlaying Signal class
            Signal.trackNodes = trackNodes;
            Signal.trackItems = trackItems;
        }

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
            TrackCircuitOffset = source.TrackCircuitOffset;
            TrackCircuitDirection = source.TrackCircuitDirection;
            TrackCircuitNextIndex = source.TrackCircuitNextIndex;
            TrackCircuitNextDirection = source.TrackCircuitNextDirection;

            TrackDirection = source.TrackDirection;
            SignalNumClearAheadMsts = source.SignalNumClearAheadMsts;
            SignalNumClearAheadOrts = source.SignalNumClearAheadOrts;
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
            foreach (SignalHead head in SignalHeads)
            {
                head.ResetMain(this);
                switch (trackItems[head.TDBIndex])
                {
                    case SignalItem signalItem:
                        signalItem.SignalObject = Index;
                        break;
                    case SpeedPostItem speedItem:
                        speedItem.SignalObject = Index;
                        break;
                }
            }
        }

        /// <summary>
        /// Constructor for restore
        /// IMPORTANT : enabled train is restore temporarily as Trains are restored later as Signals
        /// Full restore of train link follows in RestoreTrains
        /// </summary>
        internal void Restore(BinaryReader inf)
        {
            int trainNumber = inf.ReadInt32();

            int sigfoundLength = inf.ReadInt32();
            for (int iSig = 0; iSig < sigfoundLength; iSig++)
            {
                Signalfound[iSig] = inf.ReadInt32();
            }

            bool validRoute = inf.ReadBoolean();

            if (validRoute)
            {
                SignalRoute = new TrackCircuitPartialPathRoute(inf);
            }

            TrainRouteIndex = inf.ReadInt32();
            HoldState = (SignalHoldState)inf.ReadInt32();

            int totalJnPassed = inf.ReadInt32();

            for (int iJn = 0; iJn < totalJnPassed; iJn++)
            {
                int thisJunction = inf.ReadInt32();
                junctionsPassed.Add(thisJunction);
                TrackCircuitSection.TrackCircuitList[thisJunction].SignalsPassingRoutes.Add(Index);
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
                Train thisTrain = new Train(trainNumber);
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
                    EnabledTrain = foundTrain.RoutedForward;
                    foundTrain.NextSignalObject[0] = this;
                }
                // check if this signal is next signal backward for this train
                else if (Index == foundTrain?.NextSignalObject[1]?.Index)
                {
                    EnabledTrain = foundTrain.RoutedBackward;
                    foundTrain.NextSignalObject[1] = this;
                }
                else
                {
                    // check if this section is reserved for this train

                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[TrackCircuitIndex];
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
                    CheckRouteState(false, SignalRoute, EnabledTrain);
                    PropagateRequest();
                    StateUpdate();
                }
                else
                {
                    GetNonRoutedBlockState();
                    StateUpdate();
                }
            }
        }

        /// <summary>
        /// Save
        /// </summary>
        internal void Save(BinaryWriter outf)
        {
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

        /// <summary>
        /// return blockstate
        /// </summary>
        public SignalBlockState BlockState()
        {
            return internalBlockState switch
            {
                InternalBlockstate.Reserved or InternalBlockstate.Reservable => SignalBlockState.Clear,
                InternalBlockstate.OccupiedSameDirection => SignalBlockState.Occupied,
                _ => SignalBlockState.Jn_Obstructed,
            };
        }

        public bool Enabled
        {
            get
            {
                if (MultiPlayerManager.IsMultiPlayer() && MultiPlayerManager.Instance().PreferGreen)
                    return true;
                return EnabledTrain != null;
            }
        }

        public int TrackItemIndex => (trackNodes[TrackNode] as TrackVectorNode).TrackItemIndices[TrackItemRefIndex];

        SignalState ISignal.State { get => State; set => State = value; }

        bool ISignal.CallOnEnabled => CallOnEnabled && CallOnManuallyAllowed;

        protected SignalState State
        {
            get
            {
                SignalState result = SignalState.Lock;
                foreach (var head in SignalHeads)
                {
                    if (head.SignalIndicationState == SignalAspectState.Clear_1 ||
                        head.SignalIndicationState == SignalAspectState.Clear_2)
                    {
                        result = SignalState.Clear;
                    }
                    if (head.SignalIndicationState == SignalAspectState.Approach_1 ||
                        head.SignalIndicationState == SignalAspectState.Approach_2 || head.SignalIndicationState == SignalAspectState.Approach_3)
                    {
                        result = SignalState.Approach;
                    }
                }
                return result;
            }
            set
            {
                switch (value)
                {
                    case SignalState.Clear:
                        DispatcherClearHoldSignal();
                        break;
                    case SignalState.Lock:
                        DispatcherRequestHoldSignal(true);
                        break;
                    case SignalState.Approach:
                        RequestApproachAspect();
                        break;
                    case SignalState.Manual:
                        RequestLeastRestrictiveAspect();
                        break;
                    case SignalState.CallOn:
                        SetManualCallOn(true);
                        break;
                }
            }
        }

        /// <summary>
        /// set default next signal based on non-Junction tracks ahead
        /// this routine also sets fixed routes for signals which do not lead onto junction or crossover
        /// </summary>
        public void SetSignalDefaultNextSignal()
        {
            int trackCircuitReference = TrackCircuitIndex;
            float position = TrackCircuitOffset;
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
                trackCircuitSection = TrackCircuitSection.TrackCircuitList[trackCircuitReference];
            }

            // set default
            for (int i = 0; i < defaultNextSignal.Length; i++)
            {
                defaultNextSignal[i] = -1;
            }

            // loop through valid sections
            while (trackCircuitSection != null && trackCircuitSection.CircuitType == TrackCircuitType.Normal)
            {
                if (!completedFixedRoute)
                {
                    fixedRoute.Add(new TrackCircuitRouteElement(trackCircuitSection.Index, direction));
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
                for (int i = (int)SignalFunction.Normal.Next(); i < signalEnvironment.OrtsSignalTypeCount; i++)
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
                trackCircuitSection = TrackCircuitSection.TrackCircuitList[trackCircuitSection.Pins[currentDirection, Location.NearEnd].Link];
            }

            // copy default as valid items
            for (int i = 0; i < signalEnvironment.OrtsSignalTypeCount; i++)
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

        /// <summary>
        /// Returns true if at least one signal head is type normal.
        /// </summary>
        public bool SignalNormal()
        {
            return SignalHeads.Any(head => head.SignalFunction == SignalFunction.Normal);
        }

        /// <summary>
        /// Returns true if at least one signal head is of required type
        /// </summary>
        public bool OrtsSignalType(int requestedSignalFunction)
        {
            return SignalHeads.Any(head => head.OrtsSignalFunctionIndex == requestedSignalFunction);
        }

        /// <summary>
        /// returns most restrictive state of next signal of required type
        /// </summary>
        public SignalAspectState NextSignalMR(int signalType)
        {
            int nextSignal = Signalfound[signalType];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(signalType);
                Signalfound[signalType] = nextSignal;
            }

            return nextSignal >= 0 ? signalEnvironment.Signals[nextSignal].SignalMRLimited(signalType) : SignalAspectState.Stop;
        }

        /// <summary>
        /// returns least restrictive state of next signal of required type
        /// </summary>
        public SignalAspectState NextSignalLR(int signalType)
        {
            int nextSignal = Signalfound[signalType];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(signalType);
                Signalfound[signalType] = nextSignal;
            }
            return nextSignal >= 0 ? signalEnvironment.Signals[nextSignal].SignalLRLimited(signalType) : SignalAspectState.Stop;
        }

        /// <summary>
        /// returns least restrictive state of next signal of required type of the nth signal ahead
        /// </summary>
        public SignalAspectState NextNthSignalLR(int signalType, int number)
        {
            int foundsignal = 0;
            SignalAspectState foundAspect = SignalAspectState.Clear_2;
            Signal nextSignalObject = this;

            while (foundsignal < number && foundAspect != SignalAspectState.Stop)
            {
                // use sigfound
                int nextSignal = nextSignalObject.Signalfound[signalType];

                // sigfound not set, try direct search
                if (nextSignal < 0)
                {
                    nextSignal = SONextSignal(signalType);
                    nextSignalObject.Signalfound[signalType] = nextSignal;
                }

                // signal found : get state
                if (nextSignal >= 0)
                {
                    foundsignal++;

                    nextSignalObject = signalEnvironment.Signals[nextSignal];
                    foundAspect = nextSignalObject.SignalLRLimited(signalType);

                    // reached required signal or state is stop : return
                    if (foundsignal >= number || foundAspect == SignalAspectState.Stop)
                    {
                        return foundAspect;
                    }
                }
                // signal not found : return stop
                else
                {
                    return SignalAspectState.Stop;
                }
            }
            return SignalAspectState.Stop; // emergency exit - loop should normally have exited on return
        }

        /// <summary>
        /// opp_sig_mr
        /// </summary>
        public SignalAspectState OppositeSignalMR(int signalType)
        {
            int signalFound = SONextSignalOpposite(signalType);
            return signalFound >= 0 ? signalEnvironment.Signals[signalFound].SignalMRLimited(signalType) : SignalAspectState.Stop;
        }

        /// <summary>
        /// opp_sig_lr
        /// </summary>
        public SignalAspectState OppositeSignalLR(int signalType)
        {
            int signalFound = SONextSignalOpposite(signalType);
            return signalFound >= 0 ? signalEnvironment.Signals[signalFound].SignalLRLimited(signalType) : SignalAspectState.Stop;
        }

        /// <summary>
        /// Returns the most restrictive state of this signal's heads of required type
        /// standard version, returning Stop when Unknown is found only
        /// </summary>
        public SignalAspectState SignalMRLimited(int signalType)
        {
            SignalAspectState result = SignalMR(signalType);
            return result != SignalAspectState.Unknown ? result : SignalAspectState.Stop;
        }

        /// <summary>
        /// standard version without state return using MSTS type parameter
        /// </summary>
        public SignalAspectState SignalMR(SignalFunction signalType)
        {
            return SignalMRLimited((int)signalType);
        }

        /// <summary>
        /// Returns the most restrictive state of this signal's heads of required type
        /// standard version, returning Unknown when not found
        /// </summary>
        public SignalAspectState SignalMR(int signalType)
        {
            SignalAspectState sigAsp = SignalAspectState.Unknown;
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.OrtsSignalFunctionIndex == signalType && sigHead.SignalIndicationState < sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                }
            }
            return sigAsp;
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
        /// standard version, returning Stop when Unknown is found only
        /// </summary>
        public SignalAspectState SignalLRLimited(int signalType)
        {
            SignalAspectState result = SignalLR(signalType);
            return result != SignalAspectState.Unknown ? result : SignalAspectState.Stop;
        }

        /// <summary>
        /// standard version without state return using MSTS type parameter
        /// </summary>
        public SignalAspectState SignalLR(SignalFunction signalType)
        {
            return SignalLRLimited((int)signalType);
        }

        /// <summary>
        /// additional version with state return
        /// </summary>
        public SignalAspectState SignalLR(int signalType)
        {
            SignalAspectState sigAsp = SignalAspectState.Stop;
            bool sigAspSet = false;
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.OrtsSignalFunctionIndex == signalType && sigHead.SignalIndicationState >= sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                    sigAspSet = true;
                }
            }
            return sigAspSet ? sigAsp : signalType == (int)SignalFunction.Normal ? SignalAspectState.Clear_2 : SignalAspectState.Unknown;
        }//this_sig_lr

        /// <summary>
        /// Returns the speed related to the least restrictive aspect (for normal signal)
        /// </summary>
        internal SpeedInfo SignalSpeed(SignalFunction signalType)
        {
            SpeedInfo set_speed = new SpeedInfo(null);

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == signalType && sigHead.SignalIndicationState >= SignalAspectState.Stop)
                {
                    set_speed = sigHead.SpeedInfoSet[sigHead.SignalIndicationState];
                }
            }
            return set_speed;
        }//this_sig_speed

        /// <summary>
        /// returns ident of next signal of required type
        /// </summary>
        public int NextSignalId(int signalType)
        {
            int nextSignal = Signalfound[signalType];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(signalType);
                Signalfound[signalType] = nextSignal;
            }

            if (nextSignal >= 0)
            {
                if (signalType != (int)SignalFunction.Normal)
                {
                    Signal foundSignalObject = signalEnvironment.Signals[nextSignal];
                    if (SignalNormal())
                    {
                        foundSignalObject.requestingNormalSignal = Index;
                    }
                    else
                    {
                        foundSignalObject.requestingNormalSignal = requestingNormalSignal;
                    }
                }

                return nextSignal;
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// returns ident of next signal of required type
        /// </summary>

        public int NextNthSignalId(int signalType, int nsignal)
        {
            int nextSignal = Index;
            int foundsignal = 0;
            Signal nextSignalObject = this;

            while (foundsignal < nsignal && nextSignal >= 0)
            {
                // use sigfound
                nextSignal = nextSignalObject.Signalfound[signalType];

                // sigfound not set, try direct search
                if (nextSignal < 0)
                {
                    nextSignal = nextSignalObject.SONextSignal(signalType);
                    nextSignalObject.Signalfound[signalType] = nextSignal;
                }

                // signal found
                if (nextSignal >= 0)
                {
                    foundsignal++;
                    nextSignalObject = signalEnvironment.Signals[nextSignal];
                }
            }

            if (nextSignal >= 0 && foundsignal > 0)
            {
                return nextSignal;
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// returns ident of next opposite signal of required type
        /// </summary>
        public int OppositeSignalId(int signalType)
        {
            return (SONextSignalOpposite(signalType));
        }

        /// <summary>
        /// Returns the setting if speed must be reduced on RESTRICTED or STOP_AND_PROCEED
        /// returns TRUE if speed reduction must be suppressed
        /// </summary>
        public bool SignalNoSpeedReduction(SignalFunction signalType)
        {
            SignalAspectState sigAsp = SignalAspectState.Stop;
            bool setNoReduction = false;

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == signalType && sigHead.SignalIndicationState >= sigAsp)
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
        }

        /// <summary>
        /// isRestrictedSpeedPost : Returns TRUE if it is a restricted (temp) speedpost
        /// </summary>
        public int SpeedPostType()
        {
            SignalAspectState sigAsp = SignalAspectState.Clear_2;
            int speedPostType = 0; // default = standard speedpost

            SignalHead sigHead = SignalHeads.First();

            if (sigHead.SpeedInfoSet?[sigAsp] != null)
            {
                speedPostType = sigHead.SpeedInfoSet[sigAsp].LimitedSpeedReduction;

            }
            return speedPostType;
        }

        /// <summary>
        /// Returns the lowest allowed speed (for speedpost and speed signal)
        /// </summary>
        internal SpeedInfo SpeedLimit(SignalFunction signalType)
        {
            SpeedInfo setSpeed = new SpeedInfo(9E9f, 9E9f, false, false, 0, false);

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == signalType)
                {
                    SpeedInfo speed = sigHead.SpeedInfoSet[sigHead.SignalIndicationState];
                    if (speed != null && !speed.SpeedWarning)
                    {
                        if (speed.PassengerSpeed > 0 && speed.PassengerSpeed < setSpeed.PassengerSpeed)
                        {
                            setSpeed.PassengerSpeed = speed.PassengerSpeed;
                            setSpeed.Flag = false;
                            setSpeed.Reset = false;
                            if (SignalType != SignalCategory.Signal)
                                setSpeed.LimitedSpeedReduction = speed.LimitedSpeedReduction;
                        }

                        if (speed.FreightSpeed > 0 && speed.FreightSpeed < setSpeed.FreightSpeed)
                        {
                            setSpeed.FreightSpeed = speed.FreightSpeed;
                            setSpeed.Flag = false;
                            setSpeed.Reset = false;
                            if (SignalType != SignalCategory.Signal)
                                setSpeed.LimitedSpeedReduction = speed.LimitedSpeedReduction;
                        }
                    }

                }
            }

            if (setSpeed.PassengerSpeed > 1E9f)
                setSpeed.PassengerSpeed = -1;
            if (setSpeed.FreightSpeed > 1E9f)
                setSpeed.FreightSpeed = -1;

            return setSpeed;
        }

        /// <summary>
        /// store local variable
        /// </summary>
        public void StoreLocalVariable(int index, int value)
        {
            localStorage[index] = value;
        }

        /// <summary>
        /// retrieve variable from this signal
        /// </summary>
        public int SignalLocalVariable(int index)
        {
            return localStorage.TryGetValue(index, out int result) ? result : 0;
        }

        /// <summary>
        /// retrieve variable from next signal
        /// </summary>
        public int NextSignalLocalVariable(int signalType, int index)
        {
            int nextSignal = Signalfound[signalType];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(signalType);
                Signalfound[signalType] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                Signal nextSignalObject = signalEnvironment.Signals[nextSignal];
                if (nextSignalObject.localStorage.ContainsKey(index))
                {
                    return nextSignalObject.localStorage[index];
                }
            }
            return 0;
        }

        /// <summary>
        /// check if next signal has normal head with required subtype
        /// </summary>
        public int NextSignalHasNormalSubtype(int subtype)
        {
            int nextSignal = Signalfound[(int)SignalFunction.Normal];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal((int)SignalFunction.Normal);
                Signalfound[(int)SignalFunction.Normal] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                Signal nextSignalObject = signalEnvironment.Signals[nextSignal];
                return (nextSignalObject.SignalHasNormalSubtype(subtype));
            }
            return 0;
        }

        /// <summary>
        /// check if this signal has normal head with required subtype
        /// </summary>
        public int SignalHasNormalSubtype(int subtype)
        {
            foreach (SignalHead head in SignalHeads)
            {
                if (head.SignalFunction == SignalFunction.Normal && head.OrtsNormalSubtypeIndex == subtype)
                {
                    return 1;
                }
            }
            return 0;
        }

        /// <summary>
        /// switchstand : link signal with next switch and set aspect according to switch state
        /// </summary>
        public int Switchstand(int aspect1, int aspect2)
        {
            // if switch index not yet set, find first switch in path
            if (!nextSwitchIndex.HasValue)
            {
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[TrackCircuitIndex];
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
                        thisSection = TrackCircuitSection.TrackCircuitList[thisSection.Pins[pinIndex, Location.NearEnd].Link];
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
                TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[nextSwitchIndex.Value];
                return switchSection.JunctionLastRoute == 0 ? aspect1 : aspect2;
            }

            return aspect1;
        }

        /// <summary>
        /// check if required route is set
        /// </summary>
        public bool CheckRouteSet(int mainNode, int junctionNode)
        {
            bool routeset = false;
            bool retry = false;

            // if signal is enabled for a train, check if required section is in train route path
            if (EnabledTrain != null && !MultiPlayerManager.IsMultiPlayer())
            {
                TrackCircuitPartialPathRoute routePart = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex];

                TrackNode node = trackNodes[mainNode];
                if (routePart != null)
                {
                    for (int i = 0; i <= node.TrackCircuitCrossReferences.Count - 1 && !routeset; i++)
                    {
                        int sectionIndex = node.TrackCircuitCrossReferences[i].Index;

                        for (int j = 0; j < routePart.Count && !routeset; j++)
                        {
                            routeset = (sectionIndex == routePart[j].TrackCircuitSection.Index && TrackCircuitSection.TrackCircuitList[sectionIndex].CircuitType == TrackCircuitType.Normal);
                        }
                    }
                }

                // if not found in trainroute, try signalroute

                if (!routeset && SignalRoute != null)
                {
                    for (int i = 0; i <= SignalRoute.Count - 1 && !routeset; i++)
                    {
                        TrackCircuitSection section = SignalRoute[i].TrackCircuitSection;
                        routeset = (section.OriginalIndex == mainNode && section.CircuitType == TrackCircuitType.Normal);
                    }
                }
                retry = !routeset;
            }

            // not enabled, follow set route but only if not normal signal (normal signal will not clear if not enabled)
            // also, for normal enabled signals - try and follow pins (required node may be beyond present route)
            if (retry || !SignalNormal() || MultiPlayerManager.IsMultiPlayer())
            {
                TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[TrackCircuitIndex];
                TrackDirection direction = TrackCircuitDirection;
                TrackDirection newDirection;
                bool passedTrackJn = false;

                List<int> passedSections = new List<int>
                {
                    section.Index
                };

                routeset = mainNode == section.OriginalIndex;
                while (!routeset && section != null)
                {
                    int sectionIndex;
                    if (section.ActivePins[direction, Location.NearEnd].Link >= 0)
                    {
                        newDirection = section.ActivePins[direction, Location.NearEnd].Direction;
                        sectionIndex = section.ActivePins[direction, Location.NearEnd].Link;
                    }
                    else
                    {
                        newDirection = section.ActivePins[direction, Location.FarEnd].Direction;
                        sectionIndex = section.ActivePins[direction, Location.FarEnd].Link;
                    }

                    // if Junction, if active pins not set use selected route
                    if (sectionIndex < 0 && section.CircuitType == TrackCircuitType.Junction)
                    {
                        // check if this is required junction
                        if (section.Index == junctionNode)
                        {
                            passedTrackJn = true;
                        }
                        // break if passed required junction
                        else if (passedTrackJn)
                        {
                            break;
                        }

                        if (section.ActivePins[TrackDirection.Reverse, Location.NearEnd].Link == -1 && section.ActivePins[TrackDirection.Reverse, Location.FarEnd].Link == -1)
                        {
                            Location selectedLocation = (Location)(trackNodes[section.OriginalIndex] as TrackJunctionNode).SelectedRoute;
                            newDirection = section.Pins[TrackDirection.Reverse, selectedLocation].Direction;
                            sectionIndex = section.Pins[TrackDirection.Reverse, selectedLocation].Link;
                        }
                    }

                    // if NORMAL, if active pins not set use default pins
                    if (sectionIndex < 0 && section.CircuitType == TrackCircuitType.Normal)
                    {
                        newDirection = section.Pins[direction, Location.NearEnd].Direction;
                        sectionIndex = section.Pins[direction, Location.NearEnd].Link;
                    }

                    // check for loop
                    if (passedSections.Contains(sectionIndex))
                    {
                        section = null;  // route is looped - exit
                    }
                    // next section
                    else if (sectionIndex >= 0)
                    {
                        passedSections.Add(sectionIndex);
                        section = TrackCircuitSection.TrackCircuitList[sectionIndex];
                        direction = newDirection;
                        routeset = (mainNode == section.OriginalIndex && section.CircuitType == TrackCircuitType.Normal);
                    }
                    // no next section
                    else
                    {
                        section = null;
                    }
                }
            }

            return routeset;
        }

        /// <summary>
        /// Find next signal of specified type along set sections - not for NORMAL signals
        /// </summary>
        public int SONextSignal(int signalType)
        {
            int trackCircuit = TrackCircuitIndex;
            TrackDirection direction = TrackCircuitDirection;
            int signalFound = -1;
            TrackCircuitSection section;
            bool sectionSet = false;

            // maximise fntype to length of available type list
            int reqtype = Math.Min(signalType, signalEnvironment.OrtsSignalTypeCount);

            // if searching for SPEED signal : check if enabled and use train to find next speedpost
            if (reqtype == (int)SignalFunction.Speed)
            {
                if (EnabledTrain != null)
                {
                    signalFound = SONextSignalSpeed();
                }
                else
                {
                    return -1;
                }
            }
            // for normal signals
            else if (reqtype == (int)SignalFunction.Normal)
            {
                if (SignalNormal())        // if this signal is normal : cannot be done using this route (set through sigfound variable)
                    return -1;
                signalFound = SONextSignalNormal(TrackCircuitIndex);   // other types of signals (sigfound not used)
            }
            // for other signals : move to next TC (signal would have been default if within same section)
            else
            {
                section = TrackCircuitSection.TrackCircuitList[trackCircuit];
                if (!SignalNormal())
                {
                    foreach (var item in section.CircuitItems.TrackCircuitSignals[direction][reqtype])
                    {
                        if (item.Signal.TrackCircuitOffset > TrackCircuitOffset)
                        {
                            signalFound = item.Signal.Index;
                            break;
                        }
                    }
                }
                sectionSet = EnabledTrain != null && section.IsSet(EnabledTrain, false);

                if (sectionSet)
                {
                    trackCircuit = section.ActivePins[direction, Location.NearEnd].Link;
                    direction = section.ActivePins[direction, Location.NearEnd].Direction;
                }
            }

            // loop through valid sections

            while (sectionSet && trackCircuit > 0 && signalFound < 0)
            {
                section = TrackCircuitSection.TrackCircuitList[trackCircuit];

                if (section.CircuitType == TrackCircuitType.Junction ||
                    section.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!junctionsPassed.Contains(trackCircuit))
                        junctionsPassed.Add(trackCircuit);  // set reference to junction section
                    if (!section.SignalsPassingRoutes.Contains(Index))
                        section.SignalsPassingRoutes.Add(Index);
                }

                // check if required type of signal is along this section
                TrackCircuitSignalList signalList = section.CircuitItems.TrackCircuitSignals[direction][reqtype];
                if (signalList.Count > 0)
                {
                    signalFound = signalList[0].Signal.Index;
                }

                // get next section if active link is set
                if (signalFound < 0)
                {
                    TrackDirection pinIndex = direction;
                    sectionSet = section.IsSet(EnabledTrain, false);
                    if (sectionSet)
                    {
                        trackCircuit = section.ActivePins[pinIndex, Location.NearEnd].Link;
                        direction = section.ActivePins[pinIndex, Location.NearEnd].Direction;
                        if (trackCircuit == -1)
                        {
                            trackCircuit = section.ActivePins[pinIndex, Location.FarEnd].Link;
                            direction = section.ActivePins[pinIndex, Location.FarEnd].Direction;
                        }
                    }
                }
            }

            // if signal not found following switches use signal route
            if (signalFound < 0 && SignalRoute != null && SignalRoute.Count > 0)
            {
                for (int i = 0; i <= (SignalRoute.Count - 1) && signalFound < 0; i++)
                {
                    section = SignalRoute[i].TrackCircuitSection;
                    direction = SignalRoute[i].Direction;
                    TrackCircuitSignalList signalList = section.CircuitItems.TrackCircuitSignals[direction][signalType];
                    if (signalList.Count > 0)
                    {
                        signalFound = signalList[0].Signal.Index;
                    }
                }
            }

            // if signal not found, use route from requesting normal signal
            if (signalFound < 0 && requestingNormalSignal >= 0)
            {
                Signal refSignal = signalEnvironment.Signals[requestingNormalSignal];
                if (refSignal.SignalRoute != null && refSignal.SignalRoute.Count > 0)
                {
                    int nextSectionIndex = refSignal.SignalRoute.GetRouteIndex(TrackCircuitIndex, 0);

                    if (nextSectionIndex >= 0)
                    {
                        for (int i = nextSectionIndex + 1; i <= (refSignal.SignalRoute.Count - 1) && signalFound < 0; i++)
                        {
                            section = refSignal.SignalRoute[i].TrackCircuitSection;
                            direction = refSignal.SignalRoute[i].Direction;
                            TrackCircuitSignalList signalList = section.CircuitItems.TrackCircuitSignals[direction][signalType];
                            if (signalList.Count > 0)
                            {
                                signalFound = signalList[0].Signal.Index;
                            }
                        }
                    }
                }
            }

            return signalFound;
        }

        /// <summary>
        /// Find next signal of specified type along set sections - for SPEED signals only
        /// </summary>
        private int SONextSignalSpeed()
        {
            int routeListIndex = EnabledTrain.Train.ValidRoute[0].GetRouteIndex(TrackCircuitIndex, EnabledTrain.Train.PresentPosition[Direction.Forward].RouteListIndex);

            // signal not in train's route
            if (routeListIndex < 0)
            {
                return -1;
            }

            // find next speed object
            TrackCircuitSignalItem foundItem = signalEnvironment.FindNextObjectInRoute(EnabledTrain.Train.ValidRoute[0], routeListIndex, TrackCircuitOffset, -1, SignalFunction.Speed, EnabledTrain);
            return foundItem.SignalState == SignalItemFindState.Item ? foundItem.Signal.Index : -1;
        }

        /// <summary>
        /// Find next signal of specified type along set sections - NORMAL signals ONLY
        /// </summary>
        private int SONextSignalNormal(int trackCircuit)
        {
            TrackDirection direction = TrackCircuitDirection;
            int signalFound = -1;
            TrackDirection pinIndex;

            TrackCircuitSection section;
            if (trackCircuit < 0)
            {
                trackCircuit = TrackCircuitIndex;
                section = TrackCircuitSection.TrackCircuitList[trackCircuit];
                pinIndex = direction;
                trackCircuit = section.ActivePins[pinIndex, Location.NearEnd].Link;
                direction = section.ActivePins[pinIndex, Location.NearEnd].Direction;
            }

            // loop through valid sections
            while (trackCircuit > 0 && signalFound < 0)
            {
                section = TrackCircuitSection.TrackCircuitList[trackCircuit];

                if (section.CircuitType == TrackCircuitType.Junction ||
                    section.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!junctionsPassed.Contains(trackCircuit))
                        junctionsPassed.Add(trackCircuit);  // set reference to junction section
                    if (!section.SignalsPassingRoutes.Contains(Index))
                        section.SignalsPassingRoutes.Add(Index);
                }

                // check if normal signal is along this section
                if (section.EndSignals[direction] != null)
                {
                    signalFound = section.EndSignals[direction].Index;
                }

                // get next section if active link is set
                if (signalFound < 0)
                {
                    pinIndex = direction;
                    trackCircuit = section.ActivePins[pinIndex, Location.NearEnd].Link;
                    direction = section.ActivePins[pinIndex, Location.NearEnd].Direction;
                    if (trackCircuit == -1)
                    {
                        trackCircuit = section.ActivePins[pinIndex, Location.FarEnd].Link;
                        direction = section.ActivePins[pinIndex, Location.FarEnd].Direction;
                    }

                    // if no active link but signal has route allocated, use train route to find next section
                    if (trackCircuit == -1 && SignalRoute != null)
                    {
                        int routeIndex = SignalRoute.GetRouteIndex(section.Index, 0);
                        if (routeIndex >= 0 && routeIndex <= SignalRoute.Count - 2)
                        {
                            trackCircuit = SignalRoute[routeIndex + 1].TrackCircuitSection.Index;
                            direction = SignalRoute[routeIndex + 1].Direction;
                        }
                    }
                }
            }

            return (signalFound);
        }

        /// <summary>
        /// Find next signal in opp direction
        /// </summary>
        public int SONextSignalOpposite(int signalType)
        {
            int trackCircuit = TrackCircuitIndex;
            TrackDirection direction = TrackCircuitDirection.Reverse();    // reverse direction
            int signalFound = -1;

            TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[trackCircuit];
            bool sectionSet = EnabledTrain != null && section.IsSet(EnabledTrain, false);

            // loop through valid sections

            while (sectionSet && trackCircuit > 0 && signalFound < 0)
            {
                section = TrackCircuitSection.TrackCircuitList[trackCircuit];

                if (section.CircuitType == TrackCircuitType.Junction || section.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!junctionsPassed.Contains(trackCircuit))
                        junctionsPassed.Add(trackCircuit);  // set reference to junction section
                    if (!section.SignalsPassingRoutes.Contains(Index))
                        section.SignalsPassingRoutes.Add(Index);
                }

                // check if required type of signal is along this section
                if (signalType == (int)SignalFunction.Normal)
                {
                    signalFound = section.EndSignals[direction] != null ? section.EndSignals[direction].Index : -1;
                }
                else
                {
                    TrackCircuitSignalList thisList = section.CircuitItems.TrackCircuitSignals[direction][signalType];
                    if (thisList.Count > 0)
                    {
                        signalFound = thisList[0].Signal.Index;
                    }
                }

                // get next section if active link is set
                if (signalFound < 0)
                {
                    TrackDirection pinIndex = direction;
                    sectionSet = section.IsSet(EnabledTrain, false);
                    if (sectionSet)
                    {
                        trackCircuit = section.ActivePins[pinIndex, Location.NearEnd].Link;
                        direction = section.ActivePins[pinIndex, Location.NearEnd].Direction;
                        if (trackCircuit == -1)
                        {
                            trackCircuit = section.ActivePins[pinIndex, Location.FarEnd].Link;
                            direction = section.ActivePins[pinIndex, Location.FarEnd].Direction;
                        }
                    }
                }
            }
            return signalFound;
        }

        /// <summary>
        /// Perform route check and state update
        /// </summary>
        public void Update()
        {
            if (Static)
                return;
            // perform route update for normal signals if enabled
            if (SignalNormal())
            {
                // if in hold, set to most restrictive for each head
                if (HoldState != SignalHoldState.None)
                {
                    foreach (SignalHead sigHead in SignalHeads)
                    {
                        switch (HoldState)
                        {
                            case SignalHoldState.ManualLock:
                            case SignalHoldState.StationStop:
                                sigHead.RequestMostRestrictiveAspect();
                                break;

                            case SignalHoldState.ManualApproach:
                                sigHead.RequestApproachAspect();
                                break;

                            case SignalHoldState.ManualPass:
                                sigHead.RequestLeastRestrictiveAspect();
                                break;
                        }
                    }
                }

                // if enabled - perform full update and propagate if not yet done
                if (EnabledTrain != null)
                {
                    // if internal state is not reserved (route fully claimed), perform route check
                    if (internalBlockState != InternalBlockstate.Reserved)
                    {
                        CheckRouteState(isPropagated, SignalRoute, EnabledTrain);
                    }

                    // propagate request
                    if (!isPropagated)
                    {
                        PropagateRequest();
                    }

                    StateUpdate();

                    // propagate request if not yet done
                    if (!propagated && EnabledTrain != null)
                    {
                        PropagateRequest();
                    }
                }
                // fixed route - check route and update
                else if (FixedRoute)
                {
                    // if internal state is not reserved (route fully claimed), perform route check

                    if (internalBlockState != InternalBlockstate.Reserved)
                    {
                        CheckRouteState(true, fixedRoute, null);
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
                GetNonRoutedBlockState();
                StateUpdate();
            }
        }

        /// <summary>
        /// fully reset signal as train has passed
        /// </summary>
        public void ResetSignalEnabled()
        {
            // reset train information
            EnabledTrain = null;
            CallOnManuallyAllowed = false;
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
            for (int i = 0; i < signalEnvironment.OrtsSignalTypeCount; i++)
            {
                Signalfound[i] = defaultNextSignal[i];
            }

            foreach (int sectionIndex in junctionsPassed)
            {
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[sectionIndex];
                thisSection.SignalsPassingRoutes.Remove(Index);
            }

            // reset permission //
            OverridePermission = SignalPermission.Denied;

            StateUpdate();
        }

        /// <summary>
        /// Perform the update for each head on this signal to determine state of signal.
        /// </summary>
        public void StateUpdate()
        {
            // reset approach control (must be explicitly reset as test in script may be conditional)
            approachControlSet = false;

            // update all normal heads first

            if ((MultiPlayerManager.MultiplayerState == MultiplayerState.Client) || //client won't handle signal update
                ((MultiPlayerManager.MultiplayerState == MultiplayerState.Dispatcher) && (HoldState == SignalHoldState.ManualApproach || HoldState == SignalHoldState.ManualLock || HoldState == SignalHoldState.ManualPass))) //if there were hold manually, will not update
                return;

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

        /// <summary>
        /// Returns the distance from the TDBtraveller to this signal. 
        /// </summary>
        public float DistanceTo(Traveller tdbTraveller)
        {
            if (null == tdbTraveller)
                throw new ArgumentNullException(nameof(tdbTraveller));
            int trItem = (trackNodes[TrackNode] as TrackVectorNode).TrackItemIndices[TrackItemRefIndex];
            return tdbTraveller.DistanceTo(trackItems[trItem].Location);
        }

        /// <summary>
        /// Returns the distance from this object to the next object
        /// </summary>
        public float ObjectDistance(Signal nextSignal)
        {
            if (null == nextSignal)
                throw new ArgumentNullException(nameof(nextSignal));

            int nextTrItem = (trackNodes[nextSignal.TrackNode] as TrackVectorNode).TrackItemIndices[nextSignal.TrackItemRefIndex];
            return TdbTraveller.DistanceTo(trackItems[nextTrItem].Location);
        }

        /// <summary>
        /// Check whether signal head is for this signal.
        /// </summary>
        public bool IsSignalHead(SignalItem signalItem)
        {
            if (null == signalItem)
                throw new ArgumentNullException(nameof(signalItem));
            // Tritem for this signal
            SignalItem currentSignalItem = (SignalItem)trackItems[TrackItemIndex];
            // Same Tile
            if (signalItem.Location.TileX == currentSignalItem.Location.TileX && signalItem.Location.TileZ == currentSignalItem.Location.TileZ)
            {
                // Same position
                if ((Math.Abs(signalItem.Location.Location.X - currentSignalItem.Location.Location.X) < 0.01) &&
                    (Math.Abs(signalItem.Location.Location.Y - currentSignalItem.Location.Location.Y) < 0.01) &&
                    (Math.Abs(signalItem.Location.Location.Z - currentSignalItem.Location.Location.Z) < 0.01))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds a head to this signal (for signam).
        /// </summary>
        public void AddHead(int trackItem, int tdbRef, SignalItem sigItem)
        {
            // create SignalHead
            SignalHead head = new SignalHead(this, trackItem, tdbRef, sigItem);

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
        }

        /// <summary>
        /// Adds a head to this signal (for speedpost).
        /// </summary>
        public void AddHead(int trItem, int TDBRef, SpeedPostItem speedItem)
        {
            // create SignalHead
            SignalHead head = new SignalHead(this, trItem, TDBRef, speedItem);
            SignalHeads.Add(head);

        }

        /// <summary>
        /// Sets the signal type from the sigcfg file for each signal head.
        /// </summary>
        internal void SetSignalType(SignalConfigurationFile signalConfig)
        {
            foreach (SignalHead signalHead in SignalHeads)
            {
                signalHead.SetSignalType(trackItems, signalConfig);
            }
        }

        public void Initialize()
        {
            foreach (SignalHead head in SignalHeads)
            {
                head.Initialize();
            }
        }

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


        /// <summary>
        /// request to clear signal in explorer mode
        /// </summary>
        public TrackCircuitPartialPathRoute RequestClearSignalExplorer(TrackCircuitPartialPathRoute route, Train.TrainRouted train, bool propagated, int signalNumClearAhead)
        {
            if (null == train)
                throw new ArgumentNullException(nameof(train));
            if (null == route)
                throw new ArgumentNullException(nameof(route));

            // build output route from input route
            TrackCircuitPartialPathRoute newRoute = new TrackCircuitPartialPathRoute(route);

            // don't clear if enabled for another train
            if (EnabledTrain != null && EnabledTrain != train)
                return newRoute;

            // if signal has fixed route, use that else build route
            if (fixedRoute?.Count > 0)
            {
                SignalRoute = new TrackCircuitPartialPathRoute(fixedRoute);
            }
            // build route from signal, upto next signal or max distance, take into account manual switch settings
            else
            {
                List<int> nextRoute = SignalEnvironment.ScanRoute(train.Train, TrackCircuitNextIndex, 0.0f, TrackCircuitNextDirection, true, -1, true, true, true, false,
                true, false, false, false, false, train.Train.IsFreight);

                SignalRoute = new TrackCircuitPartialPathRoute();

                foreach (int sectionIndex in nextRoute)
                {
                    TrackCircuitRouteElement routeElement = new TrackCircuitRouteElement(Math.Abs(sectionIndex), sectionIndex >= 0 ? TrackDirection.Ahead : TrackDirection.Reverse);
                    SignalRoute.Add(routeElement);
                }
            }

            // set full route if route ends with signal
            TrackCircuitSection lastSection = SignalRoute[SignalRoute.Count - 1].TrackCircuitSection;
            TrackDirection lastDirection = SignalRoute[SignalRoute.Count - 1].Direction;

            if (lastSection.EndSignals[lastDirection] != null)
            {
                fullRoute = true;
                Signalfound[(int)SignalFunction.Normal] = lastSection.EndSignals[lastDirection].Index;
            }

            // try and clear signal
            EnabledTrain = train;
            CheckRouteState(propagated, SignalRoute, train);

            // extend route if block is clear or permission is granted, even if signal is not cleared (signal state may depend on next signal)
            bool extendRoute = ((SignalLR(SignalFunction.Normal) > SignalAspectState.Stop) || (internalBlockState <= InternalBlockstate.Reservable));

            // if signal is cleared or permission is granted, extend route with signal route
            if (extendRoute || OverridePermission == SignalPermission.Granted)
            {
                foreach (TrackCircuitRouteElement thisElement in SignalRoute)
                {
                    newRoute.Add(thisElement);
                }
            }

            // if signal is cleared, propagate request if required
            if (extendRoute && fullRoute)
            {
                isPropagated = propagated;
                int ReqNumClearAhead = GetRequestNumberClearAheadExplorer(isPropagated, signalNumClearAhead);
                if (ReqNumClearAhead > 0)
                {
                    int nextSignalIndex = Signalfound[(int)SignalFunction.Normal];
                    if (nextSignalIndex >= 0)
                    {
                        Signal nextSignal = signalEnvironment.Signals[nextSignalIndex];
                        newRoute = nextSignal.RequestClearSignalExplorer(newRoute, train, true, ReqNumClearAhead);
                    }
                }
            }

            return (newRoute);
        }

        /// <summary>
        /// number of remaining signals to clear
        /// </summary>
        public int GetRequestNumberClearAheadExplorer(bool propagated, int signalNumClearAhead)
        {
            int requestNumberClearAhead;
            if (SignalNumClearAheadMsts > -2)
            {
                requestNumberClearAhead = propagated ? signalNumClearAhead - signalNumberNormalHeads : SignalNumClearAheadMsts - signalNumberNormalHeads;
            }
            else
            {
                requestNumberClearAhead = SignalNumClearAheadActive == -1
                    ? propagated ? signalNumClearAhead : 1
                    : SignalNumClearAheadActive == 0 ? 0 : isPropagated ? signalNumClearAhead - 1 : SignalNumClearAheadActive - 1;
            }

            return requestNumberClearAhead;
        }

        /// <summary>
        /// request to clear signal
        /// </summary>
        public bool RequestClearSignal(TrackCircuitPartialPathRoute routePart, Train.TrainRouted train, int clearNextSignals, bool requestIsPropagated, Signal lastSignal)
        {
            if (null == train)
                throw new ArgumentNullException(nameof(train));
            if (null == routePart)
                throw new ArgumentNullException(nameof(routePart));

            // set general variables
            int foundFirstSection = -1;
            int foundLastSection = -1;
            Signal nextSignal = null;

            isPropagated = requestIsPropagated;
            propagated = false;   // always pass on request

            // check if signal not yet enabled - if it is, give warning, reset signal and set both trains to node control, and quit
            if (EnabledTrain != null && EnabledTrain != train)
            {
                Trace.TraceWarning("Request to clear signal {0} from train {1}, signal already enabled for train {2}",
                                       Index, train.Train.Name, EnabledTrain.Train.Name);
                Train.TrainRouted otherTrain = EnabledTrain;
                ResetSignal(true);
                int routeListIndex = train.Train.PresentPosition[train.Direction].RouteListIndex;
                signalEnvironment.BreakDownRouteList(train.Train.ValidRoute[train.TrainRouteDirectionIndex], routeListIndex, train);
                routeListIndex = otherTrain.Train.PresentPosition[otherTrain.Direction].RouteListIndex;
                signalEnvironment.BreakDownRouteList(otherTrain.Train.ValidRoute[otherTrain.TrainRouteDirectionIndex], routeListIndex, otherTrain);

                train.Train.SwitchToNodeControl(train.Train.PresentPosition[train.Direction].TrackCircuitSectionIndex);
                if (otherTrain.Train.ControlMode != TrainControlMode.Explorer && !otherTrain.Train.IsPathless)
                    otherTrain.Train.SwitchToNodeControl(otherTrain.Train.PresentPosition[otherTrain.Direction].TrackCircuitSectionIndex);
                return false;
            }
            if (train.Train.TCRoute != null && HasLockForTrain(train.Train.Number, train.Train.TCRoute.ActiveSubPath))
            {
                return false;
            }
            if (EnabledTrain != train) // new allocation - reset next signals
            {
                for (int i = 0; i < signalEnvironment.OrtsSignalTypeCount; i++)
                {
                    Signalfound[i] = defaultNextSignal[i];
                }
            }
            EnabledTrain = train;

            // find section in route part which follows signal
            SignalRoute.Clear();

            int firstIndex = -1;
            if (lastSignal != null)
            {
                firstIndex = lastSignal.TrainRouteIndex;
            }
            if (firstIndex < 0)
            {
                firstIndex = train.Train.PresentPosition[train.Direction].RouteListIndex;
            }

            if (firstIndex >= 0)
            {
                for (int i = firstIndex; i < routePart.Count && foundFirstSection < 0; i++)
                {
                    TrackCircuitRouteElement thisElement = routePart[i];
                    if (thisElement.TrackCircuitSection.Index == TrackCircuitNextIndex)
                    {
                        foundFirstSection = i;
                        TrainRouteIndex = i;
                    }
                }
            }

            if (foundFirstSection < 0)
            {
                EnabledTrain = null;

                // if signal on holding list, set hold state
                if (train.Train.HoldingSignals.Contains(Index) && HoldState == SignalHoldState.None)
                {
                    HoldState = SignalHoldState.StationStop;
                }
                return false;
            }

            // copy sections upto next normal signal
            // check for loop
            List<int> sectionsInRoute = new List<int>();

            for (int i = foundFirstSection; i < routePart.Count && foundLastSection < 0; i++)
            {
                TrackCircuitRouteElement routeElement = routePart[i];
                if (sectionsInRoute.Contains(routeElement.TrackCircuitSection.Index))
                {
                    foundLastSection = i;  // loop
                }
                else
                {
                    SignalRoute.Add(routeElement);
                    sectionsInRoute.Add(routeElement.TrackCircuitSection.Index);

                    TrackCircuitSection section = routeElement.TrackCircuitSection;

                    // exit if section is pool access section (signal will clear on new route on next try)
                    // reset train details to force new signal clear request
                    // check also creates new full train route
                    // applies to timetable mode only
                    if (train.Train.CheckPoolAccess(section.Index))
                    {
                        EnabledTrain = null;
                        SignalRoute.Clear();

                        return false;
                    }

                    // check if section has end signal - if so is last section
                    if (section.EndSignals[routeElement.Direction] != null)
                    {
                        foundLastSection = i;
                        nextSignal = section.EndSignals[routeElement.Direction];
                    }
                }
            }

            // check if signal has route, is enabled, request is by enabled train and train is not occupying sections in signal route
            if (EnabledTrain == train && SignalRoute?.Count > 0)
            {
                foreach (TrackCircuitRouteElement routeElement in SignalRoute)
                {
                    TrackCircuitSection routeSection = routeElement.TrackCircuitSection;
                    if (routeSection.CircuitState.OccupiedByThisTrain(train))
                    {
                        return false;  // train has passed signal - clear request is invalid
                    }
                }
            }

            // check if end of track reached
            TrackCircuitRouteElement lastSignalElement = SignalRoute[SignalRoute.Count - 1];
            TrackCircuitSection lastSignalSection = lastSignalElement.TrackCircuitSection;

            fullRoute = true;

            // if end of signal route is not a signal or end-of-track it is not a full route
            if (nextSignal == null && lastSignalSection.CircuitType != TrackCircuitType.EndOfTrack)
            {
                fullRoute = false;
            }

            // if next signal is found and relevant, set reference
            Signalfound[(int)SignalFunction.Normal] = nextSignal?.Index ?? -1;

            // set number of signals to clear ahead
            if (SignalNumClearAheadMsts > -2)
            {
                requestedNumClearAhead = clearNextSignals > 0 ? clearNextSignals - signalNumberNormalHeads : SignalNumClearAheadMsts - signalNumberNormalHeads;
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
            CheckRouteState(isPropagated, SignalRoute, train);

            // propagate request
            if (!isPropagated && EnabledTrain != null)
            {
                PropagateRequest();
            }
            if (train.Train is AITrain aitrain && Math.Abs(train.Train.SpeedMpS) <= Simulator.MaxStoppedMpS)
            {
                ref readonly WorldLocation location = ref TdbTraveller.WorldLocation;
                aitrain.AuxActionsContainer.CheckGenActions(GetType(), location, 0f, 0f, TdbTraveller.TrackNode.Index);
            }

            return SignalMR(SignalFunction.Normal) != SignalAspectState.Stop;
        }

        /// <summary>
        /// check and update Route State
        /// </summary>
        public void CheckRouteState(bool propagated, TrackCircuitPartialPathRoute route, Train.TrainRouted train, bool sound = true)
        {
            if (null == route)
                throw new ArgumentNullException(nameof(route));

            // check if signal must be hold
            bool signalHold = HoldState == SignalHoldState.ManualLock || HoldState == SignalHoldState.StationStop;
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
            if (EnabledTrain != null && EnabledTrain == train && SignalRoute != null && SignalRoute.Count > 0)
            {
                int forcedRouteElementIndex = -1;
                foreach (TrackCircuitRouteElement routeElement in SignalRoute)
                {
                    TrackCircuitSection routeSection = routeElement.TrackCircuitSection;
                    if (routeSection.CircuitState.OccupiedByThisTrain(train))
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
                    TrackCircuitSection forcedTrackSection = SignalRoute[forcedRouteElementIndex].TrackCircuitSection;
#pragma warning disable CA1062 // Validate arguments of public methods
                    int forcedRouteSectionIndex = train.Train.ValidRoute[0].GetRouteIndex(forcedTrackSection.Index, 0);
#pragma warning restore CA1062 // Validate arguments of public methods
                    train.Train.ReRouteTrain(forcedRouteSectionIndex, forcedTrackSection.Index);
                    if (train.Train.TrainType == TrainType.Ai || train.Train.TrainType == TrainType.AiPlayerHosting)
                        (train.Train as AITrain).ResetActions(true);
                    forcedTrackSection.CircuitState.Forced = false;
                }
            }

            // test if propagate state still correct - if next signal for enabled train is this signal, it is not propagated
            if (EnabledTrain != null && EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex] != null &&
                EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex].Index == Index)
            {
                propagated = false;
            }

            // test clearance for full route section
            if (!signalHold)
            {
                if (fullRoute)
                {
                    bool newroute = GetBlockState(route, train, !sound);
                    if (newroute)
                        route = SignalRoute;
                }

                // test clearance for sections in route only if first signal ahead of train or if clearance unto partial route is allowed

                else if (EnabledTrain != null && (!propagated || allowPartialRoute) && route.Count > 0)
                {
                    GetPartialBlockState(route);
                }

                // test clearance for sections in route if signal is second signal ahead of train, first signal route is clear but first signal is still showing STOP
                // case for double-hold signals

                else if (EnabledTrain != null && propagated)
                {
                    Signal firstSignal = EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex];
                    if (firstSignal != null &&
                        firstSignal.Signalfound[(int)SignalFunction.Normal] == Index &&
                        firstSignal.internalBlockState <= InternalBlockstate.Reservable &&
                        firstSignal.SignalLR(SignalFunction.Normal) == SignalAspectState.Stop)
                    {
                        GetPartialBlockState(route);
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
            SignalAspectState signalState = SignalLR(SignalFunction.Normal);

            float lengthReserved = 0.0f;

            // check for permission

            if (internalBlockState == InternalBlockstate.OccupiedSameDirection && OverridePermission == SignalPermission.Requested && !propagated)
            {
                OverridePermission = SignalPermission.Granted;
                if (sound)
                    Simulator.Instance.SoundNotify = TrainEvent.PermissionGranted;
            }
            else
            {
                if (EnabledTrain != null && EnabledTrain.Train.ControlMode == TrainControlMode.Manual &&
                    internalBlockState <= InternalBlockstate.OccupiedSameDirection && OverridePermission == SignalPermission.Requested)
                {
                    Simulator.Instance.SoundNotify = TrainEvent.PermissionGranted;
                }
                else if (OverridePermission == SignalPermission.Requested)
                {
                    if (sound)
                        Simulator.Instance.SoundNotify = TrainEvent.PermissionDenied;
                }

                if (EnabledTrain != null && EnabledTrain.Train.ControlMode == TrainControlMode.Manual && signalState == SignalAspectState.Stop &&
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

                    foreach (TrackCircuitRouteElement routelement in route)
                    {
                        TrackCircuitSection section = routelement.TrackCircuitSection;
                        if (section.CircuitState.TrainReserved != null || section.CircuitState.OccupationState.Count > 0)
                        {
                            if (section.CircuitState.TrainReserved != train)
                            {
                                internalBlockState = InternalBlockstate.Reservable; // not all sections are reserved // 
                                break;
                            }
                        }
                        section.Reserve(EnabledTrain, route);
                        EnabledTrain.Train.LastReservedSection[EnabledTrain.TrainRouteDirectionIndex] = routelement.TrackCircuitSection.Index;
                        lengthReserved += section.Length;
                    }

                    EnabledTrain.Train.ClaimState = false;
                }
                // reserve partial sections if signal clears on occupied track or permission is granted
                else if ((signalState > SignalAspectState.Stop || OverridePermission == SignalPermission.Granted) &&
                         (internalBlockState != InternalBlockstate.Reserved && internalBlockState < InternalBlockstate.ReservedOther))
                {

                    // reserve upto available section
                    int lastSectionIndex = 0;

                    for (int i = 0; i < route.Count; i++)
                    {
                        TrackCircuitRouteElement routeElement = route[i];
                        TrackCircuitSection section = routeElement.TrackCircuitSection;

                        if (section.IsAvailable(EnabledTrain))
                        {
                            if (section.CircuitState.TrainReserved == null)
                            {
                                section.Reserve(EnabledTrain, route);
                            }
                            EnabledTrain.Train.LastReservedSection[EnabledTrain.TrainRouteDirectionIndex] = routeElement.TrackCircuitSection.Index;
                            lastSectionIndex = i;
                            lengthReserved += section.Length;
                        }
                        else
                        {
                            break;
                        }
                    }

                    // set pre-reserved or reserved for all other sections

                    for (int i = lastSectionIndex++; i < route.Count; i++)
                    {
                        TrackCircuitRouteElement routeElement = route[i];
                        TrackCircuitSection section = routeElement.TrackCircuitSection;

                        if (section.IsAvailable(EnabledTrain) && section.CircuitState.TrainReserved == null)
                        {
                            section.Reserve(EnabledTrain, route);
                        }
                        else if (section.CircuitState.OccupiedByOtherTrains(EnabledTrain))
                        {
                            section.PreReserve(EnabledTrain);
                        }
                        else if (section.CircuitState.TrainReserved == null || section.CircuitState.TrainReserved.Train != EnabledTrain.Train)
                        {
                            section.PreReserve(EnabledTrain);
                        }
                        else
                        {
                            break;
                        }
                    }
                    EnabledTrain.Train.ClaimState = false;
                }
                // if claim allowed - reserve free sections and claim all other if first signal ahead of train
                else if (EnabledTrain.Train.ClaimState && internalBlockState != InternalBlockstate.Reserved &&
                         EnabledTrain.Train.NextSignalObject[0] != null && EnabledTrain.Train.NextSignalObject[0].Index == Index)
                {
                    foreach (TrackCircuitRouteElement routeElement in route)
                    {
                        TrackCircuitSection section = routeElement.TrackCircuitSection;
                        if (section.DeadlockReference > 0) // do not claim into deadlock area as path may not have been resolved
                        {
                            break;
                        }

                        if (section.CircuitState.TrainReserved == null || (section.CircuitState.TrainReserved.Train != EnabledTrain.Train))
                        {
                            // deadlock has been set since signal request was issued - reject claim, break and reset claimstate
                            if (section.DeadlockTraps.ContainsKey(train.Train.Number))
                            {
                                train.Train.ClaimState = false;
                                break;
                            }

                            // claim only if signal claim is not locked (in case of approach control)
                            if (!claimLocked)
                            {
                                section.Claim(EnabledTrain);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// propagate clearance request
        /// </summary>
        private void PropagateRequest()
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
                nextSignal = signalEnvironment.Signals[Signalfound[(int)SignalFunction.Normal]];
            }

            TrackCircuitPartialPathRoute RoutePart;
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

                if (SignalNumClearAheadMsts <= -2 && SignalNumClearAheadActive != SignalNumClearAheadOrts)
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
            if (validBlockState && SignalLR(SignalFunction.Normal) == SignalAspectState.Stop && SignalNormal())
            {
                propagateState = false;
            }

            if ((requestedNumClearAhead > 0 || ForcePropagation) && nextSignal != null && validBlockState && (!approachControlSet || forcePropOnApproachControl))
            {
                nextSignal.RequestClearSignal(RoutePart, EnabledTrain, requestedNumClearAhead, propagateState, this);
                propagated = true;
                ForcePropagation = false;
            }

            // check if next signal is cleared by default (state != stop and enabled == false) - if so, set train as enabled train but only if train's route covers signal route

            if (nextSignal != null && nextSignal.SignalLR(SignalFunction.Normal) >= SignalAspectState.Approach_1 && nextSignal.FixedRoute && !nextSignal.Enabled && EnabledTrain != null)
            {
                int firstSectionIndex = nextSignal.fixedRoute.First().TrackCircuitSection.Index;
                int lastSectionIndex = nextSignal.fixedRoute.Last().TrackCircuitSection.Index;
                int firstSectionRouteIndex = RoutePart.GetRouteIndex(firstSectionIndex, 0);
                int lastSectionRouteIndex = RoutePart.GetRouteIndex(lastSectionIndex, 0);

                if (firstSectionRouteIndex >= 0 && lastSectionRouteIndex >= 0)
                {
                    nextSignal.RequestClearSignal(nextSignal.fixedRoute, EnabledTrain, 0, true, null);

                    int furtherSignalIndex = nextSignal.Signalfound[(int)SignalFunction.Normal];
                    int furtherSignalsToClear = requestedNumClearAhead - 1;

                    while (furtherSignalIndex >= 0)
                    {
                        Signal furtherSignal = signalEnvironment.Signals[furtherSignalIndex];
                        if (furtherSignal.SignalLR(SignalFunction.Normal) >= SignalAspectState.Approach_1 && !furtherSignal.Enabled && furtherSignal.FixedRoute)
                        {
                            firstSectionIndex = furtherSignal.fixedRoute.First().TrackCircuitSection.Index;
                            lastSectionIndex = furtherSignal.fixedRoute.Last().TrackCircuitSection.Index;
                            firstSectionRouteIndex = RoutePart.GetRouteIndex(firstSectionIndex, 0);
                            lastSectionRouteIndex = RoutePart.GetRouteIndex(lastSectionIndex, 0);

                            if (firstSectionRouteIndex >= 0 && lastSectionRouteIndex >= 0)
                            {
                                furtherSignal.RequestClearSignal(furtherSignal.fixedRoute, EnabledTrain, 0, true, null);

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
        }

        /// <summary>
        /// get block state - not routed
        /// Check blockstate for normal signal which is not enabled
        /// Check blockstate for other types of signals
        /// <summary>
        private void GetNonRoutedBlockState()
        {
            InternalBlockstate localBlockState = InternalBlockstate.Reserved; // preset to lowest option

            // check fixed route for normal signals
            if (SignalNormal() && FixedRoute)
            {
                foreach (TrackCircuitRouteElement routeElement in fixedRoute)
                {
                    if (routeElement.TrackCircuitSection.CircuitState.Occupied())
                    {
                        localBlockState = InternalBlockstate.OccupiedSameDirection;
                    }
                }
            }

            // otherwise follow sections upto first non-set switch or next signal
            else
            {
                int trackCircuit = TrackCircuitIndex;
                TrackDirection direction = TrackCircuitDirection;

                // for normal signals : start at next TC
                if (TrackCircuitNextIndex > 0)
                {
                    trackCircuit = TrackCircuitNextIndex;
                    direction = TrackCircuitNextDirection;
                }

                // get trackcircuit
                TrackCircuitSection section = null;
                if (trackCircuit > 0)
                {
                    section = TrackCircuitSection.TrackCircuitList[trackCircuit];
                }

                // loop through valid sections
                while (section != null)
                {
                    // set blockstate
                    if (section.CircuitState.Occupied())
                    {
                        if (section.Index == TrackCircuitIndex)  // for section where signal is placed, check if train is ahead
                        {
                            Dictionary<Train, float> trainAhead = section.TestTrainAhead(null, TrackCircuitOffset, TrackCircuitDirection);
                            if (trainAhead.Count > 0)
                                localBlockState = InternalBlockstate.OccupiedSameDirection;
                        }
                        else
                        {
                            localBlockState = InternalBlockstate.OccupiedSameDirection;
                        }
                    }

                    // if section has signal at end stop check
                    if (section.EndSignals[direction] != null || section.CircuitType == TrackCircuitType.EndOfTrack)
                    {
                        section = null;
                    }
                    // get next section if active link is set
                    else
                    {
                        //                     int pinIndex = direction == 0 ? 1 : 0;
                        TrackDirection pinIndex = direction;
                        int nextTC = section.ActivePins[pinIndex, Location.NearEnd].Link;
                        direction = section.ActivePins[pinIndex, Location.NearEnd].Direction;
                        if (nextTC == -1)
                        {
                            nextTC = section.ActivePins[pinIndex, Location.FarEnd].Link;
                            direction = section.ActivePins[pinIndex, Location.FarEnd].Direction;
                        }

                        // set state to blocked if ending at unset or unaligned switch

                        if (nextTC >= 0)
                        {
                            section = TrackCircuitSection.TrackCircuitList[nextTC];
                        }
                        else
                        {
                            section = null;
                            localBlockState = InternalBlockstate.Blocked;
                        }
                    }
                }
            }

            internalBlockState = localBlockState;
        }

        /// <summary>
        /// Get block state
        /// Get internal state of full block for normal enabled signal upto next signal for clear request
        /// returns true if train set to use alternative route
        /// </summary>
        private bool GetBlockState(TrackCircuitPartialPathRoute route, Train.TrainRouted train, bool aiPermissionRequest)
        {
            return signalEnvironment.UseLocationPassingPaths ? GetLocationBasedBlockState(route, train, aiPermissionRequest) : GetPathBasedBlockState(route, train);
        }

        /// <summary>
        /// Get block state
        /// Get internal state of full block for normal enabled signal upto next signal for clear request
        /// returns true if train set to use alternative route
        /// based on path-based deadlock processing
        /// </summary>
        private bool GetPathBasedBlockState(TrackCircuitPartialPathRoute route, Train.TrainRouted train)
        {
            bool returnvalue = false;

            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // loop through all sections in route list
            TrackCircuitRouteElement lastElement = null;

            foreach (TrackCircuitRouteElement routeElement in route)
            {
                lastElement = routeElement;
                TrackCircuitSection section = routeElement.TrackCircuitSection;
                TrackDirection direction = routeElement.Direction;
                blockstate = section.GetSectionState(EnabledTrain, (int)direction, blockstate, route, Index);
                if (blockstate > InternalBlockstate.Reservable)
                    break;           // break on first non-reservable section //

                // if alternative path from section available but train already waiting for deadlock, set blocked
                if (routeElement.StartAlternativePath != null)
                {
                    if (routeElement.StartAlternativePath.TrackCircuitSection.CheckDeadlockAwaited(train.Train.Number))
                    {
                        blockstate = InternalBlockstate.Blocked;
                        lastElement = routeElement;
                        break;
                    }
                }
            }

            // check if alternative route available
            int lastElementIndex = route.GetRouteIndex(lastElement.TrackCircuitSection.Index, 0);

            if (blockstate > InternalBlockstate.Reservable && train != null)
            {
                int startAlternativeRoute = -1;
                int altRoute = -1;

                TrackCircuitPartialPathRoute trainRoute = train.Train.ValidRoute[train.TrainRouteDirectionIndex];
                TrackCircuitPosition position = train.Train.PresentPosition[train.Direction];

                for (int iElement = lastElementIndex; iElement >= 0; iElement--)
                {
                    TrackCircuitRouteElement prevElement = route[iElement];
                    if (prevElement.StartAlternativePath != null)
                    {
                        startAlternativeRoute =
                            trainRoute.GetRouteIndex(route[iElement].TrackCircuitSection.Index, position.RouteListIndex);
                        altRoute = prevElement.StartAlternativePath.PathIndex;
                        break;
                    }
                }

                // check if alternative path may be used
                if (startAlternativeRoute > 0)
                {
                    TrackCircuitRouteElement startElement = trainRoute[startAlternativeRoute];
                    TrackCircuitSection endSection = startElement.StartAlternativePath.TrackCircuitSection;
                    if (endSection.CheckDeadlockAwaited(train.Train.Number))
                    {
                        startAlternativeRoute = -1; // reset use of alternative route
                    }
                }

                // if available, select part of route upto next signal
                if (startAlternativeRoute > 0)
                {
                    TrackCircuitPartialPathRoute altRoutePart = train.Train.ExtractAlternativeRoutePathBased(altRoute);

                    // check availability of alternative route
                    InternalBlockstate newblockstate = InternalBlockstate.Reservable;

                    foreach (TrackCircuitRouteElement routeElement in altRoutePart)
                    {
                        TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[routeElement.TrackCircuitSection.Index];
                        TrackDirection direction = routeElement.Direction;
                        newblockstate = section.GetSectionState(EnabledTrain, (int)direction, newblockstate, route, Index);
                        if (newblockstate > InternalBlockstate.Reservable)
                            break;           // break on first non-reservable section //
                    }

                    // if available, use alternative route
                    if (newblockstate <= InternalBlockstate.Reservable)
                    {
                        blockstate = newblockstate;
                        train.Train.SetAlternativeRoutePathBased(startAlternativeRoute, altRoute, this);
                        returnvalue = true;
                    }
                }
            }
            // check if approaching deadlock part, and if alternative route must be taken - if point where alt route start is not yet reserved
            // alternative route may not be taken if there is a train already waiting for the deadlock
            else if (train != null)
            {
                int startAlternativeRoute = -1;
                int altRoute = -1;
                TrackCircuitSection startSection = null;
                TrackCircuitSection endSection = null;

                TrackCircuitPartialPathRoute trainRoute = train.Train.ValidRoute[train.TrainRouteDirectionIndex];
                TrackCircuitPosition position = train.Train.PresentPosition[train.Direction];

                for (int iElement = lastElementIndex; iElement >= 0; iElement--)
                {
                    TrackCircuitRouteElement prevElement = route[iElement];
                    if (prevElement.StartAlternativePath != null)
                    {
                        endSection = prevElement.StartAlternativePath.TrackCircuitSection;
                        if (endSection.DeadlockTraps.ContainsKey(train.Train.Number) && !endSection.CheckDeadlockAwaited(train.Train.Number))
                        {
                            altRoute = prevElement.StartAlternativePath.PathIndex;
                            startAlternativeRoute =
                                trainRoute.GetRouteIndex(prevElement.TrackCircuitSection.Index, position.RouteListIndex);
                            startSection = prevElement.TrackCircuitSection;
                        }
                        break;
                    }
                }

                // use alternative route
                if (startAlternativeRoute > 0 &&
                    (startSection.CircuitState.TrainReserved == null || startSection.CircuitState.TrainReserved.Train != train.Train))
                {
                    TrackCircuitPartialPathRoute altRoutePart = train.Train.ExtractAlternativeRoutePathBased(altRoute);

                    // check availability of alternative route
                    InternalBlockstate newblockstate = InternalBlockstate.Reservable;

                    foreach (TrackCircuitRouteElement routeElement in altRoutePart)
                    {
                        TrackCircuitSection section = routeElement.TrackCircuitSection;
                        TrackDirection direction = routeElement.Direction;
                        newblockstate = section.GetSectionState(EnabledTrain, (int)direction, newblockstate, route, Index);
                        if (newblockstate > InternalBlockstate.Reservable)
                            break;           // break on first non-reservable section //
                    }

                    // if available, use alternative route
                    if (newblockstate <= InternalBlockstate.Reservable)
                    {
                        blockstate = newblockstate;
                        train.Train.SetAlternativeRoutePathBased(startAlternativeRoute, altRoute, this);
                        if (endSection.DeadlockTraps.ContainsKey(train.Train.Number) && !endSection.DeadlockAwaited.Contains(train.Train.Number))
                            endSection.DeadlockAwaited.Add(train.Train.Number);
                        returnvalue = true;
                    }
                }
            }

            internalBlockState = blockstate;
            return (returnvalue);
        }

        /// <summary>
        /// Get block state
        /// Get internal state of full block for normal enabled signal upto next signal for clear request
        /// returns true if train set to use alternative route
        /// based on location-based deadlock processing
        /// </summary>
        private bool GetLocationBasedBlockState(TrackCircuitPartialPathRoute route, Train.TrainRouted train, bool aiPermissionRequest)
        {
            List<int> SectionsWithAlternativePath = new List<int>();
            List<int> SectionsWithAltPathSet = new List<int>();
            bool altRouteAssigned = false;

            bool returnvalue = false;
            bool deadlockArea = false;

            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // loop through all sections in route list
            TrackCircuitRouteElement lastElement = null;

            foreach (TrackCircuitRouteElement routeElement in route)
            {
                lastElement = routeElement;
                TrackCircuitSection section = routeElement.TrackCircuitSection;
                TrackDirection direction = routeElement.Direction;

                blockstate = section.GetSectionState(EnabledTrain, (int)direction, blockstate, route, Index);
                if (blockstate > InternalBlockstate.OccupiedSameDirection)
                    break;     // exit on first none-available section

                // check if section is trigger section for waitany instruction
                if (train != null)
                {
                    if (train.Train.CheckAnyWaitCondition(section.Index))
                    {
                        blockstate = InternalBlockstate.Blocked;
                    }
                }

                // check if this section is start of passing path area
                // if so, select which path must be used - but only if cleared by train in AUTO mode

                if (section.DeadlockReference > 0 && routeElement.FacingPoint && train != null)
                {
                    if (train.Train.ControlMode == TrainControlMode.AutoNode || train.Train.ControlMode == TrainControlMode.AutoSignal)
                    {
                        DeadlockInfo sectionDeadlockInfo = signalEnvironment.DeadlockInfoList[section.DeadlockReference];

                        // if deadlock area and no path yet selected - exit loop; else follow assigned path
                        if (sectionDeadlockInfo.HasTrainAndSubpathIndex(train.Train.Number, train.Train.TCRoute.ActiveSubPath) &&
                            routeElement.UsedAlternativePath < 0)
                        {
                            deadlockArea = true;
                            break; // exits on deadlock area
                        }
                        else
                        {
                            SectionsWithAlternativePath.Add(routeElement.TrackCircuitSection.Index);
                            altRouteAssigned = true;
                        }
                    }
                }
                if (train != null && blockstate == InternalBlockstate.OccupiedSameDirection && (aiPermissionRequest || OverridePermission == SignalPermission.Requested))
                    break;
            }

            // if deadlock area : check alternative path if not yet selected - but only if opening junction is reservable
            // if free alternative path is found, set path available otherwise set path blocked

            if (deadlockArea && lastElement.UsedAlternativePath < 0)
            {
                if (blockstate <= InternalBlockstate.Reservable)
                {

                    TrackCircuitSection lastSection = lastElement.TrackCircuitSection;
                    DeadlockInfo sectionDeadlockInfo = signalEnvironment.DeadlockInfoList[lastSection.DeadlockReference];
                    List<int> availableRoutes = sectionDeadlockInfo.CheckDeadlockPathAvailability(lastSection, train.Train);

                    if (availableRoutes.Count >= 1)
                    {
                        int endSectionIndex = -1;
                        int usedRoute = sectionDeadlockInfo.SelectPath(availableRoutes, train.Train, ref endSectionIndex);
                        lastElement.UsedAlternativePath = usedRoute;
                        SectionsWithAltPathSet.Add(lastElement.TrackCircuitSection.Index);
                        altRouteAssigned = true;

                        train.Train.SetAlternativeRouteLocationBased(lastSection.Index, sectionDeadlockInfo, usedRoute, this);
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
                foreach (int sectionNo in SectionsWithAlternativePath)
                {
                    int routeIndex = train.Train.ValidRoute[0].GetRouteIndex(sectionNo, train.Train.PresentPosition[Direction.Forward].RouteListIndex);
                    train.Train.ValidRoute[0][routeIndex].UsedAlternativePath = -1;
                }
                foreach (int sectionNo in SectionsWithAltPathSet)
                {
                    int routeIndex = train.Train.ValidRoute[0].GetRouteIndex(sectionNo, train.Train.PresentPosition[Direction.Forward].RouteListIndex);
                    train.Train.ValidRoute[0][routeIndex].UsedAlternativePath = -1;
                }
            }

            return returnvalue;
        }

        //================================================================================================//
        /// <summary>
        /// Get part block state
        /// Get internal state of part of block for normal enabled signal upto next signal for clear request
        /// if there are no switches before next signal or end of track, treat as full block
        /// </summary>
        private void GetPartialBlockState(TrackCircuitPartialPathRoute route)
        {

            // check beyond last section for next signal or end of track 
            int listIndex = (route.Count > 0) ? (route.Count - 1) : TrainRouteIndex;

            TrackCircuitRouteElement lastElement = route[listIndex];
            int thisSectionIndex = lastElement.TrackCircuitSection.Index;
            TrackDirection direction = lastElement.Direction;

            TrackCircuitPartialPathRoute additionalElements = new TrackCircuitPartialPathRoute();

            bool endOfInfo = false;

            while (!endOfInfo)
            {
                TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[thisSectionIndex];

                switch (section.CircuitType)
                {
                    case (TrackCircuitType.EndOfTrack):
                        endOfInfo = true;
                        break;
                    case (TrackCircuitType.Junction):
                    case (TrackCircuitType.Crossover):
                        endOfInfo = true;
                        break;
                    default:
                        TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(thisSectionIndex, direction);
                        additionalElements.Add(newElement);

                        if (section.EndSignals[direction] != null)
                        {
                            endOfInfo = true;
                        }
                        break;
                }

                if (!endOfInfo)
                {
                    thisSectionIndex = section.Pins[direction, Location.NearEnd].Link;
                    direction = section.Pins[direction, Location.NearEnd].Direction;
                }
            }

            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // check all elements in original route
            foreach (TrackCircuitRouteElement routeElement in route)
            {
                TrackCircuitSection section = routeElement.TrackCircuitSection;
                direction = routeElement.Direction;
                blockstate = section.GetSectionState(EnabledTrain, (int)direction, blockstate, route, Index);
                if (blockstate > InternalBlockstate.Reservable)
                    break;           // break on first non-reservable section //
            }

            // check all additional elements upto signal, junction or end-of-track
            if (blockstate <= InternalBlockstate.Reservable)
            {
                foreach (TrackCircuitRouteElement routeElement in additionalElements)
                {
                    TrackCircuitSection section = routeElement.TrackCircuitSection;
                    direction = routeElement.Direction;
                    blockstate = section.GetSectionState(EnabledTrain, (int)direction, blockstate, additionalElements, Index);
                    if (blockstate > InternalBlockstate.Reservable)
                        break;           // break on first non-reservable section //
                }
            }

            internalBlockState = blockstate;
        }

        /// <summary>
        /// Set signal default route and next signal list as switch in route is reset
        /// Used in manual mode for signals which clear by default
        /// </summary>
        public void SetDefaultRoute()
        {
            SignalRoute = new TrackCircuitPartialPathRoute(fixedRoute);
            for (int i = 0; i < defaultNextSignal.Length; i++)
            {
                Signalfound[i] = defaultNextSignal[i];
            }
        }

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
                while (thisSignalIndex >= 0 && signalEnvironment.Signals[thisSignalIndex].EnabledTrain == thisTrain)
                {
                    thisSignal = signalEnvironment.Signals[thisSignalIndex];
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
                    foreach (TrackCircuitRouteElement element in nextSignal.SignalRoute)
                    {
                        TrackCircuitSection thisSection = element.TrackCircuitSection;
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

                nextSignal.ResetSignalEnabled();
            }
        }

        /// <summary>
        /// Reset signal route and next signal list as switch in route is reset
        /// </summary>
        public void ResetRoute(int resetSectionIndex)
        {
            // remove this signal from any other junctions
            foreach (int sectionIndex in junctionsPassed)
            {
                if (sectionIndex != resetSectionIndex)
                {
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[sectionIndex];
                    thisSection.SignalsPassingRoutes.Remove(Index);
                }
            }

            junctionsPassed.Clear();

            for (int i = 0; i < signalEnvironment.OrtsSignalTypeCount; i++)
            {
                Signalfound[i] = defaultNextSignal[i];
            }

            // if signal is enabled, ensure next normal signal is reset
            if (EnabledTrain != null && Signalfound[(int)SignalFunction.Normal] < 0)
            {
                Signalfound[(int)SignalFunction.Normal] = SONextSignalNormal(TrackCircuitNextIndex);
            }
        }

        /// <summary>
        /// Set flag to allow signal to clear to partial route
        /// </summary>
        public void AllowClearPartialRoute(int setting)
        {
            allowPartialRoute = setting == 1;
        }

        /// <summary>
        /// Test for approach control - position only
        /// </summary>
        public bool ApproachControlPosition(int requiredPositionM, bool forced)
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
                return true;
            }

            bool found = false;
            bool isNormal = SignalNormal();
            float distance = 0;
            TrackCircuitPartialPathRoute routePath = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex];
            int actRouteIndex = routePath == null ? -1 : routePath.GetRouteIndex(EnabledTrain.Train.PresentPosition[EnabledTrain.Direction].TrackCircuitSectionIndex, 0);
            if (actRouteIndex >= 0)
            {
                float offset;
                if (EnabledTrain.TrainRouteDirectionIndex == 0)
                    offset = EnabledTrain.Train.PresentPosition[Direction.Forward].Offset;
                else
                    offset = TrackCircuitSection.TrackCircuitList[EnabledTrain.Train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex].Length - EnabledTrain.Train.PresentPosition[Direction.Backward].Offset;

                while (!found && actRouteIndex < routePath.Count)
                {
                    TrackCircuitRouteElement routeElement = routePath[actRouteIndex];
                    TrackCircuitSection section = routeElement.TrackCircuitSection;
                    if (section.EndSignals[routeElement.Direction] == this)
                    {
                        distance += section.Length - offset;
                        found = true;
                    }
                    else if (!isNormal)
                    {
                        TrackCircuitSignalList signalList = section.CircuitItems.TrackCircuitSignals[routeElement.Direction][SignalHeads[0].OrtsSignalFunctionIndex];
                        foreach (TrackCircuitSignalItem signal in signalList)
                        {
                            if (signal.Signal == this)
                            {
                                distance += signal.SignalLocation - offset;
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found)
                    {
                        distance += section.Length - offset;
                        offset = 0;
                        actRouteIndex++;
                    }
                }
            }

            if (!found)
            {
                approachControlSet = true;
                return false;
            }

            // test distance
            if ((int)distance < requiredPositionM)
            {
                approachControlSet = false;
                approachControlCleared = true;
                claimLocked = false;
                forcePropOnApproachControl = false;
                return true;
            }
            else
            {
                approachControlSet = true;
                return false;
            }
        }

        /// <summary>
        /// Test for approach control - position and speed
        /// </summary>
        public bool ApproachControlSpeed(int requiredPositionM, int requiredSpeedMpS)
        {
            // no train approaching
            if (EnabledTrain == null)
            {
                return false;
            }

            // signal is not first signal for train
            if (EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex] != null &&
                EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex].Index != Index)
            {
                approachControlSet = true;
                return false;
            }

            // if already cleared - return true
            if (approachControlCleared)
            {
                approachControlSet = false;
                forcePropOnApproachControl = false;
                return true;
            }

            // check if distance is valid
            if (!EnabledTrain.Train.DistanceToSignal.HasValue)
            {
                approachControlSet = true;
                return false;
            }

            // test distance
            if ((int)EnabledTrain.Train.DistanceToSignal.Value < requiredPositionM)
            {
                bool validSpeed = ((requiredSpeedMpS > 0 && Math.Abs(EnabledTrain.Train.SpeedMpS) < requiredSpeedMpS) ||
                    (requiredSpeedMpS == 0 && Math.Abs(EnabledTrain.Train.SpeedMpS) < 0.1));

                if (validSpeed)
                {
                    approachControlCleared = true;
                    approachControlSet = false;
                    claimLocked = false;
                    forcePropOnApproachControl = false;
                    return true;
                }
                else
                {
                    approachControlSet = true;
                    return false;
                }
            }
            else
            {
                approachControlSet = true;
                return false;
            }
        }

        /// <summary>
        /// Test for approach control in case of APC on next STOP
        /// </summary>
        public bool ApproachControlNextStop(int requiredPositionM, int requiredSpeedMpS)
        {
            // no train approaching
            if (EnabledTrain == null)
            {
                return false;
            }

            // signal is not first signal for train
            if (EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex] != null &&
                EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex].Index != Index)
            {
                approachControlSet = true;
                forcePropOnApproachControl = true;
                return false;
            }

            // if already cleared - return true
            if (approachControlCleared)
            {
                return true;
            }

            // check if distance is valid
            if (!EnabledTrain.Train.DistanceToSignal.HasValue)
            {
                approachControlSet = true;
                return false;
            }

            // test distance
            if ((int)EnabledTrain.Train.DistanceToSignal.Value < requiredPositionM)
            {
                bool validSpeed = (requiredSpeedMpS > 0 && Math.Abs(EnabledTrain.Train.SpeedMpS) < requiredSpeedMpS) ||
                    (requiredSpeedMpS == 0 && Math.Abs(EnabledTrain.Train.SpeedMpS) < 0.1);

                if (validSpeed)
                {
                    approachControlCleared = true;
                    approachControlSet = false;
                    claimLocked = false;
                    forcePropOnApproachControl = false;
                    return true;
                }
                else
                {
                    approachControlSet = true;
                    forcePropOnApproachControl = true;
                    return false;
                }
            }
            else
            {
                approachControlSet = true;
                forcePropOnApproachControl = true;
                return false;
            }
        }

        /// <summary>
        /// Lock claim (only if approach control is active)
        /// </summary>
        public void LockClaim()
        {
            claimLocked = approachControlSet;
        }

        /// <summary>
        /// Activate timing trigger
        /// </summary>
        public void ActivateTimingTrigger()
        {
            timingTriggerValue = Simulator.Instance.GameTime;
        }

        /// <summary>
        /// Check timing trigger
        /// </summary>
        public bool CheckTimingTrigger(int requiredTiming)
        {
            int foundDelta = (int)(Simulator.Instance.GameTime - timingTriggerValue);
            return foundDelta > requiredTiming;
        }

        /// <summary>
        /// Test if train has call-on set
        /// </summary>
        public bool TrainHasCallOn(bool allowOnNonePlatform, bool allowAdvancedSignal)
        {
            // no train approaching
            if (EnabledTrain == null)
            {
                return false;
            }

            // signal is not first signal for train
            Signal nextSignal = EnabledTrain.Train.NextSignalObject[EnabledTrain.TrainRouteDirectionIndex];

            return (allowAdvancedSignal || nextSignal == null || nextSignal.Index == Index)
                && EnabledTrain.Train != null && SignalRoute != null
                && (CallOnManuallyAllowed || EnabledTrain.Train.TestCallOn(this, allowOnNonePlatform, SignalRoute));
        }

        /// <summary>
        /// Test if train requires next signal
        /// </summary>
        public bool RequiresNextSignal(int nextSignalId, int requiredPosition)
        {
            // no enabled train
            if (EnabledTrain == null)
            {
                return false;
            }

            // train has no path
            Train reqTrain = EnabledTrain.Train;
            if (reqTrain.ValidRoute == null || reqTrain.ValidRoute[EnabledTrain.TrainRouteDirectionIndex] == null || reqTrain.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].Count <= 0)
            {
                return false;
            }

            // next signal is not valid
            if (nextSignalId < 0 || nextSignalId >= signalEnvironment.Signals.Count || !signalEnvironment.Signals[nextSignalId].SignalNormal())
            {
                return false;
            }

            // trains present position is unknown
            if (reqTrain.PresentPosition[EnabledTrain.Direction].RouteListIndex < 0 ||
                reqTrain.PresentPosition[EnabledTrain.Direction].RouteListIndex >= reqTrain.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].Count)
            {
                return false;
            }

            // check if section beyond or ahead of next signal is within trains path ahead of present position of train
            int reqSection = requiredPosition == 1 ? signalEnvironment.Signals[nextSignalId].TrackCircuitNextIndex : signalEnvironment.Signals[nextSignalId].TrackCircuitIndex;

            int sectionIndex = reqTrain.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].GetRouteIndex(reqSection, reqTrain.PresentPosition[EnabledTrain.Direction].RouteListIndex);
            return sectionIndex > 0;
        }

        /// <summary>
        /// Get ident of signal ahead with specific details
        /// </summary>
        public int FindRequiredNormalSignal(int requiredValue)
        {
            int foundSignal = -1;

            // signal not enabled - no route available
            if (EnabledTrain != null)
            {
                int startIndex = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].GetRouteIndex(TrackCircuitNextIndex, EnabledTrain.Train.PresentPosition[Direction.Forward].RouteListIndex);
                if (startIndex >= 0)
                {
                    for (int iRouteIndex = startIndex; iRouteIndex < EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex].Count; iRouteIndex++)
                    {
                        TrackCircuitRouteElement element = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex][iRouteIndex];
                        TrackCircuitSection section = element.TrackCircuitSection;
                        if (section.EndSignals[element.Direction] != null)
                        {
                            Signal endSignal = section.EndSignals[element.Direction];

                            // found signal, check required value
                            bool found_value = false;

                            foreach (SignalHead thisHead in endSignal.SignalHeads)
                            {
                                if (thisHead.OrtsNormalSubtypeIndex == requiredValue)
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
            return foundSignal;
        }

        /// <summary>
        /// Check if route for train is cleared upto or beyond next required signal
        /// parameter req_position : 0 = check upto signal, 1 = check beyond signal
        /// </summary>
        public SignalBlockState RouteClearedToSignal(int requiredSignalId, bool allowCallOn)
        {
            SignalBlockState routeState = SignalBlockState.Jn_Obstructed;
            if (EnabledTrain?.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex] != null && requiredSignalId >= 0 && requiredSignalId < signalEnvironment.Signals.Count)
            {
                Signal otherSignal = signalEnvironment.Signals[requiredSignalId];

                TrackCircuitPartialPathRoute trainRoute = EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex];

                int thisRouteIndex = trainRoute.GetRouteIndex(SignalNormal() ? TrackCircuitNextIndex : TrackCircuitIndex, 0);
                int otherRouteIndex = trainRoute.GetRouteIndex(otherSignal.TrackCircuitIndex, thisRouteIndex);
                // extract route
                if (otherRouteIndex >= 0)
                {
                    bool routeCleared = true;
                    TrackCircuitPartialPathRoute reqPath = new TrackCircuitPartialPathRoute(trainRoute, thisRouteIndex, otherRouteIndex);

                    for (int i = 0; i < reqPath.Count && routeCleared; i++)
                    {
                        TrackCircuitSection section = reqPath[i].TrackCircuitSection;
                        if (!section.IsSet(EnabledTrain, false))
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
            return routeState;
        }

        /// <summary>
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
            return lockedTrains.Remove(lockedTrains.First(item => item.Key.Equals(trainNumber) && item.Value.Equals(subpath)));
        }

        public bool HasLockForTrain(int trainNumber, int subpath = 0)
        {
            return lockedTrains.Count > 0 && lockedTrains.Exists(item => item.Key.Equals(trainNumber) && item.Value.Equals(subpath));
        }

        public bool CleanAllLock(int trainNumber)
        {
            return lockedTrains.RemoveAll(item => item.Key.Equals(trainNumber)) > 0;
        }

        /// <summary>
        /// Returns 1 if signal has optional head set, 0 if not
        /// </summary>
        public int HasHead(int requiredHeadIndex)
        {
            if (WorldObject == null || WorldObject.HeadsSet == null)
            {
                Trace.TraceInformation("Signal {0} (TDB {1}) has no heads", Index, SignalHeads[0].TDBIndex);
                return 0;
            }
            return (requiredHeadIndex < WorldObject.HeadsSet.Length) ? (WorldObject.HeadsSet[requiredHeadIndex] ? 1 : 0) : 0;
        }

        /// <summary>
        /// Increase SignalNumClearAhead from its default value with the value as passed
        /// <summary>
        public void IncreaseSignalNumClearAhead(int requiredIncreaseValue)
        {
            if (SignalNumClearAheadOrts > -2)
            {
                SignalNumClearAheadActive = SignalNumClearAheadOrts + requiredIncreaseValue;
            }
        }

        /// <summary>
        /// Decrease SignalNumClearAhead from its default value with the value as passed
        /// </summary>
        public void DecreaseSignalNumClearAhead(int requiredDecreaseValue)
        {
            if (SignalNumClearAheadOrts > -2)
            {
                SignalNumClearAheadActive = SignalNumClearAheadOrts - requiredDecreaseValue;
            }
        }

        /// <summary>
        /// Set SignalNumClearAhead to the value as passed
        /// <summary>
        public void SetSignalNumClearAhead(int requiredValue)
        {
            if (SignalNumClearAheadOrts > -2)
            {
                SignalNumClearAheadActive = requiredValue;
            }
        }

        /// <summary>
        /// Reset SignalNumClearAhead to the default value
        /// </summary>
        public void ResetSignalNumClearAhead()
        {
            if (SignalNumClearAheadOrts > -2)
            {
                SignalNumClearAheadActive = SignalNumClearAheadOrts;
            }
        }

        /// <summary>
        /// Set HOLD state for dispatcher control
        /// Parameter : bool, if set signal must be reset if set (and train position allows)
        public void DispatcherRequestHoldSignal(bool requestResetSignal)
        {

            SignalAspectState thisAspect = SignalLR(SignalFunction.Normal);

            SetManualCallOn(false);

            // signal not enabled - set lock, reset if cleared (auto signal can clear without enabling)
            if (EnabledTrain == null || EnabledTrain.Train == null)
            {
                if (thisAspect > SignalAspectState.Stop)
                    ResetSignal(true);

                RequestMostRestrictiveAspect();
            }
            // if enabled, cleared and reset not requested : no action
            else if (!requestResetSignal && thisAspect > SignalAspectState.Stop)
            {
                RequestMostRestrictiveAspect();
            }
            // if enabled and not cleared : set hold, no reset required
            else if (thisAspect == SignalAspectState.Stop)
            {
                RequestMostRestrictiveAspect();
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
                    signalEnvironment.BreakDownRouteList(EnabledTrain.Train.ValidRoute[EnabledTrain.TrainRouteDirectionIndex], signalRouteIndex, EnabledTrain);
                    ResetSignal(true);
                }

                RequestMostRestrictiveAspect();
            }
        }

        public void RequestMostRestrictiveAspect()
        {
            HoldState = SignalHoldState.ManualLock;
            foreach (SignalHead sigHead in SignalHeads)
            {
                sigHead.RequestMostRestrictiveAspect();
            }
        }

        public void RequestApproachAspect()
        {
            HoldState = SignalHoldState.ManualApproach;
            foreach (SignalHead sigHead in SignalHeads)
            {
                sigHead.RequestApproachAspect();
            }
        }

        public void RequestLeastRestrictiveAspect()
        {
            HoldState = SignalHoldState.ManualPass;
            foreach (SignalHead sigHead in SignalHeads)
            {
                sigHead.RequestLeastRestrictiveAspect();
            }
        }

        /// <summary>
        /// Reset HOLD state for dispatcher control
        /// </summary>
        public void DispatcherClearHoldSignal()
        {
            HoldState = SignalHoldState.None;
        }

        /// <summary>
        /// Set call on manually from dispatcher
        /// </summary>
        public void SetManualCallOn(bool state)
        {
            if (EnabledTrain != null)
            {
                if (state && CallOnEnabled)
                {
                    DispatcherClearHoldSignal();
                    CallOnManuallyAllowed = true;
                }
                else
                {
                    CallOnManuallyAllowed = false;
                }
            }
        }

        /// <summary>
        /// Count number of normal signal heads
        /// </summary>
        internal void SetNumberSignalHeads()
        {
            signalNumberNormalHeads = SignalHeads.Where(head => head.SignalFunction == SignalFunction.Normal).Count();
        }

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
                    signal.TrackCircuitOffset = section.Length;
                    signal.TrackCircuitDirection = direction;

                    signal.TrackCircuitNextIndex = section.Pins[direction, Location.NearEnd].Link;
                    signal.TrackCircuitNextDirection = section.Pins[direction, Location.NearEnd].Direction;
                }
            }

            // process other signals - only set info if not already set
            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                for (int i = 0; i < signalEnvironment.OrtsSignalTypeCount; i++)
                {
                    foreach (TrackCircuitSignalItem signalItem in section.CircuitItems.TrackCircuitSignals[direction][i])
                    {
                        Signal signal = signalItem.Signal;

                        if (signal.TrackCircuitIndex <= 0)
                        {
                            signal.TrackCircuitIndex = section.Index;
                            signal.TrackCircuitOffset = signalItem.SignalLocation;
                            signal.TrackCircuitDirection = direction;
                        }
                    }
                }
            }

            // process speedposts
            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                foreach (TrackCircuitSignalItem signalItem in section.CircuitItems.TrackCircuitSpeedPosts[direction])
                {
                    Signal signal = signalItem.Signal;

                    if (signal.TrackCircuitIndex <= 0)
                    {
                        signal.TrackCircuitIndex = section.Index;
                        signal.TrackCircuitOffset = signalItem.SignalLocation;
                        signal.TrackCircuitDirection = direction;
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

        internal bool ValidateSignal()
        {
            if (SignalNormal())
            {
                if (TrackCircuitNextIndex < 0)
                {
                    Trace.TraceInformation($"Signal {Index}; TC : {TrackCircuitIndex}; NextTC : {TrackCircuitNextIndex}; TN : {TrackNode}; TDB (0) : {SignalHeads[0].TDBIndex}");
                }

                if (TrackCircuitIndex < 0) // signal is not on any track - remove it!
                {
                    Trace.TraceInformation($"Signal removed {Index}; TC : {TrackCircuitIndex}; NextTC : {TrackCircuitNextIndex}; TN : {TrackNode}; TDB (0) : {SignalHeads[0].TDBIndex}");
                    return false;
                }
            }
            return true;
        }
    }  // SignalObject

}
