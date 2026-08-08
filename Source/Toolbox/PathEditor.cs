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
        // One resolution per path model instance, shared by the persisted validation state and by consumers such
        // as the train path tool window, so an edit resolves the path exactly once.
        private readonly PathRouteResolutionCache resolutionCache = new PathRouteResolutionCache();
        private PathModelHeader path;
        private PathModel currentPathModel;
        private long lastPathClickTick;
        private bool validPointAdded;
        private bool editorDragged;
        private int movingNodeIndex = -1;
        private PathModel moveSourceModel;
        private PathModel movePreviewModel;
        private PathNode movePreviewAnchor;
        private int pendingViaNodeIndex = -1;
        private PathModel pendingViaSourceModel;
        private bool pendingViaSourceEditMode;
        private bool pendingViaSourceUnsavedChanges;
        private bool unsavedChanges;

        public string PathId => path?.Id;

        public bool CanUndo => undoHistory.Count > 0;

        public bool CanRedo => redoHistory.Count > 0;

        public bool IsMovingNode => movingNodeIndex >= 0;

        public bool CanCommitMoveNode => IsMovingNode && movePreviewModel != null;

        public bool HasUnsavedChanges => unsavedChanges;

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
            userCommandController?.RemoveEvent(CommonUserCommand.PointerReleased, MouseReleasedLeft);
            userCommandController?.RemoveEvent(CommonUserCommand.AlternatePointerReleased, MouseReleasedRight);
            userCommandController?.RemoveEvent(CommonUserCommand.PointerDragged, MouseDragged);

            base.Dispose(disposing);
        }

        public async Task<bool> InitializePathAsync(PathModelHeader path, CancellationToken cancellationToken = default)
        {
            try
            {
                PathModel pathModel = path as PathModel;
                if (path != null && pathModel == null)
                {
                    pathModel = await path.GetExtended(cancellationToken).ConfigureAwait(false);
                }

                this.path = pathModel ?? path;
                currentPathModel = pathModel;

                if (pathModel != null && !CanInitializePath(pathModel, out PathRouteResolution resolution))
                {
                    string diagnostics = string.Join("; ", resolution.Diagnostics
                        .Where(diagnostic => diagnostic.Severity == PathRouteDiagnosticSeverity.Fatal)
                        .Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
                    Trace.TraceWarning($"Path editor cannot open path '{path.Id}' because the path content has fatal route diagnostics. {diagnostics}");
                    return false;
                }

                ClearMoveNodeState();
                ClearHistory();
                await InitializePathModelAsync(pathModel, cancellationToken).ConfigureAwait(false);
                currentPathModel = pathModel;
                unsavedChanges = false;
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

            return ResolveValidationState(PathRouteResolver.Resolve(pathModel, trackWorld, CancellationToken.None));
        }

        // Maps an already computed resolution onto the persisted validation state, so callers holding a
        // resolution do not have to resolve again.
        internal static PathValidationState ResolveValidationState(PathRouteResolution resolution)
        {
            ArgumentNullException.ThrowIfNull(resolution);

            return resolution.HighestSeverity < PathRouteDiagnosticSeverity.Error ? PathValidationState.Valid : PathValidationState.Invalid;
        }

        /// <summary>
        /// Returns the resolution of <paramref name="pathModel"/>, reusing the editor's cached resolution when it
        /// was already resolved. Lets consumers show route diagnostics without resolving the path again.
        /// </summary>
        internal PathRouteResolution ResolveCurrent(PathModel pathModel)
        {
            return resolutionCache.Resolve(pathModel, RuntimeDataResolver.Instance?.TrackWorld);
        }

        private static async Task<bool> CanInitializePathAsync(PathModelHeader path, CancellationToken cancellationToken)
        {
            PathModel pathModel = path is PathModel model
                ? model
                : await path.GetExtended(cancellationToken).ConfigureAwait(false);

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
            currentPathModel = newPath;
            ClearMoveNodeState();
            ClearHistory();
            InitializePathEdit(newPath);
            unsavedChanges = true;
            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;

            PathModel redoSnapshot = currentPathModel;
            PathModel undoSnapshot = undoHistory.Pop();
            RestoreSnapshot(undoSnapshot);
            if (redoSnapshot != null)
                redoHistory.Push(redoSnapshot);
            unsavedChanges = true;
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
                return false;

            PathModel undoSnapshot = currentPathModel;
            PathModel redoSnapshot = redoHistory.Pop();
            RestoreSnapshot(redoSnapshot);
            if (undoSnapshot != null)
                undoHistory.Push(undoSnapshot);
            unsavedChanges = true;
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
        public PathEditResult RemoveRestOfPath(int nodeIndex) => ApplySelectedNodeEdit(nodeIndex, model => PathModelEditor.RemoveRestOfPath(model, nodeIndex));

        public PathEditorCommandResult RemoveRestOfPathCommand(int nodeIndex)
        {
            return PathEditorCommandResult.FromPathEditResult(RemoveRestOfPath(nodeIndex));
        }

        /// <summary>
        /// Resolves the current authored path and rebuilds it along the resolved track anchors, weaving any
        /// resolved passing branches back into the generated graph, and records an undo snapshot. Refuses paths
        /// that the resolver cannot resolve or whose passing shapes the generator cannot represent; the reason is
        /// returned in the result.
        /// </summary>
        public PathEditResult SnapToTrack()
            => ApplyUndoableEdit(model => SnapPathToTrack(model, RuntimeDataResolver.Instance.TrackWorld));

        public PathEditorCommandResult ReResolvePathCommand()
        {
            return PathEditorCommandResult.FromPathEditResult(SnapToTrack());
        }

        public bool TryGetPathSpanAt(in PointD location, double toleranceWorldUnits, out int fromNodeIndex, out PathNode placementAnchor)
        {
            return TryGetRenderedMainPathSpanAt(location, toleranceWorldUnits, out fromNodeIndex, out placementAnchor);
        }

        /// <summary>
        /// Returns the equal-cost route candidates for the span starting at <paramref name="fromNodeIndex"/>, or
        /// an empty array when the span is unambiguous.
        /// </summary>
        public ImmutableArray<ResolvedRouteCandidate> GetSpanCandidates(int fromNodeIndex)
        {
            foreach (ResolvedPathSpan span in GetAmbiguousSpans())
            {
                if (span.FromNodeIndex == fromNodeIndex)
                    return span.Candidates;
            }

            return ImmutableArray<ResolvedRouteCandidate>.Empty;
        }

        /// <summary>
        /// Whether the current path can be appended to interactively.
        /// </summary>
        public bool CanExtendPath => TrainPath != null && !EditMode && !IsMovingNode;

        /// <summary>
        /// Resumes interactive appending on the current path. When the path already ends with an end node, that
        /// node is demoted to an intermediate node (as a single undoable step) so appending continues beyond it;
        /// the user then clicks to add points and double-clicks to set the new end.
        /// </summary>
        public PathEditorCommandResult ExtendPathCommand()
        {
            PathModel currentModel = TryGetEditablePathModel();
            if (currentModel == null)
                return PathEditorCommandResult.Failed("No editable path is currently loaded.", null);

            PathModel extendedModel = currentModel;
            if (HasFlag(currentModel, PathNodeType.End))
            {
                PathEditResult removeEnd = PathModelEditor.RemoveEnd(currentModel);
                if (!removeEnd.Success)
                    return PathEditorCommandResult.FromPathEditResult(removeEnd);

                extendedModel = removeEnd.PathModel;
                PushUndoSnapshot(currentModel);
                unsavedChanges = true;
            }

            path = extendedModel;
            currentPathModel = extendedModel;
            RestorePath(extendedModel, true);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return PathEditorCommandResult.Succeeded("Click to append path points; double-click to set the new end.", extendedModel);
        }

        public bool CanMoveNode(int nodeIndex)
        {
            PathModel currentModel = TryGetEditablePathModel();
            ImmutableArray<PathNode> nodes = currentModel?.PathNodes ?? ImmutableArray<PathNode>.Empty;
            return nodeIndex >= 0 && nodeIndex < nodes.Length;
        }

        /// <summary>
        /// Finds the path node closest to <paramref name="location"/> within <paramref name="toleranceWorldUnits"/>.
        /// Used for map surface hit testing; returns false when no node is within tolerance.
        /// </summary>
        public bool TryGetPathNodeAt(in PointD location, double toleranceWorldUnits, out int nodeIndex)
            => TryGetPathNodeAt(TrainPath?.PathPoints, location, toleranceWorldUnits, out nodeIndex);

        /// <summary>
        /// Finds the path point closest to <paramref name="location"/> within <paramref name="toleranceWorldUnits"/>.
        /// </summary>
        internal static bool TryGetPathNodeAt(IReadOnlyList<TrainPathPointBase> pathPoints, in PointD location, double toleranceWorldUnits, out int nodeIndex)
        {
            nodeIndex = -1;
            if (toleranceWorldUnits <= 0)
                return false;

            if (pathPoints == null || pathPoints.Count == 0)
                return false;

            double closestDistanceSquared = toleranceWorldUnits * toleranceWorldUnits;
            for (int i = 0; i < pathPoints.Count; i++)
            {
                double distanceSquared = pathPoints[i].Location.DistanceSquared(location);
                if (distanceSquared <= closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    nodeIndex = i;
                }
            }

            return nodeIndex >= 0;
        }

        public PathEditResult RepairSelectedNode(int nodeIndex)
        {
            return ApplySelectedNodeEdit(nodeIndex, model => PathModelEditor.RepairNode(model, nodeIndex, RuntimeDataResolver.Instance.TrackWorld));
        }

        /// <summary>
        /// Marks the node at <paramref name="nodeIndex"/> as a wait point with the given wait time in seconds and
        /// records an undo snapshot. Returns the operation result.
        /// </summary>
        public PathEditResult SetWaitPoint(int nodeIndex, int waitTimeSeconds)
        {
            return ApplySelectedNodeEdit(nodeIndex, model => PathModelEditor.SetWaitPoint(model, nodeIndex, waitTimeSeconds));
        }

        /// <summary>Clears the wait point on the node at <paramref name="nodeIndex"/> and records an undo snapshot.</summary>
        public PathEditResult ClearWaitPoint(int nodeIndex)
        {
            return ApplySelectedNodeEdit(nodeIndex, model => PathModelEditor.ClearWaitPoint(model, nodeIndex));
        }

        /// <summary>Marks the node at <paramref name="nodeIndex"/> as a reversal point and records an undo snapshot.</summary>
        public PathEditResult SetReversalPoint(int nodeIndex)
        {
            return ApplySelectedNodeEdit(nodeIndex, model => PathModelEditor.SetReversalPoint(model, nodeIndex));
        }

        /// <summary>Clears the reversal point on the node at <paramref name="nodeIndex"/> and records an undo snapshot.</summary>
        public PathEditResult ClearReversalPoint(int nodeIndex)
        {
            return ApplySelectedNodeEdit(nodeIndex, model => PathModelEditor.ClearReversalPoint(model, nodeIndex));
        }

        /// <summary>
        /// Inserts a via point after the node at <paramref name="afterNodeIndex"/> and immediately starts the map
        /// placement interaction for it, so the user positions the new node by clicking the map. Canceling the
        /// placement also removes the inserted node.
        /// </summary>
        public PathEditResult BeginViaPointPlacement(int afterNodeIndex)
        {
            PathModel currentModel = TryGetEditablePathModel();
            ImmutableArray<PathNode> nodes = currentModel?.PathNodes ?? ImmutableArray<PathNode>.Empty;
            if (afterNodeIndex < 0 || afterNodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {afterNodeIndex} is out of range.", currentModel);

            // The new node starts on the anchor of its predecessor; the placement interaction moves it to the
            // location the user picks on the map.
            PathNode anchor = new PathNode(nodes[afterNodeIndex].Location) { NodeIndex = nodes[afterNodeIndex].NodeIndex };
            return BeginViaPointPlacementAt(afterNodeIndex, anchor);
        }

        public PathEditResult BeginViaPointPlacementAt(int afterNodeIndex, PathNode anchor)
        {
            ArgumentNullException.ThrowIfNull(anchor);

            PathModel currentModel = TryGetEditablePathModel();
            ImmutableArray<PathNode> nodes = currentModel?.PathNodes ?? ImmutableArray<PathNode>.Empty;
            if (afterNodeIndex < 0 || afterNodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {afterNodeIndex} is out of range.", currentModel);

            PathEditResult result = PathModelEditor.InsertViaPoint(currentModel, afterNodeIndex, anchor, false);
            if (!result.Success)
                return result;

            pendingViaSourceModel = currentModel;
            pendingViaSourceEditMode = EditMode;
            pendingViaSourceUnsavedChanges = unsavedChanges;
            path = result.PathModel;
            currentPathModel = result.PathModel;
            RestorePath(result.PathModel, EditMode);
            unsavedChanges = true;

            int viaNodeIndex = afterNodeIndex + 1;
            if (!BeginMoveNode(viaNodeIndex))
            {
                RestorePendingViaSource();
                return PathEditResult.Failed($"Cannot place via point {viaNodeIndex}.", currentModel);
            }

            pendingViaNodeIndex = viaNodeIndex;
            return result;
        }

        /// <summary>Removes the via point at <paramref name="nodeIndex"/> and records an undo snapshot.</summary>
        public PathEditResult RemoveViaPoint(int nodeIndex)
        {
            return ApplySelectedNodeEdit(nodeIndex, model => PathModelEditor.RemoveViaPoint(model, nodeIndex));
        }

        /// <summary>
        /// Resolves the current authored path and returns the spans that have several equal-cost route
        /// candidates, so the user can choose the intended route.
        /// </summary>
        public ImmutableArray<ResolvedPathSpan> GetAmbiguousSpans()
        {
            PathModel currentModel = TryGetEditablePathModel();
            if (currentModel == null)
                return ImmutableArray<ResolvedPathSpan>.Empty;

            PathRouteResolution resolution = PathRouteResolver.Resolve(currentModel, RuntimeDataResolver.Instance.TrackWorld, CancellationToken.None);
            return resolution.MainRoute == null || resolution.MainRoute.Spans.IsDefaultOrEmpty
                ? ImmutableArray<ResolvedPathSpan>.Empty
                : resolution.MainRoute.Spans.Where(span => span.Candidates.Length > 1).ToImmutableArray();
        }

        /// <summary>
        /// Shows the route candidate on the map without changing the authored path. The preview is discarded by
        /// <see cref="ClearRouteCandidatePreview"/> or replaced by the next preview.
        /// </summary>
        public PathEditResult PreviewRouteCandidate(int fromNodeIndex, int candidateIndex)
        {
            PathEditResult result = BuildRouteCandidateModel(fromNodeIndex, candidateIndex);
            if (!result.Success)
                return result;

            SetPreviewPath(result.PathModel);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return result;
        }

        /// <summary>Discards an active route candidate preview.</summary>
        public void ClearRouteCandidatePreview()
        {
            if (IsMovingNode)
                return;

            SetPreviewPath(null);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        /// <summary>
        /// Commits the route candidate by authoring its intermediary anchors as via points, making the span
        /// unambiguous, and records an undo snapshot.
        /// </summary>
        public PathEditResult AcceptRouteCandidate(int fromNodeIndex, int candidateIndex)
        {
            ResolvedRouteCandidate candidate = FindRouteCandidate(fromNodeIndex, candidateIndex);
            if (candidate == null)
                return PathEditResult.Failed($"No route candidate {candidateIndex} exists for the span starting at node {fromNodeIndex}.", TryGetEditablePathModel());

            PathEditResult result = ApplySelectedNodeEdit(fromNodeIndex, model => PathModelEditor.ApplyRouteCandidate(model, fromNodeIndex, candidate));
            if (result.Success)
                SetPreviewPath(null);

            return result;
        }

        public PathEditorCommandResult PreviewRouteCandidateCommand(int fromNodeIndex, int candidateIndex)
        {
            PathEditResult result = PreviewRouteCandidate(fromNodeIndex, candidateIndex);
            return result.Success
                ? PathEditorCommandResult.Succeeded($"Previewing route candidate {candidateIndex} for the span starting at node {fromNodeIndex}.", currentPathModel)
                : PathEditorCommandResult.FromPathEditResult(result);
        }

        public PathEditorCommandResult AcceptRouteCandidateCommand(int fromNodeIndex, int candidateIndex)
        {
            return PathEditorCommandResult.FromPathEditResult(AcceptRouteCandidate(fromNodeIndex, candidateIndex));
        }

        // Builds (but does not commit) the authored path that results from choosing a candidate.
        private PathEditResult BuildRouteCandidateModel(int fromNodeIndex, int candidateIndex)
        {
            PathModel currentModel = TryGetEditablePathModel();
            if (currentModel == null)
                return PathEditResult.Failed("No editable path is currently loaded.", null);

            ResolvedRouteCandidate candidate = FindRouteCandidate(fromNodeIndex, candidateIndex);
            return candidate == null
                ? PathEditResult.Failed($"No route candidate {candidateIndex} exists for the span starting at node {fromNodeIndex}.", currentModel)
                : PathModelEditor.ApplyRouteCandidate(currentModel, fromNodeIndex, candidate);
        }

        private ResolvedRouteCandidate FindRouteCandidate(int fromNodeIndex, int candidateIndex)
        {
            ResolvedPathSpan span = GetAmbiguousSpans().FirstOrDefault(span => span.FromNodeIndex == fromNodeIndex);
            return span != null && candidateIndex >= 0 && candidateIndex < span.Candidates.Length
                ? span.Candidates[candidateIndex]
                : null;
        }

        public PathEditorCommandResult BeginViaPointPlacementCommand(int afterNodeIndex)
        {
            PathEditResult result = BeginViaPointPlacement(afterNodeIndex);
            return result.Success
                ? PathEditorCommandResult.Succeeded($"Select a track location for the new via point after node {afterNodeIndex}.", currentPathModel)
                : PathEditorCommandResult.FromPathEditResult(result);
        }

        public PathEditorCommandResult BeginViaPointPlacementAtCommand(int afterNodeIndex, PathNode anchor)
        {
            PathEditResult result = BeginViaPointPlacementAt(afterNodeIndex, anchor);
            return result.Success
                ? PathEditorCommandResult.Succeeded($"Via point added after node {afterNodeIndex}; move it or click to confirm.", currentPathModel)
                : PathEditorCommandResult.FromPathEditResult(result);
        }

        public PathEditorCommandResult RemoveViaPointCommand(int nodeIndex)
        {
            return PathEditorCommandResult.FromPathEditResult(RemoveViaPoint(nodeIndex));
        }

        public PathEditorCommandResult SetWaitPointCommand(int nodeIndex, int waitTimeSeconds)
        {
            return PathEditorCommandResult.FromPathEditResult(SetWaitPoint(nodeIndex, waitTimeSeconds));
        }

        public PathEditorCommandResult ClearWaitPointCommand(int nodeIndex)
        {
            return PathEditorCommandResult.FromPathEditResult(ClearWaitPoint(nodeIndex));
        }

        public PathEditorCommandResult SetReversalPointCommand(int nodeIndex)
        {
            return PathEditorCommandResult.FromPathEditResult(SetReversalPoint(nodeIndex));
        }

        public PathEditorCommandResult ClearReversalPointCommand(int nodeIndex)
        {
            return PathEditorCommandResult.FromPathEditResult(ClearReversalPoint(nodeIndex));
        }

        // Runs a single-node edit against the current path. Unlike ApplyUndoableEdit this also works when the
        // editor is not yet in edit mode: the current path is promoted to an editable model first, so selecting a
        // node and acting on it does not require a separate 'start editing' step. Keeps the node selected so the
        // user can chain edits on the same node.
        //
        // NOTE: promoting the model must NOT switch the editor into EditMode. EditMode means "interactively
        // appending points to the path tail": it seeds an active path point which the next pointer move turns
        // into a rubber-band segment growing from the last node. That is correct while drawing a path, but after
        // a single-node edit (repair, wait, reversal, via removal) it leaves the path dangling from its end node
        // towards the cursor, which is not what the user asked for.
        private PathEditResult ApplySelectedNodeEdit(int nodeIndex, Func<PathModel, PathEditResult> edit)
        {
            PathModel currentModel = TryGetEditablePathModel();
            ImmutableArray<PathNode> nodes = currentModel?.PathNodes ?? ImmutableArray<PathNode>.Empty;
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", currentModel);

            PathEditResult result = edit(currentModel);
            if (!result.Success)
                return result;

            if (!EditMode)
            {
                path = currentModel;
                currentPathModel = currentModel;
            }

            PushUndoSnapshot(currentModel);
            unsavedChanges = true;
            RestoreSnapshot(result.PathModel);
            SelectPathItem(nodeIndex);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return result;
        }

        public PathEditorCommandResult RepairSelectedNodeCommand(int nodeIndex)
        {
            return PathEditorCommandResult.FromPathEditResult(RepairSelectedNode(nodeIndex));
        }

        public bool BeginMoveNode(int nodeIndex)
        {
            PathModel currentModel = TryGetEditablePathModel();
            ImmutableArray<PathNode> nodes = currentModel?.PathNodes ?? ImmutableArray<PathNode>.Empty;
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return false;

            if (!EditMode)
            {
                path = currentModel;
                currentPathModel = currentModel;
                RestorePath(currentModel, true);
            }

            movingNodeIndex = nodeIndex;
            moveSourceModel = currentModel;
            movePreviewModel = null;
            movePreviewAnchor = null;
            UseStandaloneActivePathPointPreview = true;
            if (!InitializeActivePathPointPreview(nodeIndex))
            {
                ClearMoveNodeState();
                return false;
            }
            SetHiddenPathNodeIndex(nodeIndex);
            SelectPathItem(nodeIndex);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

        public PathEditorCommandResult BeginMoveNodeCommand(int nodeIndex)
        {
            return BeginMoveNode(nodeIndex)
                ? PathEditorCommandResult.Succeeded($"Select a new track location for node {nodeIndex}.", currentPathModel)
                : PathEditorCommandResult.Failed($"Cannot move path node {nodeIndex}; the node is not available in the current path model.", currentPathModel);
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
                // Keep the last valid preview available for Commit Move. The current hover candidate may be
                // off-track (or the pointer may have moved over WPF chrome), but that should not discard the
                // last valid target the user previewed.
                return;
            }

            PathNode replacementAnchor = CreateReplacementAnchor(candidate);
            if (EquivalentMoveAnchor(movePreviewAnchor, replacementAnchor))
                return;

            bool isJunction = candidate.JunctionNode != null || candidate.NodeType.Includes(PathNodeType.Junction);
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

            int movedNodeIndex = movingNodeIndex;
            PathModel viaSourceModel = pendingViaSourceModel;
            bool viaSourceEditMode = pendingViaSourceEditMode;
            bool viaSourceUnsavedChanges = pendingViaSourceUnsavedChanges;
            PathModel currentModel = TryGetEditablePathModel();
            ClearMoveNodeState();
            if (viaSourceModel != null)
            {
                path = viaSourceModel;
                currentPathModel = viaSourceModel;
                RestorePath(viaSourceModel, viaSourceEditMode);
                unsavedChanges = viaSourceUnsavedChanges;
                SelectPathItem(Math.Min(movedNodeIndex, viaSourceModel.PathNodes.Length - 1));
            }
            else if (currentModel != null)
            {
                path = currentModel;
                currentPathModel = currentModel;
                RestorePath(currentModel, false);
                SelectPathItem(movedNodeIndex);
            }

            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

        public PathEditorCommandResult CancelMoveNodeCommand()
        {
            return CancelMoveNode()
                ? PathEditorCommandResult.Succeeded("Node move canceled.", currentPathModel)
                : PathEditorCommandResult.Failed("No node move is active.", currentPathModel);
        }

        // Captures the current authored path, runs the edit, and on success records an undo snapshot and
        // rebuilds the editor from the new model. Returns a failed result (with a reason) when the editor is not
        // in edit mode, no path is currently loaded, or the edit reports failure.
        private PathEditResult ApplyUndoableEdit(Func<PathModel, PathEditResult> edit)
        {
            if (!EditMode)
                return PathEditResult.Failed("The path is not in edit mode.", null);

            PathModel currentModel = TryGetEditablePathModel() ?? moveSourceModel;
            if (currentModel == null)
                return PathEditResult.Failed("No editable path is currently loaded.", null);

            PathEditResult result = edit(currentModel);
            if (!result.Success)
                return result;

            PushUndoSnapshot(currentModel);
            RestoreSnapshot(result.PathModel);
            unsavedChanges = true;
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return result;
        }

        private bool HasNodeType(Func<PathModel, bool> predicate)
        {
            if (!EditMode)
                return false;

            PathModel currentModel = TryGetEditablePathModel();
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
            PathModel pathModel = new PathModel(pathDetails) { PathNodes = currentPathModel?.PathNodes ?? ImmutableArray<PathNode>.Empty };

            // Editor-saved paths are always normalized to track (guard-and-refuse preserves passing-branch paths).
            pathModel = TrySnapForSave(pathModel);

            // Stamp the validation state from the model that will actually be persisted so the header is self-describing.
            pathModel = pathModel with { ValidationState = ResolveValidationState(pathModel, RuntimeDataResolver.Instance.TrackWorld) };

            // The toolbox registers RuntimeDataResolver process-wide only (RuntimeDataResolver.Initialize
            // passes game: null), so Instance is the single authoritative resolver here; a game-scoped
            // GameInstance(game) lookup would resolve to the same object.
            pathModel = await RuntimeDataResolver.Instance.RouteData.Save(pathModel).ConfigureAwait(false);
            path = pathModel;
            currentPathModel = pathModel;
            unsavedChanges = false;
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
                PathEditResult result = CommitMoveNode();
                if (!result.Success)
                    Trace.TraceWarning($"Cannot commit moved path node: {result.Message}");
                userCommandArgs.Handled = true;
                editorDragged = false;
                return;
            }

            if (EditMode && !editorDragged)
            {
                PathModel undoSnapshot = currentPathModel;
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
                {
                    currentPathModel = TryCaptureSnapshot() ?? currentPathModel;
                    PushUndoSnapshot(undoSnapshot);
                }
                if (changed)
                    unsavedChanges = true;
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

            PathModel undoSnapshot = currentPathModel;
            if (RemovePathPoint())
            {
                currentPathModel = TryCaptureSnapshot() ?? currentPathModel;
                PushUndoSnapshot(undoSnapshot);
                unsavedChanges = true;
            }
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            userCommandArgs.Handled = true;
        }

        private void ClearHistory()
        {
            undoHistory.Clear();
            redoHistory.Clear();
        }

        public PathEditResult CommitMoveNode()
        {
            PathModel currentModel = TryGetEditablePathModel() ?? moveSourceModel;
            if (currentModel == null)
                return PathEditResult.Failed("No editable path is currently loaded.", null);

            PathModel committedModel = movePreviewModel;
            PathEditResult result;
            if (committedModel == null)
            {
                TrainPathPointBase candidate = ActivePathPoint;
                if (candidate == null || candidate.ValidationResult != PathNodeInvalidReasons.None || candidate.ConnectedSegments.IsDefaultOrEmpty)
                    return PathEditResult.Failed("Select a valid track location for the node.", currentModel);

                PathNode replacementAnchor = CreateReplacementAnchor(candidate);
                bool isJunction = candidate.JunctionNode != null || candidate.NodeType.Includes(PathNodeType.Junction);
                result = PathModelEditor.MoveNode(currentModel, movingNodeIndex, replacementAnchor, isJunction);
                if (!result.Success)
                    return result;

                committedModel = result.PathModel;
            }

            int movedNodeIndex = movingNodeIndex;
            PathModel undoSnapshot = pendingViaSourceModel ?? currentModel;
            PushUndoSnapshot(undoSnapshot);
            unsavedChanges = true;
            ClearMoveNodeState();
            path = committedModel;
            currentPathModel = committedModel;
            RestorePath(committedModel, false);
            SelectPathItem(movedNodeIndex);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return PathEditResult.Succeeded($"Moved node {movedNodeIndex}.", committedModel, ImmutableArray.Create(movedNodeIndex));
        }

        public PathEditorCommandResult CommitMoveNodeCommand()
        {
            return PathEditorCommandResult.FromPathEditResult(CommitMoveNode());
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
            return ReferenceEquals(first, second) || first != null && second != null && first.NodeIndex == second.NodeIndex && first.Location == second.Location;
        }

        private void ClearMoveNodeState()
        {
            movingNodeIndex = -1;
            pendingViaNodeIndex = -1;
            pendingViaSourceModel = null;
            pendingViaSourceEditMode = false;
            pendingViaSourceUnsavedChanges = false;
            moveSourceModel = null;
            ClearMovePreview();
            UseStandaloneActivePathPointPreview = false;
            SetHiddenPathNodeIndex(-1);
        }

        private void RestorePendingViaSource()
        {
            PathModel sourceModel = pendingViaSourceModel;
            bool sourceEditMode = pendingViaSourceEditMode;
            bool sourceUnsavedChanges = pendingViaSourceUnsavedChanges;
            ClearMoveNodeState();
            if (sourceModel == null)
                return;

            path = sourceModel;
            currentPathModel = sourceModel;
            RestorePath(sourceModel, sourceEditMode);
            unsavedChanges = sourceUnsavedChanges;
        }

        private PathModel TryCaptureSnapshot()
        {
            try
            {
                // Interactive point add/remove captures the model directly instead of going through
                // RestoreSnapshot, so refresh the validation state here as well.
                return TrainPath == null || path == null ? null : RefreshValidationState(ConvertTrainPath(path));
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceWarning($"Cannot capture train path undo snapshot because the current path is invalid: {ex.Message}. {BuildSnapshotContext()}");
                return null;
            }
        }

        private PathModel TryGetEditablePathModel()
        {
            return currentPathModel;
        }

        internal PathModel TryCaptureCurrentPathModel()
        {
            return PreviewPathModel ?? TryGetEditablePathModel();
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

            // Every mutation funnels through here, so this is where the model's validation state is refreshed.
            snapshot = RefreshValidationState(snapshot);

            // Preserve the current View/Edit mode across the rebuild: undoing/redoing or mutating must not
            // silently switch a path that was opened for viewing into edit mode.
            path = snapshot;
            currentPathModel = snapshot;
            RestorePath(snapshot, EditMode);
            ClearMoveNodeState();
            validPointAdded = false;
            editorDragged = false;
        }

        // Recomputes the model's persisted ValidationState. ValidationState travels with the model, so an edit
        // that replaces PathNodes would otherwise keep the previously persisted value and leave the path list and
        // details showing a stale valid/invalid marker until the path is saved or 'Validate All' is run.
        internal static PathModel RefreshValidationState(PathModel pathModel, TrackWorld trackWorld)
        {
            if (pathModel == null)
                return null;

            return ApplyValidationState(pathModel, ResolveValidationState(pathModel, trackWorld));
        }

        // Instance variant reusing the editor's resolution cache, so the resolution computed here is the same one
        // consumers get from ResolveCurrent.
        private PathModel RefreshValidationState(PathModel pathModel)
        {
            PathRouteResolution resolution = ResolveCurrent(pathModel);
            return resolution == null ? pathModel : ApplyValidationState(pathModel, ResolveValidationState(resolution));
        }

        private static PathModel ApplyValidationState(PathModel pathModel, PathValidationState validationState)
        {
            return validationState == pathModel.ValidationState ? pathModel : pathModel with { ValidationState = validationState };
        }
    }
}
