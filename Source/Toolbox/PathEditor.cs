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
    internal enum PathEditorPlacementMode
    {
        None,
        MoveNode,
        StartAnchor,
        EndAnchor,
        BuildRoute,
    }

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
        internal const string NewPathId = "<New Path>";

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Stack<PathModel> undoHistory = new Stack<PathModel>();
        private readonly Stack<PathModel> redoHistory = new Stack<PathModel>();
        // One resolution per path model instance, shared by the persisted validation state and by consumers such
        // as the train path tool window, so an edit resolves the path exactly once.
        private readonly PathRouteResolutionCache resolutionCache = new PathRouteResolutionCache();
        private const long DoubleClickIntervalMilliseconds = 500;
        private PathModelHeader path;
        private PathModel currentPathModel;

        private bool editorDragged;
        private long lastRoutePointClickTick;
        private int movingNodeIndex = -1;
        private PathModel moveSourceModel;
        private PathModel routeAuthoringModel;
        private PathModel movePreviewAuthoringModel;
        private PathModel movePreviewModel;
        private PathNode movePreviewAnchor;
        private PathSpanCommitResult movePreviewSpanCommit;
        private PathEditorPlacementMode placementMode;
        private bool placementSourceEditMode;
        private bool placementSourceUnsavedChanges;
        private int pendingViaNodeIndex = -1;
        private PathModel pendingViaSourceModel;
        private bool pendingViaSourceEditMode;
        private bool pendingViaSourceUnsavedChanges;
        private PendingAmbiguousSpanCommit pendingAmbiguousSpanCommit;
        private int previewedRouteCandidateFromNodeIndex = -1;
        private int previewedRouteCandidateIndex = -1;
        private bool unsavedChanges;
        private PathSaveOperation activeSaveOperation;

        public string PathId => path?.Id;

        public bool CanUndo => undoHistory.Count > 0;

        public bool CanRedo => redoHistory.Count > 0;

        public PathEditorPlacementMode PlacementMode => placementMode;

        public bool IsPlacementActive => placementMode != PathEditorPlacementMode.None;

        public bool IsMovingNode => placementMode == PathEditorPlacementMode.MoveNode && movingNodeIndex >= 0;

        public bool IsPlacingStartAnchor => placementMode == PathEditorPlacementMode.StartAnchor;

        public bool IsPlacingEndAnchor => placementMode == PathEditorPlacementMode.EndAnchor;

        public bool IsBuildingRoute => placementMode == PathEditorPlacementMode.BuildRoute;

        public bool CanCommitMoveNode => IsMovingNode && movePreviewModel != null;

        public bool CanCommitPlacement => IsPlacementActive && movePreviewModel != null;

        public bool HasUnsavedChanges => unsavedChanges;

        /// <summary>Whether persistence is currently running for the captured editor model.</summary>
        public bool IsSaveInProgress => activeSaveOperation != null;

        /// <summary><see langword="true"/> when the editor contains the unsaved path created by New Path.</summary>
        public bool IsNewPath => string.Equals(currentPathModel?.Id, NewPathId, StringComparison.Ordinal);

        public bool HasPendingAmbiguousSpanCommit => pendingAmbiguousSpanCommit != null;

        public bool CanCancelPathInteraction => IsPlacementActive || pendingAmbiguousSpanCommit != null || previewedRouteCandidateFromNodeIndex >= 0;

        public ImmutableArray<ResolvedPathSpan> PendingAmbiguousSpans => pendingAmbiguousSpanCommit?.AmbiguousSpans
            ?? ImmutableArray<ResolvedPathSpan>.Empty;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathChanged;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathUpdated;

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

        public async Task<bool> InitializePathAsync(PathModelHeader path, CancellationToken cancellationToken)
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
                routeAuthoringModel = null;

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
            return resolutionCache.Resolve(pathModel, TrackWorld);
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
                Id = NewPathId,
                Name = NewPathId,
                Start = "Start",
                End = "End",
                PlayerPath = true,
            };
            path = newPath;
            currentPathModel = newPath;
            ClearMoveNodeState();
            ClearHistory();
            unsavedChanges = true;
            InitializeAnchorPathEdit(newPath);
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

        public bool CanPlaceStartAnchor => !IsPlacementActive && TryGetEditablePathModel() != null;

        public bool CanPlaceEndAnchor => !IsPlacementActive
            && TryGetEditablePathModel() is PathModel currentModel
            && HasFlag(currentModel, PathNodeType.Start);

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

        public PathEditorCommandResult BeginStartAnchorPlacementCommand()
        {
            return BeginAnchorPlacement(PathEditorPlacementMode.StartAnchor)
                ? PathEditorCommandResult.Succeeded("Select a valid track location for the start anchor.", currentPathModel)
                : PathEditorCommandResult.Failed("Cannot place a start anchor in the current path.", currentPathModel);
        }

        public PathEditorCommandResult BeginEndAnchorPlacementCommand()
        {
            PathModel currentModel = TryGetEditablePathModel();
            if (currentModel == null || !HasFlag(currentModel, PathNodeType.Start))
                return PathEditorCommandResult.Failed("Set a start anchor before placing the end anchor.", currentModel);

            return BeginAnchorPlacement(PathEditorPlacementMode.EndAnchor)
                ? PathEditorCommandResult.Succeeded("Select a valid track location for the end anchor.", currentPathModel)
                : PathEditorCommandResult.Failed("Cannot place an end anchor in the current path.", currentPathModel);
        }

        /// <summary>
        /// Immediately commits an authored start anchor supplied by a map interaction. Unlike placement mode this
        /// does not require a subsequent pointer move or click.
        /// </summary>
        public PathEditorCommandResult SetStartAnchorCommand(PathNode anchor, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(anchor);

            if (IsPlacementActive)
            {
                if (!IsPlacingStartAnchor || moveSourceModel == null)
                    return PathEditorCommandResult.Failed("Cancel the active placement before setting the start anchor.", currentPathModel);

                PathEditResult placementResult = PathModelEditor.SetStartAnchor(moveSourceModel, anchor, isJunction);
                if (!placementResult.Success)
                    return PathEditorCommandResult.FromPathEditResult(placementResult);

                movePreviewAnchor = anchor;
                movePreviewModel = placementResult.PathModel;
                return PathEditorCommandResult.FromPathEditResult(CommitPlacement());
            }

            PathModel currentModel = TryGetEditablePathModel();
            bool beginRouteBuilding = currentModel != null && !HasFlag(currentModel, PathNodeType.Start);
            PathEditorCommandResult result = ApplyEndpointAnchor(model => PathModelEditor.SetStartAnchor(model, anchor, isJunction));
            if (result.Success && beginRouteBuilding)
                _ = BeginAnchorPlacement(PathEditorPlacementMode.BuildRoute);

            return result;
        }

        /// <summary>Finishes progressive route building at the last committed route point.</summary>
        public PathEditorCommandResult FinishPathCommand()
        {
            PathModel currentModel = TryGetEditablePathModel();
            if (!IsBuildingRoute || currentModel == null || !HasFlag(currentModel, PathNodeType.End))
                return PathEditorCommandResult.Failed("Add at least one route point before finishing the path.", currentModel);

            ClearMoveNodeState();
            routeAuthoringModel = null;
            path = currentModel;
            currentPathModel = currentModel;
            RestorePath(currentModel, false);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return PathEditorCommandResult.Succeeded("Path finished.", currentModel);
        }

        /// <summary>Commits the current route-point preview as the final endpoint and finishes route building.</summary>
        public PathEditorCommandResult FinishPathHereCommand()
        {
            if (!IsBuildingRoute)
                return PathEditorCommandResult.Failed("Route building is not active.", currentPathModel);

            return PathEditorCommandResult.FromPathEditResult(CommitPlacement(false));
        }

        public PathEditorCommandResult AddRoutePointHereCommand(PathNode anchor, bool isJunction, bool finishPath)
        {
            ArgumentNullException.ThrowIfNull(anchor);

            if (!IsBuildingRoute || moveSourceModel == null)
                return PathEditorCommandResult.Failed("Route building is not active.", currentPathModel);

            PathEditResult routePoint = AddRoutePoint(moveSourceModel, anchor, isJunction);
            if (!routePoint.Success)
                return PathEditorCommandResult.FromPathEditResult(routePoint);

            movePreviewAnchor = anchor;
            movePreviewAuthoringModel = routePoint.PathModel;
            movePreviewSpanCommit = EvaluateSpanCommit(routePoint.PathModel, routePoint.ChangedNodeIndexes,
                "Route point added.", moveSourceModel, true);
            if (movePreviewSpanCommit.Status == PathSpanCommitStatus.Unresolved)
                return PathEditorCommandResult.Failed(movePreviewSpanCommit.Message, currentPathModel);

            movePreviewModel = movePreviewSpanCommit.PathModel;
            return PathEditorCommandResult.FromPathEditResult(CommitPlacement(!finishPath));
        }

        /// <summary>
        /// Immediately commits an authored end anchor supplied by a map interaction. Unlike placement mode this
        /// does not require a subsequent pointer move or click.
        /// </summary>
        public PathEditorCommandResult SetEndAnchorCommand(PathNode anchor, bool isJunction)
        {
            if (IsPlacementActive)
                return PathEditorCommandResult.Failed("Cancel the active placement before setting the end anchor.", currentPathModel);

            return ApplyEndpointAnchor(model => PathModelEditor.SetEndAnchor(model, anchor, isJunction));
        }

        private PathEditorCommandResult ApplyEndpointAnchor(Func<PathModel, PathEditResult> edit)
        {
            PathModel currentModel = TryGetEditablePathModel();
            if (currentModel == null)
                return PathEditorCommandResult.Failed("No editable path is currently loaded.", null);

            PathSpanCommitResult spanCommit = ResolveAnchorSpan(currentModel, edit, true);
            switch (spanCommit.Status)
            {
                case PathSpanCommitStatus.Resolved:
                    PathEditResult committed = ApplyUndoableEdit(_ => PathEditResult.Succeeded(spanCommit.Message, spanCommit.PathModel, spanCommit.ChangedNodeIndexes));
                    return PathEditorCommandResult.FromPathEditResult(committed);
                case PathSpanCommitStatus.Ambiguous:
                    BeginPendingAmbiguousSpanCommit(currentModel, spanCommit);
                    return PathEditorCommandResult.Failed(spanCommit.Message, currentModel);
                default:
                    return PathEditorCommandResult.Failed(spanCommit.Message, currentModel);
            }
        }

        #region unified span commit
        // Single span-resolve-then-commit routine shared by every authored anchor mutation (start, end, move,
        // via). The authored edit is applied to a tentative model only; the affected spans (those bounded by an
        // edited node) are resolved and, when unambiguous, materialized into concrete nodes. Nothing is committed
        // and no history is touched here: the caller commits the returned model exactly once.
        private PathSpanCommitResult ResolveAnchorSpan(PathModel sourceModel, Func<PathModel, PathEditResult> anchorEdit)
            => ResolveAnchorSpan(sourceModel, anchorEdit, false);

        private PathSpanCommitResult ResolveAnchorSpan(PathModel sourceModel, Func<PathModel, PathEditResult> anchorEdit,
            bool allowAutomaticReversal)
        {
            ArgumentNullException.ThrowIfNull(sourceModel);
            ArgumentNullException.ThrowIfNull(anchorEdit);

            PathEditResult edit = anchorEdit(sourceModel);
            return edit.Success
                ? EvaluateSpanCommit(edit.PathModel, edit.ChangedNodeIndexes, edit.Message, sourceModel, allowAutomaticReversal)
                : PathSpanCommitResult.Failed(edit.Message, sourceModel);
        }

        // Resolves tentativeModel, inspects only the spans adjacent to changedNodeIndexes, and materializes the
        // generated intermediaries when every affected span resolved to a single route.
        private PathSpanCommitResult EvaluateSpanCommit(PathModel tentativeModel, ImmutableArray<int> changedNodeIndexes, string message, PathModel sourceModel)
            => EvaluateSpanCommit(tentativeModel, changedNodeIndexes, message, sourceModel, false);

        // allowAutomaticReversal is set by the endpoint-authoring commands (route building, Set End Here). When the
        // requested target only fails because it lies behind the current direction of travel, retry once with the
        // preceding route point marked as a reversal, which is what the user means by clicking back along the path.
        private PathSpanCommitResult EvaluateSpanCommit(PathModel tentativeModel, ImmutableArray<int> changedNodeIndexes, string message,
            PathModel sourceModel, bool allowAutomaticReversal)
        {
            PathRouteResolution resolution = PathRouteResolver.Resolve(tentativeModel, TrackWorld, PathRouteResolverOptions.Default, CancellationToken.None);
            ImmutableArray<ResolvedPathSpan> affectedSpans = AffectedSpans(resolution, changedNodeIndexes);

            if (affectedSpans.IsEmpty)
            {
                return CanCommitStandaloneStartAnchor(tentativeModel, changedNodeIndexes)
                    ? PathSpanCommitResult.Resolved(message, tentativeModel, changedNodeIndexes)
                    : PathSpanCommitResult.Unresolved("The edit did not produce a route span that can be materialized.", sourceModel);
            }

            ImmutableArray<ResolvedPathSpan> ambiguousSpans = affectedSpans
                .Where(span => span.Status == PathRouteSpanStatus.Ambiguous || span.Candidates.Length > 1)
                .ToImmutableArray();
            if (!ambiguousSpans.IsEmpty)
            {
                return PathSpanCommitResult.Ambiguous(
                    $"The affected span has {ambiguousSpans[0].Candidates.Length} equal-cost routes; choose a candidate or add a via point.",
                    tentativeModel, ambiguousSpans, changedNodeIndexes);
            }

            if (affectedSpans.Any(span => span.Status is PathRouteSpanStatus.Unresolved or PathRouteSpanStatus.NotResolved))
            {
                return TryReverseAndResolve(tentativeModel, changedNodeIndexes, message, sourceModel, allowAutomaticReversal)
                    ?? PathSpanCommitResult.Unresolved(
                        "The affected span could not be routed; click closer to the last anchor or add a via point.",
                        sourceModel);
            }

            if (HasImplicitRouteBack(tentativeModel, resolution.MainRoute?.Spans ?? ImmutableArray<ResolvedPathSpan>.Empty, affectedSpans))
            {
                return TryReverseAndResolve(tentativeModel, changedNodeIndexes, message, sourceModel, allowAutomaticReversal)
                    ?? PathSpanCommitResult.Unresolved(
                        "The affected span reverses over the existing route; mark the route point as a reversal before routing back.",
                        sourceModel);
            }

            // Materialize the generated intermediaries so the committed model is the single persistence source.
            PathPersistenceValidationResult materialization = PathPersistenceValidationPolicy.MaterializeResolvedPath(tentativeModel, resolution, TrackWorld);
            return materialization.PersistenceAllowed
                ? PathSpanCommitResult.Resolved(message, materialization.PathModel, materialization.ChangedNodeIndexes)
                : PathSpanCommitResult.Unresolved(materialization.FailureMessage, sourceModel);
        }

        // Marks the route point preceding the end anchor as a reversal and re-evaluates. Returns null when the
        // reversal is not applicable (no preceding point, or it is a junction/terminus) or still does not resolve,
        // so the caller reports its original failure.
        private PathSpanCommitResult TryReverseAndResolve(PathModel tentativeModel, ImmutableArray<int> changedNodeIndexes,
            string message, PathModel sourceModel, bool allowAutomaticReversal)
        {
            if (!allowAutomaticReversal)
                return null;

            int reversalNodeIndex = PrecedingEndNodeIndex(tentativeModel);
            if (reversalNodeIndex < 0)
                return null;

            PathEditResult reversal = PathModelEditor.SetReversalPoint(tentativeModel, reversalNodeIndex);
            if (!reversal.Success)
                return null;

            ImmutableArray<int> reversalChangedNodes = changedNodeIndexes.Contains(reversalNodeIndex)
                ? changedNodeIndexes
                : changedNodeIndexes.Add(reversalNodeIndex);
            PathSpanCommitResult result = EvaluateSpanCommit(reversal.PathModel, reversalChangedNodes,
                $"{message} Reversal added at node {reversalNodeIndex}.", sourceModel, false);

            return result.Success ? result : null;
        }

        private static bool CanCommitStandaloneStartAnchor(PathModel pathModel, ImmutableArray<int> changedNodeIndexes)
        {
            ImmutableArray<PathNode> nodes = pathModel.PathNodes.IsDefault ? ImmutableArray<PathNode>.Empty : pathModel.PathNodes;
            return nodes.Length == 1
                && changedNodeIndexes.Length == 1
                && changedNodeIndexes[0] == 0
                && nodes[0].NodeType.Includes(PathNodeType.Start)
                && nodes[0].NextMainNode == -1
                && nodes[0].NextSidingNode == -1;
        }

        // The node linking to the end anchor on the main chain, i.e. the route point committed just before it.
        private static int PrecedingEndNodeIndex(PathModel pathModel)
        {
            ImmutableArray<PathNode> nodes = pathModel?.PathNodes ?? ImmutableArray<PathNode>.Empty;
            int endIndex = -1;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].NodeType.Includes(PathNodeType.End))
                {
                    endIndex = i;
                    break;
                }
            }
            if (endIndex < 0)
                return -1;

            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].NextMainNode == endIndex)
                    return i;
            }

            return -1;
        }

        internal static bool HasImplicitRouteBack(PathModel pathModel, ImmutableArray<ResolvedPathSpan> allSpans,
            ImmutableArray<ResolvedPathSpan> affectedSpans)
        {
            if (pathModel == null || allSpans.IsDefaultOrEmpty || affectedSpans.IsDefaultOrEmpty)
                return false;

            HashSet<(int From, int To)> precedingEdges = new();
            foreach (ResolvedPathSpan span in allSpans)
            {
                if (affectedSpans.Contains(span) &&
                    !(span.FromNodeIndex >= 0 && span.FromNodeIndex < pathModel.PathNodes.Length &&
                    pathModel.PathNodes[span.FromNodeIndex].NodeType.Includes(PathNodeType.Reversal)))
                {
                    ImmutableArray<int> route = PrimaryRoute(span);
                    for (int i = 1; i < route.Length; i++)
                    {
                        if (precedingEdges.Contains((route[i], route[i - 1])))
                            return true;
                    }
                }

                AddRouteEdges(precedingEdges, span);
            }

            return false;
        }

        private static void AddRouteEdges(HashSet<(int From, int To)> edges, ResolvedPathSpan span)
        {
            ImmutableArray<int> route = PrimaryRoute(span);
            for (int i = 1; i < route.Length; i++)
                edges.Add((route[i - 1], route[i]));
        }

        private static ImmutableArray<int> PrimaryRoute(ResolvedPathSpan span)
        {
            return span.Candidates.IsDefaultOrEmpty
                ? span.TrackVectorNodeIndexes
                : span.Candidates[0].RouteNodeIndexes;
        }

        // Adjacency-based span boundaries: a span is affected when one of its bounding nodes was edited. Without
        // any changed node (or resolved span) the whole route is considered affected.
        private static ImmutableArray<ResolvedPathSpan> AffectedSpans(PathRouteResolution resolution, ImmutableArray<int> changedNodeIndexes)
        {
            ImmutableArray<ResolvedPathSpan> spans = resolution?.MainRoute?.Spans ?? ImmutableArray<ResolvedPathSpan>.Empty;
            if (spans.IsDefaultOrEmpty)
                return ImmutableArray<ResolvedPathSpan>.Empty;
            if (changedNodeIndexes.IsDefaultOrEmpty)
                return spans;

            return spans
                .Where(span => changedNodeIndexes.Contains(span.FromNodeIndex) || changedNodeIndexes.Contains(span.ToNodeIndex))
                .ToImmutableArray();
        }

        private void BeginPendingAmbiguousSpanCommit(PathModel sourceModel, PathSpanCommitResult spanCommit,
            bool resumeRouteBuilding = false)
        {
            pendingAmbiguousSpanCommit = new PendingAmbiguousSpanCommit(sourceModel, spanCommit.PathModel,
                spanCommit.ChangedNodeIndexes, spanCommit.AmbiguousSpans, resumeRouteBuilding);
            SetPendingCandidatePreview();
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        private void SetPendingCandidatePreview()
        {
            if (pendingAmbiguousSpanCommit == null)
                return;

            PathModel previewModel = BuildPendingCandidateModel(false, out string failure);
            if (previewModel == null)
            {
                SetPreviewPath(pendingAmbiguousSpanCommit.TentativeModel);
                Trace.TraceWarning($"Cannot materialize pending route candidate preview: {failure}");
            }
            else
            {
                SetPreviewPath(previewModel);
            }
        }

        private PathModel BuildPendingCandidateModel(bool requireExplicitSelections, out string failure)
        {
            failure = null;
            PathModel candidateModel = pendingAmbiguousSpanCommit.TentativeModel;
            foreach (ResolvedPathSpan span in pendingAmbiguousSpanCommit.AmbiguousSpans.OrderByDescending(span => span.FromNodeIndex))
            {
                int candidateIndex;
                if (!pendingAmbiguousSpanCommit.CandidateSelections.TryGetValue(span.FromNodeIndex, out candidateIndex))
                {
                    if (requireExplicitSelections)
                    {
                        failure = $"Select a route candidate for the span starting at node {span.FromNodeIndex}.";
                        return null;
                    }

                    candidateIndex = 0;
                }

                if (candidateIndex < 0 || candidateIndex >= span.Candidates.Length)
                {
                    failure = $"No route candidate {candidateIndex} exists for the span starting at node {span.FromNodeIndex}.";
                    return null;
                }

                PathEditResult applied = PathModelEditor.ApplyRouteCandidate(candidateModel, span.FromNodeIndex, span.Candidates[candidateIndex]);
                if (!applied.Success)
                {
                    failure = applied.Message;
                    return null;
                }

                candidateModel = applied.PathModel;
            }

            PathGenerationResult generated = GenerateTrackSnappedPath(candidateModel, TrackWorld);
            if (!generated.Success)
            {
                failure = generated.Message;
                return null;
            }

            return generated.PathModel;
        }

        public PathEditResult PreviewPendingRouteCandidate(int fromNodeIndex, int candidateIndex)
        {
            if (pendingAmbiguousSpanCommit == null)
                return PathEditResult.Failed("No pending ambiguous route selection exists.", TryGetEditablePathModel());

            ResolvedPathSpan span = pendingAmbiguousSpanCommit.AmbiguousSpans.FirstOrDefault(item => item.FromNodeIndex == fromNodeIndex);
            if (span == null || candidateIndex < 0 || candidateIndex >= span.Candidates.Length)
                return PathEditResult.Failed($"No route candidate {candidateIndex} exists for the span starting at node {fromNodeIndex}.", pendingAmbiguousSpanCommit.SourceModel);

            pendingAmbiguousSpanCommit.CandidateSelections[fromNodeIndex] = candidateIndex;
            PathModel previewModel = BuildPendingCandidateModel(false, out string failure);
            if (previewModel == null)
            {
                pendingAmbiguousSpanCommit.CandidateSelections.Remove(fromNodeIndex);
                return PathEditResult.Failed(failure, pendingAmbiguousSpanCommit.SourceModel);
            }

            SetPreviewPath(previewModel);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return PathEditResult.Succeeded($"Previewing route candidate {candidateIndex} for the span starting at node {fromNodeIndex}.",
                pendingAmbiguousSpanCommit.SourceModel, ImmutableArray<int>.Empty);
        }

        public PathEditResult AcceptPendingRouteCandidate(int fromNodeIndex, int candidateIndex)
        {
            PathEditResult preview = PreviewPendingRouteCandidate(fromNodeIndex, candidateIndex);
            if (!preview.Success)
                return preview;

            PathModel materialized = BuildPendingCandidateModel(true, out string failure);
            if (materialized == null)
                return PathEditResult.Failed(failure, pendingAmbiguousSpanCommit.SourceModel);

            PathModel sourceModel = pendingAmbiguousSpanCommit.SourceModel;
            ImmutableArray<int> changedNodeIndexes = pendingAmbiguousSpanCommit.ChangedNodeIndexes;
            bool resumeRouteBuilding = pendingAmbiguousSpanCommit.ResumeRouteBuilding;
            pendingAmbiguousSpanCommit = null;
            ClearMoveNodeState();
            PushUndoSnapshot(sourceModel);
            unsavedChanges = true;
            path = materialized;
            currentPathModel = materialized;
            RestorePath(materialized, EditMode);
            SelectPathItem(changedNodeIndexes.IsDefaultOrEmpty ? -1 : changedNodeIndexes[0]);
            if (resumeRouteBuilding)
                _ = BeginAnchorPlacement(PathEditorPlacementMode.BuildRoute);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return PathEditResult.Succeeded("Route candidate accepted.", materialized, changedNodeIndexes);
        }

        public void CancelPendingRouteCandidate()
        {
            if (pendingAmbiguousSpanCommit == null)
                return;

            pendingAmbiguousSpanCommit = null;
            SetPreviewPath(null);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }
        #endregion

        private bool BeginAnchorPlacement(PathEditorPlacementMode mode)
        {
            PathModel currentModel = TryGetEditablePathModel();
            if (currentModel == null || IsPlacementActive)
                return false;

            placementSourceEditMode = EditMode;
            placementSourceUnsavedChanges = unsavedChanges;
            if (!EditMode)
            {
                path = currentModel;
                currentPathModel = currentModel;
                RestorePath(currentModel, false);
            }

            placementMode = mode;
            if (mode == PathEditorPlacementMode.BuildRoute)
                routeAuthoringModel ??= currentModel;
            moveSourceModel = mode == PathEditorPlacementMode.BuildRoute ? routeAuthoringModel : currentModel;
            movePreviewModel = null;
            movePreviewAuthoringModel = null;
            movePreviewAnchor = null;
            movingNodeIndex = mode == PathEditorPlacementMode.StartAnchor
                ? IndexOfNodeType(currentModel, PathNodeType.Start, 0)
                : IndexOfNodeType(currentModel, PathNodeType.End, currentModel.PathNodes.Length);
            UseStandaloneActivePathPointPreview = true;
            ActivatePathPlacementInput();
            if (mode == PathEditorPlacementMode.BuildRoute && movingNodeIndex < currentModel.PathNodes.Length)
                _ = InitializeActivePathPointPreview(movingNodeIndex);
            SelectPathItem(movingNodeIndex < currentModel.PathNodes.Length ? movingNodeIndex : -1);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

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
        /// Re-resolves the whole path against the current track: every node is re-anchored through its hybrid
        /// anchor (stored track node index first, stored location when that index no longer matches the layout),
        /// and the route is rebuilt along the resolved anchors, weaving any resolved passing branches back into
        /// the generated graph. This is the whole-path counterpart to <see cref="RepairSelectedNode"/>: where
        /// that repairs a single node, this performs the legacy "fix broken path" operation for the entire path.
        /// On an unchanged layout it is near-idempotent (no node churn). Records an undo snapshot. Refuses paths
        /// the resolver cannot resolve or whose passing shapes the generator cannot represent; the reason is
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
        public bool CanContinuePath => TrainPath != null && !EditMode && !IsMovingNode;

        /// <summary>
        /// Starts resolver-backed interactive appending on the current path. Each click commits one resolved span
        /// and keeps Build Route active for the next route point.
        /// </summary>
        public PathEditorCommandResult ContinuePathCommand()
        {
            PathModel currentModel = TryGetEditablePathModel();
            if (currentModel == null)
                return PathEditorCommandResult.Failed("No editable path is currently loaded.", null);

            if (!HasFlag(currentModel, PathNodeType.End))
                return PathEditorCommandResult.Failed("Set an end anchor before extending the path.", currentModel);

            if (EditMode)
            {
                path = currentModel;
                currentPathModel = currentModel;
                RestorePath(currentModel, false);
            }

            return BeginAnchorPlacement(PathEditorPlacementMode.BuildRoute)
                ? PathEditorCommandResult.Succeeded("Click a track location to add the next route point.", currentPathModel)
                : PathEditorCommandResult.Failed("Cannot continue the current path.", currentPathModel);
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
            return ApplySelectedNodeEdit(nodeIndex, model => PathModelEditor.RepairNode(model, nodeIndex, TrackWorld));
        }

        public bool CanRepairNode(int nodeIndex)
        {
            PathModel currentModel = TryGetEditablePathModel();
            return currentModel != null && PathModelEditor.RepairNode(currentModel, nodeIndex, TrackWorld).Success;
        }

        public void HighlightDiagnosticTarget(int nodeIndex, int fromNodeIndex, int toNodeIndex)
        {
            if (nodeIndex >= 0)
            {
                HighlightPathItem(nodeIndex);
                return;
            }

            if (fromNodeIndex >= 0 && toNodeIndex >= 0 && HighlightPathSpan(fromNodeIndex, toNodeIndex))
                return;

            if (fromNodeIndex >= 0)
            {
                HighlightPathItem(fromNodeIndex);
                return;
            }

            ClearPathHighlight();
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

        /// <summary>
        /// Inserts a via anchor at a snapped map location, resolves both spans adjacent to it, and commits the
        /// materialized result as one undoable edit. Ambiguous spans are previewed for candidate selection and do
        /// not change the committed path.
        /// </summary>
        public PathEditorCommandResult AddViaPointHereCommand(int afterNodeIndex, PathNode anchor, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(anchor);

            PathModel currentModel = TryGetEditablePathModel();
            if (currentModel == null)
                return PathEditorCommandResult.Failed("No editable path is currently loaded.", null);

            PathSpanCommitResult spanCommit = ResolveAnchorSpan(currentModel,
                model => PathModelEditor.InsertViaPoint(model, afterNodeIndex, anchor, isJunction));
            switch (spanCommit.Status)
            {
                case PathSpanCommitStatus.Resolved:
                    PathEditResult committed = ApplyUndoableEdit(_ => PathEditResult.Succeeded(
                        "Via point added and adjacent spans resolved.", spanCommit.PathModel, spanCommit.ChangedNodeIndexes));
                    return PathEditorCommandResult.FromPathEditResult(committed);
                case PathSpanCommitStatus.Ambiguous:
                    BeginPendingAmbiguousSpanCommit(currentModel, spanCommit);
                    return PathEditorCommandResult.Failed(spanCommit.Message, currentModel);
                default:
                    return PathEditorCommandResult.Failed(spanCommit.Message, currentModel);
            }
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

            PathRouteResolution resolution = ResolveCurrent(currentModel);
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
            if (pendingAmbiguousSpanCommit != null)
            {
                PathEditResult pendingResult = PreviewPendingRouteCandidate(fromNodeIndex, candidateIndex);
                if (pendingResult.Success)
                    SetPreviewedRouteCandidate(fromNodeIndex, candidateIndex);
                return pendingResult;
            }

            PathEditResult result = BuildRouteCandidateModel(fromNodeIndex, candidateIndex);
            if (!result.Success)
                return result;

            SetPreviewPath(result.PathModel);
            SetPreviewedRouteCandidate(fromNodeIndex, candidateIndex);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return result;
        }

        /// <summary>Discards an active route candidate preview.</summary>
        public void ClearRouteCandidatePreview()
        {
            if (IsMovingNode)
                return;

            if (pendingAmbiguousSpanCommit != null)
            {
                CancelPendingRouteCandidate();
                return;
            }

            SetPreviewPath(null);
            ClearPreviewedRouteCandidate();
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        /// <summary>
        /// Commits the route candidate by authoring its intermediary anchors as via points, making the span
        /// unambiguous, and records an undo snapshot.
        /// </summary>
        public PathEditResult AcceptRouteCandidate(int fromNodeIndex, int candidateIndex)
        {
            if (pendingAmbiguousSpanCommit != null)
            {
                PathEditResult pendingResult = AcceptPendingRouteCandidate(fromNodeIndex, candidateIndex);
                if (pendingResult.Success)
                    ClearPreviewedRouteCandidate();
                return pendingResult;
            }

            ResolvedRouteCandidate candidate = FindRouteCandidate(fromNodeIndex, candidateIndex);
            if (candidate == null)
                return PathEditResult.Failed($"No route candidate {candidateIndex} exists for the span starting at node {fromNodeIndex}.", TryGetEditablePathModel());

            PathEditResult result = ApplySelectedNodeEdit(fromNodeIndex, model => PathModelEditor.ApplyRouteCandidate(model, fromNodeIndex, candidate));
            if (result.Success)
            {
                SetPreviewPath(null);
                ClearPreviewedRouteCandidate();
            }

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

        public PathEditorCommandResult CycleRouteCandidateCommand(int direction)
        {
            if (direction == 0)
                return PathEditorCommandResult.Failed("A route candidate direction is required.", currentPathModel);

            int fromNodeIndex = previewedRouteCandidateFromNodeIndex;
            int candidateIndex = previewedRouteCandidateIndex;
            ImmutableArray<ResolvedRouteCandidate> candidates = GetSpanCandidates(fromNodeIndex);
            if (candidates.IsDefaultOrEmpty)
            {
                fromNodeIndex = SelectedPathNodeIndex;
                candidates = GetSpanCandidates(fromNodeIndex);
                candidateIndex = -1;
            }

            if (candidates.IsDefaultOrEmpty && pendingAmbiguousSpanCommit != null)
            {
                ResolvedPathSpan span = pendingAmbiguousSpanCommit.AmbiguousSpans.FirstOrDefault();
                if (span != null)
                {
                    fromNodeIndex = span.FromNodeIndex;
                    candidates = span.Candidates;
                    candidateIndex = -1;
                }
            }

            if (candidates.IsDefaultOrEmpty)
                return PathEditorCommandResult.Failed("No route candidates are available.", currentPathModel);

            int nextCandidateIndex = candidateIndex < 0
                ? direction > 0 ? 0 : candidates.Length - 1
                : (candidateIndex + (direction > 0 ? 1 : candidates.Length - 1)) % candidates.Length;
            return PreviewRouteCandidateCommand(fromNodeIndex, nextCandidateIndex);
        }

        public PathEditorCommandResult AcceptPreviewedRouteCandidateCommand()
        {
            if (previewedRouteCandidateFromNodeIndex < 0 || previewedRouteCandidateIndex < 0)
                return PathEditorCommandResult.Failed("Preview a route candidate before accepting it.", currentPathModel);

            return AcceptRouteCandidateCommand(previewedRouteCandidateFromNodeIndex, previewedRouteCandidateIndex);
        }

        public PathEditorCommandResult CancelPathInteractionCommand()
        {
            if (pendingAmbiguousSpanCommit != null)
            {
                CancelPendingRouteCandidate();
                ClearPreviewedRouteCandidate();
                return PathEditorCommandResult.Succeeded("Route candidate selection canceled.", currentPathModel);
            }

            if (IsPlacementActive)
                return CancelPlacementCommand();

            if (previewedRouteCandidateFromNodeIndex >= 0)
            {
                ClearRouteCandidatePreview();
                return PathEditorCommandResult.Succeeded("Route candidate preview canceled.", currentPathModel);
            }

            return PathEditorCommandResult.Failed("No path interaction is active.", currentPathModel);
        }

        private void SetPreviewedRouteCandidate(int fromNodeIndex, int candidateIndex)
        {
            previewedRouteCandidateFromNodeIndex = fromNodeIndex;
            previewedRouteCandidateIndex = candidateIndex;
        }

        private void ClearPreviewedRouteCandidate()
        {
            previewedRouteCandidateFromNodeIndex = -1;
            previewedRouteCandidateIndex = -1;
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
            if (nodeIndex < 0 || nodeIndex >= nodes.Length || IsPlacementActive)
                return false;

            placementSourceEditMode = EditMode;
            placementSourceUnsavedChanges = unsavedChanges;
            if (!EditMode)
            {
                path = currentModel;
                currentPathModel = currentModel;
                RestorePath(currentModel, true);
            }

            placementMode = PathEditorPlacementMode.MoveNode;
            movingNodeIndex = nodeIndex;
            moveSourceModel = currentModel;
            movePreviewModel = null;
            movePreviewAnchor = null;
            movePreviewSpanCommit = null;
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
            if (IsPlacementActive)
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
                // Keep the last valid preview available for Commit Move.
                // off-track (or the pointer may have moved over WPF chrome), but that should not discard the
                // last valid target the user previewed.
                return;
            }

            PathNode replacementAnchor = CreateReplacementAnchor(candidate);
            if (EquivalentMoveAnchor(movePreviewAnchor, replacementAnchor))
                return;

            bool isJunction = candidate.JunctionNode != null || candidate.NodeType.Includes(PathNodeType.Junction);
            PathEditResult result = placementMode switch
            {
                PathEditorPlacementMode.MoveNode => PathModelEditor.MoveNode(moveSourceModel, movingNodeIndex, replacementAnchor, isJunction),
                PathEditorPlacementMode.StartAnchor => PathModelEditor.SetStartAnchor(moveSourceModel, replacementAnchor, isJunction),
                PathEditorPlacementMode.EndAnchor => PathModelEditor.SetEndAnchor(moveSourceModel, replacementAnchor, isJunction),
                PathEditorPlacementMode.BuildRoute => AddRoutePoint(moveSourceModel, replacementAnchor, isJunction),
                _ => PathEditResult.Failed("No path placement is active.", moveSourceModel),
            };
            if (!result.Success)
            {
                // Preserve the last valid preview. A transient invalid hover near overlapping junction geometry
                // must not erase the route point the user can still see and intends to commit.
                return;
            }

            PathSpanCommitResult spanCommit = EvaluateSpanCommit(result.PathModel, result.ChangedNodeIndexes,
                "Route point preview.", moveSourceModel, AllowsAutomaticReversal(placementMode));
            if (spanCommit.Status == PathSpanCommitStatus.Unresolved)
            {
                // Keep the last resolved preview committable while the pointer crosses an unresolved sliver.
                return;
            }

            movePreviewAnchor = replacementAnchor;
            movePreviewAuthoringModel = placementMode == PathEditorPlacementMode.BuildRoute ? result.PathModel : null;
            movePreviewSpanCommit = spanCommit;
            movePreviewModel = spanCommit.PathModel;
            SetPreviewPath(movePreviewModel);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        private void ClearMovePreview()
        {
            movePreviewAnchor = null;
            movePreviewModel = null;
            movePreviewSpanCommit = null;
            SetPreviewPath(null);
        }

        public bool CancelMoveNode()
            => CancelPlacement();

        public bool CancelPlacement()
        {
            if (!IsPlacementActive)
                return false;

            pendingAmbiguousSpanCommit = null;
            ClearPreviewedRouteCandidate();

            int movedNodeIndex = movingNodeIndex;
            PathModel viaSourceModel = pendingViaSourceModel;
            bool viaSourceEditMode = pendingViaSourceEditMode;
            bool viaSourceUnsavedChanges = pendingViaSourceUnsavedChanges;
            bool sourceEditMode = placementSourceEditMode;
            bool sourceUnsavedChanges = placementSourceUnsavedChanges;
            PathModel currentModel = TryGetEditablePathModel();
            bool canceledBuildRoute = IsBuildingRoute;
            ClearMoveNodeState();
            if (canceledBuildRoute)
                routeAuthoringModel = null;
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
                RestorePath(currentModel, sourceEditMode);
                unsavedChanges = sourceUnsavedChanges;
                SelectPathItem(movedNodeIndex >= 0 && movedNodeIndex < currentModel.PathNodes.Length ? movedNodeIndex : -1);
            }

            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

        public PathEditorCommandResult CancelMoveNodeCommand()
        {
            return CancelPlacementCommand();
        }

        public PathEditorCommandResult CancelPlacementCommand()
        {
            PathEditorPlacementMode canceledMode = placementMode;
            return CancelPlacement()
                ? PathEditorCommandResult.Succeeded(canceledMode switch
                {
                    PathEditorPlacementMode.StartAnchor => "Start anchor placement canceled.",
                    PathEditorPlacementMode.EndAnchor => "End anchor placement canceled.",
                    _ => "Node move canceled.",
                }, currentPathModel)
                : PathEditorCommandResult.Failed("No path placement is active.", currentPathModel);
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

        // Re-snaps the model to the current track and rebuilds its route, weaving any resolved passing branches
        // back into the generated graph. This is the whole-path "fix broken path" operation: each node is
        // re-anchored via its hybrid anchor (stored track node index first, stored location when that index no
        // longer matches the layout, see PathRouteResolver.ResolveTrackNodeIndex), so a path stays usable after
        // the underlying track database changed. On resolver/generation failure (including passing shapes the
        // generator cannot represent, such as non-rejoining branches) the original model is returned unchanged
        // with the reason.
        internal static PathEditResult SnapPathToTrack(PathModel model, TrackWorld trackWorld)
        {
            PathGenerationResult generation = GenerateTrackSnappedPath(model, trackWorld);
            return generation.Success
                ? PathEditResult.Succeeded(generation.Message, generation.PathModel, generation.ChangedNodeIndexes)
                : PathEditResult.Failed(generation.Message, model);
        }

        // Resolves and rebuilds the model using the same materialization policy as normal persistence, including
        // deterministic tie-breaking for warning-only ambiguous spans. Shared by SnapToTrack and snap-on-save.
        internal static PathGenerationResult GenerateTrackSnappedPath(PathModel model, TrackWorld trackWorld)
        {
            PathPersistenceValidationResult materialization = PathPersistenceValidationPolicy.ValidateForPersistence(model, trackWorld);
            return materialization.PersistenceAllowed
                ? PathGenerationResult.Succeeded("Path generated.", materialization.PathModel, materialization.Diagnostics, materialization.ChangedNodeIndexes)
                : PathGenerationResult.Failed(materialization.FailureMessage, model, materialization.Diagnostics);
        }
        #endregion

        internal PathSaveOperation BeginSave(PathModelHeader pathDetails)
        {
            ArgumentNullException.ThrowIfNull(pathDetails);

            if (activeSaveOperation != null)
            {
                return new PathSaveOperation(false, currentPathModel, currentPathModel?.Id, Task.FromResult(new PathPersistenceValidationResult(false,
                    currentPathModel, null, default, default, "A save is already in progress for this path.", null)));
            }

            PathModel sourceModel = currentPathModel;
            PathModel pathModel = new PathModel(pathDetails) { PathNodes = sourceModel?.PathNodes ?? ImmutableArray<PathNode>.Empty };
            PathSaveOperation operation = new PathSaveOperation(true, sourceModel, sourceModel?.Id, PersistSaveAsync(pathModel));
            activeSaveOperation = operation;
            return operation;
        }

        private static async Task<PathPersistenceValidationResult> PersistSaveAsync(PathModel pathModel)
        {
            // The toolbox registers RuntimeDataResolver process-wide only (RuntimeDataResolver.Initialize
            // passes game: null), so Instance is the single authoritative resolver here; a game-scoped
            // GameInstance(game) lookup would resolve to the same object.
            return await SaveValidatedPath(pathModel, RuntimeDataResolver.Instance.RouteData,
                RuntimeDataResolver.Instance.TrackWorld).ConfigureAwait(false);
        }

        /// <summary>
        /// Completes the current save on the game thread. A save result only replaces the editor model when the
        /// captured source is still current; mutations committed while I/O was pending remain dirty.
        /// </summary>
        internal bool CompleteSave(PathSaveOperation operation, PathPersistenceValidationResult validation)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(validation);

            if (!ReferenceEquals(activeSaveOperation, operation))
                return false;

            activeSaveOperation = null;

            if (!validation.PersistenceAllowed)
                return true;

            if (!ReferenceEquals(currentPathModel, operation.SourceModel))
                return true;

            path = validation.PathModel;
            currentPathModel = validation.PathModel;
            unsavedChanges = false;

            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return true;
        }

        /// <summary>Clears the pending save after a persistence failure on the game thread.</summary>
        internal bool CancelSave(PathSaveOperation operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            if (!ReferenceEquals(activeSaveOperation, operation))
                return false;

            activeSaveOperation = null;
            return true;
        }

        internal PathPersistenceValidationResult ValidateCurrentPathForPersistence()
        {
            PathModel pathModel = TryGetEditablePathModel();
            if (pathModel == null)
            {
                return new PathPersistenceValidationResult(false, null, null, default, default,
                    "No editable path is currently loaded.", null);
            }

            return PathPersistenceValidationPolicy.ValidateForPersistence(pathModel, ResolveCurrent(pathModel), TrackWorld);
        }

        internal static async Task<PathPersistenceValidationResult> SaveValidatedPath(PathModel pathModel, RouteModel routeData, TrackWorld trackWorld)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(routeData);

            PathPersistenceValidationResult validation = PathPersistenceValidationPolicy.ValidateForPersistence(pathModel, trackWorld);
            if (!validation.PersistenceAllowed)
                return validation;

            PathModel validatedModel = validation.PathModel with { ValidationState = ResolveValidationState(validation.Resolution) };
            PathModel savedModel = await routeData.Save(validatedModel).ConfigureAwait(false);
            return new PathPersistenceValidationResult(true, savedModel, validation.Resolution, validation.Diagnostics,
                validation.ChangedNodeIndexes, null, null);
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

        public void MouseDragged(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            editorDragged = true;
        }

        public void MouseReleasedLeft(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (IsPlacementActive)
            {
                // Commit only on a genuine click; a drag is a map pan and must not drop the anchor at the
                // drag-release location (mirrors the pointerDraggedSinceLeftPress guard in GameWindow.UserActivity).
                if (!editorDragged)
                {
                    long clickTick = Environment.TickCount64;
                    if (IsBuildingRoute && clickTick - lastRoutePointClickTick <= DoubleClickIntervalMilliseconds)
                    {
                        PathEditorCommandResult result = FinishPathCommand();
                        if (!result.Success)
                            Trace.TraceWarning($"Cannot finish path: {result.Message}");
                        lastRoutePointClickTick = 0;
                    }
                    else
                    {
                        PathEditResult result = CommitPlacement();
                        if (!result.Success)
                            Trace.TraceWarning($"Cannot commit moved path node: {result.Message}");
                        else if (IsBuildingRoute)
                            lastRoutePointClickTick = clickTick;
                    }
                    userCommandArgs.Handled = true;
                }
                editorDragged = false;
                return;
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
            => CommitPlacement();

        public PathEditResult CommitPlacement() => CommitPlacement(true);

        private PathEditResult CommitPlacement(bool continueRouteBuilding)
        {
            PathModel currentModel = TryGetEditablePathModel() ?? moveSourceModel;
            if (currentModel == null)
                return PathEditResult.Failed("No editable path is currently loaded.", null);
            if (!IsPlacementActive)
                return PathEditResult.Failed("No path placement is active.", currentModel);

            PathModel committedModel = movePreviewModel;
            PathModel committedAuthoringModel = movePreviewAuthoringModel;
            PathEditResult result = null;
            if (committedModel == null)
            {
                TrainPathPointBase candidate = ActivePathPoint;
                if (candidate == null || candidate.ValidationResult != PathNodeInvalidReasons.None || candidate.ConnectedSegments.IsDefaultOrEmpty)
                    return PathEditResult.Failed("Select a valid track location for the node.", currentModel);

                PathNode replacementAnchor = CreateReplacementAnchor(candidate);
                bool isJunction = candidate.JunctionNode != null || candidate.NodeType.Includes(PathNodeType.Junction);
                result = placementMode switch
                {
                    PathEditorPlacementMode.MoveNode => PathModelEditor.MoveNode(currentModel, movingNodeIndex, replacementAnchor, isJunction),
                    PathEditorPlacementMode.StartAnchor => PathModelEditor.SetStartAnchor(currentModel, replacementAnchor, isJunction),
                    PathEditorPlacementMode.EndAnchor => PathModelEditor.SetEndAnchor(currentModel, replacementAnchor, isJunction),
                    PathEditorPlacementMode.BuildRoute => AddRoutePoint(currentModel, replacementAnchor, isJunction),
                    _ => PathEditResult.Failed("No path placement is active.", currentModel),
                };
                if (!result.Success)
                    return result;

                committedModel = result.PathModel;
                if (placementMode == PathEditorPlacementMode.BuildRoute)
                    committedAuthoringModel = result.PathModel;
            }

            PathEditorPlacementMode committedMode = placementMode;
            int movedNodeIndex = movingNodeIndex;
            PathModel undoSnapshot = pendingViaSourceModel ?? currentModel;

            // Every placement commit funnels through the unified span-commit routine, so the affected span is
            // resolved and materialized exactly like a directly authored anchor.
            ImmutableArray<int> changedNodeIndexes = result?.ChangedNodeIndexes ?? ImmutableArray.Create(movedNodeIndex);
            PathSpanCommitResult spanCommit = movePreviewSpanCommit
                ?? EvaluateSpanCommit(committedModel, changedNodeIndexes, "Placement committed.", undoSnapshot,
                    AllowsAutomaticReversal(placementMode));
            if (spanCommit.Status == PathSpanCommitStatus.Unresolved)
                return PathEditResult.Failed(spanCommit.Message, currentModel);
            if (spanCommit.Status == PathSpanCommitStatus.Ambiguous)
            {
                BeginPendingAmbiguousSpanCommit(undoSnapshot, spanCommit,
                    committedMode == PathEditorPlacementMode.BuildRoute && continueRouteBuilding);
                return PathEditResult.Failed(spanCommit.Message, currentModel);
            }
            if (spanCommit.Status == PathSpanCommitStatus.Resolved)
                committedModel = spanCommit.PathModel;

            bool beginRouteBuilding = committedMode == PathEditorPlacementMode.StartAnchor
                && !HasFlag(undoSnapshot, PathNodeType.Start);
            PushUndoSnapshot(undoSnapshot);
            unsavedChanges = true;
            ClearMoveNodeState();
            path = committedModel;
            currentPathModel = committedModel;
            if (committedMode == PathEditorPlacementMode.BuildRoute)
                routeAuthoringModel = committedAuthoringModel;
            RestorePath(committedModel, false);
            PathEditResult committedResult = committedMode switch
            {
                PathEditorPlacementMode.StartAnchor => PathEditResult.Succeeded("Start anchor placed.", committedModel, result?.ChangedNodeIndexes ?? ImmutableArray.Create(movedNodeIndex)),
                PathEditorPlacementMode.EndAnchor => PathEditResult.Succeeded("End anchor placed.", committedModel, result?.ChangedNodeIndexes ?? ImmutableArray.Create(movedNodeIndex)),
                PathEditorPlacementMode.BuildRoute => PathEditResult.Succeeded("Route point added.", committedModel, spanCommit.ChangedNodeIndexes),
                _ => PathEditResult.Succeeded($"Moved node {movedNodeIndex}.", committedModel, ImmutableArray.Create(movedNodeIndex)),
            };
            int selectedIndex = committedMode == PathEditorPlacementMode.StartAnchor
                ? IndexOfNodeType(committedModel, PathNodeType.Start, 0)
                : committedMode == PathEditorPlacementMode.EndAnchor
                    ? IndexOfNodeType(committedModel, PathNodeType.End, committedModel.PathNodes.Length - 1)
                    : movedNodeIndex;
            SelectPathItem(selectedIndex);
            if (committedMode is PathEditorPlacementMode.StartAnchor or PathEditorPlacementMode.EndAnchor)
            {
                PathGenerationResult generated = GenerateTrackSnappedPath(committedModel, TrackWorld);
                if (generated.Success)
                    SetPreviewPath(generated.PathModel);
            }
            if (beginRouteBuilding)
                _ = BeginAnchorPlacement(PathEditorPlacementMode.BuildRoute);
            else if (committedMode == PathEditorPlacementMode.BuildRoute && continueRouteBuilding)
                _ = BeginAnchorPlacement(PathEditorPlacementMode.BuildRoute);
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            return committedResult;
        }

        // Only the endpoint-authoring modes advance the route forward, so only they can meaningfully interpret a
        // backwards click as a reversal. Moving an existing node keeps rejecting, because a reversal there would
        // silently change the meaning of a node the user only intended to reposition.
        private static bool AllowsAutomaticReversal(PathEditorPlacementMode mode)
            => mode is PathEditorPlacementMode.BuildRoute or PathEditorPlacementMode.EndAnchor;

        private static PathEditResult AddRoutePoint(PathModel pathModel, PathNode anchor, bool isJunction)
        {
            if (!HasFlag(pathModel, PathNodeType.End))
                return PathModelEditor.SetEndAnchor(pathModel, anchor, isJunction);

            PathEditResult removeEnd = PathModelEditor.RemoveEnd(pathModel);
            if (!removeEnd.Success)
                return removeEnd;

            return PathModelEditor.SetEndAnchor(removeEnd.PathModel, anchor, isJunction);
        }

        public PathEditorCommandResult CommitMoveNodeCommand()
        {
            return PathEditorCommandResult.FromPathEditResult(CommitPlacement());
        }

        public PathEditorCommandResult CommitPlacementCommand()
            => PathEditorCommandResult.FromPathEditResult(CommitPlacement());

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
            placementMode = PathEditorPlacementMode.None;
            movingNodeIndex = -1;
            pendingViaNodeIndex = -1;
            pendingViaSourceModel = null;
            pendingViaSourceEditMode = false;
            pendingViaSourceUnsavedChanges = false;
            placementSourceEditMode = false;
            placementSourceUnsavedChanges = false;
            moveSourceModel = null;
            movePreviewAuthoringModel = null;
            lastRoutePointClickTick = 0;
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
            return TryGetEditablePathModel();
        }

        internal TrainPathBase TryCaptureRenderedPath() => RenderedPath;

        private static int IndexOfNodeType(PathModel pathModel, PathNodeType nodeType, int fallback)
        {
            ImmutableArray<PathNode> nodes = pathModel?.PathNodes ?? ImmutableArray<PathNode>.Empty;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].NodeType.Includes(nodeType))
                    return i;
            }
            return fallback;
        }

        private string BuildSnapshotContext()
        {
            return BuildSnapshotContext(path, TrainPath, EditMode, CanUndo, CanRedo, editorDragged);
        }

        internal static string BuildSnapshotContext(PathModelHeader path, TrainPathBase trainPath, bool editMode, bool canUndo, bool canRedo, bool editorDragged)
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
                + $"EditorDragged={editorDragged}";
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
