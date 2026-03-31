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
        private const double SectionOffsetTolerance = 0.5;  // metres (or equivalent radians * radius)

        /// <summary>
        /// Master switch. Automatically enabled in DEBUG builds.
        /// When <see langword="false"/>, <see cref="AssertEquivalent"/> returns immediately.
        /// </summary>
        public static bool Enabled { get; set; }
#if DEBUG
            = true;
#endif

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

            // SectionIndex
            int oldSectionIndex = oldTraveller.TrackVectorSectionIndex;
            int newSectionIndex = newTraveller.SectionIndex;
            if (oldSectionIndex != newSectionIndex)
            {
                Trace.TraceWarning($"[TravellerTrace:{caller}] SectionIndex mismatch: old={oldSectionIndex} new={newSectionIndex}");
                equivalent = false;
            }

            // SectionOffset
            float oldOffset = oldTraveller.TrackSectionOffset;
            double newOffset = newTraveller.SectionOffset;
            if (Math.Abs(oldOffset - newOffset) > SectionOffsetTolerance)
            {
                Trace.TraceWarning($"[TravellerTrace:{caller}] SectionOffset mismatch: old={oldOffset:F4} new={newOffset:F4} delta={Math.Abs(oldOffset - newOffset):F4}");
                equivalent = false;
            }

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
