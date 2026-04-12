using System.Collections.Immutable;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Signal
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
    }
}
