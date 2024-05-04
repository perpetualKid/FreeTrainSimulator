using System;

using Orts.Formats.OR.Parsers;

namespace Orts.Formats.OR.Models
{
    public class OvercastCondition : WeatherConditionBase
    {
        public float Overcast { get; set; }                     // required overcast - range : 0 - 100 (percentage)
        public float Variation { get; set; }            // variation in overcast - range : 0 - 100 (percentage change)
        public float RateOfChange { get; set; }         // overcast rate of change - range : 0 - 1 (scaling factor)
        public float Visibility { get; set; } = 60000; // required visibility - range 1000 - 60000 (for lower values use fog)

        public OvercastCondition() { }

        internal OvercastCondition(JsonReader json)
        {
            ArgumentNullException.ThrowIfNull(json, nameof(json));
            json.ReadBlock(TryParse);
        }

        internal protected override bool TryParse(JsonReader reader)
        {
            // get values
            if (base.TryParse(reader))
                return true;
            switch (reader.Path)
            {
                case "Overcast":
                    Overcast = reader.AsFloat(Overcast); break;
                case "OvercastVariation":
                    Variation = reader.AsFloat(Variation); break;
                case "OvercastRateOfChange":
                    RateOfChange = reader.AsFloat(RateOfChange); break;
                case "OvercastVisibility":
                    Visibility = reader.AsFloat(Visibility); break;
                default: return false;
            }

            return true;
        }

        public override void Check(TimeSpan duration)
        {
            Overcast = (float)CheckValue(Overcast, true, 0, 100, duration, "Overcast");
            Variation = (float)CheckValue(Variation, true, 0, 100, duration, "Overcast Variation");
            RateOfChange = (float)CheckValue(RateOfChange, true, 0, 1, duration, "Overcast Rate of Change");
            Visibility = (float)CheckValue(Visibility, false, 1000, 60000, duration, "Overcast Visibility");
        }
    }

}
