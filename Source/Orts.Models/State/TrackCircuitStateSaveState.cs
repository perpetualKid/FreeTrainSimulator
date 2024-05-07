using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TrackCircuitStateSaveState: SaveStateBase
    {
        public readonly struct TrainReservationItem
        {
            public readonly int TrainNumber;
            public readonly Direction Direction;

            public TrainReservationItem(int trainNumber, Direction direction)
            {
                Direction = direction;
                TrainNumber = trainNumber;
            }
        }

        public Collection<TrainReservationItem> OccupationStates { get; set; }
        public TrainReservationItem? TrainReservation { get; set; }
        public int SignalReserved { get; set; }
        public Collection<TrainReservationItem> TrainPreReserved { get; set; }
        public Collection<TrainReservationItem> TrainClaimed { get; set; }
        public bool Forced { get; set; }

    }
}
