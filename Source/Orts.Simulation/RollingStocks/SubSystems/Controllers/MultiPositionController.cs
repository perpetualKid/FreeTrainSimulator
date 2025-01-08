// COPYRIGHT 2010, 2011, 2012, 2013 by the Open Rails project.
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
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.Imported.State;

using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;

namespace Orts.Simulation.RollingStocks.SubSystems.Controllers
{
    public class MultiPositionController : ISaveStateApi<ControllerSaveState>
    {

        private const float FullRangeIncreaseTime = 6.0f;
        private const float DynamicBrakeIncreaseStepPerSecond = 50f;
        private const float DynamicBrakeDecreaseStepPerSecond = 25f;
        private const float ThrottleIncreaseStepPerSecond = 15f;
        private const float ThrottleDecreaseStepPerSecond = 15f;

        private readonly MSTSLocomotive locomotive;
        private readonly Simulator simulator;

        private bool messageDisplayed;

        private double elapsedSecondsFromLastChange;
        private bool checkNeutral;
        private bool noKeyPressed = true;
        private CruiseControllerPosition currentPosition = CruiseControllerPosition.Undefined;
        private bool emergencyBrake;
        private bool previousDriveModeWasAddPower;
        private bool isBraking;
        private bool needPowerUpAfterBrake;
        private bool movedForward;
        private bool movedAft;
        private bool haveCruiseControl;

        private readonly List<Position> positionsList = new List<Position>();
        public bool StateChanged { get; set; }
        public CruiseControllerPosition ControllerPosition { get; private set; }
        public CruiseControllerBinding ControllerBinding { get; private set; }
        public bool CanControlTrainBrake { get; set; }
        public int ControllerId { get; set; }
        public bool MouseInputActive { get; set; }

        public MultiPositionController(MSTSLocomotive locomotive)
        {
            simulator = Simulator.Instance;
            this.locomotive = locomotive;

            ControllerPosition = CruiseControllerPosition.Neutral;
        }

        public MultiPositionController(MultiPositionController source, MSTSLocomotive locomotive)
        {
            ArgumentNullException.ThrowIfNull(source);

            simulator = Simulator.Instance;
            this.locomotive = locomotive;

            positionsList = source.positionsList;
            ControllerBinding = source.ControllerBinding;
            ControllerId = source.ControllerId;
            CanControlTrainBrake = source.CanControlTrainBrake;
        }

        public ValueTask<ControllerSaveState> Snapshot()
        {
            return ValueTask.FromResult(new ControllerSaveState()
            { 
                CheckNeutral = checkNeutral,
                AnyKeyPressed = !noKeyPressed,
                EmergencyBrake = emergencyBrake,
                Braking = isBraking,
                ElapsedTimer = elapsedSecondsFromLastChange,
                ControllerPosition = ControllerPosition,
                CurrentPosition = currentPosition,
                AddPowerMode = previousDriveModeWasAddPower,
                StateChanged = StateChanged,
            });
        }

        public ValueTask Restore(ControllerSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));
            
            checkNeutral = saveState.CheckNeutral;
            emergencyBrake = saveState.EmergencyBrake;
            isBraking = saveState.Braking;
            noKeyPressed = !saveState.AnyKeyPressed;
            elapsedSecondsFromLastChange = saveState.ElapsedTimer;
            ControllerPosition = saveState.ControllerPosition;
            currentPosition = saveState.CurrentPosition;
            previousDriveModeWasAddPower = saveState.AddPowerMode;
            StateChanged = saveState.StateChanged;
            return ValueTask.CompletedTask;
        }

        public void Parse(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new[] {
                new STFReader.TokenProcessor("positions", () => {
                    stf.MustMatch("(");
                    stf.ParseBlock(new [] {
                        new STFReader.TokenProcessor("position", ()=>{
                            stf.MustMatch("(");
                            string positionType = stf.ReadString();
                            string positionFlag = stf.ReadString();
                            string positionName = stf.ReadString();
                            stf.SkipRestOfBlock();
                            positionsList.Add(new Position(positionType, positionFlag, positionName));
                        }),
                    });
                }),
                new STFReader.TokenProcessor("controllerbinding", () =>
                {
                        string binding = stf.ReadStringBlock("null");
                        if (EnumExtension.GetValue(binding, out CruiseControllerBinding controllerBinding))
                            ControllerBinding = controllerBinding;
                }),
                new STFReader.TokenProcessor("controllerid", () => ControllerId = stf.ReadIntBlock(0)),
                new STFReader.TokenProcessor("cancontroltrainbrake", () => CanControlTrainBrake = stf.ReadBoolBlock(false)),
            });
        }

        public void Initialize()
        {
            if (locomotive.CruiseControl != null)
                haveCruiseControl = true;
            foreach (Position pair in positionsList)
            {
                if (pair.Flag == Simulation.ControllerPosition.Default)
                {
                    currentPosition = pair.Type;
                    break;
                }
            }
        }

        public void Update(double elapsedClockSeconds)
        {
            if (!locomotive.IsPlayerTrain)
                return;

            if (haveCruiseControl)
                if (locomotive.CruiseControl.DynamicBrakePriority)
                    return;

            ReloadPositions();
            if (locomotive.AbsSpeedMpS > 0)
            {
                if (emergencyBrake)
                {
                    locomotive.TrainBrakeController.EmergencyBrakingPushButton = true;
                    return;
                }
            }
            else
            {
                emergencyBrake = false;
            }
            elapsedSecondsFromLastChange += elapsedClockSeconds;
            if (checkNeutral)
            {
                // Check every 200 ms if state of MPC has changed
                if (elapsedSecondsFromLastChange > 0.2)
                {
                    CheckNeutralPosition();
                    checkNeutral = false;
                }
            }
            bool ccAutoMode = false;
            if (haveCruiseControl)
            {
                if (locomotive.CruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Auto)
                {
                    ccAutoMode = true;
                }

            }
            if (!haveCruiseControl || !ccAutoMode)
            {
                if (ControllerPosition == CruiseControllerPosition.ThrottleIncrease)
                {
                    if (locomotive.DynamicBrakePercent < 1)
                    {
                        if (locomotive.ThrottlePercent < 100)
                        {
                            float step = (haveCruiseControl ? ThrottleIncreaseStepPerSecond : 100 / FullRangeIncreaseTime);
                            step *= (float)elapsedClockSeconds;
                            locomotive.SetThrottlePercent(locomotive.ThrottlePercent + step);
                        }
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.ThrottleIncreaseFast)
                {
                    if (locomotive.DynamicBrakePercent < 1)
                    {
                        if (locomotive.ThrottlePercent < 100)
                        {
                            float step = (haveCruiseControl ? ThrottleIncreaseStepPerSecond * 2 : 200 / FullRangeIncreaseTime);
                            step *= (float)elapsedClockSeconds;
                            locomotive.SetThrottlePercent(locomotive.ThrottlePercent + step);
                        }
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.ThrottleDecrease)
                {
                    if (locomotive.ThrottlePercent > 0)
                    {
                        float step = (haveCruiseControl ? 100 / ThrottleDecreaseStepPerSecond : 100 / FullRangeIncreaseTime);
                        step *= (float)elapsedClockSeconds;
                        locomotive.SetThrottlePercent(locomotive.ThrottlePercent - step);
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.ThrottleDecreaseFast)
                {
                    if (locomotive.ThrottlePercent > 0)
                    {
                        float step = (haveCruiseControl ? 100 / ThrottleDecreaseStepPerSecond * 2 : 200 / FullRangeIncreaseTime);
                        step *= (float)elapsedClockSeconds;
                        locomotive.SetThrottlePercent(locomotive.ThrottlePercent - step);
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.Neutral || ControllerPosition == CruiseControllerPosition.DynamicBrakeHold)
                {
                    if (CanControlTrainBrake)
                    {
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Apply)
                        {
                            locomotive.StartTrainBrakeDecrease(null);
                        }
                        else if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Neutral)
                        {
                            locomotive.StopTrainBrakeDecrease();
                        }
                    }
                    if (locomotive.ThrottlePercent < 2 && ControllerBinding == CruiseControllerBinding.Throttle)
                    {
                        if (locomotive.ThrottlePercent != 0)
                            locomotive.SetThrottlePercent(0);
                    }
                    if (locomotive.ThrottlePercent > 1 && ControllerBinding == CruiseControllerBinding.Throttle)
                    {
                        locomotive.SetThrottlePercent(locomotive.ThrottlePercent - 1f);
                    }
                    if (locomotive.ThrottlePercent > 100 && ControllerBinding == CruiseControllerBinding.Throttle)
                    {
                        locomotive.ThrottlePercent = 100;
                    }

                }
                if (ControllerPosition == CruiseControllerPosition.DynamicBrakeIncrease)
                {
                    if (CanControlTrainBrake)
                    {
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Apply)
                        {
                            locomotive.StartTrainBrakeDecrease(null);
                        }
                        else if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Neutral)
                        {
                            locomotive.StopTrainBrakeDecrease();
                        }
                    }
                    if (locomotive.DynamicBrakePercent == -1)
                        locomotive.SetDynamicBrakePercent(0);
                    if (locomotive.ThrottlePercent < 1 && locomotive.DynamicBrakePercent < 100)
                    {
                        locomotive.SetDynamicBrakePercent((float)(locomotive.DynamicBrakePercent + DynamicBrakeIncreaseStepPerSecond * elapsedClockSeconds));
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.DynamicBrakeIncreaseFast)
                {
                    if (CanControlTrainBrake)
                    {
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Apply)
                        {
                            locomotive.StartTrainBrakeDecrease(null);
                        }
                        else if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Neutral)
                        {
                            locomotive.StopTrainBrakeDecrease();
                        }
                    }
                    if (locomotive.DynamicBrakePercent == -1)
                        locomotive.SetDynamicBrakePercent(0);
                    if (locomotive.ThrottlePercent < 1 && locomotive.DynamicBrakePercent < 100)
                    {
                        locomotive.SetDynamicBrakePercent((float)(locomotive.DynamicBrakePercent + DynamicBrakeIncreaseStepPerSecond * elapsedClockSeconds));
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.DynamicBrakeDecrease)
                {
                    if (locomotive.DynamicBrakePercent > 0)
                    {
                        locomotive.SetDynamicBrakePercent((float)(locomotive.DynamicBrakePercent - DynamicBrakeDecreaseStepPerSecond * elapsedClockSeconds));
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.Drive || ControllerPosition == CruiseControllerPosition.ThrottleHold)
                {
                    if (locomotive.DynamicBrakePercent < 2)
                    {
                        locomotive.SetDynamicBrakePercent(-1);
                    }
                    if (locomotive.DynamicBrakePercent > 1)
                    {
                        locomotive.SetDynamicBrakePercent(locomotive.DynamicBrakePercent - 1);
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.TrainBrakeIncrease)
                {
                    if (CanControlTrainBrake)
                    {
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Apply)
                        {
                            locomotive.StartTrainBrakeIncrease(null);
                        }
                        else
                        {
                            locomotive.StopTrainBrakeIncrease();
                        }
                    }
                }
                else if (ControllerPosition == CruiseControllerPosition.Drive)
                {
                    if (CanControlTrainBrake)
                    {
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Release)
                        {
                            locomotive.StartTrainBrakeDecrease(null);
                        }
                        else
                            locomotive.StopTrainBrakeDecrease();
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.TrainBrakeDecrease)
                {
                    if (CanControlTrainBrake)
                    {
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Release)
                        {
                            locomotive.StartTrainBrakeDecrease(null);
                        }
                        else
                            locomotive.StopTrainBrakeDecrease();
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.EmergencyBrake)
                {
                    EmergencyBrakes();
                    emergencyBrake = true;
                }
                if (ControllerPosition == CruiseControllerPosition.ThrottleIncreaseOrDynamicBrakeDecrease)
                {
                    if (locomotive.DynamicBrakePercent > 0)
                    {
                        locomotive.SetDynamicBrakePercent((float)(locomotive.DynamicBrakePercent - DynamicBrakeDecreaseStepPerSecond * elapsedClockSeconds));
                        if (locomotive.DynamicBrakePercent < 2)
                        {
                            locomotive.SetDynamicBrakePercent(0);
                            locomotive.DynamicBrakeChangeActiveState(false);
                        }
                    }
                    else
                    {
                        if (locomotive.ThrottlePercent < 100)
                            locomotive.SetThrottlePercent((float)(locomotive.ThrottlePercent + ThrottleIncreaseStepPerSecond * elapsedClockSeconds));
                        if (locomotive.ThrottlePercent > 100)
                            locomotive.SetThrottlePercent(100);
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.ThrottleIncreaseOrDynamicBrakeDecreaseFast)
                {
                    if (locomotive.DynamicBrakePercent > 0)
                    {
                        locomotive.SetDynamicBrakePercent((float)(locomotive.DynamicBrakePercent - DynamicBrakeDecreaseStepPerSecond * 2 * elapsedClockSeconds));
                        if (locomotive.DynamicBrakePercent < 2)
                        {
                            locomotive.SetDynamicBrakePercent(0);
                            locomotive.DynamicBrakeChangeActiveState(false);
                        }
                    }
                    else
                    {
                        if (locomotive.ThrottlePercent < 100)
                            locomotive.SetThrottlePercent((float)(locomotive.ThrottlePercent + ThrottleIncreaseStepPerSecond * 2 * elapsedClockSeconds));
                        if (locomotive.ThrottlePercent > 100)
                            locomotive.SetThrottlePercent(100);
                    }
                }

                if (ControllerPosition == CruiseControllerPosition.DynamicBrakeIncreaseOrThrottleDecrease)
                {
                    if (locomotive.ThrottlePercent > 0)
                    {
                        locomotive.SetThrottlePercent((float)(locomotive.ThrottlePercent - ThrottleDecreaseStepPerSecond * elapsedClockSeconds));
                        if (locomotive.ThrottlePercent < 0)
                            locomotive.ThrottlePercent = 0;
                    }
                    else
                    {
                        if (locomotive.DynamicBrakePercent < 100)
                        {
                            locomotive.SetDynamicBrakePercent((float)(locomotive.DynamicBrakePercent + DynamicBrakeIncreaseStepPerSecond * elapsedClockSeconds));
                        }
                        if (locomotive.DynamicBrakePercent > 100)
                            locomotive.SetDynamicBrakePercent(100);
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.DynamicBrakeIncreaseOrThrottleDecreaseFast)
                {
                    if (locomotive.ThrottlePercent > 0)
                    {
                        locomotive.SetThrottlePercent((float)(locomotive.ThrottlePercent - ThrottleDecreaseStepPerSecond * 2 * elapsedClockSeconds));
                        if (locomotive.ThrottlePercent < 0)
                            locomotive.ThrottlePercent = 0;
                    }
                    else
                    {
                        if (locomotive.DynamicBrakePercent < 100)
                        {
                            locomotive.SetDynamicBrakePercent((float)(locomotive.DynamicBrakePercent + DynamicBrakeIncreaseStepPerSecond * 2 * elapsedClockSeconds));
                        }
                        if (locomotive.DynamicBrakePercent > 100)
                            locomotive.SetDynamicBrakePercent(100);
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.SelectedSpeedIncrease)
                {
                    if (locomotive.CruiseControl.ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero &&
                        locomotive.CruiseControl.SelectedMaxAccelerationPercent == 0 && locomotive.CruiseControl.DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed &&
                           locomotive.ThrottleController.CurrentValue == 0 && locomotive.DynamicBrakeController.CurrentValue == 0)
                    {
                        locomotive.CruiseControl.SpeedRegulatorMode = SpeedRegulatorMode.Auto;
                        locomotive.CruiseControl.SpeedRegulatorSelectedSpeedIncrease();
                    }
                }
            }
            else if (haveCruiseControl && ccAutoMode)
            {
                if (locomotive.CruiseControl.CruiseControlLogic == CruiseControlLogic.SpeedOnly)
                {
                    if (ControllerPosition == CruiseControllerPosition.ThrottleIncrease)
                    {
                        if (!locomotive.CruiseControl.ContinuousSpeedIncreasing && movedForward)
                            return;
                        movedForward = true;
                        locomotive.CruiseControl.SelectedSpeedMpS = Math.Max(locomotive.CruiseControl.MinimumSpeedForCCEffectMpS,
                            locomotive.CruiseControl.SelectedSpeedMpS + locomotive.CruiseControl.SpeedRegulatorNominalSpeedStepMpS);
                        if (locomotive.CruiseControl.SelectedSpeedMpS > locomotive.MaxSpeedMpS)
                            locomotive.CruiseControl.SelectedSpeedMpS = locomotive.MaxSpeedMpS;
                    }
                    if (ControllerPosition == CruiseControllerPosition.ThrottleIncreaseFast)
                    {
                        if (!locomotive.CruiseControl.ContinuousSpeedIncreasing && movedForward)
                            return;
                        movedForward = true;
                        locomotive.CruiseControl.SelectedSpeedMpS = Math.Max(locomotive.CruiseControl.MinimumSpeedForCCEffectMpS,
                            locomotive.CruiseControl.SelectedSpeedMpS + locomotive.CruiseControl.SpeedRegulatorNominalSpeedStepMpS * 2);
                        if (locomotive.CruiseControl.SelectedSpeedMpS > locomotive.MaxSpeedMpS)
                            locomotive.CruiseControl.SelectedSpeedMpS = locomotive.MaxSpeedMpS;
                    }
                    if (ControllerPosition == CruiseControllerPosition.ThrottleDecrease)
                    {
                        if (!locomotive.CruiseControl.ContinuousSpeedDecreasing && movedAft)
                            return;
                        movedAft = true;
                        locomotive.CruiseControl.SelectedSpeedMpS = locomotive.CruiseControl.SelectedSpeedMpS - locomotive.CruiseControl.SpeedRegulatorNominalSpeedStepMpS;
                        if (locomotive.CruiseControl.SelectedSpeedMpS < 0)
                            locomotive.CruiseControl.SelectedSpeedMpS = 0;
                        if (locomotive.CruiseControl.MinimumSpeedForCCEffectMpS > 0 && locomotive.CruiseControl.SelectedSpeedMpS < locomotive.CruiseControl.MinimumSpeedForCCEffectMpS)
                            locomotive.CruiseControl.SelectedSpeedMpS = 0;
                    }
                    if (ControllerPosition == CruiseControllerPosition.ThrottleDecreaseFast)
                    {
                        if (!locomotive.CruiseControl.ContinuousSpeedDecreasing && movedAft)
                            return;
                        movedAft = true;
                        locomotive.CruiseControl.SelectedSpeedMpS = locomotive.CruiseControl.SelectedSpeedMpS - locomotive.CruiseControl.SpeedRegulatorNominalSpeedStepMpS * 2;
                        if (locomotive.CruiseControl.SelectedSpeedMpS < 0)
                            locomotive.CruiseControl.SelectedSpeedMpS = 0;
                        if (locomotive.CruiseControl.MinimumSpeedForCCEffectMpS > 0 && locomotive.CruiseControl.SelectedSpeedMpS < locomotive.CruiseControl.MinimumSpeedForCCEffectMpS)
                            locomotive.CruiseControl.SelectedSpeedMpS = 0;
                    }
                    return;
                }
                if (ControllerPosition == CruiseControllerPosition.ThrottleIncrease)
                {
                    isBraking = false;
                    locomotive.CruiseControl.SpeedSelectorMode = SpeedSelectorMode.Start;
                    previousDriveModeWasAddPower = true;
                }
                if (ControllerPosition == CruiseControllerPosition.Neutral)
                {
                    locomotive.CruiseControl.SpeedSelectorMode = SpeedSelectorMode.Neutral;
                }
                if (ControllerPosition == CruiseControllerPosition.Drive)
                {
                    bool applyPower = true;
                    if (isBraking && needPowerUpAfterBrake)
                    {
                        if (locomotive.DynamicBrakePercent < 2)
                        {
                            locomotive.SetDynamicBrakePercent(-1);
                        }
                        if (locomotive.DynamicBrakePercent > 1)
                        {
                            locomotive.SetDynamicBrakePercent(locomotive.DynamicBrakePercent - 1);
                        }
                        if (CanControlTrainBrake)
                        {
                            if (!locomotive.TrainBrakeController.GetStatus().Equals("release", StringComparison.OrdinalIgnoreCase))
                            {
                                locomotive.StartTrainBrakeDecrease(null);
                            }
                            else
                                locomotive.StopTrainBrakeDecrease();
                        }
                        applyPower = false;
                    }
                    if (applyPower)
                        locomotive.CruiseControl.SpeedSelectorMode = SpeedSelectorMode.On;
                }
                if (ControllerPosition == CruiseControllerPosition.DynamicBrakeIncrease)
                {
                    isBraking = true;
                    previousDriveModeWasAddPower = false;
                    locomotive.CruiseControl.SpeedSelectorMode = SpeedSelectorMode.Neutral;
                    if (CanControlTrainBrake)
                    {
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Apply)
                        {
                            locomotive.StartTrainBrakeDecrease(null);
                        }
                        else if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Neutral)
                        {
                            locomotive.StopTrainBrakeDecrease();
                        }
                    }
                    if (locomotive.ThrottlePercent < 1 && locomotive.DynamicBrakePercent < 100)
                    {
                        if (locomotive.DynamicBrakePercent < 0)
                            locomotive.DynamicBrakeChangeActiveState(true);
                        locomotive.SetDynamicBrakePercent(locomotive.DynamicBrakePercent + 1f);
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.DynamicBrakeIncreaseFast)
                {
                    isBraking = true;
                    previousDriveModeWasAddPower = false;
                    locomotive.CruiseControl.SpeedSelectorMode = SpeedSelectorMode.Neutral;
                    if (CanControlTrainBrake)
                    {
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Apply)
                        {
                            locomotive.StartTrainBrakeDecrease(null);
                        }
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState == ControllerState.Neutral)
                        {
                            locomotive.StopTrainBrakeDecrease();
                        }
                    }
                    if (locomotive.ThrottlePercent < 1 && locomotive.DynamicBrakePercent < 100)
                    {
                        locomotive.SetDynamicBrakePercent(locomotive.DynamicBrakePercent + 2f);
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.TrainBrakeIncrease)
                {
                    isBraking = true;
                    previousDriveModeWasAddPower = false;
                    locomotive.CruiseControl.SpeedSelectorMode = SpeedSelectorMode.Neutral;
                    if (CanControlTrainBrake)
                    {
                        if (locomotive.TrainBrakeController.TrainBrakeControllerState != ControllerState.Apply)
                        {
                            locomotive.StartTrainBrakeIncrease(null);
                        }
                        else
                        {
                            locomotive.StopTrainBrakeIncrease();
                        }
                    }
                }
                if (ControllerPosition == CruiseControllerPosition.EmergencyBrake)
                {
                    isBraking = true;
                    previousDriveModeWasAddPower = false;
                    locomotive.CruiseControl.SpeedSelectorMode = SpeedSelectorMode.Neutral;
                    EmergencyBrakes();
                    emergencyBrake = true;
                }
                if (ControllerPosition == CruiseControllerPosition.SelectedSpeedIncrease)
                {
                    locomotive.CruiseControl.SpeedRegulatorSelectedSpeedIncrease();
                }
                if (ControllerPosition == CruiseControllerPosition.SelectedSpeedDecrease)
                {
                    locomotive.CruiseControl.SpeedRegulatorSelectedSpeedDecrease();
                }
                if (ControllerPosition == CruiseControllerPosition.SelectSpeedZero)
                {
                    locomotive.CruiseControl.SetSpeed(0);
                }
            }
        }

        public void DoMovement(Movement movement)
        {
            if (movement == Movement.Backward)
                movedForward = false;
            if (movement == Movement.Forward)
                movedAft = false;
            if (movement == Movement.Neutral)
                movedForward = movedAft = false;
            messageDisplayed = false;
            if (currentPosition == CruiseControllerPosition.Undefined)
            {
                foreach (Position pair in positionsList)
                {
                    if (pair.Flag == Simulation.ControllerPosition.Default)
                    {
                        currentPosition = pair.Type;
                        break;
                    }
                }
            }
            if (movement == Movement.Forward)
            {
                noKeyPressed = false;
                checkNeutral = false;
                bool isFirst = true;
                CruiseControllerPosition previous = CruiseControllerPosition.Undefined;
                foreach (Position pair in positionsList)
                {
                    if (pair.Type == currentPosition)
                    {
                        if (isFirst)
                            break;
                        currentPosition = previous;
                        locomotive.SignalEvent(TrainEvent.MPCChangePosition);
                        break;
                    }
                    isFirst = false;
                    previous = pair.Type;
                }
            }
            if (movement == Movement.Backward)
            {
                noKeyPressed = false;
                checkNeutral = false;
                bool selectNext = false;
                foreach (Position pair in positionsList)
                {
                    if (selectNext)
                    {
                        currentPosition = pair.Type;
                        locomotive.SignalEvent(TrainEvent.MPCChangePosition);
                        break;
                    }
                    if (pair.Type == currentPosition)
                        selectNext = true;
                }
            }
            if (movement == Movement.Neutral)
            {
                noKeyPressed = true;
                foreach (Position pair in positionsList)
                {
                    if (pair.Type == currentPosition)
                    {
                        switch (pair.Flag)
                        {
                            case Simulation.ControllerPosition.SpringLoadedBackwards:
                            case Simulation.ControllerPosition.SpringLoadedForwards:
                                checkNeutral = true;
                                elapsedSecondsFromLastChange = 0;
                                break;
                            case Simulation.ControllerPosition.SpringLoadedForwardsImmediately:
                            case Simulation.ControllerPosition.SpringLoadedBackwardsImmediately:
                                if (!MouseInputActive)
                                {
                                    CheckNeutralPosition();
                                    ReloadPositions();
                                }
                                break;
                        }
                    }
                }
            }
        }

        protected void ReloadPositions()
        {
            if (noKeyPressed)
            {
                foreach (Position pair in positionsList)
                {
                    if (pair.Type == currentPosition)
                    {
                        switch (pair.Flag)
                        {
                            case Simulation.ControllerPosition.CCNeedIncreaseAfterAnyBrake:
                                needPowerUpAfterBrake = true;
                                break;
                            case Simulation.ControllerPosition.SpringLoadedForwards:
                            case Simulation.ControllerPosition.SpringLoadedBackwards:
                                if (elapsedSecondsFromLastChange > 0.2)
                                {
                                    elapsedSecondsFromLastChange = 0;
                                    checkNeutral = true;
                                }
                                break;
                        }
                    }
                }
            }
            ControllerPosition = currentPosition;
            if (!messageDisplayed)
            {
                string msg = GetPositionName(currentPosition);
                if (!string.IsNullOrEmpty(msg))
                    simulator.Confirmer.Information(msg);
            }
            messageDisplayed = true;
        }

        protected void CheckNeutralPosition()
        {
            bool setNext = false;
            CruiseControllerPosition previous = CruiseControllerPosition.Undefined;
            foreach (Position pair in positionsList)
            {
                if (setNext)
                {
                    currentPosition = pair.Type;
                    locomotive.SignalEvent(TrainEvent.MPCChangePosition);
                    break;
                }
                if (pair.Type == currentPosition)
                {
                    switch (pair.Flag)
                    {
                        case Simulation.ControllerPosition.SpringLoadedBackwards:
                        case Simulation.ControllerPosition.SpringLoadedBackwardsImmediately:
                            setNext = true;
                            break;
                        case Simulation.ControllerPosition.SpringLoadedForwards:
                        case Simulation.ControllerPosition.SpringLoadedForwardsImmediately:
                            currentPosition = previous;
                            locomotive.SignalEvent(TrainEvent.MPCChangePosition);
                            break;
                    }
                }
                previous = pair.Type;
            }
        }

        private string GetPositionName(CruiseControllerPosition positionType)
        {
            foreach (Position position in positionsList)
            {
                if (position.Type == positionType)
                    return position.Name;
            }
            return string.Empty;
        }

        protected void EmergencyBrakes()
        {
            locomotive.SetThrottlePercent(0);
            locomotive.SetDynamicBrakePercent(100);
            locomotive.TrainBrakeController.EmergencyBrakingPushButton = true;
        }

        public float GetDataOf(CabViewControl cvc)
        {
            float data = 0;
            switch (ControllerPosition)
            {
                case CruiseControllerPosition.ThrottleIncrease:
                    data = 0;
                    break;
                case CruiseControllerPosition.Drive:
                case CruiseControllerPosition.ThrottleHold:
                    data = 1;
                    break;
                case CruiseControllerPosition.Neutral:
                    data = 2;
                    break;
                case CruiseControllerPosition.DynamicBrakeIncrease:
                    data = 3;
                    break;
                case CruiseControllerPosition.TrainBrakeIncrease:
                    data = 4;
                    break;
                case CruiseControllerPosition.EmergencyBrake:
                case CruiseControllerPosition.DynamicBrakeIncreaseFast:
                    data = 5;
                    break;
                case CruiseControllerPosition.ThrottleIncreaseFast:
                    data = 6;
                    break;
                case CruiseControllerPosition.ThrottleDecrease:
                    data = 7;
                    break;
                case CruiseControllerPosition.ThrottleDecreaseFast:
                    data = 8;
                    break;
                case CruiseControllerPosition.SelectedSpeedIncrease:
                    data = 9;
                    break;
                case CruiseControllerPosition.SelectedSpeedDecrease:
                    data = 10;
                    break;
                case CruiseControllerPosition.SelectSpeedZero:
                    data = 11;
                    break;
            }
            return data;
        }

        private class Position
        {
            public CruiseControllerPosition Type { get; }
            public ControllerPosition Flag { get; }
            public string Name { get; }

            public Position(string positionType, string positionFlag, string name)
            {
                if (EnumExtension.GetValue(positionType, out CruiseControllerPosition type))
                    Type = type;
                if (!EnumExtension.GetValue(positionFlag, out ControllerPosition controllerPosition))
                {
                    if (string.Equals(positionFlag, "cruisecontrol.needincreaseafteranybrake", StringComparison.OrdinalIgnoreCase))
                    {
                        controllerPosition = Simulation.ControllerPosition.CCNeedIncreaseAfterAnyBrake;
                    }
                }
                Flag = controllerPosition;
                Name = name;
            }
        }
    }
}