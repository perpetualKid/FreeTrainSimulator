using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Full route model extending <see cref="RouteModelHeader"/> with detailed environment,
    /// sound, speed restriction, and infrastructure configuration.
    /// Abstracts data from MSTS route (<c>.trk</c>) and environment files.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record RouteModel : RouteModelHeader
    {
        /// <summary>Environment sound/texture file names indexed by season and weather combination.</summary>
        public EnumArray2D<string, SeasonType, WeatherType> EnvironmentConditions { get; init; }
        /// <summary>Unique key identifying this route within the content folder.</summary>
        public string RouteKey { get; init; }

        /// <summary>Default sound file names indexed by <see cref="DefaultSoundType"/>.</summary>
        public EnumArray<string, DefaultSoundType> RouteSounds { get; init; }

        /// <summary>Infrastructure conditions for this route (track gauge, electrification, etc.).</summary>
        public RouteConditionModel RouteConditions { get; init; }

        /// <summary>Global and temporary speed limits in meters per second, indexed by restriction type.</summary>
        public EnumArray<float, SpeedRestrictionType> SpeedRestrictions { get; init; }

        /// <summary>Arbitrary key/value settings extracted from the route file.</summary>
        public ImmutableDictionary<string, string> Settings { get; init; } = ImmutableDictionary<string, string>.Empty;

        /// <summary>Interpolation table mapping curve radius to super-elevation values.</summary>
        public Interpolator SuperElevationRadiusSettings { get; init; }

        public RouteModel(in WorldLocation routeStart) : base(routeStart)
        {
        }
    }
}