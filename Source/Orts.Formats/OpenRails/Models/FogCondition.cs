using System;

using Orts.Formats.OpenRails.Parsers;

namespace Orts.Formats.OpenRails.Models
{

    // fog
    public class FogCondition : WeatherConditionBase
    {
        public float Visibility { get; set; } = 1000;                            // required fog density - range 0 - 1000
        public double SetTime { get; set; } = 300;                                // required rate for fog setting - range 300 - 3600
        public double LiftTime { get; set; } = 300;                               // required rate for fog lifting - range 300 - 3600 - required visibility is taken from next weather
        public float Overcast { get; set; }                                      // required overcast after fog lifted - range 0 - 100

        public FogCondition()
        { }

        public FogCondition(JsonReader json)
        {
            ArgumentNullException.ThrowIfNull(json, nameof(json));
            json.ReadBlock(TryParse);
        }

        internal protected override bool TryParse(JsonReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader, nameof(reader));
            if (base.TryParse(reader))
                return true;
            switch (reader.Path)
            {
                case "FogVisibility":
                    Visibility = reader.AsFloat(Visibility);
                    break;
                case "FogSetTime":
                    SetTime = reader.AsDouble(SetTime);
                    break;
                case "FogLiftTime":
                    LiftTime = reader.AsDouble(LiftTime);
                    break;
                case "FogOvercast":
                    Overcast = reader.AsFloat(Overcast);
                    break;
                default:
                    return false;
            }

            return true;
        }

        public override void Check(TimeSpan duration)
        {
            Overcast = (float)CheckValue(Overcast, true, 0, 100, duration, "Fog Overcast");
            SetTime = CheckValue(SetTime, false, 300, 3600, duration, "Fog Set Time");
            LiftTime = CheckValue(LiftTime, false, 300, 3600, duration, "Fog Lift Time");
            Visibility = (float)CheckValue(Visibility, false, 10, 20000, duration, "Fog Visibility");
        }
    }
}
