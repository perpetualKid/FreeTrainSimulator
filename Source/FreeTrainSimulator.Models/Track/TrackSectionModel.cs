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
        public ImmutableDictionary<int, TrackSection> TrackSections { get; init; } = ImmutableDictionary<int, TrackSection>.Empty;
        public ImmutableDictionary<int, TrackShape> TrackShapes { get; init; } = ImmutableDictionary<int, TrackShape>.Empty;
        public ImmutableDictionary<int, DynamicTrackSection> DynamicTrackSections { get; init; } = ImmutableDictionary<int, DynamicTrackSection>.Empty;
        public override RouteModel Parent => _parent as RouteModel;
    }
}
