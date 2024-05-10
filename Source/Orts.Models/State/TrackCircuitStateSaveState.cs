using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct TrainReservationItem
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public readonly int TrainNumber;
        public readonly Direction Direction;

        public TrainReservationItem(int trainNumber, Direction direction)
        {
            Direction = direction;
            TrainNumber = trainNumber;
        }
    }

    [MemoryPackable]
    public sealed partial class TrackCircuitStateSaveState: SaveStateBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<TrainReservationItem> OccupationStates { get; set; }
        public TrainReservationItem? TrainReservation { get; set; }
        public int SignalReserved { get; set; }
        public Collection<TrainReservationItem> TrainPreReserved { get; set; }
        public Collection<TrainReservationItem> TrainClaimed { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public bool Forced { get; set; }
    }
}
