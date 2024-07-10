using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class CommandSwitchSaveState : SaveStateBase
    {
        public bool CommandSwitch { get; set; }
        public bool CommandButtonOn { get; set; }
        public bool CommandButtonOff { get; set; }
        public bool State { get; set; }
    }
}
