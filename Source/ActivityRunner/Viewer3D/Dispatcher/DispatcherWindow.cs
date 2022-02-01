using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;

using GetText;
using GetText.WindowsForms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.Track;
using Orts.Graphics.Track.Shapes;
using Orts.Graphics.Xna;
using Orts.Settings;
using Orts.Simulation;

using static Orts.Common.Position.TileHelper;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher
{
    public class DispatcherWindow : Game
    {
        private readonly GraphicsDeviceManager graphicsDeviceManager;
        private readonly System.Windows.Forms.Form windowForm;
        private bool syncing;
        private ScreenMode currentScreenMode;
        private System.Windows.Forms.Screen currentScreen;
        private Point windowPosition;
        private System.Drawing.Size windowSize;
        private readonly Point clientRectangleOffset;

        private readonly UserSettings settings;

        private Catalog Catalog;
        private readonly ObjectPropertiesStore store = new ObjectPropertiesStore();

        private readonly Action onClientSizeChanged;

        private SpriteBatch spriteBatch;
        private ContentArea contentArea;

        private UserCommandController<UserCommand> userCommandController;

        public DispatcherWindow(UserSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            windowForm = (System.Windows.Forms.Form)System.Windows.Forms.Control.FromHandle(Window.Handle);

            if (settings.DispatcherWindowScreen < System.Windows.Forms.Screen.AllScreens.Length)
                currentScreen = System.Windows.Forms.Screen.AllScreens[settings.DispatcherWindowScreen];
            else
                currentScreen = System.Windows.Forms.Screen.PrimaryScreen;
            FontManager.ScalingFactor = (float)SystemInfo.DisplayScalingFactor(currentScreen);
            LoadSettings();

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
            TextShape.Initialize(this, spriteBatch);
            BasicShapes.Initialize(spriteBatch);

            userCommandController = new UserCommandController<UserCommand>();

            KeyboardInputGameComponent keyboardInputGameComponent = new KeyboardInputGameComponent(this);
            Components.Add(keyboardInputGameComponent);
            KeyboardInputHandler<UserCommand> keyboardInput = new KeyboardInputHandler<UserCommand>();
            keyboardInput.Initialize(InputSettings.UserCommands, keyboardInputGameComponent, userCommandController);

            MouseInputGameComponent mouseInputGameComponent = new MouseInputGameComponent(this);
            Components.Add(mouseInputGameComponent);
            MouseInputHandler<UserCommand> mouseInput = new MouseInputHandler<UserCommand>();
            mouseInput.Initialize(mouseInputGameComponent, keyboardInputGameComponent, userCommandController);

            base.Initialize();
        }

        protected override async void LoadContent()
        {
            BasicShapes.LoadContent(GraphicsDevice);
            ScaleRulerComponent scaleRuler = new ScaleRulerComponent(this, FontManager.Exact(System.Drawing.FontFamily.GenericSansSerif, System.Drawing.FontStyle.Regular)[14], Color.Black, new Vector2(-20, -55));
            Components.Add(scaleRuler);
            Components.Add(new InsetComponent(this, Color.DarkGray, new Vector2(-10, 30)));
            EnumArray<string, ColorSetting> colorSettings = new EnumArray<string, ColorSetting>(new string[]
            {
                "DarkGray",
                "Blue",
                "BlueViolet",
                "DarkMagenta",
                "Firebrick",
                "Crimson",
                "Olive",
                "ForestGreen",
                "DeepPink",
                "White",
                "White",
                "Navy",
                "ForestGreen",
                "RoyalBlue",
                "White",
                "White",
                "White",
                "White",
            });

            Simulator simulator = Simulator.Instance;
            base.LoadContent();
            bool useMetricUnits = settings.MeasurementUnit == MeasurementUnit.Metric || (settings.MeasurementUnit == MeasurementUnit.System && RegionInfo.CurrentRegion.IsMetric) ||
                (settings.MeasurementUnit == MeasurementUnit.Route && simulator.Route.MilepostUnitsMetric);

            DispatcherContent content = new DispatcherContent(this);
            await content.Initialize().ConfigureAwait(true);
            content.UpdateItemVisiblity(TrackViewerViewSettings.All);
            content.UpdateWidgetColorSettings(colorSettings);
            contentArea = content.ContentArea;
            contentArea.ResetSize(Window.ClientBounds.Size, 60);
            Components.Add(contentArea);
            contentArea.Enabled = true;

            #region usercommandcontroller
            userCommandController.AddEvent(UserCommand.MoveLeft, KeyEventType.KeyDown, contentArea.MoveByKeyLeft);
            userCommandController.AddEvent(UserCommand.MoveRight, KeyEventType.KeyDown, contentArea.MoveByKeyRight);
            userCommandController.AddEvent(UserCommand.MoveUp, KeyEventType.KeyDown, contentArea.MoveByKeyUp);
            userCommandController.AddEvent(UserCommand.MoveDown, KeyEventType.KeyDown, contentArea.MoveByKeyDown);
            userCommandController.AddEvent(UserCommand.ZoomIn, KeyEventType.KeyDown, contentArea.ZoomIn);
            userCommandController.AddEvent(UserCommand.ZoomOut, KeyEventType.KeyDown, contentArea.ZoomOut);
            userCommandController.AddEvent(UserCommand.ResetZoomAndLocation, KeyEventType.KeyPressed, () => { contentArea.ResetZoomAndLocation(Window.ClientBounds.Size, 0); });
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, contentArea.MouseDragging);
            userCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, contentArea.MouseWheel);
            #endregion

        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Beige);
            base.Draw(gameTime);
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
            windowSize.Width = (int)(currentScreen.WorkingArea.Size.Width * Math.Abs(settings.DispatcherWindowSettings[WindowSetting.Size][0]) / 100.0);
            windowSize.Height = (int)(currentScreen.WorkingArea.Size.Height * Math.Abs(settings.DispatcherWindowSettings[WindowSetting.Size][1]) / 100.0);

            windowPosition = PointExtension.ToPoint(settings.DispatcherWindowSettings[WindowSetting.Location]);
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
        }

        private void SaveSettings()
        {
            settings.DispatcherWindowSettings[WindowSetting.Size][0] = (int)Math.Round(100.0 * windowSize.Width / currentScreen.WorkingArea.Width);
            settings.DispatcherWindowSettings[WindowSetting.Size][1] = (int)Math.Round(100.0 * windowSize.Height / currentScreen.WorkingArea.Height);

            settings.DispatcherWindowSettings[WindowSetting.Location][0] = (int)Math.Max(0, Math.Round(100f * (windowPosition.X - currentScreen.Bounds.Left) / (currentScreen.WorkingArea.Width - windowSize.Width)));
            settings.DispatcherWindowSettings[WindowSetting.Location][1] = (int)Math.Max(0, Math.Round(100.0 * (windowPosition.Y - currentScreen.Bounds.Top) / (currentScreen.WorkingArea.Height - windowSize.Height)));
            settings.DispatcherWindowScreen = System.Windows.Forms.Screen.AllScreens.ToList().IndexOf(currentScreen);

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
            if (windowForm.InvokeRequired)
                windowForm.Invoke(new Action(() => BringToFront()));
            else
            {
                windowForm.WindowState = System.Windows.Forms.FormWindowState.Minimized;
                windowForm.Show();
                windowForm.WindowState = System.Windows.Forms.FormWindowState.Normal;
            }
        }
        #endregion
    }
}
