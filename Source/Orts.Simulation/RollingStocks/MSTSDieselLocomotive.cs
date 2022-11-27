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

/* DIESEL LOCOMOTIVE CLASSES
 * 
 * The Locomotive is represented by two classes:
 *  MSTSDieselLocomotiveSimulator - defines the behaviour, ie physics, motion, power generated etc
 *  MSTSDieselLocomotiveViewer - defines the appearance in a 3D viewer.  The viewer doesn't
 *  get attached to the car until it comes into viewing range.
 *  
 * Both these classes derive from corresponding classes for a basic locomotive
 *  LocomotiveSimulator - provides for movement, basic controls etc
 *  LocomotiveViewer - provides basic animation for running gear, wipers, etc
 * 
 */

//#define ALLOW_ORTS_SPECIFIC_ENG_PARAMETERS

using System;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Text;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.DebugInfo;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;
using Orts.Simulation.RollingStocks.SubSystems.Controllers;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using Orts.Simulation.RollingStocks.SubSystems.PowerTransmissions;

namespace Orts.Simulation.RollingStocks
{
    ///////////////////////////////////////////////////
    ///   SIMULATION BEHAVIOUR
    ///////////////////////////////////////////////////

    /// <summary>
    /// Adds physics and control for a diesel locomotive
    /// </summary>
    public class MSTSDieselLocomotive : MSTSLocomotive
    {

        private readonly DistributedPowerStatus distributedPowerStatus;

        public ScriptedDieselPowerSupply DieselPowerSupply => PowerSupply as ScriptedDieselPowerSupply;

        public float IdleRPM;
        public float MaxRPM;
        public float GovernorRPM;
        public float MaxRPMChangeRate;
        public float PercentChangePerSec = .2f;
        public float InitialExhaust;
        public float InitialMagnitude;
        public float MaxExhaust = 2.8f;
        public float MaxMagnitude = 1.5f;
        public float EngineRPMderivation;
        private float EngineRPMold;
        private float EngineRPMRatio; // used to compute Variable1 and Variable2
        public float MaximumDieselEnginePowerW;

        public MSTSNotchController FuelController = new MSTSNotchController(0, 1, 0.0025f);
        public float MaxDieselLevelL = 5000.0f;
        public float DieselLevelL
        {
            get { return FuelController.CurrentValue * MaxDieselLevelL; }
            set { FuelController.CurrentValue = value / MaxDieselLevelL; }
        }

        public float DieselUsedPerHourAtMaxPowerL = 1.0f;
        public float DieselUsedPerHourAtIdleL = 1.0f;
        public float DieselFlowLps;
        public float DieselWeightKgpL = 0.8508f; //per liter
        private float InitialMassKg = 100000.0f;

        public float LocomotiveMaxRailOutputPowerW;

        internal int currentGearIndexRestore = -1;
        internal int currentnextGearRestore = -1;
        internal bool gearSaved;
        public int dieselEngineRestoreState;

        public float EngineRPM;
        public SmoothedData ExhaustParticles = new SmoothedData(1);
        public SmoothedData ExhaustMagnitude = new SmoothedData(1);
        public SmoothedData ExhaustColorR = new SmoothedData(1);
        public SmoothedData ExhaustColorG = new SmoothedData(1);
        public SmoothedData ExhaustColorB = new SmoothedData(1);

        public float DieselOilPressurePSI;
        public float DieselMinOilPressurePSI;
        public float DieselMaxOilPressurePSI;
        public float DieselTemperatureDeg = 40f;
        public float DieselMaxTemperatureDeg;
        public DieselEngine.Cooling DieselEngineCooling = DieselEngine.Cooling.Proportional;

        public DieselTransmissionType DieselTransmissionType { get; private set; }

        private float CalculatedMaxContinuousForceN;

        // diesel performance reporting
        public float DieselPerformanceTimeS; // Records the time since starting movement

        public DieselEngines DieselEngines { get; private set; }

        /// <summary>
        /// Used to accumulate a quantity that is not lost because of lack of precision when added to the Fuel level
        /// </summary>        
        private float partialFuelConsumption;

        private const float GearBoxControllerBoost = 1; // Slow boost to enable easy single gear up/down commands

        public DetailInfoBase DistributedPowerInformation => distributedPowerStatus;

        public MSTSDieselLocomotive(string wagFile)
            : base(wagFile)
        {
            DieselEngines = new DieselEngines(this);
            PowerSupply = new ScriptedDieselPowerSupply(this);
            RefillImmediately();
            distributedPowerStatus = new DistributedPowerStatus(this);
        }

        /// <summary>
        /// Parse the wag file parameters required for the simulator and viewer classes
        /// </summary>
        public override void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(ortspowerondelay":
                case "engine(ortsauxpowerondelay":
                case "engine(ortspowersupply":
                case "engine(ortstractioncutoffrelay":
                case "engine(ortstractioncutoffrelayclosingdelay":
                case "engine(ortsbattery(mode":
                case "engine(ortsbattery(delay":
                case "engine(ortsmasterkey(mode":
                case "engine(ortsmasterkey(delayoff":
                case "engine(ortsmasterkey(headlightcontrol":
                case "engine(ortselectrictrainsupply(mode":
                case "engine(ortselectrictrainsupply(dieselengineminrpm":
                    LocomotivePowerSupply.Parse(lowercasetoken, stf);
                    break;
                case "engine(dieselengineidlerpm":
                    IdleRPM = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "engine(dieselenginemaxrpm":
                    MaxRPM = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "engine(ortsdieselenginegovernorrpm":
                    GovernorRPM = stf.ReadFloatBlock(STFReader.Units.None, 0);
                    break;
                case "engine(dieselenginemaxrpmchangerate":
                    MaxRPMChangeRate = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "engine(ortsdieselenginemaxpower":
                    MaximumDieselEnginePowerW = stf.ReadFloatBlock(STFReader.Units.Power, null);
                    break;

                case "engine(effects(dieselspecialeffects":
                    ParseEffects(lowercasetoken, stf);
                    break;
                case "engine(dieselsmokeeffectinitialsmokerate":
                    InitialExhaust = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "engine(dieselsmokeeffectinitialmagnitude":
                    InitialMagnitude = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "engine(dieselsmokeeffectmaxsmokerate":
                    MaxExhaust = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "engine(dieselsmokeeffectmaxmagnitude":
                    MaxMagnitude = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;

                case "engine(ortsdieseltransmissiontype":
                    stf.MustMatch("(");
                    string transmissionType = stf.ReadString();
                    if (!EnumExtension.GetValue(transmissionType, out DieselTransmissionType dieselTransmissionType))
                        STFException.TraceWarning(stf, "Skipped unknown diesel transmission type " + transmissionType);
                    DieselTransmissionType = dieselTransmissionType;
                    break;
                case "engine(ortsdieselengines":
                case "engine(gearboxnumberofgears":
                case "engine(gearboxdirectdrivegear":
                case "engine(ortsmainclutchtype":
                case "engine(ortsgearboxtype":
                case "engine(gearboxoperation":
                case "engine(gearboxenginebraking":
                case "engine(gearboxmaxspeedforgears":
                case "engine(gearboxmaxtractiveforceforgears":
                case "engine(ortsgearboxtractiveforceatspeed":
                case "engine(gearboxoverspeedpercentageforfailure":
                case "engine(gearboxbackloadforce":
                case "engine(gearboxcoastingforce":
                case "engine(gearboxupgearproportion":
                case "engine(gearboxdowngearproportion":
                case "engine(ortsgearboxfreewheel":
                    DieselEngines.Parse(lowercasetoken, stf);
                    break;
                case "engine(maxdiesellevel":
                    MaxDieselLevelL = stf.ReadFloatBlock(STFReader.Units.Volume, null);
                    break;
                case "engine(dieselusedperhouratmaxpower":
                    DieselUsedPerHourAtMaxPowerL = stf.ReadFloatBlock(STFReader.Units.Volume, null);
                    break;
                case "engine(dieselusedperhouratidle":
                    DieselUsedPerHourAtIdleL = stf.ReadFloatBlock(STFReader.Units.Volume, null);
                    break;
                case "engine(maxoilpressure":
                    DieselMaxOilPressurePSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, 120f);
                    break;
                case "engine(ortsminoilpressure":
                    DieselMinOilPressurePSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, 40f);
                    break;
                case "engine(maxtemperature":
                    DieselMaxTemperatureDeg = stf.ReadFloatBlock(STFReader.Units.Temperature, 0);
                    break;
                case "engine(ortsdieselcooling":
                    DieselEngineCooling = (DieselEngine.Cooling)stf.ReadInt((int)DieselEngine.Cooling.Proportional);
                    break;
                default:
                    base.Parse(lowercasetoken, stf);
                    break;
            }

            if (IdleRPM != 0 && MaxRPM != 0 && MaxRPMChangeRate != 0)
            {
                PercentChangePerSec = MaxRPMChangeRate / (MaxRPM - IdleRPM);
                EngineRPM = IdleRPM;
            }
        }

        public override void LoadFromWagFile(string wagFilePath)
        {
            base.LoadFromWagFile(wagFilePath);

            if (simulator.Settings.VerboseConfigurationMessages)  // Display locomotivve name for verbose error messaging
            {
                Trace.TraceInformation("\n\n ================================================= {0} =================================================", LocomotiveName);
            }

            NormalizeParams();

            // Check to see if Speed of Max Tractive Force has been set - use ORTS value as first priority, if not use MSTS, last resort use an arbitary value.
            if (SpeedOfMaxContinuousForceMpS == 0)
            {
                if (MSTSSpeedOfMaxContinuousForceMpS != 0)
                {
                    SpeedOfMaxContinuousForceMpS = MSTSSpeedOfMaxContinuousForceMpS; // Use MSTS value if present

                    if (simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Speed Of Max Continuous Force: set to default value {0}", FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));

                }
                else if (MaxPowerW != 0 && MaxContinuousForceN != 0)
                {
                    SpeedOfMaxContinuousForceMpS = MaxPowerW / MaxContinuousForceN;

                    if (simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Speed Of Max Continuous Force: set to 'calculated' value {0}", FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));

                }
                else
                {
                    SpeedOfMaxContinuousForceMpS = 10.0f; // If not defined then set at an "arbitary" value of 22mph

                    if (simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Speed Of Max Continuous Force: set to 'arbitary' value {0}", FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));

                }
            }

            // Create a diesel engine block if none exits, typically for a MSTS or BASIC configuration
            if (DieselEngines.Count == 0)
            {
                DieselEngines.Add(new DieselEngine(this));

                DieselEngines[0].InitFromMSTS();
                DieselEngines[0].Initialize();
            }


            // Check initialization of power values for diesel engines
            for (int i = 0; i < DieselEngines.Count; i++)
            {
                DieselEngines[i].InitDieselRailPowers(this);

            }

            InitialMassKg = MassKG;

            // If traction force curves not set (BASIC configuration) then check that power values are set, otherwise locomotive will not move.
            if (TractiveForceCurves == null && LocomotiveMaxRailOutputPowerW == 0)
            {
                if (MaxPowerW != 0)
                {

                    LocomotiveMaxRailOutputPowerW = MaxPowerW;  // Set to default power value

                    if (simulator.Settings.VerboseConfigurationMessages)
                    {
                        Trace.TraceInformation("MaxRailOutputPower (BASIC Config): set to default value = {0}", FormatStrings.FormatPower(LocomotiveMaxRailOutputPowerW, simulator.MetricUnits, false, false));
                    }
                }
                else
                {
                    LocomotiveMaxRailOutputPowerW = 2500000.0f; // If no default value then set to arbitary value

                    if (simulator.Settings.VerboseConfigurationMessages)
                    {
                        Trace.TraceInformation("MaxRailOutputPower (BASIC Config): set at arbitary value = {0}", FormatStrings.FormatPower(LocomotiveMaxRailOutputPowerW, simulator.MetricUnits, false, false));
                    }

                }


                if (MaximumDieselEnginePowerW == 0)
                {
                    MaximumDieselEnginePowerW = LocomotiveMaxRailOutputPowerW;  // If no value set in ENG file, then set the Prime Mover power to same as RailOutputPower (typically the MaxPower value)

                    if (simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Maximum Diesel Engine Prime Mover Power set the same as MaxRailOutputPower {0} value", FormatStrings.FormatPower(MaximumDieselEnginePowerW, simulator.MetricUnits, false, false));

                }

            }

            // Check that maximum force value has been set
            if (MaxForceN == 0)
            {

                if (TractiveForceCurves == null)  // Basic configuration - ie no force and Power tables, etc
                {
                    float StartingSpeedMpS = 0.1f; // Assumed starting speed for diesel - can't be zero otherwise error will occurr
                    MaxForceN = LocomotiveMaxRailOutputPowerW / StartingSpeedMpS;

                    if (simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Maximum Force set to {0} value, calculated from Rail Power Value.", FormatStrings.FormatForce(MaxForceN, simulator.MetricUnits));
                }
                else
                {
                    float ThrottleSetting = 1.0f; // Must be at full throttle for these calculations
                    float StartingSpeedMpS = 0.1f; // Assumed starting speed for diesel - can't be zero otherwise error will occurr
                    float MaxForceN = (float)TractiveForceCurves.Get(ThrottleSetting, StartingSpeedMpS);

                    if (simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Maximum Force set to {0} value, calcuated from Tractive Force Tables", FormatStrings.FormatForce(MaxForceN, simulator.MetricUnits));
                }


            }


            // Check force assumptions set for diesel
            if (simulator.Settings.VerboseConfigurationMessages)
            {
                CalculatedMaxContinuousForceN = 0;
                float ThrottleSetting = 1.0f; // Must be at full throttle for these calculations
                if (TractiveForceCurves == null)  // Basic configuration - ie no force and Power tables, etc
                {
                    CalculatedMaxContinuousForceN = ThrottleSetting * LocomotiveMaxRailOutputPowerW / SpeedOfMaxContinuousForceMpS;
                    Trace.TraceInformation("Diesel Force Settings (BASIC Config): Max Starting Force {0}, Calculated Max Continuous Force {1} @ speed of {2}", FormatStrings.FormatForce(MaxForceN, simulator.MetricUnits), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, simulator.MetricUnits), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));
                    Trace.TraceInformation("Diesel Power Settings (BASIC Config): Prime Mover {0}, Max Rail Output Power {1}", FormatStrings.FormatPower(MaximumDieselEnginePowerW, simulator.MetricUnits, false, false), FormatStrings.FormatPower(LocomotiveMaxRailOutputPowerW, simulator.MetricUnits, false, false));

                    if (MaxForceN < MaxContinuousForceN)
                    {
                        Trace.TraceInformation("!!!! Warning: Starting Tractive force {0} is less then Calculated Continuous force {1}, please check !!!!", FormatStrings.FormatForce(MaxForceN, simulator.MetricUnits), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, simulator.MetricUnits), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));
                    }

                }
                else // Advanced configuration - 
                {
                    float StartingSpeedMpS = 0.1f; // Assumed starting speed for diesel - can't be zero otherwise error will occurr
                    float StartingForceN = (float)TractiveForceCurves.Get(ThrottleSetting, StartingSpeedMpS);
                    CalculatedMaxContinuousForceN = (float)TractiveForceCurves.Get(ThrottleSetting, SpeedOfMaxContinuousForceMpS);
                    Trace.TraceInformation("Diesel Force Settings (ADVANCED Config): Max Starting Force {0}, Calculated Max Continuous Force {1}, @ speed of {2}", FormatStrings.FormatForce(StartingForceN, simulator.MetricUnits), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, simulator.MetricUnits), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));
                    Trace.TraceInformation("Diesel Power Settings (ADVANCED Config): Prime Mover {0}, Max Rail Output Power {1} @ {2} rpm", FormatStrings.FormatPower(DieselEngines.MaxPowerW, simulator.MetricUnits, false, false), FormatStrings.FormatPower(DieselEngines.MaximumRailOutputPowerW, simulator.MetricUnits, false, false), MaxRPM);

                    if (StartingForceN < MaxContinuousForceN)
                    {
                        Trace.TraceInformation("!!!! Warning: Calculated Starting Tractive force {0} is less then Calculated Continuous force {1}, please check !!!!", FormatStrings.FormatForce(StartingForceN, simulator.MetricUnits), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, simulator.MetricUnits), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));
                    }
                }

                // Check that MaxPower value is realistic - Calculate power - metric - P = F x V
                float CalculatedContinuousPowerW = MaxContinuousForceN * SpeedOfMaxContinuousForceMpS;
                if (MaxPowerW < CalculatedContinuousPowerW)
                {
                    Trace.TraceInformation("!!!! Warning: MaxPower {0} is less then continuous force calculated power {1} @ speed of {2}, please check !!!!", FormatStrings.FormatPower(MaxPowerW, simulator.MetricUnits, false, false), FormatStrings.FormatPower(CalculatedContinuousPowerW, simulator.MetricUnits, false, false), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));
                }

                if (DieselEngines.GearBox == null)
                {
                    // Check Adhesion values
                    var calculatedmaximumpowerw = CalculatedMaxContinuousForceN * SpeedOfMaxContinuousForceMpS;
                    var maxforcekN = MaxForceN / 1000.0f;
                    var designadhesionzerospeed = maxforcekN / (Mass.Kilogram.ToTonnes(DrvWheelWeightKg) * 10);
                    var calculatedmaxcontinuousforcekN = CalculatedMaxContinuousForceN / 1000.0f;
                    var designadhesionmaxcontspeed = calculatedmaxcontinuousforcekN / (Mass.Kilogram.ToTonnes(DrvWheelWeightKg) * 10);
                    var zerospeed = 0;
                    var configuredadhesionzerospeed = (Curtius_KnifflerA / (zerospeed + Curtius_KnifflerB) + Curtius_KnifflerC);
                    var configuredadhesionmaxcontinuousspeed = (Curtius_KnifflerA / (SpeedOfMaxContinuousForceMpS + Curtius_KnifflerB) + Curtius_KnifflerC);
                    var dropoffspeed = calculatedmaximumpowerw / (MaxForceN);
                    var configuredadhesiondropoffspeed = (Curtius_KnifflerA / (dropoffspeed + Curtius_KnifflerB) + Curtius_KnifflerC);

                    Trace.TraceInformation("Apparent (Design) Adhesion: Zero - {0:N2} @ {1}, Max Continuous Speed - {2:N2} @ {3}, Drive Wheel Weight - {4}", designadhesionzerospeed, FormatStrings.FormatSpeedDisplay(zerospeed, simulator.MetricUnits), designadhesionmaxcontspeed, FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits), FormatStrings.FormatMass(DrvWheelWeightKg, simulator.MetricUnits));
                    Trace.TraceInformation("OR Calculated Adhesion Setting: Zero Speed - {0:N2} @ {1}, Dropoff Speed - {2:N2} @ {3}, Max Continuous Speed - {4:N2} @ {5}", configuredadhesionzerospeed, FormatStrings.FormatSpeedDisplay(zerospeed, simulator.MetricUnits), configuredadhesiondropoffspeed, FormatStrings.FormatSpeedDisplay(dropoffspeed, simulator.MetricUnits), configuredadhesionmaxcontinuousspeed, FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));
                }

                Trace.TraceInformation("===================================================================================================================\n\n");
            }

        }

        /// <summary>
        /// This initializer is called when we are making a new copy of a locomotive already
        /// loaded in memory.  We use this one to speed up loading by eliminating the
        /// need to parse the wag file multiple times.
        /// NOTE:  you must initialize all the same variables as you parsed above
        /// </summary>
        public override void Copy(MSTSWagon source)
        {
            base.Copy(source);  // each derived level initializes its own variables

            MSTSDieselLocomotive locoCopy = (MSTSDieselLocomotive)source;
            EngineRPM = locoCopy.EngineRPM;
            IdleRPM = locoCopy.IdleRPM;
            MaxRPM = locoCopy.MaxRPM;
            GovernorRPM = locoCopy.GovernorRPM;
            MaxRPMChangeRate = locoCopy.MaxRPMChangeRate;
            MaximumDieselEnginePowerW = locoCopy.MaximumDieselEnginePowerW;
            PercentChangePerSec = locoCopy.PercentChangePerSec;
            LocomotiveMaxRailOutputPowerW = locoCopy.LocomotiveMaxRailOutputPowerW;
            DieselTransmissionType = locoCopy.DieselTransmissionType;

            EngineRPMderivation = locoCopy.EngineRPMderivation;
            EngineRPMold = locoCopy.EngineRPMold;

            MaxDieselLevelL = locoCopy.MaxDieselLevelL;
            DieselUsedPerHourAtMaxPowerL = locoCopy.DieselUsedPerHourAtMaxPowerL;
            DieselUsedPerHourAtIdleL = locoCopy.DieselUsedPerHourAtIdleL;

            DieselFlowLps = 0.0f;
            InitialMassKg = MassKG;

            if (this.CarID.StartsWith('0'))
                DieselLevelL = locoCopy.DieselLevelL;
            else
                DieselLevelL = locoCopy.MaxDieselLevelL;

            if (locoCopy.GearBoxController != null)
                GearBoxController = new MSTSNotchController(locoCopy.GearBoxController);

            DieselEngines.Copy(locoCopy.DieselEngines);
        }

        public override void Initialize()
        {
            DieselEngines.Initialize();

            if (DieselEngines[0].GearBox != null)
            {
                GearBoxController = new MSTSNotchController(DieselEngines[0].GearBox.NumOfGears + 1);
            }

            base.Initialize();

            // Initialise water level in steam heat boiler
            if (CurrentLocomotiveSteamHeatBoilerWaterCapacityL == 0 && IsSteamHeatFitted)
            {
                if (maximumSteamHeatBoilerWaterTankCapacityL != 0)
                {
                    CurrentLocomotiveSteamHeatBoilerWaterCapacityL = (float)maximumSteamHeatBoilerWaterTankCapacityL;
                }
                else
                {
                    CurrentLocomotiveSteamHeatBoilerWaterCapacityL = (float)Size.LiquidVolume.FromGallonUK(800.0f);
                }
                // Check force assumptions set for diesel
                if (EngineType == EngineType.Diesel && SpeedOfMaxContinuousForceMpS != 0)
                {

                    float ThrottleSetting = 1.0f; // Must be at full throttle for these calculations
                    if (TractiveForceCurves == null)  // Basic configuration - ie no force and Power tables, etc
                    {
                        float CalculatedMaxContinuousForceN = ThrottleSetting * DieselEngines.MaximumRailOutputPowerW / SpeedOfMaxContinuousForceMpS;
                        Trace.TraceInformation("Diesel Force Settings (BASIC Config):Max Starting Force {0}, Max Continuous Force {1} @ speed of {2}", FormatStrings.FormatForce(MaxForceN, simulator.MetricUnits), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, simulator.MetricUnits), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));
                        Trace.TraceInformation("Diesel Power Settings (BASIC Config):Prime Mover {0}, Rail Output Power {1}", FormatStrings.FormatPower(MaximumDieselEnginePowerW, simulator.MetricUnits, false, false), FormatStrings.FormatPower(DieselEngines.MaximumRailOutputPowerW, simulator.MetricUnits, false, false));
                    }
                    else // Advanced configuration - 
                    {
                        float StartingSpeedMpS = 0.1f; // Assumed starting speed for diesel - can't be zero otherwise error will occurr
                        float StartingForceN = (float)TractiveForceCurves.Get(ThrottleSetting, StartingSpeedMpS);
                        float CalculatedMaxContinuousForceN = (float)TractiveForceCurves.Get(ThrottleSetting, SpeedOfMaxContinuousForceMpS);
                        Trace.TraceInformation("Diesel Force Settings (ADVANCED Config): Max Starting Force {0} Max Continuous Force {1}, @ speed of {2}", FormatStrings.FormatForce(StartingForceN, simulator.MetricUnits), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, simulator.MetricUnits), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, simulator.MetricUnits));
                        Trace.TraceInformation("Diesel Power Settings (ADVANCED Config):Prime Mover {0}, Rail Output Power {1} @ {2} rpm", FormatStrings.FormatPower(MaximumDieselEnginePowerW, simulator.MetricUnits, false, false), FormatStrings.FormatPower(DieselEngines.MaximumRailOutputPowerW, simulator.MetricUnits, false, false), MaxRPM);
                    }
                }

            }
            // TO BE LOOKED AT - fix restoration process for gearbox and gear controller
            // It appears that the gearbox is initialised in two different places to cater for Basic and Advanced ENG file configurations(?).
            // Hence the restore values recovered in gearbox class are being overwritten , and resume was not working correctly
            // Hence restore gear position values are read as part of the diesel and restored at this point.
            if (gearSaved)
            {
                DieselEngines[0].GearBox.NextGearIndex = currentnextGearRestore;
                DieselEngines[0].GearBox.CurrentGearIndex = currentGearIndexRestore;
                GearBoxController.SetValue((float)DieselEngines[0].GearBox.CurrentGearIndex);
            }

            if (simulator.Settings.VerboseConfigurationMessages)
            {
                if (DieselEngines.GearBox is GearBox gearBox)
                {
                    Trace.TraceInformation("==================================================== {0} has Gearbox =========================================================", LocomotiveName);
                    Trace.TraceInformation("Gearbox Type: {0}, Transmission Type: {1}, Number of Gears: {2}, Idle RpM: {3}, Max RpM: {4}, Gov RpM: {5}, GearBoxType: {6}, ClutchType: {7}, FreeWheel: {8}", gearBox.GearBoxOperation, DieselTransmissionType, gearBox.NumOfGears, DieselEngines[0].IdleRPM, DieselEngines[0].MaxRPM, DieselEngines[0].GovernorRPM, gearBox.GearBoxType, gearBox.ClutchType, gearBox.GearBoxFreeWheelFitted);

                    Trace.TraceInformation("Gear\t Ratio\t Max Speed\t Max TE\t    Chg Up RpM\t Chg Dwn RpM\t Coast Force\t Back Force\t");

                    for (int i = 0; i < DieselEngines[0].GearBox.NumOfGears; i++)
                    {
                        Trace.TraceInformation("\t{0}\t\t\t {1:N2}\t\t{2:N2}\t\t{3:N2}\t\t\t{4}\t\t\t\t{5:N0}\t\t\t\t\t{6}\t\t\t{7}", i + 1, gearBox.Gears[i].Ratio, FormatStrings.FormatSpeedDisplay(gearBox.Gears[i].MaxSpeedMpS, simulator.MetricUnits), FormatStrings.FormatForce(gearBox.Gears[i].MaxTractiveForceN, simulator.MetricUnits), gearBox.Gears[i].ChangeUpSpeedRpM, gearBox.Gears[i].ChangeDownSpeedRpM, FormatStrings.FormatForce(gearBox.Gears[i].CoastingForceN, simulator.MetricUnits), FormatStrings.FormatForce(gearBox.Gears[i].BackLoadForceN, simulator.MetricUnits));

                    }

                    var calculatedmaxcontinuousforcekN = gearBox.Gears[0].MaxTractiveForceN / 1000.0f;
                    var designadhesionmaxcontspeed = calculatedmaxcontinuousforcekN / (Mass.Kilogram.ToTonnes(DrvWheelWeightKg) * 10);

                    Trace.TraceInformation("Apparent (Design) Adhesion for Gear 1: {0:N2} @ {1}, Drive Wheel Weight - {2}", designadhesionmaxcontspeed, FormatStrings.FormatSpeedDisplay(gearBox.Gears[0].MaxSpeedMpS, simulator.MetricUnits), FormatStrings.FormatMass(DrvWheelWeightKg, simulator.MetricUnits));

                    Trace.TraceInformation("===================================================================================================================\n\n");
                }
            }

        }

        /// <summary>
        /// We are saving the game.  Save anything that we'll need to restore the 
        /// status later.
        /// </summary>
        public override void Save(BinaryWriter outf)
        {
            // for example
            // outf.Write(Pan);
            base.Save(outf);
            outf.Write(DieselLevelL);
            outf.Write(CurrentLocomotiveSteamHeatBoilerWaterCapacityL);
            DieselEngines.Save(outf);
            ControllerFactory.Save(GearBoxController, outf);
        }

        /// <summary>
        /// We are restoring a saved game.  The TrainCar class has already
        /// been initialized.   Restore the game state.
        /// </summary>
        public override void Restore(BinaryReader inf)
        {
            base.Restore(inf);
            DieselLevelL = inf.ReadSingle();
            CurrentLocomotiveSteamHeatBoilerWaterCapacityL = inf.ReadDouble();
            DieselEngines.Restore(inf);
            ControllerFactory.Restore(GearBoxController, inf);

        }

        //================================================================================================//
        /// <summary>
        /// Set starting conditions  when initial speed > 0 
        /// 

        public override void InitializeMoving()
        {
            base.InitializeMoving();
            WheelSpeedMpS = SpeedMpS;
            DynamicBrakePercent = -1;

            DieselEngines.InitializeMoving();

            if (DieselEngines[0].GearBox != null && GearBoxController != null)
            {
                if (IsLeadLocomotive())
                {
                    Train.MUGearboxGearIndex = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                    Train.AITrainGearboxGearIndex = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                }
                GearBoxController.NotchIndex = Train.MUGearboxGearIndex;
                GearboxGearIndex = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                GearBoxController.SetValue((float)GearBoxController.NotchIndex);
            }

            ThrottleController.SetValue(Train.MUThrottlePercent / 100);
        }


        /// <summary>
        /// This function updates periodically the states and physical variables of the locomotive's subsystems.
        /// </summary>
        public override void Update(double elapsedClockSeconds)
        {
            DieselEngines.Update(elapsedClockSeconds);

            ExhaustParticles.Update(elapsedClockSeconds, DieselEngines[0].ExhaustParticles);
            ExhaustMagnitude.Update(elapsedClockSeconds, DieselEngines[0].ExhaustMagnitude);
            ExhaustColorR.Update(elapsedClockSeconds, DieselEngines[0].ExhaustColor.R);
            ExhaustColorG.Update(elapsedClockSeconds, DieselEngines[0].ExhaustColor.G);
            ExhaustColorB.Update(elapsedClockSeconds, DieselEngines[0].ExhaustColor.B);

            base.Update(elapsedClockSeconds);

            // The following is not in the UpdateControllers function due to the fact that fuel level has to be calculated after the motive force calculation.
            FuelController.Update(elapsedClockSeconds);
            if (FuelController.UpdateValue > 0.0)
                simulator.Confirmer.UpdateWithPerCent(CabControl.DieselFuel, CabSetting.Increase, FuelController.CurrentValue * 100);

            // Update water controller for steam boiler heating tank
            if (this.IsLeadLocomotive() && IsSteamHeatFitted)
            {
                WaterController.Update(elapsedClockSeconds);
                if (WaterController.UpdateValue > 0.0)
                    simulator.Confirmer.UpdateWithPerCent(CabControl.SteamHeatBoilerWater, CabSetting.Increase, WaterController.CurrentValue * 100);
            }
            distributedPowerStatus.Update(null);
        }

        /// <summary>
        /// This function updates periodically the states and physical variables of the locomotive's controllers.
        /// </summary>
        protected override void UpdateControllers(double elapsedClockSeconds)
        {
            base.UpdateControllers(elapsedClockSeconds);

            //Currently the ThrottlePercent is global to the entire train
            //So only the lead locomotive updates it, the others only updates the controller (actually useless)
            if (this.IsLeadLocomotive() || (RemoteControlGroup == RemoteControlGroup.Unconnected))
            {
                if (GearBoxController != null)
                {
                    GearboxGearIndex = (int)GearBoxController.UpdateAndSetBoost(elapsedClockSeconds, GearBoxControllerBoost);
                }
            }
            else
            {
                if (GearBoxController != null)
                {
                    GearBoxController.UpdateAndSetBoost(elapsedClockSeconds, GearBoxControllerBoost);
                }
            }
        }

        /// <summary>
        /// This function updates periodically the locomotive's motive force.
        /// </summary>
        protected override void UpdateTractiveForce(double elapsedClockSeconds, float t, float AbsSpeedMpS, float AbsWheelSpeedMpS)
        {
            // This section calculates the motive force of the locomotive as follows:
            // Basic configuration (no TF table) - uses P = F /speed  relationship - requires power and force parameters to be set in the ENG file. 
            // Advanced configuration (TF table) - use a user defined tractive force table
            // With Simple adhesion apart from correction for rail adhesion, there is no further variation to the motive force. 
            // With Advanced adhesion the raw motive force is fed into the advanced (axle) adhesion model, and is corrected for wheel slip and rail adhesion
            // TO be Checked how main power supply conditions apply to geared locomotives - Note for geared locomotives it is possible to get some tractive force due to the drag of a stalled engine, if in gear, and clutch engaged
            if ((LocomotivePowerSupply.MainPowerSupplyOn || DieselEngines.GearBox != null) && Direction != MidpointDirection.N)
            {
                // Appartent throttle setting is a reverse lookup of the throttletab vs rpm, hence motive force increase will be related to increase in rpm. The minimum of the two values
                // is checked to enable fast reduction in tractive force when decreasing the throttle. Typically it will take longer for the prime mover to decrease rpm then drop motive force.
                float LocomotiveApparentThrottleSetting;
                if (IsPlayerTrain)
                {
                    LocomotiveApparentThrottleSetting = Math.Min(t, DieselEngines.ApparentThrottleSetting / 100.0f);
                }
                else // For AI trains, just use the throttle setting
                {
                    LocomotiveApparentThrottleSetting = t;
                }

                LocomotiveApparentThrottleSetting = MathHelper.Clamp(LocomotiveApparentThrottleSetting, 0.0f, 1.0f);  // Clamp decay within bounds

                // If there is more then one diesel engine, and one or more engines is stopped, then the Fraction Power will give a fraction less then 1 depending upon power definitions of engines.
                float DieselEngineFractionPower = 1.0f;

                if (DieselEngines.Count > 1)
                {
                    DieselEngineFractionPower = DieselEngines.RunningPowerFraction;
                }

                DieselEngineFractionPower = MathHelper.Clamp(DieselEngineFractionPower, 0.0f, 1.0f);  // Clamp decay within bounds


                // For the advanced adhesion model, a rudimentary form of slip control is incorporated by using the wheel speed to calculate tractive effort.
                // As wheel speed is increased tractive effort is decreased. Hence wheel slip is "controlled" to a certain extent.
                // This doesn't cover all types of locomotives, for eaxmple if DC traction motors and no slip control, then the tractive effort shouldn't be reduced. This won't eliminate slip, but limits
                // its impact. More modern locomotive have a more sophisticated system that eliminates slip in the majority (if not all circumstances).
                // Simple adhesion control does not have any slip control feature built into it.
                // TODO - a full review of slip/no slip control.
                if (WheelSlip && AdvancedAdhesionModel)
                {
                    AbsTractionSpeedMpS = AbsWheelSpeedMpS;
                }
                else
                {
                    AbsTractionSpeedMpS = AbsSpeedMpS;
                }

                if (TractiveForceCurves == null)
                {
                    // This sets the maximum force of the locomotive, it will be adjusted down if it exceeds the max power of the locomotive.
                    float maxForceN = Math.Min(t * MaxForceN * (1 - PowerReduction), AbsTractionSpeedMpS == 0.0f ? (t * MaxForceN * (1 - PowerReduction)) : (t * LocomotiveMaxRailOutputPowerW / AbsTractionSpeedMpS));

                    // Maximum rail power is reduced by apparent throttle factor and the number of engines running (power ratio)
                    float maxPowerW = LocomotiveMaxRailOutputPowerW * DieselEngineFractionPower * LocomotiveApparentThrottleSetting;

                    // If unloading speed is in ENG file, and locomotive speed is greater then unloading speed, and less then max speed, then apply a decay factor to the power/force
                    if (UnloadingSpeedMpS != 0 && AbsTractionSpeedMpS > UnloadingSpeedMpS && AbsTractionSpeedMpS < MaxSpeedMpS && !WheelSlip)
                    {
                        // use straight line curve to decay power to zero by 2 x unloading speed
                        float unloadingspeeddecay = 1.0f - (1.0f / UnloadingSpeedMpS) * (AbsTractionSpeedMpS - UnloadingSpeedMpS);
                        unloadingspeeddecay = MathHelper.Clamp(unloadingspeeddecay, 0.0f, 1.0f);  // Clamp decay within bounds
                        maxPowerW *= unloadingspeeddecay;
                    }

                    if (DieselEngines.GearBox != null)
                    {
                        TractiveForceN = DieselEngines.TractiveForceN;
                    }
                    else
                    {
                        if (maxForceN * AbsSpeedMpS > maxPowerW)
                            maxForceN = maxPowerW / AbsTractionSpeedMpS;

                        TractiveForceN = maxForceN;
                        // Motive force will be produced until power reaches zero, some locomotives had a overspeed monitor set at the maximum design speed
                    }

                }
                else
                {
                    if (DieselEngines.GearBox != null && DieselTransmissionType == DieselTransmissionType.Mechanic)
                    {
                        TractiveForceN = DieselEngines.TractiveForceN;
                    }
                    else
                    {
                        // Tractive force is read from Table using the apparent throttle setting, and then reduced by the number of engines running (power ratio)
                        TractiveForceN = (float)TractiveForceCurves.Get(LocomotiveApparentThrottleSetting, AbsTractionSpeedMpS) * DieselEngineFractionPower * (1 - PowerReduction);
                    }
                    if (TractiveForceN < 0 && !TractiveForceCurves.HasNegativeValues)
                        TractiveForceN = 0;
                }

            }
            else
            {
                TractiveForceN = 0f;
            }

            if (MaxForceN > 0 && MaxContinuousForceN > 0 && PowerReduction < 1)
            {
                TractiveForceN *= 1 - (MaxForceN - MaxContinuousForceN) / (MaxForceN * MaxContinuousForceN) * AverageForceN * (1 - PowerReduction);
                float w = (float)(ContinuousForceTimeFactor - elapsedClockSeconds) / ContinuousForceTimeFactor;
                if (w < 0)
                    w = 0;
                AverageForceN = w * AverageForceN + (1 - w) * TractiveForceN;
            }

            // Calculate fuel consumption will occur unless diesel engine is stopped
            DieselFlowLps = DieselEngines.DieselFlowLps;
            partialFuelConsumption += DieselEngines.DieselFlowLps * (float)elapsedClockSeconds;
            if (partialFuelConsumption >= 0.1)
            {
                DieselLevelL -= partialFuelConsumption;
                partialFuelConsumption = 0;
            }
            // stall engine if fuel runs out
            if (DieselLevelL <= 0.0f)
            {
                SignalEvent(TrainEvent.EnginePowerOff);
                DieselEngines.HandleEvent(PowerSupplyEvent.StopEngine);
            }
        }

        /// <summary>
        /// This function updates periodically the locomotive's sound variables.
        /// </summary>
        protected override void UpdateSoundVariables(double elapsedClockSeconds)
        {
            EngineRPMRatio = (DieselEngines[0].RealRPM - DieselEngines[0].IdleRPM) / (DieselEngines[0].MaxRPM - DieselEngines[0].IdleRPM);

            Variable1 = ThrottlePercent / 100.0f;
            // else Variable1 = MotiveForceN / MaxForceN; // Gearbased, Variable1 proportional to motive force
            // allows for motor volume proportional to effort.

            // Refined Variable2 setting to graduate
            if (Variable2 != EngineRPMRatio)
            {
                // We must avoid Variable2 to run outside of [0, 1] range, even temporarily (because of multithreading)
                Variable2 = EngineRPMRatio < Variable2 ?
                    (float)Math.Max(Math.Max(Variable2 - elapsedClockSeconds * PercentChangePerSec, EngineRPMRatio), 0) :
                    (float)Math.Min(Math.Min(Variable2 + elapsedClockSeconds * PercentChangePerSec, EngineRPMRatio), 1);
            }

            EngineRPM = Variable2 * (MaxRPM - IdleRPM) + IdleRPM;

            if (DynamicBrakePercent > 0)
            {
                if (MaxDynamicBrakeForceN == 0)
                    Variable3 = DynamicBrakePercent / 100f;
                else
                    Variable3 = DynamicBrakeForceN / MaxDynamicBrakeForceN;
            }
            else
                Variable3 = 0;

            if (elapsedClockSeconds > 0.0f)
            {
                EngineRPMderivation = (EngineRPM - EngineRPMold) / (float)elapsedClockSeconds;
                EngineRPMold = EngineRPM;
            }
        }

        public override void ChangeGearUp()
        {
            if (DieselEngines[0].GearBox != null)
            {
                if (DieselEngines[0].GearBox.GearBoxOperation == GearBoxOperation.Semiautomatic)
                {
                    DieselEngines[0].GearBox.AutoGearUp();
                    GearBoxController.SetValue((float)DieselEngines[0].GearBox.NextGearIndex);
                }
                else if (DieselEngines[0].GearBox.GearBoxOperation == GearBoxOperation.Manual)
                {
                    DieselEngines[0].GearBox.ManualGearUp = true;
                }
            }
        }

        public override void ChangeGearDown()
        {

            if (DieselEngines[0].GearBox != null)
            {
                if (DieselEngines[0].GearBox.GearBoxOperation == GearBoxOperation.Semiautomatic)
                {
                    DieselEngines[0].GearBox.AutoGearDown();
                    GearBoxController.SetValue((float)DieselEngines[0].GearBox.NextGearIndex);
                }
                else if (DieselEngines[0].GearBox.GearBoxOperation == GearBoxOperation.Manual)
                {
                    DieselEngines[0].GearBox.ManualGearDown = true;
                }
            }
        }

        public override float GetDataOf(CabViewControl cvc)
        {
            float data = 0;

            switch (cvc.ControlType)
            {
                case CabViewControlType.Gears:
                    if (DieselEngines.GearBox is GearBox gearBox)
                        data = gearBox.CurrentGearIndex + 1;
                    break;
                case CabViewControlType.Fuel_Gauge:
                    if (cvc.ControlUnit == CabViewControlUnit.Gallons)
                        data = (float)Size.LiquidVolume.ToGallonUS(DieselLevelL);
                    else
                        data = DieselLevelL;
                    break;

                case CabViewControlType.Orts_Traction_CutOff_Relay_Driver_Closing_Order:
                    data = DieselPowerSupply.TractionCutOffRelay.DriverClosingOrder ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Traction_CutOff_Relay_Driver_Opening_Order:
                    data = DieselPowerSupply.TractionCutOffRelay.DriverOpeningOrder ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Traction_CutOff_Relay_Driver_Closing_Authorization:
                    data = DieselPowerSupply.TractionCutOffRelay.DriverClosingAuthorization ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Traction_CutOff_Relay_State:
                    switch (DieselPowerSupply.TractionCutOffRelay.State)
                    {
                        case TractionCutOffRelayState.Open:
                            data = 0;
                            break;
                        case TractionCutOffRelayState.Closing:
                            data = 1;
                            break;
                        case TractionCutOffRelayState.Closed:
                            data = 2;
                            break;
                    }
                    break;

                case CabViewControlType.Orts_Traction_CutOff_Relay_Closed:
                    switch (DieselPowerSupply.TractionCutOffRelay.State)
                    {
                        case TractionCutOffRelayState.Open:
                        case TractionCutOffRelayState.Closing:
                            data = 0;
                            break;
                        case TractionCutOffRelayState.Closed:
                            data = 1;
                            break;
                    }
                    break;

                case CabViewControlType.Orts_Traction_CutOff_Relay_Open:
                    switch (DieselPowerSupply.TractionCutOffRelay.State)
                    {
                        case TractionCutOffRelayState.Open:
                        case TractionCutOffRelayState.Closing:
                            data = 1;
                            break;
                        case TractionCutOffRelayState.Closed:
                            data = 0;
                            break;
                    }
                    break;

                case CabViewControlType.Orts_Traction_CutOff_Relay_Authorized:
                    data = DieselPowerSupply.TractionCutOffRelay.ClosingAuthorization ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Traction_CutOff_Relay_Open_And_Authorized:
                    data = (DieselPowerSupply.TractionCutOffRelay.State < TractionCutOffRelayState.Closed && DieselPowerSupply.TractionCutOffRelay.ClosingAuthorization) ? 1 : 0;
                    break;

                default:
                    data = base.GetDataOf(cvc);
                    break;
            }

            return data;
        }

        public string DistributedPowerThrottleInfo()
        {
            string throttle;
            if (ThrottlePercent > 0)
            {
                throttle = ThrottleController.NotchCount() > 3
                    ? Simulator.Catalog.GetParticularString("Notch", "N") + MathHelper.Clamp(ThrottleController.GetNearestNotch(ThrottlePercent / 100f), 1, 8)
                    : $"{ThrottlePercent:F0}%";
            }
            else if (DynamicBrakePercent > 0 && DynamicBrake)
            {
                if (RemoteControlGroup == RemoteControlGroup.RearGroupAsync)
                {
                    throttle = Simulator.Catalog.GetParticularString("Notch", "B") + MathHelper.Clamp((Train.LeadLocomotive as MSTSLocomotive).DistributedPowerDynamicBrakeController.NotchIndex, 1, 8);
                }
                else
                {
                    // The clause here below leads to possible differences of one notch near the notch value, and therefore is commented
                    //               if (DynamicBrakeController.NotchCount() > 3)
                    //                   throttle = Simulator.Catalog.GetParticularString("Notch", "B") + MathHelper.Clamp((DynamicBrakeController.GetNearestNotch(DynamicBrakePercent / 100f)), 1, 8);
                    //               else
                    throttle = Simulator.Catalog.GetParticularString("Notch", "B") + MathHelper.Clamp((Train.LeadLocomotive as MSTSLocomotive).DistributedPowerDynamicBrakeController.GetNotch(DynamicBrakePercent / 100f), 1, 8);
                }
            }
            else if (DynamicBrakePercent == 0 && !DynamicBrake)
                throttle = Simulator.Catalog.GetString("Setup");
            else
                throttle = Simulator.Catalog.GetParticularString("Notch", "Idle");

            return throttle;
        }

        public double DistributedPowerForceInfo()
        {
            double data = FilteredMotiveForceN != 0 ? (double)Math.Abs(FilteredMotiveForceN) : (double)Math.Abs(LocomotiveAxle.DriveForceN);
            if (DynamicBrakePercent > 0)
            {
                data = -Math.Abs(DynamicBrakeForceN);
            }
            if (simulator.Route.MilepostUnitsMetric)  // return an Ampere value
            {
                if (ThrottlePercent >= 0 && DynamicBrakePercent == -1)
                {
                    data = (data / MaxForceN) * MaxCurrentA;
                }
                if (ThrottlePercent == 0 && DynamicBrakePercent >= 0)
                {
                    data = (data / MaxDynamicBrakeForceN) * DynamicBrakeMaxCurrentA;
                }
                return data;
            }
            else // return a Kilo Lbs value 
            {
                data = Dynamics.Force.ToLbf(data) * 0.001f;
                return data;
            }
        }

        public string GetDpuStatus(bool dataDpu, CabViewControlUnit loadUnits = CabViewControlUnit.None)// used by the TrainDpuInfo window
        {
            string throttle;
            if (ThrottlePercent > 0)
            {
                if (ThrottleController.NotchCount() > 3)
                    throttle = Simulator.Catalog.GetParticularString("Notch", "N") + MathHelper.Clamp(ThrottleController.GetNearestNotch(ThrottlePercent / 100f), 1, 8);
                else
                    throttle = $"{ThrottlePercent:F0}%";
            }
            else if (DynamicBrakePercent > 0 && DynamicBrake)
            {
                if (RemoteControlGroup == RemoteControlGroup.RearGroupAsync)
                {
                    throttle = Simulator.Catalog.GetParticularString("Notch", "B") + MathHelper.Clamp((Train.LeadLocomotive as MSTSLocomotive).DistributedPowerDynamicBrakeController.NotchIndex, 1, 8);
                }
                else
                {
                    // The clause here below leads to possible differences of one notch near the notch value, and therefore is commented
                    //               if (DynamicBrakeController.NotchCount() > 3)
                    //                   throttle = Simulator.Catalog.GetParticularString("Notch", "B") + MathHelper.Clamp((DynamicBrakeController.GetNearestNotch(DynamicBrakePercent / 100f)), 1, 8);
                    //               else
                    throttle = Simulator.Catalog.GetParticularString("Notch", "B") + MathHelper.Clamp((Train.LeadLocomotive as MSTSLocomotive).DistributedPowerDynamicBrakeController.GetNotch(DynamicBrakePercent / 100f), 1, 8);
                }
            }
            else if (DynamicBrakePercent == 0 && !DynamicBrake)
                throttle = Simulator.Catalog.GetString("Setup");
            else
                throttle = Simulator.Catalog.GetParticularString("Notch", "Idle");
            if (DynamicBrakePercent >= 0)
                throttle += "???";

            var status = new StringBuilder();
            // ID
            status.Append($"{CarID.Replace(" ", "")}({DistributedPowerUnitId})\t");
            // Throttle
            status.Append($"{throttle}\t");

            // Load
            float data;
            if (FilteredMotiveForceN != 0)
                data = Math.Abs(this.FilteredMotiveForceN);
            else
                data = Math.Abs(this.LocomotiveAxle.DriveForceN);
            if (DynamicBrakePercent > 0)
            {
                data = -Math.Abs(DynamicBrakeForceN);
            }
            if (loadUnits == CabViewControlUnit.None)
                loadUnits = simulator.Route.MilepostUnitsMetric ? CabViewControlUnit.Amps : CabViewControlUnit.Kilo_Lbs;
            switch (loadUnits)
            {
                case CabViewControlUnit.Amps:
                    if (ThrottlePercent >= 0 && DynamicBrakePercent == -1)
                    {
                        data = (data / MaxForceN) * MaxCurrentA;
                    }
                    if (ThrottlePercent == 0 && DynamicBrakePercent >= 0)
                    {
                        data = (data / MaxDynamicBrakeForceN) * DynamicBrakeMaxCurrentA;
                    }
                    status.Append($"{data:F0} A");
                    break;

                case CabViewControlUnit.Newtons:
                    status.Append($"{data:F0} N");
                    break;

                case CabViewControlUnit.Kilo_Newtons:
                    data /= 1000.0f;
                    status.Append($"{data:F0} kN");
                    break;

                case CabViewControlUnit.Lbs:
                    data = (float)Dynamics.Force.ToLbf(data);
                    status.Append($"{data:F0} l");
                    break;

                case CabViewControlUnit.Kilo_Lbs:
                default:
                    data = (float)Dynamics.Force.ToLbf(data) * 0.001f;
                    status.Append($"{data:F0} K");
                    break;
            }

            status.Append((data < 0 ? "???" : " ") + "\t");

            // BP
            var brakeInfoValue = BrakeValue(Simulator.Catalog.GetString("BP"), Simulator.Catalog.GetString("EOT"));
            status.Append($"{brakeInfoValue:F0}\t");

            // Flow.
            // TODO:The BP air flow that feeds the brake tube is not yet modeled in Open Rails.

            // Remote
            if (dataDpu)
            {
                status.Append($"{(IsLeadLocomotive() || RemoteControlGroup < 0 ? "———" : RemoteControlGroup == 0 ? Simulator.Catalog.GetString("Sync") : Simulator.Catalog.GetString("Async"))}\t");
            }
            else
            {
                status.Append($"{(IsLeadLocomotive() || RemoteControlGroup < 0 ? "———" : RemoteControlGroup == 0 ? Simulator.Catalog.GetString("Sync") : Simulator.Catalog.GetString("Async"))}");
            }

            if (dataDpu)
            {   // ER
                brakeInfoValue = BrakeValue(Simulator.Catalog.GetString("EQ"), Simulator.Catalog.GetString("BC"));
                status.Append($"{brakeInfoValue:F0}\t");

                // BC
                brakeInfoValue = Math.Round(BrakeSystem.GetCylPressurePSI()).ToString() + " psi";
                status.Append($"{brakeInfoValue:F0}\t");

                // MR
                status.Append($"{(FormatStrings.FormatPressure((Simulator.Instance.PlayerLocomotive as MSTSLocomotive).MainResPressurePSI, Pressure.Unit.PSI, (Simulator.Instance.PlayerLocomotive as MSTSLocomotive).BrakeSystemPressureUnits[BrakeSystemComponent.MainReservoir], true)):F0}");
            }
            return status.ToString();
        }


        //TODO 20220901 this should be refactored
        private static string BrakeValue(string tokenIni, string tokenEnd) // used by GetDpuStatus(bool dataHud)
        {
            string trainBrakeStatus = Simulator.Instance.PlayerLocomotive.GetTrainBrakeStatus();
            var brakeInfoValue = "-";
            if (trainBrakeStatus.Contains(tokenIni, StringComparison.OrdinalIgnoreCase) && trainBrakeStatus.Contains(tokenEnd, StringComparison.OrdinalIgnoreCase))
            {
                var indexIni = trainBrakeStatus.IndexOf(tokenIni, StringComparison.OrdinalIgnoreCase) + tokenIni.Length + 1;
                var indexEnd = trainBrakeStatus.IndexOf(tokenEnd, StringComparison.OrdinalIgnoreCase) - indexIni;
                if (indexEnd > 0)// BP found before EOT
                    brakeInfoValue = trainBrakeStatus.Substring(indexIni, indexEnd).TrimEnd();
            }
            return brakeInfoValue;
        }

        public string GetMultipleUnitsConfiguration()
        {
            if (Train == null)
                return null;
            int numberOfLocomotives = 0;
            int group = 0;
            string configuration = "";
            int dpUnitId = 1;
            RemoteControlGroup remoteControlGroup = RemoteControlGroup.FrontGroupSync;
            for (var i = 0; i < Train.Cars.Count; i++)
            {
                if (Train.Cars[i] is MSTSDieselLocomotive)
                {
                    if (dpUnitId != (dpUnitId = (Train.Cars[i] as MSTSLocomotive).DistributedPowerUnitId))
                    {
                        configuration += $"{group}{(remoteControlGroup != (remoteControlGroup = Train.Cars[i].RemoteControlGroup) ? " | " : "\u2013")}"; //en-dash
                        group = 0;
                    }
                    group++;
                    numberOfLocomotives++;
                }
            }
            if (group > 0)
                configuration += $"{group}";
            return numberOfLocomotives > 0 ? configuration : null;
        }

        private static int MaxNumberOfEngines;
        private static string[] DpuLabels;
        private static string[] DPULabels;

        private static void SetDPULabels(bool dpuFull, int numberOfEngines)
        {
            MaxNumberOfEngines = numberOfEngines;
            var labels = new StringBuilder();
            labels.Append($"{Simulator.Catalog.GetString("ID")}\t");
            labels.Append($"{Simulator.Catalog.GetString("Throttle")}\t");
            labels.Append($"{Simulator.Catalog.GetString("Load")}\t");
            labels.Append($"{Simulator.Catalog.GetString("BP")}\t");
            if (!dpuFull)
            {
                labels.Append($"{Simulator.Catalog.GetString("Remote")}");
                DpuLabels = labels.ToString().Split('\t');
            }
            if (dpuFull)
            {
                labels.Append($"{Simulator.Catalog.GetString("Remote")}\t");
                labels.Append($"{Simulator.Catalog.GetString("ER")}\t");
                labels.Append($"{Simulator.Catalog.GetString("BC")}\t");
                labels.Append($"{Simulator.Catalog.GetString("MR")}");
                DPULabels = labels.ToString().Split('\t');
            }
        }

        public static string GetDpuHeader(bool dpuVerticalFull, int locomotivesInTrain, int dpuMaxNumberOfEngines)
        {
            if (MaxNumberOfEngines != dpuMaxNumberOfEngines || dpuVerticalFull ? DPULabels == null : DpuLabels == null)
                SetDPULabels(dpuVerticalFull, dpuMaxNumberOfEngines);
            string table = "";
            for (var i = 0; i < (dpuVerticalFull ? DPULabels.Length : DpuLabels.Length); i++)
            {
                table += dpuVerticalFull ? DPULabels[i] : DpuLabels[i];
                table += "\n";
            }
            table = table.TrimEnd('\n');
            return table;
        }

        /// <summary>
        /// Returns the controller which refills from the matching pickup point.
        /// </summary>
        /// <param name="type">Pickup type</param>
        /// <returns>Matching controller or null</returns>
        public override MSTSNotchController GetRefillController(PickupType type)
        {
            MSTSNotchController controller = null;
            if (type == PickupType.FuelDiesel)
                return FuelController;
            if (type == PickupType.FuelWater)
                return WaterController;
            return controller;
        }

        /// <summary>
        /// Sets step size for the fuel controller basing on pickup feed rate and engine fuel capacity
        /// </summary>
        /// <param name="type">Pickup</param>

        public override void SetStepSize(PickupObject matchPickup)
        {
            if (null == matchPickup)
                throw new ArgumentNullException(nameof(matchPickup));
            if (MaxDieselLevelL != 0)
                FuelController.SetStepSize(matchPickup.Capacity.FeedRateKGpS / MSTSNotchController.StandardBoost / (MaxDieselLevelL * DieselWeightKgpL));
            if (maximumSteamHeatBoilerWaterTankCapacityL != 0)
                WaterController.SetStepSize(matchPickup.Capacity.FeedRateKGpS / MSTSNotchController.StandardBoost / (float)maximumSteamHeatBoilerWaterTankCapacityL);
        }

        /// <summary>
        /// Sets coal and water supplies to full immediately.
        /// Provided in case route lacks pickup points for diesel oil.
        /// </summary>
        public override void RefillImmediately()
        {
            FuelController.CurrentValue = 1.0f;
            WaterController.CurrentValue = 1.0f;
        }

        /// <summary>
        /// Returns the fraction of diesel oil already in tank.
        /// </summary>
        /// <param name="pickupType">Pickup type</param>
        /// <returns>0.0 to 1.0. If type is unknown, returns 0.0</returns>
        public override float GetFilledFraction(PickupType pickupType)
        {
            if (pickupType == PickupType.FuelDiesel)
            {
                return FuelController.CurrentValue;
            }
            if (pickupType == PickupType.FuelWater)
            {
                return WaterController.CurrentValue;
            }
            return 0f;
        }

        /// <summary>
        /// Restores the type of gearbox, that was forced to
        /// automatic for AI trains
        /// </summary>
        public override void SwitchToPlayerControl()
        {
            foreach (DieselEngine de in DieselEngines)
            {
                if (de.GearBox != null)
                    de.GearBox.GearBoxOperation = de.GearBox.OriginalGearBoxOperation;
            }
            if (DieselEngines[0].GearBox != null && GearBoxController != null)
            {
                GearBoxController.NotchIndex = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                GearboxGearIndex = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                GearBoxController.SetValue((float)GearBoxController.NotchIndex);
            }

        }

        public override void SwitchToAutopilotControl()
        {
            SetDirection(MidpointDirection.Forward);
            if (!LocomotivePowerSupply.MainPowerSupplyOn)
            {
                LocomotivePowerSupply.HandleEvent(PowerSupplyEvent.QuickPowerOn);
            }
            foreach (DieselEngine de in DieselEngines)
            {
                if (de.GearBox != null)
                    de.GearBox.GearBoxOperation = GearBoxOperation.Automatic;
            }
            base.SwitchToAutopilotControl();
        }

        protected override void UpdateCarSteamHeat(double elapsedClockSeconds)
        {
            // Update Steam Heating System

            // TO DO - Add test to see if cars are coupled, if Light Engine, disable steam heating.

            if (IsSteamHeatFitted && this.IsLeadLocomotive())  // Only Update steam heating if train and locomotive fitted with steam heating
            {

                CurrentSteamHeatPressurePSI = SteamHeatController.CurrentValue * MaxSteamHeatPressurePSI;

                // Calculate steam boiler usage values
                // Don't turn steam heat on until pressure valve has been opened, water and fuel capacity also needs to be present, and steam boiler is not locked out
                if (CurrentSteamHeatPressurePSI > 0.1 && CurrentLocomotiveSteamHeatBoilerWaterCapacityL > 0 && DieselLevelL > 0 && !steamHeatBoilerLockedOut)
                {
                    // Set values for visible exhaust based upon setting of steam controller
                    HeatingSteamBoilerVolumeM3pS = 1.5f * SteamHeatController.CurrentValue;
                    HeatingSteamBoilerDurationS = 1.0f * SteamHeatController.CurrentValue;
                    Train.CarSteamHeatOn = true; // turn on steam effects on wagons

                    // Calculate fuel usage for steam heat boiler
                    double FuelUsageLpS = Size.LiquidVolume.FromGallonUK(Frequency.Periodic.FromHours(TrainHeatBoilerFuelUsageGalukpH[Frequency.Periodic.ToHours(CalculatedCarHeaterSteamUsageLBpS)]));
                    DieselLevelL -= (float)(FuelUsageLpS * elapsedClockSeconds); // Reduce Tank capacity as fuel used.

                    // Calculate water usage for steam heat boiler
                    double WaterUsageLpS = Size.LiquidVolume.FromGallonUK(Frequency.Periodic.FromHours(TrainHeatBoilerWaterUsageGalukpH[Frequency.Periodic.ToHours(CalculatedCarHeaterSteamUsageLBpS)]));
                    CurrentLocomotiveSteamHeatBoilerWaterCapacityL -= (float)(WaterUsageLpS * elapsedClockSeconds); // Reduce Tank capacity as water used.
                }
                else
                {
                    Train.CarSteamHeatOn = false; // turn on steam effects on wagons
                }


            }
        }

        //used by remote diesels to update their exhaust
        public void RemoteUpdate(float exhPart, float exhMag, float exhColorR, float exhColorG, float exhColorB)
        {
            ExhaustParticles.Preset(exhPart);
            ExhaustMagnitude.Preset(exhMag);
            ExhaustColorR.Preset(exhColorR);
            ExhaustColorG.Preset(exhColorG);
            ExhaustColorB.Preset(exhColorB);
        }


        //================================================================================================//
        /// <summary>
        /// The method copes with the strange parameters that some british gear-based DMUs have: throttle 
        /// values arrive up to 1000%, and conversely GearBoxMaxTractiveForceForGears are divided by 10.
        /// Apparently MSTS works well with such values. This method recognizes such case and corrects such values.
        /// </summary>
        protected void NormalizeParams()
        {
            // check for wrong GearBoxMaxTractiveForceForGears parameters
            if (DieselEngines.MSTSGearBoxParams.GearBoxMaxTractiveForceForGearsN.Count > 0)
            {
                if (ThrottleController != null && ThrottleController.MaximumValue > 1 && MaxForceN / DieselEngines.MSTSGearBoxParams.GearBoxMaxTractiveForceForGearsN[0] > 3)
                // Tricky things have been made with this .eng file, see e.g Cravens 105; let's correct them
                {
                    for (int i = 0; i < DieselEngines.MSTSGearBoxParams.GearBoxMaxTractiveForceForGearsN.Count; i++)
                        DieselEngines.MSTSGearBoxParams.GearBoxMaxTractiveForceForGearsN[i] *= ThrottleController.MaximumValue;
                }
                float maximum = ThrottleController.MaximumValue;
                ThrottleController.Normalize(ThrottleController.MaximumValue);
                // correct also .cvf files
                if (CabViewList.Count > 0)
                    foreach (var cabView in CabViewList)
                    {
                        if (cabView.CVFFile != null && cabView.CVFFile.CabViewControls != null && cabView.CVFFile.CabViewControls.Count > 0)
                        {
                            foreach (var control in cabView.CVFFile.CabViewControls)
                            {
                                if (control is CabViewDiscreteControl discreteCabControl && control.ControlType == CabViewControlType.Throttle && discreteCabControl.Values.Count > 0 && discreteCabControl.Values[^1] > 1)
                                {
                                    var discreteControl = discreteCabControl;
                                    for (var i = 0; i < discreteControl.Values.Count; i++)
                                        discreteControl.Values[i] /= maximum;
                                    if (discreteControl.ScaleRangeMax > 0)
                                        discreteControl.ResetScaleRange(discreteControl.ScaleRangeMin, (float)discreteControl.Values[discreteControl.Values.Count - 1]);
                                }
                            }
                        }
                    }
            }
            // Check also for very low DieselEngineIdleRPM
            if (IdleRPM < 10)
                IdleRPM = Math.Max(150, MaxRPM / 10);
        }

        protected internal override void UpdateRemotePosition(double elapsedClockSeconds, float speed, float targetSpeed)
        {
            base.UpdateRemotePosition(elapsedClockSeconds, speed, targetSpeed);
            if (AbsSpeedMpS > 0.5f)
            {
                Variable1 = 0.7f;
                Variable2 = 0.7f;
            }
            else
            {
                Variable1 = 0;
                Variable2 = 0;
            }
        }

        private protected override void UpdateCarStatus()
        {
            base.UpdateCarStatus();
            carInfo["Engine"] = DieselEngines[0].State.GetLocalizedDescription();
            carInfo["Remote"] = $"{(IsLeadLocomotive() ? RemoteControlGroup.Unconnected.GetLocalizedDescription() : RemoteControlGroup.GetLocalizedDescription())}";
            if (DieselEngines.GearBox is GearBox gearBox)
            {
                carInfo["GearBox Rpm"] = $"{gearBox.HuDShaftRPM:N0}";
                carInfo["Gear"] = gearBox.CurrentGearIndex < 0 ? Simulator.Catalog.GetParticularString("Gear", "N") : $"{gearBox.CurrentGearIndex + 1}";
                carInfo["Gear Type"] = $"Type \"{gearBox.GearBoxType}\" ({gearBox.GearBoxOperation}, {gearBox.ClutchType} clutch)";
            }
            carInfo["BatterySwitch"] = LocomotivePowerSupply.BatterySwitch.On ? Simulator.Catalog.GetString("On") : Simulator.Catalog.GetString("Off");
            carInfo["MasterKey"] = LocomotivePowerSupply.MasterKey.On ? Simulator.Catalog.GetString("On") : Simulator.Catalog.GetString("Off");
            carInfo["TractionCutOffRelay"] = Simulator.Catalog.GetParticularString("TractionCutOffRelay", DieselPowerSupply.TractionCutOffRelay.State.GetLocalizedDescription());
            carInfo["ElectricTrainSupply"] = LocomotivePowerSupply.ElectricTrainSupplySwitch.On ? Simulator.Catalog.GetString("On") : Simulator.Catalog.GetString("Off");
            carInfo["PowerSupply"] = LocomotivePowerSupply.MainPowerSupplyState.GetLocalizedDescription();
            carInfo["Fuel"] = $"{FormatStrings.FormatFuelVolume(DieselLevelL, simulator.MetricUnits, Simulator.Instance.Settings.MeasurementUnit == MeasurementUnit.UK)}";

            DieselEngine engine = DieselEngines[0];
            carInfo["Power"] = FormatStrings.FormatPower(engine.CurrentDieselOutputPowerW, Simulator.Instance.MetricUnits, false, false);
            carInfo["Load"] = $"{engine.LoadPercent:F1} %";
            carInfo["RPM"] = $"{engine.RealRPM:F0} {FormatStrings.rpm}";
            carInfo["Flow"] = $"{FormatStrings.FormatFuelVolume(Frequency.Periodic.ToHours(engine.DieselFlowLps), Simulator.Instance.MetricUnits, Simulator.Instance.Settings.MeasurementUnit == MeasurementUnit.UK)}/{FormatStrings.h}";
            carInfo["Temperature"] = FormatStrings.FormatTemperature(engine.DieselTemperatureDeg, Simulator.Instance.MetricUnits);
            carInfo["Oil Pressure"] = FormatStrings.FormatPressure(engine.DieselOilPressurePSI, Pressure.Unit.PSI, MainPressureUnit, true);
        }

        private class DistributedPowerStatus : DetailInfoBase
        {
            private readonly MSTSDieselLocomotive locomotive;

            public DistributedPowerStatus(MSTSDieselLocomotive locomotive) : base(true)
            {
                this.locomotive = locomotive;
            }

            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    // ID
                    this["Car ID (Unit)"] = $"{locomotive.CarID} ({locomotive.DistributedPowerUnitId})";
                    this["Throttle"] = locomotive.DistributedPowerThrottleInfo();
                    FormattingOptions["Throttle"] = locomotive.DynamicBrakePercent >= 0 ? FormatOption.RegularYellow : null;

                    this["Reverser"] = $"{locomotive.Direction.GetLocalizedDescription()} {(locomotive.Flipped ? Simulator.Catalog.GetString("(flipped)") : "")}";
                    this["Remote"] = $"{(locomotive.IsLeadLocomotive() ? RemoteControlGroup.Unconnected.GetLocalizedDescription() : locomotive.RemoteControlGroup.GetLocalizedDescription())}";
                    this["Fuel"] = $"{FormatStrings.FormatFuelVolume(locomotive.DieselLevelL, simulator.MetricUnits, Simulator.Instance.Settings.MeasurementUnit == MeasurementUnit.UK)}";

                    this["Motive Force"] = $"{FormatStrings.FormatForce(locomotive.MotiveForceN, simulator.MetricUnits)}";
                    FormattingOptions["Motive Force"] = locomotive.CouplerOverloaded ? FormatOption.RegularYellow : null;
                    double effort = locomotive.DistributedPowerForceInfo();
                    this["Tractive Effort"] = $"{effort:F0} {(Simulator.Instance.Route.MilepostUnitsMetric ? "A" : "K")}";
                    FormattingOptions["Tractive Effort"] = effort < 0 ? FormatOption.RegularYellow : null;

                    DieselEngine engine = locomotive.DieselEngines[0];
                    this["Engine Status"] = engine.State.GetLocalizedDescription();
                    this["Power"] = FormatStrings.FormatPower(engine.CurrentDieselOutputPowerW, Simulator.Instance.MetricUnits, false, false);
                    this["Load"] = $"{engine.LoadPercent:F1} %";
                    this["RPM"] = $"{engine.RealRPM:F0} {FormatStrings.rpm}";
                    this["Flow"] = $"{FormatStrings.FormatFuelVolume(Frequency.Periodic.ToHours(engine.DieselFlowLps), Simulator.Instance.MetricUnits, Simulator.Instance.Settings.MeasurementUnit == MeasurementUnit.UK)}/{FormatStrings.h}";
                    this["Temperature"] = FormatStrings.FormatTemperature(engine.DieselTemperatureDeg, Simulator.Instance.MetricUnits);
                    this["Oil Pressure"] = FormatStrings.FormatPressure(engine.DieselOilPressurePSI, Pressure.Unit.PSI, locomotive.MainPressureUnit, true);

                    base.Update(gameTime);
                }
            }
        }
    } // class DieselLocomotive
}
