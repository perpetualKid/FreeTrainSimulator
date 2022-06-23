using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

using Microsoft.CodeAnalysis;

using Orts.Common;
using Orts.Graphics;
using Orts.Settings;
using Orts.Settings.Store;
using Orts.Toolbox.PopupWindows;

namespace Orts.Toolbox.Settings
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
        }

        public ToolboxSettings(IEnumerable<string> options) :
            this(options, SettingsStore.GetSettingsStore(SettingsStoreType, Location, null))
        {
        }

        internal ToolboxSettings(IEnumerable<string> options, SettingsStore store) :
            base(SettingsStore.GetSettingsStore(store.StoreType, store.Location, SettingLiteral))
        {
            LoadSettings(options);
            UserSettings = new UserSettings(options, store);
        }

        public UserSettings UserSettings { get; private set; }

        #region Toolbox Settings
        [Default(new string[]
        {
            nameof(WindowSetting.Location) + "=50,50",  // % of the windows Screen
            nameof(WindowSetting.Size) + "=75, 75"    // % of screen size
        })]
        public EnumArray<int[], WindowSetting> WindowSettings { get; set; }

        [Default(0)]
        public int Screen { get; set; }

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

        [Default(new string[]
        {
        nameof(MapViewItemSettings.Tracks) + "=True",
        nameof(MapViewItemSettings.EndNodes) + "=True",
        nameof(MapViewItemSettings.JunctionNodes) + "=True",
        nameof(MapViewItemSettings.LevelCrossings) + "=True",
        nameof(MapViewItemSettings.CrossOvers) + "=True",
        nameof(MapViewItemSettings.Roads) + "=True",
        nameof(MapViewItemSettings.RoadEndNodes) + "=True",
        nameof(MapViewItemSettings.RoadCrossings) + "=True",
        nameof(MapViewItemSettings.CarSpawners) + "=True",
        nameof(MapViewItemSettings.Sidings) + "=True",
        nameof(MapViewItemSettings.SidingNames) + "=True",
        nameof(MapViewItemSettings.Platforms) + "=True",
        nameof(MapViewItemSettings.PlatformNames) + "=True",
        nameof(MapViewItemSettings.StationNames) + "=True",
        nameof(MapViewItemSettings.SpeedPosts) + "=True",
        nameof(MapViewItemSettings.MilePosts) + "=True",
        nameof(MapViewItemSettings.Signals) + "=True",
        nameof(MapViewItemSettings.OtherSignals) + "=True",
        nameof(MapViewItemSettings.Hazards) + "=True",
        nameof(MapViewItemSettings.Pickups) + "=True",
        nameof(MapViewItemSettings.SoundRegions) + "=True",
        nameof(MapViewItemSettings.Grid) + "=True",
        nameof(MapViewItemSettings.Paths) + "=True",
        nameof(MapViewItemSettings.PathEnds) + "=True",
        nameof(MapViewItemSettings.PathIntermediates) + "=True",
        nameof(MapViewItemSettings.PathJunctions) + "=True",
        nameof(MapViewItemSettings.PathReversals) + "=True",
        })]
        public EnumArray<bool, MapViewItemSettings> ViewSettings { get; set; }

        [Default("{Application} Log.txt")]
        public string LogFilename { get; set; }

        [Default(new string[]{
            nameof(ColorSetting.Background)+"=DarkGray",
            nameof(ColorSetting.RailTrack)+"=Blue",
            nameof(ColorSetting.RailTrackEnd)+"=BlueViolet",
            nameof(ColorSetting.RailTrackJunction)+"=DarkMagenta",
            nameof(ColorSetting.RailTrackCrossing)+"=Firebrick",
            nameof(ColorSetting.RailLevelCrossing)+"=Crimson",
            nameof(ColorSetting.RoadTrack)+"=Olive",
            nameof(ColorSetting.RoadTrackEnd)+"=ForestGreen",
            nameof(ColorSetting.RoadLevelCrossing)+"=DeepPink",
            nameof(ColorSetting.PathTrack)+"=Gold",
            nameof(ColorSetting.PathTrackEnd)+"=Gold",
            nameof(ColorSetting.PathTrackIntermediate)+"=Gold",
            nameof(ColorSetting.PathJunction)+"=Gold",
            nameof(ColorSetting.PathReversal)+"=Gold",
            nameof(ColorSetting.RoadCarSpawner)+"=White",
            nameof(ColorSetting.SignalItem)+"=White",
            nameof(ColorSetting.PlatformItem)+"=Navy",
            nameof(ColorSetting.SidingItem)+"=ForestGreen",
            nameof(ColorSetting.SpeedPostItem)+"=RoyalBlue",
            nameof(ColorSetting.HazardItem)+"=White",
            nameof(ColorSetting.PickupItem)+"=White",
            nameof(ColorSetting.SoundRegionItem)+"=White",
            nameof(ColorSetting.LevelCrossingItem)+"=White",
        })]
        public EnumArray<string, ColorSetting> ColorSettings { get; set; }

        [Default(new string[]
        {
            nameof(WindowType.QuitWindow) + "=50,50",
            nameof(WindowType.AboutWindow) + "=50,50",
            nameof(WindowType.StatusWindow) + "=50,50",
            nameof(WindowType.DebugScreen) + "=0,0",
            nameof(WindowType.LocationWindow) + "=100,100",
            nameof(WindowType.HelpWindow) + "=10,90",
            nameof(WindowType.TrackNodeInfoWindow) + "=10,70",
        })]
        public EnumArray<int[], WindowType> WindowLocations { get; set; }

        [Default(new string[]
        {
            nameof(WindowType.QuitWindow) + "=False",
            nameof(WindowType.AboutWindow) + "=False",
            nameof(WindowType.StatusWindow) + "=False",
            nameof(WindowType.DebugScreen) + "=False",
            nameof(WindowType.LocationWindow) + "=True",
            nameof(WindowType.HelpWindow) + "=True",
            nameof(WindowType.TrackNodeInfoWindow) + "=True",
        })]
        public EnumArray<bool, WindowType> WindowStatus { get; set; }

        [Default("Segoe UI")]
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
            if (properties == null)
                properties = base.GetProperties().Where(pi => !new string[] { "UserSettings" }.Contains(pi.Name)).ToArray();
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
