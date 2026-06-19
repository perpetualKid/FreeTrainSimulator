using FreeTrainSimulator.Graphics;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Applies the settings tool window's appearance preferences that have live game-side side effects (as
    /// opposed to plain model writes). Implementations are responsible for marshaling each call onto the game
    /// thread, where the MonoGame map state lives. Injecting this role instead of the concrete
    /// <see cref="GameWindow"/> keeps <see cref="SettingsToolWindow"/> decoupled and unit-testable.
    /// </summary>
    internal interface ISettingsApplier
    {
        /// <summary>Enables or disables logging by switching the log level.</summary>
        void ApplyEnableLogging(bool value);

        /// <summary>Applies the font-outline preference and re-applies the dependent colour/debug-screen state.</summary>
        void ApplyFontOutline(bool value);

        /// <summary>Applies the real-track-width preference and re-applies the dependent map redraw.</summary>
        void ApplyRealTrackWidth(bool value);

        /// <summary>Applies a single color preference to the live map.</summary>
        void ApplyColorPreference(ColorSetting setting, string colorName);

        /// <summary>Re-applies the current appearance model values to the live map (used after a reset).</summary>
        void ReapplyAppearance();
    }
}
