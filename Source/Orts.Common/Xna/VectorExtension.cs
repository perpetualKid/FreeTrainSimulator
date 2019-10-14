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

        /// <summary>
        /// InterpolateAlongCurve interpolates position along a circular arc.
        /// (Uses MSTS rigid-body rotation method for curve on a grade.)
        /// </summary>
        /// <param name="start">Local position vector for Point-of-Curve (PC) in x-z plane.</param>
        /// <param name="startToTarget">Units vector in direction from PC to arc center (O).</param>
        /// <param name="rotation">Rotation matrix that deflects arc from PC to a point on curve (P).</param>
        /// <param name="yawPitchRoll">>Vector3 containing Yaw, Pitch, and Roll components.</param>
        /// <param name="displacement">Position vector for desired point on curve (P), returned by reference.</param>
        /// <returns>Displacement vector from PC to P in world coordinates.</returns>
        public static void InterpolateAlongCurveLine(in Vector3 start, in Vector3 startToTarget, Matrix rotation, in Vector3 yawPitchRoll, out Vector3 position, out Vector3 displacement)
        {
            Matrix matrix = MatrixExtension.CreateFromYawPitchRoll(yawPitchRoll);
            // Shared method returns displacement from present world position and, by reference,
            // local position in x-z plane of end of this section
            position = Vector3.Transform(-startToTarget, rotation); // Rotate O_PC to O_P
            position = start + startToTarget + position; // Position of P relative to PC
            Vector3.Transform(ref position, ref matrix, out displacement); // Transform to world coordinates and return as displacement.
        }

        /// <summary>
        /// InterpolateAlongStraight interpolates position along a straight stretch.
        /// </summary>
        /// <param name="start">Local position vector for starting point P0 in x-z plane.</param>
        /// <param name="startToEnd">Units vector in direction from starting point P0 to target point P.</param>
        /// <param name="offset">Distance from start to P.</param>
        /// <param name="yawPitchRoll">Vector3 containing Yaw, Pitch, and Roll components.</param>
        /// <param name="vP">Position vector for desired point(P), returned by reference.</param>
        /// <returns>Displacement vector from P0 to P in world coordinates.</returns>
        public static void InterpolateAlongStraightLine(in Vector3 start, Vector3 startToTarget, float offset, in Vector3 yawPitchRoll, out Vector3 position, out Vector3 displacement)
        {
            Quaternion.CreateFromYawPitchRoll(yawPitchRoll.Y, yawPitchRoll.X, yawPitchRoll.Z, out Quaternion quaternion);
            Matrix.CreateFromQuaternion(ref quaternion, out Matrix matrix);
            position = start + offset * startToTarget; // Position of desired point in local coordinates.
            Vector3.Transform(ref position, ref matrix, out displacement);
        }


    }
}