using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
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
    internal sealed class ToolboxMenuViewModel : ObservableObject
    {
        private readonly HostedToolboxMenu menu;
        private readonly Dispatcher dispatcher;

        private bool enabled = true;
        private string selectedRouteName;
        private PathModelHeader selectedPath;
        private FolderModel selectedFolder;
        private RouteModelHeader selectedRoute;
        private bool synchronizingSelection;

        public ToolboxMenuViewModel(HostedToolboxMenu menu, Dispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(menu);
            ArgumentNullException.ThrowIfNull(dispatcher);
            this.menu = menu;
            this.dispatcher = dispatcher;

            TogglePathCommand = new RelayCommand(parameter => OnTogglePath(parameter as PathModelHeader), _ => Enabled);
            EditPathCommand = new RelayCommand(_ => menu.EditPath(), _ => Enabled);
            SavePathCommand = new RelayCommand(_ => menu.SavePath(), _ => Enabled);
            TakeScreenshotCommand = new RelayCommand(_ => menu.TakeScreenshot(), _ => Enabled);
            ShowAboutCommand = new RelayCommand(_ => menu.ShowAbout(), _ => Enabled);
            QuitCommand = new RelayCommand(_ => menu.Quit());

            menu.ContentFoldersChanged += Menu_ContentFoldersChanged;
            menu.RoutesChanged += Menu_RoutesChanged;
            menu.PathsChanged += Menu_PathsChanged;
            menu.SelectedFolderChanged += Menu_SelectedFolderChanged;
            menu.SelectedRouteChanged += Menu_SelectedRouteChanged;
            menu.SelectedPathChanged += Menu_SelectedPathChanged;
            menu.EnabledChanged += Menu_EnabledChanged;

            // Pull any data that was populated before the view model subscribed.
            ReplaceContent(ContentFolders, menu.ContentFolders);
            ReplaceContent(Routes, menu.Routes);
            ReplaceContent(Paths, menu.Paths);
            enabled = menu.Enabled;
            selectedRouteName = menu.SelectedRouteName;
            selectedPath = menu.SelectedPath;
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
            private set => SetProperty(ref selectedPath, value);
        }

        /// <summary>
        /// Content folder chosen in the Routes tool window. Selecting a folder loads its routes through the
        /// hosted bridge (which unloads any currently loaded route first).
        /// </summary>
        public FolderModel SelectedFolder
        {
            get => selectedFolder;
            set
            {
                if (!SetProperty(ref selectedFolder, value) || synchronizingSelection)
                    return;
                OnSelectFolder(value);
            }
        }

        /// <summary>
        /// Route chosen in the Routes tool window. Selecting a route toggles it through the hosted bridge
        /// (loading the new route, or unloading when the already-loaded route is picked again).
        /// </summary>
        public RouteModelHeader SelectedRoute
        {
            get => selectedRoute;
            set
            {
                if (!SetProperty(ref selectedRoute, value) || synchronizingSelection)
                    return;
                OnToggleRoute(value);
            }
        }

        public RelayCommand TogglePathCommand { get; }

        public RelayCommand EditPathCommand { get; }

        public RelayCommand SavePathCommand { get; }

        public RelayCommand TakeScreenshotCommand { get; }

        public RelayCommand ShowAboutCommand { get; }

        public RelayCommand QuitCommand { get; }

        private void OnSelectFolder(FolderModel folder)
        {
            if (folder != null)
                menu.SelectFolder(folder);
        }

        private void OnToggleRoute(RouteModelHeader route)
        {
            if (route != null)
                menu.ToggleRoute(route);
        }

        private void OnTogglePath(PathModelHeader path)
        {
            if (path != null)
                menu.TogglePath(path);
        }

        private void Menu_ContentFoldersChanged(object sender, EventArgs e)
            => RunOnDispatcher(() =>
            {
                ReplaceContent(ContentFolders, menu.ContentFolders);
                // The folder instances were replaced, so re-resolve the selection against the new collection
                // to keep the Routes tool-window folder combo box in sync without re-triggering a load.
                SyncSelectedFolderFromBridge();
            });

        private void Menu_RoutesChanged(object sender, EventArgs e)
            => RunOnDispatcher(() =>
            {
                ReplaceContent(Routes, menu.Routes);
                // The route instances were replaced, so re-resolve the selection against the new collection
                // to keep the Routes tool-window combo box in sync without re-triggering a load.
                SyncSelectedRouteFromBridge();
            });

        private void Menu_PathsChanged(object sender, EventArgs e)
            => RunOnDispatcher(() => ReplaceContent(Paths, menu.Paths));

        private void Menu_SelectedFolderChanged(object sender, EventArgs e)
            => RunOnDispatcher(SyncSelectedFolderFromBridge);

        private void Menu_SelectedRouteChanged(object sender, EventArgs e)
            => RunOnDispatcher(() =>
            {
                SelectedRouteName = menu.SelectedRouteName;
                SyncSelectedRouteFromBridge();
            });

        private void Menu_SelectedPathChanged(object sender, EventArgs e)
            => RunOnDispatcher(() => SelectedPath = menu.SelectedPath);

        private void Menu_EnabledChanged(object sender, EventArgs e)
            => RunOnDispatcher(() => Enabled = menu.Enabled);

        private void RunOnDispatcher(Action action)
        {
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }

        private static void ReplaceContent<T>(ObservableCollection<T> target, System.Collections.Immutable.ImmutableArray<T> source)
        {
            target.Clear();
            foreach (T item in source)
                target.Add(item);
        }

        // Re-resolves SelectedFolder from the bridge's selected folder against the current ContentFolders
        // collection. Guarded so the assignment reflects bridge state without forwarding back into a load.
        private void SyncSelectedFolderFromBridge()
        {
            synchronizingSelection = true;
            try
            {
                SelectedFolder = FindFolderByName(menu.SelectedFolder?.Name);
            }
            finally
            {
                synchronizingSelection = false;
            }
        }

        // Re-resolves SelectedRoute from the bridge's selected route name against the current Routes
        // collection. Guarded so the assignment reflects bridge state without forwarding back into a load.
        private void SyncSelectedRouteFromBridge()
        {
            synchronizingSelection = true;
            try
            {
                SelectedRoute = FindRouteByName(menu.SelectedRouteName);
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

        private void RaiseCommandsCanExecuteChanged()
        {
            TogglePathCommand.RaiseCanExecuteChanged();
            EditPathCommand.RaiseCanExecuteChanged();
            SavePathCommand.RaiseCanExecuteChanged();
            TakeScreenshotCommand.RaiseCanExecuteChanged();
            ShowAboutCommand.RaiseCanExecuteChanged();
        }
    }
}
