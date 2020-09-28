using System;

using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class StartTime
    {
        public int Hour => time.Days * 24 + time.Hours;
        public int Minute => time.Minutes;
        public int Second => time.Seconds;

        private TimeSpan time;

        public StartTime(int hours, int mminutes, int seconds)
        {
            time = new TimeSpan(hours, mminutes, seconds);
        }

        public StartTime(TimeSpan startTime)
        {
            time = startTime;
        }

        public StartTime(STFReader stf)
        {
            stf.MustMatchBlockStart();
            time = new TimeSpan(stf.ReadInt(null), stf.ReadInt(null), stf.ReadInt(null));
            stf.MustMatchBlockEnd();
        }

        public override string ToString()
        {
            return time.ToString();
        }
    }

    public class Duration
    {
        private TimeSpan time;

        public Duration(int hours, int minutes, int seconds=0)
        {
            time = new TimeSpan(hours, minutes, seconds);
        }

        public Duration(STFReader stf)
        {
            stf.MustMatchBlockStart();
            time = new TimeSpan(stf.ReadInt(null), stf.ReadInt(null), 0);
            stf.MustMatchBlockEnd();
        }

        public int ActivityDuration()
        {
            return (int)time.TotalSeconds;
        }

        public string FormattedDurationTime()
        {
            return time.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        public string FormattedDurationTimeHMS()
        {
            return time.ToString();
        }

    }

}
