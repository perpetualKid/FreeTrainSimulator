// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

// Debug for Airbrake operation - Train Pipe Leak
//#define DEBUG_TRAIN_PIPE_LEAK

using System;
using System.Diagnostics;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{
    public class AirSinglePipe : MSTSBrakeSystem
    {
        private protected static string debugBrakeType = string.Empty;

        private protected float cylPressurePSI = 64;
        private protected float autoCylPressurePSI = 64;
        private protected float auxResPressurePSI = 64;
        private protected float emergResPressurePSI = 64;
        private protected float controlResPressurePSI = 64;
        private protected float fullServPressurePSI = 50;
        private protected float maxCylPressurePSI = 64;
        private protected float auxCylVolumeRatio = 2.5f;
        private protected float auxBrakeLineVolumeRatio;
        private protected float emergResVolumeM3 = 0.07f;
        private protected float retainerPressureThresholdPSI;
        private protected float releaseRatePSIpS = 1.86f;
        private protected float maxReleaseRatePSIpS = 1.86f;
        private protected float maxApplicationRatePSIpS = .9f;
        private protected float maxAuxilaryChargingRatePSIpS = 1.684f;
        private protected float brakeInsensitivityPSIpS;
        private protected float emergencyValveActuationRatePSIpS;
        private protected float emergResChargingRatePSIpS = 1.684f;
        private protected float emergAuxVolumeRatio = 1.4f;
        private protected string retainerDebugState = string.Empty;
        private protected bool mrpAuxResCharging;
        private protected float cylVolumeM3;

        private protected bool trainBrakePressureChanging;
        private protected bool brakePipePressureChanging;
        private protected float soundTriggerCounter;
        private protected float prevCylPressurePSI;
        private protected float prevBrakePipePressurePSI;
        private protected float prevBrakePipePressurePSI_sound;
        private protected bool bailOffOn;

        /// <summary>
        /// EP brake holding valve. Needs to be closed (Lap) in case of brake application or holding.
        /// For non-EP brake types must default to and remain in Release.
        /// </summary>
        private protected ValveState holdingValve = ValveState.Release;

        private protected ValveState tripleValveState = ValveState.Lap;

        public AirSinglePipe(TrainCar car) : base(car ?? throw new ArgumentNullException(nameof(car)))
        {
            // taking into account very short (fake) cars to prevent NaNs in brake line pressures
            BrakePipeVolumeM3 = (0.032f * 0.032f * (float)Math.PI / 4f) * Math.Max(5.0f, (1 + car.CarLengthM)); // Using DN32 (1-1/4") pipe
            debugBrakeType = "1P";

            // Force graduated releasable brakes. Workaround for MSTS with bugs preventing to set eng/wag files correctly for this.
            if (Simulator.Instance.Settings.GraduatedRelease)
                (car as MSTSWagon).BrakeValve = MSTSWagon.BrakeValveType.Distributor;

            if (Simulator.Instance.Settings.RetainersOnAllCars && car is not MSTSLocomotive)
                (car as MSTSWagon).RetainerPositions = 4;
        }

        public override void InitializeFrom(BrakeSystem source)
        {
            AirSinglePipe singlePipe = source as AirSinglePipe ?? throw new InvalidCastException(nameof(source));
            maxCylPressurePSI = singlePipe.maxCylPressurePSI;
            auxCylVolumeRatio = singlePipe.auxCylVolumeRatio;
            auxBrakeLineVolumeRatio = singlePipe.auxBrakeLineVolumeRatio;
            emergResVolumeM3 = singlePipe.emergResVolumeM3;
            BrakePipeVolumeM3 = singlePipe.BrakePipeVolumeM3;
            retainerPressureThresholdPSI = singlePipe.retainerPressureThresholdPSI;
            releaseRatePSIpS = singlePipe.releaseRatePSIpS;
            maxReleaseRatePSIpS = singlePipe.maxReleaseRatePSIpS;
            maxApplicationRatePSIpS = singlePipe.maxApplicationRatePSIpS;
            maxAuxilaryChargingRatePSIpS = singlePipe.maxAuxilaryChargingRatePSIpS;
            brakeInsensitivityPSIpS = singlePipe.brakeInsensitivityPSIpS;
            emergencyValveActuationRatePSIpS = singlePipe.emergencyValveActuationRatePSIpS;
            emergResChargingRatePSIpS = singlePipe.emergResChargingRatePSIpS;
            emergAuxVolumeRatio = singlePipe.emergAuxVolumeRatio;
            TwoPipes = singlePipe.TwoPipes;
            mrpAuxResCharging = singlePipe.mrpAuxResCharging;
            holdingValve = singlePipe.holdingValve;
        }

        // Get the brake BC & BP for EOT conditions
        public override string GetStatus(EnumArray<Pressure.Unit, BrakeSystemComponent> units)
        {
            string s = Simulator.Catalog.GetString($" BC {FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakeCylinder], true)}");
            s += Simulator.Catalog.GetString($" BP {FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakePipe], true)}");
            return s;
        }

        // Get Brake information for train
        public override string GetFullStatus(BrakeSystem lastCarBrakeSystem, EnumArray<Pressure.Unit, BrakeSystemComponent> units)
        {
            string s = Simulator.Catalog.GetString($" EQ {FormatStrings.FormatPressure(car.Train.BrakeSystem.EqualReservoirPressurePSIorInHg, Pressure.Unit.PSI, units[BrakeSystemComponent.EqualizingReservoir], true)}");
            s += Simulator.Catalog.GetString($" BC {FormatStrings.FormatPressure(car.Train.HUDWagonBrakeCylinderPSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakeCylinder], true)}");

            s += Simulator.Catalog.GetString($" BP {FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakePipe], true)}");
            if (lastCarBrakeSystem != null && lastCarBrakeSystem != this)
                s += Simulator.Catalog.GetString(" EOT ") + lastCarBrakeSystem.GetStatus(units);
            if (handbrakePercent > 0)
                s += Simulator.Catalog.GetString($" Handbrake {handbrakePercent:F0}%");
            return s;
        }

        public override float GetCylPressurePSI()
        {
            return cylPressurePSI;
        }

        public override float GetCylVolumeM3()
        {
            return cylVolumeM3;
        }

        public float FullServPressurePSI => fullServPressurePSI;

        public float MaxCylPressurePSI => maxCylPressurePSI;

        public float AuxCylVolumeRatio => auxCylVolumeRatio;

        public float MaxReleaseRatePSIpS => maxReleaseRatePSIpS;

        public float MaxApplicationRatePSIpS => maxApplicationRatePSIpS;

        public override float VacResPressurePSI => 0;

        public override float VacResVolume => 0;

        public override float VacBrakeCylNumber => 0;

        public override void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "wagon(brakecylinderpressureformaxbrakebrakeforce":
                    maxCylPressurePSI = autoCylPressurePSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null);
                    break;
                case "wagon(triplevalveratio":
                    auxCylVolumeRatio = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "wagon(brakedistributorreleaserate":
                case "wagon(maxreleaserate":
                    maxReleaseRatePSIpS = releaseRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;
                case "wagon(brakedistributorapplicationrate":
                case "wagon(maxapplicationrate":
                    maxApplicationRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;
                case "wagon(maxauxilarychargingrate":
                    maxAuxilaryChargingRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;
                case "wagon(emergencyreschargingrate":
                    emergResChargingRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;
                case "wagon(emergencyresvolumemultiplier":
                    emergAuxVolumeRatio = stf.ReadFloatBlock(STFReader.Units.None, null);
                    break;
                case "wagon(emergencyrescapacity":
                    emergResVolumeM3 = (float)Size.Volume.FromFt3(stf.ReadFloatBlock(STFReader.Units.VolumeDefaultFT3, null));
                    break;

                // OpenRails specific parameters
                case "wagon(brakepipevolume":
                    BrakePipeVolumeM3 = (float)Size.Volume.FromFt3(stf.ReadFloatBlock(STFReader.Units.VolumeDefaultFT3, null));
                    break;
                case "wagon(ortsbrakeinsensitivity":
                    brakeInsensitivityPSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;
                case "wagon(ortsemergencyvalveactuationrate":
                    emergencyValveActuationRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, 15f);
                    break;
                case "wagon(ortsmainrespipeauxrescharging":
                    mrpAuxResCharging = this is AirTwinPipe && stf.ReadBoolBlock(true);
                    break;
            }
        }

        public override ValueTask<BrakeSystemSaveState> Snapshot()
        {
            return ValueTask.FromResult(new BrakeSystemSaveState()
            {
                BrakeLine1Pressure = BrakeLine1PressurePSI,
                BrakeLine2Pressure = BrakeLine2PressurePSI,
                BrakeLine3Pressure = BrakeLine3PressurePSI,
                HandBrake = handbrakePercent,
                ReleaseRate = releaseRatePSIpS,
                RetainerPressureThreshold = retainerPressureThresholdPSI,
                AutoCylinderPressure = autoCylPressurePSI,
                AuxReservoirPressure = auxResPressurePSI,
                EmergencyReservoirPressure = emergResPressurePSI,
                ControlReservoirPressure = controlResPressurePSI,
                FullServicePressure = fullServPressurePSI,
                TripleValveState = tripleValveState,
                FrontBrakeHoseConnected = FrontBrakeHoseConnected,
                AngleCockAOpen = AngleCockAOpen,
                AngleCockBOpen = AngleCockBOpen,
                BleedOffValveOpen = BleedOffValveOpen,
                HoldingValveState = holdingValve,
                CylinderVolume = cylVolumeM3,
                BailOffOn = bailOffOn,
            });
        }

        public override ValueTask Restore(BrakeSystemSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            BrakeLine1PressurePSI = saveState.BrakeLine1Pressure;
            BrakeLine2PressurePSI = saveState.BrakeLine2Pressure;
            BrakeLine3PressurePSI = saveState.BrakeLine3Pressure;
            handbrakePercent = saveState.HandBrake;
            releaseRatePSIpS = saveState.ReleaseRate;
            retainerPressureThresholdPSI = saveState.RetainerPressureThreshold;
            autoCylPressurePSI = saveState.AutoCylinderPressure;
            auxResPressurePSI = saveState.AuxReservoirPressure;
            emergResPressurePSI = saveState.EmergencyReservoirPressure;
            controlResPressurePSI = saveState.ControlReservoirPressure;
            fullServPressurePSI = saveState.FullServicePressure;
            tripleValveState = saveState.TripleValveState;
            FrontBrakeHoseConnected = saveState.FrontBrakeHoseConnected;
            AngleCockAOpen = saveState.AngleCockAOpen;
            AngleCockBOpen = saveState.AngleCockBOpen;
            BleedOffValveOpen = saveState.BleedOffValveOpen;
            holdingValve = saveState.HoldingValveState;
            cylVolumeM3 = saveState.CylinderVolume;
            bailOffOn = saveState.BailOffOn;

            return ValueTask.CompletedTask;
        }

        public override void Initialize(bool handbrakeOn, float maxPressurePSI, float fullServPressurePSI, bool immediateRelease)
        {
            // reducing size of Emergency Reservoir for short (fake) cars
            if (Simulator.Instance.Settings.CorrectQuestionableBrakingParams && car.CarLengthM <= 1)
                emergResVolumeM3 = Math.Min(0.02f, emergResVolumeM3);

            if (Simulator.Instance.Settings.CorrectQuestionableBrakingParams && (car as MSTSWagon).BrakeValve == MSTSWagon.BrakeValveType.None)
            {
                (car as MSTSWagon).BrakeValve = MSTSWagon.BrakeValveType.TripleValve;
                Trace.TraceWarning("{0} does not define a brake valve, defaulting to a plain triple valve", (car as MSTSWagon).WagFilePath);
            }

            // In simple brake mode set emergency reservoir volume, override high volume values to allow faster brake release.
            if (Simulator.Instance.Settings.SimpleControlPhysics && emergResVolumeM3 > 2.0)
                emergResVolumeM3 = 0.7f;

            BrakeLine1PressurePSI = car.Train.BrakeSystem.EqualReservoirPressurePSIorInHg;
            BrakeLine2PressurePSI = car.Train.BrakeSystem.BrakeLine2Pressure;
            BrakeLine3PressurePSI = 0;
            if (maxPressurePSI > 0)
                controlResPressurePSI = maxPressurePSI;
            this.fullServPressurePSI = fullServPressurePSI;
            autoCylPressurePSI = immediateRelease ? 0 : Math.Min((maxPressurePSI - BrakeLine1PressurePSI) * auxCylVolumeRatio, maxCylPressurePSI);
            auxResPressurePSI = Math.Max(TwoPipes ? maxPressurePSI : maxPressurePSI - autoCylPressurePSI / AuxCylVolumeRatio, BrakeLine1PressurePSI);
            if ((car as MSTSWagon).EmergencyReservoirPresent)
                emergResPressurePSI = Math.Max(auxResPressurePSI, maxPressurePSI);
            tripleValveState = autoCylPressurePSI < 1 ? ValveState.Release : ValveState.Lap;
            holdingValve = ValveState.Release;
            HandbrakePercent = handbrakeOn ? 100 : 0;
            SetRetainer(RetainerSetting.Exhaust);
            MSTSLocomotive loco = car as MSTSLocomotive;
            if (loco != null)
            {
                loco.MainResPressurePSI = loco.MaxMainResPressurePSI;
            }

            if (emergResVolumeM3 > 0 && emergAuxVolumeRatio > 0 && BrakePipeVolumeM3 > 0)
                auxBrakeLineVolumeRatio = emergResVolumeM3 / emergAuxVolumeRatio / BrakePipeVolumeM3;
            else
                auxBrakeLineVolumeRatio = 3.1f;

            cylVolumeM3 = emergResVolumeM3 / emergAuxVolumeRatio / auxCylVolumeRatio;
        }

        /// <summary>
        /// Used when initial speed > 0
        /// </summary>
        public override void InitializeMoving()
        {
            Initialize(false, 0, fullServPressurePSI, true);
        }

        public override void LocoInitializeMoving() // starting conditions when starting speed > 0
        {
        }

        public void UpdateTripleValveState(double elapsedClockSeconds)
        {
            if ((car as MSTSWagon).BrakeValve == MSTSWagon.BrakeValveType.Distributor)
            {
                float targetPressurePSI = (controlResPressurePSI - BrakeLine1PressurePSI) * auxCylVolumeRatio;
                if (targetPressurePSI > autoCylPressurePSI && emergencyValveActuationRatePSIpS > 0 && (prevBrakePipePressurePSI - BrakeLine1PressurePSI) > Math.Max(elapsedClockSeconds, 0.0001f) * emergencyValveActuationRatePSIpS)
                    tripleValveState = ValveState.Emergency;
                else if (targetPressurePSI < autoCylPressurePSI - (tripleValveState != ValveState.Release ? 2.2f : 0f)
                    || targetPressurePSI < 2.2f) // The latter is a UIC regulation (0.15 bar)
                    tripleValveState = ValveState.Release;
                else if (!bailOffOn && tripleValveState != ValveState.Emergency && targetPressurePSI > autoCylPressurePSI + (tripleValveState != ValveState.Apply ? 2.2f : 0f))
                    tripleValveState = ValveState.Apply;
                else
                    tripleValveState = ValveState.Lap;
            }
            else
            {
                if (BrakeLine1PressurePSI < auxResPressurePSI - 1 && emergencyValveActuationRatePSIpS > 0 && (prevBrakePipePressurePSI - BrakeLine1PressurePSI) > Math.Max(elapsedClockSeconds, 0.0001f) * emergencyValveActuationRatePSIpS)
                    tripleValveState = ValveState.Emergency;
                else if (BrakeLine1PressurePSI > auxResPressurePSI + 1)
                    tripleValveState = ValveState.Release;
                else if (tripleValveState == ValveState.Emergency && BrakeLine1PressurePSI > auxResPressurePSI)
                    tripleValveState = ValveState.Release;
                else if (controlResPressurePSI > 70 && BrakeLine1PressurePSI > controlResPressurePSI * 0.97f) // UIC regulation: for 5 bar systems, release if > 4.85 bar
                    tripleValveState = ValveState.Release;
                else if (tripleValveState != ValveState.Emergency && BrakeLine1PressurePSI < auxResPressurePSI - 1)
                    tripleValveState = ValveState.Apply;
                else if (tripleValveState == ValveState.Apply && BrakeLine1PressurePSI >= auxResPressurePSI)
                    tripleValveState = ValveState.Lap;
            }
            prevBrakePipePressurePSI = BrakeLine1PressurePSI;
        }

        public override void Update(double elapsedClockSeconds)
        {
            float threshold = ((car as MSTSWagon).BrakeValve == MSTSWagon.BrakeValveType.Distributor) ? (controlResPressurePSI - BrakeLine1PressurePSI) * auxCylVolumeRatio : 0;

            if (BleedOffValveOpen)
            {
                if (auxResPressurePSI < 0.01f && autoCylPressurePSI < 0.01f && BrakeLine1PressurePSI < 0.01f && (emergResPressurePSI < 0.01f || !(car as MSTSWagon).EmergencyReservoirPresent))
                {
                    BleedOffValveOpen = false;
                }
                else
                {
                    auxResPressurePSI -= (float)elapsedClockSeconds * maxApplicationRatePSIpS;
                    if (auxResPressurePSI < 0)
                        auxResPressurePSI = 0;
                    autoCylPressurePSI -= (float)elapsedClockSeconds * maxReleaseRatePSIpS;
                    if (autoCylPressurePSI < 0)
                        autoCylPressurePSI = 0;
                    if ((car as MSTSWagon).EmergencyReservoirPresent)
                    {
                        emergResPressurePSI -= (float)elapsedClockSeconds * emergResChargingRatePSIpS;
                        if (emergResPressurePSI < 0)
                            emergResPressurePSI = 0;
                    }
                    tripleValveState = ValveState.Release;
                }
            }
            else
                UpdateTripleValveState(elapsedClockSeconds);

            // triple valve is set to charge the brake cylinder
            if ((tripleValveState == ValveState.Apply || tripleValveState == ValveState.Emergency) && !car.WheelBrakeSlideProtectionActive)
            {
                float dp = (float)elapsedClockSeconds * maxApplicationRatePSIpS;
                if (auxResPressurePSI - dp / auxCylVolumeRatio < autoCylPressurePSI + dp)
                    dp = (auxResPressurePSI - autoCylPressurePSI) * auxCylVolumeRatio / (1 + auxCylVolumeRatio);
                if (((car as MSTSWagon).BrakeValve == MSTSWagon.BrakeValveType.Distributor) && tripleValveState != ValveState.Emergency && dp > threshold - autoCylPressurePSI)
                    dp = threshold - autoCylPressurePSI;
                if (autoCylPressurePSI + dp > maxCylPressurePSI)
                    dp = maxCylPressurePSI - autoCylPressurePSI;
                if (BrakeLine1PressurePSI > auxResPressurePSI - dp / auxCylVolumeRatio && !BleedOffValveOpen)
                    dp = (auxResPressurePSI - BrakeLine1PressurePSI) * auxCylVolumeRatio;
                if (dp < 0)
                    dp = 0;

                auxResPressurePSI -= dp / auxCylVolumeRatio;
                autoCylPressurePSI += dp;

                if (tripleValveState == ValveState.Emergency && (car as MSTSWagon).EmergencyReservoirPresent)
                {
                    dp = (float)elapsedClockSeconds * maxApplicationRatePSIpS;
                    if (emergResPressurePSI - dp < auxResPressurePSI + dp * emergAuxVolumeRatio)
                        dp = (emergResPressurePSI - auxResPressurePSI) / (1 + emergAuxVolumeRatio);
                    emergResPressurePSI -= dp;
                    auxResPressurePSI += dp * emergAuxVolumeRatio;
                }
            }

            // triple valve set to release pressure in brake cylinder and EP valve set
            if (tripleValveState == ValveState.Release)
            {
                if ((car as MSTSWagon).EmergencyReservoirPresent)
                {
                    if (auxResPressurePSI < emergResPressurePSI && auxResPressurePSI < BrakeLine1PressurePSI)
                    {
                        float dp = (float)elapsedClockSeconds * emergResChargingRatePSIpS;
                        if (emergResPressurePSI - dp < auxResPressurePSI + dp * emergAuxVolumeRatio)
                            dp = (emergResPressurePSI - auxResPressurePSI) / (1 + emergAuxVolumeRatio);
                        if (BrakeLine1PressurePSI < auxResPressurePSI + dp * emergAuxVolumeRatio)
                            dp = (BrakeLine1PressurePSI - auxResPressurePSI) / emergAuxVolumeRatio;
                        emergResPressurePSI -= dp;
                        auxResPressurePSI += dp * emergAuxVolumeRatio;
                    }
                    if (auxResPressurePSI > emergResPressurePSI)
                    {
                        float dp = (float)elapsedClockSeconds * emergResChargingRatePSIpS;
                        if (emergResPressurePSI + dp > auxResPressurePSI - dp * emergAuxVolumeRatio)
                            dp = (auxResPressurePSI - emergResPressurePSI) / (1 + emergAuxVolumeRatio);
                        emergResPressurePSI += dp;
                        auxResPressurePSI -= dp * emergAuxVolumeRatio;
                    }
                }
                if (auxResPressurePSI < BrakeLine1PressurePSI && (!TwoPipes || !mrpAuxResCharging || ((car as MSTSWagon).BrakeValve != MSTSWagon.BrakeValveType.Distributor) || BrakeLine2PressurePSI < BrakeLine1PressurePSI) && !BleedOffValveOpen)
                {
                    float dp = (float)elapsedClockSeconds * maxAuxilaryChargingRatePSIpS; // Change in pressure for train brake pipe.
                    if (auxResPressurePSI + dp > BrakeLine1PressurePSI - dp * auxBrakeLineVolumeRatio)
                        dp = (BrakeLine1PressurePSI - auxResPressurePSI) / (1 + auxBrakeLineVolumeRatio);
                    auxResPressurePSI += dp;
                    BrakeLine1PressurePSI -= dp * auxBrakeLineVolumeRatio;  // Adjust the train brake pipe pressure
                }
                if (auxResPressurePSI > BrakeLine1PressurePSI) // Allow small flow from auxiliary reservoir to brake pipe so the triple valve is not sensible to small pressure variations when in release position
                {
                    float dp = (float)elapsedClockSeconds * brakeInsensitivityPSIpS;
                    if (auxResPressurePSI - dp < BrakeLine1PressurePSI + dp * auxBrakeLineVolumeRatio)
                        dp = (auxResPressurePSI - BrakeLine1PressurePSI) / (1 + auxBrakeLineVolumeRatio);
                    auxResPressurePSI -= dp;
                    BrakeLine1PressurePSI += dp * auxBrakeLineVolumeRatio;
                }
            }

            // Handle brake release: reduce cylinder pressure if all triple valve, EP holding valve and retainers allow so
            float minCylPressurePSI = Math.Max(threshold, retainerPressureThresholdPSI);
            if (tripleValveState == ValveState.Release && holdingValve == ValveState.Release && autoCylPressurePSI > minCylPressurePSI)
            {
                float dp = (float)elapsedClockSeconds * releaseRatePSIpS;
                if (autoCylPressurePSI - dp < minCylPressurePSI)
                    dp = autoCylPressurePSI - minCylPressurePSI;
                if (dp < 0)
                    dp = 0;
                autoCylPressurePSI -= dp;
            }

            // Charge Auxiliary reservoir for MRP
            if (TwoPipes
                && mrpAuxResCharging
                && ((car as MSTSWagon).BrakeValve == MSTSWagon.BrakeValveType.Distributor)
                && auxResPressurePSI < BrakeLine2PressurePSI
                && auxResPressurePSI < controlResPressurePSI
                && (BrakeLine2PressurePSI > BrakeLine1PressurePSI || tripleValveState != ValveState.Release) && !BleedOffValveOpen)
            {
                float dp = (float)elapsedClockSeconds * maxAuxilaryChargingRatePSIpS;
                if (auxResPressurePSI + dp > BrakeLine2PressurePSI - dp * auxBrakeLineVolumeRatio)
                    dp = (BrakeLine2PressurePSI - auxResPressurePSI) / (1 + auxBrakeLineVolumeRatio);
                auxResPressurePSI += dp;
                BrakeLine2PressurePSI -= dp * auxBrakeLineVolumeRatio;
            }

            if (car is MSTSLocomotive loco && loco.EngineType != EngineType.Control)  // TODO - Control cars ned to be linked to power suppy requirements.
            {
                //    if (Car is MSTSLocomotive loco && loco.LocomotivePowerSupply.MainPowerSupplyOn)
                if (loco.LocomotivePowerSupply.MainPowerSupplyOn)
                {
                    bailOffOn = false;
                    if ((loco.Train.LeadLocomotiveIndex >= 0 && ((MSTSLocomotive)loco.Train.Cars[loco.Train.LeadLocomotiveIndex]).BailOff) || loco.DynamicBrakeAutoBailOff && loco.Train.MUDynamicBrakePercent > 0 && loco.DynamicBrakeForceCurves == null)
                        bailOffOn = true;
                    else if (loco.DynamicBrakeAutoBailOff && loco.Train.MUDynamicBrakePercent > 0 && loco.DynamicBrakeForceCurves != null)
                    {
                        var dynforce = loco.DynamicBrakeForceCurves.Get(1.0f, loco.AbsSpeedMpS);  // max dynforce at that speed
                        if ((loco.MaxDynamicBrakeForceN == 0 && dynforce > 0) || dynforce > loco.MaxDynamicBrakeForceN * 0.6)
                            bailOffOn = true;
                    }
                    if (bailOffOn)
                        autoCylPressurePSI -= maxReleaseRatePSIpS * (float)elapsedClockSeconds;
                }
            }

            if (autoCylPressurePSI < 0)
                autoCylPressurePSI = 0;
            if (autoCylPressurePSI < BrakeLine3PressurePSI) // Brake Cylinder pressure will be the greater of engine brake pressure or train brake pressure
                cylPressurePSI = BrakeLine3PressurePSI;
            else
                cylPressurePSI = autoCylPressurePSI;

            // During braking wheelslide control is effected throughout the train by additional equipment on each vehicle. In the piping to each pair of brake cylinders are fitted electrically operated 
            // dump valves. When axle rotations which are sensed electrically, differ by a predetermined speed the dump valves are operated releasing brake cylinder pressure to both axles of the affected 
            // bogie.

            // Dump valve operation will cease when differences in axle rotations arewithin specified limits or the axle accelerates faster than a specified rate. The dump valve resets whenever the wheel
            // creep speed drops to normal. The dump valve will only operate continuously for a maximum period of seven seconds after which time it will be de-energised and the dump valve will not 
            // re-operate until the train has stopped or the throttle operated. 

            // Dump valve operation is prevented under the following conditions:-
            // (i) When the Power Controller is open.

            // (ii) When Brake Pipe Pressure has been reduced below 250 kPa (36.25psi). 

            if (car.WheelBrakeSlideProtectionFitted && car.Train.IsPlayerDriven)
            {
                // WSP dump valve active
                if ((car.BrakeSkidWarning || car.BrakeSkid) && cylPressurePSI > 0 && !car.WheelBrakeSlideProtectionDumpValveLockout && ((!car.WheelBrakeSlideProtectionLimitDisabled && BrakeLine1PressurePSI > 36.25) || car.WheelBrakeSlideProtectionLimitDisabled))
                {
                    car.WheelBrakeSlideProtectionActive = true;
                    autoCylPressurePSI -= (float)(elapsedClockSeconds * maxReleaseRatePSIpS);
                    cylPressurePSI = autoCylPressurePSI;
                    car.WheelBrakeSlideProtectionTimerS -= (float)elapsedClockSeconds;

                    // Lockout WSP dump valve if it is open for greater then 7 seconds continuously
                    if (car.WheelBrakeSlideProtectionTimerS <= 0)
                    {
                        car.WheelBrakeSlideProtectionDumpValveLockout = true;
                    }

                }
                else if (!car.WheelBrakeSlideProtectionDumpValveLockout)
                {
                    // WSP dump valve stops
                    car.WheelBrakeSlideProtectionActive = false;
                    car.WheelBrakeSlideProtectionTimerS = car.WheelBrakeSlideTimerResetValueS; // Reset WSP timer if 
                }

            }

            // Record HUD display values for brake cylinders depending upon whether they are wagons or locomotives/tenders (which are subject to their own engine brakes)   
            if (car.WagonType == WagonType.Engine || car.WagonType == WagonType.Tender)
            {
                car.Train.HUDLocomotiveBrakeCylinderPSI = cylPressurePSI;
                car.Train.HUDWagonBrakeCylinderPSI = car.Train.HUDLocomotiveBrakeCylinderPSI;  // Initially set Wagon value same as locomotive, will be overwritten if a wagon is attached
            }
            else
            {
                // Record the Brake Cylinder pressure in first wagon, as EOT is also captured elsewhere, and this will provide the two extremeties of the train
                // Identifies the first wagon based upon the previously identified UiD 
                if (car.UiD == car.Train.FirstCarUiD)
                {
                    car.Train.HUDWagonBrakeCylinderPSI = cylPressurePSI;
                }

            }

            // If wagons are not attached to the locomotive, then set wagon BC pressure to same as locomotive in the Train brake line
            if (!car.Train.WagonsAttached && (car.WagonType == WagonType.Engine || car.WagonType == WagonType.Tender))
            {
                car.Train.HUDWagonBrakeCylinderPSI = cylPressurePSI;
            }

            float f;
            if (!car.BrakesStuck)
            {
                f = car.MaxBrakeForceN * Math.Min(cylPressurePSI / maxCylPressurePSI, 1);
                if (f < car.MaxHandbrakeForceN * handbrakePercent / 100)
                    f = car.MaxHandbrakeForceN * handbrakePercent / 100;
            }
            else
                f = Math.Max(car.MaxBrakeForceN, car.MaxHandbrakeForceN / 2);
            car.SetBrakeForce(f);
            // sound trigger checking runs every half second, to avoid the problems caused by the jumping BrakeLine1PressurePSI value, and also saves cpu time :)
            if (soundTriggerCounter >= 0.5f)
            {
                soundTriggerCounter = 0f;
                if (Math.Abs(autoCylPressurePSI - prevCylPressurePSI) > 0.1f) //(AutoCylPressurePSI != prevCylPressurePSI)
                {
                    if (!trainBrakePressureChanging)
                    {
                        if (autoCylPressurePSI > prevCylPressurePSI)
                            car.SignalEvent(TrainEvent.TrainBrakePressureIncrease);
                        else
                            car.SignalEvent(TrainEvent.TrainBrakePressureDecrease);
                        trainBrakePressureChanging = !trainBrakePressureChanging;
                    }

                }
                else if (trainBrakePressureChanging)
                {
                    trainBrakePressureChanging = !trainBrakePressureChanging;
                    car.SignalEvent(TrainEvent.TrainBrakePressureStoppedChanging);
                }

                if (Math.Abs(BrakeLine1PressurePSI - prevBrakePipePressurePSI_sound) > 0.1f /*BrakeLine1PressurePSI > prevBrakePipePressurePSI*/)
                {
                    if (!brakePipePressureChanging)
                    {
                        if (BrakeLine1PressurePSI > prevBrakePipePressurePSI_sound)
                            car.SignalEvent(TrainEvent.BrakePipePressureIncrease);
                        else
                            car.SignalEvent(TrainEvent.BrakePipePressureDecrease);
                        brakePipePressureChanging = !brakePipePressureChanging;
                    }

                }
                else if (brakePipePressureChanging)
                {
                    brakePipePressureChanging = !brakePipePressureChanging;
                    car.SignalEvent(TrainEvent.BrakePipePressureStoppedChanging);
                }
                prevCylPressurePSI = autoCylPressurePSI;
                prevBrakePipePressurePSI_sound = BrakeLine1PressurePSI;

                var lead = car as MSTSLocomotive;

                if (lead != null && car.WagonType == WagonType.Engine)
                {
                    if (lead.TrainBrakeController.TrainBrakeControllerState == ControllerState.Overcharge && !lead.BrakeOverchargeSoundOn)
                    {
                        car.SignalEvent(TrainEvent.OverchargeBrakingOn);
                        lead.BrakeOverchargeSoundOn = true;
                    }
                    else if (lead.TrainBrakeController.TrainBrakeControllerState != ControllerState.Overcharge && lead.BrakeOverchargeSoundOn)
                    {
                        car.SignalEvent(TrainEvent.OverchargeBrakingOff);
                        lead.BrakeOverchargeSoundOn = false;
                    }
                }

            }
            soundTriggerCounter = soundTriggerCounter + (float)elapsedClockSeconds;
            brakeInfo.Update(null);
        }

        public override void PropagateBrakePressure(double elapsedClockSeconds)
        {
            PropagateBrakeLinePressures(elapsedClockSeconds, car, TwoPipes);
        }

        private static void PropagateBrakeLinePressures(double elapsedClockSeconds, TrainCar trainCar, bool twoPipes)
        {
            var train = trainCar.Train;
            var lead = trainCar as MSTSLocomotive;
            (int first, int last) = train.FindLeadLocomotives();

            // Propagate brake line (1) data if pressure gradient disabled
            if (lead != null && lead.BrakePipeChargingRatePSIorInHgpS >= 1000)
            {   // pressure gradient disabled
                if (lead.BrakeSystem.BrakeLine1PressurePSI < train.BrakeSystem.EqualReservoirPressurePSIorInHg)
                {
                    var dp1 = train.BrakeSystem.EqualReservoirPressurePSIorInHg - lead.BrakeSystem.BrakeLine1PressurePSI;
                    lead.MainResPressurePSI -= dp1 * lead.BrakeSystem.BrakePipeVolumeM3 / lead.MainResVolumeM3;
                }
                foreach (TrainCar car in train.Cars)
                {
                    if (car.BrakeSystem.BrakeLine1PressurePSI >= 0)
                        car.BrakeSystem.BrakeLine1PressurePSI = train.BrakeSystem.EqualReservoirPressurePSIorInHg;
                    if (car.BrakeSystem.TwoPipes)
                        car.BrakeSystem.BrakeLine2PressurePSI = Math.Min(lead.MainResPressurePSI, lead.MaximumMainReservoirPipePressurePSI);
                }
            }
            else
            {   // approximate pressure gradient in train pipe line1
                var brakePipeTimeFactorS = lead == null ? 0.0015f : lead.BrakePipeTimeFactorS;
                int nSteps = (int)(elapsedClockSeconds / brakePipeTimeFactorS + 1);
                float trainPipeTimeVariationS = (float)(elapsedClockSeconds / nSteps);
                float trainPipeLeakLossPSI = lead == null ? 0.0f : (trainPipeTimeVariationS * lead.TrainBrakePipeLeakPSIorInHgpS);
                float serviceTimeFactor = lead != null ? lead.TrainBrakeController != null && lead.TrainBrakeController.EmergencyBraking ? lead.BrakeEmergencyTimeFactorPSIpS : lead.BrakeServiceTimeFactorPSIpS : 0;
                for (int i = 0; i < nSteps; i++)
                {
                    if (lead != null)
                    {
                        // Allow for leaking train air brakepipe
                        if (lead.BrakeSystem.BrakeLine1PressurePSI - trainPipeLeakLossPSI > 0 && lead.TrainBrakePipeLeakPSIorInHgpS != 0) // if train brake pipe has pressure in it, ensure result will not be negative if loss is subtracted
                        {
                            lead.BrakeSystem.BrakeLine1PressurePSI -= trainPipeLeakLossPSI;
                        }

                        if (lead.TrainBrakeController.TrainBrakeControllerState != ControllerState.Neutral)
                        {
                            // Charge train brake pipe - adjust main reservoir pressure, and lead brake pressure line to maintain brake pipe equal to equalising resevoir pressure - release brakes
                            if (lead.BrakeSystem.BrakeLine1PressurePSI < train.BrakeSystem.EqualReservoirPressurePSIorInHg)
                            {
                                // Calculate change in brake pipe pressure between equalising reservoir and lead brake pipe
                                float chargingRatePSIpS = lead.BrakePipeChargingRatePSIorInHgpS;
                                if (lead.TrainBrakeController.TrainBrakeControllerState == ControllerState.FullQuickRelease || lead.TrainBrakeController.TrainBrakeControllerState == ControllerState.Overcharge)
                                {
                                    chargingRatePSIpS = lead.BrakePipeQuickChargingRatePSIpS;
                                }
                                float PressureDiffEqualToPipePSI = trainPipeTimeVariationS * chargingRatePSIpS; // default condition - if EQ Res is higher then Brake Pipe Pressure

                                if (lead.BrakeSystem.BrakeLine1PressurePSI + PressureDiffEqualToPipePSI > train.BrakeSystem.EqualReservoirPressurePSIorInHg)
                                    PressureDiffEqualToPipePSI = train.BrakeSystem.EqualReservoirPressurePSIorInHg - lead.BrakeSystem.BrakeLine1PressurePSI;

                                if (lead.BrakeSystem.BrakeLine1PressurePSI + PressureDiffEqualToPipePSI > lead.MainResPressurePSI)
                                    PressureDiffEqualToPipePSI = lead.MainResPressurePSI - lead.BrakeSystem.BrakeLine1PressurePSI;

                                if (PressureDiffEqualToPipePSI < 0)
                                    PressureDiffEqualToPipePSI = 0;

                                // Adjust brake pipe pressure based upon pressure differential
                                if (lead.TrainBrakeController.TrainBrakeControllerState != ControllerState.Lap) // in LAP psoition brake pipe is isolated, and thus brake pipe pressure decreases, but reservoir remains at same pressure
                                {
                                    lead.BrakeSystem.BrakeLine1PressurePSI += PressureDiffEqualToPipePSI;
                                    lead.MainResPressurePSI -= PressureDiffEqualToPipePSI * lead.BrakeSystem.BrakePipeVolumeM3 / lead.MainResVolumeM3;
                                }
                            }
                            // reduce pressure in lead brake line if brake pipe pressure is above equalising pressure - apply brakes
                            else if (lead.BrakeSystem.BrakeLine1PressurePSI > train.BrakeSystem.EqualReservoirPressurePSIorInHg)
                            {
                                float serviceVariationFactor = Math.Min(trainPipeTimeVariationS / serviceTimeFactor, 0.95f);
                                float pressureDiffPSI = serviceVariationFactor * lead.BrakeSystem.BrakeLine1PressurePSI;
                                if (lead.BrakeSystem.BrakeLine1PressurePSI - pressureDiffPSI > train.BrakeSystem.EqualReservoirPressurePSIorInHg)
                                    pressureDiffPSI = lead.BrakeSystem.BrakeLine1PressurePSI - train.BrakeSystem.EqualReservoirPressurePSIorInHg;
                                lead.BrakeSystem.BrakeLine1PressurePSI -= pressureDiffPSI;
                            }
                        }
                    }

                    // Propagate air pipe pressure along the train (brake pipe and main reservoir pipe)
#if DEBUG_TRAIN_PIPE_LEAK

                    Trace.TraceInformation("======================================= Train Pipe Leak (AirSinglePipe) ===============================================");
                    Trace.TraceInformation("Before:  CarID {0}  TrainPipeLeak {1} Lead BrakePipe Pressure {2}", trainCar.CarID, lead.TrainBrakePipeLeakPSIpS, lead.BrakeSystem.BrakeLine1PressurePSI);
                    Trace.TraceInformation("Brake State {0}", lead.TrainBrakeController.TrainBrakeControllerState);
                    Trace.TraceInformation("Main Resevoir {0} Compressor running {1}", lead.MainResPressurePSI, lead.CompressorIsOn);

#endif
                    train.BrakeSystem.TotalTrainBrakePipeVolume = 0.0f; // initialise train brake pipe volume
                    for (int carIndex = 0; carIndex < train.Cars.Count; carIndex++)
                    {
                        TrainCar car = train.Cars[carIndex];
                        TrainCar nextCar = carIndex < train.Cars.Count - 1 ? train.Cars[carIndex + 1] : null;
                        TrainCar prevCar = carIndex > 0 ? train.Cars[carIndex - 1] : null;
                        train.BrakeSystem.TotalTrainBrakePipeVolume += car.BrakeSystem.BrakePipeVolumeM3; // Calculate total brake pipe volume of train

                        if (prevCar != null && car.BrakeSystem.FrontBrakeHoseConnected && car.BrakeSystem.AngleCockAOpen && prevCar.BrakeSystem.AngleCockBOpen)
                        {
                            // Brake pipe
                            {
                                float pressureDiffPSI = car.BrakeSystem.BrakeLine1PressurePSI - prevCar.BrakeSystem.BrakeLine1PressurePSI;
                                // Based on the principle of pressure equalization between adjacent cars
                                // First, we define a variable storing the pressure diff between cars, but limited to a maximum flow rate depending on pipe characteristics
                                // The sign in the equation determines the direction of air flow.
                                float trainPipePressureDiffPropagationPSI = pressureDiffPSI * Math.Min(trainPipeTimeVariationS / brakePipeTimeFactorS, 1);

                                // Air flows from high pressure to low pressure, until pressure is equal in both cars.
                                // Brake pipe volumes of both cars are taken into account, so pressure increase/decrease is proportional to relative volumes.
                                // If TrainPipePressureDiffPropagationPSI equals to p1-p0 the equalization is achieved in one step.
                                car.BrakeSystem.BrakeLine1PressurePSI -= trainPipePressureDiffPropagationPSI * prevCar.BrakeSystem.BrakePipeVolumeM3 / (prevCar.BrakeSystem.BrakePipeVolumeM3 + car.BrakeSystem.BrakePipeVolumeM3);
                                prevCar.BrakeSystem.BrakeLine1PressurePSI += trainPipePressureDiffPropagationPSI * car.BrakeSystem.BrakePipeVolumeM3 / (prevCar.BrakeSystem.BrakePipeVolumeM3 + car.BrakeSystem.BrakePipeVolumeM3);
                            }
                            // Main reservoir pipe
                            if (prevCar.BrakeSystem.TwoPipes && car.BrakeSystem.TwoPipes)
                            {
                                float pressureDiffPSI = car.BrakeSystem.BrakeLine2PressurePSI - prevCar.BrakeSystem.BrakeLine2PressurePSI;
                                float trainPipePressureDiffPropagationPSI = pressureDiffPSI * Math.Min(trainPipeTimeVariationS / brakePipeTimeFactorS, 1);
                                car.BrakeSystem.BrakeLine2PressurePSI -= trainPipePressureDiffPropagationPSI * prevCar.BrakeSystem.BrakePipeVolumeM3 / (prevCar.BrakeSystem.BrakePipeVolumeM3 + car.BrakeSystem.BrakePipeVolumeM3);
                                prevCar.BrakeSystem.BrakeLine2PressurePSI += trainPipePressureDiffPropagationPSI * car.BrakeSystem.BrakePipeVolumeM3 / (prevCar.BrakeSystem.BrakePipeVolumeM3 + car.BrakeSystem.BrakePipeVolumeM3);
                            }
                        }
                        // Empty the brake pipe if the brake hose is not connected and angle cocks are open
                        if (!car.BrakeSystem.FrontBrakeHoseConnected && car.BrakeSystem.AngleCockAOpen)
                        {
                            car.BrakeSystem.BrakeLine1PressurePSI = Math.Max(car.BrakeSystem.BrakeLine1PressurePSI * (1 - trainPipeTimeVariationS / brakePipeTimeFactorS), 0);
                        }
                        if ((nextCar == null || !nextCar.BrakeSystem.FrontBrakeHoseConnected) && car.BrakeSystem.AngleCockBOpen)
                        {
                            car.BrakeSystem.BrakeLine1PressurePSI = Math.Max(car.BrakeSystem.BrakeLine1PressurePSI * (1 - trainPipeTimeVariationS / brakePipeTimeFactorS), 0);
                        }
                    }
#if DEBUG_TRAIN_PIPE_LEAK
                    Trace.TraceInformation("After: Lead Brake Pressure {0}", lead.BrakeSystem.BrakeLine1PressurePSI);
#endif
                }
            }

            // Join main reservoirs of adjacent locomotives
            if (first != -1 && last != -1)
            {
                float sumv = 0;
                float sumpv = 0;
                for (int i = first; i <= last; i++)
                {
                    if (train.Cars[i] is MSTSLocomotive loco)
                    {
                        sumv += loco.MainResVolumeM3;
                        sumpv += loco.MainResVolumeM3 * loco.MainResPressurePSI;
                    }
                }
                float totalReservoirPressurePSI = sumpv / sumv;
                for (int i = first; i <= last; i++)
                {
                    if (train.Cars[i] is MSTSLocomotive loco)
                    {
                        loco.MainResPressurePSI = totalReservoirPressurePSI;
                    }
                }
            }
            // Equalize main reservoir with train pipe for every locomotive
            foreach (TrainCar car in train.Cars)
            {
                if (car is MSTSLocomotive loco && car.BrakeSystem.TwoPipes)
                {
                    float volumeRatio = loco.BrakeSystem.BrakePipeVolumeM3 / loco.MainResVolumeM3;
                    float dp = Math.Min((loco.MainResPressurePSI - loco.BrakeSystem.BrakeLine2PressurePSI) / (1 + volumeRatio), loco.MaximumMainReservoirPipePressurePSI - loco.BrakeSystem.BrakeLine2PressurePSI);
                    loco.MainResPressurePSI -= dp * volumeRatio;
                    loco.BrakeSystem.BrakeLine2PressurePSI += dp;
                    if (loco.MainResPressurePSI < 0)
                        loco.MainResPressurePSI = 0;
                    if (loco.BrakeSystem.BrakeLine2PressurePSI < 0)
                        loco.BrakeSystem.BrakeLine2PressurePSI = 0;
                }
            }

            // Propagate engine brake pipe (3) data
            for (int i = 0; i < train.Cars.Count; i++)
            {
                BrakeSystem brakeSystem = train.Cars[i].BrakeSystem;
                // Collect and propagate engine brake pipe (3) data
                // This appears to be calculating the engine brake cylinder pressure???
                if (i < first || i > last)
                {
                    brakeSystem.BrakeLine3PressurePSI = 0;
                }
                else
                {
                    if (lead != null)
                    {
                        float p = brakeSystem.BrakeLine3PressurePSI;
                        if (p > 1000)
                            p -= 1000;
                        var prevState = lead.EngineBrakeState;
                        if (p < train.BrakeSystem.BrakeLine3Pressure && p < lead.MainResPressurePSI)  // Apply the engine brake as the pressure decreases
                        {
                            float dp = (float)elapsedClockSeconds * lead.EngineBrakeApplyRatePSIpS / (last - first + 1);
                            if (p + dp > train.BrakeSystem.BrakeLine3Pressure)
                                dp = train.BrakeSystem.BrakeLine3Pressure - p;
                            if (train.Cars[i] is MSTSLocomotive loco) // If this is a locomotive, drain air from main reservoir
                            {
                                float volumeRatio = brakeSystem.GetCylVolumeM3() / loco.MainResVolumeM3;
                                if (loco.MainResPressurePSI - dp * volumeRatio < p + dp)
                                {
                                    dp = (loco.MainResPressurePSI - p) / (1 + volumeRatio);
                                }
                                if (dp < 0)
                                    dp = 0;
                                loco.MainResPressurePSI -= dp * volumeRatio;
                            }
                            else // Otherwise, drain from train pipe
                            {
                                float volumeRatio = brakeSystem.GetCylVolumeM3() / brakeSystem.BrakePipeVolumeM3;
                                if (brakeSystem.BrakeLine2PressurePSI - dp * volumeRatio < p + dp)
                                {
                                    dp = (brakeSystem.BrakeLine2PressurePSI - p) / (1 + volumeRatio);
                                }
                                if (dp < 0)
                                    dp = 0;
                                brakeSystem.BrakeLine2PressurePSI -= dp * volumeRatio;
                            }
                            p += dp;
                            lead.EngineBrakeState = ValveState.Apply;
                        }
                        else if (p > train.BrakeSystem.BrakeLine3Pressure)  // Release the engine brake as the pressure increases in the brake cylinder
                        {
                            float dp = (float)elapsedClockSeconds * lead.EngineBrakeReleaseRatePSIpS / (last - first + 1);
                            if (p - dp < train.BrakeSystem.BrakeLine3Pressure)
                                dp = p - train.BrakeSystem.BrakeLine3Pressure;
                            p -= dp;
                            lead.EngineBrakeState = ValveState.Release;
                        }
                        else  // Engine brake does not change
                            lead.EngineBrakeState = ValveState.Lap;
                        if (lead.EngineBrakeState != prevState)
                            switch (lead.EngineBrakeState)
                            {
                                case ValveState.Release:
                                    lead.SignalEvent(TrainEvent.EngineBrakePressureIncrease);
                                    break;
                                case ValveState.Apply:
                                    lead.SignalEvent(TrainEvent.EngineBrakePressureDecrease);
                                    break;
                                case ValveState.Lap:
                                    lead.SignalEvent(TrainEvent.EngineBrakePressureStoppedChanging);
                                    break;
                            }
                        brakeSystem.BrakeLine3PressurePSI = p;
                    }
                }
            }
        }

        public override float InternalPressure(float realPressure)
        {
            return realPressure;
        }

        public override void SetRetainer(RetainerSetting setting)
        {
            switch (setting)
            {
                case RetainerSetting.Exhaust:
                    retainerPressureThresholdPSI = 0;
                    releaseRatePSIpS = maxReleaseRatePSIpS;
                    retainerDebugState = "EX";
                    break;
                case RetainerSetting.HighPressure:
                    if ((car as MSTSWagon).RetainerPositions > 0)
                    {
                        retainerPressureThresholdPSI = 20;
                        releaseRatePSIpS = (50 - 20) / 90f;
                        retainerDebugState = "HP";
                    }
                    break;
                case RetainerSetting.LowPressure:
                    if ((car as MSTSWagon).RetainerPositions > 3)
                    {
                        retainerPressureThresholdPSI = 10;
                        releaseRatePSIpS = (50 - 10) / 60f;
                        retainerDebugState = "LP";
                    }
                    else if ((car as MSTSWagon).RetainerPositions > 0)
                    {
                        retainerPressureThresholdPSI = 20;
                        releaseRatePSIpS = (50 - 20) / 90f;
                        retainerDebugState = "HP";
                    }
                    break;
                case RetainerSetting.SlowDirect:
                    retainerPressureThresholdPSI = 0;
                    releaseRatePSIpS = (50 - 10) / 86f;
                    retainerDebugState = "SD";
                    break;
            }
        }

        public override void AISetPercent(float percent)
        {
            if (percent < 0)
                percent = 0;
            if (percent > 100)
                percent = 100;
            car.Train.BrakeSystem.EqualReservoirPressurePSIorInHg = 90 - (90 - fullServPressurePSI) * percent / 100;
        }

        // used when switching from autopilot to player driven mode, to move from default values to values specific for the trainset
        public void NormalizePressures(float maxPressurePSI)
        {
            if (auxResPressurePSI > maxPressurePSI)
                auxResPressurePSI = maxPressurePSI;
            if (BrakeLine1PressurePSI > maxPressurePSI)
                BrakeLine1PressurePSI = maxPressurePSI;
            if (emergResPressurePSI > maxPressurePSI)
                emergResPressurePSI = maxPressurePSI;
            if (controlResPressurePSI > maxPressurePSI)
                controlResPressurePSI = maxPressurePSI;
        }

        public override bool IsBraking()
        {
            return autoCylPressurePSI > maxCylPressurePSI * 0.3;
        }

        //Corrects MaxCylPressure (e.g 380.eng) when too high
        public override void CorrectMaxCylPressurePSI(MSTSLocomotive loco)
        {
            if (maxCylPressurePSI > loco.TrainBrakeController.MaxPressurePSI - maxCylPressurePSI / auxCylVolumeRatio)
            {
                maxCylPressurePSI = loco.TrainBrakeController.MaxPressurePSI * auxCylVolumeRatio / (1 + auxCylVolumeRatio);
            }
        }

        private protected override void UpdateBrakeStatus()
        {
            EnumArray<Pressure.Unit, BrakeSystemComponent> pressureUnits = Simulator.Instance.PlayerLocomotive.BrakeSystemPressureUnits;
            brakeInfo["Car"] = car.CarID;
            brakeInfo["BrakeType"] = "1P";
            brakeInfo["Handbrake"] = handbrakePercent > 0 ? $"{handbrakePercent:F0}%" : null;
            brakeInfo["BrakehoseConnected"] = FrontBrakeHoseConnected ? "I" : "T";
            brakeInfo["AngleCock"] = $"A{(AngleCockAOpen ? "+" : "-")} B{(AngleCockBOpen ? "+" : "-")}";
            brakeInfo["BleedOff"] = BleedOffValveOpen ? "Open" : string.Empty;

            if (car.Train.LeadLocomotive?.BrakeSystem is SMEBrakeSystem)
            {
                // Set values for SME type brake
                brakeInfo["SrvPipe"] = FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.BrakePipe], true);
                brakeInfo["StrPipe"] = TwoPipes ? FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.MainPipe], true) : null;
            }
            else
            {
                brakeInfo["MainReservoirPipe"] = TwoPipes ? FormatStrings.FormatPressure(BrakeLine2PressurePSI, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.MainPipe], true) : null;
            }
            brakeInfo["AuxReservoir"] = FormatStrings.FormatPressure(auxResPressurePSI, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.AuxiliaryReservoir], true);
            brakeInfo["EmergencyReservoir"] = (car as MSTSWagon).EmergencyReservoirPresent ? FormatStrings.FormatPressure(emergResPressurePSI, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.EmergencyReservoir], true) : null;
            brakeInfo["RetainerValve"] = (car as MSTSWagon).RetainerPositions == 0 ? null : retainerDebugState;
            brakeInfo["TripleValve"] = tripleValveState.GetLocalizedDescription();

            brakeInfo["EQ"] = FormatStrings.FormatPressure(car.Train.BrakeSystem.EqualReservoirPressurePSIorInHg, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.EqualizingReservoir], true);
            brakeInfo["BC"] = FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.BrakeCylinder], true);
            brakeInfo["BP"] = FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.BrakePipe], true);
            brakeInfo["Status"] = $"BC {brakeInfo["BC"]} BP {brakeInfo["BP"]}";
            brakeInfo["StatusShort"] = $"BC{FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.BrakeCylinder], false)} BP{FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, pressureUnits[BrakeSystemComponent.BrakePipe], false)}";
        }
    }
}
