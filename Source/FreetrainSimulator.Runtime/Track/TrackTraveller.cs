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
    /// </summary>
    public class TrackTraveller
    {
        private static TrackWorld trackWorld;

        // Reference-equality map: VectorSectionNode instance → parent VectorNode and index within VectorSections
        private static Dictionary<VectorSectionNode, (VectorNode node, int sectionIndex)> sectionOwnership
            = new Dictionary<VectorSectionNode, (VectorNode, int)>(ReferenceEqualityComparer.Instance);
        private int sectionIndex;

        /// <summary>The <see cref="VectorNode"/> (track node) the traveller is currently on, or <see langword="null"/> if not on track.</summary>
        public VectorNode CurrentNode { get; private set; }

        /// <summary>The individual <see cref="VectorSectionNode"/> within <see cref="CurrentNode"/> that the traveller occupies,
        /// or <see langword="null"/> if not on track.</summary>
        public VectorSectionNode CurrentSection => OnTrack ? CurrentNode.VectorSections[sectionIndex] : null;

        /// <summary>The track-node index of the current <see cref="CurrentNode"/>, or <c>-1</c> if not on track.</summary>
        public int TrackNodeIndex => CurrentNode?.NodeIndex ?? -1;

        /// <summary>The current world location of the traveller on the track.</summary>
        public WorldLocation Location { get; private set; }

        /// <summary><see langword="true"/> when the traveller is positioned on a track section.</summary>
        public bool OnTrack => CurrentNode != null;

        /// <summary>The kind of track database (<see cref="TrackDataBaseType.Rail"/> or <see cref="TrackDataBaseType.Road"/>) this traveller operates on.</summary>
        public TrackDataBaseType TrackDataBaseType { get; }
        
        public double SectionOffset { get; private set; }

        /// <summary>
        /// Builds (or rebuilds) the shared ownership map from every <see cref="VectorSectionNode"/> instance
        /// to its parent <see cref="VectorNode"/> and section index, covering both the rail
        /// (<see cref="TrackModel.TrackDatabase"/>) and road (<see cref="TrackModel.RoadDatabase"/>) databases.
        /// Must be called once after <see cref="TrackWorld.Initialize"/> before any <see cref="TrackTraveller"/> is used.
        /// </summary>
        public static void Initialize(TrackWorld trackWorld)
        {
            ArgumentNullException.ThrowIfNull(trackWorld);
            TrackTraveller.trackWorld = trackWorld;
            sectionOwnership = BuildSectionOwnership(trackWorld);
        }

        /// <summary>
        /// Creates a new <see cref="TrackTraveller"/>.
        /// </summary>
        /// <param name="trackDataBaseType">Whether the traveller operates on rail (<see cref="TrackDataBaseType.Rail"/>, default)
        /// or road (<see cref="TrackDataBaseType.Road"/>) geometry.</param>
        public TrackTraveller(TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
        {
            TrackDataBaseType = trackDataBaseType;
        }

        /// <summary>
        /// Places the traveller on the nearest track section within <see cref="ProximityTolerance"/> metres
        /// of <paramref name="location"/>, searching the database that matches <see cref="TrackDataBaseType"/>.
        /// When a section is found, <see cref="Location"/> is snapped to the projected point on that section's geometry.
        /// </summary>
        /// <returns><see langword="true"/> if a section was found and the traveller was placed; <see langword="false"/> otherwise.</returns>
        public bool PlaceOnTrack(in WorldLocation location)
        {
            VectorSectionNode bestSection = null;
            WorldLocation bestSnapped = WorldLocation.None;
            double bestOffset = 0.0;
            double bestDistSq = WorldLocation.ProximityTolerance * WorldLocation.ProximityTolerance;

            MapContentType contentType = TrackDataBaseType == TrackDataBaseType.Road ? MapContentType.Roads : MapContentType.Tracks;
            ITileIndexedList<ITileCoordinate> bucket = trackWorld.ContentByTile[contentType];
            if (bucket == null)
                return false;

            int tileRadius = WorldLocation.IsNearTileBoundary(location) ? 1 : 0;
            foreach (VectorSectionNode section in bucket.BoundingBox(location.Tile, tileRadius).Cast<VectorSectionNode>())
            {
                if (!TryGetTrackSection(section, out TrackSection ts))
                    continue;

                (WorldLocation snapped, double offset) = SnapToSection(location, section, ts);
                double distSq = WorldLocation.GetDistanceSquared(location, snapped);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestSection = section;
                    bestSnapped = snapped;
                    bestOffset = offset;
                }
            }

            if (bestSection == null || !sectionOwnership.TryGetValue(bestSection, out (VectorNode node, int idx) owner))
            {
                CurrentNode = null;
                return false;
            }

            CurrentNode = owner.node;
            sectionIndex = owner.idx;
            SectionOffset = bestOffset;
            Location = bestSnapped;
            return true;
        }

        /// <summary>
        /// Moves the traveller <paramref name="distance"/> metres along the track in <paramref name="direction"/>,
        /// crossing <see cref="VectorSectionNode"/> and <see cref="VectorNode"/> boundaries as needed.
        /// At a <see cref="JunctionNode"/> boundary, the active path from <see cref="TrackWorld.SwitchStates"/> is followed.
        /// At an <see cref="EndNode"/> boundary, movement halts and any unspent distance is returned.
        /// </summary>
        /// <param name="distance">Distance in metres. A negative value reverses the effective direction of travel.</param>
        /// <param name="direction">Direction of travel along the track. A negative <paramref name="distance"/> flips this direction.</param>
        /// <returns>
        /// The unconsumed distance in metres: <c>0</c> when the full distance was travelled,
        /// or a positive value when movement was stopped early at an <see cref="EndNode"/>.
        /// </returns>
        public float Move(float distance, TrackDirection direction = TrackDirection.Ahead)
        {
            if (!OnTrack)
                throw new InvalidOperationException("Traveller is not on a track. Call TrySnapToTrack first.");

            if (distance < 0)
            {
                direction = direction == TrackDirection.Ahead ? TrackDirection.Reverse : TrackDirection.Ahead;
                distance = -distance;
            }

            return (float)MoveInternal(distance, forward: direction == TrackDirection.Ahead);
        }

        // Moves remaining metres; forward=true advances through VectorSections, false retreats.
        // Returns the unconsumed distance (>0 only when halted at an EndNode).
        private double MoveInternal(double remaining, bool forward)
        {
            while (remaining > 0.0)
            {
                if (forward)
                {
                    double sectionLength = TryGetTrackSection(CurrentNode.VectorSections[sectionIndex], out TrackSection ts)
                        ? ts.Length : 0.0;
                    double available = sectionLength - SectionOffset;

                    if (remaining <= available)
                    {
                        SectionOffset += remaining;
                        remaining = 0.0;
                    }
                    else
                    {
                        remaining -= available;
                        if (sectionIndex < CurrentNode.VectorSections.Length - 1)
                        {
                            sectionIndex++;
                            SectionOffset = 0.0;
                        }
                        else
                        {
                            SectionOffset = sectionLength;
                            bool? newForward = TryCrossNodeBoundary(atEnd: true, ref remaining);
                            if (!newForward.HasValue)
                                break;
                            forward = newForward.Value;
                        }
                    }
                }
                else
                {
                    double available = SectionOffset;

                    if (remaining <= available)
                    {
                        SectionOffset -= remaining;
                        remaining = 0.0;
                    }
                    else
                    {
                        remaining -= available;
                        if (sectionIndex > 0)
                        {
                            sectionIndex--;
                            SectionOffset = TryGetTrackSection(CurrentNode.VectorSections[sectionIndex], out TrackSection ts)
                                ? ts.Length : 0.0;
                        }
                        else
                        {
                            SectionOffset = 0.0;
                            bool? newForward = TryCrossNodeBoundary(atEnd: false, ref remaining);
                            if (!newForward.HasValue)
                                break;
                            forward = newForward.Value;
                        }
                    }
                }
            }
            UpdateLocation();
            return remaining;
        }

        private void UpdateLocation()
        {
            VectorSectionNode section = CurrentNode.VectorSections[sectionIndex];
            Location = TryGetTrackSection(section, out TrackSection ts)
                ? LocationAtOffset(section, ts, SectionOffset)
                : section.Location;
        }

        /// <summary>
        /// Attempts to cross the VectorNode boundary the traveller has just reached.
        /// <paramref name="atEnd"/> = <see langword="true"/> means the end-of-node (section array tail) boundary was hit;
        /// <see langword="false"/> means the start-of-node (section array head) boundary was hit.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> to continue moving forward through sections on the new node,
        /// <see langword="false"/> to continue in reverse, or <see langword="null"/> when movement must halt
        /// (an <see cref="EndNode"/> was reached or the topology is inconsistent).
        /// </returns>
        private bool? TryCrossNodeBoundary(bool atEnd, ref double remaining)
        {
            TrackDatabase db = TrackDataBaseType == TrackDataBaseType.Road
                ? trackWorld.TrackModel.RoadDatabase
                : trackWorld.TrackModel.TrackDatabase;

            if (db == null)
            {
                remaining = 0.0;
                return null;
            }

            // VectorNode owns exactly two connectors: [0] = start end, [1] = finish end.
            ImmutableArray<TrackNodeConnector> ownConnectors = db.TrackNodeConnectors[CurrentNode.NodeIndex].TrackNodeConnectors;
            TrackNodeConnector exitConnector = atEnd ? ownConnectors[1] : ownConnectors[0];
            TrackNodeBase neighbor = db.TrackNodes[exitConnector.Link];

            if (neighbor is EndNode)
            {
                // SectionOffset already pinned to the boundary by the caller.
                remaining = 0.0;
                return null;
            }

            if (neighbor is not JunctionNode junctionNode)
            {
                remaining = 0.0;
                return null;
            }

            // Find which connector of the junction links back to our current VectorNode.
            TrackNodeConnectorIndex jConns = db.TrackNodeConnectors[junctionNode.NodeIndex];
            int incomingIdx = -1;
            for (int i = 0; i < jConns.TrackNodeConnectors.Length; i++)
            {
                if (jConns.TrackNodeConnectors[i].Link == CurrentNode.NodeIndex)
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

            if (db.TrackNodes[outgoing.Link] is not VectorNode nextNode)
            {
                remaining = 0.0;
                return null;
            }

            CurrentNode = nextNode;

            if (outgoing.Direction == TrackDirection.Reverse)
            {
                // The new node's START is at this junction: enter from section 0 and move forward.
                sectionIndex = 0;
                SectionOffset = 0.0;
                return true;
            }
            else
            {
                // The new node's END is at this junction: enter from the last section and move in reverse.
                sectionIndex = nextNode.VectorSections.Length - 1;
                SectionOffset = TryGetTrackSection(nextNode.VectorSections[sectionIndex], out TrackSection ts)
                    ? ts.Length : 0.0;
                return false;
            }
        }

        private static bool TryGetTrackSection(VectorSectionNode section, out TrackSection trackSection)
        {
            return RuntimeDataResolver.Instance.TrackSections.TrackSections.TryGetValue(section.NodeIndex, out trackSection);
        }

        /// <summary>
        /// Returns the <see cref="WorldLocation"/> at <paramref name="offset"/> metres from the start of
        /// <paramref name="section"/>, following the section's straight or curved geometry.
        /// </summary>
        private static WorldLocation LocationAtOffset(VectorSectionNode section, TrackSection ts, double offset)
        {
            if (ts.Curved)
            {
                double arcAngle = Math.PI / 180.0 * ts.Angle;
                double angular = ts.Radius > 0.0
                    ? Math.Clamp(offset / ts.Radius, 0.0, Math.Abs(arcAngle))
                    : 0.0;
                return WorldLocation.PointAlongArc(section.Location, section.EndLocation, (float)arcAngle, ts.Radius, (float)angular);
            }
            return WorldLocation.InterpolateAlong(section.Location, section.EndLocation, (float)offset);
        }

        /// <summary>
        /// Projects <paramref name="query"/> onto the section geometry, returning the snapped
        /// <see cref="WorldLocation"/> and the offset in metres from the section start.
        /// </summary>
        private static (WorldLocation snapped, double offset) SnapToSection(in WorldLocation query, VectorSectionNode section, TrackSection ts)
        {
            if (ts.Curved)
            {
                double arcAngle = Math.PI / 180.0 * ts.Angle;
                return SnapToCurvedSection(section.Location, section.EndLocation, arcAngle, ts.Radius, query);
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
            return (WorldLocation.InterpolateAlong(start, end, (float)offset), offset);
        }

        /// <summary>
        /// Snaps <paramref name="query"/> onto the arc defined by [<paramref name="start"/>, <paramref name="end"/>],
        /// <paramref name="arcAngle"/> and <paramref name="radius"/>.
        /// Uses the same arc basis vectors as <see cref="WorldLocation.PointAlongArc"/> to ensure consistency.
        /// </summary>
        private static (WorldLocation snapped, double offset) SnapToCurvedSection(in WorldLocation start, in WorldLocation end, double arcAngle, double radius, in WorldLocation query)
        {
            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, (float)arcAngle, (float)radius);

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

            return (WorldLocation.PointAlongArc(start, end, (float)arcAngle, (float)radius, (float)angular), angular * radius);
        }

        /// <summary>
        /// Builds a reference-equality map from every <see cref="VectorSectionNode"/> instance to its parent
        /// <see cref="VectorNode"/> and its index within <see cref="VectorNode.VectorSections"/>.
        /// The instances in <see cref="TrackWorld.ContentByTile"/> and <see cref="VectorNode.VectorSections"/>
        /// are the same object references (both come from the same <see cref="TrackDatabase"/>), so reference
        /// equality gives an exact, unambiguous lookup.
        /// </summary>
        private static Dictionary<VectorSectionNode, (VectorNode, int)> BuildSectionOwnership(TrackWorld trackWorld)
        {
            Dictionary<VectorSectionNode, (VectorNode, int)> map = new Dictionary<VectorSectionNode, (VectorNode, int)>(ReferenceEqualityComparer.Instance);
            Models.Track.TrackModel trackModel = trackWorld.TrackModel;
            if (trackModel == null)
                return map;

            AddDatabase(trackModel.TrackDatabase);
            AddDatabase(trackModel.RoadDatabase);
            return map;

            void AddDatabase(TrackDatabase db)
            {
                if (db == null)
                    return;
                foreach (VectorNode vn in db.VectorNodes)
                    foreach ((VectorSectionNode section, int index) in vn.VectorSections.IndexedSelect())
                        map[section] = (vn, index);
            }
        }
    }
}

