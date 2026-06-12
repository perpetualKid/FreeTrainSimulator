using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.Multiplayer;

namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    ///
    /// class SignalHead
    ///
    //================================================================================================//

    public class SignalHead
    {
        private SignalScripts.SCRScripts signalScript;   // used sigscript
        private CsSignalScript csSignalScript;

        /// <summary>Extensible signal function type identifier.</summary>
        public SignalFunctionType SignalFunction { get; private set; } = SignalFunctionType.Unknown;

        public FreeTrainSimulator.Models.Signalling.SignalType SignalType { get; private set; }

        /// <summary>Extensible normal subtype identifier.</summary>
        public SignalNormalSubType NormalSubType { get; set; }

        public int TDBIndex { get; private set; }
        internal EnumArray<SpeedInfo, SignalAspectState> SpeedInfoSet { get; } = new EnumArray<SpeedInfo, SignalAspectState>();

        internal SpeedInfo CurrentSpeedInfo => SpeedInfoSetBySignalScript ? SignalScriptSpeedInfo : SpeedInfoSet[SignalIndicationState];

        public bool SpeedInfoSetBySignalScript { get; internal set; }
        internal SpeedInfo SignalScriptSpeedInfo { get; set; } // speed limit info set by C# signal script

        public Signal MainSignal { get; private set; }

        public SignalAspectState SignalIndicationState { get; set; } = SignalAspectState.Stop;
        public int DrawState { get; set; }
        public int TrackItemIndex { get; private set; }
        public int TrackJunctionNode { get; private set; }
        public int JunctionPath { get; private set; }
        public int JunctionMainNode { get; internal set; }
        public float? ApproachControlLimitPositionM { get; private set; }
        public float? ApproachControlLimitSpeedMpS { get; private set; }

        public string TextSignalAspect { get; set; } = string.Empty;


        /// <summary>
        /// Constructor for signals.
        /// </summary>
        public SignalHead(Signal signal, int trackItem, int tbdRef, SignalTrackItem signalItem)
        {
            MainSignal = signal ?? throw new ArgumentNullException(nameof(signal));
            TrackItemIndex = trackItem;
            TDBIndex = tbdRef;

            if (signalItem?.SignalDirection is FreeTrainSimulator.Models.Track.SignalDirection signalDirection && signalDirection.NodeIndex != 0)
            {
                TrackJunctionNode = signalDirection.NodeIndex;
                JunctionPath = signalDirection.JunctionPath;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for speedposts (old types — retained for TempSpeedPostItem from runtime speed zones)
        /// </summary>

        public SignalHead(Signal signal, int trackItem, int tbdRef, SpeedPostItem speedItem)
        {
            ArgumentNullException.ThrowIfNull(speedItem);

            MainSignal = signal ?? throw new ArgumentNullException(nameof(signal));
            TrackItemIndex = trackItem;
            TDBIndex = tbdRef;
            DrawState = 1;
            SignalIndicationState = SignalAspectState.Clear2;
            SignalType = new FreeTrainSimulator.Models.Signalling.SignalType
            {
                Name = "UNDEFINED",
                FunctionType = SignalFunctionType.Speed,
                DrawStates = new Dictionary<string, FreeTrainSimulator.Models.Signalling.SignalDrawState>(StringComparer.OrdinalIgnoreCase)
                {
                    { "CLEAR", new FreeTrainSimulator.Models.Signalling.SignalDrawState { Name = "CLEAR", Index = 1 } }
                }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
                SignalAspects = [new FreeTrainSimulator.Models.Signalling.SignalAspect { Aspect = SignalAspectState.Clear2, DrawStateName = "CLEAR", SpeedLimit = -1 }],
            };
            SignalFunction = SignalFunctionType.Speed;

            double speedMpS = Speed.MeterPerSecond.ToMpS(speedItem.Distance, !speedItem.IsMPH);
            if (speedItem.IsResume)
                speedMpS = 999.0;

            float passSpeed = speedItem.IsPassenger ? (float)speedMpS : -1;
            float freightSpeed = speedItem.IsFreight ? (float)speedMpS : -1;
            SpeedInfoSet[SignalIndicationState] = new SpeedInfo(passSpeed, freightSpeed, false, false, speedItem is TempSpeedPostItem ? (speedMpS == 999f ? 2 : 1) : 0, speedItem.IsWarning);
        }

        /// <summary>
        /// Constructor for speedposts using new TrackDatabase types.
        /// </summary>
        public SignalHead(Signal signal, int trackItem, int tbdRef, SpeedpostTrackItem speedItem)
        {
            ArgumentNullException.ThrowIfNull(speedItem);

            MainSignal = signal ?? throw new ArgumentNullException(nameof(signal));
            TrackItemIndex = trackItem;
            TDBIndex = tbdRef;
            DrawState = 1;
            SignalIndicationState = SignalAspectState.Clear2;
            SignalType = new FreeTrainSimulator.Models.Signalling.SignalType
            {
                Name = "UNDEFINED",
                FunctionType = SignalFunctionType.Speed,
                DrawStates = new Dictionary<string, FreeTrainSimulator.Models.Signalling.SignalDrawState>(StringComparer.OrdinalIgnoreCase)
                {
                    { "CLEAR", new FreeTrainSimulator.Models.Signalling.SignalDrawState { Name = "CLEAR", Index = 1 } }
                }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
                SignalAspects = [new FreeTrainSimulator.Models.Signalling.SignalAspect { Aspect = SignalAspectState.Clear2, DrawStateName = "CLEAR", SpeedLimit = -1 }],
            };
            SignalFunction = SignalFunctionType.Speed;

            double speedMpS = Speed.MeterPerSecond.ToMpS(speedItem.SpeedValue, speedItem.SpeedpostType.HasFlag(SpeedpostType.Metric));
            if (speedItem.SpeedpostType.HasFlag(SpeedpostType.Resume))
                speedMpS = 999.0;

            float passSpeed = speedItem.SpeedpostType.HasFlag(SpeedpostType.Passenger) ? (float)speedMpS : -1;
            float freightSpeed = speedItem.SpeedpostType.HasFlag(SpeedpostType.Freight) ? (float)speedMpS : -1;
            SpeedInfoSet[SignalIndicationState] = new SpeedInfo(passSpeed, freightSpeed, false, false, 0, speedItem.SpeedpostType.HasFlag(SpeedpostType.Warning));
        }

        internal void ResetMain(Signal signal)
        {
            MainSignal = signal;
        }

        //================================================================================================//
        /// <summary>
        /// Set the signal type object from the signal configuration model
        /// </summary>
        internal void SetSignalType(List<TrackItem> trackItems, SignalConfigurationModel signalConfig)
        {
            if (trackItems[TDBIndex] is SignalItem signalItem)
            {
                SetSignalTypeCore(signalItem.SignalType, signalConfig);
            }
        }

        /// <summary>
        /// Set the signal type object using the new TrackDatabase types.
        /// </summary>
        internal void SetSignalType(ImmutableArray<FreeTrainSimulator.Models.Track.TrackItemBase> trackItems, SignalConfigurationModel signalConfig)
        {
            if (trackItems[TDBIndex] is SignalTrackItem signalItem)
            {
                SetSignalTypeCore(signalItem.SignalType, signalConfig);
            }
        }

        /// <summary>
        /// Core logic for setting signal type from a signal type name string.
        /// </summary>
        private void SetSignalTypeCore(string signalTypeName, SignalConfigurationModel signalConfig)
        {
            {

                // set signal type
                if (signalConfig.SignalTypes.TryGetValue(signalTypeName, out FreeTrainSimulator.Models.Signalling.SignalType value))
                {
                    // set signal type
                    SignalType = value;
                    SignalFunction = SignalType.FunctionType;
                    // get related signalscript
                    SignalScriptProcessing.SignalScripts.Scripts.TryGetValue(SignalType.Name, out signalScript);

                    csSignalScript = CsSignalScripts.TryGetScript(SignalType.Name);
                    if (csSignalScript == null && !string.IsNullOrEmpty(SignalType.Script))
                        csSignalScript = CsSignalScripts.TryGetScript(SignalType.Script);

                    csSignalScript?.AttachToHead(this);

                    // set signal speeds
                    foreach (FreeTrainSimulator.Models.Signalling.SignalAspect aspect in SignalType.SignalAspects)
                    {
                        SpeedInfoSet[aspect.Aspect] = new SpeedInfo(
                            aspect.SpeedLimit, aspect.SpeedLimit,
                            aspect.AspectFlags.HasFlag(SignalAspectOptions.Asap),
                            aspect.AspectFlags.HasFlag(SignalAspectOptions.SpeedReset),
                            aspect.AspectFlags.HasFlag(SignalAspectOptions.NoSpeedReduction) ? 1 : 0,
                            false);
                    }

                    // set normal subtype
                    NormalSubType = SignalType.NormalSubType;

                    // update overall SignalNumClearAhead

                    if (SignalFunction == SignalFunctionType.Normal)
                    {
                        if (SignalType.SignalClearAheadMode != CompatibilityMode.None)
                        {
                            MainSignal.ClearAheadMode = SignalType.SignalClearAheadMode;
                        }
                        MainSignal.SignalNumClearAheadDefault = Math.Max(MainSignal.SignalNumClearAheadDefault, SignalType.ClearAheadNumber);
                        MainSignal.SignalNumClearAheadActive = MainSignal.SignalNumClearAheadDefault;
                    }

                    // set approach control limits
                    ApproachControlLimitPositionM = SignalType.ApproachControlLimitPosition;
                    ApproachControlLimitSpeedMpS = SignalType.ApproachControlLimitSpeed;
                }
                else
                {
                    Trace.TraceWarning($"SignalObject trItem={MainSignal.TrackItemIndex}, trackNode={MainSignal.TrackNode} has SignalHead with undefined SignalType {signalTypeName}.");
                }
            }
        }

        public void Initialize()
        {
            csSignalScript?.Initialize();
        }

        //================================================================================================//
        /// <summary>
        ///  Set of methods called per signal head from signal script processing
        ///  All methods link through to the main method set for signal objec
        /// </summary>

        public void HandleSignalMessage(int signalId, string message)
        {
            csSignalScript?.HandleSignalMessage(signalId, message);
        }

        public SignalAspectState NextSignalMR(int signalType)
        {
            return MainSignal.NextSignalMR(signalType);
        }

        public SignalAspectState NextSignalLR(int signalType)
        {
            return MainSignal.NextSignalLR(signalType);
        }

        public SignalAspectState ThisSignalLR(int signalType)
        {
            return MainSignal.SignalLR(signalType);
        }

        public SignalAspectState ThisSignalMR(int signalType)
        {
            return MainSignal.SignalMR(signalType);
        }

        public SignalAspectState OppositeSignalMR(int signalType)
        {
            return MainSignal.OppositeSignalMR(signalType);
        }

        public SignalAspectState OppositeSignalLR(int signalType)
        {
            return MainSignal.OppositeSignalLR(signalType);
        }

        public SignalAspectState NextNthSignalLR(int signalType, int nsignals)
        {
            return MainSignal.NextNthSignalLR(signalType, nsignals);
        }

        public int NextSignalId(int signalType)
        {
            return MainSignal.NextSignalId(signalType);
        }

        public int NextNthSignalId(int signalType, int nsignal)
        {
            return MainSignal.NextNthSignalId(signalType, nsignal);
        }

        public int OppositeSignalId(int signalType)
        {
            return MainSignal.OppositeSignalId(signalType);
        }

        public SignalAspectState SignalLRById(int signalId, int signalType)
        {
            if (signalId >= 0 && signalId < Simulator.Instance.SignalEnvironment.Signals.Count)
            {
                return Simulator.Instance.SignalEnvironment.Signals[signalId].SignalLRLimited(signalType);
            }
            return SignalAspectState.Stop;
        }

        public int SignalEnabledById(int signalId)
        {
            if (signalId >= 0 && signalId < Simulator.Instance.SignalEnvironment.Signals.Count)
            {
                return Simulator.Instance.SignalEnvironment.Signals[signalId].Enabled ? 1 : 0;
            }
            return 0;
        }

        public void StoreLocalVariable(int index, int value)
        {
            MainSignal.StoreLocalVariable(index, value);
        }

        public int ThisSignalLocalVariable(int index)
        {
            return MainSignal.SignalLocalVariable(index);
        }

        public int NextSignalLocalVariable(int signalType, int index)
        {
            return MainSignal.NextSignalLocalVariable(signalType, index);
        }

        public int LocalVariableBySignalId(int signalId, int index)
        {
            if (signalId >= 0 && signalId < Simulator.Instance.SignalEnvironment.Signals.Count)
            {
                return Simulator.Instance.SignalEnvironment.Signals[signalId].SignalLocalVariable(index);
            }
            return 0;
        }

        public int NextSignalHasNormalSubtype(int requestedSubtype)
        {
            return MainSignal.NextSignalHasNormalSubtype(requestedSubtype);
        }

        public int SignalHasNormalSubtype(int requestedSubtype)
        {
            return MainSignal.SignalHasNormalSubtype(requestedSubtype);
        }

        public int SignalHasNormalSubtypeById(int signalId, int requestedSubtype)
        {
            if (signalId >= 0 && signalId < Simulator.Instance.SignalEnvironment.Signals.Count)
            {
                return Simulator.Instance.SignalEnvironment.Signals[signalId].SignalHasNormalSubtype(requestedSubtype);
            }
            return 0;
        }

        internal int Switchstand(int aspect1, int aspect2)
        {
            return MainSignal.Switchstand(aspect1, aspect2);
        }

        //================================================================================================//
        /// <summary>
        ///  Returns most restrictive state of signal type A, for all type A upto type B
        ///  Uses Most Restricted state per signal, but checks for valid routing
        /// </summary>
        public SignalAspectState MRSignalMultiOnRoute(int signalType, int signalTypeOther)
        {
            SignalAspectState foundState = SignalAspectState.Clear2;
            bool foundValid = false;

            // get signal of type 2 (end signal)
            int sig2Index = MainSignal.Signalfound[signalTypeOther];
            if (sig2Index < 0)           // try renewed search with full route
            {
                sig2Index = MainSignal.SONextSignal(signalTypeOther);
                MainSignal.Signalfound[signalTypeOther] = sig2Index;
            }

            Signal thisSignal = MainSignal;

            // ensure next signal of type 1 is located correctly (cannot be done for normal signals searching next normal signal)

            if (!thisSignal.SignalNormal() || signalType != SignalFunctionType.Normal)
            {
                thisSignal.Signalfound[signalType] = thisSignal.SONextSignal(signalType);
            }

            // loop through all available signals of type 1

            while (thisSignal.Signalfound[signalType] >= 0)
            {
                thisSignal = Simulator.Instance.SignalEnvironment.Signals[thisSignal.Signalfound[signalType]];

                SignalAspectState thisState = thisSignal.MRSignalOnRoute(signalType);

                // ensure correct next signals are located
                if (signalType != SignalFunctionType.Normal || !thisSignal.SignalNormal())
                {
                    int sigFound = thisSignal.SONextSignal(signalType);
                    if (sigFound >= 0)
                        thisSignal.Signalfound[(int)signalType] = thisSignal.SONextSignal(signalType);
                }
                if (signalTypeOther != SignalFunctionType.Normal || !thisSignal.SignalNormal())
                {
                    int sigFound = thisSignal.SONextSignal(signalTypeOther);
                    if (sigFound >= 0)
                        thisSignal.Signalfound[(int)signalTypeOther] = thisSignal.SONextSignal(signalTypeOther);
                }

                if (sig2Index == thisSignal.Index) // this signal also contains type 2 signal and is therefor valid
                {
                    return foundState < thisState ? foundState : thisState;
                }
                else if (sig2Index >= 0 && thisSignal.Signalfound[signalTypeOther] != sig2Index)  // we are beyond type 2 signal
                {
                    return (foundValid ? foundState : SignalAspectState.Stop);
                }
                foundValid = true;
                foundState = foundState < thisState ? foundState : thisState;
            }

            return (foundValid ? foundState : SignalAspectState.Stop);   // no type 2 or running out of signals before finding type 2
        }

        //================================================================================================//
        /// <summary>
        ///  Returns most restrictive state of signal type A, for all type A upto type B
        ///  Uses Least Restrictive state per signal
        /// </summary>
        public SignalAspectState LRSignalMultiOnRoute(int signalType, int signalTypeOther)
        {
            SignalAspectState foundState = SignalAspectState.Clear2;
            bool foundValid = false;

            // get signal of type 2 (end signal)

            int sig2Index = MainSignal.Signalfound[signalTypeOther];
            if (sig2Index < 0)           // try renewed search with full route
            {
                sig2Index = MainSignal.SONextSignal(signalTypeOther);
                MainSignal.Signalfound[signalTypeOther] = sig2Index;
            }

            Signal thisSignal = MainSignal;

            // ensure next signal of type 1 is located correctly (cannot be done for normal signals searching next normal signal)

            if (!thisSignal.SignalNormal() || signalType != SignalFunctionType.Normal)
            {
                thisSignal.Signalfound[signalType] = thisSignal.SONextSignal(signalType);
            }

            // loop through all available signals of type 1

            while (thisSignal.Signalfound[signalType] >= 0)
            {
                thisSignal = Simulator.Instance.SignalEnvironment.Signals[thisSignal.Signalfound[signalType]];

                SignalAspectState thisState = thisSignal.SignalLRLimited(signalType);

                // ensure correct next signals are located
                if (signalType != SignalFunctionType.Normal || !thisSignal.SignalNormal())
                {
                    int sigFound = thisSignal.SONextSignal(signalType);
                    if (sigFound >= 0)
                        thisSignal.Signalfound[signalType] = thisSignal.SONextSignal(signalType);
                }
                if (signalTypeOther != SignalFunctionType.Normal || !thisSignal.SignalNormal())
                {
                    int sigFound = thisSignal.SONextSignal(signalTypeOther);
                    if (sigFound >= 0)
                        thisSignal.Signalfound[signalTypeOther] = thisSignal.SONextSignal(signalTypeOther);
                }

                if (sig2Index == thisSignal.Index) // this signal also contains type 2 signal and is therefor valid
                {
                    return foundState < thisState ? foundState : thisState;
                }
                else if (sig2Index >= 0 && thisSignal.Signalfound[signalTypeOther] != sig2Index)  // we are beyond type 2 signal
                {
                    return (foundValid ? foundState : SignalAspectState.Stop);
                }
                foundValid = true;
                foundState = foundState < thisState ? foundState : thisState;
            }

            return (foundValid ? foundState : SignalAspectState.Stop);   // no type 2 or running out of signals before finding type 2
        }

        //================================================================================================//
        /// </summary>
        ///  Return state of requested feature through signal head flags
        /// </summary>
        public bool VerifySignalFeature(int feature)
        {
            if (feature < MainSignal.WorldObject?.FlagsSet.Count)
            {
                return MainSignal.WorldObject.FlagsSet[feature];
            }
            return true;
        }

        //================================================================================================//
        /// <summary>
        ///  Returns the default draw state for this signal head from the SIGCFG file
        ///  Retruns -1 id no draw state.
        /// </summary>
        public int DefaultDrawState(SignalAspectState state)
        {
            return SignalType?.GetDefaultDrawState(state) ?? -1;
        }

        //================================================================================================//
        /// <summary>
        ///  Sets the state to the most restrictive aspect for this head.
        /// </summary>
        public void RequestMostRestrictiveAspect()
        {
            if (csSignalScript != null)
            {
                csSignalScript.HandleEvent(SignalEvent.RequestMostRestrictiveAspect);
                csSignalScript.Update();
            }
            else
            {
                SignalIndicationState = SignalType?.GetMostRestrictiveAspect() ?? SignalAspectState.Stop;
                DrawState = DefaultDrawState(SignalIndicationState);
            }
        }

        public void RequestApproachAspect()
        {
            if (csSignalScript != null)
            {
                csSignalScript.HandleEvent(SignalEvent.RequestApproachAspect);
                csSignalScript.Update();
            }
            else
            {
                int drawState1 = DefaultDrawState(SignalAspectState.Approach1);
                int drawState2 = DefaultDrawState(SignalAspectState.Approach2);

                SignalIndicationState = drawState1 > 0 ? SignalAspectState.Approach1 : drawState2 > 0 ? SignalAspectState.Approach2 : SignalAspectState.Approach3;
                DrawState = DefaultDrawState(SignalIndicationState);
            }
        }

        //================================================================================================//
        /// <summary>
        ///  Sets the state to the least restrictive aspect for this head.
        /// </summary>
        public void RequestLeastRestrictiveAspect()
        {
            if (csSignalScript != null)
            {
                csSignalScript.HandleEvent(SignalEvent.RequestLeastRestrictiveAspect);
                csSignalScript.Update();
            }
            else
            {
                SignalIndicationState = SignalType?.GetLeastRestrictiveAspect() ?? SignalAspectState.Clear2;
                DefaultDrawState(SignalIndicationState);
            }
        }

        //================================================================================================//
        /// <summary>
        ///  check if linked route is set
        /// </summary>
        public int VerifyRouteSet()
        {
            // call route_set routine from main signal
            if (TrackJunctionNode > 0)
            {
                return MainSignal.CheckRouteSet(JunctionMainNode, TrackJunctionNode) ? 1 : 0;
            }
            //added by JTang
            else if (MultiPlayerManager.IsMultiPlayer())
            {
                TrackDatabase trackDatabase = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase;
                TrackNodeConnectorIndex connectorIndex = trackDatabase.TrackNodeConnectors[MainSignal.TrackNode];
                TrackNodeBase node = trackDatabase.TrackNodes[MainSignal.TrackNode];
                if (node is not JunctionNode && !connectorIndex.TrackNodeConnectors.IsDefaultOrEmpty && (int)MainSignal.TrackCircuitDirection < connectorIndex.TrackNodeConnectors.Length)
                {
                    int linkedNodeIndex = connectorIndex.TrackNodeConnectors[(int)MainSignal.TrackCircuitDirection].Link;
                    TrackNodeConnectorIndex linkedConnectors = trackDatabase.TrackNodeConnectors[linkedNodeIndex];
                    if (trackDatabase.TrackNodes[linkedNodeIndex] is not JunctionNode)
                        return 0;
                    for (int pin = linkedConnectors.InboundCount; pin < linkedConnectors.TrackNodeConnectors.Length; pin++)
                    {
                        if (linkedConnectors.TrackNodeConnectors[pin].Link == MainSignal.TrackNode && pin - linkedConnectors.InboundCount != RuntimeDataResolver.Instance.TrackWorld.SwitchStates[linkedNodeIndex])
                        {
                            return 0;
                        }
                    }
                }
            }
            return 1;
        }

        /// <summary>
        ///  Default update process
        /// </summary>
        public void Update()
        {
            if (csSignalScript != null)
                csSignalScript.Update();
            else
                SignalScriptProcessing.SignalHeadUpdate(this, signalScript);
        }
    } //Update

}
