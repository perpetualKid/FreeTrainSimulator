using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.PopupWindows;

using DrawingColor = System.Drawing.Color;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Hosted-mode bridge exposing the train-path editor as a dockable WPF tool window. Mirrors the legacy
    /// <c>TrainPathWindow</c> popup: it lists the route's available paths, shows the currently edited path's
    /// nodes and metadata, and lets the user select a path or highlight a node on the map.
    /// <para>
    /// Like the other read-only info bridges it uses a pull/snapshot model: <see cref="RefreshSnapshot"/>
    /// rebuilds an immutable <see cref="TrainPathSnapshot"/> on the game thread and the WPF view model reads
    /// the latest snapshot lock-free through <see cref="CaptureSnapshot"/>. Path selection and node highlight
    /// mutate game-thread state, so they are marshaled back onto the game thread via the supplied invoker
    /// (mirroring <see cref="HostedToolboxMenu"/>).
    /// </para>
    /// </summary>
    internal sealed class TrainPathToolWindow : IToolboxToolWindow
    {
        private readonly Func<PathEditor> pathEditorAccessor;
        private readonly Func<ITrainPathToolingContext> toolingContextAccessor;
        private readonly Action<Action> gameThreadInvoker;
        private readonly Action createPathAction;
        private readonly Action savePathAction;
        private readonly Action<PathModelHeader> loadPathAction;
        private readonly Action unloadPathAction;
        private readonly Action activateMapInputAction;
        private volatile TrainPathSnapshot snapshot = TrainPathSnapshot.Empty;
        private volatile bool active;

        private ImmutableArray<PathModelHeader> cachedPaths = ImmutableArray<PathModelHeader>.Empty;
        private readonly Dictionary<string, PathModel> transientPaths = new Dictionary<string, PathModel>(StringComparer.OrdinalIgnoreCase);
        private string lastPathId;
        private int lastNodeCount = -1;
        private int snapshotVersion;
        private int lastSnapshotVersion = -1;
        private PathPersistenceValidationResult blockedSaveValidation;
        private PathModel blockedSaveSourceModel;
        private int blockedSaveFeedbackVersion;
        private string commandResultMessage;
        private bool commandResultIsWarning;
        private int commandResultVersion;

        internal TrainPathToolWindow(Func<PathEditor> pathEditorAccessor, Func<ITrainPathToolingContext> toolingContextAccessor,
            Action<Action> gameThreadInvoker, Action createPathAction, Action savePathAction, Action<PathModelHeader> loadPathAction,
            Action unloadPathAction, Action activateMapInputAction)
        {
            this.pathEditorAccessor = pathEditorAccessor ?? throw new ArgumentNullException(nameof(pathEditorAccessor));
            this.toolingContextAccessor = toolingContextAccessor ?? throw new ArgumentNullException(nameof(toolingContextAccessor));
            this.gameThreadInvoker = gameThreadInvoker ?? throw new ArgumentNullException(nameof(gameThreadInvoker));
            this.createPathAction = createPathAction ?? throw new ArgumentNullException(nameof(createPathAction));
            this.savePathAction = savePathAction ?? throw new ArgumentNullException(nameof(savePathAction));
            this.loadPathAction = loadPathAction ?? throw new ArgumentNullException(nameof(loadPathAction));
            this.unloadPathAction = unloadPathAction ?? throw new ArgumentNullException(nameof(unloadPathAction));
            this.activateMapInputAction = activateMapInputAction ?? throw new ArgumentNullException(nameof(activateMapInputAction));
        }

        public ToolboxWindowType WindowType => ToolboxWindowType.TrainPathWindow;

        public string Title => "Path Editor";

        internal bool HasUnsavedPathChanges => pathEditorAccessor()?.HasUnsavedChanges == true || transientPaths.Count > 0;

        public bool Active
        {
            get => active;
            set => active = value;
        }

        public ToolWindowSnapshot CaptureSnapshot() => ToolWindowSnapshot.Empty;

        /// <summary>Captures the latest train-path snapshot. Safe to call from the WPF UI thread.</summary>
        internal TrainPathSnapshot CaptureTrainPathSnapshot() => snapshot;

        /// <summary>
        /// Rebuilds the immutable snapshot from the current path editor. Must be called on the game thread; a
        /// no-op while the pane is hidden.
        /// </summary>
        internal void RefreshSnapshot()
        {
            if (!Active)
                return;

            PathEditor pathEditor = pathEditorAccessor();
            if (pathEditor == null)
            {
                ClearBlockedSaveFeedback();

                // No edit session is active, but the available-paths list (and its validation markers) must
                // still be shown and refreshed, e.g. after loading a route or running "Validate All". The path
                // list only changes when UpdatePaths/InvalidatePaths bump snapshotVersion, so rebuild the
                // paths-only snapshot only then; otherwise this branch runs every frame and must stay idle to
                // avoid per-frame allocations.
                int pathsSnapshotVersion = snapshotVersion;
                bool snapshotIsPathsOnly = snapshot.SelectedPathId == null
                    && snapshot.Nodes.IsEmpty
                    && snapshot.Metadata.IsEmpty
                    && !snapshot.CanUndo
                    && !snapshot.CanRedo
                    && !snapshot.CanSnapToTrack
                    && !snapshot.CanCancelMoveNode
                    && !snapshot.CanCommitMoveNode
                    && !snapshot.CanCancelPathInteraction
                    && !snapshot.CanCommitPlacement;
                if (snapshotIsPathsOnly && pathsSnapshotVersion == lastSnapshotVersion)
                    return;

                lastPathId = null;
                lastNodeCount = 0;
                lastSnapshotVersion = pathsSnapshotVersion;
                snapshot = TrainPathSnapshot.Empty with
                {
                    Paths = BuildPaths(null),
                    CommandResultMessage = commandResultMessage,
                    CommandResultIsWarning = commandResultIsWarning,
                    CommandResultVersion = commandResultVersion,
                };
                return;
            }

            TrainPathBase authoredPath = pathEditor.TrainPath;
            PathModel editorPathModel = pathEditor.TryCaptureCurrentPathModel() ?? authoredPath?.PathModel;
            if (blockedSaveValidation != null && !ReferenceEquals(blockedSaveSourceModel, editorPathModel))
                ClearBlockedSaveFeedback();

            PathModel currentPathModel = NormalizeTransientPathModel(editorPathModel);
            ImmutableArray<TrainPathListRow> paths = BuildPaths(currentPathModel);
            bool isRepairMode = pathEditor.IsRepairMode;
            string selectedPathId = currentPathModel?.Id;
            int nodeCount = isRepairMode ? currentPathModel?.PathNodes.Length ?? 0 : authoredPath?.PathPoints.Count ?? 0;
            int selectedNodeIndex = pathEditor.SelectedAuthoredNodeIndex;
            bool canUndo = pathEditor.CanUndo;
            bool canRedo = pathEditor.CanRedo;
            bool canSnapToTrack = pathEditor.CanSnapToTrack;
            bool canCancelMoveNode = pathEditor.IsMovingNode;
            bool canCommitMoveNode = pathEditor.CanCommitMoveNode;
            PathEditorPlacementMode placementMode = pathEditor.PlacementMode;
            bool canCancelPathInteraction = pathEditor.CanCancelPathInteraction;
            bool canCommitPlacement = pathEditor.CanCommitPlacement;
            bool canPlaceStartAnchor = pathEditor.CanPlaceStartAnchor;
            bool canPlaceEndAnchor = pathEditor.CanPlaceEndAnchor;
            bool canMoveSelectedNode = pathEditor.CanMoveNode(selectedNodeIndex);
            bool canRepairSelectedNode = pathEditor.CanRepairNode(selectedNodeIndex);
            bool canRemoveSelectedViaPoint = pathEditor.CanRemoveViaPoint(selectedNodeIndex);
            bool canCancelNewPath = pathEditor.IsNewPath;
            bool isBuildingRoute = pathEditor.IsBuildingRoute;
            bool canFinishPath = isBuildingRoute && pathEditor.CanRemoveEnd;
            bool canBeginPassingBranch = pathEditor.CanBeginPassingBranch(selectedNodeIndex);
            bool canCompletePassingBranch = pathEditor.CanCompletePassingBranch(selectedNodeIndex);
            bool canCancelPassingBranch = pathEditor.CanCancelPassingBranch;
            bool canRemovePassingBranch = pathEditor.CanRemovePassingBranch(selectedNodeIndex);
            bool hasPendingPassingBranchCandidate = pathEditor.HasPendingPassingBranchCandidate;
            PassingBranchAuthoringPhase passingBranchPhase = pathEditor.PassingBranchPhase;

            int currentSnapshotVersion = snapshotVersion;

            // Only rebuild the heavier node/metadata content when the selected path, node count, path list, or
            // editor version changed.
            if (snapshot != TrainPathSnapshot.Empty
                && string.Equals(selectedPathId, lastPathId, StringComparison.Ordinal)
                && nodeCount == lastNodeCount
                && selectedNodeIndex == snapshot.SelectedNodeIndex
                && currentSnapshotVersion == lastSnapshotVersion
                && canUndo == snapshot.CanUndo
                && canRedo == snapshot.CanRedo
                && canSnapToTrack == snapshot.CanSnapToTrack
                && canCancelMoveNode == snapshot.CanCancelMoveNode
                && canCommitMoveNode == snapshot.CanCommitMoveNode
                && placementMode == snapshot.PlacementMode
                && canCancelPathInteraction == snapshot.CanCancelPathInteraction
                && canCommitPlacement == snapshot.CanCommitPlacement
                && canPlaceStartAnchor == snapshot.CanPlaceStartAnchor
                && canPlaceEndAnchor == snapshot.CanPlaceEndAnchor
                && canMoveSelectedNode == snapshot.CanMoveSelectedNode
                && canRepairSelectedNode == snapshot.CanRepairSelectedNode
                && canRemoveSelectedViaPoint == snapshot.CanRemoveSelectedViaPoint
                && canCancelNewPath == snapshot.CanCancelNewPath
                && isBuildingRoute == snapshot.IsBuildingRoute
                && canFinishPath == snapshot.CanFinishPath
                && canBeginPassingBranch == snapshot.CanBeginPassingBranch
                && canCompletePassingBranch == snapshot.CanCompletePassingBranch
                && canCancelPassingBranch == snapshot.CanCancelPassingBranch
                && canRemovePassingBranch == snapshot.CanRemovePassingBranch
                && hasPendingPassingBranchCandidate == snapshot.HasPendingPassingBranchCandidate
                && passingBranchPhase == snapshot.PassingBranchPhase
                && commandResultVersion == snapshot.CommandResultVersion
                && isRepairMode == snapshot.IsRepairMode
                && blockedSaveFeedbackVersion == snapshot.BlockedSaveFeedbackVersion
                && string.Equals(currentPathModel?.Name, snapshot.PathName, StringComparison.Ordinal)
                && string.Equals(currentPathModel?.Start, snapshot.PathStart, StringComparison.Ordinal)
                && string.Equals(currentPathModel?.End, snapshot.PathEnd, StringComparison.Ordinal)
                && currentPathModel?.PlayerPath == snapshot.PlayerPath
                && paths.SequenceEqual(snapshot.Paths))
            {
                return;
            }

            lastPathId = selectedPathId;
            lastNodeCount = nodeCount;
            lastSnapshotVersion = currentSnapshotVersion;
            PathRouteResolution resolution = currentPathModel == null ? null : pathEditor.ResolveCurrent(currentPathModel);
            ImmutableArray<TrainPathDiagnosticRow> diagnostics = BuildResolverDiagnostics(resolution, pathEditor);

            snapshot = new TrainPathSnapshot
            {
                Paths = paths,
                SelectedPathId = selectedPathId,
                Nodes = isRepairMode ? BuildAuthoredNodes(currentPathModel, resolution) : BuildNodes(authoredPath),
                SelectedNodeIndex = selectedNodeIndex,
                Metadata = BuildMetadata(pathEditor, authoredPath, currentPathModel, isRepairMode),
                PathName = currentPathModel?.Name,
                PathStart = currentPathModel?.Start,
                PathEnd = currentPathModel?.End,
                PlayerPath = currentPathModel?.PlayerPath == true,
                RouteCandidates = isRepairMode ? ImmutableArray<TrainPathRouteCandidateRow>.Empty : BuildRouteCandidates(pathEditor),
                Diagnostics = diagnostics,
                IsRepairMode = isRepairMode,
                BlockedSaveMessage = blockedSaveValidation?.FailureMessage,
                BlockedSaveDiagnostic = FindDiagnosticRow(diagnostics, blockedSaveValidation?.HighestActionableDiagnostic),
                BlockedSaveFeedbackVersion = blockedSaveFeedbackVersion,
                CanUndo = canUndo,
                CanRedo = canRedo,
                CanSnapToTrack = canSnapToTrack,
                CanCancelMoveNode = canCancelMoveNode,
                CanCommitMoveNode = canCommitMoveNode,
                PlacementMode = placementMode,
                CanCancelPathInteraction = canCancelPathInteraction,
                CanCommitPlacement = canCommitPlacement,
                CanPlaceStartAnchor = canPlaceStartAnchor,
                CanPlaceEndAnchor = canPlaceEndAnchor,
                CanMoveSelectedNode = canMoveSelectedNode,
                CanRepairSelectedNode = canRepairSelectedNode,
                CanRemoveSelectedViaPoint = canRemoveSelectedViaPoint,
                CanCancelNewPath = canCancelNewPath,
                IsBuildingRoute = isBuildingRoute,
                CanFinishPath = canFinishPath,
                CanBeginPassingBranch = canBeginPassingBranch,
                CanCompletePassingBranch = canCompletePassingBranch,
                CanCancelPassingBranch = canCancelPassingBranch,
                CanRemovePassingBranch = canRemovePassingBranch,
                HasPendingPassingBranchCandidate = hasPendingPassingBranchCandidate,
                PassingBranchPhase = passingBranchPhase,
                CommandResultMessage = commandResultMessage,
                CommandResultIsWarning = commandResultIsWarning,
                CommandResultVersion = commandResultVersion,
            };
        }

        /// <summary>
        /// Selects (loads) the path with the given id for editing, or clears the selection when
        /// <paramref name="pathId"/> is null/empty. Returns nothing; failures are reported through the next
        /// snapshot (the path will not become selected) and surfaced by the view model as a status message.
        /// Safe to call from the WPF UI thread.
        /// </summary>
        internal void SelectPath(string pathId)
        {
            gameThreadInvoker(() =>
            {
                CaptureTransientCurrentPath();

                if (string.IsNullOrEmpty(pathId))
                {
                    unloadPathAction();
                    MarkDirty();
                    return;
                }

                PathModelHeader path = transientPaths.TryGetValue(pathId, out PathModel transientPath)
                    ? transientPath
                    : cachedPaths.FirstOrDefault(p => string.Equals(p.Id, pathId, StringComparison.OrdinalIgnoreCase));
                if (path == null)
                {
                    commandResultMessage = $"Path '{pathId}' is no longer available.";
                    commandResultIsWarning = true;
                    commandResultVersion++;
                    MarkDirty();
                    return;
                }

                loadPathAction(path);
                MarkDirty();
            });
        }

        internal void StartNewPathAt(PathNode anchor, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(anchor);

            gameThreadInvoker(() =>
            {
                CaptureTransientCurrentPath();
                createPathAction();
                PathEditor pathEditor = pathEditorAccessor();
                if (pathEditor == null)
                    return;

                PathEditorCommandResult result = pathEditor.SetStartAnchorCommand(anchor, isJunction);
                if (!result.Success)
                    Trace.TraceWarning(result.Message);
                MarkDirty();
            });
        }

        internal void HighlightDiagnosticTarget(int nodeIndex, int fromNodeIndex, int toNodeIndex)
            => InvokeEditorAction(pathEditor => pathEditor.HighlightDiagnosticTarget(nodeIndex, fromNodeIndex, toNodeIndex));

        internal void RepairDiagnosticNode(int nodeIndex)
            => ExecuteEditorCommand(pathEditor => pathEditor.RepairSelectedNodeCommand(nodeIndex));

        /// <summary>
        /// Highlights the path node with the given index on the map (or clears the highlight when negative).
        /// Safe to call from the WPF UI thread.
        /// </summary>
        internal void HighlightNode(int index)
        {
            InvokeEditorAction(pathEditor =>
            {
                pathEditor.SelectAuthoredNode(index);
            });
        }

        internal void Undo() => InvokeEditorMutation(pathEditor => pathEditor.Undo());

        internal void Redo() => InvokeEditorMutation(pathEditor => pathEditor.Redo());

        internal void CancelPathInteraction() => ExecuteEditorCommand(pathEditor => pathEditor.CancelPathInteractionCommand());

        internal void RemoveSelectedViaPoint()
            => ExecuteEditorCommand(pathEditor => pathEditor.RemoveViaPointCommand(pathEditor.SelectedAuthoredNodeIndex));

        internal void CycleRouteCandidate(int direction)
            => ExecuteEditorCommand(pathEditor => pathEditor.CycleRouteCandidateCommand(direction));

        internal void AcceptPreviewedRouteCandidate()
            => ExecuteEditorCommand(pathEditor => pathEditor.AcceptPreviewedRouteCandidateCommand());

        internal void SnapToTrack() => ExecuteEditorCommand(pathEditor => pathEditor.ReResolvePathCommand());

        internal void ContinuePath() => ExecuteEditorCommand(pathEditor => pathEditor.ContinuePathCommand(), activateMapInputAction);

        internal void AddRoutePointHere(PathNode anchor, bool isJunction, bool finishPath)
            => ExecuteEditorCommand(pathEditor => pathEditor.AddRoutePointHereCommand(anchor, isJunction, finishPath));

        internal void FinishPath() => ExecuteEditorCommand(pathEditor => pathEditor.FinishPathCommand());

        internal void BeginMoveNode(int nodeIndex)
        {
            ExecuteEditorCommand(pathEditor => pathEditor.BeginMoveNodeCommand(nodeIndex), activateMapInputAction);
        }

        internal void CommitMoveNode() => ExecuteEditorCommand(pathEditor => pathEditor.CommitMoveNodeCommand());

        internal void CancelMoveNode() => ExecuteEditorCommand(pathEditor => pathEditor.CancelMoveNodeCommand());

        internal void BeginStartAnchorPlacement()
            => ExecuteEditorCommand(pathEditor => pathEditor.BeginStartAnchorPlacementCommand(), activateMapInputAction);

        internal void BeginEndAnchorPlacement()
            => ExecuteEditorCommand(pathEditor => pathEditor.BeginEndAnchorPlacementCommand(), activateMapInputAction);

        internal void SetStartAnchor(PathNode anchor, bool isJunction)
            => ExecuteEditorCommand(pathEditor => pathEditor.SetStartAnchorCommand(anchor, isJunction));

        internal void SetEndAnchor(PathNode anchor, bool isJunction)
            => ExecuteEditorCommand(pathEditor => pathEditor.SetEndAnchorCommand(anchor, isJunction));

        internal void CancelPlacement() => ExecuteEditorCommand(pathEditor => pathEditor.CancelPlacementCommand());

        internal void CommitPlacement() => ExecuteEditorCommand(pathEditor => pathEditor.CommitPlacementCommand());

        internal void RepairSelectedNode(int nodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.RepairSelectedNodeCommand(nodeIndex));

        internal void SetWaitPoint(int nodeIndex, int waitTimeSeconds) => ExecuteEditorCommand(pathEditor => pathEditor.SetWaitPointCommand(nodeIndex, waitTimeSeconds));

        internal void ClearWaitPoint(int nodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.ClearWaitPointCommand(nodeIndex));

        internal void RemoveRestOfPath(int nodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.RemoveRestOfPathCommand(nodeIndex));

        internal void SetReversalPoint(int nodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.SetReversalPointCommand(nodeIndex));

        internal void ClearReversalPoint(int nodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.ClearReversalPointCommand(nodeIndex));

        internal void BeginViaPointPlacement(int afterNodeIndex)
            => ExecuteEditorCommand(pathEditor => pathEditor.BeginViaPointPlacementCommand(afterNodeIndex), activateMapInputAction);

        internal void BeginViaPointPlacementAt(int afterNodeIndex, PathNode anchor)
            => ExecuteEditorCommand(pathEditor => pathEditor.BeginViaPointPlacementAtCommand(afterNodeIndex, anchor), activateMapInputAction);

        internal void AddViaPointHere(int afterNodeIndex, PathNode anchor, bool isJunction)
            => ExecuteEditorCommand(pathEditor => pathEditor.AddViaPointHereCommand(afterNodeIndex, anchor, isJunction));

        internal void RemoveViaPoint(int nodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.RemoveViaPointCommand(nodeIndex));

        internal void BeginPassingBranch(int startNodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.BeginPassingBranchCommand(startNodeIndex));

        internal void CompletePassingBranch(int rejoinNodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.CompletePassingBranchCommand(rejoinNodeIndex));

        internal void CancelPassingBranch() => ExecuteEditorCommand(pathEditor => pathEditor.CancelPassingBranchCommand());

        internal void RemovePassingBranch(int startNodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.RemovePassingBranchCommand(startNodeIndex));

        internal void PreviewRouteCandidate(int fromNodeIndex, int candidateIndex) => ExecuteEditorCommand(pathEditor => pathEditor.PreviewRouteCandidateCommand(fromNodeIndex, candidateIndex));

        internal void ClearRouteCandidatePreview() => InvokeEditorAction(pathEditor => pathEditor.ClearRouteCandidatePreview());

        internal void AcceptRouteCandidate(int fromNodeIndex, int candidateIndex) => ExecuteEditorCommand(pathEditor => pathEditor.AcceptRouteCandidateCommand(fromNodeIndex, candidateIndex));

        internal bool CanCreatePath => toolingContextAccessor() != null;

        internal bool CanValidatePaths => toolingContextAccessor() != null;

        internal bool CanSavePath
        {
            get
            {
                PathEditor pathEditor = pathEditorAccessor();
                return pathEditor?.TrainPath != null && pathEditor.HasUnsavedChanges && !pathEditor.IsSaveInProgress
                    && !pathEditor.CanCancelPathInteraction;
            }
        }

        internal bool CanCancelNewPath => pathEditorAccessor()?.IsNewPath == true;

        internal void CreatePath()
        {
            gameThreadInvoker(() =>
            {
                CaptureTransientCurrentPath();
                createPathAction();
                MarkDirty();
            });
        }

        /// <summary>
        /// Cancels the active unsaved New Path model. Persisted paths and their transient edits are unaffected.
        /// </summary>
        internal void CancelNewPath()
        {
            gameThreadInvoker(() =>
            {
                PathEditor pathEditor = pathEditorAccessor();
                if (pathEditor?.IsNewPath != true)
                {
                    PublishCommandResult(PathEditorCommandResult.Failed("No unsaved new path is active.", pathEditor?.TryCaptureCurrentPathModel()));
                    MarkDirty();
                    return;
                }

                PathModel canceledPath = pathEditor.TryCaptureCurrentPathModel();
                transientPaths.Remove(PathEditor.NewPathId);
                unloadPathAction();
                PublishCommandResult(PathEditorCommandResult.Succeeded("New path canceled.", canceledPath));
                MarkDirty();
            });
        }

        internal void StartNewPathPlacement()
        {
            gameThreadInvoker(() =>
            {
                CaptureTransientCurrentPath();
                createPathAction();
                PathEditor pathEditor = pathEditorAccessor();
                if (pathEditor == null)
                {
                    PathEditorCommandResult unavailable = PathEditorCommandResult.Failed("Cannot start a new path because no path editor is active.", null);
                    PublishCommandResult(unavailable);
                    Trace.TraceWarning(unavailable.Message);
                    MarkDirty();
                    return;
                }

                PathEditorCommandResult result = pathEditor.BeginStartAnchorPlacementCommand();
                PublishCommandResult(result);
                if (result.Success)
                    activateMapInputAction();
                else
                    Trace.TraceWarning(result.Message);
                MarkDirty();
            });
        }

        internal void SavePath()
        {
            gameThreadInvoker(() =>
            {
                PathEditor pathEditor = pathEditorAccessor();
                if (pathEditor == null)
                    return;

                PathModel currentModel = pathEditor.TryCaptureCurrentPathModel();
                if (currentModel == null || string.IsNullOrWhiteSpace(currentModel.Name)
                    || currentModel.Name.Trim().IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0
                    || currentModel.Name.Trim() is "." or "..")
                {
                    commandResultMessage = "Enter a path name without filename-invalid characters before saving.";
                    commandResultIsWarning = true;
                    commandResultVersion++;
                    MarkDirty();
                    return;
                }

                PathPersistenceValidationResult validation = pathEditor.ValidateCurrentPathForPersistence();
                if (!validation.PersistenceAllowed)
                {
                    ReportBlockedSave(validation, pathEditor.TryCaptureCurrentPathModel());
                    return;
                }

                ClearBlockedSaveFeedback();
                savePathAction();
                MarkDirty();
            });
        }

        internal void SetMetadata(string name, string start, string end, bool playerPath)
            => ExecuteEditorCommand(pathEditor => pathEditor.SetMetadataCommand(name, start, end, playerPath));

        internal ImmutableArray<MapContextMenuItem> GetNodeActions(int nodeIndex)
        {
            PathEditor pathEditor = pathEditorAccessor();
            PathModel model = pathEditor?.TryCaptureCurrentPathModel();
            if (model == null || nodeIndex < 0 || nodeIndex >= model.PathNodes.Length)
                return ImmutableArray<MapContextMenuItem>.Empty;

            PathNode node = model.PathNodes[nodeIndex];
            MapContextMenuActionBuilder.MapContextMenuState state = new()
            {
                CanBeginPassingBranch = pathEditor.CanBeginPassingBranch(nodeIndex),
                CanCompletePassingBranch = pathEditor.CanCompletePassingBranch(nodeIndex),
                CanCancelPassingBranch = pathEditor.CanCancelPassingBranch,
                CanRemovePassingBranch = pathEditor.CanRemovePassingBranch(nodeIndex),
                IsPlacementActive = pathEditor.IsPlacementActive,
                CanMoveNode = pathEditor.CanMoveNode(nodeIndex),
                CanClearWaitPoint = pathEditor.CanClearWaitPoint(nodeIndex),
                CanSetReversalPoint = pathEditor.CanSetReversalPoint(nodeIndex),
                CanClearReversalPoint = pathEditor.CanClearReversalPoint(nodeIndex),
                CanRemoveViaPoint = pathEditor.CanRemoveViaPoint(nodeIndex),
                CanRepairNode = pathEditor.CanRepairNode(nodeIndex),
                CanRemoveRestOfPath = pathEditor.CanRemoveRestOfPath(nodeIndex),
            };
            return MapContextMenuActionBuilder.BuildNodeActions(nodeIndex, state);
        }

        internal void ExecuteNodeAction(MapContextMenuAction action, int nodeIndex)
        {
            switch (action)
            {
                case MapContextMenuAction.MoveNode:
                    BeginMoveNode(nodeIndex);
                    break;
                case MapContextMenuAction.CancelPlacement:
                    CancelPlacement();
                    break;
                case MapContextMenuAction.ClearWaitPoint:
                    ClearWaitPoint(nodeIndex);
                    break;
                case MapContextMenuAction.SetReversalPoint:
                    SetReversalPoint(nodeIndex);
                    break;
                case MapContextMenuAction.ClearReversalPoint:
                    ClearReversalPoint(nodeIndex);
                    break;
                case MapContextMenuAction.RemoveViaPoint:
                    RemoveViaPoint(nodeIndex);
                    break;
                case MapContextMenuAction.RepairNode:
                    RepairSelectedNode(nodeIndex);
                    break;
                case MapContextMenuAction.StartPassingBranch:
                    BeginPassingBranch(nodeIndex);
                    break;
                case MapContextMenuAction.RejoinPassingBranch:
                    CompletePassingBranch(nodeIndex);
                    break;
                case MapContextMenuAction.CancelPassingBranch:
                    CancelPassingBranch();
                    break;
                case MapContextMenuAction.RemovePassingBranch:
                    RemovePassingBranch(nodeIndex);
                    break;
                case MapContextMenuAction.RemoveRestOfPath:
                    RemoveRestOfPath(nodeIndex);
                    break;
                default:
                    Trace.TraceWarning($"Unsupported path node action {action}.");
                    break;
            }
        }

        internal void ReportBlockedSave(PathPersistenceValidationResult validation, PathModel sourceModel)
        {
            ArgumentNullException.ThrowIfNull(validation);
            ArgumentNullException.ThrowIfNull(sourceModel);

            blockedSaveValidation = validation;
            blockedSaveSourceModel = sourceModel;
            blockedSaveFeedbackVersion++;
            MarkDirty();
        }

        private void ClearBlockedSaveFeedback()
        {
            if (blockedSaveValidation == null)
                return;

            blockedSaveValidation = null;
            blockedSaveSourceModel = null;
            blockedSaveFeedbackVersion++;
        }

        private void ExecuteEditorCommand(Func<PathEditor, PathEditorCommandResult> command, Action onSuccess = null)
        {
            ArgumentNullException.ThrowIfNull(command);

            gameThreadInvoker(() =>
            {
                PathEditor pathEditor = pathEditorAccessor();
                if (pathEditor == null)
                {
                    Trace.TraceWarning("Cannot execute path editor command because no path editor is active.");
                    return;
                }

                PathEditorCommandResult result = command(pathEditor);
                PublishCommandResult(result);
                MarkDirty();
                if (result.Success)
                {
                    onSuccess?.Invoke();
                    return;
                }

                Trace.TraceWarning(result.Message);
            });
        }

        private void PublishCommandResult(PathEditorCommandResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            commandResultMessage = result.Message;
            commandResultIsWarning = !result.Success;
            commandResultVersion++;
        }

        // Marshals onto the game thread, resolves the current (possibly not-yet-created) editor, and runs
        // <paramref name="mutate"/> against it, marking the snapshot dirty when the mutation reports a change.
        // No-op when no editor exists yet.
        private void InvokeEditorMutation(Func<PathEditor, bool> mutate)
        {
            gameThreadInvoker(() =>
            {
                PathEditor pathEditor = pathEditorAccessor();
                if (pathEditor != null && mutate(pathEditor))
                    MarkDirty();
            });
        }

        // Marshals onto the game thread, resolves the current (possibly not-yet-created) editor, and runs
        // <paramref name="action"/> against it. No-op when no editor exists yet. Unlike InvokeEditorMutation this
        // does not mark the snapshot dirty; callers that change snapshot-relevant state do so explicitly.
        private void InvokeEditorAction(Action<PathEditor> action)
        {
            gameThreadInvoker(() =>
            {
                PathEditor pathEditor = pathEditorAccessor();
                if (pathEditor != null)
                    action(pathEditor);
            });
        }

        /// <summary>
        /// Forces revalidation of every path for the loaded route against the current track, persisting the
        /// updated validity flags, then refreshes the cached path list so the review markers update. Awaitable so
        /// the caller can surface failures; the cache update is marshalled back onto the game thread.
        /// </summary>
        internal async Task<bool> ValidateAllPaths()
        {
            ITrainPathToolingContext toolingContext = toolingContextAccessor();
            if (toolingContext == null)
                return false;

            ImmutableArray<PathModelHeader> paths = await toolingContext.ValidateAllPaths().ConfigureAwait(false);
            gameThreadInvoker(() => UpdatePaths(paths));
            return true;
        }

        private ImmutableArray<TrainPathListRow> BuildPaths(PathModel currentPathModel)
        {
            return BuildPathRows(cachedPaths, transientPaths.Values.ToImmutableArray(), currentPathModel,
                pathEditorAccessor()?.HasUnsavedChanges == true);
        }

        internal static ImmutableArray<TrainPathListRow> BuildPathRows(ImmutableArray<PathModelHeader> savedPaths, PathModel currentPathModel)
        {
            return BuildPathRows(savedPaths, ImmutableArray<PathModel>.Empty, currentPathModel, false);
        }

        internal static ImmutableArray<TrainPathListRow> BuildPathRows(ImmutableArray<PathModelHeader> savedPaths, ImmutableArray<PathModel> transientPaths, PathModel currentPathModel)
        {
            return BuildPathRows(savedPaths, transientPaths, currentPathModel, false);
        }

        /// <summary>
        /// Builds the available-paths rows. A row is flagged as having unsaved changes when it is a transient
        /// (not yet persisted) path, or when it is the path currently open in the editor and the editor reports
        /// pending edits.
        /// </summary>
        internal static ImmutableArray<TrainPathListRow> BuildPathRows(ImmutableArray<PathModelHeader> savedPaths, ImmutableArray<PathModel> transientPaths,
            PathModel currentPathModel, bool currentPathHasUnsavedChanges)
        {
            savedPaths = savedPaths.IsDefault ? ImmutableArray<PathModelHeader>.Empty : savedPaths;
            transientPaths = transientPaths.IsDefault ? ImmutableArray<PathModel>.Empty : transientPaths;
            ImmutableArray<TrainPathListRow>.Builder builder = ImmutableArray.CreateBuilder<TrainPathListRow>();

            bool IsCurrentPath(string pathId)
                => currentPathModel != null && string.Equals(currentPathModel.Id, pathId, StringComparison.OrdinalIgnoreCase);

            if (currentPathModel != null && !savedPaths.Any(path => string.Equals(path.Id, currentPathModel.Id, StringComparison.OrdinalIgnoreCase)))
            {
                // Never persisted, so it always carries unsaved changes.
                builder.Add(new TrainPathListRow(currentPathModel.Id, currentPathModel.Name, currentPathModel.ValidationState, true));
            }

            foreach (PathModel transientPath in transientPaths.OrderBy(path => path.Name))
            {
                if (IsCurrentPath(transientPath.Id))
                    continue;
                if (savedPaths.Any(path => string.Equals(path.Id, transientPath.Id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                builder.Add(new TrainPathListRow(transientPath.Id, transientPath.Name, transientPath.ValidationState, true));
            }

            foreach (PathModelHeader path in savedPaths.OrderBy(p => p.Name))
            {
                bool isTransient = transientPaths.Any(transientPath => string.Equals(transientPath.Id, path.Id, StringComparison.OrdinalIgnoreCase));
                PathModelHeader rowPath = IsCurrentPath(path.Id)
                    ? currentPathModel
                    : transientPaths.FirstOrDefault(transientPath => string.Equals(transientPath.Id, path.Id, StringComparison.OrdinalIgnoreCase)) ?? path;
                bool hasUnsavedChanges = isTransient || (IsCurrentPath(path.Id) && currentPathHasUnsavedChanges);
                builder.Add(new TrainPathListRow(rowPath.Id, rowPath.Name, rowPath.ValidationState, hasUnsavedChanges));
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<TrainPathNodeRow> BuildAuthoredNodes(PathModel pathModel, PathRouteResolution resolution)
        {
            if (pathModel == null)
                return ImmutableArray<TrainPathNodeRow>.Empty;

            ImmutableArray<TrainPathNodeRow>.Builder builder = ImmutableArray.CreateBuilder<TrainPathNodeRow>(pathModel.PathNodes.Length);
            for (int i = 0; i < pathModel.PathNodes.Length; i++)
            {
                PathNode node = pathModel.PathNodes[i];
                string validation = string.Join("; ", resolution?.Diagnostics
                    .Where(diagnostic => diagnostic.NodeIndex == i || diagnostic.FromNodeIndex == i || diagnostic.ToNodeIndex == i)
                    .Select(diagnostic => diagnostic.Message) ?? Enumerable.Empty<string>());
                builder.Add(new TrainPathNodeRow(i, node.NodeType, string.IsNullOrEmpty(validation), node.NodeIndex,
                    node.NextMainNode, node.NextSidingNode, node.WaitInfo?.WaitTime, validation, null, null, null));
            }

            return builder.ToImmutable();
        }

        private static TrainPathDiagnosticRow? FindDiagnosticRow(ImmutableArray<TrainPathDiagnosticRow> diagnostics, PathRouteDiagnostic diagnostic)
        {
            if (diagnostic == null)
                return null;

            foreach (TrainPathDiagnosticRow row in diagnostics)
            {
                if (row.Code == diagnostic.Code
                    && row.NodeIndex == diagnostic.NodeIndex
                    && row.FromNodeIndex == diagnostic.FromNodeIndex
                    && row.ToNodeIndex == diagnostic.ToNodeIndex)
                {
                    return row;
                }
            }

            return null;
        }

        private static ImmutableArray<TrainPathNodeRow> BuildNodes(TrainPathBase currentPath)
        {
            if (currentPath == null)
                return ImmutableArray<TrainPathNodeRow>.Empty;

            ImmutableArray<TrainPathNodeRow>.Builder builder = ImmutableArray.CreateBuilder<TrainPathNodeRow>();
            for (int i = 0; i < currentPath.PathPoints.Count; i++)
            {
                TrainPathPointBase item = currentPath.PathPoints[i];
                PathNodeInvalidReasons validationResult = item.ValidationResult;
                TrackDistanceDiagnostic nearestTrackDistance = item.NearestTrackDistance;
                builder.Add(new TrainPathNodeRow(i, item.NodeType, validationResult == PathNodeInvalidReasons.None,
                    item.NodeIndex, item.NextMainNode, item.NextSidingNode, item.WaitInfo?.WaitTime,
                    validationResult == PathNodeInvalidReasons.None ? null : validationResult.ToString(),
                    nearestTrackDistance?.TrackNodeIndex, nearestTrackDistance?.TrackVectorSectionIndex, nearestTrackDistance?.DistanceMeters));
            }
            return builder.ToImmutable();
        }

        // Flattens the ambiguous spans of the current path into selectable candidate rows. Runs on the game
        // thread as part of the snapshot capture, so the view model never touches resolver state directly.
        private static ImmutableArray<TrainPathRouteCandidateRow> BuildRouteCandidates(PathEditor pathEditor)
        {
            if (pathEditor == null)
                return ImmutableArray<TrainPathRouteCandidateRow>.Empty;

            ImmutableArray<TrainPathRouteCandidateRow>.Builder builder = ImmutableArray.CreateBuilder<TrainPathRouteCandidateRow>();
            foreach (ResolvedPathSpan span in pathEditor.GetAmbiguousSpans())
            {
                for (int i = 0; i < span.Candidates.Length; i++)
                {
                    string route = string.Join(" - ", span.Candidates[i].RouteNodeIndexes);
                    builder.Add(new TrainPathRouteCandidateRow(span.FromNodeIndex, span.ToNodeIndex, i,
                        $"Nodes {span.FromNodeIndex}-{span.ToNodeIndex}, candidate {i + 1}: {route}"));
                }
            }
            return builder.ToImmutable();
        }

        private ImmutableArray<ToolWindowRow> BuildMetadata(PathEditor pathEditor, TrainPathBase currentPath, PathModel currentPathModel, bool isRepairMode)
        {
            PathModel pathModel = currentPath?.PathModel ?? currentPathModel;
            if (pathModel == null)
                return ImmutableArray<ToolWindowRow>.Empty;

            ITrainPathToolingContext toolingContext = toolingContextAccessor();
            bool metricUnits = toolingContext?.UseMetricUnits ?? true;
            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();
            builder.Add(new ToolWindowRow { Name = "Editor Mode", Value = isRepairMode ? "Repair" : "Normal", Color = isRepairMode ? DrawingColor.OrangeRed : null, Bold = isRepairMode });
            builder.Add(new ToolWindowRow { Name = "Path ID", Value = pathModel.Id });
            if (currentPath != null)
            {
                builder.Add(new ToolWindowRow { Name = "Path Length", Value = FormatStrings.FormatDistanceDisplay(currentPath.Length, metricUnits, 1000) });
                builder.AddRange(BuildEditorStateMetadata(currentPath));
            }
            else
            {
                builder.Add(new ToolWindowRow { Name = "Authored Node Count", Value = pathModel.PathNodes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                builder.Add(new ToolWindowRow { Name = "Runtime Route", Value = "Not constructed", Color = DrawingColor.OrangeRed, Bold = true });
            }
            builder.AddRange(BuildEditorHistoryMetadata(pathEditor?.CanUndo == true, pathEditor?.CanRedo == true));
            return builder.ToImmutable();
        }

        internal static ImmutableArray<ToolWindowRow> BuildEditorHistoryMetadata(bool canUndo, bool canRedo)
        {
            return ImmutableArray.Create(
                new ToolWindowRow { Name = "Can Undo", Value = FormatStrings.FormatYesNo(canUndo) },
                new ToolWindowRow { Name = "Can Redo", Value = FormatStrings.FormatYesNo(canRedo) });
        }

        internal static ImmutableArray<ToolWindowRow> BuildEditorStateMetadata(TrainPathBase currentPath)
        {
            if (currentPath == null)
                return ImmutableArray<ToolWindowRow>.Empty;

            bool hasEnd = currentPath.PathPoints.Any(point => point.NodeType.Includes(PathNodeType.End));
            bool hasBrokenNodes = currentPath.PathPoints.Any(point => point.ValidationResult != PathNodeInvalidReasons.None);
            bool hasPassingPaths = currentPath.PathPoints.Any(point => point.NextSidingNode >= 0);
            bool hasWaitNodes = currentPath.PathPoints.Any(point => point.NodeType.Includes(PathNodeType.Wait) || point.WaitInfo != null);
            bool hasReversalNodes = currentPath.PathPoints.Any(point => point.NodeType.Includes(PathNodeType.Reversal));

            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();
            builder.Add(new ToolWindowRow { Name = "Node Count", Value = currentPath.PathPoints.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            builder.Add(new ToolWindowRow { Name = "Has End", Value = FormatStrings.FormatYesNo(hasEnd) });
            builder.Add(new ToolWindowRow { Name = "Has Broken Nodes", Value = FormatStrings.FormatYesNo(hasBrokenNodes), Color = hasBrokenNodes ? DrawingColor.OrangeRed : null, Bold = hasBrokenNodes });
            builder.Add(new ToolWindowRow { Name = "Has Passing Paths", Value = FormatStrings.FormatYesNo(hasPassingPaths) });
            builder.Add(new ToolWindowRow { Name = "Has Wait Nodes", Value = FormatStrings.FormatYesNo(hasWaitNodes) });
            builder.Add(new ToolWindowRow { Name = "Has Reversal Nodes", Value = FormatStrings.FormatYesNo(hasReversalNodes) });
            return builder.ToImmutable();
        }

        internal static ImmutableArray<TrainPathDiagnosticRow> BuildResolverDiagnostics(PathRouteResolution resolution)
            => BuildResolverDiagnostics(resolution, null);

        private static ImmutableArray<TrainPathDiagnosticRow> BuildResolverDiagnostics(PathRouteResolution resolution, PathEditor pathEditor)
        {
            if (resolution == null || resolution.Diagnostics.IsDefaultOrEmpty)
                return ImmutableArray<TrainPathDiagnosticRow>.Empty;

            ImmutableArray<TrainPathDiagnosticRow>.Builder builder = ImmutableArray.CreateBuilder<TrainPathDiagnosticRow>(resolution.Diagnostics.Length);
            foreach (PathRouteDiagnostic diagnostic in resolution.Diagnostics)
            {
                builder.Add(new TrainPathDiagnosticRow(diagnostic.Severity, diagnostic.Code, diagnostic.Message,
                    diagnostic.NodeIndex, diagnostic.FromNodeIndex, diagnostic.ToNodeIndex, diagnostic.SuggestedAction,
                    diagnostic.NodeIndex >= 0 && pathEditor?.CanRepairNode(diagnostic.NodeIndex) == true));
            }

            return builder.ToImmutable();
        }

        /// <summary>
        /// Invalidates the cached path list so the next refresh re-queries the route. Called on the game
        /// thread when the route/path editor availability changes.
        /// </summary>
        internal void InvalidatePaths()
        {
            ClearBlockedSaveFeedback();
            cachedPaths = ImmutableArray<PathModelHeader>.Empty;
            transientPaths.Clear();
            lastPathId = null;
            lastNodeCount = -1;
            lastSnapshotVersion = -1;
            snapshot = TrainPathSnapshot.Empty;
            MarkDirty();
        }

        /// <summary>
        /// Updates the cached route path list without blocking snapshot refresh. Called on the game thread
        /// after route path loading completes.
        /// </summary>
        internal void UpdatePaths(ImmutableArray<PathModelHeader> paths)
        {
            cachedPaths = paths.IsDefault ? ImmutableArray<PathModelHeader>.Empty : paths;
            foreach (PathModelHeader path in cachedPaths)
                transientPaths.Remove(path.Id);
            MarkDirty();
        }

        /// <summary>Removes transient identities replaced by a successful persisted save.</summary>
        internal void CompleteSavedPath(string sourcePathId, string savedPathId)
        {
            if (!string.IsNullOrWhiteSpace(sourcePathId))
                transientPaths.Remove(sourcePathId);
            if (!string.IsNullOrWhiteSpace(savedPathId))
                transientPaths.Remove(savedPathId);
            ClearBlockedSaveFeedback();
            MarkDirty();
        }

        // Preserves in-memory edits of the path being left behind, so switching paths does not discard them.
        // Only paths with pending edits are captured: merely browsing must not turn every visited path into a
        // transient (and therefore 'unsaved') one.
        private void CaptureTransientCurrentPath()
        {
            PathEditor pathEditor = pathEditorAccessor();
            if (pathEditor?.HasUnsavedChanges != true)
                return;

            PathModel currentModel = NormalizeTransientPathModel(pathEditor.TryCaptureCurrentPathModel());
            if (currentModel == null || string.IsNullOrWhiteSpace(currentModel.Id))
                return;

            transientPaths[currentModel.Id] = currentModel;
        }

        private PathModel NormalizeTransientPathModel(PathModel pathModel)
        {
            if (pathModel == null)
                return null;

            ITrainPathToolingContext toolingContext = toolingContextAccessor();
            PathValidationState validationState = PathEditor.ResolveValidationState(pathModel, toolingContext?.TrackWorld);
            return pathModel.ValidationState == validationState ? pathModel : pathModel with { ValidationState = validationState };
        }

        /// <summary>
        /// Marks the current train-path snapshot stale after editor mutations that may not change path id or
        /// node count, such as node validity/type or metadata changes.
        /// </summary>
        internal void MarkDirty()
        {
            snapshotVersion++;
        }
    }
}
