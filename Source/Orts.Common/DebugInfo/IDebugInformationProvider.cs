using System;
using System.Collections.Generic;
using System.Collections.Specialized;

using Microsoft.Xna.Framework;

namespace Orts.Common.DebugInfo
{
    public interface IDebugInformationProvider
    {
        NameValueCollection DebugInfo { get; }

        Dictionary<string, FormatOption> FormattingOptions { get; }
    }

    public readonly struct FormatOption : IEquatable<FormatOption>
    { 
        public Color TextColor { get; }
        public System.Drawing.FontStyle FontStyle { get; }

        public override bool Equals(object obj)
        {
            return obj is FormatOption formatOption && Equals(formatOption);
        }

        public bool Equals(FormatOption other)
        {
            return TextColor == other.TextColor && FontStyle == other.FontStyle;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TextColor, FontStyle);
        }

        public static bool operator ==(FormatOption left, FormatOption right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FormatOption left, FormatOption right)
        {
            return !(left == right);
        }
    }
}
