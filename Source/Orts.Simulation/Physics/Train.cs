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
// #define DEBUG_DEADLOCK
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
using Orts.Common.Logging;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.MultiPlayer;
using Orts.Settings;
using Orts.Simulation.AIs;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;
using Orts.Simulation.Signalling;
using Orts.Simulation.Timetables;
using Orts.Simulation.Track;

namespace Orts.Simulation.Physics
{
    public class Train
    {
        public List<TrainCar> Cars = new List<TrainCar>();           // listed front to back
        public int Number;
        public string Name;
        public static int TotalNumber = 1; // start at 1 (0 is reserved for player train)
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
        public int NPull;                                // Count of number of couplings being stretched (pulled)
        public int NPush;                                // Count of number of couplings being compressed (pushed)
        public float AdvancedCouplerDuplicationFactor = 2.0f;
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
        public bool TrainWindResistanceDependent
        {
            get
            {
                return Simulator.Settings.WindResistanceDependent;
            }
        }

        // Auxiliary Water Tenders
        public float MaxAuxTenderWaterMassKG;
        public bool IsAuxTenderCoupled = false;
        bool AuxTenderFound = false;
        string PrevWagonType;


        //To investigate coupler breaks on route
        private bool numOfCouplerBreaksNoted = false;
        public static int NumOfCouplerBreaks = 0;//Debrief Eval
        public bool DbfEvalValueChanged { get; set; }//Debrief Eval

        public TrainType TrainType { get; set; } = TrainType.Player;

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
        public float rearPositionOverlap = 25.0f;        // allowed overlap when slipping
        private float standardWaitTimeS = 60.0f;         // wait for 1 min before claim state
        private float backwardThreshold = 20;            // counter threshold to detect backward move

        protected SignalEnvironment signalRef; // reference to main Signals class: SPA change protected to public with get, set!
        internal TrackCircuitRoutePath TCRoute;                      // train path converted to TC base
        public TrackCircuitPartialPathRoute[] ValidRoute = new TrackCircuitPartialPathRoute[2] { null, null };  // actual valid path
        public TrackCircuitPartialPathRoute TrainRoute;                // partial route under train for Manual mode
        public bool ClaimState;                          // train is allowed to perform claim on sections
        public double actualWaitTimeS;                    // actual time waiting for signal
        public int movedBackward;                        // counter to detect backward move
        public float waitingPointWaitTimeS = -1.0f;      // time due at waiting point (PLAYER train only, valid in >= 0)

        public List<TrackCircuitSection> OccupiedTrack = new List<TrackCircuitSection>();

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
                if (Simulator.PlayerLocomotive == null)
                {
                    return false;
                }
                return this == Simulator.PlayerLocomotive.Train;
            }
        }

        public bool IsPlayerDriven => TrainType == TrainType.Player || TrainType == TrainType.AiPlayerDriven;

        public bool IsPlayable = false;
        public bool IsPathless = false;

        // End variables used for autopilot mode and played train switching

        public TrainRouted routedForward;                 // routed train class for forward moves (used in signalling)
        public TrainRouted routedBackward;                // routed train class for backward moves (used in signalling)

        public TrainControlMode ControlMode { get; set; } = TrainControlMode.Undefined;     // train control mode

        public OutOfControlReason OutOfControlReason { get; set; } = OutOfControlReason.UnDefined; // train out of control

        public TCPosition[] PresentPosition = new TCPosition[2] { new TCPosition(), new TCPosition() };         // present position : 0 = front, 1 = rear
        public TCPosition[] PreviousPosition = new TCPosition[2] { new TCPosition(), new TCPosition() };        // previous train position

        public float DistanceTravelledM;                                 // actual distance travelled
        public float ReservedTrackLengthM = 0.0f;                        // lenght of reserved section

        public float travelled;                                          // distance travelled, but not exactly
        public float targetSpeedMpS;                                    // target speed for remote trains; used for sound management
        public DistanceTravelledActions requiredActions = new DistanceTravelledActions(); // distance travelled action list
        public AuxActionsContainer AuxActionsContain;          // Action To Do during activity, like WP

        public float activityClearingDistanceM = 30.0f;        // clear distance to stopping point for activities
        public const float shortClearingDistanceM = 15.0f;     // clearing distance for short trains in activities
        public const float standardClearingDistanceM = 30.0f;  // standard clearing distance for trains in activities
        public const int standardTrainMinCarNo = 10;           // Minimum number of cars for a train to have standard clearing distance

        public float ClearanceAtRearM = -1;              // save distance behind train (when moving backward)
        public Signal RearSignalObject;            // direct reference to signal at rear (when moving backward)
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

        public EndAuthorityType[] EndAuthorityTypes = new EndAuthorityType[2] { EndAuthorityType.NoPathReserved, EndAuthorityType.NoPathReserved };

        public int[] LastReservedSection = new int[2] { -1, -1 };         // index of furthest cleared section (for NODE control)
        public float[] DistanceToEndNodeAuthorityM = new float[2];      // distance to end of authority
        public int LoopSection = -1;                                    // section where route loops back onto itself

        public bool nextRouteReady = false;                             // indication to activity.cs that a reversal has taken place

        // Deadlock Info : 
        // list of sections where deadlock begins
        // per section : list with trainno and end section
        public Dictionary<int, List<Dictionary<int, int>>> DeadlockInfo =
            new Dictionary<int, List<Dictionary<int, int>>>();

        private static double lastLogTime;
        private protected bool evaluateTrainSpeed;                  // logging of train speed required
        private protected int evaluationInterval;                   // logging interval
        private protected EvaluationLogContents evaluationContent;  // logging selection
        private protected string evaluationLogFile;                 // required datalog file

        public Simulator Simulator { get; protected set; }                   // reference to the simulator


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

        //================================================================================================//
        //
        // Constructor
        //

        void Init(Simulator simulator)
        {
            Simulator = simulator;
            allowedAbsoluteMaxSpeedSignalMpS = (float)Simulator.TRK.Route.SpeedLimit;
            allowedAbsoluteMaxSpeedLimitMpS = allowedAbsoluteMaxSpeedSignalMpS;
            allowedAbsoluteMaxTempSpeedLimitMpS = allowedAbsoluteMaxSpeedSignalMpS;
        }

        public Train(Simulator simulator)
        {
            Init(simulator);

            if (Simulator.IsAutopilotMode && TotalNumber == 1 && Simulator.TrainDictionary.Count == 0) TotalNumber = 0; //The autopiloted train has number 0
            Number = TotalNumber;
            TotalNumber++;
            SignalObjectItems = new List<SignalItemInfo>();
            signalRef = simulator.SignalEnvironment;
            Name = "";

            routedForward = new TrainRouted(this, 0);
            routedBackward = new TrainRouted(this, 1);
            AuxActionsContain = new AuxActionsContainer(this);
        }

        //================================================================================================//
        //
        // Constructor for Dummy entries used on restore
        // Signals is restored before Trains, links are restored by Simulator
        //

        public Train(Simulator simulator, int number)
        {
            Init(simulator);
            Number = number;
            routedForward = new TrainRouted(this, 0);
            routedBackward = new TrainRouted(this, 1);
            AuxActionsContain = new AuxActionsContainer(this);
        }

        //================================================================================================//
        //
        // Constructor for uncoupled trains
        // copy path info etc. from original train
        //

        public Train(Simulator simulator, Train orgTrain)
        {
            Init(simulator);
            Number = TotalNumber;
            Name = String.Concat(String.Copy(orgTrain.Name), TotalNumber.ToString());
            TotalNumber++;
            SignalObjectItems = new List<SignalItemInfo>();
            signalRef = simulator.SignalEnvironment;

            AuxActionsContain = new AuxActionsContainer(this);
            if (orgTrain.TrafficService != null)
            {
                TrafficService = new ServiceTraffics(orgTrain.TrafficService.Time);

                foreach (ServiceTrafficItem thisTrafficItem in orgTrain.TrafficService)
                {
                    TrafficService.Add(thisTrafficItem);
                }
            }

            if (orgTrain.TCRoute != null)
            {
                TCRoute = new TrackCircuitRoutePath(orgTrain.TCRoute);
            }

            ValidRoute[0] = new TrackCircuitPartialPathRoute(orgTrain.ValidRoute[0]);
            ValidRoute[1] = new TrackCircuitPartialPathRoute(orgTrain.ValidRoute[1]);

            DistanceTravelledM = orgTrain.DistanceTravelledM;

            if (orgTrain.requiredActions.Count > 0)
            {
                requiredActions = orgTrain.requiredActions.Copy();
            }

            routedForward = new TrainRouted(this, 0);
            routedBackward = new TrainRouted(this, 1);

            ControlMode = orgTrain.ControlMode;

            AllowedMaxSpeedMpS = orgTrain.AllowedMaxSpeedMpS;
            allowedMaxSpeedLimitMpS = orgTrain.allowedMaxSpeedLimitMpS;
            allowedMaxSpeedSignalMpS = orgTrain.allowedMaxSpeedSignalMpS;
            allowedAbsoluteMaxSpeedLimitMpS = orgTrain.allowedAbsoluteMaxSpeedLimitMpS;
            allowedAbsoluteMaxSpeedSignalMpS = orgTrain.allowedAbsoluteMaxSpeedSignalMpS;

            if (orgTrain.StationStops != null)
            {
                foreach (StationStop thisStop in orgTrain.StationStops)
                {
                    StationStop newStop = thisStop.CreateCopy();
                    StationStops.Add(newStop);
                }
            }
            else
            {
                StationStops = null;
            }

        }

        //================================================================================================//
        /// <summary>
        /// Restore
        /// <\summary>

        public Train(Simulator simulator, BinaryReader inf)
        {
            Init(simulator);

            routedForward = new TrainRouted(this, 0);
            routedBackward = new TrainRouted(this, 1);
            ColdStart = false;
            RestoreCars(simulator, inf);
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
            signalRef = simulator.SignalEnvironment;

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

            int totalOccTrack = inf.ReadInt32();
            for (int iTrack = 0; iTrack < totalOccTrack; iTrack++)
            {
                int sectionIndex = inf.ReadInt32();
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[sectionIndex];
                OccupiedTrack.Add(thisSection);
            }

            int totalHoldSignals = inf.ReadInt32();
            for (int iSignal = 0; iSignal < totalHoldSignals; iSignal++)
            {
                int thisHoldSignal = inf.ReadInt32();
                HoldingSignals.Add(thisHoldSignal);
            }

            int totalStations = inf.ReadInt32();
            for (int iStation = 0; iStation < totalStations; iStation++)
            {
                StationStop thisStation = new StationStop(inf);
                StationStops.Add(thisStation);
            }

            int prevStopAvail = inf.ReadInt32();
            if (prevStopAvail >= 0)
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

            int DelaySeconds = inf.ReadInt32();
            if (DelaySeconds < 0) // delay value (in seconds, as integer)
            {
                Delay = null;
            }
            else
            {
                Delay = TimeSpan.FromSeconds(DelaySeconds);
            }

            int totalPassedSignals = inf.ReadInt32();
            for (int iPassedSignal = 0; iPassedSignal < totalPassedSignals; iPassedSignal++)
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
                PresentPosition[0] = new TCPosition();
                PresentPosition[0].RestorePresentPosition(inf, this);
                PresentPosition[1] = new TCPosition();
                PresentPosition[1].RestorePresentRear(inf, this);
                PreviousPosition[0] = new TCPosition();
                PreviousPosition[0].RestorePreviousPosition(inf);

                PresentPosition[0].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
                PresentPosition[1].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
            }
            else
            {
                DistanceTravelledM = inf.ReadSingle();
                PresentPosition[0] = new TCPosition();
                PresentPosition[0].RestorePresentPositionDummy(inf, this);
                PresentPosition[1] = new TCPosition();
                PresentPosition[1].RestorePresentRearDummy(inf, this);
                PreviousPosition[0] = new TCPosition();
                PreviousPosition[0].RestorePreviousPositionDummy(inf);
            }
            travelled = DistanceTravelledM;
            int activeActions = inf.ReadInt32();
            for (int iAction = 0; iAction < activeActions; iAction++)
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
                        Trace.TraceWarning("Unknown type of DistanceTravelledItem (type {0}",
                                actionType.ToString());
                        break;
                }
            }

            AuxActionsContain = new AuxActionsContainer(this, inf, Simulator.RoutePath);
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
                        Simulator.PlayerLocomotive = LeadLocomotive;
                }

                // restore logfile
                if (evaluateTrainSpeed)
                {
                    CreateLogFile();
                }
            }
        }

        private void RestoreCars(Simulator simulator, BinaryReader inf)
        {
            int count = inf.ReadInt32();
            if (count > 0)
            {
                for (int i = 0; i < count; ++i)
                    Cars.Add(RollingStock.Restore(simulator, inf, this));
            }
        }

        static ServiceTraffics RestoreTrafficSDefinition(BinaryReader inf)
        {
            ServiceTraffics thisDefinition = new ServiceTraffics(inf.ReadInt32());

            int totalTrafficItems = inf.ReadInt32();

            for (int iTraffic = 0; iTraffic < totalTrafficItems; iTraffic++)
            {
                ServiceTrafficItem thisItem = RestoreTrafficItem(inf);
                thisDefinition.Add(thisItem);
            }

            return (thisDefinition);
        }

        static ServiceTrafficItem RestoreTrafficItem(BinaryReader inf)
        {
            ServiceTrafficItem thisTraffic = new ServiceTrafficItem(inf.ReadInt32(), inf.ReadInt32(), 0, inf.ReadSingle(), inf.ReadInt32());

            return (thisTraffic);
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


        //================================================================================================//
        /// <summary>
        /// save game state
        /// <\summary>

        public virtual void Save(BinaryWriter outf)
        {
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
            PresentPosition[0].Save(outf);
            PresentPosition[1].Save(outf);
            PreviousPosition[0].Save(outf);
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

        static void SaveTrafficSDefinition(BinaryWriter outf, ServiceTraffics thisTSD)
        {
            outf.Write(thisTSD.Time);
            outf.Write(thisTSD.Count);
            foreach (ServiceTrafficItem thisTI in thisTSD)
            {
                SaveTrafficItem(outf, thisTI);
            }
        }

        static void SaveTrafficItem(BinaryWriter outf, ServiceTrafficItem thisTI)
        {
            outf.Write(thisTI.ArrivalTime);
            outf.Write(thisTI.DepartTime);
            outf.Write(thisTI.DistanceDownPath);
            outf.Write(thisTI.PlatformStartID);
        }

        private void SaveDeadlockInfo(BinaryWriter outf)
        {
            outf.Write(DeadlockInfo.Count);
            foreach (KeyValuePair<int, List<Dictionary<int, int>>> thisInfo in DeadlockInfo)
            {
                outf.Write(thisInfo.Key);
                outf.Write(thisInfo.Value.Count);

                foreach (Dictionary<int, int> thisDeadlock in thisInfo.Value)
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
            AuxActionsContain.Save(outf, Convert.ToInt32(Math.Floor(Simulator.ClockTime)));
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
            if (((MSTSLocomotive)LeadLocomotive).UsingRearCab) presentIndex = -presentIndex;

            List<int> cabList = new List<int>();

            for (int i = 0; i < Cars.Count; i++)
            {
                if (SkipOtherUsersCar(i)) continue;
                var cab3d = Cars[i].HasFront3DCab || Cars[i].HasRear3DCab;
                var hasFrontCab = cab3d ? Cars[i].HasFront3DCab : Cars[i].HasFrontCab;
                var hasRearCab = cab3d ? Cars[i].HasRear3DCab : Cars[i].HasRearCab;
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
            if (Simulator.PlayerLocomotive != null && Simulator.PlayerLocomotive.Train == this)

                Simulator.PlayerLocomotive = newLead;

            return newLead;
        }

        //this function is needed for Multiplayer games as they do not need to have cabs, but need to know lead locomotives
        // Sets the Lead locomotive to the next in the consist
        public void LeadNextLocomotive()
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
            Trace.Assert(Simulator.PlayerLocomotive != null, "Player loco is null when trying to switch locos");
            Trace.Assert(Simulator.PlayerLocomotive.Train == this, "Trying to switch locos but not on player's train");

            int driveableCabs = 0;
            for (int i = 0; i < Cars.Count; i++)
            {
                if (SkipOtherUsersCar(i)) continue;
                if (Cars[i].HasFrontCab || Cars[i].HasFront3DCab) driveableCabs++;
                if (Cars[i].HasRearCab || Cars[i].HasRear3DCab) driveableCabs++;
            }
            if (driveableCabs < 2)
            {
                Simulator.Confirmer.Warning(CabControl.ChangeCab, CabSetting.Warn1);
                return false;
            }
            return true;
        }

        //================================================================================================//
        /// <summary>
        /// In multiplayer, don't want to switch to a locomotive which is player locomotive of another user
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private bool SkipOtherUsersCar(int i)
        {
            if (!MPManager.IsMultiPlayer()) return false;
            else
            {
                var thisUsername = MPManager.GetUserName();
                var skip = false;
                foreach (OnlinePlayer onlinePlayer in MPManager.OnlineTrains.Players.Values)
                {
                    // don't consider the present user
                    if (onlinePlayer.Username == thisUsername) continue;
                    if (onlinePlayer.LeadingLocomotiveID == Cars[i].CarID)
                    {
                        skip = true;
                        break;
                    }
                }
                return skip;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Flips the train if necessary so that the train orientation matches the lead locomotive cab direction
        /// </summary>

        //       public void Orient()
        //       {
        //           TrainCar lead = LeadLocomotive;
        //           if (lead == null || !(lead.Flipped ^ lead.GetCabFlipped()))
        //               return;
        //
        //           if (ControlMode == TRAIN_CONTROL.AUTO_SIGNAL || ControlMode == TRAIN_CONTROL.AUTO_NODE || ControlMode == TRAIN_CONTROL.MANUAL)
        //               return;
        //
        //           for (int i = Cars.Count - 1; i > 0; i--)
        //               Cars[i].CopyCoupler(Cars[i - 1]);
        //           for (int i = 0; i < Cars.Count / 2; i++)
        //           {
        //               int j = Cars.Count - i - 1;
        //               TrainCar car = Cars[i];
        //               Cars[i] = Cars[j];
        //               Cars[j] = car;
        //           }
        //           if (LeadLocomotiveIndex >= 0)
        //               LeadLocomotiveIndex = Cars.Count - LeadLocomotiveIndex - 1;
        //           for (int i = 0; i < Cars.Count; i++)
        //               Cars[i].Flipped = !Cars[i].Flipped;
        //
        //           Traveller t = FrontTDBTraveller;
        //           FrontTDBTraveller = new Traveller(RearTDBTraveller, Traveller.TravellerDirection.Backward);
        //           RearTDBTraveller = new Traveller(t, Traveller.TravellerDirection.Backward);
        //
        //           MUDirection = DirectionControl.Flip(MUDirection);
        //           MUReverserPercent = -MUReverserPercent;
        //       }

        //================================================================================================//
        /// <summary>
        /// Reverse train formation
        /// Only performed when train activates a reversal point
        /// NOTE : this routine handles the physical train orientation only, all related route settings etc. must be handled separately
        /// </summary>

        public void ReverseFormation(bool setMUParameters)
        {
            if (MPManager.IsMultiPlayer()) MPManager.BroadCast((new MSGFlip(this, setMUParameters, Number)).ToString()); // message contains data before flip
            ReverseCars();
            // Flip the train's travellers.
            var t = FrontTDBTraveller;
            FrontTDBTraveller = new Traveller(RearTDBTraveller, Traveller.TravellerDirection.Backward);
            RearTDBTraveller = new Traveller(t, Traveller.TravellerDirection.Backward);
            // If we are updating the controls...
            if (setMUParameters)
            {
                // Flip the controls.
                MUDirection = (MidpointDirection)((int)MUDirection * -1);
                MUReverserPercent = -MUReverserPercent;
            }
            if (!((this is AITrain && (this as AITrain).AI.PreUpdate) || TrainType == TrainType.Static)) FormationReversed = true;
        }

        //================================================================================================//
        /// <summary>
        /// Reverse cars and car order
        /// </summary>
        /// 

        public void ReverseCars()
        {
            // Shift all the coupler data along the train by 1 car. Not sure whether this logic is correct, as it appears to give incorrect coupler information - To Be Checked
            for (var i = Cars.Count - 1; i > 0; i--)
            {
                Cars[i].CopyCoupler(Cars[i - 1]);
            }
            // Reverse brake hose connections and angle cocks
            for (var i = 0; i < Cars.Count; i++)
            {
                var ac = Cars[i].BrakeSystem.AngleCockAOpen;
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
            for (var i = 0; i < Cars.Count; i++)
                Cars[i].Flipped = !Cars[i].Flipped;
        }



        //================================================================================================//
        /// <summary>
        /// Someone is sending an event notification to all cars on this train.
        /// ie doors open, pantograph up, lights on etc.
        /// </summary>

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
            if (IsActualPlayerTrain && Simulator.ActiveMovingTable != null)
                Simulator.ActiveMovingTable.CheckTrainOnMovingTable(this);

            if (IsActualPlayerTrain && Simulator.OriginalPlayerTrain != this && !CheckStations) // if player train is to check own stations
            {
                CheckStationTask();
            }


            if (IsActualPlayerTrain && Simulator.Settings.ActRandomizationLevel > 0 && Simulator.ActivityRun != null) // defects might occur
            {
                CheckFailures(elapsedClockSeconds);
            }

            // Update train physics, position and movement

            physicsUpdate(elapsedClockSeconds);

            // Update the UiD of First Wagon
            FirstCarUiD = GetFirstWagonUiD();

            // Check to see if wagons are attached to train
            WagonsAttached = GetWagonsAttachedIndication();

            //Exit here when train is static consist (no further actions required)

            if (GetAIMovementState() == AITrain.AI_MOVEMENT_STATE.AI_STATIC)
            {
                int presentTime = Convert.ToInt32(Math.Floor(Simulator.ClockTime));
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
                int SignalObjIndex = CheckSignalPassed(0, PresentPosition[0], PreviousPosition[0]);   // check if passed signal  //
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
                LogTrainSpeed(Simulator.GameTime);
            }

        } // end Update

        //================================================================================================//
        /// <summary>
        /// Update train physics
        /// <\summary>

        public virtual void physicsUpdate(double elapsedClockSeconds)
        {
            //if out of track, will set it to stop
            if ((FrontTDBTraveller != null && FrontTDBTraveller.IsEnd) || (RearTDBTraveller != null && RearTDBTraveller.IsEnd))
            {
                if (FrontTDBTraveller.IsEnd && RearTDBTraveller.IsEnd)
                {//if both travellers are out, very rare occation, but have to treat it
                    RearTDBTraveller.ReverseDirection();
                    RearTDBTraveller.NextTrackNode();
                }
                else if (FrontTDBTraveller.IsEnd) RearTDBTraveller.Move(-1);//if front is out, move back
                else if (RearTDBTraveller.IsEnd) RearTDBTraveller.Move(1);//if rear is out, move forward
                foreach (var car in Cars) { car.SpeedMpS = 0; } //can set crash here by setting XNA matrix
                SignalEvent(TrainEvent.ResetWheelSlip);//reset everything to 0 power
            }

            if (TrainType == TrainType.Remote || updateMSGReceived == true) //server tolds me this train (may include mine) needs to update position
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

                        Trace.WriteLine(String.Format("Num of coupler breaks: {0}", NumOfCouplerBreaks));
                        numOfCouplerBreaksNoted = true;

                        if (Simulator.BreakCouplers)
                        {
                            Simulator.UncoupleBehind(uncoupleBehindCar, true);
                            uncoupleBehindCar.CouplerExceedBreakLimit = false;
                            Simulator.Confirmer.Warning(Simulator.Catalog.GetString("Coupler broken!"));
                        }
                        else
                            Simulator.Confirmer.Warning(Simulator.Catalog.GetString("Coupler overloaded!"));
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
            if (double.IsNaN(distanceM)) distanceM = 0;//avoid NaN, if so will not move
            if (TrainType == TrainType.Ai && LeadLocomotiveIndex == (Cars.Count - 1) && LastCar.Flipped)
                distanceM = -distanceM;
            DistanceTravelledM += (float)distanceM;

            SpeedMpS = 0;
            foreach (TrainCar car1 in Cars)
            {
                SpeedMpS += car1.SpeedMpS;
                //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                //                 if (car1.Flipped)
                if (car1.Flipped ^ (car1.IsDriveable && car1.Train.IsActualPlayerTrain && ((MSTSLocomotive)car1).UsingRearCab))
                    car1.SpeedMpS = -car1.SpeedMpS;
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

        //================================================================================================//
        /// <summary>
        /// Update Wind components for the train
        /// <\summary>

        public void UpdateWindComponents()
        {
            // Gets wind direction and speed, and determines HUD display values for the train as a whole. 
            //These will be representative of the train whilst it is on a straight track, but each wagon will vary when going around a curve.
            // Note both train and wind direction will be positive between 0 (north) and 180 (south) through east, and negative between 0 (north) and 180 (south) through west
            // Wind and train direction to be converted to an angle between 0 and 360 deg.
            if (TrainWindResistanceDependent)
            {
                // Calculate Wind speed and direction, and train direction
                // Update the value of the Wind Speed and Direction for the train
                PhysicsWindDirectionDeg = MathHelper.ToDegrees(Simulator.Weather.WindDirection);
                PhysicsWindSpeedMpS = Simulator.Weather.WindSpeed;
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
                if (Math.Abs(ResultantWindComponentDeg) > 360)
                    ResultantWindComponentDeg = ResultantWindComponentDeg - 360.0f;

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


        //================================================================================================//
        /// <summary>
        /// Update Auxiliary Tenders added to train
        /// <\summary>

        public void UpdateAuxTender()
        {

            var mstsSteamLocomotive = Cars[0] as MSTSSteamLocomotive;  // Don't process if locomotive is not steam locomotive
            if (mstsSteamLocomotive != null)
            {
                AuxTenderFound = false;    // Flag to confirm that there is still an auxiliary tender in consist
                // Calculate when an auxiliary tender is coupled to train
                for (int i = 0; i < Cars.Count; i++)
                {

                    if (Cars[i].AuxWagonType == "AuxiliaryTender" && i > LeadLocomotiveIndex && IsPlayerDriven)  // If value has been entered for auxiliary tender & AuxTender car value is greater then the lead locomotive & and it is player driven
                    {
                        PrevWagonType = Cars[i - 1].AuxWagonType;
                        if (PrevWagonType == "Tender" || PrevWagonType == "Engine")  // Aux tender found in consist
                        {
                            if (Simulator.Activity != null) // If an activity check to see if fuel presets are used.
                            {
                                if (mstsSteamLocomotive.AuxTenderMoveFlag == false)  // If locomotive hasn't moved and Auxtender connected use fuel presets on aux tender
                                {
                                    MaxAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                                    mstsSteamLocomotive.CurrentAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG * (Simulator.Activity.Activity.Header.FuelWater / 100.0f); // 
                                    IsAuxTenderCoupled = true;      // Flag to advise MSTSSteamLovcomotive that tender is set.
                                    AuxTenderFound = true;      // Auxililary tender found in consist.

                                }
                                else     // Otherwise assume aux tender not connected at start of activity and therefore full value of water mass available when connected.
                                {
                                    MaxAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                                    mstsSteamLocomotive.CurrentAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                                    IsAuxTenderCoupled = true;
                                    AuxTenderFound = true;      // Auxililary tender found in consist.
                                }
                            }
                            else  // In explore mode set aux tender to full water value
                            {
                                MaxAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                                mstsSteamLocomotive.CurrentAuxTenderWaterMassKG = Cars[i].AuxTenderWaterMassKG;
                                IsAuxTenderCoupled = true;
                                AuxTenderFound = true;      // Auxililary tender found in consist.

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

                if (AuxTenderFound == false && IsAuxTenderCoupled == true)     // If an auxiliary tender is not found in the consist, then assume that it has been uncoupled
                {
                    MaxAuxTenderWaterMassKG = 0.0f;     // Reset values
                    IsAuxTenderCoupled = false;
                }
            }

        }


        //================================================================================================//
        /// <summary>
        /// Update Steam Heating - this model calculates the total train heat losses and gains for all the cars
        /// <\summary>

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

        public void UpdateCarSteamHeat(double elapsedClockSeconds)
        {
            var mstsLocomotive = Simulator.PlayerLocomotive as MSTSLocomotive;
            if (mstsLocomotive != null)
            {

                if (IsFirstTimeBoilerCarAttached)
                {
                    for (int i = 0; i < Cars.Count; i++)
                    {
                        var car = Cars[i];
                        if (car.WagonSpecialType == MSTSWagon.WagonSpecialTypes.HeatingBoiler)
                        {
                            HeatingBoilerCarAttached = true; // A steam heating boiler is fitted in a wagon
                        }
                        if (car.WagonSpecialType == MSTSWagon.WagonSpecialTypes.Heated)
                        {
                            HeatedCarAttached = true; // A steam heating boiler is fitted in a wagon
                        }

                    }
                    IsFirstTimeBoilerCarAttached = false;
                }

                // Check to confirm that train is player driven and has passenger cars in the consist. Steam heating is OFF if steam heat valve is closed and no pressure is present
                if (IsPlayerDriven && (PassengerCarsNumber > 0 || HeatedCarAttached) && (mstsLocomotive.IsSteamHeatFitted || HeatingBoilerCarAttached) && mstsLocomotive.CurrentSteamHeatPressurePSI > 0)
                {
                    // Set default values required
                    float SteamFlowRateLbpHr = 0;
                    float ProgressiveHeatAlongTrainBTU = 0;
                    float ConnectSteamHoseLengthFt = 2.0f * 2.0f; // Assume two hoses on each car * 2 ft long

                    // Calculate total heat loss and car temperature along the train
                    for (int i = 0; i < Cars.Count; i++)
                    {
                        var car = Cars[i];
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

                            if (mstsLocomotive.EngineType == TrainCar.EngineTypes.Steam && Simulator.Settings.HotStart || mstsLocomotive.EngineType == TrainCar.EngineTypes.Diesel || mstsLocomotive.EngineType == TrainCar.EngineTypes.Electric)
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
                            car.SteamHoseLeakRateRandom = (float)Simulator.Random.Next(100) / 100.0f; // Achieves a two digit random number betwee 0 and 1
                            car.SteamHoseLeakRateRandom = MathHelper.Clamp(car.SteamHoseLeakRateRandom, 0.5f, 1.0f); // Keep Random Factor ratio within bounds

                            // Calculate Starting Heat value in Car Q = C * M * Tdiff, where C = Specific heat capacity, M = Mass ( Volume * Density), Tdiff - difference in temperature
                            car.TotalPossibleCarHeatW = (float)(Dynamics.Power.FromKW(SpecificHeatCapcityAirKJpKgK * DensityAirKgpM3 * car.CarHeatVolumeM3 * (car.CarCurrentCarriageHeatTempC - TrainOutsideTempC)));

                            //                            Trace.TraceInformation("Initialise TotalCarHeat - CarID {0} Possible {1} Max {2} Out {3} Vol {4} Density {5} Specific {6}", car.CarID, car.TotalPossibleCarHeatW, car.CarCurrentCarriageHeatTempC, TrainOutsideTempC, car.CarHeatVolumeM3, DensityAirKgpM3, SpecificHeatCapcityAirKJpKgC);

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
                                if (MayDepart) // If the train is ready to depart, assume all doors are closed, and hence no ventilation loss
                                {
                                    HeatLossVentilationWpT = 0;
                                }
                                else //
                                {
                                    HeatLossVentilationWpT = (float)Dynamics.Power.FromKW((1.0f - HeatRecoveryEfficiency) * SpecificHeatCapcityAirKJpKgK * DensityAirKgpM3 * AirFlowVolumeM3pS * (car.CarCurrentCarriageHeatTempC - car.CarOutsideTempC));
                                }
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
                        car.CarHeatConnectSteamHoseHeatLossBTU = (float)(ConnectSteamHoseLengthFt * (MathHelper.Pi * ConnectSteamHoseOuterDiaFt) * HeatTransCoeffConnectHoseBTUpFt2pHrpF * (CarMainSteamPipeTempF - Temperature.Celsius.ToF(car.CarOutsideTempC)));

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
                            ProgressiveHeatAlongTrainBTU += (float)((car.CarHeatSteamMainPipeHeatLossBTU + car.CarHeatConnectSteamHoseHeatLossBTU) + Frequency.Periodic.ToHours(Dynamics.Power.ToBTUpS(car.CarHeatCompartmentSteamPipeHeatW)));
                            CurrentComparmentSteamPipeHeatW = car.CarHeatCompartmentSteamPipeHeatW; // Car is being heated as main pipe pressure is high enough, and temperature increase is required
                            car.SteamHeatingCompartmentSteamTrapOn = true; // turn on the compartment steam traps
                        }
                        else
                        {
                            // If main pipe pressure is < 0 or temperature in compartment is above the desired temeperature,
                            // then no heating will occur in comparment, so leave compartment heat exchanger value out
                            ProgressiveHeatAlongTrainBTU += (car.CarHeatSteamMainPipeHeatLossBTU + car.CarHeatConnectSteamHoseHeatLossBTU);
                            CurrentComparmentSteamPipeHeatW = 0; // Car is not being heated as main pipe pressure is not high enough, or car temp is hot enough
                            car.SteamHeatingCompartmentSteamTrapOn = false; // turn off the compartment steam traps
                        }

                        // Calculate steam flow rates and steam used
                        SteamFlowRateLbpHr = (float)((ProgressiveHeatAlongTrainBTU / mstsLocomotive.SteamHeatPSItoBTUpLB[mstsLocomotive.CurrentSteamHeatPressurePSI]) + Frequency.Periodic.ToHours(car.CarHeatSteamTrapUsageLBpS) + Frequency.Periodic.ToHours(car.CarHeatConnectingSteamHoseLeakageLBpS));
                        mstsLocomotive.CalculatedCarHeaterSteamUsageLBpS = (float)Frequency.Periodic.FromHours(SteamFlowRateLbpHr);

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

                        float DesiredCompartmentAlarmTempSetpointC = (float)Temperature.Celsius.FromF(45.0f); // Alarm temperature
                        if (car.CarCurrentCarriageHeatTempC < DesiredCompartmentAlarmTempSetpointC) // If temp below 45of then alarm
                        {
                            if (!IsSteamHeatLow)
                            {
                                IsSteamHeatLow = true;
                                // Provide warning message if temperature is too hot
                                float CarTemp = car.CarCurrentCarriageHeatTempC;
                                if (car.WagonType == TrainCar.WagonTypes.Passenger)
                                {
                                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Carriage {0} temperature is too cold, the passengers are freezing.", car.CarID));
                                }
                                else
                                {
                                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Car {0} temperature is too cold for the freight.", car.CarID));
                                }
                            }

                        }
                        else if (car.CarCurrentCarriageHeatTempC > Temperature.Celsius.FromF(65.0f))
                        {

                            IsSteamHeatLow = false;        // Reset temperature warning
                        }
                    }

                    #region Calculate Steam Pressure drop along train

                    // Initialise main steam pipe pressure to same as steam heat valve setting
                    float ProgressivePressureAlongTrainPSI = mstsLocomotive.CurrentSteamHeatPressurePSI;

                    // Calculate pressure drop along whole train
                    for (int i = 0; i < Cars.Count; i++)
                    {
                        var car = Cars[i];

                        // Calculate pressure drop in pipe along train. This calculation is based upon the Unwin formula - https://www.engineeringtoolbox.com/steam-pressure-drop-calculator-d_1093.html
                        // dp = 0.0001306 * q^2 * L * (1 + 3.6/d) / (3600 * ρ * d^5)
                        // where dp = pressure drop (psi), q = steam flow rate(lb/ hr), L = length of pipe(ft), d = pipe inside diameter(inches), ρ = steam density(lb / ft3)
                        // Use values for the specific volume corresponding to the average pressure if the pressure drop exceeds 10 - 15 % of the initial absolute pressure

                        float HeatPipePressureDropPSI = (float)((0.0001306f * SteamFlowRateLbpHr * SteamFlowRateLbpHr * Size.Length.ToFt(car.CarLengthM) * (1 + 3.6f / 2.5f)) / (3600 * mstsLocomotive.SteamDensityPSItoLBpFT3[mstsLocomotive.CurrentSteamHeatPressurePSI] * (float)Math.Pow(car.MainSteamHeatPipeInnerDiaM, 5.0f)));
                        float ConnectHosePressureDropPSI = (float)((0.0001306f * SteamFlowRateLbpHr * SteamFlowRateLbpHr * ConnectSteamHoseLengthFt * (1 + 3.6f / 2.5f)) / (3600 * mstsLocomotive.SteamDensityPSItoLBpFT3[mstsLocomotive.CurrentSteamHeatPressurePSI] * (float)Math.Pow(car.CarConnectSteamHoseInnerDiaM, 5.0f)));
                        float CarPressureDropPSI = HeatPipePressureDropPSI + ConnectHosePressureDropPSI;

                        ProgressivePressureAlongTrainPSI -= CarPressureDropPSI;
                        if (ProgressivePressureAlongTrainPSI < 0)
                        {
                            ProgressivePressureAlongTrainPSI = 0; // Make sure that pressure never goes negative
                        }
                        car.CarSteamHeatMainPipeSteamPressurePSI = ProgressivePressureAlongTrainPSI;

                        // For the boiler heating car adjust mass based upon fuel and water usage
                        if (car.WagonSpecialType == MSTSWagon.WagonSpecialTypes.HeatingBoiler)
                        {

                            // Don't process if water or fule capacities are low
                            if (mstsLocomotive.CurrentSteamHeatPressurePSI > 0 && car.CurrentSteamHeatBoilerFuelCapacityL > 0 && car.CurrentCarSteamHeatBoilerWaterCapacityL > 0 && !car.IsSteamHeatBoilerLockedOut)
                            {
                                // Test boiler steam capacity can deliever steam required for the system
                                if (mstsLocomotive.CalculatedCarHeaterSteamUsageLBpS > car.MaximumSteamHeatingBoilerSteamUsageRateLbpS)
                                {
                                    car.IsSteamHeatBoilerLockedOut = true; // Lock steam heat boiler out is steam usage exceeds capacity
                                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("The steam usage has exceeded the capacity of the steam boiler. Steam boiler locked out."));
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
        }

        //================================================================================================//
        /// <summary>
        /// ProcessTunnels : check position of each car in train wrt tunnel
        /// <\summary>        

        public void ProcessTunnels()
        {
            // start at front of train
            int thisSectionIndex = PresentPosition[0].TCSectionIndex;
            float thisSectionOffset = PresentPosition[0].TCOffset;
            TrackDirection thisSectionDirection = (TrackDirection)PresentPosition[0].TCDirection;

            for (int icar = 0; icar <= Cars.Count - 1; icar++)
            {
                var car = Cars[icar];

                float usedCarLength = car.CarLengthM;
                float processedCarLength = 0;
                bool validSections = true;

                float? FrontCarPositionInTunnel = null;
                float? FrontCarLengthOfTunnelAhead = null;
                float? RearCarLengthOfTunnelBehind = null;
                int numTunnelPaths = 0;

                while (validSections)
                {
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
                    bool inTunnel = false;

                    // car spans sections
                    if ((car.CarLengthM - processedCarLength) > thisSectionOffset)
                    {
                        usedCarLength = thisSectionOffset - processedCarLength;
                    }

                    // section has tunnels
                    foreach (TunnelInfoData tunnel in thisSection.TunnelInfo ?? Enumerable.Empty<TunnelInfoData>())
                    {
                        float tunnelStartOffset = tunnel.Start[thisSectionDirection];
                        float tunnelEndOffset = tunnel.End[thisSectionDirection];

                        if (tunnelStartOffset > 0 && tunnelStartOffset > thisSectionOffset)      // start of tunnel is in section beyond present position - cannot be in this tunnel nor any following
                        {
                            break;
                        }

                        if (tunnelEndOffset > 0 && tunnelEndOffset < (thisSectionOffset - usedCarLength)) // beyond end of tunnel, test next
                        {
                            continue;
                        }

                        if (tunnelStartOffset <= 0 || tunnelStartOffset < (thisSectionOffset - usedCarLength)) // start of tunnel is behind
                        {
                            if (tunnelEndOffset < 0) // end of tunnel is out of this section
                            {
                                if (processedCarLength != 0)
                                {
                                    Trace.TraceInformation("Train : " + Name + " ; found tunnel in section " + thisSectionIndex + " with End < 0 while processed length : " + processedCarLength + "\n");
                                }
                            }

                            inTunnel = true;

                            numTunnelPaths = tunnel.NumberPaths;

                            // get position in tunnel
                            if (tunnelStartOffset < 0)
                            {
                                FrontCarPositionInTunnel = thisSectionOffset + tunnel.SectionStartOffset[thisSectionDirection];
                                FrontCarLengthOfTunnelAhead = tunnel.LengthTotal - FrontCarPositionInTunnel;
                                RearCarLengthOfTunnelBehind = tunnel.LengthTotal - (FrontCarLengthOfTunnelAhead + car.CarLengthM);
                            }
                            else
                            {
                                FrontCarPositionInTunnel = thisSectionOffset - tunnelStartOffset;
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
                        thisSectionOffset = thisSectionOffset - usedCarLength;   // position of next car in this section

                        car.CarTunnelData.FrontPositionBeyondStartOfTunnel = FrontCarPositionInTunnel.HasValue ? FrontCarPositionInTunnel : null;
                        car.CarTunnelData.LengthMOfTunnelAheadFront = FrontCarLengthOfTunnelAhead.HasValue ? FrontCarLengthOfTunnelAhead : null;
                        car.CarTunnelData.LengthMOfTunnelBehindRear = RearCarLengthOfTunnelBehind.HasValue ? RearCarLengthOfTunnelBehind : null;
                        car.CarTunnelData.numTunnelPaths = numTunnelPaths;
                    }
                    else
                    {
                        // go back one section
                        int thisSectionRouteIndex = ValidRoute[0].GetRouteIndexBackward(thisSectionIndex, PresentPosition[0].RouteListIndex);
                        if (thisSectionRouteIndex >= 0)
                        {
                            thisSectionIndex = thisSectionRouteIndex;
                            thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
                            thisSectionOffset = thisSection.Length;  // always at end of next section
                            thisSectionDirection = (TrackDirection)ValidRoute[0][thisSectionRouteIndex].Direction;
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

        //================================================================================================//
        /// <summary>
        /// Train speed evaluation logging - open file
        /// <\summary>

        public void CreateLogFile()
        {
            //Time, Train Speed, Max Speed, Signal Aspect, Elevation, Direction, Control Mode, Distance Travelled, Throttle, Brake, Dyn Brake, Gear

            StringBuilder stringBuild = new StringBuilder();

            if (!File.Exists(evaluationLogFile))
            {
                char Separator = (char)Simulator.Settings.DataLoggerSeparator;

                if ((evaluationContent & EvaluationLogContents.Time) == EvaluationLogContents.Time)
                {
                    stringBuild.Append("TIME");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Speed) == EvaluationLogContents.Speed)
                {
                    stringBuild.Append("TRAINSPEED");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.MaxSpeed) == EvaluationLogContents.MaxSpeed)
                {
                    stringBuild.Append("MAXSPEED");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.SignalAspect) == EvaluationLogContents.SignalAspect)
                {
                    stringBuild.Append("SIGNALASPECT");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Elevation) == EvaluationLogContents.Elevation)
                {
                    stringBuild.Append("ELEVATION");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Direction) == EvaluationLogContents.Direction)
                {
                    stringBuild.Append("DIRECTION");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.ControlMode) == EvaluationLogContents.ControlMode)
                {
                    stringBuild.Append("CONTROLMODE");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Distance) == EvaluationLogContents.Distance)
                {
                    stringBuild.Append("DISTANCETRAVELLED");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Throttle) == EvaluationLogContents.Throttle)
                {
                    stringBuild.Append("THROTTLEPERC");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Brake) == EvaluationLogContents.Brake)
                {
                    stringBuild.Append("BRAKEPRESSURE");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.DynBrake) == EvaluationLogContents.DynBrake)
                {
                    stringBuild.Append("DYNBRAKEPERC");
                    stringBuild.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Gear) == EvaluationLogContents.Gear)
                {
                    stringBuild.Append("GEARINDEX");
                    stringBuild.Append(Separator);
                }

                stringBuild.Append('\n');

                try
                {
                    File.AppendAllText(evaluationLogFile, stringBuild.ToString());
                }
                catch (Exception e)
                {
                    Trace.TraceWarning("Cannot open required logfile : " + evaluationLogFile + " : " + e.Message);
                    evaluateTrainSpeed = false;
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Train speed evaluation logging
        /// <\summary>

        public void LogTrainSpeed(double timeStamp)
        {
            if (lastLogTime + evaluationInterval >= timeStamp)
            {
                lastLogTime = timeStamp;

                // User settings flag indices :
                //Time, Train Speed, Max Speed, Signal Aspect, Elevation, Direction, Control Mode, Distance Travelled, Throttle, Brake, Dyn Brake, Gear

                StringBuilder builder = new StringBuilder();

                char Separator = (char)Simulator.Settings.DataLoggerSeparator;

                if ((evaluationContent & EvaluationLogContents.Time) == EvaluationLogContents.Time)
                {
                    builder.Append(FormatStrings.FormatTime(Simulator.ClockTime));
                    builder.Append(Separator);
                }

                bool moveForward = (Math.Sign(SpeedMpS) >= 0);
                if ((evaluationContent & EvaluationLogContents.Speed) == EvaluationLogContents.Speed)
                {
                    builder.Append(Speed.MeterPerSecond.FromMpS(Math.Abs(SpeedMpS), Simulator.MilepostUnitsMetric).ToString("0000.0"));
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.MaxSpeed) == EvaluationLogContents.MaxSpeed)
                {
                    builder.Append(Speed.MeterPerSecond.FromMpS(AllowedMaxSpeedMpS, Simulator.MilepostUnitsMetric).ToString("0000.0"));
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.SignalAspect) == EvaluationLogContents.SignalAspect)
                {
                    if (moveForward)
                    {
                        if (NextSignalObject[0] == null)
                        {
                            builder.Append("-");
                        }
                        else
                        {
                            SignalAspectState nextAspect = NextSignalObject[0].SignalLR(SignalFunction.Normal);
                            builder.Append(nextAspect.ToString());
                        }
                    }
                    else
                    {
                        if (NextSignalObject[1] == null)
                        {
                            builder.Append("-");
                        }
                        else
                        {
                            SignalAspectState nextAspect = NextSignalObject[1].SignalLR(SignalFunction.Normal);
                            builder.Append(nextAspect.ToString());
                        }
                    }
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Elevation) == EvaluationLogContents.Elevation)
                {
                    builder.Append((0 - Simulator.PlayerLocomotive.CurrentElevationPercent).ToString("00.0"));
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Direction) == EvaluationLogContents.Direction)
                {
                    if (moveForward)
                    {
                        builder.Append("F");
                    }
                    else
                    {
                        builder.Append("B");
                    }
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.ControlMode) == EvaluationLogContents.ControlMode)
                {
                    builder.Append(ControlMode.ToString());
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Distance) == EvaluationLogContents.Distance)
                {
                    builder.Append(PresentPosition[0].DistanceTravelledM.ToString());
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Throttle) == EvaluationLogContents.Throttle)
                {
                    builder.Append(MUThrottlePercent.ToString("000"));
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Brake) == EvaluationLogContents.Brake)
                {
                    //                    stringBuild.Append(BrakeLine1PressurePSIorInHg.ToString("000"));
                    builder.Append(Simulator.PlayerLocomotive.BrakeSystem.GetCylPressurePSI().ToString("000"));
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.DynBrake) == EvaluationLogContents.DynBrake)
                {
                    builder.Append(MUDynamicBrakePercent.ToString("000"));
                    builder.Append(Separator);
                }

                if ((evaluationContent & EvaluationLogContents.Gear) == EvaluationLogContents.Gear)
                {
                    builder.Append(MUGearboxGearIndex.ToString("0"));
                    builder.Append(Separator);
                }

                builder.Append('\n');
                File.AppendAllText(evaluationLogFile, builder.ToString());
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update in manual mode
        /// <\summary>

        public void UpdateManual(double elapsedClockSeconds)
        {
            UpdateTrainPosition();                                                                // position update                  //
            int SignalObjIndex = CheckSignalPassed(0, PresentPosition[0], PreviousPosition[0]);   // check if passed signal forward   //
            if (SignalObjIndex < 0)
            {
                SignalObjIndex = CheckSignalPassed(1, PresentPosition[1], PreviousPosition[1]);   // check if passed signal backward  //
            }
            if (SignalObjIndex >= 0)
            {
                var signalObject = signalRef.Signals[SignalObjIndex];

                //the following is added by CSantucci, applying also to manual mode what Jtang implemented for activity mode: after passing a manually forced signal,
                // system will take back control of the signal
                if (signalObject.HoldState == SignalHoldState.ManualPass ||
                    signalObject.HoldState == SignalHoldState.ManualApproach) signalObject.HoldState = SignalHoldState.None;
            }
            UpdateSectionStateManual();                                                           // update track occupation          //
            UpdateManualMode(SignalObjIndex);                                                     // update route clearance           //
            // for manual, also includes signal update //
        }

        //================================================================================================//
        /// <summary>
        /// Update in explorer mode
        /// <\summary>

        public void UpdateExplorer(double elapsedClockSeconds)
        {
            UpdateTrainPosition();                                                                // position update                  //
            int SignalObjIndex = CheckSignalPassed(0, PresentPosition[0], PreviousPosition[0]);   // check if passed signal forward   //
            if (SignalObjIndex < 0)
            {
                SignalObjIndex = CheckSignalPassed(1, PresentPosition[1], PreviousPosition[1]);   // check if passed signal backward  //
            }
            if (SignalObjIndex >= 0)
            {
                var signalObject = signalRef.Signals[SignalObjIndex];

                //the following is added by CSantucci, applying also to explorer mode what Jtang implemented for activity mode: after passing a manually forced signal,
                // system will take back control of the signal
                if (signalObject.HoldState == SignalHoldState.ManualPass ||
                    signalObject.HoldState == SignalHoldState.ManualApproach) signalObject.HoldState = SignalHoldState.None;
            }
            UpdateSectionStateExplorer();                                                         // update track occupation          //
            UpdateExplorerMode(SignalObjIndex);                                                   // update route clearance           //
            // for manual, also includes signal update //
        }

        //================================================================================================//
        /// <summary>
        /// Update in turntable mode
        /// <\summary>

        public void UpdateTurntable(double elapsedClockSeconds)
        {
            //           UpdateTrainPosition();                                                                // position update                  //
            if (LeadLocomotive != null && (LeadLocomotive.ThrottlePercent >= 1 || Math.Abs(LeadLocomotive.SpeedMpS) > 0.05 || !(LeadLocomotive.Direction == MidpointDirection.N
            || Math.Abs(MUReverserPercent) <= 1)) || ControlMode != TrainControlMode.TurnTable)
            // Go to emergency.
            {
                ((MSTSLocomotive)LeadLocomotive).SetEmergency(true);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Post Init : perform all actions required to start
        /// </summary>

        public virtual bool PostInit()
        {

            // if train has no valid route, build route over trainlength (from back to front)

            bool validPosition = InitialTrainPlacement();

            if (validPosition)
            {
                InitializeSignals(false);     // Get signal information - only if train has route //
                if (TrainType != TrainType.Static)
                    CheckDeadlock(ValidRoute[0], Number);    // Check deadlock against all other trains (not for static trains)
                if (TCRoute != null) TCRoute.SetReversalOffset(Length, Simulator.TimetableMode);

                AuxActionsContain.SetAuxAction(this);
            }


            // set train speed logging flag (valid per activity, so will be restored after save)

            if (IsActualPlayerTrain)
            {
                SetTrainSpeedLoggingFlag();


                // if debug, print out all passing paths

#if DEBUG_DEADLOCK
                Printout_PassingPaths();
#endif
            }

            return (validPosition);
        }

        //================================================================================================//
        /// <summary>
        /// set train speed logging flag (valid per activity, so will be restored after save)
        /// </summary>

        protected void SetTrainSpeedLoggingFlag()
        {
            evaluateTrainSpeed = Simulator.Settings.EvaluationTrainSpeed;
            evaluationInterval = Simulator.Settings.EvaluationInterval;

            evaluationContent = Simulator.Settings.EvaluationContent;

            // if logging required, derive filename and open file
            if (evaluateTrainSpeed)
            {
                evaluationLogFile = Simulator.DeriveLogFile("Speed");
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

        //================================================================================================//
        /// <summary>
        /// get aspect of next signal ahead
        /// </summary>

        public SignalAspectState GetNextSignalAspect(int direction)
        {
            SignalAspectState thisAspect = SignalAspectState.Stop;
            if (NextSignalObject[direction] != null)
            {
                thisAspect = NextSignalObject[direction].SignalLR(SignalFunction.Normal);
            }

            return thisAspect;
        }

        //================================================================================================//
        /// <summary>
        /// initialize signal array
        /// </summary>

        public void InitializeSignals(bool existingSpeedLimits)
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
                if ((TrainMaxSpeedMpS <= 0f) && (this.LeadLocomotive != null))
                    TrainMaxSpeedMpS = (this.LeadLocomotive as MSTSLocomotive).MaxSpeedMpS;
                AllowedMaxSpeedMpS = TrainMaxSpeedMpS;   // set default
                allowedMaxSpeedSignalMpS = TrainMaxSpeedMpS;   // set default
                allowedMaxTempSpeedLimitMpS = AllowedMaxSpeedMpS; // set default

                //  try to find first speed limits behind the train

                List<int> speedpostList = SignalEnvironment.ScanRoute(null, PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset,
                                (TrackDirection)PresentPosition[1].TCDirection, false, -1, false, true, false, false, false, false, false, true, false, IsFreight);

                if (speedpostList.Count > 0)
                {
                    var thisSpeedpost = signalRef.Signals[speedpostList[0]];
                    var speed_info = thisSpeedpost.SpeedLimit(SignalFunction.Speed);

                    AllowedMaxSpeedMpS = Math.Min(AllowedMaxSpeedMpS, IsFreight ? speed_info.FreightSpeed : speed_info.PassengerSpeed);
                    allowedAbsoluteMaxSpeedLimitMpS = Math.Min(allowedAbsoluteMaxSpeedLimitMpS, IsFreight ? speed_info.FreightSpeed : speed_info.PassengerSpeed);
                }

                float validSpeedMpS = AllowedMaxSpeedMpS;

                //  try to find first speed limits along train - scan back to front

                bool noMoreSpeedposts = false;
                int thisSectionIndex = PresentPosition[1].TCSectionIndex;
                float thisSectionOffset = PresentPosition[1].TCOffset;
                TrackDirection thisDirection = (TrackDirection)PresentPosition[1].TCDirection;
                float remLength = Length;

                while (!noMoreSpeedposts)
                {
                    speedpostList = SignalEnvironment.ScanRoute(null, thisSectionIndex, thisSectionOffset,
                            thisDirection, true, remLength, false, true, false, false, false, false, false, true, false, IsFreight);

                    if (speedpostList.Count > 0)
                    {
                        var thisSpeedpost = signalRef.Signals[speedpostList[0]];
                        var speed_info = thisSpeedpost.SpeedLimit(SignalFunction.Speed);
                        float distanceFromFront = Length - thisSpeedpost.DistanceTo(RearTDBTraveller);
                        if (distanceFromFront >= 0)
                        {
                            float newSpeedMpS = IsFreight ? speed_info.FreightSpeed : speed_info.PassengerSpeed;
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
                                    speed_info.LimitedSpeedReduction == 0 ? newSpeedMpS : -1, -1f,
                                    speed_info.LimitedSpeedReduction == 0 ? -1 : newSpeedMpS);
                                requiredActions.InsertAction(speedLimit);
                                requiredActions.UpdatePendingSpeedlimits(newSpeedMpS);  // update any older pending speed limits
                            }

                            if (newSpeedMpS < allowedAbsoluteMaxSpeedLimitMpS) allowedAbsoluteMaxSpeedLimitMpS = newSpeedMpS;
                            thisSectionIndex = thisSpeedpost.TrackCircuitIndex;
                            thisSectionOffset = thisSpeedpost.TrackCircuitOffset;
                            thisDirection = thisSpeedpost.TrackCircuitDirection;
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

            //  get first item from train (irrespective of distance)

            SignalItemFindState returnState = SignalItemFindState.None;
            float distanceToLastObject = 9E29f;  // set to overlarge value
            SignalAspectState nextAspect = SignalAspectState.Unknown;

            SignalItemInfo firstObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                PresentPosition[0].RouteListIndex, PresentPosition[0].TCOffset, -1,
                SignalItemType.Any);

            returnState = firstObject.State;
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

            int routeListIndex = PresentPosition[0].RouteListIndex;
            float offset = PresentPosition[0].TCOffset;
            int nextIndex = routeListIndex;

            while (returnState == SignalItemFindState.Item &&
                distanceToLastObject < maxDistance &&
                nextAspect != SignalAspectState.Stop)
            {
                int foundSection = -1;

                var thisSignal = prevObject.SignalDetails;

                int reqTCReference = thisSignal.TrackCircuitIndex;
                float reqOffset = thisSignal.TrackCircuitOffset + 0.0001f;   // make sure you find NEXT object ! //

                if (thisSignal.TrackCircuitNextIndex > 0)
                {
                    reqTCReference = thisSignal.TrackCircuitNextIndex;
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

            for (int isig = 0; isig < SignalObjectItems.Count && (!signalFound || !speedlimFound); isig++)
            {
                if (!signalFound)
                {
                    SignalItemInfo thisObject = SignalObjectItems[isig];
                    if (thisObject.ItemType == SignalItemType.Signal)
                    {
                        signalFound = true;
                        IndexNextSignal = isig;
                    }
                }

                if (!speedlimFound)
                {
                    SignalItemInfo thisObject = SignalObjectItems[isig];
                    if (thisObject.ItemType == SignalItemType.SpeedLimit)
                    {
                        speedlimFound = true;
                        IndexNextSpeedlimit = isig;
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
                    PresentPosition[0].RouteListIndex, PresentPosition[0].TCOffset, -1,
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

            updateSpeedInfo();
        }

        //================================================================================================//
        /// <summary>
        ///  Update the distance to and aspect of next signal
        /// </summary>

        public void UpdateSignalState(int backward)
        {
            // for AUTO mode, use direction 0 only
            SignalItemFindState returnState = SignalItemFindState.Item;

            bool listChanged = false;
            bool signalFound = false;
            bool speedlimFound = false;

            SignalItemInfo firstObject = null;

            //
            // get distance to first object
            //

            if (SignalObjectItems.Count > 0)
            {
                firstObject = SignalObjectItems[0];
                firstObject.DistanceToTrain = GetObjectDistanceToTrain(firstObject);


                //
                // check if passed object - if so, remove object
                // if object is speed, set max allowed speed as distance travelled action
                //

                while (firstObject.DistanceToTrain < 0.0f && SignalObjectItems.Count > 0)
                {
                    var temp1MaxSpeedMpS = IsFreight ? firstObject.SpeedInfo.FreightSpeed : firstObject.SpeedInfo.PassengerSpeed;
                    if (firstObject.SignalDetails.IsSignal)
                    {
                        allowedAbsoluteMaxSpeedSignalMpS = temp1MaxSpeedMpS == -1 ? (float)Simulator.TRK.Route.SpeedLimit : temp1MaxSpeedMpS;
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
                            if (!Simulator.TimetableMode)
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
                            else if (Simulator.TimetableMode || !firstObject.SpeedInfo.Reset)
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
                    else if (!Simulator.TimetableMode)
                    {
                        var tempMaxSpeedMps = IsFreight ? firstObject.SpeedInfo.FreightSpeed : firstObject.SpeedInfo.PassengerSpeed;
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

                    if (NextSignalObject[0] != null && firstObject.SignalDetails == NextSignalObject[0])
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

                //
                // if moving backward, check signals have been passed
                //

                if (backward > backwardThreshold)
                {

                    int newSignalIndex = -1;
                    bool noMoreNewSignals = false;

                    int thisIndex = PresentPosition[0].RouteListIndex;
                    float offset = PresentPosition[0].TCOffset;

                    while (!noMoreNewSignals)
                    {
                        SignalItemInfo newObjectItem = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                           thisIndex, offset, -1, SignalItemType.Signal);

                        returnState = newObjectItem.State;
                        if (returnState == SignalItemFindState.Item)
                        {
                            newSignalIndex = newObjectItem.SignalDetails.Index;

                            noMoreNewSignals = (NextSignalObject[0] == null || (NextSignalObject[0] != null && newSignalIndex == NextSignalObject[0].Index));

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

                                int foundIndex = ValidRoute[0].GetRouteIndex(newObjectItem.SignalDetails.TrackCircuitNextIndex, thisIndex);

                                if (foundIndex > 0)
                                {
                                    thisIndex = foundIndex;
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

            //
            // if no objects left on list, find first object whatever the distance
            //

            if (SignalObjectItems.Count <= 0)
            {
                firstObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                      PresentPosition[0].RouteListIndex, PresentPosition[0].TCOffset, -1,
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

            //
            // process further if any object available
            //

            if (SignalObjectItems.Count > 0)
            {

                //
                // Update state and speed of first object if signal
                //

                if (firstObject.SignalDetails.IsSignal)
                {
                    firstObject.SignalState = firstObject.SignalDetails.SignalLR(SignalFunction.Normal);
                    firstObject.SpeedInfo = new SpeedInfo(firstObject.SignalDetails.SignalSpeed(SignalFunction.Normal));
                }
                else if (firstObject.SignalDetails.SignalHeads != null)  // check if object is SPEED info signal
                {
                    if (firstObject.SignalDetails.SignalHeads[0].SignalFunction == SignalFunction.Speed)
                    {
                        firstObject.SpeedInfo = new SpeedInfo(firstObject.SignalDetails.SignalSpeed(SignalFunction.Speed));
                    }
                }

                //
                // Update all objects in list (except first)
                //

                float lastDistance = firstObject.DistanceToTrain;

                SignalItemInfo prevObject = firstObject;

                for (int isig = 1; isig < SignalObjectItems.Count && !signalFound; isig++)
                {
                    SignalItemInfo nextObject = SignalObjectItems[isig];
                    nextObject.DistanceToTrain = prevObject.DistanceToTrain + nextObject.DistanceToObject;
                    lastDistance = nextObject.DistanceToTrain;

                    if (nextObject.SignalDetails.IsSignal)
                    {
                        nextObject.SignalState = nextObject.SignalDetails.SignalLR(SignalFunction.Normal);
                        if (nextObject.SignalDetails.EnabledTrain != null && nextObject.SignalDetails.EnabledTrain.Train != this)
                            nextObject.SignalState = SignalAspectState.Stop; // state not valid if not enabled for this train
                        nextObject.SpeedInfo = new SpeedInfo(nextObject.SignalState == SignalAspectState.Stop ? null : nextObject.SignalDetails.SignalSpeed(SignalFunction.Normal));
                    }
                    else if (nextObject.SignalDetails.SignalHeads != null)  // check if object is SPEED info signal
                    {
                        if (nextObject.SignalDetails.SignalHeads[0].SignalFunction == SignalFunction.Speed)
                        {
                            nextObject.SpeedInfo = new SpeedInfo(nextObject.SignalDetails.SignalSpeed(SignalFunction.Speed));
                        }
                    }


                    prevObject = nextObject;
                }

                //
                // check if last signal aspect is STOP, and if last signal is enabled for this train
                // If so, no check on list is required
                //

                SignalAspectState nextAspect = SignalAspectState.Unknown;

                for (int isig = SignalObjectItems.Count - 1; isig >= 0 && !signalFound; isig--)
                {
                    SignalItemInfo nextObject = SignalObjectItems[isig];
                    if (nextObject.ItemType == SignalItemType.Signal)
                    {
                        signalFound = true;
                        nextAspect = nextObject.SignalState;
                    }
                }

                //
                // read next items if last item within max distance
                //

                float maxDistance = Math.Max(AllowedMaxSpeedMpS * maxTimeS, minCheckDistanceM);

                int routeListIndex = PresentPosition[0].RouteListIndex;
                int lastIndex = routeListIndex;
                float offset = PresentPosition[0].TCOffset;

                prevObject = SignalObjectItems[SignalObjectItems.Count - 1];  // last object

                while (lastDistance < maxDistance &&
                          returnState == SignalItemFindState.Item &&
                          nextAspect != SignalAspectState.Stop)
                {

                    var prevSignal = prevObject.SignalDetails;
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

                    SignalItemInfo nextObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                         lastIndex, offset, -1, SignalItemType.Any);

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

                //
                // check if IndexNextSignal still valid, if not, force list changed
                //

                if (IndexNextSignal >= SignalObjectItems.Count)
                {
                    listChanged = true;
                }
            }


            //
            // if list is changed, get new indices to first signal and speedpost
            //

            if (listChanged)
            {
                signalFound = false;
                speedlimFound = false;

                IndexNextSignal = -1;
                IndexNextSpeedlimit = -1;
                NextSignalObject[0] = null;

                for (int isig = 0; isig < SignalObjectItems.Count && (!signalFound || !speedlimFound); isig++)
                {
                    SignalItemInfo nextObject = SignalObjectItems[isig];
                    if (!signalFound && nextObject.ItemType == SignalItemType.Signal)
                    {
                        signalFound = true;
                        IndexNextSignal = isig;
                    }
                    else if (!speedlimFound && nextObject.ItemType == SignalItemType.SpeedLimit)
                    {
                        speedlimFound = true;
                        IndexNextSpeedlimit = isig;
                    }
                }
            }

            //
            // check if any signal in list, if not get direct from train
            // get state and details
            //

            if (IndexNextSignal < 0)
            {
                SignalItemInfo firstSignalObject = signalRef.GetNextObjectInRoute(routedForward, ValidRoute[0],
                        PresentPosition[0].RouteListIndex, PresentPosition[0].TCOffset, -1,
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

                if (this is AITrain)
                {
                    AITrain aiTrain = this as AITrain;

                    // do not switch to node control if train is set for auxiliary action
                    if (aiTrain.nextActionInfo != null && aiTrain.nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.AUX_ACTION)
                    {
                        validModeSwitch = false;
                    }
                }

                if (validModeSwitch)
                {
                    SwitchToNodeControl(LastReservedSection[0]);
                }
            }

            //
            // determine actual speed limits depending on overall speed and type of train
            //

            updateSpeedInfo();

        }

        //================================================================================================//
        /// <summary>
        /// set actual speed limit for all objects depending on state and type of train
        /// </summary>

        public void updateSpeedInfo()
        {
            float validSpeedMpS = AllowedMaxSpeedMpS;
            float validSpeedSignalMpS = allowedMaxSpeedSignalMpS;
            float validSpeedLimitMpS = allowedMaxSpeedLimitMpS;
            float validTempSpeedLimitMpS = allowedMaxTempSpeedLimitMpS;

            // update valid speed with pending actions

            foreach (var thisAction in requiredActions)
            {
                if (thisAction is ActivateSpeedLimit)
                {
                    ActivateSpeedLimit thisLimit = (thisAction as ActivateSpeedLimit);

                    if (thisLimit.MaxSpeedMpSLimit > validSpeedLimitMpS)
                    {
                        validSpeedLimitMpS = thisLimit.MaxSpeedMpSLimit;
                    }

                    if (thisLimit.MaxSpeedMpSSignal > validSpeedSignalMpS)
                    {
                        validSpeedSignalMpS = thisLimit.MaxSpeedMpSSignal;
                    }
                    if (thisLimit.MaxTempSpeedMpSLimit > validTempSpeedLimitMpS)
                    {
                        validTempSpeedLimitMpS = thisLimit.MaxTempSpeedMpSLimit;
                    }
                }
            }

            // loop through objects

            foreach (SignalItemInfo thisObject in SignalObjectItems)
            {
                //
                // select speed on type of train 
                //

                float actualSpeedMpS = IsFreight ? thisObject.SpeedInfo.FreightSpeed : thisObject.SpeedInfo.PassengerSpeed;

                if (thisObject.SignalDetails.IsSignal)
                {
                    if (actualSpeedMpS > 0 && (thisObject.SpeedInfo.Flag || !Simulator.TimetableMode))
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
                    thisObject.ActualSpeed = actualSpeedMpS;
                    if (actualSpeedMpS > 0)
                    {
                        validSpeedMpS = actualSpeedMpS;
                    }
                }
                else if (Simulator.TimetableMode)
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
                        else if (actualSpeedMpS < 0 && thisObject.SpeedInfo.Reset)
                        {
                            validSpeedMpS = validSpeedLimitMpS;
                            actualSpeedMpS = validSpeedLimitMpS;
                        }

                        thisObject.ActualSpeed = Math.Min(actualSpeedMpS, TrainMaxSpeedMpS);
                    }
                }

                else  // Enhanced Compatibility on & SpeedLimit
                {
                    if (actualSpeedMpS > 998f)
                    {
                        actualSpeedMpS = (float)Simulator.TRK.Route.SpeedLimit;
                    }

                    if (actualSpeedMpS > 0)
                    {
                        var tempValidSpeedSignalMpS = validSpeedSignalMpS == -1 ? 999 : validSpeedSignalMpS;
                        if (thisObject.SpeedInfo.LimitedSpeedReduction == 0)
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
                    else if (actualSpeedMpS < 0 && !thisObject.SpeedInfo.Reset)
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
                    else if (thisObject.SpeedInfo.Reset)
                    {
                        actualSpeedMpS = validSpeedLimitMpS;
                    }

                    thisObject.ActualSpeed = actualSpeedMpS;
                    if (actualSpeedMpS > 0)
                    {
                        validSpeedMpS = actualSpeedMpS;
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Initialize brakes
        /// <\summary>

        public virtual void InitializeBrakes()
        {
            if (Math.Abs(SpeedMpS) > 0.1)
            {
                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Warning(CabControl.InitializeBrakes, CabSetting.Warn1);
                return;
            }
            UnconditionalInitializeBrakes();
            return;
        }

        /// <summary>
        /// Initializes brakes also if Speed != 0; directly used by keyboard command
        /// <\summary>
        public void UnconditionalInitializeBrakes()
        {
            if (Simulator.Settings.SimpleControlPhysics && LeadLocomotiveIndex >= 0) // If brake and control set to simple, and a locomotive present, then set all cars to same brake system as the locomotive
            {
                MSTSLocomotive lead = (MSTSLocomotive)Cars[LeadLocomotiveIndex];
                if (lead.TrainBrakeController != null)
                {
                    foreach (MSTSWagon car in Cars)
                    {
                        if (lead.CarBrakeSystemType != car.CarBrakeSystemType) // Test to see if car brake system is the same as the locomotive
                        {
                            // If not, change so that they are compatible
                            car.CarBrakeSystemType = lead.CarBrakeSystemType;
                            if (lead.BrakeSystem is VacuumSinglePipe)
                                car.MSTSBrakeSystem = new VacuumSinglePipe(car);
                            else if (lead.BrakeSystem is AirTwinPipe)
                                car.MSTSBrakeSystem = new AirTwinPipe(car);
                            else if (lead.BrakeSystem is AirSinglePipe)
                            {
                                car.MSTSBrakeSystem = new AirSinglePipe(car);
                                // if emergency reservoir has been set on lead locomotive then also set on trailing cars
                                if (lead.EmergencyReservoirPresent)
                                {
                                    car.EmergencyReservoirPresent = lead.EmergencyReservoirPresent;
                                }
                            }
                            else if (lead.BrakeSystem is EPBrakeSystem)
                                car.MSTSBrakeSystem = new EPBrakeSystem(car);
                            else if (lead.BrakeSystem is SingleTransferPipe)
                                car.MSTSBrakeSystem = new SingleTransferPipe(car);
                            else
                                throw new Exception("Unknown brake type");

                            car.MSTSBrakeSystem.InitializeFromCopy(lead.BrakeSystem);
                            Trace.TraceInformation("Car and Locomotive Brake System Types Incompatible on Car {0} - Car brakesystem type changed to {1}", car.CarID, car.CarBrakeSystemType);
                        }
                    }
                }
            }

            if (Simulator.Confirmer != null && IsActualPlayerTrain) // As Confirmer may not be created until after a restore.
                Simulator.Confirmer.Confirm(CabControl.InitializeBrakes, CabSetting.Off);

            float maxPressurePSI = 90;
            float fullServPressurePSI = 64;
            if (FirstCar != null && FirstCar.BrakeSystem is VacuumSinglePipe)
            {
                maxPressurePSI = 21;
                fullServPressurePSI = 16;
            }

            if (LeadLocomotiveIndex >= 0)
            {
                MSTSLocomotive lead = (MSTSLocomotive)Cars[LeadLocomotiveIndex];
                if (lead.TrainBrakeController != null)
                {
                    var (pressurePSI, epControllerState) = lead.TrainBrakeController.UpdatePressure(EqualReservoirPressurePSIorInHg, BrakeLine4, 1000);
                    EqualReservoirPressurePSIorInHg = (float)pressurePSI;
                    BrakeLine4 = (float)epControllerState;
                    maxPressurePSI = lead.TrainBrakeController.MaxPressurePSI;
                    fullServPressurePSI = lead.BrakeSystem is VacuumSinglePipe ? 16 : maxPressurePSI - lead.TrainBrakeController.FullServReductionPSI;
                    EqualReservoirPressurePSIorInHg =
                            MathHelper.Max(EqualReservoirPressurePSIorInHg, fullServPressurePSI);
                }
                if (lead.EngineBrakeController != null)
                    BrakeLine3PressurePSI = (float)lead.EngineBrakeController.UpdateEngineBrakePressure(BrakeLine3PressurePSI, 1000);
                if (lead.DynamicBrakeController != null)
                {
                    MUDynamicBrakePercent = lead.DynamicBrakeController.Update(1000) * 100;
                    if (MUDynamicBrakePercent == 0)
                        MUDynamicBrakePercent = -1;
                }
                BrakeLine2PressurePSI = maxPressurePSI;
                ConnectBrakeHoses();
            }
            else
            {
                EqualReservoirPressurePSIorInHg = BrakeLine2PressurePSI = BrakeLine3PressurePSI = 0;
                // Initialize static consists airless for allowing proper shunting operations,
                // but set AI trains pumped up with air.
                if (TrainType == TrainType.Static)
                    maxPressurePSI = 0;
                BrakeLine4 = -1;
            }
            foreach (TrainCar car in Cars)
                car.BrakeSystem.Initialize(LeadLocomotiveIndex < 0, maxPressurePSI, fullServPressurePSI, false);
        }

        //================================================================================================//
        /// <summary>
        /// Set handbrakes
        /// <\summary>

        public void SetHandbrakePercent(float percent)
        {
            if (SpeedMpS < -.1 || SpeedMpS > .1)
                return;
            foreach (TrainCar car in Cars)
                car.BrakeSystem.SetHandbrakePercent(percent);
        }

        //================================================================================================//
        /// <summary>
        /// Connect brake hoses when train is initialised
        /// <\summary>

        public void ConnectBrakeHoses()
        {
            for (var i = 0; i < Cars.Count; i++)
            {
                Cars[i].BrakeSystem.FrontBrakeHoseConnected = i > 0;
                Cars[i].BrakeSystem.AngleCockAOpen = i > 0;
                Cars[i].BrakeSystem.AngleCockBOpen = i < Cars.Count - 1;
                // If end of train is not reached yet, then test the attached following car. If it is a manual braked car then set the brake cock on this car to closed.
                // Hence automatic brakes will operate to this point in the train.
                if (i < Cars.Count - 1)
                {
                    if (Cars[i + 1].CarBrakeSystemType == "manual_braking")
                    {
                        Cars[i].BrakeSystem.AngleCockBOpen = false;
                    }
                }
                Cars[i].BrakeSystem.BleedOffValveOpen = false;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Disconnect brakes
        /// <\summary>

        public void DisconnectBrakes()
        {
            if (SpeedMpS < -.1 || SpeedMpS > .1)
                return;
            int first = -1;
            int last = -1;
            FindLeadLocomotives(ref first, ref last);
            for (int i = 0; i < Cars.Count; i++)
            {
                Cars[i].BrakeSystem.FrontBrakeHoseConnected = first < i && i <= last;
                Cars[i].BrakeSystem.AngleCockAOpen = i != first;
                Cars[i].BrakeSystem.AngleCockBOpen = i != last;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set retainers
        /// <\summary>

        public void SetRetainers(bool increase)
        {
            if (SpeedMpS < -.1 || SpeedMpS > .1)
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
            int first = -1;
            int last = -1;
            FindLeadLocomotives(ref first, ref last);
            int step = 100 / RetainerPercent;
            for (int i = 0; i < Cars.Count; i++)
            {
                int j = Cars.Count - 1 - i;
                if (j <= last)
                    break;
                Cars[j].BrakeSystem.SetRetainer(i % step == 0 ? RetainerSetting : RetainerSetting.Exhaust);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Find lead locomotive
        /// <\summary>

        // FindLeadLocomotives stores the index of a single locomotive, or alternatively multiple locomotives, such as 
        // in the case of MU'd diesel units, the "first" and "last" values enclose the group of locomotives where the 
        // lead locomotive (the player driven one) resides. Within this group both the main reservoir pressure and the 
        // engine brake pipe pressure will be propagated. It only identifies multiple units when coupled directly together,
        // for example a double headed steam locomotive will most often have a tender separating the two locomotives, 
        // so the second locomotive will not be identified, nor will a locomotive added at the rear of the train. 

        public void FindLeadLocomotives(ref int first, ref int last)
        {
            first = last = -1;
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
        }

        public TrainCar FindLeadLocomotive()
        {
            int first = -1;
            int last = -1;
            FindLeadLocomotives(ref first, ref last);
            if (first != -1 && first < LeadLocomotiveIndex)
            {
                return Cars[first];
            }
            else if (last != -1 && last > LeadLocomotiveIndex)
            {
                return Cars[last];
            }
            for (int idx = 0; idx < Cars.Count(); idx++)
            {
                if (Cars[idx].IsDriveable)
                    return Cars[idx];
            }
            Trace.TraceWarning("Train {0} ({1}) has no locomotive!", Name, Number);
            return null;
        }

        //================================================================================================//
        /// <summary>
        /// Propagate brake pressure
        /// <\summary>

        public void PropagateBrakePressure(double elapsedClockSeconds)
        {
            if (LeadLocomotiveIndex >= 0)
            {
                MSTSLocomotive lead = (MSTSLocomotive)Cars[LeadLocomotiveIndex];
                if (lead.TrainBrakeController != null)
                {
                    var (pressurePSI, epControllerState) = lead.TrainBrakeController.UpdatePressure(EqualReservoirPressurePSIorInHg, BrakeLine4, elapsedClockSeconds);
                    EqualReservoirPressurePSIorInHg = (float)pressurePSI;
                    BrakeLine4 = (float)epControllerState;
                }
                if (lead.EngineBrakeController != null)
                    BrakeLine3PressurePSI = (float)lead.EngineBrakeController.UpdateEngineBrakePressure(BrakeLine3PressurePSI, elapsedClockSeconds);
                lead.BrakeSystem.PropagateBrakePressure(elapsedClockSeconds);
            }
            else if (TrainType == TrainType.Static)
            {
                // Propagate brake pressure of locomotiveless static consists in the advanced way,
                // to allow proper shunting operations.
                Cars[0].BrakeSystem.PropagateBrakePressure(elapsedClockSeconds);
            }
            else
            {
                // Propagate brake pressure of AI trains simplified
                AISetUniformBrakePressures();
            }
        }

        /// <summary>
        /// AI trains simplyfied brake control is done by setting their Train.BrakeLine1PressurePSIorInHg,
        /// that is propagated promptly to each car directly.
        /// </summary>
        private void AISetUniformBrakePressures()
        {
            foreach (TrainCar car in Cars)
            {
                car.BrakeSystem.BrakeLine1PressurePSI = car.BrakeSystem.InternalPressure(EqualReservoirPressurePSIorInHg);
                car.BrakeSystem.BrakeLine2PressurePSI = BrakeLine2PressurePSI;
                car.BrakeSystem.BrakeLine3PressurePSI = 0;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Cars have been added to the rear of the train, recalc the rearTDBtraveller
        /// </summary>

        public void RepositionRearTraveller()
        {
            var traveller = new Traveller(FrontTDBTraveller, Traveller.TravellerDirection.Backward);
            // The traveller location represents the front of the train.
            var length = 0f;

            // process the cars first to last
            for (var i = 0; i < Cars.Count; ++i)
            {
                var car = Cars[i];
                if (car.WheelAxlesLoaded)
                {
                    car.ComputePosition(traveller, false, 0, 0, SpeedMpS);
                }
                else
                {
                    var bogieSpacing = car.CarLengthM * 0.65f;  // we'll use this approximation since the wagfile doesn't contain info on bogie position

                    // traveller is positioned at the front of the car
                    // advance to the first bogie 
                    traveller.Move((car.CarLengthM - bogieSpacing) / 2.0f);
                    var tileX = traveller.TileX;
                    var tileZ = traveller.TileZ;
                    var x = traveller.X;
                    var y = traveller.Y;
                    var z = traveller.Z;
                    traveller.Move(bogieSpacing);

                    // normalize across tile boundaries
                    while (tileX > traveller.TileX)
                    {
                        x += 2048;
                        --tileX;
                    }
                    while (tileX < traveller.TileX)
                    {
                        x -= 2048;
                        ++tileX;
                    }
                    while (tileZ > traveller.TileZ)
                    {
                        z += 2048;
                        --tileZ;
                    }
                    while (tileZ < traveller.TileZ)
                    {
                        z -= 2048;
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


        //================================================================================================//
        /// <summary>
        /// Check if train is passenger or freight train
        /// </summary>

        public void CheckFreight()
        {
            IsFreight = false;
            PassengerCarsNumber = 0;
            IsPlayable = false;
            foreach (var car in Cars)
            {
                if (car.WagonType == TrainCar.WagonTypes.Freight)
                    IsFreight = true;
                if ((car.WagonType == TrainCar.WagonTypes.Passenger) || (car.IsDriveable && car.HasPassengerCapacity))
                    PassengerCarsNumber++;
                if (car.IsDriveable && (car as MSTSLocomotive).CabViewList.Count > 0) IsPlayable = true;
            }
            if (TrainType == TrainType.AiIncorporated && IncorporatingTrainNo > -1) IsPlayable = true;
        } // CheckFreight

        public void CalculatePositionOfCars()
        {
            CalculatePositionOfCars(0, 0);
        }

        //================================================================================================//
        /// <summary>
        /// Distance is the signed distance the cars are moving.
        /// </summary>
        /// <param name="distance"></param>

        public void CalculatePositionOfCars(double elapsedTime, double distance)
        {
            if (double.IsNaN(distance)) distance = 0;//sanity check

            RearTDBTraveller.Move(distance);

            // TODO : check if train moved back into previous section

            var traveller = new Traveller(RearTDBTraveller);
            // The traveller location represents the back of the train.
            var length = 0f;

            // process the cars last to first
            for (var i = Cars.Count - 1; i >= 0; --i)
            {
                var car = Cars[i];
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
                    var bogieSpacing = car.CarLengthM * 0.65f;  // we'll use this approximation since the wagfile doesn't contain info on bogie position

                    // traveller is positioned at the back of the car
                    // advance to the first bogie 
                    traveller.Move((car.CarLengthM - bogieSpacing) / 2.0f);
                    var tileX = traveller.TileX;
                    var tileZ = traveller.TileZ;
                    var x = traveller.X;
                    var y = traveller.Y;
                    var z = traveller.Z;
                    traveller.Move(bogieSpacing);

                    // normalize across tile boundaries
                    while (tileX > traveller.TileX)
                    {
                        x += 2048;
                        --tileX;
                    }
                    while (tileX < traveller.TileX)
                    {
                        x -= 2048;
                        ++tileX;
                    }
                    while (tileZ > traveller.TileZ)
                    {
                        z += 2048;
                        --tileZ;
                    }
                    while (tileZ < traveller.TileZ)
                    {
                        z -= 2048;
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

        //================================================================================================//
        /// <summary>
        ///  Sets this train's speed so that momentum is conserved when otherTrain is coupled to it
        /// <\summary>

        public void SetCoupleSpeed(Train otherTrain, float otherMult)
        {
            float kg1 = 0;
            foreach (TrainCar car in Cars)
                kg1 += car.MassKG;
            float kg2 = 0;
            foreach (TrainCar car in otherTrain.Cars)
                kg2 += car.MassKG;
            SpeedMpS = (kg1 * SpeedMpS + kg2 * otherTrain.SpeedMpS * otherMult) / (kg1 + kg2);
            otherTrain.SpeedMpS = SpeedMpS;
            foreach (TrainCar car1 in Cars)
                //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                //                 car1.SpeedMpS = car1.Flipped ? -SpeedMpS : SpeedMpS;
                car1.SpeedMpS = car1.Flipped ^ (car1.IsDriveable && car1.Train.IsActualPlayerTrain && ((MSTSLocomotive)car1).UsingRearCab) ? -SpeedMpS : SpeedMpS;
            foreach (TrainCar car2 in otherTrain.Cars)
                //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                //                 car2.SpeedMpS = car2.Flipped ? -SpeedMpS : SpeedMpS;
                car2.SpeedMpS = car2.Flipped ^ (car2.IsDriveable && car2.Train.IsActualPlayerTrain && ((MSTSLocomotive)car2).UsingRearCab) ? -SpeedMpS : SpeedMpS;
        }


        //================================================================================================//
        /// <summary>
        /// setups of the left hand side of the coupler force solving equations
        /// <\summary>

        void SetupCouplerForceEquations()
        {
            for (int i = 0; i < Cars.Count - 1; i++)
            {
                TrainCar car = Cars[i];
                car.CouplerForceB = 1 / car.MassKG;
                car.CouplerForceA = -car.CouplerForceB;
                car.CouplerForceC = -1 / Cars[i + 1].MassKG;
                car.CouplerForceB -= car.CouplerForceC;
            }
        }


        //================================================================================================//
        /// <summary>
        /// solves coupler force equations
        /// <\summary>


        void SolveCouplerForceEquations()
        {
            float b = Cars[0].CouplerForceB;
            Cars[0].CouplerForceU = Cars[0].CouplerForceR / b;


            for (int i = 1; i < Cars.Count - 1; i++)
            {
                Cars[i].CouplerForceG = Cars[i - 1].CouplerForceC / b;
                b = Cars[i].CouplerForceB - Cars[i].CouplerForceA * Cars[i].CouplerForceG;
                Cars[i].CouplerForceU = (Cars[i].CouplerForceR - Cars[i].CouplerForceA * Cars[i - 1].CouplerForceU) / b;
            }

            for (int i = Cars.Count - 3; i >= 0; i--)
            {
                Cars[i].CouplerForceU -= Cars[i + 1].CouplerForceG * Cars[i + 1].CouplerForceU;
            }

        }


        //================================================================================================//
        /// <summary>
        /// removes equations if forces don't match faces in contact
        /// returns true if a change is made
        /// <\summary>

        bool FixCouplerForceEquations()
        {

            // This section zeroes coupler forces if either of the simple or advanced coupler are in Zone 1, ie coupler faces not touching yet.
            // Simple coupler is almost a rigid symetrical couler
            // Advanced coupler can have different zone 1 dimension depending upon coupler type.

            // coupler in tension
            for (int i = 0; i < Cars.Count - 1; i++)
            {
                TrainCar car = Cars[i];

                // if coupler in compression on this car, or coupler is not to be solved, then jump car
                if (car.CouplerSlackM < 0 || car.CouplerForceB >= 1)
                    continue;

                if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
                {
                    float MaxZ1TensionM = car.GetMaximumCouplerTensionSlack1M() * AdvancedCouplerDuplicationFactor;
                    // If coupler in Zone 1 tension, ie ( -ve CouplerForceU ) then set coupler forces to zero, as coupler faces not touching yet

                    if (car.CouplerSlackM < MaxZ1TensionM && car.CouplerSlackM >= 0)
                    {
                        SetCouplerForce(car, 0);
                        return true;
                    }
                }
                else // "Simple coupler" - only operates on two extension zones, coupler faces not in contact, so set coupler forces to zero
                {
                    float maxs1 = car.GetMaximumSimpleCouplerSlack1M();
                    // In Zone 1 set coupler forces to zero, as coupler faces not touching , or if coupler force is in the opposite direction, ie compressing ( +ve CouplerForceU )
                    if (car.CouplerSlackM < maxs1 || car.CouplerForceU > 0)
                    {
                        SetCouplerForce(car, 0);
                        return true;
                    }
                }
            }

            // Coupler in compression
            for (int i = Cars.Count - 1; i >= 0; i--)
            {
                TrainCar car = Cars[i];

                // Coupler in tension on this car or coupler force is "zero" then jump to (process) next car
                if (car.CouplerSlackM > 0 || car.CouplerForceB >= 1)
                    continue;

                if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
                {
                    float MaxZ1CompressionM = -car.GetMaximumCouplerCompressionSlack1M() * AdvancedCouplerDuplicationFactor;

                    if (car.CouplerSlackM > MaxZ1CompressionM && car.CouplerSlackM < 0) // In Zone 1 set coupler forces to zero
                    {
                        SetCouplerForce(car, 0);
                        return true;
                    }
                }
                else // "Simple coupler" - only operates on two extension zones, coupler faces not in contact, so set coupler forces to zero
                {

                    float maxs1 = car.GetMaximumSimpleCouplerSlack1M();
                    // In Zone 1 set coupler forces to zero, as coupler faces not touching, or if coupler force is in the opposite direction, ie in tension ( -ve CouplerForceU )
                    if (car.CouplerSlackM > -maxs1 || car.CouplerForceU < 0)
                    {
                        SetCouplerForce(car, 0);
                        return true;
                    }
                }
            }
            return false;
        }


        //================================================================================================//
        /// <summary>
        /// changes the coupler force equation for car to make the corresponding force equal to forceN
        /// <\summary>

        static void SetCouplerForce(TrainCar car, float forceN)
        {
            car.CouplerForceA = car.CouplerForceC = 0;
            car.CouplerForceB = 1;
            car.CouplerForceR = forceN;
        }

        //================================================================================================//
        /// <summary>
        /// removes equations if forces don't match faces in contact
        /// returns true if a change is made
        /// <\summary>

        bool FixCouplerImpulseForceEquations()
        {
            // This section zeroes impulse coupler forces where there is a force mismatch, ie where coupler is in compression, and a tension force is applied, or vicer versa

            // coupler in tension - CouplerForce (-ve)
            for (int i = 0; i < Cars.Count - 1; i++)
            {
                TrainCar car = Cars[i];
                // if coupler in compression on this car, or coupler is not to be solved, then jump car
                if (car.CouplerSlackM < 0 || car.CouplerForceB >= 1) // if coupler in compression on this car, or coupler is not to be solved, then jump to next car and skip processing this one
                    continue;
                if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
                {
                    float MaxZ3TensionM = car.AdvancedCouplerDynamicTensionSlackLimitM;

                    if (car.CouplerSlackM < MaxZ3TensionM && car.CouplerSlackM >= 0 || car.CouplerForceU > 0) // If slack is less then coupler maximum extension, then set Impulse forces to zero
                    {
                        SetCouplerForce(car, 0);
                        return true;
                    }

                }
                else
                // Simple Coupler
                {
                    // Coupler is in tension according to slack measurement, but a tension force is present
                    if (car.CouplerSlackM < car.CouplerSlack2M || car.CouplerForceU > 0)
                    {
                        SetCouplerForce(car, 0);
                        return true;
                    }
                }
            }

            // Coupler in compression - CouplerForce (+ve)
            for (int i = Cars.Count - 1; i >= 0; i--)
            {
                TrainCar car = Cars[i];
                // Coupler in tension on this car or coupler force is "zero" then jump to next car
                if (car.CouplerSlackM > 0 || car.CouplerForceB >= 1)
                    continue;
                if (Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
                {
                    float MaxZ3CompressionM = car.AdvancedCouplerDynamicCompressionSlackLimitM;

                    if (car.CouplerSlackM > MaxZ3CompressionM && car.CouplerSlackM <= 0 || car.CouplerForceU < 0) // If slack is less then coupler maximum extension, then set Impulse forces to zero
                    {
                        SetCouplerForce(car, 0);
                        return true;
                    }
                }
                else // Simple coupler
                {
                    if (car.CouplerSlackM > -car.CouplerSlack2M || car.CouplerForceU < 0)
                    {
                        SetCouplerForce(car, 0);
                        return true;
                    }
                }
            }
            return false;
        }


        //================================================================================================//
        /// <summary>
        /// computes and applies coupler impulse forces which force speeds to match when no relative movement is possible
        /// <\summary>

        public void AddCouplerImpulseForces(double elapsedTime)
        {
            if (Cars.Count < 2)
                return;
            SetupCouplerForceEquations();

            for (int i = 0; i < Cars.Count - 1; i++)
            {
                TrainCar car = Cars[i];

                if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler"
                {
                    float MaxTensionCouplerLimitM = car.AdvancedCouplerDynamicTensionSlackLimitM;
                    float MaxCompressionCouplerLimitM = car.AdvancedCouplerDynamicCompressionSlackLimitM;

                    if (MaxCompressionCouplerLimitM < car.CouplerSlackM && car.CouplerSlackM < MaxTensionCouplerLimitM)
                    {
                        car.CouplerForceB = 1;
                        car.CouplerForceA = car.CouplerForceC = car.CouplerForceR = 0;
                    }
                    else
                        car.CouplerForceR = Cars[i + 1].SpeedMpS - car.SpeedMpS;
                }

                else // Simple coupler - set impulse force to zero if coupler slack has not exceeded zone 2 limit
                {
                    float max = car.CouplerSlack2M;
                    if (-max < car.CouplerSlackM && car.CouplerSlackM < max)
                    {
                        car.CouplerForceB = 1;
                        car.CouplerForceA = car.CouplerForceC = car.CouplerForceR = 0;
                    }
                    else
                        car.CouplerForceR = Cars[i + 1].SpeedMpS - car.SpeedMpS;
                }
            }

            do
                SolveCouplerForceEquations();
            while (FixCouplerImpulseForceEquations());
            MaximumCouplerForceN = 0;
            for (int i = 0; i < Cars.Count - 1; i++)
            {
                TrainCar car = Cars[i];

                // save impulse coupler force as it will be overwritten by "static" coupler force
                car.ImpulseCouplerForceUN = car.CouplerForceU;

                // This section seems to be required to get car moving
                if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler"
                {
                    Cars[i].SpeedMpS += Cars[i].CouplerForceU / Cars[i].MassKG;
                    Cars[i + 1].SpeedMpS -= Cars[i].CouplerForceU / Cars[i + 1].MassKG;

                    // This ensures that the last car speed never goes negative - as this might cause a sudden jerk in the train when viewed.
                    if (i == Cars.Count - 2)
                    {
                        if (FirstCar.SpeedMpS > 0 && Cars[i + 1].SpeedMpS < 0)
                        {
                            Cars[i + 1].SpeedMpS = 0;
                            //                                         Trace.TraceInformation("Last Car Zero Speed Set - CarID {0} - -ve set +ve", car.CarID);
                        }
                        else if (FirstCar.SpeedMpS < 0 && Cars[i + 1].SpeedMpS > 0)
                        {
                            Cars[i + 1].SpeedMpS = 0;
                            //                                        Trace.TraceInformation("Last Car Zero Speed Set - CarID {0} - +ve set -ve", car.CarID);
                        }
                    }


                }
                else // Simple Coupler
                {
                    Cars[i].SpeedMpS += Cars[i].CouplerForceU / Cars[i].MassKG;
                    Cars[i + 1].SpeedMpS -= Cars[i].CouplerForceU / Cars[i + 1].MassKG;
                }

                //if (Cars[i].CouplerForceU != 0)
                //    Console.WriteLine("impulse {0} {1} {2} {3} {4}", i, Cars[i].CouplerForceU, Cars[i].CouplerSlackM, Cars[i].SpeedMpS, Cars[i+1].SpeedMpS);
                //if (MaximumCouplerForceN < Math.Abs(Cars[i].CouplerForceU))
                //    MaximumCouplerForceN = Math.Abs(Cars[i].CouplerForceU);
            }
        }

        //================================================================================================//
        /// <summary>
        /// computes coupler acceleration balancing forces for Coupler
        /// The couplers are calculated using the formulas 9.7 to 9.9 (pg 243), described in the Handbook of Railway Vehicle Dynamics by Simon Iwnicki
        ///  In the book there is one equation per car and in OR there is one equation per coupler. To get the OR equations, first solve the 
        ///  equations in the book for acceleration. Then equate the acceleration equation for each pair of adjacent cars. Arrange the fwc 
        ///  terms on the left hand side and all other terms on the right side. Now if the fwc values are treated as unknowns, there is a 
        ///  tridiagonal system of linear equations which can be solved to find the coupler forces needed to make the accelerations match.
        ///  
        ///  Each fwc value corresponds to one of the CouplerForceU values.The CouplerForceA, CouplerForceB and CouplerForceC values are 
        ///  the CouplerForceU coefficients for the previuous coupler, the current coupler and the next coupler.The CouplerForceR values are 
        ///  the sum of the right hand side terms. The notation and the code in SolveCouplerForceEquations() that solves for the CouplerForceU 
        ///  values is from "Numerical Recipes in C".
        ///  
        /// Or has two coupler models - Simple and Advanced
        /// Simple - has two extension zones - #1 where the coupler faces have not come into contact, and hence CouplerForceU is zero, #2 where coupler 
        /// forces are taking the full weight of the following car. The breaking capacity of the coupler could be considered zone 3
        /// 
        /// Advanced - has three extension zones, and the breaking zone - #1 where the coupler faces have not come into contact, and hence 
        /// CouplerForceU is zero, #2 where the spring is taking the load, and car is able to oscilate in the train as it moves backwards and 
        /// forwards due to the action of the spring, #3 - where the coupler is fully extended against the friction brake, and the full force of the 
        /// following wagons will be applied to the coupler.
        /// 
        /// Coupler Force (CouplerForceU) : Fwd = -ve, Rev = +ve,  Total Force (TotalForceN): Fwd = -ve, Rev = +ve
        /// 
        /// <\summary>

        public void ComputeCouplerForces(double elapsedTime)
        {

            // TODO: this loop could be extracted and become a separate method, that could be called also by TTTrain.physicsPreUpdate
            for (int i = 0; i < Cars.Count; i++)
            {
                // If car is moving then the raw total force on each car is adjusted according to changing forces.
                if (Cars[i].SpeedMpS > 0)
                    Cars[i].TotalForceN -= (Cars[i].FrictionForceN + Cars[i].BrakeForceN + Cars[i].CurveForceN + Cars[i].WindForceN + Cars[i].TunnelForceN + Cars[i].DynamicBrakeForceN);
                else if (Cars[i].SpeedMpS < 0)
                    Cars[i].TotalForceN += Cars[i].FrictionForceN + Cars[i].BrakeForceN + Cars[i].CurveForceN + Cars[i].WindForceN + Cars[i].TunnelForceN + +Cars[i].DynamicBrakeForceN;
            }

            if (Cars.Count < 2)
                return;

            SetupCouplerForceEquations(); // Based upon the car Mass, set up LH side forces (ABC) parameters

            // Calculate RH side coupler force
            // Whilever coupler faces not in contact, then "zero coupler force" by setting A = C = R = 0
            // otherwise R is calculated based on difference in acceleration between cars, or stiffness and damping value

            for (int i = 0; i < Cars.Count - 1; i++)
            {
                TrainCar car = Cars[i];
                if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
                {

                    //Force on coupler is set so that no force is applied until coupler faces come into contact with each other
                    float MaxZ1TensionM = car.GetMaximumCouplerTensionSlack1M();
                    float MaxZ1CompressionM = -car.GetMaximumCouplerCompressionSlack1M();

                    float IndividualCouplerSlackM = car.CouplerSlackM / AdvancedCouplerDuplicationFactor;

                    if (IndividualCouplerSlackM >= MaxZ1CompressionM && IndividualCouplerSlackM <= MaxZ1TensionM) // Zone 1 coupler faces not in contact - no force generated
                    {
                        car.CouplerForceB = 1;
                        car.CouplerForceA = car.CouplerForceC = car.CouplerForceR = 0;
                    }
                    else
                    {
                        car.CouplerForceR = Cars[i + 1].TotalForceN / Cars[i + 1].MassKG - car.TotalForceN / car.MassKG;
                    }

                }
                else // "Simple coupler" - only operates on two extension zones, coupler faces not in contact, so set coupler forces to zero
                {
                    float max = car.GetMaximumSimpleCouplerSlack1M();
                    if (-max < car.CouplerSlackM && car.CouplerSlackM < max)
                    {
                        car.CouplerForceB = 1;
                        car.CouplerForceA = car.CouplerForceC = car.CouplerForceR = 0;
                    }
                    else
                        car.CouplerForceR = Cars[i + 1].TotalForceN / Cars[i + 1].MassKG - car.TotalForceN / car.MassKG;
                }
            }

            // Solve coupler forces to find CouplerForceU
            do
                SolveCouplerForceEquations();
            while (FixCouplerForceEquations());



            for (int i = 0; i < Cars.Count - 1; i++)
            {
                // Calculate total forces on cars
                TrainCar car = Cars[i];

                // Check to make sure that last car does not have any coulpler force on its coupler (no cars connected). When cars reversed, there is sometimes a "residual" coupler force.
                if (i == Cars.Count - 1 && Cars[i + 1].CouplerForceU != 0)
                {
                    Cars[i].CouplerForceU = 0;
                }

                car.CouplerForceUSmoothed.Update(elapsedTime, car.CouplerForceU);
                car.SmoothedCouplerForceUN = (float)car.CouplerForceUSmoothed.SmoothedValue;

                // Total force acting on each car is adjusted depending upon the calculated coupler forces
                car.TotalForceN += car.CouplerForceU;
                Cars[i + 1].TotalForceN -= car.CouplerForceU;

                // Find max coupler force on the car - currently doesn't appear to be used anywhere
                if (MaximumCouplerForceN < Math.Abs(car.CouplerForceU))
                    MaximumCouplerForceN = Math.Abs(car.CouplerForceU);

                // Update coupler slack which acts as the  upper limit in slack calculations
                // For the advanced coupler the slack limit is "dynamic", and depends upon the force applied to the coupler, and hence how far it will extend. 
                // This gives the effect that coupler extension will decrease down the train as the coupler force decreases. CouplerForce has a small smoothing 
                // effect to redcuce jerk, especially when starting.

                // As a coupler is defined in terms of force for one car only, then force/slack calculations need to be done with half the slack (IndividualCouplerSlackM) for calculation puposes.
                // The calculated slack will then be doubled to compensate.

                // The location of each car in the train is referenced from the last car in the train. Hence when starting a jerking motion can be present if the last car is still stationary
                // and the coupler slack increases and decreases along the train. This section of the code attempts to reduce this jerking motion by holding the coupler extension (slack) distance
                // to a "fixed" value until the last car has commenced moving. This is consistent with real life as the coupler would be extended as each car starts moving. 
                // A damping factor is also used to reduce any large variations during train start. CouplerForce is also smoothed slightly to also reduce any jerkiness

                if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
                {

                    // Note different slack lengths can be used depending upon whether the coupler is in tension or compression
                    // Rigid couplers use a fixed limit, and there is no variation.
                    float MaxZ1TensionM = car.GetMaximumCouplerTensionSlack1M();
                    float MaxZ2TensionM = car.GetMaximumCouplerTensionSlack2M();
                    float MaxZ3TensionM = car.GetMaximumCouplerTensionSlack3M();
                    float MaxZ1CompressionM = -car.GetMaximumCouplerCompressionSlack1M();
                    float MaxZ2CompressionM = -car.GetMaximumCouplerCompressionSlack2M();
                    float MaxZ3CompressionM = -car.GetMaximumCouplerCompressionSlack3M();

                    // Default initialisation of starting regions and limits for defining whether complete train is staring or in motion.
                    // Typically the train is considereded to be starting if the last car is moving at less then 0.25mps in either direction.
                    float LastCarZeroSpeedMpS = 0;
                    float LastCarCompressionMoveSpeedMpS = -0.025f;
                    float LastCarTensionMoveSpeedMpS = 0.025f;
                    float CouplerChangeDampingFactor = 0;

                    // The magnitude of the change factor is varied depending upon whether the train is completely in motion, or is just starting.
                    if (LastCar.SpeedMpS != FirstCar.SpeedMpS && LastCar.SpeedMpS > LastCarCompressionMoveSpeedMpS && LastCar.SpeedMpS < LastCarTensionMoveSpeedMpS)
                    {
                        CouplerChangeDampingFactor = 0.99f;
                    }
                    else if (LastCar.SpeedMpS == FirstCar.SpeedMpS)
                    {
                        CouplerChangeDampingFactor = 0.98f;
                    }
                    else
                    {
                        CouplerChangeDampingFactor = 0.98f;
                    }

                    // Default initialisation of limits
                    car.AdvancedCouplerDynamicTensionSlackLimitM = MaxZ3TensionM * AdvancedCouplerDuplicationFactor;
                    car.AdvancedCouplerDynamicCompressionSlackLimitM = MaxZ3CompressionM * AdvancedCouplerDuplicationFactor;
                    bool IsRigidCoupler = car.GetCouplerRigidIndication();

                    // For calculation purposes use only have the individual coupler distance between each car for calculations.
                    float IndividualCouplerSlackM = car.CouplerSlackM / AdvancedCouplerDuplicationFactor;

                    if (car.SmoothedCouplerForceUN < 0) // Tension
                    {
                        int Loop = 0;

                        if (!IsRigidCoupler)
                        {

                            if (IndividualCouplerSlackM < 0 && FirstCar.SpeedMpS > 0 && LastCar.SpeedMpS >= LastCarZeroSpeedMpS && LastCar.SpeedMpS < LastCarTensionMoveSpeedMpS && LeadLocomotive.Direction == MidpointDirection.Forward)
                            {
                                // Whilst train is starting in forward direction, don't allow negative coupler slack.

                                float TempDiff = car.PreviousCouplerSlackM * (1.0f - CouplerChangeDampingFactor);
                                if (car.PreviousCouplerSlackM - TempDiff > IndividualCouplerSlackM)
                                {
                                    car.CouplerSlackM = car.PreviousCouplerSlackM - TempDiff;
                                    car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                    Loop = 1;
                                }

                                //                                Trace.TraceInformation("Tension Slack -ve : CarID {0} Force {1} Slack {2} PrevSlack {3} TempDiff {4}", car.CarID, car. , car.CouplerSlackM, car.PreviousCouplerSlackM, TempDiff);
                            }
                            else if (FirstCar.SpeedMpS < 0 && LastCar.SpeedMpS > LastCarCompressionMoveSpeedMpS && LastCar.SpeedMpS <= LastCarZeroSpeedMpS && LeadLocomotive.Direction == MidpointDirection.Reverse)
                            {
                                if (IndividualCouplerSlackM > 0)
                                {
                                    // Train is starting to move reverse, don't allow positive coupler slack - should either be negative or zero
                                    car.CouplerSlackM = car.PreviousCouplerSlackM;
                                    car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, car.CouplerSlackM, 0);
                                    car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                    Loop = 2;
                                }
                                else if (IndividualCouplerSlackM < MaxZ1CompressionM)
                                {
                                    car.CouplerSlackM = MaxZ1CompressionM * AdvancedCouplerDuplicationFactor;
                                    car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                    Loop = 3;
                                }
                            }

                            else if (IndividualCouplerSlackM > MaxZ1TensionM && IndividualCouplerSlackM <= MaxZ3TensionM)
                            {
                                // A linear curve is assumed for coupler stiffness - this curve is then used to calculate the amount of slack that the coupler should have. 
                                //These values are set to "lock" the coupler at this maximum slack length

                                if (Math.Abs(car.SmoothedCouplerForceUN) < car.GetCouplerTensionStiffness1N())
                                {
                                    // Calculate coupler slack based upon force on coupler
                                    float SlackDiff = MaxZ2TensionM - MaxZ1TensionM;
                                    float GradStiffness = car.GetCouplerTensionStiffness1N() / (SlackDiff); // Calculate gradient of line
                                    float ComputedZone2SlackM = (Math.Abs(car.SmoothedCouplerForceUN) / GradStiffness) + MaxZ1TensionM; // Current slack distance in this zone of coupler

                                    if (LastCar.SpeedMpS >= LastCarZeroSpeedMpS && LastCar.SpeedMpS < LastCarTensionMoveSpeedMpS)
                                    {
                                        // Whilst train is starting
                                        if (ComputedZone2SlackM < (Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor))
                                        {
                                            // Train is starting, don't allow coupler slack to decrease untill complete train is moving
                                            car.CouplerSlackM = car.PreviousCouplerSlackM;
                                            car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                            Loop = 4;
                                        }
                                        else if (ComputedZone2SlackM > Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor)
                                        {
                                            // Allow coupler slack to slowly increase
                                            // Increase slack value
                                            car.CouplerSlackM = car.PreviousCouplerSlackM * (1.0f / CouplerChangeDampingFactor);
                                            car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, 0, MaxZ3TensionM * AdvancedCouplerDuplicationFactor);
                                            car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                            Loop = 5;
                                        }
                                    }
                                    //   else if (ComputedZone2SlackM < IndividualCouplerSlackM)
                                    else if (ComputedZone2SlackM < (Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor))
                                    {
                                        // Once train is moving then allow gradual reduction in coupler slack
                                        //    car.CouplerSlackM = ComputedZone2SlackM * AdvancedCouplerDuplicationFactor * CouplerChangeDampingFactor;
                                        car.CouplerSlackM = car.PreviousCouplerSlackM * CouplerChangeDampingFactor;
                                        car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                        Loop = 7;
                                    }
                                    else
                                    //       else if (ComputedZone2SlackM > IndividualCouplerSlackM)
                                    {
                                        // If train moving then allow coupler slack to increase depending upon the caclulated slack
                                        //    car.CouplerSlackM = ComputedZone2SlackM * AdvancedCouplerDuplicationFactor * (1.0f / CouplerChangeDampingFactor);
                                        car.CouplerSlackM = ComputedZone2SlackM * AdvancedCouplerDuplicationFactor * CouplerChangeDampingFactor;
                                        car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, 0, MaxZ3TensionM * AdvancedCouplerDuplicationFactor);
                                        car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                        Loop = 8;
                                    }

                                    //   Trace.TraceInformation("Zone 2 Tension - ID {0} Diff {1} Stiff {2} SmoothForce {3} CouplerForceN {4} Slack {5} ComputedSlack {6} MaxZ2 {7} IndSlack {8} LastSpeed {9} FinalDiff {10} ChangeFactror {11}", car.CarID, SlackDiff, GradStiffness, car.SmoothedCouplerForceUN, car.CouplerForceU, car.CouplerSlackM, ComputedZone2SlackM, MaxZ2TensionM, IndividualCouplerSlackM, LastCar.SpeedMpS, (car.CouplerSlackM - (IndividualCouplerSlackM * AdvancedCouplerDuplicationFactor)), CouplerChangeDampingFactor);
                                }
                                else
                                {
                                    // A linear curve is assumed for coupler stiffness - this curve is then used to calculate the amount of slack that the coupler should have. 
                                    //These values are set to "lock" the coupler at this maximum slack length
                                    float SlackDiff = MaxZ3TensionM - MaxZ2TensionM;
                                    float GradStiffness = (car.GetCouplerTensionStiffness2N() - car.GetCouplerTensionStiffness1N()) / (SlackDiff);
                                    float ComputedZone3SlackM = (Math.Abs(car.SmoothedCouplerForceUN) / GradStiffness) + MaxZ2TensionM;

                                    if (LastCar.SpeedMpS >= LastCarZeroSpeedMpS && LastCar.SpeedMpS < LastCarTensionMoveSpeedMpS)
                                    {
                                        // Train is starting, don't allow coupler slack to decrease until complete train is moving
                                        if (ComputedZone3SlackM < (Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor))
                                        {
                                            car.CouplerSlackM = car.PreviousCouplerSlackM;
                                            car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                            Loop = 9;
                                        }
                                        else if (ComputedZone3SlackM > Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor)
                                        {
                                            // Allow coupler slack to slowly increase

                                            // Increase slack value
                                            car.CouplerSlackM = car.PreviousCouplerSlackM * (1.0f / CouplerChangeDampingFactor);
                                            car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, 0, MaxZ3TensionM * AdvancedCouplerDuplicationFactor);
                                            car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                            Loop = 10;

                                        }
                                    }
                                    // else if (ComputedZone3SlackM < IndividualCouplerSlackM)
                                    else if (ComputedZone3SlackM < (Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor))
                                    {
                                        // Decrease coupler slack - moving
                                        // car.CouplerSlackM = ComputedZone3SlackM * AdvancedCouplerDuplicationFactor * CouplerChangeDampingFactor;
                                        car.CouplerSlackM = car.PreviousCouplerSlackM * CouplerChangeDampingFactor;
                                        car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                        Loop = 12;
                                    }
                                    else
                                    //     else if (ComputedZone3SlackM > IndividualCouplerSlackM)
                                    {
                                        // Allow coupler slack to be increased - moving
                                        car.CouplerSlackM = ComputedZone3SlackM * AdvancedCouplerDuplicationFactor * CouplerChangeDampingFactor;
                                        //    car.CouplerSlackM = ComputedZone3SlackM * AdvancedCouplerDuplicationFactor * (1.0f / CouplerChangeDampingFactor);
                                        car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, 0, MaxZ3TensionM * AdvancedCouplerDuplicationFactor);
                                        car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                        Loop = 13;
                                    }

                                    //   Trace.TraceInformation("Zone 3 Tension - ID {0} Diff {1} Stiff {2} SmoothForce {3} CouplerForceN {4} Slack {5} ComputedSlack {6} MaxZ3 {7} IndSlack {8} LastSpeed {9} FinalDiff {10} ChangeFactror {11}", car.CarID, SlackDiff, GradStiffness, car.SmoothedCouplerForceUN, car.CouplerForceU, car.CouplerSlackM, ComputedZone3SlackM, MaxZ3TensionM, IndividualCouplerSlackM, LastCar.SpeedMpS, (car.CouplerSlackM - (IndividualCouplerSlackM * AdvancedCouplerDuplicationFactor)), CouplerChangeDampingFactor);
                                }
                            }
                            else if (IndividualCouplerSlackM > MaxZ3TensionM)  // Make sure that a new computed slack value does not take slack into the next zone.
                            {
                                // If computed slack is higher then Zone 3 limit, then set to max Z3. 
                                if (LastCar.SpeedMpS >= LastCarZeroSpeedMpS && LastCar.SpeedMpS < LastCarTensionMoveSpeedMpS)
                                {
                                    car.CouplerSlackM = MaxZ3TensionM * AdvancedCouplerDuplicationFactor;
                                    car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                    Loop = 17;
                                }
                                else
                                {
                                    // A linear curve is assumed for coupler stiffness - this curve is then used to calculate the amount of slack that the coupler should have. 
                                    //These values are set to "lock" the coupler at this maximum slack length
                                    float SlackDiff = MaxZ3TensionM - MaxZ2TensionM;
                                    float GradStiffness = (car.GetCouplerTensionStiffness2N() - car.GetCouplerTensionStiffness1N()) / (SlackDiff);
                                    float ComputedZone4SlackM = (Math.Abs(car.SmoothedCouplerForceUN) / GradStiffness) + MaxZ2TensionM;

                                    if (ComputedZone4SlackM < MaxZ3TensionM && ComputedZone4SlackM > car.PreviousCouplerSlackM / AdvancedCouplerDuplicationFactor)
                                    {
                                        // Increase coupler slack
                                        car.CouplerSlackM = ComputedZone4SlackM * AdvancedCouplerDuplicationFactor * (1.0f / CouplerChangeDampingFactor);
                                        car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, 0, MaxZ3TensionM * AdvancedCouplerDuplicationFactor);
                                        car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                        Loop = 14;
                                    }
                                    else if (ComputedZone4SlackM > MaxZ3TensionM)
                                    {
                                        car.CouplerSlackM = MaxZ3TensionM * AdvancedCouplerDuplicationFactor;
                                        car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                        Loop = 15;
                                    }
                                    else if (ComputedZone4SlackM < MaxZ3TensionM && ComputedZone4SlackM < car.PreviousCouplerSlackM / AdvancedCouplerDuplicationFactor)
                                    {
                                        // Decrease coupler slack
                                        car.CouplerSlackM = car.PreviousCouplerSlackM * CouplerChangeDampingFactor;
                                        car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                        Loop = 16;
                                    }
                                }
                            }
                            //                          Trace.TraceInformation("Zone Tension - ID {0} SmoothForce {1} CouplerForceN {2} Slack {3} Speed {4} Loop {5}", car.CarID, car.SmoothedCouplerForceUN, car.CouplerForceU, car.CouplerSlackM, car.SpeedMpS, Loop);
                        }
                    }

                    else if (car.SmoothedCouplerForceUN == 0) // In this instance the coupler slack must be greater then the Z1 limit, as no coupler force is generated, and train will not move.
                    {
                        int Loop = 0;

                        if (car.SpeedMpS == 0)
                        {
                            // In this instance the coupler slack must be greater then the Z1 limit, otherwise no coupler force is generated, and train will not move.
                            car.AdvancedCouplerDynamicTensionSlackLimitM = MaxZ1TensionM * AdvancedCouplerDuplicationFactor * 1.05f;
                            car.AdvancedCouplerDynamicCompressionSlackLimitM = MaxZ1CompressionM * AdvancedCouplerDuplicationFactor * 1.05f;

                            if (car.CouplerSlackM < 0 && LastCar.SpeedMpS >= 0 && LastCar.SpeedMpS < LastCarTensionMoveSpeedMpS && FirstCar.SpeedMpS > 0)
                            {
                                // When starting in forward we don't want to allow slack to go negative
                                car.CouplerSlackM = car.PreviousCouplerSlackM;
                                // Make sure that coupler slack never goes negative when train starting and moving forward and starting
                                car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, 0, car.CouplerSlackM);
                                Loop = 1;
                            }
                            else if (car.CouplerSlackM > 0 && LastCar.SpeedMpS <= 0 && LastCar.SpeedMpS > LastCarCompressionMoveSpeedMpS && FirstCar.SpeedMpS < 0)
                            {
                                // When starting in reverse we don't want to allow slack to go positive
                                car.CouplerSlackM = car.PreviousCouplerSlackM;
                                // Make sure that coupler slack never goes positive when train starting and moving reverse and starting
                                car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, car.CouplerSlackM, 0);
                                Loop = 2;
                            }
                        }

                        //                        if (car.SpeedMpS != 0)
                        //                            Trace.TraceInformation("Advanced - Zero coupler force - CarID {0} Slack {1} Loop {2} Speed {3} Previous {4}", car.CarID, car.CouplerSlackM, Loop, car.SpeedMpS, car.PreviousCouplerSlackM);
                    }
                    else   // Compression
                    {
                        int Loop = 0;

                        if (!IsRigidCoupler)
                        {
                            if (IndividualCouplerSlackM > 0 && FirstCar.SpeedMpS < 0 && LastCar.SpeedMpS <= LastCarZeroSpeedMpS && LastCar.SpeedMpS > LastCarCompressionMoveSpeedMpS && LeadLocomotive.Direction == MidpointDirection.Reverse)
                            {
                                // Train is moving in reverse, don't allow positive coupler slack.
                                float TempDiff = Math.Abs(car.PreviousCouplerSlackM) * (1.0f - CouplerChangeDampingFactor);
                                if (Math.Abs(car.PreviousCouplerSlackM) - TempDiff > Math.Abs(IndividualCouplerSlackM))
                                {
                                    car.CouplerSlackM = -1.0f * Math.Abs(car.PreviousCouplerSlackM) - TempDiff;
                                    car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                    Loop = 1;
                                }
                            }
                            else if (FirstCar.SpeedMpS > 0 && LastCar.SpeedMpS >= LastCarZeroSpeedMpS && LastCar.SpeedMpS < LastCarTensionMoveSpeedMpS && LeadLocomotive.Direction == MidpointDirection.Forward)
                            {
                                if (IndividualCouplerSlackM < 0)
                                {
                                    // Train is starting to move forward, don't allow negative coupler slack - should either be positive or zero
                                    car.CouplerSlackM = car.PreviousCouplerSlackM;
                                    car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, 0, car.CouplerSlackM);
                                    car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                    Loop = 2;
                                }
                                else if (IndividualCouplerSlackM > MaxZ1TensionM)
                                {
                                    car.CouplerSlackM = MaxZ1TensionM * AdvancedCouplerDuplicationFactor;
                                    car.AdvancedCouplerDynamicTensionSlackLimitM = car.CouplerSlackM;
                                    Loop = 3;
                                }
                            }
                            else if (MaxZ3CompressionM < IndividualCouplerSlackM && IndividualCouplerSlackM <= MaxZ1CompressionM)
                            {

                                // A linear curve is assumed for coupler stiffness - this curve is then used to calculate the amount of slack that the coupler should have. 
                                //These values are set to "lock" the coupler at this maximum slack length

                                if (Math.Abs(car.SmoothedCouplerForceUN) < car.GetCouplerCompressionStiffness1N())
                                {
                                    float SlackDiff = Math.Abs(MaxZ2CompressionM - MaxZ1CompressionM);
                                    float GradStiffness = car.GetCouplerCompressionStiffness1N() / (SlackDiff); // Calculate gradient of line
                                    float ComputedZone2SlackM = (Math.Abs(car.SmoothedCouplerForceUN) / GradStiffness) + Math.Abs(MaxZ1CompressionM); // Current slack distance in this zone of coupler

                                    if (LastCar.SpeedMpS > LastCarCompressionMoveSpeedMpS && LastCar.SpeedMpS <= LastCarZeroSpeedMpS)
                                    {

                                        if (ComputedZone2SlackM < (Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor))
                                        {
                                            // Train is starting, don't allow coupler slack to decrease until complete train is moving
                                            car.CouplerSlackM = car.PreviousCouplerSlackM;
                                            car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                            Loop = 4;
                                        }
                                        else if (ComputedZone2SlackM > Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor)
                                        {
                                            // Allow coupler slack to slowly increase

                                            // Increase coupler slack slowly
                                            car.CouplerSlackM = car.PreviousCouplerSlackM * (1.0f / CouplerChangeDampingFactor);
                                            car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, MaxZ3CompressionM * AdvancedCouplerDuplicationFactor, 0);
                                            car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                            Loop = 5;
                                        }
                                    }
                                    else if (ComputedZone2SlackM < (Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor))
                                    {
                                        // Once train is moving then allow gradual reduction in coupler slack
                                        car.CouplerSlackM = car.PreviousCouplerSlackM * CouplerChangeDampingFactor;
                                        car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                        Loop = 7;
                                    }
                                    else
                                    //                              else if (ComputedZone2SlackM > Math.Abs(IndividualCouplerSlackM))
                                    {
                                        // If train moving then allow coupler slack to increase slowly depending upon the caclulated slack
                                        car.CouplerSlackM = -1.0f * ComputedZone2SlackM * AdvancedCouplerDuplicationFactor * CouplerChangeDampingFactor;
                                        car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, MaxZ3CompressionM * AdvancedCouplerDuplicationFactor, 0);
                                        car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                        Loop = 8;
                                    }

                                    //   Trace.TraceInformation("Zone 2 Compression - ID {0} Diff {1} Stiff {2} SmoothForce {3} CouplerForceN {4} Slack {5} ComputedSlack {6} MaxZ2 {7} IndSlack {8} LastSpeed {9} FinalDiff {10} ChangeFactror {11}", car.CarID, SlackDiff, GradStiffness, car.SmoothedCouplerForceUN, car.CouplerForceU, car.CouplerSlackM, ComputedZone2SlackM, MaxZ2TensionM, IndividualCouplerSlackM, LastCar.SpeedMpS, (car.CouplerSlackM - (IndividualCouplerSlackM * AdvancedCouplerDuplicationFactor)), CouplerChangeDampingFactor);
                                }
                                else
                                {
                                    // A linear curve is assumed for coupler stiffness - this curve is then used to calculate the amount of slack that the coupler should have. 
                                    //These values are set to "lock" the coupler at this maximum slack length
                                    float SlackDiff = Math.Abs(MaxZ3CompressionM - MaxZ2CompressionM);
                                    float GradStiffness = (car.GetCouplerCompressionStiffness2N() - car.GetCouplerCompressionStiffness1N()) / (SlackDiff);
                                    float ComputedZone3SlackM = (Math.Abs(car.SmoothedCouplerForceUN) / GradStiffness) + Math.Abs(MaxZ2CompressionM);

                                    if (LastCar.SpeedMpS > LastCarCompressionMoveSpeedMpS && LastCar.SpeedMpS <= LastCarZeroSpeedMpS)
                                    {
                                        // Train is starting, don't allow coupler slack to decrease until complete train is moving
                                        if (ComputedZone3SlackM < (Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor))
                                        {
                                            car.CouplerSlackM = car.PreviousCouplerSlackM;
                                            car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                            Loop = 9;
                                        }
                                        else if (ComputedZone3SlackM > Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor)
                                        {
                                            // Increase slack
                                            car.CouplerSlackM = car.PreviousCouplerSlackM * (1.0f / CouplerChangeDampingFactor);
                                            car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, MaxZ3CompressionM * AdvancedCouplerDuplicationFactor, 0);
                                            car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                            Loop = 10;
                                        }
                                        //   Trace.TraceInformation("Zone 3 Compression - ID {0} Diff {1} Stiff {2} SmoothForce {3} CouplerForceN {4} Slack {5} ComputedSlack {6} MaxZ2 {7} IndSlack {8} LastSpeed {9} FinalDiff {10} ChangeFactror {11}", car.CarID, SlackDiff, GradStiffness, car.SmoothedCouplerForceUN, car.CouplerForceU, car.CouplerSlackM, ComputedZone3SlackM, MaxZ2TensionM, IndividualCouplerSlackM, LastCar.SpeedMpS, (car.CouplerSlackM - (IndividualCouplerSlackM * AdvancedCouplerDuplicationFactor)), CouplerChangeDampingFactor);
                                    }
                                    else if (ComputedZone3SlackM < (Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor))
                                    {
                                        // Train moving - Decrease slack if Computed Slack is less then the previous slack value
                                        car.CouplerSlackM = car.PreviousCouplerSlackM * CouplerChangeDampingFactor;
                                        car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                        Loop = 12;
                                    }
                                    else
                                    //                else if (ComputedZone3SlackM > IndividualCouplerSlackM)
                                    {
                                        // Train moving - Allow coupler slack to be slowly increased if it is not the same as the computed value
                                        car.CouplerSlackM = -1.0f * ComputedZone3SlackM * AdvancedCouplerDuplicationFactor * CouplerChangeDampingFactor;
                                        car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, MaxZ3CompressionM * AdvancedCouplerDuplicationFactor, 0);
                                        car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                        Loop = 13;
                                    }
                                }
                            }
                            else if (IndividualCouplerSlackM < MaxZ3CompressionM)  // Make sure that a new computed slack value does not take slack into the next zone.
                            {
                                // If computed slack is higher then Zone 3 limit, then set to max Z3. 

                                if (LastCar.SpeedMpS > LastCarCompressionMoveSpeedMpS && LastCar.SpeedMpS <= LastCarZeroSpeedMpS)
                                {
                                    // Train starting - limit slack to maximum
                                    car.CouplerSlackM = MaxZ3CompressionM * AdvancedCouplerDuplicationFactor;
                                    car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                    Loop = 17;
                                }
                                else
                                {

                                    // A linear curve is assumed for coupler stiffness - this curve is then used to calculate the amount of slack that the coupler should have. 
                                    //These values are set to "lock" the coupler at this maximum slack length
                                    float SlackDiff = Math.Abs(MaxZ3CompressionM - MaxZ2CompressionM);
                                    float GradStiffness = (car.GetCouplerCompressionStiffness2N() - car.GetCouplerCompressionStiffness1N()) / (SlackDiff);
                                    float ComputedZone4SlackM = (Math.Abs(car.SmoothedCouplerForceUN) / GradStiffness) + Math.Abs(MaxZ2CompressionM);

                                    if (ComputedZone4SlackM < Math.Abs(MaxZ3CompressionM) && ComputedZone4SlackM > Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor)
                                    {
                                        car.CouplerSlackM = -1.0f * ComputedZone4SlackM * AdvancedCouplerDuplicationFactor * (1.0f / CouplerChangeDampingFactor);
                                        car.CouplerSlackM = MathHelper.Clamp(car.CouplerSlackM, MaxZ3CompressionM * AdvancedCouplerDuplicationFactor, 0);
                                        car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                        Loop = 14;
                                    }
                                    else if (ComputedZone4SlackM > Math.Abs(MaxZ3CompressionM))
                                    {
                                        car.CouplerSlackM = MaxZ3CompressionM * AdvancedCouplerDuplicationFactor;
                                        car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                        Loop = 15;
                                    }
                                    else if (ComputedZone4SlackM < Math.Abs(MaxZ3CompressionM) && ComputedZone4SlackM < Math.Abs(car.PreviousCouplerSlackM) / AdvancedCouplerDuplicationFactor)
                                    {
                                        // Decrease coupler slack
                                        car.CouplerSlackM = car.PreviousCouplerSlackM * CouplerChangeDampingFactor;
                                        car.AdvancedCouplerDynamicCompressionSlackLimitM = car.CouplerSlackM;
                                        Loop = 16;
                                    }
                                }
                            }

                            //                        if (car.SpeedMpS < 0)
                            //                            Trace.TraceInformation("Zone Compression - ID {0} SmoothForce {1} CouplerForceN {2} Slack {3} Speed {4} Loop {5} ", car.CarID, car.SmoothedCouplerForceUN, car.CouplerForceU, car.CouplerSlackM, car.SpeedMpS, Loop);
                        }
                    }

                }
                else  // Update couplerslack2m which acts as an upper limit in slack calculations for the simple coupler
                {
                    float maxs = car.GetMaximumSimpleCouplerSlack2M();

                    if (car.CouplerForceU > 0) // Compression
                    {
                        float f = -(car.CouplerSlackM + car.GetMaximumSimpleCouplerSlack1M()) * car.GetSimpleCouplerStiffnessNpM();
                        if (car.CouplerSlackM > -maxs && f > car.CouplerForceU)
                            car.CouplerSlack2M = -car.CouplerSlackM;
                        else
                            car.CouplerSlack2M = maxs;
                    }
                    else if (car.CouplerForceU == 0) // Faces not touching
                        car.CouplerSlack2M = maxs;
                    else   // Tension
                    {
                        float f = (car.CouplerSlackM - car.GetMaximumSimpleCouplerSlack1M()) * car.GetSimpleCouplerStiffnessNpM();
                        if (car.CouplerSlackM < maxs && f > car.CouplerForceU)
                            car.CouplerSlack2M = car.CouplerSlackM;
                        else
                            car.CouplerSlack2M = maxs;
                    }
                }

                car.PreviousCouplerSlackM = car.CouplerSlackM;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update Car speeds
        /// <\summary>

        public void UpdateCarSpeeds(double elapsedTime)
        {
            // The train speed is calculated by averaging all the car speeds. The individual car speeds are calculated from the TotalForce acting on each car. 
            // Typically the TotalForce consists of the MotiveForce or Gravitational forces (though other forces like friction have a small impact as well).
            // At stop under normal circumstances the BrakeForce exceeds the TotalForces, and therefore the wagon is "held in a stationary position". 
            // In the case of "air_piped" wagons which have no BrakeForces acting on them, the car is not held stationary, and each car shows a small speed vibration in either direction.
            // To overcome this any "air_piped and vacuum_piped" cars are forced to zero speed if the preceeding car is stationary.
            int n = 0;
            float PrevCarSpeedMps = 0.0f;
            float NextCarSpeedMps = 0.0f;
            bool locoBehind = true;
            for (int iCar = 0; iCar < Cars.Count; iCar++)
            {
                var car = Cars[iCar];
                if (iCar < Cars.Count - 1) NextCarSpeedMps = Cars[iCar + 1].SpeedMpS;
                if (TrainMaxSpeedMpS <= 0f)
                {
                    if (car is MSTSLocomotive)
                        TrainMaxSpeedMpS = (car as MSTSLocomotive).MaxSpeedMpS;
                    if (car is MSTSElectricLocomotive)
                        TrainMaxSpeedMpS = (car as MSTSElectricLocomotive).MaxSpeedMpS;
                    if (car is MSTSDieselLocomotive)
                        TrainMaxSpeedMpS = (car as MSTSDieselLocomotive).MaxSpeedMpS;
                    if (car is MSTSSteamLocomotive)
                        TrainMaxSpeedMpS = (car as MSTSSteamLocomotive).MaxSpeedMpS;
                }
                if (car is MSTSLocomotive) locoBehind = false;
                if (car.SpeedMpS > 0)
                {
                    car.SpeedMpS += car.TotalForceN / car.MassKG * (float)elapsedTime;
                    if (car.SpeedMpS < 0)
                        car.SpeedMpS = 0;
                    // If car is manual braked, air_piped car or vacuum_piped, and preceeding car is at stop, then set speed to zero.  
                    // These type of cars do not have any brake force to hold them still
                    if ((car.CarBrakeSystemType == "air_piped" || car.CarBrakeSystemType == "vacuum_piped" || car.CarBrakeSystemType == "manual_braking") && (locoBehind ? n != Cars.Count - 1 && NextCarSpeedMps == 0 : n != 0 && PrevCarSpeedMps == 0))
                    {
                        car.SpeedMpS = 0;
                    }
                    PrevCarSpeedMps = car.SpeedMpS;
                }
                else if (car.SpeedMpS < 0)
                {
                    car.SpeedMpS += car.TotalForceN / car.MassKG * (float)elapsedTime;
                    if (car.SpeedMpS > 0)
                        car.SpeedMpS = 0;
                    // If car is manual braked, air_piped car or vacuum_piped, and preceeding car is at stop, then set speed to zero.  
                    // These type of cars do not have any brake force to hold them still
                    if ((car.CarBrakeSystemType == "air_piped" || car.CarBrakeSystemType == "vacuum_piped" || car.CarBrakeSystemType == "manual_braking") && (locoBehind ? n != Cars.Count - 1 && NextCarSpeedMps == 0 : n != 0 && PrevCarSpeedMps == 0))
                    {
                        car.SpeedMpS = 0;
                    }
                    PrevCarSpeedMps = car.SpeedMpS;
                }
                else // if speed equals zero
                    PrevCarSpeedMps = car.SpeedMpS;
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
                    if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
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
                    if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
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

        //================================================================================================//
        /// <summary>
        /// Update coupler slack - ensures that coupler slack doesn't exceed the maximum permissible value, and provides indication to HUD
        /// <\summary>

        public void UpdateCouplerSlack(double elapsedTime)
        {
            TotalCouplerSlackM = 0;
            NPull = NPush = 0;
            for (int i = 0; i < Cars.Count - 1; i++)
            {
                // update coupler slack distance
                TrainCar car = Cars[i];

                // Initialise individual car coupler slack values
                car.RearCouplerSlackM = 0;
                car.FrontCouplerSlackM = 0;

                // Calculate coupler slack - this should be the full amount for both couplers
                car.CouplerSlackM += (float)((car.SpeedMpS - Cars[i + 1].SpeedMpS) * elapsedTime);

                // Make sure that coupler slack does not exceed the maximum (dynamic) coupler slack

                if (car.IsPlayerTrain && Simulator.UseAdvancedAdhesion && car.IsAdvancedCoupler) // "Advanced coupler" - operates in three extension zones
                {
                    float AdvancedCouplerCompressionLimitM = car.AdvancedCouplerDynamicCompressionSlackLimitM;
                    float AdvancedCouplerTensionLimitM = car.AdvancedCouplerDynamicTensionSlackLimitM;

                    if (car.CouplerSlackM < AdvancedCouplerCompressionLimitM) // Compression
                        car.CouplerSlackM = AdvancedCouplerCompressionLimitM;

                    else if (car.CouplerSlackM > AdvancedCouplerTensionLimitM) // Tension
                        car.CouplerSlackM = AdvancedCouplerTensionLimitM;
                }
                else // Simple coupler
                {
                    float max = car.GetMaximumSimpleCouplerSlack2M();
                    if (car.CouplerSlackM < -max)  // Compression
                        car.CouplerSlackM = -max;
                    else if (car.CouplerSlackM > max) // Tension
                        car.CouplerSlackM = max;
                }

                // Proportion coupler slack across front and rear couplers of this car, and the following car
                car.RearCouplerSlackM = car.CouplerSlackM / AdvancedCouplerDuplicationFactor;
                car.FrontCouplerSlackM = Cars[i + 1].CouplerSlackM / AdvancedCouplerDuplicationFactor;

                // Check to see if coupler is opened or closed - only closed or opened couplers have been specified
                // It is assumed that the front coupler on first car will always be opened, and so will coupler on last car. All others on the train will be coupled
                if (i == 0)
                {
                    if (car.FrontCouplerOpenFitted)
                    {

                        car.FrontCouplerOpen = true;
                    }
                    else
                    {
                        car.FrontCouplerOpen = false;
                    }
                }
                else
                {
                    car.FrontCouplerOpen = false;
                }


                if (i == Cars.Count - 2)
                {

                    if (Cars[i + 1].RearCouplerOpenFitted)
                    {
                        Cars[i + 1].RearCouplerOpen = true;
                    }
                    else
                    {
                        Cars[i + 1].RearCouplerOpen = false;
                    }

                }
                else
                {
                    car.RearCouplerOpen = false;
                }



                TotalCouplerSlackM += car.CouplerSlackM; // Total coupler slack displayed in HUD only

#if DEBUG_COUPLER_FORCES
                if (car.IsAdvancedCoupler)
                {
                    Trace.TraceInformation("Advanced Coupler - Tension - CarID {0} CouplerSlack {1} Zero {2} MaxSlackZone1 {3} MaxSlackZone2 {4} MaxSlackZone3 {5} Stiffness1 {6} Stiffness2 {7} AdvancedCpl {8} CplSlackA {9} CplSlackB {10}  Rigid {11}",
                    car.CarID, car.CouplerSlackM, car.GetCouplerZeroLengthM(), car.GetMaximumCouplerTensionSlack1M(), car.GetMaximumCouplerTensionSlack2M(), car.GetMaximumCouplerTensionSlack3M(),
                    car.GetCouplerTensionStiffness1N(), car.GetCouplerTensionStiffness2N(), car.IsAdvancedCoupler, car.GetCouplerTensionSlackAM(), car.GetCouplerTensionSlackBM(), car.GetCouplerRigidIndication());

                    Trace.TraceInformation("Advanced Coupler - Compression - CarID {0} CouplerSlack {1} Zero {2} MaxSlackZone1 {3} MaxSlackZone2 {4} MaxSlackZone3 {5} Stiffness1 {6} Stiffness2 {7} AdvancedCpl {8} CplSlackA {9} CplSlackB {10}  Rigid {11}",
                    car.CarID, car.CouplerSlackM, car.GetCouplerZeroLengthM(), car.GetMaximumCouplerCompressionSlack1M(), car.GetMaximumCouplerCompressionSlack2M(), car.GetMaximumCouplerCompressionSlack3M(),
                    car.GetCouplerCompressionStiffness1N(), car.GetCouplerCompressionStiffness2N(), car.IsAdvancedCoupler, car.GetCouplerCompressionSlackAM(), car.GetCouplerCompressionSlackBM(), car.GetCouplerRigidIndication());
                }
                else
                {
                    Trace.TraceInformation("Simple Coupler - CarID {0} CouplerSlack {1} Zero {2} MaxSlackZone1 {3} MaxSlackZone2 {4} Stiffness {5} Rigid {6}",
                    car.CarID, car.CouplerSlackM, car.GetCouplerZeroLengthM(), car.GetMaximumSimpleCouplerSlack1M(), car.GetMaximumSimpleCouplerSlack2M(),
                    car.GetSimpleCouplerStiffnessNpM(), car.GetCouplerRigidIndication());
                }
#endif

                if (!car.GetCouplerRigidIndication()) // Flexible coupling - pulling and pushing value will be equal to slack when couplers faces touch
                {

                    if (car.CouplerSlackM >= 0.001) // Coupler pulling
                    {
                        NPull++;
                        car.HUDCouplerForceIndication = 1;
                    }
                    else if (car.CouplerSlackM <= -0.001) // Coupler pushing
                    {
                        NPush++;
                        car.HUDCouplerForceIndication = 2;
                    }
                    else
                    {
                        car.HUDCouplerForceIndication = 0; // Coupler neutral
                    }
                }
                else if (car.GetCouplerRigidIndication()) // Rigid coupling - starts pulling/pushing at a lower value then flexible coupling
                {
                    if (car.CouplerSlackM >= 0.000125) // Coupler pulling
                    {
                        NPull++;
                        car.HUDCouplerForceIndication = 1;
                    }
                    else if (car.CouplerSlackM <= -0.000125) // Coupler pushing
                    {
                        NPush++;
                        car.HUDCouplerForceIndication = 2;
                    }
                    else
                    {
                        car.HUDCouplerForceIndication = 0; // Coupler neutral
                    }

                }




            }
            foreach (TrainCar car in Cars)
                car.DistanceM += (float)Math.Abs(car.SpeedMpS * elapsedTime);
        }

        //================================================================================================//
        /// <summary>
        /// Calculate initial position
        /// </summary>

        public virtual TrackCircuitPartialPathRoute CalculateInitialTrainPosition(ref bool trackClear)
        {

            // calculate train length

            float trainLength = 0f;

            for (var i = Cars.Count - 1; i >= 0; --i)
            {
                var car = Cars[i];
                if (i < Cars.Count - 1)
                {
                    trainLength += car.CouplerSlackM + car.GetCouplerZeroLengthM();
                }
                trainLength += car.CarLengthM;
            }

            // get starting position and route

            TrackNode tn = RearTDBTraveller.TN;
            float offset = RearTDBTraveller.TrackNodeOffset;
            int direction = (int)RearTDBTraveller.Direction;

            PresentPosition[1].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[1].TCSectionIndex];
            offset = PresentPosition[1].TCOffset;

            //<CSComment> must do preliminary calculation of PresentPosition[0] parameters in order to use subsequent code
            // limited however to case of train fully in one section to avoid placement ambiguities </CSComment>
            float offsetFromEnd = thisSection.Length - (Length + offset);
            if (PresentPosition[0].TCSectionIndex == -1 && offsetFromEnd >= 0) // train is fully in one section
            {
                PresentPosition[0].TCDirection = PresentPosition[1].TCDirection;
                PresentPosition[0].TCSectionIndex = PresentPosition[1].TCSectionIndex;
                PresentPosition[0].TCOffset = PresentPosition[1].TCOffset + trainLength;
            }

            // create route if train has none

            if (ValidRoute[0] == null)
            {
                ValidRoute[0] = SignalEnvironment.BuildTempRoute(this, thisSection.Index, PresentPosition[1].TCOffset,
                            (TrackDirection)PresentPosition[1].TCDirection, trainLength, true, true, false);
            }

            // find sections

            bool sectionAvailable = true;
            float remLength = trainLength;
            int routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
            if (routeIndex < 0)
                routeIndex = 0;

            bool sectionsClear = true;

            TrackCircuitPartialPathRoute tempRoute = new TrackCircuitPartialPathRoute();

            TrackCircuitRouteElement thisElement = ValidRoute[0][routeIndex];
            thisSection = thisElement.TrackCircuitSection;
            if (!thisSection.CanPlaceTrain(this, offset, remLength))
            {
                sectionsClear = false;
            }

            while (remLength > 0 && sectionAvailable)
            {
                tempRoute.Add(thisElement);
                remLength -= (thisSection.Length - offset);
                offset = 0.0f;

                if (remLength > 0)
                {
                    if (routeIndex < ValidRoute[0].Count - 1)
                    {
                        routeIndex++;
                        thisElement = ValidRoute[0][routeIndex];
                        thisSection = thisElement.TrackCircuitSection;
                        if (!thisSection.CanPlaceTrain(this, offset, remLength))
                        {
                            sectionsClear = false;
                        }
                        offset = 0.0f;
                    }
                    else
                    {
                        Trace.TraceWarning("Not sufficient track to place train {0} , service name {1} ", Number, Name);
                        sectionAvailable = false;
                    }
                }

            }

            trackClear = true;

            if (MPManager.IsMultiPlayer()) return (tempRoute);
            if (!sectionAvailable || !sectionsClear)
            {
                trackClear = false;
                tempRoute.Clear();
            }

            return (tempRoute);
        }

        //================================================================================================//
        //
        // Set initial train route
        //

        public void SetInitialTrainRoute(TrackCircuitPartialPathRoute tempRoute)
        {

            // reserve sections, use direction 0 only

            foreach (TrackCircuitRouteElement thisElement in tempRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                thisSection.Reserve(routedForward, tempRoute);
            }
        }

        //================================================================================================//
        //
        // Reset initial train route
        //

        public void ResetInitialTrainRoute(TrackCircuitPartialPathRoute tempRoute)
        {

            // unreserve sections

            foreach (TrackCircuitRouteElement thisElement in tempRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                thisSection.RemoveTrain(this, false);
            }
        }

        //================================================================================================//
        //
        // Initial train placement
        //

        public virtual bool InitialTrainPlacement()
        {
            // for initial placement, use direction 0 only
            // set initial positions

            TrackNode tn = FrontTDBTraveller.TN;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            int direction = (int)FrontTDBTraveller.Direction;

            PresentPosition[0].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PresentPosition[0].CopyTo(ref PreviousPosition[0]);

            DistanceTravelledM = 0.0f;

            tn = RearTDBTraveller.TN;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (int)RearTDBTraveller.Direction;

            PresentPosition[1].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);

            // check if train has route, if not create dummy

            if (ValidRoute[0] == null)
            {
                ValidRoute[0] = SignalEnvironment.BuildTempRoute(this, PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset,
                        (TrackDirection)PresentPosition[1].TCDirection, Length, true, true, false);
            }

            // get index of first section in route

            int rearIndex = ValidRoute[0].GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
            if (rearIndex < 0)
            {
                rearIndex = 0;
            }

            PresentPosition[1].RouteListIndex = rearIndex;

            // get index of front of train

            int frontIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
            if (frontIndex < 0)
            {
                Trace.TraceWarning("Start position of front of train {0}, service name {1} not on route ", Number, Name);
                frontIndex = 0;
            }

            PresentPosition[0].RouteListIndex = frontIndex;

            // check if train can be placed
            // get index of section in train route //

            int routeIndex = rearIndex;
            List<TrackCircuitSection> placementSections = new List<TrackCircuitSection>();

            // check if route is available

            offset = PresentPosition[1].TCOffset;
            float remLength = Length;
            bool sectionAvailable = true;

            for (int iRouteIndex = rearIndex; iRouteIndex <= frontIndex && sectionAvailable; iRouteIndex++)
            {
                TrackCircuitSection thisSection = ValidRoute[0][iRouteIndex].TrackCircuitSection;
                if (thisSection.CanPlaceTrain(this, offset, remLength))
                {
                    placementSections.Add(thisSection);
                    remLength -= (thisSection.Length - offset);

                    if (remLength > 0)
                    {
                        if (routeIndex < ValidRoute[0].Count - 1)
                        {
                            routeIndex++;
                            offset = 0.0f;
                        }
                        else
                        {
                            Trace.TraceWarning("Not sufficient track to place train");
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
                return (false);
            }

            // set any deadlocks for sections ahead of start with end beyond start

            for (int iIndex = 0; iIndex < rearIndex; iIndex++)
            {
                int rearSectionIndex = ValidRoute[0][iIndex].TrackCircuitSection.Index;
                if (DeadlockInfo.ContainsKey(rearSectionIndex))
                {
                    foreach (Dictionary<int, int> thisDeadlock in DeadlockInfo[rearSectionIndex])
                    {
                        foreach (KeyValuePair<int, int> thisDetail in thisDeadlock)
                        {
                            int endSectionIndex = thisDetail.Value;
                            if (ValidRoute[0].GetRouteIndex(endSectionIndex, rearIndex) >= 0)
                            {
                                TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[endSectionIndex];
                                endSection.SetDeadlockTrap(Number, thisDetail.Key);
                            }
                        }
                    }
                }
            }

            // set track occupied (if not done yet)

            foreach (TrackCircuitSection thisSection in placementSections)
            {
                if (!thisSection.IsSet(routedForward, false))
                {
                    thisSection.Reserve(routedForward, ValidRoute[0]);
                    thisSection.SetOccupied(routedForward);
                }
            }

            return (true);
        }
        //================================================================================================//
        /// <summary>
        /// Set Formed Occupied
        /// Set track occupied for train formed out of other train
        /// </summary>

        public void SetFormedOccupied()
        {

            int rearIndex = PresentPosition[1].RouteListIndex;
            int frontIndex = PresentPosition[0].RouteListIndex;

            int routeIndex = rearIndex;

            List<TrackCircuitSection> placementSections = new List<TrackCircuitSection>();

            // route is always available as previous train was there

            float offset = PresentPosition[1].TCOffset;
            float remLength = Length;

            for (int iRouteIndex = rearIndex; iRouteIndex <= frontIndex; iRouteIndex++)
            {
                TrackCircuitSection thisSection = ValidRoute[0][iRouteIndex].TrackCircuitSection;
                placementSections.Add(thisSection);
                remLength -= (thisSection.Length - offset);

                if (remLength > 0)
                {
                    if (routeIndex < ValidRoute[0].Count - 1)
                    {
                        routeIndex++;
                        offset = 0.0f;
                    }
                    else
                    {
                        Trace.TraceWarning("Not sufficient track to place train");
                    }
                }
            }

            // set track occupied (if not done yet)

            foreach (TrackCircuitSection thisSection in placementSections)
            {
                if (!thisSection.IsSet(routedForward, false))
                {
                    thisSection.Reserve(routedForward, ValidRoute[0]);
                    thisSection.SetOccupied(routedForward);
                }
            }
        }

        /// <summary>
        /// Check if train is stopped in station
        /// </summary>
        /// <param name="thisPlatform"></param>
        /// <param name="stationDirection"></param>
        /// <param name="stationTCSectionIndex"></param>
        /// <returns></returns>
        public virtual bool CheckStationPosition(PlatformDetails thisPlatform, int stationDirection, int stationTCSectionIndex)
        {
            bool atStation = false;
            float platformBeginOffset = thisPlatform.TrackCircuitOffset[Location.NearEnd, (TrackDirection)stationDirection];
            float platformEndOffset = thisPlatform.TrackCircuitOffset[Location.FarEnd, (TrackDirection)stationDirection];
            int endSectionIndex = stationDirection == 0 ?
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1] :
                    thisPlatform.TCSectionIndex[0];
            int endSectionRouteIndex = ValidRoute[0].GetRouteIndex(endSectionIndex, 0);

            int beginSectionIndex = stationDirection == 1 ?
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1] :
                    thisPlatform.TCSectionIndex[0];
            int beginSectionRouteIndex = ValidRoute[0].GetRouteIndex(beginSectionIndex, 0);

            // if rear is in platform, station is valid
            if (((((beginSectionRouteIndex != -1 && PresentPosition[1].RouteListIndex == beginSectionRouteIndex) || (PresentPosition[1].RouteListIndex == -1 && PresentPosition[1].TCSectionIndex == beginSectionIndex))
                && PresentPosition[1].TCOffset >= platformBeginOffset) || PresentPosition[1].RouteListIndex > beginSectionRouteIndex) &&
                ((PresentPosition[1].TCSectionIndex == endSectionIndex && PresentPosition[1].TCOffset <= platformEndOffset) || endSectionRouteIndex == -1 && beginSectionRouteIndex != -1 ||
                PresentPosition[1].RouteListIndex < endSectionRouteIndex))
            {
                atStation = true;
            }
            // if front is in platform and most of the train is as well, station is valid
            else if (((((endSectionRouteIndex != -1 && PresentPosition[0].RouteListIndex == endSectionRouteIndex) || (PresentPosition[0].RouteListIndex == -1 && PresentPosition[0].TCSectionIndex == endSectionIndex))
                && PresentPosition[0].TCOffset <= platformEndOffset) && ((thisPlatform.Length - (platformEndOffset - PresentPosition[0].TCOffset)) > Length / 2)) ||
                (PresentPosition[0].RouteListIndex != -1 && PresentPosition[0].RouteListIndex < endSectionRouteIndex &&
                (PresentPosition[0].RouteListIndex > beginSectionRouteIndex || (PresentPosition[0].RouteListIndex == beginSectionRouteIndex && PresentPosition[0].TCOffset >= platformBeginOffset))))
            {
                atStation = true;
            }
            // if front is beyond platform and and most of the train is within it, station is valid (isn't it already covered by cases 1 or 4?)
            else if (endSectionRouteIndex != -1 && PresentPosition[0].RouteListIndex == endSectionRouteIndex && PresentPosition[0].TCOffset > platformEndOffset &&
                     (PresentPosition[0].TCOffset - platformEndOffset) < (Length / 3))
            {
                atStation = true;
            }
            // if front is beyond platform and rear is not on route or before platform : train spans platform
            else if (((endSectionRouteIndex != -1 && PresentPosition[0].RouteListIndex > endSectionRouteIndex) || (endSectionRouteIndex != -1 && PresentPosition[0].RouteListIndex == endSectionRouteIndex && PresentPosition[0].TCOffset >= platformEndOffset))
                  && (PresentPosition[1].RouteListIndex < beginSectionRouteIndex || (PresentPosition[1].RouteListIndex == beginSectionRouteIndex && PresentPosition[1].TCOffset <= platformBeginOffset)))
            {
                atStation = true;
            }

            return atStation;
        }


        //================================================================================================//
        /// <summary>
        /// Update train position
        /// </summary>

        public void UpdateTrainPosition()
        {
            // update positions

            PresentPosition[0].CopyTo(ref PreviousPosition[0]);

            TrackNode tn = FrontTDBTraveller.TN;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            int direction = (int)FrontTDBTraveller.Direction;
            int routeIndex;

            PresentPosition[0].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);
            routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
            PresentPosition[0].RouteListIndex = routeIndex;

            tn = RearTDBTraveller.TN;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (int)RearTDBTraveller.Direction;

            PresentPosition[1].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);
            routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
            PresentPosition[1].RouteListIndex = routeIndex;

            if (doJump) // jump do be performed in multiplayer mode when train re-enters game in different position
            {
                doJump = false;
                PresentPosition[0].CopyTo(ref PreviousPosition[0]);
                Trace.TraceInformation("Multiplayer server requested the player train to jump");
                // reset some items
                SignalObjectItems.Clear();
                NextSignalObject[0] = null;
                InitializeSignals(true);
                LastReservedSection[0] = PresentPosition[0].TCSectionIndex;
            }

            // get reserved length
            ReservedTrackLengthM = GetReservedLength();
        }

        //================================================================================================//
        /// <summary>
        /// Update Position linked information
        /// Switches train to Out_Of_Control if it runs out of path
        /// <\summary>

        public void UpdateTrainPositionInformation()
        {

            // check if train still on route - set train to OUT_OF_CONTROL

            PresentPosition[0].DistanceTravelledM = DistanceTravelledM;
            PresentPosition[1].DistanceTravelledM = DistanceTravelledM - Length;

            if (PresentPosition[0].RouteListIndex < 0)
            {
                SetTrainOutOfControl(OutOfControlReason.OutOfPath);
            }
            else if (StationStops.Count > 0)
            {
                StationStop thisStation = StationStops[0];
                thisStation.DistanceToTrainM = ComputeDistanceToNextStation(thisStation);
            }
        }

        //================================================================================================//
        /// <summary>
        /// compute boarding time for activity mode
        /// also check validity of depart time value
        /// <\summary>

        public virtual bool ComputeTrainBoardingTime(StationStop thisStop, ref int stopTime)
        {
            stopTime = thisStop.ComputeStationBoardingTime(this);
            return (thisStop.CheckScheduleValidity(this));
        }

        //================================================================================================//
        /// <summary>
        /// Compute distance to next station
        /// <\summary>
        /// 
        public float ComputeDistanceToNextStation(StationStop thisStation)
        {
            int thisSectionIndex = PresentPosition[0].TCSectionIndex;
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
            float leftInSectionM = thisSection.Length - PresentPosition[0].TCOffset;
            float distanceToTrainM = -1;
            int stationIndex;

            if (thisStation.SubrouteIndex > TCRoute.ActiveSubPath && !Simulator.TimetableMode)
            // if the station is in a further subpath, distance computation is longer
            {
                // first compute distance up to end or reverse point of activeSubpath. To be restudied for subpaths with no reversal
                if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                    distanceToTrainM = ComputeDistanceToReversalPoint();
                else
                {
                    int lastSectionRouteIndex = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].Count - 1;
                    float lastSectionLength = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][lastSectionRouteIndex].TrackCircuitSection.Length;
                    distanceToTrainM = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath].GetDistanceAlongRoute(PresentPosition[0].RouteListIndex,
                 leftInSectionM, lastSectionRouteIndex, lastSectionLength, true);
                }
                float lengthOfIntSubpath = 0;
                int firstSection = 0;
                float firstSectionOffsetToGo = 0;
                int lastSection = 0;
                float lastSectionOffsetToGo = 0;
                if (distanceToTrainM >= 0)
                {

                    // compute length of intermediate subpaths, if any, from reversal or section at beginning to reversal or section at end

                    for (int iSubpath = TCRoute.ActiveSubPath + 1; iSubpath < thisStation.SubrouteIndex; iSubpath++)
                    {
                        if (TCRoute.ReversalInfo[iSubpath - 1].Valid)
                        // skip sections before reversal at beginning of path
                        {
                            for (int iSection = 0; iSection < TCRoute.TCRouteSubpaths[iSubpath].Count; iSection++)
                            {
                                if (TCRoute.TCRouteSubpaths[iSubpath][iSection].TrackCircuitSection.Index == TCRoute.ReversalInfo[iSubpath - 1].ReversalSectionIndex)
                                {
                                    firstSection = iSection;
                                    firstSectionOffsetToGo = TCRoute.ReversalInfo[iSubpath - 1].ReverseReversalOffset;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            for (int iSection = 0; iSection < TCRoute.TCRouteSubpaths[iSubpath].Count; iSection++)
                            {
                                if (TCRoute.TCRouteSubpaths[iSubpath][iSection].TrackCircuitSection.Index ==
                                    TCRoute.TCRouteSubpaths[iSubpath - 1][TCRoute.TCRouteSubpaths[iSubpath - 1].Count - 1].TrackCircuitSection.Index)
                                {
                                    firstSection = iSection + 1;
                                    firstSectionOffsetToGo = TCRoute.TCRouteSubpaths[iSubpath][firstSection].TrackCircuitSection.Length;
                                    break;
                                }
                            }
                        }

                        if (TCRoute.ReversalInfo[iSubpath].Valid)
                        // skip sections before reversal at beginning of path
                        {
                            for (int iSection = TCRoute.TCRouteSubpaths[iSubpath].Count - 1; iSection >= 0; iSection--)
                            {
                                if (TCRoute.TCRouteSubpaths[iSubpath][iSection].TrackCircuitSection.Index == TCRoute.ReversalInfo[iSubpath].ReversalSectionIndex)
                                {
                                    lastSection = iSection;
                                    lastSectionOffsetToGo = TCRoute.ReversalInfo[iSubpath].ReverseReversalOffset;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            lastSection = TCRoute.TCRouteSubpaths[iSubpath].Count - 1;
                            lastSectionOffsetToGo = TCRoute.TCRouteSubpaths[iSubpath][lastSection].TrackCircuitSection.Length;
                        }

                        lengthOfIntSubpath = TCRoute.TCRouteSubpaths[iSubpath].GetDistanceAlongRoute(firstSection,
                            firstSectionOffsetToGo, lastSection, lastSectionOffsetToGo, true);
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
                    if (TCRoute.ReversalInfo[thisStation.SubrouteIndex - 1].Valid)
                    // skip sections before reversal at beginning of path
                    {
                        for (int iSection = 0; iSection < TCRoute.TCRouteSubpaths[thisStation.SubrouteIndex].Count; iSection++)
                        {
                            if (TCRoute.TCRouteSubpaths[thisStation.SubrouteIndex][iSection].TrackCircuitSection.Index == TCRoute.ReversalInfo[thisStation.SubrouteIndex - 1].ReversalSectionIndex)
                            {
                                firstSection = iSection;
                                firstSectionOffsetToGo = TCRoute.ReversalInfo[thisStation.SubrouteIndex - 1].ReverseReversalOffset;
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int iSection = 0; iSection < TCRoute.TCRouteSubpaths[thisStation.SubrouteIndex].Count; iSection++)
                        {
                            if (TCRoute.TCRouteSubpaths[thisStation.SubrouteIndex][iSection].TrackCircuitSection.Index ==
                                TCRoute.TCRouteSubpaths[thisStation.SubrouteIndex - 1][TCRoute.TCRouteSubpaths[thisStation.SubrouteIndex - 1].Count - 1].TrackCircuitSection.Index)
                            {
                                firstSection = iSection + 1;
                                firstSectionOffsetToGo = TCRoute.TCRouteSubpaths[thisStation.SubrouteIndex][firstSection].TrackCircuitSection.Length;
                                break;
                            }
                        }
                    }

                    stationIndex = thisStation.RouteIndex;
                    float distanceFromStartOfsubPath = TCRoute.TCRouteSubpaths[thisStation.SubrouteIndex].GetDistanceAlongRoute(firstSection,
                        firstSectionOffsetToGo, stationIndex, thisStation.StopOffset, true);
                    if (distanceFromStartOfsubPath < 0) distanceToTrainM = -1;
                    else distanceToTrainM += distanceFromStartOfsubPath;
                }
            }

            else
            {
                // No enhanced compatibility, simple computation
                // if present position off route, try rear position
                // if both off route, skip station stop
                stationIndex = ValidRoute[0].GetRouteIndex(thisStation.TCSectionIndex, PresentPosition[0].RouteListIndex);
                distanceToTrainM = ValidRoute[0].GetDistanceAlongRoute(PresentPosition[0].RouteListIndex, leftInSectionM, stationIndex, thisStation.StopOffset, true);
            }
            return distanceToTrainM;
        }


        //================================================================================================//
        /// <summary>
        /// Compute distance to reversal point
        /// <\summary>

        public float ComputeDistanceToReversalPoint()
        {
            float lengthToGoM = -PresentPosition[0].TCOffset;
            TrackCircuitSection thisSection;
            if (PresentPosition[0].RouteListIndex == -1)
            {
                Trace.TraceWarning("Train {0} service {1} off path; distance to reversal point set to -1", Number, Name);
                return -1;
            }
            // in case the AI train is out of its original path the reversal info is simulated to point to the end of the last route section
            int reversalRouteIndex = ValidRoute[0].Count - 1;
            TrackCircuitSection reversalSection = ValidRoute[0][reversalRouteIndex].TrackCircuitSection;
            float reverseReversalOffset = reversalSection.Length;
            reversalRouteIndex = ValidRoute[0].GetRouteIndex(TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex, PresentPosition[0].RouteListIndex);
            if (reversalRouteIndex == -1)
            {
                Trace.TraceWarning("Train {0} service {1}, reversal or end point off path; distance to reversal point set to -1", Number, Name);
                return -1;
            }
            reversalSection = TrackCircuitSection.TrackCircuitList[TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReversalSectionIndex];
            reverseReversalOffset = TCRoute.ReversalInfo[TCRoute.ActiveSubPath].ReverseReversalOffset;
            if (PresentPosition[0].RouteListIndex <= reversalRouteIndex)
            {
                for (int iElement = PresentPosition[0].RouteListIndex; iElement < ValidRoute[0].Count; iElement++)
                {
                    TrackCircuitRouteElement thisElement = ValidRoute[0][iElement];
                    thisSection = thisElement.TrackCircuitSection;
                    if (thisSection.Index == reversalSection.Index)
                    {
                        break;
                    }
                    else lengthToGoM += thisSection.Length;
                }
                return lengthToGoM += reverseReversalOffset;
            }
            else
            {
                for (int iElement = PresentPosition[0].RouteListIndex - 1; iElement >= 0; iElement--)
                {
                    TrackCircuitRouteElement thisElement = ValidRoute[0][iElement];
                    thisSection = thisElement.TrackCircuitSection;
                    if (thisSection.Index == reversalSection.Index)
                    {
                        break;
                    }
                    else lengthToGoM -= thisSection.Length;
                }
                return lengthToGoM += reverseReversalOffset - reversalSection.Length;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Compute path length
        /// <\summary>

        public float ComputePathLength()
        {
            float pathLength = 0;
            int tcRouteSubpathIndex = -1;
            foreach (var tcRouteSubpath in TCRoute.TCRouteSubpaths)
            {
                tcRouteSubpathIndex++;
                if (tcRouteSubpathIndex > 0 && TCRoute.ReversalInfo[tcRouteSubpathIndex - 1].Valid) pathLength += TCRoute.ReversalInfo[tcRouteSubpathIndex - 1].ReverseReversalOffset;
                else if (tcRouteSubpathIndex > 0) pathLength += TCRoute.ReversalInfo[tcRouteSubpathIndex - 1].ReverseReversalOffset -
                    TrackCircuitSection.TrackCircuitList[TCRoute.ReversalInfo[tcRouteSubpathIndex - 1].ReversalSectionIndex].Length;
                else { } //start point offset?
                int routeListIndex = 1;
                TrackCircuitSection thisSection;
                int reversalRouteIndex = tcRouteSubpath.GetRouteIndex(TCRoute.ReversalInfo[tcRouteSubpathIndex].ReversalSectionIndex, routeListIndex);
                if (reversalRouteIndex == -1)
                {
                    Trace.TraceWarning("Train {0} service {1}, reversal or end point off path; distance to reversal point set to -1", Number, Name);
                    return -1;
                }
                if (routeListIndex <= reversalRouteIndex)
                {
                    for (int iElement = routeListIndex; iElement < tcRouteSubpath.Count; iElement++)
                    {
                        TrackCircuitRouteElement thisElement = tcRouteSubpath[iElement];
                        thisSection = thisElement.TrackCircuitSection;
                        if (thisSection.Index == TCRoute.ReversalInfo[tcRouteSubpathIndex].ReversalSectionIndex)
                        {
                            break;
                        }
                        else pathLength += thisSection.Length;
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


        //================================================================================================//
        /// <summary>
        /// get list of required actions (only if not moving backward)
        /// </summary>

        public void ObtainRequiredActions(int backward)
        {
            if (this is AITrain && (this as AITrain).MovementState == AITrain.AI_MOVEMENT_STATE.SUSPENDED) return;
            if (backward < backwardThreshold)
            {
                List<DistanceTravelledItem> nowActions = requiredActions.GetActions(DistanceTravelledM);
                if (nowActions.Count > 0)
                {
                    PerformActions(nowActions);
                }
            }
            if (backward < backwardThreshold || SpeedMpS > -0.01)
            {
                List<DistanceTravelledItem> nowActions = AuxActionsContain.specRequiredActions.GetAuxActions(this, DistanceTravelledM);

                if (nowActions.Count > 0)
                {
                    PerformActions(nowActions);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update section occupy states
        /// Input is backward movement counter
        /// </summary>

        public void UpdateSectionState(int backward)
        {

            List<int[]> sectionList = new List<int[]>();

            int lastIndex = PreviousPosition[0].RouteListIndex;
            int presentIndex = PresentPosition[0].RouteListIndex;

            int lastDTM = Convert.ToInt32(PreviousPosition[0].DistanceTravelledM);
            TrackCircuitSection lastSection = TrackCircuitSection.TrackCircuitList[PreviousPosition[0].TCSectionIndex];
            int lastDTatEndLastSectionM = lastDTM + Convert.ToInt32(lastSection.Length - PreviousPosition[0].TCOffset);

            int presentDTM = Convert.ToInt32(DistanceTravelledM);

            // don't bother with update if train out of control - all will be reset when train is stopped

            if (ControlMode == TrainControlMode.OutOfControl)
            {
                return;
            }

            // don't bother with update if train off route - set train to out of control

            if (presentIndex < 0)
            {
                SetTrainOutOfControl(OutOfControlReason.OutOfPath);
                return;
            }

            // train moved backward

            if (backward > backwardThreshold)
            {
                if (presentIndex < lastIndex)
                {
                    for (int iIndex = lastIndex; iIndex > presentIndex; iIndex--)
                    {
                        sectionList.Add(new int[2] { iIndex, presentDTM });
                    }
                    sectionList.Add(new int[2] { presentIndex, presentDTM });
                }
            }

            // train moves forward

            else
            {
                if (presentIndex > lastIndex)
                {
                    TrackCircuitSection thisSection;
                    int lastValidDTM = lastDTatEndLastSectionM;

                    for (int iIndex = lastIndex + 1; iIndex < presentIndex; iIndex++)
                    {
                        sectionList.Add(new int[2] { iIndex, lastValidDTM });
                        thisSection = ValidRoute[0][iIndex].TrackCircuitSection;
                        lastValidDTM += Convert.ToInt32(thisSection.Length);
                    }
                    sectionList.Add(new int[2] { presentIndex, presentDTM });
                }
            }

            // set section states, for AUTOMODE use direction 0 only

            foreach (int[] routeListIndex in sectionList)
            {
                TrackCircuitSection thisSection = ValidRoute[0][routeListIndex[0]].TrackCircuitSection;
                if (!thisSection.CircuitState.OccupiedByThisTrain(routedForward))
                {
                    thisSection.SetOccupied(routedForward, routeListIndex[1]);
                    if (!Simulator.TimetableMode && thisSection.CircuitState.OccupiedByOtherTrains(routedForward))
                    {
                        SwitchToNodeControl(thisSection.Index);
                        EndAuthorityTypes[0] = EndAuthorityType.TrainAhead;
                        ChangeControlModeOtherTrains(thisSection);
                    }
                    // additional actions for child classes
                    UpdateSectionState_Additional(thisSection.Index);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Change control mode of other trains in same section if needed
        /// </summary>

        public void ChangeControlModeOtherTrains(TrackCircuitSection thisSection)
        {
            int otherdirection = -1;
            int owndirection = PresentPosition[0].TCDirection;
            foreach (KeyValuePair<TrainRouted, int> trainToCheckInfo in thisSection.CircuitState.OccupationState)
            {
                Train OtherTrain = trainToCheckInfo.Key.Train;
                if (OtherTrain.ControlMode == TrainControlMode.AutoSignal) // train is still in signal mode, might need adjusting
                {
                    otherdirection = OtherTrain.PresentPosition[0].TCSectionIndex == thisSection.Index ? OtherTrain.PresentPosition[0].TCDirection :
                        OtherTrain.PresentPosition[1].TCSectionIndex == thisSection.Index ? OtherTrain.PresentPosition[1].TCDirection : -1;
                    if (owndirection >= 0 && otherdirection >= 0) // both trains found
                    {
                        if (owndirection != otherdirection) // opposite directions - this train is now ahead of train in section
                        {
                            OtherTrain.SwitchToNodeControl(thisSection.Index);
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check if train went passed signal
        /// if so, and signal was at danger, set train Out_Of_Control
        /// </summary>

        public int CheckSignalPassed(int direction, TCPosition trainPosition, TCPosition trainPreviousPos)
        {
            int passedSignalIndex = -1;
            if (NextSignalObject[direction] != null)
            {

                while (NextSignalObject[direction] != null && !ValidRoute[direction].SignalIsAheadOfTrain(NextSignalObject[direction], trainPosition)) // signal not in front //
                {
                    // correct route index if necessary
                    int correctedRouteIndex = ValidRoute[0].GetRouteIndex(trainPreviousPos.TCSectionIndex, 0);
                    if (correctedRouteIndex >= 0) trainPreviousPos.RouteListIndex = correctedRouteIndex;
                    // check if train really went passed signal in correct direction
                    if (ValidRoute[direction].SignalIsAheadOfTrain(NextSignalObject[direction], trainPreviousPos)) // train was in front on last check, so we did pass
                    {
                        SignalAspectState signalState = GetNextSignalAspect(direction);
                        passedSignalIndex = NextSignalObject[direction].Index;
#if DEBUG_SIGNALPASS
                        double passtime = 0;
                        if (TrainType != Train.TRAINTYPE.PLAYER)
                        {
                            AITrain aiocctrain = this as AITrain;
                            passtime = aiocctrain.AI.clockTime;
                        }
                        else
                        {
                            passtime = Simulator.ClockTime;
                        }

                        var sob = new StringBuilder();
                        sob.AppendFormat("{0};{1};{2};{3};{4};{5};{6};{7}", Number, Name, NextSignalObject[direction].SignalHeads[0].TDBIndex.ToString(),signalState.ToString(),
                            passtime,DistanceTravelledM,SpeedMpS,Delay);
                        File.AppendAllText(@"C:\temp\passsignal.txt", sob.ToString() + "\n");
#endif

                        if (signalState == SignalAspectState.Stop && NextSignalObject[direction].OverridePermission == SignalPermission.Denied)
                        {
                            Trace.TraceWarning("Train {1} ({0}) passing signal {2} at {3} at danger at {4}",
                               Number.ToString(), Name, NextSignalObject[direction].Index.ToString(),
                               DistanceTravelledM.ToString("###0.0"), SpeedMpS.ToString("##0.00"));
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

            return (passedSignalIndex);
        }

        //================================================================================================//
        /// <summary>
        /// Check if train moves backward and if so, check clearance behindtrain
        /// If no save clearance left, set train to Out_Of_Control
        /// </summary>

        public int CheckBackwardClearance()
        {
            bool outOfControl = false;

            int lastIndex = PreviousPosition[0].RouteListIndex;
            float lastOffset = PreviousPosition[0].TCOffset;
            int presentIndex = PresentPosition[0].RouteListIndex;
            float presentOffset = PresentPosition[0].TCOffset;

            if (presentIndex < 0) // we are off the path, stop train //
            {
                SetTrainOutOfControl(OutOfControlReason.OutOfPath);
            }

            // backward

            if (presentIndex < lastIndex || (presentIndex == lastIndex && presentOffset < lastOffset))
            {
                movedBackward = movedBackward < 2 * backwardThreshold ? ++movedBackward : movedBackward;
            }

            if (movedBackward > backwardThreshold)
            {
                // run through sections behind train
                // if still in train route : try to reserve section
                // if multiple train in section : calculate distance to next train, stop oncoming train
                // if section reserved for train : stop train
                // if out of route : set out_of_control
                // if signal : set distance, check if passed

                // TODO : check if other train in section, get distance to train
                // TODO : check correct alignment of any switches passed over while moving backward (reset activepins)

                if (RearSignalObject != null)
                {

                    // create new position some 25 m. behind train as allowed overlap

                    TCPosition overlapPosition = new TCPosition();
                    PresentPosition[1].CopyTo(ref overlapPosition);
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[overlapPosition.TCSectionIndex];
                    overlapPosition.TCOffset = thisSection.Length - (PresentPosition[1].TCOffset + rearPositionOverlap);  // reverse offset because of reversed direction
                    overlapPosition.TCDirection = overlapPosition.TCDirection == 0 ? 1 : 0; // looking backwards, so reverse direction

                    TrackCircuitSection rearSection = TrackCircuitSection.TrackCircuitList[RearSignalObject.TrackCircuitNextIndex];
                    if (!IsAheadOfTrain(rearSection, 0.0f, overlapPosition))
                    {
                        if (RearSignalObject.SignalLR(SignalFunction.Normal) == SignalAspectState.Stop)
                        {
                            Trace.TraceWarning("Train {1} ({0}) passing rear signal {2} at {3} at danger at {4}",
                            Number.ToString(), Name, RearSignalObject.Index.ToString(),
                            DistanceTravelledM.ToString("###0.0"), SpeedMpS.ToString("##0.00"));
                            SetTrainOutOfControl(OutOfControlReason.RearPassedAtDanger);
                            outOfControl = true;
                        }
                        else
                        {
                            RearSignalObject = null;   // passed signal, so reset //
                        }
                    }
                }

                if (!outOfControl && RearSignalObject == null)
                {
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[1].TCSectionIndex];
                    float clearPath = thisSection.Length - PresentPosition[1].TCOffset;   // looking other direction //
                    TrackDirection direction = ((TrackDirection)PresentPosition[1].TCDirection).Next();

                    while (clearPath < rearPositionOverlap && !outOfControl && RearSignalObject == null)
                    {
                        if (thisSection.EndSignals[direction] != null)
                        {
                            RearSignalObject = thisSection.EndSignals[direction];
                        }
                        else
                        {
                            TrackDirection pinLink = direction.Next();

                            // TODO : check required junction and crossover path

                            int nextSectionIndex = thisSection.Pins[pinLink, Location.NearEnd].Link;
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
                                    thisSection = nextSection;
                                    if (thisSection.CircuitType == TrackCircuitType.EndOfTrack)
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
                        ClearanceAtRearM = -1;
                        RearSignalObject = null;
                    }
                    else
                    {
                        ClearanceAtRearM = clearPath;
                    }
                }
            }
            else
            {
                movedBackward = movedBackward >= 0 ? --movedBackward : movedBackward;
                ClearanceAtRearM = -1;
                RearSignalObject = null;
            }

            return (movedBackward);

        }

        //================================================================================================//
        //
        /// <summary>
        // Check for end of route actions - for activity PLAYER train only
        // Reverse train if required
        // Return parameter : true if train still exists (only used in timetable mode)
        /// </summary>
        //

        public virtual bool CheckRouteActions(double elapsedClockSeconds)
        {
            int directionNow = PresentPosition[0].TCDirection;
            int positionNow = PresentPosition[0].TCSectionIndex;
            int directionNowBack = PresentPosition[1].TCDirection;
            int positionNowBack = PresentPosition[1].TCSectionIndex;

            if (PresentPosition[0].RouteListIndex >= 0) directionNow = (int)ValidRoute[0][PresentPosition[0].RouteListIndex].Direction;

            bool[] nextRoute = UpdateRouteActions(elapsedClockSeconds, false);

            AuxActionsContain.SetAuxAction(this);
            if (!nextRoute[0]) return (true);  // not at end of route

            // check if train reversed

            if (nextRoute[1])
            {
                if (positionNowBack == PresentPosition[0].TCSectionIndex && directionNowBack != PresentPosition[0].TCDirection)
                {
                    ReverseFormation(IsActualPlayerTrain);
                    // active subpath must be incremented in parallel in incorporated train if present
                    if (IncorporatedTrainNo >= 0) IncrementSubpath(Simulator.TrainDictionary[IncorporatedTrainNo]);
                }
                else if (positionNow == PresentPosition[1].TCSectionIndex && directionNow != PresentPosition[1].TCDirection)
                {
                    ReverseFormation(IsActualPlayerTrain);
                    // active subpath must be incremented in parallel in incorporated train if present
                    if (IncorporatedTrainNo >= 0) IncrementSubpath(Simulator.TrainDictionary[IncorporatedTrainNo]);
                }
            }

            // check if next station was on previous subpath - if so, move to this subpath

            if (nextRoute[1] && StationStops.Count > 0)
            {
                StationStop thisStation = StationStops[0];
                if (thisStation.SubrouteIndex < TCRoute.ActiveSubPath)
                {
                    thisStation.SubrouteIndex = TCRoute.ActiveSubPath;
                }
            }

            return (true); // always return true for activity player train
        }


        //================================================================================================//
        /// <summary>
        /// Check for end of route actions
        /// Called every update, actions depend on route state
        /// returns :
        /// bool[0] "false" end of route not reached
        /// bool[1] "false" if no further route available
        /// </summary>

        public bool[] UpdateRouteActions(double elapsedClockSeconds, bool checkLoop = true)
        {
            bool endOfRoute = false;
            bool[] returnState = new bool[2] { false, false };
            nextRouteReady = false;

            // obtain reversal section index

            int reversalSectionIndex = -1;
            if (TCRoute != null && (ControlMode == TrainControlMode.AutoNode || ControlMode == TrainControlMode.AutoSignal))
            {
                TrackCircuitReversalInfo thisReversal = TCRoute.ReversalInfo[TCRoute.ActiveSubPath];
                if (thisReversal.Valid)
                {
                    reversalSectionIndex = thisReversal.SignalUsed ? thisReversal.LastSignalIndex : thisReversal.LastDivergeIndex;
                }
            }

            // check if train in loop
            // if so, forward to next subroute and continue
            if (checkLoop || StationStops.Count <= 1 || StationStops.Count > 1 && TCRoute != null && StationStops[1].SubrouteIndex > TCRoute.ActiveSubPath)
            {
                if (TCRoute != null && (ControlMode == TrainControlMode.AutoNode || ControlMode == TrainControlMode.AutoSignal) && TCRoute.LoopEnd[TCRoute.ActiveSubPath] >= 0)
                {
                    int loopSectionIndex = ValidRoute[0].GetRouteIndex(TCRoute.LoopEnd[TCRoute.ActiveSubPath], 0);

                    if (loopSectionIndex >= 0 && PresentPosition[1].RouteListIndex > loopSectionIndex)
                    {
                        int frontSection = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][PresentPosition[0].RouteListIndex].TrackCircuitSection.Index;
                        int rearSection = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath][PresentPosition[1].RouteListIndex].TrackCircuitSection.Index;
                        TCRoute.ActiveSubPath++;
                        ValidRoute[0] = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];

                        PresentPosition[0].RouteListIndex = ValidRoute[0].GetRouteIndex(frontSection, 0);
                        PresentPosition[1].RouteListIndex = ValidRoute[0].GetRouteIndex(rearSection, 0);

                        // Invalidate preceding section indexes to avoid wrong indexing when building route forward (in Reserve())

                        for (int routeListIndex = 0; routeListIndex < PresentPosition[1].RouteListIndex; routeListIndex++)
                        {
                            ValidRoute[0][routeListIndex].Invalidate();
                        }
                        returnState[0] = true;
                        returnState[1] = true;
                        return (returnState);
                    }

                    // if loopend no longer on this valid route, remove loopend indication
                    else if (loopSectionIndex < 0)
                    {
                        TCRoute.LoopEnd[TCRoute.ActiveSubPath] = -1;
                    }
                }
            }

            // check position in relation to present end of path

            endOfRoute = CheckEndOfRoutePosition();

            // not end of route - no action

            if (!endOfRoute)
            {
                return (returnState);
            }

            // <CSComment> TODO: check if holding signals correctly released in case of reversal point between WP and signal

            // if next subpath available : check if it can be activated

            bool nextRouteAvailable = false;

            TrackCircuitPartialPathRoute nextRoute = null;

            if (endOfRoute && TCRoute.ActiveSubPath < (TCRoute.TCRouteSubpaths.Count - 1))
            {
                nextRouteAvailable = true;

                nextRoute = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath + 1];
                int firstSectionIndex = PresentPosition[1].TCSectionIndex;

                // find index of present rear position

                int firstRouteIndex = nextRoute.GetRouteIndex(firstSectionIndex, 0);

                // if not found try index of present front position

                if (firstRouteIndex >= 0)
                {
                    nextRouteReady = true;
                }
                else
                {
                    firstSectionIndex = PresentPosition[0].TCSectionIndex;
                    firstRouteIndex = nextRoute.GetRouteIndex(firstSectionIndex, 0);

                    // cant find next part of route - check if really at end of this route, if so, error, else just wait and see (train stopped for other reason)

                    if (PresentPosition[0].RouteListIndex == ValidRoute[0].Count - 1)
                    {
                        if (firstRouteIndex < 0)
                        {
                            Trace.TraceInformation(
                                "Cannot find next part of route (index {0}) for Train {1} ({2}) (at section {3})",
                                TCRoute.ActiveSubPath.ToString(), Name, Number.ToString(),
                                PresentPosition[0].TCSectionIndex.ToString());
                        }
                        // search for junction and check if it is not clear

                        else
                        {
                            bool junctionFound = false;
                            bool junctionOccupied = false;

                            for (int iIndex = firstRouteIndex + 1; iIndex < nextRoute.Count && !junctionFound; iIndex++)
                            {
                                int thisSectionIndex = nextRoute[iIndex].TrackCircuitSection.Index;
                                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
                                if (thisSection.CircuitType == TrackCircuitType.Junction)
                                {
                                    junctionFound = true;
                                    if (thisSection.CircuitState.OccupiedByThisTrain(this))
                                    {
                                        // Before deciding that route is not yet ready check if the new train head is off path because at end of new route
                                        var thisElement = nextRoute[nextRoute.Count - 1];
                                        thisSection = thisElement.TrackCircuitSection;
                                        if (thisSection.CircuitState.OccupiedByThisTrain(this)) break;
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
                    if (NextSignalObject[0] != null && NextSignalObject[0].EnabledTrain == routedForward)
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

                    if (PresentPosition[0].RouteListIndex >= 0 && PresentPosition[0].RouteListIndex < ValidRoute[0].Count - 1) // not at end of route
                    {
                        int nextRouteIndex = PresentPosition[0].RouteListIndex + 1;
                        signalRef.BreakDownRouteList(ValidRoute[0], nextRouteIndex, routedForward);
                        ValidRoute[0].RemoveRange(nextRouteIndex, ValidRoute[0].Count - nextRouteIndex);
                    }
                }

                int nextIndex = PresentPosition[0].RouteListIndex + 1;
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

                int newIndex = nextRoute.GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
                var oldDirection = ValidRoute[0][PresentPosition[0].RouteListIndex].Direction;
                if (newIndex < 0)
                {
                    newIndex = nextRoute.GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
                    oldDirection = ValidRoute[0][PresentPosition[1].RouteListIndex].Direction;
                }

                if (oldDirection != nextRoute[newIndex].Direction)
                {

                    // set new train positions and reset distance travelled

                    TCPosition tempPosition = new TCPosition();
                    PresentPosition[0].CopyTo(ref tempPosition);
                    PresentPosition[1].CopyTo(ref PresentPosition[0]);
                    tempPosition.CopyTo(ref PresentPosition[1]);

                    PresentPosition[0].Reverse(ValidRoute[0][PresentPosition[0].RouteListIndex].Direction, nextRoute, Length);
                    PresentPosition[0].CopyTo(ref PreviousPosition[0]);
                    PresentPosition[1].Reverse(ValidRoute[0][PresentPosition[1].RouteListIndex].Direction, nextRoute, 0.0f);
                }
                else
                {
                    PresentPosition[0].RouteListIndex = nextRoute.GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
                    PresentPosition[1].RouteListIndex = nextRoute.GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
                    PresentPosition[0].CopyTo(ref PreviousPosition[0]);
                }

                DistanceTravelledM = PresentPosition[0].DistanceTravelledM;

                // perform any remaining actions of type clear section (except sections now occupied)

                // reset old actions
                ClearActiveSectionItems();

                // set new route
                TCRoute.ActiveSubPath++;
                ValidRoute[0] = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];


                TCRoute.SetReversalOffset(Length, Simulator.TimetableMode);

                // clear existing list of occupied track, and build new list
                for (int iSection = OccupiedTrack.Count - 1; iSection >= 0; iSection--)
                {
                    TrackCircuitSection thisSection = OccupiedTrack[iSection];
                    thisSection.ResetOccupied(this);

                }
                int rearIndex = PresentPosition[1].RouteListIndex;

                if (rearIndex < 0) // end of train not on new route
                {
                    TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset,
                        (TrackDirection)PresentPosition[1].TCDirection, Length, false, true, false);

                    for (int iIndex = 0; iIndex < tempRoute.Count; iIndex++)
                    {
                        TrackCircuitSection thisSection = tempRoute[iIndex].TrackCircuitSection;
                        thisSection.SetOccupied(routedForward);
                    }
                }
                else
                {
                    for (int iIndex = PresentPosition[1].RouteListIndex; iIndex <= PresentPosition[0].RouteListIndex; iIndex++)
                    {
                        TrackCircuitSection thisSection = ValidRoute[0][iIndex].TrackCircuitSection;
                        thisSection.SetOccupied(routedForward);
                    }
                }

                // Check deadlock against all other trains
                CheckDeadlock(ValidRoute[0], Number);

                // reset signal information

                SignalObjectItems.Clear();
                NextSignalObject[0] = null;

                InitializeSignals(true);

                LastReservedSection[0] = PresentPosition[0].TCSectionIndex;

                // clear claims of any trains which have claimed present occupied sections upto common point - this avoids deadlocks
                // trains may have claimed while train was reversing

                TrackCircuitSection presentSection = TrackCircuitSection.TrackCircuitList[LastReservedSection[0]];
                presentSection.ClearReversalClaims(routedForward);

                // switch to NODE mode
                if (ControlMode == TrainControlMode.AutoSignal)
                {
                    SwitchToNodeControl(PresentPosition[0].TCSectionIndex);
                }
            }

            returnState[0] = endOfRoute;
            returnState[1] = nextRouteAvailable;

            return (returnState);  // return state
        }

        //================================================================================================//
        /// <summary>
        /// Check End of Route Position
        /// </summary>

        public virtual bool CheckEndOfRoutePosition()
        {
            bool endOfRoute = false;

            // obtain reversal section index

            int reversalSectionIndex = -1;
            if (TCRoute != null && (ControlMode == TrainControlMode.AutoNode || ControlMode == TrainControlMode.AutoSignal))
            {
                TrackCircuitReversalInfo thisReversal = TCRoute.ReversalInfo[TCRoute.ActiveSubPath];
                if (thisReversal.Valid)
                {
                    reversalSectionIndex = thisReversal.SignalUsed ? thisReversal.LastSignalIndex : thisReversal.LastDivergeIndex;
                }
            }

            // check if present subroute ends in reversal or is last subroute
            if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid || TCRoute.ActiveSubPath == TCRoute.TCRouteSubpaths.Count - 1)
            {
                // can only be performed if train is stationary

                if (Math.Abs(SpeedMpS) > 0.03)
                    return (endOfRoute);

                // check position in relation to present end of path
                // front is in last route section
                if (PresentPosition[0].RouteListIndex == (ValidRoute[0].Count - 1) &&
                    (!TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid && TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1))
                {
                    endOfRoute = true;
                }
                // front is within 150m. of end of route and no junctions inbetween (only very short sections ahead of train)
                else
                {
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[0].TCSectionIndex];
                    float lengthToGo = thisSection.Length - PresentPosition[0].TCOffset;

                    bool junctionFound = false;
                    if (TCRoute.ActiveSubPath < TCRoute.TCRouteSubpaths.Count - 1)
                    {
                        for (int iIndex = PresentPosition[0].RouteListIndex + 1; iIndex < ValidRoute[0].Count && !junctionFound; iIndex++)
                        {
                            thisSection = ValidRoute[0][iIndex].TrackCircuitSection;
                            junctionFound = thisSection.CircuitType == TrackCircuitType.Junction;
                            lengthToGo += thisSection.Length;
                        }
                    }
                    else lengthToGo = ComputeDistanceToReversalPoint();
                    float compatibilityNegligibleRouteChunk = ((TrainType == TrainType.Ai || TrainType == TrainType.AiPlayerHosting)
                        && TCRoute.TCRouteSubpaths.Count - 1 == TCRoute.ActiveSubPath) ? 40f : 5f;
                    float negligibleRouteChunk = compatibilityNegligibleRouteChunk;

                    if (lengthToGo < negligibleRouteChunk && !junctionFound && !TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                    {
                        endOfRoute = true;
                    }
                }

                //<CSComment: check of vicinity to reverse point; only in subpaths ending with reversal
                if (TCRoute.ReversalInfo[TCRoute.ActiveSubPath].Valid)
                {
                    float distanceToReversalPoint = ComputeDistanceToReversalPoint();
                    if (distanceToReversalPoint < 50 && PresentPosition[1].RouteListIndex >= reversalSectionIndex)
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
                    if (NextSignalObject[0] != null && PresentPosition[0].TCSectionIndex == NextSignalObject[0].TrackCircuitIndex &&
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
                var distanceToReversalPoint = ComputeDistanceToReversalPoint();
                if (distanceToReversalPoint <= 0 && distanceToReversalPoint != -1) endOfRoute = true;
            }

            return (endOfRoute);
        }

        //================================================================================================//
        /// <summary>
        /// Update route clearance ahead of train
        /// Called every update, actions depend on present control state
        /// </summary>

        public void UpdateRouteClearanceAhead(int signalObjectIndex, int backward, double elapsedClockSeconds)
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
                var signalObject = signalRef.Signals[signalObjectIndex];

                //the following is added by JTang, passing a hold signal, will take back control by the system
                if (signalObject.HoldState == SignalHoldState.ManualPass ||
                    signalObject.HoldState == SignalHoldState.ManualApproach)
                {
                    signalObject.HoldState = SignalHoldState.None;
                }

                signalObject.ResetSignalEnabled();
            }
        }

        //================================================================================================//
        /// <summary>
        /// Perform auto signal mode update
        /// </summary>

        public void UpdateSignalMode(int signalObjectIndex, int backward, double elapsedClockSeconds)
        {
            // in AUTO mode, use forward route only
            // if moving backward, check if slipped passed signal, if so, re-enable signal

            if (backward > backwardThreshold)
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
                var thisSignal = signalRef.Signals[signalObjectIndex];
                int nextSignalIndex = thisSignal.Signalfound[(int)SignalFunction.Normal];
                if (nextSignalIndex >= 0)
                {
                    var nextSignal = signalRef.Signals[nextSignalIndex];
                    nextSignal.RequestClearSignal(ValidRoute[0], routedForward, 0, false, null);
                }
            }

            // if next signal not enabled or enabled for other train, also send request (can happen after choosing passing path or after detach)

            else if (NextSignalObject[0] != null && (!NextSignalObject[0].Enabled || NextSignalObject[0].EnabledTrain != routedForward))
            {
                NextSignalObject[0].RequestClearSignal(ValidRoute[0], routedForward, 0, false, null);
            }


            // check if waiting for signal

            else if (SpeedMpS < Math.Abs(0.1) &&
             NextSignalObject[0] != null &&
                     GetNextSignalAspect(0) == SignalAspectState.Stop &&
                     CheckTrainWaitingForSignal(NextSignalObject[0], 0))
            {
                bool hasClaimed = ClaimState;
                bool claimAllowed = true;

                // perform special actions on stopped at signal for specific train classes
                ActionsForSignalStop(ref claimAllowed);

                // cannot claim on deadlock to prevent further deadlocks
                bool DeadlockWait = CheckDeadlockWait(NextSignalObject[0]);
                if (DeadlockWait) claimAllowed = false;

                // cannot claim while in waitstate as this would lock path for other train
                if (isInWaitState()) claimAllowed = false;

                // cannot claim on hold signal
                if (HoldingSignals.Contains(NextSignalObject[0].Index)) claimAllowed = false;

                // process claim if allowed
                if (claimAllowed)
                {
                    if (CheckStoppedTrains(NextSignalObject[0].SignalRoute)) // do not claim when train ahead is stationary or in Manual mode
                    {
                        actualWaitTimeS = standardWaitTimeS;  // allow immediate claim if other train moves
                        ClaimState = false;
                    }
                    else
                    {
                        actualWaitTimeS += elapsedClockSeconds;
                        if (actualWaitTimeS > standardWaitTimeS)
                        {
                            ClaimState = true;
                        }
                    }
                }
                else
                {
                    actualWaitTimeS = 0.0;
                    ClaimState = false;

                    // Reset any invalid claims (occurs on WAIT commands, reason still to be checked!) - not unclaiming causes deadlocks
                    for (int iIndex = PresentPosition[0].RouteListIndex; iIndex <= ValidRoute[0].Count - 1; iIndex++)
                    {
                        TrackCircuitSection claimSection = ValidRoute[0][iIndex].TrackCircuitSection;
                        claimSection.CircuitState.TrainClaimed.Remove(routedForward);
                    }
                }
            }
            else
            {
                actualWaitTimeS = 0.0;
                ClaimState = false;
            }
        }

        //================================================================================================//
        //
        // Check if train is waiting for a stationary (stopped) train or a train in manual mode
        //

        public bool CheckStoppedTrains(TrackCircuitPartialPathRoute thisRoute)
        {
            foreach (TrackCircuitRouteElement thisElement in thisRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                foreach (KeyValuePair<TrainRouted, int> thisTrain in thisSection.CircuitState.OccupationState)
                {
                    if (thisTrain.Key.Train.SpeedMpS == 0.0f)
                    {
                        return (true);
                    }
                    if (thisTrain.Key.Train.ControlMode == TrainControlMode.Manual)
                    {
                        return (true);
                    }
                }
            }

            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Test if call on allowed
        /// </summary>
        /// <param name="thisSignal"></param>
        /// <param name="allowOnNonePlatform"></param>
        /// <param name="thisRoute"></param>
        /// <param name="dumpfile"></param>
        /// <returns></returns>
        /// 

        public virtual bool TestCallOn(Signal thisSignal, bool allowOnNonePlatform, TrackCircuitPartialPathRoute thisRoute)
        {
            bool intoPlatform = false;

            foreach (TrackCircuitRouteElement routeElement in thisSignal.SignalRoute)
            {
                TrackCircuitSection routeSection = routeElement.TrackCircuitSection;

                // check if route leads into platform

                if (routeSection.PlatformIndices.Count > 0)
                {
                    intoPlatform = true;
                }
            }

            if (!intoPlatform)
            {
                //if track does not lead into platform, return state as defined in call
                return (allowOnNonePlatform);
            }
            else
            {
                // never allow if track leads into platform
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check if train is waiting for signal
        /// </summary>

        public bool CheckTrainWaitingForSignal(Signal thisSignal, int direction)
        {
            TrainRouted thisRouted = direction == 0 ? routedForward : routedBackward;
            int trainRouteIndex = PresentPosition[direction].RouteListIndex;
            int signalRouteIndex = ValidRoute[direction].GetRouteIndex(thisSignal.TrackCircuitIndex, trainRouteIndex);

            // signal section is not in train route, so train can't be waiting for signal

            if (signalRouteIndex < 0)
            {
                return (false);
            }

            // check if any other trains in section ahead of this train

            TrackCircuitSection thisSection = ValidRoute[0][trainRouteIndex].TrackCircuitSection;

            Dictionary<Train, float> trainAhead = thisSection.TestTrainAhead(this,
                    PresentPosition[0].TCOffset, PresentPosition[0].TCDirection);

            if (trainAhead.Count > 0)
            {
                KeyValuePair<Train, float> foundTrain = trainAhead.ElementAt(0);
                // check if train is closer as signal
                if (!DistanceToSignal.HasValue || foundTrain.Value < DistanceToSignal)
                {
                    return (false);
                }
            }

            // check if any other sections inbetween train and signal

            if (trainRouteIndex != signalRouteIndex)
            {
                for (int iIndex = trainRouteIndex + 1; iIndex <= signalRouteIndex; iIndex++)
                {
                    TrackCircuitSection nextSection = ValidRoute[0][iIndex].TrackCircuitSection;

                    if (nextSection.CircuitState.Occupied())  // train is ahead - it's not our signal //
                    {
                        return (false);
                    }
                    else if (!nextSection.IsAvailable(this)) // is section really available to us? //

                    // something is wrong - section upto signal is not available - give warning and switch to node control
                    // also reset signal if it was enabled to us
                    {
                        Trace.TraceWarning("Train {0} ({1}) in Signal control but route to signal not cleared - switching to Node control",
                                Name, Number);

                        if (thisSignal.EnabledTrain == thisRouted)
                        {
                            thisSignal.ResetSignal(true);
                        }
                        SwitchToNodeControl(thisSection.Index);

                        return (false);
                    }
                }
            }

            // we are waiting, but is signal clearance requested ?

            if (thisSignal.EnabledTrain == null)
            {
                thisSignal.RequestClearSignal(ValidRoute[0], thisRouted, 0, false, null);
            }

            // we are waiting, but is it really our signal ?

            else if (thisSignal.EnabledTrain != thisRouted)
            {

                // something is wrong - we are waiting, but it is not our signal - give warning, reset signal and clear route

                Trace.TraceWarning("Train {0} ({1}) waiting for signal which is enabled to train {2}",
                        Name, Number, thisSignal.EnabledTrain.Train.Number);

                // stop other train - switch other train to node control

                Train otherTrain = thisSignal.EnabledTrain.Train;
                otherTrain.LastReservedSection[0] = -1;
                if (Math.Abs(otherTrain.SpeedMpS) > 0)
                {
                    otherTrain.ForcedStop(Simulator.Catalog.GetString("Stopped due to errors in route setting"), Name, Number);
                }
                otherTrain.SwitchToNodeControl(-1);

                // reset signal and clear route

                thisSignal.ResetSignal(false);
                thisSignal.RequestClearSignal(ValidRoute[0], thisRouted, 0, false, null);
                return (false);   // do not yet set to waiting, signal might clear //
            }

            // signal is in holding list - so not really waiting - but remove from list if held for station stop

            if (thisSignal.HoldState == SignalHoldState.ManualLock)
            {
                return (false);
            }
            else if (thisSignal.HoldState == SignalHoldState.StationStop && HoldingSignals.Contains(thisSignal.Index))
            {
                if (StationStops != null && StationStops.Count > 0 && StationStops[0].ExitSignal != thisSignal.Index) // not present station stop
                {
                    HoldingSignals.Remove(thisSignal.Index);
                    thisSignal.HoldState = SignalHoldState.None;
                    return (false);
                }
            }

            return (true);  // it is our signal and we are waiting //
        }

        //================================================================================================//
        /// <summary>
        /// Breakdown claimed route when signal set to hold
        /// </summary>

        public void BreakdownClaim(TrackCircuitSection thisSection, int routeDirectionIndex, TrainRouted thisTrainRouted)
        {
            TrackCircuitSection nextSection = thisSection;
            int routeIndex = ValidRoute[routeDirectionIndex].GetRouteIndex(thisSection.Index, PresentPosition[routeDirectionIndex].RouteListIndex);
            bool isClaimed = thisSection.CircuitState.TrainClaimed.Contains(thisTrainRouted);

            for (int iIndex = routeIndex + 1; iIndex < (ValidRoute[routeDirectionIndex].Count - 1) && isClaimed; iIndex++)
            {
                thisSection.RemoveTrain(this, false);
                nextSection = ValidRoute[routeDirectionIndex][iIndex].TrackCircuitSection;
            }
        }


        //================================================================================================//
        /// <summary>
        /// Perform auto node mode update
        /// </summary>

        public virtual void UpdateNodeMode()
        {

            // update distance to end of authority

            int lastRouteIndex = ValidRoute[0].GetRouteIndex(LastReservedSection[0], PresentPosition[0].RouteListIndex);

            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[0].TCSectionIndex];
            DistanceToEndNodeAuthorityM[0] = thisSection.Length - PresentPosition[0].TCOffset;

            for (int iSection = PresentPosition[0].RouteListIndex + 1; iSection <= lastRouteIndex; iSection++)
            {
                thisSection = ValidRoute[0][iSection].TrackCircuitSection;
                DistanceToEndNodeAuthorityM[0] += thisSection.Length;
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

        //================================================================================================//
        /// <summary>
        /// Switches switch after dispatcher window command, when in auto mode
        /// </summary>

        public bool ProcessRequestAutoSetSwitch(int reqSwitchIndex)
        {
            TrackCircuitSection reqSwitch = TrackCircuitSection.TrackCircuitList[reqSwitchIndex];

            bool switchSet = false;
            if (reqSwitch.CircuitState.TrainReserved != null && reqSwitch.CircuitState.TrainReserved.Train == this)
            {
                // store required position
                int reqSwitchPosition = reqSwitch.JunctionSetManual;
                ClearReservedSections();
                Reinitialize();
                reqSwitch.JunctionSetManual = reqSwitchPosition;
            }
            switchSet = true;
            return switchSet;
        }

        //================================================================================================//
        /// <summary>
        /// Update section occupy states for manual mode
        /// Note : manual mode has no distance actions so sections must be cleared immediately
        /// </summary>

        public void UpdateSectionStateManual()
        {
            // occupation is set in forward mode only
            // build route from rear to front - before reset occupy so correct switch alignment is used
            TrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset, (TrackDirection)PresentPosition[1].TCDirection, Length, false, true, false);

            // save present occupation list

            List<TrackCircuitSection> clearedSections = new List<TrackCircuitSection>();
            for (int iindex = OccupiedTrack.Count - 1; iindex >= 0; iindex--)
            {
                clearedSections.Add(OccupiedTrack[iindex]);
            }

            // set track occupied

            OccupiedTrack.Clear();

            foreach (TrackCircuitRouteElement thisElement in TrainRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

                if (clearedSections.Contains(thisSection))
                {
                    thisSection.ResetOccupied(this); // reset occupation if it was occupied
                    clearedSections.Remove(thisSection);  // remove from cleared list
                }

                thisSection.Reserve(routedForward, TrainRoute);  // reserve first to reset switch alignments
                thisSection.SetOccupied(routedForward);
            }

            foreach (TrackCircuitSection exSection in clearedSections)
            {
                exSection.ClearOccupied(this, true); // sections really cleared
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update Manual Mode
        /// </summary>

        public void UpdateManualMode(int signalObjectIndex)
        {
            // check present forward
            TrackCircuitPartialPathRoute newRouteF = CheckManualPath(0, PresentPosition[0], ValidRoute[0], true, ref EndAuthorityTypes[0],
                ref DistanceToEndNodeAuthorityM[0]);
            ValidRoute[0] = newRouteF;
            int routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
            PresentPosition[0].RouteListIndex = routeIndex;

            // check present reverse
            // reverse present rear position direction to build correct path backwards
            TCPosition tempRear = new TCPosition();
            PresentPosition[1].CopyTo(ref tempRear);
            tempRear.TCDirection = tempRear.TCDirection == 0 ? 1 : 0;
            TrackCircuitPartialPathRoute newRouteR = CheckManualPath(1, tempRear, ValidRoute[1], true, ref EndAuthorityTypes[1],
                ref DistanceToEndNodeAuthorityM[1]);
            ValidRoute[1] = newRouteR;


            // select valid route

            if (MUDirection == MidpointDirection.Forward)
            {
                // use position from other end of section
                float reverseOffset = TrackCircuitSection.TrackCircuitList[PresentPosition[1].TCSectionIndex].Length - PresentPosition[1].TCOffset;
                CheckSpeedLimitManual(ValidRoute[1], TrainRoute, reverseOffset, PresentPosition[1].TCOffset, signalObjectIndex, 0);
            }
            else
            {
                TrackCircuitPartialPathRoute tempRoute = new TrackCircuitPartialPathRoute(); // reversed trainRoute
                for (int iindex = TrainRoute.Count - 1; iindex >= 0; iindex--)
                {
                    TrackCircuitRouteElement thisElement = TrainRoute[iindex];
                    thisElement.Direction = thisElement.Direction.Next();
                    tempRoute.Add(thisElement);
                }
                float reverseOffset = TrackCircuitSection.TrackCircuitList[PresentPosition[0].TCSectionIndex].Length - PresentPosition[0].TCOffset;
                CheckSpeedLimitManual(ValidRoute[0], tempRoute, PresentPosition[0].TCOffset, reverseOffset, signalObjectIndex, 1);
            }

            // reset signal

            if (signalObjectIndex >= 0)
            {
                var thisSignal = signalRef.Signals[signalObjectIndex];
                thisSignal.OverridePermission = SignalPermission.Denied;
                //the following is added by JTang, passing a hold signal, will take back control by the system
                if (thisSignal.HoldState == SignalHoldState.ManualPass ||
                    thisSignal.HoldState == SignalHoldState.ManualApproach) thisSignal.HoldState = SignalHoldState.None;

                thisSignal.ResetSignalEnabled();
            }

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

            // clear all build up distance actions
            requiredActions.RemovePendingAIActionItems(true);
        }


        //================================================================================================//
        /// <summary>
        /// Check Manual Path
        /// <\summary>

        public TrackCircuitPartialPathRoute CheckManualPath(int direction, TCPosition requiredPosition, TrackCircuitPartialPathRoute requiredRoute, bool forward,
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

            int thisRouteIndex = newRoute.GetRouteIndex(requiredPosition.TCSectionIndex, 0);
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
                            nextSection.ActivePins[TrackDirection.Ahead, Location.NearEnd].Link == requiredPosition.TCSectionIndex)
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
                    signalRef.BreakDownRouteList(requiredRoute, 0, thisRouted);
                    requiredRoute.Clear();
                }


                // build new route

                MisalignedSwitch[direction, 0] = -1;
                MisalignedSwitch[direction, 1] = -1;

                List<int> tempSections = new List<int>();
                tempSections = SignalEnvironment.ScanRoute(this, requiredPosition.TCSectionIndex, requiredPosition.TCOffset,
                        (TrackDirection)requiredPosition.TCDirection, forward, minCheckDistanceManualM, true, false,
                        true, false, true, false, false, false, false, IsFreight);

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
            thisSection = TrackCircuitSection.TrackCircuitList[requiredPosition.TCSectionIndex];
            offsetM = direction == 0 ? requiredPosition.TCOffset : thisSection.Length - requiredPosition.TCOffset;
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
                        endSignal.EnabledTrain = thisRouted;
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
                offsetM = direction == 0 ? requiredPosition.TCOffset : thisSection.Length - requiredPosition.TCOffset;

                Dictionary<Train, float> firstTrainInfo = thisSection.TestTrainAhead(this, offsetM, (int)reqDirection);
                if (firstTrainInfo.Count > 0)
                {
                    foreach (KeyValuePair<Train, float> thisTrainAhead in firstTrainInfo)  // there is only one value
                    {
                        endAuthority = EndAuthorityType.TrainAhead;
                        endAuthorityDistanceM = thisTrainAhead.Value;
                        if (!thisSection.CircuitState.OccupiedByThisTrain(this))
                            thisSection.PreReserve(thisRouted);
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
                                reqDirection = forward ? (TrackDirection)nextElement.Direction : ((TrackDirection)nextElement.Direction).Next();

                                bool oppositeTrain = nextSection.CircuitState.Occupied(oppositeDirection, false);

                                if (!oppositeTrain)
                                {
                                    Dictionary<Train, float> nextTrainInfo = nextSection.TestTrainAhead(this, 0.0f, (int)reqDirection);
                                    if (nextTrainInfo.Count > 0)
                                    {
                                        foreach (KeyValuePair<Train, float> thisTrainAhead in nextTrainInfo)  // there is only one value
                                        {
                                            endAuthority = EndAuthorityType.TrainAhead;
                                            endAuthorityDistanceM = thisTrainAhead.Value + totalLengthM;
                                            lastValidSectionIndex++;
                                            nextSection.PreReserve(thisRouted);
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
                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("No signal in train's path"));
                return;
            }

            var requestedSignal = lastSection.EndSignals[lastElement.Direction];
            if (requestedSignal.EnabledTrain != null && requestedSignal.EnabledTrain.Train != this)
            {
                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Next signal already allocated to other train"));
                Simulator.SoundNotify = TrainEvent.PermissionDenied;
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
                    if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                        Simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Request to clear signal cannot be processed"));
                    Simulator.SoundNotify = TrainEvent.PermissionDenied;
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
                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Confirm(
                        (direction == Direction.Forward) ? CabControl.SwitchAhead : CabControl.SwitchBehind,
                        CabSetting.On);
            }
            else
            {
                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("No switch found"));
            }

            return (switchSet);
        }

        public bool ProcessRequestManualSetSwitch(int reqSwitchIndex)
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
                return (true);
            }

            return (false);
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
                selectedRoute = CheckManualPath(0, PresentPosition[0], null, true, ref EndAuthorityTypes[0],
                    ref DistanceToEndNodeAuthorityM[0]);
                routeIndex = 0;

            }
            else
            {
                TCPosition tempRear = new TCPosition();
                PresentPosition[1].CopyTo(ref tempRear);
                tempRear.TCDirection = tempRear.TCDirection == 0 ? 1 : 0;
                selectedRoute = CheckManualPath(1, tempRear, null, true, ref EndAuthorityTypes[1],
                     ref DistanceToEndNodeAuthorityM[1]);
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
                    if (Simulator.TimetableMode) AllowedMaxSpeedMpS = thisSpeedMpS;
                    else AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedLimitMpS, Math.Min(allowedMaxTempSpeedLimitMpS,
                                       allowedMaxSpeedSignalMpS == -1 ? 999 : allowedMaxSpeedSignalMpS));
                }
            }
            // No speed limits behind us, initialize allowedMaxSpeedLimitMpS.
            else if (!Simulator.TimetableMode)
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
                        if (Simulator.TimetableMode)
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
                            if (Simulator.TimetableMode) AllowedMaxSpeedMpS = Math.Min(AllowedMaxSpeedMpS, allowedMaxSpeedSignalMpS);
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
                                if (Simulator.TimetableMode)
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
            TrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset,
                            (TrackDirection)PresentPosition[1].TCDirection, Length, false, true, false);

            // save present occupation list

            List<TrackCircuitSection> clearedSections = new List<TrackCircuitSection>();
            for (int iindex = OccupiedTrack.Count - 1; iindex >= 0; iindex--)
            {
                clearedSections.Add(OccupiedTrack[iindex]);
            }

            // first check for misaligned switch

            int reqDirection = MUDirection == MidpointDirection.Forward ? 0 : 1;
            foreach (TrackCircuitRouteElement thisElement in TrainRoute)
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

            foreach (TrackCircuitRouteElement thisElement in TrainRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

                if (clearedSections.Contains(thisSection))
                {
                    thisSection.ResetOccupied(this); // reset occupation if it was occupied
                    clearedSections.Remove(thisSection);  // remove from cleared list
                }

                thisSection.Reserve(routedForward, TrainRoute);  // reserve first to reset switch alignments
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
            TrackCircuitPartialPathRoute newRouteF = CheckExplorerPath(0, PresentPosition[0], ValidRoute[0], true, ref EndAuthorityTypes[0],
                ref DistanceToEndNodeAuthorityM[0]);
            ValidRoute[0] = newRouteF;
            int routeIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
            PresentPosition[0].RouteListIndex = routeIndex;

            // check present reverse
            // reverse present rear position direction to build correct path backwards
            TCPosition tempRear = new TCPosition();
            PresentPosition[1].CopyTo(ref tempRear);
            tempRear.TCDirection = tempRear.TCDirection == 0 ? 1 : 0;
            TrackCircuitPartialPathRoute newRouteR = CheckExplorerPath(1, tempRear, ValidRoute[1], true, ref EndAuthorityTypes[1],
                ref DistanceToEndNodeAuthorityM[1]);
            ValidRoute[1] = newRouteR;

            // select valid route

            if (MUDirection == MidpointDirection.Forward)
            {
                // use position from other end of section
                float reverseOffset = TrackCircuitSection.TrackCircuitList[PresentPosition[1].TCSectionIndex].Length - PresentPosition[1].TCOffset;
                CheckSpeedLimitManual(ValidRoute[1], TrainRoute, reverseOffset, PresentPosition[1].TCOffset, signalObjectIndex, 0);
            }
            else
            {
                TrackCircuitPartialPathRoute tempRoute = new TrackCircuitPartialPathRoute(); // reversed trainRoute
                for (int iindex = TrainRoute.Count - 1; iindex >= 0; iindex--)
                {
                    TrackCircuitRouteElement thisElement = TrainRoute[iindex];
                    thisElement.Direction = thisElement.Direction.Next();
                    tempRoute.Add(thisElement);
                }
                float reverseOffset = TrackCircuitSection.TrackCircuitList[PresentPosition[0].TCSectionIndex].Length - PresentPosition[0].TCOffset;
                CheckSpeedLimitManual(ValidRoute[0], tempRoute, PresentPosition[0].TCOffset, reverseOffset, signalObjectIndex, 1);
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

        public TrackCircuitPartialPathRoute CheckExplorerPath(int direction, TCPosition requiredPosition, TrackCircuitPartialPathRoute requiredRoute, bool forward,
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

            int thisRouteIndex = newRoute.GetRouteIndex(requiredPosition.TCSectionIndex, 0);
            if (thisRouteIndex < 0)    // no valid point in route
            {
                if (requiredRoute != null && requiredRoute.Count > 0)  // if route defined, then breakdown route
                {
                    signalRef.BreakDownRouteList(requiredRoute, 0, thisRouted);
                    requiredRoute.Clear();
                }

                // build new route

                List<int> tempSections = new List<int>();

                tempSections = SignalEnvironment.ScanRoute(this, requiredPosition.TCSectionIndex, requiredPosition.TCOffset,
                        (TrackDirection)requiredPosition.TCDirection, forward, -1, true, false,
                        false, false, true, false, false, false, false, IsFreight);

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
            thisSection = TrackCircuitSection.TrackCircuitList[requiredPosition.TCSectionIndex];
            offsetM = direction == 0 ? requiredPosition.TCOffset : thisSection.Length - requiredPosition.TCOffset;
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
                offsetM = direction == 0 ? requiredPosition.TCOffset : thisSection.Length - requiredPosition.TCOffset;

                Dictionary<Train, float> firstTrainInfo = thisSection.TestTrainAhead(this, offsetM, (int)reqDirection);
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
                                    Dictionary<Train, float> nextTrainInfo = nextSection.TestTrainAhead(this, 0.0f, (int)reqDirection);
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

        public void RequestExplorerSignalPermission(ref TrackCircuitPartialPathRoute selectedRoute, int routeIndex)
        {
            // check route for first signal at danger, from present position

            Signal reqSignal = null;
            bool signalFound = false;

            if (ValidRoute[routeIndex] != null)
            {
                for (int iIndex = PresentPosition[routeIndex].RouteListIndex; iIndex <= ValidRoute[routeIndex].Count - 1 && !signalFound; iIndex++)
                {
                    TrackCircuitSection thisSection = ValidRoute[routeIndex][iIndex].TrackCircuitSection;
                    TrackDirection direction = ValidRoute[routeIndex][iIndex].Direction;

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
                if (Simulator.Confirmer != null && TrainType != TrainType.Remote) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("No signal in train's path"));
                return;
            }

            // signal at danger is found - set PERMISSION REQUESTED, and request clear signal
            // if signal has a route, set PERMISSION REQUESTED, and perform signal update
            reqSignal.OverridePermission = SignalPermission.Requested;

            TCPosition tempPos = new TCPosition();

            if (routeIndex == 0)
            {
                PresentPosition[0].CopyTo(ref tempPos);
            }
            else
            {
                PresentPosition[1].CopyTo(ref tempPos);
                tempPos.TCDirection = tempPos.TCDirection == 0 ? 1 : 0;
            }

            TrackCircuitPartialPathRoute newRouteR = CheckExplorerPath(routeIndex, tempPos, ValidRoute[routeIndex], true, ref EndAuthorityTypes[routeIndex],
                ref DistanceToEndNodeAuthorityM[routeIndex]);
            ValidRoute[routeIndex] = newRouteR;
            Simulator.SoundNotify = reqSignal.OverridePermission == SignalPermission.Granted ?
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
                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("No switch found"));
            }

            return (switchSet);
        }

        public bool ProcessRequestExplorerSetSwitch(int reqSwitchIndex)
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
                return (true);
            }

            return (false);
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
                TrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset,
                    (TrackDirection)PresentPosition[1].TCDirection, Length, false, true, false);
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
                int listIndex = PresentPosition[0].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[0], listIndex, routedForward);
                ClearDeadlocks();
            }

            ValidRoute[0] = null;
            LastReservedSection[0] = -1;

            if (ValidRoute[1] != null)
            {
                int listIndex = PresentPosition[1].RouteListIndex;
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

            PresentPosition[0].RouteListIndex = -1;
            PresentPosition[1].RouteListIndex = -1;
            PreviousPosition[0].RouteListIndex = -1;

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
            int firstSectionIndex = PresentPosition[0].RouteListIndex;
            int lastSectionIndex = ValidRoute[0].GetRouteIndex(thisSignal.TrackCircuitIndex, firstSectionIndex);

            // first, all signals in present section beyond position of train
            TrackCircuitSection thisSection = ValidRoute[0][firstSectionIndex].TrackCircuitSection;
            TrackDirection thisDirection = ValidRoute[0][firstSectionIndex].Direction;

            for (int isigtype = 0; isigtype < signalRef.OrtsSignalTypeCount; isigtype++)
            {
                TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[thisDirection][isigtype];
                foreach (TrackCircuitSignalItem thisItem in thisList)
                {
                    if (thisItem.SignalLocation > PresentPosition[0].TCOffset && !thisItem.Signal.SignalNormal())
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
                endListIndex = ValidRoute[0].GetRouteIndex(thisSectionIndex, PresentPosition[0].RouteListIndex);

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
                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("You cannot enter manual mode when autopiloted"));
            }
            else if (IsPathless && ControlMode != TrainControlMode.OutOfControl && ControlMode == TrainControlMode.Manual)
            {
                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("You cannot use this command for pathless trains"));
            }
            else if (ControlMode == TrainControlMode.Manual)
            {
                // check if train is back on path

                TrackCircuitPartialPathRoute lastRoute = TCRoute.TCRouteSubpaths[TCRoute.ActiveSubPath];
                int routeIndex = lastRoute.GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);

                if (routeIndex < 0)
                {
                    if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                        Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Train is not back on original route"));
                }
                else
                {
                    TrackDirection lastDirection = lastRoute[routeIndex].Direction;
                    TrackDirection presentDirection = (TrackDirection)PresentPosition[0].TCDirection;
                    if (lastDirection != presentDirection && Math.Abs(SpeedMpS) > 0.1f)
                    {
                        if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                            Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Original route is reverse from present direction, stop train before switching"));
                    }
                    else
                    {
                        ToggleFromManualMode(routeIndex);
                        Simulator.Confirmer.Confirm(CabControl.SignalMode, CabSetting.On);
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
                else if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Cannot change to Manual Mode while in Explorer Mode"));
            }
            else
            {
                ToggleToManualMode();
                Simulator.Confirmer.Confirm(CabControl.SignalMode, CabSetting.Off);
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
                int listIndex = PresentPosition[0].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[0], listIndex, routedForward);
                ClearDeadlocks();
            }

            ValidRoute[0] = null;
            LastReservedSection[0] = -1;

            if (ValidRoute[1] != null)
            {
                int listIndex = PresentPosition[1].RouteListIndex;
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

            PresentPosition[0].RouteListIndex = -1;
            PresentPosition[1].RouteListIndex = -1;
            PreviousPosition[0].RouteListIndex = -1;

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
                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Reversal required and rear of train not on required route"));
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
            PresentPosition[0].DistanceTravelledM = DistanceTravelledM;
            PresentPosition[1].DistanceTravelledM = DistanceTravelledM - Length;

            // set track occupation (using present route)
            // This procedure is also needed for clearing track occupation.
            UpdateSectionStateManual();

            // restore signal information
            PassedSignalSpeeds.Clear();
            InitializeSignals(true);

            // restore deadlock information

            CheckDeadlock(ValidRoute[0], Number);    // Check deadlock against all other trains

            // switch to AutoNode mode

            LastReservedSection[0] = PresentPosition[0].TCSectionIndex;
            LastReservedSection[1] = PresentPosition[1].TCSectionIndex;

            if (!Simulator.TimetableMode) AuxActionsContain.ResetAuxAction(this);
            SwitchToNodeControl(PresentPosition[0].TCSectionIndex);
            TCRoute.SetReversalOffset(Length, Simulator.TimetableMode);
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
                int listIndex = PresentPosition[0].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[0], listIndex, routedForward);
                ClearDeadlocks();
            }

            ValidRoute[0] = null;
            LastReservedSection[0] = -1;

            if (ValidRoute[1] != null)
            {
                int listIndex = PresentPosition[1].RouteListIndex;
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

            PresentPosition[0].RouteListIndex = -1;
            PresentPosition[1].RouteListIndex = -1;
            PreviousPosition[0].RouteListIndex = -1;

            UpdateExplorerMode(-1);
        }

        //================================================================================================//
        //
        // Check if reversal is required
        //

        public bool CheckReversal(TrackCircuitPartialPathRoute reqRoute, ref bool reversal)
        {
            bool valid = true;

            int presentRouteIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
            int reqRouteIndex = reqRoute.GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
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
                int rearRouteIndex = reqRoute.GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
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
                if (IncorporatedTrainNo >= 0) IncrementSubpath(Simulator.TrainDictionary[IncorporatedTrainNo]);
            }

            // reset distance travelled

            DistanceTravelledM = 0.0f;

            // check if end of train on original route
            // copy sections from earliest start point (front or rear)

            int rearIndex = oldRoute.GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
            int startIndex = rearIndex >= 0 ? Math.Min(rearIndex, frontIndex) : frontIndex;

            for (int iindex = startIndex; iindex < oldRoute.Count; iindex++)
            {
                newRoute.Add(oldRoute[iindex]);
            }

            // if rear not on route, build route under train and add sections

            if (rearIndex < 0)
            {

                TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset,
                            (TrackDirection)PresentPosition[1].TCDirection, Length, true, true, false);

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

            rearIndex = ValidRoute[0].GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
            PresentPosition[1].RouteListIndex = rearIndex;

            // get index of front of train

            frontIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
            PresentPosition[0].RouteListIndex = frontIndex;

            PresentPosition[0].CopyTo(ref PreviousPosition[0]);

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
                if (direction == Direction.Forward)
                {
                    RequestExplorerSignalPermission(ref ValidRoute[0], 0);
                }
                else
                {
                    RequestExplorerSignalPermission(ref ValidRoute[1], 1);
                }
            }
            else
            {
                if (direction != Direction.Forward)
                {
                    if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                        Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Cannot clear signal behind train while in AUTO mode"));
                    Simulator.SoundNotify = TrainEvent.PermissionDenied;
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
                    int reqRouteIndex = (int)direction;

                    if (NextSignalObject[reqRouteIndex] != null &&
                        NextSignalObject[reqRouteIndex].SignalLR(SignalFunction.Normal) != SignalAspectState.Stop)
                    {
                        int routeIndex = ValidRoute[reqRouteIndex].GetRouteIndex(NextSignalObject[reqRouteIndex].TrackCircuitNextIndex, PresentPosition[reqRouteIndex].RouteListIndex);
                        signalRef.BreakDownRouteList(ValidRoute[reqRouteIndex], routeIndex, routedForward);
                        ValidRoute[reqRouteIndex].RemoveRange(routeIndex, ValidRoute[reqRouteIndex].Count - routeIndex);

                        NextSignalObject[reqRouteIndex].ResetSignal(true);
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

            int endListIndex = ValidRoute[0].GetRouteIndex(sectionIndex, PresentPosition[0].RouteListIndex);
            if (endListIndex < 0)
                endListIndex = ValidRoute[0].GetRouteIndex(sectionIndex, 0);

            if (endListIndex >= 0 && endListIndex < PresentPosition[0].RouteListIndex) // index before present so we must have passed object
            {
                return (-1.0f);
            }

            if (endListIndex == PresentPosition[0].RouteListIndex && endOffset < PresentPosition[0].TCOffset) // just passed
            {
                return (-1.0f);
            }

            // section is not on route

            if (endListIndex < 0)
            {
                return (-1.0f);
            }

            int thisSectionIndex = PresentPosition[0].TCSectionIndex;
            TrackDirection direction = (TrackDirection)PresentPosition[0].TCDirection;
            float startOffset = PresentPosition[0].TCOffset;
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];

            return (TrackCircuitSection.GetDistanceBetweenObjects(thisSectionIndex, startOffset, direction, sectionIndex, endOffset));
        }

        //================================================================================================//
        /// <summary>
        /// Switch train to Out-of-Control
        /// Set mode and apply emergency brake
        /// </summary>

        public void SetTrainOutOfControl(OutOfControlReason reason)
        {

            if (ControlMode == TrainControlMode.OutOfControl) // allready out of control, so exit
            {
                return;
            }

            // clear all reserved sections etc. - both directions
            if (ControlMode == TrainControlMode.AutoSignal)
            {
                if (NextSignalObject[0] != null && NextSignalObject[0].EnabledTrain == routedForward)
                {
                    var routeIndexBeforeSignal = NextSignalObject[0].TrainRouteIndex - 1;
                    NextSignalObject[0].ResetSignal(true);
                    if (routeIndexBeforeSignal >= 0)
                        signalRef.BreakDownRoute(ValidRoute[0][routeIndexBeforeSignal].TrackCircuitSection.Index, routedForward);
                }
                if (NextSignalObject[1] != null && NextSignalObject[1].EnabledTrain == routedBackward)
                {
                    NextSignalObject[1].ResetSignal(true);
                }
            }
            else if (ControlMode == TrainControlMode.AutoNode)
            {
                signalRef.BreakDownRoute(LastReservedSection[0], routedForward);
            }

            // TODO : clear routes for MANUAL
            if (!MPManager.IsMultiPlayer() || Simulator.TimetableMode || reason != OutOfControlReason.OutOfPath || IsActualPlayerTrain)
            {

                // set control state and issue warning

                if (ControlMode != TrainControlMode.Explorer)
                    ControlMode = TrainControlMode.OutOfControl;

                var report = string.Format("Train {0} is out of control and will be stopped. Reason : ", Number.ToString());

                OutOfControlReason = reason;

                switch (reason)
                {
                    case (OutOfControlReason.PassedAtDanger):
                        report = String.Concat(report, " train passed signal at Danger");
                        break;
                    case (OutOfControlReason.RearPassedAtDanger):
                        report = String.Concat(report, " train passed signal at Danger at rear of train");
                        break;
                    case (OutOfControlReason.OutOfAuthority):
                        report = String.Concat(report, " train passed limit of authority");
                        break;
                    case (OutOfControlReason.OutOfPath):
                        report = String.Concat(report, " train has ran off its allocated path");
                        break;
                    case (OutOfControlReason.SlippedIntoPath):
                        report = String.Concat(report, " train slipped back into path of another train");
                        break;
                    case (OutOfControlReason.SlippedToEndOfTrack):
                        report = String.Concat(report, " train slipped of the end of track");
                        break;
                    case (OutOfControlReason.OutOfTrack):
                        report = String.Concat(report, " train has moved off the track");
                        break;
                }

                if (LeadLocomotive != null)
                    ((MSTSLocomotive)LeadLocomotive).SetEmergency(true);
            }
            // the AI train is now out of path. Instead of killing him, we give him a chance on a new path
            else
            {
                GenerateValidRoute(PresentPosition[0].RouteListIndex, PresentPosition[0].TCSectionIndex);
                // switch to NODE mode
                if (ControlMode == TrainControlMode.AutoSignal)
                {
                    SwitchToNodeControl(PresentPosition[0].TCSectionIndex);
                }
                // reset actions to recalculate distances
                if (TrainType == TrainType.Ai || TrainType == TrainType.AiPlayerHosting) ((AITrain)this).ResetActions(true);
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

        //================================================================================================//
        /// <summary>
        /// Resets ValidRoute after some event like a switch moved
        /// </summary>

        public void ResetValidRoute()
        {
            // clear all reserved sections etc. - both directions
            if (ControlMode == TrainControlMode.AutoSignal)
            {
                if (NextSignalObject[0] != null && NextSignalObject[0].EnabledTrain == routedForward)
                {
                    int routeIndexBeforeSignal = NextSignalObject[0].TrainRouteIndex - 1;
                    NextSignalObject[0].ResetSignal(true);
                    if (routeIndexBeforeSignal >= 0)
                        signalRef.BreakDownRoute(ValidRoute[0][routeIndexBeforeSignal].TrackCircuitSection.Index, routedForward);
                }
                if (NextSignalObject[1] != null && NextSignalObject[1].EnabledTrain == routedBackward)
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
            if (PresentPosition[0].RouteListIndex > 0)
                // clean case, train is in route and switch has been forced in front of it
                tempSections = SignalEnvironment.ScanRoute(this, forcedTCSectionIndex, 0, (TrackDirection)ValidRoute[0][forcedRouteSectionIndex].Direction,
                        true, 0, true, true,
                        false, false, true, false, false, false, false, IsFreight, false, true);
            else
                // dirty case, train is out of route and has already passed forced switch
                tempSections = SignalEnvironment.ScanRoute(this, PresentPosition[0].TCSectionIndex, PresentPosition[0].TCOffset,
                    (TrackDirection)PresentPosition[0].TCDirection, true, 0, true, true,
                    false, false, true, false, false, false, false, IsFreight, false, true);

            TrackCircuitPartialPathRoute newRoute = new TrackCircuitPartialPathRoute();
            // Copy part of route already run
            if (PresentPosition[0].RouteListIndex > 0)
            {
                for (int routeListIndex = 0; routeListIndex < forcedRouteSectionIndex; routeListIndex++) newRoute.Add(ValidRoute[0][routeListIndex]);
            }
            else if (PresentPosition[0].RouteListIndex < 0)
            {
                for (int routeListIndex = 0; routeListIndex <= PreviousPosition[0].RouteListIndex + 1; routeListIndex++) newRoute.Add(ValidRoute[0][routeListIndex]); // maybe + 1 is wrong?
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
            if (PresentPosition[0].RouteListIndex == -1)
                PresentPosition[0].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, PreviousPosition[0].RouteListIndex);

            // reset signal information

            SignalObjectItems.Clear();
            NextSignalObject[0] = null;
            // create new list
            InitializeSignals(true);
            LastReservedSection[0] = PresentPosition[0].TCSectionIndex;
            CheckDeadlock(ValidRoute[0], Number);    // Check deadlock against all other trains
        }

        //================================================================================================//
        /// <summary>
        /// Perform actions linked to distance travelled
        /// </summary>

        public virtual void PerformActions(List<DistanceTravelledItem> nowActions)
        {
            foreach (var thisAction in nowActions)
            {
                if (thisAction is ClearSectionItem)
                {
                    ClearOccupiedSection(thisAction as ClearSectionItem);
                }
                else if (thisAction is ActivateSpeedLimit)
                {
                    SetPendingSpeedLimit(thisAction as ActivateSpeedLimit);
                }
                else if (thisAction is AuxActionItem)
                {
                    int presentTime = Convert.ToInt32(Math.Floor(Simulator.ClockTime));
                    ((AuxActionItem)thisAction).ProcessAction(this, presentTime);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Clear section
        /// </summary>

        public void ClearOccupiedSection(ClearSectionItem sectionInfo)
        {
            int thisSectionIndex = sectionInfo.TrackSectionIndex;
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];

            thisSection.ClearOccupied(this, true);
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
                allowedMaxSpeedSignalMpS = Simulator.TimetableMode ? speedInfo.MaxSpeedMpSSignal : allowedAbsoluteMaxSpeedSignalMpS;
                AllowedMaxSpeedMpS = Math.Min(speedInfo.MaxSpeedMpSSignal, Math.Min(allowedMaxSpeedLimitMpS, allowedMaxTempSpeedLimitMpS));
            }
            if (speedInfo.MaxSpeedMpSLimit > 0)
            {
                allowedMaxSpeedLimitMpS = Simulator.TimetableMode ? speedInfo.MaxSpeedMpSLimit : allowedAbsoluteMaxSpeedLimitMpS;
                if (Simulator.TimetableMode)
                    AllowedMaxSpeedMpS = speedInfo.MaxSpeedMpSLimit;
                else
                    AllowedMaxSpeedMpS = Math.Min(speedInfo.MaxSpeedMpSLimit, Math.Min(allowedMaxSpeedSignalMpS, allowedMaxTempSpeedLimitMpS));
            }
            if (speedInfo.MaxTempSpeedMpSLimit > 0 && !Simulator.TimetableMode)
            {
                allowedMaxTempSpeedLimitMpS = allowedAbsoluteMaxTempSpeedLimitMpS;
                AllowedMaxSpeedMpS = Math.Min(speedInfo.MaxTempSpeedMpSLimit, Math.Min(allowedMaxSpeedSignalMpS, allowedMaxSpeedLimitMpS));
            }
            if (IsActualPlayerTrain && AllowedMaxSpeedMpS > prevMaxSpeedMpS)
            {
                Simulator.OnAllowedSpeedRaised(this);
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

            if (Simulator.PlayerLocomotive != null && Simulator.PlayerLocomotive.Train == this)
            {
                var report = Simulator.Catalog.GetString("Train stopped due to problems with other train: train {0} , reason: {1}", otherTrainNumber, reason);

                if (Simulator.Confirmer != null) // As Confirmer may not be created until after a restore.
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, report);

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
            Simulator.Trains.Remove(this);
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

            int presentIndex = PresentPosition[1].RouteListIndex;

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

            TCPosition oldPresentPosition = new TCPosition();
            PresentPosition[0].CopyTo(ref oldPresentPosition);
            TCPosition oldRearPosition = new TCPosition();
            PresentPosition[1].CopyTo(ref oldRearPosition);

            PresentPosition[0] = new TCPosition();
            PresentPosition[1] = new TCPosition();

            // create new TCPositions

            TrackNode tn = FrontTDBTraveller.TN;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            int direction = (int)FrontTDBTraveller.Direction;

            PresentPosition[0].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PresentPosition[0].CopyTo(ref PreviousPosition[0]);

            tn = RearTDBTraveller.TN;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (int)RearTDBTraveller.Direction;

            PresentPosition[1].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);

            PresentPosition[0].DistanceTravelledM = DistanceTravelledM;
            PresentPosition[1].DistanceTravelledM = oldRearPosition.DistanceTravelledM;

            // use difference in position to update existing DistanceTravelled

            float deltaoffset = 0.0f;

            if (couple_to_front)
            {
                float offset_old = oldPresentPosition.TCOffset;
                float offset_new = PresentPosition[0].TCOffset;

                if (oldPresentPosition.TCSectionIndex == PresentPosition[0].TCSectionIndex)
                {
                    deltaoffset = offset_new - offset_old;
                }
                else
                {
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[oldPresentPosition.TCSectionIndex];
                    deltaoffset = thisSection.Length - offset_old;
                    deltaoffset += offset_new;

                    for (int iIndex = oldPresentPosition.RouteListIndex + 1; iIndex < PresentPosition[0].RouteListIndex; iIndex++)
                    {
                        thisSection = ValidRoute[0][iIndex].TrackCircuitSection;
                        deltaoffset += thisSection.Length;
                    }
                }
                PresentPosition[0].DistanceTravelledM += deltaoffset;
                DistanceTravelledM += deltaoffset;
            }
            else
            {
                float offset_old = oldRearPosition.TCOffset;
                float offset_new = PresentPosition[1].TCOffset;

                if (oldRearPosition.TCSectionIndex == PresentPosition[1].TCSectionIndex)
                {
                    deltaoffset = offset_old - offset_new;
                }
                else
                {
                    deltaoffset = offset_old;
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[1].TCSectionIndex];
                    deltaoffset += (thisSection.Length - offset_new);

                    for (int iIndex = oldRearPosition.RouteListIndex - 1; iIndex > PresentPosition[1].RouteListIndex; iIndex--)
                    {
                        thisSection = ValidRoute[0][iIndex].TrackCircuitSection;
                        deltaoffset += thisSection.Length;
                    }
                }
                PresentPosition[1].DistanceTravelledM -= deltaoffset;
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

                if (PresentPosition[0].TCSectionIndex == thisSection.Index)
                {
                    distanceToClear += Length - PresentPosition[0].TCOffset;
                }
                else if (PresentPosition[1].TCSectionIndex == thisSection.Index)
                {
                    distanceToClear -= PresentPosition[1].TCOffset;
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
            LastReservedSection[0] = PresentPosition[0].TCSectionIndex;
            LastReservedSection[1] = PresentPosition[0].TCSectionIndex;

            InitializeSignals(true);

            if (TCRoute != null && (ControlMode == TrainControlMode.AutoSignal || ControlMode == TrainControlMode.AutoNode))
            {
                PresentPosition[0].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
                PresentPosition[1].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);

                SwitchToNodeControl(PresentPosition[0].TCSectionIndex);
                CheckDeadlock(ValidRoute[0], Number);
                TCRoute.SetReversalOffset(Length, Simulator.TimetableMode);
            }
            else if (ControlMode == TrainControlMode.Manual)
            {
                // set track occupation

                UpdateSectionStateManual();

                // reset routes and check sections either end of train

                PresentPosition[0].RouteListIndex = -1;
                PresentPosition[1].RouteListIndex = -1;
                PreviousPosition[0].RouteListIndex = -1;

                UpdateManualMode(-1);
            }
            else if (ControlMode == TrainControlMode.Explorer)
            {
                // set track occupation

                UpdateSectionStateExplorer();

                // reset routes and check sections either end of train

                PresentPosition[0].RouteListIndex = -1;
                PresentPosition[1].RouteListIndex = -1;
                PreviousPosition[0].RouteListIndex = -1;

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
            if (TrainRoute != null) TrainRoute.Clear();
            TrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset,
                (TrackDirection)PresentPosition[1].TCDirection, Length, false, true, false);

            foreach (TrackCircuitRouteElement thisElement in TrainRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                thisSection.Reserve(routedForward, TrainRoute);
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
            int direction = (int)FrontTDBTraveller.Direction;

            PresentPosition[0].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);
            PresentPosition[0].CopyTo(ref PreviousPosition[0]);

            tn = RearTDBTraveller.TN;
            offset = RearTDBTraveller.TrackNodeOffset;
            direction = (int)RearTDBTraveller.Direction;

            PresentPosition[1].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);

            PresentPosition[0].DistanceTravelledM = DistanceTravelledM;
            PresentPosition[1].DistanceTravelledM = DistanceTravelledM - Length;

            // Set track sections to occupied

            OccupiedTrack.Clear();

            // build route of sections now occupied
            OccupiedTrack.Clear();
            if (TrainRoute != null) TrainRoute.Clear();
            TrainRoute = SignalEnvironment.BuildTempRoute(this, PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset,
                (TrackDirection)PresentPosition[1].TCDirection, Length, false, true, false);

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

                thisSection = TrackCircuitSection.TrackCircuitList[PresentPosition[1].TCSectionIndex];
                offset = PresentPosition[1].TCOffset;

                ValidRoute[0] = SignalEnvironment.BuildTempRoute(this, thisSection.Index, PresentPosition[1].TCOffset,
                            (TrackDirection)PresentPosition[1].TCDirection, Length, true, true, false);

                foreach (TrackCircuitRouteElement thisElement in TrainRoute)
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
                        PresentPosition[0].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
                        PresentPosition[1].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);
                        if (PresentPosition[0].RouteListIndex < 0 || PresentPosition[1].RouteListIndex < 0)
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
                            if (PresentPosition[0].TCDirection != (int)ValidRoute[0][PresentPosition[0].RouteListIndex].Direction)
                            // Train must be reverted
                            {
                                ReverseFormation(false);
                                var tempTCPosition = PresentPosition[0];
                                PresentPosition[0] = PresentPosition[1];
                                PresentPosition[1] = tempTCPosition;
                            }
                            break;
                        }
                    }
                }

                foreach (TrackCircuitRouteElement thisElement in TrainRoute)
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
            LastReservedSection[0] = PresentPosition[0].TCSectionIndex;
            LastReservedSection[1] = PresentPosition[1].TCSectionIndex;


            InitializeSignals(true);

            if (ControlMode == TrainControlMode.AutoSignal || ControlMode == TrainControlMode.AutoNode)
            {
                PresentPosition[0].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
                PresentPosition[1].RouteListIndex = ValidRoute[0].GetRouteIndex(PresentPosition[1].TCSectionIndex, 0);

                CheckDeadlock(ValidRoute[0], Number);
                SwitchToNodeControl(PresentPosition[0].TCSectionIndex);
                TCRoute.SetReversalOffset(Length, Simulator.TimetableMode);
            }
            else if (ControlMode == TrainControlMode.Manual)
            {
                // set track occupation

                UpdateSectionStateManual();

                // reset routes and check sections either end of train

                PresentPosition[0].RouteListIndex = -1;
                PresentPosition[1].RouteListIndex = -1;
                PreviousPosition[0].RouteListIndex = -1;

                UpdateManualMode(-1);
            }
            else if (ControlMode == TrainControlMode.Explorer)
            {
                // set track occupation

                UpdateSectionStateExplorer();

                // reset routes and check sections either end of train

                PresentPosition[0].RouteListIndex = -1;
                PresentPosition[1].RouteListIndex = -1;
                PreviousPosition[0].RouteListIndex = -1;

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
        // Check on deadlock
        //

        internal void CheckDeadlock(TrackCircuitPartialPathRoute thisRoute, int thisNumber)
        {
            if (signalRef.UseLocationPassingPaths)
            {
                CheckDeadlock_locationBased(thisRoute, thisNumber);  // new location based logic
            }
            else
            {
                CheckDeadlock_pathBased(thisRoute, thisNumber);      // old path based logic
            }
        }

        //================================================================================================//
        //
        // Check on deadlock - old style path based logic
        //

        internal void CheckDeadlock_pathBased(TrackCircuitPartialPathRoute thisRoute, int thisNumber)
        {
            // clear existing deadlock info

            ClearDeadlocks();

            // build new deadlock info

            foreach (Train otherTrain in Simulator.Trains)
            {
                if (otherTrain.Number != thisNumber && otherTrain.TrainType != TrainType.Static)
                {
                    TrackCircuitPartialPathRoute otherRoute = otherTrain.ValidRoute[0];
                    Dictionary<int, int> otherRouteDict = otherRoute.ConvertRoute();

                    for (int iElement = 0; iElement < thisRoute.Count; iElement++)
                    {
                        TrackCircuitRouteElement thisElement = thisRoute[iElement];
                        TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                        TrackDirection thisSectionDirection = thisElement.Direction;

                        if (thisSection.CircuitType != TrackCircuitType.Crossover)
                        {
                            if (otherRouteDict.ContainsKey(thisSection.Index))
                            {
                                TrackDirection otherTrainDirection = (TrackDirection)otherRouteDict[thisSection.Index];
                                //<CSComment> Right part of OR clause refers to initial placement with trains back-to-back and running away one from the other</CSComment>
                                if (otherTrainDirection == thisSectionDirection ||
                                    (PresentPosition[1].TCSectionIndex == otherTrain.PresentPosition[1].TCSectionIndex && thisSection.Index == PresentPosition[1].TCSectionIndex &&
                                    PresentPosition[1].TCOffset + otherTrain.PresentPosition[1].TCOffset - 1 > thisSection.Length))
                                {
                                    iElement = EndCommonSection(iElement, thisRoute, otherRoute);
                                }
                                else
                                {
                                    int[] endDeadlock = SetDeadlock_pathBased(iElement, thisRoute, otherRoute, otherTrain);
                                    // use end of alternative path if set - if so, compensate for iElement++
                                    iElement = endDeadlock[1] > 0 ? --endDeadlock[1] : endDeadlock[0];
                                }
                            }
                        }
                    }
                }
            }
#if DEBUG_DEADLOCK
            File.AppendAllText(@"C:\Temp\deadlock.txt", "\n================= Check Deadlock \nTrain : " + Number.ToString() + "\n");

            foreach (KeyValuePair<int, List<Dictionary<int, int>>> thisDeadlock in DeadlockInfo)
            {
                File.AppendAllText(@"C:\Temp\deadlock.txt", "Section : " + thisDeadlock.Key.ToString() + "\n");
                foreach (Dictionary<int, int> actDeadlocks in thisDeadlock.Value)
                {
                    foreach (KeyValuePair<int, int> actDeadlockInfo in actDeadlocks)
                    {
                        File.AppendAllText(@"C:\Temp\deadlock.txt", "  Other Train : " + actDeadlockInfo.Key.ToString() +
                            " - end Sector : " + actDeadlockInfo.Value.ToString() + "\n");
                    }
                }
                File.AppendAllText(@"C:\Temp\deadlock.txt", "\n");
            }
#endif
        }

        //================================================================================================//
        //
        // Obtain deadlock details - old style path based logic
        //

        private int[] SetDeadlock_pathBased(int thisIndex, TrackCircuitPartialPathRoute thisRoute, TrackCircuitPartialPathRoute otherRoute, Train otherTrain)
        {
            int[] returnValue = new int[2];
            returnValue[1] = -1;  // set to no alternative path used

            TrackCircuitRouteElement firstElement = thisRoute[thisIndex];
            int firstSectionIndex = firstElement.TrackCircuitSection.Index;
            bool allreadyActive = false;

            int thisTrainSection = firstSectionIndex;
            int otherTrainSection = firstSectionIndex;

            int thisTrainIndex = thisIndex;
            int otherTrainIndex = otherRoute.GetRouteIndex(firstSectionIndex, 0);

            int thisFirstIndex = thisTrainIndex;
            int otherFirstIndex = otherTrainIndex;

            TrackCircuitRouteElement thisTrainElement = thisRoute[thisTrainIndex];
            TrackCircuitRouteElement otherTrainElement = otherRoute[otherTrainIndex];

            // loop while not at end of route for either train and sections are equal
            // loop is also exited when alternative path is found for either train
            for (int iLoop = 0; ((thisFirstIndex + iLoop) <= (thisRoute.Count - 1)) && ((otherFirstIndex - iLoop)) >= 0 && (thisTrainSection == otherTrainSection); iLoop++)
            {
                thisTrainIndex = thisFirstIndex + iLoop;
                otherTrainIndex = otherFirstIndex - iLoop;

                thisTrainElement = thisRoute[thisTrainIndex];
                otherTrainElement = otherRoute[otherTrainIndex];
                thisTrainSection = thisTrainElement.TrackCircuitSection.Index;
                otherTrainSection = otherTrainElement.TrackCircuitSection.Index;

                if (thisTrainElement.StartAlternativePath != null)
                {
                    int endAlternativeSection = thisTrainElement.StartAlternativePath.TrackCircuitSection.Index;
                    returnValue[1] = thisRoute.GetRouteIndex(endAlternativeSection, thisIndex);
                    break;
                }

                if (otherTrainElement.EndAlternativePath != null)
                {
                    int endAlternativeSection = otherTrainElement.EndAlternativePath.TrackCircuitSection.Index;
                    returnValue[1] = thisRoute.GetRouteIndex(endAlternativeSection, thisIndex);
                    break;
                }

                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisTrainSection];

                if (thisSection.IsSet(otherTrain, true))
                {
                    allreadyActive = true;
                }
            }

            // get sections on which loop ended
            thisTrainElement = thisRoute[thisTrainIndex];
            thisTrainSection = thisTrainElement.TrackCircuitSection.Index;

            otherTrainElement = otherRoute[otherTrainIndex];
            otherTrainSection = otherTrainElement.TrackCircuitSection.Index;

            // if last sections are still equal - end of route reached for one of the trains
            // otherwise, last common sections was previous sections for this train
            int lastSectionIndex = (thisTrainSection == otherTrainSection) ? thisTrainSection :
                thisRoute[thisTrainIndex - 1].TrackCircuitSection.Index;

            // if section is not a junction, check if either route not ended, if so continue up to next junction
            TrackCircuitSection lastSection = TrackCircuitSection.TrackCircuitList[lastSectionIndex];
            if (lastSection.CircuitType != TrackCircuitType.Junction)
            {
                bool endSectionFound = false;
                if (thisTrainIndex < (thisRoute.Count - 1))
                {
                    for (int iIndex = thisTrainIndex + 1; iIndex < thisRoute.Count - 1 && !endSectionFound; iIndex++)
                    {
                        lastSection = thisRoute[iIndex].TrackCircuitSection;
                        endSectionFound = lastSection.CircuitType == TrackCircuitType.Junction;
                    }
                }

                else if (otherTrainIndex > 0)
                {
                    for (int iIndex = otherTrainIndex - 1; iIndex >= 0 && !endSectionFound; iIndex--)
                    {
                        lastSection = otherRoute[iIndex].TrackCircuitSection;
                        endSectionFound = lastSection.CircuitType == TrackCircuitType.Junction;
                        if (lastSection.IsSet(otherTrain, true))
                        {
                            allreadyActive = true;
                        }
                    }
                }
                lastSectionIndex = lastSection.Index;
            }

            // set deadlock info for both trains

            SetDeadlockInfo(firstSectionIndex, lastSectionIndex, otherTrain.Number);
            otherTrain.SetDeadlockInfo(lastSectionIndex, firstSectionIndex, Number);

            if (allreadyActive)
            {
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[lastSectionIndex];
                thisSection.SetDeadlockTrap(otherTrain, otherTrain.DeadlockInfo[lastSectionIndex]);
            }

            returnValue[0] = thisRoute.GetRouteIndex(lastSectionIndex, thisIndex);
            if (returnValue[0] < 0)
                returnValue[0] = thisTrainIndex;
            return (returnValue);
        }

        //================================================================================================//
        //
        // Check on deadlock - new style location based logic
        //

        internal void CheckDeadlock_locationBased(TrackCircuitPartialPathRoute thisRoute, int thisNumber)
        {
            // clear existing deadlock info

            ClearDeadlocks();

            // build new deadlock info

            foreach (Train otherTrain in Simulator.Trains)
            {
                bool validTrain = true;

                // check if not AI_Static

                if (otherTrain.GetAIMovementState() == AITrain.AI_MOVEMENT_STATE.AI_STATIC)
                {
                    validTrain = false;
                }

                if (otherTrain.Number != thisNumber && otherTrain.TrainType != TrainType.Static && validTrain)
                {
                    TrackCircuitPartialPathRoute otherRoute = otherTrain.ValidRoute[0];
                    Dictionary<int, int> otherRouteDict = otherRoute.ConvertRoute();

                    for (int iElement = 0; iElement < thisRoute.Count; iElement++)
                    {
                        TrackCircuitRouteElement thisElement = thisRoute[iElement];
                        TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                        TrackDirection thisSectionDirection = thisElement.Direction;

                        if (thisSection.CircuitType != TrackCircuitType.Crossover)
                        {
                            if (otherRouteDict.ContainsKey(thisSection.Index))
                            {
                                TrackDirection otherTrainDirection = (TrackDirection)otherRouteDict[thisSection.Index];
                                //<CSComment> Right part of OR clause refers to initial placement with trains back-to-back and running away one from the other</CSComment>
                                if (otherTrainDirection == thisSectionDirection ||
                                    (PresentPosition[1].TCSectionIndex == otherTrain.PresentPosition[1].TCSectionIndex && thisSection.Index == PresentPosition[1].TCSectionIndex &&
                                    PresentPosition[1].TCOffset + otherTrain.PresentPosition[1].TCOffset - 1 > thisSection.Length))
                                {
                                    iElement = EndCommonSection(iElement, thisRoute, otherRoute);

                                }
                                else
                                {
                                    if (CheckRealDeadlock_locationBased(thisRoute, otherRoute, ref iElement))
                                    {
                                        int[] endDeadlock = SetDeadlock_locationBased(iElement, thisRoute, otherRoute, otherTrain);
                                        // use end of alternative path if set
                                        iElement = endDeadlock[1] > 0 ? --endDeadlock[1] : endDeadlock[0];
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        //
        // Obtain deadlock details - new style location based logic
        //

        private int[] SetDeadlock_locationBased(int thisIndex, TrackCircuitPartialPathRoute thisRoute, TrackCircuitPartialPathRoute otherRoute, Train otherTrain)
        {
            int[] returnValue = new int[2];
            returnValue[1] = -1;  // set to no alternative path used

            TrackCircuitRouteElement firstElement = thisRoute[thisIndex];
            int firstSectionIndex = firstElement.TrackCircuitSection.Index;
            bool allreadyActive = false;

            int thisTrainSectionIndex = firstSectionIndex;
            int otherTrainSectionIndex = firstSectionIndex;

            // double index variables required as last valid index must be known when exiting loop
            int thisTrainIndex = thisIndex;
            int thisTrainNextIndex = thisTrainIndex;
            int otherTrainIndex = otherRoute.GetRouteIndex(firstSectionIndex, 0);
            int otherTrainNextIndex = otherTrainIndex;

            int thisFirstIndex = thisTrainIndex;
            int otherFirstIndex = otherTrainIndex;

            TrackCircuitRouteElement thisTrainElement = thisRoute[thisTrainIndex];
            TrackCircuitRouteElement otherTrainElement = otherRoute[otherTrainIndex];

            bool validPassLocation = false;
            int endSectionRouteIndex = -1;

            bool endOfLoop = false;

            // loop while not at end of route for either train and sections are equal
            // loop is also exited when alternative path is found for either train
            while (!endOfLoop)
            {
                thisTrainIndex = thisTrainNextIndex;
                thisTrainElement = thisRoute[thisTrainIndex];
                otherTrainIndex = otherTrainNextIndex;
                thisTrainSectionIndex = thisTrainElement.TrackCircuitSection.Index;

                otherTrainElement = otherRoute[otherTrainIndex];
                otherTrainSectionIndex = otherTrainElement.TrackCircuitSection.Index;

                TrackCircuitSection thisSection = otherTrainElement.TrackCircuitSection;

                // if sections not equal : test length of next not-common section, if long enough then exit loop
                if (thisTrainSectionIndex != otherTrainSectionIndex)
                {
                    int nextThisRouteIndex = thisTrainIndex;
                    TrackCircuitSection passLoopSection = ValidRoute[0][nextThisRouteIndex].TrackCircuitSection;
                    int nextOtherRouteIndex = otherRoute.GetRouteIndex(passLoopSection.Index, otherTrainIndex);

                    float passLength = passLoopSection.Length;
                    bool endOfPassLoop = false;

                    while (!endOfPassLoop)
                    {
                        // loop is longer as at least one of the trains so is valid
                        if (passLength > Length || passLength > otherTrain.Length)
                        {
                            endOfPassLoop = true;
                            endOfLoop = true;
                        }

                        // get next section
                        else if (nextThisRouteIndex < ValidRoute[0].Count - 2)
                        {
                            nextThisRouteIndex++;
                            passLoopSection = ValidRoute[0][nextThisRouteIndex].TrackCircuitSection;
                            nextOtherRouteIndex = otherRoute.GetRouteIndexBackward(passLoopSection.Index, otherTrainIndex);

                            // new common section after too short loop - not a valid deadlock point
                            if (nextOtherRouteIndex >= 0)
                            {
                                endOfPassLoop = true;
                                thisTrainNextIndex = nextThisRouteIndex;
                                otherTrainNextIndex = nextOtherRouteIndex;
                            }
                            else
                            {
                                passLength += passLoopSection.Length;
                            }
                        }

                        // end of route
                        else
                        {
                            endOfPassLoop = true;
                            endOfLoop = true;
                        }
                    }
                }

                // if section is a deadlock boundary, check available paths for both trains

                else
                {

                    List<int> thisTrainAllocatedPaths = new List<int>();
                    List<int> otherTrainAllocatedPaths = new List<int>();

                    bool gotoNextSection = true;

                    if (thisSection.DeadlockReference >= 0 && thisTrainElement.FacingPoint) // test for facing points only
                    {
                        bool thisTrainFits = false;
                        bool otherTrainFits = false;

                        int endSectionIndex = -1;

                        validPassLocation = true;

                        // get allocated paths for this train
                        DeadlockInfo thisDeadlockInfo = signalRef.DeadlockInfoList[thisSection.DeadlockReference];

                        // get allocated paths for this train - if none yet set, create references
                        int thisTrainReferenceIndex = thisDeadlockInfo.GetTrainAndSubpathIndex(Number, TCRoute.ActiveSubPath);
                        if (!thisDeadlockInfo.TrainReferences.ContainsKey(thisTrainReferenceIndex))
                        {
                            thisDeadlockInfo.SetTrainDetails(Number, TCRoute.ActiveSubPath, Length, ValidRoute[0], thisTrainIndex);
                        }

                        // if valid path for this train
                        if (thisDeadlockInfo.TrainReferences.ContainsKey(thisTrainReferenceIndex))
                        {
                            thisTrainAllocatedPaths = thisDeadlockInfo.TrainReferences[thisDeadlockInfo.GetTrainAndSubpathIndex(Number, TCRoute.ActiveSubPath)];

                            // if paths available, get end section and check train against shortest path
                            if (thisTrainAllocatedPaths.Count > 0)
                            {
                                endSectionIndex = thisDeadlockInfo.AvailablePathList[thisTrainAllocatedPaths[0]].EndSectionIndex;
                                endSectionRouteIndex = thisRoute.GetRouteIndex(endSectionIndex, thisTrainIndex);
                                Dictionary<int, bool> thisTrainFitList = thisDeadlockInfo.TrainLengthFit[thisDeadlockInfo.GetTrainAndSubpathIndex(Number, TCRoute.ActiveSubPath)];
                                foreach (int iPath in thisTrainAllocatedPaths)
                                {
                                    if (thisTrainFitList[iPath])
                                    {
                                        thisTrainFits = true;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            validPassLocation = false;
                        }

                        // get allocated paths for other train - if none yet set, create references
                        int otherTrainReferenceIndex = thisDeadlockInfo.GetTrainAndSubpathIndex(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath);
                        if (!thisDeadlockInfo.TrainReferences.ContainsKey(otherTrainReferenceIndex))
                        {
                            int otherTrainElementIndex = otherTrain.ValidRoute[0].GetRouteIndexBackward(endSectionIndex, otherFirstIndex);
                            if (otherTrainElementIndex < 0) // train joins deadlock area on different node
                            {
                                validPassLocation = false;
                                thisDeadlockInfo.RemoveTrainAndSubpathIndex(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath); // remove index as train has no valid path
                            }
                            else
                            {
                                thisDeadlockInfo.SetTrainDetails(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath, otherTrain.Length,
                                    otherTrain.ValidRoute[0], otherTrainElementIndex);
                            }
                        }

                        // if valid path for other train
                        if (validPassLocation && thisDeadlockInfo.TrainReferences.ContainsKey(otherTrainReferenceIndex))
                        {
                            otherTrainAllocatedPaths =
                                thisDeadlockInfo.TrainReferences[thisDeadlockInfo.GetTrainAndSubpathIndex(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath)];

                            // if paths available, get end section (if not yet set) and check train against shortest path
                            if (otherTrainAllocatedPaths.Count > 0)
                            {
                                if (endSectionRouteIndex < 0)
                                {
                                    endSectionIndex = thisDeadlockInfo.AvailablePathList[otherTrainAllocatedPaths[0]].EndSectionIndex;
                                    endSectionRouteIndex = thisRoute.GetRouteIndex(endSectionIndex, thisTrainIndex);
                                }

                                Dictionary<int, bool> otherTrainFitList =
                                    thisDeadlockInfo.TrainLengthFit[thisDeadlockInfo.GetTrainAndSubpathIndex(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath)];
                                foreach (int iPath in otherTrainAllocatedPaths)
                                {
                                    if (otherTrainFitList[iPath])
                                    {
                                        otherTrainFits = true;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        // other train has no valid path relating to the passing path, so passing not possible
                        {
                            validPassLocation = false;
                        }

                        // if both trains have only one route, make sure it's not the same (inverse) route

                        if (thisTrainAllocatedPaths.Count == 1 && otherTrainAllocatedPaths.Count == 1)
                        {
                            if (thisDeadlockInfo.InverseInfo.ContainsKey(thisTrainAllocatedPaths[0]) && thisDeadlockInfo.InverseInfo[thisTrainAllocatedPaths[0]] == otherTrainAllocatedPaths[0])
                            {
                                validPassLocation = false;
                            }
                        }

                        // if there are passing paths and at least one train fits in shortest path, it is a valid location so break loop
                        if (validPassLocation)
                        {
                            gotoNextSection = false;
                            if (thisTrainFits || otherTrainFits)
                            {
                                if (thisSection.IsSet(otherTrain, true))
                                {
                                    allreadyActive = true;
                                }
                                endOfLoop = true;
                            }
                            else
                            {
                                thisTrainNextIndex = endSectionRouteIndex;
                                otherTrainNextIndex = otherRoute.GetRouteIndexBackward(endSectionIndex, otherTrainIndex);
                                if (otherTrainNextIndex < 0) endOfLoop = true;
                            }
                        }
                    }

                    // if loop not yet ended - not a valid pass location, move to next section (if available)

                    if (gotoNextSection)
                    {
                        // if this section is occupied by other train, break loop - further checks are of no use
                        if (thisSection.IsSet(otherTrain, true))
                        {
                            allreadyActive = true;
                            endOfLoop = true;
                        }
                        else
                        {
                            thisTrainNextIndex++;
                            otherTrainNextIndex--;

                            if (thisTrainNextIndex > thisRoute.Count - 1 || otherTrainNextIndex < 0)
                            {
                                endOfLoop = true; // end of path reached for either train
                            }
                        }
                    }
                }
            }

            // if valid pass location : set return index

            if (validPassLocation && endSectionRouteIndex >= 0)
            {
                returnValue[1] = endSectionRouteIndex;
            }

            // get sections on which loop ended
            thisTrainElement = thisRoute[thisTrainIndex];
            thisTrainSectionIndex = thisTrainElement.TrackCircuitSection.Index;

            otherTrainElement = otherRoute[otherTrainIndex];
            otherTrainSectionIndex = otherTrainElement.TrackCircuitSection.Index;

            // if last sections are still equal - end of route reached for one of the trains
            // otherwise, last common sections was previous sections for this train
            TrackCircuitSection lastSection = (thisTrainSectionIndex == otherTrainSectionIndex) ? TrackCircuitSection.TrackCircuitList[thisTrainSectionIndex]  :
                thisRoute[thisTrainIndex - 1].TrackCircuitSection;

            // TODO : if section is not a junction but deadlock is allready active, wind back to last junction
            // if section is not a junction, check if either route not ended, if so continue up to next junction
            if (lastSection.CircuitType != TrackCircuitType.Junction)
            {
                bool endSectionFound = false;
                if (thisTrainIndex < (thisRoute.Count - 1))
                {
                    for (int iIndex = thisTrainIndex; iIndex < thisRoute.Count - 1 && !endSectionFound; iIndex++)
                    {
                        lastSection = thisRoute[iIndex].TrackCircuitSection;
                        endSectionFound = lastSection.CircuitType == TrackCircuitType.Junction;
                    }
                }

                else if (otherTrainIndex > 0)
                {
                    for (int iIndex = otherTrainIndex; iIndex >= 0 && !endSectionFound; iIndex--)
                    {
                        lastSection = otherRoute[iIndex].TrackCircuitSection;
                        endSectionFound = false;

                        // junction found - end of loop
                        if (lastSection.CircuitType == TrackCircuitType.Junction)
                        {
                            endSectionFound = true;
                        }
                        // train has active wait condition at this location - end of loop
                        else if (otherTrain.CheckWaitCondition(lastSection.Index))
                        {
                            endSectionFound = true;
                        }

                        if (lastSection.IsSet(otherTrain, true))
                        {
                            allreadyActive = true;
                        }
                    }
                }
            }

            // set deadlock info for both trains

            SetDeadlockInfo(firstSectionIndex, lastSection.Index, otherTrain.Number);
            otherTrain.SetDeadlockInfo(lastSection.Index, firstSectionIndex, Number);

            if (allreadyActive)
            {
                lastSection.SetDeadlockTrap(otherTrain, otherTrain.DeadlockInfo[lastSection.Index]);
                returnValue[1] = thisRoute.Count;  // set beyond end of route - no further checks required
            }

            // if any section occupied by own train, reverse deadlock is active

            TrackCircuitSection firstSection = TrackCircuitSection.TrackCircuitList[firstSectionIndex];

            int firstRouteIndex = ValidRoute[0].GetRouteIndex(firstSectionIndex, 0);
            int lastRouteIndex = ValidRoute[0].GetRouteIndex(lastSection.Index, 0);

            for (int iRouteIndex = firstRouteIndex; iRouteIndex < lastRouteIndex; iRouteIndex++)
            {
                TrackCircuitSection partSection = ValidRoute[0][iRouteIndex].TrackCircuitSection;
                if (partSection.IsSet(this, true))
                {
                    firstSection.SetDeadlockTrap(this, DeadlockInfo[firstSectionIndex]);
                }
            }

            returnValue[0] = thisRoute.GetRouteIndex(lastSection.Index, thisIndex);
            if (returnValue[0] < 0)
                returnValue[0] = thisTrainIndex;
            return (returnValue);
        }

        //================================================================================================//
        //
        // Check if conflict is real deadlock situation
        // Conditions :
        //   if section is part of deadlock definition, it is a deadlock
        //   if section has intermediate signals, it is a deadlock
        //   if section has no intermediate signals but there are signals on both approaches to the deadlock, it is not a deadlock
        // Return value : boolean to indicate it is a deadlock or not
        // If not a deadlock, the REF int elementIndex is set to index of the last common section (will be increased in the loop)
        //

        internal bool CheckRealDeadlock_locationBased(TrackCircuitPartialPathRoute thisRoute, TrackCircuitPartialPathRoute otherRoute, ref int elementIndex)
        {
            bool isValidDeadlock = false;

            TrackCircuitSection thisSection = thisRoute[elementIndex].TrackCircuitSection;

            // check if section is start or part of deadlock definition
            if (thisSection.DeadlockReference >= 0 || (thisSection.DeadlockBoundaries != null && thisSection.DeadlockBoundaries.Count > 0))
            {
                return (true);
            }

            // loop through common section - if signal is found, it is a deadlock 

            bool validLoop = true;
            int otherRouteIndex = otherRoute.GetRouteIndex(thisSection.Index, 0);

            for (int iIndex = 0; validLoop; iIndex++)
            {
                int thisElementIndex = elementIndex + iIndex;
                int otherElementIndex = otherRouteIndex - iIndex;

                if (thisElementIndex > thisRoute.Count - 1) validLoop = false;
                if (otherElementIndex < 0) validLoop = false;

                if (validLoop)
                {
                    TrackCircuitSection thisRouteSection = thisRoute[thisElementIndex].TrackCircuitSection;
                    TrackCircuitSection otherRouteSection = otherRoute[otherElementIndex].TrackCircuitSection;

                    if (thisRouteSection.Index != otherRouteSection.Index)
                    {
                        validLoop = false;
                    }
                    else if (thisRouteSection.EndSignals[TrackDirection.Ahead] != null || thisRouteSection.EndSignals[TrackDirection.Reverse] != null)
                    {
                        isValidDeadlock = true;
                        validLoop = false;
                    }
                }
            }

            // if no signals along section, check if section is protected by signals - if so, it is not a deadlock
            // check only as far as maximum signal check distance

            if (!isValidDeadlock)
            {
                // this route backward first
                float totalDistance = 0.0f;
                bool thisSignalFound = false;
                validLoop = true;

                for (int iIndex = 0; validLoop; iIndex--)
                {
                    int thisElementIndex = elementIndex + iIndex; // going backward as iIndex is negative!
                    if (thisElementIndex < 0)
                    {
                        validLoop = false;
                    }
                    else
                    {
                        TrackCircuitRouteElement thisElement = thisRoute[thisElementIndex];
                        TrackCircuitSection thisRouteSection = thisElement.TrackCircuitSection;
                        totalDistance += thisRouteSection.Length;

                        if (thisRouteSection.EndSignals[thisElement.Direction] != null)
                        {
                            validLoop = false;
                            thisSignalFound = true;
                        }

                        if (totalDistance > minCheckDistanceM) validLoop = false;
                    }
                }

                // other route backward next
                totalDistance = 0.0f;
                bool otherSignalFound = false;
                validLoop = true;

                for (int iIndex = 0; validLoop; iIndex--)
                {
                    int thisElementIndex = otherRouteIndex + iIndex; // going backward as iIndex is negative!
                    if (thisElementIndex < 0)
                    {
                        validLoop = false;
                    }
                    else
                    {
                        TrackCircuitRouteElement thisElement = otherRoute[thisElementIndex];
                        TrackCircuitSection thisRouteSection = thisElement.TrackCircuitSection;
                        totalDistance += thisRouteSection.Length;

                        if (thisRouteSection.EndSignals[thisElement.Direction] != null)
                        {
                            validLoop = false;
                            otherSignalFound = true;
                        }

                        if (totalDistance > minCheckDistanceM) validLoop = false;
                    }
                }

                if (!thisSignalFound || !otherSignalFound) isValidDeadlock = true;
            }

            // if not a valid deadlock, find end of common section

            if (!isValidDeadlock)
            {
                int newElementIndex = EndCommonSection(elementIndex, thisRoute, otherRoute);
                elementIndex = newElementIndex;
            }

            return (isValidDeadlock);
        }

        //================================================================================================//
        //
        // Set deadlock information
        //

        private void SetDeadlockInfo(int firstSection, int lastSection, int otherTrainNumber)
        {
            List<Dictionary<int, int>> DeadlockList = null;

            if (DeadlockInfo.ContainsKey(firstSection))
            {
                DeadlockList = DeadlockInfo[firstSection];
            }
            else
            {
                DeadlockList = new List<Dictionary<int, int>>();
                DeadlockInfo.Add(firstSection, DeadlockList);
            }
            Dictionary<int, int> thisDeadlock = new Dictionary<int, int>();
            thisDeadlock.Add(otherTrainNumber, lastSection);
            DeadlockList.Add(thisDeadlock);
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
        //
        // Check if waiting for deadlock
        //

        public bool CheckDeadlockWait(Signal nextSignal)
        {

            bool deadlockWait = false;

            // check section list of signal for any deadlock traps

            foreach (TrackCircuitRouteElement thisElement in nextSignal.SignalRoute)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                if (thisSection.DeadlockTraps.ContainsKey(Number))              // deadlock trap
                {
                    deadlockWait = true;

                    List<int> deadlockTrains = thisSection.DeadlockTraps[Number];

                    if (DeadlockInfo.ContainsKey(thisSection.Index) && !CheckWaitCondition(thisSection.Index)) // reverse deadlocks and not waiting
                    {
                        foreach (Dictionary<int, int> thisDeadlockList in DeadlockInfo[thisSection.Index])
                        {
                            foreach (KeyValuePair<int, int> thisDeadlock in thisDeadlockList)
                            {
                                if (!deadlockTrains.Contains(thisDeadlock.Key))
                                {
                                    TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[thisDeadlock.Value];
                                    endSection.SetDeadlockTrap(Number, thisDeadlock.Key);
                                }
                                else
                                {
                                    // check if train has reversal before end of path of other train
                                    if (TCRoute.TCRouteSubpaths.Count > (TCRoute.ActiveSubPath + 1))
                                    {
                                        Train otherTrain = GetOtherTrainByNumber(thisDeadlock.Key);

                                        bool commonSectionFound = false;
                                        bool lastReserved = false;
                                        for (int otherIndex = otherTrain.PresentPosition[0].RouteListIndex + 1;
                                             otherIndex < otherTrain.ValidRoute[0].Count - 1 && !commonSectionFound && !lastReserved;
                                             otherIndex++)
                                        {
                                            TrackCircuitSection otherSection = otherTrain.ValidRoute[0][otherIndex].TrackCircuitSection;
                                            for (int ownIndex = PresentPosition[0].RouteListIndex; ownIndex < ValidRoute[0].Count - 1; ownIndex++)
                                            {
                                                if (otherSection.Index == ValidRoute[0][ownIndex].TrackCircuitSection.Index)
                                                {
                                                    commonSectionFound = true;
                                                }
                                            }
                                            if (otherSection.CircuitState.TrainReserved == null || otherSection.CircuitState.TrainReserved.Train.Number != otherTrain.Number)
                                            {
                                                lastReserved = true;
                                            }
                                            //if (sectionIndex == otherTrain.LastReservedSection[0]) lastReserved = true;
                                        }

                                        if (!commonSectionFound)
                                        {
                                            TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[thisDeadlock.Value];
                                            endSection.ClearDeadlockTrap(Number);
                                            thisSection.ClearDeadlockTrap(otherTrain.Number);
                                            deadlockWait = false;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return (deadlockWait);
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
                DateTime arriveDT = new DateTime().AddSeconds(thisItem.ArrivalTime);
                DateTime departDT = new DateTime().AddSeconds(thisItem.DepartTime);
                bool validStop =
                    CreateStationStop(thisItem.PlatformStartID, thisItem.ArrivalTime, thisItem.DepartTime, arriveDT, departDT, clearingDistanceM,
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

        public bool CreateStationStop(int platformStartID, int arrivalTime, int departTime, DateTime arrivalDT, DateTime departureDT, float clearingDistanceM,
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

                if (!Simulator.TimetableMode && routeIndex == thisRoute.Count - 1 && TCRoute.ReversalInfo[activeSubroute].Valid)
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
                    if (!Simulator.TimetableMode && routeIndex == thisRoute.Count - 1 && TCRoute.ReversalInfo[activeSubroute].Valid)
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
                    if (!Simulator.TimetableMode && routeIndex == thisRoute.Count - 1 && TCRoute.ReversalInfo[activeSubroute].Valid)
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
                        if (!Simulator.TimetableMode && routeIndex == thisRoute.Count - 1 && TCRoute.ReversalInfo[activeSubroute].Valid)
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
                if (terminalStation && deltaLength > 0 && !Simulator.TimetableMode)
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
                        (CheckVicinityOfPlatformToReversalPoint(thisPlatform.TrackCircuitOffset[Location.FarEnd, (TrackDirection)thisElement.Direction], activeSubrouteNodeIndex, activeSubroute) || Simulator.TimetableMode)
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
                        else if (terminalStation && deltaLength <= 0 && !Simulator.TimetableMode)
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
                        if (thisPlatform.EndSignals[oldUseDirection] >= 0 && terminalStation && deltaLength <= 0 && !Simulator.TimetableMode)
                        {
                            // check also the back of train after reverse
                            stopOffset = endOffset + thisPlatform.DistanceToSignals[oldUseDirection] - 3.0f;
                        }
                        if ((beginOffset - thisPlatform.DistanceToSignals[useDirection]) < (stopOffset - Length))
                        {
                            HoldSignal = true;

                            if ((stopOffset - Length - beginOffset + thisPlatform.DistanceToSignals[useDirection]) < clearingDistanceM)
                            {
                                if (!(terminalStation && deltaLength > 0 && !Simulator.TimetableMode))
                                    stopOffset = beginOffset - thisPlatform.DistanceToSignals[useDirection] + Length + clearingDistanceM + 1.0f;
                            }
                        }
                        // if most of train fits in platform then stop at signal
                        else if ((thisPlatform.DistanceToSignals[useDirection] - clearingDistanceM + thisPlatform.Length) >
                                      (0.6 * Length))
                        {
                            // set 1m earlier to give priority to station stop over signal
                            if (!(terminalStation && deltaLength > 0 && !Simulator.TimetableMode))
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
                                    if (!(terminalStation && deltaLength > 0 && !Simulator.TimetableMode))
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

                if (Simulator.Settings.NoForcedRedAtStationStops)
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
                        (int)thisElement.Direction,
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
                        StationStop.STOPTYPE.STATION_STOP);

                thisStation.arrivalDT = arrivalDT;
                thisStation.departureDT = departureDT;

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
            int frontIndex = PresentPosition[0].RouteListIndex;
            int rearIndex = PresentPosition[1].RouteListIndex;
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

            int stationRouteIndex = ValidRoute[0].GetRouteIndex(StationStops[0].TCSectionIndex, 0);

            if (StationStops[0].SubrouteIndex == TCRoute.ActiveSubPath)
            {
                if (stationRouteIndex < 0)
                {
                    return true;
                }
                else if (stationRouteIndex <= PresentPosition[1].RouteListIndex)
                {
                    var platformSection = TrackCircuitSection.TrackCircuitList[StationStops[0].TCSectionIndex];
                    var platformReverseStopOffset = platformSection.Length - StationStops[0].StopOffset;
                    return ValidRoute[0].GetDistanceAlongRoute(stationRouteIndex, platformReverseStopOffset, PresentPosition[1].RouteListIndex, PresentPosition[1].TCOffset, true) > thresholdDistance;
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
            if (DateTime.UtcNow.Millisecond % 10 < 6 - Simulator.Settings.ActRandomizationLevel) return 0;
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
                randomizedDelay += RandomizedDelayWithThreshold(15 + 5 * Simulator.Settings.ActRandomizationLevel);
            }
            else if (randomizedDelay >= 30000 && randomizedDelay < 40000) // absolute WP
            {
                randomizedDelay += RandomizedDelayWithThreshold(2 + Simulator.Settings.ActRandomizationLevel);
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
            TrainMaxSpeedMpS = Math.Min((float)Simulator.TRK.Route.SpeedLimit, ((MSTSLocomotive)Simulator.PlayerLocomotive).MaxSpeedMpS);
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
            if (!Simulator.TimetableMode && this != Simulator.OriginalPlayerTrain) statusString[iColumn] = Name.Substring(0, Math.Min(Name.Length, 7));
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

                int startIndex = PresentPosition[0].RouteListIndex;
                if (startIndex < 0)
                {
                    circuitString = String.Concat(circuitString, "<out of route>");
                }
                else
                {
                    for (int iIndex = PresentPosition[0].RouteListIndex; iIndex < ValidRoute[0].Count; iIndex++)
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
            if (this == Simulator.OriginalPlayerTrain)
            {
                if (Simulator.ActivityRun != null && Simulator.ActivityRun.Current is ActivityTaskPassengerStopAt && ((ActivityTaskPassengerStopAt)Simulator.ActivityRun.Current).BoardingS > 0)
                {
                    movString = "STA";
                    DateTime depTime = baseDT.AddSeconds(((ActivityTaskPassengerStopAt)Simulator.ActivityRun.Current).BoardingEndS);
                    abString = depTime.ToString("HH:mm:ss");
                }
                else
                   if (Math.Abs(SpeedMpS) <= 0.01 && AuxActionsContain.specRequiredActions.Count > 0 && AuxActionsContain.specRequiredActions.First.Value is AuxActSigDelegate &&
                    (AuxActionsContain.specRequiredActions.First.Value as AuxActSigDelegate).currentMvmtState == AITrain.AI_MOVEMENT_STATE.HANDLE_ACTION)
                {
                    movString = "WTS";
                    DateTime depTime = baseDT.AddSeconds((AuxActionsContain.specRequiredActions.First.Value as AuxActSigDelegate).ActualDepart);
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
            else if (Math.Abs(SpeedMpS) <= 0.01 && AuxActionsContain.SpecAuxActions.Count > 0 && AuxActionsContain.SpecAuxActions[0] is AIActionWPRef &&
                (AuxActionsContain.SpecAuxActions[0] as AIActionWPRef).keepIt != null &&
                (AuxActionsContain.SpecAuxActions[0] as AIActionWPRef).keepIt.currentMvmtState == AITrain.AI_MOVEMENT_STATE.HANDLE_ACTION)
            {
                movString = "WTP";
                DateTime depTime = baseDT.AddSeconds((AuxActionsContain.SpecAuxActions[0] as AIActionWPRef).keepIt.ActualDepart);
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
                SpeedMpS, ProjectedSpeedMpS, Math.Min(AllowedMaxSpeedMpS, TrainMaxSpeedMpS), Simulator.PlayerLocomotive?.CurrentElevationPercent ?? 0,
                Simulator.PlayerLocomotive != null ? ((Simulator.PlayerLocomotive.Flipped ^ Simulator.PlayerLocomotive.GetCabFlipped()) ? Direction.Backward : Direction.Forward) : Direction.Forward, true);

            AddTrainReversalInfo(result, TCRoute.ReversalInfo[TCRoute.ActiveSubPath]);

            // set waiting point
            if (this != Simulator.OriginalPlayerTrain)
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

            if (ClearanceAtRearM <= 0)
            {
                result.ObjectInfoBackward.Add(new TrainPathItem(EndAuthorityType.NoPathReserved, 0.0f));
            }
            else
            {
                if (RearSignalObject != null)
                {
                    TrackMonitorSignalAspect signalAspect = RearSignalObject.TranslateTMAspect(RearSignalObject.SignalLR(SignalFunction.Normal));
                    result.ObjectInfoBackward.Add(new TrainPathItem(signalAspect, -1.0f, ClearanceAtRearM));
                }
                else
                {
                    result.ObjectInfoBackward.Add(new TrainPathItem(EndAuthorityType.EndOfAuthority, ClearanceAtRearM));
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
                float offset = PresentPosition[routeDirection].TCOffset;
                TrackCircuitSection firstSection = TrackCircuitSection.TrackCircuitList[PresentPosition[routeDirection].TCSectionIndex];
                float sectionStart = routeDirection == 0 ? -offset : offset - firstSection.Length;
                int startRouteIndex = PresentPosition[routeDirection].RouteListIndex;
                if (startRouteIndex < 0) startRouteIndex = ValidRoute[routeDirection].GetRouteIndex(PresentPosition[routeDirection].TCSectionIndex, 0);
                if (startRouteIndex >= 0)
                {
                    for (int iRouteElement = startRouteIndex; iRouteElement < ValidRoute[routeDirection].Count && distanceToTrainM < 7000 && sectionStart < 7000; iRouteElement++)
                    {
                        TrackCircuitSection thisSection = ValidRoute[routeDirection][iRouteElement].TrackCircuitSection;
                        TrackDirection sectionDirection = ValidRoute[routeDirection][iRouteElement].Direction;

                        if (thisSection.CircuitType == TrackCircuitType.Junction && (thisSection.Pins[sectionDirection, Location.FarEnd].Link != -1) && sectionStart < 7000)
                        {
                            bool isRightSwitch = true;
                            TrackJunctionNode junctionNode = Simulator.TDB.TrackDB.TrackNodes[thisSection.OriginalIndex] as TrackJunctionNode;
                            var isDiverging = false;
                            if ((thisSection.ActivePins[sectionDirection, Location.FarEnd].Link > 0 && thisSection.JunctionDefaultRoute == 0) ||
                                (thisSection.ActivePins[sectionDirection, Location.NearEnd].Link > 0 && thisSection.JunctionDefaultRoute > 0))
                            {
                                // diverging 
                                isDiverging = true;
                                var junctionAngle = junctionNode.GetAngle(Simulator.TSectionDat);
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

            TrackCircuitSection rearSection = TrackCircuitSection.TrackCircuitList[PresentPosition[1].TCSectionIndex];
            float reversalDistanceM = TrackCircuitSection.GetDistanceBetweenObjects(PresentPosition[1].TCSectionIndex, PresentPosition[1].TCOffset, (TrackDirection)PresentPosition[1].TCDirection, reversalSection, 0.0f);

            bool reversalEnabled = true;
            TrackCircuitSection frontSection = TrackCircuitSection.TrackCircuitList[PresentPosition[0].TCSectionIndex];
            reversalDistanceM = Math.Max(reversalDistanceM, TrackCircuitSection.GetDistanceBetweenObjects
                (PresentPosition[0].TCSectionIndex, PresentPosition[0].TCOffset, (TrackDirection)PresentPosition[0].TCDirection,
                reversalInfo.ReversalSectionIndex, reversalInfo.ReverseReversalOffset));
            int reversalIndex = reversalInfo.SignalUsed ? reversalInfo.LastSignalIndex : reversalInfo.LastDivergeIndex;
            if (reversalDistanceM > 50f || (PresentPosition[1].RouteListIndex < reversalIndex))
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
            if (AuxActionsContain.SpecAuxActions.Count > 0 && AuxActionsContain.SpecAuxActions[0] is AIActionWPRef &&
                (AuxActionsContain.SpecAuxActions[0] as AIActionWPRef).SubrouteIndex == TCRoute.ActiveSubPath)
            {
                TrackCircuitSection frontSection = TrackCircuitSection.TrackCircuitList[PresentPosition[0].TCSectionIndex];
                int thisSectionIndex = PresentPosition[0].TCSectionIndex;
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
                float leftInSectionM = thisSection.Length - PresentPosition[0].TCOffset;

                // get action route index - if not found, return distances < 0

                int actionIndex0 = PresentPosition[0].RouteListIndex;
                int actionRouteIndex = ValidRoute[0].GetRouteIndex((AuxActionsContain.SpecAuxActions[0] as AIActionWPRef).TCSectionIndex, actionIndex0);
                var wpDistance = ValidRoute[0].GetDistanceAlongRoute(actionIndex0, leftInSectionM, actionRouteIndex, (AuxActionsContain.SpecAuxActions[0] as AIActionWPRef).RequiredDistance, AITrainDirectionForward);
                bool wpEnabled = false;
                if (Math.Abs(SpeedMpS) <= Simulator.MaxStoppedMpS && (((AuxActionsContain.SpecAuxActions[0] as AIActionWPRef).keepIt != null &&
                    (AuxActionsContain.SpecAuxActions[0] as AIActionWPRef).keepIt.currentMvmtState == AITrain.AI_MOVEMENT_STATE.HANDLE_ACTION) ||
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
                validPath = pathRoute.GetRouteIndex(PresentPosition[0].TCSectionIndex, 0) >= 0;
            }

            TrainInfo result = new TrainInfo(ControlMode, MidPointDirectionToDirectionUnset(MUDirection), SpeedMpS, ProjectedSpeedMpS,
                Math.Min(AllowedMaxSpeedMpS, TrainMaxSpeedMpS), Simulator.PlayerLocomotive != null ? Simulator.PlayerLocomotive.CurrentElevationPercent : 0,
                (Simulator.PlayerLocomotive.Flipped ^ Simulator.PlayerLocomotive.GetCabFlipped()) ? Direction.Backward : Direction.Forward, validPath);


            // set forward information

            // set authority
            result.ObjectInfoForward.Add(new TrainPathItem(EndAuthorityTypes[0], DistanceToEndNodeAuthorityM[0]));

            // run along forward path to catch all speedposts and signals
            if (ValidRoute[0] != null)
            {
                float distanceToTrainM = 0.0f;
                float offset = PresentPosition[0].TCOffset;
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
                float offset = PresentPosition[1].TCOffset;
                TrackCircuitSection firstSection = TrackCircuitSection.TrackCircuitList[PresentPosition[1].TCSectionIndex];
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
                (Simulator.PlayerLocomotive.Flipped ^ Simulator.PlayerLocomotive.GetCabFlipped()) ? Direction.Backward : Direction.Forward, false) ;

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
                float offset = PresentPosition[0].TCOffset;
                TrackCircuitSection firstSection = TrackCircuitSection.TrackCircuitList[PresentPosition[0].TCSectionIndex];
                float sectionStart = -offset;
                int startRouteIndex = PresentPosition[0].RouteListIndex;
                if (startRouteIndex < 0) startRouteIndex = ValidRoute[0].GetRouteIndex(PresentPosition[0].TCSectionIndex, 0);
                if (startRouteIndex >= 0)
                {
                    int routeSectionIndex = PresentPosition[0].TCSectionIndex;
                    for (int iRouteElement = startRouteIndex; iRouteElement < ValidRoute[0].Count && distanceToTrainM < maxDistanceM && sectionStart < maxDistanceM; iRouteElement++)
                    {
                        TrackCircuitSection thisSection = ValidRoute[0][iRouteElement].TrackCircuitSection;
                        TrackDirection sectionDirection = ValidRoute[0][iRouteElement].Direction;

                        if (thisSection.CircuitType == TrackCircuitType.Junction && (thisSection.Pins[sectionDirection, Location.FarEnd].Link == -1) && sectionStart < maxDistanceM)
                        {
                            // is trailing
                            TrackJunctionNode junctionNode = Simulator.TDB.TrackDB.TrackNodes[thisSection.OriginalIndex] as TrackJunctionNode;
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
                routeListIndex = PresentPosition[0].RouteListIndex;
                presentOffset = PresentPosition[0].TCOffset;
                routedTrain = routedForward;
            }
            else
            {
                usedRoute = ValidRoute[1];
                routeListIndex = PresentPosition[1].RouteListIndex;
                presentOffset = PresentPosition[1].TCOffset;
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

            int startElementIndex = thisRoute.GetRouteIndex(startSectionIndex, PresentPosition[0].RouteListIndex);
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

#if DEBUG_DEADLOCK
                File.AppendAllText(@"C:\temp\deadlock.txt","Abandoning section " + abdSection.Index + " for Train " + Number + "\n");
#endif

                if (newRoute.GetRouteIndex(abdSection.Index, 0) < 0)
                {

#if DEBUG_DEADLOCK
                File.AppendAllText(@"C:\temp\deadlock.txt","Removing deadlocks for section " + abdSection.Index + " for Train " + Number + "\n");
#endif

                    abdSection.ClearDeadlockTrap(Number);
                }

#if DEBUG_DEADLOCK
                else
                {
                    File.AppendAllText(@"C:\temp\deadlock.txt","Section " + abdSection.Index + " for Train " + Number + " in new route, not removing deadlocks\n");
                }
                File.AppendAllText(@"C:\temp\deadlock.txt", "\n");
#endif

            }

#if DEBUG_DEADLOCK
            File.AppendAllText(@"C:\temp\deadlock.txt", "\n");
#endif
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
                    statStop.RouteIndex = newRoute.GetRouteIndex(statStop.TCSectionIndex, prevIndex);
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
                        orgStop.ArrivalTime, orgStop.DepartTime, orgStop.arrivalDT, orgStop.departureDT, 15.0f);

                    return (newStop);
                }
            }

            return (null);
        }

        //================================================================================================//
        /// <summary>
        /// Create station stop (used in activity mode only)
        /// <\summary>

        public StationStop CalculateStationStop(int platformStartID, int arrivalTime, int departTime, DateTime arrivalDT, DateTime departureDT, float clearingDistanceM)
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

                if (Simulator.Settings.NoForcedRedAtStationStops)
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
                        (int)thisElement.Direction,
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
                        StationStop.STOPTYPE.STATION_STOP);

                thisStation.arrivalDT = arrivalDT;
                thisStation.departureDT = departureDT;

                return (thisStation);
            }
        }

        //================================================================================================//
        //
        // Set train route to alternative route - location based deadlock processing
        //

        public void ClearDeadlocks()
        {
            // clear deadlocks
            foreach (KeyValuePair<int, List<Dictionary<int, int>>> thisDeadlock in DeadlockInfo)
            {
#if DEBUG_DEADLOCK
                File.AppendAllText(@"C:\Temp\deadlock.txt", "\n === Removed Train : " + Number.ToString() + "\n");
                File.AppendAllText(@"C:\Temp\deadlock.txt", "Deadlock at section : " + thisDeadlock.Key.ToString() + "\n");
#endif
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisDeadlock.Key];
                foreach (Dictionary<int, int> deadlockTrapInfo in thisDeadlock.Value)
                {
                    foreach (KeyValuePair<int, int> deadlockedTrain in deadlockTrapInfo)
                    {
                        Train otherTrain = GetOtherTrainByNumber(deadlockedTrain.Key);

#if DEBUG_DEADLOCK
                        File.AppendAllText(@"C:\Temp\deadlock.txt", "Other train index : " + deadlockedTrain.Key.ToString() + "\n");
                        if (otherTrain == null)
                        {
                            File.AppendAllText(@"C:\Temp\deadlock.txt", "Other train not found!" + "\n");
                        }
                        else
                        {
                            File.AppendAllText(@"C:\Temp\deadlock.txt", "CrossRef train info : " + "\n");
                            foreach (KeyValuePair<int, List<Dictionary<int, int>>> reverseDeadlock in otherTrain.DeadlockInfo)
                            {
                                File.AppendAllText(@"C:\Temp\deadlock.txt", "   " + reverseDeadlock.Key.ToString() + "\n");
                            }

                            foreach (KeyValuePair<int, List<Dictionary<int, int>>> reverseDeadlock in otherTrain.DeadlockInfo)
                            {
                                if (reverseDeadlock.Key == deadlockedTrain.Value)
                                {
                                    File.AppendAllText(@"C:\Temp\deadlock.txt", "Reverse Info : " + "\n");
                                    foreach (Dictionary<int, int> sectorList in reverseDeadlock.Value)
                                    {
                                        foreach (KeyValuePair<int, int> reverseInfo in sectorList)
                                        {
                                            File.AppendAllText(@"C:\Temp\deadlock.txt", "   " + reverseInfo.Key.ToString() + " + " + reverseInfo.Value.ToString() + "\n");
                                        }
                                    }
                                }
                            }
                        }
#endif
                        if (otherTrain != null && otherTrain.DeadlockInfo.ContainsKey(deadlockedTrain.Value))
                        {
                            List<Dictionary<int, int>> otherDeadlock = otherTrain.DeadlockInfo[deadlockedTrain.Value];
                            for (int iDeadlock = otherDeadlock.Count - 1; iDeadlock >= 0; iDeadlock--)
                            {
                                Dictionary<int, int> otherDeadlockInfo = otherDeadlock[iDeadlock];
                                if (otherDeadlockInfo.ContainsKey(Number)) otherDeadlockInfo.Remove(Number);
                                if (otherDeadlockInfo.Count <= 0) otherDeadlock.RemoveAt(iDeadlock);
                            }

                            if (otherDeadlock.Count <= 0)
                                otherTrain.DeadlockInfo.Remove(deadlockedTrain.Value);

                            if (otherTrain.DeadlockInfo.Count <= 0)
                                thisSection.ClearDeadlockTrap(otherTrain.Number);
                        }
                        TrackCircuitSection otherSection = TrackCircuitSection.TrackCircuitList[deadlockedTrain.Value];
                        otherSection.ClearDeadlockTrap(Number);
                    }
                }
            }

            DeadlockInfo.Clear();
        }

        //================================================================================================//
        /// <summary>
        /// Get other train from number
        /// Use Simulator.Trains to get other train
        /// </summary>

        public Train GetOtherTrainByNumber(int reqNumber)
        {
            return Simulator.Trains.GetTrainByNumber(reqNumber);
        }

        //================================================================================================//
        /// <summary>
        /// Get other train from number
        /// Use Simulator.Trains to get other train
        /// </summary>

        public Train GetOtherTrainByName(string reqName)
        {
            return Simulator.Trains.GetTrainByName(reqName);
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
        public virtual void ActionsForSignalStop(ref bool claimAllowed)
        {
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

        //================================================================================================//
        /// <summary>
        /// Update Section State - additional
        /// dummy method to allow virtualisation for Timetable trains
        /// </summary>

        public virtual void UpdateSectionState_Additional(int sectionIndex)
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
        /// Check if deadlock must be accepted
        /// Dummy method to allow virtualization by child classes
        /// <\summary>

        public virtual bool VerifyDeadlock(List<int> deadlockReferences)
        {
            return (true);
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
                            if (BrakingTime >= 1200.0 / Simulator.Settings.ActRandomizationLevel || ContinuousBrakingTime >= 600.0 / Simulator.Settings.ActRandomizationLevel)
                            {
                                var randInt = Simulator.Random.Next(200000);
                                var brakesStuck = false;
                                if (randInt > 200000 - (Simulator.Settings.ActRandomizationLevel == 1 ? 4 : Simulator.Settings.ActRandomizationLevel == 2 ? 8 : 31))
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
                                        Simulator.Confirmer.Warning(Simulator.Catalog.GetString("Car " + Cars[iBrakesStuckCar].CarID + " has stuck brakes"));
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
                        if (randInt > 2000000 / nLocos - (Simulator.Settings.ActRandomizationLevel == 1 ? 2 : Simulator.Settings.ActRandomizationLevel == 2 ? 8 : 50))
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
                                    Simulator.Confirmer.Warning(Simulator.Catalog.GetString("Locomotive " + unpoweredLoco.CarID + " partial failure: 1 unpowered bogie"));
                                }
                                else
                                {
                                    unpoweredLoco.PowerReduction = 1.0f;
                                    Simulator.Confirmer.Warning(Simulator.Catalog.GetString("Locomotive " + unpoweredLoco.CarID + " compressor blown"));
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
            public int TrainRouteDirectionIndex;

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
        /// TrackCircuit position class
        /// </summary>

        public class TCPosition
        {
            public int TCSectionIndex;
            public int TCDirection;
            public float TCOffset;
            public int RouteListIndex;
            public int TrackNode;
            public float DistanceTravelledM;

            //================================================================================================//
            /// <summary>
            /// constructor - creates empty item
            /// </summary>

            public TCPosition()
            {
                TCSectionIndex = -1;
                TCDirection = 0;
                TCOffset = 0.0f;
                RouteListIndex = -1;
                TrackNode = -1;
                DistanceTravelledM = 0.0f;
            }

            //================================================================================================//
            //
            // Restore
            //

            public void RestorePresentPosition(BinaryReader inf, Train train)
            {
                TrackNode tn = train.FrontTDBTraveller.TN;
                float offset = train.FrontTDBTraveller.TrackNodeOffset;
                int direction = (int)train.FrontTDBTraveller.Direction;

                TCPosition tempPosition = new TCPosition();
                tempPosition.SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);

                TCSectionIndex = inf.ReadInt32();
                TCDirection = inf.ReadInt32();
                TCOffset = inf.ReadSingle();
                RouteListIndex = inf.ReadInt32();
                TrackNode = inf.ReadInt32();
                DistanceTravelledM = inf.ReadSingle();

                float offsetDif = Math.Abs(TCOffset - tempPosition.TCOffset);
                if (TCSectionIndex != tempPosition.TCSectionIndex ||
                        (TCSectionIndex == tempPosition.TCSectionIndex && offsetDif > 5.0f))
                {
                    Trace.TraceWarning("Train {0} restored at different present position : was {1} - {3}, is {2} - {4}",
                            train.Number, TCSectionIndex, tempPosition.TCSectionIndex,
                            TCOffset, tempPosition.TCOffset);
                }
            }


            public void RestorePresentRear(BinaryReader inf, Train train)
            {
                TrackNode tn = train.RearTDBTraveller.TN;
                float offset = train.RearTDBTraveller.TrackNodeOffset;
                int direction = (int)train.RearTDBTraveller.Direction;

                TCPosition tempPosition = new TCPosition();
                tempPosition.SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction);

                TCSectionIndex = inf.ReadInt32();
                TCDirection = inf.ReadInt32();
                TCOffset = inf.ReadSingle();
                RouteListIndex = inf.ReadInt32();
                TrackNode = inf.ReadInt32();
                DistanceTravelledM = inf.ReadSingle();

                float offsetDif = Math.Abs(TCOffset - tempPosition.TCOffset);
                if (TCSectionIndex != tempPosition.TCSectionIndex ||
                        (TCSectionIndex == tempPosition.TCSectionIndex && offsetDif > 5.0f))
                {
                    Trace.TraceWarning("Train {0} restored at different present rear : was {1}-{2}, is {3}-{4}",
                            train.Number, TCSectionIndex, tempPosition.TCSectionIndex,
                            TCOffset, tempPosition.TCOffset);
                }
            }


            public void RestorePreviousPosition(BinaryReader inf)
            {
                TCSectionIndex = inf.ReadInt32();
                TCDirection = inf.ReadInt32();
                TCOffset = inf.ReadSingle();
                RouteListIndex = inf.ReadInt32();
                TrackNode = inf.ReadInt32();
                DistanceTravelledM = inf.ReadSingle();
            }


            //================================================================================================//
            //
            // Restore dummies for trains not yet started
            //

            public void RestorePresentPositionDummy(BinaryReader inf, Train train)
            {
                TCSectionIndex = inf.ReadInt32();
                TCDirection = inf.ReadInt32();
                TCOffset = inf.ReadSingle();
                RouteListIndex = inf.ReadInt32();
                TrackNode = inf.ReadInt32();
                DistanceTravelledM = inf.ReadSingle();
            }


            public void RestorePresentRearDummy(BinaryReader inf, Train train)
            {
                TCSectionIndex = inf.ReadInt32();
                TCDirection = inf.ReadInt32();
                TCOffset = inf.ReadSingle();
                RouteListIndex = inf.ReadInt32();
                TrackNode = inf.ReadInt32();
                DistanceTravelledM = inf.ReadSingle();
            }


            public void RestorePreviousPositionDummy(BinaryReader inf)
            {
                TCSectionIndex = inf.ReadInt32();
                TCDirection = inf.ReadInt32();
                TCOffset = inf.ReadSingle();
                RouteListIndex = inf.ReadInt32();
                TrackNode = inf.ReadInt32();
                DistanceTravelledM = inf.ReadSingle();
            }

            //================================================================================================//
            //
            // Save
            //

            public void Save(BinaryWriter outf)
            {
                outf.Write(TCSectionIndex);
                outf.Write(TCDirection);
                outf.Write(TCOffset);
                outf.Write(RouteListIndex);
                outf.Write(TrackNode);
                outf.Write(DistanceTravelledM);
            }

            //================================================================================================//
            /// <summary>
            /// Copy TCPosition
            /// <\summary>

            public void CopyTo(ref TCPosition thisPosition)
            {
                thisPosition.TCSectionIndex = this.TCSectionIndex;
                thisPosition.TCDirection = this.TCDirection;
                thisPosition.TCOffset = this.TCOffset;
                thisPosition.RouteListIndex = this.RouteListIndex;
                thisPosition.TrackNode = this.TrackNode;
                thisPosition.DistanceTravelledM = this.DistanceTravelledM;
            }

            //================================================================================================//
            /// <summary>
            /// Reverse (or continue in same direction)
            /// <\summary>

            public void Reverse(TrackDirection oldDirection, TrackCircuitPartialPathRoute thisRoute, float offset)
            {
                RouteListIndex = thisRoute.GetRouteIndex(TCSectionIndex, 0);
                if (RouteListIndex >= 0)
                {
                    TCDirection = (int)thisRoute[RouteListIndex].Direction;
                }
                else
                {
                    TCDirection = TCDirection == 0 ? 1 : 0;
                }

                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[TCSectionIndex];
                if (oldDirection != (TrackDirection)TCDirection)
                    TCOffset = thisSection.Length - TCOffset; // actual reversal so adjust offset

                DistanceTravelledM = offset;
            }

            /// <summary>
            /// Set the position based on the trackcircuit section.
            /// </summary>
            /// <param name="trackCircuitXRefList">List of cross-references from tracknode to trackcircuitsection</param>
            /// <param name="offset">Offset along the tracknode</param>
            /// <param name="direction">direction along the tracknode (1 is forward)</param>
            public void SetTCPosition(TrackCircuitCrossReferences trackCircuitXRefList, float offset, int direction)
            {
                int XRefIndex = trackCircuitXRefList.GetCrossReferenceIndex(offset, direction);

                if (XRefIndex < 0) return;

                TrackCircuitSectionCrossReference thisReference = trackCircuitXRefList[XRefIndex];
                this.TCSectionIndex = thisReference.Index;
                this.TCDirection = direction;
                this.TCOffset = offset - thisReference.OffsetLength[direction];
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

        //================================================================================================//
        /// <summary>
        /// StationStop class
        /// Class to hold information on station stops
        /// <\summary>

        public class StationStop : IComparable<StationStop>
        {

            public enum STOPTYPE
            {
                STATION_STOP,
                SIDING_STOP,
                MANUAL_STOP,
                WAITING_POINT,
            }

            // common variables
            public STOPTYPE ActualStopType;

            public int PlatformReference;
            public PlatformDetails PlatformItem;
            public int SubrouteIndex;
            public int RouteIndex;
            public int TCSectionIndex;
            public int Direction;
            public int ExitSignal;
            public bool HoldSignal;
            public bool NoWaitSignal;
            public bool CallOnAllowed;
            public bool NoClaimAllowed;
            public float StopOffset;
            public float DistanceToTrainM;
            public int ArrivalTime;
            public int DepartTime;
            public int ActualArrival;
            public int ActualDepart;
            public DateTime arrivalDT;
            public DateTime departureDT;
            public bool Passed;

            // variables for activity mode only
            public const int NumSecPerPass = 10; // number of seconds to board of a passengers
            public const int DefaultFreightStopTime = 20; // MSTS stoptime for freight trains

            // variables for timetable mode only
            public bool Terminal;                                                                 // station is terminal - train will run to end of platform
            public int? ActualMinStopTime;                                                        // actual minimum stop time
            public float? KeepClearFront = null;                                                  // distance to be kept clear ahead of train
            public float? KeepClearRear = null;                                                   // distance to be kept clear behind train
            public bool ForcePosition = false;                                                    // front or rear clear position must be forced
            public bool CloseupSignal = false;                                                    // train may close up to signal within normal clearing distance
            public bool Closeup = false;                                                          // train may close up to other train in platform
            public bool RestrictPlatformToSignal = false;                                         // restrict end of platform to signal position
            public bool ExtendPlatformToSignal = false;                                           // extend end of platform to next signal position
            public bool EndStop = false;                                                          // train terminates at station
            public List<int> ConnectionsWaiting = new List<int>();                                // List of trains waiting
            public Dictionary<int, int> ConnectionsAwaited = new Dictionary<int, int>();          // List of awaited trains : key = trainno., value = arr time
            public Dictionary<int, WaitInfo> ConnectionDetails = new Dictionary<int, WaitInfo>(); // Details of connection : key = trainno., value = wait info

            //================================================================================================//
            //
            // Constructor
            //

            public StationStop(int platformReference, PlatformDetails platformItem, int subrouteIndex, int routeIndex,
                int tcSectionIndex, int direction, int exitSignal, bool holdSignal, bool noWaitSignal, bool noClaimAllowed, float stopOffset,
                int arrivalTime, int departTime, bool terminal, int? actualMinStopTime, float? keepClearFront, float? keepClearRear,
                bool forcePosition, bool closeupSignal, bool closeup,
                bool restrictPlatformToSignal, bool extendPlatformToSignal, bool endStop, STOPTYPE actualStopType)
            {
                ActualStopType = actualStopType;
                PlatformReference = platformReference;
                PlatformItem = platformItem;
                SubrouteIndex = subrouteIndex;
                RouteIndex = routeIndex;
                TCSectionIndex = tcSectionIndex;
                Direction = direction;
                ExitSignal = exitSignal;
                HoldSignal = holdSignal;
                NoWaitSignal = noWaitSignal;
                NoClaimAllowed = noClaimAllowed;
                StopOffset = stopOffset;
                if (actualStopType == STOPTYPE.STATION_STOP)
                {
                    ArrivalTime = Math.Max(0, arrivalTime);
                    DepartTime = Math.Max(0, departTime);
                }
                else
                // times may be <0 for waiting point
                {
                    ArrivalTime = arrivalTime;
                    DepartTime = departTime;
                }
                ActualArrival = -1;
                ActualDepart = -1;
                DistanceToTrainM = 9999999f;
                Passed = false;

                Terminal = terminal;
                ActualMinStopTime = actualMinStopTime;
                KeepClearFront = keepClearFront;
                KeepClearRear = keepClearRear;
                ForcePosition = forcePosition;
                CloseupSignal = closeupSignal;
                Closeup = closeup;
                RestrictPlatformToSignal = restrictPlatformToSignal;
                ExtendPlatformToSignal = extendPlatformToSignal;
                EndStop = endStop;

                CallOnAllowed = false;
            }

            //================================================================================================//
            //
            // Constructor to create empty item (used for passing variables only)
            //

            public StationStop()
            {
            }

            //================================================================================================//
            //
            // Restore
            //

            public StationStop(BinaryReader inf)
            {
                ActualStopType = (STOPTYPE)inf.ReadInt32();
                PlatformReference = inf.ReadInt32();

                if (PlatformReference >= 0)
                {
                    int platformIndex;
                    if (Simulator.Instance.SignalEnvironment.PlatformXRefList.TryGetValue(PlatformReference, out platformIndex))
                    {
                        PlatformItem = Simulator.Instance.SignalEnvironment.PlatformDetailsList[platformIndex];
                    }
                    else
                    {
                        Trace.TraceInformation("Cannot find platform {0}", PlatformReference);
                    }
                }
                else
                {
                    PlatformItem = null;
                }

                SubrouteIndex = inf.ReadInt32();
                RouteIndex = inf.ReadInt32();
                TCSectionIndex = inf.ReadInt32();
                Direction = inf.ReadInt32();
                ExitSignal = inf.ReadInt32();
                HoldSignal = inf.ReadBoolean();
                NoWaitSignal = inf.ReadBoolean();
                NoClaimAllowed = inf.ReadBoolean();
                CallOnAllowed = inf.ReadBoolean();
                StopOffset = inf.ReadSingle();
                ArrivalTime = inf.ReadInt32();
                DepartTime = inf.ReadInt32();
                ActualArrival = inf.ReadInt32();
                ActualDepart = inf.ReadInt32();
                DistanceToTrainM = 9999999f;
                Passed = inf.ReadBoolean();
                arrivalDT = new DateTime(inf.ReadInt64());
                departureDT = new DateTime(inf.ReadInt64());

                ConnectionsWaiting = new List<int>();
                int totalConWait = inf.ReadInt32();
                for (int iCW = 0; iCW <= totalConWait - 1; iCW++)
                {
                    ConnectionsWaiting.Add(inf.ReadInt32());
                }

                ConnectionsAwaited = new Dictionary<int, int>();
                int totalConAwait = inf.ReadInt32();
                for (int iCA = 0; iCA <= totalConAwait - 1; iCA++)
                {
                    ConnectionsAwaited.Add(inf.ReadInt32(), inf.ReadInt32());
                }

                ConnectionDetails = new Dictionary<int, WaitInfo>();
                int totalConDetails = inf.ReadInt32();
                for (int iCD = 0; iCD <= totalConDetails - 1; iCD++)
                {
                    ConnectionDetails.Add(inf.ReadInt32(), new WaitInfo(inf));
                }

                if (inf.ReadBoolean())
                {
                    ActualMinStopTime = inf.ReadInt32();
                }
                else
                {
                    ActualMinStopTime = null;
                }

                if (inf.ReadBoolean())
                {
                    KeepClearFront = inf.ReadSingle();
                }
                else
                {
                    KeepClearFront = null;
                }

                if (inf.ReadBoolean())
                {
                    KeepClearRear = inf.ReadSingle();
                }
                else
                {
                    KeepClearRear = null;
                }

                Terminal = inf.ReadBoolean();
                ForcePosition = inf.ReadBoolean();
                CloseupSignal = inf.ReadBoolean();
                Closeup = inf.ReadBoolean();
                RestrictPlatformToSignal = inf.ReadBoolean();
                ExtendPlatformToSignal = inf.ReadBoolean();
                EndStop = inf.ReadBoolean();
            }

            //================================================================================================//
            //
            // Compare To (to allow sort)
            //

            public int CompareTo(StationStop otherStop)
            {
                if (this.SubrouteIndex < otherStop.SubrouteIndex)
                {
                    return -1;
                }
                else if (this.SubrouteIndex > otherStop.SubrouteIndex)
                {
                    return 1;
                }
                else if (this.RouteIndex < otherStop.RouteIndex)
                {
                    return -1;
                }
                else if (this.RouteIndex > otherStop.RouteIndex)
                {
                    return 1;
                }
                else if (this.StopOffset < otherStop.StopOffset)
                {
                    return -1;
                }
                else if (this.StopOffset > otherStop.StopOffset)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }

            //================================================================================================//
            //
            // Save
            //

            public void Save(BinaryWriter outf)
            {
                outf.Write((int)ActualStopType);
                outf.Write(PlatformReference);
                outf.Write(SubrouteIndex);
                outf.Write(RouteIndex);
                outf.Write(TCSectionIndex);
                outf.Write(Direction);
                outf.Write(ExitSignal);
                outf.Write(HoldSignal);
                outf.Write(NoWaitSignal);
                outf.Write(NoClaimAllowed);
                outf.Write(CallOnAllowed);
                outf.Write(StopOffset);
                outf.Write(ArrivalTime);
                outf.Write(DepartTime);
                outf.Write(ActualArrival);
                outf.Write(ActualDepart);
                outf.Write(Passed);
                outf.Write((Int64)arrivalDT.Ticks);
                outf.Write((Int64)departureDT.Ticks);

                outf.Write(ConnectionsWaiting.Count);
                foreach (int iWait in ConnectionsWaiting)
                {
                    outf.Write(iWait);
                }

                outf.Write(ConnectionsAwaited.Count);
                foreach (KeyValuePair<int, int> thisAwait in ConnectionsAwaited)
                {
                    outf.Write(thisAwait.Key);
                    outf.Write(thisAwait.Value);
                }

                outf.Write(ConnectionDetails.Count);
                foreach (KeyValuePair<int, WaitInfo> thisDetails in ConnectionDetails)
                {
                    outf.Write(thisDetails.Key);
                    WaitInfo thisWait = (WaitInfo)thisDetails.Value;
                    thisWait.Save(outf);
                }

                if (ActualMinStopTime.HasValue)
                {
                    outf.Write(true);
                    outf.Write(ActualMinStopTime.Value);
                }
                else
                {
                    outf.Write(false);
                }

                if (KeepClearFront.HasValue)
                {
                    outf.Write(true);
                    outf.Write(KeepClearFront.Value);
                }
                else
                {
                    outf.Write(false);
                }
                if (KeepClearRear.HasValue)
                {
                    outf.Write(true);
                    outf.Write(KeepClearRear.Value);
                }
                else
                {
                    outf.Write(false);
                }

                outf.Write(Terminal);
                outf.Write(ForcePosition);
                outf.Write(CloseupSignal);
                outf.Write(Closeup);
                outf.Write(RestrictPlatformToSignal);
                outf.Write(ExtendPlatformToSignal);
                outf.Write(EndStop);
            }

            /// <summary>
            ///  create copy
            /// </summary>
            /// <returns></returns>
            public StationStop CreateCopy()
            {
                return ((StationStop)this.MemberwiseClone());
            }

            /// <summary>
            /// Calculate actual depart time
            /// Make special checks for stops arount midnight
            /// </summary>
            /// <param name="presentTime"></param>

            public int CalculateDepartTime(int presentTime, Train stoppedTrain)
            {
                int eightHundredHours = 8 * 3600;
                int sixteenHundredHours = 16 * 3600;

                // preset depart to booked time
                ActualDepart = DepartTime;

                // correct arrival for stop around midnight
                if (ActualArrival < eightHundredHours && ArrivalTime > sixteenHundredHours) // arrived after midnight, expected to arrive before
                {
                    ActualArrival += (24 * 3600);
                }
                else if (ActualArrival > sixteenHundredHours && ArrivalTime < eightHundredHours) // arrived before midnight, expected to arrive before
                {
                    ActualArrival -= (24 * 3600);
                }

                // correct stop time for stop around midnight
                int stopTime = DepartTime - ArrivalTime;
                if (DepartTime < eightHundredHours && ArrivalTime > sixteenHundredHours) // stop over midnight
                {
                    stopTime += (24 * 3600);
                }

                // compute boarding time (depends on train type)
                var validSched = stoppedTrain.ComputeTrainBoardingTime(this, ref stopTime);

                // correct departure time for stop around midnight
                int correctedTime = ActualArrival + stopTime;
                if (validSched)
                {
                    ActualDepart = Time.Compare.Latest(DepartTime, correctedTime);
                }
                else
                {
                    ActualDepart = correctedTime;
                    if (ActualDepart < 0)
                    {
                        ActualDepart += (24 * 3600);
                        ActualArrival += (24 * 3600);
                    }
                }
                if (ActualDepart != correctedTime)
                {
                    stopTime += ActualDepart - correctedTime;
                    if (stopTime > 24 * 3600) stopTime -= 24 * 3600;
                    else if (stopTime < 0) stopTime += 24 * 3600;

                }
                return stopTime;
            }

            //================================================================================================//
            /// <summary>
            /// <CScomment> Compute boarding time for passenger train. Solution based on number of carriages within platform.
            /// Number of carriages computed considering an average Traincar length...
            /// ...moreover considering that carriages are in the train part within platform (MSTS apparently does so).
            /// Player train has more sophisticated computing, as in MSTS.
            /// As of now the position of the carriages within the train is computed here at every station together with the statement if the carriage is within the 
            /// platform boundaries. To be evaluated if the position of the carriages within the train could later be computed together with the CheckFreight method</CScomment>
            /// <\summary>
            public int ComputeStationBoardingTime(Train stopTrain)
            {
                var passengerCarsWithinPlatform = stopTrain.PassengerCarsNumber;
                int stopTime = DefaultFreightStopTime;
                if (passengerCarsWithinPlatform == 0) return stopTime; // pure freight train
                var distancePlatformHeadtoTrainHead = -stopTrain.StationStops[0].StopOffset
                   + PlatformItem.TrackCircuitOffset[Location.FarEnd, (TrackDirection)stopTrain.StationStops[0].Direction]
                   + stopTrain.StationStops[0].DistanceToTrainM;
                var trainPartOutsidePlatformForward = distancePlatformHeadtoTrainHead < 0 ? -distancePlatformHeadtoTrainHead : 0;
                if (trainPartOutsidePlatformForward >= stopTrain.Length) return (int)PlatformItem.MinWaitingTime; // train actually passed platform; should not happen
                var distancePlatformTailtoTrainTail = distancePlatformHeadtoTrainHead - PlatformItem.Length + stopTrain.Length;
                var trainPartOutsidePlatformBackward = distancePlatformTailtoTrainTail > 0 ? distancePlatformTailtoTrainTail : 0;
                if (trainPartOutsidePlatformBackward >= stopTrain.Length) return (int)PlatformItem.MinWaitingTime; // train actually stopped before platform; should not happen
                if (stopTrain == stopTrain.Simulator.OriginalPlayerTrain)
                {
                    if (trainPartOutsidePlatformForward == 0 && trainPartOutsidePlatformBackward == 0) passengerCarsWithinPlatform = stopTrain.PassengerCarsNumber;
                    else
                    {
                        if (trainPartOutsidePlatformForward > 0)
                        {
                            var walkingDistance = 0.0f;
                            int trainCarIndex = 0;
                            while (walkingDistance <= trainPartOutsidePlatformForward && passengerCarsWithinPlatform > 0 && trainCarIndex < stopTrain.Cars.Count - 1)
                            {
                                var walkingDistanceBehind = walkingDistance + stopTrain.Cars[trainCarIndex].CarLengthM;
                                if ((stopTrain.Cars[trainCarIndex].WagonType != TrainCar.WagonTypes.Freight && stopTrain.Cars[trainCarIndex].WagonType != TrainCar.WagonTypes.Tender && !stopTrain.Cars[trainCarIndex].IsDriveable) ||
                                   (stopTrain.Cars[trainCarIndex].IsDriveable && stopTrain.Cars[trainCarIndex].HasPassengerCapacity))
                                {
                                    if ((trainPartOutsidePlatformForward - walkingDistance) > 0.67 * stopTrain.Cars[trainCarIndex].CarLengthM) passengerCarsWithinPlatform--;
                                }
                                walkingDistance = walkingDistanceBehind;
                                trainCarIndex++;
                            }
                        }
                        if (trainPartOutsidePlatformBackward > 0 && passengerCarsWithinPlatform > 0)
                        {
                            var walkingDistance = 0.0f;
                            int trainCarIndex = stopTrain.Cars.Count - 1;
                            while (walkingDistance <= trainPartOutsidePlatformBackward && passengerCarsWithinPlatform > 0 && trainCarIndex >= 0)
                            {
                                var walkingDistanceBehind = walkingDistance + stopTrain.Cars[trainCarIndex].CarLengthM;
                                if ((stopTrain.Cars[trainCarIndex].WagonType != TrainCar.WagonTypes.Freight && stopTrain.Cars[trainCarIndex].WagonType != TrainCar.WagonTypes.Tender && !stopTrain.Cars[trainCarIndex].IsDriveable) ||
                                   (stopTrain.Cars[trainCarIndex].IsDriveable && stopTrain.Cars[trainCarIndex].HasPassengerCapacity))
                                {
                                    if ((trainPartOutsidePlatformBackward - walkingDistance) > 0.67 * stopTrain.Cars[trainCarIndex].CarLengthM) passengerCarsWithinPlatform--;
                                }
                                walkingDistance = walkingDistanceBehind;
                                trainCarIndex--;
                            }
                        }
                    }
                }
                else
                {

                    passengerCarsWithinPlatform = stopTrain.Length - trainPartOutsidePlatformForward - trainPartOutsidePlatformBackward > 0 ?
                    stopTrain.PassengerCarsNumber : (int)Math.Min((stopTrain.Length - trainPartOutsidePlatformForward - trainPartOutsidePlatformBackward) / stopTrain.Cars.Count() + 0.33,
                    stopTrain.PassengerCarsNumber);
                }
                if (passengerCarsWithinPlatform > 0)
                {
                    var actualNumPassengersWaiting = PlatformItem.NumPassengersWaiting;
                    if (stopTrain.TrainType != TrainType.AiPlayerHosting) RandomizePassengersWaiting(ref actualNumPassengersWaiting, stopTrain);
                    stopTime = Math.Max(NumSecPerPass * actualNumPassengersWaiting / passengerCarsWithinPlatform, DefaultFreightStopTime);
                }
                else stopTime = 0; // no passenger car stopped within platform: sorry, no countdown starts
                return stopTime;
            }

            //================================================================================================//
            /// <summary>
            /// CheckScheduleValidity
            /// Quite frequently in MSTS activities AI trains have invalid values (often near midnight), because MSTS does not consider them anyway
            /// As OR considers them, it is wise to discard the least credible values, to avoid AI trains stopping for hours
            /// </summary>
            public bool CheckScheduleValidity(Train stopTrain)
            {
                if (stopTrain.TrainType != TrainType.Ai) return true;
                if (ArrivalTime == DepartTime && Math.Abs(ArrivalTime - ActualArrival) > 14400) return false;
                else return true;
            }

            //================================================================================================//
            /// <summary>
            /// RandomizePassengersWaiting
            /// Randomizes number of passengers waiting for train, and therefore boarding time
            /// Randomization can be upwards or downwards
            /// </summary>

            private void RandomizePassengersWaiting(ref int actualNumPassengersWaiting, Train stopTrain)
            {
                if (stopTrain.Simulator.Settings.ActRandomizationLevel > 0)
                {
                    var randms = DateTime.UtcNow.Millisecond % 10;
                    if (randms >= 6 - stopTrain.Simulator.Settings.ActRandomizationLevel)
                    {
                        if (randms < 8)
                        {
                            actualNumPassengersWaiting += stopTrain.RandomizedDelay(2 * PlatformItem.NumPassengersWaiting *
                                stopTrain.Simulator.Settings.ActRandomizationLevel); // real passenger number may be up to 3 times the standard.
                        }
                        else
                        // less passengers than standard
                        {
                            actualNumPassengersWaiting -= stopTrain.RandomizedDelay(PlatformItem.NumPassengersWaiting *
                                stopTrain.Simulator.Settings.ActRandomizationLevel / 6);
                        }
                    }
                }
            }

        }

        //used by remote train to update location based on message received
        public int expectedTileX, expectedTileZ, expectedTracIndex, expectedDIr, expectedTDir;
        public float expectedX, expectedZ, expectedTravelled, expectedLength;
        public bool updateMSGReceived;
        public bool jumpRequested; // set when a train jump has been requested by the server (when player re-enters game in old position
        public bool doJump; // used in conjunction with above flag to manage thread safety
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
                                    t = new Traveller(Simulator.TSectionDat, Simulator.TDB.TrackDB.TrackNodes, new WorldLocation(expectedTileX, expectedTileZ, expectedX, 0, expectedZ), (Traveller.TravellerDirection)expectedTDir);
                                }
                                else
                                {
                                    t = new Traveller(Simulator.TSectionDat, Simulator.TDB.TrackDB.TrackNodes, Simulator.TDB.TrackDB.TrackNodes[expectedTracIndex] as TrackVectorNode, new WorldLocation(expectedTileX, expectedTileZ, expectedX, 0, expectedZ), (Traveller.TravellerDirection)expectedTDir);
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
                        if (jumpRequested)
                        {
                            doJump = true;
                            jumpRequested = false;
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
                int listIndex = PresentPosition[0].RouteListIndex;
                signalRef.BreakDownRouteList(ValidRoute[0], listIndex, routedForward);
                ClearDeadlocks();
            }

            ValidRoute[0] = null;
            LastReservedSection[0] = -1;

            if (ValidRoute[1] != null)
            {
                int listIndex = PresentPosition[1].RouteListIndex;
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
                int listIndex = PresentPosition[0].RouteListIndex;
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
            FrontTDBTraveller = new Traveller(Simulator.TSectionDat, Simulator.TDB.TrackDB.TrackNodes, Simulator.TDB.TrackDB.TrackNodes[trackNodeIndex],
                 Cars[0].WorldPosition.TileX, Cars[0].WorldPosition.TileZ, finalFrontTravellerXNALocation.X, -finalFrontTravellerXNALocation.Z, FrontTDBTraveller.Direction);
            RearTDBTraveller = new Traveller(Simulator.TSectionDat, Simulator.TDB.TrackDB.TrackNodes, Simulator.TDB.TrackDB.TrackNodes[trackNodeIndex],
                Cars[0].WorldPosition.TileX, Cars[0].WorldPosition.TileZ, finalRearTravellerXNALocation.X, -finalRearTravellerXNALocation.Z, RearTDBTraveller.Direction);
            if (direction == Traveller.TravellerDirection.Backward)
            {
                FrontTDBTraveller.ReverseDirection();
                RearTDBTraveller.ReverseDirection();
            }

            ClearValidRoutes();
            bool canPlace = true;
            PresentPosition[0].TCSectionIndex = -1;
            TrackCircuitPartialPathRoute tempRoute = CalculateInitialTrainPosition(ref canPlace);
            if (tempRoute.Count == 0 || !canPlace)
            {
                throw new InvalidDataException("Position of train in turntable not clear");
            }

            SetInitialTrainRoute(tempRoute);
            CalculatePositionOfCars();
            ResetInitialTrainRoute(tempRoute);

            CalculatePositionOfCars();

            TrackNode tn = FrontTDBTraveller.TN;
            float offset = FrontTDBTraveller.TrackNodeOffset;
            int direction1 = (int)FrontTDBTraveller.Direction;

            PresentPosition[0].SetTCPosition(tn.TrackCircuitCrossReferences, offset, direction1);
            PresentPosition[0].CopyTo(ref PreviousPosition[0]);

            if (TrainType == TrainType.Static)
            {
                ControlMode = TrainControlMode.Undefined;
                return;
            }

            if (Simulator.Activity == null && !Simulator.TimetableMode) ToggleToExplorerMode();
            else ToggleToManualMode();
            Simulator.Confirmer.Confirm(CabControl.SignalMode, CabSetting.Off);
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
        public static bool IsAheadOfTrain(TrackCircuitSection section, Train.TCPosition position)
        {
            return IsAheadOfTrain(section, 0f, position);
        }

        // with offset
        public static bool IsAheadOfTrain(TrackCircuitSection section, float offset, Train.TCPosition position)
        {
            if (null == section)
                throw new ArgumentNullException(nameof(section));
            if (null == position)
                throw new ArgumentNullException(nameof(position));

            float distanceAhead = TrackCircuitSection.GetDistanceBetweenObjects(
                position.TCSectionIndex, position.TCOffset, (TrackDirection)position.TCDirection, section.Index, offset);
            return (distanceAhead > 0.0f);
        }

    }// class Train
}
