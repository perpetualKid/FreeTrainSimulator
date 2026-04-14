using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    /// <summary>
    /// Represents metadata for a saved game state (save-point). Stored as <c>.save</c> files
    /// alongside the corresponding binary save data, under a route-specific path.
    /// Contains summary information displayed in the resume/replay UI.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".save")]
    public sealed partial record SavePointModel : ModelBase
    {
        public override RouteModelHeader Parent => _parent as RouteModelHeader;

        /// <summary>Name of the route for this save-point.</summary>
        public string Route { get; init; }
        /// <summary>Name of the path used when the game was saved.</summary>
        public string Path { get; init; }
        /// <summary>In-simulation elapsed game time at the moment of save.</summary>
        public TimeSpan GameTime { get; init; }
        /// <summary>Wall-clock time when the save was created.</summary>
        public DateTime RealTime { get; init; }
        /// <summary>World tile the player train was located on.</summary>
        public Tile CurrentTile { get; init; }
        /// <summary>Total distance the player train had travelled (metres).</summary>
        public double DistanceTravelled { get; init; }
        /// <summary>Three-state validity flag: <see langword="null"/> = unknown, <see langword="true"/> = valid, <see langword="false"/> = invalid.</summary>
        public bool? ValidState { get; init; }
        /// <summary>Indicates whether this save-point was from a multiplayer session.</summary>
        public bool MultiplayerGame {  get; init; }
        /// <summary>Indicates whether debrief evaluation data was recorded.</summary>
        public bool DebriefEvaluation { get; init; }
    }
}
