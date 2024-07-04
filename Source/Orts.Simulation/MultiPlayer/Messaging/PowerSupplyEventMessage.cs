using FreeTrainSimulator.Common;

using MemoryPack;

using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class PowerSupplyEventMessage : MultiPlayerMessageContent
    {
        public PowerSupplyEvent PowerSupplyEvent { get; set; }
        public int CarIndex { get; set; }

        public override void HandleMessage()
        {
            Train train = MultiPlayerManager.FindPlayerTrain(User);
            if (train == null)
                return;

            if (CarIndex > -1)
                train.SignalEvent(PowerSupplyEvent, CarIndex);
            else
                train.SignalEvent(PowerSupplyEvent);
        }
    }
}
