using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class LocomotiveSaveState : SaveStateBase
    {
        public bool Bell { get; set; }
        public bool Sander { get; set; }
        public bool Wiper { get; set; }
        public bool VacuumExhauster { get; set; }
        public double OdometerResetPosition { get; set; }
        public bool OdometerCountingForward { get; set; }
        public bool OdometerCountingUp { get; set; }
        public bool OdometerVisible { get; set; }
        public float MainReservoirPressure { get; set; }
        public bool CompressorActive { get; set; }
        public float VacuumMainReservoirVacuum { get; set; }
        public bool VacuumExhausterActive { get; set; }
        public float TrainBrakePipeLeak { get; set; }
        public float AverageForce { get; set; }
        public float AxleSpeed { get; set; }
        public bool CabLight { get; set; }
        public bool RearCab { get; set; }
        public float PowerReduction { get; set; }
        public bool ScoopBroken { get; set; }
        public bool WaterScoopDown { get; set; }
        public float CurrentTrackSandBoxCapacity { get; set; }
        public double AdhesionFilter { get; set; }
        public bool GenericItem1 { get; set; }
        public bool GenericItem2 { get; set; }
        public double CalculatedCarHeaterSteamUsage { get; set; }
        public RemoteControlGroup RemoteControlGroup { get; set; }
        public int DistributedPowerUnitId { get; set; }
        public int PreviousGearBoxNotch { get; set; }
        public int PreviousChangedGearBoxNotch { get; set; }
        public double CurrentLocomotiveSteamHeatBoilerWaterCapacity { get; set; }
        public ControllerSaveState ThrottleController { get; set; }
        public ControllerSaveState TrainBrakeController { get; set; }
        public ControllerSaveState EngineBrakeController { get; set; }
        public ControllerSaveState BrakemanBrakeController { get; set; }
        public ControllerSaveState DynamicBrakeController { get; set; }
        public ControllerSaveState SteamHeatController { get; set; }
        public ScriptedTrainControlSystemSaveState TrainControlSystemSaveState { get; set; }
        public TrainAxleSaveState AxleSaveState { get; set; }
        public CruiseControlSaveState CruiseControlSaveState { get; set; }
        public PowerSupplySaveState PowerSupplySaveState { get; set; }
        // Control Trailer
        public ControlTrailerSaveState ControlTrailerSaveState { get; set; }
        public DieselLocomotiveSaveState DieselLocomotiveSaveState { get; set; }
        public SteamLocomotiveSaveState SteamLocomotiveSaveState { get; set; }
    }
}
