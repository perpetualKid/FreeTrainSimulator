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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.OR.Files;
using Orts.Formats.OR.Models;
using Orts.Models.State;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Environment
{
    public class AutomaticWeatherControl : WeatherControl
    {
        // Variables used for auto weather control
        // settings
        private readonly List<WeatherConditionBase> weatherDetails = new List<WeatherConditionBase>();

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

        public AutomaticWeatherControl(Viewer viewer, string fileName, double realTime)
            : base(viewer)
        {
            // read weather details from file
            WeatherFile weatherFile = new WeatherFile(fileName);
            weatherDetails = weatherFile.Changes;

            if (weatherDetails.Count == 0)
                Trace.TraceWarning("Weather file contains no settings {0}", weatherFile);
            else
                CheckWeatherDetails();

            // set initial weather parameters
            SetInitialWeatherParameters(realTime);
        }

        // dummy constructor for restore
        public AutomaticWeatherControl(Viewer viewer)
            : base(viewer)
        {
        }

        // check weather details, set auto variables
        private void CheckWeatherDetails()
        {
            float prevTime = 0;

            foreach (WeatherConditionBase weatherSet in weatherDetails)
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
            time = (float)realTime;

            // find last valid weather change
            AWActiveIndex = 0;
            var passedTime = false;

            if (weatherDetails.Count == 0)
                return;

            for (var iIndex = 1; iIndex < weatherDetails.Count && !passedTime; iIndex++)
                if (weatherDetails[iIndex].Time > time)
                {
                    passedTime = true;
                    AWActiveIndex = iIndex - 1;
                }

            // get last weather
            WeatherConditionBase lastWeather = weatherDetails[AWActiveIndex];

            AWNextChangeTime = AWActiveIndex < weatherDetails.Count - 1 ? weatherDetails[AWActiveIndex + 1].Time : 24 * 3600;
            int nextIndex = AWActiveIndex < weatherDetails.Count - 1 ? AWActiveIndex + 1 : -1;

            // fog
            if (lastWeather is FogCondition fogCondition)
            {
                float actualLiftingTime = 0.9f * fogCondition.LiftTime + (float)StaticRandom.Next(10) / 100 * fogCondition.LiftTime; // defined time +- 10%
                AWFogLiftTime = AWNextChangeTime - actualLiftingTime;

                // check if fog is allready lifting
                if ((float)realTime > AWFogLiftTime && nextIndex > 1)
                {
                    float reqVisibility = GetWeatherVisibility(weatherDetails[nextIndex]);
                    float remainingFactor = ((float)realTime - AWNextChangeTime + actualLiftingTime) / actualLiftingTime;
                    AWActualVisibility = fogCondition.Visibility + remainingFactor * remainingFactor * (reqVisibility - fogCondition.Visibility);
                    AWOvercastCloudcover = fogCondition.Overcast / 100;
                }
                else
                    StartFog(fogCondition, (float)realTime, AWActiveIndex);
            }

            // precipitation
            else if (lastWeather is PrecipitationCondition precipitationCondition)
                StartPrecipitation(precipitationCondition, (float)realTime, true);

            // cloudcover
            else if (lastWeather is OvercastCondition overcastCondition)
            {
                AWOvercastCloudcover = Math.Max(0, Math.Min(1, overcastCondition.Overcast / 100 +
                    (float)StaticRandom.Next((int)(-0.5f * overcastCondition.Variation), (int)(0.5f * overcastCondition.Variation)) / 100));
                AWActualVisibility = weather.FogVisibilityDistance = overcastCondition.Visibility;
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
                    viewer.SoundProcess.AddSoundSources(this, rainSound);
                    foreach (var soundSource in rainSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                case WeatherType.Snow:
                    weather.PrecipitationIntensity = AWPrecipitationActualPPSPM2;
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, snowSound);
                    foreach (var soundSource in snowSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                default:
                    weather.PrecipitationIntensity = 0;
                    viewer.SoundProcess.AddSoundSources(this, clearSound);
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
                    break;
            }
        }

        public override void Update(in ElapsedTime elapsedTime)
        {
            // not client and weather auto mode
            time += (float)elapsedTime.ClockSeconds;
            var fogActive = false;

            if (weatherDetails.Count == 0)
                return;

            WeatherConditionBase lastWeather = weatherDetails[AWActiveIndex];
            int nextIndex = AWActiveIndex < weatherDetails.Count - 1 ? AWActiveIndex + 1 : -1;
            fogActive = false;

            // check for fog
            if (lastWeather is FogCondition fogCondition)
            {
                CalculateFog(fogCondition, nextIndex);
                fogActive = true;

                // if fog has lifted, change to next sequence
                if (time > AWNextChangeTime - fogCondition.LiftTime && AWActualVisibility >= 19999 && AWActiveIndex < weatherDetails.Count - 1)
                {
                    fogActive = false;
                    AWNextChangeTime = time - 1;  // force change to next weather
                }
            }

            // check for precipitation
            else if (lastWeather is PrecipitationCondition precipitationCondition)
                // precipitation not active
                if (AWPrecipitationActiveType == WeatherType.Clear)
                    // if beyond start of next spell start precipitation
                    if (time > AWPrecipitationNextSpell)
                        // if cloud has build up
                        if (AWOvercastCloudcover >= precipitationCondition.OvercastPrecipitationStart / 100)
                        {
                            StartPrecipitationSpell(precipitationCondition, AWNextChangeTime);
                            CalculatePrecipitation(precipitationCondition, elapsedTime);
                        }
                        // build up cloud
                        else
                            AWOvercastCloudcover = CalculateOvercast(precipitationCondition.OvercastPrecipitationStart, 0, precipitationCondition.OvercastBuildUp, elapsedTime);
                    // set overcast and visibility
                    else
                    {
                        AWOvercastCloudcover = CalculateOvercast(precipitationCondition.Overcast.Overcast, precipitationCondition.Overcast.Variation, precipitationCondition.Overcast.RateOfChange, elapsedTime);
                        if (weather.FogVisibilityDistance > precipitationCondition.Overcast.Visibility)
                            AWActualVisibility = weather.FogVisibilityDistance - 40 * (float)elapsedTime.RealSeconds; // reduce visibility by 40 m/s
                        else if (weather.FogVisibilityDistance < precipitationCondition.Overcast.Visibility)
                            AWActualVisibility = weather.FogVisibilityDistance + 40 * (float)elapsedTime.RealSeconds; // increase visibility by 40 m/s
                    }
                // active precipitation
                // if beyond end of spell : decrease densitity, if density below minimum threshold stop precipitation
                else if (time > AWPrecipitationEndSpell)
                {
                    StopPrecipitationSpell(precipitationCondition, elapsedTime);
                    // if density dropped under min threshold precipitation has ended
                    if (AWPrecipitationActualPPSPM2 <= PrecipitationViewer.MinIntensityPPSPM2)
                        AWPrecipitationActiveType = WeatherType.Clear;
                }
                // active precipitation : set density and related visibility
                else
                    CalculatePrecipitation(precipitationCondition, elapsedTime);
            // clear
            else if (lastWeather is OvercastCondition overcastCondition)
            {
                AWOvercastCloudcover = CalculateOvercast(overcastCondition.Overcast, overcastCondition.Variation, overcastCondition.RateOfChange, elapsedTime);
                if (AWActualVisibility > overcastCondition.Visibility)
                    AWActualVisibility = (float)Math.Max(overcastCondition.Visibility, AWActualVisibility - 40 * elapsedTime.RealSeconds); // reduce visibility by 40 m/s
                else if (AWActualVisibility < overcastCondition.Visibility)
                    AWActualVisibility = (float)Math.Min(overcastCondition.Visibility, AWActualVisibility + 40 * elapsedTime.RealSeconds); // increase visibility by 40 m/s
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
                    viewer.SoundProcess.AddSoundSources(this, rainSound);
                    foreach (var soundSource in rainSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                case WeatherType.Snow:
                    weather.PrecipitationIntensity = AWPrecipitationActualPPSPM2;
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, snowSound);
                    foreach (var soundSource in snowSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                default:
                    weather.PrecipitationIntensity = 0;
                    viewer.SoundProcess.AddSoundSources(this, clearSound);
                    weather.OvercastFactor = AWOvercastCloudcover;
                    weather.FogVisibilityDistance = AWActualVisibility;
                    break;
            }

            // check for change in required weather
            // time to change but no change after midnight and further weather available
            if (time < 24 * 3600 && time > AWNextChangeTime && AWActiveIndex < weatherDetails.Count - 1)
                // if precipitation still active or fog not lifted, postpone change by one minute
                if (AWPrecipitationActiveType != WeatherType.Clear || fogActive)
                    AWNextChangeTime += 60;
                else
                {
                    AWActiveIndex++;
                    AWNextChangeTime = AWActiveIndex < weatherDetails.Count - 2 ? weatherDetails[AWActiveIndex + 1].Time : 24 * 3600;

                    WeatherConditionBase nextWeather = weatherDetails[AWActiveIndex];
                    if (nextWeather is FogCondition nextfogCondition)
                        StartFog(nextfogCondition, time, AWActiveIndex);
                    else if (nextWeather is PrecipitationCondition nextprecipitationCondition)
                        StartPrecipitation(nextprecipitationCondition, time, false);
                }
        }

        private float GetWeatherVisibility(WeatherConditionBase weatherDetail)
        {
            float nextVisibility = weather.FogVisibilityDistance; // present visibility
            if (weatherDetail is FogCondition fogCondition)
                nextVisibility = fogCondition.Visibility;
            else if (weatherDetail is OvercastCondition overcastCondition)
                nextVisibility = overcastCondition.Visibility;
            else if (weatherDetail is PrecipitationCondition precipitationCondition)
                nextVisibility = precipitationCondition.Overcast.Visibility;
            return nextVisibility;
        }

        private void StartFog(FogCondition fogCondition, float startTime, int activeIndex)
        {
            // fog fully set or fog at start of day
            if (startTime > fogCondition.Time + fogCondition.SetTime || activeIndex == 0)
                AWActualVisibility = fogCondition.Visibility;
            // fog still setting
            else
            {
                float remainingFactor = (startTime - fogCondition.Time + fogCondition.SetTime) / fogCondition.SetTime;
                AWActualVisibility = MathHelper.Clamp(AWActualVisibility - remainingFactor * remainingFactor * (AWActualVisibility - fogCondition.Visibility), fogCondition.Visibility, AWActualVisibility);
            }
        }

        private void CalculateFog(FogCondition fogCondition, int nextIndex)
        {
            if (AWFogLiftTime > 0 && time > AWFogLiftTime && nextIndex > 0) // fog is lifting
            {
                float reqVisibility = GetWeatherVisibility(weatherDetails[nextIndex]);
                float remainingFactor = (time - weatherDetails[nextIndex].Time + fogCondition.LiftTime) / fogCondition.LiftTime;
                AWActualVisibility = fogCondition.Visibility + remainingFactor * remainingFactor * (reqVisibility - fogCondition.Visibility);
                AWOvercastCloudcover = fogCondition.Overcast / 100;
            }
            else if (AWActualVisibility > fogCondition.Visibility)
            {
                float remainingFactor = (time - fogCondition.Time + fogCondition.SetTime) / fogCondition.SetTime;
                AWActualVisibility = MathHelper.Clamp(AWLastVisibility - remainingFactor * remainingFactor * (AWLastVisibility - fogCondition.Visibility), fogCondition.Visibility, AWLastVisibility);
            }
        }

        private void StartPrecipitation(PrecipitationCondition precipitationCondition, float startTime, bool allowImmediateStart)
        {
            AWPrecipitationRequiredType = precipitationCondition.PrecipitationType;

            // determine actual duration of precipitation
            float maxDuration = AWNextChangeTime - weatherDetails[AWActiveIndex].Time;
            AWPrecipitationTotalDuration = (float)maxDuration * (precipitationCondition.Probability / 100f);  // nominal value
            AWPrecipitationTotalDuration = (0.9f + (float)StaticRandom.Next(20) / 20) * AWPrecipitationTotalDuration; // randomized value, +- 10% 
            AWPrecipitationTotalDuration = Math.Min(AWPrecipitationTotalDuration, maxDuration); // but never exceeding maximum duration
            AWPrecipitationNextSpell = precipitationCondition.Time; // set start of spell to start of weather change

            // determine spread : no. of periods with precipitation (no. of showers)
            if (precipitationCondition.Spread == 1)
                AWPrecipitationTotalSpread = 1;
            else
            {
                AWPrecipitationTotalSpread = Math.Max(1, (int)((0.9f + (float)StaticRandom.Next(20) / 20) * precipitationCondition.Spread));
                if (AWPrecipitationTotalDuration / AWPrecipitationTotalSpread < 900) // length of spell at least 15 mins
                    AWPrecipitationTotalSpread = (int)(AWPrecipitationTotalDuration / 900);
            }


            // determine actual precipitation state - only if immediate start allowed
            bool precipitationActive = allowImmediateStart && StaticRandom.Next(100) >= precipitationCondition.Probability;

            // determine total remaining time as well as remaining periods, based on start/end time and present time
            // this is independent from actual precipitation state

            if (AWPrecipitationTotalSpread > 1)
            {
                AWPrecipitationTotalDuration = (float)((AWNextChangeTime - startTime) / (AWNextChangeTime - weatherDetails[AWActiveIndex].Time)) * AWPrecipitationTotalDuration;
                AWPrecipitationTotalSpread = (int)((float)((AWNextChangeTime - startTime) / (AWNextChangeTime - weatherDetails[AWActiveIndex].Time)) * AWPrecipitationTotalSpread);
            }

            // set actual details
            if (precipitationActive)
            {
                // precipitation active : set actual details, calculate end of present spell
                int precvariation = (int)(precipitationCondition.Variation * 100);
                float baseDensitiy = PrecipitationViewer.MaxIntensityPPSPM2 * precipitationCondition.Density;
                AWPrecipitationActualPPSPM2 = MathHelper.Clamp((1.0f + StaticRandom.Next(-precvariation, precvariation) / 100f) * baseDensitiy,
                                               PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
                AWPrecipitationRequiredPPSPM2 = MathHelper.Clamp((1.0f + StaticRandom.Next(-precvariation, precvariation) / 100f) * baseDensitiy,
                                               PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);

                // rate of change is max. difference over random timespan between 1 and 10 mins.
                // startphase
                float startrate = 1.75f * precipitationCondition.RateOfChange +
                                           0.5F * StaticRandom.Next((int)(precipitationCondition.RateOfChange * 100)) / 100f;
                float spellStartPhase = Math.Min(60f + 300f * startrate, 600);
                AWPrecipitationStartRate = (AWPrecipitationRequiredPPSPM2 - AWPrecipitationActualPPSPM2) / spellStartPhase;

                // endphase
                float endrate = 1.75f * precipitationCondition.RateOfChange +
                               0.5f * StaticRandom.Next((int)(precipitationCondition.RateOfChange * 100)) / 100f;
                float spellEndPhase = Math.Min(60f + 300f * endrate, 600);

                float avduration = AWPrecipitationTotalDuration / AWPrecipitationTotalSpread;
                float actduration = (0.5f + StaticRandom.Next(100) / 100f) * avduration;
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
                    AWPrecipitationNextSpell = spellEndTime + (0.9f + StaticRandom.Next(200) / 1000f) * avclearspell;
                }
                else
                    AWPrecipitationNextSpell = AWNextChangeTime + 1; // set beyond next weather such that it never occurs

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
                    AWPrecipitationNextSpell = -1;
                else
                {
                    int clearSpell = (int)((AWNextChangeTime - startTime - AWPrecipitationTotalDuration) / AWPrecipitationTotalSpread);
                    AWPrecipitationNextSpell = clearSpell > 0 ? startTime + StaticRandom.Next(clearSpell) : startTime;

                    if (allowImmediateStart)
                    {
                        AWOvercastCloudcover = precipitationCondition.Overcast.Overcast / 100;
                        AWActualVisibility = precipitationCondition.Overcast.Visibility;
                    }
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
            AWPrecipitationRequiredPPSPM2 = MathHelper.Clamp((1.0f + StaticRandom.Next(-precvariation, precvariation) / 100f) * baseDensitiy,
                                           PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
            AWLastVisibility = weather.FogVisibilityDistance;

            // rate of change at start is max. difference over defined time span +- 10%, scaled between 1/2 and 4 mins
            float startphase = MathHelper.Clamp(precipitationCondition.PrecipitationStartPhase * (0.9f + StaticRandom.Next(100) / 1000f), 30, 240);
            AWPrecipitationStartRate = (AWPrecipitationRequiredPPSPM2 - AWPrecipitationActualPPSPM2) / startphase;
            AWPrecipitationRateOfChangePPSPM2PS = AWPrecipitationStartRate;

            // rate of change at end is max. difference over defined time span +- 10%, scaled between 1/2 and 6 mins
            float endphase = MathHelper.Clamp(precipitationCondition.PrecipitationEndPhase * (0.9f + StaticRandom.Next(100) / 1000f), 30, 360);
            AWPrecipitationEndRate = (AWPrecipitationRequiredPPSPM2 - AWPrecipitationActualPPSPM2) / endphase;

            // calculate end of spell and start of next spell
            if (AWPrecipitationTotalSpread > 1)
            {
                float avduration = AWPrecipitationTotalDuration / AWPrecipitationTotalSpread;
                float actduration = (0.5f + StaticRandom.Next(100) / 100f) * avduration;
                float spellEndTime = Math.Min(time + actduration, AWNextChangeTime);
                AWPrecipitationEndSpell = Math.Max(time, spellEndTime - endphase);

                AWPrecipitationTotalDuration -= actduration;
                AWPrecipitationTotalSpread -= 1;

                int clearSpell = (int)((nextWeatherTime - spellEndTime - AWPrecipitationTotalDuration) / AWPrecipitationTotalSpread);
                AWPrecipitationNextSpell = spellEndTime + 60f; // always a minute between spells
                AWPrecipitationNextSpell = clearSpell > 0 ? AWPrecipitationNextSpell + StaticRandom.Next(clearSpell) : AWPrecipitationNextSpell;
            }
            else
                AWPrecipitationEndSpell = Math.Max(time, nextWeatherTime - endphase);
        }

        private void CalculatePrecipitation(PrecipitationCondition precipitationCondition, in ElapsedTime elapsedTime)
        {
            if (AWPrecipitationActualPPSPM2 < AWPrecipitationRequiredPPSPM2)
                AWPrecipitationActualPPSPM2 = (float)Math.Min(AWPrecipitationRequiredPPSPM2, AWPrecipitationActualPPSPM2 + AWPrecipitationRateOfChangePPSPM2PS * elapsedTime.RealSeconds);
            else if (AWPrecipitationActualPPSPM2 > AWPrecipitationRequiredPPSPM2)
                AWPrecipitationActualPPSPM2 = (float)Math.Max(AWPrecipitationRequiredPPSPM2, AWPrecipitationActualPPSPM2 - AWPrecipitationRateOfChangePPSPM2PS * elapsedTime.RealSeconds);
            else
            {
                AWPrecipitationRateOfChangePPSPM2PS = precipitationCondition.RateOfChange / 120 * (PrecipitationViewer.MaxIntensityPPSPM2 - PrecipitationViewer.MinIntensityPPSPM2);
                int precvariation = (int)(precipitationCondition.Variation * 100);
                float baseDensitiy = PrecipitationViewer.MaxIntensityPPSPM2 * precipitationCondition.Density;
                AWPrecipitationRequiredPPSPM2 = MathHelper.Clamp((1.0f + StaticRandom.Next(-precvariation, precvariation) / 100f) * baseDensitiy,
                                               PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
                AWLastVisibility = precipitationCondition.VisibilityAtMinDensity; // reach required density, so from now on visibility is determined by density
            }

            // calculate visibility - use last visibility which is either visibility at start of precipitation (at start of spell) or visibility at minimum density (after reaching required density)
            float reqVisibility = precipitationCondition.VisibilityAtMinDensity + (float)Math.Sqrt(AWPrecipitationRequiredPPSPM2 / PrecipitationViewer.MaxIntensityPPSPM2) *
                (precipitationCondition.VisibilityAtMaxDensity - precipitationCondition.VisibilityAtMinDensity);
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
                AWOvercastCloudRateOfChangepS = StaticRandom.Next(50) / (100f * 300) * (0.8f + StaticRandom.Next(100) / 250f);
            else
                AWOvercastCloudRateOfChangepS = overcastRateOfChange / 300 * (0.8f + StaticRandom.Next(100) / 250f);

            if (AWOvercastCloudcover < requiredOvercastFactor)
            {
                float newOvercast = (float)Math.Min(requiredOvercastFactor, weather.OvercastFactor + AWOvercastCloudRateOfChangepS * elapsedTime.RealSeconds);
                return newOvercast;
            }
            else if (weather.OvercastFactor > requiredOvercastFactor)
            {
                float newOvercast = (float)Math.Max(requiredOvercastFactor, weather.OvercastFactor - AWOvercastCloudRateOfChangepS * elapsedTime.RealSeconds);
                return newOvercast;
            }
            else
            {
                float newOvercast = Math.Max(0, Math.Min(1, requiredOvercastFactor + StaticRandom.Next((int)(-0.5f * overcastVariation), (int)(0.5f * overcastVariation)) / 100f));
                return newOvercast;
            }
        }

        public override void SaveWeatherParameters(BinaryWriter outf)
        {
            // set indication to automatic weather
            outf.Write(1);

            // save input details
            foreach (WeatherConditionBase condition in weatherDetails)
                condition.Save(outf);
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
                Trace.TraceError(Simulator.Catalog.GetString("Restoring wrong weather type : trying to restore user controlled weather but save contains dynamic weather"));

            weatherDetails.Clear();

            string readtype = inf.ReadString();

            while (!string.IsNullOrEmpty(readtype))
            {
                if (readtype == "fog")
                    weatherDetails.Add(new FogCondition(inf));
                else if (readtype == "precipitation")
                    weatherDetails.Add(new PrecipitationCondition(inf));
                else if (readtype == "overcast")
                    weatherDetails.Add(new OvercastCondition(inf));
                else if (readtype == "end")
                    break;
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

            time = (float)viewer.Simulator.ClockTime;
        }

        public override async ValueTask<WeatherSaveState> Snapshot()
        {
            WeatherSaveState result = await base.Snapshot();

            result.AutomaticWeather = new AutomaticWeatherSaveState()
            {

            };
            return result;
        }
    }
}
