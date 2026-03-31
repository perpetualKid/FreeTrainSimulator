using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Runtime.Track;

namespace Orts.Simulation.Track
{
    /// <summary>
    /// Pure-geometry utilities for train coupling overlap detection.
    /// All methods operate on <see cref="WorldLocation"/> and heading values,
    /// independent of any traveller type.
    /// </summary>
    internal static class CouplingGeometry
    {
        /// <summary>
        /// Computes the signed overlap distance between two track positions projected
        /// along the heading of <paramref name="selfLocation"/>.
        /// Returns a value &lt; 0 when the positions overlap (the other position is
        /// ahead/behind depending on <paramref name="rear"/>); returns 1 when the
        /// positions are more than 1 metre apart in any axis.
        /// </summary>
        /// <param name="selfLocation">Position of this end of the train.</param>
        /// <param name="selfHeadingY">Y-axis heading (RotY / Heading) at <paramref name="selfLocation"/> in radians.</param>
        /// <param name="otherLocation">Position of the other train's end.</param>
        /// <param name="rear"><see langword="true"/> when checking behind (rear coupling); <see langword="false"/> for front coupling.</param>
        /// <returns>Signed overlap distance; values &lt; 0 indicate overlap. Returns 1 when positions are far apart.</returns>
        public static float OverlapDistanceM(in WorldLocation selfLocation, float selfHeadingY, in WorldLocation otherLocation, bool rear)
        {
            Tile delta = selfLocation.Tile - otherLocation.Tile;
            float dx = selfLocation.Location.X - otherLocation.Location.X + 2048 * delta.X;
            float dz = selfLocation.Location.Z - otherLocation.Location.Z + 2048 * delta.Z;
            float dy = selfLocation.Location.Y - otherLocation.Location.Y;

            if (dx * dx + dz * dz > 1)
                return 1;
            if (MathF.Abs(dy) > 1)
                return 1;

            float dot = dx * MathF.Sin(selfHeadingY) + dz * MathF.Cos(selfHeadingY);
            return rear ? dot : -dot;
        }

        /// <summary>
        /// Rough overlap check used in multiplayer to avoid train overlapping.
        /// Performs coarse bounding checks using far-end positions and train lengths
        /// before falling back to the fine heading projection.
        /// </summary>
        /// <param name="selfLocation">Position of this end of the train.</param>
        /// <param name="selfHeadingY">Y-axis heading (RotY / Heading) at <paramref name="selfLocation"/> in radians.</param>
        /// <param name="otherLocation">Position of the other train's end.</param>
        /// <param name="farSelfLocation">Far end of this train (opposite end from <paramref name="selfLocation"/>).</param>
        /// <param name="farOtherLocation">Far end of the other train (opposite end from <paramref name="otherLocation"/>).</param>
        /// <param name="lengthSelf">Length of this train in metres.</param>
        /// <param name="lengthOther">Length of the other train in metres.</param>
        /// <param name="rear"><see langword="true"/> when checking behind (rear coupling); <see langword="false"/> for front coupling.</param>
        /// <returns>Signed overlap distance; values &lt; 0 indicate overlap. Returns 1 when positions are far apart.</returns>
        public static float RoughOverlapDistanceM(
            in WorldLocation selfLocation, float selfHeadingY,
            in WorldLocation otherLocation,
            in WorldLocation farSelfLocation,
            in WorldLocation farOtherLocation,
            float lengthSelf, float lengthOther, bool rear)
        {
            float dy = selfLocation.Location.Y - otherLocation.Location.Y;
            if (MathF.Abs(dy) > 1)
                return 1;

            Tile tileDelta = farSelfLocation.Tile - otherLocation.Tile;
            float dx = farSelfLocation.Location.X - otherLocation.Location.X + 2048 * tileDelta.X;
            float dz = farSelfLocation.Location.Z - otherLocation.Location.Z + 2048 * tileDelta.Z;
            if (dx * dx + dz * dz > lengthSelf * lengthSelf)
                return 1;

            tileDelta = selfLocation.Tile - farOtherLocation.Tile;
            dx = selfLocation.Location.X - farOtherLocation.Location.X + 2048 * tileDelta.X;
            dz = selfLocation.Location.Z - farOtherLocation.Location.Z + 2048 * tileDelta.Z;
            if (dx * dx + dz * dz > lengthOther * lengthOther)
                return 1;

            tileDelta = selfLocation.Tile - otherLocation.Tile;
            dx = selfLocation.Location.X - otherLocation.Location.X + 2048 * tileDelta.X;
            dz = selfLocation.Location.Z - otherLocation.Location.Z + 2048 * tileDelta.Z;
            float diagonal = dx * dx + dz * dz;
            if (diagonal < 200 && diagonal < (lengthSelf + lengthOther) * (lengthSelf + lengthOther))
            {
                float dot = dx * MathF.Sin(selfHeadingY) + dz * MathF.Cos(selfHeadingY);
                return rear ? dot : -dot;
            }

            return 1;
        }

        /// <summary>
        /// Convenience overload computing the signed overlap distance between two <see cref="TrackTraveller"/> positions.
        /// Delegates to <see cref="OverlapDistanceM(in WorldLocation, float, in WorldLocation, bool)"/>
        /// using the traveller's <see cref="TrackTraveller.Location"/> and <see cref="TrackTraveller.Heading"/>.
        /// </summary>
        public static float OverlapDistanceM(in TrackTraveller self, in TrackTraveller other, bool rear)
        {
            return OverlapDistanceM(self.Location, self.Heading, other.Location, rear);
        }
    }
}
