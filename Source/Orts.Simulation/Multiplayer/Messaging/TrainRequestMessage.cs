﻿using MemoryPack;

using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class TrainRequestMessage : MultiPlayerMessageContent
    {
        public int TrainNumber { get; set; }

        public override void HandleMessage()
        {
            if (multiPlayerManager.IsDispatcher)
            {
                foreach (Train train in Simulator.Instance.Trains)
                {
                    if (train != null && train.Number == TrainNumber) //found it, broadcast to everyone
                    {
                        MultiPlayerManager.Broadcast(new TrainUpdateMessage(User, train));
                        break;
                    }
                }
            }
        }
    }
}
