using System;
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
            int hash = HashCode.Combine(fontName, style);
            if (!fontManagerCache.TryGetValue(hash, out FontManager result))
            {
                result = new FontManager(fontName, style);
                fontManagerCache.Add(hash, result);
            }
            return result;
        }

        public static FontManager Instance(FontFamily fontFamily, FontStyle style)
        {
            int hash = HashCode.Combine(fontFamily, style);
            if (!fontManagerCache.TryGetValue(hash, out FontManager result))
            {
                result = new FontManager(fontFamily ?? throw new ArgumentNullException(nameof(fontFamily)), style);
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
    }
}
