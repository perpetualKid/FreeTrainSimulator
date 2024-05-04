using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class CabRendererSaveState: SaveStateBase
    {
        public Collection<string> ActiveScreens { get; } = new Collection<string>();
    }
}
