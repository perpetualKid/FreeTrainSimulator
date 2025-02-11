using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PathNode
    {
        private readonly WorldLocation location;

        public ref readonly WorldLocation Location => ref location;
        public PathNodeType NodeType { get; init; }
        public int NodeIndex { get; init; }
        public int NextMainNode { get; init; }
        public int NextSidingNode { get; init; }

        [MemoryPackConstructor]
        public PathNode(in WorldLocation location)
        {
            this.location = location;
        }
    }
}
