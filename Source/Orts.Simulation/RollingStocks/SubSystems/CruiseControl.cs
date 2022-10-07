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
using Orts.Simulation.RollingStocks.SubSystems.Controllers;

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
        private int selectedNumberOfAxles;
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
        private readonly Odometer restrictedRegionOdometer;

        private float startReducingSpeedDelta = 0.5f;
        private float StartReducingSpeedDeltaDownwards;
        private bool throttleNeutralPriority;
        private readonly List<int> forceStepsThrottleTable = new List<int>();

        private float throttleFullRangeIncreaseTimeSeconds = 6;
        private float throttleFullRangeDecreaseTimeSeconds = 6;
        private readonly List<float> accelerationTable = new List<float>();

        public bool DynamicBrakePriority { get; set; }
        private float dynamicBrakeIncreaseSpeed;
        private float dynamicBrakeDecreaseSpeed;
        private float accelerationRampMaxMpSSS = 0.7f;
        private float accelerationDemandMpSS;
        private float accelerationRampMinMpSSS = 0.01f;
        private bool resetForceAfterAnyBraking;
        private float dynamicBrakeFullRangeIncreaseTimeSeconds;
        private float dynamicBrakeFullRangeDecreaseTimeSeconds;
        private float TrainBrakeFullRangeIncreaseTimeSeconds = 10;
        private float trainBrakeFullRangeDecreaseTimeSeconds = 5;
        private float parkingBrakeEngageSpeed;
        private float parkingBrakePercent;
        public bool SkipThrottleDisplay { get; set; }
        private bool disableZeroForceStep;
        private bool dynamicBrakeIsSelectedForceDependant;
        public bool UseThrottleAsSpeedSelector { get; set; }
        public bool UseThrottleAsForceSelector { get; set; }
        public bool ContinuousSpeedIncreasing { get; set; }
        public bool ContinuousSpeedDecreasing { get; set; }
        public float PowerBreakoutAmpers { get; set; }
        public float PowerBreakoutSpeedDelta { get; set; }
        private float powerResumeSpeedDelta;
        private float powerReductionDelayPaxTrain;
        private float powerReductionDelayCargoTrain;
        private float powerReductionValue;
        private float maxPowerThreshold;
        private float safeSpeedForAutomaticOperationMpS;
        private float speedSelectorStepTimeSeconds;
        private double totalTime;
        public bool DisableCruiseControlOnThrottleAndZeroSpeed { get; set; }
        public bool DisableCruiseControlOnThrottleAndZeroForce { get; set; }
        public bool DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed { get; set; }
        private bool forceResetRequiredAfterBraking;
        private bool forceResetIncludeDynamicBrake;
        public bool ZeroSelectedSpeedWhenPassingToThrottleMode { get; set; }
        public bool DynamicBrakeCommandHasPriorityOverCruiseControl { get; set; } = true;
        public bool HasIndependentThrottleDynamicBrakeLever { get; set; }
        public bool HasProportionalSpeedSelector { get; set; }
        private bool speedSelectorIsDiscrete;
        private bool doComputeNumberOfAxles;
        private bool disableManualSwitchToManualWhenSetForceNotAtZero;
        private bool disableManualSwitchToAutoWhenThrottleNotAtZero;
        private bool disableManualSwitchToAutoWhenSetSpeedNotAtTop;
        private bool enableSelectedSpeedSelectionWhenManualModeSet;
        private bool useTrainBrakeAndDynBrake;
        private float speedDeltaToEnableTrainBrake = 5;
        private float speedDeltaToEnableFullTrainBrake = 10;
        public float MinimumSpeedForCCEffectMpS { get; set; }
        private double speedRegulatorIntermediateValue;
        private const float stepSize = 20;
        private float RelativeAccelerationMpSS => locomotive.Direction == MidpointDirection.Reverse ? -locomotive.AccelerationMpSS : locomotive.AccelerationMpSS; // Acceleration relative to state of reverser
        public bool UsingTrainBrake { get; set; } // Cruise control is using (also) train brake to brake
        private float trainBrakeMinPercentValue = 30f; // Minimum train brake settable percent Value
        private float trainBrakeMaxPercentValue = 85f; // Maximum train brake settable percent Value
        private bool startInAutoMode; // at startup cruise control is in auto mode
        private bool throttleNeutralPosition; // when UseThrottleAsSpeedSelector is true and this is true
                                              // and we are in auto mode, the throttle zero position is a neutral position
        private bool throttleLowSpeedPosition; // when UseThrottleAsSpeedSelector is true and this is true
                                               // and we are in auto mode, the first throttle above zero position is used to run at low speed
        private float lowSpeed = 2f; // default parking speed
        private bool hasTwoForceValues; // when UseThrottleAsSpeedSelector is true, two max force values (50% and 100%) are available

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
            dynamicBrakeIncreaseSpeed = source.dynamicBrakeIncreaseSpeed;
            dynamicBrakeDecreaseSpeed = source.dynamicBrakeDecreaseSpeed;
            accelerationRampMaxMpSSS = source.accelerationRampMaxMpSSS;
            accelerationRampMinMpSSS = source.accelerationRampMinMpSSS;
            resetForceAfterAnyBraking = source.resetForceAfterAnyBraking;
            throttleFullRangeIncreaseTimeSeconds = source.throttleFullRangeIncreaseTimeSeconds;
            throttleFullRangeDecreaseTimeSeconds = source.throttleFullRangeDecreaseTimeSeconds;
            dynamicBrakeFullRangeIncreaseTimeSeconds = source.dynamicBrakeFullRangeIncreaseTimeSeconds;
            dynamicBrakeFullRangeDecreaseTimeSeconds = source.dynamicBrakeFullRangeDecreaseTimeSeconds;
            TrainBrakeFullRangeIncreaseTimeSeconds = source.TrainBrakeFullRangeIncreaseTimeSeconds;
            trainBrakeFullRangeDecreaseTimeSeconds = source.trainBrakeFullRangeDecreaseTimeSeconds;
            parkingBrakeEngageSpeed = source.parkingBrakeEngageSpeed;
            parkingBrakePercent = source.parkingBrakePercent;
            disableZeroForceStep = source.disableZeroForceStep;
            dynamicBrakeIsSelectedForceDependant = source.dynamicBrakeIsSelectedForceDependant;
            UseThrottleAsSpeedSelector = source.UseThrottleAsSpeedSelector;
            UseThrottleAsForceSelector = source.UseThrottleAsForceSelector;
            ContinuousSpeedIncreasing = source.ContinuousSpeedIncreasing;
            ContinuousSpeedDecreasing = source.ContinuousSpeedDecreasing;
            PowerBreakoutAmpers = source.PowerBreakoutAmpers;
            PowerBreakoutSpeedDelta = source.PowerBreakoutSpeedDelta;
            powerResumeSpeedDelta = source.powerResumeSpeedDelta;
            powerReductionDelayPaxTrain = source.powerReductionDelayPaxTrain;
            powerReductionDelayCargoTrain = source.powerReductionDelayCargoTrain;
            powerReductionValue = source.powerReductionValue;
            maxPowerThreshold = source.maxPowerThreshold;
            safeSpeedForAutomaticOperationMpS = source.safeSpeedForAutomaticOperationMpS;
            speedSelectorStepTimeSeconds = source.speedSelectorStepTimeSeconds;
            DisableCruiseControlOnThrottleAndZeroSpeed = source.DisableCruiseControlOnThrottleAndZeroSpeed;
            DisableCruiseControlOnThrottleAndZeroForce = source.DisableCruiseControlOnThrottleAndZeroForce;
            DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed = source.DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed;
            forceResetRequiredAfterBraking = source.forceResetRequiredAfterBraking;
            forceResetIncludeDynamicBrake = source.forceResetIncludeDynamicBrake;
            ZeroSelectedSpeedWhenPassingToThrottleMode = source.ZeroSelectedSpeedWhenPassingToThrottleMode;
            DynamicBrakeCommandHasPriorityOverCruiseControl = source.DynamicBrakeCommandHasPriorityOverCruiseControl;
            HasIndependentThrottleDynamicBrakeLever = source.HasIndependentThrottleDynamicBrakeLever;
            HasProportionalSpeedSelector = source.HasProportionalSpeedSelector;
            disableManualSwitchToManualWhenSetForceNotAtZero = source.disableManualSwitchToManualWhenSetForceNotAtZero;
            disableManualSwitchToAutoWhenThrottleNotAtZero = source.disableManualSwitchToAutoWhenThrottleNotAtZero;
            disableManualSwitchToAutoWhenSetSpeedNotAtTop = source.disableManualSwitchToAutoWhenSetSpeedNotAtTop;
            enableSelectedSpeedSelectionWhenManualModeSet = source.enableSelectedSpeedSelectionWhenManualModeSet;
            speedSelectorIsDiscrete = source.speedSelectorIsDiscrete;
            doComputeNumberOfAxles = source.doComputeNumberOfAxles;
            useTrainBrakeAndDynBrake = source.useTrainBrakeAndDynBrake;
            speedDeltaToEnableTrainBrake = source.speedDeltaToEnableTrainBrake;
            speedDeltaToEnableFullTrainBrake = source.speedDeltaToEnableFullTrainBrake;
            MinimumSpeedForCCEffectMpS = source.MinimumSpeedForCCEffectMpS;
            trainBrakeMinPercentValue = source.trainBrakeMinPercentValue;
            trainBrakeMaxPercentValue = source.trainBrakeMaxPercentValue;
            startInAutoMode = source.startInAutoMode;
            throttleNeutralPosition = source.throttleNeutralPosition;
            throttleLowSpeedPosition = source.throttleLowSpeedPosition;
            lowSpeed = source.lowSpeed;
            hasTwoForceValues = source.hasTwoForceValues;
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
                        speedSelectorStepTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 0.1f);
                        break;
                    case "throttlefullrangeincreasetimeseconds":
                        throttleFullRangeIncreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                        break;
                    case "throttlefullrangedecreasetimeseconds":
                        throttleFullRangeDecreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                        break;
                    case "resetforceafteranybraking":
                        resetForceAfterAnyBraking = stf.ReadBoolBlock(false);
                        break;
                    case "dynamicbrakefullrangeincreasetimeseconds":
                        dynamicBrakeFullRangeIncreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                        break;
                    case "dynamicbrakefullrangedecreasetimeseconds":
                        dynamicBrakeFullRangeDecreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                        break;
                    case "trainbrakefullrangeincreasetimeseconds":
                        TrainBrakeFullRangeIncreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 10);
                        break;
                    case "trainbrakefullrangedecreasetimeseconds":
                        trainBrakeFullRangeDecreaseTimeSeconds = stf.ReadFloatBlock(STFReader.Units.Any, 5);
                        break;
                    case "parkingbrakeengagespeed":
                        parkingBrakeEngageSpeed = stf.ReadFloatBlock(STFReader.Units.Speed, 0);
                        break;
                    case "parkingbrakepercent":
                        parkingBrakePercent = stf.ReadFloatBlock(STFReader.Units.Any, 0);
                        break;
                    case "maxpowerthreshold":
                        maxPowerThreshold = stf.ReadFloatBlock(STFReader.Units.Any, 0);
                        break;
                    case "safespeedforautomaticoperationmps":
                        safeSpeedForAutomaticOperationMpS = stf.ReadFloatBlock(STFReader.Units.Any, 0);
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
                        disableManualSwitchToManualWhenSetForceNotAtZero = stf.ReadBoolBlock(false);
                        break;
                    case "disablemanualswitchtoautowhenthrottlenotatzero":
                        disableManualSwitchToAutoWhenThrottleNotAtZero = stf.ReadBoolBlock(false);
                        break;
                    case "disablemanualswitchtoautowhensetspeednotattop":
                        disableManualSwitchToAutoWhenSetSpeedNotAtTop = stf.ReadBoolBlock(false);
                        break;
                    case "enableselectedspeedselectionwhenmanualmodeset":
                        enableSelectedSpeedSelectionWhenManualModeSet = stf.ReadBoolBlock(false);
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
                        powerResumeSpeedDelta = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                        break;
                    case "powerreductiondelaypaxtrain":
                        powerReductionDelayPaxTrain = stf.ReadFloatBlock(STFReader.Units.Any, 0.0f);
                        break;
                    case "powerreductiondelaycargotrain":
                        powerReductionDelayCargoTrain = stf.ReadFloatBlock(STFReader.Units.Any, 0.0f);
                        break;
                    case "powerreductionvalue":
                        powerReductionValue = stf.ReadFloatBlock(STFReader.Units.Any, 100.0f);
                        break;
                    case "disablezeroforcestep":
                        disableZeroForceStep = stf.ReadBoolBlock(false);
                        break;
                    case "dynamicbrakeisselectedforcedependant":
                        dynamicBrakeIsSelectedForceDependant = stf.ReadBoolBlock(false);
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
                        dynamicBrakeIncreaseSpeed = stf.ReadFloatBlock(STFReader.Units.Any, 0.5f);
                        break;
                    case "dynamicbrakedecreasespeed":
                        dynamicBrakeDecreaseSpeed = stf.ReadFloatBlock(STFReader.Units.Any, 0.5f);
                        break;
                    case "forceresetrequiredafterbraking":
                        forceResetRequiredAfterBraking = stf.ReadBoolBlock(false);
                        break;
                    case "forceresetincludedynamicbrake":
                        forceResetIncludeDynamicBrake = stf.ReadBoolBlock(false);
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
                        speedSelectorIsDiscrete = stf.ReadBoolBlock(false);
                        break;
                    case "usetrainbrakeanddynbrake":
                        useTrainBrakeAndDynBrake = stf.ReadBoolBlock(false);
                        break;
                    case "speeddeltatoenabletrainbrake":
                        speedDeltaToEnableTrainBrake = stf.ReadFloatBlock(STFReader.Units.Speed, 5f);
                        break;
                    case "speeddeltatoenablefulltrainbrake":
                        speedDeltaToEnableFullTrainBrake = stf.ReadFloatBlock(STFReader.Units.Speed, 10f);
                        break;
                    case "minimumspeedforcceffect":
                        MinimumSpeedForCCEffectMpS = stf.ReadFloatBlock(STFReader.Units.Speed, 0f);
                        break;
                    case "trainbrakeminpercentvalue":
                        trainBrakeMinPercentValue = stf.ReadFloatBlock(STFReader.Units.Any, 0.3f);
                        break;
                    case "trainbrakemaxpercentvalue":
                        trainBrakeMaxPercentValue = stf.ReadFloatBlock(STFReader.Units.Any, 0.85f);
                        break;
                    case "startinautomode":
                        startInAutoMode = stf.ReadBoolBlock(false);
                        break;
                    case "throttleneutralposition":
                        throttleNeutralPosition = stf.ReadBoolBlock(false);
                        break;
                    case "throttlelowspeedposition":
                        throttleLowSpeedPosition = stf.ReadBoolBlock(false);
                        break;
                    case "lowspeed":
                        lowSpeed = stf.ReadFloatBlock(STFReader.Units.Speed, 2f);
                        break;
                    case "hasttwoforcevalues":
                        hasTwoForceValues = stf.ReadBoolBlock(false);
                        break;
                    case "docomputenumberofaxles":
                        doComputeNumberOfAxles = stf.ReadBoolBlock(false);
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
            if (dynamicBrakeFullRangeIncreaseTimeSeconds == 0)
                dynamicBrakeFullRangeIncreaseTimeSeconds = 4;
            if (dynamicBrakeFullRangeDecreaseTimeSeconds == 0)
                dynamicBrakeFullRangeDecreaseTimeSeconds = 6;
            ComputeNumberOfAxles();
            if (StartReducingSpeedDeltaDownwards == 0)
                StartReducingSpeedDeltaDownwards = startReducingSpeedDelta;
            if (startInAutoMode)
                SpeedRegulatorMode = SpeedRegulatorMode.Auto;
        }

        private void ComputeNumberOfAxles()
        {
            if (doComputeNumberOfAxles && locomotive == simulator.PlayerLocomotive)
            {
                selectedNumberOfAxles = 0;
                foreach (TrainCar tc in locomotive.Train.Cars)
                {
                    selectedNumberOfAxles += tc.WheelAxles.Count;
                }
            }
        }

        public void Update(double elapsedClockSeconds)
        {
            if (!locomotive.IsPlayerTrain || locomotive != locomotive.Train.LeadLocomotive)
            {
                wasForceReset = false;
                throttleOrDynBrakePercent = 0;
                trainBrakePercent = 0;
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
                trainBrakePercent = 0;
            }
            else if (SpeedRegulatorMode == SpeedRegulatorMode.Manual || (SpeedRegulatorMode == SpeedRegulatorMode.Auto && (DynamicBrakePriority || throttleNeutralPriority)))
            {
                wasForceReset = false;
                throttleOrDynBrakePercent = 0;
                trainBrakePercent = 0;
            }
            else if (SpeedRegulatorMode == SpeedRegulatorMode.Auto)
            {
                float prevTrainBrakePercent = trainBrakePercent;
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
                if (prevTrainBrakePercent != trainBrakePercent && !TrainBrakePriority)
                {
                    locomotive.TrainBrakeController.SetPercent(trainBrakePercent); // TODO: do not move actual train brake lever
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
            outf.Write(selectedNumberOfAxles);
            outf.Write(SelectedSpeedMpS);
            outf.Write(DynamicBrakePriority);
            outf.Write((int)SpeedRegulatorMode);
            outf.Write((int)SpeedSelectorMode);
            outf.Write(trainBrakePercent);
            outf.Write(trainLengthMeters);
            outf.Write(speedRegulatorIntermediateValue);
            outf.Write(UsingTrainBrake);
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
            selectedNumberOfAxles = inf.ReadInt32();
            SelectedSpeedMpS = inf.ReadSingle();
            DynamicBrakePriority = inf.ReadBoolean();
            SpeedRegulatorMode = (SpeedRegulatorMode)inf.ReadInt32();
            SpeedSelectorMode = (SpeedSelectorMode)inf.ReadInt32();
            trainBrakePercent = inf.ReadSingle();
            trainLengthMeters = inf.ReadInt32();
            speedRegulatorIntermediateValue = inf.ReadDouble();
            UsingTrainBrake = inf.ReadBoolean();
        }

        public void UpdateSelectedSpeed(double elapsedClockSeconds)
        {
            totalTime += elapsedClockSeconds;
            if (SpeedRegulatorMode == SpeedRegulatorMode.Auto && !DynamicBrakePriority ||
             enableSelectedSpeedSelectionWhenManualModeSet)
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
               ((disableManualSwitchToAutoWhenThrottleNotAtZero && (locomotive.ThrottlePercent != 0 ||
               (locomotive.DynamicBrakePercent != -1 && locomotive.DynamicBrakePercent != 0))) ||
               (disableManualSwitchToAutoWhenSetSpeedNotAtTop && SelectedSpeedMpS != locomotive.MaxSpeedMpS && locomotive.AbsSpeedMpS > Simulator.MaxStoppedMpS)))
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
                            if (!disableManualSwitchToAutoWhenSetSpeedNotAtTop && !KeepSelectedSpeedWhenManualModeSet)
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
                (disableManualSwitchToManualWhenSetForceNotAtZero && SelectedMaxAccelerationStep != 0))
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

            if (disableZeroForceStep)
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
            if (disableZeroForceStep)
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
            MultiPositionController multiPositionController = locomotive.MultiPositionControllers.Where(x => x.ControllerBinding == CruiseControllerBinding.SelectedSpeed && !x.StateChanged).FirstOrDefault();
            if (multiPositionController != null)
            {
                multiPositionController.StateChanged = true;
                if (SpeedRegulatorMode != SpeedRegulatorMode.Auto && (ForceRegulatorAutoWhenNonZeroSpeedSelected ||
                    SelectedMaxAccelerationStep == 0 && DisableCruiseControlOnThrottleAndZeroForce && ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero &&
                    locomotive.ThrottleController.CurrentValue == 0 && locomotive.DynamicBrakeController.CurrentValue == 0))
                {
                    SpeedRegulatorMode = SpeedRegulatorMode.Auto;
                }

                multiPositionController.DoMovement(Movement.Forward);
                return;
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
            MultiPositionController multiPositionController = locomotive.MultiPositionControllers.Where(x => x.ControllerBinding == CruiseControllerBinding.SelectedSpeed).FirstOrDefault();
            if (multiPositionController != null)
            {
                multiPositionController.StateChanged = false;
                multiPositionController.DoMovement(Movement.Neutral);
                return;
            }
            if (UseThrottleAsSpeedSelector || HasProportionalSpeedSelector)
                selectedSpeedIncreasing = false;
            else
                SpeedSelectorModeStopIncrease();
        }

        public void SpeedRegulatorSelectedSpeedIncrease()
        {
            if (selectedSpeedLeverHoldTime + speedSelectorStepTimeSeconds > totalTime)
                return;
            selectedSpeedLeverHoldTime = totalTime;

            SelectedSpeedMpS = Math.Max(MinimumSpeedForCCEffectMpS, SelectedSpeedMpS + SpeedRegulatorNominalSpeedStepMpS);
            if (SelectedSpeedMpS > locomotive.MaxSpeedMpS)
                SelectedSpeedMpS = locomotive.MaxSpeedMpS;

            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Selected speed changed to {FormatStrings.FormatSpeedLimit(SelectedSpeedMpS, !speedIsMph)}"));
        }

        public void SpeedRegulatorSelectedSpeedStartDecrease()
        {
            MultiPositionController multiPositionController = locomotive.MultiPositionControllers.Where(x => x.ControllerBinding == CruiseControllerBinding.SelectedSpeed && !x.StateChanged).FirstOrDefault();
            if (multiPositionController != null)
            {
                multiPositionController.StateChanged = true;
                multiPositionController.DoMovement(Movement.Backward);
                return;
            }
            if (UseThrottleAsSpeedSelector || HasProportionalSpeedSelector)
                SelectedSpeedDecreasing = true;
            else
                SpeedSelectorModeDecrease();
        }

        public void SpeedRegulatorSelectedSpeedStopDecrease()
        {
            MultiPositionController multiPositionController = locomotive.MultiPositionControllers.Where(x => x.ControllerBinding == CruiseControllerBinding.SelectedSpeed).FirstOrDefault();
            if (multiPositionController != null)
            {
                multiPositionController.StateChanged = false;
                multiPositionController.DoMovement(Movement.Neutral);
                return;
            }
            SelectedSpeedDecreasing = false;
        }

        public void SpeedRegulatorSelectedSpeedDecrease()
        {
            if (selectedSpeedLeverHoldTime + speedSelectorStepTimeSeconds > totalTime)
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
                double deltaSpeed = speedSelectorIsDiscrete ? (metric ? Speed.MeterPerSecond.FromKpH((float)Math.Round(movExtension * maxValue / speedRegulatorNominalSpeedStepKpHOrMpH) * speedRegulatorNominalSpeedStepKpHOrMpH) :
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
            selectedNumberOfAxles += ByAmount;
            trainLength = selectedNumberOfAxles * 6.6f;
            trainLengthMeters = (int)Math.Round(trainLength + 0.5, 0);
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Number of axles increased to {selectedNumberOfAxles}"));
        }

        public void NumberOfAxlesDecrease()
        {
            NumberOfAxlesDecrease(1);
        }

        public void NumberOfAxlesDecrease(int ByAmount)
        {
            if ((selectedNumberOfAxles - ByAmount) < 1)
                return;
            selectedNumberOfAxles -= ByAmount;
            trainLength = selectedNumberOfAxles * 6.6f;
            trainLengthMeters = (int)Math.Round(trainLength + 0.5, 0);
            simulator.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString($"Number of axles decreased to {selectedNumberOfAxles}"));
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

            if ((SpeedSelectorMode == SpeedSelectorMode.On || SpeedSelectorMode == SpeedSelectorMode.Start) && !TrainBrakePriority)
            {
                if (locomotive.AbsSpeedMpS == 0)
                {
                    timeFromEngineMoved = 0;
                    reducingForce = true;
                }
                else if (reducingForce)
                {
                    timeFromEngineMoved += elapsedClockSeconds;
                    float timeToReduce = locomotive.SelectedTrainType == TrainCategory.Passenger ? powerReductionDelayPaxTrain : powerReductionDelayCargoTrain;
                    if (timeFromEngineMoved > timeToReduce)
                        reducingForce = false;
                }
            }
            else
            {
                timeFromEngineMoved = 0;
                reducingForce = true;
            }

            if (TrainBrakePriority)
            {
                wasForceReset = false;
                wasBraking = true;
            }
            else if (SpeedSelectorMode == SpeedSelectorMode.Start)
                wasForceReset = true;
            else if (SelectedMaxAccelerationPercent == 0 && SelectedMaxAccelerationStep == 0)
            {
                wasBraking = false;
                wasForceReset = true;
            }
            if (TrainBrakePriority || (throttleNeutralPosition && SelectedSpeedMpS == 0) || SelectedMaxAccelerationStep == 0 ||
                (forceResetRequiredAfterBraking &&
                    (!wasForceReset || (wasBraking && (SelectedMaxAccelerationStep > 0 || SelectedMaxAccelerationPercent > 0)))))
            {
                if (SpeedSelectorMode == SpeedSelectorMode.Parking)
                    if (absWheelSpeedMpS < (speedIsMph ? Speed.MeterPerSecond.FromMpH(parkingBrakeEngageSpeed) : Speed.MeterPerSecond.FromKpH(parkingBrakeEngageSpeed)))
                        locomotive.SetEngineBrakePercent(parkingBrakePercent);
                throttleOrDynBrakePercent = 0;
                trainBrakePercent = 0;
                return;
            }
            if (locomotive.TrainBrakeController.MaxPressurePSI - locomotive.BrakeSystem.BrakeLine1PressurePSI > 1)
            {
                if (!useTrainBrakeAndDynBrake || !UsingTrainBrake)
                {
                    reducingForce = true;
                    timeFromEngineMoved = 0;
                    if (throttleOrDynBrakePercent > 0)
                        throttleOrDynBrakePercent = 0;
                    return;
                }
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
                if (!doComputeNumberOfAxles)
                    selectedNumberOfAxles = (int)(locomotive.Train.Length / 6.6f); // also set the axles, for better delta computing, if user omits to set it
                firstIteration = false;
            }

            float deltaSpeedMpS = SetSpeedMpS - absWheelSpeedMpS;
            if (SpeedSelectorMode == SpeedSelectorMode.Parking && !EngineBrakePriority)
            {
                if (throttleOrDynBrakePercent > 0 || absWheelSpeedMpS == 0)
                {
                    throttleOrDynBrakePercent = 0;
                }

                if (absWheelSpeedMpS < (speedIsMph ? Speed.MeterPerSecond.FromMpH(parkingBrakeEngageSpeed) : Speed.MeterPerSecond.FromKpH(parkingBrakeEngageSpeed)))
                    locomotive.SetEngineBrakePercent(parkingBrakePercent);
            }
            else if (SpeedSelectorMode == SpeedSelectorMode.Neutral || SpeedSelectorMode < SpeedSelectorMode.Start && !speedRegulatorOptions.Contains("startfromzero") && absWheelSpeedMpS < safeSpeedForAutomaticOperationMpS)
            {
                if (deltaSpeedMpS >= 0)
                {
                    // Progressively stop accelerating/braking: reach 0
                    if (throttleOrDynBrakePercent < 0)
                        IncreaseForce(elapsedClockSeconds, 0);
                    else if (throttleOrDynBrakePercent > 0)
                        DecreaseForce(elapsedClockSeconds, 0);
                    trainBrakePercent = 0;
                }
                else // start braking
                {
                    if (throttleOrDynBrakePercent > 0)
                    {
                        DecreaseForce(elapsedClockSeconds, 0);
                    }
                    else
                    {
                        deltaSpeedMpS = SetSpeedMpS + (trainElevation < -0.01 ? trainElevation * (selectedNumberOfAxles / 12) : 0) - absWheelSpeedMpS;
                        if (locomotive.DynamicBrakeAvailable)
                        {
                            deltaSpeedMpS = SetSpeedMpS + (trainElevation < -0.01 ? trainElevation * (selectedNumberOfAxles / 12) : 0) - absWheelSpeedMpS;

                            accelerationDemandMpSS = (float)-Math.Sqrt(-StartReducingSpeedDeltaDownwards * deltaSpeedMpS);
                            if (throttleOrDynBrakePercent < -(accelerationDemandMpSS * 100) && accelerationDemandMpSS < -0.05f)
                            {
                                float maxPercent = dynamicBrakeIsSelectedForceDependant ? ((MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps) : 100;
                                DecreaseForce(elapsedClockSeconds, -maxPercent);
                            }

                            if (throttleOrDynBrakePercent > -((accelerationDemandMpSS - 0.05f) * 100))
                            {
                                IncreaseForce(elapsedClockSeconds, 0);
                            }
                        }
                        if (useTrainBrakeAndDynBrake || !locomotive.DynamicBrakeAvailable) // use TrainBrake
                            SetTrainBrake(elapsedClockSeconds, deltaSpeedMpS);
                    }
                }
            }

            if ((absWheelSpeedMpS > safeSpeedForAutomaticOperationMpS || SpeedSelectorMode == SpeedSelectorMode.Start || speedRegulatorOptions.Contains("startfromzero")) && (SpeedSelectorMode != SpeedSelectorMode.Neutral && SpeedSelectorMode != SpeedSelectorMode.Parking))
            {
                double coeff = Math.Max(Speed.MeterPerSecond.FromMpS(locomotive.WheelSpeedMpS, !speedIsMph) / 100 * 1.2f, 1);
                if (deltaSpeedMpS >= 0)
                {
                    accelerationDemandMpSS = (float)Math.Sqrt(startReducingSpeedDelta * coeff * deltaSpeedMpS);
                    if ((SpeedSelectorMode == SpeedSelectorMode.On || SpeedSelectorMode == SpeedSelectorMode.Start) && throttleOrDynBrakePercent < 0)
                    {
                        IncreaseForce(elapsedClockSeconds, 0);
                    }
                    trainBrakePercent = 0;
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
                            accelerationDemandMpSS = -(float)Math.Sqrt(val);
                            if (RelativeAccelerationMpSS > accelerationDemandMpSS)
                            {
                                float maxPercent = dynamicBrakeIsSelectedForceDependant ? ((MaxForceSelectorIsDiscrete ? (int)SelectedMaxAccelerationStep : SelectedMaxAccelerationStep) * 100 / SpeedRegulatorMaxForceSteps) : 100;
                                DecreaseForce(elapsedClockSeconds, -maxPercent);
                            }
                            else if (RelativeAccelerationMpSS + 0.01f < accelerationDemandMpSS)
                            {
                                IncreaseForce(elapsedClockSeconds, 0);
                            }
                        }
                        if (useTrainBrakeAndDynBrake || !locomotive.DynamicBrakeAvailable) // use TrainBrake
                            SetTrainBrake(elapsedClockSeconds, deltaSpeedMpS);
                    }
                }

                if (locomotive.Direction != MidpointDirection.N)
                {
                    float a = 0;
                    float demandedPercent = 0;
                    if (throttleOrDynBrakePercent >= 0 && RelativeAccelerationMpSS < accelerationDemandMpSS)
                    {
                        float newThrottle = 0;
                        // calculate new max force if MaxPowerThreshold is set
                        if (maxPowerThreshold > 0)
                        {
                            double currentSpeed = Speed.MeterPerSecond.FromMpS(absWheelSpeedMpS, !speedIsMph);
                            float percentComplete = (int)Math.Round((double)(100 * currentSpeed) / maxPowerThreshold);
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
                        if (demandedPercent > powerReductionValue)
                            demandedPercent = powerReductionValue;
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
                                float accelDiff = accelerationDemandMpSS - locomotive.AccelerationMpSS;
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
                double step = 100 / dynamicBrakeFullRangeDecreaseTimeSeconds * elapsedClockSeconds;
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
                throttleOrDynBrakePercent = Math.Max(throttleOrDynBrakePercent - step, Math.Max(minPercent, 0));
            }
            else
            {
                double step = 100 / dynamicBrakeFullRangeIncreaseTimeSeconds * elapsedClockSeconds;
                throttleOrDynBrakePercent = Math.Max(throttleOrDynBrakePercent - step, minPercent);
            }
        }

        private void SetTrainBrake(double elapsedClockSeconds, float deltaSpeedMpS)
        {
            if (deltaSpeedMpS > -speedDeltaToEnableFullTrainBrake)
            {
                if (!locomotive.DynamicBrakeAvailable || deltaSpeedMpS < -speedDeltaToEnableTrainBrake)
                {
                    UsingTrainBrake = true;
                    trainBrakePercent = trainBrakeMinPercentValue - 3.0f + (-deltaSpeedMpS * 10) / speedDeltaToEnableTrainBrake;
                }
                else
                {
                    trainBrakePercent = 0;
                }
            }
            else
            {
                UsingTrainBrake = true;
                if (RelativeAccelerationMpSS > -maxDecelerationMpSS + 0.01f)
                    trainBrakePercent += (float)(100 / TrainBrakeFullRangeIncreaseTimeSeconds * elapsedClockSeconds);
                else if (RelativeAccelerationMpSS < -maxDecelerationMpSS - 0.01f)
                    trainBrakePercent -= (float)(100 / trainBrakeFullRangeDecreaseTimeSeconds * elapsedClockSeconds);
                trainBrakePercent = MathHelper.Clamp(trainBrakePercent, trainBrakeMinPercentValue - 3.0f, trainBrakeMaxPercentValue);
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
                    data = selectedNumberOfAxles % 10;
                    break;
                case CabViewControlType.Orts_Number_Of_Axes_Display_Tens:
                    data = (selectedNumberOfAxles / 10) % 10;
                    break;
                case CabViewControlType.Orts_Number_Of_Axes_Display_Hundreds:
                    data = (selectedNumberOfAxles / 100) % 10;
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
