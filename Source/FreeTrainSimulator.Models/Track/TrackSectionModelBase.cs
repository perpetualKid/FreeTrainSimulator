using System.Collections.Immutable;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    public abstract record TrackSectionModelBase : ModelBase
    {
        public int BuildVersion { get; init; }

        public ImmutableArray<TrackSection> TrackSections { get; init; } = ImmutableArray<TrackSection>.Empty;
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Global", ".tsection")]
    public partial record GlobalTrackSectionModel: TrackSectionModelBase
    {
        public override TrackSectionModelBase Parent => null;
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".tsection")]
    public partial record RouteTrackSectionModel : TrackSectionModelBase
    {
        public override RouteModelHeader Parent => _parent as RouteModelHeader;
    }
}
