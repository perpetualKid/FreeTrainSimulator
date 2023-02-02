using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace Orts.Models.Track
{
    public abstract class TrainPathBase : TrackSegmentPathBase<TrainPathSegmentBase>
    {
        protected enum PathType
        {
            Invalid,
            MainPath,
            PassingPath,
        }

        public string FilePath { get; }
        public bool Invalid { get; set; }
        private List<TrainPathItemPoint> PathItems { get; } = new List<TrainPathItemPoint>();

        public PathFile PathFile { get; }

#pragma warning disable CA1002 // Do not expose generic lists
        public List<TrainPathItemBase> PathPoints { get; } = new List<TrainPathItemBase>();
#pragma warning restore CA1002 // Do not expose generic lists
        private (TrackSegmentBase NodeSegment, bool Reverse)? sectionStart;
        protected TrackModel TrackModel { get; }

        protected abstract TrainPathItemBase CreateEditorPathItem(in PointD location, in PointD vector, PathNodeType nodeType);

        protected abstract TrainPathItemBase CreateEditorPathItem(in PointD location, TrackSegmentBase trackSegment, PathNodeType nodeType, bool reverseDirection);

        protected abstract class TrainPathSectionBase : TrackSegmentSectionBase<TrainPathSegmentBase>
        {
            public PathType PathType { get; internal set; }

            public TrainPathItemBase PathItem { get; set; }

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

            public void UpdateVector(in PointD vector)
            {
                SectionSegments[0].UpdateVector(vector);
            }
        }

        protected TrainPathBase(Game game) : base(PointD.None, PointD.None)
        {
            TrackModel = TrackModel.Instance(game);
        }

        protected TrainPathBase(PathFile pathFile, string filePath, Game game)
            : base(PointD.FromWorldLocation(pathFile?.PathNodes.Where(n => n.NodeType == PathNodeType.Start).First().Location ?? throw new ArgumentNullException(nameof(pathFile))),
                  PointD.FromWorldLocation(pathFile.PathNodes.Where(n => n.NodeType == PathNodeType.End).First().Location))
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);
            TrackModel = TrackModel.Instance(game);

            List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();

            PathFile = pathFile;
            FilePath = filePath;

            PathItems.AddRange(pathFile.PathNodes.Select(node => new TrainPathItemPoint(node, TrackModel)));
            TrainPathItemBase.LinkPathPoints(PathItems.Cast<TrainPathItemBase>().ToList());

            for (int i = 0; i < PathItems.Count; i++)
            {
                TrainPathItemBase pathItem = PathItems[i];

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

        protected void AddPathPoint(PathType pathType, TrainPathItemBase start, TrainPathItemBase end, int index)
        {
            sectionStart = null;
            List<TrainPathSectionBase> sections = AddSections(pathType, start, end, index);

            if (start.NodeType != PathNodeType.End)
                PathSections.AddRange(sections);

            TrainPathItemBase pathItem = null;

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
                    if (start.NodeType == PathNodeType.End)
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

#pragma warning disable CA1002 // Do not expose generic lists
        protected List<TrainPathSectionBase> AddSections(PathType pathType, TrainPathItemBase start, TrainPathItemBase end, int index)
#pragma warning restore CA1002 // Do not expose generic lists
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();
            TrainPathSectionBase section;

            if (!start.CheckPathItem(index) || !end.CheckPathItem(index))
            {
                // either start or end are invalid in a sense they are not on track or no way to connect the ends
                // so we draw an "invalid" path section shown as straight dotted line on the map
                section = AddSection(start.Location, end.Location) as TrainPathSectionBase;
                section.PathType = PathType.Invalid;
                sections.Add(section);
            }
            else
            {
                List<TrackSegmentBase> trackSegments = start.ConnectedSegments.IntersectBy(end.ConnectedSegments.Select(s => s.TrackNodeIndex), s => s.TrackNodeIndex).ToList();
                switch (trackSegments.Count)
                {
                    case 0:
                        TrainPathItemBase intermediary = TrackModel.FindIntermediaryConnection(start, end);
                        if (intermediary != null)
                        {
                            foreach (var item in AddSections(pathType, start, intermediary, index))
                                sections.Add(item);
                            foreach (var item in AddSections(pathType, intermediary, end, index))
                                sections.Add(item);
                        }
                        else
                        {
                            Trace.TraceWarning($"No valid connection found for #{index}");
                            start.ValidationResult |= InvalidReasons.NoConnectionPossible;
                            section = AddSection(start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = PathType.Invalid;
                            sections.Add(section);
                        }
                        break;
                    case 1:
                        TrackSegmentBase nodeSegment = trackSegments[0];
                        section = AddSection(TrackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                        section.PathType = pathType;
                        sections.Add(section);
                        sectionStart ??= (nodeSegment, nodeSegment.IsReverseDirectionTowards(start, end));
                        break;
                    default:
                        nodeSegment = trackSegments.Where(s => s.TrackNodeIndex == (start.JunctionNode ?? end.JunctionNode).MainRoute).FirstOrDefault();
                        if (nodeSegment == null)
                        {
                            section = AddSection(start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = PathType.Invalid;
                            sections.Add(section);
                            start.ValidationResult |= InvalidReasons.NoConnectionPossible;
                        }
                        else
                        {
                            section = AddSection(TrackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location) as TrainPathSectionBase;
                            section.PathType = pathType;
                            sections.Add(section);
                            sectionStart ??= (nodeSegment, nodeSegment.IsReverseDirectionTowards(start, end));
                        }
                        break;
                }
            }
            return sections;
        }
    }
}
