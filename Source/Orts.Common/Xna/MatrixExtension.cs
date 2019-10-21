using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace Orts.Common.Xna
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

            if (m.M21 == 0.998)
                return (float)Math.Atan2(-m.M31, m.M11);
            else            // singularity at poles
                return (float)Math.Atan2(m.M13, m.M33);
        }
    }
}
