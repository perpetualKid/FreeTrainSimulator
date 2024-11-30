using System.Collections.Frozen;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
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
}
