using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Pre-computed geometric data for one <see cref="VectorSectionNode"/> instance,
    /// combining the placed-section data with the resolved geometry template from
    /// <see cref="TrackSection"/> and caching values that are expensive to recompute on every
    /// traversal step: arc centre point and orthonormal basis vectors for curved sections.
    /// Stored in <see cref="TrackWorld.SectionGeometry"/>, keyed by
    /// <see cref="VectorSectionNode"/> reference identity.
    /// </summary>
    public sealed class SectionGeometry
    {
        /// <summary>The <see cref="VectorNode"/> that owns this section.</summary>
        public VectorNode Node { get; }

        /// <summary>Zero-based index of this section within <see cref="Node.VectorSections"/>.</summary>
        public int SectionIndex { get; }

        /// <summary><see langword="true"/> when a <see cref="TrackSection"/> geometry template was resolved for this section.</summary>
        public bool HasGeometry { get; }

        /// <summary>Arc or straight length of the section in metres. Zero when <see cref="HasGeometry"/> is <see langword="false"/>.</summary>
        public double Length { get; }

        /// <summary><see langword="true"/> for an arc (curved) section; <see langword="false"/> for a straight section.</summary>
        public bool Curved { get; }

        // ── Curved-section data (valid only when Curved is true) ───────────────────────

        /// <summary>Arc radius in metres.</summary>
        public double Radius { get; }

        /// <summary>Total arc angle in radians (pre-converted from <see cref="TrackSection.Angle"/> in degrees).</summary>
        public double ArcAngle { get; }

        /// <summary>Pre-computed arc centre point.</summary>
        public WorldLocation ArcCenter { get; }

        /// <summary>Unit vector from <see cref="ArcCenter"/> toward the section start (arc basis vector <i>u</i>).</summary>
        public Vector3 U { get; }

        /// <summary>Tangent at the section start in the direction toward the section end (arc basis vector <i>v</i>).</summary>
        public Vector3 V { get; }

        // ── Super-elevation (valid only for curved sections; set by SuperElevation at sim start) ──

        /// <summary>Super-elevation rotation (radians) at the start of the section.
        /// Zero means the section ramps up from zero; non-zero means it is already at full tilt.
        /// Set by <c>Orts.Simulation.SuperElevation</c> via <see cref="SetElevation"/> after track geometry is loaded.</summary>
        public float StartElevation { get; private set; }

        /// <summary>Super-elevation rotation (radians) at the end of the section.
        /// Zero means the section ramps back down to zero; non-zero means it stays at full tilt.
        /// Set by <c>Orts.Simulation.SuperElevation</c> via <see cref="SetElevation"/> after track geometry is loaded.</summary>
        public float EndElevation { get; private set; }

        /// <summary>Maximum (peak) super-elevation rotation (radians) for this section.
        /// Zero when no super-elevation applies.
        /// Set by <c>Orts.Simulation.SuperElevation</c> via <see cref="SetElevation"/> after track geometry is loaded.</summary>
        public float MaxElevation { get; private set; }

        /// <summary>
        /// Sets all three super-elevation values in a single call.
        /// Intended to be called once by <c>Orts.Simulation.SuperElevation</c> during simulation initialisation.
        /// </summary>
        public void SetElevation(float startElevation, float endElevation, float maxElevation)
        {
            StartElevation = startElevation;
            EndElevation = endElevation;
            MaxElevation = maxElevation;
        }

        internal SectionGeometry(VectorNode node, int sectionIndex, TrackSection trackSection, VectorSectionNode section)
        {
            Node = node;
            SectionIndex = sectionIndex;

            if (trackSection == null)
                return;

            HasGeometry = true;
            Length = trackSection.Length;
            Curved = trackSection.Curved;

            if (!Curved)
                return;

            Radius = trackSection.Radius;
            ArcAngle = MathHelper.ToRadians(trackSection.Angle);
            ArcCenter = WorldLocation.ArcCenterPoint(section.Location, section.EndLocation, ArcAngle, Radius);

            Vector3 centerToStart = WorldLocation.GetDistanceVector(ArcCenter, section.Location);
            Vector3 k = Vector3.Normalize(Vector3.Cross(centerToStart,
                WorldLocation.GetDistanceVector(ArcCenter, section.EndLocation)));
            U = Vector3.Normalize(centerToStart);
            V = Vector3.Cross(k, U);
        }

        /// <summary>
        /// Returns the squared perpendicular distance (in metres²) from <paramref name="location"/> to this section,
        /// or <see cref="double.NaN"/> when the point does not project onto the section (i.e. is beyond the endpoints).
        /// Points within <see cref="WorldLocation.ProximityTolerance"/> of either endpoint are considered on-section.
        /// </summary>
        public double DistanceSquared(in WorldLocation location)
        {
            if (!HasGeometry)
                return double.NaN;

            VectorSectionNode section = Node.VectorSections[SectionIndex];

            if (Curved)
            {
                Vector3 centerToPoint = WorldLocation.GetDistanceVector(ArcCenter, location);
                centerToPoint.Y = 0; // horizontal-only: callers may pass elevation=0 (from PointD)
                double distFromCenter = centerToPoint.Length();
                double radialError = distFromCenter - Radius;

                // Check angular bounds: the point's angle from centre must fall within the arc span.
                // Project onto the U/V basis to get the parametric angle.
                double dotU = Vector3.Dot(centerToPoint, U);
                double dotV = Vector3.Dot(centerToPoint, V);
                double pointAngle = Math.Atan2(dotV, dotU); // angle from start direction

                // The U/V basis is built so the arc always sweeps from 0 to +|ArcAngle| (the end always
                // projects to +absArc via the right-handed cross product), independent of the stored
                // ArcAngle sign. So on-arc points always fall in [0, absArc].
                double absArc = Math.Abs(ArcAngle);
                double angleTolerance = WorldLocation.ProximityTolerance / Radius;
                bool inArc = pointAngle >= -angleTolerance && pointAngle <= absArc + angleTolerance;

                if (inArc)
                    return radialError * radialError;

                // Outside arc span — check endpoint proximity
                double startDist = WorldLocation.GetDistanceSquared2D(section.Location, location);
                if (startDist <= WorldLocation.ProximityTolerance)
                    return startDist;
                double endDist = WorldLocation.GetDistanceSquared2D(section.EndLocation, location);
                return endDist <= WorldLocation.ProximityTolerance ? endDist : double.NaN;
            }
            else
            {
                // Straight section: project point onto the line segment start→end (horizontal plane only)
                Vector3 segVec = WorldLocation.GetDistanceVector(section.Location, section.EndLocation);
                Vector3 toPoint = WorldLocation.GetDistanceVector(section.Location, location);
                segVec.Y = 0;
                toPoint.Y = 0;
                double segLenSq = segVec.LengthSquared();

                if (segLenSq < 1e-12)
                    return WorldLocation.GetDistanceSquared2D(section.Location, location);

                double t = Vector3.Dot(toPoint, segVec) / segLenSq;

                if (t < 0)
                {
                    double d = WorldLocation.GetDistanceSquared2D(section.Location, location);
                    return d <= WorldLocation.ProximityTolerance ? d : double.NaN;
                }
                if (t > 1)
                {
                    double d = WorldLocation.GetDistanceSquared2D(section.EndLocation, location);
                    return d <= WorldLocation.ProximityTolerance ? d : double.NaN;
                }

                // Closest point on segment: start + t * segVec
                Vector3 closest = new Vector3(
                    toPoint.X - (float)(t * segVec.X),
                    0,
                    toPoint.Z - (float)(t * segVec.Z));
                return closest.LengthSquared();
            }
        }

        /// <summary>
        /// Returns the squared nearest horizontal distance from <paramref name="location"/> to this section.
        /// Unlike <see cref="DistanceSquared"/>, points outside the section extents are measured to the nearest endpoint.
        /// </summary>
        public double NearestDistanceSquared(in WorldLocation location)
        {
            if (!HasGeometry)
                return double.NaN;

            VectorSectionNode section = Node.VectorSections[SectionIndex];

            if (Curved)
            {
                Vector3 centerToPoint = WorldLocation.GetDistanceVector(ArcCenter, location);
                centerToPoint.Y = 0;
                double distFromCenter = centerToPoint.Length();
                double radialError = distFromCenter - Radius;

                double dotU = Vector3.Dot(centerToPoint, U);
                double dotV = Vector3.Dot(centerToPoint, V);
                double pointAngle = Math.Atan2(dotV, dotU);

                // The arc always sweeps from 0 to +|ArcAngle| in the U/V basis (see DistanceSquared),
                // so on-arc points fall in [0, absArc] regardless of the stored ArcAngle sign.
                // No tolerance padding is needed here (unlike DistanceSquared): points just past an
                // endpoint already fall through to the endpoint-distance branch below.
                double absArc = Math.Abs(ArcAngle);
                bool inArc = pointAngle >= 0 && pointAngle <= absArc;

                if (inArc)
                    return radialError * radialError;

                return Math.Min(
                    WorldLocation.GetDistanceSquared2D(section.Location, location),
                    WorldLocation.GetDistanceSquared2D(section.EndLocation, location));
            }

            Vector3 segVec = WorldLocation.GetDistanceVector(section.Location, section.EndLocation);
            Vector3 toPoint = WorldLocation.GetDistanceVector(section.Location, location);
            segVec.Y = 0;
            toPoint.Y = 0;
            double segLenSq = segVec.LengthSquared();

            if (segLenSq < 1e-12)
                return WorldLocation.GetDistanceSquared2D(section.Location, location);

            double t = Math.Clamp(Vector3.Dot(toPoint, segVec) / segLenSq, 0.0, 1.0);
            Vector3 closest = new Vector3(
                toPoint.X - (float)(t * segVec.X),
                0,
                toPoint.Z - (float)(t * segVec.Z));
            return closest.LengthSquared();
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="location"/> is within
        /// <see cref="WorldLocation.ProximityTolerance"/> of this section.
        /// </summary>
        public bool SectionAt(in WorldLocation location)
        {
            double d = DistanceSquared(location);
            return !double.IsNaN(d) && d <= WorldLocation.ProximityTolerance;
        }

        /// <summary>
        /// Projects <paramref name="location"/> onto this section, returning the nearest point on the section geometry.
        /// Falls back to <paramref name="location"/> when the section has no geometry.
        /// </summary>
        public WorldLocation SnapToSection(in WorldLocation location)
        {
            if (!HasGeometry)
                return location;

            VectorSectionNode section = Node.VectorSections[SectionIndex];

            if (Curved)
            {
                Vector3 centerToPoint = WorldLocation.GetDistanceVector(ArcCenter, location);
                double dotU = Vector3.Dot(centerToPoint, U);
                double dotV = Vector3.Dot(centerToPoint, V);
                double pointAngle = Math.Atan2(dotV, dotU);

                // The arc always sweeps from 0 to +|ArcAngle| in the U/V basis (see DistanceSquared),
                // so clamp the parametric angle into [0, absArc] regardless of the stored ArcAngle sign.
                double absArc = Math.Abs(ArcAngle);
                pointAngle = Math.Clamp(pointAngle, 0, absArc);

                double distance = pointAngle * Radius;
                return WorldLocation.PointAlongArc(section.Location, section.EndLocation, ArcAngle, Radius, distance);
            }
            else
            {
                Vector3 segVec = WorldLocation.GetDistanceVector(section.Location, section.EndLocation);
                Vector3 toPoint = WorldLocation.GetDistanceVector(section.Location, location);
                double segLenSq = segVec.LengthSquared();

                if (segLenSq < 1e-12)
                    return section.Location;

                double t = Math.Clamp(Vector3.Dot(toPoint, segVec) / segLenSq, 0, 1);
                return WorldLocation.PointAlongDirection(section.Location, section.EndLocation, t * Length);
            }
        }

        /// <summary>
        /// Returns the distance in metres from the section start to the point on the section nearest
        /// to <paramref name="location"/>. Returns <see cref="double.NaN"/> when the point is not on the section.
        /// </summary>
        public double DistanceOnSection(in WorldLocation location)
        {
            if (!HasGeometry || !SectionAt(location))
                return double.NaN;

            VectorSectionNode section = Node.VectorSections[SectionIndex];

            if (Curved)
            {
                Vector3 centerToPoint = WorldLocation.GetDistanceVector(ArcCenter, location);
                double dotU = Vector3.Dot(centerToPoint, U);
                double dotV = Vector3.Dot(centerToPoint, V);
                double pointAngle = Math.Atan2(dotV, dotU);
                // pointAngle is unclamped here; Math.Abs covers the tolerance band where a near-start
                // on-section point (admitted by SectionAt) can project to a slightly negative angle.
                return Math.Abs(pointAngle) * Radius;
            }
            else
            {
                Vector3 segVec = WorldLocation.GetDistanceVector(section.Location, section.EndLocation);
                Vector3 toPoint = WorldLocation.GetDistanceVector(section.Location, location);
                double segLenSq = segVec.LengthSquared();
                if (segLenSq < 1e-12)
                    return 0;
                double t = Math.Clamp(Vector3.Dot(toPoint, segVec) / segLenSq, 0, 1);
                return t * Length;
            }
        }

        /// <summary>
        /// Returns the 2D heading (Y-rotation in radians, north = 0, clockwise positive) at the point on
        /// this section nearest to <paramref name="location"/>.
        /// </summary>
        public float DirectionAt(in WorldLocation location)
        {
            if (!HasGeometry)
                return 0;

            VectorSectionNode section = Node.VectorSections[SectionIndex];

            if (Curved)
            {
                double offset = DistanceOnSection(location);
                if (double.IsNaN(offset))
                    offset = 0;
                return DirectionAt(offset);
            }
            else
            {
                return MathHelper.WrapAngle(section.Direction.Y - MathHelper.PiOver2);
            }
        }

        /// <summary>
        /// Returns the 2D heading (Y-rotation in radians) at <paramref name="distance"/> metres along the section.
        /// </summary>
        public float DirectionAt(double distance)
        {
            if (!HasGeometry)
                return 0;

            VectorSectionNode section = Node.VectorSections[SectionIndex];

            if (Curved)
            {
                double angularOffset = Radius > 0 ? distance / Radius : 0;
                float baseDirection = MathHelper.WrapAngle(section.Direction.Y - MathHelper.PiOver2);
                return baseDirection + (float)(Math.Sign(ArcAngle) * angularOffset);
            }
            else
            {
                return MathHelper.WrapAngle(section.Direction.Y - MathHelper.PiOver2);
            }
        }

        /// <summary>
        /// Returns the <see cref="WorldLocation"/> at <paramref name="distance"/> metres along the section.
        /// </summary>
        public WorldLocation LocationAt(double distance)
        {
            if (!HasGeometry)
                return Node.VectorSections[SectionIndex].Location;

            VectorSectionNode section = Node.VectorSections[SectionIndex];
            double clampedDistance = Math.Clamp(distance, 0, Length);

            return Curved
                ? WorldLocation.PointAlongArc(section.Location, section.EndLocation, ArcAngle, Radius, clampedDistance)
                : WorldLocation.PointAlongDirection(section.Location, section.EndLocation, clampedDistance);
        }
    }
}
