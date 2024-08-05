using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable]
    public sealed partial record ContentRouteModel: ModelBase<ContentRouteModel>
    {
        private readonly WorldLocation routeStart;

        //public override string FileExtension { get; init; } = ".route";
        public string RouteId { get; init; }
        public string Description { get; init; }
        public ref readonly WorldLocation RouteStart => ref routeStart;
        public bool MetricUnits { get; init; }
        public EnumArray2D<string, SeasonType, WeatherType> EnvironmentConditions { get; init; }

        [MemoryPackIgnore]
        public string Path { get; set; }
        [MemoryPackIgnore]
        public new string FileName => System.IO.Path.GetFileName(Path);

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
