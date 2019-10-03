namespace Orts.Common.Xna
{
    public readonly struct Matrix3x3
    {
        public readonly float M00, M01, M02, M10, M11, M12, M20, M21, M22;

        public Matrix3x3(float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22)
        {
            M00 = m00;
            M01 = m01;
            M02 = m02;
            M10 = m10;
            M11 = m11;
            M12 = m12;
            M20 = m20;
            M21 = m21;
            M22 = m22;
        }
    }
}
