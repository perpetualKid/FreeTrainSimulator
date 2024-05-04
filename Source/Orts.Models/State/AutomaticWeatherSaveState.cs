using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class AutomaticWeatherSaveState : SaveStateBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<WeatherConditionFogSaveState> FogConditions { get; set; }
        public Collection<WeatherConditionOvercastSaveState> OvercastConditions { get; set; }
        public Collection<WeatherConditionPrecipitationSaveState> PrecipitationConditions { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public int ActiveIndex { get; set; }
        public double NextChangeTime { get; set; }
        // fog
        public float ActualVisibility { get; set; }
        public float LastVisibility { get; set; }
        public float FogChangeRate { get; set; }
        public double FogLiftTime { get; set; }
        // precipitation
        public WeatherType PrecipitationWeatherType { get; set; }
        public float PrecipitationTotalDuration { get; set; }
        public int PrecipitationTotalSpread { get; set; }
        public float PrecipitationActualRate { get; set; }
        public float PrecipitationRequiredRate { get; set; }
        public float PrecipitationRateOfChange { get; set; }
        public float PrecipitationEndSpell {  get; set; }
        public float PrecipitationNextSpell { get; set; }
        public float PrecipitationStartRate { get; set; }
        public float PrecipitationEndRate { get; set; }
        // cloud
        public float OvercastCloudCover {  get; set; }
        public float OvercastCloudRateOfChange { get; set; }
    }
}
