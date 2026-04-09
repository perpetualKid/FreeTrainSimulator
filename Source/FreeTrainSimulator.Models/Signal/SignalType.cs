using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signal
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
        public string Name { get; init; }
        /// allocated script
        public string Script { get; init; }
        /// <summary>Extensible signal function type identifier (covers both MSTS and custom ORTS function types).</summary>
        public SignalFunction FunctionType { get; init; }
        /// <summary>Extensible normal subtype identifier for Normal signals.</summary>
        public SignalNormalSubType NormalSubType { get; init; }
        public SignalFlags SignalFlags { get; init; }
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
        /// <summary>Number of blocks ahead which need to be cleared in order to maintain a 'clear' indication
        /// in front of a train. MSTS calculation</summary>
        public int ClearAheadNumberMsts { get; init; }
        /// <summary>Number of blocks ahead which need to be cleared in order to maintain a 'clear' indication
        /// in front of a train. ORTS calculation</summary>
        public int ClearAheadNumberOrts { get; init; }
        //public ImmutableArray<SignalLightModel> Lights { get; init; } = ImmutableArray<SignalLightModel>.Empty;
        //public ImmutableDictionary<string, SignalDrawStateModel> DrawStates { get; init; } = ImmutableDictionary<string, SignalDrawStateModel>.Empty;
        //public ImmutableArray<SignalAspectModel> Aspects { get; init; } = ImmutableArray<SignalAspectModel>.Empty;
        //public ApproachControlLimitsModel ApproachControlDetails { get; init; }

    }
}
