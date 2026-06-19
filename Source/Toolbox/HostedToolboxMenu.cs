using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Hosted-mode bridge between <see cref="GameWindow"/> and a native WPF main menu.
    /// <para>
    /// As an <see cref="IToolboxMenu"/> it receives menu data from the game thread (folders, routes,
    /// paths and selection state) and surfaces it through properties and change events that the WPF
    /// shell binds to. Commands triggered by the WPF menu are forwarded back onto the game thread via
    /// <see cref="GameWindow.InvokeOnGameThread(Action)"/> so all MonoGame/WinForms state stays
    /// single-threaded.
    /// </para>
    /// <para>
    /// Change events are raised on the game thread; WPF subscribers must marshal to the UI thread.
    /// </para>
    /// </summary>
    internal sealed class HostedToolboxMenu : IToolboxMenu
    {
        private readonly GameWindow game;
        private bool enabled = true;

        internal HostedToolboxMenu(GameWindow game)
        {
            ArgumentNullException.ThrowIfNull(game);
            this.game = game;
        }

        #region Bound state (data in from the game thread)

        /// <summary>Available content folders for the current profile.</summary>
        public ImmutableArray<FolderModel> ContentFolders { get; private set; } = ImmutableArray<FolderModel>.Empty;

        /// <summary>Routes available for the currently selected folder.</summary>
        public ImmutableArray<RouteModelHeader> Routes { get; private set; } = ImmutableArray<RouteModelHeader>.Empty;

        /// <summary>Paths available for the currently loaded route.</summary>
        public ImmutableArray<PathModelHeader> Paths { get; private set; } = ImmutableArray<PathModelHeader>.Empty;

        /// <summary>The currently selected content folder, or null when none is selected.</summary>
        public FolderModel SelectedFolder { get; private set; }

        /// <summary>Name of the currently selected route, or null when none is selected.</summary>
        public string SelectedRouteName { get; private set; }

        /// <summary>The currently selected path, or null when none is selected.</summary>
        public PathModelHeader SelectedPath { get; private set; }

        /// <summary>Raised whenever <see cref="ContentFolders"/> changes.</summary>
        public event EventHandler ContentFoldersChanged;

        /// <summary>Raised whenever <see cref="Routes"/> changes.</summary>
        public event EventHandler RoutesChanged;

        /// <summary>Raised whenever <see cref="Paths"/> changes.</summary>
        public event EventHandler PathsChanged;

        /// <summary>Raised whenever <see cref="SelectedFolder"/> changes.</summary>
        public event EventHandler SelectedFolderChanged;

        /// <summary>Raised whenever <see cref="SelectedRouteName"/> changes.</summary>
        public event EventHandler SelectedRouteChanged;

        /// <summary>Raised whenever <see cref="SelectedPath"/> changes.</summary>
        public event EventHandler SelectedPathChanged;

        /// <summary>Raised whenever <see cref="Enabled"/> changes.</summary>
        public event EventHandler EnabledChanged;

        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value)
                    return;
                enabled = value;
                EnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        void IToolboxMenu.PopulateContentFolders(ImmutableArray<FolderModel> folders)
        {
            ContentFolders = folders.IsDefault ? ImmutableArray<FolderModel>.Empty : folders;
            ContentFoldersChanged?.Invoke(this, EventArgs.Empty);
        }

        void IToolboxMenu.PopulateRoutes(ImmutableArray<RouteModelHeader> routes)
        {
            Routes = routes.IsDefault ? ImmutableArray<RouteModelHeader>.Empty : routes;
            RoutesChanged?.Invoke(this, EventArgs.Empty);
        }

        void IToolboxMenu.PopulatePaths(ImmutableArray<PathModelHeader> paths)
        {
            Paths = paths.IsDefault ? ImmutableArray<PathModelHeader>.Empty : paths;
            PathsChanged?.Invoke(this, EventArgs.Empty);
        }

        void IToolboxMenu.ClearPathMenu()
        {
            Paths = ImmutableArray<PathModelHeader>.Empty;
            SelectedPath = null;
            PathsChanged?.Invoke(this, EventArgs.Empty);
            SelectedPathChanged?.Invoke(this, EventArgs.Empty);
        }

        void IToolboxMenu.PreSelectRoute(string routeName)
        {
            SelectedRouteName = routeName;
            SelectedRouteChanged?.Invoke(this, EventArgs.Empty);
        }

        void IToolboxMenu.PreSelectPath(PathModelHeader path)
        {
            SelectedPath = path;
            SelectedPathChanged?.Invoke(this, EventArgs.Empty);
        }

        FolderModel IToolboxMenu.SelectContentFolder(string folderName)
        {
            foreach (FolderModel folder in ContentFolders)
            {
                if (folder.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    SelectFolder(folder);
                    return folder;
                }
            }
            return null;
        }

        #endregion

        #region Commands (in from the WPF UI thread, marshaled to the game thread)

        /// <summary>Selects a content folder and loads its routes.</summary>
        public void SelectFolder(FolderModel folder)
        {
            ArgumentNullException.ThrowIfNull(folder);

            // Raise the change notification asynchronously on the game thread, symmetric with the route path
            // (PopulateRoutes/PreSelectRoute). Raising it synchronously here re-enters the WPF ComboBox while
            // it is still committing the user's pick, which makes the Selector revert the selection (the
            // folder dropdown appears to ignore the choice).
            InvokeOnGameThreadAsync($"Select content folder '{folder.Name}'", async () =>
            {
                SelectedFolder = folder;
                SelectedFolderChanged?.Invoke(this, EventArgs.Empty);

                game.UnloadRoute();
                ((IToolboxMenu)this).PopulateRoutes(await game.FindRoutes(folder).ConfigureAwait(true));
            });
        }

        /// <summary>Loads the given route, or unloads it when it is already selected.</summary>
        public void ToggleRoute(RouteModelHeader route)
        {
            ArgumentNullException.ThrowIfNull(route);

            InvokeOnGameThreadAsync($"Toggle route '{route.Name}'", async () =>
            {
                if (string.Equals(SelectedRouteName, route.Name, StringComparison.Ordinal))
                {
                    game.UnloadRoute();
                    ((IToolboxMenu)this).PreSelectRoute(null);
                }
                else
                {
                    await game.LoadRoute(route).ConfigureAwait(true);
                    ((IToolboxMenu)this).PreSelectRoute(route.Name);
                }
            });
        }

        /// <summary>Loads the given path, or unloads it when it is already selected.</summary>
        public void TogglePath(PathModelHeader path)
        {
            ArgumentNullException.ThrowIfNull(path);
            game.InvokeOnGameThread(() =>
            {
                if (SelectedPath != null && string.Equals(SelectedPath.Id, path.Id, StringComparison.OrdinalIgnoreCase))
                {
                    game.UnloadPath();
                    ((IToolboxMenu)this).PreSelectPath(null);
                }
                else if (game.LoadPath(path))
                {
                    ((IToolboxMenu)this).PreSelectPath(path);
                }
            });
        }

        /// <summary>Starts editing a new path for the loaded route.</summary>
        public void EditPath() => game.InvokeOnGameThread(game.EditPath);

        /// <summary>Saves the currently edited path.</summary>
        public void SavePath() => game.InvokeOnGameThread(game.SavePath);

        /// <summary>Takes a screenshot of the current map view.</summary>
        public void TakeScreenshot() => game.InvokeOnGameThread(game.PrintScreen);

        /// <summary>Shows the about window.</summary>
        public void ShowAbout() => game.InvokeOnGameThread(game.ShowAboutWindow);

        /// <summary>Requests application exit.</summary>
        public void Quit() => game.InvokeOnGameThread(game.PrepareExitApplication);

        /// <summary>Updates a color preference.</summary>
        public void UpdateColorPreference(ColorSetting setting, string colorName)
            => game.InvokeOnGameThread(() => game.UpdateColorPreference(setting, colorName));

        /// <summary>Updates an item visibility preference.</summary>
        public void UpdateItemVisibilityPreference(MapContentType setting, bool visible)
            => game.InvokeOnGameThread(() => game.UpdateItemVisibilityPreference(setting, visible));

        /// <summary>Updates the language preference.</summary>
        public void UpdateLanguagePreference(string language)
            => game.InvokeOnGameThread(() => game.UpdateLanguagePreference(language));

        private void InvokeOnGameThreadAsync(string operationName, Func<Task> action)
        {
            _ = game.InvokeOnGameThreadAsync(async () =>
            {
                try
                {
                    await action().ConfigureAwait(true);
                }
                catch (OperationCanceledException ex)
                {
                    Trace.TraceInformation($"Hosted menu operation canceled: {operationName}. {ex.Message}");
                    throw;
                }
                catch (InvalidOperationException ex)
                {
                    Trace.TraceError($"Hosted menu operation failed: {operationName}. {ex}");
                    throw;
                }
                catch (IOException ex)
                {
                    Trace.TraceError($"Hosted menu operation failed: {operationName}. {ex}");
                    throw;
                }
            });
        }

        #endregion
    }
}
