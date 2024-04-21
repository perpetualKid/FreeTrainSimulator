using System;

using MemoryPack;

using Orts.Formats.Msts;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class SignalHeadState
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
}
