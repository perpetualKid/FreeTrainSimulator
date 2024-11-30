using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class BrakeSystemSaveState : SaveStateBase
    {
        public float BrakeLine1Pressure { get; set; }
        public float BrakeLine2Pressure { get; set; }
        public float BrakeLine3Pressure { get; set; }
        public float HandBrake { get; set; }
        public float ReleaseRate { get; set; }
        public float RetainerPressureThreshold { get; set; }
        public float AutoCylinderPressure { get; set; }
        public float AuxReservoirPressure { get; set; }
        public float EmergencyReservoirPressure { get; set; }
        public float ControlReservoirPressure { get; set; }
        public float CylinderPressure { get; set; }
        public float VacuumReservoirPressure { get; set; }
        public float FullServicePressure { get; set; }
        public ValveState TripleValveState { get; set; }
        public bool FrontBrakeHoseConnected { get; set; }
        public bool AngleCockAOpen { get; set; }
        public bool AngleCockBOpen { get; set; }
        public bool BleedOffValveOpen { get; set; }
        public ValveState HoldingValveState { get; set; }
        public float CylinderVolume { get; set; }
        public bool BailOffOn { get; set; }
        public float ManualBraking { get; set; }
    }
}
