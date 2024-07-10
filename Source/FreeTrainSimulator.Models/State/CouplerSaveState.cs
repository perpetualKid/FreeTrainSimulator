using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class CouplerSaveState : SaveStateBase
    {
        public bool Rigid { get; set; }
        public float R0X { get; set; }
        public float R0Y { get; set; }
        public float R0Delta { get; set; }
        public float Stiffness1 { get; set; }
        public float Stiffness2 { get; set; }
        public float CouplerSlackA { get; set; }
        public float CouplerSlackB { get; set; }
        public float Break1 { get; set; }
        public float Break2 { get; set; }
    }
}
