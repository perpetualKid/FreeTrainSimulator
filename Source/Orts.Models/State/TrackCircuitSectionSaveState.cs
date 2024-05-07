using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Formats.Msts.Models;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TrackCircuitSectionSaveState: SaveStateBase
    {
        public int Index { get; set; }
        public TrackPin[,] ActivePins { get; set; }
        public int JunctionSetManual { get; set; }
        public int JunctionLastRoute { get; set; }
        public TrackCircuitStateSaveState TrackCircuitState {  get; set; }
        public Dictionary<int, List<int>> DeadlockTraps { get; set; }
        public Collection<int> DeadlocksActive { get; set; }
        public Collection<int> DeadlocksAwaited { get; set; }
        public int DeadlockReference { get; set; }
        public Dictionary<int, int> DeadlockBoundaries { get; set; }

    }
}
