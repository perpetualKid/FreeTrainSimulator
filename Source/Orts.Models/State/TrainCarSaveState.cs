using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TrainCarSaveState: SaveStateBase
    {
        public Collection<PantographSaveState> PantographSaveState { get; set; }
        public DoorSaveState[] DoorStates { get; set; }
    }
}
