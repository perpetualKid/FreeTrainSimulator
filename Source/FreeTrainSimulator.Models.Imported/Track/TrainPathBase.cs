using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;

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
        private (TrackSegmentBase NodeSegment, TrackDirection PathDirection)? sectionStart;
        protected TrackModel TrackModel { get; }

        protected abstract TrainPathPointBase CreateEditorPathItem(in PointD location, in PointD vector, PathNodeType nodeType);

        protected abstract TrainPathPointBase CreateEditorPathItem(in PointD location, TrackSegmentBase trackSegment, PathNodeType nodeType, bool reverseDirection);

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
            RuntimeData runtimeData = RuntimeData.GameInstance(game);
            TrackModel = TrackModel.Instance(game);

            List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();

            PathModel = pathModel;

            List<TrainPathPointBase> pathItems = new List<TrainPathPointBase>();
            pathItems.AddRange(pathModel.PathNodes.Select(node => new TrainPathPoint(node, TrackModel)));
//            TrainPathPointBase.LinkPathPoints(pathItems, pathModel.PathNodes.Select(p => (p.NextMainNode, p.NextSidingNode)).ToList());

            for (int i = 0; i < pathItems.Count; i++)
            {
                TrainPathPointBase pathItem = pathItems[i];

                if (pathItem.NextMainNode > -1) //main path
                {
                    AddPathPoint(PathSectionType.MainPath, pathItems, i);
                }
                if (pathItem.NextSidingNode > -1) //passing path
                {
                    AddPathPoint(PathSectionType.PassingPath, pathItems, i);
                }
                if (pathItem.NextMainNode == -1 && pathItem.NextSidingNode == -1) // end node
                {
                    AddPathPoint(PathSectionType.MainPath, pathItems, i, true);
                }
            }
            SetBounds();
        }

        protected TrainPathBase(PathModel pathModel, ImmutableArray<TrainPathPointBase> pathPoints, Game game)
            : base(pathPoints.NodeOfType(PathNodeType.Start).Location, pathPoints.NodeOfType(PathNodeType.End).Location)
        {
            List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();

            TrackModel = TrackModel.Instance(game);

            PathModel = pathModel ?? throw new ArgumentNullException(nameof(pathModel));

            List<TrainPathPointBase> pathItems = new List<TrainPathPointBase>();
            pathItems.AddRange(pathPoints.Select(node => new TrainPathPoint(node)));
            //TrainPathPointBase.LinkPathPoints(pathItems, pathItems.Select((p, Index) => (Index == (pathItems.Count - 1) ? -1 : (Index + 1), -1)).ToList());

            for (int i = 0; i < pathItems.Count; i++)
            {
                TrainPathPointBase pathItem = pathItems[i];

                if (pathItem.NextMainNode > -1) //main path
                {
                    AddPathPoint(PathSectionType.MainPath, pathItems, i);
                }
                if (pathItem.NextSidingNode > -1) //passing path
                {
                    AddPathPoint(PathSectionType.PassingPath, pathItems, i);
                }
                if (pathItem.NextMainNode == -1 && pathItem.NextSidingNode == -1) // end node
                {
                    AddPathPoint(PathSectionType.MainPath, pathItems, i, true);
                }
            }
            SetBounds();
        }

        protected void AddPathPoint(PathSectionType pathType, IList<TrainPathPointBase> pathItems, int index, bool endNode = false)
        {
            ArgumentNullException.ThrowIfNull(pathItems, nameof(pathItems));

            sectionStart = null;

            TrainPathPointBase pathItem = null;

            TrainPathPointBase start = pathItems[index];
            TrainPathPointBase end = (start.NodeType & PathNodeType.End) == PathNodeType.End ? pathItems.PreviousPathPoint(start, pathType) : pathItems.NextPathPoint(start, pathType);

            ImmutableArray<TrainPathSectionBase> sections = AddSections(pathType, start, end, index);

            if ((start.NodeType & PathNodeType.End) != PathNodeType.End)
                PathSections.AddRange(sections);

//            if (start.NextMainItem == null || start.NextMainItem == end)
            {
                if (sectionStart == null)
                {
                    PathPoints.Add(pathItem = CreateEditorPathItem(start.Location, end.Location, start.NodeType));
                    pathItem.ValidationResult = start.ValidationResult;
                }
                else
                {
                    bool reverse = sectionStart.Value.PathDirection == TrackDirection.Reverse;
                    if ((start.NodeType & PathNodeType.End) == PathNodeType.End)
                        reverse = !reverse;
                    PathPoints.Add(pathItem = CreateEditorPathItem(start.Location, sectionStart.Value.NodeSegment, start.NodeType, reverse));
                    pathItem.ValidationResult = start.ValidationResult;
                }
            }

            foreach (TrainPathSectionBase section in sections)
            {
                section.PathItem = pathItem ?? PathPoints[^1];
            }
        }

        protected ImmutableArray<TrainPathSectionBase> AddSections(PathSectionType pathType, TrainPathPointBase start, TrainPathPointBase end, int index)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();
            TrainPathSectionBase section;

            if (!start.ValidatePathItem(index) || !end.ValidatePathItem(index))
            {
                // either start or end are invalid in a sense they are not on track or no way to connect the ends
                // so we draw an "invalid" path section shown as straight dotted line on the map
                section = AddSection(start.Location, end.Location) as TrainPathSectionBase;
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
                            foreach (TrainPathSectionBase item in AddSections(pathType, start, intermediary, index))
                                sections.Add(item);
                            foreach (TrainPathSectionBase item in AddSections(pathType, intermediary, end, index))
                                sections.Add(item);
                        }
                        else
                        {
                            Debug.WriteLine($"No valid connection found for #{index}");
                            start.ValidationResult |= PathNodeInvalidReasons.NoConnectionPossible;
                            section = AddSection(start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = PathSectionType.Invalid;
                            sections.Add(section);
                        }
                        break;
                    case 1:
                        TrackSegmentBase nodeSegment = trackSegments[0];
                        section = AddSection(TrackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                        section.PathType = pathType;
                        sections.Add(section);
                        sectionStart ??= (nodeSegment, nodeSegment.TrackDirectionOnSegment(start, end));
                        break;
                    default:
                        nodeSegment = trackSegments.Where(s => s.TrackNodeIndex == (start.JunctionNode ?? end.JunctionNode).MainRoute).FirstOrDefault();
                        if (nodeSegment == null)
                        {
                            section = AddSection(start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = PathSectionType.Invalid;
                            sections.Add(section);
                            start.ValidationResult |= PathNodeInvalidReasons.NoConnectionPossible;
                        }
                        else
                        {
                            section = AddSection(TrackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = pathType;
                            sections.Add(section);
                            sectionStart ??= (nodeSegment, nodeSegment.TrackDirectionOnSegment(start, end));
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
