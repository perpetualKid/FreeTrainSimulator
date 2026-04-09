using System;
using System.Globalization;

namespace FreeTrainSimulator.Models.Signal
{
    /// <summary>
    /// A type-safe, extensible identifier for normal signal subtypes.
    /// Custom subtypes are registered at runtime from configuration files.
    /// </summary>
    /// <remarks>
    /// Use <see cref="SignalTypeRegistry"/> to register and look up identifiers by name.
    /// Implicit conversion to <see langword="int"/> allows direct use as an array/list index.
    /// </remarks>
    public readonly record struct SignalNormalSubType : IComparable<SignalNormalSubType>
    {
        /// <summary>Sentinel value representing no valid subtype.</summary>
        public static readonly SignalNormalSubType None = new SignalNormalSubType(-1);

        public int Index { get; }

        /// <summary>Whether this identifier represents a valid registered subtype.</summary>
        public bool Valid => Index >= 0;

        public SignalNormalSubType(int index)
        {
            Index = index;
        }

        /// <inheritdoc/>
        public int CompareTo(SignalNormalSubType other) => Index.CompareTo(other.Index);

        /// <summary>Implicit conversion to <see langword="int"/> for array/list indexing.</summary>
        public static implicit operator int(SignalNormalSubType normalSubType) => normalSubType.Index;

        public int ToInt32() => Index;

        /// <inheritdoc/>
        public override string ToString() => SignalTypeRegistry.Instance?.GetNormalSubTypeName(this) ?? Index.ToString(CultureInfo.InvariantCulture);

        public static bool operator <(SignalNormalSubType left, SignalNormalSubType right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(SignalNormalSubType left, SignalNormalSubType right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(SignalNormalSubType left, SignalNormalSubType right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(SignalNormalSubType left, SignalNormalSubType right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
