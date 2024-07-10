using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;

namespace Orts.Toolbox.WinForms.Controls
{
    internal sealed class ComboBoxItem<T>
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
        private static readonly System.Drawing.SolidBrush backgroundBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Transparent);
        private static readonly System.Drawing.SolidBrush fontBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Transparent);

        private static readonly object[] sortedColors = ColorExtension.ColorCodes.OrderByDescending(kvp => (kvp.Value.R << 8) + (kvp.Value.G << 8) + kvp.Value.B).Select(c => c.Key).Cast<object>().ToArray();

        internal static void DisplayXnaColors(this ToolStripComboBox comboBox, string defaultColor, ColorSetting setting)
        {
            // Make the ComboBox owner-drawn.
            comboBox.ComboBox.DrawMode = DrawMode.OwnerDrawVariable;

            comboBox.Items.Clear();
            comboBox.Items.AddRange(sortedColors);
            // Subscribe to the DrawItem event.
            //cbo.MeasureItem += cboDrawImageAndText_MeasureItem;
            comboBox.ComboBox.DrawItem += ComboBox_DrawItem;
            comboBox.SelectedItem = defaultColor;
            comboBox.Tag = setting;
        }

        private static void ComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index > -1)
            {
                string colorName = (sender as ComboBox).Items[e.Index].ToString();
                Color color = ColorExtension.FromName(colorName);
                backgroundBrush.Color = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
                e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
                fontBrush.Color = color.ToComplementSystemDrawingColor();
                e.Graphics.DrawString(colorName, e.Font, fontBrush, e.Bounds, System.Drawing.StringFormat.GenericDefault);
                // If the ListBox has focus, draw a focus rectangle around the selected item.
                e.DrawFocusRectangle();
            }
        }

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

        private static List<ComboBoxItem<int>> FromEnumValue<E>() where E : Enum
        {
            return EnumExtension.GetValues<E>().Select(data => new ComboBoxItem<int>(Convert.ToInt32(data, System.Globalization.CultureInfo.InvariantCulture), data.GetLocalizedDescription())).ToList();
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
                return (from item in source
                        select new ComboBoxItem<T>(item, lookup(item))).ToList();
            }
            catch (ArgumentException)
            {
                return [];
            }
        }

    }

}
