using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Xna
{
    public static class InterpolateHelper
    {
        /// <summary>
        /// InterpolateAlongCurve interpolates position along a circular arc.
        /// (Uses MSTS rigid-body rotation method for curve on a grade.)
        /// </summary>
        /// <param name="vPC">Local position vector for Point-of-Curve (PC) in x-z plane.</param>
        /// <param name="vPCO">Units vector in direction from PC to arc center (O).</param>
        /// <param name="rotation">Rotation matrix that deflects arc from PC to a point on curve (P).</param>
        /// <param name="pitchYawRoll">>Vector3 containing Yaw, Pitch, and Roll components.</param>
        /// <param name="position">Position vector for desired point on curve (P), returned by reference.</param>
        /// <param name="displacement">Displacement vector from PC to P in world coordinates, returned by reference.</param>
        public static void InterpolateAlongCurveLine(in Vector3 vPC, in Vector3 vPCO, Matrix rotation, in Vector3 pitchYawRoll, out Vector3 position, out Vector3 displacement)
        {
            Matrix matrix = MatrixExtension.CreateFromYawPitchRoll(pitchYawRoll);
            // Shared method returns displacement from present world position and, by reference,
            // local position in x-z plane of end of this section
            position = Vector3.Transform(-vPCO, rotation); // Rotate O_PC to O_P
            position = vPC + vPCO + position; // Position of P relative to PC
            Vector3.Transform(ref position, ref matrix, out displacement); // Transform to world coordinates and return as displacement.
        }

        /// <summary>
        /// InterpolateAlongStraight interpolates position along a straight stretch.
        /// </summary>
        /// <param name="vP0">Local position vector for starting point P0 in x-z plane.</param>
        /// <param name="startToEnd">Units vector in direction from starting point P0 to target point P.</param>
        /// <param name="offset">Distance from start to P.</param>
        /// <param name="pitchYawRoll">Vector3 containing Yaw, Pitch, and Roll components.</param>
        /// <param name="vP">Position vector for desired point(P), returned by reference.</param>
        /// <returns>Displacement vector from P0 to P in world coordinates.</returns>
        public static void InterpolateAlongStraightLine(in Vector3 vP0, Vector3 vP0P, float offset, in Vector3 pitchYawRoll, out Vector3 position, out Vector3 displacement)
        {
            Quaternion.CreateFromYawPitchRoll(pitchYawRoll.Y, pitchYawRoll.X, pitchYawRoll.Z, out Quaternion quaternion);
            Matrix.CreateFromQuaternion(ref quaternion, out Matrix matrix);
            position = vP0 + offset * vP0P; // Position of desired point in local coordinates.
            Vector3.Transform(ref position, ref matrix, out displacement);
        }

        /// <summary>
        /// MSTSInterpolateAlongCurve interpolates position along a circular arc.
        /// (Uses MSTS rigid-body rotation method for curve on a grade.)
        /// </summary>
        /// <param name="vPC">Local position vector for Point-of-Curve (PC) in x-z plane.</param>
        /// <param name="vPCO">Units vector in direction from PC to arc center (O).</param>
        /// <param name="mRotY">Rotation matrix that deflects arc from PC to a point on curve (P).</param>
        /// <param name="mWorld">Transformation from local to world coordinates.</param>
        /// <param name="vP">Position vector for desired point on curve (P), returned by reference.</param>
        /// <returns>Displacement vector from PC to P in world coordinates.</returns>
        public static Vector3 MSTSInterpolateAlongCurve(Vector3 vPC, Vector3 vPCO, Matrix mRotY, Matrix mWorld, out Vector3 vP)
        {
            // Shared method returns displacement from present world position and, by reference,
            // local position in x-z plane of end of this section
            Vector3 vO_P = Vector3.Transform(-vPCO, mRotY); // Rotate O_PC to O_P
            vP = vPC + vPCO + vO_P; // Position of P relative to PC
            return Vector3.Transform(vP, mWorld); // Transform to world coordinates and return as displacement.
        }

        /// <summary>
        /// MSTSInterpolateAlongStraight interpolates position along a straight stretch.
        /// </summary>
        /// <param name="vP0">Local position vector for starting point P0 in x-z plane.</param>
        /// <param name="vP0P">Units vector in direction from P0 to P.</param>
        /// <param name="offset">Distance from P0 to P.</param>
        /// <param name="mWorld">Transformation from local to world coordinates.</param>
        /// <param name="vP">Position vector for desired point(P), returned by reference.</param>
        /// <returns>Displacement vector from P0 to P in world coordinates.</returns>
        public static Vector3 MSTSInterpolateAlongStraight(Vector3 vP0, Vector3 vP0P, float offset, Matrix mWorld, out Vector3 vP)
        {
            vP = vP0 + offset * vP0P; // Position of desired point in local coordinates.
            return Vector3.Transform(vP, mWorld);
        }


    }
}
