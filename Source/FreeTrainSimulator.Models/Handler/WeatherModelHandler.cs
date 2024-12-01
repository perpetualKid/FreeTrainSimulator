using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class WeatherModelHandler : ContentHandlerBase<WeatherModelCore>
    {
        public static Task<WeatherModelCore> GetCore(WeatherModelCore weatherModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(weatherModel, nameof(weatherModel));
            return GetCore(weatherModel.Id, weatherModel.Parent, cancellationToken);
        }

        public static Task<WeatherModelCore> GetCore(string weatherId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(weatherId);

            if (!modelTaskCache.TryGetValue(key, out Task<WeatherModelCore> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(weatherId, routeModel, cancellationToken);
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static Task<FrozenSet<WeatherModelCore>> GetWeatherFiles(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<FrozenSet<WeatherModelCore>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadWeatherModels(routeModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<FrozenSet<WeatherModelCore>> LoadWeatherModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            string weatherFolder = ModelFileResolver<WeatherModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<WeatherModelCore>.WildcardSavePattern;

            ConcurrentBag<WeatherModelCore> results = new ConcurrentBag<WeatherModelCore>();

            //load existing weather models, and compare if the corresponding folder still exists.
            if (Directory.Exists(weatherFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(weatherFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string weatherId = Path.GetFileNameWithoutExtension(file);

                    if (weatherId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        weatherId = weatherId[..^fileExtension.Length];

                    WeatherModelCore path = await GetCore(weatherId, routeModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }
    }
}
