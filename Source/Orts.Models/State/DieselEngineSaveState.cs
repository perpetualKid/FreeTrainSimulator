using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class DieselEngineSaveState: SaveStateBase
    {
        public DieselEngineState DieselEngineState { get; set; }
        public float Rpm { get; set; }
        public float OutputPower { get; set; }
        public float DieselTemperature { get; set; }
        public bool GovernorEnabled { get; set; }
        public GearBoxSaveState GearboxSaveState { get; set; }

    }
}
