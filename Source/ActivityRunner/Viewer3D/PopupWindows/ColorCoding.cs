using System;

using Microsoft.Xna.Framework;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal static class ColorCoding
    {

        private const double speedThreshold = 5 / 3.6; // 5km/h to m/s

        internal static Color ArrivalColor(TimeSpan expected, TimeSpan? actual)
        {
            return actual.HasValue && actual.Value <= expected ? Color.LightGreen : Color.LightSalmon;
        }

        internal static Color ArrivalColor(TimeSpan expected, TimeSpan actual)
        {
            return actual <= expected ? Color.LightGreen : Color.LightSalmon;
        }

        internal static Color DepartureColor(TimeSpan expected, TimeSpan? actual)
        {
            return actual.HasValue && actual.Value >= expected ? Color.LightGreen : Color.LightSalmon;
        }

        internal static Color DepartureColor(TimeSpan expected, TimeSpan actual)
        {
            return actual >= expected ? Color.LightGreen : Color.LightSalmon;
        }

        internal static Color SpeedingColor(double speed, double allowedSpeed)
        {
            speed = Math.Abs(speed);
            return speed < (allowedSpeed - speedThreshold) ? Color.LimeGreen :
                speed <= allowedSpeed ? Color.PaleGreen :
                speed < allowedSpeed + speedThreshold ? Color.Orange : Color.Red;
        }

        internal static Color DelayColor(in TimeSpan delay)
        {
            return delay.TotalSeconds switch
            {
                > 120 => Color.OrangeRed,
                > 60 => Color.LightSalmon,
                0 => Color.White,
                < 0 => Color.LightSalmon,
                _ => Color.LightGreen,
            };

        }
    }
}
