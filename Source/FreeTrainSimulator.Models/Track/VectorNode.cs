using System.Collections.Immutable;

using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record VectorNode : TrackNodeBase
    {
        public ImmutableArray<VectorSectionNode> VectorSections { get; init; } = ImmutableArray<VectorSectionNode>.Empty;

        [MemoryPackConstructor]
        public VectorNode(in WorldLocation location, in Tile worldTile) : base(location, worldTile, Vector3.Zero)
        {
        }
    }
}
