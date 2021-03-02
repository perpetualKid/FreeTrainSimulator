using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;

using Orts.Settings.Store;

namespace Orts.Settings
{
    public class TrackViewerSettings : SettingsBase
    {
        internal const string SettingLiteral = "TrackViewer";

        internal TrackViewerSettings(IEnumerable<string> options, SettingsStore store) :
            base(SettingsStore.GetSettingsStore(store.StoreType, store.Location, SettingLiteral))
        {
            LoadSettings(options);
        }

        #region TrackViewer Settings
#pragma warning disable CA1819 // Properties should not return arrays
        [Default(new[] { 1200, 800 })] public int[] WindowSize { get; set; }
        [Default("CadetBlue")] public string ColorBackground { get; set; }
        [Default("Black")] public string ColorRailTrack { get; set; }
        [Default("Gray")] public string ColorTrackEnd { get; set; }
        [Default("Gray")] public string ColorTrackJunction { get; set; }
        [Default("Gray")] public string ColorRoadTrack { get; set; }
        [Default("Gray")] public string ColorSidingItem { get; set; }
        [Default("Gray")] public string ColorPlatformItem { get; set; }
        [Default("Gray")] public string ColorSpeedpostItem { get; set; }
        [Default("Gray")] public string ColorHazardItem { get; set; }
        [Default("Gray")] public string ColorPickupItem { get; set; }
        [Default("Gray")] public string ColorLevelCrossingItem { get; set; }
        [Default("Gray")] public string ColorSoundRegionItem { get; set; }
        [Default("Gray")] public string ColorSignalItem { get; set; }
        [Default(new string[0])] public string[] RouteSelection { get; set; }
        [Default(false)] public bool LoadRouteOnStart { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays
        #endregion

        public override object GetDefaultValue(string name)
        {
            PropertyInfo property = GetProperty(name);

            return property.GetCustomAttributes<DefaultAttribute>(false).FirstOrDefault()?.Value ?? throw new InvalidDataException($"TrackViewer setting {property.Name} has no default value.");
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
            properties = null;
        }

        protected override void SetValue(string name, object value)
        {
            GetProperty(name).SetValue(this, value, null);
        }
    }
}
