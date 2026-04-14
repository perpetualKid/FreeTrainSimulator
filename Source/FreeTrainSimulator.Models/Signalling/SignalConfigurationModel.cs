using System;
using System.Collections.Immutable;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signalling
{
    /// <summary>
    /// Pre-processed model of a route's signal configuration, combining all signal types,
    /// shapes, light textures, and scripting references into a MemoryPack-serializable container.
    /// Serialized with the <c>.sigcfg</c> file extension.
    /// </summary>
    /// <remarks>
    /// Abstracts the MSTS <c>sigcfg.dat</c> and associated signal script files.
    /// String keys use case-insensitive comparison to match MSTS semantics.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".sigcfg")]
    public partial record SignalConfigurationModel: ModelBase
    {
        /// <inheritdoc/>
        public override RouteModel Parent => _parent as RouteModel;

        /// <summary>Light texture definitions used for rendering signal lights, keyed by texture name.</summary>
        public ImmutableDictionary<string, SignalLightTexture> LightTextures { get; init; } = ImmutableDictionary<string, SignalLightTexture>.Empty;

        /// <summary>Signal type definitions describing signal head behavior, keyed by type name.</summary>
        public ImmutableDictionary<string, SignalType> SignalTypes { get; init; } = ImmutableDictionary<string, SignalType>.Empty;

        /// <summary>Signal shape definitions describing physical signal mast arrangements, keyed by shape name.</summary>
        public ImmutableDictionary<string, SignalShape> SignalShapes { get; init; } = ImmutableDictionary<string, SignalShape>.Empty;

        /// <summary>Paths to signal script files (<c>.cs</c>) that define custom signal logic.</summary>
        public ImmutableArray<string> ScriptFiles { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>Custom (non-MSTS) signal function type names in registration order, used to reconstruct the <see cref="SignalTypeRegistry"/> at runtime.</summary>
        public ImmutableArray<string> CustomFunctionTypes { get; init; } = ImmutableArray<string>.Empty;
        /// <summary>Custom normal subtype names in registration order, used to reconstruct the <see cref="SignalTypeRegistry"/> at runtime.</summary>
        public ImmutableArray<string> CustomNormalSubTypes { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// MemoryPack deserialization does not preserve ImmutableDictionary key comparers.
        /// Rebuild with OrdinalIgnoreCase to match case-insensitive MSTS signal configuration semantics.
        /// </summary>
        [MemoryPackOnDeserialized]
        private static void OnDeserialized(ref MemoryPackReader _, ref SignalConfigurationModel instance)
        {
            if (instance is null)
                return;

            instance = instance with
            {
                LightTextures = instance.LightTextures.WithComparers(StringComparer.OrdinalIgnoreCase),
                SignalTypes = instance.SignalTypes.WithComparers(StringComparer.OrdinalIgnoreCase),
                SignalShapes = instance.SignalShapes.WithComparers(StringComparer.OrdinalIgnoreCase),
            };
        }
    }
}
