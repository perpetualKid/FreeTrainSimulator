using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Orts.Common.Native;

namespace Orts.Settings.Store
{
    /// <summary>
    /// INI file implementation of <see cref="SettingsStore"/>.
    /// </summary>
    public sealed class SettingsStoreLocalIni : SettingsStore
    {
        private const string defaultSection = "ORTS";

        private readonly string filePath;

        internal SettingsStoreLocalIni(string filePath, string section)
            : base(string.IsNullOrEmpty(section) ? defaultSection : section)
        {
            this.filePath = filePath;
        }

        /// <summary>
        /// Returns an array of all sections within the store, including the one used by this instance.
        /// </summary>
        /// <returns></returns>
        public string[] GetSectionNames()
        {
            StringBuilder builder = new StringBuilder(255);
            while (true)
            {
                var length = NativeMethods.GetPrivateProfileString(null, null, null, builder, builder.Length, filePath);
                if (length < builder.Length - 2)
                {
                    builder.Length = length;
                    break;
                }
                builder = new StringBuilder(builder.Length * 2);
            }
            if (builder.Length == 0)
                return null;

            return builder.ToString().Split('\0');
        }

        /// <summary>
        /// Return an array of all setting-names that are in the store
        /// </summary>
        public override string[] GetUserNames()
        {
            var buffer = new string('\0', 256);
            while (true)
            {
                var length = NativeMethods.GetPrivateProfileSection(Section, buffer, buffer.Length, filePath);
                if (length < buffer.Length - 2)
                {
                    buffer = buffer.Substring(0, length);
                    break;
                }
                buffer = new string('\0', buffer.Length * 2);
            }
            return buffer.Split('\0').Where(s => s.Contains('=')).Select(s => s.Split('=')[0]).ToArray();
        }

        /// <summary>
        /// Get the value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="expectedType">Type that is expected</param>
        /// <returns>the value from the store, as a general object</returns>
        public override object GetUserValue(string name, Type expectedType)
        {
            AssertGetUserValueType(expectedType);

            var buffer = new string('\0', 256);
            while (true)
            {
                var length = NativeMethods.GetPrivateProfileString(Section, name, null, buffer, buffer.Length, filePath);
                if (length < buffer.Length - 1)
                {
                    buffer = buffer.Substring(0, length);
                    break;
                }
                buffer = new string('\0', buffer.Length * 2);
            }
            if (buffer.Length == 0)
                return null;

            var value = buffer.Split(':');
            if (value.Length != 2)
            {
                Trace.TraceWarning("Setting {0} contains invalid value {1}.", name, buffer);
                return null;
            }

            try
            {
                object userValue = null;
                switch (value[0])
                {
                    case "bool":
                        userValue = value[1].Equals("true", StringComparison.InvariantCultureIgnoreCase);
                        break;
                    case "int":
                        userValue = int.Parse(Uri.UnescapeDataString(value[1]), CultureInfo.InvariantCulture);
                        break;
                    case "byte":
                        userValue = byte.Parse(Uri.UnescapeDataString(value[1]), CultureInfo.InvariantCulture);
                        break;
                    case "DateTime":
                        userValue = DateTime.FromBinary(long.Parse(Uri.UnescapeDataString(value[1]), CultureInfo.InvariantCulture));
                        break;
                    case "TimeSpan":
                        userValue = TimeSpan.FromSeconds(long.Parse(Uri.UnescapeDataString(value[1]), CultureInfo.InvariantCulture));
                        break;
                    case "string":
                        userValue = Uri.UnescapeDataString(value[1]);
                        break;
                    case "int[]":
                        userValue = value[1].Split(',').Select(v => int.Parse(Uri.UnescapeDataString(v), CultureInfo.InvariantCulture)).ToArray();
                        break;
                    case "string[]":
                        userValue = value[1].Split(',').Select(v => Uri.UnescapeDataString(v)).ToArray();
                        break;
                    default:
                        Trace.TraceWarning("Setting {0} contains invalid type {1}.", name, value[0]);
                        break;
                }

                // Convert whatever we're left with into the expected type.
                return Convert.ChangeType(userValue, expectedType);
            }
            catch
            {
                Trace.TraceWarning("Setting {0} contains invalid value {1}.", name, value[1]);
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
            NativeMethods.WritePrivateProfileString(Section, name, "bool:" + (value ? "true" : "false"), filePath);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, int value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "int:" + Uri.EscapeDataString(value.ToString(CultureInfo.InvariantCulture)), filePath);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, byte value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "byte:" + Uri.EscapeDataString(value.ToString(CultureInfo.InvariantCulture)), filePath);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, DateTime value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "DateTime:" + Uri.EscapeDataString(value.ToBinary().ToString(CultureInfo.InvariantCulture)), filePath);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, TimeSpan value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "TimeSpan:" + Uri.EscapeDataString(value.TotalSeconds.ToString(CultureInfo.InvariantCulture)), filePath);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, string value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "string:" + Uri.EscapeDataString(value), filePath);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, int[] value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "int[]:" + string.Join(",", ((int[])value).Select(v => Uri.EscapeDataString(v.ToString(CultureInfo.InvariantCulture))).ToArray()), filePath);
        }

        /// <summary>
        /// Set a value of a user setting
        /// </summary>
        /// <param name="name">name of the setting</param>
        /// <param name="value">value of the setting</param>
        public override void SetUserValue(string name, string[] value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "string[]:" + string.Join(",", value.Select(v => Uri.EscapeDataString(v)).ToArray()), filePath);
        }

        /// <summary>
        /// Remove a user setting from the store
        /// </summary>
        /// <param name="name">name of the setting</param>
        public override void DeleteUserValue(string name)
        {
            NativeMethods.WritePrivateProfileString(Section, name, null, filePath);
        }
    }
}
