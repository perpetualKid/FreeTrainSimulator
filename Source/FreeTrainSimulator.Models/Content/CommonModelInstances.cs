using System;
using System.Collections.Frozen;

using FreeTrainSimulator.Common;

namespace FreeTrainSimulator.Models.Content
{
    public static class CommonModelInstances
    {
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

        public static readonly WagonSetModel Missing = new WagonSetModel()
        {
            Id = "<unknown>",
            Name = "Missing",
            TrainCars = FrozenSet<WagonReferenceModel>.Empty
        };
    }
}
