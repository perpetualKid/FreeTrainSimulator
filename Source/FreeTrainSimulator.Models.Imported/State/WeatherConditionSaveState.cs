using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    public class WeatherConditionSaveState : SaveStateBase
    {
        public double Time { get; set; }
    }

    [MemoryPackable]
    public sealed partial class WeatherConditionFogSaveState : WeatherConditionSaveState
    {
        public double SetTime { get; set; }
        public double LiftTime { get; set; }
        public float Visibility { get; set; }
        public float Overcast { get; set; }
    }

    [MemoryPackable]
    public sealed partial class WeatherConditionOvercastSaveState : WeatherConditionSaveState
    {
        public float Overcast { get; set; }
        public float Variation { get; set; }
        public float RateOfChange { get; set; }
        public float Visibility { get; set; }
    }

    [MemoryPackable]
    public sealed partial class WeatherConditionPrecipitationSaveState : WeatherConditionSaveState
    {
        public WeatherType PrecipitationWeatherType { get; set; }
        public float Densitiy { get; set; }
        public float Variation { get; set; }
        public float RateOfChange { get; set; }
        public float Probability { get; set; }
        public float Spread { get; set; }
        public float VisibilityAtMinDensity { get; set; }
        public float VisibilityAtMaxDensity { get; set; }
        public float OvercastPrecipitationStart { get; set; }
        public float OvercastBuildUp { get; set; }
        public float PrecipitationStartPhase { get; set; }
        public float OvercastDispersion { get; set; }
        public float PrecipitationEndPhase { get; set; }
        public WeatherConditionOvercastSaveState OvercastCondition { get; set; }
    }
}
