// COPYRIGHT 2013 - 2021 by the Open Rails project.
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
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;

namespace Orts.Simulation.RollingStocks.SubSystems
{
    public enum CruiseControlSpeed
    {
        Speed0,
        Speed10,
        Speed20,
        Speed30,
        Speed40,
        Speed50,
        Speed60,
        Speed70,
        Speed80,
        Speed90,
        Speed100,
        Speed110,
        Speed120,
        Speed130,
        Speed140,
        Speed150,
        Speed160,
        Speed170,
        Speed180,
        Speed190,
        Speed200,
    }

    public class CruiseControl
    {
        private readonly MSTSLocomotive locomotive;
        private readonly Simulator simulator;

        private bool selectedSpeedIncreasing;
        private double selectedSpeedLeverHoldTime;
        private bool SelectedSpeedDecreasing;

        private bool speedIsMph;
        private bool maxForceIncreasing;
        private bool maxForceDecreasing;

        public bool Equipped { get; set; }
        public bool SpeedRegulatorMaxForcePercentUnits { get; set; }
        public float SpeedRegulatorMaxForceSteps { get; set; }
        public bool MaxForceSetSingleStep { get; set; }
        public bool MaxForceKeepSelectedStepWhenManualModeSet { get; set; }
        public bool KeepSelectedSpeedWhenManualModeSet { get; set; }
        public bool ForceRegulatorAutoWhenNonZeroSpeedSelected { get; set; }
        public bool ForceRegulatorAutoWhenNonZeroForceSelected { get; set; }
        public bool ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero { get; set; }
        public bool MaxForceSelectorIsDiscrete { get; set; }
        private readonly List<string> speedRegulatorOptions = new List<string>();
        public SpeedRegulatorMode SpeedRegulatorMode { get; set; } = SpeedRegulatorMode.Manual;
        public SpeedSelectorMode SpeedSelectorMode { get; set; } = SpeedSelectorMode.Neutral;
        public CruiseControlLogic CruiseControlLogic { get; private set; }
        public float SelectedMaxAccelerationPercent { get; set; }
        public float SelectedMaxAccelerationStep { get; set; }
        public float SelectedSpeedMpS { get; set; }
        public int SelectedNumberOfAxles { get; set; }
        public float SpeedRegulatorNominalSpeedStepMpS { get; set; }
        private float speedRegulatorNominalSpeedStepKpHOrMpH;
        private float maxAccelerationMpSS = 1;
        private float maxDecelerationMpSS = 0.5f;
        private bool useThrottle;
        public bool UseThrottleInCombinedControl { get; set; }
        private bool antiWheelSpinEquipped;
        private float antiWheelSpinSpeedDiffThreshold;
        private float dynamicBrakeMaxForceAtSelectorStep;
        private float forceThrottleAndDynamicBrake;
        private double maxForceN;
        private float trainBrakePercent;
        private float trainLength;
        private int trainLengthMeters;
        private int remainingTrainLengthToPassRestrictedZone;
        private bool restrictedSpeedActive;
        private float currentSelectedSpeedMpS;
        private float nextSelectedSpeedMps;
        private float restrictedRegionTravelledDistance;
        private float currentThrottlePercent;
        private double clockTime;
        private bool dynamicBrakeSetToZero;

        private float startReducingSpeedDelta = 0.5f;
        private float StartReducingSpeedDeltaDownwards;
        private bool battery;
        private bool throttleNeutralPriority;
        private readonly List<int> forceStepsThrottleTable = new List<int>();

        private float throttleFullRangeIncreaseTimeSeconds = 6;
        private float throttleFullRangeDecreaseTimeSeconds = 6;
        private readonly List<float> accelerationTable = new List<float>();

        public bool DynamicBrakePriority { get; set; }
        private float absMaxForceN;
        private float brakePercent;
        public float DynamicBrakeIncreaseSpeed { get; set; }
        public float DynamicBrakeDecreaseSpeed { get; set; }
        public uint MinimumMetersToPass { get; set; } = 19;
        private float relativeAcceleration;
        public float AccelerationRampMaxMpSSS { get; set; } = 0.7f;
        public float AccelerationDemandMpSS { get; set; }
        public float AccelerationRampMinMpSSS { get; set; } = 0.01f;
        public bool ResetForceAfterAnyBraking { get; set; }
        public float DynamicBrakeFullRangeIncreaseTimeSeconds { get; set; }
        public float DynamicBrakeFullRangeDecreaseTimeSeconds { get; set; }
        public float ParkingBrakeEngageSpeed { get; set; }
        public float ParkingBrakePercent { get; set; }
        public bool SkipThrottleDisplay { get; set; }
        public bool DisableZeroForceStep { get; set; }
        public bool DynamicBrakeIsSelectedForceDependant { get; set; }
        public bool UseThrottleAsSpeedSelector { get; set; }
        public bool UseThrottleAsForceSelector { get; set; }
        public float Ampers { get; set; }
        public bool ContinuousSpeedIncreasing { get; set; }
        public bool ContinuousSpeedDecreasing { get; set; }
        public float PowerBreakoutAmpers { get; set; }
        public float PowerBreakoutSpeedDelta { get; set; }
        public float PowerResumeSpeedDelta { get; set; }
        public float PowerReductionDelayPaxTrain { get; set; }
        public float PowerReductionDelayCargoTrain { get; set; }
        public float PowerReductionValue { get; set; }
        public float MaxPowerThreshold { get; set; }
        public float SafeSpeedForAutomaticOperationMpS { get; set; }
        protected float SpeedSelectorStepTimeSeconds { get; set; }
        private double totalTime;
        public bool DisableCruiseControlOnThrottleAndZeroSpeed { get; set; }
        public bool DisableCruiseControlOnThrottleAndZeroForce { get; set; }
        public bool DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed { get; set; }
        public bool ForceResetRequiredAfterBraking { get; set; }
        public bool ForceResetIncludeDynamicBrake { get; set; }
        public bool ZeroSelectedSpeedWhenPassingToThrottleMode { get; set; }
        public bool DynamicBrakeCommandHasPriorityOverCruiseControl { get; set; } = true;
        public bool HasIndependentThrottleDynamicBrakeLever { get; set; }
        public bool HasProportionalSpeedSelector { get; set; }
        public bool SpeedSelectorIsDiscrete { get; set; }
        public bool DoComputeNumberOfAxles { get; set; }
        public bool DisableManualSwitchToManualWhenSetForceNotAtZero { get; set; }
        public bool DisableManualSwitchToAutoWhenThrottleNotAtZero { get; set; }
        public bool DisableManualSwitchToAutoWhenSetSpeedNotAtTop { get; set; }
        public bool EnableSelectedSpeedSelectionWhenManualModeSet { get; set; }
        public bool UseTrainBrakeAndDynBrake { get; set; }
        private float SpeedDeltaToEnableTrainBrake = 5;
        private float SpeedDeltaToEnableFullTrainBrake = 10;
        public float MinimumSpeedForCCEffectMpS { get; set; }
        private double speedRegulatorIntermediateValue;
        private float StepSize = 20;
        private float RelativeAccelerationMpSS; // Acceleration relative to state of reverser
        public bool CCIsUsingTrainBrake { get; set; } // Cruise control is using (also) train brake to brake
        private float TrainBrakeMinPercentValue = 30f; // Minimum train brake settable percent Value
        private float TrainBrakeMaxPercentValue = 85f; // Maximum train brake settable percent Value
        public bool StartInAutoMode { get; set; } // at startup cruise control is in auto mode
        public bool ThrottleNeutralPosition { get; set; } // when UseThrottleAsSpeedSelector is true and this is true
                                                          // and we are in auto mode, the throttle zero position is a neutral position
        public bool ThrottleLowSpeedPosition { get; set; } // when UseThrottleAsSpeedSelector is true and this is true
                                                           // and we are in auto mode, the first throttle above zero position is used to run at low speed
        public float LowSpeed { get; set; } = 2f; // default parking speed
        public bool HasTwoForceValues { get; set; } // when UseThrottleAsSpeedSelector is true, two max force values (50% and 100%) are available

        public bool OverrideForceCalculation { get; set; }

        private readonly List<MSTSLocomotive> playerNotDriveableTrainLocomotives = new List<MSTSLocomotive>();
        private bool throttleIsZero;
        private bool brakeIncreasing;
        private float controllerTime;
        private float fromAcceleration;
        private bool applyingPneumaticBrake;
        private bool firstIteration = true;
        // throttleOrDynBrakePercent may vary from -100 to 100 and is the percentage value which the Cruise Control
        // sets to throttle (if throttleOrDynBrakePercent >=0) or to dynamic brake (if throttleOrDynBrakePercent <0)
        private double throttleOrDynBrakePercent;
        private bool breakout;
        private double timeFromEngineMoved;
        private bool reducingForce;
        private bool canAddForce = true;
        private float TrainElevation;
        private float skidSpeedDegratation;
        public bool TrainBrakePriority { get; set; }
        private bool WasBraking;
        private bool WasForceReset = true;

        private float previousSelectedSpeed;

        public bool SelectingSpeedPressed { get; set; }
        public bool EngineBrakePriority { get; set; }
        public bool IsAPartOfPlayerTrain { get; private set; }
        public float ThrottleOverriden { get; private set; }
        public int AccelerationBits { get; }
        public EnumArray<bool, CruiseControlSpeed> SpeedPressed { get; } = new EnumArray<bool, CruiseControlSpeed>();

        public CruiseControl(MSTSLocomotive locomotive)
        {
            this.locomotive = locomotive;
        }


        public void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(ortscruisecontrol(speedismph":
                    speedIsMph = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(usethrottle":
                    useThrottle = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(usethrottleincombinedcontrol":
                    UseThrottleInCombinedControl = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(speedselectorsteptimeseconds":
                    SpeedSelectorStepTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 0.1f);
                    break;
                case "engine(ortscruisecontrol(throttlefullrangeincreasetimeseconds":
                    throttleFullRangeIncreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                    break;
                case "engine(ortscruisecontrol(throttlefullrangedecreasetimeseconds":
                    throttleFullRangeDecreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                    break;
                case "engine(ortscruisecontrol(resetforceafteranybraking":
                    ResetForceAfterAnyBraking = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(dynamicbrakefullrangeincreasetimeseconds":
                    DynamicBrakeFullRangeIncreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                    break;
                case "engine(ortscruisecontrol(dynamicbrakefullrangedecreasetimeseconds":
                    DynamicBrakeFullRangeDecreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                    break;
                case "engine(ortscruisecontrol(parkingbrakeengagespeed":
                    ParkingBrakeEngageSpeed = stf.ReadFloatBlock(STFReader.Units.Speed, 0);
                    break;
                case "engine(ortscruisecontrol(parkingbrakepercent":
                    ParkingBrakePercent = stf.ReadFloatBlock(STFReader.Units.Any, 0);
                    break;
                case "engine(ortscruisecontrol(maxpowerthreshold":
                    MaxPowerThreshold = stf.ReadFloatBlock(STFReader.Units.Any, 0);
                    break;
                case "engine(ortscruisecontrol(safespeedforautomaticoperationmps":
                    SafeSpeedForAutomaticOperationMpS = stf.ReadFloatBlock(STFReader.Units.Any, 0);
                    break;
                case "engine(ortscruisecontrol(maxforcepercentunits":
                    SpeedRegulatorMaxForcePercentUnits = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(maxforcesteps":
                    SpeedRegulatorMaxForceSteps = stf.ReadIntBlock(0);
                    break;
                case "engine(ortscruisecontrol(maxforcesetsinglestep":
                    MaxForceSetSingleStep = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(maxforcekeepselectedstepwhenmanualmodeset":
                    MaxForceKeepSelectedStepWhenManualModeSet = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(keepselectedspeedwhenmanualmodeset":
                    KeepSelectedSpeedWhenManualModeSet = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(forceregulatorautowhennonzerospeedselected":
                    ForceRegulatorAutoWhenNonZeroSpeedSelected = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(forceregulatorautowhennonzeroforceselected":
                    ForceRegulatorAutoWhenNonZeroForceSelected = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(forceregulatorautowhennonzerospeedselectedandthrottleatzero":
                    ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(maxforceselectorisdiscrete":
                    MaxForceSelectorIsDiscrete = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(continuousspeedincreasing":
                    ContinuousSpeedIncreasing = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(disablecruisecontrolonthrottleandzerospeed":
                    DisableCruiseControlOnThrottleAndZeroSpeed = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(disablecruisecontrolonthrottleandzeroforce":
                    DisableCruiseControlOnThrottleAndZeroForce = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(disablecruisecontrolonthrottleandzeroforceandzerospeed":
                    DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(disablemanualswitchtomanualwhensetforcenotatzero":
                    DisableManualSwitchToManualWhenSetForceNotAtZero = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(disablemanualswitchtoautowhenthrottlenotatzero":
                    DisableManualSwitchToAutoWhenThrottleNotAtZero = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(disablemanualswitchtoautowhensetspeednotattop":
                    DisableManualSwitchToAutoWhenSetSpeedNotAtTop = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(enableselectedspeedselectionwhenmanualmodeset":
                    EnableSelectedSpeedSelectionWhenManualModeSet = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(forcestepsthrottletable":
                    foreach (string forceStepThrottleString in stf.ReadStringBlock("").Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(forceStepThrottleString, out int forceStepThrottleValue))
                            forceStepsThrottleTable.Add(forceStepThrottleValue);
                    }
                    break;
                case "engine(ortscruisecontrol(accelerationtable":
                    foreach (string accelerationString in stf.ReadStringBlock("").Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (float.TryParse(accelerationString, out float accelerationValue))
                            accelerationTable.Add(accelerationValue);
                    }
                    break;
                case "engine(ortscruisecontrol(powerbreakoutampers":
                    PowerBreakoutAmpers = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                    break;
                case "engine(ortscruisecontrol(powerbreakoutspeeddelta":
                    PowerBreakoutSpeedDelta = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                    break;
                case "engine(ortscruisecontrol(powerresumespeeddelta":
                    PowerResumeSpeedDelta = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                    break;
                case "engine(ortscruisecontrol(powerreductiondelaypaxtrain":
                    PowerReductionDelayPaxTrain = stf.ReadFloatBlock(STFReader.Units.Any, 0.0f);
                    break;
                case "engine(ortscruisecontrol(powerreductiondelaycargotrain":
                    PowerReductionDelayCargoTrain = stf.ReadFloatBlock(STFReader.Units.Any, 0.0f);
                    break;
                case "engine(ortscruisecontrol(powerreductionvalue":
                    PowerReductionValue = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                    break;
                case "engine(ortscruisecontrol(disablezeroforcestep":
                    DisableZeroForceStep = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(dynamicbrakeisselectedforcedependant":
                    DynamicBrakeIsSelectedForceDependant = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(defaultforcestep":
                    SelectedMaxAccelerationStep = stf.ReadFloatBlock(STFReader.Units.Any, 1.0f);
                    break;
                case "engine(ortscruisecontrol(dynamicbrakemaxforceatselectorstep":
                    dynamicBrakeMaxForceAtSelectorStep = stf.ReadFloatBlock(STFReader.Units.Any, 1.0f);
                    break;
                case "engine(ortscruisecontrol(startreducingspeeddelta":
                    startReducingSpeedDelta = (stf.ReadFloatBlock(STFReader.Units.Any, 1.0f) / 10);
                    break;
                case "engine(ortscruisecontrol(startreducingspeeddeltadownwards":
                    StartReducingSpeedDeltaDownwards = (stf.ReadFloatBlock(STFReader.Units.Any, 1.0f) / 10);
                    break;
                case "engine(ortscruisecontrol(maxacceleration":
                    maxAccelerationMpSS = stf.ReadFloatBlock(STFReader.Units.Any, 1);
                    break;
                case "engine(ortscruisecontrol(maxdeceleration":
                    maxDecelerationMpSS = stf.ReadFloatBlock(STFReader.Units.Any, 0.5f);
                    break;
                case "engine(ortscruisecontrol(antiwheelspinequipped":
                    antiWheelSpinEquipped = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(antiwheelspinspeeddiffthreshold":
                    antiWheelSpinSpeedDiffThreshold = stf.ReadFloatBlock(STFReader.Units.None, 0.5f);
                    break;
                case "engine(ortscruisecontrol(nominalspeedstep":
                    {
                        speedRegulatorNominalSpeedStepKpHOrMpH = stf.ReadFloatBlock(STFReader.Units.Speed, 0);
                        SpeedRegulatorNominalSpeedStepMpS = (float)(speedIsMph ? Speed.MeterPerSecond.FromMpH(speedRegulatorNominalSpeedStepKpHOrMpH) : Speed.MeterPerSecond.FromKpH(speedRegulatorNominalSpeedStepKpHOrMpH));
                        break;
                    }
                case "engine(ortscruisecontrol(usethrottleasspeedselector":
                    UseThrottleAsSpeedSelector = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(usethrottleasforceselector":
                    UseThrottleAsForceSelector = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(dynamicbrakeincreasespeed":
                    DynamicBrakeIncreaseSpeed = stf.ReadFloatBlock(STFReader.Units.Any, 0.5f);
                    break;
                case "engine(ortscruisecontrol(dynamicbrakedecreasespeed":
                    DynamicBrakeDecreaseSpeed = stf.ReadFloatBlock(STFReader.Units.Any, 0.5f);
                    break;
                case "engine(ortscruisecontrol(forceresetrequiredafterbraking":
                    ForceResetRequiredAfterBraking = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(forceresetincludedynamicbrake":
                    ForceResetIncludeDynamicBrake = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(zeroselectedspeedwhenpassingtothrottlemode":
                    ZeroSelectedSpeedWhenPassingToThrottleMode = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(dynamicbrakecommandhaspriorityovercruisecontrol":
                    DynamicBrakeCommandHasPriorityOverCruiseControl = stf.ReadBoolBlock(true);
                    break;
                case "engine(ortscruisecontrol(hasindependentthrottledynamicbrakelever":
                    HasIndependentThrottleDynamicBrakeLever = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(hasproportionalspeedselector":
                    HasProportionalSpeedSelector = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(speedselectorisdiscrete":
                    SpeedSelectorIsDiscrete = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(usetrainbrakeanddynbrake":
                    UseTrainBrakeAndDynBrake = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(speeddeltatoenabletrainbrake":
                    SpeedDeltaToEnableTrainBrake = stf.ReadFloatBlock(STFReader.Units.Speed, 5f);
                    break;
                case "engine(ortscruisecontrol(speeddeltatoenablefulltrainbrake":
                    SpeedDeltaToEnableFullTrainBrake = stf.ReadFloatBlock(STFReader.Units.Speed, 10f);
                    break;
                case "engine(ortscruisecontrol(minimumspeedforcceffect":
                    MinimumSpeedForCCEffectMpS = stf.ReadFloatBlock(STFReader.Units.Speed, 0f);
                    break;
                case "engine(ortscruisecontrol(trainbrakeminpercentvalue":
                    TrainBrakeMinPercentValue = stf.ReadFloatBlock(STFReader.Units.Any, 0.3f);
                    break;
                case "engine(ortscruisecontrol(trainbrakemaxpercentvalue":
                    TrainBrakeMaxPercentValue = stf.ReadFloatBlock(STFReader.Units.Any, 0.85f);
                    break;
                case "engine(ortscruisecontrol(startinautomode":
                    StartInAutoMode = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(throttleneutralposition":
                    ThrottleNeutralPosition = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(throttlelowspeedposition":
                    ThrottleLowSpeedPosition = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(lowspeed":
                    LowSpeed = stf.ReadFloatBlock(STFReader.Units.Speed, 2f);
                    break;
                case "engine(ortscruisecontrol(hasttwoforcevalues":
                    HasTwoForceValues = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(docomputenumberofaxles":
                    DoComputeNumberOfAxles = stf.ReadBoolBlock(false);
                    break;
                case "engine(ortscruisecontrol(options":
                    foreach (string speedRegulatorString in stf.ReadStringBlock("").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        speedRegulatorOptions.Add(speedRegulatorString.ToLower());
                    }
                    break;
                case "engine(ortscruisecontrol(controllercruisecontrollogic":
                    {
                        string speedControlLogic = stf.ReadStringBlock("none").ToLower();
                        switch (speedControlLogic)
                        {
                            case "full":
                                {
                                    CruiseControlLogic = CruiseControlLogic.Full;
                                    break;
                                }
                            case "speedonly":
                                {
                                    CruiseControlLogic = CruiseControlLogic.SpeedOnly;
                                    break;
                                }
                        }
                        break;
                    }
            }
        }

        public CruiseControl(CruiseControl source, MSTSLocomotive locomotive)
        {
            ArgumentNullException.ThrowIfNull(source);

            simulator = Simulator.Instance;
            this.locomotive = locomotive;

            Equipped = source.Equipped;
            speedIsMph = source.speedIsMph;
            SpeedRegulatorMaxForcePercentUnits = source.SpeedRegulatorMaxForcePercentUnits;
            SpeedRegulatorMaxForceSteps = source.SpeedRegulatorMaxForceSteps;
            MaxForceSetSingleStep = source.MaxForceSetSingleStep;
            MaxForceKeepSelectedStepWhenManualModeSet = source.MaxForceKeepSelectedStepWhenManualModeSet;
            KeepSelectedSpeedWhenManualModeSet = source.KeepSelectedSpeedWhenManualModeSet;
            ForceRegulatorAutoWhenNonZeroSpeedSelected = source.ForceRegulatorAutoWhenNonZeroSpeedSelected;
            ForceRegulatorAutoWhenNonZeroForceSelected = source.ForceRegulatorAutoWhenNonZeroForceSelected;
            ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero = source.ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero;
            MaxForceSelectorIsDiscrete = source.MaxForceSelectorIsDiscrete;
            speedRegulatorOptions = source.speedRegulatorOptions;
            CruiseControlLogic = source.CruiseControlLogic;
            SpeedRegulatorNominalSpeedStepMpS = source.SpeedRegulatorNominalSpeedStepMpS;
            speedRegulatorNominalSpeedStepKpHOrMpH = source.speedRegulatorNominalSpeedStepKpHOrMpH;
            maxAccelerationMpSS = source.maxAccelerationMpSS;
            maxDecelerationMpSS = source.maxDecelerationMpSS;
            useThrottle = source.useThrottle;
            UseThrottleInCombinedControl = source.UseThrottleInCombinedControl;
            antiWheelSpinEquipped = source.antiWheelSpinEquipped;
            antiWheelSpinSpeedDiffThreshold = source.antiWheelSpinSpeedDiffThreshold;
            dynamicBrakeMaxForceAtSelectorStep = source.dynamicBrakeMaxForceAtSelectorStep;
            startReducingSpeedDelta = source.startReducingSpeedDelta;
            StartReducingSpeedDeltaDownwards = source.StartReducingSpeedDeltaDownwards;
            forceStepsThrottleTable = source.forceStepsThrottleTable;
            accelerationTable = source.accelerationTable;
            DynamicBrakeIncreaseSpeed = source.DynamicBrakeIncreaseSpeed;
            DynamicBrakeDecreaseSpeed = source.DynamicBrakeDecreaseSpeed;
            AccelerationRampMaxMpSSS = source.AccelerationRampMaxMpSSS;
            AccelerationRampMinMpSSS = source.AccelerationRampMinMpSSS;
            ResetForceAfterAnyBraking = source.ResetForceAfterAnyBraking;
            throttleFullRangeIncreaseTimeSeconds = source.throttleFullRangeIncreaseTimeSeconds;
            throttleFullRangeDecreaseTimeSeconds = source.throttleFullRangeDecreaseTimeSeconds;
            DynamicBrakeFullRangeIncreaseTimeSeconds = source.DynamicBrakeFullRangeIncreaseTimeSeconds;
            DynamicBrakeFullRangeDecreaseTimeSeconds = source.DynamicBrakeFullRangeDecreaseTimeSeconds;
            ParkingBrakeEngageSpeed = source.ParkingBrakeEngageSpeed;
            ParkingBrakePercent = source.ParkingBrakePercent;
            DisableZeroForceStep = source.DisableZeroForceStep;
            DynamicBrakeIsSelectedForceDependant = source.DynamicBrakeIsSelectedForceDependant;
            UseThrottleAsSpeedSelector = source.UseThrottleAsSpeedSelector;
            UseThrottleAsForceSelector = source.UseThrottleAsForceSelector;
            ContinuousSpeedIncreasing = source.ContinuousSpeedIncreasing;
            ContinuousSpeedDecreasing = source.ContinuousSpeedDecreasing;
            PowerBreakoutAmpers = source.PowerBreakoutAmpers;
            PowerBreakoutSpeedDelta = source.PowerBreakoutSpeedDelta;
            PowerResumeSpeedDelta = source.PowerResumeSpeedDelta;
            PowerReductionDelayPaxTrain = source.PowerReductionDelayPaxTrain;
            PowerReductionDelayCargoTrain = source.PowerReductionDelayCargoTrain;
            PowerReductionValue = source.PowerReductionValue;
            MaxPowerThreshold = source.MaxPowerThreshold;
            SafeSpeedForAutomaticOperationMpS = source.SafeSpeedForAutomaticOperationMpS;
            SpeedSelectorStepTimeSeconds = source.SpeedSelectorStepTimeSeconds;
            DisableCruiseControlOnThrottleAndZeroSpeed = source.DisableCruiseControlOnThrottleAndZeroSpeed;
            DisableCruiseControlOnThrottleAndZeroForce = source.DisableCruiseControlOnThrottleAndZeroForce;
            DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed = source.DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed;
            ForceResetRequiredAfterBraking = source.ForceResetRequiredAfterBraking;
            ForceResetIncludeDynamicBrake = source.ForceResetIncludeDynamicBrake;
            ZeroSelectedSpeedWhenPassingToThrottleMode = source.ZeroSelectedSpeedWhenPassingToThrottleMode;
            DynamicBrakeCommandHasPriorityOverCruiseControl = source.DynamicBrakeCommandHasPriorityOverCruiseControl;
            HasIndependentThrottleDynamicBrakeLever = source.HasIndependentThrottleDynamicBrakeLever;
            HasProportionalSpeedSelector = source.HasProportionalSpeedSelector;
            DisableManualSwitchToManualWhenSetForceNotAtZero = source.DisableManualSwitchToManualWhenSetForceNotAtZero;
            DisableManualSwitchToAutoWhenThrottleNotAtZero = source.DisableManualSwitchToAutoWhenThrottleNotAtZero;
            DisableManualSwitchToAutoWhenSetSpeedNotAtTop = source.DisableManualSwitchToAutoWhenSetSpeedNotAtTop;
            EnableSelectedSpeedSelectionWhenManualModeSet = source.EnableSelectedSpeedSelectionWhenManualModeSet;
            SpeedSelectorIsDiscrete = source.SpeedSelectorIsDiscrete;
            DoComputeNumberOfAxles = source.DoComputeNumberOfAxles;
            UseTrainBrakeAndDynBrake = source.UseTrainBrakeAndDynBrake;
            SpeedDeltaToEnableTrainBrake = source.SpeedDeltaToEnableTrainBrake;
            SpeedDeltaToEnableFullTrainBrake = source.SpeedDeltaToEnableFullTrainBrake;
            MinimumSpeedForCCEffectMpS = source.MinimumSpeedForCCEffectMpS;
            TrainBrakeMinPercentValue = source.TrainBrakeMinPercentValue;
            TrainBrakeMaxPercentValue = source.TrainBrakeMaxPercentValue;
            StartInAutoMode = source.StartInAutoMode;
            ThrottleNeutralPosition = source.ThrottleNeutralPosition;
            ThrottleLowSpeedPosition = source.ThrottleLowSpeedPosition;
            LowSpeed = source.LowSpeed;
            HasTwoForceValues = source.HasTwoForceValues;
        }

        public void Initialize()
        {
            clockTime = simulator.ClockTime * 100;
            ComputeNumberOfAxles();
            if (StartReducingSpeedDeltaDownwards == 0)
                StartReducingSpeedDeltaDownwards = startReducingSpeedDelta;
            if (StartInAutoMode)
                SpeedRegulatorMode = SpeedRegulatorMode.Auto;
        }

        private void ComputeNumberOfAxles()
        {
            if (DoComputeNumberOfAxles && locomotive == simulator.PlayerLocomotive)
            {
                SelectedNumberOfAxles = 0;
                foreach (TrainCar tc in locomotive.Train.Cars)
                {
                    SelectedNumberOfAxles += tc.WheelAxles.Count;
                }
            }
        }

        public void Update(double elapsedClockSeconds)
        {
            OverrideForceCalculation = false;
            if (!locomotive.IsPlayerTrain)
            {
                WasForceReset = false;
                throttleOrDynBrakePercent = 0;
                return;
            }

            UpdateSelectedSpeed(elapsedClockSeconds);

            if (!ThrottleNeutralPosition || SelectedSpeedMpS > 0)
                throttleNeutralPriority = false;

            if (locomotive.TrainBrakeController.TCSEmergencyBraking || locomotive.TrainBrakeController.TCSFullServiceBraking)
            {
                WasBraking = true;
            }
            else if (SpeedRegulatorMode == SpeedRegulatorMode.Manual || (SpeedRegulatorMode == SpeedRegulatorMode.Auto && (DynamicBrakePriority || throttleNeutralPriority)))
            {
                WasForceReset = false;
                throttleOrDynBrakePercent = 0;
            }
            else if (SpeedRegulatorMode == SpeedRegulatorMode.Auto)
            {
                if (ThrottleNeutralPosition && SelectedSpeedMpS == 0)
                {
                    // we are in the neutral position
                    throttleNeutralPriority = true;
                    locomotive.ThrottleController.SetPercent(0);
                    if (locomotive.DynamicBrakePercent != -1)
                    {
                        locomotive.SetDynamicBrakePercent(0);
                        locomotive.DynamicBrakeChangeActiveState(false);
                    }
                    throttleOrDynBrakePercent = 0;
                    WasForceReset = false;
                    locomotive.DynamicBrakeIntervention = -1;
                }
                else
                {
                    OverrideForceCalculation = true;
                }
            }

            if (SpeedRegulatorMode == SpeedRegulatorMode.Manual)
                SkipThrottleDisplay = false;

            RelativeAccelerationMpSS = locomotive.AccelerationMpSS;
            if (locomotive.Direction == MidpointDirection.Reverse)
                RelativeAccelerationMpSS *= -1;
            if (maxForceIncreasing)
                SpeedRegulatorMaxForceIncrease(elapsedClockSeconds);
            if (maxForceIncreasing)
                SpeedRegulatorMaxForceIncrease(elapsedClockSeconds);
            if (maxForceDecreasing)
            {
                if (SelectedMaxAccelerationStep <= 0)
                    maxForceDecreasing = false;
                else
                    SpeedRegulatorMaxForceDecrease(elapsedClockSeconds);
            }
        }

        public void Save(BinaryWriter outf)
        {
            outf.Write(this.applyingPneumaticBrake);
            outf.Write(this.battery);
            outf.Write(this.brakeIncreasing);
            outf.Write(this.clockTime);
            outf.Write(this.controllerTime);
            outf.Write(this.currentSelectedSpeedMpS);
            outf.Write(this.currentThrottlePercent);
            outf.Write(this.dynamicBrakeSetToZero);
            outf.Write(this.fromAcceleration);
            outf.Write(this.maxForceDecreasing);
            outf.Write(this.maxForceIncreasing);
            outf.Write(this.maxForceN);
            outf.Write(this.nextSelectedSpeedMps);
            outf.Write(this.restrictedRegionTravelledDistance);
            outf.Write(this.restrictedSpeedActive);
            outf.Write(this.SelectedMaxAccelerationPercent);
            outf.Write(this.SelectedMaxAccelerationStep);
            outf.Write(this.SelectedNumberOfAxles);
            outf.Write(this.SelectedSpeedMpS);
            outf.Write((int)this.SpeedRegulatorMode);
            outf.Write((int)this.SpeedSelectorMode);
            outf.Write(this.throttleIsZero);
            outf.Write(this.trainBrakePercent);
            outf.Write(this.trainLengthMeters);
            outf.Write(speedRegulatorIntermediateValue);
            outf.Write(CCIsUsingTrainBrake);
        }

        public void Restore(BinaryReader inf)
        {
            applyingPneumaticBrake = inf.ReadBoolean();
            battery = inf.ReadBoolean();
            brakeIncreasing = inf.ReadBoolean();
            clockTime = inf.ReadDouble();
            controllerTime = inf.ReadSingle();
            currentSelectedSpeedMpS = inf.ReadSingle();
            currentThrottlePercent = inf.ReadSingle();
            dynamicBrakeSetToZero = inf.ReadBoolean();
            fromAcceleration = inf.ReadSingle();
            maxForceDecreasing = inf.ReadBoolean();
            maxForceIncreasing = inf.ReadBoolean();
            maxForceN = inf.ReadDouble();
            nextSelectedSpeedMps = inf.ReadSingle();
            restrictedRegionTravelledDistance = inf.ReadSingle();
            restrictedSpeedActive = inf.ReadBoolean();
            SelectedMaxAccelerationPercent = inf.ReadSingle();
            SelectedMaxAccelerationStep = inf.ReadSingle();
            SelectedNumberOfAxles = inf.ReadInt32();
            SelectedSpeedMpS = inf.ReadSingle();
            SpeedRegulatorMode = (SpeedRegulatorMode)inf.ReadInt32();
            SpeedSelectorMode = (SpeedSelectorMode)inf.ReadInt32();
            throttleIsZero = inf.ReadBoolean();
            trainBrakePercent = inf.ReadSingle();
            trainLengthMeters = inf.ReadInt32();
            speedRegulatorIntermediateValue = inf.ReadDouble();
            CCIsUsingTrainBrake = inf.ReadBoolean();
        }

        public void UpdateSelectedSpeed(double elapsedClockSeconds)
        {
            totalTime += elapsedClockSeconds;
            if (SpeedRegulatorMode == SpeedRegulatorMode.Auto && !DynamicBrakePriority ||
             EnableSelectedSpeedSelectionWhenManualModeSet)
            {
                if (selectedSpeedIncreasing)
                    SpeedRegulatorSelectedSpeedIncrease();
                if (SelectedSpeedDecreasing)
                    SpeedRegulatorSelectedSpeedDecrease();
            }
        }

        public void SpeedRegulatorModeIncrease()
        {
            if (!locomotive.IsPlayerTrain)
                return;
            locomotive.SignalEvent(TrainEvent.CruiseControlSpeedRegulator);
            SpeedRegulatorMode previousMode = SpeedRegulatorMode;
            if (!Equipped)
                return;
            if (SpeedRegulatorMode == SpeedRegulatorMode.Testing)
                return;
            if (SpeedRegulatorMode == SpeedRegulatorMode.Manual &&
               ((DisableManualSwitchToAutoWhenThrottleNotAtZero && (locomotive.ThrottlePercent != 0 ||
               (locomotive.DynamicBrakePercent != -1 && locomotive.DynamicBrakePercent != 0))) ||
               (DisableManualSwitchToAutoWhenSetSpeedNotAtTop && SelectedSpeedMpS != locomotive.MaxSpeedMpS && locomotive.AbsSpeedMpS > Simulator.MaxStoppedMpS)))
                return;
            bool test = false;
            while (!test)
            {
                SpeedRegulatorMode++;
                switch (SpeedRegulatorMode)
                {
                    case SpeedRegulatorMode.Auto:
                        {
                            if (speedRegulatorOptions.Contains("regulatorauto"))
                                test = true;
                            if (!DisableManualSwitchToAutoWhenSetSpeedNotAtTop && !KeepSelectedSpeedWhenManualModeSet)
                                SelectedSpeedMpS = locomotive.AbsSpeedMpS;
                            break;
                        }
                    case SpeedRegulatorMode.Testing:
                        if (speedRegulatorOptions.Contains("regulatortest"))
                            test = true;
                        break;
                }
                if (!test && SpeedRegulatorMode == SpeedRegulatorMode.Testing) // if we're here, then it means no higher option, return to previous state and get out
                {
                    SpeedRegulatorMode = previousMode;
                    return;
                }
            }
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed regulator mode changed to {SpeedRegulatorMode.GetLocalizedDescription()}"));
        }

        public void SpeedRegulatorModeDecrease()
        {
            locomotive.SignalEvent(TrainEvent.CruiseControlSpeedRegulator);
            if (!Equipped)
                return;
            if (SpeedRegulatorMode == SpeedRegulatorMode.Manual)
                return;
            if (SpeedRegulatorMode == SpeedRegulatorMode.Auto &&
                (DisableManualSwitchToManualWhenSetForceNotAtZero && SelectedMaxAccelerationStep != 0))
                return;
            bool test = false;
            while (!test)
            {
                SpeedRegulatorMode--;
                switch (SpeedRegulatorMode)
                {
                    case SpeedRegulatorMode.Auto:
                        if (speedRegulatorOptions.Contains("regulatorauto"))
                            test = true;
                        break;
                    case SpeedRegulatorMode.Manual:
                        {
                            locomotive.ThrottleController.SetPercent(0);
                            currentThrottlePercent = 0;
                            if (speedRegulatorOptions.Contains("regulatormanual"))
                                test = true;
                            if (ZeroSelectedSpeedWhenPassingToThrottleMode)
                                SelectedSpeedMpS = 0;
                            foreach (MSTSLocomotive locomotive in playerNotDriveableTrainLocomotives)
                            {
                                ThrottleOverriden = 0;
                                IsAPartOfPlayerTrain = false; // in case we uncouple the loco later
                            }
                            break;
                        }
                }
                if (!test && SpeedRegulatorMode == SpeedRegulatorMode.Manual)
                    return;
            }
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed regulator mode changed to {SpeedRegulatorMode.GetLocalizedDescription()}"));
        }

        public void SpeedSelectorModeStartIncrease()
        {
            locomotive.SignalEvent(TrainEvent.CruiseControlSpeedSelector);
            if (!Equipped)
                return;
            if (SpeedSelectorMode == SpeedSelectorMode.Start)
                return;
            bool test = false;
            while (!test)
            {
                SpeedSelectorMode++;
                if (SpeedSelectorMode != SpeedSelectorMode.Parking && !EngineBrakePriority)
                    locomotive.SetEngineBrakePercent(0);
                switch (SpeedSelectorMode)
                {
                    case SpeedSelectorMode.Neutral:
                        if (speedRegulatorOptions.Contains("selectorneutral"))
                            test = true;
                        break;
                    case SpeedSelectorMode.On:
                        if (speedRegulatorOptions.Contains("selectoron"))
                            test = true;
                        break;
                    case SpeedSelectorMode.Start:
                        if (speedRegulatorOptions.Contains("selectorstart"))
                            test = true;
                        break;
                }
                if (!test && SpeedSelectorMode == SpeedSelectorMode.Start)
                    return;
            }
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed selector mode changed to {SpeedSelectorMode.GetLocalizedDescription()}"));
        }

        public void SpeedSelectorModeStopIncrease()
        {
            locomotive.SignalEvent(TrainEvent.CruiseControlSpeedSelector);
            if (!Equipped)
                return;
            if (SpeedSelectorMode == SpeedSelectorMode.Start)
            {
                bool test = false;
                while (!test)
                {
                    SpeedSelectorMode--;
                    switch (SpeedSelectorMode)
                    {
                        case SpeedSelectorMode.On:
                            if (speedRegulatorOptions.Contains("selectoron"))
                                test = true;
                            break;
                        case SpeedSelectorMode.Neutral:
                            if (speedRegulatorOptions.Contains("selectorneutral"))
                                test = true;
                            break;
                        case SpeedSelectorMode.Parking:
                            if (speedRegulatorOptions.Contains("selectorparking"))
                                test = true;
                            break;
                    }
                    if (!test && SpeedSelectorMode == SpeedSelectorMode.Parking && !EngineBrakePriority)
                        return;
                }
            }
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed selector mode changed to {SpeedSelectorMode.GetLocalizedDescription()}"));
        }

        public void SpeedSelectorModeDecrease()
        {
            locomotive.SignalEvent(TrainEvent.CruiseControlSpeedSelector);
            SpeedSelectorMode previousMode = SpeedSelectorMode;
            if (!Equipped)
                return;
            if (SpeedSelectorMode == SpeedSelectorMode.Parking && !EngineBrakePriority)
                return;
            bool test = false;
            while (!test)
            {
                SpeedSelectorMode--;
                switch (SpeedSelectorMode)
                {
                    case SpeedSelectorMode.On:
                        if (speedRegulatorOptions.Contains("selectoron"))
                            test = true;
                        break;
                    case SpeedSelectorMode.Neutral:
                        if (speedRegulatorOptions.Contains("selectorneutral"))
                            test = true;
                        break;
                    case SpeedSelectorMode.Parking:
                        if (speedRegulatorOptions.Contains("selectorparking"))
                            test = true;
                        break;
                }
                if (!test && SpeedSelectorMode == SpeedSelectorMode.Parking && !EngineBrakePriority)
                {
                    SpeedSelectorMode = previousMode;
                    return;
                }
            }
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed selector mode changed to {SpeedSelectorMode.GetLocalizedDescription()}"));
        }

        public void SetMaxForcePercent(float percent)
        {
            if (SelectedMaxAccelerationPercent == percent)
                return;
            SelectedMaxAccelerationPercent = percent;
            SelectedMaxAccelerationStep = (float)Math.Round(SelectedMaxAccelerationPercent * SpeedRegulatorMaxForceSteps / 100, 0);
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed regulator max acceleration percent changed to {SelectedMaxAccelerationPercent:F0}%"));
        }

        public void SpeedRegulatorMaxForceStartIncrease()
        {
            if (SelectedMaxAccelerationStep == 0)
            {
                locomotive.SignalEvent(TrainEvent.LeverFromZero);
            }
            locomotive.SignalEvent(TrainEvent.CruiseControlMaxForce);
            if (SelectedMaxAccelerationStep == 0 && DisableCruiseControlOnThrottleAndZeroForce && ForceRegulatorAutoWhenNonZeroForceSelected &&
                locomotive.ThrottleController.CurrentValue == 0 && locomotive.DynamicBrakeController.CurrentValue == 0 && locomotive.CruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Manual)
            {
                SpeedRegulatorMode = SpeedRegulatorMode.Auto;
                WasForceReset = true;
            }
            maxForceIncreasing = true;
            speedRegulatorIntermediateValue = SpeedRegulatorMaxForcePercentUnits ? SelectedMaxAccelerationPercent : SelectedMaxAccelerationStep;
        }

        public void SpeedRegulatorMaxForceStopIncrease()
        {
            maxForceIncreasing = false;
        }

        protected void SpeedRegulatorMaxForceIncrease(double elapsedClockSeconds)
        {
            locomotive.SignalEvent(TrainEvent.CruiseControlMaxForce);
            if (MaxForceSetSingleStep)
                maxForceIncreasing = false;
            if (SelectedMaxAccelerationStep == 0.5f)
                SelectedMaxAccelerationStep = 0;
            if (!Equipped)
                return;
            if (SpeedRegulatorMaxForcePercentUnits)
            {
                if (SelectedMaxAccelerationPercent == 100)
                    return;
                speedRegulatorIntermediateValue += StepSize * elapsedClockSeconds;
                SelectedMaxAccelerationPercent = (float)Math.Truncate(speedRegulatorIntermediateValue + 1);
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed regulator max acceleration percent changed to {SelectedMaxAccelerationPercent:F0}%"));
            }
            else
            {
                if (SelectedMaxAccelerationStep == SpeedRegulatorMaxForceSteps)
                    return;
                speedRegulatorIntermediateValue += MaxForceSelectorIsDiscrete ? elapsedClockSeconds : StepSize * elapsedClockSeconds * SpeedRegulatorMaxForceSteps / 100.0f;
                SelectedMaxAccelerationStep = (float)Math.Truncate(speedRegulatorIntermediateValue + 1);
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed regulator max acceleration changed to {Math.Round(SelectedMaxAccelerationStep * 100 / SpeedRegulatorMaxForceSteps, 0)}"));
            }
        }

        public void SpeedRegulatorMaxForceStartDecrease()
        {
            locomotive.SignalEvent(TrainEvent.CruiseControlMaxForce);
            maxForceDecreasing = true;
            speedRegulatorIntermediateValue = SpeedRegulatorMaxForcePercentUnits ? SelectedMaxAccelerationPercent : SelectedMaxAccelerationStep;
        }

        public void SpeedRegulatorMaxForceStopDecrease()
        {
            maxForceDecreasing = false;
        }

        protected void SpeedRegulatorMaxForceDecrease(double elapsedClockSeconds)
        {
            locomotive.SignalEvent(TrainEvent.CruiseControlMaxForce);
            if (MaxForceSetSingleStep)
                maxForceDecreasing = false;
            if (!Equipped)
                return;
            if (DisableZeroForceStep)
            {
                if (SelectedMaxAccelerationStep <= 1)
                    return;
            }
            else
            {
                if (SelectedMaxAccelerationStep <= 0)
                    return;
            }
            speedRegulatorIntermediateValue -= MaxForceSelectorIsDiscrete ? elapsedClockSeconds : StepSize * elapsedClockSeconds * SpeedRegulatorMaxForceSteps / 100.0f;
            SelectedMaxAccelerationStep = (float)Math.Truncate(speedRegulatorIntermediateValue);
            if (DisableZeroForceStep)
            {
                if (SelectedMaxAccelerationStep <= 1)
                {
                    locomotive.SignalEvent(TrainEvent.LeverToZero);
                }
            }
            else
            {
                if (SelectedMaxAccelerationStep <= 0)
                {
                    locomotive.SignalEvent(TrainEvent.LeverToZero);
                }
            }
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed regulator max acceleration changed to {Math.Round(SelectedMaxAccelerationStep * 100 / SpeedRegulatorMaxForceSteps, 0)}"));
        }

        public void SpeedRegulatorMaxForceChangeByMouse(float movExtension, float maxValue)
        {
            if (movExtension != 0 && SelectedMaxAccelerationStep == 0 && DisableCruiseControlOnThrottleAndZeroForce && ForceRegulatorAutoWhenNonZeroForceSelected &&
                locomotive.ThrottleController.CurrentValue == 0 && locomotive.DynamicBrakeController.CurrentValue == 0 && SpeedRegulatorMode == SpeedRegulatorMode.Manual)
            {
                SpeedRegulatorMode = SpeedRegulatorMode.Auto;
                WasForceReset = true;
            }
            if (SelectedMaxAccelerationStep == 0)
            {
                if (movExtension > 0)
                {
                    locomotive.SignalEvent(TrainEvent.LeverFromZero);
                }
                else if (movExtension < 0)
                    return;
            }
            if (movExtension == 1)
            {
                SelectedMaxAccelerationStep += 1;
            }
            if (movExtension == -1)
            {
                SelectedMaxAccelerationStep -= 1;
            }
            if (movExtension != 0)
            {
                SelectedMaxAccelerationStep += movExtension * maxValue;
                if (SelectedMaxAccelerationStep > SpeedRegulatorMaxForceSteps)
                    SelectedMaxAccelerationStep = SpeedRegulatorMaxForceSteps;
                if (SelectedMaxAccelerationStep < 0)
                    SelectedMaxAccelerationStep = 0;
                if (SelectedMaxAccelerationStep == 0)
                {
                    locomotive.SignalEvent(TrainEvent.LeverToZero);
                }
                simulator.Confirmer.Information($"Selected maximum acceleration was changed to {Math.Round((MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps, 0)} %");
            }
        }

        public void SpeedRegulatorSelectedSpeedStartIncrease()
        {
            if (locomotive.MultiPositionControllers != null)
            {
                foreach (Controllers.MultiPositionController mpc in locomotive.MultiPositionControllers)
                {
                    if (mpc.ControllerBinding != CruiseControllerBinding.SelectedSpeed)
                        return;
                    if (!mpc.StateChanged)
                    {
                        mpc.StateChanged = true;
                        if (SpeedRegulatorMode != SpeedRegulatorMode.Auto && (ForceRegulatorAutoWhenNonZeroSpeedSelected ||
                            SelectedMaxAccelerationStep == 0 && DisableCruiseControlOnThrottleAndZeroForce && ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero &&
                            locomotive.ThrottleController.CurrentValue == 0 && locomotive.DynamicBrakeController.CurrentValue == 0))
                        {
                            SpeedRegulatorMode = SpeedRegulatorMode.Auto;
                        }

                        mpc.DoMovement(Movement.Forward);
                        return;
                    }
                }
            }
            if (SpeedRegulatorMode != SpeedRegulatorMode.Auto && (ForceRegulatorAutoWhenNonZeroSpeedSelected || HasProportionalSpeedSelector &&
                SelectedMaxAccelerationStep == 0 && DisableCruiseControlOnThrottleAndZeroForce && ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero &&
                            locomotive.ThrottleController.CurrentValue == 0 && locomotive.DynamicBrakeController.CurrentValue == 0))
            {
                SpeedRegulatorMode = SpeedRegulatorMode.Auto;
            }
            if (SpeedRegulatorMode != SpeedRegulatorMode.Auto && (ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero &&
                SelectedMaxAccelerationStep == 0 && DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed &&
                            locomotive.ThrottleController.CurrentValue == 0 && locomotive.DynamicBrakeController.CurrentValue == 0))
            {
                SpeedRegulatorMode = SpeedRegulatorMode.Auto;
            }
            if (UseThrottleAsSpeedSelector || HasProportionalSpeedSelector)
            {
                selectedSpeedIncreasing = true;
                if (SelectedSpeedMpS == 0)
                {
                    locomotive.SignalEvent(TrainEvent.LeverFromZero);
                }
            }
            else
                SpeedSelectorModeStartIncrease();
        }

        public void SpeedRegulatorSelectedSpeedStopIncrease()
        {
            if (locomotive.MultiPositionControllers != null)
            {
                foreach (Controllers.MultiPositionController mpc in locomotive.MultiPositionControllers)
                {
                    if (mpc.ControllerBinding != CruiseControllerBinding.SelectedSpeed)
                        return;
                    mpc.StateChanged = false;
                    mpc.DoMovement(Movement.Neutral);
                    return;
                }
            }
            if (UseThrottleAsSpeedSelector || HasProportionalSpeedSelector)
                selectedSpeedIncreasing = false;
            else
                SpeedSelectorModeStopIncrease();
        }

        public void SpeedRegulatorSelectedSpeedIncrease()
        {
            if (!Equipped)
                return;

            if (selectedSpeedLeverHoldTime + SpeedSelectorStepTimeSeconds > totalTime)
                return;
            selectedSpeedLeverHoldTime = totalTime;

            SelectedSpeedMpS = Math.Max(MinimumSpeedForCCEffectMpS, SelectedSpeedMpS + SpeedRegulatorNominalSpeedStepMpS);
            if (SelectedSpeedMpS > locomotive.MaxSpeedMpS)
                SelectedSpeedMpS = locomotive.MaxSpeedMpS;
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Selected speed changed to {Math.Round(Speed.MeterPerSecond.FromMpS(SelectedSpeedMpS, !speedIsMph), 0, MidpointRounding.AwayFromZero)} {(speedIsMph ? FormatStrings.mph : FormatStrings.kmph)}"));
        }

        public void SpeedRegulatorSelectedSpeedStartDecrease()
        {
            if (locomotive.MultiPositionControllers != null)
            {
                foreach (Controllers.MultiPositionController mpc in locomotive.MultiPositionControllers)
                {
                    if (mpc.ControllerBinding != CruiseControllerBinding.SelectedSpeed)
                        return;
                    if (!mpc.StateChanged)
                    {
                        mpc.StateChanged = true;
                        mpc.DoMovement(Movement.Backward);
                        return;
                    }
                }
            }
            if (UseThrottleAsSpeedSelector || HasProportionalSpeedSelector)
                SelectedSpeedDecreasing = true;
            else
                SpeedSelectorModeDecrease();
        }
        public void SpeedRegulatorSelectedSpeedStopDecrease()
        {
            if (locomotive.MultiPositionControllers != null)
            {
                foreach (Controllers.MultiPositionController mpc in locomotive.MultiPositionControllers)
                {
                    if (mpc.ControllerBinding != CruiseControllerBinding.SelectedSpeed)
                        return;
                    mpc.StateChanged = false;
                    mpc.DoMovement(Movement.Neutral);
                    return;
                }
            }
            SelectedSpeedDecreasing = false;
        }
        public void SpeedRegulatorSelectedSpeedDecrease()
        {
            if (!Equipped)
                return;

            if (selectedSpeedLeverHoldTime + SpeedSelectorStepTimeSeconds > totalTime)
                return;
            selectedSpeedLeverHoldTime = totalTime;
            if (SelectedSpeedMpS == 0)
                return;
            SelectedSpeedMpS -= SpeedRegulatorNominalSpeedStepMpS;
            if (SelectedSpeedMpS < 0)
                SelectedSpeedMpS = 0f;
            if (MinimumSpeedForCCEffectMpS > 0 && SelectedSpeedMpS < MinimumSpeedForCCEffectMpS)
                SelectedSpeedMpS = 0;
            if (SpeedRegulatorMode == SpeedRegulatorMode.Auto && ForceRegulatorAutoWhenNonZeroSpeedSelected && SelectedSpeedMpS == 0)
            {
                // return back to manual, clear all we have controlled before and let the driver to set up new stuff
                SpeedRegulatorMode = SpeedRegulatorMode.Manual;
                DynamicBrakePriority = false;
                //                Locomotive.ThrottleController.SetPercent(0);
                //                Locomotive.SetDynamicBrakePercent(0);
                locomotive.DynamicBrakeChangeActiveState(false);
            }
            if (SelectedSpeedMpS == 0)
            {
                if (HasProportionalSpeedSelector)
                {
                    locomotive.SignalEvent(TrainEvent.LeverToZero);
                }
                SelectedSpeedDecreasing = false;
            }
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Selected speed changed to {Math.Round(Speed.MeterPerSecond.FromMpS(SelectedSpeedMpS, !speedIsMph), 0, MidpointRounding.AwayFromZero)} {(speedIsMph ? FormatStrings.mph : FormatStrings.kmph)}"));
        }

        public void SpeedRegulatorSelectedSpeedChangeByMouse(float movExtension, bool metric, float maxValue)
        {
            if (movExtension != 0 && SelectedMaxAccelerationStep == 0 && DisableCruiseControlOnThrottleAndZeroForce && ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero &&
            locomotive.ThrottleController.CurrentValue == 0 && locomotive.DynamicBrakeController.CurrentValue == 0 && SpeedRegulatorMode == SpeedRegulatorMode.Manual)
                SpeedRegulatorMode = SpeedRegulatorMode.Auto;
            if (movExtension != 0)
            {
                if (SelectedSpeedMpS == 0)
                {
                    if (movExtension > 0)
                    {
                        locomotive.SignalEvent(TrainEvent.LeverFromZero);
                    }
                    else if (movExtension < 0)
                        return;
                }
                double deltaSpeed = SpeedSelectorIsDiscrete ? (metric ? Speed.MeterPerSecond.FromKpH((float)Math.Round(movExtension * maxValue / speedRegulatorNominalSpeedStepKpHOrMpH) * speedRegulatorNominalSpeedStepKpHOrMpH) :
                    Speed.MeterPerSecond.FromMpH((float)Math.Round(movExtension * maxValue / speedRegulatorNominalSpeedStepKpHOrMpH) * speedRegulatorNominalSpeedStepKpHOrMpH)) :
                    (metric ? Speed.MeterPerSecond.FromKpH((float)Math.Round(movExtension * maxValue)) :
                    Speed.MeterPerSecond.FromMpH((float)Math.Round(movExtension * maxValue)));
                if (deltaSpeed > 0)
                    SelectedSpeedMpS = (float)Math.Max(SelectedSpeedMpS + deltaSpeed, MinimumSpeedForCCEffectMpS);
                else
                {
                    SelectedSpeedMpS += (float)deltaSpeed;
                    if (MinimumSpeedForCCEffectMpS > 0 && SelectedSpeedMpS < MinimumSpeedForCCEffectMpS)
                        SelectedSpeedMpS = 0;
                }

                if (SelectedSpeedMpS > locomotive.MaxSpeedMpS)
                    SelectedSpeedMpS = locomotive.MaxSpeedMpS;
                if (SelectedSpeedMpS < 0)
                    SelectedSpeedMpS = 0;
                if (SelectedSpeedMpS == 0 && movExtension < 0)
                {
                    locomotive.SignalEvent(TrainEvent.LeverToZero);
                }
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Selected speed changed to {Math.Round(Speed.MeterPerSecond.FromMpS(SelectedSpeedMpS, !speedIsMph), 0, MidpointRounding.AwayFromZero)} {(speedIsMph ? FormatStrings.mph : FormatStrings.kmph)}"));
            }
        }

        public void NumerOfAxlesIncrease()
        {
            NumerOfAxlesIncrease(1);
        }

        public void NumerOfAxlesIncrease(int ByAmount)
        {
            SelectedNumberOfAxles += ByAmount;
            trainLength = SelectedNumberOfAxles * 6.6f;
            trainLengthMeters = (int)Math.Round(trainLength + 0.5, 0);
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Number of axles increased to {SelectedNumberOfAxles}"));
        }

        public void NumberOfAxlesDecrease()
        {
            NumberOfAxlesDecrease(1);
        }

        public void NumberOfAxlesDecrease(int ByAmount)
        {
            if ((SelectedNumberOfAxles - ByAmount) < 1)
                return;
            SelectedNumberOfAxles -= ByAmount;
            trainLength = SelectedNumberOfAxles * 6.6f;
            trainLengthMeters = (int)Math.Round(trainLength + 0.5, 0);
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Number of axles decreased to {SelectedNumberOfAxles}"));
        }

        public void ActivateRestrictedSpeedZone()
        {
            remainingTrainLengthToPassRestrictedZone = trainLengthMeters;
            if (!restrictedSpeedActive)
            {
                restrictedRegionTravelledDistance = simulator.PlayerLocomotive.Train.DistanceTravelledM;
                currentSelectedSpeedMpS = SelectedSpeedMpS;
                restrictedSpeedActive = true;
            }
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Speed restricted zone active."));
        }

        public virtual void CheckRestrictedSpeedZone()
        {
            remainingTrainLengthToPassRestrictedZone = (int)Math.Round((simulator.PlayerLocomotive.Train.DistanceTravelledM - restrictedRegionTravelledDistance));
            if (remainingTrainLengthToPassRestrictedZone < 0)
                remainingTrainLengthToPassRestrictedZone = 0;
            if ((simulator.PlayerLocomotive.Train.DistanceTravelledM - restrictedRegionTravelledDistance) >= trainLength)
            {
                restrictedSpeedActive = false;
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Speed restricted zone off."));
                locomotive.SignalEvent(TrainEvent.CruiseControlAlert);
            }
        }

        public void SetSpeed(float speed)
        {
            if (!Equipped)
                return;
            if (SpeedRegulatorMode == SpeedRegulatorMode.Manual && (ForceRegulatorAutoWhenNonZeroSpeedSelected || ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero))
                SpeedRegulatorMode = SpeedRegulatorMode.Auto;
            if (SpeedRegulatorMode == SpeedRegulatorMode.Manual)
                return;
            locomotive.SignalEvent(TrainEvent.CruiseControlAlert1);
            double requiredSpeedMpS = speedIsMph ? Speed.MeterPerSecond.FromMpH(speed) : Speed.MeterPerSecond.FromKpH(speed);
            if (MinimumSpeedForCCEffectMpS == 0)
                SelectedSpeedMpS = (float)requiredSpeedMpS;
            else if (requiredSpeedMpS > SelectedSpeedMpS)
                SelectedSpeedMpS = (float)Math.Max(requiredSpeedMpS, MinimumSpeedForCCEffectMpS);
            else if (requiredSpeedMpS < MinimumSpeedForCCEffectMpS)
                SelectedSpeedMpS = 0;
            else
                SelectedSpeedMpS = (float)requiredSpeedMpS;
            if (SelectedSpeedMpS > locomotive.MaxSpeedMpS)
                SelectedSpeedMpS = locomotive.MaxSpeedMpS;
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Selected speed set to {speed} {(speedIsMph ? FormatStrings.mph : FormatStrings.kmph)}"));
        }

        public virtual void UpdateMotiveForce(double elapsedClockSeconds, float absWheelSpeedMpS)
        {
            if (absMaxForceN == 0)
                absMaxForceN = locomotive.MaxForceN;

            if (locomotive.DynamicBrakePercent > 0)
                if (locomotive.DynamicBrakePercent > 100)
                    locomotive.DynamicBrakePercent = 100;
            forceThrottleAndDynamicBrake = locomotive.DynamicBrakePercent;

            if (DynamicBrakeFullRangeIncreaseTimeSeconds == 0)
                DynamicBrakeFullRangeIncreaseTimeSeconds = 4;
            if (DynamicBrakeFullRangeDecreaseTimeSeconds == 0)
                DynamicBrakeFullRangeDecreaseTimeSeconds = 6;
            float speedDiff = absWheelSpeedMpS - locomotive.AbsSpeedMpS;
            foreach (MSTSLocomotive loco in playerNotDriveableTrainLocomotives)
            {
                if ((loco.AbsWheelSpeedMpS - loco.AbsSpeedMpS) > speedDiff)
                    speedDiff = loco.AbsWheelSpeedMpS - loco.AbsSpeedMpS;
            }
            float newThrotte = 0;
            // calculate new max force if MaxPowerThreshold is set
            if (MaxPowerThreshold > 0)
            {
                double currentSpeed = speedIsMph ? Speed.MeterPerSecond.ToMpH(absWheelSpeedMpS) : Speed.MeterPerSecond.ToKpH(absWheelSpeedMpS);
                float percentComplete = (int)Math.Round((double)(100 * currentSpeed) / MaxPowerThreshold);
                if (percentComplete > 100)
                    percentComplete = 100;
                newThrotte = percentComplete;
            }

            int count = 0;
            TrainElevation = 0;
            foreach (TrainCar tc in locomotive.Train.Cars)
            {
                count++;
                TrainElevation += tc.Flipped ? tc.CurrentElevationPercent : -tc.CurrentElevationPercent;
            }
            TrainElevation /= count;

            if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Release ||
                locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Neutral)
            {
                if (TrainBrakePriority && SelectedMaxAccelerationStep > 0 && ForceResetRequiredAfterBraking)
                {
                    if (locomotive.DynamicBrakePercent > 0 && SelectedSpeedMpS > 0)
                        locomotive.SetDynamicBrakePercent(0);
                    throttleOrDynBrakePercent = 0;
                    locomotive.ThrottlePercent = 0;
                    return;
                }
                TrainBrakePriority = false;
            }
            if (DynamicBrakePriority)
                throttleOrDynBrakePercent = 0;
            {

                if (TrainBrakePriority || DynamicBrakePriority)
                {
                    WasForceReset = false;
                    WasBraking = true;
                }

                if ((SpeedSelectorMode == SpeedSelectorMode.On || SpeedSelectorMode == SpeedSelectorMode.Start) && !TrainBrakePriority)
                {
                    canAddForce = true;
                }
                else
                {
                    canAddForce = false;
                    timeFromEngineMoved = 0;
                    reducingForce = true;
                    locomotive.TractiveForceN = 0;
                    if (TrainBrakePriority)
                    {
                        if (SpeedSelectorMode == SpeedSelectorMode.Parking)
                            if (absWheelSpeedMpS < (speedIsMph ? Speed.MeterPerSecond.FromMpH(ParkingBrakeEngageSpeed) : Speed.MeterPerSecond.FromKpH(ParkingBrakeEngageSpeed)))
                                locomotive.SetEngineBrakePercent(ParkingBrakePercent);
                        if (locomotive.DynamicBrakePercent > 0 && SelectedSpeedMpS > 0)
                            locomotive.SetDynamicBrakePercent(0);
                        throttleOrDynBrakePercent = 0;
                        locomotive.ThrottlePercent = 0;
                        return;
                    }
                }

                if ((SelectedMaxAccelerationStep == 0 && SelectedMaxAccelerationPercent == 0) || SpeedSelectorMode == SpeedSelectorMode.Start)
                    WasForceReset = true;

                if (SelectedMaxAccelerationPercent == 0 && SelectedMaxAccelerationStep == 0)
                {
                    WasBraking = false;
                    if (SpeedRegulatorMode == SpeedRegulatorMode.Auto && UseThrottleAsForceSelector)
                        locomotive.ThrottleController.SetPercent(0);
                    locomotive.SetThrottlePercent(0);
                }
                if (ForceResetRequiredAfterBraking && WasBraking && (SelectedMaxAccelerationStep > 0 || SelectedMaxAccelerationPercent > 0))
                {
                    locomotive.SetThrottlePercent(0);
                    throttleOrDynBrakePercent = 0;
                    maxForceN = 0;
                    if (SpeedSelectorMode == SpeedSelectorMode.Parking)
                        if (absWheelSpeedMpS < (speedIsMph ? Speed.MeterPerSecond.FromMpH(ParkingBrakeEngageSpeed) : Speed.MeterPerSecond.FromKpH(ParkingBrakeEngageSpeed)))
                            locomotive.SetEngineBrakePercent(ParkingBrakePercent);
                    return;
                }

                if (ForceResetRequiredAfterBraking && !WasForceReset)
                {
                    locomotive.SetThrottlePercent(0);
                    throttleOrDynBrakePercent = 0;
                    maxForceN = 0;
                    if (SpeedSelectorMode == SpeedSelectorMode.Parking)
                        if (absWheelSpeedMpS < (speedIsMph ? Speed.MeterPerSecond.FromMpH(ParkingBrakeEngageSpeed) : Speed.MeterPerSecond.FromKpH(ParkingBrakeEngageSpeed)))
                            locomotive.SetEngineBrakePercent(ParkingBrakePercent);
                    return;
                }


                if (canAddForce)
                {
                    if (locomotive.AbsSpeedMpS == 0)
                    {
                        timeFromEngineMoved = 0;
                        reducingForce = true;
                    }
                    else if (reducingForce)
                    {

                        timeFromEngineMoved += elapsedClockSeconds;
                        float timeToReduce = locomotive.SelectedTrainType == TrainCategory.Passenger ? PowerReductionDelayPaxTrain : PowerReductionDelayCargoTrain;
                        if (timeFromEngineMoved > timeToReduce)
                            reducingForce = false;
                    }
                }
                if (UseTrainBrakeAndDynBrake && CCIsUsingTrainBrake)
                {
                    canAddForce = true;
                }
                else if (Pressure.Atmospheric.FromPSI(locomotive.BrakeSystem.BrakeLine1PressurePSI) < 4.98)
                {
                    canAddForce = false;
                    reducingForce = true;
                    timeFromEngineMoved = 0;
                    maxForceN = 0;
                    if (throttleOrDynBrakePercent > 0)
                        throttleOrDynBrakePercent = 0;
                    Ampers = 0;
                    locomotive.ThrottleController.SetPercent(0);
                    return;
                }
                else if (Pressure.Atmospheric.FromPSI(locomotive.BrakeSystem.BrakeLine1PressurePSI) > 4.7)
                {
                    canAddForce = true;
                }

                if (speedRegulatorOptions.Contains("engageforceonnonzerospeed") && SelectedSpeedMpS > 0)
                {
                    SpeedSelectorMode = SpeedSelectorMode.On;
                    SpeedRegulatorMode = SpeedRegulatorMode.Auto;
                    SkipThrottleDisplay = true;
                    reducingForce = false;
                }
                /*           if (SpeedRegulatorOptions.Contains("engageforceonnonzerospeed") && SelectedSpeedMpS == 0)
                           {
                               if (playerNotDriveableTrainLocomotives.Count > 0) // update any other than the player's locomotive in the consist throttles to percentage of the current force and the max force
                               {
                                   foreach (MSTSLocomotive lc in playerNotDriveableTrainLocomotives)
                                   {
                                       if (UseThrottle)
                                       {
                                           lc.SetThrottlePercent(0);
                                       }
                                       else
                                       {
                                           lc.IsAPartOfPlayerTrain = true;
                                           lc.ThrottleOverriden = 0;
                                       }
                                   }
                               }
                               Locomotive.TractiveForceN = Locomotive.MotiveForceN = 0;
                               Locomotive.SetThrottlePercent(0);
                               return;
                           }*/

                float t = 0;
                if (SpeedRegulatorMode == SpeedRegulatorMode.Manual)
                    DynamicBrakePriority = false;

                if (restrictedSpeedActive)
                    CheckRestrictedSpeedZone();
                if (DynamicBrakePriority)
                {
                    locomotive.ThrottleController.SetPercent(0);
                    forceThrottleAndDynamicBrake = -locomotive.DynamicBrakePercent;
                    return;
                }

                if (firstIteration) // if this is executed the first time, let's check all other than player engines in the consist, and record them for further throttle manipulation
                {
                    if (!DoComputeNumberOfAxles)
                        SelectedNumberOfAxles = (int)(locomotive.Train.Length / 6.6f); // also set the axles, for better delta computing, if user omits to set it
                    foreach (MSTSLocomotive loco in locomotive.Train.Cars.OfType<MSTSLocomotive>())
                    {
                        playerNotDriveableTrainLocomotives.Add(loco);
                    }
                    firstIteration = false;
                }

                if (SelectedMaxAccelerationStep == 0) // no effort, no throttle (i.e. for reverser change, etc) and return
                {
                    locomotive.SetThrottlePercent(0);
                    if (locomotive.DynamicBrakePercent > 0)
                        locomotive.SetDynamicBrakePercent(0);
                }

                if (SpeedRegulatorMode == SpeedRegulatorMode.Auto)
                {
                    if (SpeedSelectorMode == SpeedSelectorMode.Parking && !EngineBrakePriority)
                    {
                        if (locomotive.DynamicBrakePercent > 0)
                        {
                            if (absWheelSpeedMpS == 0)
                            {
                                locomotive.SetDynamicBrakePercent(0);
                                locomotive.DynamicBrakeChangeActiveState(false);
                            }
                        }
                        if (!useThrottle)
                            locomotive.ThrottleController.SetPercent(0);
                        throttleIsZero = true;

                        if (absWheelSpeedMpS < (speedIsMph ? Speed.MeterPerSecond.FromMpH(ParkingBrakeEngageSpeed) : Speed.MeterPerSecond.FromKpH(ParkingBrakeEngageSpeed)))
                            locomotive.SetEngineBrakePercent(ParkingBrakePercent);
                    }
                    else if (SpeedSelectorMode == SpeedSelectorMode.Neutral || SpeedSelectorMode < SpeedSelectorMode.Start && !speedRegulatorOptions.Contains("startfromzero") && absWheelSpeedMpS < SafeSpeedForAutomaticOperationMpS)
                    {
                        if (throttleOrDynBrakePercent > 0)
                        {
                            double step = 100 / throttleFullRangeIncreaseTimeSeconds;
                            step *= elapsedClockSeconds;
                            throttleOrDynBrakePercent -= (float)step;
                            if (throttleOrDynBrakePercent < 0)
                                throttleOrDynBrakePercent = 0;
                            if (throttleOrDynBrakePercent > 0 && throttleOrDynBrakePercent < 0.1)
                                throttleOrDynBrakePercent = 0;
                        }

                        float deltaSpeedMpS = 0;
                        if (!restrictedSpeedActive)
                            deltaSpeedMpS = SelectedSpeedMpS - absWheelSpeedMpS;
                        else
                            deltaSpeedMpS = currentSelectedSpeedMpS - absWheelSpeedMpS;

                        if (deltaSpeedMpS > 0)
                        {
                            if (throttleOrDynBrakePercent < -0.1)
                            {
                                double step = 100 / throttleFullRangeDecreaseTimeSeconds;
                                step *= elapsedClockSeconds;
                                throttleOrDynBrakePercent += step;
                            }
                            else if (throttleOrDynBrakePercent > 0.1)
                            {

                                double step = 100 / throttleFullRangeIncreaseTimeSeconds;
                                step *= elapsedClockSeconds;
                                throttleOrDynBrakePercent -= step;
                            }
                            else
                            {
                                throttleOrDynBrakePercent = 0;
                            }
                        }

                        if (deltaSpeedMpS < 0) // start braking
                        {
                            if (maxForceN > 0)
                            {
                                if (throttleOrDynBrakePercent > 0)
                                {
                                    double step = 100 / throttleFullRangeDecreaseTimeSeconds;
                                    step *= elapsedClockSeconds;
                                    throttleOrDynBrakePercent -= step;
                                }
                            }
                            else
                            {
                                if (locomotive.DynamicBrakeAvailable)
                                {
                                    deltaSpeedMpS = 0;
                                    if (!restrictedSpeedActive)
                                        deltaSpeedMpS = (SelectedSpeedMpS + (TrainElevation < -0.01 ? TrainElevation * (SelectedNumberOfAxles / 12) : 0)) - absWheelSpeedMpS;
                                    else
                                        deltaSpeedMpS = (currentSelectedSpeedMpS + (TrainElevation < -0.01 ? TrainElevation * (SelectedNumberOfAxles / 12) : 0)) - absWheelSpeedMpS;

                                    relativeAcceleration = (float)-Math.Sqrt(-StartReducingSpeedDeltaDownwards * deltaSpeedMpS);
                                    AccelerationDemandMpSS = (float)-Math.Sqrt(-StartReducingSpeedDeltaDownwards * deltaSpeedMpS);
                                    if (maxForceN > 0)
                                    {
                                        if (throttleOrDynBrakePercent > 0)
                                        {
                                            double step = 100 / throttleFullRangeDecreaseTimeSeconds;
                                            step *= elapsedClockSeconds;
                                            throttleOrDynBrakePercent -= step;
                                        }
                                    }
                                    if (maxForceN == 0)
                                    {
                                        if (!useThrottle)
                                            locomotive.ThrottleController.SetPercent(0);
                                        if (relativeAcceleration < -1)
                                            relativeAcceleration = -1;
                                        if (locomotive.DynamicBrakePercent < -(AccelerationDemandMpSS * 100) && AccelerationDemandMpSS < -0.05f)
                                        {
                                            if (DynamicBrakeIsSelectedForceDependant)
                                            {
                                                if (throttleOrDynBrakePercent >
                                                    -(MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps)
                                                {
                                                    double step = 100 / DynamicBrakeFullRangeIncreaseTimeSeconds;
                                                    step *= elapsedClockSeconds;
                                                    throttleOrDynBrakePercent -= step;
                                                }
                                            }
                                            else
                                            {
                                                if (throttleOrDynBrakePercent > -100)
                                                {
                                                    double step = 100 / DynamicBrakeFullRangeIncreaseTimeSeconds;
                                                    step *= elapsedClockSeconds;
                                                    throttleOrDynBrakePercent -= step;
                                                }
                                            }
                                        }
                                        if (locomotive.DynamicBrakePercent > -((AccelerationDemandMpSS - 0.05f) * 100))
                                        {
                                            if (throttleOrDynBrakePercent < 0)
                                            {
                                                double step = 100 / DynamicBrakeFullRangeDecreaseTimeSeconds;
                                                step *= elapsedClockSeconds;
                                                throttleOrDynBrakePercent += step;
                                            }
                                        }
                                    }
                                }
                                else // use TrainBrake
                                {
                                    if (deltaSpeedMpS > -0.1)
                                    {
                                        if (!useThrottle)
                                            locomotive.ThrottleController.SetPercent(100);
                                        throttleIsZero = false;
                                        maxForceN = 0;
                                    }
                                    else if (deltaSpeedMpS > -1)
                                    {
                                        if (!useThrottle)
                                            locomotive.ThrottleController.SetPercent(0);
                                        throttleIsZero = true;

                                        brakePercent = TrainBrakeMinPercentValue - 3.0f + (-deltaSpeedMpS * 10);
                                    }
                                    else
                                    {
                                        locomotive.TractiveForceN = 0;
                                        if (!useThrottle)
                                            locomotive.ThrottleController.SetPercent(0);
                                        throttleIsZero = true;

                                        if (RelativeAccelerationMpSS > -maxDecelerationMpSS + 0.01f)
                                            brakePercent += 0.5f;
                                        else if (RelativeAccelerationMpSS < -maxDecelerationMpSS - 0.01f)
                                            brakePercent -= 1;
                                        brakePercent = MathHelper.Clamp(brakePercent, TrainBrakeMinPercentValue - 3.0f, TrainBrakeMaxPercentValue);
                                    }
                                    locomotive.SetTrainBrakePercent(brakePercent);
                                }
                                if (UseTrainBrakeAndDynBrake)
                                {
                                    if (-deltaSpeedMpS > SpeedDeltaToEnableTrainBrake)
                                    {
                                        CCIsUsingTrainBrake = true;
                                        /*                               brakePercent = Math.Max(TrainBrakeMinPercentValue + 3.0f, -delta * 2);
                                                                        if (brakePercent > TrainBrakeMaxPercentValue)
                                                                        brakePercent = TrainBrakeMaxPercentValue;*/
                                        brakePercent = (TrainBrakeMaxPercentValue - TrainBrakeMinPercentValue - 3.0f) * SelectedMaxAccelerationStep / SpeedRegulatorMaxForceSteps + TrainBrakeMinPercentValue + 3.0f;
                                        if (-deltaSpeedMpS < SpeedDeltaToEnableFullTrainBrake)
                                            brakePercent = Math.Min(brakePercent, TrainBrakeMinPercentValue + 13.0f);
                                        locomotive.SetTrainBrakePercent(brakePercent);
                                    }
                                    else if (-deltaSpeedMpS < SpeedDeltaToEnableTrainBrake)
                                    {
                                        brakePercent = 0;
                                        locomotive.SetTrainBrakePercent(brakePercent);
                                        if (Pressure.Atmospheric.FromPSI(locomotive.BrakeSystem.BrakeLine1PressurePSI) >= 4.98)
                                            CCIsUsingTrainBrake = false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (locomotive.DynamicBrakeAvailable)
                            {
                                if (locomotive.DynamicBrakePercent > 0)
                                {
                                    if (throttleOrDynBrakePercent < 0)
                                    {
                                        double step = 100 / DynamicBrakeFullRangeDecreaseTimeSeconds;
                                        step *= elapsedClockSeconds;
                                        throttleOrDynBrakePercent += step;
                                    }
                                }
                            }
                        }
                    }

                    if ((absWheelSpeedMpS > SafeSpeedForAutomaticOperationMpS || SpeedSelectorMode == SpeedSelectorMode.Start || speedRegulatorOptions.Contains("startfromzero")) && (SpeedSelectorMode != SpeedSelectorMode.Neutral && SpeedSelectorMode != SpeedSelectorMode.Parking))
                    {
                        float delta = 0;
                        if (!restrictedSpeedActive)
                            delta = SelectedSpeedMpS - absWheelSpeedMpS;
                        else
                            delta = currentSelectedSpeedMpS - absWheelSpeedMpS;
                        double coeff = 1;
                        double speed = speedIsMph ? Speed.MeterPerSecond.ToMpH(locomotive.WheelSpeedMpS) : Speed.MeterPerSecond.ToKpH(locomotive.WheelSpeedMpS);
                        coeff = speed > 100 ? (speed / 100) * 1.2f : 1;
                        float tempAccDemand = AccelerationDemandMpSS;
                        AccelerationDemandMpSS = (float)Math.Sqrt((startReducingSpeedDelta) * coeff * (delta));
                        if (float.IsNaN(AccelerationDemandMpSS))
                        {
                            AccelerationDemandMpSS = tempAccDemand;
                        }
                        if (delta > 0.0f && locomotive.DynamicBrakePercent < 1)
                        {
                            if (locomotive.DynamicBrakePercent > 0)
                            {
                                double step = 100 / DynamicBrakeFullRangeDecreaseTimeSeconds;
                                step *= elapsedClockSeconds;
                                throttleOrDynBrakePercent += step;
                            }
                            if (locomotive.DynamicBrakePercent < 1 && locomotive.DynamicBrake)
                            {
                                locomotive.SetDynamicBrakePercent(0);
                                locomotive.DynamicBrakeChangeActiveState(false);
                            }
                            relativeAcceleration = (float)Math.Sqrt(AccelerationRampMaxMpSSS * delta);
                        }
                        else // start braking
                        {
                            if (throttleOrDynBrakePercent > 0)
                            {
                                double step = 100 / throttleFullRangeDecreaseTimeSeconds;
                                step *= elapsedClockSeconds;
                                throttleOrDynBrakePercent -= step;
                                if (throttleOrDynBrakePercent < 0)
                                    throttleOrDynBrakePercent = 0;
                                if (throttleOrDynBrakePercent > 0 && throttleOrDynBrakePercent < 0.1)
                                    throttleOrDynBrakePercent = 0;
                            }

                            if (delta < 0) // start braking
                            {
                                if (maxForceN > 0)
                                {
                                    if (throttleOrDynBrakePercent > 0)
                                    {
                                        double step = 100 / throttleFullRangeDecreaseTimeSeconds;
                                        step *= elapsedClockSeconds;
                                        throttleOrDynBrakePercent -= step;
                                    }
                                }
                                else
                                {
                                    if (locomotive.DynamicBrakeAvailable)
                                    {
                                        relativeAcceleration = (float)-Math.Sqrt(-StartReducingSpeedDeltaDownwards * delta);

                                        double val = (StartReducingSpeedDeltaDownwards) * coeff * ((delta + 0.5f) / 3);
                                        if (val < 0)
                                            val = -val;
                                        AccelerationDemandMpSS = -(float)Math.Sqrt(val);
                                        if (maxForceN == 0)
                                        {
                                            if (!useThrottle)
                                                locomotive.ThrottleController.SetPercent(0);
                                            if (RelativeAccelerationMpSS > AccelerationDemandMpSS)
                                            {
                                                if (DynamicBrakeIsSelectedForceDependant)
                                                {
                                                    if (throttleOrDynBrakePercent >
                                                    -(MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps)
                                                    {
                                                        double step = 100 / DynamicBrakeFullRangeIncreaseTimeSeconds;
                                                        step *= elapsedClockSeconds;
                                                        if (step > (RelativeAccelerationMpSS - AccelerationDemandMpSS) * 2)
                                                            step = (RelativeAccelerationMpSS - AccelerationDemandMpSS) * 2;
                                                        throttleOrDynBrakePercent -= step;
                                                        if (throttleOrDynBrakePercent < -100)
                                                            throttleOrDynBrakePercent = -100;
                                                    }
                                                    if (SelectedMaxAccelerationStep == 0 && throttleOrDynBrakePercent < 0)
                                                    {
                                                        double step = 100 / DynamicBrakeFullRangeDecreaseTimeSeconds;
                                                        step *= elapsedClockSeconds;
                                                        if (step > (AccelerationDemandMpSS - RelativeAccelerationMpSS) * 2)
                                                            step = (AccelerationDemandMpSS - RelativeAccelerationMpSS) * 2;
                                                        throttleOrDynBrakePercent -= step;
                                                    }
                                                }
                                                else
                                                {
                                                    if (throttleOrDynBrakePercent > -100)
                                                    {
                                                        double step = 100 / DynamicBrakeFullRangeIncreaseTimeSeconds;
                                                        step *= elapsedClockSeconds;
                                                        if (step > (RelativeAccelerationMpSS - AccelerationDemandMpSS) * 2)
                                                            step = (RelativeAccelerationMpSS - AccelerationDemandMpSS) * 2;
                                                        throttleOrDynBrakePercent -= step;
                                                        if (throttleOrDynBrakePercent < -100)
                                                            throttleOrDynBrakePercent = -100;
                                                    }
                                                }
                                            }
                                            if (RelativeAccelerationMpSS + 0.01f < AccelerationDemandMpSS)
                                            {
                                                if (throttleOrDynBrakePercent < 0)
                                                {
                                                    double step = 100 / DynamicBrakeFullRangeDecreaseTimeSeconds;
                                                    step *= elapsedClockSeconds;
                                                    if (step > (AccelerationDemandMpSS - RelativeAccelerationMpSS) * 2)
                                                        step = (AccelerationDemandMpSS - RelativeAccelerationMpSS) * 2;
                                                    throttleOrDynBrakePercent += step;
                                                    if (DynamicBrakeIsSelectedForceDependant)
                                                    {
                                                        if (throttleOrDynBrakePercent < Math.Round(-(MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps, 0))
                                                        {
                                                            throttleOrDynBrakePercent = (float)Math.Round(-(MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps, 0);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else // use TrainBrake
                                    {
                                        if (delta > -0.1)
                                        {
                                            if (!useThrottle)
                                                locomotive.ThrottleController.SetPercent((float)throttleOrDynBrakePercent);
                                            throttleIsZero = false;
                                            maxForceN = 0;
                                        }
                                        else if (delta > -1)
                                        {
                                            if (!useThrottle)
                                                locomotive.ThrottleController.SetPercent(0);
                                            throttleIsZero = true;

                                            brakePercent = TrainBrakeMinPercentValue - 3.0f + (-delta * 10);
                                        }
                                        else
                                        {
                                            locomotive.TractiveForceN = 0;
                                            if (!useThrottle)
                                                locomotive.ThrottleController.SetPercent(0);
                                            throttleIsZero = true;

                                            if (RelativeAccelerationMpSS > -maxDecelerationMpSS + 0.01f)
                                                brakePercent += 0.5f;
                                            else if (RelativeAccelerationMpSS < -maxDecelerationMpSS - 0.01f)
                                                brakePercent -= 1;
                                            brakePercent = MathHelper.Clamp(brakePercent, TrainBrakeMinPercentValue - 3.0f, TrainBrakeMaxPercentValue);
                                        }
                                        locomotive.SetTrainBrakePercent(brakePercent);
                                    }
                                    if (UseTrainBrakeAndDynBrake)
                                    {
                                        if (-delta > SpeedDeltaToEnableTrainBrake)
                                        {
                                            CCIsUsingTrainBrake = true;
                                            /*                               brakePercent = Math.Max(TrainBrakeMinPercentValue + 3.0f, -delta * 2);
                                                                            if (brakePercent > TrainBrakeMaxPercentValue)
                                                                            brakePercent = TrainBrakeMaxPercentValue;*/
                                            brakePercent = (TrainBrakeMaxPercentValue - TrainBrakeMinPercentValue - 3.0f) * SelectedMaxAccelerationStep / SpeedRegulatorMaxForceSteps + TrainBrakeMinPercentValue + 3.0f;
                                            if (-delta < SpeedDeltaToEnableFullTrainBrake)
                                                brakePercent = Math.Min(brakePercent, TrainBrakeMinPercentValue + 13.0f);
                                            locomotive.SetTrainBrakePercent(brakePercent);
                                        }
                                        else if (-delta < SpeedDeltaToEnableTrainBrake)
                                        {
                                            brakePercent = 0;
                                            locomotive.SetTrainBrakePercent(brakePercent);
                                            if (Pressure.Atmospheric.FromPSI(locomotive.BrakeSystem.BrakeLine1PressurePSI) >= 4.98)
                                                CCIsUsingTrainBrake = false;
                                        }
                                    }
                                }
                            }
                        }
                        if (relativeAcceleration > 1.0f)
                            relativeAcceleration = 1.0f;

                        if ((SpeedSelectorMode == SpeedSelectorMode.On || SpeedSelectorMode == SpeedSelectorMode.Start) && delta > 0)
                        {
                            if (locomotive.DynamicBrakePercent > 0)
                            {
                                if (throttleOrDynBrakePercent <= 0)
                                {
                                    double step = 100 / DynamicBrakeFullRangeDecreaseTimeSeconds;
                                    step *= elapsedClockSeconds;
                                    if (step > (AccelerationDemandMpSS - RelativeAccelerationMpSS) * 2)
                                        step = (AccelerationDemandMpSS - RelativeAccelerationMpSS) * 2;
                                    throttleOrDynBrakePercent += step;
                                }
                            }
                            else
                            {
                                if (!useThrottle)
                                {
                                    if (SelectedMaxAccelerationPercent == 0 && SelectedMaxAccelerationStep == 0)
                                        locomotive.ThrottleController.SetPercent(0);
                                    else
                                        locomotive.ThrottleController.SetPercent((float)throttleOrDynBrakePercent);
                                }
                                throttleIsZero = false;
                            }
                        }
                        float a = 0;
                        if (locomotive.LocomotivePowerSupply.MainPowerSupplyOn && locomotive.Direction != MidpointDirection.N)
                        {
                            if (locomotive.DynamicBrakePercent < 0)
                            {
                                if (RelativeAccelerationMpSS < AccelerationDemandMpSS)
                                {
                                    if (forceStepsThrottleTable.Count > 0)
                                    {
                                        t = forceStepsThrottleTable[(int)SelectedMaxAccelerationStep - 1];
                                        if (accelerationTable.Count > 0)
                                            a = accelerationTable[(int)SelectedMaxAccelerationStep - 1];
                                    }
                                    else
                                        t = (MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps;
                                    if (t < newThrotte)
                                        t = newThrotte;
                                    t /= 100;
                                }
                            }
                            if (reducingForce)
                            {
                                if (t > PowerReductionValue / 100)
                                    t = PowerReductionValue / 100;
                            }
                            float demandedVolts = t * 100;
                            double current = maxForceN / locomotive.MaxForceN * locomotive.MaxCurrentA;
                            if (current < PowerBreakoutAmpers)
                                breakout = true;
                            if (breakout && delta > 0.2f)
                                breakout = false;
                            if (useThrottle) // not valid for diesel engines.
                                breakout = false;
                            if ((throttleOrDynBrakePercent != demandedVolts) && delta > 0)
                            {
                                if (a > 0 && (speedIsMph ? Speed.MeterPerSecond.ToMpH(locomotive.WheelSpeedMpS) : Speed.MeterPerSecond.ToKpH(locomotive.WheelSpeedMpS)) > 5)
                                {
                                    if (throttleOrDynBrakePercent < demandedVolts && locomotive.AccelerationMpSS < a - 0.02)
                                    {
                                        double step = 100 / throttleFullRangeIncreaseTimeSeconds;
                                        step *= elapsedClockSeconds;
                                        throttleOrDynBrakePercent += step;
                                    }
                                }
                                else
                                {
                                    if (throttleOrDynBrakePercent < demandedVolts && demandedVolts >= 0)
                                    {
                                        double step = 100 / throttleFullRangeIncreaseTimeSeconds;
                                        step *= elapsedClockSeconds;
                                        float accelDiff = AccelerationDemandMpSS - locomotive.AccelerationMpSS;
                                        if (step / 10 > accelDiff)
                                            step = accelDiff * 10;
                                        throttleOrDynBrakePercent += step;
                                    }
                                }
                                if (a > 0 && (speedIsMph ? Speed.MeterPerSecond.ToMpH(locomotive.WheelSpeedMpS) : Speed.MeterPerSecond.ToKpH(locomotive.WheelSpeedMpS)) > 5)
                                {
                                    if (throttleOrDynBrakePercent > demandedVolts && locomotive.AccelerationMpSS > a + 0.02)
                                    {
                                        double step = 100 / throttleFullRangeIncreaseTimeSeconds;
                                        step *= elapsedClockSeconds;
                                        throttleOrDynBrakePercent -= step;
                                    }
                                }
                                else
                                {
                                    if (throttleOrDynBrakePercent - 0.2f > demandedVolts)
                                    {
                                        double step = 100 / throttleFullRangeIncreaseTimeSeconds;
                                        step *= elapsedClockSeconds;
                                        throttleOrDynBrakePercent -= step;
                                    }
                                }
                                if (throttleOrDynBrakePercent > demandedVolts && delta < 0.8)
                                {
                                    double step = 100 / throttleFullRangeIncreaseTimeSeconds;
                                    step *= elapsedClockSeconds;
                                    throttleOrDynBrakePercent -= step;
                                }
                            }
                            if (a > 0 && (speedIsMph ? Speed.MeterPerSecond.ToMpH(locomotive.WheelSpeedMpS) : Speed.MeterPerSecond.ToKpH(locomotive.WheelSpeedMpS)) > 5)
                            {
                                if ((a != locomotive.AccelerationMpSS) && delta > 0.8)
                                {
                                    if (locomotive.AccelerationMpSS < a + 0.02)
                                    {
                                        double step = 100 / throttleFullRangeIncreaseTimeSeconds;
                                        step *= elapsedClockSeconds;
                                        throttleOrDynBrakePercent += step;
                                    }
                                    if (locomotive.AccelerationMpSS > a - 0.02)
                                    {
                                        double step = 100 / throttleFullRangeIncreaseTimeSeconds;
                                        step *= elapsedClockSeconds;
                                        throttleOrDynBrakePercent -= step;
                                    }
                                }
                            }

                            if (useThrottle)
                            {
                                if (throttleOrDynBrakePercent > 0)
                                    locomotive.ThrottleController.SetPercent((float)throttleOrDynBrakePercent);
                            }
                        }
                    }
                    else if (useThrottle)
                    {
                        if (locomotive.ThrottlePercent > 0)
                        {
                            float newValue = (locomotive.ThrottlePercent - 1) / 100;
                            if (newValue < 0)
                                newValue = 0;
                            locomotive.StartThrottleDecrease(newValue);
                        }
                    }

                    if (locomotive.WheelSpeedMpS == 0 && throttleOrDynBrakePercent < 0)
                        throttleOrDynBrakePercent = 0;
                    forceThrottleAndDynamicBrake = (float)throttleOrDynBrakePercent;

                    if (throttleOrDynBrakePercent > 0)
                    {
                        if (speedDiff > antiWheelSpinSpeedDiffThreshold)
                        {
                            skidSpeedDegratation += 0.05f;
                        }
                        else if (skidSpeedDegratation > 0)
                        {
                            skidSpeedDegratation -= 0.1f;
                        }
                        if (speedDiff < antiWheelSpinSpeedDiffThreshold - 0.05f)
                            skidSpeedDegratation = 0;
                        if (antiWheelSpinEquipped)
                            throttleOrDynBrakePercent -= skidSpeedDegratation;
                        if (breakout || Pressure.Atmospheric.FromPSI(locomotive.BrakeSystem.BrakeLine1PressurePSI) < 4.98)
                        {
                            maxForceN = 0;
                            throttleOrDynBrakePercent = 0;
                            Ampers = 0;
                            if (!useThrottle)
                                locomotive.ThrottleController.SetPercent(0);
                        }
                        else
                        {
                            if (locomotive.ThrottlePercent < 100 && SpeedSelectorMode != SpeedSelectorMode.Parking && !useThrottle)
                            {
                                if (SelectedMaxAccelerationPercent == 0 && SelectedMaxAccelerationStep == 0)
                                {
                                    locomotive.ThrottleController.SetPercent(0);
                                    throttleIsZero = true;
                                }
                                else
                                {
                                    locomotive.ThrottleController.SetPercent((float)throttleOrDynBrakePercent);
                                    throttleIsZero = false;
                                }
                            }
                            if (locomotive.DynamicBrakePercent > -1)
                            {
                                locomotive.SetDynamicBrakePercent(0);
                                locomotive.DynamicBrakeChangeActiveState(false);
                            }

                            if (locomotive.TractiveForceCurves != null && !useThrottle)
                            {
                                maxForceN = locomotive.TractiveForceCurves.Get(throttleOrDynBrakePercent / 100, absWheelSpeedMpS) * (1 - locomotive.PowerReduction);
                            }
                            else
                            {
                                if (locomotive.TractiveForceCurves == null)
                                {
                                    maxForceN = locomotive.MaxForceN * (throttleOrDynBrakePercent / 100);
                                    //                               if (maxForceN * AbsWheelSpeedMpS > Locomotive.MaxPowerW * (controllerVolts / 100))
                                    //                                   maxForceN = Locomotive.MaxPowerW / AbsWheelSpeedMpS * (controllerVolts / 100);
                                    if (locomotive.MaxForceN * absWheelSpeedMpS > locomotive.MaxPowerW * (throttleOrDynBrakePercent / 100))
                                        maxForceN = locomotive.MaxPowerW / absWheelSpeedMpS * (throttleOrDynBrakePercent / 100) * (throttleOrDynBrakePercent / 100);
                                    maxForceN *= 1 - locomotive.PowerReduction;
                                }
                                else
                                    maxForceN = locomotive.TractiveForceCurves.Get(throttleOrDynBrakePercent / 100, absWheelSpeedMpS) * (1 - locomotive.PowerReduction);
                            }
                        }
                    }
                    else if (throttleOrDynBrakePercent < 0)
                    {
                        if (maxForceN > 0)
                            maxForceN = 0;
                        if (locomotive.ThrottlePercent > 0)
                            locomotive.ThrottleController.SetPercent(0);
                        if (locomotive.DynamicBrakeAvailable)
                        {
                            if (locomotive.DynamicBrakePercent <= 0)
                            {
                                string status = locomotive.GetDynamicBrakeStatus();
                                locomotive.DynamicBrakeChangeActiveState(true);
                            }
                            if (SelectedMaxAccelerationPercent == 0 && SelectedMaxAccelerationStep == 0)
                            {
                                locomotive.SetDynamicBrakePercent(0);
                                locomotive.DynamicBrakePercent = 0;
                                throttleOrDynBrakePercent = 0;
                            }
                            else
                            {
                                locomotive.SetDynamicBrakePercent(-(float)throttleOrDynBrakePercent);
                                locomotive.DynamicBrakePercent = -(float)throttleOrDynBrakePercent;
                            }
                        }
                        else if (SelectedMaxAccelerationPercent == 0 && SelectedMaxAccelerationStep == 0)
                            throttleOrDynBrakePercent = 0;
                    }
                    else if (throttleOrDynBrakePercent == 0)
                    {
                        if (!breakout)
                        {

                            /*if (Locomotive.MultiPositionController.controllerPosition == Controllers.MultiPositionController.ControllerPosition.DynamicBrakeIncrease || Locomotive.MultiPositionController.controllerPosition == Controllers.MultiPositionController.ControllerPosition.DynamicBrakeIncreaseFast)
                            {
                                controllerVolts = -Locomotive.DynamicBrakePercent;
                            }
                            else
                            {*/
                            if (maxForceN > 0)
                                maxForceN = 0;
                            if (locomotive.ThrottlePercent > 0 && !useThrottle)
                                locomotive.ThrottleController.SetPercent(0);
                            if (locomotive.DynamicBrakeAvailable && locomotive.DynamicBrakePercent > -1)
                            {
                                locomotive.SetDynamicBrakePercent(0);
                                locomotive.DynamicBrakeChangeActiveState(false);
                            }
                        }
                    }

                    if (!locomotive.LocomotivePowerSupply.MainPowerSupplyOn) 
                    {
                        throttleOrDynBrakePercent = 0;
                        locomotive.ThrottleController.SetPercent(0);
                        if (locomotive.DynamicBrakeAvailable && locomotive.DynamicBrakePercent > 0)
                            locomotive.SetDynamicBrakePercent(0);
                        locomotive.DynamicBrakeIntervention = -1;
                        maxForceN = 0;
                        forceThrottleAndDynamicBrake = 0;
                        Ampers = 0;
                    }
                    else
                        forceThrottleAndDynamicBrake = (float)throttleOrDynBrakePercent;

                    locomotive.MotiveForceN = (float)maxForceN;
                    locomotive.TractiveForceN = (float)maxForceN;
                }
            }

            if (playerNotDriveableTrainLocomotives.Count > 0) // update any other than the player's locomotive in the consist throttles to percentage of the current force and the max force
            {
                float locoPercent = locomotive.MaxForceN - (locomotive.MaxForceN - locomotive.MotiveForceN);
                locoPercent = (locoPercent / locomotive.MaxForceN) * 100;
                //Simulator.Confirmer.MSG(locoPercent.ToString());
                foreach (MSTSLocomotive lc in playerNotDriveableTrainLocomotives)
                {
                    if (locomotive.LocomotivePowerSupply.MainPowerSupplyOn)
                    {
                        if (useThrottle)
                        {
                            lc.SetThrottlePercent(locomotive.ThrottlePercent);
                        }
                        else
                        {
                            IsAPartOfPlayerTrain = true;
                            ThrottleOverriden = locoPercent / 100;
                        }
                    }
                    else
                    {
                        if (useThrottle)
                        {
                            lc.SetThrottlePercent(0);
                        }
                        else
                        {
                            IsAPartOfPlayerTrain = true;
                            ThrottleOverriden = 0;
                        }
                    }
                }
            }
        }

        public float GetDataOf(CabViewControl cvc)
        {
            float data = 0;
            switch (cvc.ControlType)
            {
                case CabViewControlType.Orts_Selected_Speed:
                case CabViewControlType.Orts_Selected_Speed_Display:
                    bool metric = cvc.ControlUnit == CabViewControlUnit.Km_Per_Hour;
                    float temp = (float)Math.Round(restrictedSpeedActive ? Speed.MeterPerSecond.FromMpS(currentSelectedSpeedMpS, metric) : Speed.MeterPerSecond.FromMpS(SelectedSpeedMpS, metric));
                    if (previousSelectedSpeed < temp)
                        previousSelectedSpeed += 1f;
                    if (previousSelectedSpeed > temp)
                        previousSelectedSpeed -= 1f;
                    data = previousSelectedSpeed;
                    break;
                case CabViewControlType.Orts_Selected_Speed_Mode:
                    data = (float)SpeedSelectorMode;
                    break;
                case CabViewControlType.Orts_Selected_Speed_Regulator_Mode:
                    data = (float)SpeedRegulatorMode;
                    break;
                case CabViewControlType.Orts_Selected_Speed_Selector:
                    metric = cvc.ControlUnit == CabViewControlUnit.Km_Per_Hour;
                    data = (float)Math.Round(restrictedSpeedActive ? Speed.MeterPerSecond.FromMpS(currentSelectedSpeedMpS, metric) : Speed.MeterPerSecond.FromMpS(SelectedSpeedMpS, metric));
                    break;
                case CabViewControlType.Orts_Selected_Speed_Maximum_Acceleration:
                    if (SpeedRegulatorMode == SpeedRegulatorMode.Auto || MaxForceKeepSelectedStepWhenManualModeSet)
                    {
                        data = SelectedMaxAccelerationStep * (float)cvc.ScaleRangeMax / SpeedRegulatorMaxForceSteps;
                    }
                    else
                        data = 0;
                    break;
                case CabViewControlType.Orts_Restricted_Speed_Zone_Active:
                    data = restrictedSpeedActive ? 1 : 0;
                    break;
                case CabViewControlType.Orts_Number_Of_Axes_Display_Units:
                    data = SelectedNumberOfAxles % 10;
                    break;
                case CabViewControlType.Orts_Number_Of_Axes_Display_Tens:
                    data = (SelectedNumberOfAxles / 10) % 10;
                    break;
                case CabViewControlType.Orts_Number_Of_Axes_Display_Hundreds:
                    data = (SelectedNumberOfAxles / 100) % 10;
                    break;
                case CabViewControlType.Orts_Train_Length_Metres:
                    data = trainLengthMeters;
                    break;
                case CabViewControlType.Orts_Remaining_Train_Length_Speed_Restricted:
                    if (remainingTrainLengthToPassRestrictedZone == 0)
                        data = 0;
                    else
                        data = trainLengthMeters - remainingTrainLengthToPassRestrictedZone;
                    break;
                case CabViewControlType.Orts_Remaining_Train_Length_Percent:
                    if (SpeedRegulatorMode != SpeedRegulatorMode.Auto)
                    {
                        data = 0;
                        break;
                    }
                    if (trainLengthMeters > 0 && remainingTrainLengthToPassRestrictedZone > 0)
                    {
                        data = (trainLengthMeters - (float)remainingTrainLengthToPassRestrictedZone) / trainLengthMeters * 100;
                    }
                    break;
                case CabViewControlType.Orts_Motive_Force:
                    data = locomotive.FilteredMotiveForceN;
                    break;
                case CabViewControlType.Orts_Motive_Force_KiloNewton:
                    if (locomotive.FilteredMotiveForceN > locomotive.DynamicBrakeForceN)
                        data = (float)Math.Round(locomotive.FilteredMotiveForceN / 1000, 0);
                    else if (locomotive.DynamicBrakeForceN > 0)
                        data = -(float)Math.Round(locomotive.DynamicBrakeForceN / 1000, 0);
                    break;
                case CabViewControlType.Orts_Maximum_Force:
                    data = locomotive.MaxForceN;
                    break;
                case CabViewControlType.Orts_Force_In_Percent_Throttle_And_Dynamic_Brake:
                    if (SpeedRegulatorMode == SpeedRegulatorMode.Auto)
                    {
                        data = forceThrottleAndDynamicBrake;
                        if (locomotive.DynamicBrakePercent > 0 && data > -locomotive.DynamicBrakePercent)
                            data = -locomotive.DynamicBrakePercent;
                    }
                    else
                    {
                        if (locomotive.ThrottlePercent > 0)
                        {
                            data = locomotive.ThrottlePercent;
                        }
                        else if (locomotive.DynamicBrakePercent > 0 && locomotive.AbsSpeedMpS > 0)
                        {
                            data = -locomotive.DynamicBrakePercent;
                        }
                        else
                            data = 0;
                    }
                    break;
                case CabViewControlType.Orts_Train_Type_Pax_Or_Cargo:
                    data = (int)locomotive.SelectedTrainType;
                    break;
                case CabViewControlType.Orts_Controller_Voltage:
                    data = (float)throttleOrDynBrakePercent;
                    break;
                case CabViewControlType.Orts_Ampers_By_Controller_Voltage:
                    if (SpeedRegulatorMode == SpeedRegulatorMode.Auto)
                    {
                        if (throttleOrDynBrakePercent < 0)
                            data = -(float)throttleOrDynBrakePercent / 100 * (locomotive.MaxCurrentA * 0.8f);
                        else
                            data = (float)throttleOrDynBrakePercent / 100 * (locomotive.MaxCurrentA * 0.8f);
                        if (data == 0 && locomotive.DynamicBrakePercent > 0 && locomotive.AbsSpeedMpS > 0)
                            data = locomotive.DynamicBrakePercent / 100 * (locomotive.MaxCurrentA * 0.8f);
                    }
                    else
                    {
                        if (locomotive.DynamicBrakePercent > 0 && locomotive.AbsSpeedMpS > 0)
                            data = locomotive.DynamicBrakePercent / 200 * (locomotive.MaxCurrentA * 0.8f);
                        else
                            data = locomotive.ThrottlePercent / 100 * (locomotive.MaxCurrentA * 0.8f);
                    }
                    break;
                case CabViewControlType.Orts_Acceleration_In_Time:
                    {
                        data = AccelerationBits;
                        break;
                    }
                case CabViewControlType.Orts_CC_Select_Speed:
                    data = SelectingSpeedPressed ? 1 : 0;
                    break;
                case CabViewControlType.Orts_CC_Speed_0:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed0] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_10:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed10] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_20:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed20] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_30:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed30] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_40:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed40] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_50:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed50] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_60:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed60] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_70:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed70] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_80:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed80] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_90:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed90] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_100:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed100] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_110:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed110] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_120:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed120] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_130:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed130] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_140:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed140] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_150:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed150] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_160:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed160] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_170:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed170] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_180:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed180] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_190:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed190] ? 1 : 0;
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_200:
                    {
                        data = SpeedPressed[CruiseControlSpeed.Speed200] ? 1 : 0;
                        break;
                    }
                default:
                    data = 0;
                    break;
            }
            return data;
        }

        public AvvSignal AvvSignal { get; private set; } = AvvSignal.Stop;

        public void DrawAvvSignal(AvvSignal targetState)
        {
            AvvSignal = targetState;
        }
    }
}
