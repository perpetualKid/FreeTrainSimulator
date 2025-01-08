using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class TrainAxleSaveState : SaveStateBase
    {
        public float SlipPercentage { get; set; }
        public float SlipSpeed { get; set; }
        public float AxleForce { get; set; }
    }
}
