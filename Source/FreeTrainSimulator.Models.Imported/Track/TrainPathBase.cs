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

        public List<TrainPathPointBase> PathPoints { get; } = new List<TrainPathPointBase>();
        protected TrackModel TrackModel { get; }

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
                        intermediary = TrackModel.FindIntermediaryConnection(start, end);
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
            return (sections, intermediary);
        }

        protected PathModel ToPathModel()
        {
            List<PathNode> pathNodes = new List<PathNode>();
            foreach (TrainPathPointBase pathPoint in PathPoints)
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
}
