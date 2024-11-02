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

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class ActivityModelHandler : ContentHandlerBase<ActivityModelCore>
    {
        internal const string SourceNameKey = "MstsSourceActivity";

        public static ActivityModelCore ExploreMode { get; private set; } = new ActivityModelCore()
        {
            ActivityType = ActivityType.Explorer,
            Name = "- Explore Route -",
            Id = "- Explore Route -",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static ActivityModelCore ExploreActivityMode { get; private set; } = new ActivityModelCore()
        {
            ActivityType = ActivityType.ExploreActivity,
            Name = "+ Explore in Activity Mode +",
            Id = "+ Explore in Activity Mode +",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static ValueTask<ActivityModelCore> GetCore(ActivityModelCore activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return GetCore(activityModel.Id, activityModel.Parent, cancellationToken);
        }

        public static async ValueTask<ActivityModelCore> GetCore(string activityId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<ActivityModelCore>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<ActivityModelCore>>(FromFile(activityId, routeModel, cancellationToken));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            ActivityModelCore activityModel = await modelTask.Value.ConfigureAwait(false);

            if (activityModel?.RefreshRequired ?? false)
            {
                taskLazyCache[key] = new Lazy<Task<ActivityModelCore>>(() => Cast(Convert(activityModel, cancellationToken)));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return activityModel;
        }

        public static ValueTask<ActivityModel> GetExtended(ActivityModelCore activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return activityModel is ActivityModel activityModelExtended ? ValueTask.FromResult(activityModelExtended) : GetExtended(activityModel.Id, activityModel.Parent, cancellationToken);
        }

        public static async ValueTask<ActivityModel> GetExtended(string activityId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<ActivityModelCore>> modelTask) || !modelTask.IsValueCreated ||
                (modelTask.IsValueCreated && (modelTask.Value.IsFaulted || (await modelTask.Value.ConfigureAwait(false) is not ActivityModel))))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<ActivityModelCore>>(Cast(FromFile<ActivityModel, RouteModelCore>(activityId, routeModel, cancellationToken)));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            ActivityModel activityModel = await modelTask.Value.ConfigureAwait(false) as ActivityModel;

            if (activityModel?.RefreshRequired ?? false)
            {
                taskLazyCache[key] = new Lazy<Task<ActivityModelCore>>(() => Cast(Convert(activityModel, cancellationToken)));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return activityModel;
        }

        public static async ValueTask<FrozenSet<ActivityModelCore>> GetActivities(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !taskLazyCollectionCache.TryGetValue(key, out Lazy<Task<FrozenSet<ActivityModelCore>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<ActivityModelCore>>>(() => LoadActivities(routeModel, cancellationToken));
            }

            return await modelSetTask.Value.ConfigureAwait(false);
        }

        public static async Task<FrozenSet<ActivityModelCore>> ExpandActivityModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string activitiesFolder = ModelFileResolver<ActivityModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<ActivityModelCore>.WildcardSavePattern;

            ConcurrentBag<ActivityModelCore> results = new ConcurrentBag<ActivityModelCore>();

            string sourceFolder = routeModel.MstsRouteFolder().ActivitiesFolder;
            if (Directory.Exists(sourceFolder))
            {
                // load existing MSTS files
                ConcurrentDictionary<string, string> activityFiles = new ConcurrentDictionary<string, string>(Directory.EnumerateFiles(sourceFolder, "*.act").
                    ToDictionary(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

                //load existing activity models, and compare if the corresponding path file folder still exists.
                if (Directory.Exists(activitiesFolder))
                {
                    FrozenSet<ActivityModelCore> existingPaths = await GetActivities(routeModel, cancellationToken).ConfigureAwait(false);
                    foreach (ActivityModelCore activityModel in existingPaths)
                    {
                        if (activityFiles.Remove(activityModel?.Id, out string filePath)) //
                        {
                            results.Add(activityModel);
                        }
                    }
                }

                //for any new MSTS path (remaining in the preloaded dictionary), Create a path model
                await Parallel.ForEachAsync(activityFiles, cancellationToken, async (path, token) =>
                {
                    Lazy<Task<ActivityModelCore>> modelTask = new Lazy<Task<ActivityModelCore>>(Cast(Convert(path.Value, routeModel, cancellationToken)));
                    ActivityModelCore activityModel = await modelTask.Value.ConfigureAwait(false);
                    string key = activityModel.Hierarchy();
                    results.Add(activityModel);
                    taskLazyCache[key] = modelTask;
                }).ConfigureAwait(false);
            }

            FrozenSet<ActivityModelCore> result = results.Concat(new ActivityModelCore[] { ExploreMode, ExploreActivityMode }).ToFrozenSet();
            string key = routeModel.Hierarchy();
            Lazy<Task<FrozenSet<ActivityModelCore>>> modelSetTask;
            taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<ActivityModelCore>>>(Task.FromResult(result));
            return result;
        }

        private static async Task<FrozenSet<ActivityModelCore>> LoadActivities(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            string activiesFolder = ModelFileResolver<ActivityModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<ActivityModelCore>.WildcardSavePattern;

            ConcurrentBag<ActivityModelCore> results = new ConcurrentBag<ActivityModelCore>();

            //load existing activit models, and compare if the corresponding folder still exists.
            if (Directory.Exists(activiesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(activiesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string activityId = Path.GetFileNameWithoutExtension(file);

                    if (activityId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        activityId = activityId[..^fileExtension.Length];

                    ActivityModelCore path = await GetCore(activityId, routeModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.Concat(new ActivityModelCore[] { ExploreMode, ExploreActivityMode }).ToFrozenSet();
        }

        private static Task<ActivityModel> Convert(ActivityModelCore activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return Convert(activityModel.Parent.MstsRouteFolder().ActivityFile(activityModel.Tags[SourceNameKey]), activityModel.Parent, cancellationToken);
        }

        public static async Task<ActivityModel> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (File.Exists(filePath))
            {
                ActivityFile activityFile = new ActivityFile(filePath);

                ActivityModel activityModel = new ActivityModel()
                {
                    Id = Path.GetFileNameWithoutExtension(filePath),
                    Name = string.IsNullOrEmpty(activityFile.Activity.Header.Name) ?
                        $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : activityFile.Activity.Header.Name,
                    Description = activityFile.Activity.Header.Description,
                    Briefing = activityFile.Activity.Header.Briefing,
                    StartTime = TimeOnly.FromTimeSpan(activityFile.Activity.Header.StartTime),
                    Season = activityFile.Activity.Header.Season,
                    Weather = activityFile.Activity.Header.Weather,
                    Difficulty = activityFile.Activity.Header.Difficulty,
                    Duration = activityFile.Activity.Header.Duration,
                    ActivityType = ActivityType.Activity,
                    PathId = activityFile.Activity.Header.PathID,
                    Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileNameWithoutExtension(filePath) } },
                };

                await Create(activityModel, routeModel, cancellationToken).ConfigureAwait(false);
                return activityModel;
            }
            else
            {
                Trace.TraceWarning($"Activity file {filePath} refers to non-existing file.");
                return null;
            }
        }
    }
}
