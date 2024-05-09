using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class DeadlockPathInfoSaveState: SaveStateBase
    {
        public string Name { get; set; }
        public TrackCircuitPartialPathRouteSaveState PathInfo { get; set; }
        public Collection<string> Groups { get; set; }
        public float UsableLength { get; set; }
        public int EndSectionIndex { get; set; }
        public int LastUsableSectionIndex { get; set; }
        public Collection<int> AllowedTrains { get; set; }
    }
}
