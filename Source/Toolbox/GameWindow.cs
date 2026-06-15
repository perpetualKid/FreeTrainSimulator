using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.PopupWindows;
using FreeTrainSimulator.Toolbox.Settings;

using GetText;
using GetText.WindowsForms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Toolbox
{
    public partial class GameWindow : Game, IInputCapture
    {
        private readonly GraphicsDeviceManager graphicsDeviceManager;
        private readonly Form windowForm;
        private readonly CommonDebugInfo debugInfo;
        private readonly GraphicsDebugInfo graphicsDebugInfo = new GraphicsDebugInfo();

        private SpriteBatch spriteBatch;

        private bool syncing;
        private ScreenMode currentScreenMode;
        private Screen currentScreen;
        private Point windowPosition;
        private System.Drawing.Size windowSize;
        private readonly Point clientRectangleOffset;

        private readonly Action onClientSizeChanged;

        private WindowManager<ToolboxWindowType> windowManager;
        private ContentArea contentArea;
        private int suppressCount;
        private bool waitOnExit;

        internal ContentArea ContentArea
        {
            get => contentArea;
            set => windowForm.Invoke((System.Windows.Forms.MethodInvoker)delegate
            {
                if (contentArea != null)
                {
                    contentArea.Enabled = false;
                    Components.Remove(contentArea);
                    Window.Title = windowTitle;
                    contentArea.Dispose();
                    contentArea = null;
                }
                if (value != null)
                {
                    IMapHostControl hostControl = value;
                    hostControl.ResetSize(Window.ClientBounds.Size, 60);
                    Components.Add(value);
                    hostControl.IsEnabled = true;
                    Window.Title = windowTitle + Catalog.GetString($" Route: {value.Content.RouteName}");
                }
                contentArea = value;
                OnContentAreaChanged?.Invoke(this, new ContentAreaChangedEventArgs(contentArea));
            });
        }

        internal event EventHandler<ContentAreaChangedEventArgs> OnContentAreaChanged;

        private ProfileModel currentProfile;

        internal ProfileToolboxSettingsModel ToolboxSettings { get; private set; }
        internal ProfileUserSettingsModel ToolboxUserSettings { get; private set; }

        internal string LogFileName { get; }

        private Color backgroundColor;

        internal Catalog Catalog { get; private set; }
        private readonly ObjectPropertiesStore store = new ObjectPropertiesStore();
        private readonly string windowTitle;
        private UserCommandController<UserCommand> userCommandController;

        // Abstraction over the main menu: a hosted bridge that forwards menu data/commands to the WPF menu.
        private IToolboxMenu menu;

        // Non-null only in hosted mode; exposed to the WPF host so the native menu can bind to it.
        private HostedToolboxMenu hostedMenu;

        // Non-null only in hosted mode; exposed to the WPF host so a dockable tool window can pull
        // read-only debug/graphics information snapshots from the game thread.
        private DebugToolWindow hostedDebugToolWindow;

        // Non-null only in hosted mode; exposed to the WPF host for read-only map location information.
        private LocationToolWindow hostedLocationToolWindow;

        // Non-null only in hosted mode; exposed to the WPF host for read-only log file content.
        private LogToolWindow hostedLogToolWindow;

        // Non-null only in hosted mode; exposed to the WPF host for read-only track item information.
        private TrackItemInfoToolWindow hostedTrackItemInfoToolWindow;

        // Non-null only in hosted mode; exposed to the WPF host for read-only track node information.
        private TrackNodeInfoToolWindow hostedTrackNodeInfoToolWindow;

        // Non-null only in hosted mode; exposed to the WPF host for read-only command/key help information.
        private HelpToolWindow hostedHelpToolWindow;

        // Non-null only in hosted mode; exposed to the WPF host for two-way settings editing.
        private SettingsToolWindow hostedSettingsToolWindow;

        // Non-null only in hosted mode; exposed to the WPF host for train-path browsing/editing.
        private TrainPathToolWindow hostedTrainPathToolWindow;

        public GameWindow()
        {
            ImmutableArray<string> options = Environment.GetCommandLineArgs().Where(a => a.StartsWith('-') || a.StartsWith('/')).Select(a => a[1..]).ToImmutableArray();

            CatalogManager.SetCatalogDomainPattern(CatalogDomainPattern.AssemblyName, null, RuntimeInfo.LocalesFolder);

            Task.Run(LoadSettings).Wait();
            if (ToolboxUserSettings.LogLevel != TraceEventType.Critical)
            {
                LogFileName = RuntimeInfo.LogFile(ToolboxUserSettings.LogFilePath, ToolboxUserSettings.LogFileName);
                LoggingUtil.InitLogging(LogFileName, TraceEventType.Error, false, false);
                ToolboxSettings.Log();
            }

            windowForm = (Form)Control.FromHandle(Window.Handle);
            currentScreen = ToolboxSettings.WindowScreen < Screen.AllScreens.Length
                ? Screen.AllScreens[ToolboxSettings.WindowScreen]
                : Screen.PrimaryScreen;
            FontManager.ScalingFactor = (float)WindowManager.DisplayScalingFactor(currentScreen);

            ApplySettings();
            hostedMenu = new HostedToolboxMenu(this);
            menu = hostedMenu;
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreparingDeviceSettings += GraphicsPreparingDeviceSettings;
            graphicsDeviceManager.PreferMultiSampling = ToolboxUserSettings.MultiSamplingCount > 0;
            IsMouseVisible = true;

            // Set title to show revision or build info.
            windowTitle = $"{RuntimeInfo.ProductName} {VersionInfo.Version}";
#if DEBUG
            windowTitle += " (debug)";
#endif
            Window.Title = windowTitle;
            Window.AllowUserResizing = true;

            //Window.ClientSizeChanged += Window_ClientSizeChanged; // not using the GameForm event as it does not raise when Window is moved (ie to another screeen) using keyboard shortcut

            //graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;
            //IsFixedTimeStep = false;
            //TargetElapsedTime = TimeSpan.FromMilliseconds(5);

            clientRectangleOffset = new Point(windowForm.Width - windowForm.ClientRectangle.Width, windowForm.Height - windowForm.ClientRectangle.Height);
            Window.Position = windowPosition;

            SetScreenMode(currentScreenMode);

            windowForm.LocationChanged += WindowForm_LocationChanged;
            windowForm.ClientSizeChanged += WindowForm_ClientSizeChanged;

            // using reflection to be able to trigger ClientSizeChanged event manually as this is not 
            // reliably raised otherwise with the resize functionality below in SetScreenMode
            MethodInfo m = Window.GetType().GetMethod("OnClientSizeChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            onClientSizeChanged = (Action)Delegate.CreateDelegate(typeof(Action), Window, m);

            windowForm.FormClosing += WindowForm_FormClosing;
            Exiting += GameWindow_Exiting;
            LoadLanguage();
            SystemInfo.SetGraphicAdapterInformation(graphicsDeviceManager.GraphicsDevice.Adapter.Description);
            debugInfo = new CommonDebugInfo(this);
            hostedDebugToolWindow = new DebugToolWindow(debugInfo, graphicsDebugInfo);
            hostedLocationToolWindow = new LocationToolWindow(ToolboxSettings);
            hostedLogToolWindow = new LogToolWindow(LogFileName);
            hostedTrackItemInfoToolWindow = new TrackItemInfoToolWindow(InvokeOnGameThread);
            hostedTrackNodeInfoToolWindow = new TrackNodeInfoToolWindow(InvokeOnGameThread);
            hostedHelpToolWindow = new HelpToolWindow();
            hostedSettingsToolWindow = new SettingsToolWindow(
                () => ToolboxUserSettings.LogLevel != TraceEventType.Critical,
                () => ToolboxSettings.RestoreLastView,
                () => ToolboxSettings.FontOutline,
                () => !ToolboxSettings.LimitTrackWidth,
                value => InvokeOnGameThread(() => ToolboxUserSettings.LogLevel = value ? TraceEventType.Verbose : TraceEventType.Critical),
                value => InvokeOnGameThread(() => ToolboxSettings.RestoreLastView = value),
                value => InvokeOnGameThread(() => UpdateFontOutlinePreference(value)),
                value => InvokeOnGameThread(() => UpdateTrackWidthPreference(!value)));
            hostedTrainPathToolWindow = new TrainPathToolWindow(() => HostedPathEditor, () => HostedTrainPathToolingContext, InvokeOnGameThread);
            OnContentAreaChanged += GameWindow_OnContentAreaChanged;
            windowForm.KeyPreview = true;// need to preview keys to enable Monogames TextInput handler, otherwise adding the main menu will break text input
        }

        private void GameWindow_Exiting(object sender, ExitingEventArgs e)
        {
            e.Cancel = waitOnExit;
        }

        private void WindowForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // In hosted mode the WPF shell owns application shutdown; the child game window must not drive it.
        }

        #region window size/position handling
        private void WindowForm_ClientSizeChanged(object sender, EventArgs e)
        {
            if (syncing)
                return;

            // The WPF host drives sizing of the embedded child window.
            ApplyHostedClientSize(windowForm.ClientSize);
            return;

            if (currentScreenMode == ScreenMode.Windowed)
                windowSize = new System.Drawing.Size(Window.ClientBounds.Width, Window.ClientBounds.Height);
            //originally, following code would be in Window.LocationChanged handler, but seems to be more reliable here for MG version 3.7.1
            if (currentScreenMode == ScreenMode.Windowed)
                windowPosition = Window.Position;
            // if (fullscreen) gameWindow is moved to different screen we may need to refit for different screen resolution
            Screen newScreen = Screen.FromControl(windowForm);
            (newScreen, currentScreen) = (currentScreen, newScreen);
            if (newScreen.DeviceName != currentScreen.DeviceName && currentScreenMode != ScreenMode.Windowed)
            {
                SetScreenMode(currentScreenMode);
                //reset Window position to center on new screen
                windowPosition = new Point(
                    currentScreen.WorkingArea.Left + ((currentScreen.WorkingArea.Size.Width - windowSize.Width) / 2),
                    currentScreen.WorkingArea.Top + ((currentScreen.WorkingArea.Size.Height - windowSize.Height) / 2));
            }
        }

        private void WindowForm_LocationChanged(object sender, EventArgs e)
        {
            // The WPF host owns window placement; nothing to do for the embedded child window.
        }

        internal IntPtr HostedWindowHandle
        {
            get
            {
                if (windowForm == null || windowForm.IsDisposed || windowForm.Disposing)
                    return IntPtr.Zero;

                if (windowForm.InvokeRequired)
                {
                    return (IntPtr)windowForm.Invoke((Func<IntPtr>)(() =>
                        windowForm.IsHandleCreated ? windowForm.Handle : IntPtr.Zero));
                }

                return windowForm.IsHandleCreated ? windowForm.Handle : IntPtr.Zero;
            }
        }

        // Callback supplied by the WPF host to re-parent/re-style the embedded child window. It is invoked
        // on the game thread (the window's owning thread) right after a hosted resize applies, so the
        // reparenting happens in-thread and never stalls the WPF UI thread during its modal resize loop.
        private Action<IntPtr, int, int> hostedReattachCallback;

        internal void EnableHostedMode(Action<IntPtr, int, int> reattachCallback)
        {
            hostedReattachCallback = reattachCallback;
            Window.AllowUserResizing = false;
            windowForm.FormBorderStyle = FormBorderStyle.None;
            SetScreenMode(ScreenMode.Windowed);
        }

        /// <summary>
        /// Hosted-mode menu bridge that the WPF shell binds its native menu to. Null in standalone mode.
        /// </summary>
        internal HostedToolboxMenu HostedMenu => hostedMenu;

        /// <summary>
        /// Hosted-mode debug tool-window bridge that the WPF shell pulls read-only information snapshots
        /// from. Null in standalone mode.
        /// </summary>
        internal DebugToolWindow HostedDebugToolWindow => hostedDebugToolWindow;

        /// <summary>
        /// Hosted-mode location tool-window bridge that the WPF shell pulls read-only location snapshots
        /// from. Null in standalone mode.
        /// </summary>
        internal LocationToolWindow HostedLocationToolWindow => hostedLocationToolWindow;

        /// <summary>
        /// Hosted-mode log tool-window bridge that the WPF shell pulls read-only log content from. Null in
        /// standalone mode.
        /// </summary>
        internal LogToolWindow HostedLogToolWindow => hostedLogToolWindow;

        /// <summary>
        /// Hosted-mode track item tool-window bridge that the WPF shell pulls read-only track item snapshots
        /// from. Null in standalone mode.
        /// </summary>
        internal TrackItemInfoToolWindow HostedTrackItemInfoToolWindow => hostedTrackItemInfoToolWindow;

        /// <summary>
        /// Hosted-mode track node tool-window bridge that the WPF shell pulls read-only track node snapshots
        /// from. Null in standalone mode.
        /// </summary>
        internal TrackNodeInfoToolWindow HostedTrackNodeInfoToolWindow => hostedTrackNodeInfoToolWindow;

        /// <summary>
        /// Hosted-mode help tool-window bridge that the WPF shell pulls read-only command/key help snapshots
        /// from. Null in standalone mode.
        /// </summary>
        internal HelpToolWindow HostedHelpToolWindow => hostedHelpToolWindow;

        /// <summary>
        /// Hosted-mode settings tool-window bridge that the WPF shell reads and writes settings through.
        /// Null in standalone mode.
        /// </summary>
        internal SettingsToolWindow HostedSettingsToolWindow => hostedSettingsToolWindow;

        /// <summary>
        /// Hosted-mode train-path tool-window bridge that the WPF shell reads path/node snapshots from and
        /// drives path selection/node highlight through. Null in standalone mode.
        /// </summary>
        internal TrainPathToolWindow HostedTrainPathToolWindow => hostedTrainPathToolWindow;

        internal void ApplyHostedClientSize(System.Drawing.Size clientSize)
        {
            if (clientSize.Width <= 0 || clientSize.Height <= 0)
                return;

            if (windowForm.InvokeRequired)
            {
                windowForm.BeginInvoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    ApplyHostedClientSize(clientSize);
                });
                return;
            }

            int previousClientWidth = graphicsDeviceManager.PreferredBackBufferWidth;
            int previousClientHeight = graphicsDeviceManager.PreferredBackBufferHeight;
            bool sizeChanged = previousClientWidth != clientSize.Width || previousClientHeight != clientSize.Height;

            if (sizeChanged)
            {
                syncing = true;
                try
                {
                    graphicsDeviceManager.PreferredBackBufferWidth = clientSize.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = clientSize.Height;
                    graphicsDeviceManager.ApplyChanges();
                }
                finally
                {
                    syncing = false;
                }
            }

            // Always re-assert the embedded child parenting/styles/size on this (the game) thread, even when
            // the back buffer size was unchanged. graphicsDeviceManager.ApplyChanges() resets the MonoGame
            // window to a top-level style and detaches it from the WPF host panel; if we skipped this on a
            // size-unchanged call, the window would remain top-level and stop receiving input after a resize.
            // Doing this in-thread avoids cross-thread SetParent calls that would deadlock the WPF UI thread.
            hostedReattachCallback?.Invoke(windowForm.Handle, clientSize.Width, clientSize.Height);

            // Hosted resizes do not reliably raise MonoGame's client-size pipeline. Trigger it explicitly so
            // map viewport bounds and drawable overlays (inset, overlays, etc.) recompute to the new size
            // without resetting the user's zoom/position state.
            onClientSizeChanged?.Invoke();

            // Keep top-left world position anchored on hosted resize. Default viewport resize behavior centers
            // around the current center point; here we re-apply a compensating pan so the old top-left world
            // coordinate remains at screen origin when size changes.
            if (sizeChanged && contentArea is IMapHostControl hostControl && previousClientWidth > 0 && previousClientHeight > 0)
            {
                PointD previousTopLeft = hostControl.CenterPoint + new PointD(-previousClientWidth / (2d * hostControl.Scale), previousClientHeight / (2d * hostControl.Scale));
                PointD newTopLeft = hostControl.CenterPoint + new PointD(-clientSize.Width / (2d * hostControl.Scale), clientSize.Height / (2d * hostControl.Scale));
                Vector2 compensate = new Vector2((float)((newTopLeft.X - previousTopLeft.X) * hostControl.Scale), (float)((previousTopLeft.Y - newTopLeft.Y) * hostControl.Scale));
                hostControl.UpdatePosition(compensate);
            }
        }

        // Marshals a command coming from the WPF UI thread onto the game (windowForm) thread, where all
        // MonoGame/WinForms state lives. Used by HostedToolboxMenu to forward native-menu actions safely.
        internal void InvokeOnGameThread(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (windowForm.IsDisposed || windowForm.Disposing)
                return;

            if (windowForm.InvokeRequired)
                _ = windowForm.BeginInvoke(action);
            else
                action();
        }

        internal void FocusHostedWindow()
        {
            if (windowForm.IsDisposed || windowForm.Disposing)
                return;

            if (windowForm.InvokeRequired)
            {
                windowForm.BeginInvoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    FocusHostedWindow();
                });
                return;
            }

            if (!windowForm.IsHandleCreated)
                return;

            windowForm.ActiveControl = null;
            windowForm.Select();
        }

        /// <summary>
        /// Updates hosted input capture state from the WPF shell. When captured is true, keyboard/mouse map
        /// interactions are suspended on the game thread while focus is on non-map controls/documents.
        /// </summary>
        internal void SetHostedInputCaptured(bool captured)
        {
            InputCaptured = captured;
        }

        internal void UpdateColorPreference(ColorSetting setting, string colorName)
        {
            ToolboxSettings.ColorSettings[setting] = colorName;
            contentArea?.UpdateColor(setting, ColorExtension.FromName(colorName), ToolboxSettings.FontOutline);
            if (setting == ColorSetting.Background)
            {
                backgroundColor = ColorExtension.FromName(colorName);
                (windowManager[ToolboxWindowType.DebugScreen] as DebugScreen)?.UpdateBackgroundColor(backgroundColor);
            }
        }

        internal void UpdateItemVisibilityPreference(MapContentType setting, bool enabled)
        {
            ToolboxSettings.ViewSettings[setting] = enabled;
        }

        internal void UpdateFontOutlinePreference(bool fontOutline)
        {
            ToolboxSettings.FontOutline = fontOutline;
            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
                contentArea?.UpdateColor(setting, ColorExtension.FromName(ToolboxSettings.ColorSettings[setting]), ToolboxSettings.FontOutline);
            (windowManager[ToolboxWindowType.DebugScreen] as DebugScreen)?.UpdateBackgroundColor(ColorExtension.FromName(ToolboxSettings.ColorSettings[ColorSetting.Background]));
        }

        internal void UpdateTrackWidthPreference(bool limitTrackWidth)
        {
            ToolboxSettings.LimitTrackWidth = limitTrackWidth;
            (contentArea as IMapDisplaySettingsContext)?.UpdateTrackWidthSettings(limitTrackWidth);
        }

        internal void UpdateLanguagePreference(string language)
        {
            ToolboxUserSettings.Language = language;
            LoadLanguage();
        }

        private async Task LoadSettings()
        {
            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(loadRouteSemaphore, true).ConfigureAwait(false);
            currentProfile = await currentProfile.Current(ctsProfileLoading.Token).ConfigureAwait(false);
            ToolboxUserSettings = await currentProfile.LoadSettingsModel<ProfileUserSettingsModel>(ctsProfileLoading.Token).ConfigureAwait(false);
            ToolboxSettings = await currentProfile.LoadSettingsModel<ProfileToolboxSettingsModel>(ctsProfileLoading.Token).ConfigureAwait(false);
        }

        private void ApplySettings()
        {
            windowSize = new System.Drawing.Size(
                (int)(currentScreen.WorkingArea.Size.Width * Math.Abs(ToolboxSettings.WindowSettings[WindowSetting.Size].X) / 100.0),
                (int)(currentScreen.WorkingArea.Size.Height * Math.Abs(ToolboxSettings.WindowSettings[WindowSetting.Size].Y) / 100.0));

            windowPosition = ToolboxSettings.WindowSettings[WindowSetting.Location].ToPoint();
            windowPosition = new Point(
                    currentScreen.WorkingArea.Left + (windowPosition.X * (currentScreen.WorkingArea.Size.Width - windowSize.Width) / 100),
                    currentScreen.WorkingArea.Top + (windowPosition.Y * (currentScreen.WorkingArea.Size.Height - windowSize.Height) / 100));
            backgroundColor = ColorExtension.FromName(ToolboxSettings.ColorSettings[ColorSetting.Background]);
        }

        private async Task SaveSettings()
        {
            ToolboxSettings.WindowSettings[WindowSetting.Size] = ((int)Math.Round(100.0 * windowSize.Width / currentScreen.WorkingArea.Width), (int)Math.Round(100.0 * windowSize.Height / currentScreen.WorkingArea.Height));

            ToolboxSettings.WindowSettings[WindowSetting.Location] = (
                (int)Math.Max(0, Math.Round(100f * (windowPosition.X - currentScreen.Bounds.Left) / (currentScreen.WorkingArea.Width - windowSize.Width))),
                (int)Math.Max(0, Math.Round(100.0 * (windowPosition.Y - currentScreen.Bounds.Top) / (currentScreen.WorkingArea.Height - windowSize.Height))));

            foreach (ToolboxWindowType windowType in EnumExtension.GetValues<ToolboxWindowType>())
            {
                if (windowManager.WindowInitialized(windowType))
                {
                    ToolboxSettings.PopupLocations[windowType] = windowManager[windowType].RelativeLocation.FromPoint();
                    ToolboxSettings.PopupStatus[windowType] = windowManager.WindowOpened(windowType) && !windowManager.WindowModal(windowType);
                }
                else
                {
                    ToolboxSettings.PopupStatus[windowType] = false;
                }
            }

            ToolboxSettings.WindowScreen = Screen.AllScreens.ToList().IndexOf(currentScreen);
            ToolboxSettings.ContentPosition = contentArea is IMapHostControl hostControl ? hostControl.CenterPoint : PointD.None;
            ToolboxSettings.ContentScale = contentArea is IMapHostControl hostControl2 ? hostControl2.Scale : 1;

            ToolboxSettings.Folder = selectedFolder?.Id;
            ToolboxSettings.RouteId = selectedRoute?.Id;
            ToolboxSettings.PathId = PathEditor?.PathId;

            //            ProfileSettingModelHandler<ProfileUserSettingsModel>.SetValueByName(ToolboxUserSettings, "MultiSamplingCount", 8);

            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(loadRouteSemaphore, true).ConfigureAwait(false);
            ToolboxSettings = await currentProfile.UpdateSettingsModel(ToolboxSettings, ctsProfileLoading.Token).ConfigureAwait(false);
            ToolboxUserSettings = await currentProfile.UpdateSettingsModel(ToolboxUserSettings, ctsProfileLoading.Token).ConfigureAwait(false);

        }

        private void LoadLanguage()
        {
            Localizer.Revert(windowForm, store);
            CatalogManager.Reset();

            if (!string.IsNullOrEmpty(ToolboxUserSettings.Language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(ToolboxUserSettings.Language);
                }
                catch (CultureNotFoundException exception)
                {
                    Trace.WriteLine(exception.Message);
                }
            }
            else
            {
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InstalledUICulture;
            }
            Catalog = CatalogManager.Catalog;
            Localizer.Localize(windowForm, Catalog, store);
        }

        private void GraphicsPreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
            e.GraphicsDeviceInformation.PresentationParameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;
            e.GraphicsDeviceInformation.PresentationParameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = ToolboxUserSettings.MultiSamplingCount;
        }

        private void SetScreenMode(ScreenMode targetMode)
        {
            syncing = true;
            _ = windowForm.Invoke((System.Windows.Forms.MethodInvoker)delegate
            {
                if (graphicsDeviceManager.IsFullScreen)
                    graphicsDeviceManager.ToggleFullScreen();

                windowForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                System.Drawing.Size clientSize = windowForm.ClientSize;
                if (clientSize.Width > 0 && clientSize.Height > 0)
                {
                    graphicsDeviceManager.PreferredBackBufferWidth = clientSize.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = clientSize.Height;
                    graphicsDeviceManager.ApplyChanges();
                }
                targetMode = ScreenMode.Windowed;
            });
            currentScreenMode = targetMode;
            onClientSizeChanged?.Invoke();
            syncing = false;
        }
        #endregion

        protected override async void Initialize()
        {
            Task<bool> loadFolders = LoadFolders();

            spriteBatch = new SpriteBatch(GraphicsDevice);

            userCommandController = new UserCommandController<UserCommand>();
            KeyboardInputGameComponent keyboardInputGameComponent = new KeyboardInputGameComponent(this)
            {
                // When hosted as a child window, the form can lose top-level activation on resize and never
                // report Game.IsActive again; bypass the IsActive gate so keyboard input keeps working.
                IgnoreActiveState = true,
            };
            Components.Add(keyboardInputGameComponent);
            KeyboardInputHandler<UserCommand> keyboardInput = new KeyboardInputHandler<UserCommand>();
            keyboardInput.Initialize(InputSettings.UserCommands, keyboardInputGameComponent, userCommandController);

            MouseInputGameComponent mouseInputGameComponent = new MouseInputGameComponent(this)
            {
                DisableTouchInput = true,
                UseWindowMouseState = false,
                // When hosted as a child window, the form can lose top-level activation on resize and never
                // report Game.IsActive again; bypass the IsActive gate so mouse input keeps working.
                IgnoreActiveState = true,
            };
            Components.Add(mouseInputGameComponent);
            MouseInputHandler<UserCommand> mouseInput = new MouseInputHandler<UserCommand>();
            mouseInput.Initialize(mouseInputGameComponent, keyboardInputGameComponent, userCommandController);

            #region usercommandcontroller
            userCommandController.AddEvent(UserCommand.PrintScreen, KeyEventType.KeyPressed, PrintScreen);
            userCommandController.AddEvent(UserCommand.ChangeScreenMode, KeyEventType.KeyPressed, ChangeScreenMode);
            userCommandController.AddEvent(UserCommand.QuitWindow, KeyEventType.KeyPressed, CloseWindow);
            userCommandController.AddEvent(UserCommand.MoveLeft, KeyEventType.KeyDown, MoveByKeyLeft);
            userCommandController.AddEvent(UserCommand.MoveRight, KeyEventType.KeyDown, MoveByKeyRight);
            userCommandController.AddEvent(UserCommand.MoveUp, KeyEventType.KeyDown, MoveByKeyUp);
            userCommandController.AddEvent(UserCommand.MoveDown, KeyEventType.KeyDown, MoveByKeyDown);
            userCommandController.AddEvent(UserCommand.NewInstance, KeyEventType.KeyPressed, () => new Thread(GameWindowThread).Start());
            userCommandController.AddEvent(UserCommand.ZoomIn, KeyEventType.KeyDown, ZoomIn);
            userCommandController.AddEvent(UserCommand.ZoomOut, KeyEventType.KeyDown, ZoomOut);
            userCommandController.AddEvent(UserCommand.ResetZoomAndLocation, KeyEventType.KeyPressed, ResetZoomAndLocation);
            userCommandController.AddEvent(UserCommand.DisplayDebugScreen, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ToolboxWindowType.DebugScreen].ToggleVisibility();
            });
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, (userCommandArgs) =>
            {
                MouseDragging(userCommandArgs);
            });
            userCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, MouseWheel);
            userCommandController.AddEvent(UserCommand.DisplayLocationWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                    hostedLocationToolWindow?.ToggleCoordinateMode();
            });
            #endregion

            #region popup windows
            windowManager = WindowManager.Initialize<UserCommand, ToolboxWindowType>(this, userCommandController.AddTopLayerController());
            windowManager[ToolboxWindowType.StatusWindow] = new StatusTextWindow(windowManager, ToolboxSettings.PopupLocations[ToolboxWindowType.StatusWindow].ToPoint());
            windowManager[ToolboxWindowType.AboutWindow] = new AboutWindow(windowManager, ToolboxSettings.PopupLocations[ToolboxWindowType.AboutWindow].ToPoint());
            windowManager.SetLazyWindows(ToolboxWindowType.QuitWindow, new Lazy<FormBase>(() =>
            {
                QuitWindow quitWindow = new QuitWindow(windowManager, ToolboxSettings.PopupLocations[ToolboxWindowType.QuitWindow].ToPoint());
                quitWindow.OnQuitGame += QuitWindow_OnQuitGame;
                quitWindow.OnWindowClosed += QuitWindow_OnWindowClosed;
                quitWindow.OnPrintScreen += QuitWindow_OnPrintScreen;
                return quitWindow;
            }));

            windowManager.SetLazyWindows(ToolboxWindowType.DebugScreen, new Lazy<FormBase>(() =>
            {
                DebugScreen debugWindow = new DebugScreen(windowManager, ToolboxSettings, backgroundColor);
                debugWindow.SetInformationProvider(DebugScreenInformation.Common, debugInfo);
                debugWindow.SetInformationProvider(DebugScreenInformation.Graphics, graphicsDebugInfo);
                debugWindow.SetInformationProvider(DebugScreenInformation.Route, ContentArea?.Content);
                OnContentAreaChanged += debugWindow.GameWindow_OnContentAreaChanged;
                return debugWindow;
            }));

            #endregion

            windowManager.OnModalWindow += WindowManager_OnModalWindow;
            Components.Add(windowManager);
            base.Initialize();

            if (!(await loadFolders.ConfigureAwait(true)))
            {
                // content may need updates
                MessageBox.Show($"In an effort to optimize content, {RuntimeInfo.ProductName} will need to analyze existing content files and folders." + Environment.NewLine + Environment.NewLine +
                    $"Please close {RuntimeInfo.ApplicationName}, and use the Menu-application to review current content folder settings for further analysis.", "Please read!");
                return;
            }
            if (ToolboxSettings.RestoreLastView)
            {
                try
                {
                    await PreSelectRoute(ToolboxSettings.Folder, ToolboxSettings.RouteId, ToolboxSettings.PathId).ConfigureAwait(true);
                    ContentArea?.PresetPosition(ToolboxSettings.ContentPosition, ToolboxSettings.ContentScale);
                    foreach (ToolboxWindowType windowType in EnumExtension.GetValues<ToolboxWindowType>())
                    {
                        if (windowType == ToolboxWindowType.LocationWindow || windowType == ToolboxWindowType.LogWindow
                            || windowType == ToolboxWindowType.TrackNodeInfoWindow || windowType == ToolboxWindowType.TrackItemInfoWindow
                            || windowType == ToolboxWindowType.HelpWindow || windowType == ToolboxWindowType.SettingsWindow
                            || windowType == ToolboxWindowType.TrainPathWindow)
                            continue;

                        if (ToolboxSettings.PopupStatus[windowType])
                            windowManager[windowType].Open();
                    }
                }
                catch (Exception ex) when (ex is Exception)
                {
                    Trace.TraceError($"Error restoring last view: {ex}");
                    windowManager[ToolboxWindowType.StatusWindow].Close();
                    ToolboxSettings.RestoreLastView = false;
                }
            }
        }

        private void WindowManager_OnModalWindow(object sender, ModalWindowEventArgs e)
        {
            menu.Enabled = !e.ModalWindowOpen;

            if (null != ContentArea)
                ContentArea.Enabled = !e.ModalWindowOpen;
        }

        private static void GameWindowThread(object data)
        {
            using (GameWindow game = new GameWindow())
            {
                game.Run();
            }
        }

        protected override void LoadContent()
        {
            //DigitalClockComponent clock = new DigitalClockComponent(this, TimeType.RealWorldLocalTime, FontManager.Exact("Segoe UI", System.Drawing.FontStyle.Regular)[14], Color.White, new Vector2(-200, -100), true);
            //Components.Add(clock);
            ScaleRulerComponent scaleRuler = new ScaleRulerComponent(this, FontManager.Scaled(System.Drawing.FontFamily.GenericSansSerif, System.Drawing.FontStyle.Regular)[14], Color.Black, new Vector2(-20, -55));
            Components.Add(scaleRuler);
            Components.Add(new InsetComponent(this, backgroundColor, new Vector2(-10, 30)));
            //Components.Add(new WorldCoordinatesComponent(this, FontManager.Exact(System.Drawing.FontFamily.GenericSansSerif, System.Drawing.FontStyle.Regular)[20], Color.Blue, new Vector2(40, 40)));
        }

        protected override void Update(GameTime gameTime)
        {
            if ((contentArea?.SuppressDrawing ?? false) && windowManager.SuppressDrawing && suppressCount-- > 0)
            {
                SuppressDraw();
            }
            else
            {
                suppressCount = 10;
            }
            base.Update(gameTime);
        }

        public bool InputCaptured { get; internal set; }

        private void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            hostedLocationToolWindow?.UpdateLocationContext(e.LocationContext);
            hostedTrackItemInfoToolWindow?.UpdateContext(e.TrackItemInfoContext);
            hostedTrackNodeInfoToolWindow?.UpdateContext(e.TrackNodeInfoContext);
            hostedTrainPathToolWindow?.InvalidatePaths();
        }

        protected override void Draw(GameTime gameTime)
        {
            debugInfo.Update(gameTime);
            GraphicsDevice.Clear(backgroundColor);
            base.Draw(gameTime);

            graphicsDebugInfo.CurrentMetrics = GraphicsDevice.Metrics;
            graphicsDebugInfo.Update(gameTime);

            // Rebuild hosted tool-window snapshots on the game thread once providers have updated this frame.
            hostedDebugToolWindow?.RefreshSnapshot();
            hostedLocationToolWindow?.RefreshSnapshot();
            hostedLogToolWindow?.RefreshSnapshot();
            hostedTrackItemInfoToolWindow?.RefreshSnapshot();
            hostedTrackNodeInfoToolWindow?.RefreshSnapshot();
            hostedHelpToolWindow?.RefreshSnapshot();
            hostedTrainPathToolWindow?.RefreshSnapshot();
        }

        private sealed class CommonDebugInfo : DetailInfoBase
        {
            private readonly SmoothedData frameRate = new SmoothedData();
            private ContentArea contentArea;

            private readonly int slowFps;

            public CommonDebugInfo(GameWindow gameWindow) : base(true)
            {
                int targetFps = (int)Math.Round(1000 / gameWindow.TargetElapsedTime.TotalMilliseconds);
                slowFps = targetFps - (targetFps / 6);
                frameRate.Preset(targetFps);
                this["Version"] = VersionInfo.FullVersion;
                gameWindow.OnContentAreaChanged += GameWindow_OnContentAreaChanged;
            }

            private void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
            {
                contentArea = e.ContentArea;
            }

            public override void Update(GameTime gameTime)
            {
                this["Time"] = DateTime.Now.ToString(CultureInfo.CurrentCulture);
                this["Scale"] = contentArea == null ? null : $"{contentArea.Scale:F3} (pixel/meter)";
                double elapsedRealTime = gameTime?.ElapsedGameTime.TotalSeconds ?? 1;
                frameRate.Update(elapsedRealTime, 1.0 / elapsedRealTime);
                this["FPS"] = $"{1 / gameTime.ElapsedGameTime.TotalSeconds:0.0} - {frameRate.SmoothedValue:0.0}";
                FormattingOptions["FPS"] = frameRate.SmoothedValue < slowFps ? FormatOption.RegularRed : null;
            }
        }

        private sealed class GraphicsDebugInfo : DetailInfoBase
        {
            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    this["Clear Calls"] = $"{CurrentMetrics.ClearCount}";
                    this["Draw Calls"] = $"{CurrentMetrics.DrawCount}";
                    this["Primitives"] = $"{CurrentMetrics.PrimitiveCount}";
                    this["Textures"] = $"{CurrentMetrics.TextureCount}";
                    this["Sprites"] = $"{CurrentMetrics.SpriteCount}";
                    this["Targets"] = $"{CurrentMetrics.TargetCount}";
                    this["PixelShaders"] = $"{CurrentMetrics.PixelShaderCount}";
                    this["VertexShaders"] = $"{CurrentMetrics.VertexShaderCount}";
                }
                base.Update(gameTime);
            }

            public GraphicsMetrics CurrentMetrics;

            public GraphicsDebugInfo() : base(true)
            {
                FormattingOptions["GPU Information"] = FormatOption.Bold;
                this["GPU Information"] = null;
                this["Clear Calls"] = null;
                this["Draw Calls"] = null;
                this["Primitives"] = null;
                this["Textures"] = null;
                this["Sprites"] = null;
                this["Targets"] = null;
                this["PixelShaders"] = null;
                this["VertexShaders"] = null;
            }
        }
    }
}
