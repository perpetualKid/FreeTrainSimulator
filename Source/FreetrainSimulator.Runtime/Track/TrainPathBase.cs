using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Runtime.Track
{
    public abstract record TrainPathBase : TrackSegmentPathBase<TrainPathSegmentBase>
    {
        public PathModel PathModel { get; }

#pragma warning disable CA1002 // Do not expose generic lists
        public List<TrainPathPointBase> PathPoints { get; } = new List<TrainPathPointBase>();
#pragma warning restore CA1002 // Do not expose generic lists
        protected TrackWorld TrackWorld { get; }

        protected abstract record TrainPathSectionBase : TrackSegmentSectionBase<TrainPathSegmentBase>
        {
            public PathSectionType PathType { get; internal set; }

            public TrainPathPointBase PathItem { get; set; }

            protected TrainPathSectionBase(in PointD startLocation, in PointD endLocation) :
                base(startLocation, endLocation)
            {
            }

            protected TrainPathSectionBase(TrackWorld trackWorld, int trackNodeIndex) :
                base(trackWorld, trackNodeIndex)
            {
            }

            protected TrainPathSectionBase(TrackWorld trackWorld, int trackNodeIndex, in PointD startLocation, in PointD endLocation) :
                base(trackWorld, trackNodeIndex, startLocation, endLocation)
            {
            }
        }

        protected TrainPathBase(PathModel pathModel, TrackWorld trackWorld)
            :   base(pathModel == null ? throw new ArgumentNullException(nameof(pathModel)) :
                    pathModel.PathNodes.IsDefaultOrEmpty ? PointD.None :
                    // During editing the path may be incomplete (a partial path has a Start and intermediate
                    // nodes but no End yet); fall back to the first/last node for the viewport bounds rather
                    // than requiring a complete path. Structural completeness is validated by PathRouteResolver.
                    PointD.FromWorldLocation((pathModel.PathNodes.NodeOfType(PathNodeType.Start) ?? pathModel.PathNodes[0]).Location),
                  pathModel.PathNodes.IsDefaultOrEmpty ? PointD.None : 
                    PointD.FromWorldLocation((pathModel.PathNodes.NodeOfType(PathNodeType.End) ?? pathModel.PathNodes[^1]).Location))
        {
            TrackWorld = trackWorld ?? throw new ArgumentNullException(nameof(trackWorld));
            PathModel = pathModel;
        }

        protected (List<TrainPathSectionBase> Sections, TrainPathPointBase JunctionNode) InitializeSections(PathSectionType pathType, TrainPathPointBase start, TrainPathPointBase end)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();
            TrainPathPointBase intermediary = null;
            TrainPathSectionBase section;

            if (start.ValidationResult != PathNodeInvalidReasons.None || end.ValidationResult != PathNodeInvalidReasons.None)
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
                        intermediary = TrackWorld.FindIntermediaryConnection(start, end);
                        if (intermediary != null)
                        {
                            sections.AddRange(InitializeSections(pathType, start, intermediary).Sections);
                            sections.AddRange(InitializeSections(pathType, intermediary, end).Sections);
                        }
                        else
                        {
                            start.ValidationResult |= PathNodeInvalidReasons.NoConnectionPossible;
                            section = InitializeSection(start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = PathSectionType.Invalid;
                            sections.Add(section);
                        }
                        break;
                    case 1:
                        TrackSegmentBase nodeSegment = trackSegments[0];
                        section = InitializeSection(TrackWorld, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                        section.PathType = pathType;
                        sections.Add(section);
                        break;
                    default:
                        JunctionNodeBase junctionNode = start.JunctionNode ?? end.JunctionNode;
                        // if neither end is a junction, there is no main route to prefer, so just take the first shared segment
                        nodeSegment = junctionNode == null
                            ? trackSegments[0]
                            : trackSegments.Where(s => s.TrackNodeIndex == junctionNode.MainRoute).FirstOrDefault();
                        if (nodeSegment == null)
                        {
                            section = InitializeSection(start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = PathSectionType.Invalid;
                            sections.Add(section);
                            start.ValidationResult |= PathNodeInvalidReasons.NoConnectionPossible;
                        }
                        else
                        {
                            section = InitializeSection(TrackWorld, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = pathType;
                            sections.Add(section);
                        }
                        break;
                }
            }
            return (sections, intermediary);
        }

        protected PathModel ToPathModel(PathModelHeader pathDetails)
        {
            List<PathNode> pathNodes = new List<PathNode>();
            foreach (TrainPathPointBase pathPoint in PathPoints)
            {
                if (pathPoint.ConnectedSegments.IsDefaultOrEmpty)
                    throw new InvalidOperationException("Invalid path point not on track segment");

                TrackSegmentBase segment = pathPoint.ConnectedSegments[0];
                float distance = segment.DistanceOnSegment(pathPoint.Location);

                // find the approximate Elevation by doing an linear interpolation between this section's start and end point
                ref readonly WorldLocation segmentStart = ref (TrackWorld.TrackDatabase.TrackNodes[pathPoint.ConnectedSegments[0].TrackNodeIndex] as VectorNode).VectorSections[pathPoint.ConnectedSegments[0].TrackVectorSectionIndex].Location;
                ref readonly WorldLocation segmentEnd = ref TrackWorld.ResolveEndNodeLocation(pathPoint.ConnectedSegments[0].TrackNodeIndex, pathPoint.ConnectedSegments[0].TrackVectorSectionIndex);
                float elevation = WorldLocation.PointAlongDirection(segmentStart, segmentEnd, distance).Location.Y;

                WorldLocation location = PointD.ToWorldLocation(pathPoint.Location).SetElevation(elevation);
                pathNodes.Add(new PathNode(location)
                {
                    NodeType = pathPoint.NodeType,
                    NodeIndex = pathPoint.NodeIndex,
                    NextMainNode = pathPoint.NodeType.Includes(PathNodeType.End) ? -1 : pathPoint.NextMainNode,
                    NextSidingNode = pathPoint.NextSidingNode,
                    WaitInfo = pathPoint.WaitInfo,
                });
            }

            return new PathModel(pathDetails)
            {
                PathNodes = pathNodes.ToImmutableArray(),
            };
        }
    }
}
