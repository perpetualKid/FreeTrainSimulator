using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

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
        public string[] LastLocation { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays

        [Default(false)]
        public bool RestoreLastView { get; set; }

        [Default(TrackViewerViewSettings.All)]
        public TrackViewerViewSettings ViewSettings { get; set; }

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
            nameof(ColorSetting.RoadCarSpawner)+"=White",
            nameof(ColorSetting.SignalItem)+"=White",
            nameof(ColorSetting.PlatformItem)+"=Navy",
            nameof(ColorSetting.SidingItem)+"=ForestGreen",
            nameof(ColorSetting.SpeedPostItem)+"=RoyalBlue",
            nameof(ColorSetting.HazardItem)+"=White",
            nameof(ColorSetting.PickupItem)+"=White",
            nameof(ColorSetting.SoundRegionItem)+"=White",
            nameof(ColorSetting.LevelCrossingItem)+"=White",
            nameof(ColorSetting.TrainPathMain)+"=Yellow",
        })]
        public EnumArray<string, ColorSetting> ColorSettings { get; set; }

        [Default(new string[]
        {
            nameof(WindowType.QuitWindow) + "=50,50",
            nameof(WindowType.StatusWindow) + "=50,50",
        })]
        public EnumArray<int[], WindowType> WindowLocations { get; set; }

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
            return defaultValue ?? throw new InvalidDataException($"TrackViewer setting {property.Name} has no default value.");
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

        protected override void ResetCachedProperties()
        {
            properties = null;
            base.ResetCachedProperties();
        }
    }
}
