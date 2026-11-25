using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

namespace FreeTrainSimulator.Toolbox
{
    public partial class MainWindow
    {
        private ShellSettingsApplier shellSettingsApplier;

        private void InitializeShellSettings()
        {
            shellSettingsApplier = new ShellSettingsApplier(this);
            BindSettings(new SettingsToolWindow(toolboxSettings, userSettings, shellSettingsApplier));
        }

        private void BindSettings(SettingsToolWindow settings)
        {
            viewModel.SettingsTool?.Dispose();
            viewModel.SettingsTool = new SettingsToolWindowViewModel(settings)
            {
                ProfileName = currentProfile.Name,
                ResetCommand = viewModel.ResetSettingsCommand,
            };
            viewModel.ResetSettingsCommand.RaiseCanExecuteChanged();
        }

        private sealed class ShellSettingsApplier : ISettingsApplier
        {
            private readonly MainWindow shell;
            private readonly (bool RestoreLastView, bool FontOutline, bool LimitTrackWidth) initialAppearance;
            private readonly EnumArray<string, ColorSetting> initialColors;
            private readonly EnumArray<bool, MapContentType> initialVisibility;
            private readonly string initialLanguage;
            private readonly TraceEventType initialLogLevel;

            public ShellSettingsApplier(MainWindow shell)
            {
                this.shell = shell;
                ProfileToolboxSettingsModel settings = shell.toolboxSettings;
                initialAppearance = (settings.RestoreLastView, settings.FontOutline, settings.LimitTrackWidth);
                initialColors = new EnumArray<string, ColorSetting>(settings.ColorSettings);
                initialVisibility = new EnumArray<bool, MapContentType>(settings.ViewSettings);
                initialLanguage = shell.userSettings.Language;
                initialLogLevel = shell.userSettings.LogLevel;
            }

            public void ApplyEnableLogging(bool value)
            {
                shell.userSettings.LogLevel = value ? TraceEventType.Verbose : TraceEventType.Critical;
                if (value)
                {
                    shell.logFileName = RuntimeInfo.LogFile(shell.userSettings.LogFilePath, shell.userSettings.LogFileName);
                    LoggingUtil.InitLogging(shell.logFileName, shell.userSettings.LogLevel, false, true);
                    shell.toolboxSettings.Log();
                }
                else
                {
                    LoggingUtil.StopLogging();
                }
            }

            public void ApplyLanguage(string language)
            {
                shell.userSettings.Language = language;
                shell.ApplyWpfLanguage(language);
            }

            public void ApplyFontOutline(bool value) => shell.toolboxSettings.FontOutline = value;

            public void ApplyRealTrackWidth(bool value) => shell.toolboxSettings.LimitTrackWidth = !value;

            public void ApplyColorPreference(ColorSetting setting, string colorName) => shell.toolboxSettings.ColorSettings[setting] = colorName;

            // The bridge has already reset the settings model; there is no live map to redraw.
            public void ReapplyAppearance() { }

            public void CopyChangedToolboxPreferences(ProfileToolboxSettingsModel target)
            {
                ProfileToolboxSettingsModel settings = shell.toolboxSettings;
                if (settings.RestoreLastView != initialAppearance.RestoreLastView)
                    target.RestoreLastView = settings.RestoreLastView;
                if (settings.FontOutline != initialAppearance.FontOutline)
                    target.FontOutline = settings.FontOutline;
                if (settings.LimitTrackWidth != initialAppearance.LimitTrackWidth)
                    target.LimitTrackWidth = settings.LimitTrackWidth;
                foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
                {
                    if (!string.Equals(settings.ColorSettings[setting], initialColors[setting], StringComparison.Ordinal))
                        target.ColorSettings[setting] = settings.ColorSettings[setting];
                }
                foreach (MapContentType setting in EnumExtension.GetValues<MapContentType>())
                {
                    if (settings.ViewSettings[setting] != initialVisibility[setting])
                        target.ViewSettings[setting] = settings.ViewSettings[setting];
                }
            }

            public async Task SaveChangedUserPreferencesAsync()
            {
                string language = shell.userSettings.Language;
                TraceEventType logLevel = shell.userSettings.LogLevel;
                bool languageChanged = !string.Equals(language ?? string.Empty, initialLanguage ?? string.Empty, StringComparison.Ordinal);
                bool logLevelChanged = logLevel != initialLogLevel;
                if (!languageChanged && !logLevelChanged)
                    return;

                ProfileUserSettingsModel target = await shell.currentProfile.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None).ConfigureAwait(false);
                if (languageChanged)
                    target.Language = language;
                if (logLevelChanged)
                    target.LogLevel = logLevel;
                _ = await shell.currentProfile.UpdateSettingsModel(target, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}