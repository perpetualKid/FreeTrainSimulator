using System;

using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
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
