using System;
using System.Collections.Generic;
using System.Drawing;

namespace Orts.Graphics
{
    public class FontManager
    {
        private static readonly Dictionary<int, FontManagerInstance> fontManagerCache = new Dictionary<int, FontManagerInstance>();

        public string FontName { get; }

        public FontFamily FontFamily { get; }

        public FontStyle FontStyle { get; }

        public static float ScalingFactor { get; set; } = 1.0f;

        public static FontManagerInstance Exact(string fontName, FontStyle style)
        {
            int hash = HashCode.Combine(fontName, style, false);
            if (!fontManagerCache.TryGetValue(hash, out FontManagerInstance result))
            {
                result = new FontManagerInstance(fontName, style);
                fontManagerCache.Add(hash, result);
            }
            return result;
        }

        public static FontManagerInstance Exact(FontFamily fontFamily, FontStyle style)
        {
            int hash = HashCode.Combine(fontFamily, style, false);
            if (!fontManagerCache.TryGetValue(hash, out FontManagerInstance result))
            {
                result = new FontManagerInstance(fontFamily ?? throw new ArgumentNullException(nameof(fontFamily)), style);
                fontManagerCache.Add(hash, result);
            }
            return result;
        }

        public static FontManagerInstance Scaled(string fontName, FontStyle style)
        {
            int hash = HashCode.Combine(fontName, style, true);
            if (!fontManagerCache.TryGetValue(hash, out FontManagerInstance result))
            {
                result = new FontManagerInstance(fontName, style, ScalingFactor);
                fontManagerCache.Add(hash, result);
            }
            return result;
        }

        public static FontManagerInstance Scaled(FontFamily fontFamily, FontStyle style)
        {
            int hash = HashCode.Combine(fontFamily, style, true);
            if (!fontManagerCache.TryGetValue(hash, out FontManagerInstance result))
            {
                result = new FontManagerInstance(fontFamily ?? throw new ArgumentNullException(nameof(fontFamily)), style, ScalingFactor);
                fontManagerCache.Add(hash, result);
            }
            return result;
        }
    }

    public sealed class FontManagerInstance
    {
        private readonly Dictionary<int, Font> fontCache = new Dictionary<int, Font>();

        public string FontName { get; }

        public FontFamily FontFamily { get; }

        public FontStyle FontStyle { get; }

        public float DpiScale { get; }

        internal FontManagerInstance(string fontName, FontStyle fontStyle, float scale = 1.0f)
        {
            FontName = fontName;
            FontStyle = fontStyle;
            DpiScale = scale;
        }

        internal FontManagerInstance(FontFamily fontFamily, FontStyle fontStyle, float scale = 1.0f)
        {
            FontName = fontFamily.Name;
            FontFamily = fontFamily;
            FontStyle = fontStyle;
            DpiScale = scale;
        }

        public Font this[int size]
        {
            get
            {
                if (!fontCache.TryGetValue(size, out Font result))
                {
                    result = FontFamily != null ? new Font(FontFamily, (int)Math.Round(size * DpiScale), FontStyle, GraphicsUnit.Pixel) : new Font(FontName, (int)Math.Round(size * DpiScale), FontStyle, GraphicsUnit.Pixel);
                    fontCache.Add(size, result);
                }
                return result;
            }
        }

    }
}
