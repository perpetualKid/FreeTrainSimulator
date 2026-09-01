using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>Derives path endpoint metadata from station platforms traversed by the resolved main route.</summary>
    public static class PathEndpointNameResolver
    {
        /// <summary>Maximum along-route distance used when selecting a station name.</summary>
        public const double DefaultMaximumDistance = 1000;

        /// <summary>Returns the closest station name in the path direction, or the endpoint fallback.</summary>
        public static string Resolve(PathModel pathModel, PathRouteResolution resolution, TrackWorld trackWorld, bool startEndpoint, double maximumDistance)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDistance);

            string fallback = startEndpoint ? "Start" : "End";
            if (resolution?.MainRoute == null || trackWorld?.TrackDatabase == null)
                return fallback;

            ImmutableArray<int> routeNodes = BuildRouteNodes(resolution.MainRoute.Spans);
            if (routeNodes.IsDefaultOrEmpty)
                return fallback;
            if (!startEndpoint)
                routeNodes = routeNodes.Reverse().ToImmutableArray();

            int endpointIndex = FindEndpointIndex(pathModel.PathNodes, startEndpoint ? PathNodeType.Start : PathNodeType.End);
            if (endpointIndex < 0 || endpointIndex >= resolution.AuthoredNodeAnchors.Length)
                return fallback;
            PathRouteAnchor endpointAnchor = resolution.AuthoredNodeAnchors[endpointIndex];

            Dictionary<int, List<PlatformTrackItem>> platformsByNode = trackWorld.TrackDatabase.TrackItems
                .OfType<PlatformTrackItem>()
                .Where(platform => !string.IsNullOrWhiteSpace(platform.StationName))
                .GroupBy(platform => platform.NodeIndex)
                .ToDictionary(group => group.Key, group => group.ToList());

            string stationName = NearestStation(RouteNodeDistances(routeNodes, endpointAnchor, trackWorld, maximumDistance),
                platformsByNode, maximumDistance);
            return stationName ?? fallback;
        }

        /// <summary>Returns the nearest named platform in a directed sequence of route-node distances.</summary>
        internal static string NearestStation(IEnumerable<(int TrackNodeIndex, double StartPosition, TrackDirection Direction, double DistanceToNodeStart)> routeNodes, IEnumerable<PlatformTrackItem> platforms, double maximumDistance)
        {
            Dictionary<int, List<PlatformTrackItem>> platformsByNode = platforms
                .Where(platform => !string.IsNullOrWhiteSpace(platform.StationName))
                .GroupBy(platform => platform.NodeIndex)
                .ToDictionary(group => group.Key, group => group.ToList());

            return NearestStation(routeNodes, platformsByNode, maximumDistance);
        }

        private static string NearestStation(IEnumerable<(int TrackNodeIndex, double StartPosition, TrackDirection Direction, double DistanceToNodeStart)> routeNodes,
            Dictionary<int, List<PlatformTrackItem>> platformsByNode, double maximumDistance)
        {
            foreach ((int trackNodeIndex, double startPosition, TrackDirection direction, double distanceToNodeStart) in routeNodes)
            {
                if (!platformsByNode.TryGetValue(trackNodeIndex, out List<PlatformTrackItem> platforms))
                    continue;

                PlatformTrackItem nearest = platforms
                    .Select(platform => (Platform: platform, Distance: direction == TrackDirection.Ahead
                        ? platform.SectionDistance - startPosition
                        : startPosition - platform.SectionDistance))
                    .Where(candidate => candidate.Distance >= 0
                        && distanceToNodeStart + candidate.Distance <= maximumDistance)
                    .OrderBy(candidate => candidate.Distance)
                    .Select(candidate => candidate.Platform)
                    .FirstOrDefault();
                if (nearest != null)
                    return nearest.StationName.Trim();
            }
            return null;
        }

        private static IEnumerable<(int TrackNodeIndex, double StartPosition, TrackDirection Direction, double DistanceToNodeStart)> RouteNodeDistances(
            ImmutableArray<int> routeNodes, PathRouteAnchor endpointAnchor, TrackWorld trackWorld, double maximumDistance)
        {
            double distanceToNodeStart = 0;
            for (int routeIndex = 0; routeIndex < routeNodes.Length && distanceToNodeStart <= maximumDistance; routeIndex++)
            {
                int trackNodeIndex = routeNodes[routeIndex];
                if (trackWorld.TrackNodeByIndex(trackNodeIndex) is not VectorNode vectorNode)
                    continue;

                double nodeLength = VectorNodeLength(vectorNode, trackWorld);
                TrackDirection direction = DepartureDirection(routeNodes, routeIndex, trackWorld.TrackDatabase);
                double startPosition = routeIndex == 0 && endpointAnchor.TrackNodeIndex == trackNodeIndex
                    ? AnchorPosition(endpointAnchor, vectorNode, trackWorld)
                    : direction == TrackDirection.Ahead ? 0 : nodeLength;
                yield return (trackNodeIndex, startPosition, direction, distanceToNodeStart);

                distanceToNodeStart += direction == TrackDirection.Ahead
                    ? nodeLength - startPosition
                    : startPosition;
            }
        }

        private static int FindEndpointIndex(ImmutableArray<PathNode> nodes, PathNodeType endpointType)
        {
            for (int index = 0; index < nodes.Length; index++)
            {
                if (nodes[index].NodeType.Includes(endpointType))
                    return index;
            }
            return -1;
        }

        private static TrackDirection DepartureDirection(ImmutableArray<int> routeNodes, int routeIndex, TrackDatabase trackDatabase)
        {
            if (routeIndex >= routeNodes.Length - 1)
                return TrackDirection.Ahead;

            ImmutableArray<TrackNodeConnector> connectors = trackDatabase.TrackNodeConnectors[routeNodes[routeIndex]].TrackNodeConnectors;
            return connectors.Length > 1 && connectors[1].Link == routeNodes[routeIndex + 1]
                ? TrackDirection.Ahead
                : TrackDirection.Reverse;
        }

        private static double VectorNodeLength(VectorNode vectorNode, TrackWorld trackWorld)
        {
            double length = 0;
            for (int sectionIndex = 0; sectionIndex < vectorNode.VectorSections.Length; sectionIndex++)
                length += trackWorld.SectionLength(vectorNode, sectionIndex);
            return length;
        }

        private static double AnchorPosition(PathRouteAnchor anchor, VectorNode vectorNode, TrackWorld trackWorld)
        {
            if (anchor.TrackVectorSectionIndex < 0 || anchor.TrackVectorSectionIndex >= vectorNode.VectorSections.Length)
                return 0;

            double position = 0;
            for (int sectionIndex = 0; sectionIndex < anchor.TrackVectorSectionIndex; sectionIndex++)
                position += trackWorld.SectionLength(vectorNode, sectionIndex);

            VectorSectionNode section = vectorNode.VectorSections[anchor.TrackVectorSectionIndex];
            return trackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geometry)
                ? position + geometry.DistanceOnSection(anchor.Location)
                : position;
        }

        private static ImmutableArray<int> BuildRouteNodes(ImmutableArray<ResolvedPathSpan> spans)
        {
            ImmutableArray<int>.Builder nodes = ImmutableArray.CreateBuilder<int>();
            foreach (ResolvedPathSpan span in spans)
            {
                ImmutableArray<int> route = span.Candidates.IsDefaultOrEmpty
                    ? span.TrackVectorNodeIndexes
                    : span.Candidates[0].RouteNodeIndexes;
                foreach (int nodeIndex in route)
                {
                    if (nodes.Count == 0 || nodes[^1] != nodeIndex)
                        nodes.Add(nodeIndex);
                }
            }
            return nodes.ToImmutable();
        }
    }
}
