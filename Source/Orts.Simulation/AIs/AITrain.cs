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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Models.State;
using Orts.Simulation.Activities;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Multiplayer.Messaging;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;
using Orts.Simulation.Signalling;
using Orts.Simulation.Timetables;
using Orts.Simulation.Track;

namespace Orts.Simulation.AIs
{
    public class AITrain : Train
    {
        internal AIPath Path { get; set; }

        public float MaxDecelMpSSP = 1.0f;               // maximum decelleration
        public float MaxAccelMpSSP = 1.0f;               // maximum accelleration
        public float MaxDecelMpSSF = 0.8f;               // maximum decelleration
        public float MaxAccelMpSSF = 0.5f;               // maximum accelleration
        public float MaxDecelMpSS = 0.5f;                // maximum decelleration
        public float MaxAccelMpSS = 1.0f;                // maximum accelleration
        public float Efficiency = 1.0f;                  // train efficiency
        public float LastSpeedMpS;                       // previous speed
        public int Alpha10 = 10;                         // 10*alpha

        public bool PreUpdate;                           // pre update state
        internal AIActionItem nextActionInfo;              // no next action
        internal AIActionItem nextGenAction;               // Can't remove GenAction if already active but we need to manage the normal Action, so
        public float NextStopDistanceM;                  // distance to next stop node
        public int? StartTime;                           // starting time
        public bool PowerState = true;                   // actual power state : true if power in on
        public float MaxVelocityA = 30.0f;               // max velocity as set in .con file
        public Services ServiceDefinition; // train's service definition in .act file
        public bool UncondAttach;                       // if false it states that train will unconditionally attach to a train on its path

        public float doorOpenDelay = -1f;
        public float doorCloseAdvance = -1f;
        public AILevelCrossingHornPattern LevelCrossingHornPattern { get; set; }

        public float PathLength;


        public AiMovementState MovementState { get; set; } = AiMovementState.Init;  // actual movement state

        public AI AI;

        //  SPA:    Add public in order to be able to get these infos in new AIActionItems
        protected const float KeepDistanceStatTrainPassenger = 10.0f;  // stay 10m behind stationary train (pass in station)
        protected const float KeepDistanceStatTrainFreight = 50.0f;  // stay 50m behind stationary train (freight or pass outside station)
        protected const float FollowDistanceStatTrain = 30.0f;  // min dist for starting to follow
        protected const float KeepDistanceMovingTrain = 300.0f; // stay 300m behind moving train
        protected const float PresetCreepSpeed = 2.5f;              // speed for creeping up behind train or upto signal
        protected const float PresetCouplingSpeed = 0.4f;           // speed for coupling to other train
        protected const float MaxFollowSpeed = 15.0f;         // max. speed when following
        protected const float PresetMovingtableSpeed = 2.5f;        // speed for moving tables (approx. max 8 kph)
        protected const float SpeedHysteris = 0.5f;                // speed hysteris value to avoid instability
        internal const float ClearingDistance = 30.0f;         // clear distance to stopping point
        internal const float MinStopDistance = 3.0f;           // minimum clear distance for stopping at signal in station
        internal const float SignalApproachDistance = 20.0f;   // final approach to signal

        /// <summary>
        /// Constructor
        /// </summary>
        public AITrain(Services sd, AI ai, AIPath path, float efficiency, string name, ServiceTraffics trafficService, float maxVelocityA)
            : base()
        {
            ServiceDefinition = sd;
            AI = ai;
            Path = path;
            TrainType = TrainType.AiNotStarted;
            StartTime = ServiceDefinition.Time;
            Efficiency = efficiency;
            if (simulator.Settings.ActRandomizationLevel > 0 && simulator.ActivityRun != null) // randomize efficiency
            {
                RandomizeEfficiency(ref Efficiency);
            }
            Name = name;
            base.trafficService = trafficService;
            MaxVelocityA = maxVelocityA;
            // <CSComment> TODO: as Cars.Count is always = 0 at this point, activityClearingDistanceM is set to the short distance also for long trains
            // However as no one complained about AI train SPADs it may be considered to consolidate short distance for all trains</CSComment>
            if (Cars.Count < StandardTrainMinCarNo)
                ActivityClearingDistanceM = ShortClearingDistanceM;
        }

        public AITrain()
            : base()
        {
            TrainType = TrainType.AiNotStarted;
            AI = simulator.AI;
        }

        /// <summary>
        /// convert route and build station list
        /// </summary>
        public void CreateRoute(bool usePosition)
        {
            if (Path != null)
            {
                SetRoutePath(Path, usePosition);
            }
            else if (usePosition)
            {
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];

                ValidRoutes[Direction.Forward] = SignalEnvironment.BuildTempRoute(this, thisSection.Index, PresentPosition[Direction.Backward].Offset, PresentPosition[Direction.Backward].Direction, Length, true, true, false);
            }
        }

        public override async ValueTask<TrainSaveState> Snapshot()
        {
            TrainSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.AiTrainSaveState = new AiTrainSaveState()
            {
                PlayerLocomotiveIndex = Cars.IndexOf(simulator.PlayerLocomotive),
                StartTime = StartTime,
                MaxAcceleration = MaxAccelMpSS,
                MaxDeceleration = MaxDecelMpSS,
                PowerState = PowerState,
                Alpha10 = Alpha10,

                MovementState = MovementState,
                Efficiency = Efficiency,
                MaxVelocity = MaxVelocityA,
                UnconditionalAttach = UncondAttach,
                DoorsCloseAdvance = doorCloseAdvance,
                DoorsOpenDelay = doorOpenDelay,
                LevelCrossingHornPattern = AILevelCrossingHornPattern.LevelCrossingHornPatternType(LevelCrossingHornPattern),
                TrafficItemSaveStates = ServiceDefinition == null ? null : await ServiceDefinition.SnapshotCollection<TrafficItemSaveState, TrafficItem>().ConfigureAwait(false),
            };
            return saveState;
        }

        public override async ValueTask Restore([NotNull] TrainSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(saveState.AiTrainSaveState, nameof(saveState.AiTrainSaveState));

            AiTrainSaveState aiTrainSaveState = saveState.AiTrainSaveState;
            ColdStart = false;
            MaxDecelMpSS = aiTrainSaveState.MaxDeceleration;
            MaxAccelMpSS = aiTrainSaveState.MaxAcceleration;

            if (Cars.Count < StandardTrainMinCarNo)
                ActivityClearingDistanceM = ShortClearingDistanceM;

            StartTime = aiTrainSaveState.StartTime;

            PowerState = aiTrainSaveState.PowerState;
            Alpha10 = aiTrainSaveState.Alpha10;

            MovementState = aiTrainSaveState.MovementState;
            if (MovementState == AiMovementState.InitAction || MovementState == AiMovementState.HandleAction)
                MovementState = AiMovementState.Braking;

            Efficiency = aiTrainSaveState.Efficiency;
            MaxVelocityA = aiTrainSaveState.MaxVelocity;
            UncondAttach = aiTrainSaveState.UnconditionalAttach;
            doorCloseAdvance = aiTrainSaveState.DoorsCloseAdvance;
            doorOpenDelay = aiTrainSaveState.DoorsOpenDelay;
            if (!simulator.TimetableMode && doorOpenDelay <= 0 && doorCloseAdvance > 0 && simulator.OpenDoorsInAITrains &&
                MovementState == AiMovementState.StationStop && StationStops.Count > 0)
            {
                StationStop stationStop = StationStops[0];
                bool frontIsFront = stationStop.PlatformReference == stationStop.PlatformItem.PlatformFrontUiD;
                if ((stationStop.PlatformItem.PlatformSide & PlatformDetails.PlatformSides.Left) == PlatformDetails.PlatformSides.Left)
                {
                    //open left doors
                    SetDoors(frontIsFront ? DoorSide.Right : DoorSide.Left, true);
                }
                if ((stationStop.PlatformItem.PlatformSide & PlatformDetails.PlatformSides.Right) == PlatformDetails.PlatformSides.Right)
                {
                    //open right doors
                    SetDoors(frontIsFront ? DoorSide.Left : DoorSide.Right, true);
                }
            }
            LevelCrossingHornPattern = AILevelCrossingHornPattern.CreateInstance(aiTrainSaveState.LevelCrossingHornPattern);
            ServiceDefinition = new Services();
            await ServiceDefinition.RestoreCollectionCreateNewInstances(aiTrainSaveState.TrafficItemSaveStates).ConfigureAwait(false);

            // set signals and actions if train is active train
            bool activeTrain = (TrainType != TrainType.AiNotStarted && TrainType != TrainType.AiAutoGenerated && TrainType != TrainType.AiIncorporated && MovementState != AiMovementState.Static && MovementState != AiMovementState.Init);

            if (activeTrain)
            {
                InitializeSignals(true);
                ResetActions(true);
                CheckSignalObjects();
                if (MovementState != AiMovementState.Suspended)
                    ObtainRequiredActions(0);
            }
            // associate location events
            simulator.ActivityRun?.AssociateEvents(this);
            LastSpeedMpS = SpeedMpS;

            if ( aiTrainSaveState.PlayerLocomotiveIndex >= 0)
                simulator.PlayerLocomotive = Cars[aiTrainSaveState.PlayerLocomotiveIndex] as MSTSLocomotive ?? throw new InvalidCastException(nameof(simulator.PlayerLocomotive));
        }

        //================================================================================================//
        /// <summary>
        /// Set starting conditions when speed > 0 
        /// </summary>
        /// 

        public override void InitializeMoving() // TODO
        {
            {
                ColdStart = false;
                if (TrainType == TrainType.AiPlayerDriven)
                {
                    base.InitializeMoving();
                    return;
                }
                SpeedMpS = InitialSpeed;
                MUDirection = MidpointDirection.Forward;
                float initialThrottlepercent = InitialThrottlepercent;
                MUDynamicBrakePercent = -1;
                AITrainBrakePercent = 0;

                FirstCar.CurrentElevationPercent = -100f * FirstCar.WorldPosition.XNAMatrix.M32;
                // give it a bit more gas if it is uphill
                if (FirstCar.CurrentElevationPercent > 2.0)
                    initialThrottlepercent = 40f;
                // better block gas if it is downhill
                else if (FirstCar.CurrentElevationPercent < -1.0)
                    initialThrottlepercent = 0f;
                AdjustControlsBrakeOff();
                AITrainThrottlePercent = initialThrottlepercent;

                TraincarsInitializeMoving();
                LastSpeedMpS = SpeedMpS;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Post Init (override from Train)
        /// perform all actions required to start
        /// </summary>

        internal override bool PostInit()
        {
            // check deadlocks; do it after placing for player train, like done for it when autopilot option unchecked

            if (!IsActualPlayerTrain)
                TrainDeadlockInfo.CheckDeadlock(ValidRoutes[Direction.Forward], Number);

            // Set up horn blow at crossings if required
            LevelCrossingHornPattern = Simulator.Instance.ActivityFile.Activity.AIBlowsHornAtLevelCrossings ? AILevelCrossingHornPattern.CreateInstance(Simulator.Instance.ActivityFile.Activity.AILevelCrossingHornPattern) : null;

            // set initial position and state

            bool atStation = false;
            bool validPosition = InitialTrainPlacement();     // Check track and if clear, set occupied

            if (validPosition)
            {
                if (IsFreight)
                {
                    MaxAccelMpSS = MaxAccelMpSSF;  // set freigth accel and decel
                    MaxDecelMpSS = MaxAccelMpSSF;
                }
                else
                {
                    MaxAccelMpSS = MaxAccelMpSSP;  // set passenger accel and decel
                    MaxDecelMpSS = MaxDecelMpSSP;
                    if (TrainMaxSpeedMpS > 55.0f)
                    {
                        MaxDecelMpSS = 2.5f * MaxDecelMpSSP;  // higher decel for very high speed trains
                    }
                    else if (TrainMaxSpeedMpS > 40.0f)
                    {
                        MaxDecelMpSS = 1.5f * MaxDecelMpSSP;  // higher decel for high speed trains

                    }
                    else
                    {
                        if (Cars[0] is MSTSElectricLocomotive && Cars[0].PassengerCapacity > 0
                            && Cars[^1] is MSTSElectricLocomotive && Cars[^1].PassengerCapacity > 0)  // EMU or DMU train, higher decel
                        {
                            MaxAccelMpSS = 1.5f * MaxAccelMpSS;
                            MaxDecelMpSS = 2f * MaxDecelMpSSP;
                        }
                    }

                }

                BuildWaitingPointList(ActivityClearingDistanceM);
                BuildStationList(ActivityClearingDistanceM);

                // <CSComment> This creates problems in push-pull paths </CSComment>
                //                StationStops.Sort();
                if (!atStation && StationStops.Count > 0 && this != simulator.Trains[0])
                {
                    if (MaxVelocityA > 0 &&
                        ServiceDefinition != null && ServiceDefinition.Count > 0)
                    {
                        // <CScomment> gets efficiency from .act file to override TrainMaxSpeedMpS computed from .srv efficiency
                        var sectionEfficiency = ServiceDefinition[0].Efficiency;
                        if (simulator.Settings.ActRandomizationLevel > 0)
                            RandomizeEfficiency(ref sectionEfficiency);
                        if (sectionEfficiency > 0)
                            TrainMaxSpeedMpS = Math.Min((float)simulator.Route.SpeedLimit, MaxVelocityA * sectionEfficiency);
                    }
                }

                InitializeSignals(false);           // Get signal information
                if (IsActualPlayerTrain)
                    TrainDeadlockInfo.CheckDeadlock(ValidRoutes[Direction.Forward], Number);
                TCRoute.SetReversalOffset(Length, false);  // set reversal information for first subpath
                SetEndOfRouteAction();              // set action to ensure train stops at end of route

                // check if train starts at station stop
                AuxActionsContainer.SetAuxAction(this);
                if (StationStops.Count > 0)
                {
                    atStation = CheckInitialStation();
                }

                if (!atStation)
                {
                    if (StationStops.Count > 0)
                    {
                        SetNextStationAction();               // set station details
                    }

                    if (TrainHasPower())
                    {
                        MovementState = AiMovementState.Init;   // start in STOPPED mode to collect info
                    }
                }
            }

            if (IsActualPlayerTrain)
                SetTrainSpeedLoggingFlag();
            return (validPosition);
        }

        //================================================================================================//
        /// <summary>
        /// Check initial station
        /// </summary>

        public virtual bool CheckInitialStation()
        {
            bool atStation = false;

            // get station details

            StationStop thisStation = StationStops[0];
            if (thisStation.SubrouteIndex != TCRoute.ActiveSubPath)
            {
                return (false);
            }

            if (thisStation.StopType != StationStopType.Station)
            {
                return (false);
            }

            atStation = CheckStationPosition(thisStation.PlatformItem, thisStation.Direction, thisStation.TrackCircuitSectionIndex);

            // At station : set state, create action item

            if (atStation)
            {
                thisStation.ActualArrival = -1;
                thisStation.ActualDepart = -1;
                MovementState = AiMovementState.StationStop;

                AIActionItem newAction = new AIActionItem(null, AiActionType.StationStop);
                newAction.SetParam(-10f, 0.0f, 0.0f, 0.0f);
                nextActionInfo = newAction;
                NextStopDistanceM = 0.0f;
            }

            return (atStation);
        }

        //================================================================================================//
        /// <summary>
        /// Get AI Movement State
        /// </summary>

        internal override AiMovementState GetAiMovementState()
        {
            return (ControlMode == TrainControlMode.Inactive ? AiMovementState.Static : MovementState);
        }


        //================================================================================================//
        /// <summary>
        /// Get AI Movement State
        /// </summary>
        /// 
        private void RandomizeEfficiency(ref float efficiency)
        {
            efficiency *= 100;
            var incOrDecEfficiency = DateTime.UtcNow.Millisecond % 2 == 0 ? true : false;
            if (incOrDecEfficiency)
                efficiency = Math.Min(100, efficiency + RandomizedDelayWithThreshold(20)); // increment it
            else if (efficiency > 50)
                efficiency = Math.Max(50, efficiency - RandomizedDelayWithThreshold(20)); // decrement it
            efficiency /= 100;
        }

        //================================================================================================//
        /// <summary>
        /// Update
        /// Update function for a single AI train.
        /// </summary>

        public void AIUpdate(double elapsedClockSeconds, double clockTime, bool preUpdate)
        {
            PreUpdate = preUpdate;   // flag for pre-update phase

            if (TrainType == TrainType.AiIncorporated || TrainType == TrainType.Static || MovementState == AiMovementState.Suspended || MovementState == AiMovementState.Frozen)
                return;
            // Check if at stop point and stopped.
            //          if ((NextStopDistanceM < actClearance) || (SpeedMpS <= 0 && MovementState == AI_MOVEMENT_STATE.STOPPED))
            // <CSComment> TODO: next if block is in effect only a workaround due to OR braking physics not working well with AI trains
            if (MovementState == AiMovementState.Stopped || MovementState == AiMovementState.StationStop || MovementState == AiMovementState.Static ||
                MovementState == AiMovementState.InitAction || MovementState == AiMovementState.HandleAction)
            {
                SpeedMpS = 0;
                foreach (TrainCar car in Cars)
                {
                    car.MotiveForceN = 0;
                    car.TotalForceN = 0;
                    car.SpeedMpS = 0;
                }

                AITrainThrottlePercent = 0;
                AITrainBrakePercent = 100;
            }

            // update position, route clearance and objects

            if (MovementState == AiMovementState.Static)
            {
                CalculatePositionOfCars(0, 0);   //required to make train visible ; set elapsed time to zero to avoid actual movement
            }
            else
            {
                if (!preUpdate)
                {
                    Update(elapsedClockSeconds, false);
                }
                else
                {
                    AIPreUpdate(elapsedClockSeconds);
                }

                // get through list of objects, determine necesarry actions

                CheckSignalObjects();

                // check if state still matches authority level

                if (MovementState != AiMovementState.Init && ControlMode == TrainControlMode.AutoNode && EndAuthorities[Direction.Forward].EndAuthorityType != EndAuthorityType.MaxDistance) // restricted authority
                {
                    CheckRequiredAction();
                }

                // check if reversal point reached and not yet activated - but station stop has preference over reversal point
                SetReversalAction();

                // check if out of control - if so, remove

                if (ControlMode == TrainControlMode.OutOfControl && TrainType != TrainType.AiPlayerHosting)
                {
                    Trace.TraceInformation("Train {0} ({1}) is removed for out of control, reason : {2}", Name, Number, OutOfControlReason.ToString());
                    RemoveTrain();
                    return;
                }

                // set wipers on or off
                if (Cars[0] is MSTSLocomotive leadingLoco)
                {
                    bool rainingOrSnowing = simulator.Weather.PrecipitationIntensity > 0;
                    if (leadingLoco.Wiper && !rainingOrSnowing)
                        leadingLoco.SignalEvent(TrainEvent.WiperOff);
                    else if (!leadingLoco.Wiper && rainingOrSnowing)
                        leadingLoco.SignalEvent(TrainEvent.WiperOn);
                }
            }

            // switch on action depending on state

            int presentTime = Convert.ToInt32(Math.Floor(clockTime));

            bool[] stillExist;

            AuxActionsContainer.ProcessGenAction(this, presentTime, elapsedClockSeconds, MovementState);
            MovementState = AuxActionsContainer.ProcessSpecAction(this, presentTime, elapsedClockSeconds, MovementState);

            switch (MovementState)
            {
                case AiMovementState.Static:
                    UpdateAIStaticState(presentTime);
                    break;
                case AiMovementState.Stopped:
                    if (nextActionInfo != null && nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem)))
                    {
                        MovementState = nextActionInfo.ProcessAction(this, presentTime, elapsedClockSeconds, MovementState);
                    }
                    else
                    {
                        stillExist = ProcessEndOfPath(presentTime, false);
                        if (stillExist[1])
                        {
                            if (nextActionInfo != null && nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem)))
                            {
                                MovementState = nextActionInfo.ProcessAction(this, presentTime, elapsedClockSeconds, MovementState);
                            }
                            else if (MovementState == AiMovementState.Stopped) // process only if moving state has not changed
                            {
                                UpdateStoppedState(elapsedClockSeconds);
                            }
                        }
                    }
                    break;
                case AiMovementState.Init:
                    stillExist = ProcessEndOfPath(presentTime);
                    if (stillExist[1])
                        UpdateStoppedState(elapsedClockSeconds);
                    break;
                case AiMovementState.Turntable:
                    UpdateTurntableState(elapsedClockSeconds, presentTime);
                    break;
                case AiMovementState.StationStop:
                    UpdateStationState(elapsedClockSeconds, presentTime);
                    break;
                case AiMovementState.Braking:
                    UpdateBrakingState(elapsedClockSeconds, presentTime);
                    break;
                case AiMovementState.ApproachingEndOfPath:
                    UpdateBrakingState(elapsedClockSeconds, presentTime);
                    break;
                case AiMovementState.Accelerating:
                    UpdateAccelState(elapsedClockSeconds);
                    break;
                case AiMovementState.Following:
                    UpdateFollowingState(elapsedClockSeconds, presentTime);
                    break;
                case AiMovementState.Running:
                    UpdateRunningState(elapsedClockSeconds);
                    break;
                case AiMovementState.StoppedExisting:
                    UpdateStoppedState(elapsedClockSeconds);
                    break;
                default:
                    if (nextActionInfo != null && nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem)))
                    {
                        MovementState = nextActionInfo.ProcessAction(this, presentTime, elapsedClockSeconds, MovementState);
                    }
                    break;

            }
            LastSpeedMpS = SpeedMpS;
            //            Trace.TraceWarning ("Time {0} Train no. {1} Speed {2} AllowedMaxSpeed {3} Throttle percent {4} Distance travelled {5} Movement State {6} BrakePerCent {7}",
            //               clockTime, Number, SpeedMpS, AllowedMaxSpeedMpS, AITrainThrottlePercent, DistanceTravelledM, MovementState, AITrainBrakePercent);
        }

        //================================================================================================//
        /// <summary>
        /// Update for pre-update state
        /// </summary>

        public virtual void AIPreUpdate(double elapsedClockSeconds)
        {

            // calculate delta speed and speed

            double deltaSpeedMpS = (0.01 * AITrainThrottlePercent * MaxAccelMpSS - 0.01 * AITrainBrakePercent * MaxDecelMpSS) *
                Efficiency * elapsedClockSeconds;
            if (AITrainBrakePercent > 0 && deltaSpeedMpS < 0 && Math.Abs(deltaSpeedMpS) > SpeedMpS)
            {
                deltaSpeedMpS = -SpeedMpS;
            }
            SpeedMpS = (float)Math.Min(TrainMaxSpeedMpS, Math.Max(0.0, SpeedMpS + deltaSpeedMpS));

            // calculate position

            double distanceM = SpeedMpS * elapsedClockSeconds;

            if (double.IsNaN(distanceM))
                distanceM = 0;//sometimes it may become NaN, force it to be 0, so no move

            // force stop
            if (distanceM > NextStopDistanceM)
            {
                //                Trace.TraceWarning("Forced stop for train {0} ({1}) at speed {2}", Number, Name, SpeedMpS);

                distanceM = Math.Max(0.0f, NextStopDistanceM);
                SpeedMpS = 0;
            }

            // set speed and position

            foreach (TrainCar car in Cars)
            {
                if (car.Flipped)
                {
                    car.SpeedMpS = -SpeedMpS;
                }
                else
                {
                    car.SpeedMpS = SpeedMpS;
                }
            }

            CalculatePositionOfCars(elapsedClockSeconds, (float)distanceM);

            DistanceTravelledM += (float)distanceM;

            // perform overall update

            if (ValidRoutes != null)     // no actions required for static objects //
            {
                movedBackward = CheckBackwardClearance();                                       // check clearance at rear //
                UpdateTrainPosition();                                                          // position update         //              
                UpdateTrainPositionInformation();                                               // position linked info    //
                int SignalObjIndex = CheckSignalPassed(0, PresentPosition[Direction.Forward], PreviousPosition[Direction.Forward]);    // check if passed signal  //
                UpdateSectionState(movedBackward);                                              // update track occupation //
                ObtainRequiredActions(movedBackward);                                           // process Actions         //
                UpdateRouteClearanceAhead(SignalObjIndex, movedBackward, elapsedClockSeconds);  // update route clearance  //
                UpdateSignalState(movedBackward);                                               // update signal state     //
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set reversal point action
        /// </summary>

        public virtual void SetReversalAction()
        {
            if ((nextActionInfo == null ||
                 (nextActionInfo.NextAction != AiActionType.StationStop && nextActionInfo.NextAction != AiActionType.Reversal)) &&
                 TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
            {
                int reqSection = TCRoute.ReversalInfo[TCRoute.ActiveSubPath].SignalUsed ?
                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex :
                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex;

                if (reqSection >= 0 && PresentPosition[Direction.Backward].RouteListIndex >= reqSection && TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalActionInserted == false)
                {
                    float reqDistance = SpeedMpS * SpeedMpS * MaxDecelMpSS;
                    float distanceToReversalPoint = 0;
                    reqDistance = nextActionInfo != null ? Math.Min(nextActionInfo.RequiredDistance, reqDistance) : reqDistance;


                    distanceToReversalPoint = ComputeDistanceToReversalPoint();
                    // <CSComment: the AI train runs up to the reverse point no matter how far it is from the diverging point.

                    CreateTrainAction(TrainMaxSpeedMpS, 0.0f, distanceToReversalPoint, null, AiActionType.Reversal);
                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalActionInserted = true;

                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// change in authority state - check action
        /// </summary>

        public virtual void CheckRequiredAction()
        {
            // check if train ahead
            if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.TrainAhead)
            {
                if (MovementState != AiMovementState.StationStop && MovementState != AiMovementState.Stopped)
                {
                    if (MovementState != AiMovementState.InitAction && MovementState != AiMovementState.HandleAction)
                    {
                        MovementState = AiMovementState.Following;  // start following
                    }
                }
            }
            else if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.ReservedSwitch || EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.Loop)
            {
                if (MovementState != AiMovementState.InitAction && MovementState != AiMovementState.HandleAction &&
                     (nextActionInfo == null || nextActionInfo.NextAction != AiActionType.EndOfAuthority))
                {
                    ResetActions(true);
                    NextStopDistanceM = EndAuthorities[Direction.Forward].Distance - 2.0f * JunctionOverlapM;
                    CreateTrainAction(SpeedMpS, 0.0f, NextStopDistanceM, null,
                                AiActionType.EndOfAuthority);
                    ObtainRequiredActions(0);
                }
            }
            // first handle outstanding actions
            else if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.EndOfPath &&
                (nextActionInfo == null || nextActionInfo.NextAction == AiActionType.EndOfRoute))
            {
                ResetActions(false);
                if (TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1)
                    NextStopDistanceM = EndAuthorities[Direction.Forward].Distance - ActivityClearingDistanceM;
                else
                    NextStopDistanceM = ComputeDistanceToReversalPoint() - ActivityClearingDistanceM;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check all signal objects
        /// </summary>

        public void CheckSignalObjects()
        {
            float validSpeed = AllowedMaxSpeedMpS;
            List<SignalItemInfo> processedList = new List<SignalItemInfo>();

            foreach (SignalItemInfo thisInfo in SignalObjectItems.Where(item => !item.SpeedInfo.SpeedWarning))
            {

                // check speedlimit
                float setSpeed = IsFreight ? thisInfo.SpeedInfo.FreightSpeed : thisInfo.SpeedInfo.PassengerSpeed;
                if (setSpeed < validSpeed && setSpeed < AllowedMaxSpeedMpS && setSpeed > 0)
                {
                    if (!thisInfo.Processed)
                    {
                        bool process_req = true;

                        if (ControlMode == TrainControlMode.AutoNode &&
                                        thisInfo.DistanceToTrain > EndAuthorities[Direction.Forward].Distance)
                        {
                            process_req = false;
                        }
                        else if (thisInfo.DistanceToTrain > SignalApproachDistance ||
                                 (MovementState == AiMovementState.Running && SpeedMpS > setSpeed) ||
                                  MovementState == AiMovementState.Accelerating)
                        {
                            process_req = true;
                        }
                        else
                        {
                            process_req = false;
                        }

                        if (process_req)
                        {
                            if (thisInfo.ItemType == SignalItemType.SpeedLimit)
                            {
                                CreateTrainAction(validSpeed, setSpeed,
                                        thisInfo.DistanceToTrain, thisInfo, AiActionType.SpeedLimit);
                            }
                            else
                            {
                                CreateTrainAction(validSpeed, setSpeed,
                                        thisInfo.DistanceToTrain, thisInfo, AiActionType.SpeedSignal);
                            }
                            processedList.Add(thisInfo);
                        }
                    }
                    validSpeed = setSpeed;
                }
                else if (setSpeed > 0)
                {
                    validSpeed = setSpeed;
                }

                // check signal state

                if (thisInfo.ItemType == SignalItemType.Signal &&
                        thisInfo.SignalState < SignalAspectState.Approach_1 &&
                        !thisInfo.Processed && thisInfo.SignalDetails.OverridePermission != SignalPermission.Granted)
                {
                    if (!(ControlMode == TrainControlMode.AutoNode &&
                                    thisInfo.DistanceToTrain > (EndAuthorities[Direction.Forward].Distance - ClearingDistance)))
                    {
                        if (thisInfo.SignalState == SignalAspectState.Stop ||
                            thisInfo.SignalDetails.EnabledTrain != RoutedForward)
                        {
                            CreateTrainAction(validSpeed, 0.0f,
                                    thisInfo.DistanceToTrain, thisInfo,
                                    AiActionType.SignalAspectStop);
                            processedList.Add(thisInfo);
                            var validClearingDistanceM = simulator.TimetableMode ? ClearingDistance : ActivityClearingDistanceM;
                            if (((thisInfo.DistanceToTrain - validClearingDistanceM) < validClearingDistanceM) &&
                                         (SpeedMpS > 0.0f || MovementState == AiMovementState.Accelerating))
                            {
                                AITrainBrakePercent = 100;
                                AITrainThrottlePercent = 0;
                                NextStopDistanceM = validClearingDistanceM;
                                if (PreUpdate && !simulator.TimetableMode)
                                    ObtainRequiredActions(movedBackward); // fast track to stop train; else a precious update is lost
                            }
                        }
                        else if (thisInfo.DistanceToTrain > 2.0f * SignalApproachDistance) // set restricted only if not close
                        {
                            if (!thisInfo.SignalDetails.SignalNoSpeedReduction(SignalFunction.Normal))
                            {
                                CreateTrainAction(validSpeed, 0.0f,
                                        thisInfo.DistanceToTrain, thisInfo,
                                        AiActionType.SignalAspectRestricted);
                            }
                            processedList.Add(thisInfo);
                        }
                    }
                }
            }

            // set processed items - must be collected as item can be processed twice (speed and signal)

            foreach (SignalItemInfo thisInfo in processedList)
            {
                thisInfo.Processed = true;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check for next station
        /// </summary>

        public virtual void SetNextStationAction(bool fromAutopilotSwitch = false)
        {
            // if train is player driven and is at station, do nothing
            if (TrainType == TrainType.AiPlayerDriven && this == simulator.OriginalPlayerTrain && simulator.ActivityRun.ActivityTask is ActivityTaskPassengerStopAt && TrainAtStation())
                return;

            // check if station in this subpath

            int stationIndex = 0;
            StationStop thisStation = StationStops[stationIndex];
            while (thisStation.SubrouteIndex < TCRoute.ActiveSubPath) // station was in previous subpath
            {
                StationStops.RemoveAt(0);
                if (StationStops.Count == 0) // no more stations
                {
                    return;
                }
                thisStation = StationStops[0];
            }

            if (thisStation.SubrouteIndex > TCRoute.ActiveSubPath)    // station is not in this subpath
            {
                return;
            }

            // get distance to station, but not if just after switch to Autopilot and not during station stop
            bool validStop = false;
            if (!fromAutopilotSwitch || (simulator.PlayerLocomotive != null && simulator.ActivityRun != null && !(
                this == simulator.OriginalPlayerTrain && simulator.ActivityRun.ActivityTask is ActivityTaskPassengerStopAt && TrainAtStation())))
            {
                while (!validStop)
                {
                    float[] distancesM = CalculateDistancesToNextStation(thisStation, TrainMaxSpeedMpS, false);
                    if (distancesM[0] < 0f && !(MovementState == AiMovementState.StationStop && distancesM[0] != -1)) // stop is not valid
                    {

                        StationStops.RemoveAt(0);
                        if (StationStops.Count == 0)
                        {
                            return;  // no more stations - exit
                        }

                        thisStation = StationStops[0];
                        if (thisStation.SubrouteIndex > TCRoute.ActiveSubPath)
                            return;  // station not in this subpath - exit
                    }
                    else
                    {
                        validStop = true;
                        AIActionItem newAction = new AIActionItem(null, AiActionType.StationStop);
                        newAction.SetParam(distancesM[1], 0.0f, distancesM[0], DistanceTravelledM);
                        RequiredActions.InsertAction(newAction);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Calculate actual distance and trigger distance for next station
        /// </summary>

        public float[] CalculateDistancesToNextStation(StationStop thisStation, float presentSpeedMpS, bool reschedule)
        {
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
            float leftInSectionM = thisSection.Length - PresentPosition[Direction.Forward].Offset;

            // get station route index - if not found, return distances < 0

            int stationIndex0 = ValidRoutes[Direction.Forward].GetRouteIndex(thisStation.TrackCircuitSectionIndex, PresentPosition[Direction.Forward].RouteListIndex);
            int stationIndex1 = ValidRoutes[Direction.Forward].GetRouteIndex(thisStation.TrackCircuitSectionIndex, PresentPosition[Direction.Backward].RouteListIndex);

            float distanceToTrainM = -1f;

            // use front position
            if (stationIndex0 >= 0)
            {
                distanceToTrainM = ValidRoutes[Direction.Forward].GetDistanceAlongRoute(PresentPosition[Direction.Forward].RouteListIndex,
                    leftInSectionM, stationIndex0, thisStation.StopOffset, true);
            }

            // if front beyond station, use rear position (correct for length)
            else if (stationIndex1 >= 0)
            {
                thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];
                leftInSectionM = thisSection.Length - PresentPosition[Direction.Backward].Offset;
                distanceToTrainM = ValidRoutes[Direction.Forward].GetDistanceAlongRoute(PresentPosition[Direction.Backward].RouteListIndex,
                    leftInSectionM, stationIndex1, thisStation.StopOffset, true) - Length;
            }

            // if beyond station and train is stopped - return present position
            if (distanceToTrainM < 0f && MovementState == AiMovementState.StationStop)
            {
                return (new float[2] { PresentPosition[Direction.Forward].DistanceTravelled, 0.0f });
            }

            // if station not on route at all return negative values
            if (distanceToTrainM < 0f && stationIndex0 < 0 && stationIndex1 < 0)
            {
                return (new float[2] { -1f, -1f });
            }

            // if reschedule, use actual speed

            float activateDistanceTravelledM = PresentPosition[Direction.Forward].DistanceTravelled + distanceToTrainM;
            float triggerDistanceM = 0.0f;

            if (reschedule)
            {
                float firstPartTime = 0.0f;
                float firstPartRangeM = 0.0f;
                float secndPartRangeM = 0.0f;
                float remainingRangeM = activateDistanceTravelledM - PresentPosition[Direction.Forward].DistanceTravelled;

                firstPartTime = presentSpeedMpS / (0.25f * MaxDecelMpSS);
                firstPartRangeM = 0.25f * MaxDecelMpSS * (firstPartTime * firstPartTime);

                if (firstPartRangeM < remainingRangeM && SpeedMpS < TrainMaxSpeedMpS) // if distance left and not at max speed
                // split remaining distance based on relation between acceleration and deceleration
                {
                    secndPartRangeM = (remainingRangeM - firstPartRangeM) * (2.0f * MaxDecelMpSS) / (MaxDecelMpSS + MaxAccelMpSS);
                }

                triggerDistanceM = activateDistanceTravelledM - (firstPartRangeM + secndPartRangeM);
            }
            else

            // use maximum speed
            {
                float deltaTime = TrainMaxSpeedMpS / MaxDecelMpSS;
                float brakingDistanceM = (TrainMaxSpeedMpS * deltaTime) + (0.5f * MaxDecelMpSS * deltaTime * deltaTime);
                triggerDistanceM = activateDistanceTravelledM - brakingDistanceM;
            }

            float[] distancesM = new float[2];
            distancesM[0] = activateDistanceTravelledM;
            distancesM[1] = triggerDistanceM;

            return (distancesM);
        }

        //================================================================================================//
        /// <summary>
        /// Override Switch to Signal control
        /// </summary>

        internal override void SwitchToSignalControl(Signal thisSignal)
        {
            base.SwitchToSignalControl(thisSignal);
            if (TrainType != TrainType.Player)
            {
                if (!((this is AITrain) && (this as AITrain).MovementState == AiMovementState.Suspended))
                {
                    ResetActions(true);

                    // check if any actions must be processed immediately

                    ObtainRequiredActions(0);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Override Switch to Node control
        /// </summary>

        internal override void SwitchToNodeControl(int thisSectionIndex)
        {
            base.SwitchToNodeControl(thisSectionIndex);
            if (TrainType != TrainType.Player)
            {
                if (!((this is AITrain) && (this as AITrain).MovementState == AiMovementState.Suspended))
                {
                    ResetActions(true);

                    // check if any actions must be processed immediately

                    ObtainRequiredActions(0);
                }
            }
        }

        protected override void UpdateNodeMode()
        {
            // update node mode
            EndAuthorityType oldAuthority = EndAuthorities[Direction.Forward].EndAuthorityType;
            base.UpdateNodeMode();

            // if authoriy type changed, reset actions
            if (EndAuthorities[Direction.Forward].EndAuthorityType != oldAuthority)
            {
                ResetActions(true, false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update AI Static state
        /// </summary>
        /// <param name="presentTime"></param>

        internal override void UpdateAIStaticState(int presentTime)
        {
            // start if start time is reached

            if (StartTime.HasValue && StartTime.Value < presentTime && TrainHasPower())
            {
                foreach (var car in Cars)
                {
                    if (car is MSTSLocomotive)
                    {
                        MSTSLocomotive loco = car as MSTSLocomotive;
                        loco.SetPower(true);
                    }
                }
                PowerState = true;

                PostInit();
                return;
            }

            // switch off power for all engines

            if (PowerState)
            {
                foreach (var car in Cars)
                {
                    if (car is MSTSLocomotive)
                    {
                        MSTSLocomotive loco = car as MSTSLocomotive;
                        loco.SetPower(false);
                    }
                }
                PowerState = false;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update train in stopped state
        /// </summary>

        public virtual AiMovementState UpdateStoppedState(double elapsedClockSeconds)
        {
            var AuxActionnextActionInfo = nextActionInfo;
            var tryBraking = true;
            if (SpeedMpS > 0)   // if train still running force it to stop
            {
                SpeedMpS = 0;
                Update(0);   // stop the wheels from moving etc
                AITrainThrottlePercent = 0;
                AITrainBrakePercent = 100;

            }

            if (SpeedMpS < 0)   // if train still running force it to stop
            {
                SpeedMpS = 0;
                Update(0);   // stop the wheels from moving etc
                AITrainThrottlePercent = 0;
                AITrainBrakePercent = 100;

            }

            // check if train ahead - if so, determine speed and distance

            if (ControlMode == TrainControlMode.AutoNode &&
                EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.TrainAhead)
            {

                // check if train ahead is in same section
                int sectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                int startIndex = ValidRoutes[Direction.Forward].GetRouteIndex(sectionIndex, 0);
                int endIndex = ValidRoutes[Direction.Forward].GetRouteIndex(EndAuthorities[Direction.Forward].LastReservedSection, 0);

                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[sectionIndex];

                Dictionary<Train, float> trainInfo = thisSection.TestTrainAhead(this, PresentPosition[Direction.Forward].Offset, PresentPosition[Direction.Forward].Direction);

                // search for train ahead in route sections
                for (int iIndex = startIndex + 1; iIndex <= endIndex && trainInfo.Count <= 0; iIndex++)
                {
                    trainInfo = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection.TestTrainAhead(this, 0.0f, ValidRoutes[Direction.Forward][iIndex].Direction);
                }

                if (trainInfo.Count <= 0)
                // train is in section beyond last reserved
                {
                    if (endIndex < ValidRoutes[Direction.Forward].Count - 1)
                    {
                        trainInfo = ValidRoutes[Direction.Forward][endIndex + 1].TrackCircuitSection.TestTrainAhead(this, 0.0f, ValidRoutes[Direction.Forward][endIndex + 1].Direction);
                    }
                }

                if (trainInfo.Count > 0)  // found train
                {
                    foreach (KeyValuePair<Train, float> trainAhead in trainInfo) // always just one
                    {
                        Train OtherTrain = trainAhead.Key;
                        if (Math.Abs(OtherTrain.SpeedMpS) < 0.001f &&
                                    (EndAuthorities[Direction.Forward].Distance > FollowDistanceStatTrain || UncondAttach || OtherTrain.TrainType == TrainType.Static ||
                                    OtherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex ==
                                    TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count - 1].TrackCircuitSection.Index
                                    || OtherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex ==
                                    TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count - 1].TrackCircuitSection.Index))
                        {
                            // allow creeping closer
                            CreateTrainAction(PresetCreepSpeed, 0.0f, EndAuthorities[Direction.Forward].Distance, null, AiActionType.TrainAhead);
                            MovementState = AiMovementState.Following;
                            StartMoving(AiStartMovement.FollowTrain);
                        }

                        else if (Math.Abs(OtherTrain.SpeedMpS) > 0 &&
                            EndAuthorities[Direction.Forward].Distance > KeepDistanceMovingTrain)
                        {
                            // train started moving
                            MovementState = AiMovementState.Following;
                            StartMoving(AiStartMovement.FollowTrain);
                        }
                    }
                }
                // if train not found, do nothing - state will change next update

            }

            // Other node mode : check distance ahead (path may have cleared)

            else if (ControlMode == TrainControlMode.AutoNode && EndAuthorities[Direction.Forward].EndAuthorityType != EndAuthorityType.ReservedSwitch &&
                        EndAuthorities[Direction.Forward].Distance > ActivityClearingDistanceM)
            {
                NextStopDistanceM = EndAuthorities[Direction.Forward].Distance;
                StartMoving(AiStartMovement.SignalCleared);
            }

            else if (ControlMode == TrainControlMode.AutoNode && EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.ReservedSwitch &&
                        EndAuthorities[Direction.Forward].Distance > 2.0f * JunctionOverlapM + ActivityClearingDistanceM)
            {
                NextStopDistanceM = EndAuthorities[Direction.Forward].Distance - 2.0f * JunctionOverlapM;
                StartMoving(AiStartMovement.SignalCleared);
            }


            // signal node : check state of signal

            else if (ControlMode == TrainControlMode.AutoSignal)
            {
                SignalAspectState nextAspect = SignalAspectState.Unknown;
                bool nextPermission = false;
                Signal nextSignal = null;
                // there is a next item and it is the next signal
                if (nextActionInfo != null && nextActionInfo.ActiveItem != null &&
                    nextActionInfo.ActiveItem.SignalDetails == NextSignalObjects[Direction.Forward])
                {
                    nextSignal = nextActionInfo.ActiveItem.SignalDetails;
                    nextAspect = nextSignal.SignalLR(SignalFunction.Normal);
                }
                else
                {
                    nextAspect = GetNextSignalAspect(0);
                    if (NextSignalObjects[Direction.Forward] != null)
                        nextSignal = NextSignalObjects[Direction.Forward];
                }
                nextPermission = nextSignal != null && nextSignal.OverridePermission == SignalPermission.Granted;

                if (NextSignalObjects[Direction.Forward] == null) // no signal ahead so switch Node control
                {
                    SwitchToNodeControl(PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
                    NextStopDistanceM = EndAuthorities[Direction.Forward].Distance;
                }

                else if ((nextAspect > SignalAspectState.Stop || nextPermission) &&
                        nextAspect < SignalAspectState.Approach_1)
                {
                    // check if any other signals within clearing distance
                    bool signalCleared = true;
                    bool withinDistance = true;

                    for (int iitem = 0; iitem <= SignalObjectItems.Count - 1 && withinDistance && signalCleared; iitem++)
                    {
                        SignalItemInfo nextObject = SignalObjectItems[iitem];
                        if (nextObject.ItemType == SignalItemType.Signal)
                        {
                            if (nextObject.SignalDetails != NextSignalObjects[Direction.Forward]) // not signal we are waiting for
                            {
                                if (nextObject.DistanceToTrain > 2.0 * ClearingDistance)
                                {
                                    withinDistance = false;  // signal is far enough ahead
                                }
                                else if (nextObject.SignalState == SignalAspectState.Stop)
                                {
                                    signalCleared = false;   // signal is not clear
                                    NextSignalObjects[Direction.Forward].ForcePropagation = true;
                                }
                            }
                        }
                    }

                    if (signalCleared)
                    {
                        ResetActions(true);
                        NextStopDistanceM = 5000f; // clear to 5000m, will be updated if required
                        StartMoving(AiStartMovement.SignalRestricted);
                    }
                }
                else if (nextAspect >= SignalAspectState.Approach_1)
                {
                    // check if any other signals within clearing distance
                    bool signalCleared = true;
                    bool withinDistance = true;

                    for (int iitem = 0; iitem <= SignalObjectItems.Count - 1 && withinDistance && signalCleared; iitem++)
                    {
                        SignalItemInfo nextObject = SignalObjectItems[iitem];
                        if (nextObject.ItemType == SignalItemType.Signal)
                        {
                            if (nextObject.SignalDetails != NextSignalObjects[Direction.Forward]) // not signal we are waiting for
                            {
                                if (nextObject.DistanceToTrain > 2.0 * ClearingDistance)
                                {
                                    withinDistance = false;  // signal is far enough ahead
                                }
                                else if (nextObject.SignalState == SignalAspectState.Stop)
                                {
                                    signalCleared = false;   // signal is not clear
                                    NextSignalObjects[Direction.Forward].ForcePropagation = true;
                                }
                            }
                        }
                    }

                    if (signalCleared)
                    {
                        ResetActions(true);
                        NextStopDistanceM = 5000f; // clear to 5000m, will be updated if required
                        StartMoving(AiStartMovement.SignalCleared);
                    }
                }

                else if (nextAspect == SignalAspectState.Stop)
                {
                    // if stop but train is well away from signal allow to close; also if at end of path.
                    if (DistanceToSignal.HasValue && DistanceToSignal.Value > 5 * SignalApproachDistance ||
                        (TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count - 1 == PresentPosition[Direction.Forward].RouteListIndex))
                    {
                        MovementState = AiMovementState.Accelerating;
                        StartMoving(AiStartMovement.PathAction);
                    }
                    else
                        tryBraking = false;
                    //                    else if (IsActualPlayerTrain && NextSignalObjects[Direction.Forward].hasPermission == SignalObject.Permission.Granted)
                    //                    {
                    //                        MovementState = AI_MOVEMENT_STATE.ACCELERATING;
                    //                        StartMoving(AI_START_MOVEMENT.PATH_ACTION);
                    //                    }
                }

                else if (nextActionInfo != null &&
                 nextActionInfo.NextAction == AiActionType.StationStop)
                {
                    if (StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath &&
                       ValidRoutes[Direction.Forward].GetRouteIndex(StationStops[0].TrackCircuitSectionIndex, PresentPosition[Direction.Forward].RouteListIndex) <= PresentPosition[Direction.Forward].RouteListIndex)
                    // assume to be in station
                    {
                        MovementState = AiMovementState.StationStop;
                    }
                    else
                    // approaching next station
                    {
                        MovementState = AiMovementState.Braking;
                    }
                }
                else if (nextActionInfo != null &&
                    nextActionInfo.NextAction == AiActionType.AuxiliaryAction)
                {
                    MovementState = AiMovementState.Braking;
                }
                else if (nextActionInfo == null || nextActionInfo.NextAction != AiActionType.SignalAspectStop)
                {
                    if (nextAspect != SignalAspectState.Stop)
                    {
                        MovementState = AiMovementState.Running;
                        StartMoving(AiStartMovement.SignalCleared);
                    }
                    else
                    {
                        //<CSComment: without this train would not start moving if there is a stop signal in front
                        if (NextSignalObjects[Direction.Forward] != null)
                        {
                            var distanceSignaltoTrain = NextSignalObjects[Direction.Forward].DistanceTo(FrontTDBTraveller);
                            float distanceToReversalPoint = 10000000f;
                            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath] != null && TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                            {
                                distanceToReversalPoint = ComputeDistanceToReversalPoint();
                            }
                            if (distanceSignaltoTrain >= 100.0f || (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.Reversal
                                && nextActionInfo.ActivateDistanceM - DistanceTravelledM > 10) ||
                                distanceSignaltoTrain > distanceToReversalPoint)
                            {
                                MovementState = AiMovementState.Braking;
                                //>CSComment: better be sure the train will stop in front of signal
                                CreateTrainAction(0.0f, 0.0f, distanceSignaltoTrain, SignalObjectItems[0], AiActionType.SignalAspectStop);
                                Alpha10 = PreUpdate ? 2 : 10;
                                AITrainThrottlePercent = 25;
                                AdjustControlsBrakeOff();
                            }
                        }
                    }
                }
            }
            float distanceToNextSignal = DistanceToSignal.HasValue ? DistanceToSignal.Value : 0.1f;
            if (AuxActionnextActionInfo != null && MovementState == AiMovementState.Stopped && tryBraking && distanceToNextSignal > ClearingDistance
                && EndAuthorities[Direction.Forward].EndAuthorityType != EndAuthorityType.ReservedSwitch && EndAuthorities[Direction.Forward].Distance <= 2.0f * JunctionOverlapM)   // && ControlMode == TRAIN_CONTROL.AUTO_NODE)
            {
                MovementState = AiMovementState.Braking;
            }
            return MovementState;
        }

        //================================================================================================//
        /// <summary>
        /// Train is on turntable
        /// Dummy method for child instancing
        /// </summary>
        public virtual void UpdateTurntableState(double elapsedTimeSeconds, int presentTime)
        { }

        //================================================================================================//
        /// <summary>
        /// Train is at station
        /// </summary>
        public virtual void UpdateStationState(double elapsedClockSeconds, int presentTime)
        {
            StationStop thisStation = StationStops[0];
            bool removeStation = true;

            int eightHundredHours = 8 * 3600;
            int sixteenHundredHours = 16 * 3600;
            double actualdepart = thisStation.ActualDepart;

            // no arrival / departure time set : update times

            if (thisStation.StopType == StationStopType.Station)
            {
                AtStation = true;

                if (thisStation.ActualArrival < 0)
                {
                    thisStation.ActualArrival = presentTime;
                    var stopTime = thisStation.CalculateDepartTime(this);
                    actualdepart = (int)thisStation.ActualDepart;
                    doorOpenDelay = 4.0f;
                    doorCloseAdvance = stopTime - 10.0f;
                    if (PreUpdate)
                        doorCloseAdvance -= 10;
                    if (doorCloseAdvance - 6 < doorOpenDelay)
                    {
                        doorOpenDelay = 0;
                        doorCloseAdvance = stopTime - 3;
                    }
                }
                else
                {
                    if (!IsFreight && simulator.OpenDoorsInAITrains)
                    {
                        var frontIsFront = thisStation.PlatformReference == thisStation.PlatformItem.PlatformFrontUiD;
                        if (doorOpenDelay > 0)
                        {
                            doorOpenDelay -= (float)elapsedClockSeconds;
                            if (doorOpenDelay < 0)
                            {
                                if ((thisStation.PlatformItem.PlatformSide & PlatformDetails.PlatformSides.Left) == PlatformDetails.PlatformSides.Left)
                                {
                                    //open left doors
                                    SetDoors(frontIsFront ? DoorSide.Right : DoorSide.Left, true);
                                }
                                if ((thisStation.PlatformItem.PlatformSide & PlatformDetails.PlatformSides.Right) == PlatformDetails.PlatformSides.Right)
                                {
                                    //open right doors
                                    SetDoors(frontIsFront ? DoorSide.Left : DoorSide.Right, true);
                                }
                            }
                        }
                        if (doorCloseAdvance > 0)
                        {
                            doorCloseAdvance -= (float)elapsedClockSeconds;
                            if (doorCloseAdvance < 0)
                            {
                                if ((thisStation.PlatformItem.PlatformSide & PlatformDetails.PlatformSides.Left) == PlatformDetails.PlatformSides.Left)
                                {
                                    //open left doors
                                    SetDoors(frontIsFront ? DoorSide.Right : DoorSide.Left, false);
                                }
                                if ((thisStation.PlatformItem.PlatformSide & PlatformDetails.PlatformSides.Right) == PlatformDetails.PlatformSides.Right)
                                {
                                    //open right doors
                                    SetDoors(frontIsFront ? DoorSide.Left : DoorSide.Right, false);
                                }
                            }
                        }
                    }
                }

            }
            // not yet time to depart - check if signal can be released

            int correctedTime = presentTime;

            if (actualdepart > sixteenHundredHours && presentTime < eightHundredHours) // should have departed before midnight
            {
                correctedTime = presentTime + (24 * 3600);
            }

            if (actualdepart < eightHundredHours && presentTime > sixteenHundredHours) // to depart after midnight
            {
                correctedTime = presentTime - 24 * 3600;
            }

            if (actualdepart > correctedTime)
            {
                if (thisStation.StopType == StationStopType.Station &&
                    (actualdepart - 120 < correctedTime) &&
                     thisStation.HoldSignal)
                {
                    HoldingSignals.Remove(thisStation.ExitSignal);
                    var nextSignal = Simulator.Instance.SignalEnvironment.Signals[thisStation.ExitSignal];

                    if (nextSignal.EnabledTrain != null && nextSignal.EnabledTrain.Train == this)
                    {
                        nextSignal.RequestClearSignal(ValidRoutes[Direction.Forward], RoutedForward, 0, false, null);// for AI always use direction 0
                    }
                    thisStation.HoldSignal = false;
                }
                return;
            }

            // depart

            thisStation.Passed = true;
            Delay = TimeSpan.FromSeconds((presentTime - thisStation.DepartTime) % (24 * 3600));
            PreviousStop = thisStation.CreateCopy();

            if (thisStation.StopType == StationStopType.Station
                && MaxVelocityA > 0 && ServiceDefinition != null && ServiceDefinition.Count > 0 && this != simulator.Trains[0])
            // <CScomment> Recalculate TrainMaxSpeedMpS and AllowedMaxSpeedMpS
            {
                var actualServiceItemIdx = ServiceDefinition.FindIndex(si => si.PlatformStartID == thisStation.PlatformReference);
                if (actualServiceItemIdx >= 0 && ServiceDefinition.Count >= actualServiceItemIdx + 2)
                {
                    var sectionEfficiency = ServiceDefinition[actualServiceItemIdx + 1].Efficiency;
                    if (simulator.Settings.ActRandomizationLevel > 0)
                        RandomizeEfficiency(ref sectionEfficiency);
                    if (sectionEfficiency > 0)
                    {
                        TrainMaxSpeedMpS = Math.Min((float)simulator.Route.SpeedLimit, MaxVelocityA * sectionEfficiency);
                        RecalculateAllowedMaxSpeed();
                    }
                }
                else if (MaxVelocityA > 0 && Efficiency > 0)
                {
                    TrainMaxSpeedMpS = Math.Min((float)simulator.Route.SpeedLimit, MaxVelocityA * Efficiency);
                    RecalculateAllowedMaxSpeed();
                }
            }

            // first, check state of signal

            if (thisStation.ExitSignal >= 0 && (thisStation.HoldSignal || Simulator.Instance.SignalEnvironment.Signals[thisStation.ExitSignal].HoldState == SignalHoldState.StationStop))
            {
                if (HoldingSignals.Contains(thisStation.ExitSignal))
                    HoldingSignals.Remove(thisStation.ExitSignal);
                var nextSignal = Simulator.Instance.SignalEnvironment.Signals[thisStation.ExitSignal];

                // only request signal if in signal mode (train may be in node control)
                if (ControlMode == TrainControlMode.AutoSignal)
                {
                    nextSignal.RequestClearSignal(ValidRoutes[Direction.Forward], RoutedForward, 0, false, null); // for AI always use direction 0
                }
            }

            // check if station is end of path

            bool[] endOfPath = ProcessEndOfPath(presentTime, false);

            if (endOfPath[0])
            {
                removeStation = false; // do not remove station from list - is done by path processing
            }
            // check if station has exit signal and this signal is at danger
            else if (thisStation.ExitSignal >= 0 && NextSignalObjects[Direction.Forward] != null && NextSignalObjects[Direction.Forward].Index == thisStation.ExitSignal)
            {
                SignalAspectState nextAspect = GetNextSignalAspect(0);
                if (nextAspect == SignalAspectState.Stop && !NextSignalObjects[Direction.Forward].HasLockForTrain(Number, TCRoute.ActiveSubPath) &&
                    !(TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count - 1 == PresentPosition[Direction.Forward].RouteListIndex &&
                        TCRoute.TCRouteSubpaths.Count - 1 == TCRoute.ActiveSubPath))
                {
                    return;  // do not depart if exit signal at danger
                }
            }

            // change state if train still exists
            if (endOfPath[1])
            {
                if (MovementState == AiMovementState.StationStop)
                {
                    // if state is still station_stop and ready to depart - change to stop to check action
                    MovementState = AiMovementState.StoppedExisting;
                    if (TrainType != TrainType.AiPlayerHosting)
                        AtStation = false;
                }

                Delay = TimeSpan.FromSeconds((presentTime - thisStation.DepartTime) % (24 * 3600));
            }

            if (StationStops.Count > 0)
                PreviousStop = StationStops[0].CreateCopy();
            if (removeStation)
                StationStops.RemoveAt(0);
            ResetActions(true);
        }

        //================================================================================================//
        /// <summary>
        /// Train is braking
        /// </summary>

        public virtual void UpdateBrakingState(double elapsedClockSeconds, int presentTime)
        {

            // check if action still required

            bool clearAction = false;

            float distanceToGoM = ActivityClearingDistanceM;
            if (nextActionInfo != null && nextActionInfo.RequiredSpeedMpS == 99999f)  //  RequiredSpeed doesn't matter
            {
                return;
            }

            if (nextActionInfo == null) // action has been reset - keep status quo
            {
                if (ControlMode == TrainControlMode.AutoNode)  // node control : use control distance
                {
                    distanceToGoM = EndAuthorities[Direction.Forward].Distance;

                    if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.ReservedSwitch)
                    {
                        distanceToGoM = EndAuthorities[Direction.Forward].Distance - 2.0f * JunctionOverlapM;
                    }
                    else if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.EndOfPath)
                    {
                        distanceToGoM = EndAuthorities[Direction.Forward].Distance - ActivityClearingDistanceM;
                    }

                    if (distanceToGoM <= 0)
                    {
                        if (SpeedMpS > 0)
                        {
                            AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
                        }
                    }

                    if (distanceToGoM < ActivityClearingDistanceM && SpeedMpS <= 0)
                    {
                        MovementState = AiMovementState.Stopped;
                        return;
                    }
                }
                else // action cleared - set running or stopped
                {
                    if (SpeedMpS > 0)
                    {
                        MovementState = AiMovementState.Running;
                    }
                    else
                    {
                        MovementState = AiMovementState.Stopped;
                    }
                    return;
                }

            }

            // check if speedlimit on signal is cleared

            else if (nextActionInfo.NextAction == AiActionType.SpeedSignal)
            {
                if (nextActionInfo.ActiveItem.ActualSpeed >= AllowedMaxSpeedMpS)
                {
                    clearAction = true;
                }
                else if (nextActionInfo.ActiveItem.ActualSpeed < 0)
                {
                    clearAction = true;
                }
            }

            // check if STOP signal cleared

            else if (nextActionInfo.NextAction == AiActionType.SignalAspectStop)
            {
                var nextSignal = nextActionInfo.ActiveItem.SignalDetails;
                var nextPermission = nextSignal.OverridePermission == SignalPermission.Granted;
                if (nextActionInfo.ActiveItem.SignalState >= SignalAspectState.Approach_1 || nextPermission)
                {
                    clearAction = true;
                }
                else if (nextActionInfo.ActiveItem.SignalState != SignalAspectState.Stop)
                {
                    nextActionInfo.NextAction = AiActionType.SignalAspectRestricted;
                    if (((nextActionInfo.ActivateDistanceM - PresentPosition[Direction.Forward].DistanceTravelled) < SignalApproachDistance) ||
                         nextActionInfo.ActiveItem.SignalDetails.SignalNoSpeedReduction(SignalFunction.Normal))
                    {
                        clearAction = true;
                    }
                }
            }

            // check if RESTRICTED signal cleared

            else if (nextActionInfo.NextAction == AiActionType.SignalAspectRestricted)
            {
                if ((nextActionInfo.ActiveItem.SignalState >= SignalAspectState.Approach_1) ||
                   ((nextActionInfo.ActivateDistanceM - PresentPosition[Direction.Forward].DistanceTravelled) < SignalApproachDistance) ||
                   (nextActionInfo.ActiveItem.SignalDetails.SignalNoSpeedReduction(SignalFunction.Normal)))
                {
                    clearAction = true;
                }
            }

            // check if END_AUTHORITY extended

            else if (nextActionInfo.NextAction == AiActionType.EndOfAuthority)
            {
                nextActionInfo.ActivateDistanceM = EndAuthorities[Direction.Forward].Distance + DistanceTravelledM;
                if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.MaxDistance)
                {
                    clearAction = true;
                }
                else if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.ReservedSwitch)
                {
                    nextActionInfo.ActivateDistanceM -= 2.0f * JunctionOverlapM;
                }
            }

            else if (nextActionInfo.NextAction == AiActionType.SpeedLimit)
            {
                if (nextActionInfo.RequiredSpeedMpS >= AllowedMaxSpeedMpS)
                {
                    clearAction = true;
                }
                else if (nextActionInfo.ActiveItem.ActualSpeed != nextActionInfo.RequiredSpeedMpS)
                {
                    clearAction = true;
                }
            }

            // action cleared - reset processed info for object items to determine next action
            // clear list of pending action to create new list

            if (clearAction)
            {
                ResetActions(true);
                MovementState = AiMovementState.Running;
                Alpha10 = PreUpdate ? 2 : 10;
                if (SpeedMpS < AllowedMaxSpeedMpS - 3.0f * SpeedHysteris)
                {
                    AdjustControlsBrakeOff();
                }
                return;
            }

            // check ideal speed

            float requiredSpeedMpS = 0;
            float creepDistanceM = 3.0f * SignalApproachDistance;

            if (nextActionInfo != null)
            {
                requiredSpeedMpS = nextActionInfo.RequiredSpeedMpS;
                distanceToGoM = nextActionInfo.ActivateDistanceM - PresentPosition[Direction.Forward].DistanceTravelled;

                if (nextActionInfo.ActiveItem != null)
                {
                    if (Cars != null && Cars.Count < 10)
                    {
                        distanceToGoM = nextActionInfo.ActiveItem.DistanceToTrain - SignalApproachDistance / 4;
                        if (PreUpdate)
                            distanceToGoM -= SignalApproachDistance * 0.25f;
                        // Be more conservative if braking downhill
                        /* else if (FirstCar != null)
                        {
                            var Elevation = FirstCar.CurrentElevationPercent;
                            if (FirstCar.Flipped ^ (FirstCar.IsDriveable && FirstCar.Train.IsPlayerDriven && ((MSTSLocomotive)FirstCar).UsingRearCab)) Elevation = -Elevation;
                            if (FirstCar.CurrentElevationPercent < -2.0) distanceToGoM -= signalApproachDistanceM;
                        }*/
                    }
                    else
                        distanceToGoM = nextActionInfo.ActiveItem.DistanceToTrain - SignalApproachDistance;
                    //                    distanceToGoM = nextActionInfo.ActiveItem.distance_to_train - signalApproachDistanceM;
                }

                // check if stopped at station

                if (nextActionInfo.NextAction == AiActionType.StationStop)
                {
                    NextStopDistanceM = distanceToGoM;
                    if (distanceToGoM <= 0.1f)
                    {
                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 100);
                        AITrainThrottlePercent = 0;

                        // train is stopped - set departure time

                        if (Math.Abs(SpeedMpS) <= Simulator.MaxStoppedMpS)
                        {
                            MovementState = AiMovementState.StationStop;
                            StationStop thisStation = StationStops[0];

                            if (thisStation.StopType == StationStopType.Station)
                            {
                            }
                            else if (thisStation.StopType == StationStopType.WaitingPoint)
                            {
                                thisStation.ActualArrival = presentTime;

                                // delta time set
                                if (thisStation.DepartTime < 0)
                                {
                                    thisStation.ActualDepart = presentTime - thisStation.DepartTime; // depart time is negative!!
                                }
                                // actual time set
                                else
                                {
                                    thisStation.ActualDepart = thisStation.DepartTime;
                                }
                            }
                        }
                        return;
                    }
                }
                else if (nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem)))
                {
                    NextStopDistanceM = distanceToGoM;
                    MovementState = nextActionInfo.ProcessAction(this, presentTime, elapsedClockSeconds, MovementState);
                }
                // check speed reduction position reached

                else if (nextActionInfo.RequiredSpeedMpS > 0)
                {
                    if (distanceToGoM <= 0.0f)
                    {
                        AdjustControlsBrakeOff();
                        AllowedMaxSpeedMpS = nextActionInfo.RequiredSpeedMpS;
                        MovementState = AiMovementState.Running;
                        Alpha10 = PreUpdate ? 2 : 10;
                        ResetActions(true);
                        return;
                    }
                }

                // check if approaching reversal point

                else if (nextActionInfo.NextAction == AiActionType.Reversal)
                {
                    if (Math.Abs(SpeedMpS) < 0.03f && nextActionInfo.ActivateDistanceM - DistanceTravelledM < 10.0f)
                        MovementState = AiMovementState.Stopped;
                }

                // check if stopped at signal

                else if (nextActionInfo.RequiredSpeedMpS == 0)
                {
                    NextStopDistanceM = distanceToGoM;
                    if (distanceToGoM < SignalApproachDistance * 0.75f)
                    {
                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
                        AITrainThrottlePercent = 0;
                        if (Math.Abs(SpeedMpS) <= Simulator.MaxStoppedMpS)
                        {
                            MovementState = AiMovementState.Stopped;
                        }
                        else
                        {
                            AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 10);
                        }

                        // if approaching signal and at approach distance and still moving, force stop
                        if (distanceToGoM < 0 && SpeedMpS > 0 &&
                            nextActionInfo != null && nextActionInfo.NextAction == AiActionType.SignalAspectStop)
                        {
                            SpeedMpS = 0.0f;
                            foreach (TrainCar car in Cars)
                            {
                                car.SpeedMpS = SpeedMpS;
                            }
                        }

                        return;
                    }

                    if (nextActionInfo.NextAction == AiActionType.SignalAspectRestricted)
                    {
                        if (distanceToGoM < creepDistanceM)
                        {
                            requiredSpeedMpS = PresetCreepSpeed;
                        }
                    }
                }
            }

            if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop)
                creepDistanceM = 0.0f;
            if (nextActionInfo == null && requiredSpeedMpS == 0)
                creepDistanceM = ClearingDistance;

            // keep speed within required speed band

            // preset, also valid for reqSpeed > 0
            float lowestSpeedMpS = requiredSpeedMpS;
            creepDistanceM = 0.5f * SignalApproachDistance;

            if (requiredSpeedMpS == 0)
            {
                // station stop : use 0.5 signalApproachDistanceM as final stop approach
                if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop)
                {
                    creepDistanceM = 0.0f;
                    lowestSpeedMpS = PresetCreepSpeed;
                }
                // signal : use 3 * signalApproachDistanceM as final stop approach to avoid signal overshoot
                if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.SignalAspectStop)
                {
                    creepDistanceM = 3.0f * SignalApproachDistance;
                    lowestSpeedMpS =
                        distanceToGoM < creepDistanceM ? (0.5f * PresetCreepSpeed) : PresetCreepSpeed;
                }
                // otherwise use clearingDistanceM as approach distance
                else if (nextActionInfo == null)
                {
                    creepDistanceM = ClearingDistance;
                    lowestSpeedMpS =
                        distanceToGoM < creepDistanceM ? (0.5f * PresetCreepSpeed) : PresetCreepSpeed;
                }
                else
                {
                    lowestSpeedMpS = PresetCreepSpeed;
                }

            }

            lowestSpeedMpS = Math.Min(lowestSpeedMpS, AllowedMaxSpeedMpS);

            // braking distance - use 0.22 * MaxDecelMpSS as average deceleration (due to braking delay)
            // Videal - Vreq = a * T => T = (Videal - Vreq) / a
            // R = Vreq * T + 0.5 * a * T^2 => R = Vreq * (Videal - Vreq) / a + 0.5 * a * (Videal - Vreq)^2 / a^2 =>
            // R = Vreq * Videal / a - Vreq^2 / a + Videal^2 / 2a - 2 * Vreq * Videal / 2a + Vreq^2 / 2a => R = Videal^2 / 2a - Vreq^2 /2a
            // so : Videal = SQRT (2 * a * R + Vreq^2)
            // remaining distance is corrected for minimal approach distance as safety margin
            // for requiredSpeed > 0, take hysteris margin off ideal speed so speed settles on required speed
            // for requiredSpeed == 0, use ideal speed, this allows actual speed to be a little higher
            // upto creep distance : set creep speed as lowest possible speed

            float correctedDistanceToGoM = distanceToGoM - creepDistanceM;

            float maxPossSpeedMpS = lowestSpeedMpS;
            if (correctedDistanceToGoM > 0)
            {
                maxPossSpeedMpS = (float)Math.Sqrt(0.22f * MaxDecelMpSS * 2.0f * correctedDistanceToGoM + (requiredSpeedMpS * requiredSpeedMpS));
                maxPossSpeedMpS = Math.Max(lowestSpeedMpS, maxPossSpeedMpS);
            }

            float idealSpeedMpS = requiredSpeedMpS == 0 ? Math.Min((AllowedMaxSpeedMpS - 2f * SpeedHysteris), maxPossSpeedMpS) : Math.Min(AllowedMaxSpeedMpS, maxPossSpeedMpS) - (2f * SpeedHysteris);
            float idealLowBandMpS = Math.Max(0.25f * lowestSpeedMpS, idealSpeedMpS - (3f * SpeedHysteris));
            float ideal3LowBandMpS = Math.Max(0.5f * lowestSpeedMpS, idealSpeedMpS - (9f * SpeedHysteris));
            float idealHighBandMpS = Math.Min(AllowedMaxSpeedMpS, Math.Max(lowestSpeedMpS, idealSpeedMpS) + SpeedHysteris);
            float ideal3HighBandMpS = Math.Min(AllowedMaxSpeedMpS, Math.Max(lowestSpeedMpS, idealSpeedMpS) + (2f * SpeedHysteris));

            float deltaSpeedMpS = SpeedMpS - requiredSpeedMpS;
            float idealDecelMpSS = Math.Max((0.5f * MaxDecelMpSS), (deltaSpeedMpS * deltaSpeedMpS / (2.0f * distanceToGoM)));

            float lastDecelMpSS = elapsedClockSeconds > 0 ? (float)((SpeedMpS - LastSpeedMpS) / elapsedClockSeconds) : idealDecelMpSS;

            float preferredBrakingDistanceM = 2 * AllowedMaxSpeedMpS / (MaxDecelMpSS * MaxDecelMpSS);

            if (distanceToGoM < 0f)
            {
                idealSpeedMpS = requiredSpeedMpS;
                idealLowBandMpS = Math.Max(0.0f, idealSpeedMpS - SpeedHysteris);
                idealHighBandMpS = idealSpeedMpS;
                idealDecelMpSS = MaxDecelMpSS;
            }


            // keep speed withing band 

            if (SpeedMpS > AllowedMaxSpeedMpS)
            {
                if (AITrainThrottlePercent > 0)
                {
                    AdjustControlsThrottleOff();
                }
                else
                {
                    AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 20);
                }

                // clamp speed if still too high
                if (SpeedMpS > AllowedMaxSpeedMpS)
                {
                    AdjustControlsFixedSpeed(AllowedMaxSpeedMpS);
                    // PreUpdate doesn't use car speeds, so you need to adjust also overall train speed
                    if (PreUpdate)
                        SpeedMpS = AllowedMaxSpeedMpS;
                }

                Alpha10 = PreUpdate ? 1 : 5;
            }
            else if (SpeedMpS > requiredSpeedMpS && distanceToGoM < 0)
            {
                AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
            }
            else if (SpeedMpS > ideal3HighBandMpS)
            {
                if (AITrainThrottlePercent > 0)
                {
                    AdjustControlsThrottleOff();
                }
                else if (AITrainBrakePercent < 50)
                {
                    AdjustControlsBrakeMore(2.0f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    Alpha10 = PreUpdate ? 1 : 5;
                }
                // if at full brake always perform application as it forces braking in case of brake failure (eg due to wheelslip)
                else if (AITrainBrakePercent == 100)
                {
                    AdjustControlsBrakeMore(2.0f * MaxDecelMpSS, elapsedClockSeconds, 50);
                    Alpha10 = 0;
                }
                else if (lastDecelMpSS < 0.5f * idealDecelMpSS || Alpha10 <= 0)
                {
                    AdjustControlsBrakeMore(2.0f * MaxDecelMpSS, elapsedClockSeconds, 50);
                    Alpha10 = PreUpdate ? 1 : 5;
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS > idealHighBandMpS)
            {
                if (LastSpeedMpS > SpeedMpS)
                {
                    if (AITrainBrakePercent > 50)
                    {
                        AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    }
                    else if (AITrainBrakePercent > 0)
                    {
                        if (lastDecelMpSS > 1.5f * idealDecelMpSS)
                        {
                            AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 2);
                        }
                        else if (Alpha10 <= 0)
                        {
                            AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 2);
                            Alpha10 = PreUpdate ? 2 : 10;
                        }
                    }
                }
                else if (LastSpeedMpS < SpeedMpS)
                {
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(MaxAccelMpSS, elapsedClockSeconds, 10);
                    }
                    else if (AITrainThrottlePercent > 20)
                    {
                        AdjustControlsAccelLess(MaxAccelMpSS, elapsedClockSeconds, 2);
                    }
                    else if (AITrainThrottlePercent > 0)
                    {
                        AdjustControlsThrottleOff();
                    }
                    else if (Alpha10 <= 0 || lastDecelMpSS < (0.5 * idealDecelMpSS))
                    {
                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 10);
                        Alpha10 = PreUpdate ? 2 : 10;
                    }

                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS > idealLowBandMpS)
            {
                if (LastSpeedMpS < SpeedMpS)
                {
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                        Alpha10 = PreUpdate ? 1 : 5;
                    }
                }
                else
                {
                    if (AITrainBrakePercent > 50)
                    {
                        AdjustControlsBrakeLess(0.0f, elapsedClockSeconds, 20);
                    }
                    else if (AITrainBrakePercent > 0)
                    {
                        AdjustControlsBrakeOff();
                    }
                    else
                    {
                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                    }
                }
            }
            else if (distanceToGoM > 4 * preferredBrakingDistanceM && SpeedMpS < idealLowBandMpS)
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
            }
            else if (SpeedMpS > ideal3LowBandMpS)
            {
                if (AITrainBrakePercent > 0)
                {
                    AdjustControlsBrakeLess(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
                else if (LastSpeedMpS >= SpeedMpS)
                {
                    if (Alpha10 <= 0)
                    {
                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                        Alpha10 = PreUpdate ? 1 : 5;
                    }
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (distanceToGoM > preferredBrakingDistanceM && SpeedMpS < ideal3LowBandMpS)
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
            }
            else if (SpeedMpS < requiredSpeedMpS)
            {
                AdjustControlsBrakeOff();
                if (((SpeedMpS - LastSpeedMpS) / elapsedClockSeconds) < 0.5f * MaxAccelMpSS)
                {
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
                Alpha10 = PreUpdate ? 1 : 5;
            }
            else if (requiredSpeedMpS == 0 && distanceToGoM > creepDistanceM && SpeedMpS < PresetCreepSpeed)
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
            }
            else if (requiredSpeedMpS == 0 && distanceToGoM > SignalApproachDistance && SpeedMpS < PresetCreepSpeed)
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.25f * MaxAccelMpSS, elapsedClockSeconds, 10);
            }

            // in preupdate : avoid problems with overshoot due to low update rate
            // check if at present speed train would pass beyond end of authority
            if (PreUpdate)
            {
                if (requiredSpeedMpS == 0 && (elapsedClockSeconds * SpeedMpS) > distanceToGoM && SpeedMpS > PresetCreepSpeed)
                {
                    SpeedMpS = (0.5f * SpeedMpS);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Train is accelerating
        /// </summary>

        public virtual void UpdateAccelState(double elapsedClockSeconds)
        {

            // check speed
            if (((SpeedMpS - LastSpeedMpS) / elapsedClockSeconds) < 0.5 * MaxAccelMpSS)
            {
                int stepSize = (!PreUpdate) ? 10 : 40;
                float corrFactor = (!PreUpdate) ? 0.5f : 1.0f;
                AdjustControlsAccelMore(Efficiency * corrFactor * MaxAccelMpSS, elapsedClockSeconds, stepSize);
            }

            if (SpeedMpS > (AllowedMaxSpeedMpS - ((9.0f - 6.0f * Efficiency) * SpeedHysteris)))
            {
                AdjustControlsAccelLess(0.0f, elapsedClockSeconds, (int)(AITrainThrottlePercent * 0.5f));
                MovementState = AiMovementState.Running;
                Alpha10 = 0;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Train is following
        /// </summary>

        public virtual void UpdateFollowingState(double elapsedClockSeconds, int presentTime)
        {
            if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.TrainAhead && nextActionInfo.ActivateDistanceM - PresentPosition[Direction.Forward].DistanceTravelled < -5)

                if (ControlMode != TrainControlMode.AutoNode || EndAuthorities[Direction.Forward].EndAuthorityType != EndAuthorityType.TrainAhead) // train is gone
                {
                    MovementState = AiMovementState.Running;
                    ResetActions(true);
                }
                else
                {
                    // check if train is in sections ahead
                    Dictionary<Train, float> trainInfo = null;

                    // find other train
                    int startIndex = PresentPosition[Direction.Forward].RouteListIndex;
                    int endSectionIndex = EndAuthorities[Direction.Forward].LastReservedSection;
                    int endIndex = ValidRoutes[Direction.Forward].GetRouteIndex(endSectionIndex, startIndex);

                    TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].TrackCircuitSection;

                    trainInfo = thisSection.TestTrainAhead(this, PresentPosition[Direction.Forward].Offset, PresentPosition[Direction.Forward].Direction);
                    float addOffset = 0;
                    if (trainInfo.Count <= 0)
                    {
                        addOffset = thisSection.Length - PresentPosition[Direction.Forward].Offset;
                    }
                    else
                    {
                        // ensure train in section is aware of this train in same section if this is required
                        UpdateTrainOnEnteringSection(thisSection, trainInfo);
                    }

                    // train not in this section, try reserved sections ahead
                    for (int iIndex = startIndex + 1; iIndex <= endIndex && trainInfo.Count <= 0; iIndex++)
                    {
                        TrackCircuitSection nextSection = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection;
                        trainInfo = nextSection.TestTrainAhead(this, 0, ValidRoutes[Direction.Forward][iIndex].Direction);
                    }

                    // if train not ahead, try first section beyond last reserved
                    if (trainInfo.Count <= 0 && endIndex < ValidRoutes[Direction.Forward].Count - 1)
                    {
                        TrackCircuitSection nextSection = ValidRoutes[Direction.Forward][endIndex + 1].TrackCircuitSection;
                        trainInfo = nextSection.TestTrainAhead(this, 0, ValidRoutes[Direction.Forward][endIndex + 1].Direction);
                        if (trainInfo.Count <= 0)
                        {
                            addOffset += nextSection.Length;
                        }
                    }

                    // train is found
                    if (trainInfo.Count > 0)  // found train
                    {
                        foreach (KeyValuePair<Train, float> trainAhead in trainInfo) // always just one
                        {
                            Train OtherTrain = trainAhead.Key;

                            float distanceToTrain = trainAhead.Value + addOffset;

                            // update action info with new position

                            float keepDistanceTrainM = 0f;
                            bool attachToTrain = attachTo == OtherTrain.Number;

                            // <CScomment> Make check when this train in same section of OtherTrain or other train at less than 50m;
                            // if other train is static or other train is in last section of this train, pass to passive coupling
                            if (Math.Abs(OtherTrain.SpeedMpS) < 0.025f && distanceToTrain <= 2 * KeepDistanceMovingTrain)
                            {
                                if (OtherTrain.TrainType == TrainType.Static || (OtherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex ==
                                    TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count - 1].TrackCircuitSection.Index
                                    || OtherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex ==
                                    TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count - 1].TrackCircuitSection.Index) &&
                                    (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid || TCRoute.ActiveSubPath == TCRoute.TCRouteSubpaths.Count - 1)
                                    || UncondAttach)
                                {
                                    attachToTrain = true;
                                    attachTo = OtherTrain.Number;
                                }

                            }
                            if (Math.Abs(OtherTrain.SpeedMpS) >= 0.025f)
                            {
                                keepDistanceTrainM = KeepDistanceMovingTrain;
                            }
                            else if (!attachToTrain)
                            {
                                keepDistanceTrainM = (OtherTrain.IsFreight || IsFreight) ? KeepDistanceStatTrainFreight : KeepDistanceStatTrainPassenger;
                            }

                            if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.TrainAhead)
                            {
                                NextStopDistanceM = distanceToTrain - keepDistanceTrainM;
                            }
                            else if (nextActionInfo != null)
                            {
                                float deltaDistance = nextActionInfo.ActivateDistanceM - DistanceTravelledM;
                                if (nextActionInfo.RequiredSpeedMpS > 0.0f)
                                {
                                    NextStopDistanceM = distanceToTrain - keepDistanceTrainM;
                                }
                                else
                                {
                                    NextStopDistanceM = Math.Min(deltaDistance, (distanceToTrain - keepDistanceTrainM));
                                }

                                if (deltaDistance < distanceToTrain) // perform to normal braking to handle action
                                {
                                    MovementState = AiMovementState.Braking;  // not following the train
                                    UpdateBrakingState(elapsedClockSeconds, presentTime);
                                    return;
                                }
                            }

                            // check distance and speed
                            if (Math.Abs(OtherTrain.SpeedMpS) < 0.025f)
                            {
                                float brakingDistance = SpeedMpS * SpeedMpS * 0.5f * (0.5f * MaxDecelMpSS);
                                float reqspeed = (float)Math.Sqrt(distanceToTrain * MaxDecelMpSS);

                                float maxspeed = Math.Max(reqspeed / 2, PresetCreepSpeed); // allow continue at creepspeed
                                if (distanceToTrain < KeepDistanceStatTrainPassenger - 2.0f && attachToTrain)
                                    maxspeed = Math.Min(maxspeed, PresetCouplingSpeed);
                                maxspeed = Math.Min(maxspeed, AllowedMaxSpeedMpS); // but never beyond valid speed limit

                                // set brake or acceleration as required

                                if (SpeedMpS > maxspeed)
                                {
                                    AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                                }
                                else if ((distanceToTrain - brakingDistance) > keepDistanceTrainM * 3.0f)
                                {
                                    if (brakingDistance > distanceToTrain)
                                    {
                                        AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                                    }
                                    else if (SpeedMpS < maxspeed)
                                    {
                                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                                    }
                                }
                                else if ((distanceToTrain - brakingDistance) > keepDistanceTrainM)
                                {
                                    if (SpeedMpS > maxspeed)
                                    {
                                        AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 50);
                                    }
                                    else if (SpeedMpS > 0.25f * maxspeed)
                                    {
                                        AdjustControlsBrakeOff();
                                    }
                                    else
                                    {
                                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                                    }
                                }
                                if (OtherTrain.UncoupledFrom == this)
                                {
                                    if (distanceToTrain > 5.0f)
                                    {
                                        UncoupledFrom = null;
                                        OtherTrain.UncoupledFrom = null;
                                    }
                                    else
                                        attachToTrain = false;
                                }
                                //                            if (distanceToTrain < keepDistanceStatTrainM_P - 4.0f || (distanceToTrain - brakingDistance) <= keepDistanceTrainM) // Other possibility
                                if ((distanceToTrain - brakingDistance) <= keepDistanceTrainM)
                                {
                                    float reqMinSpeedMpS = attachToTrain ? PresetCouplingSpeed : 0;
                                    bool thisTrainFront;
                                    bool otherTrainFront;

                                    if (attachToTrain && CheckCouplePosition(OtherTrain, out thisTrainFront, out otherTrainFront))
                                    {
                                        MovementState = AiMovementState.Stopped;
                                        CoupleAI(OtherTrain, thisTrainFront, otherTrainFront);
                                        AI.TrainListChanged = true;
                                        attachTo = -1;
                                    }
                                    else if ((SpeedMpS - reqMinSpeedMpS) > 0.1f)
                                    {
                                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);

                                        // if too close, force stop or slow down if coupling
                                        if (distanceToTrain < 0.25 * keepDistanceTrainM)
                                        {
                                            foreach (TrainCar car in Cars)
                                            {
                                                //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                                                // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                                                //  car.SpeedMpS = car.Flipped ? -reqMinSpeedMpS : reqMinSpeedMpS;
                                                car.SpeedMpS = car.Flipped ^ (car is MSTSLocomotive && car.Train.IsActualPlayerTrain && (car as MSTSLocomotive).UsingRearCab) ? -reqMinSpeedMpS : reqMinSpeedMpS;
                                            }
                                            SpeedMpS = reqMinSpeedMpS;
                                        }
                                    }
                                    else if (attachToTrain)
                                    {
                                        AdjustControlsBrakeOff();
                                        if (SpeedMpS < 0.2 * PresetCreepSpeed)
                                        {
                                            AdjustControlsAccelMore(0.2f * MaxAccelMpSSP, 0.0f, 20);
                                        }
                                    }
                                    else
                                    {
                                        MovementState = AiMovementState.Stopped;

                                        // check if stopped in next station
                                        // conditions : 
                                        // next action must be station stop
                                        // next station must be in this subroute
                                        // if next train is AI and that trains state is STATION_STOP, station must be ahead of present position
                                        // else this train must be in station section

                                        bool otherTrainInStation = false;

                                        if (OtherTrain.TrainType == TrainType.Ai || OtherTrain.TrainType == TrainType.AiPlayerHosting)
                                        {
                                            AITrain OtherAITrain = OtherTrain as AITrain;
                                            otherTrainInStation = (OtherAITrain.MovementState == AiMovementState.StationStop);
                                        }

                                        bool thisTrainInStation = (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop);
                                        if (thisTrainInStation)
                                            thisTrainInStation = (StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath);
                                        if (thisTrainInStation)
                                        {
                                            var thisStation = StationStops[0];
                                            thisTrainInStation = CheckStationPosition(thisStation.PlatformItem, thisStation.Direction, thisStation.TrackCircuitSectionIndex);
                                        }

                                        if (thisTrainInStation)
                                        {
                                            MovementState = AiMovementState.StationStop;
                                            StationStop thisStation = StationStops[0];

                                            if (thisStation.StopType == StationStopType.Station)
                                            {
                                            }
                                            else if (thisStation.StopType == StationStopType.WaitingPoint)
                                            {
                                                thisStation.ActualArrival = presentTime;

                                                // delta time set
                                                if (thisStation.DepartTime < 0)
                                                {
                                                    thisStation.ActualDepart = presentTime - thisStation.DepartTime; // depart time is negative!!
                                                }
                                                // actual time set
                                                else
                                                {
                                                    thisStation.ActualDepart = thisStation.DepartTime;
                                                }

                                                // if waited behind other train, move remaining track sections to next subroute if required

                                                // scan sections in backward order
                                                TrackCircuitPartialPathRoute nextRoute = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath + 1];

                                                for (int iIndex = ValidRoutes[Direction.Forward].Count - 1; iIndex > PresentPosition[Direction.Forward].RouteListIndex; iIndex--)
                                                {
                                                    int nextSectionIndex = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection.Index;
                                                    if (nextRoute.GetRouteIndex(nextSectionIndex, 0) <= 0)
                                                    {
                                                        nextRoute.Insert(0, ValidRoutes[Direction.Forward][iIndex]);
                                                    }
                                                    ValidRoutes[Direction.Forward].RemoveAt(iIndex);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // check whether trains are running same direction or not
                                bool runningAgainst = false;
                                if (PresentPosition[Direction.Forward].TrackCircuitSectionIndex == OtherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex &&
                                    PresentPosition[Direction.Forward].Direction != OtherTrain.PresentPosition[Direction.Forward].Direction)
                                    runningAgainst = true;
                                if ((SpeedMpS > (OtherTrain.SpeedMpS + SpeedHysteris) && !runningAgainst) ||
                                    SpeedMpS > (MaxFollowSpeed + SpeedHysteris) ||
                                    distanceToTrain < (keepDistanceTrainM - ClearingDistance))
                                {
                                    AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                                }
                                else if ((SpeedMpS < (OtherTrain.SpeedMpS - SpeedHysteris) && !runningAgainst) &&
                                           SpeedMpS < MaxFollowSpeed &&
                                           distanceToTrain > (keepDistanceTrainM + ClearingDistance))
                                {
                                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 2);
                                }
                            }
                        }
                    }

                    // train not found - keep moving, state will change next update
                    else
                        attachTo = -1;
                }
        }

        //================================================================================================//
        /// <summary>
        /// Train is running at required speed
        /// </summary>

        public virtual void UpdateRunningState(double elapsedClockSeconds)
        {
            float topBand = AllowedMaxSpeedMpS > PresetCreepSpeed ? AllowedMaxSpeedMpS - ((1.5f - Efficiency) * SpeedHysteris) : AllowedMaxSpeedMpS;
            float highBand = AllowedMaxSpeedMpS > PresetCreepSpeed ? Math.Max(0.5f, AllowedMaxSpeedMpS - ((3.0f - 2.0f * Efficiency) * SpeedHysteris)) : AllowedMaxSpeedMpS;
            float lowBand = AllowedMaxSpeedMpS > PresetCreepSpeed ? Math.Max(0.4f, AllowedMaxSpeedMpS - ((9.0f - 3.0f * Efficiency) * SpeedHysteris)) : AllowedMaxSpeedMpS;
            int throttleTop = 90;

            // check speed

            if (SpeedMpS > AllowedMaxSpeedMpS)
            {
                if (AITrainThrottlePercent > 0)
                {
                    AdjustControlsThrottleOff();
                }
                else
                {
                    AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 20);
                }

                AdjustControlsFixedSpeed(AllowedMaxSpeedMpS);
                Alpha10 = PreUpdate ? 1 : 5;
            }
            else if (SpeedMpS > topBand)
            {
                if (LastSpeedMpS > SpeedMpS)
                {
                    if (AITrainBrakePercent > 0)
                    {
                        AdjustControlsBrakeLess(0.5f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    }
                }
                else
                {
                    if (AITrainThrottlePercent > throttleTop)
                    {
                        AdjustControlsAccelLess(0.0f, elapsedClockSeconds, 20);
                    }
                    else if (AITrainThrottlePercent > 0)
                    {
                        if (Alpha10 <= 0)
                        {
                            AdjustControlsAccelLess(0.0f, elapsedClockSeconds, 2);
                            Alpha10 = PreUpdate ? 1 : 5;
                        }
                    }
                    else if (AITrainBrakePercent < 50)
                    {
                        AdjustControlsBrakeMore(0.0f, elapsedClockSeconds, 10);
                    }
                    else
                    {
                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
                    }
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS > highBand)
            {
                if (LastSpeedMpS > SpeedMpS)
                {
                    if (AITrainBrakePercent > 50)
                    {
                        AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    }
                    else if (AITrainBrakePercent > 0)
                    {
                        AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 2);
                    }
                    else if (Alpha10 <= 0)
                    {
                        AdjustControlsAccelMore(0.3f * MaxAccelMpSS, elapsedClockSeconds, 2);
                        Alpha10 = PreUpdate ? 2 : 10;
                    }
                }
                else
                {
                    if (AITrainThrottlePercent > throttleTop)
                    {
                        AdjustControlsAccelLess(0.3f * MaxAccelMpSS, elapsedClockSeconds, 20);
                    }
                    else if (Alpha10 <= 0 && AITrainThrottlePercent > 20)
                    {
                        AdjustControlsAccelLess(0.3f * MaxAccelMpSS, elapsedClockSeconds, 5);
                        Alpha10 = PreUpdate ? 2 : 10;
                    }
                    else if (Alpha10 <= 0 && AITrainThrottlePercent < 10)
                    {
                        AdjustControlsAccelMore(0.3f * MaxAccelMpSS, elapsedClockSeconds, 2);
                        Alpha10 = PreUpdate ? 2 : 10;
                    }
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS > lowBand)
            {
                if (LastSpeedMpS < SpeedMpS)
                {
                    if (AITrainThrottlePercent > throttleTop)
                    {
                        AdjustControlsAccelLess(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                    }
                }
                else
                {
                    if (AITrainBrakePercent > 50)
                    {
                        AdjustControlsBrakeLess(0.0f, elapsedClockSeconds, 20);
                    }
                    else if (AITrainBrakePercent > 0)
                    {
                        AdjustControlsBrakeOff();
                    }
                    else
                    {
                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                    }
                }
                Alpha10 = 0;
            }
            else
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                Alpha10 = 0;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Start Moving
        /// </summary>

        public virtual void StartMoving(AiStartMovement reason)
        {
            // reset brakes, set throttle

            if (reason == AiStartMovement.FollowTrain)
            {
                MovementState = AiMovementState.Following;
                AITrainThrottlePercent = 25;
                AdjustControlsBrakeOff();
            }
            else if (ControlMode == TrainControlMode.AutoNode && EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.TrainAhead)
            {
                MovementState = AiMovementState.Following;
                AITrainThrottlePercent = 0;
            }
            else if (reason == AiStartMovement.NewTrain)
            {
                MovementState = AiMovementState.Stopped;
                AITrainThrottlePercent = 0;
            }
            else if (nextActionInfo != null)  // train has valid action, so start in BRAKE mode
            {
                MovementState = AiMovementState.Braking;
                Alpha10 = PreUpdate ? 2 : 10;
                AITrainThrottlePercent = 25;
                AdjustControlsBrakeOff();
            }
            else
            {
                MovementState = AiMovementState.Accelerating;
                Alpha10 = PreUpdate ? 2 : 10;
                AITrainThrottlePercent = (!PreUpdate) ? 25 : 50;
                AdjustControlsBrakeOff();
            }

            SetPercentsFromTrainToTrainset();

        }

        //================================================================================================//
        /// <summary>
        /// Set correct state for train allready in section when entering occupied section
        /// </summary>

        public void UpdateTrainOnEnteringSection(TrackCircuitSection thisSection, Dictionary<Train, float> trainsInSection)
        {
            foreach (KeyValuePair<Train, float> trainAhead in trainsInSection) // always just one
            {
                Train OtherTrain = trainAhead.Key;
                if (OtherTrain.ControlMode == TrainControlMode.AutoSignal) // train is still in signal mode, might need adjusting
                {
                    // check directions of this and other train
                    Direction owndirection = (Direction)(-1);
                    Direction otherdirection = (Direction)(-1);

                    foreach (KeyValuePair<TrainRouted, Direction> trainToCheckInfo in thisSection.CircuitState.OccupationState)
                    {
                        TrainRouted trainToCheck = trainToCheckInfo.Key;

                        if (trainToCheck.Train.Number == Number)  // this train
                        {
                            owndirection = trainToCheckInfo.Value;
                        }
                        else if (trainToCheck.Train.Number == OtherTrain.Number)
                        {
                            otherdirection = trainToCheckInfo.Value;
                        }
                    }

                    if (owndirection >= 0 && otherdirection >= 0) // both trains found
                    {
                        if (owndirection != otherdirection) // opposite directions - this train is now ahead of train in section
                        {
                            OtherTrain.SwitchToNodeControl(thisSection.Index);
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Train control routines
        /// </summary>

        public void AdjustControlsBrakeMore(float reqDecelMpSS, double timeS, int stepSize)
        {
            if (AITrainThrottlePercent > 0)
            {
                AITrainThrottlePercent = 0;
            }

            if (AITrainBrakePercent < 100)
            {
                AITrainBrakePercent += stepSize;
                if (AITrainBrakePercent > 100)
                    AITrainBrakePercent = 100;
            }
            else
            {
                double ds = timeS * (reqDecelMpSS);
                SpeedMpS = (float)Math.Max(SpeedMpS - ds, 0); // avoid negative speeds
                foreach (TrainCar car in Cars)
                {
                    //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                    // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                    //  car.SpeedMpS = car.Flipped ? -SpeedMpS : SpeedMpS;
                    car.SpeedMpS = car.Flipped ^ (car is MSTSLocomotive && car.Train.IsActualPlayerTrain && (car as MSTSLocomotive).UsingRearCab) ? -SpeedMpS : SpeedMpS;
                }
            }

            SetPercentsFromTrainToTrainset();

        }

        public void AdjustControlsBrakeLess(float reqDecelMpSS, double timeS, int stepSize)
        {
            if (AITrainThrottlePercent > 0)
            {
                AITrainThrottlePercent = 0;
            }

            if (AITrainBrakePercent > 0)
            {
                AITrainBrakePercent -= stepSize;
                if (AITrainBrakePercent < 0)
                    AdjustControlsBrakeOff();
            }
            else
            {
                double ds = timeS * (reqDecelMpSS);
                SpeedMpS = SpeedMpS + (float)ds; // avoid negative speeds
                foreach (TrainCar car in Cars)
                {
                    //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                    // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                    //  car.SpeedMpS = car.Flipped ? -SpeedMpS : SpeedMpS;
                    car.SpeedMpS = car.Flipped ^ (car is MSTSLocomotive && car.Train.IsActualPlayerTrain && (car as MSTSLocomotive).UsingRearCab) ? -SpeedMpS : SpeedMpS;
                }
            }

            SetPercentsFromTrainToTrainset();
        }

        public void AdjustControlsBrakeOff()
        {
            AITrainBrakePercent = 0;
            InitializeBrakes();

            if (FirstCar != null)
            {
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
                if (TrainType == TrainType.AiPlayerHosting)
                {
                    if (FirstCar is MSTSLocomotive)
                        ((MSTSLocomotive)FirstCar).SetTrainBrakePercent(AITrainBrakePercent);
                    if (simulator.PlayerLocomotive != null && FirstCar != simulator.PlayerLocomotive)
                    {
                        simulator.PlayerLocomotive.BrakeSystem.AISetPercent(AITrainBrakePercent);
                        ((MSTSLocomotive)simulator.PlayerLocomotive).SetTrainBrakePercent(AITrainBrakePercent);
                    }
                }
            }
        }

        public void AdjustControlsBrakeFull()
        {
            AITrainThrottlePercent = 0;
            AITrainBrakePercent = 100;

            if (FirstCar != null)
            {
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
                if (TrainType == TrainType.AiPlayerHosting)
                {
                    if (FirstCar is MSTSLocomotive)
                        ((MSTSLocomotive)FirstCar).SetTrainBrakePercent(AITrainBrakePercent);
                    if (simulator.PlayerLocomotive != null && FirstCar != simulator.PlayerLocomotive)
                    {
                        simulator.PlayerLocomotive.BrakeSystem.AISetPercent(AITrainBrakePercent);
                        ((MSTSLocomotive)simulator.PlayerLocomotive).SetTrainBrakePercent(AITrainBrakePercent);
                    }
                }
            }
        }

        public void AdjustControlsThrottleOff()
        {
            AITrainThrottlePercent = 0;

            if (FirstCar != null)
            {
                FirstCar.ThrottlePercent = AITrainThrottlePercent;
                if (TrainType == TrainType.AiPlayerHosting)
                {
                    if (FirstCar is MSTSLocomotive)
                    {
                        ((MSTSLocomotive)FirstCar).SetThrottlePercent(AITrainThrottlePercent);
                    }
                    if (simulator.PlayerLocomotive != null && FirstCar != simulator.PlayerLocomotive)
                    {
                        simulator.PlayerLocomotive.ThrottlePercent = AITrainThrottlePercent;
                        ((MSTSLocomotive)simulator.PlayerLocomotive).SetThrottlePercent(AITrainThrottlePercent);
                    }
                }
            }
        }

        public void AdjustControlsAccelMore(float reqAccelMpSS, double timeS, int stepSize)
        {
            if (AITrainBrakePercent > 0)
            {
                AdjustControlsBrakeOff();
            }

            if (AITrainThrottlePercent < 100)
            {
                AITrainThrottlePercent += stepSize;
                if (AITrainThrottlePercent > 100)
                    AITrainThrottlePercent = 100;
            }
            else if (LastSpeedMpS == 0 || (((SpeedMpS - LastSpeedMpS) / timeS) < 0.5f * MaxAccelMpSS))
            {
                double ds = timeS * (reqAccelMpSS);
                SpeedMpS = LastSpeedMpS + (float)ds;
                foreach (TrainCar car in Cars)
                {
                    //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                    // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                    //  car.SpeedMpS = car.Flipped ? -SpeedMpS : SpeedMpS;
                    car.SpeedMpS = car.Flipped ^ (car is MSTSLocomotive && car.Train.IsActualPlayerTrain && (car as MSTSLocomotive).UsingRearCab) ? -SpeedMpS : SpeedMpS;
                }

            }

            SetPercentsFromTrainToTrainset();
        }


        public void AdjustControlsAccelLess(float reqAccelMpSS, double timeS, int stepSize)
        {
            if (AITrainBrakePercent > 0)
            {
                AdjustControlsBrakeOff();
            }

            if (AITrainThrottlePercent > 0)
            {
                AITrainThrottlePercent -= stepSize;
                if (AITrainThrottlePercent < 0)
                    AITrainThrottlePercent = 0;
            }
            else
            {
                double ds = timeS * (reqAccelMpSS);
                SpeedMpS = (float)Math.Max(SpeedMpS - ds, 0); // avoid negative speeds
                foreach (TrainCar car in Cars)
                {
                    //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                    // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                    //  car.SpeedMpS = car.Flipped ? -SpeedMpS : SpeedMpS;
                    car.SpeedMpS = car.Flipped ^ (car is MSTSLocomotive && car.Train.IsActualPlayerTrain && (car as MSTSLocomotive).UsingRearCab) ? -SpeedMpS : SpeedMpS;
                }
            }
            SetPercentsFromTrainToTrainset();
        }

        public void AdjustControlsFixedSpeed(float reqSpeedMpS)
        {
            foreach (TrainCar car in Cars)
            {
                //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                //  car.SpeedMpS = car.Flipped ? -reqSpeedMpS : reqSpeedMpS;
                car.SpeedMpS = car.Flipped ^ (car is MSTSLocomotive && car.Train.IsActualPlayerTrain && (car as MSTSLocomotive).UsingRearCab) ? -reqSpeedMpS : reqSpeedMpS;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set first car and player loco throttle and brake percent in accordance with their AI train ones
        /// </summary>
        ///
        public void SetPercentsFromTrainToTrainset()
        {
            if (FirstCar != null)
            {
                FirstCar.ThrottlePercent = AITrainThrottlePercent;
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
                if (TrainType == TrainType.AiPlayerHosting)
                {
                    if (FirstCar is MSTSLocomotive)
                    {
                        ((MSTSLocomotive)FirstCar).SetTrainBrakePercent(AITrainBrakePercent);
                        ((MSTSLocomotive)FirstCar).SetThrottlePercent(AITrainThrottlePercent);
                    }
                    if (simulator.PlayerLocomotive != null && FirstCar != simulator.PlayerLocomotive)
                    {
                        simulator.PlayerLocomotive.ThrottlePercent = AITrainThrottlePercent;
                        simulator.PlayerLocomotive.BrakeSystem.AISetPercent(AITrainBrakePercent);
                        ((MSTSLocomotive)simulator.PlayerLocomotive).SetTrainBrakePercent(AITrainBrakePercent);
                        ((MSTSLocomotive)simulator.PlayerLocomotive).SetThrottlePercent(AITrainThrottlePercent);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update AllowedMaxSpeedMps after station stop
        /// </summary>
        /// 

        public void RecalculateAllowedMaxSpeed()
        {
            var allowedMaxSpeedPathMpS = Math.Min(allowedAbsoluteMaxSpeedSignalMpS, allowedAbsoluteMaxSpeedLimitMpS);
            allowedMaxSpeedPathMpS = Math.Min(allowedMaxSpeedPathMpS, allowedAbsoluteMaxTempSpeedLimitMpS);
            AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedPathMpS, TrainMaxSpeedMpS);
            AllowedMaxSpeedSignalMpS = (Math.Min(allowedAbsoluteMaxSpeedSignalMpS, TrainMaxSpeedMpS));
            AllowedMaxSpeedLimitMpS = (Math.Min(allowedAbsoluteMaxSpeedLimitMpS, TrainMaxSpeedMpS));
            allowedMaxTempSpeedLimitMpS = (Math.Min(allowedAbsoluteMaxTempSpeedLimitMpS, TrainMaxSpeedMpS));
        }

        //================================================================================================//
        /// <summary>
        /// Create waiting point list
        /// </summary>

        public void BuildWaitingPointList(float clearingDistanceM)
        {
            bool insertSigDelegate = true;
            // loop through all waiting points - back to front as the processing affects the actual routepaths

            List<int> signalIndex = new List<int>();
            for (int iWait = 0; iWait <= TCRoute.WaitingPoints.Count - 1; iWait++)
            {
                WaitingPointDetail waitingPoint = TCRoute.WaitingPoints[iWait];

                //check if waiting point is in existing subpath
                if (waitingPoint.SubListIndex >= TCRoute.TCRouteSubpaths.Count)
                {
                    Trace.TraceInformation($"Waiting point for train {Name}({Number}) is not on route - point removed");
                    continue;
                }

                TrackCircuitPartialPathRoute thisRoute = TCRoute.TCRouteSubpaths[waitingPoint.SubListIndex];
                int routeIndex = thisRoute.GetRouteIndex(waitingPoint.WaitingPointSection, 0);
                int lastIndex = routeIndex;

                // check if waiting point is in route - else give warning and skip
                if (routeIndex < 0)
                {
                    Trace.TraceInformation($"Waiting point for train {Name}({Number}) is not on route - point removed");
                    continue;
                }

                bool endSectionFound = false;
                int endSignalIndex = -1;
                float distanceToEndOfWPSection = 0;

                TrackCircuitSection thisSection = thisRoute[routeIndex].TrackCircuitSection;
                TrackCircuitSection nextSection =
                    routeIndex < thisRoute.Count - 2 ? thisRoute[routeIndex + 1].TrackCircuitSection : null;
                TrackDirection direction = thisRoute[routeIndex].Direction;
                if (thisSection.EndSignals[direction] != null)
                {
                    endSectionFound = true;
                    if (routeIndex < thisRoute.Count - 1)
                        endSignalIndex = thisSection.EndSignals[direction].Index;
                }

                // check if next section is junction

                else if (nextSection == null || nextSection.CircuitType != TrackCircuitType.Normal)
                {
                    endSectionFound = true;
                }

                // try and find next section with signal; if junction is found, stop search

                int nextIndex = routeIndex + 1;
                while (nextIndex < thisRoute.Count - 1 && !endSectionFound)
                {
                    nextSection = thisRoute[nextIndex].TrackCircuitSection;
                    direction = thisRoute[nextIndex].Direction;

                    if (nextSection.EndSignals[direction] != null)
                    {
                        endSectionFound = true;
                        lastIndex = nextIndex;
                        if (lastIndex < thisRoute.Count - 1)
                            endSignalIndex = nextSection.EndSignals[direction].Index;
                    }
                    else if (nextSection.CircuitType != TrackCircuitType.Normal)
                    {
                        endSectionFound = true;
                        lastIndex = nextIndex - 1;
                    }
                    nextIndex++;
                    if (nextSection != null)
                        distanceToEndOfWPSection += nextSection.Length;
                }
                signalIndex.Add(endSignalIndex);

                //<CSComment> TODO This is probably redundant now, however removing it would require extensive testing </CSComment>
                // move backwards WPs within clearingDistanceM, except if of type Horn
                for (int rWP = iWait; insertSigDelegate && signalIndex[iWait] != -1 && rWP >= 0; rWP--)
                {
                    WaitingPointDetail currWP = TCRoute.WaitingPoints[rWP];
                    if ((currWP.WaitTime >= 60011 && currWP.WaitTime <= 60021)
                        || currWP.WaitingPointSection != thisSection.Index || currWP.Offset < (int)(thisSection.Length + distanceToEndOfWPSection - clearingDistanceM - 1))
                        break;
                    currWP.Offset = (int)(thisSection.Length + distanceToEndOfWPSection - clearingDistanceM - 1);
                }

            }
            //            insertSigDelegate = false;
            for (int iWait = 0; iWait <= TCRoute.WaitingPoints.Count - 1; iWait++)
            {
                insertSigDelegate = true;
                WaitingPointDetail waitingPoint = TCRoute.WaitingPoints[iWait];

                //check if waiting point is in existing subpath
                if (waitingPoint.SubListIndex >= TCRoute.TCRouteSubpaths.Count)
                {
                    Trace.TraceInformation($"Waiting point for train {Name}({Number}) is not on route - point removed");
                    continue;
                }

                TrackCircuitPartialPathRoute thisRoute = TCRoute.TCRouteSubpaths[waitingPoint.SubListIndex];
                int routeIndex = thisRoute.GetRouteIndex(waitingPoint.WaitingPointSection, 0);
                int lastIndex = routeIndex;
                if (!(waitingPoint.WaitTime >= 60011 && waitingPoint.WaitTime <= 60021))
                {
                    if (iWait != TCRoute.WaitingPoints.Count - 1)
                    {
                        for (int nextWP = iWait + 1; nextWP < TCRoute.WaitingPoints.Count; nextWP++)
                        {
                            if (signalIndex[iWait] != signalIndex[nextWP])
                            {
                                break;
                            }
                            else if (TCRoute.WaitingPoints[nextWP].WaitTime >= 60011 && TCRoute.WaitingPoints[nextWP].WaitTime <= 60021)
                                continue;
                            else
                            {
                                insertSigDelegate = false;
                                break;
                            }
                        }
                    }
                }


                // check if waiting point is in route - else give warning and skip
                if (routeIndex < 0)
                {
                    Trace.TraceInformation($"Waiting point for train {Name}({Number}) is not on route - point removed");
                    continue;
                }
                Direction direction = (Direction)thisRoute[routeIndex].Direction;
                if (!IsActualPlayerTrain)
                {
                    if (waitingPoint.WaitTime >= 60011 && waitingPoint.WaitTime <= 60021)
                    {
                        var durationS = waitingPoint.WaitTime - 60010;
                        AILevelCrossingHornPattern hornPattern;
                        switch (durationS)
                        {
                            case 11:
                                hornPattern = AILevelCrossingHornPattern.CreateInstance(Common.LevelCrossingHornPattern.US);
                                break;
                            default:
                                hornPattern = AILevelCrossingHornPattern.CreateInstance(Common.LevelCrossingHornPattern.Single);
                                break;
                        }
                        AIActionHornRef action = new AIActionHornRef(this, waitingPoint.Offset, 0f, waitingPoint.SubListIndex, lastIndex, thisRoute[lastIndex].TrackCircuitSection.Index, direction, durationS, hornPattern);
                        AuxActionsContainer.Add(action);
                    }
                    else
                    {
                        AIActionWPRef action = new AIActionWPRef(this, waitingPoint.Offset, 0f, waitingPoint.SubListIndex, lastIndex, thisRoute[lastIndex].TrackCircuitSection.Index, direction);
                        var randomizedDelay = waitingPoint.WaitTime;
                        if (simulator.Settings.ActRandomizationLevel > 0)
                        {
                            randomizedDelay = RandomizedWPDelay(randomizedDelay);
                        }
                        action.SetDelay(randomizedDelay);
                        AuxActionsContainer.Add(action);
                        if (insertSigDelegate && (waitingPoint.WaitTime != 60002) && signalIndex[iWait] > -1)
                        {
                            AIActSigDelegateRef delegateAction = new AIActSigDelegateRef(this, waitingPoint.Offset, 0f, waitingPoint.SubListIndex, lastIndex, thisRoute[lastIndex].TrackCircuitSection.Index, direction, action);
                            Simulator.Instance.SignalEnvironment.Signals[signalIndex[iWait]].LockForTrain(this.Number, waitingPoint.SubListIndex);
                            delegateAction.SetEndSignalIndex(signalIndex[iWait]);

                            if (randomizedDelay >= 30000 && randomizedDelay < 40000)
                            {
                                delegateAction.Delay = randomizedDelay;
                                delegateAction.IsAbsolute = true;
                            }
                            else
                                delegateAction.Delay = 0;
                            delegateAction.SetSignalObject(Simulator.Instance.SignalEnvironment.Signals[signalIndex[iWait]]);

                            AuxActionsContainer.Add(delegateAction);
                        }
                    }
                }
                else if (insertSigDelegate && signalIndex[iWait] > -1)
                {
                    AIActionWPRef action = new AIActionWPRef(this, waitingPoint.Offset, 0f, waitingPoint.SubListIndex, lastIndex, thisRoute[lastIndex].TrackCircuitSection.Index, direction);
                    var randomizedDelay = waitingPoint.WaitTime;
                    if (simulator.Settings.ActRandomizationLevel > 0)
                    {
                        randomizedDelay = RandomizedWPDelay(randomizedDelay);
                    }
                    action.SetDelay((randomizedDelay >= 30000 && randomizedDelay < 40000) ? randomizedDelay : 0);
                    AuxActionsContainer.Add(action);
                    AIActSigDelegateRef delegateAction = new AIActSigDelegateRef(this, waitingPoint.Offset, 0f, waitingPoint.SubListIndex, lastIndex, thisRoute[lastIndex].TrackCircuitSection.Index, direction, action);
                    Simulator.Instance.SignalEnvironment.Signals[signalIndex[iWait]].LockForTrain(this.Number, waitingPoint.SubListIndex);
                    delegateAction.SetEndSignalIndex(signalIndex[iWait]);
                    delegateAction.Delay = randomizedDelay;
                    if (randomizedDelay >= 30000 && randomizedDelay < 40000)
                        delegateAction.IsAbsolute = true;
                    delegateAction.SetSignalObject(Simulator.Instance.SignalEnvironment.Signals[signalIndex[iWait]]);

                    AuxActionsContainer.Add(delegateAction);
                }
                //                insertSigDelegate = false;
            }
        }


        //================================================================================================//
        /// <summary>
        /// Initialize brakes for AI trains
        /// </summary>

        internal override void InitializeBrakes()
        {
            if (TrainType == TrainType.AiPlayerDriven || TrainType == TrainType.Player)
            {
                base.InitializeBrakes();
                return;
            }
            float maxPressurePSI = 90;
            float fullServPressurePSI = 64;
            float maxPressurePSIVacuum = 21;
            float fullServReductionPSI = -5;
            float max = maxPressurePSI;
            float fullServ = fullServPressurePSI;
            BrakeSystem.BrakeLine3Pressure = BrakeSystem.BrakeLine4Pressure = 0;
            if (FirstCar != null && FirstCar.BrakeSystem is VacuumSinglePipe)
            {
                max = maxPressurePSIVacuum;
                fullServ = maxPressurePSIVacuum + fullServReductionPSI;
            }
            BrakeSystem.EqualReservoirPressurePSIorInHg = (float)(BrakeSystem.BrakeLine2Pressure = max);
            ConnectBrakeHoses();
            foreach (TrainCar car in Cars)
            {
                car.BrakeSystem.Initialize(false, max, fullServ, true);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Process end of path 
        /// returns :
        /// [0] : true : end of route, false : not end of route
        /// [1] : true : train still exists, false : train is removed and no longer exists
        /// </summary>

        public virtual bool[] ProcessEndOfPath(int presentTime, bool checkLoop = true)
        {
            bool[] returnValue = new bool[2] { false, true };

            if (PresentPosition[Direction.Forward].RouteListIndex < 0)
            // Is already off path
            {
                returnValue[0] = true;
                if (TrainType != TrainType.AiPlayerHosting)
                    Trace.TraceWarning("AI Train {0} service {1} off path and removed", Number, Name);
                ProcessEndOfPathReached(ref returnValue, presentTime);
                return (returnValue);
            }

            TrackDirection directionNow = ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].Direction;
            int positionNow = ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].TrackCircuitSection.Index;
            TrackDirection directionNowBack = PresentPosition[Direction.Backward].Direction;
            int positionNowBack = PresentPosition[Direction.Backward].TrackCircuitSectionIndex;

            (bool endOfRoute, bool otherRouteAvailable) = UpdateRouteActions(0, checkLoop);

            if (!endOfRoute)
                return returnValue;   // not at end and not to attach to anything

            returnValue[0] = true; // end of path reached
            if (otherRouteAvailable)   // next route available
            {
                if (positionNowBack == PresentPosition[Direction.Forward].TrackCircuitSectionIndex && directionNowBack != PresentPosition[Direction.Forward].Direction)
                {
                    ReverseFormation(false);
                    // active subpath must be incremented in parallel in incorporated train if present
                    if (IncorporatedTrainNo >= 0)
                        IncrementSubpath(simulator.TrainDictionary[IncorporatedTrainNo]);
                }
                else if (positionNow == PresentPosition[Direction.Backward].TrackCircuitSectionIndex && directionNow != PresentPosition[Direction.Backward].Direction)
                {
                    ReverseFormation(false);
                    // active subpath must be incremented in parallel in incorporated train if present
                    if (IncorporatedTrainNo >= 0)
                        IncrementSubpath(simulator.TrainDictionary[IncorporatedTrainNo]);
                }

                // check if next station was on previous subpath - if so, move to this subpath

                if (StationStops.Count > 0)
                {
                    StationStop thisStation = StationStops[0];

                    if (thisStation.Passed)
                    {
                        StationStops.RemoveAt(0);
                    }
                    else if (thisStation.SubrouteIndex < TCRoute.ActiveSubPath)
                    {
                        thisStation.SubrouteIndex = TCRoute.ActiveSubPath;

                        if (ValidRoutes[Direction.Forward].GetRouteIndex(thisStation.TrackCircuitSectionIndex, 0) < 0) // station no longer on route
                        {
                            if (thisStation.ExitSignal >= 0 && thisStation.HoldSignal && HoldingSignals.Contains(thisStation.ExitSignal))
                            {
                                HoldingSignals.Remove(thisStation.ExitSignal);
                            }
                            StationStops.RemoveAt(0);
                        }
                    }
                }

                // reset to node control, also reset required actions

                SwitchToNodeControl(-1);
            }
            else
            {
                ProcessEndOfPathReached(ref returnValue, presentTime);
            }

            return (returnValue);
        }

        public virtual void ProcessEndOfPathReached(ref bool[] returnValue, int presentTime)
        {
            var removeIt = true;
            var distanceThreshold = PreUpdate ? 5.0f : 2.0f;
            var distanceToNextSignal = DistanceToSignal.HasValue ? DistanceToSignal.Value : 0.1f;

            if (simulator.TimetableMode)
                removeIt = true;
            else if (TrainType == TrainType.AiPlayerHosting || simulator.OriginalPlayerTrain == this)
                removeIt = false;
            else if (TCRoute.TCRouteSubpaths.Count == 1 || TCRoute.ActiveSubPath != TCRoute.TCRouteSubpaths.Count - 1)
                removeIt = true;
            else if (NextSignalObjects[Direction.Forward] != null && NextSignalObjects[Direction.Forward].SignalType == SignalCategory.Signal && distanceToNextSignal < 25 && distanceToNextSignal >= 0 && PresentPosition[Direction.Backward].DistanceTravelled < distanceThreshold)
            {
                removeIt = false;
                MovementState = AiMovementState.Frozen;
            }
            else if (PresentPosition[Direction.Backward].DistanceTravelled < distanceThreshold && FrontTDBTraveller.TrackNodeOffset + 25 > FrontTDBTraveller.TrackNodeLength)
            {
                var tempTraveller = new Traveller(FrontTDBTraveller);
                if (tempTraveller.NextTrackNode() && tempTraveller.TrackNodeType == TrackNodeType.End)
                {
                    removeIt = false;
                    MovementState = AiMovementState.Frozen;
                }
            }
            else
            {
                if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath - 1].Valid && PresentPosition[Direction.Backward].DistanceTravelled < distanceThreshold && PresentPosition[Direction.Backward].Offset < 25)
                {
                    var tempTraveller = new Traveller(RearTDBTraveller);
                    tempTraveller.ReverseDirection();
                    if (tempTraveller.NextTrackNode() && tempTraveller.TrackNodeType == TrackNodeType.End)
                    {
                        removeIt = false;
                        MovementState = AiMovementState.Frozen;
                    }
                }
            }

            if (removeIt)
            {
                if (IncorporatedTrainNo >= 0 && simulator.TrainDictionary.Count > IncorporatedTrainNo &&
                   simulator.TrainDictionary[IncorporatedTrainNo] != null)
                    simulator.TrainDictionary[IncorporatedTrainNo].RemoveTrain();
                RemoveTrain();
            }
            returnValue[1] = false;
        }

        public virtual bool CheckCouplePosition(Train attachTrain, out bool thisTrainFront, out bool otherTrainFront)
        {
            thisTrainFront = true;
            otherTrainFront = true;

            Traveller usedTraveller = new Traveller(FrontTDBTraveller);
            Direction direction = Direction.Forward;

            if (MUDirection == MidpointDirection.Reverse)
            {
                usedTraveller = new Traveller(RearTDBTraveller, true); // use in direction of movement
                thisTrainFront = false;
                direction = Direction.Backward;
            }

            Traveller otherTraveller = null;
            Direction otherDirection = Direction.Forward;
            bool withinSection = false;

            // Check if train is in same section as other train, either for the other trains front or rear
            if (PresentPosition[direction].TrackCircuitSectionIndex == attachTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex) // train in same section as front
            {
                withinSection = true;
            }
            else if (PresentPosition[direction].TrackCircuitSectionIndex == attachTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex) // train in same section as rear
            {
                otherDirection = Direction.Backward;
                withinSection = true;
            }

            if (!withinSection) // not yet in same section
            {
                return (false);
            }

            // test directions
            if (PresentPosition[direction].Direction == attachTrain.PresentPosition[otherDirection].Direction) // trains are in same direction
            {
                if (direction == Direction.Backward)
                {
                    otherTraveller = new Traveller(attachTrain.FrontTDBTraveller);
                }
                else
                {
                    otherTraveller = new Traveller(attachTrain.RearTDBTraveller);
                    otherTrainFront = false;
                }
            }
            else
            {
                if (direction == Direction.Backward)
                {
                    otherTraveller = new Traveller(attachTrain.RearTDBTraveller);
                    otherTrainFront = false;
                }
                else
                {
                    otherTraveller = new Traveller(attachTrain.FrontTDBTraveller);
                }
            }

            if (PreUpdate)
                return (true); // in pre-update, being in the same section is good enough

            // check distance to other train
            float dist = usedTraveller.OverlapDistanceM(otherTraveller, false);
            return (dist < 0.1f);
        }

        public void CoupleAI(Train attachTrain, bool thisTrainFront, bool attachTrainFront)
        {
            // stop train
            SpeedMpS = 0;
            AdjustControlsThrottleOff();
            PhysicsUpdate(0);
            // check for length of remaining path
            if (attachTrain.TrainType == TrainType.Static && (TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1 || ValidRoutes[Direction.Forward].Count > 5))
            {
                CoupleAIToStatic(attachTrain, thisTrainFront, attachTrainFront);
                return;
            }
            else if (attachTrain.TrainType != TrainType.Static && TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1 && !UncondAttach)
            {
                if ((thisTrainFront && Cars[0] is MSTSLocomotive) || (!thisTrainFront && Cars[Cars.Count - 1] is MSTSLocomotive))
                {
                    StealCarsToLivingTrain(attachTrain, thisTrainFront, attachTrainFront);
                    return;
                }
                else
                {
                    LeaveCarsToLivingTrain(attachTrain, thisTrainFront, attachTrainFront);
                    return;
                }
            }

            {
                // check on reverse formation
                if (thisTrainFront == attachTrainFront)
                {
                    ReverseFormation(false);
                }

                if (attachTrain.TrainType == TrainType.AiPlayerDriven)
                {
                    foreach (var car in Cars)
                        if (car is MSTSLocomotive)
                            (car as MSTSLocomotive).AntiSlip = (attachTrain.LeadLocomotive as MSTSLocomotive).AntiSlip; // <CSComment> TODO Temporary patch until AntiSlip is re-implemented
                }

                var attachCar = Cars[0];
                // Must save this because below the player locomotive passes to the other train
                var isActualPlayerTrain = IsActualPlayerTrain;

                // attach to front of waiting train
                if (attachTrainFront)
                {
                    attachCar = Cars[Cars.Count - 1];
                    for (int iCar = Cars.Count - 1; iCar >= 0; iCar--)
                    {
                        var car = Cars[iCar];
                        car.Train = attachTrain;
                        //                        car.CarID = "AI" + attachTrain.Number.ToString() + " - " + (attachTrain.Cars.Count - 1).ToString();
                        attachTrain.Cars.Insert(0, car);
                    }
                    if (attachTrain.LeadLocomotiveIndex >= 0)
                        attachTrain.LeadLocomotiveIndex += Cars.Count;
                }
                // attach to rear of waiting train
                else
                {
                    foreach (var car in Cars)
                    {
                        car.Train = attachTrain;
                        //                        car.CarID = "AI" + attachTrain.Number.ToString() + " - " + (attachTrain.Cars.Count - 1).ToString();
                        attachTrain.Cars.Add(car);
                    }
                }

                // remove cars from this train
                Cars.Clear();
                attachTrain.Length += Length;

                // recalculate position of formed train
                if (attachTrainFront)  // coupled to front, so rear position is still valid
                {
                    attachTrain.CalculatePositionOfCars();
                    attachTrain.DistanceTravelledM += Length;
                }
                else // coupled to rear so front position is still valid
                {
                    attachTrain.RepositionRearTraveller();    // fix the rear traveller
                    attachTrain.CalculatePositionOfCars();
                }

                // update positions train
                TrackNode tn = attachTrain.FrontTDBTraveller.TrackNode;
                float offset = attachTrain.FrontTDBTraveller.TrackNodeOffset;
                TrackDirection direction = (TrackDirection)attachTrain.FrontTDBTraveller.Direction.Reverse();

                attachTrain.PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
                attachTrain.PreviousPosition[Direction.Forward].UpdateFrom(attachTrain.PresentPosition[Direction.Forward]);

                tn = attachTrain.RearTDBTraveller.TrackNode;
                offset = attachTrain.RearTDBTraveller.TrackNodeOffset;
                direction = (TrackDirection)attachTrain.RearTDBTraveller.Direction.Reverse();

                attachTrain.PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
                // set various items
                attachTrain.CheckFreight();
                attachTrain.SetDistributedPowerUnitIds();
                attachTrain.ReinitializeEOT();
                attachTrain.ActivityClearingDistanceM = attachTrain.Cars.Count < StandardTrainMinCarNo ? ShortClearingDistanceM : StandardClearingDistanceM;
                attachCar.SignalEvent(TrainEvent.Couple);

                // <CSComment> as of now it seems to run better without this initialization
                //if (MovementState != AI_MOVEMENT_STATE.AI_STATIC)
                //{
                //     if (!Simulator.Settings.EnhancedActCompatibility) InitializeSignals(true);
                //}
                //  <CSComment> Why initialize brakes of a disappeared train?    
                //            InitializeBrakes();
                attachTrain.PhysicsUpdate(0);   // stop the wheels from moving etc
                // remove original train
                if (isActualPlayerTrain && this != simulator.OriginalPlayerTrain)
                {
                    // Switch to the attached train as the one where we are now will be removed
                    simulator.TrainSwitcher.PickedTrainFromList = attachTrain;
                    simulator.TrainSwitcher.ClickedTrainFromList = true;
                    attachTrain.TrainType = TrainType.AiPlayerHosting;
                    AI.TrainsToRemoveFromAI.Add((AITrain)attachTrain);
                    simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Player train has been included into train {0} service {1}, that automatically becomes the new player train",
                        Number, Name));
                    simulator.PlayerLocomotive = Simulator.SetPlayerLocomotive(attachTrain);
                    (attachTrain as AITrain).SwitchToPlayerControl();
                    simulator.OnPlayerLocomotiveChanged();
                    AI.AITrains.Add(this);
                }
                if (!UncondAttach)
                {
                    RemoveTrain();
                }
                else
                {
                    // if there is just here a reversal point, increment subpath in order to be in accordance with attachTrain

                    var ppTCSectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                    this.IncorporatingTrainNo = attachTrain.Number;
                    this.IncorporatingTrain = attachTrain;
                    SuspendTrain(attachTrain);
                    if (ppTCSectionIndex == TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count - 1].TrackCircuitSection.Index)
                        IncrementSubpath(this);
                    attachTrain.IncorporatedTrainNo = this.Number;
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Couple AI train to static train
        /// </summary>
        /// 

        public void CoupleAIToStatic(Train attachTrain, bool thisTrainFront, bool attachTrainFront)
        {
            // check on reverse formation
            if (thisTrainFront == attachTrainFront)
            {
                attachTrain.ReverseFormation(false);
            }
            // Move cars from attachTrain to train
            // attach to front of this train
            var attachCar = Cars[Cars.Count - 1];
            if (thisTrainFront)
            {
                attachCar = Cars[0];
                for (int iCar = attachTrain.Cars.Count - 1; iCar >= 0; iCar--)
                {
                    var car = attachTrain.Cars[iCar];
                    car.Train = this;
                    //                    car.CarID = "AI" + Number.ToString() + " - " + (Cars.Count - 1).ToString();
                    Cars.Insert(0, car);
                }
            }
            else
            {
                foreach (var car in attachTrain.Cars)
                {
                    car.Train = this;
                    //                    car.CarID = "AI" + Number.ToString() + " - " + (Cars.Count - 1).ToString();
                    Cars.Add(car);
                }
            }
            // remove cars from attached train
            Length += attachTrain.Length;
            attachTrain.Cars.Clear();

            // recalculate position of formed train
            if (thisTrainFront)  // coupled to front, so rear position is still valid
            {
                CalculatePositionOfCars();
                DistanceTravelledM += attachTrain.Length;
                PresentPosition[Direction.Forward].DistanceTravelled = DistanceTravelledM;
                RequiredActions.ModifyRequiredDistance(attachTrain.Length);
            }
            else // coupled to rear so front position is still valid
            {
                RepositionRearTraveller();    // fix the rear traveller
                CalculatePositionOfCars();
                PresentPosition[Direction.Backward].DistanceTravelled = DistanceTravelledM - Length;
            }

            // update positions train
            TrackNode tn = FrontTDBTraveller.TrackNode;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)FrontTDBTraveller.Direction.Reverse();

            PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            tn = RearTDBTraveller.TrackNode;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)RearTDBTraveller.Direction.Reverse();

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            // set various items
            CheckFreight();
            SetDistributedPowerUnitIds();
            ActivityClearingDistanceM = Cars.Count < StandardTrainMinCarNo ? ShortClearingDistanceM : StandardClearingDistanceM;
            attachCar.SignalEvent(TrainEvent.Couple);

            // remove attached train
            if (attachTrain.TrainType == TrainType.Ai)
                ((AITrain)attachTrain).RemoveTrain();
            else
            {
                attachTrain.RemoveFromTrack();
                simulator.Trains.Remove(attachTrain);
                simulator.TrainDictionary.Remove(attachTrain.Number);
                simulator.NameDictionary.Remove(attachTrain.Name);
            }
            if (MultiPlayerManager.IsMultiPlayer())
            {
                MultiPlayerManager.Broadcast(new TrainCoupleMessage(this, attachTrain));
            }
            UpdateOccupancies();
            AddTrackSections();
            ResetActions(true);
            ReinitializeEOT();
            PhysicsUpdate(0);

        }

        //================================================================================================//
        /// <summary>
        /// Couple AI train to living train (AI or player) and leave cars to it; both remain alive in this case
        /// </summary>
        /// 

        public void LeaveCarsToLivingTrain(Train attachTrain, bool thisTrainFront, bool attachTrainFront)
        {
            // find set of cars between loco and attachtrain and pass them to train to attachtrain
            var passedLength = 0.0f;
            if (thisTrainFront)
            {
                while (0 < Cars.Count - 1)
                {
                    var car = Cars[0];
                    if (car is MSTSLocomotive)
                    {
                        break;
                    }
                    else
                    {
                        if (attachTrainFront)
                        {
                            attachTrain.Cars.Insert(0, car);
                            car.Train = attachTrain;
                            car.Flipped = !car.Flipped;
                            if (attachTrain.IsActualPlayerTrain && attachTrain.LeadLocomotiveIndex != -1)
                                attachTrain.LeadLocomotiveIndex++;
                        }
                        else
                        {
                            attachTrain.Cars.Add(car);
                            car.Train = attachTrain;
                        }
                        passedLength += car.CarLengthM;
                        attachTrain.Length += car.CarLengthM;
                        Length -= car.CarLengthM;
                        Cars.Remove(car);
                    }
                }
                Cars[0].SignalEvent(TrainEvent.Couple);
            }
            else
            {
                while (0 < Cars.Count - 1)
                {
                    var car = Cars[Cars.Count - 1];
                    if (car is MSTSLocomotive)
                    {
                        break;
                    }
                    else
                    {
                        if (!attachTrainFront)
                        {
                            attachTrain.Cars.Add(car);
                            car.Train = attachTrain;
                            car.Flipped = !car.Flipped;
                        }
                        else
                        {
                            attachTrain.Cars.Insert(0, car);
                            car.Train = attachTrain;
                            if (attachTrain.IsActualPlayerTrain && attachTrain.LeadLocomotiveIndex != -1)
                                attachTrain.LeadLocomotiveIndex++;
                        }
                        passedLength += car.CarLengthM;
                        attachTrain.Length += car.CarLengthM;
                        Length -= car.CarLengthM;
                        Cars.Remove(car);
                    }
                }
                Cars[Cars.Count - 1].SignalEvent(TrainEvent.Couple);
            }

            TerminateCoupling(attachTrain, thisTrainFront, attachTrainFront, passedLength);
        }

        //================================================================================================//
        /// <summary>
        /// Coupling AI train steals cars to coupled AI train
        /// </summary>

        public void StealCarsToLivingTrain(Train attachTrain, bool thisTrainFront, bool attachTrainFront)
        {
            var stealedLength = 0.0f;
            if (attachTrainFront)
            {
                while (0 < attachTrain.Cars.Count - 1)
                {
                    var car = attachTrain.Cars[0];
                    if (car is MSTSLocomotive)
                    {
                        // no other car to steal, leave to the attached train its loco
                        break;
                    }
                    else
                    {
                        if (thisTrainFront)
                        {
                            Cars.Insert(0, car);
                            car.Train = this;
                            car.Flipped = !car.Flipped;
                        }
                        else
                        {
                            Cars.Add(car);
                            car.Train = this;
                        }
                        stealedLength += car.CarLengthM;
                        Length += car.CarLengthM;
                        attachTrain.Length -= car.CarLengthM;
                        attachTrain.Cars.Remove(car);
                        if (attachTrain.IsActualPlayerTrain && attachTrain.LeadLocomotiveIndex != -1)
                            attachTrain.LeadLocomotiveIndex--;
                    }
                }
                attachTrain.Cars[0].SignalEvent(TrainEvent.Couple);
            }
            else
            {
                while (0 < attachTrain.Cars.Count - 1)
                {
                    var car = attachTrain.Cars[attachTrain.Cars.Count - 1];
                    if (car is MSTSLocomotive)
                    {
                        // ditto
                        break;
                    }
                    else
                    {
                        if (!thisTrainFront)
                        {
                            Cars.Add(car);
                            car.Train = this;
                            car.Flipped = !car.Flipped;
                        }
                        else
                        {
                            Cars.Insert(0, car);
                            car.Train = this;
                        }
                        stealedLength += car.CarLengthM;
                        Length += car.CarLengthM;
                        attachTrain.Length -= car.CarLengthM;
                        attachTrain.Cars.Remove(car);
                    }
                }
                attachTrain.Cars[attachTrain.Cars.Count - 1].SignalEvent(TrainEvent.Couple);
            }

            TerminateCoupling(attachTrain, thisTrainFront, attachTrainFront, -stealedLength);
        }

        //================================================================================================//
        /// <summary>
        /// Uncouple and perform housekeeping
        /// </summary>
        /// 
        public void TerminateCoupling(Train attachTrain, bool thisTrainFront, bool attachTrainFront, float passedLength)
        {

            // uncouple
            UncoupledFrom = attachTrain;
            attachTrain.UncoupledFrom = this;


            // recalculate position of coupling train
            if (thisTrainFront)  // coupled to front, so rear position is still valid
            {
                CalculatePositionOfCars();
                DistanceTravelledM -= passedLength;
                Cars[0].BrakeSystem.AngleCockAOpen = false;
            }
            else // coupled to rear so front position is still valid
            {
                RepositionRearTraveller();    // fix the rear traveller
                CalculatePositionOfCars();
                Cars[Cars.Count - 1].BrakeSystem.AngleCockBOpen = false;
            }

            // recalculate position of coupled train
            if (attachTrainFront)  // coupled to front, so rear position is still valid
            {
                attachTrain.CalculatePositionOfCars();
                attachTrain.DistanceTravelledM += passedLength;
                attachTrain.Cars[0].BrakeSystem.AngleCockAOpen = false;
            }
            else // coupled to rear so front position is still valid
            {
                attachTrain.RepositionRearTraveller();    // fix the rear traveller
                attachTrain.CalculatePositionOfCars();
                attachTrain.Cars[attachTrain.Cars.Count - 1].BrakeSystem.AngleCockBOpen = false;
            }


            // update positions of coupling train
            TrackNode tn = FrontTDBTraveller.TrackNode;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)FrontTDBTraveller.Direction.Reverse();

            PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            tn = RearTDBTraveller.TrackNode;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)RearTDBTraveller.Direction.Reverse();

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);

            // update positions of coupled train
            tn = attachTrain.FrontTDBTraveller.TrackNode;
            offset = attachTrain.FrontTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)attachTrain.FrontTDBTraveller.Direction.Reverse();

            attachTrain.PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PreviousPosition[Direction.Forward].UpdateFrom(attachTrain.PresentPosition[Direction.Forward]);

            tn = attachTrain.RearTDBTraveller.TrackNode;
            offset = attachTrain.RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)attachTrain.RearTDBTraveller.Direction.Reverse();

            attachTrain.PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            // set various items
            CheckFreight();
            SetDistributedPowerUnitIds();
            ReinitializeEOT();
            ActivityClearingDistanceM = Cars.Count < StandardTrainMinCarNo ? ShortClearingDistanceM : StandardClearingDistanceM;
            attachTrain.CheckFreight();
            attachTrain.SetDistributedPowerUnitIds();
            attachTrain.ReinitializeEOT();
            attachTrain.ActivityClearingDistanceM = attachTrain.Cars.Count < StandardTrainMinCarNo ? ShortClearingDistanceM : StandardClearingDistanceM;
            // anticipate reversal point and remove active action
            TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReverseReversalOffset = Math.Max(PresentPosition[Direction.Forward].Offset - 10f, 0.3f);
            if (PresentPosition[Direction.Forward].TrackCircuitSectionIndex != TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex)
            {
                TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            }
            if (PresentPosition[Direction.Backward].RouteListIndex < TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex)
                TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex = PresentPosition[Direction.Backward].RouteListIndex;
            // move WP, if any, just under the loco;
            AuxActionsContainer.MoveAuxActionAfterReversal(this);
            ResetActions(true);

            PhysicsUpdate(0);   // stop the wheels from moving etc

        }

        //================================================================================================//
        /// <summary>
        /// TestUncouple
        /// Tests if Waiting point delay >40000 and <59999; under certain conditions this means that
        /// an uncoupling action happens
        ///delay (in decimal notation) = 4NNSS (uncouple cars after NNth from train front (locos included), wait SS seconds)
        //                            or 5NNSS (uncouple cars before NNth from train rear (locos included), keep rear, wait SS seconds)
        /// remember that for AI trains train front is the one of the actual moving direction, so train front changes at every reverse point
        /// </summary>
        /// 
        public void TestUncouple(ref int delay)
        {
            if (delay <= 40000 || delay >= 60000)
                return;
            bool keepFront = true;
            int carsToKeep;
            if (delay > 50000 && delay < 60000)
            {
                keepFront = false;
                delay = delay - 10000;
            }
            carsToKeep = (delay - 40000) / 100;
            delay = delay - 40000 - carsToKeep * 100;
            if (IsActualPlayerTrain && TrainType == TrainType.AiPlayerDriven && this != simulator.OriginalPlayerTrain)
            {
                simulator.ActivityRun.SendActivityMessage(Name, $"Uncouple and keep coupled only {carsToKeep} {(keepFront ? "first" : "last")} cars");
            }
            else
                UncoupleSomeWagons(carsToKeep, keepFront);
        }

        //================================================================================================//
        /// <summary>
        /// UncoupleSomeWagons
        /// Uncouples some wagons, starting from rear if keepFront is true and from front if it is false
        /// Uncoupled wagons become a static consist
        /// </summary>
        /// 
        private void UncoupleSomeWagons(int carsToKeep, bool keepFront)
        {
            // first test that carsToKeep is smaller than number of cars of train
            if (carsToKeep >= Cars.Count)
            {
                carsToKeep = Cars.Count - 1;
                Trace.TraceWarning("Train {0} Service {1} reduced cars to uncouple", Number, Name);
            }
            // then test if there is at least one loco in the not-uncoupled part
            int startCarIndex = keepFront ? 0 : Cars.Count - carsToKeep;
            int endCarIndex = keepFront ? carsToKeep - 1 : Cars.Count - 1;
            bool foundLoco = false;
            for (int carIndex = startCarIndex; carIndex <= endCarIndex; carIndex++)
            {
                if (Cars[carIndex] is MSTSLocomotive)
                {
                    foundLoco = true;
                    break;
                }
            }
            if (!foundLoco)
            {
                // no loco in remaining part, abort operation
                Trace.TraceWarning("Train {0} Service {1} Uncoupling not executed, no loco in remaining part of train", Number, Name);
                return;
            }
            int uncouplePoint = keepFront ? carsToKeep - 1 : Cars.Count - carsToKeep - 1;
            simulator.UncoupleBehind(Cars[uncouplePoint], keepFront);


        }

        //================================================================================================//
        /// <summary>
        /// TestUncondAttach
        /// Tests if Waiting point delay =60001; under certain conditions this means that the train has to attach the nearby train

        /// </summary>
        /// 
        public void TestUncondAttach(ref int delay)
        {
            if (delay != 60001)
                return;
            else
            {
                if (IsActualPlayerTrain && this != simulator.OriginalPlayerTrain)
                {
                    simulator.ActivityRun.SendActivityMessage(Name, "You are involved in a join and split task; when you will couple to next train, you automatically will be switched to drive such next train");
                }
                delay = 0;
                UncondAttach = true;
            }
        }

        //================================================================================================//
        /// <summary>
        /// TestPermission
        /// Tests if Waiting point delay =60002; a permission request to pass next signal is launched.

        /// </summary>
        /// 
        public void TestPermission(ref int delay)
        {
            if (delay != 60002)
                return;
            else
            {
                delay = 20;
                if (IsActualPlayerTrain && TrainType == TrainType.AiPlayerDriven && this != simulator.OriginalPlayerTrain)
                {
                    simulator.ActivityRun.SendActivityMessage(Name, "Ask permission to pass signal (press TAB or Shift-TAB) and proceed");
                }
                else
                    RequestSignalPermission(ValidRoutes[Direction.Forward], 0);
            }
        }

        //================================================================================================//
        //
        // Request signal permission for AI trains (triggered by WP 60002)
        //

        public void RequestSignalPermission(TrackCircuitPartialPathRoute selectedRoute, int routeIndex)
        {
            // check if signal at danger

            TrackCircuitRouteElement thisElement = selectedRoute[PresentPosition[Direction.Forward].RouteListIndex];
            TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

            // no signal in required direction

            if (thisSection.EndSignals[thisElement.Direction] == null)
                return;

            var requestedSignal = thisSection.EndSignals[thisElement.Direction];
            if (requestedSignal.EnabledTrain != null && requestedSignal.EnabledTrain.Train != this)
                return;

            requestedSignal.EnabledTrain = routeIndex == 0 ? RoutedForward : RoutedBackward;
            requestedSignal.HoldState = SignalHoldState.None;
            requestedSignal.OverridePermission = SignalPermission.Requested;

            requestedSignal.CheckRouteState(false, requestedSignal.SignalRoute, RoutedForward, false);
        }

        internal override void RemoveTrain()
        {
            RemoveFromTrack();
            TrainDeadlockInfo.ClearDeadlocks();

            // remove train
            AI.TrainsToRemove.Add(this);
        }

        /// <summary>
        /// Suspend train because incorporated in other train
        /// </summary>
        public virtual void SuspendTrain(Train incorporatingTrain)
        {
            RemoveFromTrack();
            TrainDeadlockInfo.ClearDeadlocks();
            NextSignalObjects[Direction.Forward] = null;
            NextSignalObjects[Direction.Backward] = null;
            // reset AuxAction if any
            AuxActionsContainer.ResetAuxAction(this);
            TrainType = TrainType.AiIncorporated;
            LeadLocomotiveIndex = -1;
            Cars.Clear();
            RequiredActions.RemovePendingAIActionItems(true);
            UncondAttach = false;
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Join success: Train {0} service {1} has been incorporated into train {2} service {3}",
                Number, Name.Substring(0, Math.Min(Name.Length, 20)), incorporatingTrain.Number, incorporatingTrain.Name.Substring(0, Math.Min(incorporatingTrain.Name.Length, 20))));
        }

        //================================================================================================//
        /// <summary>
        /// Insert action item
        /// </summary>

        internal void CreateTrainAction(float presentSpeedMpS, float reqSpeedMpS, float distanceToTrainM,
                SignalItemInfo thisItem, AiActionType thisAction)
        {
            // if signal or speed limit take off clearing distance

            float activateDistanceTravelledM = PresentPosition[Direction.Forward].DistanceTravelled + distanceToTrainM;
            if (thisItem != null)
            {
                activateDistanceTravelledM -= simulator.TimetableMode ? ClearingDistance : ActivityClearingDistanceM;
            }

            // calculate braking distance

            float firstPartTime = 0.0f;
            float firstPartRangeM = 0.0f;
            float secondPartTime = 0.0f;
            float secondPartRangeM = 0.0f;
            float minReqRangeM = 0.0f;
            float remainingRangeM = activateDistanceTravelledM - PresentPosition[Direction.Forward].DistanceTravelled;

            float triggerDistanceM = PresentPosition[Direction.Forward].DistanceTravelled; // worst case

            // braking distance - use 0.22 * MaxDecelMpSS as average deceleration (due to braking delay)
            // T = deltaV / A
            // R = 0.5 * Vdelta * T + Vreq * T = 0.5 * (Vnow + Vreq) * T 
            // 0.5 * Vdelta is average speed over used time, 0.5 * Vdelta * T is related distance covered , Vreq * T is additional distance covered at minimal speed

            float fullPartTime = (AllowedMaxSpeedMpS - reqSpeedMpS) / (0.22f * MaxDecelMpSS);
            float fullPartRangeM = ((AllowedMaxSpeedMpS + reqSpeedMpS) * 0.5f * fullPartTime);

            // if present speed higher, brake distance is always required (same equation)
            if (presentSpeedMpS > reqSpeedMpS)
            {
                firstPartTime = (presentSpeedMpS - reqSpeedMpS) / (0.22f * MaxDecelMpSS);
                firstPartRangeM = ((presentSpeedMpS + reqSpeedMpS) * 0.5f * firstPartTime);
            }

            minReqRangeM = Math.Max(fullPartRangeM, firstPartRangeM);

            // if present speed below max speed, calculate distance required to accelerate to max speed (same equation)
            if (presentSpeedMpS < AllowedMaxSpeedMpS)
            {
                secondPartTime = (AllowedMaxSpeedMpS - presentSpeedMpS) / (0.5f * MaxAccelMpSS);
                secondPartRangeM = (AllowedMaxSpeedMpS + presentSpeedMpS) * 0.5f * secondPartTime;
            }

            // if full length possible, set as trigger distance
            if ((minReqRangeM + secondPartRangeM) < remainingRangeM)
            {
                triggerDistanceM = activateDistanceTravelledM - (fullPartRangeM + secondPartRangeM);
            }
            // if braking from full speed still possible, set as trigger distance
            // train will accelerate upto trigger point but probably not reach full speed, so there is enough braking distance available
            else if (minReqRangeM < remainingRangeM)
            {
                triggerDistanceM = activateDistanceTravelledM - fullPartRangeM;
            }
            // else if still possible, use minimun range based on present speed
            else if (firstPartRangeM < remainingRangeM)
            {
                triggerDistanceM = activateDistanceTravelledM - firstPartRangeM;
            }

            // correct trigger for approach distance but not backward beyond present position
            triggerDistanceM = Math.Max(PresentPosition[Direction.Forward].DistanceTravelled, triggerDistanceM - (3.0f * SignalApproachDistance));

            // for signal stop item : check if action allready in list, if so, remove (can be result of restore action)
            LinkedListNode<DistanceTravelledItem> thisItemLink = RequiredActions.First;
            bool itemFound = false;

            while (thisItemLink != null && !itemFound)
            {
                if (thisItemLink.Value is AIActionItem actionItem)
                {
                    if (actionItem.ActiveItem != null && actionItem.NextAction == thisAction)
                    {
                        if (actionItem.ActiveItem.SignalDetails.Index == thisItem.SignalDetails.Index)
                        {
                            // equal item, so remove it
                            RequiredActions.Remove(thisItemLink.Value);
                            itemFound = true;
                        }
                    }
                }
                if (!itemFound)
                {
                    thisItemLink = thisItemLink.Next;
                }
            }

            // create and insert action

            AIActionItem newAction = new AIActionItem(thisItem, thisAction);
            newAction.SetParam(triggerDistanceM, reqSpeedMpS, activateDistanceTravelledM, DistanceTravelledM);
            RequiredActions.InsertAction(newAction);
        }

        //================================================================================================//
        /// <summary>
        /// Insert action item for end-of-route
        /// </summary>

        public virtual void SetEndOfRouteAction()
        {
            // remaining length first section

            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
            float lengthToGoM = thisSection.Length - PresentPosition[Direction.Forward].Offset;
            if (TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1)
            {
                // go through all further sections

                for (int iElement = PresentPosition[Direction.Forward].RouteListIndex + 1; iElement < ValidRoutes[Direction.Forward].Count; iElement++)
                {
                    TrackCircuitRouteElement thisElement = ValidRoutes[Direction.Forward][iElement];
                    thisSection = thisElement.TrackCircuitSection;
                    lengthToGoM += thisSection.Length;
                }
            }
            else
                lengthToGoM = ComputeDistanceToReversalPoint();
            lengthToGoM -= 5.0f; // keep save distance from end

            // if last section does not end at signal at next section is switch, set back overlap to keep clear of switch
            // only do so for last subroute to avoid falling short of reversal points

            TrackCircuitRouteElement lastElement = ValidRoutes[Direction.Forward][ValidRoutes[Direction.Forward].Count - 1];

            CreateTrainAction(TrainMaxSpeedMpS, 0.0f, lengthToGoM, null, AiActionType.EndOfRoute);
            NextStopDistanceM = lengthToGoM;
        }

        //================================================================================================//
        /// <summary>
        /// Reset action list
        /// </summary>

        public void ResetActions(bool setEndOfPath, bool fromAutopilotSwitch = false)
        {
            // do not set actions for player train
            if (TrainType == TrainType.Player)
            {
                return;
            }

            // reset signal items processed state
            nextActionInfo = null;
            foreach (SignalItemInfo thisInfo in SignalObjectItems)
            {
                thisInfo.Processed = false;
            }

            // clear any outstanding actions
            RequiredActions.RemovePendingAIActionItems(false);

            // reset auxiliary actions
            AuxActionsContainer.SetAuxAction(this);

            // set next station stop in not at station
            if (StationStops.Count > 0)
            {
                SetNextStationAction(fromAutopilotSwitch);
            }

            // set end of path if required
            if (setEndOfPath)
            {
                SetEndOfRouteAction();
            }

            // to allow re-inserting of reversal action if necessary
            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalActionInserted == true)
            {
                TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalActionInserted = false;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Perform stored actions
        /// </summary>

        private protected override void PerformActions(List<DistanceTravelledItem> nowActions)
        {
            foreach (var thisAction in nowActions)
            {
                if (thisAction is ClearSectionItem)
                {
                    ClearOccupiedSection(thisAction as ClearSectionItem);
                }
                else if (thisAction is ActivateSpeedLimit)
                {
                    if (TrainType == TrainType.Player)
                    {
                        SetPendingSpeedLimit(thisAction as ActivateSpeedLimit);
                    }
                    else
                    {
                        SetAIPendingSpeedLimit(thisAction as ActivateSpeedLimit);
                    }
                }
                else if (thisAction is ClearMovingTableAction)
                {
                    ClearMovingTable(thisAction);
                }
                else if (thisAction is AIActionItem && !(thisAction is AuxActionItem))
                {
                    ProcessActionItem(thisAction as AIActionItem);
                }
                else if (thisAction is AuxActionWPItem)
                {
                    var valid = ((AuxActionItem)thisAction).ValidAction(this);
                    if (valid && TrainType == TrainType.AiPlayerDriven)
                    {
                        var presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
                        ((AuxActionItem)thisAction).ProcessAction(this, presentTime);
                    }
                }
                else if (thisAction is AuxActionItem)
                {
                    var presentTime = 0;
                    if (!PreUpdate)
                        presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
                    else
                        presentTime = Convert.ToInt32(Math.Floor(AI.ClockTime));
                    var actionState = ((AuxActionItem)thisAction).ProcessAction(this, presentTime);
                    if (actionState != AiMovementState.InitAction && actionState != AiMovementState.HandleAction)
                        MovementState = actionState;
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set pending speed limits
        /// </summary>

        internal void SetAIPendingSpeedLimit(ActivateSpeedLimit speedInfo)
        {
            if (speedInfo.MaxSpeedMpSSignal > 0)
            {
                AllowedMaxSpeedSignalMpS = simulator.TimetableMode ? speedInfo.MaxSpeedMpSSignal : allowedAbsoluteMaxSpeedSignalMpS;
                AllowedMaxSpeedMpS = Math.Min(speedInfo.MaxSpeedMpSSignal, Math.Min(AllowedMaxSpeedLimitMpS, allowedMaxTempSpeedLimitMpS));
            }
            if (speedInfo.MaxSpeedMpSLimit > 0)
            {
                AllowedMaxSpeedLimitMpS = simulator.TimetableMode ? speedInfo.MaxSpeedMpSLimit : allowedAbsoluteMaxSpeedLimitMpS;
                if (simulator.TimetableMode)
                    AllowedMaxSpeedMpS = speedInfo.MaxSpeedMpSLimit;
                else
                    AllowedMaxSpeedMpS = Math.Min(speedInfo.MaxSpeedMpSLimit, Math.Min(AllowedMaxSpeedSignalMpS, allowedMaxTempSpeedLimitMpS));
            }
            if (speedInfo.MaxTempSpeedMpSLimit > 0)
            {
                allowedMaxTempSpeedLimitMpS = allowedAbsoluteMaxTempSpeedLimitMpS;
                AllowedMaxSpeedMpS = Math.Min(speedInfo.MaxTempSpeedMpSLimit, Math.Min(AllowedMaxSpeedSignalMpS, AllowedMaxSpeedLimitMpS));
            }
            // <CScomment> following statement should be valid in general, as it seems there was a bug here in the original SW
            AllowedMaxSpeedMpS = Math.Min(AllowedMaxSpeedMpS, TrainMaxSpeedMpS);

            if (MovementState == AiMovementState.Running && SpeedMpS < AllowedMaxSpeedMpS - 2.0f * SpeedHysteris)
            {
                MovementState = AiMovementState.Accelerating;
                Alpha10 = PreUpdate & !simulator.TimetableMode ? 2 : 10;
            }
            // reset pending actions to recalculate braking distance

            ResetActions(true);
        }

        //================================================================================================//
        /// <summary>
        /// Process pending actions
        /// </summary>

        private void ProcessActionItem(AIActionItem thisItem)
        {
            // normal actions

            bool actionValid = true;
            bool actionCleared = false;

            // if signal speed, check if still set

            if (thisItem.NextAction == AiActionType.SpeedSignal)
            {
                if (thisItem.ActiveItem.ActualSpeed == AllowedMaxSpeedMpS)  // no longer valid
                {
                    actionValid = false;
                }
                else if (thisItem.ActiveItem.ActualSpeed != thisItem.RequiredSpeedMpS)
                {
                    actionValid = false;
                }
            }

            // if signal, check if not held for station stop (station stop comes first)

            else if (thisItem.NextAction == AiActionType.SignalAspectStop)
            {
                if (thisItem.ActiveItem.SignalState == SignalAspectState.Stop &&
                    thisItem.ActiveItem.SignalDetails.HoldState == SignalHoldState.StationStop)
                {
                    // check if train is approaching or standing at station and has not yet departed
                    if (StationStops != null && StationStops.Count >= 1 && AtStation && StationStops[0].ExitSignal == thisItem.ActiveItem.SignalDetails.Index)
                    {
                        actionValid = false;
                    }
                }

                // check if cleared

                else if (thisItem.ActiveItem.SignalState >= SignalAspectState.Approach_1)
                {
                    actionValid = false;
                    actionCleared = true;
                }

                // check if restricted

                else if (thisItem.ActiveItem.SignalState != SignalAspectState.Stop)
                {
                    thisItem.NextAction = AiActionType.SignalAspectRestricted;
                    if (((thisItem.ActivateDistanceM - PresentPosition[Direction.Forward].DistanceTravelled) < SignalApproachDistance) ||
                         thisItem.ActiveItem.SignalDetails.SignalNoSpeedReduction(SignalFunction.Normal))
                    {
                        actionValid = false;
                        actionCleared = true;
                    }
                }

                // recalculate braking distance if train is running slow
                if (actionValid && SpeedMpS < PresetCreepSpeed)
                {
                    float firstPartTime = 0.0f;
                    float firstPartRangeM = 0.0f;
                    float secndPartRangeM = 0.0f;
                    float remainingRangeM = thisItem.ActivateDistanceM - PresentPosition[Direction.Forward].DistanceTravelled;

                    if (SpeedMpS > thisItem.RequiredSpeedMpS)   // if present speed higher, brake distance is always required
                    {
                        firstPartTime = (SpeedMpS - thisItem.RequiredSpeedMpS) / (0.25f * MaxDecelMpSS);
                        firstPartRangeM = 0.25f * MaxDecelMpSS * (firstPartTime * firstPartTime);
                    }

                    if (firstPartRangeM < remainingRangeM && SpeedMpS < TrainMaxSpeedMpS) // if distance left and not at max speed
                    // split remaining distance based on relation between acceleration and deceleration
                    {
                        secndPartRangeM = (remainingRangeM - firstPartRangeM) * (2.0f * MaxDecelMpSS) / (MaxDecelMpSS + MaxAccelMpSS);
                    }

                    float fullRangeM = firstPartRangeM + secndPartRangeM;
                    if (fullRangeM < remainingRangeM && remainingRangeM > 300.0f) // if range is shorter and train not too close, reschedule
                    {
                        actionValid = false;
                        thisItem.RequiredDistance = thisItem.ActivateDistanceM - fullRangeM;
                        RequiredActions.InsertAction(thisItem);
                    }

                }
            }

            // if signal at RESTRICTED, check if not cleared

            else if (thisItem.NextAction == AiActionType.SignalAspectRestricted)
            {
                if (thisItem.ActiveItem.SignalState >= SignalAspectState.Approach_1 ||
                (thisItem.ActivateDistanceM - PresentPosition[Direction.Forward].DistanceTravelled) < SignalApproachDistance)
                {
                    actionValid = false;
                }
            }

            // get station stop, recalculate with present speed if required

            else if (thisItem.NextAction == AiActionType.StationStop)
            {
                float[] distancesM = CalculateDistancesToNextStation(StationStops[0], SpeedMpS, true);

                if (distancesM[1] - 300.0f > DistanceTravelledM) // trigger point more than 300m away
                {
                    actionValid = false;
                    thisItem.RequiredDistance = distancesM[1];
                    thisItem.ActivateDistanceM = distancesM[0];
                    RequiredActions.InsertAction(thisItem);
                }
                else
                // always copy active stop distance
                {
                    thisItem.ActivateDistanceM = distancesM[0];
                }
            }

            EndProcessAction(actionValid, thisItem, actionCleared);
        }

        //  SPA:    To be able to call it by AuxActionItems
        internal void EndProcessAction(bool actionValid, AIActionItem thisItem, bool actionCleared)
        {
            // if still valid - check if at station and signal is exit signal
            // if so, use minimum distance of both items to ensure train stops in time for signal

            if (actionValid && nextActionInfo != null &&
                nextActionInfo.NextAction == AiActionType.StationStop &&
                thisItem.NextAction == AiActionType.SignalAspectStop)
            {
                int signalIdent = thisItem.ActiveItem.SignalDetails.Index;
                if (signalIdent == StationStops[0].ExitSignal)
                {
                    actionValid = false;
                    nextActionInfo.ActivateDistanceM = Math.Min(nextActionInfo.ActivateDistanceM, thisItem.ActivateDistanceM);
                }
            }

            // if still valid, check if this action is end of route and actual next action is station stop - if so, reject
            if (actionValid && nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop &&
                thisItem.NextAction == AiActionType.EndOfRoute)
            {
                actionValid = false;
            }

            // if still valid - check if actual next action is WP and signal is WP controlled signal
            // if so, use minimum distance of both items to ensure train stops in time for signal

            if (actionValid && nextActionInfo != null && nextActionInfo is AuxActionWPItem &&
                thisItem.NextAction == AiActionType.SignalAspectStop)
            {
                if ((thisItem.ActiveItem.SignalDetails.HasLockForTrain(Number, TCRoute.ActiveSubPath) && nextActionInfo.ActivateDistanceM - thisItem.ActivateDistanceM < 40) ||
                    nextActionInfo.ActivateDistanceM - thisItem.ActivateDistanceM < ActivityClearingDistanceM)
                {
                    actionValid = false;
                    nextActionInfo.ActivateDistanceM = Math.Min(nextActionInfo.ActivateDistanceM, thisItem.ActivateDistanceM);
                }
            }

            // if still valid - check if more severe as existing action

            bool earlier = false;

            if (actionValid)
            {
                if (nextActionInfo != null)
                {
                    if (thisItem.ActivateDistanceM < nextActionInfo.ActivateDistanceM)
                    {
                        if (thisItem.RequiredSpeedMpS <= nextActionInfo.RequiredSpeedMpS)
                        {
                            earlier = true;
                        }
                        else  // new requirement earlier with higher speed - check if enough braking distance remaining
                        {
                            float deltaTime = (thisItem.RequiredSpeedMpS - nextActionInfo.RequiredSpeedMpS) / MaxDecelMpSS;
                            float brakingDistanceM = (thisItem.RequiredSpeedMpS * deltaTime) + (0.5f * MaxDecelMpSS * deltaTime * deltaTime);

                            if (brakingDistanceM < (nextActionInfo.ActivateDistanceM - thisItem.ActivateDistanceM))
                            {
                                earlier = true;
                            }
                        }
                    }
                    else if (thisItem.RequiredSpeedMpS < nextActionInfo.RequiredSpeedMpS)
                    // new requirement further but with lower speed - check if enough braking distance left
                    {
                        float deltaTime = (nextActionInfo.RequiredSpeedMpS - thisItem.RequiredSpeedMpS) / MaxDecelMpSS;
                        float brakingDistanceM = (nextActionInfo.RequiredSpeedMpS * deltaTime) + (0.5f * MaxDecelMpSS * deltaTime * deltaTime);

                        if (brakingDistanceM > (thisItem.ActivateDistanceM - nextActionInfo.ActivateDistanceM))
                        {
                            earlier = true;
                        }
                    }

                    // if earlier : check if present action is station stop, new action is signal - if so, check is signal really in front of or behind station stop

                    if (earlier && thisItem.NextAction == AiActionType.SignalAspectStop &&
                                 nextActionInfo.NextAction == AiActionType.StationStop)
                    {
                        float newposition = thisItem.ActivateDistanceM + 0.75f * ActivityClearingDistanceM; // correct with clearing distance - leave smaller gap
                        float actposition = nextActionInfo.ActivateDistanceM;

                        if (actposition < newposition)
                            earlier = false;

                        // if still earlier : check if signal really beyond start of platform
                        if (earlier && (StationStops[0].DistanceToTrainM - thisItem.ActiveItem.DistanceToTrain) < StationStops[0].StopOffset)
                        {
                            earlier = false;
                            StationStops[0].DistanceToTrainM = thisItem.ActiveItem.DistanceToTrain - 1;
                            nextActionInfo.ActivateDistanceM = thisItem.ActivateDistanceM - 1;
                        }
                    }

                    // check if present action is signal and new action is station - if so, check actual position of signal in relation to stop

                    if (thisItem.NextAction == AiActionType.StationStop && nextActionInfo.NextAction == AiActionType.SignalAspectStop)
                    {
                        if (StationStops[0].DistanceToTrainM < nextActionInfo.ActiveItem.DistanceToTrain)
                        {
                            earlier = true;
                        }
                    }

                    // if not earlier and station stop and present action is signal stop : check if signal is hold signal, if so set station stop
                    // set distance to signal if that is less than distance to platform to ensure trains stops at signal

                    if (!earlier && thisItem.NextAction == AiActionType.StationStop &&
                               nextActionInfo.NextAction == AiActionType.SignalAspectStop)
                    {
                        if (HoldingSignals.Contains(nextActionInfo.ActiveItem.SignalDetails.Index))
                        {
                            earlier = true;
                            thisItem.ActivateDistanceM = Math.Min(nextActionInfo.ActivateDistanceM, thisItem.ActivateDistanceM);
                        }
                    }

                    // if not earlier and station stop and present action is end of route : favour station

                    if (!earlier && thisItem.NextAction == AiActionType.StationStop &&
                               (nextActionInfo.NextAction == AiActionType.EndOfRoute || nextActionInfo.NextAction == AiActionType.EndOfAuthority))
                    {
                        earlier = true;
                        nextActionInfo.ActivateDistanceM = thisItem.ActivateDistanceM + 1.0f;
                    }

                    // if not earlier and is a waiting point and present action is signal stop : check if signal is locking signal, if so set waiting
                    // set distance to signal if that is less than distance to WP to ensure trains stops at signal

                    if (!earlier && thisItem is AuxActionWPItem &&
                               nextActionInfo.NextAction == AiActionType.SignalAspectStop)
                    {
                        // check if it is the the AI action is related to the signal linked to the WP
                        if ((nextActionInfo.ActiveItem.SignalDetails.HasLockForTrain(Number, TCRoute.ActiveSubPath) && thisItem.ActivateDistanceM - nextActionInfo.ActivateDistanceM < 40) ||
                            thisItem.ActivateDistanceM - nextActionInfo.ActivateDistanceM < ActivityClearingDistanceM)
                        {
                            earlier = true;
                            thisItem.ActivateDistanceM = Math.Min(nextActionInfo.ActivateDistanceM, thisItem.ActivateDistanceM);
                        }
                    }

                    if (MovementState == AiMovementState.InitAction || MovementState == AiMovementState.HandleAction)
                        earlier = false;

                    // reject if less severe (will be rescheduled if active item is cleared)

                    if (!earlier)
                    {
                        actionValid = false;
                    }
                    else
                    {
                    }
                }
            }

            // if still valid, set as action, set state to braking if still running

            var stationCancelled = false;
            if (actionValid)
            {
                if (thisItem.GetType().IsSubclassOf(typeof(AuxActionItem)))
                {
                    AuxActionItem action = thisItem as AuxActionItem;
                    AuxActionRef actionRef = action.ActionRef;
                    if (actionRef.GenericAction)
                    {
                        nextGenAction = thisItem;   //  SPA In order to manage GenericAuxAction without disturbing normal actions
                        RequiredActions.Remove(thisItem);
                    }
                    else
                    {
                        if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop)
                            stationCancelled = true;
                        nextActionInfo = thisItem;
                    }
                }
                else
                {
                    if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop)
                        stationCancelled = true;
                    nextActionInfo = thisItem;
                }
                if (nextActionInfo.RequiredSpeedMpS == 0)
                {
                    NextStopDistanceM = thisItem.ActivateDistanceM - PresentPosition[Direction.Forward].DistanceTravelled;
                    if (simulator.PreUpdate && !(nextActionInfo.NextAction == AiActionType.AuxiliaryAction && NextStopDistanceM > MinCheckDistanceM))
                    {
                        AITrainBrakePercent = 100; // because of short reaction time
                        AITrainThrottlePercent = 0;
                    }
                }
                if (MovementState != AiMovementState.StationStop &&
                    MovementState != AiMovementState.Stopped &&
                    MovementState != AiMovementState.HandleAction &&
                    MovementState != AiMovementState.Following &&
                    MovementState != AiMovementState.Turntable &&
                    MovementState != AiMovementState.Braking)
                {
                    MovementState = AiMovementState.Braking;
                    Alpha10 = PreUpdate & !simulator.TimetableMode ? 2 : 10;
                }
                else if (MovementState == AiMovementState.Stopped && nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem)))
                {
                    MovementState = AiMovementState.Braking;
                    Alpha10 = PreUpdate ? 2 : 10;
                }
            }

            if (actionCleared)
            {
                // reset actions - ensure next action is validated
                ResetActions(true);
            }
            else if (stationCancelled)
            {
                SetNextStationAction(false);
            }
        }

        public bool TrainHasPower()
        {
            foreach (var car in Cars)
            {
                if (car is MSTSLocomotive)
                {
                    return (true);
                }
            }

            return (false);
        }

        //================================================================================================//
        //
        // Extra actions when alternative route is set
        //

        internal override void SetAlternativeRoutePathBased(int startElementIndex, int altRouteIndex, Signal nextSignal)
        {
            base.SetAlternativeRoutePathBased(startElementIndex, altRouteIndex, nextSignal);

            // reset actions to recalculate distances

            ResetActions(true);
        }

        internal override void SetAlternativeRouteLocationBased(int startSectionIndex, DeadlockInfo sectionDeadlockInfo, int usedPath, Signal nextSignal)
        {
            base.SetAlternativeRouteLocationBased(startSectionIndex, sectionDeadlockInfo, usedPath, nextSignal);

            // reset actions to recalculate distances

            ResetActions(true);
        }

        //================================================================================================//
        //
        // Find station on alternative route
        //
        //

        private protected override StationStop SetAlternativeStationStop(StationStop orgStop, TrackCircuitPartialPathRoute newRoute)
        {
            var newStop = base.SetAlternativeStationStop(orgStop, newRoute);
            if (newStop != null)
            {
                // Modify PlatformStartID in ServiceList
                var actualServiceItem = ServiceDefinition.Find(si => si.PlatformStartID == orgStop.PlatformReference);
                if (actualServiceItem != null)
                {
                    actualServiceItem.SetAlternativeStationStop(newStop.PlatformReference);
                }
            }
            return newStop;
        }

        //================================================================================================//
        /// <summary>
        /// When in autopilot mode, switches to player control
        /// </summary>
        /// 
        public bool SwitchToPlayerControl()
        {
            bool success = false;
            int leadLocomotiveIndex = -1;
            var j = 0;
            foreach (TrainCar car in Cars)
            {
                if (car is MSTSLocomotive)
                {
                    var loco = car as MSTSLocomotive;
                    loco.LocomotiveAxle.Reset(simulator.GameTime, SpeedMpS);
                    loco.AntiSlip = false; // <CSComment> TODO Temporary patch until AntiSlip is re-implemented
                }
                if (car == simulator.PlayerLocomotive)
                { leadLocomotiveIndex = j; }
                j++;
            }
            MSTSLocomotive lead = (MSTSLocomotive)simulator.PlayerLocomotive;
            BrakeSystem.EqualReservoirPressurePSIorInHg = Math.Min(BrakeSystem.EqualReservoirPressurePSIorInHg, lead.TrainBrakeController.MaxPressurePSI);
            foreach (TrainCar car in Cars)
            {
                if (car.BrakeSystem is AirSinglePipe)
                {
                    ((AirSinglePipe)car.BrakeSystem).NormalizePressures(lead.TrainBrakeController.MaxPressurePSI);
                }
            }
            LeadLocomotiveIndex = leadLocomotiveIndex;
            simulator.PlayerLocomotive.SwitchToPlayerControl();
            if (MovementState == AiMovementState.HandleAction && nextActionInfo != null && nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem))
                && AuxActionsContainer[0] != null && ((AIAuxActionsRef)AuxActionsContainer[0]).NextAction == AuxiliaryAction.WaitingPoint)
            {
                (AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).WaitingPoint.currentMvmtState = AiMovementState.HandleAction;
            }
            TrainType = TrainType.AiPlayerDriven;
            success = true;
            return success;
        }

        //================================================================================================//
        /// <summary>
        /// When in autopilot mode, switches to autopilot control
        /// </summary>
        /// 
        public bool SwitchToAutopilotControl()
        {
            bool success = false;
            // MUDirection set within following method call
            simulator.PlayerLocomotive.SwitchToAutopilotControl();
            LeadLocomotive = null;
            LeadLocomotiveIndex = -1;
            TrainType = TrainType.AiPlayerHosting;
            InitializeBrakes();
            foreach (TrainCar car in Cars)
            {
                if (car is MSTSLocomotive)
                {
                    var loco = car as MSTSLocomotive;
                    if (loco.EngineBrakeController != null)
                        loco.SetEngineBrakePercent(0);
                    if (loco.DynamicBrakeController != null)
                        loco.DynamicBrakePercent = -1;
                }
            }

            if (FirstCar != null)
            {
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
                if (FirstCar is MSTSLocomotive)
                    ((MSTSLocomotive)FirstCar).SetTrainBrakePercent(AITrainBrakePercent);
                if (simulator.PlayerLocomotive != null && FirstCar != simulator.PlayerLocomotive)
                {
                    simulator.PlayerLocomotive.BrakeSystem.AISetPercent(AITrainBrakePercent);
                    ((MSTSLocomotive)simulator.PlayerLocomotive).SetTrainBrakePercent(AITrainBrakePercent);
                }
            }
            ResetActions(true, true);
            if (SpeedMpS != 0)
                MovementState = AiMovementState.Braking;
            else if (this == simulator.OriginalPlayerTrain && simulator.ActivityRun != null && simulator.ActivityRun.ActivityTask is ActivityTaskPassengerStopAt at && TrainAtStation() &&
                at.BoardingS > 0)
            {
                StationStops[0].ActualDepart = (int)at.BoardingEndS;
                StationStops[0].ActualArrival = (int)at.ActualArrival.GetValueOrDefault(at.ScheduledArrival).TotalSeconds;
                MovementState = AiMovementState.StationStop;
            }
            else if (this != simulator.OriginalPlayerTrain && AtStation)
            {
                MovementState = AiMovementState.StationStop;
            }
            else if (Math.Abs(SpeedMpS) <= 0.1f && ((AuxActionsContainer.SpecAuxActions.Count > 0 && AuxActionsContainer.SpecAuxActions[0] is AIActionWPRef && (AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).WaitingPoint != null &&
            (AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).WaitingPoint.currentMvmtState == AiMovementState.HandleAction) || (nextActionInfo is AuxActionWPItem &&
                    MovementState == AiMovementState.HandleAction)))
            {
                MovementState = AiMovementState.HandleAction;
            }
            else
                MovementState = AiMovementState.Stopped;
            success = true;
            return success;
        }

        //================================================================================================//
        /// <summary>
        /// Check on station tasks, required when player train is not original player train
        /// </summary>
        protected override void CheckStationTask()
        {
            // if at station
            if (AtStation)
            {
                int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
                int eightHundredHours = 8 * 3600;
                int sixteenHundredHours = 16 * 3600;

                // if moving, set departed
                if (Math.Abs(SpeedMpS) > 0)
                {
                    if (TrainType != TrainType.AiPlayerHosting)
                    {
                        StationStops[0].ActualDepart = presentTime;
                        StationStops[0].Passed = true;
                        Delay = TimeSpan.FromSeconds((presentTime - StationStops[0].DepartTime) % (24 * 3600));
                        PreviousStop = StationStops[0].CreateCopy();
                        StationStops.RemoveAt(0);
                    }
                    AtStation = false;
                    MayDepart = false;
                    DisplayMessage = "";
                }
                else
                {

                    {
                        double remaining;
                        if (StationStops.Count == 0)
                        {
                            remaining = 0;
                        }
                        else
                        {
                            double actualDepart = StationStops[0].ActualDepart;
                            int correctedTime = presentTime;
                            if (presentTime > sixteenHundredHours && StationStops[0].DepartTime < eightHundredHours)
                            {
                                correctedTime = presentTime - 24 * 3600;  // correct to time before midnight (negative value!)
                            }

                            remaining = actualDepart - correctedTime;
                        }

                        // set display text color
                        if (remaining < 1)
                        {
                            DisplayColor = Color.LightGreen;
                        }
                        else if (remaining < 11)
                        {
                            DisplayColor = new Color(255, 255, 128);
                        }
                        else
                        {
                            DisplayColor = Color.White;
                        }

                        // clear holding signal
                        if (remaining < (IsActualPlayerTrain ? 120 : 2) && remaining > 0 && StationStops[0].ExitSignal >= 0) // within two minutes of departure and hold signal?
                        {
                            HoldingSignals.Remove(StationStops[0].ExitSignal);

                            if (ControlMode == TrainControlMode.AutoSignal)
                            {
                                Signal nextSignal = Simulator.Instance.SignalEnvironment.Signals[StationStops[0].ExitSignal];
                                nextSignal.RequestClearSignal(ValidRoutes[Direction.Forward], RoutedForward, 0, false, null);
                            }
                            StationStops[0].ExitSignal = -1;
                        }

                        // check departure time
                        if (remaining <= 0)
                        {
                            if (!MayDepart)
                            {
                                float distanceToNextSignal = -1;
                                if (NextSignalObjects[Direction.Forward] != null)
                                    distanceToNextSignal = NextSignalObjects[Direction.Forward].DistanceTo(FrontTDBTraveller);
                                // check if signal ahead is cleared - if not, do not allow depart
                                if (NextSignalObjects[Direction.Forward] != null && distanceToNextSignal >= 0 && distanceToNextSignal < 300 &&
                                        NextSignalObjects[Direction.Forward].SignalLR(SignalFunction.Normal) == SignalAspectState.Stop
                                    && NextSignalObjects[Direction.Forward].OverridePermission != SignalPermission.Granted)
                                {
                                    DisplayMessage = Simulator.Catalog.GetString("Passenger boarding completed. Waiting for signal ahead to clear.");
                                }
                                else
                                {
                                    MayDepart = true;
                                    DisplayMessage = Simulator.Catalog.GetString("Passenger boarding completed. You may depart now.");
                                    simulator.SoundNotify = TrainEvent.PermissionToDepart;
                                }
                            }
                        }
                        else
                        {
                            DisplayMessage = Simulator.Catalog.GetString("Passenger boarding completes in {0:D2}:{1:D2}",
                                remaining / 60, remaining % 60);
                        }
                    }
                }
            }
            else
            {
                // if stations to be checked
                if (StationStops.Count > 0)
                {
                    // check if stopped at station
                    if (Math.Abs(SpeedMpS) == 0.0f)
                    {
                        AtStation = AtPlatform();
                        if (AtStation)
                        {
                            int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
                            StationStops[0].ActualArrival = presentTime;
                            StationStops[0].CalculateDepartTime(this);
                        }
                    }
                    else if (ControlMode == TrainControlMode.AutoNode || ControlMode == TrainControlMode.AutoSignal)
                    {
                        // check if station missed : station must be at least 250m. behind us
                        bool missedStation = MissedPlatform(250);

                        if (missedStation)
                        {
                            PreviousStop = StationStops[0].CreateCopy();
                            if (TrainType != TrainType.AiPlayerHosting)
                                StationStops.RemoveAt(0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Restarts waiting train due to event triggered by player train
        /// </summary>
        public void RestartWaitingTrain(RestartWaitingTrain restartWaitingTrain)
        {
            var delayToRestart = restartWaitingTrain.DelayToRestart;
            var matchingWPDelay = restartWaitingTrain.MatchingWPDelay;
            int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
            var roughActualDepart = presentTime + delayToRestart;
            if (MovementState == AiMovementState.HandleAction && (((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay == matchingWPDelay ||
                (AuxActionsContainer.SpecificRequiredActions.Count > 0 && ((AuxActSigDelegate)(AuxActionsContainer.SpecificRequiredActions).First.Value).currentMvmtState == AiMovementState.HandleAction &&
                (((AuxActSigDelegate)(AuxActionsContainer.SpecificRequiredActions).First.Value).ActionRef as AIActSigDelegateRef).Delay == matchingWPDelay)))
            {
                if (((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay >= 30000 && ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay < 32400)
                // absolute WP, use minutes as unit of measure
                {
                    (nextActionInfo as AuxActionWPItem).ActualDepart = (roughActualDepart / 60) * 60 + (roughActualDepart % 60 == 0 ? 0 : 60);
                    // compute hrs and minutes
                    var hrs = (nextActionInfo as AuxActionWPItem).ActualDepart / 3600;
                    var minutes = ((nextActionInfo as AuxActionWPItem).ActualDepart - hrs * 3600) / 60;
                    ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay = 30000 + minutes + hrs * 100;
                    (nextActionInfo as AuxActionWPItem).SetDelay(30000 + minutes + hrs * 100);
                }
                else
                {
                    (nextActionInfo as AuxActionWPItem).ActualDepart = roughActualDepart;
                    ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay = delayToRestart;
                    (nextActionInfo as AuxActionWPItem).SetDelay(delayToRestart);
                }
                if (((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).LinkedAuxAction)
                // also a signal is connected with this WP
                {
                    if (AuxActionsContainer.SpecificRequiredActions.Count > 0 && AuxActionsContainer.SpecificRequiredActions.First.Value is AuxActSigDelegate)
                    // if should be true only for absolute WPs, where the linked aux action is started in parallel
                    {
                        (AuxActionsContainer.SpecificRequiredActions.First.Value as AuxActSigDelegate).ActualDepart = (nextActionInfo as AuxActionWPItem).ActualDepart;
                        ((AuxActionsContainer.SpecificRequiredActions.First.Value as AuxActSigDelegate).ActionRef as AIActSigDelegateRef).Delay = ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay;
                    }
                }

            }
            else if (nextActionInfo != null & nextActionInfo is AuxActionWPItem && ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay == matchingWPDelay)
            {
                var actualDepart = 0;
                var delay = 0;
                // not yet at WP
                if (((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay >= 30000 && ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay < 32400)
                {
                    // compute hrs and minutes
                    var hrs = roughActualDepart / 3600;
                    var minutes = (roughActualDepart - hrs * 3600) / 60;
                    ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay = 30000 + minutes + hrs * 100;
                    (nextActionInfo as AuxActionWPItem).SetDelay(30000 + minutes + hrs * 100);
                    if (AuxActionsContainer.SpecAuxActions.Count > 0 && AuxActionsContainer.SpecAuxActions[0] is AIActionWPRef)
                        (AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).Delay = 30000 + minutes + hrs * 100;
                    actualDepart = (roughActualDepart / 60) * 60 + (roughActualDepart % 60 == 0 ? 0 : 60);
                    delay = ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay;
                }
                else
                {
                    ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay = delayToRestart;
                    (nextActionInfo as AuxActionWPItem).SetDelay(delayToRestart);
                    actualDepart = roughActualDepart;
                    delay = 1;
                }
                if (((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).LinkedAuxAction)
                // also a signal is connected with this WP
                {
                    if (AuxActionsContainer.SpecificRequiredActions.Count > 0 && AuxActionsContainer.SpecificRequiredActions.First.Value is AuxActSigDelegate)
                    // if should be true only for absolute WPs, where the linked aux action is started in parallel
                    {
                        (AuxActionsContainer.SpecificRequiredActions.First.Value as AuxActSigDelegate).ActualDepart = actualDepart;
                        ((AuxActionsContainer.SpecificRequiredActions.First.Value as AuxActSigDelegate).ActionRef as AIActSigDelegateRef).Delay = ((nextActionInfo as AuxActionWPItem).ActionRef as AIActionWPRef).Delay;
                    }
                    if (AuxActionsContainer.SpecAuxActions.Count > 1 && AuxActionsContainer.SpecAuxActions[1] is AIActSigDelegateRef)
                        (AuxActionsContainer.SpecAuxActions[1] as AIActSigDelegateRef).Delay = delay;
                }
            }
        }

        private protected override INameValueInformationProvider GetDispatcherInfoProvider() => new AiTrainDispatcherInfo(this);

        private protected class AiTrainDispatcherInfo : TrainDispatcherInfo
        {
            private readonly AITrain train;

            public AiTrainDispatcherInfo(AITrain train) : base(train)
            {
                this.train = train;
            }

            /// Add movement status to train status string
            /// Update the string for 'TextPageDispatcherInfo' in case of AI train.
            /// Modifiy fields 4, 5, 7, 8 & 11
            /// 4   AIMode :
            ///     INI     : AI is in INIT mode
            ///     STC     : AI is static
            ///     STP     : AI is Stopped
            ///     BRK     : AI Brakes
            ///     ACC     : AI do acceleration
            ///     FOL     : AI follows
            ///     RUN     : AI is running
            ///     EOP     : AI approch and of path
            ///     STA     : AI is on Station Stop
            ///     WTP     : AI is on Waiting Point
            /// 5   AI Data :
            ///     000&000     : for mode INI, BRK, ACC, FOL, RUN or EOP
            ///     HH:mm:ss    : for mode STA or WTP with actualDepart or DepartTime
            ///                 : for mode STC with Start Time Value
            ///     ..:..:..    : For other case
            /// 7   Next Action : 
            ///     SPDL    :   Speed limit
            ///     SIGL    :   Speed signal
            ///     STOP    :   Signal STOP
            ///     REST    :   Signal RESTRICTED
            ///     EOA     :   End Of Authority
            ///     STAT    :   Station Stop
            ///     TRAH    :   Train Ahead
            ///     EOR     :   End Of Route
            ///     NONE    :   None
            /// 8   Distance :
            ///     Distance to
            /// 11  Train Name

            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    base.Update(gameTime);
                    this["AiMode"] = train.MovementState.ToString();

                    if (train.TrainType == TrainType.Player || (train.TrainType == TrainType.Remote && MultiPlayerManager.IsServer()) || train.IsActualPlayerTrain)
                    {
                        this["AiMode"] = null;
                    }

                    switch (train.MovementState)
                    {
                        case AiMovementState.StationStop:
                            this["AiMode"] = train.StationStops[0].StopType.ToString();
                            this["AiData"] = train.StationStops[0].ActualDepart > 0
                                ? $"{TimeSpan.FromSeconds(train.StationStops[0].ActualDepart):c}"
                                : train.StationStops[0].DepartTime > 0 ? $"{TimeSpan.FromSeconds(train.StationStops[0].DepartTime):c}" : "..:..:..";
                            break;
                        case AiMovementState.HandleAction:
                            if (train.nextActionInfo is AuxActionItem auxAction)
                            {
                                AuxActionRef actionRef = auxAction.ActionRef;
                                if (actionRef.GenericAction)
                                {
                                    this["AiMode"] = "Generic Action";
                                }
                                else if ((train.AuxActionsContainer[0] as AIAuxActionsRef)?.NextAction == AuxiliaryAction.WaitingPoint)
                                {
                                    this["AiMode"] = StationStopType.WaitingPoint.ToString();
                                    this["AiData"] = train.nextActionInfo is AuxActionWPItem wpItem && wpItem.ActualDepart > 0
                                        ? $"{TimeSpan.FromSeconds(wpItem.ActualDepart):c}"
                                        : "..:..:..";
                                }
                            }
                            else if (train.AuxActionsContainer.SpecificRequiredActions.First.Value is AuxActSigDelegate auxActSigDelegate &&
                                auxActSigDelegate.currentMvmtState == AiMovementState.HandleAction)
                            {
                                this["AiMode"] = "Waitstate";
                                this["AiData"] = $"{TimeSpan.FromSeconds(auxActSigDelegate.ActualDepart):c}";
                            }
                            break;
                        case AiMovementState.Static:
                            this["AiData"] = train.StartTime.HasValue ?
                                $"{TimeSpan.FromSeconds(train.StartTime.Value):c}" : "--------";
                            break;
                        default:
                            this["AiData"] = train.TrainType == TrainType.Player ||
                                (train.TrainType == TrainType.Remote && MultiPlayerManager.IsServer()) || train.IsActualPlayerTrain
                                ? null
                                : $"{train.AITrainThrottlePercent}% & {train.AITrainBrakePercent}%";
                            break;
                    }

                    if (null != train.nextActionInfo)
                    {
                        this["Authorization"] = train.nextActionInfo?.NextAction.ToString();
                        this["AuthDistance"] = FormatStrings.FormatDistance(train.nextActionInfo.ActivateDistanceM - train.PresentPosition[Direction.Forward].DistanceTravelled, metricData);
                    }

                }
            }
        }
    }
}
