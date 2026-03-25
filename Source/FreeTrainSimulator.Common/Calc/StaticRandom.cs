using System;
using System.Threading;

namespace FreeTrainSimulator.Common.Calc
{
    /// <summary>
    /// Thread-safe equivalent of System.Random, using just static methods.
    /// If all you want is a source of random numbers, this is an easy class to
    /// use. If you need to specify your own seeds (eg for reproducible sequences
    /// of numbers), use System.Random.
    /// It's safe to use for non-security relevant code
    /// </summary>
    public static class StaticRandom
    {
        private static volatile Random random = Random.Shared;
        private static readonly Lock lockObj = new Lock();

        public static void MakeDeterministic()
        {
            lock (lockObj)
                random = new Random(0);
        }

#pragma warning disable CA5394 // Do not use insecure randomness
        /// <summary>
        /// Returns a nonnegative random number. 
        /// </summary>		
        /// <returns>A 32-bit signed integer greater than or equal to zero and less than Int32.MaxValue.</returns>
        public static int Next()
        {
            var r = random;
            if (r == Random.Shared)
                return r.Next();
            lock (lockObj)
                return random.Next();
        }

        /// <summary>
        /// Returns a non negative random number less than the specified maximum. 
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer greater than or equal to zero, and less than maxValue; 
        /// that is, the range of return values includes zero but not maxValue.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">maxValue is less than zero.</exception>
        public static int Next(int max)
        {
            var r = random;
            if (r == Random.Shared)
                return r.Next(max);
            lock (lockObj)
                return random.Next(max);
        }

        /// <summary>
        /// Returns a random number within a specified range. 
        /// </summary>
        /// <param name="min">The inclusive lower bound of the random number returned. </param>
        /// <param name="max">
        /// The exclusive upper bound of the random number returned. 
        /// maxValue must be greater than or equal to minValue.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer greater than or equal to minValue and less than maxValue;
        /// that is, the range of return values includes minValue but not maxValue.
        /// If minValue equals maxValue, minValue is returned.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">minValue is greater than maxValue.</exception>
        public static int Next(int min, int max)
        {
            var r = random;
            if (r == Random.Shared)
                return r.Next(min, max);
            lock (lockObj)
                return random.Next(min, max);
        }

        /// <summary>
        /// Returns a random number between 0.0 and 1.0.
        /// </summary>
        /// <returns>A double-precision floating point number greater than or equal to 0.0, and less than 1.0.</returns>
        public static double NextDouble()
        {
            var r = random;
            if (r == Random.Shared)
                return r.NextDouble();
            lock (lockObj)
                return random.NextDouble();
        }

        /// <summary>
        /// Fills the elements of a specified array of bytes with random numbers.
        /// </summary>
        /// <param name="buffer">An array of bytes to contain random numbers.</param>
        /// <exception cref="ArgumentNullException">buffer is a null reference (Nothing in Visual Basic).</exception>
        public static void NextBytes(byte[] buffer)
        {
            var r = random;
            if (r == Random.Shared)
            {
                r.NextBytes(buffer);
                return;
            }
            lock (lockObj)
                random.NextBytes(buffer);
        }
#pragma warning restore CA5394 // Do not use insecure randomness
    }
}
