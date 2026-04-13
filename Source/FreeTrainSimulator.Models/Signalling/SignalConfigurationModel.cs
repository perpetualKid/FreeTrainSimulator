using System;
using System.Collections.Immutable;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signalling
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".sigcfg")]
    public partial record SignalConfigurationModel: ModelBase
    {
        public override RouteModel Parent => _parent as RouteModel;

        public ImmutableDictionary<string, SignalLightTexture> LightTextures { get; init; } = ImmutableDictionary<string, SignalLightTexture>.Empty;
        public ImmutableDictionary<string, SignalType> SignalTypes { get; init; } = ImmutableDictionary<string, SignalType>.Empty;
        public ImmutableDictionary<string, SignalShape> SignalShapes { get; init; } = ImmutableDictionary<string, SignalShape>.Empty;

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
