using System;

using FreeTrainSimulator.Common.Position;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Graphics.MapView.Widgets;
using Orts.Models.Track;

namespace Orts.Graphics.MapView
{
    public abstract class PathEditorBase : IDisposable
    {
        private EditorTrainPath trainPath;
        private EditorPathPoint pathItem;

        private bool disposedValue;

        protected ToolboxContent ToolboxContent { get; }
        protected TrackModel TrackModel { get; }

        public TrainPathBase TrainPath
        {
            get => trainPath;
            protected set => trainPath = value as EditorTrainPath;
        }

        protected PathEditorBase(ContentArea contentArea)
        {
            ArgumentNullException.ThrowIfNull(contentArea);

            TrackModel = TrackModel.Instance(contentArea.Game);
            ToolboxContent = contentArea?.Content as ToolboxContent ?? throw new ArgumentNullException(nameof(contentArea));
            ToolboxContent.PathEditor = this;
        }

        internal void UpdatePointerLocation(in PointD location, TrackSegmentBase nearestSegment)
        {
            // if a tracksegment is nearby, snap to the segment
            PointD snapLocation = nearestSegment?.SnapToSegment(location) ?? location;
            JunctionNodeBase junction;
            if ((junction = TrackModel.JunctionAt(snapLocation)) != null) //if within junction proximity, snap to the junction
            {
                snapLocation = junction.Location;
            }
            pathItem.UpdateLocation(snapLocation, nearestSegment != null);
            trainPath.UpdateLocation(snapLocation);
            if (trainPath.PathPoints.Count > 0)
            {
                (trainPath.PathPoints[^1] as EditorPathPoint).UpdateDirection(snapLocation);
            }
        }

        internal void Draw()
        {
            trainPath?.Draw(ToolboxContent.ContentArea);
            pathItem?.Draw(ToolboxContent.ContentArea);
        }

        #region additional content (Paths)
        public void InitializePath(PathFile path, string filePath)
        {
            trainPath = path != null ? new EditorTrainPath(path, filePath, ToolboxContent.ContentArea.Game) : null;
            if (trainPath != null && trainPath.TopLeftBound != PointD.None && trainPath.BottomRightBound != PointD.None)
            {
                ToolboxContent.ContentArea?.UpdateScaleToFit(trainPath.TopLeftBound, trainPath.BottomRightBound);
                ToolboxContent.ContentArea?.SetTrackingPosition(trainPath.MidPoint);
                ToolboxContent.ContentMode = ToolboxContentMode.ViewPath;
            }
            else
            {
                ToolboxContent.ContentMode = ToolboxContentMode.ViewRoute;
            }
        }

        protected void InitializePath()
        {
            ToolboxContent.ContentMode = ToolboxContentMode.EditPath;
            trainPath = new EditorTrainPath(ToolboxContent.ContentArea.Game);
            pathItem = new EditorPathPoint(PointD.None, PointD.None, Formats.Msts.PathNodeType.Start);
        }

        protected bool AddPathEndPoint()
        {
            if (trainPath?.PathPoints.Count > 1 && pathItem.ValidationResult == PathNodeInvalidReasons.None)
            {
                (trainPath.PathPoints[^1] as EditorPathPoint).UpdateDirection(trainPath.PathPoints[^2].Location);
                (trainPath.PathPoints[^1] as EditorPathPoint).UpdateNodeType(Formats.Msts.PathNodeType.End);
                pathItem = null;
                ToolboxContent.ContentMode = ToolboxContentMode.ViewPath;
                return true;
            }
            return false;
        }

        protected bool AddPathPoint()
        {
            EditorPathPoint currentItem = pathItem;
            return trainPath != null && pathItem.ValidationResult == PathNodeInvalidReasons.None && (pathItem = trainPath.AddPathPoint(pathItem)) != currentItem;
        }
        #endregion

        public void HighlightPathItem(int index)
        {
            trainPath.SelectedNodeIndex = index;
            TrainPathPointBase item = trainPath.SelectedNode;
            if (item != null)
                ToolboxContent.ContentArea.SetTrackingPosition(item.Location);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ToolboxContent.PathEditor = null;
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
