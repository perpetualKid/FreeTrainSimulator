using System;
using System.Diagnostics;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.MultiPlayer;

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

        public SignalFunction SignalFunction => SignalType?.FunctionType ?? SignalFunction.Unknown;

        public int OrtsSignalFunctionIndex => SignalType?.OrtsFunctionTypeIndex ?? -1;

        public SignalType SignalType { get; private set; }
        public int OrtsNormalSubtypeIndex { get; set; }
        public int TDBIndex { get; private set; }
        public EnumArray<SpeedInfo, SignalAspectState> SpeedInfoSet { get; } = new EnumArray<SpeedInfo, SignalAspectState>();
        public Signal MainSignal { get; private set; }
        public SignalScripts.SCRScripts SignalScript => signalScript;

        public SignalAspectState SignalIndicationState { get; set; } = SignalAspectState.Stop;
        public int DrawState { get; set; }
        public int TrackItemIndex { get; private set; }
        public uint TrackJunctionNode { get; private set; }
        public uint JunctionPath { get; private set; }
        public int JunctionMainNode { get; internal set; }
        public float? ApproachControlLimitPositionM { get; private set; }
        public float? ApproachControlLimitSpeedMpS { get; private set; }

        //================================================================================================//
        /// <summary>
        /// Constructor for signals
        /// </summary>

        public SignalHead(Signal signal, int trackItem, int tbdRef, SignalItem signalItem)
        {
            MainSignal = signal ?? throw new ArgumentNullException(nameof(signal));
            TrackItemIndex = trackItem;
            TDBIndex = tbdRef;

            if (signalItem?.SignalDirections?.Length > 0)
            {
                TrackJunctionNode = signalItem.SignalDirections[0].TrackNode;
                JunctionPath = signalItem.SignalDirections[0].LinkLRPath;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for speedposts
        /// </summary>

        public SignalHead(Signal signal, int trackItem, int tbdRef, SpeedPostItem speedItem)
        {
            if (speedItem == null)
                throw new ArgumentNullException(nameof(speedItem));

            MainSignal = signal ?? throw new ArgumentNullException(nameof(signal));
            TrackItemIndex = trackItem;
            TDBIndex = tbdRef;
            DrawState = 1;
            SignalIndicationState = SignalAspectState.Clear_2;
            SignalType = new SignalType(SignalFunction.Speed, SignalAspectState.Clear_2);

            double speedMpS = Speed.MeterPerSecond.ToMpS(speedItem.Distance, !speedItem.IsMPH);
            if (speedItem.IsResume)
                speedMpS = 999.0;

            float passSpeed = speedItem.IsPassenger ? (float)speedMpS : -1;
            float freightSpeed = speedItem.IsFreight ? (float)speedMpS : -1;
            SpeedInfoSet[SignalIndicationState] = new SpeedInfo(passSpeed, freightSpeed, false, false, speedItem is TempSpeedPostItem ? (speedMpS == 999f ? 2 : 1) : 0); ;
        }

        internal void ResetMain(Signal signal)
        {
            MainSignal = signal;
        }

        //================================================================================================//
        /// <summary>
        /// Set the signal type object from the SIGCFG file
        /// </summary>
        internal void SetSignalType(TrackItem[] trackItems, SignalConfigurationFile signalConfig)
        {
            SignalItem signalItem = (SignalItem)trackItems[TDBIndex];

            // set signal type
            if (signalConfig.SignalTypes.ContainsKey(signalItem.SignalType))
            {
                // set signal type
                SignalType = signalConfig.SignalTypes[signalItem.SignalType];

                // get related signalscript
                SignalEnvironment.SignalScriptsFile.SignalScripts.Scripts.TryGetValue(SignalType, out signalScript);

                // set signal speeds
                foreach (SignalAspect aspect in SignalType.Aspects)
                {
                    SpeedInfoSet[aspect.Aspect] = new SpeedInfo(aspect.SpeedLimit, aspect.SpeedLimit, aspect.Asap, aspect.Reset, aspect.NoSpeedReduction ? 1 : 0);
                }

                // set normal subtype
                OrtsNormalSubtypeIndex = SignalType.OrtsNormalSubTypeIndex;

                // update overall SignalNumClearAhead

                if (SignalFunction == SignalFunction.Normal)
                {
                    MainSignal.SignalNumClearAhead_MSTS = Math.Max(MainSignal.SignalNumClearAhead_MSTS, SignalType.NumClearAhead_MSTS);
                    MainSignal.SignalNumClearAhead_ORTS = Math.Max(MainSignal.SignalNumClearAhead_ORTS, SignalType.NumClearAhead_ORTS);
                    MainSignal.SignalNumClearAheadActive = MainSignal.SignalNumClearAhead_ORTS;
                }

                // set approach control limits
                if (SignalType.ApproachControlDetails != null)
                {
                    ApproachControlLimitPositionM = SignalType.ApproachControlDetails.ApproachControlPositionM;
                    ApproachControlLimitSpeedMpS = SignalType.ApproachControlDetails.ApproachControlSpeedMpS;
                }
                else
                {
                    ApproachControlLimitPositionM = null;
                    ApproachControlLimitSpeedMpS = null;
                }

                if (SignalFunction == SignalFunction.Speed)
                {
                    MainSignal.IsSignal = false;
                    MainSignal.IsSpeedSignal = true;
                }
            }
            else
            {
                Trace.TraceWarning($"SignalObject trItem={MainSignal.TrackItemIndex}, trackNode={MainSignal.TrackNode} has SignalHead with undefined SignalType {signalItem.SignalType}.");
            }
        }

        //================================================================================================//
        /// <summary>
        ///  Set of methods called per signal head from signal script processing
        ///  All methods link through to the main method set for signal objec
        /// </summary>

        public SignalAspectState NextSignalMR(int signalType)
        {
            return MainSignal.next_sig_mr(signalType);
        }

        public SignalAspectState NextSignalLR(int signalType)
        {
            return MainSignal.next_sig_lr(signalType);
        }

        public SignalAspectState ThisSignalLR(int signalType)
        {
            return MainSignal.this_sig_lr(signalType);
        }

        public SignalAspectState ThisSignalLR(int signalType, ref bool sigfound)
        {
            return MainSignal.this_sig_lr(signalType, ref sigfound);
        }

        public SignalAspectState ThisSignalMR(int signalType)
        {
            return MainSignal.this_sig_mr(signalType);
        }

        public SignalAspectState ThisSignalMR(int signalType, ref bool sigfound)
        {
            return MainSignal.this_sig_mr(signalType, ref sigfound);
        }

        public SignalAspectState OppositeSignalMR(int signalType)
        {
            return MainSignal.opp_sig_mr(signalType);
        }

        public SignalAspectState OppositeSignalMR(int signalType, ref Signal signalFound) // for debug purposes
        {
            return MainSignal.opp_sig_mr(signalType, ref signalFound);
        }

        public SignalAspectState OppositeSignalLR(int signalType)
        {
            return MainSignal.opp_sig_lr(signalType);
        }

        public SignalAspectState OppositeSignalLR(int signalType, ref Signal signalFound) // for debug purposes
        {
            return MainSignal.opp_sig_lr(signalType, ref signalFound);
        }

        public SignalAspectState NextNthSignalLR(int signalType, int nsignals)
        {
            return MainSignal.next_nsig_lr(signalType, nsignals);
        }

        public int NextSignalId(int signalType)
        {
            return MainSignal.next_sig_id(signalType);
        }

        public int NextNthSignalId(int signalType, int nsignal)
        {
            return MainSignal.next_nsig_id(signalType, nsignal);
        }

        public int OppositeSignalId(int signalType)
        {
            return MainSignal.opp_sig_id(signalType);
        }

        public SignalAspectState SignalLRById(int signalId, int signalType)
        {
            if (signalId >= 0 && signalId < Signal.SignalEnvironment.SignalObjects.Count)
            {
                return Signal.SignalEnvironment.SignalObjects[signalId].this_sig_lr(signalType);
            }
            return SignalAspectState.Stop;
        }

        public int SignalEnabledById(int signalId)
        {
            if (signalId >= 0 && signalId < Signal.SignalEnvironment.SignalObjects.Count)
            {
                return Signal.SignalEnvironment.SignalObjects[signalId].Enabled ? 1 : 0;
            }
            return 0;
        }

        public void StoreLocalVariable(int index, int value)
        {
            MainSignal.store_lvar(index, value);
        }

        public int ThisSignalLocalVariable(int index)
        {
            return MainSignal.this_sig_lvar(index);
        }

        public int NextSignalLocalVariable(int signalType, int index)
        {
            return MainSignal.next_sig_lvar(signalType, index);
        }

        public int LocalVariableBySignalId(int signalId, int index)
        {
            if (signalId >= 0 && signalId < Signal.SignalEnvironment.SignalObjects.Count)
            {
                return Signal.SignalEnvironment.SignalObjects[signalId].this_sig_lvar(index);
            }
            return 0;
        }

        public int NextSignalHasNormalSubtype(int requestedSubtype)
        {
            return MainSignal.next_sig_hasnormalsubtype(requestedSubtype);
        }

        public int ThisSignalHasNormalSubtype(int requestedSubtype)
        {
            return MainSignal.this_sig_hasnormalsubtype(requestedSubtype);
        }

        public int SignalHasNormalSubtypeById(int signalId, int requestedSubtype)
        {
            if (signalId >= 0 && signalId < Signal.SignalEnvironment.SignalObjects.Count)
            {
                return Signal.SignalEnvironment.SignalObjects[signalId].this_sig_hasnormalsubtype(requestedSubtype);
            }
            return 0;
        }

        internal int Switchstand(int aspect1, int aspect2)
        {
            return MainSignal.switchstand(aspect1, aspect2);
        }

        //================================================================================================//
        /// <summary>
        ///  Returns most restrictive state of signal type A, for all type A upto type B
        ///  Uses Most Restricted state per signal, but checks for valid routing
        /// </summary>
        public SignalAspectState MRSignalMultiOnRoute(int signalType, int signalTypeOther)
        {
            SignalAspectState foundState = SignalAspectState.Clear_2;
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

            if (!thisSignal.SignalNormal() || signalType != (int)SignalFunction.Normal)
            {
                thisSignal.Signalfound[signalType] = thisSignal.SONextSignal(signalType);
            }

            // loop through all available signals of type 1

            while (thisSignal.Signalfound[signalType] >= 0)
            {
                thisSignal = Signal.SignalEnvironment.SignalObjects[thisSignal.Signalfound[signalType]];

                SignalAspectState thisState = thisSignal.MRSignalOnRoute(signalType);

                // ensure correct next signals are located
                if (signalType != (int)SignalFunction.Normal || !thisSignal.SignalNormal())
                {
                    int sigFound = thisSignal.SONextSignal(signalType);
                    if (sigFound >= 0) thisSignal.Signalfound[(int)signalType] = thisSignal.SONextSignal(signalType);
                }
                if (signalTypeOther != (int)SignalFunction.Normal || !thisSignal.SignalNormal())
                {
                    int sigFound = thisSignal.SONextSignal(signalTypeOther);
                    if (sigFound >= 0) thisSignal.Signalfound[(int)signalTypeOther] = thisSignal.SONextSignal(signalTypeOther);
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
            SignalAspectState foundState = SignalAspectState.Clear_2;
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

            if (!thisSignal.SignalNormal() || signalType != (int)SignalFunction.Normal)
            {
                thisSignal.Signalfound[signalType] = thisSignal.SONextSignal(signalType);
            }

            // loop through all available signals of type 1

            while (thisSignal.Signalfound[signalType] >= 0)
            {
                thisSignal = Signal.SignalEnvironment.SignalObjects[thisSignal.Signalfound[signalType]];

                SignalAspectState thisState = thisSignal.this_sig_lr(signalType);

                // ensure correct next signals are located
                if (signalType != (int)SignalFunction.Normal || !thisSignal.SignalNormal())
                {
                    int sigFound = thisSignal.SONextSignal(signalType);
                    if (sigFound >= 0)
                        thisSignal.Signalfound[signalType] = thisSignal.SONextSignal(signalType);
                }
                if (signalTypeOther != (int)SignalFunction.Normal || !thisSignal.SignalNormal())
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
        public void SetMostRestrictiveAspect()
        {
            SignalIndicationState = SignalType?.GetMostRestrictiveAspect() ?? SignalAspectState.Stop;
            DrawState = DefaultDrawState(SignalIndicationState);
        }

        //================================================================================================//
        /// <summary>
        ///  Sets the state to the least restrictive aspect for this head.
        /// </summary>
        public void SetLeastRestrictiveAspect()
        {
            SignalIndicationState = SignalType?.GetLeastRestrictiveAspect() ?? SignalAspectState.Clear_2;
            DefaultDrawState(SignalIndicationState);
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
                return MainSignal.route_set(JunctionMainNode, TrackJunctionNode) ? 1 : 0;
            }
            //added by JTang
            else if (MPManager.IsMultiPlayer())
            {
                TrackNode node = Signal.SignalEnvironment.Simulator.TDB.TrackDB.TrackNodes[MainSignal.TrackNode];
                if (!(node is TrackJunctionNode) && node.TrackPins != null && (int)MainSignal.TrackCircuitDirection < node.TrackPins.Length)
                {
                    node = Signal.SignalEnvironment.Simulator.TDB.TrackDB.TrackNodes[node.TrackPins[(int)MainSignal.TrackCircuitDirection].Link];
                    if (!(node is TrackJunctionNode junctionNode)) return 0;
                    for (int pin = junctionNode.InPins; pin < junctionNode.InPins + junctionNode.OutPins; pin++)
                    {
                        if (junctionNode.TrackPins[pin].Link == MainSignal.TrackNode && pin - junctionNode.InPins != junctionNode.SelectedRoute)
                        {
                            return 0;
                        }
                    }
                }
            }
            return 1;
        }

        //================================================================================================//
        /// <summary>
        ///  Default update process
        /// </summary>

        public void Update()
        {
            SIGSCRfile.SH_update(this, SignalEnvironment.SignalScriptsFile);
        }
    } //Update

}
