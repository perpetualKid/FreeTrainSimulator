// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015, 2016, 2017 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D.Common;
using Orts.ActivityRunner.Viewer3D.Popups;
using Orts.ActivityRunner.Viewer3D.PopupWindows;
using Orts.ActivityRunner.Viewer3D.RollingStock;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.DebugInfo;
using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Common.Native;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Graphics.Xna;
using Orts.Settings;
using Orts.Settings.Util;
using Orts.Simulation;
using Orts.Simulation.Activities;
using Orts.Simulation.AIs;
using Orts.Simulation.Commanding;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.World;

namespace Orts.ActivityRunner.Viewer3D
{
    public enum DetailInfoType
    {
        GraphicDetails,
        GameDetails,
        WeatherDetails,
        TrainDetails,
        ConsistDetails,
        LocomotiveDetails,
        LocomotiveForce,
        ForceDetails,
        LocomotiveBrake,
        BrakeDetails,
        PowerSupplyDetails,
        DistributedPowerDetails,
        DispatcherDetails,
    }


    public class Viewer
    {
        private bool pauseWindow;

        public static ICatalog Catalog { get; private set; }
        // User setups.
        public UserSettings Settings { get; private set; }

        public UserCommandController<UserCommand> UserCommandController { get; }

        public EnumArray<DetailInfoBase, DetailInfoType> DetailInfo { get; } = new EnumArray<DetailInfoBase, DetailInfoType>();

        // Multi-threaded processes
        internal LoaderProcess LoaderProcess { get; private set; }
        internal UpdaterProcess UpdaterProcess { get; private set; }
        internal RenderProcess RenderProcess { get; private set; }
        internal SoundProcess SoundProcess { get; private set; }
        public string ContentPath { get; private set; }
        public SharedTextureManager TextureManager { get; private set; }
        public SharedMaterialManager MaterialManager { get; private set; }
        public SharedShapeManager ShapeManager { get; private set; }
        public SignalTypeDataManager SignalTypeDataManager { get; private set; }
        public Point DisplaySize { get { return RenderProcess.DisplaySize; } }
        // Components
        public GameHost Game { get; private set; }
        public Simulator Simulator { get; private set; }
        public World World { get; private set; }
        private SoundSource ViewerSounds { get; set; }
        /// <summary>
        /// Monotonically increasing time value (in seconds) for the game/viewer. Starts at 0 and only ever increases, at real-time.
        /// </summary>
        public double RealTime { get; private set; }

        private Thread dispatcherThread;
        private Dispatcher.DispatcherWindow dispatcherWindow;

        private Orts.Graphics.Window.WindowManager<ViewerWindowType> windowManager;

        private InfoDisplay InfoDisplay;
        public WindowTextManager TextManager { get; } = new WindowTextManager();
        // Route Information
        public TileManager Tiles { get; private set; }
        public TileManager LoTiles { get; private set; }
        public EnvironmentFile ENVFile { get; private set; }
        public TrackTypesFile TrackTypes { get; private set; }
        public SpeedpostDatFile SpeedpostDatFile;
        public bool MilepostUnitsMetric { get; private set; }
        // Cameras
        public Camera Camera { get; set; } // Current camera
        public Camera AbovegroundCamera { get; private set; } // Previous camera for when automatically switching to cab.
        public CabCamera CabCamera { get; private set; } // Camera 1
        public HeadOutCamera HeadOutForwardCamera { get; private set; } // Camera 1+Up
        public HeadOutCamera HeadOutBackCamera { get; private set; } // Camera 2+Down
        public TrackingCamera FrontCamera { get; private set; } // Camera 2
        public TrackingCamera BackCamera { get; private set; } // Camera 3
        public TracksideCamera TracksideCamera { get; private set; } // Camera 4
        public SpecialTracksideCamera SpecialTracksideCamera { get; private set; } // Camera 4 for special points (platforms and level crossings)
        public PassengerCamera PassengerCamera { get; private set; } // Camera 5
        public BrakemanCamera BrakemanCamera { get; private set; } // Camera 6
        public List<FreeRoamCamera> FreeRoamCameraList = new List<FreeRoamCamera>();
        public FreeRoamCamera FreeRoamCamera { get { return FreeRoamCameraList[0]; } } // Camera 8
        public CabCamera3D ThreeDimCabCamera; //Camera 0

        private List<Camera> WellKnownCameras; // Providing Camera save functionality by GeorgeS

        private TrainCarViewer playerLocomotiveViewer;

        private Camera currentCamera;
        private float fieldOfView;


        public TrainCarViewer PlayerLocomotiveViewer
        {
            get => playerLocomotiveViewer;
            private set
            {
                playerLocomotiveViewer?.UnregisterUserCommandHandling();
                playerLocomotiveViewer = value;
                playerLocomotiveViewer?.RegisterUserCommandHandling();
            }
        }  // we are controlling this loco, or null if we aren't controlling any

        // This is the train we are controlling
        public MSTSLocomotive PlayerLocomotive { get { return Simulator.PlayerLocomotive; } set { Simulator.PlayerLocomotive = value; } }
        public Train PlayerTrain { get { if (PlayerLocomotive == null) return null; else return PlayerLocomotive.Train; } }

        public Process CurrentProcess { get; } = Process.GetCurrentProcess();

        // This is the train we are viewing
        public Train SelectedTrain { get; private set; }

        private void CameraActivate()
        {
            if (Camera == null || !Camera.IsAvailable) //passenger camera may jump to a train without passenger view
                FrontCamera.Activate();
            else
                Camera.Activate();
        }

        private bool forceMouseVisible;
        private double mouseVisibleTillRealTime;
        private Cursor actualCursor = Cursors.Default;
        public static Viewport DefaultViewport;
        public bool SaveScreenshot { get; set; }
        public bool SaveActivityThumbnail { get; private set; }
        public string SaveActivityFileStem { get; private set; }

        public bool DebugViewerEnabled { get; set; }
        public bool SoundDebugFormEnabled { get; set; }

        public TRPFile TRP; // Track profile file

        private enum VisibilityState
        {
            Visible,
            Hidden,
            ScreenshotPending,
        };

        private VisibilityState Visibility = VisibilityState.Visible;

        // MSTS cab views are images with aspect ratio 4:3.
        // OR can use cab views with other aspect ratios where these are available.
        // On screen with other aspect ratios (e.g. 16:9), three approaches are possible:
        //   1) stretch the width to fit the screen. This gives flattened controls, most noticeable with round dials.
        //   2) clip the image losing a slice off top and bottom.
        //   3) letterbox the image by drawing black bars in the unfilled spaces.
        // Setting.Cab2DStretch controls the amount of stretch and clip. 0 is entirely clipped and 100 is entirely stretched.
        // No difference is seen on screens with 4:3 aspect ratio.
        // This adjustment assumes that the cab view is 4:3. Where the cab view matches the aspect ratio of the screen, use an adjustment of 100.
        public int CabHeightPixels { get; private set; }
        public int CabWidthPixels { get; private set; }
        public int CabYOffsetPixels { get; set; } // Note: Always -ve. Without it, the cab view is fixed to the top of the screen. -ve values pull it up the screen.
        public int CabXOffsetPixels { get; set; }
        public int CabExceedsDisplay; // difference between cabview texture vertical resolution and display vertical resolution
        public int CabExceedsDisplayHorizontally; // difference between cabview texture horizontal resolution and display vertical resolution
        public int CabYLetterboxPixels { get; set; } // offset the cab when drawing it if it is smaller than the display; both coordinates should always be >= 0
        public int CabXLetterboxPixels { get; set; }
        public float CabTextureInverseRatio = 0.75f; // default of inverse of cab texture ratio 

        public CommandLog Log { get { return Simulator.Log; } }
        public static bool ClockTimeBeforeNoon => Simulator.Instance.ClockTime % 86400 < 43200;

        // After dawn and before dusk, so definitely daytime
        public bool Daytime => (MaterialManager.sunDirection.Y > 0.05f && ClockTimeBeforeNoon) || (MaterialManager.sunDirection.Y > 0.15f && !ClockTimeBeforeNoon);

        // Before dawn and after dusk, so definitely nighttime
        public bool Nighttime => (MaterialManager.sunDirection.Y < -0.05f && !ClockTimeBeforeNoon) || (MaterialManager.sunDirection.Y < -0.15f && ClockTimeBeforeNoon);

        public bool NightTexturesNotLoaded; // At least one night texture hasn't been loaded
        public bool DayTexturesNotLoaded; // At least one day texture hasn't been loaded
        public long LoadMemoryThreshold; // Above this threshold loader doesn't bulk load day or night textures
        public bool tryLoadingNightTextures;
        public bool tryLoadingDayTextures;

        public Camera SuspendedCamera { get; private set; }

        private bool lockShadows;
        private bool logRenderFrame;
        private bool uncoupleWithMouseActive;

        /// <summary>
        /// Finds time of last entry to set ReplayEndsAt and provide the Replay started message.
        /// </summary>
        private void InitReplay()
        {
            if (Simulator.ReplayCommandList != null)
            {
                // Get time of last entry
                int lastEntry = Simulator.ReplayCommandList.Count - 1;
                if (lastEntry >= 0)
                {
                    double lastTime = Simulator.ReplayCommandList[lastEntry].Time;
                    Log.ReplayEndsAt = lastTime;
                    double duration = lastTime - Simulator.ClockTime;
                    Simulator.Confirmer.PlainTextMessage(ConfirmLevel.Message, $"Replay started: ending at {FormatStrings.FormatApproximateTime(lastTime)} after {FormatStrings.FormatTime(duration)}", 3.0);
                }
            }
        }

        /// <summary>
        /// Initializes a new instances of the <see cref="Viewer3D"/> class based on the specified <paramref name="simulator"/> and <paramref name="game"/>.
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> with which the viewer runs.</param>
        /// <param name="game">The <see cref="Game"/> with which the viewer runs.</param>
        public Viewer(Simulator simulator, GameHost game)
        {
            CatalogManager.SetCatalogDomainPattern(CatalogDomainPattern.AssemblyName, null, RuntimeInfo.LocalesFolder);
            Catalog = CatalogManager.Catalog;

            Simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Settings = simulator.Settings;

            RenderProcess = game.RenderProcess;
            UpdaterProcess = game.UpdaterProcess;
            LoaderProcess = game.LoaderProcess;
            SoundProcess = game.SoundProcess;

            UserCommandController = new UserCommandController<UserCommand>();

            WellKnownCameras = new List<Camera>();
            WellKnownCameras.Add(CabCamera = new CabCamera(this));
            WellKnownCameras.Add(FrontCamera = new TrackingCamera(this, TrackingCamera.AttachedTo.Front));
            WellKnownCameras.Add(BackCamera = new TrackingCamera(this, TrackingCamera.AttachedTo.Rear));
            WellKnownCameras.Add(PassengerCamera = new PassengerCamera(this));
            WellKnownCameras.Add(BrakemanCamera = new BrakemanCamera(this));
            WellKnownCameras.Add(HeadOutForwardCamera = new HeadOutCamera(this, HeadOutCamera.HeadDirection.Forward));
            WellKnownCameras.Add(HeadOutBackCamera = new HeadOutCamera(this, HeadOutCamera.HeadDirection.Backward));
            WellKnownCameras.Add(TracksideCamera = new TracksideCamera(this));
            WellKnownCameras.Add(SpecialTracksideCamera = new SpecialTracksideCamera(this));
            WellKnownCameras.Add(new FreeRoamCamera(this, FrontCamera)); // Any existing camera will suffice to satisfy .Save() and .Restore()
            WellKnownCameras.Add(ThreeDimCabCamera = new CabCamera3D(this));

            ContentPath = RuntimeInfo.ContentFolder;
            Trace.Write(" ENV");
            ENVFile = new EnvironmentFile(Path.Combine(Simulator.RouteFolder.EnvironmentFolder, Simulator.Route.Environment.GetEnvironmentFileName(Simulator.Season, Simulator.WeatherType)));

            Trace.Write(" TTYPE");
            TrackTypes = new TrackTypesFile(Path.Combine(Simulator.RouteFolder.CurrentFolder, "TTYPE.DAT"));

            Tiles = new TileManager(Simulator.RouteFolder.TilesFolder, false);
            LoTiles = new TileManager(Simulator.RouteFolder.TilesFolderLow, true);
            MilepostUnitsMetric = Simulator.Route.MilepostUnitsMetric;

            Simulator.AllowedSpeedRaised += (object sender, EventArgs e) =>
            {
                var train = sender as Train;
                if (Simulator.Confirmer != null && train != null)
                {
                    var message = Catalog.GetString("Allowed speed raised to {0}", FormatStrings.FormatSpeedDisplay(train.AllowedMaxSpeedMpS, MilepostUnitsMetric));
                    Simulator.Confirmer.Message(ConfirmLevel.Information, message);
                }
            };

            Simulator.PlayerLocomotiveChanged += PlayerLocomotiveChanged;
            Simulator.PlayerTrainChanged += PlayerTrainChanged;
            Simulator.RequestTTDetachWindow += RequestTimetableDetachWindow;

            if (Simulator.ActivityRun != null)
            {
                // The speedpost.dat file is needed only to derive the shape names for the temporary speed restriction zones,
                // so it is opened only in activity mode
                if (Simulator.ActivityFile.Activity.ActivityRestrictedSpeedZones != null)
                {
                    string speedpostDatFile = Path.Combine(Simulator.RouteFolder.CurrentFolder, "speedpost.dat");
                    if (File.Exists(speedpostDatFile))
                    {
                        Trace.Write(" SPEEDPOST");
                        SpeedpostDatFile = new SpeedpostDatFile(speedpostDatFile, Simulator.RouteFolder.ShapesFolder);
                    }
                }
            }

            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.GraphicDetails] = new GraphicInformation(this));
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.GameDetails] = new RouteInformation());
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.WeatherDetails] = new WeatherInformation());
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.TrainDetails] = new TrainInformation(Catalog as Catalog));
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.ConsistDetails] = new ConsistInformation(Catalog as Catalog));
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.DistributedPowerDetails] = new DistributedPowerInformation());
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.DispatcherDetails] = new DispatcherInformation(Catalog as Catalog));
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.LocomotiveDetails] = new LocomotiveInformation());
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.ForceDetails] = new ForceInformation(Catalog as Catalog));
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.LocomotiveForce] = new LocomotiveForceInformation());
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.LocomotiveBrake] = new LocomotiveBrakeInformation());
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.BrakeDetails] = new BrakeInformation());
            game.SystemProcess.Updateables.Add(DetailInfo[DetailInfoType.PowerSupplyDetails] = new PowerSupplyInformation());
        }

        private void ActivityRun_OnEventTriggered(object sender, ActivityEventArgs e)
        {
            (windowManager[ViewerWindowType.ActivityWindow] as PopupWindows.ActivityWindow).OpenFromEvent(e.TriggeredEvent);
        }

        private void SaveSettings()
        {
            foreach (ViewerWindowType windowType in EnumExtension.GetValues<ViewerWindowType>())
            {
                if (windowManager.WindowInitialized(windowType))
                {
                    Settings.PopupLocations[windowType] = PointExtension.ToArray(windowManager[windowType].RelativeLocation);
                }
                Settings.PopupStatus[windowType] = windowManager.WindowOpened(windowType);
            }

            Settings.Save(nameof(Settings.PopupLocations));
            Settings.Save(nameof(Settings.PopupStatus));
            Settings.Save(nameof(Settings.PopupSettings));
            Settings.Save(nameof(Settings.OdometerShortDistanceMode));
        }

        public void Save(BinaryWriter outf, string fileStem)
        {
            outf.Write(Simulator.Trains.IndexOf(PlayerTrain));
            outf.Write(PlayerTrain.Cars.IndexOf(PlayerLocomotive));
            outf.Write(Simulator.Trains.IndexOf(SelectedTrain));

            outf.Write(WellKnownCameras.IndexOf(Camera));
            foreach (var camera in WellKnownCameras)
                camera.Save(outf);
            Camera.Save(outf);
            outf.Write(CabYOffsetPixels);
            outf.Write(CabXOffsetPixels);

            // Set these so RenderFrame can use them when its thread gets control.
            SaveActivityFileStem = fileStem;
            SaveActivityThumbnail = true;
            outf.Write(NightTexturesNotLoaded);
            outf.Write(DayTexturesNotLoaded);
            World.WeatherControl.SaveWeatherParameters(outf);
            if ((PlayerLocomotiveViewer as MSTSLocomotiveViewer).CabRenderer != null)
            {
                outf.Write(0);
                (PlayerLocomotiveViewer as MSTSLocomotiveViewer).CabRenderer.Save(outf);
            }
            else
                outf.Write(-1);
            if ((PlayerLocomotiveViewer as MSTSLocomotiveViewer).CabRenderer3D != null)
            {
                outf.Write(0);
                (PlayerLocomotiveViewer as MSTSLocomotiveViewer).CabRenderer3D.Save(outf);
            }
            else
                outf.Write(-1);
        }

        public void Restore(BinaryReader inf)
        {
            Train playerTrain = Simulator.Trains[inf.ReadInt32()];
            PlayerLocomotive = playerTrain.Cars[inf.ReadInt32()] as MSTSLocomotive;
            var selected = inf.ReadInt32();
            if (selected >= 0 && selected < Simulator.Trains.Count)
            {
                SelectedTrain = Simulator.Trains[selected];
            }
            else if (selected < 0)
            {
                SelectedTrain = Simulator.Trains[0];
            }

            var cameraToRestore = inf.ReadInt32();
            foreach (var camera in WellKnownCameras)
                camera.Restore(inf);
            if (cameraToRestore == -1)
                new FreeRoamCamera(this, Camera).Activate();
            else
                WellKnownCameras[cameraToRestore].Activate();
            Camera.Restore(inf);
            CabYOffsetPixels = inf.ReadInt32();
            CabXOffsetPixels = inf.ReadInt32();
            NightTexturesNotLoaded = inf.ReadBoolean();
            DayTexturesNotLoaded = inf.ReadBoolean();
            LoadMemoryThreshold = (long)GetVirtualAddressLimit() - 512;// * 1024 * 1024; <-- this seemed wrong as the virtual address limit is already given in bytes
            tryLoadingNightTextures = true;
            tryLoadingDayTextures = true;

            World.WeatherControl.RestoreWeatherParameters(inf);
            var cabRendererPresent = inf.ReadInt32();
            if (cabRendererPresent != -1)
                (PlayerLocomotiveViewer as MSTSLocomotiveViewer).CabRenderer.Restore(inf);
            cabRendererPresent = inf.ReadInt32();
            if (cabRendererPresent != -1)
                (PlayerLocomotiveViewer as MSTSLocomotiveViewer).CabRenderer3D.Restore(inf);
        }

        /// <summary>
        /// Called once after the graphics device is ready
        /// to load any static graphics content, background
        /// processes haven't started yet.
        /// </summary>
        internal void Initialize()
        {
            #region Input Command Controller
            KeyboardInputGameComponent keyboardInputGameComponent = new KeyboardInputGameComponent(Game);
            KeyboardInputHandler<UserCommand> keyboardInput = new KeyboardInputHandler<UserCommand>();
            keyboardInput.Initialize(Settings.Input.UserCommands, keyboardInputGameComponent, UserCommandController);

            MouseInputGameComponent mouseInputGameComponent = new MouseInputGameComponent(Game);
            MouseInputHandler<UserCommand> mouseInput = new MouseInputHandler<UserCommand>();
            mouseInput.Initialize(mouseInputGameComponent, keyboardInputGameComponent, UserCommandController);

            RailDriverInputGameComponent railDriverInputGameComponent = new RailDriverInputGameComponent(Game, Settings.RailDriver.CalibrationSettings);
            RailDriverInputHandler<UserCommand> railDriverInput = new RailDriverInputHandler<UserCommand>();
            railDriverInput.Initialize(Settings.RailDriver.UserCommands, railDriverInputGameComponent, UserCommandController);
            #endregion

            DefaultViewport = Game.GraphicsDevice.Viewport;

            if (PlayerLocomotive == null)
                PlayerLocomotive = Simulator.InitialPlayerLocomotive();
            SelectedTrain = PlayerTrain;
            PlayerTrain.InitializePlayerTrainData();
            if (PlayerTrain.TrainType == TrainType.AiPlayerHosting)
            {
                Simulator.InitializeAiPlayerHosting();
            }

            SharedSMSFileManager.Initialize(TrackTypes.Count, Simulator.Route.SwitchSMSNumber, Simulator.Route.CurveSMSNumber, Simulator.Route.CurveSwitchSMSNumber);

            TextureManager = new SharedTextureManager(this, Game.GraphicsDevice);

            AdjustCabHeight(DisplaySize.X, DisplaySize.Y);

            MaterialManager = new SharedMaterialManager(this);
            ShapeManager = new SharedShapeManager(this);
            SignalTypeDataManager = new SignalTypeDataManager(this);

            windowManager = Orts.Graphics.Window.WindowManager.Initialize<UserCommand, ViewerWindowType>(Game, UserCommandController.AddTopLayerController());
            windowManager.MultiLayerModalWindows = true;
            windowManager.WindowOpacity = 0.4f;
            windowManager.OnModalWindow += WindowManager_OnModalWindow;
            windowManager.SetLazyWindows(ViewerWindowType.QuitWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new QuitWindow(windowManager, Settings.PopupLocations[ViewerWindowType.QuitWindow].ToPoint(), Settings);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.HelpWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new HelpWindow(windowManager, Settings.PopupLocations[ViewerWindowType.HelpWindow].ToPoint(), Settings, this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.ActivityWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new ActivityWindow(windowManager, Settings.PopupLocations[ViewerWindowType.ActivityWindow].ToPoint(), this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.CompassWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new CompassWindow(windowManager, Settings.PopupLocations[ViewerWindowType.CompassWindow].ToPoint(), this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.SwitchWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new SwitchWindow(windowManager, Settings.PopupLocations[ViewerWindowType.SwitchWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(ViewerWindowType.EndOfTrainDeviceWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new EndOfTrainDeviceWindow(windowManager, Settings.PopupLocations[ViewerWindowType.EndOfTrainDeviceWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(ViewerWindowType.NextStationWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new NextStationWindow(windowManager, Settings.PopupLocations[ViewerWindowType.NextStationWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(ViewerWindowType.DetachTimetableTrainWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new TimetableDetachWindow(windowManager, Settings.PopupLocations[ViewerWindowType.DetachTimetableTrainWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(ViewerWindowType.TrainListWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new TrainListWindow(windowManager, Settings.PopupLocations[ViewerWindowType.TrainListWindow].ToPoint(), this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.MultiPlayerWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new MultiPlayerWindow(windowManager, Settings.PopupLocations[ViewerWindowType.MultiPlayerWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(ViewerWindowType.DistributedPowerWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new DistributedPowerWindow(windowManager, Settings.PopupLocations[ViewerWindowType.DistributedPowerWindow].ToPoint(), Settings, this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.DrivingTrainWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new DrivingTrainWindow(windowManager, Settings.PopupLocations[ViewerWindowType.DrivingTrainWindow].ToPoint(), Settings, this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.PauseOverlay, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new PauseOverlay(windowManager, this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.TrainOperationsWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new TrainOperationsWindow(windowManager, Settings.PopupLocations[ViewerWindowType.TrainOperationsWindow].ToPoint(), this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.CarOperationsWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new CarOperationsWindow(windowManager, this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.TrackMonitorWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new TrackMonitorWindow(windowManager, Settings.PopupLocations[ViewerWindowType.TrackMonitorWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(ViewerWindowType.MultiPlayerMessagingWindow, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new MultiPlayerMessaging(windowManager, Settings.PopupLocations[ViewerWindowType.MultiPlayerMessagingWindow].ToPoint());
            }));
            windowManager.SetLazyWindows(ViewerWindowType.NotificationOverlay, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new NotificationOverlay(windowManager);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.DebugOverlay, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new DebugOverlay(windowManager, Settings, this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.CarIdentifierOverlay, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new CarIdentifierOverlay(windowManager, Settings, this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.LocationsOverlay, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new LocationOverlay(windowManager, Settings, this);
            }));
            windowManager.SetLazyWindows(ViewerWindowType.TrackItemOverlay, new Lazy<Orts.Graphics.Window.FormBase>(() =>
            {
                return new TrackDebugOverlay(windowManager, Settings, this);
            }));

            Game.Components.Add(windowManager);

            InfoDisplay = new InfoDisplay(this);

            World = new World(this, Simulator.ClockTime);

            ViewerSounds = new SoundSource(soundSource => new[]
            {
                new SoundStream(soundSource, soundStream => new[]
                {
                    new ORTSDiscreteTrigger(soundStream, TrainEvent.TakeScreenshot, ORTSSoundCommand.Precompiled(Path.Combine(ContentPath, "TakeScreenshot.wav"), soundStream)),
                }),
            });
            SoundProcess.AddSoundSource(this, ViewerSounds);
            Simulator.Confirmer.PlayErrorSound += (s, e) =>
            {
                if (World.GameSounds != null)
                    World.GameSounds.HandleEvent(TrainEvent.ControlError);
            };
            Simulator.Confirmer.DisplayMessage += (s, e) => (windowManager[ViewerWindowType.NotificationOverlay] as NotificationOverlay).AddMessage(e.Key, e.Text, e.Duration);

            if (Simulator.PlayerLocomotive.HasFront3DCab || Simulator.PlayerLocomotive.HasRear3DCab)
            {
                ThreeDimCabCamera.Enabled = true;
                ThreeDimCabCamera.Activate();
            }
            else if (Simulator.PlayerLocomotive.HasFrontCab || Simulator.PlayerLocomotive.HasRearCab)
                CabCamera.Activate();
            else
                CameraActivate();

            // Prepare the world to be loaded and then load it from the correct thread for debugging/tracing purposes.
            // This ensures that a) we have all the required objects loaded when the 3D view first appears and b) that
            // all loading is performed on a single thread that we can handle in debugging and tracing.
            World.LoadPrep();
            MaterialManager.LoadPrep();
            LoadMemoryThreshold = (long)GetVirtualAddressLimit() - 512; // * 1024 * 1024; <-- this seemed wrong as the virtual address limit is already given in bytes
            Load();

            // MUST be after loading is done! (Or we try and load shapes on the main thread.)
            PlayerLocomotiveViewer = World.Trains.GetViewer(PlayerLocomotive);

            #region UserCommmands
            if (MultiPlayerManager.IsMultiPlayer())
            {
                UserCommandController.AddEvent(UserCommand.GamePauseMenu, KeyEventType.KeyPressed, () => Simulator.Confirmer?.Information(Catalog.GetString("In multiplayer mode, use Alt-F4 to quit directly")));
                UserCommandController.AddEvent(UserCommand.GameMultiPlayerTexting, KeyEventType.KeyPressed, () =>
                {
                    windowManager[ViewerWindowType.MultiPlayerMessagingWindow].ToggleVisibility();
                });
            }
            else
            {
                UserCommandController.AddEvent(UserCommand.GamePauseMenu, KeyEventType.KeyPressed, () =>
                {
                    Pause(!Simulator.GamePaused);
                });
                UserCommandController.AddEvent(UserCommand.GamePause, KeyEventType.KeyPressed, () => Simulator.GamePaused = !Simulator.GamePaused);
                UserCommandController.AddEvent(UserCommand.DebugSpeedUp, KeyEventType.KeyPressed, () =>
                {
                    Simulator.GameSpeed *= 1.5f;
                    Simulator.Confirmer.ConfirmWithPerCent(CabControl.SimulationSpeed, CabSetting.Increase, Simulator.GameSpeed * 100);
                });
                UserCommandController.AddEvent(UserCommand.DebugSpeedDown, KeyEventType.KeyPressed, () =>
                {
                    Simulator.GameSpeed /= 1.5f;
                    Simulator.Confirmer.ConfirmWithPerCent(CabControl.SimulationSpeed, CabSetting.Decrease, Simulator.GameSpeed * 100);
                });
                UserCommandController.AddEvent(UserCommand.DebugSpeedReset, KeyEventType.KeyPressed, () =>
                {
                    Simulator.GameSpeed = 1;
                    Simulator.Confirmer.ConfirmWithPerCent(CabControl.SimulationSpeed, CabSetting.Off, Simulator.GameSpeed * 100);
                });
            }
            if (MultiPlayerManager.IsMultiPlayer() || (Settings.MultiplayerClient && Simulator.Confirmer != null))
            {
                UserCommandController.AddEvent(UserCommand.DisplayMultiPlayerWindow, KeyEventType.KeyPressed, () =>
                {
                    windowManager[ViewerWindowType.MultiPlayerWindow].ToggleVisibility();
                });
            }
            UserCommandController.AddEvent(UserCommand.DisplayHUD, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ViewerWindowType.DebugOverlay].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.GameFullscreen, KeyEventType.KeyPressed, RenderProcess.ToggleFullScreen);
            UserCommandController.AddEvent(UserCommand.GameSave, KeyEventType.KeyPressed, GameStateRunActivity.Save);
            UserCommandController.AddEvent(UserCommand.DisplayHelpWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ViewerWindowType.HelpWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplayTrackMonitorWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ViewerWindowType.TrackMonitorWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplayTrainDrivingWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ViewerWindowType.DrivingTrainWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplaySwitchWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                windowManager[ViewerWindowType.SwitchWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplayTrainOperationsWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                windowManager[ViewerWindowType.TrainOperationsWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplayDistributedPowerWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ViewerWindowType.DistributedPowerWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplayNextStationWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                windowManager[ViewerWindowType.NextStationWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplayCompassWindow, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                windowManager[ViewerWindowType.CompassWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DebugTracks, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ViewerWindowType.TrackItemOverlay].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DebugSignalling, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                //
            });
            UserCommandController.AddEvent(UserCommand.DisplayTrainListWindow, KeyEventType.KeyPressed, () =>
            {
                windowManager[ViewerWindowType.TrainListWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplayEOTListWindow, KeyEventType.KeyPressed, () =>
            {
                windowManager[ViewerWindowType.EndOfTrainDeviceWindow].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplayStationLabels, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ViewerWindowType.LocationsOverlay].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.DisplayCarLabels, KeyEventType.KeyPressed, (UserCommandArgs userCommandArgs) =>
            {
                if (userCommandArgs is not ModifiableKeyCommandArgs)
                    windowManager[ViewerWindowType.CarIdentifierOverlay].ToggleVisibility();
            });
            UserCommandController.AddEvent(UserCommand.GameChangeCab, KeyEventType.KeyPressed, () =>
            {
                if (PlayerLocomotive.ThrottlePercent >= 1 || PlayerLocomotive.AbsSpeedMpS > 1 || !IsReverserInNeutral(PlayerLocomotive))
                {
                    Simulator.Confirmer.Warning(CabControl.ChangeCab, CabSetting.Warn2);
                }
                else
                {
                    _ = new ChangeCabCommand(Log);
                }
            });
            UserCommandController.AddEvent(UserCommand.CameraReset, KeyEventType.KeyPressed, Camera.Reset);
            UserCommandController.AddEvent(UserCommand.CameraCab, KeyEventType.KeyPressed, () =>
            {
                if (CabCamera.IsAvailable || ThreeDimCabCamera.IsAvailable)
                {
                    _ = new UseCabCameraCommand(Log);
                }
                else
                {
                    Simulator.Confirmer.Warning(Catalog.GetString("Cab view not available"));
                }
            });
            UserCommandController.AddEvent(UserCommand.CameraToggleThreeDimensionalCab, KeyEventType.KeyPressed, () =>
            {
                if (!CabCamera.IsAvailable)
                {
                    Simulator.Confirmer.Warning(Catalog.GetString("This car doesn't have a 2D cab"));
                }
                else if (!ThreeDimCabCamera.IsAvailable)
                {
                    Simulator.Confirmer.Warning(Catalog.GetString("This car doesn't have a 3D cab"));
                }
                else
                {
                    _ = new ToggleThreeDimensionalCabCameraCommand(Log);
                }
            });
            UserCommandController.AddEvent(UserCommand.CameraOutsideFront, KeyEventType.KeyPressed, () =>
            {
                CheckReplaying();
                _ = new UseFrontCameraCommand(Log);
            });
            UserCommandController.AddEvent(UserCommand.CameraOutsideRear, KeyEventType.KeyPressed, () =>
            {
                CheckReplaying();
                _ = new UseBackCameraCommand(Log);
            });
            UserCommandController.AddEvent(UserCommand.CameraJumpingTrains, KeyEventType.KeyPressed, RandomSelectTrain);
            UserCommandController.AddEvent(UserCommand.CameraVibrate, KeyEventType.KeyPressed, () =>
            {
                Settings.CarVibratingLevel = (Settings.CarVibratingLevel + 1) % 4;
                Simulator.Confirmer.Message(ConfirmLevel.Information, Catalog.GetString($"Vibrating at level {Settings.CarVibratingLevel}"));
                Settings.Save("CarVibratingLevel");
            });
            UserCommandController.AddEvent(UserCommand.DebugToggleConfirmations, KeyEventType.KeyPressed, () =>
            {
                Simulator.Settings.SuppressConfirmations = !Simulator.Settings.SuppressConfirmations;
                Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Settings.SuppressConfirmations ? Catalog.GetString("Confirmations suppressed") : Catalog.GetString("Confirmations visible"));
                Simulator.Settings.Save();
            });
            UserCommandController.AddEvent(UserCommand.CameraJumpBackPlayer, KeyEventType.KeyPressed, () =>
            {
                SelectedTrain = PlayerTrain;
                CameraActivate();
            });
            UserCommandController.AddEvent(UserCommand.CameraTrackside, KeyEventType.KeyPressed, () =>
            {
                CheckReplaying();
                _ = new UseTracksideCameraCommand(Log);
            });
            UserCommandController.AddEvent(UserCommand.CameraSpecialTracksidePoint, KeyEventType.KeyPressed, () =>
            {
                CheckReplaying();
                _ = new UseSpecialTracksideCameraCommand(Log);
            });
            UserCommandController.AddEvent(UserCommand.CameraPassenger, KeyEventType.KeyPressed, () =>
            {
                if (PassengerCamera.IsAvailable)
                {
                    CheckReplaying();
                    _ = new UsePassengerCameraCommand(Log);
                }
            });
            UserCommandController.AddEvent(UserCommand.CameraBrakeman, KeyEventType.KeyPressed, () =>
            {
                CheckReplaying();
                _ = new UseBrakemanCameraCommand(Log);
            });
            UserCommandController.AddEvent(UserCommand.CameraFree, KeyEventType.KeyPressed, () =>
            {
                CheckReplaying();
                _ = new UseFreeRoamCameraCommand(Log);
                Simulator.Confirmer.Message(ConfirmLevel.None, Catalog.GetPluralString(
                    "{0} viewpoint stored. Use Shift+8 to restore viewpoints.", "{0} viewpoints stored. Use Shift+8 to restore viewpoints.", FreeRoamCameraList.Count - 1));
            });
            UserCommandController.AddEvent(UserCommand.CameraPreviousFree, KeyEventType.KeyPressed, () =>
            {
                if (FreeRoamCameraList.Count > 0)
                {
                    CheckReplaying();
                    _ = new UsePreviousFreeRoamCameraCommand(Log);
                }
            });
            UserCommandController.AddEvent(UserCommand.CameraHeadOutForward, KeyEventType.KeyPressed, () =>
            {
                if (HeadOutForwardCamera.IsAvailable)
                {
                    CheckReplaying();
                    _ = new UseHeadOutForwardCameraCommand(Log);
                }
            });
            UserCommandController.AddEvent(UserCommand.CameraHeadOutBackward, KeyEventType.KeyPressed, () =>
            {
                if (HeadOutBackCamera.IsAvailable)
                {
                    CheckReplaying();
                    _ = new UseHeadOutBackCameraCommand(Log);
                }
            });
            UserCommandController.AddEvent(UserCommand.GameExternalCabController, KeyEventType.KeyPressed, () => UserCommandController.Send(CommandControllerInput.Activate));
            UserCommandController.AddEvent(UserCommand.GameSwitchAhead, KeyEventType.KeyPressed, () =>
            {
                if (PlayerTrain.ControlMode == TrainControlMode.Manual || PlayerTrain.ControlMode == TrainControlMode.Explorer)
                    _ = new ToggleSwitchAheadCommand(Log);
                else
                    Simulator.Confirmer.Warning(CabControl.SwitchAhead, CabSetting.Warn1);
            });
            UserCommandController.AddEvent(UserCommand.GameSwitchBehind, KeyEventType.KeyPressed, () =>
            {
                if (PlayerTrain.ControlMode == TrainControlMode.Manual || PlayerTrain.ControlMode == TrainControlMode.Explorer)
                    _ = new ToggleSwitchBehindCommand(Log);
                else
                    Simulator.Confirmer.Warning(CabControl.SwitchBehind, CabSetting.Warn1);
            });
            UserCommandController.AddEvent(UserCommand.GameClearSignalForward, KeyEventType.KeyPressed, () => PlayerTrain.RequestSignalPermission(Direction.Forward));
            UserCommandController.AddEvent(UserCommand.GameClearSignalBackward, KeyEventType.KeyPressed, () => PlayerTrain.RequestSignalPermission(Direction.Backward));
            UserCommandController.AddEvent(UserCommand.GameResetSignalForward, KeyEventType.KeyPressed, () => PlayerTrain.RequestResetSignal(Direction.Forward));
            UserCommandController.AddEvent(UserCommand.GameResetSignalBackward, KeyEventType.KeyPressed, () => PlayerTrain.RequestResetSignal(Direction.Backward));
            UserCommandController.AddEvent(UserCommand.GameSwitchManualMode, KeyEventType.KeyPressed, PlayerTrain.RequestToggleManualMode);
            UserCommandController.AddEvent(UserCommand.GameResetOutOfControlMode, KeyEventType.KeyPressed, () => _ = new ResetOutOfControlModeCommand(Log));

            UserCommandController.AddEvent(UserCommand.GameMultiPlayerDispatcher, KeyEventType.KeyPressed, ToggleDispatcherView);
            UserCommandController.AddEvent(UserCommand.DebugSoundForm, KeyEventType.KeyPressed, () => SoundDebugFormEnabled = !SoundDebugFormEnabled);
            UserCommandController.AddEvent(UserCommand.CameraJumpSeeSwitch, KeyEventType.KeyPressed, () =>
            {
                //TODO 20220322 select items in new dispatcher 
                //if (Program.DebugViewer != null && Program.DebugViewer.Enabled && (Program.DebugViewer.SwitchPickedItem != null || Program.DebugViewer.SignalPickedItem != null))
                //{
                //    WorldLocation location = Program.DebugViewer.SwitchPickedItem?.Item != null ? Program.DebugViewer.SwitchPickedItem.Item.UiD.Location.ChangeElevation(8) : Program.DebugViewer.SignalPickedItem.Item.Location.ChangeElevation(8);
                //    if (FreeRoamCameraList.Count == 0)
                //        _ = new UseFreeRoamCameraCommand(Log);
                //    FreeRoamCamera.SetLocation(location);
                //    //FreeRoamCamera
                //    FreeRoamCamera.Activate();
                //}
            });
            UserCommandController.AddEvent(UserCommand.DebugDumpKeymap, KeyEventType.KeyPressed, () =>
            {
                //TODO 20210320 move path settings to RuntimeInfo
                string textPath = Path.Combine(Settings.LoggingPath, "OpenRailsKeyboard.txt");
                Settings.Input.DumpToText(textPath);
                Simulator.Confirmer.PlainTextMessage(ConfirmLevel.Message, Catalog.GetString("Keyboard map list saved to '{0}'.", textPath), 10);

                string graphicPath = Path.Combine(Settings.LoggingPath, "OpenRailsKeyboard.png");
                KeyboardMap.DumpToGraphic(Settings.Input, graphicPath);
                Simulator.Confirmer.PlainTextMessage(ConfirmLevel.Message, Catalog.GetString("Keyboard map image saved to '{0}'.", graphicPath), 10);
            });

            UserCommandController.AddEvent(UserCommand.ControlOdoMeterDisplayMode, KeyEventType.KeyPressed, () =>
            {
                Settings.OdometerShortDistanceMode = !Settings.OdometerShortDistanceMode;
                Simulator.Confirmer.Confirm(CabControl.Odometer, Settings.OdometerShortDistanceMode ? "Short distance Measure mode" : "Long distance mode");
            });

            // Turntable commands
            if (Simulator.MovingTables.Any())
            {
                UserCommandController.AddEvent(UserCommand.ControlTurntableClockwise, KeyEventType.KeyPressed, () =>
                {
                    Simulator.ActiveMovingTable = FindActiveMovingTable();
                    if (Simulator.ActiveMovingTable != null)
                    {
                        TurntableClockwiseCommand.Receiver = Simulator.ActiveMovingTable;
                        _ = new TurntableClockwiseCommand(Log);
                    }
                });
                UserCommandController.AddEvent(UserCommand.ControlTurntableClockwise, KeyEventType.KeyReleased, () =>
                {
                    if (Simulator.ActiveMovingTable != null)
                    {
                        TurntableClockwiseTargetCommand.Receiver = Simulator.ActiveMovingTable;
                        _ = new TurntableClockwiseTargetCommand(Log);
                    }
                });
                UserCommandController.AddEvent(UserCommand.ControlTurntableCounterclockwise, KeyEventType.KeyPressed, () =>
                {
                    Simulator.ActiveMovingTable = FindActiveMovingTable();
                    if (Simulator.ActiveMovingTable != null)
                    {
                        TurntableCounterclockwiseCommand.Receiver = Simulator.ActiveMovingTable;
                        _ = new TurntableCounterclockwiseCommand(Log);
                    }
                });
                UserCommandController.AddEvent(UserCommand.ControlTurntableCounterclockwise, KeyEventType.KeyReleased, () =>
                {
                    if (Simulator.ActiveMovingTable != null)
                    {
                        TurntableCounterclockwiseTargetCommand.Receiver = Simulator.ActiveMovingTable;
                        _ = new TurntableCounterclockwiseTargetCommand(Log);
                    }
                });
            }
            UserCommandController.AddEvent(UserCommand.GameAutopilotMode, KeyEventType.KeyPressed, () =>
            {
                switch (PlayerLocomotive.Train.TrainType)
                {
                    case TrainType.AiPlayerHosting:
                        if (((AITrain)PlayerLocomotive.Train).SwitchToPlayerControl())
                        {
                            Simulator.Confirmer.Message(ConfirmLevel.Information, Catalog.GetString("Switched to player control"));
                            ActivityEvaluation.Instance.StopAutoPilotTime();
                        }
                        break;
                    case TrainType.AiPlayerDriven:
                        if (PlayerLocomotive.Train.ControlMode == TrainControlMode.Manual)
                            Simulator.Confirmer.Message(ConfirmLevel.Warning, Catalog.GetString("You can't switch from manual to autopilot mode"));
                        else
                        {
                            if (((AITrain)PlayerLocomotive.Train).SwitchToAutopilotControl())
                            {
                                Simulator.Confirmer.Message(ConfirmLevel.Information, Catalog.GetString("Switched to autopilot"));
                                ActivityEvaluation.Instance.StartAutoPilotTime();
                            }
                        }
                        break;
                }
            });
            UserCommandController.AddEvent(UserCommand.GameScreenshot, KeyEventType.KeyPressed, () =>
            {
                if (Visibility == VisibilityState.Visible) // Ensure we only get one screenshot.
                    _ = new SaveScreenshotCommand(Log);
            });
            #endregion
            if (MultiPlayerManager.IsMultiPlayer())
            {
                //get key strokes and determine if some messages should be sent
                MultiPlayerViewer.RegisterInputEvents(this);
            }

            UserCommandController.AddEvent(UserCommand.DebugLockShadows, KeyEventType.KeyPressed, () => lockShadows = !lockShadows);
            UserCommandController.AddEvent(UserCommand.DebugLogRenderFrame, KeyEventType.KeyPressed, () => logRenderFrame = true);
            UserCommandController.AddEvent(UserCommand.GameUncoupleWithMouse, KeyEventType.KeyDown, () => { uncoupleWithMouseActive = true; forceMouseVisible = true; });
            UserCommandController.AddEvent(UserCommand.GameUncoupleWithMouse, KeyEventType.KeyReleased, () => { uncoupleWithMouseActive = false; forceMouseVisible = false; });

            UserCommandController.AddEvent(CommonUserCommand.PointerDown, (UserCommandArgs userCommandArgs, GameTime gameTime, KeyModifiers modifiers) =>
            {
                PointerCommandArgs pointerCommandArgs = userCommandArgs as PointerCommandArgs;
                Vector3 nearsource = new Vector3(pointerCommandArgs.Position.X, pointerCommandArgs.Position.Y, 0f);
                Vector3 farsource = new Vector3(pointerCommandArgs.Position.X, pointerCommandArgs.Position.Y, 1f);
                Matrix world = Matrix.CreateTranslation(0, 0, 0);
                Vector3 nearPoint = DefaultViewport.Unproject(nearsource, Camera.XnaProjection, Camera.XnaView, world);
                Vector3 farPoint = DefaultViewport.Unproject(farsource, Camera.XnaProjection, Camera.XnaView, world);
                forceMouseVisible = true;
                if (!Simulator.GamePaused)
                {
                    if (uncoupleWithMouseActive)
                    {
                        TryUncoupleAt(nearPoint, farPoint);
                    }
                    if (modifiers.HasFlag(Settings.Input.GameSwitchWithMouseModifier))
                    {
                        TryThrowSwitchAt(nearPoint, farPoint);
                    }
                }
            });
            UserCommandController.AddEvent(CommonUserCommand.PointerReleased, (UserCommandArgs, GameTime) =>
            {
                forceMouseVisible = false;
            });
            UserCommandController.AddEvent(CommonUserCommand.PointerMoved, (UserCommandArgs, GameTime) =>
            {
                mouseVisibleTillRealTime = RealTime + 1;
            });
            SetCommandReceivers();
            InitReplay();

            //only add here at the end, so they do not fire during load process already
            Game.Components.Add(keyboardInputGameComponent);
            Game.Components.Add(mouseInputGameComponent);
            Game.Components.Add(railDriverInputGameComponent);

            foreach (ViewerWindowType windowType in EnumExtension.GetValues<ViewerWindowType>())
            {
                if (Settings.PopupStatus[windowType] &&
                    (windowType is not ViewerWindowType.QuitWindow
                    and not ViewerWindowType.ActivityWindow
                    and not ViewerWindowType.PauseOverlay
                    and not ViewerWindowType.CarOperationsWindow
                    and not ViewerWindowType.MultiPlayerMessagingWindow))
                    windowManager[windowType].Open();
            }
            windowManager[ViewerWindowType.NotificationOverlay].Open();
            if (Simulator.ActivityRun != null)
                Simulator.ActivityRun.OnEventTriggered += ActivityRun_OnEventTriggered;
        }

        private void WindowManager_OnModalWindow(object sender, Graphics.Window.ModalWindowEventArgs e)
        {
            forceMouseVisible = e.ModalWindowOpen;
        }

        private void ToggleDispatcherView()
        {
            DebugViewerEnabled = !DebugViewerEnabled;
            if (null == dispatcherThread)
            {
                dispatcherThread = new Thread(StartDispatcherViewThread);
                dispatcherThread.Start();
            }
            else
            {
                dispatcherWindow?.BringToFront();
            }
        }

        private void StartDispatcherViewThread()
        {
            using (Dispatcher.DispatcherWindow dispatcherWindow = new Dispatcher.DispatcherWindow(Settings))
            {
                this.dispatcherWindow = dispatcherWindow;
                dispatcherWindow.Run();
            }
            dispatcherThread = null;
            dispatcherWindow = null;
        }

        /// <summary>
        /// Each Command needs to know its Receiver so it can call a method of the Receiver to action the command.
        /// The Receiver is a static property as all commands of the same class share the same Receiver
        /// and it needs to be set before the command is used.
        /// </summary>
        public void SetCommandReceivers()
        {
            ReverserCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            NotchedThrottleCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ContinuousThrottleCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            TrainBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            EngineBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            BrakemanBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            DynamicBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            InitializeBrakesCommand.Receiver = PlayerLocomotive.Train;
            ResetOutOfControlModeCommand.Receiver = PlayerLocomotive.Train;
            EmergencyPushButtonCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            HandbrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            BailOffCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            QuickReleaseCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            BrakeOverchargeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            RetainersCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            BrakeHoseConnectCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleWaterScoopCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;

            if (PlayerLocomotive is MSTSSteamLocomotive)
            {
                ContinuousReverserCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousInjectorCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousSmallEjectorCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousLargeEjectorCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ToggleInjectorCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ToggleBlowdownValveCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousBlowerCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousDamperCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousFiringRateCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ToggleManualFiringCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ToggleCylinderCocksCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ToggleCylinderCompoundCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                FireShovelfullCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                AIFireOnCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                AIFireOffCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                AIFireResetCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
            }

            PantographCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            if (PlayerLocomotive is MSTSElectricLocomotive)
            {
                CircuitBreakerClosingOrderCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                CircuitBreakerClosingOrderButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                CircuitBreakerOpeningOrderButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                CircuitBreakerClosingAuthorizationCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
            }

            if (PlayerLocomotive is MSTSDieselLocomotive)
            {
                TractionCutOffRelayClosingOrderCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                TractionCutOffRelayClosingOrderButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                TractionCutOffRelayOpeningOrderButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                TractionCutOffRelayClosingAuthorizationCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                TogglePlayerEngineCommand.Receiver = (MSTSDieselLocomotive)PlayerLocomotive;
                VacuumExhausterCommand.Receiver = (MSTSDieselLocomotive)PlayerLocomotive;
            }

            ImmediateRefillCommand.Receiver = (MSTSLocomotiveViewer)PlayerLocomotiveViewer;
            RefillCommand.Receiver = (MSTSLocomotiveViewer)PlayerLocomotiveViewer;
            SelectScreenCommand.Receiver = this;
            ResetOdometerCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleOdometerDirectionCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            SanderCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            AlerterCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            HornCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            BellCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleCabLightCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            WipersCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            HeadlightCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ChangeCabCommand.Receiver = this;
            ToggleDoorsLeftCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleDoorsRightCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleMirrorsCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            CabRadioCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleSwitchAheadCommand.Receiver = this;
            ToggleSwitchBehindCommand.Receiver = this;
            ToggleAnySwitchCommand.Receiver = this;
            UncoupleCommand.Receiver = this;
            SaveScreenshotCommand.Receiver = this;
            ActivityCommand.Receiver = windowManager[ViewerWindowType.ActivityWindow] as PopupWindows.ActivityWindow;
            UseCameraCommand.Receiver = this;
            MoveCameraCommand.Receiver = this;
            ToggleHelpersEngineCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            BatterySwitchCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
            BatterySwitchCloseButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
            BatterySwitchOpenButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
            ToggleMasterKeyCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
            ServiceRetentionButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
            ServiceRetentionCancellationButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
            ElectricTrainSupplyCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
            TCSButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).TrainControlSystem;
            TCSSwitchCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).TrainControlSystem;
            ToggleGenericItem1Command.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleGenericItem2Command.Receiver = (MSTSLocomotive)PlayerLocomotive;

            //Distributed power
            DistributedPowerMoveToFrontCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            DistributedPowerMoveToBackCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            DistributedPowerTractionCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            DistributedPowerIdleCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            DistributedPowerDynamicBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            DistributedPowerIncreaseCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            DistributedPowerDecreaseCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;

            //EOT
            EOTCommTestCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            EOTDisarmCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            EOTArmTwoWayCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            EOTEmergencyBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleEOTEmergencyBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            EOTMountCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
        }

        public void ChangeToPreviousFreeRoamCamera()
        {
            if (Camera == FreeRoamCamera)
            {
                // If 8 is the current camera, rotate the list and then activate a different camera.
                RotateFreeRoamCameraList();
                FreeRoamCamera.Activate();
            }
            else
            {
                FreeRoamCamera.Activate();
                RotateFreeRoamCameraList();
            }
        }

        private void RotateFreeRoamCameraList()
        {
            // Rotate list moving 1 to 0 etc. (by adding 0 to end, then removing 0)
            FreeRoamCameraList.Add(FreeRoamCamera);
            FreeRoamCameraList.RemoveAt(0);
        }

        public void ChangeSelectedTrain(Train selectedTrain)
        {
            SelectedTrain = selectedTrain;
        }

        public void AdjustCabHeight(int windowWidth, int windowHeight)
        {
            CabTextureInverseRatio = 0.75f; // start setting it to default
            // MSTS cab views are designed for 4:3 aspect ratio. This is the default. However a check is done with the actual
            // cabview texture. If this has a different aspect ratio, that one is considered
            // For wider screens (e.g. 16:9), the height of the cab view before adjustment exceeds the height of the display.
            // The user can decide how much of this excess to keep. Setting of 0 keeps all the excess and 100 keeps none.

            // <CSComment> If the aspect ratio of the viewing window is greater than the aspect ratio of the cabview texture file
            // it is either possible to stretch the cabview texture file or to leave the proportions unaltered and to vertically pan
            // the screen
            if (CabCamera.IsAvailable)
            {
                var i = ((PlayerLocomotive as MSTSLocomotive).UsingRearCab) ? 1 : 0;
                var cabTextureFileName = (PlayerLocomotive as MSTSLocomotive).CabViewList[i].CVFFile.Views2D[0];
                var cabTextureInverseRatio = ComputeCabTextureInverseRatio(cabTextureFileName);
                if (cabTextureInverseRatio != -1)
                    CabTextureInverseRatio = cabTextureInverseRatio;
            }
            int unstretchedCabHeightPixels = (int)(CabTextureInverseRatio * windowWidth);
            int unstretchedCabWidthPixels = (int)(windowHeight / CabTextureInverseRatio);
            float windowInverseRatio = (float)windowHeight / windowWidth;
            if (Settings.Letterbox2DCab)
            {
                CabWidthPixels = Math.Min((int)Math.Round(windowHeight / CabTextureInverseRatio), windowWidth);
                CabHeightPixels = Math.Min((int)Math.Round(windowWidth * CabTextureInverseRatio), windowHeight);
                CabXLetterboxPixels = (windowWidth - CabWidthPixels) / 2;
                CabYLetterboxPixels = (windowHeight - CabHeightPixels) / 2;
                CabExceedsDisplay = CabExceedsDisplayHorizontally = CabXOffsetPixels = CabYOffsetPixels = 0;
            }
            else
            {
                if (windowInverseRatio == CabTextureInverseRatio)
                {
                    // nice, window aspect ratio and cabview aspect ratio are identical
                    CabExceedsDisplay = 0;
                    CabExceedsDisplayHorizontally = 0;
                }
                else if (windowInverseRatio < CabTextureInverseRatio)
                {
                    // screen is wide-screen, so can choose between vertical scroll or horizontal stretch
                    CabExceedsDisplay = (int)((unstretchedCabHeightPixels - windowHeight) * ((100 - Settings.Cab2DStretch) / 100f));
                    CabExceedsDisplayHorizontally = 0;
                }
                else
                {
                    // must scroll horizontally
                    CabExceedsDisplay = 0;
                    CabExceedsDisplayHorizontally = unstretchedCabWidthPixels - windowWidth;
                }
                CabHeightPixels = windowHeight + CabExceedsDisplay;
                CabYOffsetPixels = -CabExceedsDisplay / 2; // Initial value is halfway. User can adjust with arrow keys.
                CabWidthPixels = windowWidth + CabExceedsDisplayHorizontally;
                CabXOffsetPixels = CabExceedsDisplayHorizontally / 2;
                CabXLetterboxPixels = CabYLetterboxPixels = 0;
            }
            if (CabCamera.IsAvailable)
                CabCamera.Initialize();
        }

        public float ComputeCabTextureInverseRatio(string cabTextureFileName)
        {
            float cabTextureInverseRatio = -1;
            bool _isNightTexture;
            var cabTexture = CABTextureManager.GetTexture(cabTextureFileName, false, false, out _isNightTexture, false);
            if (cabTexture != SharedMaterialManager.MissingTexture)
            {
                cabTextureInverseRatio = (float)cabTexture.Height / cabTexture.Width;
                // if square cab texture files with dimension of at least 1024 pixels are used, they are considered as stretched 4 : 3 ones
                if (cabTextureInverseRatio == 1 && cabTexture.Width >= 1024)
                    cabTextureInverseRatio = 0.75f;
            }
            return cabTextureInverseRatio;
        }

        public void Load()
        {
            World.Load();
            TextManager.Load(Game.GraphicsDevice);
        }

        public void Update(RenderFrame frame, double elapsedRealTime)
        {
            RealTime += elapsedRealTime;
            var elapsedTime = new ElapsedTime(Simulator.GetElapsedClockSeconds(elapsedRealTime), elapsedRealTime);

            HandleUserInput(elapsedTime);
            // We need to do it also here, because passing from manual to auto a ReverseFormation may be needed
            if (Camera is TrackingCamera && Camera.AttachedCar != null && Camera.AttachedCar.Train != null && Camera.AttachedCar.Train.FormationReversed)
            {
                Camera.AttachedCar.Train.FormationReversed = false;
                (Camera as TrackingCamera).SwapCameras();
            }
            Simulator.Update(elapsedTime.ClockSeconds);
            if (PlayerLocomotive.Train.BrakingTime == -2) // We just had a wagon with stuck brakes
            {
                LoadDefectCarSound(PlayerLocomotive.Train.Cars[-(int)PlayerLocomotive.Train.ContinuousBrakingTime], "BrakesStuck.sms");
            }

            if (MultiPlayerManager.IsMultiPlayer())
            {
                ////get key strokes and determine if some messages should be sent
                //MultiPlayerViewer.HandleUserInput();
                MultiPlayerManager.Instance().Update(Simulator.GameTime);
            }

            UserCommandController.Send(CommandControllerInput.Speed, Speed.MeterPerSecond.FromMpS(PlayerLocomotive.SpeedMpS, Simulator.Instance.MetricUnits));

            // This has to be done also for stopped trains
            var cars = World.Trains.Cars;
            foreach (var car in cars)
                car.Value.UpdateSoundPosition();

            if (Simulator.ReplayCommandList != null)
            {
                Log.Update(Simulator.ReplayCommandList);

                if (Log.PauseState == ReplayPauseState.Due)
                {
                    if (Simulator.Settings.ReplayPauseBeforeEnd)
                    {
                        // Reveal Quit Menu
                        //QuitWindow.Visible = Simulator.GamePaused = !QuitWindow.Visible;
                        Pause(true);
                        Log.PauseState = ReplayPauseState.During;
                    }
                    else
                    {
                        Log.PauseState = ReplayPauseState.Done;
                    }
                }
            }
            if (Log.ReplayComplete)
            {
                Simulator.Confirmer.PlainTextMessage(ConfirmLevel.Message, "Replay complete", 2);
                Log.ReplayComplete = false;
            }

            World.Update(elapsedTime);

            if (frame.IsScreenChanged)
                Camera.ScreenChanged();

            // Check if you need to swap camera
            if (Camera is TrackingCamera && Camera.AttachedCar != null && Camera.AttachedCar.Train != null && Camera.AttachedCar.Train.FormationReversed)
            {
                Camera.AttachedCar.Train.FormationReversed = false;
                (Camera as TrackingCamera).SwapCameras();
            }

            // Update camera first...
            Camera.Update(elapsedTime);
            // No above camera means we're allowed to auto-switch to cab view.
            if ((AbovegroundCamera == null) && Camera.IsUnderground)
            {
                AbovegroundCamera = Camera;
                bool ViewingPlayer = true;

                if (Camera.AttachedCar != null)
                    ViewingPlayer = Camera.AttachedCar.Train == Simulator.PlayerLocomotive.Train;

                if ((Simulator.PlayerLocomotive.HasFront3DCab || Simulator.PlayerLocomotive.HasRear3DCab && ViewingPlayer) && !(Camera is CabCamera))
                {
                    ThreeDimCabCamera.Activate();
                }
                else if ((Simulator.PlayerLocomotive.HasFrontCab || Simulator.PlayerLocomotive.HasRearCab) && ViewingPlayer)
                {
                    CabCamera.Activate();
                }
                else
                {
                    Simulator.Confirmer.Warning(Viewer.Catalog.GetString("Cab view not available"));
                }
            }
            else if (AbovegroundCamera != null
                && Camera.AttachedCar != null
                && Camera.AttachedCar.Train == Simulator.PlayerLocomotive.Train)
            {
                // The AbovegroundCamera.Update() has been creating an odd sound issue when the locomotive is in the tunnel.
                // Allowing the update to take place when only in cab view solved the issue.
                if (Camera == CabCamera || Camera == ThreeDimCabCamera)
                    AbovegroundCamera.Update(elapsedTime);
                if (!AbovegroundCamera.IsUnderground)
                {
                    // But only if the user hasn't selected another camera!
                    if (Camera == CabCamera || Camera == ThreeDimCabCamera)
                        AbovegroundCamera.Activate();
                    AbovegroundCamera = null;
                }
            }

            Simulator.ActiveMovingTable = FindActiveMovingTable();

            frame.PrepareFrame(this);
            Camera.PrepareFrame(frame, elapsedTime);
            frame.PrepareFrame(elapsedTime, lockShadows, logRenderFrame);
            World.PrepareFrame(frame, elapsedTime);
            InfoDisplay.PrepareFrame(frame, elapsedTime);

            logRenderFrame = false;
            if (pauseWindow != (pauseWindow = Simulator.GamePaused))
            {
                windowManager[ViewerWindowType.PauseOverlay].Open();
            }

            // Camera notices
            if (currentCamera == (currentCamera = Camera) && currentCamera != null)
            {
                if (fieldOfView != (fieldOfView = Camera.FieldOfView))
                    (windowManager[ViewerWindowType.NotificationOverlay] as NotificationOverlay).AddNotice(Catalog.GetString($"FOV: {fieldOfView:F0}°"));
            }
            else
                fieldOfView = Camera?.FieldOfView ?? 0;
        }

        private void LoadDefectCarSound(TrainCar car, string filename)
        {
            var smsFilePath = Simulator.RouteFolder.ContentFolder.SoundFile(filename);
            if (!File.Exists(smsFilePath))
            {
                Trace.TraceWarning("Cannot find defect car sound file {0}", filename);
                return;
            }

            try
            {
                SoundProcess.AddSoundSource(this, new SoundSource(car as MSTSWagon, World.Trains.GetViewer(car), smsFilePath));
            }
            catch (Exception error)
            {
                Trace.WriteLine(new FileLoadException(smsFilePath, error));
            }
        }

        private void HandleUserInput(in ElapsedTime elapsedTime)
        {
            if (PlayerLocomotiveViewer != null)
                PlayerLocomotiveViewer.HandleUserInput(elapsedTime);

            //in the dispatcher window, when one clicks a train and "See in Game", will jump to see that train
            if (Program.DebugViewer != null && Program.DebugViewer.ClickedTrain == true)
            {
                Program.DebugViewer.ClickedTrain = false;
                if (SelectedTrain != Program.DebugViewer.PickedTrain)
                {
                    SelectedTrain = Program.DebugViewer.PickedTrain;
                    Simulator.AI.TrainListChanged = true;

                    if (SelectedTrain.Cars == null || SelectedTrain.Cars.Count == 0)
                        SelectedTrain = PlayerTrain;

                    CameraActivate();
                }
            }

            //in TrainSwitcher, when one clicks a train, Viewer will jump to see that train
            if (Simulator.TrainSwitcher.ClickedTrainFromList == true)
            {
                Simulator.TrainSwitcher.ClickedTrainFromList = false;
                if (SelectedTrain != Simulator.TrainSwitcher.PickedTrainFromList && SelectedTrain.Cars != null || SelectedTrain.Cars.Count != 0)
                {
                    SelectedTrain = Simulator.TrainSwitcher.PickedTrainFromList;
                    Simulator.AI.TrainListChanged = true;

                    CameraActivate();
                }
            }

            // reset cursor type when needed
            if (!(Camera is CabCamera) && !(Camera is CabCamera3D) && actualCursor != Cursors.Default)
                actualCursor = Cursors.Default;

            RenderProcess.IsMouseVisible = forceMouseVisible || RealTime < mouseVisibleTillRealTime;
        }

        private static bool IsReverserInNeutral(TrainCar car)
        {
            // Diesel and electric locos have a Reverser lever and,
            // in the neutral position, direction == N
            return car.Direction == MidpointDirection.N
            // Steam locos never have direction == N, so check for setting close to zero.
            || Math.Abs(car.Train.MUReverserPercent) <= 1;
        }
        /// <summary>
        /// If the player changes the camera during replay, then further replay of the camera is suspended.
        /// The player's camera commands will be recorded instead of the replay camera commands.
        /// Replay and recording of non-camera commands such as controls continues.
        /// </summary>
        public void CheckReplaying()
        {
            if (Simulator.IsReplaying)
            {
                if (!Log.CameraReplaySuspended)
                {
                    Log.CameraReplaySuspended = true;
                    SuspendedCamera = Camera;
                    Simulator.Confirmer.Confirm(CabControl.Replay, CabSetting.Warn1);
                }
            }
        }

        /// <summary>
        /// Replay of the camera is not resumed until the player opens the Quit Menu and then presses Esc to unpause the simulator.
        /// </summary>
        public void ResumeReplaying()
        {
            Simulator.GamePaused = false;
            if (Log.PauseState == ReplayPauseState.During)
                Log.PauseState = ReplayPauseState.Done;

            Log.CameraReplaySuspended = false;
            if (SuspendedCamera != null)
                SuspendedCamera.Activate();
        }

        public void Pause(bool pause)
        {
            if (pause)
            {
                Simulator.GamePaused = true;
                _ = windowManager[ViewerWindowType.QuitWindow].Open();
            }
            else
            {
                Simulator.GamePaused = false;
                _ = windowManager[ViewerWindowType.QuitWindow].Close();
            }
        }

        public void ChangeCab()
        {
            if (!Simulator.PlayerLocomotive.Train.IsChangeCabAvailable())
                return;

            Simulator.PlayerLocomotive = Simulator.PlayerLocomotive.Train.GetNextCab();
            PlayerLocomotiveViewer = World.Trains.GetViewer(Simulator.PlayerLocomotive);
            if (PlayerLocomotiveViewer is MSTSLocomotiveViewer && (PlayerLocomotiveViewer as MSTSLocomotiveViewer).HasCabRenderer)
                AdjustCabHeight(DisplaySize.X, DisplaySize.Y);

            ThreeDimCabCamera.ChangeCab(Simulator.PlayerLocomotive);
            HeadOutForwardCamera.ChangeCab(Simulator.PlayerLocomotive);
            HeadOutBackCamera.ChangeCab(Simulator.PlayerLocomotive);

            if (!Simulator.PlayerLocomotive.HasFront3DCab && !Simulator.PlayerLocomotive.HasRear3DCab)
                CabCamera.Activate(); // If you need anything else here the cameras should check for it.
            else
                ThreeDimCabCamera.Activate();
            SetCommandReceivers();
            if (MultiPlayerManager.IsMultiPlayer())
                MultiPlayerManager.LocoChange(Simulator.PlayerLocomotive.Train, Simulator.PlayerLocomotive);
            Simulator.Confirmer.Confirm(CabControl.ChangeCab, CabSetting.On);
        }

        /// <summary>
        /// Called when switching player train
        /// </summary>
        private void PlayerLocomotiveChanged(object sender, EventArgs e)
        {
            PlayerLocomotiveViewer = World.Trains.GetViewer(Simulator.PlayerLocomotive);
            CabCamera.Activate(); // If you need anything else here the cameras should check for it.
            SetCommandReceivers();
            ThreeDimCabCamera.ChangeCab(Simulator.PlayerLocomotive);
            HeadOutForwardCamera.ChangeCab(Simulator.PlayerLocomotive);
            HeadOutBackCamera.ChangeCab(Simulator.PlayerLocomotive);
        }

        // change reference to player train when switching train in Timetable mode
        private void PlayerTrainChanged(object sender, PlayerTrainChangedEventArgs e)
        {
            if (SelectedTrain == e.PreviousTrain)
            {
                SelectedTrain = e.CurrentTrain;
            }
        }

        // display window for Timetable Player train detach actions
        private void RequestTimetableDetachWindow(object sender, EventArgs e)
        {
            windowManager[ViewerWindowType.DetachTimetableTrainWindow].Open();
        }

        // Finds the Turntable or Transfertable nearest to the viewing point
        private MovingTable FindActiveMovingTable()
        {
            MovingTable activeMovingTable = null;
            float minDistanceSquared = 1000_000f;
            foreach (MovingTable movingTable in Simulator.MovingTables)
            {
                if (movingTable.WorldPosition.XNAMatrix.M44 != 100_000_000)
                {
                    float distanceSquared = (float)WorldLocation.GetDistanceSquared(movingTable.WorldPosition.WorldLocation, Camera.CameraWorldLocation);
                    if (distanceSquared <= minDistanceSquared && distanceSquared < 160_000) //must be the nearest one, but must also be near!
                    {
                        minDistanceSquared = distanceSquared;
                        activeMovingTable = movingTable;
                    }
                }
            }
            return activeMovingTable;
        }

        internal void Terminate()
        {
            SaveSettings();
            dispatcherWindow?.Close();
            InfoDisplay.Terminate();
        }

        private int trainCount;

        private void RandomSelectTrain()
        {
            try
            {
                SortedList<double, Train> users = new SortedList<double, Train>();
                foreach (var t in Simulator.Trains)
                {
                    if (t == null || t.Cars == null || t.Cars.Count == 0)
                        continue;
                    var d = WorldLocation.GetDistanceSquared(t.RearTDBTraveller.WorldLocation, PlayerTrain.RearTDBTraveller.WorldLocation);
                    users.Add(d + StaticRandom.NextDouble(), t);
                }
                trainCount++;
                if (trainCount >= users.Count)
                    trainCount = 0;

                SelectedTrain = users.ElementAt(trainCount).Value;
                if (SelectedTrain.Cars == null || SelectedTrain.Cars.Count == 0)
                    SelectedTrain = PlayerTrain;

                //if (SelectedTrain.LeadLocomotive == null) SelectedTrain.LeadNextLocomotive();
                //if (SelectedTrain.LeadLocomotive != null) { PlayerLocomotive = SelectedTrain.LeadLocomotive; PlayerLocomotiveViewer = World.Trains.GetViewer(Simulator.PlayerLocomotive); }

            }
            catch
            {
                SelectedTrain = PlayerTrain;
            }
            Simulator.AI.TrainListChanged = true;
            CameraActivate();
        }

        /// <summary>
        /// The user has left-clicked with U pressed.
        /// If the mouse was over a coupler, then uncouple the car.
        /// </summary>
        private void TryUncoupleAt(Vector3 nearPoint, Vector3 farPoint)
        {
            // Create a ray from the near clip plane to the far clip plane.
            Vector3 direction = farPoint - nearPoint;
            direction.Normalize();
            Ray pickRay = new Ray(nearPoint, direction);

            // check each car
            Traveller traveller = new Traveller(PlayerTrain.FrontTDBTraveller, true);
            int carNo = 0;
            foreach (TrainCar car in PlayerTrain.Cars)
            {
                float d = (car.CouplerSlackM + car.GetCouplerZeroLengthM()) / 2;
                traveller.Move(car.CarLengthM + d);

                Vector3 xnaCenter = Camera.XnaLocation(traveller.WorldLocation);
                float radius = 2f;  // 2 meter click range
                BoundingSphere boundingSphere = new BoundingSphere(xnaCenter, radius);

                if (null != pickRay.Intersects(boundingSphere))
                {
                    _ = new UncoupleCommand(Log, carNo);
                    break;
                }
                traveller.Move(d);
                carNo++;
            }
        }

        /// <summary>
        /// The user has left-clicked with Alt key pressed.
        /// If the mouse was over a switch, then toggle the switch.
        /// No action if toggling blocks the player loco's path.
        /// </summary>
        private void TryThrowSwitchAt(Vector3 nearPoint, Vector3 farPoint)
        {
            TrackNode bestTn = null;
            float bestD = 10;
            // check each switch
            foreach (TrackNode junctionNode in RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes)
            {
                Vector3 xnaCenter = Camera.XnaLocation(junctionNode.UiD.Location);
                float d = xnaCenter.LineSegmentDistanceSquare(nearPoint, farPoint);

                if (bestD > d)
                {
                    bestTn = junctionNode;
                    bestD = d;
                }
            }
            if (bestTn != null)
            {
                _ = new ToggleAnySwitchCommand(Log, bestTn.TrackCircuitCrossReferences[0].Index);
            }
        }

        public void ToggleAnySwitch(int index)
        {
            Simulator.SignalEnvironment.RequestSetSwitch(index);
        }

        public void ToggleSwitchAhead()
        {
            SignalEnvironment.RequestSetSwitch(PlayerTrain, Direction.Forward);
            //if (PlayerTrain.ControlMode == TrainControlMode.Manual)
            //{
            //    PlayerTrain.ProcessRequestManualSetSwitch(Direction.Forward);
            //}
            //else if (PlayerTrain.ControlMode == TrainControlMode.Explorer)
            //{
            //    PlayerTrain.ProcessRequestExplorerSetSwitch(Direction.Forward);
            //}
        }

        public void ToggleSwitchBehind()
        {
            SignalEnvironment.RequestSetSwitch(PlayerTrain, Direction.Backward);
            //if (PlayerTrain.ControlMode == TrainControlMode.Manual)
            //{
            //    PlayerTrain.ProcessRequestManualSetSwitch(Direction.Backward);
            //}
            //else if (PlayerTrain.ControlMode == TrainControlMode.Explorer)
            //{
            //    PlayerTrain.ProcessRequestExplorerSetSwitch(Direction.Backward);
            //}
        }

        internal void UncoupleBehind(int carPosition)
        {
            Simulator.UncoupleBehind(carPosition);
            //make the camera train to be the player train
            if (PlayerLocomotive != null && PlayerLocomotive.Train != null)
                this.SelectedTrain = PlayerLocomotive.Train;
            CameraActivate();
        }

        internal void BeginRender(RenderFrame frame)
        {
            if (frame.IsScreenChanged)
            {
                AdjustCabHeight(DisplaySize.X, DisplaySize.Y);
            }

            MaterialManager.UpdateShaders();
        }

        internal void EndRender(RenderFrame frame)
        {
            // VisibilityState is used to delay calling SaveScreenshot() by one render cycle.
            // We want the hiding of the MessageWindow to take effect on the screen before the screen content is saved.
            if (Visibility == VisibilityState.Hidden)  // Test for Hidden state must come before setting Hidden state.
            {
                Visibility = VisibilityState.ScreenshotPending;  // Next state else this path would be taken more than once.
                if (!Directory.Exists(Settings.ScreenshotPath))
                    Directory.CreateDirectory(Settings.ScreenshotPath);
                string fileName = Path.Combine(Settings.ScreenshotPath, $"{Application.ProductName} {DateTime.Now:yyyy-MM-dd hh-mm-ss}.png");
                SaveScreenshotToFile(Game.GraphicsDevice, fileName, false, false);
                SaveScreenshot = false; // cancel trigger
            }
            if (SaveScreenshot)
            {
                Visibility = VisibilityState.Hidden;
                // Hide MessageWindow
                windowManager[ViewerWindowType.NotificationOverlay].Close();
                // Audible confirmation that screenshot taken
                ViewerSounds.HandleEvent(TrainEvent.TakeScreenshot);
            }

            //// Use IsDown() not IsPressed() so users can take multiple screenshots as fast as possible by holding down the key.
            //if (UserInput.IsDown(UserCommand.GameScreenshot)
            //    && Visibility == VisibilityState.Visible) // Ensure we only get one screenshot.
            //    new SaveScreenshotCommand(Log);

            // SaveActivityThumbnail and FileStem set by Viewer3D
            // <CJComment> Intended to save a thumbnail-sized image but can't find a way to do this.
            // Currently saving a full screen image and then showing it in Menu.exe at a thumbnail size.
            // </CJComment>
            if (SaveActivityThumbnail)
            {
                SaveActivityThumbnail = false;
                SaveScreenshotToFile(Game.GraphicsDevice, Path.Combine(RuntimeInfo.UserDataFolder, SaveActivityFileStem + ".png"), true, true);
                Simulator.Confirmer.PlainTextMessage(ConfirmLevel.Message, Catalog.GetString("Game saved"), 5);
            }
        }

        private void SaveScreenshotToFile(GraphicsDevice graphicsDevice, string fileName, bool silent, bool thumbnail)
        {
            if (graphicsDevice.GraphicsProfile != GraphicsProfile.HiDef)
                return;

            int width = graphicsDevice.PresentationParameters.BackBufferWidth;
            int heigh = graphicsDevice.PresentationParameters.BackBufferHeight;

            byte[] backBuffer = new byte[width * heigh * 4];
            graphicsDevice.GetBackBufferData(backBuffer);

            Task.Run(() =>
            {
                for (int i = 0; i < backBuffer.Length; i += 4)
                {
                    (backBuffer[i + 0], backBuffer[i + 2], backBuffer[i + 3]) = (backBuffer[i + 2], backBuffer[i + 0], 0xFF);
                }

                using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(width, heigh, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    System.Drawing.Imaging.BitmapData bmData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, width, heigh), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
                    System.Runtime.InteropServices.Marshal.Copy(backBuffer, 0, bmData.Scan0, 4 * width * heigh);
                    bitmap.UnlockBits(bmData);
                    if (thumbnail)
                    {
                        float scale = Math.Min(bitmap.Width / 640f, bitmap.Height / 480);
                        using (System.Drawing.Bitmap resize = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size((int)(bitmap.Width / scale), (int)(bitmap.Height / scale))))
                            resize.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    else
                    {
                        bitmap.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
            });
            if (!silent)
                Simulator.Confirmer.PlainTextMessage(ConfirmLevel.Message, $"Saving screenshot to '{fileName}'.", 10);
            Visibility = VisibilityState.Visible;
            // Reveal MessageWindow
            windowManager[ViewerWindowType.NotificationOverlay].Open();
        }

        private static ulong GetVirtualAddressLimit()
        {
            var buffer = new NativeStructs.MEMORYSTATUSEX { Size = 64 };
            NativeMethods.GlobalMemoryStatusEx(buffer);
            return Math.Min(buffer.TotalVirtual, buffer.TotalPhysical);
        }
    }
}
