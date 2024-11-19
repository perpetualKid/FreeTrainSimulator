using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record WeatherModelCore : ModelBase<WeatherModelCore>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".weather";
            subFolder = "Weather";
        }

        public override RouteModelCore Parent => _parent as RouteModelCore;
    }
}
