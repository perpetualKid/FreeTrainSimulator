using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Defines the geometry of a single track section piece: a straight or curved segment.
    /// </summary>
    /// <remarks>
    /// Derived from the <c>TrackSection</c> entries in the MSTS <c>tsection.dat</c> file.
    /// Straight sections are defined by <see cref="Length"/>; curved sections additionally
    /// specify <see cref="Radius"/> and <see cref="Angle"/>.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackSection
    {
        /// <summary>Unique section index, used as key in <see cref="TrackSectionModel.TrackSections"/>.</summary>
        public int SectionIndex { get; init; }

        /// <summary>Track gauge in meters.</summary>
        public float Gauge { get; init; }

        /// <summary>Length of this section in meters. For curved sections this is the arc length.</summary>
        public float Length { get; init; }

        /// <summary>Whether this section is curved. When <see langword="false"/>, it is a straight segment.</summary>
        public bool Curved { get; init; }

        /// <summary>Curve radius in meters. Only meaningful when <see cref="Curved"/> is <see langword="true"/>.</summary>
        public float Radius { get; init; }

        /// <summary>Curve angle in degrees. Only meaningful when <see cref="Curved"/> is <see langword="true"/>.</summary>
        public float Angle { get; init; }
    }
}
