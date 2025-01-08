using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class DoorSaveState : SaveStateBase
    {
        public DoorState DoorState { get; set; }
        public bool Locked { get; set; }
    }
}
