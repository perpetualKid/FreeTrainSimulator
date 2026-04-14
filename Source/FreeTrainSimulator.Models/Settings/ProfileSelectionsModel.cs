using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    /// <summary>
    /// Persists the user's most recent selections in the main menu UI — content folder, route,
    /// activity/explore/timetable mode, path, consist, weather, season and other per-session
    /// choices — so they can be restored on next launch.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".selections")]
    public sealed partial record ProfileSelectionsModel: ProfileSettingsModelBase
    {
        #region Base selections
        /// <summary>Name of the selected content folder.</summary>
        public string FolderName { get; set; }
        /// <summary>Identifier of the selected route.</summary>
        public string RouteId { get; set; }
        /// <summary>Activity type (Activity, Explore, ExploreActivity, Timetable).</summary>
        public ActivityType ActivityType { get; set; }
        #endregion

        #region Activity mode / Explore mode selections
        /// <summary>Identifier of the selected path (explore modes).</summary>
        public string PathId { get; set; }
        /// <summary>Identifier of the selected activity.</summary>
        public string ActivityId { get; set; }
        /// <summary>Identifier of the selected locomotive.</summary>
        public string LocomotiveId { get; set; }
        /// <summary>Identifier of the selected wagon set / consist.</summary>
        public string WagonSetId { get; set; }
        /// <summary>Simulation start time for explore mode.</summary>
        public TimeOnly StartTime { get; set; } = new TimeOnly(12, 00);
        #endregion

        #region Timetable mode selections
        /// <summary>Identifier of the selected timetable set.</summary>
        public string TimetableSet { get; set; }
        /// <summary>Name of the selected timetable within the set.</summary>
        public string TimetableName { get; set; }
        /// <summary>Identifier of the player train within the timetable.</summary>
        public string TimetableTrain { get; set; }
        /// <summary>Day of week for the timetable run.</summary>
        public DayOfWeek TimetableDay { get; set; }
        /// <summary>Identifier of the selected weather-changes file.</summary>
        public string WeatherChanges { get; set; }
        #endregion

        #region Shared selections
        /// <summary>Season selection (Spring, Summer, Autumn, Winter).</summary>
        public SeasonType Season { get; set; } = SeasonType.Summer;
        /// <summary>Weather selection (Clear, Snow, Rain).</summary>
        public WeatherType Weather { get; set; } = WeatherType.Clear;
        #endregion

        #region Other selections
        /// <summary>Action to perform at launch (Start, Resume, Replay, Test).</summary>
        public GamePlayAction GamePlayAction { get; set; }
        /// <summary>File name of the save-point to resume or replay.</summary>
        public string GameSaveFile { get; set; }
        #endregion
    }
}
