
using Microsoft.Xna.Framework;

namespace Orts.Graphics.Xna
{
    public static class PointExtension
    {
        public static readonly Point EmptyPoint = new Point(-1, -1);

        public static Point ToPoint(this int[] source)
        {
            if (source?.Length > 1)
                return new Point(source[0], source[1]);
            return Point.Zero;
        }

        public static Point ToPoint(this in System.Drawing.Size size)
        {
            return new Point(size.Width, size.Height);
        }

        public static int[] ToArray(this in Point source)
        {
            return new int[] { source.X, source.Y };
        }
    }
}
