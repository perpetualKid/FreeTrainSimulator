using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct TrainReservationItemSaveState
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public readonly int TrainNumber;
        public readonly Direction Direction;

        public TrainReservationItemSaveState(int trainNumber, Direction direction)
        {
            Direction = direction;
            TrainNumber = trainNumber;
        }
    }

    [MemoryPackable]
    public sealed partial class TrackCircuitStateSaveState: SaveStateBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<TrainReservationItemSaveState> OccupationStates { get; set; }
        public TrainReservationItemSaveState? TrainReservation { get; set; }
        public int SignalReserved { get; set; }
        public Collection<TrainReservationItemSaveState> TrainPreReserved { get; set; }
        public Collection<TrainReservationItemSaveState> TrainClaimed { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public bool Forced { get; set; }
    }
}
