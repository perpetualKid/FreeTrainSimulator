// COPYRIGHT 2012, 2013, 2014 by the Open Rails project.
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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework.Input;

using Orts.Settings.Store;

namespace Orts.Settings
{
    /// <summary>
    /// Loads, stores and manages keyboard input settings for all available <see cref="UserCommands"/>.
    /// </summary>
    /// <remarks>
    /// <para>Keyboard input is processed by associating specific combinations of keys (either scan codes or virtual keys) and modifiers with each <see cref="UserCommands"/>.</para>
    /// <para>There are three kinds of <see cref="UserCommand"/>, each using a different <see cref="UserCommandInput"/>:</para>
    /// <list type="bullet">
    /// <item><description><see cref="UserCommandModifierInput"/> represents a specific combination of keyboard modifiers (Shift, Control and Alt). E.g. Shift.</description></item>
    /// <item><description><see cref="UserCommandKeyInput"/> represents a key (scan code or virtual key) and a specific combination of keyboard modifiers. E.g. Alt-F4.</description></item>
    /// <item><description><see cref="UserCommandModifiableKeyInput"/> represents a key (scan code or virtual key), a specific combination of keyboard modifiers and a set of keyboard modifiers to ignore. E.g. Up Arrow (+ Shift) (+ Control).</description></item>
    /// </list>
    /// <para>Keyboard input is identified in two distinct ways:</para>
    /// <list>
    /// <item><term>Scan code</term><description>A scan code represents a specific location on the physical keyboard, irrespective of the user's locale, keyboard layout and other enviromental settings. For this reason, this is the preferred way to refer to the "main" area of the keyboard - this area varies significantly by locale and usually it is the physical location that matters.</description></item>
    /// <item><term>Virtual key</term><description>A virtual key represents a logical key on the keyboard, irrespective of where it might be located. For keys outside the "main" area, this is much the same as scan codes and is preferred when refering to logical keys like "Up Arrow".</description></item>
    /// </list>
    /// </remarks>
    public class InputSettings : SettingsBase
    {

        public static EnumArray<UserCommandInput, UserCommand> DefaultCommands { get; } = new EnumArray<UserCommandInput, UserCommand>();
        public EnumArray<UserCommandInput, UserCommand> UserCommands { get; } = new EnumArray<UserCommandInput, UserCommand>();

        public KeyModifiers WindowTabCommandModifier { get; }
        public KeyModifiers CameraMoveFastModifier { get; }
        public KeyModifiers CameraMoveSlowModifier { get; }
        public KeyModifiers GameSuspendOldPlayerModifier { get; }
        public KeyModifiers GameSwitchWithMouseModifier { get; }
        static InputSettings()
        {
            InitializeCommands(DefaultCommands);
        }

        public InputSettings(in ImmutableArray<string> options, SettingsStore store) :
            base(SettingsStore.GetSettingsStore(store?.StoreType ?? StoreType.Registry, store.Location, "Keyboard"))
        {
            InitializeCommands(UserCommands);
            LoadSettings(options);

            UserCommandModifierInput userCommandModifier = UserCommands[UserCommand.DisplayNextWindowTab] as UserCommandModifierInput;
            WindowTabCommandModifier = (userCommandModifier.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandModifier.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandModifier.Shift ? KeyModifiers.Shift : KeyModifiers.None);

            userCommandModifier = UserCommands[UserCommand.CameraMoveFast] as UserCommandModifierInput;
            CameraMoveFastModifier = (userCommandModifier.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandModifier.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandModifier.Shift ? KeyModifiers.Shift : KeyModifiers.None);
            userCommandModifier = UserCommands[UserCommand.CameraMoveSlow] as UserCommandModifierInput;
            CameraMoveSlowModifier = (userCommandModifier.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandModifier.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandModifier.Shift ? KeyModifiers.Shift : KeyModifiers.None);
            userCommandModifier = UserCommands[UserCommand.GameSuspendOldPlayer] as UserCommandModifierInput;
            GameSuspendOldPlayerModifier = (userCommandModifier.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandModifier.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandModifier.Shift ? KeyModifiers.Shift : KeyModifiers.None);
            userCommandModifier = UserCommands[UserCommand.GameSwitchWithMouse] as UserCommandModifierInput;
            GameSwitchWithMouseModifier = (userCommandModifier.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (userCommandModifier.Control ? KeyModifiers.Control : KeyModifiers.None) | (userCommandModifier.Shift ? KeyModifiers.Shift : KeyModifiers.None);
        }

        private static UserCommand GetCommand(string name)
        {
            if (!EnumExtension.GetValue(name, out UserCommand result))
                throw new ArgumentOutOfRangeException(nameof(name));
            return result;
        }

        public override object GetDefaultValue(string name)
        {
            return DefaultCommands[GetCommand(name)].UniqueDescriptor;
        }

        protected override object GetValue(string name)
        {
            return UserCommands[GetCommand(name)].UniqueDescriptor;
        }

        protected override void SetValue(string name, object value)
        {
            UserCommands[GetCommand(name)].UniqueDescriptor = (int)value;
        }

        protected override void Load(bool allowUserSettings, NameValueCollection optionalValues)
        {
            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
                LoadSetting(allowUserSettings, optionalValues, command.ToString());
            properties = null;
        }

        public override void Save()
        {
            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
                SaveSetting(command.ToString());
            properties = null;
        }

        public override void Reset()
        {
            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
                Reset(command.ToString());
        }

        #region Default Input Settings
        private static void InitializeCommands(EnumArray<UserCommandInput, UserCommand> commands)
        {
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
            commands[UserCommand.CameraTrackside] = new UserCommandKeyInput(0x05); ;
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
        }
        #endregion

        public string CheckForErrors()
        {
            // Make sure all modifiable input commands are synchronized first.
            foreach (UserCommandInput command in UserCommands)
                (command as UserCommandModifiableKeyInput)?.SynchronizeCombine();

            StringBuilder errors = new StringBuilder();

            // Check for commands which both require a particular modifier, and ignore it.
            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
            {
                if (UserCommands[command] is UserCommandModifiableKeyInput modInput)
                {
                    if (modInput.Shift && modInput.IgnoreShift)
                        errors.AppendLine(catalog.GetString("{0} requires and is modified by Shift", command.GetLocalizedDescription()));
                    if (modInput.Control && modInput.IgnoreControl)
                        errors.AppendLine(catalog.GetString("{0} requires and is modified by Control", command.GetLocalizedDescription()));
                    if (modInput.Alt && modInput.IgnoreAlt)
                        errors.AppendLine(catalog.GetString("{0} requires and is modified by Alt", command.GetLocalizedDescription()));
                }
            }

            // Check for two commands assigned to the same key
            UserCommand firstCommand = EnumExtension.GetValues<UserCommand>().Min();
            UserCommand lastCommand = EnumExtension.GetValues<UserCommand>().Max();
            for (UserCommand command1 = firstCommand; command1 <= lastCommand; command1++)
            {
                UserCommandInput input1 = UserCommands[command1];

                // Modifier inputs don't matter as they don't represent any key.
                if (input1 is UserCommandModifierInput)
                    continue;

                for (UserCommand command2 = command1 + 1; command2 <= lastCommand; command2++)
                {
                    UserCommandInput input2 = UserCommands[command2];

                    // Modifier inputs don't matter as they don't represent any key.
                    if (input2 is UserCommandModifierInput)
                        continue;

                    // Ignore problems when both inputs are on defaults. (This protects the user somewhat but leaves developers in the dark.)
                    if (input1.UniqueDescriptor == DefaultCommands[command1].UniqueDescriptor &&
                    input2.UniqueDescriptor == DefaultCommands[command2].UniqueDescriptor)
                        continue;

                    IEnumerable<string> unique1 = input1.GetUniqueInputs();
                    IEnumerable<string> unique2 = input2.GetUniqueInputs();
                    IEnumerable<string> sharedUnique = unique1.Where(id => unique2.Contains(id));
                    foreach (string uniqueInput in sharedUnique)
                        errors.AppendLine(catalog.GetString("{0} and {1} both match {2}", command1.GetLocalizedDescription(), command2.GetLocalizedDescription(), KeyboardMap.GetPrettyUniqueInput(uniqueInput)));
                }
            }

            return errors.ToString();
        }
    }

}
