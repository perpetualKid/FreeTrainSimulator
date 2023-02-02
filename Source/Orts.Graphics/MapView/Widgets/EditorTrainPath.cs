﻿using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class EditorTrainPath : TrainPathBase, IDrawable<VectorPrimitive>
    {
        private readonly ILookup<TrainPathItemBase, TrainPathSectionBase> pathSectionLookup;

        public int SelectedNodeIndex { get; set; } = -1;

        public TrainPathItemBase SelectedNode => (SelectedNodeIndex >= 0 && SelectedNodeIndex < PathPoints.Count) ? PathPoints[SelectedNodeIndex] : null;

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

        public EditorTrainPath(Game game): base(game)
        {
        }

        #region path editing
        internal EditorPathItem Update(EditorPathItem pathItem)
        { 
            if (pathItem == null)
                return null;

            if (editorSegmentStart != null && editorSegmentStart.ValidationResult != TrainPathPoint.InvalidReasons.None)
                return pathItem;

            editorSegmentStart = new TrainPathPoint(pathItem.Location, TrackModel);
            PathPoints.Add(pathItem);
            sections.Clear();
            return new EditorPathItem(pathItem.Location, pathItem.Location, PathNodeType.Temporary);
        }

        private TrainPathPoint editorSegmentStart;
        private List<TrainPathSectionBase> sections = new List<TrainPathSectionBase>();

        internal void UpdateLocation(in PointD location)
        {
            if (editorSegmentStart != null)
            {
                TrainPathPoint end = new TrainPathPoint(location, TrackModel);
                editorSegmentStart.ValidationResult = TrainPathPoint.InvalidReasons.None;
                PathSections.RemoveRange(PathSections.Count - sections.Count,  sections.Count);
                sections = AddSections(PathType.MainPath, editorSegmentStart, end, 0);
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
            foreach (EditorPathItem pathItem in PathPoints)
            {
                pathItem.Draw(contentArea, colorVariation, scaleFactor);
            }

            if (SelectedNodeIndex >= 0 && SelectedNodeIndex < PathPoints.Count)
            {
                (PathPoints[SelectedNodeIndex] as EditorPathItem)?.Draw(contentArea, ColorVariation.ComplementHighlight, 5);

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
            throw new System.NotImplementedException();
        }

        protected override TrackSegmentSectionBase<TrainPathSegmentBase> AddSection(TrackModel trackModel, int trackNodeIndex)
        {
            throw new System.NotImplementedException();
        }

        protected override TrainPathItemBase CreateEditorPathItem(in PointD location, in PointD vector, PathNodeType nodeType)
        {
            return new EditorPathItem(location, vector, nodeType);
        }

        protected override TrainPathItemBase CreateEditorPathItem(in PointD location, TrackSegmentBase trackSegment, PathNodeType nodeType, bool reverseDirection)
        {
            return new EditorPathItem(location, trackSegment, nodeType, reverseDirection);
        }
    }
}
