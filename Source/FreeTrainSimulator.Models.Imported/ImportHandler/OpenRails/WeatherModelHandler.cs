﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.Shim;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.OpenRails
{
    internal sealed class WeatherModelHandler : ContentHandlerBase<WeatherModelHeader>
    {
        internal const string SourceNameKey = "ORSourceWeather";

        public static async Task<ImmutableArray<WeatherModelHeader>> ExpandPathModels(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            ConcurrentBag<WeatherModelHeader> results = new ConcurrentBag<WeatherModelHeader>();

            string sourceFolder = routeModel.MstsRouteFolder().WeatherFolder;
            if (Directory.Exists(sourceFolder))
            {
                // load existing OR weather files
                ConcurrentBag<string> pathFiles = new ConcurrentBag<string>(Directory.EnumerateFiles(sourceFolder, "*.weather-or"));

                await Parallel.ForEachAsync(pathFiles, cancellationToken, async (path, token) =>
                {
                    Task<WeatherModelHeader> modelTask = Cast(Convert(path, routeModel, cancellationToken));

                    WeatherModelHeader weatherModel = await modelTask.ConfigureAwait(false);
                    string key = weatherModel.Hierarchy();
                    results.Add(weatherModel);
                    modelTaskCache[key] = modelTask;
                }).ConfigureAwait(false);
            }
            ImmutableArray<WeatherModelHeader> result = results.ToImmutableArray();
            string key = routeModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            _ = collectionUpdateRequired.TryRemove(key, out _);
            return result;
        }

        private static async Task<WeatherModelHeader> Convert(string filePath, RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (File.Exists(filePath))
            {
                string id = Path.GetFileNameWithoutExtension(filePath);
                //inelegant but works - split into separate words, bound by _ and - separators as well uppercase char inside as in camelCase, and convert all words to Title Case
                id = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(new string(id.Replace('_', ' ').Replace('-', ' ').SelectMany((c, i) => i != 0 && char.IsUpper(c) && !char.IsUpper(id[i - 1]) ? new char[] { ' ', c } : new char[] { c }).ToArray()));
                WeatherModelHeader weatherModel = new WeatherModelHeader()
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
