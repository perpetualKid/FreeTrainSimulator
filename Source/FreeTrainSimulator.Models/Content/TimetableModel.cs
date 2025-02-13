using System.Collections.Frozen;
using System.Collections.Immutable;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Timetables", ".timetable")]
    public sealed partial record TimetableModel : ModelBase
    {
        public override RouteModelHeader Parent => _parent as RouteModelHeader;

        public ImmutableArray<TimetableTrainModel> TimetableTrains { get; init; } = ImmutableArray<TimetableTrainModel>.Empty;

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
