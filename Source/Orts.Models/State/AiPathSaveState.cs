using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class AiPathSaveState: SaveStateBase
    {
        public Collection<AiPathNodeSaveState> AiPathNodeSaveStates { get; set; }
        public string ExpectedPath { get; set; }
    }
}
