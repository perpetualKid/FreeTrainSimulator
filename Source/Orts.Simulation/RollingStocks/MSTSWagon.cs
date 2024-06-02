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

/*
 *    TrainCarSimulator
 *    
 *    TrainCarViewer
 *    
 *  Every TrainCar generates a FrictionForce.
 *  
 *  The viewer is a separate class object since there could be multiple 
 *  viewers potentially on different devices for a single car. 
 *  
 */

//#define DEBUG_AUXTENDER

// Debug for Friction Force
//#define DEBUG_FRICTION

// Debug for Freight Animation Variable Mass
//#define DEBUG_VARIABLE_MASS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Simulation.RollingStocks.SubSystems;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;
using Orts.Simulation.RollingStocks.SubSystems.Controllers;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using Orts.Simulation.World;

namespace Orts.Simulation.RollingStocks
{

    ///////////////////////////////////////////////////
    ///   SIMULATION BEHAVIOUR
    ///////////////////////////////////////////////////


    /// <summary>
    /// Represents the physical motion and behaviour of the car.
    /// </summary>

    public class SoundSourceEventArgs : EventArgs
    {
        public object Owner { get; }
        public TrainEvent SoundEvent { get; }

        public SoundSourceEventArgs(TrainEvent trainEvent, object owner)
        {
            Owner = owner;
            SoundEvent = trainEvent;
        }
    }

    public class MSTSWagon : TrainCar, ISaveStateApi<TrainCarSaveState>
    {
        private int initWagonNumAxles; // Initial read of number of axles on a wagon

        public Pantographs Pantographs { get; }
        public ScriptedPassengerCarPowerSupply PassengerCarPowerSupply => PowerSupply as ScriptedPassengerCarPowerSupply;
        public Doors Doors { get; }
        public bool MirrorOpen;
        public bool UnloadingPartsOpen;
        public bool WaitForAnimationReady; // delay counter to start loading/unliading is on;
        public bool IsRollerBearing; // Has roller bearings
        public bool IsLowTorqueRollerBearing; // Has low torque roller bearings
        public bool IsFrictionBearing; //Has oil based friction (or solid bearings)
        public bool IsGreaseFrictionBearing; // Has grease based friction (or solid bearings)
        public bool IsDavisFriction = true; // Default to new Davis type friction
        public bool IsBelowMergeSpeed = true; // set indicator for low speed operation as per given speed


        public bool GenericItem1;
        public bool GenericItem2;
        private Interpolator BrakeShoeFrictionFactor;  // Factor of friction for wagon brake shoes
        private const float WaterLBpUKG = 10.0f;    // lbs of water in 1 gal (uk)
        private float TempMassDiffRatio;


        // simulation parameters
        private protected Vector3 soundDebugValues = new Vector3();
        public Vector3 SoundValues => soundDebugValues; // used to convey status to soundsource
        public float Variable1 { get; protected set; }

        // wag file data
        public string MainShapeFileName;
        public string FreightShapeFileName;
        public float FreightAnimMaxLevelM;
        public float FreightAnimMinLevelM;
        public float FreightAnimFlag = 1;   // if absent or >= 0 causes the freightanim to drop in tenders
        public string Cab3DShapeFileName; // 3DCab view shape file name
        public string InteriorShapeFileName; // passenger view shape file name
        public string MainSoundFileName;
        public string InteriorSoundFileName;
        public string Cab3DSoundFileName;
        public float ExternalSoundPassThruPercent = -1;
        public float WheelRadiusM = (float)Size.Length.FromIn(18.0f);  // Provide some defaults in case it's missing from the wag - Wagon wheels could vary in size from approx 10" to 25".
        protected float StaticFrictionFactorN;    // factor to multiply friction by to determine static or starting friction - will vary depending upon whether roller or friction bearing
        private float FrictionLowSpeedN; // Davis low speed value 0 - 5 mph
        private float FrictionBelowMergeSpeedN; // Davis low speed value for defined speed
        public float Friction0N;        // static friction
        protected float Friction5N;               // Friction at 5mph
        public float StandstillFrictionN;
        public float MergeSpeedFrictionN;
        public float MergeSpeedMpS = (float)Speed.MeterPerSecond.FromMpH(5f);
        public float DavisAN;           // davis equation constant
        public float DavisBNSpM;        // davis equation constant for speed
        public float DavisCNSSpMM;      // davis equation constant for speed squared
        public float DavisDragConstant; // Drag coefficient for wagon
        public float WagonFrontalAreaM2; // Frontal area of wagon
        public float TrailLocoResistanceFactor; // Factor to reduce base and wind resistance if locomotive is not leading - based upon original Davis drag coefficients

        private bool TenderWeightInitialize = true;
        private float TenderWagonMaxCoalMassKG;
        private float TenderWagonMaxWaterMassKG;

        // Wind Impacts
        private float WagonDirectionDeg;
        private float WagonResultantWindComponentDeg;
        private float WagonWindResultantSpeedMpS;

        protected float FrictionC1; // MSTS Friction parameters
        protected float FrictionE1; // MSTS Friction parameters
        protected float FrictionV2; // MSTS Friction parameters
        protected float FrictionC2; // MSTS Friction parameters
        protected float FrictionE2; // MSTS Friction parameters

        //protected float FrictionSpeedMpS; // Train current speed value for friction calculations ; this value is never used outside of this class, and FrictionSpeedMpS is always = AbsSpeedMpS
        private EnumArray<Coupler, TrainCarLocation> couplers = new EnumArray<Coupler, TrainCarLocation>();
        public float Adhesion1 = .27f;   // 1st MSTS adhesion value
        public float Adhesion2 = .49f;   // 2nd MSTS adhesion value
        public float Adhesion3 = 2;   // 3rd MSTS adhesion value
        public float Curtius_KnifflerA = 7.5f;               //Curtius-Kniffler constants                   A
        public float Curtius_KnifflerB = 44.0f;              // (adhesion coeficient)       umax = ---------------------  + C
        public float Curtius_KnifflerC = 0.161f;             //                                      speedMpS * 3.6 + B
        public float AdhesionK = 0.7f;   //slip characteristics slope
        public float AxleInertiaKgm2;    //axle inertia
        public float AdhesionDriveWheelRadiusM;
        public float WheelSpeedMpS;
        public float WheelSpeedSlipMpS; // speed of wheel if locomotive is slipping
        public float SlipWarningThresholdPercent = 70;
        public MSTSNotchController WeightLoadController; // Used to control freight loading in freight cars
        public float AbsWheelSpeedMpS; // Math.Abs(WheelSpeedMpS) is used frequently in the subclasses, maybe it's more efficient to compute it once

        // Colours for smoke and steam effects
        public Color ExhaustTransientColor = Color.Black;
        public Color ExhaustDecelColor = Color.WhiteSmoke;
        public Color ExhaustSteadyColor = Color.Gray;

        // Wagon steam leaks
        public float HeatingHoseParticleDurationS;
        public float HeatingHoseSteamVelocityMpS;
        public float HeatingHoseSteamVolumeM3pS;

        // Wagon heating compartment steamtrap leaks
        public float HeatingCompartmentSteamTrapParticleDurationS;
        public float HeatingCompartmentSteamTrapVelocityMpS;
        public float HeatingCompartmentSteamTrapVolumeM3pS;

        // Wagon heating steamtrap leaks
        public float HeatingMainPipeSteamTrapDurationS;
        public float HeatingMainPipeSteamTrapVelocityMpS;
        public float HeatingMainPipeSteamTrapVolumeM3pS;

        // Steam Brake leaks
        public float SteamBrakeLeaksDurationS;
        public float SteamBrakeLeaksVelocityMpS;
        public float SteamBrakeLeaksVolumeM3pS;

        // Water Scoop Spray
        public float WaterScoopParticleDurationS;
        public float WaterScoopWaterVelocityMpS;
        public float WaterScoopWaterVolumeM3pS;

        // Tender Water overflow
        public float TenderWaterOverflowParticleDurationS;
        public float TenderWaterOverflowVelocityMpS;
        public float TenderWaterOverflowVolumeM3pS;

        // Wagon Power Generator
        public float WagonGeneratorDurationS = 1.5f;
        public float WagonGeneratorVolumeM3pS = 2.0f;
        public Color WagonGeneratorSteadyColor = Color.Gray;

        // Heating Steam Boiler
        public float HeatingSteamBoilerDurationS;
        public float HeatingSteamBoilerVolumeM3pS;
        public Color HeatingSteamBoilerSteadyColor = Color.LightSlateGray;

        private bool heatingBoilerSet;
        private bool trainHeatingBoilerInitialised;

        public bool InitializeBoilerHeating()
        {
            // set flag to indicate that heating boiler is active on this car only - only sets first boiler steam effect found in the train
            if (!trainHeatingBoilerInitialised && !heatingBoilerSet)
            {
                heatingBoilerSet = true;
                trainHeatingBoilerInitialised = true;
                return true;
            }
            return false;
        }

        // Wagon Smoke
        public float WagonSmokeVolumeM3pS;
        private float InitialWagonSmokeVolumeM3pS = 3.0f;
        public float WagonSmokeDurationS;
        private float InitialWagonSmokeDurationS = 1.0f;
        public float WagonSmokeVelocityMpS = 15.0f;
        public Color WagonSmokeSteadyColor = Color.Gray;
        TrainCarLocation couplerLocation;

        // Bearing Hot Box Smoke
        public float BearingHotBoxSmokeVolumeM3pS;
        public float BearingHotBoxSmokeDurationS;
        public float BearingHotBoxSmokeVelocityMpS = 15.0f;
        public Color BearingHotBoxSmokeSteadyColor = Color.Gray;

        /// <summary>
        /// True if vehicle is equipped with an additional emergency brake reservoir
        /// </summary>
        public bool EmergencyReservoirPresent;
        public enum BrakeValveType
        {
            None,
            TripleValve, // Plain triple valve
            Distributor, // Triple valve with graduated release
        }
        /// <summary>
        /// Type of brake valve in the car
        /// </summary>
        public BrakeValveType BrakeValve;
        /// <summary>
        /// True if equipped with handbrake. (Not common for older steam locomotives.)
        /// </summary>
        public bool HandBrakePresent;
        /// <summary>
        /// Number of available retainer positions. (Used on freight cars, mostly.) Might be 0, 3 or 4.
        /// </summary>
        public int RetainerPositions;

        /// <summary>
        /// Indicates whether a brake is present or not when Manual Braking is selected.
        /// </summary>
        public bool ManualBrakePresent;

        /// <summary>
        /// Indicates whether a non auto (straight) brake is present or not when braking is selected.
        /// </summary>
        public bool NonAutoBrakePresent;

        /// <summary>
        /// Indicates whether an auxiliary reservoir is present on the wagon or not.
        /// </summary>
        public bool AuxiliaryReservoirPresent;

        /// <summary>
        /// Active locomotive for a control trailer
        /// </summary>
        public MSTSLocomotive ControlActiveLocomotive { get; private set; }

        /// <summary>
        /// Attached steam locomotive in case this wagon is a tender
        /// </summary>
        public MSTSSteamLocomotive TendersSteamLocomotive { get; private set; }

        /// <summary>
        /// Attached steam locomotive in case this wagon is an auxiliary tender
        /// </summary>
        public MSTSSteamLocomotive AuxTendersSteamLocomotive { get; private set; }

        /// <summary>
        /// Steam locomotive has a tender coupled to it
        /// </summary>
        public MSTSSteamLocomotive SteamLocomotiveTender { get; private set; }

        /// <summary>
        /// Steam locomotive identifier (pass parameters from MSTSSteamLocomotive to MSTSWagon)
        /// </summary>
        public MSTSSteamLocomotive SteamLocomotiveIdentification { get; private set; }

        /// <summary>
        /// Diesel locomotive identifier  (pass parameters from MSTSDieselLocomotive to MSTSWagon)
        /// </summary>
        public MSTSDieselLocomotive DieselLocomotiveIdentification { get; private set; }

        public Dictionary<string, List<ParticleEmitterData>> EffectData = new Dictionary<string, List<ParticleEmitterData>>();

        protected void ParseEffects(string lowercasetoken, STFReader stf)
        {
            stf.MustMatch("(");
            string s;

            while ((s = stf.ReadItem()) != ")")
            {
                var data = new ParticleEmitterData(stf);
                if (!EffectData.ContainsKey(s))
                    EffectData.Add(s, new List<ParticleEmitterData>());
                EffectData[s].Add(data);
            }

        }


        public List<IntakePoint> IntakePointList = new List<IntakePoint>();

        public static class RefillProcess
        {
            public static bool OkToRefill { get; set; }
            public static int ActivePickupObjectUID { get; set; }
            public static bool Unload { get; set; }
        }

        public MSTSBrakeSystem MSTSBrakeSystem
        {
            get { return (MSTSBrakeSystem)base.BrakeSystem; }
            set { base.BrakeSystem = value; } // value needs to be set to allow trailing cars to have same brake system as locomotive when in simple brake mode
        }

        public MSTSWagon(string wagFilePath)
            : base(wagFilePath)
        {
            Pantographs = new Pantographs(this);
            Doors = new Doors(this);
        }

        public void Load()
        {
            if (CarManager.LoadedCars.TryGetValue(WagFilePath, out MSTSWagon value))
            {
                Copy(value);
            }
            else
            {
                LoadFromWagFile(WagFilePath);
                CarManager.LoadedCars.Add(WagFilePath, this);
            }
        }

        // Values for adjusting wagon physics due to load changes
        private float LoadEmptyMassKg;
        private float LoadEmptyORTSDavis_A;
        private float LoadEmptyORTSDavis_B;
        private float LoadEmptyORTSDavis_C;
        private float LoadEmptyWagonFrontalAreaM2;
        private float LoadEmptyDavisDragConstant;
        private float LoadEmptyMaxBrakeForceN;
        private float LoadEmptyMaxHandbrakeForceN;
        private float LoadEmptyCentreOfGravityM_Y;
        private float LoadFullMassKg;
        private float LoadFullORTSDavis_A;
        private float LoadFullORTSDavis_B;
        private float LoadFullORTSDavis_C;
        private float LoadFullWagonFrontalAreaM2;
        private float LoadFullDavisDragConstant;
        private float LoadFullMaxBrakeForceN;
        private float LoadFullMaxHandbrakeForceN;
        private float LoadFullCentreOfGravityM_Y;


        /// <summary>
        /// This initializer is called when we haven't loaded this type of car before
        /// and must read it new from the wag file.
        /// </summary>
        public virtual void LoadFromWagFile(string wagFilePath)
        {
            string dir = Path.GetDirectoryName(wagFilePath);
            string file = Path.GetFileName(wagFilePath);
            string orFile = dir + @"\openrails\" + file;
            if (File.Exists(orFile))
                wagFilePath = orFile;

            using (STFReader stf = new STFReader(wagFilePath, true))
            {
                while (!stf.Eof)
                {
                    stf.ReadItem();
                    Parse(stf.Tree.ToLowerInvariant(), stf);
                }
            }

            string wagonFolder = Path.GetDirectoryName(WagFilePath);
            if (MainShapeFileName != null && !File.Exists(Path.Combine(wagonFolder, MainShapeFileName)))
            {
                Trace.TraceWarning("{0} references non-existent shape {1}", WagFilePath, (Path.GetFullPath(Path.Combine(wagonFolder, MainShapeFileName))));
                MainShapeFileName = string.Empty;
            }
            if (FreightShapeFileName != null && !File.Exists(Path.Combine(wagonFolder, FreightShapeFileName)))
            {
                Trace.TraceWarning("{0} references non-existent shape {1}", WagFilePath, Path.GetFullPath(Path.Combine(wagonFolder, FreightShapeFileName)));
                FreightShapeFileName = null;
            }
            if (InteriorShapeFileName != null && !File.Exists(Path.Combine(wagonFolder, InteriorShapeFileName)))
            {
                Trace.TraceWarning("{0} references non-existent shape {1}", WagFilePath, Path.GetFullPath(Path.Combine(wagonFolder, InteriorShapeFileName)));
                InteriorShapeFileName = null;
            }

            if (FrontCouplerAnimation != null && !File.Exists(Path.Combine(wagonFolder, FrontCouplerAnimation.ShapeFileName)))
            {
                Trace.TraceWarning("{0} references non-existent shape {1}", WagFilePath, Path.GetFullPath(Path.Combine(wagonFolder, FrontCouplerAnimation.ShapeFileName)));
                FrontCouplerAnimation = null;
            }

            if (RearCouplerAnimation != null && !File.Exists(Path.Combine(wagonFolder, RearCouplerAnimation.ShapeFileName)))
            {
                Trace.TraceWarning("{0} references non-existent shape {1}", WagFilePath, Path.GetFullPath(Path.Combine(wagonFolder, RearCouplerAnimation.ShapeFileName)));
                RearCouplerAnimation = null;
            }

            if (FrontAirHoseAnimation != null && !File.Exists(Path.Combine(wagonFolder, FrontAirHoseAnimation.ShapeFileName)))
            {
                Trace.TraceWarning("{0} references non-existent shape {1}", WagFilePath, Path.GetFullPath(Path.Combine(wagonFolder, FrontAirHoseAnimation.ShapeFileName)));
                FrontAirHoseAnimation = null;
            }

            if (RearAirHoseAnimation != null && !File.Exists(Path.Combine(wagonFolder, RearAirHoseAnimation.ShapeFileName)))
            {
                Trace.TraceWarning("{0} references non-existent shape {1}", WagFilePath, Path.GetFullPath(Path.Combine(wagonFolder, RearAirHoseAnimation.ShapeFileName)));
                RearAirHoseAnimation = null;
            }

            // If trailing loco resistance constant has not been  defined in WAG/ENG file then assign default value based upon orig Davis values
            if (TrailLocoResistanceFactor == 0)
            {
                if (WagonType == WagonType.Engine)
                {
                    TrailLocoResistanceFactor = 0.2083f;  // engine drag value
                }
                else if (WagonType == WagonType.Tender)
                {
                    TrailLocoResistanceFactor = 1.0f;  // assume that tenders have been set with a value of 0.0005 as per freight wagons
                }
                else  //Standard default if not declared anywhere else
                {
                    TrailLocoResistanceFactor = 1.0f;
                }
            }

            // Initialise car body lengths. Assume overhang is 2.0m each end, and bogie centres are the car length minus this value

            if (CarCouplerFaceLength == 0)
            {
                CarCouplerFaceLength = CarLengthM;
            }

            if (CarBodyLength == 0)
            {
                CarBodyLength = CarCouplerFaceLength - 0.8f;
            }

            if (CarBogieCentreLength == 0)
            {
                CarBogieCentreLength = CarCouplerFaceLength - 4.3f;
            }

            if (airHoseLengthM == 0)
            {
                airHoseLengthM = (float)Size.Length.FromIn(26.25); // 26.25 inches
            }

            var couplerlength = ((CarCouplerFaceLength - CarBodyLength) / 2) + 0.1f; // coupler length at rest, allow 0.1m also for slack

            if (airHoseHorizontalLengthM == 0)
            {
                airHoseHorizontalLengthM = 0.3862f; // 15.2 inches
            }

            // Disable derailment coefficent on "dummy" cars. NB: Ideally this should never be used as "dummy" cars interfer with the overall train physics.
            if (wagonNumWheels == 0 && initWagonNumAxles == 0)
            {
                derailmentCoefficientEnabled = false;
            }

            // Ensure Drive Axles is set to a default if no OR value added to WAG file
            if (wagonNumAxles == 0 && WagonType != WagonType.Engine)
            {
                if (wagonNumWheels != 0 && wagonNumWheels < 6)
                {
                    wagonNumAxles = (int)wagonNumWheels;
                }
                else
                {
                    wagonNumAxles = 4; // Set 4 axles as default
                }

                if (simulator.Settings.VerboseConfigurationMessages)
                {
                    Trace.TraceInformation("Number of Wagon Axles set to default value of {0}", wagonNumAxles);
                }
            }
            else
            {
                wagonNumAxles = initWagonNumAxles;
            }

            // Set wheel flange parameters to default values.
            if (maximumWheelFlangeAngle == 0)
            {
                maximumWheelFlangeAngle = 1.22173f; // Default = 70 deg - Pre 1990 AAR 1:20 wheel
            }

            if (wheelFlangeLength == 0)
            {
                wheelFlangeLength = 0.0254f; // Height = 1.00in - Pre 1990 AAR 1:20 wheel
            }

            // Initialise steam heat parameters
            if (TrainHeatBoilerWaterUsageGalukpH == null) // If no table entered in WAG file, then use the default table
            {
                TrainHeatBoilerWaterUsageGalukpH = SteamHeatBoilerWaterUsageGalukpH;
            }

            if (TrainHeatBoilerFuelUsageGalukpH == null) // If no table entered in WAG file, then use the default table
            {
                TrainHeatBoilerFuelUsageGalukpH = SteamHeatBoilerFuelUsageGalukpH;
            }
            maximumSteamHeatingBoilerSteamUsageRateLbpS = (float)TrainHeatBoilerWaterUsageGalukpH.MaxX(); // Find maximum steam capacity of the generator based upon the information in the water usage table
            currentSteamHeatBoilerFuelCapacityL = maximiumSteamHeatBoilerFuelTankCapacityL;

            if (maximumSteamHeatBoilerWaterTankCapacityL != 0)
            {
                currentCarSteamHeatBoilerWaterCapacityL = maximumSteamHeatBoilerWaterTankCapacityL;
            }
            else
            {
                currentCarSteamHeatBoilerWaterCapacityL = (float)Size.LiquidVolume.FromGallonUK(800.0f);
            }

            // If Drag constant not defined in WAG/ENG file then assign default value based upon orig Davis values
            if (DavisDragConstant == 0)
            {
                if (WagonType == WagonType.Engine)
                {
                    DavisDragConstant = 0.0024f;
                }
                else if (WagonType == WagonType.Freight)
                {
                    DavisDragConstant = 0.0005f;
                }
                else if (WagonType == WagonType.Passenger)
                {
                    DavisDragConstant = 0.00034f;
                }
                else if (WagonType == WagonType.Tender)
                {
                    DavisDragConstant = 0.0005f;
                }
                else  //Standard default if not declared anywhere else
                {
                    DavisDragConstant = 0.0005f;
                }
            }

            // If wagon frontal area not user defined, assign a default value based upon the wagon dimensions

            if (WagonFrontalAreaM2 == 0)
            {
                WagonFrontalAreaM2 = CarWidthM * CarHeightM;
            }

            // Initialise key wagon parameters
            MassKG = InitialMassKG;
            MaxHandbrakeForceN = initialMaxHandbrakeForce;
            MaxBrakeForceN = initialMaxBrakeForce;
            centreOfGravityM = initialCentreOfGravityM;

            if (FreightAnimations != null)
            {
                foreach (var ortsFreightAnim in FreightAnimations.Animations)
                {
                    if (ortsFreightAnim.ShapeFileName != null && !File.Exists(Path.Combine(wagonFolder, ortsFreightAnim.ShapeFileName)))
                    {
                        Trace.TraceWarning("ORTS FreightAnim in trainset {0} references non-existent shape {1}", WagFilePath, Path.GetFullPath(Path.Combine(wagonFolder, ortsFreightAnim.ShapeFileName)));
                        ortsFreightAnim.ShapeFileName = null;
                    }

                }

                // Read freight animation values from animation INCLUDE files
                // Read (initialise) "common" (empty values first).
                // Test each value to make sure that it has been defined in the WAG file, if not default to Root WAG file value
                if (FreightAnimations.WagonEmptyWeight > 0)
                {
                    LoadEmptyMassKg = FreightAnimations.WagonEmptyWeight;
                }
                else
                {
                    LoadEmptyMassKg = MassKG;
                }

                if (FreightAnimations.EmptyORTSDavis_A > 0)
                {
                    LoadEmptyORTSDavis_A = FreightAnimations.EmptyORTSDavis_A;
                }
                else
                {
                    LoadEmptyORTSDavis_A = DavisAN;
                }

                if (FreightAnimations.EmptyORTSDavis_B > 0)
                {
                    LoadEmptyORTSDavis_B = FreightAnimations.EmptyORTSDavis_B;
                }
                else
                {
                    LoadEmptyORTSDavis_B = DavisBNSpM;
                }

                if (FreightAnimations.EmptyORTSDavis_C > 0)
                {
                    LoadEmptyORTSDavis_C = FreightAnimations.EmptyORTSDavis_C;
                }
                else
                {
                    LoadEmptyORTSDavis_C = DavisCNSSpMM;
                }

                if (FreightAnimations.EmptyORTSDavisDragConstant > 0)
                {
                    LoadEmptyDavisDragConstant = FreightAnimations.EmptyORTSDavisDragConstant;
                }
                else
                {
                    LoadEmptyDavisDragConstant = DavisDragConstant;
                }

                if (FreightAnimations.EmptyORTSWagonFrontalAreaM2 > 0)
                {
                    LoadEmptyWagonFrontalAreaM2 = FreightAnimations.EmptyORTSWagonFrontalAreaM2;
                }
                else
                {
                    LoadEmptyWagonFrontalAreaM2 = WagonFrontalAreaM2;
                }

                if (FreightAnimations.EmptyMaxBrakeForceN > 0)
                {
                    LoadEmptyMaxBrakeForceN = FreightAnimations.EmptyMaxBrakeForceN;
                }
                else
                {
                    LoadEmptyMaxBrakeForceN = MaxBrakeForceN;
                }

                if (FreightAnimations.EmptyMaxHandbrakeForceN > 0)
                {
                    LoadEmptyMaxHandbrakeForceN = FreightAnimations.EmptyMaxHandbrakeForceN;
                }
                else
                {
                    LoadEmptyMaxHandbrakeForceN = MaxHandbrakeForceN;
                }

                if (FreightAnimations.EmptyCentreOfGravityM_Y > 0)
                {
                    LoadEmptyCentreOfGravityM_Y = FreightAnimations.EmptyCentreOfGravityM_Y;
                }
                else
                {
                    LoadEmptyCentreOfGravityM_Y = centreOfGravityM.Y;
                }

                // Read (initialise) Static load ones if a static load
                // Test each value to make sure that it has been defined in the WAG file, if not default to Root WAG file value
                if (FreightAnimations.FullPhysicsStaticOne != null)
                {
                    if (FreightAnimations.FullPhysicsStaticOne.FullStaticORTSDavis_A > 0)
                    {
                        LoadFullORTSDavis_A = FreightAnimations.FullPhysicsStaticOne.FullStaticORTSDavis_A;
                    }
                    else
                    {
                        LoadFullORTSDavis_A = DavisAN;
                    }

                    if (FreightAnimations.FullPhysicsStaticOne.FullStaticORTSDavis_B > 0)
                    {
                        LoadFullORTSDavis_B = FreightAnimations.FullPhysicsStaticOne.FullStaticORTSDavis_B;
                    }
                    else
                    {
                        LoadFullORTSDavis_B = DavisBNSpM;
                    }

                    if (FreightAnimations.FullPhysicsStaticOne.FullStaticORTSDavis_C > 0)
                    {
                        LoadFullORTSDavis_C = FreightAnimations.FullPhysicsStaticOne.FullStaticORTSDavis_C;
                    }
                    else
                    {
                        LoadFullORTSDavis_C = DavisCNSSpMM;
                    }

                    if (FreightAnimations.FullPhysicsStaticOne.FullStaticORTSDavisDragConstant > 0)
                    {
                        LoadFullDavisDragConstant = FreightAnimations.FullPhysicsStaticOne.FullStaticORTSDavisDragConstant;
                    }
                    else
                    {
                        LoadFullDavisDragConstant = DavisDragConstant;
                    }

                    if (FreightAnimations.FullPhysicsStaticOne.FullStaticORTSWagonFrontalAreaM2 > 0)
                    {
                        LoadFullWagonFrontalAreaM2 = FreightAnimations.FullPhysicsStaticOne.FullStaticORTSWagonFrontalAreaM2;
                    }
                    else
                    {
                        LoadFullWagonFrontalAreaM2 = WagonFrontalAreaM2;
                    }


                    if (FreightAnimations.FullPhysicsStaticOne.FullStaticMaxBrakeForceN > 0)
                    {
                        LoadFullMaxBrakeForceN = FreightAnimations.FullPhysicsStaticOne.FullStaticMaxBrakeForceN;
                    }
                    else
                    {
                        LoadFullMaxBrakeForceN = MaxBrakeForceN;
                    }

                    if (FreightAnimations.FullPhysicsStaticOne.FullStaticMaxHandbrakeForceN > 0)
                    {
                        LoadFullMaxHandbrakeForceN = FreightAnimations.FullPhysicsStaticOne.FullStaticMaxHandbrakeForceN;
                    }
                    else
                    {
                        LoadFullMaxHandbrakeForceN = MaxHandbrakeForceN;
                    }

                    if (FreightAnimations.FullPhysicsStaticOne.FullStaticCentreOfGravityM_Y > 0)
                    {
                        LoadFullCentreOfGravityM_Y = FreightAnimations.FullPhysicsStaticOne.FullStaticCentreOfGravityM_Y;
                    }
                    else
                    {
                        LoadFullCentreOfGravityM_Y = centreOfGravityM.Y;
                    }
                }

                // Read (initialise) Continuous load ones if a continuous load
                // Test each value to make sure that it has been defined in the WAG file, if not default to Root WAG file value
                if (FreightAnimations.FullPhysicsContinuousOne != null)
                {
                    if (FreightAnimations.FullPhysicsContinuousOne.FreightWeightWhenFull > 0)
                    {
                        LoadFullMassKg = FreightAnimations.WagonEmptyWeight + FreightAnimations.FullPhysicsContinuousOne.FreightWeightWhenFull;
                    }
                    else
                    {
                        LoadFullMassKg = MassKG;
                    }

                    if (FreightAnimations.FullPhysicsContinuousOne.FullORTSDavis_A > 0)
                    {
                        LoadFullORTSDavis_A = FreightAnimations.FullPhysicsContinuousOne.FullORTSDavis_A;
                    }
                    else
                    {
                        LoadFullORTSDavis_A = DavisAN;
                    }

                    if (FreightAnimations.FullPhysicsContinuousOne.FullORTSDavis_B > 0)
                    {
                        LoadFullORTSDavis_B = FreightAnimations.FullPhysicsContinuousOne.FullORTSDavis_B;
                    }
                    else
                    {
                        LoadFullORTSDavis_B = DavisBNSpM;
                    }

                    if (FreightAnimations.FullPhysicsContinuousOne.FullORTSDavis_C > 0)
                    {
                        LoadFullORTSDavis_C = FreightAnimations.FullPhysicsContinuousOne.FullORTSDavis_C;
                    }
                    else
                    {
                        LoadFullORTSDavis_C = DavisCNSSpMM;
                    }

                    if (FreightAnimations.FullPhysicsContinuousOne.FullORTSDavisDragConstant > 0)
                    {
                        LoadFullDavisDragConstant = FreightAnimations.FullPhysicsContinuousOne.FullORTSDavisDragConstant;
                    }
                    else
                    {
                        LoadFullDavisDragConstant = DavisDragConstant;
                    }

                    if (FreightAnimations.FullPhysicsContinuousOne.FullORTSWagonFrontalAreaM2 > 0)
                    {
                        LoadFullWagonFrontalAreaM2 = FreightAnimations.FullPhysicsContinuousOne.FullORTSWagonFrontalAreaM2;
                    }
                    else
                    {
                        LoadFullWagonFrontalAreaM2 = WagonFrontalAreaM2;
                    }


                    if (FreightAnimations.FullPhysicsContinuousOne.FullMaxBrakeForceN > 0)
                    {
                        LoadFullMaxBrakeForceN = FreightAnimations.FullPhysicsContinuousOne.FullMaxBrakeForceN;
                    }
                    else
                    {
                        LoadFullMaxBrakeForceN = MaxBrakeForceN;
                    }

                    if (FreightAnimations.FullPhysicsContinuousOne.FullMaxHandbrakeForceN > 0)
                    {
                        LoadFullMaxHandbrakeForceN = FreightAnimations.FullPhysicsContinuousOne.FullMaxHandbrakeForceN;
                    }
                    else
                    {
                        LoadFullMaxHandbrakeForceN = MaxHandbrakeForceN;
                    }

                    if (FreightAnimations.FullPhysicsContinuousOne.FullCentreOfGravityM_Y > 0)
                    {
                        LoadFullCentreOfGravityM_Y = FreightAnimations.FullPhysicsContinuousOne.FullCentreOfGravityM_Y;
                    }
                    else
                    {
                        LoadFullCentreOfGravityM_Y = centreOfGravityM.Y;
                    }
                }

                if (!FreightAnimations.MSTSFreightAnimEnabled)
                    FreightShapeFileName = null;
                if (FreightAnimations.WagonEmptyWeight != -1)
                {
                    // Computes mass when it carries containers
                    float totalContainerMassKG = 0;
                    if (FreightAnimations.Animations != null)
                    {
                        foreach (FreightAnimation anim in FreightAnimations.Animations)
                        {
                            if (anim is FreightAnimationDiscrete discreteAnim && discreteAnim.Container != null)
                            {
                                totalContainerMassKG += discreteAnim.Container.MassKG;
                            }
                        }
                        CalculateTotalMass(totalContainerMassKG);

                        if (FreightAnimations.StaticFreightAnimationsPresent) // If it is static freight animation, set wagon physics to full wagon value
                        {
                            // Update brake parameters   
                            MaxBrakeForceN = LoadFullMaxBrakeForceN;
                            MaxHandbrakeForceN = LoadFullMaxHandbrakeForceN;

                            // Update friction related parameters
                            DavisAN = LoadFullORTSDavis_A;
                            DavisBNSpM = LoadFullORTSDavis_B;
                            DavisCNSSpMM = LoadFullORTSDavis_C;
                            DavisDragConstant = LoadFullDavisDragConstant;
                            WagonFrontalAreaM2 = LoadFullWagonFrontalAreaM2;

                            // Update CoG related parameters
                            centreOfGravityM.Y = LoadFullCentreOfGravityM_Y;

                        }

                    }
                    if (FreightAnimations.LoadedOne != null) // If it is a Continuouos freight animation, set freight wagon parameters to FullatStart
                    {
                        WeightLoadController.CurrentValue = FreightAnimations.LoadedOne.LoadPerCent / 100;

                        // Update wagon parameters sensitive to wagon mass change
                        // Calculate the difference ratio, ie how full the wagon is. This value allows the relevant value to be scaled from the empty mass to the full mass of the wagon
                        TempMassDiffRatio = WeightLoadController.CurrentValue;
                        // Update brake parameters
                        MaxBrakeForceN = ((LoadFullMaxBrakeForceN - LoadEmptyMaxBrakeForceN) * TempMassDiffRatio) + LoadEmptyMaxBrakeForceN;
                        MaxHandbrakeForceN = ((LoadFullMaxHandbrakeForceN - LoadEmptyMaxHandbrakeForceN) * TempMassDiffRatio) + LoadEmptyMaxHandbrakeForceN;

                        // Update friction related parameters
                        DavisAN = ((LoadFullORTSDavis_A - LoadEmptyORTSDavis_A) * TempMassDiffRatio) + LoadEmptyORTSDavis_A;
                        DavisBNSpM = ((LoadFullORTSDavis_B - LoadEmptyORTSDavis_B) * TempMassDiffRatio) + LoadEmptyORTSDavis_B;
                        DavisCNSSpMM = ((LoadFullORTSDavis_C - LoadEmptyORTSDavis_C) * TempMassDiffRatio) + LoadEmptyORTSDavis_C;

                        if (LoadEmptyDavisDragConstant > LoadFullDavisDragConstant) // Due to wind turbulence empty drag might be higher then loaded drag, and therefore both scenarios need to be covered.
                        {
                            DavisDragConstant = LoadEmptyDavisDragConstant - ((LoadEmptyDavisDragConstant - LoadFullDavisDragConstant) * TempMassDiffRatio);
                        }
                        else
                        {
                            DavisDragConstant = ((LoadFullDavisDragConstant - LoadEmptyDavisDragConstant) * TempMassDiffRatio) + LoadEmptyDavisDragConstant;
                        }

                        WagonFrontalAreaM2 = ((LoadFullWagonFrontalAreaM2 - LoadEmptyWagonFrontalAreaM2) * TempMassDiffRatio) + LoadEmptyWagonFrontalAreaM2;

                        // Update CoG related parameters
                        centreOfGravityM.Y = ((LoadFullCentreOfGravityM_Y - LoadEmptyCentreOfGravityM_Y) * TempMassDiffRatio) + LoadEmptyCentreOfGravityM_Y;
                    }
                    else  // If Freight animation is Continuous and freight is not loaded then set initial values to the empty wagon values
                    {
                        if (FreightAnimations.ContinuousFreightAnimationsPresent)
                        {
                            // If it is an empty continuous freight animation, set wagon physics to empty wagon value
                            // Update brake physics
                            MaxBrakeForceN = LoadEmptyMaxBrakeForceN;
                            MaxHandbrakeForceN = LoadEmptyMaxHandbrakeForceN;

                            // Update friction related parameters
                            DavisAN = LoadEmptyORTSDavis_A;
                            DavisBNSpM = LoadEmptyORTSDavis_B;
                            DavisCNSSpMM = LoadEmptyORTSDavis_C;

                            // Update CoG related parameters
                            centreOfGravityM.Y = LoadEmptyCentreOfGravityM_Y;
                        }
                    }

#if DEBUG_VARIABLE_MASS

                Trace.TraceInformation(" ===============================  Variable Load Initialisation (MSTSWagon.cs) ===============================");

                Trace.TraceInformation("CarID {0}", CarID);
                Trace.TraceInformation("Initial Values = Brake {0} Handbrake {1} CoGY {2} Mass {3}", InitialMaxBrakeForceN, InitialMaxHandbrakeForceN, InitialCentreOfGravityM.Y, InitialMassKG);
                Trace.TraceInformation("Empty Values = Brake {0} Handbrake {1} DavisA {2} DavisB {3} DavisC {4} CoGY {5}", LoadEmptyMaxBrakeForceN, LoadEmptyMaxHandbrakeForceN, LoadEmptyORTSDavis_A, LoadEmptyORTSDavis_B, LoadEmptyORTSDavis_C, LoadEmptyCentreOfGravityM_Y);
                Trace.TraceInformation("Full Values = Brake {0} Handbrake {1} DavisA {2} DavisB {3} DavisC {4} CoGY {5}", LoadFullMaxBrakeForceN, LoadFullMaxHandbrakeForceN, LoadFullORTSDavis_A, LoadFullORTSDavis_B, LoadFullORTSDavis_C, LoadFullCentreOfGravityM_Y);
#endif

                }

                // Determine whether or not to use the Davis friction model. Must come after freight animations are initialized.
                IsDavisFriction = DavisAN != 0 && DavisBNSpM != 0 && DavisCNSSpMM != 0;

                if (BrakeSystem == null)
                    BrakeSystem = MSTSBrakeSystem.Create(BrakeSystemType, this);
            }
        }

        // Compute total mass of wagon including freight animations and variable loads like containers
        public void CalculateTotalMass(float totalContainerMassKG)
        {
            MassKG = FreightAnimations.WagonEmptyWeight + FreightAnimations.FreightWeight + FreightAnimations.StaticFreightWeight + totalContainerMassKG;
        }

        public override void Initialize()
        {
            Pantographs.Initialize();
            Doors.Initialize();
            PassengerCarPowerSupply?.Initialize();

            base.Initialize();

            if (unbalancedSuperElevation == 0 || unbalancedSuperElevation > 0.5) // If UnbalancedSuperElevationM > 18", or equal to zero, then set a default value
            {
                switch (WagonType)
                {
                    case WagonType.Freight:
                        unbalancedSuperElevation = (float)Size.Length.FromIn(3.0f);  // Unbalanced superelevation has a maximum default value of 3"
                        break;
                    case WagonType.Passenger:
                        unbalancedSuperElevation = (float)Size.Length.FromIn(3.0f);  // Unbalanced superelevation has a maximum default value of 3"
                        break;
                    case WagonType.Engine:
                        unbalancedSuperElevation = (float)Size.Length.FromIn(6.0f);  // Unbalanced superelevation has a maximum default value of 6"
                        break;
                    case WagonType.Tender:
                        unbalancedSuperElevation = (float)Size.Length.FromIn(6.0f);  // Unbalanced superelevation has a maximum default value of 6"
                        break;
                    default:
                        unbalancedSuperElevation = (float)Size.Length.FromIn(0.01f);  // if no value in wag file or is outside of bounds then set to a default value
                        break;
                }
            }
            FreightAnimations?.Load(FreightAnimations.LoadDataList, true);
            InitializeLoadPhysics();
        }

        public override void InitializeMoving()
        {
            PassengerCarPowerSupply?.InitializeMoving();

            base.InitializeMoving();
        }

        /// <summary>
        /// Parse the wag file parameters required for the simulator and viewer classes
        /// </summary>
        public virtual void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "wagon(wagonshape":
                    MainShapeFileName = stf.ReadStringBlock(null);
                    break;
                case "wagon(type":
                    stf.MustMatch("(");
                    string wagonType = stf.ReadString();
                    if (EnumExtension.GetValue(wagonType.Replace("Carriage", "Passenger", StringComparison.InvariantCultureIgnoreCase), out WagonType wagonTypeResult))
                        WagonType = wagonTypeResult;
                    else
                        STFException.TraceWarning(stf, "Skipped unknown wagon type " + wagonType);
                    break;
                case "wagon(ortswagonspecialtype":
                    stf.MustMatch("(");
                    string wagonspecialType = stf.ReadString();
                    if (EnumExtension.GetValue(wagonspecialType, out WagonSpecialType wagonSpecialTypeResult))
                        WagonSpecialType = wagonSpecialTypeResult;
                    else
                        STFException.TraceWarning(stf, "Assumed unknown engine type " + wagonspecialType);
                    break;
                case "wagon(freightanim":
                    stf.MustMatch("(");
                    FreightShapeFileName = stf.ReadString();
                    FreightAnimMaxLevelM = stf.ReadFloat(STFReader.Units.Distance, null);
                    FreightAnimMinLevelM = stf.ReadFloat(STFReader.Units.Distance, null);
                    // Flags are optional and we want to avoid a warning.
                    if (!stf.EndOfBlock())
                    {
                        // TODO: The variable name (Flag), data type (Float), and unit (Distance) don't make sense here.
                        FreightAnimFlag = stf.ReadFloat(STFReader.Units.Distance, 1.0f);
                        stf.SkipRestOfBlock();
                    }
                    break;
                case "wagon(size":
                    stf.MustMatch("(");
                    CarWidthM = stf.ReadFloat(STFReader.Units.Distance, null);
                    CarHeightM = stf.ReadFloat(STFReader.Units.Distance, null);
                    CarLengthM = stf.ReadFloat(STFReader.Units.Distance, null);
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(ortslengthbogiecentre":
                    CarBogieCentreLength = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortslengthcarbody":
                    CarBodyLength = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortslengthairhose":
                    airHoseLengthM = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortshorizontallengthairhose":
                    airHoseHorizontalLengthM = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortslengthcouplerface":
                    CarCouplerFaceLength = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortswheelflangelength":
                    wheelFlangeLength = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortsmaximumwheelflangeangle":
                    maximumWheelFlangeAngle = stf.ReadFloatBlock(STFReader.Units.Angle, null);
                    break;
                case "wagon(ortstrackgauge":
                    stf.MustMatch("(");
                    trackGauge = stf.ReadFloat(STFReader.Units.Distance, null);
                    // Allow for imperial feet and inches to be specified separately (not ideal - please don't copy this).
                    if (!stf.EndOfBlock())
                    {
                        trackGauge += stf.ReadFloat(STFReader.Units.Distance, 0);
                        stf.SkipRestOfBlock();
                    }
                    break;
                case "wagon(centreofgravity":
                    stf.MustMatch("(");
                    initialCentreOfGravityM.X = stf.ReadFloat(STFReader.Units.Distance, null);
                    initialCentreOfGravityM.Y = stf.ReadFloat(STFReader.Units.Distance, null);
                    initialCentreOfGravityM.Z = stf.ReadFloat(STFReader.Units.Distance, null);
                    if (Math.Abs(initialCentreOfGravityM.Z) > 1)
                    {
                        STFException.TraceWarning(stf, $"Ignored CentreOfGravity Z value {initialCentreOfGravityM.Z} outside range -1 to +1");
                        initialCentreOfGravityM.Z = 0;
                    }
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(ortsunbalancedsuperelevation":
                    unbalancedSuperElevation = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortsrigidwheelbase":
                    stf.MustMatch("(");
                    rigidWheelBaseM = stf.ReadFloat(STFReader.Units.Distance, null);
                    // Allow for imperial feet and inches to be specified separately (not ideal - please don't copy this).
                    if (!stf.EndOfBlock())
                    {
                        rigidWheelBaseM += stf.ReadFloat(STFReader.Units.Distance, 0);
                        stf.SkipRestOfBlock();
                    }
                    break;
                case "wagon(ortsauxtenderwatermass":
                    AuxTenderWaterMassKG = stf.ReadFloatBlock(STFReader.Units.Mass, null);
                    AuxWagonType = AuxTenderWaterMassKG > 0 ? AuxWagonType.AuxiliaryTender : (AuxWagonType)WagonType;
                    break;
                case "wagon(ortstenderwagoncoalmass":
                    TenderWagonMaxCoalMassKG = stf.ReadFloatBlock(STFReader.Units.Mass, null);
                    break;
                case "wagon(ortstenderwagonwatermass":
                    TenderWagonMaxWaterMassKG = stf.ReadFloatBlock(STFReader.Units.Mass, null);
                    break;
                case "wagon(ortsheatingwindowderatingfactor":
                    windowDeratingFactor = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "wagon(ortsheatingcompartmenttemperatureset":
                    DesiredCompartmentTempSetpointC = stf.ReadFloatBlock(STFReader.Units.Temperature, null);
                    break;
                case "wagon(ortsheatingcompartmentpipeareafactor":
                    compartmentHeatingPipeAreaFactor = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "wagon(ortsheatingtrainpipeouterdiameter":
                    mainSteamHeatPipeOuterDiaM = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortsheatingtrainpipeinnerdiameter":
                    mainSteamHeatPipeInnerDiaM = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortsheatingconnectinghoseinnerdiameter":
                    carConnectSteamHoseInnerDiaM = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(ortsheatingconnectinghoseouterdiameter":
                    carConnectSteamHoseOuterDiaM = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(mass":
                    InitialMassKG = stf.ReadFloatBlock(STFReader.Units.Mass, null);
                    if (InitialMassKG < 0.1f)
                        InitialMassKG = 0.1f;
                    break;
                case "wagon(ortsheatingboilerwatertankcapacity":
                    maximumSteamHeatBoilerWaterTankCapacityL = stf.ReadFloatBlock(STFReader.Units.Volume, null);
                    break;
                case "wagon(ortsheatingboilerfueltankcapacity":
                    maximiumSteamHeatBoilerFuelTankCapacityL = stf.ReadFloatBlock(STFReader.Units.Volume, null);
                    break;
                case "wagon(ortsheatingboilerwaterusage":
                    TrainHeatBoilerWaterUsageGalukpH = stf.CreateInterpolator();
                    break;
                case "wagon(ortsheatingboilerfuelusage":
                    TrainHeatBoilerFuelUsageGalukpH = stf.CreateInterpolator();
                    break;
                case "wagon(wheelradius":
                    WheelRadiusM = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "engine(wheelradius":
                    DriverWheelRadiusM = stf.ReadFloatBlock(STFReader.Units.Distance, null);
                    break;
                case "wagon(sound":
                    MainSoundFileName = stf.ReadStringBlock(null);
                    break;
                case "wagon(ortsbrakeshoefriction":
                    BrakeShoeFrictionFactor = stf.CreateInterpolator();
                    break;
                case "wagon(maxhandbrakeforce":
                    initialMaxHandbrakeForce = stf.ReadFloatBlock(STFReader.Units.Force, null);
                    break;
                case "wagon(maxbrakeforce":
                    initialMaxBrakeForce = stf.ReadFloatBlock(STFReader.Units.Force, null);
                    break;
                case "wagon(ortswheelbrakeslideprotection":
                    WheelBrakeSlideProtectionFitted = stf.ReadFloatBlock(STFReader.Units.None, null) == 1;
                    break;
                case "wagon(ortswheelbrakesslideprotectionlimitdisable":
                    WheelBrakeSlideProtectionLimitDisabled = stf.ReadFloatBlock(STFReader.Units.None, null) == 1;
                    break;
                case "wagon(ortsdavis_a":
                    DavisAN = stf.ReadFloatBlock(STFReader.Units.Force, null);
                    break;
                case "wagon(ortsdavis_b":
                    DavisBNSpM = stf.ReadFloatBlock(STFReader.Units.Resistance, null);
                    break;
                case "wagon(ortsdavis_c":
                    DavisCNSSpMM = stf.ReadFloatBlock(STFReader.Units.ResistanceDavisC, null);
                    break;
                case "wagon(ortsdavisdragconstant":
                    DavisDragConstant = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "wagon(ortswagonfrontalarea":
                    WagonFrontalAreaM2 = stf.ReadFloatBlock(STFReader.Units.AreaDefaultFT2, null);
                    break;
                case "wagon(ortstraillocomotiveresistancefactor":
                    TrailLocoResistanceFactor = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "wagon(ortsstandstillfriction":
                    StandstillFrictionN = stf.ReadFloatBlock(STFReader.Units.Force, null);
                    break;
                case "wagon(ortsmergespeed":
                    MergeSpeedMpS = stf.ReadFloatBlock(STFReader.Units.Speed, MergeSpeedMpS);
                    break;
                case "wagon(effects(specialeffects":
                    ParseEffects(lowercasetoken, stf);
                    break;
                case "wagon(ortsbearingtype":
                    stf.MustMatch("(");
                    string typeString2 = stf.ReadString();
                    IsRollerBearing = string.Equals(typeString2, "Roller", StringComparison.OrdinalIgnoreCase);
                    IsLowTorqueRollerBearing = string.Equals(typeString2, "Low", StringComparison.OrdinalIgnoreCase);
                    IsFrictionBearing = string.Equals(typeString2, "Friction", StringComparison.OrdinalIgnoreCase);
                    IsGreaseFrictionBearing = string.Equals(typeString2, "Grease", StringComparison.OrdinalIgnoreCase);
                    break;
                case "wagon(friction":
                    stf.MustMatch("(");
                    FrictionC1 = stf.ReadFloat(STFReader.Units.Resistance, null);
                    FrictionE1 = stf.ReadFloat(STFReader.Units.None, null);
                    FrictionV2 = stf.ReadFloat(STFReader.Units.Speed, null);
                    FrictionC2 = stf.ReadFloat(STFReader.Units.Resistance, null);
                    FrictionE2 = stf.ReadFloat(STFReader.Units.None, null);
                    stf.SkipRestOfBlock();
                    ;
                    break;
                case "wagon(brakesystemtype":
                    EnumExtension.GetValue(stf.ReadStringBlock(null).Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase), out BrakeSystemType brakeSystemType);
                    BrakeSystemType = brakeSystemType;
                    BrakeSystem = MSTSBrakeSystem.Create(brakeSystemType, this);
                    break;
                case "wagon(brakeequipmenttype":
                    foreach (var equipment in stf.ReadStringBlock("").ToLowerInvariant().Replace(" ", "").Split(','))
                    {
                        switch (equipment)
                        {
                            case "triple_valve":
                                BrakeValve = BrakeValveType.TripleValve;
                                break;
                            case "distributor":
                            case "graduated_release_triple_valve":
                                BrakeValve = BrakeValveType.Distributor;
                                break;
                            case "emergency_brake_reservoir":
                                EmergencyReservoirPresent = true;
                                break;
                            case "handbrake":
                                HandBrakePresent = true;
                                break;

                            case "auxilary_reservoir": // MSTS legacy parameter - use is discouraged
                            case "auxiliary_reservoir":
                                AuxiliaryReservoirPresent = true;
                                break;
                            case "manual_brake":
                                ManualBrakePresent = true;
                                break;
                            case "retainer_3_position":
                                RetainerPositions = 3;
                                break;
                            case "retainer_4_position":
                                RetainerPositions = 4;
                                break;
                        }
                    }
                    break;
                case "wagon(coupling":
                    couplerLocation = couplerLocation.Next();
                    if (couplers[couplerLocation] == null)
                        couplers[couplerLocation] = new Coupler();
                    break;

                // Used for simple or legacy coupler
                case "wagon(coupling(spring(break":
                    stf.MustMatch("(");
                    couplers[couplerLocation].SetSimpleBreak(stf.ReadFloat(STFReader.Units.Force, null), stf.ReadFloat(STFReader.Units.Force, null));
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(coupling(spring(r0":
                    stf.MustMatch("(");
                    couplers[couplerLocation].SetSimpleR0(stf.ReadFloat(STFReader.Units.Distance, null), stf.ReadFloat(STFReader.Units.Distance, null));
                    stf.SkipRestOfBlock();
                    break;

                case "wagon(coupling(spring(stiffness":
                    stf.MustMatch("(");
                    couplers[couplerLocation].SetSimpleStiffness(stf.ReadFloat(STFReader.Units.Stiffness, null), stf.ReadFloat(STFReader.Units.Stiffness, null));
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(coupling(spring(ortsslack":
                    stf.MustMatch("(");
                    // IsAdvancedCoupler = true; // If this parameter is present in WAG file then treat coupler as advanced ones.  Temporarily disabled for v1.3 release
                    couplers[couplerLocation].SetSlack(stf.ReadFloat(STFReader.Units.Distance, null), stf.ReadFloat(STFReader.Units.Distance, null));
                    stf.SkipRestOfBlock();
                    break;

                // Used for advanced coupler
                case "wagon(coupling(frontcoupleranim":
                    FrontCouplerAnimation = new ShapeAnimation(stf);
                    break;
                case "wagon(coupling(frontairhoseanim":
                    FrontAirHoseAnimation = new ShapeAnimation(stf);
                    break;
                case "wagon(coupling(rearcoupleranim":
                    RearCouplerAnimation = new ShapeAnimation(stf);
                    break;
                case "wagon(coupling(rearairhoseanim":
                    RearAirHoseAnimation = new ShapeAnimation(stf);
                    break;
                case "wagon(coupling(spring(ortstensionstiffness":
                    stf.MustMatch("(");
                    couplers[couplerLocation].SetTensionStiffness(stf.ReadFloat(STFReader.Units.Force, null), stf.ReadFloat(STFReader.Units.Force, null));
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(coupling(frontcoupleropenanim":
                    FrontCouplerOpenAnimation = new ShapeAnimation(stf);
                    FrontCouplerOpenFitted = true;
                    break;
                case "wagon(coupling(rearcoupleropenanim":
                    RearCouplerOpenAnimation = new ShapeAnimation(stf);
                    RearCouplerOpenFitted = true;
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(coupling(frontairhosediconnectedanim":
                    FrontAirHoseDisconnectedAnimation = new ShapeAnimation(stf);
                    break;
                case "wagon(coupling(rearairhosediconnectedanim":
                    RearAirHoseDisconnectedAnimation = new ShapeAnimation(stf);
                    break;
                case "wagon(coupling(spring(ortscompressionstiffness":
                    stf.MustMatch("(");
                    couplers[couplerLocation].SetCompressionStiffness(stf.ReadFloat(STFReader.Units.Force, null), stf.ReadFloat(STFReader.Units.Force, null));
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(coupling(spring(ortstensionslack":
                    stf.MustMatch("(");
                    avancedCoupler = true; // If this parameter is present in WAG file then treat coupler as advanced ones.
                    couplers[couplerLocation].SetTensionSlack(stf.ReadFloat(STFReader.Units.Distance, null), stf.ReadFloat(STFReader.Units.Distance, null));
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(coupling(spring(ortscompressionslack":
                    stf.MustMatch("(");
                    avancedCoupler = true; // If this parameter is present in WAG file then treat coupler as advanced ones.
                    couplers[couplerLocation].SetCompressionSlack(stf.ReadFloat(STFReader.Units.Distance, null), stf.ReadFloat(STFReader.Units.Distance, null));
                    stf.SkipRestOfBlock();
                    break;
                // This is for the advanced coupler and is designed to be used instead of the MSTS parameter Break

                case "wagon(coupling(spring(ortsbreak":
                    stf.MustMatch("(");
                    couplers[couplerLocation].SetAdvancedBreak(stf.ReadFloat(STFReader.Units.Force, null), stf.ReadFloat(STFReader.Units.Force, null));
                    stf.SkipRestOfBlock();
                    break;
                // This is for the advanced coupler and is designed to be used instead of the MSTS parameter R0
                case "wagon(coupling(spring(ortstensionr0":
                    stf.MustMatch("(");
                    couplers[couplerLocation].SetTensionR0(stf.ReadFloat(STFReader.Units.Distance, null), stf.ReadFloat(STFReader.Units.Distance, null));
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(coupling(spring(ortscompressionr0":
                    stf.MustMatch("(");
                    couplers[couplerLocation].SetCompressionR0(stf.ReadFloat(STFReader.Units.Distance, null), stf.ReadFloat(STFReader.Units.Distance, null));
                    stf.SkipRestOfBlock();
                    break;
                // Used for both coupler types
                case "wagon(coupling(couplinghasrigidconnection":
                    couplers[couplerLocation].Rigid = stf.ReadBoolBlock(true);
                    break;
                case "wagon(adheasion":
                    stf.MustMatch("(");
                    Adhesion1 = stf.ReadFloat(STFReader.Units.None, null);
                    Adhesion2 = stf.ReadFloat(STFReader.Units.None, null);
                    Adhesion3 = stf.ReadFloat(STFReader.Units.None, null);
                    stf.ReadFloat(STFReader.Units.None, null);
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(ortsadhesion(ortscurtius_kniffler":
                    //e.g. Wagon ( ORTSAdhesion ( ORTSCurtius_Kniffler ( 7.5 44 0.161 0.7 ) ) )
                    stf.MustMatch("(");
                    Curtius_KnifflerA = stf.ReadFloat(STFReader.Units.None, 7.5f);
                    if (Curtius_KnifflerA <= 0)
                        Curtius_KnifflerA = 7.5f;
                    Curtius_KnifflerB = stf.ReadFloat(STFReader.Units.None, 44.0f);
                    if (Curtius_KnifflerB <= 0)
                        Curtius_KnifflerB = 44.0f;
                    Curtius_KnifflerC = stf.ReadFloat(STFReader.Units.None, 0.161f);
                    if (Curtius_KnifflerC <= 0)
                        Curtius_KnifflerC = 0.161f;
                    AdhesionK = stf.ReadFloat(STFReader.Units.None, 0.7f);
                    if (AdhesionK <= 0)
                        AdhesionK = 0.7f;
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(ortsadhesion(ortsslipwarningthreshold":
                    stf.MustMatch("(");
                    SlipWarningThresholdPercent = stf.ReadFloat(STFReader.Units.None, 70.0f);
                    if (SlipWarningThresholdPercent <= 0)
                        SlipWarningThresholdPercent = 70.0f;
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(ortsadhesion(wheelset(axle(ortsinertia":
                    stf.MustMatch("(");
                    AxleInertiaKgm2 = stf.ReadFloat(STFReader.Units.RotationalInertia, null);
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(ortsadhesion(wheelset(axle(ortsradius":
                    stf.MustMatch("(");
                    AdhesionDriveWheelRadiusM = stf.ReadFloat(STFReader.Units.Distance, null);
                    stf.SkipRestOfBlock();
                    break;
                case "wagon(lights":
                    Lights = new Lights(stf);
                    break;
                case "wagon(inside":
                    HasInsideView = true;
                    ParseWagonInside(stf);
                    break;
                case "wagon(orts3dcab":
                    Parse3DCab(stf);
                    break;
                case "wagon(numwheels":
                    wagonNumWheels = stf.ReadFloatBlock(STFReader.Units.None, 4.0f);
                    break;
                case "wagon(ortsnumberaxles":
                    initWagonNumAxles = stf.ReadIntBlock(null);
                    break;
                //case "wagon(ortsnumberbogies":
                //    WagonNumBogies = stf.ReadIntBlock(null);
                //    break;
                case "wagon(ortspantographs":
                    Pantographs.Parse(lowercasetoken, stf);
                    break;
                case "wagon(ortsdoors(closingdelay":
                case "wagon(ortsdoors(openingdelay":
                    Doors.Parse(lowercasetoken, stf);
                    break;
                case "wagon(ortspowersupply":
                case "wagon(ortspowerondelay":
                case "wagon(ortsbattery(mode":
                case "wagon(ortsbattery(delay":
                case "wagon(ortsbattery(defaulton":
                case "wagon(ortspowersupplycontinuouspower":
                case "wagon(ortspowersupplyheatingpower":
                case "wagon(ortspowersupplyairconditioningpower":
                case "wagon(ortspowersupplyairconditioningyield":
                    if (this is MSTSLocomotive)
                    {
                        Trace.TraceWarning($"Defining the {lowercasetoken} parameter is forbidden for locomotives (in {stf.FileName}:line {stf.LineNumber})");
                    }
                    else if (PassengerCarPowerSupply == null)
                    {
                        PowerSupply = new ScriptedPassengerCarPowerSupply(this);
                    }
                    PassengerCarPowerSupply?.Parse(lowercasetoken, stf);
                    break;

                case "wagon(intakepoint":
                    IntakePointList.Add(new IntakePoint(stf));
                    break;
                case "wagon(passengercapacity":
                    PassengerCapacity = (int)stf.ReadFloatBlock(STFReader.Units.None, 0);
                    break;
                case "wagon(ortsfreightanims":
                    FreightAnimations = new FreightAnimations(stf, this);
                    break;
                case "wagon(ortsexternalsoundpassedthroughpercent":
                    ExternalSoundPassThruPercent = stf.ReadFloatBlock(STFReader.Units.None, -1);
                    break;
                case "wagon(ortsalternatepassengerviewpoints": // accepted only if there is already a passenger viewpoint
                    if (HasInsideView)
                    {
                        ParseAlternatePassengerViewPoints(stf);
                    }
                    else
                        stf.SkipRestOfBlock();
                    break;
                default:
                    if (MSTSBrakeSystem != null)
                        MSTSBrakeSystem.Parse(lowercasetoken, stf);
                    break;
            }
        }

        /// <summary>
        /// This initializer is called when we are making a new copy of a car already
        /// loaded in memory.  We use this one to speed up loading by eliminating the
        /// need to parse the wag file multiple times.
        /// 
        /// IMPORTANT NOTE:  everything you initialized in parse, must be initialized here
        /// </summary>
        public virtual void Copy(MSTSWagon source)
        {
            ArgumentNullException.ThrowIfNull(source);

            MainShapeFileName = source.MainShapeFileName;
            PassengerCapacity = source.PassengerCapacity;
            WagonType = source.WagonType;
            WagonSpecialType = source.WagonSpecialType;
            FreightShapeFileName = source.FreightShapeFileName;
            FreightAnimMaxLevelM = source.FreightAnimMaxLevelM;
            FreightAnimMinLevelM = source.FreightAnimMinLevelM;
            FreightAnimFlag = source.FreightAnimFlag;
            FrontCouplerAnimation = source.FrontCouplerAnimation;
            FrontCouplerOpenAnimation = source.FrontCouplerOpenAnimation;
            RearCouplerAnimation = source.RearCouplerAnimation;
            RearCouplerOpenAnimation = source.RearCouplerOpenAnimation;
            FrontCouplerOpenFitted = source.FrontCouplerOpenFitted;
            RearCouplerOpenFitted = source.RearCouplerOpenFitted;

            FrontAirHoseAnimation = source.FrontAirHoseAnimation;
            FrontAirHoseDisconnectedAnimation = source.FrontAirHoseDisconnectedAnimation;
            RearAirHoseAnimation = source.RearAirHoseAnimation;
            RearAirHoseDisconnectedAnimation = source.RearAirHoseDisconnectedAnimation;

            CarWidthM = source.CarWidthM;
            CarHeightM = source.CarHeightM;
            CarLengthM = source.CarLengthM;
            trackGauge = source.trackGauge;
            centreOfGravityM = source.centreOfGravityM;
            initialCentreOfGravityM = source.initialCentreOfGravityM;
            unbalancedSuperElevation = source.unbalancedSuperElevation;
            rigidWheelBaseM = source.rigidWheelBaseM;
            CarBogieCentreLength = source.CarBogieCentreLength;
            CarBodyLength = source.CarBodyLength;
            CarCouplerFaceLength = source.CarCouplerFaceLength;
            airHoseLengthM = source.airHoseLengthM;
            airHoseHorizontalLengthM = source.airHoseHorizontalLengthM;
            maximumWheelFlangeAngle = source.maximumWheelFlangeAngle;
            wheelFlangeLength = source.wheelFlangeLength;
            AuxTenderWaterMassKG = source.AuxTenderWaterMassKG;
            TenderWagonMaxCoalMassKG = source.TenderWagonMaxCoalMassKG;
            TenderWagonMaxWaterMassKG = source.TenderWagonMaxWaterMassKG;
            initWagonNumAxles = source.initWagonNumAxles;
            derailmentCoefficientEnabled = source.derailmentCoefficientEnabled;
            wagonNumAxles = source.wagonNumAxles;
            wagonNumWheels = source.wagonNumWheels;
            MassKG = source.MassKG;
            InitialMassKG = source.InitialMassKG;
            WheelRadiusM = source.WheelRadiusM;
            DriverWheelRadiusM = source.DriverWheelRadiusM;
            MainSoundFileName = source.MainSoundFileName;
            BrakeShoeFrictionFactor = source.BrakeShoeFrictionFactor;
            WheelBrakeSlideProtectionFitted = source.WheelBrakeSlideProtectionFitted;
            WheelBrakeSlideProtectionLimitDisabled = source.WheelBrakeSlideProtectionLimitDisabled;
            initialMaxBrakeForce = source.initialMaxBrakeForce;
            initialMaxHandbrakeForce = source.initialMaxHandbrakeForce;
            MaxBrakeForceN = source.MaxBrakeForceN;
            MaxHandbrakeForceN = source.MaxHandbrakeForceN;
            windowDeratingFactor = source.windowDeratingFactor;
            DesiredCompartmentTempSetpointC = source.DesiredCompartmentTempSetpointC;
            compartmentHeatingPipeAreaFactor = source.compartmentHeatingPipeAreaFactor;
            mainSteamHeatPipeOuterDiaM = source.mainSteamHeatPipeOuterDiaM;
            mainSteamHeatPipeInnerDiaM = source.mainSteamHeatPipeInnerDiaM;
            carConnectSteamHoseInnerDiaM = source.carConnectSteamHoseInnerDiaM;
            carConnectSteamHoseOuterDiaM = source.carConnectSteamHoseOuterDiaM;
            maximumSteamHeatBoilerWaterTankCapacityL = source.maximumSteamHeatBoilerWaterTankCapacityL;
            maximiumSteamHeatBoilerFuelTankCapacityL = source.maximiumSteamHeatBoilerFuelTankCapacityL;
            TrainHeatBoilerWaterUsageGalukpH = new Interpolator(source.TrainHeatBoilerWaterUsageGalukpH);
            TrainHeatBoilerFuelUsageGalukpH = new Interpolator(source.TrainHeatBoilerFuelUsageGalukpH);
            DavisAN = source.DavisAN;
            DavisBNSpM = source.DavisBNSpM;
            DavisCNSSpMM = source.DavisCNSSpMM;
            DavisDragConstant = source.DavisDragConstant;
            WagonFrontalAreaM2 = source.WagonFrontalAreaM2;
            TrailLocoResistanceFactor = source.TrailLocoResistanceFactor;
            FrictionC1 = source.FrictionC1;
            FrictionE1 = source.FrictionE1;
            FrictionV2 = source.FrictionV2;
            FrictionC2 = source.FrictionC2;
            FrictionE2 = source.FrictionE2;
            EffectData = source.EffectData;
            IsBelowMergeSpeed = source.IsBelowMergeSpeed;
            StandstillFrictionN = source.StandstillFrictionN;
            MergeSpeedFrictionN = source.MergeSpeedFrictionN;
            MergeSpeedMpS = source.MergeSpeedMpS;
            IsDavisFriction = source.IsDavisFriction;
            IsRollerBearing = source.IsRollerBearing;
            IsLowTorqueRollerBearing = source.IsLowTorqueRollerBearing;
            IsFrictionBearing = source.IsFrictionBearing;
            IsGreaseFrictionBearing = source.IsGreaseFrictionBearing;
            BrakeSystemType = source.BrakeSystemType;
            BrakeSystem = MSTSBrakeSystem.Create(BrakeSystemType, this);
            EmergencyReservoirPresent = source.EmergencyReservoirPresent;
            BrakeValve = source.BrakeValve;
            HandBrakePresent = source.HandBrakePresent;
            ManualBrakePresent = source.ManualBrakePresent;
            AuxiliaryReservoirPresent = source.AuxiliaryReservoirPresent;
            RetainerPositions = source.RetainerPositions;
            InteriorShapeFileName = source.InteriorShapeFileName;
            InteriorSoundFileName = source.InteriorSoundFileName;
            Cab3DShapeFileName = source.Cab3DShapeFileName;
            Cab3DSoundFileName = source.Cab3DSoundFileName;
            Adhesion1 = source.Adhesion1;
            Adhesion2 = source.Adhesion2;
            Adhesion3 = source.Adhesion3;
            Curtius_KnifflerA = source.Curtius_KnifflerA;
            Curtius_KnifflerB = source.Curtius_KnifflerB;
            Curtius_KnifflerC = source.Curtius_KnifflerC;
            AdhesionK = source.AdhesionK;
            AxleInertiaKgm2 = source.AxleInertiaKgm2;
            AdhesionDriveWheelRadiusM = source.AdhesionDriveWheelRadiusM;
            SlipWarningThresholdPercent = source.SlipWarningThresholdPercent;
            Lights = source.Lights;
            ExternalSoundPassThruPercent = source.ExternalSoundPassThruPercent;
            if (source.PassengerViewpoints != null)
                PassengerViewpoints = new List<PassengerViewPoint>(source.PassengerViewpoints);
            if (source.HeadOutViewpoints != null)
                HeadOutViewpoints = new List<ViewPoint>(source.HeadOutViewpoints);
            if (source.CabViewpoints != null)
                CabViewpoints = new List<PassengerViewPoint>(source.CabViewpoints);
            avancedCoupler = source.avancedCoupler;
            couplers = new EnumArray<Coupler, TrainCarLocation>(source.couplers);
            Pantographs.Copy(source.Pantographs);
            Doors.Copy(source.Doors);

            if (source.FreightAnimations != null)
            {
                FreightAnimations = new FreightAnimations(source.FreightAnimations, this);
            }

            LoadEmptyMassKg = source.LoadEmptyMassKg;
            LoadEmptyCentreOfGravityM_Y = source.LoadEmptyCentreOfGravityM_Y;
            LoadEmptyMaxBrakeForceN = source.LoadEmptyMaxBrakeForceN;
            LoadEmptyMaxHandbrakeForceN = source.LoadEmptyMaxHandbrakeForceN;
            LoadEmptyORTSDavis_A = source.LoadEmptyORTSDavis_A;
            LoadEmptyORTSDavis_B = source.LoadEmptyORTSDavis_B;
            LoadEmptyORTSDavis_C = source.LoadEmptyORTSDavis_C;
            LoadEmptyDavisDragConstant = source.LoadEmptyDavisDragConstant;
            LoadEmptyWagonFrontalAreaM2 = source.LoadEmptyWagonFrontalAreaM2;
            LoadFullMassKg = source.LoadFullMassKg;
            LoadFullCentreOfGravityM_Y = source.LoadFullCentreOfGravityM_Y;
            LoadFullMaxBrakeForceN = source.LoadFullMaxBrakeForceN;
            LoadFullMaxHandbrakeForceN = source.LoadFullMaxHandbrakeForceN;
            LoadFullORTSDavis_A = source.LoadFullORTSDavis_A;
            LoadFullORTSDavis_B = source.LoadFullORTSDavis_B;
            LoadFullORTSDavis_C = source.LoadFullORTSDavis_C;
            LoadFullDavisDragConstant = source.LoadFullDavisDragConstant;
            LoadFullWagonFrontalAreaM2 = source.LoadFullWagonFrontalAreaM2;

            if (source.IntakePointList != null)
            {
                foreach (IntakePoint copyIntakePoint in source.IntakePointList)
                {
                    // If freight animations not used or else wagon is a tender or locomotive, use the "MSTS" type IntakePoints if present in WAG / ENG file

                    if (copyIntakePoint.LinkedFreightAnim == null)
                        //     if (copyIntakePoint.LinkedFreightAnim == null || WagonType == WagonTypes.Engine || WagonType == WagonTypes.Tender || AuxWagonType == "AuxiliaryTender")
                        IntakePointList.Add(new IntakePoint(copyIntakePoint));
                }
            }

            MSTSBrakeSystem.InitializeFrom(source.BrakeSystem);
            if (source.WeightLoadController != null)
                WeightLoadController = new MSTSNotchController(source.WeightLoadController);

            if (source.PassengerCarPowerSupply != null)
            {
                PowerSupply = new ScriptedPassengerCarPowerSupply(this);
                PassengerCarPowerSupply.Copy(source.PassengerCarPowerSupply);
            }
        }

        protected void ParseWagonInside(STFReader stf)
        {
            PassengerViewPoint passengerViewPoint = new PassengerViewPoint();
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("sound", ()=>{ InteriorSoundFileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("passengercabinfile", ()=>{ InteriorShapeFileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("passengercabinheadpos", ()=>{ passengerViewPoint.Location = stf.ReadVector3Block(STFReader.Units.Distance, new Vector3()); }),
                new STFReader.TokenProcessor("rotationlimit", ()=>{ passengerViewPoint.RotationLimit = stf.ReadVector3Block(STFReader.Units.None, new Vector3()); }),
                new STFReader.TokenProcessor("startdirection", ()=>{ passengerViewPoint.StartDirection = stf.ReadVector3Block(STFReader.Units.None, new Vector3()); }),
            });
            // Set initial direction
            passengerViewPoint.RotationXRadians = MathHelper.ToRadians(passengerViewPoint.StartDirection.X);
            passengerViewPoint.RotationYRadians = MathHelper.ToRadians(passengerViewPoint.StartDirection.Y);
            PassengerViewpoints ??= new List<PassengerViewPoint>();
            PassengerViewpoints.Add(passengerViewPoint);
        }

        protected void Parse3DCab(STFReader stf)
        {
            PassengerViewPoint passengerViewPoint = new PassengerViewPoint();
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("sound", ()=>{ Cab3DSoundFileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("orts3dcabfile", ()=>{ Cab3DShapeFileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("orts3dcabheadpos", ()=>{ passengerViewPoint.Location = stf.ReadVector3Block(STFReader.Units.Distance, new Vector3()); }),
                new STFReader.TokenProcessor("rotationlimit", ()=>{ passengerViewPoint.RotationLimit = stf.ReadVector3Block(STFReader.Units.None, new Vector3()); }),
                new STFReader.TokenProcessor("startdirection", ()=>{ passengerViewPoint.StartDirection = stf.ReadVector3Block(STFReader.Units.None, new Vector3()); }),
            });
            // Set initial direction
            passengerViewPoint.RotationXRadians = MathHelper.ToRadians(passengerViewPoint.StartDirection.X);
            passengerViewPoint.RotationYRadians = MathHelper.ToRadians(passengerViewPoint.StartDirection.Y);
            CabViewpoints ??= new List<PassengerViewPoint>();
            CabViewpoints.Add(passengerViewPoint);
        }

        // parses additional passenger viewpoints, if any
        protected void ParseAlternatePassengerViewPoints(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new[] {
                new STFReader.TokenProcessor("ortsalternatepassengerviewpoint", ()=>{ ParseWagonInside(stf); }),
            });
        }

        public override async ValueTask<TrainCarSaveState> Snapshot()
        {
            TrainCarSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.WagonSaveState = new WagonSaveState()
            {
                WagonFile = WagFilePath,
                PantographSaveStates = await Pantographs.SnapshotCollection<PantographSaveState, Pantograph>().ConfigureAwait(false),
                DoorSaveStates = await Task.WhenAll(Doors.Select(async door => await door.Snapshot().ConfigureAwait(false))),
                CouplerSaveStates = await Task.WhenAll(couplers.Select(async coupler => coupler == null ? null : await coupler.Snapshot().ConfigureAwait(false))),
                SoundValues = soundDebugValues,
                Friction = Friction0N,
                DavisA = DavisAN,
                DavisB = DavisBNSpM,
                DavisC = DavisCNSSpMM,
                StandstillFriction = StandstillFrictionN,
                MergeSpeedFriction = MergeSpeedFrictionN,
                BelowMergeSpeed = IsBelowMergeSpeed,
                Mass = MassKG,
                MaxBrakeForce = MaxBrakeForceN,
                MaxHandbrakeForce = MaxHandbrakeForceN,
                CurrentSteamHeatBoilerFuelCapacity = currentSteamHeatBoilerFuelCapacityL,
                CurrentCarSteamHeatBoilerWaterCapacity = currentCarSteamHeatBoilerWaterCapacityL,
                CarInsideTemp = CarInsideTempC,
                WheelBrakeSlideProtectionActive = WheelBrakeSlideProtectionActive,
                WheelBrakeSlideProtectionTimer = WheelBrakeSlideProtectionTimerS,
                DerailPossible = DerailPossible,
                DerailExpected = DerailExpected,
                DerailClimbDistance = derailClimbDistance,
                DerailElapsedTime = derailElapsedTime,
                PowerSupplySaveStates = PassengerCarPowerSupply == null ? null : await PassengerCarPowerSupply.Snapshot().ConfigureAwait(false),
                FreightAnimationsSaveState = FreightAnimations == null ? null : await FreightAnimations.Snapshot().ConfigureAwait(false),                
                WeightControllerSaveState = WeightLoadController == null ? null : await WeightLoadController.Snapshot().ConfigureAwait(false),
            };
            return saveState;
        }

        public override async ValueTask Restore([NotNull] TrainCarSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(saveState.WagonSaveState, nameof(saveState.WagonSaveState));

            WagonSaveState wagonSaveState = saveState.WagonSaveState;

            soundDebugValues = wagonSaveState.SoundValues;
            Friction0N = wagonSaveState.Friction;
            DavisAN = wagonSaveState.DavisA;
            DavisBNSpM = wagonSaveState.DavisB;
            DavisCNSSpMM = wagonSaveState.DavisC;
            StandstillFrictionN = wagonSaveState.StandstillFriction;
            MergeSpeedFrictionN = wagonSaveState.MergeSpeedFriction;
            IsBelowMergeSpeed = wagonSaveState.BelowMergeSpeed;
            MassKG = wagonSaveState.Mass;
            MaxBrakeForceN = wagonSaveState.MaxBrakeForce;
            MaxHandbrakeForceN = wagonSaveState.MaxHandbrakeForce;

            currentSteamHeatBoilerFuelCapacityL = wagonSaveState.CurrentSteamHeatBoilerFuelCapacity;
            currentCarSteamHeatBoilerWaterCapacityL = wagonSaveState.CurrentCarSteamHeatBoilerWaterCapacity;
            CarInsideTempC = wagonSaveState.CarInsideTemp;

            WheelBrakeSlideProtectionActive = wagonSaveState.WheelBrakeSlideProtectionActive;
            WheelBrakeSlideProtectionTimerS = wagonSaveState.WheelBrakeSlideProtectionTimer;
            derailClimbDistance = wagonSaveState.DerailClimbDistance;
            DerailPossible = wagonSaveState.DerailPossible;
            DerailExpected = wagonSaveState.DerailExpected;
            derailElapsedTime = wagonSaveState.DerailElapsedTime;

            await Pantographs.RestoreCollectionCreateNewInstances(wagonSaveState.PantographSaveStates, Pantographs).ConfigureAwait(false);
            await Doors.RestoreCollectionOnExistingInstances(wagonSaveState.DoorSaveStates).ConfigureAwait(false);

            couplers = new EnumArray<Coupler, TrainCarLocation>(await Task.WhenAll(wagonSaveState.CouplerSaveStates.Select(async couplerSaveState =>
            {
                if (couplerSaveState != null)
                {
                    Coupler coupler = new Coupler();
                    await coupler.Restore(couplerSaveState).ConfigureAwait(false);
                    return coupler;
                }
                return null;
            })).ConfigureAwait(false));
            if (null != PassengerCarPowerSupply)
                await PassengerCarPowerSupply.Restore(wagonSaveState.PowerSupplySaveStates).ConfigureAwait(false);
            if (null != FreightAnimations)
                await FreightAnimations.Restore(wagonSaveState.FreightAnimationsSaveState).ConfigureAwait(false);
            if (null != WeightLoadController)
                await WeightLoadController.Restore(wagonSaveState.WeightControllerSaveState).ConfigureAwait(false);

            soundDebugValues = wagonSaveState.SoundValues;
        }

        public override void Update(double elapsedClockSeconds)
        {
            base.Update(elapsedClockSeconds);

            PassengerCarPowerSupply?.Update(elapsedClockSeconds);

            ConfirmSteamLocomotiveTender(); // Confirms that a tender is connected to the steam locomotive

            // Adjusts water and coal mass based upon values assigned to the tender found in the WAG file rather then those defined in ENG file.
            if (WagonType == WagonType.Tender && TenderWeightInitialize && TenderWagonMaxCoalMassKG != 0 && TenderWagonMaxWaterMassKG != 0)
            {

                // Find the associated steam locomotive for this tender
                if (TendersSteamLocomotive == null)
                    FindTendersSteamLocomotive();

                // If no locomotive is found to be associated with this tender, then OR crashes, ie TendersSteamLocomotive is still null. 
                // This message will provide the user with information to correct the problem
                if (TendersSteamLocomotive == null)
                {
                    Trace.TraceInformation("Tender @ position {0} does not have a locomotive associated with. Check that it is preceeded by a steam locomotive.", CarID);
                }

                if (TendersSteamLocomotive != null)
                {
                    if (TendersSteamLocomotive.IsTenderRequired == 1)
                    {
                        // Combined total water found by taking the current combined water (which may have extra water added via the auxiliary tender), and subtracting the 
                        // amount of water defined in the ENG file, and adding the water defined in the WAG file.
                        float TempMaxCombinedWater = TendersSteamLocomotive.MaxTotalCombinedWaterVolumeUKG;
                        TendersSteamLocomotive.MaxTotalCombinedWaterVolumeUKG = (float)((TempMaxCombinedWater - (Mass.Kilogram.ToLb(TendersSteamLocomotive.MaxLocoTenderWaterMassKG) / WaterLBpUKG)) + (Mass.Kilogram.ToLb(TenderWagonMaxWaterMassKG) / WaterLBpUKG));

                        TendersSteamLocomotive.MaxTenderCoalMassKG = TenderWagonMaxCoalMassKG;
                        TendersSteamLocomotive.MaxLocoTenderWaterMassKG = TenderWagonMaxWaterMassKG;

                        if (simulator.Settings.VerboseConfigurationMessages)
                        {
                            Trace.TraceInformation("Fuel and Water Masses adjusted to Tender Values Specified in WAG File - Coal mass {0} kg, Water Mass {1}", FormatStrings.FormatMass(TendersSteamLocomotive.MaxTenderCoalMassKG, simulator.MetricUnits),
                                FormatStrings.FormatFuelVolume(Size.LiquidVolume.FromGallonUK(TendersSteamLocomotive.MaxTotalCombinedWaterVolumeUKG), simulator.MetricUnits, simulator.Settings.MeasurementUnit == MeasurementUnit.UK));
                        }
                    }
                }

                // Rest flag so that this loop is not executed again
                TenderWeightInitialize = false;
            }

            UpdateTenderLoad(); // Updates the load physics characteristics of tender and aux tender

            UpdateLocomotiveLoadPhysics(); // Updates the load physics characteristics of locomotives

            UpdateSpecialEffects(elapsedClockSeconds); // Updates the wagon special effects

            //// Update Aux Tender Information

            //// TODO: Replace AuxWagonType with new values of WagonType or similar. It's a bad idea having two fields that are nearly the same but not quite.
            //if (AuxTenderWaterMassKG != 0)   // SetStreamVolume wagon type for later use
            //{

            //    AuxWagonType = "AuxiliaryTender";
            //}
            //else
            //{
            //    AuxWagonType = WagonType.ToString();
            //}

#if DEBUG_AUXTENDER
            Trace.TraceInformation("***************************************** DEBUG_AUXTENDER (MSTSWagon.cs) ***************************************************************");
            Trace.TraceInformation("Car ID {0} Aux Tender Water Mass {1} Wagon Type {2}", CarID, AuxTenderWaterMassKG, AuxWagonType);
#endif

            AbsWheelSpeedMpS = Math.Abs(WheelSpeedMpS);

            UpdateTrainBaseResistance();

            UpdateWindForce();

            UpdateWheelBearingTemperature(elapsedClockSeconds);

            foreach (Coupler coupler in couplers)
            {

                // Test to see if coupler forces have exceeded the Proof (or safety limit). Exceeding this limit will provide an indication only
                if (IsPlayerTrain)
                {
                    if (Math.Abs(CouplerForceU) > GetCouplerBreak1N() || Math.Abs(ImpulseCouplerForceUN) > GetCouplerBreak1N())  // break couplers if either static or impulse forces exceeded
                    {
                        CouplerOverloaded = true;
                    }
                    else
                    {
                        CouplerOverloaded = false;
                    }
                }
                else
                {
                    CouplerOverloaded = false;
                }

                // Test to see if coupler forces have been exceeded, and coupler has broken. Exceeding this limit will break the coupler
                if (IsPlayerTrain) // Only break couplers on player trains
                {
                    if (Math.Abs(CouplerForceU) > GetCouplerBreak2N() || Math.Abs(ImpulseCouplerForceUN) > GetCouplerBreak2N())  // break couplers if either static or impulse forces exceeded
                    {
                        CouplerExceedBreakLimit = true;

                        if (Math.Abs(CouplerForceU) > GetCouplerBreak2N())
                        {
                            Trace.TraceInformation("Coupler on CarID {0} has broken due to excessive static coupler force {1}", CarID, CouplerForceU);

                        }
                        else if (Math.Abs(ImpulseCouplerForceUN) > GetCouplerBreak2N())
                        {
                            Trace.TraceInformation("Coupler on CarID {0} has broken due to excessive impulse coupler force {1}", CarID, ImpulseCouplerForceUN);
                        }
                    }
                    else
                    {
                        CouplerExceedBreakLimit = false;
                    }
                }
                else // if not a player train then don't ever break the couplers
                {
                    CouplerExceedBreakLimit = false;
                }
            }

            Pantographs.Update(elapsedClockSeconds);

            Doors.Update(elapsedClockSeconds);

            MSTSBrakeSystem.Update(elapsedClockSeconds);

            // Updates freight load animations when defined in WAG file - Locomotive and Tender load animation are done independently in UpdateTenderLoad() & UpdateLocomotiveLoadPhysics()
            if (WeightLoadController != null && WagonType != WagonType.Tender && AuxWagonType != AuxWagonType.AuxiliaryTender && WagonType != WagonType.Engine)
            {
                WeightLoadController.Update(elapsedClockSeconds);
                if (FreightAnimations.LoadedOne != null)
                {
                    FreightAnimations.LoadedOne.LoadPerCent = WeightLoadController.CurrentValue * 100;
                    FreightAnimations.FreightWeight = WeightLoadController.CurrentValue * FreightAnimations.LoadedOne.FreightWeightWhenFull;
                    if (IsPlayerTrain)
                    {
                        if (WeightLoadController.UpdateValue != 0.0)
                            simulator.Confirmer.UpdateWithPerCent(CabControl.FreightLoad,
                                CabSetting.Increase, WeightLoadController.CurrentValue * 100);
                        // Update wagon parameters sensitive to wagon mass change
                        // Calculate the difference ratio, ie how full the wagon is. This value allows the relevant value to be scaled from the empty mass to the full mass of the wagon
                        TempMassDiffRatio = WeightLoadController.CurrentValue;
                        // Update brake parameters
                        MaxBrakeForceN = ((LoadFullMaxBrakeForceN - LoadEmptyMaxBrakeForceN) * TempMassDiffRatio) + LoadEmptyMaxBrakeForceN;
                        MaxHandbrakeForceN = ((LoadFullMaxHandbrakeForceN - LoadEmptyMaxHandbrakeForceN) * TempMassDiffRatio) + LoadEmptyMaxHandbrakeForceN;
                        // Update friction related parameters
                        DavisAN = ((LoadFullORTSDavis_A - LoadEmptyORTSDavis_A) * TempMassDiffRatio) + LoadEmptyORTSDavis_A;
                        DavisBNSpM = ((LoadFullORTSDavis_B - LoadEmptyORTSDavis_B) * TempMassDiffRatio) + LoadEmptyORTSDavis_B;
                        DavisCNSSpMM = ((LoadFullORTSDavis_C - LoadEmptyORTSDavis_C) * TempMassDiffRatio) + LoadEmptyORTSDavis_C;

                        if (LoadEmptyDavisDragConstant > LoadFullDavisDragConstant) // Due to wind turbulence empty drag might be higher then loaded drag, and therefore both scenarios need to be covered.
                        {
                            DavisDragConstant = LoadEmptyDavisDragConstant - ((LoadEmptyDavisDragConstant - LoadFullDavisDragConstant) * TempMassDiffRatio);
                        }
                        else
                        {
                            DavisDragConstant = ((LoadFullDavisDragConstant - LoadEmptyDavisDragConstant) * TempMassDiffRatio) + LoadEmptyDavisDragConstant;
                        }

                        WagonFrontalAreaM2 = ((LoadFullWagonFrontalAreaM2 - LoadEmptyWagonFrontalAreaM2) * TempMassDiffRatio) + LoadEmptyWagonFrontalAreaM2;


                        // Update CoG related parameters
                        centreOfGravityM.Y = ((LoadFullCentreOfGravityM_Y - LoadEmptyCentreOfGravityM_Y) * TempMassDiffRatio) + LoadEmptyCentreOfGravityM_Y;
                    }
                }
                if (WeightLoadController.UpdateValue == 0.0 && FreightAnimations.LoadedOne != null && FreightAnimations.LoadedOne.LoadPerCent == 0.0)
                {
                    FreightAnimations.LoadedOne = null;
                    FreightAnimations.FreightType = PickupType.None;
                }

                if (WaitForAnimationReady && WeightLoadController.CommandStartTime + FreightAnimations.UnloadingStartDelay <= simulator.ClockTime)
                {
                    WaitForAnimationReady = false;
                    simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Starting unload"));
                    if (FreightAnimations.LoadedOne is FreightAnimationContinuous)
                        WeightLoadController.StartDecrease(WeightLoadController.MinimumValue);
                }
            }

            if (WagonType != WagonType.Tender && AuxWagonType != AuxWagonType.AuxiliaryTender && WagonType != WagonType.Engine)
            {
                // Updates mass when it carries containers
                float totalContainerMassKG = 0;
                if (FreightAnimations?.Animations != null)
                {
                    foreach (FreightAnimation anim in FreightAnimations.Animations)
                    {
                        if (anim is FreightAnimationDiscrete discreteAnim && discreteAnim.Container != null)
                        {
                            totalContainerMassKG += discreteAnim.Container.MassKG;
                        }
                    }
                }

                // Updates the mass of the wagon considering all types of loads
                if (FreightAnimations != null && FreightAnimations.WagonEmptyWeight != -1)
                {
                    CalculateTotalMass(totalContainerMassKG);
                }
            }
        }

        private void UpdateLocomotiveLoadPhysics()
        {
            // This section updates the weight and physics of the locomotive
            if (FreightAnimations != null && FreightAnimations.ContinuousFreightAnimationsPresent) // make sure that a freight animation INCLUDE File has been defined, and it contains "continuous" animation data.
            {
                if (this is MSTSSteamLocomotive)
                // If steam locomotive then water, and coal variations will impact the weight of the locomotive
                {
                    // set a process to pass relevant locomotive parameters from locomotive file to this wagon file
                    var LocoIndex = 0;
                    for (var i = 0; i < Train.Cars.Count; i++) // test each car to find where the steam locomotive is in the consist
                        if (Train.Cars[i] == this)  // If this car is a Steam locomotive then set loco index
                            LocoIndex = i;
                    if (Train.Cars[LocoIndex] is MSTSSteamLocomotive)
                        SteamLocomotiveIdentification = Train.Cars[LocoIndex] as MSTSSteamLocomotive;
                    if (SteamLocomotiveIdentification != null)
                    {
                        if (SteamLocomotiveIdentification.IsTenderRequired == 0) // Test to see if the locomotive is a tender locomotive or tank locomotive. 
                        // If = 0, then locomotive must be a tank type locomotive. A tank locomotive has the fuel (coal and water) onboard.
                        // Thus the loco weight changes as boiler level goes up and down, and coal mass varies with the fire mass. Also onboard fuel (coal and water ) will vary as used.
                        {
                            MassKG = (float)(LoadEmptyMassKg + Mass.Kilogram.FromLb(SteamLocomotiveIdentification.BoilerMassLB) + SteamLocomotiveIdentification.FireMassKG + SteamLocomotiveIdentification.TenderCoalMassKG + Mass.Kilogram.FromLb(SteamLocomotiveIdentification.CombinedTenderWaterVolumeUKG * WaterLBpUKG));
                            MassKG = MathHelper.Clamp(MassKG, LoadEmptyMassKg, LoadFullMassKg); // Clamp Mass to between the empty and full wagon values   
                            // Adjust drive wheel weight
                            SteamLocomotiveIdentification.DrvWheelWeightKg = (MassKG / InitialMassKG) * SteamLocomotiveIdentification.InitialDrvWheelWeightKg;
                        }
                        else // locomotive must be a tender type locomotive
                        // This is a tender locomotive. A tender locomotive does not have any fuel onboard.
                        // Thus the loco weight only changes as boiler level goes up and down, and coal mass varies in the fire
                        {
                            MassKG = (float)(LoadEmptyMassKg + Mass.Kilogram.FromLb(SteamLocomotiveIdentification.BoilerMassLB) + SteamLocomotiveIdentification.FireMassKG);
                            MassKG = MathHelper.Clamp(MassKG, LoadEmptyMassKg, LoadFullMassKg); // Clamp Mass to between the empty and full wagon values        
                                                                                                // Adjust drive wheel weight
                            SteamLocomotiveIdentification.DrvWheelWeightKg = (MassKG / InitialMassKG) * SteamLocomotiveIdentification.InitialDrvWheelWeightKg;
                        }

                        // Update wagon physics parameters sensitive to wagon mass change
                        // Calculate the difference ratio, ie how full the wagon is. This value allows the relevant value to be scaled from the empty mass to the full mass of the wagon
                        float TempTenderMassDiffRatio = (MassKG - LoadEmptyMassKg) / (LoadFullMassKg - LoadEmptyMassKg);
                        // Update brake parameters
                        MaxBrakeForceN = ((LoadFullMaxBrakeForceN - LoadEmptyMaxBrakeForceN) * TempMassDiffRatio) + LoadEmptyMaxBrakeForceN;
                        MaxHandbrakeForceN = ((LoadFullMaxHandbrakeForceN - LoadEmptyMaxHandbrakeForceN) * TempMassDiffRatio) + LoadEmptyMaxHandbrakeForceN;
                        // Update friction related parameters
                        DavisAN = ((LoadFullORTSDavis_A - LoadEmptyORTSDavis_A) * TempMassDiffRatio) + LoadEmptyORTSDavis_A;
                        DavisBNSpM = ((LoadFullORTSDavis_B - LoadEmptyORTSDavis_B) * TempMassDiffRatio) + LoadEmptyORTSDavis_B;
                        DavisCNSSpMM = ((LoadFullORTSDavis_C - LoadEmptyORTSDavis_C) * TempMassDiffRatio) + LoadEmptyORTSDavis_C;

                        if (LoadEmptyDavisDragConstant > LoadFullDavisDragConstant) // Due to wind turbulence empty drag might be higher then loaded drag, and therefore both scenarios need to be covered.
                        {
                            DavisDragConstant = LoadEmptyDavisDragConstant - ((LoadEmptyDavisDragConstant - LoadFullDavisDragConstant) * TempMassDiffRatio);
                        }
                        else
                        {
                            DavisDragConstant = ((LoadFullDavisDragConstant - LoadEmptyDavisDragConstant) * TempMassDiffRatio) + LoadEmptyDavisDragConstant;
                        }

                        WagonFrontalAreaM2 = ((LoadFullWagonFrontalAreaM2 - LoadEmptyWagonFrontalAreaM2) * TempMassDiffRatio) + LoadEmptyWagonFrontalAreaM2;

                        // Update CoG related parameters
                        centreOfGravityM.Y = ((LoadFullCentreOfGravityM_Y - LoadEmptyCentreOfGravityM_Y) * TempMassDiffRatio) + LoadEmptyCentreOfGravityM_Y;
                    }
                }

                else if (this is MSTSDieselLocomotive)
                // If diesel locomotive
                {
                    // set a process to pass relevant locomotive parameters from locomotive file to this wagon file
                    var LocoIndex = 0;
                    for (var i = 0; i < Train.Cars.Count; i++) // test each car to find the where the Diesel locomotive is in the consist
                        if (Train.Cars[i] == this)  // If this car is a Diesel locomotive then set loco index
                            LocoIndex = i;
                    if (Train.Cars[LocoIndex] is MSTSDieselLocomotive)
                        DieselLocomotiveIdentification = Train.Cars[LocoIndex] as MSTSDieselLocomotive;
                    if (DieselLocomotiveIdentification != null)
                    {

                        MassKG = LoadEmptyMassKg + (DieselLocomotiveIdentification.DieselLevelL * DieselLocomotiveIdentification.DieselWeightKgpL) + (float)DieselLocomotiveIdentification.CurrentLocomotiveSteamHeatBoilerWaterCapacityL;
                        MassKG = MathHelper.Clamp(MassKG, LoadEmptyMassKg, LoadFullMassKg); // Clamp Mass to between the empty and full wagon values  
                        // Adjust drive wheel weight
                        DieselLocomotiveIdentification.DrvWheelWeightKg = (MassKG / InitialMassKG) * DieselLocomotiveIdentification.InitialDrvWheelWeightKg;

                        // Update wagon parameters sensitive to wagon mass change
                        // Calculate the difference ratio, ie how full the wagon is. This value allows the relevant value to be scaled from the empty mass to the full mass of the wagon
                        float TempTenderMassDiffRatio = (MassKG - LoadEmptyMassKg) / (LoadFullMassKg - LoadEmptyMassKg);
                        // Update brake parameters
                        MaxBrakeForceN = ((LoadFullMaxBrakeForceN - LoadEmptyMaxBrakeForceN) * TempMassDiffRatio) + LoadEmptyMaxBrakeForceN;
                        MaxHandbrakeForceN = ((LoadFullMaxHandbrakeForceN - LoadEmptyMaxHandbrakeForceN) * TempMassDiffRatio) + LoadEmptyMaxHandbrakeForceN;
                        // Update friction related parameters
                        DavisAN = ((LoadFullORTSDavis_A - LoadEmptyORTSDavis_A) * TempMassDiffRatio) + LoadEmptyORTSDavis_A;
                        DavisBNSpM = ((LoadFullORTSDavis_B - LoadEmptyORTSDavis_B) * TempMassDiffRatio) + LoadEmptyORTSDavis_B;
                        DavisCNSSpMM = ((LoadFullORTSDavis_C - LoadEmptyORTSDavis_C) * TempMassDiffRatio) + LoadEmptyORTSDavis_C;

                        if (LoadEmptyDavisDragConstant > LoadFullDavisDragConstant) // Due to wind turbulence empty drag might be higher then loaded drag, and therefore both scenarios need to be covered.
                        {
                            DavisDragConstant = LoadEmptyDavisDragConstant - ((LoadEmptyDavisDragConstant - LoadFullDavisDragConstant) * TempMassDiffRatio);
                        }
                        else
                        {
                            DavisDragConstant = ((LoadFullDavisDragConstant - LoadEmptyDavisDragConstant) * TempMassDiffRatio) + LoadEmptyDavisDragConstant;
                        }

                        WagonFrontalAreaM2 = ((LoadFullWagonFrontalAreaM2 - LoadEmptyWagonFrontalAreaM2) * TempMassDiffRatio) + LoadEmptyWagonFrontalAreaM2;

                        // Update CoG related parameters
                        centreOfGravityM.Y = ((LoadFullCentreOfGravityM_Y - LoadEmptyCentreOfGravityM_Y) * TempMassDiffRatio) + LoadEmptyCentreOfGravityM_Y;

                    }
                }
            }
        }

        private void UpdateTrainBaseResistance()
        {
            IsBelowMergeSpeed = AbsSpeedMpS < MergeSpeedMpS;
            bool isStartingFriction = StandstillFrictionN != 0;

            if (IsDavisFriction) // If set to use next Davis friction then do so
            {
                if (isStartingFriction && IsBelowMergeSpeed) // Davis formulas only apply above merge speed, so different treatment required for low speed
                    UpdateTrainBaseResistance_StartingFriction();
                else if (IsBelowMergeSpeed)
                    UpdateTrainBaseResistance_DavisLowSpeed();
                else
                    UpdateTrainBaseResistance_DavisHighSpeed();
            }
            else if (isStartingFriction && IsBelowMergeSpeed)
            {
                UpdateTrainBaseResistance_StartingFriction();
            }
            else
            {
                UpdateTrainBaseResistance_ORTS();
            }
        }

        /// <summary>
        /// Update train base resistance with the conventional Open Rails algorithm.
        /// </summary>
        /// <remarks>
        /// For all speeds.
        /// </remarks>
        private void UpdateTrainBaseResistance_ORTS()
        {
            if (FrictionV2 < 0 || FrictionV2 > 4.4407f) // > 10 mph
            {   // not fcalc ignore friction and use default davis equation
                // Starting Friction 
                //
                //                      Above Freezing   Below Freezing
                //    Journal Bearing      25 lb/ton        35 lb/ton   (short ton)
                //     Roller Bearing       5 lb/ton        15 lb/ton
                //
                // [2009-10-25 from http://www.arema.org/publications/pgre/ ]
                //Friction0N = MassKG * 30f /* lb/ton */ * 4.84e-3f;  // convert lbs/short-ton to N/kg 
                DavisAN = 6.3743f * MassKG / 1000 + 128.998f * 4;
                DavisBNSpM = .49358f * MassKG / 1000;
                DavisCNSSpMM = .11979f * 100 / 10.76f;
                Friction0N = DavisAN * 2.0f;            //More firendly to high load trains and the new physics
            }
            else
            {   // probably fcalc, recover approximate davis equation
                float mps1 = FrictionV2;
                float mps2 = 80 * .44704f;
                float s = mps2 - mps1;
                float x1 = mps1 * mps1;
                float x2 = mps2 * mps2;
                float sx = (x2 - x1) / 2;
                float y0 = FrictionC1 * (float)Math.Pow(mps1, FrictionE1) + FrictionC2 * mps1;
                float y1 = FrictionC2 * (float)Math.Pow(mps1, FrictionE2) * mps1;
                float y2 = FrictionC2 * (float)Math.Pow(mps2, FrictionE2) * mps2;
                float sy = y0 * (mps2 - mps1) + (y2 - y1) / (1 + FrictionE2);
                y1 *= mps1;
                y2 *= mps2;
                float syx = y0 * (x2 - x1) / 2 + (y2 - y1) / (2 + FrictionE2);
                x1 *= mps1;
                x2 *= mps2;
                float sx2 = (x2 - x1) / 3;
                y1 *= mps1;
                y2 *= mps2;
                float syx2 = y0 * (x2 - x1) / 3 + (y2 - y1) / (3 + FrictionE2);
                x1 *= mps1;
                x2 *= mps2;
                float sx3 = (x2 - x1) / 4;
                x1 *= mps1;
                x2 *= mps2;
                float sx4 = (x2 - x1) / 5;
                float s1 = syx - sy * sx / s;
                float s2 = sx * sx2 / s - sx3;
                float s3 = sx2 - sx * sx / s;
                float s4 = syx2 - sy * sx2 / s;
                float s5 = sx2 * sx2 / s - sx4;
                float s6 = sx3 - sx * sx2 / s;
                DavisCNSSpMM = (s1 * s6 - s3 * s4) / (s3 * s5 - s2 * s6);
                DavisBNSpM = (s1 + DavisCNSSpMM * s2) / s3;
                DavisAN = (sy - DavisBNSpM * sx - DavisCNSSpMM * sx2) / s;
                Friction0N = FrictionC1;
                if (FrictionE1 < 0)
                    Friction0N *= (float)Math.Pow(.0025 * .44704, FrictionE1);
            }

            if (AbsSpeedMpS < 0.1f)
            {
                FrictionForceN = Friction0N;
            }
            else
            {
                FrictionForceN = DavisAN + AbsSpeedMpS * (DavisBNSpM + AbsSpeedMpS * DavisCNSSpMM);

                // if this car is a locomotive, but not the lead one then recalculate the resistance with lower value as drag will not be as high on trailing locomotives
                // Only the drag (C) factor changes if a trailing locomotive, so only running resistance, and not starting resistance needs to be corrected
                if (WagonType == WagonType.Engine && Train.LeadLocomotive != this)
                    FrictionForceN = DavisAN + AbsSpeedMpS * (DavisBNSpM + AbsSpeedMpS * (TrailLocoResistanceFactor * DavisCNSSpMM));

                // Test to identify whether a tender is attached to the leading engine, if not then the resistance should also be derated as for the locomotive
                bool IsLeadTender = false;
                if (WagonType == WagonType.Tender)
                {
                    bool PrevCarLead = false;
                    foreach (var car in Train.Cars)
                    {
                        // If this car is a tender and the previous car is the lead locomotive then set the flag so that resistance will be reduced
                        if (car == this && PrevCarLead)
                        {
                            IsLeadTender = true;
                            break;  // If the tender has been identified then break out of the loop, otherwise keep going until whole train is done.
                        }
                        // Identify whether car is a lead locomotive or not. This is kept for when the next iteration (next car) is checked.
                        PrevCarLead = Train.LeadLocomotive == car;
                    }

                    // If tender is coupled to a trailing locomotive then reduce resistance
                    if (!IsLeadTender)
                        FrictionForceN = DavisAN + AbsSpeedMpS * (DavisBNSpM + AbsSpeedMpS * (TrailLocoResistanceFactor * DavisCNSSpMM));
                }
            }
        }

        /// <summary>
        /// Update train base resistance with a manually specified starting friction.
        /// </summary>
        /// <remarks>
        /// For speeds slower than the merge speed.
        /// </remarks>
        private void UpdateTrainBaseResistance_StartingFriction()
        {
            // Dtermine the starting friction factor based upon the type of bearing
            float StartFrictionLoadN = StandstillFrictionN;  // Starting friction

            // Determine the starting resistance due to wheel bearing temperature
            // Note reference values in lbf and US tons - converted to metric values as appropriate
            // At -10 DegC it will be equal to the snowing value, as the temperature increases to 25 DegC, it will move towards the summer value
            // Assume a linear relationship between the two sets of points above and plot a straight line relationship.
            const float RunGrad = -0.0085714285714286f;
            const float RunIntersect = 1.2142857142857f;
            if (WheelBearingTemperatureDegC < -10) // Set to snowing (frozen value)
                StartFrictionLoadN = 1.2f;  // Starting friction, snowing
            else if (WheelBearingTemperatureDegC > 25) // Set to normal temperature value
                StartFrictionLoadN = 1.0f;  // Starting friction, not snowing
            else // Set to variable value as bearing heats and cools
                StartFrictionLoadN = RunGrad * WheelBearingTemperatureDegC + RunIntersect;
            StaticFrictionFactorN = StartFrictionLoadN;

            // Determine the running resistance due to wheel bearing temperature
            float WheelBearingTemperatureResistanceFactor = 0;

            // Assume the running resistance is impacted by wheel bearing temperature, ie gets higher as tmperature decreasses. This will only impact the A parameter as it is related to
            // bearing. Assume that resistance will increase by 30% as temperature drops below 0 DegC.
            // At -10 DegC it will be equal to the snowing value, as the temperature increases to 25 DegC, it will move towards the summer value
            // Assume a linear relationship between the two sets of points above and plot a straight line relationship.

            if (WheelBearingTemperatureDegC < -10) // Set to snowing (frozen value)
                WheelBearingTemperatureResistanceFactor = 1.3f;
            else if (WheelBearingTemperatureDegC > 25) // Set to normal temperature value
                WheelBearingTemperatureResistanceFactor = 1.0f;
            else // Set to variable value as bearing heats and cools
                WheelBearingTemperatureResistanceFactor = RunGrad * WheelBearingTemperatureDegC + RunIntersect;
            // If hot box has been initiated, then increase friction on the wagon significantly
            if (hotBoxActivated && activityElapsedDuration > hotBoxStartTime)
            {
                WheelBearingTemperatureResistanceFactor = 2.0f;
                StaticFrictionFactorN *= 2.0f;
            }
            // Calculation of resistance @ low speeds
            // Wind resistance is not included at low speeds, as it does not have a significant enough impact
            MergeSpeedFrictionN = DavisAN * WheelBearingTemperatureResistanceFactor + (MergeSpeedMpS) * (DavisBNSpM + (MergeSpeedMpS) * DavisCNSSpMM); // Calculate friction @ merge speed
            Friction0N = StandstillFrictionN * StaticFrictionFactorN; // Static friction x external resistance as this matches reference value
            FrictionBelowMergeSpeedN = ((1.0f - (AbsSpeedMpS / (MergeSpeedMpS))) * (Friction0N - MergeSpeedFrictionN)) + MergeSpeedFrictionN; // Calculate friction below merge speed - decreases linearly with speed
            FrictionForceN = FrictionBelowMergeSpeedN; // At low speed use this value
        }

        /// <summary>
        /// Update train base resistance with the Davis function.
        /// </summary>
        /// <remarks>
        /// For speeds slower than the "slow" speed.
        /// Based upon the article "Carriage and Wagon Tractive Resistance" by L. I. Sanders and printed "The Locomotive" of June 15, 1938.
        /// It is suggested that Rs (Starting Resistance) = Rin (Internal resistance of wagon - typically journal resistance) + Rt (Track resistance - due to weight of car depressing track).
        /// 
        /// Rt = 1120 x weight on axle x tan (angle of track depression) lbs/ton (UK). Typical depression angles for wagons would be 1 in 800, and locomotives 1 in 400.
        /// 
        /// This article suggests the following values for Rin Internal Starting Resistance:
        /// 
        ///                            Above Freezing
        ///    Journal (Oil) Bearing      17.5 lb/ton   (long (UK) ton)
        ///    Journal (Grease) Bearing   30 lb/ton     (long (UK) ton)
        ///    Roller Bearing             4.5 lb/ton    (long (UK) ton)
        /// 
        /// AREMA suggests the following figures for Starting Resistance:
        /// 
        ///                       Above Freezing   Below Freezing                       Above Freezing   Below Freezing
        ///    Journal Bearing      25 lb/ton        35 lb/ton   (short (US) ton)           29.75 lb/ton    41.65 lb/ton   (long (UK) ton)
        ///    Roller Bearing        5 lb/ton        15 lb/ton                              5.95 lb/ton     17.85 lb/ton
        ///    
        /// Davis suggests, "After a long stop in cold weather, the tractive effort at the instant of starting may reach 15 to 25 pounds per ton (us),
        /// diminishing rapidly to a minimum at 5 to 10 miles per hour".
        /// 
        /// AREMA suggests - "The starting resistance of roller bearings is essentially the same as when they are in motion". Hence the starting resistance should not be less 
        /// then the A value in the Davis formula.
        /// 
        /// This model uses the following criteria:
        /// i) Fixed journal resistance based upon UK figures (never is less then the A Davis value). This value is also varied with different wheel diameters. Reference wheel diameter = 37" (uk wheel).
        /// ii) Track resistance which varies depending upon the axle weight
        /// 
        /// </remarks>
        private void UpdateTrainBaseResistance_DavisLowSpeed()
        {
            // Determine the internal starting friction factor based upon the type of bearing

            float StartFrictionInternalFactorN = 0.0f;  // Internal starting friction
            float StartFrictionTrackN = 0.0f;
            float AxleLoadKg = 0;
            float ResistanceGrade = 0;
            float ReferenceWheelDiameterIn = 37.0f;
            float wheelvariationfactor = 1;

            // Find the variation in journal resistance due to wheel size. Steam locomotive don't have any variation at this time.
            if (WagonType == WagonType.Engine)
            {
                if (EngineType != EngineType.Steam)
                {
                    float wheeldiamM = 2.0f * DriverWheelRadiusM;
                    wheelvariationfactor = (float)(Size.Length.ToIn(wheeldiamM) / ReferenceWheelDiameterIn);
                }
            }
            else
            {
                float wheeldiamM = 2.0f * WheelRadiusM;
                wheelvariationfactor = (float)(Size.Length.ToIn(wheeldiamM) / ReferenceWheelDiameterIn);
            }

            if (IsRollerBearing)
            {
                // Determine the starting resistance due to wheel bearing temperature
                // Note reference values in lbf and US tons - converted to metric values as appropriate
                // At -10 DegC it will be equal to the snowing value, as the temperature increases to 25 DegC, it will move towards the summer value
                // Assume a linear relationship between the two sets of points above and plot a straight line relationship.
                float LowTemperature = -10.0f;
                float HighTemeprature = 25.0f;
                float LowTemperatureResistanceN = (float)Dynamics.Force.FromLbf(12.0f) * wheelvariationfactor;
                float HighTemperatureResistanceN = (float)Dynamics.Force.FromLbf(4.5f) * wheelvariationfactor;

                float LowGrad = (LowTemperatureResistanceN - HighTemperatureResistanceN) / (LowTemperature - HighTemeprature);
                float LowIntersect = LowTemperatureResistanceN - (LowGrad * LowTemperature);

                if (WheelBearingTemperatureDegC < -10)
                {
                    // Set to snowing (frozen value)
                    StartFrictionInternalFactorN = LowTemperatureResistanceN;  // Starting friction for car with standard roller bearings, snowing
                }
                else if (WheelBearingTemperatureDegC > 25)
                {
                    // Set to normal temperature value
                    StartFrictionInternalFactorN = HighTemperatureResistanceN;  // Starting friction for car with standard roller bearings, not snowing
                }
                else
                {
                    // Set to variable value as bearing heats and cools
                    StartFrictionInternalFactorN = LowGrad * WheelBearingTemperatureDegC + LowIntersect;
                }
            }
            else if (IsLowTorqueRollerBearing)
            {
                // Determine the starting resistance due to wheel bearing temperature
                // Note reference values in lbf and US tons - converted to metric values as appropriate
                // At -10 DegC it will be equal to the snowing value, as the temperature increases to 25 DegC, it will move towards the summer value
                // Assume a linear relationship between the two sets of points above and plot a straight line relationship.
                float LowTemperature = -10.0f;
                float HighTemeprature = 25.0f;
                float LowTemperatureResistanceN = (float)Dynamics.Force.FromLbf(7.5f) * wheelvariationfactor;
                float HighTemperatureResistanceN = (float)Dynamics.Force.FromLbf(2.5f) * wheelvariationfactor;

                float LowGrad = (LowTemperatureResistanceN - HighTemperatureResistanceN) / (LowTemperature - HighTemeprature);
                float LowIntersect = LowTemperatureResistanceN - (LowGrad * LowTemperature);

                if (WheelBearingTemperatureDegC < -10)
                {
                    // Set to snowing (frozen value)
                    StartFrictionInternalFactorN = LowTemperatureResistanceN;  // Starting friction for car with Low torque bearings, snowing
                }
                else if (WheelBearingTemperatureDegC > 25)
                {
                    // Set to normal temperature value
                    StartFrictionInternalFactorN = HighTemperatureResistanceN;  // Starting friction for car with Low troque bearings, not snowing
                }
                else
                {
                    // Set to variable value as bearing heats and cools
                    StartFrictionInternalFactorN = LowGrad * WheelBearingTemperatureDegC + LowIntersect;
                }
            }
            else if (IsGreaseFrictionBearing)
            {
                // Determine the starting resistance due to wheel bearing temperature
                // Note reference values in lbf and US tons - converted to metric values as appropriate
                // At -10 DegC it will be equal to the snowing value, as the temperature increases to 25 DegC, it will move towards the summer value
                // Assume a linear relationship between the two sets of points above and plot a straight line relationship.
                float LowTemperature = -10.0f;
                float HighTemeprature = 25.0f;
                float LowTemperatureResistanceN = (float)Dynamics.Force.FromLbf(45.0f) * wheelvariationfactor;
                float HighTemperatureResistanceN = (float)Dynamics.Force.FromLbf(30.0f) * wheelvariationfactor;

                float LowGrad = (LowTemperatureResistanceN - HighTemperatureResistanceN) / (LowTemperature - HighTemeprature);
                float LowIntersect = LowTemperatureResistanceN - (LowGrad * LowTemperature);

                if (WheelBearingTemperatureDegC < -10)
                {
                    // Set to snowing (frozen value)
                    StartFrictionInternalFactorN = LowTemperatureResistanceN;  // Starting friction car with Low torque bearings, snowing
                }
                else if (WheelBearingTemperatureDegC > 25)
                {
                    // Set to normal temperature value
                    StartFrictionInternalFactorN = HighTemperatureResistanceN;  // Starting friction for car with Low troque bearings, not snowing
                }
                else
                {
                    // Set to variable value as bearing heats and cools
                    StartFrictionInternalFactorN = LowGrad * WheelBearingTemperatureDegC + LowIntersect;
                }
            }
            else  // default to friction (solid - oil journal) bearing
            {

                // Determine the starting resistance due to wheel bearing temperature
                // Note reference values in lbf and US tons - converted to metric values as appropriate
                // At -10 DegC it will be equal to the snowing value, as the temperature increases to 25 DegC, it will move towards the summer value
                // Assume a linear relationship between the two sets of points above and plot a straight line relationship.
                float LowTemperature = -10.0f;
                float HighTemeprature = 25.0f;
                float LowTemperatureResistanceN = (float)Dynamics.Force.FromLbf(30.0f) * wheelvariationfactor;
                float HighTemperatureResistanceN = (float)Dynamics.Force.FromLbf(20.0f) * wheelvariationfactor;

                float LowGrad = (LowTemperatureResistanceN - HighTemperatureResistanceN) / (LowTemperature - HighTemeprature);
                float LowIntersect = LowTemperatureResistanceN - (LowGrad * LowTemperature);

                if (WheelBearingTemperatureDegC < -10)
                {
                    // Set to snowing (frozen value)
                    StartFrictionInternalFactorN = (float)Dynamics.Force.FromLbf(LowTemperatureResistanceN); // Starting friction for car with friction (journal) bearings - ton (US), snowing
                }
                else if (WheelBearingTemperatureDegC > 25)
                {
                    // Set to normal temperature value
                    StartFrictionInternalFactorN = (float)Dynamics.Force.FromLbf(HighTemperatureResistanceN); // Starting friction for car with friction (journal) bearings - ton (US), not snowing
                }
                else
                {
                    // Set to variable value as bearing heats and cools
                    StartFrictionInternalFactorN = LowGrad * WheelBearingTemperatureDegC + LowIntersect;
                }
            }

            // Determine the track starting resistance, based upon the axle loading of the wagon
            float LowLoadGrade = 800.0f;
            float HighLoadGrade = 400.0f;
            float LowLoadKg = (float)Mass.Kilogram.FromTonsUK(5.0f); // Low value is determined by average weight of passenger car with 6 axles = approx 30/6 = 5 tons uk
            float HighLoadKg = (float)Mass.Kilogram.FromTonsUK(26.0f); // High value is determined by average maximum axle loading for PRR K2 locomotive - used for deflection tests 

            float TrackGrad = (LowLoadGrade - HighLoadGrade) / (LowLoadKg - HighLoadKg);
            float TrackIntersect = LowLoadGrade - (TrackGrad * LowLoadKg);

            // Determine Axle loading of Car
            if (WagonType == WagonType.Engine && IsPlayerTrain && simulator.PlayerLocomotive is MSTSLocomotive locoParameters)
            {
                // This only takes into account the driven axles for 100% accuracy the non driven axles should also be considered
                AxleLoadKg = locoParameters.DrvWheelWeightKg / locoParameters.locoNumDrvAxles;
            }
            else
            {
                // Typically this loop should only be processed when it is a car of some description, and therefore it will use the wagon axles as it reference.
                if (wagonNumAxles > 0)
                {
                    AxleLoadKg = MassKG / wagonNumAxles;
                }
            }
            // Calculate the track gradient based on wagon axle loading
            ResistanceGrade = TrackGrad * AxleLoadKg + TrackIntersect;

            ResistanceGrade = MathHelper.Clamp(ResistanceGrade, 100, ResistanceGrade); // Clamp gradient so it doesn't go below 1 in 100

            const float trackfactor = 1120.0f;
            StartFrictionTrackN = (float)Dynamics.Force.FromLbf(trackfactor * (1 / ResistanceGrade) * Mass.Kilogram.ToTonsUK(AxleLoadKg));

            // Determine the running resistance due to wheel bearing temperature
            float WheelBearingTemperatureResistanceFactor = 0;

            // This section temperature compensates the running friction only - for comparion of merge point of running and starting friction.
            // Assume the running resistance is impacted by wheel bearing temperature, ie gets higher as tmperature decreasses. This will only impact the A parameter as it is related to
            // bearing. Assume that resistance will increase by 30% as temperature drops below 0 DegC.
            // At -10 DegC it will be equal to the snowing value, as the temperature increases to 25 DegC, it will move towards the summer value
            // Assume a linear relationship between the two sets of points above and plot a straight line relationship.
            float MotionLowTemperature = -10.0f;
            float MotionHighTemeprature = 25.0f;
            float MotionLowTemperatureResistance = 1.3f;
            float MotionHighTemperatureResistance = 1.0f;

            float RunGrad = (MotionLowTemperatureResistance - MotionHighTemperatureResistance) / (MotionLowTemperature - MotionHighTemeprature);
            float RunIntersect = MotionLowTemperatureResistance - (RunGrad * MotionLowTemperature);

            if (WheelBearingTemperatureDegC < -10)
            {
                // Set to snowing (frozen value)
                WheelBearingTemperatureResistanceFactor = 1.3f;
            }
            else if (WheelBearingTemperatureDegC > 25)
            {
                // Set to normal temperature value
                WheelBearingTemperatureResistanceFactor = 1.0f;
            }
            else
            {
                // Set to variable value as bearing heats and cools
                WheelBearingTemperatureResistanceFactor = RunGrad * WheelBearingTemperatureDegC + RunIntersect;
            }


            Friction0N = (float)((Mass.Kilogram.ToTonnes(MassKG) * StartFrictionInternalFactorN) + StartFrictionTrackN); // Static friction is journal or roller bearing friction x weight + track resistance. Mass value must be in tons uk to match reference used for starting resistance
            float Friction0DavisN = DavisAN * WheelBearingTemperatureResistanceFactor; // Calculate the starting firction if Davis formula was extended to zero
            // if the starting friction is less then the zero davis value, then set it higher then the zero davis value.
            if (Friction0N < Friction0DavisN)
            {
                Friction0N = Friction0DavisN * 1.2f;
            }

            // Calculation of resistance @ low speeds
            // Wind resistance is not included at low speeds, as it does not have a significant enough impact
            float speed5 = (float)Speed.MeterPerSecond.FromMpH(5); // 5 mph
            Friction5N = DavisAN * WheelBearingTemperatureResistanceFactor + speed5 * (DavisBNSpM + speed5 * DavisCNSSpMM); // Calculate friction @ 5 mph using "running" Davis values
            const float ForceDecayFactor = 2.5f; // Multiplier to determine what fraction of force to decay to - ie 2.5 x normal friction at 5mph
            FrictionLowSpeedN = ((1.0f - (AbsSpeedMpS / speed5)) * (Friction0N - Friction5N)) + Friction5N; // Calculate friction below 5mph - decreases linearly with speed
            FrictionForceN = FrictionLowSpeedN; // At low speed use this value
#if DEBUG_FRICTION
            Trace.TraceInformation("========================== Debug Stationary Friction in MSTSWagon.cs ==========================================");
            Trace.TraceInformation("Stationary - CarID {0} Bearing - Roller: {1}, Low: {2}, Grease: {3}, Friction(Oil) {4}", CarID, IsRollerBearing, IsLowTorqueRollerBearing, IsGreaseFrictionBearing, IsFrictionBearing);
            Trace.TraceInformation("Stationary - Mass {0}, Mass (UK-tons) {1}, AxleLoad {2}, BearingTemperature {3}", MassKG, Kg.ToTUK(MassKG), Kg.ToTUK(AxleLoadKg), WheelBearingTemperatureDegC);

            Trace.TraceInformation("Stationary - Weather Type (1 for Snow) {0}", (int)Simulator.WeatherType);
            Trace.TraceInformation("Stationary - StartFrictionInternal {0}", N.ToLbf(StartFrictionInternalFactorN));
            Trace.TraceInformation("Stationary - StartFrictionTrack: {0}, ResistanceGrade: {1}", N.ToLbf(StartFrictionTrackN), ResistanceGrade);
            Trace.TraceInformation("Stationary - Force0N {0}, FrictionDavis0N {1}, Force5N {2}, Speed {3}, TemperatureFactor {4}", N.ToLbf(Friction0N), N.ToLbf(Friction0DavisN), N.ToLbf(Friction5N), AbsSpeedMpS, WheelBearingTemperatureResistanceFactor);
            Trace.TraceInformation("=============================================================================================================");
#endif

            //// Starting friction is decayed using an exponential vs speed function (similar to Newtons law of cooling), an arbitary decay rate of decreasing resistance to 
            //// 2 x the Davis value at 5mph by the time the train reaches a speed of 
            //float FrictionDN = Friction5N * ForceDecayFactor;
            //float FrictionVariationN = (FrictionDN - Friction5N) / (Friction0N - Friction5N);

            //// Log function in ExpValue must never be less then zero, otherwise Na value will occur
            //if (FrictionVariationN <= 0)
            //{
            //    FrictionVariationN = 0.0001f;
            //}

            //float ExpValue = (float)Math.Log(FrictionVariationN) / speedDecay;
            //float DecayValue = AbsSpeedMpS * ExpValue;
            //FrictionLowSpeedN = Friction5N + (Friction0N - Friction5N) * (float)Math.Exp(DecayValue);
            //FrictionForceN = FrictionLowSpeedN; // At low speed use this value
            //FrictionForceN = MathHelper.Clamp(FrictionForceN, Friction5N, Friction0N); // Clamp FrictionForce to a value of resistance between 0 and 5 mph
        }




        /// <summary>
        /// Update train base resistance with the Davis function.
        /// </summary>
        /// <remarks>
        /// For speeds faster than the "slow" speed.
        /// </remarks>
        private void UpdateTrainBaseResistance_DavisHighSpeed()
        {
            // Determine the running resistance due to wheel bearing temperature
            float WheelBearingTemperatureResistanceFactor = 0;

            // Assume the running resistance is impacted by wheel bearing temperature, ie gets higher as tmperature decreasses. This will only impact the A parameter as it is related to
            // bearing. Assume that resisnce will increase by 30% as temperature drops below 0 DegC.
            // At -10 DegC it will be equal to the snowing value, as the temperature increases to 25 DegC, it will move towards the summer value
            // Assume a linear relationship between the two sets of points above and plot a straight line relationship.
            const float RunGrad = -0.0085714285714286f;
            const float RunIntersect = 1.2142857142857f;

            if (WheelBearingTemperatureDegC < -10)
            {
                // Set to snowing (frozen value)
                WheelBearingTemperatureResistanceFactor = 1.3f;
            }
            else if (WheelBearingTemperatureDegC > 25)
            {
                // Set to normal temperature value
                WheelBearingTemperatureResistanceFactor = 1.0f;
            }
            else
            {
                // Set to variable value as bearing heats and cools
                WheelBearingTemperatureResistanceFactor = RunGrad * WheelBearingTemperatureDegC + RunIntersect;
            }

            // If hot box has been initiated, then increase friction on the wagon significantly
            if (hotBoxActivated && activityElapsedDuration > hotBoxStartTime)
            {
                WheelBearingTemperatureResistanceFactor = 2.0f;
            }

            FrictionForceN = DavisAN * WheelBearingTemperatureResistanceFactor + AbsSpeedMpS * (DavisBNSpM + AbsSpeedMpS * DavisCNSSpMM); // for normal speed operation

            // if this car is a locomotive, but not the lead one then recalculate the resistance with lower value as drag will not be as high on trailing locomotives
            // Only the drag (C) factor changes if a trailing locomotive, so only running resistance, and not starting resistance needs to be corrected
            if (WagonType == WagonType.Engine && Train.LeadLocomotive != this)
            {
                FrictionForceN = DavisAN * WheelBearingTemperatureResistanceFactor + AbsSpeedMpS * (DavisBNSpM + AbsSpeedMpS * (TrailLocoResistanceFactor * DavisCNSSpMM));
            }

            // Test to identify whether a tender is attached to the leading engine, if not then the resistance should also be derated as for the locomotive
            bool IsLeadTender = false;
            if (WagonType == WagonType.Tender)
            {
                bool PrevCarLead = false;
                foreach (var car in Train.Cars)
                {
                    // If this car is a tender and the previous car is the lead locomotive then set the flag so that resistance will be reduced
                    if (car == this && PrevCarLead)
                    {
                        IsLeadTender = true;
                        break;  // If the tender has been identified then break out of the loop, otherwise keep going until whole train is done.
                    }
                    // Identify whether car is a lead locomotive or not. This is kept for when the next iteration (next car) is checked.
                    if (Train.LeadLocomotive == car)
                    {
                        PrevCarLead = true;
                    }
                    else
                    {
                        PrevCarLead = false;
                    }
                }

                // If tender is coupled to a trailing locomotive then reduce resistance
                if (!IsLeadTender)
                {
                    FrictionForceN = DavisAN * WheelBearingTemperatureResistanceFactor + AbsSpeedMpS * (DavisBNSpM + AbsSpeedMpS * (TrailLocoResistanceFactor * DavisCNSSpMM));
                }
            }
        }

        /// <summary>
        /// Updates the temperature of the wheel bearing on each wagon.
        /// </summary>
        /// <summary>
        /// Updates the temperature of the wheel bearing on each wagon.
        /// </summary>
        private void UpdateWheelBearingTemperature(double elapsedClockSeconds)
        {

            // Increased bearing temperature impacts the train physics model in two ways - it reduces the starting friction, and also a hot box failure, can result in failure of the train.
            // This is a "representative" model of bearing heat based upon the information described in the following publications- 
            // PRR Report (Bulletin #26) - Train Resistance and Tonnage Rating
            // Illinois Test Report (Bulletin #59) - The Effects of Cold Weather upon Train Resistance and Tonnage Rating
            // This information is for plain (friction) type bearings, and there are many variables that effect bearing heating and cooling, however it is considered a "close approximation" 
            // for the purposes it serves, ie to simulate resistance variation with temperature.
            // The model uses the Newton Law of Heating and cooling to model the time taken for temperature rise and fall - ie of the form T(t) = Ts + (T0 - Ts)exp(kt)

            // Keep track of Activity details if an activity, setup random wagon, and start time for hotbox
            if (simulator.ActivityRun != null && IsPlayerTrain)
            {
                if (activityElapsedDuration < hotBoxStartTime)
                {
                    activityElapsedDuration += elapsedClockSeconds;
                }

                // Determine whether car will be activated with a random hot box, only tested once at start of activity
                if (!hotBoxHasBeenInitialized) // If already initialised then skip
                {
                    // Activity randomizatrion needs to be active in Options menu, and HotBox will not be applied to a locomotive or tender.
                    if (simulator.Settings.ActRandomizationLevel > 0 && WagonType != WagonType.Engine && WagonType != WagonType.Tender)
                    {
                        var HotboxRandom = StaticRandom.Next(100) / simulator.Settings.ActRandomizationLevel;
                        float PerCentRandom = 0.66f; // Set so that random time is always in first 66% of activity duration
                        var RawHotBoxTimeRandomS = StaticRandom.Next((int)simulator.ActivityFile.Activity.Header.Duration.TotalSeconds);
                        if (!Train.HotBoxSetOnTrain) // only allow one hot box to be set per train 
                        {
                            if (HotboxRandom < 10)
                            {
                                hotBoxActivated = true;
                                Train.HotBoxSetOnTrain = true;
                                hotBoxStartTime = PerCentRandom * RawHotBoxTimeRandomS;

                                Trace.TraceInformation("Hotbox Bearing Activated on CarID {0}. Hotbox to start from {1:F1} minutes into activity", CarID, Time.Second.ToM(hotBoxStartTime));
                            }
                        }
                    }
                }

                hotBoxHasBeenInitialized = true; // Only allow to loop once at first pass
            }

            float BearingSpeedMaximumTemperatureDegC = 0;
            float MaximumNormalBearingTemperatureDegC = 90.0f;
            float MaximumHotBoxBearingTemperatureDegC = 120.0f;

            // K values calculated based on data in PRR report
            float CoolingKConst = -0.0003355569417321907f; // Time = 1380s, amb = -9.4. init = 56.7C, final = 32.2C
            float HeatingKConst = -0.000790635114477831f;  // Time = 3600s, amb = -9.4. init = 56.7C, final = 12.8C

            // Empty wagons take longer for hot boxes to heat up, this section looks at the load on a wagon, and assigns a K value to suit loading.
            // Guesstimated K values for Hotbox
            float HotBoxKConst = 0;
            float HotBoxKConstHighLoad = -0.002938026821980944f;  // Time = 600s, amb = -9.4. init = 120.0C, final = 12.8C
            float HotBoxKConstLowLoad = -0.001469013410990472f;  // Time = 1200s, amb = -9.4. init = 120.0C, final = 12.8C

            // Aligns to wagon weights used in friction calculations, ie < 10 tonsUS, and > 100 tonsUS either the low or high value used rspectively. In between these two values KConst scaled.
            if (MassKG < Mass.Kilogram.FromTonsUS(10)) // Lightly loaded wagon
            {
                HotBoxKConst = -0.001469013410990472f;
            }
            else if (MassKG > Mass.Kilogram.FromTonsUS(100)) // Heavily loaded wagon
            {
                HotBoxKConst = -0.002938026821980944f;
            }
            else
            {
                // Scaled between light and heavy loads
                var HotBoxScaleFactor = (MassKG - Mass.Kilogram.FromTonsUS(10)) / (Mass.Kilogram.FromTonsUS(100) - Mass.Kilogram.FromTonsUS(10));
                HotBoxKConst = (float)(HotBoxKConstLowLoad - (Math.Abs(HotBoxKConstHighLoad - HotBoxKConstLowLoad)) * HotBoxScaleFactor);
            }

            if (elapsedClockSeconds > 0) // Prevents zero values resetting temperature
            {

                // Keep track of wheel bearing temperature until activtaion time reached
                if (activityElapsedDuration < hotBoxStartTime)
                {
                    initialHotBoxRiseTemperatureDegS = WheelBearingTemperatureDegC;
                }

                // Calculate Hot box bearing temperature
                if (hotBoxActivated && activityElapsedDuration > hotBoxStartTime && AbsSpeedMpS > 7.0)
                {

                    if (!hotBoxSoundActivated)
                    {
                        SignalEvent(TrainEvent.HotBoxBearingOn);
                        hotBoxSoundActivated = true;
                    }

                    hotBoxTemperatureRiseTimeS += (float)elapsedClockSeconds;

                    // Calculate predicted bearing temperature based upon elapsed time
                    WheelBearingTemperatureDegC = MaximumHotBoxBearingTemperatureDegC + (initialHotBoxRiseTemperatureDegS - MaximumHotBoxBearingTemperatureDegC) * (float)(Math.Exp(HotBoxKConst * hotBoxTemperatureRiseTimeS));

                    // Reset temperature decline values in preparation for next cylce
                    wheelBearingTemperatureDeclineTimeS = 0;
                    initialWheelBearingDeclineTemperatureDegC = WheelBearingTemperatureDegC;

                }
                // Normal bearing temperature operation
                else if (AbsSpeedMpS > 7.0) // If train is moving calculate heating temperature
                {
                    // Calculate maximum bearing temperature based on current speed using approximated linear graph y = 0.25x + 55
                    const float MConst = 0.25f;
                    const float BConst = 55;
                    BearingSpeedMaximumTemperatureDegC = MConst * AbsSpeedMpS + BConst;

                    WheelBearingTemperatureRiseTimeS += (float)elapsedClockSeconds;

                    // Calculate predicted bearing temperature based upon elapsed time
                    WheelBearingTemperatureDegC = MaximumNormalBearingTemperatureDegC + (initialWheelBearingRiseTemperatureDegC - MaximumNormalBearingTemperatureDegC) * (float)(Math.Exp(HeatingKConst * WheelBearingTemperatureRiseTimeS));

                    // Cap bearing temperature depending upon speed
                    if (WheelBearingTemperatureDegC > BearingSpeedMaximumTemperatureDegC)
                    {
                        WheelBearingTemperatureDegC = BearingSpeedMaximumTemperatureDegC;
                    }

                    // Reset Decline values in preparation for next cylce
                    wheelBearingTemperatureDeclineTimeS = 0;
                    initialWheelBearingDeclineTemperatureDegC = WheelBearingTemperatureDegC;

                }
                // Calculate cooling temperature if train stops or slows down 
                else
                {
                    if (WheelBearingTemperatureDegC > CarOutsideTempC)
                    {
                        wheelBearingTemperatureDeclineTimeS += (float)elapsedClockSeconds;
                        WheelBearingTemperatureDegC = CarOutsideTempC + (initialWheelBearingDeclineTemperatureDegC - CarOutsideTempC) * (float)(Math.Exp(CoolingKConst * wheelBearingTemperatureDeclineTimeS));
                    }

                    WheelBearingTemperatureRiseTimeS = 0;
                    initialWheelBearingRiseTemperatureDegC = WheelBearingTemperatureDegC;

                    // Turn off Hotbox sounds
                    SignalEvent(TrainEvent.HotBoxBearingOff);
                    hotBoxSoundActivated = false;

                }

            }

            // Set warning messages for hot bearing and failed bearings
            if (WheelBearingTemperatureDegC > 115)
            {
                var hotboxfailuremessage = "CarID " + CarID + " has experienced a failure due to a hot wheel bearing";
                simulator.Confirmer.Message(ConfirmLevel.Warning, hotboxfailuremessage);
                wheelBearingFailed = true;
            }
            else if (WheelBearingTemperatureDegC > 100 && WheelBearingTemperatureDegC <= 115)
            {
                if (!wheelBearingHot)
                {
                    var hotboxmessage = "CarID " + CarID + " is experiencing a hot wheel bearing";
                    simulator.Confirmer.Message(ConfirmLevel.Warning, hotboxmessage);
                    wheelBearingHot = true;
                }
            }
            else
            {
                wheelBearingHot = false;
            }

            // Assume following limits for HUD - Normal operation: Cool: < 50, 50 - 90, Warm: 90 - 100, Hot: 100 - 115, Fail: > 115 - Set up text for HUD
            DisplayWheelBearingTemperatureStatus = WheelBearingTemperatureDegC > 115 ? "Fail" + "!!!" : WheelBearingTemperatureDegC > 100 && WheelBearingTemperatureDegC <= 115 ? "Hot" + "$$$"
                  : WheelBearingTemperatureDegC > 90 && WheelBearingTemperatureDegC <= 100 ? "Warm" + "???" : WheelBearingTemperatureDegC <= 50 ? "Cool" + "%%%" : "Norm" + "";

            if (WheelBearingTemperatureDegC > 90)
            {
                // Turn on smoke effects for bearing hot box
                BearingHotBoxSmokeDurationS = 1;
                BearingHotBoxSmokeVelocityMpS = 10.0f;
                BearingHotBoxSmokeVolumeM3pS = 1.5f;
            }
            else if (WheelBearingTemperatureDegC < 50)
            {
                // Turn off smoke effects for hot boxs
                BearingHotBoxSmokeDurationS = 0;
                BearingHotBoxSmokeVelocityMpS = 0;
                BearingHotBoxSmokeVolumeM3pS = 0;
            }

        }


        private void UpdateWindForce()
        {
            // Calculate compensation for  wind
            // There are two components due to wind - 
            // Drag, impact of wind on train, will increase resistance when head on, will decrease resistance when acting as a tailwind.
            // Lateral resistance - due to wheel flange being pushed against rail due to side wind.
            // Calculation based upon information provided in AREA 1942 Proceedings - https://archive.org/details/proceedingsofann431942amer - pg 56

            if (!TunnelFrontPositionBeyondStart.HasValue && AbsSpeedMpS > 2.2352) // Only calculate wind resistance if option selected in options menu, and not in a tunnel, and speed is sufficient for wind effects (>5mph)
            {

                // Wagon Direction
                float direction = (float)Math.Atan2(WorldPosition.XNAMatrix.M13, WorldPosition.XNAMatrix.M11);
                WagonDirectionDeg = MathHelper.ToDegrees((float)direction);

                // If car is flipped, then the car's direction will be reversed by 180 compared to the rest of the train, and thus for calculation purposes only, 
                // it is necessary to reverse the "assumed" direction of the car back again. This shouldn't impact the visual appearance of the car.
                if (Flipped)
                {
                    WagonDirectionDeg += 180.0f; // Reverse direction of car
                    if (WagonDirectionDeg > 360) // If this results in an angle greater then 360, then convert it back to an angle between 0 & 360.
                    {
                        WagonDirectionDeg -= 360;
                    }
                }

                // If a westerly direction (ie -ve) convert to an angle between 0 and 360
                if (WagonDirectionDeg < 0)
                    WagonDirectionDeg += 360;

                float TrainSpeedMpS = Math.Abs(SpeedMpS);

                // Find angle between wind and direction of train
                if (Train.PhysicsWindDirectionDeg > WagonDirectionDeg)
                    WagonResultantWindComponentDeg = Train.PhysicsWindDirectionDeg - WagonDirectionDeg;
                else if (WagonDirectionDeg > Train.PhysicsWindDirectionDeg)
                    WagonResultantWindComponentDeg = WagonDirectionDeg - Train.PhysicsWindDirectionDeg;
                else
                    WagonResultantWindComponentDeg = 0.0f;

                // Correct wind direction if it is greater then 360 deg, then correct to a value less then 360
                if (Math.Abs(WagonResultantWindComponentDeg) > 360)
                    WagonResultantWindComponentDeg = WagonResultantWindComponentDeg - 360.0f;

                // Wind angle should be kept between 0 and 180 the formulas do not cope with angles > 180. If angle > 180, denotes wind of "other" side of train
                if (WagonResultantWindComponentDeg > 180)
                    WagonResultantWindComponentDeg = 360 - WagonResultantWindComponentDeg;

                float ResultantWindComponentRad = MathHelper.ToRadians(WagonResultantWindComponentDeg);

                // Find the resultand wind vector for the combination of wind and train speed
                WagonWindResultantSpeedMpS = (float)Math.Sqrt(TrainSpeedMpS * TrainSpeedMpS + Train.PhysicsWindSpeedMpS * Train.PhysicsWindSpeedMpS + 2.0f * TrainSpeedMpS * Train.PhysicsWindSpeedMpS * (float)Math.Cos(ResultantWindComponentRad));

                // Calculate Drag Resistance
                // The drag resistance will be the difference between the STILL firction calculated using the standard Davies equation, 
                // and that produced using the wind resultant speed (combination of wind speed and train speed)
                float TempStillDragResistanceForceN = AbsSpeedMpS * AbsSpeedMpS * DavisCNSSpMM;
                float TempCombinedDragResistanceForceN = WagonWindResultantSpeedMpS * WagonWindResultantSpeedMpS * DavisCNSSpMM; // R3 of Davis formula taking into account wind
                float WindDragResistanceForceN = 0.0f;

                // Find the difference between the Still and combined resistances
                // This difference will be added or subtracted from the overall friction force depending upon the estimated wind direction.
                // Wind typically headon to train - increase resistance - +ve differential
                if (TempCombinedDragResistanceForceN > TempStillDragResistanceForceN)
                {
                    WindDragResistanceForceN = TempCombinedDragResistanceForceN - TempStillDragResistanceForceN;
                }
                else // wind typically following train - reduce resistance - -ve differential
                {
                    WindDragResistanceForceN = TempStillDragResistanceForceN - TempCombinedDragResistanceForceN;
                    WindDragResistanceForceN *= -1.0f;  // Convert to negative number to allow subtraction from ForceN
                }

                // Calculate Lateral Resistance

                // Calculate lateral resistance due to wind
                // Resistance is due to the wheel flanges being pushed further onto rails when a cross wind is experienced by a train
                float A = Train.PhysicsWindSpeedMpS / AbsSpeedMpS;
                float C = (float)Math.Sqrt((1 + (A * A) + 2.0f * A * Math.Cos(ResultantWindComponentRad)));
                float WindConstant = 8.25f;
                float TrainSpeedMpH = (float)Size.Length.ToMi(Frequency.Periodic.ToHours(AbsSpeedMpS));
                float WindSpeedMpH = (float)Size.Length.ToMi(Frequency.Periodic.ToHours(Train.PhysicsWindSpeedMpS));

                double WagonFrontalAreaFt2 = Size.Area.ToFt2(WagonFrontalAreaM2);

                lateralWindForce = (float)(Dynamics.Force.FromLbf(WindConstant * A * Math.Sin(ResultantWindComponentRad) * DavisDragConstant * WagonFrontalAreaFt2 * TrainSpeedMpH * TrainSpeedMpH * C));

                float LateralWindResistanceForceN = (float)(Dynamics.Force.FromLbf(WindConstant * A * Math.Sin(ResultantWindComponentRad) * DavisDragConstant * WagonFrontalAreaFt2 * TrainSpeedMpH * TrainSpeedMpH * C * Train.WagonCoefficientFriction));

                // if this car is a locomotive, but not the lead one then recalculate the resistance with lower C value as drag will not be as high on trailing locomotives
                if (WagonType == WagonType.Engine && Train.LeadLocomotive != this)
                {
                    LateralWindResistanceForceN *= TrailLocoResistanceFactor;
                }

                // Test to identify whether a tender is attached to the leading engine, if not then the resistance should also be derated as for the locomotive
                bool IsLeadTender = false;
                if (WagonType == WagonType.Tender)
                {
                    bool PrevCarLead = false;
                    foreach (var car in Train.Cars)
                    {
                        // If this car is a tender and the previous car is the lead locomotive then set the flag so that resistance will be reduced
                        if (car == this && PrevCarLead)
                        {
                            IsLeadTender = true;
                            break;  // If the tender has been identified then break out of the loop, otherwise keep going until whole train is done.
                        }
                        // Identify whether car is a lead locomotive or not. This is kept for when the next iteration (next car) is checked.
                        if (Train.LeadLocomotive == car)
                        {
                            PrevCarLead = true;
                        }
                        else
                        {
                            PrevCarLead = false;
                        }
                    }

                    // If tender is coupled to a trailing locomotive then reduce resistance
                    if (!IsLeadTender)
                    {
                        LateralWindResistanceForceN *= TrailLocoResistanceFactor;
                    }
                }
                WindForceN = LateralWindResistanceForceN + WindDragResistanceForceN;
            }
            else
            {
                WindForceN = 0.0f; // Set to zero if wind resistance is not to be calculated
            }

        }

        private void UpdateTenderLoad()
        // This section updates the weight and physics of the tender, and aux tender as load varies on it
        {

            if (FreightAnimations != null && FreightAnimations.ContinuousFreightAnimationsPresent) // make sure that a freight animation INCLUDE File has been defined, and it contains "continuous" animation data.
            {

                if (WagonType == WagonType.Tender)
                {
                    // Find the associated steam locomotive for this tender
                    if (TendersSteamLocomotive == null)
                        FindTendersSteamLocomotive();

                    // If no locomotive is found to be associated with this tender, then OR crashes, ie TendersSteamLocomotive is still null. 
                    // This message will provide the user with information to correct the problem
                    if (TendersSteamLocomotive == null)
                    {
                        Trace.TraceInformation("Tender @ position {0} does not have a locomotive associated with. Check that it is preceeded by a steam locomotive.", CarID);
                    }

                    MassKG = FreightAnimations.WagonEmptyWeight + TendersSteamLocomotive.TenderCoalMassKG + (float)Mass.Kilogram.FromLb((TendersSteamLocomotive.CurrentLocoTenderWaterVolumeUKG * WaterLBpUKG));
                    MassKG = MathHelper.Clamp(MassKG, LoadEmptyMassKg, LoadFullMassKg); // Clamp Mass to between the empty and full wagon values   

                    // Update wagon parameters sensitive to wagon mass change
                    // Calculate the difference ratio, ie how full the wagon is. This value allows the relevant value to be scaled from the empty mass to the full mass of the wagon
                    float TempTenderMassDiffRatio = (MassKG - LoadEmptyMassKg) / (LoadFullMassKg - LoadEmptyMassKg);
                    // Update brake parameters
                    MaxBrakeForceN = ((LoadFullMaxBrakeForceN - LoadEmptyMaxBrakeForceN) * TempTenderMassDiffRatio) + LoadEmptyMaxBrakeForceN;
                    MaxHandbrakeForceN = ((LoadFullMaxHandbrakeForceN - LoadEmptyMaxHandbrakeForceN) * TempTenderMassDiffRatio) + LoadEmptyMaxHandbrakeForceN;
                    // Update friction related parameters
                    DavisAN = ((LoadFullORTSDavis_A - LoadEmptyORTSDavis_A) * TempTenderMassDiffRatio) + LoadEmptyORTSDavis_A;
                    DavisBNSpM = ((LoadFullORTSDavis_B - LoadEmptyORTSDavis_B) * TempTenderMassDiffRatio) + LoadEmptyORTSDavis_B;
                    DavisCNSSpMM = ((LoadFullORTSDavis_C - LoadEmptyORTSDavis_C) * TempTenderMassDiffRatio) + LoadEmptyORTSDavis_C;

                    if (LoadEmptyDavisDragConstant > LoadFullDavisDragConstant) // Due to wind turbulence empty drag might be higher then loaded drag, and therefore both scenarios need to be covered.
                    {
                        DavisDragConstant = LoadEmptyDavisDragConstant - ((LoadEmptyDavisDragConstant - LoadFullDavisDragConstant) * TempMassDiffRatio);
                    }
                    else
                    {
                        DavisDragConstant = ((LoadFullDavisDragConstant - LoadEmptyDavisDragConstant) * TempMassDiffRatio) + LoadEmptyDavisDragConstant;
                    }

                    WagonFrontalAreaM2 = ((LoadFullWagonFrontalAreaM2 - LoadEmptyWagonFrontalAreaM2) * TempMassDiffRatio) + LoadEmptyWagonFrontalAreaM2;

                    // Update CoG related parameters
                    centreOfGravityM.Y = ((LoadFullCentreOfGravityM_Y - LoadEmptyCentreOfGravityM_Y) * TempTenderMassDiffRatio) + LoadEmptyCentreOfGravityM_Y;
                }
                else if (AuxWagonType == AuxWagonType.AuxiliaryTender)
                {
                    // Find the associated steam locomotive for this tender
                    if (AuxTendersSteamLocomotive == null)
                        FindAuxTendersSteamLocomotive();

                    MassKG = FreightAnimations.WagonEmptyWeight + (float)Mass.Kilogram.FromLb((AuxTendersSteamLocomotive.CurrentAuxTenderWaterVolumeUKG * WaterLBpUKG));
                    MassKG = MathHelper.Clamp(MassKG, LoadEmptyMassKg, LoadFullMassKg); // Clamp Mass to between the empty and full wagon values   

                    // Update wagon parameters sensitive to wagon mass change
                    // Calculate the difference ratio, ie how full the wagon is. This value allows the relevant value to be scaled from the empty mass to the full mass of the wagon
                    float TempTenderMassDiffRatio = (MassKG - LoadEmptyMassKg) / (LoadFullMassKg - LoadEmptyMassKg);
                    // Update brake parameters
                    MaxBrakeForceN = ((LoadFullMaxBrakeForceN - LoadEmptyMaxBrakeForceN) * TempTenderMassDiffRatio) + LoadEmptyMaxBrakeForceN;
                    MaxHandbrakeForceN = ((LoadFullMaxHandbrakeForceN - LoadEmptyMaxHandbrakeForceN) * TempTenderMassDiffRatio) + LoadEmptyMaxHandbrakeForceN;
                    // Update friction related parameters
                    DavisAN = ((LoadFullORTSDavis_A - LoadEmptyORTSDavis_A) * TempTenderMassDiffRatio) + LoadEmptyORTSDavis_A;
                    DavisBNSpM = ((LoadFullORTSDavis_B - LoadEmptyORTSDavis_B) * TempTenderMassDiffRatio) + LoadEmptyORTSDavis_B;
                    DavisCNSSpMM = ((LoadFullORTSDavis_C - LoadEmptyORTSDavis_C) * TempTenderMassDiffRatio) + LoadEmptyORTSDavis_C;

                    if (LoadEmptyDavisDragConstant > LoadFullDavisDragConstant) // Due to wind turbulence empty drag might be higher then loaded drag, and therefore both scenarios need to be covered.
                    {
                        DavisDragConstant = LoadEmptyDavisDragConstant - ((LoadEmptyDavisDragConstant - LoadFullDavisDragConstant) * TempMassDiffRatio);
                    }
                    else
                    {
                        DavisDragConstant = ((LoadFullDavisDragConstant - LoadEmptyDavisDragConstant) * TempMassDiffRatio) + LoadEmptyDavisDragConstant;
                    }

                    WagonFrontalAreaM2 = ((LoadFullWagonFrontalAreaM2 - LoadEmptyWagonFrontalAreaM2) * TempMassDiffRatio) + LoadEmptyWagonFrontalAreaM2;

                    // Update CoG related parameters
                    centreOfGravityM.Y = ((LoadFullCentreOfGravityM_Y - LoadEmptyCentreOfGravityM_Y) * TempTenderMassDiffRatio) + LoadEmptyCentreOfGravityM_Y;
                }
            }
        }

        private void UpdateSpecialEffects(double elapsedClockSeconds)
        // This section updates the special effects
        {

            var LocomotiveParameters = simulator.PlayerLocomotive as MSTSLocomotive;

            if (LocomotiveParameters != null)
            {

                // if this is a heating steam boiler car then adjust steam pressure
                // Don't turn steam heat on until pressure valve has been opened, water and fuel capacity also needs to be present, steam heating shouldn't already be present on diesel or steam locomotive
                if (IsPlayerTrain && WagonSpecialType == WagonSpecialType.HeatingBoiler && !LocomotiveParameters.IsSteamHeatFitted && LocomotiveParameters.SteamHeatController.CurrentValue > 0.05 && currentCarSteamHeatBoilerWaterCapacityL > 0 && currentSteamHeatBoilerFuelCapacityL > 0 && !steamHeatBoilerLockedOut)
                {
                    //   LocomotiveParameters.CurrentSteamHeatPressurePSI = LocomotiveParameters.SteamHeatController.CurrentValue * 100;
                    LocomotiveParameters.CurrentSteamHeatPressurePSI = 60.0f;
                    Train.CarSteamHeatOn = true; // turn on steam effects on wagons
                }
                else if (IsPlayerTrain && WagonSpecialType == WagonSpecialType.HeatingBoiler)
                {
                    LocomotiveParameters.CurrentSteamHeatPressurePSI = 0.0f;
                    Train.CarSteamHeatOn = false; // turn off steam effects on wagons
                    steamHeatingBoilerOn = false;
                }

                // Turn on Heating steam boiler
                if (Train.CarSteamHeatOn && LocomotiveParameters.SteamHeatController.CurrentValue > 0)
                {
                    // Turn heating boiler on 
                    HeatingSteamBoilerDurationS = 1.0f * LocomotiveParameters.SteamHeatController.CurrentValue;
                    HeatingSteamBoilerVolumeM3pS = 1.5f * LocomotiveParameters.SteamHeatController.CurrentValue;
                }
                else
                {
                    // Turn heating boiler off 
                    HeatingSteamBoilerVolumeM3pS = 0.0f;
                    HeatingSteamBoilerDurationS = 0.0f;
                }
                // Update Heating hose steam leaks Information
                if (Train.CarSteamHeatOn && carSteamHeatMainPipeSteamPressurePSI > 0)
                {
                    // Turn wagon steam leaks on 
                    HeatingHoseParticleDurationS = 0.75f;
                    HeatingHoseSteamVelocityMpS = 15.0f;
                    HeatingHoseSteamVolumeM3pS = (float)(4.0 * steamHoseLeakRateRandom);
                }
                else
                {
                    // Turn wagon steam leaks off 
                    HeatingHoseParticleDurationS = 0.0f;
                    HeatingHoseSteamVelocityMpS = 0.0f;
                    HeatingHoseSteamVolumeM3pS = 0.0f;
                }

                // Update Heating main pipe steam trap leaks Information
                if (Train.CarSteamHeatOn && carSteamHeatMainPipeSteamPressurePSI > 0)
                {
                    // Turn wagon steam leaks on 
                    HeatingMainPipeSteamTrapDurationS = 0.75f;
                    HeatingMainPipeSteamTrapVelocityMpS = 15.0f;
                    HeatingMainPipeSteamTrapVolumeM3pS = 8.0f;
                }
                else
                {
                    // Turn wagon steam leaks off 
                    HeatingMainPipeSteamTrapDurationS = 0.0f;
                    HeatingMainPipeSteamTrapVelocityMpS = 0.0f;
                    HeatingMainPipeSteamTrapVolumeM3pS = 0.0f;
                }

                // Update Heating compartment steam trap leaks Information
                if (steamHeatingCompartmentSteamTrapOn)
                {
                    // Turn wagon steam leaks on 
                    HeatingCompartmentSteamTrapParticleDurationS = 0.75f;
                    HeatingCompartmentSteamTrapVelocityMpS = 15.0f;
                    HeatingCompartmentSteamTrapVolumeM3pS = 4.0f;
                }
                else
                {
                    // Turn wagon steam leaks off 
                    HeatingCompartmentSteamTrapParticleDurationS = 0.0f;
                    HeatingCompartmentSteamTrapVelocityMpS = 0.0f;
                    HeatingCompartmentSteamTrapVolumeM3pS = 0.0f;
                }

                // Update Water Scoop Spray Information when scoop is down and filling from trough

                bool ProcessWaterEffects = false; // Initialise test flag to see whether this wagon will have water sccop effects active

                if (WagonType == WagonType.Tender || WagonType == WagonType.Engine)
                {

                    if (WagonType == WagonType.Tender)
                    {
                        // Find the associated steam locomotive for this tender
                        if (TendersSteamLocomotive == null)
                            FindTendersSteamLocomotive();

                        if (TendersSteamLocomotive == LocomotiveParameters && TendersSteamLocomotive.HasWaterScoop)
                        {
                            ProcessWaterEffects = true; // Set flag if this tender is attached to player locomotive
                        }

                    }
                    else if (simulator.PlayerLocomotive == this && LocomotiveParameters.HasWaterScoop)
                    {
                        ProcessWaterEffects = true; // Allow water effects to be processed
                    }
                    else
                    {
                        ProcessWaterEffects = false; // Default off
                    }

                    // Tender Water overflow control
                    if (LocomotiveParameters.RefillingFromTrough && ProcessWaterEffects)
                    {

                        float SpeedRatio = (float)(AbsSpeedMpS / Speed.MeterPerSecond.FromMpH(100)); // Ratio to reduce water disturbance with speed - an arbitary value of 100mph has been chosen as the reference

                        // Turn tender water overflow on if water level is greater then 100% nominally and minimum water scoop speed is reached
                        if (LocomotiveParameters.TenderWaterLevelFraction >= 0.9999 && AbsSpeedMpS > LocomotiveParameters.WaterScoopMinSpeedMpS)
                        {
                            float InitialTenderWaterOverflowParticleDurationS = 1.25f;
                            float InitialTenderWaterOverflowVelocityMpS = 50.0f;
                            float InitialTenderWaterOverflowVolumeM3pS = 10.0f;

                            // Turn tender water overflow on - changes due to speed of train
                            TenderWaterOverflowParticleDurationS = InitialTenderWaterOverflowParticleDurationS * SpeedRatio;
                            TenderWaterOverflowVelocityMpS = InitialTenderWaterOverflowVelocityMpS * SpeedRatio;
                            TenderWaterOverflowVolumeM3pS = InitialTenderWaterOverflowVolumeM3pS * SpeedRatio;
                        }
                    }
                    else
                    {
                        // Turn tender water overflow off 
                        TenderWaterOverflowParticleDurationS = 0.0f;
                        TenderWaterOverflowVelocityMpS = 0.0f;
                        TenderWaterOverflowVolumeM3pS = 0.0f;
                    }

                    // Water scoop spray effects control - always on when scoop over trough, regardless of whether above minimum speed or not
                    if (ProcessWaterEffects && LocomotiveParameters.IsWaterScoopDown && IsOverTrough() && AbsSpeedMpS > 0.1)
                    {
                        float SpeedRatio = (float)(AbsSpeedMpS / Speed.MeterPerSecond.FromMpH(100)); // Ratio to reduce water disturbance with speed - an arbitary value of 100mph has been chosen as the reference

                        float InitialWaterScoopParticleDurationS = 1.25f;
                        float InitialWaterScoopWaterVelocityMpS = 50.0f;
                        float InitialWaterScoopWaterVolumeM3pS = 10.0f;

                        // Turn water scoop spray effects on
                        if (AbsSpeedMpS <= Speed.MeterPerSecond.FromMpH(10))
                        {
                            double SprayDecay = (Speed.MeterPerSecond.FromMpH(25) / Speed.MeterPerSecond.FromMpH(100)) / Speed.MeterPerSecond.FromMpH(10); // Linear decay factor - based upon previous level starts @ a value @ 25mph
                            SpeedRatio = (float)((SprayDecay * AbsSpeedMpS) / Speed.MeterPerSecond.FromMpH(100)); // Decrease the water scoop spray effect to minimum level of visibility
                            WaterScoopParticleDurationS = InitialWaterScoopParticleDurationS * SpeedRatio;
                            WaterScoopWaterVelocityMpS = InitialWaterScoopWaterVelocityMpS * SpeedRatio;
                            WaterScoopWaterVolumeM3pS = InitialWaterScoopWaterVolumeM3pS * SpeedRatio;

                        }
                        // Below 25mph effect does not vary, above 25mph effect varies according to speed
                        else if (AbsSpeedMpS < Speed.MeterPerSecond.FromMpH(25) && AbsSpeedMpS > Speed.MeterPerSecond.FromMpH(10))
                        {
                            SpeedRatio = (float)(Speed.MeterPerSecond.FromMpH(25) / Speed.MeterPerSecond.FromMpH(100)); // Hold the water scoop spray effect to a minimum level of visibility
                            WaterScoopParticleDurationS = InitialWaterScoopParticleDurationS * SpeedRatio;
                            WaterScoopWaterVelocityMpS = InitialWaterScoopWaterVelocityMpS * SpeedRatio;
                            WaterScoopWaterVolumeM3pS = InitialWaterScoopWaterVolumeM3pS * SpeedRatio;
                        }
                        else
                        {
                            // Allow water sccop spray effect to vary with speed
                            WaterScoopParticleDurationS = InitialWaterScoopParticleDurationS * SpeedRatio;
                            WaterScoopWaterVelocityMpS = InitialWaterScoopWaterVelocityMpS * SpeedRatio;
                            WaterScoopWaterVolumeM3pS = InitialWaterScoopWaterVolumeM3pS * SpeedRatio;
                        }
                    }
                    else
                    {
                        // Turn water scoop spray effects off 
                        WaterScoopParticleDurationS = 0.0f;
                        WaterScoopWaterVelocityMpS = 0.0f;
                        WaterScoopWaterVolumeM3pS = 0.0f;

                    }

                    // Update Steam Brake leaks Information
                    if (LocomotiveParameters.EngineBrakeFitted && LocomotiveParameters.SteamEngineBrakeFitted && (WagonType == WagonType.Tender || WagonType == WagonType.Engine))
                    {
                        // Find the steam leakage rate based upon valve opening and current boiler pressure
                        float SteamBrakeLeakRate = LocomotiveParameters.EngineBrakeController.CurrentValue * (LocomotiveParameters.BoilerPressurePSI / LocomotiveParameters.MaxBoilerPressurePSI);

                        if (simulator.PlayerLocomotive == this && LocomotiveParameters.EngineBrakeController.CurrentValue > 0)
                        {
                            // Turn steam brake leaks on 
                            SteamBrakeLeaksDurationS = 0.75f;
                            SteamBrakeLeaksVelocityMpS = 15.0f;
                            SteamBrakeLeaksVolumeM3pS = 4.0f * SteamBrakeLeakRate;
                        }
                        else
                        {
                            // Turn steam brake leaks off 
                            SteamBrakeLeaksDurationS = 0.0f;
                            SteamBrakeLeaksVelocityMpS = 0.0f;
                            SteamBrakeLeaksVolumeM3pS = 0.0f;
                        }

                        if (WagonType == WagonType.Tender)
                        {
                            // Find the associated steam locomotive for this tender
                            if (TendersSteamLocomotive == null)
                                FindTendersSteamLocomotive();

                            // Turn steam brake effect on or off
                            if (TendersSteamLocomotive == LocomotiveParameters && LocomotiveParameters.EngineBrakeController.CurrentValue > 0)
                            {
                                // Turn steam brake leaks on 
                                SteamBrakeLeaksDurationS = 0.75f;
                                SteamBrakeLeaksVelocityMpS = 15.0f;
                                SteamBrakeLeaksVolumeM3pS = 4.0f * SteamBrakeLeakRate;
                            }
                            else
                            {
                                // Turn steam brake leaks off 
                                SteamBrakeLeaksDurationS = 0.0f;
                                SteamBrakeLeaksVelocityMpS = 0.0f;
                                SteamBrakeLeaksVolumeM3pS = 0.0f;
                            }
                        }
                    }
                }
            }

            WagonSmokeDurationS = InitialWagonSmokeDurationS;
            WagonSmokeVolumeM3pS = InitialWagonSmokeVolumeM3pS;
        }

        public override void SignalEvent(TrainEvent evt)
        {
            switch (evt)
            {
                // Compatibility layer for MSTS events
                case TrainEvent.Pantograph1Up:
                    SignalEvent(PowerSupplyEvent.RaisePantograph, 1);
                    break;
                case TrainEvent.Pantograph1Down:
                    SignalEvent(PowerSupplyEvent.LowerPantograph, 1);
                    break;
                case TrainEvent.Pantograph2Up:
                    SignalEvent(PowerSupplyEvent.RaisePantograph, 2);
                    break;
                case TrainEvent.Pantograph2Down:
                    SignalEvent(PowerSupplyEvent.LowerPantograph, 2);
                    break;
                case TrainEvent.Pantograph3Up:
                    SignalEvent(PowerSupplyEvent.RaisePantograph, 3);
                    break;
                case TrainEvent.Pantograph3Down:
                    SignalEvent(PowerSupplyEvent.LowerPantograph, 3);
                    break;
                case TrainEvent.Pantograph4Up:
                    SignalEvent(PowerSupplyEvent.RaisePantograph, 4);
                    break;
                case TrainEvent.Pantograph4Down:
                    SignalEvent(PowerSupplyEvent.LowerPantograph, 4);
                    break;
                case TrainEvent.DoorOpenLeft:
                    Doors[Flipped ? DoorSide.Right : DoorSide.Left].SetDoor(true);
                    break;
                case TrainEvent.DoorOpenRight:
                    Doors[Flipped ? DoorSide.Left : DoorSide.Right].SetDoor(true);
                    break;
                case TrainEvent.DoorCloseLeft:
                    Doors[Flipped ? DoorSide.Right : DoorSide.Left].SetDoor(false);
                    break;
                case TrainEvent.DoorCloseRight:
                    Doors[Flipped ? DoorSide.Left : DoorSide.Right].SetDoor(false);
                    break;
            }

            TriggerWagonSoundEvent(evt, null);
            base.SignalEvent(evt);
        }

        public override void SignalEvent(PowerSupplyEvent evt)
        {
            if (simulator.PlayerLocomotive == this || RemoteControlGroup != RemoteControlGroup.Unconnected)
            {
                switch (evt)
                {
                    case PowerSupplyEvent.RaisePantograph:
                    case PowerSupplyEvent.LowerPantograph:
                        if (Pantographs != null)
                        {
                            Pantographs.HandleEvent(evt);
                            SignalEvent(TrainEvent.PantographToggle);
                        }
                        break;
                }
            }

            base.SignalEvent(evt);
        }

        public override void SignalEvent(PowerSupplyEvent evt, int id)
        {
            if (simulator.PlayerLocomotive == this || RemoteControlGroup != RemoteControlGroup.Unconnected)
            {
                switch (evt)
                {
                    case PowerSupplyEvent.RaisePantograph:
                    case PowerSupplyEvent.LowerPantograph:
                        if (Pantographs != null)
                        {
                            Pantographs.HandleEvent(evt, id);
                            SignalEvent(TrainEvent.PantographToggle);
                        }
                        break;
                }
            }

            base.SignalEvent(evt, id);
        }

        public void ToggleMirrors()
        {
            MirrorOpen = !MirrorOpen;
            if (MirrorOpen)
                SignalEvent(TrainEvent.MirrorOpen); // hook for sound trigger
            else
                SignalEvent(TrainEvent.MirrorClose);
            if (simulator.PlayerLocomotive == this)
                simulator.Confirmer.Confirm(CabControl.Mirror, MirrorOpen ? CabSetting.On : CabSetting.Off);
        }

        public void FindControlActiveLocomotive()
        {
            // Find the active locomotive associated with a control car
            if (Train == null || Train.Cars == null || Train.Cars.Count == 1)
            {
                ControlActiveLocomotive = null;
                return;
            }
            var controlIndex = 0;
            var activeIndex = 0;
            bool controlCar = false;
            bool activeLocomotive = false;

            // Check to see if this car is an active locomotive, if so then set linkage to relevant control car.
            // Note this only checks the "closest" locomotive to the control car. Hence it could be "fooled" if there is another locomotive besides the two DMU locomotives.

            for (var i = 0; i < Train.Cars.Count; i++)
            {

                if (activeIndex == 0 && Train.Cars[i].EngineType == EngineType.Diesel)
                {
                    activeIndex = i;
                    activeLocomotive = true;
                }

                if (controlIndex == 0 && Train.Cars[i].EngineType == EngineType.Control)
                {
                    controlIndex = i;
                    controlCar = true;
                }

                // As soon as the control and active locomotive have been identified, then stop loop.
                if (activeLocomotive && controlCar)
                {
                    ControlActiveLocomotive = Train.Cars[activeIndex] as MSTSDieselLocomotive;
                    return;
                }
            }
        }

        public void FindTendersSteamLocomotive()
        {
            // Find the steam locomotive associated with this wagon tender, this allows parameters processed in the steam loocmotive module to be used elsewhere
            if (Train == null || Train.Cars == null || Train.Cars.Count == 1)
            {
                TendersSteamLocomotive = null;
                return;
            }

            bool HasTender = false;
            var tenderIndex = 0;

            // Check to see if this car is defined as a tender, if so then set linkage to relevant steam locomotive. If no tender, then set linkage to null
            for (var i = 0; i < Train.Cars.Count; i++)
            {
                if (Train.Cars[i] == this && Train.Cars[i].WagonType == WagonType.Tender)
                {
                    tenderIndex = i;
                    HasTender = true;
                }
            }
            if (HasTender && tenderIndex > 0 && Train.Cars[tenderIndex - 1] is MSTSSteamLocomotive)
                TendersSteamLocomotive = Train.Cars[tenderIndex - 1] as MSTSSteamLocomotive;
            else if (HasTender && tenderIndex < Train.Cars.Count - 1 && Train.Cars[tenderIndex + 1] is MSTSSteamLocomotive)
                TendersSteamLocomotive = Train.Cars[tenderIndex + 1] as MSTSSteamLocomotive;
            else
                TendersSteamLocomotive = null;
        }

        /// <summary>
        /// This function checks each steam locomotive to see if it has a tender attached.
        /// </summary>
        public void ConfirmSteamLocomotiveTender()
        {

            // Check each steam locomotive to see if it has a tender attached.
            if (this is MSTSSteamLocomotive)
            {

                if (Train == null || Train.Cars == null)
                {
                    SteamLocomotiveTender = null;
                    return;
                }
                else if (Train.Cars.Count == 1) // If car count is equal to 1, then there must be no tender attached
                {
                    SteamLocomotiveTender = Train.Cars[0] as MSTSSteamLocomotive;
                    SteamLocomotiveTender.HasTenderCoupled = false;
                }

                var tenderIndex = 0;
                for (var i = 0; i < Train.Cars.Count; i++) // test each car to find the where the steam locomotive is in the consist
                {
                    if (Train.Cars[i] == this)  // If this car is a Steam locomotive the set tender index
                        tenderIndex = i;
                }

                if (tenderIndex < Train.Cars.Count - 1 && Train.Cars[tenderIndex + 1].WagonType == WagonType.Tender) // Assuming the tender is behind the locomotive
                {
                    SteamLocomotiveTender = Train.Cars[tenderIndex] as MSTSSteamLocomotive;
                    SteamLocomotiveTender.HasTenderCoupled = true;
                }

                else if (tenderIndex > 0 && Train.Cars[tenderIndex - 1].WagonType == WagonType.Tender) // Assuming the tender is "in front" of the locomotive, ie it is running in reverse
                {
                    // TO BE CHECKED - What happens if multiple locomotives are coupled together in reverse?
                    SteamLocomotiveTender = Train.Cars[tenderIndex] as MSTSSteamLocomotive;
                    SteamLocomotiveTender.HasTenderCoupled = true;
                }
                else // Assuming that locomotive is a tank locomotive, and no tender is coupled
                {
                    SteamLocomotiveTender = Train.Cars[tenderIndex] as MSTSSteamLocomotive;
                    SteamLocomotiveTender.HasTenderCoupled = false;
                }
            }
        }

        /// <summary>
        /// This function finds the steam locomotive associated with this wagon aux tender, this allows parameters processed in the steam loocmotive module to be used elsewhere.
        /// </summary>
        public void FindAuxTendersSteamLocomotive()
        {
            if (Train == null || Train.Cars == null || Train.Cars.Count == 1)
            {
                AuxTendersSteamLocomotive = null;
                return;
            }
            bool AuxTenderFound = false;
            var tenderIndex = 0;
            for (var i = 0; i < Train.Cars.Count; i++)
            {
                if (Train.Cars[i] == this)
                    tenderIndex = i;
            }

            // If a "normal" tender is not connected try checking if locomotive is directly connected to the auxiliary tender - this will be the case for a tank locomotive.
            if (tenderIndex > 0 && Train.Cars[tenderIndex - 1] is MSTSSteamLocomotive)
            {
                AuxTendersSteamLocomotive = Train.Cars[tenderIndex - 1] as MSTSSteamLocomotive;
                AuxTenderFound = true;
            }

            if (tenderIndex < Train.Cars.Count - 1 && Train.Cars[tenderIndex + 1] is MSTSSteamLocomotive)
            {
                AuxTendersSteamLocomotive = Train.Cars[tenderIndex + 1] as MSTSSteamLocomotive;
                AuxTenderFound = true;
            }

            // If a "normal" tender is connected then the steam locomotive will be two cars away.

            if (!AuxTenderFound)
            {

                if (tenderIndex > 0 && Train.Cars[tenderIndex - 2] is MSTSSteamLocomotive)
                {
                    AuxTendersSteamLocomotive = Train.Cars[tenderIndex - 2] as MSTSSteamLocomotive;
                }

                if (tenderIndex < Train.Cars.Count - 2 && Train.Cars[tenderIndex + 2] is MSTSSteamLocomotive)
                {
                    AuxTendersSteamLocomotive = Train.Cars[tenderIndex + 2] as MSTSSteamLocomotive;
                }
            }
        }

        public event EventHandler<SoundSourceEventArgs> OnCarSound;

        internal void TriggerWagonSoundEvent(TrainEvent carEvent, object owner)
        {
            OnCarSound?.Invoke(this, new SoundSourceEventArgs(carEvent, owner));
        }

        #region Coupling and Advanced Couplers
        // This determines which coupler to use from WAG file, typically it will be the first one as by convention the rear coupler is always read first.
        internal Coupler Coupler => (Flipped && couplers[TrainCarLocation.Front] != null) ? couplers[TrainCarLocation.Front] : couplers[TrainCarLocation.Rear]; // defaults to the rear coupler (typically the first read)

        public override float GetCouplerZeroLengthM()
        {
            if (IsPlayerTrain && simulator.Settings.UseAdvancedAdhesion && !simulator.Settings.SimpleControlPhysics && avancedCoupler)
            {
                // Ensure zerolength doesn't go higher then 0.5
                return Math.Min(Coupler?.R0X ?? base.GetCouplerZeroLengthM(), 0.5f);
            }
            else
            {
                return Coupler?.R0X ?? base.GetCouplerZeroLengthM();
            }
        }

        public override float GetSimpleCouplerStiffnessNpM()
        {
            return Coupler != null && Coupler.R0X == 0 ? 7 * (Coupler.Stiffness1NpM + Coupler.Stiffness2NpM) : base.GetSimpleCouplerStiffnessNpM();
        }

        public override float GetCouplerStiffness1NpM()
        {
            return Coupler != null ? Coupler.Rigid ? 10 * Coupler.Stiffness1NpM : Coupler.Stiffness1NpM : base.GetCouplerStiffness1NpM();
        }

        public override float GetCouplerStiffness2NpM()
        {
            return Coupler != null ? Coupler.Rigid ? 10 * Coupler.Stiffness1NpM : Coupler.Stiffness2NpM : base.GetCouplerStiffness2NpM();
        }

        public override float GetCouplerSlackAM()
        {
            return Coupler?.CouplerSlackAM ?? base.GetCouplerSlackAM();
        }

        public override float GetCouplerSlackBM()
        {
            return Coupler?.CouplerSlackBM ?? base.GetCouplerSlackBM();
        }

        public override bool GetCouplerRigidIndication()
        {
            return Coupler?.Rigid ?? base.GetCouplerRigidIndication(); // Return whether coupler Rigid or Flexible
        }

        public override bool GetAdvancedCouplerFlag()
        {
            return Coupler != null ? avancedCoupler : base.GetAdvancedCouplerFlag();
        }

        public override float GetMaximumSimpleCouplerSlack1M()  // This limits the maximum amount of slack, and typically will be equal to y - x of R0 statement
        {
            return Coupler != null ? Coupler.Rigid ? 0.0001f : Coupler.R0Diff : base.GetMaximumSimpleCouplerSlack1M();
        }

        public override float GetMaximumSimpleCouplerSlack2M() // This limits the slack due to draft forces (?) and should be marginally greater then GetMaximumCouplerSlack1M
        {
            return (Coupler != null && Coupler.Rigid) ? 0.0002f : base.GetMaximumSimpleCouplerSlack2M(); //  GetMaximumCouplerSlack2M > GetMaximumCouplerSlack1M
        }

        // Advanced coupler parameters
        public override float GetCouplerTensionStiffness1N()
        {
            return Coupler != null ? Coupler.Rigid ? 10 * Coupler.TensionStiffness1N : Coupler.TensionStiffness1N : base.GetCouplerTensionStiffness1N();
        }

        public override float GetCouplerTensionStiffness2N()
        {
            return Coupler != null ? Coupler.Rigid ? 10 * Coupler.TensionStiffness2N : Coupler.TensionStiffness2N : base.GetCouplerTensionStiffness2N();
        }

        public override float GetCouplerCompressionStiffness1N()
        {
            return Coupler != null ? Coupler.Rigid ? 10 * Coupler.CompressionStiffness1N : Coupler.CompressionStiffness1N : base.GetCouplerCompressionStiffness1N();
        }

        public override float GetCouplerCompressionStiffness2N()
        {
            return Coupler != null ? Coupler.Rigid ? 10 * Coupler.CompressionStiffness2N : Coupler.CompressionStiffness2N : base.GetCouplerCompressionStiffness2N();
        }

        public override float GetCouplerTensionSlackAM()
        {
            return Coupler?.CouplerTensionSlackAM ?? base.GetCouplerTensionSlackAM();
        }

        public override float GetCouplerTensionSlackBM()
        {
            return Coupler?.CouplerTensionSlackBM ?? base.GetCouplerTensionSlackBM();
        }

        public override float GetCouplerCompressionSlackAM()
        {
            return Coupler?.CouplerCompressionSlackAM ?? base.GetCouplerCompressionSlackAM();
        }

        public override float GetCouplerCompressionSlackBM()
        {
            return Coupler?.CouplerCompressionSlackBM ?? base.GetCouplerCompressionSlackBM();
        }

        public override float GetMaximumCouplerTensionSlack1M()  // This limits the maximum amount of slack, and typically will be equal to y - x of R0 statement
        {
            if (Coupler == null)
                return base.GetMaximumCouplerTensionSlack1M();

            if (Coupler.TensionR0Y == 0)
            {
                Coupler.TensionR0Y = GetCouplerTensionR0Y(); // if no value present, default value to tension value
            }
            return Coupler.Rigid ? 0.00001f : Coupler.TensionR0Y;
        }

        public override float GetMaximumCouplerTensionSlack2M()
        {
            // Zone 2 limit - ie Zone 1 + 2
            return Coupler != null ? Coupler.Rigid ? 0.0001f : Coupler.TensionR0Y + GetCouplerTensionSlackAM() : base.GetMaximumCouplerTensionSlack2M();
        }

        public override float GetMaximumCouplerTensionSlack3M() // This limits the slack due to draft forces (?) and should be marginally greater then GetMaximumCouplerSlack2M
        {
            if (Coupler == null)
            {
                return base.GetMaximumCouplerTensionSlack3M();
            }
            float Coupler2MTemporary = GetCouplerTensionSlackBM();
            if (Coupler2MTemporary == 0)
            {
                Coupler2MTemporary = 0.1f; // make sure that SlackBM is always > 0
            }
            return Coupler.Rigid ? 0.0002f : Coupler.TensionR0Y + GetCouplerTensionSlackAM() + Coupler2MTemporary; //  GetMaximumCouplerSlack3M > GetMaximumCouplerSlack2M
        }

        public override float GetMaximumCouplerCompressionSlack1M()  // This limits the maximum amount of slack, and typically will be equal to y - x of R0 statement
        {
            if (Coupler == null)
                return base.GetMaximumCouplerCompressionSlack1M();
            if (Coupler.CompressionR0Y == 0)
            {
                Coupler.CompressionR0Y = GetCouplerCompressionR0Y(); // if no value present, default value to compression value
            }
            return Coupler.Rigid ? 0.00005f : Coupler.CompressionR0Y;
        }

        public override float GetMaximumCouplerCompressionSlack2M()  // This limits the maximum amount of slack, and typically will be equal to y - x of R0 statement
        {
            return Coupler != null ? Coupler.Rigid ? 0.0001f : Coupler.CompressionR0Y + GetCouplerCompressionSlackAM() : base.GetMaximumCouplerCompressionSlack2M();
        }

        public override float GetMaximumCouplerCompressionSlack3M() // This limits the slack due to draft forces (?) and should be marginally greater then GetMaximumCouplerSlack1M
        {
            if (Coupler == null)
            {
                return base.GetMaximumCouplerCompressionSlack3M();
            }
            float Coupler2MTemporary = GetCouplerCompressionSlackBM();
            if (Coupler2MTemporary == 0)
            {
                Coupler2MTemporary = 0.1f; // make sure that SlackBM is always > 0
            }
            return Coupler.Rigid ? 0.0002f : Coupler.CompressionR0Y + GetCouplerCompressionSlackAM() + Coupler2MTemporary; //  GetMaximumCouplerSlack3M > GetMaximumCouplerSlack2M
        }

        public override float GetCouplerBreak1N()
        {
            return Coupler?.Break1N ?? base.GetCouplerBreak1N();
        }

        public override float GetCouplerBreak2N()
        {
            return Coupler?.Break2N ?? base.GetCouplerBreak2N();
        }

        public override float GetCouplerTensionR0Y()
        {
            return Coupler?.TensionR0Y ?? base.GetCouplerTensionR0Y();
        }

        public override float GetCouplerCompressionR0Y()
        {
            return Coupler?.CompressionR0Y ?? base.GetCouplerCompressionR0Y();
        }

        // TODO: This code appears to be being called by ReverseCars (in Trains.cs). 
        // Reverse cars moves the couplers along by one car, however this may be encountering a null coupler at end of train. 
        // Thus all coupler parameters need to be tested for null coupler and default values inserted (To be confirmed)
        public override void CopyCoupler(TrainCar source)
        {
            // To be checked
            base.CopyCoupler(source);
            Coupler coupler = new Coupler(source, GetCouplerZeroLengthM());
            // ADvanced coupler parameters
            avancedCoupler = source.GetAdvancedCouplerFlag();

            if (couplers[TrainCarLocation.Rear] == null)
                couplers[TrainCarLocation.Rear] = coupler;
            couplers[TrainCarLocation.Front] = null;
        }
        #endregion

        public void SetWagonHandbrake(bool ToState)
        {
            MSTSBrakeSystem.HandbrakePercent = ToState ? 100 : 0;
        }

        /// <summary>
        /// Returns the fraction of load already in wagon.
        /// </summary>
        /// <param name="pickupType">Pickup type</param>
        /// <returns>0.0 to 1.0. If type is unknown, returns 0.0</returns>
        public override float GetFilledFraction(PickupType pickupType)
        {
            return FreightAnimations?.LoadedOne?.LoadPerCent / 100 ?? 0;
        }

        /// <summary>
        /// Returns the Brake shoe coefficient.
        /// </summary>

        public override float GetUserBrakeShoeFrictionFactor()
        {
            float frictionfraction;
            if (BrakeShoeFrictionFactor == null)
            {
                frictionfraction = 0.0f;
            }
            else
            {
                frictionfraction = (float)BrakeShoeFrictionFactor[Speed.MeterPerSecond.ToKpH(AbsSpeedMpS)];
            }

            return frictionfraction;
        }

        /// <summary>
        /// Returns the Brake shoe coefficient at zero speed.
        /// </summary>

        public override float GetZeroUserBrakeShoeFrictionFactor()
        {
            float frictionfraction;
            if (BrakeShoeFrictionFactor == null)
            {
                frictionfraction = 0.0f;
            }
            else
            {
                frictionfraction = (float)BrakeShoeFrictionFactor[0.0f];
            }

            return frictionfraction;
        }



        /// <summary>
        /// Starts a continuous increase in controlled value.
        /// </summary>
        public void StartRefillingOrUnloading(PickupObject matchPickup, IntakePoint intakePoint, float fraction, bool unload)
        {
            var controller = WeightLoadController;
            if (controller == null)
            {
                simulator.Confirmer.Message(ConfirmLevel.Error, Simulator.Catalog.GetString("Incompatible data"));
                return;
            }
            controller.SetValue(fraction);
            controller.CommandStartTime = simulator.ClockTime;  // for Replay to use 

            if (FreightAnimations.LoadedOne == null)
            {
                FreightAnimations.FreightType = matchPickup.PickupType;
                if (intakePoint.LinkedFreightAnim is FreightAnimationContinuous freightAnimationContinuous)
                    FreightAnimations.LoadedOne = freightAnimationContinuous;
            }
            if (!unload)
            {
                controller.SetStepSize(matchPickup.Capacity.FeedRateKGpS / MSTSNotchController.StandardBoost / FreightAnimations.LoadedOne.FreightWeightWhenFull);
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Starting refill"));
                controller.StartIncrease(controller.MaximumValue);
            }
            else
            {
                controller.SetStepSize(-matchPickup.Capacity.FeedRateKGpS / MSTSNotchController.StandardBoost / FreightAnimations.LoadedOne.FreightWeightWhenFull);
                WaitForAnimationReady = true;
                UnloadingPartsOpen = true;
                if (FreightAnimations.UnloadingStartDelay > 0)
                    simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Preparing for unload"));
            }

        }

        /// <summary>
        /// Starts loading or unloading of a discrete load.
        /// </summary>
        /// <param name="type">Pickup point</param>
        public void StartLoadingOrUnloading(PickupObject matchPickup, IntakePoint intakePoint, bool unload)
        {
            /*           var controller = WeightLoadController;
                       if (controller == null)
                       {
                           Simulator.Confirmer.Message(ConfirmLevel.Error, Simulator.Catalog.GetString("Incompatible data"));
                           return;
                       }
                       controller.CommandStartTime = Simulator.ClockTime;  // for Replay to use */

            FreightAnimations.FreightType = matchPickup.PickupType;

            var containerStation = simulator.ContainerManager.ContainerStations.Where(item => item.Key == matchPickup.TrackItemIds.TrackDbItems[0]).Select(item => item.Value).First();
            if (containerStation.Status != ContainerStationStatus.Idle)
            {
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Container station busy with preceding mission"));
                return;
            }
            if (!unload)
            {
                if (containerStation.Containers.Count == 0)
                {
                    simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("No containers to load"));
                    return;
                }
                //               var container = containerStation.Containers.Last();
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Starting load"));
                // immediate load at the moment
                //                FreightAnimations.DiscreteLoadedOne.Container = container;
                containerStation.PrepareForLoad((FreightAnimationDiscrete)intakePoint.LinkedFreightAnim);
                //               FreightAnimations.DiscreteLoadedOne.Loaded = true;
            }
            else
            {
                if (containerStation.Containers.Count >= containerStation.MaxStackedContainers * containerStation.StackLocationsCount)
                {
                    simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Container station full, can't unload"));
                    return;
                }
                WaitForAnimationReady = true;
                UnloadingPartsOpen = true;
                if (FreightAnimations.UnloadingStartDelay > 0)
                    simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Preparing for unload"));
                // immediate unload at the moment
                // switch from freightanimation to container
                containerStation.PrepareForUnload((FreightAnimationDiscrete)intakePoint.LinkedFreightAnim);
            }

        }

        protected internal override void UpdateRemotePosition(double elapsedClockSeconds, float speed, float targetSpeed)
        {
            base.UpdateRemotePosition(elapsedClockSeconds, speed, targetSpeed);
            WheelSpeedMpS = speed;
        }
    }

    /// <summary>
    /// An IntakePoint object is created for any engine or wagon having a 
    /// IntakePoint block in its ENG/WAG file. 
    /// Called from within the MSTSWagon class.
    /// </summary>
    public class IntakePoint
    {
        public float OffsetM { get; internal set; }   // distance forward? from the centre of the vehicle as defined by LengthM/2.
        public float WidthM { get; } = 10f;   // of the filling point. Is the maximum positioning error allowed equal to this or half this value? 
        public PickupType Type { get; }          // 'freightgrain', 'freightcoal', 'freightgravel', 'freightsand', 'fuelcoal', 'fuelwater', 'fueldiesel', 'fuelwood', freightgeneral, freightlivestock, specialmail, container
        public float? DistanceFromFrontOfTrainM { get; }
        public FreightAnimation LinkedFreightAnim { get; internal set; }

        public IntakePoint(float offset, float width, PickupType pickupType)
        {
            OffsetM = offset;
            WidthM = width;
            Type = pickupType;
        }

        public IntakePoint(STFReader stf)
        {
            stf.MustMatch("(");
            OffsetM = stf.ReadFloat(STFReader.Units.None, 0f);
            WidthM = stf.ReadFloat(STFReader.Units.None, 10f);
            if (EnumExtension.GetValue(stf.ReadString(), out PickupType type))
                Type = type;
            stf.SkipRestOfBlock();
        }

        // for copy
        public IntakePoint(IntakePoint source)
        {
            ArgumentNullException.ThrowIfNull(source);
            OffsetM = source.OffsetM;
            WidthM = source.WidthM;
            Type = source.Type;

        }
        public bool Validity(bool onlyUnload, PickupObject pickup, ContainerManager containerManager,
            FreightAnimations freightAnimations, out ContainerHandlingStation containerStation)
        {
            var validity = false;
            containerStation = null;
            var load = LinkedFreightAnim as FreightAnimationDiscrete;
            // discrete freight wagon animation
            if (load == null)
                return validity;
            else
            {
                containerStation = containerManager.ContainerStations.Where(item => item.Key == pickup.TrackItemIds.TrackDbItems[0]).Select(item => item.Value).First();
                if (containerStation.Containers.Count == 0 && !onlyUnload)
                    return validity;
            }
            if (load.Container != null && !onlyUnload)
                return validity;
            else if (load.Container == null && onlyUnload)
                return validity;
            if (freightAnimations.DoubleStacker)
            {
                if (onlyUnload)
                    for (var i = freightAnimations.Animations.Count - 1; i >= 0; i--)
                    {
                        if (freightAnimations.Animations[i] is FreightAnimationDiscrete discreteAnimation)
                            if (discreteAnimation.LoadPosition == LoadPosition.Above && load != discreteAnimation)
                                return validity;
                            else
                                break;
                    }
            }
            if (!onlyUnload)
            {
                if (containerStation.Containers.Count == 0)
                    return validity;
                foreach (var stackLocation in containerStation.StackLocations)
                {
                    if (stackLocation.Containers?.Count > 0)
                    {
                        if (freightAnimations.Validity(load.Wagon, stackLocation.Containers[stackLocation.Containers.Count - 1],
                            load.LoadPosition, load.Offset, load.LoadingAreaLength, out Vector3 offset))
                            return true;
                    }
                }
                return validity;
            }
            validity = onlyUnload ? containerStation.CheckForEligibleStackPosition(load.Container) : true;
            return validity;
        }
    }

    /// <summary>
    /// Utility class to avoid loading the wag file multiple times
    /// </summary>
    public static class CarManager
    {
        public static Dictionary<string, MSTSWagon> LoadedCars { get; } = new Dictionary<string, MSTSWagon>();
    }

    public readonly struct ParticleEmitterData
    {
        public readonly Vector3 XNALocation;
        public readonly Vector3 XNADirection;
        public readonly float NozzleWidth;

        public ParticleEmitterData(STFReader stf)
        {
            stf.MustMatch("(");
            XNALocation.X = stf.ReadFloat(STFReader.Units.Distance, 0.0f);
            XNALocation.Y = stf.ReadFloat(STFReader.Units.Distance, 0.0f);
            XNALocation.Z = -stf.ReadFloat(STFReader.Units.Distance, 0.0f);
            XNADirection.X = stf.ReadFloat(STFReader.Units.Distance, 0.0f);
            XNADirection.Y = stf.ReadFloat(STFReader.Units.Distance, 0.0f);
            XNADirection.Z = -stf.ReadFloat(STFReader.Units.Distance, 0.0f);
            XNADirection.Normalize();
            NozzleWidth = stf.ReadFloat(STFReader.Units.Distance, 0.0f);
            stf.SkipRestOfBlock();
        }
    }

    public class ShapeAnimation
    {
        public string ShapeFileName { get; }

        public float Width { get; }
        public float Height { get; }
        public float Length { get; }

        public ShapeAnimation(STFReader stf)
        {
            ArgumentNullException.ThrowIfNull(stf);
            stf.MustMatch("(");
            ShapeFileName = stf.ReadString();
            Width = stf.ReadFloat(STFReader.Units.Distance, null);
            Height = stf.ReadFloat(STFReader.Units.Distance, null);
            Length = stf.ReadFloat(STFReader.Units.Distance, null);
            stf.SkipRestOfBlock();
        }
    }
}
