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

/* AI
 * 
 * Contains code to initialize and control AI trains.
 * 
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Models.Imported.State;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.OpenRails.Models;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;

namespace Orts.Simulation.Timetables
{
    public partial class TTTrain : AITrain
    {
        public const float DefMaxDecelMpSSP = 1.0f;               // maximum decelleration
        public const float DefMaxAccelMpSSP = 1.0f;               // maximum accelleration
        public const float DefMaxDecelMpSSF = 0.8f;               // maximum decelleration
        public const float DefMaxAccelMpSSF = 0.5f;               // maximum accelleration

        public bool Closeup { get; set; }                           // closeup to other train when stabling
        private const float keepDistanceCloseupM = 2.5f;       // stay 2.5m from end of route when closeup required (for stabling only)
        private const float keepDistanceTrainAheadCloseupM = 0.5f;       // stay 0.5m from train ahead when closeup required (for stabling only)
        private const float keepDistanceCloseupSignalM = 7.0f;          // stay 10m from signal ahead when signalcloseup required
        private const float endOfRouteDistance = 150f;         // Max length to remain for train to continue on route

        public int? ActivateTime { get; set; }                           // time train is activated
        public bool TriggeredActivationRequired { get; set; }    // train activation is triggered by other train

        public bool Created { get; set; }                        // train is created at start
        public string CreateAhead { get; set; } = string.Empty;           // train is created ahead of other train
        public string CreateInPool { get; set; } = string.Empty;          // train is to be created in pool at start of timetable
        public string CreateFromPool { get; set; } = string.Empty;        // train is to be created from pool
        public PoolExitDirection CreatePoolDirection { get; set; } = PoolExitDirection.Undefined;
        // required direction on leaving pool (if applicable)
        public string ForcedConsistName { get; set; } = string.Empty;     // forced consist name for extraction from pool

        // Timetable Commands info
        private List<WaitInfo> waitList;                            //used when in timetable mode for wait instructions
        private Dictionary<int, List<WaitInfo>> waitAnyList;        //used when in timetable mode for waitany instructions
        public bool StableCallOn { get; set; }                                //used when in timetable mode to show stabled train is allowed to call on
        public bool DriverOnlyOperation { get; set; }                          //used when in timetable mode to indicate driver only operation
        public bool ForceReversal { get; set; }                                //used when in timetable mode to force reversal at diverging point ignoring signals

        public int Forms { get; set; } = -1;                                            //indicates which train is to be formed out of this train on termination
        public bool FormsStatic { get; set; }                                  //indicate if train is to remain as static
        public string ExitPool { get; set; } = string.Empty;                            //set if train is to be stabled in pool
        public int PoolAccessSection { get; set; } = -1;                                //set to last section index if train is to be stabled in pool, section is access section to pool

        public int PoolStorageIndex { get; set; } = -1;                                 // index in selected pool path (>=0)

        public PoolExitDirection PoolExitDirection { get; set; } = PoolExitDirection.Undefined;
        // required exit direction from pool (if applicable) 
        public TimetableTurntableControl ActiveTurntable { get; set; }          //active turntable

        public int FormedOf { get; set; } = -1;                                         //indicates out of which train this train is formed
        public TimetableFormationCommand FormedOfType { get; set; } = TimetableFormationCommand.None;               //indicates type of formed-of command
        public int OrgAINumber { get; set; } = -1;                                      //original AI number of formed player train
        public bool SetStop { get; set; }                                      //indicates train must copy station stop from formed train
        public bool FormsAtStation { get; set; }                               //indicates train must form into next service at last station, route must be curtailed to that stop
        public bool LeadLocoAntiSlip { get; private set; }                             //anti slip indication for original leading engine

        // detach details
        public Dictionary<int, List<DetachInfo>> DetachDetails { get; } = new Dictionary<int, List<DetachInfo>>();
        // key is platform reference (use -1 for detach at start or end), list is detach commands at that location
        public EnumArray<int, DetachDetailsIndex> DetachActive { get; } = new EnumArray<int, DetachDetailsIndex>(-1);// detach is activated - first index is key in DetachDetails, second index is index in valuelist
        // 2nd index = -1 indicates invalid (first index -1 is a valid index)
        public int DetachUnits { get; set; }                                       // no. of units to detach
        public bool DetachPosition { get; set; }                               // if true detach from front
        public bool DetachPending { get; set; }                                // true when player detach window is displayed

        // attach details
        public AttachInfo AttachDetails { get; set; }                                  // attach details
        public Dictionary<int, List<int>> NeedAttach { get; private set; } = new Dictionary<int, List<int>>();
        // key is platform reference or -1 for attach to static train, list are trains which are to attach

        // pickup details
        public List<PickUpInfo> PickUpDetails { get; private set; } = new List<PickUpInfo>();   // only used during train building
        public List<int> PickUpTrains { get; private set; } = new List<int>();                  // list of train to be picked up
        public List<int> PickUpStatic { get; private set; } = new List<int>();                  // index of locations where static consists are to be picked up
        public bool PickUpStaticOnForms { get; set; }                          // set if pickup of static is required when forming next train
        public bool NeedPickUp { get; set; }                                   // indicates pickup is required

        // transfer details
        public Dictionary<int, TransferInfo> TransferStationDetails { get; private set; } = new Dictionary<int, TransferInfo>();
        // list of transfer to take place in station
        public Dictionary<int, List<TransferInfo>> TransferTrainDetails { get; private set; } = new Dictionary<int, List<TransferInfo>>();
        // list of transfers defined per train - if int = -1, transfer is to be performed on static train
        public bool NeedTransfer { get; set; }                                 // indicates transfer is required
        public Dictionary<int, List<int>> NeedStationTransfer { get; private set; } = new Dictionary<int, List<int>>();
        // list of required station transfers, per station index
        public Dictionary<int, int> NeedTrainTransfer { get; private set; } = new Dictionary<int, int>();
        // number of required train transfers per section

        // delayed restart
        public bool DelayStart { get; set; }                                 // start is delayed
        public float RestdelayS { get; set; }                                   // time to wait
        public AiStartMovement DelayedStartState { get; set; }               // state to start

        public EnumArray<DelayedStart, DelayedStartType> DelayedStartSettings { get; } = new EnumArray<DelayedStart, DelayedStartType>();
        public float ReverseAddedDelaySperM { get; set; }                          // additional delay on reversal based on train length

        public EnumArray<float?, SpeedValueType> SpeedSettings { get; } = new EnumArray<float?, SpeedValueType>();

        public bool SpeedRestrictionActive { get; set; }                // special speed has been set
        public int? CruiseMaxDelay { get; set; }                                     // max. delay to maintain cruise speed

        // special patch conditions
        public enum LastSignalStop
        {
            None,
            Last,
            Reverse,
        }
        public LastSignalStop ReqLastSignalStop { get; set; } = LastSignalStop.None;

        public Collection<TriggerActivation> ActivatedTrainTriggers { get; } = new Collection<TriggerActivation>();
        public string Briefing { get; set; } = string.Empty;

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// <\summary>
        public TTTrain()
            : base()
        {
            // preset accel and decel values
            MaxAccelMpSSP = DefMaxAccelMpSSP;
            MaxAccelMpSSF = DefMaxAccelMpSSF;
            MaxDecelMpSSP = DefMaxDecelMpSSP;
            MaxDecelMpSSF = DefMaxDecelMpSSF;

            // preset movement state
            MovementState = AiMovementState.Static;

            // preset restart delays
            DelayedStartSettings[DelayedStartType.NewStart] = new DelayedStart(0, 10);
            DelayedStartSettings[DelayedStartType.PathRestart] = new DelayedStart(1, 10);
            DelayedStartSettings[DelayedStartType.FollowRestart] = new DelayedStart(15, 10);
            DelayedStartSettings[DelayedStartType.StationRestart] = new DelayedStart(0, 15);
            DelayedStartSettings[DelayedStartType.AttachRestart] = new DelayedStart(30, 30);
            DelayedStartSettings[DelayedStartType.DetachRestart] = new DelayedStart(5, 20);
            DelayedStartSettings[DelayedStartType.MovingTableRestart] = new DelayedStart(1, 10);
            ReverseAddedDelaySperM = 0.5f;

            // preset speed values
        }

        //================================================================================================//
        /// <summary>
        /// Constructor using existing train
        /// <\summary>
        public TTTrain(TTTrain TTrain)
            : base()
        {
            // set AI reference
            AI = simulator.AI;

            // preset accel and decel values
            MaxAccelMpSSP = DefMaxAccelMpSSP;
            MaxAccelMpSSF = DefMaxAccelMpSSF;
            MaxDecelMpSSP = DefMaxDecelMpSSP;
            MaxDecelMpSSF = DefMaxDecelMpSSF;

            // preset movement state
            MovementState = AiMovementState.Static;

            // copy restart delays
            DelayedStartSettings = TTrain.DelayedStartSettings;

            // copy speed values
            SpeedSettings = TTrain.SpeedSettings;
        }

        public override async ValueTask<TrainSaveState> Snapshot()
        {
            TrainSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.TimetableTrainSaveState = new TimetableTrainSaveState()
            {
                CloseUpStabling = Closeup,
                Created = Created,
                CreatedAhead = CreateAhead,
                CreatedFromPool = CreateFromPool,
                CreatedInPool = CreateInPool,
                ConsistName = ForcedConsistName,
                CreatePoolDirection = CreatePoolDirection,
                MaxAccelerationPassenger = MaxAccelMpSSP,
                MaxDecelerationPassenger = MaxDecelMpSSP,
                MaxAccelerationFreight = MaxAccelMpSSF,
                MaxDecelerationFreight = MaxDecelMpSSF,
                ActivationTime = ActivateTime,
                TriggeredActivationRequired = TriggeredActivationRequired,
                ActivationTriggerSaveStates = await ActivatedTrainTriggers.SnapshotCollection<TriggerActivationSaveState, TriggerActivation>().ConfigureAwait(false),
                WaitInfoSaveStates = waitList == null ? null : await waitList.SnapshotCollection<WaitInfoSaveState, WaitInfo>().ConfigureAwait(false),
                WaitInfoAnySaveStates = waitAnyList == null ? null : await waitAnyList.SnapshotListDictionary<WaitInfoSaveState, WaitInfo, int>().ConfigureAwait(false),
                StableCallOn = StableCallOn,
                FormedTrainNumber = Forms,
                FormedStaticTrain = FormsStatic,
                ExitPool = ExitPool,
                PoolAccessSection = PoolAccessSection,
                PoolStorageIndex = PoolStorageIndex,
                PoolExitDirection = PoolExitDirection,
                FormedFromTrainNumber = FormedOf,
                OriginalAiTrainNumber = OrgAINumber,
                TimetableFormationCommand = FormedOfType,
                FormsAtStation = FormsAtStation,
                InheritStationStop = SetStop,

                AttachInfoSaveState = AttachDetails == null ? null : await AttachDetails.Snapshot().ConfigureAwait(false),
                DetachInfoSaveStates = DetachDetails == null ? null : await DetachDetails.SnapshotListDictionary<DetachInfoSaveState, DetachInfo, int>().ConfigureAwait(false),
                TrainOnTurntableSaveState = ActiveTurntable == null ? null : await ActiveTurntable.Snapshot().ConfigureAwait(false),
                ActiveDetaches = DetachActive.ToArray(),
                DetachUnits = DetachUnits,
                DetachPosition = DetachPosition,
                DetachPending = DetachPending,
                PickupTrains = new Collection<int>(PickUpTrains),
                PickupStaticTrains = new Collection<int>(PickUpStatic),
                PickupNeeded = NeedPickUp,
                PickupStaticOnForms = PickUpStaticOnForms,
                TransferStationDetailsSaveStates = await TransferStationDetails.SnapshotDictionary<TransferInfoSaveState, TransferInfo, int>().ConfigureAwait(false),
                TransferTrainDetailSateStates = await TransferTrainDetails.SnapshotListDictionary<TransferInfoSaveState, TransferInfo, int>().ConfigureAwait(false),
                NeedAttach = await NeedAttach.SnapshotListDictionary().ConfigureAwait(false),
                NeedStationTransfer = await NeedStationTransfer.SnapshotListDictionary().ConfigureAwait(false),
                NeedTrainTransfer = new Dictionary<int, int>(NeedTrainTransfer),
                DelayedStarts = DelayedStartSettings.ToArray(),
                ReverseAddedDelay = ReverseAddedDelaySperM,
                DelayedStart = DelayStart,
                DelayedStartState = DelayedStartState,
                RemainingDelay = RestdelayS,
                SpeedSettings = SpeedSettings.ToArray(),
                SpeedRestrictionActive = SpeedRestrictionActive,
                CruiseDelayMax = CruiseMaxDelay,
                DriverOnlyOperation = DriverOnlyOperation,
                ForceReversal = ForceReversal,
                Briefing = Briefing,
            };
            return saveState;
        }

        public override async ValueTask Restore([NotNull] TrainSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(saveState.TimetableTrainSaveState, nameof(saveState.TimetableTrainSaveState));

            TimetableTrainSaveState timetableTrainSaveState = saveState.TimetableTrainSaveState;


            // TTTrains own additional fields
            Closeup = timetableTrainSaveState.CloseUpStabling;
            Created = timetableTrainSaveState.Created;
            CreateAhead = timetableTrainSaveState.CreatedAhead;
            CreateFromPool = timetableTrainSaveState.CreatedFromPool;
            CreateInPool = timetableTrainSaveState.CreatedInPool;
            ForcedConsistName = timetableTrainSaveState.ConsistName;
            CreatePoolDirection = timetableTrainSaveState.PoolExitDirection;

            MaxAccelMpSSP = timetableTrainSaveState.MaxAccelerationPassenger;
            MaxAccelMpSSF = timetableTrainSaveState.MaxAccelerationFreight;
            MaxDecelMpSSP = timetableTrainSaveState.MaxDecelerationPassenger;
            MaxDecelMpSSF = timetableTrainSaveState.MaxDecelerationFreight;

            ActivateTime = timetableTrainSaveState.ActivationTime;

            TriggeredActivationRequired = timetableTrainSaveState.TriggeredActivationRequired;

            await ActivatedTrainTriggers.RestoreCollectionCreateNewItems(timetableTrainSaveState.ActivationTriggerSaveStates).ConfigureAwait(false);

            if (timetableTrainSaveState.WaitInfoSaveStates?.Count > 0)
            {
                waitList = new List<WaitInfo>();
                await waitList.RestoreCollectionCreateNewItems(timetableTrainSaveState.WaitInfoSaveStates).ConfigureAwait(false);
            }

            if (timetableTrainSaveState.WaitInfoAnySaveStates?.Count > 0)
            {
                waitAnyList = new Dictionary<int, List<WaitInfo>>();
                await waitAnyList.RestoreListDictionaryCreateNewItem(timetableTrainSaveState.WaitInfoAnySaveStates).ConfigureAwait(false);
            }

            StableCallOn = timetableTrainSaveState.StableCallOn;

            Forms = timetableTrainSaveState.FormedTrainNumber;
            FormsStatic = timetableTrainSaveState.FormedStaticTrain;
            ExitPool = timetableTrainSaveState.ExitPool;
            PoolAccessSection = timetableTrainSaveState.PoolAccessSection;
            PoolStorageIndex = timetableTrainSaveState.PoolStorageIndex;
            PoolExitDirection = timetableTrainSaveState.PoolExitDirection;

            
            if (timetableTrainSaveState.TrainOnTurntableSaveState != null)
                await ActiveTurntable.Restore(timetableTrainSaveState.TrainOnTurntableSaveState).ConfigureAwait(false);

            FormedOf = timetableTrainSaveState.FormedFromTrainNumber;
            FormedOfType = timetableTrainSaveState.TimetableFormationCommand;
            OrgAINumber = timetableTrainSaveState.OriginalAiTrainNumber;
            SetStop = timetableTrainSaveState.InheritStationStop;
            FormsAtStation = timetableTrainSaveState.FormsAtStation;

            //DetachDetails = new Dictionary<int, List<DetachInfo>>();
            if (timetableTrainSaveState.DetachInfoSaveStates != null)
                await DetachDetails.RestoreListDictionaryCreateNewItem(timetableTrainSaveState.DetachInfoSaveStates).ConfigureAwait(false);

            DetachActive.FromArray(timetableTrainSaveState.ActiveDetaches);
            DetachUnits = timetableTrainSaveState.DetachUnits;
            DetachPosition = timetableTrainSaveState.DetachPosition;
            DetachPending = timetableTrainSaveState.DetachPending;

            if (timetableTrainSaveState.AttachInfoSaveState != null)
            {
                AttachDetails = new AttachInfo();
                await AttachDetails.Restore(timetableTrainSaveState.AttachInfoSaveState).ConfigureAwait(false);
            }

            PickUpTrains = new List<int>(timetableTrainSaveState.PickupTrains);
            PickUpStatic = new List<int>(timetableTrainSaveState.PickupStaticTrains);
            PickUpStaticOnForms = timetableTrainSaveState.PickupStaticOnForms;
            NeedPickUp = timetableTrainSaveState.PickupNeeded;

            TransferStationDetails = new Dictionary<int, TransferInfo>();
            await TransferStationDetails.RestoreDictionaryCreateNewInstances(timetableTrainSaveState.TransferStationDetailsSaveStates).ConfigureAwait(false);

            TransferTrainDetails = new Dictionary<int, List<TransferInfo>>();
            await TransferTrainDetails.RestoreListDictionaryCreateNewItem(timetableTrainSaveState.TransferTrainDetailSateStates).ConfigureAwait(false);

            NeedAttach = new Dictionary<int, List<int>>();
            await NeedAttach.RestoreListDictionary(timetableTrainSaveState.NeedAttach).ConfigureAwait(false);

            NeedStationTransfer = new Dictionary<int, List<int>>();
            await NeedStationTransfer.RestoreListDictionary(timetableTrainSaveState.NeedStationTransfer).ConfigureAwait(false);

            NeedTrainTransfer = new Dictionary<int, int>(timetableTrainSaveState.NeedTrainTransfer);

            DelayedStartSettings.FromArray(timetableTrainSaveState.DelayedStarts);
            ReverseAddedDelaySperM = timetableTrainSaveState.ReverseAddedDelay;

            DelayStart = timetableTrainSaveState.DelayedStart;
            DelayedStartState = timetableTrainSaveState.DelayedStartState;
            RestdelayS = timetableTrainSaveState.RemainingDelay;

            // preset speed values
            SpeedSettings.FromArray(timetableTrainSaveState.SpeedSettings);
            SpeedRestrictionActive = timetableTrainSaveState.SpeedRestrictionActive;
            CruiseMaxDelay = timetableTrainSaveState.CruiseDelayMax;

            DriverOnlyOperation = timetableTrainSaveState.DriverOnlyOperation;
            ForceReversal = timetableTrainSaveState.ForceReversal;

            Briefing = timetableTrainSaveState.Briefing;

            // reset actions if train is active
            bool activeTrain = true;

            if (TrainType == TrainType.AiNotStarted)
                activeTrain = false;
            if (TrainType == TrainType.AiAutoGenerated)
                activeTrain = false;

            if (activeTrain)
            {
                if (MovementState == AiMovementState.Static || MovementState == AiMovementState.Init)
                    activeTrain = false;
            }
            if (activeTrain)
            {
                ResetActions(true);
            }
        }

        /// <summary>
        /// Terminate route at last signal in train's direction
        /// </summary>
        public void EndRouteAtLastSignal()
        {
            // no action required
            if (ReqLastSignalStop == LastSignalStop.None)
            {
                return;
            }

            int lastIndex = TCRoute.TCRouteSubpaths.Count - 1;
            TrackCircuitPartialPathRoute lastSubpath = new TrackCircuitPartialPathRoute(TCRoute.TCRouteSubpaths[lastIndex]);

            int lastSectionIndex = -1;

            // search for last signal in required direction
            for (int iIndex = lastSubpath.Count - 1; iIndex >= 0 && lastSectionIndex < 0; iIndex--)
            {
                TrackCircuitSection thisSection = lastSubpath[iIndex].TrackCircuitSection;
                TrackDirection reqEndSignal = ReqLastSignalStop == LastSignalStop.Last ? lastSubpath[iIndex].Direction : lastSubpath[iIndex].Direction.Reverse();

                if (thisSection.EndSignals[reqEndSignal] != null)
                {
                    lastSectionIndex = iIndex;
                }
            }

            // remove sections beyond last signal
            for (int iIndex = lastSubpath.Count - 1; iIndex > lastSectionIndex; iIndex--)
            {
                lastSubpath.RemoveAt(iIndex);
            }

            // reinsert subroute
            TCRoute.TCRouteSubpaths.RemoveAt(lastIndex);
            TCRoute.TCRouteSubpaths.Add(new TrackCircuitPartialPathRoute(lastSubpath));
        }

        //================================================================================================//
        /// <summary>
        /// Set alternative station stop when alternative path is selected
        /// Override from Train class
        /// </summary>
        /// <param name="orgStop"></param>
        /// <param name="newRoute"></param>
        /// <returns></returns>
        //TODO 20201123 this should potentially be moved to StationStop class
        private protected override StationStop SetAlternativeStationStop(StationStop orgStop, TrackCircuitPartialPathRoute newRoute)
        {
            int altPlatformIndex = -1;

            // get station platform list
            if (Simulator.Instance.SignalEnvironment.StationXRefList.TryGetValue(orgStop.PlatformItem.Name, out List<int> xrefKeys))
            {
                // search through all available platforms
                for (int platformIndex = 0; platformIndex <= xrefKeys.Count - 1 && altPlatformIndex < 0; platformIndex++)
                {
                    int platformXRefIndex = xrefKeys[platformIndex];
                    PlatformDetails altPlatform = Simulator.Instance.SignalEnvironment.PlatformDetailsList[platformXRefIndex];

                    // check if section is in new route
                    for (int iSectionIndex = 0; iSectionIndex <= altPlatform.TCSectionIndex.Count - 1 && altPlatformIndex < 0; iSectionIndex++)
                    {
                        if (newRoute.GetRouteIndex(altPlatform.TCSectionIndex[iSectionIndex], 0) > 0)
                        {
                            altPlatformIndex = platformXRefIndex;
                        }
                    }
                }

                // remove holding signal if set
                int holdSig = -1;
                if (orgStop.HoldSignal && orgStop.ExitSignal >= 0 && HoldingSignals.Contains(orgStop.ExitSignal))
                {
                    holdSig = orgStop.ExitSignal;
                    HoldingSignals.Remove(holdSig);
                }
                // section found in new route - set new station details using old details
                if (altPlatformIndex > 0)
                {
                    bool isNewPlatform = true;
                    // check if new found platform is actually same as original
                    foreach (int platfReference in Simulator.Instance.SignalEnvironment.PlatformDetailsList[altPlatformIndex].PlatformReference)
                    {
                        if (platfReference == orgStop.PlatformReference)
                        {
                            isNewPlatform = false;
                            break;
                        }
                    }

                    // if platform found is original platform, reinstate hold signal but take no further action
                    if (!isNewPlatform)
                    {
                        if (holdSig >= 0)
                            HoldingSignals.Add(holdSig);
                        return (orgStop);
                    }
                    else
                    {
                        // calculate new stop

                        StationStop newStop = CalculateStationStop(Simulator.Instance.SignalEnvironment.PlatformDetailsList[altPlatformIndex].PlatformReference[SignalLocation.NearEnd],
                        orgStop.ArrivalTime, orgStop.DepartTime, ClearingDistance, MinStopDistance,
                        orgStop.Terminal, orgStop.ActualMinStopTime, orgStop.KeepClearFront, orgStop.KeepClearRear, orgStop.ForcePosition,
                        orgStop.CloseupSignal, orgStop.Closeup, orgStop.RestrictPlatformToSignal, orgStop.ExtendPlatformToSignal, orgStop.EndStop);
                        // add new holding signal if required
                        if (newStop.HoldSignal && newStop.ExitSignal >= 0)
                        {
                            HoldingSignals.Add(newStop.ExitSignal);
                        }

                        if (newStop != null)
                        {
                            if (orgStop.ConnectionDetails != null)
                                newStop.EnsureListsExists();
                            foreach (KeyValuePair<int, WaitInfo> thisConnect in orgStop.ConnectionDetails ?? Enumerable.Empty<KeyValuePair<int, WaitInfo>>())
                            {
                                newStop.ConnectionDetails.Add(thisConnect.Key, thisConnect.Value);
                            }
                        }

                        return (newStop);
                    }
                }
            }
            return (null);
        }

        //================================================================================================//
        /// <summary>
        /// Post Init (override from Train) (with activate train option)
        /// perform all actions required to start
        /// </summary>

        public bool PostInit(bool activateTrain)
        {
            // if train itself forms other train, check if train is to end at station (only if other train is not autogen and this train has SetStop set)

            if (Forms >= 0 && SetStop)
            {
                TTTrain nextTrain = AI.StartList.GetNotStartedTTTrainByNumber(Forms, false);

                if (nextTrain != null && nextTrain.StationStops != null && nextTrain.StationStops.Count > 0)
                {
                    TrackCircuitPartialPathRoute lastSubpath = TCRoute.TCRouteSubpaths[TCRoute.TCRouteSubpaths.Count - 1];
                    int lastSectionIndex = lastSubpath[lastSubpath.Count - 1].TrackCircuitSection.Index;

                    if (nextTrain.StationStops[0].PlatformItem.TCSectionIndex.Contains(lastSectionIndex))
                    {
                        StationStops.Clear();
                        StationStop newStop = nextTrain.StationStops[0].CreateCopy();

                        int startvalue = nextTrain.ActivateTime.HasValue ? nextTrain.ActivateTime.Value : nextTrain.StartTime.Value;

                        newStop.ArrivalTime = startvalue;
                        newStop.DepartTime = startvalue;
                        newStop.RouteIndex = lastSubpath.GetRouteIndex(newStop.TrackCircuitSectionIndex, 0);
                        newStop.SubrouteIndex = TCRoute.TCRouteSubpaths.Count - 1;
                        if (newStop.RouteIndex >= 0)
                        {
                            StationStops.Add(newStop); // do not set stop if platform is not on route

                            // switch stop position if train is to reverse
                            int nextTrainRouteIndex = nextTrain.TCRoute.TCRouteSubpaths[0].GetRouteIndex(lastSectionIndex, 0);
                            if (nextTrainRouteIndex >= 0 && nextTrain.TCRoute.TCRouteSubpaths[0][nextTrainRouteIndex].Direction != lastSubpath[newStop.RouteIndex].Direction)
                            {
                                TrackCircuitSection lastSection = TrackCircuitSection.TrackCircuitList[lastSectionIndex];
                                newStop.StopOffset = lastSection.Length - newStop.StopOffset + Length;
                            }
                        }
                    }
                }
            }

            if (Forms >= 0 && FormsAtStation && StationStops != null && StationStops.Count > 0)  // curtail route to last station stop
            {
                StationStop lastStop = StationStops[StationStops.Count - 1];
                TrackCircuitPartialPathRoute reqSubroute = TCRoute.TCRouteSubpaths[lastStop.SubrouteIndex];
                for (int iRouteIndex = reqSubroute.Count - 1; iRouteIndex > lastStop.RouteIndex; iRouteIndex--)
                {
                    reqSubroute.RemoveAt(iRouteIndex);
                }

                // if subroute is present route, create new ValidRoute
                if (lastStop.SubrouteIndex == TCRoute.ActiveSubPath)
                {
                    ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath]);
                }
            }

            // on activation : if train is to join pool, set proper dispose details
            // copy new route if required

            if (activateTrain && !String.IsNullOrEmpty(ExitPool) && ActiveTurntable == null)
            {
                TimetablePool thisPool = simulator.PoolHolder.Pools[ExitPool];
                bool validPool = thisPool.TestPoolExit(this);

                if (validPool)
                {
                    PoolAccessSection = TCRoute.TCRouteSubpaths.Last().Last().TrackCircuitSection.Index;
                }
                else
                {
                    ExitPool = String.Empty;
                }
            }

            // check deadlocks (if train has valid activate time only - otherwise it is static and won't move)

            if (ActivateTime.HasValue)
            {
                TrainDeadlockInfo.CheckDeadlock(ValidRoutes[Direction.Forward], Number);
            }

            // set initial position and state

            bool atStation = base.AtStation;
            bool validPosition = InitialTrainPlacement(String.IsNullOrEmpty(CreateAhead));     // Check track and if clear, set occupied

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
                    if (TrainMaxSpeedMpS > 40.0f)
                    {
                        MaxDecelMpSS = 1.5f * MaxDecelMpSSP;  // higher decel for high speed trains
                    }
                    if (TrainMaxSpeedMpS > 55.0f)
                    {
                        MaxDecelMpSS = 2.5f * MaxDecelMpSSP;  // higher decel for very high speed trains
                    }
                }

                InitializeSignals(false);               // Get signal information
                TCRoute.SetReversalOffset(Length, true);      // set reversal information for first subpath
                SetEndOfRouteAction();                  // set action to ensure train stops at end of route
                ControlMode = TrainControlMode.Inactive;   // set control mode to INACTIVE

                // active train
                if (activateTrain)
                {
                    MovementState = AiMovementState.Init;        // start in INIT mode to collect info
                    ControlMode = TrainControlMode.AutoNode;         // start up in NODE control

                    // if there is an active turntable and action is not completed, start in turntable state
                    if (ActiveTurntable != null && ActiveTurntable.MovingTableState != MovingTableState.Completed)
                    {
                        MovementState = AiMovementState.Turntable;
                        if (TrainType == TrainType.Player)
                        {
                            if (ActiveTurntable.MovingTableState == MovingTableState.WaitingMovingTableAvailability)
                            {
                                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                                    simulator.Confirmer.Information("Wait for turntable to become available");
                            }
                        }
                    }

                    // recalculate station stops based on present train length
                    RecalculateStationStops(atStation);

                    // check if train starts at station stop
                    if (StationStops.Count > 0 && !atStation)
                    {
                        atStation = CheckInitialStation();

                        if (!atStation)
                        {
                            if (StationStops.Count > 0)
                            {
                                SetNextStationAction();               // set station details
                            }
                        }
                    }
                    else if (atStation)
                    {
                        MovementState = AiMovementState.StationStop;
                        AIActionItem newAction = new AIActionItem(null, AiActionType.StationStop);
                        newAction.SetParam(-10f, 0.0f, 0.0f, 0.0f);

                        nextActionInfo = newAction;
                        NextStopDistanceM = 0.0f;
                    }
                }
                // start train as static
                else
                {
                    MovementState = AiMovementState.Static;   // start in STATIC mode until required activate time
                }
            }

            return (validPosition);
        }

        //================================================================================================//
        /// <summary>
        /// Post Init : perform all actions required to start
        /// Override from Train class
        /// </summary>

        internal override bool PostInit()
        {
            // start ahead of train if required

            bool validPosition = true;

            if (!String.IsNullOrEmpty(CreateAhead))
            {
                CalculateInitialTTTrainPosition(ref validPosition, null);
            }

            // if not yet started, start normally

            if (validPosition)
            {
                validPosition = InitialTrainPlacement(true);
            }

            if (validPosition)
            {
                InitializeSignals(false);     // Get signal information - only if train has route //
                if (TrainType != TrainType.Static)
                    TrainDeadlockInfo.CheckDeadlock(ValidRoutes[Direction.Forward], Number);    // Check deadlock against all other trains (not for static trains)
                if (TCRoute != null)
                    TCRoute.SetReversalOffset(Length, true);
            }

            // set train speed logging flag (valid per activity, so will be restored after save)

            if (TrainType == TrainType.Player)
            {
                SetupStationStopHandling(); // player train must now perform own station stop handling (no activity function available)

                evaluateTrainSpeed = simulator.UserSettings.EvaluationTrainSpeed;
                evaluationInterval = simulator.UserSettings.EvaluationInterval;

                evaluationContent = simulator.UserSettings.EvaluationContent;

                // if logging required, derive filename and open file
                if (evaluateTrainSpeed)
                {
                    evaluationLogFile = simulator.DeriveLogFile("Speed");
                    if (String.IsNullOrEmpty(evaluationLogFile))
                    {
                        evaluateTrainSpeed = false;
                    }
                    else
                    {
                        CreateLogFile();
                    }
                }
            }

            return (validPosition);
        }


        //================================================================================================//
        /// <summary>
        /// Calculate actual station stop details
        /// <\summary>

        public StationStop CalculateStationStop(int platformStartID, int arrivalTime, int departTime, float clearingDistanceM,
            float minStopDistance, bool terminal, int? actMinStopTime, float? keepClearFront, float? keepClearRear, bool forcePosition, bool closeupSignal,
            bool closeup, bool restrictPlatformToSignal, bool extendPlatformToSignal, bool endStop)
        {
            int platformIndex;
            int activeSubroute = 0;

            TrackCircuitPartialPathRoute thisRoute = TCRoute.TCRouteSubpaths[activeSubroute];

            // get platform details

            if (!Simulator.Instance.SignalEnvironment.PlatformXRefList.TryGetValue(platformStartID, out platformIndex))
            {
                return (null); // station not found
            }
            else
            {
                PlatformDetails thisPlatform = Simulator.Instance.SignalEnvironment.PlatformDetailsList[platformIndex];
                int sectionIndex = thisPlatform.TCSectionIndex[0];
                int routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);

                // if first section not found in route, try last

                if (routeIndex < 0)
                {
                    sectionIndex = thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                    routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);
                }

                // if neither section found - try next subroute - keep trying till found or out of subroutes

                while (routeIndex < 0 && activeSubroute < (TCRoute.TCRouteSubpaths.Count - 1))
                {
                    activeSubroute++;
                    thisRoute = TCRoute.TCRouteSubpaths[activeSubroute];
                    routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);

                    // if first section not found in route, try last

                    if (routeIndex < 0)
                    {
                        sectionIndex = thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                        routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);
                    }
                }

                // if neither section found - platform is not on route - skip

                if (routeIndex < 0)
                {
                    Trace.TraceWarning($"Train {Name} ({Number}) : platform {platformStartID} is not on route");
                    return (null);
                }

                // determine end stop position depending on direction

                StationStop dummyStop = CalculateStationStopPosition(thisRoute, routeIndex, thisPlatform, activeSubroute,
                    keepClearFront, keepClearRear, forcePosition, closeupSignal, closeup, restrictPlatformToSignal, extendPlatformToSignal,
                    terminal, platformIndex);

                // build and add station stop

                StationStop thisStation = new StationStop(
                        platformStartID,
                        thisPlatform,
                        activeSubroute,
                        dummyStop.RouteIndex,
                        dummyStop.TrackCircuitSectionIndex,
                        dummyStop.Direction,
                        dummyStop.ExitSignal,
                        dummyStop.HoldSignal,
                        false,
                        false,
                        dummyStop.StopOffset,
                        arrivalTime,
                        departTime,
                        terminal,
                        actMinStopTime,
                        keepClearFront,
                        keepClearRear,
                        forcePosition,
                        closeupSignal,
                        closeup,
                        restrictPlatformToSignal,
                        extendPlatformToSignal,
                        endStop,
                        StationStopType.Station);

                return (thisStation);
            }
        }

        /// <summary>
        /// Calculate station stop position
        /// </summary>
        /// <param name="thisRoute"></param>
        /// <param name="routeIndex"></param>
        /// <param name="thisPlatform"></param>
        /// <param name="activeSubroute"></param>
        /// <param name="keepClearFront"></param>
        /// <param name="keepClearRear"></param>
        /// <param name="forcePosition"></param>
        /// <param name="terminal"></param>
        /// <param name="platformIndex"></param>
        /// <returns></returns>
        public StationStop CalculateStationStopPosition(TrackCircuitPartialPathRoute thisRoute, int routeIndex, PlatformDetails thisPlatform, int activeSubroute,
            float? keepClearFront, float? keepClearRear, bool forcePosition, bool closeupSignal, bool closeup,
            bool restrictPlatformToSignal, bool ExtendPlatformToSignal, bool terminal, int platformIndex)
        {
            TrackCircuitRouteElement thisElement = thisRoute[routeIndex];

            int routeSectionIndex = thisElement.TrackCircuitSection.Index;
            int endSectionIndex = thisElement.Direction == 0 ?
                thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1] :
                thisPlatform.TCSectionIndex[0];
            int beginSectionIndex = thisElement.Direction == 0 ?
                thisPlatform.TCSectionIndex[0] :
                thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];

            bool platformHasEndSignal = thisElement.Direction == 0 ? (thisPlatform.EndSignals[TrackDirection.Ahead] >= 0) : (thisPlatform.EndSignals[TrackDirection.Reverse] >= 0);
            float distanceToEndSignal = platformHasEndSignal ? (thisElement.Direction == 0 ? thisPlatform.DistanceToSignals[TrackDirection.Ahead] : thisPlatform.DistanceToSignals[TrackDirection.Reverse]) : -1;

            float endOffset = thisPlatform.TrackCircuitOffset[SignalLocation.FarEnd, (TrackDirection)thisElement.Direction];
            float beginOffset = thisPlatform.TrackCircuitOffset[SignalLocation.NearEnd, (TrackDirection)thisElement.Direction];

            float deltaLength = thisPlatform.Length - Length; // platform length - train length

            // get all sections which form platform
            TrackCircuitPartialPathRoute platformRoute = SignalEnvironment.BuildTempRoute(this, beginSectionIndex, beginOffset, (TrackDirection)thisElement.Direction, thisPlatform.Length, true, true, false);
            int platformRouteIndex = platformRoute.GetRouteIndex(routeSectionIndex, 0);

            TrackCircuitSection beginSection = platformRoute.First().TrackCircuitSection;
            TrackCircuitSection endSection = platformRoute.Last().TrackCircuitSection;

            int firstRouteIndex = thisRoute.GetRouteIndex(beginSectionIndex, 0);
            int lastRouteIndex = thisRoute.GetRouteIndex(endSectionIndex, 0);

            int signalSectionIndex = -1;
            TrackCircuitSection signalSection = null;
            int signalRouteIndex = -1;

            float fullLength = thisPlatform.Length;

            // path does not extend through station : adjust variables to last section
            if (lastRouteIndex < 0)
            {
                lastRouteIndex = thisRoute.Count - 1;
                routeSectionIndex = lastRouteIndex;
                routeIndex = lastRouteIndex;
                endSectionIndex = lastRouteIndex;
                endSection = thisRoute[lastRouteIndex].TrackCircuitSection;
                endOffset = endSection.Length - 1.0f;
                platformHasEndSignal = (endSection.EndSignals[thisRoute[lastRouteIndex].Direction] != null);
                distanceToEndSignal = 1.0f;

                float newLength = -beginOffset;  // correct new length for begin offset
                for (int sectionRouteIndex = firstRouteIndex; sectionRouteIndex <= lastRouteIndex; sectionRouteIndex++)
                {
                    TrackCircuitSection thisSection = thisRoute[sectionRouteIndex].TrackCircuitSection;
                    newLength += thisSection.Length;
                }

                platformRoute = SignalEnvironment.BuildTempRoute(this, beginSectionIndex, beginOffset, thisElement.Direction, newLength, true, true, false);
                platformRouteIndex = routeIndex;

                deltaLength = newLength - Length; // platform length - train length
                fullLength = newLength;
            }

            // if required, check if there is a signal within the platform
            // not possible if there is only one section
            else if (restrictPlatformToSignal && !platformHasEndSignal && firstRouteIndex != lastRouteIndex)
            {
                bool intermediateSignal = false;

                for (int sectionRouteIndex = lastRouteIndex - 1; sectionRouteIndex >= firstRouteIndex; sectionRouteIndex--)
                {
                    TrackCircuitSection thisSection = thisRoute[sectionRouteIndex].TrackCircuitSection;
                    if (thisSection.EndSignals[thisRoute[sectionRouteIndex].Direction] != null)
                    {
                        intermediateSignal = true;
                        signalSectionIndex = thisSection.Index;
                        signalSection = thisSection;
                        signalRouteIndex = sectionRouteIndex;
                        break;
                    }
                }

                // if signal found, reset all end indicators
                if (intermediateSignal)
                {
                    routeSectionIndex = signalRouteIndex;
                    routeIndex = signalRouteIndex;
                    lastRouteIndex = signalRouteIndex;
                    endSectionIndex = signalSectionIndex;
                    endOffset = signalSection.Length - keepDistanceCloseupSignalM;
                    platformHasEndSignal = true;
                    distanceToEndSignal = 1.0f;

                    endSection = signalSection;

                    float newLength = -beginOffset;  // correct new length for begin offset
                    for (int sectionRouteIndex = firstRouteIndex; sectionRouteIndex <= lastRouteIndex; sectionRouteIndex++)
                    {
                        TrackCircuitSection thisSection = thisRoute[sectionRouteIndex].TrackCircuitSection;
                        newLength += thisSection.Length;
                    }

                    platformRoute = SignalEnvironment.BuildTempRoute(this, beginSectionIndex, beginOffset, thisElement.Direction, newLength, true, true, false);
                    platformRouteIndex = routeIndex;

                    deltaLength = newLength - Length; // platform length - train length
                    fullLength = newLength;
                }
            }

            // extend platform to next signal
            // only if required, platform has no signal and train does not fit into platform
            else if (ExtendPlatformToSignal && !platformHasEndSignal && deltaLength < 0)
            {
                bool nextSignal = false;

                // find next signal in route
                for (int sectionRouteIndex = lastRouteIndex + 1; sectionRouteIndex < thisRoute.Count; sectionRouteIndex++)
                {
                    TrackCircuitSection thisSection = thisRoute[sectionRouteIndex].TrackCircuitSection;
                    if (thisSection.EndSignals[thisRoute[sectionRouteIndex].Direction] != null)
                    {
                        nextSignal = true;
                        signalSectionIndex = thisSection.Index;
                        signalSection = thisSection;
                        signalRouteIndex = sectionRouteIndex;
                        break;
                    }

                }

                // if signal found, reset all end indicators
                if (nextSignal)
                {
                    routeSectionIndex = signalRouteIndex;
                    routeIndex = signalRouteIndex;
                    lastRouteIndex = signalRouteIndex;
                    endSectionIndex = signalSectionIndex;
                    endOffset = signalSection.Length - keepDistanceCloseupSignalM;
                    platformHasEndSignal = true;
                    distanceToEndSignal = 1.0f;

                    endSection = signalSection;

                    float newLength = -beginOffset + endOffset;  // correct new length for begin offset and end offset
                    // do not add last section as that is included through endOffset
                    for (int sectionRouteIndex = firstRouteIndex; sectionRouteIndex < lastRouteIndex; sectionRouteIndex++)
                    {
                        TrackCircuitSection thisSection = thisRoute[sectionRouteIndex].TrackCircuitSection;
                        newLength += thisSection.Length;
                    }

                    platformRoute = SignalEnvironment.BuildTempRoute(this, beginSectionIndex, beginOffset, thisElement.Direction, newLength, true, true, false);
                    platformRouteIndex = routeIndex;

                    deltaLength = newLength - Length; // platform length - train length
                    fullLength = newLength;
                }
            }

            // calculate corrected offsets related to last section
            float beginOffsetCorrection = 0;
            float endOffsetCorrection = 0;

            if (firstRouteIndex < 0)
            {
                for (int iIndex = 0; iIndex < platformRouteIndex - 1; iIndex++)
                {
                    beginOffsetCorrection += platformRoute[iIndex].TrackCircuitSection.Length;
                }
                firstRouteIndex = routeIndex;
                beginOffset -= beginOffsetCorrection;
            }
            else if (firstRouteIndex < routeIndex)
            {
                for (int iIndex = firstRouteIndex; iIndex <= routeIndex - 1; iIndex++)
                {
                    beginOffsetCorrection += thisRoute[iIndex].TrackCircuitSection.Length;
                }
                firstRouteIndex = routeIndex;
                beginOffset -= beginOffsetCorrection;
            }

            if (lastRouteIndex < 0)
            {
                for (int iIndex = platformRouteIndex; iIndex < platformRoute.Count - 1; iIndex++)
                {
                    endOffsetCorrection += platformRoute[iIndex].TrackCircuitSection.Length;
                }
                lastRouteIndex = routeIndex;
                endOffset += endOffsetCorrection;
            }
            else if (lastRouteIndex > routeIndex)
            {
                for (int iIndex = routeIndex; iIndex <= lastRouteIndex - 1; iIndex++)
                {
                    endOffsetCorrection += thisRoute[iIndex].TrackCircuitSection.Length;
                }
                lastRouteIndex = routeIndex;
                endOffset += endOffsetCorrection;
            }

            // relate beginoffset and endoffset to section defined as platform section

            // if station is terminal, check if train is starting or terminating, and set stop position at 0.5 clearing distance from end

            float stopOffset = 0;
            bool forceThroughSignal = false;  // set if rear clearance is defined with force parameter

            // check for terminal position
            if (terminal)
            {
                int startRouteIndex = firstRouteIndex < lastRouteIndex ? firstRouteIndex : lastRouteIndex;
                int endRouteIndex = firstRouteIndex > lastRouteIndex ? firstRouteIndex : lastRouteIndex;

                bool routeNodeBeforeStart = false;
                bool routeNodeAfterEnd = false;

                // check if any junctions in path before start
                for (int iIndex = 0; iIndex < startRouteIndex && !routeNodeBeforeStart; iIndex++)
                {
                    routeNodeBeforeStart = (thisRoute[iIndex].TrackCircuitSection.CircuitType == TrackCircuitType.Junction);
                }

                // check if any junctions in path after end
                for (int iIndex = lastRouteIndex + 1; iIndex < (thisRoute.Count - 1) && !routeNodeAfterEnd; iIndex++)
                {
                    routeNodeAfterEnd = (thisRoute[iIndex].TrackCircuitSection.CircuitType == TrackCircuitType.Junction);
                }

                // check if terminal is at start of route
                if (firstRouteIndex == 0 || !routeNodeBeforeStart)
                {
                    stopOffset = beginOffset + (0.5f * ClearingDistance) + Length;
                }
                // if at end of route use closeup distance
                else if (lastRouteIndex == thisRoute.Count - 1 || !routeNodeAfterEnd)
                {
                    stopOffset = endOffset - keepDistanceCloseupM;
                }
                // if inbetween use safety distance
                else
                {
                    stopOffset = endOffset - (0.5f * ClearingDistance);
                }
            }

            // if train too long : search back for platform with same name
            else
            {
                if (deltaLength < 0)
                {
                    float actualBegin = beginOffset;

                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[beginSectionIndex];

                    // Other platforms in same section

                    if (thisSection.PlatformIndices.Count > 1)
                    {
                        foreach (int nextIndex in thisSection.PlatformIndices)
                        {
                            if (nextIndex != platformIndex)
                            {
                                PlatformDetails otherPlatform = Simulator.Instance.SignalEnvironment.PlatformDetailsList[nextIndex];
                                if (string.Equals(otherPlatform.Name, thisPlatform.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    int otherSectionIndex = thisElement.Direction == 0 ?
                                        otherPlatform.TCSectionIndex[0] :
                                        otherPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                                    if (otherSectionIndex == beginSectionIndex)
                                    {
                                        if (otherPlatform.TrackCircuitOffset[SignalLocation.NearEnd, (TrackDirection)thisElement.Direction] < actualBegin)
                                        {
                                            actualBegin = otherPlatform.TrackCircuitOffset[SignalLocation.NearEnd, (TrackDirection)thisElement.Direction];
                                            fullLength = endOffset - actualBegin;
                                        }
                                    }
                                    else
                                    {
                                        int addRouteIndex = thisRoute.GetRouteIndex(otherSectionIndex, 0);
                                        float addOffset = otherPlatform.TrackCircuitOffset[SignalLocation.FarEnd, (TrackDirection)(thisElement.Direction == 0 ? 1 : 0)];
                                        // offset of begin in other direction is length of available track

                                        if (lastRouteIndex > 0)
                                        {
                                            float thisLength =
                                                thisRoute.GetDistanceAlongRoute(addRouteIndex, addOffset, lastRouteIndex, endOffset, true);
                                            if (thisLength > fullLength)
                                                fullLength = thisLength;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    deltaLength = fullLength - Length;
                }

                // search back along route

                if (deltaLength < 0)
                {
                    float distance = fullLength + beginOffset;
                    bool platformFound = false;

                    for (int iIndex = firstRouteIndex - 1;
                                iIndex >= 0 && distance < 500f && platformFound;
                                iIndex--)
                    {
                        TrackCircuitSection nextSection = thisRoute[iIndex].TrackCircuitSection;

                        foreach (int otherPlatformIndex in nextSection.PlatformIndices)
                        {
                            PlatformDetails otherPlatform = Simulator.Instance.SignalEnvironment.PlatformDetailsList[otherPlatformIndex];
                            if (string.Equals(otherPlatform.Name, thisPlatform.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                fullLength = otherPlatform.Length + distance;
                                // we miss a little bit (offset) - that's because we don't know direction of other platform
                                platformFound = true; // only check for one more
                            }
                        }
                        distance += nextSection.Length;
                    }

                    deltaLength = fullLength - Length;
                }

                // default stopposition : place train in middle of platform
                stopOffset = endOffset - (0.5f * deltaLength);

                // check if position is not beyond end of route
                TrackCircuitSection followingSection = TrackCircuitSection.TrackCircuitList[endSectionIndex];
                float remLength = followingSection.Length - endOffset;

                for (int iSection = lastRouteIndex + 1; iSection < thisRoute.Count; iSection++)
                {
                    followingSection = thisRoute[iSection].TrackCircuitSection;
                    remLength += followingSection.Length;
                    if (followingSection.CircuitType == TrackCircuitType.EndOfTrack)
                    {
                        remLength -= keepDistanceCloseupM; // stay clear from end of track
                    }
                    if (remLength > (0.5f * deltaLength))
                        break; // stop check if length exceeds required overshoot
                }

                stopOffset = Math.Min(stopOffset, endOffset + remLength);

                // keep clear at front
                if (keepClearFront.HasValue)
                {
                    // if force position is set, stop train as required regardless of rear position of train
                    if (forcePosition)
                    {
                        stopOffset = endOffset - keepClearFront.Value;
                    }
                    else
                    // keep clear at front but ensure train is in station
                    {
                        float frontClear = Math.Min(keepClearFront.Value, (endOffset - Length - beginOffset));
                        if (frontClear > 0)
                            stopOffset = endOffset - frontClear;
                    }
                }
                else if (keepClearRear.HasValue)
                {
                    // if force position is set, stop train as required regardless of front position of train
                    // reset hold signal state if front position is passed signal
                    if (forcePosition)
                    {
                        stopOffset = beginOffset + keepClearRear.Value + Length;
                        forceThroughSignal = true;

                        // beyond original platform and beyond section : check for route validity (may not exceed route)
                        if (stopOffset > endOffset && stopOffset > endSection.Length)
                        {
                            float addOffset = stopOffset - endOffset;
                            float overlap = 0f;

                            for (int iIndex = lastRouteIndex + 1; iIndex < thisRoute.Count && overlap < addOffset; iIndex++)
                            {
                                TrackCircuitSection nextSection = thisRoute[iIndex].TrackCircuitSection;
                                overlap += nextSection.Length;
                            }

                            if (overlap < addOffset)
                                stopOffset = endSection.Length + overlap;
                        }
                    }
                    else
                    {
                        // check if space available between end of platform and exit signal
                        float endPosition = endOffset;
                        if (platformHasEndSignal)
                        {
                            endPosition = endOffset + distanceToEndSignal;   // distance to signal
                            endPosition = closeupSignal ? (endPosition - keepDistanceCloseupSignalM - 1.0f) : (endPosition - ClearingDistance - 1.0f);   // correct for clearing distance
                        }

                        stopOffset = Math.Min((beginOffset + keepClearRear.Value + Length), endPosition);

                        //float rearClear = Math.Min(keepClearRear.Value, (endOffset - stopOffset));
                        //if (rearClear > 0) stopOffset = beginOffset + rearClear + Length;
                    }
                }
            }

            // check if stop offset beyond end signal - do not hold at signal

            int EndSignal = -1;
            bool HoldSignal = false;

            // check if train is to reverse in platform
            // if so, set signal at other end as hold signal

            TrackDirection useDirection = (TrackDirection)thisElement.Direction;
            bool inDirection = true;

            if (TCRoute.ReversalInfo[activeSubroute].Valid)
            {
                TrackCircuitReversalInfo thisReversal = TCRoute.ReversalInfo[activeSubroute];
                int reversalIndex = thisReversal.SignalUsed ? thisReversal.LastSignalIndex : thisReversal.LastDivergeIndex;
                if (reversalIndex >= 0 && reversalIndex <= lastRouteIndex) // reversal point is this section or earlier
                {
                    useDirection = useDirection.Reverse();
                    inDirection = false;
                }
            }

            // check for end signal

            if (inDirection)
            {
                if (distanceToEndSignal >= 0)
                {
                    EndSignal = thisPlatform.EndSignals[useDirection];

                    // stop location is in front of signal
                    if (distanceToEndSignal > (stopOffset - endOffset))
                    {
                        HoldSignal = true;

                        // check if stop is too close to signal
                        // if platform length is forced always use closeup
                        if (ExtendPlatformToSignal || restrictPlatformToSignal)
                        {
                            stopOffset = Math.Min(stopOffset, endOffset + distanceToEndSignal - keepDistanceCloseupSignalM - 1.0f);
                        }
                        else if (!closeupSignal && (distanceToEndSignal + (endOffset - stopOffset)) < ClearingDistance)
                        {
                            if (forceThroughSignal)
                            {
                                HoldSignal = false;
                                EndSignal = -1;
                            }
                            else
                            {
                                stopOffset = endOffset + distanceToEndSignal - ClearingDistance - 1.0f;
                                // check if train still fits in platform
                                if ((stopOffset - beginOffset) < Length)
                                {
                                    float keepDistanceM = Math.Max((0.5f * ClearingDistance), (endOffset + distanceToEndSignal) - (beginOffset + Length));
                                    stopOffset = endOffset + distanceToEndSignal - keepDistanceM;
                                }
                            }
                        }
                        else if (closeupSignal && (distanceToEndSignal + (endOffset - stopOffset)) < keepDistanceCloseupSignalM)
                        {
                            if (forceThroughSignal)
                            {
                                HoldSignal = false;
                                EndSignal = -1;
                            }
                            else
                            {
                                stopOffset = endOffset + distanceToEndSignal - keepDistanceCloseupSignalM - 1.0f;
                            }
                        }
                    }
                    // reset hold signal if stop is forced beyond signal
                    else if (forceThroughSignal)
                    {
                        HoldSignal = false;
                        EndSignal = -1;
                    }
                    // if most of train fits in platform then stop at signal
                    else if ((distanceToEndSignal - ClearingDistance + thisPlatform.Length) > (0.6 * Length))
                    {
                        HoldSignal = true;
                        if (closeupSignal || ExtendPlatformToSignal || restrictPlatformToSignal)
                        {
                            stopOffset = endOffset + distanceToEndSignal - keepDistanceCloseupSignalM - 1.0f;
                        }
                        else
                        {
                            stopOffset = endOffset + distanceToEndSignal - ClearingDistance - 1.0f;
                        }
                        // set 1m earlier to give priority to station stop over signal
                    }
                    // if platform positions forced always use closeup
                    else if (ExtendPlatformToSignal || restrictPlatformToSignal)
                    {
                        HoldSignal = true;
                        stopOffset = endOffset + distanceToEndSignal - keepDistanceCloseupSignalM - 1.0f;
                        // set 1m earlier to give priority to station stop over signal
                    }
                    // train does not fit in platform - reset exit signal
                    else
                    {
                        EndSignal = -1;
                    }
                }
            }
            else
            // check in reverse direction
            // end of train is beyond signal
            {
                if (thisPlatform.EndSignals[useDirection] >= 0)
                {
                    if ((beginOffset - thisPlatform.DistanceToSignals[useDirection]) < (stopOffset - Length))
                    {
                        HoldSignal = true;

                        if ((stopOffset - Length - beginOffset + thisPlatform.DistanceToSignals[useDirection]) < ClearingDistance)
                        {
                            stopOffset = beginOffset - thisPlatform.DistanceToSignals[useDirection] + Length + ClearingDistance + 1.0f;
                        }
                    }
                    // if most of train fits in platform then stop at signal
                    else if ((thisPlatform.DistanceToSignals[useDirection] - ClearingDistance + thisPlatform.Length) >
                                  (0.6 * Length))
                    {
                        // set 1m earlier to give priority to station stop over signal
                        stopOffset = beginOffset - thisPlatform.DistanceToSignals[useDirection] + Length + ClearingDistance + 1.0f;

                        // check if stop is clear of end signal (if any)
                        if (thisPlatform.EndSignals[(TrackDirection)thisElement.Direction] != -1)
                        {
                            if (stopOffset < (endOffset + thisPlatform.DistanceToSignals[(TrackDirection)thisElement.Direction]))
                            {
                                HoldSignal = true; // if train fits between signals
                            }
                            else
                            {
                                stopOffset = endOffset + thisPlatform.DistanceToSignals[(TrackDirection)thisElement.Direction] - 1.0f; // stop at end signal
                            }
                        }
                    }
                    // train does not fit in platform - reset exit signal
                    else
                    {
                        EndSignal = -1;
                    }
                }
            }

            // store details
            TrackCircuitRouteElement lastElement = thisRoute[lastRouteIndex];

            return new StationStop(lastElement.TrackCircuitSection.Index, lastElement.Direction, EndSignal, EndSignal >= 0 && HoldSignal, stopOffset, lastRouteIndex);
        }

        /// <summary>
        /// Create new station stop
        /// </summary>
        /// <param name="platformStartID"></param>
        /// <param name="arrivalTime"></param>
        /// <param name="departTime"></param>
        /// <param name="clearingDistanceM"></param>
        /// <param name="minStopDistanceM"></param>
        /// <param name="terminal"></param>
        /// <param name="actMinStopTime"></param>
        /// <param name="keepClearFront"></param>
        /// <param name="keepClearRear"></param>
        /// <param name="forcePosition"></param>
        /// <param name="endStop"></param>
        /// <returns></returns>
        public bool CreateStationStop(int platformStartID, int arrivalTime, int departTime, float clearingDistanceM,
            float minStopDistanceM, bool terminal, int? actMinStopTime, float? keepClearFront, float? keepClearRear, bool forcePosition, bool closeupSignal,
            bool closeup, bool restrictPlatformToSignal, bool extendPlatformToSignal, bool endStop)
        {
            StationStop thisStation = CalculateStationStop(platformStartID, arrivalTime, departTime, clearingDistanceM,
                minStopDistanceM, terminal, actMinStopTime, keepClearFront, keepClearRear, forcePosition, closeupSignal, closeup,
                restrictPlatformToSignal, extendPlatformToSignal, endStop);

            if (thisStation != null)
            {
                bool HoldSignal = thisStation.HoldSignal;
                int EndSignal = thisStation.ExitSignal;

                StationStops.Add(thisStation);

                // if station has hold signal and this signal is the same as the exit signal for previous station, remove the exit signal from the previous station

                if (HoldSignal && StationStops.Count > 1)
                {
                    if (EndSignal == StationStops[StationStops.Count - 2].ExitSignal && StationStops[StationStops.Count - 2].HoldSignal)
                    {
                        StationStops[StationStops.Count - 2].HoldSignal = false;
                        StationStops[StationStops.Count - 2].ExitSignal = -1;
                        HoldingSignals.Remove(EndSignal);
                    }
                }

                // add signal to list of hold signals

                if (HoldSignal)
                {
                    HoldingSignals.Add(EndSignal);
                }
                else
                    HoldingSignals.Remove(EndSignal);
            }
            else
            {
                return (false);
            }

            return (true);
        }

        //================================================================================================//
        /// <summary>
        /// Check initial station
        /// Override from AITrain class
        /// <\summary>

        public override bool CheckInitialStation()
        {
            bool atStation = false;
            if (FormedOf > 0 && base.AtStation)  // if train was formed and at station, train is in initial station
            {
                return (true);
            }

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

        /// <summary>
        /// Check if train is stopped in station
        /// </summary>
        /// <param name="thisPlatform"></param>
        /// <param name="stationDirection"></param>
        /// <param name="stationTCSectionIndex"></param>
        /// <returns></returns>
        internal override bool CheckStationPosition(PlatformDetails thisPlatform, TrackDirection stationDirection, int stationTCSectionIndex)
        {
            bool atStation = false;
            //            PlatformDetails thisPlatform = thisStation.PlatformItem;

            float platformBeginOffset = thisPlatform.TrackCircuitOffset[SignalLocation.NearEnd, stationDirection];
            float platformEndOffset = thisPlatform.TrackCircuitOffset[SignalLocation.FarEnd, stationDirection];
            int endSectionIndex = stationDirection == TrackDirection.Ahead ?
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1] :
                    thisPlatform.TCSectionIndex[0];
            int endSectionRouteIndex = ValidRoutes[Direction.Forward].GetRouteIndex(endSectionIndex, 0);

            int beginSectionIndex = stationDirection == TrackDirection.Reverse ?
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1] :
                    thisPlatform.TCSectionIndex[0];
            int beginSectionRouteIndex = ValidRoutes[Direction.Forward].GetRouteIndex(beginSectionIndex, 0);

            // check position

            int stationIndex = ValidRoutes[Direction.Forward].GetRouteIndex(stationTCSectionIndex, PresentPosition[Direction.Forward].RouteListIndex);

            // if not found from front of train, try from rear of train (front may be beyond platform)
            if (stationIndex < 0)
            {
                stationIndex = ValidRoutes[Direction.Forward].GetRouteIndex(stationTCSectionIndex, PresentPosition[Direction.Backward].RouteListIndex);
            }

            // if rear is in platform, station is valid
            if (PresentPosition[Direction.Backward].RouteListIndex == stationIndex && PresentPosition[Direction.Backward].Offset > platformEndOffset)
            {
                atStation = true;
            }

            // if front is in platform and most of the train is as well, station is valid
            else if (PresentPosition[Direction.Forward].RouteListIndex == stationIndex &&
                    ((thisPlatform.Length - (platformBeginOffset - PresentPosition[Direction.Forward].Offset)) > (Length / 2)))
            {
                atStation = true;
            }

            // if front is beyond platform and rear is not on route or before platform : train spans platform
            else if (PresentPosition[Direction.Forward].RouteListIndex > stationIndex && PresentPosition[Direction.Backward].RouteListIndex < stationIndex)
            {
                atStation = true;
            }

            // if front is beyond platform and rear is in platform section but ahead of position : train spans platform
            else if (PresentPosition[Direction.Forward].RouteListIndex > stationIndex && PresentPosition[Direction.Backward].RouteListIndex == stationIndex && PresentPosition[Direction.Backward].Offset < platformEndOffset)
            {
                atStation = true;
            }

            return (atStation);
        }

        //================================================================================================//
        /// <summary>
        /// Check for next station
        /// Override from AITrain
        /// <\summary>

        public override void SetNextStationAction(bool fromAutopilotSwitch = false)
        {
            // do not set action if stopped in station
            if (MovementState == AiMovementState.StationStop || AtStation)
            {
                return;
            }

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

        /// <summary>
        /// Recalculate station stop
        /// Main method, check if train presently in station
        /// Called e.g. after detach or attach etc. because lenght of train has changed so stop positions must be recalculated
        /// </summary>
        public void RecalculateStationStops()
        {
            bool isAtStation = AtStation || MovementState == AiMovementState.StationStop;
            RecalculateStationStops(isAtStation);
        }

        /// <summary>
        /// Recalculate station stop
        /// Actual calculation
        /// </summary>
        /// <param name="atStation"></param>
        public void RecalculateStationStops(bool atStation)
        {
            int firstStopIndex = atStation ? 1 : 0;

            for (int iStation = firstStopIndex; iStation < StationStops.Count; iStation++)
            {
                StationStop actualStation = StationStops[iStation];
                TrackCircuitPartialPathRoute thisRoute = TCRoute.TCRouteSubpaths[actualStation.SubrouteIndex];
                TrackCircuitRouteElement thisElement = thisRoute[actualStation.RouteIndex];
                PlatformDetails thisPlatform = actualStation.PlatformItem;

                StationStop newStop = CalculateStationStopPosition(TCRoute.TCRouteSubpaths[actualStation.SubrouteIndex], actualStation.RouteIndex, actualStation.PlatformItem,
                    actualStation.SubrouteIndex, actualStation.KeepClearFront, actualStation.KeepClearRear, actualStation.ForcePosition,
                    actualStation.CloseupSignal, actualStation.Closeup, actualStation.RestrictPlatformToSignal, actualStation.ExtendPlatformToSignal,
                    actualStation.Terminal, actualStation.PlatformReference);

                actualStation.RouteIndex = newStop.RouteIndex;
                actualStation.TrackCircuitSectionIndex = newStop.TrackCircuitSectionIndex;
                actualStation.StopOffset = newStop.StopOffset;
                actualStation.HoldSignal = newStop.HoldSignal;
                actualStation.ExitSignal = newStop.ExitSignal;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Start train out of AI train due to 'formed' action
        /// </summary>
        /// <param name="otherTrain"></param>
        /// <returns></returns>

        public bool StartFromAITrain(TTTrain otherTrain, int presentTime, TrackCircuitSection[] occupiedTrack)
        {
            // check if new train has route at present position of front of train
            Direction direction = Direction.Forward;
            int startPositionIndex = TCRoute.TCRouteSubpaths[0].GetRouteIndex(otherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            int usedPositionIndex = startPositionIndex;

            int rearPositionIndex = TCRoute.TCRouteSubpaths[0].GetRouteIndex(otherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);

            if (startPositionIndex < 0)
            {
                direction = Direction.Backward;
                usedPositionIndex = rearPositionIndex;
            }

            // if not found - train cannot start out of other train as there is no valid route - let train start of its own
            if (startPositionIndex < 0 && rearPositionIndex < 0)
            {
                FormedOf = -1;
                FormedOfType = TimetableFormationCommand.None;
                return (false);
            }

            OccupiedTrack.Clear();
            foreach (TrackCircuitSection thisSection in occupiedTrack)
            {
                OccupiedTrack.Add(thisSection);
            }

            int addedSections = AdjustTrainRouteOnStart(startPositionIndex, rearPositionIndex, otherTrain);
            usedPositionIndex += addedSections;

            // copy consist information incl. max speed and type

            if (FormedOfType == TimetableFormationCommand.TerminationFormed)
            {
                Cars.Clear();
                int carId = 0;
                foreach (TrainCar car in otherTrain.Cars)
                {
                    Cars.Add(car);
                    car.Train = this;
                    car.CarID = $"{Number:0000}_{carId:000}";
                    carId++;
                }
                IsFreight = otherTrain.IsFreight;
                Length = otherTrain.Length;
                MassKg = otherTrain.MassKg;
                LeadLocomotiveIndex = otherTrain.LeadLocomotiveIndex;

                // copy other train speed if not restricted for either train
                if (!otherTrain.SpeedRestrictionActive && !SpeedRestrictionActive)
                {
                    TrainMaxSpeedMpS = otherTrain.TrainMaxSpeedMpS;
                }
                AllowedMaxSpeedMpS = otherTrain.AllowedMaxSpeedMpS;
                AllowedMaxSpeedSignalMpS = otherTrain.AllowedMaxSpeedSignalMpS;
                AllowedMaxSpeedLimitMpS = otherTrain.AllowedMaxSpeedLimitMpS;

                FrontTDBTraveller = new Traveller(otherTrain.FrontTDBTraveller);
                RearTDBTraveller = new Traveller(otherTrain.RearTDBTraveller);

                // check if train reversal is required

                if (TCRoute.TCRouteSubpaths[0][usedPositionIndex].Direction != otherTrain.PresentPosition[direction].Direction)
                {
                    ReverseFormation(false);

                    // if reversal is required and units must be detached at start : reverse detached units position
                    if (DetachDetails.TryGetValue(-1, out List<DetachInfo> detachList))
                    {
                        for (int iDetach = detachList.Count - 1; iDetach >= 0; iDetach--)
                        {
                            DetachInfo thisDetach = detachList[iDetach];
                            if (thisDetach.DetachPosition == DetachPositionInfo.Start)
                            {
                                switch (thisDetach.DetachUnits)
                                {
                                    case TransferUnits.AllLeadingPower:
                                        thisDetach.DetachUnits = TransferUnits.AllTrailingPower;
                                        break;

                                    case TransferUnits.AllTrailingPower:
                                        thisDetach.DetachUnits = TransferUnits.AllLeadingPower;
                                        break;

                                    case TransferUnits.UnitsAtEnd:
                                        thisDetach.DetachUnits = TransferUnits.UnitsAtFront;
                                        break;

                                    case TransferUnits.UnitsAtFront:
                                        thisDetach.DetachUnits = TransferUnits.UnitsAtEnd;
                                        break;

                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }

                InitialTrainPlacement(false);

            }
            else if (FormedOfType == TimetableFormationCommand.TerminationTriggered)
            {
                if (TCRoute.TCRouteSubpaths[0][usedPositionIndex].Direction != otherTrain.PresentPosition[direction].Direction)
                {
                    FrontTDBTraveller = new Traveller(otherTrain.RearTDBTraveller, true);
                    RearTDBTraveller = new Traveller(otherTrain.FrontTDBTraveller, true);
                }
                else
                {
                    FrontTDBTraveller = new Traveller(otherTrain.FrontTDBTraveller);
                    RearTDBTraveller = new Traveller(otherTrain.RearTDBTraveller);
                }
                CalculatePositionOfCars();
                InitialTrainPlacement(true);
            }

            // set state
            MovementState = AiMovementState.Static;
            ControlMode = TrainControlMode.Inactive;

            int eightHundredHours = 8 * 3600;
            int sixteenHundredHours = 16 * 3600;

            // if no activate time, set to now + 30
            if (!ActivateTime.HasValue)
            {
                ActivateTime = presentTime + 30;
            }
            // if activate time < 08:00 and present time > 16:00, assume activate time is after midnight
            else if (ActivateTime.Value < eightHundredHours && presentTime > sixteenHundredHours)
            {
                ActivateTime = ActivateTime.Value + (24 * 3600);
            }

            return (true);
        }

        //================================================================================================//
        /// <summary>
        /// Update for pre-update state
        /// Override from AITrain class
        /// <\summary>

        public override void AIPreUpdate(double elapsedClockSeconds)
        {
            // calculate delta speed and speed
            float distanceM = physicsPreUpdate(elapsedClockSeconds);

            // force stop - no forced stop if mode is following and attach is true
            if (distanceM > NextStopDistanceM)
            {
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

            CalculatePositionOfCars(elapsedClockSeconds, distanceM);

            DistanceTravelledM += distanceM;

            // perform overall update

            if (ControlMode == TrainControlMode.TurnTable)
            {
                UpdateTurntable(elapsedClockSeconds);
            }
            else if (ValidRoutes != null && MovementState != AiMovementState.Static)        // no actions required for static objects //
            {
                movedBackward = CheckBackwardClearance();                                       // check clearance at rear //
                UpdateTrainPosition();                                                          // position update         //              
                UpdateTrainPositionInformation();                                               // position linked info    //
                int SignalObjIndex = CheckSignalPassed(0, PresentPosition[Direction.Forward], PreviousPosition[Direction.Forward]);    // check if passed signal  //
                UpdateSectionState(movedBackward);                                              // update track occupation //
                ObtainRequiredActions(movedBackward);                                           // process Actions         //
                UpdateRouteClearanceAhead(SignalObjIndex, movedBackward, elapsedClockSeconds);  // update route clearance  //
                UpdateSignalState(movedBackward);                                               // update signal state     //

                UpdateMinimalDelay();

                // if train ahead and approaching turntable, check if train is beyond turntable
                if (ValidRoutes[Direction.Forward].Last().MovingTableApproachPath > -1 && EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.TrainAhead)
                {
                    CheckTrainBeyondTurntable();
                }

            }
        }

        //================================================================================================//
        /// <summary>
        /// Update train physics during Pre-Update
        /// <\summary>

        public float physicsPreUpdate(double elapsedClockSeconds)
        {

            // Update train physics, position and movement
            // Simplified calculation for use in pre-update phase

            PropagateBrakePressure(elapsedClockSeconds);

            float massKg = 0f;
            foreach (TrainCar car in Cars)
            {
                car.MotiveForceN = 0;
                car.Update(elapsedClockSeconds);
                car.TotalForceN = car.MotiveForceN + car.GravityForceN - car.CurveForceN;
                massKg += car.MassKG;

                if (car.Flipped)
                {
                    car.TotalForceN = -car.TotalForceN;
                    car.SpeedMpS = -car.SpeedMpS;
                }
            }
            MassKg = massKg;

            UpdateCarSpeeds(elapsedClockSeconds);

            double distanceM = LastCar.SpeedMpS * elapsedClockSeconds;
            if (Math.Abs(distanceM) < 0.1f)
                distanceM = 0.0f; //clamp to avoid movement due to calculation noise
            if (double.IsNaN(distanceM))
                distanceM = 0;        //avoid NaN, if so will not move

            if (TrainType == TrainType.Ai && LeadLocomotiveIndex == (Cars.Count - 1) && LastCar.Flipped)
                distanceM = -distanceM;

            return (float)distanceM;
        }

        //================================================================================================//
        /// <summary>
        /// Update train 
        /// </summary>

        public override void Update(double elapsedClockSeconds, bool auxiliaryUpdate = true)
        {
            // Update train physics, position and movement

            PhysicsUpdate(elapsedClockSeconds);

            // Update the UiD of First Wagon
            FirstCarUiD = GetFirstWagonUiD();

            // Check to see if wagons are attached to train
            WagonsAttached = GetWagonsAttachedIndication();

            //Exit here when train is static consist (no further actions required)

            if (GetAiMovementState() == AiMovementState.Static)
            {
                int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
                UpdateAIStaticState(presentTime);
            }

            if (TrainType == TrainType.Static)
                return;

            // perform overall update

            if (ControlMode == TrainControlMode.Manual)                                        // manual mode
            {
                UpdateManual(elapsedClockSeconds);
            }

            else if (TrainType == TrainType.Player && ControlMode == TrainControlMode.TurnTable) // turntable mode
            {
                string infoString = "Do NOT move the train";

                if (LeadLocomotive.ThrottlePercent > 1)
                {
                    infoString = String.Concat(infoString, " ; set throttle to 0");
                }
                if (LeadLocomotive.Direction != MidpointDirection.N || Math.Abs(MUReverserPercent) > 1)
                {
                    infoString = String.Concat(infoString, " ; set reverser to neutral (or 0)");
                }
                simulator.Confirmer.Warning(infoString);

                ActiveTurntable.UpdateTurntableStatePlayer(elapsedClockSeconds);            // update turntable state
            }
            else if (ValidRoutes[Direction.Forward] != null && GetAiMovementState() != AiMovementState.Static)     // no actions required for static objects //
            {
                if (ControlMode != TrainControlMode.OutOfControl)
                    movedBackward = CheckBackwardClearance();  // check clearance at rear if not out of control //
                UpdateTrainPosition();                                                          // position update         //
                UpdateTrainPositionInformation();                                               // position update         //
                int SignalObjIndex = CheckSignalPassed(0, PresentPosition[Direction.Forward], PreviousPosition[Direction.Forward]);   // check if passed signal  //
                UpdateSectionState(movedBackward);                                              // update track occupation //
                ObtainRequiredActions(movedBackward);                                           // process list of actions //

                bool stillExist = true;

                if (TrainType == TrainType.Player)                                              // player train is to check own stations
                {
                    if (MovementState == AiMovementState.Turntable)
                    {
                        ActiveTurntable.UpdateTurntableStatePlayer(elapsedClockSeconds);
                    }
                    else
                    {
                        CheckStationTask();
                        CheckPlayerAttachState();                                               // check for player attach

                        if (ControlMode != TrainControlMode.OutOfControl)
                        {
                            stillExist = CheckRouteActions(elapsedClockSeconds);                 // check routepath (AI check at other point) //
                        }
                    }
                }
                if (stillExist && ControlMode != TrainControlMode.OutOfControl)
                {
                    UpdateRouteClearanceAhead(SignalObjIndex, movedBackward, elapsedClockSeconds);  // update route clearance  //
                    if (MovementState != AiMovementState.Turntable)
                    {
                        UpdateSignalState(movedBackward);                                           // update signal state but not when on turntable
                    }

                    // if train ahead and approaching turntable, check if train is beyond turntable
                    if (ValidRoutes[Direction.Forward].Last().MovingTableApproachPath > -1 && EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.TrainAhead)
                    {
                        CheckTrainBeyondTurntable();
                    }
                }
            }

            // calculate minimal delay (Timetable only)
            UpdateMinimalDelay();

            // check position of train wrt tunnels
            ProcessTunnels();

            // log train details

            if (evaluateTrainSpeed)
            {
                LogTrainSpeed(simulator.ClockTime);
            }

        } // end Update

        //================================================================================================//
        /// <summary>
        /// If approaching turntable and there is a train ahead, check if train is beyond turntable
        /// </summary>

        public void CheckTrainBeyondTurntable()
        {
            TrackCircuitRouteElement lastElement = ValidRoutes[Direction.Forward].Last();
            if (lastElement.MovingTableApproachPath > -1 && simulator.PoolHolder.Pools.TryGetValue(ExitPool, out TimetablePool thisPool))
            {
                float lengthToGoM = thisPool.GetEndOfRouteDistance(TCRoute.TCRouteSubpaths.Last(), PresentPosition[Direction.Forward], lastElement.MovingTableApproachPath);

                if (lengthToGoM < EndAuthorities[Direction.Forward].Distance)
                {
                    EndAuthorities[Direction.Forward].EndAuthorityType = EndAuthorityType.EndOfPath;
                    EndAuthorities[Direction.Forward].Distance = NextStopDistanceM = lengthToGoM + ClearingDistance; // add clearing distance to avoid position lock short of turntable
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Calculate running delay if present time is later than next station arrival
        /// Override from Train class
        /// </summary>

        public void UpdateMinimalDelay()
        {
            int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));

            if (TrainType == TrainType.Ai)
            {
                AITrain thisAI = this as AITrain;
                presentTime = Convert.ToInt32(Math.Floor(thisAI.AI.ClockTime));
            }

            if (StationStops != null && StationStops.Count > 0 && !AtStation)
            {
                if (presentTime > StationStops[0].ArrivalTime)
                {
                    TimeSpan tempDelay = TimeSpan.FromSeconds((presentTime - StationStops[0].ArrivalTime) % (24 * 3600));
                    //skip when delay exceeds 12 hours - that's due to passing midnight
                    if (tempDelay.TotalSeconds < (12 * 3600) && (!Delay.HasValue || tempDelay > Delay.Value))
                    {
                        Delay = tempDelay;
                    }
                }
            }

            // update max speed if separate cruise speed is set
            if (SpeedSettings[SpeedValueType.CruiseSpeed].HasValue)
            {
                if (CruiseMaxDelay.HasValue && Delay.HasValue && Delay.Value.TotalSeconds > CruiseMaxDelay.Value)
                {
                    TrainMaxSpeedMpS = SpeedSettings[SpeedValueType.MaxSpeed].Value;
                }
                else
                {
                    TrainMaxSpeedMpS = SpeedSettings[SpeedValueType.CruiseSpeed].Value;
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// TestAbsDelay
        /// Dummy to allow function for parent classes (Train class) to be called in common methods
        /// </summary>
        /// 
        internal override int TestAbsDelay(int delay, int correctedTime)
        {
            return delay;
        }

        //================================================================================================//
        /// <summary>
        /// Update AI Static state
        /// Override from Train class
        /// </summary>
        /// <param name="presentTime"></param>

        internal override void UpdateAIStaticState(int presentTime)
        {
            // start if start time is reached
            bool reqActivate = false;
            if (ActivateTime.HasValue && !TriggeredActivationRequired)
            {
                if (ActivateTime.Value < (presentTime % (24 * 3600)))
                    reqActivate = true;
                if (ActivateTime > (24 * 3600) && ActivateTime < presentTime)
                    reqActivate = true;
            }

            bool maystart = true;
            if (TriggeredActivationRequired)
            {
                maystart = false;
            }

            // check if anything needs to attach or transfer
            if (reqActivate)
            {
                if (NeedAttach != null && NeedAttach.TryGetValue(-1, out List<int> needAttachList))
                {
                    if (needAttachList.Count > 0)
                    {
                        reqActivate = false;
                        maystart = false;
                    }
                }

                foreach (TrackCircuitSection occSection in OccupiedTrack)
                {
                    if (NeedTrainTransfer.ContainsKey(occSection.Index))
                    {
                        reqActivate = false;
                        maystart = false;
                        break;
                    }
                }
            }

            // check if anything needs be detached
            if (DetachDetails.TryGetValue(-1, out List<DetachInfo> detachList))
            {
                for (int iDetach = detachList.Count - 1; iDetach >= 0; iDetach--)
                {
                    DetachInfo thisDetach = detachList[iDetach];
                    if (thisDetach.Valid)
                    {

                        bool validTime = !thisDetach.DetachTime.HasValue || thisDetach.DetachTime.Value < presentTime;
                        if (thisDetach.DetachPosition == DetachPositionInfo.Start && validTime)
                        {
                            DetachActive[DetachDetailsIndex.DetachDetailsList] = -1;
                            DetachActive[DetachDetailsIndex.DetachActiveList] = iDetach;
                            thisDetach.PerformDetach(this, true);
                            thisDetach.Valid = false;
                        }

                        if (reqActivate && thisDetach.DetachPosition == DetachPositionInfo.Activation)
                        {
                            DetachActive[DetachDetailsIndex.DetachDetailsList] = -1;
                            DetachActive[DetachDetailsIndex.DetachActiveList] = iDetach;
                            thisDetach.PerformDetach(this, true);
                            thisDetach.Valid = false;
                        }
                    }
                }

                if (detachList.Count <= 0)
                    DetachDetails.Remove(-1);
            }

            // check if other train must be activated
            if (reqActivate)
            {
                ActivateTriggeredTrain(TriggerActivationType.Start, -1);
            }

            // switch power
            if (reqActivate && TrainHasPower())
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

                if (TrainType == TrainType.PlayerIntended)
                {
                    TrainType = TrainType.Player;
                }

                PostInit(true);

                if (TrainType == TrainType.Player)
                {
                    SetupStationStopHandling();
                }

                return;
            }

            // switch off power for all engines until 20 secs before start

            if (ActivateTime.HasValue && TrainHasPower() && maystart)
            {
                if (PowerState && ActivateTime.Value < (presentTime - 20))
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
                else if (!PowerState) // switch power on 20 secs before start
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
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set reversal point action
        /// Override from AITrain class
        /// <\summary>

        public override void SetReversalAction()
        {
            if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop)
            {
                return; // station stop required - reversal not valid
            }

            if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.Reversal)
            {
                return; // other reversal still active - reversal not valid
            }

            if (StationStops != null && StationStops.Count > 0 && StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath)
            {
                return; // station stop required in this subpath - reversal not valid
            }

            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
            {
                int reqSection = (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].SignalUsed && !ForceReversal) ?
                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex :
                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex;

                if (reqSection >= 0 && PresentPosition[Direction.Backward].RouteListIndex >= reqSection && TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalActionInserted == false)
                {
                    if (!NeedPickUp && !CheckTransferRequired())
                    {
                        float reqDistance = (SpeedMpS * SpeedMpS * MaxDecelMpSS) + DistanceTravelledM;
                        reqDistance = nextActionInfo != null ? Math.Min(nextActionInfo.RequiredDistance, reqDistance) : reqDistance;

                        nextActionInfo = new AIActionItem(null, AiActionType.Reversal);
                        nextActionInfo.SetParam((PresentPosition[Direction.Forward].DistanceTravelled - 1), 0.0f, reqDistance, PresentPosition[Direction.Forward].DistanceTravelled);
                        MovementState = MovementState != AiMovementState.Stopped ? AiMovementState.Braking : AiMovementState.Stopped;
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// change in authority state - check action
        /// Override from AITrain class
        /// <\summary>

        public override void CheckRequiredAction()
        {
            // check if train ahead
            if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.TrainAhead)
            {
                if (MovementState != AiMovementState.StationStop && MovementState != AiMovementState.Stopped)
                {
                    MovementState = AiMovementState.Following;  // start following
                    CheckReadyToAttach();                         // check for attach
                }
            }
            else if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.ReservedSwitch ||
                EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.Loop ||
                EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.NoPathReserved)
            {
                ResetActions(true);
                NextStopDistanceM = EndAuthorities[Direction.Forward].Distance - 2.0f * JunctionOverlapM;
                CreateTrainAction(SpeedMpS, 0.0f, NextStopDistanceM, null,
                           AiActionType.EndOfAuthority);
            }
            // first handle outstanding actions
            else if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.EndOfPath &&
                (nextActionInfo == null || nextActionInfo.NextAction == AiActionType.EndOfRoute))
            {
                ResetActions(false);
                NextStopDistanceM = EndAuthorities[Direction.Forward].Distance - ClearingDistance;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update train in stopped state
        /// Override from AITrain class
        /// <\summary>

        public override AiMovementState UpdateStoppedState(double elapsedClockSeconds)
        {
            // check if restart is delayed
            if (DelayStart)// && simulator.Settings.TTUseRestartDelays)
            {
                RestdelayS -= (float)elapsedClockSeconds;
                if (RestdelayS <= 0)   // wait time has elapsed - start moving
                {
                    DelayStart = false;
                    RestdelayS = 0;
                    StartMoving(DelayedStartState);
                }

                return (MovementState);
            }

            else if (RestdelayS > 0)
            {
                RestdelayS -= (float)elapsedClockSeconds; // decrease pre-restart wait time while stopped
            }

            if (SpeedMpS > 0 || SpeedMpS < 0)   // if train still running force it to stop
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

                float addOffset = 0;
                if (trainInfo.Count <= 0)
                {
                    addOffset = thisSection.Length - PresentPosition[Direction.Forward].Offset;
                }

                // train not in this section, try reserved sections ahead
                for (int iIndex = startIndex + 1; iIndex <= endIndex && trainInfo.Count <= 0; iIndex++)
                {
                    TrackCircuitSection nextSection = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection;
                    trainInfo = nextSection.TestTrainAhead(this, 0, ValidRoutes[Direction.Forward][iIndex].Direction);
                    if (trainInfo.Count <= 0)
                    {
                        addOffset += nextSection.Length;
                    }
                }

                // if train not ahead, try first section beyond last reserved
                if (trainInfo.Count <= 0 && endIndex < ValidRoutes[Direction.Forward].Count - 1)
                {
                    TrackCircuitSection nextSection = ValidRoutes[Direction.Forward][endIndex + 1].TrackCircuitSection;
                    trainInfo = nextSection.TestTrainAhead(this, 0, ValidRoutes[Direction.Forward][endIndex + 1].Direction);
                }

                // if train found get distance
                if (trainInfo.Count > 0)  // found train
                {
                    foreach (KeyValuePair<Train, float> trainAhead in trainInfo) // always just one
                    {
                        TTTrain OtherTrain = trainAhead.Key as TTTrain;
                        float distanceToTrain = trainAhead.Value + addOffset;

                        if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.TrainAhead)
                        {
                            EndAuthorities[Direction.Forward].Distance = distanceToTrain;
                        }

                        if (Math.Abs(OtherTrain.SpeedMpS) < 0.1f &&
                                    distanceToTrain > FollowDistanceStatTrain)
                        {
                            // allow creeping closer
                            CreateTrainAction(SpeedSettings[SpeedValueType.CreepSpeed].Value, 0.0f,
                                    distanceToTrain, null, AiActionType.TrainAhead);
                            DelayedStartMoving(AiStartMovement.FollowTrain);
                        }

                        else if (Math.Abs(OtherTrain.SpeedMpS) > 0.1f &&
                            distanceToTrain > KeepDistanceMovingTrain)
                        {
                            // train started moving
                            DelayedStartMoving(AiStartMovement.FollowTrain);
                        }
                        else
                        {
                            bool attachToTrain = false;
                            bool pickUpTrain = false;

                            bool transferTrain = false;
                            int? transferStationIndex = null;
                            int? transferTrainIndex = null;

                            CheckReadyToAttach();

                            // check attach details
                            if (AttachDetails != null && AttachDetails.Valid && AttachDetails.ReadyToAttach)
                            {
                                attachToTrain = true;
                            }

                            if (!attachToTrain)
                            {
                                // check pickup details
                                pickUpTrain = CheckPickUp(OtherTrain);

                                // check transfer details
                                transferTrain = CheckTransfer(OtherTrain, ref transferStationIndex, ref transferTrainIndex);
                            }

                            // if to attach to train, start moving
                            if (attachToTrain || pickUpTrain || transferTrain)
                            {
                                DelayedStartMoving(AiStartMovement.FollowTrain);
                            }

                            // if other train in station, check if this train to terminate in station - if so, set state as in station
                            else if (OtherTrain.AtStation && StationStops != null && StationStops.Count == 1 && OtherTrain.StationStops[0].PlatformReference == StationStops[0].PlatformReference)
                            {
                                MovementState = AiMovementState.StationStop;
                            }
                        }
                    }

                    // update action info
                    if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.TrainAhead)
                    {
                        nextActionInfo.ActivateDistanceM = DistanceTravelledM + EndAuthorities[Direction.Forward].Distance;
                    }

                    // if no action, check for station stop (may not be activated due to distance travelled)
                    else if (nextActionInfo == null && StationStops != null && StationStops.Count > 0)
                    {
                        if (StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath &&
                           ValidRoutes[Direction.Forward].GetRouteIndex(StationStops[0].TrackCircuitSectionIndex, PresentPosition[Direction.Forward].RouteListIndex) <= PresentPosition[Direction.Forward].RouteListIndex)
                        // assume to be in station
                        {
                            MovementState = AiMovementState.StationStop;
                        }
                    }
                }
                else
                {
                    // if next action still is train ahead, reset actions
                    if (nextActionInfo.NextAction == AiActionType.TrainAhead)
                    {
                        ResetActions(true);
                    }
                }
                // if train not found, do nothing - state will change next update

            }

            // Other node mode : check distance ahead (path may have cleared)

            else if (ControlMode == TrainControlMode.AutoNode)
            {
                if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.ReservedSwitch || EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.Loop)
                {
                    float ReqStopDistanceM = EndAuthorities[Direction.Forward].Distance - 2.0f * JunctionOverlapM;
                    if (ReqStopDistanceM > ClearingDistance)
                    {
                        NextStopDistanceM = EndAuthorities[Direction.Forward].Distance;
                        DelayedStartMoving(AiStartMovement.SignalCleared);
                    }
                }
                else if (EndAuthorities[Direction.Forward].Distance > ClearingDistance)
                {
                    NextStopDistanceM = EndAuthorities[Direction.Forward].Distance;
                    DelayedStartMoving(AiStartMovement.SignalCleared);
                }
            }

            // signal node : check state of signal

            else if (ControlMode == TrainControlMode.AutoSignal)
            {
                SignalAspectState nextAspect = SignalAspectState.Unknown;
                // there is a next item and it is the next signal
                if (nextActionInfo != null && nextActionInfo.ActiveItem != null &&
                    nextActionInfo.ActiveItem.SignalDetails == NextSignalObjects[Direction.Forward])
                {
                    nextAspect = nextActionInfo.ActiveItem.SignalDetails.SignalLR(SignalFunction.Normal);
                }
                else
                {
                    nextAspect = GetNextSignalAspect(0);
                }

                if (NextSignalObjects[Direction.Forward] == null) // no signal ahead so switch Node control
                {
                    SwitchToNodeControl(PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
                    NextStopDistanceM = EndAuthorities[Direction.Forward].Distance;
                }

                else if (nextAspect > SignalAspectState.Stop &&
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
                        DelayedStartMoving(AiStartMovement.SignalRestricted);
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
                                    // set this signal as passed, and next signal as waiting
                                    signalCleared = false;   // signal is not clear
                                    int nextSignalIndex = NextSignalObjects[Direction.Forward].Signalfound[(int)SignalFunction.Normal];
                                    if (nextSignalIndex >= 0)
                                    {
                                        NextSignalObjects[Direction.Forward] = Simulator.Instance.SignalEnvironment.Signals[nextSignalIndex];

                                        int reqSectionIndex = NextSignalObjects[Direction.Forward].TrackCircuitIndex;
                                        float endOffset = NextSignalObjects[Direction.Forward].TrackCircuitOffset;

                                        DistanceToSignal = GetDistanceToTrain(reqSectionIndex, endOffset);
                                        SignalObjectItems.RemoveAt(0);
                                    }
                                }
                            }
                        }
                    }

                    if (signalCleared)
                    {
                        ResetActions(true);
                        NextStopDistanceM = 5000f; // clear to 5000m, will be updated if required
                        DelayedStartMoving(AiStartMovement.SignalCleared);
                    }
                }

                else if (nextAspect == SignalAspectState.Stop)
                {
                    // if stop but train is well away from signal allow to close
                    if (DistanceToSignal.HasValue && DistanceToSignal.Value > 5 * SignalApproachDistance)
                    {
                        ResetActions(true);
                        DelayedStartMoving(AiStartMovement.PathAction);
                    }
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
                else if (nextActionInfo == null || nextActionInfo.NextAction != AiActionType.SignalAspectStop)
                {
                    if (nextAspect != SignalAspectState.Stop)
                    {
                        DelayedStartMoving(AiStartMovement.SignalCleared);
                    }
                }
            }

            return (MovementState);
        }

        //================================================================================================//
        /// <summary>
        /// Update when train on turntable
        /// </summary>

        public override void UpdateTurntableState(double elapsedTimeSeconds, int presentTime)
        {

            // check if delayed action is due
            if (DelayStart)
            {
                RestdelayS -= (float)elapsedTimeSeconds;
                if (RestdelayS <= 0)   // wait time has elapsed - start moving
                {
                    DelayStart = false;
                    RestdelayS = 0;
                }
                else
                {
                    return;
                }
            }

            // check if turntable available, else exit turntable mode
            if (ActiveTurntable == null || ActiveTurntable.MovingTableState == MovingTableState.Inactive)
            {
                NextStopDistanceM = EndAuthorities[Direction.Forward].Distance;   // set authorized distance
                MovementState = AiMovementState.Stopped;  // set state to stopped to revert to normal working
                return;
            }

            if (ActiveTurntable.CheckTurntableAvailable())
            {
                ActiveTurntable.UpdateTurntableStateAI(elapsedTimeSeconds, presentTime);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update for train in Station state (train is at station)
        /// Override for AITrain class
        /// <\summary>

        public override void UpdateStationState(double elapsedClockSeconds, int presentTime)
        {
            StationStop thisStation = StationStops[0];
            bool removeStation = false;

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
                    thisStation.CalculateDepartTime(this);
                    actualdepart = (int)thisStation.ActualDepart;
                }

                // check for activation of other train
                ActivateTriggeredTrain(TriggerActivationType.StationStop, thisStation.PlatformReference);

                // set reference arrival for any waiting connections
                foreach (int otherTrainNumber in thisStation.ConnectionsWaiting ?? Enumerable.Empty<int>())
                {
                    Train otherTrain = GetOtherTTTrainByNumber(otherTrainNumber);
                    if (otherTrain != null)
                    {
                        foreach (StationStop otherStop in otherTrain.StationStops)
                        {
                            if (string.Equals(thisStation.PlatformItem.Name, otherStop.PlatformItem.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                if (otherStop.ConnectionsAwaited?.ContainsKey(Number) ?? false)
                                {
                                    otherStop.ConnectionsAwaited.Remove(Number);
                                    otherStop.ConnectionsAwaited.Add(Number, (int)thisStation.ActualArrival);
                                }
                            }
                        }
                    }
                }

                // check for detach actions

                if (DetachDetails.TryGetValue(thisStation.PlatformReference, out List<DetachInfo> detachList))
                {
                    for (int iDetach = 0; iDetach < detachList.Count; iDetach++)
                    {
                        DetachInfo thisDetach = detachList[iDetach];
                        if (thisDetach.Valid)
                        {
                            DetachActive[DetachDetailsIndex.DetachDetailsList] = thisStation.PlatformReference;
                            DetachActive[DetachDetailsIndex.DetachActiveList] = iDetach;
                            thisDetach.PerformDetach(this, true);
                            thisDetach.Valid = false;
                        }
                    }
                    DetachDetails.Remove(thisStation.PlatformReference);
                }

                // check for connections

                if (thisStation.ConnectionsAwaited?.Count > 0)
                {
                    int deptime = -1;
                    int needwait = -1;
                    needwait = ProcessConnections(thisStation, out deptime);

                    // if required to wait : exit
                    if (needwait >= 0)
                    {
                        return;
                    }

                    if (deptime >= 0)
                    {
                        actualdepart = Time.Compare.Latest((int)actualdepart, deptime);
                        thisStation.ActualDepart = actualdepart;
                    }
                }

                // check for attachments or transfers

                // waiting for train to attach : exit
                if (NeedAttach.TryGetValue(thisStation.PlatformReference, out List<int> value) && value.Count > 0)
                {
                    return;
                }

                // waiting for transfer : exit
                if (NeedStationTransfer.TryGetValue(thisStation.PlatformReference, out value) && value.Count > 0)
                {
                    return;
                }

                foreach (TrackCircuitSection occSection in OccupiedTrack)
                {
                    // waiting for transfer : exit
                    if (NeedTrainTransfer.ContainsKey(occSection.Index))
                    {
                        return;
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

            // check for activation of other train
            ActivateTriggeredTrain(TriggerActivationType.StationDepart, thisStation.PlatformReference);

            // check if to attach in this platform

            bool readyToAttach = false;
            TTTrain trainToTransfer = null;

            if (AttachDetails != null)
            {
                // check if to attach at this station
                bool attachAtStation = AttachDetails.StationPlatformReference == StationStops[0].PlatformReference;

                // check if to attach at end, train is in last station, not first in and other train is ahead
                if (!attachAtStation && !AttachDetails.FirstIn)
                {
                    if (AttachDetails.StationPlatformReference == -1 && StationStops.Count == 1)
                    {
                        TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[StationStops[0].TrackCircuitSectionIndex];
                        foreach (KeyValuePair<TrainRouted, Direction> trainToCheckInfo in thisSection.CircuitState.OccupationState)
                        {
                            TTTrain otherTrain = trainToCheckInfo.Key.Train as TTTrain;
                            if (otherTrain.OrgAINumber == AttachDetails.TrainNumber)
                            {
                                attachAtStation = true;
                                AttachDetails.StationPlatformReference = StationStops[0].PlatformReference; // set attach is at this station
                            }
                        }
                    }
                }

                readyToAttach = attachAtStation;
            }

            if (readyToAttach)
            {
                trainToTransfer = GetOtherTTTrainByNumber(AttachDetails.TrainNumber);

                // check if train exists
                if (trainToTransfer == null)
                {
                    // if firstin, check if train is among trains to be started, or if it is an autogen train, or if it is waiting to be started
                    if (AttachDetails.FirstIn)
                    {
                        trainToTransfer = simulator.GetAutoGenTTTrainByNumber(AttachDetails.TrainNumber);

                        if (trainToTransfer == null)
                        {
                            trainToTransfer = AI.StartList.GetNotStartedTTTrainByNumber(AttachDetails.TrainNumber, false);
                        }

                        if (trainToTransfer == null)
                        {
                            foreach (TTTrain wtrain in AI.TrainsToAdd)
                            {
                                if (wtrain.Number == AttachDetails.TrainNumber || wtrain.OrgAINumber == AttachDetails.TrainNumber)
                                {
                                    return;  // found train - just wait a little longer
                                }
                            }
                        }

                        // train cannot be found
                        if (trainToTransfer == null)
                        {
                            Trace.TraceInformation("Train {0} : cannot find train {1} to attach", Name, AttachDetails.TrainName);
                            AttachDetails = null;
                        }
                        else
                        {
                            return;  // wait until train exists
                        }
                    }
                    // not first in - train not found
                    else
                    {
                        Trace.TraceInformation("Train {0} : cannot find train {1} to attach", Name, AttachDetails.TrainName);
                        AttachDetails = null;
                    }
                }
                else
                {
                    if (trainToTransfer.AtStation && trainToTransfer.StationStops[0].PlatformReference == AttachDetails.StationPlatformReference)
                    {
                        readyToAttach = AttachDetails.ReadyToAttach = true;
                    }
                    else if (trainToTransfer.TrainType == TrainType.Player && trainToTransfer.AtStation)
                    {
                        readyToAttach = AttachDetails.ReadyToAttach = true;
                    }
                    else
                    {
                        // exit as departure is not allowed
                        return;
                    }
                }

                if (readyToAttach)
                {
                    // if setback required, reverse train
                    if (AttachDetails.SetBack)
                    {
                        // remove any reserved sections
                        RemoveFromTrackNotOccupied(ValidRoutes[Direction.Forward]);

                        // check if train in same section
                        float distanceToTrain = 0.0f;
                        if (trainToTransfer.PresentPosition[Direction.Forward].TrackCircuitSectionIndex == PresentPosition[Direction.Backward].TrackCircuitSectionIndex)
                        {
                            TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][PresentPosition[Direction.Backward].RouteListIndex].TrackCircuitSection;
                            distanceToTrain = thisSection.Length;
                        }
                        else
                        {
                            // get section index of other train in train route
                            int endSectionIndex = ValidRoutes[Direction.Forward].GetRouteIndexBackward(trainToTransfer.PresentPosition[Direction.Forward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].RouteListIndex);
                            if (endSectionIndex < 0)
                            {
                                Trace.TraceWarning("Train {0} : attach to train {1} failed, cannot find path", Name, trainToTransfer.Name);
                            }

                            // get distance to train
                            for (int iSection = PresentPosition[Direction.Forward].RouteListIndex; iSection >= endSectionIndex; iSection--)
                            {
                                TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][iSection].TrackCircuitSection;
                                distanceToTrain += thisSection.Length;
                            }
                        }

                        // create temp route and set as valid route
                        TrackDirection newDirection = PresentPosition[Direction.Forward].Direction.Reverse();
                        TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0.0f, newDirection, distanceToTrain, true, true, false);

                        // set reverse positions
                        (PresentPosition[Direction.Forward], PresentPosition[Direction.Backward]) = (PresentPosition[Direction.Backward], PresentPosition[Direction.Forward]);

                        PresentPosition[Direction.Forward].Reverse(ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].Direction, tempRoute, Length);
                        PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);
                        PresentPosition[Direction.Backward].Reverse(ValidRoutes[Direction.Forward][PresentPosition[Direction.Backward].RouteListIndex].Direction, tempRoute, 0.0f);

                        // reverse formation
                        ReverseFormation(false);

                        // get new route list indices from new route

                        DistanceTravelledM = 0;
                        ValidRoutes[Direction.Forward] = tempRoute;
                        TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath] = new TrackCircuitPartialPathRoute(tempRoute);

                        PresentPosition[Direction.Forward].RouteListIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                        PresentPosition[Direction.Backward].RouteListIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
                    }
                    else
                    {
                        // build path to train - straight forward, set distance of 2000m (should be enough)
                        TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0.0f, PresentPosition[Direction.Backward].Direction, 2000, true, true, false);
                        ValidRoutes[Direction.Forward] = tempRoute;
                        TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath] = new TrackCircuitPartialPathRoute(tempRoute);

                        PresentPosition[Direction.Forward].RouteListIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                        PresentPosition[Direction.Backward].RouteListIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
                    }

                    EndAuthorities[Direction.Forward].LastReservedSection = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                    EndAuthorities[Direction.Backward].LastReservedSection = PresentPosition[Direction.Backward].TrackCircuitSectionIndex;

                    MovementState = AiMovementState.Following;
                    SwitchToNodeControl(PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
                    StartMoving(AiStartMovement.FollowTrain);

                    return;
                }
            }

            // first, check state of signal

            if (thisStation.ExitSignal >= 0 && thisStation.HoldSignal)
            {
                HoldingSignals.Remove(thisStation.ExitSignal);
                var nextSignal = Simulator.Instance.SignalEnvironment.Signals[thisStation.ExitSignal];

                // only request signal if in signal mode (train may be in node control)
                if (ControlMode == TrainControlMode.AutoSignal)
                {
                    nextSignal.RequestClearSignal(ValidRoutes[Direction.Forward], RoutedForward, 0, false, null); // for AI always use direction 0
                }
            }

            // check if station is end of path

            bool[] endOfPath = ProcessEndOfPath(presentTime);

            // check for exit signal

            bool exitSignalStop = false;
            if (thisStation.ExitSignal >= 0 && NextSignalObjects[Direction.Forward] != null && NextSignalObjects[Direction.Forward].Index == thisStation.ExitSignal)
            {
                SignalAspectState nextAspect = GetNextSignalAspect(0);
                exitSignalStop = (nextAspect == SignalAspectState.Stop && !thisStation.NoWaitSignal);
            }

            // if not end of path, check if departure allowed
            if (!endOfPath[0] && exitSignalStop)
            {
                return;  // do not depart if exit signal at danger and waiting is required
            }

            DateTime baseDTd = new DateTime();
            DateTime depTime = baseDTd.AddSeconds(AI.ClockTime);

            // change state if train still exists
            if (endOfPath[1])
            {
                if (MovementState == AiMovementState.StationStop && !exitSignalStop)
                {
                    AtStation = false;
                    thisStation.Passed = true;

                    MovementState = AiMovementState.Stopped;   // if state is still station_stop and ready and allowed to depart - change to stop to check action
                    RestdelayS = DelayedStartSettings[DelayedStartType.StationRestart].FixedPart + (StaticRandom.Next(DelayedStartSettings[DelayedStartType.StationRestart].RandomPart * 10) / 10f);
                    if (!endOfPath[0])
                    {
                        removeStation = true;  // set next station if not at end of path
                    }
                    else if (StationStops.Count > 0 && thisStation.PlatformReference == StationStops[0].PlatformReference)
                    {
                        removeStation = true;  // this station is still set as next station so remove
                    }
                }

                if (thisStation.StopType == StationStopType.Station)
                {
                    Delay = TimeSpan.FromSeconds((presentTime - thisStation.DepartTime) % (24 * 3600));
                }
            }

            if (removeStation)
            {
                StationStops.RemoveAt(0);
            }

            ResetActions(true);
        }

        //================================================================================================//
        /// <summary>
        /// Update for train in Braking state
        /// Override for AITrain class
        /// <\summary>

        public override void UpdateBrakingState(double elapsedClockSeconds, int presentTime)
        {

            // check if action still required

            bool clearAction = false;
            float distanceToGoM = ClearingDistance;

            if (MovementState == AiMovementState.Turntable)
            {
                distanceToGoM = EndAuthorities[Direction.Forward].Distance;
            }

            else if (nextActionInfo == null) // action has been reset - keep status quo
            {
                if (ControlMode == TrainControlMode.AutoNode)  // node control : use control distance
                {
                    distanceToGoM = EndAuthorities[Direction.Forward].Distance;

                    if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.ReservedSwitch)
                    {
                        distanceToGoM = EndAuthorities[Direction.Forward].Distance - 2.0f * JunctionOverlapM;
                    }
                    else if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.EndOfPath || EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.EndOfAuthority)
                    {
                        distanceToGoM = EndAuthorities[Direction.Forward].Distance - (Closeup ? keepDistanceCloseupM : ClearingDistance);
                    }

                    if (distanceToGoM <= 0)
                    {
                        if (SpeedMpS > 0)
                        {
                            AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
                        }
                    }

                    if (distanceToGoM < ClearingDistance && SpeedMpS <= 0)
                    {
                        MovementState = AiMovementState.Stopped;
                        return;
                    }
                }
                else // action cleared - set running or stopped
                {
                    MovementState = SpeedMpS > 0 ? AiMovementState.Running : AiMovementState.Stopped;
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

                if (nextActionInfo.ActiveItem.SignalState >= SignalAspectState.Approach_1)
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
                Alpha10 = 10;
                if (SpeedMpS < AllowedMaxSpeedMpS - 3.0f * SpeedHysteris)
                {
                    AdjustControlsBrakeOff();
                }
                return;
            }

            // check ideal speed

            float requiredSpeedMpS = 0;
            float creepDistanceM = 3.0f * SignalApproachDistance;

            if (MovementState == AiMovementState.Turntable)
            {
                creepDistanceM = distanceToGoM + SignalApproachDistance; // ensure creep distance always exceeds distance to go
                NextStopDistanceM = distanceToGoM;

                // if almost in the middle, apply full brakes
                if (distanceToGoM < 0.25)
                {
                    AdjustControlsBrakeFull();
                }

                // if stopped, move to next state
                if (distanceToGoM < 1 && Math.Abs(SpeedMpS) < 0.05f)
                {
                    ActiveTurntable.SetNextStageOnStopped();
                    return;
                }
            }
            else if (nextActionInfo != null)
            {
                requiredSpeedMpS = nextActionInfo.RequiredSpeedMpS;
                distanceToGoM = nextActionInfo.ActivateDistanceM - PresentPosition[Direction.Forward].DistanceTravelled;

                if (nextActionInfo.ActiveItem != null)
                {
                    distanceToGoM = nextActionInfo.ActiveItem.DistanceToTrain;
                }

                // check if stopped at station

                if (nextActionInfo.NextAction == AiActionType.StationStop)
                {
                    NextStopDistanceM = distanceToGoM;

                    // check if station has exit signal and if signal is clear
                    // if signal is at stop, check if stop position is sufficiently clear of signal

                    if (NextSignalObjects[Direction.Forward] != null && NextSignalObjects[Direction.Forward].SignalLR(SignalFunction.Normal) == SignalAspectState.Stop)
                    {
                        float reqsignaldistance = StationStops[0].CloseupSignal ? keepDistanceCloseupSignalM : SignalApproachDistance;
                        if (distanceToGoM > DistanceToSignal.Value - reqsignaldistance)
                        {
                            distanceToGoM = DistanceToSignal.Value - reqsignaldistance;
                        }
                    }

                    // check if stopped
                    // train is stopped - set departure time

                    if (distanceToGoM < 0.25f * keepDistanceCloseupSignalM)
                    {
                        if (Math.Abs(SpeedMpS) < 0.05f)
                        {
                            SpeedMpS = 0;
                            MovementState = AiMovementState.StationStop;
                        }

                        // perform slow approach to stop
                        else if (distanceToGoM > 0)
                        {
                            if (AITrainBrakePercent < 50)
                            {
                                AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 10);
                                AITrainThrottlePercent = 0;
                            }
                        }

                        else
                        {
                            AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 100);
                            AITrainThrottlePercent = 0;
                        }

                        return;
                    }
                }

                // check speed reduction position reached

                else if (nextActionInfo.RequiredSpeedMpS > 0)
                {
                    if (distanceToGoM <= 0.0f)
                    {
                        AdjustControlsBrakeOff();
                        AllowedMaxSpeedMpS = nextActionInfo.RequiredSpeedMpS;
                        MovementState = AiMovementState.Running;
                        Alpha10 = 5;
                        ResetActions(true);
                        return;
                    }
                }

                // check if approaching reversal point

                else if (nextActionInfo.NextAction == AiActionType.Reversal)
                {
                    if (SpeedMpS < 0.05f)
                        MovementState = AiMovementState.Stopped;
                    RestdelayS = ReverseAddedDelaySperM * Length;
                }

                // check if stopped at signal

                else if (nextActionInfo.RequiredSpeedMpS == 0)
                {
                    NextStopDistanceM = distanceToGoM;
                    float stopDistanceM = SignalApproachDistance;

                    // allow closeup on end of route
                    if (nextActionInfo.NextAction == AiActionType.EndOfRoute && Closeup)
                        stopDistanceM = keepDistanceCloseupM;

                    // set distance for signal
                    else if (nextActionInfo.NextAction == AiActionType.SignalAspectStop)
                        stopDistanceM = ClearingDistance;

                    if (distanceToGoM < stopDistanceM)
                    {
                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
                        AITrainThrottlePercent = 0;
                        if (Math.Abs(SpeedMpS) < 0.05f)
                        {
                            SpeedMpS = 0;
                            MovementState = AiMovementState.Stopped;
                        }
                        else
                        {
                            AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 10);
                        }

                        // if approaching signal and at 0.25 of approach distance and still moving, force stop
                        if (distanceToGoM < (0.25 * SignalApproachDistance) && SpeedMpS > 0 &&
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
                            requiredSpeedMpS = SpeedSettings[SpeedValueType.CreepSpeed].Value;
                        }
                    }
                }
            }

            // keep speed within required speed band

            // preset, also valid for reqSpeed > 0
            float lowestSpeedMpS = requiredSpeedMpS;

            if (requiredSpeedMpS == 0)
            {
                // station stop : use closeup distance as final stop approach
                if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop)
                {
                    creepDistanceM = keepDistanceCloseupSignalM;
                    lowestSpeedMpS = SpeedSettings[SpeedValueType.CreepSpeed].Value;
                }
                // signal : use 3 * signalApproachDistanceM as final stop approach to avoid signal overshoot
                else if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.SignalAspectStop)
                {
                    creepDistanceM = 3.0f * SignalApproachDistance;
                    lowestSpeedMpS =
                        distanceToGoM < creepDistanceM ? (0.5f * SpeedSettings[SpeedValueType.CreepSpeed].Value) : SpeedSettings[SpeedValueType.CreepSpeed].Value;
                }
                // otherwise use clearingDistanceM as approach distance
                else if (nextActionInfo == null && requiredSpeedMpS == 0)
                {
                    creepDistanceM = ClearingDistance;
                    lowestSpeedMpS =
                        distanceToGoM < creepDistanceM ? (0.5f * SpeedSettings[SpeedValueType.CreepSpeed].Value) : SpeedSettings[SpeedValueType.CreepSpeed].Value;
                }
                else
                {
                    lowestSpeedMpS = SpeedSettings[SpeedValueType.CreepSpeed].Value;
                }

            }

            lowestSpeedMpS = Math.Min(lowestSpeedMpS, AllowedMaxSpeedMpS);

            // braking distance - use 0.25 * MaxDecelMpSS as average deceleration (due to braking delay)
            // Videal - Vreq = a * T => T = (Videal - Vreq) / a
            // R = Vreq * T + 0.5 * a * T^2 => R = Vreq * (Videal - Vreq) / a + 0.5 * a * (Videal - Vreq)^2 / a^2 =>
            // R = Vreq * Videal / a - Vreq^2 / a + Videal^2 / 2a - 2 * Vreq * Videal / 2a + Vreq^2 / 2a => R = Videal^2 / 2a - Vreq^2 /2a
            // so : Vreq = SQRT (2 * a * R + Vreq^2)
            // remaining distance is corrected for minimal approach distance as safety margin
            // for requiredSpeed > 0, take hysteris margin off ideal speed so speed settles on required speed
            // for requiredSpeed == 0, use ideal speed, this allows actual speed to be a little higher
            // upto creep distance : set creep speed as lowest possible speed

            float correctedDistanceToGoM = distanceToGoM - creepDistanceM;

            float maxPossSpeedMpS = lowestSpeedMpS;
            if (correctedDistanceToGoM > 0)
            {
                maxPossSpeedMpS = (float)Math.Sqrt(0.25f * MaxDecelMpSS * 2.0f * correctedDistanceToGoM + (requiredSpeedMpS * requiredSpeedMpS));
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

            // speed exceeds allowed maximum - set brakes and clamp speed
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

                // clamp speed
                AdjustControlsFixedSpeed(AllowedMaxSpeedMpS);

                Alpha10 = 5;
            }

            // reached end position
            else if (SpeedMpS > requiredSpeedMpS && distanceToGoM < 0)
            {
                // if required to stop then force stop
                if (requiredSpeedMpS == 0 && nextActionInfo != null && nextActionInfo.NextAction == AiActionType.SignalAspectStop)
                {
                    Trace.TraceInformation($"Train : {Name} ({Number}) forced to stop, at {DistanceTravelledM}, and speed {SpeedMpS}");
                    SpeedMpS = 0;  // force to standstill
                }
                // increase brakes
                else
                {
                    AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
                }
            }

            // speed beyond top threshold
            else if (SpeedMpS > ideal3HighBandMpS)
            {

                if (AITrainThrottlePercent > 0)
                {
                    AdjustControlsThrottleOff();
                }
                else if (AITrainBrakePercent < 50)
                {
                    AdjustControlsBrakeMore(2.0f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    Alpha10 = 10;
                }
                // if at full brake always perform application as it forces braking in case of brake failure (eg due to wheelslip)
                else if (AITrainBrakePercent == 100)
                {
                    AdjustControlsBrakeMore(2.0f * MaxDecelMpSS, elapsedClockSeconds, 50);
                    Alpha10 = 0;
                }
                else if (lastDecelMpSS < 0.5f * idealDecelMpSS)
                {
                    AdjustControlsBrakeMore(2.0f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    Alpha10 = 10;
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }

            // speed just above ideal
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
                            Alpha10 = 10;
                        }
                    }
                    else if (AITrainThrottlePercent <= 50 && Alpha10 <= 0)
                    {
                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                        Alpha10 = 10;
                    }
                }
                else
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
                        Alpha10 = 10;
                    }

                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }

            // speed just below ideal
            else if (SpeedMpS > idealLowBandMpS)
            {
                if (SpeedMpS > LastSpeedMpS)
                {
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                        Alpha10 = 10;
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
                    else if (AITrainThrottlePercent <= 50)
                    {
                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                    }
                    else if (Alpha10 <= 0)
                    {
                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                        Alpha10 = 10;
                    }
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }

            // speed below ideal but above lowest threshold
            else if (SpeedMpS > ideal3LowBandMpS)
            {
                if (AITrainBrakePercent > 50)
                {
                    AdjustControlsBrakeLess(0.0f, elapsedClockSeconds, 20);
                }
                else if (AITrainBrakePercent > 0)
                {
                    AdjustControlsBrakeOff();
                }
                else if (AITrainThrottlePercent <= 50)
                {
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
                else if (SpeedMpS < LastSpeedMpS || Alpha10 <= 0)
                {
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                    Alpha10 = 10;
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }

            // speed below required speed
            else if (SpeedMpS < requiredSpeedMpS || (requiredSpeedMpS == 0 && Math.Abs(SpeedMpS) < 0.1f))
            {
                AdjustControlsBrakeOff();
                if (((SpeedMpS - LastSpeedMpS) / elapsedClockSeconds) < 0.5f * MaxAccelMpSS)
                {
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
                else if (AITrainThrottlePercent > 99)
                {
                    // force setting to 100% to force acceleration
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
                Alpha10 = 5;
            }
            else if (distanceToGoM > 4 * preferredBrakingDistanceM && SpeedMpS < idealLowBandMpS)
            {
                if (AITrainBrakePercent > 0)
                {
                    AdjustControlsBrakeOff();
                }
                else
                {
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
            }
            else if (distanceToGoM > preferredBrakingDistanceM && SpeedMpS < ideal3LowBandMpS)
            {
                if (AITrainBrakePercent > 0)
                {
                    AdjustControlsBrakeOff();
                }
                else
                {
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
            }
            else if (requiredSpeedMpS == 0 && distanceToGoM > creepDistanceM && SpeedMpS < idealLowBandMpS)
            {
                if (AITrainBrakePercent > 0)
                {
                    AdjustControlsBrakeOff();
                }
                else
                {
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
            }
            else if (requiredSpeedMpS == 0 && distanceToGoM > SignalApproachDistance && SpeedMpS < ideal3LowBandMpS)
            {
                if (AITrainBrakePercent > 0)
                {
                    AdjustControlsBrakeOff();
                }
                else
                {
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
            }

            // in preupdate : avoid problems with overshoot due to low update rate
            // check if at present speed train would pass beyond end of authority
            if (PreUpdate)
            {
                if (requiredSpeedMpS == 0 && distanceToGoM < (10.0f * ClearingDistance) && (elapsedClockSeconds * SpeedMpS) > (0.5f * distanceToGoM) && SpeedMpS > SpeedSettings[SpeedValueType.CreepSpeed])
                {
                    SpeedMpS = (0.5f * SpeedMpS);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update in accelerating mode
        /// Override from AITrain class
        /// <\summary>

        public override void UpdateAccelState(double elapsedClockSeconds)
        {

            // check speed

            if (((SpeedMpS - LastSpeedMpS) / elapsedClockSeconds) < 0.5 * MaxAccelMpSS)
            {
                AdjustControlsAccelMore(Efficiency * 0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
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
        /// Update train in following state (train ahead in same section)
        /// Override from AITrain class
        /// <\summary>

        public override void UpdateFollowingState(double elapsedClockSeconds, int presentTime)
        {
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

                    // train not in this section, try reserved sections ahead
                    for (int iIndex = startIndex + 1; iIndex <= endIndex; iIndex++)
                    {
                        TrackCircuitSection nextSection = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection;
                        trainInfo = nextSection.TestTrainAhead(this, 0, ValidRoutes[Direction.Forward][iIndex].Direction);
                        if (trainInfo.Count <= 0)
                        {
                            addOffset += nextSection.Length;
                        }
                    }
                    // if train not ahead, try first section beyond last reserved
                    if (trainInfo.Count <= 0 && endIndex < ValidRoutes[Direction.Forward].Count - 1)
                    {
                        TrackCircuitSection nextSection = ValidRoutes[Direction.Forward][endIndex + 1].TrackCircuitSection;
                        trainInfo = nextSection.TestTrainAhead(this, 0, ValidRoutes[Direction.Forward][endIndex + 1].Direction);
                    }

                    // if train found get distance
                    if (trainInfo.Count > 0)  // found train
                    {
                        foreach (KeyValuePair<Train, float> trainAhead in trainInfo) // always just one
                        {
                            Train OtherTrain = trainAhead.Key;
                            float distanceToTrain = trainAhead.Value + addOffset;
                        }
                    }
                }
                else
                {
                    // ensure train in section is aware of this train in same section if this is required
                    if (PresentPosition[Direction.Backward].TrackCircuitSectionIndex != thisSection.Index)
                    {
                        UpdateTrainOnEnteringSection(thisSection, trainInfo);
                    }
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
                        TTTrain OtherTrain = trainAhead.Key as TTTrain;
                        float distanceToTrain = trainAhead.Value + addOffset;

                        // update action info with new position

                        float keepDistanceTrainM = 0f;
                        bool attachToTrain = false;
                        bool pickUpTrain = false;

                        bool transferTrain = false;
                        int? transferStationIndex = null;
                        int? transferTrainIndex = null;

                        // check attach details
                        if (AttachDetails != null && AttachDetails.Valid && AttachDetails.ReadyToAttach && AttachDetails.TrainNumber == OtherTrain.OrgAINumber)
                        {
                            attachToTrain = true;
                        }

                        // check pickup details
                        if (!attachToTrain)
                        {
                            pickUpTrain = CheckPickUp(OtherTrain);

                            // check transfer details
                            transferTrain = CheckTransfer(OtherTrain, ref transferStationIndex, ref transferTrainIndex);
                        }

                        if (Math.Abs(OtherTrain.SpeedMpS) > 0.1f)
                        {
                            keepDistanceTrainM = KeepDistanceMovingTrain;
                        }
                        else if (!attachToTrain && !pickUpTrain && !transferTrain)
                        {
                            keepDistanceTrainM = (OtherTrain.IsFreight || IsFreight) ? KeepDistanceStatTrainFreight : KeepDistanceStatTrainPassenger;
                            // if closeup is set for termination
                            if (Closeup)
                            {
                                keepDistanceTrainM = keepDistanceTrainAheadCloseupM;
                            }
                            // if train has station stop and closeup set, check if approaching train in station

                            if (StationStops != null && StationStops.Count > 0 && StationStops[0].Closeup)
                            {
                                // other train at station and this is same station
                                if (OtherTrain.AtStation && OtherTrain.StationStops[0].PlatformItem.Name == StationStops[0].PlatformItem.Name)
                                {
                                    keepDistanceTrainM = keepDistanceTrainAheadCloseupM;
                                }
                                // other train in station and this is same station
                                else if (OtherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex == StationStops[0].TrackCircuitSectionIndex)
                                {
                                    keepDistanceTrainM = keepDistanceTrainAheadCloseupM;
                                }
                            }

                            // if reversing on track where train is located, also allow closeup
                            if (TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1 && TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                            {
                                if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex == OtherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex ||
                                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex == OtherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex)
                                {
                                    keepDistanceTrainM = keepDistanceTrainAheadCloseupM;
                                }
                            }
                        }

                        if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.TrainAhead)
                        {
                            NextStopDistanceM = distanceToTrain - keepDistanceTrainM;
                        }

                        // disregard action if train is to attach
                        else if (nextActionInfo != null && !(attachToTrain || pickUpTrain || transferTrain))
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

                        bool atCouplePosition = false;
                        bool thisTrainFront = false;
                        bool otherTrainFront = false;

                        if (Math.Abs(OtherTrain.SpeedMpS) < 0.1f && (attachToTrain || pickUpTrain || transferTrain))
                        {
                            atCouplePosition = CheckCouplePosition(OtherTrain, out thisTrainFront, out otherTrainFront);
                        }

                        if (Math.Abs(OtherTrain.SpeedMpS) < 0.1f && atCouplePosition)
                        {
                            float reqMinSpeedMpS = SpeedSettings[SpeedValueType.AttachSpeed].Value;
                            if (attachToTrain)
                            {
                                // check if any other train needs to be activated
                                ActivateTriggeredTrain(TriggerActivationType.Dispose, -1);

                                TTCouple(OtherTrain, thisTrainFront, otherTrainFront); // couple this train to other train (this train is aborted)
                            }
                            else if (pickUpTrain)
                            {
                                OtherTrain.TTCouple(this, otherTrainFront, thisTrainFront); // couple other train to this train (other train is aborted)
                                NeedPickUp = false;
                            }
                            else if (transferTrain)
                            {
                                TransferInfo thisTransfer = transferStationIndex.HasValue ? TransferStationDetails[transferStationIndex.Value] : TransferTrainDetails[transferTrainIndex.Value][0];
                                thisTransfer.PerformTransfer(OtherTrain, otherTrainFront, this, thisTrainFront);
                                if (transferStationIndex.HasValue)
                                {
                                    TransferStationDetails.Remove(transferStationIndex.Value);
                                }
                                else if (transferTrainIndex.HasValue)
                                {
                                    TransferTrainDetails.Remove(transferTrainIndex.Value);
                                }
                                NeedTransfer = false;
                            }
                        }

                        // check distance and speed
                        else if (Math.Abs(OtherTrain.SpeedMpS) < 0.1f)
                        {
                            float brakingDistance = SpeedMpS * SpeedMpS * 0.5f * (0.5f * MaxDecelMpSS);
                            float reqspeed = (float)Math.Sqrt(distanceToTrain * MaxDecelMpSS);

                            // allow creepspeed, but if to attach, allow only attach speed
                            float maxspeed = attachToTrain || pickUpTrain || transferTrain ? Math.Max(reqspeed / 2, SpeedSettings[SpeedValueType.AttachSpeed].Value) : Math.Max(reqspeed / 2, SpeedSettings[SpeedValueType.CreepSpeed].Value);

                            if (attachToTrain && AttachDetails.SetBack)
                            {
                                maxspeed = SpeedSettings[SpeedValueType.AttachSpeed].Value;
                            }

                            maxspeed = Math.Min(maxspeed, AllowedMaxSpeedMpS); // but never beyond valid speed limit

                            // set brake or acceleration as required

                            if (SpeedMpS > maxspeed)
                            {
                                AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                            }

                            if ((distanceToTrain - brakingDistance) > keepDistanceTrainM * 3.0f)
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
                            else if ((distanceToTrain - brakingDistance) > keepDistanceTrainM + StandardClearingDistanceM)
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
                            else
                            {
                                float reqMinSpeedMpS = attachToTrain || pickUpTrain || transferTrain ? SpeedSettings[SpeedValueType.AttachSpeed].Value : 0.0f;
                                if ((SpeedMpS - reqMinSpeedMpS) > 0.1f)
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
                                            // car.SpeedMpS = car.Flipped ^ (car.IsDriveable && car.Train.IsActualPlayerTrain && ((MSTSLocomotive)car).UsingRearCab) ? -reqMinSpeedMpS : reqMinSpeedMpS;
                                            car.SpeedMpS = car.Flipped ? -reqMinSpeedMpS : reqMinSpeedMpS;
                                        }
                                        SpeedMpS = reqMinSpeedMpS;
                                    }
                                }
                                else if (attachToTrain || pickUpTrain || transferTrain)
                                {
                                    AdjustControlsBrakeOff();
                                    if (SpeedMpS < 0.2 * SpeedSettings[SpeedValueType.CreepSpeed].Value)
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

                                    TTTrain OtherAITrain = OtherTrain as TTTrain;
                                    otherTrainInStation = (OtherAITrain.MovementState == AiMovementState.StationStop || OtherAITrain.MovementState == AiMovementState.Static);

                                    bool thisTrainInStation = (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop);
                                    if (thisTrainInStation)
                                        thisTrainInStation = (StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath);
                                    if (thisTrainInStation)
                                    {
                                        if (otherTrainInStation)
                                        {
                                            thisTrainInStation =
                                                (ValidRoutes[Direction.Forward].GetRouteIndex(StationStops[0].TrackCircuitSectionIndex, PresentPosition[Direction.Forward].RouteListIndex) >= PresentPosition[Direction.Forward].RouteListIndex);
                                        }
                                        else
                                        {
                                            thisTrainInStation =
                                                (ValidRoutes[Direction.Forward].GetRouteIndex(StationStops[0].TrackCircuitSectionIndex, PresentPosition[Direction.Forward].RouteListIndex) == PresentPosition[Direction.Forward].RouteListIndex);
                                        }
                                    }

                                    if (thisTrainInStation)
                                    {
                                        MovementState = AiMovementState.StationStop;
                                        AtStation = true;
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
                        // if getting too close apply full brake
                        else if (distanceToTrain < (2 * ClearingDistance))
                        {
                            AdjustControlsBrakeFull();
                        }
                        // check only if train is close and other train speed is below allowed speed
                        else if (distanceToTrain < keepDistanceTrainM - ClearingDistance && OtherTrain.SpeedMpS < AllowedMaxSpeedMpS)
                        {
                            if (SpeedMpS > (OtherTrain.SpeedMpS + SpeedHysteris) ||
                                SpeedMpS > (MaxFollowSpeed + SpeedHysteris) ||
                                       distanceToTrain < (keepDistanceTrainM - ClearingDistance))
                            {
                                AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                            }
                            else if (SpeedMpS < (OtherTrain.SpeedMpS - SpeedHysteris) &&
                                       SpeedMpS < MaxFollowSpeed &&
                                       distanceToTrain > (keepDistanceTrainM + ClearingDistance))
                            {
                                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 2);
                            }
                        }
                        // perform normal update
                        else
                            UpdateRunningState(elapsedClockSeconds);
                    }
                }

                // train not found - keep moving, state will change next update
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update train in running state
        /// Override from AITrain class
        /// <\summary>

        public override void UpdateRunningState(double elapsedClockSeconds)
        {

            float topBand = AllowedMaxSpeedMpS - ((1.5f - Efficiency) * SpeedHysteris);
            float highBand = Math.Max(0.5f, AllowedMaxSpeedMpS - ((3.0f - 2.0f * Efficiency) * SpeedHysteris));
            float lowBand = Math.Max(0.4f, AllowedMaxSpeedMpS - ((9.0f - 3.0f * Efficiency) * SpeedHysteris));

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
                Alpha10 = 5;
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
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(0.0f, elapsedClockSeconds, 20);
                    }
                    else if (AITrainThrottlePercent > 0)
                    {
                        if (Alpha10 <= 0)
                        {
                            AdjustControlsAccelLess(0.0f, elapsedClockSeconds, 2);
                            Alpha10 = 5;
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
                        Alpha10 = 10;
                    }
                }
                else
                {
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(0.3f * MaxAccelMpSS, elapsedClockSeconds, 20);
                    }
                    else if (Alpha10 <= 0 && AITrainThrottlePercent > 20)
                    {
                        AdjustControlsAccelLess(0.3f * MaxAccelMpSS, elapsedClockSeconds, 5);
                        Alpha10 = 10;
                    }
                    else if (Alpha10 <= 0 && AITrainThrottlePercent < 10)
                    {
                        AdjustControlsAccelMore(0.3f * MaxAccelMpSS, elapsedClockSeconds, 2);
                        Alpha10 = 10;
                    }
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS > lowBand)
            {
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
                    Alpha10 = 0;
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
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
        /// Delay Start Moving
        /// <\summary>

        public void DelayedStartMoving(AiStartMovement reason)
        {
            // do not apply delayed restart while running in pre-update
            if (simulator.PreUpdate)
            {
                RestdelayS = 0.0f;
                DelayStart = false;
                StartMoving(reason);
                return;
            }

            // note : RestDelayS may have a preset value due to previous action (e.g. reverse)

            DelayedStart delayedStart = reason switch
            {
                //AiStartMovement.EndStationStop: // not used as state is processed through stop - rest delay is preset
                AiStartMovement.FollowTrain => DelayedStartSettings[DelayedStartType.FollowRestart],
                AiStartMovement.NewTrain => DelayedStartSettings[DelayedStartType.NewStart],
                AiStartMovement.PathAction => DelayedStartSettings[DelayedStartType.PathRestart],
                AiStartMovement.SignalCleared => DelayedStartSettings[DelayedStartType.PathRestart],
                AiStartMovement.SignalRestricted => DelayedStartSettings[DelayedStartType.PathRestart],
                AiStartMovement.Turntable => DelayedStartSettings[DelayedStartType.MovingTableRestart],
                _ => new DelayedStart(),
            };

            RestdelayS += delayedStart.RemainingDelay();
            DelayStart = true;
            DelayedStartState = reason;
        }

        //================================================================================================//
        /// <summary>
        /// Start Moving
        /// Override from AITrain class
        /// <\summary>

        public override void StartMoving(AiStartMovement reason)
        {
            // reset brakes, set throttle

            if (reason == AiStartMovement.FollowTrain)
            {
                MovementState = AiMovementState.Following;
                AITrainThrottlePercent = 25;
                AdjustControlsBrakeOff();
            }
            else if (reason == AiStartMovement.Turntable)
            {
                if (MovementState != AiMovementState.Static)  // do not restart while still in static mode)
                {
                    MovementState = AiMovementState.Turntable;
                }
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
                Alpha10 = 10;
                AITrainThrottlePercent = 25;
                AdjustControlsBrakeOff();
            }
            else
            {
                MovementState = AiMovementState.Accelerating;
                Alpha10 = 10;
                AITrainThrottlePercent = PreUpdate ? 50 : 25;
                AdjustControlsBrakeOff();
            }

            SetPercentsFromTrainToTrainset();
        }

        //================================================================================================//
        /// <summary>
        /// Calculate initial position
        /// </summary>

        public TrackCircuitPartialPathRoute CalculateInitialTTTrainPosition(ref bool trackClear, Collection<TTTrain> nextTrains)
        {
            bool sectionAvailable = true;

            // calculate train length

            float trainLength = 0f;

            for (var i = Cars.Count - 1; i >= 0; --i)
            {
                var car = Cars[i];
                if (i < Cars.Count - 1)
                {
                    trainLength += car.CouplerSlackM + car.GetCouplerZeroLengthM();
                }
                trainLength += car.CarLengthM;
            }

            // default is no referenced train
            TTTrain otherTTTrain = null;

            // check if to be placed ahead of other train

            if (!String.IsNullOrEmpty(CreateAhead))
            {
                otherTTTrain = GetOtherTTTrainByName(CreateAhead);

                // if not found - check if it is the player train
                if (otherTTTrain == null)
                {
                    if (simulator.PlayerLocomotive != null && simulator.PlayerLocomotive.Train != null &&
                        string.Equals(simulator.PlayerLocomotive.Train.Name, CreateAhead, StringComparison.OrdinalIgnoreCase))
                    {
                        TTTrain playerTrain = simulator.PlayerLocomotive.Train as TTTrain;
                        if (playerTrain.TrainType == TrainType.Player || playerTrain.TrainType == TrainType.PlayerIntended) // train is started
                        {
                            otherTTTrain = simulator.PlayerLocomotive.Train as TTTrain;
                        }
                    }
                }

                // if other train does not yet exist, check if it is on the 'to start' list, and check start-time
                if (otherTTTrain == null)
                {
                    otherTTTrain = AI.StartList.GetNotStartedTTTrainByName(CreateAhead, false);

                    // if other train still does not exist, check if it is on starting list
                    if (otherTTTrain == null && nextTrains != null)
                    {
                        foreach (TTTrain otherTT in nextTrains)
                        {
                            if (string.Equals(otherTT.Name, CreateAhead, StringComparison.OrdinalIgnoreCase))
                            {
                                otherTTTrain = otherTT;
                                break;
                            }
                        }
                    }

                    // if really not found - set error
                    if (otherTTTrain == null)
                    {
                        Trace.TraceWarning($"Creating train : {Name} ; cannot find train {CreateAhead} for initial placement, /ahead qualifier ignored\n");
                        CreateAhead = string.Empty;
                    }
                    else
                    {
                        if (!otherTTTrain.StartTime.HasValue)
                        {
                            Trace.TraceWarning("Creating train : " + Name + " ; train refered in /ahead qualifier is not started by default, /ahead qualifier ignored\n");
                            CreateAhead = string.Empty;
                        }
                        else if (otherTTTrain.StartTime > StartTime)
                        {
                            Trace.TraceWarning("Creating train : " + Name + " ; train refered in /ahead qualifier has later start-time, start time for this train reset\n");
                            StartTime = otherTTTrain.StartTime + 1;
                        }
                        // train is to be started now so just wait
                        else
                        {
                            trackClear = false;
                            return (null);
                        }
                    }
                }
            }

            // get starting position and route

            TrackNode tn = RearTDBTraveller.TrackNode;
            float offset = RearTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)RearTDBTraveller.Direction.Reverse();

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];
            offset = PresentPosition[Direction.Backward].Offset;

            // create route if train has none

            if (ValidRoutes[Direction.Forward] == null)
            {
                ValidRoutes[Direction.Forward] = SignalEnvironment.BuildTempRoute(this, thisSection.Index, PresentPosition[Direction.Backward].Offset, PresentPosition[Direction.Backward].Direction, trainLength, true, true, false);
            }

            // find sections

            float remLength = trainLength;
            int routeIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
            if (routeIndex < 0)
                routeIndex = 0;

            bool sectionsClear = true;

            TrackCircuitPartialPathRoute tempRoute = new TrackCircuitPartialPathRoute();
            TrackCircuitRouteElement thisElement = ValidRoutes[Direction.Forward][routeIndex];

            // check sections if not placed ahead of other train

            if (otherTTTrain != null)
            {
                sectionAvailable = GetPositionAheadOfTrain(otherTTTrain, ValidRoutes[Direction.Forward], ref tempRoute);
            }
            else
            {
                thisSection = thisElement.TrackCircuitSection;
                if (!thisSection.CanPlaceTrain(this, offset, remLength))
                {
                    sectionsClear = false;
                }

                while (remLength > 0 && sectionAvailable)
                {
                    tempRoute.Add(thisElement);
                    remLength -= (thisSection.Length - offset);
                    offset = 0.0f;

                    if (remLength > 0)
                    {
                        if (routeIndex < ValidRoutes[Direction.Forward].Count - 1)
                        {
                            routeIndex++;
                            thisElement = ValidRoutes[Direction.Forward][routeIndex];
                            thisSection = thisElement.TrackCircuitSection;
                            if (!thisSection.CanPlaceTrain(this, offset, remLength))
                            {
                                sectionsClear = false;
                            }
                            offset = 0.0f;
                        }
                        else
                        {
                            Trace.TraceWarning("Not sufficient track to place train {0}", Name);
                            sectionAvailable = false;
                        }
                    }

                }
            }

            trackClear = true;

            if (!sectionAvailable || !sectionsClear)
            {
                trackClear = false;
                tempRoute.Clear();
            }

            return (tempRoute);
        }

        //================================================================================================//
        /// <summary>
        /// Calculate initial train placement
        /// </summary>
        /// <param name="testOccupied"></param>
        /// <returns></returns>

        public bool InitialTrainPlacement(bool testOccupied)
        {
            // for initial placement, use direction 0 only
            // set initial positions

            TrackNode tn = FrontTDBTraveller.TrackNode;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)FrontTDBTraveller.Direction.Reverse();

            PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            DistanceTravelledM = 0.0f;

            tn = RearTDBTraveller.TrackNode;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)RearTDBTraveller.Direction.Reverse();

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);

            // create route to cover all train sections

            TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                        PresentPosition[Direction.Backward].Direction, Length, true, true, false);

            if (ValidRoutes[Direction.Forward] == null)
            {
                ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(tempRoute);
            }

            // get index of first section in route

            int rearIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
            if (rearIndex < 0)
            {
                rearIndex = 0;
            }

            PresentPosition[Direction.Backward].RouteListIndex = rearIndex;

            // get index of front of train

            int frontIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            if (frontIndex < 0)
            {
                Trace.TraceWarning("Start position of front of train {0} ({1}) not on route ", Name, Number);
                frontIndex = 0;
            }

            PresentPosition[Direction.Forward].RouteListIndex = frontIndex;

            // check if train can be placed
            // get index of section in train route //

            int routeIndex = rearIndex;
            List<TrackCircuitSection> placementSections = new List<TrackCircuitSection>();

            // check if route is available - use temp route as trains own route may not cover whole train

            offset = PresentPosition[Direction.Backward].Offset;
            float remLength = Length;
            bool sectionAvailable = true;

            for (int iRouteIndex = 0; iRouteIndex <= tempRoute.Count - 1 && sectionAvailable; iRouteIndex++)
            {
                TrackCircuitSection thisSection = tempRoute[iRouteIndex].TrackCircuitSection;
                if (thisSection.CanPlaceTrain(this, offset, remLength) || !testOccupied)
                {
                    placementSections.Add(thisSection);
                    remLength -= (thisSection.Length - offset);

                    if (remLength > 0)
                    {
                        if (routeIndex < ValidRoutes[Direction.Forward].Count - 1)
                        {
                            routeIndex++;
                            TrackCircuitRouteElement thisElement = ValidRoutes[Direction.Forward][routeIndex];
                            thisSection = thisElement.TrackCircuitSection;
                            offset = 0.0f;
                        }
                        else
                        {
                            Trace.TraceWarning("Not sufficient track to place train {0}", Name);
                            sectionAvailable = false;
                        }
                    }

                }
                else
                {
                    sectionAvailable = false;
                }
            }

            // if not available - return

            if (!sectionAvailable || placementSections.Count <= 0)
            {
                return (false);
            }

            // set any deadlocks for sections ahead of start with end beyond start

            for (int iIndex = 0; iIndex < rearIndex; iIndex++)
            {
                int rearSectionIndex = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection.Index;
                if (TrainDeadlockInfo.TryGet(rearSectionIndex, out List<Dictionary<int, int>> value))
                {
                    foreach (Dictionary<int, int> thisDeadlock in value)
                    {
                        foreach (KeyValuePair<int, int> thisDetail in thisDeadlock)
                        {
                            int endSectionIndex = thisDetail.Value;
                            if (ValidRoutes[Direction.Forward].GetRouteIndex(endSectionIndex, rearIndex) >= 0)
                            {
                                TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[endSectionIndex];
                                endSection.SetDeadlockTrap(Number, thisDetail.Key);
                            }
                        }
                    }
                }
            }

            // set track occupied (if not done yet)

            List<TrackCircuitSection> newPlacementSections = new List<TrackCircuitSection>();
            foreach (TrackCircuitSection thisSection in placementSections)
            {
                if (!thisSection.IsSet(RoutedForward, false))
                {
                    newPlacementSections.Add(thisSection);
                }
            }

            // first reserve to ensure switches are all alligned properly
            foreach (TrackCircuitSection thisSection in newPlacementSections)
            {
                thisSection.Reserve(RoutedForward, ValidRoutes[Direction.Forward]);
            }

            // next set occupied
            foreach (TrackCircuitSection thisSection in newPlacementSections)
            {
                OccupiedTrack.Remove(thisSection);
                thisSection.SetOccupied(RoutedForward);
            }

            // reset TrackOccupied to remove any 'hanging' occupations and set the sections in correct sequence
            OccupiedTrack.Clear();
            foreach (TrackCircuitSection thisSection in placementSections)
            {
                OccupiedTrack.Add(thisSection);
            }

            return (true);
        }

        //================================================================================================//
        /// <summary>
        /// Get position of train if train is placed ahead of other train in same section
        /// </summary>
        /// <param name="otherTTTrain"></param>
        /// <param name="trainRoute"></param>
        /// <param name="tempRoute"></param>
        /// <returns></returns>

        public bool GetPositionAheadOfTrain(TTTrain otherTTTrain, TrackCircuitPartialPathRoute trainRoute, ref TrackCircuitPartialPathRoute tempRoute)
        {
            float remainingLength = Length;

            // get front position of other train
            int otherTrainSectionIndex = otherTTTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            int routeListIndex = trainRoute.GetRouteIndex(otherTrainSectionIndex, 0);

            // front position is not in this trains route - reset clear ahead and check normal path
            if (routeListIndex < 0)
            {
                Trace.TraceWarning("Train : " + Name + " : train referred to in /ahead qualifier is not in train's path, /ahead ignored\n");
                CreateAhead = String.Empty;
                return CalculateInitialTrainPosition().Count > 0;
            }

            // front position is in this trains route - check direction
            TrackCircuitRouteElement thisElement = trainRoute[routeListIndex];

            // not the same direction : cannot place train as front or rear is now not clear
            if (otherTTTrain.PresentPosition[Direction.Forward].Direction != thisElement.Direction)
            {
                Trace.TraceWarning("Train : " + Name + " : train referred to in /ahead qualifier has different direction, train can not be placed \n");
                return (false);
            }

            // train is positioned correctly

            TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

            float startoffset = otherTTTrain.PresentPosition[Direction.Forward].Offset + keepDistanceCloseupM;
            int firstSection = thisElement.TrackCircuitSection.Index;
            bool validPlacement = true;
            // train starts in same section - check rest of section
            if (startoffset <= thisSection.Length)
            {

                tempRoute.Add(thisElement);

                PresentPosition[Direction.Forward].Direction = thisElement.Direction;
                PresentPosition[Direction.Forward].TrackCircuitSectionIndex = thisElement.TrackCircuitSection.Index;
                PresentPosition[Direction.Forward].Offset = startoffset;
                PresentPosition[Direction.Forward].RouteListIndex = trainRoute.GetRouteIndex(thisElement.TrackCircuitSection.Index, 0);

                Dictionary<Train, float> moreTrains = thisSection.TestTrainAhead(this, startoffset, thisElement.Direction);

                // more trains found - check if sufficient space in between, if so, place train
                if (moreTrains.Count > 0)
                {
                    KeyValuePair<Train, float> nextTrainInfo = moreTrains.ElementAt(0);
                    if (nextTrainInfo.Value > Length)
                    {
                        return (true);
                    }

                    // more trains found - cannot place train
                    // do not report as warning - train may move away in time
                    return (false);
                }

                // no other trains in section - determine remaining length
                remainingLength -= (thisSection.Length - startoffset);
                validPlacement = true;
            }
            else
            {
                startoffset = startoffset - thisSection.Length; // offset in next section

                routeListIndex++;
                if (routeListIndex <= trainRoute.Count - 1)
                {
                    thisElement = trainRoute[routeListIndex];
                    firstSection = thisElement.TrackCircuitSection.Index;
                }
                else
                {
                    validPlacement = false;
                }
            }

            // check for rest of train

            float offset = startoffset;

            // test rest of train in rest of route
            while (remainingLength > 0 && validPlacement)
            {
                tempRoute.Add(thisElement);
                remainingLength -= (thisSection.Length - offset);

                if (remainingLength > 0)
                {
                    if (routeListIndex < trainRoute.Count - 1)
                    {
                        routeListIndex++;
                        thisElement = trainRoute[routeListIndex];
                        thisSection = thisElement.TrackCircuitSection;
                        offset = 0;

                        if (!thisSection.CanPlaceTrain(this, 0, remainingLength))
                        {
                            validPlacement = false;
                        }
                    }
                    else
                    {
                        Trace.TraceWarning("Not sufficient track to place train {0}", Name);
                        validPlacement = false;
                    }
                }
            }

            // adjust front traveller to use found offset
            float moved = -RearTDBTraveller.TrackNodeOffset;

            foreach (TrackCircuitRouteElement nextElement in trainRoute)
            {
                if (nextElement.TrackCircuitSection.Index == firstSection)
                {
                    moved += startoffset;
                    break;
                }
                else
                {
                    moved += nextElement.TrackCircuitSection.Length;
                }
            }

            RearTDBTraveller.Move(moved);

            return (validPlacement);
        }

        //================================================================================================//
        /// <summary>
        /// Check if train is close enough to other train to perform coupling
        /// Override from AITrain
        /// </summary>
        /// <param name="attachTrain"></param>
        /// <param name="thisTrainFront"></param>
        /// <param name="otherTrainFront"></param>
        /// <returns></returns>

        public override bool CheckCouplePosition(Train attachTrain, out bool thisTrainFront, out bool otherTrainFront)
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
            Direction otherDirection = Direction.Backward;
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

            //if (PreUpdate) return (true); // in pre-update, being in the same section is good enough

            // check distance to other train
            float dist = usedTraveller.OverlapDistanceM(otherTraveller, false);
            return (dist < 0.1f);
        }

        //================================================================================================//
        /// <summary>
        /// Update Section State - additional
        /// clear waitany actions for this section
        /// Override from Train class
        /// </summary>

        protected override void UpdateSectionStateAdditional(int sectionIndex)
        {
            // clear any entries in WaitAnyList as these are now redundant
            if (waitAnyList != null && waitAnyList.ContainsKey(sectionIndex))
            {
                waitAnyList.Remove(sectionIndex);
                if (waitAnyList.Count <= 0)
                {
                    waitAnyList = null;
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Clear moving table after moving off table
        /// </summary>

        internal override void ClearMovingTable(DistanceTravelledItem action)
        {
            // only if valid reference
            if (ActiveTurntable != null)
            {
                ActiveTurntable.RemoveTrainFromTurntable();
                ActiveTurntable = null;

                // set action to restore original speed
                ClearMovingTableAction clearAction = action as ClearMovingTableAction;

                float reqDistance = DistanceTravelledM + 1;
                ActivateSpeedLimit speedLimit = new ActivateSpeedLimit((DistanceTravelledM + 1), clearAction.OriginalMaxTrainSpeedMpS, clearAction.OriginalMaxTrainSpeedMpS, clearAction.OriginalMaxTrainSpeedMpS);
                RequiredActions.InsertAction(speedLimit);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Initialize player train
        /// </summary>

        public void InitalizePlayerTrain()
        {
            TrainType = TrainType.Player;
            InitializeBrakes();

            foreach (var tcar in Cars)
            {
                if (tcar is MSTSLocomotive)
                {
                    MSTSLocomotive loco = tcar as MSTSLocomotive;
                    loco.SetPower(true);
                    loco.AntiSlip = LeadLocoAntiSlip;
                }
            }

            PowerState = true;
        }

        //================================================================================================//
        /// <summary>
        /// Check couple actions for player train to other train
        /// </summary>
        public void CheckPlayerAttachState()
        {
            // check for attach
            if (AttachDetails != null)
            {
                CheckPlayerAttachTrain();
            }

            // check for pickup
            CheckPlayerPickUpTrain();

            // check for transfer
            CheckPlayerTransferTrain();
        }

        //================================================================================================//
        /// <summary>
        /// Check attach for player train
        /// Perform attach if train is ready
        /// </summary>
        public void CheckPlayerAttachTrain()
        {
            // check for attach for player train
            if (AttachDetails.Valid && AttachDetails.ReadyToAttach)
            {
                TTTrain attachTrain = GetOtherTTTrainByNumber(AttachDetails.TrainNumber);

                if (attachTrain != null)
                {
                    // if in neutral, use forward position
                    Direction direction = MUDirection == MidpointDirection.Reverse ? Direction.Backward : Direction.Backward;

                    // check if train is in same section
                    if (PresentPosition[direction].TrackCircuitSectionIndex == attachTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex ||
                        PresentPosition[direction].TrackCircuitSectionIndex == attachTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex)
                    {
                        bool thisTrainFront = true;
                        bool otherTrainFront = true;
                        bool readyToAttach = CheckCouplePosition(attachTrain, out thisTrainFront, out otherTrainFront);

                        if (readyToAttach)
                        {
                            ProcessRouteEndTimetablePlayer();  // perform end of route actions
                            TTCouple(attachTrain, thisTrainFront, otherTrainFront);
                        }
                    }
                }
                // check if train not yet started
                else
                {
                    attachTrain = AI.StartList.GetNotStartedTTTrainByNumber(AttachDetails.TrainNumber, false);

                    if (attachTrain == null)
                    {
                        attachTrain = simulator.GetAutoGenTTTrainByNumber(AttachDetails.TrainNumber);
                    }
                }

                // train cannot be found
                if (attachTrain == null)
                {
                    Trace.TraceWarning("Train {0} : Train {1} to attach to not found", Name, AttachDetails.TrainName);
                    AttachDetails.Valid = false;
                }
            }
            // check for train to attach in static mode
            else if (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.TrainAhead && AttachDetails.StationPlatformReference < 0 && AttachDetails.Valid)
            {
                for (int iRouteSection = PresentPosition[Direction.Forward].RouteListIndex; iRouteSection < ValidRoutes[Direction.Forward].Count; iRouteSection++)
                {
                    TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][iRouteSection].TrackCircuitSection;
                    if (thisSection.CircuitState.Occupied())
                    {
                        List<TrainRouted> allTrains = thisSection.CircuitState.TrainsOccupying();
                        foreach (TrainRouted routedTrain in allTrains)
                        {
                            TTTrain otherTrain = routedTrain.Train as TTTrain;
                            if (otherTrain.OrgAINumber == AttachDetails.TrainNumber && otherTrain.MovementState == AiMovementState.Static && otherTrain.ActivateTime != null)
                            {
                                AttachDetails.ReadyToAttach = true;
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check pickup for player train
        /// If ready perform pickup
        /// </summary>
        public void CheckPlayerPickUpTrain()
        {
            List<TTTrain> pickUpTrainList = new List<TTTrain>();

            int thisSectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];

            if (thisSection.CircuitState.OccupiedByOtherTrains(RoutedForward))
            {
                List<TrainRouted> otherTrains = thisSection.CircuitState.TrainsOccupying();

                foreach (TrainRouted otherTrain in otherTrains)
                {
                    TTTrain otherTTTrain = otherTrain.Train as TTTrain;
                    CheckPickUp(otherTTTrain);

                    if (NeedPickUp)
                    {
                        // if in neutral, use forward position
                        Direction direction = MUDirection == MidpointDirection.Reverse ? Direction.Backward : Direction.Forward;

                        // check if train is in same section
                        if (PresentPosition[direction].TrackCircuitSectionIndex == otherTTTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex ||
                            PresentPosition[direction].TrackCircuitSectionIndex == otherTTTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex)
                        {
                            bool thisTrainFront = true;
                            bool otherTrainFront = true;
                            bool readyToPickUp = CheckCouplePosition(otherTTTrain, out thisTrainFront, out otherTrainFront);

                            if (readyToPickUp)
                            {
                                otherTTTrain.TTCouple(this, otherTrainFront, thisTrainFront);
                                NeedPickUp = false;
                                break;
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check transfer state for player train
        /// If ready perform transfer
        /// </summary>
        public void CheckPlayerTransferTrain()
        {
            bool transferRequired = false;
            int? transferStationIndex = null;
            int? transferTrainIndex = null;

            TTTrain otherTrain = null;
            TransferInfo thisTransfer = null;

            int thisSectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];

            // check train ahead
            if (thisSection.CircuitState.OccupiedByOtherTrains(RoutedForward))
            {
                Dictionary<Train, float> trainInfo = thisSection.TestTrainAhead(this, PresentPosition[Direction.Forward].Offset, PresentPosition[Direction.Forward].Direction);

                foreach (KeyValuePair<Train, float> thisTrain in trainInfo) // always just one
                {
                    otherTrain = thisTrain.Key as TTTrain;
                    transferRequired = CheckTransfer(otherTrain, ref transferStationIndex, ref transferTrainIndex);
                    break;
                }

                if (transferRequired)
                {
                    // if in neutral, use forward position
                    Direction direction = MUDirection == MidpointDirection.Reverse ? Direction.Backward : Direction.Forward;

                    // check if train is in same section
                    if (PresentPosition[direction].TrackCircuitSectionIndex == otherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex ||
                        PresentPosition[direction].TrackCircuitSectionIndex == otherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex)
                    {
                        bool thisTrainFront = true;
                        bool otherTrainFront = true;
                        bool readyToTransfer = CheckCouplePosition(otherTrain, out thisTrainFront, out otherTrainFront);

                        if (readyToTransfer)
                        {
                            if (transferStationIndex.HasValue)
                            {
                                thisTransfer = TransferStationDetails[transferStationIndex.Value];
                                TransferStationDetails.Remove(transferStationIndex.Value);
                            }
                            else if (transferTrainIndex.HasValue)
                            {
                                thisTransfer = TransferTrainDetails[transferTrainIndex.Value][0];
                                TransferTrainDetails.Remove(transferTrainIndex.Value);
                            }
                        }

                        if (thisTransfer != null)
                        {
                            thisTransfer.PerformTransfer(otherTrain, otherTrainFront, this, thisTrainFront);
                            NeedTransfer = false;
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Test if section is access to pool
        /// If so, extend route to pool storage road
        /// Override from Train class
        /// </endsummary>
        internal override bool CheckPoolAccess(int sectionIndex)
        {
            bool validPool = false;

            if (PoolAccessSection == sectionIndex)
            {
                TimetablePool thisPool = simulator.PoolHolder.Pools[ExitPool];
                int PoolStorageState = PoolAccessState.PoolInvalid;

                TrackCircuitPartialPathRoute newRoute = thisPool.SetPoolExit(this, out PoolStorageState, true);

                // if pool is valid, set new path
                if (newRoute != null)
                {
                    // reset pool access
                    PoolAccessSection = -1;

                    PoolStorageIndex = PoolStorageState;
                    TCRoute.TCRouteSubpaths[TCRoute.TCRouteSubpaths.Count - 1] = new TrackCircuitPartialPathRoute(newRoute);
                    if (TCRoute.ActiveSubPath == TCRoute.TCRouteSubpaths.Count - 1)
                    {
                        ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(newRoute);

                        // remove end-of-route action and recreate it as it may be altered on approach to moving table
                        DistanceTravelledItem removeAction = null;
                        foreach (DistanceTravelledItem thisAction in RequiredActions)
                        {
                            if (thisAction.GetType() == typeof(AIActionItem))
                            {
                                AIActionItem thisAIAction = thisAction as AIActionItem;
                                if (thisAIAction.NextAction == AiActionType.EndOfRoute)
                                {
                                    removeAction = thisAction;
                                    break;
                                }
                            }
                        }

                        if (removeAction != null)
                            RequiredActions.Remove(removeAction);

                        // set new end of route action
                        SetEndOfRouteAction();
                    }
                    validPool = true;
                }

                // if pool is claimed, set valid pool but take no further actions
                else if (PoolStorageState == PoolAccessState.PoolClaimed)
                {
                    validPool = true;
                }

                // if pool is not valid, reset pool info
                else
                {
                    // reset pool access
                    PoolAccessSection = -1;
                    ExitPool = String.Empty;
                }
            }

            return (validPool);
        }

        //================================================================================================//
        /// <summary>
        /// Test for Call On state if train is stopped at signal
        /// CallOn state for TT mode depends on $callon flag, or attach/pickup/transfer requirement for train ahead
        /// Override from Train class
        /// </summary>
        /// <param name="thisSignal"></param>
        /// <param name="allowOnNonePlatform"></param>
        /// <param name="thisRoute"></param>
        /// <param name="dumpfile"></param>
        /// <returns></returns>
        internal override bool TestCallOn(Signal thisSignal, bool allowOnNonePlatform, TrackCircuitPartialPathRoute thisRoute)
        {
            // always allow if set for stable working
            if (StableCallOn)
            {
                return (true);
            }

            // test for pool
            if (PoolStorageIndex >= 0)
            {
                TimetablePool thisPool = simulator.PoolHolder.Pools[ExitPool];
                if (thisPool.TestRouteLeadingToPool(thisRoute, PoolStorageIndex, Name))
                {
                    return (true);
                }
            }

            // loop through sections in signal route
            bool allclear = true;
            bool intoPlatform = false;
            bool firstTrainFound = false;

            foreach (TrackCircuitRouteElement routeElement in thisRoute)
            {
                TrackCircuitSection routeSection = routeElement.TrackCircuitSection;

                // if train is to attach to train in section, allow callon if train is stopped

                if (routeSection.CircuitState.OccupiedByOtherTrains(RoutedForward))
                {
                    firstTrainFound = true;
                    Dictionary<Train, float> trainInfo = routeSection.TestTrainAhead(this, 0, routeElement.Direction);

                    foreach (KeyValuePair<Train, float> thisTrain in trainInfo) // always just one
                    {
                        TTTrain occTTTrain = thisTrain.Key as TTTrain;
                        AiMovementState movState = occTTTrain.ControlMode == TrainControlMode.Inactive ? AiMovementState.Static : occTTTrain.MovementState;

                        // if train is moving - do not allow call on
                        if (Math.Abs(occTTTrain.SpeedMpS) > 0.1f)
                        {
                            return (false);
                        }

                        bool goingToAttach = false;
                        if (AttachDetails != null && AttachDetails.Valid && AttachDetails.ReadyToAttach && AttachDetails.TrainNumber == occTTTrain.OrgAINumber)
                        {
                            goingToAttach = true;
                        }

                        if (goingToAttach)
                        {
                            if (movState == AiMovementState.Stopped || movState == AiMovementState.StationStop || movState == AiMovementState.Static)
                            {
                                return (true);
                            }
                            else if ((occTTTrain.TrainType == TrainType.Player || occTTTrain.TrainType == TrainType.PlayerIntended) && occTTTrain.AtStation)
                            {
                                return (true);
                            }
                            else
                            {
                                return (false);
                            }
                        }

                        // check if going to pick up or transfer
                        int? transferStationIndex = null;
                        int? transferTrainIndex = null;

                        if (CheckPickUp(occTTTrain))
                        {
                            return (true);
                        }
                        else if (CheckTransfer(occTTTrain, ref transferStationIndex, ref transferTrainIndex))
                        {
                            return (true);
                        }
                    }

                    // check if route leads into platform

                    if (routeSection.PlatformIndices.Count > 0)
                    {
                        intoPlatform = true;
                        PlatformDetails thisPlatform = Simulator.Instance.SignalEnvironment.PlatformDetailsList[routeSection.PlatformIndices[0]];

                        // stop is next station stop and callon is set
                        if (StationStops.Count > 0 && StationStops[0].PlatformItem.Name == thisPlatform.Name && StationStops[0].CallOnAllowed)
                        {
                            // only allow if train ahead is stopped
                            foreach (KeyValuePair<TrainRouted, Direction> occTrainInfo in routeSection.CircuitState.OccupationState)
                            {
                                Train.TrainRouted occTrain = occTrainInfo.Key;
                                TTTrain occTTTrain = occTrain.Train as TTTrain;
                                AiMovementState movState = occTTTrain.ControlMode == TrainControlMode.Inactive ? AiMovementState.Static : occTTTrain.MovementState;

                                if (movState == AiMovementState.Stopped || movState == AiMovementState.StationStop || movState == AiMovementState.Static)
                                {
                                }
                                else if (occTTTrain.TrainType == TrainType.Player && occTTTrain.AtStation)
                                {
                                }
                                else
                                {
                                    allclear = false;
                                    break; // no need to check for other trains
                                }
                            }
                        }
                        else
                        {
                            allclear = false;
                        }
                    }

                    // if first train found, check rest of route for platform
                    if (firstTrainFound && !intoPlatform)
                    {
                        int thisSectionRouteIndex = thisRoute.GetRouteIndex(routeSection.Index, 0);
                        for (int iSection = thisSectionRouteIndex + 1; iSection < thisRoute.Count && !intoPlatform; iSection++)
                        {
                            routeSection = thisRoute[iSection].TrackCircuitSection;
                            if (routeSection.PlatformIndices.Count > 0)
                            {
                                PlatformDetails thisPlatform = Simulator.Instance.SignalEnvironment.PlatformDetailsList[routeSection.PlatformIndices[0]];
                                if (StationStops.Count > 0) // train has stops
                                {
                                    if (string.Equals(StationStops[0].PlatformItem.Name, thisPlatform.Name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        intoPlatform = StationStops[0].CallOnAllowed;
                                    }
                                }
                            }
                        }
                    }

                    if (firstTrainFound)
                        break;
                }
            }

            if (intoPlatform)
            {
                return (allclear);
            }
            else
            {
                // path does not lead into platform - return state as defined in call
                return (allowOnNonePlatform);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check if train is ready to attach
        /// </summary>
        public void CheckReadyToAttach()
        {
            // test for attach for train ahead
            if (AttachDetails != null && AttachDetails.StationPlatformReference < 0 && !AttachDetails.ReadyToAttach)
            {
                for (int iRoute = PresentPosition[Direction.Forward].RouteListIndex; iRoute < ValidRoutes[Direction.Forward].Count && !AttachDetails.ReadyToAttach; iRoute++)
                {
                    TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][iRoute].TrackCircuitSection;
                    if (thisSection.CircuitState.Occupied())
                    {
                        List<TrainRouted> allTrains = thisSection.CircuitState.TrainsOccupying();
                        foreach (TrainRouted routedTrain in allTrains)
                        {
                            TTTrain otherTrain = routedTrain.Train as TTTrain;
                            if (otherTrain.OrgAINumber == AttachDetails.TrainNumber && otherTrain.MovementState == AiMovementState.Static && otherTrain.ActivateTime != null)
                            {
                                AttachDetails.ReadyToAttach = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check if train is ready to pick up
        /// </summary>
        /// <param name="otherTrain"></param>
        /// <returns></returns>
        public bool CheckPickUp(TTTrain otherTrain)
        {
            bool pickUpTrain = false;

            // if allready set, no need to check again
            if (NeedPickUp)
            {
                return (true);
            }

            // pick up is only possible if train is stopped, inactive and not reactivated at any time
            if (Math.Abs(otherTrain.SpeedMpS) < 0.1f && otherTrain.ControlMode == TrainControlMode.Inactive && otherTrain.ActivateTime == null)
            {
                // check train
                if (PickUpTrains.Contains(otherTrain.OrgAINumber))
                {
                    pickUpTrain = true;
                    PickUpTrains.Remove(otherTrain.Number);
                    NeedPickUp = true;
                }

                // check platform location
                else
                {
                    foreach (TrackCircuitSection thisSection in otherTrain.OccupiedTrack)
                    {
                        foreach (int thisPlatform in thisSection.PlatformIndices)
                        {
                            foreach (int platformReference in Simulator.Instance.SignalEnvironment.PlatformDetailsList[thisPlatform].PlatformReference)
                            {
                                if (PickUpStatic.Contains(platformReference))
                                {
                                    pickUpTrain = true;
                                    PickUpStatic.Remove(platformReference);
                                    NeedPickUp = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // check if train is at end of path and pickup on forms is required

                if (!pickUpTrain && PickUpStaticOnForms)
                {
                    // train is not in last subpath so pickup cannot be valid
                    if (TCRoute.ActiveSubPath != TCRoute.TCRouteSubpaths.Count - 1)
                    {
                        return (false);
                    }

                    // check if we are at end of route
                    if (CheckEndOfRoutePositionTT())
                    {
                        pickUpTrain = true;
                        PickUpStaticOnForms = false;
                        NeedPickUp = true;
                    }
                    // check position other train or check remaining route
                    else
                    {
                        int otherTrainRearIndex = ValidRoutes[Direction.Forward].GetRouteIndex(otherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex, PresentPosition[Direction.Forward].RouteListIndex);
                        int otherTrainFrontIndex = ValidRoutes[Direction.Forward].GetRouteIndex(otherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Forward].RouteListIndex);

                        bool validtrain = false;

                        // other train front or rear is in final section
                        if (otherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex == ValidRoutes[Direction.Forward][ValidRoutes[Direction.Forward].Count - 1].TrackCircuitSection.Index)
                        {
                            validtrain = true;
                        }
                        else if (otherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex == ValidRoutes[Direction.Forward][ValidRoutes[Direction.Forward].Count - 1].TrackCircuitSection.Index)
                        {
                            validtrain = true;
                        }
                        // other train front or rear is not on our route - other train stretches beyond end of route
                        else if (otherTrainRearIndex < 0 || otherTrainFrontIndex < 0)
                        {
                            validtrain = true;
                        }
                        // check if length of remaining path is less than safety clearance
                        // use intended route, not actual route as that may be restricted
                        else
                        {
                            int useindex = Math.Max(otherTrainRearIndex, otherTrainFrontIndex);
                            float remLength = 0;

                            for (int iElement = useindex + 1; iElement < TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count; iElement++)
                            {
                                remLength += TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][iElement].TrackCircuitSection.Length;
                            }

                            if (remLength < endOfRouteDistance)
                            {
                                validtrain = true;
                            }
                        }

                        // check if there are any other trains in remaining path
                        if (!validtrain)
                        {
                            bool moretrains = false;
                            int useindex = Math.Max(otherTrainRearIndex, otherTrainFrontIndex);

                            for (int iElement = useindex + 1; iElement < TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count; iElement++)
                            {
                                TrackCircuitSection thisSection = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][iElement].TrackCircuitSection;
                                List<Train.TrainRouted> trainsInSection = thisSection.CircuitState.TrainsOccupying();
                                if (trainsInSection.Count > 0)
                                {
                                    moretrains = true;
                                    break;
                                }
                            }
                            validtrain = !moretrains;
                        }

                        if (validtrain)
                        {
                            pickUpTrain = true;
                            PickUpStaticOnForms = false;
                            NeedPickUp = true;
                        }
                    }
                }
            }

            return (pickUpTrain);
        }

        //================================================================================================//
        /// <summary>
        /// Check if train is ready to transfer
        /// </summary>
        /// <param name="otherTrain"></param>
        /// <param name="stationTransferIndex"></param>
        /// <param name="trainTransferIndex"></param>
        /// <returns></returns>
        public bool CheckTransfer(TTTrain otherTrain, ref int? stationTransferIndex, ref int? trainTransferIndex)
        {
            bool transferTrain = false;

            // transfer is only possible if train is stopped, either at station (station transfer) or as inactive (train transfer)
            if (Math.Abs(otherTrain.SpeedMpS) > 0.1f)
            {
                return (transferTrain);
            }

            // train transfer
            if (otherTrain.ControlMode == TrainControlMode.Inactive)
            {
                if (TransferTrainDetails.ContainsKey(otherTrain.OrgAINumber))
                {
                    transferTrain = true;
                    trainTransferIndex = otherTrain.OrgAINumber;
                }
                // static transfer required and train is static, set this train number
                else if (TransferTrainDetails.TryGetValue(-99, out List<TransferInfo> value) && otherTrain.MovementState == AiMovementState.Static && otherTrain.Forms < 0)
                {
                    TransferTrainDetails.Add(otherTrain.OrgAINumber, value);
                    TransferTrainDetails.Remove(-99);
                    transferTrain = true;
                    trainTransferIndex = otherTrain.OrgAINumber;
                }

                // if found, no need to look any further
                if (transferTrain)
                {
                    return (transferTrain);
                }
            }

            // station transfer
            if (otherTrain.AtStation)
            {
                if (otherTrain.StationStops != null && otherTrain.StationStops.Count > 0)
                {
                    int stationIndex = otherTrain.StationStops[0].PlatformReference;
                    if (TransferStationDetails.TryGetValue(stationIndex, out TransferInfo thisTransfer))
                    {
                        if (thisTransfer.TrainNumber == otherTrain.OrgAINumber)
                        {
                            transferTrain = true;
                            stationTransferIndex = stationIndex;
                        }
                    }
                }
            }

            // transfer at dispose - check if train in required section
            if (!transferTrain)
            {
                if (TransferTrainDetails.ContainsKey(otherTrain.OrgAINumber))
                {
                    foreach (TrackCircuitSection occSection in otherTrain.OccupiedTrack)
                    {
                        if (otherTrain.NeedTrainTransfer.ContainsKey(occSection.Index))
                        {
                            transferTrain = true;
                            trainTransferIndex = otherTrain.OrgAINumber;
                            break;
                        }
                    }
                }
            }

            return (transferTrain);
        }

        //================================================================================================//
        /// <summary>
        /// Check if transfer is required
        /// </summary>
        /// <returns></returns>
        public bool CheckTransferRequired()
        {
            bool transferRequired = false;

            // check if state allready set, if so return state

            if (NeedTransfer)
            {
                return (NeedTransfer);
            }

            // check if transfer required
            if ((TransferStationDetails != null && TransferStationDetails.Count > 0) || (TransferTrainDetails != null && TransferTrainDetails.Count > 0))
            {
                bool firstTrainFound = false;

                for (int iRouteIndex = PresentPosition[Direction.Forward].RouteListIndex; iRouteIndex < ValidRoutes[Direction.Forward].Count && !firstTrainFound; iRouteIndex++)
                {
                    TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][iRouteIndex].TrackCircuitSection;
                    if (thisSection.CircuitState.OccupiedByOtherTrains(RoutedForward))
                    {
                        firstTrainFound = true;
                        Dictionary<Train, float> trainInfo = thisSection.TestTrainAhead(this, 0, ValidRoutes[Direction.Forward][iRouteIndex].Direction);

                        foreach (KeyValuePair<Train, float> thisTrain in trainInfo) // always just one
                        {
                            int? transferStationIndex = 0;
                            int? transferTrainIndex = 0;
                            TTTrain otherTrain = thisTrain.Key as TTTrain;

                            if (CheckTransfer(otherTrain, ref transferStationIndex, ref transferTrainIndex))
                            {
                                transferRequired = true;
                                break;
                            }
                        }
                    }
                }
            }

            return (transferRequired);
        }

        //================================================================================================//
        /// <summary>
        /// Insert action item for end-of-route
        /// Override from AITrain class
        /// <\summary>

        public override void SetEndOfRouteAction()
        {
            // check if route leads to moving table

            float lengthToGoM = 0;

            TrackCircuitRouteElement lastElement = ValidRoutes[Direction.Forward].Last();
            if (lastElement.MovingTableApproachPath > -1 && simulator.PoolHolder.Pools.TryGetValue(ExitPool, out TimetablePool thisPool))
            {
                lengthToGoM = thisPool.GetEndOfRouteDistance(TCRoute.TCRouteSubpaths.Last(), PresentPosition[Direction.Forward], lastElement.MovingTableApproachPath);
            }

            // remaining length first section
            else
            {
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
                lengthToGoM = thisSection.Length - PresentPosition[Direction.Forward].Offset;
                // go through all further sections

                for (int iElement = PresentPosition[Direction.Forward].RouteListIndex + 1; iElement < ValidRoutes[Direction.Forward].Count; iElement++)
                {
                    TrackCircuitRouteElement thisElement = ValidRoutes[Direction.Forward][iElement];
                    thisSection = thisElement.TrackCircuitSection;
                    lengthToGoM += thisSection.Length;
                }

                lengthToGoM -= 5.0f; // keep save distance from end

                // if last section does not end at signal or next section is switch, set back overlap to keep clear of switch
                // only do so for last subroute to avoid falling short of reversal points
                // only do so if last section is not a station and closeup is not set for dispose

                TrackCircuitSection lastSection = lastElement.TrackCircuitSection;
                if (lastSection.EndSignals[lastElement.Direction] == null && TCRoute.ActiveSubPath == (TCRoute.TCRouteSubpaths.Count - 1))
                {
                    int nextIndex = lastSection.Pins[lastElement.Direction, SignalLocation.NearEnd].Link;
                    bool lastIsStation = false;

                    if (StationStops != null && StationStops.Count > 0)
                    {
                        StationStop lastStop = StationStops.Last();
                        if (lastStop.SubrouteIndex == TCRoute.TCRouteSubpaths.Count - 1 && lastStop.PlatformItem.TCSectionIndex.Contains(lastSection.Index))
                        {
                            lastIsStation = true;
                        }
                    }

                    // closeup to junction if closeup is set except on last stop or when storing in pool
                    bool reqcloseup = Closeup && String.IsNullOrEmpty(ExitPool);
                    if (nextIndex >= 0 && !lastIsStation && !reqcloseup)
                    {
                        if (TrackCircuitSection.TrackCircuitList[nextIndex].CircuitType == TrackCircuitType.Junction)
                        {
                            float lengthCorrection = Math.Max(Convert.ToSingle(TrackCircuitSection.TrackCircuitList[nextIndex].Overlap), StandardOverlapM);
                            if (lastSection.Length - 2 * lengthCorrection < Length) // make sure train fits
                            {
                                lengthCorrection = Math.Max(0.0f, (lastSection.Length - Length) / 2);
                            }
                            lengthToGoM -= lengthCorrection; // correct for stopping position
                        }
                    }
                }
            }

            CreateTrainAction(TrainMaxSpeedMpS, 0.0f, lengthToGoM, null,
                    AiActionType.EndOfRoute);
            NextStopDistanceM = lengthToGoM;
        }

        //================================================================================================//
        /// <summary>
        /// Check if train is in Wait state
        /// Override from Train class
        /// </summary>
        /// <returns></returns>
        protected override bool InWaitState()
        {
            bool inWaitState = false;

            if (waitList != null && waitList.Count > 0 && waitList[0].WaitActive)
                inWaitState = true;

            if (waitAnyList != null)
            {
                foreach (KeyValuePair<int, List<WaitInfo>> actWInfo in waitAnyList)
                {
                    foreach (WaitInfo actWait in actWInfo.Value)
                    {
                        if (actWait.WaitActive)
                        {
                            inWaitState = true;
                            break;
                        }
                    }
                }
            }

            return (inWaitState);
        }

        //================================================================================================//
        /// <summary>
        /// Check if train has AnyWait condition at this location
        /// Override from Train class
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal override bool CheckAnyWaitCondition(int index)
        {
            if (waitAnyList != null && waitAnyList.TryGetValue(index, out List<WaitInfo> value))
            {
                foreach (WaitInfo reqWait in value)
                {
                    bool pathClear = CheckForRouteWait(reqWait);
                    return (!pathClear);
                }
            }
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Set unprocessed info for train timetable commands
        /// Info will be fully processed after all trains have been created - this is necessary as only then is all required info available
        /// StationStop info may be null if command is not linked to a station (for commands issued at #note line)
        /// </summary>

        public void ProcessTimetableStopCommands(TTTrainCommands thisCommand, int subrouteIndex, int sectionIndex, int stationIndex, int plattformReferenceID, TimetableInfo ttinfo)
        {

            StationStop thisStationStop = (StationStops.Count > 0 && stationIndex >= 0) ? StationStops[stationIndex] : null;

            switch (thisCommand.CommandToken.Trim())
            {
                // WAIT command
                case "wait":
                    if (thisCommand.CommandValues == null)
                    {
                        Trace.TraceInformation("Train : {0} : invalid wait command at {1}", Name, thisStationStop == null ? "note line" : thisStationStop.PlatformItem.Name);
                        break;
                    }

                    foreach (string reqReferenceTrain in thisCommand.CommandValues)
                    {
                        WaitInfo newWaitItem = new WaitInfo();
                        newWaitItem.WaitType = WaitInfoType.Wait;

                        if (sectionIndex < 0)
                        {
                            newWaitItem.StartSectionIndex = TCRoute.TCRouteSubpaths[subrouteIndex][0].TrackCircuitSection.Index;
                        }
                        else
                        {
                            newWaitItem.StartSectionIndex = sectionIndex;
                        }
                        newWaitItem.StartSubrouteIndex = subrouteIndex;

                        newWaitItem.ReferencedTrainName = reqReferenceTrain;

                        // check if name is full name, otherwise add timetable file info from this train
                        if (!newWaitItem.ReferencedTrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
                        {
                            int seppos = Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                            newWaitItem.ReferencedTrainName = $"{newWaitItem.ReferencedTrainName}:{Name[(seppos + 1)..]}";
                        }

                        // qualifiers : 
                        //  maxdelay (single value only)
                        //  owndelay (single value only)
                        //  notstarted (no value)
                        //  trigger (single value only)
                        //  endtrigger (single value only)
                        //  atstart (no value)

                        if (thisCommand.CommandQualifiers != null)
                        {
                            foreach (TTTrainCommands.TTTrainComQualifiers addQualifier in thisCommand.CommandQualifiers)
                            {
                                switch (addQualifier.QualifierName)
                                {
                                    case "maxdelay":
                                        if (int.TryParse(addQualifier.QualifierValues[0], out int maxDelayS))
                                        {
                                            newWaitItem.MaxDelayS = maxDelayS * 60; // defined in MINUTES!!
                                        }
                                        else
                                        {
                                            Trace.TraceInformation("Train {0} : invalid value in $wait command for {1} : {2}",
                                                Name, addQualifier.QualifierName, addQualifier.QualifierValues[0]);
                                        }
                                        break;
                                    case "notstarted":
                                        newWaitItem.NotStarted = true;
                                        break;
                                    case "atstart":
                                        newWaitItem.AtStart = true;
                                        break;
                                    case "owndelay":
                                        if (int.TryParse(addQualifier.QualifierValues[0], out int ownDelayS))
                                        {
                                            newWaitItem.OwnDelayS = ownDelayS * 60; // defined in MINUTES!!
                                        }
                                        else
                                        {
                                            Trace.TraceInformation("Train {0} : invalid value in $wait command for {1} : {2} \n",
                                                Name, addQualifier.QualifierName, addQualifier.QualifierValues[0]);
                                        }
                                        break;
                                    case "trigger":
                                        TimeSpan ttime;
                                        bool validTriggerTim = false;

                                        validTriggerTim = TimeSpan.TryParse(addQualifier.QualifierValues[0], out ttime);
                                        if (validTriggerTim)
                                        {
                                            newWaitItem.Waittrigger = Convert.ToInt32(ttime.TotalSeconds);
                                        }
                                        break;
                                    case "endtrigger":
                                        TimeSpan etime;
                                        bool validEndTime = false;

                                        validEndTime = TimeSpan.TryParse(addQualifier.QualifierValues[0], out etime);
                                        if (validEndTime)
                                        {
                                            newWaitItem.Waitendtrigger = Convert.ToInt32(etime.TotalSeconds);
                                        }
                                        break;

                                    default:
                                        if (thisStationStop == null)
                                        {
                                            Trace.TraceWarning("Invalid qualifier for WAIT command for train {0} in #note line : {1}",
                                                Name, addQualifier.QualifierName);
                                        }
                                        else
                                        {
                                            Trace.TraceWarning("Invalid qualifier for WAIT command for train {0} at station {1} : {2}",
                                                Name, thisStationStop.PlatformItem.Name, addQualifier.QualifierName);
                                        }
                                        break;
                                }
                            }
                        }

                        if (waitList == null)
                        {
                            waitList = new List<WaitInfo>();
                        }
                        waitList.Add(newWaitItem);
                    }
                    break;

                // FOLLOW command
                case "follow":
                    foreach (string reqReferenceTrain in thisCommand.CommandValues)
                    {
                        WaitInfo newWaitItem = new WaitInfo();
                        newWaitItem.WaitType = WaitInfoType.Follow;

                        if (sectionIndex < 0)
                        {
                            newWaitItem.StartSectionIndex = TCRoute.TCRouteSubpaths[subrouteIndex][0].TrackCircuitSection.Index;
                        }
                        else
                        {
                            newWaitItem.StartSectionIndex = sectionIndex;
                        }
                        newWaitItem.StartSubrouteIndex = subrouteIndex;

                        newWaitItem.ReferencedTrainName = reqReferenceTrain;

                        // check if name is full name, otherwise add timetable file info from this train
                        if (!newWaitItem.ReferencedTrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
                        {
                            int seppos = Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                            newWaitItem.ReferencedTrainName = $"{newWaitItem.ReferencedTrainName}:{Name[(seppos + 1)..]}";
                        }

                        // qualifiers : 
                        //  maxdelay (single value only)
                        //  owndelay (single value only)
                        //  notstarted (no value)
                        //  trigger (single value only)
                        //  endtrigger (single value only)

                        if (thisCommand.CommandQualifiers != null)
                        {
                            foreach (TTTrainCommands.TTTrainComQualifiers addQualifier in thisCommand.CommandQualifiers)
                            {
                                switch (addQualifier.QualifierName)
                                {
                                    case "maxdelay":
                                        if (int.TryParse(addQualifier.QualifierValues[0], out int maxDelayS))
                                        {
                                            newWaitItem.MaxDelayS = maxDelayS * 60;
                                        }
                                        else
                                        {
                                            Trace.TraceInformation("Train {0} : invalid value in $follow command for {1} : {2}",
                                                Name, addQualifier.QualifierName, addQualifier.QualifierValues[0]);
                                        }
                                        break;
                                    case "notstarted":
                                        newWaitItem.NotStarted = true;
                                        break;
                                    case "atStart":
                                        newWaitItem.AtStart = true;
                                        break;
                                    case "owndelay":
                                        if (int.TryParse(addQualifier.QualifierValues[0], out int ownDelayS))
                                        {
                                            newWaitItem.OwnDelayS = ownDelayS * 60;
                                        }
                                        else
                                        {
                                            Trace.TraceInformation("Train {0} : invalid value in $follow command for {1} : {2}",
                                                Name, addQualifier.QualifierName, addQualifier.QualifierValues[0]);
                                        }
                                        break;
                                    case "trigger":
                                        TimeSpan ttime;
                                        bool validTriggerTim = false;

                                        validTriggerTim = TimeSpan.TryParse(addQualifier.QualifierValues[0], out ttime);
                                        if (validTriggerTim)
                                        {
                                            newWaitItem.Waittrigger = Convert.ToInt32(ttime.TotalSeconds);
                                        }
                                        break;
                                    case "endtrigger":
                                        TimeSpan etime;
                                        bool validEndTime = false;

                                        validEndTime = TimeSpan.TryParse(addQualifier.QualifierValues[0], out etime);
                                        if (validEndTime)
                                        {
                                            newWaitItem.Waitendtrigger = Convert.ToInt32(etime.TotalSeconds);
                                        }
                                        break;

                                    default:
                                        Trace.TraceWarning("Invalid qualifier for FOLLOW command for train {0} at station {1} : {2}",
                                            Name, thisStationStop.PlatformItem.Name, addQualifier.QualifierName);
                                        break;
                                }
                            }
                        }

                        if (waitList == null)
                        {
                            waitList = new List<WaitInfo>();
                        }
                        waitList.Add(newWaitItem);
                    }
                    break;

                case "connect":
                    // only valid with section index

                    if (sectionIndex < 0 || stationIndex < 0)
                    {
                        Trace.TraceInformation("Invalid CONNECT command for train {0} : command must be linked to location", Name);
                    }
                    else
                    {
                        foreach (string reqReferenceTrain in thisCommand.CommandValues)
                        {
                            WaitInfo newWaitItem = new WaitInfo();
                            newWaitItem.WaitType = WaitInfoType.Connect;

                            newWaitItem.StartSectionIndex = sectionIndex;
                            newWaitItem.StartSubrouteIndex = subrouteIndex;
                            newWaitItem.StationIndex = stationIndex;

                            newWaitItem.ReferencedTrainName = reqReferenceTrain;

                            // check if name is full name, otherwise add timetable file info from this train
                            if (!newWaitItem.ReferencedTrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
                            {
                                int seppos = Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                                newWaitItem.ReferencedTrainName = $"{newWaitItem.ReferencedTrainName}:{Name[(seppos + 1)..]}";
                            }

                            // qualifiers : 
                            //  maxdelay (single value only)
                            //  hold (single value only)

                            if (thisCommand.CommandQualifiers != null)
                            {
                                foreach (TTTrainCommands.TTTrainComQualifiers addQualifier in thisCommand.CommandQualifiers)
                                {
                                    switch (addQualifier.QualifierName)
                                    {
                                        case "maxdelay":
                                            if (!int.TryParse(addQualifier.QualifierValues[0], out int maxDelayS))
                                            {
                                                newWaitItem.MaxDelayS = null;
                                                Trace.TraceInformation("Train {0} : invalid value in $connect command for {1} : {2}",
                                                    Name, addQualifier.QualifierName, addQualifier.QualifierValues[0]);
                                            }
                                            else
                                                newWaitItem.MaxDelayS = maxDelayS * 60; // defined in MINUTES!!
                                            break;
                                        case "hold":
                                            if (!int.TryParse(addQualifier.QualifierValues[0], out int holdTimeS))
                                            {
                                                newWaitItem.HoldTimeS = null;
                                                Trace.TraceInformation("Train {0} : invalid value in $connect command for {1} : {2}",
                                                    Name, addQualifier.QualifierName, addQualifier.QualifierValues[0]);
                                            }
                                            else
                                                newWaitItem.HoldTimeS = holdTimeS * 60; // defined in MINUTES!!
                                            break;

                                        default:
                                            Trace.TraceWarning("Invalid qualifier for CONNECT command for train {0} at station {1} : {2}",
                                                Name, thisStationStop.PlatformItem.Name, addQualifier.QualifierName);
                                            break;
                                    }
                                }
                            }

                            if (waitList == null)
                            {
                                waitList = new List<WaitInfo>();
                            }
                            waitList.Add(newWaitItem);
                        }
                    }
                    break;

                case "waitany":
                    foreach (string reqReferencePath in thisCommand.CommandValues)
                    {
                        WaitInfo newWaitItem = new WaitInfo();
                        newWaitItem.WaitType = WaitInfoType.WaitAny;
                        newWaitItem.WaitActive = false;

                        newWaitItem.PathDirection = PathCheckDirection.Same;
                        if (thisCommand.CommandQualifiers != null && thisCommand.CommandQualifiers.Count > 0)
                        {
                            TTTrainCommands.TTTrainComQualifiers thisQualifier = thisCommand.CommandQualifiers[0]; // takes only 1 qualifier
                            switch (thisQualifier.QualifierName)
                            {
                                case "both":
                                    newWaitItem.PathDirection = PathCheckDirection.Both;
                                    break;

                                case "opposite":
                                    newWaitItem.PathDirection = PathCheckDirection.Opposite;
                                    break;

                                default:
                                    Trace.TraceWarning("Invalid qualifier for WAITANY command for train {0} at station {1} : {2}",
                                        Name, thisStationStop.PlatformItem.Name, thisQualifier.QualifierName);
                                    break;
                            }
                        }

                        bool loadPathNoFailure;
                        AIPath fullpath = ttinfo.LoadPath(reqReferencePath, out loadPathNoFailure);

                        // create a copy of this train to process route
                        // copy is required as otherwise the original route would be lost

                        if (fullpath != null) // valid path
                        {
                            TrackCircuitRoutePath fullRoute = new TrackCircuitRoutePath(fullpath, (TrackDirection)(-2), 1, -1);
                            newWaitItem.CheckPath = new TrackCircuitPartialPathRoute(fullRoute.TCRouteSubpaths[0]);

                            // find first overlap section with train route
                            int overlapSection = -1;
                            int useSubpath = 0;

                            while (overlapSection < 0 && useSubpath <= TCRoute.TCRouteSubpaths.Count)
                            {
                                foreach (TrackCircuitRouteElement pathElement in newWaitItem.CheckPath)
                                {
                                    if (TCRoute.TCRouteSubpaths[useSubpath].GetRouteIndex(pathElement.TrackCircuitSection.Index, 0) > 0)
                                    {
                                        overlapSection = pathElement.TrackCircuitSection.Index;
                                        break;
                                    }
                                }

                                useSubpath++;
                            }

                            // if overlap found : insert in waiting list
                            if (overlapSection >= 0)
                            {
                                if (waitAnyList == null)
                                {
                                    waitAnyList = new Dictionary<int, List<WaitInfo>>();
                                }

                                if (waitAnyList.TryGetValue(overlapSection, out List<WaitInfo> waitList))
                                {
                                    waitList.Add(newWaitItem);
                                }
                                else
                                {
                                    waitList = [newWaitItem];
                                    waitAnyList.Add(overlapSection, waitList);
                                }
                            }
                        }
                    }
                    break;

                case "callon":
                    if (thisStationStop == null)
                    {
                        Trace.TraceInformation("Cannot set CALLON without station stop time : train " + Name + " ( " + Number + " )");
                    }
                    else
                    {
                        thisStationStop.CallOnAllowed = true;
                    }
                    break;

                case "hold":
                    if (thisStationStop == null)
                    {
                        Trace.TraceInformation("Cannot set HOLD without station stop time : train " + Name + " ( " + Number + " )");
                    }
                    else
                    {
                        thisStationStop.HoldSignal = thisStationStop.ExitSignal >= 0; // set holdstate only if exit signal is defined
                    }
                    break;

                case "nohold":
                    if (thisStationStop == null)
                    {
                        Trace.TraceInformation("Cannot set NOHOLD without station stop time : train " + Name + " ( " + Number + " )");
                    }
                    else
                    {
                        thisStationStop.HoldSignal = false;
                    }
                    break;

                case "forcehold":
                    if (thisStationStop != null)
                    {
                        // use platform signal
                        if (thisStationStop.ExitSignal >= 0)
                        {
                            thisStationStop.HoldSignal = true;
                        }
                        // use first signal in route
                        else
                        {
                            TrackCircuitPartialPathRoute usedRoute = TCRoute.TCRouteSubpaths[thisStationStop.SubrouteIndex];
                            int signalFound = -1;

                            TrackCircuitRouteElement routeElement = usedRoute[thisStationStop.RouteIndex];
                            float distanceToStationSignal = thisStationStop.PlatformItem.DistanceToSignals[(TrackDirection)routeElement.Direction];

                            for (int iRouteIndex = thisStationStop.RouteIndex; iRouteIndex <= usedRoute.Count - 1 && signalFound < 0; iRouteIndex++)
                            {
                                routeElement = usedRoute[iRouteIndex];
                                TrackCircuitSection routeSection = TrackCircuitSection.TrackCircuitList[routeElement.TrackCircuitSection.Index];

                                if (routeSection.EndSignals[(TrackDirection)routeElement.Direction] != null)
                                {
                                    signalFound = routeSection.EndSignals[(TrackDirection)routeElement.Direction].Index;
                                }
                                else
                                {
                                    distanceToStationSignal += routeSection.Length;
                                }
                            }

                            if (signalFound >= 0)
                            {
                                thisStationStop.ExitSignal = signalFound;
                                thisStationStop.HoldSignal = true;
                                HoldingSignals.Add(signalFound);

                                thisStationStop.StopOffset = Math.Min(thisStationStop.StopOffset, distanceToStationSignal + thisStationStop.PlatformItem.Length - 10.0f);
                            }
                        }
                    }
                    break;

                case "forcewait":
                    if (thisStationStop != null)
                    {
                        // if no platform signal, use first signal in route
                        if (thisStationStop.ExitSignal < 0)
                        {
                            TrackCircuitPartialPathRoute usedRoute = TCRoute.TCRouteSubpaths[thisStationStop.SubrouteIndex];
                            int signalFound = -1;

                            TrackCircuitRouteElement routeElement = usedRoute[thisStationStop.RouteIndex];
                            float distanceToStationSignal = thisStationStop.PlatformItem.DistanceToSignals[(TrackDirection)routeElement.Direction];

                            for (int iRouteIndex = thisStationStop.RouteIndex; iRouteIndex <= usedRoute.Count - 1 && signalFound < 0; iRouteIndex++)
                            {
                                routeElement = usedRoute[iRouteIndex];
                                TrackCircuitSection routeSection = TrackCircuitSection.TrackCircuitList[routeElement.TrackCircuitSection.Index];

                                if (routeSection.EndSignals[(TrackDirection)routeElement.Direction] != null)
                                {
                                    signalFound = routeSection.EndSignals[(TrackDirection)routeElement.Direction].Index;
                                }
                                else
                                {
                                    distanceToStationSignal += routeSection.Length;
                                }
                            }

                            if (signalFound >= 0)
                            {
                                thisStationStop.ExitSignal = signalFound;
                                thisStationStop.StopOffset = Math.Min(thisStationStop.StopOffset, distanceToStationSignal + thisStationStop.PlatformItem.Length - 10.0f);
                            }
                        }
                    }
                    break;

                case "nowaitsignal":
                    if (thisStationStop == null)
                    {
                        Trace.TraceInformation("Cannot set NOWAITSIGNAL without station stop time : train " + Name + " ( " + Number + " )");
                    }
                    else
                    {
                        thisStationStop.NoWaitSignal = true;
                    }
                    break;

                case "waitsignal":
                    if (thisStationStop == null)
                    {
                        Trace.TraceInformation("Cannot set WAITSIGNAL without station stop time : train " + Name + " ( " + Number + " )");
                    }
                    else
                    {
                        thisStationStop.NoWaitSignal = false;
                    }
                    break;

                case "noclaim":
                    if (thisStationStop == null)
                    {
                        Trace.TraceInformation("Cannot set NOCLAIM without station stop time : train " + Name + " ( " + Number + " )");
                    }
                    else
                    {
                        thisStationStop.NoClaimAllowed = true;
                    }
                    break;

                // no action for terminal (processed in create station stop)
                case "terminal":
                    break;

                // no action for closeupsignal (processed in create station stop)
                case "closeupsignal":
                    break;

                // no action for closeupsignal (processed in create station stop)
                case "closeup":
                    break;

                // no action for extendplatformtosignal (processed in create station stop)
                case "extendplatformtosignal":
                    break;

                // no action for restrictplatformtosignal (processed in create station stop)
                case "restrictplatformtosignal":
                    break;

                // no action for keepclear (processed in create station stop)
                case "keepclear":
                    break;

                // no action for endstop (processed in create station stop)
                case "endstop":
                    break;

                // no action for stoptime (processed in create station stop)
                case "stoptime":
                    break;

                case "detach":
                    // detach at station
                    if (thisStationStop != null)
                    {
                        DetachInfo thisDetach = new DetachInfo(this, thisCommand, false, true, false, thisStationStop.TrackCircuitSectionIndex, thisStationStop.ArrivalTime);
                        if (DetachDetails.TryGetValue(thisStationStop.PlatformReference, out List<DetachInfo> detachList))
                        {
                            detachList.Add(thisDetach);
                        }
                        else
                        {
                            detachList = [thisDetach];
                            DetachDetails.Add(thisStationStop.PlatformReference, detachList);
                        }
                    }
                    // detach at start
                    else
                    {
                        int startSection = TCRoute.TCRouteSubpaths[0][0].TrackCircuitSection.Index;
                        DetachInfo thisDetach = new DetachInfo(this, thisCommand, true, false, false, startSection, ActivateTime);
                        if (DetachDetails.TryGetValue(-1, out List<DetachInfo> detachList))
                        {
                            detachList.Add(thisDetach);
                        }
                        else
                        {
                            detachList = [thisDetach];
                            DetachDetails.Add(-1, detachList);
                        }
                    }
                    break;

                case "attach":
                    // attach at station
                    if (plattformReferenceID >= 0)
                    {
                        AttachDetails = new AttachInfo(plattformReferenceID, thisCommand, this);
                    }
                    break;

                case "pickup":
                    // pickup at station
                    if (plattformReferenceID >= 0)
                    {
                        PickUpInfo thisPickUp = new PickUpInfo(plattformReferenceID, thisCommand, this);
                        PickUpDetails.Add(thisPickUp);
                    }
                    break;

                case "transfer":
                    TransferInfo thisTransfer = new TransferInfo(plattformReferenceID, thisCommand, this);
                    if (plattformReferenceID >= 0)
                    {
                        if (!TransferStationDetails.TryAdd(plattformReferenceID, thisTransfer))
                        {
                            Trace.TraceInformation("Train {0} : transfer command : cannot define multiple transfer at a single stop", Name);
                        }
                        else
                        {
                            List<TransferInfo> thisTransferList = new List<TransferInfo>();
                        }
                    }
                    else
                    {
                        // for now, insert with train set to -1 - will be updated for proper crossreference later
                        if (TransferTrainDetails.TryGetValue(-1, out List<TransferInfo> value))
                        {
                            value.Add(thisTransfer);
                        }
                        else
                        {
                            List<TransferInfo> thisTransferList = [thisTransfer];
                            TransferTrainDetails.Add(-1, thisTransferList);
                        }
                    }
                    break;

                case "activate":
                    if (thisCommand.CommandValues == null || thisCommand.CommandValues.Count < 1)
                    {
                        Trace.TraceInformation("No train reference set for train activation, train {0}", Name);
                        break;
                    }

                    TriggerActivation thisTrigger = new TriggerActivation();

                    if (plattformReferenceID >= 0)
                    {
                        thisTrigger.PlatformId = plattformReferenceID;
                        thisTrigger.ActivationType = TriggerActivationType.StationStop;

                        if (thisCommand.CommandQualifiers != null && thisCommand.CommandQualifiers.Count > 0)
                        {
                            TTTrainCommands.TTTrainComQualifiers thisQualifier = thisCommand.CommandQualifiers[0]; // takes only 1 qualifier
                            if (thisQualifier.QualifierName.Equals("depart", StringComparison.OrdinalIgnoreCase))
                            {
                                thisTrigger.ActivationType = TriggerActivationType.StationDepart;
                            }
                        }
                    }
                    else
                    {
                        thisTrigger.ActivationType = TriggerActivationType.Start;
                    }

                    thisTrigger.ActivatedTrainName = thisCommand.CommandValues[0];
                    ActivatedTrainTriggers.Add(thisTrigger);

                    break;

                default:
                    Trace.TraceWarning("Invalid station stop command for train {0} : {1}", Name, thisCommand.CommandToken);
                    break;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Finalize the command information - process referenced train details
        /// </summary>

        public void FinalizeTimetableCommands()
        {
            // process all wait information
            if (waitList != null)
            {
                TTTrain otherTrain = null;
                List<WaitInfo> newWaitItems = new List<WaitInfo>();

                foreach (WaitInfo reqWait in waitList)
                {
                    switch (reqWait.WaitType)
                    {
                        // WAIT command
                        case WaitInfoType.Wait:
                            otherTrain = GetOtherTTTrainByName(reqWait.ReferencedTrainName);
                            if (otherTrain != null)
                            {
                                ProcessWaitRequest(reqWait, otherTrain, true, true, true, ref newWaitItems);
                            }
                            reqWait.WaitType = WaitInfoType.Invalid; // set to invalid as item is processed
                            break;

                        // FOLLOW command
                        case WaitInfoType.Follow:
                            otherTrain = GetOtherTTTrainByName(reqWait.ReferencedTrainName);
                            if (otherTrain != null)
                            {
                                ProcessWaitRequest(reqWait, otherTrain, true, false, false, ref newWaitItems);
                            }
                            reqWait.WaitType = WaitInfoType.Invalid; // set to invalid as item is processed
                            break;

                        // CONNECT command
                        case WaitInfoType.Connect:
                            otherTrain = GetOtherTTTrainByName(reqWait.ReferencedTrainName);
                            if (otherTrain != null)
                            {
                                ProcessConnectRequest(reqWait, otherTrain, ref newWaitItems);
                            }
                            reqWait.WaitType = WaitInfoType.Invalid; // set to invalid as item is processed
                            break;

                        default:
                            break;
                    }
                }

                // remove processed and invalid items
                for (int iWaitItem = waitList.Count - 1; iWaitItem >= 0; iWaitItem--)
                {
                    if (waitList[iWaitItem].WaitType == WaitInfoType.Invalid)
                    {
                        waitList.RemoveAt(iWaitItem);
                    }
                }

                // add new created items
                foreach (WaitInfo newWait in newWaitItems)
                {
                    waitList.Add(newWait);
                }

                // sort list - list is sorted on subpath and route index
                waitList.Sort();
            }
        }

        //================================================================================================//
        /// <summary>
        /// Process wait request for Timetable waits
        /// </summary>

        public void ProcessWaitRequest(WaitInfo reqWait, TTTrain otherTrain, bool allowSameDirection, bool allowOppositeDirection, bool singleWait, ref List<WaitInfo> newWaitItems)
        {
            // find first common section to determine train directions
            int otherRouteIndex = -1;
            int thisSubpath = reqWait.StartSubrouteIndex;
            int thisIndex = TCRoute.TCRouteSubpaths[thisSubpath].GetRouteIndex(reqWait.StartSectionIndex, 0);
            int otherSubpath = 0;

            int startSectionIndex = TCRoute.TCRouteSubpaths[thisSubpath][thisIndex].TrackCircuitSection.Index;

            bool allSubpathsProcessed = false;
            bool sameDirection = false;
            bool validWait = true;  // presume valid wait

            // set actual start

            TrackCircuitRouteElement thisTrainElement = null;
            TrackCircuitRouteElement otherTrainElement = null;

            int thisTrainStartSubpathIndex = reqWait.StartSubrouteIndex;
            int thisTrainStartRouteIndex = TCRoute.TCRouteSubpaths[thisTrainStartSubpathIndex].GetRouteIndex(reqWait.StartSectionIndex, 0);

            // loop while no section found and further subpaths available

            while (!allSubpathsProcessed)
            {
                otherRouteIndex = -1;

                while (otherRouteIndex < 0)
                {
                    int sectionIndex = TCRoute.TCRouteSubpaths[thisSubpath][thisIndex].TrackCircuitSection.Index;
                    otherRouteIndex = otherTrain.TCRoute.TCRouteSubpaths[otherSubpath].GetRouteIndex(sectionIndex, 0);

                    if (otherRouteIndex < 0 && otherSubpath < otherTrain.TCRoute.TCRouteSubpaths.Count - 1)
                    {
                        otherSubpath++;
                    }
                    else if (otherRouteIndex < 0 && thisIndex < TCRoute.TCRouteSubpaths[thisSubpath].Count - 1)
                    {
                        thisIndex++;
                        otherSubpath = 0; // reset other train subpath
                    }
                    else if (otherRouteIndex < 0 && thisSubpath < TCRoute.TCRouteSubpaths.Count - 1)
                    {
                        thisSubpath++;
                        thisIndex = 0;
                        otherSubpath = 0; // reset other train subpath
                    }
                    else if (otherRouteIndex < 0)
                    {
                        validWait = false;
                        break; // no common section found
                    }
                }

                // if valid wait but wait is in next subpath, use start section of next subpath

                if (validWait && thisTrainStartSubpathIndex < thisSubpath)
                {
                    thisTrainStartSubpathIndex = thisSubpath;
                    thisTrainStartRouteIndex = 0;
                }

                // check directions
                if (validWait)
                {
                    thisTrainElement = TCRoute.TCRouteSubpaths[thisSubpath][thisIndex];
                    otherTrainElement = otherTrain.TCRoute.TCRouteSubpaths[otherSubpath][otherRouteIndex];

                    sameDirection = thisTrainElement.Direction == otherTrainElement.Direction;

                    validWait = sameDirection ? allowSameDirection : allowOppositeDirection;
                }

                // if original start section index is also first common index, search for first not common section
                if (validWait && startSectionIndex == otherTrainElement.TrackCircuitSection.Index)
                {
                    int notCommonSectionRouteIndex = -1;

                    if (sameDirection)
                    {
                        notCommonSectionRouteIndex =
                                FindCommonSectionEnd(TCRoute.TCRouteSubpaths[thisSubpath], thisIndex,
                                otherTrain.TCRoute.TCRouteSubpaths[otherSubpath], otherRouteIndex);
                    }
                    else
                    {
                        notCommonSectionRouteIndex =
                                FindCommonSectionEndReverse(TCRoute.TCRouteSubpaths[thisSubpath], thisIndex,
                                otherTrain.TCRoute.TCRouteSubpaths[otherSubpath], otherRouteIndex);
                    }

                    // check on found not-common section - start wait here if atstart is set, otherwise start wait at first not-common section
                    int notCommonSectionIndex = otherTrain.TCRoute.TCRouteSubpaths[otherSubpath][notCommonSectionRouteIndex].TrackCircuitSection.Index;
                    int lastIndex = TCRoute.TCRouteSubpaths[thisTrainStartSubpathIndex].GetRouteIndex(notCommonSectionIndex, 0);

                    bool atStart = reqWait.AtStart.HasValue ? reqWait.AtStart.Value : false;

                    if (lastIndex < TCRoute.TCRouteSubpaths[thisTrainStartSubpathIndex].Count - 1) // not last entry
                    {
                        // if opposite direction, use next section as start for common section search
                        // if same direction and atStart not set also use next section as start for common section search
                        // if same direction and atStart is set use first section as start for common section search as train is to wait in this section
                        lastIndex++;
                        if (!sameDirection || !atStart)
                        {
                            thisTrainStartRouteIndex = lastIndex;
                        }
                        // valid wait, so set all subpaths processed
                        allSubpathsProcessed = true;
                    }
                    else
                    {
                        // full common route but further subpath available - try next subpath
                        if (otherSubpath < otherTrain.TCRoute.TCRouteSubpaths.Count - 1)
                        {
                            otherSubpath++;
                        }
                        else
                        {
                            validWait = false; // full common route - no waiting point possible
                            allSubpathsProcessed = true;
                        }
                    }
                }
                else
                // no valid wait or common section found
                {
                    allSubpathsProcessed = true;
                }
            }

            if (!validWait)
                return;

            // if in same direction, start at beginning and move downward
            // if in opposite direction, start at end (of found subpath!) and move upward

            int startSubpath = sameDirection ? 0 : otherSubpath;
            int endSubpath = sameDirection ? otherSubpath : 0;
            int startIndex = sameDirection ? 0 : otherTrain.TCRoute.TCRouteSubpaths[startSubpath].Count - 1;
            int endIndex = sameDirection ? otherTrain.TCRoute.TCRouteSubpaths[startSubpath].Count - 1 : 0;
            int increment = sameDirection ? 1 : -1;

            // if first section is common, first search for first non common section

            bool allCommonFound = false;

            // loop through all possible waiting points
            while (!allCommonFound)
            {
                int[,] sectionfound = FindCommonSectionStart(thisTrainStartSubpathIndex, thisTrainStartRouteIndex, otherTrain.TCRoute,
                    startSubpath, startIndex, endSubpath, endIndex, increment);

                // no common section found
                if (sectionfound[0, 0] < 0)
                {
                    allCommonFound = true;
                }
                else
                {
                    WaitInfo newItem = new WaitInfo();
                    newItem.WaitActive = false;
                    newItem.WaitType = reqWait.WaitType;
                    newItem.ActiveSubrouteIndex = sectionfound[0, 0];
                    newItem.ActiveRouteIndex = sectionfound[0, 1];
                    newItem.ActiveSectionIndex = TCRoute.TCRouteSubpaths[newItem.ActiveSubrouteIndex][newItem.ActiveRouteIndex].TrackCircuitSection.Index;

                    newItem.WaitTrainNumber = otherTrain.OrgAINumber;
                    newItem.WaitTrainSubpathIndex = sectionfound[1, 0];
                    newItem.WaitTrainRouteIndex = sectionfound[1, 1];
                    newItem.MaxDelayS = reqWait.MaxDelayS;
                    newItem.OwnDelayS = reqWait.OwnDelayS;
                    newItem.NotStarted = reqWait.NotStarted;
                    newItem.AtStart = reqWait.AtStart;
                    newItem.Waittrigger = reqWait.Waittrigger;
                    newItem.Waitendtrigger = reqWait.Waitendtrigger;

                    newWaitItems.Add(newItem);

                    int endSection = -1;

                    if (singleWait)
                    {
                        allCommonFound = true;
                        break;
                    }
                    else if (sameDirection)
                    {
                        endSection = FindCommonSectionEnd(TCRoute.TCRouteSubpaths[newItem.ActiveSubrouteIndex], newItem.ActiveRouteIndex,
                            otherTrain.TCRoute.TCRouteSubpaths[newItem.WaitTrainSubpathIndex], newItem.WaitTrainRouteIndex);
                    }
                    else
                    {
                        endSection = FindCommonSectionEndReverse(TCRoute.TCRouteSubpaths[newItem.ActiveSubrouteIndex], newItem.ActiveRouteIndex,
                            otherTrain.TCRoute.TCRouteSubpaths[newItem.WaitTrainSubpathIndex], newItem.WaitTrainRouteIndex);
                    }

                    // last common section
                    int lastSectionIndex = otherTrain.TCRoute.TCRouteSubpaths[newItem.WaitTrainSubpathIndex][endSection].TrackCircuitSection.Index;
                    thisTrainStartRouteIndex = TCRoute.TCRouteSubpaths[thisTrainStartSubpathIndex].GetRouteIndex(lastSectionIndex, thisTrainStartRouteIndex);
                    if (thisTrainStartRouteIndex < TCRoute.TCRouteSubpaths[thisTrainStartSubpathIndex].Count - 1)
                    {
                        thisTrainStartRouteIndex++;  // first not-common section
                    }
                    // end of subpath - shift to next subpath if available
                    else
                    {
                        if (thisTrainStartSubpathIndex < endSubpath)
                        {
                            thisTrainStartSubpathIndex++;
                            thisTrainStartRouteIndex = 0;
                        }
                        else
                        {
                            allCommonFound = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find start of common section of two trains
        /// May check through all subpaths for other train but only through start subpath for this train
        /// 
        /// Return value indices :
        ///   [0, 0] = own train subpath index
        ///   [0, 1] = own train route index
        ///   [1, 0] = other train subpath index
        ///   [1, 1] = other train route index
        /// </summary>
        /// <param name="thisTrainStartSubpathIndex"></param>
        /// <param name="thisTrainStartRouteIndex"></param>
        /// <param name="otherTrainRoute"></param>
        /// <param name="startSubpath"></param>
        /// <param name="startIndex"></param>
        /// <param name="endSubpath"></param>
        /// <param name="endIndex"></param>
        /// <param name="increment"></param>
        /// <param name="sameDirection"></param>
        /// <param name="oppositeDirection"></param>
        /// <param name="foundSameDirection"></param>
        /// <returns></returns>

        private int[,] FindCommonSectionStart(int thisTrainStartSubpathIndex, int thisTrainStartRouteIndex, TrackCircuitRoutePath otherTrainRoute,
                int startSubpath, int startIndex, int endSubpath, int endIndex, int increment)
        {
            // preset all loop data
            int thisSubpathIndex = thisTrainStartSubpathIndex;
            int thisRouteIndex = thisTrainStartRouteIndex;
            TrackCircuitPartialPathRoute thisRoute = TCRoute.TCRouteSubpaths[thisSubpathIndex];
            int thisRouteEndIndex = thisRoute.Count - 1;

            int otherSubpathIndex = startSubpath;
            int otherRouteIndex = startIndex;
            TrackCircuitPartialPathRoute otherRoute = otherTrainRoute.TCRouteSubpaths[otherSubpathIndex];
            int otherSubpathEndIndex = endSubpath;
            int otherRouteEndIndex = endIndex;

            bool thisEndOfRoute = false;
            bool otherEndOfRoute = false;
            bool commonSectionFound = false;

            // preset result
            int[,] sectionFound = new int[2, 2] { { -1, -1 }, { -1, -1 } };

            // convert other subpath to dictionary for quick reference
            TrackCircuitPartialPathRoute partRoute;
            if (otherRouteIndex < otherRouteEndIndex)
            {
                partRoute = new TrackCircuitPartialPathRoute(otherRoute, otherRouteIndex, otherRouteEndIndex);
            }
            else
            {
                partRoute = new TrackCircuitPartialPathRoute(otherRoute, otherRouteEndIndex, otherRouteIndex);
            }

            // loop until common section is found or route is ended
            while (!commonSectionFound && !otherEndOfRoute)
            {
                while (!thisEndOfRoute)
                {
                    if (partRoute.ContainsSection(TCRoute.TCRouteSubpaths[thisSubpathIndex][thisRouteIndex]))
                    {
                        commonSectionFound = true;
                        sectionFound[0, 0] = thisSubpathIndex;
                        sectionFound[0, 1] = thisRouteIndex;
                        sectionFound[1, 0] = otherSubpathIndex;
                        sectionFound[1, 1] = otherRoute.GetRouteIndex(TCRoute.TCRouteSubpaths[thisSubpathIndex][thisRouteIndex].TrackCircuitSection.Index, 0);
                        break;
                    }

                    // move to next section for this train
                    thisRouteIndex++;
                    if (thisRouteIndex > thisRouteEndIndex)
                    {
                        thisEndOfRoute = true;
                    }
                }

                // move to next subpath for other train
                // move to next subpath if end of subpath reached
                if (increment > 0)
                {
                    otherSubpathIndex++;
                    if (otherSubpathIndex > otherSubpathEndIndex)
                    {
                        otherEndOfRoute = true;
                    }
                    else
                    {
                        otherRoute = otherTrainRoute.TCRouteSubpaths[otherSubpathIndex];
                    }
                }
                else
                {
                    otherSubpathIndex--;
                    if (otherSubpathIndex < otherSubpathEndIndex)
                    {
                        otherEndOfRoute = true;
                    }
                }

                // reset to start for this train
                thisRouteIndex = thisTrainStartRouteIndex;
                thisEndOfRoute = false;
            }

            return (sectionFound);
        }

        /// <summary>
        /// Find end of common section searching through both routes in forward direction
        /// </summary>
        /// <param name="thisRoute"></param>
        /// <param name="thisRouteIndex"></param>
        /// <param name="otherRoute"></param>
        /// <param name="otherRouteIndex"></param>
        /// <returns></returns>
        private int FindCommonSectionEnd(TrackCircuitPartialPathRoute thisRoute, int thisRouteIndex, TrackCircuitPartialPathRoute otherRoute, int otherRouteIndex)
        {
            int lastIndex = otherRouteIndex;
            int thisIndex = thisRouteIndex;
            int otherIndex = otherRouteIndex;
            int thisSectionIndex = thisRoute[thisIndex].TrackCircuitSection.Index;
            int otherSectionIndex = otherRoute[otherIndex].TrackCircuitSection.Index;

            while (thisSectionIndex == otherSectionIndex)
            {
                lastIndex = otherIndex;
                thisIndex++;
                otherIndex++;

                if (thisIndex >= thisRoute.Count || otherIndex >= otherRoute.Count)
                {
                    break;
                }
                else
                {
                    thisSectionIndex = thisRoute[thisIndex].TrackCircuitSection.Index;
                    otherSectionIndex = otherRoute[otherIndex].TrackCircuitSection.Index;
                }
            }

            return (lastIndex);
        }

        /// <summary>
        /// Find end of common section searching through own train forward but through other train backward
        /// </summary>
        /// <param name="thisRoute"></param>
        /// <param name="thisRouteIndex"></param>
        /// <param name="otherRoute"></param>
        /// <param name="otherRouteIndex"></param>
        /// <returns></returns>
        private int FindCommonSectionEndReverse(TrackCircuitPartialPathRoute thisRoute, int thisRouteIndex, TrackCircuitPartialPathRoute otherRoute, int otherRouteIndex)
        {
            int lastIndex = otherRouteIndex;
            int thisIndex = thisRouteIndex;
            int otherIndex = otherRouteIndex;
            int thisSectionIndex = thisRoute[thisIndex].TrackCircuitSection.Index;
            int otherSectionIndex = otherRoute[otherIndex].TrackCircuitSection.Index;

            while (thisSectionIndex == otherSectionIndex)
            {
                lastIndex = otherIndex;
                thisIndex++;
                otherIndex--;
                if (thisIndex >= thisRoute.Count || otherIndex < 0)
                {
                    break;
                }
                else
                {
                    thisSectionIndex = thisRoute[thisIndex].TrackCircuitSection.Index;
                    otherSectionIndex = otherRoute[otherIndex].TrackCircuitSection.Index;
                }
            }

            return (lastIndex);
        }

        //================================================================================================//
        /// <summary>
        /// Process Connect Request : process details of connect command
        /// </summary>
        /// <param name="reqWait"></param>
        /// <param name="otherTrain"></param>
        /// <param name="allowSameDirection"></param>
        /// <param name="allowOppositeDirection"></param>
        /// <param name="singleWait"></param>
        /// <param name="newWaitItems"></param>
        private void ProcessConnectRequest(WaitInfo reqWait, TTTrain otherTrain, ref List<WaitInfo> newWaitItems)
        {
            // find station reference
            StationStop stopStation = StationStops[reqWait.StationIndex];
            int otherStationStopIndex = -1;

            for (int iStation = 0; iStation <= otherTrain.StationStops.Count - 1; iStation++)
            {
                if (string.Equals(stopStation.PlatformItem.Name, otherTrain.StationStops[iStation].PlatformItem.Name, StringComparison.OrdinalIgnoreCase))
                {
                    otherStationStopIndex = iStation;
                    break;
                }
            }

            if (otherStationStopIndex >= 0) // if other stop is found
            {
                WaitInfo newWait = reqWait.CreateCopy();
                newWait.WaitTrainNumber = otherTrain.OrgAINumber;
                StationStop otherTrainStationStop = otherTrain.StationStops[otherStationStopIndex];
                otherTrainStationStop.EnsureListsExists();
                otherTrainStationStop.ConnectionsWaiting.Add(Number);
                newWait.WaitTrainSubpathIndex = otherTrainStationStop.SubrouteIndex;
                newWait.StartSectionIndex = otherTrainStationStop.TrackCircuitSectionIndex;

                newWait.ActiveSubrouteIndex = reqWait.StartSubrouteIndex;
                newWait.ActiveSectionIndex = reqWait.StartSectionIndex;

                stopStation.ConnectionsAwaited.Add(newWait.WaitTrainNumber, -1);
                stopStation.ConnectionDetails.Add(newWait.WaitTrainNumber, newWait);

                newWaitItems.Add(newWait);

            }
        }

        //================================================================================================//
        /// <summary>
        /// Check for active wait condition for this section
        /// </summary>
        /// <param name="trackSectionIndex"></param>
        /// <returns></returns>
        internal override bool CheckWaitCondition(int trackSectionIndex)
        {
            // no waits defined
            if (waitList == null || waitList.Count <= 0)
            {
                return (false);
            }

            bool waitState = false;

            // check if first wait is this section

            int processedWait = 0;
            WaitInfo firstWait = waitList[processedWait];

            // if first wait is connect : no normal waits or follows to process
            if (firstWait.WaitType == WaitInfoType.Connect)
            {
                return (false);
            }

            while (firstWait.ActiveSubrouteIndex == TCRoute.ActiveSubPath && firstWait.ActiveSectionIndex == trackSectionIndex)
            {
                switch (firstWait.WaitType)
                {
                    case WaitInfoType.Wait:
                    case WaitInfoType.Follow:
                        waitState = CheckForSingleTrainWait(firstWait);
                        break;

                    default:
                        break;
                }

                // if not awaited, check for further waits
                // wait list may have changed if first item is no longer valid
                if (!waitState)
                {
                    if (processedWait > waitList.Count - 1)
                    {
                        break; // no more waits to check
                    }
                    else if (firstWait == waitList[processedWait])  // wait was maintained
                    {
                        processedWait++;
                    }

                    if (waitList.Count > processedWait)
                    {
                        firstWait = waitList[processedWait];
                    }
                    else
                    {
                        break; // no more waits to check
                    }
                }
                else
                {
                    break; // no more waits to check
                }
            }

            return (waitState);
        }

        //================================================================================================//
        /// <summary>
        /// Check for active wait condition for this section
        /// </summary>
        /// <param name="trackSectionIndex"></param>
        /// <returns></returns>
        internal override bool VerifyDeadlock(List<int> deadlockReferences)
        {
            bool attachTrainAhead = false;
            bool otherTrainAhead = false;
            List<int> possibleAttachTrains = new List<int>();

            if (AttachDetails != null)
            {
                if (deadlockReferences.Contains(AttachDetails.TrainNumber))
                {
                    possibleAttachTrains.Add(AttachDetails.TrainNumber);
                }
            }

            if (TransferStationDetails != null && TransferStationDetails.Count > 0)
            {
                foreach (KeyValuePair<int, TransferInfo> thisTransfer in TransferStationDetails)
                {
                    if (deadlockReferences.Contains(thisTransfer.Value.TrainNumber))
                    {
                        possibleAttachTrains.Add(thisTransfer.Value.TrainNumber);
                    }
                }
            }

            if (TransferTrainDetails != null && TransferTrainDetails.Count > 0)
            {
                foreach (KeyValuePair<int, List<TransferInfo>> thisTransferList in TransferTrainDetails)
                {
                    foreach (TransferInfo thisTransfer in thisTransferList.Value)
                    {
                        if (deadlockReferences.Contains(thisTransfer.TrainNumber))
                        {
                            possibleAttachTrains.Add(thisTransfer.TrainNumber);
                        }
                    }
                }
            }

            // check if any possible attach trains ahead in deadlock references
            if (possibleAttachTrains.Count > 0)
            {
                // test if required train is first train ahead
                int presentSectionListIndex = PresentPosition[Direction.Forward].RouteListIndex;

                for (int iSection = presentSectionListIndex + 1; iSection < ValidRoutes[Direction.Forward].Count && !attachTrainAhead && !otherTrainAhead; iSection++)
                {
                    TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][iSection].TrackCircuitSection;
                    List<TrainRouted> occupyingTrains = thisSection.CircuitState.TrainsOccupying();
                    foreach (TrainRouted nextTrain in occupyingTrains)
                    {
                        if (possibleAttachTrains.Contains(nextTrain.Train.Number))
                        {
                            attachTrainAhead = true;
                        }
                        else
                        {
                            otherTrainAhead = true;
                        }
                    }
                }
            }

            return (!attachTrainAhead);
        }

        //================================================================================================//
        /// <summary>
        /// TrainGetSectionStateClearNode
        /// Override method from train
        /// </summary>

        internal override bool TrainGetSectionStateClearNode(TrackDirection elementDirection, TrackCircuitPartialPathRoute routePart, TrackCircuitSection thisSection)
        {
            return (thisSection.GetSectionState(RoutedForward, elementDirection, InternalBlockstate.Reserved, routePart, -1) <= InternalBlockstate.OccupiedSameDirection);
        }

        //================================================================================================//
        /// <summary>
        /// Check for actual wait condition (for single train wait - $wait, $follow or $forcewait commands
        /// </summary>
        /// <param name="reqWait"></param>
        /// <returns></returns>

        public bool CheckForSingleTrainWait(WaitInfo reqWait)
        {
            bool waitState = false;

            // get other train
            TTTrain otherTrain = GetOtherTTTrainByNumber(reqWait.WaitTrainNumber);

            // get clock time - for AI use AI clock as simulator clock is not valid during pre-process
            double presentTime = simulator.ClockTime;
            if (TrainType == TrainType.Ai)
            {
                AITrain aitrain = this as AITrain;
                presentTime = aitrain.AI.ClockTime;
            }

            if (reqWait.Waittrigger.HasValue)
            {
                if (reqWait.Waittrigger.Value < Convert.ToInt32(presentTime))
                {
                    return (waitState); // exit as wait must be retained
                }
            }

            // check if end trigger time passed
            if (reqWait.Waitendtrigger.HasValue)
            {
                if (reqWait.Waitendtrigger.Value < Convert.ToInt32(presentTime))
                {
                    otherTrain = null;          // reset other train to remove wait
                    reqWait.NotStarted = false; // ensure wait is not triggered accidentally
                }
            }

            // check on own delay condition
            bool owndelayexceeded = true;  // default is no own delay
            if (reqWait.OwnDelayS.HasValue && Delay.HasValue)
            {
                float ownDelayS = (float)Delay.Value.TotalSeconds;
                owndelayexceeded = ownDelayS > reqWait.OwnDelayS.Value;
            }

            // other train does exist or wait is cancelled
            if (otherTrain != null)
            {
                // other train in correct subpath
                // check if trigger time passed

                if (otherTrain.TCRoute.ActiveSubPath == reqWait.WaitTrainSubpathIndex)
                {
                    // check if section on train route and if so, if end of train is beyond this section
                    // check only for forward path - train must have passed section in 'normal' mode
                    if (otherTrain.ValidRoutes[Direction.Forward] != null)
                    {
                        int waitTrainRouteIndex = otherTrain.ValidRoutes[Direction.Forward].GetRouteIndex(reqWait.ActiveSectionIndex, 0);

                        if (waitTrainRouteIndex >= 0)
                        {
                            if (otherTrain.PresentPosition[Direction.Backward].RouteListIndex < waitTrainRouteIndex) // train is not yet passed this section
                            {
                                float? totalDelayS = null;
                                float? ownDelayS = null;

                                // determine own delay
                                if (reqWait.OwnDelayS.HasValue)
                                {
                                    if (owndelayexceeded)
                                    {
                                        ownDelayS = (float)Delay.Value.TotalSeconds;

                                        if (otherTrain.Delay.HasValue)
                                        {
                                            ownDelayS -= (float)otherTrain.Delay.Value.TotalSeconds;
                                        }

                                        if (ownDelayS.Value > reqWait.OwnDelayS.Value)
                                        {
                                            waitState = true;
                                            reqWait.WaitActive = true;
                                        }
                                    }
                                }

                                // determine other train delay
                                else
                                {
                                    if (reqWait.MaxDelayS.HasValue && otherTrain.Delay.HasValue)
                                    {
                                        totalDelayS = reqWait.MaxDelayS.Value;
                                        totalDelayS += Delay.HasValue ? (float)Delay.Value.TotalSeconds : 0f;     // add own delay if set
                                    }

                                    if (!totalDelayS.HasValue || (float)otherTrain.Delay.Value.TotalSeconds < totalDelayS)           // train is not too much delayed
                                    {
                                        waitState = true;
                                        reqWait.WaitActive = true;
                                    }
                                }
                            }
                        }
                    }
                }

                // if other train not in this subpath but notstarted is set, wait is valid (except when conditioned by own delay)
                else if (otherTrain.TCRoute.ActiveSubPath < reqWait.WaitTrainSubpathIndex && reqWait.NotStarted.HasValue && owndelayexceeded)
                {
                    waitState = true;
                    reqWait.WaitActive = true;
                }
            }

            // check if waiting is also required if train not yet started
            else if (reqWait.NotStarted.HasValue && owndelayexceeded)
            {
                if (CheckTTTrainNotStartedByNumber(reqWait.WaitTrainNumber))
                {
                    waitState = true;
                    reqWait.WaitActive = true;
                }
            }

            if (!waitState) // wait is no longer valid
            {
                waitList.RemoveAt(0); // remove this wait
            }

            return (waitState);
        }

        //================================================================================================//
        /// <summary>
        /// Check for route wait state (for $anywait command)
        /// </summary>
        /// <param name="reqWait"></param>
        /// <returns></returns>
        public bool CheckForRouteWait(WaitInfo reqWait)
        {
            bool pathClear = false;

            if (reqWait.PathDirection == PathCheckDirection.Same || reqWait.PathDirection == PathCheckDirection.Both)
            {
                pathClear = CheckRouteWait(reqWait.CheckPath, true);
                if (!pathClear)
                {
                    reqWait.WaitActive = true;
                    return (pathClear);  // no need to check opposite direction if allready blocked
                }
            }

            if (reqWait.PathDirection == PathCheckDirection.Opposite || reqWait.PathDirection == PathCheckDirection.Both)
            {
                pathClear = CheckRouteWait(reqWait.CheckPath, false);
            }

            reqWait.WaitActive = !pathClear;
            return (pathClear);
        }

        //================================================================================================//
        /// <summary>
        /// Check block state for route wait request
        /// </summary>
        /// <param name="thisRoute"></param>
        /// <param name="sameDirection"></param>
        /// <returns></returns>
        private bool CheckRouteWait(TrackCircuitPartialPathRoute thisRoute, bool sameDirection)
        {
            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // loop through all sections in route list

            TrackCircuitRouteElement lastElement = null;

            foreach (TrackCircuitRouteElement thisElement in thisRoute)
            {
                lastElement = thisElement;
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                TrackDirection direction = sameDirection ? thisElement.Direction : thisElement.Direction.Reverse();

                blockstate = thisSection.GetSectionState(RoutedForward, direction, blockstate, thisRoute, -1);
                if (blockstate > InternalBlockstate.Reservable)
                    break;     // exit on first none-available section
            }

            return (blockstate < InternalBlockstate.OccupiedSameDirection);
        }

        //================================================================================================//
        /// <summary>
        /// Check for any active waits in indicated path
        /// </summary>

        internal override bool HasActiveWait(int startSectionIndex, int endSectionIndex)
        {
            bool returnValue = false;

            int startRouteIndex = ValidRoutes[Direction.Forward].GetRouteIndex(startSectionIndex, PresentPosition[Direction.Forward].RouteListIndex);
            int endRouteIndex = ValidRoutes[Direction.Forward].GetRouteIndex(endSectionIndex, startRouteIndex);

            if (startRouteIndex < 0 || endRouteIndex < 0)
            {
                return (returnValue);
            }

            // check for any wait in indicated route section
            for (int iRouteIndex = startRouteIndex; iRouteIndex <= endRouteIndex; iRouteIndex++)
            {
                int sectionIndex = ValidRoutes[Direction.Forward][iRouteIndex].TrackCircuitSection.Index;
                if (waitList != null && waitList.Count > 0)
                {
                    if (CheckWaitCondition(sectionIndex))
                    {
                        returnValue = true;
                    }
                }

                if (waitAnyList != null && waitAnyList.Count > 0)
                {
                    if (waitAnyList.TryGetValue(sectionIndex, out List<WaitInfo> value))
                    {
                        foreach (WaitInfo reqWait in value)
                        {
                            if (CheckForRouteWait(reqWait))
                            {
                                returnValue = true;
                            }
                        }
                    }
                }
            }

            return (returnValue);
        }

        //================================================================================================//
        /// <summary>
        /// Process end of path 
        /// returns :
        /// [0] : true : end of route, false : not end of route
        /// [1] : true : train still exists, false : train is removed and no longer exists
        /// 
        /// Override from AITrain class
        /// <\summary>

        public override bool[] ProcessEndOfPath(int presentTime, bool checkLoop = true)
        {
            bool[] returnValue = new bool[2] { false, true };


            // if train not on route in can't be at end
            if (PresentPosition[0].RouteListIndex < 0)
            {
                return (returnValue);
            }

            TrackDirection directionNow = ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].Direction;
            int positionNow = ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].TrackCircuitSection.Index;

            (bool endOfRoute, bool otherRouteAvailable) = UpdateRouteActions(0, checkLoop);

            if (!endOfRoute)
                return (returnValue);   // not at end and not to attach to anything

            returnValue[0] = true; // end of path reached
            if (otherRouteAvailable)   // next route available
            {
                if (positionNow == PresentPosition[Direction.Forward].TrackCircuitSectionIndex && directionNow != PresentPosition[Direction.Forward].Direction)
                {
                    ReverseFormation(TrainType == TrainType.Player);
                }
                else if (positionNow == PresentPosition[Direction.Backward].TrackCircuitSectionIndex && directionNow != PresentPosition[Direction.Backward].Direction)
                {
                    ReverseFormation(TrainType == TrainType.Player);
                }

                // check if next station was on previous subpath - if so, move to this subpath

                if (StationStops.Count > 0)
                {
                    StationStop thisStation = StationStops[0];

                    if (thisStation.Passed)
                    {
                        StationStops.RemoveAt(0);
                    }
                    // if station was in previous path, checked if passed
                    else if (thisStation.SubrouteIndex < TCRoute.ActiveSubPath)
                    {
                        int routeIndex = ValidRoutes[Direction.Forward].GetRouteIndex(thisStation.TrackCircuitSectionIndex, 0);
                        if (routeIndex < 0 || PresentPosition[Direction.Forward].RouteListIndex > routeIndex) // station no longer on route or train beyond station
                        {
                            if (thisStation.ExitSignal >= 0 && thisStation.HoldSignal && HoldingSignals.Contains(thisStation.ExitSignal))
                            {
                                HoldingSignals.Remove(thisStation.ExitSignal);
                            }
                            StationStops.RemoveAt(0);
                        }
                        // if in station set correct subroute and route indices
                        else if (PresentPosition[Direction.Forward].RouteListIndex == routeIndex)
                        {
                            thisStation.SubrouteIndex = TCRoute.ActiveSubPath;
                            thisStation.RouteIndex = routeIndex;

                            AtStation = true;
                            MovementState = AiMovementState.StationStop;
                        }
                    }
                }

                // reset to node control, also reset required actions

                SwitchToNodeControl(-1);
                ResetActions(true);
            }
            else
            {
                ProcessEndOfPathReached(ref returnValue, presentTime);
            }

            return (returnValue);
        }

        //================================================================================================//
        /// <summary>
        /// Process actions when end of path is reached
        /// Override from AITrain class
        /// </summary>
        /// <param name="returnValue"></param>
        /// <param name="presentTime"></param>
        public override void ProcessEndOfPathReached(ref bool[] returnValue, int presentTime)
        {
            // check if any other train needs to be activated
            ActivateTriggeredTrain(TriggerActivationType.Dispose, -1);

            // check if any outstanding moving table actions
            List<DistanceTravelledItem> reqActions = RequiredActions.GetActions(0.0f, typeof(ClearMovingTableAction));
            foreach (DistanceTravelledItem thisAction in reqActions)
            {
                ClearMovingTable(thisAction);
            }

            // check if train is to form new train
            // note : if formed train == 0, formed train is player train which requires different actions

            if (Forms >= 0 && DetachActive[DetachDetailsIndex.DetachActiveList] == -1)
            {
                // check if anything needs be detached
                bool allowForm = true; // preset form may be activated

                if (DetachDetails.TryGetValue(-1, out List<DetachInfo> detachList))
                {
                    for (int iDetach = detachList.Count - 1; iDetach >= 0; iDetach--)
                    {
                        DetachInfo thisDetach = detachList[iDetach];
                        if (thisDetach.DetachPosition == DetachPositionInfo.End && thisDetach.Valid)
                        {
                            DetachActive[DetachDetailsIndex.DetachDetailsList] = -1;
                            DetachActive[DetachDetailsIndex.DetachActiveList] = iDetach;
                            allowForm = thisDetach.PerformDetach(this, true);
                            thisDetach.Valid = false;
                        }
                    }
                    if (detachList.Count <= 0 & allowForm)
                        DetachDetails.Remove(-1);
                }

                // if detach was performed, form may proceed
                if (allowForm)
                {
                    FormTrainFromAI(presentTime);
                    returnValue[1] = false;
                }
            }

            // check if train is to remain as static
            else if (FormsStatic)
            {
                // check if anything needs be detached
                if (DetachDetails.TryGetValue(-1, out List<DetachInfo> detachList))
                {
                    for (int iDetach = detachList.Count - 1; iDetach >= 0; iDetach--)
                    {
                        DetachInfo thisDetach = detachList[iDetach];
                        if (thisDetach.DetachPosition == DetachPositionInfo.End && thisDetach.Valid)
                        {
                            DetachActive[DetachDetailsIndex.DetachDetailsList] = -1;
                            DetachActive[DetachDetailsIndex.DetachActiveList] = iDetach;
                            thisDetach.PerformDetach(this, true);
                            thisDetach.Valid = false;
                        }
                    }
                    if (detachList.Count <= 0)
                        DetachDetails.Remove(-1);
                }

                MovementState = AiMovementState.Static;
                ControlMode = TrainControlMode.Inactive;
                StartTime = null;  // set starttime to invalid
                ActivateTime = null;  // set activate to invalid

                // remove existing train from track
                TrackCircuitSection[] occupiedSections = new TrackCircuitSection[OccupiedTrack.Count];
                OccupiedTrack.CopyTo(occupiedSections);
                RemoveFromTrack();
                TrainDeadlockInfo.ClearDeadlocks();

                foreach (TrackCircuitSection occSection in occupiedSections)
                {
                    occSection.SetOccupied(RoutedForward);
                }

                // train is in pool : update pool info
                if (!String.IsNullOrEmpty(ExitPool))
                {
                    TimetablePool thisPool = simulator.PoolHolder.Pools[ExitPool];

                    if (thisPool.StoragePool[PoolStorageIndex].StoredUnits.Contains(Number) && !thisPool.StoragePool[PoolStorageIndex].ClaimUnits.Contains(Number))
                    {
                        Trace.TraceWarning("Pool {0} : train : {1} ({2}) : adding train allready in pool \n", thisPool.PoolName, Name, Number);
                    }
                    else
                    {
                        thisPool.AddUnit(this, false);
                        Update(0);
                    }
                }
            }
            else if (AttachDetails != null && AttachDetails.Valid)
            {
            }
            else
            {
                RemoveTrain();
                returnValue[1] = false;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Form train from existing AI train (i.e. not from player train)
        /// </summary>
        /// <param name="presentTime"></param>
        public void FormTrainFromAI(int presentTime)
        {
            TTTrain formedTrain = null;
            bool autogenStart = false;

            if (Forms == 0)
            {
                formedTrain = simulator.Trains[0] as TTTrain; // get player train
                formedTrain.TrainType = TrainType.PlayerIntended;
            }
            else
            {
                // get train which is to be formed
                formedTrain = AI.StartList.GetNotStartedTTTrainByNumber(Forms, true);

                if (formedTrain == null)
                {
                    formedTrain = simulator.GetAutoGenTTTrainByNumber(Forms);
                    autogenStart = true;
                }
            }

            // if found - start train
            if (formedTrain != null)
            {
                // remove existing train
                TrackCircuitSection[] occupiedSections = new TrackCircuitSection[OccupiedTrack.Count];
                OccupiedTrack.CopyTo(occupiedSections);

                Forms = -1;
                RemoveTrain();

                // set details for new train from existing train
                bool validFormed = formedTrain.StartFromAITrain(this, presentTime, occupiedSections);

                if (validFormed)
                {
                    // start new train
                    if (!autogenStart)
                    {
                        simulator.StartReference.Remove(formedTrain.Number);
                    }

                    if (formedTrain.TrainType == TrainType.PlayerIntended)
                    {
                        AI.TrainsToAdd.Add(formedTrain);

                        // set player locomotive
                        // first test first and last cars - if either is drivable, use it as player locomotive
                        simulator.PlayerLocomotive = formedTrain.LeadLocomotive = formedTrain.Cars[0] as MSTSLocomotive ?? formedTrain.Cars[^1] as MSTSLocomotive ?? formedTrain.Cars.OfType<MSTSLocomotive>().FirstOrDefault();

                        // only initialize brakes if previous train was not player train
                        if (TrainType == TrainType.Player)
                        {
                            formedTrain.ConnectBrakeHoses();
                        }
                        else
                        {
                            formedTrain.InitializeBrakes();
                        }

                        if (simulator.PlayerLocomotive == null && (formedTrain.NeedAttach == null || formedTrain.NeedAttach.Count <= 0))
                        {
                            throw new InvalidDataException("Can't find player locomotive in " + formedTrain.Name);
                        }
                        else
                        {
                            foreach (TrainCar car in formedTrain.Cars)
                            {
                                if (car.WagonType == WagonType.Engine)
                                {
                                    MSTSLocomotive loco = car as MSTSLocomotive;
                                    loco.AntiSlip = formedTrain.LeadLocoAntiSlip;
                                }
                            }
                        }
                    }
                    else
                    {
                        formedTrain.TrainType = TrainType.Ai;
                        AI.TrainsToAdd.Add(formedTrain);
                    }

                    formedTrain.MovementState = AiMovementState.Static;
                    formedTrain.SetFormedOccupied();

                    if (MovementState == AiMovementState.StationStop && formedTrain.StationStops != null && formedTrain.StationStops.Count > 0)
                    {
                        if (StationStops[0].PlatformReference == formedTrain.StationStops[0].PlatformReference)
                        {
                            formedTrain.AtStation = true;
                            formedTrain.StationStops[0].ActualArrival = StationStops[0].ActualArrival;
                            formedTrain.StationStops[0].ArrivalTime = StationStops[0].ArrivalTime;
                            formedTrain.StationStops[0].CalculateDepartTime(this);
                        }
                    }
                }
                else if (!autogenStart)
                {
                    // reinstate as to be started (note : train is not yet removed from reference)
                    AI.StartList.InsertTrain(formedTrain);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check End of Route Position
        /// Override class from Train, but needs additional checks before actually testing end of route
        /// </summary>

        protected override bool CheckEndOfRoutePosition()
        {
            // check if at end of route
            bool endOfRoute = CheckEndOfRoutePositionTT();

            // if so, perform further checks
            if (endOfRoute)
            {
                // if train needs to pick up before reaching end of path, continue train
                if (PickUpStaticOnForms && TCRoute.ActiveSubPath == (TCRoute.TCRouteSubpaths.Count - 1))
                {
                    return (false);
                }

                // if present or any further sections are platform section and pick up is required in platform, continue train
                // if present or any further sections occupied by train which must be picked up, continue train
                for (int iRouteIndex = PresentPosition[Direction.Forward].RouteListIndex; iRouteIndex < ValidRoutes[Direction.Forward].Count; iRouteIndex++)
                {
                    // check platform
                    TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][iRouteIndex].TrackCircuitSection;
                    foreach (int thisPlatform in thisSection.PlatformIndices)
                    {
                        foreach (int platformReference in Simulator.Instance.SignalEnvironment.PlatformDetailsList[thisPlatform].PlatformReference)
                        {
                            if (PickUpStatic.Contains(platformReference))
                            {
                                return (false);
                            }
                        }
                    }


                    // check occupying trains
                    List<TrainRouted> otherTrains = thisSection.CircuitState.TrainsOccupying();

                    foreach (TrainRouted otherTrain in otherTrains)
                    {
                        TTTrain otherTTTrain = otherTrain.Train as TTTrain;
                        // check for pickup
                        if (CheckPickUp(otherTTTrain))
                        {
                            return (false);
                        }
                        else if (TransferTrainDetails.ContainsKey(otherTTTrain.OrgAINumber))
                        {
                            return (false);
                        }
                        else if (AttachDetails != null && AttachDetails.Valid && AttachDetails.TrainNumber == otherTTTrain.OrgAINumber)
                        {
                            return (false);
                        }
                    }
                }
            }

            return (endOfRoute);
        }

        //================================================================================================//
        /// <summary>
        /// Check End of Route Position
        /// </summary>

        public bool CheckEndOfRoutePositionTT()
        {
            bool endOfRoute = false;

            // only allowed when stopped
            if (Math.Abs(SpeedMpS) > 0.05f)
            {
                return (endOfRoute);
            }

            // if access to pool is required and section is in present route, train can never be at end of route

            if (PoolAccessSection >= 0)
            {
                int poolAccessRouteIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PoolAccessSection, 0);
                if (poolAccessRouteIndex >= 0)
                {
                    return (endOfRoute);
                }
            }

            // if not stopped in station and next stop in this subpath, it cannot be end of route
            if (!AtStation && StationStops.Count > 0 && StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath)
            {
                return (endOfRoute);
            }

            // if stopped at station and next stop in this subpath, it cannot be end of route
            if (AtStation && StationStops.Count > 1 && StationStops[1].SubrouteIndex == TCRoute.ActiveSubPath)
            {
                return (endOfRoute);
            }

            // if stopped in last section of route and this section is exit to moving table switch to moving table mode
            if (ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].MovingTableApproachPath > -1)
            {
                if (simulator.PoolHolder.Pools.TryGetValue(ExitPool, out TimetablePool thisPool))
                {
                    if (thisPool.GetType() == typeof(TimetableTurntablePool))
                    {
                        TimetableTurntablePool thisTurntablePool = thisPool as TimetableTurntablePool;
                        ActiveTurntable = new TimetableTurntableControl(thisTurntablePool, thisTurntablePool.PoolName, thisTurntablePool.AdditionalTurntableDetails.TurntableIndex, this);
                        ActiveTurntable.MovingTableState = MovingTableState.WaitingMovingTableAvailability;
                        ActiveTurntable.MovingTableAction = MovingTableAction.FromAccess;
                        MovementState = AiMovementState.Turntable;
                        return (endOfRoute);
                    }
                }
            }

            // obtain reversal section index
            int reversalSectionIndex = -1;
            if (TCRoute != null && (ControlMode == TrainControlMode.AutoNode || ControlMode == TrainControlMode.AutoSignal))
            {
                TrackCircuitReversalInfo thisReversal = TCRoute.ReversalInfo[TCRoute.ActiveSubPath];
                if (thisReversal.Valid)
                {
                    reversalSectionIndex = (thisReversal.SignalUsed && !ForceReversal) ? thisReversal.LastSignalIndex : thisReversal.LastDivergeIndex;
                }
            }

            // if last entry in route is END_OF_TRACK, check against previous entry as this can never be the trains position nor a signal reference section
            int lastValidRouteIndex = ValidRoutes[Direction.Forward].Count - 1;
            if (ValidRoutes[Direction.Forward][lastValidRouteIndex].TrackCircuitSection.CircuitType == TrackCircuitType.EndOfTrack)
                lastValidRouteIndex--;

            // train authority is end of path
            if (ControlMode == TrainControlMode.AutoNode &&
                (EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.EndOfTrack ||
                EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.EndOfPath ||
                EndAuthorities[Direction.Forward].EndAuthorityType == EndAuthorityType.EndOfAuthority))
            {
                // front is in last route section
                if (PresentPosition[Direction.Forward].RouteListIndex == lastValidRouteIndex)
                {
                    endOfRoute = true;
                }
                // front is within 150m. of end of route and no junctions inbetween (only very short sections ahead of train)
                else
                {
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
                    float lengthToGo = thisSection.Length - PresentPosition[Direction.Forward].Offset;

                    bool junctionFound = false;
                    for (int iIndex = PresentPosition[Direction.Forward].RouteListIndex + 1; iIndex <= lastValidRouteIndex && !junctionFound; iIndex++)
                    {
                        thisSection = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection;
                        junctionFound = thisSection.CircuitType == TrackCircuitType.Junction;
                        lengthToGo += thisSection.Length;
                    }

                    if (lengthToGo < endOfRouteDistance && !junctionFound)
                    {
                        endOfRoute = true;
                    }
                }
            }


            // other checks unrelated to state
            if (!endOfRoute)
            {
                // if train is in station and endstop is set
                if (AtStation)
                {
                    endOfRoute = StationStops[0].EndStop;
                }
            }

            // if end of train on last section in route - end of route reached
            if (!endOfRoute)
            {
                if (PresentPosition[Direction.Backward].RouteListIndex == lastValidRouteIndex)
                {
                    endOfRoute = true;
                }
                // if length of last section is less than train length, check if front position is on last section
                else
                {
                    TrackCircuitSection lastSection = ValidRoutes[Direction.Forward][lastValidRouteIndex].TrackCircuitSection;
                    if (lastSection.Length < Length && PresentPosition[Direction.Forward].RouteListIndex == lastValidRouteIndex)
                    {
                        endOfRoute = true;
                    }
                }
            }

            // if in last station and station is on end of route
            if (!endOfRoute)
            {
                if (MovementState == AiMovementState.StationStop && StationStops.Count == 1)
                {
                    StationStop presentStation = StationStops[0];
                    int stationRouteIndex = -1;

                    // check all platform sections
                    foreach (int sectionIndex in presentStation.PlatformItem.TCSectionIndex)
                    {
                        stationRouteIndex = ValidRoutes[Direction.Forward].GetRouteIndex(sectionIndex, PresentPosition[Direction.Backward].RouteListIndex);
                        if (stationRouteIndex > 0)
                        {
                            break;
                        }
                    }

                    if (stationRouteIndex < 0 || stationRouteIndex == lastValidRouteIndex)
                    {
                        endOfRoute = true;
                    }

                    // test length of track beyond station
                    else
                    {
                        float remainLength = 0;
                        for (int Index = stationRouteIndex; Index <= lastValidRouteIndex; Index++)
                        {
                            remainLength += ValidRoutes[Direction.Forward][Index].TrackCircuitSection.Length;
                            if (remainLength > 2 * Length)
                                break;
                        }

                        if (remainLength < Length)
                        {
                            endOfRoute = true;
                        }
                    }
                }
            }

            // if waiting for next signal and section in front of signal is last in route - end of route reached
            // if stopped at station and NoWaitSignal is set, train cannot be waiting for a signal
            if (!endOfRoute && !(AtStation && StationStops[0].NoWaitSignal))
            {
                if (NextSignalObjects[Direction.Forward] != null && PresentPosition[Direction.Forward].TrackCircuitSectionIndex == NextSignalObjects[Direction.Forward].TrackCircuitIndex &&
                     NextSignalObjects[Direction.Forward].TrackCircuitIndex == ValidRoutes[Direction.Forward][lastValidRouteIndex].TrackCircuitSection.Index)
                {
                    endOfRoute = true;
                }
                if (NextSignalObjects[Direction.Forward] != null && ControlMode == TrainControlMode.AutoSignal && CheckTrainWaitingForSignal(NextSignalObjects[Direction.Forward], Direction.Forward) &&
                 NextSignalObjects[Direction.Forward].TrackCircuitIndex == ValidRoutes[Direction.Forward][lastValidRouteIndex].TrackCircuitSection.Index)
                {
                    endOfRoute = true;
                }
            }

            // if waiting for next signal and section beyond signal is last in route and there is no valid reversal index - end of route reached
            // if stopped at station and NoWaitSignal is set, train cannot be waiting for a signal
            if (!endOfRoute && !(AtStation && StationStops[0].NoWaitSignal))
            {
                if (NextSignalObjects[Direction.Forward] != null && PresentPosition[Direction.Forward].TrackCircuitSectionIndex == NextSignalObjects[Direction.Forward].TrackCircuitIndex &&
                     NextSignalObjects[Direction.Forward].TrackCircuitNextIndex == ValidRoutes[Direction.Forward][lastValidRouteIndex].TrackCircuitSection.Index && reversalSectionIndex < 0)
                {
                    endOfRoute = true;
                }
            }

            // if remaining section length is less than safety distance
            if (!endOfRoute)
            {
                TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].TrackCircuitSection;
                float remLength = (thisSection.Length - PresentPosition[Direction.Forward].Offset);

                for (int Index = PresentPosition[Direction.Forward].RouteListIndex + 1; Index <= lastValidRouteIndex && (remLength < 2 * StandardOverlapM); Index++)
                {
                    remLength += ValidRoutes[Direction.Forward][Index].TrackCircuitSection.Length;
                }

                if (remLength < 2 * StandardOverlapM)
                {
                    endOfRoute = true;
                }
            }

            // if next action is end of route and remaining distance is less than safety distance and no junction ahead of rear of train
            if (!endOfRoute)
            {
                bool junctionFound = false;

                for (int Index = PresentPosition[Direction.Backward].RouteListIndex + 1; Index <= lastValidRouteIndex && !junctionFound; Index++)
                {
                    junctionFound = ValidRoutes[Direction.Forward][Index].TrackCircuitSection.CircuitType == TrackCircuitType.Junction;
                }

                if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.EndOfRoute && !junctionFound)
                {
                    float remDistance = nextActionInfo.ActivateDistanceM - DistanceTravelledM;
                    if (remDistance < 2 * StandardOverlapM)
                    {
                        endOfRoute = true;
                    }
                }
            }

            // if rear of train is beyond reversal section
            if (!endOfRoute)
            {
                if (reversalSectionIndex >= 0 && PresentPosition[Direction.Backward].RouteListIndex >= reversalSectionIndex)
                {
                    // if there is a station ahead, this is not end of route
                    if (MovementState != AiMovementState.StationStop && StationStops != null && StationStops.Count > 0 &&
                        StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath)
                    {
                        endOfRoute = false;
                    }
                    else
                    {
                        endOfRoute = true;
                    }
                }
            }

            // if remaining length less then train length and no junctions to end of route - end of route reached
            // if no junctions or signals to end of route - end of route reached
            if (!endOfRoute)
            {
                bool intermediateJunction = false;
                bool intermediateSignal = false;
                float length = 0f;
                float distanceToNextJunction = -1f;
                float distanceToNextSignal = -1f;

                if (PresentPosition[Direction.Backward].RouteListIndex >= 0) // end of train is on route
                {
                    TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][PresentPosition[Direction.Backward].RouteListIndex].TrackCircuitSection;
                    TrackDirection direction = ValidRoutes[Direction.Forward][PresentPosition[Direction.Backward].RouteListIndex].Direction;
                    length = (thisSection.Length - PresentPosition[Direction.Backward].Offset);
                    if (thisSection.EndSignals[direction] != null)                         // check for signal only in direction of train (other signal is behind train)
                    {
                        intermediateSignal = true;
                        distanceToNextSignal = length; // distance is total length
                    }

                    if (thisSection.CircuitType == TrackCircuitType.Junction || thisSection.CircuitType == TrackCircuitType.Crossover)
                    {
                        intermediateJunction = true;
                        distanceToNextJunction = 0f;
                    }

                    for (int iIndex = PresentPosition[Direction.Backward].RouteListIndex + 1; iIndex >= 0 && iIndex <= lastValidRouteIndex; iIndex++)
                    {
                        thisSection = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection;
                        length += thisSection.Length;

                        if (thisSection.CircuitType == TrackCircuitType.Junction ||
                            thisSection.CircuitType == TrackCircuitType.Crossover)
                        {
                            intermediateJunction = true;
                            distanceToNextJunction = distanceToNextJunction < 0 ? length : distanceToNextJunction;
                        }

                        if (thisSection.EndSignals[direction] != null)
                        {
                            intermediateSignal = true;
                            distanceToNextSignal = distanceToNextSignal < 0 ? length : distanceToNextSignal;
                        }
                        if (thisSection.EndSignals[direction.Reverse()] != null) // check in other direction
                        {
                            intermediateSignal = true;
                            distanceToNextSignal = distanceToNextSignal < 0 ? length - thisSection.Length : distanceToNextSignal; // signal is at start of section
                        }
                    }
                    // check if intermediate junction or signal is valid : only so if there is enough distance (from the front of the train) left for train to pass that junction
                    // however, do accept signal or junction if train is still in first section
                    float frontlength = length;
                    if (intermediateJunction)
                    {
                        if ((frontlength - distanceToNextJunction) < Length && PresentPosition[Direction.Forward].RouteListIndex > 0)
                            intermediateJunction = false;
                    }

                    if (intermediateSignal)
                    {
                        if ((frontlength - distanceToNextSignal) < Length && PresentPosition[Direction.Forward].RouteListIndex > 0)
                            intermediateSignal = false;
                    }
                }
                else if (PresentPosition[Direction.Forward].RouteListIndex >= 0) // else use front position - check for further signals or junctions only
                {
                    for (int iIndex = PresentPosition[Direction.Forward].RouteListIndex; iIndex >= 0 && iIndex <= lastValidRouteIndex; iIndex++)
                    {
                        TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection;
                        TrackDirection direction = ValidRoutes[Direction.Forward][iIndex].Direction;

                        if (thisSection.CircuitType == TrackCircuitType.Junction ||
                            thisSection.CircuitType == TrackCircuitType.Crossover)
                        {
                            intermediateJunction = true;
                        }

                        if (thisSection.EndSignals[direction] != null)
                        {
                            intermediateSignal = true;
                        }
                    }
                }

                // check overall position

                if (!intermediateJunction && !intermediateSignal && (StationStops == null || StationStops.Count < 1))  // no more junctions and no more signal and no more stations - reverse subpath
                {
                    endOfRoute = true;
                }

                // check if there is a train ahead, and that train is stopped at the end of our route - if so, we can't go any further

                if (!endOfRoute)
                {
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
                    Dictionary<Train, float> trainInfo = thisSection.TestTrainAhead(this, PresentPosition[Direction.Forward].Offset, PresentPosition[Direction.Forward].Direction);

                    if (trainInfo.Count > 0)
                    {
                        foreach (KeyValuePair<Train, float> trainAhead in trainInfo)
                        {
                            TTTrain otherTrain = trainAhead.Key as TTTrain;
                            if (Math.Abs(otherTrain.SpeedMpS) < 0.1f) // other train must be stopped
                            {
                                if (otherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex == ValidRoutes[Direction.Forward][lastValidRouteIndex].TrackCircuitSection.Index)
                                {
                                    endOfRoute = true;
                                }
                                else if (otherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex == ValidRoutes[Direction.Forward][lastValidRouteIndex].TrackCircuitSection.Index)
                                {
                                    endOfRoute = true;
                                }
                            }
                        }
                    }
                }
            }

            // return state

            // never return end of route if train has not moved
            if (endOfRoute && DistanceTravelledM < 0.1)
                endOfRoute = false;

            return (endOfRoute);
        }

        //================================================================================================//
        /// <summary>
        /// Remove train
        /// Override from Train class
        /// <\summary>

        internal override void RemoveTrain()
        {
            RemoveFromTrack();
            TrainDeadlockInfo.ClearDeadlocks();

            // if train was to form another train, ensure this other train is started by removing the formed link
            if (Forms >= 0)
            {
                TTTrain formedTrain = AI.StartList.GetNotStartedTTTrainByNumber(Forms, true);
                if (formedTrain != null)
                {
                    formedTrain.FormedOf = -1;
                    formedTrain.FormedOfType = TimetableFormationCommand.None;
                }
            }

            // remove train
            AI.TrainsToRemove.Add(this);
        }

        /// <summary>
        /// Add reversal info to TrackMonitorInfo
        /// Override from Train class
        /// </summary>
        private protected override void AddTrainReversalInfo(TrainInfo trainInfo, TrackCircuitReversalInfo reversalInfo)
        {
            if (!reversalInfo.Valid)
                return;

            int reversalSection = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][(TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count) - 1].TrackCircuitSection.Index;
            if (reversalInfo.LastDivergeIndex >= 0)
            {
                reversalSection = reversalInfo.SignalUsed ? reversalInfo.SignalSectorIndex : reversalInfo.DivergeSectorIndex;
            }

            TrackCircuitSection rearSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];
            float reversalDistanceM = TrackCircuitSection.GetDistanceBetweenObjects(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset, PresentPosition[Direction.Backward].Direction,
            reversalSection, 0.0f);

            bool reversalEnabled = true;
            if (reversalDistanceM > 0)
            {
                trainInfo.ObjectInfoForward.Add(new TrainPathItem(reversalEnabled, reversalDistanceM, true));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check for end of route actions - for PLAYER train only
        /// Reverse train if required
        /// Return parameter : true if train still exists (used only for player train)
        /// Override from Train class
        /// </summary>
        protected override bool CheckRouteActions(double elapsedClockSeconds)
        {
            TrackDirection directionNow = PresentPosition[Direction.Forward].Direction;
            int positionNow = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;

            if (PresentPosition[Direction.Forward].RouteListIndex >= 0)
                directionNow = ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].Direction;

            // check if at station
            CheckStationTask();
            if (DetachPending)
                return (true);  // do not check for further actions if player train detach is pending

            (bool endOfRoute, bool otherRouteAvailable) = UpdateRouteActions(elapsedClockSeconds);
            if (!endOfRoute)
                return (true);  // not at end of route

            // check if train reversed

            if (otherRouteAvailable)
            {
                if (positionNow == PresentPosition[Direction.Forward].TrackCircuitSectionIndex && directionNow != PresentPosition[Direction.Forward].Direction)
                {
                    ReverseFormation(true);
                }
                else if (positionNow == PresentPosition[Direction.Backward].TrackCircuitSectionIndex && directionNow != PresentPosition[Direction.Backward].Direction)
                {
                    ReverseFormation(true);
                }

                // check if next station was on previous subpath - if so, move to this subpath

                if (StationStops.Count > 0)
                {
                    StationStop thisStation = StationStops[0];
                    if (thisStation.SubrouteIndex < TCRoute.ActiveSubPath)
                    {
                        thisStation.SubrouteIndex = TCRoute.ActiveSubPath;
                    }
                }
            }

            //process train end for player if stopped
            else
            {
                if (Math.Abs(SpeedMpS) < 0.05f)
                {
                    SpeedMpS = 0.0f;
                    return (ProcessRouteEndTimetablePlayer());
                }
            }

            // return train still exists

            return (true);
        }

        //================================================================================================//
        /// <summary>
        /// compute boarding time for timetable mode
        /// also check validity of depart time value
        /// Override from Train class
        /// <\summary>

        internal override (bool, int) ComputeTrainBoardingTime(StationStop thisStop, int stopTime)
        {
            // use minimun station dwell time
            if (stopTime <= 0 && thisStop.ActualMinStopTime.HasValue)
            {
                stopTime = thisStop.ActualMinStopTime.Value;
            }
            else if (stopTime <= 0)
            {
                stopTime = thisStop.PlatformItem.MinWaitingTime;
            }
            else if (thisStop.ActualArrival > thisStop.ArrivalTime && stopTime > thisStop.PlatformItem.MinWaitingTime)
            {
                stopTime = thisStop.PlatformItem.MinWaitingTime;
            }

            return (true, stopTime);
        }

        //================================================================================================//
        /// <summary>
        /// setup station stop handling for player train
        /// </summary>

        public void SetupStationStopHandling()
        {
            CheckStations = true;  // set station stops to be handled by train

            // check if initial at station
            if (StationStops.Count > 0)
            {
                int frontIndex = PresentPosition[Direction.Forward].RouteListIndex;
                int rearIndex = PresentPosition[Direction.Backward].RouteListIndex;
                List<int> occupiedSections = new List<int>();

                int startIndex = frontIndex < rearIndex ? frontIndex : rearIndex;
                int stopIndex = frontIndex < rearIndex ? rearIndex : frontIndex;

                for (int iIndex = startIndex; iIndex <= stopIndex; iIndex++)
                {
                    occupiedSections.Add(ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection.Index);
                }

                foreach (int sectionIndex in StationStops[0].PlatformItem.TCSectionIndex)
                {
                    if (occupiedSections.Contains(sectionIndex))
                    {
                        AtStation = true;
                        int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
                        if (StationStops[0].ActualArrival < 0)
                        {
                            StationStops[0].ActualArrival = presentTime;
                            StationStops[0].CalculateDepartTime(this);
                        }
                        break;
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check on station tasks for player train
        /// Override from Train class, to allow call from common methods
        /// </summary>
        protected override void CheckStationTask()
        {
            // if at station
            if (AtStation)
            {
                // check for activation of other train
                ActivateTriggeredTrain(TriggerActivationType.StationStop, StationStops[0].PlatformReference);

                // get time
                int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
                int eightHundredHours = 8 * 3600;
                int sixteenHundredHours = 16 * 3600;

                // if moving, set departed
                if (Math.Abs(SpeedMpS) > 1.0f)
                {
                    StationStops[0].ActualDepart = presentTime;
                    StationStops[0].Passed = true;
                    AtStation = false;
                    MayDepart = false;
                    DisplayMessage = "";
                    Delay = TimeSpan.FromSeconds((presentTime - StationStops[0].DepartTime) % (24 * 3600));

                    // check for activation of other train
                    ActivateTriggeredTrain(TriggerActivationType.StationDepart, StationStops[0].PlatformReference);

                    // remove stop
                    PreviousStop = StationStops[0].CreateCopy();
                    StationStops.RemoveAt(0);
                }
                else
                {
                    // check for detach
                    if (DetachDetails.TryGetValue(StationStops[0].PlatformReference, out List<DetachInfo> detachList))
                    {
                        bool detachPerformed = DetachActive[DetachDetailsIndex.DetachActiveList] < 0;

                        for (int iDetach = 0; iDetach < detachList.Count; iDetach++)
                        {
                            DetachInfo thisDetach = detachList[iDetach];
                            if (thisDetach.Valid)
                            {
                                DetachActive[DetachDetailsIndex.DetachDetailsList] = StationStops[0].PlatformReference;
                                DetachActive[DetachDetailsIndex.DetachActiveList] = iDetach;
                                detachPerformed = thisDetach.PerformDetach(this, true);
                                thisDetach.Valid = false;
                            }
                        }
                        if (detachPerformed)
                        {
                            DetachDetails.Remove(StationStops[0].PlatformReference);
                        }
                    }

                    // check for connection
                    int helddepart = -1;
                    int needwait = -1;

                    // keep trying to set connections as train may be created during stop
                    foreach (int otherTrainNumber in StationStops[0].ConnectionsWaiting ?? Enumerable.Empty<int>())
                    {
                        TTTrain otherTrain = GetOtherTTTrainByNumber(otherTrainNumber);
                        if (otherTrain != null)
                        {
                            foreach (StationStop otherStop in otherTrain.StationStops)
                            {
                                if (string.Equals(StationStops[0].PlatformItem.Name, otherStop.PlatformItem.Name, StringComparison.OrdinalIgnoreCase) && otherStop.ConnectionsAwaited.ContainsKey(OrgAINumber))
                                {
                                    otherStop.ConnectionsAwaited.Remove(OrgAINumber);
                                    otherStop.ConnectionsAwaited.Add(OrgAINumber, (int)StationStops[0].ActualArrival);
                                }
                            }
                        }
                    }

                    // check if waiting for connection
                    if (StationStops[0].ConnectionsAwaited?.Count > 0)
                    {
                        needwait = ProcessConnections(StationStops[0], out helddepart);
                    }

                    // check for attachments
                    int waitAttach = -1;

                    if (NeedAttach.TryGetValue(StationStops[0].PlatformReference, out List<int> needAttachList) && needAttachList.Count > 0)
                    {
                        waitAttach = needAttachList[0];
                    }

                    int waitTransfer = -1;
                    if (NeedStationTransfer.TryGetValue(StationStops[0].PlatformReference, out List<int> needTransferList) && needTransferList.Count > 0)
                    {
                        waitTransfer = needTransferList[0];
                    }

                    // check if attaching
                    int waitArrivalAttach = -1;
                    bool readyToAttach = false;
                    bool attaching = false;
                    TTTrain attachTrain = null;

                    if (AttachDetails != null && AttachDetails.StationPlatformReference == StationStops[0].PlatformReference && AttachDetails.FirstIn)
                    {
                        attachTrain = GetOtherTTTrainByNumber(AttachDetails.TrainNumber);
                        if (attachTrain != null)
                        {
                            waitArrivalAttach = AttachDetails.TrainNumber;
                            if (attachTrain.MovementState == AiMovementState.StationStop && attachTrain.StationStops[0].PlatformReference == StationStops[0].PlatformReference)
                            {
                                // attach not already taking place
                                if (AttachDetails.ReadyToAttach)
                                {
                                    attaching = true;
                                }
                                else
                                {
                                    readyToAttach = AttachDetails.ReadyToAttach = true;
                                }
                            }
                        }
                    }

                    // set message
                    double remaining = 999;

                    if (needwait >= 0)
                    {
                        TTTrain otherTrain = GetOtherTTTrainByNumber(needwait);
                        DisplayMessage = Simulator.Catalog.GetString("Held for connecting train : ");
                        DisplayMessage = String.Concat(DisplayMessage, otherTrain.Name);
                        DisplayColor = Color.Orange;
                        remaining = 999;
                    }
                    else if (waitAttach >= 0)
                    {
                        TTTrain otherTrain = GetOtherTTTrainByNumber(waitAttach);
                        DisplayMessage = Simulator.Catalog.GetString("Waiting for train to attach : ");
                        if (otherTrain != null)
                        {
                            DisplayMessage += otherTrain.Name;
                        }
                        else
                        {
                            DisplayMessage += $"train no. {waitAttach}";
                        }
                        DisplayColor = Color.Orange;
                        remaining = 999;
                    }
                    else if (waitTransfer >= 0)
                    {
                        TTTrain otherTrain = GetOtherTTTrainByNumber(waitTransfer);
                        DisplayMessage = Simulator.Catalog.GetString("Waiting for transfer with train : ");
                        if (otherTrain != null)
                        {
                            DisplayMessage += otherTrain.Name;
                        }
                        else
                        {
                            DisplayMessage += $"train no. {waitAttach}";
                        }
                        DisplayColor = Color.Orange;
                        remaining = 999;
                    }
                    else if (waitArrivalAttach >= 0 && !readyToAttach && !attaching)
                    {
                        DisplayMessage = $"{Simulator.Catalog.GetString("Waiting for train to arrive : ")}{attachTrain.Name}";
                        DisplayColor = Color.Orange;
                        remaining = 999;
                    }
                    else if (readyToAttach)
                    {
                        string attachPositionInfo = string.Empty;

                        // if setback required, reverse train
                        if (AttachDetails.SetBack)
                        {
                            // remove any reserved sections
                            RemoveFromTrackNotOccupied(ValidRoutes[Direction.Forward]);

                            // check if train in same section
                            float distanceToTrain = 0.0f;
                            if (attachTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex == PresentPosition[Direction.Backward].TrackCircuitSectionIndex)
                            {
                                TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][PresentPosition[Direction.Backward].RouteListIndex].TrackCircuitSection;
                                distanceToTrain = thisSection.Length;
                            }
                            else
                            {
                                // get section index of other train in train route
                                int endSectionIndex = ValidRoutes[Direction.Forward].GetRouteIndexBackward(attachTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].RouteListIndex);
                                if (endSectionIndex < 0)
                                {
                                    Trace.TraceWarning("Train {0} : attach to train {1} failed, cannot find path", Name, attachTrain.Name);
                                }

                                // get distance to train
                                for (int iSection = PresentPosition[Direction.Forward].RouteListIndex; iSection >= endSectionIndex; iSection--)
                                {
                                    TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][iSection].TrackCircuitSection;
                                    distanceToTrain += thisSection.Length;
                                }
                            }

                            // create temp route and set as valid route
                            TrackDirection newDirection = PresentPosition[Direction.Forward].Direction.Reverse();
                            TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0.0f, newDirection, distanceToTrain, true, true, false);

                            // set reverse positions
                            (PresentPosition[Direction.Forward], PresentPosition[Direction.Backward]) = (PresentPosition[Direction.Backward], PresentPosition[Direction.Forward]);

                            PresentPosition[Direction.Forward].Reverse(ValidRoutes[Direction.Forward][PresentPosition[Direction.Forward].RouteListIndex].Direction, tempRoute, Length);
                            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);
                            PresentPosition[Direction.Backward].Reverse(ValidRoutes[Direction.Forward][PresentPosition[Direction.Backward].RouteListIndex].Direction, tempRoute, 0.0f);

                            // reverse formation
                            ReverseFormation(true);
                            attachPositionInfo = Simulator.Catalog.GetString(", backward");

                            // get new route list indices from new route

                            DistanceTravelledM = 0;
                            ValidRoutes[Direction.Forward] = tempRoute;

                            PresentPosition[Direction.Forward].RouteListIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                            PresentPosition[Direction.Backward].RouteListIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
                        }
                        else
                        {
                            // build path to train - straight forward, set distance of 2000m (should be enough)
                            TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0.0f, PresentPosition[Direction.Backward].Direction, 2000, true, true, false);
                            ValidRoutes[Direction.Forward] = tempRoute;
                            attachPositionInfo = Simulator.Catalog.GetString(", forward");
                        }

                        EndAuthorities[Direction.Forward].LastReservedSection = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                        EndAuthorities[Direction.Backward].LastReservedSection = PresentPosition[Direction.Backward].TrackCircuitSectionIndex;

                        MovementState = AiMovementState.Following;
                        SwitchToNodeControl(PresentPosition[Direction.Forward].TrackCircuitSectionIndex);

                        DisplayMessage = $"{Simulator.Catalog.GetString("Train is ready to attach to : ")}{attachTrain.Name}{attachPositionInfo}";
                        DisplayColor = Color.Green;
                        remaining = 999;
                    }
                    else if (attaching)
                    {
                        string attachPositionInfo = AttachDetails.SetBack ? Simulator.Catalog.GetString(", backward") : Simulator.Catalog.GetString(", forward");
                        DisplayMessage = $"{Simulator.Catalog.GetString("Train is ready to attach to : ")}{attachTrain.Name}{attachPositionInfo}";
                        DisplayColor = Color.Green;
                        remaining = 999;
                    }
                    else
                    {
                        double actualDepart = StationStops[0].ActualDepart;
                        if (helddepart >= 0)
                        {
                            actualDepart = Time.Compare.Latest(helddepart, (int)actualDepart);
                            StationStops[0].ActualDepart = actualDepart;
                        }

                        int correctedTime = presentTime;
                        if (presentTime > sixteenHundredHours && StationStops[0].DepartTime < eightHundredHours)
                        {
                            correctedTime = presentTime - 24 * 3600;  // correct to time before midnight (negative value!)
                        }

                        remaining = actualDepart - correctedTime;

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
                        if (remaining < 120 && StationStops[0].ExitSignal >= 0 && HoldingSignals.Contains(StationStops[0].ExitSignal)) // within two minutes of departure and hold signal?
                        {
                            HoldingSignals.Remove(StationStops[0].ExitSignal);

                            if (ControlMode == TrainControlMode.AutoSignal)
                            {
                                Signal nextSignal = Simulator.Instance.SignalEnvironment.Signals[StationStops[0].ExitSignal];
                                nextSignal.RequestClearSignal(ValidRoutes[Direction.Forward], RoutedForward, 0, false, null);
                            }
                        }

                        // check departure time
                        if (remaining <= 0)
                        {
                            // if at end of route allow depart without playing departure sound
                            if (CheckEndOfRoutePositionTT())
                            {
                                MayDepart = true;
                                DisplayMessage = Simulator.Catalog.GetString("Passenger detraining completed. Train terminated.");
                            }
                            else if (!MayDepart)
                            {
                                // check if signal ahead is cleared - if not, and signal is station exit signal, do not allow depart
                                if (NextSignalObjects[Direction.Forward] != null && NextSignalObjects[Direction.Forward].SignalLR(SignalFunction.Normal) == SignalAspectState.Stop
                                    && NextSignalObjects[Direction.Forward].OverridePermission != SignalPermission.Granted && !StationStops[0].NoWaitSignal
                                    && NextSignalObjects[Direction.Forward].Index == StationStops[0].ExitSignal)
                                {
                                    DisplayMessage = Simulator.Catalog.GetString("Passenger boarding completed. Waiting for signal ahead to clear.");
                                }
                                else
                                {
                                    MayDepart = true;
                                    if (!StationStops[0].EndStop)
                                    {
                                        if (!DriverOnlyOperation)
                                            simulator.SoundNotify = TrainEvent.PermissionToDepart;  // sound departure if not doo
                                        DisplayMessage = Simulator.Catalog.GetString("Passenger boarding completed. You may depart now.");
                                    }
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
                    if (Math.Abs(SpeedMpS) < 0.05f)
                    {
                        // build list of occupied section
                        int frontIndex = PresentPosition[Direction.Forward].RouteListIndex;
                        int rearIndex = PresentPosition[Direction.Backward].RouteListIndex;
                        List<int> occupiedSections = new List<int>();

                        // check valid positions
                        if (frontIndex < 0 && rearIndex < 0) // not on route so cannot be in station
                        {
                            return; // no further actions possible
                        }

                        // correct position if either end is off route
                        if (frontIndex < 0)
                            frontIndex = rearIndex;
                        if (rearIndex < 0)
                            rearIndex = frontIndex;

                        // set start and stop in correct order
                        int startIndex = frontIndex < rearIndex ? frontIndex : rearIndex;
                        int stopIndex = frontIndex < rearIndex ? rearIndex : frontIndex;

                        for (int iIndex = startIndex; iIndex <= stopIndex; iIndex++)
                        {
                            occupiedSections.Add(ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection.Index);
                        }

                        // check if any platform section is in list of occupied sections - if so, we're in the station
                        foreach (int sectionIndex in StationStops[0].PlatformItem.TCSectionIndex)
                        {
                            if (occupiedSections.Contains(sectionIndex))
                            {
                                // TODO : check offset within section
                                AtStation = true;
                                break;
                            }
                        }

                        if (AtStation)
                        {
                            MovementState = AiMovementState.StationStop;
                            int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));

                            StationStops[0].ActualArrival = presentTime;
                            StationStops[0].CalculateDepartTime(this);

                            foreach (int otherTrainNumber in StationStops[0].ConnectionsWaiting ?? Enumerable.Empty<int>())
                            {
                                TTTrain otherTrain = GetOtherTTTrainByNumber(otherTrainNumber);
                                if (otherTrain != null)
                                {
                                    foreach (StationStop otherStop in otherTrain.StationStops)
                                    {
                                        if (string.Equals(StationStops[0].PlatformItem.Name, otherStop.PlatformItem.Name, StringComparison.OrdinalIgnoreCase))
                                        {
                                            otherStop.ConnectionsAwaited.Remove(OrgAINumber);
                                            otherStop.ConnectionsAwaited.Add(OrgAINumber, (int)StationStops[0].ActualArrival);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        MovementState = AiMovementState.Running;   // reset movement state (must not remain set at STATION_STOP)
                        if (nextActionInfo != null && nextActionInfo.NextAction == AiActionType.StationStop)
                        {
                            nextActionInfo = null;   // clear next action if still referring to station stop
                        }

                        // check if station missed : station must be at least 500m. behind us
                        bool missedStation = false;

                        int stationRouteIndex = ValidRoutes[Direction.Forward].GetRouteIndex(StationStops[0].TrackCircuitSectionIndex, 0);

                        if (StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath)
                        {
                            if (stationRouteIndex < 0)
                            {
                                missedStation = true;
                            }
                            else if (stationRouteIndex < PresentPosition[Direction.Backward].RouteListIndex)
                            {
                                missedStation = ValidRoutes[Direction.Forward].GetDistanceAlongRoute(stationRouteIndex, StationStops[0].StopOffset, PresentPosition[Direction.Backward].RouteListIndex, PresentPosition[Direction.Backward].Offset, true) > 500f;
                            }
                        }

                        if (missedStation)
                        {
                            PreviousStop = StationStops[0].CreateCopy();
                            StationStops.RemoveAt(0);

                            if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                                simulator.Confirmer.Information("Missed station stop : " + PreviousStop.PlatformItem.Name);

                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Special actions in timetable mode when waiting for signal to clear
        /// Override from Train class to allow call from common methods
        /// <\summary>

        protected override bool ActionsForSignalStop()
        {
            bool result = true;
            // cannot claim if in station and noclaim is set
            if (AtStation)
            {
                if (StationStops[0].NoClaimAllowed)
                    result = false;
            }

            // test for attach for train ahead
            if (AttachDetails != null && AttachDetails.StationPlatformReference < 0 && !AttachDetails.ReadyToAttach)
            {
                for (int iIndex = PresentPosition[Direction.Forward].RouteListIndex; iIndex < ValidRoutes[Direction.Forward].Count; iIndex++)
                {
                    TrackCircuitSection thisSection = ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection;
                    if (thisSection.CircuitState.Occupied())
                    {
                        List<TrainRouted> allTrains = thisSection.CircuitState.TrainsOccupying();
                        foreach (TrainRouted routedTrain in allTrains)
                        {
                            TTTrain otherTrain = routedTrain.Train as TTTrain;
                            if (otherTrain.OrgAINumber == AttachDetails.TrainNumber)
                            {
                                if (otherTrain.AtStation)
                                {
                                    AttachDetails.ReadyToAttach = true;
                                }
                                else if (otherTrain.MovementState == AiMovementState.Static && otherTrain.ActivateTime != null)
                                {
                                    AttachDetails.ReadyToAttach = true;
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        //================================================================================================//
        /// <summary>
        /// Switch to Node control
        /// Override from Train class
        /// <\summary>

        internal override void SwitchToNodeControl(int thisSectionIndex)
        {
            base.SwitchToNodeControl(thisSectionIndex);

            // check if train is to attach in sections ahead (otherwise done at signal)
            if (TrainType != TrainType.Player)
            {
                CheckReadyToAttach();
            }
        }

        //================================================================================================//
        /// <summary>
        /// Clear station from list, clear exit signal if required
        /// Override from Train class
        /// <\summary>

        internal override void ClearStation(int id1, int id2, bool removeStation)
        {
            int foundStation = -1;
            StationStop thisStation = null;

            for (int iStation = 0; iStation < StationStops.Count && foundStation < 0; iStation++)
            {
                thisStation = StationStops[iStation];
                if (thisStation.PlatformReference == id1 ||
                    thisStation.PlatformReference == id2)
                {
                    foundStation = iStation;
                }

                if (thisStation.SubrouteIndex > TCRoute.ActiveSubPath)
                    break; // stop looking if station is in next subpath
            }

            if (foundStation >= 0)
            {
                thisStation = StationStops[foundStation];
                if (thisStation.ExitSignal >= 0)
                {
                    HoldingSignals.Remove(thisStation.ExitSignal);

                    if (ControlMode == TrainControlMode.AutoSignal)
                    {
                        Signal nextSignal = Simulator.Instance.SignalEnvironment.Signals[thisStation.ExitSignal];
                        nextSignal.RequestClearSignal(ValidRoutes[Direction.Forward], RoutedForward, 0, false, null);
                    }
                }
            }
            if (removeStation)
            {
                for (int iStation = foundStation; iStation >= 0; iStation--)
                {
                    StationStops.RemoveAt(iStation);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Process connection at station stop
        /// </summary>
        /// <param name="thisStop"></param>
        /// <param name="deptime"></param>
        /// <returns></returns>
        public int ProcessConnections(StationStop thisStop, out int deptime)
        {
            int? helddepart = null;
            int needwait = -1;
            List<int> removeKeys = new List<int>();

            foreach (KeyValuePair<int, int> connectionInfo in thisStop.ConnectionsAwaited ?? Enumerable.Empty<KeyValuePair<int, int>>())
            {
                // check if train arrival time set
                int otherTrainNumber = connectionInfo.Key;
                WaitInfo reqWait = thisStop.ConnectionDetails?[otherTrainNumber];

                if (reqWait != null && connectionInfo.Value >= 0)
                {
                    removeKeys.Add(connectionInfo.Key);
                    int reqHoldTime = (reqWait.HoldTimeS.HasValue) ? reqWait.HoldTimeS.Value : 0;
                    int allowedDepart = (connectionInfo.Value + reqHoldTime) % (24 * 3600);
                    if (helddepart.HasValue)
                    {
                        helddepart = Time.Compare.Latest(helddepart.Value, allowedDepart);
                    }
                    else
                    {
                        helddepart = allowedDepart;
                    }
                }
                else
                // check if train exists and if so, check its delay
                {
                    TTTrain otherTrain = GetOtherTTTrainByNumber(otherTrainNumber);

                    if (otherTrain != null)
                    {
                        // get station index for other train
                        StationStop reqStop = null;

                        foreach (StationStop nextStop in otherTrain.StationStops)
                        {
                            if (nextStop.PlatformItem.Name.Equals(StationStops[0].PlatformItem.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                reqStop = nextStop;
                                break;
                            }
                        }

                        // check if train is not passed the station
                        if (reqStop != null && reqWait != null)
                        {
                            if (otherTrain.Delay.HasValue && reqWait.MaxDelayS.HasValue)
                            {
                                if (otherTrain.Delay.Value.TotalSeconds <= reqWait.MaxDelayS.Value)
                                {
                                    needwait = otherTrainNumber;  // train is within allowed time - wait required
                                    break;                        // no need to check other trains
                                }
                                else if (Delay.HasValue && (thisStop.ActualDepart > reqStop.ArrivalTime + otherTrain.Delay.Value.TotalSeconds))
                                {
                                    needwait = otherTrainNumber;  // train expected to arrive before our departure - wait
                                    break;
                                }
                                else
                                {
                                    removeKeys.Add(connectionInfo.Key); // train is excessively late - remove connection
                                }
                            }
                            else
                            {
                                needwait = otherTrainNumber;
                                break;                        // no need to check other trains
                            }
                        }
                    }
                }
            }

            // remove processed keys
            foreach (int key in removeKeys)
            {
                thisStop.ConnectionsAwaited.Remove(key);
            }

            // set departure time
            deptime = -1;
            if (helddepart.HasValue)
            {
                deptime = helddepart.Value;
            }

            return (needwait);
        }

        //================================================================================================//
        /// <summary>
        /// Perform end of route actions for player train
        /// Detach any required portions
        /// Return parameter : true is train still exists
        /// </summary>
        public bool ProcessRouteEndTimetablePlayer()
        {
            ActivateTriggeredTrain(TriggerActivationType.Dispose, -1);

            int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
            int nextTrainNumber = -1;
            bool stillExist = true;

            // check if needs to attach - if so, keep alive
            if (AttachDetails != null && AttachDetails.Valid)
            {
                return (true);
            }

            // check if final station not yet processed and any detach actions required
            bool allowForm = DetachActive[DetachDetailsIndex.DetachActiveList] == -1; // preset if form is allowed to proceed - may not proceed if detach action is still active

            // check if detach action active
            if (DetachActive[DetachDetailsIndex.DetachActiveList] == -1)
            {
                if (StationStops != null && StationStops.Count > 0)
                {
                    if (DetachDetails.TryGetValue(StationStops[0].PlatformReference, out List<DetachInfo> detachActionList))
                    {
                        for (int iDetach = detachActionList.Count - 1; iDetach >= 0; iDetach--)
                        {
                            DetachInfo thisDetach = detachActionList[iDetach];
                            if (thisDetach.DetachPosition == DetachPositionInfo.End && thisDetach.Valid)
                            {
                                DetachActive[DetachDetailsIndex.DetachDetailsList] = -1;
                                DetachActive[DetachDetailsIndex.DetachActiveList] = iDetach;
                                allowForm = thisDetach.PerformDetach(this, true);
                                thisDetach.Valid = false;
                            }
                        }
                    }
                    if (allowForm)
                        DetachDetails.Remove(StationStops[0].PlatformReference);
                }

                // check if anything needs be detached at formed
                if (DetachDetails.TryGetValue(-1, out List<DetachInfo> detachList))
                {
                    for (int iDetach = detachList.Count - 1; iDetach >= 0; iDetach--)
                    {
                        DetachInfo thisDetach = detachList[iDetach];
                        if (thisDetach.DetachPosition == DetachPositionInfo.End && thisDetach.Valid)
                        {
                            DetachActive[DetachDetailsIndex.DetachDetailsList] = -1;
                            DetachActive[DetachDetailsIndex.DetachActiveList] = iDetach;
                            allowForm = thisDetach.PerformDetach(this, true);
                            thisDetach.Valid = false;
                        }
                    }
                    if (allowForm)
                        DetachDetails.Remove(-1);
                }
            }

            // check if train is still player train
            if (TrainType != TrainType.Player)
            {
                FormTrainFromAI(presentTime);
                stillExist = false;
            }

            // if player train, only form new train if allowed - may be blocked by detach if detach is performed through player detach window
            else if (allowForm)
            {
                // train is terminated and does not form next train - set to static
                if (Forms < 0)
                {
                    ControlMode = TrainControlMode.Inactive;
                    ActivateTime = null;
                    StartTime = null;

                    // train is stored in pool
                    if (!String.IsNullOrEmpty(ExitPool))
                    {
                        TimetablePool thisPool = simulator.PoolHolder.Pools[ExitPool];
                        thisPool.AddUnit(this, false);
                    }

                    MovementState = AiMovementState.Static;
                    return (true);
                }

                // form next train
                TTTrain nextPlayerTrain = null;
                List<int> newTrains = new List<int>();

                bool autogenStart = false;

                // get train which is to be formed
                TTTrain formedTrain = simulator.AI.StartList.GetNotStartedTTTrainByNumber(Forms, true);

                if (formedTrain == null)
                {
                    formedTrain = simulator.GetAutoGenTTTrainByNumber(Forms);
                    autogenStart = true;
                }

                // if found - start train
                if (formedTrain != null)
                {
                    // remove existing train
                    Forms = -1;

                    // remove all existing deadlock path references
                    Simulator.Instance.SignalEnvironment.RemoveDeadlockPathReferences(0);

                    // set details for new train from existing train
                    TrackCircuitSection[] occupiedSections = new TrackCircuitSection[OccupiedTrack.Count];
                    OccupiedTrack.CopyTo(occupiedSections);
                    bool validFormed = formedTrain.StartFromAITrain(this, presentTime, occupiedSections);

                    if (validFormed)
                    {
                        // start new train
                        if (!autogenStart)
                        {
                            simulator.StartReference.Remove(formedTrain.Number);
                        }
                        if (nextTrainNumber < 0)
                        {
                            nextPlayerTrain = formedTrain;
                            nextTrainNumber = formedTrain.Number;
                        }
                        else
                        {
                            formedTrain.SetFormedOccupied();
                            simulator.AI.TrainsToAdd.Add(formedTrain);
                        }
                    }
                    else if (!autogenStart)
                    {
                        // reinstate as to be started (note : train is not yet removed from reference)
                        simulator.AI.StartList.InsertTrain(formedTrain);
                    }
                }

                // set proper player train references

                if (nextTrainNumber > 0)
                {
                    // clear this train - prepare for removal
                    RemoveFromTrack();
                    TrainDeadlockInfo.ClearDeadlocks();
                    simulator.Trains.Remove(this);
                    Number = OrgAINumber;  // reset number
                    stillExist = false;
                    AI.TrainsToRemove.Add(this);

                    // remove formed train from AI list
                    AI.TrainsToRemoveFromAI.Add(formedTrain);

                    // set proper details for new formed train
                    formedTrain.OrgAINumber = nextTrainNumber;
                    formedTrain.Number = 0;
                    AI.TrainsToAdd.Add(formedTrain);
                    AI.TrainListChanged = true;
                    simulator.Trains.Add(formedTrain);

                    formedTrain.SetFormedOccupied();
                    formedTrain.TrainType = TrainType.Player;
                    formedTrain.ControlMode = TrainControlMode.Inactive;
                    formedTrain.MovementState = AiMovementState.Static;

                    // copy train control details
                    formedTrain.MUDirection = MUDirection;
                    formedTrain.MUThrottlePercent = MUThrottlePercent;
                    formedTrain.MUGearboxGearIndex = MUGearboxGearIndex;
                    formedTrain.MUReverserPercent = MUReverserPercent;
                    formedTrain.MUDynamicBrakePercent = MUDynamicBrakePercent;

                    if (TrainType == TrainType.Player)
                    {
                        formedTrain.ConnectBrakeHoses();
                    }
                    else
                    {
                        formedTrain.InitializeBrakes();
                    }

                    // reallocate deadlock path references for new train
                    Simulator.Instance.SignalEnvironment.ReallocateDeadlockPathReferences(nextTrainNumber, 0);

                    bool foundPlayerLocomotive = false;
                    MSTSLocomotive newPlayerLocomotive = null;

                    // search for player locomotive
                    for (int icar = 0; icar < formedTrain.Cars.Count; icar++)
                    {
                        var car = formedTrain.Cars[icar];
                        if (car is MSTSLocomotive locomotive)
                        {
                            if (simulator.PlayerLocomotive == car)
                            {
                                foundPlayerLocomotive = true;
                                formedTrain.LeadLocomotiveIndex = icar;
                                break;
                            }
                            else if (newPlayerLocomotive == null)
                            {
                                newPlayerLocomotive = locomotive;
                                formedTrain.LeadLocomotiveIndex = icar;
                            }
                        }
                    }

                    if (!foundPlayerLocomotive)
                    {
                        simulator.PlayerLocomotive = newPlayerLocomotive;
                        simulator.OnPlayerLocomotiveChanged();
                    }

                    // notify viewer of change in selected train
                    simulator.OnPlayerTrainChanged(this, formedTrain);
                    simulator.PlayerLocomotive.Train = formedTrain;

                    // set up station handling for new train
                    formedTrain.SetupStationStopHandling();

                    if (AtStation && formedTrain.AtStation && StationStops[0].PlatformReference == formedTrain.StationStops[0].PlatformReference)
                    {
                        formedTrain.MovementState = AiMovementState.StationStop;
                        formedTrain.StationStops[0].ActualArrival = StationStops[0].ActualArrival;
                        formedTrain.StationStops[0].ArrivalTime = StationStops[0].ArrivalTime;
                        formedTrain.StationStops[0].CalculateDepartTime(this);
                    }

                    // clear replay commands
                    simulator.Log.CommandList.Clear();

                    // display messages
                    if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                        simulator.Confirmer.Information("Player switched to train : " + formedTrain.Name);
                }
            }

            return (stillExist);
        }

        //================================================================================================//
        /// <summary>
        /// Process speed settings defined in timetable
        /// </summary>

        public void ProcessSpeedSettings()
        {
            SpeedSettings[SpeedValueType.ConsistSpeed] = SpeedSettings[SpeedValueType.ConsistSpeed] == 0 ? SpeedSettings[SpeedValueType.RouteSpeed] : Math.Min(SpeedSettings[SpeedValueType.ConsistSpeed].Value, SpeedSettings[SpeedValueType.RouteSpeed].Value);

            // correct cruise speed if value is incorrect
            if (SpeedSettings[SpeedValueType.MaxSpeed].HasValue && SpeedSettings[SpeedValueType.CruiseSpeed].HasValue &&
                SpeedSettings[SpeedValueType.MaxSpeed] < SpeedSettings[SpeedValueType.CruiseSpeed])
            {
                SpeedSettings[SpeedValueType.CruiseSpeed] = SpeedSettings[SpeedValueType.MaxSpeed];
            }

            // take max of maxspeed and consist speed, or set maxspeed
            if (SpeedSettings[SpeedValueType.MaxSpeed].HasValue)
            {
                SpeedSettings[SpeedValueType.MaxSpeed] = Math.Min(SpeedSettings[SpeedValueType.MaxSpeed].Value, SpeedSettings[SpeedValueType.ConsistSpeed].Value);
                SpeedRestrictionActive = true;
            }
            else
            {
                SpeedSettings[SpeedValueType.MaxSpeed] = SpeedSettings[SpeedValueType.ConsistSpeed];
            }

            // take max of cruisespeed and consist speed
            if (SpeedSettings[SpeedValueType.CruiseSpeed].HasValue)
            {
                SpeedSettings[SpeedValueType.CruiseSpeed] = Math.Min(SpeedSettings[SpeedValueType.CruiseSpeed].Value, SpeedSettings[SpeedValueType.CruiseSpeed].Value);
            }

            // set creep, attach and detach speed if not defined
            if (!SpeedSettings[SpeedValueType.CruiseSpeed].HasValue)
            {
                SpeedSettings[SpeedValueType.CreepSpeed] = PresetCreepSpeed;
            }

            if (!SpeedSettings[SpeedValueType.AttachSpeed].HasValue)
            {
                SpeedSettings[SpeedValueType.AttachSpeed] = PresetCouplingSpeed;
            }

            if (!SpeedSettings[SpeedValueType.DetachSpeed].HasValue)
            {
                SpeedSettings[SpeedValueType.DetachSpeed] = PresetCouplingSpeed;
            }

            if (!SpeedSettings[SpeedValueType.MovingtableSpeed].HasValue)
            {
                SpeedSettings[SpeedValueType.MovingtableSpeed] = PresetMovingtableSpeed;
            }

            TrainMaxSpeedMpS = SpeedSettings[SpeedValueType.MaxSpeed].Value;
        }

        //================================================================================================//
        /// <summary>
        /// Get no. of units which are to be detached
        /// Process detach or transfer command to determine no. of required units
        /// </summary>
        /// <param name="detachUnits"></param>
        /// <param name="numberOfUnits"></param>
        /// <param name="detachConsist"></param>
        /// <param name="frontpos"></param>
        /// <returns></returns>
        public int GetUnitsToDetach(TransferUnits detachUnits, int numberOfUnits, List<string> detachConsist, ref bool frontpos)
        {
            int iunits = 0;
            var thisCar = Cars[0];

            switch (detachUnits)
            {
                case TransferUnits.LeadingPower:
                    bool checktender = false;
                    bool checkengine = false;

                    // check first unit
                    thisCar = Cars[0];
                    if (thisCar.WagonType == WagonType.Engine)
                    {
                        iunits++;
                        checktender = true;
                    }
                    else if (thisCar.WagonType == WagonType.Tender)
                    {
                        iunits++;
                        checkengine = true;
                    }

                    int nextunit = 1;
                    while (checktender && nextunit < Cars.Count)
                    {
                        thisCar = Cars[nextunit];
                        if (thisCar.WagonType == WagonType.Tender)
                        {
                            iunits++;
                            nextunit++;
                        }
                        else
                        {
                            checktender = false;
                        }
                    }

                    while (checkengine && nextunit < Cars.Count)
                    {
                        thisCar = Cars[nextunit];
                        if (thisCar.WagonType == WagonType.Tender)
                        {
                            iunits++;
                            nextunit++;
                        }
                        else if (thisCar.WagonType == WagonType.Engine)
                        {
                            iunits++;
                            checkengine = false;
                        }
                        else
                        {
                            checkengine = false;
                        }
                    }
                    break;

                case TransferUnits.AllLeadingPower:
                    for (int iCar = 0; iCar < Cars.Count; iCar++)
                    {
                        thisCar = Cars[iCar];
                        if (thisCar.WagonType == WagonType.Engine || thisCar.WagonType == WagonType.Tender)
                        {
                            iunits++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    break;

                case TransferUnits.TrailingPower:
                    checktender = false;
                    checkengine = false;
                    frontpos = false;

                    // check first unit
                    thisCar = Cars[Cars.Count - 1];
                    if (thisCar.WagonType == WagonType.Engine)
                    {
                        iunits++;
                        checktender = true;
                    }
                    else if (thisCar.WagonType == WagonType.Tender)
                    {
                        iunits++;
                        checkengine = true;
                    }

                    nextunit = Cars.Count - 2;
                    while (checktender && nextunit >= 0)
                    {
                        thisCar = Cars[nextunit];
                        if (thisCar.WagonType == WagonType.Tender)
                        {
                            iunits++;
                            nextunit--;
                        }
                        else
                        {
                            checktender = false;
                        }
                    }

                    while (checkengine && nextunit >= 0)
                    {
                        thisCar = Cars[nextunit];
                        if (thisCar.WagonType == WagonType.Tender)
                        {
                            iunits++;
                            nextunit--;
                        }
                        else if (thisCar.WagonType == WagonType.Engine)
                        {
                            iunits++;
                            checkengine = false;
                        }
                        else
                        {
                            checkengine = false;
                        }
                    }
                    break;

                case TransferUnits.AllTrailingPower:
                    frontpos = false;

                    for (int iCar = Cars.Count - 1; iCar >= 0; iCar--)
                    {
                        thisCar = Cars[iCar];
                        if (thisCar.WagonType == WagonType.Engine || thisCar.WagonType == WagonType.Tender)
                        {
                            iunits++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    break;

                case TransferUnits.NonPower:

                    int frontunits = 0;
                    // power is at front
                    if (Cars[0].WagonType == WagonType.Engine || Cars[0].WagonType == WagonType.Tender)
                    {
                        frontpos = false;
                        nextunit = 0;
                        thisCar = Cars[nextunit];

                        while ((thisCar.WagonType == WagonType.Engine || thisCar.WagonType == WagonType.Tender) && nextunit < Cars.Count)
                        {
                            frontunits++;
                            nextunit++;
                            if (nextunit < Cars.Count)
                            {
                                thisCar = Cars[nextunit];
                            }
                        }
                        iunits = Cars.Count - frontunits;
                    }
                    // power is at rear
                    else
                    {
                        frontpos = true;
                        nextunit = Cars.Count - 1;
                        thisCar = Cars[nextunit];

                        while ((thisCar.WagonType == WagonType.Engine || thisCar.WagonType == WagonType.Tender) && nextunit >= 0)
                        {
                            frontunits++;
                            nextunit--;
                            if (nextunit >= 0)
                            {
                                thisCar = Cars[nextunit];
                            }
                        }
                        iunits = Cars.Count - frontunits;
                    }
                    break;

                case TransferUnits.Consists:
                    bool inConsist = false;

                    // check if front must be detached
                    if (detachConsist.Contains(Cars[0].OriginalConsist))
                    {
                        inConsist = true;
                        frontpos = true;
                        nextunit = 1;
                        iunits = 1;

                        while (nextunit < Cars.Count && inConsist)
                        {
                            if (detachConsist.Contains(Cars[nextunit].OriginalConsist))
                            {
                                iunits++;
                                nextunit++;
                            }
                            else
                            {
                                inConsist = false;
                            }
                        }
                    }
                    else if (detachConsist.Contains(Cars[Cars.Count - 1].OriginalConsist))
                    {
                        inConsist = true;
                        frontpos = false;
                        nextunit = Cars.Count - 2;
                        iunits = 1;

                        while (nextunit >= 0 && inConsist)
                        {
                            if (detachConsist.Contains(Cars[nextunit].OriginalConsist))
                            {
                                iunits++;
                                nextunit--;
                            }
                            else
                            {
                                inConsist = false;
                            }
                        }
                    }
                    break;

                default:
                    iunits = numberOfUnits;
                    if (iunits > Cars.Count - 1)
                    {
                        Trace.TraceInformation("Train {0} : no. of units to detach ({1}) : value too large, only {2} units on train\n", Name, iunits, Cars.Count);
                        iunits = Cars.Count - 1;
                    }
                    frontpos = detachUnits == TransferUnits.UnitsAtFront;
                    break;
            }

            return (iunits);
        }

        //================================================================================================//
        /// <summary>
        /// Couple trains
        /// </summary>
        /// <param name="attachTrain"></param>
        /// <param name="thisTrainFront"></param>
        /// <param name="attachTrainFront"></param>
        public void TTCouple(TTTrain attachTrain, bool thisTrainFront, bool attachTrainFront)
        {
            // stop train
            SpeedMpS = 0;
            foreach (var car in Cars)
            {
                car.SpeedMpS = 0;
            }

            if (TrainType != TrainType.Player)
                AdjustControlsThrottleOff();
            PhysicsUpdate(0);

            // stop attach train
            attachTrain.SpeedMpS = 0;
            foreach (var car in attachTrain.Cars)
            {
                car.SpeedMpS = 0;
            }

            if (attachTrain.TrainType != TrainType.Player)
                attachTrain.AdjustControlsThrottleOff();
            attachTrain.PhysicsUpdate(0);

            // check on reverse formation
            if (thisTrainFront == attachTrainFront)
            {
                ReverseFormation(TrainType == TrainType.Player);
            }

            var attachCar = Cars[0];

            int playerLocomotiveIndex = -1;
            if (TrainType == TrainType.Player || TrainType == TrainType.PlayerIntended)
            {
                playerLocomotiveIndex = LeadLocomotiveIndex;
            }
            else if (attachTrain.TrainType == TrainType.Player || attachTrain.TrainType == TrainType.PlayerIntended)
            {
                playerLocomotiveIndex = attachTrain.LeadLocomotiveIndex;
            }

            // attach to front of waiting train
            if (attachTrainFront)
            {
                attachCar = Cars[Cars.Count - 1];
                for (int iCar = Cars.Count - 1; iCar >= 0; iCar--)
                {
                    var car = Cars[iCar];
                    car.Train = attachTrain;
                    attachTrain.Cars.Insert(0, car);
                    if (attachTrain.TrainType == TrainType.Player)
                        playerLocomotiveIndex++;
                }
            }
            // attach to rear of waiting train
            else
            {
                if (TrainType == TrainType.Player)
                    playerLocomotiveIndex += attachTrain.Cars.Count;
                foreach (var car in Cars)
                {
                    car.Train = attachTrain;
                    attachTrain.Cars.Add(car);
                }
            }

            // renumber cars
            int carId = 0;
            foreach (var car in attachTrain.Cars)
            {
                car.CarID = $"{attachTrain.Number:0000}_{carId:000}";
                carId++;
            }

            // remove cars from this train
            Cars.Clear();
            attachTrain.Length += Length;
            float distanceTravelledCorrection = 0;

            // recalculate position of formed train
            if (attachTrainFront)  // coupled to front, so rear position is still valid
            {
                attachTrain.CalculatePositionOfCars();
                attachTrain.DistanceTravelledM += Length;
                distanceTravelledCorrection = Length;
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

            // set new track sections occupied
            TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(attachTrain, attachTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex,
                attachTrain.PresentPosition[Direction.Backward].Offset, attachTrain.PresentPosition[Direction.Backward].Direction, attachTrain.Length, true, true, false);

            List<TrackCircuitSection> newOccupiedSections = new List<TrackCircuitSection>();
            foreach (TrackCircuitRouteElement thisElement in tempRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                if (!attachTrain.OccupiedTrack.Contains(thisSection))
                {
                    newOccupiedSections.Add(thisSection);
                }
            }

            // first reserve to ensure all switches are properly alligned
            foreach (TrackCircuitSection newSection in newOccupiedSections)
            {
                newSection.Reserve(attachTrain.RoutedForward, tempRoute);
            }

            // next set occupied
            foreach (TrackCircuitSection newSection in newOccupiedSections)
            {
                newSection.SetOccupied(attachTrain.RoutedForward);
            }

            // reset OccupiedTrack to ensure it is set in correct sequence
            attachTrain.OccupiedTrack.Clear();
            foreach (TrackCircuitRouteElement thisElement in tempRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                attachTrain.OccupiedTrack.Add(thisSection);
            }

            // set various items
            attachTrain.CheckFreight();
            attachTrain.SetDistributedPowerUnitIds();
            attachTrain.ReinitializeEOT();
            attachCar.SignalEvent(TrainEvent.Couple);
            attachTrain.ProcessSpeedSettings();
            // adjust set actions for updated distance travelled value
            if (distanceTravelledCorrection > 0)
            {
                attachTrain.RequiredActions.ModifyRequiredDistance(distanceTravelledCorrection);
            }
            // if not static, reassess signals if coupled at front (no need to reassess signals if coupled to rear)
            // also, reset movement state if not player train
            if (attachTrain.MovementState != AiMovementState.Static)
            {
                if (attachTrainFront)
                {
                    attachTrain.InitializeSignals(true);
                }

                if (attachTrain.TrainType != TrainType.Player && attachTrain.TrainType != TrainType.PlayerIntended)
                {
                    attachTrain.MovementState = AiMovementState.Stopped;
                    AiActionType attachTrainAction = AiActionType.None;
                    if (attachTrain.nextActionInfo != null)
                    {
                        attachTrainAction = attachTrain.nextActionInfo.NextAction;
                    }
                    attachTrain.ResetActions(true, false);

                    // check if stopped in station - either this train or the attach train
                    if (AtStation && attachTrain.StationStops.Count > 0)
                    {
                        if (StationStops[0].PlatformReference == attachTrain.StationStops[0].PlatformReference)
                        {
                            attachTrain.MovementState = AiMovementState.StationStop;
                            if (attachTrain.nextActionInfo != null && attachTrain.nextActionInfo.NextAction == AiActionType.StationStop)
                            {
                                attachTrain.nextActionInfo = null;
                            }
                        }
                    }
                    else if (attachTrain.AtStation)
                    {
                        attachTrain.MovementState = AiMovementState.StationStop;
                    }
                    else if (attachTrainAction == AiActionType.StationStop)
                    {
                        if (attachTrain.StationStops[0].SubrouteIndex == attachTrain.TCRoute.ActiveSubPath &&
                           attachTrain.ValidRoutes[Direction.Forward].GetRouteIndex(attachTrain.StationStops[0].TrackCircuitSectionIndex, attachTrain.PresentPosition[Direction.Forward].RouteListIndex) <= attachTrain.PresentPosition[Direction.Forward].RouteListIndex)
                        // assume to be in station
                        // also set state of present train to station stop
                        {
                            MovementState = attachTrain.MovementState = AiMovementState.StationStop;
                            attachTrain.AtStation = true;
                        }
                    }
                }
            }
            else
            {
                attachTrain.DistanceTravelledM = 0;
            }

            // check for needattach in attached train
            // if stopped at station use platform reference

            bool needAttachFound = false;
            if (AtStation)
            {
                int stationPlatformIndex = StationStops[0].PlatformReference;
                if (attachTrain.NeedAttach.TryGetValue(stationPlatformIndex, out List<int> trainList))
                {
                    if (trainList.Contains(OrgAINumber))
                    {
                        needAttachFound = true;
                        trainList.Remove(OrgAINumber);
                        if (trainList.Count < 1)
                        {
                            attachTrain.NeedAttach.Remove(stationPlatformIndex);
                        }
                    }
                }
            }
            // else search through all entries

            if (!needAttachFound && attachTrain.NeedAttach != null && attachTrain.NeedAttach.Count > 0)
            {
                int? indexRemove = null;
                foreach (KeyValuePair<int, List<int>> thisNeedAttach in attachTrain.NeedAttach)
                {
                    int foundKey = thisNeedAttach.Key;
                    List<int> trainList = thisNeedAttach.Value;
                    if (trainList.Remove(OrgAINumber))
                        needAttachFound = true;

                    if (trainList.Count < 1)
                    {
                        indexRemove = foundKey;
                    }
                }

                if (indexRemove.HasValue)
                    attachTrain.NeedAttach.Remove(indexRemove.Value);
            }

            // if train is player or intended player and train has no player engine, determine new loco lead index
            if (attachTrain.TrainType == TrainType.Player || attachTrain.TrainType == TrainType.PlayerIntended)
            {
                if (simulator.Confirmer != null)
                    simulator.Confirmer.Information("Train " + Name + " has attached");
                Trace.TraceInformation("Train " + Name + " has attached to player train");

                if (attachTrain.LeadLocomotive == null)
                {
                    attachTrain.LeadLocomotive = simulator.PlayerLocomotive = attachTrain.Cars[0] as MSTSLocomotive ?? attachTrain.Cars[^1] as MSTSLocomotive ?? attachTrain.Cars.OfType<MSTSLocomotive>().FirstOrDefault();
                }
                else
                {
                    // reassign leadlocomotive to reset index
                    attachTrain.LeadLocomotive = simulator.PlayerLocomotive = attachTrain.Cars[playerLocomotiveIndex] as MSTSLocomotive;
                    attachTrain.LeadLocomotiveIndex = playerLocomotiveIndex;
                }

                // if not in preupdate there must be an engine
                if (simulator.PlayerLocomotive == null && !simulator.PreUpdate)
                {
                    throw new InvalidDataException("Can't find player locomotive in " + attachTrain.Name);
                }
            }
            // if attaching train is player : switch trains and set new engine index
            else if (TrainType == TrainType.Player)
            {
                // prepare to remove old train
                Number = OrgAINumber;
                attachTrain.OrgAINumber = attachTrain.Number;
                attachTrain.Number = 0;

                RemoveTrain();
                simulator.Trains.Remove(this);

                // reassign leadlocomotive to reset index
                attachTrain.LeadLocomotiveIndex = playerLocomotiveIndex;
                attachTrain.LeadLocomotive = simulator.PlayerLocomotive = attachTrain.Cars[playerLocomotiveIndex] as MSTSLocomotive;

                // correctly insert new player train
                attachTrain.AI.TrainsToRemoveFromAI.Add(attachTrain);
                simulator.Trains.Remove(attachTrain);
                attachTrain.AI.TrainsToAdd.Add(attachTrain);
                attachTrain.AI.TrainListChanged = true;
                simulator.Trains.Add(attachTrain);

                attachTrain.SetFormedOccupied();
                attachTrain.TrainType = TrainType.Player;

                // if present movement state is active state, copy to new train
                if (MovementState != AiMovementState.Static)
                {
                    attachTrain.MovementState = MovementState;
                }

                // inform viewer about player train switch
                simulator.OnPlayerTrainChanged(this, attachTrain);
                simulator.PlayerLocomotive.Train = attachTrain;

                attachTrain.SetupStationStopHandling();

                if (simulator.Confirmer != null)
                {
                    simulator.Confirmer.Information("Train attached to " + attachTrain.Name);
                    simulator.Confirmer.Information("Train continues as " + attachTrain.Name);
                }

            }
            // set anti-slip for all engines in AI train
            else
            {
                foreach (TrainCar car in attachTrain.Cars)
                {
                    if (car.WagonType == WagonType.Engine)
                    {
                        MSTSLocomotive loco = car as MSTSLocomotive;
                        loco.AntiSlip = attachTrain.LeadLocoAntiSlip;
                    }
                }
            }

            // remove original train
            RemoveTrain();

            // stop the wheels from moving etc
            attachTrain.PhysicsUpdate(0);

            // initialize brakes on resulting train except when both trains are player trains
            if (attachTrain.TrainType == TrainType.Player)
            {
                attachTrain.ConnectBrakeHoses();
            }
            else
            {
                attachTrain.InitializeBrakes();
            }

            // update route positions if required
            int trainRearPositionIndex = attachTrain.ValidRoutes[Direction.Forward].GetRouteIndex(tempRoute.First().TrackCircuitSection.Index, 0);
            int trainFrontPositionIndex = attachTrain.ValidRoutes[Direction.Forward].GetRouteIndex(tempRoute.Last().TrackCircuitSection.Index, 0);

            if (trainRearPositionIndex < 0 || trainFrontPositionIndex < 0)
            {
                attachTrain.AdjustTrainRouteOnStart(trainRearPositionIndex, trainFrontPositionIndex, this);
            }

            // recalculate station stop positions
            attachTrain.RecalculateStationStops();

            // if normal stop, set restart delay
            if (!AtStation && !attachTrain.AtStation)
            {
                RestdelayS = DelayedStartSettings[DelayedStartType.AttachRestart].RemainingDelay();
                DelayStart = true;
                DelayedStartState = AiStartMovement.PathAction;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Uncouple required units to form pre-defined train
        /// Uncouple performed in detach or transfer commands
        /// </summary>
        /// <param name="newTrain"></param>
        /// <param name="reverseTrain"></param>
        /// <param name="leadLocomotiveIndex"></param>
        /// <param name="newIsPlayer"></param>
        /// <returns></returns>
        public int TTUncoupleBehind(TTTrain newTrain, bool reverseTrain, int leadLocomotiveIndex, bool newIsPlayer)
        {
            // if front portion : move req units to new train and remove from old train
            // remove from rear to front otherwise they cannot be deleted

            int newLeadLocomotiveIndex = leadLocomotiveIndex;
            bool leadLocomotiveInNewTrain = false;
            var detachCar = Cars[DetachUnits];

            // detach from front
            if (DetachPosition)
            {
                detachCar = Cars[DetachUnits];
                newTrain.Cars.Clear();  // remove any cars on new train

                for (int iCar = 0; iCar <= DetachUnits - 1; iCar++)
                {
                    var car = Cars[0]; // each car is removed so always detach first car!!!
                    Cars.Remove(car);
                    Length = -car.CarLengthM;
                    newTrain.Cars.Add(car); // place in rear
                    car.Train = newTrain;
                    car.CarID = $"{newTrain.Number:0000}_{(newTrain.Cars.Count - 1):0000}";
                    newTrain.Length += car.CarLengthM;
                    leadLocomotiveInNewTrain = leadLocomotiveInNewTrain || iCar == LeadLocomotiveIndex; // if detached car is leadlocomotive, the locomotive is in the new train
                }
                // if lead locomotive is beyond detach unit, update index
                if (leadLocomotiveIndex >= DetachUnits)
                {
                    newLeadLocomotiveIndex = leadLocomotiveIndex - DetachUnits;
                }
                // if new train is player but engine is not in new train, reset Simulator.Playerlocomotive
                else if (newIsPlayer && !leadLocomotiveInNewTrain)
                {
                    simulator.PlayerLocomotive = null;
                }

            }
            // detach from rear
            else
            {
                int detachUnitsFromFront = Cars.Count - DetachUnits;
                detachCar = Cars[Cars.Count - DetachUnits];
                int totalCars = Cars.Count;
                newTrain.Cars.Clear();  // remove any cars on new train

                for (int iCar = 0; iCar <= DetachUnits - 1; iCar++)
                {
                    var car = Cars[totalCars - 1 - iCar]; // total cars is original length which keeps value despite cars are removed
                    Cars.Remove(car);
                    Length -= car.CarLengthM;
                    newTrain.Cars.Insert(0, car); // place in front
                    car.Train = newTrain;
                    car.CarID = $"{newTrain.Number:0000}_{(DetachUnits - newTrain.Cars.Count):0000}";
                    newTrain.Length += car.CarLengthM;
                    leadLocomotiveInNewTrain = leadLocomotiveInNewTrain || (totalCars - 1 - iCar) == LeadLocomotiveIndex;
                }
                // if lead locomotive is beyond detach unit, update index
                if (leadLocomotiveIndex >= detachUnitsFromFront)
                {
                    newLeadLocomotiveIndex = leadLocomotiveIndex - detachUnitsFromFront;
                }
                else if (newIsPlayer && !leadLocomotiveInNewTrain)
                {
                    simulator.PlayerLocomotive = null;
                }

            }

            // and fix up the travellers
            if (DetachPosition)
            {
                CalculatePositionOfCars();
                newTrain.RearTDBTraveller = new Traveller(FrontTDBTraveller);
                newTrain.CalculatePositionOfCars();
            }
            else
            {
                newTrain.RearTDBTraveller = new Traveller(RearTDBTraveller);
                newTrain.CalculatePositionOfCars();
                RepositionRearTraveller();    // fix the rear traveller
            }

            LastCar.CouplerSlackM = 0;

            newTrain.SpeedMpS = SpeedMpS = 0;
            newTrain.TrainMaxSpeedMpS = TrainMaxSpeedMpS;
            newTrain.AITrainBrakePercent = AITrainBrakePercent;
            newTrain.AITrainDirectionForward = true;

            // disconnect brake hose and close angle cocks
            if (DetachPosition)
            {
                Cars[0].BrakeSystem.FrontBrakeHoseConnected = false;
                Cars[0].BrakeSystem.AngleCockAOpen = false;
                newTrain.Cars[newTrain.Cars.Count - 1].BrakeSystem.AngleCockBOpen = false;
            }
            else
            {
                newTrain.Cars[0].BrakeSystem.FrontBrakeHoseConnected = false;
                newTrain.Cars[0].BrakeSystem.AngleCockAOpen = false;
                Cars[Cars.Count - 1].BrakeSystem.AngleCockBOpen = false;
            }

            // reverse new train if required
            if (reverseTrain)
            {
                newTrain.ReverseFormation(false);
                if (leadLocomotiveInNewTrain)
                {
                    newLeadLocomotiveIndex = newTrain.Cars.Count - newLeadLocomotiveIndex - 1;
                }
            }

            // check freight for both trains
            CheckFreight();
            SetDistributedPowerUnitIds();
            ReinitializeEOT();
            newTrain.CheckFreight();
            newTrain.SetDistributedPowerUnitIds();
            newTrain.ReinitializeEOT();

            // check speed values for both trains
            ProcessSpeedSettings();
            newTrain.ProcessSpeedSettings();

            // set states
            newTrain.MovementState = AiMovementState.Static; // start of as AI static
            newTrain.StartTime = null; // time will be set later

            // set delay
            RestdelayS = DelayedStartSettings[DelayedStartType.DetachRestart].RemainingDelay();
            DelayStart = true;
            DelayedStartState = AiStartMovement.NewTrain;

            if (!newIsPlayer)
            {
                newTrain.TrainType = TrainType.Ai;
                newTrain.ControlMode = TrainControlMode.Inactive;
                newTrain.AI.TrainsToAdd.Add(newTrain);
            }

            // signal event
            detachCar.SignalEvent(TrainEvent.Uncouple);

            // update positions train
            TrackNode tn = FrontTDBTraveller.TrackNode;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)FrontTDBTraveller.Direction.Reverse();

            PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PresentPosition[Direction.Forward].RouteListIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            if (DetachPosition)
            {
                DistanceTravelledM -= newTrain.Length;
            }

            tn = RearTDBTraveller.TrackNode;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)RearTDBTraveller.Direction.Reverse();

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PresentPosition[Direction.Backward].RouteListIndex = ValidRoutes[Direction.Forward].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);

            // get new track sections occupied
            TrackCircuitPartialPathRoute tempRouteTrain = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex,
                PresentPosition[Direction.Backward].Offset, PresentPosition[Direction.Backward].Direction, Length, false, true, false);

            // if detached from front, clear train from track and all further sections
            // set train occupied for new sections
            if (DetachPosition)
            {
                RemoveFromTrack();

                // first reserve all sections to ensure switched are alligned, next set occupied
                for (int iIndex = 0; iIndex < tempRouteTrain.Count; iIndex++)
                {
                    TrackCircuitSection thisSection = tempRouteTrain[iIndex].TrackCircuitSection;
                    thisSection.Reserve(RoutedForward, tempRouteTrain);
                }
                for (int iIndex = 0; iIndex < tempRouteTrain.Count; iIndex++)
                {
                    TrackCircuitSection thisSection = tempRouteTrain[iIndex].TrackCircuitSection;
                    thisSection.SetOccupied(RoutedForward);
                }

                if (ControlMode == TrainControlMode.AutoSignal)
                    ControlMode = TrainControlMode.AutoNode;  // set to node control as detached portion is in front
                NextSignalObjects[Direction.Forward] = null; // reset signal object (signal is not directly in front)
            }

            // remove train from track which it no longer occupies and clear actions for those sections
            else
            {
                RemoveFromTrackNotOccupied(tempRouteTrain);
            }

            // update positions new train
            tn = newTrain.FrontTDBTraveller.TrackNode;
            offset = newTrain.FrontTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)newTrain.FrontTDBTraveller.Direction.Reverse();

            newTrain.PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            newTrain.PresentPosition[Direction.Forward].RouteListIndex = newTrain.ValidRoutes[Direction.Forward].GetRouteIndex(newTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            newTrain.PreviousPosition[Direction.Forward].UpdateFrom(newTrain.PresentPosition[Direction.Forward]);

            newTrain.DistanceTravelledM = 0.0f;

            tn = newTrain.RearTDBTraveller.TrackNode;
            offset = newTrain.RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)newTrain.RearTDBTraveller.Direction.Reverse();

            newTrain.PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            newTrain.PresentPosition[Direction.Backward].RouteListIndex = newTrain.ValidRoutes[Direction.Forward].GetRouteIndex(newTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
            newTrain.PreviousPosition[Direction.Backward].UpdateFrom(newTrain.PresentPosition[Direction.Backward]);

            // check if train is on valid path
            if (newTrain.PresentPosition[Direction.Forward].RouteListIndex < 0 && newTrain.PresentPosition[Direction.Backward].RouteListIndex < 0)
            {
                Trace.TraceInformation("Train : {0} ({1}) : detached from {2} ({3}) : is not on valid path", newTrain.Name, newTrain.Number, Name, Number);
                newTrain.ValidRoutes[Direction.Forward].Clear();
                newTrain.ValidRoutes[Direction.Forward] = null;
            }
            else
            {
                // ensure new trains route extends fully underneath train
                AdjustTrainRouteOnStart(newTrain.PresentPosition[Direction.Forward].RouteListIndex, newTrain.PresentPosition[Direction.Backward].RouteListIndex, this);
            }

            // build temp route for new train
            TrackCircuitPartialPathRoute tempRouteNewTrain = SignalEnvironment.BuildTempRoute(newTrain, newTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex,
                newTrain.PresentPosition[Direction.Backward].Offset, newTrain.PresentPosition[Direction.Backward].Direction, newTrain.Length, false, true, false);

            // if train has no valid route, create from occupied sections
            if (newTrain.ValidRoutes[Direction.Forward] == null)
            {
                newTrain.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(tempRouteNewTrain);
                newTrain.TCRoute.TCRouteSubpaths.Add(new TrackCircuitPartialPathRoute(tempRouteNewTrain));
                newTrain.PresentPosition[Direction.Forward].RouteListIndex = newTrain.ValidRoutes[Direction.Forward].GetRouteIndex(newTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                newTrain.PreviousPosition[Direction.Forward].UpdateFrom(newTrain.PresentPosition[Direction.Forward]);
                newTrain.PresentPosition[Direction.Backward].RouteListIndex = newTrain.ValidRoutes[Direction.Forward].GetRouteIndex(newTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
                newTrain.PreviousPosition[Direction.Backward].UpdateFrom(newTrain.PresentPosition[Direction.Backward]);
            }

            // set track section reserved - first reserve to ensure correct alignment of switches
            for (int iIndex = 0; iIndex < tempRouteNewTrain.Count; iIndex++)
            {
                TrackCircuitSection thisSection = tempRouteNewTrain[iIndex].TrackCircuitSection;
                thisSection.Reserve(newTrain.RoutedForward, tempRouteNewTrain);
            }

            // set track section occupied
            for (int iIndex = 0; iIndex < tempRouteNewTrain.Count; iIndex++)
            {
                TrackCircuitSection thisSection = tempRouteNewTrain[iIndex].TrackCircuitSection;
                thisSection.SetOccupied(newTrain.RoutedForward);
            }

            // update station stop offsets for continuing train
            RecalculateStationStops();

            // if normal stop, set restart delay
            if (MovementState == AiMovementState.Stopped)
            {
                RestdelayS = DelayedStartSettings[DelayedStartType.DetachRestart].RemainingDelay();
                DelayStart = true;
                DelayedStartState = AiStartMovement.PathAction;
            }

            // return new lead locomotive position
            return (newLeadLocomotiveIndex);
        }

        //================================================================================================//
        /// <summary>
        /// Check if other train must be activated through trigger 
        /// </summary>
        /// <param name="thisTriggerType"></param>
        /// <param name="reqPlatformID"></param>
        /// <returns></returns>
        public void ActivateTriggeredTrain(TriggerActivationType thisTriggerType, int reqPlatformID)
        {
            for (int itrigger = ActivatedTrainTriggers.Count - 1; itrigger >= 0; itrigger--)
            {
                TriggerActivation thisTrigger = ActivatedTrainTriggers[itrigger];
                if (thisTrigger.ActivationType == thisTriggerType)
                {
                    if (thisTriggerType == TriggerActivationType.StationDepart || thisTriggerType == TriggerActivationType.StationStop)
                    {
                        if (thisTrigger.PlatformId != reqPlatformID)
                        {
                            continue;
                        }
                    }

                    TTTrain triggeredTrain = GetOtherTTTrainByNumber(thisTrigger.ActivatedTrain);
                    if (triggeredTrain == null)
                    {
                        triggeredTrain = AI.StartList.GetNotStartedTTTrainByNumber(thisTrigger.ActivatedTrain, false);
                    }

                    if (triggeredTrain != null)
                    {
                        triggeredTrain.TriggeredActivationRequired = false;
                    }
                    else
                    {
                        Trace.TraceInformation("Train to trigger : {0} not found for train {1}", thisTrigger.ActivatedTrainName, Name);
                    }

                    ActivatedTrainTriggers.RemoveAt(itrigger);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Adjust train route om start of train
        /// Front or rear may be off route as result of only partial overlap of old and new routes
        /// Can occur on form or couple/uncouple actions
        /// </summary>
        /// <param name="trainRearPositionIndex"></param>
        /// <param name="trainFrontPositionIndex"></param>
        /// <param name="oldTrain"></param>
        /// <returns></returns>
        public int AdjustTrainRouteOnStart(int trainRearPositionIndex, int trainFrontPositionIndex, TTTrain oldTrain)
        {
            int addedSections = 0;

            TrackCircuitSection[] occupiedSections = new TrackCircuitSection[OccupiedTrack.Count];
            OccupiedTrack.CopyTo(occupiedSections);

            // check if train is occupying end of route (happens when attaching) or front of route (happens when forming)
            bool addFront = false;

            int firstSectionIndex = ValidRoutes[Direction.Forward][0].TrackCircuitSection.Index;
            foreach (TrackCircuitSection thisSection in OccupiedTrack)
            {
                if (thisSection.Index == firstSectionIndex)
                {
                    addFront = true;
                    break;
                }
            }

            // if start position not on route, add sections to route to cover
            if (trainRearPositionIndex < 0)
            {
                // add to front
                if (addFront)
                {
                    //ensure first section in route is occupied, otherwise do not add sections
                    int firstIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][0].TrackCircuitSection.Index;
                    bool firstoccupied = false;
                    for (int iSection = occupiedSections.Length - 1; iSection >= 0 && !firstoccupied; iSection--)
                    {
                        TrackCircuitSection thisSection = occupiedSections[iSection];
                        firstoccupied = (thisSection.Index == firstIndex);
                    }

                    // create route for occupied sections if position is available

                    TrackCircuitPartialPathRoute tempRoute = null;
                    if (PresentPosition[Direction.Backward].TrackCircuitSectionIndex >= 0)
                    {
                        tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                            PresentPosition[Direction.Backward].Direction, Length, true, true, false);
                    }

                    // add if first section is occupied
                    for (int iSection = occupiedSections.Length - 1; iSection >= 0 && firstoccupied; iSection--)
                    {
                        TrackCircuitSection thisSection = occupiedSections[iSection];
                        int routeIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].GetRouteIndex(thisSection.Index, 0);

                        if (routeIndex < 0)
                        {
                            TrackCircuitRouteElement newElement = null;

                            // try to use element from old route
                            int otherTrainRouteIndex = oldTrain.ValidRoutes[Direction.Forward].GetRouteIndex(thisSection.Index, 0);
                            if (otherTrainRouteIndex >= 0)
                            {
                                newElement = new TrackCircuitRouteElement(oldTrain.ValidRoutes[Direction.Forward][otherTrainRouteIndex]);
                            }
                            // if failed and temp route available, try to use from temp route
                            else if (tempRoute != null)
                            {
                                otherTrainRouteIndex = tempRoute.GetRouteIndex(thisSection.Index, 0);
                                {

                                    if (otherTrainRouteIndex >= 0)
                                    {
                                        newElement = new TrackCircuitRouteElement(tempRoute[otherTrainRouteIndex]);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }

                            TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Insert(0, newElement);
                            addedSections++;
                        }
                    }
                }
                // add to rear
                else
                {
                    //ensure last section in route is occupied, otherwise do not add sections
                    int lastIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Last().TrackCircuitSection.Index;
                    bool lastoccupied = false;
                    for (int iSection = occupiedSections.Length - 1; iSection >= 0 && !lastoccupied; iSection--)
                    {
                        TrackCircuitSection thisSection = occupiedSections[iSection];
                        lastoccupied = (thisSection.Index == lastIndex);
                    }

                    // create route for occupied sections if position is available
                    TrackCircuitPartialPathRoute tempRoute = null;
                    if (PresentPosition[Direction.Backward].TrackCircuitSectionIndex >= 0)
                    {
                        tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                            PresentPosition[Direction.Backward].Direction, Length, true, true, false);
                    }

                    // add if last section is occupied
                    for (int iSection = 0; iSection < occupiedSections.Length && lastoccupied; iSection++)
                    {
                        TrackCircuitSection thisSection = occupiedSections[iSection];
                        int routeIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].GetRouteIndex(thisSection.Index, 0);
                        if (routeIndex < 0)
                        {
                            TrackCircuitRouteElement newElement = null;

                            // first try to add from old route
                            int otherTrainRouteIndex = oldTrain.ValidRoutes[Direction.Forward].GetRouteIndex(thisSection.Index, 0);
                            if (otherTrainRouteIndex >= 0)
                            {
                                newElement = new TrackCircuitRouteElement(oldTrain.ValidRoutes[Direction.Forward][otherTrainRouteIndex]);
                            }
                            // if failed try from temp route if available
                            else if (tempRoute != null)
                            {
                                otherTrainRouteIndex = tempRoute.GetRouteIndex(thisSection.Index, 0);
                                {

                                    if (otherTrainRouteIndex >= 0)
                                    {
                                        newElement = new TrackCircuitRouteElement(tempRoute[otherTrainRouteIndex]);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }

                            TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Add(newElement);
                            addedSections++;
                        }
                    }
                }

                ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath]);

                // update section index references in TCroute data
                if (TCRoute.ReversalInfo[0].Valid)
                {
                    TCRoute.ReversalInfo[0].FirstDivergeIndex += addedSections;
                    TCRoute.ReversalInfo[0].FirstSignalIndex += addedSections;
                    TCRoute.ReversalInfo[0].LastDivergeIndex += addedSections;
                    TCRoute.ReversalInfo[0].LastSignalIndex += addedSections;
                }

                // update station stop indices
                if (StationStops != null)
                {
                    foreach (StationStop thisStation in StationStops)
                    {
                        if (thisStation.SubrouteIndex == 0)
                        {
                            thisStation.RouteIndex += addedSections;
                        }
                    }
                }
            }

            // if end position not on route, add sections to route to cover
            else if (trainFrontPositionIndex < 0)
            {
                if (addFront)
                {
                    //ensure first section in route is occupied, otherwise do not add sections
                    int firstIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][0].TrackCircuitSection.Index;
                    bool firstoccupied = false;
                    for (int iSection = occupiedSections.Length - 1; iSection >= 0 && !firstoccupied; iSection--)
                    {
                        TrackCircuitSection thisSection = occupiedSections[iSection];
                        firstoccupied = (thisSection.Index == firstIndex);
                    }

                    for (int iSection = occupiedSections.Length - 1; iSection >= 0 && firstoccupied; iSection--)
                    {
                        TrackCircuitSection thisSection = occupiedSections[iSection];
                        int routeIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].GetRouteIndex(thisSection.Index, 0);
                        if (routeIndex < 0)
                        {
                            int otherTrainRouteIndex = oldTrain.ValidRoutes[Direction.Forward].GetRouteIndex(thisSection.Index, 0);
                            TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(oldTrain.ValidRoutes[Direction.Forward][otherTrainRouteIndex]);
                            TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Insert(0, newElement);
                            addedSections++;
                        }
                    }
                }
                else
                {
                    //ensure last section in route is occupied, otherwise do not add sections
                    int lastIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Last().TrackCircuitSection.Index;
                    bool lastoccupied = false;
                    for (int iSection = occupiedSections.Length - 1; iSection >= 0 && !lastoccupied; iSection--)
                    {
                        TrackCircuitSection thisSection = occupiedSections[iSection];
                        lastoccupied = (thisSection.Index == lastIndex);
                    }

                    for (int iSection = 0; iSection < occupiedSections.Length && lastoccupied; iSection++)
                    {
                        TrackCircuitSection thisSection = occupiedSections[iSection];
                        int routeIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].GetRouteIndex(thisSection.Index, 0);
                        if (routeIndex < 0)
                        {
                            int otherTrainRouteIndex = oldTrain.ValidRoutes[Direction.Forward].GetRouteIndex(thisSection.Index, 0);
                            TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(oldTrain.ValidRoutes[Direction.Forward][otherTrainRouteIndex]);
                            TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Add(newElement);
                            addedSections++;
                        }
                    }
                }

                trainFrontPositionIndex = 0;
                ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath]);

                // update section index references in TCroute data
                if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                {
                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].FirstDivergeIndex += addedSections;
                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].FirstSignalIndex += addedSections;
                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex += addedSections;
                    TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex += addedSections;
                }
                // update station stop indices
                if (StationStops != null)
                {
                    foreach (StationStop thisStation in StationStops)
                    {
                        if (thisStation.SubrouteIndex == 0)
                        {
                            thisStation.RouteIndex += addedSections;
                        }
                    }
                }
            }
            return (addedSections);
        }

        //================================================================================================//
        /// <summary>
        /// Create reference name for static train
        /// </summary>
        /// <param name="train"></param>
        /// <param name="trainlist"></param>
        /// <param name="reqName"></param>
        /// <param name="sectionInfo"></param>
        /// <returns></returns>
        public int CreateStaticTrainRef(TTTrain train, ref List<TTTrain> trainlist, string reqName, int sectionInfo, int seqNo)
        {
            TTTrain formedTrain = new TTTrain(train);
            formedTrain.Name = $"S{train.Number:0000}_{seqNo:00}";
            formedTrain.FormedOf = train.Number;
            formedTrain.FormedOfType = TimetableFormationCommand.Detached;
            formedTrain.TrainType = TrainType.AiAutoGenerated;

            TrackCircuitSection DetachSection = TrackCircuitSection.TrackCircuitList[sectionInfo];
            if (DetachSection.CircuitType == TrackCircuitType.EndOfTrack)
            {
                DetachSection = TrackCircuitSection.TrackCircuitList[DetachSection.Pins[TrackDirection.Ahead, SignalLocation.NearEnd].Link];
            }
            TrackVectorNode detachNode = RuntimeData.Instance.TrackDB.TrackNodes[DetachSection.OriginalIndex] as TrackVectorNode;

            formedTrain.RearTDBTraveller = new Traveller(detachNode);

            trainlist.Add(formedTrain);

            return (formedTrain.Number);
        }

        //================================================================================================//
        /// <summary>
        /// Create static train
        /// </summary>
        /// <param name="train"></param>
        /// <param name="trainList"></param>
        /// <param name="reqName"></param>
        /// <param name="sectionInfo"></param>
        /// <returns></returns>
        public int CreateStaticTrain(TTTrain train, ref List<TTTrain> trainList, string reqName, int sectionInfo)
        {
            TTTrain formedTrain = new TTTrain(train);
            TrackCircuitSection DetachSection = TrackCircuitSection.TrackCircuitList[sectionInfo];
            TrackVectorNode DetachNode = RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes[DetachSection.OriginalIndex];

            formedTrain.RearTDBTraveller = new Traveller(DetachNode);
            formedTrain.PresentPosition[Direction.Forward].UpdateFrom(train.PresentPosition[Direction.Forward]);
            formedTrain.PresentPosition[Direction.Backward].UpdateFrom(train.PresentPosition[Direction.Backward]);
            formedTrain.CreateRoute(true);
            if (formedTrain.TCRoute == null)
            {
                formedTrain.TCRoute = new TrackCircuitRoutePath(formedTrain.ValidRoutes[Direction.Forward]);
            }
            else
            {
                formedTrain.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(formedTrain.TCRoute.TCRouteSubpaths[0]);
            }

            formedTrain.AITrainDirectionForward = true;
            if (string.IsNullOrEmpty(reqName))
            {
                formedTrain.Name = $"D_{train.Number:0000}_{formedTrain.Number:00}";
            }
            else
            {
                formedTrain.Name = reqName;
            }
            formedTrain.FormedOf = train.Number;
            formedTrain.FormedOfType = TimetableFormationCommand.Detached;
            formedTrain.TrainType = TrainType.AiAutoGenerated;
            formedTrain.MovementState = AiMovementState.Static;

            // set starttime to 1 sec, and set activate time to null (train is never activated)
            formedTrain.StartTime = 1;
            formedTrain.ActivateTime = null;
            formedTrain.AI = train.AI;

            trainList.Add(formedTrain);
            return (formedTrain.Number);
        }

        //================================================================================================//
        /// <summary>
        /// Get other train from number
        /// Use Simulator.Trains to get other train
        /// </summary>

        public TTTrain GetOtherTTTrainByNumber(int reqNumber)
        {
            TTTrain returnTrain = simulator.Trains.GetTrainByNumber(reqNumber) as TTTrain;

            // if not found, try if player train has required number as original number
            if (returnTrain == null)
            {
                TTTrain playerTrain = simulator.Trains.GetTrainByNumber(0) as TTTrain;
                if (playerTrain.OrgAINumber == reqNumber)
                {
                    returnTrain = playerTrain;
                }
            }

            return (returnTrain);
        }

        //================================================================================================//
        /// <summary>
        /// Get other train from name
        /// Use Simulator.Trains to get other train
        /// </summary>

        public TTTrain GetOtherTTTrainByName(string reqName)
        {
            return (simulator.Trains.GetTrainByName(reqName) as TTTrain);
        }

        /// <summary>
        /// Check if other train is yet to be started
        /// Use Simulator.Trains to get other train
        /// </summary>
        public bool CheckTTTrainNotStartedByNumber(int reqNumber)
        {
            bool notStarted = false;
            // check if on startlist
            if (simulator.Trains.CheckTrainNotStartedByNumber(reqNumber))
            {
                notStarted = true;
            }
            // check if on autogen list
            else if (simulator.AutoGenDictionary.ContainsKey(reqNumber))
            {
                notStarted = true;
            }
            // check if in process of being started
            else
                foreach (TTTrain thisTrain in AI.TrainsToAdd)
                {
                    if (thisTrain.Number == reqNumber)
                    {
                        notStarted = true;
                        break;
                    }
                }

            return (notStarted);
        }

        private protected override INameValueInformationProvider GetDispatcherInfoProvider() => new TimeTableTrainDispatcherInfo(this);

        private protected class TimeTableTrainDispatcherInfo : AiTrainDispatcherInfo
        {
            private readonly TTTrain train;

            public TimeTableTrainDispatcherInfo(TTTrain train) : base(train)
            {
                this.train = train;
            }

            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    base.Update(gameTime);

                    switch (train.MovementState)
                    {
                        case AiMovementState.Static:
                            this["AiData"] = train.TriggeredActivationRequired ? catalog.GetString("Triggered Activation") :
                                train.ActivateTime.HasValue ? $"{TimeSpan.FromSeconds(train.ActivateTime.Value):c}" : "--------";
                            break;
                    }
                }
            }
        }
    }
}

