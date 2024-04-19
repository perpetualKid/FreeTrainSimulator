using System;
using System.Collections.ObjectModel;

using MemoryPack;

using Orts.Formats.Msts;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class SignalHeadState
    {
        public int SignalIndex { get; set; }
        public int TdbIndex { get; set; }
        public SignalAspectState SignalAspect { get; set; }
        public int DrawState { get; set; }
        public string CustomAspect { get; set; }

        [MemoryPackConstructor]
        public SignalHeadState() { }

        public SignalHeadState(SignalHead signalHead)
        {
            ArgumentNullException.ThrowIfNull(signalHead, nameof(signalHead));

            SignalIndex = signalHead.MainSignal.Index;
            TdbIndex = signalHead.TDBIndex;
            SignalAspect = signalHead.SignalIndicationState;
            DrawState = signalHead.DrawState;
            CustomAspect = signalHead.TextSignalAspect;
        }
    }

    [MemoryPackable]
    public partial class SignalStateMessage : MultiPlayerMessageContent
    {
        public Collection<SignalHeadState> SignalStates { get; } = new Collection<SignalHeadState>();

        [MemoryPackConstructor]
        public SignalStateMessage()
        { }

        public SignalStateMessage(bool initialize)
        {
            if (initialize)
            {
                foreach (Signal signal in Simulator.Instance.SignalEnvironment.Signals)
                {
                    if (signal != null && (signal.SignalType == SignalCategory.Signal || signal.SignalType == SignalCategory.SpeedSignal) && signal.SignalHeads != null)
                    {
                        foreach (SignalHead signalHead in signal.SignalHeads)
                        {
                            SignalStates.Add(new SignalHeadState(signalHead));
                        }
                    }
                }
            }
        }

        public override void HandleMessage()
        {
            foreach (SignalHeadState headState in SignalStates)
            {
                Signal signal = Simulator.Instance.SignalEnvironment.Signals[headState.SignalIndex];
                foreach (SignalHead signalHead in signal.SignalHeads)
                {
                    if (signalHead.TDBIndex == headState.TdbIndex)
                    {
                        signalHead.SignalIndicationState = headState.SignalAspect;
                        signalHead.DrawState = headState.DrawState;
                        signalHead.TextSignalAspect = headState.CustomAspect;
                        break;
                    }
                }
            }
        }
    }
}
