using System.Collections.Generic;
using System.Drawing;

namespace Orts.Graphics
{
    public class FontManager
    {
        private static readonly Dictionary<int, FontManager> fontManagerCache = new Dictionary<int, FontManager>();
        private readonly Dictionary<int, Font> fontCache = new Dictionary<int, Font>();

        public string FontName { get; }

        public FontFamily FontFamily { get; }

        public FontStyle FontStyle { get; }

        public static FontManager Instance(string fontName, FontStyle style)
        {
#pragma warning disable RS1024 // Compare symbols correctly
            int hash = GetHashCode(fontName, style);
#pragma warning restore RS1024 // Compare symbols correctly
            if (!fontManagerCache.TryGetValue(hash, out FontManager result))
            {
                result = new FontManager(fontName, style);
                fontManagerCache.Add(hash, result);
            }
            return result;
        }

        public static FontManager Instance(FontFamily fontFamily, FontStyle style)
        {
#pragma warning disable RS1024 // Compare symbols correctly
            int hash = GetHashCode(fontFamily, style);
#pragma warning restore RS1024 // Compare symbols correctly
            if (!fontManagerCache.TryGetValue(hash, out FontManager result))
            {
                result = new FontManager(fontFamily, style);
                fontManagerCache.Add(hash, result);
            }
            return result;
        }

        public Font this[int size]
        {
            get 
            {
                if (!fontCache.TryGetValue(size, out Font result))
                {
                    result = FontFamily != null ? new Font(FontFamily, size, FontStyle, GraphicsUnit.Pixel): new Font(FontName, size, FontStyle, GraphicsUnit.Pixel);
                    fontCache.Add(size, result);
                }
                return result;
            }
        }

        private FontManager(string fontName, FontStyle fontStyle)
        {
            FontName = fontName;
            FontStyle = fontStyle;
        }

        private FontManager(FontFamily fontFamily, FontStyle fontStyle)
        {
            FontName = fontFamily.Name;
            FontFamily = fontFamily;
            FontStyle = fontStyle;
        }


        private static int GetHashCode(string fontName, FontStyle style)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash *= fontName?.GetHashCode(System.StringComparison.OrdinalIgnoreCase) ?? 0;
                hash <<= 8;
                hash += (int)style;
                return hash;
            }
        }

        private static int GetHashCode(FontFamily fontFamily, FontStyle style)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 19;
                hash *= fontFamily?.GetHashCode() ?? 0;
                hash <<= 8;
                hash += (int)style;
                return hash;
            }
        }

    }
}
