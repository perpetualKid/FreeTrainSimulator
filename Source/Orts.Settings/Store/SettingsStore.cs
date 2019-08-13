using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Orts.Settings.Store
{
    public enum StoreType
    {
        Ini,
        Json,
        Registry
    }

    /// <summary>
    /// Base class for all means of persisting settings from the user/game.
    /// </summary>
    public abstract class SettingsStore
    {
        /// <summary>Name of a 'section', to distinguish various part within a underlying store</summary>
        protected string Section { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="section">Name of the 'section', to distinguish various part within a underlying store</param>
		protected SettingsStore(string section)
        {
            Section = section;
        }

        /// <summary>
        /// Assert that the type expected from the settings store is an allowed type.
        /// </summary>
        /// <param name="expectedType">Type that is expected</param>
        protected static void AssertGetUserValueType(Type expectedType)
        {
            Debug.Assert(new[] {
                typeof(bool),
                typeof(int),
                typeof(DateTime),
                typeof(TimeSpan),
                typeof(string),
                typeof(int[]),
                typeof(string[]),
                typeof(byte),
            }.Contains(expectedType), string.Format("GetUserValue called with unexpected type {0}.", expectedType.FullName));
        }

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
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public T GetSettingValue<T>(string name, T defaultValue)
        {
            dynamic result = GetSettingValue(name, typeof(T));
            return result ?? defaultValue;
        }

        /// <summary>
        /// Get the value of a setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="expectedType">Type that is expected</param>
        /// <returns>the value from the store, as a general object</returns>
        public abstract object GetSettingValue(string name, Type expectedType);

        public void SetSettingValue<T>(string name, T value)
        {
            AssertGetUserValueType(typeof(T));
            SetUserValue(name, (dynamic)value);
        }

        /// <summary>
        /// Set a boolean user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public abstract void SetUserValue(string name, bool value);

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public abstract void SetUserValue(string name, int value);

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public abstract void SetUserValue(string name, byte value);

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public abstract void SetUserValue(string name, DateTime value);

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public abstract void SetUserValue(string name, TimeSpan value);

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public abstract void SetUserValue(string name, string value);

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public abstract void SetUserValue(string name, int[] value);

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public abstract void SetUserValue(string name, string[] value);

        /// <summary>
        /// Remove a user setting from the store
        /// </summary>
        /// <param name="name">name of the setting</param>
		public abstract void DeleteUserValue(string name);

        /// <summary>
        /// Factory method to create a setting store (sub-class of SettingsStore)
        /// </summary>
        /// <param name="filePath">File path to an .init file, if you want to use a .ini file</param>
        /// <param name="registryKey">key to the 'windows' register, if you want to use a registry-based store</param>
        /// <param name="section">Name to distinguish between various 'section's used in underlying store.</param>
        /// <returns>The created SettingsStore</returns>
		public static SettingsStore GetSettingStore(string filePath, string registryKey, string section)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                return new SettingsStoreLocalIni(filePath, section);
            if (!string.IsNullOrEmpty(registryKey))
                return new SettingsStoreRegistry(registryKey, section);
            throw new ArgumentException("Neither 'filePath' nor 'registryKey' arguments are valid.");
        }

        public static SettingsStore GetSettingsStore(StoreType storeType, string location, string section)
        {
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("Argument need to point to a valid location (registry or file path)", nameof(location));
            SettingsStore result = null;
            switch (storeType)
            {
                case StoreType.Ini:
                    result = new SettingsStoreLocalIni(location, section);
                    break;
                case StoreType.Json:
                    break;
                case StoreType.Registry:
                    new SettingsStoreRegistry(location, section);
                    break;
            }
            if (null == result)
                throw new InvalidOperationException("Invalid setting store arguments");
            return result;
        }
    }
}
