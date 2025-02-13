﻿using System;

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
        public override RouteModelHeader Parent => _parent as RouteModelHeader;

        public string Route { get; init; }
        public string Path { get; init; }
        public TimeSpan GameTime { get; init; }
        public DateTime RealTime { get; init; }
        public Tile CurrentTile { get; init; }
        public double DistanceTravelled { get; init; }
        public bool? ValidState { get; init; }// 3 possibilities: invalid, unknown validity, valid
        public bool MultiplayerGame {  get; init; }
        public bool DebriefEvaluation { get; init; }
    }
}
