using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TimetableModel : ModelBase<TimetableModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".timetable";
            subFolder = "Timetables";
        }

        public override RouteModelCore Parent => _parent as RouteModelCore;

        public string Description {  get; init; }
        public DayOfWeek Weekday { get; init; }
        public SeasonType Season { get; init; }
        public WeatherType Weather { get; init; }
    }
}
