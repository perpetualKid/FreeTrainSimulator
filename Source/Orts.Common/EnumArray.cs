using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Orts.Common
{
    /// <summary>An array indexed by an Enum</summary>
    /// <typeparam name="T">Type stored in array</typeparam>
    /// <typeparam name="TEnum">Indexer Enum type</typeparam>
#pragma warning disable CA1710 // Identifiers should have correct suffix
    public class EnumArray<T, TEnum> : IEnumerable, IEnumerable<T> where TEnum : Enum
#pragma warning restore CA1715 // Identifiers should have correct prefix
    {
        private readonly T[] array;
        private readonly int lowBound;

        public EnumArray()
        {
            if (!typeof(int).IsAssignableFrom(Enum.GetUnderlyingType(typeof(TEnum))))
                throw new ArgumentException(nameof(TEnum));
            lowBound = (int)(object)EnumExtension.Min<TEnum>();
            int highBound = (int)(object)EnumExtension.Max<TEnum>();
            array = new T[1 + highBound - lowBound];
        }

        public T this[TEnum key]
        {
            get => array[(int)(object)(key) - lowBound];
            set => array[(int)(object)(key) - lowBound] = value;
        }

        public IEnumerator GetEnumerator()
        {
            return EnumExtension.GetValues<TEnum>().Select(i => this[i]).GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return EnumExtension.GetValues<TEnum>().Select(i => this[i]).GetEnumerator();
        }
    }
}
