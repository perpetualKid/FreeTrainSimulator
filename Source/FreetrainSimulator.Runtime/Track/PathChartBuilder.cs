using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>Extracts placed geometry and source items from the exact resolved main traversal.</summary>
    public static class PathChartBuilder
    {
        private const double DistanceEpsilon = 0.000001;
        private const double MaximumSampleSpacing = 25;
        private const int MaximumSamples = 200000;
        private const string NoMainTraversal = "The path has no resolved main traversal.";
        private const string InvalidMainTopology = "The authored main path topology is not proven.";
        private const string StaleResolution = "The path resolution does not match the authored main path.";
        private const string UnresolvedMainSpan = "The main path contains an unresolved or ambiguous span.";
        private const string MissingGeometry = "The main path contains missing or inconsistent track geometry.";
        private const string UnprovenConnector = "The selected main path connector cannot be established.";
        private const string SampleLimit = "The path chart exceeds the supported sample limit.";
        private const string InvalidPlatform = "A platform pair cannot be established on the traversed track.";
        private const string InvalidItem = "A chart item cannot be located unambiguously on its owning track.";
        private const string MissingMilepostMetadata = "Milepost units and direction are not retained in this track model.";
        private const string BranchesExcluded = "Passing branch profiles are not included in the main path chart.";

        /// <summary>
        /// Builds a chart for the complete committed main extent, including a growing draft without an end marker.
        /// Tentative alternatives, unproven spans and disconnected geometry never produce a partial-success prefix.
        /// Only the supplied world's geometry and connector topology are consulted.
        /// </summary>
        public static PathChartProfile Build(PathModel model, PathRouteResolution resolution, TrackWorld world)
        {
            if (model == null || resolution?.MainRoute == null || world?.TrackDatabase == null || model.PathNodes.IsDefaultOrEmpty || resolution.MainRoute.Spans.IsDefaultOrEmpty)
                return Unavailable(NoMainTraversal);

            string reason = ValidateMainTopology(model, resolution);
            if (reason != null)
                return Unavailable(reason);

            List<SectionTraversal> sections = new List<SectionTraversal>();
            List<NodeTraversal> nodes = new List<NodeTraversal>();
            double length = 0;
            int directionSequence = 0;
            foreach (ResolvedPathSpan span in resolution.MainRoute.Spans)
            {
                if (model.PathNodes[span.FromNodeIndex].NodeType.Includes(PathNodeType.Reversal))
                    directionSequence++;

                reason = AppendSpan(span, resolution.AuthoredNodeAnchors, world, sections, nodes, directionSequence, ref length);

                if (reason != null)
                    return Unavailable(reason);
            }

            if (sections.Count == 0 || length <= DistanceEpsilon)
                return Unavailable(NoMainTraversal);

            if (sections.Sum(section => Math.Ceiling(Math.Abs(section.EndOffset - section.StartOffset) / SampleSpacing(section.Geometry)) + 1) > MaximumSamples)
                return Unavailable(SampleLimit);

            List<string> diagnostics = new List<string>();
            if (model.PathNodes.Any(node => node.NextSidingNode >= 0) || !resolution.PassingRoutes.IsDefaultOrEmpty)
                diagnostics.Add(BranchesExcluded);

            List<PathChartMarker> markers = new List<PathChartMarker>();
            List<PlatformOccurrence> platforms = new List<PlatformOccurrence>();
            ExtractItems(nodes, sections, world, markers, platforms, diagnostics);
            markers.Sort((first, second) => first.Distance.CompareTo(second.Distance));
            ImmutableArray<PathChartStationVisit> stations = GroupStations(platforms, sections);
            ImmutableArray<PathChartSample> samples = SampleGeometry(sections, stations, markers, out reason);
            
            return samples.IsDefault
                ? Unavailable(reason)
                : new PathChartProfile(true, string.Empty, samples, stations, markers.ToImmutableArray(), length,
                diagnostics.Distinct(StringComparer.Ordinal).ToImmutableArray());
        }

        private static PathChartProfile Unavailable(string reason)
        {
            return new PathChartProfile(false, reason, ImmutableArray<PathChartSample>.Empty,
                ImmutableArray<PathChartStationVisit>.Empty, ImmutableArray<PathChartMarker>.Empty, 0,
                ImmutableArray<string>.Empty);
        }

        private static string ValidateMainTopology(PathModel model, PathRouteResolution resolution)
        {
            ResolvedPathRoute main = resolution.MainRoute;

            if (main.BranchKind != PathRouteBranchKind.Main || main.StartNodeIndex < 0 || main.StartNodeIndex >= model.PathNodes.Length || !model.PathNodes[main.StartNodeIndex].NodeType.Includes(PathNodeType.Start))
                return InvalidMainTopology;
            if (resolution.AuthoredNodeAnchors.IsDefault || resolution.AuthoredNodeAnchors.Length != model.PathNodes.Length)
                return StaleResolution;

            HashSet<int> visited = new HashSet<int>();
            int current = main.StartNodeIndex;
            int spanIndex = 0;

            while (current >= 0 && current < model.PathNodes.Length && visited.Add(current))
            {
                PathNode node = model.PathNodes[current];
                PathRouteAnchor anchor = resolution.AuthoredNodeAnchors[current];

                if (anchor == null || anchor.AuthoredNodeIndex != current || anchor.Location != node.Location || anchor.NodeType != node.NodeType)
                    return StaleResolution;

                if (!anchor.HasTrackAnchor)
                    return MissingGeometry;

                if (resolution.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AmbiguousAnchor && diagnostic.NodeIndex == current && node.NodeIndex != anchor.TrackNodeIndex))
                    return UnresolvedMainSpan;

                int next = node.NextMainNode;
                if (next == -1)
                {
                    return spanIndex != main.Spans.Length || main.EndNodeIndex != current
                        ? StaleResolution : resolution.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.MainRouteDoesNotReachEnd)
                        ? InvalidMainTopology : null;
                }
                if (next < 0 || next >= model.PathNodes.Length || visited.Contains(next) || node.NodeType.Includes(PathNodeType.End))
                    return InvalidMainTopology;

                if (spanIndex >= main.Spans.Length)
                    return StaleResolution;

                ResolvedPathSpan span = main.Spans[spanIndex++];

                if (span.FromNodeIndex != current || span.ToNodeIndex != next)
                    return StaleResolution;

                if (span.Status != PathRouteSpanStatus.Resolved || span.Candidates.IsDefault || span.Candidates.Length != 1)
                    return UnresolvedMainSpan;
                current = next;
            }
            return InvalidMainTopology;
        }

        private static string AppendSpan(ResolvedPathSpan span, ImmutableArray<PathRouteAnchor> anchors, TrackWorld world, List<SectionTraversal> sections, List<NodeTraversal> nodes, int directionSequence, ref double distance)
        {
            ResolvedRouteCandidate candidate = span.Candidates[0];
            ImmutableArray<int> route = candidate.RouteNodeIndexes;
            ImmutableArray<TrackRouteTraversal> traversals = candidate.PhysicalTraversals;
            PathRouteAnchor from = anchors[span.FromNodeIndex];
            PathRouteAnchor to = anchors[span.ToNodeIndex];

            if (route.IsDefaultOrEmpty || traversals.IsDefault || traversals.Length != route.Length - 1 || route[0] != from.TrackNodeIndex || route[^1] != to.TrackNodeIndex)
                return UnprovenConnector;

            int incoming = -1;
            WorldLocation? previousBoundary = null;
            for (int index = 0; index < route.Length; index++)
            {
                int nodeIndex = route[index];
                
                if (nodeIndex < 0 || nodeIndex >= world.TrackDatabase.TrackNodes.Length || world.TrackDatabase.TrackNodes[nodeIndex] == null)
                    return MissingGeometry;
                
                TrackNodeBase node = world.TrackDatabase.TrackNodes[nodeIndex];
                int outgoing = -1;
                int nextIncoming = -1;

                if (index < traversals.Length)
                {
                    TrackRouteTraversal traversal = traversals[index];
                    if (traversal.FromNodeIndex != nodeIndex || traversal.ToNodeIndex != route[index + 1] || !TryReciprocalConnector(world.TrackDatabase, traversal, out nextIncoming))
                        return UnprovenConnector;
                    outgoing = traversal.FromConnectorIndex;
                }

                WorldLocation start;
                WorldLocation end;
                if (node is VectorNode vector)
                {
                    if (vector.VectorSections.IsDefaultOrEmpty || incoming > 1 || outgoing > 1)
                        return MissingGeometry;

                    int firstSectionIndex = 0;
                    int lastSectionIndex = vector.VectorSections.Length - 1;
                    
                    if (route.Length == 1)
                    {
                        firstSectionIndex = Math.Min(from.TrackVectorSectionIndex, to.TrackVectorSectionIndex);
                        lastSectionIndex = Math.Max(from.TrackVectorSectionIndex, to.TrackVectorSectionIndex);
                    }
                    else if (index == 0)
                    {
                        if (outgoing == 0)
                            lastSectionIndex = from.TrackVectorSectionIndex;
                        else
                            firstSectionIndex = from.TrackVectorSectionIndex;
                    }
                    else if (index == route.Length - 1)
                    {
                        if (incoming == 0)
                            lastSectionIndex = to.TrackVectorSectionIndex;
                        else
                            firstSectionIndex = to.TrackVectorSectionIndex;
                    }
                    
                    if (firstSectionIndex < 0 || firstSectionIndex > lastSectionIndex || lastSectionIndex >= vector.VectorSections.Length)
                        return MissingGeometry;

                    double nodeLength = 0;
                    WorldLocation? previousSectionEnd = null;
                    // Prefix lengths locate anchors, but only traversed geometry and joins constrain the chart.
                    for (int sectionIndex = 0; sectionIndex <= lastSectionIndex; sectionIndex++)
                    {
                        if (!world.SectionGeometry.TryGetValue(vector.VectorSections[sectionIndex], out SectionGeometry geometry)
                            || !ReferenceEquals(geometry.Node, vector) || !double.IsFinite(geometry.Length) || geometry.Length <= DistanceEpsilon)
                            return MissingGeometry;
                        if (sectionIndex >= firstSectionIndex)
                        {
                            if (!HasUsableGeometry(geometry) || (previousSectionEnd.HasValue
                                && WorldLocation.GetDistanceSquared(previousSectionEnd.Value, LocationOnProfileSection(geometry, 0)) > WorldLocation.ProximityTolerance))
                                return MissingGeometry;
                            previousSectionEnd = LocationOnProfileSection(geometry, geometry.Length);
                        }
                        nodeLength += geometry.Length;
                    }
                    double startPosition = index == 0 ? AnchorPosition(from, vector, world)
                        : incoming == 0 ? 0 : nodeLength;
                    double endPosition = index == route.Length - 1 ? AnchorPosition(to, vector, world)
                        : outgoing == 0 ? 0 : nodeLength;
                    
                    if (!double.IsFinite(startPosition) || !double.IsFinite(endPosition) || (index > 0 && incoming < 0) || (index < route.Length - 1 && outgoing < 0))
                        return MissingGeometry;

                    if (index > 0 && index < route.Length - 1 && incoming == outgoing)
                        return UnprovenConnector;
                    
                    start = LocationOnNode(vector, startPosition, firstSectionIndex, world);
                    end = LocationOnNode(vector, endPosition, firstSectionIndex, world);
                    
                    if (sections.Count > 0 && WorldLocation.GetDistanceSquared(LocationOnProfileSection(sections[^1].Geometry, sections[^1].EndOffset), start) > WorldLocation.ProximityTolerance)
                        return MissingGeometry;

                    AppendVector(vector, startPosition, endPosition, lastSectionIndex, nodeLength, world, sections,
                        nodes, directionSequence, ref distance);
                }
                else
                {
                    start = node.Location;
                    end = start;

                    if ((index == 0 && WorldLocation.GetDistanceSquared(from.Location, start) > WorldLocation.ProximityTolerance)
                        || (index == route.Length - 1 && WorldLocation.GetDistanceSquared(to.Location, end) > WorldLocation.ProximityTolerance))
                        return MissingGeometry;

                    if (index > 0 && index < route.Length - 1)
                    {
                        ImmutableArray<TrackNodeConnector> pins = world.TrackDatabase.TrackNodeConnectors[nodeIndex].TrackNodeConnectors;
                        if (incoming == outgoing || (node is JunctionNode && pins.Any(pin => pin.ConnectorType == ConnectorType.InPin)
                            && pins.Any(pin => pin.ConnectorType == ConnectorType.OutPin) && pins[incoming].ConnectorType == pins[outgoing].ConnectorType))
                            return UnprovenConnector;
                    }
                }

                if (previousBoundary.HasValue && WorldLocation.GetDistanceSquared(previousBoundary.Value, start) > WorldLocation.ProximityTolerance)
                    return MissingGeometry;

                previousBoundary = end;
                incoming = nextIncoming;
            }
            return null;
        }

        private static bool TryReciprocalConnector(TrackDatabase database, TrackRouteTraversal traversal, out int reciprocal)
        {
            reciprocal = -1;
            if (traversal.FromNodeIndex < 0 || traversal.ToNodeIndex < 0
                || traversal.FromNodeIndex >= database.TrackNodeConnectors.Length
                || traversal.ToNodeIndex >= database.TrackNodeConnectors.Length
                || database.TrackNodeConnectors[traversal.FromNodeIndex] == null
                || database.TrackNodeConnectors[traversal.ToNodeIndex] == null)
                return false;
            ImmutableArray<TrackNodeConnector> source = database.TrackNodeConnectors[traversal.FromNodeIndex].TrackNodeConnectors;
            ImmutableArray<TrackNodeConnector> target = database.TrackNodeConnectors[traversal.ToNodeIndex].TrackNodeConnectors;

            if (source.IsDefaultOrEmpty || target.IsDefaultOrEmpty || traversal.FromConnectorIndex < 0
                || traversal.FromConnectorIndex >= source.Length || source[traversal.FromConnectorIndex] == null
                || source[traversal.FromConnectorIndex].Link != traversal.ToNodeIndex)
                return false;

            // Match the resolver's ordinal reciprocal pairing, not the first pin with the same linked node.
            int ordinal = source.Take(traversal.FromConnectorIndex).Count(pin => pin.Link == traversal.ToNodeIndex);
            for (int index = 0; index < target.Length; index++)
            {
                if (target[index].Link != traversal.FromNodeIndex)
                    continue;
            
                if (ordinal-- == 0)
                {
                    reciprocal = index;
                    return true;
                }
            }
            return false;
        }

        private static bool HasUsableGeometry(SectionGeometry geometry)
        {
            if (!geometry.HasGeometry || !double.IsFinite(geometry.Length) || geometry.Length <= DistanceEpsilon
                || (geometry.Curved && (!double.IsFinite(geometry.Radius) || geometry.Radius <= 0
                    || !double.IsFinite(geometry.ArcAngle) || Math.Abs(geometry.ArcAngle) <= DistanceEpsilon)))
                return false;

            VectorSectionNode section = geometry.Node.VectorSections[geometry.SectionIndex];
            return IsFinite(geometry.LocationAt(0)) && IsFinite(geometry.LocationAt(geometry.Length))
                && IsFinite(LocationOnProfileSection(geometry, 0)) && IsFinite(LocationOnProfileSection(geometry, geometry.Length))
                && WorldLocation.GetDistanceSquared(section.Location, geometry.LocationAt(0)) <= WorldLocation.ProximityTolerance
                && WorldLocation.GetDistanceSquared(section.EndLocation, geometry.LocationAt(geometry.Length)) <= WorldLocation.ProximityTolerance;
        }

        private static bool IsFinite(WorldLocation location)
        {
            return float.IsFinite(location.Location.X) && float.IsFinite(location.Location.Y) && float.IsFinite(location.Location.Z);
        }

        private static double AnchorPosition(PathRouteAnchor anchor, VectorNode node, TrackWorld world)
        {
            if (anchor.TrackVectorSectionIndex < 0 || anchor.TrackVectorSectionIndex >= node.VectorSections.Length)
                return double.NaN;
            
            SectionGeometry geometry = world.SectionGeometry[node.VectorSections[anchor.TrackVectorSectionIndex]];
            double offset = geometry.DistanceOnSection(anchor.Location);
            return !double.IsFinite(offset) ? double.NaN : world.SectionOffset(node, anchor.TrackVectorSectionIndex) + Math.Clamp(offset, 0, geometry.Length);
        }

        private static WorldLocation LocationOnNode(VectorNode node, double position, int firstSectionIndex, TrackWorld world)
        {
            for (int index = 0; index < node.VectorSections.Length; index++)
            {
                SectionGeometry geometry = world.SectionGeometry[node.VectorSections[index]];

                if (index >= firstSectionIndex && (position <= geometry.Length || index == node.VectorSections.Length - 1))
                    return LocationOnProfileSection(geometry, position);
                position -= geometry.Length;
            }
            return node.Location;
        }

        private static void AppendVector(VectorNode node, double start, double end, int lastSectionIndex, double nodeLength, TrackWorld world,
            List<SectionTraversal> sections, List<NodeTraversal> nodes, int directionSequence, ref double distance)
        {
            if (Math.Abs(end - start) <= DistanceEpsilon)
                return;
            
            double initialDistance = distance;
            double lower = Math.Min(start, end);
            double upper = Math.Max(start, end);
            int direction = Math.Sign(end - start);
            IEnumerable<int> indexes = Enumerable.Range(0, lastSectionIndex + 1);
            double sectionStart = 0;

            if (direction < 0)
            {
                indexes = indexes.Reverse();
                sectionStart = nodeLength;
            }
            foreach (int index in indexes)
            {
                SectionGeometry geometry = world.SectionGeometry[node.VectorSections[index]];
            
                if (direction < 0)
                    sectionStart -= geometry.Length;
                double clippedStart = Math.Max(lower, sectionStart);
                double clippedEnd = Math.Min(upper, sectionStart + geometry.Length);
                
                if (direction > 0)
                    sectionStart += geometry.Length;
                
                if (clippedEnd - clippedStart <= DistanceEpsilon)
                    continue;
                
                double offset = direction > 0 ? sectionStart - geometry.Length : sectionStart;
                double from = (direction > 0 ? clippedStart : clippedEnd) - offset;
                double to = (direction > 0 ? clippedEnd : clippedStart) - offset;
                sections.Add(new SectionTraversal(geometry, from, to, distance));
                distance += clippedEnd - clippedStart;
            }
            if (nodes.Count > 0 && ReferenceEquals(nodes[^1].Node, node) && nodes[^1].DirectionSequence == directionSequence
                && Math.Sign(nodes[^1].End - nodes[^1].Start) == direction && Math.Abs(nodes[^1].End - start) <= DistanceEpsilon)
                nodes[^1] = nodes[^1] with { End = end };
            else
                nodes.Add(new NodeTraversal(node, start, end, initialDistance, directionSequence));
        }

        private static void ExtractItems(List<NodeTraversal> nodes, List<SectionTraversal> sections, TrackWorld world, List<PathChartMarker> markers, List<PlatformOccurrence> platforms, List<string> diagnostics)
        {
            ILookup<int, Models.Track.TrackItemBase> itemsByNode = world.TrackDatabase.TrackItems
                .Where(item => item is PlatformTrackItem or SpeedpostTrackItem or MilepostTrackItem)
                .ToLookup(item => item.NodeIndex);

            for (int traversalIndex = 0; traversalIndex < nodes.Count; traversalIndex++)
            {
                NodeTraversal traversal = nodes[traversalIndex];
                foreach (Models.Track.TrackItemBase item in itemsByNode[traversal.Node.NodeIndex])
                {
                    if (!TryItemPosition(item, traversal.Node, world, out double position, out SectionGeometry geometry, out double offset))
                    {
                        diagnostics.Add(InvalidItem);
                        continue;
                    }
                    if (item is PlatformTrackItem platform)
                    {
                        if (platform.LinkedPlatformItem < 0 || platform.LinkedPlatformItem >= world.TrackDatabase.TrackItems.Length
                            || world.TrackDatabase.TrackItems[platform.LinkedPlatformItem] is not PlatformTrackItem other
                            || other.LinkedPlatformItem != platform.TrackItemIndex || platform.TrackItemIndex == other.TrackItemIndex
                            || other.NodeIndex != platform.NodeIndex || string.IsNullOrWhiteSpace(platform.StationName)
                            || !string.Equals(platform.StationName, other.StationName, StringComparison.Ordinal)
                            || !TryItemPosition(other, traversal.Node, world, out double otherPosition, out _, out _))
                        {
                            diagnostics.Add(InvalidPlatform);
                            continue;
                        }

                        if (platform.TrackItemIndex > other.TrackItemIndex)
                            continue;
                        
                        double start = Math.Max(Math.Min(position, otherPosition), Math.Min(traversal.Start, traversal.End));
                        double end = Math.Min(Math.Max(position, otherPosition), Math.Max(traversal.Start, traversal.End));
                        
                        if (end - start <= DistanceEpsilon)
                            continue;
                        
                        double startDistance = traversal.Distance + Math.Abs((traversal.End > traversal.Start ? start : end) - traversal.Start);
                        double endDistance = startDistance + end - start;
                        double midpoint = (startDistance + endDistance) / 2;
                        PathChartPlatformEncounter encounter = new PathChartPlatformEncounter(platform.StationName, platform.PlatformName, platform.TrackItemIndex, other.TrackItemIndex, midpoint, startDistance, endDistance, LocationAt(sections, midpoint));
                        platforms.Add(new PlatformOccurrence(encounter, traversal.DirectionSequence, traversal.Node.NodeIndex, traversalIndex));
                        continue;
                    }
                    if (position < Math.Min(traversal.Start, traversal.End) - DistanceEpsilon || position > Math.Max(traversal.Start, traversal.End) + DistanceEpsilon)
                        continue;

                    if (item is MilepostTrackItem)
                    {
                        diagnostics.Add(MissingMilepostMetadata);
                        continue;
                    }
                    SpeedpostTrackItem speedpost = (SpeedpostTrackItem)item;

                    if (!speedpost.SpeedpostType.HasFlag(SpeedpostType.Limit))
                        continue;
                    
                    if (!float.IsFinite(speedpost.Angle) || !float.IsFinite(speedpost.SpeedValue) || speedpost.SpeedValue < 0)
                    {
                        diagnostics.Add(InvalidItem);
                        continue;
                    }
                    // Keep the existing source sign-facing convention, using the placed geometry tangent
                    // rather than assuming the stored section rotation still agrees with its endpoints.
                    Vector3 tangent = TangentAt(geometry, offset);
                    double relativeAngle = Math.Atan2(tangent.X, tangent.Z) + speedpost.Angle - Math.PI / 2 + (traversal.End < traversal.Start ? Math.PI : 0);
                    if (Math.Cos(relativeAngle) <= 0)
                        continue;
                    double distance = traversal.Distance + Math.Abs(position - traversal.Start);
                    markers.Add(new PathChartMarker(distance, speedpost.SpeedValue, PathChartMarkerKind.Speedpost,
                        !speedpost.SpeedpostType.HasFlag(SpeedpostType.Metric), LocationAt(sections, distance)));
                }
            }
        }

        private static bool TryItemPosition(Models.Track.TrackItemBase item, VectorNode node, TrackWorld world, out double position, out SectionGeometry selectedGeometry, out double offset)
        {
            position = double.NaN;
            selectedGeometry = null;
            offset = double.NaN;
            List<(double Position, SectionGeometry Geometry, double Offset)> matches = new List<(double, SectionGeometry, double)>();
            double sectionStart = 0;

            for (int index = 0; index < node.VectorSections.Length; index++)
            {
                SectionGeometry geometry = world.SectionGeometry[node.VectorSections[index]];
                double candidateOffset = geometry.DistanceOnSection(item.Location);
                if (double.IsFinite(candidateOffset))
                {
                    double candidatePosition = sectionStart + Math.Clamp(candidateOffset, 0, geometry.Length);
                    if (!matches.Any(match => Math.Abs(match.Position - candidatePosition) <= DistanceEpsilon))
                        matches.Add((candidatePosition, geometry, candidateOffset));
                }
                sectionStart += geometry.Length;
            }
            if (matches.Count > 1)
            {
                if (!float.IsFinite(item.SectionDistance))
                    return false;

                matches.RemoveAll(match => Math.Abs(match.Position - item.SectionDistance) > WorldLocation.ProximityTolerance);
                if (matches.Count > 1)
                {
                    // Retained along-vector distance distinguishes nearby projections at section joins.
                    double closestError = matches.Min(match => Math.Abs(match.Position - item.SectionDistance));
                    matches.RemoveAll(match => Math.Abs(match.Position - item.SectionDistance) > closestError + DistanceEpsilon);
                }
            }
            if (matches.Count != 1)
                return false;
            (position, selectedGeometry, offset) = matches[0];
            return true;
        }

        private static ImmutableArray<PathChartStationVisit> GroupStations(List<PlatformOccurrence> occurrences, List<SectionTraversal> sections)
        {
            occurrences.Sort((first, second) =>
            {
                int comparison = first.Platform.StartDistance.CompareTo(second.Platform.StartDistance);
                if (comparison == 0)
                    comparison = first.Platform.EndDistance.CompareTo(second.Platform.EndDistance);
                return comparison != 0 ? comparison
                    : first.Platform.FirstPlatformItemIndex.CompareTo(second.Platform.FirstPlatformItemIndex);
            });
            ImmutableArray<PathChartStationVisit>.Builder visits = ImmutableArray.CreateBuilder<PathChartStationVisit>();
            int index = 0;
            while (index < occurrences.Count)
            {
                PlatformOccurrence first = occurrences[index++];
                double start = first.Platform.StartDistance;
                double end = first.Platform.EndDistance;
                ImmutableArray<PathChartPlatformEncounter>.Builder platforms = ImmutableArray.CreateBuilder<PathChartPlatformEncounter>();
                platforms.Add(first.Platform);
                Dictionary<int, int> vectorOccurrences = new Dictionary<int, int> { { first.TrackNodeIndex, first.TraversalOccurrenceIndex } };
                while (index < occurrences.Count && occurrences[index].DirectionSequence == first.DirectionSequence
                    && string.Equals(occurrences[index].Platform.StationName, first.Platform.StationName, StringComparison.Ordinal)
                    && !platforms.Any(platform => platform.FirstPlatformItemIndex == occurrences[index].Platform.FirstPlatformItemIndex)
                    && (!vectorOccurrences.TryGetValue(occurrences[index].TrackNodeIndex, out int occurrenceIndex)
                        || occurrenceIndex == occurrences[index].TraversalOccurrenceIndex))
                {
                    vectorOccurrences[occurrences[index].TrackNodeIndex] = occurrences[index].TraversalOccurrenceIndex;
                    PathChartPlatformEncounter next = occurrences[index++].Platform;
                    platforms.Add(next);
                    end = Math.Max(end, next.EndDistance);
                }
                double midpoint = (start + end) / 2;
                visits.Add(new PathChartStationVisit(first.Platform.StationName, midpoint, start, end,
                    LocationAt(sections, midpoint), platforms.ToImmutable()));
            }
            return visits.ToImmutable();
        }

        private static ImmutableArray<PathChartSample> SampleGeometry(List<SectionTraversal> sections, ImmutableArray<PathChartStationVisit> stations, List<PathChartMarker> markers, out string reason)
        {
            reason = SampleLimit;
            SortedSet<double> exactDistances = new SortedSet<double>();
            foreach (PathChartMarker marker in markers)
                exactDistances.Add(marker.Distance);

            foreach (PathChartStationVisit station in stations)
            {
                exactDistances.Add(station.StartDistance);
                exactDistances.Add(station.Distance);
                exactDistances.Add(station.EndDistance);
                foreach (PathChartPlatformEncounter platform in station.Platforms)
                {
                    exactDistances.Add(platform.StartDistance);
                    exactDistances.Add(platform.Distance);
                    exactDistances.Add(platform.EndDistance);
                }
            }
            
            ImmutableArray<PathChartSample>.Builder samples = ImmutableArray.CreateBuilder<PathChartSample>();
            foreach (SectionTraversal section in sections)
            {
                double length = Math.Abs(section.EndOffset - section.StartOffset);
                double spacing = SampleSpacing(section.Geometry);
                double intervals = Math.Ceiling(length / spacing);

                if (!double.IsFinite(intervals) || intervals + samples.Count + 1 > MaximumSamples)
                    return default(ImmutableArray<PathChartSample>);
                
                SortedSet<double> distances = new SortedSet<double>();
                
                for (int index = 0; index <= (int)intervals; index++)
                    distances.Add(section.Distance + length * index / intervals);
                
                distances.UnionWith(exactDistances.GetViewBetween(section.Distance, section.Distance + length));
                
                if (samples.Count + distances.Count > MaximumSamples)
                    return default(ImmutableArray<PathChartSample>);
                
                foreach (double distance in distances)
                {
                    double offset = section.StartOffset + Math.Sign(section.EndOffset - section.StartOffset) * (distance - section.Distance);
                    WorldLocation location = LocationOnProfileSection(section.Geometry, offset);
                    Vector3 tangent = TangentAt(section.Geometry, offset) * Math.Sign(section.EndOffset - section.StartOffset);
                    double horizontal = Math.Sqrt((tangent.X * tangent.X) + (tangent.Z * tangent.Z));
                    if (!IsFinite(location) || horizontal <= DistanceEpsilon)
                    {
                        reason = MissingGeometry;
                        return default(ImmutableArray<PathChartSample>);
                    }
                    double grade = 100 * tangent.Y / horizontal;
                    double curvature = section.Geometry.Curved
                        ? Math.Sign(section.Geometry.ArcAngle) * Math.Sign(section.EndOffset - section.StartOffset) * 1000 / section.Geometry.Radius
                        : 0;
                    PathChartSample sample = new PathChartSample(distance, location.Location.Y, grade, curvature, location);
                    // Keep both one-sided values at a grade/curvature boundary; remove identical boundary copies only.
                    if (samples.Count == 0 || samples[^1] != sample)
                        samples.Add(sample);
                }
            }
            return samples.ToImmutable();
        }

        private static double SampleSpacing(SectionGeometry geometry)
        {
            return geometry.Curved ? Math.Min(MaximumSampleSpacing, geometry.Radius * Math.PI / 180)
                : MaximumSampleSpacing;
        }

        private static Vector3 TangentAt(SectionGeometry geometry, double offset)
        {
            VectorSectionNode section = geometry.Node.VectorSections[geometry.SectionIndex];
            Vector3 tangent;
            
            if (geometry.Curved)
            {
                double angle = offset / geometry.Radius;
                tangent = geometry.U * (float)-Math.Sin(angle) + geometry.V * (float)Math.Cos(angle);
            }
            else
            {
                tangent = WorldLocation.GetDistanceVector(section.Location, section.EndLocation);
            }

            if (UsesRetainedPitch(section))
                tangent.Y = (float)(-Math.Tan(section.Direction.X) * (tangent.X * Math.Sin(section.Direction.Y) + tangent.Z * Math.Cos(section.Direction.Y)));
            return tangent;
        }

        private static bool UsesRetainedPitch(VectorSectionNode section)
        {
            return section.Location.Location.Y == section.EndLocation.Location.Y && section.Direction.X != 0;
        }

        private static WorldLocation LocationOnProfileSection(SectionGeometry geometry, double offset)
        {
            WorldLocation location = geometry.LocationAt(offset);
            VectorSectionNode section = geometry.Node.VectorSections[geometry.SectionIndex];
            if (!UsesRetainedPitch(section))
                return location;

            // Import computes flat map endpoints; recover the retained pitch plane only for the chart.
            Vector3 delta = WorldLocation.GetDistanceVector(section.Location, location);
            double elevation = section.Location.Location.Y - Math.Tan(section.Direction.X) * (delta.X * Math.Sin(section.Direction.Y) + delta.Z * Math.Cos(section.Direction.Y));
            return new WorldLocation(location.Tile, new Vector3(location.Location.X, (float)elevation, location.Location.Z));
        }

        private static WorldLocation LocationAt(List<SectionTraversal> sections, double distance)
        {
            int lower = 0;
            int upper = sections.Count - 1;
            while (lower < upper)
            {
                int middle = lower + (upper - lower) / 2;
                SectionTraversal section = sections[middle];
                double length = Math.Abs(section.EndOffset - section.StartOffset);
                if (distance <= section.Distance + length + DistanceEpsilon)
                    upper = middle;
                else
                    lower = middle + 1;
            }
            SectionTraversal selected = sections[lower];
            return LocationOnProfileSection(selected.Geometry, selected.StartOffset
                + Math.Sign(selected.EndOffset - selected.StartOffset) * (distance - selected.Distance));
        }

        private sealed record SectionTraversal
        {
            /// <summary>Placed section geometry in the supplied world.</summary>
            public SectionGeometry Geometry { get; init; }
            /// <summary>Source offset in section order, in metres.</summary>
            public double StartOffset { get; init; }
            /// <summary>Target offset in section order, in metres.</summary>
            public double EndOffset { get; init; }
            /// <summary>Cumulative distance at the source.</summary>
            public double Distance { get; init; }

            public SectionTraversal(SectionGeometry geometry, double startOffset, double endOffset, double distance)
            {
                Geometry = geometry;
                StartOffset = startOffset;
                EndOffset = endOffset;
                Distance = distance;
            }
        }

        private sealed record NodeTraversal
        {
            /// <summary>Owning physical vector node.</summary>
            public VectorNode Node { get; init; }
            /// <summary>Source offset along the vector, in metres.</summary>
            public double Start { get; init; }
            /// <summary>Target offset along the vector, in metres.</summary>
            public double End { get; init; }
            /// <summary>Cumulative distance at the source.</summary>
            public double Distance { get; init; }
            /// <summary>Traversal sequence separated by explicit reversals.</summary>
            public int DirectionSequence { get; init; }

            public NodeTraversal(VectorNode node, double start, double end, double distance, int directionSequence)
            {
                Node = node;
                Start = start;
                End = end;
                Distance = distance;
                DirectionSequence = directionSequence;
            }
        }

        private readonly record struct PlatformOccurrence
        {
            /// <summary>The constituent clipped platform encounter.</summary>
            public PathChartPlatformEncounter Platform { get; init; }
            /// <summary>Traversal sequence separated by reversals.</summary>
            public int DirectionSequence { get; init; }
            /// <summary>Vector identity used to distinguish physical return visits.</summary>
            public int TrackNodeIndex { get; init; }
            /// <summary>Physical vector traversal occurrence, shared only by uninterrupted merged fragments.</summary>
            public int TraversalOccurrenceIndex { get; init; }

            public PlatformOccurrence(PathChartPlatformEncounter platform, int directionSequence, int trackNodeIndex, int traversalOccurrenceIndex)
            {
                Platform = platform;
                DirectionSequence = directionSequence;
                TrackNodeIndex = trackNodeIndex;
                TraversalOccurrenceIndex = traversalOccurrenceIndex;
            }
        }
    }
}