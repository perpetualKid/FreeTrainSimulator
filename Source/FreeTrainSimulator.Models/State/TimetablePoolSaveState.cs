using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TimetablePoolSaveState : SaveStateBase
    {
        public TimetablePoolType PoolType { get; set; }
        public string PoolName { get; set; }
        public bool ForceCreation { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<TimetablePoolDetailSaveState> PoolDetails { get; set; }
        public Collection<AccessPathDetailSaveState> AccessDetails { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public int TurntableIndex { get; set; }
        public float TurntableApproachClearance { get; set; }
        public float TurntableReleaseClearance { get; set; }
        public float? TurntableSpeed {  get; set; }
        public int? TurntableFrameRate { get; set; }

    }
}

