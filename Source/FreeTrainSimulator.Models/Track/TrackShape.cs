using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Describes a 3D track shape used for rendering track sections.
    /// A shape may represent normal track, a tunnel, or a road surface.
    /// </summary>
    /// <remarks>
    /// Derived from the <c>TrackShape</c> entries in the MSTS <c>tsection.dat</c> file.
    /// The shape's geometry paths are stored separately in <see cref="TrackSectionModel.TrackShapePaths"/>,
    /// keyed by <see cref="ShapeIndex"/>.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackShape
    {
        /// <summary>Unique shape index, used as key in <see cref="TrackSectionModel.TrackShapes"/>.</summary>
        public int ShapeIndex { get; init; }

        /// <summary>File name of the 3D shape model (<c>.s</c> file). May be <see langword="null"/> for shapes
        /// without a visual representation.</summary>
        public string FileName { get; init; }

        /// <summary>Index of the main (through) route path within this shape.</summary>
        public int MainRoute { get; init; }

        /// <summary>Minimum clearance distance in meters (used primarily for junction shapes).</summary>
        public float ClearanceDistance { get; init; }

        /// <summary>Classification of this shape (normal track, tunnel, or road).</summary>
        public ShapeType ShapeType { get; init; }
    }
}
