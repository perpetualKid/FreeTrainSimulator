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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Sound;
using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Input;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.OR.Files;
using Orts.Formats.OR.Models;
using Orts.Simulation;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.World;

namespace Orts.ActivityRunner.Viewer3D
{
    public class WeatherControl
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
        private float WindDirectionVariationRad = (float)MathHelper.ToRadians(45.0f); // Set at 45 Deg
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

            this.viewer.Simulator.WeatherChanged += (object sender, EventArgs e) =>
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
                    if (dynamicWeather != null)
                        dynamicWeather.ResetWeatherTargets();
                    UpdateWeatherParameters();

                    // If we're a multiplayer server, send out the new weather to all clients.
                    if (MultiPlayerManager.IsServer())
                        MultiPlayerManager.Notify(new MSGWeather((int)this.viewer.Simulator.WeatherType, -1, -1, -1).ToString());

                });

                // Overcast ranges from 0 (completely clear) to 1 (completely overcast).
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastIncrease, KeyEventType.KeyDown, (GameTime gameTime) =>
                {
                    weather.OvercastFactor = (float)MathHelperD.Clamp(weather.OvercastFactor + gameTime.ElapsedGameTime.TotalSeconds / 10, 0, 1);
                    weatherChangeOn = false;
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSOvercast = -1;
                });
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastDecrease, KeyEventType.KeyDown, (GameTime gameTime) =>
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
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogIncrease, KeyEventType.KeyDown, (GameTime gameTime) =>
                {
                    weather.FogVisibilityDistance = (float)MathHelperD.Clamp(weather.FogVisibilityDistance - gameTime.ElapsedGameTime.TotalSeconds * weather.FogVisibilityDistance, 10, 100000);
                    weatherChangeOn = false;
                    if (dynamicWeather != null)
                        dynamicWeather.ORTSFog = -1;
                });
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogDecrease, KeyEventType.KeyDown, (GameTime gameTime) =>
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
                viewer.UserCommandController.AddEvent(UserCommand.DebugClockForwards, KeyEventType.KeyDown, (GameTime gameTime) => this.viewer.Simulator.ClockTime += gameTime.ElapsedGameTime.TotalSeconds * 3600);
                viewer.UserCommandController.AddEvent(UserCommand.DebugClockBackwards, KeyEventType.KeyDown, (GameTime gameTime) => this.viewer.Simulator.ClockTime -= gameTime.ElapsedGameTime.TotalSeconds * 3600);
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
            MultiPlayerManager.Instance().SetEnvInfo(weather.OvercastFactor, weather.FogVisibilityDistance);
            MultiPlayerManager.Notify((new MSGWeather(-1, weather.OvercastFactor, weather.PrecipitationIntensity, weather.FogVisibilityDistance)).ToString());
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
            {
                dynamicWeather.Save(outf);
            }
        }

        public virtual void RestoreWeatherParameters(BinaryReader inf)
        {
            int weathercontroltype = inf.ReadInt32();

            // restoring wrong type of weather - abort
            if (weathercontroltype != 0)
            {
                Trace.TraceError(Simulator.Catalog.GetString("Restoring wrong weather type : trying to restore dynamic weather but save contains user controlled weather"));
            }

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

                var TotalwindMagnitude = windSpeedInternalMpS.Length() / (windSpeedMaxMpS);

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
                if (calculatedWindDirection > (BaseWindDirectionRad + WindDirectionVariationRad))
                    calculatedWindDirection = BaseWindDirectionRad + (WindDirectionVariationRad * (float)StaticRandom.NextDouble());


                if (calculatedWindDirection < (BaseWindDirectionRad - WindDirectionVariationRad))
                    calculatedWindDirection = BaseWindDirectionRad - (WindDirectionVariationRad * (float)StaticRandom.NextDouble());

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
            var intermValue = randValue >= 50 ? (float)(randValue - 50f) : (float)randValue;
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
                weather.FogVisibilityDistance = Math.Max(100, (float)Math.Pow(10, ((int)(randValue / 1000) + 2)) * (float)((randValue % 1000 + 1) / 100f));
            else
                weather.FogVisibilityDistance = Math.Max(500, (float)Math.Pow(10, (int)((randValue / 1000) + 3)) * (float)((randValue % 1000 + 1) / 100f));
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
            {
                if (LatitudeDeg > DesertZones[i, 0] && LatitudeDeg < DesertZones[i, 1] && LongitudeDeg > DesertZones[i, 2] && LongitudeDeg < DesertZones[i, 3]
                     && viewer.PlayerLocomotive.Train.FrontTDBTraveller.Location.Y < 1000 ||
                     LatitudeDeg > DesertZones[i, 0] + 1 && LatitudeDeg < DesertZones[i, 1] - 1 && LongitudeDeg > DesertZones[i, 2] + 1 && LongitudeDeg < DesertZones[i, 3] - 1)
                {
                    DesertZone = true;
                    return;
                }
            }
        }

        public virtual void Update(in ElapsedTime elapsedTime)
        {
            Time += (float)elapsedTime.ClockSeconds;
            MultiPlayerManager manager;
            if (MultiPlayerManager.MultiplayerState == MultiplayerState.Client && (manager = MultiPlayerManager.Instance()).weatherChanged)
            {
                // Multiplayer weather has changed so we need to update our state to match weather, overcastFactor, pricipitationIntensity and fogDistance.
                if (manager.weather >= 0 && manager.weather != (int)viewer.Simulator.WeatherType)
                {
                    viewer.Simulator.WeatherType = (WeatherType)manager.weather;
                    UpdateWeatherParameters();
                }
                if (manager.overcastFactor >= 0)
                    weather.OvercastFactor = manager.overcastFactor;
                if (manager.pricipitationIntensity >= 0)
                {
                    weather.PrecipitationIntensity = manager.pricipitationIntensity;
                    UpdateVolume();
                }
                if (manager.fogDistance >= 0)
                    weather.FogVisibilityDistance = manager.fogDistance;

                // Reset the message now that we've applied all the changes.
                if ((manager.weather >= 0 && manager.weather != (int)viewer.Simulator.WeatherType) || manager.overcastFactor >= 0 || manager.pricipitationIntensity >= 0 || manager.fogDistance >= 0)
                {
                    manager.weatherChanged = false;
                    manager.weather = -1;
                    manager.overcastFactor = -1;
                    manager.pricipitationIntensity = -1;
                    manager.fogDistance = -1;
                }
            }
            else if (MultiPlayerManager.MultiplayerState != MultiplayerState.Client)
            {
                UpdateWind(elapsedTime);
            }

            if (Simulator.Instance != null && Simulator.Instance.ActivityRun != null && Simulator.Instance.ActivityRun.TriggeredActivityEvent != null &&
               (Simulator.Instance.ActivityRun.TriggeredActivityEvent.ActivityEvent.WeatherChange != null || Simulator.Instance.ActivityRun.TriggeredActivityEvent.ActivityEvent.Outcomes.WeatherChange != null))
            // Start a weather change sequence in activity mode
            {
                // if not yet weather changes, create the instance
                if (dynamicWeather == null)
                {
                    dynamicWeather = new DynamicWeather();
                }
                OrtsWeatherChange weatherChange = Simulator.Instance.ActivityRun.TriggeredActivityEvent.ActivityEvent.WeatherChange ?? Simulator.Instance.ActivityRun.TriggeredActivityEvent.ActivityEvent.Outcomes.WeatherChange;
                dynamicWeather.WeatherChange_Init(weatherChange, this);
                Simulator.Instance.ActivityRun.TriggeredActivityEvent = null;
            }
            if (weatherChangeOn)
            // manage the weather change sequence
            {
                dynamicWeather.WeatherChange_Update(elapsedTime, this);
            }
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
                    overcastTimer = (float)ORTSOvercastTransitionTimeS;
                    overcastChangeRate = overcastTimer > 0 ? (MathHelper.Clamp(ORTSOvercast, 0, 1.0f) - weatherControl.weather.OvercastFactor) / ORTSOvercastTransitionTimeS : 0;
                    wChangeOn = true;
                }
                if (eventWeatherChange.Fog >= 0 && eventWeatherChange.FogTransitionTime >= 0)
                {
                    ORTSFog = eventWeatherChange.Fog;
                    ORTSFogTransitionTimeS = eventWeatherChange.FogTransitionTime;
                    fogTimer = (float)ORTSFogTransitionTimeS;
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
                    precipitationIntensityTimer = (float)ORTSPrecipitationIntensityTransitionTimeS;
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
                    precipitationLiquidityTimer = (float)ORTSPrecipitationLiquidityTransitionTimeS;
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
                    {
                        if (oldPrecipitationIntensityPPSPM2 > 0)
                        {
                            weatherControl.viewer.Simulator.WeatherType = WeatherType.Clear;
                            weatherControl.UpdateSoundSources();
                        }
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
                    {
                        if (oldPrecipitationLiquidity <= RainSnowLiquidityThreshold)
                        {
                            weatherControl.viewer.Simulator.WeatherType = WeatherType.Rain;
                            weatherControl.UpdateSoundSources();
                            weatherControl.UpdateVolume();
                        }
                    }
                    if (weatherControl.weather.PrecipitationLiquidity <= RainSnowLiquidityThreshold)
                    {
                        if (oldPrecipitationLiquidity > RainSnowLiquidityThreshold)
                        {
                            weatherControl.viewer.Simulator.WeatherType = WeatherType.Snow;
                            weatherControl.UpdateSoundSources();
                            weatherControl.UpdateVolume();
                        }
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
                var intermValue = randValue >= 50 ? (float)(randValue - 50f) : (float)randValue;
                ORTSOvercast = intermValue >= 20 ? (float)(intermValue - 20f) / 100f : (float)intermValue / 100f; // give more probability to less overcast
                ORTSOvercastTransitionTimeS = weatherChangeTimer;
                overcastTimer = (float)ORTSOvercastTransitionTimeS;
                overcastChangeRate = overcastTimer > 0 ? (MathHelper.Clamp(ORTSOvercast, 0, 1.0f) - weatherControl.weather.OvercastFactor) / ORTSOvercastTransitionTimeS : 0;
                // Then check if we are in precipitation zone
                if (ORTSOvercast > 0.5)
                {
                    randValue = StaticRandom.Next(75);
                    if (randValue > 40)
                    {
                        ORTSPrecipitationIntensity = (float)(randValue - 40f) / 1000f;
                        if (weatherControl.viewer.Simulator.Season == SeasonType.Winter)
                        {
                            weatherControl.weather.PrecipitationLiquidity = 0;
                        }
                        else
                        {
                            weatherControl.weather.PrecipitationLiquidity = 1;
                        }
                    }
                }
                if (weatherControl.weather.PrecipitationIntensity > 0 && ORTSPrecipitationIntensity == -1)
                {
                    ORTSPrecipitationIntensity = 0;
                    // must return to zero before overcast < 0.5
                    ORTSPrecipitationIntensityTransitionTimeS = (int)((0.5 - weatherControl.weather.OvercastFactor) / overcastChangeRate);
                }
                if (weatherControl.weather.PrecipitationIntensity == 0 && ORTSPrecipitationIntensity > 0 && weatherControl.weather.OvercastFactor < 0.5)
                {
                    // we will have precipitation now, but it must start after overcast is over 0.5
                    precipitationIntensityDelayTimer = (0.5f - weatherControl.weather.OvercastFactor) / overcastChangeRate;
                }

                if (ORTSPrecipitationIntensity > 0)
                {
                    ORTSPrecipitationIntensityTransitionTimeS = weatherChangeTimer;
                }
                if (ORTSPrecipitationIntensity >= 0)
                {
                    precipitationIntensityTimer = (float)ORTSPrecipitationIntensityTransitionTimeS;
                    // Precipitation ranges from 0 to max PrecipitationViewer.MaxIntensityPPSPM2 if 32bit.
                    precipitationIntensityChangeRate = precipitationIntensityTimer > 0 ? (MathHelper.Clamp(ORTSPrecipitationIntensity, 0, PrecipitationViewer.MaxIntensityPPSPM2)
                        - weatherControl.weather.PrecipitationIntensity) / ORTSPrecipitationIntensityTransitionTimeS : 0;
                }

                // and now define visibility
                randValue = StaticRandom.Next(2000);
                if (ORTSPrecipitationIntensity > 0 || ORTSOvercast > 0.7f)
                    // use first digit to define power of ten and the other three to define the multiplying number
                    ORTSFog = Math.Max(100, (float)Math.Pow(10, ((int)(randValue / 1000) + 2)) * (float)((randValue % 1000 + 1) / 100f));
                else
                    ORTSFog = Math.Max(500, (float)Math.Pow(10, (int)((randValue / 1000) + 3)) * (float)((randValue % 1000 + 1) / 100f));
                ORTSFogTransitionTimeS = weatherChangeTimer;
                fogTimer = (float)ORTSFogTransitionTimeS;
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


    public class AutomaticWeather : WeatherControl
    {
        // Variables used for auto weather control
        // settings
        private List<WeatherCondition> weatherDetails = new List<WeatherCondition>();

        // running values
        // general
        public int AWActiveIndex;                                        // active active index in waether list
        public float AWNextChangeTime;                                   // time for next change
        public float AWLastVisibility;                                   // visibility at end of previous weather
        public WeatherType AWPrecipitationActiveType;  // actual active precipitation

        // cloud
        public float AWOvercastCloudcover;                               // actual cloudcover
        public float AWOvercastCloudRateOfChangepS;                      // rate of change of cloudcover

        // precipitation
        public WeatherType AWPrecipitationRequiredType;// actual active precipitation
        public float AWPrecipitationTotalDuration;                       // actual total duration (seconds)
        public int AWPrecipitationTotalSpread;                           // actual number of periods with precipitation
        public float AWPrecipitationActualPPSPM2;                        // actual rate of precipitation (particals per second per square meter)
        public float AWPrecipitationRequiredPPSPM2;                      // required rate of precipitation
        public float AWPrecipitationRateOfChangePPSPM2PS;                // rate of change for rate of precipitation (particals per second per square meter per second)
        public float AWPrecipitationEndSpell;                            // end of present spell of precipitation (time in seconds)
        public float AWPrecipitationNextSpell;                           // start of next spell (time in seconds) (-1 if no further spells)
        public float AWPrecipitationStartRate;                           // rate of change at start of spell
        public float AWPrecipitationEndRate;                             // rate of change at end of spell

        // fog
        public float AWActualVisibility;                                    // actual fog visibility
        public float AWFogChangeRateMpS;                                 // required rate of change for fog
        public float AWFogLiftTime;                                      // start time of fog lifting to be clear at required time

        // wind
        public float AWPreviousWindSpeed;                                // windspeed at end of previous weather
        public float AWRequiredWindSpeed;                                // required wind speed at end of weather
        public float AWAverageWindSpeed;                                 // required average windspeed
        public float AWAverageWindGust;                                  // required average additional wind gust
        public float AWWindGustTime;                                     // time of next wind gust
        public float AWActualWindSpeed;                                  // actual wind speed
        public float AWWindSpeedChange;                                  // required change of wind speed
        public float AWRequiredWindDirection;                            // required wind direction at end of weather
        public float AWAverageWindDirection;                             // required average wind direction
        public float AWActualWindDirection;                              // actual wind direction
        public float AWWindDirectionChange;                              // required wind direction change

        public AutomaticWeather(Viewer viewer, string fileName, double realTime)
            : base(viewer)
        {
            // read weather details from file
            WeatherFile weatherFile = new WeatherFile(fileName);
            weatherDetails = weatherFile.Changes;

            if (weatherDetails.Count == 0)
            {
                Trace.TraceWarning("Weather file contains no settings {0}", weatherFile);
            }
            else
            {
                CheckWeatherDetails();
            }

            // set initial weather parameters
            SetInitialWeatherParameters(realTime);
        }

        // dummy constructor for restore
        public AutomaticWeather(Viewer viewer)
            : base(viewer)
        {
        }

        // check weather details, set auto variables
        private void CheckWeatherDetails()
        {
            float prevTime = 0;

            foreach (WeatherCondition weatherSet in weatherDetails)
            {
                TimeSpan acttime = new TimeSpan((long)(weatherSet.Time * 10000000));

                // check if time is in sequence
                if (weatherSet.Time < prevTime)
                {
                    Trace.TraceInformation("Invalid time value : time out of sequence : {0}", acttime.ToString());
                    weatherSet.UpdateTime(prevTime + 1);
                }
                prevTime = weatherSet.Time;

                weatherSet.Check(acttime);
            }
        }

        // set initial weather parameters
        private void SetInitialWeatherParameters(double realTime)
        {
            Time = (float)realTime;

            // find last valid weather change
            AWActiveIndex = 0;
            var passedTime = false;

            if (weatherDetails.Count == 0)
                return;

            for (var iIndex = 1; iIndex < weatherDetails.Count && !passedTime; iIndex++)
            {
                if (weatherDetails[iIndex].Time > Time)
                {
                    passedTime = true;
                    AWActiveIndex = iIndex - 1;
                }
            }

            // get last weather
#if DEBUG_AUTOWEATHER
            Trace.TraceInformation("Initial active weather : {0}", AWActiveIndex);
#endif
            WeatherCondition lastWeather = weatherDetails[AWActiveIndex];

            AWNextChangeTime = AWActiveIndex < (weatherDetails.Count - 1) ? weatherDetails[AWActiveIndex + 1].Time : (24 * 3600);
            int nextIndex = AWActiveIndex < (weatherDetails.Count - 1) ? AWActiveIndex + 1 : -1;

            // fog
            if (lastWeather is FogCondition fogCondition)
            {
                float actualLiftingTime = (0.9f * fogCondition.LiftTime) + (((float)StaticRandom.Next(10) / 100) * fogCondition.LiftTime); // defined time +- 10%
                AWFogLiftTime = AWNextChangeTime - actualLiftingTime;

                // check if fog is allready lifting
                if ((float)realTime > AWFogLiftTime && nextIndex > 1)
                {
                    float reqVisibility = GetWeatherVisibility(weatherDetails[nextIndex]);
                    float remainingFactor = ((float)realTime - AWNextChangeTime + actualLiftingTime) / actualLiftingTime;
                    AWActualVisibility = fogCondition.Visibility + (remainingFactor * remainingFactor * (reqVisibility - fogCondition.Visibility));
                    AWOvercastCloudcover = fogCondition.Overcast / 100;
                }
                else
                {
                    StartFog(fogCondition, (float)realTime, AWActiveIndex);
                }
            }

            // precipitation
            else if (lastWeather is PrecipitationCondition precipitationCondition)
            {
                StartPrecipitation(precipitationCondition, (float)realTime, true);
            }

            // cloudcover
            else if (lastWeather is OvercastCondition overcastCondition)
            {
                AWOvercastCloudcover = Math.Max(0, Math.Min(1, (overcastCondition.Overcast / 100) +
                    ((float)StaticRandom.Next((int)(-0.5f * overcastCondition.Variation), (int)(0.5f * overcastCondition.Variation)) / 100)));
                AWActualVisibility = weather.FogVisibilityDistance = overcastCondition.Visibility;

#if DEBUG_AUTOWEATHER
                Trace.TraceInformation("Visibility : {0}", Weather.FogDistance);
#endif

            }

            // set system weather parameters
            viewer.SoundProcess.RemoveSoundSources(this);
            viewer.Simulator.WeatherType = AWPrecipitationActiveType;

            switch (AWPrecipitationActiveType)
            {
                case WeatherType.Rain:
                    weather.PrecipitationIntensity = AWPrecipitationActualPPSPM2;
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, RainSound);
                    foreach (var soundSource in RainSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
#if DEBUG_AUTOWEATHER
                    Trace.TraceInformation("Weather type RAIN");
#endif
                    break;

                case WeatherType.Snow:
                    weather.PrecipitationIntensity = AWPrecipitationActualPPSPM2;
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, SnowSound);
                    foreach (var soundSource in SnowSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
#if DEBUG_AUTOWEATHER
                    Trace.TraceInformation("Weather type SNOW");
#endif
                    break;

                default:
                    weather.PrecipitationIntensity = 0;
                    viewer.SoundProcess.AddSoundSources(this, ClearSound);
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
#if DEBUG_AUTOWEATHER
                    Trace.TraceInformation("Weather type CLEAR");
#endif
                    break;
            }

#if DEBUG_AUTOWEATHER
            Trace.TraceInformation("Overcast : {0}\nPrecipitation : {1}\n Visibility : {2}",
                Weather.OvercastFactor, Weather.PricipitationIntensityPPSPM2, Weather.FogDistance);
#endif
        }

        public override void Update(in ElapsedTime elapsedTime)
        {
            // not client and weather auto mode
            Time += (float)elapsedTime.ClockSeconds;
            var fogActive = false;

            if (weatherDetails.Count == 0)
                return;

            WeatherCondition lastWeather = weatherDetails[AWActiveIndex];
            int nextIndex = AWActiveIndex < (weatherDetails.Count - 1) ? AWActiveIndex + 1 : -1;
            fogActive = false;

            // check for fog
            if (lastWeather is FogCondition fogCondition)
            {
                CalculateFog(fogCondition, nextIndex);
                fogActive = true;

                // if fog has lifted, change to next sequence
                if (Time > (AWNextChangeTime - fogCondition.LiftTime) && AWActualVisibility >= 19999 && AWActiveIndex < (weatherDetails.Count - 1))
                {
                    fogActive = false;
                    AWNextChangeTime = Time - 1;  // force change to next weather
                }
            }

            // check for precipitation
            else if (lastWeather is PrecipitationCondition precipitationCondition)
            {
                // precipitation not active
                if (AWPrecipitationActiveType == WeatherType.Clear)
                {
                    // if beyond start of next spell start precipitation
                    if (Time > AWPrecipitationNextSpell)
                    {
                        // if cloud has build up
                        if (AWOvercastCloudcover >= (precipitationCondition.OvercastPrecipitationStart / 100))
                        {
                            StartPrecipitationSpell(precipitationCondition, AWNextChangeTime);
                            CalculatePrecipitation(precipitationCondition, elapsedTime);
                        }
                        // build up cloud
                        else
                        {
                            AWOvercastCloudcover = CalculateOvercast(precipitationCondition.OvercastPrecipitationStart, 0, precipitationCondition.OvercastBuildUp, elapsedTime);
                        }
                    }
                    // set overcast and visibility
                    else
                    {
                        AWOvercastCloudcover = CalculateOvercast(precipitationCondition.Overcast.Overcast, precipitationCondition.Overcast.Variation, precipitationCondition.Overcast.RateOfChange, elapsedTime);
                        if (weather.FogVisibilityDistance > precipitationCondition.Overcast.Visibility)
                        {
                            AWActualVisibility = weather.FogVisibilityDistance - 40 * (float)elapsedTime.RealSeconds; // reduce visibility by 40 m/s
                        }
                        else if (weather.FogVisibilityDistance < precipitationCondition.Overcast.Visibility)
                        {
                            AWActualVisibility = weather.FogVisibilityDistance + 40 * (float)elapsedTime.RealSeconds; // increase visibility by 40 m/s
                        }
                    }
                }
                // active precipitation
                // if beyond end of spell : decrease densitity, if density below minimum threshold stop precipitation
                else if (Time > AWPrecipitationEndSpell)
                {
                    StopPrecipitationSpell(precipitationCondition, elapsedTime);
                    // if density dropped under min threshold precipitation has ended
                    if (AWPrecipitationActualPPSPM2 <= PrecipitationViewer.MinIntensityPPSPM2)
                    {
                        AWPrecipitationActiveType = WeatherType.Clear;
#if DEBUG_AUTOWEATHER
                        Trace.TraceInformation("Start of clear spell, duration : {0}", (AWPrecipitationNextSpell - Time));
                        TimeSpan wt = new TimeSpan((long)(AWPrecipitationNextSpell * 10000000));
                        Trace.TraceInformation("Next spell : {0}", wt.ToString());
#endif                    
                    }
                }
                // active precipitation : set density and related visibility
                else
                {
                    CalculatePrecipitation(precipitationCondition, elapsedTime);
                }
            }
            // clear
            else if (lastWeather is OvercastCondition overcastCondition)
            {
                AWOvercastCloudcover = CalculateOvercast(overcastCondition.Overcast, overcastCondition.Variation, overcastCondition.RateOfChange, elapsedTime);
                if (AWActualVisibility > overcastCondition.Visibility)
                {
                    AWActualVisibility = (float)Math.Max(overcastCondition.Visibility, AWActualVisibility - 40 * elapsedTime.RealSeconds); // reduce visibility by 40 m/s
                }
                else if (AWActualVisibility < overcastCondition.Visibility)
                {
                    AWActualVisibility = (float)Math.Min(overcastCondition.Visibility, AWActualVisibility + 40 * elapsedTime.RealSeconds); // increase visibility by 40 m/s
                }
            }

            // set weather parameters
            viewer.SoundProcess.RemoveSoundSources(this);
            viewer.Simulator.WeatherType = AWPrecipitationActiveType;

            switch (AWPrecipitationActiveType)
            {
                case WeatherType.Rain:
                    weather.PrecipitationIntensity = AWPrecipitationActualPPSPM2;
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, RainSound);
                    foreach (var soundSource in RainSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                case WeatherType.Snow:
                    weather.PrecipitationIntensity = AWPrecipitationActualPPSPM2;
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, SnowSound);
                    foreach (var soundSource in SnowSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                default:
                    weather.PrecipitationIntensity = 0;
                    viewer.SoundProcess.AddSoundSources(this, ClearSound);
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
                    break;
            }

            // check for change in required weather
            // time to change but no change after midnight and further weather available
            if (Time < 24 * 3600 && Time > AWNextChangeTime && AWActiveIndex < (weatherDetails.Count - 1))
            {
                // if precipitation still active or fog not lifted, postpone change by one minute
                if (AWPrecipitationActiveType != WeatherType.Clear || fogActive)
                {
                    AWNextChangeTime += 60;
                }
                else
                {
                    AWActiveIndex++;
                    AWNextChangeTime = AWActiveIndex < (weatherDetails.Count - 2) ? weatherDetails[AWActiveIndex + 1].Time : 24 * 3600;

#if DEBUG_AUTOWEATHER
                    Trace.TraceInformation("Weather change : index {0}, type {1}", AWActiveIndex, weatherDetails[AWActiveIndex].GetType().ToString());
#endif                    

                    WeatherCondition nextWeather = weatherDetails[AWActiveIndex];
                    if (nextWeather is FogCondition nextfogCondition)
                    {
                        StartFog(nextfogCondition, Time, AWActiveIndex);
                    }
                    else if (nextWeather is PrecipitationCondition nextprecipitationCondition)
                    {
                        StartPrecipitation(nextprecipitationCondition, Time, false);
                    }
                }
            }
        }

        private float GetWeatherVisibility(WeatherCondition weatherDetail)
        {
            float nextVisibility = weather.FogVisibilityDistance; // present visibility
            if (weatherDetail is FogCondition fogCondition)
            {
                nextVisibility = fogCondition.Visibility;
            }
            else if (weatherDetail is OvercastCondition overcastCondition)
            {
                nextVisibility = overcastCondition.Visibility;
            }
            else if (weatherDetail is PrecipitationCondition precipitationCondition)
            {
                nextVisibility = precipitationCondition.Overcast.Visibility;
            }
            return (nextVisibility);
        }

        private void StartFog(FogCondition fogCondition, float startTime, int activeIndex)
        {
            // fog fully set or fog at start of day
            if (startTime > (fogCondition.Time + fogCondition.SetTime) || activeIndex == 0)
            {
                AWActualVisibility = fogCondition.Visibility;
            }
            // fog still setting
            else
            {
                float remainingFactor = (startTime - fogCondition.Time + fogCondition.SetTime) / fogCondition.SetTime;
                AWActualVisibility = MathHelper.Clamp(AWActualVisibility - (remainingFactor * remainingFactor * (AWActualVisibility - fogCondition.Visibility)), fogCondition.Visibility, AWActualVisibility);
            }
        }

        private void CalculateFog(FogCondition fogCondition, int nextIndex)
        {
            if (AWFogLiftTime > 0 && Time > AWFogLiftTime && nextIndex > 0) // fog is lifting
            {
                float reqVisibility = GetWeatherVisibility(weatherDetails[nextIndex]);
                float remainingFactor = (Time - weatherDetails[nextIndex].Time + fogCondition.LiftTime) / fogCondition.LiftTime;
                AWActualVisibility = fogCondition.Visibility + (remainingFactor * remainingFactor * (reqVisibility - fogCondition.Visibility));
                AWOvercastCloudcover = fogCondition.Overcast / 100;
            }
            else if (AWActualVisibility > fogCondition.Visibility)
            {
                float remainingFactor = (Time - fogCondition.Time + fogCondition.SetTime) / fogCondition.SetTime;
                AWActualVisibility = MathHelper.Clamp(AWLastVisibility - (remainingFactor * remainingFactor * (AWLastVisibility - fogCondition.Visibility)), fogCondition.Visibility, AWLastVisibility);
            }
        }

        private void StartPrecipitation(PrecipitationCondition precipitationCondition, float startTime, bool allowImmediateStart)
        {
            AWPrecipitationRequiredType = precipitationCondition.PrecipitationType;

            // determine actual duration of precipitation
            float maxDuration = AWNextChangeTime - weatherDetails[AWActiveIndex].Time;
            AWPrecipitationTotalDuration = (float)maxDuration * (precipitationCondition.Probability / 100f);  // nominal value
            AWPrecipitationTotalDuration = (0.9f + ((float)StaticRandom.Next(20) / 20)) * AWPrecipitationTotalDuration; // randomized value, +- 10% 
            AWPrecipitationTotalDuration = Math.Min(AWPrecipitationTotalDuration, maxDuration); // but never exceeding maximum duration
            AWPrecipitationNextSpell = precipitationCondition.Time; // set start of spell to start of weather change

            // determine spread : no. of periods with precipitation (no. of showers)
            if (precipitationCondition.Spread == 1)
            {
                AWPrecipitationTotalSpread = 1;
            }
            else
            {
                AWPrecipitationTotalSpread = Math.Max(1, (int)((0.9f + ((float)StaticRandom.Next(20) / 20)) * precipitationCondition.Spread));
                if ((AWPrecipitationTotalDuration / AWPrecipitationTotalSpread) < 900) // length of spell at least 15 mins
                {
                    AWPrecipitationTotalSpread = (int)(AWPrecipitationTotalDuration / 900);
                }
            }


            // determine actual precipitation state - only if immediate start allowed
            bool precipitationActive = allowImmediateStart && StaticRandom.Next(100) >= precipitationCondition.Probability;

#if DEBUG_AUTOWEATHER
            Trace.TraceInformation("Precipitation active on start : {0}", precipitationActive.ToString());
#endif                    

            // determine total remaining time as well as remaining periods, based on start/end time and present time
            // this is independent from actual precipitation state

            if (AWPrecipitationTotalSpread > 1)
            {
                AWPrecipitationTotalDuration = ((float)((AWNextChangeTime - startTime) / (AWNextChangeTime - weatherDetails[AWActiveIndex].Time))) * AWPrecipitationTotalDuration;
                AWPrecipitationTotalSpread = (int)(((float)((AWNextChangeTime - startTime) / (AWNextChangeTime - weatherDetails[AWActiveIndex].Time))) * AWPrecipitationTotalSpread);
            }

            // set actual details
            if (precipitationActive)
            {
                // precipitation active : set actual details, calculate end of present spell
                int precvariation = (int)(precipitationCondition.Variation * 100);
                float baseDensitiy = PrecipitationViewer.MaxIntensityPPSPM2 * precipitationCondition.Density;
                AWPrecipitationActualPPSPM2 = MathHelper.Clamp(((1.0f + (StaticRandom.Next(-precvariation, precvariation) / 100f)) * baseDensitiy),
                                               PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
                AWPrecipitationRequiredPPSPM2 = MathHelper.Clamp(((1.0f + (StaticRandom.Next(-precvariation, precvariation) / 100f)) * baseDensitiy),
                                               PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);

                // rate of change is max. difference over random timespan between 1 and 10 mins.
                // startphase
                float startrate = 1.75f * precipitationCondition.RateOfChange +
                                           (0.5F * StaticRandom.Next((int)(precipitationCondition.RateOfChange * 100)) / 100f);
                float spellStartPhase = Math.Min(60f + (300f * startrate), 600);
                AWPrecipitationStartRate = (AWPrecipitationRequiredPPSPM2 - AWPrecipitationActualPPSPM2) / spellStartPhase;

                // endphase
                float endrate = 1.75f * precipitationCondition.RateOfChange +
                               (0.5f * StaticRandom.Next((int)(precipitationCondition.RateOfChange * 100)) / 100f);
                float spellEndPhase = Math.Min(60f + (300f * endrate), 600);

                float avduration = AWPrecipitationTotalDuration / AWPrecipitationTotalSpread;
                float actduration = (0.5f + (StaticRandom.Next(100) / 100f)) * avduration;
                float spellEndTime = Math.Min(startTime + actduration, AWNextChangeTime);
                AWPrecipitationEndSpell = Math.Max(startTime, spellEndTime - spellEndPhase);
                // for end rate, use minimum precipitation
                AWPrecipitationEndRate = (AWPrecipitationActualPPSPM2 - PrecipitationViewer.MinIntensityPPSPM2) / spellEndPhase;
                AWPrecipitationTotalDuration -= actduration;
                AWPrecipitationTotalSpread -= 1;

                // calculate length of clear period and start of next spell
                if (AWPrecipitationTotalDuration > 0 && AWPrecipitationTotalSpread > 0)
                {
                    float avclearspell = (AWNextChangeTime - startTime - AWPrecipitationTotalDuration) / AWPrecipitationTotalSpread;
                    AWPrecipitationNextSpell = spellEndTime + (0.9f + (StaticRandom.Next(200) / 1000f)) * avclearspell;
                }
                else
                {
                    AWPrecipitationNextSpell = AWNextChangeTime + 1; // set beyond next weather such that it never occurs
                }

                // set active values
                AWPrecipitationActiveType = precipitationCondition.PrecipitationType;
                AWOvercastCloudcover = precipitationCondition.OvercastPrecipitationStart / 100;  // fixed cloudcover during precipitation
                AWActualVisibility = precipitationCondition.VisibilityAtMinDensity + (float)(Math.Sqrt(AWPrecipitationActualPPSPM2 / PrecipitationViewer.MaxIntensityPPSPM2) *
                    (precipitationCondition.VisibilityAtMaxDensity - precipitationCondition.VisibilityAtMinDensity));
                AWLastVisibility = precipitationCondition.VisibilityAtMinDensity; // fix last visibility to visibility at minimum density
            }
            else
            // if presently not active, set start of next spell
            {
                if (AWPrecipitationTotalSpread < 1)
                {
                    AWPrecipitationNextSpell = -1;
                }
                else
                {
                    int clearSpell = (int)((AWNextChangeTime - startTime - AWPrecipitationTotalDuration) / AWPrecipitationTotalSpread);
                    AWPrecipitationNextSpell = clearSpell > 0 ? startTime + StaticRandom.Next(clearSpell) : startTime;

                    if (allowImmediateStart)
                    {
                        AWOvercastCloudcover = precipitationCondition.Overcast.Overcast / 100;
                        AWActualVisibility = precipitationCondition.Overcast.Visibility;
                    }

#if DEBUG_AUTOWEATHER
                    TimeSpan wt = new TimeSpan((long)(AWPrecipitationNextSpell * 10000000));
                    Trace.TraceInformation("Next spell : {0}", wt.ToString());
#endif                    
                }

                AWPrecipitationActiveType = WeatherType.Clear;
            }
        }

        private void StartPrecipitationSpell(PrecipitationCondition precipitationCondition, float nextWeatherTime)
        {
            int precvariation = (int)(precipitationCondition.Variation * 100);
            float baseDensitiy = PrecipitationViewer.MaxIntensityPPSPM2 * precipitationCondition.Density;
            AWPrecipitationActiveType = AWPrecipitationRequiredType;
            AWPrecipitationActualPPSPM2 = PrecipitationViewer.MinIntensityPPSPM2;
            AWPrecipitationRequiredPPSPM2 = MathHelper.Clamp(((1.0f + (StaticRandom.Next(-precvariation, precvariation) / 100f)) * baseDensitiy),
                                           PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
            AWLastVisibility = weather.FogVisibilityDistance;

            // rate of change at start is max. difference over defined time span +- 10%, scaled between 1/2 and 4 mins
            float startphase = MathHelper.Clamp(precipitationCondition.PrecipitationStartPhase * (0.9f + (StaticRandom.Next(100) / 1000f)), 30, 240);
            AWPrecipitationStartRate = (AWPrecipitationRequiredPPSPM2 - AWPrecipitationActualPPSPM2) / startphase;
            AWPrecipitationRateOfChangePPSPM2PS = AWPrecipitationStartRate;

            // rate of change at end is max. difference over defined time span +- 10%, scaled between 1/2 and 6 mins
            float endphase = MathHelper.Clamp(precipitationCondition.PrecipitationEndPhase * (0.9f + (StaticRandom.Next(100) / 1000f)), 30, 360);
            AWPrecipitationEndRate = (AWPrecipitationRequiredPPSPM2 - AWPrecipitationActualPPSPM2) / endphase;

            // calculate end of spell and start of next spell
            if (AWPrecipitationTotalSpread > 1)
            {
                float avduration = AWPrecipitationTotalDuration / AWPrecipitationTotalSpread;
                float actduration = (0.5f + (StaticRandom.Next(100) / 100f)) * avduration;
                float spellEndTime = Math.Min(Time + actduration, AWNextChangeTime);
                AWPrecipitationEndSpell = Math.Max(Time, spellEndTime - endphase);

                AWPrecipitationTotalDuration -= actduration;
                AWPrecipitationTotalSpread -= 1;

                int clearSpell = (int)((nextWeatherTime - spellEndTime - AWPrecipitationTotalDuration) / AWPrecipitationTotalSpread);
                AWPrecipitationNextSpell = spellEndTime + 60f; // always a minute between spells
                AWPrecipitationNextSpell = clearSpell > 0 ? AWPrecipitationNextSpell + StaticRandom.Next(clearSpell) : AWPrecipitationNextSpell;
            }
            else
            {
                AWPrecipitationEndSpell = Math.Max(Time, nextWeatherTime - endphase);
            }

#if DEBUG_AUTOWEATHER
            Trace.TraceInformation("Start next spell, duration : {0} , start phase : {1} , end phase {2}, density {3} (of max. {4}) , rate of change : {5} - {6} - {7}",
                                    (AWPrecipitationEndSpell - Time), startphase, endphase, AWPrecipitationRequiredPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2, 
                                    AWPrecipitationRateOfChangePPSPM2PS, AWPrecipitationStartRate, AWPrecipitationEndRate);
#endif

        }

        private void CalculatePrecipitation(PrecipitationCondition precipitationCondition, in ElapsedTime elapsedTime)
        {
            if (AWPrecipitationActualPPSPM2 < AWPrecipitationRequiredPPSPM2)
            {
                AWPrecipitationActualPPSPM2 = (float)Math.Min(AWPrecipitationRequiredPPSPM2, AWPrecipitationActualPPSPM2 + AWPrecipitationRateOfChangePPSPM2PS * elapsedTime.RealSeconds);
            }
            else if (AWPrecipitationActualPPSPM2 > AWPrecipitationRequiredPPSPM2)
            {
                AWPrecipitationActualPPSPM2 = (float)Math.Max(AWPrecipitationRequiredPPSPM2, AWPrecipitationActualPPSPM2 - AWPrecipitationRateOfChangePPSPM2PS * elapsedTime.RealSeconds);
            }
            else
            {
                AWPrecipitationRateOfChangePPSPM2PS = (precipitationCondition.RateOfChange / 120) * (PrecipitationViewer.MaxIntensityPPSPM2 - PrecipitationViewer.MinIntensityPPSPM2);
                int precvariation = (int)(precipitationCondition.Variation * 100);
                float baseDensitiy = PrecipitationViewer.MaxIntensityPPSPM2 * precipitationCondition.Density;
                AWPrecipitationRequiredPPSPM2 = MathHelper.Clamp(((1.0f + (StaticRandom.Next(-precvariation, precvariation) / 100f)) * baseDensitiy),
                                               PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
#if DEBUG_AUTOWEATHER
                Trace.TraceInformation("New density : {0}", AWPrecipitationRequiredPPSPM2);
#endif

                AWLastVisibility = precipitationCondition.VisibilityAtMinDensity; // reach required density, so from now on visibility is determined by density
            }

            // calculate visibility - use last visibility which is either visibility at start of precipitation (at start of spell) or visibility at minimum density (after reaching required density)
            float reqVisibility = precipitationCondition.VisibilityAtMinDensity + ((float)(Math.Sqrt(AWPrecipitationRequiredPPSPM2 / PrecipitationViewer.MaxIntensityPPSPM2)) *
                (precipitationCondition.VisibilityAtMaxDensity - precipitationCondition.VisibilityAtMinDensity));
            AWActualVisibility = AWLastVisibility + (float)(Math.Sqrt(AWPrecipitationActualPPSPM2 / AWPrecipitationRequiredPPSPM2) *
                (reqVisibility - AWLastVisibility));
        }

        private void StopPrecipitationSpell(PrecipitationCondition precipitationCondition, in ElapsedTime elapsedTime)
        {
            AWPrecipitationActualPPSPM2 = (float)Math.Max(PrecipitationViewer.MinIntensityPPSPM2, AWPrecipitationActualPPSPM2 - AWPrecipitationEndRate * elapsedTime.RealSeconds);
            AWActualVisibility = AWLastVisibility +
                (float)(Math.Sqrt(AWPrecipitationActualPPSPM2 / PrecipitationViewer.MaxIntensityPPSPM2) * (precipitationCondition.VisibilityAtMaxDensity - AWLastVisibility));
            AWOvercastCloudcover = CalculateOvercast(precipitationCondition.Overcast.Overcast, 0, precipitationCondition.OvercastDispersion, elapsedTime);
        }

        private float CalculateOvercast(float requiredOvercast, float overcastVariation, float overcastRateOfChange, in ElapsedTime elapsedTime)
        {
            float requiredOvercastFactor = requiredOvercast / 100f;
            if (overcastRateOfChange == 0)
            {
                AWOvercastCloudRateOfChangepS = (StaticRandom.Next(50) / (100f * 300)) * (0.8f + (StaticRandom.Next(100) / 250f));
            }
            else
            {
                AWOvercastCloudRateOfChangepS = (overcastRateOfChange / 300) * (0.8f + (StaticRandom.Next(100) / 250f));
            }

            if (AWOvercastCloudcover < requiredOvercastFactor)
            {
                float newOvercast = (float)Math.Min(requiredOvercastFactor, weather.OvercastFactor + AWOvercastCloudRateOfChangepS * elapsedTime.RealSeconds);
                return (newOvercast);
            }
            else if (weather.OvercastFactor > requiredOvercastFactor)
            {
                float newOvercast = (float)Math.Max(requiredOvercastFactor, weather.OvercastFactor - AWOvercastCloudRateOfChangepS * elapsedTime.RealSeconds);
                return (newOvercast);
            }
            else
            {
                float newOvercast = Math.Max(0, Math.Min(1, requiredOvercastFactor + (StaticRandom.Next((int)(-0.5f * overcastVariation), (int)(0.5f * overcastVariation)) / 100f)));
                return (newOvercast);
            }
        }

        public override void SaveWeatherParameters(BinaryWriter outf)
        {
            // set indication to automatic weather
            outf.Write(1);

            // save input details
            foreach (WeatherCondition condition in weatherDetails)
            {
                condition.Save(outf);
            }
            outf.Write("end");

            outf.Write(AWActiveIndex);
            outf.Write(AWNextChangeTime);

            outf.Write(AWActualVisibility);
            outf.Write(AWLastVisibility);
            outf.Write(AWFogLiftTime);
            outf.Write(AWFogChangeRateMpS);

            outf.Write((int)AWPrecipitationActiveType);
            outf.Write(AWPrecipitationActualPPSPM2);
            outf.Write(AWPrecipitationRequiredPPSPM2);
            outf.Write(AWPrecipitationRateOfChangePPSPM2PS);
            outf.Write(AWPrecipitationTotalDuration);
            outf.Write(AWPrecipitationTotalSpread);
            outf.Write(AWPrecipitationEndSpell);
            outf.Write(AWPrecipitationNextSpell);
            outf.Write(AWPrecipitationStartRate);
            outf.Write(AWPrecipitationEndRate);

            outf.Write(AWOvercastCloudcover);
            outf.Write(AWOvercastCloudRateOfChangepS);

            outf.Write(weather.OvercastFactor);
            outf.Write(weather.FogVisibilityDistance);
            outf.Write(weather.PrecipitationIntensity);
        }

        public override void RestoreWeatherParameters(BinaryReader inf)
        {
            int weathercontroltype = inf.ReadInt32();

            // restoring wrong type of weather - abort
            if (weathercontroltype != 1)
            {
                Trace.TraceError(Simulator.Catalog.GetString("Restoring wrong weather type : trying to restore user controlled weather but save contains dynamic weather"));
            }

            weatherDetails.Clear();

            string readtype = inf.ReadString();

            while (!string.IsNullOrEmpty(readtype))
            {
                if (readtype == "fog")
                {
                    weatherDetails.Add(new FogCondition(inf));
                }
                else if (readtype == "precipitation")
                {
                    weatherDetails.Add(new PrecipitationCondition(inf));
                }
                else if (readtype == "overcast")
                {
                    weatherDetails.Add(new OvercastCondition(inf));
                }
                else if (readtype == "end")
                {
                    break;
                }
                readtype = inf.ReadString();
            }

            AWActiveIndex = inf.ReadInt32();
            AWNextChangeTime = inf.ReadSingle();

            AWActualVisibility = inf.ReadSingle();
            AWLastVisibility = inf.ReadSingle();
            AWFogLiftTime = inf.ReadSingle();
            AWFogChangeRateMpS = inf.ReadSingle();

            AWPrecipitationActiveType = (WeatherType)inf.ReadInt32();
            AWPrecipitationActualPPSPM2 = inf.ReadSingle();
            AWPrecipitationRequiredPPSPM2 = inf.ReadSingle();
            AWPrecipitationRateOfChangePPSPM2PS = inf.ReadSingle();
            AWPrecipitationTotalDuration = inf.ReadSingle();
            AWPrecipitationTotalSpread = inf.ReadInt32();
            AWPrecipitationEndSpell = inf.ReadSingle();
            AWPrecipitationNextSpell = inf.ReadSingle();
            AWPrecipitationStartRate = inf.ReadSingle();
            AWPrecipitationEndRate = inf.ReadSingle();

            AWOvercastCloudcover = inf.ReadSingle();
            AWOvercastCloudRateOfChangepS = inf.ReadSingle();

            weather.OvercastFactor = inf.ReadSingle();
            weather.FogVisibilityDistance = inf.ReadSingle();
            weather.PrecipitationIntensity = inf.ReadSingle();

            Time = (float)viewer.Simulator.ClockTime;
        }

    }
}
