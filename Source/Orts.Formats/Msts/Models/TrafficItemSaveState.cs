using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Formats.Msts.Models
{
    [MemoryPackable]
    public sealed partial class TrafficItemSaveState : SaveStateBase
    {
        public float Efficiency { get; set; }
        public int PlatformStartId { get; set; }
    }
}
