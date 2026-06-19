using System;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PopupWindows;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// One available train path in the hosted train-path tool window's path list.
    /// </summary>
    internal readonly record struct TrainPathListRow(string Id, string Name);

    /// <summary>
    /// One node row of the currently edited train path (index, node type, validity).
    /// </summary>
    internal readonly record struct TrainPathNodeRow(int Index, string NodeType, bool Valid);

    /// <summary>
    /// Immutable snapshot of the hosted train-path tool window state, captured on the game thread and read
    /// lock-free by the WPF view model. Combines the available paths, the selected path id, the current
    /// path's node rows, and its metadata name/value rows.
    /// </summary>
    internal sealed record TrainPathSnapshot(
        ImmutableArray<TrainPathListRow> Paths,
        string SelectedPathId,
        ImmutableArray<TrainPathNodeRow> Nodes,
        ImmutableArray<ToolWindowRow> Metadata)
    {
        public static TrainPathSnapshot Empty { get; } = new(
            ImmutableArray<TrainPathListRow>.Empty,
            null,
            ImmutableArray<TrainPathNodeRow>.Empty,
            ImmutableArray<ToolWindowRow>.Empty);
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
        private volatile TrainPathSnapshot snapshot = TrainPathSnapshot.Empty;
        private volatile bool active;

        private ImmutableArray<PathModelHeader> cachedPaths = ImmutableArray<PathModelHeader>.Empty;
        private bool pathsCached;
        private string lastPathId;
        private int lastNodeCount = -1;
        private int snapshotVersion;
        private int lastSnapshotVersion = -1;

        internal TrainPathToolWindow(Func<PathEditor> pathEditorAccessor, Func<ITrainPathToolingContext> toolingContextAccessor, Action<Action> gameThreadInvoker)
        {
            this.pathEditorAccessor = pathEditorAccessor ?? throw new ArgumentNullException(nameof(pathEditorAccessor));
            this.toolingContextAccessor = toolingContextAccessor ?? throw new ArgumentNullException(nameof(toolingContextAccessor));
            this.gameThreadInvoker = gameThreadInvoker ?? throw new ArgumentNullException(nameof(gameThreadInvoker));
        }

        public ToolboxWindowType WindowType => ToolboxWindowType.TrainPathWindow;

        public string Title => "Train Path Details";

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
                if (snapshot != TrainPathSnapshot.Empty)
                {
                    ResetCaches();
                    snapshot = TrainPathSnapshot.Empty;
                }
                return;
            }

            ImmutableArray<TrainPathListRow> paths = BuildPaths();
            TrainPathBase currentPath = pathEditor.TrainPath;
            string selectedPathId = currentPath?.PathModel?.Id;
            int nodeCount = currentPath?.PathPoints.Count ?? 0;

            int currentSnapshotVersion = snapshotVersion;

            // Only rebuild the heavier node/metadata content when the selected path, node count, path list, or
            // editor version changed.
            if (snapshot != TrainPathSnapshot.Empty
                && string.Equals(selectedPathId, lastPathId, StringComparison.Ordinal)
                && nodeCount == lastNodeCount
                && currentSnapshotVersion == lastSnapshotVersion
                && paths.SequenceEqual(snapshot.Paths))
            {
                return;
            }

            lastPathId = selectedPathId;
            lastNodeCount = nodeCount;
            lastSnapshotVersion = currentSnapshotVersion;

            snapshot = new TrainPathSnapshot(paths, selectedPathId, BuildNodes(currentPath), BuildMetadata(currentPath));
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
                PathEditor pathEditor = pathEditorAccessor();
                if (pathEditor == null)
                    return;

                if (string.IsNullOrEmpty(pathId))
                {
                    _ = pathEditor.InitializePath(null);
                    return;
                }

                PathModelHeader path = cachedPaths.FirstOrDefault(p => string.Equals(p.Id, pathId, StringComparison.OrdinalIgnoreCase));
                if (path != null)
                    _ = pathEditor.InitializePath(path);
            });
        }

        /// <summary>
        /// Highlights the path node with the given index on the map (or clears the highlight when negative).
        /// Safe to call from the WPF UI thread.
        /// </summary>
        internal void HighlightNode(int index)
        {
            gameThreadInvoker(() =>
            {
                PathEditor pathEditor = pathEditorAccessor();
                if (pathEditor?.TrainPath == null)
                    return;

                pathEditor.HighlightPathItem(index);
            });
        }

        private ImmutableArray<TrainPathListRow> BuildPaths()
        {
            ImmutableArray<TrainPathListRow>.Builder builder = ImmutableArray.CreateBuilder<TrainPathListRow>();
            foreach (PathModelHeader path in cachedPaths.OrderBy(p => p.Name))
                builder.Add(new TrainPathListRow(path.Id, path.Name));
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
                builder.Add(new TrainPathNodeRow(i, item.NodeType.ToString(), item.ValidationResult == PathNodeInvalidReasons.None));
            }
            return builder.ToImmutable();
        }

        private ImmutableArray<ToolWindowRow> BuildMetadata(TrainPathBase currentPath)
        {
            if (currentPath?.PathModel == null)
                return ImmutableArray<ToolWindowRow>.Empty;

            bool metricUnits = toolingContextAccessor()?.UseMetricUnits ?? true;
            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();
            builder.Add(new ToolWindowRow("Path ID", currentPath.PathModel.Id, null, false));
            builder.Add(new ToolWindowRow("Path Name", currentPath.PathModel.Name, null, false));
            builder.Add(new ToolWindowRow("Start", currentPath.PathModel.Start, null, false));
            builder.Add(new ToolWindowRow("End", currentPath.PathModel.End, null, false));
            builder.Add(new ToolWindowRow("Player Path", currentPath.PathModel.PlayerPath ? "Yes" : "No", null, false));
            builder.Add(new ToolWindowRow("Path Length", FormatStrings.FormatDistanceDisplay(currentPath.Length, metricUnits, 1000), null, false));
            return builder.ToImmutable();
        }

        /// <summary>
        /// Invalidates the cached path list so the next refresh re-queries the route. Called on the game
        /// thread when the route/path editor availability changes.
        /// </summary>
        internal void InvalidatePaths()
        {
            pathsCached = false;
            cachedPaths = ImmutableArray<PathModelHeader>.Empty;
            MarkDirty();
        }

        /// <summary>
        /// Updates the cached route path list without blocking snapshot refresh. Called on the game thread
        /// after route path loading completes.
        /// </summary>
        internal void UpdatePaths(ImmutableArray<PathModelHeader> paths)
        {
            cachedPaths = paths.IsDefault ? ImmutableArray<PathModelHeader>.Empty : paths;
            pathsCached = true;
            MarkDirty();
        }

        /// <summary>
        /// Marks the current train-path snapshot stale after editor mutations that may not change path id or
        /// node count, such as node validity/type or metadata changes.
        /// </summary>
        internal void MarkDirty()
        {
            snapshotVersion++;
        }

        private void ResetCaches()
        {
            pathsCached = false;
            cachedPaths = ImmutableArray<PathModelHeader>.Empty;
            lastPathId = null;
            lastNodeCount = -1;
            lastSnapshotVersion = -1;
        }
    }
}
