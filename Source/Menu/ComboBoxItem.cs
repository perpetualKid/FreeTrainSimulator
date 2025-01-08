using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using FreeTrainSimulator.Common;

namespace FreeTrainSimulator.Menu
{
    internal sealed class ComboBoxItem<T>
    {
        public T Value { get; }
        public string Text { get; }

        public ComboBoxItem(string text, T value)
        {
            Value = value;
            Text = text;
        }

        private ComboBoxItem() { }
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
            ArgumentNullException.ThrowIfNull(comboBox, nameof(comboBox));

            comboBox.EnableComboBoxItemDataSource(FromList(source, lookup));
        }

        /// <summary>
        /// Populates combobox items from an enum.
        /// Display members are the enum value description attributes
        /// Value Members are the enum value
        /// </summary>
        public static void DataSourceFromEnum<T>(this ComboBox comboBox) where T : Enum
        {
            ArgumentNullException.ThrowIfNull(comboBox, nameof(comboBox));

            comboBox.EnableComboBoxItemDataSource(FromEnum<T>());
        }

        /// <summary>
        /// Populates combobox items from an enum.
        /// Display members are the enum value description attributes
        /// Value Members are the int values of the enums
        /// </summary>
        public static void DataSourceFromEnumIndex<T>(this ComboBox comboBox) where T : Enum
        {
            ArgumentNullException.ThrowIfNull(comboBox, nameof(comboBox));

            comboBox.EnableComboBoxItemDataSource(FromEnumValue<T>());
        }

        private static List<ComboBoxItem<T>> FromEnum<T>() where T : Enum
        {
            return EnumExtension.GetValues<T>().Select(data => new ComboBoxItem<T>(data.GetLocalizedDescription(), data)).ToList();
        }

        private static List<ComboBoxItem<int>> FromEnumValue<T>() where T : Enum
        {
            return EnumExtension.GetValues<T>().Select(data => new ComboBoxItem<int>(data.GetLocalizedDescription(), Convert.ToInt32(data, System.Globalization.CultureInfo.InvariantCulture))).ToList();
        }

        /// <summary>
        /// Returns a new List<ComboBoxItem<T>> created from source enum.
        /// Text and values are mapped from enum values, typically text are enum values or enum value names
        /// </summary>
        private static List<ComboBoxItem<T>> FromEnumCustomLookup<E, T>(Func<E, T> valueLookup2, Func<E, string> textLookup) where E : Enum
        {
            return EnumExtension.GetValues<E>().Select(data => new ComboBoxItem<T>(textLookup(data), valueLookup2(data))).ToList();
        }

        /// <summary>
        /// Returns a new List<ComboBoxItem<T>> created from source list.
        /// Keys are mapped from list items, display values are mapped through lookup function
        /// </summary>
        private static List<ComboBoxItem<T>> FromList<T>(IEnumerable<T> source, Func<T, string> textLookup)
        {
            return source.Select(item => new ComboBoxItem<T>(textLookup(item), item)).ToList();
        }

        internal static void EnableComboBoxItemDataSource<T>(this ComboBox comboBox, IEnumerable<ComboBoxItem<T>> datasource)
        {
            ArgumentNullException.ThrowIfNull(comboBox, nameof(comboBox));

            comboBox.DataSource = datasource?.ToList();

            comboBox.DisplayMember = nameof(ComboBoxItem<T>.Text);
            comboBox.ValueMember = nameof(ComboBoxItem<T>.Value);
        }

        private delegate T SetGetComboBoxItemDelegate<T>(ComboBox comboBox, Func<T, bool> predicate);

        public static T SetComboBoxItem<T>(this ComboBox comboBox, Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(comboBox, nameof(comboBox));
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));

            if (comboBox.InvokeRequired)
            {
                return (comboBox.Invoke(new SetGetComboBoxItemDelegate<T>(SetComboBoxItem), comboBox, predicate)) is T result ? result : default;
            }
            if (comboBox.Items.Count == 0)
                return default;

            bool found = false;
            if (comboBox.Items[0] is ComboBoxItem<T>)
            {
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    if (comboBox.Items[i] is ComboBoxItem<T> cbi && predicate(cbi.Value))
                    {
                        comboBox.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    comboBox.SelectedIndex = 0;
                return comboBox.SelectedValue is T result ? result : default;
            }
            else
            {
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    if (comboBox.Items[i] is T t && predicate(t))
                    {
                        comboBox.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    comboBox.SelectedIndex = 0;
                return comboBox.SelectedValue is T result ? result : default;
            }
        }
    }
}
