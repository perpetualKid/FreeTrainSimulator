using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".raildriversettings")]
    public sealed partial record ProfileRailDriverSettingsModel : ProfileSettingsModelBase
    {
        private static ProfileRailDriverSettingsModel defaultModel;

        public override ProfileModel Parent => base.Parent as ProfileModel;

        public static ProfileRailDriverSettingsModel Default => defaultModel ??= new ProfileRailDriverSettingsModel();

        public EnumArray<byte, UserCommand> UserCommands { get; } = InitializeCommands();

        public EnumArray<byte, RailDriverCalibrationSetting> CalibrationSettings { get; } = new EnumArray<byte, RailDriverCalibrationSetting>();


        private static EnumArray<byte, UserCommand> InitializeCommands()
        {
            EnumArray<byte, UserCommand> commands = new EnumArray<byte, UserCommand>(byte.MaxValue);
            // top row of blue buttons left to right
            commands[UserCommand.GamePauseMenu] = 0;                  // Btn 00 Default Legend Game Pause
            commands[UserCommand.GameSave] = 1;                       // Btn 01 Default Legend Game Save
                                                                      // Btn 02 Default Legend Control Gauges
            commands[UserCommand.DisplayTrackMonitorWindow] = 3;      // Btn 03 Default Legend Track Monitor
                                                                      // Btn 04 Default Legend Station/Siding Names
                                                                      // Btn 05 Default Legend Car #
            commands[UserCommand.DisplaySwitchWindow] = 6;            // Btn 06 Default Legend Switching Drive Aids
            commands[UserCommand.DisplayTrainOperationsWindow] = 7;   // Btn 07 Default Legend Train Operations
            commands[UserCommand.DisplayNextStationWindow] = 8;       // Btn 08 Default Legend Next Station Window
                                                                      // Btn 09 Default Legend Ops Notebook
                                                                      // Btn 10 Default Legend Hide Drive Aids
            commands[UserCommand.DisplayCompassWindow] = 11;          // Btn 11 Default Legend Compass Window
            commands[UserCommand.GameSwitchAhead] = 12;               // Btn 12 Default Legend Switch Ahead
            commands[UserCommand.GameSwitchBehind] = 13;              // Btn 13 Default Legend Switch Behind

            // bottom row of blue buttons left to right
            commands[UserCommand.GameExternalCabController] = 14;     // Btn 14 Default Legend RailDriver Run/Stop
            commands[UserCommand.CameraToggleShowCab] = 15;           // Btn 15 Default Legend Hide Cab Panel
            commands[UserCommand.CameraCab] = 16;                     // Btn 16 Default Legend Frnt Cab View
            commands[UserCommand.CameraOutsideFront] = 17;            // Btn 17 Default Legend Ext View 1
            commands[UserCommand.CameraOutsideRear] = 18;             // Btn 18 Default Legend Ext.View 2
            commands[UserCommand.CameraCarPrevious] = 19;             // Btn 19 Default Legend FrontCoupler
            commands[UserCommand.CameraCarNext] = 20;                 // Btn 20 Default Legend Rear Coupler
            commands[UserCommand.CameraTrackside] = 21;               // Btn 21 Default Legend Track View      
            commands[UserCommand.CameraPassenger] = 22;               // Btn 22 Default Legend Passgr View      
            commands[UserCommand.CameraBrakeman] = 23;                // Btn 23 Default Legend Coupler View
            commands[UserCommand.CameraFree] = 24;                    // Btn 24 Default Legend Yard View
            commands[UserCommand.GameClearSignalForward] = 25;        // Btn 25 Default Legend Request Pass
                                                                      // Btn 26 Default Legend Load/Unload
                                                                      // Btn 27 Default Legend OK

            // controls to right of blue buttons
            commands[UserCommand.CameraZoomIn] = 28;
            commands[UserCommand.CameraZoomOut] = 29;
            commands[UserCommand.CameraPanUp] = 30;
            commands[UserCommand.CameraPanRight] = 31;
            commands[UserCommand.CameraPanDown] = 32;
            commands[UserCommand.CameraPanLeft] = 33;

            // buttons on top left
            commands[UserCommand.ControlGearUp] = 34;
            commands[UserCommand.ControlGearDown] = 35;
            commands[UserCommand.ControlEmergencyPushButton] = 36;
            //commands[UserCommand.ControlEmergencyPushButton] =37 ;   //can't map the same command to different buttons
            commands[UserCommand.ControlAlerter] = 38;
            commands[UserCommand.ControlSander] = 39;
            commands[UserCommand.ControlPantograph1] = 40;
            commands[UserCommand.ControlBellToggle] = 41;
            commands[UserCommand.ControlHorn] = 42;
            //commands[UserCommand.ControlHorn] =43 ;                  //can't map the same command to different buttons

            return commands;
        }
    }
}
