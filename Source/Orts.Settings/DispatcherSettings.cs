using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;

using Orts.Common;
using Orts.Settings.Store;

namespace Orts.Settings
{
    public class DispatcherSettings : SettingsBase
    {
        internal DispatcherSettings(IEnumerable<string> options, SettingsStore store) :
            base(SettingsStore.GetSettingsStore(store.StoreType, store.Location, "Dispatcher"))
        {
            LoadSettings(options);
        }

        #region Dispatcher Settings
        [Default(new string[]
        {
            nameof(WindowSetting.Location) + "=50,50",  // % of the windows Screen
            nameof(WindowSetting.Size) + "=75, 75"    // % of screen size
        })]
        public EnumArray<int[], WindowSetting> WindowSettings { get; set; }

        [Default(0)]
        public int WindowScreen { get; set; }

        [Default(new string[]
        {
            nameof(DispatcherWindowType.SignalState) + "=100,100",
            nameof(DispatcherWindowType.HelpWindow) + "=50,50",
            nameof(DispatcherWindowType.SignalChange) + "=0,0",
            nameof(DispatcherWindowType.SwitchChange) + "=0,0",
            nameof(DispatcherWindowType.DebugScreen) + "=0,0",
            nameof(DispatcherWindowType.Settings) + "=0,100",
            nameof(DispatcherWindowType.TrainInfo) + "=75,75",
        })]
        public EnumArray<int[], DispatcherWindowType> WindowLocations { get; set; }

        [Default(new string[]
        {
            nameof(DispatcherWindowType.SignalState) + "=True",
            nameof(DispatcherWindowType.HelpWindow) + "=True",
        })]
        public EnumArray<bool, DispatcherWindowType> WindowStatus { get; set; }

        [Default(new string[]
{
        nameof(MapViewItemSettings.Tracks) + "=True",
        nameof(MapViewItemSettings.EndNodes) + "=True",
        nameof(MapViewItemSettings.JunctionNodes) + "=True",
        nameof(MapViewItemSettings.LevelCrossings) + "=True",
        nameof(MapViewItemSettings.CrossOvers) + "=True",
        nameof(MapViewItemSettings.Roads) + "=False",
        nameof(MapViewItemSettings.RoadEndNodes) + "=False",
        nameof(MapViewItemSettings.RoadCrossings) + "=False",
        nameof(MapViewItemSettings.CarSpawners) + "=False",
        nameof(MapViewItemSettings.Sidings) + "=True",
        nameof(MapViewItemSettings.SidingNames) + "=True",
        nameof(MapViewItemSettings.Platforms) + "=True",
        nameof(MapViewItemSettings.PlatformNames) + "=True",
        nameof(MapViewItemSettings.StationNames) + "=True",
        nameof(MapViewItemSettings.SpeedPosts) + "=True",
        nameof(MapViewItemSettings.MilePosts) + "=True",
        nameof(MapViewItemSettings.Signals) + "=True",
        nameof(MapViewItemSettings.OtherSignals) + "=False",
        nameof(MapViewItemSettings.Hazards) + "=False",
        nameof(MapViewItemSettings.Pickups) + "=False",
        nameof(MapViewItemSettings.SoundRegions) + "=False",
        nameof(MapViewItemSettings.Grid) + "=False",
        nameof(MapViewItemSettings.Paths) + "=False",
        nameof(MapViewItemSettings.TrainNames) + "=True",
})]
        public EnumArray<bool, MapViewItemSettings> ViewSettings { get; set; }

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
