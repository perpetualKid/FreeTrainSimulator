using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Myra;
using Myra.Graphics2D.UI;

using Orts.Common;
using Orts.Models.Simplified;
using Orts.Formats.Msts.Files;
using Orts.Toolbox.Settings;
using static System.Windows.Forms.Design.AxImporter;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using System.Threading;
using static Orts.Formats.Msts.FolderStructure.ContentFolder;

namespace Toolbox.YardOffice
{
    public class GameWindow : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Desktop Mdesktop;


        private Folder selectedFolder;
        private Route selectedRoute;
        private Path selectedPath; // going forward, there may be multiple paths selected at once
        private IOrderedEnumerable<Folder> folders;
        private IEnumerable<Route> routes;
        private IEnumerable<Path> paths;
        private YardOffice ui;


        public GameWindow()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;

            IEnumerable<string> options = Environment.GetCommandLineArgs().Where(a => a.StartsWith("-", StringComparison.OrdinalIgnoreCase) || a.StartsWith("/", StringComparison.OrdinalIgnoreCase)).Select(a => a[1..]);
            Settings = new ToolboxSettings(options);

        }

        internal ToolboxSettings Settings { get; }

        protected override async void Initialize()
        {
            await LoadFolders().ConfigureAwait(true);
            base.Initialize();
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            // TODO: use this.Content to load your game content here


            MyraEnvironment.Game = this;

            UpdateTitle();

            // Load UI
            ui = new YardOffice();

            // Populate Routes           

            var selectItem = ui.menuItemSelect;
            foreach (Folder folder in folders)
            {
                var menuItemRouteName = new MenuItem();
                menuItemRouteName.Text = folder.Name;
                selectItem.Items.Add(menuItemRouteName);
            }

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

            Mdesktop?.Render();

        }

        internal async Task LoadFolders()
        {

            try
            {
                this.folders = (await Folder.GetFolders(Settings.UserSettings.FolderSettings.Folders).ConfigureAwait(true)).OrderBy(f => f.Name);
            }

            catch (TaskCanceledException)
            {
            }

        }
    }
}