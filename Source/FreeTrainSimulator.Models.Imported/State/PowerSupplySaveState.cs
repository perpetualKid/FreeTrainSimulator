using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class PowerSupplySaveState : SaveStateBase
    {
        public CommandSwitchSaveState BatterySwitchState { get; set; }
        public CommandSwitchSaveState MasterKeyState { get; set; }
        public CommandSwitchSaveState ElectricTrainSupplySwitchState { get; set; }
        public bool FrontElectricTrainSupplyCableConnected { get; set; }
        public PowerSupplyState ElectricTrainSupplyState { get; set; }
        public PowerSupplyState LowVoltagePowerSupplyState { get; set; }
        public PowerSupplyState BatteryState { get; set; }
        public PowerSupplyState VentilationState { get; set; }
        public PowerSupplyState HeatingState { get; set; }
        public PowerSupplyState AirConditioningState { get; set; }
        public PowerSupplyState MainPowerSupplyState { get; set; }
        public PowerSupplyState AuxiliaryPowerSupplyState { get; set; }
        public PowerSupplyState CabPowerSupplyState { get; set; }
        public float HeatFlowRate { get; set; }
        public string ScriptName { get; set; }
        public CircuitBreakerSaveState CircuitBreakerState { get; set; }
        public CircuitBreakerSaveState TractionCutOffRelayState { get; set; }
    }
}
