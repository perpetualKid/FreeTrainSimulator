using System.Collections.Immutable;
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
        private EditorPathPoint editorSegmentStart;
        private ImmutableArray<TrainPathSectionBase> sections = ImmutableArray<TrainPathSectionBase>.Empty;
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
            pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section) as Lookup<TrainPathPointBase, TrainPathSectionBase>;
        }

        public EditorTrainPath(PathModel pathModel, ImmutableArray<TrainPathPointBase> pathPoints, Game game) : base(pathModel, pathPoints, game)
        {
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

            if (editorSegmentStart != null && editorSegmentStart.ValidationResult != PathNodeInvalidReasons.None)
                return pathPoint;

            editorSegmentStart = new EditorPathPoint(pathPoint.Location, TrackModel);

            pathPoint = PathPoints.Count == 0
                ? (editorSegmentStart with { NodeType = PathNodeType.Start | editorSegmentStart.NodeType, NextMainNode = 1 })
                : (editorSegmentStart with { NextMainNode = PathPoints.Count + 1 });
            PathPoints.Add(pathPoint);
            //if ((pathPoint.NodeType & PathNodeType.Start) != PathNodeType.Start)
            //{
            //    PathPoints[^2].NextMainItem = pathPoint;
            //}
            sections = sections.Clear();
            editorUseIntermediaryPathPoint = false;
            pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section) as Lookup<TrainPathPointBase, TrainPathSectionBase>;
            return editorSegmentStart with { NodeType = PathNodeType.None };
        }

        internal EditorPathPoint RemovePathPoint(EditorPathPoint pathPoint)
        {
            if (pathPoint == null)
                return null;

            if (PathPoints.Count > 0)
            {
                PathPoints.RemoveAt(PathPoints.Count - 1);
                editorSegmentStart = new EditorPathPoint(PathPoints[^1].Location, TrackModel);
                PathSections.RemoveRange(PathSections.Count - sections.Length, sections.Length);
                editorUseIntermediaryPathPoint = false;
                pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section) as Lookup<TrainPathPointBase, TrainPathSectionBase>;
            }
            return new EditorPathPoint(pathPoint.Location, pathPoint.Location, PathNodeType.None);
        }

        internal void UpdateLocation(in PointD location)
        {
            if (editorSegmentStart != null)
            {
                EditorPathPoint end = new EditorPathPoint(location, TrackModel);
                editorSegmentStart.ValidationResult = PathNodeInvalidReasons.None;
                PathSections.RemoveRange(PathSections.Count - sections.Length, sections.Length);

                if (editorUseIntermediaryPathPoint)
                    PathPoints.RemoveAt(PathPoints.Count - 1);
                editorUseIntermediaryPathPoint = false;
                sections = AddSections(PathSectionType.MainPath, editorSegmentStart, end, 0);

                if (PathSections.Count > 0)
                {
                    TrackSegmentSectionBase<TrainPathSegmentBase> previous = PathSections[^1];
                    TrackDirection direction = previous.SectionSegments[0].TrackDirectionOnSegment(previous.Location, previous.Vector);
                    if (sections[0].TrackNodeIndex == previous.TrackNodeIndex && direction != sections[0].SectionSegments[0].TrackDirectionOnSegment(editorSegmentStart.Location, location))
                        PathPoints[^1] = PathPoints[^1] with { NodeType = PathNodeType.Reversal};
                }

                if (sections.Length > 1)
                {
                    PathPoints.Add(CreateEditorPathItem(sections[0].Vector, end.Location, PathNodeType.Junction));
                    editorUseIntermediaryPathPoint = true;
                }
                PathSections.AddRange(sections);
            }
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

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> AddSection(in PointD start, in PointD end)
        {
            return new TrainPathSection(start, end);
        }

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> AddSection(TrackModel trackModel, int trackNodeIndex, in PointD start, in PointD end)
        {
            return new TrainPathSection(trackModel, trackNodeIndex, start, end);
        }

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> AddSection(TrackModel trackModel, int trackNodeIndex)
        {
            throw new System.NotImplementedException();
        }

        protected override TrainPathPointBase CreateEditorPathItem(in PointD location, in PointD vector, PathNodeType nodeType)
        {
            return new EditorPathPoint(location, vector, nodeType);
        }

        protected override TrainPathPointBase CreateEditorPathItem(in PointD location, TrackSegmentBase trackSegment, PathNodeType nodeType, bool reverseDirection)
        {
            return new EditorPathPoint(location, trackSegment, nodeType, reverseDirection);
        }
    }
}
