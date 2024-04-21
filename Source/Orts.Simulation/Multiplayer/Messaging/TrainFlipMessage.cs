using MemoryPack;

using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class TrainFlipMessage : TrainStateBaseMessage
    {
        public bool ReverseMultiUnit { get; set; }

        [MemoryPackConstructor]
        public TrainFlipMessage() { }

        public TrainFlipMessage(Train train, bool reverseMultiUnit) : base(train)
        {
            ReverseMultiUnit = reverseMultiUnit;
        }

        public override void HandleMessage()
        {
            foreach (Train train in Simulator.Instance.Trains)
            {
                if (train.Number == TrainNumber)
                {
                    train.ToDoUpdate(TrackNodeIndex, RearLocation.TileX, RearLocation.TileZ, RearLocation.Location.X, RearLocation.Location.Z, DistanceTravelled, Speed, MultiUnitDirection, TrainDirection, Length, true, ReverseMultiUnit);
                    return;
                }
            }
        }
    }
}
