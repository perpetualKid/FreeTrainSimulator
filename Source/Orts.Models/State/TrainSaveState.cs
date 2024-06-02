using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TrainSaveState : SaveStateBase
    {
        public Collection<TrainCarSaveState> TrainCars { get; set; }
    }
}
