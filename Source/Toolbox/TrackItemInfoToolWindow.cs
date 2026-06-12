using System;

using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Toolbox.PopupWindows;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Hosted-mode bridge exposing the read-only track item information provider as a dockable WPF tool
    /// window. Uses the same pull/snapshot model as <see cref="DebugToolWindow"/>: the game thread rebuilds
    /// an immutable <see cref="ToolWindowSnapshot"/> each frame (via <see cref="RefreshSnapshot"/>) and the
    /// WPF view model reads the latest snapshot lock-free through <see cref="CaptureSnapshot"/>.
    /// <para>
    /// The "find track item by index" search navigates the map viewport, which is game-thread state, so it
    /// is marshaled back onto the game thread through the supplied invoker (mirroring
    /// <see cref="HostedToolboxMenu"/>).
    /// </para>
    /// </summary>
    internal sealed class TrackItemInfoToolWindow : IToolboxToolWindow
    {
        private readonly Action<Action> gameThreadInvoker;
        private ITrackItemInfoContext context;
        private volatile ToolWindowSnapshot snapshot = ToolWindowSnapshot.Empty;
        private volatile bool active;

        internal TrackItemInfoToolWindow(Action<Action> gameThreadInvoker)
        {
            this.gameThreadInvoker = gameThreadInvoker ?? throw new ArgumentNullException(nameof(gameThreadInvoker));
        }

        public ToolboxWindowType WindowType => ToolboxWindowType.TrackItemInfoWindow;

        public string Title => "Track Item Information";

        public bool Active
        {
            get => active;
            set => active = value;
        }

        public ToolWindowSnapshot CaptureSnapshot() => snapshot;

        /// <summary>
        /// Updates the active track-item information context. Called on the game thread when the content area
        /// changes (route loaded/unloaded).
        /// </summary>
        internal void UpdateContext(ITrackItemInfoContext context)
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

            snapshot = ToolWindowSnapshotFactory.FromProvider(context?.TrackItemInfo);
        }

        /// <summary>
        /// Navigates the map to the track item with the given index. Safe to call from the WPF UI thread:
        /// the parse happens inline and the viewport navigation is marshaled onto the game thread.
        /// </summary>
        internal void SearchByIndex(string searchText)
        {
            if (!int.TryParse(searchText, out int index))
                return;

            gameThreadInvoker(() => NavigateToItem(index));
        }

        private void NavigateToItem(int index)
        {
            ITrackItemInfoContext currentContext = context;
            var trackItem = currentContext?.TrackWorld?.TrackItemByIndex(index);
            if (trackItem != null)
                currentContext?.Viewport?.SetTrackingPosition(trackItem.Location);
        }
    }
}
