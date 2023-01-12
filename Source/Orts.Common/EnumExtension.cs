using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using GetText;

namespace Orts.Common
{
    public static class EnumExtension
    {
        private static class EnumCache<T> where T : Enum
        {
            internal static readonly ReadOnlyCollection<string> Names;
            internal static readonly ReadOnlyCollection<T> Values;
            internal static readonly Dictionary<T, string> ValueToDescriptionMap;
            internal static readonly string EnumDescription;
            internal static readonly Dictionary<string, T> NameValuePairs;
            internal static readonly int Length;
            internal static readonly int SupportsReverse;
#pragma warning disable CA1802 // Use literals where appropriate
            internal static readonly bool ConsecutiveValues = true;
#pragma warning restore CA1802 // Use literals where appropriate
            internal static readonly Dictionary<T, int> ValueLookup;
            internal static readonly int Offset;
            internal static Catalog Catalog;

#pragma warning disable CA1810 // Initialize reference type static fields inline
            static EnumCache()
#pragma warning restore CA1810 // Initialize reference type static fields inline
            {
                Values = new ReadOnlyCollection<T>((T[])Enum.GetValues(typeof(T)));
                Names = new ReadOnlyCollection<string>(Enum.GetNames(typeof(T)));
                ValueToDescriptionMap = new Dictionary<T, string>();
                EnumDescription = typeof(T).GetCustomAttributes(typeof(DescriptionAttribute), false).
                    Cast<DescriptionAttribute>().
                    Select(x => x.Description).
                    FirstOrDefault();
                foreach (T value in Values)
                {
                    ValueToDescriptionMap[value] = GetDescription(value);
                }

                NameValuePairs = Names.Zip(Values, (k, v) => new { k, v })
                              .ToDictionary(x => x.k, x => x.v, StringComparer.OrdinalIgnoreCase);

                Length = Values.Count;
                if (typeof(int).IsAssignableFrom(Enum.GetUnderlyingType(typeof(T))))
                {
                    if (Length == 2 && (int)(object)Values[0] == 0 && (int)(object)Values[1] == 1)      //Simple case having two values only (like Forward and Backward, values 0 and 1 only)
                        SupportsReverse = 2;
                    else if (Length % 2 == 1 && (int)(object)Values[Length / 2] == 0) //odd number of items, like Backward, Neutral and Forward
                        SupportsReverse = 1;

                    SortedSet<T> sortedValues = new SortedSet<T>(Values);
                    for (int i = 0; i < sortedValues.Count - 1; i++)
                    {
                        T refItem = sortedValues.ElementAt(i);
                        T nextItem = sortedValues.ElementAt(i + 1);
                        if (Unsafe.As<T, int>(ref refItem) + 1 != Unsafe.As<T, int>(ref nextItem))
                        {
                            ConsecutiveValues = false;
                            break;
                        }
                    }
                    T offsetItem = sortedValues.FirstOrDefault();
                    Offset = Unsafe.As<T, int>(ref offsetItem);

                    ValueLookup = Values.Select((i, index) => (i, index)).ToDictionary(pair => pair.i, pair => pair.index);
                }
            }

            private static string GetDescription(T value)
            {
                FieldInfo field = typeof(T).GetField(value.ToString());
                return field.GetCustomAttributes(typeof(DescriptionAttribute), false)
                            .Cast<DescriptionAttribute>()
                            .Select(x => x.Description)
                            .FirstOrDefault();
            }
        }

        /// <summary>
        /// returns the Description attribute for the particular enum value
        /// </summary>
        public static string GetDescription<T>(this T item) where T : Enum
        {
            return EnumCache<T>.ValueToDescriptionMap.TryGetValue(item, out string description)
                ? description
                : throw new ArgumentOutOfRangeException(nameof(item));
        }

        /// <summary>
        /// returns the Description attribute for the enum type
        /// </summary>
        public static string EnumDescription<T>() where T : Enum
        {
            return EnumCache<T>.EnumDescription;
        }

        /// <summary>
        /// returns the Description attribute for the particular enum value
        /// </summary>
        public static string GetLocalizedDescription<T>(this T item) where T : Enum
        {
            if (EnumCache<T>.Catalog == null)
                EnumCache<T>.Catalog = CatalogManager<T>.Catalog;

            if (EnumCache<T>.ValueToDescriptionMap.TryGetValue(item, out string description))
            {
                string context;
                return string.IsNullOrEmpty(context = EnumCache<T>.EnumDescription)
                    ? EnumCache<T>.Catalog.GetString(description)
                    : EnumCache<T>.Catalog.GetParticularString(context, description);
            }
            throw new ArgumentOutOfRangeException(nameof(item));
        }

        /// <summary>
        /// returns the Description attribute for the enum type
        /// </summary>
        public static string GetLocalizedEnumDescription<T>() where T : Enum
        {
            if (string.IsNullOrEmpty(EnumCache<T>.EnumDescription))
                return null;
            if (EnumCache<T>.Catalog == null)
                EnumCache<T>.Catalog = CatalogManager<T>.Catalog;

            return EnumCache<T>.Catalog.GetString(EnumCache<T>.EnumDescription);
        }

        /// <summary>
        /// returns a static list of all names in this enum
        /// </summary>
        public static IReadOnlyCollection<string> GetNames<T>() where T : Enum
        {
            return EnumCache<T>.Names;
        }

        /// <summary>
        /// returns a static list of all values in this enum
        /// </summary>
        public static IReadOnlyCollection<T> GetValues<T>() where T : Enum
        {
            return EnumCache<T>.Values;
        }

        /// <summary>
        /// returns a number of elements in this enum
        /// </summary>
        public static int GetLength<T>() where T : Enum
        {
            return EnumCache<T>.Length;
        }

        /// <summary>
        /// Similar as Enum.TryParse, but based on statically cached dictionary
        /// </summary>
        public static bool GetValue<T>(string name, out T result) where T : Enum
        {
            if (string.IsNullOrEmpty(name))
            {
                result = default(T);
                return false;
            }
            return EnumCache<T>.NameValuePairs.TryGetValue(name, out result);
        }

        /// <summary>
        /// allows to enumerate forward over enum values
        /// </summary>
        public static T Next<T>(this T item) where T : Enum
        {
            if (EnumCache<T>.ConsecutiveValues)
            {
                int next = ((Unsafe.As<T, int>(ref item) + 1 - EnumCache<T>.Offset) % EnumCache<T>.Length) + EnumCache<T>.Offset;
                return Unsafe.As<int, T>(ref next);
            }
            else
            {
                int next = (EnumCache<T>.ValueLookup[item] + 1) % EnumCache<T>.Length;
                return EnumCache<T>.Values[next];
            }
        }

        /// <summary>
        /// allows to enumerate backward over enum values
        /// </summary>
        public static T Previous<T>(this T item) where T : Enum
        {
            if (EnumCache<T>.ConsecutiveValues)
            {
                int next = ((Unsafe.As<T, int>(ref item) + EnumCache<T>.Length - 1 - EnumCache<T>.Offset) % EnumCache<T>.Length) + EnumCache<T>.Offset;
                return Unsafe.As<int, T>(ref next);
            }
            else
            {
                int next = (EnumCache<T>.ValueLookup[item] + EnumCache<T>.Length - 1) % EnumCache<T>.Length;
                return EnumCache<T>.Values[next];
            }
        }

        /// <summary>
        /// Allows to reverse simple enums, 
        /// i.e. Forward(0)<-->Backward(1)
        /// or Backward(-1)<-->Forward(1) with Neutral (0) not being changed
        /// </summary>
        public static T Reverse<T>(this T item) where T : Enum
        {
            switch (EnumCache<T>.SupportsReverse)
            {
                case 1:
                    int reverse = -Unsafe.As<T, int>(ref item);
                    return Unsafe.As<int, T>(ref reverse);
                case 2:
                    int inverse = Unsafe.As<T, int>(ref item) > 0 ? 0 : 1;
                    return Unsafe.As<int, T>(ref inverse);
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// returns the Minimum enum value
        /// </summary>
        public static T Min<T>() where T : Enum
        {
            return EnumCache<T>.Offset < 0 ? EnumCache<T>.Values.Min() : EnumCache<T>.Values[0];
        }

        /// <summary>
        /// returns the Maximum enum value
        /// </summary>
        public static T Max<T>() where T : Enum
        {
            return EnumCache<T>.Offset < 0 ? EnumCache<T>.Values.Max() : EnumCache<T>.Values[EnumCache<T>.Length - 1];
        }

    }
}
