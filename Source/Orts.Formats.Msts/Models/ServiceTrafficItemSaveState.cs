using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Formats.Msts.Models
{
    [MemoryPackable]
    public sealed partial class ServiceTrafficItemSaveState : SaveStateBase
    {
        public int ArrivalTime { get; set; }
        public int DepartureTime { get; set; }
        public float Distance { get; set; }
        public int PlatformId { get; set; }
    }
}
