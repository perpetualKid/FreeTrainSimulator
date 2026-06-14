using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;

using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.Wpf.Dialogs;
using FreeTrainSimulator.Toolbox.Wpf.ViewModels;

namespace FreeTrainSimulator.Toolbox.Wpf
{
    public partial class MainWindow : Window
    {
        private const string LocationToolWindowContentId = "LocationToolWindow";
        private const string DebugToolWindowContentId = "DebugToolWindow";
        private const string LogToolWindowContentId = "LogToolWindow";
        private const string TrackItemInfoToolWindowContentId = "TrackItemInfoToolWindow";
        private const string TrackNodeInfoToolWindowContentId = "TrackNodeInfoToolWindow";
        private const string HelpToolWindowContentId = "HelpToolWindow";
        private const string SettingsToolWindowContentId = "SettingsToolWindow";
        private const string TrainPathToolWindowContentId = "TrainPathToolWindow";

        private readonly MainWindowViewModel viewModel = new MainWindowViewModel();
        private ProfileModel currentProfile;
        private ProfileToolboxSettingsModel toolboxSettings;
        private bool isShuttingDown;
        private int deactivationSequence;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = viewModel;
            InitializeToolWindowCommands();

            Loaded += MainWindow_Loaded;
            Activated += MainWindow_Activated;
            Deactivated += MainWindow_Deactivated;
            MapHost.GotFocus += MapHost_GotFocus;
            MapHost.GotKeyboardFocus += MapHost_GotKeyboardFocus;
            MapHost.MouseEnter += MapHost_MouseEnter;
            MapHost.PreviewMouseDown += MapHost_PreviewMouseDown;
            MapHost.HostedWindowPointerDown += MapHost_HostedWindowPointerDown;
            MapHost.HostedMenuReady += MapHost_HostedMenuReady;
            MapHost.HostedToolWindowsReady += MapHost_HostedToolWindowsReady;
            MapHost.SaveTrainPathRequested += MapHost_SaveTrainPathRequested;
            DockingManager.ActiveContentChanged += DockingManager_ActiveContentChanged;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDockLayoutAsync().ConfigureAwait(true);

            HookToolWindowAnchorable(EnsureLocationToolWindowAnchorable, LocationToolAnchorable_PropertyChanged);
            HookToolWindowAnchorable(EnsureDebugToolWindowAnchorable, DebugToolAnchorable_PropertyChanged);
            HookToolWindowAnchorable(EnsureLogToolWindowAnchorable, LogToolAnchorable_PropertyChanged);
            HookToolWindowAnchorable(EnsureTrackItemInfoToolWindowAnchorable, TrackItemInfoToolAnchorable_PropertyChanged);
            HookToolWindowAnchorable(EnsureTrackNodeInfoToolWindowAnchorable, TrackNodeInfoToolAnchorable_PropertyChanged);
            HookToolWindowAnchorable(EnsureHelpToolWindowAnchorable, HelpToolAnchorable_PropertyChanged);
            HookToolWindowAnchorable(EnsureSettingsToolWindowAnchorable, SettingsToolAnchorable_PropertyChanged);
            HookToolWindowAnchorable(EnsureTrainPathToolWindowAnchorable, TrainPathToolAnchorable_PropertyChanged);

            UpdateAllToolWindowLifecycles();
            UpdateHostedInputCapture();
        }

        private void MapHost_HostedMenuReady(object sender, EventArgs e)
        {
            if (MapHost.HostedMenu != null)
                viewModel.Menu = new ToolboxMenuViewModel(MapHost.HostedMenu, Dispatcher);
        }

        private void InitializeToolWindowCommands()
        {
            viewModel.ToggleLocationToolCommand = new RelayCommand(_ => ToggleLocationToolWindow(), _ => viewModel.LocationTool != null);
            viewModel.ToggleDebugToolCommand = new RelayCommand(_ => ToggleDebugToolWindow(), _ => viewModel.DebugTool != null);
            viewModel.ToggleLogToolCommand = new RelayCommand(_ => ToggleLogToolWindow(), _ => viewModel.LogTool != null);
            viewModel.ToggleTrackItemInfoToolCommand = new RelayCommand(_ => ToggleTrackItemInfoToolWindow(), _ => viewModel.TrackItemInfoTool != null);
            viewModel.ToggleTrackNodeInfoToolCommand = new RelayCommand(_ => ToggleTrackNodeInfoToolWindow(), _ => viewModel.TrackNodeInfoTool != null);
            viewModel.ToggleHelpToolCommand = new RelayCommand(_ => ToggleHelpToolWindow(), _ => viewModel.HelpTool != null);
            viewModel.ToggleSettingsToolCommand = new RelayCommand(_ => ToggleSettingsToolWindow(), _ => viewModel.SettingsTool != null);
            viewModel.ToggleTrainPathToolCommand = new RelayCommand(_ => ToggleTrainPathToolWindow(), _ => viewModel.TrainPathTool != null);
        }

        private void HookToolWindowAnchorable(Func<LayoutAnchorable> ensureAnchorable, PropertyChangedEventHandler handler)
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
            viewModel.ToggleLocationToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleDebugToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleLogToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleTrackItemInfoToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleTrackNodeInfoToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleHelpToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleSettingsToolCommand?.RaiseCanExecuteChanged();
            viewModel.ToggleTrainPathToolCommand?.RaiseCanExecuteChanged();
        }

        private void UpdateAllToolWindowLifecycles()
        {
            UpdateLocationToolWindowLifecycle();
            UpdateDebugToolWindowLifecycle();
            UpdateLogToolWindowLifecycle();
            UpdateTrackItemInfoToolWindowLifecycle();
            UpdateTrackNodeInfoToolWindowLifecycle();
            UpdateHelpToolWindowLifecycle();
            UpdateSettingsToolWindowLifecycle();
            UpdateTrainPathToolWindowLifecycle();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            deactivationSequence++;
            UpdateHostedInputCapture();
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            _ = ApplyDeactivationCaptureAsync(++deactivationSequence);
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

        private void MapHost_SaveTrainPathRequested(object sender, EventArgs e)
        {
            TrainPathSaveDialog dialog = new TrainPathSaveDialog
            {
                Owner = this,
            };

            if (dialog.ShowDialog() == true && dialog.PathDetails != null)
                MapHost.SubmitSavePath(dialog.PathDetails);
        }

        private void MapHost_HostedToolWindowsReady(object sender, EventArgs e)
        {
            if (MapHost.HostedDebugToolWindow is null || MapHost.HostedLocationToolWindow is null || MapHost.HostedLogToolWindow is null
                || MapHost.HostedTrackItemInfoToolWindow is null || MapHost.HostedTrackNodeInfoToolWindow is null || MapHost.HostedHelpToolWindow is null
                || MapHost.HostedSettingsToolWindow is null || MapHost.HostedTrainPathToolWindow is null)
                return;

            viewModel.LocationTool = new LocationToolWindowViewModel(MapHost.HostedLocationToolWindow, Dispatcher);
            viewModel.DebugTool = new DebugToolWindowViewModel(MapHost.HostedDebugToolWindow, Dispatcher);
            viewModel.LogTool = new LogToolWindowViewModel(MapHost.HostedLogToolWindow, Dispatcher);
            viewModel.TrackItemInfoTool = new TrackItemInfoToolWindowViewModel(MapHost.HostedTrackItemInfoToolWindow, Dispatcher);
            viewModel.TrackNodeInfoTool = new TrackNodeInfoToolWindowViewModel(MapHost.HostedTrackNodeInfoToolWindow, Dispatcher);
            viewModel.HelpTool = new HelpToolWindowViewModel(MapHost.HostedHelpToolWindow, Dispatcher);
            viewModel.SettingsTool = new SettingsToolWindowViewModel(MapHost.HostedSettingsToolWindow);
            viewModel.TrainPathTool = new TrainPathToolWindowViewModel(MapHost.HostedTrainPathToolWindow, Dispatcher);
            RaiseToolWindowCommandCanExecuteChanged();
            UpdateAllToolWindowLifecycles();
            UpdateHostedInputCapture();
        }

        private void ToggleLocationToolWindow()
        {
            ToggleToolWindow(EnsureLocationToolWindowAnchorable, UpdateLocationToolWindowLifecycle);
        }

        private void ToggleDebugToolWindow()
        {
            ToggleToolWindow(EnsureDebugToolWindowAnchorable, UpdateDebugToolWindowLifecycle);
        }

        private void ToggleLogToolWindow()
        {
            ToggleToolWindow(EnsureLogToolWindowAnchorable, UpdateLogToolWindowLifecycle);
        }

        private void ToggleTrackItemInfoToolWindow()
        {
            ToggleToolWindow(EnsureTrackItemInfoToolWindowAnchorable, UpdateTrackItemInfoToolWindowLifecycle);
        }

        private void ToggleTrackNodeInfoToolWindow()
        {
            ToggleToolWindow(EnsureTrackNodeInfoToolWindowAnchorable, UpdateTrackNodeInfoToolWindowLifecycle);
        }

        private void ToggleHelpToolWindow()
        {
            ToggleToolWindow(EnsureHelpToolWindowAnchorable, UpdateHelpToolWindowLifecycle);
        }

        private void ToggleSettingsToolWindow()
        {
            ToggleToolWindow(EnsureSettingsToolWindowAnchorable, UpdateSettingsToolWindowLifecycle);
        }

        private void ToggleTrainPathToolWindow()
        {
            ToggleToolWindow(EnsureTrainPathToolWindowAnchorable, UpdateTrainPathToolWindowLifecycle);
        }

        private void ToggleToolWindow(Func<LayoutAnchorable> ensureAnchorable, Action updateLifecycle)
        {
            ArgumentNullException.ThrowIfNull(ensureAnchorable);
            ArgumentNullException.ThrowIfNull(updateLifecycle);

            LayoutAnchorable anchorable = ensureAnchorable();
            if (anchorable is null)
                return;

            if (!anchorable.IsVisible)
                anchorable.Show();
            else
                anchorable.Hide();

            updateLifecycle();
            UpdateHostedInputCapture();
        }

        private void LocationToolAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HandleToolWindowAnchorablePropertyChanged(e, UpdateLocationToolWindowLifecycle);
        }

        private void DebugToolAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HandleToolWindowAnchorablePropertyChanged(e, UpdateDebugToolWindowLifecycle);
        }

        private void LogToolAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HandleToolWindowAnchorablePropertyChanged(e, UpdateLogToolWindowLifecycle);
        }

        private void TrackItemInfoToolAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HandleToolWindowAnchorablePropertyChanged(e, UpdateTrackItemInfoToolWindowLifecycle);
        }

        private void TrackNodeInfoToolAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HandleToolWindowAnchorablePropertyChanged(e, UpdateTrackNodeInfoToolWindowLifecycle);
        }

        private void HelpToolAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HandleToolWindowAnchorablePropertyChanged(e, UpdateHelpToolWindowLifecycle);
        }

        private void SettingsToolAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HandleToolWindowAnchorablePropertyChanged(e, UpdateSettingsToolWindowLifecycle);
        }

        private void TrainPathToolAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HandleToolWindowAnchorablePropertyChanged(e, UpdateTrainPathToolWindowLifecycle);
        }

        private static void HandleToolWindowAnchorablePropertyChanged(PropertyChangedEventArgs e, Action updateLifecycle)
        {
            if (e?.PropertyName == nameof(LayoutAnchorable.IsVisible))
                updateLifecycle?.Invoke();
        }

        private void LogTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
                textBox.ScrollToEnd();
        }

        private LayoutAnchorable LocationToolWindowAnchorable => FindToolWindowAnchorable(LocationToolWindowContentId);

        private LayoutAnchorable DebugToolWindowAnchorable => FindToolWindowAnchorable(DebugToolWindowContentId);

        private LayoutAnchorable LogToolWindowAnchorable => FindToolWindowAnchorable(LogToolWindowContentId);

        private LayoutAnchorable TrackItemInfoToolWindowAnchorable => FindToolWindowAnchorable(TrackItemInfoToolWindowContentId);

        private LayoutAnchorable TrackNodeInfoToolWindowAnchorable => FindToolWindowAnchorable(TrackNodeInfoToolWindowContentId);

        private LayoutAnchorable HelpToolWindowAnchorable => FindToolWindowAnchorable(HelpToolWindowContentId);

        private LayoutAnchorable SettingsToolWindowAnchorable => FindToolWindowAnchorable(SettingsToolWindowContentId);

        private LayoutAnchorable TrainPathToolWindowAnchorable => FindToolWindowAnchorable(TrainPathToolWindowContentId);

        private LayoutAnchorable FindToolWindowAnchorable(string contentId)
        {
            if (DockingManager.Layout is null)
                return null;

            return DockingManager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(anchorable =>
                string.Equals(anchorable.ContentId, contentId, StringComparison.Ordinal));
        }

        private LayoutAnchorable EnsureToolWindowAnchorable(LayoutAnchorable template, string contentId, string title)
        {
            LayoutAnchorable existing = FindToolWindowAnchorable(contentId);
            if (existing is not null)
                return existing;

            if (template is null || DockingManager.Layout?.RootPanel is null)
                return null;

            LayoutAnchorablePane pane = DockingManager.Layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault();
            if (pane is null)
                return null;

            if (!pane.Children.Contains(template))
                pane.Children.Insert(0, template);

            template.ContentId = contentId;
            template.CanClose = false;
            template.CanHide = true;
            template.Title = title;

            return template;
        }

        private LayoutAnchorable EnsureLocationToolWindowAnchorable() =>
            EnsureToolWindowAnchorable(LocationToolAnchorable, LocationToolWindowContentId, "Location");

        private LayoutAnchorable EnsureDebugToolWindowAnchorable() =>
            EnsureToolWindowAnchorable(DebugToolAnchorable, DebugToolWindowContentId, "Debug Information");

        private LayoutAnchorable EnsureLogToolWindowAnchorable() =>
            EnsureToolWindowAnchorable(LogToolAnchorable, LogToolWindowContentId, "Logging");

        private LayoutAnchorable EnsureTrackItemInfoToolWindowAnchorable() =>
            EnsureToolWindowAnchorable(TrackItemInfoToolAnchorable, TrackItemInfoToolWindowContentId, "Track Item Information");

        private LayoutAnchorable EnsureTrackNodeInfoToolWindowAnchorable() =>
            EnsureToolWindowAnchorable(TrackNodeInfoToolAnchorable, TrackNodeInfoToolWindowContentId, "Track Node Information");

        private LayoutAnchorable EnsureHelpToolWindowAnchorable() =>
            EnsureToolWindowAnchorable(HelpToolAnchorable, HelpToolWindowContentId, "Help");

        private LayoutAnchorable EnsureSettingsToolWindowAnchorable() =>
            EnsureToolWindowAnchorable(SettingsToolAnchorable, SettingsToolWindowContentId, "Settings");

        private LayoutAnchorable EnsureTrainPathToolWindowAnchorable() =>
            EnsureToolWindowAnchorable(TrainPathToolAnchorable, TrainPathToolWindowContentId, "Train Path Details");

        private void UpdateLocationToolWindowLifecycle()
        {
            bool isVisible = LocationToolWindowAnchorable?.IsVisible == true;
            viewModel.IsLocationToolVisible = isVisible;
            UpdateToolWindowLifecycle(isVisible, viewModel.LocationTool, () => viewModel.LocationTool.Start(), () => viewModel.LocationTool.Stop());
        }

        private void UpdateDebugToolWindowLifecycle()
        {
            bool isVisible = DebugToolWindowAnchorable?.IsVisible == true;
            viewModel.IsDebugToolVisible = isVisible;
            UpdateToolWindowLifecycle(isVisible, viewModel.DebugTool, () => viewModel.DebugTool.Start(), () => viewModel.DebugTool.Stop());
        }

        private void UpdateLogToolWindowLifecycle()
        {
            bool isVisible = LogToolWindowAnchorable?.IsVisible == true;
            viewModel.IsLogToolVisible = isVisible;
            UpdateToolWindowLifecycle(isVisible, viewModel.LogTool, () => viewModel.LogTool.Start(), () => viewModel.LogTool.Stop());
        }

        private void UpdateTrackItemInfoToolWindowLifecycle()
        {
            bool isVisible = TrackItemInfoToolWindowAnchorable?.IsVisible == true;
            viewModel.IsTrackItemInfoToolVisible = isVisible;
            UpdateToolWindowLifecycle(isVisible, viewModel.TrackItemInfoTool, () => viewModel.TrackItemInfoTool.Start(), () => viewModel.TrackItemInfoTool.Stop());
        }

        private void UpdateTrackNodeInfoToolWindowLifecycle()
        {
            bool isVisible = TrackNodeInfoToolWindowAnchorable?.IsVisible == true;
            viewModel.IsTrackNodeInfoToolVisible = isVisible;
            UpdateToolWindowLifecycle(isVisible, viewModel.TrackNodeInfoTool, () => viewModel.TrackNodeInfoTool.Start(), () => viewModel.TrackNodeInfoTool.Stop());
        }

        private void UpdateHelpToolWindowLifecycle()
        {
            bool isVisible = HelpToolWindowAnchorable?.IsVisible == true;
            viewModel.IsHelpToolVisible = isVisible;
            UpdateToolWindowLifecycle(isVisible, viewModel.HelpTool, () => viewModel.HelpTool.Start(), () => viewModel.HelpTool.Stop());
        }

        private void UpdateSettingsToolWindowLifecycle()
        {
            bool isVisible = SettingsToolWindowAnchorable?.IsVisible == true;
            viewModel.IsSettingsToolVisible = isVisible;
            UpdateToolWindowLifecycle(isVisible, viewModel.SettingsTool, () => viewModel.SettingsTool.Start(), () => viewModel.SettingsTool.Stop());
        }

        private void UpdateTrainPathToolWindowLifecycle()
        {
            bool isVisible = TrainPathToolWindowAnchorable?.IsVisible == true;
            viewModel.IsTrainPathToolVisible = isVisible;
            UpdateToolWindowLifecycle(isVisible, viewModel.TrainPathTool, () => viewModel.TrainPathTool.Start(), () => viewModel.TrainPathTool.Stop());
        }

        private static void UpdateToolWindowLifecycle(bool isVisible, object viewModelInstance, Action start, Action stop)
        {
            if (viewModelInstance is null)
                return;

            if (isVisible)
                start?.Invoke();
            else
                stop?.Invoke();
        }

        private void ActivateMapInput()
        {
            UpdateHostedInputCapture(forceMapActive: true);
        }

        private void UpdateHostedInputCapture(bool forceMapActive = false)
        {
            bool mapIsActive = forceMapActive || IsMapInteractionActive();
            MapHost.SetInputCaptured(!mapIsActive);
        }

        private bool IsMapInteractionActive()
        {
            // Base interactivity purely on whether the pointer is over the hosted map surface (or it holds
            // keyboard focus). Do not gate on the main window IsActive: when a tool window is floated into its
            // own owned top-level window, that floating window holds activation and the main window reports
            // IsActive == false, which previously captured input and froze map hover updates. A genuine switch
            // to another application is still handled by the deactivation path (ApplyDeactivationCaptureAsync).
            return MapHost.IsMouseOver || MapHost.IsKeyboardFocusWithin;
        }

        private async Task ApplyDeactivationCaptureAsync(int sequence)
        {
            await Task.Delay(80).ConfigureAwait(true);

            // Only capture input when the application has genuinely lost activation to another app. Floating a
            // tool window creates an owned top-level window whose activation deactivates this main window; in
            // that case one of our own windows is still active, so we must keep map input flowing (otherwise
            // hovering the map no longer updates tool-window content such as Track Node Information).
            if (sequence == deactivationSequence && !IsAnyApplicationWindowActive())
                MapHost.SetInputCaptured(true);
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
            try
            {
                currentProfile = await currentProfile.Current(CancellationToken.None).ConfigureAwait(true);
                toolboxSettings = await currentProfile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(true);

                if (string.IsNullOrWhiteSpace(toolboxSettings.DockLayoutXml))
                    return;

                if (IsMapDocumentFloating(toolboxSettings.DockLayoutXml))
                {
                    Trace.TraceWarning("Skipping persisted dock layout because it restores MapViewDocument as a floating window.");
                    toolboxSettings.DockLayoutXml = null;
                    toolboxSettings = await currentProfile.UpdateSettingsModel(toolboxSettings, CancellationToken.None).ConfigureAwait(true);
                    return;
                }

                XmlLayoutSerializer serializer = new(DockingManager);
                using StringReader stringReader = new(toolboxSettings.DockLayoutXml);
                serializer.Deserialize(stringReader);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to load dock layout: {ex.Message}");
            }
        }

        private async Task SaveDockLayoutAsync()
        {
            try
            {
                if (toolboxSettings is null || currentProfile is null)
                    return;

                XmlLayoutSerializer serializer = new(DockingManager);
                using StringWriter stringWriter = new();
                serializer.Serialize(stringWriter);

                toolboxSettings.DockLayoutXml = stringWriter.ToString();
                toolboxSettings = await currentProfile.UpdateSettingsModel(toolboxSettings, CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to save dock layout: {ex.Message}");
            }
        }

        private static bool IsMapDocumentFloating(string layoutXml)
        {
            try
            {
                XDocument layout = XDocument.Parse(layoutXml);

                foreach (XElement document in layout.Descendants().Where(element => element.Name.LocalName == "LayoutDocument"))
                {
                    if (!string.Equals((string)document.Attribute("ContentId"), "MapViewDocument", StringComparison.Ordinal))
                        continue;

                    if (string.Equals((string)document.Attribute("IsFloating"), "True", StringComparison.OrdinalIgnoreCase))
                        return true;

                    for (XElement parent = document.Parent; parent != null; parent = parent.Parent)
                    {
                        if (parent.Name.LocalName == "LayoutFloatingWindow")
                            return true;
                    }

                    return false;
                }

                // Missing map document in persisted layout is treated as invalid.
                return true;
            }
            catch
            {
                return true;
            }
        }

        private async Task ShutdownAsync()
        {
            if (isShuttingDown)
                return;

            isShuttingDown = true;

            Loaded -= MainWindow_Loaded;
            Activated -= MainWindow_Activated;
            Deactivated -= MainWindow_Deactivated;
            MapHost.GotFocus -= MapHost_GotFocus;
            MapHost.GotKeyboardFocus -= MapHost_GotKeyboardFocus;
            MapHost.MouseEnter -= MapHost_MouseEnter;
            MapHost.PreviewMouseDown -= MapHost_PreviewMouseDown;
            MapHost.HostedWindowPointerDown -= MapHost_HostedWindowPointerDown;
            MapHost.HostedMenuReady -= MapHost_HostedMenuReady;
            MapHost.HostedToolWindowsReady -= MapHost_HostedToolWindowsReady;
            MapHost.SaveTrainPathRequested -= MapHost_SaveTrainPathRequested;
            UnhookToolWindowAnchorable(LocationToolWindowAnchorable, LocationToolAnchorable_PropertyChanged);
            UnhookToolWindowAnchorable(DebugToolWindowAnchorable, DebugToolAnchorable_PropertyChanged);
            UnhookToolWindowAnchorable(LogToolWindowAnchorable, LogToolAnchorable_PropertyChanged);
            UnhookToolWindowAnchorable(TrackItemInfoToolWindowAnchorable, TrackItemInfoToolAnchorable_PropertyChanged);
            UnhookToolWindowAnchorable(TrackNodeInfoToolWindowAnchorable, TrackNodeInfoToolAnchorable_PropertyChanged);
            UnhookToolWindowAnchorable(HelpToolWindowAnchorable, HelpToolAnchorable_PropertyChanged);
            UnhookToolWindowAnchorable(SettingsToolWindowAnchorable, SettingsToolAnchorable_PropertyChanged);
            UnhookToolWindowAnchorable(TrainPathToolWindowAnchorable, TrainPathToolAnchorable_PropertyChanged);
            DockingManager.ActiveContentChanged -= DockingManager_ActiveContentChanged;

            await SaveDockLayoutAsync().ConfigureAwait(true);
            DisposeToolWindowViewModels();
            MapHost.Dispose();
        }

        private static void UnhookToolWindowAnchorable(LayoutAnchorable anchorable, PropertyChangedEventHandler handler)
        {
            if (anchorable is null || handler is null)
                return;

            anchorable.PropertyChanged -= handler;
        }

        private void DisposeToolWindowViewModels()
        {
            viewModel.LocationTool?.Dispose();
            viewModel.DebugTool?.Dispose();
            viewModel.LogTool?.Dispose();
            viewModel.TrackItemInfoTool?.Dispose();
            viewModel.TrackNodeInfoTool?.Dispose();
            viewModel.HelpTool?.Dispose();
            viewModel.SettingsTool?.Dispose();
            viewModel.TrainPathTool?.Dispose();
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            await ShutdownAsync().ConfigureAwait(true);
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
