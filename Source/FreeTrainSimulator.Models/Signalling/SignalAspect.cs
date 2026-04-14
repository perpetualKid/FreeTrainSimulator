using FreeTrainSimulator.Common;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Describes a signal aspect: a combination of signal indication state and its meaning.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalAspect
    {
        /// <summary>The signal aspect state this entry represents (e.g. Stop, Clear1, Approach).</summary>
        public SignalAspectState Aspect { get; init; }

        /// <summary>Name of the <see cref="SignalDrawState"/> to display when this aspect is active.</summary>
        public string DrawStateName { get; init; }
        /// <summary>Speed limit in meters per second. -1 if track speed applies.</summary>
        public float SpeedLimit { get; init; } = -1;
        /// <summary>Additional behavior flags for this aspect (ASAP speed change, speed reset, etc.).</summary>
        public SignalAspectOptions AspectFlags { get; init; }
    }
}
