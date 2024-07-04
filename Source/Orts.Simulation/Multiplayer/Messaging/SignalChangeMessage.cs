using System;

using FreeTrainSimulator.Common;

using MemoryPack;

using Orts.Simulation.Signalling;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class SignalChangeMessage : MultiPlayerMessageContent
    {
        public SignalState SignalState { get; set; }
        public int SignalIndex { get; set; }

        [MemoryPackConstructor]
        public SignalChangeMessage() { }

        public SignalChangeMessage(ISignal signal, SignalState targetState)
        {
            ArgumentNullException.ThrowIfNull(signal, nameof(signal));
            SignalIndex = (signal as Signal).Index;
            SignalState = targetState;
        }

        public override void HandleMessage()
        {
            if (multiPlayerManager.IsDispatcher && !multiPlayerManager.aiderList.Contains(User))
                return; //client will ignore it, also if not an aider, will ignore it

            Signal signal = Simulator.Instance.SignalEnvironment.Signals[SignalIndex];
            switch (SignalState)
            {
                case SignalState.Clear:
                    signal.HoldState = SignalHoldState.None;
                    break;

                case SignalState.Lock:
                    signal.HoldState = SignalHoldState.ManualLock;
                    break;

                case SignalState.Approach:
                    signal.RequestApproachAspect();
                    break;

                case SignalState.Manual:
                    signal.RequestLeastRestrictiveAspect();
                    break;

                case SignalState.CallOn:
                    signal.SetManualCallOn(true);
                    break;
            }
        }
    }
}
