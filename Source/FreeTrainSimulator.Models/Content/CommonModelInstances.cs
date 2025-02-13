using System;
using System.Collections.Immutable;

using FreeTrainSimulator.Common;

namespace FreeTrainSimulator.Models.Content
{
    public static class CommonModelInstances
    {
        public static readonly ActivityModelHeader ExploreMode = new ActivityModelHeader()
        {
            ActivityType = ActivityType.Explorer,
            Name = "- Explore Route -",
            Id = "- Explore Route -",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static readonly ActivityModelHeader ExploreActivityMode = new ActivityModelHeader()
        {
            ActivityType = ActivityType.ExploreActivity,
            Name = "- Explore Route in Activity Mode -",
            Id = "- Explore Route in Activity Mode -",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        public static readonly WagonSetModel Missing = new WagonSetModel()
        {
            Id = "<unknown>",
            Name = "Missing",
            TrainCars = ImmutableArray<WagonReferenceModel>.Empty
        };
    }
}
