using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class WeatherModelHandler: ContentHandlerBase<WeatherModelCore>
    {
        internal const string SourceNameKey = "ORSourceWeather";

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

        public static async Task<FrozenSet<WeatherModelCore>> ExpandPathModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            ConcurrentBag<WeatherModelCore> results = new ConcurrentBag<WeatherModelCore>();

            string sourceFolder = routeModel.MstsRouteFolder().WeatherFolder;
            if (Directory.Exists(sourceFolder))
            {
                // load existing OR weather files
                ConcurrentBag<string> pathFiles = new ConcurrentBag<string>(Directory.EnumerateFiles(sourceFolder, "*.weather-or"));

                await Parallel.ForEachAsync(pathFiles, cancellationToken, async (path, token) =>
                {
                    Task<WeatherModelCore> modelTask = Cast(Convert(path, routeModel, cancellationToken));

                    WeatherModelCore weatherModel = await modelTask.ConfigureAwait(false);
                    string key = weatherModel.Hierarchy();
                    results.Add(weatherModel);
                    modelTaskCache[key] = modelTask;
                }).ConfigureAwait(false);
            }
            FrozenSet<WeatherModelCore> result = results.ToFrozenSet();
            string key = routeModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            _ = collectionUpdateRequired.TryRemove(key, out _);
            return result;
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

        private static async Task<WeatherModelCore> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (File.Exists(filePath))
            {
                string id = Path.GetFileNameWithoutExtension(filePath);
                //inelegant but works - split into separate words, bound by _ and - separators as well uppercase char inside as in camelCase, and convert all words to Title Case
                id = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(new string(id.Replace('_', ' ').Replace('-', ' ').SelectMany((c, i) => i != 0 && char.IsUpper(c) && !char.IsUpper(id[i - 1]) ? new char[] { ' ', c } : new char[] { c }).ToArray()));
                WeatherModelCore weatherModel = new WeatherModelCore()
                {
                    Name = id,
                    Id = id,
                    Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileName(filePath) } },
                };
                await Create(weatherModel, routeModel, cancellationToken).ConfigureAwait(false);
                return weatherModel;
            }
            else
            {
                Trace.TraceWarning($"Weather file {filePath} refers to non-existing file.");
                return null;
            }
        }
    }
}
