using System;

namespace FreeTrainSimulator.Models.Signal
{
    [Flags]
    public enum SignalFlags
    {
        None = 0,
        /// <summary>This is a semaphore signal</summary>
        Semaphore = 1 << 0,
        /// <summary>This signal type is not suitable for placement on a gantry</summary>
        NoGantry = 1 << 1,
        /// <summary>Unknown, used at least in Marias Pass route</summary>
        Abs = 1 << 2,
    }

    public enum SignalDrawStateLightMode
    {
        Unlit,
        Lit,
        Flashing,
    }

    [Flags]
    public enum SignalAspectFlags
    {
        None = 0,
        /// <summary>Set if SignalFlags ASAP option specified, meaning train needs to go to speed As Soon As Possible</summary>
        Asap = 1 << 0,
        /// <summary>Set if SignalFlags RESET option specified (ORTS only)</summary>
        SpeedReset = 1 << 1,
        /// <summary>Set if no speed reduction is required for RESTRICTED or STOP_AND_PROCEED aspects (ORTS only) </summary>
        NoSpeedReduction = 1 << 2,
    }
}
