using System;

using FreeTrainSimulator.Common;

using Orts.Formats.OR.Parsers;

namespace Orts.Formats.OR.Models
{
    // precipitation

    public class PrecipitationCondition : WeatherConditionBase
    {
        // precipitation spell
        public WeatherType PrecipitationType { get; set; } = WeatherType.Clear;    // required precipitation : rain or snow
        public float Density { get; set; }                             // precipitation density - range 0 - 1
        public float Variation { get; set; }                           // precipitation density variation - range 0 - 1
        public float RateOfChange { get; set; }                        // precipitation rate of change - range 0 - 1
        public float Probability { get; set; }                         // precipitation probability - range : 0 - 100
        public float Spread { get; set; } = 1;                          // precipitation average continuity - range : 1 - ...
        public float VisibilityAtMinDensity { get; set; } = 20000;     // visibility during precipitation at min density
        public float VisibilityAtMaxDensity { get; set; } = 10000;     // visibility during precipitation at max density

        // build up to precipitation
        public float OvercastPrecipitationStart { get; set; }                       // required overcast to start precipitation, also overcast during precipitation - range 0 - 100
        public float OvercastBuildUp { get; set; }                                  // overcast rate of change ahead of precipitation spell - range : 0 - 1
        public float PrecipitationStartPhase { get; set; } = 60;                    // measure for duration of start phase (from dry to full density) - range : 30 to 240 (secs)

        // dispersion after precipitation
        public float OvercastDispersion { get; set; }                               // overcast rate of change after precipitation spell - range : 0 - 1
        public float PrecipitationEndPhase { get; set; } = 60;                      // measure for duration of end phase (from full density to dry) - range : 30 to 360 (secs)

        public OvercastCondition Overcast { get; } = new OvercastCondition();   //required overcast in clear spells

        public PrecipitationCondition() { }

        internal PrecipitationCondition(JsonReader json)
        {
            ArgumentNullException.ThrowIfNull(json, nameof(json));
            json.ReadBlock(TryParse);
        }

        internal protected override bool TryParse(JsonReader reader)
        {
            // read items
            if (base.TryParse(reader))
                return true;
            switch (reader.Path)
            {
                case "PrecipitationType":
                    PrecipitationType = reader.AsEnum(PrecipitationType); break;
                case "PrecipitationDensity":
                    Density = reader.AsFloat(Density); break;
                case "PrecipitationVariation":
                    Variation = reader.AsFloat(Variation); break;
                case "PrecipitationRateOfChange":
                    RateOfChange = reader.AsFloat(RateOfChange); break;
                case "PrecipitationProbability":
                    Probability = reader.AsFloat(Probability); break;
                case "PrecipitationSpread":
                    Spread = reader.AsFloat(Spread); break;
                case "PrecipitationVisibilityAtMinDensity":
                    VisibilityAtMinDensity = reader.AsFloat(VisibilityAtMinDensity); break;
                case "PrecipitationVisibilityAtMaxDensity":
                    VisibilityAtMaxDensity = reader.AsFloat(VisibilityAtMaxDensity); break;
                case "OvercastPrecipitationStart":
                    OvercastPrecipitationStart = reader.AsFloat(OvercastPrecipitationStart); break;
                case "OvercastBuildUp":
                    OvercastBuildUp = reader.AsFloat(OvercastBuildUp); break;
                case "PrecipitationStartPhase":
                    PrecipitationStartPhase = reader.AsFloat(PrecipitationStartPhase); break;
                case "OvercastDispersion":
                    OvercastDispersion = reader.AsFloat(OvercastDispersion); break;
                case "PrecipitationEndPhase":
                    PrecipitationEndPhase = reader.AsFloat(PrecipitationEndPhase); break;

                case "Overcast":
                case "OvercastVariation":
                case "OvercastRateOfChange":
                case "OvercastVisibility":
                    Overcast.TryParse(reader); break;
                default: return false;
            }

            return true;
        }

        public override void Check(TimeSpan duration)
        {
            Overcast.Check(duration);

            // precipitation
            Density = (float)CheckValue(Density, true, 0, 1, duration, "Precipitation Density");
            Variation = (float)CheckValue(Variation, true, 0, 1, duration, "Precipitation Variation");
            RateOfChange = (float)CheckValue(RateOfChange, true, 0, 1, duration, "Precipitation Rate Of Change");
            Probability = (float)CheckValue(Probability, true, 0, 100, duration, "Precipitation Probability");
            Spread = (float)CheckValue(Spread, false, 1, 1000, duration, "Precipitation Spread");
            VisibilityAtMinDensity = (float)CheckValue(VisibilityAtMinDensity, false, 100, Overcast.Visibility, duration, "Precipitation Visibility At Min Density");
            VisibilityAtMaxDensity = (float)CheckValue(VisibilityAtMaxDensity, false, 100, VisibilityAtMinDensity, duration, "Precipitation Visibility At Max Density");

            // build up
            OvercastPrecipitationStart = (float)CheckValue(OvercastPrecipitationStart, true, Overcast.Overcast, 100, duration, "Overcast Precipitation Start");
            OvercastBuildUp = (float)CheckValue(OvercastBuildUp, true, 0, 1, duration, "Overcast Build Up");
            PrecipitationStartPhase = (float)CheckValue(PrecipitationStartPhase, false, 30, 240, duration, "Precipitation Start Phase");

            // dispersion
            OvercastDispersion = (float)CheckValue(OvercastDispersion, true, 0, 1, duration, "Overcast Dispersion");
            PrecipitationEndPhase = (float)CheckValue(PrecipitationEndPhase, false, 30, 360, duration, "Precipitation End Phase");
        }
    }

}
