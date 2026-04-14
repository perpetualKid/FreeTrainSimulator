using System;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Flags describing signal type options.
    /// </summary>
    [Flags]
    public enum SignalOptions
    {
        None = 0,
        /// <summary>This is a semaphore signal</summary>
        Semaphore = 1 << 0,
        /// <summary>This signal type is not suitable for placement on a gantry</summary>
        NoGantry = 1 << 1,
        /// <summary>Unknown, used at least in Marias Pass route</summary>
        Abs = 1 << 2,
    }

    /// <summary>
    /// Mode of a single light within a <see cref="SignalDrawState"/>.
    /// </summary>
    public enum SignalDrawStateLightMode
    {
        /// <summary>Light is off.</summary>
        Unlit,
        /// <summary>Light is steadily on.</summary>
        Lit,
        /// <summary>Light is flashing on and off.</summary>
        Flashing,
    }

    /// <summary>
    /// Flags describing additional behavior of a <see cref="SignalAspect"/>.
    /// </summary>
    [Flags]
    public enum SignalAspectOptions
    {
        None = 0,
        /// <summary>Set if SignalOptions ASAP option specified, meaning train needs to go to speed As Soon As Possible</summary>
        Asap = 1 << 0,
        /// <summary>Set if SignalOptions RESET option specified (ORTS only)</summary>
        SpeedReset = 1 << 1,
        /// <summary>Set if no speed reduction is required for RESTRICTED or STOP_AND_PROCEED aspects (ORTS only) </summary>
        NoSpeedReduction = 1 << 2,
    }

    /// <summary>
    /// Flags describing optional behavior of a <see cref="SignalSubObject"/> within a <see cref="SignalShape"/>.
    /// </summary>
    [Flags]
    public enum SignalSubObjectOptions
    {
        None = 0,
        /// <summary>The sub-object is optional on this signal shape</summary>
        Optional = 1 << 0,
        /// <summary>The sub-object will be enabled by default (when manually placed)</summary>
        Default = 1 << 1,
        /// <summary>The sub-object is facing backwards w.r.t. rest of object</summary>
        BackFacing = 1 << 2,
        /// <summary>Signal should always have a junction link</summary>
        JunctionLink = 1 << 3,
    }
}
