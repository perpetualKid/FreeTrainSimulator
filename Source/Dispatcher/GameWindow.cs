using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Dispatcher.PopupWindows;
using FreeTrainSimulator.Dispatcher.Settings;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Xna;

using GetText;
using GetText.WindowsForms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Dispatcher
{
    public partial class GameWindow : Game, IInputCapture
    {
        private readonly GraphicsDeviceManager graphicsDeviceManager;
        private readonly System.Windows.Forms.Form windowForm;
        private readonly CommonDebugInfo debugInfo;
        private readonly GraphicsDebugInfo graphicsDebugInfo = new GraphicsDebugInfo();

        private SpriteBatch spriteBatch;

        private bool syncing;
        private ScreenMode currentScreenMode;
        private System.Windows.Forms.Screen currentScreen;
        private Point windowPosition;
        private System.Drawing.Size windowSize;
        private readonly Point clientRectangleOffset;

        private readonly Action onClientSizeChanged;

        private WindowManager<PopupWindows.DispatcherWindowType> windowManager;
        private ContentArea contentArea;
        private int suppressCount;

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

        internal string StatusMessage { get; set; }

        internal DispatcherSettings Settings { get; }

        internal string LogFileName { get; }

        private Color BackgroundColor;

        internal Catalog Catalog { get; private set; }
        private readonly ObjectPropertiesStore store = new ObjectPropertiesStore();
        private readonly string windowTitle
;
        public GameWindow()
        {
            ImmutableArray<string> options = Environment.GetCommandLineArgs().Where(a => a.StartsWith('-') || a.StartsWith('/')).Select(a => a[1..]).ToImmutableArray();
            Settings = new DispatcherSettings(options);

            CatalogManager.SetCatalogDomainPattern(CatalogDomainPattern.AssemblyName, null, RuntimeInfo.LocalesFolder);

            if (Settings.UserSettings.Logging)
            {
                LogFileName = RuntimeInfo.LogFile(Settings.UserSettings.LoggingPath, Settings.LogFilename);
                LoggingUtil.InitLogging(LogFileName, Settings.UserSettings.LogErrorsOnly, false);
                Settings.Log();
                Trace.WriteLine(LoggingUtil.SeparatorLine);
            }

            windowForm = (System.Windows.Forms.Form)System.Windows.Forms.Control.FromHandle(Window.Handle);
            currentScreen = Settings.WindowScreen < System.Windows.Forms.Screen.AllScreens.Length
                ? System.Windows.Forms.Screen.AllScreens[Settings.WindowScreen]
                : System.Windows.Forms.Screen.PrimaryScreen;
            FontManager.ScalingFactor = (float)WindowManager.DisplayScalingFactor(currentScreen);

            LoadSettings();

            InitializeComponent();
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreparingDeviceSettings += GraphicsPreparingDeviceSettings;
            graphicsDeviceManager.PreferMultiSampling = Settings.UserSettings.MultisamplingCount > 0;
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
            LoadLanguage();
            SystemInfo.SetGraphicAdapterInformation(graphicsDeviceManager.GraphicsDevice.Adapter.Description);
            debugInfo = new CommonDebugInfo(this);
            windowForm.KeyPreview = true;// need to preview keys to enable Monogames TextInput handler, otherwise adding the main menu will break text input
        }

        private void WindowForm_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            e.Cancel = true;
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
            System.Windows.Forms.Screen newScreen = System.Windows.Forms.Screen.FromControl(windowForm);
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
            Settings.ColorSettings[setting] = colorName;
            contentArea?.UpdateColor(setting, ColorExtension.FromName(colorName));
            if (setting == ColorSetting.Background)
            {
                BackgroundColor = ColorExtension.FromName(colorName);
                (windowManager[PopupWindows.DispatcherWindowType.DebugScreen] as DebugScreen)?.UpdateBackgroundColor(BackgroundColor);
            }
        }

        internal void UpdateItemVisibilityPreference(MapContentType setting, bool enabled)
        {
            Settings.ViewSettings[setting] = enabled;
        }

        internal void UpdateLanguagePreference(string language)
        {
            Settings.UserSettings.Language = language;
            LoadLanguage();
        }

        private void LoadSettings()
        {
            windowSize.Width = (int)(currentScreen.WorkingArea.Size.Width * Math.Abs(Settings.WindowSettings[WindowSetting.Size][0]) / 100.0);
            windowSize.Height = (int)(currentScreen.WorkingArea.Size.Height * Math.Abs(Settings.WindowSettings[WindowSetting.Size][1]) / 100.0);

            windowPosition = PointExtension.ToPoint(Settings.WindowSettings[WindowSetting.Location]);
            windowPosition = windowPosition != PointExtension.EmptyPoint
                ? new Point(
                    currentScreen.WorkingArea.Left + (windowPosition.X * (currentScreen.WorkingArea.Size.Width - windowSize.Width) / 100),
                    currentScreen.WorkingArea.Top + (windowPosition.Y * (currentScreen.WorkingArea.Size.Height - windowSize.Height) / 100))
                : new Point(
                    currentScreen.WorkingArea.Left + ((currentScreen.WorkingArea.Size.Width - windowSize.Width) / 2),
                    currentScreen.WorkingArea.Top + ((currentScreen.WorkingArea.Size.Height - windowSize.Height) / 2));

            BackgroundColor = ColorExtension.FromName(Settings.ColorSettings[ColorSetting.Background]);
        }

        private void SaveSettings()
        {
            Settings.WindowSettings[WindowSetting.Size][0] = (int)Math.Round(100.0 * windowSize.Width / currentScreen.WorkingArea.Width);
            Settings.WindowSettings[WindowSetting.Size][1] = (int)Math.Round(100.0 * windowSize.Height / currentScreen.WorkingArea.Height);

            Settings.WindowSettings[WindowSetting.Location][0] = (int)Math.Max(0, Math.Round(100f * (windowPosition.X - currentScreen.Bounds.Left) / (currentScreen.WorkingArea.Width - windowSize.Width)));
            Settings.WindowSettings[WindowSetting.Location][1] = (int)Math.Max(0, Math.Round(100.0 * (windowPosition.Y - currentScreen.Bounds.Top) / (currentScreen.WorkingArea.Height - windowSize.Height)));
            Settings.WindowScreen = System.Windows.Forms.Screen.AllScreens.ToList().IndexOf(currentScreen);

            foreach (PopupWindows.DispatcherWindowType windowType in EnumExtension.GetValues<PopupWindows.DispatcherWindowType>())
            {
                if (windowManager.WindowInitialized(windowType))
                {
                    Settings.PopupLocations[windowType] = PointExtension.ToArray(windowManager[windowType].RelativeLocation);
                }
                if (windowType != PopupWindows.DispatcherWindowType.QuitWindow)
                    Settings.PopupStatus[windowType] = windowManager.WindowOpened(windowType);
            }

            if (null != contentArea)
            {
                string[] location = new string[] { $"{contentArea.CenterX}", $"{contentArea.CenterY}", $"{contentArea.Scale}" };
                Settings.LastLocation = location;
            }
            string[] routeSelection = null;
            string[] pathSelection = null;
            if (selectedFolder != null)
            {
                routeSelection = selectedRoute != null ?
                    (new string[] { selectedFolder.Name, selectedRoute.Name }) :
                    (new string[] { selectedFolder.Name });

                pathSelection = new string[] { string.IsNullOrEmpty(PathEditor?.FilePath) ? string.Empty : PathEditor.FilePath };
            }
            Settings.RouteSelection = routeSelection;
            Settings.PathSelection = pathSelection;
            Settings.Save();
            Settings.UserSettings.Save();
        }

        private void LoadLanguage()
        {
            Localizer.Revert(windowForm, store);
            CatalogManager.Reset();

            if (!string.IsNullOrEmpty(Settings.UserSettings.Language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(Settings.UserSettings.Language);
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
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = Settings.UserSettings.MultisamplingCount;
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
            Task loadFolders = LoadFolders();
            InputSettings.Initialize();

            spriteBatch = new SpriteBatch(GraphicsDevice);

            UserCommandController<UserCommand> userCommandController = new UserCommandController<UserCommand>();

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
                    windowManager[PopupWindows.DispatcherWindowType.DebugScreen].ToggleVisibility();
            });
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, MouseDragging);
            userCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, MouseWheel);
            userCommandController.AddEvent(CommonUserCommand.PointerPressed, EditTrainPath);
            userCommandController.AddEvent(UserCommand.DisplayLocationWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[PopupWindows.DispatcherWindowType.LocationWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayHelpWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[PopupWindows.DispatcherWindowType.HelpWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayTrackNodeInfoWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[PopupWindows.DispatcherWindowType.TrackNodeInfoWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayTrackItemInfoWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[PopupWindows.DispatcherWindowType.TrackItemInfoWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplaySettingsWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[PopupWindows.DispatcherWindowType.SettingsWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayLogWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[PopupWindows.DispatcherWindowType.LogWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayTrainPathWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[PopupWindows.DispatcherWindowType.TrainPathWindow].ToggleVisibility();
            });
            #endregion

            #region popup windows
            windowManager = WindowManager.Initialize<UserCommand, PopupWindows.DispatcherWindowType>(this, userCommandController.AddTopLayerController());
            windowManager[PopupWindows.DispatcherWindowType.StatusWindow] = new StatusTextWindow(windowManager, Settings.PopupLocations[PopupWindows.DispatcherWindowType.StatusWindow].ToPoint());
            windowManager[PopupWindows.DispatcherWindowType.AboutWindow] = new AboutWindow(windowManager, Settings.PopupLocations[PopupWindows.DispatcherWindowType.AboutWindow].ToPoint());
            windowManager.SetLazyWindows(PopupWindows.DispatcherWindowType.QuitWindow, new Lazy<FormBase>(() =>
            {
                QuitWindow quitWindow = new QuitWindow(windowManager, Settings.PopupLocations[PopupWindows.DispatcherWindowType.QuitWindow].ToPoint());
                quitWindow.OnQuitGame += QuitWindow_OnQuitGame;
                quitWindow.OnWindowClosed += QuitWindow_OnWindowClosed;
                quitWindow.OnPrintScreen += QuitWindow_OnPrintScreen;
                return quitWindow;
            }));

            windowManager.SetLazyWindows(PopupWindows.DispatcherWindowType.DebugScreen, new Lazy<FormBase>(() =>
            {
                DebugScreen debugWindow = new DebugScreen(windowManager, Settings, BackgroundColor);
                debugWindow.SetInformationProvider(DebugScreenInformation.Common, debugInfo);
                debugWindow.SetInformationProvider(DebugScreenInformation.Graphics, graphicsDebugInfo);
                debugWindow.SetInformationProvider(DebugScreenInformation.Route, ContentArea?.Content);
                OnContentAreaChanged += debugWindow.GameWindow_OnContentAreaChanged;
                return debugWindow;
            }));

            windowManager.SetLazyWindows(PopupWindows.DispatcherWindowType.LocationWindow, new Lazy<FormBase>(() =>
            {
                LocationWindow locationWindow = new LocationWindow(windowManager, Settings, contentArea, Settings.PopupLocations[PopupWindows.DispatcherWindowType.LocationWindow].ToPoint());
                OnContentAreaChanged += locationWindow.GameWindow_OnContentAreaChanged;
                return locationWindow;
            }));
            windowManager.SetLazyWindows(PopupWindows.DispatcherWindowType.HelpWindow, new Lazy<FormBase>(() =>
            {
                return new HelpWindow(windowManager, Settings.PopupLocations[PopupWindows.DispatcherWindowType.HelpWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(PopupWindows.DispatcherWindowType.TrackNodeInfoWindow, new Lazy<FormBase>(() =>
            {
                TrackNodeInfoWindow trackInfoWindow = new TrackNodeInfoWindow(windowManager, contentArea, Settings.PopupLocations[PopupWindows.DispatcherWindowType.TrackNodeInfoWindow].ToPoint());
                OnContentAreaChanged += trackInfoWindow.GameWindow_OnContentAreaChanged;
                return trackInfoWindow;
            }));
            windowManager.SetLazyWindows(PopupWindows.DispatcherWindowType.TrackItemInfoWindow, new Lazy<FormBase>(() =>
            {
                TrackItemInfoWindow trackInfoWindow = new TrackItemInfoWindow(windowManager, contentArea, Settings.PopupLocations[PopupWindows.DispatcherWindowType.TrackItemInfoWindow].ToPoint());
                OnContentAreaChanged += trackInfoWindow.GameWindow_OnContentAreaChanged;
                return trackInfoWindow;
            }));
            windowManager.SetLazyWindows(PopupWindows.DispatcherWindowType.SettingsWindow, new Lazy<FormBase>(() =>
            {
                SettingsWindow settingsWindow = new SettingsWindow(windowManager, Settings, contentArea, Settings.PopupLocations[PopupWindows.DispatcherWindowType.SettingsWindow].ToPoint());
                OnContentAreaChanged += settingsWindow.GameWindow_OnContentAreaChanged;
                return settingsWindow;
            }));
            windowManager.SetLazyWindows(PopupWindows.DispatcherWindowType.LogWindow, new Lazy<FormBase>(() =>
            {
                return new LoggingWindow(windowManager, LogFileName, Settings.PopupLocations[PopupWindows.DispatcherWindowType.LogWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(PopupWindows.DispatcherWindowType.TrainPathWindow, new Lazy<FormBase>(() =>
            {
                TrainPathWindow trainPathDetailWindow = new TrainPathWindow(windowManager, Settings, Settings.PopupLocations[PopupWindows.DispatcherWindowType.TrainPathWindow].ToPoint());
                OnContentAreaChanged += trainPathDetailWindow.GameWindow_OnContentAreaChanged;
                return trainPathDetailWindow;
            }));
            #endregion

            windowManager.OnModalWindow += WindowManager_OnModalWindow;
            Components.Add(windowManager);
            base.Initialize();

            await loadFolders.ConfigureAwait(false);
            await PreSelectRoute(Settings.RouteSelection, Settings.PathSelection).ConfigureAwait(false);
            ContentArea?.PresetPosition(Settings.LastLocation);
            if (Settings.RestoreLastView)
            {
                foreach (PopupWindows.DispatcherWindowType windowType in EnumExtension.GetValues<PopupWindows.DispatcherWindowType>())
                {
                    if (Settings.PopupStatus[windowType])
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
            Components.Add(new InsetComponent(this, BackgroundColor, new Vector2(-10, 30)));
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
            GraphicsDevice.Clear(BackgroundColor);
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
