﻿using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Track;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Graphics.MapView
{
    public abstract class PathEditorBase : IDisposable
    {
        private EditorTrainPath trainPath;
        private EditorPathPoint activePathPoint;

        private bool disposedValue;

        protected ToolboxContent ToolboxContent { get; }
        protected TrackModel TrackModel { get; }

        public TrainPathBase TrainPath
        {
            get => trainPath;
            protected set => trainPath = value as EditorTrainPath;
        }

        public bool EditMode { get; private set; }

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
                snapLocation = junction.Location;

            activePathPoint = trainPath.UpdatePathEndPoint(snapLocation, junction, nearestSegment);
        }

        internal void Draw()
        {
            trainPath?.Draw(ToolboxContent.ContentArea);
            activePathPoint?.Draw(ToolboxContent.ContentArea);
        }

        #region additional content (Paths)
        protected void InitializePathModel(PathModelHeader pathModel)
        {
            EditMode = false;
            trainPath = pathModel != null ? new EditorTrainPath(Task.Run(async () => await pathModel.GetExtended(CancellationToken.None).ConfigureAwait(false)).Result, ToolboxContent.ContentArea.Game) : null;
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

        protected void InitializePathEdit(PathModel pathModel)
        {
            EditMode = true;
            ToolboxContent.ContentMode = ToolboxContentMode.EditPath;
            trainPath = new EditorTrainPath(pathModel, ToolboxContent.ContentArea.Game);
            activePathPoint = new EditorPathPoint(PointD.None, PointD.None, PathNodeType.Start);
        }

        public PathModel ConvertTrainPath(PathModelHeader pathModelHeader)
        {
            return trainPath?.ToPathModel(pathModelHeader);
        }

        protected bool AddPathEndPoint()
        {
            if (trainPath?.PathPoints.Count > 1 && activePathPoint.ValidationResult == PathNodeInvalidReasons.None)
            {
                activePathPoint = trainPath.PathPoints[^1] as EditorPathPoint;
                activePathPoint.UpdateDirectionTowards(trainPath.PathPoints[^2], true, true);
                trainPath.PathPoints[^1] = activePathPoint with { NodeType = PathNodeType.End };

                activePathPoint = null;
                ToolboxContent.ContentMode = ToolboxContentMode.ViewPath;
                EditMode = false;

                return true;
            }
            return false;
        }

        protected bool AddPathPoint()
        {
            EditorPathPoint currentItem = activePathPoint;
            return trainPath != null && activePathPoint.ValidationResult == PathNodeInvalidReasons.None && (activePathPoint = trainPath.AddPathPoint(activePathPoint)) != currentItem;
        }

        protected bool RemovePathPoint()
        {
            EditorPathPoint currentItem = activePathPoint;
            return trainPath != null && activePathPoint.ValidationResult == PathNodeInvalidReasons.None && (activePathPoint = trainPath.RemovePathPoint(activePathPoint)) != currentItem;
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
                    ToolboxContent.PathEditor = null;
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
