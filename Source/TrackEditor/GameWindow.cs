using System;
using System.Windows.Forms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Common.Info;
using Orts.View.Track.Shapes;

namespace Orts.TrackEditor
{
    public enum ScreenMode
    {
        Windowed,
        WindowedFullscreen,
        BorderlessFullscreen,
    }


    public partial class GameWindow : Game
    {
        private readonly GraphicsDeviceManager graphicsDeviceManager;
        private readonly Form windowForm;

        private SpriteBatch spriteBatch;

        private bool syncing;
        private ScreenMode currentScreenMode;
        private Screen currentScreen;
        private Point windowPosition;
        private System.Drawing.Size windowSize;
        private Point clientRectangleOffset;

        private readonly System.Drawing.Size presetSize = new System.Drawing.Size(2000, 800); //TODO

        public GameWindow()
        {
            windowForm = (Form)Control.FromHandle(Window.Handle);
            currentScreen = Screen.PrimaryScreen;

            InitializeComponent();
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreparingDeviceSettings += GraphicsPreparingDeviceSettings;
            graphicsDeviceManager.PreferMultiSampling = true;
            IsMouseVisible = true;

            // Set title to show revision or build info.
            Window.Title = $"{RuntimeInfo.ProductName} {VersionInfo.Version}";
#if DEBUG
            Window.Title += " (debug)";
#endif
#if NETCOREAPP
            Window.Title += " [.NET Core]";
#elif NETFRAMEWORK
            Window.Title += " [.NET Classic]";
#endif

            Window.AllowUserResizing = true;

            //Window.ClientSizeChanged += WindowClientSizeChanged; // not using the GameForm event as it does not raise when Window is moved (ie to another screeen) using keyboard shortcut

            //IsFixedTimeStep = false;
            //graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;
            //TargetElapsedTime = TimeSpan.FromMilliseconds(10);
            //windowForm.ClientSize = new Size(Window.ClientBounds.X / 2, Window.ClientBounds.X);
            windowSize = presetSize;
            windowPosition = new Point(
                currentScreen.WorkingArea.Left + (currentScreen.WorkingArea.Size.Width - windowSize.Width) / 2,
                currentScreen.WorkingArea.Top + (currentScreen.WorkingArea.Size.Height - windowSize.Height) / 2);
            clientRectangleOffset = new Point(windowForm.Width - windowForm.ClientRectangle.Width, windowForm.Height - windowForm.ClientRectangle.Height);
            Window.Position = windowPosition;

            SynchronizeGraphicsDeviceManager(currentScreenMode);

            windowForm.LocationChanged += WindowForm_LocationChanged;
            windowForm.ClientSizeChanged += WindowForm_ClientSizeChanged;
            graphicsDeviceManager.HardwareModeSwitch = false;
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
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 4;
            e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = PresentInterval.Two;
        }

        private void SynchronizeGraphicsDeviceManager(ScreenMode targetMode)
        {
            syncing = true;
            if (graphicsDeviceManager.IsFullScreen)
                graphicsDeviceManager.ToggleFullScreen();
            switch (targetMode)
            {
                case ScreenMode.Windowed:
                    if (targetMode != currentScreenMode)
                        Window.Position = windowPosition;
                    windowForm.FormBorderStyle = FormBorderStyle.Sizable;
                    windowForm.Size = presetSize;
                    graphicsDeviceManager.PreferredBackBufferWidth = windowSize.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = windowSize.Height;
                    graphicsDeviceManager.ApplyChanges();
                    statusbar.UpdateStatusbarVisibility(true);
                    break;
                case ScreenMode.WindowedFullscreen:
                    graphicsDeviceManager.PreferredBackBufferWidth = currentScreen.WorkingArea.Width - clientRectangleOffset.X;
                    graphicsDeviceManager.PreferredBackBufferHeight = currentScreen.WorkingArea.Height - clientRectangleOffset.Y;
                    graphicsDeviceManager.ApplyChanges();
                    windowForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                    Window.Position = new Point(currentScreen.WorkingArea.Location.X, currentScreen.WorkingArea.Location.Y);
                    graphicsDeviceManager.ApplyChanges();
                    statusbar.UpdateStatusbarVisibility(false);
                    break;
                case ScreenMode.BorderlessFullscreen:
                    graphicsDeviceManager.PreferredBackBufferWidth = currentScreen.Bounds.Width;
                    graphicsDeviceManager.PreferredBackBufferHeight = currentScreen.Bounds.Height;
                    graphicsDeviceManager.ApplyChanges();
                    windowForm.FormBorderStyle = FormBorderStyle.None;
                    Window.Position = new Point(currentScreen.Bounds.X, currentScreen.Bounds.Y);
                    graphicsDeviceManager.ApplyChanges();
                    statusbar.UpdateStatusbarVisibility(false);
                    break;
            }
            currentScreenMode = targetMode;
            syncing = false;
        }
        #endregion

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            // Create a new SpriteBatch, which can be used to draw textures.
            BasicShapes.LoadContent(GraphicsDevice, spriteBatch);

        }

        private int updateTIme;
        private bool spacePressed;
        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                Exit();

            if (Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Space) && !spacePressed)
            {
                spacePressed = true;
                SynchronizeGraphicsDeviceManager(currentScreenMode.Next());
            }
            if (Keyboard.GetState().IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Space))
            {
                spacePressed = false;
            }

            if ((int)gameTime.TotalGameTime.TotalSeconds > updateTIme)
            {
                updateTIme = (int)gameTime.TotalGameTime.TotalSeconds;
                statusbar.toolStripStatusLabel1.Text = $"{1 / gameTime.ElapsedGameTime.TotalSeconds:0.0}";

            }
            // TODO: Add your update logic here

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

            spriteBatch.Begin();
            spriteBatch.Draw(BasicShapes.BasicTextures[BasicTextureType.Signal], new Vector2(0, 0), Color.White);
            spriteBatch.Draw(BasicShapes.BasicHighlightTextures[BasicTextureType.Signal], new Vector2(100, 100), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
