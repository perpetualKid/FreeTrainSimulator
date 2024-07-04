// COPYRIGHT 2013, 2014 by the Open Rails project.
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
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Calc;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerTransmissions
{
    public class MSTSGearBoxParams
    {
        public int GearBoxNumberOfGears = 1;
        public bool ReverseGearBoxIndication;
        public int GearBoxDirectDriveGear = 1;
        public bool FreeWheelFitted;
        public GearBoxType GearBoxType = GearBoxType.Unknown;
        // GearboxType ( A ) - power is continuous during gear changes (and throttle does not need to be adjusted)
        // GearboxType ( B ) - power is interrupted during gear changes - but the throttle does not need to be adjusted when changing gear
        // GearboxType ( C ) - power is interrupted and if GearboxOperation is Manual throttle must be closed when changing gear
        // GearboxType ( D ) - power is interrupted and if GearboxOperation is Manual throttle must be closed when changing gear, clutch will remain engaged, and can stall engine

        public ClutchType ClutchType = ClutchType.Unknown;


        public GearBoxOperation GearBoxOperation = GearBoxOperation.Manual;
        public GearBoxEngineBraking GearBoxEngineBraking = GearBoxEngineBraking.None;
        public List<float> GearBoxMaxSpeedForGearsMpS = new List<float>();
        public List<float> GearBoxChangeUpSpeedRpM = new List<float>();
        public List<float> GearBoxChangeDownSpeedRpM = new List<float>();
        public List<float> GearBoxMaxTractiveForceForGearsN = new List<float>();
        public List<float> GearBoxTractiveForceAtSpeedN = new List<float>();
        public float GearBoxOverspeedPercentageForFailure = 150f;
        public float GearBoxBackLoadForceN = 1000;
        public float GearBoxCoastingForceN = 500;
        public float GearBoxUpGearProportion = 0.85f;
        public float GearBoxDownGearProportion = 0.35f;
        private int initLevel;

        public bool MaxTEFound;

        public bool IsInitialized { get { return initLevel >= 3; } }
        public bool AtLeastOneParamFound { get { return initLevel >= 1; } }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            string temp = "";
            switch (lowercasetoken)
            {
                case "engine(gearboxnumberofgears":
                    GearBoxNumberOfGears = stf.ReadIntBlock(1);
                    initLevel++;
                    break;
                case "engine(ortsreversegearboxindication":
                    int tempIndication = stf.ReadIntBlock(1);
                    if (tempIndication == 1)
                    {
                        ReverseGearBoxIndication = true;
                    }
                    break;
                case "engine(gearboxdirectdrivegear":
                    GearBoxDirectDriveGear = stf.ReadIntBlock(1);
                    break;
                case "engine(ortsgearboxfreewheel":
                    var freeWheel = stf.ReadIntBlock(null);
                    if (freeWheel == 1)
                    {
                        FreeWheelFitted = true;
                    }
                    break;
                case "engine(ortsgearboxtype":
                    stf.MustMatch("(");
                    var gearType = stf.ReadString();
                    if (!EnumExtension.GetValue(gearType, out GearBoxType))
                        STFException.TraceWarning(stf, "Assumed unknown gear box type " + gearType);
                    break;
                case "engine(ortsmainclutchtype":
                    stf.MustMatch("(");
                    var clutchType = stf.ReadString();
                    if (!EnumExtension.GetValue(clutchType, out ClutchType))
                        STFException.TraceWarning(stf, "Assumed unknown main clutch type " + clutchType);
                    break;
                case "engine(gearboxoperation":
                    stf.MustMatch("(");
                    var gearOperation = stf.ReadString();
                    if (!EnumExtension.GetValue(gearOperation, out GearBoxOperation))
                        STFException.TraceWarning(stf, "Assumed unknown gear box operation type " + gearOperation);
                    initLevel++;
                    break;
                case "engine(gearboxenginebraking":
                    stf.MustMatch("(");
                    var engineBraking = stf.ReadString();
                    if (!EnumExtension.GetValue(engineBraking, out GearBoxEngineBraking))
                        STFException.TraceWarning(stf, "Assumed unknown gear box engine braking type " + engineBraking);
                    break;
                case "engine(gearboxmaxspeedforgears":
                    temp = stf.ReadItem();
                    if (temp == ")")
                    {
                        stf.StepBackOneItem();
                    }
                    if (temp == "(")
                    {
                        GearBoxMaxSpeedForGearsMpS.Clear();
                        for (int i = 0; i < GearBoxNumberOfGears; i++)
                        {
                            GearBoxMaxSpeedForGearsMpS.Add(stf.ReadFloat(STFReader.Units.SpeedDefaultMPH, 10.0f));
                        }
                        stf.SkipRestOfBlock();
                        initLevel++;
                    }
                    break;
                // gearboxmaxtractiveforceforgears purely retained for legacy reasons
                case "engine(gearboxmaxtractiveforceforgears":
                    temp = stf.ReadItem();
                    if (temp == ")")
                    {
                        stf.StepBackOneItem();
                    }
                    if (temp == "(")
                    {
                        GearBoxMaxTractiveForceForGearsN.Clear();
                        for (int i = 0; i < GearBoxNumberOfGears; i++)
                            GearBoxMaxTractiveForceForGearsN.Add(stf.ReadFloat(STFReader.Units.Force, 10000.0f));
                        stf.SkipRestOfBlock();
                        initLevel++;
                    }
                    break;
                case "engine(ortsgearboxtractiveforceatspeed":
                    MaxTEFound = true;
                    temp = stf.ReadItem();
                    if (temp == ")")
                    {
                        stf.StepBackOneItem();
                    }
                    if (temp == "(")
                    {
                        GearBoxTractiveForceAtSpeedN.Clear();
                        for (int i = 0; i < GearBoxNumberOfGears; i++)
                        {
                            GearBoxTractiveForceAtSpeedN.Add(stf.ReadFloat(STFReader.Units.Force, 0f));
                        }
                        stf.SkipRestOfBlock();
                        initLevel++;
                    }
                    break;
                case "engine(gearboxoverspeedpercentageforfailure":
                    GearBoxOverspeedPercentageForFailure = stf.ReadFloatBlock(STFReader.Units.None, 150f);
                    break; // initLevel++; break;
                case "engine(gearboxbackloadforce":
                    GearBoxBackLoadForceN = stf.ReadFloatBlock(STFReader.Units.Force, 0f);
                    break;
                case "engine(gearboxcoastingforce":
                    GearBoxCoastingForceN = stf.ReadFloatBlock(STFReader.Units.Force, 0f);
                    break;
                case "engine(gearboxupgearproportion":
                    GearBoxUpGearProportion = stf.ReadFloatBlock(STFReader.Units.None, 0.85f);
                    break; // initLevel++; break;
                case "engine(gearboxdowngearproportion":
                    GearBoxDownGearProportion = stf.ReadFloatBlock(STFReader.Units.None, 0.25f);
                    break; // initLevel++; break;

                default:
                    break;
            }
        }

        public void Copy(MSTSGearBoxParams copy)
        {
            GearBoxNumberOfGears = copy.GearBoxNumberOfGears;
            ReverseGearBoxIndication = copy.ReverseGearBoxIndication;
            GearBoxDirectDriveGear = copy.GearBoxDirectDriveGear;
            GearBoxType = copy.GearBoxType;
            MaxTEFound = copy.MaxTEFound;
            ClutchType = copy.ClutchType;
            GearBoxOperation = copy.GearBoxOperation;
            GearBoxEngineBraking = copy.GearBoxEngineBraking;
            GearBoxMaxSpeedForGearsMpS = new List<float>(copy.GearBoxMaxSpeedForGearsMpS);
            GearBoxChangeUpSpeedRpM = new List<float>(copy.GearBoxChangeUpSpeedRpM);
            GearBoxChangeDownSpeedRpM = new List<float>(copy.GearBoxChangeDownSpeedRpM);
            GearBoxMaxTractiveForceForGearsN = new List<float>(copy.GearBoxMaxTractiveForceForGearsN);
            GearBoxTractiveForceAtSpeedN = new List<float>(copy.GearBoxTractiveForceAtSpeedN);
            GearBoxOverspeedPercentageForFailure = copy.GearBoxOverspeedPercentageForFailure;
            GearBoxBackLoadForceN = copy.GearBoxBackLoadForceN;
            GearBoxCoastingForceN = copy.GearBoxCoastingForceN;
            GearBoxUpGearProportion = copy.GearBoxUpGearProportion;
            GearBoxDownGearProportion = copy.GearBoxDownGearProportion;
            FreeWheelFitted = copy.FreeWheelFitted;
            initLevel = copy.initLevel;
        }
    }

    public class GearBox : ISubSystem<GearBox>, ISaveStateApi<GearBoxSaveState>
    {
        private readonly DieselEngine dieselEngine;
        private readonly MSTSDieselLocomotive locomotive;
        protected MSTSGearBoxParams GearBoxParams => locomotive.DieselEngines.MSTSGearBoxParams;
        public List<Gear> Gears { get; } = new List<Gear>();

        public bool GearBoxFreeWheelFitted;
        public bool GearBoxFreeWheelEnabled;

        public bool GearedThrottleDecrease;
        public float previousGearThrottleSetting;

        public float ManualGearTimerResetS = 2;  // Allow gear change to take 2 seconds
        public float ManualGearTimerS; // Time for gears to change
        public bool ManualGearBoxChangeOn;
        public bool ManualGearUp;
        public bool ManualGearDown;

        private bool clutchLockOut;

        public int CurrentGearIndex { get; set; } = -1;
        public int NextGearIndex { get; set; } = -1;

        public Gear CurrentGear
        {
            get
            {
                if ((CurrentGearIndex >= 0) && (CurrentGearIndex < NumOfGears))
                    return Gears[CurrentGearIndex];
                else
                    return null;
            }
        }

        public Gear NextGear
        {
            get
            {
                if ((NextGearIndex >= 0) && (NextGearIndex < NumOfGears))
                    return Gears[NextGearIndex];
                else
                    return null;
            }
            set
            {
                switch (GearBoxOperation)
                {
                    case GearBoxOperation.Manual:
                    case GearBoxOperation.Semiautomatic:
                        int temp = 0;
                        if (value == null)
                            NextGearIndex = -1;
                        else
                        {
                            foreach (Gear gear in Gears)
                            {
                                temp++;
                                if (gear == value)
                                {
                                    break;
                                }
                            }
                            NextGearIndex = temp - 1;
                        }
                        break;
                    case GearBoxOperation.Automatic:
                        break;
                }
            }
        }

        private bool gearedUp;
        private bool gearedDown;
        public bool GearedUp => gearedUp;
        public bool GearedDown => gearedDown;

        public bool AutoGearUp()
        {
            if (clutch < 0.05f)
            {
                if (!gearedUp)
                {
                    if (++NextGearIndex >= Gears.Count)
                        NextGearIndex = (Gears.Count - 1);
                    else
                        gearedUp = true;
                }
                else
                    gearedUp = false;
            }
            return gearedUp;
        }

        public bool AutoGearDown()
        {
            if (clutch < 0.05f)
            {
                if (!gearedDown)
                {
                    if (--NextGearIndex <= 0)
                        NextGearIndex = 0;
                    else
                        gearedDown = true;
                }
                else
                    gearedDown = false;
            }
            return gearedDown;
        }

        public void AutoAtGear()
        {
            gearedUp = false;
            gearedDown = false;
        }

        /// <summary>
        /// Indicates when a manual gear change has been initiated
        /// </summary>
        public bool ManualGearChange { get; set; }

        public bool ClutchOn { get; set; }

        /// <summary>
        /// ClutchOn is true when clutch is fully engaged, and false when slipping
        /// </summary>
        public bool IsClutchOn
        {
            get
            {
                if (locomotive != null && locomotive.DieselTransmissionType == DieselTransmissionType.Mechanic)
                {
                    if (GearBoxOperation == GearBoxOperation.Automatic)
                    {

                        if (dieselEngine.Locomotive.ThrottlePercent > 0)
                        {
                            if (ShaftRPM >= (CurrentGear.DownGearProportion * dieselEngine.MaxRPM))
                                ClutchOn = true;
                        }
                        if (ShaftRPM < dieselEngine.StartingRPM)
                            ClutchOn = false;
                        return ClutchOn;
                    }
                    else  // Manual clutch operation
                    {

                        if (dieselEngine.Locomotive.ThrottlePercent == 0 && !ClutchOn && locomotive.SpeedMpS < 0.05f && ClutchType == ClutchType.Friction)
                        {
                            ClutchOn = false;
                            return ClutchOn;
                        }
                        else if (!GearBoxFreeWheelEnabled && dieselEngine.Locomotive.ThrottlePercent == 0 && !clutchLockOut && ManualGearBoxChangeOn && ClutchType != ClutchType.Friction) // Fluid and Scoop clutches disengage if throttle is closed
                        {
                            clutchLockOut = true;
                            ClutchOn = false;
                            return ClutchOn;
                        }
                        else if (ClutchType != ClutchType.Friction && dieselEngine.Locomotive.ThrottlePercent > 0)
                        {
                            clutchLockOut = false;
                        }

                        // Set clutch status to false when gear change is initiated.
                        if (ManualGearBoxChangeOn)
                        {
                            ClutchOn = false;
                            return ClutchOn;
                        }

                        // Set clutch engaged when shaftrpm and engine rpm are equal
                        if ((dieselEngine.Locomotive.ThrottlePercent >= 0 || dieselEngine.Locomotive.SpeedMpS > 0) && CurrentGear != null && !GearBoxFreeWheelEnabled)
                        {
                            var clutchEngagementBandwidthRPM = 10.0f;
                            if (ShaftRPM >= dieselEngine.RealRPM - clutchEngagementBandwidthRPM && ShaftRPM < dieselEngine.RealRPM + clutchEngagementBandwidthRPM && ShaftRPM < dieselEngine.MaxRPM && ShaftRPM > dieselEngine.IdleRPM)
                                ClutchOn = true;
                            return ClutchOn;
                        }
                        else if ((ClutchType == ClutchType.Scoop || ClutchType == ClutchType.Fluid) && CurrentGear == null)
                        {
                            ClutchOn = false;
                            return ClutchOn;
                        }

                        // Set clutch disengaged (slip mode) if shaft rpm moves outside of acceptable bandwidth speed (on type A, B and C clutches), Type D will not slip unless put into neutral
                        var clutchSlipBandwidth = 0.1f * dieselEngine.ThrottleRPMTab[dieselEngine.DemandedThrottlePercent]; // Bandwidth 10%
                        var speedVariationRpM = Math.Abs(dieselEngine.ThrottleRPMTab[dieselEngine.DemandedThrottlePercent] - ShaftRPM);
                        if (GearBoxFreeWheelFitted && speedVariationRpM > clutchSlipBandwidth && (GearBoxType != GearBoxType.D || GearBoxType != GearBoxType.A))
                        {
                            ClutchOn = false;
                            return ClutchOn;
                        }
                        return ClutchOn;
                    }
                }
                else // default (legacy) units
                {
                    if (dieselEngine.Locomotive.ThrottlePercent > 0)
                    {
                        if (ShaftRPM >= (CurrentGear.DownGearProportion * dieselEngine.MaxRPM))
                            ClutchOn = true;
                    }
                    if (ShaftRPM < dieselEngine.StartingRPM)
                        ClutchOn = false;
                    return ClutchOn;
                }
            }
        }

        public int NumOfGears => Gears.Count;

        // The default gear configuration is N-1-2-3-4, etc. However some locomotives have a N-4-3-2-1 configuration. So the display indication is reversed to 
        // give the impression that this gear system is set.
        public int GearIndication => ReverseGearBoxIndication ? MathHelper.Clamp(NumOfGears - CurrentGearIndex, 0, NumOfGears) : CurrentGearIndex + 1;

        public float CurrentSpeedMpS
        {
            get
            {
                if (dieselEngine.Locomotive.Direction == MidpointDirection.Reverse)
                    return -(dieselEngine.Locomotive.SpeedMpS);
                else
                    return (dieselEngine.Locomotive.SpeedMpS);
            }
        }

        /// <summary>
        /// The HuD display value for ShaftRpM
        /// </summary>
        public float HuDShaftRPM
        {
            get
            {
                if (CurrentGear == null)
                {
                    return 0;
                }
                else
                {
                    var temp = ShaftRPM;

                    return temp;
                }
            }
        }


        /// <summary>
        /// ShaftRpM is the speed of the input shaft to the gearbox due to the speed of the wheel rotation
        /// </summary>
        public float ShaftRPM
        {
            get
            {
                if (locomotive != null && locomotive.DieselTransmissionType == DieselTransmissionType.Mechanic)
                {
                    if (CurrentGear == null)
                    {
                        return dieselEngine.RealRPM;
                    }
                    else
                    {
                        if (GearBoxOperation == GearBoxOperation.Automatic)
                        {
                            return CurrentSpeedMpS / CurrentGear.Ratio;
                        }
                        else
                        {
                            const float perSectoPerMin = 60;
                            var driveWheelCircumferenceM = 2 * Math.PI * locomotive.DriverWheelRadiusM;
                            var driveWheelRpm = locomotive.AbsSpeedMpS * perSectoPerMin / driveWheelCircumferenceM;
                            var shaftRPM = driveWheelRpm * CurrentGear.Ratio;
                            return (float)(shaftRPM);
                        }

                    }

                }
                else // Legacy operation
                {

                    if (CurrentGear == null)
                        return dieselEngine.RealRPM;
                    else
                        return CurrentSpeedMpS / CurrentGear.Ratio;
                }
            }
        }

        public bool IsOverspeedError
        {
            get
            {
                if (CurrentGear == null)
                    return false;
                else
                    return ((dieselEngine.RealRPM / dieselEngine.MaxRPM * 100f) > CurrentGear.OverspeedPercentage);
            }
        }

        public bool IsOverspeedWarning
        {
            get
            {
                if (CurrentGear == null)
                    return false;
                else
                    return ((dieselEngine.RealRPM / dieselEngine.MaxRPM * 100f) > 100f);
            }
        }

        private float clutch;
        public float ClutchPercent
        {
            set => clutch = (value > 100.0f ? 100f : (value < -100f ? -100f : value)) / 100f;
            get => clutch * 100f;
        }

        public bool AutoClutch = true;

        public bool ReverseGearBoxIndication { get; set; }

        public ClutchType ClutchType = ClutchType.Unknown;
        public GearBoxType GearBoxType = GearBoxType.Unknown;
        public GearBoxOperation GearBoxOperation = GearBoxOperation.Manual;
        public GearBoxOperation OriginalGearBoxOperation = GearBoxOperation.Manual;

        private float rpmRatio;
        internal float torqueCurveMultiplier;
        private float throttleFraction;
        public float TorqueCurveMultiplier => torqueCurveMultiplier;
        private double tractiveForceN;
        public float TractiveForceN
        {
            get
            {
                if (CurrentGear != null)
                {

                    if (locomotive != null && locomotive.DieselTransmissionType == DieselTransmissionType.Mechanic)
                    {

                        if (GearBoxOperation == GearBoxOperation.Automatic)
                        {
                            if (ClutchPercent >= -20)
                            {
                                tractiveForceN = dieselEngine.DieselTorqueTab[dieselEngine.RealRPM] * dieselEngine.DemandedThrottlePercent / dieselEngine.DieselTorqueTab.MaxY() * 0.01f * CurrentGear.MaxTractiveForceN;
                                if (CurrentSpeedMpS > 0)
                                {
                                    if (tractiveForceN > (dieselEngine.CurrentDieselOutputPowerW / CurrentSpeedMpS))
                                        tractiveForceN = dieselEngine.CurrentDieselOutputPowerW / CurrentSpeedMpS;
                                }
                                return (float)tractiveForceN;
                            }
                            else
                                return -CurrentGear.CoastingForceN * (100f + ClutchPercent) / 100f;
                        }
                        else if (GearBoxOperation == GearBoxOperation.Manual)
                        {
                            // Allow rpm to go below idle for display purposes, but not for calculation - creates -ve te
                            float dieselRpM;
                            if (dieselEngine.RealRPM < dieselEngine.IdleRPM)
                            {
                                dieselRpM = dieselEngine.IdleRPM;
                            }
                            else
                            {
                                dieselRpM = dieselEngine.RealRPM;
                            }

                            throttleFraction = 0;

                            if (dieselEngine.ApparentThrottleSetting < dieselEngine.DemandedThrottlePercent)
                            {
                                // Use apparent throttle when accelerating so that time delays in rpm rise and fall are used, but use demanded throttle at other times
                                //  throttleFraction = DieselEngine.ApparentThrottleSetting * 0.01f; // Convert from percentage to fraction, use the apparent throttle as this includes some delay for rpm increase

                                throttleFraction = dieselEngine.DemandedThrottlePercent * 0.01f;

                            }
                            else // As apparent throttle is related to current rpm, limit throttle to the actual demanded throttle. 
                            {
                                throttleFraction = dieselEngine.DemandedThrottlePercent * 0.01f;
                            }

                            // Limit tractive force if engine is governed, ie speed cannot exceed the governed speed or the throttled speed
                            // Diesel mechanical transmission are not "governed" at all engine speed settings, rather only at Idle and Max RpM. 
                            // (See above where DM units TE held at constant value, unless overwritten by the following)
                            if (locomotive.DieselTransmissionType == DieselTransmissionType.Mechanic)
                            {
                                // If engine RpM exceeds maximum rpm
                                if (dieselEngine.GovernorEnabled && dieselEngine.DemandedThrottlePercent > 0 && dieselEngine.RealRPM > dieselEngine.MaxRPM)
                                {
                                    var decayGradient = 1.0f / (dieselEngine.GovernorRPM - dieselEngine.MaxRPM);
                                    var rpmOverRun = (dieselEngine.RealRPM - dieselEngine.MaxRPM);
                                    throttleFraction = (1.0f - (decayGradient * rpmOverRun)) * throttleFraction;
                                    throttleFraction = MathHelper.Clamp(throttleFraction, 0.0f, 1.0f);  // Clamp throttle setting within bounds, so it doesn't go negative                           
                                }

                                // If engine RpM drops below idle rpm
                                if (dieselEngine.GovernorEnabled && dieselEngine.DemandedThrottlePercent > 0 && dieselEngine.RealRPM < dieselEngine.IdleRPM)
                                {
                                    var decayGradient = 1.0f / (dieselEngine.IdleRPM - dieselEngine.StartingRPM);
                                    var rpmUnderRun = dieselEngine.IdleRPM - dieselEngine.RealRPM;
                                    throttleFraction = decayGradient * rpmUnderRun + throttleFraction; // Increases throttle over current setting up to a maximum of 100%
                                    throttleFraction = MathHelper.Clamp(throttleFraction, 0.0f, 1.0f);  // Clamp throttle setting within bounds, so it doesn't go negative
                                }
                            }

                            // A torque vs rpm family of curves has been built based on the information on this page
                            // https://www.cm-labs.com/vortexstudiodocumentation/Vortex_User_Documentation/Content/Editor/editor_vs_configure_engine.html
                            //
                            // Calculate torque curve for throttle position and RpM
                            rpmRatio = (dieselRpM - dieselEngine.IdleRPM) / (dieselEngine.MaxRPM - dieselEngine.IdleRPM);
                            torqueCurveMultiplier = (0.824f * throttleFraction + 0.176f) + (0.785f * throttleFraction - 0.785f) * rpmRatio;

                            // During normal operation fuel admission is fixed, and therefore TE follows curve as RpM varies
                            tractiveForceN = torqueCurveMultiplier * dieselEngine.DieselTorqueTab[dieselEngine.RealRPM] / dieselEngine.DieselTorqueTab.MaxY() * CurrentGear.MaxTractiveForceN;

                            locomotive.HuDGearMaximumTractiveForce = CurrentGear.MaxTractiveForceN;

                            if (CurrentSpeedMpS > 0)
                            {
                                var tractiveEffortLimitN = (dieselEngine.DieselPowerTab[dieselEngine.RealRPM] * (dieselEngine.LoadPercent / 100f)) / CurrentSpeedMpS;

                                if (tractiveForceN > tractiveEffortLimitN )
                                {
                                    tractiveForceN = tractiveEffortLimitN;
                                }
                            }

                            // Set TE to zero if gear change happening && type B gear box
                            if (ManualGearBoxChangeOn && GearBoxType == GearBoxType.B)
                            {
                                tractiveForceN = 0;
                            }

                            // Scoop couplings prevent TE "creep" at zero throttle
                            if (throttleFraction == 0 && dieselEngine.RealRPM < 1.05f * dieselEngine.IdleRPM && ClutchType == ClutchType.Scoop)
                            {
                                tractiveForceN = 0;
                            }

                            // if freewheeling set TE to zero
                            if (GearBoxFreeWheelEnabled)
                            {
                                tractiveForceN = 0;
                            }

                            // Calculate tractive force if engine shuts down due to under or overspeed
                            // For engines when clutch is on calculate drag of "stalled" engine on locomotive. This will be maximum zero throttle @ GovernorRpM scaled back to 0 TE at 0 rpm.
                            if (dieselEngine.GearOverspeedShutdownEnabled || dieselEngine.GearUnderspeedShutdownEnabled)
                            {
                                if (IsClutchOn)
                                {
                                    var tempRpmRatio = (dieselRpM - dieselEngine.IdleRPM) / (dieselEngine.MaxRPM - dieselEngine.IdleRPM);
                                    var stallThrottleFraction = 0; // when stalled throttle fraction will be zero
                                    var stallTorqueCurveMultiplier = (0.824f * stallThrottleFraction + 0.176f) + (0.785f * stallThrottleFraction - 0.785f) * rpmRatio;

                                    // During normal operation fuel admission is fixed, and therefore TE follows curve as RpM varies
                                    var maxStallEngineTE = stallTorqueCurveMultiplier * dieselEngine.DieselTorqueTab[dieselEngine.RealRPM] / dieselEngine.DieselTorqueTab.MaxY() * CurrentGear.MaxTractiveForceN;

                                    var stallGradient = maxStallEngineTE / dieselEngine.GovernorRPM;

                                    tractiveForceN = stallGradient * dieselRpM;

                                }
                                else
                                {
                                    tractiveForceN = CurrentGear.CoastingForceN;
                                }

                            }
                            return (float)tractiveForceN;
                        }
                        else
                            return 0;
                    }
                    else
                    {
                        if (ClutchPercent >= -20)
                        {
                            double tractiveForceN = dieselEngine.DieselTorqueTab[dieselEngine.RealRPM] * dieselEngine.DemandedThrottlePercent / dieselEngine.DieselTorqueTab.MaxY() * 0.01f * CurrentGear.MaxTractiveForceN;
                            if (CurrentSpeedMpS > 0)
                            {
                                if (tractiveForceN > (dieselEngine.CurrentDieselOutputPowerW / CurrentSpeedMpS))
                                    tractiveForceN = dieselEngine.CurrentDieselOutputPowerW / CurrentSpeedMpS;
                            }
                            return (float)tractiveForceN;
                        }
                        else
                            return -CurrentGear.CoastingForceN * (100f + ClutchPercent) / 100f;
                    }
                }
                else
                    return 0;
            }
        }

        public GearBox(DieselEngine dieselEngine)
        {
            this.dieselEngine = dieselEngine;
            locomotive = dieselEngine.Locomotive;
        }

        public void Copy(GearBox source)
        {
            // Nothing to copy, all parameters will be copied from MSTSGearBoxParams at initialization
        }

        public ValueTask<GearBoxSaveState> Snapshot()
        {
            return ValueTask.FromResult(new GearBoxSaveState()
            {
                GearIndex = CurrentGearIndex,
                NextGearIndex = NextGearIndex,
                GearedUp = gearedUp,
                GearedDown = gearedDown,
                ClutchActive = ClutchOn,
                ClutchValue = clutch,
                ManualGearUp = ManualGearUp,
                ManualGearDown = ManualGearDown,
                ManualGearChange = ManualGearChange,
                ManualGearTimer = ManualGearTimerS,
            });
        }

        public ValueTask Restore(GearBoxSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            CurrentGearIndex = saveState.GearIndex;
            locomotive.currentGearIndexRestore = CurrentGearIndex;
            NextGearIndex = saveState.NextGearIndex;
            locomotive.currentnextGearRestore = NextGearIndex;
            gearedUp = saveState.GearedUp;
            gearedDown = saveState.GearedDown;
            ClutchOn = saveState.ClutchActive;
            clutch = saveState.ClutchValue;
            ManualGearDown = saveState.ManualGearDown;
            ManualGearUp = saveState.ManualGearUp;
            ManualGearChange = saveState.ManualGearChange;
            ManualGearTimerS = (float)saveState.ManualGearTimer;

            return ValueTask.CompletedTask;
        }

        public void Initialize()
        {
            if (GearBoxParams != null)
            {
                if ((!GearBoxParams.IsInitialized) && (GearBoxParams.AtLeastOneParamFound))
                    Trace.TraceWarning("Some of the gearbox parameters are missing! Default physics will be used.");

                ReverseGearBoxIndication = GearBoxParams.ReverseGearBoxIndication;
                GearBoxType = GearBoxParams.GearBoxType;
                ClutchType = GearBoxParams.ClutchType;
                GearBoxFreeWheelFitted = GearBoxParams.FreeWheelFitted;

                for (int i = 0; i < GearBoxParams.GearBoxNumberOfGears; i++)
                {
                    Gears.Add(new Gear(this));
                    Gears[i].BackLoadForceN = GearBoxParams.GearBoxBackLoadForceN;
                    Gears[i].CoastingForceN = GearBoxParams.GearBoxCoastingForceN;
                    Gears[i].DownGearProportion = GearBoxParams.GearBoxDownGearProportion;
                    Gears[i].IsDirectDriveGear = (GearBoxParams.GearBoxDirectDriveGear == GearBoxParams.GearBoxNumberOfGears);
                    Gears[i].MaxSpeedMpS = GearBoxParams.GearBoxMaxSpeedForGearsMpS[i];
                    // Maximum torque (tractive effort) actually occurs at less then the maximum engine rpm, so this section uses either 
                    // the TE at gear maximum speed, or if the user has entered the maximum TE
                    if (!GearBoxParams.MaxTEFound)
                    {
                        // If user has entered this value then assume that they have already put the maximum torque value in
                        Gears[i].MaxTractiveForceN = GearBoxParams.GearBoxMaxTractiveForceForGearsN[i] / locomotive.DieselEngines.Count;

                        // For purposes of calculating engine efficiency the tractive force at maximum gear speed needs to be used.
                        Gears[i].TractiveForceatMaxSpeedN = (float)(GearBoxParams.GearBoxMaxTractiveForceForGearsN[i] / (dieselEngine.DieselTorqueTab.MaxY() / dieselEngine.DieselTorqueTab[dieselEngine.MaxRPM])) / locomotive.DieselEngines.Count;
                    }
                    else
                    {
                        // if they entered the TE at maximum gear speed, then increase the value accordingly 
                        Gears[i].MaxTractiveForceN = (float)(GearBoxParams.GearBoxTractiveForceAtSpeedN[i] * dieselEngine.DieselTorqueTab.MaxY() / dieselEngine.DieselTorqueTab[dieselEngine.MaxRPM]) / locomotive.DieselEngines.Count;

                        // For purposes of calculating engine efficiency the tractive force at maximum gear speed needs to be used.
                        Gears[i].TractiveForceatMaxSpeedN = GearBoxParams.GearBoxTractiveForceAtSpeedN[i] / locomotive.DieselEngines.Count;
                    }                        

                    Gears[i].OverspeedPercentage = GearBoxParams.GearBoxOverspeedPercentageForFailure;
                    Gears[i].UpGearProportion = GearBoxParams.GearBoxUpGearProportion;
                    if (locomotive != null && locomotive.DieselTransmissionType == DieselTransmissionType.Mechanic)
                    {
                        // Calculate gear ratio, based on premise that drive wheel rpm @ max speed will be when engine is operating at max rpm
                        double driveWheelCircumferenceM = 2 * Math.PI * locomotive.DriverWheelRadiusM;
                        double driveWheelRpm = Frequency.Periodic.ToMinutes(Gears[i].MaxSpeedMpS) / driveWheelCircumferenceM;
                        float apparentGear = (float)(dieselEngine.MaxRPM / driveWheelRpm);

                        Gears[i].Ratio = apparentGear;

                        Gears[i].BackLoadForceN = Gears[i].Ratio * GearBoxParams.GearBoxBackLoadForceN;
                        Gears[i].CoastingForceN = Gears[i].Ratio * GearBoxParams.GearBoxCoastingForceN;

                        Gears[i].ChangeUpSpeedRpM = dieselEngine.MaxRPM;

                        Gears[0].ChangeDownSpeedRpM = dieselEngine.IdleRPM;

                        if (i > 0)
                        {
                            driveWheelRpm = Frequency.Periodic.ToMinutes(Gears[i - 1].MaxSpeedMpS) / driveWheelCircumferenceM;
                            Gears[i].ChangeDownSpeedRpM = (float)driveWheelRpm * Gears[i].Ratio;
                        }
                    }
                    else
                    {
                        Gears[i].Ratio = GearBoxParams.GearBoxMaxSpeedForGearsMpS[i] / dieselEngine.MaxRPM;
                    }
                }
                GearBoxOperation = GearBoxParams.GearBoxOperation;
                OriginalGearBoxOperation = GearBoxParams.GearBoxOperation;
            }
        }

        public void InitializeMoving()
        {
            for (int iGear = 0; iGear < Gears.Count; iGear++)
            {
                if (Gears[iGear].MaxSpeedMpS < CurrentSpeedMpS)
                    continue;
                else
                    CurrentGearIndex = NextGearIndex = iGear;
                break;
            }

            gearedUp = false;
            gearedDown = false;
            ClutchOn = true;
            clutch = 0.4f;
            dieselEngine.RealRPM = ShaftRPM;
        }

        public void Update(double elapsedClockSeconds)
        {
            if (locomotive != null && locomotive.DieselTransmissionType == DieselTransmissionType.Mechanic)
            {
                if (GearBoxOperation == GearBoxOperation.Automatic || GearBoxOperation == GearBoxOperation.Semiautomatic)
                {

                    if ((clutch <= 0.05) || (clutch >= 1f))
                    {

                        if (CurrentGearIndex < NextGearIndex)
                        {
                            dieselEngine.Locomotive.SignalEvent(TrainEvent.GearUp);
                            CurrentGearIndex = NextGearIndex;
                        }
                    }
                    if ((clutch <= 0.05) || (clutch >= 0.5f))
                    {
                        if (CurrentGearIndex > NextGearIndex)
                        {
                            dieselEngine.Locomotive.SignalEvent(TrainEvent.GearDown);
                            CurrentGearIndex = NextGearIndex;
                        }
                    }
                }
                else if (GearBoxOperation == GearBoxOperation.Manual)
                {

                    if (ManualGearUp)
                    {

                        if (CurrentGearIndex < NextGearIndex)
                        {
                            dieselEngine.Locomotive.SignalEvent(TrainEvent.GearUp);
                            CurrentGearIndex = NextGearIndex;
                            ManualGearUp = false;
                        }
                    }

                    if (ManualGearDown)
                    {
                        if (CurrentGearIndex > NextGearIndex)
                        {
                            dieselEngine.Locomotive.SignalEvent(TrainEvent.GearDown);
                            CurrentGearIndex = NextGearIndex;
                            ManualGearDown = false;
                        }
                    }
                }

            }
            else // Legacy operation
            {
                if ((clutch <= 0.05) || (clutch >= 1f))
                {
                    if (CurrentGearIndex < NextGearIndex)
                    {
                        dieselEngine.Locomotive.SignalEvent(TrainEvent.GearUp);
                        CurrentGearIndex = NextGearIndex;
                    }
                }
                if ((clutch <= 0.05) || (clutch >= 0.5f))
                {
                    if (CurrentGearIndex > NextGearIndex)
                    {
                        dieselEngine.Locomotive.SignalEvent(TrainEvent.GearDown);
                        CurrentGearIndex = NextGearIndex;
                    }
                }
            }

            if (dieselEngine.State == DieselEngineState.Running)
            {
                switch (GearBoxOperation)
                {
                    case GearBoxOperation.Manual:
                        if (dieselEngine.Locomotive.ThrottlePercent == 0 && locomotive.AbsSpeedMpS == 0)
                        {
                            ClutchOn = false;
                            ClutchPercent = 0f;
                        }
                        break;
                    case GearBoxOperation.Automatic:
                    case GearBoxOperation.Semiautomatic:
                        if ((CurrentGear != null))
                        {
                            if ((CurrentSpeedMpS > (dieselEngine.MaxRPM * CurrentGear.UpGearProportion * CurrentGear.Ratio)))// && (!GearedUp) && (!GearedDown))
                                AutoGearUp();
                            else
                            {
                                if ((CurrentSpeedMpS < (dieselEngine.MaxRPM * CurrentGear.DownGearProportion * CurrentGear.Ratio)))// && (!GearedUp) && (!GearedDown))
                                    AutoGearDown();
                                else
                                    AutoAtGear();
                            }
                            if (dieselEngine.Locomotive.ThrottlePercent == 0)
                            {
                                if ((CurrentGear != null) || (NextGear == null))
                                {
                                    NextGearIndex = -1;
                                    CurrentGearIndex = -1;
                                    ClutchOn = false;
                                    gearedDown = false;
                                    gearedUp = false;
                                }

                            }
                        }
                        else
                        {
                            if ((dieselEngine.Locomotive.ThrottlePercent > 0))
                                AutoGearUp();
                            else
                            {
                                NextGearIndex = -1;
                                CurrentGearIndex = -1;
                                ClutchOn = false;
                                gearedDown = false;
                                gearedUp = false;
                            }
                        }
                        break;
                }
            }
            // If diesel engine is stopped (potentially after a stall) on a manual gearbox then allow gears to be changed
            else if (dieselEngine.State == DieselEngineState.Stopped)
            {
                switch (GearBoxOperation)
                {
                    case GearBoxOperation.Manual:
                        if (locomotive.AbsSpeedMpS < 0.05)
                        {
                            ClutchOn = false;
                            ClutchPercent = 0f;
                        }
                        break;
                }
            }
            else
            {
                NextGearIndex = -1;
                CurrentGearIndex = -1;
                ClutchOn = false;
                gearedDown = false;
                gearedUp = false;
            }
        }
    }

    public enum ClutchType
    {
        Unknown,
        Friction,
        Fluid,
        Scoop
    }

    public enum GearBoxType
    {
        Unknown,
        A,
        B,
        C,
        D
    }

    public enum GearBoxOperation
    {
        Manual,
        Automatic,
        Semiautomatic
    }

    public enum GearBoxEngineBraking
    {
        None,
        DirectDrive,
        AllGears
    }

    public class Gear
    {
        public bool IsDirectDriveGear;
        public float MaxSpeedMpS;
        public float ChangeUpSpeedRpM;
        public float ChangeDownSpeedRpM;
        public float MaxTractiveForceN;
        public float TractiveForceatMaxSpeedN;
        public float OverspeedPercentage;
        public float BackLoadForceN;
        public float CoastingForceN;
        public float UpGearProportion;
        public float DownGearProportion;

        public float Ratio = 1f;

        protected readonly GearBox GearBox;

        public Gear(GearBox gb) { GearBox = gb; }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
