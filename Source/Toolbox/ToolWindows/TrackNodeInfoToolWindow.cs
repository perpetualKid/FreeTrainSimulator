using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PopupWindows;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Hosted-mode bridge exposing the read-only track node information provider as a dockable WPF tool
    /// window. Uses the same pull/snapshot model as <see cref="DebugToolWindow"/>: the game thread rebuilds
    /// an immutable <see cref="ToolWindowSnapshot"/> each frame (via <see cref="RefreshSnapshot"/>) and the
    /// WPF view model reads the latest snapshot lock-free through <see cref="CaptureSnapshot"/>.
    /// <para>
    /// The "find track node by index" search navigates and highlights on the map, which is game-thread
    /// state, so it is marshaled back onto the game thread through the supplied invoker (mirroring
    /// <see cref="HostedToolboxMenu"/>). The search distinguishes rail nodes from road nodes.
    /// </para>
    /// </summary>
    internal sealed class TrackNodeInfoToolWindow : IToolboxToolWindow
    {
        private readonly Action<Action> gameThreadInvoker;
        private ITrackNodeInfoContext context;
        private volatile ToolWindowSnapshot snapshot = ToolWindowSnapshot.Empty;
        private volatile bool active;

        internal TrackNodeInfoToolWindow(Action<Action> gameThreadInvoker)
        {
            this.gameThreadInvoker = gameThreadInvoker ?? throw new ArgumentNullException(nameof(gameThreadInvoker));
        }

        public ToolboxWindowType WindowType => ToolboxWindowType.TrackNodeInfoWindow;

        public string Title => "Track Node Information";

        public bool Active
        {
            get => active;
            set => active = value;
        }

        public ToolWindowSnapshot CaptureSnapshot() => snapshot;

        /// <summary>
        /// Updates the active track-node information context. Called on the game thread when the content area
        /// changes (route loaded/unloaded).
        /// </summary>
        internal void UpdateContext(ITrackNodeInfoContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Rebuilds the immutable snapshot from the current provider. Must be called on the game thread; a
        /// no-op while the pane is hidden.
        /// </summary>
        internal void RefreshSnapshot()
        {
            if (!Active)
                return;

            snapshot = ToolWindowSnapshotFactory.FromProvider(context?.Content?.TrackNodeInfo);
        }

        /// <summary>
        /// Navigates the map to the track node with the given index, fitting and highlighting it. When
        /// <paramref name="searchRoads"/> is true the search targets road nodes; otherwise rail nodes. Safe
        /// to call from the WPF UI thread: the parse happens inline and the navigation is marshaled onto the
        /// game thread.
        /// </summary>
        internal void SearchByIndex(string searchText, bool searchRoads)
        {
            if (!int.TryParse(searchText, out int nodeIndex))
                return;

            gameThreadInvoker(() => NavigateToNode(nodeIndex, searchRoads));
        }

        private void NavigateToNode(int nodeIndex, bool searchRoads)
        {
            ITrackNodeInfoContext currentContext = context;
            TrackWorld trackWorld = currentContext?.TrackWorld;
            if (trackWorld == null || nodeIndex < 0)
                return;

            if (searchRoads)
            {
                TrackSegmentSection roadSegmentSection = nodeIndex < trackWorld.RoadSegmentSections.Length ? trackWorld.RoadSegmentSections[nodeIndex] : null;
                if (roadSegmentSection != null)
                {
                    currentContext.Viewport?.UpdateScaleToFit(roadSegmentSection.TopLeftBound, roadSegmentSection.BottomRightBound);
                    currentContext.Viewport?.SetTrackingPosition(roadSegmentSection.MidPoint);
                    currentContext.Content?.HighlightItem(MapContentType.Roads, roadSegmentSection.SectionSegments[0]);
                }
            }
            else
            {
                TrackSegmentSection segmentSection = nodeIndex < trackWorld.SegmentSections.Length ? trackWorld.SegmentSections[nodeIndex] : null;
                if (segmentSection != null)
                {
                    currentContext.Viewport?.UpdateScaleToFit(segmentSection.TopLeftBound, segmentSection.BottomRightBound);
                    currentContext.Viewport?.SetTrackingPosition(segmentSection.MidPoint);
                    currentContext.Content?.HighlightItem(MapContentType.Tracks, segmentSection.SectionSegments[0]);
                }
            }
        }
    }
}
