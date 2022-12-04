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
using System.Threading;
using Orts.Graphics.MapView;


//using static Swan.Terminal;

namespace Toolbox.YO2
{
    public class GameWindow : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private M_MainWindow _mainWindow;
        

        private Folder selectedFolder;
        private Route selectedRoute;
        private Path selectedPath; // going forward, there may be multiple paths selected at once   
        private IEnumerable<Route> routes;
        private IEnumerable<Path> paths;
 

        private CancellationTokenSource ctsConsistLoading;

        //       public Folder selectedFolder;
        public Desktop _desktop;
        public IOrderedEnumerable<Folder> folders;
        public IEnumerable<Consist> consists = Array.Empty<Consist>();



        public GameWindow()
        {


            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1200,
                PreferredBackBufferHeight = 800
            };
            Window.AllowUserResizing = true;
            Window.Title = "Toolbox - Yard Office";
            
            IsMouseVisible = true;

            IEnumerable<string> options = Environment.GetCommandLineArgs().Where(a => a.StartsWith("-", StringComparison.OrdinalIgnoreCase) || a.StartsWith("/", StringComparison.OrdinalIgnoreCase)).Select(a => a[1..]);
            Settings = new ToolboxSettings(options);

            MyraEnvironment.Game = this;

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

            _mainWindow = new M_MainWindow(this);

            var quitItem = _mainWindow.menuItemQuit;
            quitItem.Selected += QuitItemOnDown;


            _desktop.Root = _mainWindow;

        }

        protected override void Draw(GameTime gameTime)
        {

            base.Draw(gameTime);

            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here

            _desktop?.Render();
       
        }
                
        private void QuitItemOnDown(object sender, EventArgs genericEventArgs)
        {
            Exit();
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
   

        internal async Task LoadConsists(Folder selectedFolder)
        {
            lock (consists)
            {
                if (ctsConsistLoading != null && !ctsConsistLoading.IsCancellationRequested)
                    ctsConsistLoading.Cancel();
                ctsConsistLoading = ResetCancellationTokenSource(ctsConsistLoading);
            }


            try
            {
                consists = (await Consist.GetConsists(selectedFolder, ctsConsistLoading.Token).ConfigureAwait(true)).OrderBy(c => c.Name);
            }
            catch (TaskCanceledException)
            {
                consists = Array.Empty<Consist>();
            }
        }

        private static CancellationTokenSource ResetCancellationTokenSource(CancellationTokenSource cts)
        {
            if (cts != null)
            {
                cts.Dispose();
            }
            // Create a new cancellation token source so that can cancel all the tokens again 
            return new CancellationTokenSource();
        }
    }
}