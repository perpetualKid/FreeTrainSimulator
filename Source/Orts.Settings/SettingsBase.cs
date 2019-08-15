// COPYRIGHT 2013, 2014 by the Open Rails project.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Orts.Settings.Store;

namespace Orts.Settings
{
    /// <summary>
    /// Base class for supporting settings (either from user, commandline, default, ...)
    /// </summary>
	public abstract class SettingsBase
	{
        /// <summary>The store of the settings</summary>
        protected internal SettingsStore SettingStore { get; private set; }

        protected readonly StringCollection optionalSettings = new StringCollection();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="settings">The store for the settings</param>
		protected SettingsBase(SettingsStore settings)
		{
			SettingStore = settings;
		}

        /// <summary>
        /// Get the default value of a setting
        /// </summary>
        /// <param name="name">The name of the setting</param>
		public abstract object GetDefaultValue(string name);

        /// <summary>
        /// Get the current value of a setting
        /// </summary>
        /// <param name="name">The name of the setting</param>
		protected abstract object GetValue(string name);

        /// <summary>
        /// set the current value of a setting
        /// </summary>
        /// <param name="name">The name of the setting</param>
        /// <param name="value">The value of the setting</param>
		protected abstract void SetValue(string name, object value);

        /// <summary>
        /// Load all settings, possibly partly from the given options
        /// </summary>
        /// <param name="allowUserSettings">Are user settings allowed?</param>
        /// <param name="optionsDictionary">???</param>
		protected abstract void Load(bool allowUserSettings, NameValueCollection options);

        /// <summary>
        /// Save all settings to the store
        /// </summary>
		public abstract void Save();

        /// <summary>
        /// Save a setting to the store. Since type is not known, this is abstract.
        /// </summary>
        /// <param name="name">name of the setting</param>
		public virtual void Save(string name)
        {
            SaveSetting(name);
        }

        /// <summary>
        /// Reset all values to their default
        /// </summary>
        public abstract void Reset();

        /// <summary>
        /// Load settings from the options
        /// </summary>
        /// <param name="options">overrideable user options</param>
		protected void LoadSettings(IEnumerable<string> options)
		{
            NameValueCollection cmdOptions = new NameValueCollection();
            bool allowUserSettings = true;

            if (null != options)
            {
                // This special command-line option prevents the registry values from being used.
                allowUserSettings = !options.Contains("skip-user-settings", StringComparer.OrdinalIgnoreCase);

                // Pull apart the command-line options so we can find them by setting name.

                foreach (string option in options)
                {
                    var kvp = option.Split(new[] { '=', ':' }, 2);

                    string k = kvp[0].ToLowerInvariant();
                    string v = kvp.Length > 1 ? kvp[1].ToLowerInvariant() : "yes";
                    cmdOptions[k] = v;
                }
            }
			Load(allowUserSettings, cmdOptions);
		}

        protected void LoadSetting(bool allowUserSettings, NameValueCollection options, string name)
        {
            // Get the default value.
            dynamic defValue = GetDefaultValue(name);

            //// Read in the user setting, if it exists.
            dynamic value = allowUserSettings ? SettingStore.GetSettingValue(name, defValue) : defValue;

            // Read in the command-line option, if it exists into optValue.
            string optValueString = options[name.ToLowerInvariant()];
            dynamic optValue = null;

            if (!string.IsNullOrEmpty(optValueString))
            {
                switch (defValue)
                {
                    case bool b:
                        optValue = new[] { "true", "yes", "on", "1" }.Contains(optValueString.ToLowerInvariant().Trim());
                        break;
                    case int i:
                        if (int.TryParse(optValueString, out i))
                            optValue = i;
                        break;
                    case string[] sA:
                        optValue = optValueString.Split(',').Select(content => content.Trim()).ToArray();
                        break;
                    case int[] iA:
                        optValue = optValueString.Split(',').Select(content => int.Parse(content.Trim())).ToArray();
                        break;
                }
            }

            if (null != optValue)
            {
                optionalSettings.Add(name.ToLowerInvariant());
                value = optValue;
            }

            // int[] values must have the same number of items as default value.
            if (value is int[] && (value?.Length != defValue?.Length))
            {
                Trace.TraceWarning($"Unable to load {name} value from type {value.GetType().FullName}");
            }

            SetValue(name, value);
        }

        /// <summary>
        /// Save a setting to the store
        /// </summary>
        /// <param name="name">name of the setting</param>
        protected void SaveSetting(string name, bool includeDefaults = false)
        {

            //save the current value if
            // - current is different from default
            // - or SaveDefaults is true
            // - and this is not overriden from optionalSettings

            if (optionalSettings.Contains(name.ToLowerInvariant())) 
                return;

            dynamic defaultValue = GetDefaultValue(name);
            dynamic value = GetValue(name);

            if (includeDefaults)
            {
                SettingStore.SetSettingValue(name, value);
            }
            else if (defaultValue == value ||
                (value is int[] && (value as int[]).SequenceEqual(defaultValue as int[])) ||
                (value is string[] && (value as string[]).SequenceEqual(defaultValue as string[])))
            {
                SettingStore.DeleteUserValue(name);
            }
            else
            {
                SettingStore.SetSettingValue(name, value);
            }

            //if (defValue == value
            //    || (value is string[] && string.Join(",", (string[])defValue) == string.Join(",", (string[])value))
            //    || (value is int[] && string.Join(",", ((int[])defValue).Select(v => v.ToString()).ToArray()) == string.Join(",", ((int[])value).Select(v => v.ToString()).ToArray())))
            //{
            //    SettingStore.DeleteUserValue(name);
            //}
            //else
            //{
            //    SettingStore.SetSettingValue(name, value);
            //}
        }

        /// <summary>
        /// Reset a single setting to its default
        /// </summary>
        /// <param name="name">name of the setting</param>
        protected void Reset(string name)
        {
            SetValue(name, GetDefaultValue(name));
            SettingStore.DeleteUserValue(name);
        }

        protected virtual PropertyInfo GetProperty(string name)
        {
            return GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        }

        protected virtual PropertyInfo[] GetProperties()
        {
            return GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).ToArray();
        }

    }
}
