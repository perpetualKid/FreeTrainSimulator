using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class AuthoritySaveState : SaveStateBase
    {
        public EndAuthorityType EndAuthorityType { get; set; }
        public int LastReservedSection { get; set; }
        public float Distance { get; set; }
    }
}
