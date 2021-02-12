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
        [Default(new[] { 1200, 800 })]
#pragma warning disable CA1819 // Properties should not return arrays
        public int[] WindowSize { get; set; }
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
