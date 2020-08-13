using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Orts.Common;

namespace Orts.Menu
{
    internal class ComboBoxItem<T>
    {
        public T Key { get; private set; }
        public string Value { get; private set; }

        public ComboBoxItem(T key, string value)
        {
            Key = key;
            Value = value;
        }

        private ComboBoxItem() { }

        /// <summary>
        /// Returns a new IList<ComboBoxItem<T>> created from source list.
        /// Keys are mapped from list items, display values are mapped through lookup function
        /// </summary>
        public static IList<ComboBoxItem<T>> FromList(IEnumerable<T> source, Func<T, string> lookup)
        {
            try
            {
                return (from item in source
                        select new ComboBoxItem<T>()
                        {
                            Key = item,
                            Value = lookup(item),
                        }).ToList();
            }
            catch (ArgumentException)
            {
                return new List<ComboBoxItem<T>>();
            }
        }

        /// <summary>
        /// Returns a new IList<ComboBoxItem<T>> created from source enum.
        /// Keys and values are mapped from enum values, typically keys are enum values or enum value names
        /// </summary>
        public static IList<ComboBoxItem<T>> FromEnum<E>(Func<E, T> keyLookup, Func<E, string> valueLookup) where E : Enum
        {
            return (from data in EnumExtension.GetValues<E>()
                    select new ComboBoxItem<T>()
                    {
                        Key = keyLookup(data),
                        Value = valueLookup(data)
                    }).ToList();
        }

        /// <summary>
        /// Prepares the combobox which property names to use for Key and Value display
        /// </summary>
        public static void SetDataSourceMembers(ComboBox comboBox)
        {
            comboBox.DisplayMember = nameof(Value);
            comboBox.ValueMember = nameof(Key);

        }
    }
}
