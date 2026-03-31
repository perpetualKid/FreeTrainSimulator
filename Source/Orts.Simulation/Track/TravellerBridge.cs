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
        /// </summary>
        /// <param name="traveller">The legacy traveller to convert.</param>
        /// <returns>A <see cref="TrackTraveller"/> at the same position.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="traveller"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The traveller's position could not be snapped onto the track database.</exception>
        public static TrackTraveller ToTrackTraveller(Traveller traveller)
        {
            ArgumentNullException.ThrowIfNull(traveller);

            TrackDirection direction = traveller.Direction == Direction.Forward
                ? TrackDirection.Ahead
                : TrackDirection.Reverse;
            int preferredNodeIndex = traveller.TrackNode?.Index ?? -1;

            TrackTraveller? initialized = preferredNodeIndex >= 0
                ? TrackTraveller.InitializeTraveller(traveller.WorldLocation, preferredNodeIndex, direction)
                : TrackTraveller.InitializeTraveller(traveller.WorldLocation, direction);

            return initialized ?? throw new InvalidOperationException(
                $"Unable to initialize {nameof(TrackTraveller)} from {nameof(Traveller)} at {traveller.WorldLocation}.");
        }
    }
}
