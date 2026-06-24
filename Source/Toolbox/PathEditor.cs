using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Toolbox
{
    public class PathEditorChangedEventArgs : EventArgs
    {
        public TrainPathBase Path { get; }

        public PathEditorChangedEventArgs(TrainPathBase path)
        {
            Path = path;
        }
    }

    internal sealed class PathEditor : PathEditorBase
    {
        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Stack<PathModel> undoHistory = new Stack<PathModel>();
        private readonly Stack<PathModel> redoHistory = new Stack<PathModel>();
        private PathModelHeader path;
        private long lastPathClickTick;
        private bool validPointAdded;
        private bool editorDragged;

        public string PathId => path?.Id;

        public bool CanUndo => undoHistory.Count > 0;

        public bool CanRedo => redoHistory.Count > 0;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathChanged;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathUpdated;
        private readonly int doubleClickInterval = System.Windows.Forms.SystemInformation.DoubleClickTime;

        internal PathEditor(IPathEditorContext editorContext) : base(editorContext) { }

        internal PathEditor(IPathEditorContext editorContext, UserCommandController<UserCommand> userCommandController) : base(editorContext)
        {
            this.userCommandController = userCommandController;
            userCommandController.AddEvent(CommonUserCommand.PointerReleased, MouseReleasedLeft);
            userCommandController.AddEvent(CommonUserCommand.AlternatePointerReleased, MouseReleasedRight);
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, MouseDragged);
        }

        protected override void Dispose(bool disposing)
        {
            userCommandController.RemoveEvent(CommonUserCommand.PointerReleased, MouseReleasedLeft);
            userCommandController.RemoveEvent(CommonUserCommand.AlternatePointerReleased, MouseReleasedRight);
            userCommandController.RemoveEvent(CommonUserCommand.PointerDragged, MouseDragged);

            base.Dispose(disposing);
        }

        public bool InitializePath(PathModelHeader path)
        {
            try
            {
                this.path = path;
                if (path != null && !CanInitializePath(path))
                    return false;

                ClearHistory();
                InitializePathModel(path);
                OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                Trace.TraceError($"Failed to initialize path editor: {ex.Message}");
                return false;
            }
        }

        internal static bool CanInitializePath(PathModel pathModel, out PathRouteResolution resolution)
        {
            return CanInitializePath(pathModel, RuntimeDataResolver.Instance.TrackWorld, out resolution);
        }

        internal static bool CanInitializePath(PathModel pathModel, TrackWorld trackWorld, out PathRouteResolution resolution)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            resolution = PathRouteResolver.Resolve(pathModel, trackWorld, CancellationToken.None);
            return resolution.HighestSeverity < PathRouteDiagnosticSeverity.Fatal;
        }

        private static bool CanInitializePath(PathModelHeader path)
        {
            PathModel pathModel = path is PathModel model
                ? model
                : Task.Run(async () => await path.GetExtended(CancellationToken.None).ConfigureAwait(false)).Result;

            if (CanInitializePath(pathModel, out PathRouteResolution resolution))
                return true;

            string diagnostics = string.Join("; ", resolution.Diagnostics
                .Where(diagnostic => diagnostic.Severity == PathRouteDiagnosticSeverity.Fatal)
                .Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
            Trace.TraceWarning($"Path editor cannot open path '{path.Id}' because the path content has fatal route diagnostics. {diagnostics}");
            return false;
        }

        public void InitializeNewPath()
        {
            this.path = new PathModel()
            {
                Id = "<New Path>",
                Name = "<New Path>",
                Start = "Start",
                End = "End",
                PlayerPath = true,
            };
            ClearHistory();
            InitializePathEdit(path as PathModel);
            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;

            PathModel redoSnapshot = TryCaptureSnapshot();
            PathModel undoSnapshot = undoHistory.Pop();
            RestoreSnapshot(undoSnapshot);
            if (redoSnapshot != null)
                redoHistory.Push(redoSnapshot);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
                return false;

            PathModel undoSnapshot = TryCaptureSnapshot();
            PathModel redoSnapshot = redoHistory.Pop();
            RestoreSnapshot(redoSnapshot);
            if (undoSnapshot != null)
                undoHistory.Push(undoSnapshot);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

        public async Task SavePath(PathModelHeader pathDetails)
        {
            PathModel pathModel = ConvertTrainPath(pathDetails);
            //TODO 2026-03-26 This needs to be a GameInstance, not just Instance
            pathModel = await RuntimeDataResolver.Instance.RouteData.Save(pathModel).ConfigureAwait(false);
            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        public void MouseDragged(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            editorDragged = true;
        }

        public void MouseReleasedLeft(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (EditMode & !editorDragged)
            {
                PathModel undoSnapshot = TryCaptureSnapshot();
                bool changed;
                if (Environment.TickCount64 - lastPathClickTick < doubleClickInterval && validPointAdded) //considered as double click
                {
                    changed = AddPathEndPoint();
                }
                else
                {
                    changed = AddPathPoint();
                    validPointAdded = changed;
                }
                if (changed)
                    PushUndoSnapshot(undoSnapshot);
                lastPathClickTick = Environment.TickCount64;
                OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
                userCommandArgs.Handled = true;
            }
            editorDragged = false;
        }

        public void MouseReleasedRight(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            PathModel undoSnapshot = TryCaptureSnapshot();
            if (RemovePathPoint())
                PushUndoSnapshot(undoSnapshot);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            userCommandArgs.Handled = true;
        }

        private void ClearHistory()
        {
            undoHistory.Clear();
            redoHistory.Clear();
        }

        private PathModel TryCaptureSnapshot()
        {
            try
            {
                return TrainPath == null || path == null ? null : ConvertTrainPath(path);
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceWarning($"Cannot capture train path undo snapshot because the current path is invalid: {ex.Message}. {BuildSnapshotContext()}");
                return null;
            }
        }

        private string BuildSnapshotContext()
        {
            int pointCount = TrainPath?.PathPoints.Count ?? 0;
            string invalidPoints = TrainPath == null
                ? "none"
                : string.Join(", ", TrainPath.PathPoints
                    .Select((point, index) => new { Point = point, Index = index })
                    .Where(item => item.Point.ValidationResult != PathNodeInvalidReasons.None || item.Point.ConnectedSegments.IsDefaultOrEmpty)
                    .Take(5)
                    .Select(item => $"#{item.Index}:{item.Point.NodeType}:{FormatInvalidPointState(item.Point)}"));

            if (string.IsNullOrEmpty(invalidPoints))
                invalidPoints = "none";

            return $"PathId='{path?.Id ?? "<none>"}', PathName='{path?.Name ?? "<none>"}', EditMode={EditMode}, "
                + $"PointCount={pointCount}, InvalidPoints={invalidPoints}, CanUndo={CanUndo}, CanRedo={CanRedo}, "
                + $"ValidPointAdded={validPointAdded}, EditorDragged={editorDragged}";
        }

        private static string FormatInvalidPointState(TrainPathPointBase point)
        {
            string validation = point.ValidationResult == PathNodeInvalidReasons.None ? "None" : point.ValidationResult.ToString();
            string connectedSegments = point.ConnectedSegments.IsDefault
                ? "DefaultSegments"
                : $"Segments={point.ConnectedSegments.Length}";

            return $"{validation}/{connectedSegments}";
        }

        private void PushUndoSnapshot(PathModel snapshot)
        {
            if (snapshot == null)
                return;

            undoHistory.Push(snapshot);
            redoHistory.Clear();
        }

        private void RestoreSnapshot(PathModel snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            path = snapshot;
            InitializePathEdit(snapshot);
            validPointAdded = false;
            editorDragged = false;
        }
    }
}
