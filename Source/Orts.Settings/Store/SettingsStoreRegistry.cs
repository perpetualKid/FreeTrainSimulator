using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

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
            registryKey = string.IsNullOrEmpty(section) ? registryKey : registryKey + @"\" + section;
            key = Registry.CurrentUser.CreateSubKey(registryKey);
        }

        public override string[] GetSectionNames()
        {
            throw new NotImplementedException();
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
        public override object GetSettingValue(string name, Type expectedType)
        {
            AssertGetUserValueType(expectedType);

            var userValue = key.GetValue(name);
            if (userValue == null)
                return userValue;

            try
            {
                // Expected bool-stored-as-int conversion.
                if (expectedType == typeof(bool) && (userValue is int))
                    return (int)userValue == 1;

                // Expected DateTime-stored-as-long conversion.
                if (expectedType == typeof(DateTime) && (userValue is long))
                    return DateTime.FromBinary((long)userValue);

                // Expected TimeSpan-stored-as-long conversion.
                if (expectedType == typeof(TimeSpan) && (userValue is long))
                    return TimeSpan.FromSeconds((long)userValue);

                // Expected int[]-stored-as-string conversion.
                if (expectedType == typeof(int[]) && (userValue is string))
                    return ((string)userValue).Split(',').Select(s => int.Parse(s)).ToArray();

                // Convert whatever we're left with into the expected type.
                return Convert.ChangeType(userValue, expectedType);
            }
            catch
            {
                Trace.TraceWarning("Setting {0} contains invalid value {1}.", name, userValue);
                return null;
            }
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, bool value)
        {
            key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, int value)
        {
            key.SetValue(name, value, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, byte value)
        {
            key.SetValue(name, value, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, DateTime value)
        {
            key.SetValue(name, value.ToBinary(), RegistryValueKind.QWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, TimeSpan value)
        {
            key.SetValue(name, value.TotalSeconds, RegistryValueKind.QWord);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, string value)
        {
            key.SetValue(name, value, RegistryValueKind.String);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, int[] value)
        {
            key.SetValue(name, string.Join(",", (value).Select(v => v.ToString()).ToArray()), RegistryValueKind.String);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, string[] value)
        {
            key.SetValue(name, value, RegistryValueKind.MultiString);
        }

        /// <summary>
        /// Remove a user setting from the store
        /// </summary>
        /// <param name="name">name of the setting</param>
        public override void DeleteUserValue(string name)
        {
            key.DeleteValue(name, false);
        }
    }

}
