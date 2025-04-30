using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.Xna
{
    public static class ColorExtension
    {
        private static readonly Dictionary<string, Color> colorCodes =
            typeof(Color).GetProperties()
            .Where(prop => prop.PropertyType == typeof(Color)).ToDictionary(c => c.Name, c => (Color)c.GetValue(null, null), StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, Color> ColorCodes => colorCodes;

        public static Color HighlightColor(in this Color color, double range)
        {
            if (range == 1)
                return color;
            System.Drawing.Color systemColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

            double h = systemColor.GetHue() / 360.0;
            double s = systemColor.GetSaturation();
            double l = systemColor.GetBrightness();
            l *= range;
            return FromHSLA(h, s, l, color.A / 255.0);
        }

        public static Color ContrastColor(in this Color color)
        {
            //https://stackoverflow.com/questions/1855884/determine-font-color-based-on-background-color
            // Counting the perceptive luminance - human eye favors green color...      
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;

            // Return black for bright colors, white for dark colors
            return luminance > 0.5 ? Color.Black : Color.White;
        }

        // return a color which is complement, i.e. to use as Foreground/Background combination
        public static Color ComplementColor(in this Color color)
        {
            return new Color(255 - color.R, 255 - color.G, 255 - color.B, 255);
        }

        public static System.Drawing.Color ToSystemDrawingColor(in this Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static Color FromName(string name)
        {
            return string.IsNullOrEmpty(name) || !colorCodes.TryGetValue(name, out Color color) ? Color.Transparent : color;
        }

        /// <summary>
        /// returns a System.Drawing.Color which is complement, i.e. to use as Foreground/Background combination
        /// </summary>
        public static System.Drawing.Color ToComplementSystemDrawingColor(in this Color color)
        {
            return System.Drawing.Color.FromArgb(255, color.A - color.R, color.A - color.G, color.A - color.B);
        }

        // Given H,S,L,A in range of 0-1
        // Returns a Color (RGB struct) in range of 0-255
        public static Color FromHSLA(double hue, double saturation, double lightness, double A)
        {
            double v;
            double r, g, b;

            if (A > 1.0)
                A = 1.0;

            r = lightness;   // default to gray
            g = lightness;
            b = lightness;
            v = lightness <= 0.5 ? lightness * (1.0 + saturation) : lightness + saturation - (lightness * saturation);

            if (v > 0)
            {
                double m;
                double sv;
                int sextant;
                double fract, vsf, mid1, mid2;

                m = lightness + lightness - v;
                sv = (v - m) / v;
                hue *= 6.0;
                sextant = (int)hue;
                fract = hue - sextant;
                vsf = v * sv * fract;
                mid1 = m + vsf;
                mid2 = v - vsf;

                switch (sextant)
                {
                    case 0:
                        r = v;
                        g = mid1;
                        b = m;
                        break;

                    case 1:
                        r = mid2;
                        g = v;
                        b = m;
                        break;

                    case 2:
                        r = m;
                        g = v;
                        b = mid1;
                        break;

                    case 3:
                        r = m;
                        g = mid2;
                        b = v;
                        break;

                    case 4:
                        r = mid1;
                        g = m;
                        b = v;
                        break;

                    case 5:
                        r = v;
                        g = m;
                        b = mid2;
                        break;
                }
            }

            return new Color(Convert.ToByte(r * 255.0f), Convert.ToByte(g * 255.0f), Convert.ToByte(b * 255.0f), Convert.ToByte(A * 255.0f));
        }

    }
}
