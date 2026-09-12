using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.Hosting;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the native WPF main menu. Wraps the hosted <see cref="HostedToolboxMenu"/>
    /// bridge: it mirrors the bridge's folder/route/path data and selection state into observable
    /// collections, and exposes commands that forward user actions back to the bridge (which marshals
    /// them onto the game thread).
    /// <para>
    /// Bridge change events are raised on the game thread; this view model marshals every update onto the
    /// WPF dispatcher so the bound UI is always touched on the UI thread.
    /// </para>
    /// </summary>
    internal sealed class ToolboxMenuViewModel : ObservableObject, IDisposable
    {
        private readonly HostedToolboxMenu menu;
        private readonly Dispatcher dispatcher;

        private bool enabled = true;
        private string selectedRouteName;
        private PathModelHeader selectedPath;
        private FolderModel selectedFolder;
        private RouteModelHeader selectedRoute;
        private bool synchronizingSelection;
        private bool disposed;

        public ToolboxMenuViewModel(HostedToolboxMenu menu, Dispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(menu);
            ArgumentNullException.ThrowIfNull(dispatcher);
            this.menu = menu;
            this.dispatcher = dispatcher;

            EditPathCommand = new RelayCommand(_ => menu.EditPath(), _ => Enabled);
            SavePathCommand = new RelayCommand(_ => menu.SavePath(), _ => Enabled);
            TakeScreenshotCommand = new RelayCommand(_ => menu.TakeScreenshot(), _ => Enabled);
            ShowAboutCommand = new RelayCommand(_ => menu.ShowAbout(), _ => Enabled);
            QuitCommand = new RelayCommand(_ => menu.Quit());

            menu.ContentFoldersChanged += MenuContentFoldersChanged;
            menu.RoutesChanged += MenuRoutesChanged;
            menu.PathsChanged += MenuPathsChanged;
            menu.SelectedFolderChanged += MenuSelectedFolderChanged;
            menu.SelectedRouteChanged += MenuSelectedRouteChanged;
            menu.SelectedPathChanged += MenuSelectedPathChanged;
            menu.EnabledChanged += MenuEnabledChanged;

            // Pull any data that was populated before the view model subscribed.
            ReplaceContent(ContentFolders, menu.ContentFolders);
            ReplaceContent(Routes, menu.Routes);
            ReplaceContent(Paths, menu.Paths);
            enabled = menu.Enabled;
            selectedRouteName = menu.SelectedRouteName;
            selectedPath = FindPathById(menu.SelectedPath?.Id);
            selectedFolder = FindFolderByName(menu.SelectedFolder?.Name);
            selectedRoute = FindRouteByName(menu.SelectedRouteName);
        }

        public ObservableCollection<FolderModel> ContentFolders { get; } = new ObservableCollection<FolderModel>();

        public ObservableCollection<RouteModelHeader> Routes { get; } = new ObservableCollection<RouteModelHeader>();

        public ObservableCollection<PathModelHeader> Paths { get; } = new ObservableCollection<PathModelHeader>();

        public bool Enabled
        {
            get => enabled;
            private set
            {
                if (SetProperty(ref enabled, value))
                    RaiseCommandsCanExecuteChanged();
            }
        }

        public string SelectedRouteName
        {
            get => selectedRouteName;
            private set => SetProperty(ref selectedRouteName, value);
        }

        public PathModelHeader SelectedPath
        {
            get => selectedPath;
            set
            {
                if (!SetProperty(ref selectedPath, value) || synchronizingSelection)
                    return;
                OnTogglePath(value);
            }
        }

        /// <summary>
        /// Content folder chosen in the Routes tool window. Bound one-way (source to target) only; user picks
        /// are delivered through <see cref="UserSelectFolder"/> from the view's SelectionChanged handler. A
        /// TwoWay binding is unreliable here because AvalonDock re-parents the tool-window content, cycling the
        /// DataContext and silently dropping the ComboBox's target-to-source write-back.
        /// </summary>
        public FolderModel SelectedFolder
        {
            get => selectedFolder;
            private set => SetProperty(ref selectedFolder, value);
        }

        /// <summary>
        /// Route chosen in the Routes tool window. Bound one-way (source to target) only; user picks are
        /// delivered through <see cref="UserSelectRoute"/> from the view's SelectionChanged handler (see
        /// <see cref="SelectedFolder"/> for why TwoWay is unreliable here).
        /// </summary>
        public RouteModelHeader SelectedRoute
        {
            get => selectedRoute;
            private set => SetProperty(ref selectedRoute, value);
        }

        public RelayCommand EditPathCommand { get; }

        public RelayCommand SavePathCommand { get; }

        public RelayCommand TakeScreenshotCommand { get; }

        public RelayCommand ShowAboutCommand { get; }

        public RelayCommand QuitCommand { get; }

        /// <summary>
        /// Handles a user-initiated content-folder pick from the Routes tool window. Ignored during
        /// programmatic synchronization or when the pick matches the current selection. The selection is not
        /// applied here; it is committed authoritatively by <see cref="RebuildFolders"/> once the
        /// hosted bridge reports the new folder (mirroring the WinForms menu flow).
        /// </summary>
        public void UserSelectFolder(FolderModel folder)
        {
            if (folder == null || synchronizingSelection || EqualityComparer<FolderModel>.Default.Equals(selectedFolder, folder))
                return;

            // Sync the backing field to the user's pick immediately (without raising PropertyChanged, since the
            // ComboBox already shows it). This keeps the OneWay binding SOURCE aligned with the displayed value
            // during the async bridge round-trip; otherwise a binding refresh in that window re-pushes the stale
            // previous folder back into the ComboBox and the display reverts. The authoritative confirmation is
            // still applied by RebuildFolders once the bridge reports the new folder.
            selectedFolder = folder;
            OnSelectFolder(folder);
        }

        /// <summary>
        /// Handles a user-initiated route pick from the Routes tool window. Ignored during programmatic
        /// synchronization or when the pick matches the current selection. The selection is committed
        /// authoritatively by <see cref="RebuildRoutes"/> once the bridge reports the new route.
        /// </summary>
        public void UserSelectRoute(RouteModelHeader route)
        {
            if (route == null || synchronizingSelection || EqualityComparer<RouteModelHeader>.Default.Equals(selectedRoute, route))
                return;

            // Keep the OneWay binding source aligned with the displayed pick (see UserSelectFolder).
            selectedRoute = route;
            OnToggleRoute(route);
        }

        private void OnSelectFolder(FolderModel folder)
        {
            if (folder == null)
                return;

            DeferToBridge(() => menu.SelectFolder(folder));
        }

        private void OnToggleRoute(RouteModelHeader route)
        {
            if (route != null)
                DeferToBridge(() => menu.ToggleRoute(route));
        }

        private void OnTogglePath(PathModelHeader path)
        {
            if (path != null)
                DeferToBridge(() => menu.TogglePath(path));
        }

        // Forwards a selection change to the hosted bridge, but only after the current WPF selection commit
        // has fully unwound. In hosted mode the game thread is the WPF UI thread, so the bridge raises its
        // Selected*Changed notifications synchronously/reentrantly; letting them run while a Selector is still
        // committing the user's pick corrupts the ComboBox binding and causes the next pick to be ignored.
        // Posting at Background priority guarantees the Selector finishes first.
        private void DeferToBridge(Action action)
        {
            if (disposed)
                return;

            _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!disposed)
                    action();
            }));
        }

        private void MenuContentFoldersChanged(object sender, EventArgs e)
            => RunOnDispatcher(() => RebuildFolders());

        private void MenuRoutesChanged(object sender, EventArgs e)
            => RunOnDispatcher(() => RebuildRoutes());

        private void MenuPathsChanged(object sender, EventArgs e)
            => RunOnDispatcher(() => RebuildPaths());

        private void MenuSelectedFolderChanged(object sender, EventArgs e) => RunOnDispatcher(RebuildFolders);

        private void MenuSelectedRouteChanged(object sender, EventArgs e)
            => RunOnDispatcher(() =>
            {
                SelectedRouteName = menu.SelectedRouteName;
                UpdateSelectedRoute();
            });

        private void MenuSelectedPathChanged(object sender, EventArgs e) => RunOnDispatcher(UpdateSelectedPath);

        private void MenuEnabledChanged(object sender, EventArgs e) => RunOnDispatcher(() => Enabled = menu.Enabled);

        private void RunOnDispatcher(Action action)
        {
            if (disposed)
                return;

            if (dispatcher.CheckAccess())
                action();
            else
                _ = dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!disposed)
                        action();
                }));
        }

        private static void ReplaceContent<T>(ObservableCollection<T> target, System.Collections.Immutable.ImmutableArray<T> source) where T : ModelBase
        {
            target.Clear();
            // Distinct() collapses value-equal records: the model types are records with value-based
            // Equals/GetHashCode, and WPF's Selector keys selectable items in a dictionary by item equality.
            // Two value-equal items would insert the same key twice and throw ArgumentException during a
            // selection change. They are indistinguishable as selectable items anyway, so drop the duplicates.
            foreach (T item in source.Distinct().OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
                target.Add(item);
        }

        // Rebuilds the folder items and re-resolves the displayed selection from the bridge. This is the
        // single authoritative point that updates the folder ComboBox (mirroring the WinForms menu's
        // SetComboBoxItem). The selection is cleared BEFORE the Clear + Add rebuild: rebuilding the items while
        // the ComboBox still holds a SelectedItem drives WPF's Selector to rebuild its selection tracking
        // mid-change, and because the model types are value-equal records, an added item that is value-equal to
        // the retained selection inserts a duplicate ItemInfo key and throws ArgumentException. Emptying the
        // selection first leaves the Selector nothing to track. Guarded so assignments do not forward into a load.
        private void RebuildFolders()
        {
            synchronizingSelection = true;
            try
            {
                SelectedFolder = null;
                OnPropertyChanged(nameof(SelectedFolder));

                ReplaceContent(ContentFolders, menu.ContentFolders);

                SelectedFolder = FindFolderByName(menu.SelectedFolder?.Name);
                OnPropertyChanged(nameof(SelectedFolder));
            }
            finally
            {
                synchronizingSelection = false;
            }
        }

        // Rebuilds the route items and re-resolves the displayed selection; see RebuildFolders for the clear-
        // before-rebuild rationale.
        private void RebuildRoutes()
        {
            synchronizingSelection = true;
            try
            {
                SelectedRoute = null;
                OnPropertyChanged(nameof(SelectedRoute));

                ReplaceContent(Routes, menu.Routes);

                SelectedRoute = FindRouteByName(menu.SelectedRouteName);
                OnPropertyChanged(nameof(SelectedRoute));
            }
            finally
            {
                synchronizingSelection = false;
            }
        }

        // Rebuilds the path items and re-resolves the displayed selection; see RebuildFolders for the clear-
        // before-rebuild rationale.
        private void RebuildPaths()
        {
            synchronizingSelection = true;
            try
            {
                SelectedPath = null;
                OnPropertyChanged(nameof(SelectedPath));

                ReplaceContent(Paths, menu.Paths);

                SelectedPath = FindPathById(menu.SelectedPath?.Id);
                OnPropertyChanged(nameof(SelectedPath));
            }
            finally
            {
                synchronizingSelection = false;
            }
        }

        // Updates the displayed route selection only, without rebuilding the item collection. Used when the
        // bridge reports a route selection change but the route list itself is unchanged, so the ComboBox does
        // not flicker through an empty selection while a route loads.
        private void UpdateSelectedRoute()
        {
            RouteModelHeader resolved = FindRouteByName(menu.SelectedRouteName);

            // Ignore the transient null the bridge raises mid-load: loading a route first unloads the current
            // one (PreSelectRoute(null)) before committing the new name, which would blank the ComboBox until
            // the load finishes. When the user is switching to another route (selectedRoute already points at
            // the target and it is still in the list), keep showing it. Genuine route clears happen through a
            // folder change (RebuildRoutes), not this path.
            if (resolved == null && selectedRoute != null && Routes.Contains(selectedRoute))
                return;

            synchronizingSelection = true;
            try
            {
                SelectedRoute = resolved;
                OnPropertyChanged(nameof(SelectedRoute));
            }
            finally
            {
                synchronizingSelection = false;
            }
        }

        // Updates the displayed path selection only, without rebuilding the item collection; see
        // UpdateSelectedRoute.
        private void UpdateSelectedPath()
        {
            synchronizingSelection = true;
            try
            {
                SelectedPath = FindPathById(menu.SelectedPath?.Id);
                OnPropertyChanged(nameof(SelectedPath));
            }
            finally
            {
                synchronizingSelection = false;
            }
        }

        private FolderModel FindFolderByName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return null;

            foreach (FolderModel folder in ContentFolders)
            {
                if (string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase))
                    return folder;
            }
            return null;
        }

        private RouteModelHeader FindRouteByName(string routeName)
        {
            if (string.IsNullOrEmpty(routeName))
                return null;

            foreach (RouteModelHeader route in Routes)
            {
                if (string.Equals(route.Name, routeName, StringComparison.Ordinal))
                    return route;
            }
            return null;
        }

        private PathModelHeader FindPathById(string pathId)
        {
            if (string.IsNullOrEmpty(pathId))
                return null;

            foreach (PathModelHeader path in Paths)
            {
                if (string.Equals(path.Id, pathId, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }

        private void RaiseCommandsCanExecuteChanged()
        {
            EditPathCommand.RaiseCanExecuteChanged();
            SavePathCommand.RaiseCanExecuteChanged();
            TakeScreenshotCommand.RaiseCanExecuteChanged();
            ShowAboutCommand.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            menu.ContentFoldersChanged -= MenuContentFoldersChanged;
            menu.RoutesChanged -= MenuRoutesChanged;
            menu.PathsChanged -= MenuPathsChanged;
            menu.SelectedFolderChanged -= MenuSelectedFolderChanged;
            menu.SelectedRouteChanged -= MenuSelectedRouteChanged;
            menu.SelectedPathChanged -= MenuSelectedPathChanged;
            menu.EnabledChanged -= MenuEnabledChanged;
        }
    }
}
