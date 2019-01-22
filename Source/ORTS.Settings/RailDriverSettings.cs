using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GNU.Gettext;
using ORTS.Common;

namespace ORTS.Settings
{

    public enum RailDriverCalibrationSetting
    {
        FullReversed,
        Neutral,
        FullForward,
        FullThrottle,
        ThrottleIdle,
        DynamicBrake,
        DynamicBrakeSetup,
        AutoBrakeRelease,
        FullAutoBrake,
        EmergencyBrake,
        IndependentBrakeRelease,
        BailOffEngagedRelease,
        IndependentBrakeFull,
        BailOffEngagedFull,
        BailOffDisengagedRelease,
        BailOffDisengagedFull,
        Rotary1Position1,
        Rotary1Position2,
        Rotary1Position3,
        Rotary2Position1,
        Rotary2Position2,
        Rotary2Position3,
        PercentageCutOffDelta,
    }

    public class RailDriverSettings : SettingsBase
    {
        private static readonly GettextResourceManager catalog = new GettextResourceManager("ORTS.Settings");
        private static readonly byte[] DefaultCommands = new byte[Enum.GetNames(typeof(UserCommand)).Length];
        private static readonly byte[] DefaultCalibrationSettings;
        private static readonly UserCommand[] DefaultUserCommands;

        private bool default0WhileSaving;

        public readonly byte[] UserCommands = new byte[Enum.GetNames(typeof(UserCommand)).Length];

        public readonly byte[] CalibrationSettings;

        static RailDriverSettings()
        {
            //default calibration settings from another developer's PC, they are as good as random numbers...
            DefaultCalibrationSettings = new byte[] { 225, 116, 60, 229, 176, 42, 119, 216, 79, 58, 213, 179, 30, 209, 109, 121, 73, 135, 180, 86, 145, 189, 5 };
            DefaultUserCommands = new UserCommand[48]; //there are 6 bytes for User-Buttons on RailDriver board

            // top row of blue buttons left to right
            DefaultUserCommands[0] = UserCommand.GamePauseMenu;                 // Btn 00 Default Legend Game Pause
            DefaultUserCommands[1] = UserCommand.GameSave;                      // Btn 01 Default Legend Game Save
                                                                                        // Btn 02 Default Legend Control Gauges
            DefaultUserCommands[3] = UserCommand.DisplayTrackMonitorWindow;     // Btn 03 Default Legend Track Monitor
                                                                                        // Btn 04 Default Legend Station/Siding Names
                                                                                        // Btn 05 Default Legend Car #
            DefaultUserCommands[6] = UserCommand.DisplaySwitchWindow;           // Btn 06 Default Legend Switching Drive Aids
            DefaultUserCommands[7] = UserCommand.DisplayTrainOperationsWindow;  // Btn 07 Default Legend Train Operations
            DefaultUserCommands[8] = UserCommand.DisplayNextStationWindow;      // Btn 08 Default Legend Next Station Window
                                                                                        // Btn 09 Default Legend Ops Notebook
                                                                                        // Btn 10 Default Legend Hide Drive Aids
            DefaultUserCommands[11] = UserCommand.DisplayCompassWindow;         // Btn 11 Default Legend Compass Window
            DefaultUserCommands[12] = UserCommand.GameSwitchAhead;              // Btn 12 Default Legend Switch Ahead
            DefaultUserCommands[13] = UserCommand.GameSwitchBehind;             // Btn 13 Default Legend Switch Behind

            // bottom row of blue buttons left to right
            DefaultUserCommands[14] = UserCommand.GameExternalCabController;    // Btn 14 Default Legend RailDriver Run/Stop
            DefaultUserCommands[15] = UserCommand.CameraToggleShowCab;          // Btn 15 Default Legend Hide Cab Panel
            DefaultUserCommands[16] = UserCommand.CameraCab;                    // Btn 16 Default Legend Frnt Cab View
            DefaultUserCommands[17] = UserCommand.CameraOutsideFront;           // Btn 17 Default Legend Ext View 1
            DefaultUserCommands[18] = UserCommand.CameraOutsideRear;            // Btn 18 Default Legend Ext.View 2
            DefaultUserCommands[19] = UserCommand.CameraCarPrevious;            // Btn 19 Default Legend FrontCoupler
            DefaultUserCommands[20] = UserCommand.CameraCarNext;                // Btn 20 Default Legend Rear Coupler
            DefaultUserCommands[21] = UserCommand.CameraTrackside;              // Btn 21 Default Legend Track View      
            DefaultUserCommands[22] = UserCommand.CameraPassenger;              // Btn 22 Default Legend Passgr View      
            DefaultUserCommands[23] = UserCommand.CameraBrakeman;               // Btn 23 Default Legend Coupler View
            DefaultUserCommands[24] = UserCommand.CameraFree;                   // Btn 24 Default Legend Yard View
            DefaultUserCommands[25] = UserCommand.GameClearSignalForward;       // Btn 25 Default Legend Request Pass
                                                                                        // Btn 26 Default Legend Load/Unload
                                                                                        // Btn 27 Default Legend OK

            // controls to right of blue buttons
            DefaultUserCommands[28] = UserCommand.CameraZoomIn;
            DefaultUserCommands[29] = UserCommand.CameraZoomOut;
            DefaultUserCommands[30] = UserCommand.CameraPanUp;
            DefaultUserCommands[31] = UserCommand.CameraPanRight;
            DefaultUserCommands[32] = UserCommand.CameraPanDown;
            DefaultUserCommands[33] = UserCommand.CameraPanLeft;

            // buttons on top left
            DefaultUserCommands[34] = UserCommand.ControlGearUp;
            DefaultUserCommands[35] = UserCommand.ControlGearDown;
            DefaultUserCommands[36] = UserCommand.ControlGearDown;
            DefaultUserCommands[37] = UserCommand.ControlGearDown;
            DefaultUserCommands[38] = UserCommand.ControlAlerter;
            DefaultUserCommands[39] = UserCommand.ControlSander;
            DefaultUserCommands[40] = UserCommand.ControlPantograph1;
            DefaultUserCommands[41] = UserCommand.ControlBellToggle;
            DefaultUserCommands[42] = UserCommand.ControlHorn;
            DefaultUserCommands[43] = UserCommand.ControlHorn;

        }

        /// <summary>
        /// Initializes a new instances of the <see cref="InputSettings"/> class with the specified options.
        /// </summary>
        /// <param name="options">The list of one-time options to override persisted settings, if any.</param>
        public RailDriverSettings(IEnumerable<string> options)
        : base(SettingsStore.GetSettingStore(UserSettings.SettingsFilePath, UserSettings.RegistryKey, "RailDriver"))
        {
            CalibrationSettings = new byte[DefaultCalibrationSettings.Length];

            for (byte i = 0; i < DefaultUserCommands.Length; i++)
            {
                UserCommands[(int)DefaultUserCommands[i]] = i;
            }

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
                return DefaultCommands[(int)userCommand];
            }
            else
                throw new ArgumentOutOfRangeException($"Enum parameter {nameof(name)} not within expected range of either {nameof(RailDriverCalibrationSetting)} or {nameof(UserCommands)}");
        }

        public override void Reset()
        {
            foreach (RailDriverCalibrationSetting setting in (RailDriverCalibrationSetting[])Enum.GetValues(typeof(RailDriverCalibrationSetting)))
                Reset(setting.ToString());

            foreach (UserCommand command in (UserCommand[])Enum.GetValues(typeof(UserCommand)))
                Reset(command.ToString());
        }

        public override void Save()
        {
            default0WhileSaving = true; //temporarily "disable" default calibration settings, so Calibration Settings are always getting written to SettingsStore

            foreach (RailDriverCalibrationSetting setting in (RailDriverCalibrationSetting[])Enum.GetValues(typeof(RailDriverCalibrationSetting)))
                Save(setting.ToString());

            foreach(UserCommand command in (UserCommand[])Enum.GetValues(typeof(UserCommand)))
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
            foreach (RailDriverCalibrationSetting setting in (RailDriverCalibrationSetting[])Enum.GetValues(typeof(RailDriverCalibrationSetting)))
                Load(allowUserSettings, optionsDictionary, setting.ToString(), typeof(byte));
            foreach (var command in (UserCommand[])Enum.GetValues(typeof(UserCommand)))
                Load(allowUserSettings, optionsDictionary, command.ToString(), typeof(string));
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
    }
}
