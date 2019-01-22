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
        private static readonly byte[] DefaultCommands = new byte[Enum.GetNames(typeof(UserCommands)).Length];
        private static readonly byte[] DefaultCalibrationSettings;
        private static readonly UserCommands[] DefaultUserCommands;

        private bool default0WhileSaving;

        public readonly byte[] UserCommands = new byte[Enum.GetNames(typeof(UserCommands)).Length];

        public readonly byte[] CalibrationSettings;

        static RailDriverSettings()
        {
            //default calibration settings from another developer's PC, they are as good as random numbers...
            DefaultCalibrationSettings = new byte[] { 225, 116, 60, 229, 176, 42, 119, 216, 79, 58, 213, 179, 30, 209, 109, 121, 73, 135, 180, 86, 145, 189, 5 };
            DefaultUserCommands = new UserCommands[48]; //there are 6 bytes for User-Buttons on RailDriver board

            // top row of blue buttons left to right
            DefaultUserCommands[0] = Common.UserCommands.GamePauseMenu;                 // Btn 00 Default Legend Game Pause
            DefaultUserCommands[1] = Common.UserCommands.GameSave;                      // Btn 01 Default Legend Game Save
                                                                                        // Btn 02 Default Legend Control Gauges
            DefaultUserCommands[3] = Common.UserCommands.DisplayTrackMonitorWindow;     // Btn 03 Default Legend Track Monitor
                                                                                        // Btn 04 Default Legend Station/Siding Names
                                                                                        // Btn 05 Default Legend Car #
            DefaultUserCommands[6] = Common.UserCommands.DisplaySwitchWindow;           // Btn 06 Default Legend Switching Drive Aids
            DefaultUserCommands[7] = Common.UserCommands.DisplayTrainOperationsWindow;  // Btn 07 Default Legend Train Operations
            DefaultUserCommands[8] = Common.UserCommands.DisplayNextStationWindow;      // Btn 08 Default Legend Next Station Window
                                                                                        // Btn 09 Default Legend Ops Notebook
                                                                                        // Btn 10 Default Legend Hide Drive Aids
            DefaultUserCommands[11] = Common.UserCommands.DisplayCompassWindow;         // Btn 11 Default Legend Compass Window
            DefaultUserCommands[12] = Common.UserCommands.GameSwitchAhead;              // Btn 12 Default Legend Switch Ahead
            DefaultUserCommands[13] = Common.UserCommands.GameSwitchBehind;             // Btn 13 Default Legend Switch Behind

            // bottom row of blue buttons left to right
            DefaultUserCommands[14] = Common.UserCommands.GameExternalCabController;    // Btn 14 Default Legend RailDriver Run/Stop
            DefaultUserCommands[15] = Common.UserCommands.CameraToggleShowCab;          // Btn 15 Default Legend Hide Cab Panel
            DefaultUserCommands[16] = Common.UserCommands.CameraCab;                    // Btn 16 Default Legend Frnt Cab View
            DefaultUserCommands[17] = Common.UserCommands.CameraOutsideFront;           // Btn 17 Default Legend Ext View 1
            DefaultUserCommands[18] = Common.UserCommands.CameraOutsideRear;            // Btn 18 Default Legend Ext.View 2
            DefaultUserCommands[19] = Common.UserCommands.CameraCarPrevious;            // Btn 19 Default Legend FrontCoupler
            DefaultUserCommands[20] = Common.UserCommands.CameraCarNext;                // Btn 20 Default Legend Rear Coupler
            DefaultUserCommands[21] = Common.UserCommands.CameraTrackside;              // Btn 21 Default Legend Track View      
            DefaultUserCommands[22] = Common.UserCommands.CameraPassenger;              // Btn 22 Default Legend Passgr View      
            DefaultUserCommands[23] = Common.UserCommands.CameraBrakeman;               // Btn 23 Default Legend Coupler View
            DefaultUserCommands[24] = Common.UserCommands.CameraFree;                   // Btn 24 Default Legend Yard View
            DefaultUserCommands[25] = Common.UserCommands.GameClearSignalForward;       // Btn 25 Default Legend Request Pass
                                                                                        // Btn 26 Default Legend Load/Unload
                                                                                        // Btn 27 Default Legend OK

            // controls to right of blue buttons
            DefaultUserCommands[28] = Common.UserCommands.CameraZoomIn;
            DefaultUserCommands[29] = Common.UserCommands.CameraZoomOut;
            DefaultUserCommands[30] = Common.UserCommands.CameraPanUp;
            DefaultUserCommands[31] = Common.UserCommands.CameraPanRight;
            DefaultUserCommands[32] = Common.UserCommands.CameraPanDown;
            DefaultUserCommands[33] = Common.UserCommands.CameraPanLeft;

            // buttons on top left
            DefaultUserCommands[34] = Common.UserCommands.ControlGearUp;
            DefaultUserCommands[35] = Common.UserCommands.ControlGearDown;
            DefaultUserCommands[36] = Common.UserCommands.ControlGearDown;
            DefaultUserCommands[37] = Common.UserCommands.ControlGearDown;
            DefaultUserCommands[38] = Common.UserCommands.ControlAlerter;
            DefaultUserCommands[39] = Common.UserCommands.ControlSander;
            DefaultUserCommands[40] = Common.UserCommands.ControlPantograph1;
            DefaultUserCommands[41] = Common.UserCommands.ControlBellToggle;
            DefaultUserCommands[42] = Common.UserCommands.ControlHorn;
            DefaultUserCommands[43] = Common.UserCommands.ControlHorn;

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
            else if (Enum.TryParse(name, true, out UserCommands userCommand))
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

            foreach (UserCommands command in (UserCommands[])Enum.GetValues(typeof(UserCommands)))
                Reset(command.ToString());
        }

        public override void Save()
        {
            default0WhileSaving = true; //temporarily "disable" default calibration settings, so Calibration Settings are always getting written to SettingsStore

            foreach (RailDriverCalibrationSetting setting in (RailDriverCalibrationSetting[])Enum.GetValues(typeof(RailDriverCalibrationSetting)))
                Save(setting.ToString());

            foreach(UserCommands command in (UserCommands[])Enum.GetValues(typeof(UserCommands)))
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
            else if (Enum.TryParse(name, true, out UserCommands userCommand))
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
            foreach (var command in (UserCommands[])Enum.GetValues(typeof(UserCommands)))
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
            else if (Enum.TryParse(name, true, out UserCommands userCommand))
            {
                UserCommands[(int)userCommand] = (byte)value;
            }
            else
                throw new ArgumentOutOfRangeException($"Enum parameter {nameof(name)} not within expected range of either {nameof(RailDriverCalibrationSetting)} or {nameof(UserCommands)}");
        }
    }
}
