﻿using FreeTrainSimulator.Common;

using MemoryPack;

using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class SignalResetMessage : MultiPlayerMessageContent
    {
        public override void HandleMessage()
        {
            if (multiPlayerManager.IsDispatcher)
            {
                Train train = MultiPlayerManager.FindPlayerTrain(User);
                train?.RequestSignalPermission(Direction.Forward);
                MultiPlayerManager.Broadcast(new SignalStateMessage(true));
            }
        }
    }
}
