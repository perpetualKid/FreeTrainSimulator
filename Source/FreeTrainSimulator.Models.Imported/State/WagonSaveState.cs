﻿using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class WagonSaveState : SaveStateBase
    {
        public string WagonFile { get; set; }
        public Vector3 SoundValues { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<PantographSaveState> PantographSaveStates { get; set; }
        public Collection<DoorSaveState> DoorSaveStates { get; set; }
        public Collection<CouplerSaveState> CouplerSaveStates { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public float Friction { get; set; }
        public float DavisA { get; set; }
        public float DavisB { get; set; }
        public float DavisC { get; set; }
        public float StandstillFriction { get; set; }
        public float MergeSpeedFriction { get; set; }
        public bool BelowMergeSpeed { get; set; }
        public float Mass { get; set; }
        public float MaxBrakeForce { get; set; }
        public float MaxHandbrakeForce { get; set; }
        public double CurrentSteamHeatBoilerFuelCapacity { get; set; }
        public double CurrentCarSteamHeatBoilerWaterCapacity { get; set; }
        public double CarInsideTemp { get; set; }
        public bool WheelBrakeSlideProtectionActive { get; set; }
        public double WheelBrakeSlideProtectionTimer { get; set; }
        public float DerailClimbDistance { get; set; }
        public bool DerailPossible { get; set; }
        public bool DerailExpected { get; set; }
        public double DerailElapsedTime { get; set; }
        public PowerSupplySaveState PowerSupplySaveStates { get; set; }
        public FreightAnimationsSetSaveState FreightAnimationsSaveState { get; set; }
        public ControllerSaveState WeightControllerSaveState { get; set; }

    }
}
