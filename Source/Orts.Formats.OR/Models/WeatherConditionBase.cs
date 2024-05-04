using System;
using System.Diagnostics;

using Orts.Common.Calc;
using Orts.Common.Xna;
using Orts.Formats.OR.Parsers;

namespace Orts.Formats.OR.Models
{
    public enum WeatherConditionType
    {
        Fog,
        Overcast,
        Precipitation,
    }

    public abstract class WeatherConditionBase
    {
        public double Time { get; set; }               // time of change

        internal protected virtual bool TryParse(JsonReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader, nameof(reader));

            switch (reader.Path)
            {
                case "Time": Time = reader.AsTime(Time); break;
                default: return false;
            }
            return true;
        }

        public void UpdateTime(double value)
        {
            Time = value;
        }

        // check value, set random value if allowed and value not set
        protected static double CheckValue(double value, bool randomize, double minValue, double maxValue, TimeSpan duration, string description)
        {
            // overcast
            if (value < 0 && randomize)
            {
                value = StaticRandom.Next((int)(maxValue * 100)) / 100f;  // ensure there is a value if range is 0 - 1
            }
            else
            {
                double correctedValue = MathHelperD.Clamp(value, minValue, maxValue);
                if (correctedValue != value)
                {
                    Trace.TraceInformation("Invalid value for {0} for weather at {1} : {2}; value must be between {3} and {4}, clamped to {5}",
                        description, duration.ToString(), value, minValue, maxValue, correctedValue);
                    value = correctedValue;
                }
            }
            return value;
        }

        public abstract void Check(TimeSpan duration);
    }

}
