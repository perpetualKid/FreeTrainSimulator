using System.Collections.Immutable;

using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    public abstract partial record TrackNode
    {
        private readonly WorldLocation location;
        private readonly Tile worldTile;
        public ref readonly WorldLocation Location => ref location;

        public ref readonly Tile WorldTile => ref worldTile;

        public int NodeIndex { get; init; }

        public int WorldId { get; init; }

        protected TrackNode(in WorldLocation location, in Tile worldTile) 
        {
            this.location = location;
            this.worldTile = worldTile;
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record VectorNode : TrackNode
    {
        public ImmutableArray<VectorSectionNode> VectorSections { get; init; } = new ImmutableArray<VectorSectionNode>();

        [MemoryPackConstructor]
        public VectorNode(in WorldLocation location, in Tile worldTile) : base(location, worldTile)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record VectorSectionNode : TrackNode
    {
        private readonly Vector3 direction;

        public ref readonly Vector3 Direction => ref direction;
        public ImmutableArray<int> Vectors { get; init; } = new ImmutableArray<int>();

        public int Flag1 { get; }
        public int Flag2 { get; }
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public VectorSectionNode(in WorldLocation location, in Tile worldTile, in Vector3 direction) : base(location, worldTile)
        {
            this.direction = direction;
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record EndNode : TrackNode
    {
        [MemoryPackConstructor]
        public EndNode(in WorldLocation location, in Tile worldTile) : base(location, worldTile)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record JunctionNode : TrackNode
    {
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public JunctionNode(in WorldLocation location, in Tile worldTile) : base(location, worldTile)
        {
        }
    }
}
