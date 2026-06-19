using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Toolbox.Settings;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Hosted-mode bridge between <see cref="GameWindow"/> and a dockable WPF settings tool window.
    /// <para>
    /// Settings are interactive/two-way: the WPF view model reads the current boolean values and writes
    /// changes back. Preferences that are plain model writes (restore-last-view, item visibility) are applied
    /// directly on the injected settings model. Preferences with live game-side side effects (logging, font
    /// outline, real track width, item colors) are applied through the injected <see cref="ISettingsApplier"/>,
    /// which marshals them onto the game thread. Injecting the model/applier instead of the concrete
    /// <see cref="GameWindow"/> keeps this bridge decoupled and unit-testable.
    /// </para>
    /// </summary>
    internal sealed class SettingsToolWindow
    {
        private readonly ProfileToolboxSettingsModel toolboxSettings;
        private readonly ProfileUserSettingsModel userSettings;
        private readonly ISettingsApplier applier;

        private static readonly IReadOnlyList<string> availableColorNames =
            ColorExtension.ColorCodes
                .OrderByDescending(kvp => (kvp.Value.R << 16) + (kvp.Value.G << 8) + kvp.Value.B)
                .Select(kvp => kvp.Key)
                .ToList();

        internal SettingsToolWindow(
            ProfileToolboxSettingsModel toolboxSettings,
            ProfileUserSettingsModel userSettings,
            ISettingsApplier applier)
        {
            this.toolboxSettings = toolboxSettings ?? throw new ArgumentNullException(nameof(toolboxSettings));
            this.userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
            this.applier = applier ?? throw new ArgumentNullException(nameof(applier));
        }

        /// <summary>Display title for the dock pane.</summary>
        public string Title => "Settings";

        /// <summary>Whether logging is enabled (log level above <see cref="TraceEventType.Critical"/>).</summary>
        public bool EnableLogging => userSettings.LogLevel != TraceEventType.Critical;

        /// <summary>Whether the last view is restored on start.</summary>
        public bool RestoreLastView => toolboxSettings.RestoreLastView;

        /// <summary>Whether map text uses a font outline.</summary>
        public bool FontOutline => toolboxSettings.FontOutline;

        /// <summary>Whether the map renders real (unlimited) track width.</summary>
        public bool RealTrackWidth => !toolboxSettings.LimitTrackWidth;

        /// <summary>Enables or disables logging by switching the log level.</summary>
        public void SetEnableLogging(bool value)
            => applier.ApplyEnableLogging(value);

        /// <summary>Sets whether the last view is restored on start.</summary>
        public void SetRestoreLastView(bool value)
            => toolboxSettings.RestoreLastView = value;

        /// <summary>Sets the font-outline preference and re-applies the dependent colour/debug-screen state.</summary>
        public void SetFontOutline(bool value)
            => applier.ApplyFontOutline(value);

        /// <summary>Sets the real-track-width preference and re-applies the dependent map redraw.</summary>
        public void SetRealTrackWidth(bool value)
            => applier.ApplyRealTrackWidth(value);

        /// <summary>The list of selectable color names, sorted for display.</summary>
        public IReadOnlyList<string> AvailableColorNames => availableColorNames;

        /// <summary>Reads the current visibility state for a map content type.</summary>
        public bool GetItemVisibility(MapContentType setting)
            => toolboxSettings.ViewSettings[setting];

        /// <summary>
        /// Sets the visibility for a map content type. This is a plain model write; the live map reads the
        /// visibility flags during draw, so no game-side re-apply is needed.
        /// </summary>
        public void SetItemVisibility(MapContentType setting, bool value)
            => toolboxSettings.ViewSettings[setting] = value;

        /// <summary>Reads the current color name for a color setting.</summary>
        public string GetColorPreference(ColorSetting setting)
            => toolboxSettings.ColorSettings[setting];

        /// <summary>Sets the color for a color setting and applies it to the live map.</summary>
        public void SetColorPreference(ColorSetting setting, string colorName)
            => applier.ApplyColorPreference(setting, colorName);

        /// <summary>
        /// Resets the appearance-related preferences (item colors, item visibility, font outline, real track
        /// width, and restore-last-view) to their model defaults, then re-applies them to the live map through
        /// the injected applier. Logging is intentionally left unchanged. The window layout is reset separately
        /// by the WPF shell.
        /// </summary>
        public void ResetToDefaults()
        {
            ProfileToolboxSettingsModel defaults = new ProfileToolboxSettingsModel();

            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
                toolboxSettings.ColorSettings[setting] = defaults.ColorSettings[setting];

            foreach (MapContentType setting in EnumExtension.GetValues<MapContentType>())
                toolboxSettings.ViewSettings[setting] = defaults.ViewSettings[setting];

            toolboxSettings.FontOutline = defaults.FontOutline;
            toolboxSettings.LimitTrackWidth = defaults.LimitTrackWidth;
            toolboxSettings.RestoreLastView = defaults.RestoreLastView;

            applier.ReapplyAppearance();
        }
    }
}