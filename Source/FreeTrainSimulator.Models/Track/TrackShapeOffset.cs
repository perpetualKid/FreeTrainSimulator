using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackShapeOffset
    {
        private readonly Vector3 offset;
        public ref readonly Vector3 Offset => ref offset; // Vector Offset of section start from shape origin (x, y, z),
        public float AngularOffset { get; init; }   // Angle offset of section start from shape origin (about y axis),

        [MemoryPackConstructor]
        public TrackShapeOffset(in Vector3 offset, float angularOffset)
        {
            this.offset = offset;
            this.AngularOffset = angularOffset;
        }

    }
}
