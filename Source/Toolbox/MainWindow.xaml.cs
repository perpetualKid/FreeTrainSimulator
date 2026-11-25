using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using AvalonDock.Layout;
using AvalonDock.Serializer.Json;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Dialogs;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ViewModels;

using GetText;

namespace FreeTrainSimulator.Toolbox
{
    public partial class MainWindow : Window, IDisposable
    {
        private const string RouteToolWindowContentId = "RouteToolWindow";
        private const string LocationToolWindowContentId = "LocationToolWindow";
        private const string DebugToolWindowContentId = "DebugToolWindow";
        private const string LogToolWindowContentId = "LogToolWindow";
        private const string RouteNavigationToolWindowContentId = "RouteNavigationToolWindow";
        private const string HelpToolWindowContentId = "HelpToolWindow";
        private const string SettingsToolWindowContentId = "SettingsToolWindow";
        private const string TrainPathToolWindowContentId = "TrainPathToolWindow";
        private const string PathChartToolWindowContentId = "PathChartToolWindow";

        private readonly MainWindowViewModel viewModel = new MainWindowViewModel();
        private ProfileModel currentProfile;
        private ProfileToolboxSettingsModel toolboxSettings;
        private ProfileUserSettingsModel userSettings;
        private ToolboxGameHostControl MapHost;
        private Task shellInitializationTask;
        private Task mapStartupTask;
        private RouteBrowserViewModel routeBrowser;
        private bool shellInitialized;
        private bool showRoutesWhenReady;
        private string logFileName;
        private string defaultDockLayoutJson;
        private string initialDockLayoutJson;
        private bool isShuttingDown;
        private bool shutdownCompleted;
        private Task shutdownTask;
        private bool disposed;
        private int deactivationSequence;
        private ToolWindowDescriptor[] toolWindowDescriptors;
        private ToolWindowRefreshScheduler refreshScheduler;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = viewModel;
            InitializeToolWindowDescriptors();
            InitializeToolWindowCommands();
            ApplyDefaultFloatingSizes();

            Loaded += MainWindow_Loaded;
            Activated += MainWindow_Activated;
            Deactivated += MainWindow_Deactivated;
            shortcutInputManager.PreProcessInput += InputManager_PreProcessInput;
            shortcutApplication?.Deactivated += ShortcutApplication_Deactivated;
            DockingManager.ActiveContentChanged += DockingManager_ActiveContentChanged;
        }

        private void SubscribeMapHost()
        {
            MapHost.GotFocus += MapHost_GotFocus;
            MapHost.GotKeyboardFocus += MapHost_GotKeyboardFocus;
            MapHost.MouseEnter += MapHost_MouseEnter;
            MapHost.PreviewMouseDown += MapHost_PreviewMouseDown;
            MapHost.HostedWindowPointerDown += MapHost_HostedWindowPointerDown;
            MapHost.HostedMenuReady += MapHost_HostedMenuReady;
            MapHost.HostedToolWindowsReady += MapHost_HostedToolWindowsReady;
            MapHost.SaveTrainPathRequested += MapHost_SaveTrainPathRequested;
            MapHost.UnsavedPathConfirmationRequested += MapHost_UnsavedPathConfirmationRequested;
            MapHost.ScreenshotRequested += MapHost_ScreenshotRequested;
            MapHost.AboutRequested += MapHost_AboutRequested;
            MapHost.ExitRequested += MapHost_ExitRequested;
            MapHost.LanguageChanged += MapHost_LanguageChanged;
            MapHost.MapContextMenuRequested += MapHost_MapContextMenuRequested;
            MapHost.ToolWindowShortcutRequested += MapHost_ToolWindowShortcutRequested;
        }

        private void MapHost_UnsavedPathConfirmationRequested(object sender, UnsavedPathConfirmationEventArgs e)
        {
            if (e.Completion.Task.IsCompleted)
                return;

            MessageBoxResult result = MessageBox.Show(this,
                CatalogManager.Catalog.GetString("There are unsaved path changes. Changing the route will discard them. Continue?"),
                CatalogManager.Catalog.GetString("Unsaved Path Changes"), MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            e.Completion.TrySetResult(result == MessageBoxResult.Yes);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await (shellInitializationTask ??= InitializeShellAsync()).ConfigureAwait(true);
                if (!isShuttingDown && shutdownTask == null && ShouldRestoreRoute())
                    await StartMapAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (CleanupExceptionPolicy.CanContinue(ex))
            {
                Trace.TraceError($"Failed to initialize Toolbox shell: {ex}");
                viewModel.WorkspaceStatus = CatalogManager.Catalog.GetString("Toolbox startup failed. See the log for details.") + Environment.NewLine + ex.Message;
                viewModel.ShowRouteToolCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task InitializeShellAsync()
        {
            viewModel.WorkspaceStatus = CatalogManager.Catalog.GetString("Loading profile settings...");
            currentProfile = await currentProfile.Current(CancellationToken.None).ConfigureAwait(true);
            userSettings = await currentProfile.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None).ConfigureAwait(true);
            toolboxSettings = await currentProfile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(true);
            CatalogManager.SetCatalogDomainPattern(CatalogDomainPattern.AssemblyName, null, RuntimeInfo.LocalesFolder);
            if (userSettings.LogLevel != TraceEventType.Critical)
            {
                logFileName = RuntimeInfo.LogFile(userSettings.LogFilePath, userSettings.LogFileName);
                LoggingUtil.InitLogging(logFileName, userSettings.LogLevel, false, false);
                toolboxSettings.Log();
            }
            ApplyWpfLanguage(userSettings.Language);
            await LoadDockLayoutAsync().ConfigureAwait(true);
            SynchronizeMapDocument();
            RestoreWindowPlacement();
            InitializeShellSettings();
            HookToolWindowAnchorables();
            UpdateAllToolWindowLifecycles();
            initialDockLayoutJson = SerializeDockLayout();
            routeBrowser = new RouteBrowserViewModel(OpenBrowsedRoute);
            viewModel.SetRouteBrowser(routeBrowser);
            await routeBrowser.InitializeAsync(toolboxSettings.Folder).ConfigureAwait(true);
            viewModel.WorkspaceStatus = CatalogManager.Catalog.GetString("Use File > Open Route to load the last route or choose a route.");
            shellInitialized = true;
            viewModel.ShowRouteToolCommand.RaiseCanExecuteChanged();
            viewModel.ToggleRouteToolCommand.RaiseCanExecuteChanged();
        }

        private bool ShouldRestoreRoute() => toolboxSettings?.RestoreLastView == true
            && !string.IsNullOrWhiteSpace(toolboxSettings.Folder) && !string.IsNullOrWhiteSpace(toolboxSettings.RouteId);

        private void OpenBrowsedRoute(FolderModel folder, RouteModelHeader route)
        {
            if (MapHost != null || !CanOpenRouteWorkspace())
                return;
            if (!string.Equals(toolboxSettings.Folder, folder.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(toolboxSettings.RouteId, route.Id, StringComparison.OrdinalIgnoreCase))
            {
                toolboxSettings.PathId = null;
                toolboxSettings.ContentPosition = FreeTrainSimulator.Common.Position.PointD.None;
                toolboxSettings.ContentScale = 1;
            }
            toolboxSettings.Folder = folder.Id;
            toolboxSettings.RouteId = route.Id;
            OpenRouteWorkspace();
        }

        private bool CanOpenRouteWorkspace() => shellInitialized && !isShuttingDown && shutdownTask == null
            && (mapStartupTask == null || mapStartupTask.IsCompletedSuccessfully);

        private async void OpenRouteWorkspace()
        {
            try
            {
                showRoutesWhenReady = true;
                await StartMapAsync().ConfigureAwait(true);
                if (!isShuttingDown && shutdownTask == null && viewModel.Menu != null)
                {
                    showRoutesWhenReady = false;
                    ShowToolWindow(RouteToolWindowContentId);
                }
            }
            catch (Exception ex) when (CleanupExceptionPolicy.CanContinue(ex))
            {
                Trace.TraceError($"Failed to open the map workspace: {ex}");
                viewModel.WorkspaceStatus = CatalogManager.Catalog.GetString("The map workspace could not start. See the log for details.") + Environment.NewLine + ex.Message;
            }
        }

        private Task StartMapAsync()
        {
            if (!shellInitialized || isShuttingDown || shutdownTask != null)
                throw new InvalidOperationException("The Toolbox shell is not available for map startup.");
            return mapStartupTask ??= StartMapCoreAsync();
        }

        private async Task StartMapCoreAsync()
        {
            viewModel.WorkspaceStatus = CatalogManager.Catalog.GetString("Starting the map workspace...");
            routeBrowser?.Stop();
            viewModel.SettingsTool.IsEditable = false;
            viewModel.ResetSettingsCommand.RaiseCanExecuteChanged();
            MapHost = new ToolboxGameHostControl();
            SubscribeMapHost();
            try
            {
                SynchronizeMapDocument();
                await MapHost.StartAsync(currentProfile, userSettings, toolboxSettings, logFileName).ConfigureAwait(true);
                if (isShuttingDown || shutdownTask != null)
                    return;
                viewModel.WorkspaceStatus = null;
                UpdateHostedInputCapture();
            }
            finally
            {
                // Dispatch after the cached startup task has settled so command availability sees its outcome.
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    viewModel.ShowRouteToolCommand.RaiseCanExecuteChanged();
                    viewModel.ToggleRouteToolCommand.RaiseCanExecuteChanged();
                }));
            }
        }

        private void MapHost_HostedMenuReady(object sender, EventArgs e)
        {
            HostedToolboxMenu menu = MapHost.HostedServices?.Menu;
            if (menu != null)
            {
                viewModel.Menu?.Dispose();
                viewModel.Menu = new ToolboxMenuViewModel(menu, Dispatcher);
            }

            viewModel.ToggleRouteToolCommand?.RaiseCanExecuteChanged();
            viewModel.ShowRouteToolCommand?.RaiseCanExecuteChanged();
            if (showRoutesWhenReady && !isShuttingDown && shutdownTask == null)
            {
                showRoutesWhenReady = false;
                ShowToolWindow(RouteToolWindowContentId);
            }
        }

        private void InitializeToolWindowCommands()
        {
            viewModel.ToggleRouteToolCommand = new RelayCommand(_ => ToggleToolWindow(RouteToolWindowContentId), _ => !isShuttingDown && shutdownTask == null);
            viewModel.ShowRouteToolCommand = new RelayCommand(_ => OpenRouteWorkspace(), _ => CanOpenRouteWorkspace());
            viewModel.ExitCommand = new RelayCommand(_ => Close(), _ => !isShuttingDown);
            viewModel.ShowAboutCommand = new RelayCommand(_ => MapHost_AboutRequested(this, EventArgs.Empty), _ => !isShuttingDown);
            viewModel.ToggleLocationToolCommand = new RelayCommand(_ => ToggleToolWindow(LocationToolWindowContentId), _ => !isShuttingDown && shutdownTask == null);
            viewModel.ToggleDebugToolCommand = new RelayCommand(_ => ToggleToolWindow(DebugToolWindowContentId), _ => !isShuttingDown && shutdownTask == null);
            viewModel.ToggleLogToolCommand = new RelayCommand(_ => ToggleToolWindow(LogToolWindowContentId), _ => !isShuttingDown && shutdownTask == null);
            viewModel.ToggleRouteNavigationToolCommand = new RelayCommand(_ => ToggleToolWindow(RouteNavigationToolWindowContentId), _ => !isShuttingDown && shutdownTask == null);
            viewModel.ToggleHelpToolCommand = new RelayCommand(_ => ToggleToolWindow(HelpToolWindowContentId), _ => !isShuttingDown && shutdownTask == null);
            viewModel.ToggleSettingsToolCommand = new RelayCommand(_ => ToggleToolWindow(SettingsToolWindowContentId), _ => !isShuttingDown && shutdownTask == null);
            viewModel.ToggleTrainPathToolCommand = new RelayCommand(_ => ToggleToolWindow(TrainPathToolWindowContentId), _ => !isShuttingDown && shutdownTask == null);
            viewModel.TogglePathChartToolCommand = new RelayCommand(_ => ToggleToolWindow(PathChartToolWindowContentId), _ => !isShuttingDown && shutdownTask == null);
            viewModel.ShowPathChartToolCommand = new RelayCommand(_ => ShowToolWindow(PathChartToolWindowContentId), _ => viewModel.PathChartTool != null);
            viewModel.ResetSettingsCommand = new RelayCommand(_ => ResetToDefaultsAsync(), _ => viewModel.SettingsTool?.IsEditable == true);
        }

        private void InitializeToolWindowDescriptors()
        {
            toolWindowDescriptors = new[]
            {
                new ToolWindowDescriptor(RouteToolWindowContentId, "Routes", () => RouteToolAnchorable, value => viewModel.IsRouteToolVisible = value),
                new ToolWindowDescriptor(LocationToolWindowContentId, "Location", () => LocationToolAnchorable, value => viewModel.IsLocationToolVisible = value,
                    () => viewModel.LocationTool, () => viewModel.LocationTool.Start(), () => viewModel.LocationTool.Stop()),
                new ToolWindowDescriptor(DebugToolWindowContentId, "Debug Information", () => DebugToolAnchorable, value => viewModel.IsDebugToolVisible = value,
                    () => viewModel.DebugTool, () => viewModel.DebugTool.Start(), () => viewModel.DebugTool.Stop()),
                new ToolWindowDescriptor(LogToolWindowContentId, "Logging", () => LogToolAnchorable, value => viewModel.IsLogToolVisible = value,
                    () => viewModel.LogTool, () => viewModel.LogTool.Start(), () => viewModel.LogTool.Stop()),
                new ToolWindowDescriptor(RouteNavigationToolWindowContentId, "Route Navigation", () => RouteNavigationToolAnchorable, value => viewModel.IsRouteNavigationToolVisible = value,
                    () => viewModel.RouteNavigationTool, () => viewModel.RouteNavigationTool.Start(), () => viewModel.RouteNavigationTool.Stop()),
                new ToolWindowDescriptor(HelpToolWindowContentId, "Help", () => HelpToolAnchorable, value => viewModel.IsHelpToolVisible = value,
                    () => viewModel.HelpTool, () => viewModel.HelpTool.Start(), () => viewModel.HelpTool.Stop()),
                new ToolWindowDescriptor(SettingsToolWindowContentId, "Settings", () => SettingsToolAnchorable, value => viewModel.IsSettingsToolVisible = value,
                    () => viewModel.SettingsTool, () => viewModel.SettingsTool.Start(), () => viewModel.SettingsTool.Stop()),
                new ToolWindowDescriptor(TrainPathToolWindowContentId, "Path Editor", () => TrainPathToolAnchorable, value => viewModel.IsTrainPathToolVisible = value,
                    () => viewModel.TrainPathTool, () => viewModel.TrainPathTool.Start(), () => viewModel.TrainPathTool.Stop()),
                new ToolWindowDescriptor(PathChartToolWindowContentId, "Path Chart", () => PathChartToolAnchorable, value => viewModel.IsPathChartToolVisible = value,
                    () => viewModel.PathChartTool, () => viewModel.PathChartTool.Start(), () => viewModel.PathChartTool.Stop()),
            };
        }

        // Stamps each anchorable's default floating (undocked) size from the size its hosted view declares via
        // Views.ToolWindow.DefaultFloatingSize. Runs before the default dock layout is captured so the values
        // flow into both the initial layout and the reset-to-default baseline. Keeping the size on the view
        // means it lives next to that view's d:DesignWidth/Height instead of being hard-coded on the shell.
        private void ApplyDefaultFloatingSizes()
        {
            foreach (ToolWindowDescriptor descriptor in toolWindowDescriptors)
                ApplyDefaultFloatingSize(descriptor.TemplateAnchorable());
        }

        private static void ApplyDefaultFloatingSize(LayoutAnchorable anchorable)
        {
            if (anchorable?.Content is not FrameworkElement view)
                return;

            Size size = Views.ToolWindow.GetDefaultFloatingSize(view);
            if (size.IsEmpty || size.Width <= 0 || size.Height <= 0)
                return;

            anchorable.FloatingWidth = size.Width;
            anchorable.FloatingHeight = size.Height;
            Views.ToolWindow.ApplyDefaultAutoHideSize(anchorable);
        }

        private void HookToolWindowAnchorables()
        {
            foreach (ToolWindowDescriptor descriptor in toolWindowDescriptors)
                HookToolWindowAnchorable(() => EnsureToolWindowAnchorable(descriptor), ToolWindowAnchorable_PropertyChanged);
        }

        private static void HookToolWindowAnchorable(Func<LayoutAnchorable> ensureAnchorable, PropertyChangedEventHandler handler)
        {
            ArgumentNullException.ThrowIfNull(ensureAnchorable);
            ArgumentNullException.ThrowIfNull(handler);

            LayoutAnchorable anchorable = ensureAnchorable();
            if (anchorable is null)
                return;

            anchorable.PropertyChanged -= handler;
            anchorable.PropertyChanged += handler;
        }

        private void RaiseToolWindowCommandCanExecuteChanged()
        {
            viewModel.ToggleRouteToolCommand?.RaiseCanExecuteChanged();
            viewModel.ShowRouteToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleLocationToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleDebugToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleLogToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleRouteNavigationToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleHelpToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleSettingsToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleTrainPathToolCommand?.RaiseCanExecuteChanged();
            viewModel.TogglePathChartToolCommand?.RaiseCanExecuteChanged();
            viewModel.ShowPathChartToolCommand?.RaiseCanExecuteChanged();
            viewModel.ResetSettingsCommand?.RaiseCanExecuteChanged();
        }

        private void UpdateAllToolWindowLifecycles()
        {
            foreach (ToolWindowDescriptor descriptor in toolWindowDescriptors)
                UpdateToolWindowLifecycle(descriptor);
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            deactivationSequence++;
            UpdateHostedInputCapture();
        }

        private async void MainWindow_Deactivated(object sender, EventArgs e)
        {
            try
            {
                await ApplyDeactivationCaptureAsync(++deactivationSequence).ConfigureAwait(true);
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceWarning($"Failed to update Toolbox input capture after deactivation: {ex.Message}");
            }
        }

        private void MapHost_GotFocus(object sender, RoutedEventArgs e)
        {
            ActivateMapInput();
        }

        private void MapHost_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            ActivateMapInput();
        }

        private void MapHost_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ActivateMapInput();
        }

        private void MapHost_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ActivateMapInput();
        }

        private void MapHost_HostedWindowPointerDown(object sender, EventArgs e)
        {
            // Child HWND click from the hosted MonoGame surface: release capture immediately without
            // requiring an extra click on WPF chrome/titlebar.
            ActivateMapInput();
        }

        private void DockingManager_ActiveContentChanged(object sender, EventArgs e)
        {
            UpdateHostedInputCapture();
        }

        private async void MapHost_SaveTrainPathRequested(object sender, EventArgs e)
        {
            try
            {
                TrainPathSaveState saveState = await MapHost.GetTrainPathSaveStateAsync().ConfigureAwait(true);
                if (saveState == null)
                    return;

                PathModelHeader pathDetails = TrainPathSaveRequest.PreparePathDetails(saveState.PathDetails, saveState.SourcePathId);

                bool overwriteConfirmed = false;
                TrainPathSaveRequest saveRequest = new TrainPathSaveRequest(pathDetails, saveState.SourcePathId, overwriteConfirmed);
                if (saveRequest.IsSaveAs && await MapHost.TrainPathIdExistsAsync(pathDetails.Id).ConfigureAwait(true))
                {
                    MessageBoxResult overwriteResult = MessageBox.Show(this,
                        CatalogManager.Catalog.GetString($"A path with ID '{pathDetails.Id}' already exists. Replace that path? The active path will remain unchanged unless the save succeeds."),
                        CatalogManager.Catalog.GetString("Confirm Save As Overwrite"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (overwriteResult != MessageBoxResult.Yes)
                        return;

                    overwriteConfirmed = true;
                    saveRequest = new TrainPathSaveRequest(pathDetails, saveState.SourcePathId, overwriteConfirmed);
                }

                await MapHost.SubmitSavePathAsync(saveRequest).ConfigureAwait(true);
            }
            catch (OperationCanceledException ex)
            {
                Trace.TraceInformation($"Train path save was canceled: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceError($"Failed to save train path: {ex}");
            }
            catch (IOException ex)
            {
                Trace.TraceError($"Failed to save train path: {ex}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.TraceError($"Failed to save train path: {ex}");
            }
        }

        private async void MapHost_ScreenshotRequested(object sender, EventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = ".png",
                FileName = $"{RuntimeInfo.ApplicationName} {DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss", System.Globalization.CultureInfo.CurrentCulture)}",
                Filter = "Image files (*.png)|*.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                OverwritePrompt = true,
            };

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                await MapHost.SaveScreenshotAsync(dialog.FileName).ConfigureAwait(true);
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceError($"Failed to save screenshot: {ex}");
            }
            catch (IOException ex)
            {
                Trace.TraceError($"Failed to save screenshot: {ex}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.TraceError($"Failed to save screenshot: {ex}");
            }
        }

        private void MapHost_AboutRequested(object sender, EventArgs e)
        {
            AboutDialog dialog = new AboutDialog
            {
                Owner = this,
            };

            dialog.ShowDialog();
        }

        private void MapHost_MapContextMenuRequested(object sender, MapContextMenuRequestedEventArgs e)
        {
            if (e is null || e.Items.IsDefaultOrEmpty)
                return;

            System.Windows.Point offset = DeviceToLogicalUnits(e.X, e.Y);
            System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu
            {
                PlacementTarget = MapHost,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                HorizontalOffset = offset.X,
                VerticalOffset = offset.Y,
            };

            foreach (MapContextMenuItem menuItem in e.Items)
            {
                if (menuItem.IsSeparator)
                {
                    contextMenu.Items.Add(new System.Windows.Controls.Separator());
                    continue;
                }

                MapContextMenuItem current = menuItem;
                System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem
                {
                    Header = GetMapContextMenuCaption(current),
                };
                item.Click += (_, _) => MapHost.ExecuteMapContextMenuAction(e, current);
                contextMenu.Items.Add(item);
            }

            // Keyboard commands are routed to the hosted map surface, so focus must return to it.
            contextMenu.Closed += (_, _) => ActivateMapInput();
            contextMenu.IsOpen = true;
        }

        internal static string GetMapContextMenuCaption(MapContextMenuItem item)
        {
            string caption = item.Action switch
            {
                MapContextMenuAction.MoveNode => CatalogManager.Catalog.GetString("Move Node"),
                MapContextMenuAction.CancelPlacement => CatalogManager.Catalog.GetString("Cancel Placement"),
                MapContextMenuAction.AddViaPoint => CatalogManager.Catalog.GetString("Insert Via Point Here"),
                MapContextMenuAction.RemoveViaPoint => CatalogManager.Catalog.GetString("Remove Via Point"),
                MapContextMenuAction.ClearWaitPoint => CatalogManager.Catalog.GetString("Clear Wait Point"),
                MapContextMenuAction.SetReversalPoint => CatalogManager.Catalog.GetString("Set Reversal Point"),
                MapContextMenuAction.ClearReversalPoint => CatalogManager.Catalog.GetString("Clear Reversal Point"),
                MapContextMenuAction.StartPassingBranch => CatalogManager.Catalog.GetString("Start Passing Branch"),
                MapContextMenuAction.RejoinPassingBranch => CatalogManager.Catalog.GetString("Rejoin Passing Branch"),
                MapContextMenuAction.RejoinPassingBranchWithVia => CatalogManager.Catalog.GetString("Rejoin Passing Branch With Via"),
                MapContextMenuAction.PreviewPassingBranchViaHere => CatalogManager.Catalog.GetString("Preview Passing Via Here"),
                MapContextMenuAction.ConfirmPassingBranchVia => CatalogManager.Catalog.GetString("Confirm Passing Via"),
                MapContextMenuAction.CancelPassingBranch => CatalogManager.Catalog.GetString("Cancel Passing Branch"),
                MapContextMenuAction.RemovePassingBranch => CatalogManager.Catalog.GetString("Remove Passing Branch"),
                MapContextMenuAction.RepairNode => CatalogManager.Catalog.GetString("Repair Node"),
                MapContextMenuAction.RemoveRestOfPath => CatalogManager.Catalog.GetString("Remove Rest of Path"),
                MapContextMenuAction.SelectRouteCandidate => CatalogManager.Catalog.GetString("Use Route"),
                MapContextMenuAction.RouteThroughJunctionExit => CatalogManager.Catalog.GetString("Route Through Exit"),
                MapContextMenuAction.ContinuePath => CatalogManager.Catalog.GetString("Continue Path"),
                MapContextMenuAction.AddRoutePointHere => CatalogManager.Catalog.GetString("Add Route Point Here"),
                MapContextMenuAction.FinishPathHere => CatalogManager.Catalog.GetString("Finish Path Here"),
                MapContextMenuAction.FinishPath => CatalogManager.Catalog.GetString("Finish Path"),
                MapContextMenuAction.ReResolvePath => CatalogManager.Catalog.GetString("Re-resolve Path"),
                MapContextMenuAction.StartNewPath => CatalogManager.Catalog.GetString("Start New Path"),
                MapContextMenuAction.StartNewPathHere => CatalogManager.Catalog.GetString("Start New Path Here"),
                MapContextMenuAction.SetStartHere => CatalogManager.Catalog.GetString("Set Start Here"),
                MapContextMenuAction.SetEndHere => CatalogManager.Catalog.GetString("Set End Here"),
                MapContextMenuAction.SavePath => CatalogManager.Catalog.GetString("Save Path"),
                MapContextMenuAction.Undo => CatalogManager.Catalog.GetString("Undo"),
                MapContextMenuAction.Redo => CatalogManager.Catalog.GetString("Redo"),
                _ => item.Action.ToString(),
            };

            return string.IsNullOrEmpty(item.Detail) ? caption : $"{caption}: {item.Detail}";
        }

        // Converts hosted map surface client pixels into the device-independent units WPF placement expects.
        private System.Windows.Point DeviceToLogicalUnits(int x, int y)
        {
            System.Windows.Media.Matrix transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
                ?? System.Windows.Media.Matrix.Identity;
            return transform.Transform(new System.Windows.Point(x, y));
        }

        private void MapHost_ExitRequested(object sender, EventArgs e)
        {
            Close();
        }

        private void MapHost_HostedToolWindowsReady(object sender, EventArgs e)
        {
            HostedToolboxServices services = MapHost.HostedServices;
            if (services is null || services.DebugToolWindow is null || services.LocationToolWindow is null || services.LogToolWindow is null
                || services.HelpToolWindow is null
                || services.SettingsToolWindow is null || services.TrainPathToolWindow is null || services.StatusBarToolWindow is null
                || services.RouteNavigationToolWindow is null)
                return;

            refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher);

            viewModel.LocationTool = new LocationToolWindowViewModel(services.LocationToolWindow, refreshScheduler);
            viewModel.DebugTool = new DebugToolWindowViewModel(services.DebugToolWindow, refreshScheduler);
            viewModel.LogTool = new LogToolWindowViewModel(services.LogToolWindow, refreshScheduler);
            viewModel.RouteNavigationTool = new RouteNavigationToolWindowViewModel(services.RouteNavigationToolWindow, refreshScheduler);
            viewModel.HelpTool = new HelpToolWindowViewModel(services.HelpToolWindow, refreshScheduler);
            BindSettings(services.SettingsToolWindow);
            viewModel.TrainPathTool = new TrainPathToolWindowViewModel(services.TrainPathToolWindow, refreshScheduler, MapHost.ToolboxSettings?.PathEditorSelectedTabIndex ?? TrainPathToolWindowViewModel.PathsTabIndex)
            {
                ShowPathChartCommand = viewModel.ShowPathChartToolCommand,
            };
            if (services.PathChartToolWindow != null)
                viewModel.PathChartTool = new PathChartToolWindowViewModel(services.PathChartToolWindow, refreshScheduler);

            // The status bar is always visible (not a dockable pane), so it starts pulling snapshots as soon
            // as the hosted bridge is available.
            viewModel.StatusBar = new StatusBarViewModel(services.StatusBarToolWindow, refreshScheduler);
            viewModel.StatusBar.Start();

            RaiseToolWindowCommandCanExecuteChanged();
            UpdateAllToolWindowLifecycles();
            UpdateHostedInputCapture();
        }

        private void ToggleToolWindow(string contentId)
        {
            ToolWindowDescriptor descriptor = FindToolWindowDescriptor(contentId);
            if (descriptor is null)
                return;

            ToggleToolWindow(() => EnsureToolWindowAnchorable(descriptor), () => UpdateToolWindowLifecycle(descriptor));
        }

        private void ToggleToolWindow(Func<LayoutAnchorable> ensureAnchorable, Action updateLifecycle)
        {
            ArgumentNullException.ThrowIfNull(ensureAnchorable);
            ArgumentNullException.ThrowIfNull(updateLifecycle);

            LayoutAnchorable anchorable = ensureAnchorable();
            if (anchorable is null)
                return;

            if (anchorable.IsDetached)
            {
                DockingManager.ReattachAnchorable(anchorable);
                anchorable.Hide();
            }
            else if (!anchorable.IsVisible)
            {
                anchorable.Show();
                if (string.Equals(anchorable.ContentId, PathChartToolWindowContentId, StringComparison.Ordinal))
                    GrowPathChartPane(anchorable);
            }
            else
                anchorable.Hide();

            updateLifecycle();
            UpdateHostedInputCapture();
        }

        private void ShowToolWindow(string contentId)
        {
            ToolWindowDescriptor descriptor = FindToolWindowDescriptor(contentId);
            if (descriptor is null)
                return;

            LayoutAnchorable anchorable = EnsureToolWindowAnchorable(descriptor);
            if (anchorable is null)
                return;

            if (anchorable.IsDetached)
            {
                if (anchorable.Content is DependencyObject content)
                    Window.GetWindow(content)?.Activate();
                UpdateToolWindowLifecycle(descriptor);
                return;
            }

            if (!anchorable.IsVisible)
            {
                anchorable.Show();
                if (string.Equals(contentId, PathChartToolWindowContentId, StringComparison.Ordinal))
                    GrowPathChartPane(anchorable);
            }

            anchorable.IsActive = true;
            UpdateToolWindowLifecycle(descriptor);
            UpdateHostedInputCapture();
        }

        private void GrowPathChartPane(LayoutAnchorable anchorable)
        {
            if (anchorable.IsFloating || anchorable.IsAutoHidden || anchorable.Parent is not LayoutAnchorablePane pane)
                return;

            const double preferredHeight = 400;
            DockingManager.UpdateLayout();
            double currentHeight = pane.DockHeight.IsAbsolute ? pane.DockHeight.Value
                : (anchorable.Content as FrameworkElement)?.ActualHeight ?? 0;
            if (currentHeight < preferredHeight)
                pane.DockHeight = new GridLength(preferredHeight);
        }

        private void ToolWindowAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e?.PropertyName is not (nameof(LayoutAnchorable.IsVisible) or nameof(LayoutAnchorable.IsDetached))
                || sender is not LayoutAnchorable anchorable)
                return;

            ToolWindowDescriptor descriptor = FindToolWindowDescriptor(anchorable.ContentId);
            if (descriptor != null)
                UpdateToolWindowLifecycle(descriptor);
        }

        private ToolWindowDescriptor FindToolWindowDescriptor(string contentId)
        {
            return toolWindowDescriptors.FirstOrDefault(descriptor => string.Equals(descriptor.ContentId, contentId, StringComparison.Ordinal));
        }

        private LayoutAnchorable FindToolWindowAnchorable(string contentId)
        {
            return DockingManager.Layout?.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(anchorable => string.Equals(anchorable.ContentId, contentId, StringComparison.Ordinal));
        }

        private LayoutAnchorable EnsureToolWindowAnchorable(ToolWindowDescriptor descriptor)
        {
            LayoutAnchorable existing = FindToolWindowAnchorable(descriptor.ContentId);
            if (existing is not null)
            {
                existing.Title = CatalogManager.Catalog.GetString(descriptor.Title);
                Views.ToolWindow.ApplyDefaultAutoHideSize(existing);
                return existing;
            }

            LayoutAnchorable template = descriptor.TemplateAnchorable();
            if (template is null || DockingManager.Layout?.RootPanel is null)
                return null;

            LayoutAnchorablePane pane;
            if (string.Equals(descriptor.ContentId, PathChartToolWindowContentId, StringComparison.Ordinal))
            {
                pane = FindToolWindowAnchorable(LogToolWindowContentId)?.Parent as LayoutAnchorablePane;
                if (pane == null)
                {
                    LayoutDocumentPane documentPane = DockingManager.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
                    LayoutPanel mapPanel = null;
                    for (ILayoutContainer parent = documentPane?.Parent; parent != null; parent = parent.Parent)
                    {
                        if (parent is LayoutPanel candidate && candidate.Orientation == System.Windows.Controls.Orientation.Vertical)
                        {
                            mapPanel = candidate;
                            break;
                        }
                    }
                    if (mapPanel == null)
                    {
                        Trace.TraceWarning("Cannot restore Path Chart: the dock layout has no lower map panel.");
                        return null;
                    }
                    pane = new LayoutAnchorablePane() { DockHeight = new GridLength(160) };
                    mapPanel.Children.Add(pane);
                }
            }
            else
                pane = DockingManager.Layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault();
            if (pane is null)
                return null;

            if (!pane.Children.Contains(template))
                pane.Children.Insert(0, template);

            template.ContentId = descriptor.ContentId;
            template.CanClose = false;
            template.CanHide = true;
            template.Title = CatalogManager.Catalog.GetString(descriptor.Title);
            ApplyDefaultFloatingSize(template);

            return template;
        }

        private void UpdateToolWindowLifecycle(ToolWindowDescriptor descriptor)
        {
            LayoutAnchorable anchorable = FindToolWindowAnchorable(descriptor.ContentId);
            // AvalonDock parks detached panes in Hidden while their content remains in a live window.
            bool isVisible = anchorable != null && (anchorable.IsVisible || anchorable.IsDetached);
            descriptor.UpdateLifecycle(isVisible);
        }

        private void ActivateMapInput()
        {
            UpdateHostedInputCapture(forceMapActive: true);
        }

        private void UpdateHostedInputCapture(bool forceMapActive = false)
        {
            bool mapIsActive = forceMapActive || IsMapInteractionActive();
            // While a tool window holds keyboard focus, hovering the map must not silently take input away from
            // it; otherwise typing in a tool window would be interrupted by an incidental pointer position.
            MapHost?.SetPointerActivationSuppressed(!mapIsActive && IsKeyboardFocusInToolWindow());
            MapHost?.SetInputCaptured(!mapIsActive);
        }

        private bool IsKeyboardFocusInToolWindow()
        {
            return DockingManager.IsKeyboardFocusWithin && MapHost?.IsKeyboardFocusWithin != true;
        }

        private bool IsMapInteractionActive()
        {
            // Base interactivity purely on whether the pointer is over the hosted map surface (or it holds
            // keyboard focus). Do not gate on the main window IsActive: when a tool window is floated into its
            // own owned top-level window, that floating window holds activation and the main window reports
            // IsActive == false, which previously captured input and froze map hover updates. A genuine switch
            // to another application is still handled by the deactivation path (ApplyDeactivationCaptureAsync).
            return MapHost?.IsMouseOver == true || MapHost?.IsKeyboardFocusWithin == true;
        }

        private async Task ApplyDeactivationCaptureAsync(int sequence)
        {
            await Task.Delay(80).ConfigureAwait(true);

            // Only capture input when the application has genuinely lost activation to another app. Floating a
            // tool window creates an owned top-level window whose activation deactivates this main window; in
            // that case one of our own windows is still active, so we must keep map input flowing (otherwise
            // hovering the map no longer updates tool-window content such as Track Node Information).
            if (sequence == deactivationSequence && !IsAnyApplicationWindowActive())
                MapHost?.SetInputCaptured(true);
        }

        private static bool IsAnyApplicationWindowActive()
        {
            if (Application.Current is null)
                return false;

            foreach (Window window in Application.Current.Windows)
            {
                if (window.IsActive)
                    return true;
            }

            return false;
        }

        private async Task LoadDockLayoutAsync()
        {
            // Capture the XAML-defined layout before any persisted layout is applied so it can be restored by
            // the settings "Reset to Defaults" action.
            CaptureDefaultDockLayout();

            try
            {
                if (string.IsNullOrWhiteSpace(toolboxSettings.DockLayoutJson))
                    return;

                DeserializeDockLayout(toolboxSettings.DockLayoutJson);

                if (IsMapDocumentFloating())
                {
                    Trace.TraceWarning("Skipping persisted dock layout because it restores MapViewDocument as a floating window.");
                    toolboxSettings.DockLayoutJson = null;
                    toolboxSettings = await currentProfile.UpdateSettingsModel(toolboxSettings, CancellationToken.None).ConfigureAwait(true);
                    ResetDockLayoutToDefault();
                    return;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is JsonException)
            {
                Trace.TraceError($"Failed to load dock layout: {ex.Message}");
            }
        }

        // Restores the shell window's saved placement (position, size, and maximized state). AvalonDock only
        // persists the docked layout inside the window, so the window's own bounds are restored separately here,
        // after the settings model has loaded and the native handle exists (this runs from the Loaded handler).
        private void RestoreWindowPlacement()
        {
            if (toolboxSettings is null)
                return;

            WindowBoundsManager.RestoreWindowBounds(this, toolboxSettings);
        }

        private void CaptureDefaultDockLayout()
        {
            if (defaultDockLayoutJson is not null)
                return;

            try
            {
                defaultDockLayoutJson = SerializeDockLayout();
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
            {
                Trace.TraceWarning($"Failed to capture default dock layout: {ex.Message}");
            }
        }

        private string SerializeDockLayout()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                JsonLayoutSerializer serializer = new JsonLayoutSerializer(DockingManager);
                serializer.Serialize(stream);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private void DeserializeDockLayout(string layoutJson)
        {
            byte[] layoutBytes = Encoding.UTF8.GetBytes(layoutJson);
            using (MemoryStream stream = new MemoryStream(layoutBytes, false))
            {
                // Retire the old flyout before layout replacement detaches its manager and title template.
                if (DockingManager.AutoHideWindow?.Model is LayoutAnchorable { IsAutoHidden: true } openFlyout)
                    openFlyout.HideAnchorable(false);

                JsonLayoutSerializer serializer = new JsonLayoutSerializer(DockingManager);
                serializer.Deserialize(stream);
            }
        }

        private async Task SaveDockLayoutAsync()
        {
            try
            {
                if (toolboxSettings is null)
                    return;

                if (MapHost?.HostedServices == null)
                {
                    // Persist shell changes without replacing saved map selections or viewport state.
                    ProfileToolboxSettingsModel savedSettings = await currentProfile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(true);
                    shellSettingsApplier?.CopyChangedToolboxPreferences(savedSettings);
                    WindowBoundsManager.SaveBoundsToSettings(this, savedSettings);
                    if (initialDockLayoutJson != null)
                    {
                        string currentLayout = SerializeDockLayout();
                        if (!string.Equals(currentLayout, initialDockLayoutJson, StringComparison.Ordinal))
                            savedSettings.DockLayoutJson = currentLayout;
                    }
                    await currentProfile.UpdateSettingsModel(savedSettings, CancellationToken.None).ConfigureAwait(true);
                    if (shellSettingsApplier != null)
                        await shellSettingsApplier.SaveChangedUserPreferencesAsync().ConfigureAwait(true);
                    return;
                }

                string dockLayoutJson = SerializeDockLayout();
                toolboxSettings.DockLayoutJson = dockLayoutJson;
                WindowBoundsManager.SaveBoundsToSettings(this, toolboxSettings);
                int selectedTabIndex = viewModel.TrainPathTool?.SelectedTabIndex
                    ?? MapHost.ToolboxSettings?.PathEditorSelectedTabIndex ?? TrainPathToolWindowViewModel.PathsTabIndex;
                await MapHost.SaveHostedSettingsAsync(dockLayoutJson, toolboxSettings.WindowPlacements, selectedTabIndex).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not ObjectDisposedException)
            {
                Trace.TraceWarning($"Failed to save dock layout: {ex.Message}");
            }
        }

        // Command handler for the settings "Reset to Defaults" button. Confirms with the user, then resets the
        // appearance preferences (colors, item visibility, font outline, real track width, restore-last-view)
        // and the dock layout to their defaults, applying live and persisting immediately. Logging is left
        // unchanged.
        private async void ResetToDefaultsAsync()
        {
            try
            {
                SettingsToolWindowViewModel settings = viewModel.SettingsTool;
                if (settings?.IsEditable != true)
                    return;

                MessageBoxResult result = MessageBox.Show(
                    this,
                    "Reset the window layout, colors, item visibility, and appearance options to their defaults?"
                        + Environment.NewLine + Environment.NewLine + "This cannot be undone.",
                    "Reset to Defaults",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes || !ReferenceEquals(settings, viewModel.SettingsTool) || !settings.IsEditable)
                    return;

                await settings.ResetToDefaultsAsync().ConfigureAwait(true);

                viewModel.TrainPathTool?.SelectedTabIndex = TrainPathToolWindowViewModel.PathsTabIndex;

                ResetDockLayoutToDefault();
                if (MapHost?.HostedServices != null)
                    await SaveDockLayoutAsync().ConfigureAwait(true);
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceError($"Failed to reset settings to defaults: {ex}");
            }
        }

        // Restores the XAML-defined default dock layout captured at startup, re-hooking the tool-window
        // anchorables and re-syncing their visibility flags/lifecycles.
        private void ResetDockLayoutToDefault()
        {
            if (string.IsNullOrWhiteSpace(defaultDockLayoutJson))
                return;

            try
            {
                UnhookToolWindowAnchorables();
                DeserializeDockLayout(defaultDockLayoutJson);
                SynchronizeMapDocument();
                HookToolWindowAnchorables();
                SelectRouteToolByDefault();
                UpdateAllToolWindowLifecycles();
                UpdateHostedInputCapture();
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is JsonException)
            {
                Trace.TraceError($"Failed to reset dock layout: {ex.Message}");
            }
        }

        private void SelectRouteToolByDefault()
        {
            LayoutAnchorable routeAnchorable = FindToolWindowAnchorable(RouteToolWindowContentId);
            if (routeAnchorable is null)
                return;

            routeAnchorable.IsSelected = true;
            routeAnchorable.IsActive = true;
        }

        private bool IsMapDocumentFloating()

        {
            LayoutDocument mapDocument = DockingManager.Layout?.Descendents()
                .OfType<LayoutDocument>()
                .FirstOrDefault(document => string.Equals(document.ContentId, "MapViewDocument", StringComparison.Ordinal));

            return mapDocument?.IsFloating == true;
        }

        private void SynchronizeMapDocument()
        {
            LayoutDocument mapDocument = DockingManager.Layout?.Descendents().OfType<LayoutDocument>()
                .FirstOrDefault(document => string.Equals(document.ContentId, "MapViewDocument", StringComparison.Ordinal));
            if (MapHost == null)
            {
                if (mapDocument?.Parent is LayoutDocumentPane parent)
                    parent.Children.Remove(mapDocument);
                return;
            }

            if (mapDocument == null)
            {
                LayoutDocumentPane pane = DockingManager.Layout?.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
                if (pane == null)
                    throw new InvalidOperationException("The Toolbox layout has no document pane for the map.");
                mapDocument = new LayoutDocument
                {
                    ContentId = "MapViewDocument",
                    CanFloat = false,
                    CanClose = false,
                };
                pane.Children.Add(mapDocument);
            }
            mapDocument.Title = CatalogManager.Catalog.GetString("Map View");
            mapDocument.Content = MapHost;
            mapDocument.IsSelected = true;
        }

        private Task ShutdownAsync()
        {
            // A previously failed stop can acquire safe owner-termination evidence later.
            // Keep its diagnostic task, but do not strand a now safely released owner.
            if (shutdownTask?.IsCompleted == true)
                ReleaseShellResources();
            return shutdownTask ??= ShutdownCoreAsync();
        }

        private async Task ShutdownCoreAsync()
        {
            if (shellInitializationTask != null)
                await shellInitializationTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
            if (mapStartupTask != null)
                await mapStartupTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
            // Save while admission and both owners are alive. Keep bridges subscribed through drain so
            // pending interactions can settle before releasing any shell/native resources.
            await SaveDockLayoutAsync().ConfigureAwait(true);
            try
            {
                if (MapHost != null)
                    await MapHost.StopAsync().ConfigureAwait(true);
            }
            finally
            {
                ReleaseShellResources();
            }
        }

        private void ReleaseShellResources()
        {
            if (shutdownCompleted || (MapHost != null && !MapHost.IsStoppedSafely))
                return;

            UnsubscribeToolWindowShortcuts();

            Loaded -= MainWindow_Loaded;
            Activated -= MainWindow_Activated;
            Deactivated -= MainWindow_Deactivated;
            if (MapHost != null)
            {
                MapHost.GotFocus -= MapHost_GotFocus;
                MapHost.GotKeyboardFocus -= MapHost_GotKeyboardFocus;
                MapHost.MouseEnter -= MapHost_MouseEnter;
                MapHost.PreviewMouseDown -= MapHost_PreviewMouseDown;
                MapHost.HostedWindowPointerDown -= MapHost_HostedWindowPointerDown;
                MapHost.HostedMenuReady -= MapHost_HostedMenuReady;
                MapHost.HostedToolWindowsReady -= MapHost_HostedToolWindowsReady;
                MapHost.SaveTrainPathRequested -= MapHost_SaveTrainPathRequested;
                MapHost.UnsavedPathConfirmationRequested -= MapHost_UnsavedPathConfirmationRequested;
                MapHost.ScreenshotRequested -= MapHost_ScreenshotRequested;
                MapHost.AboutRequested -= MapHost_AboutRequested;
                MapHost.ExitRequested -= MapHost_ExitRequested;
                MapHost.LanguageChanged -= MapHost_LanguageChanged;
                MapHost.MapContextMenuRequested -= MapHost_MapContextMenuRequested;
            }
            UnhookToolWindowAnchorables();
            DockingManager.ActiveContentChanged -= DockingManager_ActiveContentChanged;

            DisposeToolWindowViewModels();
            MapHost?.Dispose();
            shutdownCompleted = true;
        }

        private static void UnhookToolWindowAnchorable(LayoutAnchorable anchorable, PropertyChangedEventHandler handler)
        {
            if (anchorable is null || handler is null)
                return;

            anchorable.PropertyChanged -= handler;
        }

        private void UnhookToolWindowAnchorables()
        {
            foreach (ToolWindowDescriptor descriptor in toolWindowDescriptors)
                UnhookToolWindowAnchorable(FindToolWindowAnchorable(descriptor.ContentId), ToolWindowAnchorable_PropertyChanged);
        }

        private void DisposeToolWindowViewModels()
        {
            routeBrowser?.Stop();
            viewModel.Menu?.Dispose();
            viewModel.LocationTool?.Dispose();
            viewModel.DebugTool?.Dispose();
            viewModel.LogTool?.Dispose();
            viewModel.RouteNavigationTool?.Dispose();
            viewModel.HelpTool?.Dispose();
            viewModel.SettingsTool?.Dispose();
            viewModel.TrainPathTool?.Dispose();
            viewModel.PathChartTool?.Dispose();
            viewModel.StatusBar?.Dispose();
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            if (shutdownCompleted)
            {
                base.OnClosing(e);
                return;
            }

            e?.Cancel = true;
            if (isShuttingDown)
                return;

            // Set the guard before the first await/modal prompt: nested closes share this attempt.
            isShuttingDown = true;
            try
            {
                if (!await ConfirmExitAsync().ConfigureAwait(true))
                    return;

                await ShutdownAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not AccessViolationException)
            {
                Trace.TraceError($"Failed to shut down Toolbox window cleanly: {ex}");
                MessageBox.Show(this,
                    shutdownCompleted
                        ? CatalogManager.Catalog.GetString("Toolbox encountered an error while shutting down. Resources were released safely and the window will close. Changes may not have been saved. See the log for details.")
                        : CatalogManager.Catalog.GetString("Toolbox could not shut down safely. The window will remain open. See the log for details."),
                    CatalogManager.Catalog.GetString("Shutdown Failed"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isShuttingDown = false;
            }
            if (shutdownCompleted)
                Close();
        }

        private async Task<bool> ConfirmExitAsync()
        {
            bool hasUnsavedPathChanges = false;

            try
            {
                hasUnsavedPathChanges = MapHost != null && await MapHost.HasUnsavedPathChangesAsync().ConfigureAwait(true);
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceWarning($"Failed to query unsaved path changes during exit confirmation: {ex.Message}");
            }

            QuitDialog dialog = new QuitDialog(hasUnsavedPathChanges)
            {
                Owner = this,
            };

            return dialog.ShowDialog() == true;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        // Releases the shell-owned refresh scheduler. WPF never calls Dispose on a Window, so this runs from
        // OnClosed once the asynchronous shutdown sequence has completed.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (!shutdownCompleted)
                    throw new InvalidOperationException("Close the Toolbox window through its awaited shutdown before disposing it.");

                UnsubscribeToolWindowShortcuts();
                MapHost?.Dispose();
                refreshScheduler?.Dispose();
            }

            disposed = true;
        }

        private sealed class ToolWindowDescriptor
        {
            private readonly Action<bool> setIsVisible;
            private readonly Func<object> viewModelAccessor;
            private readonly Action start;
            private readonly Action stop;

            public ToolWindowDescriptor(string contentId, string title, Func<LayoutAnchorable> templateAnchorable,
                Action<bool> setIsVisible, Func<object> viewModelAccessor = null, Action start = null, Action stop = null)
            {
                ContentId = contentId ?? throw new ArgumentNullException(nameof(contentId));
                Title = title ?? throw new ArgumentNullException(nameof(title));
                TemplateAnchorable = templateAnchorable ?? throw new ArgumentNullException(nameof(templateAnchorable));
                this.setIsVisible = setIsVisible ?? throw new ArgumentNullException(nameof(setIsVisible));
                this.viewModelAccessor = viewModelAccessor;
                this.start = start;
                this.stop = stop;
            }

            public string ContentId { get; }

            public string Title { get; }

            public Func<LayoutAnchorable> TemplateAnchorable { get; }

            public void UpdateLifecycle(bool isVisible)
            {
                setIsVisible(isVisible);

                if (viewModelAccessor?.Invoke() is null)
                    return;

                if (isVisible)
                    start?.Invoke();
                else
                    stop?.Invoke();
            }
        }
    }
}
