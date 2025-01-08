using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

using MemoryPack;

namespace FreeTrainSimulator.Common
{
    /// <summary>An array indexed by an Enum</summary>
    /// <typeparam name="T">Type stored in array</typeparam>
    /// <typeparam name="TEnum">Indexer Enum type</typeparam>
#pragma warning disable CA1710 // Identifiers should have correct suffix
    [MemoryPackable]
    public partial class EnumArray<T, TEnum> : IEnumerable, IEnumerable<T> where TEnum : Enum
#pragma warning restore CA1710 // Identifiers should have correct suffix
    {
        [MemoryPackInclude]
        private readonly T[] array;
        [MemoryPackInclude]
        private readonly int lowBound;

        [MemoryPackConstructor]
        private EnumArray(T[] array, int lowBound)
        {
            this.array = array;
            this.lowBound = lowBound;
        }

        public EnumArray()
        {
            if (!typeof(int).IsAssignableFrom(Enum.GetUnderlyingType(typeof(TEnum))))
                throw new ArgumentException(nameof(TEnum));
            TEnum dimension = EnumExtension.Min<TEnum>();
            lowBound = Unsafe.As<TEnum, int>(ref dimension);
            dimension = EnumExtension.Max<TEnum>();
            int highBound = Unsafe.As<TEnum, int>(ref dimension);
            array = new T[1 + highBound - lowBound];
        }

        public EnumArray(IEnumerable<T> source) : this()
        {
            ArgumentNullException.ThrowIfNull(source);

            int i = 0;
            foreach (T item in source)
                array[i++] = item;
        }

        public EnumArray(Func<T> initializer) : this()
        {
            ArgumentNullException.ThrowIfNull(initializer);

            for (int i = 0; i < array.Length; i++)
                array[i] = initializer.Invoke();
        }

        public EnumArray(Func<TEnum, T> initializer) : this()
        {
            ArgumentNullException.ThrowIfNull(initializer);

            foreach (TEnum dimension in EnumExtension.GetValues<TEnum>())
            {
                this[dimension] = initializer.Invoke(dimension);
            }
        }

        public EnumArray(T[] source) : this()
        {
            ArgumentNullException.ThrowIfNull(source);
            FromArray(source);
        }

        public EnumArray(T source) : this()
        {
            ArgumentNullException.ThrowIfNull(source);
            if (source is not ValueType)
                throw new InvalidOperationException($"Cannot use reference type input to initialize multipe instances.");

            for (int i = 0; i < array.Length; i++)
                array[i] = source;
        }

        public T this[TEnum key]
        {
            get => array[Unsafe.As<TEnum, int>(ref key) - lowBound];
            set => array[Unsafe.As<TEnum, int>(ref key) - lowBound] = value;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return array.GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return (array as IEnumerable<T>).GetEnumerator();
        }

        public T[] ToArray()
        {
            return array;
        }

        public void FromArray(T[] values)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (values.Length != array.Length)
                throw new ArgumentOutOfRangeException($"Source array needs to be same size as number of enum values of {typeof(TEnum)}");
            Array.Copy(values, array, values.Length);
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (TEnum item in EnumExtension.GetValues<TEnum>())
            {
                builder.Append(item);
                builder.Append('=');
                if (typeof(T).IsArray)
                {
                    if (this[item] != null)
                    {
                        foreach (dynamic arrayItem in (Array)(dynamic)this[item])
                        {
                            builder.Append(arrayItem);
                        builder.Append(',');
                        }
                        if (builder[^1] == ',')
                            builder.Length--;
                    }
                }
                else
                    builder.Append(this[item]);
                builder.Append(';');
            }
            if (builder.Length > 0 && builder[^1] == ';')
                builder.Length--;
            return builder.ToString();
        }
    }

    /// <summary>An two-dimensional array indexed by Enums</summary>
    /// <typeparam name="T">Type stored in array</typeparam>
    /// <typeparam name="TDimension1">Indexer dimension 1 Enum type</typeparam>
    /// <typeparam name="TDimension2">Indexer dimension 2 Enum type</typeparam>
    [MemoryPackable]
    public partial class EnumArray2D<T, TDimension1, TDimension2> where TDimension1 : Enum where TDimension2 : Enum
    {
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
        [MemoryPackInclude]
        private readonly T[,] array;
        [MemoryPackInclude]
        private readonly int lowBoundX, lowBoundY;

        [MemoryPackConstructor]
        private EnumArray2D(T[,] array, int lowBoundX, int lowBoundY)
        {
            this.array = array;
            this.lowBoundX = lowBoundX;
            this.lowBoundY = lowBoundY;
        }

        public EnumArray2D()
        {
            if (!typeof(int).IsAssignableFrom(Enum.GetUnderlyingType(typeof(TDimension1))))
                throw new ArgumentException(nameof(TDimension1));
            if (!typeof(int).IsAssignableFrom(Enum.GetUnderlyingType(typeof(TDimension2))))
                throw new ArgumentException(nameof(TDimension2));
            TDimension1 dimension1 = EnumExtension.Min<TDimension1>();
            lowBoundX = Unsafe.As<TDimension1, int>(ref dimension1);
            TDimension2 dimension2 = EnumExtension.Min<TDimension2>();
            lowBoundY = Unsafe.As<TDimension2, int>(ref dimension2);
            dimension1 = EnumExtension.Max<TDimension1>();
            dimension2 = EnumExtension.Max<TDimension2>();
            int highBoundX = Unsafe.As<TDimension1, int>(ref dimension1);
            int highBoundY = Unsafe.As<TDimension2, int>(ref dimension2);
            array = new T[1 + highBoundX - lowBoundX, 1 + highBoundY - lowBoundY];
        }

        public EnumArray2D(T source) : this()
        {
            ArgumentNullException.ThrowIfNull(source);
            if (source is not ValueType)
                throw new InvalidOperationException($"Cannot use reference type input to initialize multipe instances.");

            for (int col = 0; col < array.GetLength(0); col++)
                for (int row = 0; row < array.GetLength(1); row++)
                    array[col, row] = source;
        }

        public EnumArray2D(Func<T> initializer) : this()
        {
            ArgumentNullException.ThrowIfNull(initializer);

            for (int col = 0; col < array.GetLength(0); col++)
                for (int row = 0; row < array.GetLength(1); row++)
                    array[col, row] = initializer.Invoke();
        }

        public EnumArray2D(Func<TDimension1, TDimension2, T> initializer) : this()
        {
            ArgumentNullException.ThrowIfNull(initializer);

            foreach (TDimension1 dimension1 in EnumExtension.GetValues<TDimension1>())
            {
                foreach (TDimension2 dimension2 in EnumExtension.GetValues<TDimension2>())
                {
                    this[dimension1, dimension2] = initializer.Invoke(dimension1, dimension2);
                }
            }
        }

        public EnumArray2D(IList<T> source) : this()
        {
            ArgumentNullException.ThrowIfNull(source);
            int columns = array.GetLength(0);
            int rows = array.GetLength(1);
            if (source.Count != columns * rows)
                throw new InvalidOperationException($"Source array needs to fit into target array.");

            for (int col = 0; col < columns; col++)
                for (int row = 0; row < rows; row++)
                    array[col, row] = source[col * columns + row];
        }

        public EnumArray2D(T[,] source) : this()
        {
            ArgumentNullException.ThrowIfNull(source);
            FromArray(source);
        }

        public T this[TDimension1 dimension1, TDimension2 dimension2]
        {
            get => array[Unsafe.As<TDimension1, int>(ref dimension1) - lowBoundX, Unsafe.As<TDimension2, int>(ref dimension2) - lowBoundY];
            set => array[Unsafe.As<TDimension1, int>(ref dimension1) - lowBoundX, Unsafe.As<TDimension2, int>(ref dimension2) - lowBoundY] = value;
        }

        public T[,] ToArray()
        {
            return array;
        }

        public void FromArray(T[,] values)
        {
            ArgumentNullException.ThrowIfNull(values);
            int columns = array.GetLength(0);
            int rows = array.GetLength(1);
            if (values.GetLength(0) != columns || values.GetLength(1) != rows)
                throw new InvalidOperationException($"Source array needs to fit into target array.");

            for (int col = 0; col < columns; col++)
                for (int row = 0; row < rows; row++)
                    array[col, row] = values[col, row];
        }
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
    }
}
