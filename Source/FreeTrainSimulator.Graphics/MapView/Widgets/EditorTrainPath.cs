using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Models.Track;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal class EditorTrainPath : TrainPathBase, IDrawable<VectorPrimitive>
    {
        #region active path editing
        private EditorPathPoint editorSegmentStart;
        private List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();
        private bool editorUseIntermediaryPathPoint;
        #endregion

        private ILookup<TrainPathPointBase, TrainPathSectionBase> pathSectionLookup;

        public int SelectedNodeIndex { get; set; } = -1;

        public TrainPathPointBase SelectedNode => SelectedNodeIndex >= 0 && SelectedNodeIndex < PathPoints.Count ? PathPoints[SelectedNodeIndex] : null;

        private class TrainPathSection : TrainPathSectionBase, IDrawable<VectorPrimitive>
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

        public EditorTrainPath(PathFile pathFile, string filePath, Game game) : base(pathFile, filePath, game)
        {
            pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section);
        }

        public EditorTrainPath(Game game) : base(game)
        {
        }

        #region path editing
        internal EditorPathPoint AddPathPoint(EditorPathPoint pathItem)
        {
            if (pathItem == null)
                return null;

            if (editorSegmentStart != null && editorSegmentStart.ValidationResult != PathNodeInvalidReasons.None)
                return pathItem;

            editorSegmentStart = new EditorPathPoint(pathItem.Location, TrackModel);
            if (PathPoints.Count == 0)
                pathItem.UpdateNodeType(PathNodeType.Start);
            else
            {
                pathItem.UpdateNodeType(PathNodeType.Junction);
            }
            PathPoints.Add(pathItem);
            sections.Clear();
            editorUseIntermediaryPathPoint = false;
            pathSectionLookup = PathSections.Select(section => section as TrainPathSectionBase).ToLookup(section => section.PathItem, section => section);
            return new EditorPathPoint(pathItem.Location, pathItem.Location, PathNodeType.Temporary);
        }

        internal void UpdateLocation(in PointD location)
        {
            if (editorSegmentStart != null)
            {
                EditorPathPoint end = new EditorPathPoint(location, TrackModel);
                editorSegmentStart.ValidationResult = PathNodeInvalidReasons.None;
                PathSections.RemoveRange(PathSections.Count - sections.Count, sections.Count);

                if (editorUseIntermediaryPathPoint)
                    PathPoints.RemoveAt(PathPoints.Count - 1);
                editorUseIntermediaryPathPoint = false;
                sections = AddSections(PathType.MainPath, editorSegmentStart, end, 0);

                if (PathSections.Count > 0)
                {
                    TrackSegmentSectionBase<TrainPathSegmentBase> previous = PathSections[^1];
                    bool reverse = previous.SectionSegments[0].IsReverseDirectionTowards(previous.Location, previous.Vector);
                    if (sections[0].TrackNodeIndex == previous.TrackNodeIndex && reverse != sections[0].SectionSegments[0].IsReverseDirectionTowards(editorSegmentStart.Location, location))
                        (PathPoints[^1] as EditorPathPoint).UpdateNodeType(PathNodeType.Reversal);
                    else
                    {
                        (PathPoints[^1] as EditorPathPoint).UpdateNodeType(PathNodeType.Junction);
                    }
                }
                if (sections.Count > 1)
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
