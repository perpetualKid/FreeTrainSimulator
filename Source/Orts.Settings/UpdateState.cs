// COPYRIGHT 2014, 2015 by the Open Rails project.
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
using Orts.Settings.Store;

namespace Orts.Settings
{
    public class UpdateState : SettingsBase
    {
        #region Update State (treated as Settings)

        // Please put all update settings in here as auto-properties. Public properties
        // of type 'string', 'int', 'bool', 'string[]' and 'int[]' are automatically loaded/saved.
        public DateTime LastCheck { get; set; }
        public DateTime NextCheck { get; set; }
        [Default("")]
        public string Update { get; set; }

        #endregion

        public UpdateState()
            : base(SettingsStore.GetSettingsStore(UserSettings.Instance.SettingStore.StoreType, 
                UserSettings.Instance.SettingStore.Location, "UpdateState"))
        {
            LoadSettings(new string[0]);
        }

        public UpdateState(UpdateSettings parent): 
            this (parent.SettingStore)
        {
        }

        public UpdateState(SettingsStore store): 
            base(SettingsStore.GetSettingsStore(store.StoreType, store.Location, "UpdateState"))
        {
            LoadSettings(new string[0]);
        }

        public override object GetDefaultValue(string name)
        {
            if (name == nameof(LastCheck) || name == nameof(NextCheck))
                return DateTime.MinValue;

            var property = GetProperty(name);

            if (property.GetCustomAttributes(typeof(DefaultAttribute), false).Length > 0)
                return (property.GetCustomAttributes(typeof(DefaultAttribute), false)[0] as DefaultAttribute).Value;

            throw new InvalidDataException(string.Format("UserSetting {0} has no default value.", property.Name));
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
            foreach (var property in GetProperties())
                LoadSetting(allowUserSettings, optionalValues, property.Name);
            properties = null;
        }

        public override void Save()
        {
            foreach (var property in GetProperties())
                Save(property.Name);
            properties = null;
        }

        public override void Save(string name)
        {
            if (AllowPropertySaving(name))
                SaveSetting(name);
        }

        public override void Reset()
        {
            foreach (var property in GetProperties())
                Reset(property.Name);
        }
    }
}
