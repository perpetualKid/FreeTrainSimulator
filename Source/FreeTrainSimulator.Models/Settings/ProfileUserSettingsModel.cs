using System.Diagnostics;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".usersettings")]
    public sealed partial record ProfileUserSettingsModel : ProfileSettingsModelBase
    {
        private static ProfileUserSettingsModel defaultModel;

        public override ProfileModel Parent => base.Parent as ProfileModel;

        public static ProfileUserSettingsModel Default => defaultModel ??= new ProfileUserSettingsModel();

        [MemoryPackIgnore]
        public ProfileKeyboardSettingsModel KeyboardSettings { get; set; }

        [MemoryPackIgnore]
        public ProfileRailDriverSettingsModel RailDriverSettings { get; set; }
        #region Logging
        public TraceEventType LogLevel { get; set; } = TraceEventType.Verbose;
        public string LogFileName { get; set; } = "{Product} {Application} Log.txt";
        public string LogFilePath { get; set; } = RuntimeInfo.LogFilesFolder;
        public bool ErrorDialogEnabled { get; set; } = true;
        public bool ShapeWarnings { get; set; }
        public bool ConfigurationMessages { get; set; }
        #endregion

        #region general settings
        public string Language { get; set; }
        public PressureUnit PressureUnit { get; set; } = PressureUnit.Automatic;
        public MeasurementUnit MeasurementUnit { get; set; } = MeasurementUnit.Route;
        public bool PerformanceTuner { get; set; }
        public int PerformanceTunerTarget { get; set; } = 60;
        public bool PauseAtStart { get; set; } = true;
        public bool TcsScripts {  get; set; }
        public int NotificationsTimeout { get; set; } = 1500;
        public bool Confirmations { get; set; }
        public bool Alerter { get; set; }
        public bool AlerterExternal { get; set; }
        public bool SpeedControl { get; set; } = true;

        #endregion

        #region Game Window Settings
        public EnumArray<(int X, int Y), WindowSetting> WindowSettings { get; set; } = new EnumArray<(int X, int Y), WindowSetting>((WindowSetting windowSetting) => windowSetting switch
        {
            WindowSetting.Location => (50, 50),// % of the windows Screen, centered
            WindowSetting.Size => (1024, 768),// absolute pixels
            _ => throw new System.NotImplementedException(),
        });
        public ScreenMode ScreenMode { get; set; } = ScreenMode.BorderlessFullscreen;
        public int WindowScreen { get; set; } = -1;
        #endregion

        #region in-game settings
        #region in-game windows
        public EnumArray<(int X, int Y), ViewerWindowType> PopupLocations { get; set; } = new EnumArray<(int X, int Y), ViewerWindowType>((ViewerWindowType windowType) => windowType switch
        {
            ViewerWindowType.QuitWindow => (50, 50),
            ViewerWindowType.HelpWindow => (20, 70),
            ViewerWindowType.DebugOverlay => (0, 0),
            ViewerWindowType.ActivityWindow => (50, 30),
            ViewerWindowType.CompassWindow => (50, 0),
            ViewerWindowType.SwitchWindow => (0, 50),
            ViewerWindowType.EndOfTrainDeviceWindow => (20, 50),
            ViewerWindowType.NextStationWindow => (0, 100),
            ViewerWindowType.DetachTimetableTrainWindow => (0, 60),
            ViewerWindowType.TrainListWindow => (80, 40),
            ViewerWindowType.MultiPlayerWindow => (20, 60),
            ViewerWindowType.DrivingTrainWindow => (100, 40),
            ViewerWindowType.DistributedPowerWindow => (10, 20),
            ViewerWindowType.PauseOverlay => (0, 0),
            ViewerWindowType.TrainOperationsWindow => (50, 50),
            ViewerWindowType.CarOperationsWindow => (50, 50),
            ViewerWindowType.TrackMonitorWindow => (100, 0),
            ViewerWindowType.MultiPlayerMessagingWindow => (50, 50),
            ViewerWindowType.NotificationOverlay => (0, 0),
            ViewerWindowType.CarIdentifierOverlay => (0, 0),
            ViewerWindowType.LocationsOverlay => (0, 0),
            ViewerWindowType.TrackItemOverlay => (0, 0),
            _ => throw new System.NotImplementedException(),
        });

        public EnumArray<bool, ViewerWindowType> PopupStatus { get; set; } = new EnumArray<bool, ViewerWindowType>((ViewerWindowType windowType) => windowType switch
        {
            ViewerWindowType.QuitWindow => false,
            ViewerWindowType.HelpWindow => false,
            ViewerWindowType.DebugOverlay => false,
            ViewerWindowType.ActivityWindow => false,
            ViewerWindowType.CompassWindow => false,
            ViewerWindowType.SwitchWindow => false,
            ViewerWindowType.EndOfTrainDeviceWindow => false,
            ViewerWindowType.NextStationWindow => false,
            ViewerWindowType.DetachTimetableTrainWindow => false,
            ViewerWindowType.TrainListWindow => false,
            ViewerWindowType.MultiPlayerWindow => false,
            ViewerWindowType.DrivingTrainWindow => false,
            ViewerWindowType.DistributedPowerWindow => false,
            ViewerWindowType.PauseOverlay => false,
            ViewerWindowType.TrainOperationsWindow => false,
            ViewerWindowType.CarOperationsWindow => false,
            ViewerWindowType.TrackMonitorWindow => false,
            ViewerWindowType.MultiPlayerMessagingWindow => false,
            ViewerWindowType.NotificationOverlay => false,
            ViewerWindowType.CarIdentifierOverlay => false,
            ViewerWindowType.LocationsOverlay => false,
            ViewerWindowType.TrackItemOverlay => false,
            _ => throw new System.NotImplementedException(),

        });

        public EnumArray<string, ViewerWindowType> PopupSettings { get; set; } = new EnumArray<string, ViewerWindowType>();
        #endregion
        public bool OdometerShortDistances { get; set; } = true;
        public int VibrationLevel { get; set; }

        #endregion

        #region Audio settings
        public int SoundVolumePercent { get; set; } = 100;
        public int SoundDetailLevel { get; set; } = 5;
        public int ExternalSoundPassThruPercent { get; set; } = 50; // higher = louder sound
        #endregion

        #region video settings
        public int MultiSamplingCount { get; set; } = 4;
        public bool DynamicShadows { get; set; } = true;
        public bool ShadowAllShapes { get; set; }
        public bool ModelInstancing { get; set; } = true;
        public OverheadWireType OverheadWireType { get; set; } = OverheadWireType.SingleWire;
        public bool VerticalSync { get; set; } = true;
        public int Cab2DStretch { get; set; }
        public int ViewingDistance { get; set; } = 2_000;
        public int FarMountainsViewingDistance { get; set; } = 40_000;
        // MSTS uses 60 FOV horizontally, on 4:3 displays this is 45 FOV vertically (what OR uses).
        public int FieldOfView { get; set; } = 45;
        public bool ExtendedDetailLevelView { get; set; } = true;
        public int DetailLevelBias { get; set; }

        public int VisibleDetailLevel { get; set; } = 49;
        public int AmbientBrightness { get; set; } = 20;
        public bool ShadowMapBlur { get; set; } = true;
        public int ShadowMapCount { get; set; } = 4;
        public int ShadowMapResolution { get; set; } = 1024;
        public bool SignalLightGlow { get; set; }

        #endregion

        #region Simulation settings
        public bool AdvancedAdhesion { get; set; } = true;
        public int AdhesionFilterSize { get; set; } = 10;
        public int AdhesionFactor { get; set; } = 100;
        public int AdhesionFactorChange { get; set; } = 10;
        public bool WeatherDependentAdhesion { get; set; }
        public bool CouplersBreak { get; set; }
        public bool CurveDependentSpeedLimits { get; set; }
        public bool SimplifiedControls { get; set; } = true;
        public bool SteamHotStart { get; set; } = true;
        public bool DieselEngineRun { get; set; } = true;
        public bool ElectricPowerConnected { get; set; } = true;

        public int ActivityRandomizationLevel { get; set; }
        public int WeatherRandomizationLevel { get; set; }
        public bool ComputerTrainDoors { get; set; }
        public bool GraduatedRelease { get; set; }
        public bool RetainersOnAllCars { get; set; }
        public int BrakePipeChargingRate { get; set; } = 21;

        public int SuperElevationLevel { get; set; }
        public int TrackGauge { get; set; } = 1435;

        public bool UseLocationPassingPaths { get; set; }
        public bool MstsEnvironment { get; set; }
        public bool ForcedRedStationStops { get; set; }
        public bool ValidateBrakingParams { get; set; }

        #endregion

        #region Online
        public string MultiplayerUser { get; set; }
        public string MultiplayerHost { get; set; } = "127.0.0.1";
        public int MultiplayerPort { get; set; } = 30_000;
        public bool WebServer { get; set; }
        public int WebServerPort { get; set; } = 2150;

        #endregion

        #region data logger
        public bool DataLogger { get; set; }
        public SeparatorChar DataLogSeparator { set; get; } = SeparatorChar.Comma;
        public SpeedUnit DataLogSpeedUnits { get; set; } = SpeedUnit.Route;
        public bool DataLogStart { get; set; }
        public bool DataLogPerformance { get; set; } = true;
        public bool DataLogPhysics { get; set; }
        public bool DataLogMisc { get; set; }
        public bool DataLogSteamPerformance { get; set; }
        #endregion

        #region evaluation
        public bool EvaluationTrainSpeed { get; set; }
        public int EvaluationInterval { get; set; } = 10;
        //Time, Train Speed, Max Speed, Signal Aspect, Elevation, Direction, Distance Travelled, Control Mode, Throttle, Brake, Dyn Brake, Gear
        public EvaluationLogContents EvaluationContent { get; set; } = EvaluationLogContents.Time | EvaluationLogContents.Speed | EvaluationLogContents.MaxSpeed;
        public bool EvaluationStationStops { get; set; }
        #endregion

        #region profiling
        public bool Profiling { get; set; }
        public int ProfilingFrameCount { get; set; }
        public int ProfilingTime { get; set; }
        public int ProfilingFps { get; set; } = 10;
        #endregion

        public bool ReplayPause { get; set; } = true;
        public int ReplayPauseDuration { get; set; }

        public bool MultiPlayer { get; set; }
    }
}
