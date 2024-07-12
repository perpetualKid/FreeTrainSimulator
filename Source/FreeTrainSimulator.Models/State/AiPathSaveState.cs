using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class AiPathSaveState : SaveStateBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<AiPathNodeSaveState> AiPathNodeSaveStates { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public string ExpectedPath { get; set; }
    }
}
