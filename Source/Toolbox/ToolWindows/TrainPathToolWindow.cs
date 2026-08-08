using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PopupWindows;

using DrawingColor = System.Drawing.Color;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// One available train path in the hosted train-path tool window's path list.
    /// </summary>
    internal readonly record struct TrainPathListRow
    {
        public TrainPathListRow(string id, string name, PathValidationState validationState)
            : this(id, name, validationState, false)
        {
        }

        public TrainPathListRow(string id, string name, PathValidationState validationState, bool hasUnsavedChanges)
        {
            Id = id;
            Name = name;
            ValidationState = validationState;
            HasUnsavedChanges = hasUnsavedChanges;
        }

        /// <summary>Unique id of the path.</summary>
        public string Id { get; }

        /// <summary>Display name of the path.</summary>
        public string Name { get; }

        /// <summary>Persisted validation state of the path (valid, invalid, or not yet validated).</summary>
        public PathValidationState ValidationState { get; }

        /// <summary>Whether the path holds edits that have not been persisted yet.</summary>
        public bool HasUnsavedChanges { get; }
    }

    /// <summary>
    /// One node row of the currently edited train path (index, node type, validity).
    /// </summary>
    internal readonly record struct TrainPathNodeRow
    {
        public TrainPathNodeRow(int index, PathNodeType nodeType, bool valid)
            : this(index, nodeType, valid, 0, -1, -1, null, null, null, null, null)
        {
        }

        public TrainPathNodeRow(int index, PathNodeType nodeType, bool valid, int trackNodeIndex, int nextMainNode, int nextSidingNode, int? waitTime, string validation)
            : this(index, nodeType, valid, trackNodeIndex, nextMainNode, nextSidingNode, waitTime, validation, null, null, null)
        {
        }

        public TrainPathNodeRow(int index, PathNodeType nodeType, bool valid, int trackNodeIndex, int nextMainNode, int nextSidingNode, int? waitTime,
            string validation, int? nearestTrackNodeIndex, int? nearestTrackSectionIndex, double? nearestTrackDistanceMeters)
        {
            Index = index;
            NodeType = nodeType;
            Valid = valid;
            TrackNodeIndex = trackNodeIndex;
            NextMainNode = nextMainNode;
            NextSidingNode = nextSidingNode;
            WaitTime = waitTime;
            Validation = validation;
            NearestTrackNodeIndex = nearestTrackNodeIndex;
            NearestTrackSectionIndex = nearestTrackSectionIndex;
            NearestTrackDistanceMeters = nearestTrackDistanceMeters;
        }

        public int Index { get; }

        public PathNodeType NodeType { get; }

        public bool Valid { get; }

        public int TrackNodeIndex { get; }

        public int NextMainNode { get; }

        public int NextSidingNode { get; }

        public int? WaitTime { get; }

        public string Validation { get; }

        public int? NearestTrackNodeIndex { get; }

        public int? NearestTrackSectionIndex { get; }

        public double? NearestTrackDistanceMeters { get; }
    }

    /// <summary>
    /// One equal-cost route candidate of an ambiguous span of the currently edited train path.
    /// </summary>
    internal readonly record struct TrainPathRouteCandidateRow
    {
        public TrainPathRouteCandidateRow(int fromNodeIndex, int toNodeIndex, int candidateIndex, string description)
        {
            FromNodeIndex = fromNodeIndex;
            ToNodeIndex = toNodeIndex;
            CandidateIndex = candidateIndex;
            Description = description;
        }

        /// <summary>Authored node index the ambiguous span starts at.</summary>
        public int FromNodeIndex { get; }

        /// <summary>Authored node index the ambiguous span ends at.</summary>
        public int ToNodeIndex { get; }

        /// <summary>Index of the candidate within the span, stable across resolutions.</summary>
        public int CandidateIndex { get; }

        /// <summary>Human readable summary of the route the candidate takes.</summary>
        public string Description { get; }
    }

    /// <summary>
    /// One resolver diagnostic of the currently edited train path, flattened into immutable UI-safe data.
    /// </summary>
    internal readonly record struct TrainPathDiagnosticRow
    {
        public TrainPathDiagnosticRow(PathRouteDiagnosticSeverity severity, PathRouteDiagnosticCode code, string message,
            int nodeIndex, int fromNodeIndex, int toNodeIndex, string suggestedAction, bool canRepair)
        {
            Severity = severity;
            Code = code;
            Message = message;
            NodeIndex = nodeIndex;
            FromNodeIndex = fromNodeIndex;
            ToNodeIndex = toNodeIndex;
            SuggestedAction = suggestedAction;
            CanRepair = canRepair;
        }

        /// <summary>Diagnostic severity.</summary>
        public PathRouteDiagnosticSeverity Severity { get; }

        /// <summary>Stable diagnostic code.</summary>
        public PathRouteDiagnosticCode Code { get; }

        /// <summary>Human-readable diagnostic message.</summary>
        public string Message { get; }

        /// <summary>Authored node index associated with the diagnostic, or -1 when not node-specific.</summary>
        public int NodeIndex { get; }

        /// <summary>Source authored node index for span diagnostics, or -1 when not span-specific.</summary>
        public int FromNodeIndex { get; }

        /// <summary>Target authored node index for span diagnostics, or -1 when not span-specific.</summary>
        public int ToNodeIndex { get; }

        /// <summary>Suggested repair or review action.</summary>
        public string SuggestedAction { get; }

        /// <summary>Whether the existing selected-node repair operation can repair this diagnostic target.</summary>
        public bool CanRepair { get; }

        /// <summary>Whether the diagnostic identifies one authored node.</summary>
        public bool HasNodeTarget => NodeIndex >= 0;

        /// <summary>Whether the diagnostic identifies an authored path span.</summary>
        public bool HasSpanTarget => FromNodeIndex >= 0 && ToNodeIndex >= 0;
    }

    /// <summary>
    /// Immutable snapshot of the hosted train-path tool window state, captured on the game thread and read
    /// lock-free by the WPF view model. Combines the available paths, the selected path id, the current
    /// path's node rows, and its metadata name/value rows.
    /// </summary>
    internal sealed record TrainPathSnapshot
    {
        /// <summary>Available paths for the loaded route.</summary>
        public ImmutableArray<TrainPathListRow> Paths { get; init; }

        /// <summary>Id of the currently selected path, or null when none is selected.</summary>
        public string SelectedPathId { get; init; }

        /// <summary>Node rows of the currently edited path.</summary>
        public ImmutableArray<TrainPathNodeRow> Nodes { get; init; }

        /// <summary>Index of the path node selected on the map, or -1 when none is selected.</summary>
        public int SelectedNodeIndex { get; init; } = -1;

        /// <summary>Name/value metadata rows for the currently edited path.</summary>
        public ImmutableArray<ToolWindowRow> Metadata { get; init; }

        /// <summary>Equal-cost route candidates of the currently edited path's ambiguous spans.</summary>
        public ImmutableArray<TrainPathRouteCandidateRow> RouteCandidates { get; init; }

        /// <summary>Resolver diagnostics of the currently edited path.</summary>
        public ImmutableArray<TrainPathDiagnosticRow> Diagnostics { get; init; }

        /// <summary>Whether an undo step is available.</summary>
        public bool CanUndo { get; init; }

        /// <summary>Whether a redo step is available.</summary>
        public bool CanRedo { get; init; }

        /// <summary>Whether the current path can be snapped to track.</summary>
        public bool CanSnapToTrack { get; init; }

        /// <summary>Whether a node move operation is currently active.</summary>
        public bool CanCancelMoveNode { get; init; }

        /// <summary>Whether the active node move has a valid preview that can be committed.</summary>
        public bool CanCommitMoveNode { get; init; }

        /// <summary>An empty snapshot used before any path content is available.</summary>
        public static TrainPathSnapshot Empty { get; } = new TrainPathSnapshot
        {
            Paths = ImmutableArray<TrainPathListRow>.Empty,
            SelectedPathId = null,
            Nodes = ImmutableArray<TrainPathNodeRow>.Empty,
            SelectedNodeIndex = -1,
            Metadata = ImmutableArray<ToolWindowRow>.Empty,
            RouteCandidates = ImmutableArray<TrainPathRouteCandidateRow>.Empty,
            Diagnostics = ImmutableArray<TrainPathDiagnosticRow>.Empty,
            CanUndo = false,
            CanRedo = false,
            CanSnapToTrack = false,
            CanCancelMoveNode = false,
            CanCommitMoveNode = false,
        };
    }

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
                    && !snapshot.CanCommitMoveNode;
                if (snapshotIsPathsOnly && pathsSnapshotVersion == lastSnapshotVersion)
                    return;

                lastPathId = null;
                lastNodeCount = 0;
                lastSnapshotVersion = pathsSnapshotVersion;
                snapshot = TrainPathSnapshot.Empty with { Paths = BuildPaths(null) };
                return;
            }

            TrainPathBase currentPath = pathEditor.TrainPath;
            PathModel currentPathModel = NormalizeTransientPathModel(pathEditor.TryCaptureCurrentPathModel() ?? currentPath?.PathModel);
            ImmutableArray<TrainPathListRow> paths = BuildPaths(currentPathModel);
            string selectedPathId = currentPath?.PathModel?.Id;
            int nodeCount = currentPath?.PathPoints.Count ?? 0;
            int selectedNodeIndex = pathEditor.SelectedPathNodeIndex;
            bool canUndo = pathEditor.CanUndo;
            bool canRedo = pathEditor.CanRedo;
            bool canSnapToTrack = pathEditor.CanSnapToTrack;
            bool canCancelMoveNode = pathEditor.IsMovingNode;
            bool canCommitMoveNode = pathEditor.CanCommitMoveNode;

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
                && paths.SequenceEqual(snapshot.Paths))
            {
                return;
            }

            lastPathId = selectedPathId;
            lastNodeCount = nodeCount;
            lastSnapshotVersion = currentSnapshotVersion;

            snapshot = new TrainPathSnapshot
            {
                Paths = paths,
                SelectedPathId = selectedPathId,
                Nodes = BuildNodes(currentPath),
                SelectedNodeIndex = selectedNodeIndex,
                Metadata = BuildMetadata(pathEditor, currentPath),
                RouteCandidates = BuildRouteCandidates(pathEditor),
                Diagnostics = BuildResolverDiagnostics(pathEditor.ResolveCurrent(currentPathModel), pathEditor),
                CanUndo = canUndo,
                CanRedo = canRedo,
                CanSnapToTrack = canSnapToTrack,
                CanCancelMoveNode = canCancelMoveNode,
                CanCommitMoveNode = canCommitMoveNode,
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
            _ = SelectPathAsync(pathId);
        }

        private async Task SelectPathAsync(string pathId)
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
                    _ = RefreshCachedPathsFromContextAsync();
                    path = cachedPaths.FirstOrDefault(p => string.Equals(p.Id, pathId, StringComparison.OrdinalIgnoreCase));
                }
                if (path != null)
                {
                    loadPathAction(path);
                    MarkDirty();
                }
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
                if (pathEditor.TrainPath == null)
                    return;

                pathEditor.HighlightPathItem(index);
            });
        }

        internal void Undo() => InvokeEditorMutation(pathEditor => pathEditor.Undo());

        internal void Redo() => InvokeEditorMutation(pathEditor => pathEditor.Redo());

        internal void SnapToTrack() => ExecuteEditorCommand(pathEditor => pathEditor.ReResolvePathCommand());

        internal void ExtendPath() => ExecuteEditorCommand(pathEditor => pathEditor.ExtendPathCommand(), activateMapInputAction);

        internal void BeginMoveNode(int nodeIndex)
        {
            ExecuteEditorCommand(pathEditor => pathEditor.BeginMoveNodeCommand(nodeIndex), activateMapInputAction);
        }

        internal void CommitMoveNode() => ExecuteEditorCommand(pathEditor => pathEditor.CommitMoveNodeCommand());

        internal void CancelMoveNode() => ExecuteEditorCommand(pathEditor => pathEditor.CancelMoveNodeCommand());

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

        internal void RemoveViaPoint(int nodeIndex) => ExecuteEditorCommand(pathEditor => pathEditor.RemoveViaPointCommand(nodeIndex));

        internal void PreviewRouteCandidate(int fromNodeIndex, int candidateIndex) => ExecuteEditorCommand(pathEditor => pathEditor.PreviewRouteCandidateCommand(fromNodeIndex, candidateIndex));

        internal void ClearRouteCandidatePreview() => InvokeEditorAction(pathEditor => pathEditor.ClearRouteCandidatePreview());

        internal void AcceptRouteCandidate(int fromNodeIndex, int candidateIndex) => ExecuteEditorCommand(pathEditor => pathEditor.AcceptRouteCandidateCommand(fromNodeIndex, candidateIndex));

        internal bool CanCreatePath => toolingContextAccessor() != null;

        internal bool CanSavePath => pathEditorAccessor()?.TrainPath != null;

        internal void CreatePath()
        {
            gameThreadInvoker(() =>
            {
                CaptureTransientCurrentPath();
                createPathAction();
                MarkDirty();
            });
        }

        internal void SavePath()
        {
            gameThreadInvoker(savePathAction);
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
                if (result.Success)
                {
                    onSuccess?.Invoke();
                    MarkDirty();
                    return;
                }

                Trace.TraceWarning(result.Message);
            });
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
        internal async Task ValidateAllPaths()
        {
            ITrainPathToolingContext toolingContext = toolingContextAccessor();
            if (toolingContext == null)
                return;

            ImmutableArray<PathModelHeader> paths = await toolingContext.ValidateAllPaths().ConfigureAwait(false);
            gameThreadInvoker(() => UpdatePaths(paths));
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

        private ImmutableArray<ToolWindowRow> BuildMetadata(PathEditor pathEditor, TrainPathBase currentPath)
        {
            if (currentPath?.PathModel == null)
                return ImmutableArray<ToolWindowRow>.Empty;

            ITrainPathToolingContext toolingContext = toolingContextAccessor();
            bool metricUnits = toolingContext?.UseMetricUnits ?? true;
            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();
            builder.Add(new ToolWindowRow { Name = "Path ID", Value = currentPath.PathModel.Id });
            builder.Add(new ToolWindowRow { Name = "Path Name", Value = currentPath.PathModel.Name });
            builder.Add(new ToolWindowRow { Name = "Start", Value = currentPath.PathModel.Start });
            builder.Add(new ToolWindowRow { Name = "End", Value = currentPath.PathModel.End });
            builder.Add(new ToolWindowRow { Name = "Player Path", Value = FormatStrings.FormatYesNo(currentPath.PathModel.PlayerPath) });
            builder.Add(new ToolWindowRow { Name = "Path Length", Value = FormatStrings.FormatDistanceDisplay(currentPath.Length, metricUnits, 1000) });
            builder.AddRange(BuildEditorStateMetadata(currentPath));
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

        private async Task RefreshCachedPathsFromContextAsync()
        {
            ITrainPathToolingContext toolingContext = toolingContextAccessor();
            if (toolingContext == null)
                return;

            try
            {
                ImmutableArray<PathModelHeader> paths = await toolingContext.GetPaths().ConfigureAwait(false);
                cachedPaths = paths.IsDefault ? ImmutableArray<PathModelHeader>.Empty : paths;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.IO.IOException)
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to refresh path cache for selection: {ex.Message}");
            }
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
