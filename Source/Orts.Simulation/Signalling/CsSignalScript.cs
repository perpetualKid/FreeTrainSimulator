using System;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Scripting.Api;

namespace Orts.Simulation.Signalling
{
    // TODO 20210506 this should be in Orts.Scripting really! Would need deep refactoring though

    // The exchange of information is done through the TextSignalAspect property.
    // The MSTS signal aspect is only used for TCS scripts that do not support TextSignalAspect..
    public abstract class CsSignalScript : ScriptBase
    {
        // References and shortcuts. Must be private to not expose them through the API
        private SignalHead signalHead;
        private Signal SignalObject => signalHead.MainSignal;

        private static int SigFnIndex(string sigFn)
        {
            return OrSignalTypes.Instance.FunctionTypes.FindIndex(functionType => StringComparer.OrdinalIgnoreCase.Equals(functionType, sigFn));
        }

        // Public interface
        /// <summary>
        /// Represents one of the eight aspects used for display purposes, AI traffic and SIGSCR signalling
        /// </summary>
        public SignalAspectState MstsSignalAspect { get => signalHead.SignalIndicationState; protected set => signalHead.SignalIndicationState = value; }

        /// <summary>
        /// Custom aspect of the signal
        /// </summary>
        public string TextSignalAspect { get => signalHead.TextSignalAspect; protected set => signalHead.TextSignalAspect = value; }

        /// <summary>
        /// Draw State that the signal object will show (i. e. active lights and semaphore position)
        /// </summary>
        public int DrawState { get => signalHead.DrawState; protected set => signalHead.DrawState = value; }

        /// <summary>
        /// True if a train is approaching the signal
        /// </summary>
        public bool Enabled => SignalObject.Enabled;

        /// <summary>
        /// Distance at which the signal will be cleared during approach control
        /// </summary>
        public float? ApproachControlRequiredPosition => signalHead.ApproachControlLimitPositionM;

        /// <summary>
        /// Maximum train speed at which the signal will be cleared during approach control
        /// </summary>
        public float? ApproachControlRequiredSpeed => signalHead.ApproachControlLimitSpeedMpS;

        /// <summary>
        /// Occupation and reservation state of the signal's route
        /// </summary>
        public SignalBlockState BlockState => SignalObject.BlockState();

        /// <summary>
        /// True if the signal link is activated
        /// </summary>
        public bool RouteSet => signalHead.VerifyRouteSet() > 0;

        /// <summary>
        /// Hold state of the signal
        /// </summary>
        public SignalHoldState HoldState => SignalObject.HoldState;
        /// <summary>
        /// Set this variable to true to allow clear to partial route
        /// </summary>
        public void AllowClearPartialRoute(bool allow)
        {
            SignalObject.AllowClearPartialRoute(allow ? 1 : 0);
        }

        /// <summary>
        /// Number of signals to clear ahead of this signal
        /// </summary>
        public int SignalNumClearAhead { get => SignalObject.SignalNumClearAheadActive; set => SignalObject.SetSignalNumClearAhead(value); }

        /// <summary>
        /// Default draw state of the signal for a specific aspect
        /// </summary>
        /// <param name="signalAspect">Aspect for which the default draw state must be found</param>
        /// <returns></returns>
        public int DefaultDrawState(SignalAspectState signalAspect)
        {
            return signalHead.DefaultDrawState(signalAspect);
        }

        /// <summary>
        /// Index of the draw state with the specified name
        /// </summary>
        /// <param name="name">Name of the draw state as defined in sigcfg</param>
        /// <returns>The index of the draw state, -1 if no one exist with that name</returns>
        public int GetDrawStateByName(string name)
        {
            return signalHead.SignalType.DrawStates.TryGetValue(name, out SignalDrawState drawState) ? drawState.Index : -1;
        }

        /// <summary>
        /// Signal identity of this signal
        /// </summary>
        public int SignalId => SignalObject.Index;

        /// <summary>
        /// Name of this signal type, as defined in sigcfg
        /// </summary>
        public string SignalTypeName => signalHead.SignalType.Name;

        /// <summary>
        /// Name of the signal shape, as defined in sigcfg
        /// </summary>
        public string SignalShapeName => SignalObject.WorldObject.ShapeFileName;

        protected CsSignalScript()
        {
        }

        /// <summary>
        /// Sends a message to the specified signal
        /// </summary>
        /// <param name="signalId">Id of the signal to which the message shall be sent</param>
        /// <param name="message">Message to send</param>
        public void SendSignalMessage(int signalId, string message)
        {
            if (signalId < 0 || signalId > Simulator.Instance.SignalEnvironment.Signals.Count)
                return;
            foreach (SignalHead head in Simulator.Instance.SignalEnvironment.Signals[signalId].SignalHeads)
            {
                head.HandleSignalMessage(SignalObject.Index, message);
            }
        }

        /// <summary>
        /// Check if this signal has a specific feature
        /// </summary>
        /// <param name="signalFeature">Name of the requested feature</param>
        /// <returns></returns>
        public bool IsSignalFeatureEnabled(string signalFeature)
        {
            if (!EnumExtension.GetValue(signalFeature, out SignalSubType subType))
                subType = SignalSubType.None;
            return signalHead.VerifySignalFeature((int)subType);
        }

        /// <summary>
        /// Checks if the signal has a specific head
        /// </summary>
        /// <param name="requiredHeadIndex">Index of the required head</param>
        /// <returns>True if the required head is present</returns>
        public bool HasHead(int requiredHeadIndex)
        {
            return SignalObject.HasHead(requiredHeadIndex) == 1;
        }

        /// <summary>
        /// Get id of next signal
        /// </summary>
        /// <param name="sigfn">Signal function of the required signal</param>
        /// <param name="count">Get id of nth signal (first is 0)</param>
        /// <returns>Id of required signal</returns>
        public int NextSignalId(string sigfn, int count = 0)
        {
            return SignalObject.NextNthSignalId(OrSignalTypes.Instance.FunctionTypes.FindIndex(i => StringComparer.OrdinalIgnoreCase.Equals(i, sigfn)), count + 1);
        }

        /// <summary>
        /// Get id of opposite signal
        /// </summary>
        /// <param name="sigfn">Signal function of the required signal</param>
        /// <returns></returns>
        public int OppositeSignalId(string sigfn)
        {
            return SignalObject.OppositeSignalId(SigFnIndex(sigfn));
        }

        /// <summary>
        /// Find next normal signal of a specific subtype
        /// </summary>
        /// <param name="normalSubtype">Required normal subtype</param>
        /// <returns>Id of required signal</returns>
        public int RequiredNormalSignalId(string normalSubtype)
        {
            return SignalObject.FindRequiredNormalSignal(OrSignalTypes.Instance.NormalSubTypes.IndexOf(normalSubtype));
        }

        /// <summary>
        /// Check if required signal has a normal subtype
        /// </summary>
        /// <param name="id">Id of the signal</param>
        /// <param name="normalSubtype">Normal subtype to test</param>
        public bool IdSignalHasNormalSubtype(int id, string normalSubtype)
        {
            return signalHead.SignalHasNormalSubtypeById(id, OrSignalTypes.Instance.NormalSubTypes.IndexOf(normalSubtype)) == 1;
        }

        /// <summary>
        /// Get the text aspect of a specific signal
        /// </summary>
        /// <param name="id">Id of the signal to query</param>
        /// <param name="sigfn">Consider only heads with a specific signal function</param>
        /// <param name="headindex">Get aspect of nth head of the specified type</param>
        public static string IdTextSignalAspect(int id, string sigfn, int headindex = 0)
        {
            if (id < 0 || id > Simulator.Instance.SignalEnvironment.Signals.Count)
                return string.Empty;

            foreach (SignalHead head in Simulator.Instance.SignalEnvironment.Signals[id].SignalHeads)
            {
                if (head.OrtsSignalFunctionIndex == SigFnIndex(sigfn))
                {
                    if (headindex <= 0) return head.TextSignalAspect;
                    headindex--;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Obtains the most restrictive aspect of the signals of type A up to the first signal of type B
        /// </summary>
        /// <param name="sigfnA">Signals to search</param>
        /// <param name="sigfnB">Signal type where search is stopped</param>
        /// <param name="mostRestrictiveHead">Check most restrictive head per signal</param>
        public SignalAspectState DistMultiSigMR(string sigfnA, string sigfnB, bool mostRestrictiveHead = true)
        {
            if (mostRestrictiveHead)
                return signalHead.MRSignalMultiOnRoute(SigFnIndex(sigfnA), SigFnIndex(sigfnB));
            return signalHead.LRSignalMultiOnRoute(SigFnIndex(sigfnA), SigFnIndex(sigfnB));
        }

        /// <summary>
        /// Get aspect of required signal
        /// </summary>
        /// <param name="id">Id of required signal</param>
        /// <param name="sigfn">Function of the signal heads to consider</param>
        /// <param name="mostRestrictive">Get most restrictive instead of least restrictive</param>
        /// <param name="checkRouting">If looking for most restrictive aspect, consider only heads with the route link activated</param>
        public SignalAspectState IdSignalAspect(int id, string sigfn, bool mostRestrictive = false, bool checkRouting = false)
        {
            if (!mostRestrictive)
                return signalHead.SignalLRById(id, SigFnIndex(sigfn));
            else if (id >= 0 && id < Simulator.Instance.SignalEnvironment.Signals.Count)
            {
                Signal signal = Simulator.Instance.SignalEnvironment.Signals[id];
                if (checkRouting)
                    return signal.MRSignalOnRoute(SigFnIndex(sigfn));
                else
                    return signal.SignalMR(SigFnIndex(sigfn));
            }
            return SignalAspectState.Stop;
        }

        /// <summary>
        /// Get local variable of the required signal
        /// </summary>
        /// <param name="id">Id of the signal to get local variable from</param>
        /// <param name="key">Key of the variable</param>
        /// <returns>The value of the required variable</returns>
        public int IdSignalLocalVariable(int id, int key)
        {
            return signalHead.LocalVariableBySignalId(id, key);
        }

        /// <summary>
        /// Check if signal is enabled
        /// </summary>
        /// <param name="id">Id of the signal to check</param>
        public bool IdSignalEnabled(int id)
        {
            return signalHead.SignalEnabledById(id) > 0;
        }

        /// <summary>
        /// Check if train has 'call on' set
        /// </summary>
        /// <param name="allowOnNonePlatform">Allow Call On without platform</param>
        /// <param name="allowAdvancedSignal">Allow Call On even if this is not first signal for train</param>
        /// <returns>True if train is allowed to call on with the required parameters, false otherwise</returns>
        public bool TrainHasCallOn(bool allowOnNonePlatform = true, bool allowAdvancedSignal = false)
        {
            SignalObject.CallOnEnabled = true;
            return SignalObject.TrainHasCallOn(allowOnNonePlatform, allowAdvancedSignal);
        }

        /// <summary>
        /// Test if train requires next signal
        /// </summary>
        /// <param name="signalId">Id of the signal to be tested</param>
        /// <param name="reqPosition">1 if next track circuit after required signal is checked, 0 if not</param>
        public bool TrainRequiresSignal(int signalId, int reqPosition)
        {
            return SignalObject.RequiresNextSignal(signalId, reqPosition);
        }

        /// <summary>
        /// Checks if train is closer than required position from the signal
        /// </summary>
        /// <param name="reqPositionM">Maximum distance to activate approach control</param>
        /// <param name="forced">Activate approach control even if this is not the first signal for the train</param>
        /// <returns>True if approach control is set</returns>
        public bool ApproachControlPosition(float reqPositionM, bool forced = false)
        {
            return SignalObject.ApproachControlPosition((int)reqPositionM, forced);
        }

        /// <summary>
        /// Checks if train is closer than required distance to the signal, and if it is running at lower speed than specified
        /// </summary>
        /// <param name="reqPositionM">Maximum distance to activate approach control</param>
        /// <param name="reqSpeedMpS">Maximum speed at which approach control will be set</param>
        /// <returns>True if the conditions are fulfilled, false otherwise</returns>
        public bool ApproachControlSpeed(float reqPositionM, float requestedSpeedMpS)
        {
            return SignalObject.ApproachControlSpeed((int)reqPositionM, (int)requestedSpeedMpS);
        }

        /// <summary>
        /// Checks if train is closer than required distance to the signal, and if it is running at lower speed than specified,
        /// in case of APC in next STOP
        /// </summary>
        /// <param name="reqPositionM">Maximum distance to activate approach control</param>
        /// <param name="reqSpeedMpS">Maximum speed at which approach control will be set</param>
        /// <returns>True if the conditions are fulfilled, false otherwise</returns>
        public bool ApproachControlNextStop(float reqPositionM, float requestedSpeedMpS)
        {
            return SignalObject.ApproachControlNextStop((int)reqPositionM, (int)requestedSpeedMpS);
        }

        /// <summary>
        /// Lock claim (only if approach control is active)
        /// </summary>
        public void ApproachControlLockClaim()
        {
            SignalObject.LockClaim();
        }

        /// <summary>
        /// Checks if the route is cleared up to the required signal
        /// </summary>
        /// <param name="signalId">Id of the signal where check is stopped</param>
        /// <param name="allowCallOn">Consider route as cleared if train has call on</param>
        /// <returns>The state of the route from this signal to the required signal</returns>
        public SignalBlockState RouteClearedToSignal(int signalId, bool allowCallOn = false)
        {
            return SignalObject.RouteClearedToSignal(signalId, allowCallOn);
        }

        /// <summary>
        /// Internally called to assign this instance to a signal head
        /// </summary>
        internal void AttachToHead(SignalHead signalHead)
        {
            this.signalHead = signalHead;

            // Build AbstractScriptClass API functions
            ClockTime = () => (float)Simulator.Instance.ClockTime;
            GameTime = () => (float)Simulator.Instance.GameTime;
            PreUpdate = () => Simulator.Instance.PreUpdate;
        }

        protected void SetSharedVariable(int index, int value)
        {
            signalHead.MainSignal.StoreLocalVariable(index, value);
        }

        protected int GetSharedVariable(int index)
        {
            return signalHead.MainSignal.SignalLocalVariable(index);
        }

        // Functions to be implemented in script

        /// <summary>
        /// Called once at initialization time
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Called regularly during the simulation
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Called when a signal sends a message to this signal
        /// </summary>
        /// <param name="signalId">Signal ID of the calling signal</param>
        /// <param name="message">Message sent to signal</param>
        /// <returns></returns>
        public virtual void HandleSignalMessage(int signalId, string message) { }
        /// <summary>
        /// Called when the simulator
        /// </summary>
        /// <param name="evt"></param>
        public virtual void HandleEvent(SignalEvent evt, string message = "") { }
    }
}
