using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".selections")]
    public sealed partial record ProfileSelectionsModel: ProfileSettingsModelBase
    {
        // Base selections
        public string FolderName { get; set; }
        public string RouteId { get; set; }
        public ActivityType ActivityType { get; set; }
        // Activity mode / Explore mode selections
        public string PathId { get; set; }
        public string ActivityId { get; set; }
        public string LocomotiveId { get; set; }
        public string WagonSetId { get; set; }
        public TimeOnly StartTime { get; set; } = new TimeOnly(12, 00);
        // Timetable mode selections
        public string TimetableSet { get; set; }
        public string TimetableName { get; set; }
        public string TimetableTrain { get; set; }
        public DayOfWeek TimetableDay { get; set; }
        public string WeatherChanges { get; set; }
        // Shared selections
        public SeasonType Season { get; set; } = SeasonType.Summer;
        public WeatherType Weather { get; set; } = WeatherType.Clear;
        // Other selections
        public GamePlayAction GamePlayAction { get; set; }
    }
}
