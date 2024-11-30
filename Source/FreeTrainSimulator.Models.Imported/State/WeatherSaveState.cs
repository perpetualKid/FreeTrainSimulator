using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class WeatherSaveState : SaveStateBase
    {
        public WeatherType WeatherType { get; set; }
        public bool RandomizeWeather { get; set; }
        public float FogVisibilityDistance { get; set; }
        public float OvercastFactor { get; set; }
        public float PrecipitationIntensity { get; set; }
        public float PrecipitationLiquidity { get; set; }
        public float WindDirection { get; set; }
        public Vector2 WindSpeed { get; set; }
        public DynamicWeatherSaveState DynamicWeather { get; set; }
        public AutomaticWeatherSaveState AutomaticWeather { get; set; }
    }
}
