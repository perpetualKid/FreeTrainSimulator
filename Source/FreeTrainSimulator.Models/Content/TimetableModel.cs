using System.Collections.Immutable;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Represents a timetable definition containing the collection of trains and their schedules.
    /// Abstracts data from Open Rails timetable files.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Timetables", ".timetable")]
    public sealed partial record TimetableModel : ModelBase
    {
        /// <inheritdoc/>
        public override RouteModelHeader Parent => _parent as RouteModelHeader;

        /// <summary>Collection of train definitions that participate in this timetable.</summary>
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
