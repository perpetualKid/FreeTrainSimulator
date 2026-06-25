using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

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

            public TrainPathSection(TrackWorld trackWorld, int trackNodeIndex) :
                base(trackWorld, trackNodeIndex)
            {
            }

            public TrainPathSection(TrackWorld trackWorld, int trackNodeIndex, in PointD startLocation, in PointD endLocation) :
                base(trackWorld, trackNodeIndex, startLocation, endLocation)
            {
            }

            public virtual void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
            {
                colorVariation = PathType switch
                {
                    PathSectionType.PassingPath => ColorVariation.Highlight,
                    _ => ColorVariation.None,
                };
                foreach (EditorTrainPathSegment segment in SectionSegments)
                {
                    segment.Draw(renderer, colorVariation, PathType == PathSectionType.Invalid ? -scaleFactor : scaleFactor);
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

        public EditorTrainPath(PathModel pathModel, TrackWorld trackWorld) : base(pathModel, trackWorld) 
        {
            PathPoints.AddRange(PathModel.PathNodes.Select(node => new EditorPathPoint(node, TrackWorld)));

            for (int i = 0; i < PathPoints.Count; i++)
            {
                TrainPathPointBase startPoint = PathPoints[i];

                void AddPathSections(PathSectionType pathType)
                {
                    TrainPathPointBase endPoint = (startPoint.NodeType & PathNodeType.End) == PathNodeType.End ? PathPoints.PreviousPathPoint(startPoint, pathType) : PathPoints.NextPathPoint(startPoint, pathType);

                    // A partial (incomplete) path can have a dangling last node that is not yet flagged End and
                    // has no next node to connect to; there is no section to build for it, so skip it.
                    if (endPoint == null)
                        return;

                    (startPoint as EditorPathPoint).UpdateDirectionTowards(endPoint, startPoint.ValidationResult == PathNodeInvalidReasons.None, (startPoint.NodeType & PathNodeType.End) == PathNodeType.End);
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

            // When the path is reconstructed from a model (e.g. after undo/redo) with existing points, seed the
            // active editing anchor to the last point so a subsequent pointer move (UpdatePathEndPoint) extends
            // the preview segment from there instead of dereferencing a null anchor.
            if (PathPoints.Count > 0)
                activeEditorSegmentStart = new EditorPathPoint(PathPoints[^1]);
        }

        public new PathModel ToPathModel(PathModelHeader pathModelHeader)
        {
            return base.ToPathModel(pathModelHeader);
        }

        #region path editing
        internal EditorPathPoint AddPathPoint(EditorPathPoint pathPoint)
        {
            if (pathPoint == null)
                return null;

            if (activeEditorSegmentStart != null && activeEditorSegmentStart.ValidationResult != PathNodeInvalidReasons.None)
                return pathPoint;

            activeEditorSegmentStart = new EditorPathPoint(pathPoint);

            pathPoint = PathPoints.Count == 0
                ? pathPoint with { NodeType = PathNodeType.Start, NextMainNode = 1 }
                : pathPoint with { NextMainNode = PathPoints.Count + 1 };
            PathPoints.Add(pathPoint);
            sections.Clear();
            editorUseIntermediaryPathPoint = false;
            pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section) as Lookup<TrainPathPointBase, TrainPathSectionBase>;
            return activeEditorSegmentStart with { NodeType = PathNodeType.None };
        }

        internal EditorPathPoint RemovePathPoint(EditorPathPoint pathPoint)
        {
            if (pathPoint == null)
                return null;

            if (PathPoints.Count > 0)
            {
                PathPoints.RemoveAt(PathPoints.Count - 1);
                // Re-seed the active anchor from the new last point, or clear it when the path is now empty.
                activeEditorSegmentStart = PathPoints.Count > 0 ? new EditorPathPoint(PathPoints[^1]) : null;
                RemoveSections(sections);
                editorUseIntermediaryPathPoint = false;
                pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section) as Lookup<TrainPathPointBase, TrainPathSectionBase>;
            }
            return new EditorPathPoint(pathPoint.Location, pathPoint.Location, PathNodeType.None);
        }

        internal EditorPathPoint UpdatePathEndPoint(in PointD location, Runtime.Track.JunctionNodeBase junctionNode, TrackSegmentBase trackSegment)
        {
            bool startPoint = PathPoints.Count == 0;

            EditorPathPoint pathPoint = new EditorPathPoint(location, junctionNode, trackSegment, TrackWorld)
            {
                NodeType = junctionNode != null || trackSegment != null ? startPoint ? PathNodeType.Start : PathNodeType.Intermediate : PathNodeType.Invalid
            };

            if (!startPoint)
            {
                // The active anchor can be null when the path was reconstructed or reduced to empty; without an
                // anchor there is no segment to preview, so return the candidate point unmodified.
                if (activeEditorSegmentStart == null)
                    return pathPoint;

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

        public virtual void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (TrainPathSection pathSection in PathSections)
            {
                pathSection.Draw(renderer, colorVariation, scaleFactor);
            }
            foreach (EditorPathPoint pathItem in PathPoints)
            {
                pathItem.Draw(renderer, colorVariation, scaleFactor);
            }

            if (SelectedNodeIndex >= 0 && SelectedNodeIndex < PathPoints.Count)
            {
                (PathPoints[SelectedNodeIndex] as EditorPathPoint)?.Draw(renderer, ColorVariation.ComplementHighlight, 5);

                foreach (TrainPathSection pathSection in pathSectionLookup[PathPoints[SelectedNodeIndex]])
                {
                    pathSection.Draw(renderer, colorVariation, 3);
                }
            }
        }

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(in PointD start, in PointD end)
        {
            return new TrainPathSection(start, end);
        }

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(TrackWorld trackWorld, int trackNodeIndex, in PointD start, in PointD end)
        {
            return new TrainPathSection(trackWorld, trackNodeIndex, start, end);
        }

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(TrackWorld trackWorld, int trackNodeIndex)
        {
            throw new System.NotImplementedException();
        }
    }
}
