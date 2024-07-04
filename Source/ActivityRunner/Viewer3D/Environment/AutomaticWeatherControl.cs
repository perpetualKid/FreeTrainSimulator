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
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.OR.Files;
using Orts.Formats.OR.Models;
using Orts.Models.State;

namespace Orts.ActivityRunner.Viewer3D.Environment
{

    public class AutomaticWeatherControl : WeatherControl
    {
        // Variables used for auto weather control
        // settings
        private readonly List<WeatherConditionBase> weatherDetails = new List<WeatherConditionBase>();

        // running values
        // general
        private int activeIndex;                                        // active active index in waether list
        private double nextChangeTime;                                   // time for next change
        private float lastVisibility;                                   // visibility at end of previous weather
        private WeatherType precipitationActiveType;  // actual active precipitation

        // cloud
        private float overcastCloudCover;                               // actual cloudcover
        private float overcastCloudRateOfChange;                      // rate of change of cloudcover

        // precipitation
        private WeatherType precipitationRequiredType;// actual active precipitation
        private float precipitationTotalDuration;                       // actual total duration (seconds)
        private int precipitationTotalSpread;                           // actual number of periods with precipitation
        private float precipitationActualPPSPM2;                        // actual rate of precipitation (particals per second per square meter)
        private float precipitationRequiredPPSPM2;                      // required rate of precipitation
        private float precipitationRateOfChangePPSPM2PS;                // rate of change for rate of precipitation (particals per second per square meter per second)
        private float precipitationEndSpell;                            // end of present spell of precipitation (time in seconds)
        private float precipitationNextSpell;                           // start of next spell (time in seconds) (-1 if no further spells)
        private float precipitationStartRate;                           // rate of change at start of spell
        private float precipitationEndRate;                             // rate of change at end of spell

        // fog
        private float actualVisibility;                                    // actual fog visibility
        private float fogChangeRateMpS;                                 // required rate of change for fog
        private double fogLiftTime;                                      // start time of fog lifting to be clear at required time

        //// wind
        //private float previousWindSpeed;                                // windspeed at end of previous weather
        //private float requiredWindSpeed;                                // required wind speed at end of weather
        //private float averageWindSpeed;                                 // required average windspeed
        //private float averageWindGust;                                  // required average additional wind gust
        //private float windGustTime;                                     // time of next wind gust
        //private float actualWindSpeed;                                  // actual wind speed
        //private float windSpeedChange;                                  // required change of wind speed
        //private float requiredWindDirection;                            // required wind direction at end of weather
        //private float averageWindDirection;                             // required average wind direction
        //private float actualWindDirection;                              // actual wind direction
        //private float windDirectionChange;                              // required wind direction change

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
            double prevTime = 0;

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
            activeIndex = 0;
            var passedTime = false;

            if (weatherDetails.Count == 0)
                return;

            for (var iIndex = 1; iIndex < weatherDetails.Count && !passedTime; iIndex++)
                if (weatherDetails[iIndex].Time > time)
                {
                    passedTime = true;
                    activeIndex = iIndex - 1;
                }

            // get last weather
            WeatherConditionBase lastWeather = weatherDetails[activeIndex];

            nextChangeTime = (activeIndex < weatherDetails.Count - 1 ? weatherDetails[activeIndex + 1].Time : 24 * 3600);
            int nextIndex = activeIndex < weatherDetails.Count - 1 ? activeIndex + 1 : -1;

            // fog
            if (lastWeather is FogCondition fogCondition)
            {
                double actualLiftingTime = 0.9f * fogCondition.LiftTime + StaticRandom.Next(10) / 100 * fogCondition.LiftTime; // defined time +- 10%
                fogLiftTime = nextChangeTime - actualLiftingTime;

                // check if fog is allready lifting
                if ((float)realTime > fogLiftTime && nextIndex > 1)
                {
                    float reqVisibility = GetWeatherVisibility(weatherDetails[nextIndex]);
                    double remainingFactor = (realTime - nextChangeTime + actualLiftingTime) / actualLiftingTime;
                    actualVisibility = (float)(fogCondition.Visibility + remainingFactor * remainingFactor * (reqVisibility - fogCondition.Visibility));
                    overcastCloudCover = fogCondition.Overcast / 100;
                }
                else
                    StartFog(fogCondition, (float)realTime, activeIndex);
            }

            // precipitation
            else if (lastWeather is PrecipitationCondition precipitationCondition)
                StartPrecipitation(precipitationCondition, realTime, true);

            // cloudcover
            else if (lastWeather is OvercastCondition overcastCondition)
            {
                overcastCloudCover = Math.Max(0, Math.Min(1, overcastCondition.Overcast / 100 +
                    (float)StaticRandom.Next((int)(-0.5f * overcastCondition.Variation), (int)(0.5f * overcastCondition.Variation)) / 100));
                actualVisibility = weather.FogVisibilityDistance = overcastCondition.Visibility;
            }

            // set system weather parameters
            viewer.SoundProcess.RemoveSoundSources(this);
            viewer.Simulator.WeatherType = precipitationActiveType;

            switch (precipitationActiveType)
            {
                case WeatherType.Rain:
                    weather.PrecipitationIntensity = precipitationActualPPSPM2;
                    weather.OvercastFactor = overcastCloudCover;
                    weather.FogVisibilityDistance = actualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, rainSound);
                    foreach (var soundSource in rainSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                case WeatherType.Snow:
                    weather.PrecipitationIntensity = precipitationActualPPSPM2;
                    weather.OvercastFactor = overcastCloudCover;
                    weather.FogVisibilityDistance = actualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, snowSound);
                    foreach (var soundSource in snowSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                default:
                    weather.PrecipitationIntensity = 0;
                    viewer.SoundProcess.AddSoundSources(this, clearSound);
                    weather.OvercastFactor = overcastCloudCover;
                    weather.FogVisibilityDistance = actualVisibility;
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

            WeatherConditionBase lastWeather = weatherDetails[activeIndex];
            int nextIndex = activeIndex < weatherDetails.Count - 1 ? activeIndex + 1 : -1;
            fogActive = false;

            // check for fog
            if (lastWeather is FogCondition fogCondition)
            {
                CalculateFog(fogCondition, nextIndex);
                fogActive = true;

                // if fog has lifted, change to next sequence
                if (time > nextChangeTime - fogCondition.LiftTime && actualVisibility >= 19999 && activeIndex < weatherDetails.Count - 1)
                {
                    fogActive = false;
                    nextChangeTime = time - 1;  // force change to next weather
                }
            }

            // check for precipitation
            else if (lastWeather is PrecipitationCondition precipitationCondition)
                // precipitation not active
                if (precipitationActiveType == WeatherType.Clear)
                    // if beyond start of next spell start precipitation
                    if (time > precipitationNextSpell)
                        // if cloud has build up
                        if (overcastCloudCover >= precipitationCondition.OvercastPrecipitationStart / 100)
                        {
                            StartPrecipitationSpell(precipitationCondition, nextChangeTime);
                            CalculatePrecipitation(precipitationCondition, elapsedTime);
                        }
                        // build up cloud
                        else
                            overcastCloudCover = CalculateOvercast(precipitationCondition.OvercastPrecipitationStart, 0, precipitationCondition.OvercastBuildUp, elapsedTime);
                    // set overcast and visibility
                    else
                    {
                        overcastCloudCover = CalculateOvercast(precipitationCondition.Overcast.Overcast, precipitationCondition.Overcast.Variation, precipitationCondition.Overcast.RateOfChange, elapsedTime);
                        if (weather.FogVisibilityDistance > precipitationCondition.Overcast.Visibility)
                            actualVisibility = weather.FogVisibilityDistance - 40 * (float)elapsedTime.RealSeconds; // reduce visibility by 40 m/s
                        else if (weather.FogVisibilityDistance < precipitationCondition.Overcast.Visibility)
                            actualVisibility = weather.FogVisibilityDistance + 40 * (float)elapsedTime.RealSeconds; // increase visibility by 40 m/s
                    }
                // active precipitation
                // if beyond end of spell : decrease densitity, if density below minimum threshold stop precipitation
                else if (time > precipitationEndSpell)
                {
                    StopPrecipitationSpell(precipitationCondition, elapsedTime);
                    // if density dropped under min threshold precipitation has ended
                    if (precipitationActualPPSPM2 <= PrecipitationViewer.MinIntensityPPSPM2)
                        precipitationActiveType = WeatherType.Clear;
                }
                // active precipitation : set density and related visibility
                else
                    CalculatePrecipitation(precipitationCondition, elapsedTime);
            // clear
            else if (lastWeather is OvercastCondition overcastCondition)
            {
                overcastCloudCover = CalculateOvercast(overcastCondition.Overcast, overcastCondition.Variation, overcastCondition.RateOfChange, elapsedTime);
                if (actualVisibility > overcastCondition.Visibility)
                    actualVisibility = (float)Math.Max(overcastCondition.Visibility, actualVisibility - 40 * elapsedTime.RealSeconds); // reduce visibility by 40 m/s
                else if (actualVisibility < overcastCondition.Visibility)
                    actualVisibility = (float)Math.Min(overcastCondition.Visibility, actualVisibility + 40 * elapsedTime.RealSeconds); // increase visibility by 40 m/s
            }

            // set weather parameters
            viewer.SoundProcess.RemoveSoundSources(this);
            viewer.Simulator.WeatherType = precipitationActiveType;

            switch (precipitationActiveType)
            {
                case WeatherType.Rain:
                    weather.PrecipitationIntensity = precipitationActualPPSPM2;
                    weather.OvercastFactor = overcastCloudCover;
                    weather.FogVisibilityDistance = actualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, rainSound);
                    foreach (var soundSource in rainSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                case WeatherType.Snow:
                    weather.PrecipitationIntensity = precipitationActualPPSPM2;
                    weather.OvercastFactor = overcastCloudCover;
                    weather.FogVisibilityDistance = actualVisibility;
                    viewer.SoundProcess.AddSoundSources(this, snowSound);
                    foreach (var soundSource in snowSound)
                        soundSource.Volume = weather.PrecipitationIntensity / PrecipitationViewer.MaxIntensityPPSPM2;
                    break;

                default:
                    weather.PrecipitationIntensity = 0;
                    viewer.SoundProcess.AddSoundSources(this, clearSound);
                    weather.OvercastFactor = overcastCloudCover;
                    weather.FogVisibilityDistance = actualVisibility;
                    break;
            }

            // check for change in required weather
            // time to change but no change after midnight and further weather available
            if (time < 24 * 3600 && time > nextChangeTime && activeIndex < weatherDetails.Count - 1)
                // if precipitation still active or fog not lifted, postpone change by one minute
                if (precipitationActiveType != WeatherType.Clear || fogActive)
                    nextChangeTime += 60;
                else
                {
                    activeIndex++;
                    nextChangeTime = activeIndex < weatherDetails.Count - 2 ? weatherDetails[activeIndex + 1].Time : 24 * 3600;

                    WeatherConditionBase nextWeather = weatherDetails[activeIndex];
                    if (nextWeather is FogCondition nextfogCondition)
                        StartFog(nextfogCondition, time, activeIndex);
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

        private void StartFog(FogCondition fogCondition, double startTime, int activeIndex)
        {
            // fog fully set or fog at start of day
            if (startTime > fogCondition.Time + fogCondition.SetTime || activeIndex == 0)
                actualVisibility = fogCondition.Visibility;
            // fog still setting
            else
            {
                double remainingFactor = (startTime - fogCondition.Time + fogCondition.SetTime) / fogCondition.SetTime;
                actualVisibility = MathHelper.Clamp((float)(actualVisibility - remainingFactor * remainingFactor * (actualVisibility - fogCondition.Visibility)), fogCondition.Visibility, actualVisibility);
            }
        }

        private void CalculateFog(FogCondition fogCondition, int nextIndex)
        {
            if (fogLiftTime > 0 && time > fogLiftTime && nextIndex > 0) // fog is lifting
            {
                float reqVisibility = GetWeatherVisibility(weatherDetails[nextIndex]);
                double remainingFactor = (time - weatherDetails[nextIndex].Time + fogCondition.LiftTime) / fogCondition.LiftTime;
                actualVisibility = (float)(fogCondition.Visibility + remainingFactor * remainingFactor * (reqVisibility - fogCondition.Visibility));
                overcastCloudCover = fogCondition.Overcast / 100;
            }
            else if (actualVisibility > fogCondition.Visibility)
            {
                double remainingFactor = (time - fogCondition.Time + fogCondition.SetTime) / fogCondition.SetTime;
                actualVisibility = MathHelper.Clamp((float)(lastVisibility - remainingFactor * remainingFactor * (lastVisibility - fogCondition.Visibility)), fogCondition.Visibility, lastVisibility);
            }
        }

        private void StartPrecipitation(PrecipitationCondition precipitationCondition, double startTime, bool allowImmediateStart)
        {
            precipitationRequiredType = precipitationCondition.PrecipitationType;

            // determine actual duration of precipitation
            double maxDuration = nextChangeTime - weatherDetails[activeIndex].Time;
            precipitationTotalDuration = (float)maxDuration * (precipitationCondition.Probability / 100f);  // nominal value
            precipitationTotalDuration = (0.9f + (float)StaticRandom.Next(20) / 20) * precipitationTotalDuration; // randomized value, +- 10% 
            precipitationTotalDuration = Math.Min(precipitationTotalDuration, (float)maxDuration); // but never exceeding maximum duration
            precipitationNextSpell = (float)precipitationCondition.Time; // set start of spell to start of weather change

            // determine spread : no. of periods with precipitation (no. of showers)
            if (precipitationCondition.Spread == 1)
                precipitationTotalSpread = 1;
            else
            {
                precipitationTotalSpread = Math.Max(1, (int)((0.9f + (float)StaticRandom.Next(20) / 20) * precipitationCondition.Spread));
                if (precipitationTotalDuration / precipitationTotalSpread < 900) // length of spell at least 15 mins
                    precipitationTotalSpread = (int)(precipitationTotalDuration / 900);
            }


            // determine actual precipitation state - only if immediate start allowed
            bool precipitationActive = allowImmediateStart && StaticRandom.Next(100) >= precipitationCondition.Probability;

            // determine total remaining time as well as remaining periods, based on start/end time and present time
            // this is independent from actual precipitation state

            if (precipitationTotalSpread > 1)
            {
                precipitationTotalDuration = (float)((nextChangeTime - startTime) / (nextChangeTime - weatherDetails[activeIndex].Time)) * precipitationTotalDuration;
                precipitationTotalSpread = (int)((float)((nextChangeTime - startTime) / (nextChangeTime - weatherDetails[activeIndex].Time)) * precipitationTotalSpread);
            }

            // set actual details
            if (precipitationActive)
            {
                // precipitation active : set actual details, calculate end of present spell
                int precvariation = (int)(precipitationCondition.Variation * 100);
                float baseDensitiy = PrecipitationViewer.MaxIntensityPPSPM2 * precipitationCondition.Density;
                precipitationActualPPSPM2 = MathHelper.Clamp((1.0f + StaticRandom.Next(-precvariation, precvariation) / 100f) * baseDensitiy,
                                               PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
                precipitationRequiredPPSPM2 = MathHelper.Clamp((1.0f + StaticRandom.Next(-precvariation, precvariation) / 100f) * baseDensitiy,
                                               PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);

                // rate of change is max. difference over random timespan between 1 and 10 mins.
                // startphase
                float startrate = 1.75f * precipitationCondition.RateOfChange +
                                           0.5F * StaticRandom.Next((int)(precipitationCondition.RateOfChange * 100)) / 100f;
                float spellStartPhase = Math.Min(60f + 300f * startrate, 600);
                precipitationStartRate = (precipitationRequiredPPSPM2 - precipitationActualPPSPM2) / spellStartPhase;

                // endphase
                float endrate = 1.75f * precipitationCondition.RateOfChange +
                               0.5f * StaticRandom.Next((int)(precipitationCondition.RateOfChange * 100)) / 100f;
                float spellEndPhase = Math.Min(60f + 300f * endrate, 600);

                float avduration = precipitationTotalDuration / precipitationTotalSpread;
                float actduration = (0.5f + StaticRandom.Next(100) / 100f) * avduration;
                double spellEndTime = Math.Min(startTime + actduration, nextChangeTime);
                precipitationEndSpell = (float)Math.Max(startTime, spellEndTime - spellEndPhase);
                // for end rate, use minimum precipitation
                precipitationEndRate = (precipitationActualPPSPM2 - PrecipitationViewer.MinIntensityPPSPM2) / spellEndPhase;
                precipitationTotalDuration -= actduration;
                precipitationTotalSpread -= 1;

                // calculate length of clear period and start of next spell
                if (precipitationTotalDuration > 0 && precipitationTotalSpread > 0)
                {
                    double avclearspell = (nextChangeTime - startTime - precipitationTotalDuration) / precipitationTotalSpread;
                    precipitationNextSpell = (float)(spellEndTime + (0.9f + StaticRandom.Next(200) / 1000f) * avclearspell);
                }
                else
                    precipitationNextSpell = (float)nextChangeTime + 1; // set beyond next weather such that it never occurs

                // set active values
                precipitationActiveType = precipitationCondition.PrecipitationType;
                overcastCloudCover = precipitationCondition.OvercastPrecipitationStart / 100;  // fixed cloudcover during precipitation
                actualVisibility = precipitationCondition.VisibilityAtMinDensity + (float)(Math.Sqrt(precipitationActualPPSPM2 / PrecipitationViewer.MaxIntensityPPSPM2) *
                    (precipitationCondition.VisibilityAtMaxDensity - precipitationCondition.VisibilityAtMinDensity));
                lastVisibility = precipitationCondition.VisibilityAtMinDensity; // fix last visibility to visibility at minimum density
            }
            else
            // if presently not active, set start of next spell
            {
                if (precipitationTotalSpread < 1)
                    precipitationNextSpell = -1;
                else
                {
                    int clearSpell = (int)((nextChangeTime - startTime - precipitationTotalDuration) / precipitationTotalSpread);
                    precipitationNextSpell = (float)(clearSpell > 0 ? startTime + StaticRandom.Next(clearSpell) : startTime);

                    if (allowImmediateStart)
                    {
                        overcastCloudCover = precipitationCondition.Overcast.Overcast / 100;
                        actualVisibility = precipitationCondition.Overcast.Visibility;
                    }
                }

                precipitationActiveType = WeatherType.Clear;
            }
        }

        private void StartPrecipitationSpell(PrecipitationCondition precipitationCondition, double nextWeatherTime)
        {
            int precvariation = (int)(precipitationCondition.Variation * 100);
            float baseDensitiy = PrecipitationViewer.MaxIntensityPPSPM2 * precipitationCondition.Density;
            precipitationActiveType = precipitationRequiredType;
            precipitationActualPPSPM2 = PrecipitationViewer.MinIntensityPPSPM2;
            precipitationRequiredPPSPM2 = MathHelper.Clamp((1.0f + StaticRandom.Next(-precvariation, precvariation) / 100f) * baseDensitiy,
                                           PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
            lastVisibility = weather.FogVisibilityDistance;

            // rate of change at start is max. difference over defined time span +- 10%, scaled between 1/2 and 4 mins
            float startphase = MathHelper.Clamp(precipitationCondition.PrecipitationStartPhase * (0.9f + StaticRandom.Next(100) / 1000f), 30, 240);
            precipitationStartRate = (precipitationRequiredPPSPM2 - precipitationActualPPSPM2) / startphase;
            precipitationRateOfChangePPSPM2PS = precipitationStartRate;

            // rate of change at end is max. difference over defined time span +- 10%, scaled between 1/2 and 6 mins
            float endphase = MathHelper.Clamp(precipitationCondition.PrecipitationEndPhase * (0.9f + StaticRandom.Next(100) / 1000f), 30, 360);
            precipitationEndRate = (precipitationRequiredPPSPM2 - precipitationActualPPSPM2) / endphase;

            // calculate end of spell and start of next spell
            if (precipitationTotalSpread > 1)
            {
                float avduration = precipitationTotalDuration / precipitationTotalSpread;
                float actduration = (0.5f + StaticRandom.Next(100) / 100f) * avduration;
                double spellEndTime = Math.Min(time + actduration, nextChangeTime);
                precipitationEndSpell = (float)Math.Max(time, spellEndTime - endphase);

                precipitationTotalDuration -= actduration;
                precipitationTotalSpread -= 1;

                int clearSpell = (int)((nextWeatherTime - spellEndTime - precipitationTotalDuration) / precipitationTotalSpread);
                precipitationNextSpell = (float)spellEndTime + 60f; // always a minute between spells
                precipitationNextSpell = clearSpell > 0 ? precipitationNextSpell + StaticRandom.Next(clearSpell) : precipitationNextSpell;
            }
            else
                precipitationEndSpell = (float)Math.Max(time, nextWeatherTime - endphase);
        }

        private void CalculatePrecipitation(PrecipitationCondition precipitationCondition, in ElapsedTime elapsedTime)
        {
            if (precipitationActualPPSPM2 < precipitationRequiredPPSPM2)
                precipitationActualPPSPM2 = (float)Math.Min(precipitationRequiredPPSPM2, precipitationActualPPSPM2 + precipitationRateOfChangePPSPM2PS * elapsedTime.RealSeconds);
            else if (precipitationActualPPSPM2 > precipitationRequiredPPSPM2)
                precipitationActualPPSPM2 = (float)Math.Max(precipitationRequiredPPSPM2, precipitationActualPPSPM2 - precipitationRateOfChangePPSPM2PS * elapsedTime.RealSeconds);
            else
            {
                precipitationRateOfChangePPSPM2PS = precipitationCondition.RateOfChange / 120 * (PrecipitationViewer.MaxIntensityPPSPM2 - PrecipitationViewer.MinIntensityPPSPM2);
                int precvariation = (int)(precipitationCondition.Variation * 100);
                float baseDensitiy = PrecipitationViewer.MaxIntensityPPSPM2 * precipitationCondition.Density;
                precipitationRequiredPPSPM2 = MathHelper.Clamp((1.0f + StaticRandom.Next(-precvariation, precvariation) / 100f) * baseDensitiy,
                                               PrecipitationViewer.MinIntensityPPSPM2, PrecipitationViewer.MaxIntensityPPSPM2);
                lastVisibility = precipitationCondition.VisibilityAtMinDensity; // reach required density, so from now on visibility is determined by density
            }

            // calculate visibility - use last visibility which is either visibility at start of precipitation (at start of spell) or visibility at minimum density (after reaching required density)
            float reqVisibility = precipitationCondition.VisibilityAtMinDensity + (float)Math.Sqrt(precipitationRequiredPPSPM2 / PrecipitationViewer.MaxIntensityPPSPM2) *
                (precipitationCondition.VisibilityAtMaxDensity - precipitationCondition.VisibilityAtMinDensity);
            actualVisibility = lastVisibility + (float)(Math.Sqrt(precipitationActualPPSPM2 / precipitationRequiredPPSPM2) *
                (reqVisibility - lastVisibility));
        }

        private void StopPrecipitationSpell(PrecipitationCondition precipitationCondition, in ElapsedTime elapsedTime)
        {
            precipitationActualPPSPM2 = (float)Math.Max(PrecipitationViewer.MinIntensityPPSPM2, precipitationActualPPSPM2 - precipitationEndRate * elapsedTime.RealSeconds);
            actualVisibility = lastVisibility +
                (float)(Math.Sqrt(precipitationActualPPSPM2 / PrecipitationViewer.MaxIntensityPPSPM2) * (precipitationCondition.VisibilityAtMaxDensity - lastVisibility));
            overcastCloudCover = CalculateOvercast(precipitationCondition.Overcast.Overcast, 0, precipitationCondition.OvercastDispersion, elapsedTime);
        }

        private float CalculateOvercast(float requiredOvercast, float overcastVariation, float overcastRateOfChange, in ElapsedTime elapsedTime)
        {
            float requiredOvercastFactor = requiredOvercast / 100f;
            if (overcastRateOfChange == 0)
                overcastCloudRateOfChange = StaticRandom.Next(50) / (100f * 300) * (0.8f + StaticRandom.Next(100) / 250f);
            else
                overcastCloudRateOfChange = overcastRateOfChange / 300 * (0.8f + StaticRandom.Next(100) / 250f);

            if (overcastCloudCover < requiredOvercastFactor)
            {
                float newOvercast = (float)Math.Min(requiredOvercastFactor, weather.OvercastFactor + overcastCloudRateOfChange * elapsedTime.RealSeconds);
                return newOvercast;
            }
            else if (weather.OvercastFactor > requiredOvercastFactor)
            {
                float newOvercast = (float)Math.Max(requiredOvercastFactor, weather.OvercastFactor - overcastCloudRateOfChange * elapsedTime.RealSeconds);
                return newOvercast;
            }
            else
            {
                float newOvercast = Math.Max(0, Math.Min(1, requiredOvercastFactor + StaticRandom.Next((int)(-0.5f * overcastVariation), (int)(0.5f * overcastVariation)) / 100f));
                return newOvercast;
            }
        }

        public override async ValueTask<WeatherSaveState> Snapshot()
        {
            WeatherSaveState result = await base.Snapshot().ConfigureAwait(false);

            result.AutomaticWeather = new AutomaticWeatherSaveState()
            {
                FogConditions = await weatherDetails.OfType<FogCondition>().Snapshot().ConfigureAwait(false),
                OvercastConditions = await weatherDetails.OfType<OvercastCondition>().Snapshot().ConfigureAwait(false),
                PrecipitationConditions = await weatherDetails.OfType<PrecipitationCondition>().Snapshot().ConfigureAwait(false),
                ActiveIndex = activeIndex,
                NextChangeTime = nextChangeTime,
                ActualVisibility = actualVisibility,
                LastVisibility = lastVisibility,
                FogChangeRate = fogChangeRateMpS,
                FogLiftTime = fogLiftTime,
                PrecipitationWeatherType = precipitationRequiredType,
                PrecipitationTotalDuration = precipitationTotalDuration,
                PrecipitationTotalSpread = precipitationTotalSpread,
                PrecipitationActualRate = precipitationActualPPSPM2,
                PrecipitationRequiredRate = precipitationRequiredPPSPM2,
                PrecipitationRateOfChange = precipitationRateOfChangePPSPM2PS,
                PrecipitationEndSpell = precipitationEndSpell,
                PrecipitationNextSpell = precipitationNextSpell,
                PrecipitationStartRate = precipitationStartRate,
                PrecipitationEndRate = precipitationEndRate,
                OvercastCloudCover = overcastCloudCover,
                OvercastCloudRateOfChange = overcastCloudRateOfChange,
            };
            return result;
        }

        public override async ValueTask Restore(WeatherSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            ValueTask result = base.Restore(saveState);
            if (saveState.AutomaticWeather.FogConditions != null)
            {
                await weatherDetails.Restore(saveState.AutomaticWeather.FogConditions).ConfigureAwait(false);
            }
            if (saveState.AutomaticWeather.OvercastConditions != null)
            {
                await weatherDetails.Restore(saveState.AutomaticWeather.OvercastConditions).ConfigureAwait(false);
            }
            if (saveState.AutomaticWeather.PrecipitationConditions != null)
            {
                await weatherDetails.Restore(saveState.AutomaticWeather.PrecipitationConditions).ConfigureAwait(false);
            }
            activeIndex = saveState.AutomaticWeather.ActiveIndex;
            nextChangeTime = saveState.AutomaticWeather.NextChangeTime;
            actualVisibility = saveState.AutomaticWeather.ActualVisibility;
            lastVisibility = saveState.AutomaticWeather.LastVisibility;
            fogChangeRateMpS = saveState.AutomaticWeather.FogChangeRate;
            fogLiftTime = saveState.AutomaticWeather.FogLiftTime;
            precipitationRequiredType = saveState.AutomaticWeather.PrecipitationWeatherType;
            precipitationTotalDuration = saveState.AutomaticWeather.PrecipitationTotalDuration;
            precipitationTotalSpread = saveState.AutomaticWeather.PrecipitationTotalSpread;
            precipitationActualPPSPM2 = saveState.AutomaticWeather.PrecipitationActualRate;
            precipitationRequiredPPSPM2 = saveState.AutomaticWeather.PrecipitationRequiredRate;
            precipitationRateOfChangePPSPM2PS = saveState.AutomaticWeather.PrecipitationRateOfChange;
            precipitationEndSpell = saveState.AutomaticWeather.PrecipitationEndSpell;
            precipitationNextSpell = saveState.AutomaticWeather.PrecipitationNextSpell;
            precipitationStartRate = saveState.AutomaticWeather.PrecipitationStartRate;
            precipitationEndRate = saveState.AutomaticWeather.PrecipitationEndRate;
            overcastCloudCover = saveState.AutomaticWeather.OvercastCloudCover;
            overcastCloudRateOfChange = saveState.AutomaticWeather.OvercastCloudRateOfChange;

            time = viewer.Simulator.ClockTime;
            await result.ConfigureAwait(false);
        }
    }
}
