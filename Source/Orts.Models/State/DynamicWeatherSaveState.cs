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
    }

    [MemoryPackable]
    public sealed partial class DynamicWeatherSaveState : SaveStateBase
    {
        public DynamicWeatherPropertyState Overcast { get; } = new DynamicWeatherPropertyState();
        public DynamicWeatherPropertyState Fog { get; } = new DynamicWeatherPropertyState();
        public DynamicWeatherPropertyState PrecipitationIntensity { get; } = new DynamicWeatherPropertyState();
        public DynamicWeatherPropertyState PrecipitationLiquidity { get; } = new DynamicWeatherPropertyState();
        public bool FogDistanceIncreasing { get; set; }
        public double FogTransitionTime { get; set; }
        public double StableWeatherTimer { get; set; }
        public double PrecipitationIntensityDelayTimer { get; set; }
    }
}
