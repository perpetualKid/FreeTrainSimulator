using System.Collections.Generic;
using System.Drawing;

namespace Orts.View
{
    public class FontManager
    {
        private static readonly Dictionary<int, FontManager> fontManagerCache = new Dictionary<int, FontManager>();
        private readonly Dictionary<int, Font> fontCache = new Dictionary<int, Font>();

        public string FontName { get; }

        public FontStyle FontStyle { get; }

        public static FontManager Instance(string fontFamily, FontStyle style)
        {
            int hash = GetHashCode(fontFamily, style);
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
                    result = new Font(FontName, size, FontStyle, GraphicsUnit.Pixel);
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

        private static int GetHashCode(string fontFamily, FontStyle style)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash *= fontFamily?.GetHashCode(System.StringComparison.OrdinalIgnoreCase) ?? 0;
                hash <<= 8;
                hash += (int)style;
                return hash;
            }
        }
    }
}
