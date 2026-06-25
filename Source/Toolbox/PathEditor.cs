using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

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
            PathModel newPath = new PathModel()
            {
                Id = "<New Path>",
                Name = "<New Path>",
                Start = "Start",
                End = "End",
                PlayerPath = true,
            };
            path = newPath;
            ClearHistory();
            InitializePathEdit(newPath);
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

        #region core editing commands
        // Command enablement derived from the current authored path. Each property is cheap and snapshot-driven,
        // so the tool window can poll it to gate its command UI. A null/unconvertible path disables everything.

        /// <summary><see langword="true"/> when the path has nodes but no start node yet.</summary>
        public bool CanAddStart => HasNodeType(snapshot => snapshot.PathNodes.Length > 0 && !HasFlag(snapshot, PathNodeType.Start));

        /// <summary><see langword="true"/> when the path has a start node to remove.</summary>
        public bool CanRemoveStart => HasNodeType(snapshot => HasFlag(snapshot, PathNodeType.Start));

        /// <summary><see langword="true"/> when the path has a start but its last node is not yet an end node.</summary>
        public bool CanAddEnd => HasNodeType(snapshot => snapshot.PathNodes.Length > 0 && HasFlag(snapshot, PathNodeType.Start)
            && (snapshot.PathNodes[^1].NodeType & PathNodeType.End) != PathNodeType.End);

        /// <summary><see langword="true"/> when the path has an end node to remove.</summary>
        public bool CanRemoveEnd => HasNodeType(snapshot => HasFlag(snapshot, PathNodeType.End));

        /// <summary>Adds a start node to the current path and records an undo snapshot. Returns the operation result.</summary>
        public PathEditResult AddStart() => ApplyMutation(PathModelEditor.AddStart);

        /// <summary>Removes the start node from the current path and records an undo snapshot. Returns the operation result.</summary>
        public PathEditResult RemoveStart() => ApplyMutation(PathModelEditor.RemoveStart);

        /// <summary>Adds an end node to the current path and records an undo snapshot. Returns the operation result.</summary>
        public PathEditResult AddEnd() => ApplyMutation(PathModelEditor.AddEnd);

        /// <summary>Removes the end node from the current path and records an undo snapshot. Returns the operation result.</summary>
        public PathEditResult RemoveEnd() => ApplyMutation(PathModelEditor.RemoveEnd);

        /// <summary>
        /// Truncates the path after the node at <paramref name="nodeIndex"/>, marking it as the new end, and
        /// records an undo snapshot. Returns the operation result.
        /// </summary>
        public PathEditResult RemoveRestOfPath(int nodeIndex) => ApplyMutation(model => PathModelEditor.RemoveRestOfPath(model, nodeIndex));

        // Captures the current authored path, runs the mutation, and on success records an undo snapshot and
        // rebuilds the editor from the new model. Returns a failed result (with a reason) when the editor is not
        // in edit mode, no path is currently loaded, or the mutation reports failure.
        private PathEditResult ApplyMutation(Func<PathModel, PathEditResult> mutation)
        {
            if (!EditMode)
                return PathEditResult.Failed("The path is not in edit mode.", null);

            PathModel currentModel = TryCaptureSnapshot();
            if (currentModel == null)
                return PathEditResult.Failed("No editable path is currently loaded.", null);

            PathEditResult result = mutation(currentModel);
            if (!result.Success)
                return result;

            PushUndoSnapshot(currentModel);
            RestoreSnapshot(result.PathModel);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return result;
        }

        private bool HasNodeType(Func<PathModel, bool> predicate)
        {
            if (!EditMode)
                return false;

            PathModel currentModel = TryCaptureSnapshot();
            return currentModel != null && predicate(currentModel);
        }

        private static bool HasFlag(PathModel pathModel, PathNodeType nodeType)
        {
            foreach (PathNode node in pathModel.PathNodes)
            {
                if ((node.NodeType & nodeType) == nodeType)
                    return true;
            }
            return false;
        }
        #endregion

        public async Task SavePath(PathModelHeader pathDetails)
        {
            PathModel pathModel = ConvertTrainPath(pathDetails);
            // The toolbox registers RuntimeDataResolver process-wide only (RuntimeDataResolver.Initialize
            // passes game: null), so Instance is the single authoritative resolver here; a game-scoped
            // GameInstance(game) lookup would resolve to the same object.
            pathModel = await RuntimeDataResolver.Instance.RouteData.Save(pathModel).ConfigureAwait(false);
            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        public void MouseDragged(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            editorDragged = true;
        }

        public void MouseReleasedLeft(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (EditMode && !editorDragged)
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
            if (!EditMode)
                return;

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
            return BuildSnapshotContext(path, TrainPath, EditMode, CanUndo, CanRedo, validPointAdded, editorDragged);
        }

        internal static string BuildSnapshotContext(PathModelHeader path, TrainPathBase trainPath, bool editMode, bool canUndo, bool canRedo, bool validPointAdded, bool editorDragged)
        {
            int pointCount = trainPath?.PathPoints.Count ?? 0;
            string invalidPoints = trainPath == null
                ? "none"
                : string.Join(", ", trainPath.PathPoints
                    .Select((point, index) => new { Point = point, Index = index })
                    .Where(item => item.Point.ValidationResult != PathNodeInvalidReasons.None || item.Point.ConnectedSegments.IsDefaultOrEmpty)
                    .Take(5)
                    .Select(item => $"#{item.Index}:{item.Point.NodeType}:{FormatInvalidPointState(item.Point)}"));

            if (string.IsNullOrEmpty(invalidPoints))
                invalidPoints = "none";

            return $"PathId='{path?.Id ?? "<none>"}', PathName='{path?.Name ?? "<none>"}', EditMode={editMode}, "
                + $"PointCount={pointCount}, InvalidPoints={invalidPoints}, CanUndo={canUndo}, CanRedo={canRedo}, "
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

            // Preserve the current View/Edit mode across the rebuild: undoing/redoing or mutating must not
            // silently switch a path that was opened for viewing into edit mode.
            path = snapshot;
            RestorePath(snapshot, EditMode);
            validPointAdded = false;
            editorDragged = false;
        }
    }
}
