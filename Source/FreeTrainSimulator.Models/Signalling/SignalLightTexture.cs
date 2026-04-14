using MemoryPack;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Defines a single light texture used as background to draw lit lights onto signals.
    /// </summary>
    /// <remarks>
    /// Derived from the <c>LightTex</c> entries in the MSTS <c>sigcfg.dat</c> file.
    /// The UV coordinates define the sub-rectangle within the texture atlas for this light.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalLightTexture
    {
        /// <summary>Name used to reference this texture from <see cref="SignalType.LightTexture"/>.</summary>
        public string Name { get; init; }

        /// <summary>File name of the texture image.</summary>
        public string TextureFile { get; init; }

        /// <summary>Left edge U coordinate (0.0–1.0) in the texture atlas.</summary>
        public float U0 { get; init; }

        /// <summary>Top edge V coordinate (0.0–1.0) in the texture atlas.</summary>
        public float V0 { get; init; }

        /// <summary>Right edge U coordinate (0.0–1.0) in the texture atlas.</summary>
        public float U1 { get; init; }

        /// <summary>Bottom edge V coordinate (0.0–1.0) in the texture atlas.</summary>
        public float V1 { get; init; }

    }
}
