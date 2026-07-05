using System.Collections.Immutable;
using System.Drawing;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// A single name/value row in a tool-window snapshot, with the formatting needed to render it.
    /// Uses BCL-only types so the WPF shell can consume snapshots without referencing MonoGame.
    /// </summary>
    internal readonly record struct ToolWindowRow
    {
        /// <summary>The row caption / name column.</summary>
        public string Name { get; init; }

        /// <summary>The row value column.</summary>
        public string Value { get; init; }

        /// <summary>Optional foreground color for the row, or null for the default.</summary>
        public Color? Color { get; init; }

        /// <summary>Whether the row is rendered in bold (used for headings/emphasis).</summary>
        public bool Bold { get; init; }
    }

    /// <summary>
    /// Immutable snapshot of a read-only tool window's name/value content, captured on the game thread and
    /// handed to the WPF view model. Snapshots are taken on a dispatcher-timer cadence because the
    /// underlying debug/info providers refresh every frame.
    /// </summary>
    internal sealed record ToolWindowSnapshot
    {
        /// <summary>The ordered name/value rows to display.</summary>
        public ImmutableArray<ToolWindowRow> Rows { get; init; }

        /// <summary>An empty snapshot used before any content is available.</summary>
        public static ToolWindowSnapshot Empty { get; } = new ToolWindowSnapshot { Rows = ImmutableArray<ToolWindowRow>.Empty };
    }
}
