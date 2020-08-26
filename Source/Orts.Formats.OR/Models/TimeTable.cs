using System;

namespace Orts.Formats.OR.Models
{
    public class TrainInformation : IComparable<TrainInformation>
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

        public int CompareTo(TrainInformation otherInfo)
        {
            return string.Compare(Train, otherInfo?.Train, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return Train;
        }
    }
}
