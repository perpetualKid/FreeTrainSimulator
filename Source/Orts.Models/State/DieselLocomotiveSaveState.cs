using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class DieselLocomotiveSaveState : SaveStateBase
    {
        public ControllerSaveState GearboxControllerSaveState { get; set; }
        public float DieselLevel { get; set; }
        public Collection<DieselEngineSaveState> EngineSaveStates { get; set; }
    }
}
