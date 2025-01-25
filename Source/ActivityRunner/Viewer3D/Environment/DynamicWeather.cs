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

using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Models.Imported.State;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Models;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Environment
{
    public partial class WeatherControl
    {
        private sealed class DynamicWeather
        {
            public enum WeatherTarget
            {
                Overcast,
                Fog,
                PrecipitationIntensity,
                PrecipicationLiquidity,
            }

            private sealed class WeatherProperty
            {
                public float ChangeRate { get; set; }
                public double Timer { get; set; }
                public float Value { get; set; } = -1;
                public int TransitionTime { get; set; } = -1;
            }

            private readonly Viewer viewer;
            private bool weatherChange;

            public const float RainSnowLiquidityThreshold = 0.3f;

            private readonly WeatherProperty overcast = new WeatherProperty();
            private readonly WeatherProperty fog = new WeatherProperty();
            private readonly WeatherProperty precipitationIntensity = new WeatherProperty();
            private readonly WeatherProperty precipitationLiquidity = new WeatherProperty();

            private float StableWeatherTimer;
            private double PrecipitationIntensityDelayTimer = -1;
            private bool fogDistanceIncreasing;

            public DynamicWeather(Viewer viewer, bool randomizedWeather)
            {
                this.viewer = viewer;
                // We have a pause in weather change, depending from randomization level
                if (randomizedWeather)
                    StableWeatherTimer = (4.0f - this.viewer.UserSettings.WeatherRandomizationLevel) * 600 + StaticRandom.Next(300) - 150;
            }

            public void ResetWeatherTargets()
            {
                overcast.Value = -1;
                fog.Value = -1;
                precipitationIntensity.Value = -1;
                precipitationLiquidity.Value = -1;
            }

            public void ResetWeatherTarget(WeatherTarget target)
            {
                switch (target)
                {
                    case WeatherTarget.Overcast:
                        overcast.Value = -1;
                        break;
                    case WeatherTarget.Fog:
                        fog.Value = -1;
                        break;
                    case WeatherTarget.PrecipitationIntensity:
                        precipitationIntensity.Value = -1;
                        break;
                    case WeatherTarget.PrecipicationLiquidity:
                        precipitationLiquidity.Value = -1;
                        break;
                }
            }

            // Check for correctness of parameters and initialize rates of change

            public void WeatherChange_Init(OrtsWeatherChange eventWeatherChange, WeatherControl weatherControl)
            {
                weatherChange = false;
                if (eventWeatherChange.Overcast >= 0 && eventWeatherChange.OvercastTransitionTime >= 0)
                {
                    overcast.Value = eventWeatherChange.Overcast;
                    overcast.TransitionTime = eventWeatherChange.OvercastTransitionTime;
                    overcast.Timer = overcast.TransitionTime;
                    overcast.ChangeRate = overcast.Timer > 0 ? (MathHelper.Clamp(overcast.Value, 0, 1.0f) - weatherControl.weather.OvercastFactor) / overcast.TransitionTime : 0;
                    weatherChange = true;
                }
                if (eventWeatherChange.Fog >= 0 && eventWeatherChange.FogTransitionTime >= 0)
                {
                    fog.Value = eventWeatherChange.Fog;
                    fog.TransitionTime = eventWeatherChange.FogTransitionTime;
                    fog.Timer = fog.TransitionTime;
                    var fogFinalValue = MathHelper.Clamp(fog.Value, 10, 100000);
                    fogDistanceIncreasing = false;
                    fog.ChangeRate = fog.Timer > 0 ? (fogFinalValue - weatherControl.weather.FogVisibilityDistance) / (fog.TransitionTime * fog.TransitionTime) : 0;
                    if (fogFinalValue > weatherControl.weather.FogVisibilityDistance)
                    {
                        fogDistanceIncreasing = true;
                        fog.ChangeRate = -fog.ChangeRate;
                        if (fog.Timer > 0)
                            fog.Value = weatherControl.weather.FogVisibilityDistance;
                    }
                    weatherChange = true;
                }
                if (eventWeatherChange.PrecipitationIntensity >= 0 && eventWeatherChange.PrecipitationIntensityTransitionTime >= 0)
                {
                    precipitationIntensity.Value = eventWeatherChange.PrecipitationIntensity;
                    precipitationIntensity.TransitionTime = eventWeatherChange.PrecipitationIntensityTransitionTime;
                    precipitationIntensity.Timer = precipitationIntensity.TransitionTime;
                    // Precipitation ranges from 0 to max PrecipitationViewer.MaxIntensityPPSPM2 if 32bit.
                    // 16bit uses PrecipitationViewer.MaxIntensityPPSPM2_16
                    precipitationIntensity.ChangeRate = precipitationIntensity.Timer > 0 ? (MathHelper.Clamp(precipitationIntensity.Value, 0, PrecipitationViewer.MaxIntensityPPSPM2)
                        - weatherControl.weather.PrecipitationIntensity) / precipitationIntensity.TransitionTime : 0;
                    weatherChange = true;
                }
                if (eventWeatherChange.PrecipitationLiquidity >= 0 && eventWeatherChange.PrecipitationLiquidityTransitionTime >= 0)
                {
                    precipitationLiquidity.Value = eventWeatherChange.PrecipitationLiquidity;
                    precipitationLiquidity.TransitionTime = eventWeatherChange.PrecipitationLiquidityTransitionTime;
                    precipitationLiquidity.Timer = precipitationLiquidity.TransitionTime;
                    precipitationLiquidity.ChangeRate = precipitationLiquidity.Timer > 0 ? (MathHelper.Clamp(precipitationLiquidity.Value, 0, 1.0f)
                        - weatherControl.weather.PrecipitationLiquidity) / precipitationLiquidity.TransitionTime : 0;
                    weatherChange = true;
                }
            }

            public void WeatherChange_Update(in ElapsedTime elapsedTime, WeatherControl weatherControl)
            {
                weatherChange = false;

                static bool CheckTimer(WeatherProperty weatherProperty, in ElapsedTime elapsedTime)
                {
                    weatherProperty.Timer -= elapsedTime.ClockSeconds;
                    if (weatherProperty.Timer <= 0)
                        weatherProperty.Timer = 0;
                    return weatherProperty.Timer > 0;
                }

                static void ResetPropertyOnTimer(WeatherProperty weatherProperty)
                {
                    if (weatherProperty.Timer <= 0)
                        weatherProperty.Value = 1;
                }

                if (overcast.Value >= 0)
                {
                    weatherChange |= CheckTimer(overcast, elapsedTime);
                    weatherControl.weather.OvercastFactor = (float)(overcast.Value - overcast.Timer * overcast.ChangeRate);
                    ResetPropertyOnTimer(overcast);
                }
                if (fog.Value >= 0)
                {
                    weatherChange |= CheckTimer(fog, elapsedTime);
                    if (!fogDistanceIncreasing)
                        weatherControl.weather.FogVisibilityDistance = (float)(fog.Value - fog.Timer * fog.Timer * fog.ChangeRate);
                    else
                    {
                        var fogTimerDifference = fog.TransitionTime - fog.Timer;
                        weatherControl.weather.FogVisibilityDistance = (float)(fog.Value - fogTimerDifference * fogTimerDifference * fog.ChangeRate);
                    }
                    ResetPropertyOnTimer(fog);
                }
                if (precipitationIntensity.Value >= 0 && PrecipitationIntensityDelayTimer == -1)
                {
                    if (!CheckTimer(precipitationIntensity, elapsedTime) && !weatherControl.RandomizedWeather)
                        weatherChange = true;
                    float oldPrecipitationIntensityPPSPM2 = weatherControl.weather.PrecipitationIntensity;
                    weatherControl.weather.PrecipitationIntensity = (float)(precipitationIntensity.Value - precipitationIntensity.Timer * precipitationIntensity.ChangeRate);
                    if (weatherControl.weather.PrecipitationIntensity > 0)
                    {
                        if (oldPrecipitationIntensityPPSPM2 == 0)
                        {
                            if (weatherControl.weather.PrecipitationLiquidity > RainSnowLiquidityThreshold)
                                viewer.Simulator.WeatherType = WeatherType.Rain;
                            else
                                viewer.Simulator.WeatherType = WeatherType.Snow;
                            weatherControl.UpdateSoundSources();
                        }
                        weatherControl.UpdateVolume();
                    }
                    if (weatherControl.weather.PrecipitationIntensity == 0)
                        if (oldPrecipitationIntensityPPSPM2 > 0)
                        {
                            viewer.Simulator.WeatherType = WeatherType.Clear;
                            weatherControl.UpdateSoundSources();
                        }
                    ResetPropertyOnTimer(precipitationIntensity);
                }
                else if (precipitationIntensity.Value >= 0 && PrecipitationIntensityDelayTimer > 0)
                {
                    PrecipitationIntensityDelayTimer -= elapsedTime.ClockSeconds;
                    if (PrecipitationIntensityDelayTimer <= 0)
                    {
                        PrecipitationIntensityDelayTimer = -1; // OK, now rain/snow can start
                        precipitationIntensity.Timer = overcast.Timer; // going in parallel now
                    }
                }
                if (precipitationLiquidity.Value >= 0)
                {
                    weatherChange |= CheckTimer(precipitationLiquidity, elapsedTime);
                    float oldPrecipitationLiquidity = weatherControl.weather.PrecipitationLiquidity;
                    weatherControl.weather.PrecipitationLiquidity = (float)(precipitationLiquidity.Value - precipitationLiquidity.Timer * precipitationLiquidity.ChangeRate);
                    if (weatherControl.weather.PrecipitationLiquidity > RainSnowLiquidityThreshold)
                        if (oldPrecipitationLiquidity <= RainSnowLiquidityThreshold)
                        {
                            viewer.Simulator.WeatherType = WeatherType.Rain;
                            weatherControl.UpdateSoundSources();
                            weatherControl.UpdateVolume();
                        }
                    if (weatherControl.weather.PrecipitationLiquidity <= RainSnowLiquidityThreshold)
                        if (oldPrecipitationLiquidity > RainSnowLiquidityThreshold)
                        {
                            viewer.Simulator.WeatherType = WeatherType.Snow;
                            weatherControl.UpdateSoundSources();
                            weatherControl.UpdateVolume();
                        }
                    ResetPropertyOnTimer(precipitationLiquidity);
                }
                if (StableWeatherTimer > 0)
                {
                    StableWeatherTimer -= (float)elapsedTime.ClockSeconds;
                    if (StableWeatherTimer <= 0)
                        StableWeatherTimer = 0;
                    else
                        weatherChange = true;
                }
            }

            public void WeatherChange_NextRandomization(in ElapsedTime elapsedTime, WeatherControl weatherControl) // start next randomization
            {
                // define how much time transition will last
                var weatherChangeTimer = (4 - viewer.UserSettings.WeatherRandomizationLevel) * 600 +
                    StaticRandom.Next((4 - viewer.UserSettings.WeatherRandomizationLevel) * 600);
                // begin with overcast
                var randValue = StaticRandom.Next(170);
                var intermValue = randValue >= 50 ? (float)(randValue - 50f) : randValue;
                overcast.Value = intermValue >= 20 ? (float)(intermValue - 20f) / 100f : (float)intermValue / 100f; // give more probability to less overcast
                overcast.TransitionTime = weatherChangeTimer;
                overcast.Timer = overcast.TransitionTime;
                overcast.ChangeRate = overcast.Timer > 0 ? (MathHelper.Clamp(overcast.Value, 0, 1.0f) - weatherControl.weather.OvercastFactor) / overcast.TransitionTime : 0;
                // Then check if we are in precipitation zone
                if (overcast.Value > 0.5)
                {
                    randValue = StaticRandom.Next(75);
                    if (randValue > 40)
                    {
                        precipitationIntensity.Value = (float)(randValue - 40f) / 1000f;
                        weatherControl.weather.PrecipitationLiquidity = Simulator.Instance.Season == SeasonType.Winter ? 0 : 1;
                    }
                }
                if (weatherControl.weather.PrecipitationIntensity > 0 && precipitationIntensity.Value == -1)
                {
                    precipitationIntensity.Value = 0;
                    // must return to zero before overcast < 0.5
                    precipitationIntensity.TransitionTime = (int)((0.5 - weatherControl.weather.OvercastFactor) / overcast.ChangeRate);
                }
                if (weatherControl.weather.PrecipitationIntensity == 0 && precipitationIntensity.Value > 0 && weatherControl.weather.OvercastFactor < 0.5)
                    // we will have precipitation now, but it must start after overcast is over 0.5
                    PrecipitationIntensityDelayTimer = (0.5f - weatherControl.weather.OvercastFactor) / overcast.ChangeRate;

                if (precipitationIntensity.Value > 0)
                    precipitationIntensity.TransitionTime = weatherChangeTimer;
                if (precipitationIntensity.Value >= 0)
                {
                    precipitationIntensity.Timer = precipitationIntensity.TransitionTime;
                    // Precipitation ranges from 0 to max PrecipitationViewer.MaxIntensityPPSPM2 if 32bit.
                    precipitationIntensity.ChangeRate = precipitationIntensity.Timer > 0 ? (MathHelper.Clamp(precipitationIntensity.Value, 0, PrecipitationViewer.MaxIntensityPPSPM2)
                        - weatherControl.weather.PrecipitationIntensity) / precipitationIntensity.TransitionTime : 0;
                }

                // and now define visibility
                randValue = StaticRandom.Next(2000);
                if (precipitationIntensity.Value > 0 || overcast.Value > 0.7f)
                    // use first digit to define power of ten and the other three to define the multiplying number
                    fog.Value = Math.Max(100, (float)Math.Pow(10, randValue / 1000 + 2) * (float)((randValue % 1000 + 1) / 100f));
                else
                    fog.Value = Math.Max(500, (float)Math.Pow(10, randValue / 1000 + 3) * (float)((randValue % 1000 + 1) / 100f));
                fog.TransitionTime = weatherChangeTimer;
                fog.Timer = fog.TransitionTime;
                var fogFinalValue = MathHelper.Clamp(fog.Value, 10, 100000);
                fogDistanceIncreasing = false;
                fog.ChangeRate = fog.Timer > 0 ? (fogFinalValue - weatherControl.weather.FogVisibilityDistance) / (fog.TransitionTime * fog.TransitionTime) : 0;
                if (fogFinalValue > weatherControl.weather.FogVisibilityDistance)
                {
                    fogDistanceIncreasing = true;
                    fog.ChangeRate = -fog.ChangeRate;
                    fog.Value = weatherControl.weather.FogVisibilityDistance;
                }

                weatherChange = true;
            }


            public ValueTask<DynamicWeatherSaveState> Snapshot()
            {
                DynamicWeatherSaveState dynamicWeather = new DynamicWeatherSaveState();
                dynamicWeather.Overcast.Timer = overcast.Timer;
                dynamicWeather.Overcast.ChangeRate = overcast.ChangeRate;
                dynamicWeather.Overcast.Value = overcast.Value;
                dynamicWeather.Overcast.TransitionTime = overcast.TransitionTime;

                dynamicWeather.Fog.Timer = fog.Timer;
                dynamicWeather.Fog.ChangeRate = fog.ChangeRate;
                dynamicWeather.Fog.Value = fog.Value;
                dynamicWeather.Fog.TransitionTime = fog.TransitionTime;

                dynamicWeather.PrecipitationIntensity.Timer = precipitationIntensity.Timer;
                dynamicWeather.PrecipitationIntensity.ChangeRate = precipitationIntensity.ChangeRate;
                dynamicWeather.PrecipitationIntensity.Value = precipitationIntensity.Value;
                dynamicWeather.PrecipitationIntensity.TransitionTime = precipitationIntensity.TransitionTime;

                dynamicWeather.PrecipitationLiquidity.Timer = precipitationLiquidity.Timer;
                dynamicWeather.PrecipitationLiquidity.ChangeRate = precipitationLiquidity.ChangeRate;
                dynamicWeather.PrecipitationLiquidity.Value = precipitationLiquidity.Value;
                dynamicWeather.PrecipitationLiquidity.TransitionTime = precipitationLiquidity.TransitionTime;

                dynamicWeather.FogDistanceIncreasing = fogDistanceIncreasing;
                dynamicWeather.StableWeatherTimer = StableWeatherTimer;
                dynamicWeather.PrecipitationIntensityDelayTimer = PrecipitationIntensityDelayTimer;
                return ValueTask.FromResult(dynamicWeather);
            }

            public ValueTask Restore(DynamicWeatherSaveState saveState)
            {
                ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

                overcast.Timer = saveState.Overcast.Timer;
                overcast.ChangeRate = saveState.Overcast.ChangeRate;
                overcast.Value = saveState.Overcast.Value;
                overcast.TransitionTime = saveState.Overcast.TransitionTime;

                fog.Timer = saveState.Fog.Timer;
                fog.ChangeRate = saveState.Fog.ChangeRate;
                fog.Value = saveState.Fog.Value;
                fog.TransitionTime = saveState.Fog.TransitionTime;

                precipitationIntensity.Timer = saveState.PrecipitationIntensity.Timer;
                precipitationIntensity.ChangeRate = saveState.PrecipitationIntensity.ChangeRate;
                precipitationIntensity.Value = saveState.PrecipitationIntensity.Value;
                precipitationIntensity.TransitionTime = saveState.PrecipitationIntensity.TransitionTime;

                precipitationLiquidity.Timer = saveState.PrecipitationLiquidity.Timer;
                precipitationLiquidity.ChangeRate = saveState.PrecipitationLiquidity.ChangeRate;
                precipitationLiquidity.Value = saveState.PrecipitationLiquidity.Value;
                precipitationLiquidity.TransitionTime = saveState.PrecipitationLiquidity.TransitionTime;
                fogDistanceIncreasing = saveState.FogDistanceIncreasing;
                StableWeatherTimer = (float)saveState.StableWeatherTimer;
                PrecipitationIntensityDelayTimer = saveState.PrecipitationIntensityDelayTimer;

                return ValueTask.CompletedTask;
            }

            public bool NeedUpdate()
            {
                return weatherChange || precipitationLiquidity.Timer > 0;
            }
        }
    }
}
