using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class SignalEnvironmentSaveState : SaveStateBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<SignalSaveState> Signals { get; set; }
        public int TrackCircuitSectionsCount { get; set; }
        public Collection<TrackCircuitSectionSaveState> TrackCircuitSections { get; set; }
        public bool LocationPassingPathsEnabled { get; set; }
        public Dictionary<int, int> DeadlockReferences { get; set; }
        public int GlobalDeadlockIndex { get; set; }
        public Dictionary<int, DeadlockInfoSaveState> DeadlockDetails { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
