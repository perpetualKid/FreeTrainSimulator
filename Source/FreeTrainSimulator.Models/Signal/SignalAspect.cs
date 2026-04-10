using FreeTrainSimulator.Common;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signal
{
    /// <summary>
    /// Describes a signal aspect: a combination of signal indication state and its meaning.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalAspect
    {
        public SignalAspectState Aspect { get; init; }
        public string DrawStateName { get; init; }
        /// <summary>Speed limit in meters per second. -1 if track speed applies.</summary>
        public float SpeedLimit { get; init; } = -1;
        public SignalAspectFlags AspectFlags { get; init; }
    }
}
