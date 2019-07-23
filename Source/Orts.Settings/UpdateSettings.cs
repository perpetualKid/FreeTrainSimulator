﻿// COPYRIGHT 2014 by the Open Rails project.
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Orts.Common;

namespace Orts.Settings
{
    public class UpdateSettings : SettingsBase
    {
        public static readonly string SettingsFilePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Updater.ini");

        #region User Settings

        // Please put all update settings in here as auto-properties. Public properties
        // of type 'string', 'int', 'bool', 'string[]' and 'int[]' are automatically loaded/saved.

        [Default("")]
        public string Channel { get; set; }
        [Default("")]
        public string URL { get; set; }
        public TimeSpan TTL { get; set; }
        [Default("")]
        public string ChangeLogLink { get; set; }

        #endregion

        public UpdateSettings()
            : base(SettingsStore.GetSettingStore(SettingsFilePath, null, "Settings"))
        {
            LoadSettings(new string[0]);
        }

        public UpdateSettings(string channel)
            : base(SettingsStore.GetSettingStore(SettingsFilePath, null, channel + "Settings"))
        {
            LoadSettings(new string[0]);
        }

        public string[] GetChannels()
        {
            // We are always a local INI settings store.
            return (from name in (SettingStore as SettingsStoreLocalIni).GetSectionNames()
                    where name.EndsWith("Settings")
                    select name.Replace("Settings", "")).ToArray();
        }

        public override object GetDefaultValue(string name)
        {

            if (name == nameof(TTL))
                return TimeSpan.FromDays(1);

            var property = GetType().GetProperty(name);

            var attributes = property.GetCustomAttributes(typeof(DefaultAttribute), false);
            if (attributes.Length > 0)
                return (attributes[0] as DefaultAttribute)?.Value;

            throw new InvalidDataException($"UserSetting {property.Name} has no default value.");
        }

        protected override object GetValue(string name)
        {
            return GetProperty(name).GetValue(this, null);
        }

        protected override void SetValue(string name, object value)
        {
            GetProperty(name).SetValue(this, value, null);
        }

        protected override void Load(bool allowUserSettings, Dictionary<string, string> optionsDictionary)
        {
            foreach (var property in GetProperties())
                LoadSetting(allowUserSettings, optionsDictionary, property.Name);
        }

        public override void Save()
        {
            foreach (var property in GetProperties())
                if (property.GetCustomAttributes(typeof(DoNotSaveAttribute), false).Length == 0)
                    SaveSetting(property.Name);
        }

        public override void Save(string name)
        {
            var property = GetProperty(name);
            if (property.GetCustomAttributes(typeof(DoNotSaveAttribute), false).Length == 0)
                SaveSetting(property.Name);
        }

        public override void Reset()
        {
            foreach (var property in GetProperties())
                Reset(property.Name);
        }
    }
}