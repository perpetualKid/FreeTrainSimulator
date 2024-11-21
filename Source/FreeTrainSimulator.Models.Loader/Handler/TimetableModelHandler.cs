using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.OR.Files;
using Orts.Formats.OR.Models;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal class TimetableModelHandler : ContentHandlerBase<TimetableModel>
    {
        internal const string SourceNameKey = "OrSourceRoute";

        public static ValueTask<TimetableModel> GetCore(TimetableModel timetableModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(timetableModel, nameof(timetableModel));
            return GetCore(timetableModel.Id, timetableModel.Parent, cancellationToken);
        }

        public static async ValueTask<TimetableModel> GetCore(string timetableId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(timetableId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<TimetableModel>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<TimetableModel>>(FromFile(timetableId, routeModel, cancellationToken));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            TimetableModel timetableModel = await modelTask.Value.ConfigureAwait(false);

            if (timetableModel?.RefreshRequired ?? false)
            {
                taskLazyCache[key] = new Lazy<Task<TimetableModel>>(() => Cast(Convert(timetableModel, cancellationToken)));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return timetableModel;
        }

        public static async ValueTask<FrozenSet<TimetableModel>> GetTimetables(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !taskLazyCollectionCache.TryGetValue(key, out Lazy<Task<FrozenSet<TimetableModel>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<TimetableModel>>>(() => LoadTimetables(routeModel, cancellationToken));
            }

            return await modelSetTask.Value.ConfigureAwait(false);
        }

        public static async Task<FrozenSet<TimetableModel>> ExpandTimetableModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string timetablesFolder = ModelFileResolver<TimetableModel>.FolderPath(routeModel);
            string pattern = ModelFileResolver<TimetableModel>.WildcardPattern;

            ConcurrentBag<TimetableModel> results = new ConcurrentBag<TimetableModel>();

            string sourceFolder = routeModel.MstsRouteFolder().OpenRailsActivitiesFolder;

            if (Directory.Exists(sourceFolder))
            {
                // load existing MSTS files
                ConcurrentBag<string> consistFiles = new ConcurrentBag<string>(Directory.EnumerateFiles(sourceFolder, "*.timetable*or"));

                await Parallel.ForEachAsync(consistFiles, cancellationToken, async (consistFile, token) =>
                {
                    Lazy<Task<TimetableModel>> modelTask = new Lazy<Task<TimetableModel>>(Convert(consistFile, routeModel, cancellationToken));

                    TimetableModel timetableModel = await modelTask.Value.ConfigureAwait(false);
                    string key = timetableModel.Hierarchy();
                    results.Add(timetableModel);
                    taskLazyCache[key] = modelTask;
                }).ConfigureAwait(false);
            }
            FrozenSet<TimetableModel> result = results.ToFrozenSet();
            string key = routeModel.Hierarchy();
            taskLazyCollectionCache[key] = new Lazy<Task<FrozenSet<TimetableModel>>>(Task.FromResult(result));
            _ = collectionUpdateRequired.TryRemove(key, out _);
            return result;
        }

        private static async Task<FrozenSet<TimetableModel>> LoadTimetables(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            string timetablesFolder = ModelFileResolver<TimetableModel>.FolderPath(routeModel);
            string pattern = ModelFileResolver<TimetableModel>.WildcardSavePattern;

            ConcurrentBag<TimetableModel> results = new ConcurrentBag<TimetableModel>();

            //load existing path models, and compare if the corresponding folder still exists.
            if (Directory.Exists(timetablesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(timetablesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string timetableId = Path.GetFileNameWithoutExtension(file);

                    if (timetableId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        timetableId = timetableId[..^fileExtension.Length];

                    TimetableModel timetable = await GetCore(timetableId, routeModel, token).ConfigureAwait(false);
                    if (null != timetable)
                        results.Add(timetable);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }

        private static Task<TimetableModel> Convert(TimetableModel timetableModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(timetableModel, nameof(timetableModel));

            return Convert(Path.Combine(timetableModel.Parent.MstsRouteFolder().OpenRailsActivitiesFolder, timetableModel.Tags[SourceNameKey]), timetableModel.Parent, cancellationToken);
        }

        private static async Task<TimetableModel> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            static IEnumerable<TimetableTrainModel> BuildTrains(IEnumerable<TrainInformation> trains, string group)
            {
                return trains.Select(t => new TimetableTrainModel()
                {
                    Id = t.Train,
                    Name = t.Train,
                    Briefing = t.Briefing,
                    Group = group,
                    Path = t.Path,
                    WagonSet = t.LeadingConsist,
                    WagonSetReverse = t.ReverseConsist,
                    StartTime = TimeOnly.Parse(t.StartTime.Max(5)),
                });
            }

            if (File.Exists(filePath))
            {
                TimetableModel timetableModel;

                if (Path.GetExtension(filePath).StartsWith("timetablelist", StringComparison.OrdinalIgnoreCase))
                {
                    TimetableGroupFile groupFile = new TimetableGroupFile(filePath);
                    timetableModel = new TimetableModel()
                    {
                        Id = Path.GetFileNameWithoutExtension(filePath),
                        Name = groupFile.Description,
                        TimetableTrains = groupFile.TimeTables.SelectMany(timetable => BuildTrains(timetable.Trains, timetable.Description))?.ToFrozenSet() ?? FrozenSet<TimetableTrainModel>.Empty,
                        Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileNameWithoutExtension(filePath) } },
                    };
                }
                else
                {
                    TimetableFile timetableFile = new TimetableFile(filePath);

                    timetableModel = new TimetableModel()
                    {
                        Id = Path.GetFileNameWithoutExtension(filePath),
                        Name = timetableFile.Description,
                        TimetableTrains = BuildTrains(timetableFile.Trains, timetableFile.Description)?.ToFrozenSet() ?? FrozenSet<TimetableTrainModel>.Empty,
                        Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileNameWithoutExtension(filePath) } },
                    };
                }

                await Create(timetableModel, routeModel, cancellationToken).ConfigureAwait(false);
                return timetableModel;
            }
            else
            {
                Trace.TraceWarning($"Consist file {filePath} refers to non-existing file.");
                return null;
            }
        }

    }
}
