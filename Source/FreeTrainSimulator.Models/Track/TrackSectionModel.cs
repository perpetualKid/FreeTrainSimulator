using System.Collections.Immutable;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".tsection")]
    public partial record TrackSectionModel: ModelBase
    {
        public ImmutableArray<TrackSection> TrackSections { get; init; } = ImmutableArray<TrackSection>.Empty;
        public override RouteModel Parent => _parent as RouteModel;
    }
}
