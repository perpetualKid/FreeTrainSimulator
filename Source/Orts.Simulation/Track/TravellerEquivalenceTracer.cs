using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Runtime.Track;

using Orts.Formats.Msts;

namespace Orts.Simulation.Track
{
    /// <summary>
    /// Compares the state of a legacy <see cref="Traveller"/> with a <see cref="TrackTraveller"/>
    /// and traces any mismatches. Used during the incremental migration to verify that
    /// both traveller types produce equivalent results before the old one is removed.
    /// </summary>
    /// <remarks>
    /// All comparison methods are no-ops when <see cref="Enabled"/> is <see langword="false"/>
    /// (the default), resulting in zero runtime cost in release builds.
    /// </remarks>
    internal static class TravellerEquivalenceTracer
    {
        private const float LocationToleranceXZ = 0.5f;    // metres
        private const float LocationToleranceY = 1.0f;     // metres

        /// <summary>
        /// Master switch. Automatically enabled in DEBUG builds.
        /// When <see langword="false"/>, <see cref="AssertEquivalent"/> returns immediately.
        /// </summary>
        public static bool Enabled { get; set; }
#if DEBUG
            = true;
#endif

        /// <summary>
        /// Convenience overload accepting a nullable <see cref="TrackTraveller"/>?.
        /// Returns <see langword="true"/> (skip) when <paramref name="newTraveller"/> is <see langword="null"/>,
        /// meaning the dual-write path has not been wired yet for this call site.
        /// </summary>
        public static bool AssertEquivalent(Traveller oldTraveller, in TrackTraveller? newTraveller, [CallerMemberName] string caller = "")
        {
            return !newTraveller.HasValue || AssertEquivalent(oldTraveller, newTraveller.Value, caller);
        }

        /// <summary>
        /// Compares the position and direction state of <paramref name="oldTraveller"/> with
        /// <paramref name="newTraveller"/> and traces any mismatches via <see cref="Trace.TraceWarning(string)"/>.
        /// </summary>
        /// <param name="oldTraveller">The legacy <see cref="Traveller"/>.</param>
        /// <param name="newTraveller">The new <see cref="TrackTraveller"/>.</param>
        /// <param name="caller">Caller context for the trace message (auto-filled).</param>
        /// <returns><see langword="true"/> when both travellers are equivalent within tolerance; <see langword="false"/> otherwise.</returns>
        public static bool AssertEquivalent(Traveller oldTraveller, in TrackTraveller newTraveller, [CallerMemberName] string caller = "")
        {
            if (!Enabled)
                return true;

            ArgumentNullException.ThrowIfNull(oldTraveller);

            bool equivalent = true;

            // Direction
            bool oldForward = oldTraveller.Direction == Direction.Forward;
            bool newForward = newTraveller.Direction == TrackDirection.Ahead;
            if (oldForward != newForward)
            {
                Trace.TraceWarning($"[TravellerTrace:{caller}] Direction mismatch: old={oldTraveller.Direction} new={newTraveller.Direction}");
                equivalent = false;
            }

            // TrackNodeIndex
            int oldNodeIndex = oldTraveller.TrackNode?.Index ?? -1;
            int newNodeIndex = newTraveller.TrackNodeIndex;
            if (oldNodeIndex != newNodeIndex)
            {
                Trace.TraceWarning($"[TravellerTrace:{caller}] TrackNodeIndex mismatch: old={oldNodeIndex} new={newNodeIndex}");
                equivalent = false;
            }

            // SectionIndex — allow off-by-one at section boundaries.
            // When the legacy Traveller sits at the exact end of section N, its WorldLocation
            // is also the geometric start of section N+1. The snap-based TrackTraveller
            // initialization may pick N+1 because the start-of-section control point is a
            // marginally better floating-point match than the end-of-section interpolated point.
            // Both indices are valid representations of the same position; the Location check
            // below confirms positional equivalence.
            int oldSectionIndex = oldTraveller.TrackVectorSectionIndex;
            int newSectionIndex = newTraveller.SectionIndex;
            if (Math.Abs(oldSectionIndex - newSectionIndex) > 1)
            {
                Trace.TraceWarning($"[TravellerTrace:{caller}] SectionIndex mismatch: old={oldSectionIndex} new={newSectionIndex}");
                equivalent = false;
            }

            // Offset comparison intentionally omitted.
            // Traveller.TrackNodeOffset / TrackSectionOffset have lazy caching side effects
            // (SetLength sets lengthSet = true). The copy constructor propagates this cached state
            // without adjusting for direction reversal, so reading the offset here would poison
            // any subsequent reversed copy. The Location (XZ + Y) check below provides a
            // side-effect-free positional equivalence verification instead.

            // Location (2D XZ distance + Y)
            WorldLocation oldLoc = oldTraveller.WorldLocation;
            WorldLocation newLoc = newTraveller.Location;
            double distSq = WorldLocation.GetDistanceSquared2D(oldLoc, newLoc);
            if (distSq > LocationToleranceXZ * LocationToleranceXZ)
            {
                Trace.TraceWarning($"[TravellerTrace:{caller}] Location XZ mismatch: dist={Math.Sqrt(distSq):F3}m old={oldLoc} new={newLoc}");
                equivalent = false;
            }

            float dy = Math.Abs(oldLoc.Location.Y - newLoc.Location.Y);
            if (dy > LocationToleranceY)
            {
                Trace.TraceWarning($"[TravellerTrace:{caller}] Location Y mismatch: deltaY={dy:F3}m old={oldLoc.Location.Y:F3} new={newLoc.Location.Y:F3}");
                equivalent = false;
            }

            return equivalent;
        }
    }
}
