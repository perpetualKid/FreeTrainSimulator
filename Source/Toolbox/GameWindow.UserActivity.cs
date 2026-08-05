using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Toolbox
{
    public partial class GameWindow : Game
    {
        #region public declarations


        #endregion

        #region private declarations
        private static readonly Vector2 moveLeft = new Vector2(1, 0);
        private static readonly Vector2 moveRight = new Vector2(-1, 0);
        private static readonly Vector2 moveUp = new Vector2(0, 1);
        private static readonly Vector2 moveDown = new Vector2(0, -1);

        // Set while the left button is held and the pointer has moved, i.e. the interaction is a pan drag
        // rather than a click. Reset on every left button press.
        private bool pointerDraggedSinceLeftPress;

        #endregion

        private const int zoomAmplifier = 3;

        // Pointer radius, in screen pixels, used to hit test path nodes on the map surface.
        private const int nodeHitTestRadiusPixels = 10;

        // Wait time applied when marking a wait point from the map context menu.
        private const int defaultWaitTimeSeconds = 60;

        /// <summary>
        /// Raised on the game thread when the user requests the map context menu. The WPF shell re-raises this
        /// on its dispatcher and shows the menu.
        /// </summary>
        internal event EventHandler<MapContextMenuRequestedEventArgs> MapContextMenuRequested;

        public void ChangeScreenMode()
        {
            SetScreenMode(currentScreenMode.Next());
        }

        public void CloseWindow()
        {
            PrepareExitApplication();
        }

        internal void PrepareExitApplication()
        {
            // The WPF shell owns the exit confirmation dialog; raise an event so it can prompt and then close
            // the window.
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            if (userCommandArgs is PointerMoveCommandArgs mouseMoveCommandArgs && contentArea is IMapHostControl hostControl)
            {
                pointerDraggedSinceLeftPress = true;
                hostControl.UpdatePosition(mouseMoveCommandArgs.Delta);
            }
        }

        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (userCommandArgs is ScrollCommandArgs mouseWheelCommandArgs && contentArea is IMapHostControl hostControl)
            {
                hostControl.UpdateScaleAt(mouseWheelCommandArgs.Position, Math.Sign(mouseWheelCommandArgs.Delta) * ZoomAmplifier(modifiers));
            }
        }

        // Right click on the map surface: hit test the current path and ask the shell to show the context menu.
        // The hit test cascades node -> span -> map, so path-scoped actions stay reachable on empty map.
        internal void RequestMapContextMenu(UserCommandArgs userCommandArgs)
        {
            if (userCommandArgs is not PointerCommandArgs pointerCommandArgs)
                return;

            PathEditor editor = pathEditor;
            ContentArea content = contentArea;
            if (editor == null || content == null)
                return;

            PointD location = content.ScreenToWorldCoordinates(pointerCommandArgs.Position);
            double tolerance = content.Scale > 0
                ? nodeHitTestRadiusPixels / content.Scale
                : 0;

            MapContextMenuActionBuilder.MapContextMenuState state = new MapContextMenuActionBuilder.MapContextMenuState
            {
                IsMovingNode = editor.IsMovingNode,
                CanUndo = editor.CanUndo,
                CanRedo = editor.CanRedo,
                CanExtendPath = editor.CanExtendPath,
                CanReResolvePath = editor.TrainPath != null,
                CanSavePath = hostedTrainPathToolWindow?.CanSavePath == true,
                CanStartNewPath = hostedTrainPathToolWindow?.CanCreatePath == true,
            };

            ImmutableArray<MapContextMenuItem> items;
            if (editor.TryGetPathNodeAt(location, tolerance, out int nodeIndex))
            {
                items = MapContextMenuActionBuilder.BuildForNode(
                    editor.TrainPath?.PathPoints[nodeIndex], nodeIndex, editor.CanMoveNode(nodeIndex), state);
            }
            else if (editor.TryGetPathSpanAt(location, tolerance, out int fromNodeIndex))
            {
                items = MapContextMenuActionBuilder.BuildForSpan(fromNodeIndex, editor.GetSpanCandidates(fromNodeIndex), state);
            }
            else
            {
                items = MapContextMenuActionBuilder.BuildForMap(state);
                nodeIndex = -1;
            }

            if (items.IsDefaultOrEmpty)
                return;

            MapContextMenuRequested?.Invoke(this, new MapContextMenuRequestedEventArgs(
                pointerCommandArgs.Position.X, pointerCommandArgs.Position.Y, nodeIndex, items));
        }

        // Left button press on the map surface: starts a new pointer interaction, which turns into a pan drag
        // only if the pointer subsequently moves.
        internal void BeginPointerInteraction(UserCommandArgs userCommandArgs)
        {
            pointerDraggedSinceLeftPress = false;
        }

        // Left button release on the map surface commits an in-progress node move to the previewed target
        // location.
        //
        // NOTE: this deliberately commits on release rather than on press, and only when the interaction was a
        // click rather than a pan drag; committing on press would consume the button-down that starts a pan and
        // move the node to whatever was under the pointer at that moment. If a future interaction model makes
        // panning and node moving mutually exclusive (for example an explicit move mode that disables panning),
        // this can be simplified back to a plain CommonUserCommand.PointerPressed handler without the
        // pointerDraggedSinceLeftPress guard.
        internal void CommitPendingNodeMove(UserCommandArgs userCommandArgs)
        {
            bool dragged = pointerDraggedSinceLeftPress;
            pointerDraggedSinceLeftPress = false;

            PathEditor editor = pathEditor;
            if (dragged || editor == null || !editor.CanCommitMoveNode)
                return;

            PathEditorCommandResult result = editor.CommitMoveNodeCommand();
            if (!result.Success)
                Trace.TraceWarning(result.Message);

            if (userCommandArgs != null)
                userCommandArgs.Handled = true;
        }

        // Cancels an in-progress node move, leaving the path unchanged.
        internal void CancelPendingNodeMove(UserCommandArgs userCommandArgs)
        {
            PathEditor editor = pathEditor;
            if (editor == null || !editor.IsMovingNode)
                return;

            _ = editor.CancelMoveNodeCommand();

            if (userCommandArgs != null)
                userCommandArgs.Handled = true;
        }

        /// <summary>
        /// Applies a node-related action selected from the map context menu. Routed through the train path tool
        /// window rather than the path editor directly, so its node list, status message and dirty state stay in
        /// sync with edits started on the map.
        /// </summary>
        internal void ExecuteMapContextMenuAction(MapContextMenuAction action, int nodeIndex, int candidateIndex)
        {
            TrainPathToolWindow toolWindow = hostedTrainPathToolWindow;
            if (toolWindow == null)
                return;

            switch (action)
            {
                case MapContextMenuAction.MoveNode:
                    toolWindow.BeginMoveNode(nodeIndex);
                    break;
                case MapContextMenuAction.CancelMoveNode:
                    toolWindow.CancelMoveNode();
                    break;
                case MapContextMenuAction.AddViaPoint:
                    toolWindow.AddViaPoint(nodeIndex);
                    break;
                case MapContextMenuAction.RemoveViaPoint:
                    toolWindow.RemoveViaPoint(nodeIndex);
                    break;
                case MapContextMenuAction.SetWaitPoint:
                    // The tool window owns the configurable wait time; the map menu applies a default which can
                    // then be fine-tuned there.
                    toolWindow.SetWaitPoint(nodeIndex, defaultWaitTimeSeconds);
                    break;
                case MapContextMenuAction.ClearWaitPoint:
                    toolWindow.ClearWaitPoint(nodeIndex);
                    break;
                case MapContextMenuAction.SetReversalPoint:
                    toolWindow.SetReversalPoint(nodeIndex);
                    break;
                case MapContextMenuAction.ClearReversalPoint:
                    toolWindow.ClearReversalPoint(nodeIndex);
                    break;
                case MapContextMenuAction.RepairNode:
                    toolWindow.RepairSelectedNode(nodeIndex);
                    break;
                case MapContextMenuAction.RemoveRestOfPath:
                    toolWindow.RemoveRestOfPath(nodeIndex);
                    break;
                case MapContextMenuAction.SelectRouteCandidate:
                    toolWindow.AcceptRouteCandidate(nodeIndex, candidateIndex);
                    break;
                case MapContextMenuAction.ExtendPath:
                    toolWindow.ExtendPath();
                    break;
                case MapContextMenuAction.ReResolvePath:
                    toolWindow.SnapToTrack();
                    break;
                case MapContextMenuAction.StartNewPath:
                    toolWindow.CreatePath();
                    break;
                case MapContextMenuAction.SavePath:
                    toolWindow.SavePath();
                    break;
                case MapContextMenuAction.Undo:
                    toolWindow.Undo();
                    break;
                case MapContextMenuAction.Redo:
                    toolWindow.Redo();
                    break;
                default:
                    Trace.TraceWarning($"Unsupported map context menu action {action}.");
                    break;
            }
        }

        private void MoveByKeyLeft(UserCommandArgs commandArgs)
        {
            if (contentArea is IMapHostControl hostControl)
                hostControl.UpdatePosition(moveLeft * MovementAmplifier(commandArgs));
        }

        private void MoveByKeyRight(UserCommandArgs commandArgs)
        {
            if (contentArea is IMapHostControl hostControl)
                hostControl.UpdatePosition(moveRight * MovementAmplifier(commandArgs));
        }

        private void MoveByKeyUp(UserCommandArgs commandArgs)
        {
            if (contentArea is IMapHostControl hostControl)
                hostControl.UpdatePosition(moveUp * MovementAmplifier(commandArgs));
        }

        private void MoveByKeyDown(UserCommandArgs commandArgs)
        {
            if (contentArea is IMapHostControl hostControl)
                hostControl.UpdatePosition(moveDown * MovementAmplifier(commandArgs));
        }

        private static int MovementAmplifier(UserCommandArgs commandArgs)
        {
            int amplifier = 5;
            if (commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand)
            {
                if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Control) == KeyModifiers.Control)
                    amplifier = 1;
                else if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                    amplifier = 10;
            }
            return amplifier;
        }

        private static int ZoomAmplifier(KeyModifiers modifiers)
        {
            int amplifier = zoomAmplifier;
            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
                amplifier = 1;
            else if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                amplifier = 5;
            return amplifier;
        }

        private static int ZoomAmplifier(UserCommandArgs commandArgs)
        {
            return commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand ? ZoomAmplifier(modifiableKeyCommand.AdditionalModifiers) : zoomAmplifier;
        }

        private void ZoomIn(UserCommandArgs commandArgs)
        {
            Zoom(ZoomAmplifier(commandArgs));
        }

        private void ZoomOut(UserCommandArgs commandArgs)
        {
            Zoom(-ZoomAmplifier(commandArgs));
        }

        private long nextUpdate;
        private void Zoom(int steps)
        {
            if (Environment.TickCount64 > nextUpdate && contentArea is IMapHostControl hostControl)
            {
                hostControl.UpdateScale(steps);
                nextUpdate = Environment.TickCount64 + 30;
            }
        }

        private void ResetZoomAndLocation()
        {
            if (contentArea is IMapHostControl hostControl)
                hostControl.ResetSize(Window.ClientBounds.Size, 60);
        }

        internal void ShowAboutWindow()
        {
            // The WPF shell shows its own About dialog; raise an event for it.
            AboutRequested?.Invoke(this, EventArgs.Empty);
        }

        // Raised on the game thread in hosted mode when the user requests the About dialog, so the WPF shell
        // can show its own modal dialog instead of the legacy MonoGame popup.
        internal event EventHandler AboutRequested;

        // Raised on the game thread in hosted mode when the user requests application exit (menu or quit
        // command), so the WPF shell can show its own confirmation and drive window close.
        internal event EventHandler ExitRequested;

        // Raised on the game thread in hosted mode when the user requests a screenshot, so the WPF shell can
        // show an owned save dialog and submit the chosen file path back to the game thread for capture.
        internal event EventHandler ScreenshotRequested;

        // Raised on the game thread whenever the active language/catalog changes (initial load and any later
        // language switch), so the WPF shell can re-localize its own chrome and dialogs against the same
        // gettext catalog.
        internal event EventHandler LanguageChanged;

        internal void RaiseLanguageChanged() => LanguageChanged?.Invoke(this, EventArgs.Empty);

        internal void PrintScreen()
        {
            if (hostedReattachCallback != null)
            {
                ScreenshotRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.DefaultExt = "png";
                dialog.FileName = $"{RuntimeInfo.ApplicationName} {DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss", CultureInfo.CurrentCulture)}";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                dialog.Filter = $"{Catalog.GetString("Image files (*.png)")}|*.png";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SaveScreenshot(dialog.FileName);
                }
            }
        }

        internal Task SaveScreenshotAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Screenshot file name must not be empty.", nameof(fileName));

            SaveScreenshot(fileName);
            return Task.CompletedTask;
        }

        private void SaveScreenshot(string fileName)
        {
            byte[] backBuffer = new byte[graphicsDeviceManager.PreferredBackBufferWidth * graphicsDeviceManager.PreferredBackBufferHeight * 4];
            GraphicsDevice graphicsDevice = graphicsDeviceManager.GraphicsDevice;
            using (RenderTarget2D screenshot = new RenderTarget2D(graphicsDevice, graphicsDeviceManager.PreferredBackBufferWidth, graphicsDeviceManager.PreferredBackBufferHeight, false, graphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.None))
            {
                graphicsDevice.GetBackBufferData(backBuffer);
                screenshot.SetData(backBuffer);
                using (FileStream stream = File.OpenWrite(fileName))
                {
                    screenshot.SaveAsPng(stream, graphicsDeviceManager.PreferredBackBufferWidth, graphicsDeviceManager.PreferredBackBufferHeight);
                }
            }
        }

        // Persists the given path metadata through the path editor and refreshes the menu's path list. Runs on
        // the game thread; callers can await completion and observe traced failures instead of relying on
        // async-void exception dispatch.
        internal async Task SubmitTrainPathSaveAsync(PathModelHeader pathDetails)
        {
            ArgumentNullException.ThrowIfNull(pathDetails);

            PathEditor editor = pathEditor;
            if (editor == null)
            {
                Trace.TraceWarning("Cannot save train path because no path editor is active.");
                return;
            }

            RouteModelHeader route = selectedRoute;
            if (route == null)
            {
                Trace.TraceWarning("Cannot save train path because no route is selected.");
                return;
            }

            try
            {
                await editor.SavePath(pathDetails).ConfigureAwait(true);
                ImmutableArray<PathModelHeader> paths = await route.GetRoutePaths(ctsProfileLoading?.Token ?? CancellationToken.None).ConfigureAwait(true);
                menu.PopulatePaths(paths);
                hostedTrainPathToolWindow?.UpdatePaths(paths);
            }
            catch (OperationCanceledException ex)
            {
                Trace.TraceInformation($"Train path save was canceled: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceError($"Failed to save train path: {ex}");
            }
            catch (IOException ex)
            {
                Trace.TraceError($"Failed to save train path: {ex}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.TraceError($"Failed to save train path: {ex}");
            }
        }


    }
}
