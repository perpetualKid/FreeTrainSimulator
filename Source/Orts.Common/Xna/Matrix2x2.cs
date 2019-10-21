namespace Orts.Common.Xna
{
    public readonly struct Matrix2x2
    {
        public readonly float M00, M01, M10, M11;

        public Matrix2x2(float m00, float m01, float m10, float m11)
        {
            M00 = m00; M01 = m01; M10 = m10; M11 = m11;
        }

        public float Interpolate2D(float x, float z)
        {
            float result = 0;

            result += (1 - x) * (1 - z) * M00;
            result += (x) * (1 - z) * M01;
            result += (1 - x) * (z) * M10;
            result += (x) * (z) * M11;

            return result;
        }

    }
}
