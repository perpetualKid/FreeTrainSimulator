using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable]
    public sealed partial record ContentRouteModel: ModelBase<ContentRouteModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".contentroute";
        }

        private readonly WorldLocation routeStart;

        public string RouteId { get; init; }
        public string Description { get; init; }
        public ref readonly WorldLocation RouteStart => ref routeStart;
        public bool MetricUnits { get; init; }
        public EnumArray2D<string, SeasonType, WeatherType> EnvironmentConditions { get; init; }

        public ContentRouteModel(in WorldLocation routeStart)
        { 
            this.routeStart = routeStart;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
