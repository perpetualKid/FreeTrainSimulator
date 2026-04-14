using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Describes a light on a signal: location, color, and size.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalLight
    {
        private readonly Vector3 position;
        private readonly Color color;

        /// <summary>Display name of this light (corresponds to a sub-object name in the shape).</summary>
        public string Name { get; init; }

        /// <summary>Color of the light when lit.</summary>
        public ref readonly Color Color => ref color;

        /// <summary>3D position of the light relative to the signal shape origin.</summary>
        public ref readonly Vector3 Position => ref position;

        /// <summary>Radius of the light glow in meters.</summary>
        public float Radius { get; init; }

        /// <summary>Whether this light is visible only when the semaphore arm has changed position.</summary>
        public bool SemaphoreChange { get; init; }

        public SignalLight(in Vector3 position, in Color color)
        {
            this.position = position;
            this.color = color;
        }
    }
}
