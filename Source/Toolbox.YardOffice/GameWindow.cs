using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Myra;
using Myra.Graphics2D.UI;

namespace Toolbox.YardOffice
{
    public class GameWindow : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Desktop Mdesktop;

        public GameWindow()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            // TODO: use this.Content to load your game content here


            MyraEnvironment.Game = this;

            UpdateTitle();

            // Load UI
            var ui = new YardOffice();

            // File/Quit
            var quitItem = ui.menuItemQuit;
            quitItem.Selected += QuitItemOnDown;

            // Help/About
            var aboutItem = ui.menuItemAbout;
            aboutItem.Selected += AboutItemOnDown;

            Mdesktop = new Desktop
            {
                Root = ui
            };
        }

        private void AboutItemOnDown(object sender, EventArgs eventArgs)
        {
            var messageBox = Dialog.CreateMessageBox("Yard Office", "Myra " + MyraEnvironment.Version);
            messageBox.ShowModal(Mdesktop);
        }

        private void UpdateTitle()
        {
            Window.Title = "Yard Office";
        }

        private void QuitItemOnDown(object sender, EventArgs genericEventArgs)
        {
            Exit();
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
            base.Draw(gameTime);

            // TODO: Add your drawing code here

            GraphicsDevice.Clear(Color.Black);

            Mdesktop.Render();
        }
    }
}