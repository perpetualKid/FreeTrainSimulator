using MemoryPack;

namespace FreeTrainSimulator.Models.Signal
{
    /// <summary>
    /// Defines a single light texture used as background to draw lit lights onto signals.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalLightTexture
    {
        public string Name { get; init; }
        public string TextureFile { get; init; }
        public float U0 { get; init; }
        public float V0 { get; init; }
        public float U1 { get; init; }
        public float V1 { get; init; }

    }
}
