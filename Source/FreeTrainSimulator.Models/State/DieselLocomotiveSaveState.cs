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
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<DieselEngineSaveState> EngineSaveStates { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
