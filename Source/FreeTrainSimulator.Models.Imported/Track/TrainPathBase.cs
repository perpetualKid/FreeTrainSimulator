using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Imported.Track
{
    public enum PathSectionType
    {
        Invalid,
        MainPath,
        PassingPath,
    }

    public abstract record TrainPathBase : TrackSegmentPathBase<TrainPathSegmentBase>
    {
        public PathModel PathModel { get; }

#pragma warning disable CA1002 // Do not expose generic lists
        public List<TrainPathPointBase> PathPoints { get; } = new List<TrainPathPointBase>();
#pragma warning restore CA1002 // Do not expose generic lists
        protected TrackModel TrackModel { get; }

        protected abstract TrainPathPointBase CreateEditorPathItem(in PointD location, in PointD vector, PathNodeType nodeType);

        protected abstract record TrainPathSectionBase : TrackSegmentSectionBase<TrainPathSegmentBase>
        {
            public PathSectionType PathType { get; internal set; }

            public TrainPathPointBase PathItem { get; set; }

            protected TrainPathSectionBase(in PointD startLocation, in PointD endLocation) :
                base(startLocation, endLocation)
            {
            }

            protected TrainPathSectionBase(TrackModel trackModel, int trackNodeIndex) :
                base(trackModel, trackNodeIndex)
            {
            }

            protected TrainPathSectionBase(TrackModel trackModel, int trackNodeIndex, in PointD startLocation, in PointD endLocation) :
                base(trackModel, trackNodeIndex, startLocation, endLocation)
            {
            }
        }

        protected TrainPathBase(Game game) : base(PointD.None, PointD.None)
        {
            TrackModel = TrackModel.Instance(game);
        }

        protected TrainPathBase(TrackModel trackModel) : base(PointD.None, PointD.None)
        {
            TrackModel = trackModel;
        }

        protected TrainPathBase(PathModel pathModel, Game game)
            : base(PointD.FromWorldLocation(pathModel?.PathNodes.NodeOfType(PathNodeType.Start).Location ?? throw new ArgumentNullException(nameof(pathModel))),
                  PointD.FromWorldLocation(pathModel.PathNodes.NodeOfType(PathNodeType.End).Location))
        {
            TrackModel = TrackModel.Instance(game);
            PathModel = pathModel;
        }

        protected ImmutableArray<TrainPathSectionBase> InitializeSections(PathSectionType pathType, TrainPathPointBase start, TrainPathPointBase end, int index)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();
            TrainPathSectionBase section;

            if (!start.ValidatePathItem(index) || !end.ValidatePathItem(index))
            {
                // either start or end are invalid in a sense they are not on track or no way to connect the ends
                // so we draw an "invalid" path section shown as straight dotted line on the map
                section = InitializeSection(start.Location, end.Location) as TrainPathSectionBase;
                section.PathType = PathSectionType.Invalid;
                sections.Add(section);
            }
            else
            {
                List<TrackSegmentBase> trackSegments = start.ConnectedSegments.IntersectBy(end.ConnectedSegments.Select(s => s.TrackNodeIndex), s => s.TrackNodeIndex).ToList();
                switch (trackSegments.Count)
                {
                    case 0:
                        TrainPathPointBase intermediary = TrackModel.FindIntermediaryConnection(start, end);
                        if (intermediary != null)
                        {
                            sections.AddRange(InitializeSections(pathType, start, intermediary, index));
                            sections.AddRange(InitializeSections(pathType, intermediary, end, index));
                        }
                        else
                        {
                            Debug.WriteLine($"No valid connection found for #{index}");
                            start.ValidationResult |= PathNodeInvalidReasons.NoConnectionPossible;
                            section = InitializeSection(start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = PathSectionType.Invalid;
                            sections.Add(section);
                        }
                        break;
                    case 1:
                        TrackSegmentBase nodeSegment = trackSegments[0];
                        section = InitializeSection(TrackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                        section.PathType = pathType;
                        sections.Add(section);
                        break;
                    default:
                        nodeSegment = trackSegments.Where(s => s.TrackNodeIndex == (start.JunctionNode ?? end.JunctionNode).MainRoute).FirstOrDefault();
                        if (nodeSegment == null)
                        {
                            section = InitializeSection(start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = PathSectionType.Invalid;
                            sections.Add(section);
                            start.ValidationResult |= PathNodeInvalidReasons.NoConnectionPossible;
                        }
                        else
                        {
                            section = InitializeSection(TrackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = pathType;
                            sections.Add(section);
                        }
                        break;
                }
            }
            return sections.ToImmutableArray();
        }

        protected PathModel ToPathModel()
        {
            Length = 0;
            foreach (TrackSegmentSectionBase<TrainPathSegmentBase> section in PathSections)
            {
                Length += section.Length;
            }
            List<PathNode> pathNodes = new List<PathNode>();
            foreach (var pathPoint in PathPoints)
            {
                if (pathPoint.ConnectedSegments.Length == 0)
                    throw new InvalidOperationException("Invalid path point not on track segment");

                TrackSegmentBase segment = pathPoint.ConnectedSegments[0];
                float distance = segment.DistanceOnSegment(pathPoint.Location);

                // find the approximate Elevation by doing an linear interpolation between this section's start and end point
                ref readonly WorldLocation segmentStart = ref (TrackModel.RuntimeData.TrackDB.TrackNodes[pathPoint.ConnectedSegments[0].TrackNodeIndex] as Orts.Formats.Msts.Models.TrackVectorNode).TrackVectorSections[pathPoint.ConnectedSegments[0].TrackVectorSectionIndex].Location;
                ref readonly WorldLocation segmentEnd = ref (TrackModel.ResolveEndNodeLocation(pathPoint.ConnectedSegments[0].TrackNodeIndex, pathPoint.ConnectedSegments[0].TrackVectorSectionIndex));
                float elevation = WorldLocation.InterpolateElevationAlong(segmentStart, segmentEnd, distance);

                WorldLocation location = PointD.ToWorldLocation(pathPoint.Location).SetElevation(elevation);
                pathNodes.Add(new PathNode(location)
                {
                    NodeType = pathPoint.NodeType,
                    NextMainNode = (pathPoint.NodeType & PathNodeType.End) == PathNodeType.End ? -1 : pathPoint.NextMainNode,
                    NextSidingNode = -1,
                });
            }
            return new PathModel()
            {
                Id = "New Path",
                Name = "New Path",
                PlayerPath = true,
                Start = "Start",
                End = "End",
                PathNodes = pathNodes.ToImmutableArray(),
            };
        }
    }

    public static class PathNodeExtensions
    {
        public static PathNode NodeOfType(this IList<PathNode> pathNodes, PathNodeType targetType)
        {
            ArgumentNullException.ThrowIfNull(pathNodes, nameof(pathNodes));

            if (targetType == PathNodeType.End)
            {
                for (int i = pathNodes.Count - 1; i >= 0; i--)
                {
                    if ((pathNodes[i].NodeType & targetType) == targetType)
                        return pathNodes[i];
                }
            }
            else
            {
                for (int i = 0; i < pathNodes.Count; i++)
                {
                    if ((pathNodes[i].NodeType & targetType) == targetType)
                        return pathNodes[i];
                }
            }
            return null;
        }

        public static TrainPathPointBase NodeOfType(this IList<TrainPathPointBase> pathNodes, PathNodeType targetType)
        {
            ArgumentNullException.ThrowIfNull(pathNodes, nameof(pathNodes));

            if (targetType == PathNodeType.End)
            {
                for (int i = pathNodes.Count - 1; i >= 0; i--)
                {
                    if ((pathNodes[i].NodeType & targetType) == targetType)
                        return pathNodes[i];
                }
            }
            else
            {
                for (int i = 0; i < pathNodes.Count; i++)
                {
                    if ((pathNodes[i].NodeType & targetType) == targetType)
                        return pathNodes[i];
                }
            }
            return null;
        }

        public static TrainPathPointBase NextPathPoint(this IList<TrainPathPointBase> pathPoints, TrainPathPointBase currentPathPoint, PathSectionType pathType)
        {
            ArgumentNullException.ThrowIfNull(pathPoints, nameof(pathPoints));
            ArgumentNullException.ThrowIfNull(currentPathPoint, nameof(currentPathPoint));

            return pathType switch
            {
                PathSectionType.MainPath => (currentPathPoint.NextMainNode > -1 && pathPoints.Count > currentPathPoint.NextMainNode) ?
                pathPoints[currentPathPoint.NextMainNode] : null,
                PathSectionType.PassingPath => (currentPathPoint.NextSidingNode > -1 && pathPoints.Count > currentPathPoint.NextSidingNode) ?
                    pathPoints[currentPathPoint.NextSidingNode] : null,
                _ => null,
            };
        }

        public static TrainPathPointBase PreviousPathPoint(this IList<TrainPathPointBase> pathPoints, TrainPathPointBase currentPathPoint, PathSectionType pathType)
        {
            ArgumentNullException.ThrowIfNull(pathPoints, nameof(pathPoints));
            ArgumentNullException.ThrowIfNull(currentPathPoint, nameof(currentPathPoint));

            int currentPathPointIndex = -1;
            for (int i = pathPoints.Count - 1; i >= 0; i--)
            {
                if (currentPathPointIndex == -1 && pathPoints[i] == currentPathPoint)
                {
                    currentPathPointIndex = i;
                }
                else
                {
                    if ((pathType == PathSectionType.PassingPath && pathPoints[i].NextSidingNode == currentPathPointIndex) ||
                        (pathType == PathSectionType.MainPath && pathPoints[i].NextMainNode == currentPathPointIndex))
                        return pathPoints[i];
                }
            }
            return null;
        }

    }
}
