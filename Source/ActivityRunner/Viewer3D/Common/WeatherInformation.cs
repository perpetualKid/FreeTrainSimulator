﻿using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal class WeatherInformation: DebugInfoBase
    {
        private readonly Simulator simulator;

        public WeatherInformation() 
        {
            simulator = Simulator.Instance;
            this["Weather Information"] = null;
            this[".0"] = null;
        }

        public override void Update(GameTime gameTime)
        {
            if (update)
            {
                this["Visibility"] = $"{simulator.Weather.FogVisibilityDistance:0} m";
                this["Cloud cover"] = $"{simulator.Weather.OvercastFactor * 100:0} %";
                this["Intensity"] = $"{simulator.Weather.PrecipitationIntensity:F4} p/s/m^2";
                this["Liquidity"] = $"{simulator.Weather.PrecipitationLiquidity * 100:0} %";
                this["Wind"] = $"{simulator.Weather.WindSpeed.X:F1},{simulator.Weather.WindSpeed.Y:F1} m/s";
                this["Amb Temp"] = FormatStrings.FormatTemperature(simulator.PlayerLocomotive.CarOutsideTempC, Simulator.Instance.MetricUnits);
                base.Update(gameTime);
            }
        }
    }
}
