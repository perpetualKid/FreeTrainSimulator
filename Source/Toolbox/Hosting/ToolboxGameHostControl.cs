using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using System.Windows.Threading;

using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.Settings;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    public sealed class ToolboxGameHostControl : WindowsFormsHost
    {
        private readonly HostPanel hostPanel;
        private readonly Lock syncLock = new Lock();
        private readonly TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource gameThreadCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool ownerResourcesReleased;

        private Thread gameThread;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
            Justification = "GameThreadStart owns and disposes the game in a using block on its STA after draining the session; this field only publishes the instance.")]
        private volatile GameWindow gameWindow;
        private Task stopTask;
        private volatile bool stopping;
        private bool hostedWindowAttached;
        private bool hostedWindowAttaching;
        private IntPtr hostPanelHandle;
        private bool disposed;
        private bool geometryMeasurementPending;
        private HostedViewportGeometry? queuedGeometry;
        private System.Windows.Window mainWindow;

        /// <summary>
        /// Grouped hosted-mode bridge set published by the hosted game window. Null until available.
        /// </summary>
        internal HostedToolboxServices HostedServices { get; private set; }

        /// <summary>The hosted game's loaded profile settings, available with its tool-window bridges.</summary>
        internal ProfileToolboxSettingsModel ToolboxSettings => gameWindow?.ToolboxSettings;

        /// <summary>True only after the owner has released its resources and finished, regardless of diagnostics.</summary>
        internal bool IsStoppedSafely => gameThreadCompletion.Task.IsCompleted && ownerResourcesReleased;

        /// <summary>
        /// Raised on the WPF UI thread once <see cref="HostedToolboxServices.Menu"/> becomes available.
        /// </summary>
        internal event EventHandler HostedMenuReady;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted game requests a train-path save, so the shell can show
        /// its WPF modal save dialog. Respond by collecting the path metadata and calling
        /// <see cref="SubmitSavePath"/>.
        /// </summary>
        internal event EventHandler SaveTrainPathRequested;

        internal event EventHandler<UnsavedPathConfirmationEventArgs> UnsavedPathConfirmationRequested;

        /// <summary>
        /// Raised on the WPF UI thread once hosted tool-window bridges become available.
        /// </summary>
        internal event EventHandler HostedToolWindowsReady;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted child window receives a mouse-down notification.
        /// Useful for input-capture handoff back to the map view.
        /// </summary>
        internal event EventHandler HostedWindowPointerDown;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted game requests the map context menu, after it has hit
        /// tested the pointer position against the current train path.
        /// </summary>
        internal event EventHandler<MapContextMenuRequestedEventArgs> MapContextMenuRequested;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted game requests a screenshot save location.
        /// </summary>
        internal event EventHandler ScreenshotRequested;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted game requests the About dialog.
        /// </summary>
        internal event EventHandler AboutRequested;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted game requests application exit.
        /// </summary>
        internal event EventHandler ExitRequested;

        /// <summary>
        /// Raised asynchronously on the WPF UI thread for a native map tool-window shortcut.
        /// </summary>
        internal event EventHandler<ToolWindowShortcutRequestedEventArgs> ToolWindowShortcutRequested;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted game's active language/catalog changes, so the shell
        /// can re-localize its own chrome and dialogs against the shared gettext catalog.
        /// </summary>
        internal event EventHandler LanguageChanged;

        /// <summary>
        /// Name of the UI culture the hosted game's active catalog was resolved for, or null until the hosted
        /// game is available. The shell uses this to set the same culture on its UI thread before pulling the
        /// shared catalog.
        /// </summary>
        internal string CurrentLanguage => gameWindow?.CurrentLanguage;

        public ToolboxGameHostControl()
        {
            hostPanel = new HostPanel(OnHostedWindowPointerDown)
            {
                Dock = DockStyle.Fill,
                TabStop = true,
            };

            Child = hostPanel;
            Loaded += HostGeometryChanged;
            SizeChanged += HostGeometryChanged;
            LayoutUpdated += HostGeometryChanged;
            hostPanel.ClientSizeChanged += HostGeometryChanged;

            StartHostedGame();
        }

        private void HostGeometryChanged(object sender, EventArgs e)
        {
            QueueHostedGeometryMeasurement();
        }

        protected override void OnWindowPositionChanged(System.Windows.Rect rcBoundingBox)
        {
            base.OnWindowPositionChanged(rcBoundingBox);
            QueueHostedGeometryMeasurement();
        }

        private void QueueHostedGeometryMeasurement()
        {
            if (disposed || hostPanel == null || !IsLoaded || geometryMeasurementPending
                || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            geometryMeasurementPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(MeasureHostedGeometry));
        }

        private void ObserveMainWindow()
        {
            System.Windows.Window window = System.Windows.Application.Current?.MainWindow;
            if (mainWindow == window)
                return;

            if (mainWindow != null)
            {
                mainWindow.LocationChanged -= HostGeometryChanged;
                mainWindow.SizeChanged -= HostGeometryChanged;
                mainWindow.StateChanged -= HostGeometryChanged;
            }

            mainWindow = window;
            if (mainWindow != null)
            {
                mainWindow.LocationChanged += HostGeometryChanged;
                mainWindow.SizeChanged += HostGeometryChanged;
                mainWindow.StateChanged += HostGeometryChanged;
            }
        }

        private void StartHostedGame()
        {
            lock (syncLock)
            {
                if (disposed)
                    return;

                if (gameThread != null)
                    return;

                gameThread = new Thread(GameThreadStart)
                {
                    IsBackground = true,
                    Name = "Toolbox.HostedGameThread",
                };
                gameThread.SetApartmentState(ApartmentState.STA);
                GameWindow.ObserveGameThreadOperation(ready.Task);
                GameWindow.ObserveGameThreadOperation(gameThreadCompletion.Task);
                try
                {
                    gameThread.Start();
                }
                catch (SystemException exception)
                {
                    // The owner never started, so no game or child resources were created.
                    ownerResourcesReleased = true;
                    ready.TrySetException(exception);
                    gameThreadCompletion.TrySetException(exception);
                    throw;
                }
            }
        }

        private void GameThreadStart()
        {
            Exception failure = null;
            try
            {
                using (GameWindow game = new GameWindow())
                {
                    gameWindow = game;
                    game.EnableHostedMode(ReattachHostedWindow, UpdateHostWindowTitle);
                    game.HostedReady += Game_HostedReady;
                    try
                    {
                        game.Run();
                    }
                    catch (Exception ex) when (ex is not ThreadAbortException)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        game.HostedReady -= Game_HostedReady;
                        try
                        {
                            DrainSessionOnOwner(game);
                        }
                        catch (Exception ex) when (ex is not ThreadAbortException)
                        {
                            failure = failure == null ? ex : new AggregateException(failure, ex);
                        }
                    }
                }
                // Run/drain diagnostics do not negate successful owner-thread resource disposal.
                ownerResourcesReleased = true;
            }
            catch (Exception ex) when (ex is not ThreadAbortException)
            {
                failure = failure == null ? ex : new AggregateException(failure, ex);
            }
            finally
            {
                gameWindow = null;
                ready.TrySetException(failure ?? new InvalidOperationException("The hosted game stopped before becoming ready."));
                // This is deliberately after Run AND the using block's owner-thread disposal.
                if (failure == null)
                    gameThreadCompletion.TrySetResult();
                else
                {
                    System.Diagnostics.Trace.TraceError($"[ToolboxGameHost] Hosted game thread failed: {failure}");
                    gameThreadCompletion.TrySetException(failure);
                }
            }
        }

        /// <summary>Drains and releases the session on its STA after MonoGame has destroyed the render form.</summary>
        internal static void DrainSessionOnOwner(GameWindow game)
        {
            Task drain = game.Session.StopAndDrainAsync();
            if (!drain.IsCompleted)
            {
                using ApplicationContext context = new ApplicationContext();
                // Both successful and failed drains must end the fallback loop through the live owner HWND.
                _ = drain.ContinueWith(_ => game.InvokeOnGameThread(context.ExitThread),
                    CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                Application.Run(context);
            }
            try
            {
                drain.GetAwaiter().GetResult();
            }
            finally
            {
                // Faulted jobs have also finished borrowing resources. Release them before reporting failure.
                try
                {
                    game.Session.Dispose();
                }
                catch (Exception cleanupFailure) when (drain.IsFaulted)
                {
                    throw new AggregateException(drain.Exception.InnerExceptions.Append(cleanupFailure));
                }
            }
        }

        private void Game_HostedReady(object sender, EventArgs e)
        {
            ready.TrySetResult((GameWindow)sender);
            if (!stopping && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    AttachHostedWindow();
                    PublishHostedBridges();
                }));
        }

        /// <summary>Shares shutdown completion; both owners must keep pumping until this task completes.</summary>
        internal Task StopAsync()
        {
            lock (syncLock)
            {
                stopping = true;
                return stopTask ??= StopCoreAsync();
            }
        }

        private async Task StopCoreAsync()
        {
            await Task.WhenAny(ready.Task, gameThreadCompletion.Task).ConfigureAwait(false);
            if (gameThreadCompletion.Task.IsCompleted || !ready.Task.IsCompletedSuccessfully)
            {
                await gameThreadCompletion.Task.ConfigureAwait(false);
                await ready.Task.ConfigureAwait(false);
                return;
            }

            GameWindow game = await ready.Task.ConfigureAwait(false);
            Task stop = game.StopForExitAsync();
            GameWindow.ObserveGameThreadOperation(stop);
            await Task.WhenAny(stop, gameThreadCompletion.Task).ConfigureAwait(false);
            // An unsuccessful resource release can leave the loop alive: report it without claiming safety.
            // A finished job failure with a released session must still await actual owner teardown.
            if (!gameThreadCompletion.Task.IsCompleted && !game.Session.IsDisposed)
                await stop.ConfigureAwait(false);
            await gameThreadCompletion.Task.ConfigureAwait(false);
            await stop.ConfigureAwait(false);
        }

        // Initialization publishes once. Failed/stopping startups never leave dispatcher retry callbacks.
        private void PublishHostedBridges()
        {
            if (disposed || stopping || gameThreadCompletion.Task.IsCompleted)
                return;

            GameWindow game = gameWindow;
            HostedToolboxServices services = game?.HostedServices;
            if (services?.Menu == null)
                return;

            HostedServices = services;
            HostedMenuReady?.Invoke(this, EventArgs.Empty);

            game.SaveTrainPathRequested -= Game_SaveTrainPathRequested;
            game.SaveTrainPathRequested += Game_SaveTrainPathRequested;
            game.UnsavedPathConfirmationRequested -= Game_UnsavedPathConfirmationRequested;
            game.UnsavedPathConfirmationRequested += Game_UnsavedPathConfirmationRequested;
            game.ScreenshotRequested -= Game_ScreenshotRequested;
            game.ScreenshotRequested += Game_ScreenshotRequested;
            game.AboutRequested -= Game_AboutRequested;
            game.AboutRequested += Game_AboutRequested;
            game.ExitRequested -= Game_ExitRequested;
            game.ExitRequested += Game_ExitRequested;
            game.LanguageChanged -= Game_LanguageChanged;
            game.LanguageChanged += Game_LanguageChanged;
            game.MapContextMenuRequested -= Game_MapContextMenuRequested;
            game.MapContextMenuRequested += Game_MapContextMenuRequested;
            game.ToolWindowShortcutRequested -= Game_ToolWindowShortcutRequested;
            game.ToolWindowShortcutRequested += Game_ToolWindowShortcutRequested;
            HostedToolWindowsReady?.Invoke(this, EventArgs.Empty);

            // The hosted game already loaded its language during construction (before the shell subscribed), so
            // raise an initial notification here to drive the shell's first localization pass.
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Game_UnsavedPathConfirmationRequested(object sender, UnsavedPathConfirmationEventArgs e)
        {
            if (e.Completion.Task.IsCompleted)
                return;

            Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                new Action(() =>
                {
                    if (!e.Completion.Task.IsCompleted)
                        UnsavedPathConfirmationRequested?.Invoke(this, e);
                }));
        }

        private void Game_ToolWindowShortcutRequested(object sender, ToolWindowShortcutRequestedEventArgs e)
        {
            if (disposed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (!disposed)
                    ToolWindowShortcutRequested?.Invoke(this, e);
            }));
        }

        private void AttachHostedWindow()
        {
            if (disposed || stopping || !IsLoaded || hostPanel.IsDisposed || !hostPanel.IsHandleCreated
                || hostedWindowAttached || hostedWindowAttaching)
                return;

            GameWindow game = gameWindow;
            
            if (game == null)
                return;

            hostedWindowAttaching = true;
            try
            {
                IntPtr windowHandle = game.HostedWindowHandle;
                if (windowHandle == IntPtr.Zero)
                    return;

                // Cache the host panel handle so the game thread can reparent the child window without touching
                // the WinForms Control across threads.
                hostPanelHandle = hostPanel.Handle;

                // SetParent is a cross-thread operation (the parent is owned by the WPF UI thread), so it must
                // only run here, at initial attach, while the WPF thread is pumping messages. It is never called
                // again on resize.
                NativeMethods.SetParent(windowHandle, hostPanelHandle);
                ConfigureChildWindow(windowHandle, hostPanel.ClientSize.Width, hostPanel.ClientSize.Height);

                hostedWindowAttached = true;
            }
            finally
            {
                hostedWindowAttaching = false;
            }

            QueueHostedGeometryMeasurement();
        }

        // Invoked on the game thread (the child window's owning thread) right after a hosted resize applies.
        // Re-asserting the WS_CHILD style and size in-thread is safe because both the child window and these
        // APIs target the game-thread-owned window. It deliberately does NOT call SetParent: ApplyChanges()
        // only strips the window style, it does not change the parent, and calling the cross-thread SetParent
        // here would block the game loop (freezing rendering and input) whenever the WPF thread is not pumping.
        private void ReattachHostedWindow(IntPtr windowHandle, int width, int height)
        {
            if (windowHandle == IntPtr.Zero || hostPanelHandle == IntPtr.Zero)
                return;

            ConfigureChildWindow(windowHandle, width, height);
        }

        // Re-asserts the WS_CHILD style and child window size. MonoGame WindowsDX re-applies its own
        // top-level window styles when the back buffer size changes (via graphicsDeviceManager.ApplyChanges),
        // which strips WS_CHILD; this restores it. All calls here target the child window owned by the
        // calling (game) thread, so no cross-thread blocking occurs. Parenting (SetParent) is established
        // once in AttachHostedWindow and is not repeated here.
        private static void ConfigureChildWindow(IntPtr windowHandle, int width, int height)
        {
            long styleValue = NativeMethods.GetWindowStyle(windowHandle).ToInt64();
            styleValue &= ~(long)(NativeMethods.WsPopup | NativeMethods.WsCaption | NativeMethods.WsThickFrame | NativeMethods.WsMinimize | NativeMethods.WsMaximize | NativeMethods.WsSysMenu);
            styleValue |= NativeMethods.WsChild | NativeMethods.WsVisible;
            NativeMethods.SetWindowStyle(windowHandle, new IntPtr(styleValue));

            _ = NativeMethods.SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, Math.Max(1, width), Math.Max(1, height),
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
        }

        private void MeasureHostedGeometry()
        {
            geometryMeasurementPending = false;
            if (disposed || !IsLoaded || hostPanel.IsDisposed || !hostPanel.IsHandleCreated)
                return;

            ObserveMainWindow();

            if (mainWindow == null || mainWindow.WindowState == System.Windows.WindowState.Minimized || !IsVisible)
                return;

            if (!hostedWindowAttached)
            {
                AttachHostedWindow();
                if (!hostedWindowAttached)
                    return;
            }

            GameWindow game = gameWindow;
            if (game == null)
                return;

            Size clientSize = hostPanel.ClientSize;
            if (clientSize.Width <= 0 || clientSize.Height <= 0)
                return;

            IntPtr mainWindowHandle = new WindowInteropHelper(mainWindow).Handle;
            if (mainWindowHandle == IntPtr.Zero || !GetWindowRect(mainWindowHandle, out NativeWindowRectangle bounds)
                || bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top)
                return;

            // Measure both ends in native screen coordinates, never WPF device-independent layout units.
            Point origin = hostPanel.PointToScreen(Point.Empty);
            PointD windowCenter = new PointD(((double)bounds.Left + bounds.Right) / 2, ((double)bounds.Top + bounds.Bottom) / 2);
            HostedViewportGeometry geometry = new HostedViewportGeometry(clientSize, origin, windowCenter);
            if (queuedGeometry == geometry)
                return;

            queuedGeometry = geometry;
            game.ApplyHostedViewportGeometry(geometry);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeWindowRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

#pragma warning disable SYSLIB1054 // Keep the small native measurement local to the host.
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr windowHandle, out NativeWindowRectangle rectangle);
#pragma warning restore SYSLIB1054

        private void FocusHostedWindow()
        {
            gameWindow?.FocusHostedWindow();
        }

        /// <summary>
        /// Enables or suppresses hosted game input. True means input is suspended (captured by non-map UI).
        /// </summary>
        internal void SetInputCaptured(bool captured)
        {
            GameWindow game = gameWindow;
            if (game == null)
                return;

            game.InvokeOnGameThread(() => game.SetHostedInputCaptured(captured));
        }

        /// <summary>
        /// Suppresses pointer-over activation of the hosted map surface, so that hovering the map does not take
        /// input away from a tool window that currently holds keyboard focus.
        /// </summary>
        internal void SetPointerActivationSuppressed(bool suppressed)
        {
            GameWindow game = gameWindow;
            if (game == null)
                return;

            game.InvokeOnGameThread(() => game.SetPointerActivationSuppressed(suppressed));
        }

        // Game-thread event: re-raise on the WPF dispatcher so the shell can show its modal save dialog.
        private void Game_SaveTrainPathRequested(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => SaveTrainPathRequested?.Invoke(this, EventArgs.Empty)));
        }

        // Game-thread event: re-raise on the WPF dispatcher so the shell can show its owned save dialog.
        private void Game_ScreenshotRequested(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => ScreenshotRequested?.Invoke(this, EventArgs.Empty)));
        }

        // Game-thread event: re-raise on the WPF dispatcher so the shell can show its modal About dialog.
        private void Game_AboutRequested(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => AboutRequested?.Invoke(this, EventArgs.Empty)));
        }

        // Game-thread event: re-raise on the WPF dispatcher so the shell can confirm and drive window close.
        private void Game_ExitRequested(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => ExitRequested?.Invoke(this, EventArgs.Empty)));
        }

        // Game-thread event: re-raise on the WPF dispatcher so the shell can re-localize its chrome/dialogs.
        private void Game_LanguageChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => LanguageChanged?.Invoke(this, EventArgs.Empty)));
        }

        // Game-thread event: re-raise on the WPF dispatcher so the shell can show its context menu.
        private void Game_MapContextMenuRequested(object sender, MapContextMenuRequestedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => MapContextMenuRequested?.Invoke(this, e)));
        }

        /// <summary>
        /// Applies a node-related action selected from the map context menu on the hosted game thread.
        /// </summary>
        internal void ExecuteMapContextMenuAction(MapContextMenuAction action, int nodeIndex, int candidateIndex, PathNode placementAnchor)
        {
            GameWindow game = gameWindow;
            game?.InvokeOnGameThread(() => game.ExecuteMapContextMenuAction(action, nodeIndex, candidateIndex, placementAnchor));
        }

        private void UpdateHostWindowTitle(string title)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                System.Windows.Window window = System.Windows.Window.GetWindow(this);
                window?.Title = title;
            }));
        }

        /// <summary>
        /// Captures the active path's metadata and identity for the WPF save dialog.
        /// </summary>
        internal Task<TrainPathSaveState> GetTrainPathSaveStateAsync()
        {
            GameWindow game = gameWindow;
            return game == null
                ? Task.FromResult<TrainPathSaveState>(null)
                : game.InvokeOnGameThreadAsync(() => Task.FromResult(game.CaptureTrainPathSaveState()));
        }

        /// <summary>Returns whether the selected route already contains the supplied train-path ID.</summary>
        internal Task<bool> TrainPathIdExistsAsync(string pathId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pathId);

            GameWindow game = gameWindow;
            return game == null
                ? Task.FromResult(false)
                : game.InvokeOnGameThreadAsync(() => game.TrainPathIdExistsAsync(pathId));
        }

        /// <summary>
        /// Submits the collected path metadata back to the hosted game (marshaled onto the game thread) to
        /// persist the path and refresh the path list. Called by the shell after the WPF save dialog is
        /// confirmed.
        /// </summary>
        internal Task SubmitSavePathAsync(TrainPathSaveRequest saveRequest)
        {
            ArgumentNullException.ThrowIfNull(saveRequest);

            GameWindow game = gameWindow;
            return game == null ? Task.CompletedTask : game.InvokeOnGameThreadAsync(() => game.SubmitTrainPathSaveAsync(saveRequest));
        }

        /// <summary>
        /// Persists the hosted game's live settings model together with the latest WPF dock layout and shell
        /// window placement.
        /// </summary>
        internal Task SaveHostedSettingsAsync(string dockLayoutJson, WindowPlacementSettings windowPlacement, int pathEditorSelectedTabIndex)
        {
            GameWindow game = gameWindow;
            return game == null ? Task.CompletedTask : game.SaveHostedSettingsAsync(dockLayoutJson, windowPlacement, pathEditorSelectedTabIndex);
        }

        /// <summary>
        /// Saves the hosted game's current back buffer to the specified PNG file path on the game thread.
        /// </summary>
        internal Task SaveScreenshotAsync(string fileName)
        {
            GameWindow game = gameWindow;
            return game == null ? Task.CompletedTask : game.InvokeOnGameThreadAsync(() => game.SaveScreenshotAsync(fileName));
        }

        /// <summary>
        /// Returns whether the hosted game has active or transient unsaved path edits.
        /// </summary>
        internal Task<bool> HasUnsavedPathChangesAsync()
        {
            GameWindow game = gameWindow;
            if (game == null)
                return Task.FromResult(false);
            return game.InvokeOnGameThreadAsync(() => Task.FromResult(game.HasUnsavedPathChanges));
        }

        private void OnHostedWindowPointerDown()
        {
            HostedWindowPointerDown?.Invoke(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            // WPF must await StopAsync first. Never block its dispatcher or destroy a live child's parent.
            if (!IsStoppedSafely)
            {
                if (disposing)
                    throw new InvalidOperationException("Await confirmed hosted game resource release and termination before disposing the host.");
                return;
            }

            disposed = true;

            Loaded -= HostGeometryChanged;
            SizeChanged -= HostGeometryChanged;
            LayoutUpdated -= HostGeometryChanged;
            hostPanel.ClientSizeChanged -= HostGeometryChanged;
            if (mainWindow != null)
            {
                mainWindow.LocationChanged -= HostGeometryChanged;
                mainWindow.SizeChanged -= HostGeometryChanged;
                mainWindow.StateChanged -= HostGeometryChanged;
                mainWindow = null;
            }

            gameThread = null;
            HostedServices = null;

            // Dispose the panel only after the game thread has stopped touching the child window.
            hostPanel?.Dispose();
            base.Dispose(disposing);
        }

        // Host panel for the reparented MonoGame window. The MonoGame window lives on a separate STA
        // game thread, so it is a cross-thread native child of this panel. When the user presses a mouse
        // button over the child, Windows sends WM_PARENTNOTIFY to this panel. The default WinForms handler
        // (WmParentNotify -> ReflectMessage) tries to read the child Control.Handle to reflect the message,
        // but there is no managed child on this thread and accessing the foreign window throws a
        // cross-thread InvalidOperationException inside the native WndProc callback, which the CLR escalates
        // to a fatal 0xc000041d (STATUS_FATAL_USER_CALLBACK_EXCEPTION) and kills the process.
        // The child has no managed sibling to reflect to, so suppressing WM_PARENTNOTIFY is safe and correct.
        private sealed class HostPanel : Panel
        {
            private const int WmParentNotify = 0x0210;
            private const int WmLButtonDown = 0x0201;
            private const int WmRButtonDown = 0x0204;
            private const int WmMButtonDown = 0x0207;
            private const int WmXButtonDown = 0x020B;

            private readonly Action hostedWindowPointerDown;

            public HostPanel(Action hostedWindowPointerDown)
            {
                this.hostedWindowPointerDown = hostedWindowPointerDown;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmParentNotify)
                {
                    int childMessage = m.WParam.ToInt32() & 0xFFFF;
                    if (childMessage is WmLButtonDown or WmRButtonDown or WmMButtonDown or WmXButtonDown)
                        hostedWindowPointerDown?.Invoke();
                    return;
                }

                base.WndProc(ref m);
            }
        }
    }
}
