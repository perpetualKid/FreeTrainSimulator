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
using System.IO;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;

namespace Orts.Simulation.RollingStocks.SubSystems.Controllers
{
    public class MultiPositionController
    {

        private const float FullRangeIncreaseTime = 6.0f;
        private const float DynamicBrakeIncreaseStepPerSecond = 50f;
        private const float DynamicBrakeDecreaseStepPerSecond = 25f;
        private const float ThrottleIncreaseStepPerSecond = 15f;
        private const float ThrottleDecreaseStepPerSecond = 15f;

        private readonly MSTSLocomotive locomotive;
        private readonly Simulator simulator;

        private bool messageDisplayed;

        private float elapsedSecondsFromLastChange;
        private bool checkNeutral;
        private bool noKeyPressed = true;
        private CruiseControllerPosition currentPosition = CruiseControllerPosition.Undefined;
        private bool emergencyBrake;
        private bool previousDriveModeWasAddPower;
        private bool isBraking;
        private bool needPowerUpAfterBrake;
        private bool initialized;
        private bool movedForward;
        private bool movedAft;
        private bool haveCruiseControl;

        private readonly List<Position> positionsList = new List<Position>();
        public bool Equipped { get; set; }
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


        public void Save(BinaryWriter outf)
        {
            outf.Write(this.checkNeutral);
            outf.Write((int)this.ControllerPosition);
            outf.Write((int)this.currentPosition);
            outf.Write(this.elapsedSecondsFromLastChange);
            outf.Write(this.emergencyBrake);
            outf.Write(this.Equipped);
            outf.Write(this.isBraking);
            outf.Write(this.noKeyPressed);
            outf.Write(this.previousDriveModeWasAddPower);
            outf.Write(this.StateChanged);
            outf.Write(haveCruiseControl);
        }

        public void Restore(BinaryReader inf)
        {
            initialized = true;
            checkNeutral = inf.ReadBoolean();
            ControllerPosition = (CruiseControllerPosition)inf.ReadInt32();
            currentPosition = (CruiseControllerPosition)inf.ReadInt32();
            elapsedSecondsFromLastChange = inf.ReadSingle();
            emergencyBrake = inf.ReadBoolean();
            Equipped = inf.ReadBoolean();
            isBraking = inf.ReadBoolean();
            noKeyPressed = inf.ReadBoolean();
            previousDriveModeWasAddPower = inf.ReadBoolean();
            StateChanged = inf.ReadBoolean();
            haveCruiseControl = inf.ReadBoolean();
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            stf.MustMatch("(");
            while (!stf.EndOfBlock())
            {
                stf.ReadItem();
                switch (stf.Tree.ToLower())
                {
                    case "engine(ortsmultipositioncontroller(positions":
                        stf.MustMatch("(");
                        while (!stf.EndOfBlock())
                        {
                            stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("position", ()=>{
                            stf.MustMatch("(");
                            positionsList.Add(new Position(stf.ReadString(), stf.ReadString(), stf.ReadString()));
                        }),
                    });
                        }
                        break;
                    case "engine(ortsmultipositioncontroller(controllerbinding":
                        string binding = stf.ReadStringBlock("null");
                        if (EnumExtension.GetValue(binding, out CruiseControllerBinding controllerBinding))
                            ControllerBinding = controllerBinding;
                        break;
                    case "engine(ortsmultipositioncontroller(controllerid":
                        ControllerId = stf.ReadIntBlock(0);
                        break;
                    case "engine(ortsmultipositioncontrollercancontroltrainbrake":
                        CanControlTrainBrake = stf.ReadBoolBlock(false);
                        break;
                }
            }
        }
        public void Update(double elapsedClockSeconds)
        {
            if (!initialized)
            {
                if (locomotive.CruiseControl != null)
                    haveCruiseControl = true;
                foreach (Position pair in positionsList)
                {
                    if (string.Equals(pair.Flag, "default", StringComparison.OrdinalIgnoreCase))
                    {
                        currentPosition = pair.Type;
                        break;
                    }
                }
                initialized = true;
            }
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
                    locomotive.TrainBrakeController.TCSEmergencyBraking = true;
                    return;
                }
            }
            else
            {
                emergencyBrake = false;
            }
            /*            if (Locomotive.TrainBrakeController.TCSEmergencyBraking)
                            Locomotive.TrainBrakeController.TCSEmergencyBraking = false; */
            elapsedSecondsFromLastChange += (float)elapsedClockSeconds;
            // Simulator.Confirmer.MSG(currentPosition.ToString());
            if (checkNeutral)
            {
                // Check every 200 ms if state of MPC has changed
                if (elapsedSecondsFromLastChange > 0.2f)
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
                        locomotive.CruiseControl.SelectedMaxAccelerationStep == 0 && locomotive.CruiseControl.DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed &&
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
                            if (locomotive.TrainBrakeController.GetStatus().ToLower() != "release")
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
                    if (pair.Flag.ToLower() == "default")
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
                        if (pair.Flag.ToLower() == "springloadedbackwards" || pair.Flag.ToLower() == "springloadedforwards")
                        {
                            checkNeutral = true;
                            elapsedSecondsFromLastChange = 0;
                        }
                        if (pair.Flag.ToLower() == "springloadedbackwardsimmediately" || pair.Flag.ToLower() == "springloadedforwardsimmediately")
                        {
                            if (!MouseInputActive)
                            {
                                CheckNeutralPosition();
                                ReloadPositions();
                            }
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
                        if (string.Equals(pair.Flag, "cruisecontrol.needincreaseafteranybrake", StringComparison.OrdinalIgnoreCase))
                        {
                            needPowerUpAfterBrake = true;
                        }
                        if (string.Equals(pair.Flag, "springloadedforwards", StringComparison.OrdinalIgnoreCase) || string.Equals(pair.Flag, "springloadedbackwards", StringComparison.OrdinalIgnoreCase))
                        {
                            if (elapsedSecondsFromLastChange > 0.2f)
                            {
                                elapsedSecondsFromLastChange = 0;
                                checkNeutral = true;
                            }
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
                    if (string.Equals(pair.Flag, "springloadedbackwards", StringComparison.OrdinalIgnoreCase) || string.Equals(pair.Flag, "springloadedbackwardsimmediately", StringComparison.OrdinalIgnoreCase))
                    {
                        setNext = true;
                    }
                    if (string.Equals(pair.Flag, "springloadedforwards", StringComparison.OrdinalIgnoreCase) || pair.Flag.ToLower() == "springloadedforwardsimmediately")
                    {
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
            locomotive.TrainBrakeController.TCSEmergencyBraking = true;
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
            public string Flag { get; }
            public string Name { get; }

            public Position(string positionType, string positionFlag, string name)
            {
                if (EnumExtension.GetValue(positionType, out CruiseControllerPosition type))
                    Type = type;
                Flag = positionFlag;
                Name = name;
            }
        }
    }
}