using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Describes a light on a signal: location, colorm size.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalLight
    {
        private readonly Vector3 position;
        private readonly Color color;

        public string Name { get; init; }
        public ref readonly Color Color => ref color;
        public ref readonly Vector3 Position => ref position;
        public float Radius { get; init; }
        public bool SemaphoreChange { get; init; }

        public SignalLight(in Vector3 position, in Color color)
        {
            this.position = position;
            this.color = color;
        }
    }
}
