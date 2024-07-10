using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TrainBrakeSaveState: SaveStateBase
    {
        public double EqualReservoirPressure { get; set; }
        public double BrakeLine2Pressure { get; set; }
        public double BrakeLine3Pressure { get; set; }
        public double BrakeLine4Pressure { get; set; }
        public RetainerSetting RetainerSetting { get; set; }
        public int RetainerPercent { get; set; }
        public double TrainBrakePipeVolume { get; set; }
        public double TrainBrakeCylinderVolume { get; set; }
        public double TrainBrakeSystemVolume { get; set; }
        public double CurrentTrainBrakeSystemVolume { get; set; }
        public bool VacuumBrakeEqualizerLocomotive { get; set; }
    }
}
