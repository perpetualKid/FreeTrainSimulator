// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Info;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Formats.OR.Files;
using Orts.Formats.OR.Models;
using Orts.Scripting.Api;
using Orts.Settings;
using Orts.Simulation.Activities;
using Orts.Simulation.AIs;
using Orts.Simulation.Commanding;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;
using Orts.Simulation.Signalling;
using Orts.Simulation.Timetables;
using Orts.Simulation.Track;
using Orts.Simulation.World;

using Activity = Orts.Simulation.Activities.Activity;

namespace Orts.Simulation
{
    public class PlayerTrainChangedEventArgs : EventArgs
    {
        public Train PreviousTrain { get; }
        public Train CurrentTrain { get; }

        public PlayerTrainChangedEventArgs(Train previousTrain, Train currentTrain)
        {
            PreviousTrain = previousTrain;
            CurrentTrain = currentTrain;
        }
    }

    public class QueryCarViewerLoadedEventArgs : EventArgs
    {
        public TrainCar Car { get; }

        public bool Loaded { get; set; }

        public QueryCarViewerLoadedEventArgs(TrainCar car)
        {
            Car = car;
        }
    }

    /// <summary>
    /// This contains all the essential code to operate trains along paths as defined
    /// in the activity.   It is meant to operate in a separate thread it handles the
    /// following:
    ///    track paths
    ///    switch track positions
    ///    signal indications
    ///    calculating positions and velocities of trains
    ///    
    /// Update is called regularly to
    ///     do physics calculations for train movement
    ///     compute new signal indications
    ///     operate ai trains
    ///     
    /// All keyboard input comes from the viewer class as calls on simulator's methods.
    /// </summary>
    public class Simulator : IGametimeSource
    {
        private string explorePath;
        private string exploreConsist;
        private string timeTableFile;
        private bool playerSwitchOngoing;
        private MovingTable activeMovingTable;

        public static ICatalog Catalog { get; private set; }

        public static Simulator Instance { get; private set; }

        public const float MaxStoppedMpS = 0.1f; // stopped is taken to be a speed less than this 

        public bool GamePaused { get; set; }
        public float GameSpeed { get; set; } = 1;
        /// <summary>
        /// Monotonically increasing time value (in seconds) for the simulation. Starts at 0 and only ever increases, at <see cref="GameSpeed"/>.
        /// Does not change if game is <see cref="GamePaused"/>.
        /// </summary>
        public double GameTime { get; private set; }
        /// <summary>
        /// "Time of day" clock value (in seconds) for the simulation. Starts at activity start time and may increase, at <see cref="GameSpeed"/>,
        /// or jump forwards or jump backwards.
        /// </summary>
        public double ClockTime { get; set; }
        // while Simulator.Update() is running, objects are adjusted to this target time 
        // after Simulator.Update() is complete, the simulator state matches this time

        public UserSettings Settings { get; }

        public bool MetricUnits { get; }
        public FolderStructure.ContentFolder.RouteFolder RouteFolder { get; }

        // Primary Simulator Data 
        // These items represent the current state of the simulator 
        // In multiplayer games, these items must be kept in sync across all players
        // These items are what are saved and loaded in a game save.
        public string RouteName { get; private set; }
        public string ActivityFileName { get; set; }
        public string TimetableFileName { get; set; }
        public bool TimetableMode { get; private set; }
        public bool PreUpdate { get; internal set; }
        public ActivityFile ActivityFile { get; private set; }
        public Activity ActivityRun { get; private set; }
        public Route Route { get; private set; }
        public TrainList Trains { get; private set; }
        public Dictionary<int, Train> TrainDictionary { get; } = new Dictionary<int, Train>();
        public Dictionary<string, Train> NameDictionary { get; } = new Dictionary<string, Train>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, AITrain> AutoGenDictionary { get; } = new Dictionary<int, AITrain>();
#pragma warning disable CA1002 // Do not expose generic lists
        public List<int> StartReference { get; } = new List<int>();
#pragma warning restore CA1002 // Do not expose generic lists
        public Weather Weather { get; } = new Weather();

        public float CurveDurability { get; private set; }  // Sets the durability due to curve speeds in TrainCars - read from consist file.

        public SignalEnvironment SignalEnvironment { get; private set; }
        public AI AI { get; private set; }
        public SeasonType Season { get; private set; }
        public WeatherType WeatherType { get; set; }
        public string UserWeatherFile { get; private set; } = string.Empty;
        public SignalConfigurationFile SignalConfig { get; }

        public string PathFileName { get; private set; }
        public string ConsistFileName { get; private set; }

        public LevelCrossings LevelCrossings { get; private set; }
        public SuperElevation SuperElevation { get; private set; }

        // Used in save and restore form
        public string PathName { get; set; } = "<unknown>";
        public float InitialTileX { get; private set; }
        public float InitialTileZ { get; private set; }
        public HazardManager HazardManager { get; }
        public FuelManager FuelManager { get; }

        public ContainerManager ContainerManager { get; }

#pragma warning disable CA1002 // Do not expose generic lists
        public List<MovingTable> MovingTables { get; } = new List<MovingTable>();
        public List<CarSpawners> CarSpawnerLists { get; } = new List<CarSpawners>();
#pragma warning restore CA1002 // Do not expose generic lists
        public ClockList Clocks { get; private set; }           // List of OR-Clocks given by externe file "openrails\clocks.dat"

        // timetable pools
        public Poolholder PoolHolder { get; private set; }

        // player locomotive
        public MSTSLocomotive PlayerLocomotive { get; set; }    // Set by the Viewer - TODO there could be more than one player so eliminate this.

        // <CJComment> Works but not entirely happy about this arrangement. 
        // Confirmer should be part of the Viewer, rather than the Simulator, as it is part of the user interface.
        // Perhaps an Observer design pattern would be better, so the Simulator sends messages to any observers. </CJComment>
        public Confirmer Confirmer { get; private set; }
        public TrainEvent SoundNotify { get; set; } = TrainEvent.None;
        public ScriptManager ScriptManager { get; private set; }

        public bool IsAutopilotMode { get; private set; }

        public bool UpdaterWorking { get; set; }
        public Train OriginalPlayerTrain { get; private set; } // Used in Activity mode

        public bool PlayerIsInCab { get; set; }
        public bool OpenDoorsInAITrains { get; private set; }

        public MovingTable ActiveMovingTable { get; set; }

        // Replay functionality!
        public CommandLog Log { get; set; }
        public List<ICommand> ReplayCommandList { get; set; }

        /// <summary>
        /// True if a replay is in progress.
        /// Used to show some confirmations which are only valuable during replay (e.g. uncouple or resume activity).
        /// Also used to show the replay countdown in the HUD.
        /// </summary>
        public bool IsReplaying => (ReplayCommandList?.Count > 0);

        public TrainSwitcherData TrainSwitcher { get; } = new TrainSwitcherData();

        public event EventHandler WeatherChanged;
        public event EventHandler AllowedSpeedRaised;
        public event EventHandler PlayerLocomotiveChanged;
        public event EventHandler<PlayerTrainChangedEventArgs> PlayerTrainChanged;
        public event EventHandler<QueryCarViewerLoadedEventArgs> QueryCarViewerLoaded;
        public event EventHandler RequestTTDetachWindow;

        public float TimetableLoadedFraction { get; internal set; }    // Set by AI.PrerunAI(), Get by GameStateRunActivity.Update()

        public Simulator(UserSettings settings, string activityPath)
        {
            Instance = this;
            CatalogManager.SetCatalogDomainPattern(CatalogDomainPattern.AssemblyName, null, RuntimeInfo.LocalesFolder);
            Catalog = CatalogManager.Catalog;

            TimetableMode = false;

            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            GamePaused = settings.StartGamePaused;

            RouteFolder = FolderStructure.RouteFromActivity(activityPath);

            Trace.Write("Loading");

            Trace.Write(" TRK");
            Route = new RouteFile(RouteFolder.TrackFileName).Route;

            RouteName = Route.Name;
            OpenDoorsInAITrains = Route.OpenDoorsInAITrains.GetValueOrDefault(Settings.OpenDoorsInAITrains);

            Trace.Write(" TDB");
            TrackDB trackDatabase = new TrackDatabaseFile(RouteFolder.TrackDatabaseFile(Route.FileName)).TrackDB;

            Trace.Write(" SIGCFG");
            SignalConfig = new SignalConfigurationFile(RouteFolder.SignalConfigurationFile, RouteFolder.ORSignalConfigFile);

            Trace.Write(" DAT");
            TrackSectionsFile tsectionDat = new TrackSectionsFile(RouteFolder.TrackSectionFile);
            if (File.Exists(RouteFolder.RouteTrackSectionFile))
                tsectionDat.AddRouteTSectionDatFile(RouteFolder.RouteTrackSectionFile);

            RoadTrackDB roadDatabase = null;
            if (File.Exists(RouteFolder.RoadTrackDatabaseFile(Route.FileName)))
            {
                Trace.Write(" RDB");
                roadDatabase = new RoadDatabaseFile(RouteFolder.RoadTrackDatabaseFile(Route.FileName)).RoadTrackDB;
            }

            MetricUnits = Settings.MeasurementUnit == MeasurementUnit.Route ? Route.MilepostUnitsMetric : (Settings.MeasurementUnit == MeasurementUnit.Metric || Settings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric);
            RuntimeData.Initialize(Route.Name, tsectionDat, trackDatabase, roadDatabase, SignalConfig, MetricUnits, new RuntimeResolver());

            SuperElevation = new SuperElevation(this);

            Trace.Write(" ACT");

            string carSpawnFile = RouteFolder.CarSpawnerFile;
            if (File.Exists(carSpawnFile))
            {
                Trace.Write(" CARSPAWN");
                CarSpawnerFile csf = new CarSpawnerFile(carSpawnFile, RouteFolder.ShapesFolder);
                CarSpawnerLists.Add(csf.CarSpawners);
            }

            if (File.Exists(carSpawnFile = RouteFolder.OpenRailsCarSpawnerFile))
            {
                Trace.Write(" EXTCARSPAWN");
                ORCarSpawnerFile acsf = new ORCarSpawnerFile(carSpawnFile, RouteFolder.ShapesFolder);
                (CarSpawnerLists as List<CarSpawners>).AddRange(acsf.CarSpawners);
            }

            //Load OR-Clock if external file "openrails\clock.dat" exists --------------------------------------------------------
            string clockFile = Path.Combine(RouteFolder.OpenRailsRouteFolder, "clocks.dat");
            if (File.Exists(clockFile))
            {
                Trace.Write(" EXTCLOCK");
                ClockFile cf = new ClockFile(clockFile, RouteFolder.ShapesFolder);
                Clocks = cf.Clocks;
            }

            Confirmer = new Confirmer(this);
            HazardManager = new HazardManager();
            FuelManager = new FuelManager(this);
            ScriptManager = new ScriptManager();
            ContainerManager = new ContainerManager(this);
            Log = new CommandLog(this);
        }

        public void SetActivity(string activityPath)
        {
            ActivityFileName = Path.GetFileNameWithoutExtension(activityPath);
            ActivityFile = new ActivityFile(activityPath);

            // check for existence of activity file in OpenRails subfolder
            activityPath = Path.Combine(RouteFolder.OpenRailsActivitiesFolder, Path.GetFileName(activityPath));
            if (File.Exists(activityPath))
            {
                ORActivitySettingsFile orActivitySettings = new ORActivitySettingsFile(activityPath);
                OverrideUserSettings(Settings, orActivitySettings.Activity);    // Override user settings for the purposes of this activity
                //TODO override Activity.Activity.AIHornAtCrossings from orActivitySettings
            }

            ActivityRun = new Activity(ActivityFile, this);

            ClockTime = ActivityFile.Activity.Header.StartTime.TotalSeconds;
            Season = ActivityFile.Activity.Header.Season;
            WeatherType = ActivityFile.Activity.Header.Weather;
            if (ActivityFile.Activity.ActivityRestrictedSpeedZones != null)
            {
                ActivityRun.AddRestrictZones(Route, ActivityFile.Activity.ActivityRestrictedSpeedZones);
            }
            IsAutopilotMode = true;
        }

        public void SetExplore(string path, string consist, TimeSpan startTime, SeasonType season, WeatherType weather)
        {
            explorePath = Path.GetFileNameWithoutExtension(path);
            exploreConsist = Path.GetFileNameWithoutExtension(consist);
            ClockTime = startTime.TotalSeconds;
            Season = season;
            WeatherType = weather;
        }

        public void SetExploreThroughActivity(string path, string consist, TimeSpan startTime, SeasonType season, WeatherType weather)
        {
            ActivityFileName = $"ea${RouteFolder.RouteName}${DateTime.Now:yyyyMMddHHmmss}";
            ActivityFile = new ActivityFile((int)startTime.TotalSeconds, Path.GetFileNameWithoutExtension(consist));
            ActivityRun = new Activity(ActivityFile, this);
            explorePath = Path.GetFileNameWithoutExtension(path);
            exploreConsist = Path.GetFileNameWithoutExtension(consist);
            ClockTime = startTime.TotalSeconds;
            Season = season;
            WeatherType = weather;
            IsAutopilotMode = true;
        }

        public void SetTimetableOptions(string timeTableFile, string train, SeasonType season, WeatherType weather, string weatherFile)
        {
            this.timeTableFile = timeTableFile;
            PathName = train;

            Season = season;
            WeatherType = weather;
            // check for user defined weather file
            UserWeatherFile = weatherFile;
        }

        public void Start(CancellationToken cancellationToken)
        {
            if (ActivityFile?.Activity?.Header?.LoadStationsPopulationFile != null)
            {
                ContainerManager.LoadPopulationFromFile(Path.Combine(RouteFolder.OpenRailsActivitiesFolder, Path.ChangeExtension(ActivityFile?.Activity?.Header?.LoadStationsPopulationFile, ".load-stations-loads-or")));
            }
            SignalEnvironment = new SignalEnvironment(SignalConfig, Settings.UseLocationPassingPaths, cancellationToken);
            MovingTables.AddRange(MovingTableFile.ReadTurntableFile(Path.Combine(RouteFolder.OpenRailsRouteFolder, "turntables.dat")));
            LevelCrossings = new LevelCrossings();
            Trains = new TrainList(this);
            PoolHolder = new Poolholder();

            _ = IsAutopilotMode ? InitializeAPTrains(cancellationToken) : InitializeTrains(cancellationToken);

            MultiPlayerManager.Instance().RememberOriginalSwitchState();

            // start activity logging if required
            if (Settings.EvaluationStationStops && ActivityRun != null)
            {
                string stationLogFile = DeriveLogFile("Stops");
                if (!string.IsNullOrEmpty(stationLogFile))
                {
                    ActivityRun.StartStationLogging(stationLogFile);
                }
            }
        }

        public void StartTimetable(CancellationToken cancellationToken)
        {
            TimetableMode = true;
            SignalEnvironment = new SignalEnvironment(SignalConfig, true, cancellationToken);
            (MovingTables as List<MovingTable>).AddRange(MovingTableFile.ReadTurntableFile(Path.Combine(RouteFolder.OpenRailsRouteFolder, "turntables.dat")));
            LevelCrossings = new LevelCrossings();
            Trains = new TrainList(this);
            PoolHolder = new Poolholder(this, timeTableFile, cancellationToken);

            TimetableInfo TTinfo = new TimetableInfo(this);
            List<TTTrain> allTrains = TTinfo.ProcessTimetable(timeTableFile, PathName, cancellationToken);
            TTTrain playerTTTrain = allTrains[0];

            AI = new AI(this, allTrains, playerTTTrain.FormedOf, playerTTTrain.FormedOfType, playerTTTrain, cancellationToken);
            ClockTime = AI.ClockTime;

            if (playerTTTrain != null)
            {
                playerTTTrain.CalculatePositionOfCars(); // calculate position of player train cars
                playerTTTrain.PostInit();               // place player train after pre-running of AI trains
                if (!TrainDictionary.ContainsKey(playerTTTrain.Number))
                    TrainDictionary.Add(playerTTTrain.Number, playerTTTrain);
                if (!NameDictionary.ContainsKey(playerTTTrain.Name))
                    NameDictionary.Add(playerTTTrain.Name, playerTTTrain);
            }
        }

        public void Stop()
        {
            if (MultiPlayerManager.IsMultiPlayer())
                MultiPlayerManager.Stop();
        }

        public void Restore(BinaryReader inf, string pathName, float initialTileX, float initialTileZ, CancellationToken cancellationToken)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            ContainerManager.FreightAnimNeedsInitialization = false;
            ClockTime = inf.ReadDouble();
            Season = (SeasonType)inf.ReadInt32();
            WeatherType = (WeatherType)inf.ReadInt32();
            TimetableMode = inf.ReadBoolean();
            UserWeatherFile = inf.ReadString();
            PathName = pathName;
            InitialTileX = initialTileX;
            InitialTileZ = initialTileZ;
            PoolHolder = new Poolholder(inf, this);

            SignalEnvironment = new SignalEnvironment(SignalConfig, false, cancellationToken);
            SignalEnvironment.Restore(inf);

            RestoreTrains(inf);
            LevelCrossings = new LevelCrossings();
            AI = new AI(this, inf);
            // Find original player train
            OriginalPlayerTrain = Trains.Find(item => item.Number == 0);
            if (OriginalPlayerTrain == null)
                OriginalPlayerTrain = AI.AITrains.Find(item => item.Number == 0);

            // initialization of turntables
            int movingTableIndex = inf.ReadInt32();
            MovingTables.AddRange(MovingTableFile.ReadTurntableFile(Path.Combine(RouteFolder.OpenRailsRouteFolder, "turntables.dat")));
            if (MovingTables.Count >= 0)
            {
                foreach (MovingTable movingTable in MovingTables)
                    movingTable.Restore(inf, this);
            }
            activeMovingTable = movingTableIndex >= 0 && movingTableIndex < MovingTables.Count ? MovingTables[movingTableIndex] : null;

            ActivityRun = Activity.Restore(inf, this, ActivityRun);
            SignalEnvironment.RestoreTrains(Trains);  // restore links to trains
            SignalEnvironment.Update(true);           // update all signals once to set proper stat
            ContainerManager.Restore(inf);
            MultiPlayerManager.Instance().RememberOriginalSwitchState(); // this prepares a string that must then be passed to clients
        }

        public void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));

            outf.Write(ClockTime);
            outf.Write((int)Season);
            outf.Write((int)WeatherType);
            outf.Write(TimetableMode);
            outf.Write(UserWeatherFile);
            PoolHolder.Save(outf);
            SignalEnvironment.Save(outf);
            SaveTrains(outf);
            // LevelCrossings
            // InterlockingSystem
            AI.Save(outf);

            outf.Write(MovingTables.IndexOf(activeMovingTable));
            if (MovingTables.Count >= 0)
            {
                foreach (MovingTable movingtable in MovingTables)
                    movingtable.Save(outf);
            }
            Activity.Save(outf, ActivityRun);
            ContainerManager.Save(outf);
        }

        private Train InitializeTrains(CancellationToken cancellationToken)
        {
            Train playerTrain = InitializePlayerTrain();
            InitializeStaticConsists();
            AI = new AI(this, ClockTime, cancellationToken);
            if (playerTrain != null)
            {
                _ = playerTrain.PostInit();
                TrainDictionary.Add(playerTrain.Number, playerTrain);
                NameDictionary.Add(playerTrain.Name, playerTrain);
            }
            return (playerTrain);
        }

        private AITrain InitializeAPTrains(CancellationToken cancellationToken)
        {
            AITrain playerTrain = InitializeAPPlayerTrain();
            InitializeStaticConsists();
            AI = new AI(this, ClockTime, cancellationToken);
            playerTrain.AI = AI;
            if (playerTrain != null)
            {
                bool validPosition = playerTrain.PostInit();  // place player train after pre-running of AI trains
                if (validPosition)
                    PreUpdate = false;
                if (playerTrain.InitialSpeed > 0 && playerTrain.MovementState != AiMovementState.StationStop)
                {
                    playerTrain.InitializeMoving();
                    playerTrain.MovementState = AiMovementState.Braking;
                }
                else if (playerTrain.InitialSpeed == 0)
                    playerTrain.InitializeBrakes();
            }
            return (playerTrain);
        }


        /// <summary>
        /// Which locomotive does the activity specified for the player.
        /// </summary>
        public MSTSLocomotive InitialPlayerLocomotive()
        {
            Train playerTrain = Trains[0];    // we install the player train first
            PlayerLocomotive = SetPlayerLocomotive(playerTrain);
            return PlayerLocomotive;
        }

        public void SetCommandReceivers()
        {
            ReverserCommand.Receiver = PlayerLocomotive;
            NotchedThrottleCommand.Receiver = PlayerLocomotive;
            ContinuousThrottleCommand.Receiver = PlayerLocomotive;
            TrainBrakeCommand.Receiver = PlayerLocomotive;
            EngineBrakeCommand.Receiver = PlayerLocomotive;
            BrakemanBrakeCommand.Receiver = PlayerLocomotive;
            DynamicBrakeCommand.Receiver = PlayerLocomotive;
            InitializeBrakesCommand.Receiver = PlayerLocomotive.Train;
            ResetOutOfControlModeCommand.Receiver = PlayerLocomotive.Train;
            EmergencyPushButtonCommand.Receiver = PlayerLocomotive;
            HandbrakeCommand.Receiver = PlayerLocomotive;
            BailOffCommand.Receiver = PlayerLocomotive;
            QuickReleaseCommand.Receiver = PlayerLocomotive;
            BrakeOverchargeCommand.Receiver = PlayerLocomotive;
            RetainersCommand.Receiver = PlayerLocomotive;
            BrakeHoseConnectCommand.Receiver = PlayerLocomotive;
            ToggleWaterScoopCommand.Receiver = PlayerLocomotive;
            if (PlayerLocomotive is MSTSSteamLocomotive steamLocomotive)
            {
                ContinuousReverserCommand.Receiver = steamLocomotive;
                ContinuousInjectorCommand.Receiver = steamLocomotive;
                ContinuousSmallEjectorCommand.Receiver = steamLocomotive;
                ContinuousLargeEjectorCommand.Receiver = steamLocomotive;
                ToggleInjectorCommand.Receiver = steamLocomotive;
                ToggleBlowdownValveCommand.Receiver = steamLocomotive;
                ContinuousBlowerCommand.Receiver = steamLocomotive;
                ContinuousDamperCommand.Receiver = steamLocomotive;
                ContinuousFiringRateCommand.Receiver = steamLocomotive;
                ToggleManualFiringCommand.Receiver = steamLocomotive;
                ToggleCylinderCocksCommand.Receiver = steamLocomotive;
                ToggleCylinderCompoundCommand.Receiver = steamLocomotive;
                FireShovelfullCommand.Receiver = steamLocomotive;
                AIFireOnCommand.Receiver = steamLocomotive;
                AIFireOffCommand.Receiver = steamLocomotive;
                AIFireResetCommand.Receiver = steamLocomotive;
            }

            PantographCommand.Receiver = PlayerLocomotive;
            if (PlayerLocomotive is MSTSElectricLocomotive electricLocomotive)
            {
                CircuitBreakerClosingOrderCommand.Receiver = electricLocomotive.LocomotivePowerSupply;
                CircuitBreakerClosingOrderButtonCommand.Receiver = electricLocomotive.LocomotivePowerSupply;
                CircuitBreakerOpeningOrderButtonCommand.Receiver = electricLocomotive.LocomotivePowerSupply;
                CircuitBreakerClosingAuthorizationCommand.Receiver = electricLocomotive.LocomotivePowerSupply;
            }

            if (PlayerLocomotive is MSTSDieselLocomotive dieselLocomotive)
            {
                TractionCutOffRelayClosingOrderCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                TractionCutOffRelayClosingOrderButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                TractionCutOffRelayOpeningOrderButtonCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                TractionCutOffRelayClosingAuthorizationCommand.Receiver = (PlayerLocomotive as MSTSLocomotive).LocomotivePowerSupply;
                TogglePlayerEngineCommand.Receiver = dieselLocomotive;
                VacuumExhausterCommand.Receiver = dieselLocomotive;
            }

            ResetOdometerCommand.Receiver = PlayerLocomotive;
            ToggleOdometerDirectionCommand.Receiver = PlayerLocomotive;
            SanderCommand.Receiver = PlayerLocomotive;
            AlerterCommand.Receiver = PlayerLocomotive;
            HornCommand.Receiver = PlayerLocomotive;
            BellCommand.Receiver = PlayerLocomotive;
            ToggleCabLightCommand.Receiver = PlayerLocomotive;
            WipersCommand.Receiver = PlayerLocomotive;
            HeadlightCommand.Receiver = PlayerLocomotive;
            ToggleDoorsLeftCommand.Receiver = PlayerLocomotive;
            ToggleDoorsRightCommand.Receiver = PlayerLocomotive;
            ToggleMirrorsCommand.Receiver = PlayerLocomotive;
            CabRadioCommand.Receiver = PlayerLocomotive;
            ToggleHelpersEngineCommand.Receiver = PlayerLocomotive;
            BatterySwitchCommand.Receiver = PlayerLocomotive.LocomotivePowerSupply;
            BatterySwitchCloseButtonCommand.Receiver = PlayerLocomotive.LocomotivePowerSupply;
            BatterySwitchOpenButtonCommand.Receiver = PlayerLocomotive.LocomotivePowerSupply;
            ToggleMasterKeyCommand.Receiver = PlayerLocomotive.LocomotivePowerSupply;
            ServiceRetentionButtonCommand.Receiver = PlayerLocomotive.LocomotivePowerSupply;
            ServiceRetentionCancellationButtonCommand.Receiver = PlayerLocomotive.LocomotivePowerSupply;
            ElectricTrainSupplyCommand.Receiver = PlayerLocomotive.LocomotivePowerSupply;
            TCSButtonCommand.Receiver = PlayerLocomotive.TrainControlSystem;
            TCSSwitchCommand.Receiver = PlayerLocomotive.TrainControlSystem;
            ToggleGenericItem1Command.Receiver = PlayerLocomotive;
            ToggleGenericItem2Command.Receiver = PlayerLocomotive;

            //Distributed power
            DistributedPowerMoveToFrontCommand.Receiver = PlayerLocomotive;
            DistributedPowerMoveToBackCommand.Receiver = PlayerLocomotive;
            DistributedPowerTractionCommand.Receiver = PlayerLocomotive;
            DistributedPowerIdleCommand.Receiver = PlayerLocomotive;
            DistributedPowerDynamicBrakeCommand.Receiver = PlayerLocomotive;
            DistributedPowerIncreaseCommand.Receiver = PlayerLocomotive;
            DistributedPowerDecreaseCommand.Receiver = PlayerLocomotive;

            //EOT
            EOTCommTestCommand.Receiver = PlayerLocomotive;
            EOTDisarmCommand.Receiver = PlayerLocomotive;
            EOTArmTwoWayCommand.Receiver = PlayerLocomotive;
            EOTEmergencyBrakeCommand.Receiver = PlayerLocomotive;
            ToggleEOTEmergencyBrakeCommand.Receiver = PlayerLocomotive;
            EOTMountCommand.Receiver = PlayerLocomotive;
        }

        internal static MSTSLocomotive SetPlayerLocomotive(Train playerTrain)
        {
            MSTSLocomotive playerLocomotive = null;
            foreach (TrainCar car in playerTrain.Cars)
                if (car is MSTSLocomotive locomotive)  // first loco is the one the player drives
                {
                    playerLocomotive = locomotive;
                    playerTrain.LeadLocomotive = locomotive;
                    playerTrain.InitializeBrakes();
                    playerLocomotive.LocalThrottlePercent = playerTrain.AITrainThrottlePercent;
                    break;
                }
            return playerLocomotive ?? throw new InvalidDataException("Can't find player locomotive in activity");
        }

        /// <summary>
        /// Gets path and consist of player train in multiplayer resume in activity
        /// </summary>
        public void SetPathAndConsist()
        {
            ServiceFile srvFile = new ServiceFile(RouteFolder.ServiceFile(ActivityFile.Activity.PlayerServices.Name));
            ConsistFileName = RouteFolder.ContentFolder.ConsistFile(srvFile.TrainConfig);
            PathFileName = RouteFolder.PathFile(srvFile.PathId);
        }


        /// <summary>
        /// Convert and elapsed real time into clock time based on simulator
        /// running speed and paused state.
        /// </summary>
        public double GetElapsedClockSeconds(double elapsedRealSeconds)
        {
            return elapsedRealSeconds * (GamePaused ? 0 : GameSpeed);
        }

        /// <summary>
        /// Update the simulator state 
        /// elapsedClockSeconds represents the time since the last call to Simulator.Update
        /// Executes in the UpdaterProcess thread.
        /// </summary>
        public void Update(double elapsedClockSeconds)
        {
            // Advance the times.
            GameTime += elapsedClockSeconds;
            ClockTime += elapsedClockSeconds;

            // Check if there is a request to switch to another played train

            if (TrainSwitcher.ClickedSelectedAsPlayer && !playerSwitchOngoing)
                StartSwitchPlayerTrain();
            if (playerSwitchOngoing)
            {
                // We need to check whether the player locomotive has loaded before we complete the train switch.
                if (!OnQueryCarViewerLoaded(PlayerLocomotive))
                    return;
                CompleteSwitchPlayerTrain();
            }

            // Must be done before trains so that during turntable rotation train follows it
            ActiveMovingTable?.Update();

            // Represent conditions at the specified clock time.
            List<Train> movingTrains = new List<Train>();

            if (PlayerLocomotive != null)
            {
                movingTrains.Add(PlayerLocomotive.Train);
                if (PlayerLocomotive.Train.LeadLocomotive != null
                    && PlayerLocomotive.Train.TrainType != TrainType.AiPlayerHosting
                    && !string.Equals(PlayerLocomotive.Train.LeadLocomotive.CarID, PlayerLocomotive.CarID, StringComparison.OrdinalIgnoreCase)
                    && !MultiPlayerManager.IsMultiPlayer())
                {
                    PlayerLocomotive = PlayerLocomotive.Train.LeadLocomotive;
                }
            }

            foreach (Train train in Trains)
            {
                if ((train.SpeedMpS != 0 || (train.ControlMode == TrainControlMode.Explorer && train.TrainType == TrainType.Remote && MultiPlayerManager.IsServer())) &&
                    !(train is AITrain) && !(train is TTTrain) &&
                    (PlayerLocomotive == null || train != PlayerLocomotive.Train))
                {
                    movingTrains.Add(train);
                }
            }

            foreach (Train train in movingTrains)
            {
                if (MultiPlayerManager.IsMultiPlayer())
                {
                    try
                    {
                        if (train.TrainType != TrainType.AiPlayerHosting)
                            train.Update(elapsedClockSeconds, false);
                        else
                            ((AITrain)train).AIUpdate(elapsedClockSeconds, ClockTime, false);
                    }
                    catch (Exception e) { Trace.TraceWarning(e.Message); }
                }
                else if (train.TrainType != TrainType.AiPlayerHosting)
                {
                    train.Update(elapsedClockSeconds, false);
                }
                else
                {
                    ((AITrain)train).AIUpdate(elapsedClockSeconds, ClockTime, false);
                }
            }

            if (!TimetableMode)
            {
                if (MultiPlayerManager.MultiplayerState != MultiplayerState.Client)
                {
                    foreach (Train train in movingTrains)
                    {
                        CheckForCoupling(train, elapsedClockSeconds);
                    }
                }
                else if (PlayerLocomotive != null)
                {
                    CheckForCoupling(PlayerLocomotive.Train, elapsedClockSeconds);
                }
            }

            if (MultiPlayerManager.MultiplayerState != MultiplayerState.Client)
                SignalEnvironment?.Update(false);

            if (AI != null)
            {
                if (TimetableMode)
                {
                    AI.TimetableUpdate(elapsedClockSeconds);
                }
                else
                {
                    AI.ActivityUpdate(elapsedClockSeconds);
                }
            }

            LevelCrossings?.Update(elapsedClockSeconds);
            ActivityRun?.Update();
            HazardManager?.Update(elapsedClockSeconds);
            FuelManager.Update();
            ContainerManager.Update();
            ActivityEvaluation.Instance.Update();
        }

        internal void SetWeather(WeatherType weather, SeasonType season)
        {
            WeatherType = weather;
            Season = season;

            WeatherChanged?.Invoke(this, EventArgs.Empty);
        }

        private void FinishFrontCoupling(Train drivenTrain, Train train, MSTSLocomotive lead, bool sameDirection)
        {
            drivenTrain.LeadLocomotive = lead;
            drivenTrain.CalculatePositionOfCars();
            FinishCoupling(drivenTrain, train, true, sameDirection);
        }

        private void FinishRearCoupling(Train drivenTrain, Train train, bool sameDirection)
        {
            drivenTrain.RepositionRearTraveller();
            FinishCoupling(drivenTrain, train, false, sameDirection);
        }

        private void FinishCoupling(Train drivenTrain, Train train, bool coupleToFront, bool sameDirection)
        {
            // if coupled train was on turntable and static, remove it from list of trains on turntable
            if (ActiveMovingTable != null && ActiveMovingTable.TrainsOnMovingTable.Count != 0)
            {
                foreach (TrainOnMovingTable trainOnMovingTable in ActiveMovingTable.TrainsOnMovingTable)
                {
                    if (trainOnMovingTable.Train.Number == train.Number)
                    {
                        ActiveMovingTable.TrainsOnMovingTable.Remove(trainOnMovingTable);
                        break;
                    }
                }
            }
            if (train.TrainType == TrainType.Ai && (((AITrain)train).UncondAttach ||
                train.TCRoute.ActiveSubPath < train.TCRoute.TCRouteSubpaths.Count - 1 || train.ValidRoute[0].Count > 5))
            {
                if (((drivenTrain.TCRoute != null && drivenTrain.TCRoute.ActiveSubPath == drivenTrain.TCRoute.TCRouteSubpaths.Count - 1 &&
                    drivenTrain.ValidRoute[0].Count < 5) || (drivenTrain is AITrain aiTrain && aiTrain.UncondAttach)) && drivenTrain != OriginalPlayerTrain)
                {
                    // Switch to the attached train as the one where we are now is at the end of its life
                    TrainSwitcher.PickedTrainFromList = train;
                    TrainSwitcher.ClickedTrainFromList = true;
                    train.TrainType = TrainType.AiPlayerHosting;
                    Confirmer.Message(ConfirmLevel.Information, Catalog.GetString("Player train has been included into train {0} service {1}, that automatically becomes the new player train",
                        train.Number, train.Name));
                    train.Cars.Clear();
                    if (sameDirection)
                    {
                        foreach (TrainCar car in drivenTrain.Cars)
                        {
                            train.Cars.Add(car);
                            car.Train = train;
                        }
                    }
                    else
                    {
                        for (int i = drivenTrain.Cars.Count - 1; i >= 0; --i)
                        {
                            TrainCar car = drivenTrain.Cars[i];
                            train.Cars.Add(car);
                            car.Train = train;
                            car.Flipped = !car.Flipped;
                        }
                        if (drivenTrain.LeadLocomotiveIndex != -1)
                            train.LeadLocomotiveIndex = train.Cars.Count - drivenTrain.LeadLocomotiveIndex - 1;
                    }
                    drivenTrain.Cars.Clear();
                    AI.TrainsToRemoveFromAI.Add((AITrain)train);
                    PlayerLocomotive = SetPlayerLocomotive(train);
                    (train as AITrain).SwitchToPlayerControl();
                    OnPlayerLocomotiveChanged();
                    if (drivenTrain.TCRoute.ActiveSubPath == drivenTrain.TCRoute.TCRouteSubpaths.Count - 1 && drivenTrain.ValidRoute[0].Count < 5)
                    {
                        (drivenTrain as AITrain).RemoveTrain();
                        train.UpdateTrackActionsCoupling(coupleToFront);
                        return;
                    }
                    // if there is just here a reversal point, increment subpath in order to be in accordance with train
                    int ppTCSectionIndex = drivenTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                    if (ppTCSectionIndex == drivenTrain.TCRoute.TCRouteSubpaths[drivenTrain.TCRoute.ActiveSubPath][^1].TrackCircuitSection.Index)
                        Train.IncrementSubpath(drivenTrain);
                    // doubled check in case of double reverse point.
                    if (ppTCSectionIndex == drivenTrain.TCRoute.TCRouteSubpaths[drivenTrain.TCRoute.ActiveSubPath][^1].TrackCircuitSection.Index)
                        Train.IncrementSubpath(drivenTrain);
                    Train tempTrain = drivenTrain;
                    drivenTrain = train;
                    train = tempTrain;
                    AI.AITrains.Add(train as AITrain);
                }
                else
                {
                    int ppTCSectionIndex = train.PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                    if (ppTCSectionIndex == train.TCRoute.TCRouteSubpaths[train.TCRoute.ActiveSubPath][^1].TrackCircuitSection.Index)
                        Train.IncrementSubpath(train);
                    // doubled check in case of double reverse point.
                    if (ppTCSectionIndex == train.TCRoute.TCRouteSubpaths[train.TCRoute.ActiveSubPath][^1].TrackCircuitSection.Index)
                        Train.IncrementSubpath(train);
                }
                train.IncorporatingTrain = drivenTrain;
                train.IncorporatingTrainNo = drivenTrain.Number;
                ((AITrain)train).SuspendTrain(drivenTrain);
                drivenTrain.IncorporatedTrainNo = train.Number;
                if (MultiPlayerManager.IsMultiPlayer())
                    MultiPlayerManager.BroadCast((new MSGCouple(drivenTrain, train, false)).ToString());
            }
            else
            {
                train.RemoveFromTrack();
                if (train.TrainType != TrainType.AiIncorporated)
                {
                    Trains.Remove(train);
                    TrainDictionary.Remove(train.Number);
                    NameDictionary.Remove(train.Name);
                }
                if (MultiPlayerManager.IsMultiPlayer())
                    MultiPlayerManager.BroadCast((new MSGCouple(drivenTrain, train, train.TrainType != TrainType.AiIncorporated)).ToString());
            }
            if (train.UncoupledFrom != null)
                train.UncoupledFrom.UncoupledFrom = null;

            if (PlayerLocomotive?.Train == train)
            {
                drivenTrain.AITrainThrottlePercent = train.AITrainThrottlePercent;
                drivenTrain.AITrainBrakePercent = train.AITrainBrakePercent;
                drivenTrain.LeadLocomotive = PlayerLocomotive;
            }

            drivenTrain.UpdateTrackActionsCoupling(coupleToFront);
            AI.TrainListChanged = true;
        }

        private static void UpdateUncoupled(Train drivenTrain, Train train, float d1, float d2, bool rear)
        {
            if (train == drivenTrain.UncoupledFrom && d1 > .5 && d2 > .5)
            {
                Traveller traveller = rear ? drivenTrain.RearTDBTraveller : drivenTrain.FrontTDBTraveller;
                float d3 = traveller.OverlapDistanceM(train.FrontTDBTraveller, rear);
                float d4 = traveller.OverlapDistanceM(train.RearTDBTraveller, rear);
                if (d3 > .5 && d4 > .5)
                {
                    train.UncoupledFrom = null;
                    drivenTrain.UncoupledFrom = null;
                }
            }
        }

        /// <summary>
        /// Scan other trains
        /// </summary>
        private void CheckForCoupling(Train drivenTrain, double elapsedClockSeconds)
        {
            if (MultiPlayerManager.IsMultiPlayer() && !MultiPlayerManager.IsServer())
                return; //in MultiPlayer mode, server will check coupling, client will get message and do things
            if (drivenTrain.SpeedMpS < 0)
            {
                foreach (Train train in Trains)
                    if (train != drivenTrain && train.TrainType != TrainType.AiIncorporated)
                    {
                        //avoid coupling of player train with other players train
                        if (MultiPlayerManager.IsMultiPlayer() && !MultiPlayerManager.TrainOK2Couple(this, drivenTrain, train))
                            continue;

                        float d1 = drivenTrain.RearTDBTraveller.OverlapDistanceM(train.FrontTDBTraveller, true);
                        // Give another try if multiplayer
                        if (d1 >= 0 && drivenTrain.TrainType == TrainType.Remote &&
                            drivenTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex == train.PresentPosition[Direction.Forward].TrackCircuitSectionIndex && drivenTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex != -1)
                            d1 = drivenTrain.RearTDBTraveller.RoughOverlapDistanceM(train.FrontTDBTraveller, drivenTrain.FrontTDBTraveller, train.RearTDBTraveller, drivenTrain.Length, train.Length, true);
                        if (d1 < 0)
                        {
                            if (train == drivenTrain.UncoupledFrom)
                            {
                                if (drivenTrain.SpeedMpS < train.SpeedMpS)
                                    drivenTrain.SetCoupleSpeed(train, 1);
                                drivenTrain.CalculatePositionOfCars(elapsedClockSeconds, -d1);
                                return;
                            }
                            // couple my rear to front of train
                            //drivenTrain.SetCoupleSpeed(train, 1);
                            drivenTrain.LastCar.SignalEvent(TrainEvent.Couple);
                            if (drivenTrain.SpeedMpS > 1.5)
                                ActivityEvaluation.Instance.OverSpeedCoupling++;

                            foreach (TrainCar car in train.Cars)
                            {
                                drivenTrain.Cars.Add(car);
                                car.Train = drivenTrain;
                            }
                            FinishRearCoupling(drivenTrain, train, true);
                            return;
                        }
                        float d2 = drivenTrain.RearTDBTraveller.OverlapDistanceM(train.RearTDBTraveller, true);
                        // Give another try if multiplayer
                        if (d2 >= 0 && drivenTrain.TrainType == TrainType.Remote &&
                            drivenTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex == train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex && drivenTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex != -1)
                            d2 = drivenTrain.RearTDBTraveller.RoughOverlapDistanceM(train.RearTDBTraveller, drivenTrain.FrontTDBTraveller, train.FrontTDBTraveller, drivenTrain.Length, train.Length, true);
                        if (d2 < 0)
                        {
                            if (train == drivenTrain.UncoupledFrom)
                            {
                                if (drivenTrain.SpeedMpS < -train.SpeedMpS)
                                    drivenTrain.SetCoupleSpeed(train, 11);
                                drivenTrain.CalculatePositionOfCars(elapsedClockSeconds, -d2);
                                return;
                            }
                            // couple my rear to rear of train
                            //drivenTrain.SetCoupleSpeed(train, -1);
                            drivenTrain.LastCar.SignalEvent(TrainEvent.Couple);
                            if (drivenTrain.SpeedMpS > 1.5)
                                ActivityEvaluation.Instance.OverSpeedCoupling++;

                            for (int i = train.Cars.Count - 1; i >= 0; --i)
                            {
                                TrainCar car = train.Cars[i];
                                drivenTrain.Cars.Add(car);
                                car.Train = drivenTrain;
                                car.Flipped = !car.Flipped;
                            }
                            FinishRearCoupling(drivenTrain, train, false);
                            return;
                        }
                        UpdateUncoupled(drivenTrain, train, d1, d2, false);
                    }
            }
            else if (drivenTrain.SpeedMpS > 0)
            {
                foreach (Train train in Trains)
                    if (train != drivenTrain && train.TrainType != TrainType.AiIncorporated)
                    {
                        //avoid coupling of player train with other players train if it is too short alived (e.g, when a train is just spawned, it may overlap with another train)
                        if (MultiPlayerManager.IsMultiPlayer() && !MultiPlayerManager.TrainOK2Couple(this, drivenTrain, train))
                            continue;
                        //	{
                        //		if ((MPManager.Instance().FindPlayerTrain(train) && drivenTrain == PlayerLocomotive.Train) || (MPManager.Instance().FindPlayerTrain(drivenTrain) && train == PlayerLocomotive.Train)) continue;
                        //		if ((MPManager.Instance().FindPlayerTrain(train) && MPManager.Instance().FindPlayerTrain(drivenTrain))) continue; //if both are player-controlled trains
                        //	}
                        float d1 = drivenTrain.FrontTDBTraveller.OverlapDistanceM(train.RearTDBTraveller, false);
                        // Give another try if multiplayer
                        if (d1 >= 0 && drivenTrain.TrainType == TrainType.Remote &&
                            drivenTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex == train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex && drivenTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex != -1)
                            d1 = drivenTrain.FrontTDBTraveller.RoughOverlapDistanceM(train.RearTDBTraveller, drivenTrain.RearTDBTraveller, train.FrontTDBTraveller, drivenTrain.Length, train.Length, false);
                        if (d1 < 0)
                        {
                            if (train == drivenTrain.UncoupledFrom)
                            {
                                if (drivenTrain.SpeedMpS > train.SpeedMpS)
                                    drivenTrain.SetCoupleSpeed(train, 1);
                                drivenTrain.CalculatePositionOfCars(elapsedClockSeconds, d1);
                                return;
                            }
                            // couple my front to rear of train
                            //drivenTrain.SetCoupleSpeed(train, 1);

                            MSTSLocomotive lead = drivenTrain.LeadLocomotive;
                            if (lead == null)
                            {//Like Rear coupling with changed data  
                                _ = train.LeadLocomotive;
                                train.LastCar.SignalEvent(TrainEvent.Couple);
                                if (drivenTrain.SpeedMpS > 1.5)
                                    ActivityEvaluation.Instance.OverSpeedCoupling++;

                                for (int i = 0; i < drivenTrain.Cars.Count; ++i)
                                {
                                    TrainCar car = drivenTrain.Cars[i];
                                    train.Cars.Add(car);
                                    car.Train = train;
                                }
                                //Rear coupling
                                FinishRearCoupling(train, drivenTrain, false);
                            }
                            else
                            {
                                drivenTrain.FirstCar.SignalEvent(TrainEvent.Couple);
                                if (drivenTrain.SpeedMpS > 1.5)
                                    ActivityEvaluation.Instance.OverSpeedCoupling++;

                                lead = drivenTrain.LeadLocomotive;
                                for (int i = 0; i < train.Cars.Count; ++i)
                                {
                                    TrainCar car = train.Cars[i];
                                    drivenTrain.Cars.Insert(i, car);
                                    car.Train = drivenTrain;
                                }
                                if (drivenTrain.LeadLocomotiveIndex >= 0)
                                    drivenTrain.LeadLocomotiveIndex += train.Cars.Count;
                                FinishFrontCoupling(drivenTrain, train, lead, true);
                            }
                            return;
                        }
                        float d2 = drivenTrain.FrontTDBTraveller.OverlapDistanceM(train.FrontTDBTraveller, false);
                        // Give another try if multiplayer
                        if (d2 >= 0 && drivenTrain.TrainType == TrainType.Remote &&
                            drivenTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex == train.PresentPosition[Direction.Forward].TrackCircuitSectionIndex && drivenTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex != -1)
                            d2 = drivenTrain.FrontTDBTraveller.RoughOverlapDistanceM(train.FrontTDBTraveller, drivenTrain.RearTDBTraveller, train.RearTDBTraveller, drivenTrain.Length, train.Length, false);
                        if (d2 < 0)
                        {
                            if (train == drivenTrain.UncoupledFrom)
                            {
                                if (drivenTrain.SpeedMpS > -train.SpeedMpS)
                                    drivenTrain.SetCoupleSpeed(train, -1);
                                drivenTrain.CalculatePositionOfCars(elapsedClockSeconds, d2);
                                return;
                            }
                            // couple my front to front of train
                            //drivenTrain.SetCoupleSpeed(train, -1);
                            drivenTrain.FirstCar.SignalEvent(TrainEvent.Couple);
                            if (drivenTrain.SpeedMpS > 1.5)
                                ActivityEvaluation.Instance.OverSpeedCoupling++;

                            MSTSLocomotive lead = drivenTrain.LeadLocomotive;
                            for (int i = 0; i < train.Cars.Count; ++i)
                            {
                                TrainCar car = train.Cars[i];
                                drivenTrain.Cars.Insert(0, car);
                                car.Train = drivenTrain;
                                car.Flipped = !car.Flipped;
                            }
                            if (drivenTrain.LeadLocomotiveIndex >= 0)
                                drivenTrain.LeadLocomotiveIndex += train.Cars.Count;
                            FinishFrontCoupling(drivenTrain, train, lead, false);
                            return;
                        }

                        UpdateUncoupled(drivenTrain, train, d1, d2, true);
                    }
            }
        }

        private Train InitializePlayerTrain()
        {

            Debug.Assert(Trains != null, "Cannot InitializePlayerTrain() without Simulator.Trains.");
            // set up the player locomotive

            Train train = new Train
            {
                TrainType = TrainType.Player,
                Number = 0,
                Name = "PLAYER"
            };

            string playerServiceFileName;
            ServiceFile serviceFile;

            playerServiceFileName = Path.GetFileNameWithoutExtension(exploreConsist);
            serviceFile = new ServiceFile(playerServiceFileName, playerServiceFileName, explorePath);

            ConsistFileName = RouteFolder.ContentFolder.ConsistFile(serviceFile.TrainConfig);
            PathFileName = RouteFolder.PathFile(serviceFile.PathId);
            OriginalPlayerTrain = train;

            train.IsTilting = ConsistFileName.Contains("tilted", StringComparison.OrdinalIgnoreCase);

            AIPath aiPath = new AIPath(PathFileName, TimetableMode);
            PathName = aiPath.PathName;

            if (aiPath.Nodes == null)
            {
                throw new InvalidDataException($"Broken path {PathFileName} for Player train - activity cannot be started");
            }

            // place rear of train on starting location of aiPath.
            train.RearTDBTraveller = new Traveller(aiPath.FirstNode.Location, aiPath.FirstNode.NextMainNode.Location);

            ConsistFile conFile = new ConsistFile(ConsistFileName);
            CurveDurability = conFile.Train.Durability;   // Finds curve durability of consist based upon the value in consist file
            train.TcsParametersFileName = conFile.Train.TcsParametersFileName;

            // add wagons
            foreach (Wagon wagon in conFile.Train.Wagons)
            {
                string wagonFilePath = RouteFolder.ContentFolder.WagonFile(wagon.Folder, wagon.Name);
                if (wagon.IsEngine)
                    wagonFilePath = Path.ChangeExtension(wagonFilePath, ".eng");
                else if (wagon.IsEOT)
                {
                    string wagonFolder = Path.Combine(RouteFolder.ContentFolder.Folder, "trains\\orts_eot", wagon.Folder);
                    wagonFilePath = wagonFolder + @"\" + wagon.Name + ".eot";
                }

                if (!File.Exists(wagonFilePath))
                {
                    // First wagon is the player's loco and required, so issue a fatal error message
                    if (wagon == conFile.Train.Wagons[0])
                        Trace.TraceError("Player's locomotive {0} cannot be loaded in {1}", wagonFilePath, ConsistFileName);
                    Trace.TraceWarning($"Ignored missing {(wagon.IsEngine ? "engine" : "wagon")} {wagonFilePath} in consist {ConsistFileName}");
                    continue;
                }

                try
                {
                    TrainCar car = RollingStock.Load(train, wagonFilePath);
                    car.Flipped = wagon.Flip;
                    car.UiD = wagon.UiD;
                    if (MultiPlayerManager.IsMultiPlayer())
                        car.CarID = MultiPlayerManager.GetUserName() + " - " + car.UiD; //player's train is always named train 0.
                    else
                        car.CarID = "0 - " + car.UiD; //player's train is always named train 0.
                    if (car is EndOfTrainDevice endOfTrain)
                        train.EndOfTrainDevice = endOfTrain;
                    car.FreightAnimations?.Load(car as MSTSWagon, wagon.LoadDataList);

                    train.Length += car.CarLengthM;

                    if (ActivityFile != null && car is MSTSDieselLocomotive mstsDieselLocomotive)
                        mstsDieselLocomotive.DieselLevelL = mstsDieselLocomotive.MaxDieselLevelL * ActivityFile.Activity.Header.FuelDiesel / 100.0f;

                    if (ActivityFile != null && car is MSTSSteamLocomotive mstsSteamLocomotive)
                    {
                        mstsSteamLocomotive.CombinedTenderWaterVolumeUKG = (float)(Mass.Kilogram.ToLb(mstsSteamLocomotive.MaxLocoTenderWaterMassKG) / 10.0f) * ActivityFile.Activity.Header.FuelWater / 100.0f;
                        mstsSteamLocomotive.TenderCoalMassKG = mstsSteamLocomotive.MaxTenderCoalMassKG * ActivityFile.Activity.Header.FuelCoal / 100.0f;
                    }
                }
                catch (Exception error)
                {
                    // First wagon is the player's loco and required, so issue a fatal error message
                    if (wagon == conFile.Train.Wagons[0])
                        throw new FileLoadException(wagonFilePath, error);
                    Trace.WriteLine(new FileLoadException(wagonFilePath, error));
                }
            }// for each rail car

            train.CheckFreight();
            train.SetDistributedPowerUnitIds();

            train.PresetExplorerPath(aiPath);
            train.ControlMode = TrainControlMode.Explorer;

            TrackCircuitPartialPathRoute tempRoute = train.CalculateInitialTrainPosition();
            if (tempRoute.Count == 0)
            {
                throw new InvalidDataException("Player train original position not clear");
            }

            train.SetInitialTrainRoute(tempRoute);
            train.CalculatePositionOfCars();
            train.ResetInitialTrainRoute(tempRoute);

            train.CalculatePositionOfCars();
            Trains.Add(train);

            // Note the initial position to be stored by a Save and used in Menu.exe to calculate DistanceFromStartM 
            InitialTileX = Trains[0].FrontTDBTraveller.TileX + (Trains[0].FrontTDBTraveller.X / 2048);
            InitialTileZ = Trains[0].FrontTDBTraveller.TileZ + (Trains[0].FrontTDBTraveller.Z / 2048);

            PlayerLocomotive = InitialPlayerLocomotive();
            if ((conFile.Train.MaxVelocity == null) ||
                ((conFile.Train.MaxVelocity.A <= 0f) || (conFile.Train.MaxVelocity.A == 40f)))
                train.TrainMaxSpeedMpS = Math.Min((float)Route.SpeedLimit, ((MSTSLocomotive)PlayerLocomotive).MaxSpeedMpS);
            else
                train.TrainMaxSpeedMpS = Math.Min((float)Route.SpeedLimit, conFile.Train.MaxVelocity.A);

            float prevEQres = train.BrakeSystem.EqualReservoirPressurePSIorInHg;
            train.AITrainBrakePercent = 100; //<CSComment> This seems a tricky way for the brake modules to test if it is an AI train or not
            train.BrakeSystem.EqualReservoirPressurePSIorInHg = prevEQres; // The previous command modifies EQ reservoir pressure, causing issues with EP brake systems, so restore to prev value

            //            if ((PlayerLocomotive as MSTSLocomotive).EOTEnabled != MSTSLocomotive.EOTenabled.no)
            //                train.EOT = new EOT((PlayerLocomotive as MSTSLocomotive).EOTEnabled, false, train);

            return (train);
        }

        // used for activity and activity in explore mode; creates the train within the AITrain class
        private AITrain InitializeAPPlayerTrain()
        {
            string playerServiceFileName;
            ServiceFile srvFile;
            if (ActivityFile?.Activity?.Serial != -1)
            {
                playerServiceFileName = ActivityFile.Activity.PlayerServices.Name;
                srvFile = new ServiceFile(RouteFolder.ServiceFile(playerServiceFileName));
            }
            else
            {
                playerServiceFileName = Path.GetFileNameWithoutExtension(exploreConsist);
                srvFile = new ServiceFile(playerServiceFileName, playerServiceFileName, explorePath);
            }
            ConsistFileName = RouteFolder.ContentFolder.ConsistFile(srvFile.TrainConfig);
            PathFileName = RouteFolder.PathFile(srvFile.PathId);
            PlayerTraffics player_Traffic_Definition = ActivityFile.Activity.PlayerServices.PlayerTraffics;
            ServiceTraffics aPPlayer_Traffic_Definition = new ServiceTraffics(playerServiceFileName, player_Traffic_Definition);
            Services aPPlayer_Service_Definition = new Services(playerServiceFileName, player_Traffic_Definition);

            AITrain train = new AI(this).CreateAITrainDetail(aPPlayer_Service_Definition, aPPlayer_Traffic_Definition, srvFile, TimetableMode, true);
            train.Name = "PLAYER";
            train.Cars[0].Headlight = 0;
            OriginalPlayerTrain = train;
            train.Efficiency = 0.9f; // Forced efficiency, as considered most similar to human player
            TrackCircuitPartialPathRoute tempRoute = train.CalculateInitialTrainPosition();
            if (tempRoute.Count == 0)
            {
                throw new InvalidDataException("Player train original position not clear");
            }
            train.SetInitialTrainRoute(tempRoute);
            train.CalculatePositionOfCars();
            train.ResetInitialTrainRoute(tempRoute);

            train.CalculatePositionOfCars();
            train.TrainType = TrainType.AiPlayerDriven;
            Trains.Add(train);

            // Note the initial position to be stored by a Save and used in Menu.exe to calculate DistanceFromStartM 
            InitialTileX = Trains[0].FrontTDBTraveller.TileX + (Trains[0].FrontTDBTraveller.X / 2048);
            InitialTileZ = Trains[0].FrontTDBTraveller.TileZ + (Trains[0].FrontTDBTraveller.Z / 2048);

            PlayerLocomotive = InitialPlayerLocomotive();
            if (train.MaxVelocityA <= 0f || train.MaxVelocityA == 40f)
                train.TrainMaxSpeedMpS = Math.Min((float)Route.SpeedLimit, ((MSTSLocomotive)PlayerLocomotive).MaxSpeedMpS);
            else
                train.TrainMaxSpeedMpS = Math.Min((float)Route.SpeedLimit, train.MaxVelocityA);
            if (train.InitialSpeed > 0 && train.MovementState != AiMovementState.StationStop)
            {
                train.InitializeMoving();
                train.MovementState = AiMovementState.Braking;
            }
            else if (train.InitialSpeed == 0)
                train.InitializeBrakes();

            // process player passing paths as required
            if (SignalEnvironment.UseLocationPassingPaths)
            {
                TrackDirection orgDirection = (TrackDirection)(train.RearTDBTraveller != null ? (int)train.RearTDBTraveller.Direction.Reverse() : -2);
                _ = new TrackCircuitRoutePath(train.Path, orgDirection, 0, -1);
            }

            train.IsTilting = ConsistFileName.Contains("tilted", StringComparison.OrdinalIgnoreCase);

            //            if ((PlayerLocomotive as MSTSLocomotive).EOTEnabled != MSTSLocomotive.EOTenabled.no)
            //                train.EOT = new EOT((PlayerLocomotive as MSTSLocomotive).EOTEnabled, false, train);

            return train;
        }

        /// <summary>
        /// Set up trains based on info in the static consists listed in the activity file.
        /// </summary>
        private void InitializeStaticConsists()
        {
            if (ActivityFile?.Activity?.ActivityObjects == null)
                return;
            // for each static consist
            foreach (ActivityObject activityObject in ActivityFile.Activity.ActivityObjects)
            {
                try
                {
                    // construct train data
                    Train train = new Train
                    {
                        TrainType = TrainType.Static,
                        Name = "STATIC" + "-" + activityObject.ID
                    };
                    int consistDirection;
                    switch (activityObject.Direction)  // TODO, we don't really understand this
                    {
                        case 0:
                            consistDirection = 0;
                            break;  // reversed ( confirmed on L&PS route )
                        case 18:
                            consistDirection = 1;
                            break;  // forward ( confirmed on ON route )
                        case 131:
                            consistDirection = 1;
                            break; // forward ( confirmed on L&PS route )
                        default:
                            consistDirection = 1;
                            break;  // forward ( confirmed on L&PS route )
                    }
                    // FIXME: Where are TSectionDat and TDB from?
                    train.RearTDBTraveller = new Traveller(activityObject.Location);
                    if (consistDirection != 1)
                        train.RearTDBTraveller.ReverseDirection();
                    // add wagons in reverse order - ie first wagon is at back of train
                    // static consists are listed back to front in the activities, so we have to reverse the order, and flip the cars
                    // when we add them to ORTS
                    for (int iWagon = activityObject.TrainSet.Wagons.Count - 1; iWagon >= 0; --iWagon)
                    {
                        Wagon wagon = activityObject.TrainSet.Wagons[iWagon];
                        string wagonFilePath = RouteFolder.ContentFolder.WagonFile(wagon.Folder, wagon.Name);
                        if (wagon.IsEngine)
                            wagonFilePath = Path.ChangeExtension(wagonFilePath, ".eng");
                        else if (wagon.IsEOT)
                        {
                            string wagonFolder = Path.Combine(RouteFolder.ContentFolder.Folder, "trains\\orts_eot", wagon.Folder);
                            wagonFilePath = wagonFolder + @"\" + wagon.Name + ".eot";
                        }

                        if (!File.Exists(wagonFilePath))
                        {
                            Trace.TraceWarning($"Ignored missing {(wagon.IsEngine ? "engine" : "wagon")} {wagonFilePath} in activity definition {activityObject.TrainSet.Name}");
                            continue;
                        }

                        try // Load could fail if file has bad data.
                        {
                            TrainCar car = RollingStock.Load(train, wagonFilePath);
                            car.Flipped = !wagon.Flip;
                            car.UiD = wagon.UiD;
                            car.CarID = activityObject.ID + " - " + car.UiD;
                            if (car is EndOfTrainDevice endOfTrain)
                                train.EndOfTrainDevice = endOfTrain;
                            car.FreightAnimations?.Load(car as MSTSWagon, wagon.LoadDataList);
                        }
                        catch (Exception error)
                        {
                            Trace.WriteLine(new FileLoadException(wagonFilePath, error));
                        }
                    }// for each rail car

                    if (train.Cars.Count == 0)
                        return;

                    // in static consists, the specified location represents the middle of the last car, 
                    // our TDB traveller is always at the back of the last car so it needs to be repositioned
                    TrainCar lastCar = train.LastCar;
                    train.RearTDBTraveller.ReverseDirection();
                    train.RearTDBTraveller.Move(lastCar.CarLengthM / 2f);
                    train.RearTDBTraveller.ReverseDirection();

                    train.CalculatePositionOfCars();
                    train.InitializeBrakes();
                    train.CheckFreight();
                    train.ReverseFormation(false); // When using autopilot mode this is needed for correct working of train switching
                    train.SetDistributedPowerUnitIds();
                    bool validPosition = train.PostInit();
                    if (validPosition)
                        Trains.Add(train);
                }
                catch (Exception error)
                {
                    Trace.WriteLine(error);
                }
            }// for each train
        }

        private void SaveTrains(BinaryWriter outf)
        {
            if (PlayerLocomotive.Train != Trains[0])
            {
                for (int i = 1; i < Trains.Count; i++)
                {
                    if (PlayerLocomotive.Train == Trains[i])
                    {
                        Trains[i] = Trains[0];
                        Trains[0] = PlayerLocomotive.Train;
                        break;
                    }
                }
            }

            // do not save AI trains (done by AITrain)
            // do not save Timetable Trains (done by TTTrain through AITrain)

            foreach (Train train in Trains)
            {
                if (train.TrainType != TrainType.Ai && train.TrainType != TrainType.AiPlayerDriven && train.TrainType != TrainType.AiPlayerHosting &&
                    train.TrainType != TrainType.AiIncorporated && train.GetType() != typeof(TTTrain))
                {
                    outf.Write(0);
                    if (train is AITrain aiTrain && train.TrainType == TrainType.Static)
                        aiTrain.SaveBase(outf);
                    else
                        train.Save(outf);
                }
                else if (train.TrainType == TrainType.AiPlayerDriven || train.TrainType == TrainType.AiPlayerHosting)
                {
                    outf.Write(-2);
                    AI.SaveAutopil(train, outf);
                }
            }
            outf.Write(-1);

        }

        //================================================================================================//
        //
        // Restore trains
        //

        private void RestoreTrains(BinaryReader inf)
        {

            Trains = new TrainList(this);

            int trainType = inf.ReadInt32();
            while (trainType != -1)
            {
                if (trainType >= 0)
                    Trains.Add(new Train(inf));
                else if (trainType == -2)                   // Autopilot mode
                {
                    _ = new AI(this, inf, true);
                }
                trainType = inf.ReadInt32();
            }

            // find player train
            foreach (Train thisTrain in Trains)
            {
                if (thisTrain.TrainType == TrainType.Player
                    || thisTrain.TrainType == TrainType.AiPlayerDriven || thisTrain.TrainType == TrainType.AiPlayerHosting)
                {
                    TrainDictionary.Add(thisTrain.Number, thisTrain);
                    NameDictionary.Add(thisTrain.Name, thisTrain);
                    // restore signal references depending on state
                    if (thisTrain.ControlMode == TrainControlMode.Explorer)
                    {
                        thisTrain.RestoreExplorerMode();
                    }
                    else if (thisTrain.ControlMode == TrainControlMode.Manual)
                    {
                        thisTrain.RestoreManualMode();
                    }
                    else if (thisTrain.TrainType == TrainType.Player)
                    {
                        thisTrain.InitializeSignals(true);
                    }
                }
            }
        }

        /// <summary>
        ///  Get Autogenerated train by number
        /// </summary>
        /// <param name="reqNumber"></param>
        /// <returns></returns>

        public TTTrain GetAutoGenTTTrainByNumber(int reqNumber)
        {
            TTTrain returnTrain = null;
            if (AutoGenDictionary.ContainsKey(reqNumber))
            {
                AITrain tempTrain = AutoGenDictionary[reqNumber];
                returnTrain = tempTrain as TTTrain;
                returnTrain.AI.AutoGenTrains.Remove(tempTrain);
                AutoGenDictionary.Remove(reqNumber);
                returnTrain.RoutedBackward = new Train.TrainRouted(returnTrain, 1);
                returnTrain.RoutedForward = new Train.TrainRouted(returnTrain, 0);
            }
            return (returnTrain);
        }

        /// <summary>
        /// The front end of a railcar is at MSTS world coordinates x1,y1,z1
        /// The other end is at x2,y2,z2
        /// Return a rotation and translation matrix for the center of the railcar.
        /// </summary>
        public static Matrix XNAMatrixFromMSTSCoordinates(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            // translate 1st coordinate to be relative to 0,0,0
            float dx = (float)(x1 - x2);
            float dy = (float)(y1 - y2);
            float dz = (float)(z1 - z2);

            // compute the rotational matrix  
            float length = (float)Math.Sqrt(dx * dx + dz * dz + dy * dy);
            float run = (float)Math.Sqrt(dx * dx + dz * dz);
            // normalize to coordinate to a length of one, ie dx is change in x for a run of 1
            if (length != 0)    // Avoid zero divide
            {
                dx /= length;
                dy /= length;   // ie if it is tilted back 5 degrees, this is sin 5 = 0.087
                run /= length;  //                              and   this is cos 5 = 0.996
                dz /= length;
            }
            else
            {                   // If length is zero all elements of its calculation are zero. Since dy is a sine and is zero,
                run = 1f;       // run is therefore 1 since it is cosine of the same angle?  See comments above.
            }


            // setup matrix values

            Matrix xnaTilt = new Matrix(1, 0, 0, 0,
                                     0, run, dy, 0,
                                     0, -dy, run, 0,
                                     0, 0, 0, 1);

            Matrix xnaRotation = new Matrix(dz, 0, dx, 0,
                                            0, 1, 0, 0,
                                            -dx, 0, dz, 0,
                                            0, 0, 0, 1);

            Matrix xnaLocation = Matrix.CreateTranslation((x1 + x2) / 2f, (y1 + y2) / 2f, -(z1 + z2) / 2f);
            MatrixExtension.Multiply(xnaTilt, xnaRotation, out Matrix result);
            return MatrixExtension.Multiply(result, xnaLocation);
            //            return xnaTilt * xnaRotation * xnaLocation;
        }

        public void UncoupleBehind(int carPosition)
        {
            // check on car position in case of mouse jitter
            if (carPosition <= PlayerLocomotive.Train.Cars.Count - 1)
                UncoupleBehind(PlayerLocomotive.Train.Cars[carPosition], true);
        }

        public void UncoupleBehind(TrainCar car, bool keepFront)
        {
            Train train = car.Train;

            if (MultiPlayerManager.IsMultiPlayer() && !MultiPlayerManager.TrainOK2Decouple(Confirmer, train))
                return;
            int i = 0;
            while (train.Cars[i] != car)
                ++i;  // it can't happen that car isn't in car.Train
            if (i == train.Cars.Count - 1)
                return;  // can't uncouple behind last car
            ++i;

            TrainCar lead = train.LeadLocomotive;
            Train train2;
            if (train.IncorporatedTrainNo == -1)
            {
                train2 = new Train(train);
                Trains.Add(train2);
            }
            else
            {
                train2 = TrainDictionary[train.IncorporatedTrainNo];
            }

            if (MultiPlayerManager.IsMultiPlayer() && !(train2 is AITrain))
                train2.ControlMode = TrainControlMode.Explorer;
            // Player locomotive is in first or in second part of train?
            int j = 0;
            while (train.Cars[j] != PlayerLocomotive && j < i)
                j++;

            // This is necessary, because else we had to create an AI train and not a train when in autopilot mode
            if ((train.IsActualPlayerTrain && j >= i) || !keepFront)
            {
                // Player locomotive in second part of train, move first part of cars to the new train
                for (int k = 0; k < i; ++k)
                {
                    TrainCar newcar = train.Cars[k];
                    train2.Cars.Add(newcar);
                    newcar.Train = train2;
                }

                // and drop them from the old train
                for (int k = i - 1; k >= 0; --k)
                {
                    train.Cars.RemoveAt(k);
                }

                train.FirstCar.CouplerSlackM = 0;
                if (train.LeadLocomotiveIndex >= 0)
                    train.LeadLocomotiveIndex -= i;
            }
            else
            {
                // move rest of cars to the new train

                for (int k = i; k < train.Cars.Count; ++k)
                {
                    TrainCar newcar = train.Cars[k];
                    train2.Cars.Add(newcar);
                    newcar.Train = train2;
                }

                // and drop them from the old train
                for (int k = train.Cars.Count - 1; k >= i; --k)
                {
                    train.Cars.RemoveAt(k);
                }

                train.LastCar.CouplerSlackM = 0;

            }

            // and fix up the travellers
            if (train.IsActualPlayerTrain && j >= i || !keepFront)
            {
                train2.FrontTDBTraveller = new Traveller(train.FrontTDBTraveller);
                train.CalculatePositionOfCars();
                train2.RearTDBTraveller = new Traveller(train.FrontTDBTraveller);
                train2.CalculatePositionOfCars();  // fix the front traveller
                train.DistanceTravelledM -= train2.Length;
            }
            else
            {
                train2.RearTDBTraveller = new Traveller(train.RearTDBTraveller);
                train2.CalculatePositionOfCars();  // fix the front traveller
                train.RepositionRearTraveller();    // fix the rear traveller
            }

            train.ActivityClearingDistanceM = train.Cars.Count < Train.StandardTrainMinCarNo ? Train.ShortClearingDistanceM : Train.StandardClearingDistanceM;
            train2.ActivityClearingDistanceM = train2.Cars.Count < Train.StandardTrainMinCarNo ? Train.ShortClearingDistanceM : Train.StandardClearingDistanceM;


            train.UncoupledFrom = train2;
            train2.UncoupledFrom = train;
            train2.SpeedMpS = train.SpeedMpS;
            train2.Cars[0].BrakeSystem.FrontBrakeHoseConnected = false;
            train2.AITrainDirectionForward = train.AITrainDirectionForward;

            // It is an action, not just a simple copy, thus don't do it if the train is driven by the player:
            if (PlayerLocomotive == null)
                train2.AITrainBrakePercent = train.AITrainBrakePercent;

            if (train.IncorporatedTrainNo != -1)
            {
                train2.AITrainBrakePercent = 100;
                train2.TrainType = TrainType.Ai;
                train.IncorporatedTrainNo = -1;
                train2.MUDirection = MidpointDirection.Forward;
            }
            else
                train2.TrainType = TrainType.Static;
            train2.LeadLocomotive = null;
            if ((train.TrainType == TrainType.Ai || train.TrainType == TrainType.AiPlayerHosting) && train2.TrainType == TrainType.Static)
                train2.InitializeBrakes();
            else
            {
                train2.Cars[0].BrakeSystem.PropagateBrakePressure(5);
                foreach (MSTSWagon wagon in train2.Cars)
                    wagon.MSTSBrakeSystem.Update(5);
            }
            bool inPath;

            if ((train.IsActualPlayerTrain && j >= i) || !keepFront)
            {
                train.TemporarilyRemoveFromTrack();
                inPath = train2.UpdateTrackActionsUncoupling(false);
                train.UpdateTrackActionsUncoupling(false);
            }
            else
            {
                train.UpdateTrackActionsUncoupling(true);
                inPath = train2.UpdateTrackActionsUncoupling(false);
            }
            if (!inPath && train2.TrainType == TrainType.Ai)
            // Out of path, degrade to static
            {
                train2.TrainType = TrainType.Static;
                ((AITrain)train2).AI.TrainsToRemoveFromAI.Add((AITrain)train2);
            }
            if (train2.TrainType == TrainType.Ai)
            {
                // Move reversal point under train if there is one in the section where the train is
                if (train2.PresentPosition[Direction.Forward].TrackCircuitSectionIndex ==
                                    train2.TCRoute.TCRouteSubpaths[train2.TCRoute.ActiveSubPath][^1].TrackCircuitSection.Index &&
                    train2.TCRoute.ActiveSubPath < train2.TCRoute.TCRouteSubpaths.Count - 1)
                {
                    train2.TCRoute.ReversalInfo[train2.TCRoute.ActiveSubPath].ReverseReversalOffset = train2.PresentPosition[Direction.Forward].Offset - 10f;
                    train2.AuxActionsContainer.MoveAuxActionAfterReversal(train2);
                }
                else if ((train.IsActualPlayerTrain && j >= i) || !keepFront)
                {
                    train2.AuxActionsContainer.MoveAuxAction(train2);
                }
                ((AITrain)train2).ResetActions(true);
            }
            if (MultiPlayerManager.IsMultiPlayer())
            {
                if (!(train is AITrain))
                    train.ControlMode = TrainControlMode.Explorer;
                if (!(train2 is AITrain))
                    train2.ControlMode = TrainControlMode.Explorer;
            }

            if (MultiPlayerManager.IsMultiPlayer() && !MultiPlayerManager.IsServer())
            {
                //add the new train to a list of uncoupled trains, handled specially
                if (PlayerLocomotive != null && PlayerLocomotive.Train == train)
                    MultiPlayerManager.Instance().AddUncoupledTrains(train2);
            }


            train.CheckFreight();
            train.SetDistributedPowerUnitIds();
            train.ReinitializeEOT();
            train2.CheckFreight();
            train2.SetDistributedPowerUnitIds();
            train2.ReinitializeEOT();

            train.Update(0);   // stop the wheels from moving etc
            train2.Update(0);  // stop the wheels from moving etc

            car.SignalEvent(TrainEvent.Uncouple);
            // TODO which event should we fire
            //car.CreateEvent(62);  these are listed as alternate events
            //car.CreateEvent(63);
            if (MultiPlayerManager.IsMultiPlayer())
            {
                MultiPlayerManager.Notify((new MSGUncouple(train, train2, MultiPlayerManager.GetUserName(), car.CarID, PlayerLocomotive)).ToString());
            }
            if (Confirmer != null && IsReplaying)
                Confirmer.Confirm(CabControl.Uncouple, train.LastCar.CarID);
            if (AI != null)
                AI.TrainListChanged = true;
            if (train2.TrainType == TrainType.Static && (train.TrainType == TrainType.Player || train.TrainType == TrainType.AiPlayerDriven))
            {
                // check if detached on turntable or transfertable
                if (ActiveMovingTable != null)
                    ActiveMovingTable.CheckTrainOnMovingTable(train2);
            }
        }

        /// <summary>
        /// Performs first part of player train switch
        /// </summary>
        private void StartSwitchPlayerTrain()
        {
            if (TrainSwitcher.SelectedAsPlayer != null && !TrainSwitcher.SelectedAsPlayer.IsActualPlayerTrain)
            {
                Train selectedAsPlayer = TrainSwitcher.SelectedAsPlayer;
                bool oldTrainReverseFormation = false;
                bool newTrainReverseFormation = false;
                if (PlayerLocomotive.Train is AITrain aiPlayerTrain && !PlayerLocomotive.Train.IsPathless)
                {
                    if (aiPlayerTrain.ControlMode == TrainControlMode.Manual)
                        TrainSwitcher.SuspendOldPlayer = true; // force suspend state to avoid disappearing of train;
                    if (TrainSwitcher.SuspendOldPlayer &&
                        (aiPlayerTrain.SpeedMpS < -0.025 || aiPlayerTrain.SpeedMpS > 0.025 || aiPlayerTrain.IsMoving()))
                    {
                        Confirmer.Message(ConfirmLevel.Warning, Catalog.GetString("Train can't be suspended with speed not equal 0"));
                        TrainSwitcher.SuspendOldPlayer = false;
                        TrainSwitcher.ClickedSelectedAsPlayer = false;
                        return;
                    }
                    if (aiPlayerTrain.TrainType == TrainType.AiPlayerDriven)
                    {
                        // it must be autopiloted first
                        aiPlayerTrain.SwitchToAutopilotControl();
                    }
                    // and now switch!
                    aiPlayerTrain.TrainType = TrainType.Ai;
                    AI.AITrains.Add(aiPlayerTrain);
                    if (TrainSwitcher.SuspendOldPlayer)
                    {
                        aiPlayerTrain.MovementState = AiMovementState.Suspended;
                        if (aiPlayerTrain.ValidRoute[0] != null && aiPlayerTrain.PresentPosition[Direction.Forward].RouteListIndex != -1 &&
                            aiPlayerTrain.ValidRoute[0].Count > aiPlayerTrain.PresentPosition[Direction.Forward].RouteListIndex + 1)
                            SignalEnvironment.BreakDownRoute(aiPlayerTrain.ValidRoute[0][aiPlayerTrain.PresentPosition[Direction.Forward].RouteListIndex + 1].TrackCircuitSection.Index,
                               aiPlayerTrain.RoutedForward);
                        TrainSwitcher.SuspendOldPlayer = false;
                    }
                }
                else if (selectedAsPlayer.TrainType == TrainType.AiIncorporated && selectedAsPlayer.IncorporatingTrain.IsPathless)
                {
                    // the former static train disappears now and becomes part of the other train. TODO; also wagons must be moved.
                    Train dyingTrain = PlayerLocomotive.Train;

                    // move all cars to former incorporated train
                    for (int k = 0; k < dyingTrain.Cars.Count; ++k)
                    {
                        TrainCar newcar = dyingTrain.Cars[k];
                        selectedAsPlayer.Cars.Add(newcar);
                        newcar.Train = selectedAsPlayer;
                    }

                    // and drop them from the old train
                    for (int k = dyingTrain.Cars.Count - 1; k >= 0; --k)
                    {
                        dyingTrain.Cars.RemoveAt(k);
                    }

                    // and fix up the travellers
                    selectedAsPlayer.RearTDBTraveller = new Traveller(dyingTrain.RearTDBTraveller);
                    selectedAsPlayer.FrontTDBTraveller = new Traveller(dyingTrain.FrontTDBTraveller);
                    // are following lines needed?
                    //                       selectedAsPlayer.CalculatePositionOfCars(0);  // fix the front traveller
                    //                       selectedAsPlayer.RepositionRearTraveller();    // fix the rear traveller

                    selectedAsPlayer.ActivityClearingDistanceM = dyingTrain.ActivityClearingDistanceM;

                    selectedAsPlayer.SpeedMpS = dyingTrain.SpeedMpS;
                    selectedAsPlayer.AITrainDirectionForward = dyingTrain.AITrainDirectionForward;

                    selectedAsPlayer.AITrainBrakePercent = 100;
                    selectedAsPlayer.TrainType = TrainType.Ai;
                    selectedAsPlayer.MUDirection = MidpointDirection.Forward;

                    selectedAsPlayer.LeadLocomotive = null;
                    selectedAsPlayer.Cars[0].BrakeSystem.PropagateBrakePressure(5);
                    foreach (MSTSWagon wagon in selectedAsPlayer.Cars)
                        wagon.MSTSBrakeSystem.Update(5);

                    // and now let the former static train die
                    dyingTrain.RemoveFromTrack();
                    dyingTrain.ClearDeadlocks();
                    Trains.Remove(dyingTrain);
                    TrainDictionary.Remove(dyingTrain.Number);
                    NameDictionary.Remove(dyingTrain.Name);

                    bool inPath = selectedAsPlayer.UpdateTrackActionsUncoupling(false);

                    if (!inPath && selectedAsPlayer.TrainType == TrainType.Ai)
                    // Out of path, degrade to static
                    {
                        selectedAsPlayer.TrainType = TrainType.Static;
                        ((AITrain)selectedAsPlayer).AI.TrainsToRemoveFromAI.Add((AITrain)selectedAsPlayer);
                    }
                    if (selectedAsPlayer.TrainType == TrainType.Ai)
                    {
                        ((AITrain)selectedAsPlayer).AI.TrainListChanged = true;
                        // Move reversal point under train if there is one in the section where the train is
                        if (selectedAsPlayer.PresentPosition[Direction.Forward].TrackCircuitSectionIndex ==
                                            selectedAsPlayer.TCRoute.TCRouteSubpaths[selectedAsPlayer.TCRoute.ActiveSubPath][^1].TrackCircuitSection.Index &&
                            selectedAsPlayer.TCRoute.ActiveSubPath < selectedAsPlayer.TCRoute.TCRouteSubpaths.Count - 1)
                        {
                            selectedAsPlayer.TCRoute.ReversalInfo[selectedAsPlayer.TCRoute.ActiveSubPath].ReverseReversalOffset = selectedAsPlayer.PresentPosition[Direction.Forward].Offset - 10f;
                            selectedAsPlayer.AuxActionsContainer.MoveAuxActionAfterReversal(selectedAsPlayer);
                        }
                        ((AITrain)selectedAsPlayer).ResetActions(true);
                    }
                    if (MultiPlayerManager.IsMultiPlayer() && !MultiPlayerManager.IsServer())
                    {
                        selectedAsPlayer.ControlMode = TrainControlMode.Explorer;
                        //add the new train to a list of uncoupled trains, handled specially
                        if (PlayerLocomotive != null)
                            MultiPlayerManager.Instance().AddUncoupledTrains(selectedAsPlayer);
                    }


                    selectedAsPlayer.CheckFreight();
                    selectedAsPlayer.SetDistributedPowerUnitIds(true);

                    selectedAsPlayer.Update(0);  // stop the wheels from moving etc
                    TrainSwitcher.PickedTrainFromList = selectedAsPlayer;
                    TrainSwitcher.ClickedTrainFromList = true;


                }
                else
                {
                    // this was a static train before
                    Train playerTrain = PlayerLocomotive.Train;
                    if (playerTrain != null)
                    {
                        if (playerTrain.SpeedMpS < -0.1 || playerTrain.SpeedMpS > 0.1)
                        {
                            Confirmer.Message(ConfirmLevel.Warning, Catalog.GetString("To return to static train speed must be = 0"));
                            TrainSwitcher.SuspendOldPlayer = false;
                            TrainSwitcher.ClickedSelectedAsPlayer = false;
                            return;
                        }
                        if (playerTrain.ValidRoute[0] != null && playerTrain.ValidRoute[0].Count > playerTrain.PresentPosition[Direction.Forward].RouteListIndex + 1)
                            SignalEnvironment.BreakDownRoute(playerTrain.ValidRoute[0][playerTrain.PresentPosition[Direction.Forward].RouteListIndex + 1].TrackCircuitSection.Index,
                            playerTrain.RoutedForward);
                        if (playerTrain.ValidRoute[1] != null && playerTrain.ValidRoute[1].Count > playerTrain.PresentPosition[Direction.Backward].RouteListIndex + 1)
                            SignalEnvironment.BreakDownRoute(playerTrain.ValidRoute[1][playerTrain.PresentPosition[Direction.Backward].RouteListIndex + 1].TrackCircuitSection.Index,
                            playerTrain.RoutedBackward);
                        playerTrain.ControlMode = TrainControlMode.Undefined;
                        playerTrain.TrainType = TrainType.Static;
                        playerTrain.SpeedMpS = 0;
                        foreach (TrainCar car in playerTrain.Cars)
                            car.SpeedMpS = 0;
                        playerTrain.CheckFreight();
                        playerTrain.SetDistributedPowerUnitIds();
                        playerTrain.InitializeBrakes();
                    }
                }
                Train oldPlayerTrain = PlayerLocomotive.Train;
                if (selectedAsPlayer.TrainType != TrainType.Static)
                {
                    AITrain playerTrain = selectedAsPlayer as AITrain;
                    if (!(playerTrain.TrainType == TrainType.AiIncorporated && playerTrain.IncorporatingTrain == PlayerLocomotive.Train))
                    {
                        PlayerLocomotive = SetPlayerLocomotive(playerTrain);
                        if (oldPlayerTrain != null)
                            oldPlayerTrain.LeadLocomotiveIndex = -1;
                    }
                }
                else
                {
                    Train pathlessPlayerTrain = selectedAsPlayer;
                    pathlessPlayerTrain.IsPathless = true;
                    PlayerLocomotive = SetPlayerLocomotive(pathlessPlayerTrain);
                    if (oldPlayerTrain != null)
                        oldPlayerTrain.LeadLocomotiveIndex = -1;
                }
                playerSwitchOngoing = true;
                if (MultiPlayerManager.IsMultiPlayer())
                {
                    MultiPlayerManager.Notify(new MSGPlayerTrainSw(MultiPlayerManager.GetUserName(), PlayerLocomotive.Train, PlayerLocomotive.Train.Number, oldTrainReverseFormation, newTrainReverseFormation).ToString());
                }
            }
            else
            {
                TrainSwitcher.ClickedSelectedAsPlayer = false;
                AI.TrainListChanged = true;
            }
        }

        private void CompleteSwitchPlayerTrain()
        {
            if (PlayerLocomotive.Train.TrainType != TrainType.Static)
            {
                AI.AITrains.Remove(PlayerLocomotive.Train as AITrain);
                if ((PlayerLocomotive.Train as AITrain).MovementState == AiMovementState.Suspended)
                {
                    PlayerLocomotive.Train.Reinitialize();
                    (PlayerLocomotive.Train as AITrain).MovementState = Math.Abs(PlayerLocomotive.Train.SpeedMpS) <= MaxStoppedMpS ?
                        AiMovementState.Init : AiMovementState.Braking;
                }
                (PlayerLocomotive.Train as AITrain).SwitchToPlayerControl();
            }
            else
            {
                PlayerLocomotive.Train.CreatePathlessPlayerTrain();
            }
            OnPlayerLocomotiveChanged();
            playerSwitchOngoing = false;
            TrainSwitcher.ClickedSelectedAsPlayer = false;
            AI.TrainListChanged = true;
        }

        /// <summary>
        /// Finds train to restart
        /// </summary>
        internal void RestartWaitingTrain(RestartWaitingTrain restartWaitingTrain)
        {
            AITrain trainToRestart = null;
            foreach (Train train in TrainDictionary.Values)
            {
                if (train is AITrain aiTrain && string.Equals(train.Name, restartWaitingTrain.WaitingTrainToRestart, StringComparison.OrdinalIgnoreCase))
                {
                    if (restartWaitingTrain.WaitingTrainStartingTime == -1 || (restartWaitingTrain.WaitingTrainStartingTime == aiTrain.StartTime))
                    {
                        trainToRestart = aiTrain;
                        trainToRestart.RestartWaitingTrain(restartWaitingTrain);
                        return;
                    }
                }
            }
            if (trainToRestart == null)
                Trace.TraceWarning("Train {0} to restart not found", restartWaitingTrain.WaitingTrainToRestart);
        }

        /// <summary>
        /// Derive log-file name from route path and activity name
        /// </summary>
        public string DeriveLogFile(string appendix)
        {
            const int maxLogFiles = 2;
            StringBuilder logfile = new StringBuilder();

            logfile.Append(RouteName);

            logfile.Append(string.IsNullOrEmpty(ActivityFileName) ? "_explorer" : "_" + ActivityFileName);

            logfile.Append(appendix);

            string logfileName = Path.Combine(RuntimeInfo.UserDataFolder, Path.ChangeExtension(logfile.ToString(), "csv"));

            int logCount = 0;

            while (File.Exists(logfileName) && logCount < maxLogFiles)
            {
                logfileName = Path.Combine(RuntimeInfo.UserDataFolder, Path.ChangeExtension($"{logfile}{logCount:00}", "csv"));
                logCount++;
            }

            if (logCount >= maxLogFiles)
            {
                logfileName = string.Empty;
            }
            return logfileName;
        }

        // Prefix with the activity filename so that, when resuming from the Menu.exe, we can quickly find those Saves 
        // that are likely to match the previously chosen route and activity.
        // Append the current date and time, so that each file is unique.
        // This is the "sortable" date format, ISO 8601, but with "." in place of the ":" which are not valid in filenames.
        public string SaveFileName => $"{(ActivityFile != null ? ActivityFileName : (!string.IsNullOrEmpty(TimetableFileName) ? $"{RouteFolder.RouteName} {TimetableFileName}" : RouteFolder.RouteName))} {(MultiPlayerManager.IsMultiPlayer() && MultiPlayerManager.IsServer() ? "$Multipl$ " : string.Empty)}{DateTime.Now:yyyy'-'MM'-'dd HH'.'mm'.'ss}";

        internal void OnAllowedSpeedRaised(Train train)
        {
            AllowedSpeedRaised?.Invoke(train, EventArgs.Empty);
        }

        internal void OnPlayerLocomotiveChanged()
        {
            PlayerLocomotiveChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void OnPlayerTrainChanged(Train oldTrain, Train newTrain)
        {
            PlayerTrainChanged?.Invoke(this, new PlayerTrainChangedEventArgs(oldTrain, newTrain));
        }

        internal void OnRequestTTDetachWindow()
        {
            EventHandler requestTTDetachWindow = RequestTTDetachWindow;
            requestTTDetachWindow(this, EventArgs.Empty);
        }

        private bool OnQueryCarViewerLoaded(TrainCar car)
        {
            QueryCarViewerLoadedEventArgs query = new QueryCarViewerLoadedEventArgs(car);
            QueryCarViewerLoaded?.Invoke(this, query);
            return query.Loaded;
        }

        // Override User settings with activity creator settings if present in INCLUDE file
        private static void OverrideUserSettings(UserSettings setting, ORActivity activitySettings)
        {
            if (activitySettings.IsActivityOverride)
            {
                Trace.Write("\n------------------------------------------------------------------------------------------------");
                Trace.Write("\nThe following Option settings have been temporarily set by this activity \r\n(no permanent changes have been made to your settings):");

                // General TAB 

                setting.RetainersOnAllCars = activitySettings.Options.RetainersOnAllCars == 1;
                Trace.Write($"\n{"Retainers on all cars",-40}={setting.RetainersOnAllCars,6}");

                setting.GraduatedRelease = activitySettings.Options.GraduatedBrakeRelease == 1;
                Trace.Write($"\n{"Graduated Brake Release",-40}={setting.GraduatedRelease,6}");

                setting.SpeedControl = activitySettings.Options.SoundSpeedControl == 1;
                Trace.Write($"\n{"Sound speed control",-40}={setting.SpeedControl,6}");

                // Simulation TAB
                setting.NoForcedRedAtStationStops = activitySettings.Options.ForcedRedAtStationStops == 0; // Note this parameter is reversed in its logic to others.
                Trace.Write($"\n{"Forced Red at Station Stops",-40}={setting.NoForcedRedAtStationStops,6}");

                setting.UseLocationPassingPaths = activitySettings.Options.UseLocationPassingPaths == 1;
                Trace.Write($"\n{"Location Based Passing Paths",-40}={setting.UseLocationPassingPaths,6}");

                setting.UseAdvancedAdhesion = activitySettings.Options.UseAdvancedAdhesion == 1;
                Trace.Write($"\n{"Use Advanced Adhesion",-40}={setting.UseAdvancedAdhesion,6}");

                setting.BreakCouplers = activitySettings.Options.BreakCouplers == 1;
                Trace.Write($"\n{"Break Couplers",-40}={setting.BreakCouplers,6}");

                setting.CurveSpeedDependent = activitySettings.Options.CurveSpeedDependent == 1;
                Trace.Write($"\n{"Curve Speed Dependent",-40}={setting.CurveSpeedDependent,6}");

                setting.HotStart = activitySettings.Options.HotStart == 1;
                Trace.Write($"\n{"Hot Start",-40}={setting.HotStart,6}");

                Trace.Write($"\n{"Simple Control/Physics",-40}={activitySettings.Options.SimpleControlPhysics == 1,6}");

                // Data Logger TAB
                Trace.Write($"\n{"Verbose Configuration Messages",-40}={activitySettings.Options.VerboseConfigurationMessages == 1,6}");

                // Experimental TAB
                if (activitySettings.Options.AdhesionFactor > 0)
                {
                    setting.AdhesionFactor = activitySettings.Options.AdhesionFactor;
                    setting.AdhesionFactor = MathHelper.Clamp(setting.AdhesionFactor, 10, 200);
                    Trace.Write($"\n{"Adhesion Factor Correction",-40}={setting.AdhesionFactor,6}");
                }

                if (activitySettings.Options.AdhesionFactorChange > 0)
                {
                    setting.AdhesionFactorChange = activitySettings.Options.AdhesionFactorChange;
                    setting.AdhesionFactorChange = MathHelper.Clamp(setting.AdhesionFactorChange, 0, 100);
                    Trace.Write($"\n{"Adhesion Factor Change",-40}={setting.AdhesionFactorChange,6}");
                }

                setting.AdhesionProportionalToWeather = activitySettings.Options.AdhesionProportionalToWeather == 1;
                Trace.Write($"\n{"Adhesion Proportional to Weather",-40}={setting.AdhesionProportionalToWeather,6}");

                if (activitySettings.Options.ActivityRandomization > 0)
                {
                    setting.ActRandomizationLevel = activitySettings.Options.ActivityRandomization;
                    setting.ActRandomizationLevel = MathHelper.Clamp(setting.ActRandomizationLevel, 0, 3);
                    Trace.Write($"\n{"Activity Randomization",-40}={setting.ActRandomizationLevel,6}");
                }

                if (activitySettings.Options.ActivityWeatherRandomization > 0)
                {
                    setting.ActWeatherRandomizationLevel = activitySettings.Options.ActivityWeatherRandomization;
                    setting.ActWeatherRandomizationLevel = MathHelper.Clamp(setting.ActWeatherRandomizationLevel, 0, 3);
                    Trace.Write($"\n{"Activity Weather Randomization",-40}={setting.ActWeatherRandomizationLevel,6}");
                }

                if (activitySettings.Options.SuperElevationLevel > 0)
                {
                    setting.UseSuperElevation = activitySettings.Options.SuperElevationLevel;
                    setting.UseSuperElevation = MathHelper.Clamp(setting.UseSuperElevation, 0, 10);
                    Trace.Write($"\n{"Super elevation - level",-40}={setting.UseSuperElevation}");
                }

                if (activitySettings.Options.SuperElevationMinimumLength > 0)
                {
                    setting.SuperElevationMinLen = activitySettings.Options.SuperElevationMinimumLength;
                    setting.SuperElevationMinLen = MathHelper.Clamp(setting.SuperElevationMinLen, 50, 1000000);
                    Trace.Write($"\n{"Super elevation - minimum length",-40}={setting.SuperElevationMinLen,6}");
                }

                if (activitySettings.Options.SuperElevationGauge > 0)
                {
                    setting.SuperElevationGauge = activitySettings.Options.SuperElevationGauge;
                    setting.SuperElevationGauge = MathHelper.Clamp(setting.SuperElevationGauge, 300, 2500);
                    Trace.Write($"\n{"Super elevation - gauge",-40}={setting.SuperElevationGauge,6}");
                }

                Trace.Write("\n------------------------------------------------------------------------------------------------");
            }
        }

        public void InitializeAiPlayerHosting()
        {
            Trains[0].LeadLocomotive = null;
            Trains[0].LeadLocomotiveIndex = -1;
        }
    } // Simulator
}
