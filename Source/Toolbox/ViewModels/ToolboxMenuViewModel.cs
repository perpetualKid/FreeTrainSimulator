using System;
using System.Collections.Immutable;
using System.Linq;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox.Hosting;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>WPF presentation of captured menu state. The session alone accepts content selections.</summary>
    internal sealed class ToolboxMenuViewModel : ObservableObject, IDisposable
    {
        private readonly HostedToolboxMenu menu;
        private readonly Dispatcher dispatcher;
        private ToolboxMenuSnapshot snapshot;
        private ToolboxMenuEntry selectedFolder;
        private ToolboxMenuEntry selectedRoute;
        private bool applyingState;
        private bool disposed;

        public ToolboxMenuViewModel(HostedToolboxMenu menu, Dispatcher dispatcher)
        {
            this.menu = menu ?? throw new ArgumentNullException(nameof(menu));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            EditPathCommand = new RelayCommand(_ => menu.EditPath(snapshot.Command(snapshot.CommittedRoute)), _ => snapshot?.CanEditPath == true);
            SavePathCommand = new RelayCommand(_ => menu.SavePath(snapshot.Command(snapshot.CommittedRoute)), _ => snapshot?.CanEditPath == true);
            TakeScreenshotCommand = new RelayCommand(_ => menu.TakeScreenshot(), _ => Enabled);
            ShowAboutCommand = new RelayCommand(_ => menu.ShowAbout(), _ => Enabled);
            QuitCommand = new RelayCommand(_ => menu.Quit());

            // Subscribe first. A concurrent publication can be delivered twice, never lost; Apply rejects
            // duplicate/older revisions. Notifications always enqueue their captured value, not a live reread.
            menu.SnapshotChanged += MenuSnapshotChanged;
            Apply(menu.Current);
        }

        /// <summary>Captured folder entries.</summary>
        public ImmutableArray<ToolboxMenuEntry> ContentFolders => snapshot?.Folders ?? ImmutableArray<ToolboxMenuEntry>.Empty;
        /// <summary>Captured route entries for the accepted folder.</summary>
        public ImmutableArray<ToolboxMenuEntry> Routes => snapshot?.Routes ?? ImmutableArray<ToolboxMenuEntry>.Empty;
        /// <summary>Whether the owner currently admits menu interaction.</summary>
        public bool Enabled => snapshot?.Enabled == true;
        /// <summary>The current transition, for status presentation.</summary>
        public ToolboxMenuStatus Status => snapshot?.Status ?? ToolboxMenuStatus.Ready;
        /// <summary>The captured failure message.</summary>
        public string Error => snapshot?.Error;

        /// <summary>Two-way selector presentation; writes outside Apply express explicit user intent.</summary>
        public ToolboxMenuEntry SelectedFolder
        {
            get => selectedFolder;
            set
            {
                if (applyingState || disposed || !SetProperty(ref selectedFolder, value) || value == null || !Enabled)
                    return;
                ToolboxMenuCommand command = snapshot.Command(value.Key);
                menu.SelectFolder(command);
            }
        }

        /// <summary>Accepted route during confirmation, pending route while loading, otherwise the committed route.</summary>
        public ToolboxMenuEntry SelectedRoute
        {
            get => selectedRoute;
            set
            {
                if (applyingState || disposed || !SetProperty(ref selectedRoute, value) || value == null || !Enabled)
                    return;
                ToolboxMenuCommand command = snapshot.Command(value.Key);
                menu.ToggleRoute(command);
            }
        }

        public RelayCommand EditPathCommand { get; }
        public RelayCommand SavePathCommand { get; }
        public RelayCommand TakeScreenshotCommand { get; }
        public RelayCommand ShowAboutCommand { get; }
        public RelayCommand QuitCommand { get; }

        private void MenuSnapshotChanged(object sender, ToolboxMenuSnapshot captured)
        {
            if (!disposed)
                _ = dispatcher.BeginInvoke(new Action(() => Apply(captured)));
        }

        private void Apply(ToolboxMenuSnapshot captured)
        {
            if (disposed || captured == null || snapshot != null &&
                (captured.SessionId != snapshot.SessionId || captured.Revision <= snapshot.Revision))
                return;

            applyingState = true;
            try
            {
                // WPF can retain an equal old SelectedItem when a captured entry's display data changes.
                // Release selector presentation before replacing the lists, then use the current entries.
                selectedFolder = null;
                selectedRoute = null;
                OnPropertyChanged(nameof(SelectedFolder));
                OnPropertyChanged(nameof(SelectedRoute));
                snapshot = captured;
                OnPropertyChanged(nameof(ContentFolders));
                OnPropertyChanged(nameof(Routes));
                selectedFolder = ContentFolders.FirstOrDefault(entry => entry.Key.Equals(captured.AcceptedFolder));
                ToolboxMenuKey route = captured.Status == ToolboxMenuStatus.LoadingRoute ? captured.PendingRoute : captured.CommittedRoute;
                selectedRoute = Routes.FirstOrDefault(entry => entry.Key.Equals(route));
                OnPropertyChanged(nameof(SelectedFolder));
                OnPropertyChanged(nameof(SelectedRoute));
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(Error));
                OnPropertyChanged(nameof(Enabled));
                EditPathCommand.RaiseCanExecuteChanged();
                SavePathCommand.RaiseCanExecuteChanged();
                TakeScreenshotCommand.RaiseCanExecuteChanged();
                ShowAboutCommand.RaiseCanExecuteChanged();
            }
            finally
            {
                applyingState = false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            menu.SnapshotChanged -= MenuSnapshotChanged;
        }
    }
}