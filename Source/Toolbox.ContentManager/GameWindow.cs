using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Myra;
using Myra.Graphics2D.UI;


namespace Toolbox.ContentManager
{
    public class GameWindow : Game
    {
        private GraphicsDeviceManager Mgraphics;
        private string MfilePath;
        private bool Mdirty = true;
        private SpriteBatch _spriteBatch;
        private Desktop Mdesktop;

        public string FilePath
        {
            get { return MfilePath; }

            set
            {
                if (value == MfilePath)
                {
                    return;
                }

                MfilePath = value;
            }
        }

        public bool Dirty
        {
            get { return Mdirty; }

            set
            {
                if (value == Mdirty)
                {
                    return;
                }

                Mdirty = value;
            }
        }

        public GameWindow()
        {
            Mgraphics = new GraphicsDeviceManager(this)
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

            UpdateTitle();

            // Load UI
            var ui = new ContentManager();

            var newItem = ui.menuItemNew;
            newItem.Selected += NewItemOnDown;

            // File/Open
            var openItem = ui.menuItemOpen;
            openItem.Selected += OpenItemOnDown;

            // File/Save
            var saveItem = ui.menuItemSave;
            saveItem.Selected += SaveItemOnDown;

            // File/Save As...
            var saveAsItem = ui.menuItemSaveAs;
            saveAsItem.Selected += SaveAsItemOnDown;

            ui.menuItemDebugOptions.Selected += DebugOptionsOnDown;

            // File/Quit
            var quitItem = ui.menuItemQuit;
            quitItem.Selected += QuitItemOnDown;

            var aboutItem = ui.menuItemAbout;
            aboutItem.Selected += AboutItemOnDown;

            _desktop.KeyDown += (s, a) =>
            {
                if (_desktop.HasModalWidget || ui._mainMenu.IsOpen)
                {
                    return;
                }

                if (_desktop.IsKeyDown(Keys.LeftControl) || _desktop.IsKeyDown(Keys.RightControl))
                {
                    if (_desktop.IsKeyDown(Keys.N))
                    {
                        NewItemOnDown(this, EventArgs.Empty);
                    }
                    else if (_desktop.IsKeyDown(Keys.O))
                    {
                        OpenItemOnDown(this, EventArgs.Empty);
                    }
                    else if (_desktop.IsKeyDown(Keys.S))
                    {
                        SaveItemOnDown(this, EventArgs.Empty);
                    }
                    else if (_desktop.IsKeyDown(Keys.Q))
                    {
                        Exit();
                    }
                }
            };
         
        }

        private void UpdateTitle()
        {
            Window.Title = "Content Manager";
        }

        private void AboutItemOnDown(object sender, EventArgs eventArgs)
        {
            var messageBox = Dialog.CreateMessageBox("Content Manager", "Version " + MyraEnvironment.Version);
            messageBox.ShowModal(Mdesktop);
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