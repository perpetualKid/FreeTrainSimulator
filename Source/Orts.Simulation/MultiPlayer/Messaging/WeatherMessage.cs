using System;

using FreeTrainSimulator.Common;

using MemoryPack;

using Orts.Common;
using Orts.Simulation.World;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class WeatherMessage : MultiPlayerMessageContent
    {
        public WeatherType Weather { get; set; }
        public float Overcast {  get; set; }
        public float Precipitation { get; set; }
        public float Fog { get; set; }

        [MemoryPackConstructor]
        public WeatherMessage() { }

        public WeatherMessage(Weather weather)
        {
            ArgumentNullException.ThrowIfNull(weather, nameof(weather));

            Weather = weather.WeatherType;
            Fog = weather.FogVisibilityDistance;
            Precipitation = weather.PrecipitationIntensity;
            Overcast = weather.OvercastFactor;
        }
            
        public override void HandleMessage()
        {
            if (multiPlayerManager.IsDispatcher)
                return;
            Simulator.Instance.UpdatedWeatherCondition = new EnvironmentalCondition()
            { 
                FogViewingDistance = Fog,
                OvercastFactor = Overcast,
                Weather = Weather,
                PrecipitationIntensity = Precipitation,
            };
        }
    }
}
