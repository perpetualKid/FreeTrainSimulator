using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Graphics.MapView
{
    public abstract class PathEditorBase : IDisposable
    {
        private EditorTrainPath trainPath;
        private EditorPathPoint activePathPoint;

        private bool disposedValue;

        private readonly IPathEditorContext editorContext;
        protected TrackWorld TrackWorld { get; }

        public TrainPathBase TrainPath
        {
            get => trainPath;
            protected set => trainPath = value as EditorTrainPath;
        }

        public bool EditMode { get; private set; }

        protected PathEditorBase(IPathEditorContext editorContext)
        {
            ArgumentNullException.ThrowIfNull(editorContext);

            this.editorContext = editorContext;
            IPathEditorServices services = ((IPathEditorContextServicesAccessor)editorContext).Services;
            TrackWorld = services.TrackWorld;
            this.editorContext.PathEditor = this;
        }

        internal void UpdatePointerLocation(in PointD location, TrackSegmentBase nearestSegment)
        {
            // if a tracksegment is nearby, snap to the segment
            PointD snapLocation = nearestSegment?.SnapToSegment(location) ?? location;
            Runtime.Track.JunctionNodeBase junction;
            if ((junction = TrackWorld.JunctionNodeBaseAt(snapLocation)) != null) //if within junction proximity, snap to the junction
                snapLocation = junction.Location;

            activePathPoint = trainPath.UpdatePathEndPoint(snapLocation, junction, nearestSegment);
        }

        internal void Draw()
        {
            trainPath?.Draw(editorContext.Renderer);
            activePathPoint?.Draw(editorContext.Renderer);
        }

        #region additional content (Paths)
        protected void InitializePathModel(PathModelHeader pathModel)
        {
            EditMode = false;
            trainPath = ((IPathEditorContextServicesAccessor)editorContext).Services.CreateEditorTrainPath(pathModel);
            if (trainPath != null && trainPath.TopLeftBound != PointD.None && trainPath.BottomRightBound != PointD.None)
            {
                editorContext.Viewport?.UpdateScaleToFit(trainPath.TopLeftBound, trainPath.BottomRightBound);
                editorContext.Viewport?.SetTrackingPosition(trainPath.MidPoint);
                editorContext.ContentMode = ToolboxContentMode.ViewPath;
            }
            else
            {
                editorContext.ContentMode = ToolboxContentMode.ViewRoute;
            }
        }

        protected void InitializePathEdit(PathModel pathModel)
        {
            EditMode = true;
            editorContext.ContentMode = ToolboxContentMode.EditPath;
            trainPath = ((IPathEditorContextServicesAccessor)editorContext).Services.CreateEditorTrainPath(pathModel);
            activePathPoint = new EditorPathPoint(PointD.None, PointD.None, PathNodeType.Start);
        }

        public PathModel ConvertTrainPath(PathModelHeader pathModelHeader)
        {
            return trainPath?.ToPathModel(pathModelHeader);
        }

        protected bool AddPathEndPoint()
        {
            if (trainPath?.PathPoints.Count > 1 && IsValidActivePathPoint())
            {
                activePathPoint = trainPath.PathPoints[^1] as EditorPathPoint;
                activePathPoint.UpdateDirectionTowards(trainPath.PathPoints[^2], true, true);
                trainPath.PathPoints[^1] = activePathPoint with { NodeType = PathNodeType.End };

                activePathPoint = null;
                editorContext.ContentMode = ToolboxContentMode.ViewPath;
                EditMode = false;

                return true;
            }
            return false;
        }

        protected bool AddPathPoint()
        {
            EditorPathPoint currentItem = activePathPoint;
            return trainPath != null && IsValidActivePathPoint() && (activePathPoint = trainPath.AddPathPoint(activePathPoint)) != currentItem;
        }

        protected bool RemovePathPoint()
        {
            EditorPathPoint currentItem = activePathPoint;
            return trainPath != null && activePathPoint.ValidationResult == PathNodeInvalidReasons.None && (activePathPoint = trainPath.RemovePathPoint(activePathPoint)) != currentItem;
        }

        private bool IsValidActivePathPoint()
        {
            return activePathPoint != null && activePathPoint.ValidationResult == PathNodeInvalidReasons.None && !activePathPoint.ConnectedSegments.IsDefaultOrEmpty;
        }
        #endregion

        public void HighlightPathItem(int index)
        {
            trainPath.SelectedNodeIndex = index;
            TrainPathPointBase item = trainPath.SelectedNode;
            if (item != null)
                editorContext.Viewport.SetTrackingPosition(item.Location);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    editorContext.PathEditor = null;
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
