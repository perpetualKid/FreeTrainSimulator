using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.Common.Xna
{
    public readonly struct Vector2D
    {
        public readonly double X;

        public readonly double Y;


        private static readonly Vector2D zeroVector = new Vector2D(0, 0);

        public static ref readonly Vector2D Zero => ref zeroVector;

        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Returns the length of this <see cref="Vector2"/>.
        /// </summary>
        /// <returns>The length of this <see cref="Vector2"/>.</returns>
        public double Length()
        {
            return Math.Sqrt((X * X) + (Y * Y));
        }

        #region operators
        /// <summary>
        /// Adds two vectors.
        /// </summary>
        /// <param name="value1">Source <see cref="Vector2D"/> on the left of the add sign.</param>
        /// <param name="value2">Source <see cref="Vector2D"/> on the right of the add sign.</param>
        /// <returns>Sum of the vectors.</returns>
        public static Vector2D operator +(Vector2D value1, Vector2D value2)
        {
            return new Vector2D(value1.X + value2.X, value1.Y + value2.Y);
        }

        /// <summary>
        /// Divides the components of a <see cref="Vector2D"/> by the components of another <see cref="Vector2D"/>.
        /// </summary>
        /// <param name="value1">Source <see cref="Vector2D"/> on the left of the div sign.</param>
        /// <param name="value2">Divisor <see cref="Vector2D"/> on the right of the div sign.</param>
        /// <returns>The result of dividing the vectors.</returns>
        public static Vector2D operator /(Vector2D value1, Vector2D value2)
        {
            return new Vector2D(value1.X / value2.X, value1.Y / value2.Y);
        }

        /// <summary>
        /// Divides the components of a <see cref="Vector2D"/> by a scalar.
        /// </summary>
        /// <param name="value1">Source <see cref="Vector2D"/> on the left of the div sign.</param>
        /// <param name="divider">Divisor scalar on the right of the div sign.</param>
        /// <returns>The result of dividing a vector by a scalar.</returns>
        public static Vector2D operator /(Vector2D value1, double divider)
        {
            return new Vector2D(value1.X / divider, value1.Y / divider) ;
        }


        #endregion
    }
}
