// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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

// Define this to log the wheel configurations on cars as they are loaded.
//#define DEBUG_WHEELS

// Debug car heat losses
// #define DEBUG_CAR_HEATLOSS

// Debug curve speed
// #define DEBUG_CURVE_SPEED

//Debug Tunnel Resistance
//   #define DEBUG_TUNNEL_RESISTANCE

// Debug User SuperElevation
//#define DEBUG_USER_SUPERELEVATION

// Debug Brake Slide Calculations
//#define DEBUG_BRAKE_SLIDE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.DebugInfo;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Scripting.Api.PowerSupply;
using Orts.Simulation.Activities;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks.SubSystems;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using Orts.Simulation.Signalling;
using Orts.Simulation.Timetables;
using Orts.Simulation.Track;

using static Orts.Common.Calc.Dynamics;

namespace Orts.Simulation.RollingStocks
{

    public abstract class TrainCar : IWorldPosition
    {
        #region const
        // Input values to allow the temperature for different values of latitude to be calculated
        private static readonly double[] WorldLatitudeDeg = new double[] { -50.0f, -40.0f, -30.0f, -20.0f, -10.0f, 0.0f, 10.0f, 20.0f, 30.0f, 40.0f, 50.0f, 60.0f };
        // Temperature in deg Celcius
        private static readonly double[] WorldTemperatureWinter = new double[] { 0.9f, 8.7f, 12.4f, 17.2f, 20.9f, 25.9f, 22.8f, 18.2f, 11.1f, 1.1f, -10.2f, -18.7f };
        private static readonly double[] WorldTemperatureAutumn = new double[] { 7.5f, 13.7f, 18.8f, 22.0f, 24.0f, 26.0f, 25.0f, 21.6f, 21.0f, 14.3f, 6.0f, 3.8f };
        private static readonly double[] WorldTemperatureSpring = new double[] { 8.5f, 13.1f, 17.6f, 18.6f, 24.6f, 25.9f, 26.8f, 23.4f, 18.5f, 12.6f, 6.1f, 1.7f };
        private static readonly double[] WorldTemperatureSummer = new double[] { 13.4f, 18.3f, 22.8f, 24.3f, 24.4f, 25.0f, 25.2f, 22.5f, 26.6f, 24.8f, 19.4f, 14.3f };
        private static readonly Interpolator WorldWinterLatitudetoTemperatureC = new Interpolator(WorldLatitudeDeg, WorldTemperatureWinter);
        private static readonly Interpolator WorldAutumnLatitudetoTemperatureC = new Interpolator(WorldLatitudeDeg, WorldTemperatureAutumn);
        private static readonly Interpolator WorldSpringLatitudetoTemperatureC = new Interpolator(WorldLatitudeDeg, WorldTemperatureSpring);
        private static readonly Interpolator WorldSummerLatitudetoTemperatureC = new Interpolator(WorldLatitudeDeg, WorldTemperatureSummer);

        // Input values to allow the water and fuel usage of steam heating boiler to be calculated based upon Spanner SwirlyFlo Mk111 Boiler
        private static readonly double[] SteamUsageLbpH = { 0.0, 3000.0 };
        // Water Usage
        private static readonly double[] WaterUsageGalukpH = { 0.0, 300.0 };
        // Fuel usage
        private static readonly double[] FuelUsageGalukpH = { 0.0, 31.0 };
        protected static readonly Interpolator SteamHeatBoilerWaterUsageGalukpH = new Interpolator(SteamUsageLbpH, WaterUsageGalukpH);
        protected static readonly Interpolator SteamHeatBoilerFuelUsageGalukpH = new Interpolator(SteamUsageLbpH, FuelUsageGalukpH);

        // Used to calculate Carriage Steam Heat Loss
        private const float BogieHeight = 1.06f; // Height reduced by 1.06m to allow for bogies, etc
        private const float CarCouplingPipeLength = 1.2f;  // Allow for connection between cars (assume 2' each end) - no heat is contributed to carriages.
        private const double LowSpeed = 2.0f;
        private const float DryLapseTemperatureC = 9.8f;
        private const float WetLapseTemperatureC = 5.5f;

        private static readonly double desiredCompartmentAlarmTempSetpointC = Temperature.Celsius.FromF(45.0); // Alarm temperature
                                                                                                               // Use Napier formula to calculate steam discharge rate through steam trap valve, ie Discharge (lb/s) = (Valve area * Abs Pressure) / 70
        private const double SteamTrapValveDischargeFactor = 70.0;
        private const double ConnectSteamHoseLengthFt = 2 * 2.0; // Assume two hoses on each car * 2 ft long
                                                                 // Find area of pipe - assume 0.1875" (3/16") dia steam trap
        private const double SteamTrapDiaIn = 0.1875f;
        // Use Napier formula to calculate steam discharge rate through steam leak in connecting hose, ie Discharge (lb/s) = (Valve area * Abs Pressure) / 70
        private const double ConnectingHoseDischargeFactor = 70.0f;
        // Find area of pipe - assume 0.1875" (3/16") dia steam trap
        private const double ConnectingHoseLeakDiaIn = 0.1875f;

        public const float SkidFriction = 0.08f; // Friction if wheel starts skidding - based upon wheel dynamic friction of approx 0.08
        #endregion

        private protected static readonly Simulator simulator = Simulator.Instance;
        #region source files
        public string WagFilePath { get; }
        public string RealWagFilePath { get; internal set; } //we are substituting missing remote cars in MP, so need to remember this
        // original consist of which car was part (used in timetable for couple/uncouple options)
        public string OriginalConsist { get; internal set; } = string.Empty;
        #endregion

        private bool dbfEvalsnappedbrakehose;//Debrief eval
        private static float dbfmaxsafecurvespeedmps;//Debrief eval
        private bool ldbfevaltrainoverturned;

        // sound related variables
        public bool IsPartOfActiveTrain { get; internal set; } = true;

        public IPowerSupply PowerSupply { get; protected set; }

        // Used to calculate Carriage Steam Heat Loss - ToDo - ctn_steamer - consolidate these parameters with other steam heat ones, also check as some now may be obsolete
        public Interpolator TrainHeatBoilerWaterUsageGalukpH { get; protected set; }
        public Interpolator TrainHeatBoilerFuelUsageGalukpH { get; protected set; }

        private protected double mainSteamHeatPipeOuterDiaM = Size.Length.FromIn(2.4); // Steel pipe OD = 1.9" + 0.5" insulation (0.25" either side of pipe)
        private protected double mainSteamHeatPipeInnerDiaM = Size.Length.FromIn(1.50); // Steel pipe ID = 1.5"
        private protected double carConnectSteamHoseOuterDiaM = Size.Length.FromIn(2.05); // Rubber hose OD = 2.05"
        private protected double carConnectSteamHoseInnerDiaM = Size.Length.FromIn(1.50); // Rubber hose ID = 1.5"
        private protected bool steamHeatBoilerLockedOut;
        private protected double maximumSteamHeatingBoilerSteamUsageRateLbpS;
        private protected double maximiumSteamHeatBoilerFuelTankCapacityL = 1500.0f; // Capacity of the fuel tank for the steam heating boiler
        private protected double currentCarSteamHeatBoilerWaterCapacityL;  // Current water level
        private protected double currentSteamHeatBoilerFuelCapacityL;  // Current fuel level - only on steam vans, diesels use main diesel tank
        private protected double maximumSteamHeatBoilerWaterTankCapacityL = Size.LiquidVolume.FromGallonUK(800.0f); // Capacity of the water feed tank for the steam heating boiler
        private protected double compartmentHeatingPipeAreaFactor = 3.0f;
        public double DesiredCompartmentTempSetpointC { get; private protected set; } = Temperature.Celsius.FromF(55.0f); // This is the desired temperature for the passenger compartment heating
        private protected double windowDeratingFactor = 0.275f;   // fraction of windows in carriage side - 27.5% of space are windows
        private protected bool steamHeatingBoilerOn;
        private protected bool steamHeatingCompartmentSteamTrapOn;
        private protected double totalCarCompartmentHeatLossW;      // Transmission loss for the wagon
        private protected double carHeatCompartmentPipeAreaM2;  // Area of surface of car pipe
        private protected bool carHeatingInitialized; // Allow steam heat to be initialised.
        private protected double carHeatSteamMainPipeHeatLossBTU;  // BTU /hr
        private protected double carHeatConnectSteamHoseHeatLossBTU;
        private protected double carSteamHeatMainPipeCurrentHeatBTU;
        internal double carSteamHeatMainPipeSteamPressurePSI;
        private protected double carCompartmentSteamPipeHeatConvW;
        private protected double carCompartmentSteamHeatPipeRadW;
        private protected bool carHeatCompartmentHeaterOn;
        private protected double carHeatSteamTrapUsageLBpS;
        private protected double carHeatConnectingSteamHoseLeakageLBpS;
        private protected double steamHoseLeakRateRandom;
        internal double carNetHeatFlowRateW;        // Net Steam loss - Loss in Cars vs Steam Pipe Heat
        internal double carHeatCompartmentSteamPipeHeatW; // Heat generated by steam exchange area in compartment
        private protected double carHeatCurrentCompartmentHeatJ;
        public double CarInsideTempC { get; private protected set; }

        // some properties of this car
        public float CarWidthM { get; protected set; } = 2.5f;
        public float CarLengthM { get; internal protected set; } = 40;       // derived classes must overwrite these defaults
        public float CarHeightM { get; protected set; } = 4;        // derived classes must overwrite these defaults
        public float MassKG { get; internal protected set; } = 10000;        // Mass in KG at runtime; coincides with InitialMassKG if there is no load and no ORTS freight anim
        public float InitialMassKG { get; protected set; } = 10000;
        public int PassengerCapacity { get; protected set; }
        public bool HasInsideView { get; protected set; }
        public float CarHeightAboveSeaLevel { get; set; }
        public float CarBogieCentreLength { get; protected set; }
        public float CarBodyLength { get; protected set; }
        public float CarCouplerFaceLength { get; protected set; }
        public float DerailmentCoefficient { get; private set; }
        private float nadalDerailmentCoefficient;
        private protected bool derailmentCoefficientEnabled = true;
        private protected float maximumWheelFlangeAngle;
        private protected float wheelFlangeLength;
        private protected float angleOfAttack;
        private protected float derailClimbDistance;
        public bool DerailPossible { get; protected set; }
        public bool DerailExpected { get; protected set; }
        private protected float derailElapsedTime;

        internal float MaxHandbrakeForceN;
        internal float MaxBrakeForceN = 89e3f;
        private protected float initialMaxHandbrakeForce;  // Initial force when Wagon initialised
        private protected float initialMaxBrakeForce = 89e3f;   // Initial force when Wagon initialised

        // Coupler Animation
        public ShapeAnimation FrontCouplerAnimation { get; private protected set; }
        public ShapeAnimation FrontCouplerOpenAnimation { get; private protected set; }
        public ShapeAnimation RearCouplerAnimation { get; private protected set; }
        public ShapeAnimation RearCouplerOpenAnimation { get; private protected set; }

        public bool FrontCouplerOpenFitted { get; private protected set; }
        public bool FrontCouplerOpen { get; internal protected set; }

        public bool RearCouplerOpenFitted { get; private protected set; }
        public bool RearCouplerOpen { get; internal protected set; }

        // Air hose animation
        public ShapeAnimation FrontAirHoseAnimation { get; private protected set; }
        public ShapeAnimation FrontAirHoseDisconnectedAnimation { get; private protected set; }
        public ShapeAnimation RearAirHoseAnimation { get; private protected set; }
        public ShapeAnimation RearAirHoseDisconnectedAnimation { get; private protected set; }

        public float FrontAirHoseHeightAdjustmentM { get; private set; }
        public float RearAirHoseHeightAdjustmentM { get; private set; }
        public float FrontAirHoseYAngleAdjustmentRad { get; private set; }
        public float FrontAirHoseZAngleAdjustmentRad { get; private set; }
        public float RearAirHoseYAngleAdjustmentRad { get; private set; }
        public float RearAirHoseZAngleAdjustmentRad { get; private set; }

        private protected float airHoseLengthM;
        private protected float airHoseHorizontalLengthM;

        public float CarHeatVolumeM3 { get => CarWidthM * (CarLengthM - CarCouplingPipeLength) * (CarHeightM - BogieHeight); } // Volume of car for heating purposes
        public float CarOutsideTempC { get; private set; }   // Ambient temperature outside of car
        private float initialCarOutsideTempC;

        public float CarHeightMinusBogie => CarHeightM - BogieHeight;
        // Used to calculate wheel sliding for locked brake
        internal bool WheelBrakeSlideProtectionFitted;
        internal bool WheelBrakeSlideProtectionActive;
        internal bool WheelBrakeSlideProtectionLimitDisabled;
        internal float WheelBrakeSlideTimerResetValueS = 7.0f; // Set wsp time to 7 secs
        internal float WheelBrakeSlideProtectionTimerS = 7.0f;
        internal bool WheelBrakeSlideProtectionDumpValveLockout;

        public bool BrakeSkid { get; private set; }
        public bool BrakeSkidWarning { get; private set; }
        public bool HUDBrakeSkid { get; internal set; }

        public float BrakeShoeCoefficientFriction { get; private set; } = 1.0f; // Brake Shoe coefficient - for simple adhesion model set to 1

        private float brakeShoeCoefficientFrictionAdjFactor = 1.0f; // Factor to adjust Brake force by - based upon changing friction coefficient with speed, will change when wheel goes into skid
        private float brakeShoeRetardCoefficientFrictionAdjFactor = 1.0f; // Factor of adjust Retard Brake force by - independent of skid
        private float defaultBrakeShoeCoefficientFriction;  // A default value of brake shoe friction is no user settings are present.
        private float brakeWheelTreadForceN; // The retarding force apparent on the tread of the wheel
        private float wagonBrakeAdhesiveForceN; // The adhesive force existing on the wheels of the wagon

        public float AuxTenderWaterMassKG { get; protected set; }    // Water mass in auxiliary tender
        public AuxWagonType AuxWagonType { get; protected set; }           // Store wagon type for use with auxilary tender calculations

        public Lights Lights { get; private protected set; }
        public FreightAnimations FreightAnimations { get; private protected set; }
        public int Headlight { get; set; }

        // instance variables set by train physics when it creates the traincar
        public Train Train { get; internal set; }  // the car is connected to this train

        public bool IsPlayerTrain => Train.IsPlayerDriven;
        public bool Flipped { get; internal set; } // the car is reversed in the consist
        public int UiD { get; internal set; }
        public string CarID { get; internal set; } = "AI"; //CarID = "0 - UID" if player train, "ActivityID - UID" if loose consist, "AI" if AI train

        private string wheelAxleInformation;
        public string WheelAxleInformation => wheelAxleInformation ??= GetWheelAxleInformation();

        // status of the traincar - set by the train physics after it calls TrainCar.Update()
        private WorldPosition worldPosition = WorldPosition.None;
        public ref readonly WorldPosition WorldPosition => ref worldPosition;  // current position of the car
        public float DistanceTravelled { get; internal set; }  // running total of distance travelled - always positive, updated by train physics
        private float prevSpeedMpS;
        public float AbsSpeedMpS { get; protected set; } // Math.Abs(SpeedMps) expression is repeated many times in the subclasses, maybe this deserves a class variable
        public float CouplerSlackM { get; internal set; }  // extra distance between cars (calculated based on relative speeds)
        public int HUDCouplerForceIndication { get; internal set; } // Flag to indicate whether coupler is 1 - pulling, 2 - pushing or 0 - neither
        internal float CouplerSlack2M;  // slack calculated using draft gear force
        internal bool avancedCoupler; // Flag to indicate that coupler is to be treated as an advanced coupler
        public float FrontCouplerSlackM { get; internal set; } // Slack in car front coupler
        public float RearCouplerSlackM { get; internal set; }  // Slack in rear coupler
        public TrainCar CarAhead { get; internal set; }
        public TrainCar CarBehind { get; internal set; }
        public Vector3 RearCouplerLocation { get; set; }
        internal float AdvancedCouplerDynamicTensionSlackLimitM;   // Varies as coupler moves
        internal float AdvancedCouplerDynamicCompressionSlackLimitM; // Varies as coupler moves

        public bool WheelSlip { get; protected set; }  // true if locomotive wheels slipping
        public bool WheelSlipWarning { get; protected set; }

        public float WheelBearingTemperatureDegC { get; protected set; } = 40.0f;
        public string DisplayWheelBearingTemperatureStatus { get; protected set; }

        private readonly IIRFilter accelerationFilter = new IIRFilter(IIRFilterType.Butterworth, 1, 1.0f, 0.1f);

        private protected float WheelBearingTemperatureRiseTimeS;
        private protected float hotBoxTemperatureRiseTimeS;
        private protected float wheelBearingTemperatureDeclineTimeS;
        private protected float initialWheelBearingDeclineTemperatureDegC;
        private protected float initialWheelBearingRiseTemperatureDegC;
        private protected float initialHotBoxRiseTemperatureDegS;
        private protected bool wheelBearingFailed;
        private protected bool wheelBearingHot;
        private protected bool hotBoxActivated;
        private protected bool hotBoxHasBeenInitialized;
        private protected bool hotBoxSoundActivated;
        private protected double activityElapsedDuration;
        private protected float hotBoxStartTime;

        // Setup for ambient temperature dependency
        private bool ambientTemperatureInitialised;
        private float prevElev = -100f;

        #region INameValueInformationProvider implementation
        private protected readonly TrainCarInformation carInfo;
        private readonly TrainCarForceInformation forceInfo;
        private readonly TrainCarPowerSupplyInfo powerInfo;

        public DetailInfoBase CarInfo => carInfo;

        public DetailInfoBase ForceInfo => forceInfo;
        public DetailInfoBase PowerSupplyInfo => powerInfo;
        #endregion

        /// <summary>
        /// Indicates which remote control group the car is in.
        /// -1: unconnected, 0: sync/front group, 1: async/rear group
        /// </summary>
        public RemoteControlGroup RemoteControlGroup { get; internal protected set; }

        public float SpeedMpS { get; set; }// meters per second; updated by train physics, relative to direction of car  50mph = 22MpS

        public float AccelerationMpSS { get; private set; }

        public float LocalThrottlePercent { get; internal protected set; }
        // represents the MU line travelling through the train.  Uncontrolled locos respond to these commands.
        public float ThrottlePercent
        {
            get
            {
                if (RemoteControlGroup == RemoteControlGroup.FrontGroupSync && Train != null)
                {
                    if (Train.LeadLocomotive is MSTSLocomotive locomotive)
                    {
                        if (!locomotive.TrainControlSystem.TractionAuthorization || Train.MUThrottlePercent <= 0)
                            return 0;
                        else if (Train.MUThrottlePercent > locomotive.TrainControlSystem.MaxThrottlePercent)
                            return Math.Max(locomotive.TrainControlSystem.MaxThrottlePercent, 0);
                    }
                    return Train.MUThrottlePercent;
                }
                else if (RemoteControlGroup == RemoteControlGroup.RearGroupAsync && Train != null)
                    return Train.DPThrottlePercent;
                else
                    return LocalThrottlePercent;
            }
            set
            {
                if (RemoteControlGroup == RemoteControlGroup.FrontGroupSync && Train != null)
                    Train.MUThrottlePercent = value;
                else
                    LocalThrottlePercent = value;
            }
        }

        private int localGearboxGearIndex;

        public int GearboxGearIndex
        {
            get
            {
                if (RemoteControlGroup != RemoteControlGroup.Unconnected)
                    return Train.MUGearboxGearIndex;
                else
                    return localGearboxGearIndex;
            }
            set
            {
                if (RemoteControlGroup != RemoteControlGroup.Unconnected)
                    Train.MUGearboxGearIndex = value;
                else
                    localGearboxGearIndex = value;
            }
        }

        public float LocalDynamicBrakePercent { get; protected set; } = -1;
        public float DynamicBrakePercent
        {
            get
            {
                if (RemoteControlGroup != RemoteControlGroup.Unconnected && Train != null)
                {
                    if (Train.LeadLocomotive is MSTSLocomotive locomotive)
                    {
                        if (locomotive.TrainControlSystem.FullDynamicBrakingOrder)
                        {
                            return 100;
                        }
                    }
                    return Train.MUDynamicBrakePercent;
                }
                else if (RemoteControlGroup == RemoteControlGroup.RearGroupAsync && Train != null)
                    return Train.DPDynamicBrakePercent;
                else
                    return LocalDynamicBrakePercent;
            }
            set
            {
                if (RemoteControlGroup != RemoteControlGroup.Unconnected && Train != null)
                    Train.MUDynamicBrakePercent = value;
                else
                    LocalDynamicBrakePercent = value;
                if (Train != null && this == Train.LeadLocomotive)
                    LocalDynamicBrakePercent = value;
            }
        }

        public virtual MidpointDirection Direction => Flipped ? (MidpointDirection)((int)Train.MUDirection * -1) : Train.MUDirection;

        public BrakeSystem BrakeSystem { get; protected set; }
        public BrakeSystemType BrakeSystemType { get; internal protected set; }

        public float PreviousSteamBrakeCylinderPressurePSI { get; internal protected set; }

        // TrainCar.Update() must set these variables
        public float MotiveForceN { get; internal protected set; }   // ie motor power in Newtons  - signed relative to direction of car -
        public float TractiveForceN { get; internal protected set; } // Raw tractive force for electric sound variable2
        private protected SmoothedData MotiveForceSmoothedN = new SmoothedData(0.5f);
        public float PrevMotiveForceN { get; internal protected set; }
        // Gravity forces have negative values on rising grade. 
        // This means they have the same sense as the motive forces and will push the train downhill.
        public float GravityForceN { get; protected set; }  // Newtons  - signed relative to direction of car.
        public float CurveForceN { get; protected set; }   // Resistive force due to curve, in Newtons
        public float WindForceN { get; protected set; }  // Resistive force due to wind
        public float DynamicBrakeForceN { get; protected set; } // Raw dynamic brake force for diesel and electric locomotives

        // Derailment variables
        private protected double totalWagonVerticalDerailForce; // Vertical force of wagon/car - essentially determined by the weight
        private protected double totalWagonLateralDerailForce;
        private protected double lateralWindForce;
        private protected double wagonFrontCouplerAngle;
        private protected double wagonFrontCouplerBuffAngle;
        private protected double wagonRearCouplerAngle;
        private protected double wagonRearCouplerBuffAngle;
        private protected double CarTrackPlayM = Size.Length.FromIn(2.0);
        private protected double wagonCouplerAngleDerail;

        private bool curveSpeedDependent;

        // temporary values used to compute coupler forces
        internal float CouplerForceA; // left hand side value below diagonal
        internal float CouplerForceB; // left hand side value on diagonal
        internal float CouplerForceC; // left hand side value above diagonal
        internal float CouplerForceG; // temporary value used by solver
        internal float CouplerForceR; // right hand side value
        internal float ImpulseCouplerForceUN;
        internal SmoothedData CouplerForceUSmoothed = new SmoothedData(1.0f);
        internal float PreviousCouplerSlackM;
        internal float SmoothedCouplerForceUN;

        // Used by Curve Speed Method
        private protected float trackGauge = 1.435f;  // Track gauge - read in MSTSWagon
        private protected Vector3 initialCentreOfGravityM = new Vector3(0, 1.8f, 0); // get centre of gravity - read in MSTSWagon
        private protected Vector3 centreOfGravityM = new Vector3(0, 1.8f, 0); // get centre of gravity after adjusted for freight animation
        private protected float superelevation; // Super elevation on the curve
        private protected float unbalancedSuperElevation;  // Unbalanced superelevation, read from MSTS Wagon File
        private protected float superElevationTotal; // Total superelevation
        private protected float superElevationAngle;
        private protected bool maxSafeCurveSpeedReached; // Has equal loading speed around the curve been exceeded, ie are all the wheesl still on the track?
        private protected bool criticalMaxSpeedReached; // Has the critical maximum speed around the curve been reached, is the wagon about to overturn?
        private protected bool criticalMinSpeedReached; // Is the speed less then the minimum required for the wagon to travel around the curve
        private protected float maxCurveEqualLoadSpeed; // Max speed that rolling stock can do whist maintaining equal load on track
        private protected float startCurveResistanceFactor = 2.0f; // Set curve friction at Start = 200%
        private protected const float gravitationalAcceleration = 9.80665f; // Acceleration due to gravity 9.80665 m/s2
        private protected int wagonNumAxles; // Number of axles on a wagon
        private protected float wagonNumWheels; // Number of axles on a wagon - used to read MSTS value as default
        private protected int locoNumDrvAxles; // Number of drive axles on locomotive
        private protected float locoNumDrvWheels; // Number of drive axles on locomotive - used to read MSTS value as default

        private protected float curveResistanceZeroSpeedFactor = 0.5f; // Based upon research (Russian experiments - 1960) the older formula might be about 2x actual value
        private protected float rigidWheelBaseM;   // Vehicle rigid wheelbase, read from MSTS Wagon file

        // filter curve force for audio to prevent rapid changes.
        private readonly SmoothedData curveForceFilter = new SmoothedData(0.75f);

        public double CurveForceFiltered => curveForceFilter.SmoothedValue;

        public float TunnelForceN { get; protected set; }  // Resistive force due to tunnel, in Newtons
        public float FrictionForceN { get; protected set; } // in Newtons ( kg.m/s^2 ) unsigned, includes effects of curvature
        public float BrakeForceN { get; internal protected set; }    // brake force applied to slow train (Newtons) - will be impacted by wheel/rail friction
        public float BrakeRetardForceN { get; internal protected set; }    // brake force applied to wheel by brakeshoe (Newtons) independent of friction wheel/rail friction

        public float AdjustedWagonFrontCouplerAngle { get; protected set; }
        public float AdjustedWagonRearCouplerAngle { get; protected set; }
        public float WagonFrontCouplerCurveExtM { get; protected set; }
        public float WagonRearCouplerCurveExtM { get; protected set; }

        // Sum of all the forces acting on a Traincar in the direction of driving.
        // MotiveForceN and GravityForceN act to accelerate the train. The others act to brake the train.
        public float TotalForceN { get; internal protected set; } // 

        public float CurrentElevationPercent { get; internal protected set; }

        public float CouplerForceU { get; internal set; } // result
        public bool CouplerExceedBreakLimit { get; internal set; }  //true when coupler force is higher then Break limit (set by 2nd parameter in Break statement)
        public bool CouplerOverloaded { get; internal set; }  //true when coupler force is higher then Proof limit, thus overloaded, but not necessarily broken (set by 1nd parameter in Break statement)
        public bool BrakesStuck { get; internal set; }  //true when brakes stuck

        // set when model is loaded
#pragma warning disable CA1002 // Do not expose generic lists
        public List<WheelAxle> WheelAxles { get; } = new List<WheelAxle>();
#pragma warning restore CA1002 // Do not expose generic lists
        public bool WheelAxlesLoaded { get; private set; }
        public TrainCarParts Parts { get; } = new TrainCarParts();

#pragma warning disable CA1002 // Do not expose generic lists
        // For use by cameras, initialized in MSTSWagon class and its derived classes
        public List<PassengerViewPoint> PassengerViewpoints { get; private protected set; }
        public List<PassengerViewPoint> CabViewpoints { get; private protected set; } //three dimensional cab view point
        public List<ViewPoint> HeadOutViewpoints { get; private protected set; }
#pragma warning restore CA1002 // Do not expose generic lists

        public float DriverWheelRadiusM { get; protected set; } = (float)Size.Length.FromIn(30.0f); // Drive wheel radius of locomotive wheels - Wheel radius of loco drive wheels can be anywhere from about 10" to 40".

        public WagonType WagonType { get; private protected set; }

        public EngineType EngineType { get; private protected set; }

        public WagonSpecialType WagonSpecialType { get; private protected set; }

        public float? TunnelFrontPositionBeyondStart { get; internal set; }          // position of front of wagon wrt start of tunnel
        public float? TunnelLengthAheadFront { get; internal set; }                 // Length of tunnel remaining ahead of front of wagon (negative if front of wagon out of tunnel)
        public float? TunnelLengthBehindRear { get; internal set; }                 // Length of tunnel behind rear of wagon (negative if rear of wagon has not yet entered tunnel)
        public int TunnelNumPaths { get; internal set; }                               // Number of paths through tunnel

        public virtual void Initialize()
        {
        }

        // called when it's time to update the MotiveForce and FrictionForce
        public virtual void Update(double elapsedClockSeconds)
        {
            // Initialise ambient temperatures on first initial loop, then ignore
            if (!ambientTemperatureInitialised)
            {
                InitializeCarTemperatures();
                ambientTemperatureInitialised = true;
            }

            // Update temperature variation for height of car above sea level
            // Typically in clear conditions there is a 9.8 DegC variation for every 1000m (1km) rise, in snow/rain there is approx 5.5 DegC variation for every 1000m (1km) rise
            float TemperatureHeightVariationDegC;
            if (simulator.WeatherType == WeatherType.Rain || simulator.WeatherType == WeatherType.Snow) // Apply snow/rain height variation
            {
                TemperatureHeightVariationDegC = (float)Size.Length.ToKM(CarHeightAboveSeaLevel) * WetLapseTemperatureC;
            }
            else  // Apply dry height variation
            {
                TemperatureHeightVariationDegC = (float)Size.Length.ToKM(CarHeightAboveSeaLevel) * DryLapseTemperatureC;
            }

            TemperatureHeightVariationDegC = MathHelper.Clamp(TemperatureHeightVariationDegC, 0.00f, 30.0f);

            CarOutsideTempC = initialCarOutsideTempC - TemperatureHeightVariationDegC;

            // gravity force, M32 is up component of forward vector
            GravityForceN = MassKG * gravitationalAcceleration * WorldPosition.XNAMatrix.M32;
            CurrentElevationPercent = -100f * WorldPosition.XNAMatrix.M32;
            AbsSpeedMpS = Math.Abs(SpeedMpS);

            //TODO: next if block has been inserted to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
            // To achieve the same result with other means, without flipping trainset physics, the block should be deleted
            //      
            if ((Train?.IsPlayerDriven ?? false) && ((this as MSTSLocomotive)?.UsingRearCab ?? false))
            {
                GravityForceN = -GravityForceN;
                CurrentElevationPercent = -CurrentElevationPercent;
            }

            UpdateCurveSpeedLimit(); // call this first as it will provide inputs for the curve force.
            UpdateCurveForce(elapsedClockSeconds);
            UpdateTunnelForce();
            UpdateBrakeSlideCalculation();
            UpdateTrainDerailmentRisk(elapsedClockSeconds);

            // acceleration
            if (elapsedClockSeconds > 0.0f)
            {
                AccelerationMpSS = (SpeedMpS - prevSpeedMpS) / (float)elapsedClockSeconds;

                if (simulator.Settings.UseAdvancedAdhesion && !simulator.Settings.SimpleControlPhysics)
                    AccelerationMpSS = (float)accelerationFilter.Filter(AccelerationMpSS, elapsedClockSeconds);

                prevSpeedMpS = SpeedMpS;
            }
            carInfo.Update(null);
            forceInfo.Update(null);
            powerInfo.Update(null);
        }

        /// <summary>
        /// update position of discrete freight animations (e.g. containers)
        /// </summary>  
        public void UpdateFreightAnimationDiscretePositions()
        {
            if (FreightAnimations?.Animations != null)
            {
                foreach (FreightAnimation freightAnim in FreightAnimations.Animations)
                {
                    if (freightAnim is FreightAnimationDiscrete freightAnimationDiscrete)
                    {
                        if (freightAnimationDiscrete.Loaded && freightAnimationDiscrete.Container != null)
                        {
                            World.Container container = freightAnimationDiscrete.Container;
                            container.SetWorldPosition(new WorldPosition(WorldPosition.TileX, WorldPosition.TileZ, MatrixExtension.Multiply(container.RelativeContainerMatrix, freightAnimationDiscrete.Wagon.WorldPosition.XNAMatrix)));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initialise Train Temperatures
        /// <\summary>           
        public void InitializeCarTemperatures()
        {
            // Find the latitude reading and set outside temperature
            EarthCoordinates.ConvertWTC(WorldPosition.TileX, WorldPosition.TileZ, WorldPosition.Location, out double latitude, out _);

            float LatitudeDeg = MathHelper.ToDegrees((float)latitude);

            // Sets outside temperature dependent upon the season
            initialCarOutsideTempC = simulator.Season switch
            {
                SeasonType.Winter => (float)WorldWinterLatitudetoTemperatureC[LatitudeDeg],// Winter temps
                SeasonType.Autumn => (float)WorldAutumnLatitudetoTemperatureC[LatitudeDeg],// Autumn temps
                SeasonType.Spring => (float)WorldSpringLatitudetoTemperatureC[LatitudeDeg],// Spring temps
                _ => (float)WorldSummerLatitudetoTemperatureC[LatitudeDeg],// Summer temps
            };

            // If weather is freezing. Snow will only be produced when temp is between 0 and 2 Deg C. Adjust temp as appropriate
            const float SnowTemperatureC = 2;

            if (simulator.WeatherType == WeatherType.Snow && initialCarOutsideTempC > SnowTemperatureC)
            {
                initialCarOutsideTempC = 0;  // Weather snowing - freezing conditions. 
            }

            // Initialise wheel bearing temperature to ambient temperature
            WheelBearingTemperatureDegC = initialCarOutsideTempC;
            initialWheelBearingRiseTemperatureDegC = initialCarOutsideTempC;
            initialWheelBearingDeclineTemperatureDegC = initialCarOutsideTempC;
        }

        #region Calculate Brake Skid

        /// <summary>
        /// This section calculates:
        /// i) Changing brake shoe friction coefficient due to changes in speed
        /// ii) force on the wheel due to braking, and whether sliding will occur.
        /// 
        /// </summary>

        public virtual void UpdateBrakeSlideCalculation()
        {

            // Only apply slide, and advanced brake friction, if advanced adhesion is selected, simplecontrolphysics is not set, and it is a Player train
            if (simulator.Settings.UseAdvancedAdhesion && !simulator.Settings.SimpleControlPhysics && IsPlayerTrain)
            {

                // Get user defined brake shoe coefficient if defined in WAG file
                float UserFriction = GetUserBrakeShoeFrictionFactor();
                float ZeroUserFriction = GetZeroUserBrakeShoeFrictionFactor();
                float AdhesionMultiplier = simulator.Settings.AdhesionFactor / 100.0f; // User set adjustment factor - convert to a factor where 100% = no change to adhesion

                // This section calculates an adjustment factor for the brake force dependent upon the "base" (zero speed) friction value. 
                //For a user defined case the base value is the zero speed value from the curve entered by the user.
                // For a "default" case where no user data has been added to the WAG file, the base friction value has been assumed to be 0.2, thus maximum value of 20% applied.

                if (UserFriction != 0)  // User defined friction has been applied in WAG file - Assume MaxBrakeForce is correctly set in the WAG, so no adjustment required 
                {
                    brakeShoeCoefficientFrictionAdjFactor = UserFriction / ZeroUserFriction * AdhesionMultiplier; // Factor calculated by normalising zero speed value on friction curve applied in WAG file
                    brakeShoeRetardCoefficientFrictionAdjFactor = UserFriction / ZeroUserFriction * AdhesionMultiplier;
                    BrakeShoeCoefficientFriction = UserFriction * AdhesionMultiplier; // For display purposes on HUD
                }
                else
                // User defined friction NOT applied in WAG file - Assume MaxBrakeForce is incorrectly set in the WAG, so adjustment is required 
                {
                    defaultBrakeShoeCoefficientFriction = (float)(7.6f / (Speed.MeterPerSecond.ToKpH(AbsSpeedMpS) + 17.5f) + 0.07f) * AdhesionMultiplier; // Base Curtius - Kniffler equation - u = 0.50, all other values are scaled off this formula
                    brakeShoeCoefficientFrictionAdjFactor = defaultBrakeShoeCoefficientFriction / 0.2f * AdhesionMultiplier;  // Assuming that current MaxBrakeForce has been set with an existing Friction Coff of 0.2f, an adjustment factor needs to be developed to reduce the MAxBrakeForce by a relative amount
                    brakeShoeRetardCoefficientFrictionAdjFactor = defaultBrakeShoeCoefficientFriction / 0.2f * AdhesionMultiplier;
                    BrakeShoeCoefficientFriction = defaultBrakeShoeCoefficientFriction * AdhesionMultiplier;  // For display purposes on HUD
                }

                // Clamp adjustment factor to a value of 1.0 - i.e. the brakeforce can never exceed the Brake Force value defined in the WAG file
                brakeShoeCoefficientFrictionAdjFactor = MathHelper.Clamp(brakeShoeCoefficientFrictionAdjFactor, 0.01f, 1.0f);
                brakeShoeRetardCoefficientFrictionAdjFactor = MathHelper.Clamp(brakeShoeRetardCoefficientFrictionAdjFactor, 0.01f, 1.0f);


                // ************  Check if diesel or electric - assumed already be cover by advanced adhesion model *********

                if (this is MSTSDieselLocomotive || this is MSTSElectricLocomotive)
                {
                    // If advanced adhesion model indicates wheel slip warning, then check other conditions (throttle and brake force) to determine whether it is a wheel slip or brake skid
                    if (WheelSlipWarning && ThrottlePercent < 0.1f && BrakeRetardForceN > 25.0)
                    {
                        BrakeSkidWarning = true;  // set brake skid flag true
                    }
                    else
                    {
                        BrakeSkidWarning = false;
                    }

                    // If advanced adhesion model indicates wheel slip, then check other conditions (throttle and brake force) to determine whether it is a wheel slip or brake skid
                    if (WheelSlip && ThrottlePercent < 0.1f && BrakeRetardForceN > 25.0)
                    {
                        BrakeSkid = true;  // set brake skid flag true
                    }
                    else
                    {
                        BrakeSkid = false;
                    }
                }

                else if (!(this is MSTSDieselLocomotive) || !(this is MSTSElectricLocomotive))
                {

                    // Calculate tread force on wheel - use the retard force as this is related to brakeshoe coefficient, and doesn't vary with skid.
                    brakeWheelTreadForceN = BrakeRetardForceN;

                    // Determine whether car is experiencing a wheel slip during braking
                    if (!BrakeSkidWarning && AbsSpeedMpS > 0.01)
                    {
                        var wagonbrakeadhesiveforcen = MassKG * gravitationalAcceleration * Train.WagonCoefficientFriction; // Adhesive force wheel normal 

                        if (brakeWheelTreadForceN > 0.80f * wagonBrakeAdhesiveForceN && ThrottlePercent > 0.01)
                        {
                            BrakeSkidWarning = true; 	// wagon wheel is about to slip
                        }
                    }
                    else if (brakeWheelTreadForceN < 0.75f * wagonBrakeAdhesiveForceN)
                    {
                        BrakeSkidWarning = false; 	// wagon wheel is back to normal
                    }

                    // Reset WSP dump valve lockout
                    if (WheelBrakeSlideProtectionFitted && WheelBrakeSlideProtectionDumpValveLockout && (ThrottlePercent > 0.01 || AbsSpeedMpS <= 0.002))
                    {
                        WheelBrakeSlideProtectionTimerS = WheelBrakeSlideTimerResetValueS;
                        WheelBrakeSlideProtectionDumpValveLockout = false;

                    }



                    // Calculate adhesive force based upon whether in skid or not
                    if (BrakeSkid)
                    {
                        wagonBrakeAdhesiveForceN = MassKG * gravitationalAcceleration * SkidFriction;  // Adhesive force if wheel skidding
                    }
                    else
                    {
                        wagonBrakeAdhesiveForceN = MassKG * gravitationalAcceleration * Train.WagonCoefficientFriction; // Adhesive force wheel normal
                    }


                    // Test if wheel forces are high enough to induce a slip. Set slip flag if slip occuring 
                    if (!BrakeSkid && AbsSpeedMpS > 0.01)  // Train must be moving forward to experience skid
                    {
                        if (brakeWheelTreadForceN > wagonBrakeAdhesiveForceN)
                        {
                            BrakeSkid = true; 	// wagon wheel is slipping
                            var message = "Car ID: " + CarID + " - experiencing braking force wheel skid.";
                            simulator.Confirmer.Message(ConfirmLevel.Warning, message);
                        }
                    }
                    else if (BrakeSkid && AbsSpeedMpS > 0.01)
                    {
                        if (brakeWheelTreadForceN < wagonBrakeAdhesiveForceN || BrakeForceN == 0.0f)
                        {
                            BrakeSkid = false; 	// wagon wheel is not slipping
                        }

                    }
                    else
                    {
                        BrakeSkid = false; 	// wagon wheel is not slipping

                    }
                }
                else
                {
                    BrakeSkid = false; 	// wagon wheel is not slipping
                    brakeShoeRetardCoefficientFrictionAdjFactor = 1.0f;
                }
            }
            else  // set default values if simple adhesion model, or if diesel or electric locomotive is used, which doesn't check for brake skid.
            {
                BrakeSkid = false; 	// wagon wheel is not slipping
                brakeShoeCoefficientFrictionAdjFactor = 1.0f;  // Default value set to leave existing brakeforce constant regardless of changing speed
                brakeShoeRetardCoefficientFrictionAdjFactor = 1.0f;
                BrakeShoeCoefficientFriction = 1.0f;  // Default value for display purposes

            }

#if DEBUG_BRAKE_SLIDE

            Trace.TraceInformation("================================== Brake Force Slide (TrainCar.cs) ===================================");
            Trace.TraceInformation("Brake Shoe Friction- Car: {0} Speed: {1} Brake Force: {2} Advanced Adhesion: {3}", CarID, MpS.ToMpH(SpeedMpS), BrakeForceN, Simulator.UseAdvancedAdhesion);
            Trace.TraceInformation("BrakeSkidCheck: {0}", BrakeSkidCheck);
            Trace.TraceInformation("Brake Shoe Friction- Coeff: {0} Adjust: {1}", BrakeShoeCoefficientFriction, BrakeShoeCoefficientFrictionAdjFactor);
            Trace.TraceInformation("Brake Shoe Force - Ret: {0} Adjust: {1} Skid {2} Adj {3}", BrakeRetardForceN, BrakeShoeRetardCoefficientFrictionAdjFactor, BrakeSkid, SkidFriction);
            Trace.TraceInformation("Tread: {0} Adhesive: {1}", BrakeWheelTreadForceN, WagonBrakeAdhesiveForceN);
            Trace.TraceInformation("Mass: {0} Rail Friction: {1}", MassKG, Train.WagonCoefficientFriction);
#endif

        }


        #endregion

        #region Calculate resistance due to tunnels
        /// <summary>
        /// Tunnel force (resistance calculations based upon formula presented in papaer titled "Reasonable compensation coefficient of maximum gradient in long railway tunnels"
        /// </summary>
        public virtual void UpdateTunnelForce()
        {
            if (Train.IsPlayerDriven)   // Only calculate tunnel resistance when it is the player train.
            {
                if (TunnelFrontPositionBeyondStart.HasValue)
                {
                    // Calculate tunnel default effective cross-section area, and tunnel perimeter - based upon the designed speed limit of the railway (TRK File)
                    float tunnelLengthM = TunnelLengthAheadFront.Value + TunnelLengthBehindRear.Value;
                    float crossSectionArea = CarWidthM * CarHeightM;

                    float tunnelPerimeterM;
                    float tunnelCrossSectionAreaM2;
                    // Determine tunnel X-sect area and perimeter based upon number of tracks
                    if (TunnelNumPaths >= 2)
                    {
                        tunnelCrossSectionAreaM2 = simulator.Route.DoubleTunnelAreaM2; // Set values for double track tunnels and above
                        tunnelPerimeterM = simulator.Route.DoubleTunnelPerimeterM;
                    }
                    else
                    {
                        tunnelCrossSectionAreaM2 = simulator.Route.SingleTunnelAreaM2; // Set values for single track tunnels
                        tunnelPerimeterM = simulator.Route.SingleTunnelPerimeterM;
                    }

                    // 
                    // Calculate first tunnel factor

                    double componentA = 0.00003318 * Const.DensityAir * tunnelCrossSectionAreaM2 / ((1 - (crossSectionArea / tunnelCrossSectionAreaM2)) * (1 - (crossSectionArea / tunnelCrossSectionAreaM2)));
                    double componentB = 174.419 * (1 - (crossSectionArea / tunnelCrossSectionAreaM2)) * (1 - (crossSectionArea / tunnelCrossSectionAreaM2));
                    double componentC = (2.907 * (1 - (crossSectionArea / tunnelCrossSectionAreaM2)) * (1 - (crossSectionArea / tunnelCrossSectionAreaM2))) / (4.0 * (tunnelCrossSectionAreaM2 / tunnelPerimeterM));

                    double tunnel1 = Math.Sqrt(componentB + (componentC * (tunnelLengthM - Train.Length) / Train.Length));
                    double tunnel2 = (1.0 - (1.0 / (1.0 + tunnel1))) * (1.0 - (1.0 / (1.0 + tunnel1)));

                    double UnitAerodynamicDrag = (componentA * Train.Length / Mass.Kilogram.ToTonnes(Train.MassKg)) * tunnel2;

                    TunnelForceN = (float)(UnitAerodynamicDrag * Mass.Kilogram.ToTonnes(MassKG) * AbsSpeedMpS * AbsSpeedMpS);
                }
                else
                {
                    TunnelForceN = 0.0f; // Reset tunnel force to zero when train is no longer in the tunnel
                }
            }
        }
        #endregion

        #region Calculate risk of train derailing

        //================================================================================================//
        /// <summary>
        /// Update Risk of train derailing and also calculate coupler angle
        /// Train will derail if lateral forces on the train exceed the vertical forces holding the train on the railway track. 
        /// Typically the train is most at risk when travelling around a curve
        ///
        /// Based upon "Fast estimation of the derailment risk of a braking train in curves and turnouts" - 
        /// https://www.researchgate.net/publication/304618476_Fast_estimation_of_the_derailment_risk_of_a_braking_train_in_curves_and_turnouts
        ///
        /// This section calculates the coupler angle behind the current car (ie the rear coupler on this car and the front coupler on the following car. The coupler angle will be used for
        /// coupler automation as well as calculating Lateral forces on the car.
        /// 
        /// In addition Chapter 2 - Flange Climb Derailment Criteria of the TRB’s Transit Cooperative Research Program (TCRP) Report 71, examines flange climb derailment criteria for transit 
        /// vehicles that include lateral-to-vertical ratio limits and a corresponding flange-climb-distance limit. The report also includes guidance to transit agencies on wheel and rail 
        /// maintenance practices.
        /// 
        /// Some of the concepts described in this publication have also been used to calculate the derailment likelihood.
        /// 
        /// https://www.nap.edu/read/13841/chapter/4
        /// 
        /// It should be noted that car derailment is a very complex process that is impacted by many diferent factors, including the track structure and train conditions. To model all of 
        /// these factors is not practical so only some of the key factors are considered. For eaxmple, wheel wear may determine whether a particular car will derial or not. So the same 
        /// type of car can either derail or not under similar circumstances.
        /// 
        /// Hence these calculations provide a "generic" approach to determining whether a car will derail or not.
        /// 
        /// Buff Coupler angle calculated from this publication: In-Train Force Limit Study by National Research Council Canada
        /// 
        /// https://nrc-publications.canada.ca/eng/view/ft/?id=8cc206d0-5dbd-42ed-9b4e-35fd9f8b8efb
        /// 
        /// </summary>

        public void UpdateTrainDerailmentRisk(double elapsedClockSeconds)
        {
            // Calculate coupler angle when travelling around curve
            // To achieve an accurate coupler angle calculation the following length need to be calculated. These values can be included in the ENG/WAG file for greatest accuracy, or alternatively OR will
            // calculate some default values based upon the length of the car specified in the "Size" statement. This value may however be inaccurate, and sets the "visual" distance for placement of the 
            // animated coupler. So often it is a good idea to add the values in the WAG file.

            double overhangThisCar = (CarBodyLength - CarBogieCentreLength) / 2; // Vehicle overhang - B
            double bogieDistanceThisCar = CarBogieCentreLength / 2; // 0.5 * distance between bogie centres - A
            double couplerDistanceThisCar = (CarCouplerFaceLength - CarBodyLength) / 2;

            double overhangBehindCar = 2.545;  // Vehicle overhang - B
            double bogieDistanceBehindCar = 8.23;  // 0.5 * distance between bogie centres - A
            double couplerDistanceBehindCar = (CarCouplerFaceLength - CarBodyLength) / 2;
            if (CarBehind != null)
            {
                overhangBehindCar = (CarBehind.CarBodyLength - CarBehind.CarBogieCentreLength) / 2;  // Vehicle overhang - B
                bogieDistanceBehindCar = CarBehind.CarBogieCentreLength / 2;  // 0.5 * distance between bogie centres - A
                couplerDistanceBehindCar = (CarBehind.CarCouplerFaceLength - CarBehind.CarBodyLength) / 2;
            }

            double couplerAlphaAngle;
            double couplerBetaAngle;
            double couplerGammaAngle;

            double finalCouplerAlphaAngle;
            double finalCouplerBetaAngle;
            double finalCouplerGammaAngle;

            double couplerDistanceM = couplerDistanceThisCar + couplerDistanceBehindCar + CouplerSlackM;

            if (couplerDistanceM == 0)
            {
                couplerDistanceM = 0.0001; // Stop couplerDistance equalling zero as this causes NaN calculations in following calculations.
            }

            double BogieCentresAdjVehiclesM = overhangThisCar + overhangBehindCar + couplerDistanceM; // L value = Overhangs + Coupler spacing - D

            if (CarBehind != null)
            {

                if (CurrentCurveRadius != 0 || CarBehind.CurrentCurveRadius != 0)
                {
                    //When coming into a curve or out of a curve it is possible for an infinity value to occur, this next section ensures that never happens
                    if (CurrentCurveRadius == 0)
                    {
                        double AspirationalCurveRadius = 10000;
                        couplerAlphaAngle = bogieDistanceThisCar / AspirationalCurveRadius;
                        couplerGammaAngle = BogieCentresAdjVehiclesM / (2.0 * AspirationalCurveRadius);


                        finalCouplerAlphaAngle = bogieDistanceThisCar / CarBehind.CurrentCurveRadius;
                        finalCouplerGammaAngle = BogieCentresAdjVehiclesM / (2.0 * CarBehind.CurrentCurveRadius);
                    }
                    else
                    {
                        couplerAlphaAngle = bogieDistanceThisCar / CurrentCurveRadius;  // current car curve
                        couplerGammaAngle = BogieCentresAdjVehiclesM / (2.0 * CurrentCurveRadius); // assume curve between cars is the same as the curve for the front car.
                        finalCouplerAlphaAngle = bogieDistanceThisCar / CurrentCurveRadius;  // current car curve
                        finalCouplerGammaAngle = BogieCentresAdjVehiclesM / (2.0f * CurrentCurveRadius); // assume curve between cars is the same as the curve for the front car.
                    }

                    //When coming into a curve or out of a curve it is possible for an infinity value to occur, which can cause calculation issues, this next section ensures that never happens
                    if (CarBehind.CurrentCurveRadius == 0)
                    {
                        double AspirationalCurveRadius = 10000;
                        couplerBetaAngle = bogieDistanceBehindCar / AspirationalCurveRadius;

                        finalCouplerBetaAngle = bogieDistanceBehindCar / CurrentCurveRadius;
                    }
                    else
                    {
                        couplerBetaAngle = bogieDistanceBehindCar / CarBehind.CurrentCurveRadius; // curve of following car

                        finalCouplerBetaAngle = bogieDistanceBehindCar / CarBehind.CurrentCurveRadius; // curve of following car
                    }

                    double AngleBetweenCarbodies = couplerAlphaAngle + couplerBetaAngle + 2.0 * couplerGammaAngle;

                    double finalAngleBetweenCarbodies = finalCouplerAlphaAngle + finalCouplerBetaAngle + 2.0 * finalCouplerGammaAngle;

                    // Find maximum coupler angle expected in this curve, ie both cars will be on the curve
                    double finalWagonRearCouplerAngleRad = (BogieCentresAdjVehiclesM * (finalCouplerGammaAngle + finalCouplerAlphaAngle) - overhangBehindCar * finalAngleBetweenCarbodies) / couplerDistanceM;
                    double finalWagonFrontCouplerAngleRad = (BogieCentresAdjVehiclesM * (finalCouplerGammaAngle + finalCouplerBetaAngle) - overhangThisCar * finalAngleBetweenCarbodies) / couplerDistanceM;

                    // If first car is starting to turn then slowly increase coupler angle to the maximum value expected
                    if (CurrentCurveRadius != 0 && CarBehind.CurrentCurveRadius == 0)
                    {
                        wagonRearCouplerAngle += 0.0006;
                        wagonRearCouplerAngle = (finalWagonRearCouplerAngleRad < 0) ? Math.Clamp(wagonRearCouplerAngle, finalWagonRearCouplerAngleRad, 0) : Math.Clamp(wagonRearCouplerAngle, 0, finalWagonRearCouplerAngleRad);

                        CarBehind.wagonFrontCouplerAngle += 0.0006;
                        CarBehind.wagonFrontCouplerAngle = finalWagonFrontCouplerAngleRad < 0 ? Math.Clamp(CarBehind.wagonFrontCouplerAngle, finalWagonFrontCouplerAngleRad, 0) : Math.Clamp(CarBehind.wagonFrontCouplerAngle, 0, finalWagonFrontCouplerAngleRad);

                    }
                    else if (CurrentCurveRadius != 0 && CarBehind.CurrentCurveRadius != 0) // both cars on the curve
                    {
                        // Find coupler angle for rear coupler on the car
                        wagonRearCouplerAngle = (BogieCentresAdjVehiclesM * (couplerGammaAngle + couplerAlphaAngle) - overhangBehindCar * AngleBetweenCarbodies) / couplerDistanceM;
                        // Find coupler angle for front coupler on the following car
                        CarBehind.wagonFrontCouplerAngle = (BogieCentresAdjVehiclesM * (couplerGammaAngle + couplerBetaAngle) - overhangThisCar * AngleBetweenCarbodies) / couplerDistanceM;
                    }

                    // If first car is still on straight, and last car is still on the curve, then slowly decrease coupler angle so that it is "straight" again
                    else if (CurrentCurveRadius == 0 && CarBehind.CurrentCurveRadius != 0)
                    {
                        wagonRearCouplerAngle -= 0.0006;
                        wagonRearCouplerAngle = (finalWagonRearCouplerAngleRad < 0) ? Math.Clamp(wagonRearCouplerAngle, finalWagonRearCouplerAngleRad, 0) : Math.Clamp(wagonRearCouplerAngle, 0, finalWagonRearCouplerAngleRad);

                        CarBehind.wagonFrontCouplerAngle -= 0.0006;
                        CarBehind.wagonFrontCouplerAngle = finalWagonFrontCouplerAngleRad < 0 ? Math.Clamp(CarBehind.wagonFrontCouplerAngle, finalWagonFrontCouplerAngleRad, 0) : Math.Clamp(CarBehind.wagonFrontCouplerAngle, 0, finalWagonFrontCouplerAngleRad);
                    }

                    // Set direction of coupler angle depending upon whether curve is left or right handed. Coupler angle will be +ve or -ve with relation to the car as a reference frame.
                    // Left hand Curves will result in: Front coupler behind: +ve, and Rear coupler front: +ve
                    // Right hand Curves will result in: Front coupler behind: -ve, and Rear coupler front: -ve

                    // Determine whether curve is left hand or right hand
                    CurveDirection curveDirection = GetCurveDirection();
                    //                    CurveDirection carBehindcurveDirection = CarBehind.GetCurveDirection();

                    switch (curveDirection)
                    {
                        case CurveDirection.Right:
                            AdjustedWagonRearCouplerAngle = (float)-wagonRearCouplerAngle;
                            CarBehind.AdjustedWagonFrontCouplerAngle = (float)-CarBehind.wagonFrontCouplerAngle;
                            break;
                        case CurveDirection.Left:
                            AdjustedWagonRearCouplerAngle = (float)wagonRearCouplerAngle;
                            CarBehind.AdjustedWagonFrontCouplerAngle = (float)CarBehind.wagonFrontCouplerAngle;
                            break;
                        default:
                            AdjustedWagonRearCouplerAngle = (float)wagonRearCouplerAngle;
                            CarBehind.AdjustedWagonFrontCouplerAngle = (float)CarBehind.wagonFrontCouplerAngle;
                            break;
                    }

                    // Only process this code segment if coupler is in compression
                    if (CouplerForceU > 0 && CouplerSlackM < 0)
                    {

                        // Calculate Buff coupler angles. Car1 is current car, and Car2 is the car behind
                        // Car ahead rear coupler angle
                        double carCouplerlengthft = Size.Length.ToFt(CarCouplerFaceLength - CarBodyLength) + CouplerSlackM / 2;
                        double carbehindCouplerlengthft = Size.Length.ToFt(CarBehind.CarCouplerFaceLength - CarBehind.CarBodyLength) + CouplerSlackM / 2;
                        double A1 = Math.Sqrt(Math.Pow(Size.Length.ToFt(CurrentCurveRadius), 2) - Math.Pow(Size.Length.ToFt(CarBogieCentreLength), 2) / 4.0f);
                        double A2 = (Size.Length.ToFt(CarCouplerFaceLength) / 2.0f) - carCouplerlengthft;
                        double A = Math.Atan(A1 / A2);

                        double B = Math.Asin(2.0f * Size.Length.ToFt(CarTrackPlayM) / Size.Length.ToFt(CarBogieCentreLength));
                        double C1 = Math.Pow(carCouplerlengthft + carbehindCouplerlengthft, 2);

                        double C2_1 = Math.Sqrt(Math.Pow(Size.Length.ToFt(CarCouplerFaceLength) / 2.0f - carCouplerlengthft, 2) + Math.Pow(Size.Length.ToFt(CurrentCurveRadius), 2) - Math.Pow(Size.Length.ToFt(CarBogieCentreLength), 2) / 4.0f);
                        double C2_2 = (2.0f * Size.Length.ToFt(CarTrackPlayM) * (Size.Length.ToFt(CarCouplerFaceLength) / 2.0f - carCouplerlengthft)) / Size.Length.ToFt(CarBogieCentreLength);
                        double C2 = Math.Pow((C2_1 + C2_2), 2);

                        double C3_1 = Math.Sqrt(Math.Pow(Size.Length.ToFt(CarBehind.CarCouplerFaceLength) / 2.0f - carbehindCouplerlengthft, 2) + Math.Pow(Size.Length.ToFt(CurrentCurveRadius), 2) - Math.Pow(Size.Length.ToFt(CarBehind.CarBogieCentreLength), 2) / 4.0f);
                        double C3_2 = (2.0f * Size.Length.ToFt(CarBehind.CarTrackPlayM) * (Size.Length.ToFt(CarBehind.CarCouplerFaceLength) / 2.0f - carbehindCouplerlengthft)) / Size.Length.ToFt(CarBehind.CarBogieCentreLength);
                        double C3 = Math.Pow((C3_1 + C3_2), 2);

                        double C4 = 2.0f * (carCouplerlengthft + carbehindCouplerlengthft) * (C2_1 + C2_2);

                        double C = Math.Acos((C1 + C2 - C3) / C4);

                        wagonRearCouplerBuffAngle = MathHelper.ToRadians(180.0f) - A + B - C;

                        // This car front coupler angle
                        double X1 = Math.Sqrt(Math.Pow(Size.Length.ToFt(CurrentCurveRadius), 2) - Math.Pow(Size.Length.ToFt(CarBehind.CarBogieCentreLength), 2) / 4.0f);
                        double X2 = (Size.Length.ToFt(CarBehind.CarCouplerFaceLength) / 2.0f) - carbehindCouplerlengthft;
                        double X = Math.Atan(X1 / X2);

                        double Y = Math.Asin(2.0f * Size.Length.ToFt(CarBehind.CarTrackPlayM) / Size.Length.ToFt(CarBehind.CarBogieCentreLength));

                        double Z1 = Math.Pow(carCouplerlengthft + carbehindCouplerlengthft, 2);
                        double Z2_1 = Math.Sqrt(Math.Pow(Size.Length.ToFt(CarBehind.CarCouplerFaceLength) / 2.0f - carbehindCouplerlengthft, 2) + Math.Pow(Size.Length.ToFt(CurrentCurveRadius), 2) - Math.Pow(Size.Length.ToFt(CarBehind.CarBogieCentreLength), 2) / 4.0f);
                        double Z2_2 = (2.0f * Size.Length.ToFt(CarBehind.CarTrackPlayM) * (Size.Length.ToFt(CarBehind.CarCouplerFaceLength) / 2.0f - carbehindCouplerlengthft)) / Size.Length.ToFt(CarBehind.CarBogieCentreLength);
                        double Z2 = Math.Pow((Z2_1 + Z2_2), 2);

                        double Z3_1 = Math.Sqrt(Math.Pow(Size.Length.ToFt(CarCouplerFaceLength) / 2.0f - carCouplerlengthft, 2) + Math.Pow(Size.Length.ToFt(CurrentCurveRadius), 2) - Math.Pow(Size.Length.ToFt(CarBogieCentreLength), 2) / 4.0f);
                        double Z3_2 = (2.0f * Size.Length.ToFt(CarTrackPlayM) * (Size.Length.ToFt(CarCouplerFaceLength) / 2.0f - carCouplerlengthft)) / Size.Length.ToFt(CarBogieCentreLength);
                        double Z3 = Math.Pow((Z3_1 + Z3_2), 2);

                        double Z4 = 2.0f * (carCouplerlengthft + carbehindCouplerlengthft) * (Z2_1 + Z2_2);

                        float Z = (float)Math.Acos((Z1 + Z2 - Z3) / Z4);

                        CarBehind.wagonFrontCouplerBuffAngle = MathHelper.ToRadians(180.0f) - X + Y - Z;
                    }

                }
                else if (CarAhead?.CurrentCurveRadius == 0)
                {
                    AdjustedWagonRearCouplerAngle = 0.0f;
                    CarBehind.AdjustedWagonFrontCouplerAngle = 0.0f;
                    wagonRearCouplerAngle = 0;
                    wagonFrontCouplerAngle = 0;
                    wagonRearCouplerBuffAngle = 0;
                    wagonFrontCouplerBuffAngle = 0;
                    CarBehind.wagonFrontCouplerAngle = 0;
                    CarAhead.wagonRearCouplerAngle = 0;
                }

                // Calculate airhose angles and height adjustment values for the air hose.  Firstly the "rest point" is calculated, and then the real time point. 
                // The height and angle variation are then calculated against "at rest" reference point. The air hose angle is used to rotate the hose in two directions, ie the Y and Z axis. 

                // Calculate height adjustment.
                double rearairhoseheightadjustmentreferenceM = Math.Sqrt(Math.Pow(airHoseLengthM, 2) - Math.Pow(airHoseHorizontalLengthM, 2));
                double frontairhoseheightadjustmentreferenceM = Math.Sqrt(Math.Pow(airHoseLengthM, 2) - Math.Pow(CarBehind.airHoseHorizontalLengthM, 2));

                // actual airhose height
                RearAirHoseHeightAdjustmentM = (float)Math.Sqrt(Math.Pow(airHoseLengthM, 2) - Math.Pow((airHoseHorizontalLengthM + CouplerSlackM), 2));
                CarBehind.FrontAirHoseHeightAdjustmentM = (float)Math.Sqrt(Math.Pow(airHoseLengthM, 2) - Math.Pow((CarBehind.airHoseHorizontalLengthM + CouplerSlackM), 2));

                // refererence adjustment heights to rest position
                // If higher then rest position, then +ve adjustment
                if (RearAirHoseHeightAdjustmentM >= rearairhoseheightadjustmentreferenceM)
                {
                    RearAirHoseHeightAdjustmentM -= (float)rearairhoseheightadjustmentreferenceM;
                }
                else // if lower then the rest position, then -ve adjustment
                {
                    RearAirHoseHeightAdjustmentM = (float)(rearairhoseheightadjustmentreferenceM - RearAirHoseHeightAdjustmentM);
                }

                if (CarBehind.FrontAirHoseHeightAdjustmentM >= frontairhoseheightadjustmentreferenceM)
                {
                    CarBehind.FrontAirHoseHeightAdjustmentM -= (float)frontairhoseheightadjustmentreferenceM;
                }
                else
                {
                    CarBehind.FrontAirHoseHeightAdjustmentM = (float)frontairhoseheightadjustmentreferenceM - CarBehind.FrontAirHoseHeightAdjustmentM;
                }

                // Calculate angle adjustments
                double rearAirhoseAngleAdjustmentReferenceRad = Math.Asin(airHoseHorizontalLengthM / airHoseLengthM);
                double frontAirhoseAngleAdjustmentReferenceRad = Math.Asin(CarBehind.airHoseHorizontalLengthM / airHoseLengthM);

                RearAirHoseZAngleAdjustmentRad = (float)Math.Asin((airHoseHorizontalLengthM + CouplerSlackM) / airHoseLengthM);
                CarBehind.FrontAirHoseZAngleAdjustmentRad = (float)Math.Asin((CarBehind.airHoseHorizontalLengthM + CouplerSlackM) / airHoseLengthM);

                // refererence adjustment angles to rest position
                if (RearAirHoseZAngleAdjustmentRad >= rearAirhoseAngleAdjustmentReferenceRad)
                {
                    RearAirHoseZAngleAdjustmentRad -= (float)rearAirhoseAngleAdjustmentReferenceRad;
                }
                else
                {
                    RearAirHoseZAngleAdjustmentRad = (float)(rearAirhoseAngleAdjustmentReferenceRad - RearAirHoseZAngleAdjustmentRad);
                }

                // The Y axis angle adjustment should be the same as the z axis
                RearAirHoseYAngleAdjustmentRad = RearAirHoseZAngleAdjustmentRad;

                if (CarBehind.FrontAirHoseZAngleAdjustmentRad >= frontAirhoseAngleAdjustmentReferenceRad)
                {
                    CarBehind.FrontAirHoseZAngleAdjustmentRad -= (float)frontAirhoseAngleAdjustmentReferenceRad;
                }
                else
                {
                    CarBehind.FrontAirHoseZAngleAdjustmentRad = (float)(frontAirhoseAngleAdjustmentReferenceRad - CarBehind.FrontAirHoseZAngleAdjustmentRad);
                }

                // The Y axis angle adjustment should be the same as the z axis
                CarBehind.FrontAirHoseYAngleAdjustmentRad = CarBehind.FrontAirHoseZAngleAdjustmentRad;

            }

            // Train will derail if lateral forces on the train exceed the vertical forces holding the train on the railway track.
            // Coupler force is calculated at the rear of each car, so calculation values may need to be from the car ahead. 
            // Typically the train is most at risk when travelling around a curve.

            // Calculate the vertical force on the wheel of the car, to determine whether wagon derails or not
            // To calculate vertical force on outer wheel = (WagMass / NumWheels) * gravity + WagMass / NumAxles * ( (Speed^2 / CurveRadius) - (gravity * superelevation angle)) * (height * track width)
            // Equation 5

            if (IsPlayerTrain && derailmentCoefficientEnabled)
            {
                if (CouplerForceU > 0 && CouplerSlackM < 0) // If car coupler is in compression, use the buff angle
                {
                    wagonCouplerAngleDerail = Math.Abs(wagonRearCouplerBuffAngle);
                }
                else // if coupler in tension, then use tension angle
                {
                    wagonCouplerAngleDerail = Math.Abs(wagonRearCouplerAngle);
                }


                int numAxles = locoNumDrvAxles + wagonNumAxles;
                int numWheels = numAxles * 2;

                if (CurrentCurveRadius != 0)
                {
                    // Prevent NaN if numWheels = 0
                    double A = numWheels != 0 ? (MassKG / numWheels) * gravitationalAcceleration : MassKG * gravitationalAcceleration;

                    double B1 = numAxles != 0 ? MassKG / numAxles : MassKG;
                    double B2 = gravitationalAcceleration * Math.Sin(superElevationAngle);
                    double B3 = Math.Pow(Math.Abs(SpeedMpS), 2) / CurrentCurveRadius;
                    float B4 = centreOfGravityM.Y / trackGauge;

                    totalWagonVerticalDerailForce = A + B1 * (B3 - B2) * B4;

                    // Calculate lateral force per wheelset on the first bogie
                    // Lateral Force = (Coupler force x Sin (Coupler Angle) / NumBogies) + WagMass / NumAxles * ( (Speed^2 / CurveRadius) - (gravity * superelevation angle))

                    if (CarAhead != null)
                    {
                        // Prevent NaN if WagonNumBogies = 0
                        double AA1 = Math.Abs(CarAhead.CouplerForceUSmoothed.SmoothedValue) * Math.Sin(wagonCouplerAngleDerail) / Math.Max(1, (Parts.Count - 1));

                        double BB1 = MassKG / Math.Max(1, numAxles);
                        double BB2 = Math.Pow(Math.Abs(SpeedMpS), 2) / CurrentCurveRadius;
                        double BB3 = gravitationalAcceleration * Math.Sin(superElevationAngle);

                        totalWagonLateralDerailForce = Math.Abs(AA1 + BB1 * (BB2 - BB3));
                    }

                    DerailmentCoefficient = (float)(totalWagonLateralDerailForce / totalWagonVerticalDerailForce);

                    // use the dynamic multiplication coefficient to calculate final derailment coefficient, the above method calculated using quasi-static factors.
                    // The differences between quasi-static and dynamic limits are due to effects of creepage, curve, conicity, wheel unloading ratio, track geometry, 
                    // car configurations and the share of wheel load changes which are not taken into account in the static analysis etc. 
                    // Hence the following factors have been used to adjust to dynamic effects.
                    // Original figures quoted - Static Draft = 0.389, Static Buff = 0.389, Dynamic Draft = 0.29, Dynamic Buff = 0.22. 
                    // Hence use the following multiplication factors, Buff = 1.77, Draft = 1.34.
                    if (CouplerForceU > 0 && CouplerSlackM < 0)
                    {
                        DerailmentCoefficient *= 1.77f; // coupler in buff condition
                    }
                    else
                    {
                        DerailmentCoefficient *= 1.34f;
                    }

                    var wagonAdhesion = Train.WagonCoefficientFriction;

                    // Calculate Nadal derailment coefficient limit
                    nadalDerailmentCoefficient = ((float)Math.Tan(maximumWheelFlangeAngle) - wagonAdhesion) / (1f + wagonAdhesion * (float)Math.Tan(maximumWheelFlangeAngle));

                    // Calculate Angle of Attack - AOA = sin-1(2 * bogie wheel base / curve radius)
                    angleOfAttack = (float)Math.Asin(2 * rigidWheelBaseM / CurrentCurveRadius);
                    var angleofAttackmRad = angleOfAttack * 1000f; // Convert to micro radians

                    // Calculate the derail climb distance - uses the general form equation 2.4 from the above publication
                    var parameterA_1 = ((100 / (-1.9128f * MathHelper.ToDegrees(maximumWheelFlangeAngle) + 146.56f)) + 3.1f) * Size.Length.ToIn(wheelFlangeLength);

                    var parameterA_2 = (1.0f / (-0.0092f * Math.Pow(MathHelper.ToDegrees(maximumWheelFlangeAngle), 2) + 1.2125f * MathHelper.ToDegrees(maximumWheelFlangeAngle) - 39.031f)) + 1.23f;

                    var parameterA = parameterA_1 + parameterA_2;

                    var parameterB_1 = ((10f / (-21.157f * Size.Length.ToIn(wheelFlangeLength) + 2.1052f)) + 0.05f) * MathHelper.ToDegrees(maximumWheelFlangeAngle);

                    var parameterB_2 = (10 / (0.2688f * Size.Length.ToIn(wheelFlangeLength) - 0.0266f)) - 5f;

                    var parameterB = parameterB_1 + parameterB_2;

                    derailClimbDistance = (float)Size.Length.FromFt((float)((parameterA * parameterB * Size.Length.ToIn(wheelFlangeLength)) / ((angleofAttackmRad + (parameterB * Size.Length.ToIn(wheelFlangeLength))))));

                    // calculate the time taken to travel the derail climb distance
                    var derailTimeS = derailClimbDistance / AbsSpeedMpS;

                    // Set indication that a derail may occur
                    DerailPossible = DerailmentCoefficient > nadalDerailmentCoefficient;

                    // If derail climb time exceeded, then derail happens
                    if (DerailPossible && derailElapsedTime > derailTimeS)
                    {
                        DerailExpected = true;
                        simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString($"Car {CarID} has derailed on the curve."));
                        //  Trace.TraceInformation("Car Derail - CarID: {0}, Coupler: {1}, CouplerSmoothed {2}, Lateral {3}, Vertical {4}, Angle {5} Nadal {6} Coeff {7}", CarID, CouplerForceU, CouplerForceUSmoothed.SmoothedValue, TotalWagonLateralDerailForceN, TotalWagonVerticalDerailForceN, WagonCouplerAngleDerailRad, NadalDerailmentCoefficient, DerailmentCoefficient);
                        //   Trace.TraceInformation("Car Ahead Derail - CarID: {0}, Coupler: {1}, CouplerSmoothed {2}, Lateral {3}, Vertical {4}, Angle {5}", CarAhead.CarID, CarAhead.CouplerForceU, CarAhead.CouplerForceUSmoothed.SmoothedValue, CarAhead.TotalWagonLateralDerailForceN, CarAhead.TotalWagonVerticalDerailForceN, CarAhead.WagonCouplerAngleDerailRad);
                    }
                    else if (DerailPossible)
                    {
                        derailElapsedTime += (float)elapsedClockSeconds;
                        //   Trace.TraceInformation("Car Derail Time - CarID: {0}, Coupler: {1}, CouplerSmoothed {2}, Lateral {3}, Vertical {4}, Angle {5}, Elapsed {6}, DeratilTime {7}, Distance {8} Nadal {9} Coeff {10}", CarID, CouplerForceU, CouplerForceUSmoothed.SmoothedValue, TotalWagonLateralDerailForceN, TotalWagonVerticalDerailForceN, WagonCouplerAngleDerailRad, DerailElapsedTimeS, derailTimeS, DerailClimbDistanceM, NadalDerailmentCoefficient, DerailmentCoefficient);
                    }
                    else
                    {
                        derailElapsedTime = 0; // Reset timer if derail is not possible
                    }

                    if (AbsSpeedMpS < 0.01)
                    {
                        DerailExpected = false;
                        DerailPossible = false;
                    }
                }
                else
                {
                    totalWagonLateralDerailForce = 0;
                    totalWagonVerticalDerailForce = 0;
                    DerailmentCoefficient = 0;
                    DerailExpected = false;
                    DerailPossible = false;
                    derailElapsedTime = 0;
                }

                //if (TotalWagonLateralDerailForceN > TotalWagonVerticalDerailForceN)
                //{
                //    BuffForceExceeded = true;
                //}
                //else
                //{
                //    BuffForceExceeded = false;
                //}
            }

        }

        #endregion

        /// <summary>
        /// Get the current direction that curve is heading relative to the train.
        /// </summary>
        /// <returns>left or Right indication</returns>
        private CurveDirection GetCurveDirection()
        {
            CurveDirection result = CurveDirection.Straight;

            if (CarBehind != null && (CurrentCurveRadius != 0 || CarBehind.CurrentCurveRadius != 0))
            {

                // Front Wagon Direction
                double direction = Math.Atan2(WorldPosition.XNAMatrix.M13, WorldPosition.XNAMatrix.M11);
                float frontWagonDirectionDeg = MathHelper.ToDegrees((float)direction);

                // If car is flipped, then the car's direction will be reversed by 180 compared to the rest of the train, and thus for calculation purposes only, 
                // it is necessary to reverse the "assumed" direction of the car back again. This shouldn't impact the visual appearance of the car.
                if (Flipped)
                {
                    frontWagonDirectionDeg += 180.0f; // Reverse direction of car
                    if (frontWagonDirectionDeg > 360) // If this results in an angle greater then 360, then convert it back to an angle between 0 & 360.
                    {
                        frontWagonDirectionDeg -= 360;
                    }
                }

                // If a westerly direction (ie -ve) convert to an angle between 0 and 360
                if (frontWagonDirectionDeg < 0)
                    frontWagonDirectionDeg += 360;

                // Rear Wagon Direction
                direction = Math.Atan2(CarBehind.WorldPosition.XNAMatrix.M13, CarBehind.WorldPosition.XNAMatrix.M11);
                float behindWagonDirectionDeg = MathHelper.ToDegrees((float)direction);


                // If car is flipped, then the car's direction will be reversed by 180 compared to the rest of the train, and thus for calculation purposes only, 
                // it is necessary to reverse the "assumed" direction of the car back again. This shouldn't impact the visual appearance of the car.
                if (CarBehind.Flipped)
                {
                    behindWagonDirectionDeg += 180.0f; // Reverse direction of car
                    if (behindWagonDirectionDeg > 360) // If this results in an angle greater then 360, then convert it back to an angle between 0 & 360.
                    {
                        behindWagonDirectionDeg -= 360;
                    }
                }

                // If a westerly direction (ie -ve) convert to an angle between 0 and 360
                if (behindWagonDirectionDeg < 0)
                    behindWagonDirectionDeg += 360;

                if (frontWagonDirectionDeg > 270 && behindWagonDirectionDeg < 90)
                {
                    frontWagonDirectionDeg -= 360;
                }

                if (frontWagonDirectionDeg < 90 && behindWagonDirectionDeg > 270)
                {
                    behindWagonDirectionDeg -= 360;
                }

                var directionBandwidth = Math.Abs(frontWagonDirectionDeg - behindWagonDirectionDeg);

                // Calculate curve direction
                if (frontWagonDirectionDeg > behindWagonDirectionDeg && directionBandwidth > 0.005)
                {
                    result = CurveDirection.Right;
                }
                else if (frontWagonDirectionDeg < behindWagonDirectionDeg && directionBandwidth > 0.005)
                {
                    result = CurveDirection.Left;
                }
            }

            return result;
        }


        #region Calculate permissible speeds around curves
        /// <summary>
        /// Reads current curve radius and computes the maximum recommended speed around the curve based upon the 
        /// superelevation of the track
        /// Based upon information extracted from - Critical Speed Analysis of Railcars and Wheelsets on Curved and Straight Track - https://scarab.bates.edu/cgi/viewcontent.cgi?article=1135&context=honorstheses
        /// </summary>
        public virtual void UpdateCurveSpeedLimit()
        {
            float s = AbsSpeedMpS; // speed of train

            // get curve radius

            if (CurrentCurveRadius > 0)  // only check curve speed if it is a curve
            {
                float SpeedToleranceMpS = (float)Size.Length.FromMi(Frequency.Periodic.FromHours(2.5f));  // Set bandwidth tolerance for resetting notifications

                // If super elevation set in Route (TRK) file
                if (simulator.Route.SuperElevationHgtpRadiusM != null)
                {
                    superelevation = (float)simulator.Route.SuperElevationHgtpRadiusM[CurrentCurveRadius];
                }
                else
                {
                    // Set to OR default values
                    if (CurrentCurveRadius > 2000)
                    {
                        double speedLimit;
                        if ((speedLimit = simulator.Route.SpeedLimit) > 55.0)   // If route speed limit is greater then 200km/h, assume high speed passenger route
                        {
                            // Calculate superelevation based upon the route speed limit and the curve radius
                            // SE = ((TrackGauge x Velocity^2 ) / Gravity x curve radius)

                            superelevation = (float)(trackGauge * speedLimit * speedLimit) / (gravitationalAcceleration * CurrentCurveRadius);
                        }
                        else
                        {
                            superelevation = 0.0254f;  // Assume minimal superelevation if conventional mixed route
                        }
                    }
                    // Set Superelevation value - based upon standard figures
                    else if (CurrentCurveRadius <= 2000 & CurrentCurveRadius > 1600)
                    {
                        superelevation = 0.0254f;  // Assume 1" (or 0.0254m)
                    }
                    else if (CurrentCurveRadius <= 1600 & CurrentCurveRadius > 1200)
                    {
                        superelevation = 0.038100f;  // Assume 1.5" (or 0.038100m)
                    }
                    else if (CurrentCurveRadius <= 1200 & CurrentCurveRadius > 1000)
                    {
                        superelevation = 0.050800f;  // Assume 2" (or 0.050800m)
                    }
                    else if (CurrentCurveRadius <= 1000 & CurrentCurveRadius > 800)
                    {
                        superelevation = 0.063500f;  // Assume 2.5" (or 0.063500m)
                    }
                    else if (CurrentCurveRadius <= 800 & CurrentCurveRadius > 600)
                    {
                        superelevation = 0.0889f;  // Assume 3.5" (or 0.0889m)
                    }
                    else if (CurrentCurveRadius <= 600 & CurrentCurveRadius > 500)
                    {
                        superelevation = 0.1016f;  // Assume 4" (or 0.1016m)
                    }
                    // for tighter radius curves assume on branch lines and less superelevation
                    else if (CurrentCurveRadius <= 500 & CurrentCurveRadius > 280)
                    {
                        superelevation = 0.0889f;  // Assume 3" (or 0.0762m)
                    }
                    else if (CurrentCurveRadius <= 280 & CurrentCurveRadius > 0)
                    {
                        superelevation = 0.063500f;  // Assume 2.5" (or 0.063500m)
                    }
                }

#if DEBUG_USER_SUPERELEVATION
                       Trace.TraceInformation(" ============================================= User SuperElevation (TrainCar.cs) ========================================");
                        Trace.TraceInformation("CarID {0} TrackSuperElevation {1} Curve Radius {2}",  CarID, SuperelevationM, CurrentCurveRadius);
#endif

                // Calulate equal wheel loading speed for current curve and superelevation - this was considered the "safe" speed to travel around a curve . In this instance the load on the both railes is evenly distributed.
                // max equal load speed = SQRT ( (superelevation x gravity x curve radius) / track gauge)
                // SuperElevation is made up of two components = rail superelevation + the amount of sideways force that a passenger will be comfortable with. This is expressed as a figure similar to superelevation.

                superelevation = MathHelper.Clamp(superelevation, 0.0001f, 0.150f); // If superelevation is greater then 6" (150mm) then limit to this value, having a value of zero causes problems with calculations

                superElevationAngle = (float)Math.Sinh(superelevation); // Balanced superelevation only angle

                maxCurveEqualLoadSpeed = (float)Math.Sqrt((superelevation * gravitationalAcceleration * CurrentCurveRadius) / trackGauge); // Used for calculating curve resistance

                // Railway companies often allow the vehicle to exceed the equal loading speed, provided that the passengers didn't feel uncomfortable, and that the car was not likely to excced the maximum critical speed
                superElevationTotal = superelevation + unbalancedSuperElevation;

                float SuperElevationTotalAngleRad = (float)Math.Sinh(superElevationTotal); // Total superelevation includes both balanced and unbalanced superelevation

                float MaxSafeCurveSpeedMps = (float)Math.Sqrt((superElevationTotal * gravitationalAcceleration * CurrentCurveRadius) / trackGauge);

                // Calculate critical speed - indicates the speed above which stock will overturn - sum of the moments of centrifrugal force and the vertical weight of the vehicle around the CoG
                // critical speed = SQRT ( (centrifrugal force x gravity x curve radius) / Vehicle weight)
                // centrifrugal force = Stock Weight x factor for movement of resultant force due to superelevation.

                float SinTheta = (float)Math.Sin(superElevationAngle);
                float CosTheta = (float)Math.Cos(superElevationAngle);
                float HalfTrackGaugeM = trackGauge / 2.0f;

                float CriticalMaxSpeedMpS = (float)Math.Sqrt((CurrentCurveRadius * gravitationalAcceleration * (centreOfGravityM.Y * SinTheta + HalfTrackGaugeM * CosTheta)) / (centreOfGravityM.Y * CosTheta - HalfTrackGaugeM * SinTheta));

                float Sin2Theta = 0.5f * (1 - (float)Math.Cos(2.0 * superElevationAngle));
                float CriticalMinSpeedMpS = (float)Math.Sqrt((gravitationalAcceleration * CurrentCurveRadius * HalfTrackGaugeM * Sin2Theta) / (CosTheta * (centreOfGravityM.Y * CosTheta + HalfTrackGaugeM * SinTheta)));

                if (curveSpeedDependent) // Function enabled by menu selection for curve speed limit
                {

                    // This section not required any more???????????
                    // This section tests for the durability value of the consist. Durability value will non-zero if read from consist files. 
                    // Timetable mode does not read consistent durability values for consists, and therefore value will be zero at this time. 
                    // Hence a large value of durability (10.0) is assumed, thus effectively disabling it in TT mode
                    //                        if (Simulator.CurveDurability != 0.0)
                    //                        {
                    //                            MaxDurableSafeCurveSpeedMpS = MaxSafeCurveSpeedMps * Simulator.CurveDurability;  // Finds user setting for durability
                    //                        }
                    //                        else
                    //                        {
                    //                            MaxDurableSafeCurveSpeedMpS = MaxSafeCurveSpeedMps * 10.0f;  // Value of durability has not been set, so set to a large value
                    //                        }

                    // Test current speed to see if greater then equal loading speed around the curve
                    if (s > MaxSafeCurveSpeedMps)
                    {
                        if (!maxSafeCurveSpeedReached)
                        {
                            maxSafeCurveSpeedReached = true; // set flag for IsMaxSafeCurveSpeed reached

                            if (Train.IsPlayerDriven && !simulator.TimetableMode)    // Warning messages will only apply if this is player train and not running in TT mode
                            {
                                if (Train.IsFreight)
                                {
                                    simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("You are travelling too fast for this curve. Slow down, your freight car {0} may be damaged. The recommended speed for this curve is {1}", CarID, FormatStrings.FormatSpeedDisplay(MaxSafeCurveSpeedMps, simulator.MetricUnits)));
                                }
                                else
                                {
                                    simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("You are travelling too fast for this curve. Slow down, your passengers in car {0} are feeling uncomfortable. The recommended speed for this curve is {1}", CarID, FormatStrings.FormatSpeedDisplay(MaxSafeCurveSpeedMps, simulator.MetricUnits)));
                                }

                                if (dbfmaxsafecurvespeedmps != MaxSafeCurveSpeedMps)//Debrief eval
                                {
                                    dbfmaxsafecurvespeedmps = MaxSafeCurveSpeedMps;
                                    ActivityEvaluation.Instance.TravellingTooFast++;
                                }
                            }

                        }
                    }
                    else if (s < MaxSafeCurveSpeedMps - SpeedToleranceMpS)  // Reset notification once spped drops
                    {
                        if (maxSafeCurveSpeedReached)
                        {
                            maxSafeCurveSpeedReached = false; // reset flag for IsMaxSafeCurveSpeed reached - if speed on curve decreases


                        }
                    }

                    // If speed exceeds the overturning speed, then indicated that an error condition has been reached.
                    if (s > CriticalMaxSpeedMpS && Train.GetType() != typeof(AITrain) && Train.GetType() != typeof(TTTrain)) // Breaking of brake hose will not apply to TT mode or AI trains)
                    {
                        if (!criticalMaxSpeedReached)
                        {
                            criticalMaxSpeedReached = true; // set flag for IsCriticalSpeed reached

                            if (Train.IsPlayerDriven && !simulator.TimetableMode)  // Warning messages will only apply if this is player train and not running in TT mode
                            {
                                BrakeSystem.FrontBrakeHoseConnected = false; // break the brake hose connection between cars if the speed is too fast
                                simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("You were travelling too fast for this curve, and have snapped a brake hose on Car " + CarID + ". You will need to repair the hose and restart."));

                                dbfEvalsnappedbrakehose = true;//Debrief eval

                                if (!ldbfevaltrainoverturned)
                                {
                                    ldbfevaltrainoverturned = true;
                                    ActivityEvaluation.Instance.TrainOverTurned++;
                                }
                            }
                        }

                    }
                    else if (s < CriticalMaxSpeedMpS - SpeedToleranceMpS) // Reset notification once speed drops
                    {
                        if (criticalMaxSpeedReached)
                        {
                            criticalMaxSpeedReached = false; // reset flag for IsCriticalSpeed reached - if speed on curve decreases
                            ldbfevaltrainoverturned = false;

                            if (dbfEvalsnappedbrakehose)
                            {
                                ActivityEvaluation.Instance.SnappedBrakeHose++;
                                dbfEvalsnappedbrakehose = false;
                            }

                        }
                    }


                    // This alarm indication comes up even in shunting yard situations where typically no superelevation would be present.
                    // Code is disabled until a bteer way is determined to work out whether track piees are superelevated or not.

                    // if speed doesn't reach minimum speed required around the curve then set notification
                    // Breaking of brake hose will not apply to TT mode or AI trains or if on a curve less then 150m to cover operation in shunting yards, where track would mostly have no superelevation
                    //                        if (s < CriticalMinSpeedMpS && Train.GetType() != typeof(AITrain) && Train.GetType() != typeof(TTTrain) && CurrentCurveRadius > 150 ) 
                    //                       {
                    //                            if (!IsCriticalMinSpeed)
                    //                            {
                    //                                IsCriticalMinSpeed = true; // set flag for IsCriticalSpeed not reached
                    //
                    //                                if (Train.IsPlayerDriven && !Simulator.TimetableMode)  // Warning messages will only apply if this is player train and not running in TT mode
                    //                                {
                    //                                      Simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("You were travelling too slow for this curve, and Car " + CarID + "may topple over."));
                    //                                }
                    //                            }
                    //
                    //                        }
                    //                        else if (s > CriticalMinSpeedMpS + SpeedToleranceMpS) // Reset notification once speed increases
                    //                        {
                    //                            if (IsCriticalMinSpeed)
                    //                            {
                    //                                IsCriticalMinSpeed = false; // reset flag for IsCriticalSpeed reached - if speed on curve decreases
                    //                            }
                    //                        }

#if DEBUG_CURVE_SPEED
                   Trace.TraceInformation("================================== TrainCar.cs - DEBUG_CURVE_SPEED ==============================================================");
                   Trace.TraceInformation("CarID {0} Curve Radius {1} Super {2} Unbalanced {3} Durability {4}", CarID, CurrentCurveRadius, SuperelevationM, UnbalancedSuperElevationM, Simulator.CurveDurability);
                   Trace.TraceInformation("CoG {0}", CentreOfGravityM);
                   Trace.TraceInformation("Current Speed {0} Equal Load Speed {1} Max Safe Speed {2} Critical Max Speed {3} Critical Min Speed {4}", MpS.ToMpH(s), MpS.ToMpH(MaxCurveEqualLoadSpeedMps), MpS.ToMpH(MaxSafeCurveSpeedMps), MpS.ToMpH(CriticalMaxSpeedMpS), MpS.ToMpH(CriticalMinSpeedMpS));
                   Trace.TraceInformation("IsMaxSafeSpeed {0} IsCriticalSpeed {1}", IsMaxSafeCurveSpeed, IsCriticalSpeed);
#endif
                }

            }
            else
            {
                // reset flags if train is on a straight - in preparation for next curve
                criticalMaxSpeedReached = false;   // reset flag for IsCriticalMaxSpeed reached
                criticalMinSpeedReached = false;   // reset flag for IsCriticalMinSpeed reached
                maxSafeCurveSpeedReached = false; // reset flag for IsMaxEqualLoadSpeed reached
            }
        }

        #endregion

        #region Calculate friction force in curves
        /// <summary>
        /// Reads current curve radius and computes the CurveForceN friction. Can be overriden by calling
        /// base.UpdateCurveForce();
        /// CurveForceN *= someCarSpecificCoef;     
        /// </summary>
        public virtual void UpdateCurveForce(double elapsedClockSeconds)
        {
            if (CurrentCurveRadius > 0)
            {

                if (rigidWheelBaseM == 0)   // Calculate default values if no value in Wag File
                {
                    float Axles = WheelAxles.Count;
                    float Bogies = Parts.Count - 1;

                    rigidWheelBaseM = 1.6764f;       // Set a default in case no option is found - assume a standard 4 wheel (2 axle) bogie - wheel base - 5' 6" (1.6764m)

                    // Calculate the number of axles in a car

                    if (WagonType != WagonType.Engine)   // if car is not a locomotive then determine wheelbase
                    {

                        if (Bogies < 2)  // if less then two bogies assume that it is a fixed wheelbase wagon
                        {
                            if (Axles == 2)
                            {
                                rigidWheelBaseM = 3.5052f;       // Assume a standard 4 wheel (2 axle) wagon - wheel base - 11' 6" (3.5052m)
                            }
                            else if (Axles == 3)
                            {
                                rigidWheelBaseM = 3.6576f;       // Assume a standard 6 wheel (3 axle) wagon - wheel base - 12' 2" (3.6576m)
                            }
                        }
                        else if (Bogies == 2)
                        {
                            if (Axles == 2)
                            {
                                if (WagonType == WagonType.Passenger)
                                {

                                    rigidWheelBaseM = 2.4384f;       // Assume a standard 4 wheel passenger bogie (2 axle) wagon - wheel base - 8' (2.4384m)
                                }
                                else
                                {
                                    rigidWheelBaseM = 1.6764f;       // Assume a standard 4 wheel freight bogie (2 axle) wagon - wheel base - 5' 6" (1.6764m)
                                }
                            }
                            else if (Axles == 3)
                            {
                                rigidWheelBaseM = 3.6576f;       // Assume a standard 6 wheel bogie (3 axle) wagon - wheel base - 12' 2" (3.6576m)
                            }
                        }

                    }
                    if (WagonType == WagonType.Engine)   // if car is a locomotive and either a diesel or electric then determine wheelbase
                    {
                        if (EngineType != EngineType.Steam)  // Assume that it is a diesel or electric locomotive
                        {
                            if (Axles == 2)
                            {
                                rigidWheelBaseM = 1.6764f;       // Set a default in case no option is found - assume a standard 4 wheel (2 axle) bogie - wheel base - 5' 6" (1.6764m)
                            }
                            else if (Axles == 3)
                            {
                                rigidWheelBaseM = 3.5052f;       // Assume a standard 6 wheel bogie (3 axle) locomotive - wheel base - 11' 6" (3.5052m)
                            }
                        }
                        else // assume steam locomotive
                        {
                            if (locoNumDrvAxles >= Axles) // Test to see if ENG file value is too big (typically doubled)
                            {
                                locoNumDrvAxles /= 2;  // Appears this might be the number of wheels rather then the axles.
                            }

                            //    Approximation for calculating rigid wheelbase for steam locomotives
                            // Wheelbase = 1.25 x (Loco Drive Axles - 1.0) x Drive Wheel diameter

                            rigidWheelBaseM = 1.25f * (locoNumDrvAxles - 1.0f) * (DriverWheelRadiusM * 2.0f);

                        }

                    }


                }

                // Curve Resistance = (Vehicle mass x Coeff Friction) * (Track Gauge + Vehicle Fixed Wheelbase) / (2 * curve radius)
                // Vehicle Fixed Wheel base is the distance between the wheels, ie bogie or fixed wheels

                CurveForceN = MassKG * Train.WagonCoefficientFriction * (trackGauge + rigidWheelBaseM) / (2.0f * CurrentCurveRadius);
                float CurveResistanceSpeedFactor = Math.Abs((maxCurveEqualLoadSpeed - AbsSpeedMpS) / maxCurveEqualLoadSpeed) * startCurveResistanceFactor;
                CurveForceN *= CurveResistanceSpeedFactor * curveResistanceZeroSpeedFactor;
                CurveForceN *= gravitationalAcceleration; // to convert to Newtons
            }
            else
            {
                CurveForceN = 0f;
            }
            //CurveForceNFiltered = CurveForceFilter.Filter(CurveForceN, elapsedClockSeconds);
            curveForceFilter.Update(elapsedClockSeconds, CurveForceN);
        }

        #endregion

        /// <summary>
        /// Signals an event from an external source (player, multi-player controller, etc.) for this car.
        /// </summary>
        /// <param name="evt"></param>
        public virtual void SignalEvent(TrainEvent evt) { }
        public virtual void SignalEvent(TCSEvent evt) { }
        public virtual void SignalEvent(PowerSupplyEvent evt) { }
        public virtual void SignalEvent(PowerSupplyEvent evt, int id) { }

        private bool wheelHasBeenSet; //indicating that the car shape has been loaded, thus no need to reset the wheels

        protected TrainCar()
        {
            carInfo = new TrainCarInformation(this);
            forceInfo = new TrainCarForceInformation(this);
            powerInfo = new TrainCarPowerSupplyInfo(this);
        }

        protected TrainCar(string wagFile) : this()
        {
            WagFilePath = wagFile;
            RealWagFilePath = wagFile;
        }

        // Game save
        public virtual void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));

            outf.Write(Flipped);
            outf.Write(UiD);
            outf.Write(CarID);
            BrakeSystem.Save(outf);
            outf.Write(MotiveForceN);
            outf.Write(FrictionForceN);
            outf.Write(SpeedMpS);
            outf.Write(CouplerSlackM);
            outf.Write(Headlight);
            outf.Write(OriginalConsist);
            outf.Write(PrevTiltingZRot);
            outf.Write(BrakesStuck);
            outf.Write(carHeatingInitialized);
            outf.Write(steamHoseLeakRateRandom);
            outf.Write(carHeatCurrentCompartmentHeatJ);
            outf.Write(carSteamHeatMainPipeSteamPressurePSI);
            outf.Write(carHeatCompartmentHeaterOn);
        }

        // Game restore
        public virtual void Restore(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            Flipped = inf.ReadBoolean();
            UiD = inf.ReadInt32();
            CarID = inf.ReadString();
            BrakeSystem.Restore(inf);
            MotiveForceN = inf.ReadSingle();
            FrictionForceN = inf.ReadSingle();
            SpeedMpS = inf.ReadSingle();
            prevSpeedMpS = SpeedMpS;
            CouplerSlackM = inf.ReadSingle();
            Headlight = inf.ReadInt32();
            OriginalConsist = inf.ReadString();
            PrevTiltingZRot = inf.ReadSingle();
            BrakesStuck = inf.ReadBoolean();
            carHeatingInitialized = inf.ReadBoolean();
            steamHoseLeakRateRandom = inf.ReadDouble();
            carHeatCurrentCompartmentHeatJ = inf.ReadDouble();
            carSteamHeatMainPipeSteamPressurePSI = inf.ReadDouble();
            carHeatCompartmentHeaterOn = inf.ReadBoolean();
            FreightAnimations?.LoadDataList?.Clear();
        }

        //================================================================================================//
        /// <summary>
        /// Set starting conditions for TrainCars when initial speed > 0 
        /// 

        public virtual void InitializeMoving()
        {
            BrakeSystem.InitializeMoving();
            //TODO: next if/else block has been inserted to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
            // To achieve the same result with other means, without flipping trainset physics, the if/else block should be deleted and replaced by following instruction:
            //            SpeedMpS = Flipped ? -Train.InitialSpeed : Train.InitialSpeed;
            SpeedMpS = this is MSTSLocomotive locomotive && Train.TrainType == TrainType.Player
                ? Flipped ^ locomotive.UsingRearCab ? -Train.InitialSpeed : Train.InitialSpeed
                : Flipped ? -Train.InitialSpeed : Train.InitialSpeed;
            prevSpeedMpS = SpeedMpS;
        }

        public bool HasFrontCab
        {
            get
            {
                int i = (int)CabViewType.Front;
                if (!(this is MSTSLocomotive loco) || loco.CabViewList.Count <= i || loco.CabViewList[i].CabViewType != CabViewType.Front)
                    return false;
                return (loco.CabViewList[i].ViewPointList.Count > 0);
            }
        }

        public bool HasRearCab
        {
            get
            {
                int i = (int)CabViewType.Rear;
                if (!(this is MSTSLocomotive loco) || loco.CabViewList.Count <= i)
                    return false;
                return (loco.CabViewList[i].ViewPointList.Count > 0);
            }
        }

        public bool HasFront3DCab
        {
            get
            {
                int i = (int)CabViewType.Front;
                if (!(this is MSTSLocomotive loco) || loco.CabView3D == null || loco.CabView3D.CabViewType != CabViewType.Front)
                    return false;
                return (loco.CabView3D.ViewPointList.Count > i);
            }
        }

        public bool HasRear3DCab
        {
            get
            {
                int i = (int)CabViewType.Rear;
                if (!(this is MSTSLocomotive loco) || loco.CabView3D == null)
                    return false;
                return (loco.CabView3D.ViewPointList.Count > i);
            }
        }

        ref readonly WorldPosition IWorldPosition.WorldPosition => ref WorldPosition;

        public virtual bool GetCabFlipped()
        {
            return false;
        }

        //<comment>
        //Initializes the physics of the car taking into account its variable discrete loads
        //</comment>
        public void InitializeLoadPhysics()
        {
            // TODO
        }

        //<comment>
        //Updates the physics of the car taking into account its variable discrete loads
        //</comment>
        public void UpdateLoadPhysics()
        {
            // TODO
        }

        public virtual float GetCouplerZeroLengthM()
        {
            return 0;
        }

        public virtual float GetSimpleCouplerStiffnessNpM()
        {
            return 2e7f;
        }
        public virtual float GetCouplerStiffness1NpM()
        {
            return 1e7f;
        }

        public virtual float GetCouplerStiffness2NpM()
        {
            return 1e7f;
        }

        public virtual float GetCouplerSlackAM()
        {
            return 0;
        }

        public virtual float GetCouplerSlackBM()
        {
            return 0.1f;
        }

        public virtual bool GetCouplerRigidIndication()
        {
            return false;
        }

        public virtual float GetMaximumSimpleCouplerSlack1M()
        {
            return 0.03f;
        }

        public virtual float GetMaximumSimpleCouplerSlack2M()
        {
            return 0.035f;
        }

        public virtual float GetMaximumCouplerForceN()
        {
            return 1e10f;
        }

        // Advanced coupler parameters

        public virtual float GetCouplerTensionStiffness1N()
        {
            return 1e7f;
        }

        public virtual float GetCouplerTensionStiffness2N()
        {
            return 2e7f;
        }

        public virtual float GetCouplerCompressionStiffness1N()
        {
            return 1e7f;
        }

        public virtual float GetCouplerCompressionStiffness2N()
        {
            return 2e7f;
        }

        public virtual float GetCouplerTensionSlackAM()
        {
            return 0;
        }

        public virtual float GetCouplerTensionSlackBM()
        {
            return 0.1f;
        }

        public virtual float GetCouplerCompressionSlackAM()
        {
            return 0;
        }

        public virtual float GetCouplerCompressionSlackBM()
        {
            return 0.1f;
        }

        public virtual float GetMaximumCouplerTensionSlack1M()
        {
            return 0.05f;
        }

        public virtual float GetMaximumCouplerTensionSlack2M()
        {
            return 0.1f;
        }

        public virtual float GetMaximumCouplerTensionSlack3M()
        {
            return 0.13f;
        }

        public virtual float GetMaximumCouplerCompressionSlack1M()
        {
            return 0.05f;
        }

        public virtual float GetMaximumCouplerCompressionSlack2M()
        {
            return 0.1f;
        }

        public virtual float GetMaximumCouplerCompressionSlack3M()
        {
            return 0.13f;
        }

        public virtual float GetCouplerBreak1N() // Sets the break force????
        {
            return 1e10f;
        }

        public virtual float GetCouplerBreak2N() // Sets the break force????
        {
            return 1e10f;
        }

        public virtual float GetCouplerTensionR0Y() // Sets the break force????
        {
            return 0.0001f;
        }

        public virtual float GetCouplerCompressionR0Y() // Sets the break force????
        {
            return 0.0001f;
        }

        public virtual void CopyCoupler(TrainCar source)
        {
            CouplerSlackM = source?.CouplerSlackM ?? throw new ArgumentNullException(nameof(source));
            CouplerSlack2M = source.CouplerSlack2M;
        }

        public virtual bool GetAdvancedCouplerFlag()
        {
            return false;
        }

        public virtual void CopyControllerSettings(TrainCar source)
        {
            Headlight = source?.Headlight ?? throw new ArgumentNullException(nameof(source));
        }

        public void AddWheelSet(float offset, int bogieID, int parentMatrix, string wheels, int bogie1Axles, int bogie2Axles)
        {
            if (WheelAxlesLoaded || wheelHasBeenSet)
                return;

            if (string.IsNullOrEmpty(wheels))
                throw new ArgumentNullException(nameof(wheels));

            // Currently looking for rolling stock that has more than 3 axles on a bogie.  This is rare, but some models are like this.
            // In this scenario, bogie1 contains 2 sets of axles.  One of them for bogie2.  Both bogie2 axles must be removed.
            // For the time being, the only rail-car that was having issues had 4 axles on one bogie. The second set of axles had a bogie index of 2 and both had to be dropped for the rail-car to operate under OR.
            if (Parts.Count > 0 && bogie1Axles == 4 || bogie2Axles == 4) // 1 bogie will have a Parts.Count of 2.
            {
                if (Parts.Count == 2 && parentMatrix == Parts[1].Matrix && wheels.Length == 8 && bogie1Axles == 4 && bogieID == 2) // This test is strictly testing for and leaving out axles meant for a Bogie2 assignment.
                    return;

                if (Parts.Count == 3)
                {
                    if (parentMatrix == Parts[1].Matrix && wheels.Length == 8 && bogie1Axles == 4 && bogieID == 2) // This test is strictly testing for and leaving out axles meant for a Bogie2 assignment.
                        return;
                    if (parentMatrix == Parts[2].Matrix && wheels.Length == 8 && bogie2Axles == 4 && bogieID == 1) // This test is strictly testing for and leaving out axles meant for a Bogie1 assignment.
                        return;
                }

            }

            //some old stocks have only two wheels, but defined to have four, two share the same offset, thus all computing of rotations will have problem
            //will check, if so, make the offset different a bit.
            foreach (var axles in WheelAxles)
                if (offset.AlmostEqual(axles.OffsetM, 0.05f))
                { offset = axles.OffsetM + 0.7f; break; }

            // Came across a model where the axle offset that is part of a bogie would become 0 during the initial process.  This is something we must test for.
            if (wheels.Length == 8 && Parts.Count > 0)
            {
                if (wheels == "WHEELS11" || wheels == "WHEELS12" || wheels == "WHEELS13" || wheels == "WHEELS14")
                    WheelAxles.Add(new WheelAxle(offset, bogieID, parentMatrix));

                else if (wheels == "WHEELS21" || wheels == "WHEELS22" || wheels == "WHEELS23" || wheels == "WHEELS24")
                    WheelAxles.Add(new WheelAxle(offset, bogieID, parentMatrix));

                else if (wheels == "WHEELS31" || wheels == "WHEELS32" || wheels == "WHEELS33" || wheels == "WHEELS34")
                    WheelAxles.Add(new WheelAxle(offset, bogieID, parentMatrix));

                else if (wheels == "WHEELS41" || wheels == "WHEELS42" || wheels == "WHEELS43" || wheels == "WHEELS44")
                    WheelAxles.Add(new WheelAxle(offset, bogieID, parentMatrix));
                // This else will cover additional Wheels added following the proper naming convention.
                else
                    WheelAxles.Add(new WheelAxle(offset, bogieID, parentMatrix));
            }
            // The else will cover WHEELS spelling where the length is less than 8.
            else
                WheelAxles.Add(new WheelAxle(offset, bogieID, parentMatrix));

        } // end AddWheelSet()

        public void AddBogie(float offset, int matrix, int id, string bogie, int numBogie1, int numBogie2)
        {
            if (WheelAxlesLoaded || wheelHasBeenSet)
                return;
            foreach (var p in Parts)
                if (p.Bogie && offset.AlmostEqual(p.OffsetM, 0.05f))
                {
                    offset = p.OffsetM + 0.1f;
                    break;
                }
            //    // This was the initial problem.  If the shape file contained only one entry that was labeled as BOGIE2(should be BOGIE1)
            //    // the process would assign 2 to id, causing it to create 2 Parts entries( or 2 bogies) when one was only needed.  It is possible that
            //    // this issue created many of the problems with articulated wagons later on in the process.
            //    // 2 would be assigned to id, not because there were 2 entries, but because 2 was in BOGIE2.
            if ("BOGIE2".Equals(bogie, StringComparison.OrdinalIgnoreCase) && numBogie2 == 1 && numBogie1 == 0)
            {
                id -= 1;
            }
            Parts[id] = new TrainCarPart(offset, matrix, true);

        } // end AddBogie()

        public void SetUpWheels()
        {

#if DEBUG_WHEELS
            Trace.WriteLine(WagFilePath);
            Trace.WriteLine("  length {0,10:F4}", LengthM);
            foreach (var w in WheelAxles)
                Trace.WriteLine("  axle:  bogie  {1,5:F0}  offset {0,10:F4}", w.OffsetM, w.BogieIndex);
            foreach (var p in Parts)
                Trace.WriteLine("  part:  matrix {1,5:F0}  offset {0,10:F4}  weight {2,5:F0}", p.OffsetM, p.iMatrix, p.SumWgt);
#endif
            wheelHasBeenSet = true;
            // No parts means no bogies (always?), so make sure we've got Parts[0] for the car itself.
            if (Parts.Count == 0)
                Parts.Add(TrainCarPart.None);
            // No axles but we have bogies.
            if (WheelAxles.Count == 0 && Parts.Count > 1)
            {
                // Fake the axles by pretending each has 1 axle.
                foreach (var part in Parts)
                    WheelAxles.Add(new WheelAxle(part.OffsetM, part.Matrix, 0));
                Trace.TraceInformation("Wheel axle data faked based on {1} bogies for {0}", WagFilePath, Parts.Count - 1);
            }
            bool articFront = !WheelAxles.Any(a => a.OffsetM < 0);
            bool articRear = !WheelAxles.Any(a => a.OffsetM > 0);
            // Validate the axles' assigned bogies and count up the axles on each bogie.
            if (WheelAxles.Count > 0)
            {
                foreach (var w in WheelAxles)
                {
                    if (w.BogieIndex >= Parts.Count)
                        w.BogieIndex = 0;
                    if (w.BogieMatrix > 0)
                    {
                        for (var i = 0; i < Parts.Count; i++)
                            if (Parts[i].Matrix == w.BogieMatrix)
                            {
                                w.BogieIndex = i;
                                break;
                            }
                    }
                    w.Part = Parts[w.BogieIndex];
                    w.Part.SumWgt++;
                }

                // Make sure the axles are sorted by OffsetM along the car.
                // Attempting to sort car w/o WheelAxles will resort to an error.
                WheelAxles.Sort(WheelAxles[0]);
            }

            //fix bogies with only one wheel set:
            // This process is to fix the bogies that did not pivot under the cab of steam locomotives as well as other locomotives that have this symptom.
            // The cause involved the bogie and axle being close by 0.05f or less on the ZAxis.
            // The ComputePosition() process was unable to work with this.
            // The fix involves first testing for how close they are then moving the bogie offset up.
            // The final fix involves adding an additional axle.  Without this, both bogie and axle would never track properly?
            // Note: Steam locomotive modelers are aware of this issue and are now making sure there is ample spacing between axle and bogie.
            for (var i = 1; i < Parts.Count; i++)
            {
                if (Parts[i].Bogie == true && Parts[i].SumWgt < 1.5)
                {
                    foreach (var w in WheelAxles)
                    {
                        if (w.BogieMatrix == Parts[i].Matrix)
                        {
                            if (w.OffsetM.AlmostEqual(Parts[i].OffsetM, 0.6f))
                            {
                                var w1 = new WheelAxle(w.OffsetM - 0.5f, w.BogieIndex, i);
                                w1.Part = Parts[w1.BogieIndex]; //create virtual wheel
                                w1.Part.SumWgt++;
                                WheelAxles.Add(w1);
                                w.OffsetM += 0.5f; //move the original bogie forward, so we have two bogies to make the future calculation happy
                                Trace.TraceInformation("A virtual wheel axle was added for bogie {1} of {0}", WagFilePath, i);
                                break;
                            }
                        }
                    }
                }
            }

            // Count up the number of bogies (parts) with at least 2 axles.
            for (var i = 1; i < Parts.Count; i++)
                if (Parts[i].SumWgt > 1.5)
                    Parts[0].SumWgt++;

            // This check is for the single axle/bogie issue.
            // Check SumWgt using Parts[0].SumWgt.
            // Certain locomotives do not test well when using Part.SumWgt versus Parts[0].SumWgt.
            // Make sure test using Parts[0] is performed after the above for loop.
            if (!articFront && !articRear && (Parts[0].SumWgt < 1.5))
            {
                foreach (var w in WheelAxles)
                {
                    if (w.BogieIndex >= Parts.Count - 1)
                    {
                        w.BogieIndex = 0;
                        w.Part = Parts[w.BogieIndex];

                    }
                }
            }
            // Using WheelAxles.Count test to control WheelAxlesLoaded flag.
            if (WheelAxles.Count > 2)
            {
                WheelAxles.Sort(WheelAxles[0]);
                WheelAxlesLoaded = true;
            }


#if DEBUG_WHEELS
            Trace.WriteLine(WagFilePath);
            Trace.WriteLine("  length {0,10:F4}", LengthM);
            Trace.WriteLine("  articulated {0}/{1}", articulatedFront, articulatedRear);
            foreach (var w in WheelAxles)
                Trace.WriteLine("  axle:  bogie  {1,5:F0}  offset {0,10:F4}", w.OffsetM, w.BogieIndex);
            foreach (var p in Parts)
                Trace.WriteLine("  part:  matrix {1,5:F0}  offset {0,10:F4}  weight {2,5:F0}", p.OffsetM, p.iMatrix, p.SumWgt);
#endif
            // Decided to control what is sent to SetUpWheelsArticulation()by using
            // WheelAxlesLoaded as a flag.  This way, wagons that have to be processed are included
            // and the rest left out.
            bool articulatedFront = !WheelAxles.Any(a => a.OffsetM < 0);
            bool articulatedRear = !WheelAxles.Any(a => a.OffsetM > 0);
            var carIndex = Train.Cars.IndexOf(this);
            //Certain locomotives are testing as articulated wagons for some reason.
            if (WagonType != WagonType.Engine)
                if (WheelAxles.Count >= 2)
                    if (articulatedFront || articulatedRear)
                    {
                        WheelAxlesLoaded = true;
                        SetUpWheelsArticulation(carIndex);
                    }
        } // end SetUpWheels()

        protected void SetUpWheelsArticulation(int carIndex)
        {
            // If there are no forward wheels, this car is articulated (joined
            // to the car in front) at the front. Likewise for the rear.
            bool articulatedFront = !WheelAxles.Any(a => a.OffsetM < 0);
            bool articulatedRear = !WheelAxles.Any(a => a.OffsetM > 0);
            // Original process originally used caused too many issues.
            // The original process did include the below process of just using WheelAxles.Add
            //  if the initial test did not work.  Since the below process is working without issues the
            //  original process was stripped down to what is below
            if (articulatedFront || articulatedRear)
            {
                if (articulatedFront && WheelAxles.Count <= 3)
                    WheelAxles.Add(new WheelAxle(-CarLengthM / 2, 0, 0) { Part = Parts[0] });

                if (articulatedRear && WheelAxles.Count <= 3)
                    WheelAxles.Add(new WheelAxle(CarLengthM / 2, 0, 0) { Part = Parts[0] });

                WheelAxles.Sort(WheelAxles[0]);
            }
        } // end SetUpWheelsArticulation()

        public void ComputePosition(Traveller traveller, bool backToFront, double elapsedTimeS, double distance, float speed)
        {
            if (null == traveller)
                throw new ArgumentNullException(nameof(traveller));

            for (int j = 0; j < Parts.Count; j++)
                Parts[j].InitLineFit();
            int tileX = traveller.TileX;
            int tileZ = traveller.TileZ;
            if (Flipped == backToFront)
            {
                float o = -CarLengthM / 2 - centreOfGravityM.Z;
                for (int k = 0; k < WheelAxles.Count; k++)
                {
                    float d = WheelAxles[k].OffsetM - o;
                    o = WheelAxles[k].OffsetM;
                    traveller.Move(d);
                    float x = traveller.X + 2048 * (traveller.TileX - tileX);
                    float y = traveller.Y;
                    float z = traveller.Z + 2048 * (traveller.TileZ - tileZ);
                    WheelAxles[k].Part.AddWheelSetLocation(1, o, x, y, z, 0);
                }
                o = CarLengthM / 2 - centreOfGravityM.Z - o;
                traveller.Move(o);
            }
            else
            {
                float o = CarLengthM / 2 - centreOfGravityM.Z;
                for (int k = WheelAxles.Count - 1; k >= 0; k--)
                {
                    float d = o - WheelAxles[k].OffsetM;
                    o = WheelAxles[k].OffsetM;
                    traveller.Move(d);
                    float x = traveller.X + 2048 * (traveller.TileX - tileX);
                    float y = traveller.Y;
                    float z = traveller.Z + 2048 * (traveller.TileZ - tileZ);
                    WheelAxles[k].Part.AddWheelSetLocation(1, o, x, y, z, 0);
                }
                o = CarLengthM / 2 + centreOfGravityM.Z + o;
                traveller.Move(o);
            }

            TrainCarPart p0 = Parts[0];
            for (int i = 1; i < Parts.Count; i++)
            {
                TrainCarPart p = Parts[i];
                p.FindCenterLine();
                if (p.SumWgt > 1.5)
                    p0.AddPartLocation(1, p);
            }
            p0.FindCenterLine();
            Vector3 fwd = new Vector3(p0.B[0], p0.B[1], -p0.B[2]);
            // Check if null vector - The Length() is fine also, but may be more time consuming - By GeorgeS
            if (fwd.X != 0 && fwd.Y != 0 && fwd.Z != 0)
                fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.Up, fwd);
            // Check if null vector - The Length() is fine also, but may be more time consuming - By GeorgeS
            if (side.X != 0 && side.Y != 0 && side.Z != 0)
                side.Normalize();
            Vector3 up = Vector3.Cross(fwd, side);
            Matrix m = Matrix.Identity;
            m.M11 = side.X;
            m.M12 = side.Y;
            m.M13 = side.Z;
            m.M21 = up.X;
            m.M22 = up.Y;
            m.M23 = up.Z;
            m.M31 = fwd.X;
            m.M32 = fwd.Y;
            m.M33 = fwd.Z;
            m.M41 = p0.A[0];
            m.M42 = p0.A[1] + 0.275f;
            m.M43 = -p0.A[2];
            worldPosition = new WorldPosition(tileX, tileZ, m);

            UpdatedTraveller(traveller, elapsedTimeS, distance, speed);

            // calculate truck angles
            for (int i = 1; i < Parts.Count; i++)
            {
                TrainCarPart p = Parts[i];
                if (p.SumWgt < .5)
                    continue;
                if (p.SumWgt < 1.5)
                {   // single axle pony trunk
                    double d = p.OffsetM - p.SumOffset / p.SumWgt;
                    if (-.2 < d && d < .2)
                        continue;
                    p.AddWheelSetLocation(1, p.OffsetM, p0.A[0] + p.OffsetM * p0.B[0], p0.A[1] + p.OffsetM * p0.B[1], p0.A[2] + p.OffsetM * p0.B[2], 0);
                    p.FindCenterLine();
                }
                Vector3 fwd1 = new Vector3(p.B[0], p.B[1], -p.B[2]);
                if (fwd1.X == 0 && fwd1.Y == 0 && fwd1.Z == 0)
                {
                    p.Cos = 1;
                }
                else
                {
                    fwd1.Normalize();
                    p.Cos = Vector3.Dot(fwd, fwd1);
                }

                if (p.Cos >= .99999f)
                    p.Sin = 0;
                else
                {
                    p.Sin = (float)Math.Sqrt(1 - p.Cos * p.Cos);
                    if (fwd.X * fwd1.Z < fwd.Z * fwd1.X)
                        p.Sin = -p.Sin;
                }
            }
        }

        #region Traveller-based updates
        public float CurrentCurveRadius { get; private set; }

        internal void UpdatedTraveller(Traveller traveller, double elapsedTimeS, double distanceM, float speedMpS)
        {
            // We need to avoid introducing any unbounded effects, so cap the elapsed time to 0.25 seconds (4FPS).
            if (elapsedTimeS > 0.25)
                return;

            CurrentCurveRadius = traveller.CurveRadius();
            UpdateVibrationAndTilting(traveller, elapsedTimeS, distanceM, speedMpS);
            UpdateSuperElevation(traveller, elapsedTimeS);
        }

        internal protected virtual void UpdateRemotePosition(double elapsedClockSeconds, float speed, float targetSpeed)
        {
            SpeedMpS = speed;
            if (Flipped)
                SpeedMpS = -SpeedMpS;
            AbsSpeedMpS = (float)(AbsSpeedMpS * (1 - elapsedClockSeconds) + targetSpeed * elapsedClockSeconds);
        }

        #endregion

        #region Super-elevation
        private void UpdateSuperElevation(Traveller traveller, double elapsedTimeS)
        {
            if (simulator.Settings.UseSuperElevation == 0)
                return;
            if (prevElev < -30f)
            { prevElev += 40f; return; }//avoid the first two updates as they are not valid

            // Because the traveler is at the FRONT of the TrainCar, smooth the super-elevation out with the rear.
            float z = traveller.GetSuperElevation(-CarLengthM);
            if (Flipped)
                z *= -1;
            // TODO This is a hack until we fix the super-elevation code as described in http://www.elvastower.com/forums/index.php?/topic/28751-jerky-superelevation-effect/
            if (prevElev < -10f || prevElev > 10f)
                prevElev = z;//initial, will jump to the desired value
            else
            {
                z = prevElev + (z - prevElev) * (float)Math.Min(elapsedTimeS, 1);//smooth rotation
                prevElev = z;
            }

            worldPosition = new WorldPosition(WorldPosition.TileX, WorldPosition.TileZ, MatrixExtension.Multiply(Matrix.CreateRotationZ(z), WorldPosition.XNAMatrix));
        }
        #endregion

        #region Vibration and tilting
        public Matrix VibrationInverseMatrix { get; private set; } = Matrix.Identity;

        // https://en.wikipedia.org/wiki/Newton%27s_laws_of_motion#Newton.27s_2nd_Law
        //   Let F be the force in N
        //   Let m be the mass in kg
        //   Let a be the acceleration in m/s/s
        //   Then F = m * a
        // https://en.wikipedia.org/wiki/Hooke%27s_law
        //   Let F be the force in N
        //   Let k be the spring constant in N/m or kg/s/s
        //   Let x be the displacement in m
        //   Then F = k * x
        // If we assume that gravity is 9.8m/s/s, then the force needed to support the train car is:
        //   F = m * 9.8
        // If we assume that the train car suspension allows for 0.2m (20cm) of travel, then substitute Hooke's law:
        //   m * 9.8 = k * 0.2
        //   k = m * 9.8 / 0.2
        // Finally, we assume a mass (m) of 1kg to calculate a mass-independent value:
        //   k' = 9.8 / 0.2
        private const float VibrationSpringConstantPrimepSpS = 9.8f / 0.2f; // 1/s/s

        // 
        private const float VibratioDampingCoefficient = 0.01f;

        // This is multiplied by the CarVibratingLevel (which goes up to 3).
        private const float VibrationIntroductionStrength = 0.03f;

        // The tightest curve we care about has a radius of 100m. This is used as the basis for the most violent vibrations.
        private const float VibrationMaximumCurvaturepM = 1f / 100;
        private const float VibrationFactorDistance = 1;
        private const float VibrationFactorTrackVectorSection = 2;
        private const float VibrationFactorTrackNode = 4;
        private Vector3 VibrationOffsetM;
        private Vector3 VibrationRotationRad;
        private Vector3 VibrationRotationVelocityRadpS;
        private Vector2 VibrationTranslationM;
        private Vector2 VibrationTranslationVelocityMpS;
        private int VibrationTrackNode;
        private int VibrationTrackVectorSection;
        private float VibrationTrackCurvaturepM;
        private float PrevTiltingZRot; // previous tilting angle
        private float TiltingZRot; // actual tilting angle

        internal void UpdateVibrationAndTilting(Traveller traveler, double elapsedTimeS, double distanceM, float speedMpS)
        {
            // NOTE: Traveller is at the FRONT of the TrainCar!

            // Don't add vibrations to train cars less than 2.5 meter in length; they're unsuitable for these calculations.
            if (CarLengthM < 2.5f)
                return;
            if (simulator.Settings.CarVibratingLevel != 0)
            {

                //var elapsedTimeS = Math.Abs(speedMpS) > 0.001f ? distanceM / speedMpS : 0;
                if (VibrationOffsetM.X == 0)
                {
                    // Initialize three different offsets (0 - 1 meters) so that the different components of the vibration motion don't align.
                    VibrationOffsetM.X = (float)(StaticRandom.NextDouble());
                    VibrationOffsetM.Y = (float)(StaticRandom.NextDouble());
                    VibrationOffsetM.Z = (float)(StaticRandom.NextDouble());
                }

                if (VibrationTrackVectorSection == 0)
                    VibrationTrackVectorSection = traveler.TrackVectorSectionIndex;
                if (VibrationTrackNode == 0)
                    VibrationTrackNode = traveler.TrackNode.Index;

                // Apply suspension/spring and damping.
                // https://en.wikipedia.org/wiki/Simple_harmonic_motion
                //   Let F be the force in N
                //   Let k be the spring constant in N/m or kg/s/s
                //   Let x be the displacement in m
                //   Then F = -k * x
                // Given F = m * a, solve for a:
                //   a = F / m
                // Substitute F:
                //   a = -k * x / m
                // Because our spring constant was never multiplied by m, we can cancel that out:
                //   a = -k' * x
                var rotationAccelerationRadpSpS = -VibrationSpringConstantPrimepSpS * VibrationRotationRad;
                var translationAccelerationMpSpS = -VibrationSpringConstantPrimepSpS * VibrationTranslationM;
                // https://en.wikipedia.org/wiki/Damping
                //   Let F be the force in N
                //   Let c be the damping coefficient in N*s/m
                //   Let v be the velocity in m/s
                //   Then F = -c * v
                // We apply the acceleration (let t be time in s, then dv/dt = a * t) and damping (-c * v) to the velocities:
                VibrationRotationVelocityRadpS += rotationAccelerationRadpSpS * (float)elapsedTimeS - VibratioDampingCoefficient * VibrationRotationVelocityRadpS;
                VibrationTranslationVelocityMpS += translationAccelerationMpSpS * (float)elapsedTimeS - VibratioDampingCoefficient * VibrationTranslationVelocityMpS;
                // Now apply the velocities (dx/dt = v * t):
                VibrationRotationRad += VibrationRotationVelocityRadpS * (float)elapsedTimeS;
                VibrationTranslationM += VibrationTranslationVelocityMpS * (float)elapsedTimeS;

                // Add new vibrations every CarLengthM in either direction.
                if (Math.Round((VibrationOffsetM.X + DistanceTravelled) / CarLengthM) != Math.Round((VibrationOffsetM.X + DistanceTravelled + distanceM) / CarLengthM))
                {
                    AddVibrations(VibrationFactorDistance);
                }

                // Add new vibrations every track vector section which changes the curve radius.
                if (VibrationTrackVectorSection != traveler.TrackVectorSectionIndex)
                {
                    float curvaturepM = MathHelper.Clamp(traveler.GetCurvature(), -VibrationMaximumCurvaturepM, VibrationMaximumCurvaturepM);
                    if (VibrationTrackCurvaturepM != curvaturepM)
                    {
                        // Use the difference in curvature to determine the strength of the vibration caused.
                        AddVibrations(VibrationFactorTrackVectorSection * Math.Abs(VibrationTrackCurvaturepM - curvaturepM) / VibrationMaximumCurvaturepM);
                        VibrationTrackCurvaturepM = curvaturepM;
                    }
                    VibrationTrackVectorSection = traveler.TrackVectorSectionIndex;
                }

                // Add new vibrations every track node.
                if (VibrationTrackNode != traveler.TrackNode.Index)
                {
                    AddVibrations(VibrationFactorTrackNode);
                    VibrationTrackNode = traveler.TrackNode.Index;
                }
            }
            if (Train != null && Train.IsTilting)
            {
                TiltingZRot = traveler.FindTiltedZ(speedMpS);//rotation if tilted, an indication of centrifugal force
                TiltingZRot = PrevTiltingZRot + (TiltingZRot - PrevTiltingZRot) * (float)elapsedTimeS;//smooth rotation
                PrevTiltingZRot = TiltingZRot;
                if (this.Flipped)
                    TiltingZRot *= -1f;
            }
            if (simulator.Settings.CarVibratingLevel != 0 || Train.IsTilting)
            {
                var rotation = Matrix.CreateFromYawPitchRoll(VibrationRotationRad.Y, VibrationRotationRad.X, VibrationRotationRad.Z + TiltingZRot);
                var translation = Matrix.CreateTranslation(VibrationTranslationM.X, VibrationTranslationM.Y, 0);
                worldPosition = new WorldPosition(WorldPosition.TileX, WorldPosition.TileZ, MatrixExtension.Multiply(MatrixExtension.Multiply(rotation, translation), WorldPosition.XNAMatrix));
                VibrationInverseMatrix = Matrix.Invert(rotation * translation);
            }
        }

        private void AddVibrations(float factor)
        {
            // NOTE: For low angles (as our vibration rotations are), sin(angle) ~= angle, and since the displacement at the end of the car is sin(angle) = displacement/half-length, sin(displacement/half-length) * half-length ~= displacement.
            switch (StaticRandom.Next(4))
            {
                case 0:
                    VibrationRotationVelocityRadpS.Y += factor * simulator.Settings.CarVibratingLevel * VibrationIntroductionStrength * 2 / CarLengthM;
                    break;
                case 1:
                    VibrationRotationVelocityRadpS.Z += factor * simulator.Settings.CarVibratingLevel * VibrationIntroductionStrength * 2 / CarLengthM;
                    break;
                case 2:
                    VibrationTranslationVelocityMpS.X += factor * simulator.Settings.CarVibratingLevel * VibrationIntroductionStrength;
                    break;
                case 3:
                    VibrationTranslationVelocityMpS.Y += factor * simulator.Settings.CarVibratingLevel * VibrationIntroductionStrength;
                    break;
            }
        }
        #endregion

        /// <summary>
        /// Checks if traincar is over trough. Used to check if refill possible
        /// </summary>
        /// <returns> returns true if car is over trough</returns>
        protected bool IsOverTrough()
        {
            bool overTrough = false;
            // start at front of train
            int sectionIndex = Train.PresentPosition[Common.Direction.Forward].TrackCircuitSectionIndex;
            if (sectionIndex < 0)
                return overTrough;
            float sectionOffset = Train.PresentPosition[Common.Direction.Forward].Offset;
            TrackDirection sectionDirection = Train.PresentPosition[Common.Direction.Forward].Direction;

            float usedCarLength = CarLengthM;
            float processedCarLength = 0;
            bool validSections = true;

            while (validSections)
            {
                TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[sectionIndex];
                overTrough = false;

                // car spans sections
                if ((CarLengthM - processedCarLength) > sectionOffset)
                {
                    usedCarLength = sectionOffset - processedCarLength;
                }

                // section has troughs
                foreach (TroughInfoData trough in section.TroughInfo ?? Enumerable.Empty<TroughInfoData>())
                {
                    float troughStartOffset = trough.Start[sectionDirection];
                    float troughEndOffset = trough.End[sectionDirection];

                    if (troughStartOffset > 0 && troughStartOffset > sectionOffset)      // start of trough is in section beyond present position - cannot be over this trough nor any following
                    {
                        return overTrough;
                    }

                    if (troughEndOffset > 0 && troughEndOffset < (sectionOffset - usedCarLength)) // beyond end of trough, test next
                    {
                        continue;
                    }

                    if (troughStartOffset <= 0 || troughStartOffset < (sectionOffset - usedCarLength)) // start of trough is behind
                    {
                        overTrough = true;
                        return overTrough;
                    }
                }

                // tested this section, any need to go beyond?
                processedCarLength += usedCarLength;
                // go back one section
                int sectionRouteIndex = Train.ValidRoute[0].GetRouteIndexBackward(sectionIndex, Train.PresentPosition[Common.Direction.Forward].RouteListIndex);
                if (sectionRouteIndex >= 0)
                {
                    sectionIndex = sectionRouteIndex;
                    section = TrackCircuitSection.TrackCircuitList[sectionIndex];
                    sectionOffset = section.Length;  // always at end of next section
                    sectionDirection = Train.ValidRoute[0][sectionRouteIndex].Direction;
                }
                else // ran out of train
                {
                    validSections = false;
                }
            }
            return overTrough;
        }

        /// <summary>
        /// Checks if traincar is over junction or crossover. Used to check if water scoop breaks
        /// </summary>
        /// <returns> returns true if car is over junction</returns>
        protected bool IsOverJunction()
        {

            if (Train.PresentPosition[Common.Direction.Forward].TrackCircuitSectionIndex != Train.PresentPosition[Common.Direction.Backward].TrackCircuitSectionIndex)
            {
                foreach (TrackCircuitSection section in Train.OccupiedTrack)
                {
                    if (section.CircuitType == TrackCircuitType.Junction || section.CircuitType == TrackCircuitType.Crossover)
                    {
                        // train is on a switch; let's see if car is on a switch too
                        ref readonly WorldLocation switchLocation = ref RuntimeData.Instance.TrackDB.TrackNodes[section.OriginalIndex].UiD.Location;
                        double distanceFromSwitch = WorldLocation.GetDistanceSquared(WorldPosition.WorldLocation, switchLocation);
                        if (distanceFromSwitch < CarLengthM * CarLengthM + Math.Min(SpeedMpS * 3, 150))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public virtual void SwitchToPlayerControl()
        {
            return;
        }

        public virtual void SwitchToAutopilotControl()
        {
            return;
        }

        public virtual float GetFilledFraction(PickupType pickupType)
        {
            return 0f;
        }

        public virtual float GetUserBrakeShoeFrictionFactor()
        {
            return 0f;
        }

        public virtual float GetZeroUserBrakeShoeFrictionFactor()
        {
            return 0f;
        }

        /// changes the coupler force equation for car to make the corresponding force equal to forceN
        internal void SetCouplerForce(float forceN)
        {
            CouplerForceA = CouplerForceC = 0;
            CouplerForceB = 1;
            CouplerForceR = forceN;
        }

        internal void SetBrakeForce(float brakeForce)
        {
            BrakeRetardForceN = brakeForce * brakeShoeRetardCoefficientFrictionAdjFactor; // calculates value of force applied to wheel, independent of wheel skid
            if (BrakeSkid) // Test to see if wheels are skiding due to excessive brake force
            {
                BrakeForceN = brakeForce * SkidFriction;   // if excessive brakeforce, wheel skids, and loses adhesion
            }
            else
            {
                BrakeForceN = brakeForce * brakeShoeCoefficientFrictionAdjFactor; // In advanced adhesion model brake shoe coefficient varies with speed, in simple model constant force applied as per value in WAG file, will vary with wheel skid.
            }
        }

        protected virtual void InitializeCarHeatingVariables()
        {
            MSTSLocomotive mstsLocomotive = Train.LeadLocomotive as MSTSLocomotive;

            // Only initialise these values the first time around the loop
            if (!carHeatingInitialized)
            {
                if (mstsLocomotive.EngineType == EngineType.Steam && simulator.Settings.HotStart || mstsLocomotive.EngineType == EngineType.Diesel || mstsLocomotive.EngineType == EngineType.Electric)
                {
                    if (CarOutsideTempC < DesiredCompartmentTempSetpointC)
                    {
                        CarInsideTempC = DesiredCompartmentTempSetpointC; // Set intial temp
                    }
                    else
                    {
                        CarInsideTempC = CarOutsideTempC;
                    }
                }
                else
                {
                    CarInsideTempC = CarOutsideTempC;
                }

                // Calculate a random factor for steam heat leaks in connecting pipes
                steamHoseLeakRateRandom = StaticRandom.Next(100) / 100.0f; // Achieves a two digit random number between 0 and 1
                steamHoseLeakRateRandom = Math.Clamp(steamHoseLeakRateRandom, 0.5f, 1.0f); // Keep Random Factor ratio within bounds

                // Initialise current Train Steam Heat based upon selected Current carriage Temp
                // Calculate Starting Heat value in Car Q = C * M * Tdiff, where C = Specific heat capacity, M = Mass ( Volume * Density), Tdiff - difference in temperature
                carHeatCurrentCompartmentHeatJ = Energy.Transfer.FromKJ(Const.AirDensityBySpecificHeatCapacity * CarHeatVolumeM3 * (CarInsideTempC - CarOutsideTempC));

                carHeatingInitialized = true;
            }
        }

        private static readonly double convHeatTxfMinSpeed = 10.45 - LowSpeed + (10.0 * Math.Pow(LowSpeed, 0.5));
        private double ConvectionFactor
        {
            get
            {
                double convHeatTxActualSpeed = 10.45 - AbsSpeedMpS + (10.0 * Math.Pow(AbsSpeedMpS, 0.5));
                double convFactor = (AbsSpeedMpS >= LowSpeed) ? convHeatTxActualSpeed / convHeatTxfMinSpeed : 1.0f; // If speed less then 2m/s then set fraction to give stationary Kc value 
                return Math.Clamp(convFactor, 1.0f, 1.6f); // Keep Conv Factor ratio within bounds - should not exceed 1.6.
            }
        }

        /// Update Steam Heating - this model calculates the total train heat losses and gains for all the cars
        internal void UpdateSteamHeat(double elapsedClockSeconds, MSTSLocomotive locomotive, ref bool lowSteamHeat, ref double progressiveHeatAlongTrainBTU, ref double steamFlowRateLbpHr)
        {
            // Only initialise these values the first time around the loop
            if (!carHeatingInitialized)
            {
                InitializeCarHeatingVariables();
            }

            if (WagonType == WagonType.Passenger || WagonSpecialType == WagonSpecialType.Heated) // Only calculate compartment heat in passenger or specially marked heated cars
            {
                UpdateHeatLoss();

                //++++++++++++++++++++++++++++++++++++++++
                // Calculate heat produced by steam pipe acting as heat exchanger inside carriage - this model is based upon the heat loss from a steam pipe. 
                // The heat loss per metre from a bare pipe equals the heat loss by convection and radiation. Temperatures in degrees Kelvin
                // QConv = hc * A * (Tp - To), where hc = convection coeff, A = surface area of pipe, Tp = pipe temperature, To = temperature of air around the pipe
                // QRad = % * A * e * (Tp^4 - To^4), where % = Boltzmans constant, A = surface area of pipe, Tp^4 = pipe temperature, To^4 = temperature of air around the pipe, e = emissivity factor

                // Calculate steam pipe surface area
                double compartmentSteamPipeRadiusM = Size.Length.FromIn(2.375) / 2.0;  // Assume the steam pipes in the compartments have  have internal diameter of 2" (50mm) - external = 2.375"
                double doorSteamPipeRadiusM = Size.Length.FromIn(2.75) / 2.0;        // Assume the steam pipes in the doors have diameter of 1.75" (50mm) - assume external = 2.0"

                // Assume door pipes are 3' 4" (ie 3.3') long, and that there are doors at both ends of the car, ie x 2
                double carDoorLengthM = 2.0 * Size.Length.FromFt(3.3f);
                double carDoorVolumeM3 = CarWidthM * carDoorLengthM * CarHeightMinusBogie;

                double carDoorPipeAreaM2 = 2.0 * Math.PI * doorSteamPipeRadiusM * carDoorLengthM;

                // Use rule of thumb - 1" of 2" steam heat pipe for every 3.0 cu ft of volume in car compartment (third class)
                double carCompartmentPipeLengthM = Size.Length.FromIn((CarHeatVolumeM3 - carDoorVolumeM3) / Size.Volume.FromFt3(compartmentHeatingPipeAreaFactor));
                double carCompartmentPipeAreaM2 = 2.0 * Math.PI * compartmentSteamPipeRadiusM * carCompartmentPipeLengthM;

                carHeatCompartmentPipeAreaM2 = carCompartmentPipeAreaM2 + carDoorPipeAreaM2;

                // Pipe convection heat produced - steam is reduced to atmospheric pressure when it is injected into compartment
                double compartmentSteamPipeTempC = Temperature.Celsius.FromF(locomotive.SteamHeatPressureToTemperaturePSItoF[0]);
                carCompartmentSteamPipeHeatConvW = Const.PipeHeatTransCoeffWpM2K * carHeatCompartmentPipeAreaM2 * (compartmentSteamPipeTempC - CarInsideTempC);

                // Pipe radiation heat produced
                double pipeTempAK = Math.Pow(Temperature.Kelvin.FromF(compartmentSteamPipeTempC), 4.0);
                double pipeTempBK = Math.Pow(Temperature.Kelvin.FromC(CarInsideTempC), 4.0);
                carCompartmentSteamHeatPipeRadW = Const.BoltzmanConstPipeWpM2 * Const.EmissivityFactor * carHeatCompartmentPipeAreaM2 * (pipeTempAK - pipeTempBK);

                carHeatCompartmentSteamPipeHeatW = carCompartmentSteamHeatPipeRadW + carCompartmentSteamPipeHeatConvW;
            }

            //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // Calculate heating loss in main supply pipe that runs under carriage

            // Set heat trans coeff
            double heatTransCoeffMainPipeBTUpFt2pHrpF = 0.4 * ConvectionFactor; // insulated pipe - BTU / sq.ft. / hr / l in / °F.
            double heatTransCoeffConnectHoseBTUpFt2pHrpF = 0.04 * ConvectionFactor; // rubber connecting hoses - BTU / sq.ft. / hr / l in / °F. TO BE CHECKED

            // Calculate Length of carriage and heat loss in main steam pipe
            double carMainSteamPipeTempF = locomotive.SteamHeatPressureToTemperaturePSItoF[carSteamHeatMainPipeSteamPressurePSI];
            carHeatSteamMainPipeHeatLossBTU = Size.Length.ToFt(CarLengthM) * (Math.PI * Size.Length.ToFt(mainSteamHeatPipeOuterDiaM)) * heatTransCoeffMainPipeBTUpFt2pHrpF * (carMainSteamPipeTempF - Temperature.Celsius.ToF(CarOutsideTempC));

            // calculate steam connecting hoses heat loss - assume 1.5" hose
            double connectSteamHoseOuterDiaFt = Size.Length.ToFt(carConnectSteamHoseOuterDiaM);
            carHeatConnectSteamHoseHeatLossBTU = ConnectSteamHoseLengthFt * (Math.PI * connectSteamHoseOuterDiaFt) * heatTransCoeffConnectHoseBTUpFt2pHrpF * (carMainSteamPipeTempF - Temperature.Celsius.ToF(CarOutsideTempC));

            double steamTrapValveSizeAreaIn2 = Math.PI * (SteamTrapDiaIn / 2.0) * (SteamTrapDiaIn / 2.0);

            carHeatSteamTrapUsageLBpS = steamTrapValveSizeAreaIn2 * (carSteamHeatMainPipeSteamPressurePSI + Const.OneAtmospherePSI) / SteamTrapValveDischargeFactor;

            double connectingHoseLeakAreaIn2 = Math.PI * (ConnectingHoseLeakDiaIn / 2.0f) * (ConnectingHoseLeakDiaIn / 2.0f);

            carHeatConnectingSteamHoseLeakageLBpS = steamHoseLeakRateRandom * (connectingHoseLeakAreaIn2 * (carSteamHeatMainPipeSteamPressurePSI + Const.OneAtmospherePSI)) / ConnectingHoseDischargeFactor;

            //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            double currentComparmentSteamPipeHeatW;

            // Calculate total steam loss along main pipe, by calculating heat into steam pipe at locomotive, deduct heat loss for each car, 
            // note if pipe pressure drops, then compartment heating will stop
            if (carSteamHeatMainPipeSteamPressurePSI >= 1 && carHeatCompartmentHeaterOn && (WagonType == WagonType.Passenger || WagonSpecialType == WagonSpecialType.Heated))
            {
                // If main pipe pressure is > 0 then heating will start to occur in comparment, so include compartment heat exchanger value
                progressiveHeatAlongTrainBTU += (float)(carHeatSteamMainPipeHeatLossBTU + carHeatConnectSteamHoseHeatLossBTU + Frequency.Periodic.ToHours(Dynamics.Power.ToBTUpS(carHeatCompartmentSteamPipeHeatW)));
                currentComparmentSteamPipeHeatW = carHeatCompartmentSteamPipeHeatW; // Car is being heated as main pipe pressure is high enough, and temperature increase is required
                steamHeatingCompartmentSteamTrapOn = true; // turn on the compartment steam traps
            }
            else
            {
                // If main pipe pressure is < 0 or temperature in compartment is above the desired temeperature,
                // then no heating will occur in comparment, so leave compartment heat exchanger value out
                progressiveHeatAlongTrainBTU += carHeatSteamMainPipeHeatLossBTU + carHeatConnectSteamHoseHeatLossBTU;
                currentComparmentSteamPipeHeatW = 0; // Car is not being heated as main pipe pressure is not high enough, or car temp is hot enough
                steamHeatingCompartmentSteamTrapOn = false; // turn off the compartment steam traps
            }

            // Calculate steam flow rates and steam used
            steamFlowRateLbpHr = (progressiveHeatAlongTrainBTU / locomotive.SteamHeatPSItoBTUpLB[locomotive.CurrentSteamHeatPressurePSI]) + Frequency.Periodic.ToHours(carHeatSteamTrapUsageLBpS) + Frequency.Periodic.ToHours(carHeatConnectingSteamHoseLeakageLBpS);
            locomotive.CalculatedCarHeaterSteamUsageLBpS = (float)Frequency.Periodic.FromHours(steamFlowRateLbpHr);

            // Calculate Net steam heat loss or gain for each compartment in the car
            carNetHeatFlowRateW = currentComparmentSteamPipeHeatW - totalCarCompartmentHeatLossW;

            // Given the net heat loss the car calculate the current heat capacity, and corresponding temperature
            carHeatCurrentCompartmentHeatJ += carNetHeatFlowRateW * (float)elapsedClockSeconds;

            CarInsideTempC = Energy.Transfer.ToKJ(carHeatCurrentCompartmentHeatJ) / (Const.AirDensityBySpecificHeatCapacity * CarHeatVolumeM3) + CarOutsideTempC;

            if (CarInsideTempC > DesiredCompartmentTempSetpointC)
            {
                carHeatCompartmentHeaterOn = false;
            }
            else if (CarInsideTempC < DesiredCompartmentTempSetpointC - 2.5f) // Allow 2.5Deg bandwidth for temperature
            {
                carHeatCompartmentHeaterOn = true;
            }

            if (CarInsideTempC < desiredCompartmentAlarmTempSetpointC) // If temp below 45of then alarm
            {
                if (!lowSteamHeat)
                {
                    lowSteamHeat = true;
                    // Provide warning message if temperature is too cold
                    if (WagonType == WagonType.Passenger)
                    {
                        simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Carriage {0} temperature is too cold, the passengers are freezing.", CarID));
                    }
                    else
                    {
                        simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Car {0} temperature is too cold for the freight.", CarID));
                    }
                }
            }
            else if (CarInsideTempC > Temperature.Celsius.FromF(65.0f))
            {
                lowSteamHeat = false;        // Reset temperature warning
            }
        }

        internal void UpdateElectricHeatingAndAirConditioning(double elapsedClockSeconds)
        {
            // Only initialise these values the first time around the loop
            if (!carHeatingInitialized)
            {
                InitializeCarHeatingVariables();
            }

            if (PowerSupply is ScriptedPassengerCarPowerSupply passengerCarPowerSupply)
            {
                UpdateHeatLoss();

                // Calculate Net steam heat loss or gain for each compartment in the car
                carNetHeatFlowRateW = passengerCarPowerSupply.HeatFlowRateW - totalCarCompartmentHeatLossW;

                // Given the net heat loss the car calculate the current heat capacity, and corresponding temperature
                carHeatCurrentCompartmentHeatJ += carNetHeatFlowRateW * elapsedClockSeconds;

                CarInsideTempC = (Energy.Transfer.ToKJ(carHeatCurrentCompartmentHeatJ) / (Const.AirDensityBySpecificHeatCapacity * CarHeatVolumeM3) + CarOutsideTempC);
            }
        }

        internal void UpdateSteamPressureDrop(double elapsedClockSeconds, MSTSLocomotive locomotive, double steamFlowRateLbpHr, ref double progressivePressureAlongTrainPSI)
        {                     // Calculate pressure drop in pipe along train. This calculation is based upon the Unwin formula - https://www.engineeringtoolbox.com/steam-pressure-drop-calculator-d_1093.html
                              // dp = 0.0001306 * q^2 * L * (1 + 3.6/d) / (3600 * ρ * d^5)
                              // where dp = pressure drop (psi), q = steam flow rate(lb/ hr), L = length of pipe(ft), d = pipe inside diameter(inches), ρ = steam density(lb / ft3)
                              // Use values for the specific volume corresponding to the average pressure if the pressure drop exceeds 10 - 15 % of the initial absolute pressure

            double heatPipePressureDropPSI = 0.0001306 * steamFlowRateLbpHr * steamFlowRateLbpHr * Size.Length.ToFt(CarLengthM) * (1 + 3.6 / 2.5) / (3600 * locomotive.SteamDensityPSItoLBpFT3[locomotive.CurrentSteamHeatPressurePSI] * Math.Pow(mainSteamHeatPipeInnerDiaM, 5.0));
            double connectHosePressureDropPSI = 0.0001306 * steamFlowRateLbpHr * steamFlowRateLbpHr * ConnectSteamHoseLengthFt * (1 + 3.6 / 2.5) / (3600 * locomotive.SteamDensityPSItoLBpFT3[locomotive.CurrentSteamHeatPressurePSI] * Math.Pow(carConnectSteamHoseInnerDiaM, 5.0));
            double carPressureDropPSI = heatPipePressureDropPSI + connectHosePressureDropPSI;

            progressivePressureAlongTrainPSI -= carPressureDropPSI;
            if (progressivePressureAlongTrainPSI < 0)
            {
                progressivePressureAlongTrainPSI = 0; // Make sure that pressure never goes negative
            }
            carSteamHeatMainPipeSteamPressurePSI = progressivePressureAlongTrainPSI;

            // For the boiler heating car adjust mass based upon fuel and water usage
            if (WagonSpecialType == WagonSpecialType.HeatingBoiler)
            {
                // Don't process if water or fule capacities are low
                if (locomotive.CurrentSteamHeatPressurePSI > 0 && currentSteamHeatBoilerFuelCapacityL > 0 && currentCarSteamHeatBoilerWaterCapacityL > 0 && !steamHeatBoilerLockedOut)
                {
                    // Test boiler steam capacity can deliever steam required for the system
                    if (locomotive.CalculatedCarHeaterSteamUsageLBpS > maximumSteamHeatingBoilerSteamUsageRateLbpS)
                    {
                        steamHeatBoilerLockedOut = true; // Lock steam heat boiler out is steam usage exceeds capacity
                        simulator.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("The steam usage has exceeded the capacity of the steam boiler. Steam boiler locked out."));
                        Trace.TraceInformation("Steam heat boiler locked out as capacity exceeded");
                    }

                    // Calculate fuel usage for steam heat boiler
                    double fuelUsageLpS = Size.LiquidVolume.FromGallonUK(Frequency.Periodic.FromHours(TrainHeatBoilerFuelUsageGalukpH[Frequency.Periodic.ToHours(locomotive.CalculatedCarHeaterSteamUsageLBpS)]));
                    const double fuelOilConvertLtoKg = 0.85;
                    currentSteamHeatBoilerFuelCapacityL -= fuelUsageLpS * elapsedClockSeconds; // Reduce tank capacity as fuel used.
                                                                                               // This may need to be changed at some stage, as currently weight decreases on freight cars does not happen, except when being filled or emptied at pickup point
                    MassKG -= (float)(fuelUsageLpS * elapsedClockSeconds * fuelOilConvertLtoKg); // Reduce locomotive weight as Steam heat boiler uses fuel.

                    // Calculate water usage for steam heat boiler
                    double WaterUsageLpS = Size.LiquidVolume.FromGallonUK(Frequency.Periodic.FromHours(TrainHeatBoilerWaterUsageGalukpH[Frequency.Periodic.ToHours(locomotive.CalculatedCarHeaterSteamUsageLBpS)]));
                    currentCarSteamHeatBoilerWaterCapacityL -= WaterUsageLpS * elapsedClockSeconds; // Reduce tank capacity as water used.
                                                                                                    // This may need to be changed at some stage, as currently weight decreases on freight cars does not happen, except when being filled or emptied at pickup point
                    MassKG -= (float)(WaterUsageLpS * elapsedClockSeconds); // Reduce locomotive weight as Steam heat boiler uses water - NB 1 litre of water = 1 kg.
                }
            }
        }

        protected virtual void UpdateHeatLoss()
        {
            // Heat loss due to train movement and air flow, based upon convection heat transfer information - http://www.engineeringtoolbox.com/convective-heat-transfer-d_430.html
            // The formula on this page ( hc = 10.45 - v + 10v1/2), where v = m/s. This formula is used to develop a multiplication factor with train speed.
            // Curve is only valid between 2.0m/s and 20.0m/s

            // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // Calculate heat loss from inside the carriage
            // Initialise car values for heating to zero
            totalCarCompartmentHeatLossW = 0.0;
            carHeatCompartmentPipeAreaM2 = 0.0;

            // Transmission heat loss = exposed area * heat transmission coeff (inside temp - outside temp)
            // Calculate the heat loss through the roof, wagon sides, and floor separately  
            // Calculate the heat loss through the carriage sides, per degree of temp change
            // References - https://www.engineeringtoolbox.com/heat-loss-transmission-d_748.html  and https://www.engineeringtoolbox.com/heat-loss-buildings-d_113.html
            double HeatTransCoeffRoofWpm2C = 1.7 * ConvectionFactor; // 2 inch wood - uninsulated
            double HeatTransCoeffEndsWpm2C = 0.9 * ConvectionFactor; // 2 inch wood - insulated - this compensates for the fact that the ends of the cars are somewhat protected from the environment
            double HeatTransCoeffSidesWpm2C = 1.7 * ConvectionFactor; // 2 inch wood - uninsulated
            double HeatTransCoeffWindowsWpm2C = 4.7 * ConvectionFactor; // Single glazed glass window in wooden frame
            double HeatTransCoeffFloorWpm2C = 2.5 * ConvectionFactor; // uninsulated floor

            // Calculate volume in carriage - note height reduced by 1.06m to allow for bogies, etc
            double CarCouplingPipeM = 1.2;  // Allow for connection between cars (assume 2' each end) - no heat is contributed to carriages.

            // Calculate the heat loss through the roof, allow 15% additional heat loss through roof because of radiation to space
            double RoofHeatLossFactor = 1.15;
            double HeatLossTransRoofW = RoofHeatLossFactor * (CarWidthM * (CarLengthM - CarCouplingPipeM)) * HeatTransCoeffRoofWpm2C * (CarInsideTempC - CarOutsideTempC);

            // Each car will have 2 x sides + 2 x ends. Each side will be made up of solid walls, and windows. A factor has been assumed to determine the ratio of window area to wall area.
            double HeatLossTransWindowsW = (windowDeratingFactor * (CarHeightM - BogieHeight) * (CarLengthM - CarCouplingPipeM)) * HeatTransCoeffWindowsWpm2C * (CarInsideTempC - CarOutsideTempC);
            double HeatLossTransSidesW = (1.0 - windowDeratingFactor) * (CarHeightM - BogieHeight) * (CarLengthM - CarCouplingPipeM) * HeatTransCoeffSidesWpm2C * (CarInsideTempC - CarOutsideTempC);
            double HeatLossTransEndsW = (CarHeightM - BogieHeight) * (CarLengthM - CarCouplingPipeM) * HeatTransCoeffEndsWpm2C * (CarInsideTempC - CarOutsideTempC);

            // Total equals 2 x sides, ends, windows
            double HeatLossTransTotalSidesW = (2.0 * HeatLossTransWindowsW) + (2.0 * HeatLossTransSidesW) + (2.0 * HeatLossTransEndsW);

            // Calculate the heat loss through the floor
            double HeatLossTransFloorW = CarWidthM * (CarLengthM - CarCouplingPipeM) * HeatTransCoeffFloorWpm2C * (CarInsideTempC - CarOutsideTempC);

            double HeatLossTransmissionW = HeatLossTransRoofW + HeatLossTransTotalSidesW + HeatLossTransFloorW;

            // ++++++++++++++++++++++++
            // Ventilation Heat loss, per degree of temp change
            // This will occur when the train is stopped at the station and prior to being ready to depart. Typically will only apply in activity mode, and not explore mode
            double HeatLossVentilationW = 0;
            double HeatRecoveryEfficiency = 0.5; // Assume a HRF of 50%
            double AirFlowVolumeM3pS = CarHeatVolumeM3 / 300.0; // Assume that the volume of the car is emptied over a period of 5 minutes

            if (Train.AtStation && !Train.MayDepart) // When train is at station, if the train is ready to depart, assume all doors are closed, and hence no ventilation loss
            {
                HeatLossVentilationW = Dynamics.Power.FromKW((1.0f - HeatRecoveryEfficiency) * Const.AirDensityBySpecificHeatCapacity * AirFlowVolumeM3pS * (CarInsideTempC - CarOutsideTempC));
            }

            // ++++++++++++++++++++++++
            // Infiltration Heat loss, per degree of temp change
            double NumAirShiftspSec = Frequency.Periodic.FromHours(10.0);      // Pepper article suggests that approx 14 air changes per hour happen for a train that is moving @ 50mph, use and av figure of 10.0.
            double HeatLossInfiltrationW = Dynamics.Power.FromKW(Const.AirDensityBySpecificHeatCapacity * NumAirShiftspSec * CarHeatVolumeM3 * (CarInsideTempC - CarOutsideTempC));

            totalCarCompartmentHeatLossW = HeatLossTransmissionW + HeatLossInfiltrationW + HeatLossVentilationW;
        }

        internal void UpdateWorldPosition(in WorldPosition worldPosition)
        {
            this.worldPosition = worldPosition;
        }

        private string GetWheelAxleInformation()
        {
            if (WheelAxles.Count == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();

            int currentBogie = WheelAxles[0].BogieIndex;
            int count = 0;
            foreach (WheelAxle axle in WheelAxles)
            {
                if (currentBogie != (currentBogie = axle.BogieIndex))
                {
                    if (count > 0)
                    {
                        if (builder.Length > 0)
                            builder.Append('-');
                        builder.Append($"{count}");
                    }
                    count = 0;
                }
                count += 2;
            }
            if (count > 0)
            {
                if (builder.Length > 0)
                    builder.Append('-');
                builder.Append($"{count}");
            }
            return builder.ToString();
        }

        private protected class TrainCarInformation : DetailInfoBase
        {
            private readonly TrainCar car;

            public TrainCarInformation(TrainCar car) : base(true)
            {
                this.car = car;
            }

            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    car.UpdateCarStatus();
                    base.Update(gameTime);
                }
            }
        }

        private protected virtual void UpdateCarStatus()
        {
            Catalog catalog = Simulator.Catalog as Catalog;
            carInfo["Car"] = CarID;
            carInfo["Speed"] = FormatStrings.FormatSpeedDisplay(SpeedMpS, simulator.MetricUnits);
            carInfo["Direction"] = Direction.GetLocalizedDescription();
            carInfo["Flipped"] = Flipped ? catalog.GetString("Yes") : catalog.GetString("No");
        }

        private protected class TrainCarForceInformation : DetailInfoBase
        {
            private readonly TrainCar car;

            public TrainCarForceInformation(TrainCar car) : base(true)
            {
                this.car = car;
            }

            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    car.UpdateForceStatus();
                    base.Update(gameTime);
                }
            }
        }

        private protected virtual void UpdateForceStatus()
        {
            bool metricUnits = Simulator.Instance.MetricUnits;
            bool ukUnits = simulator.Settings.MeasurementUnit == MeasurementUnit.UK;
            forceInfo["Car"] = CarID;

            forceInfo["Total"] = FormatStrings.FormatForce(TotalForceN, metricUnits);
            forceInfo["Motive"] = FormatStrings.FormatForce(MotiveForceN, metricUnits);
            forceInfo.FormattingOptions["Motive"] = WheelSlip ? FormatOption.RegularOrangeRed : WheelSlipWarning ? FormatOption.RegularYellow : null;
            forceInfo["Brake"] = FormatStrings.FormatForce(BrakeForceN, metricUnits);
            forceInfo["Friction"] = FormatStrings.FormatForce(FrictionForceN, metricUnits);
            forceInfo["Gravity"] = FormatStrings.FormatForce(GravityForceN, metricUnits);
            forceInfo["Curve"] = FormatStrings.FormatForce(CurveForceN, metricUnits);
            forceInfo["Tunnel"] = FormatStrings.FormatForce(TunnelForceN, metricUnits);
            forceInfo["Wind"] = FormatStrings.FormatForce(WindForceN, metricUnits);
            forceInfo["Coupler"] = FormatStrings.FormatForce(CouplerForceU, metricUnits);
            forceInfo["CouplerIndication"] = $"{(GetCouplerRigidIndication() ? "R" : "F")} : {(CouplerExceedBreakLimit ? "xxx" : CouplerOverloaded ? "O/L" : HUDCouplerForceIndication == 1 ? "Pull" : HUDCouplerForceIndication == 2 ? "Push" : "-")}";
            forceInfo["Slack"] = FormatStrings.FormatVeryShortDistanceDisplay(CouplerSlackM, metricUnits);
            forceInfo["Mass"] = FormatStrings.FormatLargeMass(MassKG, metricUnits, ukUnits);
            forceInfo["Gradient"] = $"{CurrentElevationPercent:F2}%";
            forceInfo["CurveRadius"] = FormatStrings.FormatDistance(CurrentCurveRadius, metricUnits);
            forceInfo["BrakeFriction"] = $"{BrakeShoeCoefficientFriction * 100.0f:F0}%";
            forceInfo["BrakeSlide"] = HUDBrakeSkid ? Simulator.Catalog.GetString("Yes") : Simulator.Catalog.GetString("No");
            forceInfo["BearingTemp"] = FormatStrings.JoinIfNotEmpty(' ',
                $"{FormatStrings.FormatTemperature(WheelBearingTemperatureDegC, Simulator.Instance.MetricUnits)}",
                (WheelBearingTemperatureDegC) switch
                {
                    > 115 => "(Fail)",
                    > 100 => "(Hot)",
                    > 90 => "(Warm)",
                    < 50 => "(Cool)",
                    _ => "(Normal)",
                });
            forceInfo.FormattingOptions["BearingTemp"] = (WheelBearingTemperatureDegC) switch
            {
                > 115 => FormatOption.RegularRed,
                > 100 => FormatOption.RegularOrange,
                > 90 => FormatOption.RegularYellow,
                < 50 => FormatOption.RegularCyan,
                _ => null,
            };
            forceInfo["Flipped"] = Flipped ? Simulator.Catalog.GetString("Yes") : Simulator.Catalog.GetString("No");
            forceInfo["DerailCoefficient"] = $"{DerailmentCoefficient:F2}";
            forceInfo.FormattingOptions["DerailCoefficient"] = DerailExpected ? FormatOption.RegularOrangeRed : DerailPossible ? FormatOption.RegularYellow : null;
        }

        private class TrainCarPowerSupplyInfo : DetailInfoBase
        {
            private readonly TrainCar car;

            public TrainCarPowerSupplyInfo(TrainCar car) : base(true)
            {
                this.car = car;
            }

            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    this["Car"] = car.CarID;
                    this["WagonType"] = car.WagonType.ToString();

                    switch (car.PowerSupply)
                    {
                        case ScriptedElectricPowerSupply electricPowerSupply:
                            this["Pantograph"] = (car as MSTSWagon).Pantographs.State.GetLocalizedDescription();
                            this["CircuitBreaker"] = electricPowerSupply.CircuitBreaker.State.GetLocalizedDescription();
                            this["MainPower"] = electricPowerSupply.MainPowerSupplyState.GetLocalizedDescription();
                            this["AuxPower"] = electricPowerSupply.AuxiliaryPowerSupplyState.GetLocalizedDescription();
                            break;
                        case ScriptedDieselPowerSupply dieselPowerSupply:
                            this["Engine"] = (car as MSTSDieselLocomotive).DieselEngines.State.GetLocalizedDescription();
                            this["TractionCutOffRelay"] = dieselPowerSupply.TractionCutOffRelay.State.GetLocalizedDescription();
                            this["MainPower"] = dieselPowerSupply.MainPowerSupplyState.GetLocalizedDescription();
                            this["AuxPower"] = dieselPowerSupply.AuxiliaryPowerSupplyState.GetLocalizedDescription();
                            break;
                        case ScriptedDualModePowerSupply dualModePowerSupply:
                            this["Pantograph"] = (car as MSTSWagon).Pantographs.State.GetLocalizedDescription();
                            this["Engine"] = (car as MSTSDieselLocomotive)?.DieselEngines.State.GetLocalizedDescription();
                            this["CircuitBreaker"] = dualModePowerSupply.CircuitBreaker.State.GetLocalizedDescription();
                            this["TractionCutOffRelay"] = dualModePowerSupply.TractionCutOffRelay.State.GetLocalizedDescription();
                            this["MainPower"] = dualModePowerSupply.MainPowerSupplyState.GetLocalizedDescription();
                            this["AuxPower"] = dualModePowerSupply.AuxiliaryPowerSupplyState.GetLocalizedDescription();
                            break;
                    }

                    if (car.PowerSupply != null)
                    {
                        this["Battery"] = car.PowerSupply.BatteryState.GetLocalizedDescription();
                        this["LowVoltagePower"] = car.PowerSupply.LowVoltagePowerSupplyState.GetLocalizedDescription();
                        this["CabPower"] = (car.PowerSupply as ILocomotivePowerSupply)?.CabPowerSupplyState.GetLocalizedDescription();

                        if (car.PowerSupply.ElectricTrainSupplyState != PowerSupplyState.Unavailable)
                        {
                            this["Ets"] = car.PowerSupply.ElectricTrainSupplyState.GetLocalizedDescription();
                            this["EtsCable"] = car.PowerSupply.FrontElectricTrainSupplyCableConnected ? "connected" : "disconnected";
                            this["Power"] = FormatStrings.FormatPower(car.PowerSupply.ElectricTrainSupplyPowerW, true, false, false);
                        }
                    }
                    base.Update(gameTime);
                }
            }
        }
    }
}
