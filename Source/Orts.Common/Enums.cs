// COPYRIGHT 2009, 2011 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace Orts.Common
{
    [Description("Reverser")]
    public enum MidpointDirection
    {
        [Description("Reverse")] Reverse = -1,
        [Description("N")] N = 0,
        [Description("Forward")] Forward = 1,
    }

    [Description("Rotation")]
    public enum Rotation
    {
        [Description("CounterClockwise")] CounterClockwise = -1,
        [Description("None")] None = 0,
        [Description("Clockwise")] Clockwise = 1,
    }

    [Description("Separator")]
#pragma warning disable CA1008 // Enums should have zero value
    public enum SeparatorChar
#pragma warning restore CA1008 // Enums should have zero value
    {
        [Description("Comma")] Comma = ',',
        [Description("Semicolon")] Semicolon = ';',
        [Description("Tab")] Tab = '\t',
        [Description("Space")] Space = ' '
    };

    [Description("Measurement units Preference")]
    public enum MeasurementUnit
    {
        [Description("Route")] Route,
        [Description("Player's location")] System,
        [Description("Metric")] Metric,
        [Description("Imperial US")] US,
        [Description("Imperial UK")] UK,
    }

    [Description("Pressure units Preference")]
    public enum PressureUnit
    {
        [Description("Automatic")] Automatic,
        /// <summary>bar</summary>
        [Description("bar")] Bar,
        /// <summary>Pounds Per Square Inch</summary>
        [Description("psi")] Psi,
        /// <summary>Inches Mercury</summary>
        [Description("inHg")] InHg,
        /// <summary>Mass-force per square centimetres</summary>
        [Description("kgf/cm²")] Kgfcm2,
    }

    [Description("Speed Unit Preference")]
    public enum SpeedUnit
    {
        [Description("Route")] Route,
        [Description("m/s")] Mps,
        [Description("km/h")] Kmph,
        [Description("mph")] Mph,
    }

    //Time, Train Speed, Max Speed, Signal Aspect, Elevation, Direction, Distance Travelled, Control Mode, Throttle, Brake, Dyn Brake, Gear
    [Flags]
    public enum EvaluationLogContents
    {
        [Description("None")] None = 0,
        [Description("Time")] Time = 1 << 0,
        [Description("Train Speed")] Speed = 1 << 1,
        [Description("Max. Speed")] MaxSpeed = 1 << 2,
        [Description("SignalAspect")] SignalAspect = 1 << 3,
        [Description("Track Elevation")] Elevation = 1 << 4,
        [Description("Direction")] Direction = 1 << 5,
        [Description("Distance Travelled")] Distance = 1 << 6,
        [Description("Control Mode")] ControlMode = 1 << 7,
        [Description("Throttle %")] Throttle = 1 << 8,
        [Description("Brake Cyl Press")] Brake = 1 << 9,
        [Description("Dyn Brake %")] DynBrake = 1 << 10,
        [Description("Gear Setting")] Gear = 1 << 11,
    }

    public enum ClockType
    {
        Unknown,
        Analog,
        Digital,
    }

    public enum TrackMonitorSignalAspect
    {
        None,
        Clear2,
        Clear1,
        Approach3,
        Approach2,
        Approach1,
        Restricted,
        StopAndProceed,
        Stop,
        Permission,
    }

    public enum Direction
    {
        Forward,
        Backward,
    }

    public enum TrackDirection
    {
        Ahead,
        Reverse,
    }

    public enum SwitchDirection
    {
        Facing,
        Trailing,
    }

    public enum CurveDirection
    {
        Straight,
        Left,
        Right,
    }

    public enum CabSetting
    {
        Name,       // name of control
        Off,        // 2 or 3 state control/reset/initialise
        Neutral,    // 2 or 3 state control
        On,         // 2 or 3 state control/apply/change
        Decrease,   // continuous control
        Increase,   // continuous control
        Warn1,
        Warn2,
        Range1,     // sub-range
        Range2,
        Range3,
        Range4,
    }

    public enum ConfirmLevel
    {
        [Description("None")] None,
        [Description("Information")] Information,
        [Description("Warning")] Warning,
        [Description("Error")] Error,
        [Description("Message")] Message,
    };

    #region CabControl
    public enum CabControl
    {
        None,
        // Power
        Reverser,
        Throttle,
        Wheelslip,
        // Electric Power
        Power,
        Pantograph1,
        Pantograph2,
        Pantograph3,
        Pantograph4,
        CircuitBreakerClosingOrder,
        CircuitBreakerOpeningOrder,
        CircuitBreakerClosingAuthorization,
        // Diesel Power
        PlayerDiesel,
        HelperDiesel,
        DieselFuel,
        SteamHeatBoilerWater,
        TractionCutOffRelayClosingOrder,
        // Steam power
        SteamLocomotiveReverser,
        Regulator,
        Injector1,
        Injector2,
        BlowdownValve,
        Blower,
        SteamHeat,
        Damper,
        FireboxDoor,
        FiringRate,
        FiringIsManual,
        FireShovelfull,
        CylinderCocks,
        CylinderCompound,
        LargeEjector,
        SmallEjector,
        VacuumExhauster,
        TenderCoal,
        TenderWater,
        // General
        WaterScoop,
        // Braking
        TrainBrake,
        EngineBrake,
        BrakemanBrake,
        DynamicBrake,
        EmergencyBrake,
        BailOff,
        InitializeBrakes,
        Handbrake,
        Retainers,
        BrakeHose,
        QuickRelease,
        Overcharge,
        // Cab Devices
        Sander,
        Alerter,
        Horn,
        Whistle,
        Bell,
        Headlight,
        CabLight,
        Wipers,
        ChangeCab,
        Odometer,
        Battery,
        MasterKey,
        // Train Devices
        DoorsLeft,
        DoorsRight,
        Mirror,
        // Track Devices
        SwitchAhead,
        SwitchBehind,
        // Simulation
        SimulationSpeed,
        Uncouple,
        Activity,
        Replay,
        GearBox,
        SignalMode,
        // Freight Load
        FreightLoad,
        CabRadio,
    }
    #endregion

    public enum TrainEvent
    {
        None,
        BatterySwitchOff,
        BatterySwitchOn,
        BatterySwitchCommandOff,
        BatterySwitchCommandOn,
        BellOff,
        BellOn,
        BlowdownValveToggle,
        BlowerChange,
        BrakesStuck,
        CabLightSwitchToggle,
        CabRadioOn,
        CabRadioOff,
        CircuitBreakerOpen,
        CircuitBreakerClosing,
        CircuitBreakerClosed,
        CircuitBreakerClosingOrderOff,
        CircuitBreakerClosingOrderOn,
        CircuitBreakerOpeningOrderOff,
        CircuitBreakerOpeningOrderOn,
        CircuitBreakerClosingAuthorizationOff,
        CircuitBreakerClosingAuthorizationOn,
        CompressorOff,
        CompressorOn,
        ControlError,
        Couple,
        CoupleB, // NOTE: Currently not used in Open Rails.
        CoupleC, // NOTE: Currently not used in Open Rails.
        CrossingClosing,
        CrossingOpening,
        CylinderCocksToggle,
        CylinderCompoundToggle,
        DamperChange,
        Derail1, // NOTE: Currently not used in Open Rails.
        Derail2, // NOTE: Currently not used in Open Rails.
        Derail3, // NOTE: Currently not used in Open Rails.
        DoorClose,
        DoorOpen,
        DynamicBrakeChange,
        DynamicBrakeIncrease, // NOTE: Currently not used in Open Rails.
        DynamicBrakeOff,
        ElectricTrainSupplyOff,
        ElectricTrainSupplyOn,
        ElectricTrainSupplyCommandOff,
        ElectricTrainSupplyCommandOn,
        EngineBrakeChange,
        EngineBrakePressureDecrease,
        EngineBrakePressureIncrease,
        EnginePowerOff,
        EnginePowerOn,
        FireboxDoorChange,
        FireboxDoorOpen,
        FireboxDoorClose,
        FuelTowerDown,
        FuelTowerTransferEnd,
        FuelTowerTransferStart,
        FuelTowerUp,
        GearDown,
        GearUp,
        GenericEvent1,
        GenericEvent2,
        GenericEvent3,
        GenericEvent4,
        GenericEvent5,
        GenericEvent6,
        GenericEvent7,
        GenericEvent8,
        GenericItem1On,
        GenericItem1Off,
        GenericItem2On,
        GenericItem2Off,
        HeadlightDim,
        HeadlightOff,
        HeadlightOn,
        HornOff,
        HornOn,
        LargeEjectorChange,
        LightSwitchToggle,
        MasterKeyOff,
        MasterKeyOn,
        MirrorClose,
        MirrorOpen,
        MovingTableMovingEmpty,
        MovingTableMovingLoaded,
        MovingTableStopped,
        Pantograph1Down,
        PantographToggle,
        Pantograph1Up,
        Pantograph2Down,
        Pantograph2Up,
        Pantograph3Down,
        Pantograph3Up,
        Pantograph4Down,
        Pantograph4Up,
        PermissionDenied,
        PermissionGranted,
        PermissionToDepart,
        PowerKeyOff,
        PowerKeyOn,
        ResetWheelSlip,
        ReverserChange,
        ReverserToForwardBackward,
        ReverserToNeutral,
        SanderOff,
        SanderOn,
        SemaphoreArm,
        ServiceRetentionButtonOff,
        ServiceRetentionButtonOn,
        ServiceRetentionCancellationButtonOff,
        ServiceRetentionCancellationButtonOn,
        SmallEjectorChange,
        SteamHeatChange,
        SteamPulse1,
        SteamPulse2,
        SteamPulse3,
        SteamPulse4,
        SteamPulse5,
        SteamPulse6,
        SteamPulse7,
        SteamPulse8,
        SteamPulse9,
        SteamPulse10,
        SteamPulse11,
        SteamPulse12,
        SteamPulse13,
        SteamPulse14,
        SteamPulse15,
        SteamPulse16,
        SteamSafetyValveOff,
        SteamSafetyValveOn,
        TakeScreenshot,
        ThrottleChange,
        TractionCutOffRelayOpen,
        TractionCutOffRelayClosing,
        TractionCutOffRelayClosed,
        TractionCutOffRelayClosingOrderOff,
        TractionCutOffRelayClosingOrderOn,
        TractionCutOffRelayOpeningOrderOff,
        TractionCutOffRelayOpeningOrderOn,
        TractionCutOffRelayClosingAuthorizationOff,
        TractionCutOffRelayClosingAuthorizationOn,
        TrainBrakeChange,
        TrainBrakePressureDecrease,
        TrainBrakePressureIncrease,
        TrainControlSystemActivate,
        TrainControlSystemAlert1,
        TrainControlSystemAlert2,
        TrainControlSystemDeactivate,
        TrainControlSystemInfo1,
        TrainControlSystemInfo2,
        TrainControlSystemPenalty1,
        TrainControlSystemPenalty2,
        TrainControlSystemWarning1,
        TrainControlSystemWarning2,
        Uncouple,
        UncoupleB, // NOTE: Currently not used in Open Rails.
        UncoupleC, // NOTE: Currently not used in Open Rails.
        VacuumExhausterOn,
        VacuumExhausterOff,
        VigilanceAlarmOff,
        VigilanceAlarmOn,
        VigilanceAlarmReset,
        WaterInjector1Off,
        WaterInjector1On,
        WaterInjector2Off,
        WaterInjector2On,
        WaterScoopDown,
        WaterScoopUp,
        WiperOff,
        WiperOn,

        TrainBrakePressureStoppedChanging,
        EngineBrakePressureStoppedChanging,
        BrakePipePressureIncrease,
        BrakePipePressureDecrease,
        BrakePipePressureStoppedChanging,
        CylinderCocksOpen,
        CylinderCocksClose,
        SecondEnginePowerOff,
        SecondEnginePowerOn,

        HotBoxBearingOn,
        HotBoxBearingOff,

        BoilerBlowdownOn,
        BoilerBlowdownOff,

        WaterScoopRaiseLower,
        WaterScoopBroken,

        SteamGearLeverToggle,
        AIFiremanSoundOn,
        AIFiremanSoundOff,

        GearPosition0,
        GearPosition1,
        GearPosition2,
        GearPosition3,
        GearPosition4,
        GearPosition5,
        GearPosition6,
        GearPosition7,
        GearPosition8,

        LargeEjectorOn,
        LargeEjectorOff,
        SmallEjectorOn,
        SmallEjectorOff,

        PowerConverterOff,
        PowerConverterOn,
        VentilationOff,
        VentilationLow,
        VentilationHigh,
        HeatingOff,
        HeatingOn,
        AirConditioningOff,
        AirConditioningOn,

        OverchargeBrakingOn,
        OverchargeBrakingOff,
    }

    public enum PowerSupplyEvent
    {
        QuickPowerOn,
        QuickPowerOff,
        TogglePlayerEngine,
        ToggleHelperEngine,
        CloseBatterySwitch,
        OpenBatterySwitch,
        CloseBatterySwitchButtonPressed,
        CloseBatterySwitchButtonReleased,
        OpenBatterySwitchButtonPressed,
        OpenBatterySwitchButtonReleased,
        TurnOnMasterKey,
        TurnOffMasterKey,
        RaisePantograph,
        LowerPantograph,
        CloseCircuitBreaker,
        OpenCircuitBreaker,
        CloseCircuitBreakerButtonPressed,
        CloseCircuitBreakerButtonReleased,
        OpenCircuitBreakerButtonPressed,
        OpenCircuitBreakerButtonReleased,
        GiveCircuitBreakerClosingAuthorization,
        RemoveCircuitBreakerClosingAuthorization,
        StartEngine,
        StopEngine,
        CloseTractionCutOffRelay,
        OpenTractionCutOffRelay,
        CloseTractionCutOffRelayButtonPressed,
        CloseTractionCutOffRelayButtonReleased,
        OpenTractionCutOffRelayButtonPressed,
        OpenTractionCutOffRelayButtonReleased,
        GiveTractionCutOffRelayClosingAuthorization,
        RemoveTractionCutOffRelayClosingAuthorization,
        ServiceRetentionButtonPressed,
        ServiceRetentionButtonReleased,
        ServiceRetentionCancellationButtonPressed,
        ServiceRetentionCancellationButtonReleased,
        SwitchOnElectricTrainSupply,
        SwitchOffElectricTrainSupply,
        StallEngine,
    }

    [Description("PowerSupply")]
    public enum PowerSupplyType
    {
        [Description("Steam")] Steam,
        [Description("DieselMechanical")] DieselMechanical,
        [Description("DieselHydraulic")] DieselHydraulic,
        [Description("DieselElectric")] DieselElectric,
        [Description("Electric")] Electric,
        [Description("DualMode")] DualMode,
        [Description("ControlCar")] ControlCar,
    }

    [Description("PowerSupply")]
    public enum PowerSupplyState
    {
        [Description("Unavailable")] Unavailable = -1,
        [Description("Off")] PowerOff,
        [Description("On ongoing")] PowerOnOngoing,
        [Description("On")] PowerOn
    }

    [Description("Pantograph")]
    public enum PantographState
    {
        [Description("Unavailable")] Unavailable = -1,
        [Description("Down")] Down,
        [Description("Lowering")] Lowering,
        [Description("Raising")] Raising,
        [Description("Up")] Up
    }

    [Description("Engine")]
    public enum DieselEngineState
    {
        [Description("Unavailable")] Unavailable = -1,
        [Description("Stopped")] Stopped,
        [Description("Stopping")] Stopping,
        [Description("Starting")] Starting,
        [Description("Running")] Running
    }

    [Description("CircuitBreaker")]
    public enum CircuitBreakerState
    {
        [Description("Unavailable")] Unavailable = -1,
        [Description("Open")] Open,
        [Description("Closing")] Closing,
        [Description("Closed")] Closed
    }

    [Description("TractionCutOffRelay")]
    public enum TractionCutOffRelayState
    {
        [Description("Unavailable")] Unavailable = -1,
        [Description("Open")] Open,
        [Description("Closing")] Closing,
        [Description("Closed")] Closed
    }

    public enum PowerSupplyMode
    {
        Diesel,
        Pantograph,
    }

    public enum DieselTransmissionType
    {
        Legacy,
        Electric,
        Hydraulic,
        Mechanic,
        Hydromechanic,
    }

    public enum BrakeControllerEvent
    {
        /// <summary>
        /// Starts the pressure increase (may have a target value)
        /// </summary>
        StartIncrease,
        /// <summary>
        /// Stops the pressure increase
        /// </summary>
        StopIncrease,
        /// <summary>
        /// Starts the pressure decrease (may have a target value)
        /// </summary>
        StartDecrease,
        /// <summary>
        /// Stops the pressure decrease
        /// </summary>
        StopDecrease,
        /// <summary>
        /// Sets the value of the brake controller using a RailDriver peripheral (must have a value)
        /// </summary>
        SetCurrentPercent,
        /// <summary>
        /// Sets the current value of the brake controller (must have a value)
        /// </summary>
        SetCurrentValue,
        /// <summary>
        /// Starts a full quick brake release.
        /// </summary>
        FullQuickRelease,
        /// <summary>
        /// Starts a pressure decrease to zero (may have a target value)
        /// </summary>
        StartDecreaseToZero
    }

    //TrainBrakesController
    [Description("Brake Controller")]
    public enum ControllerState
    {
        // MSTS values (DO NOT CHANGE THE ORDER !)
        [Description("")]
        Dummy,                  // Dummy
        [Description("Release")]
        Release,                // ReleaseStart 
        [Description("Quick Release")]
        FullQuickRelease,       // FullQuickReleaseStart
        [Description("Running")]
        Running,                // RunningStart 
        [Description("Neutral")]
        Neutral,                // NeutralhandleOffStart
        [Description("Self Lap")]
        SelfLap,                // SelfLapStart 
        [Description("Lap")]
        Lap,                    // HoldLapStart 
        [Description("Apply")]
        Apply,                  // ApplyStart 
        [Description("EPApply")]
        EPApply,                // EPApplyStart 
        [Description("Service")]
        GSelfLap,               // GraduatedSelfLapLimitedStart
        [Description("Service")]
        GSelfLapH,              // GraduatedSelfLapLimitedHoldStart
        [Description("Suppression")]
        Suppression,            // SuppressionStart 
        [Description("Cont. Service")]
        ContServ,               // ContinuousServiceStart 
        [Description("Full Service")]
        FullServ,               // FullServiceStart 
        [Description("Emergency")]
        Emergency,              // EmergencyStart

        // Extra MSTS values
        [Description("Minimum Reduction")]
        MinimalReduction,       // MinimalReductionStart,
        [Description("Hold")]
        Hold,                   // HoldStart

        // OR values
        [Description("Straight Brake Release On")]
        StraightReleaseOn,      // TrainBrakesControllerStraightBrakingReleaseOnStart
        [Description("Straight Brake Release Off")]
        StraightReleaseOff,     // TrainBrakesControllerStraightBrakingReleaseOffStart
        [Description("Straight Brake Release")]
        StraightRelease,      // TrainBrakesControllerStraightBrakingReleaseStart
        [Description("Straight Brake Lap")]
        StraightLap,          // TrainBrakesControllerStraightBrakingLapStart
        [Description("Straight Brake Apply")]
        StraightApply,        // TrainBrakesControllerStraightBrakingApplyStart
        [Description("Straight Brake Apply All")]
        StraightApplyAll,     // TrainBrakesControllerStraightBrakingApplyAllStart
        [Description("Straight Brake Emergency")]
        StraightEmergency,    // TrainBrakesControllerStraightBrakingEmergencyStart

        [Description("Overcharge")]
        Overcharge,             // Overcharge
        [Description("Emergency Braking Push Button")]
        EBPB,                   // Emergency Braking Push Button
        [Description("TCS Emergency Braking")]
        TCSEmergency,           // TCS Emergency Braking
        [Description("TCS Full Service Braking")]
        TCSFullServ,            // TCS Full Service Braking
        [Description("Vac. Cont. Service")]
        VacContServ,            // VacuumContinuousServiceStart
        [Description("Vac. Apply Cont.Service")]
        VacApplyContServ,       // TrainBrakesControllerVacuumApplyContinuousServiceStart
        [Description("Manual Braking")]
        ManualBraking,          // BrakemanBrakesControllerManualBraking
        [Description("Notch")]
        BrakeNotch,             // EngineBrakesControllerBrakeNotchStart
        [Description("EP Service")]
        EPOnly,                 // TrainBrakesControllerEPOnlyStart
        [Description("EP Full Service")]
        EPFullServ,             // TrainBrakesControllerEPFullServiceStart
        [Description("Slow service")]
        SlowService,            // TrainBrakesControllerSlowServiceStart
        [Description("SME service")]
        SMEOnly,            // TrainBrakesControllerSMEOnlyStart
        [Description("SME Full service")]
        SMEFullServ,        // TrainBrakesControllerSMEFullServiceStart
        [Description("SME Self Lap")]
        SMESelfLap,         // TrainBrakesControllerSMEHoldStart
        [Description("SME Release Start")]
        SMEReleaseStart,    // TrainBrakesControllerSMEReleaseStart
    }

    public enum TrainControlMode
    {
        [Description("Auto Signal")] AutoSignal,
        [Description("Node")] AutoNode,
        [Description("Manual")] Manual,
        [Description("Explorer")] Explorer,
        [Description("OutOfControl")] OutOfControl,
        [Description("Inactive")] Inactive,
        [Description("Turntable")] TurnTable,
        [Description("Unknown")] Undefined,
    }

    public enum TCSEvent
    {
        /// <summary>
        /// Emergency braking requested by simulator (train is out of control).
        /// </summary>
        EmergencyBrakingRequestedBySimulator,
        /// <summary>
        /// Emergency braking released by simulator.
        /// </summary>
        EmergencyBrakingReleasedBySimulator,
        /// <summary>
        /// Manual reset of the train's out of control mode.
        /// </summary>
        ManualResetOutOfControlMode,
        /// <summary>
        /// Reset request by pressing the alerter button.
        /// </summary>
        AlerterPressed,
        /// <summary>
        /// Alerter button was released.
        /// </summary>
        AlerterReleased,
        /// <summary>
        /// Internal reset request by touched systems other than the alerter button.
        /// </summary>
        AlerterReset,
        /// <summary>
        /// Internal reset request by the reverser.
        /// </summary>
        ReverserChanged,
        /// <summary>
        /// Internal reset request by the throttle controller.
        /// </summary>
        ThrottleChanged,
        /// <summary>
        /// Internal reset request by the gear box controller.
        /// </summary>
        GearBoxChanged,
        /// <summary>
        /// Internal reset request by the train brake controller.
        /// </summary>
        TrainBrakeChanged,
        /// <summary>
        /// Internal reset request by the engine brake controller.
        /// </summary>
        EngineBrakeChanged,
        /// <summary>
        /// Internal reset request by the brakeman brake controller.
        /// </summary>
        BrakemanBrakeChanged,
        /// <summary>
        /// Internal reset request by the dynamic brake controller.
        /// </summary>
        DynamicBrakeChanged,
        /// <summary>
        /// Internal reset request by the horn handle.
        /// </summary>
        HornActivated,
        /// <summary>
        /// Generic TCS button pressed.
        /// </summary>
        GenericTCSButtonPressed,
        /// <summary>
        /// Generic TCS button released.
        /// </summary>
        GenericTCSButtonReleased,
        /// <summary>
        /// Circuit breaker has been closed.
        /// </summary>
        CircuitBreakerClosed,
        /// <summary>
        /// Circuit breaker has been opened.
        /// </summary>
        CircuitBreakerOpen,
        /// <summary>
        /// Save request.
        /// </summary>
        Save,
        /// <summary>
        /// Restore request.
        /// </summary>
        Restore,
        /// <summary>
        /// Generic TCS switch toggled off.
        /// </summary>
        GenericTCSSwitchOff,
        /// <summary>
        /// Generic TCS switch toggled on.
        /// </summary>
        GenericTCSSwitchOn,
        /// <summary>
        /// Traction cut-off relay has been closed.
        /// </summary>
        TractionCutOffRelayClosed,
        /// <summary>
        /// Traction cut-off relay has been opened.
        /// </summary>
        TractionCutOffRelayOpen,
    }

    /// <summary>
    /// Controls what color the speed monitoring display uses.
    /// </summary>
    public enum MonitoringStatus
    {
        /// <summary>
        /// Grey color. No speed restriction is ahead.
        /// </summary>
        Normal,
        /// <summary>
        /// White color. Pre-indication, that the next signal is restricted. No manual intervention is needed yet.
        /// </summary>
        Indication,
        /// <summary>
        /// Yellow color. Next signal is restricted, driver should start decreasing speed.
        /// (Please note, it is not for indication of a "real" overspeed. In this state the locomotive still runs under the actual permitted speed.)
        /// </summary>
        Overspeed,
        /// <summary>
        /// Orange color. The locomotive is very close to next speed restriction, driver should start strong braking immediately.
        /// </summary>
        Warning,
        /// <summary>
        /// Red color. Train control system intervention speed. Computer has to apply full service or emergency brake to maintain speed restriction.
        /// </summary>
        Intervention,
    }

    public enum UpdateCheckFrequency
    {
        [Description("Manually check for updates")] Never = -1,
        [Description("Check for updates on each start")] Always = 0,
        [Description("Check for updates once a day")] Daily,
        [Description("Check for updates once a week")] Weekly,
        [Description("Check for updates every other week")] Biweekly,
        [Description("Check for updates every month")] Monthly,
    }

    public enum ActivityMode
    {
        Introductory = 0,
        Player = 2,
        Tutorial = 3,
    }

    [Description("Season")]
    public enum SeasonType
    {
        [Description("Spring")] Spring = 0,
        [Description("Summer")] Summer,
        [Description("Autumn")] Autumn,
        [Description("Winter")] Winter
    }

    [Description("Weather")]
    public enum WeatherType
    {
        [Description("Clear")] Clear = 0,
        [Description("Snow")] Snow,
        [Description("Rain")] Rain
    }

    [Description("Difficulty")]
    public enum Difficulty
    {
        [Description("Easy")] Easy = 0,
        [Description("Medium")] Medium,
        [Description("Hard")] Hard
    }

    public enum MapViewItemSettings
    {
        // types should be placed in the order they are drawn (overlap), top level last
        Grid,
        Sidings,
        Platforms,
        Tracks,
        EndNodes,
        JunctionNodes,
        CrossOvers,
        Roads,
        RoadEndNodes,
        CarSpawners,
        LevelCrossings,
        RoadCrossings,
        SidingNames,
        PlatformNames,
        StationNames,
        SpeedPosts,
        MilePosts,
        Signals,
        OtherSignals,
        Hazards,
        Pickups,
        SoundRegions,
        Paths,
        PathEnds,
        PathIntermediates,
        PathJunctions,
        PathReversals,
        Empty,
        TrainNames,
    }

    public enum TrainType
    {
        Player,
        PlayerIntended,
        Static,
        Ai,
        AiNotStarted,
        AiAutoGenerated,
        Remote,
        AiPlayerDriven,     //Player is on board and is currently driving train
        AiPlayerHosting,    //Player is on board, but train is currently autopiloted
        AiIncorporated,     //AI train is incorporated in other train
    }

    public enum RemoteControlGroup
    {
        /// -1: unconnected, 0: sync/front group, 1: async/rear group
        [Description("———")]
        Unconnected = -1,
        [Description("Sync")]
        FrontGroupSync = 0,
        [Description("Async")]
        RearGroupAsync = 1
    }

    public enum DistributedPowerMode
    {
        //Distributed Power mode: -1: Brake, 0: Idle, 1: Traction
        Brake = -1,
        Idle = 0,
        Traction = 1,
    }
    /// <summary>
    /// A type of horn pattern used by AI trains at level crossings.
    /// </summary>
    public enum LevelCrossingHornPattern
    {
        /// <summary>
        /// A single blast just before the crossing.
        /// </summary>
#pragma warning disable CA1720 // Identifier contains type name
        Single,
#pragma warning restore CA1720 // Identifier contains type name

        /// <summary>
        /// A long-long-short-long pattern used in the United States and Canada.
        /// </summary>
        US,
    }

    public enum WindowSetting
    {
        Location,
        Size,
    }

    public enum DispatcherWindowType
    {
        DebugScreen,
        SignalChange,
        SwitchChange,
        SignalState,
        HelpWindow,
        Settings,
        TrainInfo,
    }

    public enum ViewerWindowType
    {
        DebugOverlay,
        QuitWindow,
        PauseOverlay,
        HelpWindow,
        ActivityWindow,
        CompassWindow,
        SwitchWindow,
        EndOfTrainDeviceWindow,
        NextStationWindow,
        DetachTimetableTrainWindow,
        TrainListWindow,
        MultiPlayerWindow,
        DrivingTrainWindow,
        DistributedPowerWindow,
        TrainOperationsWindow,
        CarOperationsWindow,
        TrackMonitorWindow,
        MultiPlayerMessagingWindow,
        NotificationOverlay,
        CarIdentifierOverlay,
        LocationsOverlay,
        TrackItemOverlay,
    }

    public enum FourCharAcronym
    {
        // DPU
        [Description("FLOW")] Flow,
        [Description("LOAD")] Load,
        [Description("GRUP")] LocoGroup,
        [Description("OILP")] OilPressure,
        [Description("POWR")] Power,
        [Description("RMT")] Remote,
        [Description("RPM")] Rpm,
        [Description("REVR")] Reverser,
        [Description("STAT")] Status,
        [Description("TEMP")] Temperature,
        [Description("THRO")] Throttle,
        [Description("TIME")] Time,
        [Description("TRAC")] TractiveEffort,
        [Description("BRKP")] BrakePressure,
        [Description("LOCS")] Locomotives,

        //Train Driving
        [Description("AIFR")] AiFireman,
        [Description("AUTO")] AutoPilot,
        [Description("PRES")] BoilerPressure,
        [Description("WATR")] BoilerWaterGlass,
        [Description("LEVL")] BoilerWaterLevel,
        [Description("CIRC")] CircuitBreaker,
        [Description("CCOK")] CylinderCocks,
        [Description("DIR")] Direction,
        [Description("DRLC")] DerailCoefficent,
        [Description("DERL")] Derailment,
        [Description("DOOR")] DoorsOpen,
        [Description("ENGN")] Engine,
        [Description("FIRE")] FireMass,
        [Description("GEAR")] FixedGear,
        [Description("FUEL")] FuelLevel,
        [Description("GEAR")] Gear,
        [Description("GRAD")] Gradient,
        [Description("GRAT")] GrateLimit,
        [Description("PANT")] Pantographs,
        [Description("REGL")] Regulator,
        [Description("RPLY")] Replay,
        [Description("SAND")] Sander,
        [Description("SPED")] Speed,
        [Description("STEM")] SteamUsage,
        [Description("WHEL")] Wheel,
        [Description("ODO")] Odometer,

        //Braking
        [Description("BTRN")] TrainBrake,
        [Description("BDYN")] DynamicBrake,
        [Description("BLOC")] EngineBrake,
        [Description("RETN")] Retainer,
        [Description("ER")] EQReservoir,
        [Description("MR")] MainReservoir,
        [Description("BC")] BrakeCylinder,
        [Description("EOTW")] EndOfTrainCar,
        [Description("1STW")] FirstTrainCar,

        //Diesel and Electric
        [Description("TSUP")] ElectricTrainSupply,
        [Description("TRAC")] TractionCutOffRelay,
        [Description("MAST")] MasterKey,
        [Description("BATT")] BatterySwitch,

        [Description("EOTD")] EotDevice,
    }
}
