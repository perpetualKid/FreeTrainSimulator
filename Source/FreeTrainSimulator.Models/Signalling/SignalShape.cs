using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Describes the physical shape of a signal, including the number and arrangement of signal heads and lights.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalShape
    {
        /// <summary>File name of the 3D shape model (<c>.s</c> file) for this signal.</summary>
        public string ShapeFileName { get; init; }

        /// <summary>Human-readable description of this signal shape.</summary>
        public string Description { get; init; }

        /// <summary>Sub-objects (signal heads, number plates, decorative elements) that compose this signal shape.</summary>
        public ImmutableArray<SignalSubObject> SubObjects { get; init; } = ImmutableArray<SignalSubObject>.Empty;
    }
}
