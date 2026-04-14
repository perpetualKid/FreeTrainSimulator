using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Header model containing summary information for an activity.
    /// Abstracts data originally stored in MSTS activity files (<c>.act</c>).
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Activities", ".activity")]
    public partial record ActivityModelHeader : ModelBase
    {
        /// <inheritdoc/>
        public override RouteModelHeader Parent => _parent as RouteModelHeader;
        /// <summary>Human-readable description of the activity.</summary>
        public string Description { get; init; }
        /// <summary>Briefing text displayed to the player before starting the activity.</summary>
        public string Briefing { get; init; }
        /// <summary>In-game time of day when the activity starts.</summary>
        public TimeOnly StartTime { get; init; }
        /// <summary>Season (spring, summer, autumn, winter) for the activity environment.</summary>
        public SeasonType Season { get; init; }
        /// <summary>Weather condition for the activity environment.</summary>
        public WeatherType Weather { get; init; }
        /// <summary>Difficulty level of the activity.</summary>
        public Difficulty Difficulty { get; init; }
        /// <summary>Expected duration of the activity.</summary>
        public TimeSpan Duration { get; init; }
        /// <summary>Type of activity (standard, explorer, timetable, etc.).</summary>
        public ActivityType ActivityType { get; init; }
        /// <summary>Identifier of the train path (<see cref="PathModelHeader"/>) used by this activity.</summary>
        public string PathId { get; init; }
        /// <summary>Identifier of the train consist (<see cref="WagonSetModel"/>) used by this activity.</summary>
        public string ConsistId { get; init; }
    }
}
