using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class WeatherModelHandler : ContentHandlerBase<WeatherModelHeader>
    {
        public static Task<WeatherModelHeader> GetCore(WeatherModelHeader weatherModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(weatherModel, nameof(weatherModel));
            return GetCore(weatherModel.Id, weatherModel.Parent, cancellationToken);
        }

        public static Task<WeatherModelHeader> GetCore(string weatherId, RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(weatherId);

            if (!modelTaskCache.TryGetValue(key, out Task<WeatherModelHeader> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(weatherId, routeModel, cancellationToken);
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static Task<ImmutableArray<WeatherModelHeader>> GetWeatherFiles(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<WeatherModelHeader>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadWeatherModels(routeModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<WeatherModelHeader>> LoadWeatherModels(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            string weatherFolder = ModelFileResolver<WeatherModelHeader>.FolderPath(routeModel);
            string pattern = ModelFileResolver<WeatherModelHeader>.WildcardSavePattern;

            ConcurrentBag<WeatherModelHeader> results = new ConcurrentBag<WeatherModelHeader>();

            //load existing weather models, and compare if the corresponding folder still exists.
            if (Directory.Exists(weatherFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(weatherFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string weatherId = Path.GetFileNameWithoutExtension(file);

                    if (weatherId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        weatherId = weatherId[..^fileExtension.Length];

                    WeatherModelHeader path = await GetCore(weatherId, routeModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.ToImmutableArray();
        }
    }
}
