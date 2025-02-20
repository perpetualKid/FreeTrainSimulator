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
    public abstract record TrainPathBase : TrackSegmentPathBase<TrainPathSegmentBase>
    {
        protected enum PathType
        {
            Invalid,
            MainPath,
            PassingPath,
        }

        public PathModel PathModel { get; }

#pragma warning disable CA1002 // Do not expose generic lists
        public List<TrainPathPointBase> PathPoints { get; } = new List<TrainPathPointBase>();
#pragma warning restore CA1002 // Do not expose generic lists
        private (TrackSegmentBase NodeSegment, bool Reverse)? sectionStart;
        protected TrackModel TrackModel { get; }

        protected abstract TrainPathPointBase CreateEditorPathItem(in PointD location, in PointD vector, PathNodeType nodeType);

        protected abstract TrainPathPointBase CreateEditorPathItem(in PointD location, TrackSegmentBase trackSegment, PathNodeType nodeType, bool reverseDirection);

        protected abstract record TrainPathSectionBase : TrackSegmentSectionBase<TrainPathSegmentBase>
        {
            public PathType PathType { get; internal set; }

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
            : base(PointD.FromWorldLocation(pathModel?.PathNodes.Where(n => (n.NodeType & PathNodeType.Start) == PathNodeType.Start).First().Location ?? throw new ArgumentNullException(nameof(pathModel))),
                  PointD.FromWorldLocation(pathModel.PathNodes.Where(n => (n.NodeType & PathNodeType.End) == PathNodeType.End).First().Location))
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);
            TrackModel = TrackModel.Instance(game);

            List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();

            PathModel = pathModel;

            List<TrainPathPoint> pathItems = new List<TrainPathPoint>();
            pathItems.AddRange(pathModel.PathNodes.Select(node => new TrainPathPoint(node, TrackModel)));
            TrainPathPointBase.LinkPathPoints(pathItems.Cast<TrainPathPointBase>().ToList(), pathModel.PathNodes.Select(p => (p.NextMainNode, p.NextSidingNode)).ToList());

            for (int i = 0; i < pathItems.Count; i++)
            {
                TrainPathPointBase pathItem = pathItems[i];

                if (pathItem.NextMainItem != null) //main path
                {
                    AddPathPoint(PathType.MainPath, pathItem, pathItem.NextMainItem, i);
                }
                if (pathItem.NextSidingItem != null) //passing path
                {
                    AddPathPoint(PathType.PassingPath, pathItem, pathItem.NextSidingItem, i);
                }
            }
            SetBounds();
        }

        protected void AddPathPoint(PathType pathType, TrainPathPointBase start, TrainPathPointBase end, int index)
        {
            sectionStart = null;
            ImmutableArray<TrainPathSectionBase> sections = AddSections(pathType, start, end, index);

            if ((start.NodeType & PathNodeType.End) != PathNodeType.End)
                PathSections.AddRange(sections);

            TrainPathPointBase pathItem = null;

            if (start.NextMainItem == null || start.NextMainItem == end)
            {
                if (sectionStart == null)
                {
                    PathPoints.Add(pathItem = CreateEditorPathItem(start.Location, end.Location, start.NodeType));
                    pathItem.ValidationResult = start.ValidationResult;
                }
                else
                {
                    bool reverse = sectionStart.Value.Reverse;
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

        protected ImmutableArray<TrainPathSectionBase> AddSections(PathType pathType, TrainPathPointBase start, TrainPathPointBase end, int index)
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
                section.PathType = PathType.Invalid;
                sections.Add(section);
            }
            else
            {
                IEnumerable<TrackSegmentBase> trackSegments = start.ConnectedSegments.IntersectBy(end.ConnectedSegments.Select(s => s.TrackNodeIndex), s => s.TrackNodeIndex);
                TrackSegmentBase nodeSegment;
                if ((nodeSegment = trackSegments.FirstOrDefault()) == null)
                {
                    // no (0) matching track segments
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
                        section.PathType = PathType.Invalid;
                        sections.Add(section);
                    }
                }
                else
                {
                    if (trackSegments.SkipLast(1).Any())
                    {
                        //multiple segments, there should be a junction node
                        nodeSegment = trackSegments.Where(s => s.TrackNodeIndex == (start.JunctionNode ?? end.JunctionNode).MainRoute).FirstOrDefault();
                        if (nodeSegment == null)
                        {
                            section = AddSection(start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = PathType.Invalid;
                            sections.Add(section);
                            start.ValidationResult |= PathNodeInvalidReasons.NoConnectionPossible;
                        }
                        else
                        {
                            section = AddSection(TrackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = pathType;
                            sections.Add(section);
                            sectionStart ??= (nodeSegment, nodeSegment.IsReverseDirectionTowards(start, end));
                        }
                    }
                    else
                    {
                        // one segment, intermediary path point
                        section = AddSection(TrackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                        section.PathType = pathType;
                        sections.Add(section);
                        sectionStart ??= (nodeSegment, nodeSegment.IsReverseDirectionTowards(start, end));
                    }
                }
            }
            return sections.ToImmutableArray();
        }
    }
}
