using System.Collections.Immutable;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// A single field shown in the main-window status bar. <see cref="Key"/> is a stable identifier used for
    /// lookup/extensibility, <see cref="Label"/> is the optional caption rendered before the value (null when
    /// the value is self-describing), and <see cref="Value"/> is the display text. Uses BCL-only types so the
    /// WPF shell can consume snapshots without referencing MonoGame.
    /// </summary>
    internal readonly record struct StatusBarField
    {
        /// <summary>Stable identifier used for lookup/extensibility.</summary>
        public string Key { get; init; }

        /// <summary>Optional caption rendered before the value, or null when the value is self-describing.</summary>
        public string Label { get; init; }

        /// <summary>The display text of the field.</summary>
        public string Value { get; init; }
    }

    /// <summary>
    /// Immutable snapshot of the status bar's ordered fields, captured on the game thread and handed to the
    /// WPF view model. Uses the same pull/snapshot model as <see cref="ToolWindowSnapshot"/>: the game thread
    /// rebuilds it each frame and the WPF side reads the latest snapshot lock-free on a dispatcher cadence.
    /// New fields are added simply by appending to this ordered list in the status-bar bridge, so consumers
    /// (the bound <c>ItemsControl</c>) render them generically without further changes.
    /// </summary>
    internal sealed record StatusBarSnapshot
    {
        /// <summary>The ordered status-bar fields to display.</summary>
        public ImmutableArray<StatusBarField> Fields { get; init; }

        /// <summary>An empty snapshot used before any content is available.</summary>
        public static StatusBarSnapshot Empty { get; } = new StatusBarSnapshot { Fields = ImmutableArray<StatusBarField>.Empty };
    }
}
