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
    }
}
