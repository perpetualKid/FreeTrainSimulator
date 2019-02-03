using System;
using System.Collections.Generic;
using System.ComponentModel;
using GNU.Gettext;
using ORTS.Common;

namespace ORTS.Settings
{
    public enum RailDriverCalibrationSetting
    {
        [Description("Reverser Neutral")] ReverserNeutral,
        [Description("Reverser Full Reversed")] ReverserFullReversed,
        [Description("Reverser Full Forward")] ReverserFullForward,
        [Description("Throttle Idle")] ThrottleIdle,
        [Description("Full Throttle")] ThrottleFull,
        [Description("Dynamic Brake")] DynamicBrake,
        [Description("Dynamic Brake Setup")] DynamicBrakeSetup,
        [Description("Auto Brake Released")] AutoBrakeRelease,
        [Description("Full Auto Brake ")] AutoBrakeFull,
        [Description("Emergency Brake")] EmergencyBrake,
        [Description("Independent Brake Released")] IndependentBrakeRelease,
        [Description("Independent Brake Full")] IndependentBrakeFull,
        [Description("Bail Off Disengaged (in Released position)")] BailOffDisengagedRelease,
        [Description("Bail Off Engaged (in Released position)")] BailOffEngagedRelease,
        [Description("Bail Off Disengaged (in Full position)")] BailOffDisengagedFull,
        [Description("Bail Off Engaged (in Full position)")] BailOffEngagedFull,
        [Description("Rotary Switch 1-Position 1(OFF)")] Rotary1Position1,
        [Description("Rotary Switch 1-Position 2(SLOW)")] Rotary1Position2,
        [Description("Rotary Switch 1-Position 3(FULL)")] Rotary1Position3,
        [Description("Rotary Switch 2-Position 1(OFF)")] Rotary2Position1,
        [Description("Rotary Switch 2-Position 2(DIM)")] Rotary2Position2,
        [Description("Rotary Switch 2-Position 3(FULL)")] Rotary2Position3,
        [Description("Reverse Reverser Direction")] ReverseReverser,
        [Description("Reverse Throttle Direction")] ReverseThrottle,
        [Description("Reverse Auto Brake Direction")] ReverseAutoBrake,
        [Description("Reverse Independent Brake Direction")] ReverseIndependentBrake,
        [Description("Full Range Throttle")] FullRangeThrottle,
        [Description("Cut Off Delta (Percent)")] PercentageCutOffDelta,
    }

    public class RailDriverSettings : SettingsBase
    {
        private static readonly GettextResourceManager catalog = new GettextResourceManager("ORTS.Settings");
        private static readonly byte[] DefaultCommands = new byte[EnumExtension.GetLength<UserCommand>()];
        private static readonly byte[] DefaultCalibrationSettings;
        private static readonly Dictionary<UserCommand, byte> DefaultUserCommands;

        private bool default0WhileSaving;

        public readonly byte[] UserCommands = new byte[EnumExtension.GetLength<UserCommand>()];

        public readonly byte[] CalibrationSettings;

        static RailDriverSettings()
        {
            //default calibration settings from another developer's PC, they are as good as random numbers...
            DefaultCalibrationSettings = new byte[] { 225, 116, 60, 229, 176, 42, 119, 216, 79, 58, 213, 179, 30, 209, 109, 121, 73, 135, 180, 86, 145, 189, 0, 0, 0, 0, 0, 5 };
            DefaultUserCommands = new Dictionary<UserCommand, byte>();

            // top row of blue buttons left to right
            DefaultUserCommands.Add(UserCommand.GamePauseMenu, 0);                  // Btn 00 Default Legend Game Pause
            DefaultUserCommands.Add(UserCommand.GameSave, 1);                       // Btn 01 Default Legend Game Save
                                                                                    // Btn 02 Default Legend Control Gauges
            DefaultUserCommands.Add(UserCommand.DisplayTrackMonitorWindow, 3);      // Btn 03 Default Legend Track Monitor
                                                                                    // Btn 04 Default Legend Station/Siding Names
                                                                                    // Btn 05 Default Legend Car #
            DefaultUserCommands.Add(UserCommand.DisplaySwitchWindow, 6);            // Btn 06 Default Legend Switching Drive Aids
            DefaultUserCommands.Add(UserCommand.DisplayTrainOperationsWindow, 7);   // Btn 07 Default Legend Train Operations
            DefaultUserCommands.Add(UserCommand.DisplayNextStationWindow, 8);       // Btn 08 Default Legend Next Station Window
                                                                                    // Btn 09 Default Legend Ops Notebook
                                                                                    // Btn 10 Default Legend Hide Drive Aids
            DefaultUserCommands.Add(UserCommand.DisplayCompassWindow, 11);          // Btn 11 Default Legend Compass Window
            DefaultUserCommands.Add(UserCommand.GameSwitchAhead, 12);               // Btn 12 Default Legend Switch Ahead
            DefaultUserCommands.Add(UserCommand.GameSwitchBehind, 13);              // Btn 13 Default Legend Switch Behind

            // bottom row of blue buttons left to right
            DefaultUserCommands.Add(UserCommand.GameExternalCabController, 14);     // Btn 14 Default Legend RailDriver Run/Stop
            DefaultUserCommands.Add(UserCommand.CameraToggleShowCab, 15);           // Btn 15 Default Legend Hide Cab Panel
            DefaultUserCommands.Add(UserCommand.CameraCab, 16);                     // Btn 16 Default Legend Frnt Cab View
            DefaultUserCommands.Add(UserCommand.CameraOutsideFront, 17);            // Btn 17 Default Legend Ext View 1
            DefaultUserCommands.Add(UserCommand.CameraOutsideRear, 18);             // Btn 18 Default Legend Ext.View 2
            DefaultUserCommands.Add(UserCommand.CameraCarPrevious, 19);             // Btn 19 Default Legend FrontCoupler
            DefaultUserCommands.Add(UserCommand.CameraCarNext, 20);                 // Btn 20 Default Legend Rear Coupler
            DefaultUserCommands.Add(UserCommand.CameraTrackside, 21);               // Btn 21 Default Legend Track View      
            DefaultUserCommands.Add(UserCommand.CameraPassenger, 22);               // Btn 22 Default Legend Passgr View      
            DefaultUserCommands.Add(UserCommand.CameraBrakeman, 23);                // Btn 23 Default Legend Coupler View
            DefaultUserCommands.Add(UserCommand.CameraFree, 24);                    // Btn 24 Default Legend Yard View
            DefaultUserCommands.Add(UserCommand.GameClearSignalForward, 25);        // Btn 25 Default Legend Request Pass
                                                                                    // Btn 26 Default Legend Load/Unload
                                                                                    // Btn 27 Default Legend OK

            // controls to right of blue buttons
            DefaultUserCommands.Add(UserCommand.CameraZoomIn, 28);
            DefaultUserCommands.Add(UserCommand.CameraZoomOut, 29);
            DefaultUserCommands.Add(UserCommand.CameraPanUp, 30);
            DefaultUserCommands.Add(UserCommand.CameraPanRight, 31);
            DefaultUserCommands.Add(UserCommand.CameraPanDown, 32);
            DefaultUserCommands.Add(UserCommand.CameraPanLeft, 33);

            // buttons on top left
            DefaultUserCommands.Add(UserCommand.ControlGearUp, 34);
            DefaultUserCommands.Add(UserCommand.ControlGearDown, 35);
            DefaultUserCommands.Add(UserCommand.ControlEmergencyPushButton, 36);
            //DefaultUserCommands.Add(UserCommand.ControlEmergencyPushButton, 37);
            DefaultUserCommands.Add(UserCommand.ControlAlerter, 38);
            DefaultUserCommands.Add(UserCommand.ControlSander, 39);
            DefaultUserCommands.Add(UserCommand.ControlPantograph1, 40);
            DefaultUserCommands.Add(UserCommand.ControlBellToggle, 41);
            DefaultUserCommands.Add(UserCommand.ControlHorn, 42);
            //DefaultUserCommands.Add(UserCommand.ControlHorn, 43);

        }

        /// <summary>
        /// Initializes a new instances of the <see cref="InputSettings"/> class with the specified options.
        /// </summary>
        /// <param name="options">The list of one-time options to override persisted settings, if any.</param>
        public RailDriverSettings(IEnumerable<string> options)
        : base(SettingsStore.GetSettingStore(UserSettings.SettingsFilePath, UserSettings.RegistryKey, "RailDriver"))
        {
            CalibrationSettings = new byte[DefaultCalibrationSettings.Length];

            Load(options);
        }

        public override object GetDefaultValue(string name)
        {
            if (Enum.TryParse(name, true, out RailDriverCalibrationSetting calibrationSetting))
            {
                return default0WhileSaving ? 0 : DefaultCalibrationSettings[(int)calibrationSetting];
            }
            else if (Enum.TryParse(name, true, out UserCommand userCommand))
            {
                return GetDefaultValue(userCommand);
            }
            else
                throw new ArgumentOutOfRangeException($"Enum parameter {nameof(name)} not within expected range of either {nameof(RailDriverCalibrationSetting)} or {nameof(UserCommands)}");
        }

        public static byte GetDefaultValue(UserCommand command)
        {
            return DefaultUserCommands.TryGetValue(command, out byte value) ? value : byte.MaxValue;
        }

        public override void Reset()
        {
            foreach (RailDriverCalibrationSetting setting in EnumExtension.GetValues<RailDriverCalibrationSetting>())
                Reset(setting.ToString());

            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
                Reset(command.ToString());
        }

        public override void Save()
        {
            default0WhileSaving = true; //temporarily "disable" default calibration settings, so Calibration Settings are always getting written to SettingsStore
            foreach (RailDriverCalibrationSetting setting in EnumExtension.GetValues<RailDriverCalibrationSetting>())
                Save(setting.ToString());

            foreach(UserCommand command in EnumExtension.GetValues<UserCommand>())
                Save(command.ToString());

            default0WhileSaving = false;
        }

        public override void Save(string name)
        {
            Save(name, typeof(byte));
        }

        protected override object GetValue(string name)
        {
            if (Enum.TryParse(name, true, out RailDriverCalibrationSetting calibrationSetting))
            {
                return CalibrationSettings[(int)calibrationSetting];
            }
            else if (Enum.TryParse(name, true, out UserCommand userCommand))
            {
                return UserCommands[(int)userCommand];
            }
            else
                throw new ArgumentOutOfRangeException($"Enum parameter {nameof(name)} not within expected range of either {nameof(RailDriverCalibrationSetting)} or {nameof(UserCommands)}");
        }

        protected override void Load(bool allowUserSettings, Dictionary<string, string> optionsDictionary)
        {
            foreach (RailDriverCalibrationSetting setting in EnumExtension.GetValues<RailDriverCalibrationSetting>())
                Load(allowUserSettings, optionsDictionary, setting.ToString(), typeof(byte));
            foreach (var command in EnumExtension.GetValues<UserCommand>())
                Load(allowUserSettings, optionsDictionary, command.ToString(), typeof(byte));
        }

        protected override void SetValue(string name, object value)
        {
            if (Enum.TryParse(name, true, out RailDriverCalibrationSetting calibrationSetting))
            {
                if (!byte.TryParse(value?.ToString(), out byte result))
                    result = 0;
                CalibrationSettings[(int)calibrationSetting] = result;
            }
            else if (Enum.TryParse(name, true, out UserCommand userCommand))
            {
                UserCommands[(int)userCommand] = (byte)value;
            }
            else
                throw new ArgumentOutOfRangeException($"Enum parameter {nameof(name)} not within expected range of either {nameof(RailDriverCalibrationSetting)} or {nameof(UserCommands)}");
        }

        public string CheckForErrors()
        {
            return string.Empty;
        }
    }
}
