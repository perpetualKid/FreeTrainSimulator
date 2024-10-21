using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Settings
{
    [MemoryPackable]
    public sealed partial record ProfileSelectionsModel: ModelBase<ProfileSelectionsModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".profileselections";
        }

        public override ProfileModel Parent => _parent as ProfileModel;
        // Base selections
        public string FolderName { get; set; }
        public string RouteName { get; init; }
        public ActivityType ActivityType { get; init; }
        // Activity mode / Explore mode selections
        public string PathName { get; init; }
        public string ActivityName { get; init; }
        public string LocomotiveName { get; init; }
        public string ConsistName { get; init; }
        public TimeOnly StartTime { get; init; }
        // Timetable mode selections
        public string TimetableSet { get; init; }
        public string TimetableName { get; init; }
        public string TimetableTrain { get; init; }
        public int TimetableDay { get; init; }
        // Shared selections
        public SeasonType Season { get; init; }
        public WeatherType Weather { get; init; }
        // Other selections
        public string LoggingEnabled { get; init; }
    }
}
