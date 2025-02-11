using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record RouteModel : RouteModelCore
    {
        public EnumArray2D<string, SeasonType, WeatherType> EnvironmentConditions { get; init; }
        public string RouteKey { get; init; }

        public EnumArray<string, DefaultSoundType> RouteSounds { get; init; }

        public RouteConditionModel RouteConditions { get; init; }

        public EnumArray<float, SpeedRestrictionType> SpeedRestrictions { get; init; } // global and temporary speed limit m/s

        public ImmutableDictionary<string, string> Settings { get; init; } = ImmutableDictionary<string, string>.Empty; //arbitrary settings which are currently in route model but may not logically belong there

        public Interpolator SuperElevationRadiusSettings { get; init; }

        public RouteModel(in WorldLocation routeStart) : base(routeStart)
        {
        }
    }
}