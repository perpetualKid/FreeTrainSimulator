using System;
using System.Collections.Immutable;

using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Toolbox.PopupWindows;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// One selectable route-navigation entry in a name list (station, platform, or siding). Carries only the
    /// index into the matching <see cref="ToolboxContent"/> list plus display/grouping text, so the WPF view
    /// model never references the internal map widget types.
    /// </summary>
    internal readonly record struct RouteNavigationRow
    {
        /// <summary>Index into the matching <see cref="ToolboxContent"/> navigation list, used to request navigation.</summary>
        public int Index { get; init; }

        /// <summary>Display name of the entity (station, platform, or siding name).</summary>
        public string Name { get; init; }

        /// <summary>Grouping label: owning station for platforms, nearest station for sidings, or null for stations.</summary>
        public string GroupName { get; init; }

        public RouteNavigationRow(int index, string name, string groupName)
        {
            Index = index;
            Name = name;
            GroupName = groupName;
        }
    }

    /// <summary>
    /// Immutable snapshot of the hosted route-navigation tool window state, captured on the game thread and
    /// read lock-free by the WPF view model. Holds the station, platform, and siding name lists plus the
    /// detail rows for the active selection/hover. The by-id track item/node searches are direct actions and
    /// only contribute to the detail rows.
    /// </summary>
    internal sealed record RouteNavigationSnapshot
    {
        /// <summary>Selectable station rows for the Stations tab.</summary>
        public ImmutableArray<RouteNavigationRow> Stations { get; init; }

        /// <summary>Selectable platform rows (grouped by station) for the Platforms tab.</summary>
        public ImmutableArray<RouteNavigationRow> Platforms { get; init; }

        /// <summary>Selectable siding rows (grouped by nearest station) for the Sidings tab.</summary>
        public ImmutableArray<RouteNavigationRow> Sidings { get; init; }

        /// <summary>Name/value detail rows for the selected (pinned) entity, or the item currently hovered on the map.</summary>
        public ImmutableArray<ToolWindowRow> DetailRows { get; init; }

        public RouteNavigationSnapshot(ImmutableArray<RouteNavigationRow> stations,
            ImmutableArray<RouteNavigationRow> platforms, ImmutableArray<RouteNavigationRow> sidings,
            ImmutableArray<ToolWindowRow> detailRows)
        {
            Stations = stations;
            Platforms = platforms;
            Sidings = sidings;
            DetailRows = detailRows;
        }

        public static RouteNavigationSnapshot Empty { get; } = new RouteNavigationSnapshot(
            ImmutableArray<RouteNavigationRow>.Empty, ImmutableArray<RouteNavigationRow>.Empty,
            ImmutableArray<RouteNavigationRow>.Empty, ImmutableArray<ToolWindowRow>.Empty);
    }

    /// <summary>
    /// Hosted-mode bridge exposing combined route navigation as a single dockable WPF tool window. Ports the
    /// legacy TrackViewer "jump to station/platform/siding" menu and folds in the existing by-id track item and
    /// track node lookups into one place.
    /// <para>
    /// Like the other hosted bridges it uses a pull/snapshot model: <see cref="RefreshSnapshot"/> publishes the
    /// station/platform/siding name lists from <see cref="ToolboxContent"/> on the game thread and the WPF view
    /// model reads the latest snapshot lock-free through <see cref="CaptureRouteNavigationSnapshot"/>. All
    /// navigation actions mutate game-thread state (viewport, highlight), so they are marshaled back onto the
    /// game thread through the supplied invoker (mirroring <see cref="HostedToolboxMenu"/>).
    /// </para>
    /// </summary>
    internal sealed class RouteNavigationToolWindow : IToolboxToolWindow
    {
        private readonly Action<Action> gameThreadInvoker;
        private ITrackNodeInfoContext context;
        private volatile RouteNavigationSnapshot snapshot = RouteNavigationSnapshot.Empty;
        private volatile bool active;

        private ToolboxContent lastContent;
        private bool navigationDataCaptured;
        private ImmutableArray<RouteNavigationRow> stationRows = ImmutableArray<RouteNavigationRow>.Empty;
        private ImmutableArray<RouteNavigationRow> platformRows = ImmutableArray<RouteNavigationRow>.Empty;
        private ImmutableArray<RouteNavigationRow> sidingRows = ImmutableArray<RouteNavigationRow>.Empty;
        private ImmutableArray<ToolWindowRow> lastDetailRows = ImmutableArray<ToolWindowRow>.Empty;

        internal RouteNavigationToolWindow(Action<Action> gameThreadInvoker)
        {
            this.gameThreadInvoker = gameThreadInvoker ?? throw new ArgumentNullException(nameof(gameThreadInvoker));
        }

        public ToolboxWindowType WindowType => ToolboxWindowType.RouteNavigationWindow;

        public string Title => "Route Navigation";

        public bool Active
        {
            get => active;
            set => active = value;
        }

        public ToolWindowSnapshot CaptureSnapshot() => ToolWindowSnapshot.Empty;

        /// <summary>Captures the latest route-navigation snapshot. Safe to call from the WPF UI thread.</summary>
        internal RouteNavigationSnapshot CaptureRouteNavigationSnapshot() => snapshot;

        /// <summary>
        /// Updates the active track-node information context (which also exposes the route content, viewport,
        /// and track world). Called on the game thread when the content area changes (route loaded/unloaded).
        /// </summary>
        internal void UpdateContext(ITrackNodeInfoContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Rebuilds the immutable snapshot from the current route content. Must be called on the game thread; a
        /// no-op while the pane is hidden. The navigation lists are built once on a background task during route
        /// initialization, so the capture waits until they are populated before publishing them.
        /// </summary>
        internal void RefreshSnapshot()
        {
            if (!Active)
                return;

            ToolboxContent content = context?.Content;
            if (!ReferenceEquals(content, lastContent))
            {
                lastContent = content;
                navigationDataCaptured = false;
                stationRows = platformRows = sidingRows = ImmutableArray<RouteNavigationRow>.Empty;
                lastDetailRows = ImmutableArray<ToolWindowRow>.Empty;
                snapshot = RouteNavigationSnapshot.Empty;
            }

            if (content == null)
                return;

            // The lists are populated on a background task during Initialize; capture them once they appear.
            if (!navigationDataCaptured
                && !(content.Stations.IsDefaultOrEmpty && content.Platforms.IsDefaultOrEmpty && content.Sidings.IsDefaultOrEmpty))
            {
                navigationDataCaptured = true;
                stationRows = ToRows(content.Stations);
                platformRows = ToRows(content.Platforms);
                sidingRows = ToRows(content.Sidings);
            }

            // Detail rows update every frame: the live hover provider takes precedence when the cursor is over a
            // map item, otherwise the pinned selection persists. Only republish when the rows actually change.
            ImmutableArray<ToolWindowRow> detailRows = BuildDetailRows(content);
            if (!navigationDataCaptured && detailRows.IsDefaultOrEmpty)
                return;

            if (System.Linq.Enumerable.SequenceEqual<ToolWindowRow>(detailRows, lastDetailRows) && snapshot != RouteNavigationSnapshot.Empty)
                return;

            lastDetailRows = detailRows;
            snapshot = new RouteNavigationSnapshot(stationRows, platformRows, sidingRows, detailRows);
        }

        // Picks the detail source for the lower pane: the hovered track node/item provider when it currently has
        // rows, otherwise the pinned selection. Hover is transient (cleared when the cursor leaves an item), so
        // the pinned selection is the stable baseline.
        private static ImmutableArray<ToolWindowRow> BuildDetailRows(ToolboxContent content)
        {
            ToolWindowSnapshot hover = ToolWindowSnapshotFactory.FromProviders(new[] { content.TrackNodeInfo, content.TrackItemInfo });
            if (!hover.Rows.IsDefaultOrEmpty)
                return hover.Rows;

            return ToolWindowSnapshotFactory.FromProvider(content.PinnedNavigationInfo).Rows;
        }

        /// <summary>Centers and highlights the map on the station at the given list index. Safe to call from the WPF UI thread.</summary>
        internal void NavigateToStation(int index) => Navigate(RouteNavigationKind.Station, index);

        /// <summary>Centers and highlights the map on the platform at the given list index. Safe to call from the WPF UI thread.</summary>
        internal void NavigateToPlatform(int index) => Navigate(RouteNavigationKind.Platform, index);

        /// <summary>Centers and highlights the map on the siding at the given list index. Safe to call from the WPF UI thread.</summary>
        internal void NavigateToSiding(int index) => Navigate(RouteNavigationKind.Siding, index);

        private void Navigate(RouteNavigationKind kind, int index)
        {
            if (index < 0)
                return;

            gameThreadInvoker(() => context?.Content?.NavigateTo(kind, index));
        }

        /// <summary>
        /// Navigates the map to the track item with the given index and pins its details. Safe to call from the
        /// WPF UI thread: the parse happens inline and the navigation is marshaled onto the game thread.
        /// </summary>
        internal void SearchTrackItemByIndex(string searchText)
        {
            if (!int.TryParse(searchText, out int index))
                return;

            gameThreadInvoker(() => context?.Content?.NavigateToTrackItem(index));
        }

        /// <summary>
        /// Navigates the map to the track node with the given index, fitting, highlighting, and pinning its
        /// details. When <paramref name="searchRoads"/> is true the search targets road nodes; otherwise rail
        /// nodes. Safe to call from the WPF UI thread: the parse happens inline and the navigation is marshaled
        /// onto the game thread.
        /// </summary>
        internal void SearchTrackNodeByIndex(string searchText, bool searchRoads)
        {
            if (!int.TryParse(searchText, out int nodeIndex))
                return;

            gameThreadInvoker(() => context?.Content?.NavigateToTrackNode(nodeIndex, searchRoads));
        }

        private static ImmutableArray<RouteNavigationRow> ToRows(ImmutableArray<RouteNavigationItem> items)
        {
            if (items.IsDefaultOrEmpty)
                return ImmutableArray<RouteNavigationRow>.Empty;

            ImmutableArray<RouteNavigationRow>.Builder builder = ImmutableArray.CreateBuilder<RouteNavigationRow>(items.Length);
            foreach (RouteNavigationItem item in items)
                builder.Add(new RouteNavigationRow(item.Index, item.Name, item.GroupName));
            return builder.ToImmutable();
        }
    }
}
