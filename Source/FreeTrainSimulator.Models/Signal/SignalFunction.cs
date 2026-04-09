using System;
using System.Globalization;

namespace FreeTrainSimulator.Models.Signal
{
    /// <summary>
    /// A type-safe, extensible identifier for signal function types.
    /// Predefined constants cover the well-known MSTS function types (Normal through Unknown).
    /// Custom function types registered at runtime from configuration files receive new unique indices.
    /// </summary>
    /// <remarks>
    /// Use <see cref="SignalTypeRegistry"/> to register and look up identifiers by name.
    /// Implicit conversion to <see langword="int"/> allows direct use as an array/list index.
    /// </remarks>
    public readonly record struct SignalFunction : IComparable<SignalFunction>
    {
        #region standard MSTS Signal Functions
        /// <summary>Predefined MSTS signal function type names, ordered to match the static constants.</summary>
        private static readonly string[] mstsNames =
            [nameof(Normal),
            nameof(Distance),
            nameof(Repeater), 
            nameof(Shunting), 
            nameof(Info), 
            nameof(Speed), 
            nameof(Alert), 
            nameof(Unknown)];

        /// <summary>Predefined MSTS signal function type names, ordered to match the static constants.</summary>
        internal static ReadOnlySpan<string> MstsNames => mstsNames;

        // Well-known MSTS signal function types
        /// <summary>Signal head showing primary indication</summary>
        public static readonly SignalFunction Normal = new SignalFunction(0);
        /// <summary>Distance signal head</summary>
        public static readonly SignalFunction Distance = new SignalFunction(1);
        /// <summary>Repeater signal head</summary>
        public static readonly SignalFunction Repeater = new SignalFunction(2);
        /// <summary>Shunting signal head</summary>
        public static readonly SignalFunction Shunting = new SignalFunction(3);
        /// <summary>Signal is informational only</summary>
        public static readonly SignalFunction Info = new SignalFunction(4);
        /// <summary>Speedpost signal</summary>
        public static readonly SignalFunction Speed = new SignalFunction(5);
        /// <summary>Alerting function</summary>
        public static readonly SignalFunction Alert = new SignalFunction(6);
        /// <summary>Unknown signal type</summary>
        public static readonly SignalFunction Unknown = new SignalFunction(7);
        #endregion

        public int Index { get; }

        /// <summary>Sentinel value representing no valid function type.</summary>
        public static readonly SignalFunction None = new SignalFunction(-1);

        /// <summary>Whether this identifier represents a valid registered function type.</summary>
        public bool Valid => Index >= 0;

        /// <summary>Whether this is one of the predefined MSTS function types (Normal through Unknown).</summary>
        public bool MstsSignalFunction => Index >= 0 && Index <= Unknown.Index;

        public SignalFunction(int index)
        { 
            Index = index; 
        }

        /// <inheritdoc/>
        public int CompareTo(SignalFunction other) => Index.CompareTo(other.Index);

        /// <summary>Implicit conversion to <see langword="int"/> for array/list indexing.</summary>
        public static implicit operator int(SignalFunction signalFunction) => signalFunction.Index;

        public int ToInt32() => Index;

        /// <inheritdoc/>
        public override string ToString() => SignalTypeRegistry.Instance?.GetFunctionName(this) ?? Index.ToString(CultureInfo.InvariantCulture);

        public static bool operator <(SignalFunction left, SignalFunction right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(SignalFunction left, SignalFunction right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(SignalFunction left, SignalFunction right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(SignalFunction left, SignalFunction right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
