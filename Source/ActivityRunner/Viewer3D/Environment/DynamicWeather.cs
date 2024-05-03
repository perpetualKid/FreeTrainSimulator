// COPYRIGHT 2010, 2011, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

// debug compiler flag for test output for automatic weather
//#define DEBUG_AUTOWEATHER 

using System;
using System.IO;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts.Models;

namespace Orts.ActivityRunner.Viewer3D.Environment
{
    public partial class WeatherControl
    {
        public class DynamicWeather
        {
            public const float RainSnowLiquidityThreshold = 0.3f;
            public float overcastChangeRate;
            public float overcastTimer;
            public float fogChangeRate;
            public float fogTimer;
            public float stableWeatherTimer;
            public float precipitationIntensityChangeRate;
            public float precipitationIntensityTimer;
            public float precipitationIntensityDelayTimer = -1;
            public float precipitationLiquidityChangeRate;
            public float precipitationLiquidityTimer;
            public float ORTSOvercast = -1;
            public int ORTSOvercastTransitionTimeS = -1;
            public float ORTSFog = -1;
            public int ORTSFogTransitionTimeS = -1;
            public float ORTSPrecipitationIntensity = -1;
            public int ORTSPrecipitationIntensityTransitionTimeS = -1;
            public float ORTSPrecipitationLiquidity = -1;
            public int ORTSPrecipitationLiquidityTransitionTimeS = -1;
            public bool fogDistanceIncreasing;
            public DynamicWeather()
            {
            }

            public void Save(BinaryWriter outf)
            {
                outf.Write(overcastTimer);
                outf.Write(overcastChangeRate);
                outf.Write(fogTimer);
                outf.Write(fogChangeRate);
                outf.Write(precipitationIntensityTimer);
                outf.Write(precipitationIntensityChangeRate);
                outf.Write(precipitationLiquidityTimer);
                outf.Write(precipitationLiquidityChangeRate);
                outf.Write(ORTSOvercast);
                outf.Write(ORTSFog);
                outf.Write(ORTSPrecipitationIntensity);
                outf.Write(ORTSPrecipitationLiquidity);
                outf.Write(fogDistanceIncreasing);
                outf.Write(ORTSFogTransitionTimeS);
                outf.Write(stableWeatherTimer);
                outf.Write(precipitationIntensityDelayTimer);
            }

            public void Restore(BinaryReader inf)
            {
                overcastTimer = inf.ReadSingle();
                overcastChangeRate = inf.ReadSingle();
                fogTimer = inf.ReadSingle();
                fogChangeRate = inf.ReadSingle();
                precipitationIntensityTimer = inf.ReadSingle();
                precipitationIntensityChangeRate = inf.ReadSingle();
                precipitationLiquidityTimer = inf.ReadSingle();
                precipitationLiquidityChangeRate = inf.ReadSingle();
                ORTSOvercast = inf.ReadSingle();
                ORTSFog = inf.ReadSingle();
                ORTSPrecipitationIntensity = inf.ReadSingle();
                ORTSPrecipitationLiquidity = inf.ReadSingle();
                fogDistanceIncreasing = inf.ReadBoolean();
                ORTSFogTransitionTimeS = inf.ReadInt32();
                stableWeatherTimer = inf.ReadSingle();
                precipitationIntensityDelayTimer = inf.ReadSingle();
            }

            public void ResetWeatherTargets()
            {
                ORTSOvercast = -1;
                ORTSFog = -1;
                ORTSPrecipitationIntensity = -1;
                ORTSPrecipitationLiquidity = -1;
            }

            // Check for correctness of parameters and initialize rates of change

            public void WeatherChange_Init(OrtsWeatherChange eventWeatherChange, WeatherControl weatherControl)
            {
                var wChangeOn = false;
                if (eventWeatherChange.Overcast >= 0 && eventWeatherChange.OvercastTransitionTime >= 0)
                {
                    ORTSOvercast = eventWeatherChange.Overcast;
                    ORTSOvercastTransitionTimeS = eventWeatherChange.OvercastTransitionTime;
                    overcastTimer = ORTSOvercastTransitionTimeS;
                    overcastChangeRate = overcastTimer > 0 ? (MathHelper.Clamp(ORTSOvercast, 0, 1.0f) - weatherControl.weather.OvercastFactor) / ORTSOvercastTransitionTimeS : 0;
                    wChangeOn = true;
                }
                if (eventWeatherChange.Fog >= 0 && eventWeatherChange.FogTransitionTime >= 0)
                {
                    ORTSFog = eventWeatherChange.Fog;
                    ORTSFogTransitionTimeS = eventWeatherChange.FogTransitionTime;
                    fogTimer = ORTSFogTransitionTimeS;
                    var fogFinalValue = MathHelper.Clamp(ORTSFog, 10, 100000);
                    fogDistanceIncreasing = false;
                    fogChangeRate = fogTimer > 0 ? (fogFinalValue - weatherControl.weather.FogVisibilityDistance) / (ORTSFogTransitionTimeS * ORTSFogTransitionTimeS) : 0;
                    if (fogFinalValue > weatherControl.weather.FogVisibilityDistance)
                    {
                        fogDistanceIncreasing = true;
                        fogChangeRate = -fogChangeRate;
                        if (fogTimer > 0)
                            ORTSFog = weatherControl.weather.FogVisibilityDistance;
                    }
                    wChangeOn = true;
                }
                if (eventWeatherChange.PrecipitationIntensity >= 0 && eventWeatherChange.PrecipitationIntensityTransitionTime >= 0)
                {
                    ORTSPrecipitationIntensity = eventWeatherChange.PrecipitationIntensity;
                    ORTSPrecipitationIntensityTransitionTimeS = eventWeatherChange.PrecipitationIntensityTransitionTime;
                    precipitationIntensityTimer = ORTSPrecipitationIntensityTransitionTimeS;
                    // Precipitation ranges from 0 to max PrecipitationViewer.MaxIntensityPPSPM2 if 32bit.
                    // 16bit uses PrecipitationViewer.MaxIntensityPPSPM2_16
                    precipitationIntensityChangeRate = precipitationIntensityTimer > 0 ? (MathHelper.Clamp(ORTSPrecipitationIntensity, 0, PrecipitationViewer.MaxIntensityPPSPM2)
                        - weatherControl.weather.PrecipitationIntensity) / ORTSPrecipitationIntensityTransitionTimeS : 0;
                    wChangeOn = true;
                }
                if (eventWeatherChange.PrecipitationLiquidity >= 0 && eventWeatherChange.PrecipitationLiquidityTransitionTime >= 0)
                {
                    ORTSPrecipitationLiquidity = eventWeatherChange.PrecipitationLiquidity;
                    ORTSPrecipitationLiquidityTransitionTimeS = eventWeatherChange.PrecipitationLiquidityTransitionTime;
                    precipitationLiquidityTimer = ORTSPrecipitationLiquidityTransitionTimeS;
                    precipitationLiquidityChangeRate = precipitationLiquidityTimer > 0 ? (MathHelper.Clamp(ORTSPrecipitationLiquidity, 0, 1.0f)
                        - weatherControl.weather.PrecipitationLiquidity) / ORTSPrecipitationLiquidityTransitionTimeS : 0;
                    wChangeOn = true;
                }
                weatherControl.weatherChangeOn = wChangeOn;
            }

            public void WeatherChange_Update(in ElapsedTime elapsedTime, WeatherControl weatherControl)
            {
                var wChangeOn = false;
                if (ORTSOvercast >= 0)
                {
                    overcastTimer -= (float)elapsedTime.ClockSeconds;
                    if (overcastTimer <= 0)
                        overcastTimer = 0;
                    else
                        wChangeOn = true;
                    weatherControl.weather.OvercastFactor = ORTSOvercast - overcastTimer * overcastChangeRate;
                    if (overcastTimer == 0)
                        ORTSOvercast = -1;
                }
                if (ORTSFog >= 0)
                {
                    fogTimer -= (float)elapsedTime.ClockSeconds;
                    if (fogTimer <= 0)
                        fogTimer = 0;
                    else
                        wChangeOn = true;
                    if (!fogDistanceIncreasing)
                        weatherControl.weather.FogVisibilityDistance = ORTSFog - fogTimer * fogTimer * fogChangeRate;
                    else
                    {
                        var fogTimerDifference = ORTSFogTransitionTimeS - fogTimer;
                        weatherControl.weather.FogVisibilityDistance = ORTSFog - fogTimerDifference * fogTimerDifference * fogChangeRate;
                    }
                    if (fogTimer == 0)
                        ORTSFog = -1;
                }
                if (ORTSPrecipitationIntensity >= 0 && precipitationIntensityDelayTimer == -1)
                {
                    precipitationIntensityTimer -= (float)elapsedTime.ClockSeconds;
                    if (precipitationIntensityTimer <= 0)
                        precipitationIntensityTimer = 0;
                    else if (weatherControl.RandomizedWeather == false)
                        wChangeOn = true;
                    var oldPrecipitationIntensityPPSPM2 = weatherControl.weather.PrecipitationIntensity;
                    weatherControl.weather.PrecipitationIntensity = ORTSPrecipitationIntensity - precipitationIntensityTimer * precipitationIntensityChangeRate;
                    if (weatherControl.weather.PrecipitationIntensity > 0)
                    {
                        if (oldPrecipitationIntensityPPSPM2 == 0)
                        {
                            if (weatherControl.weather.PrecipitationLiquidity > RainSnowLiquidityThreshold)
                                weatherControl.viewer.Simulator.WeatherType = WeatherType.Rain;
                            else
                                weatherControl.viewer.Simulator.WeatherType = WeatherType.Snow;
                            weatherControl.UpdateSoundSources();
                        }
                        weatherControl.UpdateVolume();
                    }
                    if (weatherControl.weather.PrecipitationIntensity == 0)
                        if (oldPrecipitationIntensityPPSPM2 > 0)
                        {
                            weatherControl.viewer.Simulator.WeatherType = WeatherType.Clear;
                            weatherControl.UpdateSoundSources();
                        }
                    if (precipitationIntensityTimer == 0)
                        ORTSPrecipitationIntensity = -1;
                }
                else if (ORTSPrecipitationIntensity >= 0 && precipitationIntensityDelayTimer > 0)
                {
                    precipitationIntensityDelayTimer -= (float)elapsedTime.ClockSeconds;
                    if (precipitationIntensityDelayTimer <= 0)
                    {
                        precipitationIntensityDelayTimer = -1; // OK, now rain/snow can start
                        precipitationIntensityTimer = overcastTimer; // going in parallel now
                    }
                }
                if (ORTSPrecipitationLiquidity >= 0)
                {
                    precipitationLiquidityTimer -= (float)elapsedTime.ClockSeconds;
                    if (precipitationLiquidityTimer <= 0)
                        precipitationLiquidityTimer = 0;
                    else
                        wChangeOn = true;
                    var oldPrecipitationLiquidity = weatherControl.weather.PrecipitationLiquidity;
                    weatherControl.weather.PrecipitationLiquidity = ORTSPrecipitationLiquidity - precipitationLiquidityTimer * precipitationLiquidityChangeRate;
                    if (weatherControl.weather.PrecipitationLiquidity > RainSnowLiquidityThreshold)
                        if (oldPrecipitationLiquidity <= RainSnowLiquidityThreshold)
                        {
                            weatherControl.viewer.Simulator.WeatherType = WeatherType.Rain;
                            weatherControl.UpdateSoundSources();
                            weatherControl.UpdateVolume();
                        }
                    if (weatherControl.weather.PrecipitationLiquidity <= RainSnowLiquidityThreshold)
                        if (oldPrecipitationLiquidity > RainSnowLiquidityThreshold)
                        {
                            weatherControl.viewer.Simulator.WeatherType = WeatherType.Snow;
                            weatherControl.UpdateSoundSources();
                            weatherControl.UpdateVolume();
                        }
                    if (precipitationLiquidityTimer == 0)
                        ORTSPrecipitationLiquidity = -1;
                }
                if (stableWeatherTimer > 0)
                {
                    stableWeatherTimer -= (float)elapsedTime.ClockSeconds;
                    if (stableWeatherTimer <= 0)
                        stableWeatherTimer = 0;
                    else
                        wChangeOn = true;
                }
                weatherControl.weatherChangeOn = wChangeOn;
            }

            public void WeatherChange_NextRandomization(in ElapsedTime elapsedTime, WeatherControl weatherControl) // start next randomization
            {
                // define how much time transition will last
                var weatherChangeTimer = (4 - weatherControl.viewer.Settings.ActWeatherRandomizationLevel) * 600 +
                    StaticRandom.Next((4 - weatherControl.viewer.Settings.ActWeatherRandomizationLevel) * 600);
                // begin with overcast
                var randValue = StaticRandom.Next(170);
                var intermValue = randValue >= 50 ? (float)(randValue - 50f) : randValue;
                ORTSOvercast = intermValue >= 20 ? (float)(intermValue - 20f) / 100f : (float)intermValue / 100f; // give more probability to less overcast
                ORTSOvercastTransitionTimeS = weatherChangeTimer;
                overcastTimer = ORTSOvercastTransitionTimeS;
                overcastChangeRate = overcastTimer > 0 ? (MathHelper.Clamp(ORTSOvercast, 0, 1.0f) - weatherControl.weather.OvercastFactor) / ORTSOvercastTransitionTimeS : 0;
                // Then check if we are in precipitation zone
                if (ORTSOvercast > 0.5)
                {
                    randValue = StaticRandom.Next(75);
                    if (randValue > 40)
                    {
                        ORTSPrecipitationIntensity = (float)(randValue - 40f) / 1000f;
                        if (weatherControl.viewer.Simulator.Season == SeasonType.Winter)
                            weatherControl.weather.PrecipitationLiquidity = 0;
                        else
                            weatherControl.weather.PrecipitationLiquidity = 1;
                    }
                }
                if (weatherControl.weather.PrecipitationIntensity > 0 && ORTSPrecipitationIntensity == -1)
                {
                    ORTSPrecipitationIntensity = 0;
                    // must return to zero before overcast < 0.5
                    ORTSPrecipitationIntensityTransitionTimeS = (int)((0.5 - weatherControl.weather.OvercastFactor) / overcastChangeRate);
                }
                if (weatherControl.weather.PrecipitationIntensity == 0 && ORTSPrecipitationIntensity > 0 && weatherControl.weather.OvercastFactor < 0.5)
                    // we will have precipitation now, but it must start after overcast is over 0.5
                    precipitationIntensityDelayTimer = (0.5f - weatherControl.weather.OvercastFactor) / overcastChangeRate;

                if (ORTSPrecipitationIntensity > 0)
                    ORTSPrecipitationIntensityTransitionTimeS = weatherChangeTimer;
                if (ORTSPrecipitationIntensity >= 0)
                {
                    precipitationIntensityTimer = ORTSPrecipitationIntensityTransitionTimeS;
                    // Precipitation ranges from 0 to max PrecipitationViewer.MaxIntensityPPSPM2 if 32bit.
                    precipitationIntensityChangeRate = precipitationIntensityTimer > 0 ? (MathHelper.Clamp(ORTSPrecipitationIntensity, 0, PrecipitationViewer.MaxIntensityPPSPM2)
                        - weatherControl.weather.PrecipitationIntensity) / ORTSPrecipitationIntensityTransitionTimeS : 0;
                }

                // and now define visibility
                randValue = StaticRandom.Next(2000);
                if (ORTSPrecipitationIntensity > 0 || ORTSOvercast > 0.7f)
                    // use first digit to define power of ten and the other three to define the multiplying number
                    ORTSFog = Math.Max(100, (float)Math.Pow(10, randValue / 1000 + 2) * (float)((randValue % 1000 + 1) / 100f));
                else
                    ORTSFog = Math.Max(500, (float)Math.Pow(10, randValue / 1000 + 3) * (float)((randValue % 1000 + 1) / 100f));
                ORTSFogTransitionTimeS = weatherChangeTimer;
                fogTimer = ORTSFogTransitionTimeS;
                var fogFinalValue = MathHelper.Clamp(ORTSFog, 10, 100000);
                fogDistanceIncreasing = false;
                fogChangeRate = fogTimer > 0 ? (fogFinalValue - weatherControl.weather.FogVisibilityDistance) / (ORTSFogTransitionTimeS * ORTSFogTransitionTimeS) : 0;
                if (fogFinalValue > weatherControl.weather.FogVisibilityDistance)
                {
                    fogDistanceIncreasing = true;
                    fogChangeRate = -fogChangeRate;
                    ORTSFog = weatherControl.weather.FogVisibilityDistance;
                }

                weatherControl.weatherChangeOn = true;
            }
        }
    }
}
