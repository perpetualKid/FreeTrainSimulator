using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TransferInfoSaveState : SaveStateBase
    {
        public TransferType TransferType { get; set; }
        public TransferUnits TransferUnits { get; set; }
        public int TransferUnitsCount { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<string> TransferConsists { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public int TrainNumber { get; set; }
        public string TrainName { get; set; }
        public int StationPlatformReference { get; set; }
        public bool Valid { get; set; }
    }
}
