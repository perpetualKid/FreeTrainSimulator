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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Orts.Formats.OR.Models;
using Orts.Models.State;

namespace Orts.ActivityRunner.Viewer3D.Environment
{
    internal static class WeatherConditionExtensions
    {
        public static ValueTask<Collection<WeatherConditionFogSaveState>> Snapshot(this IEnumerable<FogCondition> fogConditions)
        {
            if (fogConditions == null || !fogConditions.Any())
            {
                return ValueTask.FromResult<Collection<WeatherConditionFogSaveState>>(null);
            }
            else
            {
                Collection<WeatherConditionFogSaveState> result = new Collection<WeatherConditionFogSaveState>();
                foreach (FogCondition fogCondition in fogConditions)
                {
                    result.Add(new WeatherConditionFogSaveState()
                    {
                        Time = fogCondition.Time,
                        Visibility = fogCondition.Visibility,
                        SetTime = fogCondition.SetTime,
                        LiftTime = fogCondition.LiftTime,
                        Overcast = fogCondition.Overcast,
                    });
                };
                return ValueTask.FromResult(result);
            }
        }

        public static ValueTask Restore(this IList<WeatherConditionBase> weatherConditions, Collection<WeatherConditionFogSaveState> saveState)
        {
            if (saveState == null || saveState.Count == 0)
            {
                return ValueTask.CompletedTask;
            }
            foreach (WeatherConditionFogSaveState fogSaveState in saveState)
            {
                FogCondition fogCondition = new FogCondition()
                {
                    Time = fogSaveState.Time,
                    Visibility = fogSaveState.Visibility,
                    SetTime = fogSaveState.SetTime,
                    LiftTime = fogSaveState.LiftTime,
                    Overcast = fogSaveState.Overcast,
                };
                weatherConditions.Add(fogCondition);
            }
            return ValueTask.CompletedTask;
        }

        public static ValueTask<Collection<WeatherConditionOvercastSaveState>> Snapshot(this IEnumerable<OvercastCondition> overcastConditions)
        {
            if (overcastConditions == null || !overcastConditions.Any())
            {
                return ValueTask.FromResult<Collection<WeatherConditionOvercastSaveState>>(null);
            }
            else
            {
                Collection<WeatherConditionOvercastSaveState> result = new Collection<WeatherConditionOvercastSaveState>();
                foreach (OvercastCondition overcastCondition in overcastConditions)
                {
                    result.Add(new WeatherConditionOvercastSaveState()
                    {
                        Time = overcastCondition.Time,
                        Overcast = overcastCondition.Overcast,
                        Variation = overcastCondition.Variation,
                        RateOfChange = overcastCondition.RateOfChange,
                        Visibility = overcastCondition.Visibility,
                    });
                };
                return ValueTask.FromResult(result);
            }
        }

        public static ValueTask Restore(this IList<WeatherConditionBase> weatherConditions, Collection<WeatherConditionOvercastSaveState> saveState)
        {
            if (saveState == null || saveState.Count == 0)
            {
                return ValueTask.CompletedTask;
            }
            foreach (WeatherConditionOvercastSaveState overcastSaveState in saveState)
            {
                OvercastCondition overcastCondition = new OvercastCondition()
                {
                    Time = overcastSaveState.Time,
                    Overcast = overcastSaveState.Overcast,
                    Variation = overcastSaveState.Variation,
                    RateOfChange = overcastSaveState.RateOfChange,
                    Visibility = overcastSaveState.Visibility,
                };
                weatherConditions.Add(overcastCondition);
            }
            return ValueTask.CompletedTask;
        }

        public static ValueTask<Collection<WeatherConditionPrecipitationSaveState>> Snapshot(this IEnumerable<PrecipitationCondition> precipitationConditions)
        {
            if (precipitationConditions == null || !precipitationConditions.Any())
            {
                return ValueTask.FromResult<Collection<WeatherConditionPrecipitationSaveState>>(null);
            }
            else
            {
                Collection<WeatherConditionPrecipitationSaveState> result = new Collection<WeatherConditionPrecipitationSaveState>();
                foreach (PrecipitationCondition precipitationCondition in precipitationConditions)
                {
                    result.Add(new WeatherConditionPrecipitationSaveState()
                    {
                        Densitiy = precipitationCondition.Density,
                        OvercastBuildUp = precipitationCondition.OvercastBuildUp,
                        OvercastDispersion = precipitationCondition.OvercastDispersion,
                        OvercastPrecipitationStart = precipitationCondition.OvercastPrecipitationStart,
                        PrecipitationEndPhase = precipitationCondition.PrecipitationEndPhase,
                        PrecipitationStartPhase = precipitationCondition.PrecipitationStartPhase,
                        PrecipitationWeatherType = precipitationCondition.PrecipitationType,
                        Probability = precipitationCondition.Probability,
                        RateOfChange = precipitationCondition.RateOfChange,
                        Spread = precipitationCondition.Spread,
                        Time = precipitationCondition.Time,
                        Variation = precipitationCondition.Variation,
                        VisibilityAtMaxDensity = precipitationCondition.VisibilityAtMaxDensity,
                        VisibilityAtMinDensity = precipitationCondition.VisibilityAtMinDensity,
                        OvercastCondition = new WeatherConditionOvercastSaveState()
                        {
                            Time = precipitationCondition.Overcast.Time,
                            Overcast = precipitationCondition.Overcast.Overcast,
                            Variation = precipitationCondition.Overcast.Variation,
                            RateOfChange = precipitationCondition.Overcast.RateOfChange,
                            Visibility = precipitationCondition.Overcast.Visibility,
                        }
                    });
                };
                return ValueTask.FromResult(result);
            }
        }

        public static ValueTask Restore(this IList<WeatherConditionBase> weatherConditions, Collection<WeatherConditionPrecipitationSaveState> saveState)
        {
            if (saveState == null || saveState.Count == 0)
            {
                return ValueTask.CompletedTask;
            }
            foreach (WeatherConditionPrecipitationSaveState precipitationSaveState in saveState)
            {
                PrecipitationCondition precipitationCondition = new PrecipitationCondition()
                {
                    Density = precipitationSaveState.Densitiy,
                    OvercastBuildUp = precipitationSaveState.OvercastBuildUp,
                    OvercastDispersion = precipitationSaveState.OvercastDispersion,
                    OvercastPrecipitationStart = precipitationSaveState.OvercastPrecipitationStart,
                    PrecipitationEndPhase = precipitationSaveState.PrecipitationEndPhase,
                    PrecipitationStartPhase = precipitationSaveState.PrecipitationStartPhase,
                    PrecipitationType = precipitationSaveState.PrecipitationWeatherType,
                    Probability = precipitationSaveState.Probability,
                    Spread = precipitationSaveState.Spread,
                    Time = precipitationSaveState.Time,
                    Variation = precipitationSaveState.Variation,
                    VisibilityAtMaxDensity = precipitationSaveState.VisibilityAtMaxDensity,
                    VisibilityAtMinDensity = precipitationSaveState.VisibilityAtMinDensity,
                };
                precipitationCondition.Overcast.Time = precipitationSaveState.OvercastCondition.Time;
                precipitationCondition.Overcast.Overcast = precipitationSaveState.OvercastCondition.Overcast;
                precipitationCondition.Overcast.Variation = precipitationSaveState.OvercastCondition.Variation;
                precipitationCondition.Overcast.RateOfChange = precipitationSaveState.OvercastCondition.RateOfChange;
                precipitationCondition.Overcast.Visibility = precipitationSaveState.OvercastCondition.Visibility;

                weatherConditions.Add(precipitationCondition);
            }
            return ValueTask.CompletedTask;
        }
    }
}
