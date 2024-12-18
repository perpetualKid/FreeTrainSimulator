using System;

using FreeTrainSimulator.Common.Calc;

namespace Orts.Formats.OpenRails.Models
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct DelayedStart
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public readonly int FixedPart;                                        // fixed part for restart delay
        public readonly int RandomPart;                                       // random part for restart delay

        public DelayedStart(int fixedPart, int randomPart)
        {
            FixedPart = fixedPart;
            RandomPart = randomPart;
        }

        public float RemainingDelay()
        {
            float randDelay = StaticRandom.Next(RandomPart * 10);
            return FixedPart + randDelay / 10f;
        }
    }

#pragma warning disable CA1036 // Override methods on comparable types
    public class TrainInformation : IComparable<TrainInformation>
#pragma warning restore CA1036 // Override methods on comparable types
    {
        public int Column { get; private set; }     // column index
        public string Train { get; private set; }              // train definition
        public string Consist { get; internal set; }            // consist definition (full string)
        public string LeadingConsist { get; internal set; }     // consist definition (extracted leading consist)
        public bool ReverseConsist { get; internal set; }       // use consist in reverse
        public string Path { get; internal set; }               // path definition
        public string StartTime { get; internal set; }          // starttime definition

        public string Briefing { get; internal set; }

        public TrainInformation(int column, string train)
        {
            Column = column;
            Train = train;
            Consist = string.Empty;
            LeadingConsist = string.Empty;
            Path = string.Empty;
        }

        public int CompareTo(TrainInformation other)
        {
            return string.Compare(Train, other?.Train, StringComparison.OrdinalIgnoreCase);
        }

        public string StartTimeCleaned
        {
            get
            {
                int split = StartTime.IndexOf('$', StringComparison.OrdinalIgnoreCase);
                return split > -1 ? StartTime[..StartTime.IndexOf('$', StringComparison.OrdinalIgnoreCase)] : StartTime;
            }
        }

        public override string ToString()
        {
            return Train;
        }
    }
}
