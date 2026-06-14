using System;
using System.Diagnostics;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Hosted-mode bridge between <see cref="GameWindow"/> and a dockable WPF settings tool window.
    /// <para>
    /// Unlike the read-only snapshot tool windows, settings are interactive/two-way: the WPF view model
    /// reads the current boolean values directly and forwards changes back onto the game thread via
    /// <see cref="GameWindow.InvokeOnGameThread(Action)"/> so all MonoGame/WinForms state stays
    /// single-threaded. This mirrors the legacy <c>SettingsWindow</c> popup behaviour and its side effects.
    /// </para>
    /// </summary>
    internal sealed class SettingsToolWindow
    {
        private readonly GameWindow game;

        internal SettingsToolWindow(GameWindow game)
        {
            ArgumentNullException.ThrowIfNull(game);
            this.game = game;
        }

        /// <summary>Display title for the dock pane.</summary>
        public string Title => "Settings";

        /// <summary>Whether logging is enabled (log level above <see cref="TraceEventType.Critical"/>).</summary>
        public bool EnableLogging => game.ToolboxUserSettings.LogLevel != TraceEventType.Critical;

        /// <summary>Whether the last view is restored on start.</summary>
        public bool RestoreLastView => game.ToolboxSettings.RestoreLastView;

        /// <summary>Whether map text uses a font outline.</summary>
        public bool FontOutline => game.ToolboxSettings.FontOutline;

        /// <summary>Whether the map renders real (unlimited) track width.</summary>
        public bool RealTrackWidth => !game.ToolboxSettings.LimitTrackWidth;

        /// <summary>Enables or disables logging by switching the log level.</summary>
        public void SetEnableLogging(bool value)
            => game.InvokeOnGameThread(() => game.ToolboxUserSettings.LogLevel = value ? TraceEventType.Verbose : TraceEventType.Critical);

        /// <summary>Sets whether the last view is restored on start.</summary>
        public void SetRestoreLastView(bool value)
            => game.InvokeOnGameThread(() => game.ToolboxSettings.RestoreLastView = value);

        /// <summary>Sets the font-outline preference and re-applies the dependent colour/debug-screen state.</summary>
        public void SetFontOutline(bool value)
            => game.InvokeOnGameThread(() => game.UpdateFontOutlinePreference(value));

        /// <summary>Sets the real-track-width preference and re-applies the dependent map redraw.</summary>
        public void SetRealTrackWidth(bool value)
            => game.InvokeOnGameThread(() => game.UpdateTrackWidthPreference(!value));
    }
}
