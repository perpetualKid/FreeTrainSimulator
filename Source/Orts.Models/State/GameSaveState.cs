using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common.Position;
using Orts.Formats.Msts;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class GameSaveState : SaveStateBase
    {
        private const string HeaderEofMarker = "9zZi9WX51VvAH25Lgi0t";

        public string GameVersion { get; set; }
        public string RouteName { get; set; }
        public string PathName { get; set; }
        public Collection<string> Arguments { get; private set; }
        public bool MultiplayerGame { get; set; }
        public double GameTime { get; set; }
        public DateTime RealSaveTime { get; set; }
        public Tile InitalTile { get; set; }
        public WorldLocation PlayerPosition { get; set; }
        public ActivityType ActivityType { get; set; }
        [MemoryPackInclude]
        private string finalMarker = HeaderEofMarker;
        public SimulatorSaveState SimulatorSaveState { get; set; }
        public ViewerSaveState ViewerSaveState { get; set; }
        public ActivityEvaluationState ActivityEvaluationState { get; set; }
        public ReadOnlySequence<byte> LegacyState { get; set; }

        [MemoryPackIgnore]
        public bool? Valid { get; private set; }
        [MemoryPackIgnore]
        public IEnumerable<string> ArgumentsSetOnly
        {
            set
            {
                Arguments = new Collection<string>();
                foreach (string item in value)
                {
                    Arguments.Add(item);
                }
            }
        }

        [MemoryPackOnDeserialized]
        public void OnDeserialized()
        {
            Valid = finalMarker == HeaderEofMarker;
        }
    }
}
