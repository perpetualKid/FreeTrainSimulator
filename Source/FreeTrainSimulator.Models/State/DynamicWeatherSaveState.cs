using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class DynamicWeatherPropertyState : SaveStateBase
    {
        public double Timer { get; set; }
        public float ChangeRate { get; set; }
        public float Value { get; set; }
        public int TransitionTime { get; set; }
    }

    [MemoryPackable]
    public sealed partial class DynamicWeatherSaveState : SaveStateBase
    {
        public DynamicWeatherPropertyState Overcast { get; private set; } = new DynamicWeatherPropertyState();
        public DynamicWeatherPropertyState Fog { get; private set; } = new DynamicWeatherPropertyState();
        public DynamicWeatherPropertyState PrecipitationIntensity { get; private set; } = new DynamicWeatherPropertyState();
        public DynamicWeatherPropertyState PrecipitationLiquidity { get; private set; } = new DynamicWeatherPropertyState();
        public bool FogDistanceIncreasing { get; set; }
        public double StableWeatherTimer { get; set; }
        public double PrecipitationIntensityDelayTimer { get; set; }
    }
}
