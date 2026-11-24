using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

using GetText;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>Browses imported folders and route headers before a map session exists.</summary>
    internal sealed class RouteBrowserViewModel : ObservableObject
    {
        private readonly Action<FolderModel, RouteModelHeader> openRoute;
        private ImmutableArray<FolderModel> folders = ImmutableArray<FolderModel>.Empty;
        private ImmutableArray<RouteModelHeader> routes = ImmutableArray<RouteModelHeader>.Empty;
        private FolderModel selectedFolder;
        private RouteModelHeader selectedRoute;
        private string statusMessage;
        private string error;
        private bool enabled = true;
        private long discoveryVersion;

        internal RouteBrowserViewModel(Action<FolderModel, RouteModelHeader> openRoute)
        {
            this.openRoute = openRoute ?? throw new ArgumentNullException(nameof(openRoute));
        }

        public ImmutableArray<FolderModel> ContentFolders => folders;
        public ImmutableArray<RouteModelHeader> Routes => routes;
        public bool Enabled => enabled;
        public string StatusMessage => statusMessage;
        public string Error => error;

        public FolderModel SelectedFolder
        {
            get => selectedFolder;
            set
            {
                if (!enabled || value == null || !folders.Contains(value) || ReferenceEquals(value, selectedFolder))
                    return;
                DiscoverRoutes(value);
            }
        }

        public RouteModelHeader SelectedRoute
        {
            get => selectedRoute;
            set
            {
                if (!enabled || value == null || !routes.Contains(value) || !SetProperty(ref selectedRoute, value))
                    return;
                openRoute(selectedFolder, value);
            }
        }

        internal async Task InitializeAsync(string savedFolderId)
        {
            statusMessage = CatalogManager.Catalog.GetString("Discovering content folders...");
            OnPropertyChanged(nameof(StatusMessage));
            try
            {
                ContentModel content = await ContentModelExtensions.Get(null, CancellationToken.None).ConfigureAwait(true);
                
                if (!enabled)
                    return;
                if (content.RefreshRequired())
                    throw new InvalidOperationException(CatalogManager.Catalog.GetString("Content analysis is required. Import or refresh content before opening a route."));
                
                folders = content.ContentFolders.OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray();
                OnPropertyChanged(nameof(ContentFolders));
                statusMessage = null;
                OnPropertyChanged(nameof(StatusMessage));
                FolderModel savedFolder = folders.FirstOrDefault(folder => string.Equals(folder.Id, savedFolderId, StringComparison.OrdinalIgnoreCase));
                
                if (savedFolder == null && folders.Length == 1)
                    savedFolder = folders[0];
                if (savedFolder != null)
                    await LoadRoutesAsync(savedFolder).ConfigureAwait(true);
            }
            catch (Exception failure) when (CleanupExceptionPolicy.CanContinue(failure))
            {
                ReportFailure(failure);
            }
        }

        private async void DiscoverRoutes(FolderModel folder)
        {
            await LoadRoutesAsync(folder).ConfigureAwait(true);
        }

        private async Task LoadRoutesAsync(FolderModel folder)
        {
            long version = ++discoveryVersion;
            SetProperty(ref selectedFolder, folder, nameof(SelectedFolder));
            selectedRoute = null;
            routes = ImmutableArray<RouteModelHeader>.Empty;
            error = null;
            statusMessage = CatalogManager.Catalog.GetString("Discovering routes...");
            OnPropertyChanged(nameof(SelectedRoute));
            OnPropertyChanged(nameof(Routes));
            OnPropertyChanged(nameof(Error));
            OnPropertyChanged(nameof(StatusMessage));
            try
            {
                ImmutableArray<RouteModelHeader> discovered = await folder.GetRoutes(CancellationToken.None).ConfigureAwait(true);
                
                if (!enabled || version != discoveryVersion)
                    return;
                
                routes = discovered.OrderBy(route => route.Name, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray();
                statusMessage = null;
                OnPropertyChanged(nameof(Routes));
                OnPropertyChanged(nameof(StatusMessage));
            }
            catch (Exception failure) when (CleanupExceptionPolicy.CanContinue(failure))
            {
                Trace.TraceError($"Failed to discover routes in {folder.Name}: {failure}");
                if (enabled && version == discoveryVersion)
                    ReportFailure(failure);
            }
        }

        private void ReportFailure(Exception failure)
        {
            Trace.TraceError($"Failed to browse Toolbox content: {failure}");
            
            if (!enabled)
                return;
            
            error = failure.Message;
            statusMessage = CatalogManager.Catalog.GetString("Unable to discover content folders or routes.");
            OnPropertyChanged(nameof(Error));
            OnPropertyChanged(nameof(StatusMessage));
        }

        internal void Stop()
        {
            enabled = false;
            ++discoveryVersion;
            OnPropertyChanged(nameof(Enabled));
        }
    }
}