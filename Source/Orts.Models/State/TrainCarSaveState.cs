using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TrainCarSaveState : SaveStateBase
    {
        // TrainCar properties
        public int UId { get; set; }
        public bool Flipped { get; set; }
        public string CarId { get; set; }
        public float MotiveForce { get; set; }
        public float FrictionForce { get; set; }
        public float Speed { get; set; }
        public float CouplerSlack { get; set; }
        public HeadLightState HeadLightState { get; set; }
        public string OriginalConsist { get; set; }
        public float PreviousTiltingZRot { get; set; }
        public bool BrakesStuck { get; set; }
        public bool CarHeatingInitialized { get; set; }
        public double SteamHoseLeakRateRandom { get; set; }
        public double CurrentCompartmentHeat { get; set; }
        public double SteamHeatMainPipeSteamPressure { get; set; }
        public bool CompartmentHeaterOn { get; set; }
        public BrakeSystemSaveState BrakeSystemSaveState { get; set; }
        // Wagon properties
        public WagonSaveState WagonSaveState { get; set; }
        // EoT
        public EndOfTrainSaveState EndOfTrainSaveState { get; set; }
        // Control Trailer
        public ControlTrailerSaveState ControlTrailerSaveState { get; set; }
        public DieselLocomotiveSaveState DieselLocomotiveSaveState { get; set; }
        // Locomotive
        public LocomotiveSaveState LocomotiveSaveState { get; set; }
    }
}
