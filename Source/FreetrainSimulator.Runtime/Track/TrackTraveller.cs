using System;
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
    public readonly record struct TrackTraveller
    {
        private static TrackWorld trackWorld;

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
        }

        /// <summary>
        /// Creates a new <see cref="TrackTraveller"/> not yet placed on any track.
        /// </summary>
        /// <param name="trackDataBaseType">Whether the traveller operates on rail (<see cref="TrackDataBaseType.Rail"/>, default)
        /// or road (<see cref="TrackDataBaseType.Road"/>) geometry.</param>
        private TrackTraveller(TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail) : this()
        {
            TrackDataBaseType = trackDataBaseType;
        }

        // Equals and GetHashCode are overridden to include SectionIndex (private, excluded from synthesised record equality)
        // and to use reference equality for CurrentNode (correct for database object identity).
        public readonly bool Equals(TrackTraveller other)
        {
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
        public static TrackTraveller? InitializeTraveller(in WorldLocation location, TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
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
        public static TrackTraveller? InitializeTraveller(in WorldLocation location, TrackDirection direction, TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
        {
            SectionGeometry bestGeometry = null;
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
                if (!trackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry sectionGeometry) || !sectionGeometry.HasGeometry)
                    continue;

                (WorldLocation snapped, double offset) = SnapToSection(location, section, sectionGeometry);
                double distSq = WorldLocation.GetDistanceSquared(location, snapped);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestGeometry = sectionGeometry;
                    bestSnapped = snapped;
                    bestOffset = offset;
                }
            }

            return bestGeometry == null
                ? null
                : new TrackTraveller(trackDataBaseType)
            {
                CurrentNode = bestGeometry.Node,
                SectionIndex = bestGeometry.SectionIndex,
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
        public static TrackTraveller? InitializeTraveller(in WorldLocation location, VectorNode node, TrackDirection direction = TrackDirection.Ahead, TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
        {
            ArgumentNullException.ThrowIfNull(node);

            int bestIndex = -1;
            WorldLocation bestSnapped = WorldLocation.None;
            double bestOffset = 0.0;
            double bestDistSq = WorldLocation.ProximityTolerance * WorldLocation.ProximityTolerance;

            foreach ((VectorSectionNode section, int idx) in node.VectorSections.IndexedSelect())
            {
                if (!trackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry sectionGeometry) || !sectionGeometry.HasGeometry)
                    continue;

                (WorldLocation snapped, double offset) = SnapToSection(location, section, sectionGeometry);
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
        /// At an <see cref="EndNode"/> boundary, movement halts at the end-of-track position.
        /// </summary>
        /// <param name="distance">Distance in metres. A negative value reverses the effective direction of travel.</param>
        /// <returns>A new <see cref="TrackTraveller"/> at the resulting position.</returns>
        public TrackTraveller Move(float distance)
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
        public float? DistanceTo(in TrackTraveller other, float maxDistance = float.MaxValue)
        {
            return !OnTrack || !other.OnTrack || other.TrackDataBaseType != TrackDataBaseType
                ? null
                : DistanceToTravellerInternal(other, Direction == TrackDirection.Ahead, maxDistance);
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
        public TrackTraveller? AdvanceToNextSection()
        {
            if (!OnTrack)
                return null;

            bool forward = Direction == TrackDirection.Ahead;

            if (forward && SectionIndex < CurrentNode.VectorSections.Length - 1)
            {
                int next = SectionIndex + 1;
                return this with { SectionIndex = next, SectionOffset = 0, Location = trackWorld.ComputeSectionLocation(CurrentNode, next, 0) };
            }

            if (!forward && SectionIndex > 0)
            {
                int next = SectionIndex - 1;
                double offset = trackWorld.SectionLength(CurrentNode, next);
                return this with { SectionIndex = next, SectionOffset = offset, Location = trackWorld.ComputeSectionLocation(CurrentNode, next, offset) };
            }

            // At a node boundary — attempt to cross it.
            TrackDatabase trackDatabase = TrackDataBaseType == TrackDataBaseType.Road
                ? trackWorld.TrackModel.RoadDatabase
                : trackWorld.TrackModel.TrackDatabase;

            VectorNode node = CurrentNode;
            int index = SectionIndex;
            double sectionOffset = 0.0;

            bool? newForward = TryCrossNodeBoundary(trackDatabase, ref node, ref index, ref sectionOffset, atEnd: forward);
            if (!newForward.HasValue)
                return null;

            TrackDirection newDirection = newForward.Value ? TrackDirection.Ahead : TrackDirection.Reverse;
            WorldLocation newLocation = trackWorld.ComputeSectionLocation(node, index, sectionOffset);
            return this with { CurrentNode = node, SectionIndex = index, SectionOffset = sectionOffset, Location = newLocation, Direction = newDirection };
        }

        /// <summary>
        /// Walks in the current direction of travel until the first <see cref="JunctionNode"/> boundary is reached,
        /// returning the junction and the <see cref="VectorNode"/> that immediately precedes it.
        /// Sections within the same <see cref="VectorNode"/> are traversed freely;
        /// this method stops at the junction rather than crossing it.
        /// </summary>
        /// <returns>
        /// A tuple of the found <see cref="JunctionNode"/> and its immediately preceding <see cref="VectorNode"/>,
        /// or <see langword="null"/> when an <see cref="EndNode"/> is reached before any junction.
        /// </returns>
        public (JunctionNode Junction, VectorNode ApproachNode)? NextJunction()
        {
            if (!OnTrack)
                return null;

            TrackDatabase trackDatabase = TrackDataBaseType == TrackDataBaseType.Road
                ? trackWorld.TrackModel.RoadDatabase
                : trackWorld.TrackModel.TrackDatabase;

            if (trackDatabase == null)
                return null;

            TrackTraveller current = this;

            while (true)
            {
                bool forward = current.Direction == TrackDirection.Ahead;
                bool atNodeBoundary = forward
                    ? current.SectionIndex == current.CurrentNode.VectorSections.Length - 1
                    : current.SectionIndex == 0;

                if (atNodeBoundary)
                {
                    TrackNodeBase neighbor = trackDatabase.TrackNodes[
                        trackDatabase.TrackNodeConnectors[current.CurrentNode.NodeIndex].TrackNodeConnectors[forward ? 1 : 0].Link];
                    if (neighbor is JunctionNode junctionNode)
                        return (junctionNode, current.CurrentNode);
                    if (neighbor is EndNode)
                        return null;
                }

                TrackTraveller? next = current.AdvanceToNextSection();
                if (!next.HasValue)
                    return null;

                current = next.Value;
            }
        }

        /// <summary>
        /// Tests whether the traveller is about to cross a <see cref="VectorNode"/> boundary in its direction of travel
        /// into a <see cref="JunctionNode"/> that is a trailing switch whose active route is not the current approach branch.
        /// A trailing switch is one where this traveller arrives from a branch connector (out-pin), not the stem (in-pin).
        /// If the branch index does not match <see cref="TrackWorld.SwitchStates"/> for that junction, the switch is set against.
        /// </summary>
        /// <param name="misalignedJunction">
        /// When the method returns <see langword="true"/>, the <see cref="JunctionNode"/> whose switch state
        /// does not align with the current approach branch; otherwise <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the traveller is at the last section boundary in its direction of travel,
        /// the boundary connects to a <see cref="JunctionNode"/>, and the approach branch index does not match
        /// the active switch route; otherwise <see langword="false"/>.
        /// </returns>
        public bool IsTrailingMisalignedSwitch(out JunctionNode misalignedJunction)
        {
            misalignedJunction = null;
            if (!OnTrack)
                return false;

            bool forward = Direction == TrackDirection.Ahead;
            bool atNodeBoundary = forward
                ? SectionIndex == CurrentNode.VectorSections.Length - 1
                : SectionIndex == 0;

            if (!atNodeBoundary)
                return false;

            TrackDatabase trackDatabase = TrackDataBaseType == TrackDataBaseType.Road
                ? trackWorld.TrackModel.RoadDatabase
                : trackWorld.TrackModel.TrackDatabase;

            if (trackDatabase == null)
                return false;

            // Connector[0] = start end, Connector[1] = finish end of a VectorNode.
            ImmutableArray<TrackNodeConnector> ownConnectors = trackDatabase.TrackNodeConnectors[CurrentNode.NodeIndex].TrackNodeConnectors;
            TrackNodeConnector exitConnector = forward ? ownConnectors[1] : ownConnectors[0];

            if (trackDatabase.TrackNodes[exitConnector.Link] is not JunctionNode junctionNode)
                return false;

            TrackNodeConnectorIndex jConns = trackDatabase.TrackNodeConnectors[junctionNode.NodeIndex];
            int incomingIdx = -1;
            for (int i = 0; i < jConns.TrackNodeConnectors.Length; i++)
            {
                if (jConns.TrackNodeConnectors[i].Link == CurrentNode.NodeIndex)
                {
                    incomingIdx = i;
                    break;
                }
            }

            // Stem approach (facing switch): the switch state selects the outgoing branch — no misalignment possible.
            if (incomingIdx < 0 || incomingIdx < jConns.InboundCount)
                return false;

            // Trailing switch: check whether this branch is the active switch route.
            int branchIndex = incomingIdx - jConns.InboundCount;
            int switchState = trackWorld.SwitchStates.TryGetValue(junctionNode.NodeIndex, out int state) ? state : 0;
            if (branchIndex == switchState)
                return false;

            misalignedJunction = junctionNode;
            return true;
        }

        // Moves remaining metres using mutable locals; forward=true advances through VectorSections, false retreats.
        // Halts silently at an EndNode; the caller receives the pinned boundary position.
        private TrackTraveller MoveInternal(double remaining, bool forward)
        {
            // Resolve once — TryCrossNodeBoundary is called on every node boundary crossing.
            TrackDatabase trackDatabase = TrackDataBaseType == TrackDataBaseType.Road
                ? trackWorld.TrackModel.RoadDatabase
                : trackWorld.TrackModel.TrackDatabase;

            VectorNode node = CurrentNode;
            int index = SectionIndex;
            double offset = SectionOffset;

            while (remaining > 0.0)
            {
                if (forward)
                {
                    double sectionLength = trackWorld.SectionLength(node, index);
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
                            bool? newForward = TryCrossNodeBoundary(trackDatabase, ref node, ref index, ref offset, atEnd: true);
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
                            offset = trackWorld.SectionLength(node, index);
                        }
                        else
                        {
                            offset = 0.0;
                            bool? newForward = TryCrossNodeBoundary(trackDatabase, ref node, ref index, ref offset, atEnd: false);
                            if (!newForward.HasValue)
                                break;
                            forward = newForward.Value;
                        }
                    }
                }
            }

            WorldLocation newLocation = trackWorld.ComputeSectionLocation(node, index, offset);
            TrackDirection newDirection = forward ? TrackDirection.Ahead : TrackDirection.Reverse;
            return this with { CurrentNode = node, SectionIndex = index, SectionOffset = offset, Location = newLocation, Direction = newDirection };
        }

        // Walks sections in the given direction, matching the target traveller by node reference and section index.
        // Avoids geometry snapping; uses the other traveller's exact SectionOffset for the distance computation.
        private float? DistanceToTravellerInternal(in TrackTraveller other, bool forward, float maxDistance)
        {
            // Resolve once — TryCrossNodeBoundary is called on every node boundary crossing.
            TrackDatabase trackDatabase = TrackDataBaseType == TrackDataBaseType.Road
                ? trackWorld.TrackModel.RoadDatabase
                : trackWorld.TrackModel.TrackDatabase;

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
                    if (!ahead)
                        return null;
                    float distance = (float)(accumulated + Math.Abs(other.SectionOffset - entryOffset));
                    return distance <= maxDistance ? distance : null;
                }

                // Accumulate the remaining distance in the current section and advance.
                double sectionLength = trackWorld.SectionLength(node, index);
                if (sectionLength > 0.0)
                    accumulated += forward ? sectionLength - entryOffset : entryOffset;

                if (forward)
                {
                    if (index < node.VectorSections.Length - 1)
                    {
                        index++;
                        entryOffset = 0.0;
                    }
                    else
                    {
                        bool? newForward = TryCrossNodeBoundary(trackDatabase, ref node, ref index, ref entryOffset, atEnd: true);
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
                        entryOffset = trackWorld.SectionLength(node, index);
                    }
                    else
                    {
                        bool? newForward = TryCrossNodeBoundary(trackDatabase, ref node, ref index, ref entryOffset, atEnd: false);
                        if (!newForward.HasValue)
                            return null;
                        forward = newForward.Value;
                    }
                }
            }

            return null;
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
        private static bool? TryCrossNodeBoundary(TrackDatabase trackDatabase, ref VectorNode node, ref int sectionIndex, ref double sectionOffset, bool atEnd)
        {
            if (trackDatabase == null)
                return null;

            // VectorNode owns exactly two connectors: [0] = start end, [1] = finish end.
            ImmutableArray<TrackNodeConnector> ownConnectors = trackDatabase.TrackNodeConnectors[node.NodeIndex].TrackNodeConnectors;
            TrackNodeConnector exitConnector = atEnd ? ownConnectors[1] : ownConnectors[0];
            TrackNodeBase neighbor = trackDatabase.TrackNodes[exitConnector.Link];

            if (neighbor is EndNode)
                return null;    // sectionOffset already pinned to the boundary by the caller; remaining is preserved for the caller to return as unconsumed.

            if (neighbor is not JunctionNode junctionNode)
                return null;

            // Find which connector of the junction links back to our current VectorNode.
            TrackNodeConnectorIndex junctionConnectors = trackDatabase.TrackNodeConnectors[junctionNode.NodeIndex];
            int incomingIdx = -1;
            for (int i = 0; i < junctionConnectors.TrackNodeConnectors.Length; i++)
            {
                if (junctionConnectors.TrackNodeConnectors[i].Link == node.NodeIndex)
                {
                    incomingIdx = i;
                    break;
                }
            }

            if (incomingIdx < 0)
                return null;

            TrackNodeConnector outgoing;
            if (incomingIdx < junctionConnectors.InboundCount)
            {
                // Arrived from the stem → select the active branch (OutPin).
                int switchState = trackWorld.SwitchStates.TryGetValue(junctionNode.NodeIndex, out int state) ? state : 0;
                ReadOnlySpan<TrackNodeConnector> outPins = junctionConnectors.OutConnectors;
                if ((uint)switchState >= (uint)outPins.Length)
                    switchState = 0;
                outgoing = outPins[switchState];
            }
            else
            {
                // Arrived from a branch → always exit through the stem.
                ReadOnlySpan<TrackNodeConnector> inPins = junctionConnectors.InConnectors;
                if (inPins.IsEmpty)
                    return null;
                outgoing = inPins[0];
            }

            if (trackDatabase.TrackNodes[outgoing.Link] is not VectorNode nextNode)
                return null;

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
                sectionOffset = trackWorld.SectionLength(nextNode, sectionIndex);
                return false;
            }
        }

        /// <summary>
        /// Projects <paramref name="query"/> onto the section geometry
        /// <see cref="WorldLocation"/> and the offset in metres from the section start.
        /// </summary>
        private static (WorldLocation snapped, double offset) SnapToSection(in WorldLocation query, VectorSectionNode section, SectionGeometry sectionGeometry)
        {
            return sectionGeometry.Curved
                ? SnapToCurvedSection(section, sectionGeometry, query)
                : SnapToStraightSection(section.Location, section.EndLocation, query);
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
            if (lenSq <= 0.0)
                return (start, 0.0);
            double dot = (double)q.X * dX + (double)q.Y * dY + (double)q.Z * dZ;
            if (dot <= 0.0)
                return (start, 0.0);
            if (dot >= lenSq)
                return (end, Math.Sqrt(lenSq));
            double offset = dot / Math.Sqrt(lenSq);
            return (WorldLocation.PointAlongDirection(start, end, offset), offset);
        }

        private static (WorldLocation snapped, double offset) SnapToCurvedSection(VectorSectionNode section, SectionGeometry geom, in WorldLocation query)
        {
            Vector3 qFromCenter = WorldLocation.GetDistanceVector(geom.ArcCenter, query);
            double dotU = (double)qFromCenter.X * geom.U.X + (double)qFromCenter.Y * geom.U.Y + (double)qFromCenter.Z * geom.U.Z;
            double dotV = (double)qFromCenter.X * geom.V.X + (double)qFromCenter.Y * geom.V.Y + (double)qFromCenter.Z * geom.V.Z;
            double angular = Math.Clamp(Math.Atan2(dotV, dotU), 0.0, Math.Abs(geom.ArcAngle));
            double arcLengthMetres = angular * geom.Radius;
            return (WorldLocation.PointAlongArc(section.Location, section.EndLocation, geom.ArcAngle, geom.Radius, arcLengthMetres), arcLengthMetres);
        }

            }
        }

