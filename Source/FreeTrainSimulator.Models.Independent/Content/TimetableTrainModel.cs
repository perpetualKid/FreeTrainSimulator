using System;

using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TimetableTrainModel : ModelBase<TimetableTrainModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".timetabletrain";
        }

        public override TimetableModel Parent => _parent as TimetableModel;
        public string Group {  get; init; }
        public string Briefing { get; init; }
        public string WagonSet { get; init; }
        public bool WagonSetReverse { get; init; }
        public string Path { get; init; }
        public TimeOnly StartTime { get; init; }
    }
}
