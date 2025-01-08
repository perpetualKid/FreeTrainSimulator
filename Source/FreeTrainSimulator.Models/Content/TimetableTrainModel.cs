using System;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", "timetabletrain")]
    public sealed partial record TimetableTrainModel : ModelBase
    {
        public override TimetableModel Parent => _parent as TimetableModel;
        public string Group {  get; init; }
        public string Briefing { get; init; }
        public string WagonSet { get; init; }
        public bool WagonSetReverse { get; init; }
        public string Path { get; init; }
        public TimeOnly StartTime { get; init; }
    }
}
