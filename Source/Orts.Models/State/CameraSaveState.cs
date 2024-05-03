using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common.Position;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class CameraSaveState: SaveStateBase
    {
        public WorldLocation Location { get; set; }
        public float FieldOfView { get; set; }
    }
}
