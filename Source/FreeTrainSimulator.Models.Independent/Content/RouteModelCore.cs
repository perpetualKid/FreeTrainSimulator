using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Independent.Base;

namespace FreeTrainSimulator.Models.Independent.Content
{
    public abstract record RouteModelCore: ModelBase<RouteModelCore>
    {
#pragma warning disable CA1810 // Initialize reference type static fields inline
        static RouteModelCore()
#pragma warning restore CA1810 // Initialize reference type static fields inline
        {
            fileExtension = ".contentroute";
        }

        private readonly WorldLocation routeStart;

        public string RouteId { get; init; }
        public string Description { get; init; }
        public ref readonly WorldLocation RouteStart => ref routeStart;
        public bool MetricUnits { get; init; }

        protected RouteModelCore(in WorldLocation routeStart)
        { 
            this.routeStart = routeStart;
        }
    }
}
