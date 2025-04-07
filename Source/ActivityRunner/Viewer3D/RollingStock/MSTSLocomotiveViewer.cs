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

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.RollingStock.CabView;
using Orts.ActivityRunner.Viewer3D.RollingStock.SubSystems;
using Orts.ActivityRunner.Viewer3D.Sound;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.Activities;
using Orts.Simulation.Commanding;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Multiplayer.Messaging;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;
using Orts.Simulation.RollingStocks.SubSystems.Controllers;
using Orts.Simulation.World;

namespace Orts.ActivityRunner.Viewer3D.RollingStock
{
    public class MSTSLocomotiveViewer : MSTSWagonViewer
    {
        private readonly MSTSLocomotive locomotive;

        public bool HasCabRenderer { get; private set; }
        public bool Has3DCabRenderer { get; private set; }
        public CabRenderer CabRenderer { get; private set; }
        public CabViewer3D CabViewer3D { get; private set; }
        public CabRenderer CabRenderer3D { get; internal set; } //allow user to have different setting of .cvf file under CABVIEW3D

        private bool emergencyButtonPressed;

        private readonly CruiseControlViewer cruiseControlViewer;

        public MSTSLocomotiveViewer(Viewer viewer, MSTSLocomotive car)
            : base(viewer, car)
        {
            locomotive = car;

            if (locomotive.CabSoundFileName != null)
                LoadCarSound(Path.GetDirectoryName(locomotive.WagFilePath), locomotive.CabSoundFileName);

            if (locomotive.TrainControlSystem != null && locomotive.TrainControlSystem.Sounds.Count > 0)
            {
                foreach (var script in locomotive.TrainControlSystem.Sounds.Keys)
                {
                    try
                    {
                        Viewer.SoundProcess.AddSoundSources(script, new Collection<SoundSourceBase>() {
                            new SoundSource(locomotive, this, locomotive.TrainControlSystem.Sounds[script])});
                    }
                    catch (Exception error) when (error is Exception)
                    {
                        Trace.TraceInformation($"File {locomotive.TrainControlSystem.Sounds[script]} in script of locomotive of train {locomotive.Train.Name} : {error.Message}");
                    }
                }
            }
            if (locomotive.CruiseControl != null)
            {
                cruiseControlViewer = new CruiseControlViewer(viewer, locomotive);
            }
        }

        protected virtual void StartGearBoxIncrease()
        {
            if (locomotive.GearBoxController != null)
                locomotive.StartGearBoxIncrease();
        }

        protected virtual void StopGearBoxIncrease()
        {
            if (locomotive.GearBoxController != null)
                locomotive.StopGearBoxIncrease();
        }

        protected virtual void StartGearBoxDecrease()
        {
            if (locomotive.GearBoxController != null)
                locomotive.StartGearBoxDecrease();
        }

        protected virtual void StopGearBoxDecrease()
        {
            if (locomotive.GearBoxController != null)
                locomotive.StopGearBoxDecrease();
        }

        protected virtual void ReverserControlForwards()
        {
            if (locomotive.Direction != MidpointDirection.Forward
            && (locomotive.ThrottlePercent >= 1
            || Math.Abs(locomotive.SpeedMpS) > 1))
            {
                Viewer.Simulator.Confirmer.Warning(CabControl.Reverser, CabSetting.Warn1);
                return;
            }
            _ = new ReverserCommand(Viewer.Log, true);    // No harm in trying to engage Forward when already engaged.
        }

        protected virtual void ReverserControlBackwards()
        {
            if (locomotive.Direction != MidpointDirection.Reverse
            && (locomotive.ThrottlePercent >= 1
            || Math.Abs(locomotive.SpeedMpS) > 1))
            {
                Viewer.Simulator.Confirmer.Warning(CabControl.Reverser, CabSetting.Warn1);
                return;
            }
            _ = new ReverserCommand(Viewer.Log, false);    // No harm in trying to engage Reverse when already engaged.
        }

        /// <summary>
        /// A keyboard or mouse click has occurred. Read the UserInput
        /// structure to determine what was pressed.
        /// </summary>
        public override void HandleUserInput(in ElapsedTime elapsedTime)
        {
            //Debrief eval
            if (!emergencyButtonPressed && locomotive.EmergencyButtonPressed && locomotive.IsPlayerTrain)
            {
                if (Math.Abs(locomotive.SpeedMpS) == 0)
                    ActivityEvaluation.Instance.EmergencyButtonStopped++;
                else
                    ActivityEvaluation.Instance.EmergencyButtonMoving++;
                emergencyButtonPressed = true;
            }
            if (emergencyButtonPressed && !locomotive.EmergencyButtonPressed)
                emergencyButtonPressed = false;
        }

        public override void RegisterUserCommandHandling()
        {
            Viewer.UserCommandController.AddEvent(UserCommand.CameraToggleShowCab, KeyEventType.KeyPressed, ShowCabCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.DebugResetWheelSlip, KeyEventType.KeyPressed, DebugResetWheelSlipCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.DebugToggleAdvancedAdhesion, KeyEventType.KeyPressed, DebugToggleAdvancedAdhesionCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlReverserForward, KeyEventType.KeyPressed, ReverserControlForwards, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyPressed, ReverserControlBackwards, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlThrottleIncrease, KeyEventType.KeyPressed, locomotive.StartThrottleIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlThrottleDecrease, KeyEventType.KeyPressed, locomotive.StartThrottleDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlThrottleIncrease, KeyEventType.KeyReleased, locomotive.StopThrottleIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlThrottleDecrease, KeyEventType.KeyReleased, locomotive.StopThrottleDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlThrottleZero, KeyEventType.KeyPressed, locomotive.ThrottleToZero, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTrainBrakeIncrease, KeyEventType.KeyPressed, locomotive.StartTrainBrakeIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTrainBrakeDecrease, KeyEventType.KeyPressed, locomotive.StartTrainBrakeDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTrainBrakeIncrease, KeyEventType.KeyReleased, locomotive.StopTrainBrakeIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTrainBrakeDecrease, KeyEventType.KeyReleased, locomotive.StopTrainBrakeDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTrainBrakeZero, KeyEventType.KeyPressed, locomotive.StartTrainBrakeZero, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlEngineBrakeIncrease, KeyEventType.KeyPressed, locomotive.StartEngineBrakeIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlEngineBrakeDecrease, KeyEventType.KeyPressed, locomotive.StartEngineBrakeDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlEngineBrakeIncrease, KeyEventType.KeyReleased, locomotive.StopEngineBrakeIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlEngineBrakeDecrease, KeyEventType.KeyReleased, locomotive.StopEngineBrakeDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBrakemanBrakeIncrease, KeyEventType.KeyPressed, locomotive.StartBrakemanBrakeIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBrakemanBrakeDecrease, KeyEventType.KeyPressed, locomotive.StartBrakemanBrakeDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBrakemanBrakeIncrease, KeyEventType.KeyReleased, locomotive.StopBrakemanBrakeIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBrakemanBrakeDecrease, KeyEventType.KeyReleased, locomotive.StopBrakemanBrakeDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDynamicBrakeIncrease, KeyEventType.KeyPressed, locomotive.StartDynamicBrakeIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDynamicBrakeDecrease, KeyEventType.KeyPressed, locomotive.StartDynamicBrakeDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDynamicBrakeIncrease, KeyEventType.KeyReleased, locomotive.StopDynamicBrakeIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDynamicBrakeDecrease, KeyEventType.KeyReleased, locomotive.StopDynamicBrakeDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlGearUp, KeyEventType.KeyPressed, StartGearBoxIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlGearDown, KeyEventType.KeyPressed, StartGearBoxDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlGearUp, KeyEventType.KeyReleased, StopGearBoxIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlGearDown, KeyEventType.KeyReleased, StopGearBoxDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSteamHeatIncrease, KeyEventType.KeyPressed, locomotive.StartSteamHeatIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSteamHeatDecrease, KeyEventType.KeyPressed, locomotive.StartSteamHeatDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSteamHeatIncrease, KeyEventType.KeyReleased, locomotive.StopSteamHeatIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSteamHeatDecrease, KeyEventType.KeyReleased, locomotive.StopSteamHeatDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBailOff, KeyEventType.KeyPressed, BailOffOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBailOff, KeyEventType.KeyReleased, BailOffOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInitializeBrakes, KeyEventType.KeyPressed, InitializeBrakesCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlHandbrakeNone, KeyEventType.KeyPressed, HandbrakeNoneCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlHandbrakeFull, KeyEventType.KeyPressed, HandbrakeFullCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlRetainersOff, KeyEventType.KeyPressed, RetainersOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlRetainersOn, KeyEventType.KeyPressed, RetainersOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBrakeHoseConnect, KeyEventType.KeyPressed, BrakeHoseConnectCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBrakeHoseDisconnect, KeyEventType.KeyPressed, BrakeHoseDisconnectCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlEmergencyPushButton, KeyEventType.KeyPressed, EmergencyPushButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlEOTEmergencyBrake, KeyEventType.KeyPressed, EOTEmergencyBrakeCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSander, KeyEventType.KeyPressed, SanderOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSander, KeyEventType.KeyReleased, SanderOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSanderToggle, KeyEventType.KeyPressed, SanderToogleCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlWiper, KeyEventType.KeyPressed, WiperCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlHorn, KeyEventType.KeyPressed, HornOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlHorn, KeyEventType.KeyReleased, HornOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBell, KeyEventType.KeyPressed, BellOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBell, KeyEventType.KeyReleased, BellOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBellToggle, KeyEventType.KeyPressed, BellToggleCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlAlerter, KeyEventType.KeyPressed, AlerterOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlAlerter, KeyEventType.KeyReleased, AlerterOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlHeadlightIncrease, KeyEventType.KeyPressed, HeadlightIncreaseCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlHeadlightDecrease, KeyEventType.KeyPressed, HeadlightDecreaseCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlLight, KeyEventType.KeyPressed, ToggleCabLightCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlRefill, KeyEventType.KeyPressed, AttemptToRefillOrUnload, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlRefill, KeyEventType.KeyReleased, StopRefillingOrUnloading, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDiscreteUnload, KeyEventType.KeyPressed, AttemptToRefillOrUnloadContainer, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDiscreteUnload, KeyEventType.KeyReleased, StopRefillingOrUnloadingContainer, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlImmediateRefill, KeyEventType.KeyPressed, ImmediateRefill, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlImmediateRefill, KeyEventType.KeyReleased, StopImmediateRefilling, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlWaterScoop, KeyEventType.KeyPressed, ToggleWaterScoopCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlOdoMeterReset, KeyEventType.KeyPressed, ResetOdometerOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlOdoMeterReset, KeyEventType.KeyReleased, ResetOdometerOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlOdoMeterDirection, KeyEventType.KeyPressed, ToggleOdometerDirectionCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlCabRadio, KeyEventType.KeyPressed, CabRadioCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDieselHelper, KeyEventType.KeyPressed, ToggleHelpersEngineCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlGenericItem1, KeyEventType.KeyPressed, ToggleGenericCommand1, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlGenericItem2, KeyEventType.KeyPressed, ToggleGenericCommand2, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTCSGeneric1, KeyEventType.KeyPressed, TCSGenericCommand1On, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTCSGeneric1, KeyEventType.KeyReleased, TCSGenericCommand1Off, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTCSGeneric2, KeyEventType.KeyPressed, TCSGenericCommand2On, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTCSGeneric2, KeyEventType.KeyReleased, TCSGenericCommand2Off, true);

            //Distributed power
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDistributedPowerMoveToFront, KeyEventType.KeyPressed, DistributedPowerMoveToFront, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDistributedPowerMoveToBack, KeyEventType.KeyPressed, DistributedPowerMoveToBack, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDistributedPOwerTraction, KeyEventType.KeyPressed, DistributedPowerTraction, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDistributedPowerIdle, KeyEventType.KeyPressed, DistributedPowerIdle, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDistributedPowerBrake, KeyEventType.KeyPressed, DistributedPowerBrake, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDistributedIncrease, KeyEventType.KeyPressed, DistributedPowerIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDistributedPowerDecrease, KeyEventType.KeyPressed, DistributedPowerDecrease, true);

            Viewer.UserCommandController.AddEvent(AnalogUserCommand.Light, LightSwitchCommand);
            Viewer.UserCommandController.AddEvent(AnalogUserCommand.Wiper, WiperSwitchCommand);
            Viewer.UserCommandController.AddEvent(AnalogUserCommand.Direction, DirectionHandleCommand);
            Viewer.UserCommandController.AddEvent(AnalogUserCommand.Throttle, ThrottleHandleCommand);
            Viewer.UserCommandController.AddEvent(AnalogUserCommand.DynamicBrake, DynamicBrakeHandleCommand);
            Viewer.UserCommandController.AddEvent(AnalogUserCommand.TrainBrake, TrainBrakeHandleCommand);
            Viewer.UserCommandController.AddEvent(AnalogUserCommand.EngineBrake, EngineBrakeHandleCommand);
            Viewer.UserCommandController.AddEvent(AnalogUserCommand.BailOff, BailOffHandleCommand);
            Viewer.UserCommandController.AddEvent(AnalogUserCommand.Emergency, EmergencyHandleCommand);
            Viewer.UserCommandController.AddEvent(AnalogUserCommand.CabActivity, AlerterResetCommand);

            cruiseControlViewer?.RegisterUserCommandHandling();
            base.RegisterUserCommandHandling();
        }

        public override void UnregisterUserCommandHandling()
        {
            Viewer.UserCommandController.RemoveEvent(UserCommand.CameraToggleShowCab, KeyEventType.KeyPressed, ShowCabCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.DebugResetWheelSlip, KeyEventType.KeyPressed, DebugResetWheelSlipCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.DebugToggleAdvancedAdhesion, KeyEventType.KeyPressed, DebugToggleAdvancedAdhesionCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserForward, KeyEventType.KeyPressed, ReverserControlForwards);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyPressed, ReverserControlBackwards);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlThrottleIncrease, KeyEventType.KeyPressed, locomotive.StartThrottleIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlThrottleDecrease, KeyEventType.KeyPressed, locomotive.StartThrottleDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlThrottleIncrease, KeyEventType.KeyReleased, locomotive.StopThrottleIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlThrottleDecrease, KeyEventType.KeyReleased, locomotive.StopThrottleDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlThrottleZero, KeyEventType.KeyPressed, locomotive.ThrottleToZero);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTrainBrakeIncrease, KeyEventType.KeyPressed, locomotive.StartTrainBrakeIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTrainBrakeDecrease, KeyEventType.KeyPressed, locomotive.StartTrainBrakeDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTrainBrakeIncrease, KeyEventType.KeyReleased, locomotive.StopTrainBrakeIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTrainBrakeDecrease, KeyEventType.KeyReleased, locomotive.StopTrainBrakeDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTrainBrakeZero, KeyEventType.KeyPressed, locomotive.StartTrainBrakeZero);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlEngineBrakeIncrease, KeyEventType.KeyPressed, locomotive.StartEngineBrakeIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlEngineBrakeDecrease, KeyEventType.KeyPressed, locomotive.StartEngineBrakeDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlEngineBrakeIncrease, KeyEventType.KeyReleased, locomotive.StopEngineBrakeIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlEngineBrakeDecrease, KeyEventType.KeyReleased, locomotive.StopEngineBrakeDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBrakemanBrakeIncrease, KeyEventType.KeyPressed, locomotive.StartBrakemanBrakeIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBrakemanBrakeDecrease, KeyEventType.KeyPressed, locomotive.StartBrakemanBrakeDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBrakemanBrakeIncrease, KeyEventType.KeyReleased, locomotive.StopBrakemanBrakeIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBrakemanBrakeDecrease, KeyEventType.KeyReleased, locomotive.StopBrakemanBrakeDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDynamicBrakeIncrease, KeyEventType.KeyPressed, locomotive.StartDynamicBrakeIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDynamicBrakeDecrease, KeyEventType.KeyPressed, locomotive.StartDynamicBrakeDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDynamicBrakeIncrease, KeyEventType.KeyReleased, locomotive.StopDynamicBrakeIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDynamicBrakeDecrease, KeyEventType.KeyReleased, locomotive.StopDynamicBrakeDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlGearUp, KeyEventType.KeyPressed, StartGearBoxIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlGearDown, KeyEventType.KeyPressed, StartGearBoxDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlGearUp, KeyEventType.KeyReleased, StopGearBoxIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlGearDown, KeyEventType.KeyReleased, StopGearBoxDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSteamHeatIncrease, KeyEventType.KeyPressed, locomotive.StartSteamHeatIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSteamHeatDecrease, KeyEventType.KeyPressed, locomotive.StartSteamHeatDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSteamHeatIncrease, KeyEventType.KeyReleased, locomotive.StopSteamHeatIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSteamHeatDecrease, KeyEventType.KeyReleased, locomotive.StopSteamHeatDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBailOff, KeyEventType.KeyPressed, BailOffOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBailOff, KeyEventType.KeyReleased, BailOffOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInitializeBrakes, KeyEventType.KeyPressed, InitializeBrakesCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlHandbrakeNone, KeyEventType.KeyPressed, HandbrakeNoneCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlHandbrakeFull, KeyEventType.KeyPressed, HandbrakeFullCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlRetainersOff, KeyEventType.KeyPressed, RetainersOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlRetainersOn, KeyEventType.KeyPressed, RetainersOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBrakeHoseConnect, KeyEventType.KeyPressed, BrakeHoseConnectCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBrakeHoseDisconnect, KeyEventType.KeyPressed, BrakeHoseDisconnectCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlEmergencyPushButton, KeyEventType.KeyPressed, EmergencyPushButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlEOTEmergencyBrake, KeyEventType.KeyPressed, EOTEmergencyBrakeCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSander, KeyEventType.KeyPressed, SanderOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSander, KeyEventType.KeyReleased, SanderOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSanderToggle, KeyEventType.KeyPressed, SanderToogleCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlWiper, KeyEventType.KeyPressed, WiperCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlHorn, KeyEventType.KeyPressed, HornOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlHorn, KeyEventType.KeyReleased, HornOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBell, KeyEventType.KeyPressed, BellOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBell, KeyEventType.KeyReleased, BellOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBellToggle, KeyEventType.KeyPressed, BellToggleCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlAlerter, KeyEventType.KeyPressed, AlerterOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlAlerter, KeyEventType.KeyReleased, AlerterOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlHeadlightIncrease, KeyEventType.KeyPressed, HeadlightIncreaseCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlHeadlightDecrease, KeyEventType.KeyPressed, HeadlightDecreaseCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlLight, KeyEventType.KeyPressed, ToggleCabLightCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlRefill, KeyEventType.KeyPressed, AttemptToRefillOrUnload);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlRefill, KeyEventType.KeyReleased, StopRefillingOrUnloading);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDiscreteUnload, KeyEventType.KeyPressed, AttemptToRefillOrUnloadContainer);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDiscreteUnload, KeyEventType.KeyReleased, StopRefillingOrUnloadingContainer);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlImmediateRefill, KeyEventType.KeyPressed, ImmediateRefill);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlImmediateRefill, KeyEventType.KeyReleased, StopImmediateRefilling);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlWaterScoop, KeyEventType.KeyPressed, ToggleWaterScoopCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlOdoMeterReset, KeyEventType.KeyPressed, ResetOdometerOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlOdoMeterReset, KeyEventType.KeyReleased, ResetOdometerOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlOdoMeterDirection, KeyEventType.KeyPressed, ToggleOdometerDirectionCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlCabRadio, KeyEventType.KeyPressed, CabRadioCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDieselHelper, KeyEventType.KeyPressed, ToggleHelpersEngineCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlGenericItem1, KeyEventType.KeyPressed, ToggleGenericCommand1);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlGenericItem2, KeyEventType.KeyPressed, ToggleGenericCommand2);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTCSGeneric1, KeyEventType.KeyPressed, TCSGenericCommand1On);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTCSGeneric1, KeyEventType.KeyReleased, TCSGenericCommand1Off);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTCSGeneric2, KeyEventType.KeyPressed, TCSGenericCommand2On);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTCSGeneric2, KeyEventType.KeyReleased, TCSGenericCommand2Off);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDistributedPowerMoveToFront, KeyEventType.KeyPressed, DistributedPowerMoveToFront);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDistributedPowerMoveToBack, KeyEventType.KeyPressed, DistributedPowerMoveToBack);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDistributedPOwerTraction, KeyEventType.KeyPressed, DistributedPowerTraction);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDistributedPowerIdle, KeyEventType.KeyPressed, DistributedPowerIdle);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDistributedPowerBrake, KeyEventType.KeyPressed, DistributedPowerBrake);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDistributedIncrease, KeyEventType.KeyPressed, DistributedPowerIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDistributedPowerDecrease, KeyEventType.KeyPressed, DistributedPowerDecrease);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.Light, LightSwitchCommand);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.Wiper, WiperSwitchCommand);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.Direction, DirectionHandleCommand);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.Throttle, ThrottleHandleCommand);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.DynamicBrake, DynamicBrakeHandleCommand);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.TrainBrake, TrainBrakeHandleCommand);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.EngineBrake, EngineBrakeHandleCommand);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.BailOff, BailOffHandleCommand);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.Emergency, EmergencyHandleCommand);
            Viewer.UserCommandController.RemoveEvent(AnalogUserCommand.CabActivity, AlerterResetCommand);

            cruiseControlViewer?.UnregisterUserCommandHandling();

            base.UnregisterUserCommandHandling();
        }

        #region private event handlers
        // To be able to attach and detach from UserCommandController we need some actual instance
        // therefore can't use anonymous lambda
#pragma warning disable IDE0022 // Use block body for methods
        private void ShowCabCommand() => locomotive.ShowCab = !locomotive.ShowCab;
        private void DebugResetWheelSlipCommand() => locomotive.Train.SignalEvent(TrainEvent.ResetWheelSlip);
        private void DebugToggleAdvancedAdhesionCommand()
        {
            locomotive.Train.SignalEvent(TrainEvent.ResetWheelSlip);
            Viewer.UserSettings.AdvancedAdhesion = !Viewer.UserSettings.AdvancedAdhesion;
        }
        private void BailOffOnCommand() => _ = new BailOffCommand(Viewer.Log, true);
        private void BailOffOffCommand() => _ = new BailOffCommand(Viewer.Log, false);
        private void InitializeBrakesCommand() => _ = new InitializeBrakesCommand(Viewer.Log);
        private void HandbrakeNoneCommand() => _ = new HandbrakeCommand(Viewer.Log, false);
        private void HandbrakeFullCommand() => _ = new HandbrakeCommand(Viewer.Log, true);
        private void RetainersOffCommand() => _ = new RetainersCommand(Viewer.Log, false);
        private void RetainersOnCommand() => _ = new RetainersCommand(Viewer.Log, true);
        private void BrakeHoseConnectCommand() => _ = new BrakeHoseConnectCommand(Viewer.Log, true);
        private void BrakeHoseDisconnectCommand() => _ = new BrakeHoseConnectCommand(Viewer.Log, false);
        private void EmergencyPushButtonCommand() => _ = new EmergencyPushButtonCommand(Viewer.Log, !locomotive.EmergencyButtonPressed);
        private void EOTEmergencyBrakeCommand() => _ = new ToggleEOTEmergencyBrakeCommand(Viewer.Log);
        private void SanderOnCommand() => _ = new SanderCommand(Viewer.Log, true);
        private void SanderOffCommand() => _ = new SanderCommand(Viewer.Log, false);
        private void SanderToogleCommand() => _ = new SanderCommand(Viewer.Log, !locomotive.Sander);
        private void WiperCommand()
        {
            _ = new WipersCommand(Viewer.Log, !locomotive.Wiper);
            MultiPlayerManager.Broadcast(new TrainEventMessage() { TrainEvent = locomotive.Wiper ? TrainEvent.WiperOn : TrainEvent.WiperOff });
        }
        private void HornOnCommand() => _ = new HornCommand(Viewer.Log, true);
        private void HornOffCommand() => _ = new HornCommand(Viewer.Log, false);
        private void BellOnCommand() => _ = new BellCommand(Viewer.Log, true);
        private void BellOffCommand() => _ = new BellCommand(Viewer.Log, false);
        private void BellToggleCommand() => _ = new BellCommand(Viewer.Log, !locomotive.Bell);
        private void AlerterOnCommand() => _ = new AlerterCommand(Viewer.Log, true);
        private void AlerterOffCommand() => _ = new AlerterCommand(Viewer.Log, false);
        private void HeadlightIncreaseCommand()
        {
            _ = new HeadlightCommand(Viewer.Log, true);
            MultiPlayerManager.Broadcast(new TrainEventMessage()
            {
                TrainEvent = MSTSWagon.Headlight switch
                {
                    HeadLightState.HeadlightDimmed => TrainEvent.HeadlightDim,
                    HeadLightState.HeadlightOn => TrainEvent.HeadlightOn,
                    _ => TrainEvent.HeadlightOff,
                }
            });
        }
        private void HeadlightDecreaseCommand()
        {
            _ = new HeadlightCommand(Viewer.Log, false);
            MultiPlayerManager.Broadcast(new TrainEventMessage()
            {
                TrainEvent = MSTSWagon.Headlight switch
                {
                    HeadLightState.HeadlightOff => TrainEvent.HeadlightOff,
                    HeadLightState.HeadlightDimmed => TrainEvent.HeadlightDim,
                    _ => TrainEvent.HeadlightOn,
                }
            });
        }
        private void ToggleCabLightCommand() => _ = new ToggleCabLightCommand(Viewer.Log);
        private void ToggleWaterScoopCommand() => _ = new ToggleWaterScoopCommand(Viewer.Log);
        private void ResetOdometerOnCommand() => _ = new ResetOdometerCommand(Viewer.Log, true);
        private void ResetOdometerOffCommand() => _ = new ResetOdometerCommand(Viewer.Log, false);
        private void ToggleOdometerDirectionCommand() => _ = new ToggleOdometerDirectionCommand(Viewer.Log);
        private void CabRadioCommand() => _ = new CabRadioCommand(Viewer.Log, !locomotive.CabRadioOn);
        private void ToggleHelpersEngineCommand() => _ = new ToggleHelpersEngineCommand(Viewer.Log);
        private void ToggleGenericCommand1() => _ = new ToggleGenericItem1Command(Viewer.Log);
        private void ToggleGenericCommand2() => _ = new ToggleGenericItem2Command(Viewer.Log);
        private void TCSGenericCommand1On()
        {
            _ = new TCSButtonCommand(Viewer.Log, true, 0);
            locomotive.TrainControlSystem.TCSCommandSwitchOn.TryGetValue(0, out bool pressed);
            _ = new TCSSwitchCommand(Viewer.Log, !pressed, 0);
        }
        private void TCSGenericCommand1Off()
        {
            _ = new TCSButtonCommand(Viewer.Log, false, 0);
        }
        private void TCSGenericCommand2On()
        {
            _ = new TCSButtonCommand(Viewer.Log, true, 1);
            locomotive.TrainControlSystem.TCSCommandSwitchOn.TryGetValue(1, out bool pressed);
            _ = new TCSSwitchCommand(Viewer.Log, !pressed, 1);
        }
        private void TCSGenericCommand2Off()
        {
            _ = new TCSButtonCommand(Viewer.Log, false, 1);
        }
        private void DistributedPowerMoveToFront()
        {
            _ = new DistributedPowerMoveToFrontCommand(Viewer.Log);
        }
        private void DistributedPowerMoveToBack()
        {
            _ = new DistributedPowerMoveToBackCommand(Viewer.Log);
        }
        private void DistributedPowerTraction()
        {
            _ = new DistributedPowerTractionCommand(Viewer.Log);
        }
        private void DistributedPowerIdle()
        {
            _ = new DistributedPowerIdleCommand(Viewer.Log);
        }
        private void DistributedPowerBrake()
        {
            _ = new DistributedPowerDynamicBrakeCommand(Viewer.Log);
        }
        private void DistributedPowerIncrease()
        {
            _ = new DistributedPowerIncreaseCommand(Viewer.Log);
        }
        private void DistributedPowerDecrease()
        {
            _ = new DistributedPowerDecreaseCommand(Viewer.Log);
        }

        private void LightSwitchCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<int> switchCommandArgs)
            {
                // changing Headlight more than one step at a time doesn't work for some reason
                if (locomotive.Headlight < HeadLightState.HeadlightOn)
                {
                    locomotive.Headlight = locomotive.Headlight.Next();
                }
                if (locomotive.Headlight > HeadLightState.HeadlightOff)
                {
                    locomotive.Headlight = locomotive.Headlight.Previous();
                }
                locomotive.SignalEvent(locomotive.Headlight switch
                {
                    HeadLightState.HeadlightOff => TrainEvent.HeadlightOff,
                    HeadLightState.HeadlightDimmed => TrainEvent.HeadlightDim,
                    HeadLightState.HeadlightOn => TrainEvent.HeadlightOn,
                    _ => throw new NotImplementedException()
                });
                locomotive.SignalEvent(TrainEvent.LightSwitchToggle);
            }
        }

        private void WiperSwitchCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<int> switchCommandArgs)
            {
                if (switchCommandArgs.Value == 1 && locomotive.Wiper)
                    locomotive.SignalEvent(TrainEvent.WiperOff);
                else if (switchCommandArgs.Value != 1 && !locomotive.Wiper)
                    locomotive.SignalEvent(TrainEvent.WiperOn);
            }
        }

        private protected virtual void DirectionHandleCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<float> handleCommandArgs)
            {
                if (handleCommandArgs.Value > 50)
                    locomotive.SetDirection(MidpointDirection.Forward);
                else if (handleCommandArgs.Value < -50)
                    locomotive.SetDirection(MidpointDirection.Reverse);
                else
                    locomotive.SetDirection(MidpointDirection.N);
            }
        }

        private void ThrottleHandleCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<float> handleCommandArgs)
            {
                locomotive.SetThrottlePercentWithSound(handleCommandArgs.Value);
            }
        }

        private void DynamicBrakeHandleCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<float> handleCommandArgs)
            {
                if (locomotive.CombinedControlType != CombinedControl.ThrottleAir)
                    locomotive.SetDynamicBrakePercentWithSound(handleCommandArgs.Value);
            }
        }

        private void TrainBrakeHandleCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<float> handleCommandArgs)
            {
                locomotive.SetTrainBrakePercent(handleCommandArgs.Value);
            }
        }

        private void EngineBrakeHandleCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<float> handleCommandArgs)
            {
                locomotive.SetEngineBrakePercent(handleCommandArgs.Value);
            }
        }

        private void BailOffHandleCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<bool> handleCommandArgs)
            {
                locomotive.SetBailOff(handleCommandArgs.Value);
            }
        }

        private void EmergencyHandleCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<bool>)
            {
                EmergencyPushButtonCommand();
            }
        }

        private void AlerterResetCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            locomotive.AlerterReset();
        }

#pragma warning restore IDE0022 // Use block body for methods
        #endregion

        /// <summary>
        /// We are about to display a video frame.  Calculate positions for 
        /// animated objects, and add their primitives to the RenderFrame list.
        /// </summary>
        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (Viewer.Camera.AttachedCar == this.MSTSWagon && Viewer.Camera.Style == CameraStyle.Cab3D)
            {
                if (CabViewer3D != null)
                    CabViewer3D.PrepareFrame(frame, elapsedTime);
            }

            // Wipers and bell animation
            wipers.UpdateLoop(locomotive.Wiper, elapsedTime);
            bell.UpdateLoop(locomotive.Bell, elapsedTime, trainCarShape.SharedShape.CustomAnimationFPS);
            item1Continuous.UpdateLoop(locomotive.GenericItem1, elapsedTime, trainCarShape.SharedShape.CustomAnimationFPS);
            item2Continuous.UpdateLoop(locomotive.GenericItem2, elapsedTime, trainCarShape.SharedShape.CustomAnimationFPS);

            // Draw 2D CAB View - by GeorgeS
            if (Viewer.Camera.AttachedCar == this.MSTSWagon &&
                Viewer.Camera.Style == CameraStyle.Cab)
            {
                if (CabRenderer != null)
                    CabRenderer.PrepareFrame(frame, elapsedTime);
            }

            base.PrepareFrame(frame, elapsedTime);
        }

        internal override void LoadForPlayer()
        {
            if (!HasCabRenderer)
            {
                if (locomotive.CabViews[CabViewType.Front]?.CVFFile != null && locomotive.CabViews[CabViewType.Front]?.CVFFile?.Views2D?.Count > 0)
                    CabRenderer = new CabRenderer(Viewer, locomotive);
                HasCabRenderer = true;
            }
            if (!Has3DCabRenderer)
            {
                if (locomotive.CabViewpoints != null)
                {
                    try
                    {
                        CabViewer3D = new CabViewer3D(Viewer, this.locomotive, this);
                        Has3DCabRenderer = true;
                    }
                    catch (Exception error) when (error is Exception)
                    {
                        Trace.TraceWarning("Could not load 3D cab. {0}", error);
                    }
                }
            }
        }

        internal override void Mark()
        {
            CabRenderer?.Mark();
            CabRenderer3D?.Mark();
            CabViewer3D?.Mark();
            base.Mark();
        }

        /// <summary>
        /// Release sounds of TCS if any, but not for player locomotive
        /// </summary>
        public override void Unload()
        {
            if (locomotive.TrainControlSystem != null && locomotive.TrainControlSystem.Sounds.Count > 0)
                foreach (var script in locomotive.TrainControlSystem.Sounds.Keys)
                {
                    Viewer.SoundProcess.RemoveSoundSources(script);
                }
            base.Unload();
        }

        /// <summary>
        /// Finds the pickup point which is closest to the loco or tender that uses coal, water or diesel oil.
        /// Uses that pickup to refill the loco or tender.
        /// Not implemented yet:
        /// 1. allowing for the position of the intake on the wagon/loco.
        /// 2. allowing for the rate at with the pickup can supply.
        /// 3. refilling any but the first loco in the player's train.
        /// 4. refilling AI trains.
        /// 5. animation is in place, but the animated object should be able to swing into place first, then the refueling process begins.
        /// 6. currently ignores locos and tenders without intake points.
        /// The note below may not be accurate since I released a fix that allows the setting of both coal and water level for the tender to be set at start of activity(EK).
        /// Note that the activity class currently parses the initial level of diesel oil, coal and water
        /// but does not use it yet.
        /// Note: With the introduction of the  animated object, I implemented the RefillProcess class as a starter to allow outside classes to use, but
        /// to solve #5 above, its probably best that the processes below be combined in a common class so that both Shapes.cs and FuelPickup.cs can properly keep up with events(EK).
        /// </summary>
        #region Refill loco or tender from pickup points

        private WagonAndMatchingPickup MatchedWagonAndPickup;


        /// <summary>
        /// Holds data for an intake point on a wagon (e.g. tender) or loco and a pickup point which can supply that intake. 
        /// </summary>
        public class WagonAndMatchingPickup
        {
            public PickupObject Pickup;
            public MSTSWagon Wagon;
            public MSTSLocomotive SteamLocomotiveWithTender;
            public IntakePoint IntakePoint;

        }

        /// <summary>
        /// Scans the train's cars for intake points and the world files for pickup refilling points of the same type.
        /// (e.g. "fuelwater").
        /// TODO: Allow for position of intake point within the car. Currently all intake points are assumed to be at the back of the car.
        /// </summary>
        /// <param name="train"></param>
        /// <returns>a combination of intake point and pickup that are closest</returns>
        // <CJComment> Might be better in the MSTSLocomotive class, but can't see the World objects from there. </CJComment>
        private WagonAndMatchingPickup GetMatchingPickup(Train train, bool onlyUnload = false)
        {
            var worldFiles = Viewer.World.Scenery.WorldFiles;
            var shortestD2 = float.MaxValue;
            WagonAndMatchingPickup nearestPickup = null;
            float distanceFromFrontOfTrainM = 0f;
            int index = 0;
            ContainerHandlingStation containerStation = null;
            foreach (var car in train.Cars)
            {
                if (car is MSTSWagon)
                {
                    MSTSWagon wagon = (MSTSWagon)car;
                    foreach (var intake in wagon.IntakePointList)
                    {
                        // TODO Use the value calculated below
                        //if (intake.DistanceFromFrontOfTrainM == null)
                        //{
                        //    intake.DistanceFromFrontOfTrainM = distanceFromFrontOfTrainM + (wagon.LengthM / 2) - intake.OffsetM;
                        //}
                        foreach (var worldFile in worldFiles)
                        {
                            foreach (var pickup in worldFile.PickupList)
                            {
                                if ((wagon.FreightAnimations != null && (wagon.FreightAnimations.FreightType == pickup.PickupType ||
                                    wagon.FreightAnimations.FreightType == PickupType.None) &&
                                    intake.Type == pickup.PickupType)
                                 || (intake.Type == pickup.PickupType && intake.Type > PickupType.FreightSand && (wagon.WagonType == WagonType.Tender || wagon is MSTSLocomotive)))
                                {
                                    if (intake.Type == PickupType.Container)
                                    {
                                        if (!intake.Validity(onlyUnload, pickup, Viewer.Simulator.ContainerManager, wagon.FreightAnimations, out containerStation))
                                            continue;
                                    }

                                    VectorExtension.Transform(new Vector3(0, 0, -intake.OffsetM), car.WorldPosition.XNAMatrix, out Vector3 intakePosition);

                                    WorldLocation intakeLocation = new WorldLocation(car.WorldPosition.Tile, intakePosition.X, intakePosition.Y, -intakePosition.Z);

                                    float d2 = (float)WorldLocation.GetDistanceSquared(intakeLocation, pickup.WorldPosition.WorldLocation);
                                    if (intake.Type == PickupType.Container && containerStation != null &&
                                        (wagon.Train.FrontTDBTraveller.TrackNode.Index == containerStation.TrackNode.Index ||
                                        wagon.Train.RearTDBTraveller.TrackNode.Index == containerStation.TrackNode.Index) &&
                                        d2 < containerStation.MinZSpan * containerStation.MinZSpan)
                                    // for container it's enough if the intake is within the reachable range of the container crane
                                    {
                                        nearestPickup = new WagonAndMatchingPickup
                                        {
                                            Pickup = pickup,
                                            Wagon = wagon,
                                            IntakePoint = intake
                                        };
                                        return nearestPickup;
                                    }
                                    if (d2 < shortestD2)
                                    {
                                        shortestD2 = d2;
                                        nearestPickup = new WagonAndMatchingPickup();
                                        nearestPickup.Pickup = pickup;
                                        nearestPickup.Wagon = wagon;
                                        if (wagon.WagonType == WagonType.Tender)
                                        {
                                            // Normal arrangement would be steam locomotive followed by the tender car.
                                            if (index > 0 && train.Cars[index - 1] is MSTSSteamLocomotive && !wagon.Flipped && !train.Cars[index - 1].Flipped)
                                                nearestPickup.SteamLocomotiveWithTender = train.Cars[index - 1] as MSTSLocomotive;
                                            // but after reversal point or turntable reversal order of cars is reversed too!
                                            else if (index < train.Cars.Count - 1 && train.Cars[index + 1] is MSTSSteamLocomotive && wagon.Flipped && train.Cars[index + 1].Flipped)
                                                nearestPickup.SteamLocomotiveWithTender = train.Cars[index + 1] as MSTSLocomotive;
                                            else if (index > 0 && train.Cars[index - 1] is MSTSSteamLocomotive)
                                                nearestPickup.SteamLocomotiveWithTender = train.Cars[index - 1] as MSTSLocomotive;
                                            else if (index < train.Cars.Count - 1 && train.Cars[index + 1] is MSTSSteamLocomotive)
                                                nearestPickup.SteamLocomotiveWithTender = train.Cars[index + 1] as MSTSLocomotive;
                                        }
                                        nearestPickup.IntakePoint = intake;
                                    }
                                }
                            }
                        }
                    }
                    distanceFromFrontOfTrainM += wagon.CarLengthM;
                }
                index++;
            }
            return nearestPickup;
        }

        /// <summary>
        /// Returns 
        /// TODO Allow for position of intake point within the car. Currently all intake points are assumed to be at the back of the car.
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        private float GetDistanceToM(WagonAndMatchingPickup match)
        {
            VectorExtension.Transform(new Vector3(0, 0, -match.IntakePoint.OffsetM), match.Wagon.WorldPosition.XNAMatrix, out Vector3 intakePosition);

            var intakeLocation = new WorldLocation(match.Wagon.WorldPosition.Tile, intakePosition.X, intakePosition.Y, -intakePosition.Z);

            return (float)Math.Sqrt(WorldLocation.GetDistanceSquared(intakeLocation, match.Pickup.WorldPosition.WorldLocation));
        }

        /// <summary>
        // This process is tied to the Shift T key combination
        // The purpose of is to perform immediate refueling without having to pull up alongside the fueling station.
        /// </summary>
        public void ImmediateRefill()
        {
            var loco = this.locomotive;

            if (loco == null)
                return;

            foreach (var car in loco.Train.Cars)
            {
                // There is no need to check for the tender.  The MSTSSteamLocomotive is the primary key in the refueling process when using immediate refueling.
                // Electric locomotives may have steam heat boilers fitted, and they can refill these
                if (car is MSTSDieselLocomotive || car is MSTSSteamLocomotive || (car is MSTSElectricLocomotive && loco.IsSteamHeatFitted))
                {
                    Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Refill: Immediate refill process selected, refilling immediately."));
                    (car as MSTSLocomotive).RefillImmediately();
                }
            }
        }


        public void AttemptToRefillOrUnloadContainer()
        {
            AttemptToRefillOrUnload(true);
        }

        public void AttemptToRefillOrUnload()
        {
            AttemptToRefillOrUnload(false);
        }

        /// <summary>
        /// Prompts if cannot refill yet, else starts continuous refilling.
        /// Tries to find the nearest supply (pickup point) which can refill the locos and tenders in the train.  
        /// </summary>
        public void AttemptToRefillOrUnload(bool onlyUnload)
        {
            MatchedWagonAndPickup = null;   // Ensures that releasing the T key doesn't do anything unless there is something to do.

            var loco = this.locomotive;

            var match = GetMatchingPickup(loco.Train, onlyUnload);
            if (match == null && !(loco is MSTSElectricLocomotive && loco.IsSteamHeatFitted))
                return;
            if (match == null)
            {
                Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Refill: Electric loco and no pickup. Command rejected"));
                return;
            }

            float distanceToPickupM = GetDistanceToM(match);
            if (match.IntakePoint.LinkedFreightAnim is FreightAnimationDiscrete)
            // for container cranes handle distance management using Z span of crane
            {
                ContainerHandlingStation containerStation = Viewer.Simulator.ContainerManager.ContainerStations.Where(item => item.Key == match.Pickup.TrackItemIds.TrackDbItems[0]).Select(item => item.Value).First();
                if (distanceToPickupM > containerStation.MinZSpan)
                {
                    Simulator.Instance.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Container crane: Distance to {0} supply is {1}.",
                        match.Pickup.PickupType.GetLocalizedDescription(), Viewer.Catalog.GetPluralString("{0} meter", "{0} meters", (long)(distanceToPickupM + 0.5f))));
                    return;
                }
                MSTSWagon.RefillProcess.ActivePickupObjectUID = (int)match.Pickup.UiD;
            }
            else
            {
                distanceToPickupM -= 2.5f; // Deduct an extra 2.5 so that the tedious placement is less of an issue.
                if (distanceToPickupM > match.IntakePoint.WidthM / 2)
                {
                    Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Refill: Distance to {0} supply is {1}.",
                        match.Pickup.PickupType.GetLocalizedDescription(), Viewer.Catalog.GetPluralString("{0} meter", "{0} meters", (long)(distanceToPickupM + 1f))));
                    return;
                }
                if (distanceToPickupM <= match.IntakePoint.WidthM / 2)
                    MSTSWagon.RefillProcess.ActivePickupObjectUID = (int)match.Pickup.UiD;
            }
            if (loco.SpeedMpS != 0 && match.Pickup.SpeedRange.UpperLimit == 0f)
            {
                Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Refill: Loco must be stationary to refill {0}.",
                    match.Pickup.PickupType.GetLocalizedDescription()));
                return;
            }
            if (loco.SpeedMpS < match.Pickup.SpeedRange.LowerLimit)
            {
                Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Refill: Loco speed must exceed {0}.",
                    FormatStrings.FormatSpeedLimit(match.Pickup.SpeedRange.LowerLimit, Viewer.MilepostUnitsMetric)));
                return;
            }
            if (loco.SpeedMpS > match.Pickup.SpeedRange.UpperLimit)
            {
                var speedLimitMpH = Speed.MeterPerSecond.ToMpH(match.Pickup.SpeedRange.UpperLimit);
                Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Refill: Loco speed must not exceed {0}.",
                    FormatStrings.FormatSpeedLimit(match.Pickup.SpeedRange.UpperLimit, Viewer.MilepostUnitsMetric)));
                return;
            }
            if (match.Wagon is MSTSDieselLocomotive || match.Wagon is MSTSSteamLocomotive || match.Wagon is MSTSElectricLocomotive || (match.Wagon.WagonType == WagonType.Tender && match.SteamLocomotiveWithTender != null))
            {
                // Note: The tender contains the intake information, but the steam locomotive includes the controller information that is needed for the refueling process.

                float fraction = 0;

                // classical MSTS Freightanim, handled as usual
                if (match.SteamLocomotiveWithTender != null)
                    fraction = match.SteamLocomotiveWithTender.GetFilledFraction(match.Pickup.PickupType);
                else
                    fraction = match.Wagon.GetFilledFraction(match.Pickup.PickupType);

                if (fraction > 0.99)
                {
                    Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Refill: {0} supply now replenished.",
                        match.Pickup.PickupType.GetLocalizedDescription()));
                    return;
                }
                else
                {
                    MSTSWagon.RefillProcess.OkToRefill = true;
                    if (match.SteamLocomotiveWithTender != null)
                        StartRefilling(match.Pickup, fraction, match.SteamLocomotiveWithTender);
                    else
                        StartRefilling(match.Pickup, fraction, match.Wagon);

                    MatchedWagonAndPickup = match;  // Save away for HandleUserInput() to use when key is released.
                }
            }
            else if (match.Wagon.FreightAnimations?.Animations?.Count > 0 && match.Wagon.FreightAnimations.Animations[0] is FreightAnimationContinuous)
            {
                // freight wagon animation
                var fraction = match.Wagon.GetFilledFraction(match.Pickup.PickupType);
                if (fraction > 0.99 && match.Pickup.Capacity.FeedRateKGpS >= 0)
                {
                    Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Refill: {0} supply now replenished.",
                        match.Pickup.PickupType.GetLocalizedDescription()));
                    return;
                }
                else if (fraction < 0.01 && match.Pickup.Capacity.FeedRateKGpS < 0)
                {
                    Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString("Unload: {0} fuel or freight now unloaded.",
                        match.Pickup.PickupType.GetLocalizedDescription()));
                    return;
                }
                else
                {
                    MSTSWagon.RefillProcess.OkToRefill = true;
                    MSTSWagon.RefillProcess.Unload = match.Pickup.Capacity.FeedRateKGpS < 0;
                    match.Wagon.StartRefillingOrUnloading(match.Pickup, match.IntakePoint, fraction, MSTSWagon.RefillProcess.Unload);
                    MatchedWagonAndPickup = match;  // Save away for HandleUserInput() to use when key is released.
                }
            }
            else if (match.IntakePoint.LinkedFreightAnim is FreightAnimationDiscrete load)
            {
                // discrete freight wagon animation
                if (load.Loaded && !onlyUnload)
                {
                    Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString($"{match.Pickup.PickupType.GetLocalizedDescription()} now loaded."));
                    return;
                }
                else if (!load.Loaded && onlyUnload)
                {
                    Viewer.Simulator.Confirmer.Message(ConfirmLevel.None, Viewer.Catalog.GetString($"{match.Pickup.PickupType.GetLocalizedDescription()}"));
                    return;
                }

                MSTSWagon.RefillProcess.OkToRefill = true;
                MSTSWagon.RefillProcess.Unload = onlyUnload;
                match.Wagon.StartLoadingOrUnloading(match.Pickup, match.IntakePoint, MSTSWagon.RefillProcess.Unload);
                MatchedWagonAndPickup = match;  // Save away for HandleUserInput() to use when key is released.
            }
        }

        /// <summary>
        /// Called by RefillCommand during replay.
        /// </summary>
        public void RefillChangeTo(float? target)
        {
            MSTSNotchController controller = new MSTSNotchController();
            var loco = this.locomotive;

            var matchedWagonAndPickup = GetMatchingPickup(loco.Train);   // Save away for RefillCommand to use.
            if (matchedWagonAndPickup != null)
            {
                if (matchedWagonAndPickup.SteamLocomotiveWithTender != null)
                    controller = matchedWagonAndPickup.SteamLocomotiveWithTender.GetRefillController(matchedWagonAndPickup.Pickup.PickupType);
                else
                    controller = (matchedWagonAndPickup.Wagon as MSTSLocomotive).GetRefillController(matchedWagonAndPickup.Pickup.PickupType);
                controller.StartIncrease(target);
            }
        }

        /// <summary>
        /// Starts a continuous increase in controlled value. This method also receives TrainCar car to process individual locomotives for refueling.
        /// </summary>
        /// <param name="type">Pickup point</param>
        public void StartRefilling(PickupObject matchPickup, float fraction, TrainCar car)
        {
            var controller = (car as MSTSLocomotive).GetRefillController(matchPickup.PickupType);

            if (controller == null)
            {
                Viewer.Simulator.Confirmer.Message(ConfirmLevel.Error, Viewer.Catalog.GetString("Incompatible pickup type"));
                return;
            }
            (car as MSTSLocomotive).SetStepSize(matchPickup);
            controller.SetValue(fraction);
            controller.CommandStartTime = Viewer.Simulator.ClockTime;  // for Replay to use 
            Viewer.Simulator.Confirmer.Message(ConfirmLevel.Information, Viewer.Catalog.GetString("Starting refill"));
            controller.StartIncrease(controller.MaximumValue);
        }

        /// <summary>
        /// Starts a continuous increase in controlled value.
        /// </summary>
        /// <param name="type">Pickup point</param>
        public void StartRefilling(PickupType type, float fraction)
        {
            var controller = locomotive.GetRefillController(type);
            if (controller == null)
            {
                Viewer.Simulator.Confirmer.Message(ConfirmLevel.Error, Viewer.Catalog.GetString("Incompatible pickup type"));
                return;
            }
            controller.SetValue(fraction);
            controller.CommandStartTime = Viewer.Simulator.ClockTime;  // for Replay to use 
            Viewer.Simulator.Confirmer.Message(ConfirmLevel.Information, Viewer.Catalog.GetString("Starting refill"));
            controller.StartIncrease(controller.MaximumValue);
        }

        /// <summary>
        // Immediate refueling process is different from the process of refueling individual locomotives.
        /// </summary> 
        public void StopImmediateRefilling()
        {
            _ = new ImmediateRefillCommand(Viewer.Log);  // for Replay to use
        }

        public void StopRefillingOrUnloadingContainer()
        {
            StopRefillingOrUnloading(true);
        }

        public void StopRefillingOrUnloading()
        {
            StopRefillingOrUnloading(true);
        }

        /// <summary>
        /// Ends a continuous increase in controlled value.
        /// </summary>
        public void StopRefillingOrUnloading(bool onlyUnload)
        {
            if (MatchedWagonAndPickup == null)
                return;
            if (MatchedWagonAndPickup.Pickup.PickupType == PickupType.Container)
                return;
            MSTSWagon.RefillProcess.OkToRefill = false;
            MSTSWagon.RefillProcess.ActivePickupObjectUID = 0;
            var match = MatchedWagonAndPickup;
            if (MatchedWagonAndPickup.Pickup.PickupType == PickupType.Container)
            {
                match.Wagon.UnloadingPartsOpen = false;
                return;
            }
            var controller = new MSTSNotchController();
            if (match.Wagon is MSTSElectricLocomotive || match.Wagon is MSTSDieselLocomotive || match.Wagon is MSTSSteamLocomotive || (match.Wagon.WagonType == WagonType.Tender && match.SteamLocomotiveWithTender != null))
            {
                if (match.SteamLocomotiveWithTender != null)
                    controller = match.SteamLocomotiveWithTender.GetRefillController(MatchedWagonAndPickup.Pickup.PickupType);
                else
                    controller = (match.Wagon as MSTSLocomotive).GetRefillController(MatchedWagonAndPickup.Pickup.PickupType);
            }
            else
            {
                controller = match.Wagon.WeightLoadController;
                match.Wagon.UnloadingPartsOpen = false;
            }

            _ = new RefillCommand(Viewer.Log, controller.CurrentValue, controller.CommandStartTime);  // for Replay to use
            if (controller.UpdateValue >= 0)
                controller.StopIncrease();
            else
                controller.StopDecrease();
        }
        #endregion
    } // Class LocomotiveViewer
}
