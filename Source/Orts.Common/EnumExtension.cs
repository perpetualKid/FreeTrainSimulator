using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Orts.Common
{
    public static class EnumExtension
    {
        private static class EnumCache<T> where T : Enum
        {
            internal static readonly IList<string> Names;
            internal static readonly IList<T> Values;
            internal static readonly Dictionary<T, string> ValueToDescriptionMap;
            internal static string EnumDescription;
            internal static IDictionary<string, T> NameValuePairs;
            internal static int Length;
            internal static int SupportsReverse;

#pragma warning disable CA1810 // Initialize reference type static fields inline
            static EnumCache()
#pragma warning restore CA1810 // Initialize reference type static fields inline
            {
                Values = new ReadOnlyCollection<T>((T[])Enum.GetValues(typeof(T)));
                Length = Values.Count;
                Names = new ReadOnlyCollection<string>(Enum.GetNames(typeof(T)));
                ValueToDescriptionMap = new Dictionary<T, string>();
                EnumDescription = typeof(T).GetCustomAttributes(typeof(DescriptionAttribute), false).
                    Cast<DescriptionAttribute>().
                    Select(x => x.Description).
                    FirstOrDefault();
                foreach (T value in Values)//(T[])Enum.GetValues(typeof(T)))
                {
                    ValueToDescriptionMap[value] = GetDescription(value);
                }

                NameValuePairs = Names.Zip(Values, (k, v) => new { k, v })
                              .ToDictionary(x => x.k, x => x.v, StringComparer.OrdinalIgnoreCase);

                if (Length == 2 && (int)(object)Values[0] == 0 && (int)(object)Values[1] == 1)      //Simple case having two values only (like Forward and Backward, values 0 and 1 only)
                    SupportsReverse = 2;
                else if (Length % 2 == 1 && (int)(object)Values[Length / 2] == 0) //odd number of items, like Backward, Neutral and Forward
                    SupportsReverse = 1;

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
            if (EnumCache<T>.ValueToDescriptionMap.TryGetValue(item, out string description))
            {
                return description;
            }
            throw new ArgumentOutOfRangeException(nameof(item));
        }

        /// <summary>
        /// returns the Description attribute for the enum type
        /// </summary>
        public static string EnumDescription<T>() where T : Enum
        {
            return EnumCache<T>.EnumDescription;
        }

        /// <summary>
        /// returns a static list of all names in this enum
        /// </summary>
        public static IList<string> GetNames<T>() where T : Enum
        {
            return EnumCache<T>.Names;
        }

        /// <summary>
        /// returns a static list of all values in this enum
        /// </summary>
        public static IList<T> GetValues<T>() where T : Enum
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
            return EnumCache<T>.NameValuePairs.TryGetValue(name, out result);
        }

        /// <summary>
        /// allows to enumerate forward over enum values
        /// </summary>
        public static T Next<T>(this T item) where T : Enum
        {
            int next = (Unsafe.As<T, int>(ref item) + 1) % EnumCache<T>.Length;
            return Unsafe.As<int, T>(ref next);
        }

        /// <summary>
        /// allows to enumerate backward over enum values
        /// </summary>
        public static T Previous<T>(this T item) where T : Enum
        {
            int previous = (Unsafe.As<T, int>(ref item) - 1 + EnumCache<T>.Length) % EnumCache<T>.Length;
            return Unsafe.As<int, T>(ref previous);

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
            return EnumCache<T>.Values[0];
        }

        /// <summary>
        /// returns the Maximum enum value
        /// </summary>
        public static T Max<T>() where T : Enum
        {
            return EnumCache<T>.Values[EnumCache<T>.Values.Count - 1];
        }

    }
}
