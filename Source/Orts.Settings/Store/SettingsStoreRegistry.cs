using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Win32;

using Orts.Common;

namespace Orts.Settings.Store
{
    /// <summary>
    /// Registry implementation of <see cref="SettingsStore"/>.
    /// </summary>
    public sealed class SettingsStoreRegistry : SettingsStore
    {
        private readonly RegistryKey key;

        internal SettingsStoreRegistry(string registryKey, string section)
            : base(section)
        {
            Location = Path.Combine(registryKey, Section);
            key = Registry.CurrentUser.CreateSubKey(Location);
            StoreType = StoreType.Registry;
        }

        public override string[] GetSectionNames()
        {
            List<string> sections = new List<string>(key.GetSubKeyNames());
            sections.Insert(0, Path.GetFileNameWithoutExtension(key.Name));
            return sections.ToArray();
        }

        /// <summary>
        /// Return an array of all setting-names that are in the store
        /// </summary>
        public override string[] GetSettingNames()
        {
            return key.GetValueNames();
        }

        /// <summary>
        /// Get the value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="expectedType">Type that is expected</param>
        /// <returns>the value from the store, as a general object</returns>
        protected override object GetSettingValue<T>(string name, Type expectedType, T defaultValue)
        {
            AssertGetUserValueType(expectedType);

            object userValue = key.GetValue(name);
            if (userValue == null)
                return defaultValue;

            try
            {
                // Expected bool-stored-as-int conversion.
                if (expectedType == typeof(bool) && (userValue is int boolValue))
                    return boolValue == 1;

                // Expected DateTime-stored-as-long conversion.
                if (expectedType == typeof(DateTime) && (userValue is long dateTimeValue))
                    return DateTime.FromBinary(dateTimeValue);

                // Expected TimeSpan-stored-as-long conversion.
                if (expectedType == typeof(TimeSpan) && (userValue is long timeSpanValue))
                    return TimeSpan.FromSeconds(timeSpanValue);

                // Expected int[]-stored-as-string conversion.
                if (expectedType == typeof(int[]) && (userValue is string intValues))
                    return intValues.Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();

                if (expectedType.IsEnum && userValue is string enumValue)
                    return Enum.Parse(expectedType, enumValue, true);

                if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(EnumArray<,>).GetGenericTypeDefinition() 
                    && userValue is string[] enumValues)
                    return InitializeEnumArray(expectedType, (dynamic)defaultValue, enumValues);
                // Convert whatever we're left with into the expected type.
                return Convert.ChangeType(userValue, expectedType, CultureInfo.InvariantCulture);
            }
            catch(InvalidCastException)
            {
                Trace.TraceWarning("Setting {0} contains invalid value {1}.", name, userValue);
                return defaultValue;
            }
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        protected override void SetSettingValue(string name, bool value)
        {
            key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        protected override void SetSettingValue(string name, int value)
        {
            key.SetValue(name, value, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        protected override void SetSettingValue(string name, byte value)
        {
            key.SetValue(name, value, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        protected override void SetSettingValue(string name, DateTime value)
        {
            key.SetValue(name, value.ToBinary(), RegistryValueKind.QWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        protected override void SetSettingValue(string name, TimeSpan value)
        {
            key.SetValue(name, value.TotalSeconds, RegistryValueKind.QWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        protected override void SetSettingValue(string name, string value)
        {
            key.SetValue(name, value, RegistryValueKind.String);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        protected override void SetSettingValue(string name, int[] value)
        {
            key.SetValue(name, string.Join(",", value.Select(v => v.ToString(CultureInfo.InvariantCulture))), RegistryValueKind.String);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        protected override void SetSettingValue(string name, string[] value)
        {
            key.SetValue(name, value, RegistryValueKind.MultiString);
        }

        protected override void SetSettingValue<T, TEnum>(string name, EnumArray<T, TEnum> value)
        {
            StringBuilder builder = new StringBuilder();
            foreach (TEnum item in EnumExtension.GetValues<TEnum>())
            {
                if ((dynamic)value[item] != default(T) || typeof(T).IsValueType)
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
                        builder.AppendLine();
                    }
                    else
                        builder.AppendLine($"{item}={value[item]?.ToString()}");
                }
            }
            if (builder.Length > 0)
                key.SetValue(name, builder.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToArray(), RegistryValueKind.MultiString);
            else
            {
                DeleteSetting(name);
            }
        }

        /// <summary>
        /// Remove a user setting from the store
        /// </summary>
        /// <param name="name">name of the setting</param>
        public override void DeleteSetting(string name)
        {
            key.DeleteValue(name, false);
        }
    }

}
