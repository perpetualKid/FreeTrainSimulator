using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class DetachInfoSaveState : SaveStateBase
    {
        public int TrainNumber { get; set; }
        public string TrainName { get; set; }
        public DetachPositionInfo DetachPosition { get; set; }
        public int DetachSectionIndex { get; set; }
        public TransferUnits DetachUnits { get; set; }
        public int DetachUnitsNumber { get; set; }
        public int? DetachTime { get; set; }
        public bool? ReverseDetachedTrain { get; set; }
        public bool PlayerAutoDetach { get; set; }
        public Collection<string> DetachConsists { get; set; }
        public bool Valid { get; set; }
        public bool DetachFormedStatic { get; set; }
    }
}
