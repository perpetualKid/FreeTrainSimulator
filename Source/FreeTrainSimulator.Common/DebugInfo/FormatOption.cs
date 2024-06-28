using System;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.DebugInfo
{
    public class FormatOption : IEquatable<FormatOption>
    {
        public Color? TextColor { get; }
        public System.Drawing.FontStyle FontStyle { get; } = System.Drawing.FontStyle.Regular;

        public static FormatOption RegularRed { get; } = new FormatOption(Color.Red);
        public static FormatOption RegularGreen { get; } = new FormatOption(Color.Green);
        public static FormatOption RegularBlue { get; } = new FormatOption(Color.Blue);
        public static FormatOption RegularYellow { get; } = new FormatOption(Color.Yellow);
        public static FormatOption RegularOrange { get; } = new FormatOption(Color.Orange);
        public static FormatOption RegularOrangeRed { get; } = new FormatOption(Color.OrangeRed);
        public static FormatOption RegularCyan { get; } = new FormatOption(Color.Cyan);

        public static FormatOption Bold { get; } = new FormatOption(System.Drawing.FontStyle.Bold);
        public static FormatOption BoldRed { get; } = new FormatOption(System.Drawing.FontStyle.Bold, Color.Red);
        public static FormatOption BoldGreen { get; } = new FormatOption(System.Drawing.FontStyle.Bold, Color.Green);
        public static FormatOption BoldBlue { get; } = new FormatOption(System.Drawing.FontStyle.Bold, Color.Blue);
        public static FormatOption BoldYellow { get; } = new FormatOption(System.Drawing.FontStyle.Bold, Color.Yellow);
        public static FormatOption BoldOrange { get; } = new FormatOption(System.Drawing.FontStyle.Bold, Color.Orange);
        public static FormatOption BoldOrangeRed { get; } = new FormatOption(System.Drawing.FontStyle.Bold, Color.OrangeRed);
        public static FormatOption BoldCyan { get; } = new FormatOption(System.Drawing.FontStyle.Bold, Color.Cyan);

        public FormatOption(Color color)
        {
            TextColor = color;
        }

        public FormatOption(System.Drawing.FontStyle fontStyle)
        {
            FontStyle = fontStyle;
        }

        public FormatOption(System.Drawing.FontStyle fontStyle, Color color)
        {
            TextColor = color;
            FontStyle = fontStyle;
        }

        public override bool Equals(object obj)
        {
            return obj is FormatOption formatOption && Equals(formatOption);
        }

        public bool Equals(FormatOption other)
        {
            return other != null && TextColor == other.TextColor && FontStyle == other.FontStyle;
        }

        public static bool operator ==(FormatOption x, FormatOption y)
        {
            return Equals(x, y);
        }

        public static bool operator !=(FormatOption x, FormatOption y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TextColor, FontStyle);
        }
    }
}
