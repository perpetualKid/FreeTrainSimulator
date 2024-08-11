using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.State
{

    [MemoryPackable]
    public sealed partial class NotchSaveState : SaveStateBase
    {
        public float CurrentValue { get; set; }
        public bool Smooth { get; set; }
        public ControllerState NotchStateType { get; set; }
    }

    [MemoryPackable]
    public sealed partial class ControllerSaveState : SaveStateBase
    {
        public ControllerType ControllerType { get; set; }
        public float CurrentValue { get; set; }
        public float MinimumValue { get; set; }
        public float MaximumValue { get; set; }
        public float StepSize { get; set; }
        public int NotchIndex { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<NotchSaveState> NotchStates { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public bool CheckNeutral { get; set; }
        public CruiseControllerPosition ControllerPosition { get; set; }
        public CruiseControllerPosition CurrentPosition { get; set; }
        public double ElapsedTimer { get; set; }
        public bool EmergencyBrake { get; set; }
        public bool Braking { get; set; }
        public bool AnyKeyPressed { get; set; }
        public bool AddPowerMode { get; set; }
        public bool StateChanged { get; set; }
        public bool TcsEmergencyBrake { get; set; }
        public bool TcsFullServiceBrake { get; set; }
    }
}
