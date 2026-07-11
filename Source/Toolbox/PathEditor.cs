using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
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
        private int movingNodeIndex = -1;
        private PathModel moveSourceModel;
        private PathModel movePreviewModel;
        private PathNode movePreviewAnchor;

        public string PathId => path?.Id;

        public bool CanUndo => undoHistory.Count > 0;

        public bool CanRedo => redoHistory.Count > 0;

        public bool IsMovingNode => movingNodeIndex >= 0;

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

                ClearMoveNodeState();
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

        // Resolves the model against the track and reports its validation state. A path is Valid when it resolves
        // without error or fatal diagnostics; warnings (e.g. ambiguity) are still considered valid, matching
        // PathRouteResolution.IsValid. This is the single source of truth for the persisted PathModelHeader.ValidationState.
        internal static PathValidationState ResolveValidationState(PathModel pathModel, TrackWorld trackWorld)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            PathRouteResolution resolution = PathRouteResolver.Resolve(pathModel, trackWorld, CancellationToken.None);
            return resolution.HighestSeverity < PathRouteDiagnosticSeverity.Error ? PathValidationState.Valid : PathValidationState.Invalid;
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
            ClearMoveNodeState();
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

        /// <summary>
        /// <see langword="true"/> when the current path can be snapped to track: it is in edit mode and has a
        /// start node. Passing branches are woven back into the generated path where they rejoin the main route;
        /// shapes the generator cannot represent are reported when the snap is attempted.
        /// </summary>
        public bool CanSnapToTrack => HasNodeType(snapshot => snapshot.PathNodes.Length > 0
            && HasFlag(snapshot, PathNodeType.Start));

        /// <summary>Adds a start node to the current path and records an undo snapshot. Returns the operation result.</summary>
        public PathEditResult AddStart() => ApplyUndoableEdit(PathModelEditor.AddStart);

        /// <summary>Removes the start node from the current path and records an undo snapshot. Returns the operation result.</summary>
        public PathEditResult RemoveStart() => ApplyUndoableEdit(PathModelEditor.RemoveStart);

        /// <summary>Adds an end node to the current path and records an undo snapshot. Returns the operation result.</summary>
        public PathEditResult AddEnd() => ApplyUndoableEdit(PathModelEditor.AddEnd);

        /// <summary>Removes the end node from the current path and records an undo snapshot. Returns the operation result.</summary>
        public PathEditResult RemoveEnd() => ApplyUndoableEdit(PathModelEditor.RemoveEnd);

        /// <summary>
        /// Truncates the path after the node at <paramref name="nodeIndex"/>, marking it as the new end, and
        /// records an undo snapshot. Returns the operation result.
        /// </summary>
        public PathEditResult RemoveRestOfPath(int nodeIndex) => ApplyUndoableEdit(model => PathModelEditor.RemoveRestOfPath(model, nodeIndex));

        /// <summary>
        /// Resolves the current authored path and rebuilds it along the resolved track anchors, weaving any
        /// resolved passing branches back into the generated graph, and records an undo snapshot. Refuses paths
        /// that the resolver cannot resolve or whose passing shapes the generator cannot represent; the reason is
        /// returned in the result.
        /// </summary>
        public PathEditResult SnapToTrack()
            => ApplyUndoableEdit(model => SnapPathToTrack(model, RuntimeDataResolver.Instance.TrackWorld));

        public bool CanMoveNode(int nodeIndex)
        {
            PathModel currentModel = TryCaptureSnapshot();
            ImmutableArray<PathNode> nodes = currentModel?.PathNodes ?? ImmutableArray<PathNode>.Empty;
            return nodeIndex >= 0 && nodeIndex < nodes.Length;
        }

        public bool BeginMoveNode(int nodeIndex)
        {
            PathModel currentModel = TryCaptureSnapshot();
            ImmutableArray<PathNode> nodes = currentModel?.PathNodes ?? ImmutableArray<PathNode>.Empty;
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return false;

            if (!EditMode)
            {
                path = currentModel;
                RestorePath(currentModel, true);
            }

            movingNodeIndex = nodeIndex;
            moveSourceModel = currentModel;
            movePreviewModel = null;
            movePreviewAnchor = null;
            UseStandaloneActivePathPointPreview = true;
            SetHiddenPathNodeIndex(nodeIndex);
            SelectPathItem(nodeIndex);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

        protected override void OnActivePathPointUpdated()
        {
            if (IsMovingNode)
                UpdateMovePreview();
        }

        private void UpdateMovePreview()
        {
            if (moveSourceModel == null)
            {
                ClearMovePreview();
                return;
            }

            TrainPathPointBase candidate = ActivePathPoint;
            if (candidate == null || candidate.ValidationResult != PathNodeInvalidReasons.None || candidate.ConnectedSegments.IsDefaultOrEmpty)
            {
                ClearMovePreview();
                return;
            }

            PathNode replacementAnchor = CreateReplacementAnchor(candidate);
            if (EquivalentMoveAnchor(movePreviewAnchor, replacementAnchor))
                return;

            bool isJunction = candidate.JunctionNode != null || (candidate.NodeType & PathNodeType.Junction) == PathNodeType.Junction;
            PathEditResult result = PathModelEditor.MoveNode(moveSourceModel, movingNodeIndex, replacementAnchor, isJunction);
            if (!result.Success)
            {
                ClearMovePreview();
                return;
            }

            movePreviewAnchor = replacementAnchor;
            movePreviewModel = result.PathModel;
            SetPreviewPath(movePreviewModel);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        private void ClearMovePreview()
        {
            movePreviewAnchor = null;
            movePreviewModel = null;
            SetPreviewPath(null);
        }

        public bool CancelMoveNode()
        {
            if (!IsMovingNode)
                return false;

            ClearMoveNodeState();
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

        // Captures the current authored path, runs the edit, and on success records an undo snapshot and
        // rebuilds the editor from the new model. Returns a failed result (with a reason) when the editor is not
        // in edit mode, no path is currently loaded, or the edit reports failure.
        private PathEditResult ApplyUndoableEdit(Func<PathModel, PathEditResult> edit)
        {
            if (!EditMode)
                return PathEditResult.Failed("The path is not in edit mode.", null);

            PathModel currentModel = TryCaptureSnapshot();
            if (currentModel == null)
                return PathEditResult.Failed("No editable path is currently loaded.", null);

            PathEditResult result = edit(currentModel);
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

        // Resolves the model and rebuilds its route along track anchors, weaving any resolved passing branches
        // back into the generated graph. On resolver/generation failure (including passing shapes the generator
        // cannot represent, such as non-rejoining branches) the original model is returned unchanged with the reason.
        internal static PathEditResult SnapPathToTrack(PathModel model, TrackWorld trackWorld)
        {
            PathGenerationResult generation = GenerateTrackSnappedPath(model, trackWorld);
            return generation.Success
                ? PathEditResult.Succeeded(generation.Message, generation.PathModel, generation.ChangedNodeIndexes)
                : PathEditResult.Failed(generation.Message, model);
        }

        // Resolves the model with default options and rebuilds its route via the generator, populating track
        // anchors, inserting generated intermediary nodes, and weaving resolved passing branches. Shared by
        // SnapToTrack and snap-on-save.
        internal static PathGenerationResult GenerateTrackSnappedPath(PathModel model, TrackWorld trackWorld)
        {
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, trackWorld, PathRouteResolverOptions.Default, CancellationToken.None);
            return PathModelRouteGenerator.GeneratePath(model, resolution, trackWorld, PathRouteResolverOptions.Default);
        }
        #endregion

        public async Task SavePath(PathModelHeader pathDetails)
        {
            PathModel pathModel = ConvertTrainPath(pathDetails);

            // Editor-saved paths are always normalized to track (guard-and-refuse preserves passing-branch paths).
            pathModel = TrySnapForSave(pathModel);

            // Stamp the validation state from the model that will actually be persisted so the header is self-describing.
            pathModel = pathModel with { ValidationState = ResolveValidationState(pathModel, RuntimeDataResolver.Instance.TrackWorld) };

            // The toolbox registers RuntimeDataResolver process-wide only (RuntimeDataResolver.Initialize
            // passes game: null), so Instance is the single authoritative resolver here; a game-scoped
            // GameInstance(game) lookup would resolve to the same object.
            pathModel = await RuntimeDataResolver.Instance.RouteData.Save(pathModel).ConfigureAwait(false);
            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        /// <summary>
        /// Validates the paths of <paramref name="routeModel"/> against <paramref name="trackWorld"/> and persists
        /// the resulting <see cref="PathModelHeader.ValidationState"/>. When <paramref name="forceRevalidate"/> is
        /// <see langword="false"/> only paths that have never been validated
        /// (<see cref="PathValidationState.NotValidated"/>) are resolved, which keeps the common route-open case
        /// cheap; when <see langword="true"/> every path is re-resolved (used by the explicit "validate all paths"
        /// command and after a route may have changed). Each path is persisted only when its computed state differs
        /// from the stored value. Returns the number of paths found to be invalid.
        /// </summary>
        internal static async Task<int> ValidateRoutePaths(RouteModelHeader routeModel, TrackWorld trackWorld, bool forceRevalidate, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel);

            ImmutableArray<PathModelHeader> headers = await routeModel.GetRoutePaths(cancellationToken).ConfigureAwait(false);
            int invalidCount = 0;
            int revalidatedCount = 0;

            foreach (PathModelHeader header in headers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!forceRevalidate && header.ValidationState != PathValidationState.NotValidated)
                {
                    if (header.ValidationState == PathValidationState.Invalid)
                        invalidCount++;
                    continue;
                }

                PathModel pathModel = await header.GetExtended(cancellationToken).ConfigureAwait(false);
                if (pathModel == null)
                    continue;

                PathValidationState state = ResolveValidationState(pathModel, trackWorld);
                if (state == PathValidationState.Invalid)
                    invalidCount++;

                if (pathModel.ValidationState != state)
                {
                    _ = await routeModel.Save(pathModel with { ValidationState = state }).ConfigureAwait(false);
                    revalidatedCount++;
                }
            }

            if (revalidatedCount > 0)
                Trace.TraceInformation($"Validated {revalidatedCount} path(s) for route '{routeModel.Id}'; {invalidCount} invalid.");

            return invalidCount;
        }

        // Attempts to snap the model to track before saving. Passing branches that rejoin the main route are
        // woven into the generated path; paths that fail to resolve or whose passing shapes the generator cannot
        // represent are saved as authored (never dropping data), with the reason traced.
        private static PathModel TrySnapForSave(PathModel pathModel)
        {
            PathGenerationResult snapped = GenerateTrackSnappedPath(pathModel, RuntimeDataResolver.Instance.TrackWorld);
            if (snapped.Success)
                return snapped.PathModel;

            Trace.TraceWarning($"Snap-to-track on save skipped for path '{pathModel.Id}': {snapped.Message}");
            return pathModel;
        }

        public void MouseDragged(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            editorDragged = true;
        }

        public void MouseReleasedLeft(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (IsMovingNode)
            {
                _ = CommitMoveNode();
                userCommandArgs.Handled = true;
                editorDragged = false;
                return;
            }

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

        private PathEditResult CommitMoveNode()
        {
            TrainPathPointBase candidate = ActivePathPoint;
            if (candidate == null || candidate.ValidationResult != PathNodeInvalidReasons.None || candidate.ConnectedSegments.IsDefaultOrEmpty)
                return PathEditResult.Failed("Select a valid track location for the node.", TryCaptureSnapshot());

            PathModel currentModel = TryCaptureSnapshot();
            if (currentModel == null)
                return PathEditResult.Failed("No editable path is currently loaded.", null);

            PathModel committedModel = movePreviewModel;
            PathEditResult result;
            if (committedModel == null)
            {
                PathNode replacementAnchor = CreateReplacementAnchor(candidate);
                bool isJunction = candidate.JunctionNode != null || (candidate.NodeType & PathNodeType.Junction) == PathNodeType.Junction;
                result = PathModelEditor.MoveNode(currentModel, movingNodeIndex, replacementAnchor, isJunction);
                if (!result.Success)
                    return result;

                committedModel = result.PathModel;
            }

            int movedNodeIndex = movingNodeIndex;
            PushUndoSnapshot(currentModel);
            ClearMoveNodeState();
            path = committedModel;
            RestorePath(committedModel, false);
            SelectPathItem(movedNodeIndex);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return PathEditResult.Succeeded($"Moved node {movedNodeIndex}.", committedModel, ImmutableArray.Create(movedNodeIndex));
        }

        private static PathNode CreateReplacementAnchor(TrainPathPointBase candidate)
        {
            int trackNodeIndex = !candidate.ConnectedSegments.IsDefaultOrEmpty
                ? candidate.ConnectedSegments[0].TrackNodeIndex
                : candidate.NearestTrackDistance?.TrackNodeIndex ?? candidate.NodeIndex;

            return new PathNode(PointD.ToWorldLocation(candidate.Location))
            {
                NodeIndex = trackNodeIndex,
            };
        }

        internal static bool EquivalentMoveAnchor(PathNode first, PathNode second)
        {
            if (ReferenceEquals(first, second))
                return true;
            if (first == null || second == null)
                return false;

            return first.NodeIndex == second.NodeIndex && first.Location == second.Location;
        }

        private void ClearMoveNodeState()
        {
            movingNodeIndex = -1;
            moveSourceModel = null;
            ClearMovePreview();
            UseStandaloneActivePathPointPreview = false;
            SetHiddenPathNodeIndex(-1);
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

        internal PathModel TryCaptureCurrentPathModel()
        {
            return PreviewPathModel ?? TryCaptureSnapshot();
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
            ClearMoveNodeState();
            validPointAdded = false;
            editorDragged = false;
        }
    }
}
