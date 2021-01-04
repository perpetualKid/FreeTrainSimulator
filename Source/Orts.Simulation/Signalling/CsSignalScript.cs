using System;
using System.Collections.Generic;
using System.Linq;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.Simulation.Signalling
{
    // The exchange of information is done through the TextSignalAspect property.
    // The MSTS signal aspect is only used for TCS scripts that do not support TextSignalAspect..
    public abstract class CsSignalScript
    {
        // References and shortcuts. Must be private to not expose them through the API
        private SignalHead signalHead;
        private Signal SignalObject => signalHead.MainSignal;

        private static int SigFnIndex(string sigFn)
        {
            return OrSignalTypes.Instance.FunctionTypes.IndexOf(sigFn);
        }

        // Public interface
        public SignalAspectState MstsSignalAspect { get => signalHead.SignalIndicationState; protected set => signalHead.SignalIndicationState = value; }
        public string TextSignalAspect { get => signalHead.TextSignalAspect; protected set => signalHead.TextSignalAspect = value; }
        public int DrawState { get => signalHead.DrawState; protected set => signalHead.DrawState = value; }
        public bool Enabled => SignalObject.Enabled;
        public float? ApproachControlRequiredPosition => signalHead.ApproachControlLimitPositionM.Value;
        public float? ApproachControlRequiredSpeed => signalHead.ApproachControlLimitSpeedMpS.Value;
        public SignalBlockState BlockState => SignalObject.BlockState();
        public bool RouteSet => signalHead.VerifyRouteSet() > 0;
        public void AllowClearPartialRoute(bool allow) 
        { 
            SignalObject.AllowClearPartialRoute(allow ? 1 : 0); 
        }

        public int SignalNumClearAhead { get => SignalObject.SignalNumClearAheadActive; set => SignalObject.SetSignalNumClearAhead(value); }

        public int DefaultDrawState(SignalAspectState signalAspect)
        {
            return signalHead.DefaultDrawState(signalAspect);
        }

        public int GetDrawState(string name)
        {
            return signalHead.SignalType.DrawStates.ContainsKey(name) ? signalHead.SignalType.DrawStates[name].Index : -1;
        }

        public int SignalId => SignalObject.Index;

        public float ClockTimeS => (float)Simulator.Instance.GameTime;

        public class Timer
        {
            float EndValue;
            protected Func<float> CurrentValue { get; }
            public Timer(CsSignalScript script)
            {
                CurrentValue = () => script.ClockTimeS;
            }
            public float AlarmValue { get; private set; }
            public float RemainingValue => EndValue - CurrentValue();
            public bool Started { get; private set; }
            public void Setup(float alarmValue) { AlarmValue = alarmValue; }
            public void Start() { EndValue = CurrentValue() + AlarmValue; Started = true; }
            public void Stop() { Started = false; }
            public bool Triggered => Started && CurrentValue() >= EndValue;
        }

        protected CsSignalScript()
        {
        }

        public void SendSignalMessage(int signalId, string message)
        {
            if (signalId < 0 || signalId > Simulator.Instance.SignalEnvironment.Signals.Count) 
                return;
            foreach (SignalHead head in Simulator.Instance.SignalEnvironment.Signals[signalId].SignalHeads)
            {
                head.HandleSignalMessage(SignalObject.Index, message);
            }
        }

        public bool IsSignalFeatureEnabled(string signalFeature)
        {
            if (!EnumExtension.GetValue(signalFeature, out SignalSubType subType))
                subType = SignalSubType.None;
            return signalHead.VerifySignalFeature((int)subType);
        }

        public bool HasHead(int requiredHeadIndex)
        {
            return SignalObject.HasHead(requiredHeadIndex) == 1;
        }

        public int NextSignalId(string sigfn, int count = 0)
        {
            return SignalObject.NextNthSignalId(OrSignalTypes.Instance.FunctionTypes.FindIndex(i => StringComparer.OrdinalIgnoreCase.Equals(i, sigfn)), count + 1);
        }

        public int OppositeSignalId(string sigfn)
        {
            return SignalObject.OppositeSignalId(SigFnIndex(sigfn));
        }

        public int RequiredNormalSignalId(string normalSubtype)
        {
            return SignalObject.FindRequiredNormalSignal(OrSignalTypes.Instance.NormalSubTypes.IndexOf(normalSubtype));
        }

        public bool IdSignalHasNormalSubtype(int id, string normalSubtype)
        {
            return signalHead.SignalHasNormalSubtypeById(id, OrSignalTypes.Instance.NormalSubTypes.IndexOf(normalSubtype)) == 1;
        }

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

        public SignalAspectState DistMultiSigMR(string sigfnA, string sigfnB, bool mostRestrictiveHead = true)
        {
            if (mostRestrictiveHead) 
                return signalHead.MRSignalMultiOnRoute(SigFnIndex(sigfnA), SigFnIndex(sigfnB));
            return signalHead.LRSignalMultiOnRoute(SigFnIndex(sigfnA), SigFnIndex(sigfnB));
        }

        public SignalAspectState IdSignalAspect(int id, string sigfn)
        {
            return signalHead.SignalLRById(id, SigFnIndex(sigfn));
        }

        public int IdSignalLocalVariable(int id, int key)
        {
            return signalHead.LocalVariableBySignalId(id, key);
        }

        public bool IdSignalEnabled(int id)
        {
            return signalHead.SignalEnabledById(id) > 0;
        }

        public bool TrainHasCallOn(bool allowOnNonePlatform = true, bool allowAdvancedSignal = false)
        {
            return SignalObject.TrainHasCallOn(allowOnNonePlatform, allowAdvancedSignal);
        }

        public bool TrainRequiresSignal(int signalId, float reqPositionM)
        {
            return SignalObject.RequiresNextSignal(signalId, (int)reqPositionM);
        }

        public bool ApproachControlPosition(float reqPositionM, bool forced = false)
        {
            return SignalObject.ApproachControlPosition((int)reqPositionM, forced);
        }

        public bool ApproachControlSpeed(float reqPositionM, float requestedSpeedMpS)
        {
            return SignalObject.ApproachControlSpeed((int)reqPositionM, (int)requestedSpeedMpS);
        }

        public bool ApproachControlNextStop(float reqPositionM, float requestedSpeedMpS)
        {
            return SignalObject.ApproachControlNextStop((int)reqPositionM, (int)requestedSpeedMpS);
        }

        public void ApproachControlLockClaim()
        {
            SignalObject.LockClaim();
        }

        public SignalBlockState RouteClearedToSignal(int signalId, bool allowCallOn = false)
        {
            return SignalObject.RouteClearedToSignal(signalId, allowCallOn);
        }

        internal void AttachToHead(SignalHead signalHead)
        {
            this.signalHead = signalHead;
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
    }
}
