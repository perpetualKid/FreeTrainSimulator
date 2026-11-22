using System;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Persisted native FTS wait or advanced path-operation data. Typed fields are authoritative; unknown operation
    /// payloads are retained losslessly without treating integer encodings as a native source of truth.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PathNodeWaitInfo
    {
        /// <summary>Native operation kind.</summary>
        public WaitOperationType WaitOperation { get; init; }

        /// <summary>Basic duration, uncouple delay, or horn duration according to <see cref="WaitOperation"/>.</summary>
        public TimeSpan Duration { get; init; }

        /// <summary>Time of day for a wait-until operation.</summary>
        public TimeSpan TimeOfDay { get; init; }

        /// <summary>Number of cars retained by an uncouple operation.</summary>
        public int CarCount { get; init; }

        /// <summary>Whether an uncouple operation retains the rear rather than the front.</summary>
        public bool KeepRear { get; init; }

        /// <summary>Opaque payload retained when the native operation type is unrecognized.</summary>
        public int UnknownCode { get; init; }
    }
}
