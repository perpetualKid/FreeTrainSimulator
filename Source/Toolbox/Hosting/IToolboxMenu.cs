using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    /// <summary>
    /// Abstraction over the toolbox main menu surface that <see cref="GameWindow"/> drives.
    /// Implemented by a hosted bridge that forwards menu data to the native WPF menu.
    /// </summary>
    internal interface IToolboxMenu
    {
        /// <summary>Enables or disables the whole menu (e.g. while a modal window is open).</summary>
        bool Enabled { get; set; }

        /// <summary>Populates the list of available content folders.</summary>
        void PopulateContentFolders(ImmutableArray<FolderModel> folders);

        /// <summary>Populates the list of routes for the currently selected folder.</summary>
        void PopulateRoutes(ImmutableArray<RouteModelHeader> routes);

        /// <summary>Populates the list of paths for the currently loaded route.</summary>
        void PopulatePaths(ImmutableArray<PathModelHeader> paths);

        /// <summary>Clears the path menu (e.g. after unloading a route).</summary>
        void ClearPathMenu();

        /// <summary>Marks the route with the given name as the selected one.</summary>
        void PreSelectRoute(string routeName);

        /// <summary>Marks the given path as the selected one, or clears the selection when null.</summary>
        void PreSelectPath(PathModelHeader path);

        /// <summary>Selects the content folder with the given name and starts loading its routes.</summary>
        FolderModel SelectContentFolder(string folderName);
    }
}
