// COPYRIGHT 2014, 2018 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Shim;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Formats.Msts;

using ORTS.TrackViewer.Drawing;
using ORTS.TrackViewer.Drawing.Labels;
using ORTS.TrackViewer.Editing;
using ORTS.TrackViewer.Editing.Charts;
using ORTS.TrackViewer.UserInterface;

using Color = Microsoft.Xna.Framework.Color;
using MessageBox = System.Windows.Forms.MessageBox;

namespace ORTS.TrackViewer
{

    /// <summary>
    /// Delegate that can be called by routines such that we can draw it to the screen
    /// </summary>
    /// <param name="message">Message to draw</param>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public delegate void MessageDelegate(string message);
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix

    /// <summary>
    /// This is the main type for your game
    /// </summary>
    internal sealed class TrackViewer : Game
    {
        #region Public members
        /// <summary>String showing the date of the program</summary>
        public const string TrackViewerVersion = "2021/12/15";
        /// <summary>Path where the content (like .png files) is stored</summary>
        public string ContentPath { get; private set; }
        /// <summary>Folder where MSTS is installed (or at least, where the files needed for tracks, routes and paths are stored)</summary>
        public FolderModel InstallFolder { get; private set; }
        /// <summary>List of available routes (in the install directory)</summary>
        public Collection<RouteModelCore> Routes { get; private set; } // Collection because of FxCop
        /// <summary>List of available paths in the current route</summary>
        public Collection<PathModelCore> Paths { get; private set; } // Collection because of FxCop
        /// <summary>Route, ie with a path c:\program files\microsoft games\train simulator\routes\usa1  - may be different on different pc's</summary>
        public RouteModelCore CurrentRoute { get; private set; }
        /// <summary>Route that was used last time</summary>
        private RouteModelCore DefaultRoute;
        /// <summary>Width of the drawing screen in pixels</summary>
        public int ScreenW { get; private set; }
        /// <summary>Height of the drawing screen in pixels</summary>
        public int ScreenH { get; private set; }
        /// <summary>The information of the route like trackDB, tsectiondat, ..., loaded from MSTS route files.</summary>
        public RouteData RouteData { get; private set; }
        /// <summary>(Draw)trackDB, that also contains the track data base and the track section data</summary>
        public DrawTrackDB DrawTrackDB { get; private set; }
        /// <summary>Main draw area</summary>
        public DrawArea DrawArea { get; private set; }
        /// <summary>The frame rate</summary>
        public SmoothedData FrameRate { get; private set; }
        /// <summary>The routines to select and draw multiple paths</summary>
        public DrawMultiplePaths DrawMultiplePaths { get; private set; }

        /// <summary>The language manager to deal with various languages.</summary>
        public LanguageManager LanguageManager { get; private set; }

        /// <summary>The Path editor</summary>
        internal PathEditor PathEditor { get; private set; }
        /// <summary>The routines to draw the .pat file</summary>
        public DrawPATfile DrawPATfile { get; private set; }

        #endregion

        #region Private members
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        /// <summary>Draw area for the inset</summary>
        private ShadowDrawArea drawAreaInset;

        /// <summary>The scale ruler to draw on screen</summary>
        private DrawScaleRuler drawScaleRuler;

        /// <summary>For drawing real world longitude and latitude</summary>
        private DrawLongitudeLatitude drawLongitudeLatitude;

        /// <summary>For drawing the action that the editor might be taking</summary>
        private DrawEditorAction drawEditorAction;

        /// <summary>The routines to draw the world tiles</summary>
        private DrawWorldTiles drawWorldTiles;

        /// <summary>The routines to draw the grade of a path</summary>
        private DrawPathChart drawPathChart;
        /// <summary>The routines to draw the terrain textures</summary>
        internal DrawTerrain drawTerrain; //todo, get it private again: statusbar

        private DrawLabels drawLabels;

        /// <summary>The menu at the top</summary>
        private MenuControl menuControl;

        /// <summary>The status bar at the bottom</summary>
        private StatusBarControl statusBarControl;

        /// <summary>when we have lost focus, we do not want to enable shifting with mouse</summary>
        private bool lostFocus;
        /// <summary>number of times we want to skip draw because nothing happened</summary>
        private int skipDrawAmount;
        /// <summary>Maximum number of times we will skipp drawing</summary>
        private const int maxSkipDrawAmount = 10;

        /// <summary>The command-line arguments</summary>
        private string[] commandLineArgs;
        #endregion

        #region Constructor and Initialization methods

        /// <summary>
        /// Constructor. This is where it all starts.
        /// </summary>
        public TrackViewer(string[] args)
        {
            if (Properties.Settings.Default.CallUpgrade)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.CallUpgrade = false;
            }

            commandLineArgs = args;

            graphics = new GraphicsDeviceManager(this);
            ContentPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Content");

            Content.RootDirectory = "Content";
            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 768;
            ScreenH = graphics.PreferredBackBufferHeight;
            ScreenW = graphics.PreferredBackBufferWidth;
            SetAliasing();
            graphics.IsFullScreen = false;
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += new EventHandler<EventArgs>(Window_ClientSizeChanged);

            //we do not a very fast behaviour, but we do need to get all key presses
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(0.05);
            FrameRate = new SmoothedData(0.5f);

            LanguageManager = new LanguageManager();
            LanguageManager.LoadLanguage(); // need this before all menus and stuff are initialized.

        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// relation ontent.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            TextManager.Initialize(this);
            TVInputSettings.SetDefaults();

            // This control is purely here to capture focus and prevent it slipping out and on to the menu.
            Control.FromHandle(Window.Handle).Controls.Add(new TextBox() { Top = -100 });

            statusBarControl = new StatusBarControl(this);
            Localize(statusBarControl);
            menuControl = new MenuControl(this);
            Localize(menuControl);
            menuControl.PopulateLanguages();
            DrawColors.Initialize(menuControl);


            Localize(statusBarControl);
            Localize(menuControl);

            drawWorldTiles = new DrawWorldTiles();
            drawScaleRuler = new DrawScaleRuler();
            DrawArea = new DrawArea(drawScaleRuler);
            drawAreaInset = new ShadowDrawArea(null)
            {
                StrictChecking = true
            };

            SetSubwindowSizes();

            IsMouseVisible = true;

            // install folder
            if (string.IsNullOrEmpty(Properties.Settings.Default.installDirectory))
            {
                try
                {
                    Properties.Settings.Default.installDirectory = FolderStructure.MstsFolder;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch { }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            ContentModel contentModel = Task.Run(async() => await ContentModel.None.Get(CancellationToken.None).ConfigureAwait(false)).Result;
            InstallFolder = contentModel.ContentFolders.Where(f => System.IO.Path.GetRelativePath(f.ContentPath, Properties.Settings.Default.installDirectory) == ".").FirstOrDefault(); 

            FindRoutes(InstallFolder);

            drawPathChart = new DrawPathChart();

            base.Initialize();
        }

        /// <summary>
        /// Set the sizes of the various subwindows that they can use to draw upon.
        /// </summary>
        private void SetSubwindowSizes()
        {
            int insetRatio = 10;

            //We need to give enough room for menu and status bar in raw pixels
            float dpiScale = System.Drawing.Graphics.FromHwnd(IntPtr.Zero).DpiY / 96;
            int menuHeight = (int)(menuControl.MenuHeight * dpiScale);
            int statusbarHeight = (int)(statusBarControl.StatusbarHeight * dpiScale);
            menuControl.SetScreenSize(ScreenW, menuHeight);
            statusBarControl.SetScreenSize(ScreenW, statusbarHeight, ScreenH);

            //The rest of the available pixes are for the draw-area's
            DrawArea.SetScreenSize(0, menuHeight, ScreenW, ScreenH - statusbarHeight - menuHeight);
            drawAreaInset.SetScreenSize(ScreenW - ScreenW / insetRatio, menuHeight + 1, ScreenW / insetRatio, ScreenH / insetRatio);

            //Some on-screen features depend on the actual font-height
            int halfHeight = (int)(TextManager.Instance.DefaultFont.Height / 2);
            drawScaleRuler.SetLocationAndSize(halfHeight, ScreenH - statusbarHeight - halfHeight, 2 * halfHeight);
            drawLongitudeLatitude = new DrawLongitudeLatitude(halfHeight, menuHeight);
            drawEditorAction = new DrawEditorAction(halfHeight, menuHeight + 2 * halfHeight);
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            BasicShapes.LoadContent(GraphicsDevice, spriteBatch, ContentPath);
            drawAreaInset.LoadContent(GraphicsDevice, spriteBatch, 2, 2, 2);
            //drawTerrain.LoadContent(GraphicsDevice); // can only be done when route is known!
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // Unload any non ContentManager content here
        }

        /// <summary>
        /// Simplified Draw routine that only shows background and a message.
        /// </summary>
        /// <param name="message">The message you want to show</param>
        private void DrawLoadingMessage(string message)
        {
            // This is not really a game State, because it is not used interactively. In fact, Draw itself is
            // probably not called because the program is doing other things
            BeginDraw();
            GraphicsDevice.Clear(DrawColors.colorsNormal.ClearWindow);
            spriteBatch.Begin();
            // it is better to have integer locations, otherwise text is difficult to read
            Vector2 messageLocation = new Vector2((float)Math.Round(ScreenW / 2f), (float)Math.Round(ScreenH / 2f));
            BasicShapes.DrawStringCentered(messageLocation, DrawColors.colorsNormal.Text, message);
            spriteBatch.End();
            EndDraw();
        }

        #endregion

        #region Main game methods
        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (!IsActive)
            {
                lostFocus = true;
                return;
            }

            TVUserInput.Update();
            if (lostFocus)
            {
                // if the previous call was in inactive mode, we do want TVUserInput to be updated, but we will only
                // act on it the next round. To make sure moving the mouse to other locations and back is influencing
                // the location visible in trackviewer.
                lostFocus = false;
                return;
            }

            if (DrawTrackDB != null)
            {   // when update is called, we are not searching via menu
                DrawTrackDB.ClearHighlightOverrides();
            }

            // First check all the buttons that can be kept down.

            if (drawPathChart.IsActived)
            {
                if (TVUserInput.IsDown(TVUserCommands.ShiftLeft))
                { drawPathChart.Shift(-1); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ShiftRight))
                { drawPathChart.Shift(1); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ZoomIn))
                { drawPathChart.Zoom(-1); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ZoomOut))
                { drawPathChart.Zoom(1); skipDrawAmount = 0; }
            }
            else if (!menuControl.IsKeyboardFocusWithin)
            {
                if (TVUserInput.IsDown(TVUserCommands.ShiftLeft))
                { DrawArea.ShiftLeft(); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ShiftRight))
                { DrawArea.ShiftRight(); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ShiftUp))
                { DrawArea.ShiftUp(); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ShiftDown))
                { DrawArea.ShiftDown(); skipDrawAmount = 0; }

                if (TVUserInput.IsDown(TVUserCommands.ZoomIn))
                { DrawArea.Zoom(-1); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ZoomOut))
                { DrawArea.Zoom(1); skipDrawAmount = 0; }
            }

            if (TVUserInput.Changed)
            {
                skipDrawAmount = 0;
            }


            if (TVUserInput.IsPressed(TVUserCommands.Quit))
                Quit();
            if (TVUserInput.IsPressed(TVUserCommands.ReloadRoute))
                ReloadRoute();

            if (TVUserInput.IsPressed(TVUserCommands.ShiftToMouseLocation))
                DrawArea.ShiftToLocation(DrawArea.MouseLocation);
            if (TVUserInput.IsPressed(TVUserCommands.ZoomInSlow))
                DrawArea.Zoom(-1);
            if (TVUserInput.IsPressed(TVUserCommands.ZoomOutSlow))
                DrawArea.Zoom(1);
            if (TVUserInput.IsPressed(TVUserCommands.ZoomToTile))
                DrawArea.ZoomToTile();
            if (TVUserInput.IsPressed(TVUserCommands.ZoomReset))
            {
                DrawArea.ZoomReset(DrawTrackDB);
                drawAreaInset.ZoomReset(DrawTrackDB);  // needed in case window was resized
            }

            if (DrawPATfile != null && Properties.Settings.Default.showPATfile)
            {
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPath))
                    DrawPATfile.ExtendPath();
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPathFull))
                    DrawPATfile.ExtendPathFull();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePath))
                    DrawPATfile.ReducePath();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePathFull))
                    DrawPATfile.ReducePathFull();
                if (TVUserInput.IsDown(TVUserCommands.ShiftToPathLocation))
                    DrawArea.ShiftToLocation(DrawPATfile.CurrentNode.Location);
            }

            if (PathEditor != null && Properties.Settings.Default.showTrainpath)
            {
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPath))
                    PathEditor.ExtendPath();
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPathFull))
                    PathEditor.ExtendPathFull();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePath))
                    PathEditor.ReducePath();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePathFull))
                    PathEditor.ReducePathFull();
                if (TVUserInput.IsDown(TVUserCommands.ShiftToPathLocation))
                    DrawArea.ShiftToLocation(PathEditor.CurrentLocation);

                if (TVUserInput.IsPressed(TVUserCommands.EditorUndo))
                    PathEditor.Undo();
                if (TVUserInput.IsPressed(TVUserCommands.EditorRedo))
                    PathEditor.Redo();
                if (TVUserInput.IsMouseXButton1Pressed())
                    PathEditor.Undo();
                if (TVUserInput.IsMouseXButton2Pressed())
                    PathEditor.Redo();
            }

            int mouseLocationAbsoluteX = Window.ClientBounds.Left + TVUserInput.MouseLocationX;
            int mouseLocationAbsoluteY = Window.ClientBounds.Top + TVUserInput.MouseLocationY;
            if (PathEditor != null && PathEditor.EditingIsActive)
            {
                if (TVUserInput.IsMouseRightButtonPressed())
                {
                    PathEditor.OnLeftMouseRelease(); // any action done with left mouse is cancelled now
                    PathEditor.PopupContextMenu(mouseLocationAbsoluteX, mouseLocationAbsoluteY);
                }

                PathEditor.DeterminePossibleActions(TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClickDrag), TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClickAction),
                    TVUserInput.MouseLocationX, TVUserInput.MouseLocationY);

                if (TVUserInput.IsPressed(TVUserCommands.PlaceEndPoint))
                    PathEditor.PlaceEndPoint();
                if (TVUserInput.IsPressed(TVUserCommands.PlaceWaitPoint))
                    PathEditor.PlaceWaitPoint();


                if (TVUserInput.IsMouseLeftButtonPressed())
                {
                    PathEditor.OnLeftMouseClick();
                }
                if (TVUserInput.IsMouseLeftButtonDown())
                {
                    PathEditor.OnLeftMouseMoved(); // to make sure it is reactive enough, don't even care if mouse is really moved
                }
                if (TVUserInput.IsMouseLeftButtonReleased())
                {
                    PathEditor.OnLeftMouseRelease();
                }

                if (TVUserInput.IsReleased(TVUserCommands.EditorTakesMouseClickDrag))
                {
                    PathEditor.OnLeftMouseCancel();
                }
            }
            else if (drawLabels != null)
            {
                if (TVUserInput.IsPressed(TVUserCommands.AddLabel))
                {
                    drawLabels.AddLabel(mouseLocationAbsoluteX, mouseLocationAbsoluteY);
                }

                if (TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClickDrag))
                {
                    if (TVUserInput.IsMouseLeftButtonPressed())
                    {
                        drawLabels.OnLeftMouseClick();
                    }
                    if (TVUserInput.IsMouseLeftButtonDown())
                    {
                        drawLabels.OnLeftMouseMoved();
                    }
                    if (TVUserInput.IsMouseLeftButtonReleased())
                    {
                        drawLabels.OnLeftMouseRelease();
                    }
                }
                if (TVUserInput.IsReleased(TVUserCommands.EditorTakesMouseClickDrag))
                {
                    drawLabels.OnLeftMouseCancel();
                }

                if (TVUserInput.IsMouseRightButtonPressed())
                {
                    drawLabels.PopupContextMenu(mouseLocationAbsoluteX, mouseLocationAbsoluteY);
                }
            }

            bool otherWindowHasMouse = menuControl.HasMouse() || drawPathChart.IsActived;
            if (!TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClickDrag) && !otherWindowHasMouse)
            {
                if (TVUserInput.IsMouseMoved() && TVUserInput.IsMouseLeftButtonDown())
                {
                    DrawArea.ShiftArea(TVUserInput.MouseMoveX(), TVUserInput.MouseMoveY());
                }
            }

            if (TVUserInput.IsMouseWheelChanged())
            {
                int mouseWheelChange = TVUserInput.MouseWheelChange();
                if (!drawPathChart.IsActived)
                {
                    if (TVUserInput.IsDown(TVUserCommands.MouseZoomSlow))
                    {
                        DrawArea.Zoom(mouseWheelChange > 0 ? -1 : 1);
                    }
                    else
                    {
                        DrawArea.Zoom(-mouseWheelChange / 40);
                    }
                }
            }


            DrawArea.Update();
            drawAreaInset.Update();
            drawAreaInset.Follow(DrawArea, 10f);

            if (TVUserInput.IsPressed(TVUserCommands.ToggleZoomAroundMouse))
                menuControl.MenuToggleZoomingAroundMouse();

            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowTerrain))
                menuControl.MenuToggleShowTerrain();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowDMTerrain))
                menuControl.MenuToggleShowDMTerrain();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPatchLines))
                menuControl.MenuToggleShowPatchLines();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSignals))
                menuControl.MenuToggleShowSignals();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSidings))
                menuControl.MenuToggleShowSidings();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSidingNames))
                menuControl.MenuToggleShowSidingNames();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPlatforms))
                menuControl.MenuToggleShowPlatforms();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPlatformNames))
                menuControl.MenuCirculatePlatformStationNames();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSpeedLimits))
                menuControl.MenuToggleShowSpeedLimits();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowMilePosts))
                menuControl.MenuToggleShowMilePosts();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowTrainpath))
                menuControl.MenuToggleShowTrainpath();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPatFile))
                menuControl.MenuToggleShowPatFile();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleHighlightTracks))
                menuControl.MenuToggleHighlightTracks();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleHighlightItems))
                menuControl.MenuToggleHighlightItems();

            //keyboard shortcuts for menu
            if (TVUserInput.IsPressed(TVUserCommands.MenuFile))
            { menuControl.menuFile.Focus(); menuControl.menuFile.IsSubmenuOpen = true; }
            if (TVUserInput.IsPressed(TVUserCommands.MenuView))
            { menuControl.menuView.Focus(); menuControl.menuView.IsSubmenuOpen = true; }
            if (TVUserInput.IsPressed(TVUserCommands.MenuTrackItems))
            { menuControl.menuTrackItems.Focus(); menuControl.menuTrackItems.IsSubmenuOpen = true; }
            if (TVUserInput.IsPressed(TVUserCommands.MenuPreferences))
            { menuControl.menuPreferences.Focus(); menuControl.menuPreferences.IsSubmenuOpen = true; }
            if (TVUserInput.IsPressed(TVUserCommands.MenuStatusbar))
            { menuControl.menuStatusbar.Focus(); menuControl.menuStatusbar.IsSubmenuOpen = true; }
            if (TVUserInput.IsPressed(TVUserCommands.MenuPathEditor))
            { menuControl.menuPathEditor.Focus(); menuControl.menuPathEditor.IsSubmenuOpen = true; }
            if (TVUserInput.IsPressed(TVUserCommands.MenuTerrain))
            { menuControl.menuTerrain.Focus(); menuControl.menuTerrain.IsSubmenuOpen = true; }
            if (TVUserInput.IsPressed(TVUserCommands.MenuHelp))
            { menuControl.menuHelp.Focus(); menuControl.menuHelp.IsSubmenuOpen = true; }

            base.Update(gameTime);

            HandleCommandLineArgs();
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {

            // Even if there is nothing new to draw for main window, we might still need to draw for the shadow textures.
            if (DrawTrackDB != null && Properties.Settings.Default.showInset)
            {
                drawAreaInset.DrawShadowTextures(DrawTrackDB.DrawTracks, DrawColors.colorsNormal.ClearWindowInset);
            }

            // if there is nothing to draw, be done.
            if (--skipDrawAmount > 0)
            {
                return;
            }

            GraphicsDevice.Clear(DrawColors.colorsNormal.ClearWindow);
            if (DrawTrackDB == null)
                return;

            spriteBatch.Begin();
            //spriteBatch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Immediate, SaveStateMode.None);
            //GraphicsDevice.SamplerStates[0].MagFilter = TextureFilter.Point;
            //GraphicsDevice.SamplerStates[0].MinFilter = TextureFilter.Point;
            //GraphicsDevice.SamplerStates[0].MipFilter = TextureFilter.Point;
            if (drawTerrain != null)
            { drawTerrain.Draw(DrawArea); }
            drawWorldTiles.Draw(DrawArea);
            DrawArea.DrawTileGrid();
            if (drawTerrain != null)
            { drawTerrain.DrawPatchLines(DrawArea); }

            DrawTrackDB.DrawRoads(DrawArea);
            DrawTrackDB.DrawTracks(DrawArea);
            DrawTrackDB.DrawTrackHighlights(DrawArea, true);

            DrawTrackDB.DrawJunctionAndEndNodes(DrawArea);

            if (Properties.Settings.Default.showInset)
            {
                drawAreaInset.DrawBackground(DrawColors.colorsNormal.ClearWindowInset);
                drawAreaInset.DrawShadowedTextures();
                DrawTrackDB.DrawTrackHighlights(drawAreaInset, false);
                drawAreaInset.DrawBorder(Color.Red, DrawArea);
                drawAreaInset.DrawBorder(Color.Black);
            }

            if (DrawMultiplePaths != null)
                DrawMultiplePaths.Draw(DrawArea);
            if (DrawPATfile != null && Properties.Settings.Default.showPATfile)
                DrawPATfile.Draw(DrawArea);
            if (PathEditor != null && Properties.Settings.Default.showTrainpath)
                PathEditor.Draw(DrawArea);
            drawEditorAction.Draw(PathEditor);

            DrawTrackDB.DrawRoadTrackItems(DrawArea);
            DrawTrackDB.DrawTrackItems(DrawArea);
            DrawTrackDB.DrawItemHighlights(DrawArea);

            CalculateFPS(gameTime ?? throw new ArgumentNullException(nameof(gameTime)));

            statusBarControl.Update(this, DrawArea.MouseLocation);

            drawScaleRuler.Draw();
            drawLongitudeLatitude.Draw(DrawArea.MouseLocation);
            drawLabels.Draw(DrawArea);

            spriteBatch.End();

            base.Draw(gameTime);
            skipDrawAmount = maxSkipDrawAmount;
        }

        #endregion

        #region User actions (e.g. from menu)
        /// <summary>
        /// Set aliasing depending on the settings (set in the menu)
        /// </summary>
        public void SetAliasing()
        {
            // Personally, I do not think anti-aliasing looks crisp at all. Poddibly because not enough multi-sampling is used.
            // If someone knows how to get better/best antisampling depending on available hardware, be my guess.
            graphics.PreferMultiSampling = Properties.Settings.Default.doAntiAliasing;
        }

        /// <summary>
        /// Show the window with the chart of the path
        /// </summary>
        public void ShowPathChart()
        {
            drawPathChart.Open();
        }

        /// <summary>
        /// Ask the user if we really want to quit or not, and if yes, well, quit.
        /// </summary>
        public void Quit()
        {
            string message = string.Empty;
            if (PathEditor != null && PathEditor.HasModifiedPath)
            {
                message = catalog.GetString("The path you are working on has un-saved changes.\n");
            }
            message += catalog.GetString("Do you really want to Quit?");

            if (MessageBox.Show(message, "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                Exit();
            }
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            ScreenW = Window.ClientBounds.Width;
            ScreenH = Window.ClientBounds.Height;
            // if something went wrong during fast window switching, let's not continue
            if (menuControl == null || statusBarControl == null || ScreenW == 0 || ScreenH == 0)
            {
                return;
            }
            SetSubwindowSizes();
        }

        /// <summary>
        /// Set the visibility of terrain drawing
        /// </summary>
        /// <param name="isVisible">Normal terrain textures are visible</param>
        /// <param name="isVisibleDM">Distant mountain terrain textures are visible</param>
        /// <returns>true only if there is terrain that can be drawn</returns>
        public bool SetTerrainVisibility(bool isVisible, bool isVisibleDM)
        {
            if (drawTerrain == null)
            {
                return false;
            }

            drawTerrain.SetTerrainVisibility(isVisible, isVisibleDM, DrawArea);
            return true;
        }

        /// <summary>
        /// Set the visibility of the patch lines between terrain
        /// </summary>
        /// <param name="showPatchLines">The value to set the visibility to</param>
        /// <returns>true only if there is terrain that can be drawn</returns>
        public bool SetPatchLineVisibility(bool showPatchLines)
        {
            if (drawTerrain == null)
            {
                return false;
            }

            drawTerrain.SetPatchLineVisibility(showPatchLines);
            return true;
        }

        internal void LoadLabels()
        {
            drawLabels?.LoadLabels();
        }

        internal void SaveLabels()
        {
            drawLabels?.SaveLabels();
        }

        internal void EditMetaData()
        {
            PathEditor?.EditMetaData(Window.ClientBounds.Left + 50, Window.ClientBounds.Top + 20);
        }

        internal void ReversePath()
        {
            PathEditor?.ReversePath(Window.ClientBounds.Left + 50, Window.ClientBounds.Top + 20);
        }

        internal void SetTerrainReduction()
        {
            drawTerrain?.SetTerrainReduction();
        }
        #endregion

        #region Folder and Route methods
        private void HandleCommandLineArgs()
        {
            if (commandLineArgs.Length == 0)
                return;
            string givenPathOrFile = commandLineArgs[0];
            commandLineArgs = Array.Empty<string>(); // discard the arguments, no longer needed

            // given_path_or_file should be something like
            // * C:\...\MSTS\Routes\USA2                        , for a directory
            // * C:\...\MSTS\Routes\USA2\usa2.trk               , for a .trk file
            // * C:\...\MSTS\Routes\USA2\marias.tdb             , for a .tdb file
            // * C:\...\MSTS\Routes\USA2\marias.rdb             , for a .rdb file
            // * C:\...\MSTS\Routes\USA2\PATHS\longhale.pat     , for a .pat file

            //Let's first see if it exists and whether it is a file or directory
            string routeFolder = givenPathOrFile;
            bool givenFileIsPat = false;
            if (System.IO.Directory.Exists(givenPathOrFile))
            {
                // It is a directory
            }
            else if (System.IO.File.Exists(givenPathOrFile))
            {
                // It is a file
                string extension = System.IO.Path.GetExtension(givenPathOrFile).ToUpperInvariant();
                switch (extension)
                {
                    case ".TRK":
                        routeFolder = System.IO.Path.GetDirectoryName(givenPathOrFile);
                        break;
                    case ".TDB":
                        routeFolder = System.IO.Path.GetDirectoryName(givenPathOrFile);
                        break;
                    case ".RDB":
                        routeFolder = System.IO.Path.GetDirectoryName(givenPathOrFile);
                        Properties.Settings.Default.drawRoads = true;
                        menuControl.InitUserSettings();
                        break;
                    case ".PAT":
                        routeFolder = System.IO.Directory.GetParent(System.IO.Path.GetDirectoryName(givenPathOrFile).ToString()).ToString();
                        givenFileIsPat = true;
                        break;

                    default:
                        MessageBox.Show(catalog.GetString($"Route cannot be loaded.\nExtension {extension} is not supported"));
                        return;
                }
            }
            else
            {
                //Obviously, this should only happen when ran on the command line, not when a file is opened using
                MessageBox.Show(catalog.GetString($"Route cannot be loaded.\n{givenPathOrFile} does not exist"));
                return;
            }

            string installFolder = System.IO.Directory.GetParent(System.IO.Directory.GetParent(routeFolder).ToString()).ToString();
            if (!SetSelectedInstallFolder(installFolder))
            {
                MessageBox.Show(catalog.GetString($"Route cannot be loaded.\nWhile trying to open {givenPathOrFile} the folder {installFolder} was inferred as (MSTS or similar) install folder but does not contain expected files"));
                return;
            }

            foreach (RouteModelCore route in Routes)
            {
                //MessageBox.Show(route.Path);

                if (route.MstsRouteFolder().CurrentFolder.Equals(routeFolder, StringComparison.OrdinalIgnoreCase))
                {
                    SetRoute(route);

                    if (!givenFileIsPat)
                    { return; }
                    foreach (PathModelCore availablePath in Paths)
                    {
                        if (System.IO.Path.GetRelativePath(availablePath.SourceFile(), givenPathOrFile.ToUpperInvariant()) == ".")
                        {
                            SetPath(availablePath);
                            menuControl.InitUserSettings();
                            return;
                        }
                    }
                }
            }

            MessageBox.Show(catalog.GetString($"Route cannot be loaded.\n{givenPathOrFile} somehow could not be translated into a loadable route"));
        }

        /// <summary>
        /// Open up a dialog so the user can select the install directory
        /// (which should contain a sub-directory called ROUTES).
        /// </summary>
        /// <returns>True if indeed a new path has been loaded</returns>
        public bool SelectInstallFolder()
        {
            if (!CanDiscardModifiedPath())
                return false;
            string folderPath = "";

            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                if (InstallFolder != null)
                {
                    folderBrowserDialog.SelectedPath = InstallFolder.ContentPath;
                }
                folderBrowserDialog.ShowNewFolderButton = false;
                DialogResult dialogResult = folderBrowserDialog.ShowDialog();

                if (dialogResult == DialogResult.OK)
                {
                    folderPath = folderBrowserDialog.SelectedPath;
                }

                if (string.IsNullOrEmpty(folderPath))
                {
                    return false;
                }

                return SetSelectedInstallFolder(folderPath);
            }
        }

        private bool SetSelectedInstallFolder(string folderPath)
        {
            drawTerrain?.Clear();
            ContentModel contentModel = Task.Run(async () => await ContentModel.None.Get(CancellationToken.None).ConfigureAwait(false)).Result;
            FolderModel newInstallFolder = contentModel.ContentFolders.Where(f => System.IO.Path.GetRelativePath(f.ContentPath, folderPath) == ".").FirstOrDefault();

            bool foundroutes = FindRoutes(newInstallFolder);
            if (!foundroutes)
            {
                MessageBox.Show(catalog.GetString($"Directory \"{folderPath}\" " + " is not a valid install directory.\nPlease make sure the install directory is available through the Menu application!)"));
                return false;
            }

            InstallFolder = newInstallFolder;

            // make sure the current route is disabled,
            CurrentRoute = null;
            DrawTrackDB = null;
            PathEditor = null;
            DrawMultiplePaths = null;

            Properties.Settings.Default.installDirectory = folderPath;
            Properties.Settings.Default.Save();

            return true;
        }

        /// <summary>
        /// Find the available routes, and if possible load the first one.
        /// </summary>
        /// <returns>True if the route loading was successfull</returns>
        private bool FindRoutes(FolderModel newInstallFolder)
        {
            if (newInstallFolder == null)
                return false;

            Routes = new Collection<RouteModelCore>(Task.Run(async() => await newInstallFolder.GetRoutes(CancellationToken.None).ConfigureAwait(false)).Result.ToList());

            // set default route
            DefaultRoute = Routes.Where(r => r.Name == Properties.Settings.Default.defaultRoute).FirstOrDefault() ?? Routes.FirstOrDefault();
                menuControl.PopulateRoutes();
            return DefaultRoute != null;
        }

        /// <summary>
        /// Load the default route. This would be either the route used last time, the current route, or else the first available route.
        /// </summary>
        public void ReloadRoute()
        {
            SetRoute(CurrentRoute ?? DefaultRoute);
        }

        /// <summary>
        /// Set and load a new route
        /// </summary>
        /// <param name="newRoute">The route to load, containing amongst other the directory name of the route</param>
        public void SetRoute(RouteModelCore newRoute)
        {
            if (newRoute == null)
                return;
            if (!CanDiscardModifiedPath())
                return;

            DrawLoadingMessage(catalog.GetString("Loading route..."));
            MessageDelegate messageHandler = new MessageDelegate(DrawLoadingMessage);

            drawTerrain?.Clear();

            try
            {
                RouteData.Load(newRoute, messageHandler);
                DrawTrackDB = new DrawTrackDB(messageHandler);
                drawLabels = new DrawLabels(TextManager.Instance.DefaultFont.Height);
                CurrentRoute = newRoute;

                Properties.Settings.Default.defaultRoute = CurrentRoute.Name;
                if (Properties.Settings.Default.zoomRoutePath != CurrentRoute.MstsRouteFolder().CurrentFolder)
                {
                    Properties.Settings.Default.zoomScale = -1; // To disable the use of zoom reset
                }
                Properties.Settings.Default.Save();
                DrawArea.ZoomReset(DrawTrackDB);
                drawAreaInset.ZoomReset(DrawTrackDB);
                SetTitle();

            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                MessageBox.Show(catalog.GetString("Route cannot be loaded. Sorry"));
            }

            if (CurrentRoute == null)
                return;

            PathEditor = null;
            DrawMultiplePaths = null;
            try
            {
                FindPaths();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch { }
#pragma warning restore CA1031 // Do not catch general exception types

            try
            {
                drawWorldTiles.SetRoute(CurrentRoute.MstsRouteFolder().CurrentFolder);
                drawTerrain = new DrawTerrain(CurrentRoute.MstsRouteFolder().CurrentFolder, messageHandler, drawWorldTiles);
                drawTerrain.LoadContent(GraphicsDevice);
                menuControl.MenuSetShowTerrain(false);
                menuControl.MenuSetShowDMTerrain(false);

            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch { }
#pragma warning restore CA1031 // Do not catch general exception types


            menuControl.PopulatePlatforms();
            menuControl.PopulateStations();
            menuControl.PopulateSidings();
        }

        /// <summary>
        /// Set the title of the window itself
        /// </summary>
        private void SetTitle()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyTitleAttribute assemblyTitle = assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), false)[0] as AssemblyTitleAttribute;
            Window.Title = assemblyTitle.Title + ": " + RuntimeData.Instance.RouteData.Name;
        }

        #endregion

        #region Path methods
        /// <summary>
        /// Find the paths (.pat files) belonging to the current route, and update the menu
        /// </summary>
        private void FindPaths()
        {
            ImmutableArray<PathModelCore> routePaths = Task.Run(async() => await CurrentRoute.GetRoutePaths(CancellationToken.None).ConfigureAwait(false)).Result;
            Paths = new Collection<PathModelCore>(routePaths.OrderBy(r => r.Name).ToList());
            menuControl.PopulatePaths();
            SetPath(null);
            DrawMultiplePaths = new DrawMultiplePaths(Paths);
        }

        /// <summary>
        /// Once a path has been selected, do the necessary loading.
        /// </summary>
        /// <param name="path">Path (with FilePath) that has to be loaded</param>
        internal void SetPath(PathModelCore path)
        {
            if (!CanDiscardModifiedPath())
                return;

            if (path == null)
            {
                DrawPATfile = null;
                PathEditor = null;
                drawPathChart.Close();
            }
            else
            {
                DrawLoadingMessage(catalog.GetString("Loading .pat file ..."));
                DrawPATfile = new DrawPATfile(path);

                DrawLoadingMessage(catalog.GetString("Processing .pat file ..."));
                PathEditor = new PathEditor(DrawTrackDB, path);
                drawPathChart.SetPathEditor(PathEditor);

                DrawLoadingMessage(" ...");
            }
        }

        internal void NewPath()
        {
            if (!CanDiscardModifiedPath())
                return;
            string pathsDirectory = System.IO.Path.Combine(CurrentRoute.MstsRouteFolder().CurrentFolder, "PATHS");
            PathEditor = new PathEditor(DrawTrackDB, pathsDirectory);
            drawPathChart.SetPathEditor(PathEditor);
            DrawPATfile = null;
            menuControl.SetEnableEditing();
            EditMetaData();
        }

        /// <summary>
        /// If the path has been modified, ask the user if he really wants to discard it
        /// </summary>
        /// <returns>false if there is a modified path that the user does not want to discard.</returns>
        private bool CanDiscardModifiedPath()
        {
            if (PathEditor == null)
                return true;
            if (!PathEditor.HasModifiedPath)
                return true;
            DialogResult dialogResult = MessageBox.Show(
                        catalog.GetString("Path has been modified. Loading a new path will discard changes.") + "\n" +
                        catalog.GetString("Do you want to continue?"),
                        catalog.GetString("Trackviewer Path Editor"), MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Question);
            return (dialogResult == DialogResult.OK);
        }

        #endregion

        #region Centering methods
        /// <summary>
        /// Find a track node, center around it and highlight it
        /// </summary>
        /// <param name="trackNumberIndex">Index of the track node</param>
        public void CenterAroundTrackNode(int trackNumberIndex)
        {
            CenterAround(DrawTrackDB.TrackNodeHighlightOverride(trackNumberIndex));
        }

        /// <summary>
        /// Find a Road track node, center around it and highlight it
        /// </summary>
        /// <param name="trackNumberIndex">Index of the track node</param>
        public void CenterAroundTrackNodeRoad(int trackNumberIndex)
        {
            CenterAround(DrawTrackDB.TrackNodeHighlightOverrideRoad(trackNumberIndex));
        }

        /// <summary>
        /// Find a trackItem and center around it and highlight it
        /// </summary>
        /// <param name="trackItemIndex">Index of the track item</param>
        public void CenterAroundTrackItem(int trackItemIndex)
        {
            WorldLocation itemLocation = DrawTrackDB.TrackItemHighlightOverride(trackItemIndex);
            if (itemLocation == WorldLocation.None)
                return;
            CenterAround(itemLocation);
        }

        /// <summary>
        /// Find a road trackItem and center around it and highlight it
        /// </summary>
        /// <param name="trackItemIndex">Index of the track item</param>
        public void CenterAroundTrackItemRoad(int trackItemIndex)
        {
            WorldLocation itemLocation = DrawTrackDB.TrackItemHighlightOverrideRoad(trackItemIndex);
            if (itemLocation == WorldLocation.None)
                return;
            CenterAround(itemLocation);
        }

        /// <summary>
        /// Center around a certain world-location. In particular, outside the normal Draw/Update loop. So it does a draw itself
        /// To be used from additional windows (like search).
        /// </summary>
        /// <param name="centerLocation">Location to center the view window around</param>
        public void CenterAround(in WorldLocation centerLocation)
        {
            if (centerLocation == WorldLocation.None)
                return;

            DrawArea.ShiftToLocation(centerLocation);
            DrawArea.Update();
            DrawArea.MouseLocation = centerLocation;
            drawAreaInset.Follow(DrawArea, 10f);
            BeginDraw();
            skipDrawAmount = 0; // make sure the draw is really done.
            Draw(new GameTime());
            EndDraw();

        }

        #endregion

        #region RestoreBrokenPaths
        /// <summary>
        /// Attempt to Auto Restore all broken paths
        /// </summary>
        public void AutoRestorePaths()
        {
            DialogResult dialogResult = MessageBox.Show(
                        catalog.GetString("This will open every single .pat file for this route, (try to) fix all broken nodes, and save the modified path. ") +
                        catalog.GetString("Potentially it will therefore change all .pat files on disc.") + "\n" +
                        catalog.GetString("This can be useful when a route has been changed and you want all paths to be corrected.") + "\n\n" +
                        catalog.GetString("Do you want to continue?"),
                        catalog.GetString("Trackviewer Path Editor"), MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Question);
            if (dialogResult != DialogResult.OK)
            { return; }

            //Close all paths that are drawn.
            if (!CanDiscardModifiedPath())
                return;
            PathEditor = null;
            DrawMultiplePaths?.ClearAll();

            AutoFixAllPaths fixer = new AutoFixAllPaths(DrawTrackDB);
            Localize(fixer);
            fixer.FixallAndShowResults(Paths, (message) => DrawLoadingMessage(message));
        }
        #endregion

        #region Debug methods
        private void CalculateFPS(GameTime gameTime)
        {
            float elapsedRealTime = (float)gameTime.TotalGameTime.TotalSeconds;
            FrameRate.Update(elapsedRealTime, 1f / elapsedRealTime);
        }

        #endregion

        #region Language and localization
        /// <summary>
        /// This is the 'catalog' needed for localization of TrackViewer (meaning translating it to different languages)
        /// </summary>
        internal static ICatalog catalog = new Catalog("Contrib", RuntimeInfo.LocalesFolder);

        /// <summary>
        /// Routine to localize (make languague-dependent) a WPF/framework element, like a menu.
        /// </summary>
        /// <param name="element">The element that is checked for localizable parameters</param>
        public static void Localize(System.Windows.FrameworkElement element)
        {
            if (null == element)
                return;

            foreach (object child in System.Windows.LogicalTreeHelper.GetChildren(element))
            {
                System.Windows.FrameworkElement childAsElement = child as System.Windows.FrameworkElement;
                if (childAsElement != null)
                {
                    Localize(childAsElement);
                }
            }

            Type objType = element.GetType();
            PropertyInfo property;
            string[] propertyTags = { "Content", "Header", "Text", "Title", "ToolTip" };

            foreach (string tag in propertyTags)
            {
                property = objType.GetProperty(tag);
                if (property != null && property.CanRead && property.CanWrite && property.GetValue(element, null) is string)
                    property.SetValue(element, catalog.GetString(property.GetValue(element, null) as string), null);
            }
        }
        #endregion

        #region IDisposable
        private bool disposed;

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    drawAreaInset?.Dispose();
                    drawTerrain?.Dispose();
                    statusBarControl?.Dispose();
                    menuControl?.Dispose();
                    spriteBatch?.Dispose();
                    graphics?.Dispose();
                }
                disposed = true;
            }
            base.Dispose(disposing);
        }
        #endregion
    }
}
