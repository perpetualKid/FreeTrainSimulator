using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".tmodel")]
    public sealed partial record TrackModel : ModelBase
    {
        public override RouteModel Parent => _parent as RouteModel;
        public TrackDatabase TrackDatabase { get; init; }
        public TrackDatabase RoadDatabase { get; init; }
    }
}
