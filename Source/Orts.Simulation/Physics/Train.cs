// COPYRIGHT 2013 by the Open Rails project.
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

/* TRAINS
 * 
 * Contains code to represent a train as a list of TrainCars and to handle the physics of moving
 * the train through the Track Database.
 * 
 * A train has:
 *  - a list of TrainCars 
 *  - a front and back position in the TDB ( represented by TDBTravellers )
 *  - speed
 *  - MU signals that are relayed from player locomtive to other locomotives and cars such as:
 *      - direction
 *      - throttle percent
 *      - brake percent  ( TODO, this should be changed to brake pipe pressure )
 *      
 *  Individual TrainCars provide information on friction and motive force they are generating.
 *  This is consolidated by the train class into overall movement for the train.
 */

// Compiler flags for debug print-out facilities
// #define DEBUG_SIGNALPASS

// Debug Calculation of Aux Tender operation
// #define DEBUG_AUXTENDER

// Debug for calculation of speed forces
// #define DEBUG_SPEED_FORCES

// Debug for calculation of Advanced coupler forces
// #define DEBUG_COUPLER_FORCES

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.MultiPlayer;
using Orts.Simulation.AIs;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;

namespace Orts.Simulation.Physics
{
    public partial class Train
    {
        private const int tileSize = 2048;

        public List<TrainCar> Cars { get; } = new List<TrainCar>();           // listed front to back
        public int Number { get; internal set; }
        public string Name { get; internal set; }
        public static int TotalNumber { get; private set; } = 1; // start at 1 (0 is reserved for player train)

        public TrainCar FirstCar
        {
            get
            {
                return Cars[0];
            }
        }
        public TrainCar LastCar
        {
            get
            {
                return Cars[Cars.Count - 1];
            }
        }
        public Traveller RearTDBTraveller;               // positioned at the back of the last car in the train
        public Traveller FrontTDBTraveller;              // positioned at the front of the train by CalculatePositionOfCars
        public float Length;                             // length of train from FrontTDBTraveller to RearTDBTraveller
        public float MassKg;                             // weight of the train
        public float SpeedMpS;                           // meters per second +ve forward, -ve when backing
        float LastSpeedMpS;                              // variable to remember last speed used for projected speed
        public SmoothedData AccelerationMpSpS = new SmoothedData(); // smoothed acceleration data
        public float ProjectedSpeedMpS;                  // projected speed
        public float LastReportedSpeed;

        public Train UncoupledFrom;                      // train not to coupled back onto
        public float TotalCouplerSlackM;
        public float MaximumCouplerForceN;        
        public int CouplersPulled { get; private set; }     // Count of number of couplings being stretched (pulled)
        public int CouplersPushed { get; private set; }     // Count of number of couplings being compressed (pushed)

        public int LeadLocomotiveIndex = -1;
        public bool IsFreight;                           // has at least one freight car
        public int PassengerCarsNumber = 0;              // Number of passenger cars
        public float SlipperySpotDistanceM;              // distance to extra slippery part of track
        public float SlipperySpotLengthM;

        public float WagonCoefficientFriction = 0.35f; // Initialise coefficient of Friction for wagons - 0.35 for dry rails, 0.1 - 0.25 for wet rails
        public float LocomotiveCoefficientFriction = 0.35f; // Initialise coefficient of Friction for locomotives - 0.5 for dry rails, 0.1 - 0.25 for wet rails

        // These signals pass through to all cars and locomotives on the train
        public MidpointDirection MUDirection = MidpointDirection.N;      // set by player locomotive to control MU'd locomotives
        public float MUThrottlePercent;                  // set by player locomotive to control MU'd locomotives
        public int MUGearboxGearIndex;                   // set by player locomotive to control MU'd locomotives
        public float MUReverserPercent = 100;            // steam engine direction/cutoff control for MU'd locomotives
        public float MUDynamicBrakePercent = -1;         // dynamic brake control for MU'd locomotives, <0 for off
        public float EqualReservoirPressurePSIorInHg = 90;   // Pressure in equalising reservoir - set by player locomotive - train brake pipe use this as a reference to set brake pressure levels

        // Class AirSinglePipe etc. use this property for pressure in PSI, 
        // but Class VacuumSinglePipe uses it for vacuum in InHg.
        public float BrakeLine2PressurePSI;              // extra line for dual line systems, main reservoir
        public float BrakeLine3PressurePSI;              // extra line just in case, engine brake pressure
        public float BrakeLine4 = -1;                    // extra line just in case, ep brake control line. -1: release/inactive, 0: hold, 0 < value <=1: apply
        public RetainerSetting RetainerSetting = RetainerSetting.Exhaust;
        public int RetainerPercent = 100;
        public float TotalTrainBrakePipeVolumeM3; // Total volume of train brake pipe
        public float TotalTrainBrakeCylinderVolumeM3; // Total volume of train brake cylinders
        public float TotalTrainBrakeSystemVolumeM3; // Total volume of train brake system
        public float TotalCurrentTrainBrakeSystemVolumeM3; // Total current volume of train brake system
        public bool EQEquippedVacLoco = false;          // Flag for locomotives fitted with vacuum brakes that have an Equalising reservoir fitted
        public float PreviousCarCount;                  // Keeps track of the last number of cars in the train consist (for vacuum brakes)
        public bool TrainBPIntact = true;           // Flag to indicate that the train BP is not intact, ie due to disconnection or an open valve cock.

        public int FirstCarUiD;                          // UiD of first car in the train
        public float HUDWagonBrakeCylinderPSI;         // Display value for wagon HUD
        public float HUDLocomotiveBrakeCylinderPSI;    // Display value for locomotive HUD
        public bool HUDBrakeSlide;                     // Display indication for brake wheel slip
        public bool WagonsAttached = false;    // Wagons are attached to train
        public float LeadPipePressurePSI;       // Keeps record of Lead locomootive brake pipe pressure

        public bool IsWheelSlipWarninq;
        public bool IsWheelSlip;
        public bool IsBrakeSkid;

        public bool HotBoxSetOnTrain = false;

        // Carriage Steam Heating
        public bool HeatedCarAttached = false;
        public bool HeatingBoilerCarAttached = false;
        bool IsFirstTimeBoilerCarAttached = true;
        public float TrainSteamPipeHeatW;               // Not required, all instances can be removed!!!!!!!!!
        public float TrainInsideTempC;                  // Desired inside temperature for carriage steam heating depending upon season
        public float TrainOutsideTempC;                 // External ambient temeprature for carriage steam heating.
        public float TrainSteamHeatLossWpT;             // Total Steam Heat loss of train
        public float TrainHeatVolumeM3;                 // Total Volume of train to steam heat
        public float TrainHeatPipeAreaM2;               // Total area of heating pipe for steam heating
        public float TrainCurrentSteamHeatPipeTempC;                 // Temperature of steam in steam heat system based upon pressure setting
        public bool CarSteamHeatOn = false;    // Is steam heating turned on
        public float TrainNetSteamHeatLossWpTime;        // Net Steam loss - Loss in Cars vs Steam Pipe Heat
        public float TrainCurrentTrainSteamHeatW;    // Current steam heat of air in train
        public float TrainTotalSteamHeatW;         // Total steam heat in train - based upon air volume
        float SpecificHeatCapcityAirKJpKgK = 1.006f; // Specific Heat Capacity of Air
        float DensityAirKgpM3 = 1.247f;   // Density of air - use a av value
        bool IsSteamHeatLow = false;        // Flag to indicate when steam heat temp is low
        public float DisplayTrainNetSteamHeatLossWpTime;  // Display Net Steam loss - Loss in Cars vs Steam Pipe Heat
        public float TrainSteamPipeHeatConvW;               // Heat radiated by steam pipe - convection
        public float TrainSteamHeatPipeRadW;                // Heat radiated by steam pipe - radiation
        float EmissivityFactor = 0.79f; // Oxidised steel
        float OneAtmospherePSI = 14.696f;      // Atmospheric Pressure
        float PipeHeatTransCoeffWpM2K = 22.0f;    // heat transmission coefficient for a steel pipe.
        float BoltzmanConstPipeWpM2 = 0.0000000567f; // Boltzman's Constant
        public bool TrainHeatingBoilerInitialised = false;

        // Values for Wind Direction and Speed - needed for wind resistance and lateral force
        public float PhysicsWindDirectionDeg;
        public float PhysicsWindSpeedMpS;
        public float PhysicsTrainLocoDirectionDeg;
        public float ResultantWindComponentDeg;
        public float WindResultantSpeedMpS;

        public bool TrainWindResistanceDependent => simulator.Settings.WindResistanceDependent;

        // Auxiliary Water Tenders
        public float MaxAuxTenderWaterMassKG;
        public bool IsAuxTenderCoupled = false;

        //To investigate coupler breaks on route
        private bool numOfCouplerBreaksNoted = false;
        public static int NumOfCouplerBreaks = 0;//Debrief Eval
        public bool DbfEvalValueChanged { get; set; }//Debrief Eval

        public TrainType TrainType { get; internal set; } = TrainType.Player;

        public float? DistanceToSignal = null;
        internal List<SignalItemInfo> SignalObjectItems;
        public int IndexNextSignal = -1;                 // Index in SignalObjectItems for next signal
        public int IndexNextSpeedlimit = -1;             // Index in SignalObjectItems for next speedpost
        public Signal[] NextSignalObject = new Signal[2];  // direct reference to next signal

        public float TrainMaxSpeedMpS;                   // Max speed as set by route (default value)
        public float AllowedMaxSpeedMpS;                 // Max speed as allowed
        public float allowedMaxSpeedSignalMpS;           // Max speed as set by signal
        public float allowedMaxSpeedLimitMpS;            // Max speed as set by limit
        public float allowedMaxTempSpeedLimitMpS;        // Max speed as set by temp speed limit
        public float allowedAbsoluteMaxSpeedSignalMpS;   // Max speed as set by signal independently from train features
        public float allowedAbsoluteMaxSpeedLimitMpS;    // Max speed as set by limit independently from train features
        public float allowedAbsoluteMaxTempSpeedLimitMpS;    // Max speed as set by temp speed limit independently from train features
        public float maxTimeS = 120;                     // check ahead for distance covered in 2 mins.
        public float minCheckDistanceM = 5000;           // minimum distance to check ahead
        public float minCheckDistanceManualM = 3000;     // minimum distance to check ahead in manual mode

        public float standardOverlapM = 15.0f;           // standard overlap on clearing sections
        public float junctionOverlapM = 75.0f;           // standard overlap on clearing sections
        private float rearPositionOverlap = 25.0f;       // allowed overlap when slipping
        private float standardWaitTimeS = 60.0f;         // wait for 1 min before claim state

        private const float BackwardThreshold = 20;            // counter threshold to detect backward move

        internal TrackCircuitRoutePath TCRoute;                      // train path converted to TC base
        public TrackCircuitPartialPathRoute[] ValidRoute = new TrackCircuitPartialPathRoute[2] { null, null };  // actual valid path
        private TrackCircuitPartialPathRoute manualTrainRoute;     // partial route under train for Manual mode
        internal bool ClaimState { get; set; }              // train is allowed to perform claim on sections
        internal double ActualWaitTimeS { get; set; }       // actual time waiting for signal
        public int movedBackward;                           // counter to detect backward move
        public float waitingPointWaitTimeS = -1.0f;         // time due at waiting point (PLAYER train only, valid in >= 0)

        public List<TrackCircuitSection> OccupiedTrack { get; } = new List<TrackCircuitSection>();

        // Station Info
        public List<int> HoldingSignals = new List<int>();// list of signals which must not be cleared (eg station stops)
        public List<StationStop> StationStops = new List<StationStop>();  //list of station stop details
        public StationStop PreviousStop = null;                           //last stop passed
        public bool AtStation = false;                                    //set if train is in station
        public bool MayDepart = false;                                    //set if train is ready to depart
        public string DisplayMessage = "";                                //string to be displayed in station information window
        public Color DisplayColor = Color.LightGreen;                     //color for DisplayMessage
        public bool CheckStations = false;                                //used when in timetable mode to check on stations
        public TimeSpan? Delay = null;                                    // present delay of the train (if any)

        public int AttachTo = -1;                              // attach information : train to which to attach at end of run
        public int IncorporatedTrainNo = -1;                        // number of train incorporated in actual train
        public Train IncorporatingTrain;                      // train incorporating another train
        public int IncorporatingTrainNo = -1;                   // number of train incorporating the actual train

        public ServiceTraffics TrafficService;
        public int[,] MisalignedSwitch = new int[2, 2] { { -1, -1 }, { -1, -1 } };  // misaligned switch indication per direction:
        // cell 0 : index of switch, cell 1 : required linked section; -1 if not valid
        public Dictionary<int, float> PassedSignalSpeeds = new Dictionary<int, float>();  // list of signals and related speeds pending processing (manual and explorer mode)
        public int[] LastPassedSignal = new int[2] { -1, -1 };  // index of last signal which set speed limit per direction (manual and explorer mode)

        // Variables used for autopilot mode and played train switching
        public bool IsActualPlayerTrain
        {
            get
            {
                if (simulator.PlayerLocomotive == null)
                {
                    return false;
                }
                return this == simulator.PlayerLocomotive.Train;
            }
        }

        public bool IsPlayerDriven => TrainType == TrainType.Player || TrainType == TrainType.AiPlayerDriven;

        public bool IsPlayable = false;
        public bool IsPathless = false;

        // End variables used for autopilot mode and played train switching

        public TrainRouted routedForward;                 // routed train class for forward moves (used in signalling)
        public TrainRouted routedBackward;                // routed train class for backward moves (used in signalling)

        public TrainControlMode ControlMode { get; set; } = TrainControlMode.Undefined;     // train control mode

        public OutOfControlReason OutOfControlReason { get; private set; } = OutOfControlReason.UnDefined; // train out of control

        public EnumArray<TrackCircuitPosition, Direction> PresentPosition { get; } =
            new EnumArray<TrackCircuitPosition, Direction>(new TrackCircuitPosition[] { new TrackCircuitPosition(), new TrackCircuitPosition() });         // present position : 0 = front, 1 = rear
        public EnumArray<TrackCircuitPosition, Direction> PreviousPosition { get; } =
            new EnumArray<TrackCircuitPosition, Direction>(new TrackCircuitPosition[] { new TrackCircuitPosition(), new TrackCircuitPosition() });        // previous train position

        public float DistanceTravelledM { get; internal set; }      // actual distance travelled
        public float ReservedTrackLengthM = 0.0f;                        // lenght of reserved section

        public float travelled;                                          // distance travelled, but not exactly
        public float targetSpeedMpS;                                    // target speed for remote trains; used for sound management
        public DistanceTravelledActions requiredActions = new DistanceTravelledActions(); // distance travelled action list
        public AuxActionsContainer AuxActionsContainer { get; } // Action To Do during activity, like WP

        public float activityClearingDistanceM = 30.0f;        // clear distance to stopping point for activities
        public const float shortClearingDistanceM = 15.0f;     // clearing distance for short trains in activities
        public const float standardClearingDistanceM = 30.0f;  // standard clearing distance for trains in activities
        public const int standardTrainMinCarNo = 10;           // Minimum number of cars for a train to have standard clearing distance

        private float clearanceAtRearM = -1;              // save distance behind train (when moving backward)
        private Signal rearSignalObject ;            // direct reference to signal at rear (when moving backward)
        public bool IsTilting;

        public float InitialSpeed = 0;                 // initial speed of train in activity as set in .srv file
        public float InitialThrottlepercent = 25; // initial value of throttle when train starts activity at speed > 0

        public double BrakingTime;              // Total braking time, used to check whether brakes get stuck
        public double ContinuousBrakingTime;     // Consecutive braking time, used to check whether brakes get stuck
        public double RunningTime;              // Total running time, used to check whether a locomotive is partly or totally unpowered due to a fault
        public int UnpoweredLoco = -1;          // car index of unpowered loco
        public bool ColdStart = true;           // False if train is moving at game start or if game resumed

        // TODO: Replace this with an event
        public bool FormationReversed;          // flags the execution of the ReverseFormation method (executed at reversal points)

        //TODO 20201126 next three properties should be made private, with some helper to update from external, and potentially using EnumArray
        public EndAuthorityType[] EndAuthorityTypes = new EndAuthorityType[2] { EndAuthorityType.NoPathReserved, EndAuthorityType.NoPathReserved };
        public int[] LastReservedSection = new int[2] { -1, -1 };         // index of furthest cleared section (for NODE control)
        public float[] DistanceToEndNodeAuthorityM = new float[2];      // distance to end of authority

        public int LoopSection = -1;                                    // section where route loops back onto itself

        public bool nextRouteReady = false;                             // indication to activity.cs that a reversal has taken place

        private static double lastLogTime;
        private protected bool evaluateTrainSpeed;                  // logging of train speed required
        private protected int evaluationInterval;                   // logging interval
        private protected EvaluationLogContents evaluationContent;  // logging selection
        private protected string evaluationLogFile;                 // required datalog file

        private protected static readonly Simulator simulator = Simulator.Instance;                 // reference to the simulator
        private protected static readonly SignalEnvironment signalRef = simulator.SignalEnvironment;// reference to main Signals class, shortcut only
        private protected static readonly char Separator = (char)simulator.Settings.DataLoggerSeparator;

        #region steam and heating
        private static readonly double desiredCompartmentAlarmTempSetpointC = Temperature.Celsius.FromF(45.0); // Alarm temperature
        private static readonly double resetCompartmentAlarmTempSetpointC = Temperature.Celsius.FromF(65.0);
        #endregion


        // For AI control of the train
        public float AITrainBrakePercent
        {
            get
            {
                return aiBrakePercent;
            }
            set
            {
                aiBrakePercent = value;
                foreach (TrainCar car in Cars)
                    car.BrakeSystem.AISetPercent(aiBrakePercent);
            }
        }
        private float aiBrakePercent;
        public float AITrainThrottlePercent
        {
            get
            {
                return MUThrottlePercent;
            }
            set
            {
                MUThrottlePercent = value;
            }
        }

        public int AITrainGearboxGearIndex
        {
            set
            {
                MUGearboxGearIndex = value;
            }
            get
            {
                return MUGearboxGearIndex;
            }
        }
        public bool AITrainDirectionForward
        {
            get
            {
                return MUDirection == MidpointDirection.Forward;
            }
            set
            {
                MUDirection = value ? MidpointDirection.Forward : MidpointDirection.Reverse;
                MUReverserPercent = value ? 100 : -100;
            }
        }
        public TrainCar LeadLocomotive
        {
            get
            {
                return LeadLocomotiveIndex >= 0 && LeadLocomotiveIndex < Cars.Count ? Cars[LeadLocomotiveIndex] : null;
            }
            set
            {
                LeadLocomotiveIndex = -1;
                for (int i = 0; i < Cars.Count; i++)
                    if (value == Cars[i] && value.IsDriveable)
                    {
                        LeadLocomotiveIndex = i;
                        //MSTSLocomotive lead = (MSTSLocomotive)Cars[LeadLocomotiveIndex];
                        //if (lead.EngineBrakeController != null)
                        //    lead.EngineBrakeController.UpdateEngineBrakePressure(ref BrakeLine3PressurePSI, 1000);
                    }
            }
        }

        // Get the UiD value of the first wagon - searches along train, and gets the integer UiD of the first wagon that is not an engine or tender
        public virtual int GetFirstWagonUiD()
        {
            FirstCarUiD = 0; // Initialise at zero every time routine runs
            foreach (TrainCar car in Cars)
            {
                if (car.WagonType != MSTSWagon.WagonTypes.Engine && car.WagonType != MSTSWagon.WagonTypes.Tender) // If car is not a locomotive or tender, then set UiD
                {
                    FirstCarUiD = car.UiD;
                }
                if (FirstCarUiD != 0)
                {
                    break; // If UiD has been set, then don't go any further
                }
            }
            return FirstCarUiD;
        }

        // Determine whther there are any wagons attached to the locomotive
        public virtual bool GetWagonsAttachedIndication()
        {
            WagonsAttached = false;
            foreach (TrainCar car in Cars)
            {
                // Test to see if freight or passenger wagons attached (used to set BC pressure in locomotive or wagons)
                if (car.WagonType == MSTSWagon.WagonTypes.Freight || car.WagonType == MSTSWagon.WagonTypes.Passenger)
                {
                    WagonsAttached = true;
                    break;
                }
                else
                {
                    WagonsAttached = false;
                }
            }
            return WagonsAttached;
        }

        #region .ctor
        private void Init()
        {
            allowedAbsoluteMaxSpeedSignalMpS = (float)simulator.TRK.Route.SpeedLimit;
            allowedAbsoluteMaxSpeedLimitMpS = allowedAbsoluteMaxSpeedSignalMpS;
            allowedAbsoluteMaxTempSpeedLimitMpS = allowedAbsoluteMaxSpeedSignalMpS;
        }

        // Constructor
        public Train()
        {
            Init();

            if (simulator.IsAutopilotMode && TotalNumber == 1 && simulator.TrainDictionary.Count == 0)
                TotalNumber = 0; //The autopiloted train has number 0

            Number = TotalNumber;
            TotalNumber++;
            SignalObjectItems = new List<SignalItemInfo>();
            Name = string.Empty;

            routedForward = new TrainRouted(this, 0);
            routedBackward = new TrainRouted(this, 1);
            AuxActionsContainer = new AuxActionsContainer(this);
        }

        // Constructor for Dummy entries used on restore
        // Signals is restored before Trains, links are restored by Simulator
        public Train(int number)
        {
            Init();
            Number = number;
            routedForward = new TrainRouted(this, 0);
            routedBackward = new TrainRouted(this, 1);
            AuxActionsContainer = new AuxActionsContainer(this);
        }

        // Constructor for uncoupled trains
        // copy path info etc. from original train
        public Train(Train source)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));

            Init();
            Number = TotalNumber;
            Name = $"{source.Name}{TotalNumber}";
            TotalNumber++;
            SignalObjectItems = new List<SignalItemInfo>();

            AuxActionsContainer = new AuxActionsContainer(this);
            if (source.TrafficService != null)
            {
                TrafficService = new ServiceTraffics(source.TrafficService.Time);

                foreach (ServiceTrafficItem thisTrafficItem in source.TrafficService)
                {
                    TrafficService.Add(thisTrafficItem);
                }
            }

            if (source.TCRoute != null)
            {
                TCRoute = new TrackCircuitRoutePath(source.TCRoute);
            }

            ValidRoute[0] = new TrackCircuitPartialPathRoute(source.ValidRoute[0]);
            ValidRoute[1] = new TrackCircuitPartialPathRoute(source.ValidRoute[1]);

            DistanceTravelledM = source.DistanceTravelledM;

            if (source.requiredActions.Count > 0)
            {
                requiredActions = source.requiredActions.Copy();
            }

            routedForward = new TrainRouted(this, 0);
            routedBackward = new TrainRouted(this, 1);

            ControlMode = source.ControlMode;

            AllowedMaxSpeedMpS = source.AllowedMaxSpeedMpS;
            allowedMaxSpeedLimitMpS = source.allowedMaxSpeedLimitMpS;
            allowedMaxSpeedSignalMpS = source.allowedMaxSpeedSignalMpS;
            allowedAbsoluteMaxSpeedLimitMpS = source.allowedAbsoluteMaxSpeedLimitMpS;
            allowedAbsoluteMaxSpeedSignalMpS = source.allowedAbsoluteMaxSpeedSignalMpS;

            if (source.StationStops != null)
            {
                foreach (StationStop stationStop in source.StationStops)
                {
                    StationStops.Add(stationStop.CreateCopy());
                }
            }
            else
            {
                StationStops = null;
            }
        }

        /// Restore
        public Train(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));
            Init();

            routedForward = new TrainRouted(this, 0);
            routedBackward = new TrainRouted(this, 1);
            ColdStart = false;
            RestoreCars(inf);
            Number = inf.ReadInt32();
            TotalNumber = Math.Max(Number + 1, TotalNumber);
            Name = inf.ReadString();
            SpeedMpS = LastSpeedMpS = inf.ReadSingle();
            AccelerationMpSpS.Preset(inf.ReadSingle());
            TrainType = (TrainType)inf.ReadInt32();
            if (TrainType == TrainType.Static)
                ColdStart = true;
            MUDirection = (MidpointDirection)inf.ReadInt32();
            MUThrottlePercent = inf.ReadSingle();
            MUGearboxGearIndex = inf.ReadInt32();
            MUDynamicBrakePercent = inf.ReadSingle();
            EqualReservoirPressurePSIorInHg = inf.ReadSingle();
            BrakeLine2PressurePSI = inf.ReadSingle();
            BrakeLine3PressurePSI = inf.ReadSingle();
            BrakeLine4 = inf.ReadSingle();
            aiBrakePercent = inf.ReadSingle();
            LeadLocomotiveIndex = inf.ReadInt32();
            RetainerSetting = (RetainerSetting)inf.ReadInt32();
            RetainerPercent = inf.ReadInt32();
            RearTDBTraveller = new Traveller(simulator.TSectionDat, simulator.TDB.TrackDB.TrackNodes, inf);
            SlipperySpotDistanceM = inf.ReadSingle();
            SlipperySpotLengthM = inf.ReadSingle();
            TrainMaxSpeedMpS = inf.ReadSingle();
            AllowedMaxSpeedMpS = inf.ReadSingle();
            allowedMaxSpeedSignalMpS = inf.ReadSingle();
            allowedMaxSpeedLimitMpS = inf.ReadSingle();
            allowedMaxTempSpeedLimitMpS = inf.ReadSingle();
            allowedAbsoluteMaxSpeedSignalMpS = inf.ReadSingle();
            allowedAbsoluteMaxSpeedLimitMpS = inf.ReadSingle();
            allowedAbsoluteMaxTempSpeedLimitMpS = inf.ReadSingle();
            BrakingTime = inf.ReadDouble();
            ContinuousBrakingTime = inf.ReadDouble();
            RunningTime = inf.ReadDouble();
            IncorporatedTrainNo = inf.ReadInt32();
            IncorporatingTrainNo = inf.ReadInt32();
            IsAuxTenderCoupled = inf.ReadBoolean();
            if (IncorporatedTrainNo > -1)
            {
                Train train = GetOtherTrainByNumber(IncorporatedTrainNo);
                if (train != null)
                {
                    train.IncorporatingTrain = this;
                    train.IncorporatingTrainNo = Number;
                }
            }
            if (IncorporatingTrainNo > -1)
            {
                Train train = GetOtherTrainByNumber(IncorporatingTrainNo);
                if (train != null)
                {
                    IncorporatingTrain = train;
                }
            }
            CheckFreight();


            SignalObjectItems = new List<SignalItemInfo>();

            TrainType = (TrainType)inf.ReadInt32();
            IsTilting = inf.ReadBoolean();
            ClaimState = inf.ReadBoolean();
            evaluateTrainSpeed = inf.ReadBoolean();
            evaluationInterval = inf.ReadInt32();
            evaluationContent = (EvaluationLogContents)inf.ReadInt32();

            int dsfile = inf.ReadInt32();
            if (dsfile < 0)
            {
                evaluationLogFile = string.Empty;
            }
            else
            {
                evaluationLogFile = inf.ReadString();
            }

            TCRoute = null;
            bool routeAvailable = inf.ReadBoolean();
            if (routeAvailable)
            {
                TCRoute = new TrackCircuitRoutePath(inf);
            }

            ValidRoute[0] = null;
            bool validRouteAvailable = inf.ReadBoolean();
            if (validRouteAvailable)
            {
                ValidRoute[0] = new TrackCircuitPartialPathRoute(inf);
            }

            ValidRoute[1] = null;
            validRouteAvailable = inf.ReadBoolean();
            if (validRouteAvailable)
            {
                ValidRoute[1] = new TrackCircuitPartialPathRoute(inf);
            }

            int count = inf.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                OccupiedTrack.Add(TrackCircuitSection.TrackCircuitList[inf.ReadInt32()]);
            }

            count = inf.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                HoldingSignals.Add(inf.ReadInt32());
            }

            count = inf.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                StationStops.Add(new StationStop(inf));
            }

            count = inf.ReadInt32();
            if (count >= 0)
            {
                PreviousStop = new StationStop(inf);
            }
            else
            {
                PreviousStop = null;
            }

            AtStation = inf.ReadBoolean();
            MayDepart = inf.ReadBoolean();
            CheckStations = inf.ReadBoolean();
            AttachTo = inf.ReadInt32();

            DisplayMessage = inf.ReadString();

            int delaySeconds = inf.ReadInt32();
            if (delaySeconds < 0) // delay value (in seconds, as integer)
            {
                Delay = null;
            }
            else
            {
                Delay = TimeSpan.FromSeconds(delaySeconds);
            }

            count = inf.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int passedSignalKey = inf.ReadInt32();
                float passedSignalValue = inf.ReadSingle();
                PassedSignalSpeeds.Add(passedSignalKey, passedSignalValue);
            }
            LastPassedSignal[0] = inf.ReadInt32();
            LastPassedSignal[1] = inf.ReadInt32();

            bool trafficServiceAvailable = inf.ReadBoolean();
            if (trafficServiceAvailable)
            {
                TrafficService = RestoreTrafficSDefinition(inf);
            }

            ControlMode = (TrainControlMode)inf.ReadInt32();
            OutOfControlReason = (OutOfControlReason)inf.ReadInt32();
            EndAuthorityTypes[0] = (EndAuthorityType)inf.ReadInt32();
            EndAuthorityTypes[1] = (EndAuthorityType)inf.ReadInt32();
            LastReservedSection[0] = inf.ReadInt32();
            LastReservedSection[1] = inf.ReadInt32();
            LoopSection = inf.ReadInt32();
            DistanceToEndNodeAuthorityM[0] = inf.ReadSingle();
            DistanceToEndNodeAuthorityM[1] = inf.ReadSingle();

            if (TrainType != TrainType.AiNotStarted && TrainType != TrainType.AiAutoGenerated)
            {
                CalculatePositionOfCars();

                DistanceTravelledM = inf.ReadSingle();
                PresentPosition[Direction.Forward] = new TrackCircuitPosition();
                PresentPosition[Direction.Forward].RestorePresentPosition(inf, this);
                PresentPosition[Direction.Backward] = new TrackCircuitPosition();
                PresentPosition[Direction.Backward].RestorePresentRear(inf, this);
                PreviousPosition[Direction.Forward] = new TrackCircuitPosition();
                PreviousPosition[Direction.Forward].RestorePreviousPosition(inf);

                PresentPosition[Direction.Forward].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                PresentPosition[Direction.Backward].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
            }
            else
            {
                DistanceTravelledM = inf.ReadSingle();
                PresentPosition[Direction.Forward] = new TrackCircuitPosition();
                PresentPosition[Direction.Forward].RestorePresentPositionDummy(inf);
                PresentPosition[Direction.Backward] = new TrackCircuitPosition();
                PresentPosition[Direction.Backward].RestorePresentRearDummy(inf);
                PreviousPosition[Direction.Forward] = new TrackCircuitPosition();
                PreviousPosition[Direction.Forward].RestorePreviousPositionDummy(inf);
            }
            travelled = DistanceTravelledM;
            count = inf.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int actionType = inf.ReadInt32();
                switch (actionType)
                {
                    case 1:
                        ActivateSpeedLimit speedLimit = new ActivateSpeedLimit(inf);
                        requiredActions.InsertAction(speedLimit);
                        break;
                    case 2:
                        ClearSectionItem clearSection = new ClearSectionItem(inf);
                        requiredActions.InsertAction(clearSection);
                        break;
                    case 3:
                        AIActionItem actionItem = new AIActionItem(inf);
                        requiredActions.InsertAction(actionItem);
                        break;
                    case 4:
                        AuxActionItem auxAction = new AuxActionItem(inf);
                        requiredActions.InsertAction(auxAction);
                        Trace.TraceWarning("DistanceTravelledItem type 4 restored as AuxActionItem");
                        break;
                    default:
                        Trace.TraceWarning($"Unknown type of DistanceTravelledItem (type {actionType}");
                        break;
                }
            }

            AuxActionsContainer = new AuxActionsContainer(this, inf, simulator.RoutePath);
            RestoreDeadlockInfo(inf);

            InitialSpeed = inf.ReadSingle();
            IsPathless = inf.ReadBoolean();

            if (TrainType != TrainType.Remote)
            {
                // restore leadlocomotive
                if (LeadLocomotiveIndex >= 0)
                {
                    LeadLocomotive = Cars[LeadLocomotiveIndex];
                    if (TrainType != TrainType.Static)
                        simulator.PlayerLocomotive = LeadLocomotive;
                }

                // restore logfile
                if (evaluateTrainSpeed)
                {
                    CreateLogFile();
                }
            }
        }
        #endregion

        private void RestoreCars(BinaryReader inf)
        {
            int count = inf.ReadInt32();
            if (count > 0)
            {
                for (int i = 0; i < count; ++i)
                    Cars.Add(RollingStock.Restore(simulator, inf, this));
            }
        }

        private static ServiceTraffics RestoreTrafficSDefinition(BinaryReader inf)
        {
            ServiceTraffics serviceDefinition = new ServiceTraffics(inf.ReadInt32());

            int totalTrafficItems = inf.ReadInt32();

            for (int i = 0; i < totalTrafficItems; i++)
            {
                ServiceTrafficItem trafficItem = new ServiceTrafficItem(inf.ReadInt32(), inf.ReadInt32(), 0, inf.ReadSingle(), inf.ReadInt32());
                serviceDefinition.Add(trafficItem);
            }

            return serviceDefinition;
        }

        private void RestoreDeadlockInfo(BinaryReader inf)
        {
            int totalDeadlock = inf.ReadInt32();
            for (int iDeadlockList = 0; iDeadlockList < totalDeadlock; iDeadlockList++)
            {
                int deadlockListKey = inf.ReadInt32();
                int deadlockListLength = inf.ReadInt32();

                List<Dictionary<int, int>> thisDeadlockList = new List<Dictionary<int, int>>();

                for (int iDeadlock = 0; iDeadlock < deadlockListLength; iDeadlock++)
                {
                    int deadlockInfoLength = inf.ReadInt32();
                    Dictionary<int, int> thisDeadlockDetails = new Dictionary<int, int>();

                    for (int iDeadlockDetails = 0; iDeadlockDetails < deadlockInfoLength; iDeadlockDetails++)
                    {
                        int deadlockKey = inf.ReadInt32();
                        int deadlockValue = inf.ReadInt32();

                        thisDeadlockDetails.Add(deadlockKey, deadlockValue);
                    }

                    thisDeadlockList.Add(thisDeadlockDetails);
                }
                DeadlockInfo.Add(deadlockListKey, thisDeadlockList);
            }
        }


        /// save game state
        public virtual void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));

            SaveCars(outf);
            outf.Write(Number);
            outf.Write(Name);
            outf.Write(SpeedMpS);
            outf.Write((float)AccelerationMpSpS.SmoothedValue);
            outf.Write((int)TrainType);
            outf.Write((int)MUDirection);
            outf.Write(MUThrottlePercent);
            outf.Write(MUGearboxGearIndex);
            outf.Write(MUDynamicBrakePercent);
            outf.Write(EqualReservoirPressurePSIorInHg);
            outf.Write(BrakeLine2PressurePSI);
            outf.Write(BrakeLine3PressurePSI);
            outf.Write(BrakeLine4);
            outf.Write(aiBrakePercent);
            outf.Write(LeadLocomotiveIndex);
            outf.Write((int)RetainerSetting);
            outf.Write(RetainerPercent);
            RearTDBTraveller.Save(outf);
            outf.Write(SlipperySpotDistanceM);
            outf.Write(SlipperySpotLengthM);
            outf.Write(TrainMaxSpeedMpS);
            outf.Write(AllowedMaxSpeedMpS);
            outf.Write(allowedMaxSpeedSignalMpS);
            outf.Write(allowedMaxSpeedLimitMpS);
            outf.Write(allowedMaxTempSpeedLimitMpS);
            outf.Write(allowedAbsoluteMaxSpeedSignalMpS);
            outf.Write(allowedAbsoluteMaxSpeedLimitMpS);
            outf.Write(allowedAbsoluteMaxTempSpeedLimitMpS);
            outf.Write(BrakingTime);
            outf.Write(ContinuousBrakingTime);
            outf.Write(RunningTime);
            outf.Write(IncorporatedTrainNo);
            outf.Write(IncorporatingTrainNo);
            outf.Write(IsAuxTenderCoupled);

            outf.Write((int)TrainType);
            outf.Write(IsTilting);
            outf.Write(ClaimState);
            outf.Write(evaluateTrainSpeed);
            outf.Write(evaluationInterval);

            outf.Write((int)evaluationContent);

            if (string.IsNullOrEmpty(evaluationLogFile))
            {
                outf.Write(-1);
            }
            else
            {
                outf.Write(1);
                outf.Write(evaluationLogFile);
            }

            if (TCRoute == null)
            {
                outf.Write(false);
            }
            else
            {
                outf.Write(true);
                TCRoute.Save(outf);
            }

            if (ValidRoute[0] == null)
            {
                outf.Write(false);
            }
            else
            {
                outf.Write(true);
                ValidRoute[0].Save(outf);
            }

            if (ValidRoute[1] == null)
            {
                outf.Write(false);
            }
            else
            {
                outf.Write(true);
                ValidRoute[1].Save(outf);
            }

            outf.Write(OccupiedTrack.Count);
            foreach (TrackCircuitSection thisSection in OccupiedTrack)
            {
                outf.Write(thisSection.Index);
            }

            outf.Write(HoldingSignals.Count);
            foreach (int thisHold in HoldingSignals)
            {
                outf.Write(thisHold);
            }

            outf.Write(StationStops.Count);
            foreach (StationStop thisStop in StationStops)
            {
                thisStop.Save(outf);
            }

            if (PreviousStop == null)
            {
                outf.Write(-1);
            }
            else
            {
                outf.Write(1);
                PreviousStop.Save(outf);
            }

            outf.Write(AtStation);
            outf.Write(MayDepart);
            outf.Write(CheckStations);
            outf.Write(AttachTo);

            outf.Write(DisplayMessage);

            int DelaySeconds = Delay.HasValue ? (int)Delay.Value.TotalSeconds : -1;
            outf.Write(DelaySeconds);

            outf.Write(PassedSignalSpeeds.Count);
            foreach (KeyValuePair<int, float> thisPair in PassedSignalSpeeds)
            {
                outf.Write(thisPair.Key);
                outf.Write(thisPair.Value);
            }
            outf.Write(LastPassedSignal[0]);
            outf.Write(LastPassedSignal[1]);

            if (TrafficService == null)
            {
                outf.Write(false);
            }
            else
            {
                outf.Write(true);
                SaveTrafficSDefinition(outf, TrafficService);
            }

            outf.Write((int)ControlMode);
            outf.Write((int)OutOfControlReason);
            outf.Write((int)EndAuthorityTypes[0]);
            outf.Write((int)EndAuthorityTypes[1]);
            outf.Write(LastReservedSection[0]);
            outf.Write(LastReservedSection[1]);
            outf.Write(LoopSection);
            outf.Write(DistanceToEndNodeAuthorityM[0]);
            outf.Write(DistanceToEndNodeAuthorityM[1]);

            outf.Write(DistanceTravelledM);
            PresentPosition[Direction.Forward].Save(outf);
            PresentPosition[Direction.Backward].Save(outf);
            PreviousPosition[Direction.Forward].Save(outf);
            //  Save requiredAction, the original actions
            outf.Write(requiredActions.Count);
            foreach (DistanceTravelledItem thisAction in requiredActions)
            {
                thisAction.Save(outf);
            }
            //  Then, save the Auxiliary Action Container
            SaveAuxContainer(outf);

            SaveDeadlockInfo(outf);
            // Save initial speed
            outf.Write(InitialSpeed);
            outf.Write(IsPathless);
        }

        private void SaveCars(BinaryWriter outf)
        {
            outf.Write(Cars.Count);
            foreach (TrainCar car in Cars)
                RollingStock.Save(outf, car);
        }

        private static void SaveTrafficSDefinition(BinaryWriter outf, ServiceTraffics trafficServices)
        {
            outf.Write(trafficServices.Time);
            outf.Write(trafficServices.Count);
            foreach (ServiceTrafficItem serviceItem in trafficServices)
            {
                SaveTrafficItem(outf, serviceItem);
            }
        }

        private static void SaveTrafficItem(BinaryWriter outf, ServiceTrafficItem thisTI)
        {
            outf.Write(thisTI.ArrivalTime);
            outf.Write(thisTI.DepartTime);
            outf.Write(thisTI.DistanceDownPath);
            outf.Write(thisTI.PlatformStartID);
        }

        private void SaveDeadlockInfo(BinaryWriter outf)
        {
            outf.Write(DeadlockInfo.Count);
            foreach (KeyValuePair<int, List<Dictionary<int, int>>> deadlockInfo in DeadlockInfo)
            {
                outf.Write(deadlockInfo.Key);
                outf.Write(deadlockInfo.Value.Count);

                foreach (Dictionary<int, int> thisDeadlock in deadlockInfo.Value)
                {
                    outf.Write(thisDeadlock.Count);
                    foreach (KeyValuePair<int, int> thisDeadlockDetails in thisDeadlock)
                    {
                        outf.Write(thisDeadlockDetails.Key);
                        outf.Write(thisDeadlockDetails.Value);
                    }
                }
            }
        }

        private void SaveAuxContainer(BinaryWriter outf)
        {
            AuxActionsContainer.Save(outf, (int)simulator.ClockTime);
        }

        //================================================================================================//
        /// <summary>
        /// Changes the Lead locomotive (i.e. the loco which the player controls) to the next in the consist.
        /// Steps back through the train, ignoring any cabs that face rearwards until there are no forward-facing
        /// cabs left. Then continues from the rearmost, rearward-facing cab, reverses the train and resumes stepping back.
        /// E.g. if consist is made of 3 cars, each with front and rear-facing cabs
        ///     (A-b]:(C-d]:[e-F)
        /// then pressing Ctrl+E cycles the cabs in the sequence
        ///     A -> b -> C -> d -> e -> F
        /// </summary>
        public TrainCar GetNextCab()
        {
            // negative numbers used if rear cab selected
            // because '0' has no negative, all indices are shifted by 1!!!!

            int presentIndex = LeadLocomotiveIndex + 1;
            if (((MSTSLocomotive)LeadLocomotive).UsingRearCab)
                presentIndex = -presentIndex;

            List<int> cabList = new List<int>();

            for (int i = 0; i < Cars.Count; i++)
            {
                if (SkipOtherUsersCar(i))
                    continue;
                bool cab3d = Cars[i].HasFront3DCab || Cars[i].HasRear3DCab;
                bool hasFrontCab = cab3d ? Cars[i].HasFront3DCab : Cars[i].HasFrontCab;
                bool hasRearCab = cab3d ? Cars[i].HasRear3DCab : Cars[i].HasRearCab;
                if (Cars[i].Flipped)
                {
                    if (hasRearCab) cabList.Add(-(i + 1));
                    if (hasFrontCab) cabList.Add(i + 1);
                }
                else
                {
                    if (hasFrontCab) cabList.Add(i + 1);
                    if (hasRearCab) cabList.Add(-(i + 1));
                }
            }

            int lastIndex = cabList.IndexOf(presentIndex);
            if (lastIndex >= cabList.Count - 1) lastIndex = -1;

            int nextCabIndex = cabList[lastIndex + 1];

            TrainCar oldLead = LeadLocomotive;
            LeadLocomotiveIndex = Math.Abs(nextCabIndex) - 1;
            Trace.Assert(LeadLocomotive != null, "Tried to switch to non-existent loco");
            TrainCar newLead = LeadLocomotive;  // Changing LeadLocomotiveIndex also changed LeadLocomotive
            ((MSTSLocomotive)newLead).UsingRearCab = nextCabIndex < 0;

            if (oldLead != null && newLead != null && oldLead != newLead)
            {
                newLead.CopyControllerSettings(oldLead);
                // TODO :: need to link HeadOut cameras to new lead locomotive
                // following should do it but cannot be used due to protection level
                // Program.Viewer.HeadOutBackCamera.SetCameraCar(Cars[LeadLocomotiveIndex]);
                // seems there is nothing to attach camera to car
            }

            // If there is a player locomotive, and it is in this train, update it to match the new lead locomotive.
            if (simulator.PlayerLocomotive?.Train == this)

                simulator.PlayerLocomotive = newLead;

            return newLead;
        }

        //this function is needed for Multiplayer games as they do not need to have cabs, but need to know lead locomotives
        // Sets the Lead locomotive to the next in the consist
        internal void LeadNextLocomotive()
        {
            // First driveable
            int firstLead = -1;
            // Next driveale to the current
            int nextLead = -1;
            // Count of driveable locos
            int coud = 0;

            for (int i = 0; i < Cars.Count; i++)
            {
                if (Cars[i].IsDriveable)
                {
                    // Count the driveables
                    coud++;

                    // Get the first driveable
                    if (firstLead == -1)
                        firstLead = i;

                    // If later than current select the next
                    if (LeadLocomotiveIndex < i && nextLead == -1)
                    {
                        nextLead = i;
                    }
                }
            }

            TrainCar prevLead = LeadLocomotive;

            // If found one after the current
            if (nextLead != -1)
                LeadLocomotiveIndex = nextLead;
            // If not, and have more than one, set the first
            else if (coud > 1)
                LeadLocomotiveIndex = firstLead;
            TrainCar newLead = LeadLocomotive;
            if (prevLead != null && newLead != null && prevLead != newLead)
                newLead.CopyControllerSettings(prevLead);
        }

        //================================================================================================//
        /// <summary>
        /// Is there another cab in the player's train to change to?
        /// </summary>
        public bool IsChangeCabAvailable()
        {
            Trace.Assert(simulator.PlayerLocomotive != null, "Player loco is null when trying to switch locos");
            Trace.Assert(simulator.PlayerLocomotive.Train == this, "Trying to switch locos but not on player's train");

            int driveableCabs = 0;
            for (int i = 0; i < Cars.Count; i++)
            {
                if (SkipOtherUsersCar(i))
                    continue;
                if (Cars[i].HasFrontCab || Cars[i].HasFront3DCab)
                    driveableCabs++;
                if (Cars[i].HasRearCab || Cars[i].HasRear3DCab)
                    driveableCabs++;
            }
            if (driveableCabs < 2)
            {
                simulator.Confirmer.Warning(CabControl.ChangeCab, CabSetting.Warn1);
                return false;
            }
            return true;
        }

        /// In multiplayer, don't want to switch to a locomotive which is player locomotive of another user
        private bool SkipOtherUsersCar(int i)
        {
            if (!MPManager.IsMultiPlayer())
                return false;
            else
            {
                string username = MPManager.GetUserName();
                foreach (OnlinePlayer onlinePlayer in MPManager.OnlineTrains.Players.Values)
                {
                    // don't consider the present user
                    if (onlinePlayer.Username == username)
                        continue;
                    if (onlinePlayer.LeadingLocomotiveID == Cars[i].CarID)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// Reverse train formation
        /// Only performed when train activates a reversal point
        /// NOTE : this routine handles the physical train orientation only, all related route settings etc. must be handled separately
        internal void ReverseFormation(bool setMUParameters)
        {
            if (MPManager.IsMultiPlayer())
                MPManager.BroadCast((new MSGFlip(this, setMUParameters, Number)).ToString()); // message contains data before flip
            ReverseCars();
            // Flip the train's travellers.
            Traveller t = FrontTDBTraveller;
            FrontTDBTraveller = new Traveller(RearTDBTraveller, Traveller.TravellerDirection.Backward);
            RearTDBTraveller = new Traveller(t, Traveller.TravellerDirection.Backward);
            // If we are updating the controls...
            if (setMUParameters)
            {
                // Flip the controls.
                MUDirection = (MidpointDirection)((int)MUDirection * -1);
                MUReverserPercent = -MUReverserPercent;
            }
            if (!((this is AITrain aitrain && aitrain.AI.PreUpdate) || TrainType == TrainType.Static))
                FormationReversed = true;
        }

        /// Reverse cars and car order
        internal void ReverseCars()
        {
            // Shift all the coupler data along the train by 1 car. Not sure whether this logic is correct, as it appears to give incorrect coupler information - To Be Checked
            for (int i = Cars.Count - 1; i > 0; i--)
            {
                Cars[i].CopyCoupler(Cars[i - 1]);
            }
            // Reverse brake hose connections and angle cocks
            for (int i = 0; i < Cars.Count; i++)
            {
                bool ac = Cars[i].BrakeSystem.AngleCockAOpen;
                Cars[i].BrakeSystem.AngleCockAOpen = Cars[i].BrakeSystem.AngleCockBOpen;
                Cars[i].BrakeSystem.AngleCockBOpen = ac;
                if (i == Cars.Count - 1)
                    Cars[i].BrakeSystem.FrontBrakeHoseConnected = false;
                else
                    Cars[i].BrakeSystem.FrontBrakeHoseConnected = Cars[i + 1].BrakeSystem.FrontBrakeHoseConnected;
            }
            // Reverse the actual order of the cars in the train.
            Cars.Reverse();
            // Update leading locomotive index.
            if (LeadLocomotiveIndex >= 0)
                LeadLocomotiveIndex = Cars.Count - LeadLocomotiveIndex - 1;
            // Update flipped state of each car.
            for (int i = 0; i < Cars.Count; i++)
                Cars[i].Flipped = !Cars[i].Flipped;
        }

        /// Someone is sending an event notification to all cars on this train.
        /// ie doors open, pantograph up, lights on etc.
        public void SignalEvent(TrainEvent evt)
        {
            foreach (TrainCar car in Cars)
                car.SignalEvent(evt);
        }

        public void SignalEvent(PowerSupplyEvent evt)
        {
            foreach (TrainCar car in Cars)
                car.SignalEvent(evt);
        }

        public void SignalEvent(PowerSupplyEvent evt, int id)
        {
            foreach (TrainCar car in Cars)
                car.SignalEvent(evt, id);
        }

        //================================================================================================//
        /// <summary>
        /// Set starting conditions when speed > 0 
        /// <\summary>

        public virtual void InitializeMoving()
        {
            ColdStart = false;
            SpeedMpS = InitialSpeed;
            MUDirection = MidpointDirection.Forward;
            float initialThrottlepercent = InitialThrottlepercent;
            MUDynamicBrakePercent = -1;
            //            aiBrakePercent = 0;
            //            AITrainBrakePercent = 0;

            if (LeadLocomotiveIndex >= 0)
            {
                MSTSLocomotive lead = (MSTSLocomotive)Cars[LeadLocomotiveIndex];
                if (lead is MSTSSteamLocomotive) MUReverserPercent = 25;
                lead.CurrentElevationPercent = 100f * lead.WorldPosition.XNAMatrix.M32;

                //TODO: next if block has been inserted to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                // To achieve the same result with other means, without flipping trainset physics, the block should be deleted
                //         
                if (lead.IsDriveable && (lead as MSTSLocomotive).UsingRearCab)
                {
                    lead.CurrentElevationPercent = -lead.CurrentElevationPercent;
                }
                // give it a bit more gas if it is uphill
                if (lead.CurrentElevationPercent < -2.0) initialThrottlepercent = 40f;
                // better block gas if it is downhill
                else if (lead.CurrentElevationPercent > 1.0) initialThrottlepercent = 0f;

                if (lead.TrainBrakeController != null)
                {
                    EqualReservoirPressurePSIorInHg = lead.TrainBrakeController.MaxPressurePSI;
                }
            }
            MUThrottlePercent = initialThrottlepercent;
            AITrainThrottlePercent = initialThrottlepercent;

            TraincarsInitializeMoving();
        }

        //================================================================================================//
        /// <summary>
        /// Set starting conditions for TrainCars when speed > 0 
        /// <\summary>

        public void TraincarsInitializeMoving()
        {
            for (int i = 0; i < Cars.Count; ++i)
            {
                TrainCar car = Cars[i];
                car.InitializeMoving();
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update train 
        /// <\summary>

        public virtual void Update(double elapsedClockSeconds, bool auxiliaryUpdate = true)
        {
            if (!auxiliaryUpdate)
                FormationReversed = false;
            if (IsActualPlayerTrain && simulator.ActiveMovingTable != null)
                simulator.ActiveMovingTable.CheckTrainOnMovingTable(this);

            if (IsActualPlayerTrain && simulator.OriginalPlayerTrain != this && !CheckStations) // if player train is to check own stations
            {
                CheckStationTask();
            }


            if (IsActualPlayerTrain && simulator.Settings.ActRandomizationLevel > 0 && simulator.ActivityRun != null) // defects might occur
            {
                CheckFailures(elapsedClockSeconds);
            }

            // Update train physics, position and movement

            PhysicsUpdate(elapsedClockSeconds);

            // Update the UiD of First Wagon
            FirstCarUiD = GetFirstWagonUiD();

            // Check to see if wagons are attached to train
            WagonsAttached = GetWagonsAttachedIndication();

            //Exit here when train is static consist (no further actions required)

            if (GetAIMovementState() == AITrain.AI_MOVEMENT_STATE.AI_STATIC)
            {
                int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
                UpdateAIStaticState(presentTime);
            }

            if (TrainType == TrainType.Static)
                return;

            // perform overall update

            if (ControlMode == TrainControlMode.TurnTable)
            {
                UpdateTurntable(elapsedClockSeconds);
            }

            else if (ControlMode == TrainControlMode.Manual)                                        // manual mode
            {
                UpdateManual(elapsedClockSeconds);
            }

            else if (ControlMode == TrainControlMode.Explorer)                                 // explorer mode
            {
                UpdateExplorer(elapsedClockSeconds);
            }

            else if (ValidRoute[0] != null && GetAIMovementState() != AITrain.AI_MOVEMENT_STATE.AI_STATIC)     // no actions required for static objects //
            {
                if (ControlMode != TrainControlMode.OutOfControl) movedBackward = CheckBackwardClearance();  // check clearance at rear if not out of control //
                UpdateTrainPosition();                                                          // position update         //
                UpdateTrainPositionInformation();                                               // position update         //
                int SignalObjIndex = CheckSignalPassed(0, PresentPosition[Direction.Forward], PreviousPosition[Direction.Forward]);   // check if passed signal  //
                UpdateSectionState(movedBackward);                                              // update track occupation //
                if (!(this is AITrain && (this as AITrain).MovementState == AITrain.AI_MOVEMENT_STATE.SUSPENDED)) ObtainRequiredActions(movedBackward);    // process list of actions //

                if (TrainType == TrainType.Player && CheckStations) // if player train is to check own stations
                {
                    CheckStationTask();
                }

                bool stillExist = true;
                if ((TrainType != TrainType.Ai && TrainType != TrainType.AiPlayerHosting) && ControlMode != TrainControlMode.OutOfControl)
                {
                    stillExist = CheckRouteActions(elapsedClockSeconds);                          // check routepath (AI check at other point) //
                }

                if (stillExist)
                {
                    UpdateRouteClearanceAhead(SignalObjIndex, movedBackward, elapsedClockSeconds);  // update route clearance  //
                    if (!(TrainType == TrainType.Remote && MPManager.IsClient()))
                        UpdateSignalState(movedBackward);                                               // update signal state     //
                }
            }

            // check position of train wrt tunnels
            ProcessTunnels();

            // log train details

            if (evaluateTrainSpeed)
            {
                LogTrainSpeed(simulator.GameTime);
            }

        } // end Update

        //================================================================================================//
        /// <summary>
        /// Update train physics
        /// <\summary>

        internal virtual void PhysicsUpdate(double elapsedClockSeconds)
        {
            //if out of track, will set it to stop
            if ((FrontTDBTraveller != null && FrontTDBTraveller.IsEnd) || (RearTDBTraveller != null && RearTDBTraveller.IsEnd))
            {
                if (FrontTDBTraveller.IsEnd && RearTDBTraveller.IsEnd)
                {//if both travellers are out, very rare occation, but have to treat it
                    RearTDBTraveller.ReverseDirection();
                    RearTDBTraveller.NextTrackNode();
                }
                else if (FrontTDBTraveller.IsEnd)
                    RearTDBTraveller.Move(-1);//if front is out, move back
                else if (RearTDBTraveller.IsEnd)
                    RearTDBTraveller.Move(1);//if rear is out, move forward
                foreach (TrainCar car in Cars)
                {
                    car.SpeedMpS = 0;
                }
                SignalEvent(TrainEvent.ResetWheelSlip);//reset everything to 0 power
            }

            if (TrainType == TrainType.Remote || updateMSGReceived) //server tolds me this train (may include mine) needs to update position
            {
                UpdateRemoteTrainPos(elapsedClockSeconds);
                return;
            }
            // Update train physics, position and movement

            PropagateBrakePressure(elapsedClockSeconds);

            bool whlslp = false;
            bool whlslpwrn = false;
            bool whlskd = false;

            TrainCar uncoupleBehindCar = null;

            float massKg = 0f;
            foreach (TrainCar car in Cars)
            {
                car.MotiveForceN = 0;
                car.Update(elapsedClockSeconds);

                // Set TotalForce at the start of each calculation cycle. This value is adjusted further through loop based upon forces acting on the train.
                car.TotalForceN = car.MotiveForceN + car.GravityForceN;

                massKg += car.MassKG;
                //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                //                 if (car.Flipped)
                if (car.Flipped ^ (car.IsDriveable && car.Train.IsActualPlayerTrain && ((MSTSLocomotive)car).UsingRearCab))
                {
                    car.TotalForceN = -car.TotalForceN;
                    car.SpeedMpS = -car.SpeedMpS;
                }
                if (car.WheelSlip)
                    whlslp = true;
                if (car.WheelSlipWarning)
                    whlslpwrn = true;
                if (car.BrakeSkid)
                {
                    whlskd = true;
                    car.HUDBrakeSkid = true;
                }
                else
                {
                    car.HUDBrakeSkid = false;
                }

                if (car is MSTSDieselLocomotive || car is MSTSElectricLocomotive)
                {
                    // Test to see if locomotive is skidding for HUD presentation
                    if (car.BrakeRetardForceN > 25.0f && car.WheelSlip && car.ThrottlePercent < 0.1f)  // throttle is not good as it may not be zero? better brake? Think about more
                    {
                        whlskd = true;
                        car.HUDBrakeSkid = true;
                    }
                    else
                    {
                        car.HUDBrakeSkid = false;
                    }

                }

                if (car.CouplerExceedBreakLimit)
                    uncoupleBehindCar = car;
            }
            MassKg = massKg;

            IsWheelSlip = whlslp;
            IsWheelSlipWarninq = whlslpwrn;
            IsBrakeSkid = whlskd;

            // Coupler breaker
            if (uncoupleBehindCar != null)
            {
                if (uncoupleBehindCar.CouplerExceedBreakLimit)
                {
                    if (!numOfCouplerBreaksNoted)
                    {
                        NumOfCouplerBreaks++;
                        DbfEvalValueChanged = true;//Debrief eval

                        Trace.WriteLine($"Num of coupler breaks: {NumOfCouplerBreaks}");
                        numOfCouplerBreaksNoted = true;

                        if (simulator.BreakCouplers)
                        {
                            simulator.UncoupleBehind(uncoupleBehindCar, true);
                            uncoupleBehindCar.CouplerExceedBreakLimit = false;
                            simulator.Confirmer.Warning(Simulator.Catalog.GetString("Coupler broken!"));
                        }
                        else
                            simulator.Confirmer.Warning(Simulator.Catalog.GetString("Coupler overloaded!"));
                    }
                }
                else
                    numOfCouplerBreaksNoted = false;

                uncoupleBehindCar = null;
            }
            else
                numOfCouplerBreaksNoted = false;


            UpdateCarSteamHeat(elapsedClockSeconds);
            UpdateAuxTender();

            AddCouplerImpulseForces(elapsedClockSeconds);
            ComputeCouplerForces(elapsedClockSeconds);

            UpdateCarSpeeds(elapsedClockSeconds);
            UpdateCouplerSlack(elapsedClockSeconds);

            // Update wind elements for the train, ie the wind speed, and direction, as well as the angle between the train and wind
            UpdateWindComponents();

            double distanceM = LastCar.SpeedMpS * elapsedClockSeconds;
            if (double.IsNaN(distanceM))
                distanceM = 0;//avoid NaN, if so will not move
            if (TrainType == TrainType.Ai && LeadLocomotiveIndex == (Cars.Count - 1) && LastCar.Flipped)
                distanceM = -distanceM;
            DistanceTravelledM += (float)distanceM;

            SpeedMpS = 0;
            foreach (TrainCar car in Cars)
            {
                SpeedMpS += car.SpeedMpS;
                //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                //                 if (car1.Flipped)
                if (car.Flipped ^ (car.IsDriveable && car.Train.IsActualPlayerTrain && ((MSTSLocomotive)car).UsingRearCab))
                    car.SpeedMpS = -car.SpeedMpS;
            }
#if DEBUG_SPEED_FORCES
            Trace.TraceInformation(" ========================= Train Speed #1 (Train.cs) ======================================== ");
            Trace.TraceInformation("Total Raw Speed {0} Train Speed {1}", SpeedMpS, SpeedMpS / Cars.Count);
#endif
            // This next statement looks odd - how can you find the updated speed of the train just by averaging the speeds of
            // the individual TrainCars? No problem if all the TrainCars had equal masses but, if they differ, then surely
            // you must find the total force on the train and then divide by the total mass?
            // Not to worry as comparison with those totals shows that this statement does indeed give a correct updated speed !
            //
            // The reason, I believe, is that when the train forces are balanced (e.g. constant power on a constant gradient),
            // then the calculation of forces in the couplers works out them out so that all the TrainCars share the
            // same acceleration.
            //
            // The updated speed for each TrainCar is simply calculated from the mass of the TrainCar and the force on it but
            // the force on it was previously such that all the TrainCars have the same acceleration. There is little need to
            // add them up and average them, as they only differ when the train forces are out of balance - Chris Jakeman 4-Mar-2019
            SpeedMpS /= Cars.Count;

            SlipperySpotDistanceM -= SpeedMpS * (float)elapsedClockSeconds;
            if (ControlMode != TrainControlMode.TurnTable)
                CalculatePositionOfCars(elapsedClockSeconds, distanceM);

            // calculate projected speed
            if (elapsedClockSeconds < AccelerationMpSpS.SmoothPeriodS)
                AccelerationMpSpS.Update(elapsedClockSeconds, (SpeedMpS - LastSpeedMpS) / elapsedClockSeconds);
            LastSpeedMpS = SpeedMpS;
            ProjectedSpeedMpS = SpeedMpS + 60 * (float)AccelerationMpSpS.SmoothedValue;
            ProjectedSpeedMpS = SpeedMpS > float.Epsilon ?
                Math.Max(0, ProjectedSpeedMpS) : SpeedMpS < -float.Epsilon ? Math.Min(0, ProjectedSpeedMpS) : 0;
        }

        /// Update Wind components for the train
        private void UpdateWindComponents()
        {
            // Gets wind direction and speed, and determines HUD display values for the train as a whole. 
            //These will be representative of the train whilst it is on a straight track, but each wagon will vary when going around a curve.
            // Note both train and wind direction will be positive between 0 (north) and 180 (south) through east, and negative between 0 (north) and 180 (south) through west
            // Wind and train direction to be converted to an angle between 0 and 360 deg.
            if (TrainWindResistanceDependent)
            {
                // Calculate Wind speed and direction, and train direction
                // Update the value of the Wind Speed and Direction for the train
                PhysicsWindDirectionDeg = MathHelper.ToDegrees(simulator.Weather.WindDirection);
                PhysicsWindSpeedMpS = simulator.Weather.WindSpeed;
                float TrainSpeedMpS = Math.Abs(SpeedMpS);

                // If a westerly direction (ie -ve) convert to an angle between 0 and 360
                if (PhysicsWindDirectionDeg < 0)
                    PhysicsWindDirectionDeg += 360;

                if (PhysicsTrainLocoDirectionDeg < 0)
                    PhysicsTrainLocoDirectionDeg += 360;

                // calculate angle between train and eind direction
                if (PhysicsWindDirectionDeg > PhysicsTrainLocoDirectionDeg)
                    ResultantWindComponentDeg = PhysicsWindDirectionDeg - PhysicsTrainLocoDirectionDeg;
                else if (PhysicsTrainLocoDirectionDeg > PhysicsWindDirectionDeg)
                    ResultantWindComponentDeg = PhysicsTrainLocoDirectionDeg - PhysicsWindDirectionDeg;
                else
                    ResultantWindComponentDeg = 0.0f;

                // Correct wind direction if it is greater then 360 deg, then correct to a value less then 360
                ResultantWindComponentDeg %= 360.0f;

                // Wind angle should be kept between 0 and 180 the formulas do not cope with angles > 180. If angle > 180, denotes wind of "other" side of train
                if (ResultantWindComponentDeg > 180)
                    ResultantWindComponentDeg = 360 - ResultantWindComponentDeg;

                float WindAngleRad = MathHelper.ToRadians(ResultantWindComponentDeg);

                WindResultantSpeedMpS = (float)Math.Sqrt(TrainSpeedMpS * TrainSpeedMpS + PhysicsWindSpeedMpS * PhysicsWindSpeedMpS + 2.0f * TrainSpeedMpS * PhysicsWindSpeedMpS * (float)Math.Cos(WindAngleRad));

            }
            else
            {
                WindResultantSpeedMpS = Math.Abs(SpeedMpS);
            }
        }

        /// Update Auxiliary Tenders added to train
        private void UpdateAuxTender()
        {
            if (!(Cars[0] is MSTSSteamLocomotive mstsSteamLocomotive))
                return;  // Don't process if locomotive is not steam locomotive

            bool auxTenderFound = false;    // Flag to confirm that there is still an auxiliary tender in consist
                                            // Calculate when an auxiliary tender is coupled to train
            for (int i = 0; i < Cars.Count; i++)
            {
                if (Cars[i].AuxWagonType == AuxWagonType.AuxiliaryTender && i > LeadLocomotiveIndex && IsPlayerDriven)  // If value has been entered for auxiliary tender & AuxTender car value is greater then the lead locomotive & and it is player driven
                {
                    if (Cars[i - 1].AuxWagonType == AuxWagonType.Tender || Cars[i - 1].AuxWagonType == AuxWagonType.Engine)  // Aux tender found in consist
                    {
                        if (simulator.Activity != null) // If an activity check to see if fuel presets are used.
                        {
                            if (!mstsSteamLocomotive.AuxTenderMoveFlag)  // If locomotive hasn't moved and Auxtender connected use fuel presets on aux tender
                            {
                                MaxAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                                mstsSteamLocomotive.CurrentAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG * (simulator.Activity.Activity.Header.FuelWater / 100.0f); // 
                                IsAuxTenderCoupled = true;      // Flag to advise MSTSSteamLovcomotive that tender is set.
                                auxTenderFound = true;      // Auxililary tender found in consist.

                            }
                            else     // Otherwise assume aux tender not connected at start of activity and therefore full value of water mass available when connected.
                            {
                                MaxAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                                mstsSteamLocomotive.CurrentAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                                IsAuxTenderCoupled = true;
                                auxTenderFound = true;      // Auxililary tender found in consist.
                            }
                        }
                        else  // In explore mode set aux tender to full water value
                        {
                            MaxAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                            mstsSteamLocomotive.CurrentAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                            IsAuxTenderCoupled = true;
                            auxTenderFound = true;      // Auxililary tender found in consist.

                        }
                    }
                    else // Aux tender not found in consist
                    {
                        MaxAuxTenderWaterMassKG = 0.0f;
                        IsAuxTenderCoupled = false;
                    }

                }

#if DEBUG_AUXTENDER
                    Trace.TraceInformation("=============================== DEBUG_AUXTENDER (Train.cs) ==============================================================");
                   // Trace.TraceInformation("Activity Fuel Value {0}", ActivityFuelLevel);
                    Trace.TraceInformation("CarID {0} AuxWagonType {1} LeadLocomotive {2} Max WaterMass {3} Current Water Mass {4}", i, Cars[i].AuxWagonType, LeadLocomotiveIndex, MaxAuxTenderWaterMassKG, mstsSteamLocomotive.CurrentAuxTenderWaterMassKG);
                    Trace.TraceInformation("Prev {0} Coupled {1}", PrevWagonType, IsAuxTenderCoupled);
#endif

            }

            if (!auxTenderFound && IsAuxTenderCoupled)     // If an auxiliary tender is not found in the consist, then assume that it has been uncoupled
            {
                MaxAuxTenderWaterMassKG = 0.0f;     // Reset values
                IsAuxTenderCoupled = false;
            }
        }

        /// Update Steam Heating - this model calculates the total train heat losses and gains for all the cars
        private void UpdateCarSteamHeat(double elapsedClockSeconds)
        {
            // The carriage steam heating model is based upon a description provided in a number of articles, including, "The Steam Heating of Railway Carriages" by Frank W. Marillier ( http://www.gwr.org.uk/links.html ),
            // and "Some Considerations on the Problem of the Heating of British Railway Coaches" by F. J. Pepper. (Journal Institution of Locomotive Engineers - Paper No. 568 - pg 13 -74)
            // Steam is carried the length of the train by a 2" or 1.5" steam pipe that is fitted between each of the cars. Rubber pressure hoses are used to couple the carriage steam pipes together. 
            // Typically these pipes would have some level of insulation on them.
            // Typically 2" bore steam pipes are then placed at strategic locations within the car. For cars with separate compartments, a steam pipe would need to be located in each compartment.
            // The length of each of these compartment steam pipes was determined by a rule of thumb based upon the volume of the space that it was desgined to heat, so for example, a 
            // 2nd class compartment, would have 1" of pipe length for each 3.5cu ft of compartment volume.
            // Steam entering into the passenger area of the train is typically reduced to atmospheric pressure??? 
            // Some articles suggest that a typical steam heating system could use approx 100lbs of steam per hour per car. (more detail)

            // The model calculates the heat capacity of each car at a default temperature, and the various heat losses, such as heat loss from the cars, main steam pipe, leaks, etc are 
            // subtracted from this value. Heat gain from the radiation heat exchange area are added to this value. If all is ok then a balance should be achieved.

            // Leaks in system, loss of heat (and pressure) as steam moves along train

            if (!(simulator.PlayerLocomotive is MSTSLocomotive mstsLocomotive))
                return;

            if (IsFirstTimeBoilerCarAttached)
            {
                foreach (TrainCar car in Cars)
                {
                    switch (car.WagonSpecialType)
                    {
                        case TrainCar.WagonSpecialTypes.HeatingBoiler:
                            HeatingBoilerCarAttached = true; // A steam heating boiler is fitted in a wagon
                            break;
                        case TrainCar.WagonSpecialTypes.Heated:
                            HeatedCarAttached = true; // A steam heating boiler is fitted in a wagon
                            break;
                    }
                }
                IsFirstTimeBoilerCarAttached = false;
            }

            // Check to confirm that train is player driven and has passenger cars in the consist. Steam heating is OFF if steam heat valve is closed and no pressure is present
            if (IsPlayerDriven && (PassengerCarsNumber > 0 || HeatedCarAttached) && (mstsLocomotive.IsSteamHeatFitted || HeatingBoilerCarAttached) && mstsLocomotive.CurrentSteamHeatPressurePSI > 0)
            {
                // Set default values required
                float steamFlowRateLbpHr = 0;
                float progressiveHeatAlongTrainBTU = 0;
                float connectSteamHoseLengthFt = 2.0f * 2.0f; // Assume two hoses on each car * 2 ft long

                // Calculate total heat loss and car temperature along the train
                foreach (TrainCar car in Cars)
                {
                    // Calculate volume in carriage - note height reduced by 1.06m to allow for bogies, etc
                    float BogieHeightM = 1.06f;

                    car.CarHeatVolumeM3 = car.CarWidthM * (car.CarLengthM) * (car.CarHeightM - BogieHeightM); // Check whether this needs to be same as compartment volume
                    car.CarOutsideTempC = TrainOutsideTempC;  // Update Outside temp

                    // Only initialise these values the first time around the loop
                    if (car.IsCarSteamHeatInitial)
                    {

                        // This section sets some arbitary default values the first time that this section is processed. Real values are set on subsequent loops, once steam heat is turned on in locomotive
                        if (TrainInsideTempC == 0)
                        {
                            TrainInsideTempC = car.DesiredCompartmentTempSetpointC; // Set intial temp - will be set in Steam and Diesel Eng, but these are done after this step
                        }

                        if (TrainOutsideTempC == 0)
                        {

                            TrainOutsideTempC = 10.0f; // Set intial temp - will be set in Steam and Diesel Eng, but these are done after this step
                        }

                        if (mstsLocomotive.EngineType == TrainCar.EngineTypes.Steam && simulator.Settings.HotStart || mstsLocomotive.EngineType == TrainCar.EngineTypes.Diesel || mstsLocomotive.EngineType == TrainCar.EngineTypes.Electric)
                        {
                            if (TrainOutsideTempC < car.DesiredCompartmentTempSetpointC)
                            {
                                car.CarCurrentCarriageHeatTempC = car.DesiredCompartmentTempSetpointC; // Set intial temp
                            }
                            else
                            {
                                car.CarCurrentCarriageHeatTempC = TrainOutsideTempC;
                            }
                        }
                        else
                        {
                            car.CarCurrentCarriageHeatTempC = TrainOutsideTempC;
                        }

                        // Calculate a random factor for steam heat leaks in connecting pipes
                        car.SteamHoseLeakRateRandom = Simulator.Random.Next(100) / 100.0f; // Achieves a two digit random number between 0 and 1
                        car.SteamHoseLeakRateRandom = MathHelper.Clamp(car.SteamHoseLeakRateRandom, 0.5f, 1.0f); // Keep Random Factor ratio within bounds

                        // Calculate Starting Heat value in Car Q = C * M * Tdiff, where C = Specific heat capacity, M = Mass ( Volume * Density), Tdiff - difference in temperature
                        car.TotalPossibleCarHeatW = (float)(Dynamics.Power.FromKW(SpecificHeatCapcityAirKJpKgK * DensityAirKgpM3 * car.CarHeatVolumeM3 * (car.CarCurrentCarriageHeatTempC - TrainOutsideTempC)));

                        //Trace.TraceInformation("Initialise TotalCarHeat - CarID {0} Possible {1} Max {2} Out {3} Vol {4} Density {5} Specific {6}", car.CarID, car.TotalPossibleCarHeatW, car.CarCurrentCarriageHeatTempC, TrainOutsideTempC, car.CarHeatVolumeM3, DensityAirKgpM3, SpecificHeatCapcityAirKJpKgC);

                        // Initialise current Train Steam Heat based upon selected Current carriage Temp
                        car.CarHeatCurrentCompartmentHeatW = car.TotalPossibleCarHeatW;
                        car.IsCarSteamHeatInitial = false;
                    }

                    // Heat loss due to train movement and air flow, based upon convection heat transfer information - http://www.engineeringtoolbox.com/convective-heat-transfer-d_430.html
                    // The formula on this page ( hc = 10.45 - v + 10v1/2), where v = m/s. This formula is used to develop a multiplication factor with train speed.
                    // Curve is only valid between 2.0m/s and 20.0m/s

                    float LowSpeedMpS = 2.0f;
                    float HighSpeedMpS = 20.0f;
                    float ConvHeatTxfMinSpeed = 10.45f - LowSpeedMpS + (10.0f * (float)Math.Pow(LowSpeedMpS, 0.5));
                    float ConvHeatTxfMaxSpeed = 10.45f - HighSpeedMpS + (10.0f * (float)Math.Pow(HighSpeedMpS, 0.5));
                    float ConvHeatTxActualSpeed = 10.45f - car.AbsSpeedMpS + (10.0f * (float)Math.Pow(car.AbsSpeedMpS, 0.5));
                    float ConvFactor = 0;

                    if (car.AbsSpeedMpS > 2 && car.AbsSpeedMpS < 20.0f)
                    {
                        ConvFactor = ConvHeatTxActualSpeed / ConvHeatTxfMinSpeed; // Calculate fraction only between 2 and 20
                    }
                    else if (car.AbsSpeedMpS < 2)
                    {
                        ConvFactor = 1.0f; // If speed less then 2m/s then set fracftion to give stationary Kc value 
                    }
                    else
                    {
                        ConvFactor = ConvHeatTxActualSpeed / ConvHeatTxfMinSpeed; // Calculate constant fraction over 20m/s
                    }
                    ConvFactor = MathHelper.Clamp(ConvFactor, 1.0f, 1.6f); // Keep Conv Factor ratio within bounds - should not exceed 1.6.


                    if (car.WagonType == TrainCar.WagonTypes.Passenger || car.WagonSpecialType == MSTSWagon.WagonSpecialTypes.Heated) // Only calculate compartment heat in passenger or specially marked heated cars
                    {

                        // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
                        // Calculate heat loss from inside the carriage
                        // Initialise car values for heating to zero
                        car.TotalCarCompartmentHeatLossWpT = 0.0f;
                        car.CarHeatCompartmentPipeAreaM2 = 0.0f;
                        car.CarHeatVolumeM3 = 0.0f;
                        float HeatLossTransmissionWpT = 0;

                        // Transmission heat loss = exposed area * heat transmission coeff (inside temp - outside temp)
                        // Calculate the heat loss through the roof, wagon sides, and floor separately  
                        // Calculate the heat loss through the carriage sides, per degree of temp change
                        // References - https://www.engineeringtoolbox.com/heat-loss-transmission-d_748.html  and https://www.engineeringtoolbox.com/heat-loss-buildings-d_113.html
                        float HeatTransCoeffRoofWm2C = 1.7f * ConvFactor; // 2 inch wood - uninsulated
                        float HeatTransCoeffEndsWm2C = 0.9f * ConvFactor; // 2 inch wood - insulated - this compensates for the fact that the ends of the cars are somewhat protected from the environment
                        float HeatTransCoeffSidesWm2C = 1.7f * ConvFactor; // 2 inch wood - uninsulated
                        float HeatTransCoeffWindowsWm2C = 4.7f * ConvFactor; // Single glazed glass window in wooden frame
                        float HeatTransCoeffFloorWm2C = 2.5f * ConvFactor; // uninsulated floor

                        // Calculate volume in carriage - note height reduced by 1.06m to allow for bogies, etc
                        float CarCouplingPipeM = 1.2f;  // Allow for connection between cars (assume 2' each end) - no heat is contributed to carriages.

                        // Calculate the heat loss through the roof, allow 15% additional heat loss through roof because of radiation to space
                        float RoofHeatLossFactor = 1.15f;
                        float HeatLossTransRoofWpT = RoofHeatLossFactor * (car.CarWidthM * (car.CarLengthM - CarCouplingPipeM)) * HeatTransCoeffRoofWm2C * (car.CarCurrentCarriageHeatTempC - car.CarOutsideTempC);

                        // Each car will have 2 x sides + 2 x ends. Each side will be made up of solid walls, and windows. A factor has been assumed to determine the ratio of window area to wall area.
                        float HeatLossTransWindowsWpT = (car.WindowDeratingFactor * (car.CarHeightM - BogieHeightM) * (car.CarLengthM - CarCouplingPipeM)) * HeatTransCoeffWindowsWm2C * (car.CarCurrentCarriageHeatTempC - car.CarOutsideTempC);
                        float HeatLossTransSidesWpT = ((1.0f - car.WindowDeratingFactor) * (car.CarHeightM - BogieHeightM) * (car.CarLengthM - CarCouplingPipeM)) * HeatTransCoeffSidesWm2C * (car.CarCurrentCarriageHeatTempC - car.CarOutsideTempC);
                        float HeatLossTransEndsWpT = ((car.CarHeightM - BogieHeightM) * (car.CarLengthM - CarCouplingPipeM)) * HeatTransCoeffEndsWm2C * (car.CarCurrentCarriageHeatTempC - car.CarOutsideTempC);

                        // Total equals 2 x sides, ends, windows
                        float HeatLossTransTotalSidesWpT = (2.0f * HeatLossTransWindowsWpT) + (2.0f * HeatLossTransSidesWpT) + (2.0f * HeatLossTransEndsWpT);

                        // Calculate the heat loss through the floor
                        float HeatLossTransFloorWpT = (car.CarWidthM * (car.CarLengthM - CarCouplingPipeM)) * HeatTransCoeffFloorWm2C * (car.CarCurrentCarriageHeatTempC - car.CarOutsideTempC);

                        HeatLossTransmissionWpT = HeatLossTransRoofWpT + HeatLossTransTotalSidesWpT + HeatLossTransFloorWpT;

                        // ++++++++++++++++++++++++
                        // Ventilation Heat loss, per degree of temp change
                        // This will occur when the train is stopped at the station and prior to being ready to depart. Typically will only apply in activity mode, and not explore mode
                        float HeatLossVentilationWpT = 0;
                        float HeatRecoveryEfficiency = 0.5f; // Assume a HRF of 50%
                        float AirFlowVolumeM3pS = car.CarHeatVolumeM3 / 300.0f; // Assume that the volume of the car is emptied over a period of 5 minutes

                        if (AtStation) // When train is at station.
                        {
                            // If the train is ready to depart, assume all doors are closed, and hence no ventilation loss
                            HeatLossVentilationWpT = MayDepart ? 0 : (float)Dynamics.Power.FromKW((1.0f - HeatRecoveryEfficiency) * SpecificHeatCapcityAirKJpKgK * DensityAirKgpM3 * AirFlowVolumeM3pS * (car.CarCurrentCarriageHeatTempC - car.CarOutsideTempC));
                        }

                        // ++++++++++++++++++++++++
                        // Infiltration Heat loss, per degree of temp change
                        float NumAirShiftspSec = (float)Frequency.Periodic.FromHours(10.0);      // Pepper article suggests that approx 14 air changes per hour happen for a train that is moving @ 50mph, use and av figure of 10.0.
                        float HeatLossInfiltrationWpT = 0;
                        car.CarHeatVolumeM3 = car.CarWidthM * (car.CarLengthM - CarCouplingPipeM) * (car.CarHeightM - BogieHeightM);
                        HeatLossInfiltrationWpT = (float)(Dynamics.Power.FromKW(SpecificHeatCapcityAirKJpKgK * DensityAirKgpM3 * NumAirShiftspSec * car.CarHeatVolumeM3 * (car.CarCurrentCarriageHeatTempC - car.CarOutsideTempC)));

                        car.TotalCarCompartmentHeatLossWpT = HeatLossTransmissionWpT + HeatLossInfiltrationWpT + HeatLossVentilationWpT;

                        //++++++++++++++++++++++++++++++++++++++++
                        // Calculate heat produced by steam pipe acting as heat exchanger inside carriage - this model is based upon the heat loss from a steam pipe. 
                        // The heat loss per metre from a bare pipe equals the heat loss by convection and radiation. Temperatures in degrees Kelvin
                        // QConv = hc * A * (Tp - To), where hc = convection coeff, A = surface area of pipe, Tp = pipe temperature, To = temperature of air around the pipe
                        // QRad = % * A * e * (Tp^4 - To^4), where % = Boltzmans constant, A = surface area of pipe, Tp^4 = pipe temperature, To^4 = temperature of air around the pipe, e = emissivity factor

                        // Calculate steam pipe surface area
                        float CompartmentSteamPipeRadiusM = (float)(Size.Length.FromIn(2.375f) / 2.0f);  // Assume the steam pipes in the compartments have  have internal diameter of 2" (50mm) - external = 2.375"
                        float DoorSteamPipeRadiusM = (float)(Size.Length.FromIn(2.75f) / 2.0f);        // Assume the steam pipes in the doors have diameter of 1.75" (50mm) - assume external = 2.0"

                        // Assume door pipes are 3' 4" (ie 3.3') long, and that there are doors at both ends of the car, ie x 2
                        float CarDoorLengthM = 2.0f * (float)(Size.Length.FromFt(3.3f));
                        float CarDoorVolumeM3 = car.CarWidthM * CarDoorLengthM * (car.CarHeightM - BogieHeightM);

                        float CarDoorPipeAreaM2 = 2.0f * MathHelper.Pi * DoorSteamPipeRadiusM * CarDoorLengthM;

                        // Use rule of thumb - 1" of 2" steam heat pipe for every 3.0 cu ft of volume in car compartment (third class)
                        float CarCompartmentPipeLengthM = (float)(Size.Length.FromIn((car.CarHeatVolumeM3 - CarDoorVolumeM3) / (Size.Volume.FromFt3(car.CompartmentHeatingPipeAreaFactor))));
                        float CarCompartmentPipeAreaM2 = 2.0f * MathHelper.Pi * CompartmentSteamPipeRadiusM * CarCompartmentPipeLengthM;

                        car.CarHeatCompartmentPipeAreaM2 = CarCompartmentPipeAreaM2 + CarDoorPipeAreaM2;

                        // Pipe convection heat produced - steam is reduced to atmospheric pressure when it is injected into compartment
                        float CompartmentSteamPipeTempC = (float)Temperature.Celsius.FromF(mstsLocomotive.SteamHeatPressureToTemperaturePSItoF[0]);
                        car.CarCompartmentSteamPipeHeatConvW = (PipeHeatTransCoeffWpM2K * car.CarHeatCompartmentPipeAreaM2 * (CompartmentSteamPipeTempC - car.CarCurrentCarriageHeatTempC));

                        // Pipe radiation heat produced
                        float PipeTempAK = (float)Math.Pow(Temperature.Kelvin.FromF(CompartmentSteamPipeTempC), 4.0f);
                        float PipeTempBK = (float)Math.Pow(Temperature.Celsius.ToK(car.CarCurrentCarriageHeatTempC), 4.0f);
                        car.CarCompartmentSteamHeatPipeRadW = (BoltzmanConstPipeWpM2 * EmissivityFactor * car.CarHeatCompartmentPipeAreaM2 * (PipeTempAK - PipeTempBK));

                        car.CarHeatCompartmentSteamPipeHeatW = car.CarCompartmentSteamHeatPipeRadW + car.CarCompartmentSteamPipeHeatConvW;

                    }

                    //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
                    // Calculate heating loss in main supply pipe that runs under carriage

                    // Set heat trans coeff
                    float HeatTransCoeffMainPipeBTUpFt2pHrpF = 0.4f * ConvFactor; // insulated pipe - BTU / sq.ft. / hr / l in / °F.
                    float HeatTransCoeffConnectHoseBTUpFt2pHrpF = 0.04f * ConvFactor; // rubber connecting hoses - BTU / sq.ft. / hr / l in / °F. TO BE CHECKED

                    // Calculate Length of carriage and heat loss in main steam pipe
                    float CarMainSteamPipeTempF = (float)mstsLocomotive.SteamHeatPressureToTemperaturePSItoF[car.CarSteamHeatMainPipeSteamPressurePSI];
                    car.CarHeatSteamMainPipeHeatLossBTU = (float)(Size.Length.ToFt(car.CarLengthM) * (MathHelper.Pi * Size.Length.ToFt(car.MainSteamHeatPipeOuterDiaM)) * HeatTransCoeffMainPipeBTUpFt2pHrpF * (CarMainSteamPipeTempF - Temperature.Celsius.ToF(car.CarOutsideTempC)));

                    // calculate steam connecting hoses heat loss - assume 1.5" hose
                    float ConnectSteamHoseOuterDiaFt = (float)Size.Length.ToFt(car.CarConnectSteamHoseOuterDiaM);
                    car.CarHeatConnectSteamHoseHeatLossBTU = (float)(connectSteamHoseLengthFt * (MathHelper.Pi * ConnectSteamHoseOuterDiaFt) * HeatTransCoeffConnectHoseBTUpFt2pHrpF * (CarMainSteamPipeTempF - Temperature.Celsius.ToF(car.CarOutsideTempC)));

                    // Use Napier formula to calculate steam discharge rate through steam trap valve, ie Discharge (lb/s) = (Valve area * Abs Pressure) / 70
                    const float SteamTrapValveDischargeFactor = 70.0f;

                    // Find area of pipe - assume 0.1875" (3/16") dia steam trap
                    float SteamTrapDiaIn = 0.1875f;
                    float SteamTrapValveSizeAreaIn2 = (float)Math.PI * (SteamTrapDiaIn / 2.0f) * (SteamTrapDiaIn / 2.0f);

                    car.CarHeatSteamTrapUsageLBpS = (SteamTrapValveSizeAreaIn2 * (car.CarSteamHeatMainPipeSteamPressurePSI + OneAtmospherePSI)) / SteamTrapValveDischargeFactor;

                    // Use Napier formula to calculate steam discharge rate through steam leak in connecting hose, ie Discharge (lb/s) = (Valve area * Abs Pressure) / 70
                    const float ConnectingHoseDischargeFactor = 70.0f;

                    // Find area of pipe - assume 0.1875" (3/16") dia steam trap
                    float ConnectingHoseLeakDiaIn = 0.1875f;
                    float ConnectingHoseLeakAreaIn2 = (float)Math.PI * (ConnectingHoseLeakDiaIn / 2.0f) * (ConnectingHoseLeakDiaIn / 2.0f);

                    car.CarHeatConnectingSteamHoseLeakageLBpS = car.SteamHoseLeakRateRandom * (ConnectingHoseLeakAreaIn2 * (car.CarSteamHeatMainPipeSteamPressurePSI + OneAtmospherePSI)) / ConnectingHoseDischargeFactor;

                    //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

                    float CurrentComparmentSteamPipeHeatW = 0;

                    // Calculate total steam loss along main pipe, by calculating heat into steam pipe at locomotive, deduct heat loss for each car, 
                    // note if pipe pressure drops, then compartment heating will stop
                    if (car.CarSteamHeatMainPipeSteamPressurePSI >= 1 && car.CarHeatCompartmentHeaterOn && (car.WagonType == TrainCar.WagonTypes.Passenger || car.WagonSpecialType == MSTSWagon.WagonSpecialTypes.Heated))
                    {
                        // If main pipe pressure is > 0 then heating will start to occur in comparment, so include compartment heat exchanger value
                        progressiveHeatAlongTrainBTU += (float)((car.CarHeatSteamMainPipeHeatLossBTU + car.CarHeatConnectSteamHoseHeatLossBTU) + Frequency.Periodic.ToHours(Dynamics.Power.ToBTUpS(car.CarHeatCompartmentSteamPipeHeatW)));
                        CurrentComparmentSteamPipeHeatW = car.CarHeatCompartmentSteamPipeHeatW; // Car is being heated as main pipe pressure is high enough, and temperature increase is required
                        car.SteamHeatingCompartmentSteamTrapOn = true; // turn on the compartment steam traps
                    }
                    else
                    {
                        // If main pipe pressure is < 0 or temperature in compartment is above the desired temeperature,
                        // then no heating will occur in comparment, so leave compartment heat exchanger value out
                        progressiveHeatAlongTrainBTU += (car.CarHeatSteamMainPipeHeatLossBTU + car.CarHeatConnectSteamHoseHeatLossBTU);
                        CurrentComparmentSteamPipeHeatW = 0; // Car is not being heated as main pipe pressure is not high enough, or car temp is hot enough
                        car.SteamHeatingCompartmentSteamTrapOn = false; // turn off the compartment steam traps
                    }

                    // Calculate steam flow rates and steam used
                    steamFlowRateLbpHr = (float)((progressiveHeatAlongTrainBTU / mstsLocomotive.SteamHeatPSItoBTUpLB[mstsLocomotive.CurrentSteamHeatPressurePSI]) + Frequency.Periodic.ToHours(car.CarHeatSteamTrapUsageLBpS) + Frequency.Periodic.ToHours(car.CarHeatConnectingSteamHoseLeakageLBpS));
                    mstsLocomotive.CalculatedCarHeaterSteamUsageLBpS = (float)Frequency.Periodic.FromHours(steamFlowRateLbpHr);

                    // Calculate Net steam heat loss or gain for each compartment in the car
                    car.CarNetSteamHeatLossWpTime = CurrentComparmentSteamPipeHeatW - car.TotalCarCompartmentHeatLossWpT;

                    car.DisplayTrainNetSteamHeatLossWpTime = car.CarNetSteamHeatLossWpTime;

                    // Given the net heat loss the car calculate the current heat capacity, and corresponding temperature
                    if (car.CarNetSteamHeatLossWpTime < 0)
                    {
                        car.CarNetSteamHeatLossWpTime = -1.0f * car.CarNetSteamHeatLossWpTime; // If steam heat loss is negative, convert to a positive number
                        car.CarHeatCurrentCompartmentHeatW -= (float)(car.CarNetSteamHeatLossWpTime * elapsedClockSeconds);  // Losses per elapsed time
                    }
                    else
                    {

                        car.CarHeatCurrentCompartmentHeatW += (float)(car.CarNetSteamHeatLossWpTime * elapsedClockSeconds);  // Gains per elapsed time         
                    }

                    car.CarCurrentCarriageHeatTempC = (float)(Dynamics.Power.ToKW(car.CarHeatCurrentCompartmentHeatW) / (SpecificHeatCapcityAirKJpKgK * DensityAirKgpM3 * car.CarHeatVolumeM3) + TrainOutsideTempC);

                    float DesiredCompartmentTempResetpointC = car.DesiredCompartmentTempSetpointC - 2.5f; // Allow 2.5Deg bandwidth for temperature

                    if (car.CarCurrentCarriageHeatTempC > car.DesiredCompartmentTempSetpointC)
                    {
                        car.CarHeatCompartmentHeaterOn = false;
                    }
                    else if (car.CarCurrentCarriageHeatTempC < DesiredCompartmentTempResetpointC)
                    {
                        car.CarHeatCompartmentHeaterOn = true;
                    }

                    if (car.CarCurrentCarriageHeatTempC < desiredCompartmentAlarmTempSetpointC) // If temp below 45of then alarm
                    {
                        if (!IsSteamHeatLow)
                        {
                            IsSteamHeatLow = true;
                            // Provide warning message if temperature is too hot
                            if (car.WagonType == TrainCar.WagonTypes.Passenger)
                            {
                                simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Carriage {0} temperature is too cold, the passengers are freezing.", car.CarID));
                            }
                            else
                            {
                                simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Car {0} temperature is too cold for the freight.", car.CarID));
                            }
                        }
                    }
                    else if (car.CarCurrentCarriageHeatTempC > resetCompartmentAlarmTempSetpointC)
                    {
                        IsSteamHeatLow = false;        // Reset temperature warning
                    }
                }

                #region Calculate Steam Pressure drop along train

                // Initialise main steam pipe pressure to same as steam heat valve setting
                float ProgressivePressureAlongTrainPSI = mstsLocomotive.CurrentSteamHeatPressurePSI;

                // Calculate pressure drop along whole train
                foreach (TrainCar car in Cars)
                {
                    // Calculate pressure drop in pipe along train. This calculation is based upon the Unwin formula - https://www.engineeringtoolbox.com/steam-pressure-drop-calculator-d_1093.html
                    // dp = 0.0001306 * q^2 * L * (1 + 3.6/d) / (3600 * ρ * d^5)
                    // where dp = pressure drop (psi), q = steam flow rate(lb/ hr), L = length of pipe(ft), d = pipe inside diameter(inches), ρ = steam density(lb / ft3)
                    // Use values for the specific volume corresponding to the average pressure if the pressure drop exceeds 10 - 15 % of the initial absolute pressure

                    float HeatPipePressureDropPSI = (float)((0.0001306f * steamFlowRateLbpHr * steamFlowRateLbpHr * Size.Length.ToFt(car.CarLengthM) * (1 + 3.6f / 2.5f)) / (3600 * mstsLocomotive.SteamDensityPSItoLBpFT3[mstsLocomotive.CurrentSteamHeatPressurePSI] * (float)Math.Pow(car.MainSteamHeatPipeInnerDiaM, 5.0f)));
                    float ConnectHosePressureDropPSI = (float)((0.0001306f * steamFlowRateLbpHr * steamFlowRateLbpHr * connectSteamHoseLengthFt * (1 + 3.6f / 2.5f)) / (3600 * mstsLocomotive.SteamDensityPSItoLBpFT3[mstsLocomotive.CurrentSteamHeatPressurePSI] * (float)Math.Pow(car.CarConnectSteamHoseInnerDiaM, 5.0f)));
                    float CarPressureDropPSI = HeatPipePressureDropPSI + ConnectHosePressureDropPSI;

                    ProgressivePressureAlongTrainPSI -= CarPressureDropPSI;
                    if (ProgressivePressureAlongTrainPSI < 0)
                    {
                        ProgressivePressureAlongTrainPSI = 0; // Make sure that pressure never goes negative
                    }
                    car.CarSteamHeatMainPipeSteamPressurePSI = ProgressivePressureAlongTrainPSI;

                    // For the boiler heating car adjust mass based upon fuel and water usage
                    if (car.WagonSpecialType == TrainCar.WagonSpecialTypes.HeatingBoiler)
                    {

                        // Don't process if water or fule capacities are low
                        if (mstsLocomotive.CurrentSteamHeatPressurePSI > 0 && car.CurrentSteamHeatBoilerFuelCapacityL > 0 && car.CurrentCarSteamHeatBoilerWaterCapacityL > 0 && !car.IsSteamHeatBoilerLockedOut)
                        {
                            // Test boiler steam capacity can deliever steam required for the system
                            if (mstsLocomotive.CalculatedCarHeaterSteamUsageLBpS > car.MaximumSteamHeatingBoilerSteamUsageRateLbpS)
                            {
                                car.IsSteamHeatBoilerLockedOut = true; // Lock steam heat boiler out is steam usage exceeds capacity
                                simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("The steam usage has exceeded the capacity of the steam boiler. Steam boiler locked out."));
                                Trace.TraceInformation("Steam heat boiler locked out as capacity exceeded");
                            }

                            // Calculate fuel usage for steam heat boiler
                            double FuelUsageLpS = Size.LiquidVolume.FromGallonUK(Frequency.Periodic.FromHours(car.TrainHeatBoilerFuelUsageGalukpH[Frequency.Periodic.ToHours(mstsLocomotive.CalculatedCarHeaterSteamUsageLBpS)]));
                            double FuelOilConvertLtoKg = 0.85f;
                            car.CurrentSteamHeatBoilerFuelCapacityL -= (float)(FuelUsageLpS * elapsedClockSeconds); // Reduce tank capacity as fuel used.
                            car.MassKG -= (float)(FuelUsageLpS * elapsedClockSeconds * FuelOilConvertLtoKg); // Reduce locomotive weight as Steam heat boiler uses fuel.

                            // Calculate water usage for steam heat boiler
                            double WaterUsageLpS = Size.LiquidVolume.FromGallonUK(Frequency.Periodic.FromHours(car.TrainHeatBoilerWaterUsageGalukpH[Frequency.Periodic.ToHours(mstsLocomotive.CalculatedCarHeaterSteamUsageLBpS)]));
                            car.CurrentCarSteamHeatBoilerWaterCapacityL -= (float)(WaterUsageLpS * elapsedClockSeconds); // Reduce tank capacity as water used.
                            car.MassKG -= (float)(WaterUsageLpS * elapsedClockSeconds); // Reduce locomotive weight as Steam heat boiler uses water - NB 1 litre of water = 1 kg.
                        }
                    }
                }
                #endregion
            }
        }

        /// ProcessTunnels : check position of each car in train wrt tunnel
        protected void ProcessTunnels()
        {
            // start at front of train
            int sectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            float sectionOffset = PresentPosition[Direction.Forward].Offset;
            TrackDirection sectionDirection = PresentPosition[Direction.Forward].Direction;

            foreach (TrainCar car in Cars)
            {
                float usedCarLength = car.CarLengthM;
                float processedCarLength = 0;
                bool validSections = true;

                float? FrontCarPositionInTunnel = null;
                float? FrontCarLengthOfTunnelAhead = null;
                float? RearCarLengthOfTunnelBehind = null;
                int numTunnelPaths = 0;

                while (validSections)
                {
                    TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[sectionIndex];
                    bool inTunnel = false;

                    // car spans sections
                    if ((car.CarLengthM - processedCarLength) > sectionOffset)
                    {
                        usedCarLength = sectionOffset - processedCarLength;
                    }

                    // section has tunnels
                    foreach (TunnelInfoData tunnel in section.TunnelInfo ?? Enumerable.Empty<TunnelInfoData>())
                    {
                        float tunnelStartOffset = tunnel.Start[sectionDirection];
                        float tunnelEndOffset = tunnel.End[sectionDirection];

                        if (tunnelStartOffset > 0 && tunnelStartOffset > sectionOffset)      // start of tunnel is in section beyond present position - cannot be in this tunnel nor any following
                        {
                            break;
                        }

                        if (tunnelEndOffset > 0 && tunnelEndOffset < (sectionOffset - usedCarLength)) // beyond end of tunnel, test next
                        {
                            continue;
                        }

                        if (tunnelStartOffset <= 0 || tunnelStartOffset < (sectionOffset - usedCarLength)) // start of tunnel is behind
                        {
                            if (tunnelEndOffset < 0) // end of tunnel is out of this section
                            {
                                if (processedCarLength != 0)
                                {
                                    Trace.TraceInformation($"Train : {Name}; found tunnel in section {sectionIndex} with End < 0 while processed length : {processedCarLength}");
                                }
                            }

                            inTunnel = true;

                            numTunnelPaths = tunnel.NumberPaths;

                            // get position in tunnel
                            if (tunnelStartOffset < 0)
                            {
                                FrontCarPositionInTunnel = sectionOffset + tunnel.SectionStartOffset[sectionDirection];
                                FrontCarLengthOfTunnelAhead = tunnel.LengthTotal - FrontCarPositionInTunnel;
                                RearCarLengthOfTunnelBehind = tunnel.LengthTotal - (FrontCarLengthOfTunnelAhead + car.CarLengthM);
                            }
                            else
                            {
                                FrontCarPositionInTunnel = sectionOffset - tunnelStartOffset;
                                FrontCarLengthOfTunnelAhead = tunnel.LengthTotal - FrontCarPositionInTunnel - processedCarLength;
                                RearCarLengthOfTunnelBehind = tunnel.LengthTotal - (FrontCarLengthOfTunnelAhead + car.CarLengthM);
                            }

                            break;  // only test one tunnel
                        }
                    }
                    // tested this section, any need to go beyond?

                    processedCarLength += usedCarLength;
                    if (inTunnel || processedCarLength >= car.CarLengthM)
                    {
                        validSections = false;  // end of while loop through sections
                        sectionOffset -= usedCarLength;   // position of next car in this section

                        car.CarTunnelData.FrontPositionBeyondStartOfTunnel = FrontCarPositionInTunnel.HasValue ? FrontCarPositionInTunnel : null;
                        car.CarTunnelData.LengthMOfTunnelAheadFront = FrontCarLengthOfTunnelAhead.HasValue ? FrontCarLengthOfTunnelAhead : null;
                        car.CarTunnelData.LengthMOfTunnelBehindRear = RearCarLengthOfTunnelBehind.HasValue ? RearCarLengthOfTunnelBehind : null;
                        car.CarTunnelData.numTunnelPaths = numTunnelPaths;
                    }
                    else
                    {
                        // go back one section
                        int thisSectionRouteIndex = ValidRoute[0].GetRouteIndexBackward(sectionIndex, PresentPosition[Direction.Forward].RouteListIndex);
                        if (thisSectionRouteIndex >= 0)
                        {
                            sectionIndex = thisSectionRouteIndex;
                            section = TrackCircuitSection.TrackCircuitList[sectionIndex];
                            sectionOffset = section.Length;  // always at end of next section
                            sectionDirection = ValidRoute[0][thisSectionRouteIndex].Direction;
                        }
                        else // ran out of train
                        {
                            validSections = false;

                            car.CarTunnelData.FrontPositionBeyondStartOfTunnel = FrontCarPositionInTunnel.HasValue ? FrontCarPositionInTunnel : null;
                            car.CarTunnelData.LengthMOfTunnelAheadFront = FrontCarLengthOfTunnelAhead.HasValue ? FrontCarLengthOfTunnelAhead : null;
                            car.CarTunnelData.LengthMOfTunnelBehindRear = RearCarLengthOfTunnelBehind.HasValue ? RearCarLengthOfTunnelBehind : null;
                            car.CarTunnelData.numTunnelPaths = numTunnelPaths;
                        }
                    }
                }
            }
        }

        /// Train speed evaluation logging - open file
        protected void CreateLogFile()
        {
            //Time, Train Speed, Max Speed, Signal Aspect, Elevation, Direction, Control Mode, Distance Travelled, Throttle, Brake, Dyn Brake, Gear

            StringBuilder builder = new StringBuilder();

            if (!string.IsNullOrEmpty(evaluationLogFile) && !File.Exists(evaluationLogFile))
            {
                if ((evaluationContent & EvaluationLogContents.Time) == EvaluationLogContents.Time)
                {
                    builder.Append("TIME");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Speed) == EvaluationLogContents.Speed)
                {
                    builder.Append("TRAINSPEED");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.MaxSpeed) == EvaluationLogContents.MaxSpeed)
                {
                    builder.Append("MAXSPEED");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.SignalAspect) == EvaluationLogContents.SignalAspect)
                {
                    builder.Append("SIGNALASPECT");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Elevation) == EvaluationLogContents.Elevation)
                {
                    builder.Append("ELEVATION");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Direction) == EvaluationLogContents.Direction)
                {
                    builder.Append("DIRECTION");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.ControlMode) == EvaluationLogContents.ControlMode)
                {
                    builder.Append("CONTROLMODE");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Distance) == EvaluationLogContents.Distance)
                {
                    builder.Append("DISTANCETRAVELLED");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Throttle) == EvaluationLogContents.Throttle)
                {
                    builder.Append("THROTTLEPERC");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Brake) == EvaluationLogContents.Brake)
                {
                    builder.Append("BRAKEPRESSURE");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.DynBrake) == EvaluationLogContents.DynBrake)
                {
                    builder.Append("DYNBRAKEPERC");
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Gear) == EvaluationLogContents.Gear)
                {
                    builder.Append("GEARINDEX");
                    builder.Append(Separator);
                }

                builder.Append('\n');

                try
                {
                    File.AppendAllText(evaluationLogFile, builder.ToString());
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Trace.TraceWarning("Cannot open required logfile : " + evaluationLogFile + " : " + e.Message);
                    evaluateTrainSpeed = false;
                }
            }
        }

        /// Train speed evaluation logging
        protected void LogTrainSpeed(double timeStamp)
        {
            //TODO 20201125 may run in separate thread
            if (lastLogTime + evaluationInterval >= timeStamp)
            {
                lastLogTime = timeStamp;

                // User settings flag indices :
                //Time, Train Speed, Max Speed, Signal Aspect, Elevation, Direction, Control Mode, Distance Travelled, Throttle, Brake, Dyn Brake, Gear

                StringBuilder builder = new StringBuilder();

                if ((evaluationContent & EvaluationLogContents.Time) == EvaluationLogContents.Time)
                {
                    builder.Append(FormatStrings.FormatTime(simulator.ClockTime));
                    builder.Append(Separator);
                }

                bool moveForward = (Math.Sign(SpeedMpS) >= 0);
                if ((evaluationContent & EvaluationLogContents.Speed) == EvaluationLogContents.Speed)
                {
                    builder.Append($"{Speed.MeterPerSecond.FromMpS(Math.Abs(SpeedMpS), simulator.MilepostUnitsMetric):0000.0}{Separator}");
                }

                if ((evaluationContent & EvaluationLogContents.MaxSpeed) == EvaluationLogContents.MaxSpeed)
                {
                    builder.Append($"{Speed.MeterPerSecond.FromMpS(AllowedMaxSpeedMpS, simulator.MilepostUnitsMetric):0000.0}{Separator}");
                }

                if ((evaluationContent & EvaluationLogContents.SignalAspect) == EvaluationLogContents.SignalAspect)
                {
                    if (moveForward)
                    {
                        builder.Append(NextSignalObject[0]?.SignalLR(SignalFunction.Normal).ToString() ?? "-");
                    }
                    else
                    {
                        builder.Append(NextSignalObject[1]?.SignalLR(SignalFunction.Normal).ToString() ?? "-");
                    }
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Elevation) == EvaluationLogContents.Elevation)
                {
                    builder.Append($"{(0 - simulator.PlayerLocomotive.CurrentElevationPercent):00.0}{Separator}");
                }

                if ((evaluationContent & EvaluationLogContents.Direction) == EvaluationLogContents.Direction)
                {
                    builder.Append(moveForward ? 'F' : 'B');
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.ControlMode) == EvaluationLogContents.ControlMode)
                {
                    builder.Append(ControlMode.ToString());
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Distance) == EvaluationLogContents.Distance)
                {
                    builder.Append($"{PresentPosition[Direction.Forward].DistanceTravelled:0.00}{Separator}");
                }

                if ((evaluationContent & EvaluationLogContents.Throttle) == EvaluationLogContents.Throttle)
                {
                    builder.Append($"{MUThrottlePercent:000}{Separator}");
                }

                if ((evaluationContent & EvaluationLogContents.Brake) == EvaluationLogContents.Brake)
                {
                    builder.Append($"{simulator.PlayerLocomotive.BrakeSystem.GetCylPressurePSI():000}{Separator}");
                }

                if ((evaluationContent & EvaluationLogContents.DynBrake) == EvaluationLogContents.DynBrake)
                {
                    builder.Append($"{MUDynamicBrakePercent:000}{Separator}");
                }

                if ((evaluationContent & EvaluationLogContents.Gear) == EvaluationLogContents.Gear)
                {
                    builder.Append($"{MUGearboxGearIndex:0}{Separator}");
                }

                builder.Append('\n');
                File.AppendAllText(evaluationLogFile, builder.ToString());
            }
        }

        /// Update in manual mode
        internal void UpdateManual(double elapsedClockSeconds)
        {
            _ = elapsedClockSeconds;
            UpdateTrainPosition();                                                                // position update                  //
            int SignalObjIndex = CheckSignalPassed(0, PresentPosition[Direction.Forward], PreviousPosition[Direction.Forward]);   // check if passed signal forward   //
            if (SignalObjIndex < 0)
            {
                SignalObjIndex = CheckSignalPassed(1, PresentPosition[Direction.Backward], PreviousPosition[Direction.Backward]);   // check if passed signal backward  //
            }
            if (SignalObjIndex >= 0)
            {
                Signal signalObject = signalRef.Signals[SignalObjIndex];

                //the following is added by CSantucci, applying also to manual mode what Jtang implemented for activity mode: after passing a manually forced signal,
                // system will take back control of the signal
                if (signalObject.HoldState == SignalHoldState.ManualPass || signalObject.HoldState == SignalHoldState.ManualApproach)
                    signalObject.HoldState = SignalHoldState.None;
            }
            UpdateSectionStateManual();                                                           // update track occupation          //
            UpdateManualMode(SignalObjIndex);                                                     // update route clearance           //
            // for manual, also includes signal update //
        }

        /// Update in explorer mode
        internal void UpdateExplorer(double elapsedClockSeconds)
        {
            _ = elapsedClockSeconds;
            UpdateTrainPosition();                                                                // position update                  //
            int SignalObjIndex = CheckSignalPassed(0, PresentPosition[Direction.Forward], PreviousPosition[Direction.Forward]);   // check if passed signal forward   //
            if (SignalObjIndex < 0)
            {
                SignalObjIndex = CheckSignalPassed(1, PresentPosition[Direction.Backward], PreviousPosition[Direction.Backward]);   // check if passed signal backward  //
            }
            if (SignalObjIndex >= 0)
            {
                Signal signalObject = signalRef.Signals[SignalObjIndex];

                //the following is added by CSantucci, applying also to explorer mode what Jtang implemented for activity mode: after passing a manually forced signal,
                // system will take back control of the signal
                if (signalObject.HoldState == SignalHoldState.ManualPass || signalObject.HoldState == SignalHoldState.ManualApproach)
                    signalObject.HoldState = SignalHoldState.None;
            }
            UpdateSectionStateExplorer();                                                         // update track occupation          //
            UpdateExplorerMode(SignalObjIndex);                                                   // update route clearance           //
            // for manual, also includes signal update //
        }

        /// Update in turntable mode
        internal void UpdateTurntable(double elapsedClockSeconds)
        {
            _ = elapsedClockSeconds;
            //           UpdateTrainPosition();                                                                // position update                  //
            if (LeadLocomotive != null && (LeadLocomotive.ThrottlePercent >= 1 || Math.Abs(LeadLocomotive.SpeedMpS) > 0.05 || !(LeadLocomotive.Direction == MidpointDirection.N
            || Math.Abs(MUReverserPercent) <= 1)) || ControlMode != TrainControlMode.TurnTable)
            // Go to emergency.
            {
                ((MSTSLocomotive)LeadLocomotive).SetEmergency(true);
            }
        }

        /// Post Init : perform all actions required to start
        internal virtual bool PostInit()
        {

            // if train has no valid route, build route over trainlength (from back to front)
            bool validPosition = InitialTrainPlacement();

            if (validPosition)
            {
                InitializeSignals(false);     // Get signal information - only if train has route //
                if (TrainType != TrainType.Static)
                    CheckDeadlock(ValidRoute[0], Number);    // Check deadlock against all other trains (not for static trains)
                if (TCRoute != null)
                    TCRoute.SetReversalOffset(Length, simulator.TimetableMode);

                AuxActionsContainer.SetAuxAction(this);
            }


            // set train speed logging flag (valid per activity, so will be restored after save)
            if (IsActualPlayerTrain)
            {
                SetTrainSpeedLoggingFlag();
            }

            return validPosition;
        }

        /// set train speed logging flag (valid per activity, so will be restored after save)
        protected void SetTrainSpeedLoggingFlag()
        {
            evaluateTrainSpeed = simulator.Settings.EvaluationTrainSpeed;
            evaluationInterval = simulator.Settings.EvaluationInterval;

            evaluationContent = simulator.Settings.EvaluationContent;

            // if logging required, derive filename and open file
            if (evaluateTrainSpeed)
            {
                evaluationLogFile = simulator.DeriveLogFile("Speed");
                if (string.IsNullOrEmpty(evaluationLogFile))
                {
                    evaluateTrainSpeed = false;
                }
                else
                {
                    CreateLogFile();
                }
            }
        }

        /// get aspect of next signal ahead
        protected SignalAspectState GetNextSignalAspect(int direction)
        {
            SignalAspectState thisAspect = SignalAspectState.Stop;
            if (NextSignalObject[direction] != null)
            {
                thisAspect = NextSignalObject[direction].SignalLR(SignalFunction.Normal);
            }

            return thisAspect;
        }

        /// initialize signal array
        internal void InitializeSignals(bool existingSpeedLimits)
        {
            Debug.Assert(signalRef != null, "Cannot InitializeSignals() without Simulator.Signals.");

            // to initialize, use direction 0 only
            // preset indices

            SignalObjectItems.Clear();
            IndexNextSignal = -1;
            IndexNextSpeedlimit = -1;

            //  set overall speed limits if these do not yet exist

            if (!existingSpeedLimits)
            {
                if ((TrainMaxSpeedMpS <= 0f) && (LeadLocomotive != null))
                    TrainMaxSpeedMpS = (LeadLocomotive as MSTSLocomotive).MaxSpeedMpS;
                AllowedMaxSpeedMpS = TrainMaxSpeedMpS;   // set default
                allowedMaxSpeedSignalMpS = TrainMaxSpeedMpS;   // set default
                allowedMaxTempSpeedLimitMpS = AllowedMaxSpeedMpS; // set default

                //  try to find first speed limits behind the train

                List<int> speedpostList = SignalEnvironment.ScanRoute(null, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                                PresentPosition[Direction.Backward].Direction, false, -1, false, true, false, false, false, false, false, true, false, IsFreight);

                if (speedpostList.Count > 0)
                {
                    Signal speedpost = signalRef.Signals[speedpostList[0]];
                    SpeedInfo speedInfo = speedpost.SpeedLimit(SignalFunction.Speed);

                    AllowedMaxSpeedMpS = Math.Min(AllowedMaxSpeedMpS, IsFreight ? speedInfo.FreightSpeed : speedInfo.PassengerSpeed);
                    allowedAbsoluteMaxSpeedLimitMpS = Math.Min(allowedAbsoluteMaxSpeedLimitMpS, IsFreight ? speedInfo.FreightSpeed : speedInfo.PassengerSpeed);
                }

                float validSpeedMpS = AllowedMaxSpeedMpS;

                //  try to find first speed limits along train - scan back to front

                bool noMoreSpeedposts = false;
                int sectionIndex = PresentPosition[Direction.Backward].TrackCircuitSectionIndex;
                float sectionOffset = PresentPosition[Direction.Backward].Offset;
                TrackDirection direction = PresentPosition[Direction.Backward].Direction;
                float remLength = Length;

                while (!noMoreSpeedposts)
                {
                    speedpostList = SignalEnvironment.ScanRoute(null, sectionIndex, sectionOffset,
                            direction, true, remLength, false, true, false, false, false, false, false, true, false, IsFreight);

                    if (speedpostList.Count > 0)
                    {
                        Signal speedpost = signalRef.Signals[speedpostList[0]];
                        SpeedInfo speedInfo = speedpost.SpeedLimit(SignalFunction.Speed);
                        float distanceFromFront = Length - speedpost.DistanceTo(RearTDBTraveller);
                        if (distanceFromFront >= 0)
                        {
                            float newSpeedMpS = IsFreight ? speedInfo.FreightSpeed : speedInfo.PassengerSpeed;
                            if (newSpeedMpS <= validSpeedMpS)
                            {
                                validSpeedMpS = newSpeedMpS;
                                if (validSpeedMpS < AllowedMaxSpeedMpS)
                                {
                                    AllowedMaxSpeedMpS = validSpeedMpS;
                                }
                                requiredActions.UpdatePendingSpeedlimits(validSpeedMpS);  // update any older pending speed limits
                            }
                            else
                            {
                                validSpeedMpS = newSpeedMpS;
                                float reqDistance = DistanceTravelledM + Length - distanceFromFront;
                                ActivateSpeedLimit speedLimit = new ActivateSpeedLimit(reqDistance,
                                    speedInfo.LimitedSpeedReduction == 0 ? newSpeedMpS : -1, -1f,
                                    speedInfo.LimitedSpeedReduction == 0 ? -1 : newSpeedMpS);
                                requiredActions.InsertAction(speedLimit);
                                requiredActions.UpdatePendingSpeedlimits(newSpeedMpS);  // update any older pending speed limits
                            }

                            if (newSpeedMpS < allowedAbsoluteMaxSpeedLimitMpS) allowedAbsoluteMaxSpeedLimitMpS = newSpeedMpS;
                            sectionIndex = speedpost.TrackCircuitIndex;
                            sectionOffset = speedpost.TrackCircuitOffset;
                            direction = speedpost.TrackCircuitDirection;
                            remLength = distanceFromFront;
                        }
                        else
                        {
                            noMoreSpeedposts = true;
                        }
                    }
                    else
                    {
                        noMoreSpeedposts = true;
                    }
                }

                allowedMaxSpeedLimitMpS = AllowedMaxSpeedMpS;   // set default
            }

            float distanceToLastObject = 9E29f;  // set to overlarge value
            SignalAspectState nextAspect = SignalAspectState.Unknown;

            SignalItemInfo firstObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                PresentPosition[Direction.Forward].RouteListIndex, PresentPosition[Direction.Forward].Offset, -1,
                SignalItemType.Any);

            //  get first item from train (irrespective of distance)
            SignalItemFindState returnState = firstObject.State;
            if (returnState == SignalItemFindState.Item)
            {
                firstObject.DistanceToTrain = firstObject.DistanceFound;
                SignalObjectItems.Add(firstObject);
                if (firstObject.SignalDetails.IsSignal)
                {
                    nextAspect = firstObject.SignalDetails.SignalLR(SignalFunction.Normal);
                    firstObject.SignalState = nextAspect;
                }
                distanceToLastObject = firstObject.DistanceFound;
            }

            // get next items within max distance

            float maxDistance = Math.Max(AllowedMaxSpeedMpS * maxTimeS, minCheckDistanceM);

            // look maxTimeS or minCheckDistance ahead

            SignalItemInfo nextObject;
            SignalItemInfo prevObject = firstObject;

            int routeListIndex = PresentPosition[Direction.Forward].RouteListIndex;
            float offset = PresentPosition[Direction.Forward].Offset;
            int nextIndex = routeListIndex;

            while (returnState == SignalItemFindState.Item && distanceToLastObject < maxDistance && nextAspect != SignalAspectState.Stop)
            {
                int foundSection = -1;

                Signal signal = prevObject.SignalDetails;

                int reqTCReference = signal.TrackCircuitIndex;
                float reqOffset = signal.TrackCircuitOffset + 0.0001f;   // make sure you find NEXT object ! //

                if (signal.TrackCircuitNextIndex > 0)
                {
                    reqTCReference = signal.TrackCircuitNextIndex;
                    reqOffset = 0.0f;
                }

                if (nextIndex < 0)
                    nextIndex = 0;
                for (int iNode = nextIndex; iNode < ValidRoute[0].Count && foundSection < 0 && reqTCReference > 0; iNode++)
                {
                    TrackCircuitRouteElement thisElement = ValidRoute[0][iNode];
                    if (thisElement.TrackCircuitSection.Index == reqTCReference)
                    {
                        foundSection = iNode;
                        nextIndex = iNode;
                        offset = reqOffset;
                    }
                }

                nextObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                nextIndex, offset, -1, SignalItemType.Any);

                returnState = nextObject.State;

                if (returnState == SignalItemFindState.Item)
                {
                    if (nextObject.SignalDetails.IsSignal)
                    {
                        nextObject.SignalState = nextObject.SignalDetails.SignalLR(SignalFunction.Normal);
                        nextAspect = nextObject.SignalState;

                    }

                    nextObject.DistanceToObject = nextObject.DistanceFound;
                    nextObject.DistanceToTrain = prevObject.DistanceToTrain + nextObject.DistanceToObject;
                    distanceToLastObject = nextObject.DistanceToTrain;
                    SignalObjectItems.Add(nextObject);
                    prevObject = nextObject;
                }
            }

            //
            // get first signal and first speedlimit
            // also initiate nextSignal variable
            //

            bool signalFound = false;
            bool speedlimFound = false;

            for (int i = 0; i < SignalObjectItems.Count && (!signalFound || !speedlimFound); i++)
            {
                if (!signalFound)
                {
                    SignalItemInfo signalInfo = SignalObjectItems[i];
                    if (signalInfo.ItemType == SignalItemType.Signal)
                    {
                        signalFound = true;
                        IndexNextSignal = i;
                    }
                }

                if (!speedlimFound)
                {
                    SignalItemInfo signalInfo = SignalObjectItems[i];
                    if (signalInfo.ItemType == SignalItemType.SpeedLimit)
                    {
                        speedlimFound = true;
                        IndexNextSpeedlimit = i;
                    }
                }
            }

            //
            // If signal in list, set signal reference,
            // else try to get first signal if in signal mode
            //
            NextSignalObject[0] = null;
            if (IndexNextSignal >= 0)
            {
                NextSignalObject[0] = SignalObjectItems[IndexNextSignal].SignalDetails;
                DistanceToSignal = SignalObjectItems[IndexNextSignal].DistanceToTrain;
            }
            else
            {
                SignalItemInfo firstSignalObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                    PresentPosition[Direction.Forward].RouteListIndex, PresentPosition[Direction.Forward].Offset, -1,
                    SignalItemType.Signal);

                if (firstSignalObject.State == SignalItemFindState.Item)
                {
                    NextSignalObject[0] = firstSignalObject.SignalDetails;
                    firstSignalObject.DistanceToTrain = firstSignalObject.DistanceFound;
                    DistanceToSignal = firstSignalObject.DistanceFound;
                }
            }

            //
            // determine actual speed limits depending on overall speed and type of train
            //

            UpdateSpeedInfo();
        }

        ///  Update the distance to and aspect of next signal
        protected void UpdateSignalState(int backward)
        {
            // for AUTO mode, use direction 0 only
            SignalItemFindState returnState = SignalItemFindState.Item;

            bool listChanged = false;
            bool signalFound = false;

            SignalItemInfo firstObject = null;

            // get distance to first object
            if (SignalObjectItems.Count > 0)
            {
                firstObject = SignalObjectItems[0];
                firstObject.DistanceToTrain = GetObjectDistanceToTrain(firstObject);

                // check if passed object - if so, remove object
                // if object is speed, set max allowed speed as distance travelled action
                while (firstObject.DistanceToTrain < 0.0f && SignalObjectItems.Count > 0)
                {
                    float temp1MaxSpeedMpS = IsFreight ? firstObject.SpeedInfo.FreightSpeed : firstObject.SpeedInfo.PassengerSpeed;
                    if (firstObject.SignalDetails.IsSignal)
                    {
                        allowedAbsoluteMaxSpeedSignalMpS = temp1MaxSpeedMpS == -1 ? (float)simulator.TRK.Route.SpeedLimit : temp1MaxSpeedMpS;
                    }
                    else if (!firstObject.SpeedInfo.Reset)
                    {
                        if (firstObject.SpeedInfo.LimitedSpeedReduction == 0) allowedAbsoluteMaxSpeedLimitMpS = temp1MaxSpeedMpS == -1 ? allowedAbsoluteMaxSpeedLimitMpS : temp1MaxSpeedMpS;
                        else allowedAbsoluteMaxTempSpeedLimitMpS = temp1MaxSpeedMpS == -1 ? allowedAbsoluteMaxTempSpeedLimitMpS : temp1MaxSpeedMpS;
                    }
                    else
                    {
                        allowedAbsoluteMaxSpeedSignalMpS = allowedAbsoluteMaxSpeedLimitMpS;
                    }

                    if (firstObject.ActualSpeed > 0)
                    {
                        if (firstObject.ActualSpeed <= AllowedMaxSpeedMpS)
                        {
                            AllowedMaxSpeedMpS = firstObject.ActualSpeed;
                            float tempMaxSpeedMps = AllowedMaxSpeedMpS;
                            if (!simulator.TimetableMode)
                            {
                                tempMaxSpeedMps = IsFreight ? firstObject.SpeedInfo.FreightSpeed : firstObject.SpeedInfo.PassengerSpeed;
                                if (tempMaxSpeedMps == -1f)
                                    tempMaxSpeedMps = AllowedMaxSpeedMpS;
                            }


                            if (firstObject.SignalDetails.IsSignal)
                            {
                                allowedMaxSpeedSignalMpS = tempMaxSpeedMps;
                            }
                            else if (firstObject.SpeedInfo.LimitedSpeedReduction == 0)
                            {
                                allowedMaxSpeedLimitMpS = tempMaxSpeedMps;
                            }
                            else
                            {
                                allowedMaxTempSpeedLimitMpS = tempMaxSpeedMps;
                            }
                            requiredActions.UpdatePendingSpeedlimits(AllowedMaxSpeedMpS);  // update any older pending speed limits
                        }
                        else
                        {
                            ActivateSpeedLimit speedLimit;
                            float reqDistance = DistanceTravelledM + Length;
                            if (firstObject.SignalDetails.IsSignal)
                            {
                                speedLimit = new ActivateSpeedLimit(reqDistance, -1f, firstObject.ActualSpeed);
                            }
                            else if (simulator.TimetableMode || !firstObject.SpeedInfo.Reset)
                            {
                                speedLimit = new ActivateSpeedLimit(reqDistance,
                                    firstObject.SpeedInfo.LimitedSpeedReduction == 0 ? firstObject.ActualSpeed : -1, -1f,
                                    firstObject.SpeedInfo.LimitedSpeedReduction == 0 ? -1 : firstObject.ActualSpeed);
                            }
                            else
                            {
                                speedLimit = new ActivateSpeedLimit(reqDistance, firstObject.ActualSpeed, firstObject.ActualSpeed);
                            }

                            requiredActions.InsertAction(speedLimit);
                            requiredActions.UpdatePendingSpeedlimits(firstObject.ActualSpeed);  // update any older pending speed limits
                        }
                    }
                    else if (!simulator.TimetableMode)
                    {
                        float tempMaxSpeedMps = IsFreight ? firstObject.SpeedInfo.FreightSpeed : firstObject.SpeedInfo.PassengerSpeed;
                        if (tempMaxSpeedMps >= 0)
                        {
                            if (firstObject.SignalDetails.IsSignal)
                            {
                                allowedMaxSpeedSignalMpS = tempMaxSpeedMps;
                            }
                            else
                            {
                                if (firstObject.SpeedInfo.LimitedSpeedReduction == 0) allowedMaxSpeedLimitMpS = tempMaxSpeedMps;
                                else allowedMaxTempSpeedLimitMpS = tempMaxSpeedMps;
                            }
                        }
                        else if (firstObject.SignalDetails.IsSignal)
                        {
                            allowedMaxSpeedSignalMpS = allowedAbsoluteMaxSpeedSignalMpS;
                        }
                    }

                    if (firstObject.SignalDetails == NextSignalObject[0])
                    {
                        NextSignalObject[0] = null;
                    }

                    SignalObjectItems.RemoveAt(0);
                    listChanged = true;

                    if (SignalObjectItems.Count > 0)
                    {
                        firstObject = SignalObjectItems[0];
                        firstObject.DistanceToTrain = GetObjectDistanceToTrain(firstObject);
                    }
                }

                // if moving backward, check signals have been passed
                if (backward > BackwardThreshold)
                {
                    bool noMoreNewSignals = false;

                    int routeIndex = PresentPosition[Direction.Forward].RouteListIndex;
                    float offset = PresentPosition[Direction.Forward].Offset;

                    while (!noMoreNewSignals)
                    {
                        SignalItemInfo newObjectItem = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                           routeIndex, offset, -1, SignalItemType.Signal);

                        returnState = newObjectItem.State;
                        if (returnState == SignalItemFindState.Item)
                        {
                            int newSignalIndex = newObjectItem.SignalDetails.Index;

                            noMoreNewSignals = NextSignalObject[0] == null || (NextSignalObject[0] != null && newSignalIndex == NextSignalObject[0].Index);

                            if (!noMoreNewSignals)
                            {
                                if (SignalObjectItems.Count > 0)  // reset distance to train to distance to object //
                                {
                                    firstObject = SignalObjectItems[0];
                                    firstObject.DistanceToObject =
                                        firstObject.DistanceToTrain - newObjectItem.DistanceToTrain;
                                }

                                SignalObjectItems.Insert(0, newObjectItem);
                                listChanged = true;

                                int foundIndex = ValidRoute[0].GetRouteIndex(newObjectItem.SignalDetails.TrackCircuitNextIndex, routeIndex);
                                if (foundIndex > 0)
                                {
                                    routeIndex = foundIndex;
                                    offset = 0.0f;
                                }
                            }
                        }
                        else
                        {
                            noMoreNewSignals = true;
                        }
                    }
                }
            }

            // if no objects left on list, find first object whatever the distance
            if (SignalObjectItems.Count <= 0)
            {
                firstObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                      PresentPosition[Direction.Forward].RouteListIndex, PresentPosition[Direction.Forward].Offset, -1,
                      SignalItemType.Any);

                returnState = firstObject.State;
                if (returnState == SignalItemFindState.Item)
                {
                    firstObject.DistanceToTrain = firstObject.DistanceFound;
                    SignalObjectItems.Add(firstObject);
                }
            }

            // reset next signal object if none found
            if (SignalObjectItems.Count <= 0 || (SignalObjectItems.Count == 1 && SignalObjectItems[0].ItemType == SignalItemType.SpeedLimit))
            {
                NextSignalObject[0] = null;
                DistanceToSignal = null;
                listChanged = true;
            }

            // process further if any object available
            if (SignalObjectItems.Count > 0)
            {

                // Update state and speed of first object if signal
                if (firstObject.SignalDetails.IsSignal)
                {
                    firstObject.SignalState = firstObject.SignalDetails.SignalLR(SignalFunction.Normal);
                    firstObject.SpeedInfo = new SpeedInfo(firstObject.SignalDetails.SignalSpeed(SignalFunction.Normal));
                }
                else if (firstObject.SignalDetails.SignalHeads[0]?.SignalFunction == SignalFunction.Speed)// check if object is SPEED info signal
                {
                    firstObject.SpeedInfo = new SpeedInfo(firstObject.SignalDetails.SignalSpeed(SignalFunction.Speed));
                }

                // Update all objects in list (except first)
                float lastDistance = firstObject.DistanceToTrain;

                SignalItemInfo prevObject = firstObject;

                foreach (SignalItemInfo nextObject in SignalObjectItems)
                {
                    nextObject.DistanceToTrain = prevObject.DistanceToTrain + nextObject.DistanceToObject;
                    lastDistance = nextObject.DistanceToTrain;

                    if (nextObject.SignalDetails.IsSignal)
                    {
                        nextObject.SignalState = nextObject.SignalDetails.SignalLR(SignalFunction.Normal);
                        if (nextObject.SignalDetails.EnabledTrain != null && nextObject.SignalDetails.EnabledTrain.Train != this)
                            nextObject.SignalState = SignalAspectState.Stop; // state not valid if not enabled for this train
                        nextObject.SpeedInfo = new SpeedInfo(nextObject.SignalState == SignalAspectState.Stop ? null : nextObject.SignalDetails.SignalSpeed(SignalFunction.Normal));
                    }
                    else if (nextObject.SignalDetails.SignalHeads[0].SignalFunction == SignalFunction.Speed) // check if object is SPEED info signal
                    {
                        nextObject.SpeedInfo = new SpeedInfo(nextObject.SignalDetails.SignalSpeed(SignalFunction.Speed));
                    }

                    prevObject = nextObject;
                }

                // check if last signal aspect is STOP, and if last signal is enabled for this train
                // If so, no check on list is required
                SignalAspectState nextAspect = SignalAspectState.Unknown;

                for (int i = SignalObjectItems.Count - 1; i >= 0 && !signalFound; i--)
                {
                    SignalItemInfo nextObject = SignalObjectItems[i];
                    if (nextObject.ItemType == SignalItemType.Signal)
                    {
                        signalFound = true;
                        nextAspect = nextObject.SignalState;
                    }
                }

                // read next items if last item within max distance
                float maxDistance = Math.Max(AllowedMaxSpeedMpS * maxTimeS, minCheckDistanceM);

                int routeListIndex = PresentPosition[Direction.Forward].RouteListIndex;
                int lastIndex = routeListIndex;
                float offset = PresentPosition[Direction.Forward].Offset;

                prevObject = SignalObjectItems[SignalObjectItems.Count - 1];  // last object

                while (lastDistance < maxDistance &&
                          returnState == SignalItemFindState.Item &&
                          nextAspect != SignalAspectState.Stop)
                {

                    Signal prevSignal = prevObject.SignalDetails;
                    int reqTCReference = prevSignal.TrackCircuitIndex;
                    float reqOffset = prevSignal.TrackCircuitOffset + 0.0001f;   // make sure you find NEXT object ! //

                    if (prevSignal.TrackCircuitNextIndex > 0 && ValidRoute[0].GetRouteIndex(prevSignal.TrackCircuitNextIndex, lastIndex) > 0)
                    {
                        reqTCReference = prevSignal.TrackCircuitNextIndex;
                        reqOffset = 0.0f;
                    }

                    int foundSection = ValidRoute[0].GetRouteIndex(reqTCReference, lastIndex);
                    if (foundSection >= 0)
                    {
                        lastIndex = foundSection;
                        offset = reqOffset;
                    }

                    SignalItemInfo nextObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0], lastIndex, offset, -1, SignalItemType.Any);

                    returnState = nextObject.State;

                    if (returnState == SignalItemFindState.Item)
                    {
                        nextObject.DistanceToObject = nextObject.DistanceFound;
                        nextObject.DistanceToTrain = prevObject.DistanceToTrain + nextObject.DistanceToObject;

                        lastDistance = nextObject.DistanceToTrain;
                        SignalObjectItems.Add(nextObject);

                        if (nextObject.SignalDetails.IsSignal)
                        {
                            nextObject.SignalState = nextObject.SignalDetails.SignalLR(SignalFunction.Normal);
                            nextAspect = nextObject.SignalState;
                            nextObject.SpeedInfo = new SpeedInfo(nextObject.SignalDetails.SignalSpeed(SignalFunction.Normal));
                        }
                        else if (nextObject.SignalDetails.SignalHeads != null)  // check if object is SPEED info signal
                        {
                            if (nextObject.SignalDetails.SignalHeads[0].SignalFunction == SignalFunction.Speed)
                            {
                                nextObject.SpeedInfo = new SpeedInfo(nextObject.SignalDetails.SignalSpeed(SignalFunction.Speed));
                            }
                        }

                        prevObject = nextObject;
                        listChanged = true;
                    }
                }

                // check if IndexNextSignal still valid, if not, force list changed
                if (IndexNextSignal >= SignalObjectItems.Count)
                {
                    listChanged = true;
                }
            }

            // if list is changed, get new indices to first signal and speedpost
            if (listChanged)
            {
                signalFound = false;
                bool speedlimFound = false;
                IndexNextSignal = -1;
                IndexNextSpeedlimit = -1;
                NextSignalObject[0] = null;

                for (int i = 0; i < SignalObjectItems.Count && (!signalFound || !speedlimFound); i++)
                {
                    SignalItemInfo nextObject = SignalObjectItems[i];
                    if (!signalFound && nextObject.ItemType == SignalItemType.Signal)
                    {
                        signalFound = true;
                        IndexNextSignal = i;
                    }
                    else if (!speedlimFound && nextObject.ItemType == SignalItemType.SpeedLimit)
                    {
                        speedlimFound = true;
                        IndexNextSpeedlimit = i;
                    }
                }
            }

            // check if any signal in list, if not get direct from train
            // get state and details
            if (IndexNextSignal < 0)
            {
                SignalItemInfo firstSignalObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                        PresentPosition[Direction.Forward].RouteListIndex, PresentPosition[Direction.Forward].Offset, -1,
                        SignalItemType.Signal);

                if (firstSignalObject.State == SignalItemFindState.Item)
                {
                    NextSignalObject[0] = firstSignalObject.SignalDetails;
                    firstSignalObject.DistanceToTrain = firstSignalObject.DistanceFound;
                }
            }
            else
            {
                NextSignalObject[0] = SignalObjectItems[IndexNextSignal].SignalDetails;
            }

            //
            // update distance of signal if out of list
            //
            if (IndexNextSignal >= 0)
            {
                DistanceToSignal = SignalObjectItems[IndexNextSignal].DistanceToTrain;
            }
            else if (NextSignalObject[0] != null)
            {
                DistanceToSignal = NextSignalObject[0].DistanceTo(FrontTDBTraveller);
            }
            else if (ControlMode != TrainControlMode.AutoNode)
            {
                bool validModeSwitch = true;

                if (this is AITrain aiTrain)
                {
                    // do not switch to node control if train is set for auxiliary action
                    if (aiTrain.nextActionInfo?.NextAction == AIActionItem.AI_ACTION_TYPE.AUX_ACTION)
                    {
                        validModeSwitch = false;
                    }
                }

                if (validModeSwitch)
                {
                    SwitchToNodeControl(LastReservedSection[0]);
                }
            }

            // determine actual speed limits depending on overall speed and type of train

            UpdateSpeedInfo();
        }

        /// set actual speed limit for all objects depending on state and type of train
        private void UpdateSpeedInfo()
        {
            float validSpeedMpS = AllowedMaxSpeedMpS;
            float validSpeedSignalMpS = allowedMaxSpeedSignalMpS;
            float validSpeedLimitMpS = allowedMaxSpeedLimitMpS;
            float validTempSpeedLimitMpS = allowedMaxTempSpeedLimitMpS;

            // update valid speed with pending actions
            foreach (DistanceTravelledItem distanceAction in requiredActions)
            {
                if (distanceAction is ActivateSpeedLimit speedLimit)
                {
                    if (speedLimit.MaxSpeedMpSLimit > validSpeedLimitMpS)
                    {
                        validSpeedLimitMpS = speedLimit.MaxSpeedMpSLimit;
                    }

                    if (speedLimit.MaxSpeedMpSSignal > validSpeedSignalMpS)
                    {
                        validSpeedSignalMpS = speedLimit.MaxSpeedMpSSignal;
                    }
                    if (speedLimit.MaxTempSpeedMpSLimit > validTempSpeedLimitMpS)
                    {
                        validTempSpeedLimitMpS = speedLimit.MaxTempSpeedMpSLimit;
                    }
                }
            }

            // loop through objects

            foreach (SignalItemInfo signalInfo in SignalObjectItems)
            {
                //
                // select speed on type of train 
                //

                float actualSpeedMpS = IsFreight ? signalInfo.SpeedInfo.FreightSpeed : signalInfo.SpeedInfo.PassengerSpeed;

                if (signalInfo.SignalDetails.IsSignal)
                {
                    if (actualSpeedMpS > 0 && (signalInfo.SpeedInfo.Flag || !simulator.TimetableMode))
                    {
                        validSpeedSignalMpS = actualSpeedMpS;
                        if (validSpeedSignalMpS > Math.Min(validSpeedLimitMpS, validTempSpeedLimitMpS))
                        {
                            if (validSpeedMpS < Math.Min(validSpeedLimitMpS, validTempSpeedLimitMpS))
                            {
                                actualSpeedMpS = Math.Min(validSpeedLimitMpS, validTempSpeedLimitMpS);
                            }
                            else
                            {
                                actualSpeedMpS = -1;
                            }
                        }
                    }
                    else
                    {
                        validSpeedSignalMpS = TrainMaxSpeedMpS;
                        float newSpeedMpS = Math.Min(validSpeedSignalMpS, Math.Min(validSpeedLimitMpS, validTempSpeedLimitMpS));

                        if (newSpeedMpS != validSpeedMpS)
                        {
                            actualSpeedMpS = newSpeedMpS;
                        }
                        else
                        {
                            actualSpeedMpS = -1;
                        }
                    }
                    signalInfo.ActualSpeed = actualSpeedMpS;
                    if (actualSpeedMpS > 0)
                    {
                        validSpeedMpS = actualSpeedMpS;
                    }
                }
                else if (simulator.TimetableMode)
                {
                    {
                        if (actualSpeedMpS > 998f)
                        {
                            actualSpeedMpS = TrainMaxSpeedMpS;
                        }

                        if (actualSpeedMpS > 0)
                        {
                            validSpeedMpS = actualSpeedMpS;
                            validSpeedLimitMpS = actualSpeedMpS;
                        }
                        else if (actualSpeedMpS < 0 && signalInfo.SpeedInfo.Reset)
                        {
                            validSpeedMpS = validSpeedLimitMpS;
                            actualSpeedMpS = validSpeedLimitMpS;
                        }

                        signalInfo.ActualSpeed = Math.Min(actualSpeedMpS, TrainMaxSpeedMpS);
                    }
                }
                else  // Enhanced Compatibility on & SpeedLimit
                {
                    if (actualSpeedMpS > 998f)
                    {
                        actualSpeedMpS = (float)simulator.TRK.Route.SpeedLimit;
                    }

                    if (actualSpeedMpS > 0)
                    {
                        float tempValidSpeedSignalMpS = validSpeedSignalMpS == -1 ? 999 : validSpeedSignalMpS;
                        if (signalInfo.SpeedInfo.LimitedSpeedReduction == 0)
                        {
                            validSpeedLimitMpS = actualSpeedMpS;
                            if (actualSpeedMpS > Math.Min(tempValidSpeedSignalMpS, validTempSpeedLimitMpS))
                            {
                                if (validSpeedMpS < Math.Min(tempValidSpeedSignalMpS, validTempSpeedLimitMpS))
                                {
                                    actualSpeedMpS = Math.Min(tempValidSpeedSignalMpS, validTempSpeedLimitMpS);
                                }
                                else
                                {
                                    actualSpeedMpS = -1;
                                }
                            }
                        }
                        else
                        {
                            validTempSpeedLimitMpS = actualSpeedMpS;
                            if (actualSpeedMpS > Math.Min(tempValidSpeedSignalMpS, validSpeedLimitMpS))
                            {
                                if (validSpeedMpS < Math.Min(tempValidSpeedSignalMpS, validSpeedLimitMpS))
                                {
                                    actualSpeedMpS = Math.Min(tempValidSpeedSignalMpS, validSpeedLimitMpS);
                                }
                                else
                                {
                                    actualSpeedMpS = -1;
                                }
                            }
                        }
                    }
                    else if (actualSpeedMpS < 0 && !signalInfo.SpeedInfo.Reset)
                    {
                        float newSpeedMpS1 = Math.Min(validSpeedSignalMpS, Math.Min(validSpeedLimitMpS, validTempSpeedLimitMpS));

                        if (newSpeedMpS1 != validSpeedMpS)
                        {
                            actualSpeedMpS = newSpeedMpS1;
                        }
                        else
                        {
                            actualSpeedMpS = -1;
                        }
                    }
                    else if (signalInfo.SpeedInfo.Reset)
                    {
                        actualSpeedMpS = validSpeedLimitMpS;
                    }

                    signalInfo.ActualSpeed = actualSpeedMpS;
                    if (actualSpeedMpS > 0)
                    {
                        validSpeedMpS = actualSpeedMpS;
                    }
                }
            }
        }

        /// Set retainers
        internal void SetRetainers(bool increase)
        {
            if (Math.Abs(SpeedMpS) > 0.1)
                return;
            if (!increase)
            {
                RetainerSetting = RetainerSetting.Exhaust;
                RetainerPercent = 100;
            }
            else if (RetainerPercent < 100)
                RetainerPercent *= 2;
            else if (RetainerSetting != RetainerSetting.SlowDirect)
            {
                RetainerPercent = 25;
                switch (RetainerSetting)
                {
                    case RetainerSetting.Exhaust:
                        RetainerSetting = RetainerSetting.LowPressure;
                        break;
                    case RetainerSetting.LowPressure:
                        RetainerSetting = RetainerSetting.HighPressure;
                        break;
                    case RetainerSetting.HighPressure:
                        RetainerSetting = RetainerSetting.SlowDirect;
                        break;
                }
            }

            (_, int last) = FindLeadLocomotives();
            int step = 100 / RetainerPercent;
            for (int i = 0; i < Cars.Count; i++)
            {
                int j = Cars.Count - 1 - i;
                if (j <= last)
                    break;
                Cars[j].BrakeSystem.SetRetainer(i % step == 0 ? RetainerSetting : RetainerSetting.Exhaust);
            }
        }

        /// Find lead locomotive
        internal (int first, int last) FindLeadLocomotives()
        {
            // FindLeadLocomotives stores the index of a single locomotive, or alternatively multiple locomotives, such as 
            // in the case of MU'd diesel units, the "first" and "last" values enclose the group of locomotives where the 
            // lead locomotive (the player driven one) resides. Within this group both the main reservoir pressure and the 
            // engine brake pipe pressure will be propagated. It only identifies multiple units when coupled directly together,
            // for example a double headed steam locomotive will most often have a tender separating the two locomotives, 
            // so the second locomotive will not be identified, nor will a locomotive added at the rear of the train. 

            int first = -1;
            int last = -1;
            if (LeadLocomotiveIndex >= 0)
            {
                for (int i = LeadLocomotiveIndex; i < Cars.Count && Cars[i].IsDriveable; i++)
                    last = i;
                for (int i = LeadLocomotiveIndex; i >= 0 && Cars[i].IsDriveable; i--)
                    first = i;
            }

            // If first (lead) locomotive is a steam locomotive check to see if the engine brake needs to be extended to cover the tender

            if (first != -1) // if lead locomotive is set at initialised value, then don't attempt to process engine brake extension
            {

                if (Cars[first] is MSTSSteamLocomotive)
                {

                    // If double headed tank steam locomotive (no tender is attached) then only apply engine brake to first locomotive for consistency
                    if (last != first && Cars[first] is MSTSSteamLocomotive && Cars[last] is MSTSSteamLocomotive)
                    {
                        last = first; // Reduce locomotive lead values to apply engine brakes only to lead locomotive, and not 2nd locomotive.
                    }
                    else // if last = first, ie only a single locomotive (can be two locomotives separated by a tender as 2nd locomotive is not counted in the first / last values.
                    {
                        if (last < Cars.Count - 1)  // Check that there are cars after the locomotive, if not skip extending brake to tender
                        {
                            if (last == first && Cars[first] is MSTSSteamLocomotive && Cars[first + 1].WagonType == TrainCar.WagonTypes.Tender)
                            {
                                last += 1;      // If a "standard" single steam locomotive with a tender then for the purposes of braking increment last above first by one
                            }
                        }
                    }
                }
            }
            return (first, last);
        }

        internal TrainCar FindLeadLocomotive()
        {
            (int first, int last) = FindLeadLocomotives();
            if (first > -1 && first < LeadLocomotiveIndex)
            {
                return Cars[first];
            }
            else if (last > -1 && last > LeadLocomotiveIndex)
            {
                return Cars[last];
            }
            for (int i = 0; i < Cars.Count; i++)
            {
                if (Cars[i].IsDriveable)
                    return Cars[i];
            }
            Trace.TraceWarning($"Train {Name} ({Number}) has no locomotive!");
            return null;
        }

        /// Check if train is passenger or freight train
        public void CheckFreight()
        {
            IsFreight = false;
            PassengerCarsNumber = 0;
            IsPlayable = false;

            foreach (TrainCar car in Cars)
            {
                if (car.WagonType == TrainCar.WagonTypes.Freight)
                    IsFreight = true;
                if ((car.WagonType == TrainCar.WagonTypes.Passenger) || (car.IsDriveable && car.HasPassengerCapacity))
                    PassengerCarsNumber++;
                if (car.IsDriveable && (car as MSTSLocomotive).CabViewList.Count > 0)
                    IsPlayable = true;
            }
            if (TrainType == TrainType.AiIncorporated && IncorporatingTrainNo > -1)
                IsPlayable = true;
        } // CheckFreight

        /// Cars have been added to the rear of the train, recalc the rearTDBtraveller
        internal void RepositionRearTraveller()
        {
            Traveller traveller = new Traveller(FrontTDBTraveller, Traveller.TravellerDirection.Backward);
            // The traveller location represents the front of the train.
            float length = 0f;

            // process the cars first to last
            for (int i = 0; i < Cars.Count; ++i)
            {
                TrainCar car = Cars[i];
                if (car.WheelAxlesLoaded)
                {
                    car.ComputePosition(traveller, false, 0, 0, SpeedMpS);
                }
                else
                {
                    float bogieSpacing = car.CarLengthM * 0.65f;  // we'll use this approximation since the wagfile doesn't contain info on bogie position

                    // traveller is positioned at the front of the car
                    // advance to the first bogie 
                    traveller.Move((car.CarLengthM - bogieSpacing) / 2.0f);
                    int tileX = traveller.TileX;
                    int tileZ = traveller.TileZ;
                    float x = traveller.X;
                    float y = traveller.Y;
                    float z = traveller.Z;
                    traveller.Move(bogieSpacing);

                    // normalize across tile boundaries
                    while (tileX > traveller.TileX)
                    {
                        x += tileSize;
                        --tileX;
                    }
                    while (tileX < traveller.TileX)
                    {
                        x -= tileSize;
                        ++tileX;
                    }
                    while (tileZ > traveller.TileZ)
                    {
                        z += tileSize;
                        --tileZ;
                    }
                    while (tileZ < traveller.TileZ)
                    {
                        z -= tileSize;
                        ++tileZ;
                    }

                    // note the railcar sits 0.275meters above the track database path  TODO - is this always consistent?
                    Matrix flipMatrix = Matrix.Identity;
                    if (!car.Flipped)
                    {
                        //  Rotate matrix 180' around Y axis.
                        flipMatrix.M11 = -1;
                        flipMatrix.M33 = -1;
                    }
                    car.WorldPosition = new WorldPosition(traveller.TileX, traveller.TileZ, MatrixExtension.Multiply(flipMatrix, Simulator.XNAMatrixFromMSTSCoordinates(traveller.X, traveller.Y + 0.275f, traveller.Z, x, y + 0.275f, z)));
                    traveller.Move((car.CarLengthM - bogieSpacing) / 2.0f);
                }
                if (i < Cars.Count - 1)
                {
                    traveller.Move(car.CouplerSlackM + car.GetCouplerZeroLengthM());
                    length += car.CouplerSlackM + car.GetCouplerZeroLengthM();
                }
                length += car.CarLengthM;
            }

            traveller.ReverseDirection();
            RearTDBTraveller = traveller;
            Length = length;
        } // RepositionRearTraveller

        public void CalculatePositionOfCars()
        {
            CalculatePositionOfCars(0, 0);
        }

        /// Distance is the signed distance the cars are moving.
        /// </summary>
        internal void CalculatePositionOfCars(double elapsedTime, double distance)
        {
            if (double.IsNaN(distance))
                distance = 0;//sanity check

            RearTDBTraveller.Move(distance);

            // TODO : check if train moved back into previous section
            Traveller traveller = new Traveller(RearTDBTraveller);
            // The traveller location represents the back of the train.
            float length = 0f;

            // process the cars last to first
            for (int i = Cars.Count - 1; i >= 0; --i)
            {
                TrainCar car = Cars[i];
                if (i < Cars.Count - 1)
                {
                    traveller.Move(car.CouplerSlackM + car.GetCouplerZeroLengthM());
                    length += car.CouplerSlackM + car.GetCouplerZeroLengthM();
                }
                if (car.WheelAxlesLoaded)
                {
                    car.ComputePosition(traveller, true, elapsedTime, distance, SpeedMpS);
                }
                else
                {
                    float bogieSpacing = car.CarLengthM * 0.65f;  // we'll use this approximation since the wagfile doesn't contain info on bogie position

                    // traveller is positioned at the back of the car
                    // advance to the first bogie 
                    traveller.Move((car.CarLengthM - bogieSpacing) / 2.0f);
                    int tileX = traveller.TileX;
                    int tileZ = traveller.TileZ;
                    float x = traveller.X;
                    float y = traveller.Y;
                    float z = traveller.Z;
                    traveller.Move(bogieSpacing);

                    // normalize across tile boundaries
                    while (tileX > traveller.TileX)
                    {
                        x += tileSize;
                        --tileX;
                    }
                    while (tileX < traveller.TileX)
                    {
                        x -= tileSize;
                        ++tileX;
                    }
                    while (tileZ > traveller.TileZ)
                    {
                        z += tileSize;
                        --tileZ;
                    }
                    while (tileZ < traveller.TileZ)
                    {
                        z -= tileSize;
                        ++tileZ;
                    }


                    // note the railcar sits 0.275meters above the track database path  TODO - is this always consistent?
                    Matrix flipMatrix = Matrix.Identity;
                    if (car.Flipped)
                    {
                        //  Rotate matrix 180' around Y axis.
                        flipMatrix.M11 = -1;
                        flipMatrix.M33 = -1;
                    }
                    car.WorldPosition = new WorldPosition(traveller.TileX, traveller.TileZ,
                        MatrixExtension.Multiply(flipMatrix, Simulator.XNAMatrixFromMSTSCoordinates(traveller.X, traveller.Y + 0.275f, traveller.Z, x, y + 0.275f, z)));

                    traveller.Move((car.CarLengthM - bogieSpacing) / 2.0f);  // Move to the front of the car 

                    car.UpdatedTraveler(traveller, elapsedTime, distance, SpeedMpS);
                }
                length += car.CarLengthM;
            }

            FrontTDBTraveller = traveller;
            Length = length;
            travelled += (float)distance;
        } // CalculatePositionOfCars

        /// Update Car speeds
        protected void UpdateCarSpeeds(double elapsedTime)
        {
            // The train speed is calculated by averaging all the car speeds. The individual car speeds are calculated from the TotalForce acting on each car. 
            // Typically the TotalForce consists of the MotiveForce or Gravitational forces (though other forces like friction have a small impact as well).
            // At stop under normal circumstances the BrakeForce exceeds the TotalForces, and therefore the wagon is "held in a stationary position". 
            // In the case of "air_piped" wagons which have no BrakeForces acting on them, the car is not held stationary, and each car shows a small speed vibration in either direction.
            // To overcome this any "air_piped and vacuum_piped" cars are forced to zero speed if the preceeding car is stationary.
            int n = 0;
            float prevCarSpeedMps = 0.0f;
            float nextCarSpeedMps = 0.0f;
            bool locoBehind = true;
            for (int i = 0; i < Cars.Count; i++)
            {
                TrainCar car = Cars[i];
                if (i < Cars.Count - 1) 
                    nextCarSpeedMps = Cars[i + 1].SpeedMpS;
                if (TrainMaxSpeedMpS <= 0f && car is MSTSLocomotive locomotive)
                {
                    TrainMaxSpeedMpS = locomotive.MaxSpeedMpS;
                    locoBehind = false;
                }
                if (car.SpeedMpS > 0)
                {
                    car.SpeedMpS += (float)(car.TotalForceN / car.MassKG * elapsedTime);
                    if (car.SpeedMpS < 0)
                        car.SpeedMpS = 0;
                    // If car is manual braked, air_piped car or vacuum_piped, and preceeding car is at stop, then set speed to zero.  
                    // These type of cars do not have any brake force to hold them still
                    if ((car.CarBrakeSystemType == "air_piped" || car.CarBrakeSystemType == "vacuum_piped" || car.CarBrakeSystemType == "manual_braking") && (locoBehind ? n != Cars.Count - 1 && nextCarSpeedMps == 0 : n != 0 && prevCarSpeedMps == 0))
                    {
                        car.SpeedMpS = 0;
                    }
                    prevCarSpeedMps = car.SpeedMpS;
                }
                else if (car.SpeedMpS < 0)
                {
                    car.SpeedMpS += (float)(car.TotalForceN / car.MassKG * elapsedTime);
                    if (car.SpeedMpS > 0)
                        car.SpeedMpS = 0;
                    // If car is manual braked, air_piped car or vacuum_piped, and preceeding car is at stop, then set speed to zero.  
                    // These type of cars do not have any brake force to hold them still
                    if ((car.CarBrakeSystemType == "air_piped" || car.CarBrakeSystemType == "vacuum_piped" || car.CarBrakeSystemType == "manual_braking") && (locoBehind ? n != Cars.Count - 1 && nextCarSpeedMps == 0 : n != 0 && prevCarSpeedMps == 0))
                    {
                        car.SpeedMpS = 0;
                    }
                    prevCarSpeedMps = car.SpeedMpS;
                }
                else // if speed equals zero
                    prevCarSpeedMps = car.SpeedMpS;
                n++;
#if DEBUG_SPEED_FORCES
                Trace.TraceInformation(" ========================================  Train Speed #2 (Train.cs) ===========================================================");
                Trace.TraceInformation("Car ID {0} TotalForceN {1} Mass {2} elapsedtime {3} CarSpeed {4}", car.CarID, car.TotalForceN, car.MassKG, elapsedTime, car.SpeedMpS);
                Trace.TraceInformation("Friction {0} Brake {1} Curve {2} Wind {3} Tunnel {4}", car.FrictionForceN, car.BrakeForceN, car.CurveForceN, car.WindForceN, car.TunnelForceN);
                Trace.TraceInformation("Coupler {0} Prev Car Speed {1}", car.CouplerForceU, PrevCarSpeedMps);
                Trace.TraceInformation("Calculated Total {0}", car.FrictionForceN + car.BrakeForceN + car.CurveForceN + car.WindForceN + car.TunnelForceN);
#endif
            }
            if (n == 0)
                return;

            //
            // start a car moving forward when it is stationary, once it is moving this whole section is skipped
            //
            for (int i = 0; i < Cars.Count; i++)
            {
                TrainCar car = Cars[i];
                if (car.SpeedMpS != 0 || car.TotalForceN <= (car.FrictionForceN + car.BrakeForceN + car.CurveForceN + car.WindForceN + car.TunnelForceN + car.DynamicBrakeForceN))
                {
                    // Skip this section to start car if car is already moving, or force not sufficient to start it moving
                    continue;
                }
                int j = i;
                float f = 0;
                float m = 0;
                for (; ; )
                {
                    // Cycle down the train consist until the first stationary car is found that has its leading couplers starting to pull it. The next car is then started by allowing its speed to increase above 0.
                    f += car.TotalForceN - (car.FrictionForceN + car.BrakeForceN + car.CurveForceN + car.WindForceN + car.TunnelForceN + car.DynamicBrakeForceN);
                    m += car.MassKG;
                    if (car.IsPlayerTrain && simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
                    {
                        if (j == Cars.Count - 1 || car.CouplerSlackM < car.AdvancedCouplerDynamicTensionSlackLimitM)
                            break;
                    }
                    else // Simple coupler
                    {
                        if (j == Cars.Count - 1 || car.CouplerSlackM < car.GetMaximumSimpleCouplerSlack2M())
                            break;
                    }
                    j++;
                    // Increment count to next car.
                    car = Cars[j];
                }
                if (f > 0)
                {
                    for (int k = i; k <= j; k++)
                    {
                        if ((Cars[k].CarBrakeSystemType == "air_piped" || Cars[k].CarBrakeSystemType == "vacuum_piped" || car.CarBrakeSystemType == "manual_braking") && FirstCar.SpeedMpS > 0 && Cars[k - 1].SpeedMpS == 0.0)
                        {
                            // If is manual braked, air_piped car or vacuum_piped, and preceeding car is at stop, then set speed to zero.  These type of cars do not have any brake force to hold them still
                            Cars[k].SpeedMpS = 0.0f;
                        }
                        else
                        {
                            // Start this stationary car
                            Cars[k].SpeedMpS = f / m * (float)elapsedTime;
                        }

                    }
                    n -= j - i + 1;
                }
            }
            if (n == 0)
                return;
            //
            // start cars moving backward when it is stationary, once it is moving it skips this whole section
            //
            for (int i = Cars.Count - 1; i >= 0; i--)
            {
                TrainCar car = Cars[i];
                if (car.SpeedMpS != 0 || car.TotalForceN > (-1.0f * (car.FrictionForceN + car.BrakeForceN + car.CurveForceN + car.WindForceN + car.TunnelForceN + car.DynamicBrakeForceN)))
                {
                    // Skip this section to start car if car is already moving, or force not sufficient to start it moving
                    continue;
                }
                int j = i;
                float f = 0;
                float m = 0;
                for (; ; )
                {
                    // Cycle up the train consist until the first stationary car is found that has its leading couplers starting to pull it. The next car is then started by allowing its speed to increase above 0.
                    f += car.TotalForceN + car.FrictionForceN + car.BrakeForceN + car.CurveForceN + car.WindForceN + car.TunnelForceN + car.DynamicBrakeForceN;
                    m += car.MassKG;
                    if (car.IsPlayerTrain && simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
                    {
                        if (j == 0 || car.CouplerSlackM > car.AdvancedCouplerDynamicCompressionSlackLimitM)
                            break;
                    }
                    else // Simple coupler
                    {
                        if (j == 0 || car.CouplerSlackM > -car.GetMaximumSimpleCouplerSlack2M())
                            break;
                    }
                    j--;
                    // Decrement the count so that next car is started
                    car = Cars[j];
                }
                if (f < 0)
                {
                    for (int k = j; k <= i; k++)
                    {

                        if ((Cars[k].CarBrakeSystemType == "air_piped" || Cars[k].CarBrakeSystemType == "vacuum_piped" || car.CarBrakeSystemType == "manual_braking") && FirstCar.SpeedMpS > 0 && Cars[k - 1].SpeedMpS == 0.0)
                        {
                            // If is manual braked, air_piped car or vacuum_piped, and preceeding car is at stop, then set speed to zero.  These type of cars do not have any brake force to hold them still
                            Cars[k].SpeedMpS = 0.0f;
                        }
                        else
                        {
                            // Start this stationary car
                            Cars[k].SpeedMpS = f / m * (float)elapsedTime;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculate initial position
        /// </summary>
        internal virtual TrackCircuitPartialPathRoute CalculateInitialTrainPosition()
        {

            // calculate train length

            float trainLength = 0f;

            for (int i = Cars.Count - 1; i >= 0; --i)
            {
                TrainCar car = Cars[i];
                if (i < Cars.Count - 1)
                {
                    trainLength += car.CouplerSlackM + car.GetCouplerZeroLengthM();
                }
                trainLength += car.CarLengthM;
            }

            // get starting position and route

            TrackNode tn = RearTDBTraveller.TN;
            float offset = RearTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)RearTDBTraveller.Direction;

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];
            offset = PresentPosition[Direction.Backward].Offset;

            //<CSComment> must do preliminary calculation of PresentPosition[Direction.Forward] parameters in order to use subsequent code
            // limited however to case of train fully in one section to avoid placement ambiguities </CSComment>
            float offsetFromEnd = section.Length - (Length + offset);
            if (PresentPosition[Direction.Forward].TrackCircuitSectionIndex == -1 && offsetFromEnd >= 0) // train is fully in one section
            {
                PresentPosition[Direction.Forward].Direction = PresentPosition[Direction.Backward].Direction;
                PresentPosition[Direction.Forward].TrackCircuitSectionIndex = PresentPosition[Direction.Backward].TrackCircuitSectionIndex;
                PresentPosition[Direction.Forward].Offset = PresentPosition[Direction.Backward].Offset + trainLength;
            }

            // create route if train has none
            if (ValidRoute[0] == null)
            {
                ValidRoute[0] = SignalEnvironment.BuildTempRoute(this, section.Index, PresentPosition[Direction.Backward].Offset,
                            PresentPosition[Direction.Backward].Direction, trainLength, true, true, false);
            }

            // find sections
            bool sectionAvailable = true;
            float remLength = trainLength;
            int routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
            if (routeIndex < 0)
                routeIndex = 0;

            bool sectionsClear = true;

            TrackCircuitPartialPathRoute tempRoute = new TrackCircuitPartialPathRoute();

            TrackCircuitRouteElement routeElement = ValidRoute[0][routeIndex];
            section = routeElement.TrackCircuitSection;
            if (!section.CanPlaceTrain(this, offset, remLength))
            {
                sectionsClear = false;
            }

            while (remLength > 0 && sectionAvailable)
            {
                tempRoute.Add(routeElement);
                remLength -= (section.Length - offset);
                offset = 0.0f;

                if (remLength > 0)
                {
                    if (routeIndex < ValidRoute[0].Count - 1)
                    {
                        routeIndex++;
                        routeElement = ValidRoute[0][routeIndex];
                        section = routeElement.TrackCircuitSection;
                        if (!section.CanPlaceTrain(this, offset, remLength))
                        {
                            sectionsClear = false;
                        }
                        offset = 0.0f;
                    }
                    else
                    {
                        Trace.TraceWarning($"No sufficient track to place train {Number} , service name {Name} ");
                        sectionAvailable = false;
                    }
                }

            }

            if (MPManager.IsMultiPlayer()) 
                return tempRoute;
            if (!sectionAvailable || !sectionsClear)
            {
                tempRoute.Clear();
            }

            return tempRoute;
        }

        // Set initial train route
        internal void SetInitialTrainRoute(TrackCircuitPartialPathRoute partialRoute)
        {
            // reserve sections, use direction Forward only
            foreach (TrackCircuitRouteElement element in partialRoute)
            {
                element.TrackCircuitSection.Reserve(routedForward, partialRoute);
            }
        }

        // Reset initial train route
        internal void ResetInitialTrainRoute(TrackCircuitPartialPathRoute partialRoute)
        {
            // unreserve sections
            foreach (TrackCircuitRouteElement element in partialRoute)
            {
                element.TrackCircuitSection.RemoveTrain(this, false);
            }
        }

        // Initial train placement
        internal virtual bool InitialTrainPlacement()
        {
            // for initial placement, use direction 0 only
            // set initial positions

            TrackNode tn = FrontTDBTraveller.TN;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)FrontTDBTraveller.Direction;

            PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            DistanceTravelledM = 0.0f;

            tn = RearTDBTraveller.TN;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)RearTDBTraveller.Direction;

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);

            // check if train has route, if not create dummy
            if (ValidRoute[0] == null)
            {
                ValidRoute[0] = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                        PresentPosition[Direction.Backward].Direction, Length, true, true, false);
            }

            // get index of first section in route
            int rearIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
            if (rearIndex < 0)
            {
                rearIndex = 0;
            }

            PresentPosition[Direction.Backward].RouteListIndex = rearIndex;

            // get index of front of train
            int frontIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            if (frontIndex < 0)
            {
                Trace.TraceWarning("Start position of front of train {0}, service name {1} not on route ", Number, Name);
                frontIndex = 0;
            }

            PresentPosition[Direction.Forward].RouteListIndex = frontIndex;

            // check if train can be placed
            // get index of section in train route //
            int routeIndex = rearIndex;
            List<TrackCircuitSection> placementSections = new List<TrackCircuitSection>();

            // check if route is available
            offset = PresentPosition[Direction.Backward].Offset;
            float remLength = Length;
            bool sectionAvailable = true;

            for (int iRouteIndex = rearIndex; iRouteIndex <= frontIndex && sectionAvailable; iRouteIndex++)
            {
                TrackCircuitSection section = ValidRoute[0][iRouteIndex].TrackCircuitSection;
                if (section.CanPlaceTrain(this, offset, remLength))
                {
                    placementSections.Add(section);
                    remLength -= (section.Length - offset);

                    if (remLength > 0)
                    {
                        if (routeIndex < ValidRoute[0].Count - 1)
                        {
                            routeIndex++;
                            offset = 0.0f;
                        }
                        else
                        {
                            Trace.TraceWarning("No sufficient track to place train");
                            sectionAvailable = false;
                        }
                    }

                }
                else
                {
                    sectionAvailable = false;
                }
            }

            // if not available - return
            if (!sectionAvailable || placementSections.Count <= 0)
            {
                return false;
            }

            // set any deadlocks for sections ahead of start with end beyond start
            for (int i = 0; i < rearIndex; i++)
            {
                int rearSectionIndex = ValidRoute[0][i].TrackCircuitSection.Index;
                if (DeadlockInfo.ContainsKey(rearSectionIndex))
                {
                    foreach (Dictionary<int, int> deadlock in DeadlockInfo[rearSectionIndex])
                    {
                        foreach (KeyValuePair<int, int> deadlockDetail in deadlock)
                        {
                            int endSectionIndex = deadlockDetail.Value;
                            if (ValidRoute[0].GetRouteIndex(endSectionIndex, rearIndex) >= 0)
                            {
                                TrackCircuitSection.TrackCircuitList[endSectionIndex].SetDeadlockTrap(Number, deadlockDetail.Key);
                            }
                        }
                    }
                }
            }

            // set track occupied (if not done yet)
            foreach (TrackCircuitSection section in placementSections)
            {
                if (!section.IsSet(routedForward, false))
                {
                    section.Reserve(routedForward, ValidRoute[0]);
                    section.SetOccupied(routedForward);
                }
            }

            return true;
        }

        /// <summary>
        /// Set Formed Occupied
        /// Set track occupied for train formed out of other train
        /// </summary>
        internal void SetFormedOccupied()
        {

            int rearIndex = PresentPosition[Direction.Backward].RouteListIndex;
            int frontIndex = PresentPosition[Direction.Forward].RouteListIndex;

            int routeIndex = rearIndex;

            List<TrackCircuitSection> placementSections = new List<TrackCircuitSection>();

            // route is always available as previous train was there

            float offset = PresentPosition[Direction.Backward].Offset;
            float remLength = Length;

            for (int iRouteIndex = rearIndex; iRouteIndex <= frontIndex; iRouteIndex++)
            {
                TrackCircuitSection thisSection = ValidRoute[0][iRouteIndex].TrackCircuitSection;
                placementSections.Add(thisSection);
                remLength -= thisSection.Length - offset;

                if (remLength > 0)
                {
                    if (routeIndex < ValidRoute[0].Count - 1)
                    {
                        routeIndex++;
                        offset = 0.0f;
                    }
                    else
                    {
                        Trace.TraceWarning("No sufficient track to place train");
                    }
                }
            }

            // set track occupied (if not done yet)
            foreach (TrackCircuitSection section in placementSections)
            {
                if (!section.IsSet(routedForward, false))
                {
                    section.Reserve(routedForward, ValidRoute[0]);
                    section.SetOccupied(routedForward);
                }
            }
        }

        /// <summary>
        /// Check if train is stopped in station
        /// </summary>
        /// <param name="platform"></param>
        /// <param name="stationDirection"></param>
        /// <param name="stationTCSectionIndex"></param>
        /// <returns></returns>
        internal virtual bool CheckStationPosition(PlatformDetails platform, TrackDirection stationDirection, int stationTCSectionIndex)
        {
            bool atStation = false;
            float platformBeginOffset = platform.TrackCircuitOffset[Location.NearEnd, stationDirection];
            float platformEndOffset = platform.TrackCircuitOffset[Location.FarEnd, stationDirection];
            int endSectionIndex = stationDirection == TrackDirection.Ahead ?
                    platform.TCSectionIndex[platform.TCSectionIndex.Count - 1] :
                    platform.TCSectionIndex[0];
            int endSectionRouteIndex = ValidRoute[0].GetRouteIndex(endSectionIndex, 0);

            int beginSectionIndex = stationDirection == TrackDirection.Reverse ?
                    platform.TCSectionIndex[platform.TCSectionIndex.Count - 1] :
                    platform.TCSectionIndex[0];
            int beginSectionRouteIndex = ValidRoute[0].GetRouteIndex(beginSectionIndex, 0);

            // if rear is in platform, station is valid
            if (((((beginSectionRouteIndex != -1 && PresentPosition[Direction.Backward].RouteListIndex == beginSectionRouteIndex) || (PresentPosition[Direction.Backward].RouteListIndex == -1 && PresentPosition[Direction.Backward].TrackCircuitSectionIndex == beginSectionIndex))
                && PresentPosition[Direction.Backward].Offset >= platformBeginOffset) || PresentPosition[Direction.Backward].RouteListIndex > beginSectionRouteIndex) &&
                ((PresentPosition[Direction.Backward].TrackCircuitSectionIndex == endSectionIndex && PresentPosition[Direction.Backward].Offset <= platformEndOffset) || endSectionRouteIndex == -1 && beginSectionRouteIndex != -1 ||
                PresentPosition[Direction.Backward].RouteListIndex < endSectionRouteIndex))
            {
                atStation = true;
            }
            // if front is in platform and most of the train is as well, station is valid
            else if (((((endSectionRouteIndex != -1 && PresentPosition[Direction.Forward].RouteListIndex == endSectionRouteIndex) || (PresentPosition[Direction.Forward].RouteListIndex == -1 && PresentPosition[Direction.Forward].TrackCircuitSectionIndex == endSectionIndex))
                && PresentPosition[Direction.Forward].Offset <= platformEndOffset) && ((platform.Length - (platformEndOffset - PresentPosition[Direction.Forward].Offset)) > Length / 2)) ||
                (PresentPosition[Direction.Forward].RouteListIndex != -1 && PresentPosition[Direction.Forward].RouteListIndex < endSectionRouteIndex &&
                (PresentPosition[Direction.Forward].RouteListIndex > beginSectionRouteIndex || (PresentPosition[Direction.Forward].RouteListIndex == beginSectionRouteIndex && PresentPosition[Direction.Forward].Offset >= platformBeginOffset))))
            {
                atStation = true;
            }
            // if front is beyond platform and and most of the train is within it, station is valid (isn't it already covered by cases 1 or 4?)
            else if (endSectionRouteIndex != -1 && PresentPosition[Direction.Forward].RouteListIndex == endSectionRouteIndex && PresentPosition[Direction.Forward].Offset > platformEndOffset &&
                     (PresentPosition[Direction.Forward].Offset - platformEndOffset) < (Length / 3))
            {
                atStation = true;
            }
            // if front is beyond platform and rear is not on route or before platform : train spans platform
            else if (((endSectionRouteIndex != -1 && PresentPosition[Direction.Forward].RouteListIndex > endSectionRouteIndex) || (endSectionRouteIndex != -1 && PresentPosition[Direction.Forward].RouteListIndex == endSectionRouteIndex && PresentPosition[Direction.Forward].Offset >= platformEndOffset))
                  && (PresentPosition[Direction.Backward].RouteListIndex < beginSectionRouteIndex || (PresentPosition[Direction.Backward].RouteListIndex == beginSectionRouteIndex && PresentPosition[Direction.Backward].Offset <= platformBeginOffset)))
            {
                atStation = true;
            }

            return atStation;
        }

        //================================================================================================//
        /// <summary>
        /// Update train position
        /// </summary>
        internal void UpdateTrainPosition()
        {
            // update positions

            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            TrackNode tn = FrontTDBTraveller.TN;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)FrontTDBTraveller.Direction;
            int routeIndex;

            PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            PresentPosition[Direction.Forward].RouteListIndex = routeIndex;

            tn = RearTDBTraveller.TN;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)RearTDBTraveller.Direction;

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
            PresentPosition[Direction.Backward].RouteListIndex = routeIndex;

            if (jumpRequested) // jump do be performed in multiplayer mode when train re-enters game in different position
            {
                jumpRequested = false;
                PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);
                Trace.TraceInformation("Multiplayer server requested the player train to jump");
                // reset some items
                SignalObjectItems.Clear();
                NextSignalObject[0] = null;
                InitializeSignals(true);
                LastReservedSection[0] = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            }

            // get reserved length
            ReservedTrackLengthM = GetReservedLength();
        }

        /// <summary>
        /// Update Position linked information
        /// Switches train to Out_Of_Control if it runs out of path
        /// <\summary>
        protected void UpdateTrainPositionInformation()
        {

            // check if train still on route - set train to OUT_OF_CONTROL
            PresentPosition[Direction.Forward].DistanceTravelled = DistanceTravelledM;
            PresentPosition[Direction.Backward].DistanceTravelled = DistanceTravelledM - Length;

            if (PresentPosition[Direction.Forward].RouteListIndex < 0)
            {
                SetTrainOutOfControl(OutOfControlReason.OutOfPath);
            }
            else if (StationStops.Count > 0)
            {
                ComputeDistanceToNextStation(StationStops[0]);
            }
        }

        /// <summary>
        /// compute boarding time for activity mode
        /// also check validity of depart time value
        /// <\summary>
        internal virtual (bool, int) ComputeTrainBoardingTime(StationStop stationStop, int stopTime)
        {
            stopTime = stationStop.ComputeStationBoardingTime(this);
            return (stationStop.CheckScheduleValidity(this), stopTime);
        }

        /// <summary>
        /// Compute distance to next station
        /// <\summary>
        private void ComputeDistanceToNextStation(StationStop station)
        {
            TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
            float leftInSectionM = section.Length - PresentPosition[Direction.Forward].Offset;
            float distanceToTrainM;
            int stationIndex;

            if (station.SubrouteIndex > TCRoute.ActiveSubPath && !simulator.TimetableMode)
            // if the station is in a further subpath, distance computation is longer
            {
                // first compute distance up to end or reverse point of activeSubpath. To be restudied for subpaths with no reversal
                if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                    distanceToTrainM = ComputeDistanceToReversalPoint();
                else
                {
                    int lastSectionRouteIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count - 1;
                    float lastSectionLength = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][lastSectionRouteIndex].TrackCircuitSection.Length;
                    distanceToTrainM = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].GetDistanceAlongRoute(PresentPosition[Direction.Forward].RouteListIndex,
                        leftInSectionM, lastSectionRouteIndex, lastSectionLength, true);
                }

                int firstSection = 0;
                float firstSectionOffsetToGo = 0;
                int lastSection = 0;
                float lastSectionOffsetToGo = 0;
                if (distanceToTrainM >= 0)
                {

                    // compute length of intermediate subpaths, if any, from reversal or section at beginning to reversal or section at end
                    for (int i = TCRoute.ActiveSubPath + 1; i < station.SubrouteIndex; i++)
                    {
                        if (TCRoute.ReversalInfo[i - 1].Valid)
                        // skip sections before reversal at beginning of path
                        {
                            for (int j = 0; j < TCRoute.TCRouteSubpaths[i].Count; j++)
                            {
                                if (TCRoute.TCRouteSubpaths[i][j].TrackCircuitSection.Index == TCRoute.ReversalInfo[i - 1].ReversalSectionIndex)
                                {
                                    firstSection = j;
                                    firstSectionOffsetToGo = TCRoute.ReversalInfo[i - 1].ReverseReversalOffset;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            for (int j = 0; j < TCRoute.TCRouteSubpaths[i].Count; j++)
                            {
                                if (TCRoute.TCRouteSubpaths[i][j].TrackCircuitSection.Index ==
                                    TCRoute.TCRouteSubpaths[i - 1][TCRoute.TCRouteSubpaths[i - 1].Count - 1].TrackCircuitSection.Index)
                                {
                                    firstSection = j + 1;
                                    firstSectionOffsetToGo = TCRoute.TCRouteSubpaths[i][firstSection].TrackCircuitSection.Length;
                                    break;
                                }
                            }
                        }

                        if (TCRoute.ReversalInfo[i].Valid)
                        // skip sections before reversal at beginning of path
                        {
                            for (int j = TCRoute.TCRouteSubpaths[i].Count - 1; j >= 0; j--)
                            {
                                if (TCRoute.TCRouteSubpaths[i][j].TrackCircuitSection.Index == TCRoute.ReversalInfo[i].ReversalSectionIndex)
                                {
                                    lastSection = j;
                                    lastSectionOffsetToGo = TCRoute.ReversalInfo[i].ReverseReversalOffset;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            lastSection = TCRoute.TCRouteSubpaths[i].Count - 1;
                            lastSectionOffsetToGo = TCRoute.TCRouteSubpaths[i][lastSection].TrackCircuitSection.Length;
                        }

                        float lengthOfIntSubpath = TCRoute.TCRouteSubpaths[i].GetDistanceAlongRoute(firstSection, firstSectionOffsetToGo, lastSection, lastSectionOffsetToGo, true);
                        if (lengthOfIntSubpath < 0)
                        {
                            distanceToTrainM = -1;
                            break;
                        }
                        distanceToTrainM += lengthOfIntSubpath;
                    }
                }
                if (distanceToTrainM >= 0)
                {
                    // finally compute distance from start of station subpath up to station
                    if (TCRoute.ReversalInfo[station.SubrouteIndex - 1].Valid)
                    // skip sections before reversal at beginning of path
                    {
                        for (int j = 0; j < TCRoute.TCRouteSubpaths[station.SubrouteIndex].Count; j++)
                        {
                            if (TCRoute.TCRouteSubpaths[station.SubrouteIndex][j].TrackCircuitSection.Index == TCRoute.ReversalInfo[station.SubrouteIndex - 1].ReversalSectionIndex)
                            {
                                firstSection = j;
                                firstSectionOffsetToGo = TCRoute.ReversalInfo[station.SubrouteIndex - 1].ReverseReversalOffset;
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int j = 0; j < TCRoute.TCRouteSubpaths[station.SubrouteIndex].Count; j++)
                        {
                            if (TCRoute.TCRouteSubpaths[station.SubrouteIndex][j].TrackCircuitSection.Index ==
                                TCRoute.TCRouteSubpaths[station.SubrouteIndex - 1][TCRoute.TCRouteSubpaths[station.SubrouteIndex - 1].Count - 1].TrackCircuitSection.Index)
                            {
                                firstSection = j + 1;
                                firstSectionOffsetToGo = TCRoute.TCRouteSubpaths[station.SubrouteIndex][firstSection].TrackCircuitSection.Length;
                                break;
                            }
                        }
                    }

                    stationIndex = station.RouteIndex;
                    float distanceFromStartOfsubPath = TCRoute.TCRouteSubpaths[station.SubrouteIndex].GetDistanceAlongRoute(firstSection, firstSectionOffsetToGo, stationIndex, station.StopOffset, true);
                    if (distanceFromStartOfsubPath < 0) 
                        distanceToTrainM = -1;
                    else 
                        distanceToTrainM += distanceFromStartOfsubPath;
                }
            }
            else
            {
                // No enhanced compatibility, simple computation
                // if present position off route, try rear position
                // if both off route, skip station stop
                stationIndex = ValidRoute[0].GetRouteIndex(station.TrackCircuitSectionIndex, PresentPosition[Direction.Forward].RouteListIndex);
                distanceToTrainM = ValidRoute[0].GetDistanceAlongRoute(PresentPosition[Direction.Forward].RouteListIndex, leftInSectionM, stationIndex, station.StopOffset, true);
            }

            station.DistanceToTrainM = distanceToTrainM;
        }

        /// <summary>
        /// Compute distance to reversal point
        /// <\summary>
        protected float ComputeDistanceToReversalPoint()
        {
            float lengthToGoM = -PresentPosition[Direction.Forward].Offset;
            TrackCircuitSection section;
            if (PresentPosition[Direction.Forward].RouteListIndex == -1)
            {
                Trace.TraceWarning($"Train {Number} service {Name} off path; distance to reversal point set to -1");
                return -1;
            }
            // in case the AI train is out of its original path the reversal info is simulated to point to the end of the last route section
            int reversalRouteIndex = ValidRoute[0].GetRouteIndex(TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex, PresentPosition[Direction.Forward].RouteListIndex);
            if (reversalRouteIndex == -1)
            {
                Trace.TraceWarning($"Train {Number} service {Name}, reversal or end point off path; distance to reversal point set to -1");
                return -1;
            }

            TrackCircuitSection reversalSection = TrackCircuitSection.TrackCircuitList[TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex];
            float reverseReversalOffset = TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReverseReversalOffset;
            if (PresentPosition[Direction.Forward].RouteListIndex <= reversalRouteIndex)
            {
                for (int i = PresentPosition[Direction.Forward].RouteListIndex; i < ValidRoute[0].Count; i++)
                {
                    section = ValidRoute[0][i].TrackCircuitSection;
                    if (section.Index == reversalSection.Index)
                    {
                        break;
                    }
                    else 
                        lengthToGoM += section.Length;
                }
                return lengthToGoM += reverseReversalOffset;
            }
            else
            {
                for (int i = PresentPosition[Direction.Forward].RouteListIndex - 1; i >= 0; i--)
                {
                    section = ValidRoute[0][i].TrackCircuitSection;
                    if (section.Index == reversalSection.Index)
                    {
                        break;
                    }
                    else 
                        lengthToGoM -= section.Length;
                }
                return lengthToGoM += reverseReversalOffset - reversalSection.Length;
            }
        }

        /// <summary>
        /// Compute path length
        /// <\summary>
        internal float ComputePathLength()
        {
            float pathLength = 0;
            int tcRouteSubpathIndex = -1;
            foreach (TrackCircuitPartialPathRoute tcRouteSubpath in TCRoute.TCRouteSubpaths)
            {
                tcRouteSubpathIndex++;
                if (tcRouteSubpathIndex > 0 && TCRoute.ReversalInfo[tcRouteSubpathIndex - 1].Valid) 
                    pathLength += TCRoute.ReversalInfo[tcRouteSubpathIndex - 1].ReverseReversalOffset;
                else if (tcRouteSubpathIndex > 0) 
                    pathLength += TCRoute.ReversalInfo[tcRouteSubpathIndex - 1].ReverseReversalOffset - 
                        TrackCircuitSection.TrackCircuitList[TCRoute.ReversalInfo[tcRouteSubpathIndex - 1].ReversalSectionIndex].Length;
                else { } //start point offset?

                int routeListIndex = 1;
                TrackCircuitSection section;
                int reversalRouteIndex = tcRouteSubpath.GetRouteIndex(TCRoute.ReversalInfo[tcRouteSubpathIndex].ReversalSectionIndex, routeListIndex);
                if (reversalRouteIndex == -1)
                {
                    Trace.TraceWarning($"Train {Number} service {Name}, reversal or end point off path; distance to reversal point set to -1");
                    return -1;
                }
                if (routeListIndex <= reversalRouteIndex)
                {
                    for (int i = routeListIndex; i < tcRouteSubpath.Count; i++)
                    {
                        section = tcRouteSubpath[i].TrackCircuitSection;
                        if (section.Index == TCRoute.ReversalInfo[tcRouteSubpathIndex].ReversalSectionIndex)
                        {
                            break;
                        }
                        else 
                            pathLength += section.Length;
                    }
                    pathLength += TCRoute.ReversalInfo[tcRouteSubpathIndex].ReverseReversalOffset;
                }
                else
                {
                    pathLength += TCRoute.ReversalInfo[tcRouteSubpathIndex].ReverseReversalOffset -
                    TrackCircuitSection.TrackCircuitList[TCRoute.ReversalInfo[tcRouteSubpathIndex].ReversalSectionIndex].Length;
                }
            }
            return pathLength;
        }


        /// <summary>
        /// get list of required actions (only if not moving backward)
        /// </summary>
        protected void ObtainRequiredActions(int backward)
        {
            if (this is AITrain aiTrain && aiTrain.MovementState == AITrain.AI_MOVEMENT_STATE.SUSPENDED) 
                return;
            if (backward < BackwardThreshold)
            {
                List<DistanceTravelledItem> nowActions = requiredActions.GetActions(DistanceTravelledM);
                if (nowActions.Count > 0)
                {
                    PerformActions(nowActions);
                }
            }
            if (backward < BackwardThreshold || SpeedMpS > -0.01)
            {
                List<DistanceTravelledItem> nowActions = AuxActionsContainer.specRequiredActions.GetAuxActions(this, DistanceTravelledM);

                if (nowActions.Count > 0)
                {
                    PerformActions(nowActions);
                }
            }
        }

        /// <summary>
        /// Update section occupy states
        /// Input is backward movement counter
        /// </summary>
        public void UpdateSectionState(int backward)
        {
            // don't bother with update if train out of control - all will be reset when train is stopped
            if (ControlMode == TrainControlMode.OutOfControl)
            {
                return;
            }

            int lastIndex = PreviousPosition[Direction.Forward].RouteListIndex;
            int presentIndex = PresentPosition[Direction.Forward].RouteListIndex;

            // don't bother with update if train off route - set train to out of control
            if (presentIndex < 0)
            {
                SetTrainOutOfControl(OutOfControlReason.OutOfPath);
                return;
            }

            int lastDTM = (int)PreviousPosition[Direction.Forward].DistanceTravelled;
            TrackCircuitSection lastSection = TrackCircuitSection.TrackCircuitList[PreviousPosition[Direction.Forward].TrackCircuitSectionIndex];
            int lastDTatEndLastSectionM = lastDTM + (int)(lastSection.Length - PreviousPosition[Direction.Forward].Offset);

            int presentDTM = (int)DistanceTravelledM;

            List<int[]> sectionList = new List<int[]>();

            if (backward > BackwardThreshold) // train moved backward

            {
                if (presentIndex < lastIndex)
                {
                    for (int i = lastIndex; i > presentIndex; i--)
                    {
                        sectionList.Add(new int[2] { i, presentDTM });
                    }
                    sectionList.Add(new int[2] { presentIndex, presentDTM });
                }
            }
            else // train moves forward
            {
                if (presentIndex > lastIndex)
                {
                    int lastValidDTM = lastDTatEndLastSectionM;

                    for (int i = lastIndex + 1; i < presentIndex; i++)
                    {
                        sectionList.Add(new int[2] { i, lastValidDTM });
                        lastValidDTM += (int)ValidRoute[0][i].TrackCircuitSection.Length;
                    }
                    sectionList.Add(new int[2] { presentIndex, presentDTM });
                }
            }

            // set section states, for AUTOMODE use direction 0 only
            foreach (int[] routeListIndex in sectionList)
            {
                TrackCircuitSection section = ValidRoute[0][routeListIndex[0]].TrackCircuitSection;
                if (!section.CircuitState.OccupiedByThisTrain(routedForward))
                {
                    section.SetOccupied(routedForward, routeListIndex[1]);
                    if (!simulator.TimetableMode && section.CircuitState.OccupiedByOtherTrains(routedForward))
                    {
                        SwitchToNodeControl(section.Index);
                        EndAuthorityTypes[0] = EndAuthorityType.TrainAhead;
                        ChangeControlModeOtherTrains(section);
                    }
                    // additional actions for child classes
                    UpdateSectionStateAdditional(section.Index);
                }
            }
        }

        /// <summary>
        /// Change control mode of other trains in same section if needed
        /// </summary>
        private void ChangeControlModeOtherTrains(TrackCircuitSection section)
        {
            TrackDirection owndirection = PresentPosition[Direction.Forward].Direction;
            foreach (KeyValuePair<TrainRouted, int> trainToCheckInfo in section.CircuitState.OccupationState)
            {
                Train otherTrain = trainToCheckInfo.Key.Train;
                if (otherTrain.ControlMode == TrainControlMode.AutoSignal) // train is still in signal mode, might need adjusting
                {
                    TrackDirection otherdirection = otherTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex == section.Index ? otherTrain.PresentPosition[Direction.Forward].Direction :
                        otherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex == section.Index ? otherTrain.PresentPosition[Direction.Backward].Direction : (TrackDirection)(-1);
                    if (owndirection >= 0 && otherdirection >= 0) // both trains found
                    {
                        if (owndirection != otherdirection) // opposite directions - this train is now ahead of train in section
                        {
                            otherTrain.SwitchToNodeControl(section.Index);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if train went passed signal
        /// if so, and signal was at danger, set train Out_Of_Control
        /// </summary>
        private protected int CheckSignalPassed(int direction, TrackCircuitPosition trainPosition, TrackCircuitPosition trainPreviousPos)
        {
            int passedSignalIndex = -1;
            if (NextSignalObject[direction] != null)
            {

                while (NextSignalObject[direction] != null && !ValidRoute[direction].SignalIsAheadOfTrain(NextSignalObject[direction], trainPosition)) // signal not in front //
                {
                    // correct route index if necessary
                    int correctedRouteIndex = ValidRoute[0].GetRouteIndex(trainPreviousPos.TrackCircuitSectionIndex, 0);
                    if (correctedRouteIndex >= 0) 
                        trainPreviousPos.RouteListIndex = correctedRouteIndex;
                    // check if train really went passed signal in correct direction
                    if (ValidRoute[direction].SignalIsAheadOfTrain(NextSignalObject[direction], trainPreviousPos)) // train was in front on last check, so we did pass
                    {
                        SignalAspectState signalState = GetNextSignalAspect(direction);
                        passedSignalIndex = NextSignalObject[direction].Index;

                        if (signalState == SignalAspectState.Stop && NextSignalObject[direction].OverridePermission == SignalPermission.Denied)
                        {
                            Trace.TraceWarning($"Train {Name} ({Number}) passing signal {NextSignalObject[direction].Index} at {DistanceTravelledM:###0.0} at danger at {SpeedMpS:##0.00}");
                            SetTrainOutOfControl(OutOfControlReason.PassedAtDanger);
                            break;
                        }
                        else if (ControlMode == TrainControlMode.AutoSignal && NextSignalObject[direction].Signalfound[(int)SignalFunction.Normal] < 0) // no next signal
                        {
                            SwitchToNodeControl(LastReservedSection[direction]);
                            break;
                        }
                        else if (ControlMode == TrainControlMode.AutoSignal && NextSignalObject[direction].BlockState() != SignalBlockState.Clear) // route to next signal not clear
                        {
                            SwitchToNodeControl(LastReservedSection[direction]);
                            break;
                        }

                        // get next signal
                        int nextSignalIndex = NextSignalObject[direction].Signalfound[(int)SignalFunction.Normal];
                        if (nextSignalIndex >= 0)
                        {
                            NextSignalObject[direction] = signalRef.Signals[nextSignalIndex];

                            int reqSectionIndex = NextSignalObject[direction].TrackCircuitIndex;
                            float endOffset = NextSignalObject[direction].TrackCircuitOffset;

                            DistanceToSignal = GetDistanceToTrain(reqSectionIndex, endOffset);
                        }
                        else
                        {
                            NextSignalObject[direction] = null;
                        }
                    }
                    else
                    {
                        // get next signal
                        int nextSignalIndex = NextSignalObject[direction].Signalfound[(int)SignalFunction.Normal];
                        if (nextSignalIndex >= 0)
                        {
                            NextSignalObject[direction] = signalRef.Signals[nextSignalIndex];

                            int reqSectionIndex = NextSignalObject[direction].TrackCircuitIndex;
                            float endOffset = NextSignalObject[direction].TrackCircuitOffset;

                            DistanceToSignal = GetDistanceToTrain(reqSectionIndex, endOffset);
                        }
                        else
                        {
                            NextSignalObject[direction] = null;
                        }
                    }
                }
            }
            return passedSignalIndex;
        }

        /// <summary>
        /// Check if train moves backward and if so, check clearance behindtrain
        /// If no save clearance left, set train to Out_Of_Control
        /// </summary>
        protected int CheckBackwardClearance()
        {
            bool outOfControl = false;

            int lastIndex = PreviousPosition[Direction.Forward].RouteListIndex;
            float lastOffset = PreviousPosition[Direction.Forward].Offset;
            int presentIndex = PresentPosition[Direction.Forward].RouteListIndex;
            float presentOffset = PresentPosition[Direction.Forward].Offset;

            if (presentIndex < 0) // we are off the path, stop train //
            {
                SetTrainOutOfControl(OutOfControlReason.OutOfPath);
            }

            // backward
            if (presentIndex < lastIndex || (presentIndex == lastIndex && presentOffset < lastOffset))
            {
                movedBackward = movedBackward < 2 * BackwardThreshold ? ++movedBackward : movedBackward;
            }

            if (movedBackward > BackwardThreshold)
            {
                // run through sections behind train
                // if still in train route : try to reserve section
                // if multiple train in section : calculate distance to next train, stop oncoming train
                // if section reserved for train : stop train
                // if out of route : set out_of_control
                // if signal : set distance, check if passed

                // TODO : check if other train in section, get distance to train
                // TODO : check correct alignment of any switches passed over while moving backward (reset activepins)

                if (rearSignalObject != null)
                {

                    // create new position some 25 m. behind train as allowed overlap
                    TrackCircuitPosition overlapPosition = new TrackCircuitPosition(PresentPosition[Direction.Backward]);
                    TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[overlapPosition.TrackCircuitSectionIndex];
                    overlapPosition.Offset = section.Length - (PresentPosition[Direction.Backward].Offset + rearPositionOverlap);  // reverse offset because of reversed direction
                    overlapPosition.Direction = overlapPosition.Direction.Next(); // looking backwards, so reverse direction

                    TrackCircuitSection rearSection = TrackCircuitSection.TrackCircuitList[rearSignalObject.TrackCircuitNextIndex];
                    if (!IsAheadOfTrain(rearSection, 0.0f, overlapPosition))
                    {
                        if (rearSignalObject.SignalLR(SignalFunction.Normal) == SignalAspectState.Stop)
                        {
                            Trace.TraceWarning($"Train {Name} ({Number}) passing rear signal {rearSignalObject.Index} at {DistanceTravelledM:###0.0} at danger at {SpeedMpS:##0.00)}");
                            SetTrainOutOfControl(OutOfControlReason.RearPassedAtDanger);
                            outOfControl = true;
                        }
                        else
                        {
                            rearSignalObject = null;   // passed signal, so reset //
                        }
                    }
                }

                if (!outOfControl && rearSignalObject == null)
                {
                    TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];
                    float clearPath = section.Length - PresentPosition[Direction.Backward].Offset;   // looking other direction //
                    TrackDirection direction = PresentPosition[Direction.Backward].Direction.Next();

                    while (clearPath < rearPositionOverlap && !outOfControl && rearSignalObject == null)
                    {
                        if (section.EndSignals[direction] != null)
                        {
                            rearSignalObject = section.EndSignals[direction];
                        }
                        else
                        {
                            TrackDirection pinLink = direction.Next();

                            // TODO : check required junction and crossover path

                            int nextSectionIndex = section.Pins[pinLink, Location.NearEnd].Link;
                            if (nextSectionIndex >= 0)
                            {
                                TrackCircuitSection nextSection = TrackCircuitSection.TrackCircuitList[nextSectionIndex];
                                if (!nextSection.IsAvailable(this))
                                {
                                    SetTrainOutOfControl(OutOfControlReason.SlippedIntoPath);
                                    outOfControl = true;

                                    // stop train in path

                                    List<TrainRouted> trainsInSection = nextSection.CircuitState.TrainsOccupying();
                                    foreach (TrainRouted nextTrain in trainsInSection)
                                    {
                                        nextTrain.Train.ForcedStop(Simulator.Catalog.GetString("Other train is blocking path"), Name, Number);
                                    }

                                    if (nextSection.CircuitState.TrainReserved != null)
                                    {
                                        nextSection.CircuitState.TrainReserved.Train.ForcedStop(Simulator.Catalog.GetString("Other train is blocking path"), Name, Number);
                                    }
                                }
                                else
                                {
                                    clearPath += nextSection.Length;
                                    section = nextSection;
                                    if (section.CircuitType == TrackCircuitType.EndOfTrack)
                                    {
                                        SetTrainOutOfControl(OutOfControlReason.SlippedToEndOfTrack);
                                        outOfControl = true;
                                    }
                                }
                            }
                        }
                    }

                    if (outOfControl)
                    {
                        clearanceAtRearM = -1;
                        rearSignalObject = null;
                    }
                    else
                    {
                        clearanceAtRearM = clearPath;
                    }
                }
            }
            else
            {
                movedBackward = movedBackward >= 0 ? --movedBackward : movedBackward;
                clearanceAtRearM = -1;
                rearSignalObject = null;
            }

            return movedBackward;
        }

        /// <summary>
        /// Check for end of route actions - for activity PLAYER train only
        /// Reverse train if required
        /// Return parameter : true if train still exists (only used in timetable mode)
        /// </summary>
        protected virtual bool CheckRouteActions(double elapsedClockSeconds)
        {
            TrackDirection directionNow = PresentPosition[Direction.Forward].Direction;
            int positionNow = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            TrackDirection directionNowBack = PresentPosition[Direction.Backward].Direction;
            int positionNowBack = PresentPosition[Direction.Backward].TrackCircuitSectionIndex;

            if (PresentPosition[Direction.Forward].RouteListIndex >= 0)
                directionNow = ValidRoute[0][PresentPosition[Direction.Forward].RouteListIndex].Direction;

            (bool endOfRoute, bool otherRouteAvailable) = UpdateRouteActions(elapsedClockSeconds, false);

            AuxActionsContainer.SetAuxAction(this);
            if (!endOfRoute) 
                return true;  // not at end of route

            // check if train reversed
            if (otherRouteAvailable)
            {
                if (positionNowBack == PresentPosition[Direction.Forward].TrackCircuitSectionIndex && directionNowBack != PresentPosition[Direction.Forward].Direction)
                {
                    ReverseFormation(IsActualPlayerTrain);
                    // active subpath must be incremented in parallel in incorporated train if present
                    if (IncorporatedTrainNo >= 0) 
                        IncrementSubpath(simulator.TrainDictionary[IncorporatedTrainNo]);
                }
                else if (positionNow == PresentPosition[Direction.Backward].TrackCircuitSectionIndex && directionNow != PresentPosition[Direction.Backward].Direction)
                {
                    ReverseFormation(IsActualPlayerTrain);
                    // active subpath must be incremented in parallel in incorporated train if present
                    if (IncorporatedTrainNo >= 0) 
                        IncrementSubpath(simulator.TrainDictionary[IncorporatedTrainNo]);
                }
            }

            // check if next station was on previous subpath - if so, move to this subpath

            if (otherRouteAvailable && StationStops.Count > 0)
            {
                if (StationStops[0].SubrouteIndex < TCRoute.ActiveSubPath)
                {
                    StationStops[0].SubrouteIndex = TCRoute.ActiveSubPath;
                }
            }

            return true; // always return true for activity player train
        }


        /// <summary>
        /// Check for end of route actions
        /// Called every update, actions depend on route state
        /// returns :
        /// bool[0] "false" end of route not reached
        /// bool[1] "false" if no further route available
        /// </summary>
        protected (bool endOfRoute, bool otherRouteAvailable) UpdateRouteActions(double elapsedClockSeconds, bool checkLoop = true)
        {
            _ = elapsedClockSeconds;

            nextRouteReady = false;

            // check if train in loop
            // if so, forward to next subroute and continue
            if (checkLoop || StationStops.Count <= 1 || StationStops.Count > 1 && TCRoute != null && StationStops[1].SubrouteIndex > TCRoute.ActiveSubPath)
            {
                if (TCRoute != null && (ControlMode == TrainControlMode.AutoNode || ControlMode == TrainControlMode.AutoSignal) && TCRoute.LoopEnd[TCRoute.ActiveSubPath] >= 0)
                {
                    int loopSectionIndex = ValidRoute[0].GetRouteIndex(TCRoute.LoopEnd[TCRoute.ActiveSubPath], 0);

                    if (loopSectionIndex >= 0 && PresentPosition[Direction.Backward].RouteListIndex > loopSectionIndex)
                    {
                        int frontSection = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][PresentPosition[Direction.Forward].RouteListIndex].TrackCircuitSection.Index;
                        int rearSection = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][PresentPosition[Direction.Backward].RouteListIndex].TrackCircuitSection.Index;
                        TCRoute.ActiveSubPath++;
                        ValidRoute[0] = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];

                        PresentPosition[Direction.Forward].RouteListIndex = ValidRoute[0].GetRouteIndex(frontSection, 0);
                        PresentPosition[Direction.Backward].RouteListIndex = ValidRoute[0].GetRouteIndex(rearSection, 0);

                        // Invalidate preceding section indexes to avoid wrong indexing when building route forward (in Reserve())
                        for (int routeListIndex = 0; routeListIndex < PresentPosition[Direction.Backward].RouteListIndex; routeListIndex++)
                        {
                            ValidRoute[0][routeListIndex].Invalidate();
                        }
                        return (true, true);
                    }

                    // if loopend no longer on this valid route, remove loopend indication
                    else if (loopSectionIndex < 0)
                    {
                        TCRoute.LoopEnd[TCRoute.ActiveSubPath] = -1;
                    }
                }
            }

            // check position in relation to present end of path

            bool endOfRoute = CheckEndOfRoutePosition();

            // not end of route - no action
            if (!endOfRoute)
            {
                return (false, false);
            }

            // <CSComment> TODO: check if holding signals correctly released in case of reversal point between WP and signal
            // if next subpath available : check if it can be activated

            bool nextRouteAvailable = false;

            TrackCircuitPartialPathRoute nextRoute = null;

            if (endOfRoute && TCRoute.ActiveSubPath < (TCRoute.TCRouteSubpaths.Count - 1))
            {
                nextRouteAvailable = true;

                nextRoute = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath + 1];
                int firstSectionIndex = PresentPosition[Direction.Backward].TrackCircuitSectionIndex;

                // find index of present rear position

                int firstRouteIndex = nextRoute.GetRouteIndex(firstSectionIndex, 0);

                // if not found try index of present front position

                if (firstRouteIndex >= 0)
                {
                    nextRouteReady = true;
                }
                else
                {
                    firstSectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                    firstRouteIndex = nextRoute.GetRouteIndex(firstSectionIndex, 0);

                    // cant find next part of route - check if really at end of this route, if so, error, else just wait and see (train stopped for other reason)

                    if (PresentPosition[Direction.Forward].RouteListIndex == ValidRoute[0].Count - 1)
                    {
                        if (firstRouteIndex < 0)
                        {
                            Trace.TraceInformation($"Cannot find next part of route (index {TCRoute.ActiveSubPath}) for Train {Name} ({Number}) (at section {PresentPosition[Direction.Forward].TrackCircuitSectionIndex})");
                        }
                        // search for junction and check if it is not clear
                        else
                        {
                            bool junctionFound = false;
                            bool junctionOccupied = false;

                            for (int i = firstRouteIndex + 1; i < nextRoute.Count && !junctionFound; i++)
                            {
                                TrackCircuitSection section = nextRoute[i].TrackCircuitSection;
                                if (section.CircuitType == TrackCircuitType.Junction)
                                {
                                    junctionFound = true;
                                    if (section.CircuitState.OccupiedByThisTrain(this))
                                    {
                                        // Before deciding that route is not yet ready check if the new train head is off path because at end of new route
                                        section = nextRoute[nextRoute.Count - 1].TrackCircuitSection;
                                        if (section.CircuitState.OccupiedByThisTrain(this)) 
                                            break;
                                        junctionOccupied = true;
                                    }
                                }
                            }

                            if (!junctionOccupied)
                            {
                                nextRouteReady = true;
                            }
                        }
                    }
                    else
                    {
                        endOfRoute = false;
                    }
                }
            }

            // if end reached : clear any remaining reservations ahead
            if (endOfRoute && (!nextRouteAvailable || (nextRouteAvailable && nextRouteReady)))
            {
                if (ControlMode == TrainControlMode.AutoSignal) // for Auto mode try forward only
                {
                    if (NextSignalObject[0]?.EnabledTrain == routedForward)
                    {
                        NextSignalObject[0].ResetSignalEnabled();
                        int nextRouteIndex = ValidRoute[0].GetRouteIndex(NextSignalObject[0].TrackCircuitNextIndex, 0);

                        // clear rest of route to avoid accidental signal activation
                        if (nextRouteIndex >= 0)
                        {
                            signalRef.BreakDownRouteList(ValidRoute[0], nextRouteIndex, routedForward);
                            ValidRoute[0].RemoveRange(nextRouteIndex, ValidRoute[0].Count - nextRouteIndex);
                        }
                    }

                    if (PresentPosition[Direction.Forward].RouteListIndex >= 0 && PresentPosition[Direction.Forward].RouteListIndex < ValidRoute[0].Count - 1) // not at end of route
                    {
                        int nextRouteIndex = PresentPosition[Direction.Forward].RouteListIndex + 1;
                        signalRef.BreakDownRouteList(ValidRoute[0], nextRouteIndex, routedForward);
                        ValidRoute[0].RemoveRange(nextRouteIndex, ValidRoute[0].Count - nextRouteIndex);
                    }
                }

                int nextIndex = PresentPosition[Direction.Forward].RouteListIndex + 1;
                if (nextIndex <= (ValidRoute[0].Count - 1))
                {
                    signalRef.BreakDownRoute(ValidRoute[0][nextIndex].TrackCircuitSection.Index, routedForward);
                }

                // clear any remaining deadlocks
                ClearDeadlocks();
                DeadlockInfo.Clear();
            }

            // if next route available : reverse train, reset and reinitiate signals
            if (endOfRoute && nextRouteAvailable && nextRouteReady)
            {

                // check if reverse is required
                int newIndex = nextRoute.GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                TrackDirection oldDirection = ValidRoute[0][PresentPosition[Direction.Forward].RouteListIndex].Direction;
                if (newIndex < 0)
                {
                    newIndex = nextRoute.GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
                    oldDirection = ValidRoute[0][PresentPosition[Direction.Backward].RouteListIndex].Direction;
                }

                if (oldDirection != nextRoute[newIndex].Direction)
                {

                    // set new train positions and reset distance travelled
                    (PresentPosition[Direction.Forward], PresentPosition[Direction.Backward]) = (PresentPosition[Direction.Backward], PresentPosition[Direction.Forward]);

                    PresentPosition[Direction.Forward].Reverse(ValidRoute[0][PresentPosition[Direction.Forward].RouteListIndex].Direction, nextRoute, Length);
                    PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);
                    PresentPosition[Direction.Backward].Reverse(ValidRoute[0][PresentPosition[Direction.Backward].RouteListIndex].Direction, nextRoute, 0.0f);
                }
                else
                {
                    PresentPosition[Direction.Forward].RouteListIndex = nextRoute.GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                    PresentPosition[Direction.Backward].RouteListIndex = nextRoute.GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
                    PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);
                }

                DistanceTravelledM = PresentPosition[Direction.Forward].DistanceTravelled;

                // perform any remaining actions of type clear section (except sections now occupied)

                // reset old actions
                ClearActiveSectionItems();

                // set new route
                TCRoute.ActiveSubPath++;
                ValidRoute[0] = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];

                TCRoute.SetReversalOffset(Length, simulator.TimetableMode);

                // clear existing list of occupied track, and build new list
                for (int i = OccupiedTrack.Count - 1; i >= 0; i--)
                {
                    OccupiedTrack[i].ResetOccupied(this);
                }

                int rearIndex = PresentPosition[Direction.Backward].RouteListIndex;

                if (rearIndex < 0) // end of train not on new route
                {
                    TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                        PresentPosition[Direction.Backward].Direction, Length, false, true, false);

                    for (int i = 0; i < tempRoute.Count; i++)
                    {
                        tempRoute[i].TrackCircuitSection.SetOccupied(routedForward);
                    }
                }
                else
                {
                    for (int i = PresentPosition[Direction.Backward].RouteListIndex; i <= PresentPosition[Direction.Forward].RouteListIndex; i++)
                    {
                        ValidRoute[0][i].TrackCircuitSection.SetOccupied(routedForward);
                    }
                }

                // Check deadlock against all other trains
                CheckDeadlock(ValidRoute[0], Number);

                // reset signal information
                SignalObjectItems.Clear();
                NextSignalObject[0] = null;

                InitializeSignals(true);

                LastReservedSection[0] = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;

                // clear claims of any trains which have claimed present occupied sections upto common point - this avoids deadlocks
                // trains may have claimed while train was reversing
                TrackCircuitSection presentSection = TrackCircuitSection.TrackCircuitList[LastReservedSection[0]];
                presentSection.ClearReversalClaims(routedForward);

                // switch to NODE mode
                if (ControlMode == TrainControlMode.AutoSignal)
                {
                    SwitchToNodeControl(PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
                }
            }

            return (endOfRoute, nextRouteAvailable);
        }

        /// <summary>
        /// Check End of Route Position
        /// </summary>
        protected virtual bool CheckEndOfRoutePosition()
        {
            bool endOfRoute = false;

            // obtain reversal section index

            int reversalSectionIndex = -1;
            if (TCRoute != null && (ControlMode == TrainControlMode.AutoNode || ControlMode == TrainControlMode.AutoSignal))
            {
                TrackCircuitReversalInfo reversalInfo = TCRoute.ReversalInfo[TCRoute.ActiveSubPath];
                if (reversalInfo.Valid)
                {
                    reversalSectionIndex = reversalInfo.SignalUsed ? reversalInfo.LastSignalIndex : reversalInfo.LastDivergeIndex;
                }
            }

            // check if present subroute ends in reversal or is last subroute
            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid || TCRoute.ActiveSubPath == TCRoute.TCRouteSubpaths.Count - 1)
            {
                // can only be performed if train is stationary
                if (Math.Abs(SpeedMpS) > 0.03)
                    return endOfRoute;

                // check position in relation to present end of path
                // front is in last route section
                if (PresentPosition[Direction.Forward].RouteListIndex == (ValidRoute[0].Count - 1) &&
                    (!TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid && TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1))
                {
                    endOfRoute = true;
                }
                // front is within 150m. of end of route and no junctions inbetween (only very short sections ahead of train)
                else
                {
                    TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
                    float remainingLength = section.Length - PresentPosition[Direction.Forward].Offset;

                    bool junctionFound = false;
                    if (TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1)
                    {
                        for (int iIndex = PresentPosition[Direction.Forward].RouteListIndex + 1; iIndex < ValidRoute[0].Count && !junctionFound; iIndex++)
                        {
                            section = ValidRoute[0][iIndex].TrackCircuitSection;
                            junctionFound = section.CircuitType == TrackCircuitType.Junction;
                            remainingLength += section.Length;
                        }
                    }
                    else 
                        remainingLength = ComputeDistanceToReversalPoint();
                    float compatibilityNegligibleRouteChunk = ((TrainType == TrainType.Ai || TrainType == TrainType.AiPlayerHosting)
                        && TCRoute.TCRouteSubpaths.Count - 1 == TCRoute.ActiveSubPath) ? 40f : 5f;
                    float negligibleRouteChunk = compatibilityNegligibleRouteChunk;

                    if (remainingLength < negligibleRouteChunk && !junctionFound && !TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                    {
                        endOfRoute = true;
                    }
                }

                //<CSComment: check of vicinity to reverse point; only in subpaths ending with reversal
                if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                {
                    float distanceToReversalPoint = ComputeDistanceToReversalPoint();
                    if (distanceToReversalPoint < 50 && PresentPosition[Direction.Backward].RouteListIndex >= reversalSectionIndex)
                        endOfRoute = true;
                }
                // other checks unrelated to state
                if (!endOfRoute)
                {
                    // if last entry in route is END_OF_TRACK, check against previous entry as this can never be the trains position nor a signal reference section
                    int lastValidRouteIndex = ValidRoute[0].Count - 1;
                    if (ValidRoute[0][lastValidRouteIndex].TrackCircuitSection.CircuitType == TrackCircuitType.EndOfTrack)
                        lastValidRouteIndex--;

                    // if waiting for next signal and section beyond signal is last in route and there is no valid reversal index - end of route reached
                    if (NextSignalObject[0] != null && PresentPosition[Direction.Forward].TrackCircuitSectionIndex == NextSignalObject[0].TrackCircuitIndex &&
                         NextSignalObject[0].TrackCircuitNextIndex == ValidRoute[0][lastValidRouteIndex].TrackCircuitSection.Index && reversalSectionIndex < 0 &&
                         NextSignalObject[0].SignalLR(SignalFunction.Normal) == SignalAspectState.Stop && TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                    {
                        endOfRoute = true;
                    }
                }
            }

            // MSTS double reversal point: can be recognized and passed at speed > 0
            else
            {
                float distanceToReversalPoint = ComputeDistanceToReversalPoint();
                if (distanceToReversalPoint <= 0 && distanceToReversalPoint != -1) 
                    endOfRoute = true;
            }

            return endOfRoute;
        }

        /// <summary>
        /// Update route clearance ahead of train
        /// Called every update, actions depend on present control state
        /// </summary>
        protected void UpdateRouteClearanceAhead(int signalObjectIndex, int backward, double elapsedClockSeconds)
        {
            switch (ControlMode)
            {
                case (TrainControlMode.AutoSignal):
                    {
                        UpdateSignalMode(signalObjectIndex, backward, elapsedClockSeconds);
                        break;
                    }
                case (TrainControlMode.AutoNode):
                    {
                        UpdateNodeMode();
                        break;
                    }
                case (TrainControlMode.OutOfControl):
                    {
                        UpdateOutOfControl();
                        if (LeadLocomotive != null)
                            ((MSTSLocomotive)LeadLocomotive).SetEmergency(true);
                        break;
                    }
                case (TrainControlMode.Undefined):
                    {
                        SwitchToNodeControl(-1);
                        break;
                    }

                // other modes are processed directly
                default:
                    break;
            }

            // reset signal which we've just passed

            if (signalObjectIndex >= 0)
            {
                Signal signalObject = signalRef.Signals[signalObjectIndex];

                //the following is added by JTang, passing a hold signal, will take back control by the system
                if (signalObject.HoldState == SignalHoldState.ManualPass || signalObject.HoldState == SignalHoldState.ManualApproach)
                {
                    signalObject.HoldState = SignalHoldState.None;
                }

                signalObject.ResetSignalEnabled();
            }
        }

        /// <summary>
        /// Perform auto signal mode update
        /// </summary>
        protected void UpdateSignalMode(int signalObjectIndex, int backward, double elapsedClockSeconds)
        {
            // in AUTO mode, use forward route only
            // if moving backward, check if slipped passed signal, if so, re-enable signal

            if (backward > BackwardThreshold)
            {
                if (NextSignalObject[0] != null && NextSignalObject[0].EnabledTrain != routedForward)
                {
                    if (NextSignalObject[0].EnabledTrain != null)
                    {
                        NextSignalObject[0].ResetSignal(true);
                    }
                    signalObjectIndex = NextSignalObject[0].Index;
                }
            }

            // if signal passed, send request to clear to next signal
            // if next signal not enabled, also send request (can happen after choosing passing path)
            if (signalObjectIndex >= 0)
            {
                Signal signal = signalRef.Signals[signalObjectIndex];
                int nextSignalIndex = signal.Signalfound[(int)SignalFunction.Normal];
                if (nextSignalIndex >= 0)
                {
                    Signal nextSignal = signalRef.Signals[nextSignalIndex];
                    nextSignal.RequestClearSignal(ValidRoute[0], routedForward, 0, false, null);
                }
            }
            // if next signal not enabled or enabled for other train, also send request (can happen after choosing passing path or after detach)
            else if (NextSignalObject[0] != null && (!NextSignalObject[0].Enabled || NextSignalObject[0].EnabledTrain != routedForward))
            {
                NextSignalObject[0].RequestClearSignal(ValidRoute[0], routedForward, 0, false, null);
            }
            // check if waiting for signal
            else if (SpeedMpS < Math.Abs(0.1) && NextSignalObject[0] != null &&
                     GetNextSignalAspect(0) == SignalAspectState.Stop && CheckTrainWaitingForSignal(NextSignalObject[0], Direction.Forward))
            {
                // perform special actions on stopped at signal for specific train classes
                bool claimAllowed  = ActionsForSignalStop();

                // cannot claim on deadlock to prevent further deadlocks
                if (CheckDeadlockWait(NextSignalObject[0]))
                    claimAllowed = false;

                // cannot claim while in waitstate as this would lock path for other train
                if (isInWaitState()) 
                    claimAllowed = false;

                // cannot claim on hold signal
                if (HoldingSignals.Contains(NextSignalObject[0].Index)) 
                    claimAllowed = false;

                // process claim if allowed
                if (claimAllowed)
                {
                    if (NextSignalObject[0].SignalRoute.CheckStoppedTrains()) // do not claim when train ahead is stationary or in Manual mode
                    {
                        ActualWaitTimeS = standardWaitTimeS;  // allow immediate claim if other train moves
                        ClaimState = false;
                    }
                    else
                    {
                        ActualWaitTimeS += elapsedClockSeconds;
                        if (ActualWaitTimeS > standardWaitTimeS)
                        {
                            ClaimState = true;
                        }
                    }
                }
                else
                {
                    ActualWaitTimeS = 0.0;
                    ClaimState = false;

                    // Reset any invalid claims (occurs on WAIT commands, reason still to be checked!) - not unclaiming causes deadlocks
                    for (int i = PresentPosition[Direction.Forward].RouteListIndex; i <= ValidRoute[0].Count - 1; i++)
                    {
                        ValidRoute[0][i].TrackCircuitSection.CircuitState.TrainClaimed.Remove(routedForward);
                    }
                }
            }
            else
            {
                ActualWaitTimeS = 0.0;
                ClaimState = false;
            }
        }

        /// <summary>
        /// Test if call on allowed
        /// </summary>
        internal virtual bool TestCallOn(Signal signal, bool allowOnNonePlatform, TrackCircuitPartialPathRoute route)
        {
            bool intoPlatform = false;

            foreach (TrackCircuitRouteElement routeElement in signal.SignalRoute)
            {
                // check if route leads into platform
                if (routeElement.TrackCircuitSection.PlatformIndices.Count > 0)
                {
                    intoPlatform = true;
                }
            }

            //if track does not lead into platform, return state as defined in call
            // else never allow if track leads into platform
            return !intoPlatform && allowOnNonePlatform;
        }

        /// <summary>
        /// Check if train is waiting for signal
        /// </summary>
        private protected bool CheckTrainWaitingForSignal(Signal signal, Direction direction)
        {
            TrainRouted routed = direction == 0 ? routedForward : routedBackward;
            int trainRouteIndex = PresentPosition[direction].RouteListIndex;
            int signalRouteIndex = ValidRoute[(int)direction].GetRouteIndex(signal.TrackCircuitIndex, trainRouteIndex);

            // signal section is not in train route, so train can't be waiting for signal
            if (signalRouteIndex < 0)
            {
                return false;
            }

            // check if any other trains in section ahead of this train

            TrackCircuitSection section = ValidRoute[0][trainRouteIndex].TrackCircuitSection;

            Dictionary<Train, float> trainAhead = section.TestTrainAhead(this, PresentPosition[Direction.Forward].Offset, PresentPosition[Direction.Forward].Direction);

            if (trainAhead.Count > 0)
            {
                KeyValuePair<Train, float> foundTrain = trainAhead.ElementAt(0);
                // check if train is closer as signal
                if (!DistanceToSignal.HasValue || foundTrain.Value < DistanceToSignal)
                {
                    return false;
                }
            }

            // check if any other sections inbetween train and signal
            if (trainRouteIndex != signalRouteIndex)
            {
                for (int i = trainRouteIndex + 1; i <= signalRouteIndex; i++)
                {
                    TrackCircuitSection nextSection = ValidRoute[0][i].TrackCircuitSection;

                    if (nextSection.CircuitState.Occupied())  // train is ahead - it's not our signal //
                    {
                        return false;
                    }
                    else if (!nextSection.IsAvailable(this)) // is section really available to us? //
                    // something is wrong - section upto signal is not available - give warning and switch to node control
                    // also reset signal if it was enabled to us
                    {
                        Trace.TraceWarning($"Train {Name} ({Number}) in Signal control but route to signal not cleared - switching to Node control");

                        if (signal.EnabledTrain == routed)
                        {
                            signal.ResetSignal(true);
                        }
                        SwitchToNodeControl(section.Index);

                        return false;
                    }
                }
            }
            
            if (signal.EnabledTrain == null) // we are waiting, but is signal clearance requested ?
            {
                signal.RequestClearSignal(ValidRoute[0], routed, 0, false, null);
            }            
            else if (signal.EnabledTrain != routed) // we are waiting, but is it really our signal ?
            {
                // something is wrong - we are waiting, but it is not our signal - give warning, reset signal and clear route
                Trace.TraceWarning($"Train {Name} ({Number}) waiting for signal which is enabled to train {signal.EnabledTrain.Train.Number}");

                // stop other train - switch other train to node control
                Train otherTrain = signal.EnabledTrain.Train;
                otherTrain.LastReservedSection[0] = -1;
                if (Math.Abs(otherTrain.SpeedMpS) > 0)
                {
                    otherTrain.ForcedStop(Simulator.Catalog.GetString("Stopped due to errors in route setting"), Name, Number);
                }
                otherTrain.SwitchToNodeControl(-1);

                // reset signal and clear route

                signal.ResetSignal(false);
                signal.RequestClearSignal(ValidRoute[0], routed, 0, false, null);
                return false;   // do not yet set to waiting, signal might clear //
            }

            // signal is in holding list - so not really waiting - but remove from list if held for station stop
            if (signal.HoldState == SignalHoldState.ManualLock)
            {
                return false;
            }
            else if (signal.HoldState == SignalHoldState.StationStop && HoldingSignals.Contains(signal.Index))
            {
                if (StationStops != null && StationStops.Count > 0 && StationStops[0].ExitSignal != signal.Index) // not present station stop
                {
                    HoldingSignals.Remove(signal.Index);
                    signal.HoldState = SignalHoldState.None;
                    return false;
                }
            }

            return true;  // it is our signal and we are waiting //
        }

        /// <summary>
        /// Perform auto node mode update
        /// </summary>
        protected virtual void UpdateNodeMode()
        {

            // update distance to end of authority
            int lastRouteIndex = ValidRoute[0].GetRouteIndex(LastReservedSection[0], PresentPosition[Direction.Forward].RouteListIndex);

            TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
            DistanceToEndNodeAuthorityM[0] = section.Length - PresentPosition[Direction.Forward].Offset;

            for (int i = PresentPosition[Direction.Forward].RouteListIndex + 1; i <= lastRouteIndex; i++)
            {
                section = ValidRoute[0][i].TrackCircuitSection;
                DistanceToEndNodeAuthorityM[0] += section.Length;
            }

            // run out of authority : train is out of control

            // TODO : check end of (sub)path
            //        set variable accordingly
            //
            //            if (DistanceToEndNodeAuthorityM < 0.0f)
            //            {
            //                SetTrainOutOfControl(OUTOFCONTROL.OUT_OF_AUTHORITY);
            //                return;
            //            }

            // look maxTimeS or minCheckDistance ahead
            float maxDistance = Math.Max(AllowedMaxSpeedMpS * maxTimeS, minCheckDistanceM);
            if (EndAuthorityTypes[0] == EndAuthorityType.MaxDistance && DistanceToEndNodeAuthorityM[0] > maxDistance)
            {
                return;   // no update required //
            }
            // perform node update - forward only

            signalRef.RequestClearNode(routedForward, ValidRoute[0]);
        }

        /// <summary>
        /// Switches switch after dispatcher window command, when in auto mode
        /// </summary>
        internal void ProcessRequestAutoSetSwitch(int requiredSwitchIndex)
        {
            TrackCircuitSection reqSwitch = TrackCircuitSection.TrackCircuitList[requiredSwitchIndex];
            if (reqSwitch.CircuitState.TrainReserved != null && reqSwitch.CircuitState.TrainReserved.Train == this)
            {
                // store required position
                int reqSwitchPosition = reqSwitch.JunctionSetManual;
                ClearReservedSections();
                Reinitialize();
                reqSwitch.JunctionSetManual = reqSwitchPosition;
            }
        }

        /// <summary>
        /// Update section occupy states for manual mode
        /// Note : manual mode has no distance actions so sections must be cleared immediately
        /// </summary>
        private void UpdateSectionStateManual()
        {
            // occupation is set in forward mode only
            // build route from rear to front - before reset occupy so correct switch alignment is used
            manualTrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset, PresentPosition[Direction.Backward].Direction, Length, false, true, false);

            // save present occupation list
            List<TrackCircuitSection> clearedSections = new List<TrackCircuitSection>();
            for (int i = OccupiedTrack.Count - 1; i >= 0; i--)
            {
                clearedSections.Add(OccupiedTrack[i]);
            }

            // set track occupied
            OccupiedTrack.Clear();

            foreach (TrackCircuitRouteElement routeElement in manualTrainRoute)
            {
                TrackCircuitSection section = routeElement.TrackCircuitSection;

                if (clearedSections.Contains(section))
                {
                    section.ResetOccupied(this); // reset occupation if it was occupied
                    clearedSections.Remove(section);  // remove from cleared list
                }

                section.Reserve(routedForward, manualTrainRoute);  // reserve first to reset switch alignments
                section.SetOccupied(routedForward);
            }

            foreach (TrackCircuitSection clearedSection in clearedSections)
            {
                clearedSection.ClearOccupied(this, true); // sections really cleared
            }
        }

        /// <summary>
        /// Update Manual Mode
        /// </summary>
        private void UpdateManualMode(int signalObjectIndex)
        {
            // check present forward
            TrackCircuitPartialPathRoute newRouteF = CheckManualPath(0, PresentPosition[Direction.Forward], ValidRoute[0], true, ref EndAuthorityTypes[0],
                ref DistanceToEndNodeAuthorityM[0]);
            ValidRoute[0] = newRouteF;
            int routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            PresentPosition[Direction.Forward].RouteListIndex = routeIndex;

            // check present reverse
            // reverse present rear position direction to build correct path backwards
            TrackCircuitPosition tempRear = new TrackCircuitPosition(PresentPosition[Direction.Backward], true);
            TrackCircuitPartialPathRoute newRouteR = CheckManualPath(1, tempRear, ValidRoute[1], true, ref EndAuthorityTypes[1], ref DistanceToEndNodeAuthorityM[1]);
            ValidRoute[1] = newRouteR;

            // select valid route
            if (MUDirection == MidpointDirection.Forward)
            {
                // use position from other end of section
                float reverseOffset = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex].Length - PresentPosition[Direction.Backward].Offset;
                CheckSpeedLimitManual(ValidRoute[1], manualTrainRoute, reverseOffset, PresentPosition[Direction.Backward].Offset, signalObjectIndex, 0);
            }
            else
            {
                TrackCircuitPartialPathRoute tempRoute = new TrackCircuitPartialPathRoute(); // reversed trainRoute
                for (int i = manualTrainRoute.Count - 1; i >= 0; i--)
                {
                    TrackCircuitRouteElement routeElement = manualTrainRoute[i];
                    routeElement.Direction = routeElement.Direction.Next();
                    tempRoute.Add(routeElement);
                }
                float reverseOffset = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex].Length - PresentPosition[Direction.Forward].Offset;
                CheckSpeedLimitManual(ValidRoute[0], tempRoute, PresentPosition[Direction.Forward].Offset, reverseOffset, signalObjectIndex, 1);
            }

            // reset signal
            if (signalObjectIndex >= 0)
            {
                Signal signal = signalRef.Signals[signalObjectIndex];
                signal.OverridePermission = SignalPermission.Denied;
                //the following is added by JTang, passing a hold signal, will take back control by the system
                if (signal.HoldState == SignalHoldState.ManualPass ||
                    signal.HoldState == SignalHoldState.ManualApproach) signal.HoldState = SignalHoldState.None;

                signal.ResetSignalEnabled();
            }

            // get next signal

            // forward
            NextSignalObject[0] = null;
            for (int i = 0; i < ValidRoute[0].Count; i++)
            {
                TrackCircuitRouteElement routeElement = ValidRoute[0][i];
                TrackCircuitSection section = routeElement.TrackCircuitSection;
                NextSignalObject[0] = section.EndSignals[routeElement.Direction];
                break;
            }

            // backward
            NextSignalObject[1] = null;
            for (int i = 0; i < ValidRoute[1].Count; i++)
            {
                TrackCircuitRouteElement routeElement = ValidRoute[1][i];
                TrackCircuitSection section = routeElement.TrackCircuitSection;
                NextSignalObject[1] = section.EndSignals[routeElement.Direction];
                break;
            }

            // clear all build up distance actions
            requiredActions.RemovePendingAIActionItems(true);
        }


        /// <summary>
        /// Check Manual Path
        /// <\summary>
        private TrackCircuitPartialPathRoute CheckManualPath(int direction, TrackCircuitPosition requiredPosition, TrackCircuitPartialPathRoute requiredRoute, bool forward,
            ref EndAuthorityType endAuthority, ref float endAuthorityDistanceM)
        {
            TrainRouted routedTrain = direction == 0 ? routedForward : routedBackward;

            // create new route or set to existing route
            TrackCircuitPartialPathRoute newRoute = requiredRoute ?? new TrackCircuitPartialPathRoute();

            TrackCircuitRouteElement thisElement = null;
            TrackCircuitSection thisSection = null;
            TrackDirection reqDirection = TrackDirection.Ahead;
            float offsetM = 0.0f;
            float totalLengthM = 0.0f;


            // check if train on valid position in route

            int thisRouteIndex = newRoute.GetRouteIndex(requiredPosition.TrackCircuitSectionIndex, 0);
            if (thisRouteIndex < 0)    // no valid point in route
            {
                // check if run out of route on misaligned switch

                if (newRoute.Count > 0)
                {
                    // get last section, and get next expected section
                    TrackCircuitSection lastSection = newRoute[newRoute.Count - 1].TrackCircuitSection;
                    int nextSectionIndex = lastSection.ActivePins[newRoute[newRoute.Count - 1].Direction, Location.NearEnd].Link;

                    if (nextSectionIndex >= 0)
                    {
                        TrackCircuitSection nextSection = TrackCircuitSection.TrackCircuitList[nextSectionIndex];

                        // is next expected section misaligned switch and is present section trailing end of this switch
                        if (nextSectionIndex == MisalignedSwitch[direction, 0] && lastSection.Index == MisalignedSwitch[direction, 1] &&
                            nextSection.ActivePins[TrackDirection.Ahead, Location.NearEnd].Link == requiredPosition.TrackCircuitSectionIndex)
                        {

                            // misaligned switch

                            // reset indication
                            MisalignedSwitch[direction, 0] = -1;
                            MisalignedSwitch[direction, 1] = -1;

                            // set to out of control
                            SetTrainOutOfControl(OutOfControlReason.MisalignedSwitch);

                            // recalculate track position
                            UpdateTrainPosition();

                            // rebuild this list
                            UpdateSectionStateManual();

                            // exit

                            return (newRoute);
                        }
                    }
                }


                if (requiredRoute != null && requiredRoute.Count > 0)  // if route defined, then breakdown route
                {
                    signalRef.BreakDownRouteList(requiredRoute, 0, routedTrain);
                    requiredRoute.Clear();
                }


                // build new route

                MisalignedSwitch[direction, 0] = -1;
                MisalignedSwitch[direction, 1] = -1;

                List<int> tempSections = new List<int>();
                tempSections = SignalEnvironment.ScanRoute(this, requiredPosition.TrackCircuitSectionIndex, requiredPosition.Offset,
                        requiredPosition.Direction, forward, minCheckDistanceManualM, true, false, true, false, true, false, false, false, false, IsFreight);

                if (tempSections.Count > 0)
                {

                    // create subpath route

                    int prevSection = -2;    // preset to invalid

                    foreach (int sectionIndex in tempSections)
                    {
                        TrackDirection sectionDirection = sectionIndex > 0 ? TrackDirection.Ahead : TrackDirection.Reverse;
                        thisElement = new TrackCircuitRouteElement(TrackCircuitSection.TrackCircuitList[Math.Abs(sectionIndex)], sectionDirection, prevSection);
                        newRoute.Add(thisElement);
                        prevSection = Math.Abs(sectionIndex);
                    }
                }
            }
            // remove any sections before present position - train has passed over these sections
            else if (thisRouteIndex > 0)
            {
                for (int iindex = thisRouteIndex - 1; iindex >= 0; iindex--)
                {
                    newRoute.RemoveAt(iindex);
                }
            }

            // check if route ends at signal, determine length

            totalLengthM = 0;
            thisSection = TrackCircuitSection.TrackCircuitList[requiredPosition.TrackCircuitSectionIndex];
            offsetM = direction == 0 ? requiredPosition.Offset : thisSection.Length - requiredPosition.Offset;
            bool endWithSignal = false;    // ends with signal at STOP
            bool hasEndSignal = false;     // ends with cleared signal
            int sectionWithSignalIndex = 0;

            Signal previousSignal = null;

            for (int iindex = 0; iindex < newRoute.Count && !endWithSignal; iindex++)
            {
                thisElement = newRoute[iindex];

                thisSection = thisElement.TrackCircuitSection;
                totalLengthM += (thisSection.Length - offsetM);
                offsetM = 0.0f; // reset offset for further sections

                reqDirection = thisElement.Direction;
                if (thisSection.EndSignals[reqDirection] != null)
                {
                    var endSignal = thisSection.EndSignals[reqDirection];
                    SignalAspectState thisAspect = thisSection.EndSignals[reqDirection].SignalLR(SignalFunction.Normal);
                    hasEndSignal = true;
                    if (previousSignal != null)
                        previousSignal.Signalfound[(int)SignalFunction.Normal] = endSignal.Index;
                    previousSignal = thisSection.EndSignals[reqDirection];

                    if (thisAspect == SignalAspectState.Stop && endSignal.OverridePermission != SignalPermission.Granted)
                    {
                        endWithSignal = true;
                        sectionWithSignalIndex = iindex;
                    }
                    else if (endSignal.EnabledTrain == null && endSignal.FixedRoute) // signal cleared by default - make sure train is set
                    {
                        endSignal.EnabledTrain = routedTrain;
                        endSignal.SetDefaultRoute();
                    }
                }
            }

            // check if signal is in last section
            // if not, probably moved forward beyond a signal, so remove all beyond first signal

            if (endWithSignal && sectionWithSignalIndex < newRoute.Count - 1)
            {
                for (int iindex = newRoute.Count - 1; iindex >= sectionWithSignalIndex + 1; iindex--)
                {
                    thisSection = newRoute[iindex].TrackCircuitSection;
                    thisSection.RemoveTrain(this, true);
                    newRoute.RemoveAt(iindex);
                }
            }

            // if route does not end with signal and is too short, extend

            if (!endWithSignal && totalLengthM < minCheckDistanceManualM)
            {

                float extendedDistanceM = minCheckDistanceManualM - totalLengthM;
                TrackCircuitRouteElement lastElement = newRoute[newRoute.Count - 1];

                TrackCircuitSection lastSection = lastElement.TrackCircuitSection;

                int nextSectionIndex = lastSection.Pins[lastElement.OutPin[Location.NearEnd], (Location)lastElement.OutPin[Location.FarEnd]].Link;
                TrackDirection nextSectionDirection = lastSection.Pins[lastElement.OutPin[Location.NearEnd], (Location)lastElement.OutPin[Location.FarEnd]].Direction;

                // check if last item is non-aligned switch

                MisalignedSwitch[direction, 0] = -1;
                MisalignedSwitch[direction, 1] = -1;

                TrackCircuitSection nextSection = nextSectionIndex >= 0 ? TrackCircuitSection.TrackCircuitList[nextSectionIndex] : null;
                if (nextSection != null && nextSection.CircuitType == TrackCircuitType.Junction)
                {
                    if (nextSection.Pins[TrackDirection.Ahead, Location.NearEnd].Link != lastSection.Index &&
                        nextSection.Pins[TrackDirection.Reverse, (Location)nextSection.JunctionLastRoute].Link != lastSection.Index)
                    {
                        MisalignedSwitch[direction, 0] = nextSection.Index;
                        MisalignedSwitch[direction, 1] = lastSection.Index;
                    }
                }

                List<int> tempSections = new List<int>();

                if (nextSectionIndex >= 0 && MisalignedSwitch[direction, 0] < 0)
                {
                    bool reqAutoAlign = hasEndSignal; // auto-align switchs if route is extended from signal

                    tempSections = SignalEnvironment.ScanRoute(this, nextSectionIndex, 0,
                            nextSectionDirection, forward, extendedDistanceM, true, reqAutoAlign,
                            true, false, true, false, false, false, false, IsFreight);
                }

                if (tempSections.Count > 0)
                {
                    // add new sections

                    int prevSection = lastElement.TrackCircuitSection.Index;

                    foreach (int sectionIndex in tempSections)
                    {
                        thisElement = new TrackCircuitRouteElement(TrackCircuitSection.TrackCircuitList[Math.Abs(sectionIndex)],
                            sectionIndex > 0 ? TrackDirection.Ahead : TrackDirection.Reverse, prevSection);
                        newRoute.Add(thisElement);
                        prevSection = Math.Abs(sectionIndex);
                    }
                }
            }

            // if route is too long, remove sections at end

            else if (totalLengthM > minCheckDistanceManualM)
            {
                float remainingLengthM = totalLengthM - newRoute[0].TrackCircuitSection.Length; // do not count first section
                bool lengthExceeded = remainingLengthM > minCheckDistanceManualM;

                for (int iindex = newRoute.Count - 1; iindex > 1 && lengthExceeded; iindex--)
                {
                    thisElement = newRoute[iindex];
                    thisSection = thisElement.TrackCircuitSection;

                    if ((remainingLengthM - thisSection.Length) > minCheckDistanceManualM)
                    {
                        remainingLengthM -= thisSection.Length;
                        newRoute.RemoveAt(iindex);
                    }
                    else
                    {
                        lengthExceeded = false;
                    }
                }
            }

            // route created to signal or max length, now check availability
            // check if other train in first section

            if (newRoute.Count > 0)
            {
                thisElement = newRoute[0];
                thisSection = thisElement.TrackCircuitSection;
                reqDirection = forward ? thisElement.Direction : (thisElement.Direction).Next();
                offsetM = direction == 0 ? requiredPosition.Offset : thisSection.Length - requiredPosition.Offset;

                Dictionary<Train, float> firstTrainInfo = thisSection.TestTrainAhead(this, offsetM, reqDirection);
                if (firstTrainInfo.Count > 0)
                {
                    foreach (KeyValuePair<Train, float> thisTrainAhead in firstTrainInfo)  // there is only one value
                    {
                        endAuthority = EndAuthorityType.TrainAhead;
                        endAuthorityDistanceM = thisTrainAhead.Value;
                        if (!thisSection.CircuitState.OccupiedByThisTrain(this))
                            thisSection.PreReserve(routedTrain);
                    }
                    RemoveSignalEnablings(0, newRoute);
                }

                // check route availability
                // reserve sections which are available

                else
                {
                    int lastValidSectionIndex = 0;
                    bool isAvailable = true;
                    totalLengthM = 0;

                    for (int iindex = 0; iindex < newRoute.Count && isAvailable; iindex++)
                    {
                        thisSection = newRoute[iindex].TrackCircuitSection;

                        if (isAvailable)
                        {
                            if (thisSection.IsAvailable(this))
                            {
                                lastValidSectionIndex = iindex;
                                totalLengthM += (thisSection.Length - offsetM);
                                offsetM = 0;
                                thisSection.Reserve(routedTrain, newRoute);
                            }
                            else
                            {
                                isAvailable = false;
                            }
                        }
                    }

                    // set default authority to max distance
                    endAuthority = EndAuthorityType.MaxDistance;
                    endAuthorityDistanceM = totalLengthM;

                    // if last section ends with signal, set authority to signal
                    thisElement = newRoute[lastValidSectionIndex];
                    thisSection = thisElement.TrackCircuitSection;
                    reqDirection = forward ? thisElement.Direction : thisElement.Direction.Next();
                    // last section ends with signal
                    if (thisSection.EndSignals[reqDirection] != null)
                    {
                        endAuthority = EndAuthorityType.Signal;
                        endAuthorityDistanceM = totalLengthM;
                    }

                    // sections not clear - check if end has signal

                    else
                    {

                        TrackCircuitSection nextSection = null;
                        TrackCircuitRouteElement nextElement = null;

                        if (lastValidSectionIndex < newRoute.Count - 1)
                        {
                            nextElement = newRoute[lastValidSectionIndex + 1];
                            nextSection = nextElement.TrackCircuitSection;
                        }

                        // check for end authority if not ended with signal
                        // last section is end of track
                        if (thisSection.CircuitType == TrackCircuitType.EndOfTrack)
                        {
                            endAuthority = EndAuthorityType.EndOfTrack;
                            endAuthorityDistanceM = totalLengthM;
                        }

                        // first non-available section is switch or crossover
                        else if (nextSection != null && (nextSection.CircuitType == TrackCircuitType.Junction ||
                                     nextSection.CircuitType == TrackCircuitType.Crossover))
                        {
                            endAuthority = EndAuthorityType.ReservedSwitch;
                            endAuthorityDistanceM = totalLengthM;
                        }

                        // set authority is end of path unless train ahead
                        else
                        {
                            endAuthority = EndAuthorityType.EndOfPath;
                            endAuthorityDistanceM = totalLengthM;

                            // check if train ahead not moving in opposite direction, in first non-available section

                            if (nextSection != null)
                            {
                                int oppositeDirection = forward ? (nextElement.Direction == 0 ? 1 : 0) : (nextElement.Direction == 0 ? 0 : 1);
                                reqDirection = forward ? (TrackDirection)nextElement.Direction : ((TrackDirection)nextElement.Direction).Next();

                                bool oppositeTrain = nextSection.CircuitState.Occupied(oppositeDirection, false);

                                if (!oppositeTrain)
                                {
                                    Dictionary<Train, float> nextTrainInfo = nextSection.TestTrainAhead(this, 0.0f, reqDirection);
                                    if (nextTrainInfo.Count > 0)
                                    {
                                        foreach (KeyValuePair<Train, float> thisTrainAhead in nextTrainInfo)  // there is only one value
                                        {
                                            endAuthority = EndAuthorityType.TrainAhead;
                                            endAuthorityDistanceM = thisTrainAhead.Value + totalLengthM;
                                            lastValidSectionIndex++;
                                            nextSection.PreReserve(routedTrain);
                                        }
                                        RemoveSignalEnablings(lastValidSectionIndex, newRoute);
                                    }
                                }
                            }
                        }
                    }

                    // remove invalid sections from route
                    if (lastValidSectionIndex < newRoute.Count - 1)
                    {
                        for (int iindex = newRoute.Count - 1; iindex > lastValidSectionIndex; iindex--)
                        {
                            newRoute.RemoveAt(iindex);
                        }
                    }
                }
            }

            // no valid route could be found
            else
            {
                endAuthority = EndAuthorityType.NoPathReserved;
                endAuthorityDistanceM = 0.0f;
            }

            return (newRoute);
        }

        //================================================================================================//
        /// <summary>
        /// Remove signal enablings for subsequent route sections.
        /// They were set before testing whether there is an occupying train
        /// </summary>

        private void RemoveSignalEnablings(int firstSection, TrackCircuitPartialPathRoute newRoute)
        {
            for (int iSection = firstSection; iSection <= newRoute.Count - 1; iSection++)
            {
                var thisRouteElement = newRoute[iSection];
                var thisRouteSection = thisRouteElement.TrackCircuitSection;
                TrackDirection thisReqDirection = thisRouteElement.Direction;
                if (thisRouteSection.EndSignals[thisReqDirection] != null)
                {
                    var endSignal = thisRouteSection.EndSignals[thisReqDirection];
                    if (endSignal.EnabledTrain != null && endSignal.EnabledTrain.Train == this) endSignal.EnabledTrain = null;
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Restore Manual Mode
        /// </summary>

        public void RestoreManualMode()
        {
            // get next signal

            // forward
            NextSignalObject[0] = null;
            for (int iindex = 0; iindex < ValidRoute[0].Count && NextSignalObject[0] == null; iindex++)
            {
                TrackCircuitRouteElement thisElement = ValidRoute[0][iindex];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                NextSignalObject[0] = thisSection.EndSignals[thisElement.Direction];
            }

            // backward
            NextSignalObject[1] = null;
            for (int iindex = 0; iindex < ValidRoute[1].Count && NextSignalObject[1] == null; iindex++)
            {
                TrackCircuitRouteElement thisElement = ValidRoute[1][iindex];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                NextSignalObject[1] = thisSection.EndSignals[thisElement.Direction];
            }
        }


        //================================================================================================//
        //
        // Request signal permission in manual mode
        //

        public void RequestManualSignalPermission(ref TrackCircuitPartialPathRoute selectedRoute, int routeIndex)
        {

            // check if route ends with signal at danger

            TrackCircuitRouteElement lastElement = selectedRoute[selectedRoute.Count - 1];
            TrackCircuitSection lastSection = lastElement.TrackCircuitSection;

            // no signal in required direction at end of path

            if (lastSection.EndSignals[lastElement.Direction] == null)
            {
                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("No signal in train's path"));
                return;
            }

            var requestedSignal = lastSection.EndSignals[lastElement.Direction];
            if (requestedSignal.EnabledTrain != null && requestedSignal.EnabledTrain.Train != this)
            {
                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Next signal already allocated to other train"));
                simulator.SoundNotify = TrainEvent.PermissionDenied;
                return;
            }

            requestedSignal.EnabledTrain = routeIndex == 0 ? routedForward : routedBackward;
            requestedSignal.SignalRoute.Clear();
            requestedSignal.HoldState = SignalHoldState.None;
            requestedSignal.OverridePermission = SignalPermission.Requested;

            // get route from next signal - extend to next signal or maximum length

            // first, get present length (except first section)

            float totalLengthM = 0;
            for (int iindex = 1; iindex < selectedRoute.Count; iindex++)
            {
                TrackCircuitSection thisSection = selectedRoute[iindex].TrackCircuitSection;
                totalLengthM += thisSection.Length;
            }

            float remainingLengthM =
                Math.Min(minCheckDistanceManualM, Math.Max((minCheckDistanceManualM - totalLengthM), (minCheckDistanceManualM * 0.25f)));

            // get section behind signal

            int nextSectionIndex = lastSection.Pins[lastElement.OutPin[Location.NearEnd], (Location)lastElement.OutPin[Location.FarEnd]].Link;
            TrackDirection nextSectionDirection = lastSection.Pins[lastElement.OutPin[Location.NearEnd], (Location)lastElement.OutPin[Location.FarEnd]].Direction;

            bool requestValid = false;

            // get route from signal - set remaining length or upto next signal

            if (nextSectionIndex > 0)
            {
                List<int> tempSections = SignalEnvironment.ScanRoute(this, nextSectionIndex, 0,
                    nextSectionDirection, true, remainingLengthM, true, true,
                    true, false, true, false, false, false, false, IsFreight);

                // set as signal route

                if (tempSections.Count > 0)
                {
                    int prevSection = -1;

                    foreach (int sectionIndex in tempSections)
                    {
                        TrackCircuitRouteElement thisElement = new TrackCircuitRouteElement(TrackCircuitSection.TrackCircuitList[Math.Abs(sectionIndex)],
                                sectionIndex > 0 ? TrackDirection.Ahead : TrackDirection.Reverse, prevSection);
                        requestedSignal.SignalRoute.Add(thisElement);
                        selectedRoute.Add(thisElement);
                        prevSection = Math.Abs(sectionIndex);
                    }

                    requestedSignal.CheckRouteState(false, requestedSignal.SignalRoute, routedForward);
                    requestValid = true;
                }

                if (!requestValid)
                {
                    if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                        simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Request to clear signal cannot be processed"));
                    simulator.SoundNotify = TrainEvent.PermissionDenied;
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Process request to set switch in manual mode
        /// Request may contain direction or actual node
        /// </summary>
        public bool ProcessRequestManualSetSwitch(Direction direction)
        {
            // find first switch in required direction

            TrackCircuitSection reqSwitch = null;
            int routeDirectionIndex = (int)direction;
            bool switchSet = false;

            for (int iindex = 0; iindex < ValidRoute[routeDirectionIndex].Count && reqSwitch == null; iindex++)
            {
                TrackCircuitSection thisSection = ValidRoute[routeDirectionIndex][iindex].TrackCircuitSection;
                if (thisSection.CircuitType == TrackCircuitType.Junction)
                {
                    reqSwitch = thisSection;
                }
            }

            if (reqSwitch == null)
            {
                // search beyond last section for switch using default pins (continue through normal sections only)

                TrackCircuitRouteElement thisElement = ValidRoute[routeDirectionIndex][ValidRoute[routeDirectionIndex].Count - 1];
                TrackCircuitSection lastSection = thisElement.TrackCircuitSection;
                TrackDirection curDirection = thisElement.Direction;
                int nextSectionIndex = thisElement.TrackCircuitSection.Index;

                bool validRoute = lastSection.CircuitType == TrackCircuitType.Normal;

                while (reqSwitch == null && validRoute)
                {
                    if (lastSection.CircuitType == TrackCircuitType.Crossover)
                    {
                        TrackDirection outPinIndex = curDirection.Next();
                        if (lastSection.Pins[curDirection, Location.NearEnd].Link == nextSectionIndex)
                        {
                            nextSectionIndex = lastSection.Pins[outPinIndex, Location.NearEnd].Link;
                            curDirection = lastSection.Pins[outPinIndex, Location.NearEnd].Direction;
                        }
                        else if (lastSection.Pins[curDirection, Location.FarEnd].Link == nextSectionIndex)
                        {
                            nextSectionIndex = lastSection.Pins[outPinIndex, Location.FarEnd].Link;
                            curDirection = lastSection.Pins[outPinIndex, Location.FarEnd].Direction;
                        }
                    }
                    else
                    {
                        nextSectionIndex = lastSection.Pins[curDirection, Location.NearEnd].Link;
                        curDirection = lastSection.ActivePins[curDirection, Location.NearEnd].Direction;
                        lastSection = TrackCircuitSection.TrackCircuitList[nextSectionIndex];
                    }

                    if (lastSection.CircuitType == TrackCircuitType.Junction)
                    {
                        reqSwitch = lastSection;
                    }
                    else if (lastSection.CircuitType != TrackCircuitType.Normal)
                    {
                        validRoute = false;
                    }
                }
            }

            if (reqSwitch != null)
            {
                // check if switch is clear
                if (!reqSwitch.CircuitState.Occupied() && reqSwitch.CircuitState.TrainReserved == null && reqSwitch.CircuitState.SignalReserved < 0)
                {
                    reqSwitch.JunctionSetManual = reqSwitch.JunctionLastRoute == 0 ? 1 : 0;
                    signalRef.SetSwitch(reqSwitch.OriginalIndex, reqSwitch.JunctionSetManual, reqSwitch);
                    switchSet = true;
                }
                // check if switch reserved by this train - if so, dealign and breakdown route
                else if (reqSwitch.CircuitState.TrainReserved != null && reqSwitch.CircuitState.TrainReserved.Train == this)
                {
                    int reqRouteIndex = reqSwitch.CircuitState.TrainReserved.TrainRouteDirectionIndex;
                    int routeIndex = ValidRoute[reqRouteIndex].GetRouteIndex(reqSwitch.Index, 0);
                    signalRef.BreakDownRouteList(ValidRoute[reqRouteIndex], routeIndex, reqSwitch.CircuitState.TrainReserved);
                    if (routeIndex >= 0 && ValidRoute[reqRouteIndex].Count > routeIndex)
                        ValidRoute[reqRouteIndex].RemoveRange(routeIndex, ValidRoute[reqRouteIndex].Count - routeIndex);
                    else Trace.TraceWarning("Switch index {0} could not be found in ValidRoute[{1}]; routeDirectionIndex = {2}",
                            reqSwitch.Index, reqRouteIndex, routeDirectionIndex);
                    reqSwitch.DeAlignSwitchPins();
                    reqSwitch.JunctionSetManual = reqSwitch.JunctionLastRoute == 0 ? 1 : 0;
                    signalRef.SetSwitch(reqSwitch.OriginalIndex, reqSwitch.JunctionSetManual, reqSwitch);
                    switchSet = true;
                }

                if (switchSet)
                    ProcessManualSwitch(routeDirectionIndex, reqSwitch, direction);
                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Confirm(
                        (direction == Direction.Forward) ? CabControl.SwitchAhead : CabControl.SwitchBehind,
                        CabSetting.On);
            }
            else
            {
                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("No switch found"));
            }

            return (switchSet);
        }

        internal void ProcessRequestManualSetSwitch(int reqSwitchIndex)
        {
            // find switch in route - forward first

            int routeDirectionIndex = -1;
            bool switchFound = false;
            Direction direction = Direction.Forward;

            for (int iindex = 0; iindex < ValidRoute[0].Count - 1 && !switchFound; iindex++)
            {
                if (ValidRoute[0][iindex].TrackCircuitSection.Index == reqSwitchIndex)
                {
                    routeDirectionIndex = 0;
                    direction = Direction.Forward;
                    switchFound = true;
                }
            }

            for (int iindex = 0; iindex < ValidRoute[1].Count - 1 && !switchFound; iindex++)
            {
                if (ValidRoute[1][iindex].TrackCircuitSection.Index == reqSwitchIndex)
                {
                    routeDirectionIndex = 1;
                    direction = Direction.Backward;
                    switchFound = true;
                }
            }

            if (switchFound)
            {
                TrackCircuitSection reqSwitch = TrackCircuitSection.TrackCircuitList[reqSwitchIndex];
                ProcessManualSwitch(routeDirectionIndex, reqSwitch, direction);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Process switching of manual switch
        /// </summary>

        public void ProcessManualSwitch(int routeDirectionIndex, TrackCircuitSection switchSection, Direction direction)
        {
            TrainRouted thisRouted = direction == Direction.Backward ? routedForward : routedBackward; //TODO 20201109 double check why using the forward route for backward direction
            TrackCircuitPartialPathRoute selectedRoute = ValidRoute[routeDirectionIndex];

            // store required position
            int reqSwitchPosition = switchSection.JunctionSetManual;

            // find index of section in present route
            int junctionIndex = selectedRoute.GetRouteIndex(switchSection.Index, 0);

            // check if any signals between train and switch
            List<Signal> signalsFound = new List<Signal>();

            for (int iindex = 0; iindex < junctionIndex; iindex++)
            {
                TrackCircuitRouteElement thisElement = selectedRoute[iindex];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                TrackDirection signalDirection = thisElement.Direction;

                if (thisSection.EndSignals[signalDirection] != null)
                {
                    signalsFound.Add(thisSection.EndSignals[signalDirection]);
                }
            }

            // if any signals found : reset signals

            foreach (Signal thisSignal in signalsFound)
            {
                thisSignal.ResetSignal(false);
            }

            // breakdown and clear route

            signalRef.BreakDownRouteList(selectedRoute, 0, thisRouted);
            selectedRoute.Clear();

            // restore required position (is cleared by route breakdown)
            switchSection.JunctionSetManual = reqSwitchPosition;

            // set switch
            switchSection.DeAlignSwitchPins();
            signalRef.SetSwitch(switchSection.OriginalIndex, switchSection.JunctionSetManual, switchSection);

            // reset indication for misaligned switch
            MisalignedSwitch[routeDirectionIndex, 0] = -1;
            MisalignedSwitch[routeDirectionIndex, 1] = -1;

            // build new route

            int routeIndex = -1;

            if (direction == Direction.Forward)
            {
                selectedRoute = CheckManualPath(0, PresentPosition[Direction.Forward], null, true, ref EndAuthorityTypes[0], ref DistanceToEndNodeAuthorityM[0]);
                routeIndex = 0;

            }
            else
            {
                TrackCircuitPosition tempRear = new TrackCircuitPosition(PresentPosition[Direction.Backward], true);
                selectedRoute = CheckManualPath(1, tempRear, null, true, ref EndAuthorityTypes[1], ref DistanceToEndNodeAuthorityM[1]);
                routeIndex = 1;
            }

            // if route ends at previously cleared signal, request clear signal again

            TrackCircuitRouteElement lastElement = selectedRoute[selectedRoute.Count - 1];
            TrackCircuitSection lastSection = lastElement.TrackCircuitSection;
            TrackDirection lastDirection = lastElement.Direction;

            var lastSignal = lastSection.EndSignals[lastDirection];

            while (lastSignal != null && signalsFound.Contains(lastSignal))
            {
                RequestManualSignalPermission(ref selectedRoute, routeIndex);

                lastElement = selectedRoute[selectedRoute.Count - 1];
                lastSection = lastElement.TrackCircuitSection;
                lastDirection = lastElement.Direction;

                lastSignal = lastSection.EndSignals[lastDirection];
            }

            ValidRoute[routeDirectionIndex] = selectedRoute;
        }

        //================================================================================================//
        /// <summary>
        /// Update speed limit in manual mode
        /// </summary>

        public void CheckSpeedLimitManual(TrackCircuitPartialPathRoute routeBehind, TrackCircuitPartialPathRoute routeUnderTrain, float offsetStart,
            float reverseOffset, int passedSignalIndex, int routeDirection)
        {
            // check backward for last speedlimit in direction of train - raise speed if passed

            TrackCircuitRouteElement thisElement = routeBehind[0];
            List<int> foundSpeedLimit = SignalEnvironment.ScanRoute(this, thisElement.TrackCircuitSection.Index, offsetStart, thisElement.Direction,
                    true, -1, false, true, false, false, false, false, false, false, true, IsFreight);

            if (foundSpeedLimit.Count > 0)
            {
                var speedLimit = signalRef.Signals[Math.Abs(foundSpeedLimit[0])];
                var thisSpeedInfo = speedLimit.SpeedLimit(SignalFunction.Speed);
                float thisSpeedMpS = IsFreight ? thisSpeedInfo.FreightSpeed : thisSpeedInfo.PassengerSpeed;

                if (thisSpeedMpS > 0)
                {
                    if (thisSpeedInfo.LimitedSpeedReduction == 0) allowedMaxSpeedLimitMpS = thisSpeedMpS;
                    else allowedMaxTempSpeedLimitMpS = thisSpeedMpS;
                    if (simulator.TimetableMode) AllowedMaxSpeedMpS = thisSpeedMpS;
                    else AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedLimitMpS, Math.Min(allowedMaxTempSpeedLimitMpS,
                                       allowedMaxSpeedSignalMpS == -1 ? 999 : allowedMaxSpeedSignalMpS));
                }
            }
            // No speed limits behind us, initialize allowedMaxSpeedLimitMpS.
            else if (!simulator.TimetableMode)
            {
                AllowedMaxSpeedMpS = allowedMaxSpeedLimitMpS;
            }

            // check backward for last signal in direction of train - check with list of pending signal speeds
            // search also checks for speedlimit to see which is nearest train

            foundSpeedLimit.Clear();
            foundSpeedLimit = SignalEnvironment.ScanRoute(this, thisElement.TrackCircuitSection.Index, offsetStart, (TrackDirection)thisElement.Direction,
                    true, -1, false, true, false, false, false, false, true, false, true, IsFreight, true);

            if (foundSpeedLimit.Count > 0)
            {
                var thisSignal = signalRef.Signals[Math.Abs(foundSpeedLimit[0])];
                if (thisSignal.IsSignal)
                {
                    // if signal is now just behind train - set speed as signal speed limit, do not reenter in list
                    if (PassedSignalSpeeds.ContainsKey(thisSignal.Index))
                    {
                        allowedMaxSpeedSignalMpS = PassedSignalSpeeds[thisSignal.Index];
                        AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedSignalMpS, AllowedMaxSpeedMpS);
                        LastPassedSignal[routeDirection] = thisSignal.Index;
                    }
                    // if signal is not last passed signal - reset signal speed limit
                    else if (thisSignal.Index != LastPassedSignal[routeDirection])
                    {
                        allowedMaxSpeedSignalMpS = TrainMaxSpeedMpS;
                        LastPassedSignal[routeDirection] = -1;
                    }
                    // set signal limit as speed limit
                    else
                    {
                        AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedSignalMpS, AllowedMaxSpeedMpS);
                    }
                }
                else if (thisSignal.SignalHeads[0].SignalFunction == SignalFunction.Speed)
                {
                    SpeedInfo thisSpeedInfo = thisSignal.SignalSpeed(SignalFunction.Speed);
                    if (thisSpeedInfo != null && thisSpeedInfo.Reset)
                    {
                        allowedMaxSpeedSignalMpS = TrainMaxSpeedMpS;
                        if (simulator.TimetableMode)
                            AllowedMaxSpeedMpS = allowedMaxSpeedLimitMpS;
                        else
                            AllowedMaxSpeedMpS = Math.Min(allowedMaxTempSpeedLimitMpS, allowedMaxSpeedLimitMpS);
                    }
                }
            }

            // check forward along train for speedlimit and signal in direction of train - limit speed if passed
            // loop as there might be more than one

            thisElement = routeUnderTrain[0];
            foundSpeedLimit.Clear();
            float remLength = Length;
            Dictionary<int, float> remainingSignals = new Dictionary<int, float>();

            foundSpeedLimit = SignalEnvironment.ScanRoute(this, thisElement.TrackCircuitSection.Index, reverseOffset, thisElement.Direction,
                    true, remLength, false, true, false, false, false, true, false, true, false, IsFreight);

            bool limitAlongTrain = true;
            while (foundSpeedLimit.Count > 0 && limitAlongTrain)
            {
                var thisObject = signalRef.Signals[Math.Abs(foundSpeedLimit[0])];

                // check if not beyond end of train
                float speedLimitDistance = TrackCircuitSection.GetDistanceBetweenObjects(thisElement.TrackCircuitSection.Index, reverseOffset, thisElement.Direction,
                    thisObject.TrackCircuitIndex, thisObject.TrackCircuitOffset);
                if (speedLimitDistance > Length)
                {
                    limitAlongTrain = false;
                }
                else
                {
                    int nextSectionIndex = thisObject.TrackCircuitIndex;
                    TrackDirection direction = thisObject.TrackCircuitDirection;
                    float objectOffset = thisObject.TrackCircuitOffset;

                    if (thisObject.IsSignal)
                    {
                        nextSectionIndex = thisObject.TrackCircuitNextIndex;
                        direction = thisObject.TrackCircuitNextDirection;
                        objectOffset = 0.0f;

                        if (PassedSignalSpeeds.ContainsKey(thisObject.Index))
                        {
                            allowedMaxSpeedSignalMpS = PassedSignalSpeeds[thisObject.Index];
                            if (simulator.TimetableMode) AllowedMaxSpeedMpS = Math.Min(AllowedMaxSpeedMpS, allowedMaxSpeedSignalMpS);
                            else AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedLimitMpS, Math.Min(allowedMaxTempSpeedLimitMpS, allowedMaxSpeedSignalMpS));

                            if (!remainingSignals.ContainsKey(thisObject.Index))
                                remainingSignals.Add(thisObject.Index, allowedMaxSpeedSignalMpS);
                        }
                    }
                    else
                    {
                        SpeedInfo thisSpeedInfo = thisObject.SpeedLimit(SignalFunction.Speed);
                        float thisSpeedMpS = IsFreight ? thisSpeedInfo.FreightSpeed : thisSpeedInfo.PassengerSpeed;
                        if (thisSpeedMpS > 0)
                        {
                            if (thisSpeedInfo.LimitedSpeedReduction == 0) // standard speedpost
                            {
                                if (simulator.TimetableMode)
                                {
                                    allowedMaxSpeedLimitMpS = Math.Min(allowedMaxSpeedLimitMpS, thisSpeedMpS);
                                    AllowedMaxSpeedMpS = allowedMaxSpeedLimitMpS;
                                }
                                else
                                {
                                    allowedMaxSpeedLimitMpS = Math.Min(allowedMaxSpeedLimitMpS, thisSpeedMpS);
                                    AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedLimitMpS, Math.Min(allowedMaxTempSpeedLimitMpS,
                                       allowedMaxSpeedSignalMpS == -1 ? 999 : allowedMaxSpeedSignalMpS));
                                }
                            }
                            else
                            {
                                allowedMaxTempSpeedLimitMpS = Math.Min(allowedMaxTempSpeedLimitMpS, thisSpeedMpS);
                                AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedLimitMpS, Math.Min(allowedMaxTempSpeedLimitMpS,
                                    allowedMaxSpeedSignalMpS == -1 ? 999 : allowedMaxSpeedSignalMpS));
                            }
                        }
                    }

                    remLength -= (thisObject.TrackCircuitOffset - offsetStart);

                    foundSpeedLimit = SignalEnvironment.ScanRoute(this, nextSectionIndex, objectOffset, direction,
                        true, remLength, false, true, false, false, false, true, false, true, false, IsFreight);
                }
            }

            // set list of remaining signals as new pending list
            PassedSignalSpeeds.Clear();
            foreach (KeyValuePair<int, float> thisPair in remainingSignals)
            {
                if (!PassedSignalSpeeds.ContainsKey(thisPair.Key))
                    PassedSignalSpeeds.Add(thisPair.Key, thisPair.Value);
            }

            // check if signal passed posed a speed limit lower than present limit

            if (passedSignalIndex >= 0)
            {
                var passedSignal = signalRef.Signals[passedSignalIndex];
                var thisSpeedInfo = passedSignal.SignalSpeed(SignalFunction.Normal);

                if (thisSpeedInfo != null)
                {
                    float thisSpeedMpS = IsFreight ? thisSpeedInfo.FreightSpeed : thisSpeedInfo.PassengerSpeed;
                    if (thisSpeedMpS > 0 && !PassedSignalSpeeds.ContainsKey(passedSignal.Index))
                    {
                        allowedMaxSpeedSignalMpS = allowedMaxSpeedSignalMpS > 0 ? Math.Min(allowedMaxSpeedSignalMpS, thisSpeedMpS) : thisSpeedMpS;
                        AllowedMaxSpeedMpS = Math.Min(AllowedMaxSpeedMpS, allowedMaxSpeedSignalMpS);

                        PassedSignalSpeeds.Add(passedSignal.Index, thisSpeedMpS);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update section occupy states fore explorer mode
        /// Note : explorer mode has no distance actions so sections must be cleared immediately
        /// </summary>

        public void UpdateSectionStateExplorer()
        {
            // occupation is set in forward mode only
            // build route from rear to front - before reset occupy so correct switch alignment is used
            manualTrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                            PresentPosition[Direction.Backward].Direction, Length, false, true, false);

            // save present occupation list

            List<TrackCircuitSection> clearedSections = new List<TrackCircuitSection>();
            for (int iindex = OccupiedTrack.Count - 1; iindex >= 0; iindex--)
            {
                clearedSections.Add(OccupiedTrack[iindex]);
            }

            // first check for misaligned switch

            int reqDirection = MUDirection == MidpointDirection.Forward ? 0 : 1;
            foreach (TrackCircuitRouteElement thisElement in manualTrainRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

                // occupying misaligned switch : reset routes and position
                if (thisSection.Index == MisalignedSwitch[reqDirection, 0])
                {
                    // align switch
                    if (!MPManager.NoAutoSwitch()) thisSection.AlignSwitchPins(MisalignedSwitch[reqDirection, 1]);
                    MisalignedSwitch[reqDirection, 0] = -1;
                    MisalignedSwitch[reqDirection, 1] = -1;

                    // recalculate track position
                    UpdateTrainPosition();

                    // rebuild this list
                    UpdateSectionStateExplorer();

                    // exit, as routine has called itself
                    return;
                }
            }

            // if all is well, set tracks to occupied

            OccupiedTrack.Clear();

            foreach (TrackCircuitRouteElement thisElement in manualTrainRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

                if (clearedSections.Contains(thisSection))
                {
                    thisSection.ResetOccupied(this); // reset occupation if it was occupied
                    clearedSections.Remove(thisSection);  // remove from cleared list
                }

                thisSection.Reserve(routedForward, manualTrainRoute);  // reserve first to reset switch alignments
                thisSection.SetOccupied(routedForward);
            }

            foreach (TrackCircuitSection exSection in clearedSections)
            {
                exSection.ClearOccupied(this, true); // sections really cleared
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update Explorer Mode
        /// </summary>

        public void UpdateExplorerMode(int signalObjectIndex)
        {
            if (MPManager.IsMultiPlayer())
            // first unreserve all route positions where train is not present
            {
                if (ValidRoute[0] != null)
                {
                    foreach (var tcRouteElement in ValidRoute[0])
                    {
                        var tcSection = tcRouteElement.TrackCircuitSection;
                        if (tcSection.CheckReserved(routedForward) && !tcSection.CircuitState.OccupationState.ContainsTrain(this))
                        {
                            tcSection.Unreserve();
                            tcSection.UnreserveTrain();
                        }
                    }
                }
                if (ValidRoute[1] != null)
                {
                    foreach (var tcRouteElement in ValidRoute[1])
                    {
                        var tcSection = tcRouteElement.TrackCircuitSection;
                        if (tcSection.CheckReserved(routedBackward) && !tcSection.CircuitState.OccupationState.ContainsTrain(this))
                        {
                            tcSection.Unreserve();
                            tcSection.UnreserveTrain();
                        }
                    }
                }
            }

            // check present forward
            TrackCircuitPartialPathRoute newRouteF = CheckExplorerPath(0, PresentPosition[Direction.Forward], ValidRoute[0], true, ref EndAuthorityTypes[0],
                ref DistanceToEndNodeAuthorityM[0]);
            ValidRoute[0] = newRouteF;
            int routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            PresentPosition[Direction.Forward].RouteListIndex = routeIndex;

            // check present reverse
            // reverse present rear position direction to build correct path backwards
            TrackCircuitPosition tempRear = new TrackCircuitPosition(PresentPosition[Direction.Backward], true);
            TrackCircuitPartialPathRoute newRouteR = CheckExplorerPath(1, tempRear, ValidRoute[1], true, ref EndAuthorityTypes[1], ref DistanceToEndNodeAuthorityM[1]);
            ValidRoute[1] = newRouteR;

            // select valid route

            if (MUDirection == MidpointDirection.Forward)
            {
                // use position from other end of section
                float reverseOffset = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex].Length - PresentPosition[Direction.Backward].Offset;
                CheckSpeedLimitManual(ValidRoute[1], manualTrainRoute, reverseOffset, PresentPosition[Direction.Backward].Offset, signalObjectIndex, 0);
            }
            else
            {
                TrackCircuitPartialPathRoute tempRoute = new TrackCircuitPartialPathRoute(); // reversed trainRoute
                for (int iindex = manualTrainRoute.Count - 1; iindex >= 0; iindex--)
                {
                    TrackCircuitRouteElement thisElement = manualTrainRoute[iindex];
                    thisElement.Direction = thisElement.Direction.Next();
                    tempRoute.Add(thisElement);
                }
                float reverseOffset = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex].Length - PresentPosition[Direction.Forward].Offset;
                CheckSpeedLimitManual(ValidRoute[0], tempRoute, PresentPosition[Direction.Forward].Offset, reverseOffset, signalObjectIndex, 1);
            }

            // reset signal permission

            if (signalObjectIndex >= 0)
            {
                var thisSignal = signalRef.Signals[signalObjectIndex];
                thisSignal.OverridePermission = SignalPermission.Denied;

                thisSignal.ResetSignalEnabled();
            }

            // get next signal

            // forward
            NextSignalObject[0] = null;
            for (int iindex = 0; iindex < ValidRoute[0].Count && NextSignalObject[0] == null; iindex++)
            {
                TrackCircuitRouteElement thisElement = ValidRoute[0][iindex];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                NextSignalObject[0] = thisSection.EndSignals[(TrackDirection)thisElement.Direction];
            }

            // backward
            NextSignalObject[1] = null;
            for (int iindex = 0; iindex < ValidRoute[1].Count && NextSignalObject[1] == null; iindex++)
            {
                TrackCircuitRouteElement thisElement = ValidRoute[1][iindex];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                NextSignalObject[1] = thisSection.EndSignals[(TrackDirection)thisElement.Direction];
            }

            // clear all build up distance actions
            requiredActions.RemovePendingAIActionItems(true);
        }

        //================================================================================================//
        /// <summary>
        /// Check Explorer Path
        /// <\summary>

        private TrackCircuitPartialPathRoute CheckExplorerPath(int direction, TrackCircuitPosition requiredPosition, TrackCircuitPartialPathRoute requiredRoute, bool forward,
            ref EndAuthorityType endAuthority, ref float endAuthorityDistanceM)
        {
            TrainRouted thisRouted = direction == 0 ? routedForward : routedBackward;

            // create new route or set to existing route

            TrackCircuitPartialPathRoute newRoute = null;

            TrackCircuitRouteElement thisElement = null;
            TrackCircuitSection thisSection = null;
            TrackDirection reqDirection = TrackDirection.Ahead;
            float offsetM = 0.0f;
            float totalLengthM = 0.0f;

            if (requiredRoute == null)
            {
                newRoute = new TrackCircuitPartialPathRoute();
            }
            else
            {
                newRoute = requiredRoute;
            }

            // check if train on valid position in route

            int thisRouteIndex = newRoute.GetRouteIndex(requiredPosition.TrackCircuitSectionIndex, 0);
            if (thisRouteIndex < 0)    // no valid point in route
            {
                if (requiredRoute != null && requiredRoute.Count > 0)  // if route defined, then breakdown route
                {
                    signalRef.BreakDownRouteList(requiredRoute, 0, thisRouted);
                    requiredRoute.Clear();
                }

                // build new route

                List<int> tempSections = new List<int>();

                tempSections = SignalEnvironment.ScanRoute(this, requiredPosition.TrackCircuitSectionIndex, requiredPosition.Offset,
                        requiredPosition.Direction, forward, -1, true, false, false, false, true, false, false, false, false, IsFreight);

                if (tempSections.Count > 0)
                {

                    // create subpath route

                    int prevSection = -2;    // preset to invalid

                    foreach (int sectionIndex in tempSections)
                    {
                        thisElement = new TrackCircuitRouteElement(TrackCircuitSection.TrackCircuitList[Math.Abs(sectionIndex)],
                                sectionIndex > 0 ? TrackDirection.Ahead : TrackDirection.Reverse, prevSection);
                        newRoute.Add(thisElement);
                        prevSection = Math.Abs(sectionIndex);
                    }
                }
            }
            // remove any sections before present position - train has passed over these sections
            else if (thisRouteIndex > 0)
            {
                for (int iindex = thisRouteIndex - 1; iindex >= 0; iindex--)
                {
                    newRoute.RemoveAt(iindex);
                }
            }

            // check if route ends at signal, determine length

            totalLengthM = 0;
            thisSection = TrackCircuitSection.TrackCircuitList[requiredPosition.TrackCircuitSectionIndex];
            offsetM = direction == 0 ? requiredPosition.Offset : thisSection.Length - requiredPosition.Offset;
            bool endWithSignal = false;    // ends with signal at STOP
            int sectionWithSignalIndex = 0;

            for (int iindex = 0; iindex < newRoute.Count && !endWithSignal; iindex++)
            {
                thisElement = newRoute[iindex];

                thisSection = thisElement.TrackCircuitSection;
                totalLengthM += (thisSection.Length - offsetM);
                offsetM = 0.0f; // reset offset for further sections

                // check on state of signals
                // also check if signal properly enabled

                reqDirection = (TrackDirection)thisElement.Direction;
                if (thisSection.EndSignals[reqDirection] != null)
                {
                    var endSignal = thisSection.EndSignals[reqDirection];
                    var thisAspect = thisSection.EndSignals[reqDirection].SignalLR(SignalFunction.Normal);

                    if (thisAspect == SignalAspectState.Stop && endSignal.OverridePermission != SignalPermission.Granted)
                    {
                        endWithSignal = true;
                        sectionWithSignalIndex = iindex;
                    }
                    else if (!endSignal.Enabled)   // signal cleared by default only - request for proper clearing
                    {
                        endSignal.RequestClearSignalExplorer(newRoute, thisRouted, true, 0);  // do NOT propagate
                    }

                }
            }

            // check if signal is in last section
            // if not, probably moved forward beyond a signal, so remove all beyond first signal

            if (endWithSignal && sectionWithSignalIndex < newRoute.Count - 1)
            {
                for (int iindex = newRoute.Count - 1; iindex >= sectionWithSignalIndex + 1; iindex--)
                {
                    thisSection = newRoute[iindex].TrackCircuitSection;
                    thisSection.RemoveTrain(this, true);
                    newRoute.RemoveAt(iindex);
                }
            }

            // check for any uncleared signals in route - if first found, request clear signal

            bool unclearedSignal = false;
            int signalIndex = newRoute.Count - 1;
            int nextUnclearSignalIndex = -1;

            for (int iindex = 0; iindex <= newRoute.Count - 1 && !unclearedSignal; iindex++)
            {
                thisElement = newRoute[iindex];
                thisSection = thisElement.TrackCircuitSection;

                var nextSignal = thisSection.EndSignals[(TrackDirection)thisElement.Direction];
                if (nextSignal != null &&
                    nextSignal.SignalLR(SignalFunction.Normal) == SignalAspectState.Stop &&
                    nextSignal.OverridePermission != SignalPermission.Granted)
                {
                    unclearedSignal = true;
                    signalIndex = iindex;
                    nextUnclearSignalIndex = nextSignal.Index;
                }
            }

            // route created to signal or max length, now check availability - but only up to first unclear signal
            // check if other train in first section

            if (newRoute.Count > 0)
            {
                thisElement = newRoute[0];
                thisSection = thisElement.TrackCircuitSection;
                reqDirection = forward ? thisElement.Direction : thisElement.Direction.Next();
                offsetM = direction == 0 ? requiredPosition.Offset : thisSection.Length - requiredPosition.Offset;

                Dictionary<Train, float> firstTrainInfo = thisSection.TestTrainAhead(this, offsetM, reqDirection);
                if (firstTrainInfo.Count > 0)
                {
                    foreach (KeyValuePair<Train, float> thisTrainAhead in firstTrainInfo)  // there is only one value
                    {
                        endAuthority = EndAuthorityType.TrainAhead;
                        endAuthorityDistanceM = thisTrainAhead.Value;
                        if (!thisSection.CircuitState.OccupiedByThisTrain(this)) thisSection.PreReserve(thisRouted);
                    }
                }

                // check route availability
                // reserve sections which are available

                else
                {
                    int lastValidSectionIndex = 0;
                    bool isAvailable = true;
                    totalLengthM = 0;

                    for (int iindex = 0; iindex <= signalIndex && isAvailable; iindex++)
                    {
                        thisSection = newRoute[iindex].TrackCircuitSection;

                        if (isAvailable)
                        {
                            if (thisSection.IsAvailable(this))
                            {
                                lastValidSectionIndex = iindex;
                                totalLengthM += (thisSection.Length - offsetM);
                                offsetM = 0;
                                thisSection.Reserve(thisRouted, newRoute);
                            }
                            else
                            {
                                isAvailable = false;
                            }
                        }
                    }

                    // set default authority to max distance
                    endAuthority = EndAuthorityType.MaxDistance;
                    endAuthorityDistanceM = totalLengthM;

                    // if last section ends with signal, set authority to signal
                    thisElement = newRoute[lastValidSectionIndex];
                    thisSection = thisElement.TrackCircuitSection;
                    reqDirection = forward ? thisElement.Direction : thisElement.Direction.Next();
                    // last section ends with signal
                    if (thisSection.EndSignals[reqDirection] != null)
                    {
                        endAuthority = EndAuthorityType.Signal;
                        endAuthorityDistanceM = totalLengthM;
                    }

                    // sections not clear - check if end has signal

                    else
                    {

                        TrackCircuitSection nextSection = null;
                        TrackCircuitRouteElement nextElement = null;

                        if (lastValidSectionIndex < newRoute.Count - 1)
                        {
                            nextElement = newRoute[lastValidSectionIndex + 1];
                            nextSection = nextElement.TrackCircuitSection;
                        }

                        // check for end authority if not ended with signal
                        // last section is end of track
                        if (thisSection.CircuitType == TrackCircuitType.EndOfTrack)
                        {
                            endAuthority = EndAuthorityType.EndOfTrack;
                            endAuthorityDistanceM = totalLengthM;
                        }

                        // first non-available section is switch or crossover
                        else if (nextSection != null && (nextSection.CircuitType == TrackCircuitType.Junction ||
                                     nextSection.CircuitType == TrackCircuitType.Crossover))
                        {
                            endAuthority = EndAuthorityType.ReservedSwitch;
                            endAuthorityDistanceM = totalLengthM;
                        }

                        // set authority is end of path unless train ahead
                        else
                        {
                            endAuthority = EndAuthorityType.EndOfPath;
                            endAuthorityDistanceM = totalLengthM;

                            // check if train ahead not moving in opposite direction, in first non-available section

                            if (nextSection != null)
                            {
                                int oppositeDirection = forward ? (nextElement.Direction == 0 ? 1 : 0) : (nextElement.Direction == 0 ? 0 : 1);
                                reqDirection = forward ? nextElement.Direction : nextElement.Direction.Next();

                                bool oppositeTrain = nextSection.CircuitState.Occupied(oppositeDirection, false);

                                if (!oppositeTrain)
                                {
                                    Dictionary<Train, float> nextTrainInfo = nextSection.TestTrainAhead(this, 0.0f, reqDirection);
                                    if (nextTrainInfo.Count > 0)
                                    {
                                        foreach (KeyValuePair<Train, float> thisTrainAhead in nextTrainInfo)  // there is only one value
                                        {
                                            endAuthority = EndAuthorityType.TrainAhead;
                                            endAuthorityDistanceM = thisTrainAhead.Value + totalLengthM;
                                            lastValidSectionIndex++;
                                            nextSection.PreReserve(thisRouted);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // remove invalid sections from route
                    if (lastValidSectionIndex < newRoute.Count - 1)
                    {
                        for (int iindex = newRoute.Count - 1; iindex > lastValidSectionIndex; iindex--)
                        {
                            newRoute.RemoveAt(iindex);
                        }
                    }
                }

                // check if route ends at signal and this is first unclear signal
                // if so, request clear signal

                if (endAuthority == EndAuthorityType.Signal)
                {
                    TrackCircuitSection lastSection = newRoute[newRoute.Count - 1].TrackCircuitSection;
                    TrackDirection lastDirection = (TrackDirection)newRoute[newRoute.Count - 1].Direction;
                    if (lastSection.EndSignals[lastDirection] != null && lastSection.EndSignals[lastDirection].Index == nextUnclearSignalIndex)
                    {
                        Signal reqSignal = signalRef.Signals[nextUnclearSignalIndex];
                        newRoute = reqSignal.RequestClearSignalExplorer(newRoute, forward ? routedForward : routedBackward, false, 0);
                    }
                }
            }

            // no valid route could be found
            else
            {
                endAuthority = EndAuthorityType.NoPathReserved;
                endAuthorityDistanceM = 0.0f;
            }

            return (newRoute);
        }

        //================================================================================================//
        /// <summary>
        /// Restore Explorer Mode
        /// </summary>

        public void RestoreExplorerMode()
        {
            // get next signal

            // forward
            NextSignalObject[0] = null;
            for (int iindex = 0; iindex < ValidRoute[0].Count && NextSignalObject[0] == null; iindex++)
            {
                TrackCircuitRouteElement thisElement = ValidRoute[0][iindex];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                NextSignalObject[0] = thisSection.EndSignals[thisElement.Direction];
            }

            // backward
            NextSignalObject[1] = null;
            for (int iindex = 0; iindex < ValidRoute[1].Count && NextSignalObject[1] == null; iindex++)
            {
                TrackCircuitRouteElement thisElement = ValidRoute[1][iindex];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                NextSignalObject[1] = thisSection.EndSignals[thisElement.Direction];
            }
        }


        //================================================================================================//
        //
        // Request signal permission in explorer mode
        //

        public void RequestExplorerSignalPermission(ref TrackCircuitPartialPathRoute selectedRoute, Direction routeDirection)
        {
            // check route for first signal at danger, from present position

            Signal reqSignal = null;
            bool signalFound = false;

            if (ValidRoute[(int)routeDirection] != null)
            {
                for (int iIndex = PresentPosition[routeDirection].RouteListIndex; iIndex <= ValidRoute[(int)routeDirection].Count - 1 && !signalFound; iIndex++)
                {
                    TrackCircuitSection thisSection = ValidRoute[(int)routeDirection][iIndex].TrackCircuitSection;
                    TrackDirection direction = ValidRoute[(int)routeDirection][iIndex].Direction;

                    if (thisSection.EndSignals[direction] != null)
                    {
                        reqSignal = thisSection.EndSignals[direction];
                        signalFound = (reqSignal.SignalLR(SignalFunction.Normal) == SignalAspectState.Stop);
                    }
                }
            }

            // if no signal at danger is found - report warning
            if (!signalFound)
            {
                if (simulator.Confirmer != null && TrainType != TrainType.Remote) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("No signal in train's path"));
                return;
            }

            // signal at danger is found - set PERMISSION REQUESTED, and request clear signal
            // if signal has a route, set PERMISSION REQUESTED, and perform signal update
            reqSignal.OverridePermission = SignalPermission.Requested;

            TrackCircuitPosition tempPos = new TrackCircuitPosition(PresentPosition[Direction.Backward], routeDirection != Direction.Forward);
            TrackCircuitPartialPathRoute newRouteR = CheckExplorerPath((int)routeDirection, tempPos, ValidRoute[(int)routeDirection], true, ref EndAuthorityTypes[(int)routeDirection],
                ref DistanceToEndNodeAuthorityM[(int)routeDirection]);
            ValidRoute[(int)routeDirection] = newRouteR;
            simulator.SoundNotify = reqSignal.OverridePermission == SignalPermission.Granted ?
                TrainEvent.PermissionGranted :
                TrainEvent.PermissionDenied;
        }

        //================================================================================================//
        /// <summary>
        /// Process request to set switch in explorer mode
        /// Request may contain direction or actual node
        /// </summary>

        public bool ProcessRequestExplorerSetSwitch(Direction direction)
        {
            // find first switch in required direction

            TrackCircuitSection reqSwitch = null;
            int routeDirectionIndex = (int)direction;
            bool switchSet = false;

            for (int iindex = 0; iindex < ValidRoute[routeDirectionIndex].Count && reqSwitch == null; iindex++)
            {
                TrackCircuitSection thisSection = ValidRoute[routeDirectionIndex][iindex].TrackCircuitSection;
                if (thisSection.CircuitType == TrackCircuitType.Junction)
                {
                    reqSwitch = thisSection;
                }
            }

            if (reqSwitch == null)
            {
                // search beyond last section for switch using default pins (continue through normal sections only)

                TrackCircuitRouteElement thisElement = ValidRoute[routeDirectionIndex][ValidRoute[routeDirectionIndex].Count - 1];
                TrackCircuitSection lastSection = thisElement.TrackCircuitSection;
                TrackDirection curDirection = thisElement.Direction;
                int nextSectionIndex = thisElement.TrackCircuitSection.Index;

                bool validRoute = lastSection.CircuitType == TrackCircuitType.Normal;

                while (reqSwitch == null && validRoute)
                {
                    if (lastSection.CircuitType == TrackCircuitType.Crossover)
                    {
                        TrackDirection outPinIndex = curDirection.Next();
                        if (lastSection.Pins[curDirection, Location.NearEnd].Link == nextSectionIndex)
                        {
                            nextSectionIndex = lastSection.Pins[outPinIndex, Location.NearEnd].Link;
                            curDirection = lastSection.Pins[outPinIndex, Location.NearEnd].Direction;
                        }
                        else if (lastSection.Pins[curDirection, Location.FarEnd].Link == nextSectionIndex)
                        {
                            nextSectionIndex = lastSection.Pins[outPinIndex, Location.FarEnd].Link;
                            curDirection = lastSection.Pins[outPinIndex, Location.FarEnd].Direction;
                        }
                    }
                    else
                    {
                        nextSectionIndex = lastSection.Pins[curDirection, Location.NearEnd].Link;
                        curDirection = lastSection.ActivePins[curDirection, Location.NearEnd].Direction;
                        lastSection = TrackCircuitSection.TrackCircuitList[nextSectionIndex];
                    }

                    if (lastSection.CircuitType == TrackCircuitType.Junction)
                    {
                        reqSwitch = lastSection;
                    }
                    else if (lastSection.CircuitType != TrackCircuitType.Normal)
                    {
                        validRoute = false;
                    }
                }
            }

            if (reqSwitch != null)
            {
                // check if switch is clear
                if (!reqSwitch.CircuitState.Occupied() && reqSwitch.CircuitState.TrainReserved == null && reqSwitch.CircuitState.SignalReserved < 0)
                {
                    reqSwitch.JunctionSetManual = reqSwitch.JunctionLastRoute == 0 ? 1 : 0;
                    signalRef.SetSwitch(reqSwitch.OriginalIndex, reqSwitch.JunctionSetManual, reqSwitch);
                    switchSet = true;
                }
                // check if switch reserved by this train - if so, dealign
                else if (reqSwitch.CircuitState.TrainReserved != null && reqSwitch.CircuitState.TrainReserved.Train == this)
                {
                    reqSwitch.DeAlignSwitchPins();
                    reqSwitch.JunctionSetManual = reqSwitch.JunctionLastRoute == 0 ? 1 : 0;
                    signalRef.SetSwitch(reqSwitch.OriginalIndex, reqSwitch.JunctionSetManual, reqSwitch);
                    switchSet = true;
                }

                if (switchSet)
                    ProcessExplorerSwitch(routeDirectionIndex, reqSwitch, direction);
            }
            else
            {
                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("No switch found"));
            }

            return (switchSet);
        }

        internal void ProcessRequestExplorerSetSwitch(int reqSwitchIndex)
        {
            // find switch in route - forward first

            int routeDirectionIndex = -1;
            bool switchFound = false;
            Direction direction = Direction.Forward;

            for (int iindex = 0; iindex < ValidRoute[0].Count - 1 && !switchFound; iindex++)
            {
                if (ValidRoute[0][iindex].TrackCircuitSection.Index == reqSwitchIndex)
                {
                    routeDirectionIndex = 0;
                    direction = Direction.Forward;
                    switchFound = true;
                }
            }

            if (ValidRoute[1] != null)
            {
                for (int iindex = 0; iindex < ValidRoute[1].Count - 1 && !switchFound; iindex++)
                {
                    if (ValidRoute[1][iindex].TrackCircuitSection.Index == reqSwitchIndex)
                    {
                        routeDirectionIndex = 1;
                        direction = Direction.Backward;
                        switchFound = true;
                    }
                }
            }

            if (switchFound)
            {
                TrackCircuitSection reqSwitch = TrackCircuitSection.TrackCircuitList[reqSwitchIndex];
                ProcessExplorerSwitch(routeDirectionIndex, reqSwitch, direction);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Process switching of explorer switch
        /// </summary>

        public void ProcessExplorerSwitch(int routeDirectionIndex, TrackCircuitSection switchSection, Direction direction)
        {
            //<CSComment> Probably also in singleplayer the logic of multiplayer should be used, but it's unwise to modify it just before a release
            TrainRouted thisRouted = direction == Direction.Backward ^ !MPManager.IsMultiPlayer() ? routedBackward : routedForward;
            TrackCircuitPartialPathRoute selectedRoute = ValidRoute[routeDirectionIndex];

            // store required position
            int reqSwitchPosition = switchSection.JunctionSetManual;

            // find index of section in present route
            int junctionIndex = selectedRoute.GetRouteIndex(switchSection.Index, 0);
            int lastIndex = junctionIndex - 1; // set previous index as last valid index

            // find first signal from train and before junction
            Signal firstSignal = null;
            float coveredLength = 0;

            for (int iindex = 0; iindex < junctionIndex && firstSignal == null; iindex++)
            {
                TrackCircuitRouteElement thisElement = selectedRoute[iindex];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                if (iindex > 0) coveredLength += thisSection.Length; // do not use first section

                TrackDirection signalDirection = thisElement.Direction;

                if (thisSection.EndSignals[signalDirection] != null &&
                    thisSection.EndSignals[signalDirection].EnabledTrain != null &&
                    thisSection.EndSignals[signalDirection].EnabledTrain.Train == this)
                {
                    firstSignal = thisSection.EndSignals[signalDirection];
                    lastIndex = iindex;
                }
            }

            // if last first is found : reset signal and further signals, clear route as from signal and request clear signal

            if (firstSignal != null)
            {
                firstSignal.ResetSignal(true);

                // breakdown and clear route

                // checke whether trailing or leading
                //<CSComment> Probably also in singleplayer the logic of multiplayer should be used, but it's unwise to modify it just before a release
                if (switchSection.Pins[TrackDirection.Ahead, Location.NearEnd].Link == selectedRoute[lastIndex].TrackCircuitSection.Index || !MPManager.IsMultiPlayer())
                // leading, train may still own switch

                {

                    signalRef.BreakDownRouteList(selectedRoute, lastIndex + 1, thisRouted);
                    selectedRoute.RemoveRange(lastIndex + 1, selectedRoute.Count - lastIndex - 1);

                    // restore required position (is cleared by route breakdown)
                    switchSection.JunctionSetManual = reqSwitchPosition;

                    // set switch
                    switchSection.DeAlignSwitchPins();
                    signalRef.SetSwitch(switchSection.OriginalIndex, switchSection.JunctionSetManual, switchSection);

                    // build new route - use signal request
                    firstSignal.RequestClearSignalExplorer(selectedRoute, thisRouted, false, 0);
                }
                else
                {
                    // trailing, train must not own switch any more
                    signalRef.BreakDownRouteList(selectedRoute, junctionIndex, thisRouted);
                    selectedRoute.RemoveRange(junctionIndex, selectedRoute.Count - junctionIndex);

                    // restore required position (is cleared by route breakdown)
                    switchSection.JunctionSetManual = reqSwitchPosition;

                    // set switch
                    switchSection.DeAlignSwitchPins();
                    signalRef.SetSwitch(switchSection.OriginalIndex, switchSection.JunctionSetManual, switchSection);
                }
            }

            // no signal is found - build route using full update process
            else
            {
                signalRef.BreakDownRouteList(selectedRoute, 0, thisRouted);
                selectedRoute.Clear();
                manualTrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                    PresentPosition[Direction.Backward].Direction, Length, false, true, false);
                UpdateExplorerMode(-1);
            }
        }

        //================================================================================================//
        //
        // Switch to explorer mode
        //

        public void ToggleToExplorerMode()
        {
            if (ControlMode == TrainControlMode.OutOfControl && LeadLocomotive != null)
                ((MSTSLocomotive)LeadLocomotive).SetEmergency(false);

            // set track occupation (using present route)
            UpdateSectionStateExplorer();

            // breakdown present route - both directions if set

            if (ValidRoute[0] != null)
            {
                int listIndex = PresentPosition[Direction.Forward].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[0], listIndex, routedForward);
                ClearDeadlocks();
            }

            ValidRoute[0] = null;
            LastReservedSection[0] = -1;

            if (ValidRoute[1] != null)
            {
                int listIndex = PresentPosition[Direction.Backward].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[1], listIndex, routedBackward);
            }
            ValidRoute[1] = null;
            LastReservedSection[1] = -1;

            // clear all outstanding actions

            ClearActiveSectionItems();
            requiredActions.RemovePendingAIActionItems(true);

            // clear signal info

            NextSignalObject[0] = null;
            NextSignalObject[1] = null;

            SignalObjectItems.Clear();

            PassedSignalSpeeds.Clear();

            // set explorer mode

            ControlMode = TrainControlMode.Explorer;

            // reset routes and check sections either end of train

            PresentPosition[Direction.Forward].RouteListIndex = -1;
            PresentPosition[Direction.Backward].RouteListIndex = -1;
            PreviousPosition[Direction.Forward].RouteListIndex = -1;

            UpdateExplorerMode(-1);
        }

        //================================================================================================//
        /// <summary>
        /// Update out-of-control mode
        /// </summary>

        public void UpdateOutOfControl()
        {

            // train is at a stand : 
            // clear all occupied blocks
            // clear signal/speedpost list 
            // clear DistanceTravelledActions 
            // clear all previous occupied sections 
            // set sections occupied on which train stands

            // all the above is still TODO
        }

        //================================================================================================//
        /// <summary>
        /// Switch to Auto Signal mode
        /// </summary>

        public virtual void SwitchToSignalControl(Signal thisSignal)
        {
            // in auto mode, use forward direction only

            ControlMode = TrainControlMode.AutoSignal;
            thisSignal.RequestClearSignal(ValidRoute[0], routedForward, 0, false, null);

            // enable any none-NORMAL signals between front of train and first NORMAL signal
            int firstSectionIndex = PresentPosition[Direction.Forward].RouteListIndex;
            int lastSectionIndex = ValidRoute[0].GetRouteIndex(thisSignal.TrackCircuitIndex, firstSectionIndex);

            // first, all signals in present section beyond position of train
            TrackCircuitSection thisSection = ValidRoute[0][firstSectionIndex].TrackCircuitSection;
            TrackDirection thisDirection = ValidRoute[0][firstSectionIndex].Direction;

            for (int isigtype = 0; isigtype < signalRef.OrtsSignalTypeCount; isigtype++)
            {
                TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[thisDirection][isigtype];
                foreach (TrackCircuitSignalItem thisItem in thisList)
                {
                    if (thisItem.SignalLocation > PresentPosition[Direction.Forward].Offset && !thisItem.Signal.SignalNormal())
                    {
                        thisItem.Signal.EnabledTrain = this.routedForward;
                    }
                }
            }

            // next, signals in any further sections
            for (int iSectionIndex = firstSectionIndex + 1; iSectionIndex <= lastSectionIndex; iSectionIndex++)
            {
                thisSection = ValidRoute[0][firstSectionIndex].TrackCircuitSection;
                thisDirection = ValidRoute[0][firstSectionIndex].Direction;

                for (int isigtype = 0; isigtype < signalRef.OrtsSignalTypeCount; isigtype++)
                {
                    TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[thisDirection][isigtype];
                    foreach (TrackCircuitSignalItem thisItem in thisList)
                    {
                        if (!thisItem.Signal.SignalNormal())
                        {
                            thisItem.Signal.EnabledTrain = this.routedForward;
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Switch to Auto Node mode
        /// </summary>

        public virtual void SwitchToNodeControl(int thisSectionIndex)
        {
            // reset enabled signal if required
            if (ControlMode == TrainControlMode.AutoSignal && NextSignalObject[0] != null && NextSignalObject[0].EnabledTrain == routedForward)
            {
                // reset any claims
                foreach (TrackCircuitRouteElement thisElement in NextSignalObject[0].SignalRoute)
                {
                    TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                    thisSection.CircuitState.TrainClaimed.Remove(routedForward);
                }

                // reset signal
                NextSignalObject[0].EnabledTrain = null;
                NextSignalObject[0].ResetSignal(true);
            }

            // use direction forward only
            float maxDistance = Math.Max(AllowedMaxSpeedMpS * maxTimeS, minCheckDistanceM);
            float clearedDistanceM = 0.0f;

            int activeSectionIndex = thisSectionIndex;
            int endListIndex = -1;

            ControlMode = TrainControlMode.AutoNode;
            EndAuthorityTypes[0] = EndAuthorityType.NoPathReserved;
            IndexNextSignal = -1; // no next signal in Node Control

            // if section is set, check if it is on route and ahead of train

            if (activeSectionIndex > 0)
            {
                endListIndex = ValidRoute[0].GetRouteIndex(thisSectionIndex, PresentPosition[Direction.Forward].RouteListIndex);

                // section is not on route - give warning and break down route, following active links and resetting reservation

                if (endListIndex < 0)
                {
                    signalRef.BreakDownRoute(thisSectionIndex, routedForward);
                    activeSectionIndex = -1;
                }

                // if section is (still) set, check if this is at maximum distance

                if (activeSectionIndex > 0)
                {
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[activeSectionIndex];
                    clearedDistanceM = GetDistanceToTrain(activeSectionIndex, thisSection.Length);

                    if (clearedDistanceM > maxDistance)
                    {
                        EndAuthorityTypes[0] = EndAuthorityType.MaxDistance;
                        LastReservedSection[0] = thisSection.Index;
                        DistanceToEndNodeAuthorityM[0] = clearedDistanceM;
                    }
                }
                else
                {
                    EndAuthorityTypes[0] = EndAuthorityType.NoPathReserved;
                }
            }

            // new request or not beyond max distance

            if (activeSectionIndex < 0 || EndAuthorityTypes[0] != EndAuthorityType.MaxDistance)
            {
                signalRef.RequestClearNode(routedForward, ValidRoute[0]);
            }
        }

        //================================================================================================//
        //
        // Request to switch to or from manual mode
        //

        public void RequestToggleManualMode()
        {
            if (TrainType == TrainType.AiPlayerHosting)
            {
                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("You cannot enter manual mode when autopiloted"));
            }
            else if (IsPathless && ControlMode != TrainControlMode.OutOfControl && ControlMode == TrainControlMode.Manual)
            {
                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("You cannot use this command for pathless trains"));
            }
            else if (ControlMode == TrainControlMode.Manual)
            {
                // check if train is back on path

                TrackCircuitPartialPathRoute lastRoute = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];
                int routeIndex = lastRoute.GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);

                if (routeIndex < 0)
                {
                    if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                        simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Train is not back on original route"));
                }
                else
                {
                    TrackDirection lastDirection = lastRoute[routeIndex].Direction;
                    TrackDirection presentDirection = PresentPosition[Direction.Forward].Direction;
                    if (lastDirection != presentDirection && Math.Abs(SpeedMpS) > 0.1f)
                    {
                        if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                            simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Original route is reverse from present direction, stop train before switching"));
                    }
                    else
                    {
                        ToggleFromManualMode(routeIndex);
                        simulator.Confirmer.Confirm(CabControl.SignalMode, CabSetting.On);
                    }
                }

            }
            else if (ControlMode == TrainControlMode.Explorer)
            {
                if (LeadLocomotive != null &&
                    (((MSTSLocomotive)LeadLocomotive).TrainBrakeController.TCSEmergencyBraking || ((MSTSLocomotive)LeadLocomotive).TrainBrakeController.TCSFullServiceBraking))
                {
                    ((MSTSLocomotive)LeadLocomotive).SetEmergency(false);
                    ResetExplorerMode();
                    return;
                }
                else if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Cannot change to Manual Mode while in Explorer Mode"));
            }
            else
            {
                ToggleToManualMode();
                simulator.Confirmer.Confirm(CabControl.SignalMode, CabSetting.Off);
            }
        }

        //================================================================================================//
        //
        // Switch to manual mode
        //

        public void ToggleToManualMode()
        {
            if (LeadLocomotive != null)
                ((MSTSLocomotive)LeadLocomotive).SetEmergency(false);

            // set track occupation (using present route)
            UpdateSectionStateManual();

            // breakdown present route - both directions if set

            if (ValidRoute[0] != null)
            {
                int listIndex = PresentPosition[Direction.Forward].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[0], listIndex, routedForward);
                ClearDeadlocks();
            }

            ValidRoute[0] = null;
            LastReservedSection[0] = -1;

            if (ValidRoute[1] != null)
            {
                int listIndex = PresentPosition[Direction.Backward].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[1], listIndex, routedBackward);
            }
            ValidRoute[1] = null;
            LastReservedSection[1] = -1;

            // clear all outstanding actions

            ClearActiveSectionItems();
            requiredActions.RemovePendingAIActionItems(true);

            // clear signal info

            NextSignalObject[0] = null;
            NextSignalObject[1] = null;

            SignalObjectItems.Clear();

            PassedSignalSpeeds.Clear();

            // set manual mode

            ControlMode = TrainControlMode.Manual;

            // reset routes and check sections either end of train

            PresentPosition[Direction.Forward].RouteListIndex = -1;
            PresentPosition[Direction.Backward].RouteListIndex = -1;
            PreviousPosition[Direction.Forward].RouteListIndex = -1;

            UpdateManualMode(-1);
        }

        //================================================================================================//
        //
        // Switch back from manual mode
        //

        public void ToggleFromManualMode(int routeIndex)
        {
            // extract route at present front position

            TrackCircuitPartialPathRoute newRoute = new TrackCircuitPartialPathRoute();
            TrackCircuitPartialPathRoute oldRoute = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];

            // test on reversal, if so check rear of train

            bool reversal = false;
            if (!CheckReversal(oldRoute, ref reversal))
            {
                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Reversal required and rear of train not on required route"));
                return;
            }

            // breakdown present routes, forward and backward
            signalRef.BreakDownRouteList(ValidRoute[0], 0, routedForward);
            signalRef.BreakDownRouteList(ValidRoute[1], 0, routedBackward);


            // clear occupied sections

            for (int iSection = OccupiedTrack.Count - 1; iSection >= 0; iSection--)
            {
                TrackCircuitSection thisSection = OccupiedTrack[iSection];
                thisSection.ResetOccupied(this);
            }

            // remove any actions build up during manual mode
            requiredActions.RemovePendingAIActionItems(true);

            // restore train placement
            RestoreTrainPlacement(ref newRoute, oldRoute, routeIndex, reversal);

            // restore distance travelled in Present Position
            PresentPosition[Direction.Forward].DistanceTravelled = DistanceTravelledM;
            PresentPosition[Direction.Backward].DistanceTravelled = DistanceTravelledM - Length;

            // set track occupation (using present route)
            // This procedure is also needed for clearing track occupation.
            UpdateSectionStateManual();

            // restore signal information
            PassedSignalSpeeds.Clear();
            InitializeSignals(true);

            // restore deadlock information

            CheckDeadlock(ValidRoute[0], Number);    // Check deadlock against all other trains

            // switch to AutoNode mode

            LastReservedSection[0] = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            LastReservedSection[1] = PresentPosition[Direction.Backward].TrackCircuitSectionIndex;

            if (!simulator.TimetableMode) AuxActionsContainer.ResetAuxAction(this);
            SwitchToNodeControl(PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
            TCRoute.SetReversalOffset(Length, simulator.TimetableMode);
        }

        //================================================================================================//
        //
        // ResetExplorerMode
        //

        public void ResetExplorerMode()
        {
            if (ControlMode == TrainControlMode.OutOfControl && LeadLocomotive != null)
                ((MSTSLocomotive)LeadLocomotive).SetEmergency(false);

            // set track occupation (using present route)
            UpdateSectionStateExplorer();

            // breakdown present route - both directions if set

            if (ValidRoute[0] != null)
            {
                int listIndex = PresentPosition[Direction.Forward].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[0], listIndex, routedForward);
                ClearDeadlocks();
            }

            ValidRoute[0] = null;
            LastReservedSection[0] = -1;

            if (ValidRoute[1] != null)
            {
                int listIndex = PresentPosition[Direction.Backward].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[1], listIndex, routedBackward);
            }
            ValidRoute[1] = null;
            LastReservedSection[1] = -1;

            // clear all outstanding actions

            ClearActiveSectionItems();
            requiredActions.RemovePendingAIActionItems(true);

            // clear signal info

            NextSignalObject[0] = null;
            NextSignalObject[1] = null;

            SignalObjectItems.Clear();

            PassedSignalSpeeds.Clear();

            // set explorer mode

            ControlMode = TrainControlMode.Explorer;

            // reset routes and check sections either end of train

            PresentPosition[Direction.Forward].RouteListIndex = -1;
            PresentPosition[Direction.Backward].RouteListIndex = -1;
            PreviousPosition[Direction.Forward].RouteListIndex = -1;

            UpdateExplorerMode(-1);
        }

        //================================================================================================//
        //
        // Check if reversal is required
        //

        public bool CheckReversal(TrackCircuitPartialPathRoute reqRoute, ref bool reversal)
        {
            bool valid = true;

            int presentRouteIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            int reqRouteIndex = reqRoute.GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            if (presentRouteIndex < 0 || reqRouteIndex < 0)
            {
                valid = false;  // front of train not on present route or not on required route
            }
            // valid point : check if reversal is required
            else
            {
                TrackCircuitRouteElement presentElement = ValidRoute[0][presentRouteIndex];
                TrackCircuitRouteElement pathElement = reqRoute[reqRouteIndex];

                if (presentElement.Direction != pathElement.Direction)
                {
                    reversal = true;
                }
            }

            // if reversal required : check if rear of train is on required route
            if (valid && reversal)
            {
                int rearRouteIndex = reqRoute.GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
                valid = rearRouteIndex >= 0;
            }

            return (valid);
        }

        //================================================================================================//
        //
        // Restore train placement
        //

        public void RestoreTrainPlacement(ref TrackCircuitPartialPathRoute newRoute, TrackCircuitPartialPathRoute oldRoute, int frontIndex, bool reversal)
        {
            // reverse train if required

            if (reversal)
            {
                ReverseFormation(true);
                // active subpath must be incremented in parallel in incorporated train if present
                if (IncorporatedTrainNo >= 0) IncrementSubpath(simulator.TrainDictionary[IncorporatedTrainNo]);
            }

            // reset distance travelled

            DistanceTravelledM = 0.0f;

            // check if end of train on original route
            // copy sections from earliest start point (front or rear)

            int rearIndex = oldRoute.GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
            int startIndex = rearIndex >= 0 ? Math.Min(rearIndex, frontIndex) : frontIndex;

            for (int iindex = startIndex; iindex < oldRoute.Count; iindex++)
            {
                newRoute.Add(oldRoute[iindex]);
            }

            // if rear not on route, build route under train and add sections

            if (rearIndex < 0)
            {

                TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                            PresentPosition[Direction.Backward].Direction, Length, true, true, false);

                for (int iindex = tempRoute.Count - 1; iindex >= 0; iindex--)
                {
                    TrackCircuitRouteElement thisElement = tempRoute[iindex];
                    if (!newRoute.ContainsSection(thisElement))
                    {
                        newRoute.Insert(0, thisElement);
                    }
                }
            }

            // set route as valid route

            ValidRoute[0] = newRoute;

            // Reindexes ReversalInfo items
            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex >= 0)
                TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex = ValidRoute[0].GetRouteIndex(TCRoute.ReversalInfo[TCRoute.ActiveSubPath].DivergeSectorIndex, 0);
            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex >= 0)
                TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex = ValidRoute[0].GetRouteIndex(TCRoute.ReversalInfo[TCRoute.ActiveSubPath].SignalSectorIndex, 0);



            // get index of first section in route

            rearIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
            PresentPosition[Direction.Backward].RouteListIndex = rearIndex;

            // get index of front of train

            frontIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
            PresentPosition[Direction.Forward].RouteListIndex = frontIndex;

            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            // set track occupied - forward only

            foreach (TrackCircuitSection thisSection in OccupiedTrack)
            {
                if (!thisSection.CircuitState.OccupiedByThisTrain(this))
                {
                    thisSection.Reserve(routedForward, ValidRoute[0]);
                    thisSection.SetOccupied(routedForward);
                }
            }

        }


        //================================================================================================//
        //
        // Request permission to pass signal
        //

        public void RequestSignalPermission(Direction direction)
        {
            if (MPManager.IsClient())
            {
                MPManager.Notify((new MSGResetSignal(MPManager.GetUserName())).ToString());
                return;
            }
            if (ControlMode == TrainControlMode.Manual)
            {
                if (direction == Direction.Forward)
                {
                    RequestManualSignalPermission(ref ValidRoute[0], 0);
                }
                else
                {
                    RequestManualSignalPermission(ref ValidRoute[1], 1);
                }
            }
            else if (ControlMode == TrainControlMode.Explorer)
            {
                RequestExplorerSignalPermission(ref ValidRoute[0], direction);
            }
            else
            {
                if (direction != Direction.Forward)
                {
                    if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                        simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Cannot clear signal behind train while in AUTO mode"));
                    simulator.SoundNotify = TrainEvent.PermissionDenied;
                }

                else if (NextSignalObject[0] != null)
                {
                    NextSignalObject[0].OverridePermission = SignalPermission.Requested;
                }
            }
        }

        //================================================================================================//
        //
        // Request reset signal
        //

        public void RequestResetSignal(Direction direction)
        {
            if (!MPManager.IsMultiPlayer())
            {
                if (ControlMode == TrainControlMode.Manual || ControlMode == TrainControlMode.Explorer)
                {
                    if (NextSignalObject[(int)direction]?.SignalLR(SignalFunction.Normal) != SignalAspectState.Stop)
                    {
                        int routeIndex = ValidRoute[(int)direction].GetRouteIndex(NextSignalObject[(int)direction].TrackCircuitNextIndex, PresentPosition[direction].RouteListIndex);
                        signalRef.BreakDownRouteList(ValidRoute[(int)direction], routeIndex, routedForward);
                        ValidRoute[(int)direction].RemoveRange(routeIndex, ValidRoute[(int)direction].Count - routeIndex);

                        NextSignalObject[(int)direction].ResetSignal(true);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Get distance from train to object position using route list
        /// </summary>

        private float GetObjectDistanceToTrain(SignalItemInfo thisObject)
        {

            // follow active links to get to object

            int reqSectionIndex = thisObject.SignalDetails.TrackCircuitIndex;
            float endOffset = thisObject.SignalDetails.TrackCircuitOffset;

            float distanceM = GetDistanceToTrain(reqSectionIndex, endOffset);

            //          if (distanceM < 0)
            //          {
            //              distanceM = thisObject.ObjectDetails.DistanceTo(FrontTDBTraveller);
            //          }

            return (distanceM);
        }

        //================================================================================================//
        /// <summary>
        /// Get distance from train to location using route list
        /// TODO : rewrite to use active links, and if fails use traveller
        /// location must have same direction as train
        /// </summary>

        public float GetDistanceToTrain(int sectionIndex, float endOffset)
        {
            // use start of list to see if passed position

            int endListIndex = ValidRoute[0].GetRouteIndex(sectionIndex, PresentPosition[Direction.Forward].RouteListIndex);
            if (endListIndex < 0)
                endListIndex = ValidRoute[0].GetRouteIndex(sectionIndex, 0);

            if (endListIndex >= 0 && endListIndex < PresentPosition[Direction.Forward].RouteListIndex) // index before present so we must have passed object
            {
                return (-1.0f);
            }

            if (endListIndex == PresentPosition[Direction.Forward].RouteListIndex && endOffset < PresentPosition[Direction.Forward].Offset) // just passed
            {
                return (-1.0f);
            }

            // section is not on route

            if (endListIndex < 0)
            {
                return (-1.0f);
            }

            int thisSectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            TrackDirection direction = PresentPosition[Direction.Forward].Direction;
            float startOffset = PresentPosition[Direction.Forward].Offset;

            return (TrackCircuitSection.GetDistanceBetweenObjects(thisSectionIndex, startOffset, direction, sectionIndex, endOffset));
        }

        /// Switch train to Out-of-Control
        /// Set mode and apply emergency brake
        internal void SetTrainOutOfControl(OutOfControlReason reason)
        {

            if (ControlMode == TrainControlMode.OutOfControl) // allready out of control, so exit
            {
                return;
            }

            // clear all reserved sections etc. - both directions
            if (ControlMode == TrainControlMode.AutoSignal)
            {
                if (NextSignalObject[0]?.EnabledTrain == routedForward)
                {
                    int routeIndexBeforeSignal = NextSignalObject[0].TrainRouteIndex - 1;
                    NextSignalObject[0].ResetSignal(true);
                    if (routeIndexBeforeSignal >= 0)
                        signalRef.BreakDownRoute(ValidRoute[0][routeIndexBeforeSignal].TrackCircuitSection.Index, routedForward);
                }
                if (NextSignalObject[1]?.EnabledTrain == routedBackward)
                {
                    NextSignalObject[1].ResetSignal(true);
                }
            }
            else if (ControlMode == TrainControlMode.AutoNode)
            {
                signalRef.BreakDownRoute(LastReservedSection[0], routedForward);
            }

            // TODO : clear routes for MANUAL
            if (!MPManager.IsMultiPlayer() || simulator.TimetableMode || reason != OutOfControlReason.OutOfPath || IsActualPlayerTrain)
            {

                // set control state and issue warning

                if (ControlMode != TrainControlMode.Explorer)
                    ControlMode = TrainControlMode.OutOfControl;

                OutOfControlReason = reason;

                StringBuilder report = new StringBuilder($"Train {Number} is out of control and will be stopped. Reason: ");
                switch (reason)
                {
                    case (OutOfControlReason.PassedAtDanger):
                        report.Append("train passed signal at Danger");
                        break;
                    case (OutOfControlReason.RearPassedAtDanger):
                        report.Append("train passed signal at Danger at rear of train");
                        break;
                    case (OutOfControlReason.OutOfAuthority):
                        report.Append("train passed limit of authority");
                        break;
                    case (OutOfControlReason.OutOfPath):
                        report.Append("train has ran off its allocated path");
                        break;
                    case (OutOfControlReason.SlippedIntoPath):
                        report.Append("train slipped back into path of another train");
                        break;
                    case (OutOfControlReason.SlippedToEndOfTrack):
                        report.Append("train slipped of the end of track");
                        break;
                    case (OutOfControlReason.OutOfTrack):
                        report.Append("train has moved off the track");
                        break;
                }
                simulator.Confirmer?.Message(ConfirmLevel.Warning, report.ToString());// As Confirmer may not be created until after a restore.

                if (LeadLocomotive != null)
                    ((MSTSLocomotive)LeadLocomotive).SetEmergency(true);
            }
            // the AI train is now out of path. Instead of killing him, we give him a chance on a new path
            else
            {
                GenerateValidRoute(PresentPosition[Direction.Forward].RouteListIndex, PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
                // switch to NODE mode
                if (ControlMode == TrainControlMode.AutoSignal)
                {
                    SwitchToNodeControl(PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
                }
                // reset actions to recalculate distances
                if (TrainType == TrainType.Ai || TrainType == TrainType.AiPlayerHosting)
                    ((AITrain)this).ResetActions(true);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Re-routes a train in auto mode after a switch moved manually
        /// </summary>

        public void ReRouteTrain(int forcedRouteSectionIndex, int forcedTCSectionIndex)
        {
            // check for any stations in abandoned path
            if (ControlMode == TrainControlMode.AutoSignal || ControlMode == TrainControlMode.AutoNode)
            // Local trains, having a defined TCRoute
            {
                int actSubpath = TCRoute.ActiveSubPath;
                Dictionary<int, StationStop> abdStations = new Dictionary<int, StationStop>();

                CheckAbandonedStations(forcedRouteSectionIndex, ValidRoute[0].Count - 1, actSubpath, abdStations);
                ResetValidRoute();
                GenerateValidRoute(forcedRouteSectionIndex, forcedTCSectionIndex);
                // check for abandoned stations - try to find alternative on passing path
                LookForReplacementStations(abdStations, ValidRoute[0], ValidRoute[0]);
            }
        }

        /// Resets ValidRoute after some event like a switch moved
        private void ResetValidRoute()
        {
            // clear all reserved sections etc. - both directions
            if (ControlMode == TrainControlMode.AutoSignal)
            {
                if (NextSignalObject[0]?.EnabledTrain == routedForward)
                {
                    int routeIndexBeforeSignal = NextSignalObject[0].TrainRouteIndex - 1;
                    NextSignalObject[0].ResetSignal(true);
                    if (routeIndexBeforeSignal >= 0)
                        signalRef.BreakDownRoute(ValidRoute[0][routeIndexBeforeSignal].TrackCircuitSection.Index, routedForward);
                }
                if (NextSignalObject[1]?.EnabledTrain == routedBackward)
                {
                    NextSignalObject[1].ResetSignal(true);
                }
            }
            else if (ControlMode == TrainControlMode.AutoNode)
            {
                signalRef.BreakDownRoute(LastReservedSection[0], routedForward);
            }
        }


        //================================================================================================//
        /// <summary>
        /// Generates a new ValidRoute after some event like a switch moved
        /// </summary>

        public void GenerateValidRoute(int forcedRouteSectionIndex, int forcedTCSectionIndex)
        {
            // We don't kill the AI train and build a new route for it
            // first of all we have to find out the new route
            List<int> tempSections = new List<int>();
            if (TCRoute.OriginalSubpath == -1) TCRoute.OriginalSubpath = TCRoute.ActiveSubPath;
            if (PresentPosition[Direction.Forward].RouteListIndex > 0)
                // clean case, train is in route and switch has been forced in front of it
                tempSections = SignalEnvironment.ScanRoute(this, forcedTCSectionIndex, 0, (TrackDirection)ValidRoute[0][forcedRouteSectionIndex].Direction,
                        true, 0, true, true,
                        false, false, true, false, false, false, false, IsFreight, false, true);
            else
                // dirty case, train is out of route and has already passed forced switch
                tempSections = SignalEnvironment.ScanRoute(this, PresentPosition[Direction.Forward].TrackCircuitSectionIndex, PresentPosition[Direction.Forward].Offset,
                    PresentPosition[Direction.Forward].Direction, true, 0, true, true, false, false, true, false, false, false, false, IsFreight, false, true);

            TrackCircuitPartialPathRoute newRoute = new TrackCircuitPartialPathRoute();
            // Copy part of route already run
            if (PresentPosition[Direction.Forward].RouteListIndex > 0)
            {
                for (int routeListIndex = 0; routeListIndex < forcedRouteSectionIndex; routeListIndex++) newRoute.Add(ValidRoute[0][routeListIndex]);
            }
            else if (PresentPosition[Direction.Forward].RouteListIndex < 0)
            {
                for (int routeListIndex = 0; routeListIndex <= PreviousPosition[Direction.Forward].RouteListIndex + 1; routeListIndex++) newRoute.Add(ValidRoute[0][routeListIndex]); // maybe + 1 is wrong?
            }
            if (tempSections.Count > 0)
            {
                // Add new part of route
                TrackCircuitRouteElement thisElement = null;
                int prevSection = -2;    // preset to invalid
                var tempSectionsIndex = 0;
                foreach (int sectionIndex in tempSections)
                {
                    TrackDirection sectionDirection = sectionIndex > 0 ? TrackDirection.Ahead : TrackDirection.Reverse;
                    thisElement = new TrackCircuitRouteElement(TrackCircuitSection.TrackCircuitList[Math.Abs(sectionIndex)], sectionDirection, prevSection);
                    // if junction, you have to adjust the OutPin
                    TrackCircuitSection.TrackCircuitList[Math.Abs(sectionIndex)].CircuitState.Forced = false;
                    if (TrackCircuitSection.TrackCircuitList[Math.Abs(sectionIndex)].CircuitType == TrackCircuitType.Junction && thisElement.FacingPoint == true)
                    {
                        var TCSection = TrackCircuitSection.TrackCircuitList[Math.Abs(sectionIndex)];
                        if (tempSectionsIndex < tempSections.Count - 1 && TCSection.Pins[sectionDirection, Location.FarEnd].Link == tempSections[tempSectionsIndex + 1])
                            thisElement.OutPin[Location.FarEnd] = TrackDirection.Reverse;
                        else thisElement.OutPin[Location.FarEnd] = TrackDirection.Ahead;
                    }
                    newRoute.Add(thisElement);
                    prevSection = Math.Abs(sectionIndex);
                    tempSectionsIndex++;
                }

                // Check if we are returning to original route
                int lastAlternativeSectionIndex = TCRoute.TCRouteSubpaths[TCRoute.OriginalSubpath].GetRouteIndex(newRoute[newRoute.Count - 1].TrackCircuitSection.Index, 0);
                if (lastAlternativeSectionIndex != -1)
                {
                    // continued path
                    var thisRoute = TCRoute.TCRouteSubpaths[TCRoute.OriginalSubpath];
                    for (int iElement = lastAlternativeSectionIndex + 1; iElement < thisRoute.Count; iElement++)
                    {
                        newRoute.Add(thisRoute[iElement]);
                    }

                    if (TCRoute.ActiveSubPath != TCRoute.OriginalSubpath)
                    {
                        TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath] = null;
                        TCRoute.ReversalInfo[TCRoute.ActiveSubPath] = null;
                        TCRoute.LoopEnd.RemoveAt(TCRoute.ActiveSubPath);
                    }
                    TCRoute.ActiveSubPath = TCRoute.OriginalSubpath;
                    TCRoute.OriginalSubpath = -1;

                    // readjust item indexes
                    // Reindexes ReversalInfo items
                    var countDifference = newRoute.Count - ValidRoute[0].Count;
                    if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex >= 0)
                        TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex = TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex + countDifference;
                    if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex >= 0)
                        TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex = TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex + countDifference;

                    TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath] = newRoute;

                }
                else
                {
                    // put at the end of the subpath list the new route
                    TCRoute.TCRouteSubpaths.Add(newRoute);

                    // TODO add reversalInfo here.
                    TCRoute.ActiveSubPath = TCRoute.TCRouteSubpaths.Count - 1;

                    TCRoute.ReversalInfo.Add(new TrackCircuitReversalInfo());
                    TCRoute.ReversalInfo[TCRoute.ReversalInfo.Count - 1].ReversalIndex = newRoute.Count - 1;
                    TCRoute.ReversalInfo[TCRoute.ReversalInfo.Count - 1].ReversalSectionIndex = newRoute[newRoute.Count - 1].TrackCircuitSection.Index;
                    TrackCircuitSection endSection = newRoute[newRoute.Count - 1].TrackCircuitSection;
                    TCRoute.ReversalInfo[TCRoute.ReversalInfo.Count - 1].ReverseReversalOffset = endSection.Length;
                    TCRoute.LoopEnd.Add(-1);
                }
            }
            // then we pass this route to ValidRoute[0]
            ValidRoute[0] = newRoute;
            // we set the routelistindex of the present position in case it was = -1
            if (PresentPosition[Direction.Forward].RouteListIndex == -1)
                PresentPosition[Direction.Forward].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, PreviousPosition[Direction.Forward].RouteListIndex);

            // reset signal information

            SignalObjectItems.Clear();
            NextSignalObject[0] = null;
            // create new list
            InitializeSignals(true);
            LastReservedSection[0] = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            CheckDeadlock(ValidRoute[0], Number);    // Check deadlock against all other trains
        }

        /// Perform actions linked to distance travelled
        private protected virtual void PerformActions(List<DistanceTravelledItem> actions)
        {
            foreach (DistanceTravelledItem action in actions)
            {
                switch (action)
                {
                    case ClearSectionItem clearSection:
                        ClearOccupiedSection(clearSection);
                        break;
                    case ActivateSpeedLimit activateSpeedLimit:
                        SetPendingSpeedLimit(activateSpeedLimit);
                        break;
                    case AuxActionItem auxAction:
                        int presentTime = Convert.ToInt32(Math.Floor(simulator.ClockTime));
                        auxAction.ProcessAction(this, presentTime);
                        break;
                }
            }
        }

        /// Clear section
        private protected void ClearOccupiedSection(ClearSectionItem sectionInfo)
        {
            TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[sectionInfo.TrackSectionIndex];
            section.ClearOccupied(this, true);
        }

        //================================================================================================//
        /// <summary>
        /// Set pending speed limits
        /// </summary>

        public void SetPendingSpeedLimit(ActivateSpeedLimit speedInfo)
        {
            float prevMaxSpeedMpS = AllowedMaxSpeedMpS;

            if (speedInfo.MaxSpeedMpSSignal > 0)
            {
                allowedMaxSpeedSignalMpS = simulator.TimetableMode ? speedInfo.MaxSpeedMpSSignal : allowedAbsoluteMaxSpeedSignalMpS;
                AllowedMaxSpeedMpS = Math.Min(speedInfo.MaxSpeedMpSSignal, Math.Min(allowedMaxSpeedLimitMpS, allowedMaxTempSpeedLimitMpS));
            }
            if (speedInfo.MaxSpeedMpSLimit > 0)
            {
                allowedMaxSpeedLimitMpS = simulator.TimetableMode ? speedInfo.MaxSpeedMpSLimit : allowedAbsoluteMaxSpeedLimitMpS;
                if (simulator.TimetableMode)
                    AllowedMaxSpeedMpS = speedInfo.MaxSpeedMpSLimit;
                else
                    AllowedMaxSpeedMpS = Math.Min(speedInfo.MaxSpeedMpSLimit, Math.Min(allowedMaxSpeedSignalMpS, allowedMaxTempSpeedLimitMpS));
            }
            if (speedInfo.MaxTempSpeedMpSLimit > 0 && !simulator.TimetableMode)
            {
                allowedMaxTempSpeedLimitMpS = allowedAbsoluteMaxTempSpeedLimitMpS;
                AllowedMaxSpeedMpS = Math.Min(speedInfo.MaxTempSpeedMpSLimit, Math.Min(allowedMaxSpeedSignalMpS, allowedMaxSpeedLimitMpS));
            }
            if (IsActualPlayerTrain && AllowedMaxSpeedMpS > prevMaxSpeedMpS)
            {
                simulator.OnAllowedSpeedRaised(this);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Clear all active items on occupied track
        /// <\summary>

        public void ClearActiveSectionItems()
        {
            ClearSectionItem dummyItem = new ClearSectionItem(0.0f, 0);
            List<DistanceTravelledItem> activeActions = requiredActions.GetActions(99999999f, dummyItem.GetType());
            foreach (DistanceTravelledItem thisAction in activeActions)
            {
                if (thisAction is ClearSectionItem)
                {
                    ClearSectionItem sectionInfo = thisAction as ClearSectionItem;
                    int thisSectionIndex = sectionInfo.TrackSectionIndex;
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];

                    if (!OccupiedTrack.Contains(thisSection))
                    {
                        thisSection.ClearOccupied(this, true);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Forced stop due to problems with other train
        /// <\summary>

        public void ForcedStop(String reason, string otherTrainName, int otherTrainNumber)
        {
            Trace.TraceInformation("Train {0} ({1}) stopped for train {2} ({3}) : {4}",
                    Name, Number, otherTrainName, otherTrainNumber, reason);

            if (simulator.PlayerLocomotive != null && simulator.PlayerLocomotive.Train == this)
            {
                var report = Simulator.Catalog.GetString("Train stopped due to problems with other train: train {0} , reason: {1}", otherTrainNumber, reason);

                if (simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    simulator.Confirmer.Message(ConfirmLevel.Warning, report);

            }

            if (LeadLocomotive != null)
                ((MSTSLocomotive)LeadLocomotive).SetEmergency(true);
        }

        //================================================================================================//
        /// <summary>
        /// Remove train
        /// <\summary>

        public virtual void RemoveTrain()
        {
            RemoveFromTrack();
            ClearDeadlocks();
            simulator.Trains.Remove(this);
        }

        //================================================================================================//
        //
        // Remove train from not-occupied sections only (for reset after uncoupling)
        //

        public void RemoveFromTrackNotOccupied(TrackCircuitPartialPathRoute newSections)
        {
            // clear occupied track

            List<int> clearedSectionIndices = new List<int>();
            TrackCircuitSection[] tempSectionArray = new TrackCircuitSection[OccupiedTrack.Count]; // copy sections as list is cleared by ClearOccupied method
            OccupiedTrack.CopyTo(tempSectionArray);

            for (int iIndex = 0; iIndex < tempSectionArray.Length; iIndex++)
            {
                TrackCircuitSection thisSection = tempSectionArray[iIndex];
                int newRouteIndex = newSections.GetRouteIndex(thisSection.Index, 0);
                if (newRouteIndex < 0)
                {
                    thisSection.ClearOccupied(this, true);
                    clearedSectionIndices.Add(thisSection.Index);
                }
            }

            // clear outstanding clear sections for sections no longer occupied

            foreach (DistanceTravelledItem thisAction in requiredActions)
            {
                if (thisAction is ClearSectionItem)
                {
                    ClearSectionItem thisItem = thisAction as ClearSectionItem;
                    if (clearedSectionIndices.Contains(thisItem.TrackSectionIndex))
                    {
                        TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisItem.TrackSectionIndex];
                        thisSection.ClearOccupied(this, true);
                    }
                }
            }
        }

        //================================================================================================//
        //
        // Remove train (after coupling or when train disappeared in multiplayer)
        //

        public void RemoveFromTrack()
        {
            // check if no reserved sections remain

            int presentIndex = PresentPosition[Direction.Backward].RouteListIndex;

            if (presentIndex >= 0)
            {
                for (int iIndex = presentIndex; iIndex < ValidRoute[0].Count; iIndex++)
                {
                    TrackCircuitRouteElement thisElement = ValidRoute[0][iIndex];
                    TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                    thisSection.RemoveTrain(this, true);
                }
            }

            // for explorer (e.g. in Multiplayer) and manual mode check also backward route

            if (ValidRoute[1] != null && ValidRoute[1].Count > 0)
            {
                for (int iIndex = 0; iIndex < ValidRoute[1].Count; iIndex++)
                {
                    TrackCircuitRouteElement thisElement = ValidRoute[1][iIndex];
                    TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                    thisSection.RemoveTrain(this, true);
                }
            }

            // clear occupied track

            TrackCircuitSection[] tempSectionArray = new TrackCircuitSection[OccupiedTrack.Count]; // copy sections as list is cleared by ClearOccupied method
            OccupiedTrack.CopyTo(tempSectionArray);

            for (int iIndex = 0; iIndex < tempSectionArray.Length; iIndex++)
            {
                TrackCircuitSection thisSection = tempSectionArray[iIndex];
                thisSection.ClearOccupied(this, true);
            }

            // clear last reserved section
            LastReservedSection[0] = -1;
            LastReservedSection[1] = -1;

            // clear outstanding clear sections

            foreach (DistanceTravelledItem thisAction in requiredActions)
            {
                if (thisAction is ClearSectionItem)
                {
                    ClearSectionItem thisItem = thisAction as ClearSectionItem;
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisItem.TrackSectionIndex];
                    thisSection.ClearOccupied(this, true);
                }
            }
        }

        //================================================================================================//
        //
        // Update track actions after coupling
        //

        public void UpdateTrackActionsCoupling(bool couple_to_front)
        {

            // remove train from track - clear all reservations etc.

            RemoveFromTrack();
            ClearDeadlocks();

            // check if new train is freight or not

            CheckFreight();

            // clear all track occupation actions

            ClearSectionItem dummyItem = new ClearSectionItem(0.0f, 0);
            List<DistanceTravelledItem> activeActions = requiredActions.GetActions(99999999f, dummyItem.GetType());
            activeActions.Clear();

            // save existing TCPositions

            TrackCircuitPosition oldPresentPosition = PresentPosition[Direction.Forward];
            TrackCircuitPosition oldRearPosition = PresentPosition[Direction.Backward];

            PresentPosition[Direction.Forward] = new TrackCircuitPosition();
            PresentPosition[Direction.Backward] = new TrackCircuitPosition();

            // create new TCPositions

            TrackNode tn = FrontTDBTraveller.TN;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)FrontTDBTraveller.Direction;

            PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            tn = RearTDBTraveller.TN;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)RearTDBTraveller.Direction;

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);

            PresentPosition[Direction.Forward].DistanceTravelled = DistanceTravelledM;
            PresentPosition[Direction.Backward].DistanceTravelled = oldRearPosition.DistanceTravelled;

            // use difference in position to update existing DistanceTravelled

            float deltaoffset = 0.0f;

            if (couple_to_front)
            {
                float offset_old = oldPresentPosition.Offset;
                float offset_new = PresentPosition[Direction.Forward].Offset;

                if (oldPresentPosition.TrackCircuitSectionIndex == PresentPosition[Direction.Forward].TrackCircuitSectionIndex)
                {
                    deltaoffset = offset_new - offset_old;
                }
                else
                {
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[oldPresentPosition.TrackCircuitSectionIndex];
                    deltaoffset = thisSection.Length - offset_old;
                    deltaoffset += offset_new;

                    for (int iIndex = oldPresentPosition.RouteListIndex + 1; iIndex < PresentPosition[Direction.Forward].RouteListIndex; iIndex++)
                    {
                        thisSection = ValidRoute[0][iIndex].TrackCircuitSection;
                        deltaoffset += thisSection.Length;
                    }
                }
                PresentPosition[Direction.Forward].DistanceTravelled += deltaoffset;
                DistanceTravelledM += deltaoffset;
            }
            else
            {
                float offset_old = oldRearPosition.Offset;
                float offset_new = PresentPosition[Direction.Backward].Offset;

                if (oldRearPosition.TrackCircuitSectionIndex == PresentPosition[Direction.Backward].TrackCircuitSectionIndex)
                {
                    deltaoffset = offset_old - offset_new;
                }
                else
                {
                    deltaoffset = offset_old;
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];
                    deltaoffset += (thisSection.Length - offset_new);

                    for (int iIndex = oldRearPosition.RouteListIndex - 1; iIndex > PresentPosition[Direction.Backward].RouteListIndex; iIndex--)
                    {
                        thisSection = ValidRoute[0][iIndex].TrackCircuitSection;
                        deltaoffset += thisSection.Length;
                    }
                }
                PresentPosition[Direction.Backward].DistanceTravelled -= deltaoffset;
            }

            // Set track sections to occupied - forward direction only
            OccupiedTrack.Clear();
            UpdateOccupancies();

            // add sections to required actions list

            foreach (TrackCircuitSection thisSection in OccupiedTrack)
            {
                float distanceToClear = DistanceTravelledM + thisSection.Length + standardOverlapM;
                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    distanceToClear += Length + junctionOverlapM;
                }

                if (PresentPosition[Direction.Forward].TrackCircuitSectionIndex == thisSection.Index)
                {
                    distanceToClear += Length - PresentPosition[Direction.Forward].Offset;
                }
                else if (PresentPosition[Direction.Backward].TrackCircuitSectionIndex == thisSection.Index)
                {
                    distanceToClear -= PresentPosition[Direction.Backward].Offset;
                }
                else
                {
                    distanceToClear += Length;
                }
                requiredActions.InsertAction(new ClearSectionItem(distanceToClear, thisSection.Index));
            }

            // rebuild list of station stops

            if (StationStops.Count > 0)
            {
                int presentStop = StationStops[0].PlatformReference;
                StationStops.Clear();
                HoldingSignals.Clear();

                BuildStationList(15.0f);

                bool removeStations = false;
                for (int iStation = StationStops.Count - 1; iStation >= 0; iStation--)
                {
                    if (removeStations)
                    {
                        if (StationStops[iStation].ExitSignal >= 0 && HoldingSignals.Contains(StationStops[iStation].ExitSignal))
                        {
                            HoldingSignals.Remove(StationStops[iStation].ExitSignal);
                        }
                        StationStops.RemoveAt(iStation);
                    }

                    if (StationStops[iStation].PlatformReference == presentStop)
                    {
                        removeStations = true;
                    }
                }
            }

            // add present occupied sections to train route to avoid out-of-path detection

            AddTrackSections();

            // reset signals etc.

            SignalObjectItems.Clear();
            NextSignalObject[0] = null;
            NextSignalObject[1] = null;
            LastReservedSection[0] = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            LastReservedSection[1] = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;

            InitializeSignals(true);

            if (TCRoute != null && (ControlMode == TrainControlMode.AutoSignal || ControlMode == TrainControlMode.AutoNode))
            {
                PresentPosition[Direction.Forward].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                PresentPosition[Direction.Backward].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);

                SwitchToNodeControl(PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
                CheckDeadlock(ValidRoute[0], Number);
                TCRoute.SetReversalOffset(Length, simulator.TimetableMode);
            }
            else if (ControlMode == TrainControlMode.Manual)
            {
                // set track occupation

                UpdateSectionStateManual();

                // reset routes and check sections either end of train

                PresentPosition[Direction.Forward].RouteListIndex = -1;
                PresentPosition[Direction.Backward].RouteListIndex = -1;
                PreviousPosition[Direction.Forward].RouteListIndex = -1;

                UpdateManualMode(-1);
            }
            else if (ControlMode == TrainControlMode.Explorer)
            {
                // set track occupation

                UpdateSectionStateExplorer();

                // reset routes and check sections either end of train

                PresentPosition[Direction.Forward].RouteListIndex = -1;
                PresentPosition[Direction.Backward].RouteListIndex = -1;
                PreviousPosition[Direction.Forward].RouteListIndex = -1;

                UpdateExplorerMode(-1);
            }
            else
            {
                signalRef.RequestClearNode(routedForward, ValidRoute[0]);
            }
        }

        //================================================================================================//
        //
        // Update occupancies
        // Update track occupancies after coupling
        //
        public void UpdateOccupancies()
        {
            if (manualTrainRoute != null) manualTrainRoute.Clear();
            manualTrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                PresentPosition[Direction.Backward].Direction, Length, false, true, false);

            foreach (TrackCircuitRouteElement thisElement in manualTrainRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                thisSection.Reserve(routedForward, manualTrainRoute);
                if (!thisSection.CircuitState.OccupiedByThisTrain(this))
                    thisSection.SetOccupied(routedForward);
            }
        }

        //================================================================================================//
        //
        // AddTrackSections
        // Add track sections not present in path to avoid out-of-path detection
        //

        public void AddTrackSections()
        {
            // check if first section in route

            if (ValidRoute[0].GetRouteIndex(OccupiedTrack[0].Index, 0) > 0)
            {
                int lastSectionIndex = OccupiedTrack[0].Index;
                int lastIndex = ValidRoute[0].GetRouteIndex(lastSectionIndex, 0);

                for (int isection = 1; isection <= OccupiedTrack.Count - 1; isection++)
                {
                    int nextSectionIndex = OccupiedTrack[isection].Index;
                    int nextIndex = ValidRoute[0].GetRouteIndex(nextSectionIndex, 0);

                    if (nextIndex < 0) // this section is not in route - if last index = 0, add to start else add to rear
                    {
                        TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[nextSectionIndex];
                        TrackDirection thisDirection = TrackDirection.Ahead;

                        foreach (Location location in EnumExtension.GetValues<Location>())
                        {
                            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
                            {
                                if (thisSection.Pins[direction, location].Link == lastSectionIndex)
                                {
                                    thisDirection = thisSection.Pins[direction, location].Direction;
                                    break;
                                }
                            }
                        }

                        if (lastIndex == 0)
                        {
                            ValidRoute[0].Insert(0, new TrackCircuitRouteElement(OccupiedTrack[isection], thisDirection, lastSectionIndex));
                        }
                        else
                        {
                            ValidRoute[0].Add(new TrackCircuitRouteElement(OccupiedTrack[isection], thisDirection, lastSectionIndex));
                        }
                    }
                    else
                    {
                        lastIndex = nextIndex;
                        lastSectionIndex = nextSectionIndex;
                    }
                }
            }
            // else start from last section
            else
            {
                int otIndex = OccupiedTrack.Count - 1;
                int lastSectionIndex = OccupiedTrack[otIndex].Index;
                int lastIndex = ValidRoute[0].GetRouteIndex(lastSectionIndex, 0);

                for (int isection = otIndex - 1; isection >= 0; isection--)
                {
                    int nextSectionIndex = OccupiedTrack[isection].Index;
                    int nextIndex = ValidRoute[0].GetRouteIndex(nextSectionIndex, 0);

                    if (nextIndex < 0) // this section is not in route - if last index = 0, add to start else add to rear
                    {
                        TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[nextSectionIndex];
                        TrackDirection thisDirection = TrackDirection.Ahead;

                        foreach (Location location in EnumExtension.GetValues<Location>())
                        {
                            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
                            {
                                if (thisSection.Pins[direction, location].Link == lastSectionIndex)
                                {
                                    thisDirection = thisSection.Pins[direction, location].Direction;
                                    break;
                                }
                            }
                        }

                        if (lastIndex == 0)
                        {
                            ValidRoute[0].Insert(0, new TrackCircuitRouteElement(OccupiedTrack[isection], thisDirection, lastSectionIndex));
                        }
                        else
                        {
                            ValidRoute[0].Add(new TrackCircuitRouteElement(OccupiedTrack[isection], thisDirection, lastSectionIndex));
                        }
                    }
                    else
                    {
                        lastIndex = nextIndex;
                        lastSectionIndex = nextSectionIndex;
                    }
                }
            }
        }

        //================================================================================================//
        //
        // Update track details after uncoupling
        //

        public bool UpdateTrackActionsUncoupling(bool originalTrain)
        {
            bool inPath = true;

            if (originalTrain)
            {
                RemoveFromTrack();
                ClearDeadlocks();

                ClearSectionItem dummyItem = new ClearSectionItem(0.0f, 0);
                List<DistanceTravelledItem> activeActions = requiredActions.GetActions(99999999f, dummyItem.GetType());
                activeActions.Clear();
            }

            // create new TCPositions

            TrackNode tn = FrontTDBTraveller.TN;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)FrontTDBTraveller.Direction;

            PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            tn = RearTDBTraveller.TN;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (TrackDirection)RearTDBTraveller.Direction;

            PresentPosition[Direction.Backward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction);

            PresentPosition[Direction.Forward].DistanceTravelled = DistanceTravelledM;
            PresentPosition[Direction.Backward].DistanceTravelled = DistanceTravelledM - Length;

            // Set track sections to occupied

            OccupiedTrack.Clear();

            // build route of sections now occupied
            OccupiedTrack.Clear();
            if (manualTrainRoute != null) manualTrainRoute.Clear();
            manualTrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset,
                PresentPosition[Direction.Backward].Direction, Length, false, true, false);

            TrackCircuitSection thisSection;


            // static train

            if (TrainType == TrainType.Static)
            {

                // clear routes, required actions, traffic details

                ControlMode = TrainControlMode.Undefined;
                if (TCRoute != null)
                {
                    if (TCRoute.TCRouteSubpaths != null) TCRoute.TCRouteSubpaths.Clear();
                    if (TCRoute.TCAlternativePaths != null) TCRoute.TCAlternativePaths.Clear();
                    TCRoute.ActiveAlternativePath = -1;
                }
                if (ValidRoute[0] != null && ValidRoute[0].Count > 0)
                {
                    signalRef.BreakDownRouteList(ValidRoute[0], 0, routedForward);
                    ValidRoute[0].Clear();
                }
                if (ValidRoute[1] != null && ValidRoute[1].Count > 0)
                {
                    signalRef.BreakDownRouteList(ValidRoute[1], 0, routedBackward);
                    ValidRoute[1].Clear();
                }
                requiredActions.Clear();

                if (TrafficService != null)
                    TrafficService.Clear();

                // build dummy route

                thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];
                offset = PresentPosition[Direction.Backward].Offset;

                ValidRoute[0] = SignalEnvironment.BuildTempRoute(this, thisSection.Index, PresentPosition[Direction.Backward].Offset,
                            PresentPosition[Direction.Backward].Direction, Length, true, true, false);

                foreach (TrackCircuitRouteElement thisElement in manualTrainRoute)
                {
                    thisSection = thisElement.TrackCircuitSection;
                    thisSection.SetOccupied(routedForward);
                }

            }

            // player train or AI train

            else
            {

                //<CSComment> InitializeSignals needs this info sometimes, so I repeat lines below here
                if (!IsActualPlayerTrain && (ControlMode == TrainControlMode.AutoSignal || ControlMode == TrainControlMode.AutoNode))
                {
                    while (TCRoute.ActiveSubPath <= TCRoute.TCRouteSubpaths.Count - 1)
                    {
                        PresentPosition[Direction.Forward].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                        PresentPosition[Direction.Backward].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);
                        if (PresentPosition[Direction.Forward].RouteListIndex < 0 || PresentPosition[Direction.Backward].RouteListIndex < 0)
                        {
                            // Try first to change valid route, if there are other subpaths.
                            if (TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1)
                            {
                                ValidRoute[0] = null;
                                TCRoute.ActiveSubPath++;
                                ValidRoute[0] = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];
                            }
                            else
                            {
                                inPath = false;
                                return inPath;
                            }
                        }
                        else
                        {
                            if (PresentPosition[Direction.Forward].Direction != ValidRoute[0][PresentPosition[Direction.Forward].RouteListIndex].Direction)
                            // Train must be reverted
                            {
                                ReverseFormation(false);
                                var tempTCPosition = PresentPosition[Direction.Forward];
                                PresentPosition[Direction.Forward] = PresentPosition[Direction.Backward];
                                PresentPosition[Direction.Backward] = tempTCPosition;
                            }
                            break;
                        }
                    }
                }

                foreach (TrackCircuitRouteElement thisElement in manualTrainRoute)
                {
                    thisSection = thisElement.TrackCircuitSection;
                    thisSection.SetOccupied(routedForward);
                }
                // rebuild list of station stops

                if (StationStops.Count > 0)
                {
                    int presentStop = StationStops[0].PlatformReference;
                    StationStops.Clear();
                    HoldingSignals.Clear();

                    BuildStationList(15.0f);

                    bool removeStations = false;
                    for (int iStation = StationStops.Count - 1; iStation >= 0; iStation--)
                    {
                        if (removeStations)
                        {
                            if (StationStops[iStation].ExitSignal >= 0 && StationStops[iStation].HoldSignal && HoldingSignals.Contains(StationStops[iStation].ExitSignal))
                            {
                                HoldingSignals.Remove(StationStops[iStation].ExitSignal);
                            }
                            StationStops.RemoveAt(iStation);
                        }

                        if (StationStops[iStation].PlatformReference == presentStop)
                        {
                            removeStations = true;
                        }
                    }
                }

                Reinitialize();
            }
            return inPath;
        }

        //================================================================================================//
        //
        // Perform various reinitializations
        //

        public void Reinitialize()
        {
            // reset signals etc.

            SignalObjectItems.Clear();
            NextSignalObject[0] = null;
            NextSignalObject[1] = null;
            LastReservedSection[0] = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            LastReservedSection[1] = PresentPosition[Direction.Backward].TrackCircuitSectionIndex;


            InitializeSignals(true);

            if (ControlMode == TrainControlMode.AutoSignal || ControlMode == TrainControlMode.AutoNode)
            {
                PresentPosition[Direction.Forward].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                PresentPosition[Direction.Backward].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, 0);

                CheckDeadlock(ValidRoute[0], Number);
                SwitchToNodeControl(PresentPosition[Direction.Forward].TrackCircuitSectionIndex);
                TCRoute.SetReversalOffset(Length, simulator.TimetableMode);
            }
            else if (ControlMode == TrainControlMode.Manual)
            {
                // set track occupation

                UpdateSectionStateManual();

                // reset routes and check sections either end of train

                PresentPosition[Direction.Forward].RouteListIndex = -1;
                PresentPosition[Direction.Backward].RouteListIndex = -1;
                PreviousPosition[Direction.Forward].RouteListIndex = -1;

                UpdateManualMode(-1);
            }
            else if (ControlMode == TrainControlMode.Explorer)
            {
                // set track occupation

                UpdateSectionStateExplorer();

                // reset routes and check sections either end of train

                PresentPosition[Direction.Forward].RouteListIndex = -1;
                PresentPosition[Direction.Backward].RouteListIndex = -1;
                PreviousPosition[Direction.Forward].RouteListIndex = -1;

                UpdateExplorerMode(-1);
            }
            else
            {
                CheckDeadlock(ValidRoute[0], Number);
                signalRef.RequestClearNode(routedForward, ValidRoute[0]);
            }
        }

        //================================================================================================//
        //
        // Temporarily remove from track to allow decoupled train to set occupied sections
        //

        public void TemporarilyRemoveFromTrack()
        {
            RemoveFromTrack();
            ClearDeadlocks();
            ClearSectionItem dummyItem = new ClearSectionItem(0.0f, 0);
            List<DistanceTravelledItem> activeActions = requiredActions.GetActions(99999999f, dummyItem.GetType());
            activeActions.Clear();
        }

        //================================================================================================//
        //
        // Goes to next active subpath
        //
        public void IncrementSubpath(Train thisTrain)
        {
            if (thisTrain.TCRoute.ActiveSubPath < thisTrain.TCRoute.TCRouteSubpaths.Count - 1)
            {
                thisTrain.TCRoute.ActiveSubPath++;
                thisTrain.ValidRoute[0] = thisTrain.TCRoute.TCRouteSubpaths[thisTrain.TCRoute.ActiveSubPath];
            }
        }

        //================================================================================================//
        //
        // Get end of common section
        //

        static int EndCommonSection(int thisIndex, TrackCircuitPartialPathRoute thisRoute, TrackCircuitPartialPathRoute otherRoute)
        {
            int firstSection = thisRoute[thisIndex].TrackCircuitSection.Index;

            int thisTrainSection = firstSection;
            int otherTrainSection = firstSection;

            int thisTrainIndex = thisIndex;
            int otherTrainIndex = otherRoute.GetRouteIndex(firstSection, 0);

            while (thisTrainSection == otherTrainSection && thisTrainIndex < (thisRoute.Count - 1) && otherTrainIndex > 0)
            {
                thisTrainIndex++;
                otherTrainIndex--;
                thisTrainSection = thisRoute[thisTrainIndex].TrackCircuitSection.Index;
                otherTrainSection = otherRoute[otherTrainIndex].TrackCircuitSection.Index;
            }

            return (thisTrainIndex);
        }

        //================================================================================================//
        /// <summary>
        /// Create station stop list
        /// <\summary>

        public void BuildStationList(float clearingDistanceM)
        {
            if (TrafficService == null)
                return;   // no traffic definition

            // loop through traffic points

            int beginActiveSubroute = 0;
            int activeSubrouteNodeIndex = 0;

            foreach (ServiceTrafficItem thisItem in TrafficService)
            {
                bool validStop =
                    CreateStationStop(thisItem.PlatformStartID, thisItem.ArrivalTime, thisItem.DepartTime, clearingDistanceM,
                    ref beginActiveSubroute, ref activeSubrouteNodeIndex);
                if (!validStop)
                {
                    Trace.TraceInformation("Train {0} Service {1} : cannot find platform {2}",
                        Number.ToString(), Name, thisItem.PlatformStartID.ToString());
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Create station stop list
        /// <\summary>

        public bool CreateStationStop(int platformStartID, int arrivalTime, int departTime, float clearingDistanceM,
            ref int beginActiveSubroute, ref int activeSubrouteNodeIndex)
        {
            int platformIndex;
            int lastRouteIndex = 0;
            int activeSubroute = beginActiveSubroute;
            bool terminalStation = false;

            TrackCircuitPartialPathRoute thisRoute = TCRoute.TCRouteSubpaths[activeSubroute];

            // get platform details

            if (signalRef.PlatformXRefList.TryGetValue(platformStartID, out platformIndex))
            {
                PlatformDetails thisPlatform = signalRef.PlatformDetailsList[platformIndex];
                int sectionIndex = thisPlatform.TCSectionIndex[0];
                int routeIndex = thisRoute.GetRouteIndex(sectionIndex, activeSubrouteNodeIndex);
                // No backwards!
                if (routeIndex >= 0 && StationStops.Count > 0 && StationStops[StationStops.Count - 1].RouteIndex == routeIndex
                    && StationStops[StationStops.Count - 1].SubrouteIndex == activeSubroute
                    && StationStops[StationStops.Count - 1].PlatformItem.TrackCircuitOffset[Location.FarEnd, (TrackDirection)thisRoute[routeIndex].Direction] >= thisPlatform.TrackCircuitOffset[Location.FarEnd, (TrackDirection)thisRoute[routeIndex].Direction])
                {
                    if (activeSubrouteNodeIndex < thisRoute.Count - 1) activeSubrouteNodeIndex++;
                    else if (activeSubroute < (TCRoute.TCRouteSubpaths.Count - 1))
                    {
                        activeSubroute++;
                        activeSubrouteNodeIndex = 0;
                        thisRoute = TCRoute.TCRouteSubpaths[activeSubroute];
                    }
                    else
                    {
                        Trace.TraceWarning("Train {0} Service {1} : platform {2} not in correct sequence",
                            Number.ToString(), Name, platformStartID.ToString());
                        return false;
                    }
                    routeIndex = thisRoute.GetRouteIndex(sectionIndex, activeSubrouteNodeIndex);
                }

                if (!simulator.TimetableMode && routeIndex == thisRoute.Count - 1 && TCRoute.ReversalInfo[activeSubroute].Valid)
                {
                    // Check if station beyond reversal point
                    if (TCRoute.ReversalInfo[activeSubroute].ReverseReversalOffset < thisPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)thisRoute[routeIndex].Direction])
                        routeIndex = -1;
                }


                // if first section not found in route, try last

                if (routeIndex < 0)
                {
                    sectionIndex = thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                    routeIndex = thisRoute.GetRouteIndex(sectionIndex, activeSubrouteNodeIndex);
                    if (!simulator.TimetableMode && routeIndex == thisRoute.Count - 1 && TCRoute.ReversalInfo[activeSubroute].Valid)
                    {
                        // Check if station beyond reversal point
                        if (TCRoute.ReversalInfo[activeSubroute].ReverseReversalOffset < thisPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)thisRoute[routeIndex].Direction])
                        {
                            routeIndex = -1;
                            // jump next subpath, because station stop can't be there
                            activeSubroute++;
                            activeSubrouteNodeIndex = 0;
                        }
                    }
                }

                // if neither section found - try next subroute - keep trying till found or out of subroutes

                while (routeIndex < 0 && activeSubroute < (TCRoute.TCRouteSubpaths.Count - 1))
                {
                    activeSubroute++;
                    activeSubrouteNodeIndex = 0;
                    thisRoute = TCRoute.TCRouteSubpaths[activeSubroute];
                    routeIndex = thisRoute.GetRouteIndex(sectionIndex, activeSubrouteNodeIndex);
                    if (!simulator.TimetableMode && routeIndex == thisRoute.Count - 1 && TCRoute.ReversalInfo[activeSubroute].Valid)
                    {
                        // Check if station beyond reversal point
                        if (TCRoute.ReversalInfo[activeSubroute].ReverseReversalOffset < thisPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)thisRoute[routeIndex].Direction])
                            routeIndex = -1;
                    }
                    // if first section not found in route, try last

                    if (routeIndex < 0)
                    {
                        sectionIndex = thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                        routeIndex = thisRoute.GetRouteIndex(sectionIndex, activeSubrouteNodeIndex);
                        if (!simulator.TimetableMode && routeIndex == thisRoute.Count - 1 && TCRoute.ReversalInfo[activeSubroute].Valid)
                        {
                            // Check if station beyond reversal point
                            var direction = thisRoute[routeIndex].Direction;
                            if (TCRoute.ReversalInfo[activeSubroute].ReverseReversalOffset < thisPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)direction])
                            {
                                routeIndex = -1;
                                // jump next subpath, because station stop can't be there
                                activeSubroute++;
                                activeSubrouteNodeIndex = 0;
                            }
                        }
                    }
                }

                // if neither section found - platform is not on route - skip

                if (routeIndex < 0)
                {
                    Trace.TraceWarning("Train {0} Service {1} : platform {2} is not on route",
                            Number.ToString(), Name, platformStartID.ToString());
                    return (false);
                }
                else
                {
                    activeSubrouteNodeIndex = routeIndex;
                    beginActiveSubroute = activeSubroute;
                }

                // determine end stop position depending on direction

                TrackCircuitRouteElement thisElement = thisRoute[routeIndex];

                int endSectionIndex = thisElement.Direction == 0 ?
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1] :
                    thisPlatform.TCSectionIndex[0];
                int beginSectionIndex = thisElement.Direction == 0 ?
                    thisPlatform.TCSectionIndex[0] :
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];

                float endOffset = thisPlatform.TrackCircuitOffset[Location.FarEnd, (TrackDirection)thisElement.Direction];
                float beginOffset = thisPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)thisElement.Direction];

                float deltaLength = thisPlatform.Length - Length; // platform length - train length

                TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[endSectionIndex];


                int firstRouteIndex = thisRoute.GetRouteIndex(beginSectionIndex, 0);
                if (firstRouteIndex < 0)
                    firstRouteIndex = routeIndex;
                lastRouteIndex = thisRoute.GetRouteIndex(endSectionIndex, 0);
                if (lastRouteIndex < 0)
                    lastRouteIndex = routeIndex;

                // if train too long : search back for platform with same name

                float fullLength = thisPlatform.Length;

                if (deltaLength < 0)
                {
                    float actualBegin = beginOffset;

                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[beginSectionIndex];

                    // Other platforms in same section

                    if (thisSection.PlatformIndices.Count > 1)
                    {
                        foreach (int nextIndex in thisSection.PlatformIndices)
                        {
                            if (nextIndex != platformIndex)
                            {
                                PlatformDetails otherPlatform = signalRef.PlatformDetailsList[nextIndex];
                                if (String.Compare(otherPlatform.Name, thisPlatform.Name) == 0)
                                {
                                    int otherSectionIndex = thisElement.Direction == 0 ?
                                        otherPlatform.TCSectionIndex[0] :
                                        otherPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                                    if (otherSectionIndex == beginSectionIndex)
                                    {
                                        if (otherPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)thisElement.Direction] < actualBegin)
                                        {
                                            actualBegin = otherPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)thisElement.Direction];
                                            fullLength = endOffset - actualBegin;
                                        }
                                    }
                                    else
                                    {
                                        int addRouteIndex = thisRoute.GetRouteIndex(otherSectionIndex, 0);
                                        float addOffset = otherPlatform.TrackCircuitOffset[Location.FarEnd, (TrackDirection)(thisElement.Direction == 0 ? 1 : 0)];
                                        // offset of begin in other direction is length of available track

                                        if (lastRouteIndex > 0)
                                        {
                                            float thisLength =
                                                thisRoute.GetDistanceAlongRoute(addRouteIndex, addOffset, lastRouteIndex, endOffset, true);
                                            if (thisLength > fullLength)
                                                fullLength = thisLength;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    deltaLength = fullLength - Length;
                }

                // search back along route

                if (deltaLength < 0)
                {
                    float distance = fullLength + beginOffset;
                    bool platformFound = false;

                    for (int iIndex = firstRouteIndex - 1;
                                iIndex >= 0 && distance < 500f && platformFound;
                                iIndex--)
                    {
                        TrackCircuitSection nextSection = thisRoute[iIndex].TrackCircuitSection;

                        foreach (int otherPlatformIndex in nextSection.PlatformIndices)
                        {
                            PlatformDetails otherPlatform = signalRef.PlatformDetailsList[otherPlatformIndex];
                            if (String.Compare(otherPlatform.Name, thisPlatform.Name) == 0)
                            {
                                fullLength = otherPlatform.Length + distance;
                                // we miss a little bit (offset) - that's because we don't know direction of other platform
                                platformFound = true; // only check for one more
                            }
                        }
                        distance += nextSection.Length;
                    }

                    deltaLength = fullLength - Length;
                }

                // check whether terminal station or not
                TrackCircuitPartialPathRoute routeToEndOfTrack = SignalEnvironment.BuildTempRoute(this, endSectionIndex, endOffset, thisElement.Direction, 30, true, true, false);
                if (routeToEndOfTrack.Count > 0)
                {
                    TrackCircuitSection thisSection = routeToEndOfTrack[routeToEndOfTrack.Count - 1].TrackCircuitSection;
                    if (thisSection.CircuitType == TrackCircuitType.EndOfTrack)
                    {
                        terminalStation = true;
                        foreach (TrackCircuitRouteElement tcElement in routeToEndOfTrack)
                        {
                            thisSection = tcElement.TrackCircuitSection;
                            if (thisSection.CircuitType == TrackCircuitType.Junction)
                            {
                                terminalStation = false;
                                break;
                            }
                        }
                    }
                }


                // determine stop position
                float stopOffset = endOffset - (0.5f * deltaLength);
                if (terminalStation && deltaLength > 0 && !simulator.TimetableMode)
                    stopOffset = endOffset - 1;

                // beyond section : check for route validity (may not exceed route)

                if (stopOffset > endSection.Length)
                {
                    float addOffset = stopOffset - endSection.Length;
                    float overlap = 0f;

                    for (int iIndex = lastRouteIndex; iIndex < thisRoute.Count && overlap < addOffset; iIndex++)
                    {
                        TrackCircuitSection nextSection = thisRoute[iIndex].TrackCircuitSection;
                        overlap += nextSection.Length;
                    }

                    if (overlap < stopOffset)
                        stopOffset = overlap;
                }

                // check if stop offset beyond end signal - do not hold at signal

                int EndSignal = -1;
                bool HoldSignal = false;
                bool NoWaitSignal = false;
                bool NoClaimAllowed = false;

                // check if train is to reverse in platform
                // if so, set signal at other end as hold signal

                TrackDirection useDirection = thisElement.Direction;
                bool inDirection = true;

                if (TCRoute.ReversalInfo[activeSubroute].Valid)
                {
                    TrackCircuitReversalInfo thisReversal = TCRoute.ReversalInfo[activeSubroute];
                    int reversalIndex = thisReversal.SignalUsed ? thisReversal.LastSignalIndex : thisReversal.LastDivergeIndex;
                    if (reversalIndex >= 0 && reversalIndex <= lastRouteIndex &&
                        (CheckVicinityOfPlatformToReversalPoint(thisPlatform.TrackCircuitOffset[Location.FarEnd, (TrackDirection)thisElement.Direction], activeSubrouteNodeIndex, activeSubroute) || simulator.TimetableMode)
                        && !(reversalIndex == lastRouteIndex && thisReversal.ReverseReversalOffset - 50.0 > thisPlatform.TrackCircuitOffset[Location.FarEnd, (TrackDirection)thisElement.Direction])) // reversal point is this section or earlier
                    {
                        useDirection = useDirection.Next();
                        inDirection = false;
                    }
                }

                // check for end signal

                if (thisPlatform.EndSignals[useDirection] >= 0)
                {
                    EndSignal = thisPlatform.EndSignals[useDirection];

                    // stop location is in front of signal
                    if (inDirection)
                    {
                        if (thisPlatform.DistanceToSignals[useDirection] > (stopOffset - endOffset))
                        {
                            HoldSignal = true;

                            if ((thisPlatform.DistanceToSignals[useDirection] + (endOffset - stopOffset)) < clearingDistanceM)
                            {
                                stopOffset = endOffset + thisPlatform.DistanceToSignals[useDirection] - clearingDistanceM - 1.0f;
                            }
                        }
                        // at terminal station we will stop just in front of signal
                        else if (terminalStation && deltaLength <= 0 && !simulator.TimetableMode)
                        {
                            HoldSignal = true;
                            stopOffset = endOffset + thisPlatform.DistanceToSignals[useDirection] - 3.0f;
                        }
                        // if most of train fits in platform then stop at signal
                        else if ((thisPlatform.DistanceToSignals[useDirection] - clearingDistanceM + thisPlatform.Length) >
                                      (0.6 * Length))
                        {
                            HoldSignal = true;
                            stopOffset = endOffset + thisPlatform.DistanceToSignals[useDirection] - clearingDistanceM - 1.0f;
                            // set 1m earlier to give priority to station stop over signal
                        }
                        // train does not fit in platform - reset exit signal
                        else
                        {
                            EndSignal = -1;
                        }
                    }
                    else
                    // end of train is beyond signal
                    {
                        TrackDirection oldUseDirection = useDirection.Next();
                        if (thisPlatform.EndSignals[oldUseDirection] >= 0 && terminalStation && deltaLength <= 0 && !simulator.TimetableMode)
                        {
                            // check also the back of train after reverse
                            stopOffset = endOffset + thisPlatform.DistanceToSignals[oldUseDirection] - 3.0f;
                        }
                        if ((beginOffset - thisPlatform.DistanceToSignals[useDirection]) < (stopOffset - Length))
                        {
                            HoldSignal = true;

                            if ((stopOffset - Length - beginOffset + thisPlatform.DistanceToSignals[useDirection]) < clearingDistanceM)
                            {
                                if (!(terminalStation && deltaLength > 0 && !simulator.TimetableMode))
                                    stopOffset = beginOffset - thisPlatform.DistanceToSignals[useDirection] + Length + clearingDistanceM + 1.0f;
                            }
                        }
                        // if most of train fits in platform then stop at signal
                        else if ((thisPlatform.DistanceToSignals[useDirection] - clearingDistanceM + thisPlatform.Length) >
                                      (0.6 * Length))
                        {
                            // set 1m earlier to give priority to station stop over signal
                            if (!(terminalStation && deltaLength > 0 && !simulator.TimetableMode))
                                stopOffset = beginOffset - thisPlatform.DistanceToSignals[useDirection] + Length + clearingDistanceM + 1.0f;

                            // check if stop is clear of end signal (if any)
                            if (thisPlatform.EndSignals[(TrackDirection)thisElement.Direction] != -1)
                            {
                                if (stopOffset < (endOffset + thisPlatform.DistanceToSignals[(TrackDirection)thisElement.Direction]))
                                {
                                    HoldSignal = true; // if train fits between signals
                                }
                                else
                                {
                                    if (!(terminalStation && deltaLength > 0 && !simulator.TimetableMode))
                                        stopOffset = endOffset + thisPlatform.DistanceToSignals[(TrackDirection)thisElement.Direction] - 1.0f; // stop at end signal
                                }
                            }
                        }
                        // train does not fit in platform - reset exit signal
                        else
                        {
                            EndSignal = -1;
                        }
                    }
                }

                if (simulator.Settings.NoForcedRedAtStationStops)
                {
                    // We don't want reds at exit signal in this case
                    HoldSignal = false;
                }

                // build and add station stop

                TrackCircuitRouteElement lastElement = thisRoute[lastRouteIndex];

                StationStop thisStation = new StationStop(
                        platformStartID,
                        thisPlatform,
                        activeSubroute,
                        lastRouteIndex,
                        lastElement.TrackCircuitSection.Index,
                        thisElement.Direction,
                        EndSignal,
                        HoldSignal,
                        NoWaitSignal,
                        NoClaimAllowed,
                        stopOffset,
                        arrivalTime,
                        departTime,
                        false,
                        null,
                        null,
                        null,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false,
                        StationStopType.Station);

                StationStops.Add(thisStation);

                //<CSComment> should this be reused?

                // 
                //
                //                    // if station has hold signal and this signal is the same as the exit signal for previous station, remove the exit signal from the previous station
                //
                //                    if (HoldSignal && StationStops.Count > 1)
                //                    {
                //                        if (EndSignal == StationStops[StationStops.Count - 2].ExitSignal && StationStops[StationStops.Count - 2].HoldSignal)
                //                        {
                //                            StationStops[StationStops.Count - 2].HoldSignal = false;
                //                            StationStops[StationStops.Count - 2].ExitSignal = -1;
                //                            if (HoldingSignals.Contains(EndSignal))
                //                            {
                //                                HoldingSignals.Remove(EndSignal);
                //                            }
                //                        }
                //                    }


                // add signal to list of hold signals

                if (HoldSignal)
                {
                    HoldingSignals.Add(EndSignal);
                }
            }
            else
            {
                return (false);
            }
            return (true);
        }

        //================================================================================================//
        /// <summary>
        /// Check whether train is at Platform
        /// returns true if yes
        /// </summary>

        public bool IsAtPlatform()
        {
            // build list of occupied section
            bool atStation = false;
            int frontIndex = PresentPosition[Direction.Forward].RouteListIndex;
            int rearIndex = PresentPosition[Direction.Backward].RouteListIndex;
            List<int> occupiedSections = new List<int>();

            // check valid positions
            if (frontIndex < 0 && rearIndex < 0) // not on route so cannot be in station
            {
                return atStation; // no further actions possible
            }

            // correct position if either end is off route
            if (frontIndex < 0) frontIndex = rearIndex;
            if (rearIndex < 0) rearIndex = frontIndex;

            // set start and stop in correct order
            int startIndex = frontIndex < rearIndex ? frontIndex : rearIndex;
            int stopIndex = frontIndex < rearIndex ? rearIndex : frontIndex;

            for (int iIndex = startIndex; iIndex <= stopIndex; iIndex++)
            {
                occupiedSections.Add(ValidRoute[0][iIndex].TrackCircuitSection.Index);
            }

            // check if any platform section is in list of occupied sections - if so, we're in the station
            foreach (int sectionIndex in StationStops[0].PlatformItem.TCSectionIndex)
            {
                if (occupiedSections.Contains(sectionIndex))
                {
                    // TODO : check offset within section
                    atStation = true;
                    break;
                }
            }
            return atStation;
        }

        //================================================================================================//
        /// <summary>
        /// Check whether train has missed platform
        /// returns true if yes
        /// </summary>

        public bool IsMissedPlatform(float thresholdDistance)
        {
            // check if station missed

            int stationRouteIndex = ValidRoute[0].GetRouteIndex(StationStops[0].TrackCircuitSectionIndex, 0);

            if (StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath)
            {
                if (stationRouteIndex < 0)
                {
                    return true;
                }
                else if (stationRouteIndex <= PresentPosition[Direction.Backward].RouteListIndex)
                {
                    var platformSection = TrackCircuitSection.TrackCircuitList[StationStops[0].TrackCircuitSectionIndex];
                    var platformReverseStopOffset = platformSection.Length - StationStops[0].StopOffset;
                    return ValidRoute[0].GetDistanceAlongRoute(stationRouteIndex, platformReverseStopOffset, PresentPosition[Direction.Backward].RouteListIndex, PresentPosition[Direction.Backward].Offset, true) > thresholdDistance;
                }
            }
            return false;
        }

        //================================================================================================//
        /// <summary>
        /// Check vicinity of reversal point to Platform
        /// returns false if distance greater than preset value 
        /// </summary>

        public bool CheckVicinityOfPlatformToReversalPoint(float tcOffset, int routeListIndex, int activeSubpath)
        {
            float Threshold = 100.0f;
            float lengthToGoM = -tcOffset;
            TrackCircuitSection thisSection;
            if (routeListIndex == -1)
            {
                Trace.TraceWarning("Train {0} service {1}, platform off path; reversal point considered remote", Number, Name);
                return false;
            }
            int reversalRouteIndex = TCRoute.TCRouteSubpaths[activeSubpath].GetRouteIndex(TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex, routeListIndex);
            if (reversalRouteIndex == -1)
            {
                Trace.TraceWarning("Train {0} service {1}, reversal or end point off path; reversal point considered remote", Number, Name);
                return false;
            }
            if (routeListIndex <= reversalRouteIndex)
            {
                for (int iElement = routeListIndex; iElement < TCRoute.TCRouteSubpaths[activeSubpath].Count; iElement++)
                {
                    thisSection = TCRoute.TCRouteSubpaths[activeSubpath][iElement].TrackCircuitSection;
                    if (thisSection.Index == TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex)
                    {
                        break;
                    }
                    else
                    {
                        lengthToGoM += thisSection.Length;
                        if (lengthToGoM > Threshold) return false;
                    }
                }
                return lengthToGoM + TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReverseReversalOffset < Threshold;
            }
            else
                // platform is beyond reversal point
                return true;
        }

        //================================================================================================//
        /// <summary>
        /// in a certain % of cases depending from randomization level returns a 0 delay
        /// in the remainder of cases computes a randomized delay using a single-sided pseudo-gaussian distribution
        /// following Daniel Howard's suggestion here https://stackoverflow.com/questions/218060/random-gaussian-variables
        /// Parameters: 
        /// maxDelay maximum added random delay (may be seconds or minutes)
        /// </summary>Ac

        public int RandomizedDelayWithThreshold(int maxAddedDelay)
        {
            if (DateTime.UtcNow.Millisecond % 10 < 6 - simulator.Settings.ActRandomizationLevel) return 0;
            return (int)(Simulator.Random.Next(0, (int)(Simulator.Resolution * Simulator.Random.NextDouble()) + 1) / Simulator.Resolution * maxAddedDelay);
        }

        //================================================================================================//
        /// <summary>
        /// Computes a randomized delay using a single-sided pseudo-gaussian distribution
        /// following Daniel Howard's suggestion here https://stackoverflow.com/questions/218060/random-gaussian-variables
        /// Parameters: 
        /// maxDelay maximum added random delay (may be seconds or minutes)
        /// </summary>

        public int RandomizedDelay(int maxAddedDelay)
        {
            return (int)(Simulator.Random.Next(0, (int)(Simulator.Resolution * Simulator.Random.NextDouble()) + 1) / Simulator.Resolution * maxAddedDelay);
        }

        //================================================================================================//
        /// <summary>
        /// Computes a randomized delay for the various types of waiting points.
        /// </summary>

        public int RandomizedWPDelay(ref int randomizedDelay)
        {
            if (randomizedDelay < 30000) // standard WP
            {
                randomizedDelay += RandomizedDelayWithThreshold(15 + 5 * simulator.Settings.ActRandomizationLevel);
            }
            else if (randomizedDelay >= 30000 && randomizedDelay < 40000) // absolute WP
            {
                randomizedDelay += RandomizedDelayWithThreshold(2 + simulator.Settings.ActRandomizationLevel);
                if (randomizedDelay % 100 > 59)
                {
                    randomizedDelay += 40;
                    if ((randomizedDelay / 100) % 100 == 24) randomizedDelay -= 2400;
                }
            }
            else if (randomizedDelay > 40000 && randomizedDelay < 60000) // car detach WP
            {
                var additionalDelay = RandomizedDelayWithThreshold(25);
                if (randomizedDelay % 100 + additionalDelay > 99) randomizedDelay += 99;
                else randomizedDelay += additionalDelay;
            }
            return randomizedDelay;
        }

        //================================================================================================//
        /// <summary>
        /// Convert player traffic list to station list
        /// <\summary>

        public void ConvertPlayerTraffic(List<ServiceTrafficItem> playerList)
        {

            if (playerList == null || playerList.Count == 0)
            {
                return;    // no traffic details
            }

            TrafficService = new ServiceTraffics(0);

            TrafficService.AddRange(playerList);
            BuildStationList(15.0f);  // use 15m. clearing distance
        }

        //================================================================================================//
        /// <summary>
        /// Clear station from list, clear exit signal if required
        /// <\summary>

        public virtual void ClearStation(uint id1, uint id2, bool removeStation)
        {
            int foundStation = -1;
            StationStop thisStation = null;

            for (int iStation = 0; iStation < StationStops.Count && foundStation < 0; iStation++)
            {
                thisStation = StationStops[iStation];
                if (thisStation.SubrouteIndex > TCRoute.ActiveSubPath) break;
                if (thisStation.PlatformReference == id1 ||
                    thisStation.PlatformReference == id2)
                {
                    foundStation = iStation;
                }

                if (thisStation.SubrouteIndex > TCRoute.ActiveSubPath) break; // stop looking if station is in next subpath
            }

            if (foundStation >= 0)
            {
                thisStation = StationStops[foundStation];
                if (thisStation.ExitSignal >= 0)
                {
                    HoldingSignals.Remove(thisStation.ExitSignal);

                    if (ControlMode == TrainControlMode.AutoSignal)
                    {
                        Signal nextSignal = signalRef.Signals[thisStation.ExitSignal];
                        nextSignal.RequestClearSignal(ValidRoute[0], routedForward, 0, false, null);
                    }
                }
            }
            if (removeStation)
            {
                for (int iStation = foundStation; iStation >= 0; iStation--)
                {
                    PreviousStop = StationStops[iStation].CreateCopy();
                    StationStops.RemoveAt(iStation);
                }
            }
        }

        /// <summary>
        /// Create pathless player train out of static train
        /// </summary>

        public void CreatePathlessPlayerTrain()
        {
            TrainType = TrainType.Player;
            IsPathless = true;
            CheckFreight();
            ToggleToManualMode();
            InitializeBrakes();
            InitializeSpeeds();
        }

        /// <summary>
        /// Initializes speeds for pathless player train
        /// </summary>
        ///

        public void InitializeSpeeds()
        {
            allowedMaxSpeedSignalMpS = allowedAbsoluteMaxSpeedSignalMpS;
            allowedMaxSpeedLimitMpS = allowedAbsoluteMaxSpeedLimitMpS;
            allowedMaxTempSpeedLimitMpS = allowedAbsoluteMaxTempSpeedLimitMpS;
            TrainMaxSpeedMpS = Math.Min((float)simulator.TRK.Route.SpeedLimit, ((MSTSLocomotive)simulator.PlayerLocomotive).MaxSpeedMpS);
        }

        /// <summary>
        /// Gets the train name from one CarID; used for remote trains
        /// </summary>
        ///

        public string GetTrainName(string ID)
        {
            int location = ID.LastIndexOf('-');
            if (location < 0) return ID;
            return ID.Substring(0, location - 1);
        }

        //================================================================================================//

        /// <summary>
        /// Create status line
        /// <\summary>
        /// <remarks>
        ///  "Train", "Travelled", "Speed", "Max", "AI mode", "AI data", "Mode", "Auth", "Distance", "Signal", "Distance", "Consist", "Path"
        ///  0   Train: Number with trailing type (F freight, P Passenger)
        ///  1   Travelled: travelled distance so far
        ///  2   Speed: Current speed
        ///  3   Max: Maximum allowed speed
        ///  4   AIMode :
        ///      INI     : AI is in INIT mode
        ///      STC     : AI is static
        ///      STP     : AI is Stopped
        ///      BRK     : AI Brakes
        ///      ACC     : AI do acceleration
        ///      FOL     : AI follows
        ///      RUN     : AI is running
        ///      EOP     : AI approch and of path
        ///      STA     : AI is on Station Stop
        ///      WTP     : AI is on Waiting Point
        ///      STE     : AI is in Stopped Existing state
        ///  5   AI Data :
        ///      000&000     : Throttel & Brake in %
        ///                  : for mode INI, BRK, ACC, FOL, RUN or EOP
        ///      HH:mm:ss    : for mode STA or WTP with actualDepart or DepartTime
        ///                  : for mode STC with Start Time Value
        ///      ..:..:..    : For other case
        ///  6   Mode:
        ///          SIGN or Sdelay: Train in AUTO_SIGNAL, with delay if train delayed
        ///          NODE or Ndelay: Train in AUTO_NODE, with delay if train delayed
        ///          MAN: Train in AUTO_MANUAL
        ///          OOC: Train in OUT_OF_CONTROL
        ///          EXP: Train in EXPLORER
        ///  7   Auth + Distance:    For Player Train
        ///          case OOC:   Distance set to blank
        ///              SPAD:   Signal Passed At Danger
        ///              RSPD:   Rear SPAD
        ///              OOAU:   Out Of Authority
        ///              OOPA:   Out Of Path
        ///              SLPP:   Slipped out Path
        ///              SLPT:   Slipped to End of Track
        ///              OOTR:   To End Of Track
        ///              MASW:   Misaligned Switch
        ///              ....:   Undefined
        ///          case Waiting Point: WAIT, Distance set to Train Number to Wait ????
        ///          case NODE:                      Distance: Blank or
        ///              EOT:    End Of Track
        ///              EOP:    End Of Path
        ///              RSW:    Reserved Switch
        ///              LP:     Loop
        ///              TAH:    Train Ahead
        ///              MXD:    Max Distance        Distance: To End Of Authority
        ///              NOP:    No Path Reserved    Distance: To End Of Authority
        ///              ...:    Undefined
        ///          Other:
        ///              Blank + Blank
        ///  7   Next Action :   For AI Train
        ///              SPDL    :   Speed limit
        ///              SIGL    :   Speed signal
        ///              STOP    :   Signal STOP
        ///              REST    :   Signal RESTRICTED
        ///              EOA     :   End Of Authority
        ///              STAT    :   Station Stop
        ///              TRAH    :   Train Ahead
        ///              EOR     :   End Of Route
        ///              NONE    :   None
        ///  9   Signal + Distance
        ///          Manual or Explorer: Distance set to blank
        ///              First:  Reverse direction
        ///                  G:  Signal at STOP but Permission Granted
        ///                  S:  Signal At STOP
        ///                  P:  Signal at STOP & PROCEED
        ///                  R:  Signal at RESTRICTING
        ///                  A:  Signal at APPROACH 1, 2 or 3
        ///                  C:  Signal at CLEAR 1 or 2
        ///                  -:  Not Defined
        ///              <>
        ///              Second: Forward direction
        ///                  G:  Signal at STOP but Permission Granted
        ///                  S:  Signal At STOP
        ///                  P:  Signal at STOP & PROCEED
        ///                  R:  Signal at RESTRICTING
        ///                  A:  Signal at APPROACH 1, 2 or 3
        ///                  C:  Signal at CLEAR 1 or 2
        ///                  -:  Not Defined
        ///          Other:  Distance is Distance to next Signal
        ///              STOP:   Signal at STOP
        ///              SPRC:   Signal at STOP & PROCEED
        ///              REST:   Signal at RESTRICTING
        ///              APP1:   Signal at APPROACH 1
        ///              APP2:   Signal at APPROACH 2
        ///              APP3:   Signal at APPROACH 3
        ///              CLR1:   Signal at CLEAR 1
        ///              CLR2:   Signal at CLEAR 2
        ///  11  Consist:
        ///          PLAYER:
        ///          REMOTE:
        ///  12  Path:
        ///          not Manual nor Explorer:
        ///              number or ?     :   Id of subpath in valid TCRoute or ? if no valid TCRoute
        ///              =[n]            :   Number of remaining station stops
        ///              {               :   Starting String
        ///              CircuitString   :   List of Circuit (see next)
        ///              }               :   Ending String
        ///              x or blank      :   x if already on TCRoute
        ///          Manual or Explorer:
        ///              CircuitString   :   Backward
        ///              ={  Dir }=      :   Dir is '<' or '>'
        ///              CircuitString   :   Forward
        ///          For AI  :
        ///              Train Name
        ///  
        ///      CircuitString analyse:
        ///          Build string for section information
        ///      returnString +
        ///      CircuitType:
        ///          >   : Junction
        ///          +   : CrossOver
        ///          [   : End of Track direction 1
        ///          ]   : End of Track direction 0
        ///          -   : Default (Track Section)
        ///      Deadlock traps:
        ///          Yes : Ended with *
        ///              Await number    : ^
        ///              Await more      : ~
        ///      Train Occupancy:    + '&' If more than one
        ///          N° of train     : If one train
        ///      If train reservation :
        ///          (
        ///          Train Number
        ///          )
        ///      If signal reserved :
        ///          (S
        ///          Signal Number
        ///          )
        ///      If one or more train claim
        ///          #
        /// <\remarks>
        public String[] GetStatus(bool metric)
        {

            int iColumn = 0;

            string[] statusString = new string[13];

            //  0, "Train"
            statusString[iColumn] = Number.ToString();

            if (Delay.HasValue && Delay.Value.TotalMinutes >= 1)
            {
                statusString[iColumn] = String.Concat(statusString[iColumn], " D");
            }
            else if (IsFreight)
            {
                statusString[iColumn] = String.Concat(statusString[iColumn], " F");
            }
            else
            {
                statusString[iColumn] = String.Concat(statusString[iColumn], " P");
            }
            iColumn++;

            //  1, "Travelled"
            statusString[iColumn] = FormatStrings.FormatDistanceDisplay(DistanceTravelledM, metric);
            iColumn++;
            //  2, "Speed"
            var trainSpeed = TrainType == TrainType.Remote && SpeedMpS != 0 ? targetSpeedMpS : SpeedMpS;
            statusString[iColumn] = FormatStrings.FormatSpeedDisplay(trainSpeed, metric);
            if (Math.Abs(trainSpeed) > Math.Abs(AllowedMaxSpeedMpS)) statusString[iColumn] += "!!!";
            iColumn++;
            //  3, "Max"
            statusString[iColumn] = FormatStrings.FormatSpeedLimit(AllowedMaxSpeedMpS, metric);
            iColumn++;

            //  4, "AI mode"
            statusString[iColumn] = " ";  // for AI trains
            iColumn++;
            //  5, "AI data"
            statusString[iColumn] = " ";  // for AI trains
            iColumn++;

            //  6, "Mode"
            switch (ControlMode)
            {
                case TrainControlMode.AutoSignal:
                    if (Delay.HasValue)
                    {
                        statusString[iColumn] = String.Concat("S +", Delay.Value.TotalMinutes.ToString("00"));
                    }
                    else
                    {
                        statusString[iColumn] = "SIGN";
                    }
                    break;
                case TrainControlMode.AutoNode:
                    if (Delay.HasValue)
                    {
                        statusString[iColumn] = String.Concat("N +", Delay.Value.TotalMinutes.ToString("00"));
                    }
                    else
                    {
                        statusString[iColumn] = "NODE";
                    }
                    break;
                case TrainControlMode.Manual:
                    statusString[iColumn] = "MAN";
                    break;
                case TrainControlMode.OutOfControl:
                    statusString[iColumn] = "OOC";
                    break;
                case TrainControlMode.Explorer:
                    statusString[iColumn] = "EXPL";
                    break;
                case TrainControlMode.TurnTable:
                    statusString[iColumn] = "TURN";
                    break;
                default:
                    statusString[iColumn] = "----";
                    break;
            }

            iColumn++;
            //  7, "Auth"
            if (ControlMode == TrainControlMode.OutOfControl)
            {
                switch (OutOfControlReason)
                {
                    case OutOfControlReason.PassedAtDanger:
                        statusString[iColumn] = "SPAD";
                        break;
                    case OutOfControlReason.RearPassedAtDanger:
                        statusString[iColumn] = "RSPD";
                        break;
                    case OutOfControlReason.OutOfAuthority:
                        statusString[iColumn] = "OOAU";
                        break;
                    case OutOfControlReason.OutOfPath:
                        statusString[iColumn] = "OOPA";
                        break;
                    case OutOfControlReason.SlippedIntoPath:
                        statusString[iColumn] = "SLPP";
                        break;
                    case OutOfControlReason.SlippedToEndOfTrack:
                        statusString[iColumn] = "SLPT";
                        break;
                    case OutOfControlReason.OutOfTrack:
                        statusString[iColumn] = "OOTR";
                        break;
                    case OutOfControlReason.MisalignedSwitch:
                        statusString[iColumn] = "MASW";
                        break;
                    case OutOfControlReason.SlippedIntoTurnTable:
                        statusString[iColumn] = "SLPT";
                        break;
                    default:
                        statusString[iColumn] = "....";
                        break;
                }

                iColumn++;
                //  8, "Distance"
                statusString[iColumn] = " ";
            }

            else if (ControlMode == TrainControlMode.AutoNode)
            {
                switch (EndAuthorityTypes[0])
                {
                    case EndAuthorityType.EndOfTrack:
                        statusString[iColumn] = "EOT";
                        break;
                    case EndAuthorityType.EndOfPath:
                        statusString[iColumn] = "EOP";
                        break;
                    case EndAuthorityType.ReservedSwitch:
                        statusString[iColumn] = "RSW";
                        break;
                    case EndAuthorityType.Loop:
                        statusString[iColumn] = "LP ";
                        break;
                    case EndAuthorityType.TrainAhead:
                        statusString[iColumn] = "TAH";
                        break;
                    case EndAuthorityType.MaxDistance:
                        statusString[iColumn] = "MXD";
                        break;
                    case EndAuthorityType.NoPathReserved:
                        statusString[iColumn] = "NOP";
                        break;
                    default:
                        statusString[iColumn] = "";
                        break;
                }

                iColumn++;
                //  8, "Distance"
                if (EndAuthorityTypes[0] != EndAuthorityType.MaxDistance && EndAuthorityTypes[0] != EndAuthorityType.NoPathReserved)
                {
                    statusString[iColumn] = FormatStrings.FormatDistance(DistanceToEndNodeAuthorityM[0], metric);
                }
                else
                {
                    statusString[iColumn] = " ";
                }
            }
            else
            {
                statusString[iColumn] = " ";
                iColumn++;
                //  8, "Distance"
                statusString[iColumn] = " ";
            }

            iColumn++;
            //  9, "Signal"
            if (ControlMode == TrainControlMode.Manual || ControlMode == TrainControlMode.Explorer)
            {
                // reverse direction
                string firstchar = "-";

                if (NextSignalObject[1] != null)
                {
                    SignalAspectState nextAspect = GetNextSignalAspect(1);
                    if (NextSignalObject[1].EnabledTrain == null || NextSignalObject[1].EnabledTrain.Train != this) nextAspect = SignalAspectState.Stop;  // aspect only valid if signal enabled for this train

                    switch (nextAspect)
                    {
                        case SignalAspectState.Stop:
                            if (NextSignalObject[1].OverridePermission == SignalPermission.Granted)
                            {
                                firstchar = "G";
                            }
                            else
                            {
                                firstchar = "S";
                            }
                            break;
                        case SignalAspectState.Stop_And_Proceed:
                            firstchar = "P";
                            break;
                        case SignalAspectState.Restricting:
                            firstchar = "R";
                            break;
                        case SignalAspectState.Approach_1:
                            firstchar = "A";
                            break;
                        case SignalAspectState.Approach_2:
                            firstchar = "A";
                            break;
                        case SignalAspectState.Approach_3:
                            firstchar = "A";
                            break;
                        case SignalAspectState.Clear_1:
                            firstchar = "C";
                            break;
                        case SignalAspectState.Clear_2:
                            firstchar = "C";
                            break;
                    }
                }

                // forward direction
                string lastchar = "-";

                if (NextSignalObject[0] != null)
                {
                    SignalAspectState nextAspect = GetNextSignalAspect(0);
                    if (NextSignalObject[0].EnabledTrain == null || NextSignalObject[0].EnabledTrain.Train != this) nextAspect = SignalAspectState.Stop;  // aspect only valid if signal enabled for this train

                    switch (nextAspect)
                    {
                        case SignalAspectState.Stop:
                            if (NextSignalObject[0].OverridePermission == SignalPermission.Granted)
                            {
                                lastchar = "G";
                            }
                            else
                            {
                                lastchar = "S";
                            }
                            break;
                        case SignalAspectState.Stop_And_Proceed:
                            lastchar = "P";
                            break;
                        case SignalAspectState.Restricting:
                            lastchar = "R";
                            break;
                        case SignalAspectState.Approach_1:
                            lastchar = "A";
                            break;
                        case SignalAspectState.Approach_2:
                            lastchar = "A";
                            break;
                        case SignalAspectState.Approach_3:
                            lastchar = "A";
                            break;
                        case SignalAspectState.Clear_1:
                            lastchar = "C";
                            break;
                        case SignalAspectState.Clear_2:
                            lastchar = "C";
                            break;
                    }
                }

                statusString[iColumn] = String.Concat(firstchar, "<>", lastchar);
                iColumn++;
                //  9, "Distance"
                statusString[iColumn] = " ";
            }
            else
            {
                if (NextSignalObject[0] != null)
                {
                    SignalAspectState nextAspect = GetNextSignalAspect(0);

                    switch (nextAspect)
                    {
                        case SignalAspectState.Stop:
                            statusString[iColumn] = "STOP";
                            break;
                        case SignalAspectState.Stop_And_Proceed:
                            statusString[iColumn] = "SPRC";
                            break;
                        case SignalAspectState.Restricting:
                            statusString[iColumn] = "REST";
                            break;
                        case SignalAspectState.Approach_1:
                            statusString[iColumn] = "APP1";
                            break;
                        case SignalAspectState.Approach_2:
                            statusString[iColumn] = "APP2";
                            break;
                        case SignalAspectState.Approach_3:
                            statusString[iColumn] = "APP3";
                            break;
                        case SignalAspectState.Clear_1:
                            statusString[iColumn] = "CLR1";
                            break;
                        case SignalAspectState.Clear_2:
                            statusString[iColumn] = "CLR2";
                            break;
                    }

                    iColumn++;
                    //  9, "Distance"
                    if (DistanceToSignal.HasValue)
                    {
                        statusString[iColumn] = FormatStrings.FormatDistance(DistanceToSignal.Value, metric);
                    }
                    else
                    {
                        statusString[iColumn] = "-";
                    }
                }
                else
                {
                    statusString[iColumn] = " ";
                    iColumn++;
                    //  9, "Distance"
                    statusString[iColumn] = " ";
                }
            }

            iColumn++;
            //  10, "Consist"
            statusString[iColumn] = "PLAYER";
            if (!simulator.TimetableMode && this != simulator.OriginalPlayerTrain) statusString[iColumn] = Name.Substring(0, Math.Min(Name.Length, 7));
            if (TrainType == TrainType.Remote)
            {
                var trainName = "";
                if (LeadLocomotive != null) trainName = GetTrainName(LeadLocomotive.CarID);
                else if (Cars != null && Cars.Count > 0) trainName = GetTrainName(Cars[0].CarID);
                else trainName = "REMOTE";
                statusString[iColumn] = trainName.Substring(0, Math.Min(trainName.Length, 7));
            }

            iColumn++;
            //  11, "Path"
            string circuitString = String.Empty;

            if ((ControlMode != TrainControlMode.Manual && ControlMode != TrainControlMode.Explorer) || ValidRoute[1] == null)
            {
                // station stops
                if (StationStops == null || StationStops.Count == 0)
                {
                    circuitString = string.Concat(circuitString, "[ ] ");
                }
                else
                {
                    circuitString = string.Concat(circuitString, "[", StationStops.Count, "] ");
                }

                // route
                if (TCRoute == null)
                {
                    circuitString = string.Concat(circuitString, "?={");
                }
                else
                {
                    circuitString = String.Concat(circuitString, TCRoute.ActiveSubPath.ToString());
                    circuitString = String.Concat(circuitString, "={");
                }

                int startIndex = PresentPosition[Direction.Forward].RouteListIndex;
                if (startIndex < 0)
                {
                    circuitString = String.Concat(circuitString, "<out of route>");
                }
                else
                {
                    for (int iIndex = PresentPosition[Direction.Forward].RouteListIndex; iIndex < ValidRoute[0].Count; iIndex++)
                    {
                        TrackCircuitRouteElement thisElement = ValidRoute[0][iIndex];
                        TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

                        circuitString = BuildSectionString(circuitString, thisSection, 0);

                    }
                }

                circuitString = String.Concat(circuitString, "}");

                if (TCRoute != null && TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1)
                {
                    circuitString = String.Concat(circuitString, "x", (TCRoute.ActiveSubPath + 1).ToString());
                }
                if (TCRoute != null && TCRoute.OriginalSubpath != -1) circuitString += "???";
            }
            else
            {
                // backward path
                string backstring = String.Empty;
                for (int iindex = ValidRoute[1].Count - 1; iindex >= 0; iindex--)
                {
                    TrackCircuitSection thisSection = ValidRoute[1][iindex].TrackCircuitSection;
                    backstring = BuildSectionString(backstring, thisSection, 1);
                }

                if (backstring.Length > 30)
                {
                    backstring = backstring.Substring(backstring.Length - 30);
                    // ensure string starts with section delimiter
                    while (String.Compare(backstring.Substring(0, 1), "-") != 0 &&
                           String.Compare(backstring.Substring(0, 1), "+") != 0 &&
                           String.Compare(backstring.Substring(0, 1), "<") != 0)
                    {
                        backstring = backstring.Substring(1);
                    }

                    circuitString = String.Concat(circuitString, "...");
                }
                circuitString = String.Concat(circuitString, backstring);

                // train indication and direction
                circuitString = String.Concat(circuitString, "={");
                if (MUDirection == MidpointDirection.Reverse)
                {
                    circuitString = String.Concat(circuitString, "<");
                }
                else
                {
                    circuitString = String.Concat(circuitString, ">");
                }
                circuitString = String.Concat(circuitString, "}=");

                // forward path

                string forwardstring = String.Empty;
                for (int iindex = 0; iindex < ValidRoute[0].Count; iindex++)
                {
                    TrackCircuitSection thisSection = ValidRoute[0][iindex].TrackCircuitSection;
                    forwardstring = BuildSectionString(forwardstring, thisSection, 0);
                }
                circuitString = String.Concat(circuitString, forwardstring);
            }

            statusString[iColumn] = String.Copy(circuitString);

            return (statusString);
        }

        //================================================================================================//


        /// <summary>
        ///  Build string for section information
        ///  <c>returnString +
        ///     CircuitType:
        ///         >   : Junction
        ///         +   : CrossOver
        ///         [   : End of Track direction 1
        ///         ]   : End of Track direction 0
        ///     Deadlock traps:
        ///         Yes : Ended with *
        ///             Await number    : ^
        ///             Await more      : ~
        ///     Train Occupancy:    + '&' If more than one
        ///         N° of train     : If one train
        ///     If train reservation :
        ///         (
        ///         Train Number
        ///         )
        ///     If signal reserved :
        ///         (S
        ///         Signal Number
        ///         )
        ///     If one or more train claim
        ///         #</c>
        /// </summary>
        public string BuildSectionString(string thisString, TrackCircuitSection thisSection, int direction)
        {

            string returnString = String.Copy(thisString);

            switch (thisSection.CircuitType)
            {
                case TrackCircuitType.Junction:
                    returnString = String.Concat(returnString, ">");
                    break;
                case TrackCircuitType.Crossover:
                    returnString = String.Concat(returnString, "+");
                    break;
                case TrackCircuitType.EndOfTrack:
                    returnString = direction == 0 ? String.Concat(returnString, "]") : String.Concat(returnString, "[");
                    break;
                default:
                    returnString = String.Concat(returnString, "-");
                    break;
            }

            if (thisSection.DeadlockTraps.ContainsKey(Number))
            {
                if (thisSection.DeadlockAwaited.Contains(Number))
                {
                    returnString = String.Concat(returnString, "^[");
                    List<int> deadlockInfo = thisSection.DeadlockTraps[Number];
                    for (int index = 0; index < deadlockInfo.Count - 2; index++)
                    {
                        returnString = String.Concat(returnString, deadlockInfo[index].ToString(), ",");
                    }
                    returnString = String.Concat(returnString, deadlockInfo.Last().ToString(), "]");
                }
                else if (thisSection.DeadlockAwaited.Count > 0)
                {
                    returnString = String.Concat(returnString, "~");
                }
                returnString = String.Concat(returnString, "*");
            }

            if (thisSection.CircuitState.OccupationState.Count > 0)
            {
                List<TrainRouted> allTrains = thisSection.CircuitState.TrainsOccupying();
                int trainno = allTrains[0].Train.Number;
                returnString = String.Concat(returnString, trainno.ToString());
                if (allTrains.Count > 1)
                {
                    returnString = String.Concat(returnString, "&");
                }
            }

            if (thisSection.CircuitState.TrainReserved != null)
            {
                int trainno = thisSection.CircuitState.TrainReserved.Train.Number;
                returnString = String.Concat(returnString, "(", trainno.ToString(), ")");
            }

            if (thisSection.CircuitState.SignalReserved >= 0)
            {
                returnString = String.Concat(returnString, "(S", thisSection.CircuitState.SignalReserved.ToString(), ")");
            }

            if (thisSection.CircuitState.TrainClaimed.Count > 0)
            {
                returnString = String.Concat(returnString, "#");
            }

            return (returnString);
        }

#if WITH_PATH_DEBUG
        //================================================================================================//
        /// <summary>
        /// Create Path information line
        /// "Train", "Path"
        /// <\summary>

        public String[] GetPathStatus(bool metric)
        {
            int iColumn = 0;

            string[] statusString = new string[5];

            //  "Train"
            statusString[0] = Number.ToString();
            iColumn++;

            //  "Action"
            statusString[1] = "----";
            statusString[2] = "..";
            iColumn = 3;

            string circuitString = String.Empty;
            circuitString = string.Concat(circuitString, "Path: ");


            statusString[iColumn] = String.Copy(circuitString);
            iColumn++;

            return (statusString);

        }
#endif

        //================================================================================================//
        /// <summary>
        /// Add restart times at stations and waiting points
        /// Update the string for 'TextPageDispatcherInfo'.
        /// Modifiy fields 4 and 5
        /// <\summary>

        public String[] AddRestartTime(String[] stateString)
        {
            String[] retString = new String[stateString.Length];
            stateString.CopyTo(retString, 0);

            string movString = "";
            string abString = "";
            DateTime baseDT = new DateTime();
            if (this == simulator.OriginalPlayerTrain)
            {
                if (simulator.ActivityRun != null && simulator.ActivityRun.Current is ActivityTaskPassengerStopAt && ((ActivityTaskPassengerStopAt)simulator.ActivityRun.Current).BoardingS > 0)
                {
                    movString = "STA";
                    DateTime depTime = baseDT.AddSeconds(((ActivityTaskPassengerStopAt)simulator.ActivityRun.Current).BoardingEndS);
                    abString = depTime.ToString("HH:mm:ss");
                }
                else
                   if (Math.Abs(SpeedMpS) <= 0.01 && AuxActionsContainer.specRequiredActions.Count > 0 && AuxActionsContainer.specRequiredActions.First.Value is AuxActSigDelegate &&
                    (AuxActionsContainer.specRequiredActions.First.Value as AuxActSigDelegate).currentMvmtState == AITrain.AI_MOVEMENT_STATE.HANDLE_ACTION)
                {
                    movString = "WTS";
                    DateTime depTime = baseDT.AddSeconds((AuxActionsContainer.specRequiredActions.First.Value as AuxActSigDelegate).ActualDepart);
                    abString = depTime.ToString("HH:mm:ss");
                }
            }
            else if (StationStops.Count > 0 && AtStation)
            {
                movString = "STA";
                if (StationStops[0].ActualDepart > 0)
                {
                    DateTime depTime = baseDT.AddSeconds(StationStops[0].ActualDepart);
                    abString = depTime.ToString("HH:mm:ss");
                }
                else
                {
                    abString = "..:..:..";
                }
            }
            else if (Math.Abs(SpeedMpS) <= 0.01 && (this as AITrain).nextActionInfo is AuxActionWPItem &&
                    (this as AITrain).MovementState == AITrain.AI_MOVEMENT_STATE.HANDLE_ACTION)
            {
                movString = "WTP";
                DateTime depTime = baseDT.AddSeconds(((this as AITrain).nextActionInfo as AuxActionWPItem).ActualDepart);
                abString = depTime.ToString("HH:mm:ss");
            }
            else if (Math.Abs(SpeedMpS) <= 0.01 && AuxActionsContainer.SpecAuxActions.Count > 0 && AuxActionsContainer.SpecAuxActions[0] is AIActionWPRef &&
                (AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).keepIt != null &&
                (AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).keepIt.currentMvmtState == AITrain.AI_MOVEMENT_STATE.HANDLE_ACTION)
            {
                movString = "WTP";
                DateTime depTime = baseDT.AddSeconds((AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).keepIt.ActualDepart);
                abString = depTime.ToString("HH:mm:ss");
            }
            retString[4] = String.Copy(movString);
            retString[5] = String.Copy(abString);

            return (retString);
        }


        //================================================================================================//
        /// <summary>
        /// Create TrackInfoObject for information in TrackMonitor window
        /// </summary>

        public TrainInfo GetTrainInfo()
        {
            TrainInfo result;
            switch (ControlMode)
            {
                case TrainControlMode.AutoNode:
                case TrainControlMode.AutoSignal:
                    result = GetTrainInfoAuto();
                    break;
                case TrainControlMode.Manual:
                case TrainControlMode.Explorer:
                    result = GetTrainInfoManual();
                    break;
                case TrainControlMode.OutOfControl:
                    result = GetTrainInfoOOC();
                    break;
                default:// no state? should not occur, but just set no details at all
                    result = new TrainInfo(ControlMode, Direction.Forward, 0);
                    TrainPathItem dummyItem = new TrainPathItem(EndAuthorityType.NoPathReserved, 0.0f);
                    result.ObjectInfoForward.Add(dummyItem);
                    result.ObjectInfoBackward.Add(dummyItem);
                    break;
            }
            // sort items on increasing distance
            result.ObjectInfoForward.Sort();
            result.ObjectInfoBackward.Sort();

            return (result);
        }

        //================================================================================================//
        /// <summary>
        /// Create TrackInfoObject for information in TrackMonitor window for Auto mode
        /// </summary>

        public TrainInfo GetTrainInfoAuto()
        {
            TrainInfo result = new TrainInfo(ControlMode, MidPointDirectionToDirectionUnset(MUDirection),
                SpeedMpS, ProjectedSpeedMpS, Math.Min(AllowedMaxSpeedMpS, TrainMaxSpeedMpS), simulator.PlayerLocomotive?.CurrentElevationPercent ?? 0,
                simulator.PlayerLocomotive != null ? ((simulator.PlayerLocomotive.Flipped ^ simulator.PlayerLocomotive.GetCabFlipped()) ? Direction.Backward : Direction.Forward) : Direction.Forward, true);

            AddTrainReversalInfo(result, TCRoute.ReversalInfo[TCRoute.ActiveSubPath]);

            // set waiting point
            if (this != simulator.OriginalPlayerTrain)
                AddWaitingPointInfo(result);

            bool maxAuthSet = false;
            // set object items - forward
            if (ControlMode == TrainControlMode.AutoNode)
            {
                result.ObjectInfoForward.Add(new TrainPathItem(EndAuthorityTypes[0], DistanceToEndNodeAuthorityM[0]));
                maxAuthSet = true;
            }

            bool signalProcessed = false;
            foreach (SignalItemInfo signalItem in SignalObjectItems)
            {
                if (signalItem.ItemType == SignalItemType.Signal)
                {
                    TrackMonitorSignalAspect signalAspect = signalItem.SignalDetails.TranslateTMAspect(signalItem.SignalDetails.SignalLR(SignalFunction.Normal));
                    if (signalItem.SignalDetails.EnabledTrain == null || signalItem.SignalDetails.EnabledTrain.Train != this)
                    {
                        signalAspect = TrackMonitorSignalAspect.Stop;
                        result.ObjectInfoForward.Add(new TrainPathItem(signalAspect, signalItem.ActualSpeed, signalItem.DistanceToTrain));
                        signalProcessed = true;
                        break;
                    }
                    result.ObjectInfoForward.Add(new TrainPathItem(signalAspect, signalItem.ActualSpeed, signalItem.DistanceToTrain));
                    signalProcessed = true;
                }
                else if (signalItem.ItemType == SignalItemType.SpeedLimit && signalItem.ActualSpeed > 0)
                {
                    result.ObjectInfoForward.Add(new TrainPathItem(signalItem.ActualSpeed, signalItem.DistanceToTrain, (SpeedItemType)(signalItem.SpeedInfo.LimitedSpeedReduction)));
                }
            }

            if (!signalProcessed && NextSignalObject[0]?.EnabledTrain?.Train == this)
            {
                TrackMonitorSignalAspect signalAspect = NextSignalObject[0].TranslateTMAspect(NextSignalObject[0].SignalLR(SignalFunction.Normal));
                SpeedInfo speedInfo = NextSignalObject[0].SignalSpeed(SignalFunction.Normal);
                float validSpeed = speedInfo == null ? -1 : (IsFreight ? speedInfo.FreightSpeed : speedInfo.PassengerSpeed);

                result.ObjectInfoForward.Add(new TrainPathItem(signalAspect, validSpeed, DistanceToSignal.GetValueOrDefault(0.1f)));
            }

            if (StationStops?.Count > 0 && (!maxAuthSet || StationStops[0].DistanceToTrainM < DistanceToEndNodeAuthorityM[0]) &&
                StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath)
            {
                result.ObjectInfoForward.Add(new TrainPathItem(StationStops[0].DistanceToTrainM, (int)StationStops[0].PlatformItem.Length));
            }


            // Draft to display more station stops
            /*            if (StationStops != null && StationStops.Count > 0)
            {
                for (int iStation = 0; iStation < StationStops.Count; iStation++)
                {
                    if ((!maxAuthSet || StationStops[iStation].DistanceToTrainM <= DistanceToEndNodeAuthorityM[0]) && StationStops[iStation].SubrouteIndex == TCRoute.activeSubpath)
                    {
                        TrainObjectItem nextItem = new TrainObjectItem(StationStops[iStation].DistanceToTrainM, (int)StationStops[iStation].PlatformItem.Length);
                        thisInfo.ObjectInfoForward.Add(nextItem);
                    }
                    else break;
                }
            }*/

            // run along forward path to catch all diverging switches and mileposts
            AddSwitch_MilepostInfo(result, Direction.Forward);

            // set object items - backward

            if (clearanceAtRearM <= 0)
            {
                result.ObjectInfoBackward.Add(new TrainPathItem(EndAuthorityType.NoPathReserved, 0.0f));
            }
            else
            {
                if (rearSignalObject != null)
                {
                    TrackMonitorSignalAspect signalAspect = rearSignalObject.TranslateTMAspect(rearSignalObject.SignalLR(SignalFunction.Normal));
                    result.ObjectInfoBackward.Add(new TrainPathItem(signalAspect, -1.0f, clearanceAtRearM));
                }
                else
                {
                    result.ObjectInfoBackward.Add(new TrainPathItem(EndAuthorityType.EndOfAuthority, clearanceAtRearM));
                }
            }
            return result;
        }

        //================================================================================================//
        /// <summary>
        /// Add all switch and milepost info to TrackMonitorInfo
        /// </summary>
        /// 
        private void AddSwitch_MilepostInfo(TrainInfo trainInfo, Direction direction)
        {
            int routeDirection = (int)direction;
            // run along forward path to catch all diverging switches and mileposts
            var prevMilepostValue = -1f;
            var prevMilepostDistance = -1f;
            if (ValidRoute[routeDirection] != null)
            {
                TrainPathItem thisItem;
                float distanceToTrainM = 0.0f;
                float offset = PresentPosition[direction].Offset;
                TrackCircuitSection firstSection = TrackCircuitSection.TrackCircuitList[PresentPosition[direction].TrackCircuitSectionIndex];
                float sectionStart = routeDirection == 0 ? -offset : offset - firstSection.Length;
                int startRouteIndex = PresentPosition[direction].RouteListIndex;
                if (startRouteIndex < 0) startRouteIndex = ValidRoute[routeDirection].GetRouteIndex(PresentPosition[direction].TrackCircuitSectionIndex, 0);
                if (startRouteIndex >= 0)
                {
                    for (int iRouteElement = startRouteIndex; iRouteElement < ValidRoute[routeDirection].Count && distanceToTrainM < 7000 && sectionStart < 7000; iRouteElement++)
                    {
                        TrackCircuitSection thisSection = ValidRoute[routeDirection][iRouteElement].TrackCircuitSection;
                        TrackDirection sectionDirection = ValidRoute[routeDirection][iRouteElement].Direction;

                        if (thisSection.CircuitType == TrackCircuitType.Junction && (thisSection.Pins[sectionDirection, Location.FarEnd].Link != -1) && sectionStart < 7000)
                        {
                            bool isRightSwitch = true;
                            TrackJunctionNode junctionNode = simulator.TDB.TrackDB.TrackNodes[thisSection.OriginalIndex] as TrackJunctionNode;
                            var isDiverging = false;
                            if ((thisSection.ActivePins[sectionDirection, Location.FarEnd].Link > 0 && thisSection.JunctionDefaultRoute == 0) ||
                                (thisSection.ActivePins[sectionDirection, Location.NearEnd].Link > 0 && thisSection.JunctionDefaultRoute > 0))
                            {
                                // diverging 
                                isDiverging = true;
                                var junctionAngle = junctionNode.GetAngle(simulator.TSectionDat);
                                if (junctionAngle < 0) isRightSwitch = false;
                            }
                            if (isDiverging)
                            {
                                thisItem = new TrainPathItem(isRightSwitch, sectionStart);
                                if (direction == Direction.Forward)
                                    trainInfo.ObjectInfoForward.Add(thisItem);
                                else
                                    trainInfo.ObjectInfoBackward.Add(thisItem);
                            }
                        }

                        if (thisSection.CircuitItems.TrackCircuitMileposts != null)
                        {
                            foreach (TrackCircuitMilepost thisMilepostItem in thisSection.CircuitItems.TrackCircuitMileposts)
                            {
                                Milepost thisMilepost = thisMilepostItem.Milepost;
                                distanceToTrainM = sectionStart + thisMilepostItem.MilepostLocation[sectionDirection == TrackDirection.Reverse ? Location.NearEnd : Location.FarEnd];

                                if (!(distanceToTrainM - prevMilepostDistance < 50 && thisMilepost.Value == prevMilepostValue) && distanceToTrainM > 0 && distanceToTrainM < 7000)
                                {
                                    thisItem = new TrainPathItem(thisMilepost.Value.ToString(), distanceToTrainM);
                                    prevMilepostDistance = distanceToTrainM;
                                    prevMilepostValue = thisMilepost.Value;
                                    if (direction == Direction.Forward)
                                        trainInfo.ObjectInfoForward.Add(thisItem);
                                    else
                                        trainInfo.ObjectInfoBackward.Add(thisItem);
                                }
                            }
                        }
                        sectionStart += thisSection.Length;
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Add reversal info to TrackMonitorInfo
        /// </summary>
        internal virtual void AddTrainReversalInfo(TrainInfo trainInfo, TrackCircuitReversalInfo reversalInfo)
        {
            if (!reversalInfo.Valid && TCRoute.ActiveSubPath == TCRoute.TCRouteSubpaths.Count - 1)
                return;

            int reversalSection = reversalInfo.ReversalSectionIndex;
            if (reversalInfo.LastDivergeIndex >= 0)
            {
                reversalSection = reversalInfo.SignalUsed ? reversalInfo.SignalSectorIndex : reversalInfo.DivergeSectorIndex;
            }

            TrackCircuitSection rearSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];
            float reversalDistanceM = TrackCircuitSection.GetDistanceBetweenObjects(PresentPosition[Direction.Backward].TrackCircuitSectionIndex, PresentPosition[Direction.Backward].Offset, PresentPosition[Direction.Backward].Direction, reversalSection, 0.0f);

            bool reversalEnabled = true;
            TrackCircuitSection frontSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
            reversalDistanceM = Math.Max(reversalDistanceM, TrackCircuitSection.GetDistanceBetweenObjects
                (PresentPosition[Direction.Forward].TrackCircuitSectionIndex, PresentPosition[Direction.Forward].Offset, PresentPosition[Direction.Forward].Direction,
                reversalInfo.ReversalSectionIndex, reversalInfo.ReverseReversalOffset));
            int reversalIndex = reversalInfo.SignalUsed ? reversalInfo.LastSignalIndex : reversalInfo.LastDivergeIndex;
            if (reversalDistanceM > 50f || (PresentPosition[Direction.Backward].RouteListIndex < reversalIndex))
            {
                reversalEnabled = false;
            }
            if (reversalDistanceM > 0)
            {
                trainInfo.ObjectInfoForward.Add(new TrainPathItem(reversalEnabled, reversalDistanceM, reversalInfo.Valid));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Add waiting point info to TrackMonitorInfo
        /// </summary>

        internal void AddWaitingPointInfo(TrainInfo trainInfo)
        {
            if (AuxActionsContainer.SpecAuxActions.Count > 0 && AuxActionsContainer.SpecAuxActions[0] is AIActionWPRef &&
                (AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).SubrouteIndex == TCRoute.ActiveSubPath)
            {
                TrackCircuitSection frontSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
                int thisSectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
                float leftInSectionM = thisSection.Length - PresentPosition[Direction.Forward].Offset;

                // get action route index - if not found, return distances < 0

                int actionIndex0 = PresentPosition[Direction.Forward].RouteListIndex;
                int actionRouteIndex = ValidRoute[0].GetRouteIndex((AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).TCSectionIndex, actionIndex0);
                var wpDistance = ValidRoute[0].GetDistanceAlongRoute(actionIndex0, leftInSectionM, actionRouteIndex, (AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).RequiredDistance, AITrainDirectionForward);
                bool wpEnabled = false;
                if (Math.Abs(SpeedMpS) <= Simulator.MaxStoppedMpS && (((AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).keepIt != null &&
                    (AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).keepIt.currentMvmtState == AITrain.AI_MOVEMENT_STATE.HANDLE_ACTION) ||
                    ((this as AITrain).nextActionInfo is AuxActionWPItem && (this as AITrain).MovementState == AITrain.AI_MOVEMENT_STATE.HANDLE_ACTION))) wpEnabled = true;

                trainInfo.ObjectInfoForward.Add(new TrainPathItem(wpDistance, wpEnabled));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Create TrackInfoObject for information in TrackMonitor window when in Manual mode
        /// </summary>

        private TrainInfo GetTrainInfoManual()
        {
            bool validPath = true;
            if (TCRoute != null && TCRoute.ActiveSubPath >= 0 && TCRoute.TCRouteSubpaths != null && TCRoute.TCRouteSubpaths.Count > TCRoute.ActiveSubPath)
            {
                TrackCircuitPartialPathRoute pathRoute = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];
                validPath = pathRoute.GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0) >= 0;
            }

            TrainInfo result = new TrainInfo(ControlMode, MidPointDirectionToDirectionUnset(MUDirection), SpeedMpS, ProjectedSpeedMpS,
                Math.Min(AllowedMaxSpeedMpS, TrainMaxSpeedMpS), simulator.PlayerLocomotive != null ? simulator.PlayerLocomotive.CurrentElevationPercent : 0,
                (simulator.PlayerLocomotive.Flipped ^ simulator.PlayerLocomotive.GetCabFlipped()) ? Direction.Backward : Direction.Forward, validPath);


            // set forward information

            // set authority
            result.ObjectInfoForward.Add(new TrainPathItem(EndAuthorityTypes[0], DistanceToEndNodeAuthorityM[0]));

            // run along forward path to catch all speedposts and signals
            if (ValidRoute[0] != null)
            {
                float distanceToTrainM = 0.0f;
                float offset = PresentPosition[Direction.Forward].Offset;
                float sectionStart = -offset;
                float progressiveMaxSpeedLimitMpS = allowedMaxSpeedLimitMpS;

                foreach (TrackCircuitRouteElement thisElement in ValidRoute[0])
                {
                    TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                    TrackDirection sectionDirection = thisElement.Direction;

                    if (thisSection.EndSignals[sectionDirection] != null)
                    {
                        distanceToTrainM = sectionStart + thisSection.Length;
                        var thisSignal = thisSection.EndSignals[sectionDirection];
                        var thisSpeedInfo = thisSignal.SignalSpeed(SignalFunction.Normal);
                        float validSpeed = thisSpeedInfo == null ? -1 : (IsFreight ? thisSpeedInfo.FreightSpeed : thisSpeedInfo.PassengerSpeed);

                        TrackMonitorSignalAspect signalAspect = thisSignal.TranslateTMAspect(thisSignal.SignalLR(SignalFunction.Normal));
                        result.ObjectInfoForward.Add(new TrainPathItem(signalAspect, validSpeed, distanceToTrainM));
                    }

                    if (thisSection.CircuitItems.TrackCircuitSpeedPosts[sectionDirection] != null)
                    {
                        foreach (TrackCircuitSignalItem thisSpeeditem in thisSection.CircuitItems.TrackCircuitSpeedPosts[sectionDirection])
                        {
                            var thisSpeedpost = thisSpeeditem.Signal;
                            var thisSpeedInfo = thisSpeedpost.SignalSpeed(SignalFunction.Speed);
                            float validSpeed = thisSpeedInfo == null ? -1 : (IsFreight ? thisSpeedInfo.FreightSpeed : thisSpeedInfo.PassengerSpeed);

                            distanceToTrainM = sectionStart + thisSpeeditem.SignalLocation;

                            if (distanceToTrainM > 0 && (validSpeed > 0 || (thisSpeedInfo != null && thisSpeedInfo.Reset)))
                            {
                                if (thisSpeedInfo != null && thisSpeedInfo.Reset)
                                    validSpeed = progressiveMaxSpeedLimitMpS;
                                else progressiveMaxSpeedLimitMpS = validSpeed;
                                result.ObjectInfoForward.Add(new TrainPathItem(validSpeed, distanceToTrainM, (SpeedItemType)thisSpeedpost.SpeedPostType()));
                            }
                        }
                    }

                    sectionStart += thisSection.Length;
                }
            }

            // do it separately for switches and mileposts
            // run along forward path to catch all diverging switches and mileposts

            AddSwitch_MilepostInfo(result, Direction.Forward);

            // set backward information

            // set authority
            result.ObjectInfoBackward.Add(new TrainPathItem(EndAuthorityTypes[1], DistanceToEndNodeAuthorityM[1]));

            // run along backward path to catch all speedposts and signals

            if (ValidRoute[1] != null)
            {
                float distanceToTrainM = 0.0f;
                float offset = PresentPosition[Direction.Backward].Offset;
                TrackCircuitSection firstSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Backward].TrackCircuitSectionIndex];
                float sectionStart = offset - firstSection.Length;
                float progressiveMaxSpeedLimitMpS = allowedMaxSpeedLimitMpS;

                foreach (TrackCircuitRouteElement thisElement in ValidRoute[1])
                {
                    TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                    TrackDirection sectionDirection = thisElement.Direction;

                    if (thisSection.EndSignals[sectionDirection] != null)
                    {
                        distanceToTrainM = sectionStart + thisSection.Length;
                        Signal thisSignal = thisSection.EndSignals[sectionDirection];
                        SpeedInfo thisSpeedInfo = thisSignal.SignalSpeed(SignalFunction.Normal);
                        float validSpeed = thisSpeedInfo == null ? -1 : (IsFreight ? thisSpeedInfo.FreightSpeed : thisSpeedInfo.PassengerSpeed);

                        TrackMonitorSignalAspect signalAspect = thisSignal.TranslateTMAspect(thisSignal.SignalLR(SignalFunction.Normal));
                        result.ObjectInfoBackward.Add(new TrainPathItem(signalAspect, validSpeed, distanceToTrainM));
                    }

                    if (thisSection.CircuitItems.TrackCircuitSpeedPosts[sectionDirection] != null)
                    {
                        foreach (TrackCircuitSignalItem thisSpeeditem in thisSection.CircuitItems.TrackCircuitSpeedPosts[sectionDirection])
                        {
                            Signal thisSpeedpost = thisSpeeditem.Signal;
                            SpeedInfo thisSpeedInfo = thisSpeedpost.SignalSpeed(SignalFunction.Speed);
                            float validSpeed = thisSpeedInfo == null ? -1 : (IsFreight ? thisSpeedInfo.FreightSpeed : thisSpeedInfo.PassengerSpeed);
                            distanceToTrainM = sectionStart + thisSpeeditem.SignalLocation;

                            if (distanceToTrainM > 0 && (validSpeed > 0 || (thisSpeedInfo != null && thisSpeedInfo.Reset)))
                            {
                                if (thisSpeedInfo != null && thisSpeedInfo.Reset)
                                    validSpeed = progressiveMaxSpeedLimitMpS;
                                else progressiveMaxSpeedLimitMpS = validSpeed;
                                result.ObjectInfoBackward.Add(new TrainPathItem(validSpeed, distanceToTrainM, (SpeedItemType)thisSpeedpost.SpeedPostType()));
                            }
                        }
                    }

                    sectionStart += thisSection.Length;
                }
            }

            // do it separately for switches and mileposts
            AddSwitch_MilepostInfo(result, Direction.Backward);

            return result;
        }

        //================================================================================================//
        /// <summary>
        /// Create TrackInfoObject for information in TrackMonitor window when OutOfControl
        /// </summary>
        public TrainInfo GetTrainInfoOOC()
        {
            TrainInfo result = new TrainInfo(ControlMode, MidPointDirectionToDirectionUnset(MUDirection), SpeedMpS, ProjectedSpeedMpS,
                Math.Min(AllowedMaxSpeedMpS, TrainMaxSpeedMpS), 0,
                (simulator.PlayerLocomotive.Flipped ^ simulator.PlayerLocomotive.GetCabFlipped()) ? Direction.Backward : Direction.Forward, false);

            // set out of control reason
            result.ObjectInfoForward.Add(new TrainPathItem(OutOfControlReason));
            return result;
        }

        //================================================================================================//
        /// <summary>
        /// Create Track Circuit Route Path
        /// </summary>
        public void SetRoutePath(AIPath aiPath, bool usePosition)
        {
            TrackDirection direction = (TrackDirection)(usePosition ? (int)FrontTDBTraveller.Direction : (RearTDBTraveller != null) ? (int)RearTDBTraveller?.Direction : -2);
            TCRoute = new TrackCircuitRoutePath(aiPath, direction, Length, Number);
            ValidRoute[0] = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];
        }

        //================================================================================================//
        /// <summary>
        /// Search trailing diverging switch
        /// </summary>
        /// 
        public float NextTrailingDivergingSwitchDistanceM(float maxDistanceM)
        {
            var switchDistanceM = float.MaxValue;
            // run along forward path to catch the first trailing diverging switch
            if (ValidRoute[0] != null)
            {
                float distanceToTrainM = 0.0f;
                float offset = PresentPosition[Direction.Forward].Offset;
                TrackCircuitSection firstSection = TrackCircuitSection.TrackCircuitList[PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
                float sectionStart = -offset;
                int startRouteIndex = PresentPosition[Direction.Forward].RouteListIndex;
                if (startRouteIndex < 0) startRouteIndex = ValidRoute[0].GetRouteIndex(PresentPosition[Direction.Forward].TrackCircuitSectionIndex, 0);
                if (startRouteIndex >= 0)
                {
                    int routeSectionIndex = PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                    for (int iRouteElement = startRouteIndex; iRouteElement < ValidRoute[0].Count && distanceToTrainM < maxDistanceM && sectionStart < maxDistanceM; iRouteElement++)
                    {
                        TrackCircuitSection thisSection = ValidRoute[0][iRouteElement].TrackCircuitSection;
                        TrackDirection sectionDirection = ValidRoute[0][iRouteElement].Direction;

                        if (thisSection.CircuitType == TrackCircuitType.Junction && (thisSection.Pins[sectionDirection, Location.FarEnd].Link == -1) && sectionStart < maxDistanceM)
                        {
                            // is trailing
                            TrackJunctionNode junctionNode = simulator.TDB.TrackDB.TrackNodes[thisSection.OriginalIndex] as TrackJunctionNode;
                            if ((thisSection.Pins[sectionDirection.Next(), Location.FarEnd].Link == routeSectionIndex && thisSection.JunctionDefaultRoute == 0) ||
                                (thisSection.Pins[sectionDirection.Next(), Location.NearEnd].Link == routeSectionIndex && thisSection.JunctionDefaultRoute > 0))
                            {
                                //is trailing diverging
                                switchDistanceM = sectionStart;
                                break;
                            }

                        }
                        routeSectionIndex = ValidRoute[0][iRouteElement].TrackCircuitSection.Index;
                        sectionStart += thisSection.Length;
                    }
                }
            }
            return switchDistanceM;
        }

        //================================================================================================//
        //
        // Preset switches for explorer mode
        //

        public void PresetExplorerPath(AIPath aiPath)
        {
            TrackDirection direction = (TrackDirection)(RearTDBTraveller != null ? (int)RearTDBTraveller.Direction : -2);
            TCRoute = new TrackCircuitRoutePath(aiPath, direction, 0, Number);

            // loop through all sections in first subroute except first and last (neither can be junction)

            for (int iElement = 1; iElement <= TCRoute.TCRouteSubpaths[0].Count - 2; iElement++)
            {
                TrackCircuitSection thisSection = TCRoute.TCRouteSubpaths[0][iElement].TrackCircuitSection;
                int nextSectionIndex = TCRoute.TCRouteSubpaths[0][iElement + 1].TrackCircuitSection.Index;
                int prevSectionIndex = TCRoute.TCRouteSubpaths[0][iElement - 1].TrackCircuitSection.Index;

                // process Junction

                if (thisSection.CircuitType == TrackCircuitType.Junction)
                {
                    if (thisSection.Pins[TrackDirection.Ahead, Location.NearEnd].Link == nextSectionIndex && !MPManager.NoAutoSwitch())
                    {
                        thisSection.AlignSwitchPins(prevSectionIndex);   // trailing switch
                    }
                    else
                    {
                        thisSection.AlignSwitchPins(nextSectionIndex);   // facing switch
                    }
                }
            }
        }

        //================================================================================================//

        /// <summary>
        /// Get total length of reserved section ahead of train
        /// </summary>
        /// <returns></returns>
        private float GetReservedLength()
        {
            float totalLength = 0f;
            TrackCircuitPartialPathRoute usedRoute = null;
            int routeListIndex = -1;
            float presentOffset = 0f;
            TrainRouted routedTrain = null;

            if (MUDirection == MidpointDirection.Forward || MUDirection == MidpointDirection.N || ValidRoute[1] == null)
            {
                usedRoute = ValidRoute[0];
                routeListIndex = PresentPosition[Direction.Forward].RouteListIndex;
                presentOffset = PresentPosition[Direction.Forward].Offset;
                routedTrain = routedForward;
            }
            else
            {
                usedRoute = ValidRoute[1];
                routeListIndex = PresentPosition[Direction.Backward].RouteListIndex;
                presentOffset = PresentPosition[Direction.Backward].Offset;
                routedTrain = routedBackward;
            }

            if (routeListIndex >= 0 && usedRoute != null && routeListIndex <= (usedRoute.Count - 1))
            {
                TrackCircuitSection thisSection = usedRoute[routeListIndex].TrackCircuitSection;
                totalLength = thisSection.Length - presentOffset;

                while (routeListIndex < usedRoute.Count - 1)
                {
                    routeListIndex++;
                    thisSection = usedRoute[routeListIndex].TrackCircuitSection;
                    if (thisSection.IsSet(routedTrain, false))
                    {
                        totalLength += thisSection.Length;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return (totalLength);
        }

        //================================================================================================//
        //
        // Extract alternative route
        //

        public TrackCircuitPartialPathRoute ExtractAlternativeRoute_pathBased(int altRouteIndex)
        {
            TrackCircuitPartialPathRoute returnRoute = new TrackCircuitPartialPathRoute();

            // extract entries of alternative route upto first signal

            foreach (TrackCircuitRouteElement thisElement in TCRoute.TCAlternativePaths[altRouteIndex])
            {
                returnRoute.Add(thisElement);
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                if (thisSection.EndSignals[thisElement.Direction] != null)
                {
                    break;
                }
            }

            return (returnRoute);
        }

        //================================================================================================//
        //
        // Extract alternative route
        //

        public TrackCircuitPartialPathRoute ExtractAlternativeRoute_locationBased(TrackCircuitPartialPathRoute altRoute)
        {
            TrackCircuitPartialPathRoute returnRoute = new TrackCircuitPartialPathRoute();

            // extract entries of alternative route upto first signal

            foreach (TrackCircuitRouteElement thisElement in altRoute)
            {
                returnRoute.Add(thisElement);
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                if (thisSection.EndSignals[thisElement.Direction] != null)
                {
                    break;
                }
            }

            return (returnRoute);
        }

        //================================================================================================//
        //
        // Set train route to alternative route - path based deadlock processing
        //

        public virtual void SetAlternativeRoute_pathBased(int startElementIndex, int altRouteIndex, Signal nextSignal)
        {
            // set new train route

            TrackCircuitPartialPathRoute thisRoute = ValidRoute[0];
            TrackCircuitPartialPathRoute newRoute = new TrackCircuitPartialPathRoute();
            int actSubpath = TCRoute.ActiveSubPath;

            TrackCircuitPartialPathRoute altRoute = TCRoute.TCAlternativePaths[altRouteIndex];
            TCRoute.ActiveAlternativePath = altRouteIndex;

            // part upto split

            for (int iElement = 0; iElement < startElementIndex; iElement++)
            {
                newRoute.Add(thisRoute[iElement]);
            }

            // alternative path

            for (int iElement = 0; iElement < altRoute.Count; iElement++)
            {
                newRoute.Add(altRoute[iElement]);
            }
            int lastAlternativeSectionIndex = thisRoute.GetRouteIndex(altRoute[altRoute.Count - 1].TrackCircuitSection.Index, startElementIndex);

            // check for any stations in abandoned path
            Dictionary<int, StationStop> abdStations = new Dictionary<int, StationStop>();
            CheckAbandonedStations(startElementIndex, lastAlternativeSectionIndex, actSubpath, abdStations);

            // continued path

            for (int iElement = lastAlternativeSectionIndex + 1; iElement < thisRoute.Count; iElement++)
            {
                newRoute.Add(thisRoute[iElement]);
            }
            // Reindexes ReversalInfo items
            var countDifference = newRoute.Count - ValidRoute[0].Count;
            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex >= 0)
                TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex = TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex + countDifference;
            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex >= 0)
                TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex = TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex + countDifference;

            // set new route

            ValidRoute[0] = newRoute;
            TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath] = newRoute;

            // check for abandoned stations - try to find alternative on passing path
            LookForReplacementStations(abdStations, newRoute, altRoute);

            // set signal route
            // part upto split

            TrackCircuitPartialPathRoute newSignalRoute = new TrackCircuitPartialPathRoute();

            int splitSignalIndex = nextSignal.SignalRoute.GetRouteIndex(thisRoute[startElementIndex].TrackCircuitSection.Index, 0);
            for (int iElement = 0; iElement < splitSignalIndex; iElement++)
            {
                newSignalRoute.Add(nextSignal.SignalRoute[iElement]);
            }

            // extract new route upto next signal

            TrackCircuitPartialPathRoute nextPart = ExtractAlternativeRoute_pathBased(altRouteIndex);
            foreach (TrackCircuitRouteElement thisElement in nextPart)
            {
                newSignalRoute.Add(thisElement);
            }

            nextSignal.ResetSignal(true);
            nextSignal.SignalRoute = newSignalRoute;

            if (ControlMode == TrainControlMode.AutoSignal)
            {
                // keep any items allready passed
                List<SignalItemInfo> keeplist = new List<SignalItemInfo>();
                foreach (SignalItemInfo checkItem in SignalObjectItems)
                {
                    float actualDistance = GetObjectDistanceToTrain(checkItem);
                    if (actualDistance < 0)
                    {
                        keeplist.Add(checkItem);
                    }
                }

                // create new list
                InitializeSignals(true);

                // add any passed items (in reverse order at start of list)
                if (keeplist.Count > 0)
                {
                    for (int iObject = keeplist.Count - 1; iObject >= 0; iObject--)
                    {
                        SignalObjectItems.Insert(0, keeplist[iObject]);
                    }
                }

                // find new next signal
                NextSignalObject[0] = null;
                for (int iObject = 0; iObject <= SignalObjectItems.Count - 1 && NextSignalObject[0] == null; iObject++)
                {
                    if (SignalObjectItems[iObject].ItemType == SignalItemType.Signal)
                    {
                        NextSignalObject[0] = SignalObjectItems[iObject].SignalDetails;
                    }
                }

                if (NextSignalObject[0] != null)
                {
                    NextSignalObject[0].RequestClearSignal(ValidRoute[0], routedForward, 0, false, null);
                }
            }
        }

        //================================================================================================//
        //
        // Set train route to alternative route - location based deadlock processing
        //

        internal virtual void SetAlternativeRoute_locationBased(int startSectionIndex, DeadlockInfo sectionDeadlockInfo, int usedPath, Signal nextSignal)
        {
            // set new train route

            TrackCircuitPartialPathRoute thisRoute = ValidRoute[0];
            TrackCircuitPartialPathRoute newRoute = new TrackCircuitPartialPathRoute();

            TrackCircuitPartialPathRoute altRoute = sectionDeadlockInfo.AvailablePathList[usedPath].Path;
            int actSubpath = TCRoute.ActiveSubPath;

            // part upto split

            int startElementIndex = thisRoute.GetRouteIndex(startSectionIndex, PresentPosition[Direction.Forward].RouteListIndex);
            for (int iElement = 0; iElement < startElementIndex; iElement++)
            {
                newRoute.Add(thisRoute[iElement]);
            }

            // alternative path

            for (int iElement = 0; iElement < altRoute.Count; iElement++)
            {
                newRoute.Add(altRoute[iElement]);
            }

            // check for any deadlocks on abandoned path - but only if not on new path

            int lastAlternativeSectionIndex = thisRoute.GetRouteIndex(altRoute[altRoute.Count - 1].TrackCircuitSection.Index, startElementIndex);
            for (int iElement = startElementIndex; iElement <= lastAlternativeSectionIndex; iElement++)
            {
                TrackCircuitSection abdSection = thisRoute[iElement].TrackCircuitSection;

                if (newRoute.GetRouteIndex(abdSection.Index, 0) < 0)
                {
                    abdSection.ClearDeadlockTrap(Number);
                }
            }
            // check for any stations in abandoned path

            Dictionary<int, StationStop> abdStations = new Dictionary<int, StationStop>();
            CheckAbandonedStations(startElementIndex, lastAlternativeSectionIndex, actSubpath, abdStations);

            // continued path

            for (int iElement = lastAlternativeSectionIndex + 1; iElement < thisRoute.Count; iElement++)
            {
                newRoute.Add(thisRoute[iElement]);
            }

            // Reindexes ReversalInfo items
            var countDifference = newRoute.Count - ValidRoute[0].Count;
            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex >= 0)
                TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex = TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastDivergeIndex + countDifference;
            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex >= 0)
                TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex = TCRoute.ReversalInfo[TCRoute.ActiveSubPath].LastSignalIndex + countDifference;

            // set new route

            ValidRoute[0] = newRoute;
            TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath] = newRoute;

            // check for abandoned stations - try to find alternative on passing path
            LookForReplacementStations(abdStations, newRoute, altRoute);

            // set signal route
            // part upto split

            if (nextSignal != null)
            {
                TrackCircuitPartialPathRoute newSignalRoute = new TrackCircuitPartialPathRoute();

                int splitSignalIndex = nextSignal.SignalRoute.GetRouteIndex(thisRoute[startElementIndex].TrackCircuitSection.Index, 0);
                for (int iElement = 0; iElement < splitSignalIndex; iElement++)
                {
                    newSignalRoute.Add(nextSignal.SignalRoute[iElement]);
                }

                // extract new route upto next signal

                TrackCircuitPartialPathRoute nextPart = ExtractAlternativeRoute_locationBased(altRoute);
                foreach (TrackCircuitRouteElement thisElement in nextPart)
                {
                    newSignalRoute.Add(thisElement);
                }

                // set new signal route
                // reset signal
                // if train in signal mode, request clear signal

                nextSignal.ResetSignal(true);
                nextSignal.SignalRoute = newSignalRoute;

                if (ControlMode == TrainControlMode.AutoSignal)
                {
                    // keep any items allready passed
                    List<SignalItemInfo> keeplist = new List<SignalItemInfo>();
                    foreach (SignalItemInfo checkItem in SignalObjectItems)
                    {
                        float actualDistance = GetObjectDistanceToTrain(checkItem);
                        if (actualDistance < 0)
                        {
                            keeplist.Add(checkItem);
                        }
                    }

                    // create new list
                    InitializeSignals(true);

                    // add any passed items (in reverse order at start of list)
                    if (keeplist.Count > 0)
                    {
                        for (int iObject = keeplist.Count - 1; iObject >= 0; iObject--)
                        {
                            SignalObjectItems.Insert(0, keeplist[iObject]);
                        }
                    }

                    // find new next signal
                    NextSignalObject[0] = null;
                    for (int iObject = 0; iObject <= SignalObjectItems.Count - 1 && NextSignalObject[0] == null; iObject++)
                    {
                        if (SignalObjectItems[iObject].ItemType == SignalItemType.Signal)
                        {
                            NextSignalObject[0] = SignalObjectItems[iObject].SignalDetails;
                            DistanceToSignal = SignalObjectItems[iObject].DistanceToTrain;
                        }
                    }

                    if (NextSignalObject[0] != null)
                    {
                        NextSignalObject[0].RequestClearSignal(ValidRoute[0], routedForward, 0, false, null);
                    }
                }
            }
        }

        //================================================================================================//
        //
        // Check for abandoned stations in the abandoned path
        //
        //
        private void CheckAbandonedStations(int startElementIndex, int lastAlternativeSectionIndex, int actSubpath, Dictionary<int, StationStop> abdStations)
        {
            int nextStationIndex = 0;


            if (StationStops != null && StationStops.Count > 0)
            {
                int stationRouteIndex = StationStops[nextStationIndex].RouteIndex;
                int stationSubpath = StationStops[nextStationIndex].SubrouteIndex;

                while (stationRouteIndex < lastAlternativeSectionIndex)
                {
                    if (stationSubpath == actSubpath && stationRouteIndex > startElementIndex)
                    {
                        abdStations.Add(nextStationIndex, StationStops[nextStationIndex]);
                    }

                    nextStationIndex++;
                    if (nextStationIndex > StationStops.Count - 1)
                    {
                        stationRouteIndex = lastAlternativeSectionIndex + 1;  // no more stations - set index beyond end
                    }
                    else
                    {
                        stationRouteIndex = StationStops[nextStationIndex].RouteIndex;
                        stationSubpath = StationStops[nextStationIndex].SubrouteIndex;
                        if (stationSubpath > actSubpath)
                        {
                            stationRouteIndex = lastAlternativeSectionIndex + 1; // no more stations in this subpath
                        }
                    }
                }
            }
        }

        //================================================================================================//
        //
        // Look for stations in alternative route
        //
        //
        private void LookForReplacementStations(Dictionary<int, StationStop> abdStations, TrackCircuitPartialPathRoute newRoute, TrackCircuitPartialPathRoute altRoute)
        {

            if (StationStops != null)
            {
                List<StationStop> newStops = new List<StationStop>();
                int firstIndex = -1;

                foreach (KeyValuePair<int, StationStop> abdStop in abdStations)
                {
                    if (firstIndex < 0) firstIndex = abdStop.Key;
                    StationStop newStop = SetAlternativeStationStop(abdStop.Value, altRoute);
                    StationStops.RemoveAt(firstIndex);
                    if (newStop != null)
                    {
                        newStops.Add(newStop);
                    }
                }

                for (int iStop = newStops.Count - 1; iStop >= 0; iStop--)
                {
                    StationStops.Insert(firstIndex, newStops[iStop]);
                }

                // recalculate indices of all stops
                int prevIndex = 0;
                foreach (StationStop statStop in StationStops)
                {
                    statStop.RouteIndex = newRoute.GetRouteIndex(statStop.TrackCircuitSectionIndex, prevIndex);
                    prevIndex = statStop.RouteIndex;
                }
            }
        }

        //================================================================================================//
        //
        // Find station on alternative route
        //
        //

        public virtual StationStop SetAlternativeStationStop(StationStop orgStop, TrackCircuitPartialPathRoute newRoute)
        {
            int altPlatformIndex = -1;

            // get station platform list
            if (signalRef.StationXRefList.ContainsKey(orgStop.PlatformItem.Name))
            {
                List<int> XRefKeys = signalRef.StationXRefList[orgStop.PlatformItem.Name];

                // search through all available platforms
                for (int platformIndex = 0; platformIndex <= XRefKeys.Count - 1 && altPlatformIndex < 0; platformIndex++)
                {
                    int platformXRefIndex = XRefKeys[platformIndex];
                    PlatformDetails altPlatform = signalRef.PlatformDetailsList[platformXRefIndex];

                    // check if section is in new route
                    for (int iSectionIndex = 0; iSectionIndex <= altPlatform.TCSectionIndex.Count - 1 && altPlatformIndex < 0; iSectionIndex++)
                    {
                        if (newRoute.GetRouteIndex(altPlatform.TCSectionIndex[iSectionIndex], 0) > 0)
                        {
                            altPlatformIndex = platformXRefIndex;
                        }
                    }
                }

                // section found in new route - set new station details using old details
                if (altPlatformIndex > 0)
                {
                    StationStop newStop = CalculateStationStop(signalRef.PlatformDetailsList[altPlatformIndex].PlatformReference[Location.NearEnd],
                        orgStop.ArrivalTime, orgStop.DepartTime, 15.0f);

                    return (newStop);
                }
            }

            return (null);
        }

        //================================================================================================//
        /// <summary>
        /// Create station stop (used in activity mode only)
        /// <\summary>

        public StationStop CalculateStationStop(int platformStartID, int arrivalTime, int departTime, float clearingDistanceM)
        {
            int platformIndex;
            int lastRouteIndex = 0;
            int activeSubroute = 0;

            TrackCircuitPartialPathRoute thisRoute = TCRoute.TCRouteSubpaths[activeSubroute];

            // get platform details

            if (!signalRef.PlatformXRefList.TryGetValue(platformStartID, out platformIndex))
            {
                return (null); // station not found
            }
            else
            {
                PlatformDetails thisPlatform = signalRef.PlatformDetailsList[platformIndex];
                int sectionIndex = thisPlatform.TCSectionIndex[0];
                int routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);

                // if first section not found in route, try last

                if (routeIndex < 0)
                {
                    sectionIndex = thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                    routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);
                }

                // if neither section found - try next subroute - keep trying till found or out of subroutes

                while (routeIndex < 0 && activeSubroute < (TCRoute.TCRouteSubpaths.Count - 1))
                {
                    activeSubroute++;
                    thisRoute = TCRoute.TCRouteSubpaths[activeSubroute];
                    routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);

                    // if first section not found in route, try last

                    if (routeIndex < 0)
                    {
                        sectionIndex = thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                        routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);
                    }
                }

                // if neither section found - platform is not on route - skip

                if (routeIndex < 0)
                {
                    Trace.TraceWarning("Train {0} Service {1} : platform {2} is not on route",
                            Number.ToString(), Name, platformStartID.ToString());
                    return (null);
                }

                // determine end stop position depending on direction

                TrackCircuitRouteElement thisElement = thisRoute[routeIndex];

                int endSectionIndex = thisElement.Direction == 0 ?
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1] :
                    thisPlatform.TCSectionIndex[0];
                int beginSectionIndex = thisElement.Direction == 0 ?
                    thisPlatform.TCSectionIndex[0] :
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];

                float endOffset = thisPlatform.TrackCircuitOffset[Location.FarEnd, (TrackDirection)thisElement.Direction];
                float beginOffset = thisPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)thisElement.Direction];

                float deltaLength = thisPlatform.Length - Length; // platform length - train length

                TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[endSectionIndex];


                int firstRouteIndex = thisRoute.GetRouteIndex(beginSectionIndex, 0);
                if (firstRouteIndex < 0)
                    firstRouteIndex = routeIndex;
                lastRouteIndex = thisRoute.GetRouteIndex(endSectionIndex, 0);
                if (lastRouteIndex < 0)
                    lastRouteIndex = routeIndex;

                float stopOffset = 0;
                float fullLength = thisPlatform.Length;


                // if train too long : search back for platform with same name
                if (deltaLength < 0)
                {
                    float actualBegin = beginOffset;

                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[beginSectionIndex];

                    // Other platforms in same section

                    if (thisSection.PlatformIndices.Count > 1)
                    {
                        foreach (int nextIndex in thisSection.PlatformIndices)
                        {
                            if (nextIndex != platformIndex)
                            {
                                PlatformDetails otherPlatform = signalRef.PlatformDetailsList[nextIndex];
                                if (String.Compare(otherPlatform.Name, thisPlatform.Name) == 0)
                                {
                                    int otherSectionIndex = thisElement.Direction == 0 ?
                                        otherPlatform.TCSectionIndex[0] :
                                        otherPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                                    if (otherSectionIndex == beginSectionIndex)
                                    {
                                        if (otherPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)thisElement.Direction] < actualBegin)
                                        {
                                            actualBegin = otherPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)thisElement.Direction];
                                            fullLength = endOffset - actualBegin;
                                        }
                                    }
                                    else
                                    {
                                        int addRouteIndex = thisRoute.GetRouteIndex(otherSectionIndex, 0);
                                        float addOffset = otherPlatform.TrackCircuitOffset[Location.FarEnd, (TrackDirection)(thisElement.Direction == 0 ? 1 : 0)];
                                        // offset of begin in other direction is length of available track

                                        if (lastRouteIndex > 0)
                                        {
                                            float thisLength =
                                                thisRoute.GetDistanceAlongRoute(addRouteIndex, addOffset, lastRouteIndex, endOffset, true);
                                            if (thisLength > fullLength)
                                                fullLength = thisLength;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    deltaLength = fullLength - Length;
                }

                // search back along route

                if (deltaLength < 0)
                {
                    float distance = fullLength + beginOffset;
                    bool platformFound = false;

                    for (int iIndex = firstRouteIndex - 1;
                                iIndex >= 0 && distance < 500f && platformFound;
                                iIndex--)
                    {
                        TrackCircuitSection nextSection = thisRoute[iIndex].TrackCircuitSection;

                        foreach (int otherPlatformIndex in nextSection.PlatformIndices)
                        {
                            PlatformDetails otherPlatform = signalRef.PlatformDetailsList[otherPlatformIndex];
                            if (String.Compare(otherPlatform.Name, thisPlatform.Name) == 0)
                            {
                                fullLength = otherPlatform.Length + distance;
                                // we miss a little bit (offset) - that's because we don't know direction of other platform
                                platformFound = true; // only check for one more
                            }
                        }
                        distance += nextSection.Length;
                    }

                    deltaLength = fullLength - Length;
                }


                // determine stop position

                stopOffset = endOffset - (0.5f * deltaLength);

                // beyond section : check for route validity (may not exceed route)

                if (stopOffset > endSection.Length)
                {
                    float addOffset = stopOffset - endSection.Length;
                    float overlap = 0f;

                    for (int iIndex = lastRouteIndex; iIndex < thisRoute.Count && overlap < addOffset; iIndex++)
                    {
                        overlap += thisRoute[iIndex].TrackCircuitSection.Length;
                    }

                    if (overlap < stopOffset)
                        stopOffset = overlap;
                }

                // check if stop offset beyond end signal - do not hold at signal

                int EndSignal = -1;
                bool HoldSignal = false;
                bool NoWaitSignal = false;
                bool NoClaimAllowed = false;

                // check if train is to reverse in platform
                // if so, set signal at other end as hold signal

                TrackDirection useDirection = (TrackDirection)thisElement.Direction;
                bool inDirection = true;

                if (TCRoute.ReversalInfo[activeSubroute].Valid)
                {
                    TrackCircuitReversalInfo thisReversal = TCRoute.ReversalInfo[activeSubroute];
                    int reversalIndex = thisReversal.SignalUsed ? thisReversal.LastSignalIndex : thisReversal.LastDivergeIndex;
                    if (reversalIndex >= 0 && reversalIndex <= lastRouteIndex) // reversal point is this section or earlier
                    {
                        useDirection = useDirection.Next();
                        inDirection = false;
                    }
                }

                // check for end signal

                if (thisPlatform.EndSignals[useDirection] >= 0)
                {
                    EndSignal = thisPlatform.EndSignals[useDirection];

                    // stop location is in front of signal
                    if (inDirection)
                    {
                        if (thisPlatform.DistanceToSignals[useDirection] > (stopOffset - endOffset))
                        {
                            HoldSignal = true;

                            if ((thisPlatform.DistanceToSignals[useDirection] + (endOffset - stopOffset)) < clearingDistanceM)
                            {
                                stopOffset = endOffset + thisPlatform.DistanceToSignals[useDirection] - clearingDistanceM - 1.0f;
                            }
                        }
                        // if most of train fits in platform then stop at signal
                        else if ((thisPlatform.DistanceToSignals[useDirection] - clearingDistanceM + thisPlatform.Length) >
                                      (0.6 * Length))
                        {
                            HoldSignal = true;
                            stopOffset = endOffset + thisPlatform.DistanceToSignals[useDirection] - clearingDistanceM - 1.0f;
                            // set 1m earlier to give priority to station stop over signal
                        }
                        // train does not fit in platform - reset exit signal
                        else
                        {
                            EndSignal = -1;
                        }
                    }
                    else
                    // end of train is beyond signal
                    {
                        if ((beginOffset - thisPlatform.DistanceToSignals[useDirection]) < (stopOffset - Length))
                        {
                            HoldSignal = true;

                            if ((stopOffset - Length - beginOffset + thisPlatform.DistanceToSignals[useDirection]) < clearingDistanceM)
                            {
                                stopOffset = beginOffset - thisPlatform.DistanceToSignals[useDirection] + Length + clearingDistanceM + 1.0f;
                            }
                        }
                        // if most of train fits in platform then stop at signal
                        else if ((thisPlatform.DistanceToSignals[useDirection] - clearingDistanceM + thisPlatform.Length) >
                                      (0.6 * Length))
                        {
                            // set 1m earlier to give priority to station stop over signal
                            stopOffset = beginOffset - thisPlatform.DistanceToSignals[useDirection] + Length + clearingDistanceM + 1.0f;

                            // check if stop is clear of end signal (if any)
                            if (thisPlatform.EndSignals[(TrackDirection)thisElement.Direction] != -1)
                            {
                                if (stopOffset < (endOffset + thisPlatform.DistanceToSignals[(TrackDirection)thisElement.Direction]))
                                {
                                    HoldSignal = true; // if train fits between signals
                                }
                                else
                                {
                                    stopOffset = endOffset + thisPlatform.DistanceToSignals[(TrackDirection)thisElement.Direction] - 1.0f; // stop at end signal
                                }
                            }
                        }
                        // train does not fit in platform - reset exit signal
                        else
                        {
                            EndSignal = -1;
                        }
                    }
                }

                if (simulator.Settings.NoForcedRedAtStationStops)
                {
                    // We don't want reds at exit signal in this case
                    HoldSignal = false;
                }

                // build and add station stop

                TrackCircuitRouteElement lastElement = thisRoute[lastRouteIndex];

                StationStop thisStation = new StationStop(
                        platformStartID,
                        thisPlatform,
                        activeSubroute,
                        lastRouteIndex,
                        lastElement.TrackCircuitSection.Index,
                        thisElement.Direction,
                        EndSignal,
                        HoldSignal,
                        NoWaitSignal,
                        NoClaimAllowed,
                        stopOffset,
                        arrivalTime,
                        departTime,
                        false,
                        null,
                        null,
                        null,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false,
                        StationStopType.Station);

                return (thisStation);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Get other train from number
        /// Use Simulator.Trains to get other train
        /// </summary>

        public Train GetOtherTrainByNumber(int reqNumber)
        {
            return simulator.Trains.GetTrainByNumber(reqNumber);
        }

        //================================================================================================//
        /// <summary>
        /// Get other train from number
        /// Use Simulator.Trains to get other train
        /// </summary>

        public Train GetOtherTrainByName(string reqName)
        {
            return simulator.Trains.GetTrainByName(reqName);
        }

        //================================================================================================//
        /// <summary>
        /// Update AI Static state - dummy method to allow virtualization by child classes
        /// </summary>

        public virtual void UpdateAIStaticState(int presentTime)
        {
        }

        //================================================================================================//
        /// <summary>
        /// Get AI Movement State - dummy method to allow virtualization by child classes
        /// </summary>

        public virtual AITrain.AI_MOVEMENT_STATE GetAIMovementState()
        {
            return (AITrain.AI_MOVEMENT_STATE.UNKNOWN);
        }

        //================================================================================================//
        /// <summary>
        /// Check on station tasks, required when in timetable mode when there is no activity - dummy method to allow virtualization by child classes
        /// </summary>
        public virtual void CheckStationTask()
        {
        }

        //================================================================================================//
        /// <summary>
        /// Special additional methods when stopped at signal in timetable mode - dummy method to allow virtualization by child classes
        /// </summary>
        public virtual bool ActionsForSignalStop()
        {
            return true;
        }

        //================================================================================================//
        //
        // Check if train is in wait mode - dummy method to allow virtualization by child classes
        //

        public virtual bool isInWaitState()
        {
            return (false);
        }

        //================================================================================================//
        //
        // Check if train has AnyWait valid for this section - dummy method to allow virtualization by child classes
        //

        public virtual bool CheckAnyWaitCondition(int index)
        {
            return (false);
        }

        //================================================================================================//
        //
        // Check if train has Wait valid for this section - dummy method to allow virtualization by child classes
        //

        public virtual bool HasActiveWait(int startSectionIndex, int endSectionIndex)
        {
            return (false);
        }

        /// <summary>
        /// Update Section State - additional
        /// dummy method to allow virtualisation for Timetable trains
        /// </summary>
        protected virtual void UpdateSectionStateAdditional(int sectionIndex)
        {
        }

        //================================================================================================//
        /// <summary>
        /// Check wait condition
        /// Dummy method to allow virtualization by child classes
        /// <\summary>

        public virtual bool CheckWaitCondition(int sectionIndex)
        {
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Check Pool Access
        /// Dummy method to allow virtualization by child classes
        /// <\summary>

        public virtual bool CheckPoolAccess(int sectionIndex)
        {
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Clear moving table after moving table actions
        /// Dummy method to allow virtualization by child classes
        /// </summary>

        public virtual void ClearMovingTable()
        {
        }

        //================================================================================================//
        /// <summary>
        /// TrainGetSectionStateClearNode
        /// Virtual method to allow differentiation by child classes
        /// </summary>

        public virtual bool TrainGetSectionStateClearNode(int elementDirection, TrackCircuitPartialPathRoute routePart, TrackCircuitSection thisSection)
        {
            return (thisSection.IsAvailable(this));
        }

        //================================================================================================//
        /// <summary>
        /// TestAbsDelay
        /// Tests if Waiting point delay >=30000 and <4000; under certain conditions this means that
        /// delay represents an absolute time of day, with format 3HHMM
        /// </summary>
        /// 
        public virtual void TestAbsDelay(ref int delay, int correctedTime)
        {
            if (delay < 30000 || delay >= 40000) return;
            int hour = (delay / 100) % 100;
            int minute = delay % 100;
            int waitUntil = 60 * (minute + 60 * hour);
            int latest = Time.Compare.Latest(waitUntil, correctedTime);
            if (latest == waitUntil && waitUntil >= correctedTime) delay = waitUntil - correctedTime;
            else if (latest == correctedTime) delay = 1; // put 1 second delay if waitUntil is already over
            else delay = waitUntil - correctedTime + 3600 * 24; // we are over midnight here
        }

        //================================================================================================//
        /// <summary>
        /// ToggleDoors
        /// Toggles status of doors of a train
        /// Parameters: right = true if right doors; open = true if opening
        /// <\summary>
        public void ToggleDoors(bool right, bool open)
        {
            foreach (TrainCar car in Cars)
            {
                var mstsWagon = car as MSTSWagon;
                if (!car.Flipped && right || car.Flipped && !right)
                {
                    mstsWagon.DoorRightOpen = open;
                }
                else
                {
                    mstsWagon.DoorLeftOpen = open;
                }
                mstsWagon.SignalEvent(open ? TrainEvent.DoorOpen : TrainEvent.DoorClose); // hook for sound trigger
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check if it's time to have a failed car or locomotive
        /// </summary>
        /// 

        public void CheckFailures(double elapsedClockSeconds)
        {
            if (IsFreight) CheckBrakes(elapsedClockSeconds);
            CheckLocoPower(elapsedClockSeconds);
        }

        //================================================================================================//
        /// <summary>
        /// Check if it's time to have a car with stuck brakes
        /// </summary>

        public void CheckBrakes(double elapsedClockSeconds)
        {
            if (BrakingTime == -1) return;
            if (BrakingTime == -2)
            {
                BrakingTime = -1; // Viewer has seen it, can pass to this value
                return;
            }
            if (SpeedMpS > 0)
            {
                for (int iCar = 0; iCar < Cars.Count; iCar++)
                {
                    var car = Cars[iCar];
                    if (!(car is MSTSLocomotive))
                    {
                        if (car.BrakeSystem.IsBraking() && BrakingTime >= 0)
                        {
                            BrakingTime += elapsedClockSeconds;
                            ContinuousBrakingTime += elapsedClockSeconds;
                            if (BrakingTime >= 1200.0 / simulator.Settings.ActRandomizationLevel || ContinuousBrakingTime >= 600.0 / simulator.Settings.ActRandomizationLevel)
                            {
                                var randInt = Simulator.Random.Next(200000);
                                var brakesStuck = false;
                                if (randInt > 200000 - (simulator.Settings.ActRandomizationLevel == 1 ? 4 : simulator.Settings.ActRandomizationLevel == 2 ? 8 : 31))
                                // a car will have brakes stuck. Select which one
                                {
                                    var iBrakesStuckCar = Simulator.Random.Next(Cars.Count);
                                    var jBrakesStuckCar = iBrakesStuckCar;
                                    while (Cars[iBrakesStuckCar] is MSTSLocomotive && iBrakesStuckCar < Cars.Count)
                                        iBrakesStuckCar++;
                                    if (iBrakesStuckCar != Cars.Count)
                                    {
                                        brakesStuck = true;
                                    }
                                    else
                                    {
                                        while (Cars[jBrakesStuckCar] is MSTSLocomotive && jBrakesStuckCar > Cars.Count)
                                            jBrakesStuckCar--;
                                        if (jBrakesStuckCar != -1)
                                        {
                                            iBrakesStuckCar = jBrakesStuckCar;
                                            brakesStuck = true;
                                        }
                                    }
                                    if (brakesStuck)
                                    {
                                        Cars[iBrakesStuckCar].BrakesStuck = true;
                                        BrakingTime = -2; //Check no more, we already have a brakes stuck car
                                        ContinuousBrakingTime = -iBrakesStuckCar; // let's use it for two purposes
                                        simulator.Confirmer.Warning(Simulator.Catalog.GetString("Car " + Cars[iBrakesStuckCar].CarID + " has stuck brakes"));
                                    }
                                }
                            }
                        }
                        else ContinuousBrakingTime = 0;
                        return;
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check if it's time to have an electric or diesel loco with a bogie not powering
        /// </summary>

        public void CheckLocoPower(double elapsedClockSeconds)
        {
            if (RunningTime == -1) return;
            if (RunningTime == -2)
            {
                RunningTime = -1; // Viewer has seen it, can pass to this value
                return;
            }
            if (SpeedMpS > 0)
            {
                var oldRunningTime = RunningTime;
                RunningTime += elapsedClockSeconds;
                if (Math.Truncate(oldRunningTime) < Math.Truncate(RunningTime)) // Check only every second
                {
                    var nLocos = 0;
                    for (int iCar = 0; iCar < Cars.Count; iCar++)
                    {
                        var car = Cars[iCar];
                        if ((car is MSTSElectricLocomotive || car is MSTSDieselLocomotive) && car.Parts.Count >= 2 &&
                            ((car as MSTSLocomotive).ThrottlePercent > 10 || (car as MSTSLocomotive).DynamicBrakePercent > 10)) nLocos++;
                    }
                    if (nLocos > 0)
                    {
                        var randInt = Simulator.Random.Next(2000000 / nLocos);
                        var locoUnpowered = false;
                        if (randInt > 2000000 / nLocos - (simulator.Settings.ActRandomizationLevel == 1 ? 2 : simulator.Settings.ActRandomizationLevel == 2 ? 8 : 50))
                        // a loco will be partly or totally unpowered. Select which one
                        {
                            var iLocoUnpoweredCar = Simulator.Random.Next(Cars.Count);
                            var jLocoUnpoweredCar = iLocoUnpoweredCar;
                            if (iLocoUnpoweredCar % 2 == 1)
                            {
                                locoUnpowered = SearchBackOfTrain(ref iLocoUnpoweredCar);
                                if (!locoUnpowered)
                                {
                                    iLocoUnpoweredCar = jLocoUnpoweredCar;
                                    locoUnpowered = SearchFrontOfTrain(ref iLocoUnpoweredCar);
                                }

                            }
                            else
                            {
                                locoUnpowered = SearchFrontOfTrain(ref iLocoUnpoweredCar);
                                if (!locoUnpowered)
                                {
                                    iLocoUnpoweredCar = jLocoUnpoweredCar;
                                    locoUnpowered = SearchBackOfTrain(ref iLocoUnpoweredCar);
                                }
                            }

                            if (locoUnpowered)
                            {
                                RunningTime = -2; //Check no more, we already have an unpowered loco
                                var unpoweredLoco = Cars[iLocoUnpoweredCar] as MSTSLocomotive;
                                if (randInt % 2 == 1 || unpoweredLoco is MSTSElectricLocomotive)
                                {
                                    unpoweredLoco.PowerReduction = 0.5f;
                                    simulator.Confirmer.Warning(Simulator.Catalog.GetString("Locomotive " + unpoweredLoco.CarID + " partial failure: 1 unpowered bogie"));
                                }
                                else
                                {
                                    unpoweredLoco.PowerReduction = 1.0f;
                                    simulator.Confirmer.Warning(Simulator.Catalog.GetString("Locomotive " + unpoweredLoco.CarID + " compressor blown"));
                                }
                                UnpoweredLoco = iLocoUnpoweredCar;
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check first electric or diesel loco searching towards back of train
        /// </summary>

        private bool SearchBackOfTrain(ref int iLocoUnpoweredCar)
        {
            var locoUnpowered = false;
            while (iLocoUnpoweredCar < Cars.Count && !((Cars[iLocoUnpoweredCar] is MSTSElectricLocomotive || Cars[iLocoUnpoweredCar] is MSTSDieselLocomotive) && Cars[iLocoUnpoweredCar].Parts.Count >= 2))
                iLocoUnpoweredCar++;
            if (iLocoUnpoweredCar != Cars.Count)
            {
                locoUnpowered = true;
            }

            return locoUnpowered;
        }

        //================================================================================================//
        /// <summary>
        /// Check first electric or diesel loco searching towards front of train
        /// </summary>

        private bool SearchFrontOfTrain(ref int iLocoUnpoweredCar)
        {

            var locoUnpowered = false;
            while (iLocoUnpoweredCar >= 0 && !((Cars[iLocoUnpoweredCar] is MSTSElectricLocomotive || Cars[iLocoUnpoweredCar] is MSTSDieselLocomotive) && Cars[iLocoUnpoweredCar].Parts.Count >= 2))
                iLocoUnpoweredCar--;
            if (iLocoUnpoweredCar != -1)
            {
                locoUnpowered = true;
            }
            return locoUnpowered;
        }

        //================================================================================================//
        /// <summary>
        /// Routed train class : train class plus valid route direction indication
        /// Used throughout in the signalling process in order to derive correct route in Manual and Explorer modes
        /// </summary>

        public class TrainRouted
        {
            public Train Train;
            public int TrainRouteDirectionIndex
            {
                get { return (int)Direction; }
                set { Direction = (Direction)value; }
            }
            public Direction Direction { get; private set; }

            //================================================================================================//
            /// <summary>
            /// Constructor
            /// </summary>

            public TrainRouted(Train thisTrain, int thisIndex)
            {
                Train = thisTrain;
                TrainRouteDirectionIndex = thisIndex;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Distance Travelled action item list
        /// </summary>

        public class DistanceTravelledActions : LinkedList<DistanceTravelledItem>
        {

            //================================================================================================//
            //
            // Copy list
            //

            public DistanceTravelledActions Copy()
            {
                DistanceTravelledActions newList = new DistanceTravelledActions();

                LinkedListNode<DistanceTravelledItem> nextNode = this.First;
                DistanceTravelledItem thisItem = nextNode.Value;

                newList.AddFirst(thisItem);
                LinkedListNode<DistanceTravelledItem> prevNode = newList.First;

                nextNode = nextNode.Next;

                while (nextNode != null)
                {
                    thisItem = nextNode.Value;
                    newList.AddAfter(prevNode, thisItem);
                    nextNode = nextNode.Next;
                    prevNode = prevNode.Next;
                }

                return (newList);
            }


            //================================================================================================//
            /// <summary>
            /// Insert item on correct distance
            /// <\summary>

            public void InsertAction(DistanceTravelledItem thisItem)
            {

                if (this.Count == 0)
                {
                    this.AddFirst(thisItem);
                }
                else
                {
                    LinkedListNode<DistanceTravelledItem> nextNode = this.First;
                    DistanceTravelledItem nextItem = nextNode.Value;
                    bool inserted = false;
                    while (!inserted)
                    {
                        if (thisItem.RequiredDistance < nextItem.RequiredDistance)
                        {
                            this.AddBefore(nextNode, thisItem);
                            inserted = true;
                        }
                        else if (nextNode.Next == null)
                        {
                            this.AddAfter(nextNode, thisItem);
                            inserted = true;
                        }
                        else
                        {
                            nextNode = nextNode.Next;
                            nextItem = nextNode.Value;
                        }
                    }
                }
            }

            //================================================================================================//
            /// <summary>
            /// Insert section clearance item
            /// <\summary>

            public void InsertClearSection(float distance, int sectionIndex)
            {
                ClearSectionItem thisItem = new ClearSectionItem(distance, sectionIndex);
                InsertAction(thisItem);
            }

            //================================================================================================//
            /// <summary>
            /// Get list of items to be processed
            /// <\summary>

            public List<DistanceTravelledItem> GetActions(float distance)
            {
                List<DistanceTravelledItem> itemList = new List<DistanceTravelledItem>();

                bool itemsCollected = false;
                LinkedListNode<DistanceTravelledItem> nextNode = this.First;
                LinkedListNode<DistanceTravelledItem> prevNode;

                while (!itemsCollected && nextNode != null)
                {
                    if (nextNode.Value.RequiredDistance <= distance)
                    {
                        itemList.Add(nextNode.Value);
                        prevNode = nextNode;
                        nextNode = prevNode.Next;
                        this.Remove(prevNode);
                    }
                    else
                    {
                        itemsCollected = true;
                    }
                }
                return (itemList);
            }

            public List<DistanceTravelledItem> GetAuxActions(Train thisTrain, float distance)
            {
                List<DistanceTravelledItem> itemList = new List<DistanceTravelledItem>();
                LinkedListNode<DistanceTravelledItem> nextNode = this.First;

                while (nextNode != null)
                {
                    if (nextNode.Value is AuxActionItem)
                    {
                        AuxActionItem item = nextNode.Value as AuxActionItem;
                        if (item.CanActivate(thisTrain, thisTrain.SpeedMpS, false))
                            itemList.Add(nextNode.Value);
                    }
                    nextNode = nextNode.Next;
                }
                return (itemList);
            }

            //================================================================================================//
            /// <summary>
            /// Get list of items to be processed of particular type
            /// <\summary>

            public List<DistanceTravelledItem> GetActions(float distance, Type reqType)
            {
                List<DistanceTravelledItem> itemList = new List<DistanceTravelledItem>();

                bool itemsCollected = false;
                LinkedListNode<DistanceTravelledItem> nextNode = this.First;
                LinkedListNode<DistanceTravelledItem> prevNode;

                while (!itemsCollected && nextNode != null)
                {
                    if (nextNode.Value.RequiredDistance <= distance)
                    {
                        if (nextNode.Value.GetType() == reqType)
                        {
                            itemList.Add(nextNode.Value);
                            prevNode = nextNode;
                            nextNode = prevNode.Next;
                            this.Remove(prevNode);
                        }
                        else
                        {
                            nextNode = nextNode.Next;
                        }
                    }
                    else
                    {
                        itemsCollected = true;
                    }
                }

                return (itemList);
            }

            //================================================================================================//
            /// <summary>
            /// Get distance of last track clearance item
            /// <\summary>

            public float? GetLastClearingDistance()
            {
                float? lastDistance = null;

                bool itemsCollected = false;
                LinkedListNode<DistanceTravelledItem> nextNode = this.Last;

                while (!itemsCollected && nextNode != null)
                {
                    if (nextNode.Value is ClearSectionItem)
                    {
                        lastDistance = nextNode.Value.RequiredDistance;
                        itemsCollected = true;
                    }
                    nextNode = nextNode.Previous;
                }

                return (lastDistance);
            }

            //================================================================================================//
            /// <summary>
            /// update any pending speed limits to new limit
            /// <\summary>

            public void UpdatePendingSpeedlimits(float reqSpeedMpS)
            {
                foreach (var thisAction in this)
                {
                    if (thisAction is ActivateSpeedLimit)
                    {
                        ActivateSpeedLimit thisLimit = (thisAction as ActivateSpeedLimit);

                        if (thisLimit.MaxSpeedMpSLimit > reqSpeedMpS)
                        {
                            thisLimit.MaxSpeedMpSLimit = reqSpeedMpS;
                        }
                        if (thisLimit.MaxSpeedMpSSignal > reqSpeedMpS)
                        {
                            thisLimit.MaxSpeedMpSSignal = reqSpeedMpS;
                        }
                        if (thisLimit.MaxTempSpeedMpSLimit > reqSpeedMpS)
                        {
                            thisLimit.MaxTempSpeedMpSLimit = reqSpeedMpS;
                        }
                    }
                }
            }

            //================================================================================================//
            /// <summary>
            /// remove any pending AIActionItems
            /// <\summary>

            public void RemovePendingAIActionItems(bool removeAll)
            {
                List<DistanceTravelledItem> itemsToRemove = new List<DistanceTravelledItem>();

                foreach (var thisAction in this)
                {
                    if ((thisAction is AIActionItem && !(thisAction is AuxActionItem)) || removeAll)
                    {
                        DistanceTravelledItem thisItem = thisAction;
                        itemsToRemove.Add(thisItem);
                    }
                }

                foreach (var thisAction in itemsToRemove)
                {
                    this.Remove(thisAction);
                }

            }


            //================================================================================================//
            /// <summary>
            /// Modifies required distance of actions after a train coupling
            /// <\summary>

            public void ModifyRequiredDistance(float Length)
            {
                foreach (var thisAction in this)
                {
                    if (thisAction is DistanceTravelledItem)
                    {
                        (thisAction as DistanceTravelledItem).RequiredDistance += Length;
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Distance Travelled action item - base class for all possible actions
        /// </summary>

        public class DistanceTravelledItem
        {
            public float RequiredDistance;

            //================================================================================================//
            //
            // Base contructor
            //

            public DistanceTravelledItem()
            {
            }

            //================================================================================================//
            //
            // Restore
            //

            public DistanceTravelledItem(BinaryReader inf)
            {
                RequiredDistance = inf.ReadSingle();
            }

            //================================================================================================//
            //
            // Save
            //

            public void Save(BinaryWriter outf)
            {
                if (this is ActivateSpeedLimit)
                {
                    outf.Write(1);
                    outf.Write(RequiredDistance);
                    ActivateSpeedLimit thisLimit = this as ActivateSpeedLimit;
                    thisLimit.SaveItem(outf);
                }
                else if (this is ClearSectionItem)
                {
                    outf.Write(2);
                    outf.Write(RequiredDistance);
                    ClearSectionItem thisSection = this as ClearSectionItem;
                    thisSection.SaveItem(outf);
                }
                else if (this is AIActionItem && !(this is AuxActionItem))
                {
                    outf.Write(3);
                    outf.Write(RequiredDistance);
                    AIActionItem thisAction = this as AIActionItem;
                    thisAction.SaveItem(outf);
                }
                else if (this is AuxActionItem)
                {
                    outf.Write(4);
                    outf.Write(RequiredDistance);
                    AuxActionItem thisAction = this as AuxActionItem;
                    thisAction.SaveItem(outf);
                }
                else
                {
                    outf.Write(-1);
                }

            }
        }

        //================================================================================================//
        /// <summary>
        /// Distance Travelled Clear Section action item
        /// </summary>

        public class ClearSectionItem : DistanceTravelledItem
        {
            public int TrackSectionIndex;  // in case of CLEAR_SECTION  //

            //================================================================================================//
            /// <summary>
            /// constructor for clear section
            /// </summary>

            public ClearSectionItem(float distance, int sectionIndex)
            {
                RequiredDistance = distance;
                TrackSectionIndex = sectionIndex;
            }

            //================================================================================================//
            //
            // Restore
            //

            public ClearSectionItem(BinaryReader inf)
                : base(inf)
            {
                TrackSectionIndex = inf.ReadInt32();
            }

            //================================================================================================//
            //
            // Save
            //

            public void SaveItem(BinaryWriter outf)
            {
                outf.Write(TrackSectionIndex);
            }


        }

        //================================================================================================//
        /// <summary>
        /// Distance Travelled Speed Limit Item
        /// </summary>

        public class ActivateSpeedLimit : DistanceTravelledItem
        {
            public float MaxSpeedMpSLimit = -1;
            public float MaxSpeedMpSSignal = -1;
            public float MaxTempSpeedMpSLimit = -1;

            //================================================================================================//
            /// <summary>
            /// constructor for speedlimit value
            /// </summary>

            public ActivateSpeedLimit(float reqDistance, float maxSpeedMpSLimit, float maxSpeedMpSSignal, float maxTempSpeedMpSLimit = -1)
            {
                RequiredDistance = reqDistance;
                MaxSpeedMpSLimit = maxSpeedMpSLimit;
                MaxSpeedMpSSignal = maxSpeedMpSSignal;
                MaxTempSpeedMpSLimit = maxTempSpeedMpSLimit;
            }

            //================================================================================================//
            //
            // Restore
            //

            public ActivateSpeedLimit(BinaryReader inf)
                : base(inf)
            {
                MaxSpeedMpSLimit = inf.ReadSingle();
                MaxSpeedMpSSignal = inf.ReadSingle();
                MaxTempSpeedMpSLimit = inf.ReadSingle();
            }

            //================================================================================================//
            //
            // Save
            //

            public void SaveItem(BinaryWriter outf)
            {
                outf.Write(MaxSpeedMpSLimit);
                outf.Write(MaxSpeedMpSSignal);
                outf.Write(MaxTempSpeedMpSLimit);
            }
        }

        public class ClearMovingTableAction : DistanceTravelledItem
        {
            public float OriginalMaxTrainSpeedMpS;                // original train speed

            //================================================================================================//
            /// <summary>
            /// constructor for speedlimit value
            /// </summary>

            public ClearMovingTableAction(float reqDistance, float maxSpeedMpSLimit)
            {
                RequiredDistance = reqDistance;
                OriginalMaxTrainSpeedMpS = maxSpeedMpSLimit;
            }

            //================================================================================================//
            //
            // Restore
            //

            public ClearMovingTableAction(BinaryReader inf)
                : base(inf)
            {
                OriginalMaxTrainSpeedMpS = inf.ReadSingle();
            }

            //================================================================================================//
            //
            // Save
            //

            public void SaveItem(BinaryWriter outf)
            {
                outf.Write(OriginalMaxTrainSpeedMpS);
            }

        }

        //used by remote train to update location based on message received
        public int expectedTileX, expectedTileZ, expectedTracIndex, expectedDIr, expectedTDir;
        public float expectedX, expectedZ, expectedTravelled, expectedLength;
        public bool updateMSGReceived;
        public bool RequestJump { get; internal set; } // set when a train jump has been requested by the server (when player re-enters game in old position
        private bool jumpRequested; // used in conjunction with above flag to manage thread safety
        public bool doReverseTrav; // reverse rear traveller in AI reversal points
        public int doReverseMU;

        public void ToDoUpdate(int tni, int tX, int tZ, float x, float z, float eT, float speed, int dir, int tDir, float len, bool reverseTrav = false,
            int reverseMU = 0)
        {
            SpeedMpS = speed;
            expectedTileX = tX;
            expectedTileZ = tZ;
            expectedX = x;
            expectedZ = z;
            expectedTravelled = eT;
            expectedTracIndex = tni;
            expectedDIr = dir;
            expectedTDir = tDir;
            expectedLength = len;
            if (reverseTrav)
            {
                doReverseTrav = true;
                doReverseMU = reverseMU;
            }
            updateMSGReceived = true;
        }

        private void UpdateCarSlack(float expectedLength)
        {
            if (Cars.Count <= 1) return;
            var staticLength = 0f;
            foreach (var car in Cars)
            {
                staticLength += car.CarLengthM;
            }
            staticLength = (expectedLength - staticLength) / (Cars.Count - 1);
            foreach (var car in Cars)//update slack for each car
            {
                car.CouplerSlackM = staticLength - car.GetCouplerZeroLengthM();
            }

        }
        public void UpdateRemoteTrainPos(double elapsedClockSeconds)
        {
            float newDistanceTravelledM = DistanceTravelledM;
            //           float xx = 0;

            if (updateMSGReceived)
            {
                updateMSGReceived = false;
                try
                {
                    targetSpeedMpS = SpeedMpS;
                    if (doReverseTrav)
                    {
                        doReverseTrav = false;
                        ReverseFormation(doReverseMU == 1 ? true : false);
                        UpdateCarSlack(expectedLength);//update car slack first
                        CalculatePositionOfCars(elapsedClockSeconds, SpeedMpS * elapsedClockSeconds);
                        newDistanceTravelledM = DistanceTravelledM + (float)(SpeedMpS * elapsedClockSeconds);
                        this.MUDirection = (MidpointDirection)expectedDIr;
                    }
                    else
                    {
                        UpdateCarSlack(expectedLength);//update car slack first

                        var x = travelled + LastSpeedMpS * elapsedClockSeconds + (SpeedMpS - LastSpeedMpS) / 2 * elapsedClockSeconds;
                        //                    xx = x;
                        this.MUDirection = (MidpointDirection)expectedDIr;

                        if (Math.Abs(x - expectedTravelled) < 1 || Math.Abs(x - expectedTravelled) > 20)
                        {
                            CalculatePositionOfCars(elapsedClockSeconds, expectedTravelled - travelled);
                            newDistanceTravelledM = DistanceTravelledM + expectedTravelled - travelled;

                            //if something wrong with the switch
                            if (this.RearTDBTraveller.TrackNodeIndex != expectedTracIndex)
                            {
                                Traveller t = null;
                                if (expectedTracIndex <= 0)
                                {
                                    t = new Traveller(simulator.TSectionDat, simulator.TDB.TrackDB.TrackNodes, new WorldLocation(expectedTileX, expectedTileZ, expectedX, 0, expectedZ), (Traveller.TravellerDirection)expectedTDir);
                                }
                                else
                                {
                                    t = new Traveller(simulator.TSectionDat, simulator.TDB.TrackDB.TrackNodes, simulator.TDB.TrackDB.TrackNodes[expectedTracIndex] as TrackVectorNode, new WorldLocation(expectedTileX, expectedTileZ, expectedX, 0, expectedZ), (Traveller.TravellerDirection)expectedTDir);
                                }
                                //move = SpeedMpS > 0 ? 0.001f : -0.001f;
                                this.travelled = expectedTravelled;
                                this.RearTDBTraveller = t;
                                CalculatePositionOfCars();

                            }
                        }
                        else//if the predicted location and reported location are similar, will try to increase/decrease the speed to bridge the gap in 1 second
                        {
                            SpeedMpS += (float)(expectedTravelled - x) / 1;
                            CalculatePositionOfCars(elapsedClockSeconds, SpeedMpS * elapsedClockSeconds);
                            newDistanceTravelledM = DistanceTravelledM + (float)(SpeedMpS * elapsedClockSeconds);
                        }
                        if (RequestJump)
                        {
                            jumpRequested = true;
                            RequestJump = false;
                        }
                    }
                }
                catch (Exception)
                {
                }
                /*if (Math.Abs(requestedSpeed) < 0.00001 && Math.Abs(SpeedMpS) > 0.01) updateMSGReceived = true; //if requested is stop, but the current speed is still moving
                else*/

            }
            else//no message received, will move at the previous speed
            {
                CalculatePositionOfCars(elapsedClockSeconds, SpeedMpS * elapsedClockSeconds);
                newDistanceTravelledM = DistanceTravelledM + (float)(SpeedMpS * elapsedClockSeconds);
            }

            //update speed for each car, so wheels will rotate
            foreach (TrainCar car in Cars)
            {
                if (car != null)
                {
                    car.SpeedMpS = SpeedMpS;
                    if (car.Flipped) car.SpeedMpS = -car.SpeedMpS;
                    car.AbsSpeedMpS = (float)(car.AbsSpeedMpS * (1 - elapsedClockSeconds) + targetSpeedMpS * elapsedClockSeconds);
                    if (car.IsDriveable && car is MSTSWagon)
                    {
                        (car as MSTSWagon).WheelSpeedMpS = SpeedMpS;
                        if (car.AbsSpeedMpS > 0.5f)
                        {
                            if (car is MSTSElectricLocomotive)
                            {
                                (car as MSTSElectricLocomotive).Variable1 = 70;
                                (car as MSTSElectricLocomotive).Variable2 = 70;
                            }
                            else if (car is MSTSDieselLocomotive)
                            {
                                (car as MSTSDieselLocomotive).Variable1 = 0.7f;
                                (car as MSTSDieselLocomotive).Variable2 = 0.7f;
                            }
                            else if (car is MSTSSteamLocomotive)
                            {
                                (car as MSTSSteamLocomotive).Variable1 = car.AbsSpeedMpS / car.DriverWheelRadiusM / MathHelper.Pi * 5;
                                (car as MSTSSteamLocomotive).Variable2 = 0.7f;
                            }
                        }
                        else if (car is MSTSLocomotive)
                        {
                            (car as MSTSLocomotive).Variable1 = 0;
                            (car as MSTSLocomotive).Variable2 = 0;
                        }
                    }
#if INDIVIDUAL_CONTROL
                if (car is MSTSLocomotive && car.CarID.StartsWith(MPManager.GetUserName()))
                        {
                            car.Update(elapsedClockSeconds);
                        }
#endif
                }
            }
            //            Trace.TraceWarning("SpeedMpS {0}  LastSpeedMpS {1}  AbsSpeedMpS {2}  targetSpeedMpS {7} x {3}  expectedTravelled {4}  travelled {5}  newDistanceTravelledM {6}",
            //                SpeedMpS, LastSpeedMpS, Cars[0].AbsSpeedMpS, xx, expectedTravelled, travelled, newDistanceTravelledM, targetSpeedMpS);
            LastSpeedMpS = SpeedMpS;
            DistanceTravelledM = newDistanceTravelledM;

            //Orient();
            return;

        }

        /// <summary>
        /// Nullify valid routes
        /// </summary>
        public void ClearValidRoutes()
        {

            if (ValidRoute[0] != null)
            {
                int listIndex = PresentPosition[Direction.Forward].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[0], listIndex, routedForward);
                ClearDeadlocks();
            }

            ValidRoute[0] = null;
            LastReservedSection[0] = -1;

            if (ValidRoute[1] != null)
            {
                int listIndex = PresentPosition[Direction.Backward].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[1], listIndex, routedBackward);
            }
            ValidRoute[1] = null;
            LastReservedSection[1] = -1;
        }

        /// <summary>
        /// Clears reserved sections (used after manual switching)
        /// </summary>
        public void ClearReservedSections()
        {

            if (ValidRoute[0] != null)
            {
                int listIndex = PresentPosition[Direction.Forward].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[0], listIndex, routedForward);
                ClearDeadlocks();
            }

        }


        /// <summary>
        /// After turntable rotation, must find where it is
        /// </summary>
        /// 

        public void ReenterTrackSections(int trackNodeIndex, int trVectorSectionIndex, Vector3 finalFrontTravellerXNALocation, Vector3 finalRearTravellerXNALocation, Traveller.TravellerDirection direction)
        {
            FrontTDBTraveller = new Traveller(simulator.TSectionDat, simulator.TDB.TrackDB.TrackNodes, simulator.TDB.TrackDB.TrackNodes[trackNodeIndex],
                 Cars[0].WorldPosition.TileX, Cars[0].WorldPosition.TileZ, finalFrontTravellerXNALocation.X, -finalFrontTravellerXNALocation.Z, FrontTDBTraveller.Direction);
            RearTDBTraveller = new Traveller(simulator.TSectionDat, simulator.TDB.TrackDB.TrackNodes, simulator.TDB.TrackDB.TrackNodes[trackNodeIndex],
                Cars[0].WorldPosition.TileX, Cars[0].WorldPosition.TileZ, finalRearTravellerXNALocation.X, -finalRearTravellerXNALocation.Z, RearTDBTraveller.Direction);
            if (direction == Traveller.TravellerDirection.Backward)
            {
                FrontTDBTraveller.ReverseDirection();
                RearTDBTraveller.ReverseDirection();
            }

            ClearValidRoutes();
            PresentPosition[Direction.Forward].TrackCircuitSectionIndex = -1;
            TrackCircuitPartialPathRoute tempRoute = CalculateInitialTrainPosition();
            if (tempRoute.Count == 0)
            {
                throw new InvalidDataException("Position of train in turntable not clear");
            }

            SetInitialTrainRoute(tempRoute);
            CalculatePositionOfCars();
            ResetInitialTrainRoute(tempRoute);

            CalculatePositionOfCars();

            TrackNode tn = FrontTDBTraveller.TN;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction1 = (TrackDirection)FrontTDBTraveller.Direction;

            PresentPosition[Direction.Forward].SetPosition(tn.TrackCircuitCrossReferences, offset, direction1);
            PreviousPosition[Direction.Forward].UpdateFrom(PresentPosition[Direction.Forward]);

            if (TrainType == TrainType.Static)
            {
                ControlMode = TrainControlMode.Undefined;
                return;
            }

            if (simulator.Activity == null && !simulator.TimetableMode) ToggleToExplorerMode();
            else ToggleToManualMode();
            simulator.Confirmer.Confirm(CabControl.SignalMode, CabSetting.Off);
        }

        private static Direction MidPointDirectionToDirectionUnset(MidpointDirection midpointDirection)
        {
            return midpointDirection == MidpointDirection.Forward ? Direction.Forward :
                midpointDirection == MidpointDirection.Reverse ? Direction.Backward : (Direction)(-1);
        }

        //================================================================================================//
        /// <summary>
        /// returns if position is ahead of train
        /// <\summary>
        // without offset
        internal static bool IsAheadOfTrain(TrackCircuitSection section, TrackCircuitPosition position)
        {
            return IsAheadOfTrain(section, 0f, position);
        }

        // with offset
        internal static bool IsAheadOfTrain(TrackCircuitSection section, float offset, TrackCircuitPosition position)
        {
            if (null == section)
                throw new ArgumentNullException(nameof(section));
            if (null == position)
                throw new ArgumentNullException(nameof(position));

            float distanceAhead = TrackCircuitSection.GetDistanceBetweenObjects(
                position.TrackCircuitSectionIndex, position.Offset, position.Direction, section.Index, offset);
            return (distanceAhead > 0.0f);
        }

        internal bool IsMoving()
        {
            return PresentPosition[Direction.Forward].Offset != PreviousPosition[Direction.Forward].Offset;
        }
    }
    // class Train
}
