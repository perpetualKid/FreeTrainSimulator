using System;

using Microsoft.Xna.Framework;

namespace Orts.View.Xna
{
    public static class ColorExtension
    {
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
            v = (lightness <= 0.5) ? (lightness * (1.0 + saturation)) : (lightness + saturation - lightness * saturation);

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
