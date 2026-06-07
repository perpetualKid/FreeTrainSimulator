using System.Collections.Immutable;
using System.Drawing;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Toolbox.PopupWindows;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Hosted-mode bridge that surfaces the read-only debug/graphics information providers as a dockable
    /// WPF tool window. Mirrors <see cref="HostedToolboxMenu"/> but uses the pull/snapshot model: the game
    /// thread rebuilds an immutable <see cref="ToolWindowSnapshot"/> each frame (via <see cref="RefreshSnapshot"/>)
    /// and the WPF view model reads the latest snapshot lock-free through <see cref="CaptureSnapshot"/>.
    /// <para>
    /// The MonoGame-specific <see cref="FormatOption"/> colours/styles are converted to BCL types here so the
    /// WPF shell never references MonoGame.
    /// </para>
    /// </summary>
    internal sealed class DebugToolWindow : IToolboxToolWindow
    {
        private readonly ImmutableArray<INameValueInformationProvider> providers;
        private volatile ToolWindowSnapshot snapshot = ToolWindowSnapshot.Empty;
        private volatile bool active;

        internal DebugToolWindow(params INameValueInformationProvider[] providers)
        {
            this.providers = providers is null
                ? ImmutableArray<INameValueInformationProvider>.Empty
                : ImmutableArray.Create(providers);
        }

        /// <summary>
        /// When false, <see cref="RefreshSnapshot"/> is a no-op so a closed tool window costs nothing on the
        /// game loop. Set by the WPF shell when the dock pane is shown or hidden.
        /// </summary>
        public bool Active
        {
            get => active;
            set => active = value;
        }

        public ToolboxWindowType WindowType => ToolboxWindowType.DebugScreen;

        public string Title => "Debug Information";

        public ToolWindowSnapshot CaptureSnapshot() => snapshot;

        /// <summary>
        /// Rebuilds the immutable snapshot from the underlying providers. Must be called on the game thread
        /// (the same thread that updates the providers each frame) so reads never race with provider
        /// mutation; the result is published to <see cref="CaptureSnapshot"/> via a volatile write.
        /// </summary>
        internal void RefreshSnapshot()
        {
            if (!Active)
                return;

            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();
            foreach (INameValueInformationProvider provider in providers)
            {
                InformationDictionary detail = provider?.DetailInfo;
                if (detail is null)
                    continue;

                foreach (string key in detail.Keys)
                {
                    FormatOption format = null;
                    _ = provider.FormattingOptions?.TryGetValue(key, out format);
                    Color? color = format?.TextColor is { } textColor
                        ? Color.FromArgb(textColor.A, textColor.R, textColor.G, textColor.B)
                        : null;
                    bool bold = format?.FontStyle.HasFlag(FontStyle.Bold) ?? false;
                    builder.Add(new ToolWindowRow(key, detail[key], color, bold));
                }
            }

            snapshot = new ToolWindowSnapshot(builder.ToImmutable());
        }
    }
}
