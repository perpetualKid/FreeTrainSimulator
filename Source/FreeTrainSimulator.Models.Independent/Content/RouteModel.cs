using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record RouteModel : RouteModelCore
    {
        public EnumArray2D<string, SeasonType, WeatherType> EnvironmentConditions { get; init; }
        public string RouteKey { get; init;}

        public RouteModel(in WorldLocation routeStart) : base(routeStart)
        {
        }
    }
}