using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.State
{
    [MemoryPackable]
    public sealed partial class TimetablePoolDetailSaveState : SaveStateBase
    {
        public TrackCircuitPartialPathRouteSaveState StoragePath { get; set; }
        public TravellerSaveState StoragePathTraveller { get; set; }
        public TravellerSaveState StoragePathReverseTraveller { get; set; }
        public string StorageName { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<TrackCircuitPartialPathRouteSaveState> AccessPaths { get; set; }
        public Collection<int> StoredUnits { get; set; }
        public Collection<int> ClaimedUnits { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public float StorageLength { get; set; }
        public float StorageOffset { get; set; }
        public int TableExitIndex { get; set; }
        public int TableVectorIndex { get; set; }
        public float TableMiddleEntry { get; set; }
        public float TableMiddleExit { get; set; }
        public float RemainingLength { get; set; }
        public int? MaxStoredUnits { get; set; }
    }
}
