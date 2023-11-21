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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

using GetText;

using Orts.Common;
using Orts.Settings.Store;

namespace Orts.Settings
{
    /// <summary>
    /// Base class for supporting settings (either from user, commandline, default, ...)
    /// </summary>
	public abstract class SettingsBase
    {
        /// <summary>The store of the settings</summary>
        internal protected SettingsStore SettingStore { get; private set; }

        internal static readonly Type[] AllowedTypes = new[] {   //allowedTypes, + Enum and EnumArray<T, TEnum> where T is any of the allowTypes
                typeof(bool),
                typeof(int),
                typeof(DateTime),
                typeof(TimeSpan),
                typeof(string),
                typeof(int[]),
                typeof(string[]),
                typeof(byte),
        };

        private protected readonly SortedSet<string> optionalSettings = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        #region reflection cache
        private protected PropertyInfo[] properties;
        private protected List<string> doNotSaveProperties;
        #endregion

        private protected static readonly ICatalog catalog = CatalogManager.Catalog;

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
		protected abstract void Load(bool allowUserSettings, NameValueCollection optionalValues);

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

        public virtual void Log()
        {
            foreach (PropertyInfo property in GetProperties().OrderBy(p => p.Name))
            {
                dynamic value = property.GetValue(this, null);
                string source = string.Empty;

                if (property.PropertyType == typeof(int[]))  //int array
                {
                    source = optionalSettings.Contains(property.Name) ? "(command-line)" :
                        (((value as int[]).SequenceEqual(GetDefaultValue(property.Name) as int[]))) ? "" : "(user set)";
                    value = string.Join(", ", (int[])value);
                }
                else if (property.PropertyType == typeof(string[]))  //string array
                {
                    source = optionalSettings.Contains(property.Name) ? "(command-line)" :
                        (((value as string[]).SequenceEqual(GetDefaultValue(property.Name) as string[]))) ? "" : "(user set)";
                    value = string.Join(", ", (string[])value);
                }
                else if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(EnumArray<,>).GetGenericTypeDefinition())
                {
                    source = "(user set)";
                    value = LogEnumArray(value);
                }
                else
                {
                    source = optionalSettings.Contains(property.Name) ? "(command-line)" :
                        (value.Equals(GetDefaultValue(property.Name)) ? "" : "(user set)");
                }

                Trace.WriteLine($"{property.Name[..Math.Min(30, property.Name.Length)],-30} = {source,-14} {value.ToString().Replace(Environment.UserName, "********")}");
            }

            properties = null;
        }

        private static string LogEnumArray<T, TEnum>(EnumArray<T, TEnum> value) where TEnum : Enum
        {
            StringBuilder builder = new StringBuilder();
            foreach (TEnum item in EnumExtension.GetValues<TEnum>())
            {
                if ((dynamic)value[item] != default(T))
                {
                    if (typeof(T).IsArray)
                    {
                        builder.Append($"{item}=");
                        foreach (dynamic arrayItem in (Array)(dynamic)value[item])
                        {
                            builder.Append($"{arrayItem},");
                        }
                        if (builder[^1] == ',')
                            builder.Length--;
                    }
                    else
                        builder.Append($"{item}={value[item]?.ToString()}");
                    builder.Append(';');
                }
            }
            if (builder.Length > 0 && builder[^1] == ';')
                builder.Length--;
            return builder.ToString();

        }
        /// <summary>
        /// Load settings from the options
        /// </summary>
        /// <param name="options">overrideable user options</param>
        protected void LoadSettings(IEnumerable<string> options)
        {
            NameValueCollection cmdOptions = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            bool allowUserSettings = true;

            if (null != options)
            {
                // This special command-line option prevents the registry values from being used.
                allowUserSettings = !options.Contains("skip-user-settings", StringComparer.OrdinalIgnoreCase);

                // Pull apart the command-line options so we can find them by setting name.
                foreach (string option in options)
                {
                    string[] kvp = option.Split(new[] { '=', ':' }, 2);

                    string k = kvp[0];
                    string v = kvp.Length > 1 ? kvp[1] : "yes";
                    cmdOptions[k] = v;
                }
            }
            Load(allowUserSettings, cmdOptions);
        }

        protected void LoadSetting(bool allowUserSettings, NameValueCollection options, string name)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Get the default value.
            dynamic defValue = GetDefaultValue(name);

            //// Read in the user setting, if it exists.
            dynamic value = allowUserSettings ? SettingStore.GetSettingValue(name, defValue) : defValue;

            // Read in the command-line option, if it exists into optValue.
            string optValueString = options[name];
            dynamic optValue = null;

            if (!string.IsNullOrEmpty(optValueString))
            {
                switch (defValue)
                {
                    case bool b:
                        optValue = new[] { "true", "yes", "on", "1" }.Contains(optValueString.Trim(), StringComparer.OrdinalIgnoreCase);
                        break;
                    case int i:
                        if (int.TryParse(optValueString, out i))
                            optValue = i;
                        break;
                    case string[] sA:
                        optValue = optValueString.Split(',').Select(content => content.Trim()).ToArray();
                        break;
                    case int[] iA:
                        optValue = optValueString.Split(',').Select(content => int.Parse(content.Trim(), CultureInfo.InvariantCulture)).ToArray();
                        break;
                    default:
                        optValue = optValueString;
                        break;
                }
            }

            if (null != optValue)
            {
                optionalSettings.Add(name);
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
            // - or includeDefaults is true
            // - and this is not overriden from optionalSettings

            if (optionalSettings.Contains(name))
                return;

            dynamic defaultValue = GetDefaultValue(name);
            dynamic value = GetValue(name);

            if (includeDefaults)
            {
                SettingStore.SetSettingValue(name, value);
            }
            else if (defaultValue == value ||
                (value is int[] && (value as int[]).SequenceEqual(defaultValue as int[])) ||
                (value is string[] && (value as string[]).SequenceEqual(defaultValue as string[])) ||
                value == null)
            {
                SettingStore.DeleteSetting(name);
            }
            else
            {
                Type valueType = value.GetType();
                if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(EnumArray<,>).GetGenericTypeDefinition())
                {
                    dynamic enumArray = Activator.CreateInstance(valueType);
                    foreach (dynamic enumName in valueType.GetGenericArguments()[1].GetEnumValues())
                    {
                        enumArray[enumName] = value[enumName];
                    }
                    value = enumArray;
                    foreach (dynamic enumName in valueType.GetGenericArguments()[1].GetEnumValues())
                    {
                        if (defaultValue[enumName] == null)
                        {
                            value[enumName] = null;
                            Debug.WriteLine($"No Default value found for {name}[{enumName}], current values will not be stored. Consider providing a default value to enable persistance of user preferences.");
                        }
                        else if (valueType.GetGenericArguments()[0].IsArray)
                        {
                            if (((Array)value[enumName]).Length == ((Array)defaultValue[enumName]).Length)
                            {
                                bool areSame = true;
                                for (int i = 0; i < ((Array)value[enumName]).Length; i++)
                                {
                                    if (value[enumName][i] != defaultValue[enumName][i])
                                    {
                                        areSame = false;
                                        break;
                                    }
                                }
                                if (areSame)
                                    value[enumName] = null;
                            }
                        }
                        else if (value[enumName] == defaultValue[enumName])
                            value[enumName] = (dynamic)(valueType.GetGenericArguments()[0].IsValueType ? defaultValue[enumName] : null);
                    }
                }
                SettingStore.SetSettingValue(name, value);
            }
        }

        /// <summary>
        /// Reset a single setting to its default
        /// </summary>
        /// <param name="name">name of the setting</param>
        protected void Reset(string name)
        {
            SetValue(name, GetDefaultValue(name));
            SettingStore.DeleteSetting(name);
        }

        protected virtual PropertyInfo GetProperty(string name)
        {
            return GetProperties().Where((p) => p.Name == name).SingleOrDefault();
        }

        protected virtual PropertyInfo[] GetProperties()
        {
            if (null == properties)
                properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            return properties;
        }

        protected virtual void ResetCachedProperties()
        {
            properties = null;
        }

        protected bool AllowPropertySaving(string propertyName)
        {
            if (null == doNotSaveProperties)
            {
                doNotSaveProperties = GetProperties().
                    Where(prop => Attribute.IsDefined(prop, typeof(DoNotSaveAttribute))).Select((p) => p.Name).ToList();
                doNotSaveProperties.Sort();
            }

            return doNotSaveProperties.BinarySearch(propertyName) < 0;
        }

        internal static bool TryParseValues(Type expectedType, string value, out dynamic result)
        {
            bool returnValue = false;
            result = default;
            if (expectedType == typeof(int))
            {
                if (returnValue = int.TryParse(value, out int i))
                    result = i;
                else
                    result = default;
            }
            else if (expectedType == typeof(bool))
            {
                result = new[] { "true", "yes", "on", "1" }.Contains(value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                returnValue = true;
            }
            else if (expectedType == typeof(byte))
            {
                if (returnValue = byte.TryParse(value, out byte b))
                    result = b;
                else
                    result = default;
            }
            else if (expectedType == typeof(DateTime))
            {
                if (returnValue = DateTime.TryParse(value, out DateTime i))
                    result = i;
                else
                    result = default;
            }
            else if (expectedType == typeof(TimeSpan))
            {
                if (returnValue = TimeSpan.TryParse(value, out TimeSpan ts))
                    result = ts;
                else
                    result = default;
            }
            else if (expectedType == typeof(int[]))
            {
                result = value?.Split(',').Select(content => int.Parse(content.Trim(), CultureInfo.InvariantCulture)).ToArray();
                returnValue = true;
            }
            else if (expectedType == typeof(string[]))
            {
                result = value?.Split(',').Select(content => content.Trim()).ToArray();
                returnValue = true;
            }
            else if (expectedType == typeof(string))
            {
                result = value;
                returnValue = true;
            }
            return returnValue;
        }

        protected static object InitializeEnumArrayDefaults(Type expectedType, object values)
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentNullException.ThrowIfNull(expectedType);

            dynamic enumArray = Activator.CreateInstance(expectedType);
            Type[] genericArguments = expectedType.GenericTypeArguments;
            Debug.Assert(genericArguments.Length == 2 && genericArguments[1].IsEnum);
            Debug.Assert(AllowedTypes.Contains(genericArguments[0]));
            Type enumType = genericArguments[1];
            Type valueType = values.GetType();
            Debug.Assert(valueType.IsArray && valueType.GetElementType() == typeof(string));
            foreach (string value in (Array)values)
            {
                string[] enumValues = value.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (enumValues.Length == 2)
                {
                    dynamic enumIndex = Enum.Parse(enumType, enumValues[0]);
                    if (TryParseValues(genericArguments[0], enumValues[1], out dynamic result))
                        enumArray[enumIndex] = result;
                }
            }
            return enumArray;
        }

    }
}
