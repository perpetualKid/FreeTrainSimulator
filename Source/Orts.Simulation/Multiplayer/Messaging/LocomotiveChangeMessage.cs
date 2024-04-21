using System;

using MemoryPack;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class LocomotiveChangeMessage : MultiPlayerMessageContent
    {
        public override void HandleMessage()
        {
            throw new NotImplementedException();
        }
    }
}
