using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Runtime.Track;

using Orts.Formats.Msts;

namespace Orts.Simulation.Track
{
    /// <summary>
    /// Bridges the legacy <see cref="Traveller"/> to the new <see cref="TrackTraveller"/>.
    /// Provides a shared conversion method used during the incremental migration period
    /// where both traveller types coexist.
    /// </summary>
    internal static class TravellerBridge
    {
        /// <summary>
        /// Creates a <see cref="TrackTraveller"/> that matches the position and direction
        /// of the given legacy <see cref="Traveller"/> by snapping to the nearest track section
        /// on the preferred track node.
        /// Returns <see langword="null"/> when the traveller's position cannot be snapped
        /// (e.g. when the legacy traveller sits on an <c>EndNode</c> beyond the last track section).
        /// </summary>
        /// <param name="traveller">The legacy traveller to convert.</param>
        /// <returns>A <see cref="TrackTraveller"/> at the same position, or <see langword="null"/> if snapping failed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="traveller"/> is <see langword="null"/>.</exception>
        public static TrackTraveller? ToTrackTraveller(Traveller traveller)
        {
            ArgumentNullException.ThrowIfNull(traveller);

            TrackDirection direction = traveller.Direction == Direction.Forward
                ? TrackDirection.Ahead
                : TrackDirection.Reverse;
            int preferredNodeIndex = traveller.TrackNode?.Index ?? -1;

            return preferredNodeIndex >= 0
                ? TrackTraveller.InitializeTraveller(traveller.WorldLocation, preferredNodeIndex, direction)
                : TrackTraveller.InitializeTraveller(traveller.WorldLocation, direction);
        }
    }
}
