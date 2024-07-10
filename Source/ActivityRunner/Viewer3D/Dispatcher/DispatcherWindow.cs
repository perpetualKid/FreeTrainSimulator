using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Track;

using GetText;
using GetText.WindowsForms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows;
using Orts.Formats.Msts;
using Orts.Settings;
using Orts.Simulation;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher
{
    public class DispatcherWindow : Game
    {
        private const int targetFps = 15;

        private readonly GraphicsDeviceManager graphicsDeviceManager;
        private readonly System.Windows.Forms.Form windowForm;
        private bool syncing;
        private ScreenMode currentScreenMode;
        private System.Windows.Forms.Screen currentScreen;
        private Point windowPosition;
        private System.Drawing.Size windowSize;
        private readonly Point clientRectangleOffset;

        private readonly UserSettings settings;
        private Color BackgroundColor;

        private Catalog Catalog;
        private readonly ObjectPropertiesStore store = new ObjectPropertiesStore();

        private readonly Action onClientSizeChanged;

        private SpriteBatch spriteBatch;
        private ContentArea contentArea;
        private DispatcherContent content;
        private CommonDebugInfo debugInfo;

        private UserCommandController<UserCommand> userCommandController;
        private WindowManager<DispatcherWindowType> windowManager;

        private bool followTrain;

        private readonly EnumArray<string, ColorSetting> colorSettings = new EnumArray<string, ColorSetting>(new string[]
        {
            "CornSilk",     // Background
            "DimGray",      // RailTrack
            "BlueViolet",   // RailTrackEnd
            "LightGray",    // RailTrackJunction
            "Firebrick",    // RailTrackCrossing
            "Crimson",      // RailLevelCrossing
            "Olive",        // RoadTrack
            "ForestGreen",  // RoadTrackEnd
            "DeepPink",     // RoadLevelCrossing
            "OrangeRed",    // PathTrack
            "White",        // RoadCarSpawner
            "White",        // SignalItem
            "Firebrick",    // StationItem
            "Navy",         // PlatformItem
            "ForestGreen",  // SidingItem
            "RoyalBlue",    // SpeedPostItem
            "White",        // HazardItem
            "White",        // PickupItem
            "White",        // SoundRegionItem
            "White",        // LevelCrossingItem
        });

        public DispatcherWindow(UserSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            windowForm = (System.Windows.Forms.Form)System.Windows.Forms.Control.FromHandle(Window.Handle);

            if (settings.Dispatcher.WindowScreen < System.Windows.Forms.Screen.AllScreens.Length)
                currentScreen = System.Windows.Forms.Screen.AllScreens[settings.Dispatcher.WindowScreen];
            else
                currentScreen = System.Windows.Forms.Screen.PrimaryScreen;
            FontManager.ScalingFactor = (float)WindowManager.DisplayScalingFactor(currentScreen);
            LoadSettings();

            TargetElapsedTime = TimeSpan.FromMilliseconds(1000 / targetFps);
            IsFixedTimeStep = true;
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreparingDeviceSettings += GraphicsPreparingDeviceSettings;
            graphicsDeviceManager.PreferMultiSampling = settings.MultisamplingCount > 0;

            IsMouseVisible = true;

            Window.AllowUserResizing = true;

            //Window.ClientSizeChanged += Window_ClientSizeChanged; // not using the GameForm event as it does not raise when Window is moved (ie to another screeen) using keyboard shortcut

            clientRectangleOffset = new Point(windowForm.Width - windowForm.ClientRectangle.Width, windowForm.Height - windowForm.ClientRectangle.Height);
            Window.Position = windowPosition;

            SetScreenMode(currentScreenMode);

            windowForm.LocationChanged += WindowForm_LocationChanged;
            windowForm.ClientSizeChanged += WindowForm_ClientSizeChanged;
            windowForm.FormClosing += WindowForm_FormClosing;

            // using reflection to be able to trigger ClientSizeChanged event manually as this is not 
            // reliably raised otherwise with the resize functionality below in SetScreenMode
            MethodInfo m = Window.GetType().GetMethod("OnClientSizeChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            onClientSizeChanged = (Action)Delegate.CreateDelegate(typeof(Action), Window, m);

            LoadLanguage();
            Window.Title = Catalog.GetString("Dispatcher View");
        }

        private void WindowForm_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void GraphicsPreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
            e.GraphicsDeviceInformation.PresentationParameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;
            e.GraphicsDeviceInformation.PresentationParameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = settings.MultisamplingCount;
        }

        protected override void Initialize()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            InputSettings.Initialize();
            userCommandController = new UserCommandController<UserCommand>();

            KeyboardInputGameComponent keyboardInputGameComponent = new KeyboardInputGameComponent(this);
            Components.Add(keyboardInputGameComponent);
            KeyboardInputHandler<UserCommand> keyboardInput = new KeyboardInputHandler<UserCommand>();
            keyboardInput.Initialize(InputSettings.UserCommands, keyboardInputGameComponent, userCommandController);

            MouseInputGameComponent mouseInputGameComponent = new MouseInputGameComponent(this);
            Components.Add(mouseInputGameComponent);
            MouseInputHandler<UserCommand> mouseInput = new MouseInputHandler<UserCommand>();
            mouseInput.Initialize(mouseInputGameComponent, keyboardInputGameComponent, userCommandController);

            #region popup windows
            windowManager = WindowManager.Initialize<UserCommand, DispatcherWindowType>(this, userCommandController.AddTopLayerController());
            windowManager.SetLazyWindows(DispatcherWindowType.DebugScreen, new Lazy<FormBase>(() =>
            {
                DebugScreen debugWindow = new DebugScreen(windowManager, BackgroundColor);
                debugWindow.SetInformationProvider(DebugScreenInformation.Common, debugInfo);
                return debugWindow;
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.SignalChange, new Lazy<FormBase>(() =>
            {
                return new SignalChangeWindow(windowManager, new Point(50, 50));
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.SwitchChange, new Lazy<FormBase>(() =>
            {
                return new SwitchChangeWindow(windowManager, new Point(50, 50));
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.SignalState, new Lazy<FormBase>(() =>
            {
                return new SignalStateWindow(windowManager, settings.Dispatcher.WindowLocations[DispatcherWindowType.SignalState].ToPoint());
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.HelpWindow, new Lazy<FormBase>(() =>
            {
                return new HelpWindow(windowManager, settings.Dispatcher.WindowLocations[DispatcherWindowType.HelpWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.Settings, new Lazy<FormBase>(() =>
            {
                return new SettingsWindow(windowManager, settings.Dispatcher, settings.Dispatcher.WindowLocations[DispatcherWindowType.Settings].ToPoint());
            }));
            windowManager.SetLazyWindows(DispatcherWindowType.TrainInfo, new Lazy<FormBase>(() =>
            {
                return new TrainInformationWindow(windowManager, settings.Dispatcher.WindowLocations[DispatcherWindowType.TrainInfo].ToPoint());
            }));
            Components.Add(windowManager);

            #endregion

            foreach (DispatcherWindowType windowType in EnumExtension.GetValues<DispatcherWindowType>())
            {
                if (settings.Dispatcher.WindowStatus[windowType])
                    windowManager[windowType].Open();
            }

            base.Initialize();
        }

        protected override async void LoadContent()
        {
            TrackModel.Instance(this)?.Reset();

            Simulator simulator = Simulator.Instance;
            base.LoadContent();
            bool useMetricUnits = settings.MeasurementUnit == MeasurementUnit.Metric || (settings.MeasurementUnit == MeasurementUnit.System && RegionInfo.CurrentRegion.IsMetric) ||
                (settings.MeasurementUnit == MeasurementUnit.Route && simulator.Route.MilepostUnitsMetric);

            ScaleRulerComponent scaleRuler = new ScaleRulerComponent(this, FontManager.Scaled(System.Drawing.FontFamily.GenericSansSerif, System.Drawing.FontStyle.Regular)[14], Color.Black, new Vector2(-20, -55));
            Components.Add(scaleRuler);
            Components.Add(new InsetComponent(this, Color.DarkGray, new Vector2(-10, 30)));

            content = new DispatcherContent(this);
            await content.Initialize().ConfigureAwait(true);
            content.InitializeItemVisiblity(settings.Dispatcher.ViewSettings);
            content.UpdateWidgetColorSettings(colorSettings);
            contentArea = content.ContentArea;
            contentArea.ResetSize(Window.ClientBounds.Size, 60);
            Components.Add(contentArea);
            contentArea.Enabled = true;

            #region usercommandcontroller
            userCommandController.AddEvent(UserCommand.ChangeScreenMode, KeyEventType.KeyPressed, () => SetScreenMode(currentScreenMode.Next()));
            userCommandController.AddEvent(UserCommand.MoveLeft, KeyEventType.KeyDown, contentArea.MoveByKeyLeft);
            userCommandController.AddEvent(UserCommand.MoveRight, KeyEventType.KeyDown, contentArea.MoveByKeyRight);
            userCommandController.AddEvent(UserCommand.MoveUp, KeyEventType.KeyDown, contentArea.MoveByKeyUp);
            userCommandController.AddEvent(UserCommand.MoveDown, KeyEventType.KeyDown, contentArea.MoveByKeyDown);
            userCommandController.AddEvent(UserCommand.ZoomIn, KeyEventType.KeyDown, contentArea.ZoomIn);
            userCommandController.AddEvent(UserCommand.ZoomOut, KeyEventType.KeyDown, contentArea.ZoomOut);
            userCommandController.AddEvent(UserCommand.ResetZoomAndLocation, KeyEventType.KeyPressed, () => { contentArea.ResetZoomAndLocation(Window.ClientBounds.Size, 0); });
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, MouseDragging);
            userCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, MouseWheel);
            userCommandController.AddEvent(CommonUserCommand.PointerDown, MouseLeftClick);
            userCommandController.AddEvent(CommonUserCommand.AlternatePointerDown, MouseRightClick);
            userCommandController.AddEvent(UserCommand.FollowTrain, KeyEventType.KeyPressed, () => { followTrain = !followTrain; if (followTrain) contentArea.UpdateScaleAbsolute(1.5); });
            userCommandController.AddEvent(UserCommand.DisplayDebugScreen, KeyEventType.KeyPressed, () => windowManager[DispatcherWindowType.DebugScreen].ToggleVisibility());
            userCommandController.AddEvent(UserCommand.DisplaySignalStateWindow, KeyEventType.KeyPressed, () => windowManager[DispatcherWindowType.SignalState].ToggleVisibility());
            userCommandController.AddEvent(UserCommand.DisplayHelpWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (!(userCommandArgs is ModifiableKeyCommandArgs))
                    windowManager[DispatcherWindowType.HelpWindow].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplaySettingsWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (!(userCommandArgs is ModifiableKeyCommandArgs))
                    windowManager[DispatcherWindowType.Settings].ToggleVisibility();
            });
            userCommandController.AddEvent(UserCommand.DisplayTrainInfoWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (!(userCommandArgs is ModifiableKeyCommandArgs))
                    windowManager[DispatcherWindowType.TrainInfo].ToggleVisibility();
            });
            //            userCommandController.AddEvent(UserCommand.DebugStep, KeyEventType.KeyPressed, null);
            #endregion

            debugInfo = new CommonDebugInfo(contentArea);
            if (windowManager.WindowInitialized(DispatcherWindowType.DebugScreen))
                (windowManager[DispatcherWindowType.DebugScreen] as DebugScreen).SetInformationProvider(DebugScreenInformation.Common, debugInfo);
        }

        protected override void Draw(GameTime gameTime)
        {
            debugInfo?.Update(gameTime);
            GraphicsDevice.Clear(BackgroundColor);
            base.Draw(gameTime);
        }

        protected override void Update(GameTime gameTime)
        {
            IEnumerable<int> trackedTrains = new List<int>();
            foreach (Train train in Simulator.Instance.Trains)
            {
                ((List<int>)trackedTrains).Add(train.Number);
                if (!content.Trains.TryGetValue(train.Number, out TrainWidget trainWidget))
                {
                    trainWidget = new TrainWidget(train.FrontTDBTraveller.WorldLocation, train.RearTDBTraveller.WorldLocation, train);
                    foreach (TrainCar car in train.Cars)
                    {
                        trainWidget.Cars.Add(car.UiD, new TrainCarWidget(car.WorldPosition, car.CarLengthM, car.WagonType == WagonType.Unknown ? car.EngineType != EngineType.Unknown ? WagonType.Engine : WagonType.Unknown : car.WagonType));
                    }
                    content.Trains.Add(train.Number, trainWidget);
                }
                else if (train.SpeedMpS != 0)
                {
                    trainWidget.UpdatePosition(train.FrontTDBTraveller.WorldLocation, train.RearTDBTraveller.WorldLocation);
                    IEnumerable<int> trackedCars = new List<int>();
                    foreach (TrainCar car in train.Cars)
                    {
                        ((List<int>)trackedCars).Add(car.UiD);
                        if (trainWidget.Cars.TryGetValue(car.UiD, out TrainCarWidget trainCar))
                        {
                            trainCar.UpdatePosition(car.WorldPosition);
                        }
                        else
                        {
                            trainWidget.Cars.Add(car.UiD, new TrainCarWidget(car.WorldPosition, car.CarLengthM, car.WagonType == WagonType.Unknown ? car.EngineType != EngineType.Unknown ? WagonType.Engine : WagonType.Unknown : car.WagonType));
                        }
                    }
                    trackedCars = trainWidget.Cars.Keys.Except(trackedCars);
                    foreach (int carNumber in trackedCars)
                        trainWidget.Cars.Remove(carNumber);
                }
            }
            trackedTrains = content.Trains.Keys.Except(trackedTrains);
            foreach (int trainNumber in trackedTrains)
                content.Trains.Remove(trainNumber);

            content.UpdateTrainPath(Simulator.Instance.Trains[0].FrontTDBTraveller);
            if (followTrain)
                content.UpdateTrainTrackingPoint(Simulator.Instance.PlayerLocomotive.WorldPosition.WorldLocation);
            base.Update(gameTime);
        }

        #region window size/position handling
        private void WindowForm_LocationChanged(object sender, EventArgs e)
        {
            WindowForm_ClientSizeChanged(sender, e);
        }

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
                    currentScreen.WorkingArea.Left + (currentScreen.WorkingArea.Size.Width - windowSize.Width) / 2,
                    currentScreen.WorkingArea.Top + (currentScreen.WorkingArea.Size.Height - windowSize.Height) / 2);
            }
        }

        private void LoadSettings()
        {
            windowSize.Width = (int)(currentScreen.WorkingArea.Size.Width * Math.Abs(settings.Dispatcher.WindowSettings[WindowSetting.Size][0]) / 100.0);
            windowSize.Height = (int)(currentScreen.WorkingArea.Size.Height * Math.Abs(settings.Dispatcher.WindowSettings[WindowSetting.Size][1]) / 100.0);

            windowPosition = PointExtension.ToPoint(settings.Dispatcher.WindowSettings[WindowSetting.Location]);
            if (windowPosition != PointExtension.EmptyPoint)
            {
                windowPosition = new Point(
                    currentScreen.WorkingArea.Left + windowPosition.X * (currentScreen.WorkingArea.Size.Width - windowSize.Width) / 100,
                    currentScreen.WorkingArea.Top + windowPosition.Y * (currentScreen.WorkingArea.Size.Height - windowSize.Height) / 100);
            }
            else
            {
                windowPosition = new Point(
                    currentScreen.WorkingArea.Left + (currentScreen.WorkingArea.Size.Width - windowSize.Width) / 2,
                    currentScreen.WorkingArea.Top + (currentScreen.WorkingArea.Size.Height - windowSize.Height) / 2);
            }
            BackgroundColor = ColorExtension.FromName(colorSettings[ColorSetting.Background]);
        }

        private void SaveSettings()
        {
            settings.Dispatcher.WindowSettings[WindowSetting.Size][0] = (int)Math.Round(100.0 * windowSize.Width / currentScreen.WorkingArea.Width);
            settings.Dispatcher.WindowSettings[WindowSetting.Size][1] = (int)Math.Round(100.0 * windowSize.Height / currentScreen.WorkingArea.Height);

            settings.Dispatcher.WindowSettings[WindowSetting.Location][0] = (int)Math.Max(0, Math.Round(100f * (windowPosition.X - currentScreen.Bounds.Left) / (currentScreen.WorkingArea.Width - windowSize.Width)));
            settings.Dispatcher.WindowSettings[WindowSetting.Location][1] = (int)Math.Max(0, Math.Round(100.0 * (windowPosition.Y - currentScreen.Bounds.Top) / (currentScreen.WorkingArea.Height - windowSize.Height)));
            settings.Dispatcher.WindowScreen = System.Windows.Forms.Screen.AllScreens.ToList().IndexOf(currentScreen);

            foreach (DispatcherWindowType windowType in EnumExtension.GetValues<DispatcherWindowType>())
            {
                if (windowManager.WindowInitialized(windowType))
                {
                    settings.Dispatcher.WindowLocations[windowType] = PointExtension.ToArray(windowManager[windowType].RelativeLocation);
                }
                settings.Dispatcher.WindowStatus[windowType] = windowManager.WindowOpened(windowType);
            }

            settings.Save();
        }


        private void LoadLanguage()
        {
            Localizer.Revert(windowForm, store);
            CatalogManager.Reset();

            if (!string.IsNullOrEmpty(settings.Language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(settings.Language);
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

        private void SetScreenMode(ScreenMode targetMode)
        {
            syncing = true;
            windowForm.Invoke((System.Windows.Forms.MethodInvoker)delegate
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

        protected override void Dispose(bool disposing)
        {
            Components.Remove(windowManager);
            windowManager.Dispose();
            Components.Remove(contentArea);
            contentArea?.Dispose();
            spriteBatch?.Dispose();
            graphicsDeviceManager?.Dispose();
            base.Dispose(disposing);
        }

        public void Close()
        {
            if (windowForm.InvokeRequired)
                windowForm.Invoke(new Action(() => Close()));
            else
                windowForm.Close();
        }

        public void BringToFront()
        {
            try
            {
                if (windowForm.InvokeRequired)
                    windowForm.Invoke(new Action(() => BringToFront()));
                else
                {
                    windowForm.WindowState = System.Windows.Forms.FormWindowState.Minimized;
                    windowForm.Show();
                    windowForm.WindowState = System.Windows.Forms.FormWindowState.Normal;
                }
            }
            catch (ObjectDisposedException)
            { }
        }
        #endregion

        #region Content area user interaction
        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (followTrain)
                contentArea.MouseWheel(userCommandArgs, modifiers);
            else
                contentArea.MouseWheelAt(userCommandArgs, modifiers);
        }

        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            followTrain = false;
            contentArea.MouseDragging(userCommandArgs);
        }

        public void MouseLeftClick(UserCommandArgs userCommandArgs)
        {
            if (content.SignalSelected != null && windowManager.WindowInitialized(DispatcherWindowType.SignalState))
            {
                SignalStateWindow signalstateWindow = windowManager[DispatcherWindowType.SignalState] as SignalStateWindow;
                signalstateWindow.UpdateSignal(content.SignalSelected);
            }
            if (content.Trains != null && windowManager.WindowInitialized(DispatcherWindowType.TrainInfo))
            {
                TrainInformationWindow trainInfoWindow = windowManager[DispatcherWindowType.TrainInfo] as TrainInformationWindow;
                trainInfoWindow.UpdateTrain(content.TrainSelected);
            }

        }

        public void MouseRightClick(UserCommandArgs userCommandArgs)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                if (content.SignalSelected != null && (MultiPlayerManager.MultiplayerState == MultiplayerState.None))
                {
                    SignalChangeWindow signalstateWindow = windowManager[DispatcherWindowType.SignalChange] as SignalChangeWindow;
                    signalstateWindow.OpenAt(pointerCommandArgs.Position, content.SignalSelected);
                }
                else if (content.SwitchSelected != null && MultiPlayerManager.MultiplayerState == MultiplayerState.None)
                {
                    SwitchChangeWindow switchstateWindow = windowManager[DispatcherWindowType.SwitchChange] as SwitchChangeWindow;
                    switchstateWindow.OpenAt(pointerCommandArgs.Position, content.SwitchSelected);
                }
            }
        }
        #endregion

        private sealed class CommonDebugInfo : DetailInfoBase
        {
            private readonly SmoothedData frameRate = new SmoothedData();
            private readonly ContentArea contentArea;

            private const double fpsLow = targetFps - targetFps / 5.0;
            public CommonDebugInfo(ContentArea contentArea): base(true)
            {
                this.contentArea = contentArea;
                frameRate.Preset(targetFps);
                this["Version"] = VersionInfo.FullVersion;
            }

            public override void Update(GameTime gameTime)
            {
                this["Time"] = DateTime.Now.ToString(CultureInfo.CurrentCulture);
                this["Scale"] = contentArea == null ? null : $"{contentArea.Scale:F3} (pixel/meter)";
                double elapsedRealTime = gameTime?.ElapsedGameTime.TotalSeconds ?? 1;
                frameRate.Update(elapsedRealTime, 1.0 / elapsedRealTime);
                this["FPS"] = $"{1 / gameTime.ElapsedGameTime.TotalSeconds:0.0} - {frameRate.SmoothedValue:0.0}";
                if (frameRate.SmoothedValue < fpsLow)
                    FormattingOptions["FPS"] = FormatOption.RegularRed;
                else
                    FormattingOptions["FPS"] = null;
            }
        }

    }
}
