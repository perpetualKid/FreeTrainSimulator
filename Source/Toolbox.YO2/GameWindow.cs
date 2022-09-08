using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Myra;
using Myra.Graphics2D.UI;

namespace Toolbox.YO2
{
    public class GameWindow : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private M_MainWindow _mainWindow;
        private Desktop _desktop;

        public static GameWindow Instance { get; private set; }

        public GameWindow()
        {
            Instance = this;

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1200,
                PreferredBackBufferHeight = 800
            };
            Window.AllowUserResizing = true;
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {

            base.LoadContent();

            // TODO: use this.Content to load your game content here

            MyraEnvironment.Game = this;

            _desktop = new Desktop
            {
                // Inform Myra that external text input is available
                // So it stops translating Keys to chars
                HasExternalTextInput = true
            };

            // Provide that text input
            Window.TextInput += (s, a) =>
            {
                _desktop.OnChar(a.Character);
            };

            // Load UI
            _mainWindow = new M_MainWindow();

            _desktop.Root = _mainWindow;

        }

        protected override void Draw(GameTime gameTime)
        {

            base.Draw(gameTime);

            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here

            _desktop?.Render();
       
        }
    }
}