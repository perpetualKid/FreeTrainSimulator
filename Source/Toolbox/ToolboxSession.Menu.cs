using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.Hosting;

namespace FreeTrainSimulator.Toolbox
{
    internal sealed partial class ToolboxSession
    {
        private readonly Guid menuSessionId = Guid.NewGuid();
        private long menuRevision;
        private long folderContext;
        private bool menuEnabled = true;
        private ImmutableArray<RouteModelHeader> menuRoutes = ImmutableArray<RouteModelHeader>.Empty;
        private ImmutableArray<PathModelHeader> menuPaths = ImmutableArray<PathModelHeader>.Empty;
        private ToolboxMenuKey pendingFolder;
        private ToolboxMenuKey pendingRoute;
        private ToolboxMenuKey loadingRoute;
        private bool discoveringRoutes;
        private ToolboxMenuEntry menuPath;
        private string menuError;

        private ToolboxMenuStatus MenuStatus => IsStopping ? ToolboxMenuStatus.Stopped
            : pendingFolder != null || pendingRoute != null ? ToolboxMenuStatus.Confirming
            : discoveringRoutes ? ToolboxMenuStatus.DiscoveringRoutes
            : loadingRoute != null ? ToolboxMenuStatus.LoadingRoute
            : menuError != null ? ToolboxMenuStatus.Failed : ToolboxMenuStatus.Ready;

        // Freshness table:
        // folder: session + current root list membership (rapid root picks survive folder acceptance);
        // route: session + accepted folder epoch + qualified membership (not route/revision changes);
        // path/editor command: additionally the committed route lifetime, rejecting unload/reload reuse.
        // Confirmation retains accepted content. Acceptance clears before discovery/load; failure retains
        // the accepted folder and its routes. UI revision orders publications only, never user intent.
        private static ToolboxMenuKey FolderKey(FolderModel folder)
            => folder == null ? null : new ToolboxMenuKey(folder.Id, null, null);

        private ToolboxMenuKey RouteKey(RouteModelHeader route)
            => route == null || selectedFolder == null ? null : new ToolboxMenuKey(selectedFolder.Id, route.Id, null);

        private ToolboxMenuEntry PathEntry(PathModelHeader path)
            => path == null || selectedRoute == null || selectedFolder == null ? null
                : new ToolboxMenuEntry(new ToolboxMenuKey(selectedFolder.Id, selectedRoute.Id, path.Id), path.Name, null);

        private void PublishMenu()
        {
            // Shutdown publishes once with admission closed. Required cleanup afterwards must not revive UI.
            if (IsDisposed || IsStopping && menuStoppedPublished)
                return;
            ImmutableArray<ToolboxMenuEntry> folders = (contentModel?.ContentFolders ?? ImmutableArray<FolderModel>.Empty)
                .Select(folder => new ToolboxMenuEntry(FolderKey(folder), folder.Name, null))
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray();
            ImmutableArray<ToolboxMenuEntry> routes = menuRoutes
                .Select(route => new ToolboxMenuEntry(RouteKey(route), route.Name, route.Description))
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray();
            ImmutableArray<ToolboxMenuEntry> paths = menuPaths.Select(PathEntry)
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray();
            game.HostedMenu?.Publish(new ToolboxMenuSnapshot(menuSessionId, ++menuRevision, folderContext, routeContentVersion,
                folders, routes, paths, FolderKey(selectedFolder), pendingFolder, pendingRoute ?? loadingRoute, RouteKey(selectedRoute),
                menuPath, MenuStatus, menuError, menuEnabled && !IsStopping));
            menuStoppedPublished = IsStopping;
        }

        private bool menuStoppedPublished;

        internal void SetMenuEnabled(bool enabled)
        {
            if (IsStopping || menuEnabled == enabled)
                return;
            menuEnabled = enabled;
            PublishMenu();
        }

        private bool IsMenuCommand(ToolboxMenuCommand command)
            => !IsStopping && menuEnabled && command != null && command.SessionId == menuSessionId && command.Key != null;

        private bool IsFolderContext(ToolboxMenuCommand command)
            => IsMenuCommand(command) && command.FolderContext == folderContext && selectedFolder != null
                && string.Equals(command.Key.FolderId, selectedFolder.Id, StringComparison.OrdinalIgnoreCase);

        private bool IsRouteContext(ToolboxMenuCommand command)
            => IsFolderContext(command) && command.RouteContext == routeContentVersion && selectedRoute != null
                && string.Equals(command.Key.RouteId, selectedRoute.Id, StringComparison.OrdinalIgnoreCase);

        private FolderModel ResolveFolder(ToolboxMenuCommand command)
            => !IsMenuCommand(command) || command.Key.RouteId != null || command.Key.PathId != null ? null
                : contentModel?.ContentFolders.FirstOrDefault(folder => FolderKey(folder).Equals(command.Key));

        private RouteModelHeader ResolveRoute(ToolboxMenuCommand command)
            => !IsFolderContext(command) || command.Key.PathId != null ? null
                : menuRoutes.FirstOrDefault(route => RouteKey(route).Equals(command.Key));

        private PathModelHeader ResolvePath(ToolboxMenuCommand command)
            => !IsRouteContext(command) ? null : menuPaths.FirstOrDefault(path => PathEntry(path).Key.Equals(command.Key));

        internal Task EditMenuPathAsync(ToolboxMenuCommand command)
            => IsRouteContext(command) && MenuStatus != ToolboxMenuStatus.Confirming ? EditPathAsync() : Task.CompletedTask;

        internal Task SaveMenuPathAsync(ToolboxMenuCommand command)
            => IsRouteContext(command) && MenuStatus != ToolboxMenuStatus.Confirming ? RequestSavePathAsync() : Task.CompletedTask;
    }
}
