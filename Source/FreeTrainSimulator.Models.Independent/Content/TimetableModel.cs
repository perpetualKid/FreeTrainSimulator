using System;
using System.Collections.Frozen;

using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TimetableModel : ModelBase<TimetableModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".timetable";
            subFolder = "Timetables";
        }

        public override RouteModelCore Parent => _parent as RouteModelCore;

        public FrozenSet<TimetableTrainModel> TimetableTrains { get; init; } = FrozenSet<TimetableTrainModel>.Empty;

        public override void Initialize(string file, IFileResolve parent)
        {
            foreach (TimetableTrainModel train in TimetableTrains)
            {
                train.Initialize(null, this);
            }
            base.Initialize(file, parent);
        }
    }

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
