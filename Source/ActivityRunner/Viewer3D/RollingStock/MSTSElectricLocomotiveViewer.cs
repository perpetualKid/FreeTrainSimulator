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

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;

using Orts.Common;
using Orts.Simulation;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;

namespace Orts.ActivityRunner.Viewer3D.RollingStock
{
    public class MSTSElectricLocomotiveViewer : MSTSLocomotiveViewer
    {
        private readonly MSTSElectricLocomotive electricLocomotive;

        public MSTSElectricLocomotiveViewer(Viewer viewer, MSTSElectricLocomotive car)
            : base(viewer, car)
        {
            electricLocomotive = car;
            if (electricLocomotive.Train != null && (electricLocomotive.Train.TrainType == TrainType.Ai ||
                ((electricLocomotive.Train.TrainType == TrainType.Player || electricLocomotive.Train.TrainType == TrainType.AiPlayerDriven || electricLocomotive.Train.TrainType == TrainType.AiPlayerHosting) &&
                (electricLocomotive.Train.MUDirection != MidpointDirection.N && electricLocomotive.LocomotivePowerSupply.MainPowerSupplyOn))))
            // following reactivates the sound triggers related to certain states
            // for pantos the sound trigger related to the raised panto must be reactivated, else SignalEvent() would raise also another panto
            {
                int iPanto = 0;
                foreach (Pantograph panto in electricLocomotive.Pantographs)
                {
                    if (panto.State == PantographState.Up)
                    {
                        switch (iPanto)
                        {
                            case 0: electricLocomotive.SignalEvent(TrainEvent.Pantograph1Up); break;
                            case 1: electricLocomotive.SignalEvent(TrainEvent.Pantograph2Up); break;
                            case 2: electricLocomotive.SignalEvent(TrainEvent.Pantograph3Up); break;
                            case 3: electricLocomotive.SignalEvent(TrainEvent.Pantograph4Up); break;
                            default: electricLocomotive.SignalEvent(TrainEvent.Pantograph1Up); break;
                        }
                    }
                    iPanto++;
                }
                electricLocomotive.SignalEvent(TrainEvent.EnginePowerOn);
                electricLocomotive.SignalEvent(TrainEvent.ReverserToForwardBackward);
                electricLocomotive.SignalEvent(TrainEvent.ReverserChange);
            }
        }

        public override void RegisterUserCommandHandling()
        {
            Viewer.UserCommandController.AddEvent(UserCommand.ControlCircuitBreakerClosingOrder, KeyEventType.KeyPressed, CircuitBreakerClosingOrderOnButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlCircuitBreakerClosingOrder, KeyEventType.KeyReleased, CircuitBreakerClosingOrderOffButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlCircuitBreakerOpeningOrder, KeyEventType.KeyPressed, CircuitBreakerOpeningOrderOnButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlCircuitBreakerOpeningOrder, KeyEventType.KeyReleased, CircuitBreakerOpeningOrderOffButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlCircuitBreakerClosingAuthorization, KeyEventType.KeyPressed, CircuitBreakerClosingAuthorizationCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBatterySwitchClose, KeyEventType.KeyPressed, BatterySwitchCloseOnButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBatterySwitchClose, KeyEventType.KeyReleased, BatterySwitchCloseOffButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBatterySwitchOpen, KeyEventType.KeyPressed, BatterySwitchOpenOnButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBatterySwitchOpen, KeyEventType.KeyReleased, BatterySwitchOpenOffButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlMasterKey, KeyEventType.KeyPressed, MasterKeyButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlElectricTrainSupply, KeyEventType.KeyPressed, ElectricTrainSupplyButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlServiceRetention, KeyEventType.KeyPressed, ServiceRetentionOnButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlServiceRetention, KeyEventType.KeyReleased, ServiceRetentionOffButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlServiceRetentionCancellation, KeyEventType.KeyPressed, ServiceRetentionCancellationOnButtonCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlServiceRetentionCancellation, KeyEventType.KeyReleased, ServiceRetentionCancellationOffButtonCommand, true);

            base.RegisterUserCommandHandling();
        }

        public override void UnregisterUserCommandHandling()
        {
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlCircuitBreakerClosingOrder, KeyEventType.KeyPressed, CircuitBreakerClosingOrderOnButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlCircuitBreakerClosingOrder, KeyEventType.KeyReleased, CircuitBreakerClosingOrderOffButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlCircuitBreakerOpeningOrder, KeyEventType.KeyPressed, CircuitBreakerOpeningOrderOnButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlCircuitBreakerOpeningOrder, KeyEventType.KeyReleased, CircuitBreakerOpeningOrderOffButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlCircuitBreakerClosingAuthorization, KeyEventType.KeyPressed, CircuitBreakerClosingAuthorizationCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBatterySwitchClose, KeyEventType.KeyPressed, BatterySwitchCloseOnButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBatterySwitchClose, KeyEventType.KeyReleased, BatterySwitchCloseOffButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBatterySwitchOpen, KeyEventType.KeyPressed, BatterySwitchOpenOnButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBatterySwitchOpen, KeyEventType.KeyReleased, BatterySwitchOpenOffButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlMasterKey, KeyEventType.KeyPressed, MasterKeyButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlElectricTrainSupply, KeyEventType.KeyPressed, ElectricTrainSupplyButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlServiceRetention, KeyEventType.KeyPressed, ServiceRetentionOnButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlServiceRetention, KeyEventType.KeyReleased, ServiceRetentionOffButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlServiceRetentionCancellation, KeyEventType.KeyPressed, ServiceRetentionCancellationOnButtonCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlServiceRetentionCancellation, KeyEventType.KeyReleased, ServiceRetentionCancellationOffButtonCommand);

            base.UnregisterUserCommandHandling();
        }

        private void CircuitBreakerClosingOrderOffButtonCommand()
        {
            _ = new CircuitBreakerClosingOrderButtonCommand(Viewer.Log, false);
        }

        private void CircuitBreakerClosingOrderOnButtonCommand()
        {
            _ = new CircuitBreakerClosingOrderCommand(Viewer.Log, !electricLocomotive.ElectricPowerSupply.CircuitBreaker.DriverClosingOrder);
            _ = new CircuitBreakerClosingOrderButtonCommand(Viewer.Log, true);
        }

        private void CircuitBreakerOpeningOrderOffButtonCommand()
        {
            _ = new CircuitBreakerOpeningOrderButtonCommand(Viewer.Log, false);
        }

        private void CircuitBreakerOpeningOrderOnButtonCommand()
        {
            _ = new CircuitBreakerOpeningOrderButtonCommand(Viewer.Log, true);
        }

        private void CircuitBreakerClosingAuthorizationCommand()
        {
            _ = new CircuitBreakerClosingAuthorizationCommand(Viewer.Log, !electricLocomotive.ElectricPowerSupply.CircuitBreaker.DriverClosingAuthorization);
        }

        private void BatterySwitchCloseOnButtonCommand()
        {
            _ = new BatterySwitchCloseButtonCommand(Viewer.Log, true);
            _ = new BatterySwitchCommand(Viewer.Log, !electricLocomotive.LocomotivePowerSupply.BatterySwitch.CommandSwitch);
        }

        private void BatterySwitchCloseOffButtonCommand()
        {
            _ = new BatterySwitchCloseButtonCommand(Viewer.Log, false);
        }

        private void BatterySwitchOpenOnButtonCommand()
        {
            _ = new BatterySwitchOpenButtonCommand(Viewer.Log, true);
        }

        private void BatterySwitchOpenOffButtonCommand()
        {
            _ = new BatterySwitchOpenButtonCommand(Viewer.Log, false);
        }

        private void MasterKeyButtonCommand()
        {
            _ = new ToggleMasterKeyCommand(Viewer.Log, !electricLocomotive.LocomotivePowerSupply.MasterKey.CommandSwitch);
        }

        private void ElectricTrainSupplyButtonCommand()
        {
            _ = new ElectricTrainSupplyCommand(Viewer.Log, !electricLocomotive.LocomotivePowerSupply.ElectricTrainSupplySwitch.CommandSwitch);
        }

        private void ServiceRetentionOnButtonCommand()
        {
            _ = new ServiceRetentionButtonCommand(Viewer.Log, true);
        }

        private void ServiceRetentionOffButtonCommand()
        {
            _ = new ServiceRetentionButtonCommand(Viewer.Log, false);
        }

        private void ServiceRetentionCancellationOnButtonCommand()
        {
            _ = new ServiceRetentionButtonCommand(Viewer.Log, true);
        }

        private void ServiceRetentionCancellationOffButtonCommand()
        {
            _ = new ServiceRetentionButtonCommand(Viewer.Log, false);
        }

        /// <summary>
        /// We are about to display a video frame.  Calculate positions for 
        /// animated objects, and add their primitives to the RenderFrame list.
        /// </summary>
        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {

            base.PrepareFrame(frame, elapsedTime);
        }
    }
}
