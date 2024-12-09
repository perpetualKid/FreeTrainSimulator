using System.Collections.Frozen;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TimetableModel : ModelBase, IFileResolve
    {
        static string IFileResolve.SubFolder => "Timetables";
        static string IFileResolve.DefaultExtension => ".timetable";

        public override RouteModelCore Parent => _parent as RouteModelCore;

        public FrozenSet<TimetableTrainModel> TimetableTrains { get; init; } = FrozenSet<TimetableTrainModel>.Empty;

        public override void Initialize(ModelBase parent)
        {
            foreach (TimetableTrainModel train in TimetableTrains)
            {
                train.Initialize(this);
            }
            base.Initialize(parent);
        }
    }
}
