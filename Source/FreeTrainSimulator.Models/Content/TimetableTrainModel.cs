using System;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Represents a single train entry within a timetable, including its consist,
    /// path, schedule, and grouping information.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("timetabletrain")]
    public sealed partial record TimetableTrainModel : ModelBase
    {
        /// <inheritdoc/>
        public override TimetableModel Parent => _parent as TimetableModel;
        /// <summary>Logical group name for categorizing related trains in the timetable.</summary>
        public string Group {  get; init; }
        /// <summary>Briefing text displayed for this train.</summary>
        public string Briefing { get; init; }
        /// <summary>Identifier of the wagon set (consist) used by this train.</summary>
        public string WagonSet { get; init; }
        /// <summary>Indicates whether the wagon set is reversed for this train.</summary>
        public bool WagonSetReverse { get; init; }
        /// <summary>Identifier of the path this train follows.</summary>
        public string Path { get; init; }
        /// <summary>Scheduled departure time for this train.</summary>
        public TimeOnly StartTime { get; init; }
    }
}
