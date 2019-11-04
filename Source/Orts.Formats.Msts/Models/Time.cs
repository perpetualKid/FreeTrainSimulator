using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class StartTime
    {
        public int Hour { get; private set; }
        public int Minute { get; private set; }
        public int Second { get; private set; }

        public StartTime(int h, int m, int s)
        {
            Hour = h;
            Minute = m;
            Second = s;
        }

        public StartTime(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Hour = stf.ReadInt(null);
            Minute = stf.ReadInt(null);
            Second = stf.ReadInt(null);
            stf.MustMatchBlockEnd();
        }

        public string FormattedStartTime()
        {
            return Hour.ToString("00") + ":" + Minute.ToString("00") + ":" + Second.ToString("00");
        }
    }

    public class Duration
    {
        private int hour, minute, second;

        public Duration(int hour, int minute, int second=0)
        {
            this.hour = hour;
            this.minute = minute;
            this.second = second;
        }

        public Duration(STFReader stf)
        {
            stf.MustMatchBlockStart();
            hour = stf.ReadInt(null);
            minute = stf.ReadInt(null);
            stf.MustMatchBlockEnd();
        }

        public int ActivityDuration()
        {

            return hour * 3600 + minute * 60 + second; // Convert time to seconds
        }

        public string FormattedDurationTime()
        {
            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        public string FormattedDurationTimeHMS()
        {
            return hour.ToString("00") + ":" + minute.ToString("00") + ":" + second.ToString("00");
        }

    }

}
