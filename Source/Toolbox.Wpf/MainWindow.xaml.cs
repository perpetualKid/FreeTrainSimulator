using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

using AvalonDock.Layout.Serialization;

using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.Wpf.ViewModels;

namespace FreeTrainSimulator.Toolbox.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel viewModel = new MainWindowViewModel();
        private ProfileModel currentProfile;
        private ProfileToolboxSettingsModel toolboxSettings;
        private bool isShuttingDown;
        private int deactivationSequence;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = viewModel;
            viewModel.ToggleDebugToolCommand = new RelayCommand(_ => ToggleDebugToolWindow(), _ => viewModel.DebugTool != null);

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
            DebugToolAnchorable.PropertyChanged += DebugToolAnchorable_PropertyChanged;
            DockingManager.ActiveContentChanged += DockingManager_ActiveContentChanged;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDockLayoutAsync().ConfigureAwait(true);
            UpdateDebugToolWindowLifecycle();
            UpdateHostedInputCapture();
        }

        private void MapHost_HostedMenuReady(object sender, EventArgs e)
        {
            if (MapHost.HostedMenu != null)
                viewModel.Menu = new ToolboxMenuViewModel(MapHost.HostedMenu, Dispatcher);
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

        private void MapHost_HostedToolWindowsReady(object sender, EventArgs e)
        {
            if (MapHost.HostedDebugToolWindow is null)
                return;

            viewModel.DebugTool = new DebugToolWindowViewModel(MapHost.HostedDebugToolWindow, Dispatcher);
            viewModel.ToggleDebugToolCommand.RaiseCanExecuteChanged();
            UpdateDebugToolWindowLifecycle();
            UpdateHostedInputCapture();
        }

        private void ToggleDebugToolWindow()
        {
            DebugToolAnchorable.IsVisible = !DebugToolAnchorable.IsVisible;
            UpdateDebugToolWindowLifecycle();
            UpdateHostedInputCapture();
        }

        private void DebugToolAnchorable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DebugToolAnchorable.IsVisible))
                UpdateDebugToolWindowLifecycle();
        }

        private void UpdateDebugToolWindowLifecycle()
        {
            if (viewModel.DebugTool is null)
                return;

            if (DebugToolAnchorable.IsVisible)
                viewModel.DebugTool.Start();
            else
                viewModel.DebugTool.Stop();
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
            if (!IsActive)
                return false;

            // Use direct host interaction state instead of DockingManager.ActiveContent. For hosted HWND content,
            // ActiveContent can lag or stay on the previous tool window, which leaves input captured even after
            // the user returns to the map.
            return MapHost.IsMouseOver || MapHost.IsKeyboardFocusWithin;
        }

        private async Task ApplyDeactivationCaptureAsync(int sequence)
        {
            await Task.Delay(80).ConfigureAwait(true);
            if (sequence == deactivationSequence && !IsActive)
                MapHost.SetInputCaptured(true);
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
            DebugToolAnchorable.PropertyChanged -= DebugToolAnchorable_PropertyChanged;
            DockingManager.ActiveContentChanged -= DockingManager_ActiveContentChanged;

            await SaveDockLayoutAsync().ConfigureAwait(true);
            viewModel.DebugTool?.Dispose();
            MapHost.Dispose();
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
