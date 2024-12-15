using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Weather", ".weather")]
    public sealed partial record WeatherModelCore : ModelBase
    {
        public override RouteModelCore Parent => _parent as RouteModelCore;
    }
}
