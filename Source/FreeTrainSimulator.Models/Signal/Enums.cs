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
}
