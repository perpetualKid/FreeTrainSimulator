using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Positional and angular offset of a <see cref="TrackShapePath"/>'s start relative to its shape origin.
    /// </summary>
    /// <remarks>
    /// Derived from the offset portion of <c>SectionIdx</c> entries in the MSTS <c>tsection.dat</c> file.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackShapeOffset
    {
        private readonly Vector3 offset;

        /// <summary>3D position offset (x, y, z) from the shape origin, in meters.</summary>
        public ref readonly Vector3 Offset => ref offset;

        /// <summary>Angular offset about the Y-axis from the shape origin, in radians.</summary>
        public float AngularOffset { get; init; }

        [MemoryPackConstructor]
        public TrackShapeOffset(in Vector3 offset, float angularOffset)
        {
            this.offset = offset;
            this.AngularOffset = angularOffset;
        }

    }
}
