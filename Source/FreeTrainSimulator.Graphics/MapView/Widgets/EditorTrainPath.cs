using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal record EditorTrainPath : TrainPathBase, IDrawable<VectorPrimitive>
    {
        #region active path editing
        private EditorPathPoint activeEditorSegmentStart;
        private List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();
        private bool editorUseIntermediaryPathPoint;
        #endregion

        private Lookup<TrainPathPointBase, TrainPathSectionBase> pathSectionLookup;

        public int SelectedNodeIndex { get; set; } = -1;

        public TrainPathPointBase SelectedNode => SelectedNodeIndex >= 0 && SelectedNodeIndex < PathPoints.Count ? PathPoints[SelectedNodeIndex] : null;

        private record TrainPathSection : TrainPathSectionBase, IDrawable<VectorPrimitive>
        {
            public TrainPathSection(in PointD startLocation, in PointD endLocation) :
                base(startLocation, endLocation)
            {
            }

            public TrainPathSection(TrackModel trackModel, int trackNodeIndex) :
                base(trackModel, trackNodeIndex)
            {
            }

            public TrainPathSection(TrackModel trackModel, int trackNodeIndex, in PointD startLocation, in PointD endLocation) :
                base(trackModel, trackNodeIndex, startLocation, endLocation)
            {
            }

            public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
            {
                colorVariation = PathType switch
                {
                    PathSectionType.PassingPath => ColorVariation.Highlight,
                    _ => ColorVariation.None,
                };
                foreach (EditorTrainPathSegment segment in SectionSegments)
                {
                    segment.Draw(contentArea, colorVariation, PathType == PathSectionType.Invalid ? -scaleFactor : scaleFactor);
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

        public EditorTrainPath(PathModel pathModel, Game game) : base(pathModel, game)
        {
            PathPoints.AddRange(PathModel.PathNodes.Select(node => new EditorPathPoint(node, TrackModel)));

            for (int i = 0; i < PathPoints.Count; i++)
            {
                TrainPathPointBase startPoint = PathPoints[i];

                void AddPathSections(PathSectionType pathType)
                {
                    TrainPathPointBase endPoint = (startPoint.NodeType & PathNodeType.End) == PathNodeType.End ? PathPoints.PreviousPathPoint(startPoint, pathType) : PathPoints.NextPathPoint(startPoint, pathType);

                    (startPoint as EditorPathPoint).UpdateDirectionTowards(endPoint, true, (startPoint.NodeType & PathNodeType.End) == PathNodeType.End);
                    List<TrainPathSectionBase> sections = InitializeSections(pathType, startPoint, endPoint).Sections;

                    if ((startPoint.NodeType & PathNodeType.End) != PathNodeType.End)
                    {
                        AddSections(sections);
                    }
                }

                if (startPoint.NextMainNode > -1) //main path
                {
                    AddPathSections(PathSectionType.MainPath);
                }
                if (startPoint.NextSidingNode > -1) //passing path
                {
                    AddPathSections(PathSectionType.PassingPath);
                }
                if (startPoint.NextMainNode == -1 && startPoint.NextSidingNode == -1) // end node
                {
                    AddPathSections(PathSectionType.MainPath);
                }
            }

            SetBounds();
            pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section) as Lookup<TrainPathPointBase, TrainPathSectionBase>;
        }

        public EditorTrainPath(Game game) : base(game)
        {
        }

        public new PathModel ToPathModel()
        {
            return base.ToPathModel();
        }

        #region path editing
        internal EditorPathPoint AddPathPoint(EditorPathPoint pathPoint)
        {
            if (pathPoint == null)
                return null;

            if (activeEditorSegmentStart != null && activeEditorSegmentStart.ValidationResult != PathNodeInvalidReasons.None)
                return pathPoint;

            activeEditorSegmentStart = new EditorPathPoint(pathPoint.Location, TrackModel);

            pathPoint = PathPoints.Count == 0
                ? new EditorPathPoint(pathPoint.Location, TrackModel) { NodeType = PathNodeType.Start, NextMainNode = 1 }
                : new EditorPathPoint(pathPoint.Location, TrackModel) { NextMainNode = PathPoints.Count + 1 };
            PathPoints.Add(pathPoint);
            sections.Clear();
            editorUseIntermediaryPathPoint = false;
            pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section) as Lookup<TrainPathPointBase, TrainPathSectionBase>;
            //return new EditorPathPoint(PointD.None, PointD.None, PathNodeType.Start);// editorSegmentStart with { NodeType = PathNodeType.None };
            return activeEditorSegmentStart with { NodeType = PathNodeType.None };
        }

        internal EditorPathPoint RemovePathPoint(EditorPathPoint pathPoint)
        {
            if (pathPoint == null)
                return null;

            if (PathPoints.Count > 0)
            {
                PathPoints.RemoveAt(PathPoints.Count - 1);
                activeEditorSegmentStart = new EditorPathPoint(PathPoints[^1].Location, TrackModel);
                RemoveSections(sections);
                editorUseIntermediaryPathPoint = false;
                pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section) as Lookup<TrainPathPointBase, TrainPathSectionBase>;
            }
            return new EditorPathPoint(pathPoint.Location, pathPoint.Location, PathNodeType.None);
        }

        internal EditorPathPoint UpdatePathEndPoint(in PointD location, JunctionNodeBase junctionNode, TrackSegmentBase trackSegment)
        {
            bool startPoint = PathPoints.Count == 0;

            EditorPathPoint pathPoint = new EditorPathPoint(location, junctionNode, trackSegment, TrackModel)
            {
                NodeType = junctionNode != null || trackSegment != null ? startPoint ? PathNodeType.Start : PathNodeType.Intermediate : PathNodeType.Invalid
            };

            if (!startPoint)
            {
                activeEditorSegmentStart.ValidationResult = PathNodeInvalidReasons.None;
                RemoveSections(sections);

                if (editorUseIntermediaryPathPoint)
                    PathPoints.RemoveAt(PathPoints.Count - 1);
                editorUseIntermediaryPathPoint = false;
                TrainPathPointBase intermediaryJunction;
                (sections, intermediaryJunction) = InitializeSections(PathSectionType.MainPath, activeEditorSegmentStart, pathPoint);

                if (PathSections.Length > 0)
                {
                    PathNodeType nodeType = PathPoints[^1].NodeType;
                    //check if we do a reversal 
                    TrackSegmentSectionBase<TrainPathSegmentBase> previous = PathSections[^1];
                    TrackDirection direction = previous.SectionSegments[0].TrackDirectionOnSegment(previous.Location, previous.Vector);
                    if (sections[0].TrackNodeIndex == previous.TrackNodeIndex && direction != sections[0].SectionSegments[0].TrackDirectionOnSegment(activeEditorSegmentStart.Location, pathPoint.Location))
                        nodeType |= PathNodeType.Reversal;
                    else
                        nodeType &= ~PathNodeType.Reversal;
                    PathPoints[^1] = PathPoints[^1] with { NodeType = nodeType };
                }

                if (sections.Count > 1) // the new sections cross a junction
                {
                    PathPoints.Add(new EditorPathPoint(intermediaryJunction) with { NodeType = PathNodeType.Junction });
                    //AddPathPoint(new EditorPathPoint(intermediaryJunction) with { NodeType = PathNodeType.Junction });
                    editorUseIntermediaryPathPoint = true;
                }
                AddSections(sections);

                pathPoint.UpdateDirectionTowards(PathPoints[^1], trackSegment != null, true);
                (PathPoints[^1] as EditorPathPoint).UpdateDirectionTowards(pathPoint, trackSegment != null, false);
            }

            return pathPoint;
        }
        #endregion

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
            foreach (EditorPathPoint pathItem in PathPoints)
            {
                pathItem.Draw(contentArea, colorVariation, scaleFactor);
            }

            if (SelectedNodeIndex >= 0 && SelectedNodeIndex < PathPoints.Count)
            {
                (PathPoints[SelectedNodeIndex] as EditorPathPoint)?.Draw(contentArea, ColorVariation.ComplementHighlight, 5);

                foreach (TrainPathSection pathSection in pathSectionLookup[PathPoints[SelectedNodeIndex]])
                {
                    pathSection.Draw(contentArea, colorVariation, 3);
                }
            }
        }

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(in PointD start, in PointD end)
        {
            return new TrainPathSection(start, end);
        }

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(TrackModel trackModel, int trackNodeIndex, in PointD start, in PointD end)
        {
            return new TrainPathSection(trackModel, trackNodeIndex, start, end);
        }

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(TrackModel trackModel, int trackNodeIndex)
        {
            throw new System.NotImplementedException();
        }
    }
}
