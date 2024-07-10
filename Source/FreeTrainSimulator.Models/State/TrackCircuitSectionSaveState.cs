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
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1819 // Properties should not return arrays
        public TrackPin[,] ActivePins { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
        public int JunctionSetManual { get; set; }
        public int JunctionLastRoute { get; set; }
        public TrackCircuitStateSaveState TrackCircuitState {  get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Dictionary<int, List<int>> DeadlockTraps { get; set; }
        public Collection<int> DeadlocksActive { get; set; }
        public Collection<int> DeadlocksAwaited { get; set; }
        public int DeadlockReference { get; set; }
        public Dictionary<int, int> DeadlockBoundaries { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
