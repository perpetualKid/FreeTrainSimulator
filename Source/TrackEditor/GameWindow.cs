using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Orts.Common.Input;
using Orts.Settings;
using Orts.View;
using Orts.View.DrawableComponents;
using Orts.View.Track;
using Orts.View.Track.Shapes;
using Orts.View.Xna;

namespace Orts.TrackEditor
{
    public enum ScreenMode
    {
        Windowed,
        WindowedFullscreen,
        BorderlessFullscreen,
    }


    public partial class GameWindow : Game, IInputCapture
    {
        private readonly GraphicsDeviceManager graphicsDeviceManager;
        private readonly System.Windows.Forms.Form windowForm;

        private SpriteBatch spriteBatch;

        private bool syncing;
        private ScreenMode currentScreenMode;
        private System.Windows.Forms.Screen currentScreen;
        private Point windowPosition;
        private System.Drawing.Size windowSize;
        private Point clientRectangleOffset;
        private Point contentAreaOffset;

        private readonly System.Drawing.Size presetSize = new System.Drawing.Size(2000, 800); //TODO

        private ContentArea contentArea;

        internal ContentArea ContentArea
        {
            get => contentArea;
            set => windowForm.Invoke((System.Windows.Forms.MethodInvoker)delegate {
                value.UpdateSize(Window.ClientBounds.Size, contentAreaOffset);
                contentArea = value;
            });
        }

        private readonly UserSettings settings;

        public GameWindow(int instance)
        {
            IEnumerable<string> options = Environment.GetCommandLineArgs().Where(a => a.StartsWith("-", StringComparison.OrdinalIgnoreCase) || a.StartsWith("/", StringComparison.OrdinalIgnoreCase)).Select(a => a.Substring(1));
            settings = new UserSettings(options);

            windowForm = (System.Windows.Forms.Form)System.Windows.Forms.Control.FromHandle(Window.Handle);
            currentScreen = System.Windows.Forms.Screen.PrimaryScreen;

            InitializeComponent();
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreparingDeviceSettings += GraphicsPreparingDeviceSettings;
            graphicsDeviceManager.PreferMultiSampling = settings.MultisamplingCount > 0;
            IsMouseVisible = true;

            // Set title to show revision or build info.
            Window.Title = $"{instance}";
            //Window.Title = $"{RuntimeInfo.ProductName} {VersionInfo.Version}";
#if DEBUG
            Window.Title += " (debug)";
#endif
#if NETCOREAPP
            Window.Title += " [.NET Core]";
#elif NETFRAMEWORK
            Window.Title += " [.NET Classic]";
#endif

            Window.AllowUserResizing = true;

            //Window.ClientSizeChanged += Window_ClientSizeChanged; // not using the GameForm event as it does not raise when Window is moved (ie to another screeen) using keyboard shortcut

            //graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;
            //IsFixedTimeStep = true;
            //TargetElapsedTime = TimeSpan.FromMilliseconds(5);
            windowSize = presetSize;
            windowPosition = new Point(
                currentScreen.WorkingArea.Left + (currentScreen.WorkingArea.Size.Width - windowSize.Width) / 2,
                currentScreen.WorkingArea.Top + (currentScreen.WorkingArea.Size.Height - windowSize.Height) / 2);
            clientRectangleOffset = new Point(windowForm.Width - windowForm.ClientRectangle.Width, windowForm.Height - windowForm.ClientRectangle.Height);
            Window.Position = windowPosition;

            SynchronizeGraphicsDeviceManager(currentScreenMode);

            windowForm.LocationChanged += WindowForm_LocationChanged;
            windowForm.ClientSizeChanged += WindowForm_ClientSizeChanged;

            contentAreaOffset = new Point(mainmenu.Bounds.Height, statusbar.Bounds.Height);
        }

        #region window size/position handling
        private void WindowForm_ClientSizeChanged(object sender, EventArgs e)
        {
            ContentArea?.UpdateSize(Window.ClientBounds.Size, contentAreaOffset);
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
                SynchronizeGraphicsDeviceManager(currentScreenMode);
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

        private void GraphicsPreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;
            e.GraphicsDeviceInformation.PresentationParameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = settings.MultisamplingCount;
        }

        private void SynchronizeGraphicsDeviceManager(ScreenMode targetMode)
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
                        windowForm.Size = presetSize;
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
            inputComponent.AddKeyEvent(Keys.F, KeyModifiers.None, InputGameComponent.KeyEventType.KeyPressed, () => new Thread(GameWindowThread).Start());
            inputComponent.AddKeyEvent(Keys.Space, KeyModifiers.None, InputGameComponent.KeyEventType.KeyPressed, ChangeScreenMode);
            inputComponent.AddKeyEvent(Keys.Q, KeyModifiers.None, InputGameComponent.KeyEventType.KeyPressed, CloseWindow);
            inputComponent.AddKeyEvent(Keys.F4, KeyModifiers.Alt, InputGameComponent.KeyEventType.KeyPressed, ExitApplication);
            inputComponent.AddMouseEvent(InputGameComponent.MouseMovedEventType.MouseMoved, MouseMove);
            inputComponent.AddMouseEvent(InputGameComponent.MouseWheelEventType.MouseWheelChanged, MouseWheel);
            inputComponent.AddMouseEvent(InputGameComponent.MouseButtonEventType.LeftButtonReleased, MouseButtonUp);
            inputComponent.AddMouseEvent(InputGameComponent.MouseButtonEventType.RightButtonDown, MouseButtonDown);
            inputComponent.AddMouseEvent(InputGameComponent.MouseMovedEventType.MouseMovedLeftButtonDown, MouseDragging);
            // TODO: Add your initialization logic here
            base.Initialize();
            await Task.WhenAll(initTasks).ConfigureAwait(false);
        }

        private static int instance = 1;
        private static void GameWindowThread(object data)
        {
            using (GameWindow game = new GameWindow(instance++))
                game.Run();

        }

        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            BasicShapes.LoadContent(GraphicsDevice);
            DigitalClockComponent clock = new DigitalClockComponent(this, spriteBatch, TimeType.RealWorldLocalTime,
                new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel), Color.White, new Vector2(200, 300), true);
            Components.Add(clock);
        }

        int updateCount;
        private int updateTIme;
        protected override void Update(GameTime gameTime)
        {

            //updateCount++;
            //if ((int)gameTime.TotalGameTime.TotalSeconds > updateTIme)
            //{
            //    updateTIme = (int)gameTime.TotalGameTime.TotalSeconds;
            //    Debug.WriteLine($"{1 / gameTime.ElapsedGameTime.TotalSeconds:0.0} - {updateCount}/{drawCount} - {Window.ClientBounds.Width}");

            //}
            // TODO: Add your update logic here
            base.Update(gameTime);
        }

        private System.Drawing.Font drawfont = new System.Drawing.Font("Segoe UI", (int)Math.Round(25.0), System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);

        int drawCount;
        int drawTime;

        public bool InputCaptured { get; internal set; }

        protected override void Draw(GameTime gameTime)
        {
            drawCount++;
            if ((int)gameTime.TotalGameTime.TotalSeconds > drawTime)
            {
                drawTime = (int)gameTime.TotalGameTime.TotalSeconds;
                statusbar.toolStripStatusLabel1.Text = $"{1 / gameTime.ElapsedGameTime.TotalSeconds:0.0}";

            }
            GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin();
            BasicShapes.DrawTexture(BasicTextureType.PlayerTrain, new Vector2(180, 180), 0, 1, Color.Green, false, false, true);
            BasicShapes.DrawTexture(BasicTextureType.PlayerTrain, new Vector2(240, 180), 0, 1, Color.Green, true, false, false);
            BasicShapes.DrawTexture(BasicTextureType.Ring, new Vector2(80, 220), 0, 0.5f, Color.Yellow, true, false, false);
            BasicShapes.DrawTexture(BasicTextureType.Circle, new Vector2(80, 220), 0, 0.2f, Color.Red, true, false, false);
            BasicShapes.DrawTexture(BasicTextureType.CrossedRing, new Vector2(240, 220), 0.5f, 1, Color.Yellow, true, false, false);
            BasicShapes.DrawTexture(BasicTextureType.Disc, new Vector2(340, 220), 0, 1, Color.Red, true, false, false);

            BasicShapes.DrawArc(3, Color.Green, new Vector2(330, 330), 120, 4.71238898, -180, 0);
            BasicShapes.DrawDashedLine(2, Color.Aqua, new Vector2(330, 330), new Vector2(450, 330));
            TextDrawShape.DrawString(new Vector2(200, 450), Color.Red, "Test Message", drawfont);
            TextDrawShape.DrawString(new Vector2(200, 500), Color.Lime, gameTime.TotalGameTime.TotalSeconds.ToString(), drawfont);

            BasicShapes.DrawArc(5, Color.IndianRed, new Vector2(240, 220), 120, Math.PI, -270, 0);
            BasicShapes.DrawLine(10, Color.DarkGoldenrod, new Vector2(100, 100), new Vector2(250, 250));
            ContentArea?.Draw();
            spriteBatch.End();

            base.Draw(gameTime);
        }

        public void DrawStatusMessage(string message)
        {
            BeginDraw();
            GraphicsDevice.Clear(Color.GreenYellow);
            spriteBatch.Begin();
            TextDrawShape.DrawString(new Vector2(300, 450), Color.Red, message, drawfont);
            spriteBatch.End();
            EndDraw();
        }
    }
}
