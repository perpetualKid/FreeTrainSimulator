using System;

using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Models.Independent.Environment
{
    public sealed class RouteModel
    {
        private readonly WorldLocation routeStart;

        public string RouteName { get; init; }
        public string RouteId { get; init; }
        public string Version { get; init; }
        public string Description { get; init; }
        public ref readonly WorldLocation RouteStart => ref routeStart;
        public bool MetricUnits { get; init;}

        public RouteModel(in WorldLocation routeStart)
        { 
            this.routeStart = routeStart;
        }
    }
}
