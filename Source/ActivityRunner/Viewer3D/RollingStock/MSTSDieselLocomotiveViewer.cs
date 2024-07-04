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

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Input;
using Orts.Simulation;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock
{
    public class MSTSDieselLocomotiveViewer : MSTSLocomotiveViewer
    {
        private MSTSDieselLocomotive dieselLocomotive;

        private List<ParticleEmitterViewer> Exhaust = new List<ParticleEmitterViewer>();

        public MSTSDieselLocomotiveViewer(Viewer viewer, MSTSDieselLocomotive car)
            : base(viewer, car)
        {
            // Now all the particle drawers have been setup, assign them textures based
            // on what emitters we know about.
            dieselLocomotive = car;
            string dieselTexture = viewer.Simulator.RouteFolder.ContentFolder.TextureFile("dieselsmoke.ace");


            // Diesel Exhaust
            foreach (var drawers in from drawer in ParticleDrawers
                                    where drawer.Key.StartsWith("exhaust", StringComparison.OrdinalIgnoreCase)
                                    select drawer.Value)
            {
                Exhaust.AddRange(drawers);
            }
            foreach (var drawer in Exhaust)
                drawer.Initialize(dieselTexture);

            if (dieselLocomotive.Train != null && (dieselLocomotive.Train.TrainType == TrainType.Ai ||
                ((dieselLocomotive.Train.TrainType == TrainType.Player || dieselLocomotive.Train.TrainType == TrainType.AiPlayerDriven || dieselLocomotive.Train.TrainType == TrainType.AiPlayerHosting) &&
                (dieselLocomotive.Train.MUDirection != MidpointDirection.N && dieselLocomotive.DieselEngines[0].State == DieselEngineState.Running))))
            {
                dieselLocomotive.SignalEvent(TrainEvent.ReverserToForwardBackward);
                dieselLocomotive.SignalEvent(TrainEvent.ReverserChange);
            }
        }

        public override void RegisterUserCommandHandling()
        {
            Viewer.UserCommandController.AddEvent(UserCommand.ControlVacuumExhausterPressed, KeyEventType.KeyPressed, VacuumExhausterOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlVacuumExhausterPressed, KeyEventType.KeyReleased, VacuumExhausterOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDieselPlayer, KeyEventType.KeyPressed, TogglePlayerEngineCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBatterySwitchClose, KeyEventType.KeyPressed, BatterySwitchCloseOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBatterySwitchClose, KeyEventType.KeyReleased, BatterySwitchCloseOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBatterySwitchOpen, KeyEventType.KeyPressed, BatterySwitchOpenOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBatterySwitchOpen, KeyEventType.KeyReleased, BatterySwitchOpenOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlMasterKey, KeyEventType.KeyPressed, MasterKeyCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlServiceRetention, KeyEventType.KeyPressed, ServiceRetentionOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlServiceRetention, KeyEventType.KeyReleased, ServiceRetentionOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlServiceRetentionCancellation, KeyEventType.KeyPressed, ServiceRetentionCancellationOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlServiceRetentionCancellation, KeyEventType.KeyReleased, ServiceRetentionCancellationOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlElectricTrainSupply, KeyEventType.KeyPressed, ElectricTrainSupplyButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTractionCutOffRelayClosingOrder, KeyEventType.KeyPressed, TractionCutOffRelayClosingOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTractionCutOffRelayClosingOrder, KeyEventType.KeyReleased, TractionCutOffRelayClosingOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTractionCutOffRelayOpeningOrder, KeyEventType.KeyPressed, TractionCutOffRelayOpeningOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTractionCutOffRelayOpeningOrder, KeyEventType.KeyReleased, TractionCutOffRelayOpeningOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlTractionCutOffRelayClosingAuthorization, KeyEventType.KeyPressed, TractionCutOffRelayClosingAuthorizationButtonCommand, true);
            base.RegisterUserCommandHandling();
        }

        public override void UnregisterUserCommandHandling()
        {
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlVacuumExhausterPressed, KeyEventType.KeyPressed, VacuumExhausterOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlVacuumExhausterPressed, KeyEventType.KeyReleased, VacuumExhausterOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDieselPlayer, KeyEventType.KeyPressed, TogglePlayerEngineCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBatterySwitchClose, KeyEventType.KeyPressed, BatterySwitchCloseOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBatterySwitchClose, KeyEventType.KeyReleased, BatterySwitchCloseOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBatterySwitchOpen, KeyEventType.KeyPressed, BatterySwitchOpenOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBatterySwitchOpen, KeyEventType.KeyReleased, BatterySwitchOpenOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlMasterKey, KeyEventType.KeyPressed, MasterKeyCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlServiceRetention, KeyEventType.KeyPressed, ServiceRetentionOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlServiceRetention, KeyEventType.KeyReleased, ServiceRetentionOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlServiceRetentionCancellation, KeyEventType.KeyPressed, ServiceRetentionCancellationOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlServiceRetentionCancellation, KeyEventType.KeyReleased, ServiceRetentionCancellationOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlElectricTrainSupply, KeyEventType.KeyPressed, ElectricTrainSupplyButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTractionCutOffRelayClosingOrder, KeyEventType.KeyPressed, TractionCutOffRelayClosingOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTractionCutOffRelayClosingOrder, KeyEventType.KeyReleased, TractionCutOffRelayClosingOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTractionCutOffRelayOpeningOrder, KeyEventType.KeyPressed, TractionCutOffRelayOpeningOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTractionCutOffRelayOpeningOrder, KeyEventType.KeyReleased, TractionCutOffRelayOpeningOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTractionCutOffRelayClosingAuthorization, KeyEventType.KeyPressed, TractionCutOffRelayClosingAuthorizationButtonCommand);
            base.UnregisterUserCommandHandling();
        }

        private void VacuumExhausterOnCommand()
        {
            _ = new VacuumExhausterCommand(Viewer.Log, true);
        }

        private void VacuumExhausterOffCommand()
        {
            _ = new VacuumExhausterCommand(Viewer.Log, false);
        }

        private void TogglePlayerEngineCommand()
        {
            _ = new TogglePlayerEngineCommand(Viewer.Log);
        }

        private void BatterySwitchCloseOnCommand()
        {
            _ = new BatterySwitchCloseButtonCommand(Viewer.Log, true);
            _ = new BatterySwitchCommand(Viewer.Log, !dieselLocomotive.LocomotivePowerSupply.BatterySwitch.CommandSwitch);
        }
        private void BatterySwitchCloseOffCommand()
        {
            _ = new BatterySwitchCloseButtonCommand(Viewer.Log, false);
        }

        private void BatterySwitchOpenOnCommand()
        {
            _ = new BatterySwitchOpenButtonCommand(Viewer.Log, true);
        }

        private void BatterySwitchOpenOffCommand()
        {
            _ = new BatterySwitchOpenButtonCommand(Viewer.Log, false);
        }

        private void MasterKeyCommand()
        {
            _ = new ToggleMasterKeyCommand(Viewer.Log, !dieselLocomotive.LocomotivePowerSupply.MasterKey.CommandSwitch);
        }

        private void ServiceRetentionOnCommand()
        {
            _ = new ServiceRetentionButtonCommand(Viewer.Log, true);
        }

        private void ServiceRetentionOffCommand()
        {
            _ = new ServiceRetentionButtonCommand(Viewer.Log, false);
        }

        private void ServiceRetentionCancellationOnCommand()
        {
            _ = new ServiceRetentionCancellationButtonCommand(Viewer.Log, true);
        }

        private void ServiceRetentionCancellationOffCommand()
        {
            _ = new ServiceRetentionCancellationButtonCommand(Viewer.Log, false);
        }

        private void ElectricTrainSupplyButtonCommand()
        {
            _ = new ElectricTrainSupplyCommand(Viewer.Log, !dieselLocomotive.LocomotivePowerSupply.ElectricTrainSupplySwitch.CommandSwitch);
        }

        private void TractionCutOffRelayClosingOnCommand()
        {
            _ = new TractionCutOffRelayClosingOrderCommand(Viewer.Log, !dieselLocomotive.DieselPowerSupply.TractionCutOffRelay.DriverClosingOrder);
            _ = new TractionCutOffRelayClosingOrderButtonCommand(Viewer.Log, true);
        }

        private void TractionCutOffRelayClosingOffCommand()
        {
            _ = new TractionCutOffRelayClosingOrderButtonCommand(Viewer.Log, false);
        }

        private void TractionCutOffRelayOpeningOnCommand()
        {
            _ = new TractionCutOffRelayOpeningOrderButtonCommand(Viewer.Log, true);
        }

        private void TractionCutOffRelayOpeningOffCommand()
        {
            _ = new TractionCutOffRelayOpeningOrderButtonCommand(Viewer.Log, false);
        }

        private void TractionCutOffRelayClosingAuthorizationButtonCommand()
        {
            _ = new TractionCutOffRelayClosingAuthorizationCommand(Viewer.Log, !dieselLocomotive.DieselPowerSupply.TractionCutOffRelay.DriverClosingAuthorization);
        }

        /// <summary>
        /// We are about to display a video frame.  Calculate positions for 
        /// animated objects, and add their primitives to the RenderFrame list.
        /// </summary>
        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var car = this.Car as MSTSDieselLocomotive;

            // Diesel exhaust
            var exhaustParticles = car.Train != null && car.Train.TrainType == TrainType.Static ? 0 : car.ExhaustParticles.SmoothedValue;
            foreach (var drawer in Exhaust)
            {
                var colorR = car.ExhaustColorR.SmoothedValue / 255f;
                var colorG = car.ExhaustColorG.SmoothedValue / 255f;
                var colorB = car.ExhaustColorB.SmoothedValue / 255f;
                drawer.SetOutput((float)exhaustParticles, (float)car.ExhaustMagnitude.SmoothedValue, new Color((byte)car.ExhaustColorR.SmoothedValue, (byte)car.ExhaustColorG.SmoothedValue, (byte)car.ExhaustColorB.SmoothedValue));
            }

            base.PrepareFrame(frame, elapsedTime);
        }
    }
}
