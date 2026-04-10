using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signal
{
    /// <summary>
    /// Describes a draw state: a combination of lights and semaphore arm positions.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalDrawState
    {
        public string Name { get; init; }
        public int SemaphorePosition { get; init; }
        public ImmutableArray<SignalDrawStateLightMode> DrawStateLights { get; init; } = ImmutableArray<SignalDrawStateLightMode>.Empty;

    }
}
