using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    /// <summary>
    /// Transports captured session publications and scoped commands between the two owners.
    /// Notifications carry their payload; subscribers never reread changing individual properties.
    /// </summary>
    internal sealed class HostedToolboxMenu
    {
        private readonly GameWindow game;
        private ToolboxMenuSnapshot current;

        internal HostedToolboxMenu(GameWindow game)
        {
            ArgumentNullException.ThrowIfNull(game);
            this.game = game;
        }

        /// <summary>Subscribe before reading this value; revision checks make the handoff lossless.</summary>
        public ToolboxMenuSnapshot Current => Volatile.Read(ref current);

        /// <summary>Raised on the game owner with the captured immutable payload.</summary>
        public event EventHandler<ToolboxMenuSnapshot> SnapshotChanged;

        internal void Publish(ToolboxMenuSnapshot snapshot)
        {
            ToolboxMenuSnapshot previous = Current;
            if (previous != null && (previous.SessionId != snapshot.SessionId || previous.Revision >= snapshot.Revision))
                return;
            Volatile.Write(ref current, snapshot);
            SnapshotChanged?.Invoke(this, snapshot);
        }

        #region Commands (in from the WPF UI thread, marshaled to the game thread)

        /// <summary>Selects a content folder and loads its routes.</summary>
        public void SelectFolder(ToolboxMenuCommand command) => InvokeOnGameThreadAsync("Select content folder", () => game.Session.SelectFolderAsync(command));

        /// <summary>Loads the given route, or unloads it when it is already selected.</summary>
        public void ToggleRoute(ToolboxMenuCommand command) => InvokeOnGameThreadAsync("Toggle route", () => game.Session.ToggleRouteAsync(command));

        /// <summary>Loads the given path, or unloads it when it is already selected.</summary>
        public void TogglePath(ToolboxMenuCommand command) => InvokeOnGameThreadAsync("Toggle path", () => game.Session.TogglePathAsync(command));

        /// <summary>Starts editing a new path for the loaded route.</summary>
        public void EditPath(ToolboxMenuCommand command) => InvokeOnGameThreadAsync("Edit path", () => game.Session.EditMenuPathAsync(command));

        /// <summary>Saves the currently edited path.</summary>
        public void SavePath(ToolboxMenuCommand command) => InvokeOnGameThreadAsync("Save path", () => game.Session.SaveMenuPathAsync(command));

        /// <summary>Takes a screenshot of the current map view.</summary>
        public void TakeScreenshot() => game.InvokeOnGameThread(game.PrintScreen);

        /// <summary>Shows the about window.</summary>
        public void ShowAbout() => game.InvokeSessionCommand(game.ShowAboutWindow);

        /// <summary>Requests application exit.</summary>
        public void Quit() => game.InvokeOnGameThread(game.PrepareExitApplication);

        /// <summary>Updates a color preference.</summary>
        public void UpdateColorPreference(ColorSetting setting, string colorName) => game.InvokeSessionCommand(() => game.UpdateColorPreference(setting, colorName));

        /// <summary>Updates an item visibility preference.</summary>
        public void UpdateItemVisibilityPreference(MapContentType setting, bool visible) => game.InvokeSessionCommand(() => game.UpdateItemVisibilityPreference(setting, visible));

        /// <summary>Updates the language preference.</summary>
        public void UpdateLanguagePreference(string language) => game.InvokeSessionCommand(() => game.UpdateLanguagePreference(language));

        private void InvokeOnGameThreadAsync(string operationName, Func<Task> action)
        {
            GameWindow.ObserveGameThreadOperation(game.InvokeOnGameThreadAsync(async () =>
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
            }));
        }

        #endregion
    }
}
