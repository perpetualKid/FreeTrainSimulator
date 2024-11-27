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

        public static readonly ActivityModelCore ExploreMode = new ActivityModelCore()
        {
            ActivityType = ActivityType.Explorer,
            Name = "- Explore Route -",
            Id = "- Explore Route -",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static readonly ActivityModelCore ExploreActivityMode = new ActivityModelCore()
        {
            ActivityType = ActivityType.ExploreActivity,
            Name = "- Explore Route in Activity Mode -",
            Id = "- Explore Route in Activity Mode -",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static Task<ActivityModelCore> GetCore(ActivityModelCore activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return GetCore(activityModel.Id, activityModel.Parent, cancellationToken);
        }

        public static Task<ActivityModelCore> GetCore(string activityId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityId);

            if (!modelTaskCache.TryGetValue(key, out Task<ActivityModelCore> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(activityId, routeModel, cancellationToken);
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return modelTask;
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

            if (!modelTaskCache.TryGetValue(key, out Task<ActivityModelCore> modelTask) || (modelTask.IsFaulted || 
                (await modelTask.ConfigureAwait(false) is not ActivityModel)))
            {
                modelTaskCache[key] = modelTask = Cast(FromFile<ActivityModel, RouteModelCore>(activityId, routeModel, cancellationToken));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return await modelTask.ConfigureAwait(false) as ActivityModel;
        }

        public static Task<FrozenSet<ActivityModelCore>> GetActivities(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<FrozenSet<ActivityModelCore>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadActivities(routeModel, cancellationToken);
            }

            return modelSetTask;
        }

        public static async Task<FrozenSet<ActivityModelCore>> ExpandActivityModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            ConcurrentBag<ActivityModelCore> results = new ConcurrentBag<ActivityModelCore>();

            string sourceFolder = routeModel.MstsRouteFolder().ActivitiesFolder;
            if (Directory.Exists(sourceFolder))
            {
                // load existing MSTS files
                ConcurrentDictionary<string, string> activityFiles = new ConcurrentDictionary<string, string>(Directory.EnumerateFiles(sourceFolder, "*.act").
                    ToDictionary(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

                await Parallel.ForEachAsync(activityFiles, cancellationToken, async (path, token) =>
                {
                    Task<ActivityModelCore> modelTask = Cast(Convert(path.Value, routeModel, cancellationToken));
                    ActivityModelCore activityModel = await modelTask.ConfigureAwait(false);
                    if (null != activityModel)
                    {
                        string key = activityModel.Hierarchy();
                        results.Add(activityModel);
                        modelTaskCache[key] = modelTask;
                    }
                }).ConfigureAwait(false);
            }

            FrozenSet<ActivityModelCore> result = results.Concat(new ActivityModelCore[] { ExploreMode, ExploreActivityMode }).ToFrozenSet();
            string key = routeModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            _ = collectionUpdateRequired.TryRemove(key, out _);
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

        public static async Task<ActivityModel> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (File.Exists(filePath))
            {
                ActivityFile activityFile;
                try
                {
                    activityFile = new ActivityFile(filePath);
                }
                catch (Exception ex) when (ex is SystemException)
                {
                    Trace.TraceError($"Could not read activity file {filePath} with reason {ex.Message}.");
                    return null;
                }

                ServiceFile srvFile;
                try
                {
                    srvFile = new ServiceFile(routeModel.MstsRouteFolder().ServiceFile(activityFile.Activity.PlayerServices.Name));
                }
                catch (Exception ex) when (ex is SystemException)
                {
                    Trace.TraceError($"Could not read service file {filePath}  for activity {activityFile.Activity.Header.Name} with reason {ex.Message}.");
                    return null;
                }

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
                    ConsistId = srvFile.TrainConfig,
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
