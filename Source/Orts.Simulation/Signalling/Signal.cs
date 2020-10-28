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

// Debug flags :
// #define DEBUG_PRINT
// prints details of the derived signal structure
// #define DEBUG_REPORTS
// print details of train behaviour
// #define DEBUG_DEADLOCK
// print details of deadlock processing

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

        public Signals signalRef;               // reference to overlaying Signal class
        public static Signal[] signalObjects;
        public static TrackNode[] trackNodes;
        public static TrackItem[] trItems;
        public SignalWorldInfo WorldObject;   // Signal World Object information

        public int trackNode;                   // Track node which contains this signal
        public int trRefIndex;                  // Index to TrItemRef within Track Node 

        public int TCReference = -1;            // Reference to TrackCircuit (index)
        public float TCOffset;                  // Position within TrackCircuit
        public int TCDirection;                 // Direction within TrackCircuit
        public int TCNextTC = -1;               // Index of next TrackCircuit (NORMAL signals only)
        public int TCNextDirection;             // Direction of next TrackCircuit 
        public int? nextSwitchIndex = null;     // index of first switch in path

        public List<int> JunctionsPassed = new List<int>();  // Junctions which are passed checking next signal //

        public int thisRef;                     // This signal's reference.
        public int direction;                   // Direction facing on track

        public bool isSignal = true;            // if signal, false if speedpost //
        public bool isSpeedSignal = true;       // if signal of type SPEED, false if fixed speedpost or actual signal
        public List<SignalHead> SignalHeads = new List<SignalHead>();

        public int SignalNumClearAhead_MSTS = -2;    // Overall maximum SignalNumClearAhead over all heads (MSTS calculation)
        public int SignalNumClearAhead_ORTS = -2;    // Overall maximum SignalNumClearAhead over all heads (ORTS calculation)
        public int SignalNumClearAheadActive = -2;   // Active SignalNumClearAhead (for ORST calculation only, as set by script)
        public int SignalNumNormalHeads;             // no. of normal signal heads in signal
        public int ReqNumClearAhead;                 // Passed on value for SignalNumClearAhead

        public int draw_state;                  // actual signal state
        public Dictionary<int, int> localStorage = new Dictionary<int, int>();  // list to store local script variables
        public bool noupdate = false;                // set if signal does not required updates (fixed signals)

        public Train.TrainRouted enabledTrain;  // full train structure for which signal is enabled

        private InternalBlockstate internalBlockState = InternalBlockstate.Open;    // internal blockstate
        public SignalPermission hasPermission = SignalPermission.Denied;  // Permission to pass red signal
        public SignalHoldState holdState = SignalHoldState.None;

        public List<int> sigfound = new List<int>();  // active next signal - used for signals with NORMAL heads only
        public int reqNormalSignal = -1;              // ref of normal signal requesting route clearing (only used for signals without NORMAL heads)
        private List<int> defaultNextSignal = new List<int>();  // default next signal
        public Traveller tdbtraveller;          // TDB traveller to determine distance between objects

        public Train.TCSubpathRoute signalRoute = new Train.TCSubpathRoute();  // train route from signal
        public int trainRouteDirectionIndex;    // direction index in train route array (usually 0, value 1 valid for Manual only)
        public int thisTrainRouteIndex;        // index of section after signal in train route list

        private Train.TCSubpathRoute fixedRoute = new Train.TCSubpathRoute();     // fixed route from signal
        public bool hasFixedRoute;              // signal has fixed route
        private bool fullRoute;                 // required route is full route to next signal or end-of-track
        private bool AllowPartRoute = false;    // signal is always allowed to clear unto partial route
        private bool propagated;                // route request propagated to next signal
        private bool isPropagated;              // route request for this signal was propagated from previous signal
        public bool ForcePropagation = false;   // Force propagation (used in case of signals at very short distance)

        public bool ApproachControlCleared;     // set in case signal has cleared on approach control
        public bool ApproachControlSet;         // set in case approach control is active
        public bool ClaimLocked;                // claim is locked in case of approach control
        public bool ForcePropOnApproachControl; // force propagation if signal is held on close control
        public double TimingTriggerValue;        // used timing trigger if time trigger is required, hold trigger time

        public bool StationHold = false;        // Set if signal must be held at station - processed by signal script
        protected List<KeyValuePair<int, int>> LockedTrains;

        public bool enabled
        {
            get
            {
                if (MPManager.IsMultiPlayer() && MPManager.PreferGreen == true) return true;
                return (enabledTrain != null);
            }
        }

        public SignalBlockState blockState
        {
            get
            {
                SignalBlockState lstate = SignalBlockState.Jn_Obstructed;
                switch (internalBlockState)
                {
                    case InternalBlockstate.Reserved:
                    case InternalBlockstate.Reservable:
                        lstate = SignalBlockState.Clear;
                        break;
                    case InternalBlockstate.OccupiedSameDirection:
                        lstate = SignalBlockState.Occupied;
                        break;
                    default:
                        lstate = SignalBlockState.Jn_Obstructed;
                        break;
                }

                return (lstate);
            }
        }

        public int trItem
        {
            get
            {
                return (trackNodes[trackNode] as TrackVectorNode).TrackItemIndices[trRefIndex];
            }
        }

        public int revDir                //  Needed because signal faces train!
        {
            get
            {
                return direction == 0 ? 1 : 0;
            }
        }

        //================================================================================================//
        /// <summary>
        ///  Constructor for empty item
        /// </summary>

        public Signal(int ORTSSignalTypes)
        {
            LockedTrains = new List<KeyValuePair<int, int>>();
            sigfound = new List<int>();
            defaultNextSignal = new List<int>();

            for (int ifntype = 0; ifntype < ORTSSignalTypes; ifntype++)
            {
                sigfound.Add(-1);
                defaultNextSignal.Add(-1);
            }
        }

        //================================================================================================//
        /// <summary>
        ///  Constructor for Copy 
        /// </summary>

        public Signal(Signal copy)
        {
            signalRef = copy.signalRef;
            WorldObject = new SignalWorldInfo(copy.WorldObject);

            trackNode = copy.trackNode;
            LockedTrains = new List<KeyValuePair<int, int>>();
            foreach (var lockInfo in copy.LockedTrains)
            {
                KeyValuePair<int, int> oneLock = new KeyValuePair<int, int>(lockInfo.Key, lockInfo.Value);
                LockedTrains.Add(oneLock);
            }

            TCReference = copy.TCReference;
            TCOffset = copy.TCOffset;
            TCDirection = copy.TCDirection;
            TCNextTC = copy.TCNextTC;
            TCNextDirection = copy.TCNextDirection;

            direction = copy.direction;
            isSignal = copy.isSignal;
            SignalNumClearAhead_MSTS = copy.SignalNumClearAhead_MSTS;
            SignalNumClearAhead_ORTS = copy.SignalNumClearAhead_ORTS;
            SignalNumClearAheadActive = copy.SignalNumClearAheadActive;
            SignalNumNormalHeads = copy.SignalNumNormalHeads;

            draw_state = copy.draw_state;
            internalBlockState = copy.internalBlockState;
            hasPermission = copy.hasPermission;

            tdbtraveller = new Traveller(copy.tdbtraveller);

            sigfound = new List<int>(copy.sigfound);
            defaultNextSignal = new List<int>(copy.defaultNextSignal);
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for restore
        /// IMPORTANT : enabled train is restore temporarily as Trains are restored later as Signals
        /// Full restore of train link follows in RestoreTrains
        /// </summary>

        public void Restore(Simulator simulator, BinaryReader inf)
        {
            int trainNumber = inf.ReadInt32();

            int sigfoundLength = inf.ReadInt32();
            for (int iSig = 0; iSig < sigfoundLength; iSig++)
            {
                sigfound[iSig] = inf.ReadInt32();
            }

            bool validRoute = inf.ReadBoolean();

            if (validRoute)
            {
                signalRoute = new Train.TCSubpathRoute(inf);
            }

            thisTrainRouteIndex = inf.ReadInt32();
            holdState = (SignalHoldState)inf.ReadInt32();

            int totalJnPassed = inf.ReadInt32();

            for (int iJn = 0; iJn < totalJnPassed; iJn++)
            {
                int thisJunction = inf.ReadInt32();
                JunctionsPassed.Add(thisJunction);
                signalRef.TrackCircuitList[thisJunction].SignalsPassingRoutes.Add(thisRef);
            }

            fullRoute = inf.ReadBoolean();
            AllowPartRoute = inf.ReadBoolean();
            propagated = inf.ReadBoolean();
            isPropagated = inf.ReadBoolean();
            ForcePropagation = false; // preset (not stored)
            SignalNumClearAheadActive = inf.ReadInt32();
            ReqNumClearAhead = inf.ReadInt32();
            StationHold = inf.ReadBoolean();
            ApproachControlCleared = inf.ReadBoolean();
            ApproachControlSet = inf.ReadBoolean();
            ClaimLocked = inf.ReadBoolean();
            ForcePropOnApproachControl = inf.ReadBoolean();
            hasPermission = (SignalPermission)inf.ReadInt32();

            // set dummy train, route direction index will be set later on restore of train

            enabledTrain = null;

            if (trainNumber >= 0)
            {
                Train thisTrain = new Train(simulator, trainNumber);
                Train.TrainRouted thisTrainRouted = new Train.TrainRouted(thisTrain, 0);
                enabledTrain = thisTrainRouted;
            }
            //  Retrieve lock table
            LockedTrains = new List<KeyValuePair<int, int>>();
            int cntLock = inf.ReadInt32();
            for (int cnt = 0; cnt < cntLock; cnt++)
            {
                KeyValuePair<int, int> lockInfo = new KeyValuePair<int, int>(inf.ReadInt32(), inf.ReadInt32());
                LockedTrains.Add(lockInfo);

            }
        }

        //================================================================================================//
        /// <summary>
        /// Restore Train Reference
        /// </summary>

        public void RestoreTrains(List<Train> trains)
        {
            if (enabledTrain != null)
            {
                int number = enabledTrain.Train.Number;

                Train foundTrain = Signals.FindTrain(number, trains);

                // check if this signal is next signal forward for this train

                if (foundTrain != null && foundTrain.NextSignalObject[0] != null && this.thisRef == foundTrain.NextSignalObject[0].thisRef)
                {
                    enabledTrain = foundTrain.routedForward;
                    foundTrain.NextSignalObject[0] = this;
                }

                // check if this signal is next signal backward for this train

                else if (foundTrain != null && foundTrain.NextSignalObject[1] != null && this.thisRef == foundTrain.NextSignalObject[1].thisRef)
                {
                    enabledTrain = foundTrain.routedBackward;
                    foundTrain.NextSignalObject[1] = this;
                }
                else
                {
                    // check if this section is reserved for this train

                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[TCReference];
                    if (thisSection.CircuitState.TrainReserved != null && thisSection.CircuitState.TrainReserved.Train.Number == number)
                    {
                        enabledTrain = thisSection.CircuitState.TrainReserved;
                    }
                    else
                    {
                        enabledTrain = null; // reset - train not found
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
            if (enabledTrain != null && !isPropagated)
            {
                if (isSignalNormal())
                {
                    checkRouteState(false, signalRoute, enabledTrain);
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
            if (enabledTrain == null)
            {
                outf.Write(-1);
            }
            else
            {
                outf.Write(enabledTrain.Train.Number);
            }

            outf.Write(sigfound.Count);
            foreach (int thisSig in sigfound)
            {
                outf.Write(thisSig);
            }

            if (signalRoute == null)
            {
                outf.Write(false);
            }
            else
            {
                outf.Write(true);
                signalRoute.Save(outf);
            }

            outf.Write(thisTrainRouteIndex);
            outf.Write((int)holdState);

            outf.Write(JunctionsPassed.Count);
            if (JunctionsPassed.Count > 0)
            {
                foreach (int thisJunction in JunctionsPassed)
                {
                    outf.Write(thisJunction);
                }
            }

            outf.Write(fullRoute);
            outf.Write(AllowPartRoute);
            outf.Write(propagated);
            outf.Write(isPropagated);
            outf.Write(SignalNumClearAheadActive);
            outf.Write(ReqNumClearAhead);
            outf.Write(StationHold);
            outf.Write(ApproachControlCleared);
            outf.Write(ApproachControlSet);
            outf.Write(ClaimLocked);
            outf.Write(ForcePropOnApproachControl);
            outf.Write((int)hasPermission);
            outf.Write(LockedTrains.Count);
            for (int cnt = 0; cnt < LockedTrains.Count; cnt++)
            {
                outf.Write(LockedTrains[cnt].Key);
                outf.Write(LockedTrains[cnt].Value);
            }

        }

        //================================================================================================//
        /// <summary>
        /// return blockstate
        /// </summary>

        public SignalBlockState block_state()
        {
            return (blockState);
        }

        //================================================================================================//
        /// <summary>
        /// return station hold state
        /// </summary>

        public bool isStationHold()
        {
            return (StationHold);
        }

        //================================================================================================//
        /// <summary>
        /// setSignalDefaultNextSignal : set default next signal based on non-Junction tracks ahead
        /// this routine also sets fixed routes for signals which do not lead onto junction or crossover
        /// </summary>

        public void setSignalDefaultNextSignal()
        {
            int thisTC = TCReference;
            float position = TCOffset;
            TrackDirection direction = (TrackDirection)TCDirection;
            bool setFixedRoute = false;

            // for normal signals : start at next TC

            if (TCNextTC > 0)
            {
                thisTC = TCNextTC;
                direction = (TrackDirection)TCNextDirection;
                position = 0.0f;
                setFixedRoute = true;
            }

            bool completedFixedRoute = !setFixedRoute;

            // get trackcircuit

            TrackCircuitSection thisSection = null;
            if (thisTC > 0)
            {
                thisSection = signalRef.TrackCircuitList[thisTC];
            }

            // set default

            for (int fntype = 0; fntype < defaultNextSignal.Count; fntype++)
            {
                defaultNextSignal[fntype] = -1;
            }

            // loop through valid sections
            while (thisSection != null && thisSection.CircuitType == TrackCircuitType.Normal)
            {
                if (!completedFixedRoute)
                {
                    fixedRoute.Add(new Train.TCRouteElement(thisSection.Index, (int)direction));
                }

                // normal signal

                if (defaultNextSignal[(int)SignalFunction.Normal] < 0)
                {
                    if (thisSection.EndSignals[direction] != null)
                    {
                        defaultNextSignal[(int)SignalFunction.Normal] = thisSection.EndSignals[direction].thisRef;
                        completedFixedRoute = true;
                    }
                }

                // other signals

                for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
                {
                    if (fntype != (int)SignalFunction.Normal)
                    {
                        TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                        bool signalFound = defaultNextSignal[fntype] >= 0;
                        for (int iItem = 0; iItem < thisList.Count && !signalFound; iItem++)
                        {
                            TrackCircuitSignalItem thisItem = thisList[iItem];
                            if (thisItem.Signal.thisRef != thisRef && (thisSection.Index != thisTC || thisItem.SignalLocation > position))
                            {
                                defaultNextSignal[fntype] = thisItem.Signal.thisRef;
                                signalFound = true;
                            }
                        }
                    }
                }

                TrackDirection currentDirection = direction;
                direction = thisSection.Pins[direction, Location.NearEnd].Direction;
                thisSection = signalRef.TrackCircuitList[thisSection.Pins[currentDirection, Location.NearEnd].Link];
            }

            // copy default as valid items

            for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
            {
                sigfound[fntype] = defaultNextSignal[fntype];
            }

            // Allow use of fixed route if ended on END_OF_TRACK

            if (thisSection != null && thisSection.CircuitType == TrackCircuitType.EndOfTrack)
            {
                completedFixedRoute = true;
            }

            // if valid next normal signal found, signal has fixed route

            if (setFixedRoute && completedFixedRoute)
            {
                hasFixedRoute = true;
                fullRoute = true;
            }
            else
            {
                hasFixedRoute = false;
                fixedRoute.Clear();
            }
        }

        //================================================================================================//
        /// <summary>
        /// isSignalNormal : Returns true if at least one signal head is type normal.
        /// </summary>

        public bool isSignalNormal()
        {
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == SignalFunction.Normal)
                    return true;
            }
            return false;
        }

        //================================================================================================//
        /// <summary>
        /// isORTSSignalType : Returns true if at least one signal head is of required type
        /// </summary>

        public bool isORTSSignalType(int reqSIGFN)
        {
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (reqSIGFN == sigHead.OrtsSignalFunctionIndex)
                    return true;
            }
            return false;
        }

        //================================================================================================//
        /// <summary>
        /// next_sig_mr : returns most restrictive state of next signal of required type
        /// </summary>

        public SignalAspectState next_sig_mr(int fn_type)
        {
            int nextSignal = sigfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                sigfound[fn_type] = nextSignal;
            }

            if (nextSignal >= 0)
            {
                return signalObjects[nextSignal].this_sig_mr(fn_type);
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
            int nextSignal = sigfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                sigfound[fn_type] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                return signalObjects[nextSignal].this_sig_lr(fn_type);
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
                int nextSignal = nextSignalObject.sigfound[fn_type];

                // sigfound not set, try direct search
                if (nextSignal < 0)
                {
                    nextSignal = SONextSignal(fn_type);
                    nextSignalObject.sigfound[fn_type] = nextSignal;
                }

                // signal found : get state
                if (nextSignal >= 0)
                {
                    foundsignal++;

                    nextSignalObject = signalObjects[nextSignal];
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
            return (signalFound >= 0 ? signalObjects[signalFound].this_sig_mr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_mr

	/// debug version
        public SignalAspectState opp_sig_mr(int fn_type, ref Signal foundSignal)
        {
            int signalFound = SONextSignalOpp(fn_type);
            foundSignal = signalFound >= 0 ? signalObjects[signalFound] : null;
            return (signalFound >= 0 ? signalObjects[signalFound].this_sig_mr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_mr

        //================================================================================================//
        /// <summary>
        /// opp_sig_lr
        /// </summary>

	/// normal version
        public SignalAspectState opp_sig_lr(int fn_type)
        {
            int signalFound = SONextSignalOpp(fn_type);
            return (signalFound >= 0 ? signalObjects[signalFound].this_sig_lr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_lr

	/// debug version
        public SignalAspectState opp_sig_lr(int fn_type, ref Signal foundSignal)
        {
            int signalFound = SONextSignalOpp(fn_type);
            foundSignal = signalFound >= 0 ? signalObjects[signalFound] : null;
            return (signalFound >= 0 ? signalObjects[signalFound].this_sig_lr(fn_type) : SignalAspectState.Stop);
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
            int nextSignal = sigfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                sigfound[fn_type] = nextSignal;
            }

            if (nextSignal >= 0)
            {
                if (fn_type != (int)SignalFunction.Normal)
                {
                    Signal foundSignalObject = signalRef.SignalObjects[nextSignal];
                    if (isSignalNormal())
                    {
                        foundSignalObject.reqNormalSignal = thisRef;
                    }
                    else
                    {
                        foundSignalObject.reqNormalSignal = reqNormalSignal;
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
            int nextSignal = thisRef;
            int foundsignal = 0;
            Signal nextSignalObject = this;

            while (foundsignal < nsignal && nextSignal >= 0)
            {
                // use sigfound
                nextSignal = nextSignalObject.sigfound[fn_type];

                // sigfound not set, try direct search
                if (nextSignal < 0)
                {
                    nextSignal = nextSignalObject.SONextSignal(fn_type);
                    nextSignalObject.sigfound[fn_type] = nextSignal;
                }

                // signal found
                if (nextSignal >= 0)
                {
                    foundsignal++;
                    nextSignalObject = signalObjects[nextSignal];
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
                            if (!isSignal) set_speed.LimitedSpeedReduction = this_speed.LimitedSpeedReduction;
                        }

                        if (this_speed.FreightSpeed > 0 && this_speed.FreightSpeed < set_speed.FreightSpeed)
                        {
                            set_speed.FreightSpeed = this_speed.FreightSpeed;
                            set_speed.Flag = false;
                            set_speed.Reset = false;
                            if (!isSignal) set_speed.LimitedSpeedReduction = this_speed.LimitedSpeedReduction;
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
            int nextSignal = sigfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                sigfound[fn_type] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                Signal nextSignalObject = signalObjects[nextSignal];
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
            int nextSignal = sigfound[(int)SignalFunction.Normal];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal((int)SignalFunction.Normal);
                sigfound[(int)SignalFunction.Normal] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                Signal nextSignalObject = signalObjects[nextSignal];
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
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[TCReference];
                TrackDirection sectionDirection = (TrackDirection)TCDirection;

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
                                    thisRef
                                };
                            }
                            else if (!thisSection.LinkedSignals.Contains(thisRef))
                            {
                                thisSection.LinkedSignals.Add(thisRef);
                            }
                        }

                    }

                    sectionDirection = thisSection.Pins[pinIndex, Location.NearEnd].Direction;

                    if (thisSection.CircuitType != TrackCircuitType.EndOfTrack && thisSection.Pins[pinIndex, Location.NearEnd].Link >= 0)
                    {
                        thisSection = signalRef.TrackCircuitList[thisSection.Pins[pinIndex, Location.NearEnd].Link];
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
                TrackCircuitSection switchSection = signalRef.TrackCircuitList[nextSwitchIndex.Value];
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

            if (enabledTrain != null && !MPManager.IsMultiPlayer())
            {
                Train.TCSubpathRoute RoutePart = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex];

                TrackNode thisNode = signalRef.trackDB.TrackNodes[req_mainnode];
                if (RoutePart != null)
                {
                    for (int iSection = 0; iSection <= thisNode.TrackCircuitCrossReferences.Count - 1 && !routeset; iSection++)
                    {
                        int sectionIndex = thisNode.TrackCircuitCrossReferences[iSection].Index;

                        for (int iElement = 0; iElement < RoutePart.Count && !routeset; iElement++)
                        {
                            routeset = (sectionIndex == RoutePart[iElement].TCSectionIndex && signalRef.TrackCircuitList[sectionIndex].CircuitType == TrackCircuitType.Normal);
                        }
                    }
                }

                // if not found in trainroute, try signalroute

                if (!routeset && signalRoute != null)
                {
                    for (int iElement = 0; iElement <= signalRoute.Count - 1 && !routeset; iElement++)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[signalRoute[iElement].TCSectionIndex];
                        routeset = (thisSection.OriginalIndex == req_mainnode && thisSection.CircuitType == TrackCircuitType.Normal);
                    }
                }
                retry = !routeset;
            }


            // not enabled, follow set route but only if not normal signal (normal signal will not clear if not enabled)
            // also, for normal enabled signals - try and follow pins (required node may be beyond present route)

            if (retry || !isSignalNormal() || MPManager.IsMultiPlayer())
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[TCReference];
                TrackDirection curDirection = (TrackDirection)TCDirection;
                TrackDirection newDirection;
                int sectionIndex = -1;
                bool passedTrackJn = false;

                List<int> passedSections = new List<int>();
                passedSections.Add(thisSection.Index);

                routeset = (req_mainnode == thisSection.OriginalIndex);
                while (!routeset && thisSection != null)
                {
                    if (thisSection.ActivePins[curDirection, Location.NearEnd].Link >= 0)
                    {
                        newDirection = thisSection.ActivePins[curDirection, Location.NearEnd].Direction;
                        sectionIndex = thisSection.ActivePins[curDirection, Location.NearEnd].Link;
                    }
                    else
                    {
                        newDirection = thisSection.ActivePins[curDirection, Location.FarEnd].Direction;
                        sectionIndex = thisSection.ActivePins[curDirection, Location.FarEnd].Link;
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
                            Location selectedLocation = (Location)(signalRef.trackDB.TrackNodes[thisSection.OriginalIndex] as TrackJunctionNode).SelectedRoute;
                            newDirection = thisSection.Pins[TrackDirection.Reverse, selectedLocation].Direction;
                            sectionIndex = thisSection.Pins[TrackDirection.Reverse, selectedLocation].Link;
                        }
                    }

                    // if NORMAL, if active pins not set use default pins
                    if (sectionIndex < 0 && thisSection.CircuitType == TrackCircuitType.Normal)
                    {
                        newDirection = thisSection.Pins[curDirection, Location.NearEnd].Direction;
                        sectionIndex = thisSection.Pins[curDirection, Location.NearEnd].Link;
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
                        thisSection = signalRef.TrackCircuitList[sectionIndex];
                        curDirection = newDirection;
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
            int thisTC = TCReference;
            TrackDirection direction = (TrackDirection)TCDirection;
            int signalFound = -1;
            TrackCircuitSection thisSection = null;
            bool sectionSet = false;

            // maximise fntype to length of available type list
            int reqtype = Math.Min(fntype, signalRef.ORTSSignalTypeCount);

            // if searching for SPEED signal : check if enabled and use train to find next speedpost
            if (reqtype == (int)SignalFunction.Speed)
            {
                if (enabledTrain != null)
                {
                    signalFound = SONextSignalSpeed(TCReference);
                }
                else
                {
                    return (-1);
                }
            }

            // for normal signals

            else if (reqtype == (int)SignalFunction.Normal)
            {
                if (isSignalNormal())        // if this signal is normal : cannot be done using this route (set through sigfound variable)
                    return (-1);
                signalFound = SONextSignalNormal(TCReference);   // other types of signals (sigfound not used)
            }

        // for other signals : move to next TC (signal would have been default if within same section)

            else
            {
                thisSection = signalRef.TrackCircuitList[thisTC];
                sectionSet = enabledTrain == null ? false : thisSection.IsSet(enabledTrain, false);

                if (sectionSet)
                {
                    thisTC = thisSection.ActivePins[direction, Location.NearEnd].Link;
                    direction = thisSection.ActivePins[direction, Location.NearEnd].Direction;
                }
            }

            // loop through valid sections

            while (sectionSet && thisTC > 0 && signalFound < 0)
            {
                thisSection = signalRef.TrackCircuitList[thisTC];

                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!JunctionsPassed.Contains(thisTC))
                        JunctionsPassed.Add(thisTC);  // set reference to junction section
                    if (!thisSection.SignalsPassingRoutes.Contains(thisRef))
                        thisSection.SignalsPassingRoutes.Add(thisRef);
                }

                // check if required type of signal is along this section

                TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][reqtype];
                if (thisList.Count > 0)
                {
                    signalFound = thisList[0].Signal.thisRef;
                }

                // get next section if active link is set

                if (signalFound < 0)
                {
                    TrackDirection pinIndex = direction;
                    sectionSet = thisSection.IsSet(enabledTrain, false);
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
            if (signalFound < 0 && signalRoute != null && signalRoute.Count > 0)
            {
                for (int iSection = 0; iSection <= (signalRoute.Count - 1) && signalFound < 0; iSection++)
                {
                    thisSection = signalRef.TrackCircuitList[signalRoute[iSection].TCSectionIndex];
                    direction = (TrackDirection)signalRoute[iSection].Direction;
                    TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                    if (thisList.Count > 0)
                    {
                        signalFound = thisList[0].Signal.thisRef;
                    }
                }
            }

            // if signal not found, use route from requesting normal signal
            if (signalFound < 0 && reqNormalSignal >= 0)
            {
                Signal refSignal = signalRef.SignalObjects[reqNormalSignal];
                if (refSignal.signalRoute != null && refSignal.signalRoute.Count > 0)
                {
                    int nextSectionIndex = refSignal.signalRoute.GetRouteIndex(TCReference, 0);

                    if (nextSectionIndex >= 0)
                    {
                        for (int iSection = nextSectionIndex+1; iSection <= (refSignal.signalRoute.Count - 1) && signalFound < 0; iSection++)
                        {
                            thisSection = signalRef.TrackCircuitList[refSignal.signalRoute[iSection].TCSectionIndex];
                            direction = (TrackDirection)refSignal.signalRoute[iSection].Direction;
                            TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                            if (thisList.Count > 0)
                            {
                                signalFound = thisList[0].Signal.thisRef;
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
            int routeListIndex = enabledTrain.Train.ValidRoute[0].GetRouteIndex(TCReference, enabledTrain.Train.PresentPosition[0].RouteListIndex);

            // signal not in train's route
            if (routeListIndex < 0)
            {
                return (-1);
            }

            // find next speed object
            TrackCircuitSignalItem foundItem = signalRef.Find_Next_Object_InRoute(enabledTrain.Train.ValidRoute[0], routeListIndex, TCOffset, -1, SignalFunction.Speed, enabledTrain);
            if (foundItem.SignalState == SignalItemFindState.Item)
            {
                return (foundItem.Signal.thisRef);
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
            TrackDirection direction = (TrackDirection)TCDirection;
            int signalFound = -1;
            TrackCircuitSection thisSection = null;

            TrackDirection pinIndex;

            if (thisTC < 0)
            {
                thisTC = TCReference;
                thisSection = signalRef.TrackCircuitList[thisTC];
                pinIndex = direction;
                thisTC = thisSection.ActivePins[pinIndex, Location.NearEnd].Link;
                direction = thisSection.ActivePins[pinIndex, Location.NearEnd].Direction;
            }

            // loop through valid sections

            while (thisTC > 0 && signalFound < 0)
            {
                thisSection = signalRef.TrackCircuitList[thisTC];

                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!JunctionsPassed.Contains(thisTC))
                        JunctionsPassed.Add(thisTC);  // set reference to junction section
                    if (!thisSection.SignalsPassingRoutes.Contains(thisRef))
                        thisSection.SignalsPassingRoutes.Add(thisRef);
                }

                // check if normal signal is along this section

                if (thisSection.EndSignals[direction] != null)
                {
                    signalFound = thisSection.EndSignals[direction].thisRef;
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

                    if (thisTC == -1 && signalRoute != null)
                    {
                        int thisIndex = signalRoute.GetRouteIndex(thisSection.Index, 0);
                        if (thisIndex >= 0 && thisIndex <= signalRoute.Count - 2)
                        {
                            thisTC = signalRoute[thisIndex + 1].TCSectionIndex;
                            direction = (TrackDirection)signalRoute[thisIndex + 1].Direction;
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
            int thisTC = TCReference;
            TrackDirection direction = TCDirection == 0 ? TrackDirection.Reverse : TrackDirection.Ahead;    // reverse direction
            int signalFound = -1;

            TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisTC];
            bool sectionSet = enabledTrain == null ? false : thisSection.IsSet(enabledTrain, false);

            // loop through valid sections

            while (sectionSet && thisTC > 0 && signalFound < 0)
            {
                thisSection = signalRef.TrackCircuitList[thisTC];

                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!JunctionsPassed.Contains(thisTC))
                        JunctionsPassed.Add(thisTC);  // set reference to junction section
                    if (!thisSection.SignalsPassingRoutes.Contains(thisRef))
                        thisSection.SignalsPassingRoutes.Add(thisRef);
                }

                // check if required type of signal is along this section

                if (fntype == (int) SignalFunction.Normal)
                {
                    signalFound = thisSection.EndSignals[direction] != null ? thisSection.EndSignals[direction].thisRef : -1;
                }
                else
                {
                    TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                    if (thisList.Count > 0)
                    {
                        signalFound = thisList[0].Signal.thisRef;
                    }
                }

                // get next section if active link is set

                if (signalFound < 0)
                {
                    TrackDirection pinIndex = direction;
                    sectionSet = thisSection.IsSet(enabledTrain, false);
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

            if (isSignalNormal())
            {
                // if in hold, set to most restrictive for each head

                if (holdState != SignalHoldState.None)
                {
                    foreach (SignalHead sigHead in SignalHeads)
                    {
                        if (holdState == SignalHoldState.ManualLock || holdState == SignalHoldState.StationStop) sigHead.SetMostRestrictiveAspect();
                    }
                    return;
                }

                // if enabled - perform full update and propagate if not yet done

                if (enabledTrain != null)
                {
                    // if internal state is not reserved (route fully claimed), perform route check

                    if (internalBlockState != InternalBlockstate.Reserved)
                    {
                        checkRouteState(isPropagated, signalRoute, enabledTrain);
                    }

                    // propagate request

                    if (!isPropagated)
                    {
                        propagateRequest();
                    }

                    StateUpdate();

                    // propagate request if not yet done

                    if (!propagated && enabledTrain != null)
                    {
                        propagateRequest();
                    }
                }

        // fixed route - check route and update

                else if (hasFixedRoute)
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

            enabledTrain = null;
            trainRouteDirectionIndex = 0;
            signalRoute.Clear();
            fullRoute = hasFixedRoute;
            thisTrainRouteIndex = -1;

            isPropagated = false;
            propagated = false;
            ForcePropagation = false;
            ApproachControlCleared = false;
            ApproachControlSet = false;
            ClaimLocked = false;
            ForcePropOnApproachControl = false;

            // reset block state to most restrictive

            internalBlockState = InternalBlockstate.Blocked;

            // reset next signal information to default

            for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
            {
                sigfound[fntype] = defaultNextSignal[fntype];
            }

            foreach (int thisSectionIndex in JunctionsPassed)
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisSectionIndex];
                thisSection.SignalsPassingRoutes.Remove(thisRef);
            }

            // reset permission //

            hasPermission = SignalPermission.Denied;

            StateUpdate();
        }

        //================================================================================================//
        /// <summary>
        /// Perform the update for each head on this signal to determine state of signal.
        /// </summary>

        public void StateUpdate()
        {
            // reset approach control (must be explicitly reset as test in script may be conditional)
            ApproachControlSet = false;

            // update all normal heads first

            if (MPManager.IsMultiPlayer())
            {
                if (MPManager.IsClient()) return; //client won't handle signal update

                //if there were hold manually, will not update
                if (holdState == SignalHoldState.ManualApproach || holdState == SignalHoldState.ManualLock || holdState == SignalHoldState.ManualPass) return;
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
            int trItem = (trackNodes[trackNode] as TrackVectorNode).TrackItemIndices[trRefIndex];
            return tdbTraveller.DistanceTo(trItems[trItem].Location);
        }//DistanceTo

        //================================================================================================//
        /// <summary>
        /// Returns the distance from this object to the next object
        /// </summary>

        public float ObjectDistance(Signal nextObject)
        {
            int nextTrItem = (trackNodes[nextObject.trackNode] as TrackVectorNode).TrackItemIndices[nextObject.trRefIndex];
            return this.tdbtraveller.DistanceTo(trItems[nextTrItem].Location);
        }//ObjectDistance

        //================================================================================================//
        /// <summary>
        /// Check whether signal head is for this signal.
        /// </summary>

        public bool isSignalHead(SignalItem signalItem)
        {
            // Tritem for this signal
            SignalItem thisSignalItem = (SignalItem)trItems[this.trItem];
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
                sigHead.SetSignalType(trItems, sigCFG);
            }
        }//SetSignalType

        //================================================================================================//
        /// <summary>
        /// Gets the display aspect for the track monitor.
        /// </summary>

        public TrackMonitorSignalAspect TranslateTMAspect(SignalAspectState SigState)
        {
            switch (SigState)
            {
                case SignalAspectState.Stop:
                    if (hasPermission == SignalPermission.Granted)
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
                signalRoute = new Train.TCSubpathRoute(fixedRoute);
            }

            // build route from signal, upto next signal or max distance, take into account manual switch settings
            else
            {
                List<int> nextRoute = signalRef.ScanRoute(thisTrain.Train, TCNextTC, 0.0f, (TrackDirection)TCNextDirection, true, -1, true, true, true, false,
                true, false, false, false, false, thisTrain.Train.IsFreight);

                signalRoute = new Train.TCSubpathRoute();

                foreach (int sectionIndex in nextRoute)
                {
                    Train.TCRouteElement thisElement = new Train.TCRouteElement(Math.Abs(sectionIndex), sectionIndex >= 0 ? 0 : 1);
                    signalRoute.Add(thisElement);
                }
            }

            // set full route if route ends with signal
            TrackCircuitSection lastSection = signalRef.TrackCircuitList[signalRoute[signalRoute.Count - 1].TCSectionIndex];
            TrackDirection lastDirection = (TrackDirection)signalRoute[signalRoute.Count - 1].Direction;

            if (lastSection.EndSignals[lastDirection] != null)
            {
                fullRoute = true;
                sigfound[(int)SignalFunction.Normal] = lastSection.EndSignals[lastDirection].thisRef;
            }

            // try and clear signal

            enabledTrain = thisTrain;
            checkRouteState(propagated, signalRoute, thisTrain);

            // extend route if block is clear or permission is granted, even if signal is not cleared (signal state may depend on next signal)
            bool extendRoute = false;
            if (this_sig_lr(SignalFunction.Normal) > SignalAspectState.Stop) extendRoute = true;
            if (internalBlockState <= InternalBlockstate.Reservable) extendRoute = true;

            // if signal is cleared or permission is granted, extend route with signal route

            if (extendRoute || hasPermission == SignalPermission.Granted)
            {
                foreach (Train.TCRouteElement thisElement in signalRoute)
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
                        signalNumClearAhead - SignalNumNormalHeads : SignalNumClearAhead_MSTS - SignalNumNormalHeads;
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
                    int nextSignalIndex = sigfound[(int)SignalFunction.Normal];
                    if (nextSignalIndex >= 0)
                    {
                        Signal nextSignal = signalObjects[nextSignalIndex];
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

#if DEBUG_REPORTS
			File.AppendAllText(@"C:\temp\printproc.txt",
				String.Format("Request for clear signal from train {0} at section {1} for signal {2}\n",
				thisTrain.Train.Number,
				thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex,
				thisRef));
#endif
            if (thisTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Request for clear signal from train {0} at section {1} for signal {2}\n",
                    thisTrain.Train.Number,
                    thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex,
                    thisRef));
            }

            // set general variables
            int foundFirstSection = -1;
            int foundLastSection = -1;
            Signal nextSignal = null;

            isPropagated = requestIsPropagated;
            propagated = false;   // always pass on request

            // check if signal not yet enabled - if it is, give warning and quit

            // check if signal not yet enabled - if it is, give warning, reset signal and set both trains to node control, and quit

            if (enabledTrain != null && enabledTrain != thisTrain)
            {
                Trace.TraceWarning("Request to clear signal {0} from train {1}, signal already enabled for train {2}",
                                       thisRef, thisTrain.Train.Name, enabledTrain.Train.Name);
                Train.TrainRouted otherTrain = enabledTrain;
                ResetSignal(true);
                int routeListIndex = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].RouteListIndex;
                signalRef.BreakDownRouteList(thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex], routeListIndex, thisTrain);
                routeListIndex = otherTrain.Train.PresentPosition[otherTrain.TrainRouteDirectionIndex].RouteListIndex;
                signalRef.BreakDownRouteList(otherTrain.Train.ValidRoute[otherTrain.TrainRouteDirectionIndex], routeListIndex, otherTrain);

                thisTrain.Train.SwitchToNodeControl(thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex);
                if (otherTrain.Train.ControlMode != Train.TRAIN_CONTROL.EXPLORER && !otherTrain.Train.IsPathless) otherTrain.Train.SwitchToNodeControl(otherTrain.Train.PresentPosition[otherTrain.TrainRouteDirectionIndex].TCSectionIndex);
                return false;
            }
            if (thisTrain.Train.TCRoute != null && HasLockForTrain(thisTrain.Train.Number, thisTrain.Train.TCRoute.activeSubpath))
            {
                return false;
            }
            if (enabledTrain != thisTrain) // new allocation - reset next signals
            {
                for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
                {
                    sigfound[fntype] = defaultNextSignal[fntype];
                }
            }
            enabledTrain = thisTrain;

            // find section in route part which follows signal

            signalRoute.Clear();

            int firstIndex = -1;
            if (lastSignal != null)
            {
                firstIndex = lastSignal.thisTrainRouteIndex;
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
                    if (thisElement.TCSectionIndex == TCNextTC)
                    {
                        foundFirstSection = iNode;
                        thisTrainRouteIndex = iNode;
                    }
                }
            }

            if (foundFirstSection < 0)
            {
                enabledTrain = null;

                // if signal on holding list, set hold state
                if (thisTrain.Train.HoldingSignals.Contains(thisRef) && holdState == SignalHoldState.None)
                {
                    holdState = SignalHoldState.StationStop;
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
                    signalRoute.Add(thisElement);
                    sectionsInRoute.Add(thisElement.TCSectionIndex);

                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];

                    // exit if section is pool access section (signal will clear on new route on next try)
                    // reset train details to force new signal clear request
                    // check also creates new full train route
                    // applies to timetable mode only
                    if (thisTrain.Train.CheckPoolAccess(thisSection.Index))
                    {
                        enabledTrain = null;
                        signalRoute.Clear();

                        if (thisTrain.Train.CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                String.Format("Reset signal for pool access : {0} \n", thisRef));
                        }

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

            if (enabledTrain != null && enabledTrain == thisTrain && signalRoute != null && signalRoute.Count > 0)
            {
                foreach (Train.TCRouteElement routeElement in signalRoute)
                {
                    TrackCircuitSection routeSection = signalRef.TrackCircuitList[routeElement.TCSectionIndex];
                    if (routeSection.CircuitState.OccupiedByThisTrain(thisTrain))
                    {
                        return false;  // train has passed signal - clear request is invalid
                    }
                }
            }

            // check if end of track reached

            Train.TCRouteElement lastSignalElement = signalRoute[signalRoute.Count - 1];
            TrackCircuitSection lastSignalSection = signalRef.TrackCircuitList[lastSignalElement.TCSectionIndex];

            fullRoute = true;

            // if end of signal route is not a signal or end-of-track it is not a full route

            if (nextSignal == null && lastSignalSection.CircuitType != TrackCircuitType.EndOfTrack)
            {
                fullRoute = false;
            }

            // if next signal is found and relevant, set reference

            if (nextSignal != null)
            {
                sigfound[(int)SignalFunction.Normal] = nextSignal.thisRef;
            }
            else
            {
                sigfound[(int)SignalFunction.Normal] = -1;
            }

            // set number of signals to clear ahead

            if (SignalNumClearAhead_MSTS > -2)
            {
                ReqNumClearAhead = clearNextSignals > 0 ?
                    clearNextSignals - SignalNumNormalHeads : SignalNumClearAhead_MSTS - SignalNumNormalHeads;
            }
            else
            {
                if (SignalNumClearAheadActive == -1)
                {
                    ReqNumClearAhead = clearNextSignals > 0 ? clearNextSignals : 1;
                }
                else if (SignalNumClearAheadActive == 0)
                {
                    ReqNumClearAhead = 0;
                }
                else
                {
                    ReqNumClearAhead = clearNextSignals > 0 ? clearNextSignals - 1 : SignalNumClearAheadActive - 1;
                }
            }

            // perform route check

            checkRouteState(isPropagated, signalRoute, thisTrain);

            // propagate request

            if (!isPropagated && enabledTrain != null)
            {
                propagateRequest();
            }
            if (thisTrain != null && thisTrain.Train is AITrain && Math.Abs(thisTrain.Train.SpeedMpS) <= Simulator.MaxStoppedMpS)
            {
                WorldLocation location = this.tdbtraveller.WorldLocation;
                ((AITrain)thisTrain.Train).AuxActionsContain.CheckGenActions(this.GetType(), location, 0f, 0f, this.tdbtraveller.TrackNodeIndex);
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
            bool signalHold = (holdState != SignalHoldState.None);
            if (enabledTrain != null && enabledTrain.Train.HoldingSignals.Contains(thisRef) && holdState < SignalHoldState.ManualLock)
            {
                holdState = SignalHoldState.StationStop;
                signalHold = true;
            }
            else if (holdState == SignalHoldState.StationStop)
            {
                if (enabledTrain == null || !enabledTrain.Train.HoldingSignals.Contains(thisRef))
                {
                    holdState = SignalHoldState.None;
                    signalHold = false;
                }
            }

            // check if signal has route, is enabled, request is by enabled train and train is not occupying sections in signal route

            if (enabledTrain != null && enabledTrain == thisTrain && signalRoute != null && signalRoute.Count > 0)
            {
                var forcedRouteElementIndex = -1;
                foreach (Train.TCRouteElement routeElement in signalRoute)
                {
                    TrackCircuitSection routeSection = signalRef.TrackCircuitList[routeElement.TCSectionIndex];
                    if (routeSection.CircuitState.OccupiedByThisTrain(thisTrain))
                    {
                        return;  // train has passed signal - clear request is invalid
                    }
                    if (routeSection.CircuitState.Forced)
                    {
                        // route must be recomputed after switch moved by dispatcher
                        forcedRouteElementIndex = signalRoute.IndexOf(routeElement);
                        break;
                    }
                }
                if (forcedRouteElementIndex >= 0)
                {
                    int forcedTCSectionIndex = signalRoute[forcedRouteElementIndex].TCSectionIndex;
                    TrackCircuitSection forcedTrackSection = signalRef.TrackCircuitList[forcedTCSectionIndex];
                    int forcedRouteSectionIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(forcedTCSectionIndex, 0);
                    thisTrain.Train.ReRouteTrain(forcedRouteSectionIndex, forcedTCSectionIndex);
                    if (thisTrain.Train.TrainType == Train.TRAINTYPE.AI || thisTrain.Train.TrainType == Train.TRAINTYPE.AI_PLAYERHOSTING)
                        (thisTrain.Train as AITrain).ResetActions(true);
                    forcedTrackSection.CircuitState.Forced = false;
                }
            }

            // test if propagate state still correct - if next signal for enabled train is this signal, it is not propagated

            if (enabledTrain != null && enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex] != null &&
                enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef == thisRef)
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
                        thisRoute = this.signalRoute;
                }

                // test clearance for sections in route only if first signal ahead of train or if clearance unto partial route is allowed

                else if (enabledTrain != null && (!isPropagated || AllowPartRoute) && thisRoute.Count > 0)
                {
                    getPartBlockState(thisRoute);
                }

                // test clearance for sections in route if signal is second signal ahead of train, first signal route is clear but first signal is still showing STOP
                // case for double-hold signals

                else if (enabledTrain != null && isPropagated)
                {
                    Signal firstSignal = enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex];
                    if (firstSignal != null &&
                        firstSignal.sigfound[(int)SignalFunction.Normal] == thisRef &&
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

            if (internalBlockState == InternalBlockstate.OccupiedSameDirection && hasPermission == SignalPermission.Requested && !isPropagated)
            {
                hasPermission = SignalPermission.Granted;
                if (sound) signalRef.Simulator.SoundNotify = TrainEvent.PermissionGranted;
            }
            else
            {
                if (enabledTrain != null && enabledTrain.Train.ControlMode == Train.TRAIN_CONTROL.MANUAL &&
                    internalBlockState <= InternalBlockstate.OccupiedSameDirection && hasPermission == SignalPermission.Requested)
                {
                    signalRef.Simulator.SoundNotify = TrainEvent.PermissionGranted;
                }
                else if (hasPermission == SignalPermission.Requested)
                {
                    if (sound) signalRef.Simulator.SoundNotify = TrainEvent.PermissionDenied;
                }

                if (enabledTrain != null && enabledTrain.Train.ControlMode == Train.TRAIN_CONTROL.MANUAL && signalState == SignalAspectState.Stop &&
                internalBlockState <= InternalBlockstate.OccupiedSameDirection && hasPermission == SignalPermission.Requested)
                {
                    hasPermission = SignalPermission.Granted;
                }
                else if (hasPermission == SignalPermission.Requested)
                {
                    hasPermission = SignalPermission.Denied;
                }
            }

            // reserve full section if allowed, do not set reserved if signal is held on approach control

            if (enabledTrain != null)
            {
                if (internalBlockState == InternalBlockstate.Reservable && !ApproachControlSet)
                {
                    internalBlockState = InternalBlockstate.Reserved; // preset all sections are reserved

                    foreach (Train.TCRouteElement thisElement in thisRoute)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        if (thisSection.CircuitState.TrainReserved != null || thisSection.CircuitState.OccupationState.Count > 0)
                        {
                            if (thisSection.CircuitState.TrainReserved != thisTrain)
                            {
                                internalBlockState = InternalBlockstate.Reservable; // not all sections are reserved // 
                                break;
                            }
                        }
                        thisSection.Reserve(enabledTrain, thisRoute);
                        enabledTrain.Train.LastReservedSection[enabledTrain.TrainRouteDirectionIndex] = thisElement.TCSectionIndex;
                        lengthReserved += thisSection.Length;
                    }

                    enabledTrain.Train.ClaimState = false;
                }

            // reserve partial sections if signal clears on occupied track or permission is granted

                else if ((signalState > SignalAspectState.Stop || hasPermission == SignalPermission.Granted) &&
                         (internalBlockState != InternalBlockstate.Reserved && internalBlockState < InternalBlockstate.ReservedOther))
                {

                    // reserve upto available section

                    int lastSectionIndex = 0;
                    bool reservable = true;

                    for (int iSection = 0; iSection < thisRoute.Count && reservable; iSection++)
                    {
                        Train.TCRouteElement thisElement = thisRoute[iSection];
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];

                        if (thisSection.IsAvailable(enabledTrain))
                        {
                            if (thisSection.CircuitState.TrainReserved == null)
                            {
                                thisSection.Reserve(enabledTrain, thisRoute);
                            }
                            enabledTrain.Train.LastReservedSection[enabledTrain.TrainRouteDirectionIndex] = thisElement.TCSectionIndex;
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
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];

                        if (thisSection.IsAvailable(enabledTrain) && thisSection.CircuitState.TrainReserved == null)
                        {
                            thisSection.Reserve(enabledTrain, thisRoute);
                        }
                        else if (thisSection.CircuitState.OccupiedByOtherTrains(enabledTrain))
                        {
                            thisSection.PreReserve(enabledTrain);
                        }
                        else if (thisSection.CircuitState.TrainReserved == null || thisSection.CircuitState.TrainReserved.Train != enabledTrain.Train)
                        {
                            thisSection.PreReserve(enabledTrain);
                        }
                        else
                        {
                            reservable = false;
                        }
                    }
                    enabledTrain.Train.ClaimState = false;
                }

            // if claim allowed - reserve free sections and claim all other if first signal ahead of train

                else if (enabledTrain.Train.ClaimState && internalBlockState != InternalBlockstate.Reserved &&
                         enabledTrain.Train.NextSignalObject[0] != null && enabledTrain.Train.NextSignalObject[0].thisRef == thisRef)
                {
                    foreach (Train.TCRouteElement thisElement in thisRoute)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        if (thisSection.DeadlockReference > 0) // do not claim into deadlock area as path may not have been resolved
                        {
                            break;
                        }

                        if (thisSection.CircuitState.TrainReserved == null || (thisSection.CircuitState.TrainReserved.Train != enabledTrain.Train))
                        {
                            // deadlock has been set since signal request was issued - reject claim, break and reset claimstate
                            if (thisSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number))
                            {
                                thisTrain.Train.ClaimState = false;
                                break;
                            }

                            // claim only if signal claim is not locked (in case of approach control)
                            if (!ClaimLocked)
                            {
                                thisSection.Claim(enabledTrain);
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
            if (!ForcePropagation && !ForcePropOnApproachControl && internalBlockState > InternalBlockstate.Reserved)
            {
                validPropagationRequest = false;
            }

            // route is not fully available so do not propagate
            if (!validPropagationRequest)
            {
                return;
            }

            Signal nextSignal = null;
            if (sigfound[(int)SignalFunction.Normal] >= 0)
            {
                nextSignal = signalObjects[sigfound[(int)SignalFunction.Normal]];
            }

            Train.TCSubpathRoute RoutePart;
            if (enabledTrain != null)
            {
                RoutePart = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex];   // if known which route to use
            }
            else
            {
                RoutePart = signalRoute; // else use signal route
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
                        ReqNumClearAhead = 0;
                    }
                    else if (SignalNumClearAheadActive > 0)
                    {
                        ReqNumClearAhead = SignalNumClearAheadActive - 1;
                    }
                    else if (SignalNumClearAheadActive < 0)
                    {
                        ReqNumClearAhead = 1;
                    }
                }
            }

            bool validBlockState = internalBlockState <= InternalBlockstate.Reserved;

            // for approach control, use reservable state instead of reserved state (sections are not reserved on approach control)
            // also on forced propagation, use reservable state instead of reserved state
            if (ApproachControlSet && ForcePropOnApproachControl)
            {
                validBlockState = internalBlockState <= InternalBlockstate.Reservable;
            }

            // if section is clear but signal remains at stop - dual signal situation - do not treat as propagate
            if (validBlockState && this_sig_lr(SignalFunction.Normal) == SignalAspectState.Stop && isSignalNormal())
            {
                propagateState = false;
            }

            if ((ReqNumClearAhead > 0 || ForcePropagation) && nextSignal != null && validBlockState && (!ApproachControlSet || ForcePropOnApproachControl))
            {
                nextSignal.requestClearSignal(RoutePart, enabledTrain, ReqNumClearAhead, propagateState, this);
                propagated = true;
                ForcePropagation = false;
            }

            // check if next signal is cleared by default (state != stop and enabled == false) - if so, set train as enabled train but only if train's route covers signal route

            if (nextSignal != null && nextSignal.this_sig_lr(SignalFunction.Normal) >= SignalAspectState.Approach_1 && nextSignal.hasFixedRoute && !nextSignal.enabled && enabledTrain != null)
            {
                int firstSectionIndex = nextSignal.fixedRoute.First().TCSectionIndex;
                int lastSectionIndex = nextSignal.fixedRoute.Last().TCSectionIndex;
                int firstSectionRouteIndex = RoutePart.GetRouteIndex(firstSectionIndex, 0);
                int lastSectionRouteIndex = RoutePart.GetRouteIndex(lastSectionIndex, 0);

                if (firstSectionRouteIndex >= 0 && lastSectionRouteIndex >= 0)
                {
                    nextSignal.requestClearSignal(nextSignal.fixedRoute, enabledTrain, 0, true, null);

                    int furtherSignalIndex = nextSignal.sigfound[(int)SignalFunction.Normal];
                    int furtherSignalsToClear = ReqNumClearAhead - 1;

                    while (furtherSignalIndex >= 0)
                    {
                        Signal furtherSignal = signalRef.SignalObjects[furtherSignalIndex];
                        if (furtherSignal.this_sig_lr(SignalFunction.Normal) >= SignalAspectState.Approach_1 && !furtherSignal.enabled && furtherSignal.hasFixedRoute)
                        {
                            firstSectionIndex = furtherSignal.fixedRoute.First().TCSectionIndex;
                            lastSectionIndex = furtherSignal.fixedRoute.Last().TCSectionIndex;
                            firstSectionRouteIndex = RoutePart.GetRouteIndex(firstSectionIndex, 0);
                            lastSectionRouteIndex = RoutePart.GetRouteIndex(lastSectionIndex, 0);

                            if (firstSectionRouteIndex >= 0 && lastSectionRouteIndex >= 0)
                            {
                                furtherSignal.requestClearSignal(furtherSignal.fixedRoute, enabledTrain, 0, true, null);

                                furtherSignal.isPropagated = true;
                                furtherSignalsToClear = furtherSignalsToClear > 0 ? furtherSignalsToClear - 1 : 0;
                                furtherSignal.ReqNumClearAhead = furtherSignalsToClear;
                                furtherSignalIndex = furtherSignal.sigfound[(int)SignalFunction.Normal];
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

            if (isSignalNormal() && hasFixedRoute)
            {
                foreach (Train.TCRouteElement thisElement in fixedRoute)
                {
                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                    if (thisSection.CircuitState.Occupied())
                    {
                        localBlockState = InternalBlockstate.OccupiedSameDirection;
                    }
                }
            }

        // otherwise follow sections upto first non-set switch or next signal
            else
            {
                int thisTC = TCReference;
                TrackDirection direction = (TrackDirection)TCDirection;
                int nextTC = -1;

                // for normal signals : start at next TC

                if (TCNextTC > 0)
                {
                    thisTC = TCNextTC;
                    direction = (TrackDirection)TCNextDirection;
                }

                // get trackcircuit

                TrackCircuitSection thisSection = null;
                if (thisTC > 0)
                {
                    thisSection = signalRef.TrackCircuitList[thisTC];
                }

                // loop through valid sections

                while (thisSection != null)
                {

                    // set blockstate

                    if (thisSection.CircuitState.Occupied())
                    {
                        if (thisSection.Index == TCReference)  // for section where signal is placed, check if train is ahead
                        {
                            Dictionary<Train, float> trainAhead =
                                                    thisSection.TestTrainAhead(null, TCOffset, TCDirection);
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
                            thisSection = signalRef.TrackCircuitList[nextTC];
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
            if (signalRef.UseLocationPassingPaths)
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
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                int direction = thisElement.Direction;
                blockstate = thisSection.GetSectionState(enabledTrain, direction, blockstate, thisRoute, thisRef);
                if (blockstate > InternalBlockstate.Reservable)
                    break;           // break on first non-reservable section //

                // if alternative path from section available but train already waiting for deadlock, set blocked
                if (thisElement.StartAlternativePath != null)
                {
                    TrackCircuitSection endSection = signalRef.TrackCircuitList[thisElement.StartAlternativePath[1]];
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
                    TrackCircuitSection endSection = signalRef.TrackCircuitList[endSectionIndex];
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
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        int direction = thisElement.Direction;
                        newblockstate = thisSection.GetSectionState(enabledTrain, direction, newblockstate, thisRoute, thisRef);
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
                        endSection = signalRef.TrackCircuitList[prevElement.StartAlternativePath[1]];
                        if (endSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number) && !endSection.CheckDeadlockAwaited(thisTrain.Train.Number))
                        {
                            altRoute = prevElement.StartAlternativePath[0];
                            startAlternativeRoute =
                                trainRoute.GetRouteIndex(prevElement.TCSectionIndex, thisPosition.RouteListIndex);
                            startSection = signalRef.TrackCircuitList[prevElement.TCSectionIndex];
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
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        int direction = thisElement.Direction;
                        newblockstate = thisSection.GetSectionState(enabledTrain, direction, newblockstate, thisRoute, thisRef);
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
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                int direction = thisElement.Direction;

                blockstate = thisSection.GetSectionState(enabledTrain, direction, blockstate, thisRoute, thisRef);
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
                        DeadlockInfo sectionDeadlockInfo = signalRef.DeadlockInfoList[thisSection.DeadlockReference];

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
                if (thisTrain != null && blockstate == InternalBlockstate.OccupiedSameDirection && (AIPermissionRequest || hasPermission == SignalPermission.Requested)) break;
            }

            // if deadlock area : check alternative path if not yet selected - but only if opening junction is reservable
            // if free alternative path is found, set path available otherwise set path blocked

            if (deadlockArea && lastElement.UsedAlternativePath < 0)
            {
                if (blockstate <= InternalBlockstate.Reservable)
                {

#if DEBUG_DEADLOCK
                File.AppendAllText(@"C:\Temp\deadlock.txt",
                    "\n **** Get block state for section " + lastElement.TCSectionIndex.ToString() + " for train : " + thisTrain.Train.Number.ToString() + "\n");
#endif
                    TrackCircuitSection lastSection = signalRef.TrackCircuitList[lastElement.TCSectionIndex];
                    DeadlockInfo sectionDeadlockInfo = signalRef.DeadlockInfoList[lastSection.DeadlockReference];
                    List<int> availableRoutes = sectionDeadlockInfo.CheckDeadlockPathAvailability(lastSection, thisTrain.Train);

#if DEBUG_DEADLOCK
                File.AppendAllText(@"C:\Temp\deadlock.txt", "\nReturned no. of available paths : " + availableRoutes.Count.ToString() + "\n");
                File.AppendAllText(@"C:\Temp\deadlock.txt", "****\n\n");
#endif

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
#if DEBUG_REPORTS
                    Trace.TraceInformation("Train : {0} : state {1} but route already set for section {2}",
                        thisTrain.Train.Name, blockstate, SectionNo);
#endif
                    int routeIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(SectionNo, thisTrain.Train.PresentPosition[0].RouteListIndex);
                    Train.TCRouteElement thisElement = thisTrain.Train.ValidRoute[0][routeIndex];
                    thisElement.UsedAlternativePath = -1;
                }
                foreach (int SectionNo in SectionsWithAltPathSet)
                {
#if DEBUG_REPORTS
                    Trace.TraceInformation("Train : {0} : state {1} but route now set for section {2}",
                        thisTrain.Train.Name, blockstate, SectionNo);
#endif
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

            int listIndex = (thisRoute.Count > 0) ? (thisRoute.Count - 1) : thisTrainRouteIndex;

            Train.TCRouteElement lastElement = thisRoute[listIndex];
            int thisSectionIndex = lastElement.TCSectionIndex;
            TrackDirection direction = (TrackDirection)lastElement.Direction;

            Train.TCSubpathRoute additionalElements = new Train.TCSubpathRoute();

            bool end_of_info = false;

            while (!end_of_info)
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisSectionIndex];

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
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                direction = (TrackDirection)thisElement.Direction;
                blockstate = thisSection.GetSectionState(enabledTrain, (int)direction, blockstate, thisRoute, thisRef);
                if (blockstate > InternalBlockstate.Reservable)
                    break;           // break on first non-reservable section //
            }

            // check all additional elements upto signal, junction or end-of-track

            if (blockstate <= InternalBlockstate.Reservable)
            {
                foreach (Train.TCRouteElement thisElement in additionalElements)
                {
                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                    direction = (TrackDirection)thisElement.Direction;
                    blockstate = thisSection.GetSectionState(enabledTrain, (int)direction, blockstate, additionalElements, thisRef);
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
            signalRoute = new Train.TCSubpathRoute(fixedRoute);
            for (int iSigtype = 0; iSigtype <= defaultNextSignal.Count - 1; iSigtype++)
            {
                sigfound[iSigtype] = defaultNextSignal[iSigtype];
            }
        }

        //================================================================================================//
        /// <summary>
        /// Reset signal and clear all train sections
        /// </summary>

        public void ResetSignal(bool propagateReset)
        {
            Train.TrainRouted thisTrain = enabledTrain;

            // search for last signal enabled for this train, start reset from there //

            Signal thisSignal = this;
            List<Signal> passedSignals = new List<Signal>();
            int thisSignalIndex = thisSignal.thisRef;

            if (propagateReset)
            {
                while (thisSignalIndex >= 0 && signalObjects[thisSignalIndex].enabledTrain == thisTrain)
                {
                    thisSignal = signalObjects[thisSignalIndex];
                    passedSignals.Add(thisSignal);
                    thisSignalIndex = thisSignal.sigfound[(int)SignalFunction.Normal];
                }
            }
            else
            {
                passedSignals.Add(thisSignal);
            }

            foreach (Signal nextSignal in passedSignals)
            {
                if (nextSignal.signalRoute != null)
                {
                    List<TrackCircuitSection> sectionsToClear = new List<TrackCircuitSection>();
                    foreach (Train.TCRouteElement thisElement in nextSignal.signalRoute)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
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

            foreach (int thisSectionIndex in JunctionsPassed)
            {
                if (thisSectionIndex != resetSectionIndex)
                {
                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisSectionIndex];
                    thisSection.SignalsPassingRoutes.Remove(thisRef);
                }
            }

            JunctionsPassed.Clear();

            for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
            {
                sigfound[fntype] = defaultNextSignal[fntype];
            }

            // if signal is enabled, ensure next normal signal is reset

            if (enabledTrain != null && sigfound[(int)SignalFunction.Normal] < 0)
            {
                sigfound[(int)SignalFunction.Normal] = SONextSignalNormal(TCNextTC);
            }

#if DEBUG_REPORTS
            File.AppendAllText(@"C:\temp\printproc.txt",
				String.Format("Signal {0} reset on Junction Change\n",
				thisRef));

            if (enabledTrain != null)
            {
				File.AppendAllText(@"C:\temp\printproc.txt",
					String.Format("Train {0} affected; new NORMAL signal : {1}\n",
					enabledTrain.Train.Number, sigfound[(int)MstsSignalFunction.NORMAL]));
            }
#endif
            if (enabledTrain != null && enabledTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Signal {0} reset on Junction Change\n",
                    thisRef));
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Train {0} affected; new NORMAL signal : {1}\n",
                    enabledTrain.Train.Number, sigfound[(int)SignalFunction.Normal]));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set flag to allow signal to clear to partial route
        /// </summary>

        public void AllowClearPartialRoute(int setting)
        {
            AllowPartRoute = setting == 1 ? true : false;
        }

        //================================================================================================//
        /// <summary>
        /// Test for approach control - position only
        /// </summary>

        public bool ApproachControlPosition(int reqPositionM, string dumpfile, bool forced)
        {
            // no train approaching
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "APPROACH CONTROL : no train approaching");
                }

                return (false);
            }

            // signal is not first signal for train - check only if not forced
            if (!forced)
            {
                if (enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex] == null ||
                    enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef != thisRef)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} : First signal is not this signal but {1} \n",
                            enabledTrain.Train.Number, enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlSet = true;  // approach control is selected but train is yet further out, so assume approach control has locked signal
                    return (false);
                }
            }

            // if already cleared - return true

            if (ApproachControlCleared)
            {
                ApproachControlSet = false;
                ClaimLocked = false;
                ForcePropOnApproachControl = false;
                return (true);
            }

            bool found = false;
            float distance = 0;
            int actDirection = enabledTrain.TrainRouteDirectionIndex;
            Train.TCSubpathRoute routePath = enabledTrain.Train.ValidRoute[actDirection];
            int actRouteIndex = routePath == null ? -1 : routePath.GetRouteIndex(enabledTrain.Train.PresentPosition[actDirection].TCSectionIndex, 0);
            if (actRouteIndex >= 0)
            {
                float offset = 0;
                if (enabledTrain.TrainRouteDirectionIndex == 0)
                    offset = enabledTrain.Train.PresentPosition[0].TCOffset;
                else
                    offset = signalRef.TrackCircuitList[enabledTrain.Train.PresentPosition[1].TCSectionIndex].Length - enabledTrain.Train.PresentPosition[1].TCOffset;
                while (!found)
                {
                    Train.TCRouteElement thisElement = routePath[actRouteIndex];
                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
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
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} has no valid path to signal, clear not allowed \n", enabledTrain.Train.Number);
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                ApproachControlSet = true;
                return (false);
            }

            // test distance

            if (Convert.ToInt32(distance) < reqPositionM)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}), clear allowed \n",
                        enabledTrain.Train.Number, distance, reqPositionM);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = false;
                ApproachControlCleared = true;
                ClaimLocked = false;
                ForcePropOnApproachControl = false;
                return (true);
            }
            else
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}), clear not allowed \n",
                        enabledTrain.Train.Number, distance, reqPositionM);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Test for approach control - position and speed
        /// </summary>

        public bool ApproachControlSpeed(int reqPositionM, int reqSpeedMpS, string dumpfile)
        {
            // no train approaching
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "APPROACH CONTROL : no train approaching");
                }

                return (false);
            }

            // signal is not first signal for train
            if (enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex] != null &&
                enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef != thisRef)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} : First signal is not this signal but {1} \n",
                        enabledTrain.Train.Number, enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }

            // if already cleared - return true

            if (ApproachControlCleared)
            {
                ApproachControlSet = false;
                ForcePropOnApproachControl = false;
                return (true);
            }

            // check if distance is valid

            if (!enabledTrain.Train.DistanceToSignal.HasValue)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} has no valid distance to signal, clear not allowed \n",
                        enabledTrain.Train.Number);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }

            // test distance

            if (Convert.ToInt32(enabledTrain.Train.DistanceToSignal.Value) < reqPositionM)
            {
                bool validSpeed = false;
                if (reqSpeedMpS > 0)
                {
                    if (Math.Abs(enabledTrain.Train.SpeedMpS) < reqSpeedMpS)
                    {
                        validSpeed = true;
                    }
                }
                else if (reqSpeedMpS == 0)
                {
                    if (Math.Abs(enabledTrain.Train.SpeedMpS) < 0.1)
                    {
                        validSpeed = true;
                    }
                }

                if (validSpeed)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}) and speed {3} (required {4}), clear allowed \n",
                            enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM, enabledTrain.Train.SpeedMpS, reqSpeedMpS);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlCleared = true;
                    ApproachControlSet = false;
                    ClaimLocked = false;
                    ForcePropOnApproachControl = false;
                    return (true);
                }
                else
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}) and speed {3} (required {4}), clear not allowed \n",
                            enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM, enabledTrain.Train.SpeedMpS, reqSpeedMpS);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlSet = true;
                    return (false);
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}), clear not allowed \n",
                        enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Test for approach control in case of APC on next STOP
        /// </summary>

        public bool ApproachControlNextStop(int reqPositionM, int reqSpeedMpS, string dumpfile)
        {
            // no train approaching
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "APPROACH CONTROL : no train approaching\n");
                }

                return (false);
            }

            // signal is not first signal for train
            if (enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex] != null &&
                enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef != thisRef)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} : First signal is not this signal but {1} \n",
                        enabledTrain.Train.Number, enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                ForcePropOnApproachControl = true;
                return (false);
            }

            // if already cleared - return true

            if (ApproachControlCleared)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "APPROACH CONTROL : cleared\n");
                }

                return (true);
            }

            // check if distance is valid

            if (!enabledTrain.Train.DistanceToSignal.HasValue)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} has no valid distance to signal, clear not allowed \n",
                        enabledTrain.Train.Number);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }

            // test distance

            if (Convert.ToInt32(enabledTrain.Train.DistanceToSignal.Value) < reqPositionM)
            {
                bool validSpeed = false;
                if (reqSpeedMpS > 0)
                {
                    if (Math.Abs(enabledTrain.Train.SpeedMpS) < reqSpeedMpS)
                    {
                        validSpeed = true;
                    }
                }
                else if (reqSpeedMpS == 0)
                {
                    if (Math.Abs(enabledTrain.Train.SpeedMpS) < 0.1)
                    {
                        validSpeed = true;
                    }
                }

                if (validSpeed)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}) and speed {3} (required {4}), clear allowed \n",
                            enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM, enabledTrain.Train.SpeedMpS, reqSpeedMpS);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlCleared = true;
                    ApproachControlSet = false;
                    ClaimLocked = false;
                    ForcePropOnApproachControl = false;
                    return (true);
                }
                else
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}) and speed {3} (required {4}), clear not allowed \n",
                            enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM, enabledTrain.Train.SpeedMpS, reqSpeedMpS);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlSet = true;
                    ForcePropOnApproachControl = true;
                    return (false);
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}), clear not allowed \n",
                        enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                ForcePropOnApproachControl = true;
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Lock claim (only if approach control is active)
        /// </summary>

        public void LockClaim()
        {
            ClaimLocked = ApproachControlSet;
        }

        //================================================================================================//
        /// <summary>
        /// Activate timing trigger
        /// </summary>

        public void ActivateTimingTrigger()
        {
            TimingTriggerValue = signalRef.Simulator.GameTime;
        }

        //================================================================================================//
        /// <summary>
        /// Check timing trigger
        /// </summary>

        public bool CheckTimingTrigger(int reqTiming, string dumpfile)
        {
            int foundDelta = (int) (signalRef.Simulator.GameTime - TimingTriggerValue);
            bool triggerExceeded = foundDelta > reqTiming;

            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("TIMING TRIGGER : found delta time : {0}; return state {1} \n", foundDelta, triggerExceeded.ToString());
                File.AppendAllText(dumpfile, sob.ToString());
            }

            return (triggerExceeded);
        }

        //================================================================================================//
        /// <summary>
        /// Test if train has call-on set
        /// </summary>

        public bool TrainHasCallOn(bool allowOnNonePlatform, bool allowAdvancedSignal, string dumpfile)
        {
            // no train approaching
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "CALL ON : no train approaching \n");
                }

                return (false);
            }

            // signal is not first signal for train
            var nextSignal = enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex];

            if (!allowAdvancedSignal &&
               nextSignal != null && nextSignal.thisRef != thisRef)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("CALL ON : Train {0} : First signal is not this signal but {1} \n",
                        enabledTrain.Train.Name, enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                return (false);
            }

            if (enabledTrain.Train != null && signalRoute != null)
            {
                bool callOnValid = enabledTrain.Train.TestCallOn(this, allowOnNonePlatform, signalRoute, dumpfile);
                return (callOnValid);
            }

            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("CALL ON : Train {0} : not valid \n", enabledTrain.Train.Name);
                File.AppendAllText(dumpfile, sob.ToString());
            }
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Test if train requires next signal
        /// </summary>

        public bool RequiresNextSignal(int nextSignalId, int reqPosition, string dumpfile)
        {
            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("REQ_NEXT_SIGNAL : check for signal {0} \n", nextSignalId);
                File.AppendAllText(dumpfile, sob.ToString());
            }

            // no enabled train
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : no enabled train \n");
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (false);
            }

            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("REQ_NEXT_SIGNAL : enabled train : {0} = {1} \n", enabledTrain.Train.Name, enabledTrain.Train.Number);
                File.AppendAllText(dumpfile, sob.ToString());
            }

            // train has no path
            Train reqTrain = enabledTrain.Train;
            if (reqTrain.ValidRoute == null || reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex] == null || reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex].Count <= 0)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : train has no valid route \n");
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (false);
            }

            // next signal is not valid
            if (nextSignalId < 0 || nextSignalId >= signalObjects.Length || !signalObjects[nextSignalId].isSignalNormal())
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : signal is not NORMAL signal \n");
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (false);
            }

            // trains present position is unknown
            if (reqTrain.PresentPosition[enabledTrain.TrainRouteDirectionIndex].RouteListIndex < 0 ||
                reqTrain.PresentPosition[enabledTrain.TrainRouteDirectionIndex].RouteListIndex >= reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex].Count)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : train has no valid position : {0} (of {1}) \n",
                        reqTrain.PresentPosition[enabledTrain.TrainRouteDirectionIndex].RouteListIndex,
                        reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex].Count);
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (false);
            }

            // check if section beyond or ahead of next signal is within trains path ahead of present position of train
            int reqSection = reqPosition == 1 ? signalObjects[nextSignalId].TCNextTC : signalObjects[nextSignalId].TCReference;

            int sectionIndex = reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex].GetRouteIndex(reqSection, reqTrain.PresentPosition[enabledTrain.TrainRouteDirectionIndex].RouteListIndex);
            if (sectionIndex > 0)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : TRUE : signal position is in route : section {0} has index {1} \n",
                        signalObjects[nextSignalId].TCNextTC, sectionIndex);
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (true);
            }

            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : signal position is not in route : section {0} has index {1} \n",
                    signalObjects[nextSignalId].TCNextTC, sectionIndex);
                File.AppendAllText(dumpfile, sob.ToString());
            }
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Get ident of signal ahead with specific details
        /// </summary>

        public int FindReqNormalSignal(int req_value, string dumpfile)
        {
            int foundSignal = -1;

            // signal not enabled - no route available
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.Append("FIND_REQ_NORMAL_SIGNAL : not found : signal is not enabled");
                    File.AppendAllText(dumpfile, sob.ToString());
                }
            }
            else
            {
                int startIndex = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex].GetRouteIndex(TCNextTC, enabledTrain.Train.PresentPosition[0].RouteListIndex);
                if (startIndex < 0)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("FIND_REQ_NORMAL_SIGNAL : not found : cannot find signal {0} at section {1} in path of train {2}\n", thisRef, TCNextTC, enabledTrain.Train.Name);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }
                }
                else
                {
                    for (int iRouteIndex = startIndex; iRouteIndex < enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex].Count; iRouteIndex++)
                    {
                        Train.TCRouteElement thisElement = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex][iRouteIndex];
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
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
                                    if (!String.IsNullOrEmpty(dumpfile))
                                    {
                                        var sob = new StringBuilder();
                                        sob.AppendFormat("FIND_REQ_NORMAL_SIGNAL : signal found : {0} : head : {1} : state : {2} \n", endSignal.thisRef, thisHead.TDBIndex, found_value);
                                    }
                                    break;
                                }
                            }

                            if (found_value)
                            {
                                foundSignal = endSignal.thisRef;
                                if (!String.IsNullOrEmpty(dumpfile))
                                {
                                    var sob = new StringBuilder();
                                    sob.AppendFormat("FIND_REQ_NORMAL_SIGNAL : signal found : {0} : ( ", endSignal.thisRef);

                                    foreach (SignalHead otherHead in endSignal.SignalHeads)
                                    {
                                        sob.AppendFormat(" {0} ", otherHead.TDBIndex);
                                    }

                                    sob.AppendFormat(") \n");
                                    File.AppendAllText(dumpfile, sob.ToString());
                                }
                                break;
                            }
                            else
                            {
                                if (!String.IsNullOrEmpty(dumpfile))
                                {
                                    var sob = new StringBuilder();
                                    sob.AppendFormat("FIND_REQ_NORMAL_SIGNAL : signal found : {0} : ( ", endSignal.thisRef);

                                    foreach (SignalHead otherHead in endSignal.SignalHeads)
                                    {
                                        sob.AppendFormat(" {0} ", otherHead.TDBIndex);
                                    }

                                    sob.AppendFormat(") ");
                                    sob.AppendFormat("incorrect variable value : {0} \n", found_value);
                                    File.AppendAllText(dumpfile, sob.ToString());
                                }
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

        public SignalBlockState RouteClearedToSignal(int req_signalid, bool allowCallOn, string dumpfile)
        {
            SignalBlockState routeState = SignalBlockState.Jn_Obstructed;
            if (enabledTrain != null && enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex] != null && req_signalid >= 0 && req_signalid < signalRef.SignalObjects.Length)
            {
                Signal otherSignal = signalRef.SignalObjects[req_signalid];

                TrackCircuitSection reqSection = null;
                reqSection = signalRef.TrackCircuitList[otherSignal.TCReference];
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : signal checked : {0} , section [ahead] found : {1} \n", req_signalid, reqSection.Index);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                Train.TCSubpathRoute trainRoute = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex];

                int thisRouteIndex = trainRoute.GetRouteIndex(isSignalNormal() ? TCNextTC : TCReference, 0);
                int otherRouteIndex = trainRoute.GetRouteIndex(otherSignal.TCReference, thisRouteIndex);

                if (otherRouteIndex < 0)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : section found is not in this trains route \n");
                        File.AppendAllText(dumpfile, sob.ToString());
                    }
                }

                // extract route
                else
                {
                    bool routeCleared = true;
                    Train.TCSubpathRoute reqPath = new Train.TCSubpathRoute(trainRoute, thisRouteIndex, otherRouteIndex);

                    for (int iIndex = 0; iIndex < reqPath.Count && routeCleared; iIndex++)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[reqPath[iIndex].TCSectionIndex];
                        if (!thisSection.IsSet(enabledTrain, false))
                        {
                            routeCleared = false;
                            if (!String.IsNullOrEmpty(dumpfile))
                            {
                                var sob = new StringBuilder();
                                sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : section {0} is not set for required train \n", thisSection.Index);
                                File.AppendAllText(dumpfile, sob.ToString());
                            }
                        }
                    }

                    if (routeCleared)
                    {
                        routeState = SignalBlockState.Clear;
                        if (!String.IsNullOrEmpty(dumpfile))
                        {
                            var sob = new StringBuilder();
                            sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : all sections set \n");
                            File.AppendAllText(dumpfile, sob.ToString());
                        }
                    }
                    else if (allowCallOn)
                    {
                        if (enabledTrain.Train.TestCallOn(this, false, reqPath, dumpfile))
                        {
                            routeCleared = true;
                            routeState = SignalBlockState.Occupied;
                            if (!String.IsNullOrEmpty(dumpfile))
                            {
                                var sob = new StringBuilder();
                                sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : callon allowed \n");
                                File.AppendAllText(dumpfile, sob.ToString());
                            }
                        }
                    }

                    if (!routeCleared)
                    {
                        routeState = SignalBlockState.Jn_Obstructed;
                        if (!String.IsNullOrEmpty(dumpfile))
                        {
                            var sob = new StringBuilder();
                            sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : route not available \n");
                            File.AppendAllText(dumpfile, sob.ToString());
                        }
                    }
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : found state : invalid request (no enabled train or invalid signalident) \n");
                    File.AppendAllText(dumpfile, sob.ToString());
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
            LockedTrains.Add(newLock);
            return false;
        }

        public bool UnlockForTrain(int trainNumber, int subpath = 0)
        {
            bool info = LockedTrains.Remove(LockedTrains.First(item => item.Key.Equals(trainNumber) && item.Value.Equals(subpath)));
            return info;
        }

        public bool HasLockForTrain(int trainNumber, int subpath = 0)
        {
            bool info = (LockedTrains.Count > 0 && LockedTrains.Exists(item => item.Key.Equals(trainNumber) && item.Value.Equals(subpath)));
            return info;
        }

        public bool CleanAllLock(int trainNumber)
        {
            int info = LockedTrains.RemoveAll(item => item.Key.Equals(trainNumber));
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
                Trace.TraceInformation("Signal {0} (TDB {1}) has no heads", thisRef, SignalHeads[0].TDBIndex);
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

            if (enabledTrain == null || enabledTrain.Train == null)
            {
                holdState = SignalHoldState.ManualLock;
                if (thisAspect > SignalAspectState.Stop) ResetSignal(true);
                returnValue[0] = true;
            }

            // if enabled, cleared and reset not requested : no action

            else if (!requestResetSignal && thisAspect > SignalAspectState.Stop)
            {
                holdState = SignalHoldState.ManualLock; //just in case this one later will be set to green by the system
                returnValue[0] = true;
            }

            // if enabled and not cleared : set hold, no reset required

            else if (thisAspect == SignalAspectState.Stop)
            {
                holdState = SignalHoldState.ManualLock;
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
                int signalRouteIndex = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex].GetRouteIndex(TCNextTC, 0);
                if (signalRouteIndex >= 0)
                {
                    signalRef.BreakDownRouteList(enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex], signalRouteIndex, enabledTrain);
                    ResetSignal(true);
                    holdState = SignalHoldState.ManualLock;
                    returnValue[0] = true;
                    returnValue[1] = true;
                }
                else //hopefully this does not happen
                {
                    holdState = SignalHoldState.ManualLock;
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
            holdState = SignalHoldState.None;
        }

    }  // SignalObject

}
