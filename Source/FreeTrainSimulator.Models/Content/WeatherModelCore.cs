using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record WeatherModelCore : ModelBase<WeatherModelCore>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".weather";
            subFolder = "Weather";
        }

        public override RouteModelCore Parent => (this as IFileResolve).Container as RouteModelCore;
    }
}
