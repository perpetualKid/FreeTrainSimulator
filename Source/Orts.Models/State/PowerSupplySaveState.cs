using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class PowerSupplySaveState: SaveStateBase
    {
        public CommandSwitchSaveState BatterySwitchState { get; set; }
        public bool FrontElectricTrainSupplyCableConnected { get; set; }
        public PowerSupplyState ElectricTrainSupplyState { get; set; }
        public PowerSupplyState LowVoltagePowerSupplyState { get; set; }
        public PowerSupplyState BatteryState { get; set; }
        public PowerSupplyState VentilationState { get; set; }
        public PowerSupplyState HeatingState { get; set; }
        public PowerSupplyState AirConditioningState { get; set; }
        public float HeatFlowRate { get; set; }
    }
}
