using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Environment
{
    public abstract class RouteModelBase: ModelBase<RouteModelBase>
    {
        [MemoryPackInclude]
        private readonly WorldLocation routeStart;
        public string RouteId { get; init; }
        public string Description { get; init; }
        public ref readonly WorldLocation RouteStart => ref routeStart;
        public bool MetricUnits { get; init; }

        public RouteModelBase(in WorldLocation routeStart)
        {
            this.routeStart = routeStart;
        }

    }
}
