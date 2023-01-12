using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

using Orts.Common;
using Orts.Common.Input;
using Orts.Settings.Store;

namespace Orts.Settings
{
    public class RailDriverSettings : SettingsBase
    {
        private static readonly Dictionary<UserCommand, byte> DefaultUserCommands = 
            new Dictionary<UserCommand, byte>
            {

                // top row of blue buttons left to right
                { UserCommand.GamePauseMenu, 0 },                  // Btn 00 Default Legend Game Pause
                { UserCommand.GameSave, 1 },                       // Btn 01 Default Legend Game Save
                                                                   // Btn 02 Default Legend Control Gauges
                { UserCommand.DisplayTrackMonitorWindow, 3 },      // Btn 03 Default Legend Track Monitor
                                                                   // Btn 04 Default Legend Station/Siding Names
                                                                   // Btn 05 Default Legend Car #
                { UserCommand.DisplaySwitchWindow, 6 },            // Btn 06 Default Legend Switching Drive Aids
                { UserCommand.DisplayTrainOperationsWindow, 7 },   // Btn 07 Default Legend Train Operations
                { UserCommand.DisplayNextStationWindow, 8 },       // Btn 08 Default Legend Next Station Window
                                                                   // Btn 09 Default Legend Ops Notebook
                                                                   // Btn 10 Default Legend Hide Drive Aids
                { UserCommand.DisplayCompassWindow, 11 },          // Btn 11 Default Legend Compass Window
                { UserCommand.GameSwitchAhead, 12 },               // Btn 12 Default Legend Switch Ahead
                { UserCommand.GameSwitchBehind, 13 },              // Btn 13 Default Legend Switch Behind

                // bottom row of blue buttons left to right
                { UserCommand.GameExternalCabController, 14 },     // Btn 14 Default Legend RailDriver Run/Stop
                { UserCommand.CameraToggleShowCab, 15 },           // Btn 15 Default Legend Hide Cab Panel
                { UserCommand.CameraCab, 16 },                     // Btn 16 Default Legend Frnt Cab View
                { UserCommand.CameraOutsideFront, 17 },            // Btn 17 Default Legend Ext View 1
                { UserCommand.CameraOutsideRear, 18 },             // Btn 18 Default Legend Ext.View 2
                { UserCommand.CameraCarPrevious, 19 },             // Btn 19 Default Legend FrontCoupler
                { UserCommand.CameraCarNext, 20 },                 // Btn 20 Default Legend Rear Coupler
                { UserCommand.CameraTrackside, 21 },               // Btn 21 Default Legend Track View      
                { UserCommand.CameraPassenger, 22 },               // Btn 22 Default Legend Passgr View      
                { UserCommand.CameraBrakeman, 23 },                // Btn 23 Default Legend Coupler View
                { UserCommand.CameraFree, 24 },                    // Btn 24 Default Legend Yard View
                { UserCommand.GameClearSignalForward, 25 },        // Btn 25 Default Legend Request Pass
                                                                   // Btn 26 Default Legend Load/Unload
                                                                   // Btn 27 Default Legend OK

                // controls to right of blue buttons
                { UserCommand.CameraZoomIn, 28 },
                { UserCommand.CameraZoomOut, 29 },
                { UserCommand.CameraPanUp, 30 },
                { UserCommand.CameraPanRight, 31 },
                { UserCommand.CameraPanDown, 32 },
                { UserCommand.CameraPanLeft, 33 },

                // buttons on top left
                { UserCommand.ControlGearUp, 34 },
                { UserCommand.ControlGearDown, 35 },
                { UserCommand.ControlEmergencyPushButton, 36 },
                //{ UserCommand.ControlEmergencyPushButton, 37 },   //can't map the same command to different buttons
                { UserCommand.ControlAlerter, 38 },
                { UserCommand.ControlSander, 39 },
                { UserCommand.ControlPantograph1, 40 },
                { UserCommand.ControlBellToggle, 41 },
                { UserCommand.ControlHorn, 42 },
                //{ UserCommand.ControlHorn, 43 },                  //can't map the same command to different buttons
            };

        private bool default0WhileSaving;

        public EnumArray<byte, UserCommand> UserCommands { get; } = new EnumArray<byte, UserCommand>();

        public EnumArray<byte, RailDriverCalibrationSetting> CalibrationSettings { get; } = new EnumArray<byte, RailDriverCalibrationSetting>();

        internal RailDriverSettings(IEnumerable<string> options, SettingsStore store) :
            base(SettingsStore.GetSettingsStore(store.StoreType, store.Location, "RailDriver"))
        {
            LoadSettings(options);
        }

        public override object GetDefaultValue(string name)
        {
            if (EnumExtension.GetValue(name, out RailDriverCalibrationSetting calibrationSetting))
            {
                return default0WhileSaving ? 0 : RailDriverDevice.DefaultCalibrationSettings[calibrationSetting];
            }
            else if (EnumExtension.GetValue(name, out UserCommand userCommand))
            {
                return GetDefaultValue(userCommand);
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), $"Enum parameter {nameof(name)} not within expected range of either {nameof(RailDriverCalibrationSetting)} or {nameof(UserCommands)}");
        }

        public static byte GetDefaultValue(UserCommand command)
        {
            return DefaultUserCommands.TryGetValue(command, out byte value) ? value : byte.MaxValue;
        }

        public override void Reset()
        {
            //do not reset calibrations
            //foreach (RailDriverCalibrationSetting setting in EnumExtension.GetValues<RailDriverCalibrationSetting>())
            //    Reset(setting.ToString());

            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
                Reset(command.ToString());
        }

        public override void Save()
        {
            default0WhileSaving = true; //temporarily "disable" default calibration settings, so Calibration Settings are always getting written to SettingsStore
            foreach (RailDriverCalibrationSetting setting in EnumExtension.GetValues<RailDriverCalibrationSetting>())
                SaveSetting(setting.ToString());

            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
                SaveSetting(command.ToString());

            default0WhileSaving = false;
            properties = null;
        }

        protected override object GetValue(string name)
        {
            if (EnumExtension.GetValue(name, out RailDriverCalibrationSetting calibrationSetting))
            {
                return CalibrationSettings[calibrationSetting];
            }
            else if (EnumExtension.GetValue(name, out UserCommand userCommand))
            {
                return UserCommands[userCommand];
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), $"Enum parameter {nameof(name)} not within expected range of either {nameof(RailDriverCalibrationSetting)} or {nameof(UserCommands)}");
        }

        protected override void Load(bool allowUserSettings, NameValueCollection optionalValues)
        {
            foreach (RailDriverCalibrationSetting setting in EnumExtension.GetValues<RailDriverCalibrationSetting>())
                LoadSetting(allowUserSettings, optionalValues, setting.ToString());
            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
                LoadSetting(allowUserSettings, optionalValues, command.ToString());
            properties = null;
        }

        protected override void SetValue(string name, object value)
        {
            if (EnumExtension.GetValue(name, out RailDriverCalibrationSetting calibrationSetting))
            {
                if (!byte.TryParse(value?.ToString(), out byte result))
                    result = 0;
                CalibrationSettings[calibrationSetting] = result;
            }
            else if (EnumExtension.GetValue(name, out UserCommand userCommand))
            {
                UserCommands[userCommand] = (byte)value;
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), $"Enum parameter {nameof(name)} not within expected range of either {nameof(RailDriverCalibrationSetting)} or {nameof(UserCommands)}");
        }

        public static string CheckForErrors(byte[] buttonSettings)
        {
            StringBuilder errors = new StringBuilder();

            var duplicates = buttonSettings.Where(button => button < 255).
                Select((value, index) => new { Index = index, Button = value }).
                GroupBy(g => g.Button).
                Where(g => g.Count() > 1).
                OrderBy(g => g.Key);

            foreach (var duplicate in duplicates)
            {
                errors.Append(catalog.GetString("Button {0} is assigned to \r\n\t", duplicate.Key));
                foreach (var buttonMapping in duplicate)
                {
                    errors.Append(catalog.GetString($"\"{((UserCommand)buttonMapping.Index).GetLocalizedDescription()}\" and "));
                }
                errors.Remove(errors.Length - 5, 5);
                errors.AppendLine();
            }
            return errors.ToString();
        }

        public void DumpToText(string filePath)
        {
            var buttonMappings = UserCommands.
                Select((value, index) => new { Index = index, Button = value }).
                Where(button => button.Button < 255).
                OrderBy(button => button.Button);

            using (StreamWriter writer = new StreamWriter(File.OpenWrite(filePath)))
            {
                writer.WriteLine("{0,-40}{1,-40}", "Command", "Button");
                writer.WriteLine(new string('=', 40 * 2));
                foreach (var buttonMapping in buttonMappings)
                    writer.WriteLine("{0,-40}{1,-40}", ((UserCommand)buttonMapping.Index).GetLocalizedDescription(), buttonMapping.Button);
            }
        }

    }
}
