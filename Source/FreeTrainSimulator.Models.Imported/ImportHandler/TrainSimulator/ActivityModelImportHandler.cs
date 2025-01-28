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
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.Shim;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal sealed class ActivityModelImportHandler : ContentHandlerBase<ActivityModelCore>
    {
        internal const string SourceNameKey = "MstsSourceActivity";

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

            FrozenSet<ActivityModelCore> result = results.Concat(new ActivityModelCore[] { CommonModelInstances.ExploreMode, CommonModelInstances.ExploreActivityMode }).ToFrozenSet();
            string key = routeModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            _ = collectionUpdateRequired.TryRemove(key, out _);
            return result;
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

                // use average of both probabilities > 0, else use only the one which is > 0, else 0
                static int CombinedHazardProbability(int workers, int animals)
                {
                    if (workers > 0 && animals > 0)
                        return (workers + animals) / 2;
                    else if (workers > 0)
                        return workers;
                    else if (animals > 0)
                        return animals;
                    else
                        return 0;
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
                    FuelLevels = new EnumArray<int, FuelType>((FuelType fuelType) => fuelType switch
                    {
                        FuelType.Water => activityFile.Activity.Header.FuelWater,
                        FuelType.Coal => activityFile.Activity.Header.FuelCoal,
                        FuelType.Diesel => activityFile.Activity.Header.FuelDiesel,
                        _ => throw new NotImplementedException(),
                    }),
                    InitialSpeed = activityFile.Activity.Header.StartingSpeed,
                    HazardProbability = CombinedHazardProbability(activityFile.Activity.Header.Workers, activityFile.Activity.Header.Animals)
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
