using System.Reflection.Metadata.Ecma335;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

using Microsoft.Xna.Framework.Input;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".keyboardsettings")]
    public partial record ProfileKeyboardSettingsModel : ProfileSettingsModelBase
    {
        private static ProfileKeyboardSettingsModel defaultModel;

        [MemoryPackInclude]
        private EnumArray<int, UserCommand> userCommands = new EnumArray<int, UserCommand>();

        public static ProfileKeyboardSettingsModel Default => defaultModel ??= new ProfileKeyboardSettingsModel();

        public override ProfileModel Parent => base.Parent as ProfileModel;

        [MemoryPackIgnore]
        public EnumArray<UserCommandInput, UserCommand> UserCommands { get; } = InitializeCommands();

        [MemoryPackIgnore]
        public KeyModifiers WindowTabCommandModifier => (UserCommands[UserCommand.DisplayNextWindowTab] is UserCommandModifierInput userCommandInput) ?
            ((userCommandInput.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandInput.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandInput.Shift ? KeyModifiers.Shift : KeyModifiers.None)) : KeyModifiers.None;

        [MemoryPackIgnore]
        public KeyModifiers CameraMoveFastModifier => (UserCommands[UserCommand.CameraMoveFast] is UserCommandModifierInput userCommandInput) ?
            ((userCommandInput.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandInput.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandInput.Shift ? KeyModifiers.Shift : KeyModifiers.None)) : KeyModifiers.None;
        [MemoryPackIgnore]
        public KeyModifiers CameraMoveSlowModifier => (UserCommands[UserCommand.CameraMoveSlow] is UserCommandModifierInput userCommandInput) ?
            ((userCommandInput.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandInput.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandInput.Shift ? KeyModifiers.Shift : KeyModifiers.None)) : KeyModifiers.None;
        [MemoryPackIgnore]
        public KeyModifiers GameSuspendOldPlayerModifier => (UserCommands[UserCommand.GameSuspendOldPlayer] is UserCommandModifierInput userCommandInput) ?
            ((userCommandInput.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandInput.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandInput.Shift ? KeyModifiers.Shift : KeyModifiers.None)) : KeyModifiers.None;
        [MemoryPackIgnore]
        public KeyModifiers GameSwitchWithMouseModifier => (UserCommands[UserCommand.GameSwitchWithMouse] is UserCommandModifierInput userCommandInput) ?
            ((userCommandInput.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandInput.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandInput.Shift ? KeyModifiers.Shift : KeyModifiers.None)) : KeyModifiers.None;

        #region Default Input Settings
        private static EnumArray<UserCommandInput, UserCommand> InitializeCommands()
        {
            EnumArray<UserCommandInput, UserCommand> commands = new EnumArray<UserCommandInput, UserCommand>();

            // All UserCommandModifierInput commands go here.
            commands[UserCommand.GameSwitchWithMouse] = new UserCommandModifierInput(KeyModifiers.Alt);
            commands[UserCommand.DisplayNextWindowTab] = new UserCommandModifierInput(KeyModifiers.Shift);
            commands[UserCommand.CameraMoveFast] = new UserCommandModifierInput(KeyModifiers.Shift);
            commands[UserCommand.GameSuspendOldPlayer] = new UserCommandModifierInput(KeyModifiers.Shift);
            commands[UserCommand.CameraMoveSlow] = new UserCommandModifierInput(KeyModifiers.Control);

            // Everything else goes here, sorted alphabetically please (and grouped by first word of name).
            commands[UserCommand.CameraBrakeman] = new UserCommandKeyInput(0x07);
            commands[UserCommand.CameraBrowseBackwards] = new UserCommandKeyInput(0x4F, KeyModifiers.Shift | KeyModifiers.Alt);
            commands[UserCommand.CameraBrowseForwards] = new UserCommandKeyInput(0x47, KeyModifiers.Shift | KeyModifiers.Alt);
            commands[UserCommand.CameraCab] = new UserCommandKeyInput(0x02);
            commands[UserCommand.CameraToggleThreeDimensionalCab] = new UserCommandKeyInput(0x02, KeyModifiers.Alt);
            commands[UserCommand.CameraCarFirst] = new UserCommandKeyInput(0x47, KeyModifiers.Alt);
            commands[UserCommand.CameraCarLast] = new UserCommandKeyInput(0x4F, KeyModifiers.Alt);
            commands[UserCommand.CameraCarNext] = new UserCommandKeyInput(0x49, KeyModifiers.Alt);
            commands[UserCommand.CameraCarPrevious] = new UserCommandKeyInput(0x51, KeyModifiers.Alt);
            commands[UserCommand.CameraFree] = new UserCommandKeyInput(0x09);
            commands[UserCommand.CameraHeadOutBackward] = new UserCommandKeyInput(0x4F);
            commands[UserCommand.CameraHeadOutForward] = new UserCommandKeyInput(0x47);
            commands[UserCommand.CameraJumpBackPlayer] = new UserCommandKeyInput(0x0A);
            commands[UserCommand.CameraJumpingTrains] = new UserCommandKeyInput(0x0A, KeyModifiers.Alt);
            commands[UserCommand.CameraJumpSeeSwitch] = new UserCommandKeyInput(0x22, KeyModifiers.Control | KeyModifiers.Alt);
            commands[UserCommand.CameraOutsideFront] = new UserCommandKeyInput(0x03);
            commands[UserCommand.CameraOutsideRear] = new UserCommandKeyInput(0x04);
            commands[UserCommand.CameraPanDown] = new UserCommandModifiableKeyInput(0x50, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);
            commands[UserCommand.CameraPanLeft] = new UserCommandModifiableKeyInput(0x4B, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);
            commands[UserCommand.CameraPanRight] = new UserCommandModifiableKeyInput(0x4D, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);
            commands[UserCommand.CameraPanUp] = new UserCommandModifiableKeyInput(0x48, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);
            commands[UserCommand.CameraPassenger] = new UserCommandKeyInput(0x06);
            commands[UserCommand.CameraPreviousFree] = new UserCommandKeyInput(0x09, KeyModifiers.Shift);
            commands[UserCommand.CameraReset] = new UserCommandKeyInput(0x09, KeyModifiers.Control);
            commands[UserCommand.CameraRotateDown] = new UserCommandModifiableKeyInput(0x50, KeyModifiers.Alt, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);
            commands[UserCommand.CameraRotateLeft] = new UserCommandModifiableKeyInput(0x4B, KeyModifiers.Alt, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);
            commands[UserCommand.CameraRotateRight] = new UserCommandModifiableKeyInput(0x4D, KeyModifiers.Alt, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);
            commands[UserCommand.CameraRotateUp] = new UserCommandModifiableKeyInput(0x48, KeyModifiers.Alt, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);
            commands[UserCommand.CameraScrollLeft] = new UserCommandModifiableKeyInput(0x4B, KeyModifiers.Alt);
            commands[UserCommand.CameraScrollRight] = new UserCommandModifiableKeyInput(0x4D, KeyModifiers.Alt);
            commands[UserCommand.CameraChangePassengerViewPoint] = new UserCommandKeyInput(0x06, KeyModifiers.Shift);
            commands[UserCommand.CameraToggleLetterboxCab] = new UserCommandKeyInput(0x02, KeyModifiers.Control);
            commands[UserCommand.CameraToggleShowCab] = new UserCommandKeyInput(0x02, KeyModifiers.Shift);
            commands[UserCommand.CameraTrackside] = new UserCommandKeyInput(0x05);
            ;
            commands[UserCommand.CameraSpecialTracksidePoint] = new UserCommandKeyInput(0x05, KeyModifiers.Shift);
            commands[UserCommand.CameraVibrate] = new UserCommandKeyInput(0x2F, KeyModifiers.Control);
            commands[UserCommand.CameraZoomIn] = new UserCommandModifiableKeyInput(0x49, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);
            commands[UserCommand.CameraZoomOut] = new UserCommandModifiableKeyInput(0x51, commands[UserCommand.CameraMoveFast], commands[UserCommand.CameraMoveSlow]);

            commands[UserCommand.ControlAIFireOn] = new UserCommandKeyInput(0x23, KeyModifiers.Alt);
            commands[UserCommand.ControlAIFireOff] = new UserCommandKeyInput(0x23, KeyModifiers.Control);
            commands[UserCommand.ControlAIFireReset] = new UserCommandKeyInput(0x23, KeyModifiers.Control | KeyModifiers.Alt);
            commands[UserCommand.ControlAlerter] = new UserCommandKeyInput(0x2C);
            commands[UserCommand.ControlBailOff] = new UserCommandKeyInput(0x35);
            commands[UserCommand.ControlBatterySwitchClose] = new UserCommandKeyInput(0x52);
            commands[UserCommand.ControlBatterySwitchOpen] = new UserCommandKeyInput(0x52, KeyModifiers.Control);
            commands[UserCommand.ControlBell] = new UserCommandKeyInput(0x30);
            commands[UserCommand.ControlBellToggle] = new UserCommandKeyInput(0x30, KeyModifiers.Shift);
            commands[UserCommand.ControlBlowerDecrease] = new UserCommandKeyInput(0x31, KeyModifiers.Shift);
            commands[UserCommand.ControlBlowerIncrease] = new UserCommandKeyInput(0x31);
            commands[UserCommand.ControlSteamHeatDecrease] = new UserCommandKeyInput(0x20, KeyModifiers.Alt);
            commands[UserCommand.ControlSteamHeatIncrease] = new UserCommandKeyInput(0x16, KeyModifiers.Alt);
            commands[UserCommand.ControlBrakeHoseConnect] = new UserCommandKeyInput(0x2B);
            commands[UserCommand.ControlBrakeHoseDisconnect] = new UserCommandKeyInput(0x2B, KeyModifiers.Shift);
            commands[UserCommand.ControlCabRadio] = new UserCommandKeyInput(0x13, KeyModifiers.Alt);
            commands[UserCommand.ControlCircuitBreakerClosingOrder] = new UserCommandKeyInput(0x18);
            commands[UserCommand.ControlCircuitBreakerOpeningOrder] = new UserCommandKeyInput(0x17);
            commands[UserCommand.ControlCircuitBreakerClosingAuthorization] = new UserCommandKeyInput(0x18, KeyModifiers.Shift);
            commands[UserCommand.ControlCylinderCocks] = new UserCommandKeyInput(0x2E);
            commands[UserCommand.ControlLargeEjectorIncrease] = new UserCommandKeyInput(0x24, KeyModifiers.Control);
            commands[UserCommand.ControlLargeEjectorDecrease] = new UserCommandKeyInput(0x24, KeyModifiers.Alt);
            commands[UserCommand.ControlSmallEjectorIncrease] = new UserCommandKeyInput(0x24);
            commands[UserCommand.ControlSmallEjectorDecrease] = new UserCommandKeyInput(0x24, KeyModifiers.Shift);
            commands[UserCommand.ControlVacuumExhausterPressed] = new UserCommandKeyInput(0x24);
            commands[UserCommand.ControlCylinderCompound] = new UserCommandKeyInput(0x19);
            commands[UserCommand.ControlDamperDecrease] = new UserCommandKeyInput(0x32, KeyModifiers.Shift);
            commands[UserCommand.ControlDamperIncrease] = new UserCommandKeyInput(0x32);
            commands[UserCommand.ControlDieselHelper] = new UserCommandKeyInput(0x15, KeyModifiers.Control);
            commands[UserCommand.ControlDieselPlayer] = new UserCommandKeyInput(0x15, KeyModifiers.Shift);
            commands[UserCommand.ControlDoorLeft] = new UserCommandKeyInput(0x10);
            commands[UserCommand.ControlDoorRight] = new UserCommandKeyInput(0x10, KeyModifiers.Shift);
            commands[UserCommand.ControlDynamicBrakeDecrease] = new UserCommandKeyInput(0x33);
            commands[UserCommand.ControlDynamicBrakeIncrease] = new UserCommandKeyInput(0x34);
            commands[UserCommand.ControlElectricTrainSupply] = new UserCommandKeyInput(0x30, KeyModifiers.Alt);
            commands[UserCommand.ControlEmergencyPushButton] = new UserCommandKeyInput(0x0E);
            commands[UserCommand.ControlEOTEmergencyBrake] = new UserCommandKeyInput(0x0E, KeyModifiers.Control);
            commands[UserCommand.ControlEngineBrakeDecrease] = new UserCommandKeyInput(0x1A);
            commands[UserCommand.ControlEngineBrakeIncrease] = new UserCommandKeyInput(0x1B);
            commands[UserCommand.ControlBrakemanBrakeDecrease] = new UserCommandKeyInput(0x1A, KeyModifiers.Alt);
            commands[UserCommand.ControlBrakemanBrakeIncrease] = new UserCommandKeyInput(0x1B, KeyModifiers.Alt);
            commands[UserCommand.ControlFireboxClose] = new UserCommandKeyInput(0x21, KeyModifiers.Shift);
            commands[UserCommand.ControlFireboxOpen] = new UserCommandKeyInput(0x21);
            commands[UserCommand.ControlFireShovelFull] = new UserCommandKeyInput(0x13, KeyModifiers.Control);
            commands[UserCommand.ControlFiring] = new UserCommandKeyInput(0x21, KeyModifiers.Control);
            commands[UserCommand.ControlFiringRateDecrease] = new UserCommandKeyInput(0x13, KeyModifiers.Shift);
            commands[UserCommand.ControlFiringRateIncrease] = new UserCommandKeyInput(0x13);
            commands[UserCommand.ControlGearDown] = new UserCommandKeyInput(0x12, KeyModifiers.Shift);
            commands[UserCommand.ControlGearUp] = new UserCommandKeyInput(0x12);
            commands[UserCommand.ControlGenericItem1] = new UserCommandKeyInput(0x33, KeyModifiers.Shift);
            commands[UserCommand.ControlGenericItem2] = new UserCommandKeyInput(0x34, KeyModifiers.Shift);
            commands[UserCommand.ControlTCSGeneric1] = new UserCommandKeyInput(0x33, KeyModifiers.Control);
            commands[UserCommand.ControlTCSGeneric2] = new UserCommandKeyInput(0x34, KeyModifiers.Control);
            commands[UserCommand.ControlHandbrakeFull] = new UserCommandKeyInput(0x28, KeyModifiers.Shift);
            commands[UserCommand.ControlHandbrakeNone] = new UserCommandKeyInput(0x27, KeyModifiers.Shift);
            commands[UserCommand.ControlHeadlightDecrease] = new UserCommandKeyInput(0x23, KeyModifiers.Shift);
            commands[UserCommand.ControlHeadlightIncrease] = new UserCommandKeyInput(0x23);
            commands[UserCommand.ControlHorn] = new UserCommandKeyInput(0x39);
            commands[UserCommand.ControlImmediateRefill] = new UserCommandKeyInput(0x14, KeyModifiers.Control);
            commands[UserCommand.ControlInitializeBrakes] = new UserCommandKeyInput(0x35, KeyModifiers.Shift);
            commands[UserCommand.ControlInjector1] = new UserCommandKeyInput(0x17);
            commands[UserCommand.ControlInjector1Decrease] = new UserCommandKeyInput(0x25, KeyModifiers.Shift);
            commands[UserCommand.ControlInjector1Increase] = new UserCommandKeyInput(0x25);
            commands[UserCommand.ControlInjector2] = new UserCommandKeyInput(0x18);
            commands[UserCommand.ControlInjector2Decrease] = new UserCommandKeyInput(0x26, KeyModifiers.Shift);
            commands[UserCommand.ControlInjector2Increase] = new UserCommandKeyInput(0x26);
            commands[UserCommand.ControlBlowdownValve] = new UserCommandKeyInput(0x2E, KeyModifiers.Shift);
            commands[UserCommand.ControlLight] = new UserCommandKeyInput(0x26);
            commands[UserCommand.ControlMasterKey] = new UserCommandKeyInput(0x1C);
            commands[UserCommand.ControlMirror] = new UserCommandKeyInput(0x2F, KeyModifiers.Shift);
            commands[UserCommand.ControlPantograph1] = new UserCommandKeyInput(0x19);
            commands[UserCommand.ControlPantograph2] = new UserCommandKeyInput(0x19, KeyModifiers.Shift);
            commands[UserCommand.ControlPantograph3] = new UserCommandKeyInput(0x19, KeyModifiers.Control);
            commands[UserCommand.ControlPantograph4] = new UserCommandKeyInput(0x19, KeyModifiers.Shift | KeyModifiers.Control);
            commands[UserCommand.ControlOdoMeterDisplayMode] = new UserCommandKeyInput(0x2C, KeyModifiers.Shift);
            commands[UserCommand.ControlOdoMeterReset] = new UserCommandKeyInput(0x2C, KeyModifiers.Control);
            commands[UserCommand.ControlOdoMeterDirection] = new UserCommandKeyInput(0x2C, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.ControlRefill] = new UserCommandKeyInput(0x14);
            commands[UserCommand.ControlDiscreteUnload] = new UserCommandKeyInput(0x14, KeyModifiers.Shift);
            commands[UserCommand.ControlRetainersOff] = new UserCommandKeyInput(0x1A, KeyModifiers.Shift);
            commands[UserCommand.ControlRetainersOn] = new UserCommandKeyInput(0x1B, KeyModifiers.Shift);
            commands[UserCommand.ControlReverserBackward] = new UserCommandKeyInput(0x1F);
            commands[UserCommand.ControlReverserForward] = new UserCommandKeyInput(0x11);
            commands[UserCommand.ControlSander] = new UserCommandKeyInput(0x2D);
            commands[UserCommand.ControlSanderToggle] = new UserCommandKeyInput(0x2D, KeyModifiers.Shift);
            commands[UserCommand.ControlServiceRetention] = new UserCommandKeyInput(0x53);
            commands[UserCommand.ControlServiceRetentionCancellation] = new UserCommandKeyInput(0x53, KeyModifiers.Control);
            commands[UserCommand.ControlThrottleDecrease] = new UserCommandKeyInput(0x1E);
            commands[UserCommand.ControlThrottleIncrease] = new UserCommandKeyInput(0x20);
            commands[UserCommand.ControlThrottleZero] = new UserCommandKeyInput(0x1E, KeyModifiers.Control);
            commands[UserCommand.ControlTractionCutOffRelayClosingOrder] = new UserCommandKeyInput(0x18);
            commands[UserCommand.ControlTractionCutOffRelayOpeningOrder] = new UserCommandKeyInput(0x17);
            commands[UserCommand.ControlTractionCutOffRelayClosingAuthorization] = new UserCommandKeyInput(0x18, KeyModifiers.Shift);
            commands[UserCommand.ControlTrainBrakeDecrease] = new UserCommandKeyInput(0x27);
            commands[UserCommand.ControlTrainBrakeIncrease] = new UserCommandKeyInput(0x28);
            commands[UserCommand.ControlTrainBrakeZero] = new UserCommandKeyInput(0x27, KeyModifiers.Control);
            commands[UserCommand.ControlTurntableClockwise] = new UserCommandKeyInput(0x2E, KeyModifiers.Alt);
            commands[UserCommand.ControlTurntableCounterclockwise] = new UserCommandKeyInput(0x2E, KeyModifiers.Control);
            commands[UserCommand.ControlWaterScoop] = new UserCommandKeyInput(0x15);
            commands[UserCommand.ControlWiper] = new UserCommandKeyInput(0x2F);

            // Cruise Control
            commands[UserCommand.ControlSpeedRegulatorModeIncrease] = new UserCommandKeyInput(0x11, KeyModifiers.Shift);
            commands[UserCommand.ControlSpeedRegulatorModeDecrease] = new UserCommandKeyInput(0x1F, KeyModifiers.Shift);
            commands[UserCommand.ControlSpeedRegulatorMaxAccelerationIncrease] = new UserCommandKeyInput(0x20, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.ControlSpeedRegulatorMaxAccelerationDecrease] = new UserCommandKeyInput(0x1E, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.ControlSpeedRegulatorSelectedSpeedIncrease] = new UserCommandKeyInput(0x20, KeyModifiers.Shift);
            commands[UserCommand.ControlSpeedRegulatorSelectedSpeedDecrease] = new UserCommandKeyInput(0x1E, KeyModifiers.Shift);
            commands[UserCommand.ControlNumberOfAxlesIncrease] = new UserCommandKeyInput(0x47, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.ControlNumberOfAxlesDecrease] = new UserCommandKeyInput(0x4F, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.ControlRestrictedSpeedZoneActive] = new UserCommandKeyInput(0x13, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.ControlCruiseControlModeDecrease] = new UserCommandKeyInput(0x1F, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.ControlCruiseControlModeIncrease] = new UserCommandKeyInput(0x11, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.ControlTrainTypePaxCargo] = new UserCommandKeyInput(0x31, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.ControlSpeedRegulatorSelectedSpeedToZero] = new UserCommandKeyInput(0x1E, KeyModifiers.Shift | KeyModifiers.Alt);

            // Distributed power
            commands[UserCommand.ControlDistributedPowerMoveToFront] = new UserCommandKeyInput(0x18, KeyModifiers.Control); //O
            commands[UserCommand.ControlDistributedPowerMoveToBack] = new UserCommandKeyInput(0x18, KeyModifiers.Control | KeyModifiers.Shift); //O
            commands[UserCommand.ControlDistributedPOwerTraction] = new UserCommandKeyInput(0x26, KeyModifiers.Control); //L
            commands[UserCommand.ControlDistributedPowerIdle] = new UserCommandKeyInput(0x26, KeyModifiers.Control | KeyModifiers.Shift); //L
            commands[UserCommand.ControlDistributedPowerBrake] = new UserCommandKeyInput(0x28, KeyModifiers.Control); //
            commands[UserCommand.ControlDistributedIncrease] = new UserCommandKeyInput(0x16, KeyModifiers.Control); //U
            commands[UserCommand.ControlDistributedPowerDecrease] = new UserCommandKeyInput(0x16, KeyModifiers.Control | KeyModifiers.Shift); //U

            commands[UserCommand.DebugClockBackwards] = new UserCommandKeyInput(0x0C);
            commands[UserCommand.DebugClockForwards] = new UserCommandKeyInput(0x0D);
            commands[UserCommand.DebugDumpKeymap] = new UserCommandKeyInput(0x3B, KeyModifiers.Alt);
            commands[UserCommand.DebugFogDecrease] = new UserCommandKeyInput(0x0C, KeyModifiers.Shift);
            commands[UserCommand.DebugFogIncrease] = new UserCommandKeyInput(0x0D, KeyModifiers.Shift);
            commands[UserCommand.DebugLockShadows] = new UserCommandKeyInput(0x1F, KeyModifiers.Control | KeyModifiers.Alt);
            commands[UserCommand.DebugLogger] = new UserCommandKeyInput(0x58);
            commands[UserCommand.DebugLogRenderFrame] = new UserCommandKeyInput(0x58, KeyModifiers.Alt);
            commands[UserCommand.DebugOvercastDecrease] = new UserCommandKeyInput(0x0C, KeyModifiers.Control);
            commands[UserCommand.DebugOvercastIncrease] = new UserCommandKeyInput(0x0D, KeyModifiers.Control);
            commands[UserCommand.DebugPhysicsForm] = new UserCommandKeyInput(0x3D, KeyModifiers.Alt);
            commands[UserCommand.DebugPrecipitationDecrease] = new UserCommandKeyInput(0x0C, KeyModifiers.Alt);
            commands[UserCommand.DebugPrecipitationIncrease] = new UserCommandKeyInput(0x0D, KeyModifiers.Alt);
            commands[UserCommand.DebugPrecipitationLiquidityDecrease] = new UserCommandKeyInput(0x0C, KeyModifiers.Control);
            commands[UserCommand.DebugPrecipitationLiquidityIncrease] = new UserCommandKeyInput(0x0D, KeyModifiers.Control);
            commands[UserCommand.DebugResetWheelSlip] = new UserCommandKeyInput(0x2D, KeyModifiers.Alt);
            commands[UserCommand.DebugSignalling] = new UserCommandKeyInput(0x57, KeyModifiers.Control | KeyModifiers.Alt);
            commands[UserCommand.DebugSoundForm] = new UserCommandKeyInput(0x1F, KeyModifiers.Alt);
            commands[UserCommand.DebugSpeedDown] = new UserCommandKeyInput(0x51, KeyModifiers.Control | KeyModifiers.Alt);
            commands[UserCommand.DebugSpeedReset] = new UserCommandKeyInput(0x47, KeyModifiers.Control | KeyModifiers.Alt);
            commands[UserCommand.DebugSpeedUp] = new UserCommandKeyInput(0x49, KeyModifiers.Control | KeyModifiers.Alt);
            commands[UserCommand.DebugToggleAdvancedAdhesion] = new UserCommandKeyInput(0x2D, KeyModifiers.Control | KeyModifiers.Alt);
            commands[UserCommand.DebugTracks] = new UserCommandKeyInput(0x40, KeyModifiers.Control | KeyModifiers.Alt);
            commands[UserCommand.DebugWeatherChange] = new UserCommandKeyInput(0x19, KeyModifiers.Alt);
            commands[UserCommand.DebugToggleConfirmations] = new UserCommandKeyInput(0x44, KeyModifiers.Control | KeyModifiers.Alt);

            commands[UserCommand.DisplayTrainListWindow] = new UserCommandKeyInput(0x43, KeyModifiers.Alt);
            commands[UserCommand.DisplayCarLabels] = new UserCommandModifiableKeyInput(0x41, commands[UserCommand.DisplayNextWindowTab]);
            commands[UserCommand.DisplayCompassWindow] = new UserCommandKeyInput(0x0B);
            commands[UserCommand.DisplayHelpWindow] = new UserCommandModifiableKeyInput(0x3B, commands[UserCommand.DisplayNextWindowTab]);
            commands[UserCommand.DisplayHUD] = new UserCommandModifiableKeyInput(0x3F, commands[UserCommand.DisplayNextWindowTab]);
            commands[UserCommand.DisplayHUDScrollLeft] = new UserCommandKeyInput(0x4B, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.DisplayHUDScrollRight] = new UserCommandKeyInput(0x4D, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.DisplayHUDScrollUp] = new UserCommandKeyInput(0x48, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.DisplayHUDScrollDown] = new UserCommandKeyInput(0x50, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.DisplayHUDPageUp] = new UserCommandKeyInput(0x49, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.DisplayHUDPageDown] = new UserCommandKeyInput(0x51, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.DisplayTrainDrivingWindow] = new UserCommandModifiableKeyInput(0x3F, KeyModifiers.Control, commands[UserCommand.DisplayNextWindowTab]);
            commands[UserCommand.DisplayMultiPlayerWindow] = new UserCommandKeyInput(0x0A, KeyModifiers.Shift);
            commands[UserCommand.DisplayNextStationWindow] = new UserCommandKeyInput(0x44);
            commands[UserCommand.DisplayStationLabels] = new UserCommandModifiableKeyInput(0x40, commands[UserCommand.DisplayNextWindowTab]);
            commands[UserCommand.DisplaySwitchWindow] = new UserCommandKeyInput(0x42);
            commands[UserCommand.DisplayTrackMonitorWindow] = new UserCommandModifiableKeyInput(0x3E, commands[UserCommand.DisplayNextWindowTab]);
            commands[UserCommand.DisplayTrainOperationsWindow] = new UserCommandKeyInput(0x43);
            commands[UserCommand.DisplayDistributedPowerWindow] = new UserCommandModifiableKeyInput(0x3F, KeyModifiers.Alt, commands[UserCommand.DisplayNextWindowTab]);
            commands[UserCommand.DisplayEOTListWindow] = new UserCommandKeyInput(0x43, KeyModifiers.Control);

            commands[UserCommand.GameAutopilotMode] = new UserCommandKeyInput(0x1E, KeyModifiers.Alt);
            commands[UserCommand.GameChangeCab] = new UserCommandKeyInput(0x12, KeyModifiers.Control);
            commands[UserCommand.GameClearSignalBackward] = new UserCommandKeyInput(0x0F, KeyModifiers.Shift);
            commands[UserCommand.GameClearSignalForward] = new UserCommandKeyInput(0x0F);
            commands[UserCommand.GameExternalCabController] = new UserCommandKeyInput(0x29);
            commands[UserCommand.GameFullscreen] = new UserCommandKeyInput(0x1C, KeyModifiers.Alt);
            commands[UserCommand.GameMultiPlayerDispatcher] = new UserCommandKeyInput(0x0A, KeyModifiers.Control);
            commands[UserCommand.GameMultiPlayerTexting] = new UserCommandKeyInput(0x14, KeyModifiers.Alt);
            commands[UserCommand.GamePause] = new UserCommandKeyInput(Keys.Pause);
            commands[UserCommand.GamePauseMenu] = new UserCommandKeyInput(0x01);
            commands[UserCommand.GameQuit] = new UserCommandKeyInput(0x3E, KeyModifiers.Alt);
            commands[UserCommand.GameResetOutOfControlMode] = new UserCommandKeyInput(0x0E, KeyModifiers.Shift);
            commands[UserCommand.GameRequestControl] = new UserCommandKeyInput(0x12, KeyModifiers.Alt);
            commands[UserCommand.GameResetSignalBackward] = new UserCommandKeyInput(0x0F, KeyModifiers.Control | KeyModifiers.Shift);
            commands[UserCommand.GameResetSignalForward] = new UserCommandKeyInput(0x0F, KeyModifiers.Control);
            commands[UserCommand.GameSave] = new UserCommandKeyInput(0x3C);
            commands[UserCommand.GameScreenshot] = new UserCommandKeyInput(Keys.PrintScreen);
            commands[UserCommand.GameSignalPicked] = new UserCommandKeyInput(0x22, KeyModifiers.Control);
            commands[UserCommand.GameSwitchAhead] = new UserCommandKeyInput(0x22);
            commands[UserCommand.GameSwitchBehind] = new UserCommandKeyInput(0x22, KeyModifiers.Shift);
            commands[UserCommand.GameSwitchManualMode] = new UserCommandKeyInput(0x32, KeyModifiers.Control);
            commands[UserCommand.GameSwitchPicked] = new UserCommandKeyInput(0x22, KeyModifiers.Alt);
            commands[UserCommand.GameUncoupleWithMouse] = new UserCommandKeyInput(0x16);

            return commands;
        }
        #endregion

        [MemoryPackOnSerializing]
        private void OnSerializing()
        {
            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
            {
                userCommands[command] = UserCommands[command].UniqueDescriptor;
            }
        }

        [MemoryPackOnDeserialized]
        private void OnDeserializing()
        {
            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
            {
                UserCommands[command].UniqueDescriptor = userCommands[command];
            }
        }
    }
}
