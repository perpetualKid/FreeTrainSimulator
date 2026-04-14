using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// A path through a <see cref="TrackShape"/>, defined as an ordered sequence of
    /// <see cref="TrackSection"/> indices with an optional positional offset from the shape origin.
    /// </summary>
    /// <remarks>
    /// Derived from the <c>SectionIdx</c> entries in the MSTS <c>tsection.dat</c> file.
    /// Multi-track shapes (e.g. double-track tunnels) have multiple paths.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackShapePath
    {
        /// <summary>Ordered list of <see cref="TrackSection.SectionIndex"/> values composing this path.</summary>
        public ImmutableArray<int> TrackSections { get; init; } = ImmutableArray<int>.Empty;

        /// <summary>Positional and angular offset of this path's start from the shape origin.
        /// May be <see langword="null"/> when no offset applies.</summary>
        public TrackShapeOffset ShapeOffset { get; init; }
    }
}
