using System.Diagnostics;

using FreeTrainSimulator.Common;

using MemoryPack;

using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class TrainEventMessage : MultiPlayerMessageContent
    {
        public TrainEvent TrainEvent { get; set; }
        public int CarIndex { get; set; }

        public override void HandleMessage()
        {
            Train train = MultiPlayerManager.FindPlayerTrain(User);
            if (train == null)
                return;

            if (CarIndex > -1)
            {
                if (CarIndex < train.Cars.Count)
                    train.Cars[CarIndex].SignalEvent(TrainEvent);
                else
                    Trace.TraceError($"Invalid TrainCar Index {CarIndex}");
            }
            else
            {
                train.SignalEvent(TrainEvent);
            }
        }
    }
}
