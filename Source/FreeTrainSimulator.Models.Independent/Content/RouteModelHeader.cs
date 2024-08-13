using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record RouteModelHeader : RouteModelCore
    {
        [MemoryPackConstructor]
        public RouteModelHeader(in WorldLocation routeStart) : base(routeStart)
        {
        }

        public RouteModelHeader(RouteModelCore routeModel) : base(routeModel.RouteStart)
        {
            Name = routeModel.Name;
            Version = routeModel.Version;
            Tag = routeModel.Tag;
            RouteId = routeModel.RouteId;
            Description = routeModel.Description;
            MetricUnits = routeModel.MetricUnits;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
