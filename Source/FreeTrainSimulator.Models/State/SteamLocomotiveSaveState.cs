using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.State
{
    [MemoryPackable]
    public sealed partial class SteamLocomotiveSaveState : SaveStateBase
    {
        public bool RestoreGame { get; set; }
        public ControllerSaveState CutoffController { get; set; }
        public ControllerSaveState Injector1Controller { get; set; }
        public ControllerSaveState Injector2Controller { get; set; }
        public ControllerSaveState BlowerController { get; set; }
        public ControllerSaveState DamperController { get; set; }
        public ControllerSaveState FireboxDoorController { get; set; }
        public ControllerSaveState FiringRateController { get; set; }
        public ControllerSaveState SmallEjectorController { get; set; }
        public ControllerSaveState LargeEjectorController { get; set; }
        public double BoilerHeatOut { get; set; }
        public double BoilerHeatIn { get; set; }
        public double PreviousBoilerHeatOut { get; set; }
        public double PreviousBoilerHeatSmoothed { get; set; }
        public double BurnRate { get; set; }
        public double TenderCoalMass { get; set; }
        public double RestoredMaxTotalCombinedWaterVolume { get; set; }
        public double RestoredCombinedTenderWaterVolume { get; set; }
        public double CumulativeWaterConsumption { get; set; }
        public double CurrentAuxTenderWaterVolume { get; set; }
        public double CurrentLocoTenderWaterVolume { get; set; }
        public double PreviousTenderWaterVolume { get; set; }
        public bool SteamAuxTenderCoupled { get; set; }
        public double CylinderSteamUsage { get; set; }
        public double BoilerHeat { get; set; }
        public double BoilerMass { get; set; }
        public double BoilerPressure { get; set; }
        public bool CoalExhausted { get; set; }
        public bool WaterExhausted { get; set; }
        public bool FireExhausted { get; set; }
        public bool FuelBoost { get; set; }
        public double FuelBoostOnTimer { get; set; }
        public double FuelBoostResetTimer { get; set; }
        public bool FuelBoostReset { get; set; }
        public bool Injector1Active { get; set; }
        public double Injector1Fraction { get; set; }
        public bool Injector2Active { get; set; }
        public double Injector2Fraction { get; set; }
        public bool InjectorLockedOut { get; set; }
        public double InjectorLockOutTime { get; set; }
        public double InjectorLockOutResetTime { get; set; }
        public double WaterTempNew { get; set; }
        public double BkwDelta { get; set; }
        public double WaterFraction { get; set; }
        public double BoilerSteamHeat { get; set; }
        public double BoilerWaterHeat { get; set; }
        public double BoilerWaterDensity { get; set; }
        public double BoilerSteamDensity { get; set; }
        public double Evaporation { get; set; }
        public double FireMass { get; set; }
        public double FlueTemp { get; set; }
        public float SteamGearPosition { get; set; }
        public double FuelBurnRateSmoothed { get; set; }
        public double BoilerHeatSmoothed { get; set; }
        public double FuelRateSmoothed { get; set; }
    }
}
