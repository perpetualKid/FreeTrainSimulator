using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.State
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
        public WorldLocation InitialLocation { get; set; }
        public WorldLocation PlayerLocation { get; set; }
        public ActivityType ActivityType { get; set; }
        public SimulatorSaveState SimulatorSaveState { get; set; }
        public ViewerSaveState ViewerSaveState { get; set; }
        public ActivityEvaluationState ActivityEvaluationState { get; set; }
        [MemoryPackInclude]
        private string finalMarker = HeaderEofMarker;

        [MemoryPackIgnore]
        public bool? Valid { get; private set; }

        [MemoryPackIgnore]
        public IEnumerable<string> ArgumentsSetOnly
        {
            get => Arguments;
            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
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
