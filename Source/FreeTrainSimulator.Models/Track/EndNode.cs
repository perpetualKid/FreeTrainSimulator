using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// A track node representing a dead-end (buffer stop) where the track terminates.
    /// </summary>
    /// <remarks>
    /// Corresponds to a <c>TrEndNode</c> in the MSTS <c>.tdb</c> file.
    /// End nodes have no additional properties beyond <see cref="TrackNodeBase"/>.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record EndNode : TrackNodeBase
    {
        [MemoryPackConstructor]
        public EndNode(in WorldLocation location, in Tile worldTile, Vector3 direction) : base(location, worldTile, direction)
        {
        }
    }
}
