using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Positions itself on a track from a <see cref="WorldLocation"/> and provides virtual movement along the
    /// track geometry, crossing <see cref="VectorSectionNode"/> boundaries within the same <see cref="VectorNode"/>.
    /// Geometry is computed directly from <see cref="VectorSectionNode"/> and <see cref="TrackSection"/> data
    /// using <see cref="WorldLocation"/> math methods.
    /// Each instance is immutable; <see cref="Move"/> and <see cref="InitializeTraveller"/> return new instances
    /// rather than mutating state, enabling snapshot and look-ahead semantics.
    /// </summary>
    public record TrackTraveller
    {
        private static TrackWorld trackWorld;

        // Reference-equality map: VectorSectionNode instance → parent VectorNode, index within VectorSections,
        // and the resolved TrackSection (null for sections with no matching geometry template).
        // Populated once by BuildSectionOwnership; avoids per-traversal RuntimeDataResolver lookups.
        private static Dictionary<VectorSectionNode, (VectorNode node, int sectionIndex, TrackSection trackSection)> sectionOwnership
            = new Dictionary<VectorSectionNode, (VectorNode, int, TrackSection)>(ReferenceEqualityComparer.Instance);

        /// <summary>The <see cref="VectorNode"/> (track node) the traveller is currently on, or <see langword="null"/> if not on track.</summary>
        public VectorNode CurrentNode { get; private init; }

        /// <summary>The zero-based index of the current <see cref="VectorSectionNode"/> within <see cref="CurrentNode.VectorSections"/>.</summary>
        public int SectionIndex { get; private init; }

        /// <summary>The individual <see cref="VectorSectionNode"/> within <see cref="CurrentNode"/> that the traveller occupies,
        /// or <see langword="null"/> if not on track.</summary>
        public VectorSectionNode CurrentSection => OnTrack ? CurrentNode.VectorSections[SectionIndex] : null;

        /// <summary>The track-node index of the current <see cref="CurrentNode"/>, or <c>-1</c> if not on track.</summary>
        public int TrackNodeIndex => CurrentNode?.NodeIndex ?? -1;

        /// <summary>The current world location of the traveller on the track.</summary>
        public WorldLocation Location { get; private init; }

        /// <summary><see langword="true"/> when the traveller is positioned on a track section.</summary>
        public bool OnTrack => CurrentNode != null;

        /// <summary>The kind of track database (<see cref="TrackDataBaseType.Rail"/> or <see cref="TrackDataBaseType.Road"/>) this traveller operates on.</summary>
        public TrackDataBaseType TrackDataBaseType { get; private init; }

        /// <summary>Offset in metres from the start of <see cref="CurrentSection"/> to the traveller's position.</summary>
        public double SectionOffset { get; private init; }

        /// <summary>Direction of travel on <see cref="CurrentNode"/>: <see cref="TrackDirection.Ahead"/> advances from
        /// section 0 toward the last section; <see cref="TrackDirection.Reverse"/> retreats toward section 0.
        /// Updated automatically when <see cref="Move"/> crosses a <see cref="VectorNode"/> boundary.</summary>
        public TrackDirection Direction { get; private init; }

        /// <summary>
        /// Builds (or rebuilds) the shared ownership map from every <see cref="VectorSectionNode"/> instance
        /// to its parent <see cref="VectorNode"/>, section index, and resolved <see cref="TrackSection"/>,
        /// covering both the rail (<see cref="TrackModel.TrackDatabase"/>) and road
        /// (<see cref="TrackModel.RoadDatabase"/>) databases.
        /// Must be called once after <see cref="TrackWorld.Initialize"/> before any <see cref="TrackTraveller"/> is used.
        /// </summary>
        public static void Initialize(TrackWorld trackWorld)
        {
            ArgumentNullException.ThrowIfNull(trackWorld);
            TrackTraveller.trackWorld = trackWorld;
            sectionOwnership = BuildSectionOwnership(trackWorld);
        }

        /// <summary>
        /// Creates a new <see cref="TrackTraveller"/> not yet placed on any track.
        /// </summary>
        /// <param name="trackDataBaseType">Whether the traveller operates on rail (<see cref="TrackDataBaseType.Rail"/>, default)
        /// or road (<see cref="TrackDataBaseType.Road"/>) geometry.</param>
        private TrackTraveller(TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
        {
            TrackDataBaseType = trackDataBaseType;
        }

        // Equals and GetHashCode are overridden to include SectionIndex (private, excluded from synthesised record equality)
        // and to use reference equality for CurrentNode (correct for database object identity).
        public virtual bool Equals(TrackTraveller other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return TrackDataBaseType == other.TrackDataBaseType
                && ReferenceEquals(CurrentNode, other.CurrentNode)
                && SectionIndex == other.SectionIndex
                && SectionOffset == other.SectionOffset
                && Direction == other.Direction;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TrackDataBaseType, CurrentNode, SectionIndex, SectionOffset, Direction);
        }

        public override string ToString()
        {
            return $"{{TrackNode Index={TrackNodeIndex} TrackVectorSection Index={SectionIndex} Offset={SectionOffset:F6}}}";
        }

        /// <summary>
        /// Returns a new <see cref="TrackTraveller"/> placed on the nearest track section within
        /// <see cref="WorldLocation.ProximityTolerance"/> metres of <paramref name="location"/>,
        /// oriented <see cref="TrackDirection.Ahead"/> on the found node.
        /// The returned traveller's <see cref="Location"/> is snapped to the projected point on that section's geometry.
        /// </summary>
        /// <param name="location">The world location to search from.</param>
        /// <param name="trackDataBaseType">Whether to search rail (<see cref="TrackDataBaseType.Rail"/>, default)
        /// or road (<see cref="TrackDataBaseType.Road"/>) geometry.</param>
        /// <returns>A new <see cref="TrackTraveller"/> on the found section, or <see langword="null"/> if none was found.</returns>
        public static TrackTraveller InitializeTraveller(in WorldLocation location, TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
            => InitializeTraveller(location, TrackDirection.Ahead, trackDataBaseType);

        /// <summary>
        /// Returns a new <see cref="TrackTraveller"/> placed on the nearest track section within
        /// <see cref="WorldLocation.ProximityTolerance"/> metres of <paramref name="location"/>,
        /// oriented in <paramref name="direction"/> on the found node.
        /// The returned traveller's <see cref="Location"/> is snapped to the projected point on that section's geometry.
        /// </summary>
        /// <param name="location">The world location to search from.</param>
        /// <param name="direction">The initial direction of travel on the found <see cref="VectorNode"/>.</param>
        /// <param name="trackDataBaseType">Whether to search rail (<see cref="TrackDataBaseType.Rail"/>, default)
        /// or road (<see cref="TrackDataBaseType.Road"/>) geometry.</param>
        /// <returns>A new <see cref="TrackTraveller"/> on the found section, or <see langword="null"/> if none was found.</returns>
        public static TrackTraveller InitializeTraveller(in WorldLocation location, TrackDirection direction, TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
        {
            VectorSectionNode bestSection = null;
            WorldLocation bestSnapped = WorldLocation.None;
            double bestOffset = 0.0;
            double bestDistSq = WorldLocation.ProximityTolerance * WorldLocation.ProximityTolerance;

            MapContentType contentType = trackDataBaseType == TrackDataBaseType.Road ? MapContentType.Roads : MapContentType.Tracks;
            ITileIndexedList<ITileCoordinate> bucket = trackWorld.ContentByTile[contentType];
            if (bucket == null)
                return null;

            int tileRadius = WorldLocation.IsNearTileBoundary(location) ? 1 : 0;
            foreach (VectorSectionNode section in bucket.BoundingBox(location.Tile, tileRadius).Cast<VectorSectionNode>())
            {
                if (!TryGetTrackSection(section, out TrackSection trackSection))
                    continue;

                (WorldLocation snapped, double offset) = SnapToSection(location, section, trackSection);
                double distSq = WorldLocation.GetDistanceSquared(location, snapped);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestSection = section;
                    bestSnapped = snapped;
                    bestOffset = offset;
                }
            }

            return bestSection == null || !sectionOwnership.TryGetValue(bestSection, out var owner)
                ? null
                : new TrackTraveller(trackDataBaseType)
            {
                CurrentNode = owner.node,
                SectionIndex = owner.sectionIndex,
                SectionOffset = bestOffset,
                Location = bestSnapped,
                Direction = direction,
            };
        }

        /// <summary>
        /// Returns a new <see cref="TrackTraveller"/> placed on the nearest track section within
        /// <see cref="WorldLocation.ProximityTolerance"/> metres of <paramref name="location"/>
        /// restricted to the specified <paramref name="node"/>, oriented in <paramref name="direction"/>.
        /// The returned traveller's <see cref="Location"/> is snapped to the projected point on that section's geometry.
        /// </summary>
        /// <param name="location">The world location to search from.</param>
        /// <param name="node">The <see cref="VectorNode"/> to restrict the section search to.</param>
        /// <param name="direction">The initial direction of travel on <paramref name="node"/>.</param>
        /// <param name="trackDataBaseType">Whether to search rail (<see cref="TrackDataBaseType.Rail"/>, default)
        /// or road (<see cref="TrackDataBaseType.Road"/>) geometry.</param>
        /// <returns>A new <see cref="TrackTraveller"/> on the found section, or <see langword="null"/> if none was found.</returns>
        public static TrackTraveller InitializeTraveller(in WorldLocation location, VectorNode node, TrackDirection direction = TrackDirection.Ahead, TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
        {
            ArgumentNullException.ThrowIfNull(node);

            int bestIndex = -1;
            WorldLocation bestSnapped = WorldLocation.None;
            double bestOffset = 0.0;
            double bestDistSq = WorldLocation.ProximityTolerance * WorldLocation.ProximityTolerance;

            foreach ((VectorSectionNode section, int idx) in node.VectorSections.IndexedSelect())
            {
                if (!TryGetTrackSection(section, out TrackSection trackSection))
                    continue;

                (WorldLocation snapped, double offset) = SnapToSection(location, section, trackSection);
                double distSq = WorldLocation.GetDistanceSquared(location, snapped);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = idx;
                    bestSnapped = snapped;
                    bestOffset = offset;
                }
            }

            return bestIndex < 0
                ? null
                : new TrackTraveller(trackDataBaseType)
            {
                CurrentNode = node,
                SectionIndex = bestIndex,
                SectionOffset = bestOffset,
                Location = bestSnapped,
                Direction = direction,
            };
        }

        /// <summary>
        /// Returns a new <see cref="TrackTraveller"/> moved <paramref name="distance"/> metres along the track,
        /// crossing <see cref="VectorSectionNode"/> and <see cref="VectorNode"/> boundaries as needed.
        /// This instance is not modified.
        /// The direction of travel is taken from this traveller's <see cref="Direction"/> property;
        /// a negative <paramref name="distance"/> reverses the effective direction.
        /// At a <see cref="JunctionNode"/> boundary, the active path from <see cref="TrackWorld.SwitchStates"/> is followed.
        /// At an <see cref="EndNode"/> boundary, movement halts and any unspent distance is returned.
        /// </summary>
        /// <param name="distance">Distance in metres. A negative value reverses the effective direction of travel.</param>
        /// <returns>
        /// A new <see cref="TrackTraveller"/> at the resulting position, and the unconsumed distance in metres:
        /// <c>0</c> when the full distance was travelled, or a positive value when stopped early at an <see cref="EndNode"/>.
        /// </returns>
        public (TrackTraveller result, float unconsumed) Move(float distance)
        {
            if (!OnTrack)
                throw new InvalidOperationException("Traveller is not on a track. Call InitializeTraveller first.");

            bool forward = Direction == TrackDirection.Ahead;
            if (distance < 0)
            {
                forward = !forward;
                distance = -distance;
            }

            return MoveInternal(distance, forward);
        }

        /// <summary>
        /// Calculates the distance in metres along the track from this traveller's current position
        /// to <paramref name="other"/>'s position, travelling in the current <see cref="Direction"/>.
        /// Uses the exact node, section index, and offset of <paramref name="other"/> — no geometry snapping.
        /// Junction switch states from <see cref="TrackWorld.SwitchStates"/> are respected.
        /// </summary>
        /// <param name="other">The target traveller to measure to.</param>
        /// <param name="maxDistance">Maximum track distance to search. Defaults to <see cref="float.MaxValue"/>.</param>
        /// <returns>
        /// The track distance in metres if <paramref name="other"/> is reachable within <paramref name="maxDistance"/>
        /// in the current direction; otherwise <see langword="null"/>.
        /// </returns>
        public float? DistanceTo(TrackTraveller other, float maxDistance = float.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(other);
            return !OnTrack || !other.OnTrack ? null : DistanceToTravellerInternal(other, Direction == TrackDirection.Ahead, maxDistance);
        }

        /// <summary>
        /// Returns a new <see cref="TrackTraveller"/> positioned at the start of the next
        /// <see cref="VectorSectionNode"/> in the current direction of travel.
        /// Advances within the same <see cref="VectorNode"/> when possible; otherwise crosses the
        /// node boundary through any intervening <see cref="JunctionNode"/>, following the active
        /// switch state from <see cref="TrackWorld.SwitchStates"/>.
        /// This instance is not modified.
        /// </summary>
        /// <returns>
        /// A new <see cref="TrackTraveller"/> at the start of the next section, or
        /// <see langword="null"/> when an <see cref="EndNode"/> is reached or the topology is inconsistent.
        /// </returns>
        public TrackTraveller AdvanceToNextSection()
        {
            if (!OnTrack)
                return null;

            bool forward = Direction == TrackDirection.Ahead;

            if (forward && SectionIndex < CurrentNode.VectorSections.Length - 1)
            {
                int next = SectionIndex + 1;
                return this with { SectionIndex = next, SectionOffset = 0, Location = ComputeLocation(CurrentNode, next, 0) };
            }

            if (!forward && SectionIndex > 0)
            {
                int next = SectionIndex - 1;
                double offset = TryGetTrackSection(CurrentNode.VectorSections[next], out TrackSection ts) ? ts.Length : 0.0;
                return this with { SectionIndex = next, SectionOffset = offset, Location = ComputeLocation(CurrentNode, next, offset) };
            }

            // At a node boundary — attempt to cross it.
            VectorNode node = CurrentNode;
            int index = SectionIndex;
            double sectionOffset = forward
                ? (TryGetTrackSection(CurrentSection, out TrackSection trackSection) ? trackSection.Length : 0.0)
                : 0.0;

            double dummy = 0.0;
            bool? newForward = TryCrossNodeBoundary(TrackDataBaseType, ref node, ref index, ref sectionOffset, atEnd: forward, ref dummy);
            if (!newForward.HasValue)
                return null;

            TrackDirection newDirection = newForward.Value ? TrackDirection.Ahead : TrackDirection.Reverse;
            WorldLocation newLocation = ComputeLocation(node, index, sectionOffset);
            return this with { CurrentNode = node, SectionIndex = index, SectionOffset = sectionOffset, Location = newLocation, Direction = newDirection };
        }


        // Moves remaining metres using mutable locals; forward=true advances through VectorSections, false retreats.
        // Returns a new record at the final position and the unconsumed distance (>0 only when halted at an EndNode).
        private (TrackTraveller result, float unconsumed) MoveInternal(double remaining, bool forward)
        {
            VectorNode node = CurrentNode;
            int index = SectionIndex;
            double offset = SectionOffset;

            while (remaining > 0.0)
            {
                if (forward)
                {
                    double sectionLength = TryGetTrackSection(node.VectorSections[index], out TrackSection trackSection)
                        ? trackSection.Length : 0.0;
                    double available = sectionLength - offset;

                    if (remaining <= available)
                    {
                        offset += remaining;
                        remaining = 0.0;
                    }
                    else
                    {
                        remaining -= available;
                        if (index < node.VectorSections.Length - 1)
                        {
                            index++;
                            offset = 0.0;
                        }
                        else
                        {
                            offset = sectionLength;
                            bool? newForward = TryCrossNodeBoundary(TrackDataBaseType, ref node, ref index, ref offset, atEnd: true, ref remaining);
                            if (!newForward.HasValue)
                                break;
                            forward = newForward.Value;
                        }
                    }
                }
                else
                {
                    double available = offset;

                    if (remaining <= available)
                    {
                        offset -= remaining;
                        remaining = 0.0;
                    }
                    else
                    {
                        remaining -= available;
                        if (index > 0)
                        {
                            index--;
                            offset = TryGetTrackSection(node.VectorSections[index], out TrackSection ts)
                                ? ts.Length : 0.0;
                        }
                        else
                        {
                            offset = 0.0;
                            bool? newForward = TryCrossNodeBoundary(TrackDataBaseType, ref node, ref index, ref offset, atEnd: false, ref remaining);
                            if (!newForward.HasValue)
                                break;
                            forward = newForward.Value;
                        }
                    }
                }
            }

            WorldLocation newLocation = ComputeLocation(node, index, offset);
            TrackDirection newDirection = forward ? TrackDirection.Ahead : TrackDirection.Reverse;
            return (this with { CurrentNode = node, SectionIndex = index, SectionOffset = offset, Location = newLocation, Direction = newDirection }, (float)remaining);
        }

        // Walks sections in the given direction, matching the target traveller by node reference and section index.
        // Avoids geometry snapping; uses the other traveller's exact SectionOffset for the distance computation.
        private float? DistanceToTravellerInternal(TrackTraveller other, bool forward, float maxDistance)
        {
            VectorNode node = CurrentNode;
            int index = SectionIndex;
            double entryOffset = SectionOffset;
            double accumulated = 0.0;

            while (accumulated < maxDistance)
            {
                if (ReferenceEquals(node, other.CurrentNode) && index == other.SectionIndex)
                {
                    // Verify the target's offset is ahead of our entry point in the current direction.
                    bool ahead = forward ? other.SectionOffset >= entryOffset : other.SectionOffset <= entryOffset;
                    return !ahead ? null : (float)(accumulated + Math.Abs(other.SectionOffset - entryOffset));
                }

                // Accumulate the remaining distance in the current section and advance.
                VectorSectionNode section = node.VectorSections[index];
                if (TryGetTrackSection(section, out TrackSection trackSection))
                {
                    double sectionRemaining = forward ? trackSection.Length - entryOffset : entryOffset;
                    accumulated += sectionRemaining;
                }

                if (forward)
                {
                    if (index < node.VectorSections.Length - 1)
                    {
                        index++;
                        entryOffset = 0.0;
                    }
                    else
                    {
                        double dummy = 0.0;
                        bool? newForward = TryCrossNodeBoundary(TrackDataBaseType, ref node, ref index, ref entryOffset, atEnd: true, ref dummy);
                        if (!newForward.HasValue)
                            return null;
                        forward = newForward.Value;
                    }
                }
                else
                {
                    if (index > 0)
                    {
                        index--;
                        entryOffset = TryGetTrackSection(node.VectorSections[index], out TrackSection prevTs) ? prevTs.Length : 0.0;
                    }
                    else
                    {
                        double dummy = 0.0;
                        bool? newForward = TryCrossNodeBoundary(TrackDataBaseType, ref node, ref index, ref entryOffset, atEnd: false, ref dummy);
                        if (!newForward.HasValue)
                            return null;
                        forward = newForward.Value;
                    }
                }
            }

            return null;
        }

        private static WorldLocation ComputeLocation(VectorNode node, int sectionIndex, double sectionOffset)
        {
            VectorSectionNode section = node.VectorSections[sectionIndex];
            if (!TryGetTrackSection(section, out TrackSection ts))
                return section.Location;
            if (ts.Curved)
            {
                double arcAngle = MathHelper.ToRadians(ts.Angle);
                double clampedOffset = Math.Clamp(sectionOffset, 0.0, ts.Length);
                return WorldLocation.PointAlongArc(section.Location, section.EndLocation, arcAngle, ts.Radius, clampedOffset);
            }
            return WorldLocation.PointAlongDirection(section.Location, section.EndLocation, sectionOffset);
        }

        /// <summary>
        /// Attempts to cross the VectorNode boundary the traveller has just reached, updating
        /// <paramref name="node"/>, <paramref name="sectionIndex"/>, and <paramref name="sectionOffset"/> in place.
        /// <paramref name="atEnd"/> = <see langword="true"/> means the end-of-node (section array tail) boundary was hit;
        /// <see langword="false"/> means the start-of-node (section array head) boundary was hit.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> to continue moving forward through sections on the new node,
        /// <see langword="false"/> to continue in reverse, or <see langword="null"/> when movement must halt
        /// (an <see cref="EndNode"/> was reached or the topology is inconsistent).
        /// </returns>
        private static bool? TryCrossNodeBoundary(TrackDataBaseType dbType, ref VectorNode node, ref int sectionIndex, ref double sectionOffset, bool atEnd, ref double remaining)
        {
            TrackDatabase trackDatabase = dbType == TrackDataBaseType.Road
                ? trackWorld.TrackModel.RoadDatabase
                : trackWorld.TrackModel.TrackDatabase;

            if (trackDatabase == null)
            {
                remaining = 0.0;
                return null;
            }

            // VectorNode owns exactly two connectors: [0] = start end, [1] = finish end.
            ImmutableArray<TrackNodeConnector> ownConnectors = trackDatabase.TrackNodeConnectors[node.NodeIndex].TrackNodeConnectors;
            TrackNodeConnector exitConnector = atEnd ? ownConnectors[1] : ownConnectors[0];
            TrackNodeBase neighbor = trackDatabase.TrackNodes[exitConnector.Link];

            if (neighbor is EndNode)
            {
                // sectionOffset already pinned to the boundary by the caller.
                remaining = 0.0;
                return null;
            }

            if (neighbor is not JunctionNode junctionNode)
            {
                remaining = 0.0;
                return null;
            }

            // Find which connector of the junction links back to our current VectorNode.
            TrackNodeConnectorIndex jConns = trackDatabase.TrackNodeConnectors[junctionNode.NodeIndex];
            int incomingIdx = -1;
            for (int i = 0; i < jConns.TrackNodeConnectors.Length; i++)
            {
                if (jConns.TrackNodeConnectors[i].Link == node.NodeIndex)
                {
                    incomingIdx = i;
                    break;
                }
            }

            if (incomingIdx < 0)
            {
                remaining = 0.0;
                return null;
            }

            TrackNodeConnector outgoing;
            if (incomingIdx < jConns.InboundCount)
            {
                // Arrived from the stem → select the active branch (OutPin).
                int switchState = trackWorld.SwitchStates.TryGetValue(junctionNode.NodeIndex, out int state) ? state : 0;
                ReadOnlySpan<TrackNodeConnector> outPins = jConns.OutConnectors;
                if ((uint)switchState >= (uint)outPins.Length)
                    switchState = 0;
                outgoing = outPins[switchState];
            }
            else
            {
                // Arrived from a branch → always exit through the stem.
                ReadOnlySpan<TrackNodeConnector> inPins = jConns.InConnectors;
                if (inPins.IsEmpty)
                {
                    remaining = 0.0;
                    return null;
                }
                outgoing = inPins[0];
            }

            if (trackDatabase.TrackNodes[outgoing.Link] is not VectorNode nextNode)
            {
                remaining = 0.0;
                return null;
            }

            node = nextNode;

            if (outgoing.Direction == TrackDirection.Reverse)
            {
                // The new node's START is at this junction: enter from section 0 and move forward.
                sectionIndex = 0;
                sectionOffset = 0.0;
                return true;
            }
            else
            {
                // The new node's END is at this junction: enter from the last section and move in reverse.
                sectionIndex = nextNode.VectorSections.Length - 1;
                sectionOffset = TryGetTrackSection(nextNode.VectorSections[sectionIndex], out TrackSection ts)
                    ? ts.Length : 0.0;
                return false;
            }
        }

        private static bool TryGetTrackSection(VectorSectionNode section, out TrackSection trackSection)
        {
            if (sectionOwnership.TryGetValue(section, out (VectorNode node, int sectionIndex, TrackSection trackSection) entry))
            {
                trackSection = entry.trackSection;
                return trackSection != null;
            }
            trackSection = null;
            return false;
        }

        /// <summary>
        /// Projects <paramref name="query"/> onto the section geometry, returning the snapped
        /// <see cref="WorldLocation"/> and the offset in metres from the section start.
        /// </summary>
        private static (WorldLocation snapped, double offset) SnapToSection(in WorldLocation query, VectorSectionNode section, TrackSection trackSection)
        {
            if (trackSection.Curved)
            {
                double arcAngle = Math.PI / 180.0 * trackSection.Angle;
                return SnapToCurvedSection(section.Location, section.EndLocation, arcAngle, trackSection.Radius, query);
            }
            return SnapToStraightSection(section.Location, section.EndLocation, query);
        }

        /// <summary>
        /// Snaps <paramref name="query"/> onto the straight line segment [<paramref name="start"/>, <paramref name="end"/>].
        /// </summary>
        private static (WorldLocation snapped, double offset) SnapToStraightSection(in WorldLocation start, in WorldLocation end, in WorldLocation query)
        {
            Vector3 d = WorldLocation.GetDistanceVector(start, end);   // end − start
            Vector3 q = WorldLocation.GetDistanceVector(start, query); // query − start
            double dX = d.X, dY = d.Y, dZ = d.Z;
            double lenSq = dX * dX + dY * dY + dZ * dZ;
            double dot = (double)q.X * dX + (double)q.Y * dY + (double)q.Z * dZ;
            double t = lenSq > 0.0 ? Math.Clamp(dot / lenSq, 0.0, 1.0) : 0.0;
            double offset = t * Math.Sqrt(lenSq);
            return (WorldLocation.PointAlongDirection(start, end, offset), offset);
        }

        /// <summary>
        /// Snaps <paramref name="query"/> onto the arc defined by [<paramref name="start"/>, <paramref name="end"/>],
        /// <paramref name="arcAngle"/> and <paramref name="radius"/>.
        /// Uses the same arc basis vectors as <see cref="WorldLocation.PointAlongArc"/> to ensure consistency.
        /// </summary>
        private static (WorldLocation snapped, double offset) SnapToCurvedSection(in WorldLocation start, in WorldLocation end, double arcAngle, double radius, in WorldLocation query)
        {
            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, arcAngle, radius);

            // Arc basis: u = unit vector from center to start; v = tangent at start toward end
            // These match the internal basis of WorldLocation.PointAlongArc
            Vector3 centerToStart = WorldLocation.GetDistanceVector(center, start);
            Vector3 k = Vector3.Normalize(Vector3.Cross(centerToStart, WorldLocation.GetDistanceVector(center, end)));
            Vector3 u = Vector3.Normalize(centerToStart);
            Vector3 v = Vector3.Cross(k, u);

            Vector3 qFromCenter = WorldLocation.GetDistanceVector(center, query);
            double dotU = (double)qFromCenter.X * u.X + (double)qFromCenter.Y * u.Y + (double)qFromCenter.Z * u.Z;
            double dotV = (double)qFromCenter.X * v.X + (double)qFromCenter.Y * v.Y + (double)qFromCenter.Z * v.Z;
            double lenSq = (double)qFromCenter.X * qFromCenter.X + (double)qFromCenter.Y * qFromCenter.Y + (double)qFromCenter.Z * qFromCenter.Z;

            double angular = lenSq > 0.0
                ? Math.Clamp(Math.Atan2(dotV, dotU), 0.0, Math.Abs(arcAngle))
                : 0.0;

            double arcLengthMetres = angular * radius;
            return (WorldLocation.PointAlongArc(start, end, arcAngle, radius, arcLengthMetres), arcLengthMetres);
        }

        /// <summary>
        /// Builds a reference-equality map from every <see cref="VectorSectionNode"/> instance to its parent
        /// <see cref="VectorNode"/>, its index within <see cref="VectorNode.VectorSections"/>, and the resolved
        /// <see cref="TrackSection"/> (or <see langword="null"/> for sections with no matching geometry template).
        /// The instances in <see cref="TrackWorld.ContentByTile"/> and <see cref="VectorNode.VectorSections"/>
        /// are the same object references (both come from the same <see cref="TrackDatabase"/>), so reference
        /// equality gives an exact, unambiguous lookup.
        /// Resolving <see cref="TrackSection"/> here avoids repeated per-traversal dictionary lookups
        /// through <see cref="RuntimeDataResolver"/> on every call to <see cref="TryGetTrackSection"/>.
        /// </summary>
        private static Dictionary<VectorSectionNode, (VectorNode node, int sectionIndex, TrackSection trackSection)> BuildSectionOwnership(TrackWorld trackWorld)
        {
            Dictionary<VectorSectionNode, (VectorNode, int, TrackSection)> map
                = new Dictionary<VectorSectionNode, (VectorNode, int, TrackSection)>(ReferenceEqualityComparer.Instance);
            Models.Track.TrackModel trackModel = trackWorld.TrackModel;
            if (trackModel == null)
                return map;

            var sections = RuntimeDataResolver.Instance.TrackSections.TrackSections;

            AddDatabase(trackModel.TrackDatabase);
            AddDatabase(trackModel.RoadDatabase);
            return map;

            void AddDatabase(TrackDatabase trackDatabase)
            {
                if (trackDatabase == null)
                    return;
                foreach (VectorNode vn in trackDatabase.VectorNodes)
                    foreach ((VectorSectionNode section, int index) in vn.VectorSections.IndexedSelect())
                    {
                        _ = sections.TryGetValue(section.NodeIndex, out TrackSection ts);
                        map[section] = (vn, index, ts);
                    }
            }
        }
    }
}

