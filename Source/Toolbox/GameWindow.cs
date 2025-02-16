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
                    value.ResetSize(Window.ClientBounds.Size, 60);
                    Components.Add(value);
                    value.Enabled = true;
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
            InitializeComponent();
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
            windowForm.KeyPreview = true;// need to preview keys to enable Monogames TextInput handler, otherwise adding the main menu will break text input
        }

        private void GameWindow_Exiting(object sender, ExitingEventArgs e)
        {
            e.Cancel = waitOnExit;
        }

        private void WindowForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            waitOnExit = true;
            PrepareExitApplication();
        }

        #region window size/position handling
        private void WindowForm_ClientSizeChanged(object sender, EventArgs e)
        {
            if (syncing)
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
            WindowForm_ClientSizeChanged(sender, e);
        }

        internal void UpdateColorPreference(ColorSetting setting, string colorName)
        {
            ToolboxSettings.ColorSettings[setting] = colorName;
            contentArea?.UpdateColor(setting, ColorExtension.FromName(colorName));
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
                }
                if (windowType != ToolboxWindowType.QuitWindow)
                    ToolboxSettings.PopupStatus[windowType] = windowManager.WindowOpened(windowType);
            }

            ToolboxSettings.WindowScreen = Screen.AllScreens.ToList().IndexOf(currentScreen);
            ToolboxSettings.ContentPosition = contentArea?.CenterPoint ?? PointD.None;
            ToolboxSettings.ContentScale = contentArea?.Scale ?? 1;

            ToolboxSettings.Folder = selectedFolder?.Id;
            ToolboxSettings.RouteId = selectedRoute?.Id;
            ToolboxSettings.PathId = PathEditor?.PathId;

            //            ProfileSettingModelHandler<ProfileUserSettingsModel>.SetValueByName(ToolboxUserSettings, "MultiSamplingCount", 8);

            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(loadRouteSemaphore, true).ConfigureAwait(false);
            await currentProfile.UpdateSettingsModel(ToolboxSettings, ctsProfileLoading.Token).ConfigureAwait(false);
            await currentProfile.UpdateSettingsModel(ToolboxUserSettings, ctsProfileLoading.Token).ConfigureAwait(false);

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
                switch (targetMode)
                {
                    case ScreenMode.Windowed:
                        if (targetMode != currentScreenMode)
                            Window.Position = windowPosition;
                        windowForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                        windowForm.Size = windowSize;
                        graphicsDeviceManager.PreferredBackBufferWidth = windowSize.Width;
                        graphicsDeviceManager.PreferredBackBufferHeight = windowSize.Height;
                        graphicsDeviceManager.ApplyChanges();
                        break;
                    case ScreenMode.WindowedFullscreen:
                        graphicsDeviceManager.PreferredBackBufferWidth = currentScreen.WorkingArea.Width - clientRectangleOffset.X;
                        graphicsDeviceManager.PreferredBackBufferHeight = currentScreen.WorkingArea.Height - clientRectangleOffset.Y;
                        windowForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
                        Window.Position = new Point(currentScreen.WorkingArea.Location.X, currentScreen.WorkingArea.Location.Y);
                        graphicsDeviceManager.ApplyChanges();
                        break;
                    case ScreenMode.BorderlessFullscreen:
                        graphicsDeviceManager.PreferredBackBufferWidth = currentScreen.Bounds.Width;
                        graphicsDeviceManager.PreferredBackBufferHeight = currentScreen.Bounds.Height;
                        graphicsDeviceManager.ApplyChanges();
                        windowForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                        Window.Position = new Point(currentScreen.Bounds.X, currentScreen.Bounds.Y);
                        graphicsDeviceManager.ApplyChanges();
                        break;
                }
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
            KeyboardInputGameComponent keyboardInputGameComponent = new KeyboardInputGameComponent(this);
            Components.Add(keyboardInputGameComponent);
            KeyboardInputHandler<UserCommand> keyboardInput = new KeyboardInputHandler<UserCommand>();
            keyboardInput.Initialize(InputSettings.UserCommands, keyboardInputGameComponent, userCommandController);

            MouseInputGameComponent mouseInputGameComponent = new MouseInputGameComponent(this);
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
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, MouseDragging);
            userCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, MouseWheel);
            userCommandController.AddEvent(UserCommand.DisplayLocationWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ToolboxWindowType.LocationWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayHelpWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ToolboxWindowType.HelpWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayTrackNodeInfoWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ToolboxWindowType.TrackNodeInfoWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayTrackItemInfoWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ToolboxWindowType.TrackItemInfoWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplaySettingsWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ToolboxWindowType.SettingsWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayLogWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ToolboxWindowType.LogWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayTrainPathWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ToolboxWindowType.TrainPathWindow].ToggleVisibility();
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

            windowManager.SetLazyWindows(ToolboxWindowType.LocationWindow, new Lazy<FormBase>(() =>
            {
                LocationWindow locationWindow = new LocationWindow(windowManager, ToolboxSettings, contentArea, ToolboxSettings.PopupLocations[ToolboxWindowType.LocationWindow].ToPoint());
                OnContentAreaChanged += locationWindow.GameWindow_OnContentAreaChanged;
                return locationWindow;
            }));
            windowManager.SetLazyWindows(ToolboxWindowType.HelpWindow, new Lazy<FormBase>(() =>
            {
                return new HelpWindow(windowManager, ToolboxSettings.PopupLocations[ToolboxWindowType.HelpWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(ToolboxWindowType.TrackNodeInfoWindow, new Lazy<FormBase>(() =>
            {
                TrackNodeInfoWindow trackInfoWindow = new TrackNodeInfoWindow(windowManager, contentArea, ToolboxSettings.PopupLocations[ToolboxWindowType.TrackNodeInfoWindow].ToPoint());
                OnContentAreaChanged += trackInfoWindow.GameWindow_OnContentAreaChanged;
                return trackInfoWindow;
            }));
            windowManager.SetLazyWindows(ToolboxWindowType.TrackItemInfoWindow, new Lazy<FormBase>(() =>
            {
                TrackItemInfoWindow trackInfoWindow = new TrackItemInfoWindow(windowManager, contentArea, ToolboxSettings.PopupLocations[ToolboxWindowType.TrackItemInfoWindow].ToPoint());
                OnContentAreaChanged += trackInfoWindow.GameWindow_OnContentAreaChanged;
                return trackInfoWindow;
            }));
            windowManager.SetLazyWindows(ToolboxWindowType.SettingsWindow, new Lazy<FormBase>(() =>
            {
                SettingsWindow settingsWindow = new SettingsWindow(windowManager, ToolboxSettings, ToolboxUserSettings, contentArea, ToolboxSettings.PopupLocations[ToolboxWindowType.SettingsWindow].ToPoint());
                OnContentAreaChanged += settingsWindow.GameWindow_OnContentAreaChanged;
                return settingsWindow;
            }));
            windowManager.SetLazyWindows(ToolboxWindowType.LogWindow, new Lazy<FormBase>(() =>
            {
                return new LoggingWindow(windowManager, LogFileName, ToolboxSettings.PopupLocations[ToolboxWindowType.LogWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(ToolboxWindowType.TrainPathWindow, new Lazy<FormBase>(() =>
            {
                TrainPathWindow trainPathDetailWindow = new TrainPathWindow(windowManager, ToolboxSettings, ToolboxSettings.PopupLocations[ToolboxWindowType.TrainPathWindow].ToPoint());
                OnContentAreaChanged += trainPathDetailWindow.GameWindow_OnContentAreaChanged;
                return trainPathDetailWindow;
            }));
            #endregion

            windowManager.OnModalWindow += WindowManager_OnModalWindow;
            Components.Add(windowManager);
            base.Initialize();

            if (!(await loadFolders.ConfigureAwait(false)))
            {
                // content may need updates
                MessageBox.Show($"In an effort to optimize content, {RuntimeInfo.ProductName} will need to analyze existing content files and folders." + Environment.NewLine + Environment.NewLine +
                    $"Please close {RuntimeInfo.ApplicationName}, and use the Menu-application to review current content folder settings for further analysis.", "Please read!");
                return;
            }
            await PreSelectRoute(ToolboxSettings.Folder, ToolboxSettings.RouteId, ToolboxSettings.PathId).ConfigureAwait(false);
            ContentArea?.PresetPosition(ToolboxSettings.ContentPosition, ToolboxSettings.ContentScale);
            if (ToolboxSettings.RestoreLastView)
            {
                foreach (ToolboxWindowType windowType in EnumExtension.GetValues<ToolboxWindowType>())
                {
                    if (ToolboxSettings.PopupStatus[windowType])
                        windowManager[windowType].Open();
                }
            }
        }

        private void WindowManager_OnModalWindow(object sender, ModalWindowEventArgs e)
        {
            mainmenu.Enabled = !e.ModalWindowOpen;

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

        protected override void Draw(GameTime gameTime)
        {
            debugInfo.Update(gameTime);
            GraphicsDevice.Clear(backgroundColor);
            base.Draw(gameTime);

            graphicsDebugInfo.CurrentMetrics = GraphicsDevice.Metrics;
            graphicsDebugInfo.Update(gameTime);
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
