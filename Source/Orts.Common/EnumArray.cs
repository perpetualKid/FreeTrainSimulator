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

        public EnumArray(IEnumerable<T> source): this()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int i = 0;
            foreach (T item in source)
            {
                array[i] = item;
            }
        }

        public EnumArray(T[] source) : this()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Length != array.Length)
                throw new IndexOutOfRangeException($"Source array needs to be same size as number of enum values of {typeof(TEnum)}");
            Array.Copy(source, array, source.Length);
        }

        public EnumArray(T source) : this()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!(source is ValueType))
                throw new InvalidOperationException($"Cannot use reference type input to initialize multipe instances.");

            for (int i = 0; i < array.Length; i++) 
            { 
                array[i] = source; 
            }
        }

        public T this[TEnum key]
        {
            get => array[(int)(object)(key) - lowBound]; //TODO 20201015 use unsafe  array[Unsafe.As<TEnum, int>(ref key)];
            set => array[(int)(object)(key) - lowBound] = value;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return array.GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return (array as IEnumerable<T>).GetEnumerator();
        }
    }

    /// <summary>An two-dimensional array indexed by Enums</summary>
    /// <typeparam name="T">Type stored in array</typeparam>
    /// <typeparam name="TDimension1">Indexer dimension 1 Enum type</typeparam>
    /// <typeparam name="TDimension2">Indexer dimension 2 Enum type</typeparam>
#pragma warning disable CA1710 // Identifiers should have correct suffix
    public class EnumArray2D<T, TDimension1, TDimension2> where TDimension1: Enum where TDimension2: Enum
#pragma warning restore CA1715 // Identifiers should have correct prefix
    {
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
        private readonly T[,] array;
        private readonly int lowBoundX, lowBoundY;

        public EnumArray2D()
        {
            if (!typeof(int).IsAssignableFrom(Enum.GetUnderlyingType(typeof(TDimension1))))
                throw new ArgumentException(nameof(TDimension1));
            if (!typeof(int).IsAssignableFrom(Enum.GetUnderlyingType(typeof(TDimension2))))
                throw new ArgumentException(nameof(TDimension2));
            lowBoundX = (int)(object)EnumExtension.Min<TDimension1>();
            lowBoundY = (int)(object)EnumExtension.Min<TDimension2>();
            int highBoundX = (int)(object)EnumExtension.Max<TDimension1>();
            int highBoundY = (int)(object)EnumExtension.Max<TDimension2>();
            array = new T[1 + highBoundX - lowBoundX, 1 + highBoundY - lowBoundY];
        }

        public EnumArray2D(T source) : this()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!(source is ValueType))
                throw new InvalidOperationException($"Cannot use reference type input to initialize multipe instances.");

            for (int col = 0; col < array.GetLength(0); col++)
                for (int row = 0; row < array.GetLength(1); row++)
                    array[col, row] = source;
        }

        public T this[TDimension1 x, TDimension2 y]
        {
            get => array[(int)(object)(x) - lowBoundX, (int)(object)(y) - lowBoundY]; //TODO 20201015 use unsafe  array[Unsafe.As<TEnum, int>(ref key)];
            set => array[(int)(object)(x) - lowBoundX, (int)(object)(y) - lowBoundY] = value;
        }
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
    }
}
