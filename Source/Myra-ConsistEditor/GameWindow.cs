using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Myra;
using Myra.Graphics2D.UI;


namespace Toolbox.ContentManager
{
    public class GameWindow : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Desktop Mdesktop;


        public GameWindow()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1200,
                PreferredBackBufferHeight = 800
            };
            Content.RootDirectory = "Content";
            Window.AllowUserResizing = true;
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
            MyraEnvironment.Game = this;

            var panel = new Panel();
            var positionedText = new Label();
            positionedText.Text = "Positioned Text";
            positionedText.Left = 50;
            positionedText.Top = 100;
            panel.Widgets.Add(positionedText);

            var paddedCenteredButton = new TextButton();
            paddedCenteredButton.Text = "Padded Centered Button";
            paddedCenteredButton.HorizontalAlignment = HorizontalAlignment.Center;
            paddedCenteredButton.VerticalAlignment = VerticalAlignment.Center;
            panel.Widgets.Add(paddedCenteredButton);

            var rightBottomText = new Label();
            rightBottomText.Text = "Right Bottom Text";
            rightBottomText.Left = -30;
            rightBottomText.Top = -20;
            rightBottomText.HorizontalAlignment = HorizontalAlignment.Right;
            rightBottomText.VerticalAlignment = VerticalAlignment.Bottom;
            panel.Widgets.Add(rightBottomText);

            var fixedSizeButton = new TextButton();
            fixedSizeButton.Text = "Fixed Size Button";
            fixedSizeButton.Width = 110;
            fixedSizeButton.Height = 80;
            panel.Widgets.Add(fixedSizeButton);

            // Add it to the desktop
            Mdesktop = new Desktop();
            Mdesktop.Root = panel;
         
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here
            GraphicsDevice.Clear(Color.Black);
            Mdesktop.Render();

            base.Draw(gameTime);
        }
    }
}