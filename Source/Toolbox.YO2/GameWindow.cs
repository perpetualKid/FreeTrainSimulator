using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Models.Simplified;
using Orts.Formats.Msts.Files;
using Orts.Toolbox.Settings;

using Myra;
using Myra.Graphics2D.UI;
using System.Threading.Tasks;


//using static Swan.Terminal;

namespace Toolbox.YO2
{
    public class GameWindow : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private M_MainWindow _mainWindow;
        private Desktop _desktop;

        private Folder selectedFolder;
        private Route selectedRoute;
        private Path selectedPath; // going forward, there may be multiple paths selected at once   
        private IEnumerable<Route> routes;
        private IEnumerable<Path> paths;

        public IOrderedEnumerable<Folder> folders;
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