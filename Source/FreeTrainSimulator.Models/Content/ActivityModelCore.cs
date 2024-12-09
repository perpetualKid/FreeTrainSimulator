using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record ActivityModelCore : ModelBase, IFileResolve
    {
#pragma warning disable CA1033 // Interface methods should be callable by child types
        static string IFileResolve.SubFolder => "Activities";
        static string IFileResolve.DefaultExtension => ".activity";
#pragma warning restore CA1033 // Interface methods should be callable by child types

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
