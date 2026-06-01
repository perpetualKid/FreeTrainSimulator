using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.Wpf.Hosting;

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

        public ToolboxMenuViewModel(HostedToolboxMenu menu, Dispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(menu);
            ArgumentNullException.ThrowIfNull(dispatcher);
            this.menu = menu;
            this.dispatcher = dispatcher;

            SelectFolderCommand = new RelayCommand(parameter => OnSelectFolder(parameter as FolderModel), _ => Enabled);
            ToggleRouteCommand = new RelayCommand(parameter => OnToggleRoute(parameter as RouteModelHeader), _ => Enabled);
            TogglePathCommand = new RelayCommand(parameter => OnTogglePath(parameter as PathModelHeader), _ => Enabled);
            EditPathCommand = new RelayCommand(_ => menu.EditPath(), _ => Enabled);
            SavePathCommand = new RelayCommand(_ => menu.SavePath(), _ => Enabled);
            TakeScreenshotCommand = new RelayCommand(_ => menu.TakeScreenshot(), _ => Enabled);
            ShowAboutCommand = new RelayCommand(_ => menu.ShowAbout(), _ => Enabled);
            QuitCommand = new RelayCommand(_ => menu.Quit());

            menu.ContentFoldersChanged += Menu_ContentFoldersChanged;
            menu.RoutesChanged += Menu_RoutesChanged;
            menu.PathsChanged += Menu_PathsChanged;
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

        public RelayCommand SelectFolderCommand { get; }

        public RelayCommand ToggleRouteCommand { get; }

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
            => RunOnDispatcher(() => ReplaceContent(ContentFolders, menu.ContentFolders));

        private void Menu_RoutesChanged(object sender, EventArgs e)
            => RunOnDispatcher(() => ReplaceContent(Routes, menu.Routes));

        private void Menu_PathsChanged(object sender, EventArgs e)
            => RunOnDispatcher(() => ReplaceContent(Paths, menu.Paths));

        private void Menu_SelectedRouteChanged(object sender, EventArgs e)
            => RunOnDispatcher(() => SelectedRouteName = menu.SelectedRouteName);

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

        private void RaiseCommandsCanExecuteChanged()
        {
            SelectFolderCommand.RaiseCanExecuteChanged();
            ToggleRouteCommand.RaiseCanExecuteChanged();
            TogglePathCommand.RaiseCanExecuteChanged();
            EditPathCommand.RaiseCanExecuteChanged();
            SavePathCommand.RaiseCanExecuteChanged();
            TakeScreenshotCommand.RaiseCanExecuteChanged();
            ShowAboutCommand.RaiseCanExecuteChanged();
        }
    }
}
