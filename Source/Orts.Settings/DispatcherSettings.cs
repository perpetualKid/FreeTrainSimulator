using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Common;

using Orts.Settings.Store;

namespace Orts.Settings
{
    public class DispatcherSettings : SettingsBase
    {
        internal DispatcherSettings(in ImmutableArray<string> options, SettingsStore store) :
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
            nameof(InternalDispatcherWindowType.SignalState) + "=100,100",
            nameof(InternalDispatcherWindowType.HelpWindow) + "=50,50",
            nameof(InternalDispatcherWindowType.SignalChange) + "=0,0",
            nameof(InternalDispatcherWindowType.SwitchChange) + "=0,0",
            nameof(InternalDispatcherWindowType.DebugScreen) + "=0,0",
            nameof(InternalDispatcherWindowType.Settings) + "=0,100",
            nameof(InternalDispatcherWindowType.TrainInfo) + "=75,75",
        })]
        public EnumArray<int[], InternalDispatcherWindowType> WindowLocations { get; set; }

        [Default(new string[]
        {
            nameof(InternalDispatcherWindowType.SignalState) + "=True",
            nameof(InternalDispatcherWindowType.HelpWindow) + "=True",
        })]
        public EnumArray<bool, InternalDispatcherWindowType> WindowStatus { get; set; }

        [Default(new string[]
{
        nameof(MapContentType.Tracks) + "=True",
        nameof(MapContentType.EndNodes) + "=True",
        nameof(MapContentType.JunctionNodes) + "=True",
        nameof(MapContentType.LevelCrossings) + "=True",
        nameof(MapContentType.CrossOvers) + "=True",
        nameof(MapContentType.Roads) + "=False",
        nameof(MapContentType.RoadEndNodes) + "=False",
        nameof(MapContentType.RoadCrossings) + "=False",
        nameof(MapContentType.CarSpawners) + "=False",
        nameof(MapContentType.Sidings) + "=True",
        nameof(MapContentType.SidingNames) + "=True",
        nameof(MapContentType.Platforms) + "=True",
        nameof(MapContentType.PlatformNames) + "=True",
        nameof(MapContentType.StationNames) + "=True",
        nameof(MapContentType.SpeedPosts) + "=True",
        nameof(MapContentType.MilePosts) + "=True",
        nameof(MapContentType.Signals) + "=True",
        nameof(MapContentType.OtherSignals) + "=False",
        nameof(MapContentType.Hazards) + "=False",
        nameof(MapContentType.Pickups) + "=False",
        nameof(MapContentType.SoundRegions) + "=False",
        nameof(MapContentType.Grid) + "=False",
        nameof(MapContentType.Paths) + "=False",
        nameof(MapContentType.TrainNames) + "=True",
})]
        public EnumArray<bool, MapContentType> ViewSettings { get; set; }

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
