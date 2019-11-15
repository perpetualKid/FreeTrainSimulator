namespace Tests.Orts.Common
{
    //as single or double operations are limited in precision, we provide a cutoff delta to be 
    //used as parameter for Equality comparisions in Unit Tests
    public static class EqualityPrecisionDelta
    {
        public const float FloatPrecisionDelta = 0.00001f;           //5 fractional digits
        public const double DoublePrecisionDelta = 0.00000001;  //8 fractional digits
    }
}
