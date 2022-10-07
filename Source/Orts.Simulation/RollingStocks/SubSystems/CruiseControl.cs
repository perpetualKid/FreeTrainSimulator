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
using Orts.Scripting.Api;

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

        private bool cruiseControlActive;

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
        public float SetSpeedMpS => restrictedRegionOdometer.Started ? currentSelectedSpeedMpS : SelectedSpeedMpS;
        public int SelectedNumberOfAxles { get; set; }
        public float SpeedRegulatorNominalSpeedStepMpS { get; set; }
        private float speedRegulatorNominalSpeedStepKpHOrMpH;
        private float maxAccelerationMpSS = 1;
        private float maxDecelerationMpSS = 0.5f;
        public bool UseThrottleInCombinedControl { get; set; }
        private bool antiWheelSpinEquipped;
        private float antiWheelSpinSpeedDiffThreshold;
        private float dynamicBrakeMaxForceAtSelectorStep;
        private float trainBrakePercent;
        private float trainLength;
        private int trainLengthMeters;
        private float currentSelectedSpeedMpS;
        private Odometer restrictedRegionOdometer;
        private float restrictedRegionTravelledDistance;

        private float startReducingSpeedDelta = 0.5f;
        private float StartReducingSpeedDeltaDownwards;
        private bool throttleNeutralPriority;
        private readonly List<int> forceStepsThrottleTable = new List<int>();

        private float throttleFullRangeIncreaseTimeSeconds = 6;
        private float throttleFullRangeDecreaseTimeSeconds = 6;
        private readonly List<float> accelerationTable = new List<float>();

        public bool DynamicBrakePriority { get; set; }
        private float brakePercent;
        public float DynamicBrakeIncreaseSpeed { get; set; }
        public float DynamicBrakeDecreaseSpeed { get; set; }
        public uint MinimumMetersToPass { get; set; } = 19;
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
        private const float stepSize = 20;
        private float RelativeAccelerationMpSS => locomotive.Direction == MidpointDirection.Reverse ? -locomotive.AccelerationMpSS : locomotive.AccelerationMpSS; // Acceleration relative to state of reverser
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

        private float skidSpeedDegratation;
        public bool TrainBrakePriority { get; set; }
        private bool wasBraking;
        private bool wasForceReset = true;

        private float previousSelectedSpeed;

        public bool SelectingSpeedPressed { get; set; }
        public bool EngineBrakePriority { get; set; }
        public int AccelerationBits { get; }
        public EnumArray<bool, CruiseControlSpeed> SpeedPressed { get; } = new EnumArray<bool, CruiseControlSpeed>();

        public CruiseControl(MSTSLocomotive locomotive)
        {
            this.locomotive = locomotive;
            simulator = Simulator.Instance;
            restrictedRegionOdometer = new Odometer(locomotive);
        }

        public CruiseControl(CruiseControl source, MSTSLocomotive locomotive)
        {
            ArgumentNullException.ThrowIfNull(source);

            simulator = Simulator.Instance;
            this.locomotive = locomotive;
            restrictedRegionOdometer = new Odometer(locomotive);

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

        public void Parse(STFReader stf)
        {
            stf.MustMatch("(");
            while (!stf.EndOfBlock())
            {
                switch (stf.ReadItem().ToLower())
                {
                    case "speedismph":
                        speedIsMph = stf.ReadBoolBlock(false);
                        break;
                    case "usethrottleincombinedcontrol":
                        UseThrottleInCombinedControl = stf.ReadBoolBlock(false);
                        break;
                    case "speedselectorsteptimeseconds":
                        SpeedSelectorStepTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 0.1f);
                        break;
                    case "throttlefullrangeincreasetimeseconds":
                        throttleFullRangeIncreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                        break;
                    case "throttlefullrangedecreasetimeseconds":
                        throttleFullRangeDecreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                        break;
                    case "resetforceafteranybraking":
                        ResetForceAfterAnyBraking = stf.ReadBoolBlock(false);
                        break;
                    case "dynamicbrakefullrangeincreasetimeseconds":
                        DynamicBrakeFullRangeIncreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                        break;
                    case "dynamicbrakefullrangedecreasetimeseconds":
                        DynamicBrakeFullRangeDecreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                        break;
                    case "parkingbrakeengagespeed":
                        ParkingBrakeEngageSpeed = stf.ReadFloatBlock(STFReader.Units.Speed, 0);
                        break;
                    case "parkingbrakepercent":
                        ParkingBrakePercent = stf.ReadFloatBlock(STFReader.Units.Any, 0);
                        break;
                    case "maxpowerthreshold":
                        MaxPowerThreshold = stf.ReadFloatBlock(STFReader.Units.Any, 0);
                        break;
                    case "safespeedforautomaticoperationmps":
                        SafeSpeedForAutomaticOperationMpS = stf.ReadFloatBlock(STFReader.Units.Any, 0);
                        break;
                    case "maxforcepercentunits":
                        SpeedRegulatorMaxForcePercentUnits = stf.ReadBoolBlock(false);
                        break;
                    case "maxforcesteps":
                        SpeedRegulatorMaxForceSteps = stf.ReadIntBlock(0);
                        break;
                    case "maxforcesetsinglestep":
                        MaxForceSetSingleStep = stf.ReadBoolBlock(false);
                        break;
                    case "maxforcekeepselectedstepwhenmanualmodeset":
                        MaxForceKeepSelectedStepWhenManualModeSet = stf.ReadBoolBlock(false);
                        break;
                    case "keepselectedspeedwhenmanualmodeset":
                        KeepSelectedSpeedWhenManualModeSet = stf.ReadBoolBlock(false);
                        break;
                    case "forceregulatorautowhennonzerospeedselected":
                        ForceRegulatorAutoWhenNonZeroSpeedSelected = stf.ReadBoolBlock(false);
                        break;
                    case "forceregulatorautowhennonzeroforceselected":
                        ForceRegulatorAutoWhenNonZeroForceSelected = stf.ReadBoolBlock(false);
                        break;
                    case "forceregulatorautowhennonzerospeedselectedandthrottleatzero":
                        ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero = stf.ReadBoolBlock(false);
                        break;
                    case "maxforceselectorisdiscrete":
                        MaxForceSelectorIsDiscrete = stf.ReadBoolBlock(false);
                        break;
                    case "continuousspeedincreasing":
                        ContinuousSpeedIncreasing = stf.ReadBoolBlock(false);
                        break;
                    case "disablecruisecontrolonthrottleandzerospeed":
                        DisableCruiseControlOnThrottleAndZeroSpeed = stf.ReadBoolBlock(false);
                        break;
                    case "disablecruisecontrolonthrottleandzeroforce":
                        DisableCruiseControlOnThrottleAndZeroForce = stf.ReadBoolBlock(false);
                        break;
                    case "disablecruisecontrolonthrottleandzeroforceandzerospeed":
                        DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed = stf.ReadBoolBlock(false);
                        break;
                    case "disablemanualswitchtomanualwhensetforcenotatzero":
                        DisableManualSwitchToManualWhenSetForceNotAtZero = stf.ReadBoolBlock(false);
                        break;
                    case "disablemanualswitchtoautowhenthrottlenotatzero":
                        DisableManualSwitchToAutoWhenThrottleNotAtZero = stf.ReadBoolBlock(false);
                        break;
                    case "disablemanualswitchtoautowhensetspeednotattop":
                        DisableManualSwitchToAutoWhenSetSpeedNotAtTop = stf.ReadBoolBlock(false);
                        break;
                    case "enableselectedspeedselectionwhenmanualmodeset":
                        EnableSelectedSpeedSelectionWhenManualModeSet = stf.ReadBoolBlock(false);
                        break;
                    case "forcestepsthrottletable":
                        foreach (string forceStepThrottleString in stf.ReadStringBlock("").Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (int.TryParse(forceStepThrottleString, out int forceStepThrottleValue))
                                forceStepsThrottleTable.Add(forceStepThrottleValue);
                        }
                        break;
                    case "accelerationtable":
                        foreach (string accelerationString in stf.ReadStringBlock("").Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (float.TryParse(accelerationString, out float accelerationValue))
                                accelerationTable.Add(accelerationValue);
                        }
                        break;
                    case "powerbreakoutampers":
                        PowerBreakoutAmpers = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                        break;
                    case "powerbreakoutspeeddelta":
                        PowerBreakoutSpeedDelta = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                        break;
                    case "powerresumespeeddelta":
                        PowerResumeSpeedDelta = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                        break;
                    case "powerreductiondelaypaxtrain":
                        PowerReductionDelayPaxTrain = stf.ReadFloatBlock(STFReader.Units.Any, 0.0f);
                        break;
                    case "powerreductiondelaycargotrain":
                        PowerReductionDelayCargoTrain = stf.ReadFloatBlock(STFReader.Units.Any, 0.0f);
                        break;
                    case "powerreductionvalue":
                        PowerReductionValue = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                        break;
                    case "disablezeroforcestep":
                        DisableZeroForceStep = stf.ReadBoolBlock(false);
                        break;
                    case "dynamicbrakeisselectedforcedependant":
                        DynamicBrakeIsSelectedForceDependant = stf.ReadBoolBlock(false);
                        break;
                    case "defaultforcestep":
                        SelectedMaxAccelerationStep = stf.ReadFloatBlock(STFReader.Units.Any, 1.0f);
                        break;
                    case "dynamicbrakemaxforceatselectorstep":
                        dynamicBrakeMaxForceAtSelectorStep = stf.ReadFloatBlock(STFReader.Units.Any, 1.0f);
                        break;
                    case "startreducingspeeddelta":
                        startReducingSpeedDelta = (stf.ReadFloatBlock(STFReader.Units.Any, 1.0f) / 10);
                        break;
                    case "startreducingspeeddeltadownwards":
                        StartReducingSpeedDeltaDownwards = (stf.ReadFloatBlock(STFReader.Units.Any, 1.0f) / 10);
                        break;
                    case "maxacceleration":
                        maxAccelerationMpSS = stf.ReadFloatBlock(STFReader.Units.Any, 1);
                        break;
                    case "maxdeceleration":
                        maxDecelerationMpSS = stf.ReadFloatBlock(STFReader.Units.Any, 0.5f);
                        break;
                    case "antiwheelspinequipped":
                        antiWheelSpinEquipped = stf.ReadBoolBlock(false);
                        break;
                    case "antiwheelspinspeeddiffthreshold":
                        antiWheelSpinSpeedDiffThreshold = stf.ReadFloatBlock(STFReader.Units.None, 0.5f);
                        break;
                    case "nominalspeedstep":
                        {
                            speedRegulatorNominalSpeedStepKpHOrMpH = stf.ReadFloatBlock(STFReader.Units.Speed, 0);
                            SpeedRegulatorNominalSpeedStepMpS = (float)(speedIsMph ? Speed.MeterPerSecond.FromMpH(speedRegulatorNominalSpeedStepKpHOrMpH) : Speed.MeterPerSecond.FromKpH(speedRegulatorNominalSpeedStepKpHOrMpH));
                            break;
                        }
                    case "usethrottleasspeedselector":
                        UseThrottleAsSpeedSelector = stf.ReadBoolBlock(false);
                        break;
                    case "usethrottleasforceselector":
                        UseThrottleAsForceSelector = stf.ReadBoolBlock(false);
                        break;
                    case "dynamicbrakeincreasespeed":
                        DynamicBrakeIncreaseSpeed = stf.ReadFloatBlock(STFReader.Units.Any, 0.5f);
                        break;
                    case "dynamicbrakedecreasespeed":
                        DynamicBrakeDecreaseSpeed = stf.ReadFloatBlock(STFReader.Units.Any, 0.5f);
                        break;
                    case "forceresetrequiredafterbraking":
                        ForceResetRequiredAfterBraking = stf.ReadBoolBlock(false);
                        break;
                    case "forceresetincludedynamicbrake":
                        ForceResetIncludeDynamicBrake = stf.ReadBoolBlock(false);
                        break;
                    case "zeroselectedspeedwhenpassingtothrottlemode":
                        ZeroSelectedSpeedWhenPassingToThrottleMode = stf.ReadBoolBlock(false);
                        break;
                    case "dynamicbrakecommandhaspriorityovercruisecontrol":
                        DynamicBrakeCommandHasPriorityOverCruiseControl = stf.ReadBoolBlock(true);
                        break;
                    case "hasindependentthrottledynamicbrakelever":
                        HasIndependentThrottleDynamicBrakeLever = stf.ReadBoolBlock(false);
                        break;
                    case "hasproportionalspeedselector":
                        HasProportionalSpeedSelector = stf.ReadBoolBlock(false);
                        break;
                    case "speedselectorisdiscrete":
                        SpeedSelectorIsDiscrete = stf.ReadBoolBlock(false);
                        break;
                    case "usetrainbrakeanddynbrake":
                        UseTrainBrakeAndDynBrake = stf.ReadBoolBlock(false);
                        break;
                    case "speeddeltatoenabletrainbrake":
                        SpeedDeltaToEnableTrainBrake = stf.ReadFloatBlock(STFReader.Units.Speed, 5f);
                        break;
                    case "speeddeltatoenablefulltrainbrake":
                        SpeedDeltaToEnableFullTrainBrake = stf.ReadFloatBlock(STFReader.Units.Speed, 10f);
                        break;
                    case "minimumspeedforcceffect":
                        MinimumSpeedForCCEffectMpS = stf.ReadFloatBlock(STFReader.Units.Speed, 0f);
                        break;
                    case "trainbrakeminpercentvalue":
                        TrainBrakeMinPercentValue = stf.ReadFloatBlock(STFReader.Units.Any, 0.3f);
                        break;
                    case "trainbrakemaxpercentvalue":
                        TrainBrakeMaxPercentValue = stf.ReadFloatBlock(STFReader.Units.Any, 0.85f);
                        break;
                    case "startinautomode":
                        StartInAutoMode = stf.ReadBoolBlock(false);
                        break;
                    case "throttleneutralposition":
                        ThrottleNeutralPosition = stf.ReadBoolBlock(false);
                        break;
                    case "throttlelowspeedposition":
                        ThrottleLowSpeedPosition = stf.ReadBoolBlock(false);
                        break;
                    case "lowspeed":
                        LowSpeed = stf.ReadFloatBlock(STFReader.Units.Speed, 2f);
                        break;
                    case "hasttwoforcevalues":
                        HasTwoForceValues = stf.ReadBoolBlock(false);
                        break;
                    case "docomputenumberofaxles":
                        DoComputeNumberOfAxles = stf.ReadBoolBlock(false);
                        break;
                    case "options":
                        foreach (string speedRegulatorString in stf.ReadStringBlock("").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            speedRegulatorOptions.Add(speedRegulatorString.ToLower());
                        }
                        break;
                    case "controllercruisecontrollogic":
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
                    case "(":
                        stf.SkipRestOfBlock();
                        break;
                    default:
                        break;
                }
            }
        }

        public void Initialize()
        {
            if (DynamicBrakeFullRangeIncreaseTimeSeconds == 0)
                DynamicBrakeFullRangeIncreaseTimeSeconds = 4;
            if (DynamicBrakeFullRangeDecreaseTimeSeconds == 0)
                DynamicBrakeFullRangeDecreaseTimeSeconds = 6;
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
            if (!locomotive.IsPlayerTrain || locomotive != locomotive.Train.LeadLocomotive)
            {
                wasForceReset = false;
                throttleOrDynBrakePercent = 0;
                return;
            }

            UpdateSelectedSpeed(elapsedClockSeconds);
            if (restrictedRegionOdometer.Triggered)
            {
                restrictedRegionOdometer.Stop();
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Speed restricted zone off."));
                locomotive.SignalEvent(TrainEvent.CruiseControlAlert);
            }
            bool active = cruiseControlActive;
            cruiseControlActive = false;
            if (SpeedRegulatorMode != SpeedRegulatorMode.Auto || locomotive.DynamicBrakePercent < 0)
                DynamicBrakePriority = false;

            if (locomotive.TrainBrakeController.TCSEmergencyBraking || locomotive.TrainBrakeController.TCSFullServiceBraking)
            {
                wasBraking = true;
                throttleOrDynBrakePercent = 0;
            }
            else if (SpeedRegulatorMode == SpeedRegulatorMode.Manual || (SpeedRegulatorMode == SpeedRegulatorMode.Auto && (DynamicBrakePriority || throttleNeutralPriority)))
            {
                wasForceReset = false;
                throttleOrDynBrakePercent = 0;
            }
            else if (SpeedRegulatorMode == SpeedRegulatorMode.Auto)
            {
                CalculateRequiredForce(elapsedClockSeconds, locomotive.AbsWheelSpeedMpS);
                throttleOrDynBrakePercent = Math.Clamp(throttleOrDynBrakePercent, -100, 100);
                if (throttleOrDynBrakePercent >= 0)
                {
                    locomotive.ThrottlePercent = (float)throttleOrDynBrakePercent;
                    locomotive.DynamicBrakePercent = -1;
                }
                else
                {
                    if (locomotive.DynamicBrakePercent < 0)
                        locomotive.DynamicBrakeCommandStartTime = Simulator.Instance.ClockTime;
                    locomotive.ThrottlePercent = 0;
                    locomotive.DynamicBrakePercent = (float)-throttleOrDynBrakePercent;
                }
                cruiseControlActive = true;
            }
            if (!cruiseControlActive && active && SpeedRegulatorMode == SpeedRegulatorMode.Auto)
            {
                locomotive.ThrottlePercent = 0;
                if (!DynamicBrakePriority)
                    locomotive.DynamicBrakePercent = -1;
            }
            if (SpeedRegulatorMode == SpeedRegulatorMode.Manual)
                SkipThrottleDisplay = false;

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
            outf.Write(currentSelectedSpeedMpS);
            outf.Write(maxForceDecreasing);
            outf.Write(maxForceIncreasing);
            outf.Write(restrictedRegionOdometer.Started);
            outf.Write(restrictedRegionOdometer.RemainingValue);
            outf.Write(SelectedMaxAccelerationPercent);
            outf.Write(SelectedMaxAccelerationStep);
            outf.Write(SelectedNumberOfAxles);
            outf.Write(SelectedSpeedMpS);
            outf.Write(DynamicBrakePriority);
            outf.Write((int)SpeedRegulatorMode);
            outf.Write((int)SpeedSelectorMode);
            outf.Write(trainBrakePercent);
            outf.Write(trainLengthMeters);
            outf.Write(speedRegulatorIntermediateValue);
            outf.Write(CCIsUsingTrainBrake);
        }

        public void Restore(BinaryReader inf)
        {
            currentSelectedSpeedMpS = inf.ReadSingle();
            maxForceDecreasing = inf.ReadBoolean();
            maxForceIncreasing = inf.ReadBoolean();
            bool started = inf.ReadBoolean();
            restrictedRegionOdometer.Setup(inf.ReadSingle());
            if (started)
                restrictedRegionOdometer.Start();
            SelectedMaxAccelerationPercent = inf.ReadSingle();
            SelectedMaxAccelerationStep = inf.ReadSingle();
            SelectedNumberOfAxles = inf.ReadInt32();
            SelectedSpeedMpS = inf.ReadSingle();
            DynamicBrakePriority = inf.ReadBoolean();
            SpeedRegulatorMode = (SpeedRegulatorMode)inf.ReadInt32();
            SpeedSelectorMode = (SpeedSelectorMode)inf.ReadInt32();
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
                            if (speedRegulatorOptions.Contains("regulatormanual"))
                                test = true;
                            if (ZeroSelectedSpeedWhenPassingToThrottleMode || UseThrottleAsSpeedSelector)
                                SelectedSpeedMpS = 0;
                            if (UseThrottleAsForceSelector)
                                SelectedMaxAccelerationStep = 0;
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
                wasForceReset = true;
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

            if (SpeedRegulatorMaxForcePercentUnits)
            {
                if (SelectedMaxAccelerationPercent == 100)
                    return;
                speedRegulatorIntermediateValue += stepSize * elapsedClockSeconds;
                SelectedMaxAccelerationPercent = (float)Math.Truncate(speedRegulatorIntermediateValue + 1);
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Speed regulator max acceleration percent changed to {SelectedMaxAccelerationPercent:F0}%"));
            }
            else
            {
                if (SelectedMaxAccelerationStep == SpeedRegulatorMaxForceSteps)
                    return;
                speedRegulatorIntermediateValue += MaxForceSelectorIsDiscrete ? elapsedClockSeconds : stepSize * elapsedClockSeconds * SpeedRegulatorMaxForceSteps / 100.0f;
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
            speedRegulatorIntermediateValue -= MaxForceSelectorIsDiscrete ? elapsedClockSeconds : stepSize * elapsedClockSeconds * SpeedRegulatorMaxForceSteps / 100.0f;
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
                wasForceReset = true;
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
            if (selectedSpeedLeverHoldTime + SpeedSelectorStepTimeSeconds > totalTime)
                return;
            selectedSpeedLeverHoldTime = totalTime;

            SelectedSpeedMpS = Math.Max(MinimumSpeedForCCEffectMpS, SelectedSpeedMpS + SpeedRegulatorNominalSpeedStepMpS);
            if (SelectedSpeedMpS > locomotive.MaxSpeedMpS)
                SelectedSpeedMpS = locomotive.MaxSpeedMpS;

            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Selected speed changed to {FormatStrings.FormatSpeedLimit(SelectedSpeedMpS, !speedIsMph)}"));
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
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Selected speed changed to {FormatStrings.FormatSpeedLimit(SelectedSpeedMpS, !speedIsMph)}"));
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
                simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Selected speed changed to {FormatStrings.FormatSpeedLimit(SelectedSpeedMpS, !speedIsMph)}"));
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
            restrictedRegionOdometer.Setup(trainLengthMeters);
            if (!restrictedRegionOdometer.Started)
            {
                restrictedRegionOdometer.Start();
                currentSelectedSpeedMpS = SelectedSpeedMpS;
            }
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Speed restricted zone active."));
        }

        public void SetSpeed(float speed)
        {
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

        public virtual void CalculateRequiredForce(double elapsedClockSeconds, float absWheelSpeedMpS)
        {
            float speedDiff = locomotive.Train.Cars.Where(x => x is MSTSLocomotive).Select(x => (x as MSTSLocomotive).AbsWheelSpeedMpS - x.AbsSpeedMpS).Max();

            float trainElevation = locomotive.Train.Cars.Select(tc => tc.Flipped ? tc.CurrentElevationPercent : -tc.CurrentElevationPercent).Sum() / locomotive.Train.Cars.Count;

            if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Release ||
                locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Neutral)
            {
                TrainBrakePriority = false;
            }
            if (TrainBrakePriority)
            {
                wasForceReset = false;
                wasBraking = true;
                if (SpeedSelectorMode == SpeedSelectorMode.Parking)
                    if (absWheelSpeedMpS < (speedIsMph ? Speed.MeterPerSecond.FromMpH(ParkingBrakeEngageSpeed) : Speed.MeterPerSecond.FromKpH(ParkingBrakeEngageSpeed)))
                        locomotive.SetEngineBrakePercent(ParkingBrakePercent);
                throttleOrDynBrakePercent = 0;
                return;
            }
            bool canAddForce = false;

            if ((SpeedSelectorMode == SpeedSelectorMode.On || SpeedSelectorMode == SpeedSelectorMode.Start) && !TrainBrakePriority)
            {
                canAddForce = true;
            }
            else
            {
                canAddForce = false;
                timeFromEngineMoved = 0;
                reducingForce = true;
            }

            if (SpeedSelectorMode == SpeedSelectorMode.Start)
                wasForceReset = true;

            if (SelectedMaxAccelerationPercent == 0 && SelectedMaxAccelerationStep == 0)
            {
                wasBraking = false;
                wasForceReset = true;
            }
            if (ForceResetRequiredAfterBraking && (!wasForceReset || (wasBraking && (SelectedMaxAccelerationStep > 0 || SelectedMaxAccelerationPercent > 0))))
            {
                throttleOrDynBrakePercent = 0;
                if (SpeedSelectorMode == SpeedSelectorMode.Parking)
                    if (absWheelSpeedMpS < (speedIsMph ? Speed.MeterPerSecond.FromMpH(ParkingBrakeEngageSpeed) : Speed.MeterPerSecond.FromKpH(ParkingBrakeEngageSpeed)))
                        locomotive.SetEngineBrakePercent(ParkingBrakePercent);
                return;
            }

            if (ThrottleNeutralPosition && SelectedSpeedMpS == 0)
            {
                throttleOrDynBrakePercent = 0;
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
            if ((!UseTrainBrakeAndDynBrake || !CCIsUsingTrainBrake) && locomotive.TrainBrakeController.MaxPressurePSI - locomotive.BrakeSystem.BrakeLine1PressurePSI > 1)
            {
                reducingForce = true;
                timeFromEngineMoved = 0;
                if (throttleOrDynBrakePercent > 0)
                    throttleOrDynBrakePercent = 0;
                return;
            }

            if (speedRegulatorOptions.Contains("engageforceonnonzerospeed") && SelectedSpeedMpS > 0)
            {
                SpeedSelectorMode = SpeedSelectorMode.On;
                SpeedRegulatorMode = SpeedRegulatorMode.Auto;
                SkipThrottleDisplay = true;
                reducingForce = false;
            }

            if (firstIteration) // if this is executed the first time, let's check all other than player engines in the consist, and record them for further throttle manipulation
            {
                if (!DoComputeNumberOfAxles)
                    SelectedNumberOfAxles = (int)(locomotive.Train.Length / 6.6f); // also set the axles, for better delta computing, if user omits to set it
                firstIteration = false;
            }

            if (SelectedMaxAccelerationStep == 0) // no effort, no throttle (i.e. for reverser change, etc) and return
            {
                throttleOrDynBrakePercent = 0;
                return;
            }

            float deltaSpeedMpS = SetSpeedMpS - absWheelSpeedMpS;
            if (SpeedSelectorMode == SpeedSelectorMode.Parking && !EngineBrakePriority)
            {
                if (throttleOrDynBrakePercent > 0 || absWheelSpeedMpS == 0)
                {
                    throttleOrDynBrakePercent = 0;
                }

                if (absWheelSpeedMpS < (speedIsMph ? Speed.MeterPerSecond.FromMpH(ParkingBrakeEngageSpeed) : Speed.MeterPerSecond.FromKpH(ParkingBrakeEngageSpeed)))
                    locomotive.SetEngineBrakePercent(ParkingBrakePercent);
            }
            else if (SpeedSelectorMode == SpeedSelectorMode.Neutral || SpeedSelectorMode < SpeedSelectorMode.Start && !speedRegulatorOptions.Contains("startfromzero") && absWheelSpeedMpS < SafeSpeedForAutomaticOperationMpS)
            {
                if (deltaSpeedMpS >= 0)
                {
                    // Progressively stop accelerating/braking: reach 0
                    if (throttleOrDynBrakePercent < 0)
                        IncreaseForce(elapsedClockSeconds, 0);
                    else if (throttleOrDynBrakePercent > 0)
                        DecreaseForce(elapsedClockSeconds, 0);
                }
                else // start braking
                {
                    if (throttleOrDynBrakePercent > 0)
                    {
                        DecreaseForce(elapsedClockSeconds, 0);
                    }
                    else
                    {
                        if (locomotive.DynamicBrakeAvailable)
                        {
                            deltaSpeedMpS = SetSpeedMpS + (trainElevation < -0.01 ? trainElevation * (SelectedNumberOfAxles / 12) : 0) - absWheelSpeedMpS;

                            AccelerationDemandMpSS = (float)-Math.Sqrt(-StartReducingSpeedDeltaDownwards * deltaSpeedMpS);
                            if (throttleOrDynBrakePercent < -(AccelerationDemandMpSS * 100) && AccelerationDemandMpSS < -0.05f)
                            {
                                float maxPercent = DynamicBrakeIsSelectedForceDependant ? ((MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps) : 100;
                                DecreaseForce(elapsedClockSeconds, -maxPercent);
                            }

                            if (throttleOrDynBrakePercent > -((AccelerationDemandMpSS - 0.05f) * 100))
                            {
                                IncreaseForce(elapsedClockSeconds, 0);
                            }
                        }
                        else // use TrainBrake
                        {
                            throttleOrDynBrakePercent = 0;
                            if (deltaSpeedMpS > -1)
                            {
                                brakePercent = TrainBrakeMinPercentValue - 3.0f + (-deltaSpeedMpS * 10);
                            }
                            else
                            {
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
            }

            if ((absWheelSpeedMpS > SafeSpeedForAutomaticOperationMpS || SpeedSelectorMode == SpeedSelectorMode.Start || speedRegulatorOptions.Contains("startfromzero")) && (SpeedSelectorMode != SpeedSelectorMode.Neutral && SpeedSelectorMode != SpeedSelectorMode.Parking))
            {
                double coeff = Math.Max(Speed.MeterPerSecond.FromMpS(locomotive.WheelSpeedMpS, !speedIsMph) / 100 * 1.2f, 1);
                if (deltaSpeedMpS >= 0)
                {
                    AccelerationDemandMpSS = (float)Math.Sqrt(startReducingSpeedDelta * coeff * deltaSpeedMpS);
                    if ((SpeedSelectorMode == SpeedSelectorMode.On || SpeedSelectorMode == SpeedSelectorMode.Start) && throttleOrDynBrakePercent <= 0)
                    {
                        IncreaseForce(elapsedClockSeconds, 0);
                    }
                }
                else // start braking
                {
                    if (throttleOrDynBrakePercent > 0)
                    {
                        DecreaseForce(elapsedClockSeconds, 0);
                    }
                    else
                    {
                        if (locomotive.DynamicBrakeAvailable)
                        {
                            double val = Math.Abs((StartReducingSpeedDeltaDownwards) * coeff * ((deltaSpeedMpS + 0.5f) / 3));
                            AccelerationDemandMpSS = -(float)Math.Sqrt(val);
                            if (RelativeAccelerationMpSS > AccelerationDemandMpSS)
                            {
                                float maxPercent = DynamicBrakeIsSelectedForceDependant ? ((MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps) : 100;
                                DecreaseForce(elapsedClockSeconds, -maxPercent);
                            }
                            else if (RelativeAccelerationMpSS + 0.01f < AccelerationDemandMpSS)
                            {
                                IncreaseForce(elapsedClockSeconds, 0);
                            }
                        }
                        else // use TrainBrake
                        {
                            if (deltaSpeedMpS > -1)
                            {
                                brakePercent = TrainBrakeMinPercentValue - 3.0f + (-deltaSpeedMpS * 10);
                            }
                            else
                            {
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

                if (locomotive.Direction != MidpointDirection.N)
                {
                    float a = 0;
                    float demandedPercent = 0;
                    if (throttleOrDynBrakePercent >= 0 && RelativeAccelerationMpSS < AccelerationDemandMpSS)
                    {
                        float newThrottle = 0;
                        // calculate new max force if MaxPowerThreshold is set
                        if (MaxPowerThreshold > 0)
                        {
                            double currentSpeed = Speed.MeterPerSecond.FromMpS(absWheelSpeedMpS, !speedIsMph);
                            float percentComplete = (int)Math.Round((double)(100 * currentSpeed) / MaxPowerThreshold);
                            if (percentComplete > 100)
                                percentComplete = 100;
                            newThrottle = percentComplete;
                        }
                        if (forceStepsThrottleTable.Count > 0)
                        {
                            demandedPercent = forceStepsThrottleTable[(int)SelectedMaxAccelerationStep - 1];
                            if (accelerationTable.Count > 0)
                                a = accelerationTable[(int)SelectedMaxAccelerationStep - 1];
                        }
                        else
                            demandedPercent = (MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps;
                        if (demandedPercent < newThrottle)
                            demandedPercent = newThrottle;
                    }
                    if (reducingForce)
                    {
                        if (demandedPercent > PowerReductionValue)
                            demandedPercent = PowerReductionValue;
                    }
                    if (deltaSpeedMpS > 0 && throttleOrDynBrakePercent >= 0)
                    {
                        float? target = null;
                        if (a > 0 && Speed.MeterPerSecond.FromMpS(locomotive.WheelSpeedMpS, !speedIsMph) > 5)
                        {
                            if (locomotive.AccelerationMpSS < a - 0.02 && deltaSpeedMpS > 0.8f)
                                target = 100;
                            else if ((locomotive.AccelerationMpSS < a - 0.02 && throttleOrDynBrakePercent < demandedPercent) ||
                                    (throttleOrDynBrakePercent > demandedPercent && (deltaSpeedMpS < 0.8f || locomotive.AccelerationMpSS > a + 0.02)))
                            {
                                target = demandedPercent;
                            }
                        }
                        else
                        {
                            if (throttleOrDynBrakePercent < demandedPercent)
                            {
                                float accelDiff = AccelerationDemandMpSS - locomotive.AccelerationMpSS;
                                target = (float)Math.Min(throttleOrDynBrakePercent + accelDiff * 10, demandedPercent);
                            }
                            else
                            {
                                target = demandedPercent;
                            }
                        }
                        if (target > throttleOrDynBrakePercent)
                            IncreaseForce(elapsedClockSeconds, target.Value);
                        else if (target < throttleOrDynBrakePercent)
                            DecreaseForce(elapsedClockSeconds, target.Value);
                    }
                }
            }
            if (locomotive.WheelSpeedMpS == 0 && throttleOrDynBrakePercent < 0)
                throttleOrDynBrakePercent = 0;

            float current = Math.Abs(locomotive.TractiveForceN) / locomotive.MaxForceN * locomotive.MaxCurrentA;
            if (current < PowerBreakoutAmpers)
                breakout = true;
            if (breakout && deltaSpeedMpS > 0.2f)
                breakout = false;
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
                if (antiWheelSpinEquipped)
                    throttleOrDynBrakePercent = Math.Max(throttleOrDynBrakePercent - skidSpeedDegratation, 0);
                if (breakout || locomotive.TrainBrakeController.MaxPressurePSI - locomotive.BrakeSystem.BrakeLine1PressurePSI > 1)
                {
                    throttleOrDynBrakePercent = 0;
                }
            }
        }

        void IncreaseForce(double elapsedClockSeconds, float maxPercent)
        {
            if (throttleOrDynBrakePercent < 0)
            {
                double step = 100 / DynamicBrakeFullRangeDecreaseTimeSeconds * elapsedClockSeconds;
                throttleOrDynBrakePercent = Math.Min(throttleOrDynBrakePercent + step, Math.Min(maxPercent, 0));
            }
            else
            {
                double step = 100 / throttleFullRangeIncreaseTimeSeconds * elapsedClockSeconds;
                throttleOrDynBrakePercent = Math.Min(throttleOrDynBrakePercent + step, maxPercent);
            }
        }

        void DecreaseForce(double elapsedClockSeconds, float minPercent)
        {
            if (throttleOrDynBrakePercent > 0)
            {
                double step = 100 / throttleFullRangeDecreaseTimeSeconds * elapsedClockSeconds;
                throttleOrDynBrakePercent = Math.Min(throttleOrDynBrakePercent - step, Math.Max(minPercent, 0));
            }
            else
            {
                double step = 100 / DynamicBrakeFullRangeIncreaseTimeSeconds * elapsedClockSeconds;
                throttleOrDynBrakePercent = Math.Min(throttleOrDynBrakePercent - step, minPercent);
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
                    float temp = (float)Math.Round(Speed.MeterPerSecond.FromMpS(SetSpeedMpS, metric));
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
                    data = (float)Math.Round(Speed.MeterPerSecond.FromMpS(SelectedSpeedMpS, metric));
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
                    data = restrictedRegionOdometer.Started ? 1 : 0;
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
                    data = restrictedRegionOdometer.Started ? (float)restrictedRegionOdometer.RemainingValue : 0;
                    break;
                case CabViewControlType.Orts_Remaining_Train_Length_Percent:
                    if (SpeedRegulatorMode == SpeedRegulatorMode.Auto && trainLengthMeters == 0)
                    {
                        data = restrictedRegionOdometer.Started ? (float)restrictedRegionOdometer.RemainingValue / trainLengthMeters : 0;
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
    }
}
