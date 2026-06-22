using System.Collections.Immutable;
using System.Drawing;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// A single name/value row in a tool-window snapshot, with the formatting needed to render it.
    /// Uses BCL-only types so the WPF shell can consume snapshots without referencing MonoGame.
    /// </summary>
    internal readonly record struct ToolWindowRow(string Name, string Value, Color? Color, bool Bold);

    /// <summary>
    /// Immutable snapshot of a read-only tool window's name/value content, captured on the game thread and
    /// handed to the WPF view model. Snapshots are taken on a dispatcher-timer cadence because the
    /// underlying debug/info providers refresh every frame.
    /// </summary>
    internal sealed record ToolWindowSnapshot(ImmutableArray<ToolWindowRow> Rows)
    {
        /// <summary>An empty snapshot used before any content is available.</summary>
        public static ToolWindowSnapshot Empty { get; } = new ToolWindowSnapshot(ImmutableArray<ToolWindowRow>.Empty);
    }
}
