using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".save")]
    public sealed partial record SavePointModel : ModelBase
    {
        public override RouteModelCore Parent => _parent as RouteModelCore;

        public string RouteName { get; init; }
        public string PathName { get; init; }
        public TimeSpan GameTime { get; init; }
        public DateTime RealTime { get; init; }
        public Tile CurrentTile { get; init; }
        public double DistanceTravelled { get; init; }
        public bool? ValidState { get; init; }// 3 possibilities: invalid, unknown validity, valid
        public bool MultiplayerGame {  get; init; }
        public bool DebriefEvaluation { get; init; }
    }
}
