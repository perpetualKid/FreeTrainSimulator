using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Content;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class ActivityModelHandler : ContentHandlerBase<ActivityModel, ActivityModelCore>
    {
        public static ActivityModelCore Explorer { get; private set; } = new ActivityModelCore()
        {
            ActivityType = ActivityType.Explorer,
            Name = "- Explore Route -",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static ActivityModelCore ExploreActivity { get; private set; } = new ActivityModelCore()
        {
            ActivityType = ActivityType.ExploreActivity,
            Name = "+ Explore in Activity Mode +",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static async ValueTask<ActivityModel> Get(string name, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            return await FromFile(name, routeModel, cancellationToken).ConfigureAwait(false);
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
    }
}
