using FreeTrainSimulator.Toolbox.PopupWindows;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Abstraction over a hosted, read-only toolbox tool window that the WPF shell binds to. Mirrors
    /// <see cref="IToolboxMenu"/>: a hosted bridge owns the game-side data and the WPF view model reads it
    /// on the dispatcher.
    /// <para>
    /// Unlike the menu (which pushes discrete changes through events), tool windows expose a pull model:
    /// the WPF side captures an immutable <see cref="ToolWindowSnapshot"/> on a timer cadence, because the
    /// underlying debug/info providers refresh every frame and pushing per-frame would flood the dispatcher.
    /// </para>
    /// </summary>
    internal interface IToolboxToolWindow
    {
        /// <summary>Stable identity of the tool window, used for dock layout persistence and lookup.</summary>
        ToolboxWindowType WindowType { get; }

        /// <summary>Display title for the dock pane.</summary>
        string Title { get; }

        /// <summary>
        /// Whether the WPF shell currently shows the tool window. When false the game thread skips snapshot
        /// rebuilds so a hidden pane costs nothing on the game loop. Set by the shell on show/hide.
        /// </summary>
        bool Active { get; set; }

        /// <summary>
        /// Captures an immutable snapshot of the current name/value rows. Safe to call from the WPF UI
        /// thread; the implementation is responsible for reading game-thread state without blocking the
        /// game loop.
        /// </summary>
        ToolWindowSnapshot CaptureSnapshot();
    }
}
