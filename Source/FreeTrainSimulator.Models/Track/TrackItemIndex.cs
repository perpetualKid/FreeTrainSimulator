using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// An ordered list of track item indices that belong to a particular track node.
    /// </summary>
    /// <remarks>
    /// Corresponds to the <c>TrItemRef</c> entries on a <c>TrackNode</c> in the MSTS <c>.tdb</c> file.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackItemIndex
    {
        /// <summary>Ordered indices into <see cref="TrackDatabase.TrackItems"/> for items on this node.</summary>
        public ImmutableArray<int> TrackItems { get; init; } = ImmutableArray<int>.Empty;
    }
}
