using System;
using System.Globalization;

namespace FreeTrainSimulator.Models.Signal
{
    /// <summary>
    /// A type-safe, extensible identifier for signal sub-object types (e.g. decor, signal head, number plate).
    /// Predefined constants cover the well-known MSTS sub-object types.
    /// Custom sub-object types registered at runtime from configuration files receive new unique indices.
    /// </summary>
    /// <remarks>
    /// Use <see cref="SignalTypeRegistry"/> to register and look up identifiers by name.
    /// Implicit conversion to <see langword="int"/> allows direct use as an array/list index.
    /// </remarks>
    public readonly record struct SignalSubObjectType : IComparable<SignalSubObjectType>
    {
        #region standard MSTS Signal Sub-Object Types
        /// <summary>Predefined MSTS signal sub-object type names, ordered to match the static constants.</summary>
        private static readonly string[] mstsNames =
            [nameof(Decor),
            "Signal_Head",
            "Dummy1",
            "Dummy2",
            "Number_Plate",
            "Gradient_Plate",
            "User1",
            "User2",
            "User3",
            "User4"];

        /// <summary>Predefined MSTS signal sub-object type names, ordered to match the static constants.</summary>
        internal static ReadOnlySpan<string> MstsNames => mstsNames;

        /// <summary>Decorative sub-object</summary>
        public static readonly SignalSubObjectType Decor = new SignalSubObjectType(0);
        /// <summary>Signal head sub-object</summary>
        public static readonly SignalSubObjectType SignalHead = new SignalSubObjectType(1);
        /// <summary>MSTS reserved placeholder</summary>
        public static readonly SignalSubObjectType Dummy1 = new SignalSubObjectType(2);
        /// <summary>MSTS reserved placeholder</summary>
        public static readonly SignalSubObjectType Dummy2 = new SignalSubObjectType(3);
        /// <summary>Number plate sub-object</summary>
        public static readonly SignalSubObjectType NumberPlate = new SignalSubObjectType(4);
        /// <summary>Gradient plate sub-object</summary>
        public static readonly SignalSubObjectType GradientPlate = new SignalSubObjectType(5);
        /// <summary>User-defined sub-object type 1</summary>
        public static readonly SignalSubObjectType User1 = new SignalSubObjectType(6);
        /// <summary>User-defined sub-object type 2</summary>
        public static readonly SignalSubObjectType User2 = new SignalSubObjectType(7);
        /// <summary>User-defined sub-object type 3</summary>
        public static readonly SignalSubObjectType User3 = new SignalSubObjectType(8);
        /// <summary>User-defined sub-object type 4</summary>
        public static readonly SignalSubObjectType User4 = new SignalSubObjectType(9);
        #endregion

        /// <summary>Sentinel value representing no valid sub-object type.</summary>
        public static readonly SignalSubObjectType None = new SignalSubObjectType(-1);
        public int Index { get; }

        /// <summary>Whether this identifier represents a valid registered sub-object type.</summary>
        public bool Valid => Index >= 0;

        /// <summary>Whether this is one of the predefined MSTS sub-object types.</summary>
        public bool MstsSubObjectType => Index >= 0 && Index <= User4.Index;

        public SignalSubObjectType(int index)
        {
            Index = index;
        }

        /// <inheritdoc/>
        public int CompareTo(SignalSubObjectType other) => Index.CompareTo(other.Index);

        /// <summary>Implicit conversion to <see langword="int"/> for array/list indexing.</summary>
        public static implicit operator int(SignalSubObjectType subObjectType) => subObjectType.Index;

        public int ToInt32() => Index;

        /// <inheritdoc/>
        public override string ToString() => SignalTypeRegistry.Instance?.GetSubObjectTypeName(this) ?? Index.ToString(CultureInfo.InvariantCulture);

        public static bool operator <(SignalSubObjectType left, SignalSubObjectType right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(SignalSubObjectType left, SignalSubObjectType right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(SignalSubObjectType left, SignalSubObjectType right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(SignalSubObjectType left, SignalSubObjectType right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
