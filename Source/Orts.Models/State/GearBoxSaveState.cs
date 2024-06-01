using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class GearBoxSaveState : SaveStateBase
    {
        public int GearIndex { get; set; }
        public int NextGearIndex { get; set; }
        public bool GearedUp { get; set; }
        public bool GearedDown { get; set; }
        public bool ClutchActive { get; set; }
        public float ClutchValue { get; set; }
        public bool ManualGearUp { get; set; }
        public bool ManualGearDown { get; set; }
        public bool ManualGearChange { get; set; }
        public double ManualGearTimer { get; set; }
    }
}
