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
        private EditorTrainPath previewTrainPath;

        private bool disposedValue;

        private readonly IPathEditorContext editorContext;
        protected TrackWorld TrackWorld { get; }

        protected TrainPathPointBase ActivePathPoint => activePathPoint;

        protected bool UseStandaloneActivePathPointPreview { get; set; }

        protected PathModel PreviewPathModel { get; private set; }

        protected void SetHiddenPathNodeIndex(int nodeIndex)
        {
            if (trainPath != null)
                trainPath.HiddenNodeIndex = nodeIndex;
        }

        protected bool InitializeActivePathPointPreview(int nodeIndex)
        {
            activePathPoint = trainPath?.CreatePathNodePreview(nodeIndex);
            return activePathPoint != null;
        }

        protected bool TryGetRenderedMainPathSpanAt(in PointD location, double toleranceWorldUnits, out int fromNodeIndex, out PathNode placementAnchor)
        {
            if (trainPath != null)
                return trainPath.TryGetMainPathSpanAt(location, toleranceWorldUnits, out fromNodeIndex, out placementAnchor);

            fromNodeIndex = -1;
            placementAnchor = null;
            return false;
        }

        public TrainPathBase TrainPath
        {
            get => trainPath;
            protected set => trainPath = value as EditorTrainPath;
        }

        /// <summary>Index of the path node selected on the map, or -1 when no node is selected.</summary>
        public int SelectedPathNodeIndex => trainPath?.SelectedNodeIndex ?? -1;

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
            if (trainPath == null)
                return;

            // if a tracksegment is nearby, snap to the segment
            PointD snapLocation = nearestSegment?.SnapToSegment(location) ?? location;
            Runtime.Track.JunctionNodeBase junction;
            if ((junction = TrackWorld.JunctionNodeBaseAt(snapLocation)) != null) //if within junction proximity, snap to the junction
                snapLocation = junction.Location;

            activePathPoint = UseStandaloneActivePathPointPreview
                ? new EditorPathPoint(snapLocation, junction, nearestSegment, TrackWorld)
                : trainPath.UpdatePathEndPoint(snapLocation, junction, nearestSegment);
            OnActivePathPointUpdated();
        }

        internal void Draw()
        {
            (previewTrainPath ?? trainPath)?.Draw(editorContext.Renderer);
            activePathPoint?.Draw(editorContext.Renderer);
        }

        protected void SetPreviewPath(PathModel pathModel)
        {
            PreviewPathModel = pathModel;
            previewTrainPath = pathModel == null
                ? null
                : ((IPathEditorContextServicesAccessor)editorContext).Services.CreateEditorTrainPath(pathModel);
        }

        protected virtual void OnActivePathPointUpdated()
        {
        }

        #region additional content (Paths)
        protected async Task InitializePathModelAsync(PathModelHeader pathModelHeader, CancellationToken cancellationToken = default)
        {
            PathModel pathModel = pathModelHeader as PathModel;
            if (pathModelHeader != null && pathModel == null)
            {
                pathModel = await pathModelHeader.GetExtended(cancellationToken).ConfigureAwait(false);
            }

            EditMode = false;
            trainPath = ((IPathEditorContextServicesAccessor)editorContext).Services.CreateEditorTrainPath(pathModel);
            SetPreviewPath(null);
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
            SetPreviewPath(null);
            activePathPoint = new EditorPathPoint(PointD.None, PointD.None, PathNodeType.Start);
        }

        /// <summary>
        /// Rebuilds the editor from <paramref name="pathModel"/> while preserving the supplied
        /// <paramref name="editMode"/>, used when restoring an undo/redo snapshot or applying a mutation so a
        /// path that was opened for viewing is not silently switched into edit mode. Seeds the active preview
        /// point consistently with the interactive editing path so a subsequent pointer move has a valid anchor.
        /// </summary>
        protected void RestorePath(PathModel pathModel, bool editMode)
        {
            EditMode = editMode;
            editorContext.ContentMode = editMode ? ToolboxContentMode.EditPath : ToolboxContentMode.ViewPath;
            trainPath = ((IPathEditorContextServicesAccessor)editorContext).Services.CreateEditorTrainPath(pathModel);
            SetPreviewPath(null);
            activePathPoint = editMode ? new EditorPathPoint(PointD.None, PointD.None, PathNodeType.Start) : null;
        }

        public PathModel ConvertTrainPath(PathModelHeader pathModelHeader)
        {
            return trainPath?.ToPathModel(pathModelHeader);
        }

        protected bool AddPathEndPoint()
        {
            if (trainPath?.PathPoints.Count > 1 && IsValidActivePathPoint()
                && trainPath.PathPoints[^1] is EditorPathPoint endPoint)
            {
                activePathPoint = endPoint;
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
            return trainPath != null && activePathPoint != null && activePathPoint.ValidationResult == PathNodeInvalidReasons.None && (activePathPoint = trainPath.RemovePathPoint(activePathPoint)) != currentItem;
        }

        private bool IsValidActivePathPoint()
        {
            return activePathPoint != null && activePathPoint.ValidationResult == PathNodeInvalidReasons.None && !activePathPoint.ConnectedSegments.IsDefaultOrEmpty;
        }
        #endregion

        public void HighlightPathItem(int index)
        {
            if (trainPath == null)
                return;

            SelectPathItem(index);
            TrainPathPointBase item = trainPath.SelectedNode;
            if (item != null)
                editorContext.Viewport.SetTrackingPosition(item.Location);
        }

        protected void SelectPathItem(int index)
        {
            if (trainPath != null)
                trainPath.SelectedNodeIndex = index;
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
