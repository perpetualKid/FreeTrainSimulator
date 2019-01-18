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
    }

    public class RailDriverSettings : SettingsBase
    {
        private static readonly GettextResourceManager catalog = new GettextResourceManager("ORTS.Settings");

        public static readonly byte[] DefaultCommands = new byte[Enum.GetNames(typeof(UserCommands)).Length];
        public readonly byte[] UserCommands = new byte[Enum.GetNames(typeof(UserCommands)).Length];

        public readonly byte[] CalibrationSettings;

        //caching values
        private static readonly RailDriverCalibrationSetting[] calibrationSettingNames = (RailDriverCalibrationSetting[])Enum.GetValues(typeof(RailDriverCalibrationSetting));
        private static readonly UserCommands[] userCommandNames = (UserCommands[])Enum.GetValues(typeof(UserCommands));

        static RailDriverSettings()
        {
            InitializeCommands(DefaultCommands);
        }

        private static void InitializeCommands(byte[] commands)
        { }

        private static byte[] DefaultCalibrationSettings()
        {
            return new byte[] { 225, 116, 60, 229, 176, 42, 119, 216, 79, 58, 213, 179, 30, 209, 109, 121, 73, 135, 180, 86, 145, 189 };
        }

    /// <summary>
    /// Initializes a new instances of the <see cref="InputSettings"/> class with the specified options.
    /// </summary>
    /// <param name="options">The list of one-time options to override persisted settings, if any.</param>
    public RailDriverSettings(IEnumerable<string> options)
        : base(SettingsStore.GetSettingStore(UserSettings.SettingsFilePath, UserSettings.RegistryKey, "RailDriver"))
        {
            InitializeCommands(UserCommands);
            CalibrationSettings = DefaultCalibrationSettings();
            Load(options);
        }

        public override object GetDefaultValue(string name)
        {
            if (Enum.TryParse(name, true, out RailDriverCalibrationSetting calibrationSetting))
            {
                return DefaultCalibrationSettings()[(int)calibrationSetting];        // no default parameters for Calibration Settings
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
            throw new NotImplementedException();
        }

        public override void Save()
        {
            foreach (RailDriverCalibrationSetting setting in calibrationSettingNames)
                Save(setting.ToString());
        }

        public override void Save(string name)
        {
            Save(name, typeof(string));
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
            foreach (RailDriverCalibrationSetting setting in calibrationSettingNames)
                Load(allowUserSettings, optionsDictionary, setting.ToString(), typeof(string));
            foreach (var command in userCommandNames)
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
