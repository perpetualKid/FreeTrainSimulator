// COPYRIGHT 2012, 2013 by the Open Rails project.
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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using Orts.Common;
using Orts.Common.Info;
using Orts.Settings.Store;

namespace Orts.Settings
{

    public class UserSettings : SettingsBase
    {
        private static readonly string[] subSettings = new string[] { "FolderSettings", "Input", "RailDriver", "Dispatcher" };

        public static string DeletedSaveFolder { get; private set; }  // ie @"C:\Users\Wayne\AppData\Roaming\Open Rails\Deleted Saves"
        public static string SavePackFolder { get; private set; }     // ie @"C:\Users\Wayne\AppData\Roaming\Open Rails\Save Packs"

        private static readonly StoreType SettingsStoreType;
        private static readonly string Location;

        private readonly Dictionary<string, object> customDefaultValues = new Dictionary<string, object>();

#pragma warning disable CA1810 // Initialize reference type static fields inline
        static UserSettings()
#pragma warning restore CA1810 // Initialize reference type static fields inline
        {
            //TODO: user settings (as any other runtime data) may not be saved in exe folder (which might be under \Program Files if installed via installer)
            // default user settings are searched in order: Json, Ini, Registry
            if (File.Exists(Location = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), EnumExtension.GetDescription(StoreType.Json))))
            {
                SettingsStoreType = StoreType.Json;
            }
            if (File.Exists(Location = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), EnumExtension.GetDescription(StoreType.Ini))))
            {
                SettingsStoreType = StoreType.Ini;
            }
            else
            {
                SettingsStoreType = StoreType.Registry;
                Location = EnumExtension.GetDescription(StoreType.Registry);
            }

            Directory.CreateDirectory(RuntimeInfo.UserDataFolder);
            DeletedSaveFolder = Path.Combine(RuntimeInfo.UserDataFolder, "Deleted Saves");
            SavePackFolder = Path.Combine(RuntimeInfo.UserDataFolder, "Save Packs");
        }

        #region User Settings

        // Please put all user settings in here as auto-properties. Public properties
        // of type 'string', 'int', 'bool', 'string[]' and 'int[]' are automatically loaded/saved.

        // Main menu settings:
        [Default(true)]
        public bool Logging { get; set; }
        [Default(false)]
        public bool LogErrorsOnly { get; set; }
        [Default(false)]
        public bool LogSaveData { get; set; }
        [Default("")]
        public string Multiplayer_User { get; set; }
        [Default("127.0.0.1")]
        public string Multiplayer_Host { get; set; }
        [Default(30000)]
        public int Multiplayer_Port { get; set; }

        // General settings:

        [Default(false)]
        public bool WebServer { get; set; }
        [Default(2150)]
        public int WebServerPort { get; set; }

        [Default(false)]
        public bool Alerter { get; set; }
        [Default(true)]
        public bool AlerterDisableExternal { get; set; }
        [Default(true)]
        public bool SpeedControl { get; set; }
        [Default(false)]
        public bool GraduatedRelease { get; set; }
        [Default(false)]
        public bool RetainersOnAllCars { get; set; }
        [Default(false)]
        public bool SuppressConfirmations { get; set; }
        [Default(1500)]
        public int NotificationsTimeout { get; set; }
        [Default(21)]
        public int BrakePipeChargingRate { get; set; }
        [Default("")]
        public string Language { get; set; }
        [Default(PressureUnit.Automatic)]
        public PressureUnit PressureUnit { get; set; }
        [Default(MeasurementUnit.Route)]
        public MeasurementUnit MeasurementUnit { get; set; }
        [Default(false)]
        public bool DisableTCSScripts { get; set; }
        [Default(true)]
        public bool StartGamePaused { get; set; }

        // Audio settings:
        [Default(100)]
        public int SoundVolumePercent { get; set; }
        [Default(5)]
        public int SoundDetailLevel { get; set; }
        [Default(50)]
        public int ExternalSoundPassThruPercent { get; set; } // higher = louder sound

        // Video settings:
        [Default(true)]
        public bool FullScreen { get; set; }
        [Default(true)]
        public bool NativeFullscreenResolution { get; set; }
        [Default(true)]
        public bool DynamicShadows { get; set; }
        [Default(false)]
        public bool ShadowAllShapes { get; set; }
        [Default(false)]
        public bool WindowGlass { get; set; }
        [Default(true)]
        public bool ModelInstancing { get; set; }
        [Default(true)]
        public bool Wire { get; set; }
        [Default(true)]
        public bool VerticalSync { get; set; }
        [Default(true)]
        public bool EnableMultisampling { get; set; }
        [Default(4)]
        public int MultisamplingCount { get; set; }
        [Default(0)]
        public int Cab2DStretch { get; set; }
        [Default(2000)]
        public int ViewingDistance { get; set; }
        [Default(true)]
        public bool DistantMountains { get; set; }
        [Default(40000)]
        public int DistantMountainsViewingDistance { get; set; }
        [Default(45)] // MSTS uses 60 FOV horizontally, on 4:3 displays this is 45 FOV vertically (what OR uses).
        public int ViewingFOV { get; set; }
        [Default(true)]
        public bool LODViewingExtension { get; set; }
        [Default(49)]
        public int WorldObjectDensity { get; set; }
        [Default(20)]
        public int DayAmbientLight { get; set; }
        #region Game Window Settings
        [Default(new string[]
        {
            nameof(WindowSetting.Location) + "=50,50",  // % of the windows Screen, centered
            nameof(WindowSetting.Size) + "=1024,768"    // absolute pixels
        })]
        public EnumArray<int[], WindowSetting> WindowSettings { get; set; }

        [Default(-1)]
        public int WindowScreen { get; set; }

        #endregion

        // Simulation settings:
        [Default(true)]
        public bool UseAdvancedAdhesion { get; set; }
        [Default(10)]
        public int AdhesionMovingAverageFilterSize { get; set; }
        [Default(false)]
        public bool BreakCouplers { get; set; }
        [Default(false)]
        public bool CurveSpeedDependent { get; set; }
        [Default(true)]
        public bool HotStart { get; set; }
        [Default(true)]
        public bool SimpleControlPhysics { get; set; }
        [Default(true)]
        public bool DieselEngineStart { get; set; }

        // Data logger settings:
        [Default(SeparatorChar.Comma)]
        public SeparatorChar DataLoggerSeparator { set; get; }
        [Default(SpeedUnit.Route)]
        public SpeedUnit DataLogSpeedUnits { get; set; }
        [Default(false)]
        public bool DataLogStart { get; set; }
        [Default(true)]
        public bool DataLogPerformance { get; set; }
        [Default(false)]
        public bool DataLogPhysics { get; set; }
        [Default(false)]
        public bool DataLogMisc { get; set; }
        [Default(false)]
        public bool DataLogSteamPerformance { get; set; }
        [Default(false)]
        public bool VerboseConfigurationMessages { get; set; }

        // Evaluation settings:
        [Default(false)]
        public bool EvaluationTrainSpeed { get; set; }
        [Default(10)]
        public int EvaluationInterval { get; set; }
        //Time, Train Speed, Max Speed, Signal Aspect, Elevation, Direction, Distance Travelled, Control Mode, Throttle, Brake, Dyn Brake, Gear
        [Default(EvaluationLogContents.Time | EvaluationLogContents.Speed | EvaluationLogContents.MaxSpeed)]
        public EvaluationLogContents EvaluationContent { get; set; }
        [Default(false)]
        public bool EvaluationStationStops { get; set; }

        // Updater settings
        #region update settings
        [Default((int)Common.UpdateCheckFrequency.Always)]
        public int UpdateCheckFrequency { get; set; }
        [Default("https://orts.blob.core.windows.net/releases/index.json")]
        public string UpdateSource { get; set; }
        [Default(false)]
        public bool UpdatePreReleases { get; set; }
        #endregion

        // Timetable settings:
        [Default(true)]
        public bool TTUseRestartDelays { get; set; }
        [Default(true)]
        public bool TTCreateTrainOnPoolUnderflow { get; set; }
        [Default(false)]
        public bool TTOutputTimetableTrainInfo { get; set; }
        [Default(false)]
        public bool TTOutputTimetableFullEvaluation { get; set; }

        // Experimental settings:
        [Default(0)]
        public int UseSuperElevation { get; set; }
        [Default(50)]
        public int SuperElevationMinLen { get; set; }
        [Default(1435)]
        public int SuperElevationGauge { get; set; }
        [Default(0)]
        public int LODBias { get; set; }
        [Default(false)]
        public bool PerformanceTuner { get; set; }
        [Default(true)]
        public bool SuppressShapeWarnings { get; set; }
        [Default(60)]
        public int PerformanceTunerTarget { get; set; }
        [Default(false)]
        public bool DoubleWire { get; set; }
        [Default(false)]
        public bool AuxActionEnabled { get; set; }
        [Default(false)]
        public bool UseLocationPassingPaths { get; set; }
        [Default(false)]
        public bool UseMSTSEnv { get; set; }
        [Default(false)]
        public bool SignalLightGlow { get; set; }
        [Default(100)]
        public int AdhesionFactor { get; set; }
        [Default(10)]
        public int AdhesionFactorChange { get; set; }
        [Default(false)]
        public bool AdhesionProportionalToWeather { get; set; }
        [Default(false)]
        public bool NoForcedRedAtStationStops { get; set; }
        [Default(100)]
        public int PrecipitationBoxHeight { get; set; }
        [Default(500)]
        public int PrecipitationBoxWidth { get; set; }
        [Default(500)]
        public int PrecipitationBoxLength { get; set; }
        [Default(false)]
        public bool CorrectQuestionableBrakingParams { get; set; }
        [Default(false)]
        public bool OpenDoorsInAITrains { get; set; }
        [Default(0)]
        public int ActRandomizationLevel { get; set; }
        [Default(0)]
        public int ActWeatherRandomizationLevel { get; set; }

        // Hidden settings:
        [Default(0)]
        public int CarVibratingLevel { get; set; }
        [Default("OpenRailsLog.txt")]
        public string LoggingFilename { get; set; }
        [Default("Evaluation.txt")]
        public string DebriefEvalFilename { get; set; }//
        [Default("")] // If left as "", OR will use the user's desktop folder
        public string LoggingPath { get; set; }
        [Default("")]
        public string ScreenshotPath { get; set; }
        [Default(true)]
        public bool ShadowMapBlur { get; set; }
        [Default(4)]
        public int ShadowMapCount { get; set; }
        [Default(0)]
        public int ShadowMapDistance { get; set; }
        [Default(1024)]
        public int ShadowMapResolution { get; set; }

        #region in-game settings
        [Default(true)]
        public bool OdometerShortDistanceMode { get; set; }
        #endregion

        // Internal settings:
        [Default(false)]
        public bool DataLogger { get; set; }
        [Default(false)]
        public bool Letterbox2DCab { get; set; }
        [Default(false)]
        public bool Profiling { get; set; }
        [Default(0)]
        public int ProfilingFrameCount { get; set; }
        [Default(0)]
        public int ProfilingTime { get; set; }
        [Default(0)]
        public int ReplayPauseBeforeEndS { get; set; }
        [Default(true)]
        public bool ReplayPauseBeforeEnd { get; set; }
        [Default(true)]
        public bool ShowErrorDialogs { get; set; }
        [Default(new string[0])]
#pragma warning disable CA1819 // Properties should not return arrays
        public string[] Menu_Selection { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays
        [Default(false)]
        public bool Multiplayer { get; set; }

        [Default(new string[]
        {
            $"{nameof(ViewerWindowType.QuitWindow)}=50,50",
            $"{nameof(ViewerWindowType.HelpWindow)}=20,70",
            $"{nameof(ViewerWindowType.DebugOverlay)}=0,0",
            $"{nameof(ViewerWindowType.ActivityWindow)}=50,30",
            $"{nameof(ViewerWindowType.CompassWindow)}=50,0",
            $"{nameof(ViewerWindowType.SwitchWindow)}=0,50",
            $"{nameof(ViewerWindowType.EndOfTrainDeviceWindow)}=20,50",
            $"{nameof(ViewerWindowType.NextStationWindow)}=0,100",
            $"{nameof(ViewerWindowType.DetachTimetableTrainWindow)}=0,60",
            $"{nameof(ViewerWindowType.TrainListWindow)}=80,40",
            $"{nameof(ViewerWindowType.MultiPlayerWindow)}=20,60",
            $"{nameof(ViewerWindowType.DrivingTrainWindow)}=100,40",
            $"{nameof(ViewerWindowType.DistributedPowerWindow)}=10,20",
            $"{nameof(ViewerWindowType.PauseOverlay)}=0,0",
            $"{nameof(ViewerWindowType.TrainOperationsWindow)}=50,50",
            $"{nameof(ViewerWindowType.CarOperationsWindow)}=50,50",
            $"{nameof(ViewerWindowType.TrackMonitorWindow)}=100,0",
            $"{nameof(ViewerWindowType.MultiPlayerMessagingWindow)}=50,50",
            $"{nameof(ViewerWindowType.NotificationOverlay)}=0,0",
            $"{nameof(ViewerWindowType.CarIdentifierOverlay)}=0,0",
            $"{nameof(ViewerWindowType.LocationsOverlay)}=0,0",
            $"{nameof(ViewerWindowType.TrackItemOverlay)}=0,0",
        })]
        public EnumArray<int[], ViewerWindowType> PopupLocations { get; set; }

        [Default(new string[]
        {
            $"{nameof(ViewerWindowType.QuitWindow)}=False",
            $"{nameof(ViewerWindowType.DebugOverlay)}=False",
            $"{nameof(ViewerWindowType.HelpWindow)}=True",
            $"{nameof(ViewerWindowType.ActivityWindow)}=False",
            $"{nameof(ViewerWindowType.CompassWindow)}=False",
            $"{nameof(ViewerWindowType.SwitchWindow)}=False",
            $"{nameof(ViewerWindowType.EndOfTrainDeviceWindow)}=False",
            $"{nameof(ViewerWindowType.NextStationWindow)}=False",
            $"{nameof(ViewerWindowType.DetachTimetableTrainWindow)}=False",
            $"{nameof(ViewerWindowType.TrainListWindow)}=False",
            $"{nameof(ViewerWindowType.MultiPlayerWindow)}=False",
            $"{nameof(ViewerWindowType.DrivingTrainWindow)}=False",
            $"{nameof(ViewerWindowType.DistributedPowerWindow)}=False",
            $"{nameof(ViewerWindowType.PauseOverlay)}=False",
            $"{nameof(ViewerWindowType.TrainOperationsWindow)}=False",
            $"{nameof(ViewerWindowType.CarOperationsWindow)}=False",
            $"{nameof(ViewerWindowType.TrackMonitorWindow)}=False",
            $"{nameof(ViewerWindowType.MultiPlayerMessagingWindow)}=False",
            $"{nameof(ViewerWindowType.NotificationOverlay)}=False",
            $"{nameof(ViewerWindowType.CarIdentifierOverlay)}=False",
            $"{nameof(ViewerWindowType.LocationsOverlay)}=False",
            $"{nameof(ViewerWindowType.TrackItemOverlay)}=False",
        })]
        public EnumArray<bool, ViewerWindowType> PopupStatus { get; set; }

        [Default(new string[]
        {
            $"{nameof(ViewerWindowType.QuitWindow)}=\"\"",
            $"{nameof(ViewerWindowType.DebugOverlay)}=\"\"",
            $"{nameof(ViewerWindowType.HelpWindow)}=\"\"",
            $"{nameof(ViewerWindowType.ActivityWindow)}=\"\"",
            $"{nameof(ViewerWindowType.CompassWindow)}=\"\"",
            $"{nameof(ViewerWindowType.SwitchWindow)}=\"\"",
            $"{nameof(ViewerWindowType.EndOfTrainDeviceWindow)}=\"\"",
            $"{nameof(ViewerWindowType.NextStationWindow)}=\"\"",
            $"{nameof(ViewerWindowType.DetachTimetableTrainWindow)}=\"\"",
            $"{nameof(ViewerWindowType.TrainListWindow)}=\"\"",
            $"{nameof(ViewerWindowType.MultiPlayerWindow)}=\"\"",
            $"{nameof(ViewerWindowType.DrivingTrainWindow)}=\"\"",
            $"{nameof(ViewerWindowType.DistributedPowerWindow)}=\"\"",
            $"{nameof(ViewerWindowType.PauseOverlay)}=\"\"",
            $"{nameof(ViewerWindowType.TrainOperationsWindow)}=\"\"",
            $"{nameof(ViewerWindowType.CarOperationsWindow)}=\"\"",
            $"{nameof(ViewerWindowType.TrackMonitorWindow)}=\"\"",
            $"{nameof(ViewerWindowType.MultiPlayerMessagingWindow)}=\"\"",
            $"{nameof(ViewerWindowType.NotificationOverlay)}=\"\"",
            $"{nameof(ViewerWindowType.CarIdentifierOverlay)}=\"\"",
            $"{nameof(ViewerWindowType.LocationsOverlay)}=\"\"",
            $"{nameof(ViewerWindowType.TrackItemOverlay)}=\"\"",
        })]

        public EnumArray<string, ViewerWindowType> PopupSettings { get; set; }
        // Menu-game communication settings:
        [Default(false)]
        [DoNotSave]
        public bool MultiplayerClient { get; set; }
        #endregion

        public FolderSettings FolderSettings { get; private set; }

        public InputSettings Input { get; private set; }

        public RailDriverSettings RailDriver { get; private set; }

        public DispatcherSettings Dispatcher { get; private set; }

        public UserSettings() :
            this(Array.Empty<string>())
        { }

        public UserSettings(IEnumerable<string> options) :
            this(options, SettingsStore.GetSettingsStore(SettingsStoreType, Location, null))
        {
        }

        public UserSettings(IEnumerable<string> options, SettingsStore store) :
            base(store)
        {
            customDefaultValues["LoggingPath"] = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            customDefaultValues["ScreenshotPath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Application.ProductName);
            customDefaultValues["Multiplayer_User"] = Environment.UserName;
            LoadSettings(options);
            FolderSettings = new FolderSettings(options, store);
            Input = new InputSettings(options, store);
            RailDriver = new RailDriverSettings(options, store);
            Dispatcher = new DispatcherSettings(options, store);
        }

        public override object GetDefaultValue(string name)
        {
            PropertyInfo property = GetProperty(name);

            if (customDefaultValues.TryGetValue(property.Name, out object value))
                return value;

            object defaultValue = property.GetCustomAttributes<DefaultAttribute>(false).FirstOrDefault()?.Value;
            Type propertyType = property.PropertyType;
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(EnumArray<,>).GetGenericTypeDefinition())
            {
                defaultValue = InitializeEnumArrayDefaults(propertyType, defaultValue);
            }
            return defaultValue ?? new InvalidDataException($"UserSetting {property.Name} has no default value.");
        }

        protected override PropertyInfo[] GetProperties()
        {
            properties ??= base.GetProperties().Where(pi => !subSettings.Contains(pi.Name)).ToArray();
            return properties;
        }

        protected override object GetValue(string name)
        {
            return GetProperty(name).GetValue(this, null);
        }

        protected override void SetValue(string name, object value)
        {
            GetProperty(name).SetValue(this, value, null);
        }

        protected override void Load(bool allowUserSettings, NameValueCollection optionalValues)
        {
            foreach (PropertyInfo property in GetProperties())
                LoadSetting(allowUserSettings, optionalValues, property.Name);
            properties = null;
        }

        public override void Save()
        {
            foreach (PropertyInfo property in GetProperties())
                Save(property.Name);

            FolderSettings.Save();
            Input.Save();
            RailDriver.Save();
            Dispatcher.Save();
            properties = null;
        }

        public override void Save(string name)
        {
            if (AllowPropertySaving(name))
            {
                SaveSetting(name);
            }
        }

        public override void Reset()
        {
            foreach (PropertyInfo property in GetProperties())
                Reset(property.Name);
        }
    }
}
