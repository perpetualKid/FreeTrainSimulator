using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
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
    internal sealed class ActivityModelHandler : ContentHandlerBase<ActivityModel, ActivityModelCore>
    {
        public static ActivityModelCore Explorer { get; private set; } = new ActivityModelCore()
        {
            ActivityType = ActivityType.Explorer,
            Name = "- Explore Route -",
            Id = "- Explore Route -",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static ActivityModelCore ExploreActivity { get; private set; } = new ActivityModelCore()
        {
            ActivityType = ActivityType.ExploreActivity,
            Name = "+ Explore in Activity Mode +",
            Id = "+ Explore in Activity Mode +",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static async ValueTask<ActivityModel> Get(string name, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            return await FromFile(name, routeModel, cancellationToken).ConfigureAwait(false);
        }
        public static async Task<ActivityModel> Convert(ActivityModelCore activityModel, CancellationToken cancellationToken)
        {
            return await Convert(activityModel.Id, activityModel.Parent, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ActivityModel> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
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
                };

                await Create(activityModel, routeModel, cancellationToken).ConfigureAwait(false);
                return activityModel;
            }
            return null;
        }

        public static async ValueTask<FrozenSet<ActivityModelCore>> ConvertActivityModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string activitiesFolder = ModelFileResolver<ActivityModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<ActivityModelCore>.WildcardPattern;

            ConcurrentBag<ActivityModelCore> results = new ConcurrentBag<ActivityModelCore>();
            // preload existing MSTS files
            ConcurrentDictionary<string, string> activityFiles = new ConcurrentDictionary<string, string>(Directory.EnumerateFiles(routeModel.MstsRouteFolder().ActivitiesFolder, "*.act").
                ToDictionary(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

            //load existing route models, and compare if the corresponding folder still exists.
            if (Directory.Exists(activitiesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(activitiesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    ActivityModelCore activityModel = await ActivityModelCoreHandler.FromFile(file, routeModel, token, false).ConfigureAwait(false);
                    if (activityModel != null && activityFiles.Remove(activityModel.Id, out string filePath)) //
                    {
                        if (activityModel.SetupRequired())
                            activityModel = await Convert(filePath, routeModel, token).ConfigureAwait(false);
                        results.Add(activityModel);
                    }
                }).ConfigureAwait(false);
            }

            //for any new MSTS path (remaining in the preloaded dictionary), Create a path model
            await Parallel.ForEachAsync(activityFiles, cancellationToken, async (activity, token) =>
            {
                ActivityModelCore activityModel = await Convert(activity.Value, routeModel, token).ConfigureAwait(false);
                if (null != activityModel)
                {
                    results.Add(activityModel);
                }
            }).ConfigureAwait(false);

            return results.Concat(new ActivityModelCore[] { ActivityModelHandler.Explorer, ActivityModelHandler.ExploreActivity }).ToFrozenSet();
        }
    }
}
