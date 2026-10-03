using System;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    /// <summary>
    /// Identifies the configured tool-window command requested by the native map keyboard adapter.
    /// </summary>
    internal sealed class ToolWindowShortcutRequestedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the tool-window command to dispatch through the shell's existing toggle command.
        /// </summary>
        public UserCommand Command { get; }

        /// <summary>
        /// Creates a request for the specified configured command.
        /// </summary>
        public ToolWindowShortcutRequestedEventArgs(UserCommand command)
        {
            Command = command;
        }
    }
}
