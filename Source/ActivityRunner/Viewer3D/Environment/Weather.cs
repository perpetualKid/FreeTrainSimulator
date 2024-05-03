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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Sound;
using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Input;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Multiplayer.Messaging;
using Orts.Simulation.World;

namespace Orts.ActivityRunner.Viewer3D.Environment
{
    public partial class WeatherControl
    {
        private protected readonly Viewer viewer;
        private protected readonly Weather weather;

        public readonly List<SoundSourceBase> ClearSound;
        public readonly List<SoundSourceBase> RainSound;
        public readonly List<SoundSourceBase> SnowSound;
        public readonly List<SoundSourceBase> WeatherSounds = new List<SoundSourceBase>();

        public bool weatherChangeOn;
        public DynamicWeather dynamicWeather;
        public bool RandomizedWeather;
        public bool DesertZone; // we are in a desert zone, so no randomized weather change...
        private float[,] DesertZones = { { 30, 45, -120, -105 } }; // minlat, maxlat, minlong, maxlong

        // Variables used for wind calculations
        private Vector2D windSpeedInternalMpS;
        private Vector2D[] windSpeedMpS = new Vector2D[2];
        public float Time;
        private readonly double[] windChangeMpSS = { 40, 5 }; // Flurry, steady
        private const double windSpeedMaxMpS = 4.5f;
        private double windUpdateTimer;
        private float WindGustUpdateTimeS = 1.0f;
        private bool InitialWind = true;
        private float BaseWindDirectionRad;
        private float WindDirectionVariationRad = MathHelper.ToRadians(45.0f); // Set at 45 Deg
        private float calculatedWindDirection;


        public WeatherControl(Viewer viewer)
        {
            this.viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));

            weather = this.viewer.Simulator.Weather;

            var pathArray = new[] {
                Path.Combine(Simulator.Instance.RouteFolder.SoundFolder),
                Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.SoundFolder)
            };

            ClearSound = new List<SoundSourceBase>() {
                new SoundSource(SoundEventSource.InGame, FolderStructure.FindFileFromFolders(pathArray, "clear_in.sms"), false),
                new SoundSource(SoundEventSource.InGame, FolderStructure.FindFileFromFolders(pathArray, "clear_ex.sms"), false),
            };
            RainSound = new List<SoundSourceBase>() {
                new SoundSource(SoundEventSource.InGame, FolderStructure.FindFileFromFolders(pathArray, "rain_in.sms"), false),
                new SoundSource(SoundEventSource.InGame, FolderStructure.FindFileFromFolders(pathArray, "rain_ex.sms"), false),
            };
            SnowSound = new List<SoundSourceBase>() {
                new SoundSource(SoundEventSource.InGame, FolderStructure.FindFileFromFolders(pathArray, "snow_in.sms"), false),
                new SoundSource(SoundEventSource.InGame, FolderStructure.FindFileFromFolders(pathArray, "snow_ex.sms"), false),
            };

            WeatherSounds.AddRange(ClearSound);
            WeatherSounds.AddRange(RainSound);
            WeatherSounds.AddRange(SnowSound);

            SetInitialWeatherParameters();
            UpdateWeatherParameters();

            // add here randomized weather
            if (this.viewer.Settings.ActWeatherRandomizationLevel > 0 && this.viewer.Simulator.ActivityRun != null && !this.viewer.Simulator.ActivityRun.WeatherChangesPresent)
            {
                RandomizedWeather = RandomizeInitialWeather();
                dynamicWeather = new DynamicWeather();
                if (RandomizedWeather)
                {
                    UpdateSoundSources();
                    UpdateVolume();
                    // We have a pause in weather change, depending from randomization level
                    dynamicWeather.stableWeatherTimer = (4.0f - this.viewer.Settings.ActWeatherRandomizationLevel) * 600 + StaticRandom.Next(300) - 150;
                    weatherChangeOn = true;
                }

            }

            this.viewer.Simulator.WeatherChanged += (sender, e) =>
            {
                SetInitialWeatherParameters();
                UpdateWeatherParameters();
            };

            if (MultiPlayerManager.MultiplayerState != MultiplayerState.Client)
            {
                viewer.UserCommandController.AddEvent(UserCommand.DebugWeatherChange, KeyEventType.KeyPressed, () =>
                {
                    this.viewer.Simulator.WeatherType = this.viewer.Simulator.WeatherType.Next();
                    // block dynamic weather change after a manual weather change operation
                    weatherChangeOn = false;
                    dynamicWeather?.ResetWeatherTargets();
                    UpdateWeatherParameters();

                    // If we're a multiplayer server, send out the new weather to all clients.
                    if (MultiPlayerManager.IsServer())
                        MultiPlayerManager.Broadcast(new WeatherMessage(Simulator.Instance.Weather));

                });

                // Overcast ranges from 0 (completely clear) to 1 (completely overcast).
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastIncrease, KeyEventType.KeyDown, (gameTime) =>
                {
                    weather.OvercastFactor = (float)MathHelperD.Clamp(weather.OvercastFactor + gameTime.ElapsedGameTime.TotalSeconds / 10, 0, 1);
                    weatherChangeOn = false;
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSOvercast = -1;
                });
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastDecrease, KeyEventType.KeyDown, (gameTime) =>
                {
                    weather.OvercastFactor = (float)MathHelperD.Clamp(weather.OvercastFactor - gameTime.ElapsedGameTime.TotalSeconds / 10, 0, 1);
                    weatherChangeOn = false;
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSOvercast = -1;
                });

                // Pricipitation ranges from 0 to max PrecipitationViewer.MaxIntensityPPSPM2 if 32bit.
                // 16bit uses PrecipitationViewer.MaxIntensityPPSPM2_16
                // 0xFFFF represents 65535 which is the max for 16bit devices.
                viewer.UserCommandController.AddEvent(UserCommand.DebugPrecipitationIncrease, KeyEventType.KeyDown, () =>
                {
                    if (this.viewer.Simulator.WeatherType == WeatherType.Clear)
                    {
                        this.viewer.SoundProcess.RemoveSoundSources(this);
                        if (weather.PrecipitationLiquidity > DynamicWeather.RainSnowLiquidityThreshold)
                        {
                            this.viewer.Simulator.WeatherType = WeatherType.Rain;
                            this.viewer.SoundProcess.AddSoundSources(this, RainSound);
                        }
                        else
                        {
                            this.viewer.Simulator.WeatherType = WeatherType.Snow;
                            this.viewer.SoundProcess.AddSoundSources(this, SnowSound);
                        }
                    }
                    weather.PrecipitationIntensity = MathHelper.Clamp(weather.PrecipitationIntensity * 1.05f, PrecipitationViewer.MinIntensityPPSPM2 + 0.0000001f, PrecipitationViewer.MaxIntensityPPSPM2);
                    weatherChangeOn = false;
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSPrecipitationIntensity = -1;
                    UpdateVolume();
                });
                viewer.UserCommandController.AddEvent(UserCommand.DebugPrecipitationDecrease, KeyEventType.KeyDown, () =>
                {
                    weather.PrecipitationIntensity = MathHelper.Clamp(weather.PrecipitationIntensity / 1.05f, PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
                    if (weather.PrecipitationIntensity < PrecipitationViewer.MinIntensityPPSPM2 + 0.00001f)
                    {
                        weather.PrecipitationIntensity = 0;
                        if (this.viewer.Simulator.WeatherType != WeatherType.Clear)
                        {
                            this.viewer.SoundProcess.RemoveSoundSources(this);
                            this.viewer.Simulator.WeatherType = WeatherType.Clear;
                            this.viewer.SoundProcess.AddSoundSources(this, ClearSound);
                        }
                    }
                    weatherChangeOn = false;
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSPrecipitationIntensity = -1;
                    UpdateVolume();
                });
                // Change in precipitation liquidity, passing from rain to snow and vice-versa
                viewer.UserCommandController.AddEvent(UserCommand.DebugPrecipitationLiquidityIncrease, KeyEventType.KeyDown, () =>
                {
                    weather.PrecipitationLiquidity = MathHelper.Clamp(weather.PrecipitationLiquidity + 0.01f, 0, 1);
                    weatherChangeOn = false;
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSPrecipitationLiquidity = -1;
                    if (weather.PrecipitationLiquidity > DynamicWeather.RainSnowLiquidityThreshold && this.viewer.Simulator.WeatherType != WeatherType.Rain && weather.PrecipitationIntensity > 0)
                    {
                        this.viewer.Simulator.WeatherType = WeatherType.Rain;
                        this.viewer.SoundProcess.RemoveSoundSources(this);
                        this.viewer.SoundProcess.AddSoundSources(this, RainSound);

                    }
                    UpdateVolume();
                });
                viewer.UserCommandController.AddEvent(UserCommand.DebugPrecipitationLiquidityDecrease, KeyEventType.KeyDown, () =>
                {
                    weather.PrecipitationLiquidity = MathHelper.Clamp(weather.PrecipitationLiquidity - 0.01f, 0, 1);
                    weatherChangeOn = false;
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSPrecipitationLiquidity = -1;
                    if (weather.PrecipitationLiquidity <= DynamicWeather.RainSnowLiquidityThreshold && this.viewer.Simulator.WeatherType != WeatherType.Snow
                        && weather.PrecipitationIntensity > 0)
                    {
                        this.viewer.Simulator.WeatherType = WeatherType.Snow;
                        this.viewer.SoundProcess.RemoveSoundSources(this);
                        this.viewer.SoundProcess.AddSoundSources(this, SnowSound);
                    }
                    UpdateVolume();
                });
                //    // Fog ranges from 10m (can't see anything) to 100km (clear arctic conditions).
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogIncrease, KeyEventType.KeyDown, (gameTime) =>
                {
                    weather.FogVisibilityDistance = (float)MathHelperD.Clamp(weather.FogVisibilityDistance - gameTime.ElapsedGameTime.TotalSeconds * weather.FogVisibilityDistance, 10, 100000);
                    weatherChangeOn = false;
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSFog = -1;
                });
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogDecrease, KeyEventType.KeyDown, (gameTime) =>
                {
                    weather.FogVisibilityDistance = (float)MathHelperD.Clamp(weather.FogVisibilityDistance + gameTime.ElapsedGameTime.TotalSeconds * weather.FogVisibilityDistance, 10, 100000);
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSFog = -1;
                    weatherChangeOn = false;
                });
            }

            if (!MultiPlayerManager.IsMultiPlayer())
            {
                // Shift the clock forwards or backwards at 1h-per-second.
                viewer.UserCommandController.AddEvent(UserCommand.DebugClockForwards, KeyEventType.KeyDown, (gameTime) => this.viewer.Simulator.ClockTime += gameTime.ElapsedGameTime.TotalSeconds * 3600);
                viewer.UserCommandController.AddEvent(UserCommand.DebugClockBackwards, KeyEventType.KeyDown, (gameTime) => this.viewer.Simulator.ClockTime -= gameTime.ElapsedGameTime.TotalSeconds * 3600);
            }

            // If we're a multiplayer server, send out the new overcastFactor, pricipitationIntensity and fogDistance to all clients.
            if (MultiPlayerManager.IsServer())
            {
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastIncrease, KeyEventType.KeyReleased, SendMultiPlayerWeatherChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastDecrease, KeyEventType.KeyReleased, SendMultiPlayerWeatherChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugPrecipitationIncrease, KeyEventType.KeyReleased, SendMultiPlayerWeatherChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugPrecipitationDecrease, KeyEventType.KeyReleased, SendMultiPlayerWeatherChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugPrecipitationLiquidityIncrease, KeyEventType.KeyReleased, SendMultiPlayerWeatherChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugPrecipitationLiquidityDecrease, KeyEventType.KeyReleased, SendMultiPlayerWeatherChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogIncrease, KeyEventType.KeyReleased, SendMultiPlayerWeatherChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogDecrease, KeyEventType.KeyReleased, SendMultiPlayerWeatherChangeNotification);
            }
        }

        private void SendMultiPlayerWeatherChangeNotification()
        {
            MultiPlayerManager.Broadcast(new WeatherMessage(weather));
        }

        public virtual void SaveWeatherParameters(BinaryWriter outf)
        {
            outf.Write(0); // set fixed weather
            outf.Write(weather.FogVisibilityDistance);
            outf.Write(weather.OvercastFactor);
            outf.Write(weather.PrecipitationIntensity);
            outf.Write(weather.PrecipitationLiquidity);
            outf.Write(RandomizedWeather);
            outf.Write(weatherChangeOn);
            if (weatherChangeOn)
                dynamicWeather.Save(outf);
        }

        public virtual void RestoreWeatherParameters(BinaryReader inf)
        {
            int weathercontroltype = inf.ReadInt32();

            // restoring wrong type of weather - abort
            if (weathercontroltype != 0)
                Trace.TraceError(Simulator.Catalog.GetString("Restoring wrong weather type : trying to restore dynamic weather but save contains user controlled weather"));

            weather.FogVisibilityDistance = inf.ReadSingle();
            weather.OvercastFactor = inf.ReadSingle();
            weather.PrecipitationIntensity = inf.ReadSingle();
            weather.PrecipitationLiquidity = inf.ReadSingle();
            RandomizedWeather = inf.ReadBoolean();
            weatherChangeOn = inf.ReadBoolean();
            if (weatherChangeOn)
            {
                dynamicWeather = new DynamicWeather();
                dynamicWeather.Restore(inf);
            }
            UpdateVolume();
        }

        public void SetInitialWeatherParameters()
        {
            // These values are defaults only; subsequent changes to the weather via debugging only change the components (weather, overcastFactor and fogDistance) individually.
            switch (viewer.Simulator.WeatherType)
            {
                case WeatherType.Clear:
                    weather.OvercastFactor = 0.05f;
                    weather.FogVisibilityDistance = 20000;
                    break;
                case WeatherType.Rain:
                    weather.OvercastFactor = 0.7f;
                    weather.FogVisibilityDistance = 1000;
                    break;
                case WeatherType.Snow:
                    weather.OvercastFactor = 0.6f;
                    weather.FogVisibilityDistance = 500;
                    break;
            }
        }

        public void UpdateWeatherParameters()
        {
            viewer.SoundProcess.RemoveSoundSources(this);
            switch (viewer.Simulator.WeatherType)
            {
                case WeatherType.Clear:
                    weather.PrecipitationLiquidity = 1;
                    weather.PrecipitationIntensity = 0;
                    viewer.SoundProcess.AddSoundSources(this, ClearSound);
                    break;
                case WeatherType.Rain:
                    weather.PrecipitationLiquidity = 1;
                    weather.PrecipitationIntensity = 0.010f;
                    viewer.SoundProcess.AddSoundSources(this, RainSound);
                    break;
                case WeatherType.Snow:
                    weather.PrecipitationLiquidity = 0;
                    weather.PrecipitationIntensity = 0.0050f;
                    viewer.SoundProcess.AddSoundSources(this, SnowSound);
                    break;
            }

        }

        private void UpdateSoundSources()
        {
            viewer.SoundProcess.RemoveSoundSources(this);
            switch (viewer.Simulator.WeatherType)
            {
                case WeatherType.Clear:
                    viewer.SoundProcess.AddSoundSources(this, ClearSound);
                    break;
                case WeatherType.Rain:
                    viewer.SoundProcess.AddSoundSources(this, RainSound);
                    break;
                case WeatherType.Snow:
                    viewer.SoundProcess.AddSoundSources(this, SnowSound);
                    break;
            }
        }

        private void UpdateVolume()
        {
            foreach (var soundSource in RainSound)
                soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
            foreach (var soundSource in SnowSound)
                soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
        }

        private void UpdateWind(in ElapsedTime elapsedTime)
        {
            windUpdateTimer += elapsedTime.ClockSeconds;

            if (windUpdateTimer > WindGustUpdateTimeS)
            {

                windSpeedInternalMpS = Vector2D.Zero;
                for (var i = 0; i < windSpeedMpS.Length; i++)
                {

                    windSpeedMpS[i] += new Vector2D((StaticRandom.NextDouble() * 2 - 1) * windChangeMpSS[i] * windUpdateTimer,
                        (StaticRandom.NextDouble() * 2 - 1) * windChangeMpSS[i] * windUpdateTimer);
                    var windMagnitude = windSpeedMpS[i].Length() / (i == 0 ? weather.WindSpeed.Length() * 0.4f : windSpeedMaxMpS);

                    if (windMagnitude > 1)
                        windSpeedMpS[i] /= (float)windMagnitude;

                    windSpeedInternalMpS += windSpeedMpS[i];
                }

                var TotalwindMagnitude = windSpeedInternalMpS.Length() / windSpeedMaxMpS;

                if (TotalwindMagnitude > 1)
                    windSpeedInternalMpS /= TotalwindMagnitude;

                weather.WindSpeed = new Vector2((float)windSpeedInternalMpS.X, (float)windSpeedInternalMpS.Y);
                windUpdateTimer = 0.0f; // Reset wind gust timer

                if (InitialWind) // Record the initial wind direction.
                {
                    BaseWindDirectionRad = (float)Math.Atan2(weather.WindSpeed.X, weather.WindSpeed.Y);
                    InitialWind = false; // set false so that base wind is not changed
                }

                calculatedWindDirection = (float)Math.Atan2(weather.WindSpeed.X, weather.WindSpeed.Y);

                // Test to ensure wind direction stays within the direction bandwidth set, if out of bounds set new random direction
                if (calculatedWindDirection > BaseWindDirectionRad + WindDirectionVariationRad)
                    calculatedWindDirection = BaseWindDirectionRad + WindDirectionVariationRad * (float)StaticRandom.NextDouble();


                if (calculatedWindDirection < BaseWindDirectionRad - WindDirectionVariationRad)
                    calculatedWindDirection = BaseWindDirectionRad - WindDirectionVariationRad * (float)StaticRandom.NextDouble();

                weather.CalculatedWindDirection = calculatedWindDirection;

            }
        }

        private bool RandomizeInitialWeather()
        {
            CheckDesertZone();
            if (DesertZone)
                return false;
            // First define overcast
            var randValue = StaticRandom.Next(170);
            var intermValue = randValue >= 50 ? (float)(randValue - 50f) : randValue;
            weather.OvercastFactor = intermValue >= 20 ? (float)(intermValue - 20f) / 100f : (float)intermValue / 100f; // give more probability to less overcast
            viewer.Simulator.WeatherType = WeatherType.Clear;
            // Then check if we are in precipitation zone
            if (weather.OvercastFactor > 0.5)
            {
                randValue = StaticRandom.Next(75);
                if (randValue > 40)
                {
                    weather.PrecipitationIntensity = (float)(randValue - 40f) / 1000f;
                    if (viewer.Simulator.Season == SeasonType.Winter)
                    {
                        viewer.Simulator.WeatherType = WeatherType.Snow;
                        weather.PrecipitationLiquidity = 0;
                    }
                    else
                    {
                        viewer.Simulator.WeatherType = WeatherType.Rain;
                        weather.PrecipitationLiquidity = 1;
                    }
                }
                else
                    weather.PrecipitationIntensity = 0;
            }
            else
                weather.PrecipitationIntensity = 0;
            // and now define visibility
            randValue = StaticRandom.Next(2000);
            if (weather.PrecipitationIntensity > 0 || weather.OvercastFactor > 0.7f)
                // use first digit to define power of ten and the other three to define the multiplying number
                weather.FogVisibilityDistance = Math.Max(100, (float)Math.Pow(10, randValue / 1000 + 2) * (float)((randValue % 1000 + 1) / 100f));
            else
                weather.FogVisibilityDistance = Math.Max(500, (float)Math.Pow(10, randValue / 1000 + 3) * (float)((randValue % 1000 + 1) / 100f));
            return true;
        }

        // TODO: Add several other weather conditions, such as PartlyCloudy, LightRain, 
        // HeavySnow, etc. to the Options dialog as dropdown list boxes. Transfer user's
        // selection to ActivityRunner and make appropriate adjustments to the weather here.
        // This class will eventually be expanded to interpret dynamic weather scripts and
        // make game-time weather transitions.

        private void CheckDesertZone()
        {
            // Compute player train lat/lon in degrees 
            var location = viewer.PlayerLocomotive.Train.FrontTDBTraveller;
            EarthCoordinates.ConvertWTC(location.TileX, location.TileZ, location.Location, out double latitude, out double longitude);
            float LatitudeDeg = MathHelper.ToDegrees((float)latitude);
            float LongitudeDeg = MathHelper.ToDegrees((float)longitude);

            // Compare player train lat/lon with array of desert zones
            for (int i = 0; i < DesertZones.Length / 4; i++)
                if (LatitudeDeg > DesertZones[i, 0] && LatitudeDeg < DesertZones[i, 1] && LongitudeDeg > DesertZones[i, 2] && LongitudeDeg < DesertZones[i, 3]
                     && viewer.PlayerLocomotive.Train.FrontTDBTraveller.Location.Y < 1000 ||
                     LatitudeDeg > DesertZones[i, 0] + 1 && LatitudeDeg < DesertZones[i, 1] - 1 && LongitudeDeg > DesertZones[i, 2] + 1 && LongitudeDeg < DesertZones[i, 3] - 1)
                {
                    DesertZone = true;
                    return;
                }
        }

        public virtual void Update(in ElapsedTime elapsedTime)
        {
            Time += (float)elapsedTime.ClockSeconds;
            EnvironmentalCondition updatedWeatherCondition;
            if ((updatedWeatherCondition = Simulator.Instance.UpdatedWeatherCondition) != null)
            {
                // Multiplayer weather has changed so we need to update our state to match weather, overcastFactor, pricipitationIntensity and fogDistance.
                if (updatedWeatherCondition.Weather != viewer.Simulator.WeatherType)
                {
                    Simulator.Instance.WeatherType = updatedWeatherCondition.Weather;
                    UpdateWeatherParameters();
                }
                weather.OvercastFactor = updatedWeatherCondition.OvercastFactor;
                if (updatedWeatherCondition.PrecipitationIntensity != weather.PrecipitationIntensity)
                {
                    weather.PrecipitationIntensity = updatedWeatherCondition.PrecipitationIntensity;
                    UpdateVolume();
                }
                weather.FogVisibilityDistance = updatedWeatherCondition.FogViewingDistance;
            }
            else if (MultiPlayerManager.MultiplayerState != MultiplayerState.Client)
                UpdateWind(elapsedTime);

            if (Simulator.Instance != null && Simulator.Instance.ActivityRun != null && Simulator.Instance.ActivityRun.TriggeredActivityEvent != null &&
               (Simulator.Instance.ActivityRun.TriggeredActivityEvent.ActivityEvent.WeatherChange != null || Simulator.Instance.ActivityRun.TriggeredActivityEvent.ActivityEvent.Outcomes.WeatherChange != null))
            // Start a weather change sequence in activity mode
            {
                // if not yet weather changes, create the instance
                if (dynamicWeather == null)
                    dynamicWeather = new DynamicWeather();
                OrtsWeatherChange weatherChange = Simulator.Instance.ActivityRun.TriggeredActivityEvent.ActivityEvent.WeatherChange ?? Simulator.Instance.ActivityRun.TriggeredActivityEvent.ActivityEvent.Outcomes.WeatherChange;
                dynamicWeather.WeatherChange_Init(weatherChange, this);
                Simulator.Instance.ActivityRun.TriggeredActivityEvent = null;
            }
            if (weatherChangeOn)
                dynamicWeather.WeatherChange_Update(elapsedTime, this);
            if (RandomizedWeather && !weatherChangeOn) // time to prepare a new weather change
                dynamicWeather.WeatherChange_NextRandomization(elapsedTime, this);
            if (weather.PrecipitationIntensity == 0 && viewer.Simulator.WeatherType != WeatherType.Clear)
            {
                viewer.Simulator.WeatherType = WeatherType.Clear;
                UpdateWeatherParameters();
            }
            else if (weather.PrecipitationIntensity > 0 && viewer.Simulator.WeatherType == WeatherType.Clear)
            {
                viewer.Simulator.WeatherType = weather.PrecipitationLiquidity > DynamicWeather.RainSnowLiquidityThreshold ? WeatherType.Rain : WeatherType.Snow;
                UpdateWeatherParameters();
            }
        }
    }
}
