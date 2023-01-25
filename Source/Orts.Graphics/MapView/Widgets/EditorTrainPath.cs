﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.XPath;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Models.Track;

using static System.Collections.Specialized.BitVector32;

namespace Orts.Graphics.MapView.Widgets
{
    internal class EditorTrainPath : TrackSegmentPathBase<EditorTrainPathSegment>, IDrawable<VectorPrimitive>
    {
        private enum PathType
        {
            Invalid,
            MainPath,
            PassingPath,
        }

        private readonly List<EditorPathItem> pathPoints = new List<EditorPathItem>();

        private readonly ILookup<EditorPathItem, TrainPathSection> pathSections;

        public int SelectedNodeIndex { get; set; } = -1;

        public EditorPathItem SelectedNode => (SelectedNodeIndex >= 0 && SelectedNodeIndex < pathPoints.Count) ? pathPoints[SelectedNodeIndex] : null;

        public TrainPath TrainPathModel { get; private set; }

        private class TrainPathSection : TrackSegmentSectionBase<EditorTrainPathSegment>, IDrawable<VectorPrimitive>
        {
            public PathType PathType { get; private set; }

            public EditorPathItem PathItem { get; set; }

            public TrainPathSection(in PointD startLocation, in PointD endLocation, PathType pathType) :
                base(startLocation, endLocation)
            {
                PathType = pathType;
            }

            public TrainPathSection(TrackModel trackModel, int trackNodeIndex, PathType pathType) :
                base(trackModel, trackNodeIndex)
            {
                PathType = pathType;
            }

            public TrainPathSection(TrackModel trackModel, int trackNodeIndex, in PointD startLocation, in PointD endLocation, PathType pathType) :
                base(trackModel, trackNodeIndex, startLocation, endLocation)
            {
                PathType = pathType;
            }

            public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
            {
                colorVariation = PathType switch
                {
                    PathType.PassingPath => ColorVariation.Highlight,
                    _ => ColorVariation.None,
                };
                foreach (EditorTrainPathSegment segment in SectionSegments)
                {
                    segment.Draw(contentArea, colorVariation, PathType == PathType.Invalid ? -scaleFactor : scaleFactor);
                }
            }

            protected override EditorTrainPathSegment CreateItem(in PointD start, in PointD end)
            {
                return new EditorTrainPathSegment(start, end);
            }

            protected override EditorTrainPathSegment CreateItem(TrackSegmentBase source)
            {
                return new EditorTrainPathSegment(source);
            }

            protected override EditorTrainPathSegment CreateItem(TrackSegmentBase source, in PointD start, in PointD end)
            {
                return new EditorTrainPathSegment(source, start, end);
            }
        }

        public EditorTrainPath(PathFile pathFile, string filePath, Game game)
            : base(PointD.FromWorldLocation(pathFile.PathNodes.Where(n => n.NodeType == PathNodeType.Start).First().Location),
                  PointD.FromWorldLocation(pathFile.PathNodes.Where(n => n.NodeType == PathNodeType.End).First().Location))
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);
            TrackModel trackModel = TrackModel.Instance(game);

            List<TrainPathSection> sections = new List<TrainPathSection>();

            TrainPathModel = new TrainPath(pathFile, filePath, game);

            (TrackSegmentBase NodeSegment, bool Reverse)? sectionStart = null;

            void AddSections(PathType pathType, TrainPathPoint start, TrainPathPoint end, int index)
            {
                if (!start.CheckPathItem(index) || !end.CheckPathItem(index))
                {
                    // either start or end are invalid in a sense they are not on track or no way to connect the ends
                    // so we draw an "invalid" path section shown as straight dotted line on the map
                    sections.Add(new TrainPathSection(start.Location, end.Location, PathType.Invalid));
                }
                else
                {
                    List<TrackSegmentBase> trackSegments = start.ConnectedSegments.IntersectBy(end.ConnectedSegments.Select(s => s.TrackNodeIndex), s => s.TrackNodeIndex).ToList();

                    switch (trackSegments.Count)
                    {
                        case 0:
                            TrainPathPoint intermediary = trackModel.FindIntermediaryConnection(start, end);
                            if (intermediary != null)
                            {
                                AddSections(pathType, start, intermediary, index);
                                AddSections(pathType, intermediary, end, index);
                            }
                            else
                            {
                                Trace.TraceWarning($"No valid connection found for #{index}");
                                start.ValidationResult |= TrainPathNodeInvalidReasons.NoConnectionPossible;
                                sections.Add(new TrainPathSection(start.Location, end.Location, PathType.Invalid));
                            }
                            break;
                        case 1:
                            TrackSegmentBase nodeSegment = trackSegments[0];
                            sections.Add(new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location, pathType));
                            sectionStart ??= (nodeSegment, nodeSegment.IsReverseDirectionTowards(start, end));
                            break;
                        default:
                            nodeSegment = trackSegments.Where(s => s.TrackNodeIndex == (start.JunctionNode ?? end.JunctionNode).MainRoute).First();
                            sections.Add(new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location, pathType));
                            sectionStart ??= (nodeSegment, nodeSegment.IsReverseDirectionTowards(start, end));
                            break;
                    }
                }
            }

            void AddPathPoint(PathType pathType, TrainPathPoint start, TrainPathPoint end, int index)
            {
                sectionStart = null;
                AddSections(pathType, start, end, index);

                if (start.NodeType != PathNodeType.End)
                    PathSections.AddRange(sections);

                EditorPathItem pathItem = null;

                if (start.NextMainItem == null || start.NextMainItem == end)
                {
                    if (sectionStart == null)
                        pathPoints.Add(pathItem = new EditorPathItem(start.Location, end.Location, start.NodeType) { ValidationResult = start.ValidationResult });
                    else
                    {
                        bool reverse = sectionStart.Value.Reverse;
                        if (start.NodeType == PathNodeType.End)
                            reverse = !reverse;
                        pathPoints.Add(pathItem = new EditorPathItem(start.Location, sectionStart.Value.NodeSegment, start.NodeType, reverse) { ValidationResult = start.ValidationResult });
                    }
                }

                foreach (TrainPathSection section in sections)
                {
                    section.PathItem = pathItem ?? pathPoints[^1];
                }
                sections.Clear();
            }

            for (int i = 0; i < TrainPathModel.PathItems.Count; i++)
            {
                TrainPathPoint pathItem = TrainPathModel.PathItems[i];

                if (pathItem.NextMainItem != null) //main path
                {
                    AddPathPoint(PathType.MainPath, pathItem, pathItem.NextMainItem, i);
                }
                if (pathItem.NextSidingItem != null) //passing path
                {
                    AddPathPoint(PathType.PassingPath, pathItem, pathItem.NextSidingItem, i);
                }
            }
            pathSections = PathSections.Select(section => section as TrainPathSection).ToLookup(section => section.PathItem, section => section);
            SetBounds();
        }

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (TrainPathSection pathSection in PathSections)
            {
                pathSection.Draw(contentArea, colorVariation, scaleFactor);
            }
            foreach (EditorPathItem pathItem in pathPoints)
            {
                pathItem.Draw(contentArea, colorVariation, scaleFactor);
            }

            if (SelectedNodeIndex >= 0 && SelectedNodeIndex < pathPoints.Count)
            {
                pathPoints[SelectedNodeIndex].Draw(contentArea, ColorVariation.ComplementHighlight, 5);

                foreach (TrainPathSection pathSection in pathSections[pathPoints[SelectedNodeIndex]])
                {
                    pathSection.Draw(contentArea, colorVariation, 3);
                }
            }
        }

        protected override TrackSegmentSectionBase<EditorTrainPathSegment> AddSection(TrackModel trackModel, int trackNodeIndex, in PointD start, in PointD end)
        {
            throw new System.NotImplementedException();
        }

        protected override TrackSegmentSectionBase<EditorTrainPathSegment> AddSection(TrackModel trackModel, int trackNodeIndex)
        {
            throw new System.NotImplementedException();
        }
    }
}
