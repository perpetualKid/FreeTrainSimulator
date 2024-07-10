using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class EndOfTrainSaveState : SaveStateBase
    {
        public int DeviceId { get; set; }
        public EndOfTrainState EndOfTrainState { get; set; }
    }
}
