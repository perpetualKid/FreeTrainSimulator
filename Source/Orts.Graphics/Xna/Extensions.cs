using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;

namespace Orts.Graphics.Xna
{
    public static class PointExtension
    {
        public static Point ToPoint(this int[] source)
        { 
            if (source?.Length >1)
                return new Point(source[0], source[1]);
            return Point.Zero;
        }
    }
}
