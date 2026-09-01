using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Represents a single node in a train path, defining a waypoint with its world location,
    /// type classification, and links to the next main and siding path nodes.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PathNode
    {
        private readonly WorldLocation location;

        /// <summary>World location of this path node.</summary>
        public ref readonly WorldLocation Location => ref location;
        /// <summary>Classification of the node (start, end, via, junction, etc.).</summary>
        public PathNodeType NodeType { get; init; }
        /// <summary>Index into the track node array identifying the associated track node.</summary>
        public int NodeIndex { get; init; }
        /// <summary>Index of the next node along the main path, or -1 if none.</summary>
        public int NextMainNode { get; init; }
        /// <summary>Index of the next node along a siding/passing path, or -1 if none.</summary>
        public int NextSidingNode { get; init; }
        /// <summary>Optional wait/stop information at this node, or <see langword="null"/> if no wait.</summary>
        public PathNodeWaitInfo WaitInfo { get; init; }

        [MemoryPackConstructor]
        public PathNode(in WorldLocation location)
        {
            this.location = location;
        }
    }
}
