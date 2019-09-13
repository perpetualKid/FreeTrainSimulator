using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace Orts.Common.Xna
{
    public static class VectorExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(in Vector3 position, in Matrix matrix, out Vector3 result)
        {
            result.X = (position.X * matrix.M11) + (position.Y * matrix.M21) + (position.Z * matrix.M31) + matrix.M41;
            result.Y = (position.X * matrix.M12) + (position.Y * matrix.M22) + (position.Z * matrix.M32) + matrix.M42;
            result.Z = (position.X * matrix.M13) + (position.Y * matrix.M23) + (position.Z * matrix.M33) + matrix.M43;
        }
        public static float LineSegmentDistanceSquare(this in Vector3 start, in Vector3 end1, in Vector3 end2)
        {
            float dx = end2.X - end1.X;
            float dy = end2.Y - end1.Y;
            float dz = end2.Z - end1.Z;
            float d = dx * dx + dy * dy + dz * dz;
            float n = dx * (start.X - end1.X) + dy * (start.Y - end1.Y) + dz * (start.Z - end1.Z);
            if (d == 0 || n < 0)
            {
                dx = end1.X - start.X;
                dy = end1.Y - start.Y;
                dz = end1.Z - start.Z;
            }
            else if (n > d)
            {
                dx = end2.X - start.X;
                dy = end2.Y - start.Y;
                dz = end2.Z - start.Z;
            }
            else
            {
                dx = end1.X + dx * n / d - start.X;
                dy = end1.Y + dy * n / d - start.Y;
                dz = end1.Z + dz * n / d - start.Z;
            }
            return dx * dx + dy * dy + dz * dz;
        }
    }
}