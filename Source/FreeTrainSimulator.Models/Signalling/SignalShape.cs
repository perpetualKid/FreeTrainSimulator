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
        public string ShapeFileName { get; init; }
        public string Description { get; init; }
        public ImmutableArray<SignalSubObject> SubObjects { get; init; } = ImmutableArray<SignalSubObject>.Empty;
    }
}
