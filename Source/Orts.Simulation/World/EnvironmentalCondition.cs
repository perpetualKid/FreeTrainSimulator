using FreeTrainSimulator.Common;

using Orts.Common;

namespace Orts.Simulation.World
{
    public class EnvironmentalCondition
    {
        public WeatherType Weather {  get; set; }
        public float PrecipitationIntensity { get; set; }
        public float OvercastFactor { get; set; }
        public float OvercastSpeed { get; set;}
        public float FogViewingDistance { get; set; }
    }
}
