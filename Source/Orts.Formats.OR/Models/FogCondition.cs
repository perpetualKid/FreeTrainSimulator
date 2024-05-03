using System;
using System.IO;

using Orts.Formats.OR.Parsers;

namespace Orts.Formats.OR.Models
{

    // fog
    public class FogCondition : WeatherConditionBase
    {
        public float Visibility { get; private set; } = 1000;                            // required fog density - range 0 - 1000
        public float SetTime { get; private set; } = 300;                                // required rate for fog setting - range 300 - 3600
        public float LiftTime { get; private set; } = 300;                               // required rate for fog lifting - range 300 - 3600 - required visibility is taken from next weather
        public float Overcast { get; private set; }                                      // required overcast after fog lifted - range 0 - 100

        public FogCondition(JsonReader json)
        {
            json.ReadBlock(TryParse);
        }

        internal protected override bool TryParse(JsonReader reader)
        {
            if (base.TryParse(reader)) return true;
            switch (reader.Path)
            {
                case "FogVisibility": Visibility = reader.AsFloat(Visibility); break;
                case "FogSetTime": SetTime = reader.AsFloat(SetTime); break;
                case "FogLiftTime": LiftTime = reader.AsFloat(LiftTime); break;
                case "FogOvercast": Overcast = reader.AsFloat(Overcast); break;
                default: return false;
            }

            return true;
        }

        public FogCondition(BinaryReader inf)
        {
            Time = inf.ReadSingle();
            Visibility = inf.ReadSingle();
            SetTime = inf.ReadSingle();
            LiftTime = inf.ReadSingle();
            Overcast = inf.ReadSingle();
        }

        public override void Save(BinaryWriter outf)
        {
            outf.Write("fog");
            outf.Write(Time);
            outf.Write(Visibility);
            outf.Write(SetTime);
            outf.Write(LiftTime);
            outf.Write(Overcast);
        }

        public override void Check(TimeSpan duration)
        {
            Overcast = CheckValue(Overcast, true, 0, 100, duration, "Fog Overcast");
            SetTime = CheckValue(SetTime, false, 300, 3600, duration, "Fog Set Time");
            LiftTime = CheckValue(LiftTime, false, 300, 3600, duration, "Fog Lift Time");
            Visibility = CheckValue(Visibility, false, 10, 20000, duration, "Fog Visibility");
        }
    }
}
