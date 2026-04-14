using System.Collections.Immutable;

using FreeTrainSimulator.Common;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Signal type defining the attributes of a category of signal heads.
    /// Only SIGFN_NORMAL signal heads will require a train to take action (e.g. to stop).  
    /// The other values act only as categories for signal types to belong to.
    /// Within MSTS and scripts known as SIGFN_ values.  
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalType
    {
        /// <summary>Unique name of this signal type as defined in the signal configuration.</summary>
        public string Name { get; init; }
        /// <summary>Name of the allocated signal script that controls this type's behavior.</summary>
        public string Script { get; init; }
        /// <summary>Extensible signal function type identifier (covers both MSTS and custom ORTS function types).</summary>
        public SignalFunctionType FunctionType { get; init; }
        /// <summary>Extensible normal subtype identifier for Normal signals.</summary>
        public SignalNormalSubType NormalSubType { get; init; }
        /// <summary>Signal option flags (semaphore, no-gantry, ABS).</summary>
        public SignalOptions SignalFlags { get; init; }
        /// <summary>On duration for flashing light. (In seconds.)</summary>
        public float FlashTimeOn { get; init; } = 1.0f;
        /// <summary>Off duration for flashing light. (In seconds.)</summary>
        public float FlashTimeOff { get; init; } = 1.0f;
        /// <summary>Transition time between lit and unlit states. (In seconds.)</summary>
        public float TransitionTime { get; init; } = 0.2f;
        /// <summary>The name of the texture to use for the lights</summary>
        public string LightTexture { get; init; }
        /// <summary>Semaphore signal animation duration. (In seconds.)</summary>
        public float SemaphoreAnimationnDuration { get; init; } = 1.0f;
        /// <summary> Glow value for daytime (optional).</summary>
        public float? DayGlow { get; init; }
        /// <summary> Glow value for nighttime (optional).</summary>
        public float? NightGlow { get; init; }
        /// <summary> Lights switched off or on during daytime (default : on) (optional).</summary>
        public bool DayLight { get; init; } = true;
        /// <summary>Determines the clear-ahead calculation mode (MSTS or ORTS).
        /// MSTS mode counts by number of signal heads; ORTS mode counts by number of signals.
        /// MSTS-style calculation: subtracts signal head count when propagating.
        /// ORTS-style calculation: subtracts 1 per signal when propagating.
        /// </summary>
        public CompatibilityMode SignalClearAheadMode { get; init; }
        /// <summary>Number of blocks ahead which need to be cleared in order to maintain a 'clear' indication
        /// in front of a train.</summary>
        public int ClearAheadNumber { get; init; }
        /// <summary>Ordered list of lights that can be displayed by signal heads of this type.</summary>
        public ImmutableArray<SignalLight> Lights { get; init; } = ImmutableArray<SignalLight>.Empty;

        /// <summary>Named draw states available for this signal type, keyed by state name.</summary>
        public ImmutableDictionary<string, SignalDrawState> DrawStates { get; init; } = ImmutableDictionary<string, SignalDrawState>.Empty;

        /// <summary>Aspect-to-draw-state mappings defining how each signal indication is visualized.</summary>
        public ImmutableArray<SignalAspect> SignalAspects { get; init; } = ImmutableArray<SignalAspect>.Empty;

        /// <summary>Approach control limit position in meters. Trains must be within this distance before
        /// the signal will clear. <see langword="null"/> if approach control is not configured.</summary>
        public float? ApproachControlLimitPosition { get; init; }

        /// <summary>Approach control limit speed in meters per second. Trains must be traveling at or below
        /// this speed before the signal will clear. <see langword="null"/> if not configured.</summary>
        public float? ApproachControlLimitSpeed { get; init; }
    }
}
