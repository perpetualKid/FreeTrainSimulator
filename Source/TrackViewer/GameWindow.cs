using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using GetText;
using GetText.WindowsForms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Common.Logging;
using Orts.Models.Simplified;
using Orts.Settings;
using Orts.View;
using Orts.View.DrawableComponents;
using Orts.View.Track;
using Orts.View.Track.Shapes;
using Orts.View.Xna;

namespace Orts.TrackViewer
{
    public partial class GameWindow : Game, IInputCapture
    {
        private readonly GraphicsDeviceManager graphicsDeviceManager;
        private readonly System.Windows.Forms.Form windowForm;
        private readonly SmoothedData frameRate;

        private SpriteBatch spriteBatch;

        private bool syncing;
        private ScreenMode currentScreenMode;
        private System.Windows.Forms.Screen currentScreen;
        private Point windowPosition;
        private System.Drawing.Size windowSize;
        private Point clientRectangleOffset;
        private Vector2 centerPoint;

        private readonly Action onClientSizeChanged;

        private ContentArea contentArea;

        internal ContentArea ContentArea
        {
            get => contentArea;
            set => windowForm.Invoke((System.Windows.Forms.MethodInvoker)delegate {
                if (value != null)
                {
                    value.ResetSize(Window.ClientBounds.Size, 60);
                    Components.Add(value);
                    value.Enabled = true;
                    Window.Title = windowTitle + Catalog.GetString($" Route: {value.RouteName}");
                }
                else
                {
                    Components.Remove(contentArea);
                    Window.Title = windowTitle;
                }
                if (contentArea != null)
                    contentArea.Enabled = false;
                contentArea = value;
            });
        }

        internal string StatusMessage { get; set; }

        internal UserSettings Settings { get; }

        internal string LogFileName { get; }

        private Color BackgroundColor;
        internal string backgroundColor;

        #region preferences
        private readonly EnumArray<string, ColorSetting> colorPreferences = new EnumArray<string, ColorSetting>();
        private TrackViewerViewSettings viewSettings;

        #endregion

        internal Catalog Catalog { get; private set; }
        internal Catalog CommonCatalog { get; private set; }
        private readonly ObjectPropertiesStore store = new ObjectPropertiesStore();
        private readonly string windowTitle
;
        public GameWindow()
        {
            IEnumerable<string> options = Environment.GetCommandLineArgs().Where(a => a.StartsWith("-", StringComparison.OrdinalIgnoreCase) || a.StartsWith("/", StringComparison.OrdinalIgnoreCase)).Select(a => a.Substring(1));
            Settings = new UserSettings(options);

            if (Settings.Logging)
            {
                LogFileName = System.IO.Path.Combine(Settings.LoggingPath, LoggingUtil.CustomizeLogFileName(Settings.TrackViewer.LogFilename));
                LoggingUtil.InitLogging(LogFileName, Settings.LogErrorsOnly, false);
                Settings.TrackViewer.Log();
                Trace.WriteLine(LoggingUtil.SeparatorLine);
            }
            LoadSettings();
            frameRate = new SmoothedData();
            windowForm = (System.Windows.Forms.Form)System.Windows.Forms.Control.FromHandle(Window.Handle);
            currentScreen = System.Windows.Forms.Screen.PrimaryScreen;

            InitializeComponent();
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreparingDeviceSettings += GraphicsPreparingDeviceSettings;
            graphicsDeviceManager.PreferMultiSampling = Settings.MultisamplingCount > 0;
            IsMouseVisible = true;

            // Set title to show revision or build info.
            windowTitle = $"{RuntimeInfo.ProductName} {VersionInfo.Version}";
#if DEBUG
            windowTitle += " (debug)";
#endif
#if NETCOREAPP
            windowTitle += " [.NET Core]";
#elif NETFRAMEWORK
            windowTitle += " [.NET Classic]";
#endif

            Window.Title = windowTitle;
            Window.AllowUserResizing = true;

            Window.ClientSizeChanged += Window_ClientSizeChanged; // not using the GameForm event as it does not raise when Window is moved (ie to another screeen) using keyboard shortcut

            //graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;
            //IsFixedTimeStep = false;
            //TargetElapsedTime = TimeSpan.FromMilliseconds(5);
            windowPosition = new Point(
                currentScreen.WorkingArea.Left + (currentScreen.WorkingArea.Size.Width - windowSize.Width) / 2,
                currentScreen.WorkingArea.Top + (currentScreen.WorkingArea.Size.Height - windowSize.Height) / 2);
            clientRectangleOffset = new Point(windowForm.Width - windowForm.ClientRectangle.Width, windowForm.Height - windowForm.ClientRectangle.Height);
            Window.Position = windowPosition;

            SetScreenMode(currentScreenMode);

            windowForm.LocationChanged += WindowForm_LocationChanged;
            windowForm.ClientSizeChanged += WindowForm_ClientSizeChanged;

            // using reflection to be able to trigger ClientSizeChanged event manually as this is not 
            // reliably raised otherwise with the resize functionality below in SetScreenMode
            MethodInfo m = Window.GetType().GetMethod("OnClientSizeChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            onClientSizeChanged = (Action)Delegate.CreateDelegate(typeof(Action), Window, m);

            Exiting += GameWindow_Exiting;
            LoadLanguage();

        }

        private void GameWindow_Exiting(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            ContentArea?.UpdateSize(Window.ClientBounds.Size);
        }

        #region window size/position handling
        private void WindowForm_ClientSizeChanged(object sender, EventArgs e)
        {
            centerPoint = new Vector2(Window.ClientBounds.Size.X / 2, Window.ClientBounds.Size.Y / 2);
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
                    currentScreen.WorkingArea.Left + (currentScreen.WorkingArea.Size.Width - windowSize.Width) / 2,
                    currentScreen.WorkingArea.Top + (currentScreen.WorkingArea.Size.Height - windowSize.Height) / 2);
            }
        }

        private void WindowForm_LocationChanged(object sender, EventArgs e)
        {
            WindowForm_ClientSizeChanged(sender, e);
        }

        internal void UpdateColorPreference(ColorSetting setting, string colorName)
        {
            colorPreferences[setting] = colorName;
            contentArea?.UpdateColor(setting, ColorExtension.FromName(colorName));
            if (setting == ColorSetting.Background)
            {
                backgroundColor = colorName;
                BackgroundColor = ColorExtension.FromName(colorName);
            }
        }

        internal void UpdateItemVisibilityPreference(TrackViewerViewSettings setting, bool enabled)
        {
            viewSettings = enabled ? viewSettings | setting : viewSettings & ~setting;
            contentArea?.UpdateItemVisiblity(viewSettings);
        }

        internal void UpdateLanguagePreference(string language)
        {
            Settings.Language = language;
            LoadLanguage();
        }

        private void LoadSettings()
        {
            windowSize = new System.Drawing.Size(Settings.TrackViewer.WindowSize[0], Settings.TrackViewer.WindowSize[1]);

            colorPreferences[ColorSetting.Background] = Settings.TrackViewer.ColorBackground;
            colorPreferences[ColorSetting.RailTrack] = Settings.TrackViewer.ColorRailTrack;
            colorPreferences[ColorSetting.RailTrackEnd] = Settings.TrackViewer.ColorRailTrackEnd;
            colorPreferences[ColorSetting.RailTrackJunction] = Settings.TrackViewer.ColorRailTrackJunction;
            colorPreferences[ColorSetting.RailTrackCrossing] = Settings.TrackViewer.ColorRailTrackCrossing;
            colorPreferences[ColorSetting.RailLevelCrossing] = Settings.TrackViewer.ColorRailLevelCrossing;
            colorPreferences[ColorSetting.RoadTrack] = Settings.TrackViewer.ColorRoadTrack;
            colorPreferences[ColorSetting.RoadTrackEnd] = Settings.TrackViewer.ColorRoadTrackEnd;
            colorPreferences[ColorSetting.RoadLevelCrossing] = Settings.TrackViewer.ColorRoadLevelCrossing;
            colorPreferences[ColorSetting.RoadCarSpawner] = Settings.TrackViewer.ColorRoadCarSpawner;
            colorPreferences[ColorSetting.SignalItem] = Settings.TrackViewer.ColorSignalItem;
            colorPreferences[ColorSetting.PlatformItem] = Settings.TrackViewer.ColorPlatformItem;
            colorPreferences[ColorSetting.SidingItem] = Settings.TrackViewer.ColorSidingItem;
            colorPreferences[ColorSetting.SpeedPostItem] = Settings.TrackViewer.ColorSpeedpostItem;
            colorPreferences[ColorSetting.HazardItem] = Settings.TrackViewer.ColorHazardItem;
            colorPreferences[ColorSetting.PickupItem] = Settings.TrackViewer.ColorPickupItem;
            colorPreferences[ColorSetting.SoundRegionItem] = Settings.TrackViewer.ColorSoundRegionItem;
            BackgroundColor = ColorExtension.FromName(colorPreferences[ColorSetting.Background]);
            viewSettings = Settings.TrackViewer.ViewSettings;

        }

        private void SaveSettings()
        {
            Settings.TrackViewer.WindowSize[0] = windowSize.Width;
            Settings.TrackViewer.WindowSize[1] = windowSize.Height;

            Settings.TrackViewer.ColorBackground = colorPreferences[ColorSetting.Background];
            Settings.TrackViewer.ColorRailTrack = colorPreferences[ColorSetting.RailTrack];
            Settings.TrackViewer.ColorRailTrackEnd = colorPreferences[ColorSetting.RailTrackEnd];
            Settings.TrackViewer.ColorRailTrackJunction = colorPreferences[ColorSetting.RailTrackJunction];
            Settings.TrackViewer.ColorRailTrackCrossing = colorPreferences[ColorSetting.RailTrackCrossing];
            Settings.TrackViewer.ColorRailLevelCrossing = colorPreferences[ColorSetting.RailLevelCrossing];
            Settings.TrackViewer.ColorRoadTrack = colorPreferences[ColorSetting.RoadTrack];
            Settings.TrackViewer.ColorRoadTrackEnd = colorPreferences[ColorSetting.RoadTrackEnd];
            Settings.TrackViewer.ColorRoadLevelCrossing = colorPreferences[ColorSetting.RoadLevelCrossing];
            Settings.TrackViewer.ColorRoadCarSpawner = colorPreferences[ColorSetting.RoadCarSpawner];
            Settings.TrackViewer.ColorSignalItem = colorPreferences[ColorSetting.SignalItem];
            Settings.TrackViewer.ColorPlatformItem = colorPreferences[ColorSetting.PlatformItem];
            Settings.TrackViewer.ColorSidingItem = colorPreferences[ColorSetting.SidingItem];
            Settings.TrackViewer.ColorSpeedpostItem = colorPreferences[ColorSetting.SpeedPostItem];
            Settings.TrackViewer.ColorHazardItem = colorPreferences[ColorSetting.HazardItem];
            Settings.TrackViewer.ColorPickupItem = colorPreferences[ColorSetting.PickupItem];
            Settings.TrackViewer.ColorSoundRegionItem = colorPreferences[ColorSetting.SoundRegionItem];
            Settings.TrackViewer.ViewSettings = viewSettings;
            if (null != contentArea)
            {
                string[] location = new string[] { $"{contentArea.CenterX}", $"{contentArea.CenterY}", $"{contentArea.Scale}" };
                Settings.TrackViewer.LastLocation = location;
                    }
            string[] routeSelection = null;
            if (selectedFolder != null)
            {
                if (selectedRoute != null)
                    routeSelection = new string[] { selectedFolder.Name, selectedRoute.Name };
                else
                    routeSelection = new string[] { selectedFolder.Name };
            }
            Settings.TrackViewer.RouteSelection = routeSelection;
            Settings.TrackViewer.Save();
        }

        private void LoadLanguage()
        {
            Localizer.Revert(windowForm, store);

            if (!string.IsNullOrEmpty(Settings.Language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(Settings.Language);
                }
                catch (CultureNotFoundException exception)
                {
                    System.Diagnostics.Trace.WriteLine(exception.Message);
                }
            }
            else
            {
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InstalledUICulture;
            }
            Catalog = new Catalog("TrackViewer", RuntimeInfo.LocalesFolder);
            CommonCatalog = new Catalog("Orts.Common", RuntimeInfo.LocalesFolder);
            Localizer.Localize(windowForm, Catalog, store);
        }

        private void GraphicsPreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;
            e.GraphicsDeviceInformation.PresentationParameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = Settings.MultisamplingCount;
        }

        private void SetScreenMode(ScreenMode targetMode)
        {
            syncing = true;
            windowForm.Invoke((System.Windows.Forms.MethodInvoker)delegate {
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
                        statusbar.UpdateStatusbarVisibility(true);
                        break;
                    case ScreenMode.WindowedFullscreen:
                        graphicsDeviceManager.PreferredBackBufferWidth = currentScreen.WorkingArea.Width - clientRectangleOffset.X;
                        graphicsDeviceManager.PreferredBackBufferHeight = currentScreen.WorkingArea.Height - clientRectangleOffset.Y;
                        windowForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
                        Window.Position = new Point(currentScreen.WorkingArea.Location.X, currentScreen.WorkingArea.Location.Y);
                        graphicsDeviceManager.ApplyChanges();
                        statusbar.UpdateStatusbarVisibility(false);
                        break;
                    case ScreenMode.BorderlessFullscreen:
                        graphicsDeviceManager.PreferredBackBufferWidth = currentScreen.Bounds.Width;
                        graphicsDeviceManager.PreferredBackBufferHeight = currentScreen.Bounds.Height;
                        graphicsDeviceManager.ApplyChanges();
                        windowForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                        Window.Position = new Point(currentScreen.Bounds.X, currentScreen.Bounds.Y);
                        graphicsDeviceManager.ApplyChanges();
                        statusbar.UpdateStatusbarVisibility(false);
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
            List<Task> initTasks = new List<Task>()
            {
                LoadFolders(),
            };
            spriteBatch = new SpriteBatch(GraphicsDevice);
            TextDrawShape.Initialize(this, spriteBatch);
            BasicShapes.Initialize(spriteBatch);

            InputGameComponent inputComponent = new InputGameComponent(this);
            Components.Add(inputComponent);
            inputComponent.AddKeyEvent(Keys.F, KeyModifiers.None, InputGameComponent.KeyEventType.KeyPressed, (keys, modifiers) => new Thread(GameWindowThread).Start());
            inputComponent.AddKeyEvent(Keys.Enter, KeyModifiers.Shift, InputGameComponent.KeyEventType.KeyPressed, ChangeScreenMode);
            inputComponent.AddKeyEvent(Keys.Q, KeyModifiers.None, InputGameComponent.KeyEventType.KeyPressed, CloseWindow);
            //inputComponent.AddKeyEvent(Keys.F4, KeyModifiers.Alt, InputGameComponent.KeyEventType.KeyPressed, ExitApplication);
            inputComponent.AddKeyEvent(Keys.Left, KeyModifiers.None, InputGameComponent.KeyEventType.KeyDown, MoveByKey);
            inputComponent.AddKeyEvent(Keys.Right, KeyModifiers.None, InputGameComponent.KeyEventType.KeyDown, MoveByKey);
            inputComponent.AddKeyEvent(Keys.Up, KeyModifiers.None, InputGameComponent.KeyEventType.KeyDown, MoveByKey);
            inputComponent.AddKeyEvent(Keys.Down, KeyModifiers.None, InputGameComponent.KeyEventType.KeyDown, MoveByKey);
            inputComponent.AddKeyEvent(Keys.Left, KeyModifiers.Control, InputGameComponent.KeyEventType.KeyDown, MoveByKey);
            inputComponent.AddKeyEvent(Keys.Right, KeyModifiers.Control, InputGameComponent.KeyEventType.KeyDown, MoveByKey);
            inputComponent.AddKeyEvent(Keys.Up, KeyModifiers.Control, InputGameComponent.KeyEventType.KeyDown, MoveByKey);
            inputComponent.AddKeyEvent(Keys.Down, KeyModifiers.Control, InputGameComponent.KeyEventType.KeyDown, MoveByKey);
            inputComponent.AddKeyEvent(Keys.OemPlus, KeyModifiers.None, InputGameComponent.KeyEventType.KeyDown, ZoomIn);
            inputComponent.AddKeyEvent(Keys.PageUp, KeyModifiers.None, InputGameComponent.KeyEventType.KeyDown, ZoomIn);
            inputComponent.AddKeyEvent(Keys.OemMinus, KeyModifiers.None, InputGameComponent.KeyEventType.KeyDown, ZoomOut);
            inputComponent.AddKeyEvent(Keys.PageDown, KeyModifiers.None, InputGameComponent.KeyEventType.KeyDown, ZoomOut);
            inputComponent.AddKeyEvent(Keys.R, KeyModifiers.None, InputGameComponent.KeyEventType.KeyPressed, ResetZoomAndLocation);
            inputComponent.AddMouseEvent(InputGameComponent.MouseMovedEventType.MouseMoved, MouseMove);
            inputComponent.AddMouseEvent(InputGameComponent.MouseWheelEventType.MouseWheelChanged, MouseWheel);
            inputComponent.AddMouseEvent(InputGameComponent.MouseButtonEventType.LeftButtonReleased, MouseButtonUp);
            inputComponent.AddMouseEvent(InputGameComponent.MouseButtonEventType.RightButtonDown, MouseButtonDown);
            inputComponent.AddMouseEvent(InputGameComponent.MouseMovedEventType.MouseMovedLeftButtonDown, MouseDragging);

            base.Initialize();

            await Task.WhenAll(initTasks).ConfigureAwait(false);
            await PreSelectRoute(Settings.TrackViewer.RouteSelection).ConfigureAwait(false);
            ContentArea?.PresetPosition(Settings.TrackViewer.LastLocation);
        }

        private static void GameWindowThread(object data)
        {
            using (GameWindow game = new GameWindow())
                game.Run();
        }

        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            BasicShapes.LoadContent(GraphicsDevice);
            DigitalClockComponent clock = new DigitalClockComponent(this, TimeType.RealWorldLocalTime,
                new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel), Color.White, new Vector2(-200, -100), true);
            Components.Add(clock);
            ScaleRulerComponent scaleRuler = new ScaleRulerComponent(this, new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 14, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel), Color.Black, new Vector2(-20, -55));
            Components.Add(scaleRuler);
            Components.Add(new InsetComponent(this, BackgroundColor, new Vector2(-10, 30)));
            Components.Add(new WorldCoordinatesComponent(this, new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 20, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel), Color.Blue, new Vector2(40, 40)));
        }

        protected override void Update(GameTime gameTime)
        {
            // TODO: Add your update logic here
            base.Update(gameTime);
        }

        private System.Drawing.Font drawfont = new System.Drawing.Font("Segoe UI", (int)Math.Round(25.0), System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);

        int drawTime;

        public bool InputCaptured { get; internal set; }

        protected override void Draw(GameTime gameTime)
        {
            double elapsedRealTime = gameTime.ElapsedGameTime.TotalSeconds;
            frameRate.Update(elapsedRealTime, 1.0 / elapsedRealTime);
            if ((int)gameTime.TotalGameTime.TotalSeconds > drawTime)
            {
                drawTime = (int)gameTime.TotalGameTime.TotalSeconds;
                statusbar.toolStripStatusLabel1.Text = $"{1 / gameTime.ElapsedGameTime.TotalSeconds:0.0} - {frameRate.SmoothedValue:0.0}";

            }
            GraphicsDevice.Clear(BackgroundColor);
            statusbar.toolStripStatusLabel2.Text = contentArea?.Scale.ToString() ?? string.Empty;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, null, null);
            //if (contentArea == null)
            //{
            //    BasicShapes.DrawTexture(BasicTextureType.Pickup, new Vector2(180, 180), 0, -1, Color.Green, false, false, true);
            //    BasicShapes.DrawTexture(BasicTextureType.PlayerTrain, new Vector2(240, 180), 0, -3, Color.Blue, true, false, true);
            //    BasicShapes.DrawTexture(BasicTextureType.Ring, new Vector2(80, 220), 0, -0.5f, Color.Yellow, true, false, false);
            //    BasicShapes.DrawTexture(BasicTextureType.Circle, new Vector2(80, 220), 0, -0.2f, Color.Red, true, false, false);
            //    BasicShapes.DrawTexture(BasicTextureType.RingCrossed, new Vector2(240, 220), 0.0f, -2, Color.Yellow, true, false, false);
            //    BasicShapes.DrawTexture(BasicTextureType.Disc, new Vector2(340, 220), 0, -1, Color.Red, true, false, false);

            //    BasicShapes.DrawArc(3, Color.Green, new Vector2(330, 330), 120, 90 * Math.PI / 180, 90, 0);
            //    BasicShapes.DrawDashedLine(2, Color.Aqua, new Vector2(330, 330), new Vector2(450, 330));
            //    TextDrawShape.DrawString(new Vector2(200, 450), Color.Red, "Test Message", drawfont, Vector2.One);
            //    TextDrawShape.DrawString(new Vector2(200, 500), Color.Lime, gameTime.TotalGameTime.TotalSeconds.ToString(), drawfont, Vector2.One);

            //    BasicShapes.DrawTexture(BasicTextureType.Disc, new Vector2(480, 180), 0, -2f, Color.Green, false, false, false);
            //    BasicShapes.DrawTexture(BasicTextureType.Disc, new Vector2(640, 180), 0, -2f, Color.Green, true, false, true);

            //    BasicShapes.DrawArc(5, Color.IndianRed, new Vector2(240, 220), 120, Math.PI, -270, 0);
            //    BasicShapes.DrawLine(10, Color.DarkGoldenrod, new Vector2(100, 100), new Vector2(250, 250));
            //}
            if (!string.IsNullOrEmpty(StatusMessage))
                TextDrawShape.DrawString(centerPoint, Color.Red, StatusMessage, drawfont, Vector2.One, TextHorizontalAlignment.Center);

            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
