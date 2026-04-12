using MemoryPack;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Describes a sub-object of a signal, such as a signal head, number plate, or decorative element.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalSubObject
    {
        /// <summary>Name of the group within the signal shape which defines this head</summary>
        public string MatrixName { get; init; }
        /// <summary></summary>
        public string Description { get; init; }
        /// <summary>Index of the signal sub-object type (decor, signal_head, ...). -1 if not specified</summary>
        public SignalSubObjectType SignalSubType { get; init; }
        /// <summary>Signal Type of the this sub-object</summary>
        public string SignalSubSignalType { get; init; }
        public SignalSubObjectOptions SubObjectFlags { get; init; }
    }
}
