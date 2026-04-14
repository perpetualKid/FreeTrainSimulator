using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Header model for a custom weather configuration associated with a route.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Weather", ".weather")]
    public sealed partial record WeatherModelHeader : ModelBase
    {
        /// <inheritdoc/>
        public override RouteModelHeader Parent => _parent as RouteModelHeader;
    }
}
