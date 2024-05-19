using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TimetablePoolSaveState : SaveStateBase
    {
        public TimetablePoolType PoolType { get; set; }
        public string PoolName { get; set; }
        public bool ForceCreation { get; set; }
        public Collection<TimetablePoolDetailSaveState> PoolDetails { get; set; }
        public Collection<AccessPathDetailSaveState> AccessDetails { get; set; }
        public int TurntableIndex { get; set; }
        public float TurntableApproachClearance { get; set; }
        public float TurntableReleaseClearance { get; set; }
        public float? TurntableSpeed {  get; set; }
        public int? TurntableFrameRate { get; set; }

    }
}

