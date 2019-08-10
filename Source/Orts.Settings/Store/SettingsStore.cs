using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Orts.Settings.Store
{
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
        /// Return an array of all setting-names that are in the store
        /// </summary>
        public abstract string[] GetUserNames();

        /// <summary>
        /// Get the value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="expectedType">Type that is expected</param>
        /// <returns>the value from the store, as a general object</returns>
        public abstract object GetUserValue(string name, Type expectedType);

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
        /// <param name="filePath">File patht o a .init file, if you want to use a .ini file</param>
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
    }
}
