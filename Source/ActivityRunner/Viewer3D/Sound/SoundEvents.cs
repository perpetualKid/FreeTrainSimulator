using FreeTrainSimulator.Common;

namespace Orts.ActivityRunner.Viewer3D.Sound
{
    public enum SoundEventSource
    {
        None,
        Car,
        Crossing,
        FuelTower,
        InGame,
        Signal,
        Turntable,
        ContainerCrane,
    }

    public static class SoundEvent
    {

        // PLEASE DO NOT EDIT THESE FUNCTIONS without references and testing!
        // These numbers are the MSTS sound triggers and must match
        // MSTS/MSTSBin behaviour whenever possible. NEVER return values for
        // non-MSTS events when passed an MSTS Source.

        public static TrainEvent From(SoundEventSource source, int eventID)
        {
            return source switch
            {
                SoundEventSource.Car => eventID switch
                {
                    // Calculated from inspection of existing engine .sms files and extensive testing.
                    // Event 1 is unused in MSTS.
                    2 => TrainEvent.DynamicBrakeIncrease,
                    3 => TrainEvent.DynamicBrakeOff,
                    4 => TrainEvent.SanderOn,
                    5 => TrainEvent.SanderOff,
                    6 => TrainEvent.WiperOn,
                    7 => TrainEvent.WiperOff,
                    8 => TrainEvent.HornOn,
                    9 => TrainEvent.HornOff,
                    10 => TrainEvent.BellOn,
                    11 => TrainEvent.BellOff,
                    12 => TrainEvent.CompressorOn,
                    13 => TrainEvent.CompressorOff,
                    14 => TrainEvent.TrainBrakePressureIncrease,
                    15 => TrainEvent.ReverserChange,
                    16 => TrainEvent.ThrottleChange,
                    17 => TrainEvent.TrainBrakeChange,// Event 17 only works first time in MSTS.
                    18 => TrainEvent.EngineBrakeChange,// Event 18 only works first time in MSTS; MSTSBin fixes this.
                                                       // Event 19 is unused in MSTS.
                    20 => TrainEvent.DynamicBrakeChange,
                    21 => TrainEvent.EngineBrakePressureIncrease,// Event 21 is defined in sound files but never used in MSTS.
                    22 => TrainEvent.EngineBrakePressureDecrease,// Event 22 is defined in sound files but never used in MSTS.
                                                                 // Event 23 is unused in MSTS.
                                                                 // Event 24 is unused in MSTS.
                                                                 // MSTSBin codes (documented at http://mstsbin.uktrainsim.com/)
                    23 => TrainEvent.EnginePowerOn,
                    24 => TrainEvent.EnginePowerOff,
                    // Event 25 is possibly a vigilance reset in MSTS sound files but is never used.
                    // Event 26 is a sander toggle in MSTS sound files but is never used.
                    27 => TrainEvent.WaterInjector2On,
                    28 => TrainEvent.WaterInjector2Off,
                    // Event 29 is unused in MSTS.
                    30 => TrainEvent.WaterInjector1On,
                    31 => TrainEvent.WaterInjector1Off,
                    32 => TrainEvent.DamperChange,
                    33 => TrainEvent.BlowerChange,
                    34 => TrainEvent.CylinderCocksToggle,
                    // Event 35 is unused in MSTS.
                    36 => TrainEvent.FireboxDoorChange,
                    37 => TrainEvent.LightSwitchToggle,
                    38 => TrainEvent.WaterScoopDown,
                    39 => TrainEvent.WaterScoopUp,
                    40 => TrainEvent.FireboxDoorOpen,// Used in default steam locomotives (Scotsman and 380)
                    41 => TrainEvent.FireboxDoorClose,
                    42 => TrainEvent.SteamSafetyValveOn,
                    43 => TrainEvent.SteamSafetyValveOff,
                    44 => TrainEvent.SteamHeatChange,// Event 44 only works first time in MSTS.
                    45 => TrainEvent.Pantograph1Up,
                    46 => TrainEvent.Pantograph1Down,
                    47 => TrainEvent.PantographToggle,
                    48 => TrainEvent.VigilanceAlarmReset,
                    // Event 49 is unused in MSTS.
                    // Event 50 is unused in MSTS.
                    // Event 51 is an engine brake of some kind in MSTS sound files but is never used.
                    // Event 52 is unused in MSTS.
                    // Event 53 is a train brake normal apply in MSTS sound files but is never used.
                    54 => TrainEvent.TrainBrakePressureDecrease,// Event 54 is a train brake emergency apply in MSTS sound files but is actually a train brake pressure decrease.
                                                                // Event 55 is unused in MSTS.
                    56 => TrainEvent.VigilanceAlarmOn,
                    57 => TrainEvent.VigilanceAlarmOff,// Event 57 is triggered constantly in MSTS when the vigilance alarm is off.
                    58 => TrainEvent.Couple,
                    59 => TrainEvent.CoupleB,
                    60 => TrainEvent.CoupleC,
                    61 => TrainEvent.Uncouple,
                    62 => TrainEvent.UncoupleB,
                    63 => TrainEvent.UncoupleC,
                    // Event 64 is unused in MSTS.
                    // MSTSBin codes (documented at http://mstsbin.uktrainsim.com/)
                    66 => TrainEvent.Pantograph2Up,
                    67 => TrainEvent.Pantograph2Down,
                    // ORTS only Events
                    101 => TrainEvent.GearUp,// for gearbox based engines
                    102 => TrainEvent.GearDown,// for gearbox based engines
                    103 => TrainEvent.ReverserToForwardBackward,// reverser moved to forward or backward position
                    104 => TrainEvent.ReverserToNeutral,// reversed moved to neutral
                    105 => TrainEvent.DoorOpen,// door opened; propagated to all locos and wagons of the consist
                    106 => TrainEvent.DoorClose,// door closed; propagated to all locos and wagons of the consist
                    107 => TrainEvent.MirrorOpen,
                    108 => TrainEvent.MirrorClose,
                    109 => TrainEvent.TrainControlSystemInfo1,
                    110 => TrainEvent.TrainControlSystemInfo2,
                    111 => TrainEvent.TrainControlSystemActivate,
                    112 => TrainEvent.TrainControlSystemDeactivate,
                    113 => TrainEvent.TrainControlSystemPenalty1,
                    114 => TrainEvent.TrainControlSystemPenalty2,
                    115 => TrainEvent.TrainControlSystemWarning1,
                    116 => TrainEvent.TrainControlSystemWarning2,
                    117 => TrainEvent.TrainControlSystemAlert1,
                    118 => TrainEvent.TrainControlSystemAlert2,
                    119 => TrainEvent.CylinderCompoundToggle,// Locomotive switched to compound
                    120 => TrainEvent.BlowdownValveToggle,
                    121 => TrainEvent.SteamPulse1,
                    122 => TrainEvent.SteamPulse2,
                    123 => TrainEvent.SteamPulse3,
                    124 => TrainEvent.SteamPulse4,
                    125 => TrainEvent.SteamPulse5,
                    126 => TrainEvent.SteamPulse6,
                    127 => TrainEvent.SteamPulse7,
                    128 => TrainEvent.SteamPulse8,
                    129 => TrainEvent.SteamPulse9,
                    130 => TrainEvent.SteamPulse10,
                    131 => TrainEvent.SteamPulse11,
                    132 => TrainEvent.SteamPulse12,
                    133 => TrainEvent.SteamPulse13,
                    134 => TrainEvent.SteamPulse14,
                    135 => TrainEvent.SteamPulse15,
                    136 => TrainEvent.SteamPulse16,
                    137 => TrainEvent.CylinderCocksOpen,
                    138 => TrainEvent.CylinderCocksClose,
                    139 => TrainEvent.TrainBrakePressureStoppedChanging,
                    140 => TrainEvent.EngineBrakePressureStoppedChanging,
                    141 => TrainEvent.BrakePipePressureIncrease,
                    142 => TrainEvent.BrakePipePressureDecrease,
                    143 => TrainEvent.BrakePipePressureStoppedChanging,
                    145 => TrainEvent.WaterScoopRaiseLower,
                    146 => TrainEvent.WaterScoopBroken,
                    147 => TrainEvent.SteamGearLeverToggle,
                    148 => TrainEvent.AIFiremanSoundOn,
                    149 => TrainEvent.AIFiremanSoundOff,
                    150 => TrainEvent.CircuitBreakerOpen,
                    151 => TrainEvent.CircuitBreakerClosing,
                    152 => TrainEvent.CircuitBreakerClosed,
                    153 => TrainEvent.CircuitBreakerClosingOrderOn,
                    154 => TrainEvent.CircuitBreakerClosingOrderOff,
                    155 => TrainEvent.CircuitBreakerOpeningOrderOn,
                    156 => TrainEvent.CircuitBreakerOpeningOrderOff,
                    157 => TrainEvent.CircuitBreakerClosingAuthorizationOn,
                    158 => TrainEvent.CircuitBreakerClosingAuthorizationOff,
                    159 => TrainEvent.LargeEjectorChange,
                    160 => TrainEvent.SmallEjectorChange,
                    161 => TrainEvent.CabLightSwitchToggle,
                    162 => TrainEvent.CabRadioOn,
                    163 => TrainEvent.CabRadioOff,
                    164 => TrainEvent.BrakesStuck,
                    165 => TrainEvent.VacuumExhausterOn,
                    166 => TrainEvent.VacuumExhausterOff,
                    167 => TrainEvent.SecondEnginePowerOn,
                    168 => TrainEvent.SecondEnginePowerOff,
                    169 => TrainEvent.Pantograph3Up,
                    170 => TrainEvent.Pantograph3Down,
                    171 => TrainEvent.Pantograph4Up,
                    172 => TrainEvent.Pantograph4Down,
                    173 => TrainEvent.HotBoxBearingOn,
                    174 => TrainEvent.HotBoxBearingOff,
                    175 => TrainEvent.BoilerBlowdownOn,
                    176 => TrainEvent.BoilerBlowdownOff,
                    181 => TrainEvent.GenericEvent1,
                    182 => TrainEvent.GenericEvent2,
                    183 => TrainEvent.GenericEvent3,
                    184 => TrainEvent.GenericEvent4,
                    185 => TrainEvent.GenericEvent5,
                    186 => TrainEvent.GenericEvent6,
                    187 => TrainEvent.GenericEvent7,
                    188 => TrainEvent.GenericEvent8,
                    189 => TrainEvent.BatterySwitchOn,
                    190 => TrainEvent.BatterySwitchOff,
                    191 => TrainEvent.BatterySwitchCommandOn,
                    192 => TrainEvent.BatterySwitchCommandOff,
                    193 => TrainEvent.MasterKeyOn,
                    194 => TrainEvent.MasterKeyOff,
                    195 => TrainEvent.ServiceRetentionButtonOn,
                    196 => TrainEvent.ServiceRetentionButtonOff,
                    197 => TrainEvent.ServiceRetentionCancellationButtonOn,
                    198 => TrainEvent.ServiceRetentionCancellationButtonOff,
                    200 => TrainEvent.GearPosition0,
                    201 => TrainEvent.GearPosition1,
                    202 => TrainEvent.GearPosition2,
                    203 => TrainEvent.GearPosition3,
                    204 => TrainEvent.GearPosition4,
                    205 => TrainEvent.GearPosition5,
                    206 => TrainEvent.GearPosition6,
                    207 => TrainEvent.GearPosition7,
                    208 => TrainEvent.GearPosition8,
                    210 => TrainEvent.LargeEjectorOn,
                    211 => TrainEvent.LargeEjectorOff,
                    212 => TrainEvent.SmallEjectorOn,
                    213 => TrainEvent.SmallEjectorOff,
                    214 => TrainEvent.TractionCutOffRelayOpen,
                    215 => TrainEvent.TractionCutOffRelayClosing,
                    216 => TrainEvent.TractionCutOffRelayClosed,
                    217 => TrainEvent.TractionCutOffRelayClosingOrderOn,
                    218 => TrainEvent.TractionCutOffRelayClosingOrderOff,
                    219 => TrainEvent.TractionCutOffRelayOpeningOrderOn,
                    220 => TrainEvent.TractionCutOffRelayOpeningOrderOff,
                    221 => TrainEvent.TractionCutOffRelayClosingAuthorizationOn,
                    222 => TrainEvent.TractionCutOffRelayClosingAuthorizationOff,
                    223 => TrainEvent.ElectricTrainSupplyOn,
                    224 => TrainEvent.ElectricTrainSupplyOff,
                    225 => TrainEvent.ElectricTrainSupplyCommandOn,
                    226 => TrainEvent.ElectricTrainSupplyCommandOff,
                    227 => TrainEvent.PowerConverterOn,
                    228 => TrainEvent.PowerConverterOff,
                    229 => TrainEvent.VentilationHigh,
                    230 => TrainEvent.VentilationLow,
                    231 => TrainEvent.VentilationOff,
                    232 => TrainEvent.HeatingOn,
                    233 => TrainEvent.HeatingOff,
                    234 => TrainEvent.AirConditioningOn,
                    235 => TrainEvent.AirConditioningOff,
                    240 => TrainEvent.GenericItem1On,
                    241 => TrainEvent.GenericItem1Off,
                    242 => TrainEvent.GenericItem2On,
                    243 => TrainEvent.GenericItem2Off,
                    250 => TrainEvent.OverchargeBrakingOn,
                    251 => TrainEvent.OverchargeBrakingOff,
                    // Cruise Control
                    298 => TrainEvent.LeverFromZero,
                    299 => TrainEvent.LeverToZero,
                    300 => TrainEvent.CruiseControlSpeedRegulator,
                    301 => TrainEvent.CruiseControlSpeedSelector,
                    302 => TrainEvent.CruiseControlMaxForce,
                    303 => TrainEvent.CruiseControlAlert,
                    304 => TrainEvent.CruiseControlAlert1,
                    //
                    _ => TrainEvent.None,
                },
                SoundEventSource.Crossing => eventID switch
                {
                    // Calculated from inspection of existing crossing.sms files.
                    3 => TrainEvent.CrossingClosing,
                    4 => TrainEvent.CrossingOpening,
                    _ => TrainEvent.None,
                },
                SoundEventSource.FuelTower => eventID switch
                {
                    // Calculated from inspection of existing *tower.sms files.
                    6 => TrainEvent.FuelTowerDown,
                    7 => TrainEvent.FuelTowerUp,
                    9 => TrainEvent.FuelTowerTransferStart,
                    10 => TrainEvent.FuelTowerTransferEnd,
                    _ => TrainEvent.None,
                },
                SoundEventSource.InGame => eventID switch
                {
                    // Calculated from inspection of existing ingame.sms files.
                    10 => TrainEvent.ControlError,
                    20 => TrainEvent.Derail1,
                    21 => TrainEvent.Derail2,
                    22 => TrainEvent.Derail3,
                    25 => 0,// TODO: What is this event?
                    60 => TrainEvent.PermissionToDepart,
                    61 => TrainEvent.PermissionGranted,
                    62 => TrainEvent.PermissionDenied,
                    _ => TrainEvent.None,
                },
                SoundEventSource.Signal => eventID switch
                {
                    // Calculated from inspection of existing signal.sms files.
                    1 => TrainEvent.SemaphoreArm,
                    _ => TrainEvent.None,
                },
                SoundEventSource.Turntable => eventID switch
                {
                    // related file is turntable.sms
                    1 => TrainEvent.MovingTableMovingEmpty,
                    2 => TrainEvent.MovingTableMovingLoaded,
                    3 => TrainEvent.MovingTableStopped,
                    _ => TrainEvent.None,
                },
                SoundEventSource.ContainerCrane => eventID switch
                {
                    // Can be different from crane to crane
                    1 => TrainEvent.CraneXAxisMove,
                    2 => TrainEvent.CraneXAxisSlowDown,
                    3 => TrainEvent.CraneYAxisMove,
                    4 => TrainEvent.CraneYAxisSlowDown,
                    5 => TrainEvent.CraneZAxisMove,
                    6 => TrainEvent.CraneZAxisSlowDown,
                    7 => TrainEvent.CraneYAxisDown,
                    _ => 0,
                },
                _ => TrainEvent.None,
            };
        }
    }
}
