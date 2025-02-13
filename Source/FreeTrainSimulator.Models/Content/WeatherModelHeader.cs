using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Weather", ".weather")]
    public sealed partial record WeatherModelHeader : ModelBase
    {
        public override RouteModelHeader Parent => _parent as RouteModelHeader;
    }
}
