using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

using Orts.Common;

namespace Orts.Settings.Store
{
    public enum StoreType
    {
        [Description("OpenRails.ini")]
        Ini,
        [Description("OpenRails.json")]
        Json,
        [Description("SOFTWARE\\OpenRails")]
        Registry
    }

    /// <summary>
    /// Base class for all means of persisting settings from the user/game.
    /// </summary>
    public abstract class SettingsStore
    {
        protected const string sectionRoot = "ORTS";
        /// <summary>Name of a 'section', to distinguish various part within a underlying store</summary>
        protected string Section { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="section">Name of the 'section', to distinguish various part within a underlying store</param>
		protected SettingsStore(string section)
        {
            Section = section ?? sectionRoot;
        }

        public StoreType StoreType { get; protected set; }

        public string Location { get; protected set; }

        /// <summary>
        /// returns an array of all Section names that are in the store.
        /// For flat file store (ini), this would be all sections
        /// For hierarchical store (registry, json), this would be the root and all (first level) child
        /// </summary>
        public abstract string[] GetSectionNames();

        /// <summary>
        /// Return an array of all setting-names that are in the store
        /// </summary>
        public abstract string[] GetSettingNames();

        /// <summary>
        /// Get the value of a setting
        /// </summary>
        public T GetSettingValue<T>(string name, T defaultValue)
        {
            return (T)GetSettingValue(name, typeof(T), defaultValue) ?? defaultValue;
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        public void SetSettingValue<T>(string name, T value)
        {
            AssertGetUserValueType(typeof(T));
            if (typeof(T).IsEnum)
                SetSettingValue(name, value.ToString());
            else
                SetSettingValue(name, (dynamic)value);
        }

        /// <summary>
        /// Delete a specific user setting
        /// </summary>
        public abstract void DeleteSetting(string name);

        /// <summary>
        /// Factory method to create a setting store (sub-class of SettingsStore)
        /// </summary>
        public static SettingsStore GetSettingsStore(StoreType storeType, string location, string section)
        {
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("Argument need to point to a valid location (registry or file path)", nameof(location));
            switch (storeType)
            {
                case StoreType.Ini:
                    return new SettingsStoreLocalIni(location, section);
                case StoreType.Json:
                    break;
                case StoreType.Registry:
                    return new SettingsStoreRegistry(location, section);
            }
            throw new InvalidOperationException("Invalid setting store arguments");
        }

        #region proctected /implementation
        protected abstract object GetSettingValue<T>(string name, Type expectedType, T defaultValue);

        protected abstract void SetSettingValue(string name, bool value);

        protected abstract void SetSettingValue(string name, int value);

        protected abstract void SetSettingValue(string name, byte value);

        protected abstract void SetSettingValue(string name, DateTime value);

        protected abstract void SetSettingValue(string name, TimeSpan value);

        protected abstract void SetSettingValue(string name, string value);

        protected abstract void SetSettingValue(string name, int[] value);

        protected abstract void SetSettingValue(string name, string[] value);

        protected abstract void SetSettingValue<T, TEnum>(string name, EnumArray<T, TEnum> value) where TEnum : Enum;

        /// <summary>
        /// Assert that the type expected from the settings store is an allowed type.
        /// </summary>
        /// <param name="expectedType">Type that is expected</param>
        internal static void AssertGetUserValueType(Type expectedType)
        {
            Debug.Assert(SettingsBase.AllowedTypes.Contains(expectedType) || expectedType.IsEnum || 
                (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(EnumArray<,>).GetGenericTypeDefinition() &&
                SettingsBase.AllowedTypes.Contains(expectedType.GenericTypeArguments[0]))
                , $"GetUserValue called with unexpected type {expectedType.FullName}.");
        }

        protected static EnumArray<T, TEnum> InitializeEnumArray<T, TEnum>(Type expectedType, EnumArray<T, TEnum> defaultValues, string[] values) where TEnum : struct, Enum
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentNullException.ThrowIfNull(defaultValues);
            ArgumentNullException.ThrowIfNull(expectedType);

            Type[] genericArguments = expectedType.GenericTypeArguments;
            Debug.Assert(genericArguments.Length == 2 && genericArguments[1].IsEnum);
            foreach (string value in values)
            {
                string[] enumValues = value.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (enumValues.Length == 2)
                {
                    if (Enum.TryParse(enumValues[0], out TEnum enumIndex) &&  SettingsBase.TryParseValues(genericArguments[0], enumValues[1], out dynamic result))
                        defaultValues[enumIndex] = result;
                }
            }
            return defaultValues;
        }


        #endregion
    }
}
