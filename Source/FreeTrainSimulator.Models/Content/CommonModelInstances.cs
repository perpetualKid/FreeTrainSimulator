using System;
using System.Collections.Immutable;

using FreeTrainSimulator.Common;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Provides well-known singleton model instances used as defaults or placeholders
    /// throughout the application (e.g. explore mode activities, missing consist fallback).
    /// </summary>
    public static class CommonModelInstances
    {
        /// <summary>Default activity header for the free-roam "Explore Route" mode.</summary>
        public static readonly ActivityModelHeader ExploreMode = new ActivityModelHeader()
        {
            ActivityType = ActivityType.Explorer,
            Name = "- Explore Route -",
            Id = "- Explore Route -",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        /// <summary>Default activity header for the "Explore Route in Activity Mode" mode.</summary>
        public static readonly ActivityModelHeader ExploreActivityMode = new ActivityModelHeader()
        {
            ActivityType = ActivityType.ExploreActivity,
            Name = "- Explore Route in Activity Mode -",
            Id = "- Explore Route in Activity Mode -",
            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)),
            Season = SeasonType.Summer,
            Weather = WeatherType.Clear,
        };

        /// <summary>Fallback consist used when the referenced wagon set cannot be found.</summary>
        public static readonly WagonSetModel Missing = new WagonSetModel()
        {
            Id = "<unknown>",
            Name = "Missing",
            TrainCars = ImmutableArray<WagonReferenceModel>.Empty
        };
    }
}
