using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using GetText;

using Orts.Common;

namespace Orts.Menu
{
    internal class ComboBoxItem<T>
    {
        public T Key { get; }
        public string Value { get; }

        public ComboBoxItem(T key, string value)
        {
            Key = key;
            Value = value;
        }

        public static void SetDataSourceMembers(ComboBox comboBox)
        {
            comboBox.DisplayMember = nameof(ComboBoxItem<int>.Value);
            comboBox.ValueMember = nameof(ComboBoxItem<int>.Key);
        }

        internal ComboBoxItem() { }
    }

    public static class ComboBoxExtension
    {
        /// <summary>
        /// Populates combobox items from an enum.
        /// Display members are the enum value description attributes
        /// Value Members are the enum value
        /// </summary>
        public static void DataSourceFromList<T>(this ComboBox comboBox, IEnumerable<T> source, Func<T, string> lookup)
        {
            ArgumentNullException.ThrowIfNull(comboBox);

            comboBox.DataSource = FromList(source, lookup);
            ComboBoxItem<T>.SetDataSourceMembers(comboBox);
        }

        /// <summary>
        /// Populates combobox items from an enum.
        /// Display members are the enum value description attributes
        /// Value Members are the enum value
        /// </summary>
        public static void DataSourceFromEnum<T>(this ComboBox comboBox) where T : Enum
        {
            ArgumentNullException.ThrowIfNull(comboBox);

            comboBox.DataSource = FromEnum<T>();
            ComboBoxItem<T>.SetDataSourceMembers(comboBox);
        }

        /// <summary>
        /// Populates combobox items from an enum.
        /// Display members are the enum value description attributes
        /// Value Members are the int values of the enums
        /// </summary>
        public static void DataSourceFromEnumIndex<T>(this ComboBox comboBox) where T : Enum
        {
            ArgumentNullException.ThrowIfNull(comboBox);

            comboBox.DataSource = FromEnumValue<T>();
            ComboBoxItem<T>.SetDataSourceMembers(comboBox);

        }

        private static List<ComboBoxItem<T>> FromEnum<T>() where T : Enum
        {
            return EnumExtension.GetValues<T>().Select(data => new ComboBoxItem<T>(data, data.GetLocalizedDescription())).ToList();
        }

        private static List<ComboBoxItem<int>> FromEnumValue<T>() where T : Enum
        {
            return EnumExtension.GetValues<T>().Select(data => new ComboBoxItem<int>(Convert.ToInt32(data, System.Globalization.CultureInfo.InvariantCulture),data.GetLocalizedDescription())).ToList();
        }

        /// <summary>
        /// Returns a new List<ComboBoxItem<T>> created from source enum.
        /// Keys and values are mapped from enum values, typically keys are enum values or enum value names
        /// </summary>
        private static List<ComboBoxItem<T>> FromEnumCustomLookup<E, T>(Func<E, T> keyLookup, Func<E, string> valueLookup) where E : Enum
        {
            return EnumExtension.GetValues<E>().Select(data => new ComboBoxItem<T>(keyLookup(data), valueLookup(data))).ToList();
        }

        /// <summary>
        /// Returns a new List<ComboBoxItem<T>> created from source list.
        /// Keys are mapped from list items, display values are mapped through lookup function
        /// </summary>
        private static List<ComboBoxItem<T>> FromList<T>(IEnumerable<T> source, Func<T, string> lookup)
        {
            try
            {
                return source.Select(item => new ComboBoxItem<T>(item, lookup(item))).ToList();
            }
            catch (ArgumentException)
            {
                return new List<ComboBoxItem<T>>();
            }
        }

    }
}
