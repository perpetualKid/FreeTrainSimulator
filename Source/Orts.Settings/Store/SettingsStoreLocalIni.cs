using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

using FreeTrainSimulator.Common;

using Orts.Common;
using Orts.Common.Native;

namespace Orts.Settings.Store
{
    /// <summary>
    /// INI file implementation of <see cref="SettingsStore"/>.
    /// </summary>
    public sealed class SettingsStoreLocalIni : SettingsStore
    {
        internal SettingsStoreLocalIni(string filePath, string section)
            : base(section)
        {
            Location = filePath;
            StoreType = StoreType.Ini;
        }

        private string GetSectionValues(string section, string name)
        {
            string buffer = new string('\0', 256);
            while (true)
            {
                int length = NativeMethods.GetPrivateProfileString(section, name, null, buffer, buffer.Length, Location);
                if (length < buffer.Length - (name == null ? 2 : 1))    // if multiple values are requested (section names, value names, each one is ended by \0 in addtion the overall string is terminated by \0, hence will be double \0
                {
                    return buffer[..length];
                }
                buffer = new string('\0', buffer.Length * 2);
            }
        }

        private static readonly char[] nullSeparator = new char[] { '\0' };

        /// <summary>
        /// Returns an array of all sections within the store, including the one used by this instance.
        /// </summary>
        /// <returns></returns>
        public override string[] GetSectionNames()
        {
            return GetSectionValues(null, null).Split(nullSeparator, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Return an array of all setting-names that are in the store
        /// </summary>
        public override string[] GetSettingNames()
        {
            return GetSectionValues(Section, null).Split(nullSeparator, StringSplitOptions.RemoveEmptyEntries);
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

            string settingValue = GetSectionValues(Section, name);
            if (string.IsNullOrEmpty(settingValue))
                return defaultValue;

            string[] value = settingValue.Split(':');
            if (value.Length != 2)
            {
                Trace.TraceWarning("Setting {0} contains invalid value {1}.", name, settingValue);
                return defaultValue;
            }

            try
            {
                if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(EnumArray<,>).GetGenericTypeDefinition())
                    return InitializeEnumArray(expectedType, (dynamic)defaultValue, value[1].Split(',').Select(v => Uri.UnescapeDataString(v)).ToArray());
                dynamic userValue = null;
                switch (value[0])
                {
                    case "bool":
                        userValue = bool.Parse(value[1]);
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
                    //case "enum[]":
                    //    userValue = InitializeEnumArray(expectedType, defaultValue, value[1].Split(',').Select(v => Uri.UnescapeDataString(v)).ToArray());
                    //    break;
                    default:
                        Trace.TraceWarning("Setting {0} contains invalid type {1}.", name, value[0]);
                        break;
                }

                if (expectedType.IsEnum)
                {
                    return Enum.Parse(expectedType, userValue, true);
                }
                else
                    // Convert whatever we're left with into the expected type.
                    return Convert.ChangeType(userValue, expectedType, CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException)
            {
                Trace.TraceWarning("Setting {0} contains invalid value {1}.", name, value[1]);
                return defaultValue;
            }
        }

        protected override void SetSettingValue(string name, bool value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "bool:" + value.ToString(), Location);
        }

        protected override void SetSettingValue(string name, int value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "int:" + Uri.EscapeDataString(value.ToString(CultureInfo.InvariantCulture)), Location);
        }

        protected override void SetSettingValue(string name, byte value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "byte:" + Uri.EscapeDataString(value.ToString(CultureInfo.InvariantCulture)), Location);
        }

        protected override void SetSettingValue(string name, DateTime value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "DateTime:" + Uri.EscapeDataString(value.ToBinary().ToString(CultureInfo.InvariantCulture)), Location);
        }

        protected override void SetSettingValue(string name, TimeSpan value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "TimeSpan:" + Uri.EscapeDataString(value.TotalSeconds.ToString(CultureInfo.InvariantCulture)), Location);
        }

        protected override void SetSettingValue(string name, string value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "string:" + Uri.EscapeDataString(value), Location);
        }

        protected override void SetSettingValue(string name, int[] value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "int[]:" + string.Join(",", value.Select(v => Uri.EscapeDataString(v.ToString(CultureInfo.InvariantCulture))).ToArray()), Location);
        }

        protected override void SetSettingValue(string name, string[] value)
        {
            NativeMethods.WritePrivateProfileString(Section, name, "string[]:" + string.Join(",", value.Select(v => Uri.EscapeDataString(v)).ToArray()), Location);
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
                        builder.Append(CultureInfo.InvariantCulture, $"{item}=");
                        foreach (dynamic arrayItem in (Array)(dynamic)value[item])
                        {
                            builder.Append(CultureInfo.InvariantCulture, $"{arrayItem},");
                        }
                        if (builder[^1] == ',')
                            builder.Length--;
                        builder.AppendLine();
                    }
                    else
                        builder.AppendLine(CultureInfo.InvariantCulture, $"{item}={value[item]?.ToString()}");
                }
            }
            if (builder.Length > 0)
            {
                NativeMethods.WritePrivateProfileString(Section, name, "enum[]:" + string.Join(",", builder.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select(v => Uri.EscapeDataString(v)).ToArray()), Location);
            }
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
            NativeMethods.WritePrivateProfileString(Section, name, null, Location);
        }
    }
}
