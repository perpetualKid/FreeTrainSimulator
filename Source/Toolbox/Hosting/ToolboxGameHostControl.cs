using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Threading;

using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    public sealed class ToolboxGameHostControl : WindowsFormsHost
    {
        private readonly HostPanel hostPanel;
        private readonly Lock syncLock = new Lock();

        private Thread gameThread;
        private GameWindow gameWindow;
        private bool hostedWindowAttached;
        private IntPtr hostPanelHandle;
        private bool disposed;

        /// <summary>
        /// Hosted-mode menu bridge that the WPF shell binds its native menu to. Null until the hosted game
        /// window has been created; subscribe to <see cref="HostedMenuReady"/> to be notified when it becomes
        /// available. Raised on the WPF UI thread.
        /// </summary>
        internal HostedToolboxMenu HostedMenu => HostedServices?.Menu;

        /// <summary>
        /// Grouped hosted-mode bridge set published by the hosted game window. Null until available.
        /// </summary>
        internal HostedToolboxServices HostedServices { get; private set; }

        /// <summary>
        /// Raised on the WPF UI thread once <see cref="HostedMenu"/> becomes available.
        /// </summary>
        internal event EventHandler HostedMenuReady;

        /// <summary>
        /// Hosted-mode debug tool-window bridge that the WPF shell pulls read-only snapshots from. Null until
        /// the hosted game window has been created; subscribe to <see cref="HostedToolWindowsReady"/> to be
        /// notified when it becomes available. Raised on the WPF UI thread.
        /// </summary>
        internal DebugToolWindow HostedDebugToolWindow => HostedServices?.DebugToolWindow;

        /// <summary>
        /// Hosted-mode location tool-window bridge that the WPF shell pulls read-only snapshots from. Null
        /// until the hosted game window has been created; subscribe to <see cref="HostedToolWindowsReady"/>
        /// to be notified when it becomes available. Raised on the WPF UI thread.
        /// </summary>
        internal LocationToolWindow HostedLocationToolWindow => HostedServices?.LocationToolWindow;

        /// <summary>
        /// Hosted-mode log tool-window bridge that the WPF shell pulls read-only snapshots from. Null until
        /// the hosted game window has been created; subscribe to <see cref="HostedToolWindowsReady"/> to be
        /// notified when it becomes available. Raised on the WPF UI thread.
        /// </summary>
        internal LogToolWindow HostedLogToolWindow => HostedServices?.LogToolWindow;

        /// <summary>
        /// Hosted-mode track item tool-window bridge that the WPF shell pulls read-only snapshots from. Null
        /// until the hosted game window has been created; subscribe to <see cref="HostedToolWindowsReady"/>
        /// to be notified when it becomes available. Raised on the WPF UI thread.
        /// </summary>
        internal TrackItemInfoToolWindow HostedTrackItemInfoToolWindow => HostedServices?.TrackItemInfoToolWindow;

        /// <summary>
        /// Hosted-mode track node tool-window bridge that the WPF shell pulls read-only snapshots from. Null
        /// until the hosted game window has been created; subscribe to <see cref="HostedToolWindowsReady"/>
        /// to be notified when it becomes available. Raised on the WPF UI thread.
        /// </summary>
        internal TrackNodeInfoToolWindow HostedTrackNodeInfoToolWindow => HostedServices?.TrackNodeInfoToolWindow;

        /// <summary>
        /// Hosted-mode help tool-window bridge that the WPF shell pulls read-only snapshots from. Null until
        /// the hosted game window has been created; subscribe to <see cref="HostedToolWindowsReady"/> to be
        /// notified when it becomes available. Raised on the WPF UI thread.
        /// </summary>
        internal HelpToolWindow HostedHelpToolWindow => HostedServices?.HelpToolWindow;

        /// <summary>
        /// Hosted-mode settings tool-window bridge that the WPF shell reads and writes settings through. Null
        /// until the hosted game window has been created; subscribe to <see cref="HostedToolWindowsReady"/> to
        /// be notified when it becomes available. Raised on the WPF UI thread.
        /// </summary>
        internal SettingsToolWindow HostedSettingsToolWindow => HostedServices?.SettingsToolWindow;

        /// <summary>
        /// Hosted-mode train-path tool-window bridge that the WPF shell reads path/node snapshots from and
        /// drives path selection/node highlight through. Null until the hosted game window has been created;
        /// subscribe to <see cref="HostedToolWindowsReady"/> to be notified. Raised on the WPF UI thread.
        /// </summary>
        internal TrainPathToolWindow HostedTrainPathToolWindow => HostedServices?.TrainPathToolWindow;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted game requests a train-path save, so the shell can show
        /// its WPF modal save dialog. Respond by collecting the path metadata and calling
        /// <see cref="SubmitSavePath"/>.
        /// </summary>
        internal event EventHandler SaveTrainPathRequested;

        /// <summary>
        /// Raised on the WPF UI thread once <see cref="HostedDebugToolWindow"/> becomes available.
        /// </summary>
        internal event EventHandler HostedToolWindowsReady;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted child window receives a mouse-down notification.
        /// Useful for input-capture handoff back to the map view.
        /// </summary>
        internal event EventHandler HostedWindowPointerDown;

        /// <summary>
        /// Raised on the WPF UI thread when the hosted game requests a screenshot save location.
        /// </summary>
        internal event EventHandler ScreenshotRequested;

        public ToolboxGameHostControl()
        {
            hostPanel = new HostPanel(OnHostedWindowPointerDown)
            {
                Dock = DockStyle.Fill,
                TabStop = true,
            };

            Child = hostPanel;
            Loaded += ToolboxGameHostControl_Loaded;
            SizeChanged += ToolboxGameHostControl_SizeChanged;

            StartHostedGame();
        }

        private void ToolboxGameHostControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplyHostedSize();
        }

        private void ToolboxGameHostControl_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            ApplyHostedSize();
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
                    Name = "Toolbox.Wpf.HostedGameThread",
                };
                gameThread.SetApartmentState(ApartmentState.STA);
                gameThread.Start();
            }
        }

        private void GameThreadStart()
        {
            try
            {
                using (GameWindow game = new GameWindow())
                {
                    gameWindow = game;
                    game.EnableHostedMode(ReattachHostedWindow);

                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(AttachHostedWindow));
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(PublishHostedBridges));

                    game.Run();
                    gameWindow = null;
                }
            }
            catch (Exception ex) when (ex is not ThreadAbortException)
            {
                System.Diagnostics.Trace.TraceError($"[ToolboxGameHost] Hosted game thread terminated unexpectedly: {ex}");
            }
        }

        // Publishes the hosted menu bridge to the WPF shell once the game window has been created. Retries on
        // the dispatcher until the game window (and therefore its menu bridge) is available.
        private void PublishHostedBridges()
        {
            GameWindow game = gameWindow;
            HostedToolboxServices services = game?.HostedServices;
            if (services?.Menu == null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(PublishHostedBridges));
                return;
            }

            HostedServices = services;
            HostedMenuReady?.Invoke(this, EventArgs.Empty);

            game.SaveTrainPathRequested -= Game_SaveTrainPathRequested;
            game.SaveTrainPathRequested += Game_SaveTrainPathRequested;
            game.ScreenshotRequested -= Game_ScreenshotRequested;
            game.ScreenshotRequested += Game_ScreenshotRequested;
            HostedToolWindowsReady?.Invoke(this, EventArgs.Empty);
        }

        private void AttachHostedWindow()
        {
            if (hostPanel.IsDisposed || hostedWindowAttached)
                return;
            GameWindow game = gameWindow;
            if (game == null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(AttachHostedWindow));
                return;
            }

            IntPtr windowHandle = game.HostedWindowHandle;
            if (windowHandle == IntPtr.Zero || hostPanel.Handle == IntPtr.Zero)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(AttachHostedWindow));
                return;
            }

            // Cache the host panel handle so the game thread can reparent the child window without touching
            // the WinForms Control across threads.
            hostPanelHandle = hostPanel.Handle;

            // SetParent is a cross-thread operation (the parent is owned by the WPF UI thread), so it must
            // only run here, at initial attach, while the WPF thread is pumping messages. It is never called
            // again on resize.
            NativeMethods.SetParent(windowHandle, hostPanelHandle);
            ConfigureChildWindow(windowHandle, hostPanel.ClientSize.Width, hostPanel.ClientSize.Height);

            hostedWindowAttached = true;
            ApplyHostedSize();
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

        private void ApplyHostedSize()
        {
            if (disposed)
                return;

            if (!hostedWindowAttached)
            {
                AttachHostedWindow();
                return;
            }

            GameWindow game = gameWindow;
            if (game == null)
                return;

            int width = Math.Max(1, hostPanel.ClientSize.Width);
            int height = Math.Max(1, hostPanel.ClientSize.Height);

            // ApplyHostedClientSize marshals the resize to the game thread and, after ApplyChanges,
            // reattaches the child window in-thread via ReattachHostedWindow. The WPF thread must not
            // manipulate the game-thread-owned window itself.
            game.ApplyHostedClientSize(new Size(width, height));
        }

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

        /// <summary>
        /// Submits the collected path metadata back to the hosted game (marshaled onto the game thread) to
        /// persist the path and refresh the path list. Called by the shell after the WPF save dialog is
        /// confirmed.
        /// </summary>
        internal Task SubmitSavePathAsync(PathModelHeader pathDetails)
        {
            ArgumentNullException.ThrowIfNull(pathDetails);

            GameWindow game = gameWindow;
            if (game == null)
                return Task.CompletedTask;

            return game.InvokeOnGameThreadAsync(() => game.SubmitTrainPathSaveAsync(pathDetails));
        }

        /// <summary>
        /// Persists the hosted game's live settings model together with the latest WPF dock layout.
        /// </summary>
        internal Task SaveHostedSettingsAsync(string dockLayoutXml)
        {
            GameWindow game = gameWindow;
            return game == null ? Task.CompletedTask : game.SaveHostedSettingsAsync(dockLayoutXml);
        }

        /// <summary>
        /// Saves the hosted game's current back buffer to the specified PNG file path on the game thread.
        /// </summary>
        internal Task SaveScreenshotAsync(string fileName)
        {
            GameWindow game = gameWindow;
            return game == null ? Task.CompletedTask : game.InvokeOnGameThreadAsync(() => game.SaveScreenshotAsync(fileName));
        }

        private void OnHostedWindowPointerDown()
        {
            HostedWindowPointerDown?.Invoke(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            disposed = true;

            GameWindow game = gameWindow;
            if (game != null)
            {
                // Signal the game loop (running on its own STA thread) to exit. Marshal Exit onto the game
                // thread because that is where the MonoGame/WinForms state lives. Do NOT call game.Dispose()
                // here: the game owns a separate thread that is actively running game.Run(), and the
                // using-block in GameThreadStart disposes it on that thread once Run() returns. Disposing
                // here races with the live loop and tears down the GraphicsDevice/Platform mid-Tick, causing
                // a NullReferenceException inside MonoGame's Game.Tick().
                game.InvokeOnGameThread(game.Exit);
            }

            // Wait for the game thread to finish its loop and dispose the GameWindow on its owning thread.
            if (gameThread != null && gameThread.IsAlive)
            {
                gameThread.Join(TimeSpan.FromSeconds(5));
            }

            gameThread = null;
            gameWindow?.Dispose();
            gameWindow = null;

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
