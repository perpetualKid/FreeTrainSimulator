using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TransferInfoSaveState: SaveStateBase
    {
        public TransferType TransferType { get; set; }
        public TransferUnits TransferUnits { get; set; }
        public int TransferUnitsCount { get; set; }
        public Collection<string> TransferConsists { get; set; }
        public int TrainNumber { get; set; }
        public string TrainName { get; set; }
        public int StationPlatformReference { get; set; }
        public bool Valid { get; set; }
    }
}
