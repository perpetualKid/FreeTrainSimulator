using System;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Toolbox.PopupWindows;

using Orts.Settings;
using Orts.Settings.Store;

namespace FreeTrainSimulator.Toolbox.Settings
{
    public class ToolboxSettings : SettingsBase
    {
        internal const string SettingLiteral = "Toolbox";

        private static readonly StoreType SettingsStoreType;
        private static readonly string Location;

        private PropertyInfo[] properties;

#pragma warning disable CA1810 // Initialize reference type static fields inline
        static ToolboxSettings()
#pragma warning restore CA1810 // Initialize reference type static fields inline
        {
            if (File.Exists(Location = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), StoreType.Json.GetDescription())))
            {
                SettingsStoreType = StoreType.Json;
            }
            if (File.Exists(Location = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), StoreType.Ini.GetDescription())))
            {
                SettingsStoreType = StoreType.Ini;
            }
            else
            {
                SettingsStoreType = StoreType.Registry;
                Location = StoreType.Registry.GetDescription();
            }
        }

        public ToolboxSettings(in ImmutableArray<string> options) :
            this(options, SettingsStore.GetSettingsStore(SettingsStoreType, Location, null))
        {
        }

        internal ToolboxSettings(in ImmutableArray<string> options, SettingsStore store) :
            base(SettingsStore.GetSettingsStore(store.StoreType, store.Location, SettingLiteral))
        {
            LoadSettings(options);
            UserSettings = new UserSettings(options, store);
        }

        public UserSettings UserSettings { get; private set; }

        #region Toolbox Settings
        [Default(new string[]
        {
            $"{nameof(WindowSetting.Location)}=50,50",  // % of the windows Screen
            $"{nameof(WindowSetting.Size)}=75,75"    // % of screen size
        })]
        public EnumArray<int[], WindowSetting> WindowSettings { get; set; }

        [Default(0)]
        public int WindowScreen { get; set; }

#pragma warning disable CA1819 // Properties should not return arrays
        [Default(new string[0])]
        public string[] RouteSelection { get; set; }

        [Default(new string[0])]
        public string[] PathSelection { get; set; }

        [Default(new string[0])]
        public string[] LastLocation { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays

        [Default(true)]
        public bool RestoreLastView { get; set; }

        [Default(true)]
        public bool OutlineFont { get; set; }

        [Default(new string[]
        {
        $"{nameof(MapContentType.Tracks)}=True",
        $"{nameof(MapContentType.EndNodes)}=True",
        $"{nameof(MapContentType.JunctionNodes)}=True",
        $"{nameof(MapContentType.LevelCrossings)}=True",
        $"{nameof(MapContentType.CrossOvers)}=True",
        $"{nameof(MapContentType.Roads)}=True",
        $"{nameof(MapContentType.RoadEndNodes)}=True",
        $"{nameof(MapContentType.RoadCrossings)}=True",
        $"{nameof(MapContentType.CarSpawners)}=True",
        $"{nameof(MapContentType.Sidings)}=True",
        $"{nameof(MapContentType.SidingNames)}=True",
        $"{nameof(MapContentType.Platforms)}=True",
        $"{nameof(MapContentType.PlatformNames)}=True",
        $"{nameof(MapContentType.StationNames)}=True",
        $"{nameof(MapContentType.SpeedPosts)}=True",
        $"{nameof(MapContentType.MilePosts)}=True",
        $"{nameof(MapContentType.Signals)}=True",
        $"{nameof(MapContentType.OtherSignals)}=True",
        $"{nameof(MapContentType.Hazards)}=True",
        $"{nameof(MapContentType.Pickups)}=True",
        $"{nameof(MapContentType.SoundRegions)}=True",
        $"{nameof(MapContentType.Grid)}=True",
        $"{nameof(MapContentType.Paths)}=True",
        })]
        public EnumArray<bool, MapContentType> ViewSettings { get; set; }

        [Default("{Application} Log.txt")]
        public string LogFilename { get; set; }

        [Default(new string[]{
            $"{nameof(ColorSetting.Background)}={nameof(Microsoft.Xna.Framework.Color.DarkGray)}",
            $"{nameof(ColorSetting.RailTrack)}={nameof(Microsoft.Xna.Framework.Color.Blue)}",
            $"{nameof(ColorSetting.RailTrackEnd)}={nameof(Microsoft.Xna.Framework.Color.BlueViolet)}",
            $"{nameof(ColorSetting.RailTrackJunction)}={nameof(Microsoft.Xna.Framework.Color.DarkMagenta)}",
            $"{nameof(ColorSetting.RailTrackCrossing)}={nameof(Microsoft.Xna.Framework.Color.Firebrick)}",
            $"{nameof(ColorSetting.RailLevelCrossing)}={nameof(Microsoft.Xna.Framework.Color.Crimson)}",
            $"{nameof(ColorSetting.RoadTrack)}={nameof(Microsoft.Xna.Framework.Color.Olive)}",
            $"{nameof(ColorSetting.RoadTrackEnd)}={nameof(Microsoft.Xna.Framework.Color.ForestGreen)}",
            $"{nameof(ColorSetting.RoadLevelCrossing)}={nameof(Microsoft.Xna.Framework.Color.DeepPink)}",
            $"{nameof(ColorSetting.PathTrack)}={nameof(Microsoft.Xna.Framework.Color.Gold)}",
            $"{nameof(ColorSetting.RoadCarSpawner)}={nameof(Microsoft.Xna.Framework.Color.White)}",
            $"{nameof(ColorSetting.SignalItem)}={nameof(Microsoft.Xna.Framework.Color.White)}",
            $"{nameof(ColorSetting.StationItem)}={nameof(Microsoft.Xna.Framework.Color.Firebrick)}",
            $"{nameof(ColorSetting.PlatformItem)}={nameof(Microsoft.Xna.Framework.Color.Navy)}",
            $"{nameof(ColorSetting.SidingItem)}={nameof(Microsoft.Xna.Framework.Color.ForestGreen)}",
            $"{nameof(ColorSetting.SpeedPostItem)}={nameof(Microsoft.Xna.Framework.Color.RoyalBlue)}",
            $"{nameof(ColorSetting.HazardItem)}={nameof(Microsoft.Xna.Framework.Color.White)}",
            $"{nameof(ColorSetting.PickupItem)}={nameof(Microsoft.Xna.Framework.Color.White)}",
            $"{nameof(ColorSetting.SoundRegionItem)}={nameof(Microsoft.Xna.Framework.Color.White)}",
            $"{nameof(ColorSetting.LevelCrossingItem)}={nameof(Microsoft.Xna.Framework.Color.White)}",
        })]
        public EnumArray<string, ColorSetting> ColorSettings { get; set; }

        [Default(new string[]
        {
            $"{nameof(ToolboxWindowType.QuitWindow)}=50,50",
            $"{nameof(ToolboxWindowType.AboutWindow)}=50,50",
            $"{nameof(ToolboxWindowType.StatusWindow)}=50,50",
            $"{nameof(ToolboxWindowType.DebugScreen)}=0,0",
            $"{nameof(ToolboxWindowType.LocationWindow)}=100,100",
            $"{nameof(ToolboxWindowType.HelpWindow)}=10,90",
            $"{nameof(ToolboxWindowType.TrackNodeInfoWindow)}=10,70",
            $"{nameof(ToolboxWindowType.TrackItemInfoWindow)}=30,70",
            $"{nameof(ToolboxWindowType.SettingsWindow)}=70,70",
            $"{nameof(ToolboxWindowType.LogWindow)}=30,70",
            $"{nameof(ToolboxWindowType.TrainPathWindow)}=10,40",
        })]
        public EnumArray<int[], ToolboxWindowType> PopupLocations { get; set; }

        [Default(new string[]
        {
            $"{nameof(ToolboxWindowType.QuitWindow)}=False",
            $"{nameof(ToolboxWindowType.AboutWindow)}=False",
            $"{nameof(ToolboxWindowType.StatusWindow)}=False",
            $"{nameof(ToolboxWindowType.DebugScreen)}=False",
            $"{nameof(ToolboxWindowType.LocationWindow)}=True",
            $"{nameof(ToolboxWindowType.HelpWindow)}=True",
            $"{nameof(ToolboxWindowType.TrackNodeInfoWindow)}=True",
            $"{nameof(ToolboxWindowType.TrackItemInfoWindow)}=True",
            $"{nameof(ToolboxWindowType.SettingsWindow)}=True",
            $"{nameof(ToolboxWindowType.LogWindow)}=False",
            $"{nameof(ToolboxWindowType.TrainPathWindow)}=False",
        })]
        public EnumArray<bool, ToolboxWindowType> PopupStatus { get; set; }

        [Default(new string[]
        {
            $"{nameof(ToolboxWindowType.QuitWindow)}=\"\"",
            $"{nameof(ToolboxWindowType.AboutWindow)}=\"\"",
            $"{nameof(ToolboxWindowType.StatusWindow)}=\"\"",
            $"{nameof(ToolboxWindowType.DebugScreen)}=\"\"",
            $"{nameof(ToolboxWindowType.LocationWindow)}=\"\"",
            $"{nameof(ToolboxWindowType.HelpWindow)}=\"\"",
            $"{nameof(ToolboxWindowType.TrackNodeInfoWindow)}=\"\"",
            $"{nameof(ToolboxWindowType.TrackItemInfoWindow)}=\"\"",
            $"{nameof(ToolboxWindowType.SettingsWindow)}=\"\"",
            $"{nameof(ToolboxWindowType.LogWindow)}=\"\"",
            $"{nameof(ToolboxWindowType.TrainPathWindow)}=\"\"",
        })]
        public EnumArray<string, ToolboxWindowType> PopupSettings { get; set; }


        [Default("Arial")]
        public string TextFont { get; set; }

        [Default(13)]
        public int FontSize { get; set; }
        #endregion

        public override object GetDefaultValue(string name)
        {
            PropertyInfo property = GetProperty(name);
            object defaultValue = property.GetCustomAttributes<DefaultAttribute>(false).FirstOrDefault()?.Value;
            Type propertyType = property.PropertyType;
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(EnumArray<,>).GetGenericTypeDefinition())
            {
                defaultValue = InitializeEnumArrayDefaults(propertyType, defaultValue);
            }
            return defaultValue ?? throw new InvalidDataException($"Toolbox setting {property.Name} has no default value.");
        }

        protected override PropertyInfo[] GetProperties()
        {
            properties ??= base.GetProperties().Where(pi => !string.Equals("UserSettings", pi.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            return properties;
        }

        public override void Reset()
        {
            foreach (PropertyInfo property in GetProperties())
                Reset(property.Name);
        }

        public override void Save()
        {
            foreach (PropertyInfo property in GetProperties())
                Save(property.Name);
        }

        protected override object GetValue(string name)
        {
            return GetProperty(name).GetValue(this, null);
        }

        protected override void Load(bool allowUserSettings, NameValueCollection optionalValues)
        {
            foreach (PropertyInfo property in GetProperties())
                LoadSetting(allowUserSettings, optionalValues, property.Name);
            ResetCachedProperties();
        }

        protected override void SetValue(string name, object value)
        {
            GetProperty(name).SetValue(this, value, null);
        }
    }
}
