using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Hosting;

using GetText;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>Browses imported folders and route headers before a map session exists.</summary>
    internal sealed class RouteBrowserViewModel : ObservableObject
    {
        private readonly Action<FolderModel, RouteModelHeader> openRoute;
        private ImmutableArray<FolderModel> folders = ImmutableArray<FolderModel>.Empty;
        private ImmutableArray<RouteModelHeader> routes = ImmutableArray<RouteModelHeader>.Empty;
        private ImmutableArray<ToolboxMenuEntry> folderEntries = ImmutableArray<ToolboxMenuEntry>.Empty;
        private ImmutableArray<ToolboxMenuEntry> routeEntries = ImmutableArray<ToolboxMenuEntry>.Empty;
        private ToolboxMenuEntry selectedFolder;
        private ToolboxMenuEntry selectedRoute;
        private string statusMessage;
        private string error;
        private bool enabled = true;
        private long discoveryVersion;

        internal RouteBrowserViewModel(Action<FolderModel, RouteModelHeader> openRoute)
        {
            this.openRoute = openRoute ?? throw new ArgumentNullException(nameof(openRoute));
        }

        public ImmutableArray<ToolboxMenuEntry> ContentFolders => folderEntries;
        public ImmutableArray<ToolboxMenuEntry> Routes => routeEntries;
        public bool Enabled => enabled;
        public string StatusMessage => statusMessage;
        public string Error => error;

        public ToolboxMenuEntry SelectedFolder
        {
            get => selectedFolder;
            set
            {
                if (!enabled || value == null || !folderEntries.Contains(value) || value.Equals(selectedFolder))
                    return;
                DiscoverRoutes(folders.Single(folder => string.Equals(folder.Id, value.Key.FolderId, StringComparison.OrdinalIgnoreCase)));
            }
        }

        public ToolboxMenuEntry SelectedRoute
        {
            get => selectedRoute;
            set
            {
                if (!enabled || value == null || !routeEntries.Contains(value) || !SetProperty(ref selectedRoute, value))
                    return;
                FolderModel folder = folders.Single(folder => string.Equals(folder.Id, selectedFolder.Key.FolderId, StringComparison.OrdinalIgnoreCase));
                RouteModelHeader route = routes.Single(route => string.Equals(route.Id, value.Key.RouteId, StringComparison.OrdinalIgnoreCase));
                openRoute(folder, route);
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
                // Native records have lazy mutable caches in their equality/hash; WPF needs stable identities.
                folderEntries = folders.Select(folder => new ToolboxMenuEntry(new ToolboxMenuKey(folder.Id, null, null), folder.Name, null)).ToImmutableArray();
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
            SetProperty(ref selectedFolder, folderEntries.Single(entry => string.Equals(entry.Key.FolderId, folder.Id, StringComparison.OrdinalIgnoreCase)), nameof(SelectedFolder));
            selectedRoute = null;
            routes = ImmutableArray<RouteModelHeader>.Empty;
            routeEntries = ImmutableArray<ToolboxMenuEntry>.Empty;
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
                routeEntries = routes.Select(route => new ToolboxMenuEntry(new ToolboxMenuKey(folder.Id, route.Id, null), route.Name, route.Description)).ToImmutableArray();
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