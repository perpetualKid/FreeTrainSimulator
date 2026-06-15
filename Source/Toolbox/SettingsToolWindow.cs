using System;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Hosted-mode bridge between <see cref="GameWindow"/> and a dockable WPF settings tool window.
    /// <para>
    /// Unlike the read-only snapshot tool windows, settings are interactive/two-way: the WPF view model
    /// reads the current boolean values directly through the injected getters and forwards changes back
    /// through the injected setters (which marshal onto the game thread so all MonoGame/WinForms state stays
    /// single-threaded). Injecting delegates instead of the concrete <see cref="GameWindow"/> keeps this
    /// bridge decoupled and unit-testable, mirroring <see cref="TrainPathToolWindow"/>.
    /// </para>
    /// </summary>
    internal sealed class SettingsToolWindow
    {
        private readonly Func<bool> enableLoggingAccessor;
        private readonly Func<bool> restoreLastViewAccessor;
        private readonly Func<bool> fontOutlineAccessor;
        private readonly Func<bool> realTrackWidthAccessor;
        private readonly Action<bool> enableLoggingSetter;
        private readonly Action<bool> restoreLastViewSetter;
        private readonly Action<bool> fontOutlineSetter;
        private readonly Action<bool> realTrackWidthSetter;

        internal SettingsToolWindow(
            Func<bool> enableLoggingAccessor,
            Func<bool> restoreLastViewAccessor,
            Func<bool> fontOutlineAccessor,
            Func<bool> realTrackWidthAccessor,
            Action<bool> enableLoggingSetter,
            Action<bool> restoreLastViewSetter,
            Action<bool> fontOutlineSetter,
            Action<bool> realTrackWidthSetter)
        {
            this.enableLoggingAccessor = enableLoggingAccessor ?? throw new ArgumentNullException(nameof(enableLoggingAccessor));
            this.restoreLastViewAccessor = restoreLastViewAccessor ?? throw new ArgumentNullException(nameof(restoreLastViewAccessor));
            this.fontOutlineAccessor = fontOutlineAccessor ?? throw new ArgumentNullException(nameof(fontOutlineAccessor));
            this.realTrackWidthAccessor = realTrackWidthAccessor ?? throw new ArgumentNullException(nameof(realTrackWidthAccessor));
            this.enableLoggingSetter = enableLoggingSetter ?? throw new ArgumentNullException(nameof(enableLoggingSetter));
            this.restoreLastViewSetter = restoreLastViewSetter ?? throw new ArgumentNullException(nameof(restoreLastViewSetter));
            this.fontOutlineSetter = fontOutlineSetter ?? throw new ArgumentNullException(nameof(fontOutlineSetter));
            this.realTrackWidthSetter = realTrackWidthSetter ?? throw new ArgumentNullException(nameof(realTrackWidthSetter));
        }

        /// <summary>Display title for the dock pane.</summary>
        public string Title => "Settings";

        /// <summary>Whether logging is enabled.</summary>
        public bool EnableLogging => enableLoggingAccessor();

        /// <summary>Whether the last view is restored on start.</summary>
        public bool RestoreLastView => restoreLastViewAccessor();

        /// <summary>Whether map text uses a font outline.</summary>
        public bool FontOutline => fontOutlineAccessor();

        /// <summary>Whether the map renders real (unlimited) track width.</summary>
        public bool RealTrackWidth => realTrackWidthAccessor();

        /// <summary>Enables or disables logging.</summary>
        public void SetEnableLogging(bool value) => enableLoggingSetter(value);

        /// <summary>Sets whether the last view is restored on start.</summary>
        public void SetRestoreLastView(bool value) => restoreLastViewSetter(value);

        /// <summary>Sets the font-outline preference and re-applies the dependent colour/debug-screen state.</summary>
        public void SetFontOutline(bool value) => fontOutlineSetter(value);

        /// <summary>Sets the real-track-width preference and re-applies the dependent map redraw.</summary>
        public void SetRealTrackWidth(bool value) => realTrackWidthSetter(value);
    }
}