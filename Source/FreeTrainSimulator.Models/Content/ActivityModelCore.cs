using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record ActivityModelCore : ModelBase, IFileResolve
    {
        static string IFileResolve.SubFolder => "Activities";
        static string IFileResolve.DefaultExtension => ".activity";

        public override RouteModelCore Parent => _parent as RouteModelCore;
        public string Description { get; init; }
        public string Briefing { get; init; }
        public TimeOnly StartTime { get; init; }
        public SeasonType Season { get; init; }
        public WeatherType Weather { get; init; }
        public Difficulty Difficulty { get; init; }
        public TimeSpan Duration { get; init; }
        public ActivityType ActivityType { get; init; }
        public string PathId { get; init; }
        public string ConsistId { get; init; }
    }
}
