using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

using GetText;

namespace FreeTrainSimulator.Toolbox
{
    public partial class MainWindow
    {
        private ShellSettingsApplier shellSettingsApplier;
        private ManageProfilesViewModel profileManagement;

        private async Task ReopenProfileRouteAsync()
        {
            if (!string.Equals(toolboxSettings.Folder, routeToReopen.Folder, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(toolboxSettings.RouteId, routeToReopen.Route, StringComparison.OrdinalIgnoreCase))
            {
                toolboxSettings.PathId = null;
                toolboxSettings.ContentPosition = PointD.None;
                toolboxSettings.ContentScale = 1;
            }
            toolboxSettings.Folder = routeToReopen.Folder;
            toolboxSettings.RouteId = routeToReopen.Route;
            IsEnabled = false;
            reopeningProfileRoute = true;
            try
            {
                await StartMapAsync().ConfigureAwait(true);
                await MapHost.WaitForInitialRouteAsync().ConfigureAwait(true);
                (string folder, string route, _) = await MapHost.CaptureProfileSwitchStateAsync().ConfigureAwait(true);
                if (!string.Equals(folder, routeToReopen.Folder, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(route, routeToReopen.Route, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(CatalogManager.Catalog.GetString("The requested route could not be loaded."));
            }
            catch (Exception failure) when (CleanupExceptionPolicy.CanContinue(failure))
            {
                Trace.TraceError($"Could not reopen route after switching Toolbox profiles: {failure}");
                string status = CatalogManager.Catalog.GetString("Could not reopen the route: {0}", failure.Message);
                skipWorkspaceSave = true;
                await ShutdownAsync().ConfigureAwait(true);
                await ShowReplacementToolboxAsync(currentProfile, default, status).ConfigureAwait(true);
                Close();
            }
            finally
            {
                reopeningProfileRoute = false;
                if (!shutdownCompleted)
                    IsEnabled = true;
            }
        }

        private async Task SwitchToolboxProfileAsync(ProfileModel target)
        {
            if (isShuttingDown || shutdownTask != null)
                return;
            if (mapStartupTask != null && !mapStartupTask.IsCompleted)
            {
                MessageBox.Show(this, CatalogManager.Catalog.GetString("Wait for the map workspace to finish starting before switching profiles."),
                    CatalogManager.Catalog.GetString("Switch Toolbox Profile"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            isShuttingDown = true;
            try
            {
                IsEnabled = false;
                (string Folder, string Route, bool Unsaved) state = MapHost == null ? default : await MapHost.CaptureProfileSwitchStateAsync().ConfigureAwait(true);
                if (state.Unsaved && MessageBox.Show(this,
                        CatalogManager.Catalog.GetString("Switching profiles will discard unsaved path changes. Continue?"),
                        CatalogManager.Catalog.GetString("Unsaved Path Changes"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                bool reopenRoute = false;
                if (state.Route != null)
                {
                    MessageBoxResult choice = MessageBox.Show(this,
                        CatalogManager.Catalog.GetString("Reopen the current route under profile '{0}'? Choose No to switch with the map closed.", target.Name),
                        CatalogManager.Catalog.GetString("Switch Toolbox Profile"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (choice == MessageBoxResult.Cancel)
                        return;
                    reopenRoute = choice == MessageBoxResult.Yes;
                }

                if (!(await target.RefreshProfiles(CancellationToken.None).ConfigureAwait(true)).Any(profile => string.Equals(profile.Name, target.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(CatalogManager.Catalog.GetString("The selected profile no longer exists."));

                await ShutdownAsync().ConfigureAwait(true);
                await ShowReplacementToolboxAsync(target, reopenRoute ? (state.Folder, state.Route) : default, null).ConfigureAwait(true);
            }
            catch (Exception failure) when (CleanupExceptionPolicy.CanContinue(failure))
            {
                Trace.TraceError($"Failed to switch Toolbox profile: {failure}");
                MessageBox.Show(this, CatalogManager.Catalog.GetString("Could not switch Toolbox profiles: {0}", failure.Message),
                    CatalogManager.Catalog.GetString("Switch Toolbox Profile"), MessageBoxButton.OK, MessageBoxImage.Error);
                if (!shutdownCompleted)
                    return;

                try
                {
                    await ShowReplacementToolboxAsync(currentProfile, default, null).ConfigureAwait(true);
                }
                catch (Exception recoveryFailure) when (CleanupExceptionPolicy.CanContinue(recoveryFailure))
                {
                    Trace.TraceError($"Failed to reopen the previous Toolbox profile: {recoveryFailure}");
                }
            }
            finally
            {
                isShuttingDown = false;
                if (!shutdownCompleted)
                    IsEnabled = true;
            }
            Close();
        }

        private Task ShowReplacementToolboxAsync(ProfileModel profile, (string Folder, string Route) route, string status)
        {
            // Once shown, the replacement closes and disposes itself. The show method cleans up on failure.
#pragma warning disable CA2000
            MainWindow next = new MainWindow(profile, route, status);
#pragma warning restore CA2000
            return next.ShowForProfileHandoffAsync(this);
        }

        private async Task ShowForProfileHandoffAsync(MainWindow previous)
        {
            // Do not open a new workspace until showing the window has succeeded.
            Loaded -= MainWindow_Loaded;
            try
            {
                Show();
                if (Application.Current?.MainWindow == previous)
                    Application.Current.MainWindow = this;
            }
            catch (Exception failure) when (CleanupExceptionPolicy.CanContinue(failure))
            {
                skipWorkspaceSave = true;
                await ShutdownAsync().ConfigureAwait(true);
                Close();
                throw;
            }

            if (IsLoaded)
                _ = StartShellAsync();
            else
                Loaded += MainWindow_Loaded;
        }

        private void InitializeShellSettings()
        {
            shellSettingsApplier = new ShellSettingsApplier(this);
            profileManagement = new ManageProfilesViewModel(currentProfile)
            {
                CaptureActiveConfigurationAsync = CaptureActiveConfigurationAsync,
                UseProfileAsync = SwitchToolboxProfileAsync,
            };
            BindSettings(new SettingsToolWindow(toolboxSettings, userSettings, shellSettingsApplier));
        }

        private async Task<(ProfileToolboxSettingsModel Toolbox, ProfileUserSettingsModel User)> CaptureActiveConfigurationAsync(ProfileModel destination, CancellationToken cancellationToken)
        {
            ProfileUserSettingsModel stored = await currentProfile.LoadSettingsModel<ProfileUserSettingsModel>(cancellationToken).ConfigureAwait(true);
            (ProfileToolboxSettingsModel preferences, string language, TraceEventType logLevel) = MapHost?.HostedServices != null
                ? await MapHost.CaptureProfilePreferencesAsync(destination).ConfigureAwait(true)
                : (toolboxSettings.CopyPreferences(destination), userSettings.Language, userSettings.LogLevel);
            shellSettingsApplier.CopyChangedUserPreferences(stored, language, logLevel);
            return (preferences, stored);
        }

        private void BindSettings(SettingsToolWindow settings)
        {
            viewModel.SettingsTool?.Dispose();
            viewModel.SettingsTool = new SettingsToolWindowViewModel(settings)
            {
                ProfileName = currentProfile.Name,
                ProfileManagement = profileManagement,
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
                if (!LanguageChanged(language) && logLevel == initialLogLevel)
                    return;

                ProfileUserSettingsModel target = await shell.currentProfile.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None).ConfigureAwait(false);
                CopyChangedUserPreferences(target, language, logLevel);
                _ = await shell.currentProfile.UpdateSettingsModel(target, CancellationToken.None).ConfigureAwait(false);
            }

            public void CopyChangedUserPreferences(ProfileUserSettingsModel target, string language, TraceEventType logLevel)
            {
                if (LanguageChanged(language))
                    target.Language = language;
                if (logLevel != initialLogLevel)
                    target.LogLevel = logLevel;
            }

            private bool LanguageChanged(string language) => !string.Equals(language ?? string.Empty, initialLanguage ?? string.Empty, StringComparison.Ordinal);
        }
    }
}