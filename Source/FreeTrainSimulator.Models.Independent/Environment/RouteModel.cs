﻿using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Environment
{
    [MemoryPackable]
    public sealed partial class RouteModel: ModelBase
    {
        private readonly WorldLocation routeStart;

        public const string Extension = ".route";
        public string RouteName { get; init; }
        public string RouteId { get; init; }
        public string Description { get; init; }
        public ref readonly WorldLocation RouteStart => ref routeStart;
        public bool MetricUnits { get; init; }
        public EnumArray2D<string, SeasonType, WeatherType> EnvironmentConditions { get; init; }

        [MemoryPackIgnore]
        public string Path { get; set; }

        public RouteModel(in WorldLocation routeStart)
        {
            this.routeStart = routeStart;
        }

        public override string ToString()
        {
            return RouteName;
        }
    }
}