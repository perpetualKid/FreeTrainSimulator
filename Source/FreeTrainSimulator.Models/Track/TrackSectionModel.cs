using System.Collections.Immutable;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Pre-processed model of the global and route-specific track section definitions.
    /// Serialized with the <c>.tsection</c> file extension.
    /// </summary>
    /// <remarks>
    /// Abstracts the MSTS <c>tsection.dat</c> file which defines the geometry of every track section piece
    /// (straight/curved segments), the 3D shapes used for track rendering, and the paths through those shapes.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".tsection")]
    public sealed partial record TrackSectionModel: ModelBase
    {
        /// <inheritdoc/>
        public override RouteModel Parent => _parent as RouteModel;

        /// <summary>All track section definitions, keyed by section index.
        /// Each section describes a straight or curved segment's geometry.</summary>
        public ImmutableDictionary<int, TrackSection> TrackSections { get; init; } = ImmutableDictionary<int, TrackSection>.Empty;

        /// <summary>Track shape definitions, keyed by shape index.
        /// Each shape describes a 3D track piece with its type (normal, tunnel, road) and clearance.</summary>
        public ImmutableDictionary<int, TrackShape> TrackShapes { get; init; } = ImmutableDictionary<int, TrackShape>.Empty;

        /// <summary>Paths through each track shape, keyed by shape index.
        /// Each path is an ordered sequence of <see cref="TrackSection"/> indices forming a route through the shape.
        /// Multiple paths per shape represent multi-track shapes (e.g. double-track tunnels).</summary>
        public ImmutableDictionary<int, ImmutableArray<TrackShapePath>> TrackShapePaths { get; init; } = ImmutableDictionary<int, ImmutableArray<TrackShapePath>>.Empty;
    }
}
