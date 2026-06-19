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
    /// changes back. The restore-last-view preference is read/written directly on the injected settings model.
    /// The preferences with game-side side effects (logging, font outline, real track width, item visibility,
    /// item colors) are applied through the injected callbacks, which <see cref="GameWindow"/> marshals onto the
    /// game thread. Injecting the models/callbacks instead of the concrete <see cref="GameWindow"/> keeps this
    /// bridge decoupled and unit-testable.
    /// </para>
    /// </summary>
    internal sealed class SettingsToolWindow
    {
        private readonly ProfileToolboxSettingsModel toolboxSettings;
        private readonly ProfileUserSettingsModel userSettings;
        private readonly Action<bool> applyEnableLogging;
        private readonly Action<bool> applyFontOutline;
        private readonly Action<bool> applyRealTrackWidth;
        private readonly Action<MapContentType, bool> applyItemVisibility;
        private readonly Action<ColorSetting, string> applyColorPreference;

        private static readonly IReadOnlyList<string> availableColorNames =
            ColorExtension.ColorCodes
                .OrderByDescending(kvp => (kvp.Value.R << 16) + (kvp.Value.G << 8) + kvp.Value.B)
                .Select(kvp => kvp.Key)
                .ToList();

        internal SettingsToolWindow(
            ProfileToolboxSettingsModel toolboxSettings,
            ProfileUserSettingsModel userSettings,
            Action<bool> applyEnableLogging,
            Action<bool> applyFontOutline,
            Action<bool> applyRealTrackWidth,
            Action<MapContentType, bool> applyItemVisibility,
            Action<ColorSetting, string> applyColorPreference)
        {
            this.toolboxSettings = toolboxSettings ?? throw new ArgumentNullException(nameof(toolboxSettings));
            this.userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
            this.applyEnableLogging = applyEnableLogging ?? throw new ArgumentNullException(nameof(applyEnableLogging));
            this.applyFontOutline = applyFontOutline ?? throw new ArgumentNullException(nameof(applyFontOutline));
            this.applyRealTrackWidth = applyRealTrackWidth ?? throw new ArgumentNullException(nameof(applyRealTrackWidth));
            this.applyItemVisibility = applyItemVisibility ?? throw new ArgumentNullException(nameof(applyItemVisibility));
            this.applyColorPreference = applyColorPreference ?? throw new ArgumentNullException(nameof(applyColorPreference));
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
            => applyEnableLogging(value);

        /// <summary>Sets whether the last view is restored on start.</summary>
        public void SetRestoreLastView(bool value)
            => toolboxSettings.RestoreLastView = value;

        /// <summary>Sets the font-outline preference and re-applies the dependent colour/debug-screen state.</summary>
        public void SetFontOutline(bool value)
            => applyFontOutline(value);

        /// <summary>Sets the real-track-width preference and re-applies the dependent map redraw.</summary>
        public void SetRealTrackWidth(bool value)
            => applyRealTrackWidth(value);

        /// <summary>The list of selectable color names, sorted for display.</summary>
        public IReadOnlyList<string> AvailableColorNames => availableColorNames;

        /// <summary>Reads the current visibility state for a map content type.</summary>
        public bool GetItemVisibility(MapContentType setting)
            => toolboxSettings.ViewSettings[setting];

        /// <summary>Sets the visibility for a map content type and applies it to the live map.</summary>
        public void SetItemVisibility(MapContentType setting, bool value)
            => applyItemVisibility(setting, value);

        /// <summary>Reads the current color name for a color setting.</summary>
        public string GetColorPreference(ColorSetting setting)
            => toolboxSettings.ColorSettings[setting];

        /// <summary>Sets the color for a color setting and applies it to the live map.</summary>
        public void SetColorPreference(ColorSetting setting, string colorName)
            => applyColorPreference(setting, colorName);
    }
}