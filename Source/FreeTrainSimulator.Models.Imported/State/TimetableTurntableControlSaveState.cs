using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class TimetableTurntableControlSaveState : SaveStateBase
    {
        public int ParentIndex { get; set; }
        public string PoolName { get; set; }
        public MovingTableState MovingTableState { get; set; }
        public MovingTableAction MovingTableAction { get; set; }
        public int StoragePathIndex { get; set; }
        public int AccessPathIndex { get; set; }
        public bool ReverseFormation { get; set; }
        public int TurnTableExit { get; set; }
        public float ClearingDistance { get; set; }
        public float TrainSpeedMax { get; set; }
        public float TrainSpeedSignal { get; set; }
        public float TrainSpeedLimit { get; set; }
        public float StopPositionOnTurntable { get; set; }
        public bool TrainOnTable { get; set; }
        public int? TrainNumber { get; set; }
        public bool FrontOnBoard { get; set; }
        public bool RearOnBoard { get; set; }
    }
}
