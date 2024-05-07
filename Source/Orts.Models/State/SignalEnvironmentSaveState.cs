using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class SignalEnvironmentSaveState: SaveStateBase
    {
        public Collection<SignalSaveState> Signals { get; set; }
        public int TrackCircuitSectionsCount { get; set; }
        public Collection<TrackCircuitSectionSaveState> TrackCircuitSections { get; set; }
        public bool LocationPassingPathsEnabled { get; set; }
        public Dictionary<int, int> DeadlockReferences { get; set; }
        public int GlobalDeadlockIndex { get; set; }
        public Collection<DeadlockInfoSaveState> DeadlockDetails { get; set;}
    }
}
