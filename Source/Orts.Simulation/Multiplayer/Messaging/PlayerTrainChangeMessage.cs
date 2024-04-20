using System;

using MemoryPack;

using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class PlayerTrainChangeMessage : MultiPlayerMessageContent
    {
        public int TrainNumber { get; set; }
        public string LocomotiveId { get; set; }

        [MemoryPackConstructor]
        public PlayerTrainChangeMessage() { }

        public PlayerTrainChangeMessage(Train train)
        { 
            ArgumentNullException.ThrowIfNull(train, nameof(train));

            TrainNumber = train.Number;
            LocomotiveId = train.LeadLocomotive != null ? train.LeadLocomotive.CarID : "NA";
        }

        public override void HandleMessage()
        {
            MultiPlayerManager.OnlineTrains.SwitchPlayerTrain(this);

            if (multiPlayerManager.IsDispatcher)
            {
                multiPlayerManager.PlayerAdded = true;
            }
        }
    }
}
