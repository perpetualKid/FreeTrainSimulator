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

        /// <summary>Enables pointer updates while a resolver-backed placement is active outside legacy edit mode.</summary>
        protected void ActivatePathPlacementInput()
        {
            editorContext.ContentMode = ToolboxContentMode.EditPath;
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
            if (trainPath == null && !UseStandaloneActivePathPointPreview)
                return;

            // if a tracksegment is nearby, snap to the segment
            PointD snapLocation = nearestSegment?.SnapToSegment(location) ?? location;
            Runtime.Track.JunctionNodeBase junction = null;
            // A selected segment carries the exact vector-node/section identity needed by resolver-backed
            // placement. Replacing it with a generic junction candidate near a junction exposes every connected
            // segment and can abruptly switch the preview to the opposite side of a loop.
            if (nearestSegment == null && (junction = TrackWorld.JunctionNodeBaseAt(snapLocation)) != null)
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

        /// <summary>Initializes an editable path without activating legacy interactive path extension.</summary>
        protected void InitializeAnchorPathEdit(PathModel pathModel)
        {
            EditMode = true;
            editorContext.ContentMode = ToolboxContentMode.EditPath;
            trainPath = ((IPathEditorContextServicesAccessor)editorContext).Services.CreateEditorTrainPath(pathModel);
            SetPreviewPath(null);
            activePathPoint = null;
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

        /// <summary>Clears all runtime path and preview state while retaining an authored model outside the renderer.</summary>
        protected void ClearRuntimePathState(bool editMode)
        {
            EditMode = editMode;
            trainPath = null;
            previewTrainPath = null;
            PreviewPathModel = null;
            activePathPoint = null;
            editorContext.ContentMode = editMode ? ToolboxContentMode.EditPath : ToolboxContentMode.ViewRoute;
        }

        /// <summary>Initializes a track-snapped pointer preview that does not require a rendered path.</summary>
        protected void InitializeStandalonePathPointPreview()
        {
            activePathPoint = new EditorPathPoint(PointD.None, PointD.None, PathNodeType.Start);
        }

        public PathModel ConvertTrainPath(PathModelHeader pathModelHeader)
        {
            return trainPath?.ToPathModel(pathModelHeader);
        }

        protected bool RemovePathPoint()
        {
            EditorPathPoint currentItem = activePathPoint;
            return trainPath != null && activePathPoint != null && activePathPoint.ValidationResult == PathNodeInvalidReasons.None && (activePathPoint = trainPath.RemovePathPoint(activePathPoint)) != currentItem;
        }

        #endregion

        public void HighlightPathItem(int index)
        {
            if (trainPath == null)
                return;

            trainPath.ClearHighlightedSpan();
            SelectPathItem(index);
            TrainPathPointBase item = trainPath.SelectedNode;
            if (item != null)
                editorContext.Viewport.SetTrackingPosition(item.Location);
        }

        protected bool HighlightPathSpan(int fromNodeIndex, int toNodeIndex)
        {
            if (trainPath == null || !trainPath.HighlightMainPathSpan(fromNodeIndex, toNodeIndex))
                return false;

            editorContext.Viewport?.SetTrackingPosition(trainPath.PathPoints[fromNodeIndex].Location);
            return true;
        }

        protected void ClearPathHighlight()
        {
            if (trainPath == null)
                return;

            trainPath.ClearHighlightedSpan();
            SelectPathItem(-1);
        }

        protected void SelectPathItem(int index)
        {
            if (trainPath != null)
            {
                trainPath.ClearHighlightedSpan();
                trainPath.SelectedNodeIndex = index;
            }
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
