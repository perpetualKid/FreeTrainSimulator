using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Handler for weather change models. Loads individual <see cref="WeatherModelHeader"/>
    /// instances from disk and enumerates all weather-change files available for a route.
    /// </summary>
    internal sealed class WeatherModelHandler : ContentHandlerBase<WeatherModelHeader>
    {
        public static Task<WeatherModelHeader> GetCore(WeatherModelHeader weatherModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(weatherModel, nameof(weatherModel));
            return GetCore(weatherModel.Id, weatherModel.Parent, cancellationToken);
        }

        public static Task<WeatherModelHeader> GetCore(string weatherId, RouteModelHeader routeModel, CancellationToken cancellationToken)
            => GetOrAddCore(weatherId, routeModel, cancellationToken);

        public static Task<ImmutableArray<WeatherModelHeader>> GetWeatherFiles(RouteModelHeader routeModel, CancellationToken cancellationToken)
            => GetOrAddCollection(routeModel, cancellationToken);
    }
}
