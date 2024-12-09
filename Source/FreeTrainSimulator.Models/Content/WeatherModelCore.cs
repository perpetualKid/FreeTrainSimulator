using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record WeatherModelCore : ModelBase, IFileResolve
    {
        static string IFileResolve.SubFolder => "Weather";
        static string IFileResolve.DefaultExtension => ".weather";

        public override RouteModelCore Parent => _parent as RouteModelCore;
    }
}
