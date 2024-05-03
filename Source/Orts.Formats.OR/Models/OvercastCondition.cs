using System;
using System.IO;

using Orts.Formats.OR.Parsers;

namespace Orts.Formats.OR.Models
{
    public class OvercastCondition : WeatherConditionBase
    {
        public float Overcast { get; private set; }                     // required overcast - range : 0 - 100 (percentage)
        public float Variation { get; private set; }            // variation in overcast - range : 0 - 100 (percentage change)
        public float RateOfChange { get; private set; }         // overcast rate of change - range : 0 - 1 (scaling factor)
        public float Visibility { get; private set; } = 60000; // required visibility - range 1000 - 60000 (for lower values use fog)

        internal OvercastCondition(JsonReader json)
        {
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

        internal OvercastCondition()
        { }

        // restore
        public OvercastCondition(BinaryReader inf)
        {
            Time = inf.ReadSingle();
            Overcast = inf.ReadSingle();
            Variation = inf.ReadSingle();
            RateOfChange = inf.ReadSingle();
            Visibility = inf.ReadSingle();
        }

        // save
        public override void Save(BinaryWriter outf)
        {
            outf.Write("overcast");
            outf.Write(Time);
            outf.Write(Overcast);
            outf.Write(Variation);
            outf.Write(RateOfChange);
            outf.Write(Visibility);
        }

        public override void Check(TimeSpan duration)
        {
            Overcast = CheckValue(Overcast, true, 0, 100, duration, "Overcast");
            Variation = CheckValue(Variation, true, 0, 100, duration, "Overcast Variation");
            RateOfChange = CheckValue(RateOfChange, true, 0, 1, duration, "Overcast Rate of Change");
            Visibility = CheckValue(Visibility, false, 1000, 60000, duration, "Overcast Visibility");
        }
    }

}
