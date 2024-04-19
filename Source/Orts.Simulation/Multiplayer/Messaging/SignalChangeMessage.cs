using MemoryPack;

using Orts.Common;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class SignalChangeMessage : MultiPlayerMessageContent
    {
        public SignalState SignalState { get; set; }

        public int SignalIndex { get; set; }

        [MemoryPackConstructor]
        public SignalChangeMessage() { }

        public SignalChangeMessage(ISignal signal) 
        { 
            if (signal is Signal baseSignal)
            {
                SignalIndex = baseSignal.Index;
                SignalState = signal.State;
            }
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
