using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Describes a draw state: a combination of lights and semaphore arm positions
    /// that together represent one visual appearance of a signal.
    /// </summary>
    /// <remarks>
    /// Derived from the <c>SignalDrawState</c> entries in the MSTS <c>sigcfg.dat</c> file.
    /// Each draw state maps to an <see cref="SignalAspect"/> via <see cref="SignalAspect.DrawStateName"/>.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalDrawState
    {
        /// <summary>Numeric index of this draw state within its signal type.</summary>
        public int Index { get; init; }

        /// <summary>Name of the draw state (used as key in <see cref="SignalType.DrawStates"/>).</summary>
        public string Name { get; init; }

        /// <summary>Semaphore arm position index (0 = default position). Only applicable to semaphore signals.</summary>
        public int SemaphorePosition { get; init; }

        /// <summary>Mode (lit, unlit, flashing) for each light in this draw state, ordered by light index.</summary>
        public ImmutableArray<SignalDrawStateLightMode> DrawStateLights { get; init; } = ImmutableArray<SignalDrawStateLightMode>.Empty;

    }
}
