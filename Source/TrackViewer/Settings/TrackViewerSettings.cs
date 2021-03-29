using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

using Orts.Common;
using Orts.Settings;
using Orts.Settings.Store;

namespace Orts.TrackViewer.Settings
{
    public class TrackViewerSettings : SettingsBase
    {
        internal const string SettingLiteral = "TrackViewer";

        private static readonly StoreType SettingsStoreType;
        private static readonly string Location;

        private PropertyInfo[] properties;

#pragma warning disable CA1810 // Initialize reference type static fields inline
        static TrackViewerSettings()
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

        public TrackViewerSettings(IEnumerable<string> options) :
            this(options, SettingsStore.GetSettingsStore(SettingsStoreType, Location, null))
        {
        }

        internal TrackViewerSettings(IEnumerable<string> options, SettingsStore store) :
            base(SettingsStore.GetSettingsStore(store.StoreType, store.Location, SettingLiteral))
        {
            LoadSettings(options);
            UserSettings = new UserSettings(options, store);
        }

        public UserSettings UserSettings { get; private set; }

        #region TrackViewer Settings
#pragma warning disable CA1819 // Properties should not return arrays
        [Default(new[] { 1200, 800 })] 
        public int[] WindowSize { get; set; }
        
        [Default("DarkGray")] 
        public string ColorBackground { get; set; }
        
        [Default("Blue")] 
        public string ColorRailTrack { get; set; }
        
        [Default("BlueViolet")] 
        public string ColorRailTrackEnd { get; set; }
        
        [Default("DarkMagenta")] 
        public string ColorRailTrackJunction { get; set; }

        [Default("Firebrick")]
        public string ColorRailTrackCrossing { get; set; }

        [Default("Crimson")]
        public string ColorRailLevelCrossing { get; set; }

        [Default("Olive")] 
        public string ColorRoadTrack { get; set; }

        [Default("ForestGreen")]
        public string ColorRoadTrackEnd { get; set; }

        [Default("DeepPink")]
        public string ColorRoadLevelCrossing { get; set; }
        
        [Default("White")]
        public string ColorRoadCarSpawner { get; set; }

        [Default("ForestGreen")] 
        public string ColorSidingItem { get; set; }
        
        [Default("Navy")] 
        public string ColorPlatformItem { get; set; }
        
        [Default("RoyalBlue")] 
        public string ColorSpeedpostItem { get; set; }
        
        [Default("White")] 
        public string ColorHazardItem { get; set; }
        
        [Default("White")] 
        public string ColorPickupItem { get; set; }
        
        [Default("White")] 
        public string ColorLevelCrossingItem { get; set; }
        
        [Default("White")] 
        public string ColorSoundRegionItem { get; set; }
        
        [Default("White")] 
        public string ColorSignalItem { get; set; }

        [Default(new string[0])]
        public string[] RouteSelection { get; set; }

        [Default(new string[0])]
        public string[] LastLocation { get; set; }

        [Default(false)] 
        public bool RestoreLastView { get; set; }

        [Default(TrackViewerViewSettings.All)]
        public TrackViewerViewSettings ViewSettings { get; set; }

        [Default("{Application} Log.txt")]
        public string LogFilename { get; set; }

#pragma warning restore CA1819 // Properties should not return arrays
        #endregion

        public override object GetDefaultValue(string name)
        {
            PropertyInfo property = GetProperty(name);

            return property.GetCustomAttributes<DefaultAttribute>(false).FirstOrDefault()?.Value ?? throw new InvalidDataException($"TrackViewer setting {property.Name} has no default value.");
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
