using System;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Xna
{
    public static class MatrixExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Multiply(in Matrix matrix1, in Matrix matrix2, out Matrix result)
        {
            result.M11 = matrix1.M11 * matrix2.M11 + matrix1.M12 * matrix2.M21 + matrix1.M13 * matrix2.M31 + matrix1.M14 * matrix2.M41;
            result.M12 = matrix1.M11 * matrix2.M12 + matrix1.M12 * matrix2.M22 + matrix1.M13 * matrix2.M32 + matrix1.M14 * matrix2.M42;
            result.M13 = matrix1.M11 * matrix2.M13 + matrix1.M12 * matrix2.M23 + matrix1.M13 * matrix2.M33 + matrix1.M14 * matrix2.M43;
            result.M14 = matrix1.M11 * matrix2.M14 + matrix1.M12 * matrix2.M24 + matrix1.M13 * matrix2.M34 + matrix1.M14 * matrix2.M44;
            result.M21 = matrix1.M21 * matrix2.M11 + matrix1.M22 * matrix2.M21 + matrix1.M23 * matrix2.M31 + matrix1.M24 * matrix2.M41;
            result.M22 = matrix1.M21 * matrix2.M12 + matrix1.M22 * matrix2.M22 + matrix1.M23 * matrix2.M32 + matrix1.M24 * matrix2.M42;
            result.M23 = matrix1.M21 * matrix2.M13 + matrix1.M22 * matrix2.M23 + matrix1.M23 * matrix2.M33 + matrix1.M24 * matrix2.M43;
            result.M24 = matrix1.M21 * matrix2.M14 + matrix1.M22 * matrix2.M24 + matrix1.M23 * matrix2.M34 + matrix1.M24 * matrix2.M44;
            result.M31 = matrix1.M31 * matrix2.M11 + matrix1.M32 * matrix2.M21 + matrix1.M33 * matrix2.M31 + matrix1.M34 * matrix2.M41;
            result.M32 = matrix1.M31 * matrix2.M12 + matrix1.M32 * matrix2.M22 + matrix1.M33 * matrix2.M32 + matrix1.M34 * matrix2.M42;
            result.M33 = matrix1.M31 * matrix2.M13 + matrix1.M32 * matrix2.M23 + matrix1.M33 * matrix2.M33 + matrix1.M34 * matrix2.M43;
            result.M34 = matrix1.M31 * matrix2.M14 + matrix1.M32 * matrix2.M24 + matrix1.M33 * matrix2.M34 + matrix1.M34 * matrix2.M44;
            result.M41 = matrix1.M41 * matrix2.M11 + matrix1.M42 * matrix2.M21 + matrix1.M43 * matrix2.M31 + matrix1.M44 * matrix2.M41;
            result.M42 = matrix1.M41 * matrix2.M12 + matrix1.M42 * matrix2.M22 + matrix1.M43 * matrix2.M32 + matrix1.M44 * matrix2.M42;
            result.M43 = matrix1.M41 * matrix2.M13 + matrix1.M42 * matrix2.M23 + matrix1.M43 * matrix2.M33 + matrix1.M44 * matrix2.M43;
            result.M44 = matrix1.M41 * matrix2.M14 + matrix1.M42 * matrix2.M24 + matrix1.M43 * matrix2.M34 + matrix1.M44 * matrix2.M44;
        }

        public static Matrix Multiply(in Matrix matrix1, in Matrix matrix2)
        {
            Multiply(matrix1, matrix2, out Matrix result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix SetTranslation(in Matrix matrix, in Vector3 position)
        {
            return SetTranslation(matrix, position.X, position.Y, position.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix SetTranslation(in Matrix matrix, float x, float y, float z)
        {
            return new Matrix(matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                x, y, z, matrix.M44);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix ChangeTranslation(in Matrix matrix, in Vector3 positionDelta)
        {
            return ChangeTranslation(matrix, positionDelta.X, positionDelta.Y, positionDelta.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix ChangeTranslation(in Matrix matrix, float xDelta, float yDelta, float zDelta)
        {
            return new Matrix(matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41 + xDelta, matrix.M42 + yDelta, matrix.M43 + zDelta, matrix.M44);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix RemoveTranslation(in Matrix matrix)
        {
            return SetTranslation(matrix, Vector3.Zero);
        }

        //
        // from http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToEuler/index.htm
        //
        public static void MatrixToAngles(this in Matrix m, out float heading, out float attitude, out float bank)
        {    // Assuming the angles are in radians.
            if (m.M21 > 0.998)
            { // singularity at north pole
                heading = (float)Math.Atan2(m.M13, m.M33);
                attitude = MathHelper.PiOver2;
                bank = 0;
            }
            else if (m.M21 < -0.998)
            { // singularity at south pole
                heading = (float)Math.Atan2(m.M13, m.M33);
                attitude = -MathHelper.PiOver2;
                bank = 0;
            }
            else
            {
                heading = (float)Math.Atan2(-m.M31, m.M11);
                bank = (float)Math.Atan2(-m.M23, m.M22);
                attitude = (float)Math.Asin(m.M21);
            }
        }

        public static float MatrixToYAngle(this in Matrix m)
        {    // Assuming the angles are in radians.

            return m.M21 == 0.998 ? (float)Math.Atan2(-m.M31, m.M11) : (float)Math.Atan2(m.M13, m.M33);
        }

        public static Matrix CreateFromYawPitchRoll(in Vector3 pitchYawRoll)
        {
            Quaternion.CreateFromYawPitchRoll(-pitchYawRoll.Y, -pitchYawRoll.X, pitchYawRoll.Z, out Quaternion quaternion);
            Matrix.CreateFromQuaternion(ref quaternion, out Matrix matrix);
            return matrix;
        }

        /// <summary>
        /// The front end of a railcar is at MSTS world coordinates x1,y1,z1
        /// The other end is at x2,y2,z2
        /// Return a rotation and translation matrix for the center of the railcar.
        /// </summary>
        public static Matrix XNAMatrixFromMSTSCoordinates(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            // translate 1st coordinate to be relative to 0,0,0
            float dx = (float)(x1 - x2);
            float dy = (float)(y1 - y2);
            float dz = (float)(z1 - z2);

            // compute the rotational matrix  
            float length = (float)Math.Sqrt(dx * dx + dz * dz + dy * dy);
            float run = (float)Math.Sqrt(dx * dx + dz * dz);
            // normalize to coordinate to a length of one, ie dx is change in x for a run of 1
            if (length != 0)    // Avoid zero divide
            {
                dx /= length;
                dy /= length;   // ie if it is tilted back 5 degrees, this is sin 5 = 0.087
                run /= length;  //                              and   this is cos 5 = 0.996
                dz /= length;
            }
            else
            {                   // If length is zero all elements of its calculation are zero. Since dy is a sine and is zero,
                run = 1f;       // run is therefore 1 since it is cosine of the same angle?  See comments above.
            }


            // setup matrix values
            Matrix xnaTilt = new Matrix(1, 0, 0, 0,
                                     0, run, dy, 0,
                                     0, -dy, run, 0,
                                     0, 0, 0, 1);

            Matrix xnaRotation = new Matrix(dz, 0, dx, 0,
                                            0, 1, 0, 0,
                                            -dx, 0, dz, 0,
                                            0, 0, 0, 1);

            Matrix xnaLocation = Matrix.CreateTranslation((x1 + x2) / 2f, (y1 + y2) / 2f, -(z1 + z2) / 2f);
            Multiply(xnaTilt, xnaRotation, out Matrix result);
            return Multiply(result, xnaLocation);
        }
    }
}
