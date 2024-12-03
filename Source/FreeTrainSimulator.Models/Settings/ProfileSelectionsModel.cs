using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record ProfileSelectionsModel: ModelBase<ProfileSelectionsModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".profileselections";
        }

        public override ProfileModel Parent => (this as IFileResolve).Container as ProfileModel;
        // Base selections
        public string FolderName { get; set; }
        public string RouteId { get; init; }
        public ActivityType ActivityType { get; init; }
        // Activity mode / Explore mode selections
        public string PathId { get; init; }
        public string ActivityId { get; init; }
        public string LocomotiveId { get; init; }
        public string WagonSetId { get; init; }
        public TimeOnly StartTime { get; init; }
        // Timetable mode selections
        public string TimetableSet { get; init; }
        public string TimetableName { get; init; }
        public string TimetableTrain { get; init; }
        public DayOfWeek TimetableDay { get; init; }
        public string WeatherChanges { get; init; }
        // Shared selections
        public SeasonType Season { get; init; }
        public WeatherType Weather { get; init; }
        // Other selections
        public string LoggingEnabled { get; init; }
        public GamePlayAction GamePlayAction { get; init; }
    }
}
